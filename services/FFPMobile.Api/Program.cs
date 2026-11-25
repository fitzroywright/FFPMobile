using System.Net;
using System.Threading.RateLimiting;
using Common.Secrets;
using Common.Security.Abstractions;
using Common.Security.Integration;
using Common.Security.Plugin.ActiveDirectory;
using Common.Security.Services;
using FFPMobile.Api;
using FFPMobile.FfpManagerBridge;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
if (Environment.GetEnvironmentVariable("FFPMOBILE_CONFIG_FILE") is { Length: > 0 } privateConfiguration)
{
    configuration.AddJsonFile(Path.GetFullPath(privateConfiguration), optional: false, reloadOnChange: false);
    configuration.AddEnvironmentVariables();
    configuration.AddCommandLine(args);
}
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 8192);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    if (configuration.GetValue<bool>("Ingress:TrustLoopbackProxy"))
        options.KnownProxies.Add(IPAddress.Loopback);
});
string ConnectionString(string key, string fallback)
{
    var file = Path.GetFullPath(configuration[key] ?? fallback);
    return new SqliteConnectionStringBuilder { DataSource = file, DefaultTimeout = 5 }.ToString();
}

builder.Services.AddCommonAccountStore(_ => new SqliteConnection(
    ConnectionString("Security:SqlitePath", "mobile-accounts.db")), "ffp-mobile");
builder.Services.AddCommonSecurity();
var directorySection = configuration.GetSection("Security:ActiveDirectory");
var directory = directorySection.Get<ActiveDirectoryAuthenticationOptions>()
    ?? throw new InvalidOperationException("Active Directory settings are required.");
if (!string.Equals(directory.Domain, "FFTPJ.LOCAL", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Warehouse authentication requires the FFTPJ.LOCAL directory.");
new ActiveDirectoryAuthenticationPlugin().Register(builder.Services, directorySection);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new DeviceStore(
    ConnectionString("Devices:SqlitePath", "mobile-devices.db"), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<EmployeeAuthentication>();
builder.Services.AddSingleton<InstallationProof>();
builder.Services.AddSingleton<OpaqueAccountSessions<DeviceSession>>();
builder.Services.AddSingleton<MobileSessions>();
if (!string.IsNullOrWhiteSpace(configuration["FfpManager:PermissionServiceOrigin"]))
{
    if (!builder.Environment.IsDevelopment() &&
        configuration.GetValue("CommonSecrets:Mode", SecretsEnvironmentMode.Production) != SecretsEnvironmentMode.Production)
        throw new InvalidOperationException("The FFP Manager bridge requires production OpenBao secret policy.");
    builder.Services.AddCommonSecrets(configuration);
}
builder.Services.AddHttpClient("xaf", client => client.Timeout = TimeSpan.FromSeconds(8))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IWarehousePermissionSource>(sp =>
{
    string? configuredOrigin = configuration["FfpManager:PermissionServiceOrigin"];
    Uri? origin = null;
    if (!string.IsNullOrWhiteSpace(configuredOrigin) &&
        (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out origin) || origin.Scheme != Uri.UriSchemeHttps ||
        origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0))
        throw new InvalidOperationException("FfpManager:PermissionServiceOrigin must be an HTTPS origin.");
    var bindings = configuration.GetSection("FfpManager:Bindings").Get<Dictionary<string, FfpManagerBinding>>() ?? [];
    if (bindings.Values.Any(binding => binding.UserId == Guid.Empty || binding.DirectoryId == Guid.Empty || binding.SubjectId == Guid.Empty) ||
        bindings.Values.Select(binding => binding.UserId).Distinct().Count() != bindings.Count)
        throw new InvalidOperationException("FFP Manager bindings must be complete and unique per XAF user.");
    return new FfpManagerPermissionSource(sp.GetRequiredService<IHttpClientFactory>().CreateClient("xaf"), origin, bindings,
        ct => sp.GetRequiredService<ISecretProvider>().GetRequiredAsync(
            configuration["FfpManager:ServiceTokenSecret"] ?? "ffpmobile-xaf-service-token", ct));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("credentials", ctx => Limit(ctx, 10));
    options.AddPolicy("devices", ctx => Limit(ctx, 120));
    options.AddPolicy("sessions", ctx => Limit(ctx, 300));
});
static RateLimitPartition<string> Limit(HttpContext ctx, int permits) =>
    RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) });

var app = builder.Build();
app.Services.GetRequiredService<DeviceStore>().Initialize();
_ = app.Services.GetRequiredService<IAccountStore>();
_ = app.Services.GetRequiredService<IAuthenticator>();
_ = app.Services.GetRequiredService<IWarehousePermissionSource>();

if (configuration.GetValue<bool>("Ingress:TrustLoopbackProxy")) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseExceptionHandler(handler => handler.Run(context =>
    Results.Json(new { code = "service-unavailable" }, statusCode: 503).ExecuteAsync(context)));
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = 400;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseRateLimiter();
app.MapWarehouseEndpoints();
app.Run();

public partial class Program;

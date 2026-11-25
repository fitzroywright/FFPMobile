using System.Net;
using System.Threading.RateLimiting;
using Common.Secrets;
using Common.Security.Abstractions;
using Common.Security.Authenticators;
using Common.Security.Integration;
using Common.Security.Plugin.ActiveDirectory;
using Common.Security.Services;
using FFPMobile.Api;
using FFPMobile.FfpManagerBridge;
using FFPMobile.Shared.WarehouseProtocol;
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
var erp = configuration.GetSection("FfpManager");
var field = erp.GetSection($"Applications:{Protocol.FieldOfficers}");
var security = erp.GetSection($"Applications:{Protocol.SecurityPost}");
if (new[] { erp, field, security }.Any(section => !string.IsNullOrWhiteSpace(section["PermissionServiceOrigin"])))
{
    if (!builder.Environment.IsDevelopment() &&
        configuration.GetValue("CommonSecrets:Mode", SecretsEnvironmentMode.Production) != SecretsEnvironmentMode.Production)
        throw new InvalidOperationException("The FFP Manager bridge requires production OpenBao secret policy.");
    builder.Services.AddCommonSecrets(configuration);
}
builder.Services.AddHttpClient("xaf", client => client.Timeout = TimeSpan.FromSeconds(8))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IWarehousePermissionSource>(sp => PermissionSource(sp, erp, "warehouse", WarehouseModules.All));
builder.Services.AddKeyedSingleton<IWarehousePermissionSource>(Protocol.FieldOfficers,
    (sp, _) => PermissionSource(sp, field, Protocol.FieldOfficers, OtherMobileModules.FieldOfficers));
builder.Services.AddKeyedSingleton<IWarehousePermissionSource>(Protocol.SecurityPost,
    (sp, _) => PermissionSource(sp, security, Protocol.SecurityPost, OtherMobileModules.SecurityPost));
static IWarehousePermissionSource PermissionSource(IServiceProvider sp, IConfigurationSection section,
    string resource, IReadOnlyList<string> modules)
{
    string? configuredOrigin = section["PermissionServiceOrigin"];
    Uri? origin = null;
    if (!string.IsNullOrWhiteSpace(configuredOrigin) &&
        (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out origin) || origin.Scheme != Uri.UriSchemeHttps ||
        origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0))
        throw new InvalidOperationException($"The FFP Manager origin for {resource} must be an HTTPS origin.");
    var bindings = section.GetSection("Bindings").Get<Dictionary<string, FfpManagerBinding>>() ?? [];
    if (bindings.Values.Any(binding => binding.UserId == Guid.Empty || binding.DirectoryId == Guid.Empty || binding.SubjectId == Guid.Empty) ||
        bindings.Values.Select(binding => binding.UserId).Distinct().Count() != bindings.Count)
        throw new InvalidOperationException("FFP Manager bindings must be complete and unique per XAF user.");
    return new FfpManagerPermissionSource(sp.GetRequiredService<IHttpClientFactory>().CreateClient("xaf"), origin, bindings,
        ct => sp.GetRequiredService<ISecretProvider>().GetRequiredAsync(
            section["ServiceTokenSecret"] ?? "ffpmobile-xaf-service-token", ct), resource, modules);
}
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
_ = app.Services.GetRequiredKeyedService<IWarehousePermissionSource>(Protocol.FieldOfficers);
_ = app.Services.GetRequiredKeyedService<IWarehousePermissionSource>(Protocol.SecurityPost);

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
var mockOptions = MockAuthenticationConfiguration.Read(configuration.GetSection("Security:Mock"),
    app.Environment.EnvironmentName, "ffp-mobile-demo");
if (mockOptions.Enabled)
{
    if (!MockEnvironmentPolicy.IsPermitted(true, app.Environment.EnvironmentName, mockOptions.IsolationConfirmed) ||
        configuration.GetValue<bool>("Ingress:TrustLoopbackProxy") ||
        string.IsNullOrWhiteSpace(mockOptions.Password) || mockOptions.Identities.Count == 0)
        throw new InvalidOperationException("Mock requires an isolated, directly accessed Development or Demo host.");

    var mock = new MockAuthenticator(mockOptions);
    app.MapPost("/api/auth/mock/login", (SessionLogin request, HttpContext context) =>
    {
        var demo = MobileDemos.ForApplication(request.Application);
        if (context.Connection.RemoteIpAddress is not { } remote ||
            !IPAddress.IsLoopback(remote) || demo is null)
            return Results.NotFound();
        if (request.UserName is not { Length: > 0 and <= 256 } ||
            request.Password is not { Length: > 0 and <= 1024 }) return Results.Unauthorized();
        var result = mock.Authenticate(request.UserName, request.Password);
        return result.Identity is { } identity && string.Equals(identity.LoginName, demo.LoginName, StringComparison.OrdinalIgnoreCase)
            ? Results.Ok(new { identity.DisplayName, expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                authorizedModules = demo.Modules })
            : Results.Unauthorized();
    }).RequireRateLimiting("credentials");
}
app.Run();

public partial class Program;

using System.Threading.RateLimiting;
using Common.Security.Abstractions;
using Common.Security.Authenticators;
using Common.Security.Integration;
using Common.Security.Models;
using Common.Security.Services;
using Microsoft.Data.Sqlite;
using FFPMobile.Api;

var builder = WebApplication.CreateBuilder(args);
string dataPath = builder.Configuration["Security:SqlitePath"] ?? "";
if (string.IsNullOrWhiteSpace(dataPath))
    throw new InvalidOperationException("Security:SqlitePath is required.");
builder.Services.AddCommonAccountStore(_ =>
    new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dataPath }.ToString()),
    "ffp-mobile");
builder.Services.AddCommonSecurity();
builder.Services.AddSingleton<IAuthenticator>(sp =>
    new PlatformAuthenticator(
        new LocalDatabaseAuthenticator(sp.GetRequiredService<ILocalCredentialStore>()),
        sp.GetRequiredService<ISecurityEventSink>()));
builder.Services.AddSingleton<OpaqueAccountSessions<string>>();
builder.Services.AddSingleton<IMobilePermissionResolver, DenyAllMobilePermissionResolver>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.MapPost("/api/auth/login", (LoginRequest request,
    IAuthenticator authenticator, IAccountStore accounts,
    OpaqueAccountSessions<string> sessions, IMobilePermissionResolver permissions) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password) ||
        request.UserName.Length > 256 || request.Password.Length > 1024 ||
        request.Application is not ("field-officers" or "warehouse-operations" or "security-post"))
        return Results.BadRequest();

    AuthenticationResult result = authenticator.Authenticate(request.UserName, request.Password);
    if (!result.IsAuthenticated || result.Identity is null) return Results.Unauthorized();

    // Never auto-provision a newly authenticated user or infer immutable bindings by login name.
    SecurityAccount account;
    try { account = accounts.Resolve(result.Identity, AccountPopulation.Staff, false).Account; }
    catch (Exception) { return Results.Forbid(); }
    if (account.Status != SecurityAccountStatus.Active) return Results.Forbid();

    // Permission resolver deliberately fails closed until a real FFP Manager XAF adapter is deployed.
    IReadOnlyList<string> allowed = permissions.GetAuthorizedModules(account.Id, request.Application);
    if (allowed.Count == 0) return Results.Forbid();
    var session = sessions.Create(account.Id, account.SessionVersion,
        request.Application, TimeSpan.FromMinutes(30));
    return Results.Ok(new LoginResponse(session.Token, account.DisplayName, session.ExpiresAt, allowed));
}).RequireRateLimiting("login");

app.MapGet("/api/auth/me", (HttpRequest request,
    OpaqueAccountSessions<string> sessions) =>
{
    string? header = request.Headers.Authorization.ToString();
    string token = header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
        ? header[7..] : string.Empty;
    var session = sessions.Find(token);
    return session is null ? Results.Unauthorized() :
        Results.Ok(new { application = session.Identity, expiresAtUtc = session.ExpiresAt });
});

app.Run();

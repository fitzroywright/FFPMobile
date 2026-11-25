using System.Net.Http.Headers;
using System.Text;
using Common.Diagnostics;
using Common.Security.Abstractions;
using Common.Security.Plugin.ActiveDirectory;
using FFPMobile.FfpManagerBridge;
using FFPMobile.Shared.WarehouseProtocol;

namespace FFPMobile.Api;

public static class WarehouseEndpoints
{
    public static void MapWarehouseEndpoints(this WebApplication app)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FFPMobile.Security");
        var administrators = (app.Configuration.GetSection("Devices:AdministratorAccountIds").Get<string[]>() ?? [])
            .ToHashSet(StringComparer.Ordinal);
        app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
        app.MapGet("/health/ready", async (IAccountStore accounts, DeviceStore devices, IEnumerable<IDependencyDiagnosticProbe> probes,
            IWarehousePermissionSource permissions, CancellationToken ct) =>
        {
            _ = accounts.Find("ffpmobile-readiness-probe");
            _ = devices.Find("ffpmobile-readiness-probe");
            var directory = probes.OfType<ActiveDirectoryDependencyProbe>().SingleOrDefault();
            bool ready = directory is not null && (await directory.VerifyAsync(ct)).Passed && await permissions.IsReadyAsync(ct);
            return Results.Json(new { status = ready ? "ready" : "unavailable" }, statusCode: ready ? 200 : 503);
        });

        app.MapPost("/api/devices/register", (RegisterDevice input, HttpRequest request, InstallationProof proof, DeviceStore devices) =>
        {
            if (input.Application != Protocol.Application || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 80 ||
                input.Name.Any(char.IsControl) || input.PublicKey is null || input.PublicKey.Length > 180)
                return Error(400, "invalid-registration");

            byte[] key;
            try { key = Convert.FromBase64String(input.PublicKey); }
            catch (FormatException) { return Error(400, "invalid-registration"); }

            string id = Protocol.InstallationId(key);
            if (!proof.Verify(request, id, key, "")) return Error(401, "installation-proof-required");
            return Results.Ok(devices.Register(id, input.Name.Trim(), input.Application, key).Details);
        }).RequireRateLimiting("devices");

        app.MapGet("/api/devices/status", (HttpRequest request, InstallationProof proof) =>
            proof.Authenticate(request) is { } device ? Results.Ok(device.Details) : Error(401, "installation-proof-required"))
            .RequireRateLimiting("devices");

        app.MapPost("/api/auth/login", async (SessionLogin input, HttpRequest request, InstallationProof proof,
            EmployeeAuthentication authentication, IWarehousePermissionSource permissions, MobileSessions sessions,
            DeviceStore devices, CancellationToken ct) =>
        {
            if (input.Application != Protocol.Application) return Error(403, "application-not-enabled");
            var device = proof.Authenticate(request);
            if (device is null) return Error(401, "installation-proof-required");
            if (device.Details.Status != DeviceStatus.Approved) return DeviceDenied(device.Details.Status);

            var employee = authentication.Authenticate(input.UserName, input.Password, AccountPopulation.Staff);
            if (employee is null)
            {
                log.LogWarning("Warehouse login rejected for installation {DeviceId}", device.Details.Id);
                return Error(401, "employee-not-authorized");
            }
            var modules = await permissions.ResolveAsync(employee.Identity, ct);
            if (modules.Count == 0)
            {
                log.LogWarning("Warehouse access denied to {AccountId} on {DeviceId}", employee.Account.Id, device.Details.Id);
                return Error(403, "warehouse-access-denied");
            }
            if (!devices.RecordConnection(device.Details.Id, device.Version)) return Error(403, "device-approval-changed");

            var session = sessions.Create(employee, device);
            log.LogInformation("Warehouse session started for {AccountId} on {DeviceId}", employee.Account.Id, device.Details.Id);
            return Results.Ok(new EmployeeSession(session.Token, employee.Account.DisplayName, session.ExpiresAt, modules));
        }).RequireRateLimiting("credentials");

        var authenticated = app.MapGroup("/api/auth").RequireRateLimiting("sessions");
        authenticated.MapGet("/me", async (HttpRequest request, InstallationProof proof, MobileSessions sessions,
            IAccountStore accounts, DeviceStore devices, IWarehousePermissionSource permissions, CancellationToken ct) =>
        {
            var context = await AuthorizeAsync(request, proof, sessions, devices, permissions, ct);
            if (context.Error is not null) return context.Error;
            var session = context.Session!;
            var account = accounts.Find(session.AccountId);
            return account is null ? Error(401, "session-expired") :
                Results.Ok(new EmployeeSession(session.Token, account.DisplayName, session.ExpiresAt, context.Modules!));
        });
        authenticated.MapGet("/permissions", async (HttpRequest request, InstallationProof proof, MobileSessions sessions,
            DeviceStore devices, IWarehousePermissionSource permissions, CancellationToken ct) =>
        {
            var context = await AuthorizeAsync(request, proof, sessions, devices, permissions, ct);
            return context.Error ?? Results.Ok(new { authorizedModules = context.Modules });
        });
        authenticated.MapPost("/refresh", async (HttpRequest request, InstallationProof proof, MobileSessions sessions,
            IAccountStore accounts, DeviceStore devices, IWarehousePermissionSource permissions, CancellationToken ct) =>
        {
            var context = await AuthorizeAsync(request, proof, sessions, devices, permissions, ct);
            if (context.Error is not null) return context.Error;
            var next = sessions.Refresh(context.Session!.Token, context.Device!);
            var account = next is null ? null : accounts.Find(next.AccountId);
            return next is null || account is null ? Error(401, "session-expired") :
                Results.Ok(new EmployeeSession(next.Token, account.DisplayName, next.ExpiresAt, context.Modules!));
        });
        authenticated.MapPost("/logout", (HttpRequest request, InstallationProof proof, MobileSessions sessions) =>
        {
            string token = Bearer(request);
            if (token.Length == 0 || proof.Authenticate(request, token) is not { } device)
                return Error(401, "installation-proof-required");
            var existing = sessions.Find(token, device);
            sessions.Remove(token, device);
            if (existing is not null)
                log.LogInformation("Warehouse session ended for {AccountId} on {DeviceId}", existing.AccountId, device.Details.Id);
            return Results.NoContent();
        });

        var admin = app.MapGroup("/api/admin/devices").RequireRateLimiting("credentials");
        admin.MapGet("", (HttpRequest request, EmployeeAuthentication authentication, DeviceStore devices, string? after) =>
        {
            var actor = Admin(request, authentication, administrators);
            if (actor is null)
            {
                log.LogWarning("Unauthorized device inventory request");
                return Error(403, "device-administration-denied");
            }
            if (after is not null && !InstallationProof.IsDeviceId(after)) return Error(400, "invalid-cursor");
            log.LogInformation("Device inventory viewed by {AccountId}", actor);
            return Results.Ok(devices.List(after ?? ""));
        });
        admin.MapPut("/{id}/status/{status}", (string id, string status, HttpRequest request,
            EmployeeAuthentication authentication, DeviceStore devices) =>
        {
            var actor = Admin(request, authentication, administrators);
            if (actor is null)
            {
                log.LogWarning("Unauthorized device approval request");
                return Error(403, "device-administration-denied");
            }
            if (!InstallationProof.IsDeviceId(id) ||
                !Enum.TryParse<DeviceStatus>(status, true, out var next) ||
                next is not (DeviceStatus.Approved or DeviceStatus.Suspended or DeviceStatus.Revoked) ||
                !string.Equals(next.ToString(), status, StringComparison.OrdinalIgnoreCase))
                return Error(400, "invalid-device-status");
            if (!devices.ChangeStatus(id, next, actor)) return Error(409, "device-status-unchanged");
            log.LogInformation("Installation {DeviceId} changed to {Status} by {AccountId}", id, next, actor);
            return Results.NoContent();
        });
    }

    private sealed record Authorization(IResult? Error, RegisteredDevice? Device = null,
        Common.Security.Services.OpaqueAccountSession<DeviceSession>? Session = null, IReadOnlyList<string>? Modules = null);

    private static async Task<Authorization> AuthorizeAsync(HttpRequest request, InstallationProof proof, MobileSessions sessions,
        DeviceStore devices, IWarehousePermissionSource permissions, CancellationToken ct)
    {
        string token = Bearer(request);
        if (token.Length == 0) return new(Error(401, "session-expired"));
        var device = proof.Authenticate(request, token);
        if (device is null) return new(Error(401, "installation-proof-required"));
        if (device.Details.Status != DeviceStatus.Approved) return new(DeviceDenied(device.Details.Status));
        var session = sessions.Find(token, device);
        if (session is null) return new(Error(401, "session-expired"));
        var modules = await permissions.ResolveAsync(session.Identity.Employee, ct);
        if (modules.Count == 0) return new(Error(403, "warehouse-access-denied"));
        if (sessions.Find(token, device) is null) return new(Error(401, "session-expired"));
        if (!devices.RecordConnection(device.Details.Id, device.Version)) return new(Error(403, "device-approval-changed"));
        return new(null, device, session, modules);
    }

    private static string? Admin(HttpRequest request, EmployeeAuthentication authentication, HashSet<string> allowed)
    {
        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header) ||
            !header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) ||
            header.Parameter is not { Length: > 0 and <= 4096 } encoded)
            return null;
        string value;
        try { value = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
        catch (FormatException) { return null; }
        int split = value.IndexOf(':');
        if (split < 1 || allowed.Count == 0) return null;
        var actor = authentication.Authenticate(value[..split], value[(split + 1)..], AccountPopulation.Administrator);
        return actor is not null && allowed.Contains(actor.Account.Id) ? actor.Account.Id : null;
    }

    private static string Bearer(HttpRequest request) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header) &&
        header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) &&
        header.Parameter is { Length: 64 } token && token.All(Uri.IsHexDigit) ? token : "";

    private static IResult DeviceDenied(DeviceStatus status) => Error(403, status switch
    {
        DeviceStatus.PendingApproval => "device-pending",
        DeviceStatus.Suspended => "device-suspended",
        DeviceStatus.Revoked => "device-revoked",
        _ => "device-not-approved"
    });

    private static IResult Error(int status, string code) => Results.Json(new ApiError(code), statusCode: status);
}

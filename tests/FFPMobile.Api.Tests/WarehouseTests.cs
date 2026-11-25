using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Common.Security.Abstractions;
using Common.Security.Integration;
using Common.Security.Models;
using Common.Security.Services;
using FFPMobile.Api;
using FFPMobile.FfpManagerBridge;
using FFPMobile.Shared.WarehouseProtocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FFPMobile.Tests;

public sealed class WarehouseTests
{
    private static readonly Uri Origin = new("https://localhost/");
    private const string Password = "fixture-password";

    [Fact]
    public async Task Registration_Requires_Approval_And_Admin_Uses_Approved_Identity()
    {
        await using var host = await Host.StartAsync();
        using var tablet = new Tablet(host.Http);
        var first = await tablet.Api.RegisterAsync(Origin, "Warehouse Tablet");
        var repeated = await tablet.Api.RegisterAsync(Origin, "Another name");
        Assert.Equal(first, repeated);
        Assert.Equal(DeviceStatus.PendingApproval, first.Status);
        Assert.Null(first.LastSuccessfulConnectionUtc);
        await Fails("device-pending", () => tablet.Api.LoginAsync(Origin, "alice", Password));
        Assert.Equal(HttpStatusCode.Forbidden, await host.StatusAsync(first.Id, "Approved", "alice"));
        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync(first.Id, "Approved"));
        var session = await tablet.Api.LoginAsync(Origin, "alice", Password);
        Assert.Equal("Alice", session.DisplayName);
        Assert.NotNull((await tablet.Api.StatusAsync(Origin)).LastSuccessfulConnectionUtc);
    }

    [Theory]
    [InlineData("alice", "incorrect", "employee-not-authorized")]
    [InlineData("disabled-in-ad", Password, "employee-not-authorized")]
    [InlineData("disabled-in-common", Password, "employee-not-authorized")]
    [InlineData("unbound", Password, "employee-not-authorized")]
    [InlineData("no-warehouse", Password, "warehouse-access-denied")]
    public async Task Unauthorized_Employees_Are_Denied(string login, string password, string code)
    {
        await using var host = await Host.StartAsync();
        using var tablet = await host.ApproveAsync();
        await Fails(code, () => tablet.Api.LoginAsync(Origin, login, password));
        Assert.Null(host.Accounts.Find("unbound"));
    }

    [Fact]
    public async Task A_Device_Administrator_Needs_Explicit_Warehouse_Enrollment_And_Erp_Permissions()
    {
        await using var defaultHost = await Host.StartAsync();
        using var defaultTablet = await defaultHost.ApproveAsync();
        await Fails("employee-not-authorized", () => defaultTablet.Api.LoginAsync(Origin, "admin", Password));

        await using var approvedHost = await Host.StartAsync(allowAdministratorAsEmployee: true);
        using var tablet = await approvedHost.ApproveAsync();
        approvedHost.Permissions.Modules = [];
        await Fails("warehouse-access-denied", () => tablet.Api.LoginAsync(Origin, "admin", Password));
        approvedHost.Permissions.Modules = [WarehouseModules.StockCounts];
        var session = await tablet.Api.LoginAsync(Origin, "admin", Password);
        Assert.Equal([WarehouseModules.StockCounts], session.AuthorizedModules);
    }

    [Theory]
    [InlineData(Protocol.FieldOfficers)]
    [InlineData(Protocol.SecurityPost)]
    public async Task Other_Applications_Require_Their_Own_Approval_And_Permission_Source(string application)
    {
        await using var host = await Host.StartAsync(allowAdministratorAsEmployee: true);
        using var tablet = new Tablet(host.Http, application);
        var registered = await tablet.Api.RegisterAsync(Origin, "Operations tablet");
        Assert.Equal(application, registered.Application);
        await Fails("device-pending", () => tablet.Api.LoginAsync(Origin, "alice", Password));

        var warehouse = new WarehouseApiClient(host.Http, tablet);
        await Fails("application-mismatch", () => warehouse.RegisterAsync(Origin, "Wrong application"));
        await Fails("application-mismatch", () => warehouse.LoginAsync(Origin, "alice", Password));
        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync(registered.Id, "Approved"));
        await Fails("employee-not-authorized", () => tablet.Api.LoginAsync(Origin, "admin", Password));
        await Fails("application-permissions-unavailable", () => tablet.Api.LoginAsync(Origin, "alice", Password));
        Assert.Equal(application, (await tablet.Api.StatusAsync(Origin)).Application);
    }

    [Theory]
    [InlineData(Protocol.FieldOfficers, "purchase-orders")]
    [InlineData(Protocol.SecurityPost, "dispatch-verification")]
    public async Task An_Application_Uses_Only_Its_Own_Permission_Source(string application, string module)
    {
        await using var host = await Host.StartAsync(application: application, applicationModule: module);
        using var tablet = new Tablet(host.Http, application);
        var registered = await tablet.Api.RegisterAsync(Origin, "Operations tablet");
        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync(registered.Id, "Approved"));
        var session = await tablet.Api.LoginAsync(Origin, "alice", Password);
        Assert.Equal([module], session.AuthorizedModules);
        Assert.Equal([module], (await tablet.Api.ValidateAsync(Origin, session.AccessToken, false)).AuthorizedModules);
    }

    [Fact]
    public async Task A_Security_Post_Administrator_Enrollment_Does_Not_Apply_To_Field_Officers()
    {
        await using var host = await Host.StartAsync(additionalAdministratorApplication: Protocol.SecurityPost);
        using var security = await host.ApproveAsync(Protocol.SecurityPost);
        await Fails("application-permissions-unavailable", () => security.Api.LoginAsync(Origin, "admin", Password));

        using var field = await host.ApproveAsync(Protocol.FieldOfficers);
        await Fails("employee-not-authorized", () => field.Api.LoginAsync(Origin, "admin", Password));
    }

    [Theory]
    [InlineData(WarehouseModules.StockCounts)]
    [InlineData(WarehouseModules.InventoryReports)]
    [InlineData(WarehouseModules.DeliveryOrders)]
    [InlineData(WarehouseModules.ReturnToStock)]
    [InlineData(WarehouseModules.GoodsReceival)]
    public async Task Permission_Decisions_Are_Module_Specific_And_Rechecked(string module)
    {
        await using var host = await Host.StartAsync();
        using var tablet = await host.ApproveAsync();
        host.Permissions.Modules = [module];
        var session = await tablet.Api.LoginAsync(Origin, "alice", Password);
        Assert.Equal([module], session.AuthorizedModules);
        using var request = tablet.SignedGet("/api/auth/permissions", session.AccessToken);
        using var response = await host.Http.SendAsync(request);
        var current = await response.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.Equal([module], current!.AuthorizedModules);
        host.Permissions.Modules = [];
        await Fails("warehouse-access-denied", () => tablet.Api.ValidateAsync(Origin, session.AccessToken, false));
    }

    [Fact]
    public async Task Suspension_And_Revocation_Invalidate_Sessions_And_Revocation_Is_Terminal()
    {
        await using var host = await Host.StartAsync();
        using var tablet = await host.ApproveAsync();
        var session = await tablet.Api.LoginAsync(Origin, "alice", Password);
        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync(tablet.Id, "Suspended"));
        await Fails("device-suspended", () => tablet.Api.ValidateAsync(Origin, session.AccessToken, false));
        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync(tablet.Id, "Approved"));
        await Fails("session-expired", () => tablet.Api.ValidateAsync(Origin, session.AccessToken, false));
        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync(tablet.Id, "Revoked"));
        await Fails("device-revoked", () => tablet.Api.LoginAsync(Origin, "alice", Password));
        Assert.Equal(HttpStatusCode.Conflict, await host.StatusAsync(tablet.Id, "Approved"));
    }

    [Fact]
    public async Task Refresh_Logout_Account_Revocation_And_Expiry_Invalidate_Tokens()
    {
        await using var host = await Host.StartAsync();
        using var tablet = await host.ApproveAsync();
        var api = tablet.Api;
        var initial = await api.LoginAsync(Origin, "alice", Password);
        var refreshed = await api.ValidateAsync(Origin, initial.AccessToken, true);
        Assert.NotEqual(initial.AccessToken, refreshed.AccessToken);
        await Fails("session-expired", () => api.ValidateAsync(Origin, initial.AccessToken, false));
        await api.LogoutAsync(Origin, refreshed.AccessToken);
        await Fails("session-expired", () => api.ValidateAsync(Origin, refreshed.AccessToken, false));
        var revoked = await api.LoginAsync(Origin, "alice", Password);
        host.Accounts.RevokeSessions("alice", "test");
        await Fails("session-expired", () => api.ValidateAsync(Origin, revoked.AccessToken, false));
        var expires = await api.LoginAsync(Origin, "alice", Password);
        host.Clock.Offset = TimeSpan.FromMinutes(31);
        Assert.Null(host.Sessions.Find(expires.AccessToken, host.Devices.Find(tablet.Id)!));
    }

    [Fact]
    public async Task Shared_Tablet_Separates_Employees_And_Tokens_Cannot_Move_Between_Installations()
    {
        await using var host = await Host.StartAsync();
        using var shared = await host.ApproveAsync();
        using var another = await host.ApproveAsync();
        var alice = await shared.Api.LoginAsync(Origin, "alice", Password);
        await Fails("session-expired", () => another.Api.ValidateAsync(Origin, alice.AccessToken, false));
        var beth = await shared.Api.LoginAsync(Origin, "beth", Password);
        Assert.Equal("Beth", beth.DisplayName);
        await shared.Api.LogoutAsync(Origin, alice.AccessToken);
        Assert.Equal("Beth", (await shared.Api.ValidateAsync(Origin, beth.AccessToken, false)).DisplayName);
        await Fails("session-expired", () => shared.Api.ValidateAsync(Origin, alice.AccessToken, false));
        using var signed = shared.SignedGet("/api/auth/me", beth.AccessToken);
        using var replay = shared.SignedGet("/api/auth/me", beth.AccessToken, signed);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.SendAsync(signed)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Http.SendAsync(replay)).StatusCode);
    }

    [Fact]
    public async Task Client_Rejects_Http_And_Reports_API_Connectivity_Failure()
    {
        using var http = new HttpClient(new FailureHandler());
        using var tablet = new Tablet(http);
        await Assert.ThrowsAsync<HttpRequestException>(() => tablet.Api.StatusAsync(Origin));
        await Assert.ThrowsAsync<ArgumentException>(() => tablet.Api.LoginAsync(new Uri("http://localhost"), "alice", Password));
    }

    private static async Task Fails(string code, Func<Task> request) =>
        Assert.Equal(code, (await Assert.ThrowsAsync<MobileApiException>(request)).Code);

    private sealed record ModuleResponse(string[] AuthorizedModules);

    private sealed class FailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Test API is unreachable"));
    }

    private sealed class Tablet : IDisposable, IInstallationKey
    {
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public Tablet(HttpClient http, string application = Protocol.Application) =>
            Api = new WarehouseApiClient(http, this, application);
        public WarehouseApiClient Api { get; }
        public byte[] PublicKey => key.ExportSubjectPublicKeyInfo();
        public string Id => Protocol.InstallationId(PublicKey);
        public byte[] Sign(byte[] data) =>
            key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        public void Dispose() => key.Dispose();

        public HttpRequestMessage SignedGet(string path, string token, HttpRequestMessage? original = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new("Bearer", token);
            string nonce = Guid.NewGuid().ToString("N");
            string time = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            if (original is not null)
            {
                foreach (var name in new[] { Protocol.InstallationHeader, Protocol.NonceHeader, Protocol.TimeHeader, Protocol.SignatureHeader })
                    request.Headers.Add(name, original.Headers.GetValues(name));
                return request;
            }
            request.Headers.Add(Protocol.InstallationHeader, Id);
            request.Headers.Add(Protocol.NonceHeader, nonce);
            request.Headers.Add(Protocol.TimeHeader, time);
            request.Headers.Add(Protocol.SignatureHeader,
                Convert.ToBase64String(Sign(Protocol.SigningInput("GET", path, Id, nonce, time, token))));
            return request;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
    }

    private sealed class Directory : IAuthenticator
    {
        private readonly Dictionary<string, VerifiedIdentity> identities = new();
        private readonly Guid domain = Guid.NewGuid();
        public VerifiedIdentity Identity(string name) => identities.TryGetValue(name, out var identity) ? identity :
            identities[name] = new(new("active-directory", domain.ToString(), Guid.NewGuid().ToString()),
                AuthenticationProviderKind.ActiveDirectory, name, name, DateTimeOffset.UtcNow, "test");

        public AuthenticationResult Authenticate(string loginName, string password) =>
            password != Password ? AuthenticationResult.Failed() :
            loginName == "disabled-in-ad" ? AuthenticationResult.Failed(AuthenticationFailure.Disabled) :
            AuthenticationResult.Succeeded(Identity(loginName), new());
    }

    private sealed class Permissions : IWarehousePermissionSource
    {
        public string[] Modules { get; set; } = [WarehouseModules.StockCounts];
        public Task<IReadOnlyList<string>> ResolveAsync(WarehouseIdentity employee, CancellationToken _) =>
            Task.FromResult<IReadOnlyList<string>>(employee.AccountId == "no-warehouse" ? [] : Modules);
        public Task<bool> IsReadyAsync(CancellationToken _) => Task.FromResult(true);
    }

    private sealed class Host(WebApplication app, SqliteConnection anchor) : IAsyncDisposable
    {
        public HttpClient Http { get; } = app.GetTestClient();
        public IAccountStore Accounts => app.Services.GetRequiredService<IAccountStore>();
        public DeviceStore Devices => app.Services.GetRequiredService<DeviceStore>();
        public Clock Clock => (Clock)app.Services.GetRequiredService<TimeProvider>();
        public MobileSessions Sessions => app.Services.GetRequiredService<MobileSessions>();
        public Permissions Permissions => (Permissions)app.Services.GetRequiredService<IWarehousePermissionSource>();

        public static async Task<Host> StartAsync(bool allowAdministratorAsEmployee = false,
            string? application = null, string? applicationModule = null, string? additionalAdministratorApplication = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration["Devices:AdministratorAccountIds:0"] = "admin";
            if (allowAdministratorAsEmployee)
                builder.Configuration["Security:Warehouse:AdditionalAdministratorAccountIds:0"] = "admin";
            if (additionalAdministratorApplication is not null)
                builder.Configuration[$"Security:Applications:{additionalAdministratorApplication}:AdditionalAdministratorAccountIds:0"] = "admin";
            string connection = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            SqliteConnection anchor = new(connection);
            anchor.Open();
            builder.Services.AddCommonAccountStore(_ => new SqliteConnection(connection), "ffp-mobile");
            var directory = new Directory();
            builder.Services.AddSingleton<IAuthenticator>(directory);
            var clock = new Clock();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddSingleton(new DeviceStore(connection, clock));
            builder.Services.AddSingleton<EmployeeAuthentication>();
            builder.Services.AddSingleton<InstallationProof>();
            builder.Services.AddSingleton<OpaqueAccountSessions<DeviceSession>>();
            builder.Services.AddSingleton<MobileSessions>();
            builder.Services.AddSingleton<IWarehousePermissionSource, Permissions>();
            if (application is not null && applicationModule is not null)
                builder.Services.AddKeyedSingleton<IWarehousePermissionSource>(application, new Permissions { Modules = [applicationModule] });
            var app = builder.Build();
            var accounts = app.Services.GetRequiredService<IAccountStore>();
            foreach (var (id, display, population, status) in new[]
            {
                ("alice", "Alice", AccountPopulation.Staff, SecurityAccountStatus.Active),
                ("beth", "Beth", AccountPopulation.Staff, SecurityAccountStatus.Active),
                ("no-warehouse", "No warehouse", AccountPopulation.Staff, SecurityAccountStatus.Active),
                ("disabled-in-common", "Disabled", AccountPopulation.Staff, SecurityAccountStatus.Disabled),
                ("admin", "Device Administrator", AccountPopulation.Administrator, SecurityAccountStatus.Active)
            })
                accounts.Create(new(id, id, display, AuthenticationProviderKind.ActiveDirectory, population, status,
                    directory.Identity(id).Key), "test");
            app.Services.GetRequiredService<DeviceStore>().Initialize();
            app.MapWarehouseEndpoints();
            await app.StartAsync();
            var host = new Host(app, anchor);
            host.Http.BaseAddress = Origin;
            return host;
        }

        public async Task<Tablet> ApproveAsync(string application = Protocol.Application)
        {
            var tablet = new Tablet(Http, application);
            await tablet.Api.RegisterAsync(Origin, "Test tablet");
            Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(tablet.Id, "Approved"));
            return tablet;
        }

        public async Task<HttpStatusCode> StatusAsync(string id, string status, string actor = "admin")
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/devices/{id}/status/{status}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{actor}:{Password}")));
            using var response = await Http.SendAsync(request);
            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await app.DisposeAsync();
            await anchor.DisposeAsync();
        }
    }
}

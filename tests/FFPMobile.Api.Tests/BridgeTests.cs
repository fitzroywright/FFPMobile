using System.Net;
using System.Net.Http.Json;
using FFPMobile.FfpManagerBridge;
using Xunit;

namespace FFPMobile.Tests;

public sealed class BridgeTests
{
    private static readonly WarehouseIdentity Employee = new("common-id", Guid.NewGuid(), Guid.NewGuid());
    private static readonly Guid XafUser = Guid.NewGuid();

    [Fact]
    public async Task Bridge_Sends_Verified_Identity_And_Only_Recognizes_Known_Modules()
    {
        using var transport = new Evaluator(XafUser, ["unrecognized", WarehouseModules.GoodsReceival, WarehouseModules.GoodsReceival]);
        using var http = new HttpClient(transport);
        var bridge = Create(http, XafUser);
        Assert.Equal([WarehouseModules.GoodsReceival], await bridge.ResolveAsync(Employee, default));
        Assert.Empty(await bridge.ResolveAsync(Employee with { SubjectId = Guid.NewGuid() }, default));
        Assert.Empty(await bridge.ResolveAsync(Employee with { AccountId = "missing" }, default));
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Bridge_Rejects_A_Decision_For_A_Different_Xaf_User()
    {
        using var http = new HttpClient(new Evaluator(Guid.NewGuid(), [WarehouseModules.DeliveryOrders]));
        Assert.Empty(await Create(http, XafUser).ResolveAsync(Employee, default));
    }

    [Fact]
    public async Task Field_Officers_Bridge_Only_Accepts_Field_Officers_Modules()
    {
        using var http = new HttpClient(new Evaluator(XafUser,
            [WarehouseModules.StockCounts, "purchase-orders", "dispatch-verification"], "field-officers"));
        var bridge = new FfpManagerPermissionSource(http, new Uri("https://xaf.example/"),
            new Dictionary<string, FfpManagerBinding> { [Employee.AccountId] = new(Employee.DirectoryId, Employee.SubjectId, XafUser) },
            _ => Task.FromResult("test-service-token"), "field-officers", OtherMobileModules.FieldOfficers);
        Assert.Equal(["purchase-orders"], await bridge.ResolveAsync(Employee, default));
    }

    [Fact]
    public async Task Unconfigured_Bridge_Denies_Access_And_Readiness()
    {
        using var transport = new Evaluator(XafUser, [WarehouseModules.StockCounts]);
        using var http = new HttpClient(transport);
        var bridge = new FfpManagerPermissionSource(http, null, new Dictionary<string, FfpManagerBinding>(),
            _ => throw new InvalidOperationException());
        Assert.Empty(await bridge.ResolveAsync(Employee, default));
        Assert.False(await bridge.IsReadyAsync(default));
        Assert.Equal(0, transport.Calls);
    }

    private static FfpManagerPermissionSource Create(HttpClient http, Guid user) =>
        new(http, new Uri("https://xaf.example/"),
            new Dictionary<string, FfpManagerBinding> { [Employee.AccountId] = new(Employee.DirectoryId, Employee.SubjectId, user) },
            _ => Task.FromResult("test-service-token"));

    private sealed class Evaluator(Guid returnedUser, string[] modules, string resource = "warehouse") : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal($"https://xaf.example/v1/{resource}/permissions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("test-service-token", request.Headers.Authorization?.Parameter);
            var input = await request.Content!.ReadFromJsonAsync<DecisionRequest>(cancellationToken);
            Assert.Equal(new DecisionRequest(XafUser, Employee.DirectoryId, Employee.SubjectId), input);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new EffectiveWarehousePermissions(returnedUser, modules)) };
        }
    }

    private sealed record DecisionRequest(Guid UserId, Guid DirectoryId, Guid SubjectId);
}

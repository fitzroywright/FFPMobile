using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FFPMobile.FfpManagerBridge;

public sealed record WarehouseIdentity(string AccountId, Guid DirectoryId, Guid SubjectId);
public sealed record EffectiveWarehousePermissions(Guid UserId, string[]? Modules);
public sealed record FfpManagerBinding(Guid DirectoryId, Guid SubjectId, Guid UserId);

public static class WarehouseModules
{
    public const string StockCounts = "stock-counts";
    public const string InventoryReports = "inventory-reports";
    public const string DeliveryOrders = "delivery-orders";
    public const string ReturnToStock = "return-to-stock";
    public const string GoodsReceival = "goods-receival";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
        new[] { StockCounts, InventoryReports, DeliveryOrders, ReturnToStock, GoodsReceival });
}

public static class OtherMobileModules
{
    public static IReadOnlyList<string> FieldOfficers { get; } =
        Array.AsReadOnly(new[] { "purchase-orders", "payment-requests" });

    public static IReadOnlyList<string> SecurityPost { get; } =
        Array.AsReadOnly(new[] { "dispatch-verification", "vehicle-movements", "visitor-log" });
}

public interface IWarehousePermissionSource
{
    Task<IReadOnlyList<string>> ResolveAsync(WarehouseIdentity identity, CancellationToken cancellationToken);
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

// Only the ERP-side evaluator interprets XAF rules. This client accepts a decision for
// the explicitly bound XAF user and exposes stable capability IDs to mobile callers.
public sealed class FfpManagerPermissionSource(
    HttpClient client, Uri? origin, IReadOnlyDictionary<string, FfpManagerBinding> bindings,
    Func<CancellationToken, Task<string>> getServiceToken, string resource = "warehouse",
    IReadOnlyList<string>? supportedModules = null) : IWarehousePermissionSource
{
    public async Task<IReadOnlyList<string>> ResolveAsync(WarehouseIdentity identity, CancellationToken cancellationToken)
    {
        if (origin is null || !bindings.TryGetValue(identity.AccountId, out var binding) ||
            binding.DirectoryId != identity.DirectoryId || binding.SubjectId != identity.SubjectId || binding.UserId == Guid.Empty)
            return [];

        var userId = binding.UserId;
        using var request = await CreateRequestAsync(HttpMethod.Post, $"/v1/{resource}/permissions", cancellationToken);
        request.Content = JsonContent.Create(new { userId, identity.DirectoryId, identity.SubjectId });
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
            return [];
        response.EnsureSuccessStatusCode();
        var decision = await response.Content.ReadFromJsonAsync<EffectiveWarehousePermissions>(cancellationToken);
        if (decision?.UserId != userId || decision.Modules is null)
            return [];
        return (supportedModules ?? WarehouseModules.All)
            .Where(module => decision.Modules.Contains(module, StringComparer.Ordinal)).ToArray();
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (origin is null || bindings.Count == 0)
            return false;
        using var request = await CreateRequestAsync(HttpMethod.Get, $"/v1/{resource}/ready", cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        string token = await getServiceToken(cancellationToken);
        return new HttpRequestMessage(method, new Uri(origin!, path))
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };
    }
}

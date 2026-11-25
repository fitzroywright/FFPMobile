namespace FFPMobile.Shared.WarehouseProtocol;

// Offline operations will be scoped to a verified employee, application and installation.
public sealed record LocalDataScope(string InstallationId, string Application, string AccountId);
public sealed record OfflineGrant(LocalDataScope Scope, DateTimeOffset ExpiresAtUtc, byte[] SignedPayload);
public sealed record QueuedOperation(Guid IdempotencyKey, string Operation, byte[] Payload);

public interface IEncryptedLocalStore
{
    Task<byte[]?> ReadAsync(LocalDataScope scope, string key, CancellationToken cancellationToken);
    Task WriteAsync(LocalDataScope scope, string key, byte[] content, CancellationToken cancellationToken);
    Task ClearAsync(LocalDataScope scope, CancellationToken cancellationToken);
}

public interface IReferenceDataCache
{
    Task<byte[]?> ReadAsync(LocalDataScope scope, string dataset, CancellationToken cancellationToken);
}

public interface IOfflineAuthorization
{
    Task<bool> RevalidateAsync(OfflineGrant grant, CancellationToken cancellationToken);
}

public interface IDurableSynchronizationQueue
{
    Task EnqueueAsync(LocalDataScope scope, QueuedOperation operation, CancellationToken cancellationToken);
    Task<QueuedOperation?> PeekAsync(LocalDataScope scope, CancellationToken cancellationToken);
    Task CompleteAsync(LocalDataScope scope, Guid idempotencyKey, CancellationToken cancellationToken);
}

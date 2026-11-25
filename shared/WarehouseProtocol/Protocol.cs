using System.Security.Cryptography;
using System.Text;

namespace FFPMobile.Shared.WarehouseProtocol;

public static class Protocol
{
    public const string Application = "warehouse-operations";
    public const string InstallationHeader = "X-FFP-Installation";
    public const string NonceHeader = "X-FFP-Nonce";
    public const string TimeHeader = "X-FFP-Time";
    public const string SignatureHeader = "X-FFP-Signature";

    public static string InstallationId(byte[] publicKey) => Convert.ToHexString(SHA256.HashData(publicKey));

    public static byte[] SigningInput(string method, string path, string deviceId, string nonce, string time, string token) =>
        Encoding.UTF8.GetBytes(string.Join('\n', "ffpmobile-device-v1", method, path, deviceId, nonce, time,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))));
}

public sealed record RegisterDevice(string Name, string Application, string PublicKey);
public sealed record DeviceDetails(string Id, string Name, string Application, DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? LastSuccessfulConnectionUtc, DeviceStatus Status);
public enum DeviceStatus { PendingApproval, Approved, Suspended, Revoked }
public sealed record SessionLogin(string UserName, string Password, string Application);
public sealed record EmployeeSession(string AccessToken, string DisplayName, DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> AuthorizedModules);
public sealed record ApiError(string Code);

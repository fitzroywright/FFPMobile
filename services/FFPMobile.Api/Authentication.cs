using System.Globalization;
using System.Security.Cryptography;
using Common.Security.Abstractions;
using Common.Security.Models;
using Common.Security.Services;
using FFPMobile.FfpManagerBridge;
using FFPMobile.Shared.WarehouseProtocol;

namespace FFPMobile.Api;

public sealed record BoundEmployee(SecurityAccount Account, WarehouseIdentity Identity);
public sealed record DeviceSession(WarehouseIdentity Employee, string DeviceId, long DeviceVersion, DateTimeOffset Deadline);

public sealed class EmployeeAuthentication(IAuthenticator authenticator, IAccountStore accounts)
{
    public BoundEmployee? Authenticate(string? username, string? password, AccountPopulation population,
        IReadOnlySet<string>? additionalStaffAccounts = null)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 256 ||
            string.IsNullOrWhiteSpace(password) || password.Length > 1024)
            return null;

        // The provider binds to FFTPJ.LOCAL. Require the organizational sAMAccountName.
        string login = username.Trim();
        if (login.Contains('@') || login.Contains('\\') || login.Any(char.IsControl)) return null;
        var result = authenticator.Authenticate(login, password);
        if (!result.IsAuthenticated || result.Identity is not
            { ProviderKind: AuthenticationProviderKind.ActiveDirectory, Key.ProviderId: "active-directory" } identity ||
            !Guid.TryParse(identity.Key.ScopeId, out var directoryId) || !Guid.TryParse(identity.Key.SubjectId, out var subjectId))
            return null;
        var account = Resolve(identity, population);
        if (account is null && population == AccountPopulation.Staff && additionalStaffAccounts is { Count: > 0 })
        {
            var administrator = Resolve(identity, AccountPopulation.Administrator);
            if (administrator is not null && additionalStaffAccounts.Contains(administrator.Id))
                account = administrator;
        }
        return account is { Status: SecurityAccountStatus.Active }
            ? new(account, new(account.Id, directoryId, subjectId)) : null;
    }

    private SecurityAccount? Resolve(VerifiedIdentity identity, AccountPopulation population)
    {
        try
        {
            var account = accounts.Resolve(identity, population, false).Account;
            return account.Population == population ? account : null;
        }
        catch (InvalidOperationException) { return null; }
        catch (KeyNotFoundException) { return null; }
    }
}

public sealed class InstallationProof(DeviceStore devices, TimeProvider clock)
{
    public RegisteredDevice? Authenticate(HttpRequest request, string token = "")
    {
        string id = request.Headers[Protocol.InstallationHeader].ToString();
        if (!IsDeviceId(id)) return null;
        var device = devices.Find(id);
        return device is not null && Verify(request, id, device.PublicKey, token) ? device : null;
    }

    public bool Verify(HttpRequest request, string id, byte[] key, string token)
    {
        if (request.Headers[Protocol.InstallationHeader] != id || !IsDeviceId(id)) return false;
        string nonce = request.Headers[Protocol.NonceHeader].ToString();
        string time = request.Headers[Protocol.TimeHeader].ToString();
        string signature = request.Headers[Protocol.SignatureHeader].ToString();
        long now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (!Guid.TryParseExact(nonce, "N", out _) || nonce != nonce.ToLowerInvariant() ||
            !long.TryParse(time, NumberStyles.None, CultureInfo.InvariantCulture, out long at) ||
            at < now - 90 || at > now + 90 || signature.Length > 128)
            return false;
        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(key, out int read);
            if (read != key.Length || verifier.KeySize != 256) return false;
            byte[] input = Protocol.SigningInput(request.Method, request.Path.Value!, id, nonce, time, token);
            return verifier.VerifyData(input, Convert.FromBase64String(signature), HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence) && devices.ConsumeNonce(id, nonce, at + 90);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    public static bool IsDeviceId(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}

public sealed class MobileSessions(OpaqueAccountSessions<DeviceSession> sessions, TimeProvider clock)
{
    private readonly object rotation = new();
    private readonly Dictionary<string, OpaqueAccountSession<DeviceSession>> byDevice = new(StringComparer.Ordinal);

    public OpaqueAccountSession<DeviceSession> Create(BoundEmployee employee, RegisteredDevice device)
    {
        lock (rotation)
        {
            foreach (var expired in byDevice.Where(pair => pair.Value.ExpiresAt <= clock.GetUtcNow()).ToArray())
                byDevice.Remove(expired.Key);
            var current = sessions.Create(employee.Account.Id, employee.Account.SessionVersion,
                new(employee.Identity, device.Details.Id, device.Version, clock.GetUtcNow().AddHours(8)), TimeSpan.FromMinutes(30));
            if (byDevice.TryGetValue(device.Details.Id, out var old)) sessions.Remove(old.Token);
            byDevice[device.Details.Id] = current;
            return current;
        }
    }

    public OpaqueAccountSession<DeviceSession>? Find(string token, RegisteredDevice device)
    {
        var session = sessions.Find(token);
        return session is not null && session.Identity.DeviceId == device.Details.Id &&
            session.Identity.DeviceVersion == device.Version ? session : null;
    }

    public OpaqueAccountSession<DeviceSession>? Refresh(string token, RegisteredDevice device)
    {
        lock (rotation)
        {
            var existing = Find(token, device);
            if (existing is null) return null;
            var remaining = existing.Identity.Deadline - clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero) return null;
            var updated = sessions.Create(existing.AccountId, existing.SessionVersion, existing.Identity,
                remaining < TimeSpan.FromMinutes(30) ? remaining : TimeSpan.FromMinutes(30));
            sessions.Remove(token);
            byDevice[device.Details.Id] = updated;
            return updated;
        }
    }

    public void Remove(string token, RegisteredDevice device)
    {
        lock (rotation)
            if (byDevice.TryGetValue(device.Details.Id, out var current) && current.Token == token)
            {
                sessions.Remove(token);
                byDevice.Remove(device.Details.Id);
            }
    }
}

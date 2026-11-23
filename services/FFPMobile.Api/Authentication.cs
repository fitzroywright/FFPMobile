namespace FFPMobile.Api;

public sealed record LoginRequest(string UserName, string Password, string Application);
public sealed record LoginResponse(string AccessToken, string DisplayName, DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> AuthorizedModules);

/// <summary>
/// Must evaluate effective FFP Manager XAF permissions for the server-bound identity.
/// An absent adapter cannot grant access.
/// </summary>
public interface IMobilePermissionResolver
{
    IReadOnlyList<string> GetAuthorizedModules(string accountId, string application);
}
public sealed class DenyAllMobilePermissionResolver : IMobilePermissionResolver
{
    public IReadOnlyList<string> GetAuthorizedModules(string accountId, string application)
        => [];
}

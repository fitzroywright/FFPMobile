namespace FFPMobile.Shared.Authentication;

public sealed record LoginRequest(string UserName, string Password, string Application);
public sealed record LoginResponse(string AccessToken, string DisplayName, DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> AuthorizedModules);
public static class MobileApplications
{
    public const string FieldOfficers = "field-officers";
    public const string Warehouse = "warehouse-operations";
    public const string Security = "security-post";
}

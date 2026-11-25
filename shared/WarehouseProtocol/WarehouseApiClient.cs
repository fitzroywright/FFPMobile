using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FFPMobile.Shared.WarehouseProtocol;

public interface IInstallationKey
{
    byte[] PublicKey { get; }
    byte[] Sign(byte[] data);
}

public sealed class MobileApiException(string code, HttpStatusCode status) : Exception(code)
{
    public string Code { get; } = code;
    public bool EndsSession => (int)status is 401 or 403;
}

public sealed class WarehouseApiClient(HttpClient client, IInstallationKey key)
{
    public Task<DeviceDetails> RegisterAsync(Uri origin, string name) =>
        SendAsync<DeviceDetails>(origin, HttpMethod.Post, "/api/devices/register",
            body: new RegisterDevice(name, Protocol.Application, Convert.ToBase64String(key.PublicKey)));

    public Task<DeviceDetails> StatusAsync(Uri origin) =>
        SendAsync<DeviceDetails>(origin, HttpMethod.Get, "/api/devices/status");

    public Task<EmployeeSession> LoginAsync(Uri origin, string username, string password) =>
        SendAsync<EmployeeSession>(origin, HttpMethod.Post, "/api/auth/login",
            body: new SessionLogin(username, password, Protocol.Application));

    public Task<EmployeeSession> ValidateAsync(Uri origin, string token, bool refresh) =>
        SendAsync<EmployeeSession>(origin, refresh ? HttpMethod.Post : HttpMethod.Get,
            refresh ? "/api/auth/refresh" : "/api/auth/me", token);

    public Task LogoutAsync(Uri origin, string token) =>
        SendAsync<object>(origin, HttpMethod.Post, "/api/auth/logout", token);

    private async Task<T> SendAsync<T>(Uri origin, HttpMethod method, string path, string token = "", object? body = null)
    {
        if (!IsOrigin(origin)) throw new ArgumentException("A private HTTPS API origin is required.", nameof(origin));
        using var request = new HttpRequestMessage(method, new Uri(origin, path));
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token.Length != 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        byte[] publicKey = key.PublicKey;
        string id = Protocol.InstallationId(publicKey);
        string nonce = Guid.NewGuid().ToString("N");
        string time = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        byte[] signature = key.Sign(Protocol.SigningInput(method.Method, path, id, nonce, time, token));
        request.Headers.Add(Protocol.InstallationHeader, id);
        request.Headers.Add(Protocol.NonceHeader, nonce);
        request.Headers.Add(Protocol.TimeHeader, time);
        request.Headers.Add(Protocol.SignatureHeader, Convert.ToBase64String(signature));
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
                error = await response.Content.ReadFromJsonAsync<ApiError>();
            throw new MobileApiException(error?.Code ?? "request-failed", response.StatusCode);
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new HttpRequestException("Empty API response.");
    }

    public static bool IsOrigin(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps &&
        uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}

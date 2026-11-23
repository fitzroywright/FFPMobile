using System.Net;
using System.Net.Http.Json;

namespace FFPMobile.Shared.Authentication;

/// <summary>
/// Calls the private service that runs Common.Security. No authentication provider or
/// privileged credential is embedded in the Android application.
/// </summary>
public sealed class MobileLoginClient
{
    private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<LoginResponse> LoginAsync(string baseUrl, LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? root) ||
            root.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(root.UserInfo))
            throw new InvalidOperationException("Configure a valid HTTPS Mobile API URL in Device Settings.");

        using var response = await httpClient.PostAsJsonAsync(
            new Uri(root, "api/auth/login"), request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Sign in was denied. Check your credentials or contact your administrator.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Authentication service is unavailable.", null, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication service returned an invalid response.");
    }
}

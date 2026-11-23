using FFPMobile.Shared.Authentication;
using FFPMobile.Shared.Profiles;

namespace FFPMobile.Shared.Ui;

public sealed class LoginPage : ContentPage
{
    private readonly MobileLoginClient loginClient = new();

    public LoginPage(AppProfile profile, string applicationId)
    {
        Title = "FFP Mobile";
        BackgroundColor = AppColors.Background;

        var username = new Entry { Placeholder = "User Name", Keyboard = Keyboard.Text,
            AutomationId = "UserName" };
        var password = new Entry { Placeholder = "Password", IsPassword = true,
            AutomationId = "Password" };
        var message = new Label { Text = "Use your authorized FFP Manager account.",
            FontSize = 13, TextColor = AppColors.Muted };
        var signIn = new Button
        {
            Text = "Sign In",
            BackgroundColor = AppColors.Blue,
            TextColor = Colors.White,
            CornerRadius = 10,
            HeightRequest = 54,
            AutomationId = "SignIn"
        };
        var settings = new Button { Text = "Device Settings", BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Navy };
        settings.Clicked += async (_, _) =>
        {
            var configured = Preferences.Default.Get("FFPMobile.ApiBaseUrl", "");
            string? endpoint = await DisplayPromptAsync("Mobile API", "Enter the private HTTPS API URL",
                initialValue: configured, keyboard: Keyboard.Url);
            if (endpoint is null) return;
            if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var parsed) ||
                parsed.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsed.UserInfo))
            {
                await DisplayAlertAsync("Invalid URL", "An HTTPS URL is required.", "OK");
                return;
            }
            Preferences.Default.Set("FFPMobile.ApiBaseUrl", parsed.AbsoluteUri);
            message.Text = "Server configured. You can sign in when connected.";
        };
        signIn.Clicked += async (_, _) =>
        {
            var baseUrl = Preferences.Default.Get("FFPMobile.ApiBaseUrl", "");
            signIn.IsEnabled = false;
            try
            {
                var result = await loginClient.LoginAsync(baseUrl,
                    new LoginRequest(username.Text?.Trim() ?? "", password.Text ?? "", applicationId));
                password.Text = string.Empty;
                if (result.AuthorizedModules.Count == 0)
                {
                    message.Text = "Identity verified, but no FFP Manager permissions are available for this application.";
                    return;
                }
                await Navigation.PushAsync(new DashboardPage(profile, result.AuthorizedModules));
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or
                HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                message.Text = exception.Message;
            }
            finally
            {
                password.Text = string.Empty;
                signIn.IsEnabled = true;
            }
        };
        var card = new VerticalStackLayout
        {
            Spacing = 18, Padding = new Thickness(34), BackgroundColor = Colors.White,
            MaximumWidthRequest = 520, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "FFP", FontSize = 44, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Blue, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Title, FontSize = 31, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Text, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Subtitle, FontSize = 21, TextColor = AppColors.Muted,
                    HorizontalTextAlignment = TextAlignment.Center },
                username, password, signIn, message, settings,
                new Label { Text = "Online authentication uses Common.Security on FFP Mobile API.\n" +
                    "Offline unlock will be enabled after device enrollment and authorization controls are implemented.",
                    FontSize = 12, TextColor = AppColors.Muted }
            }
        };
        Content = new ScrollView { Content = new Grid
        {
            Padding = new Thickness(20,36),
            Children = { card }
        }};
    }
}

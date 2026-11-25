using System.Text.Json;
using FFPMobile.Shared.Profiles;
using FFPMobile.Shared.Ui;
using FFPMobile.Shared.WarehouseProtocol;

namespace FFPMobile.App;

internal sealed class WarehousePage : ContentPage
{
    private const string OriginSetting = "FFPMobile.Warehouse.ApiOrigin";
    private const string SessionKey = "FFPMobile.Warehouse.Session.v1";
    private static readonly string[] ModuleIds =
        ["stock-counts", "inventory-reports", "delivery-orders", "return-to-stock", "goods-receival"];
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(12) };
    private readonly WarehouseApiClient api = new(Http, new InstallationKey());
    private readonly Entry username = new() { Placeholder = "User Name", AutomationId = "UserName" };
    private readonly Entry password = new() { Placeholder = "Password", IsPassword = true, AutomationId = "Password" };
    private readonly Button signIn = new() { Text = "Sign In", AutomationId = "SignIn", BackgroundColor = AppColors.Blue,
        TextColor = Colors.White, CornerRadius = 10, HeightRequest = 54, IsEnabled = false };
    private readonly Button settings = new() { Text = "Device Settings", BackgroundColor = Colors.Transparent, TextColor = AppColors.Navy };
    private readonly Button forget = new() { Text = "Sign Out", IsVisible = false, BackgroundColor = Colors.Transparent, TextColor = AppColors.Navy };
    private readonly ActivityIndicator progress = new() { Color = AppColors.Blue, IsVisible = false };
    private readonly Label message = new() { Text = "Use your organizational account.", TextColor = AppColors.Muted };
    private readonly Label network = new() { TextColor = AppColors.Muted };
    private readonly Label device = new() { TextColor = AppColors.Muted };
    private readonly View login;
    private readonly AppProfile profile;
    private readonly IDispatcherTimer timer;
    private EmployeeSession? session;
    private bool busy;
    private bool active;
    private bool started;
    private static readonly Uri ProductionOrigin = new("https://mobile.ffpja.org/");
#if DEBUG
    private static Uri Origin => new(Preferences.Default.Get(OriginSetting, ProductionOrigin.AbsoluteUri));
#else
    private static Uri Origin => ProductionOrigin;
#endif

    public WarehousePage(AppProfile profile)
    {
        this.profile = profile;
        Title = profile.Subtitle;
        BackgroundColor = AppColors.Background;
        var reveal = new Button { Text = "Show Password", BackgroundColor = Colors.Transparent, TextColor = AppColors.Navy };
        reveal.Clicked += (_, _) =>
        {
            password.IsPassword = !password.IsPassword;
            reveal.Text = password.IsPassword ? "Show Password" : "Hide Password";
        };
        signIn.Clicked += async (_, _) => await RunAsync(async () =>
        {
            string secret = password.Text ?? "";
            password.Text = "";
            password.IsPassword = true;
            reveal.Text = "Show Password";
            if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrEmpty(secret))
            {
                message.Text = "Enter your user name and password.";
                return;
            }
            // Switching users discards all locally held information from the previous session.
            await EndSessionAsync(false);
            var result = await api.LoginAsync(Origin, username.Text.Trim(), secret);
            await SaveAsync(result);
        });
        password.Completed += (_, _) => { if (signIn.IsEnabled) signIn.SendClicked(); };
        settings.Clicked += async (_, _) => await ConfigureDeviceAsync();
        forget.Clicked += async (_, _) => await SignOutAsync();

        var card = new VerticalStackLayout
        {
            Spacing = 14, Padding = new Thickness(34), BackgroundColor = Colors.White,
            MaximumWidthRequest = 520, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "FFP", FontSize = 44, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Blue, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Title, FontSize = 31, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Text, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Subtitle, FontSize = 21, TextColor = AppColors.Muted,
                    HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Description, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center },
                username, password, reveal, signIn, progress, message, network, device, settings, forget
            }
        };
        login = new ScrollView { Content = new Grid { Padding = new Thickness(20, 36), Children = { card } } };
        Content = login;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(20);
        timer.Tick += async (_, _) => await CheckAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (started) return;
        started = true;
        await ResumeAsync();
    }

    public void Pause()
    {
        active = false;
        timer.Stop();
        Connectivity.Current.ConnectivityChanged -= ConnectivityChanged;
        password.Text = "";
        Content = login;
    }

    public async Task ResumeAsync()
    {
        if (active) return;
        active = true;
        Connectivity.Current.ConnectivityChanged += ConnectivityChanged;
        timer.Start();
        await CheckAsync();
    }

    private void ConnectivityChanged(object? sender, ConnectivityChangedEventArgs args) =>
        Dispatcher.Dispatch(async () =>
        {
            if (!active) return;
            if (args.NetworkAccess != NetworkAccess.Internet)
            {
                Content = login;
                network.Text = "Mobile API unavailable. Check your Wi-Fi or VPN connection.";
            }
            await CheckAsync();
        });

    private Task CheckAsync() => RunAsync(async () =>
    {
        var stored = await SecureStorage.Default.GetAsync(SessionKey);
        forget.IsVisible = stored is not null;
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            throw new HttpRequestException();
        if (stored is not null)
        {
            var saved = JsonSerializer.Deserialize<SavedSession>(stored);
            if (saved is not null && saved.Origin == Origin.AbsoluteUri && saved.ExpiresAt > DateTimeOffset.UtcNow)
            {
                await SaveAsync(await api.ValidateAsync(Origin, saved.Token, saved.ExpiresAt - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(5)));
                return;
            }
            await EndSessionAsync(true);
            message.Text = "Your session has expired. Sign in again.";
        }
        await UpdateDeviceAsync();
    }, showProgress: false);

    private async Task UpdateDeviceAsync()
    {
        try
        {
            var status = await api.StatusAsync(Origin);
            SetDeviceStatus(status);
        }
        catch (MobileApiException ex) when (ex.Code == "installation-proof-required")
        {
            device.Text = "Device registration required. Open Device Settings.";
            deviceApproved = false;
        }
    }

    private bool deviceApproved;

    private void SetDeviceStatus(DeviceDetails status)
    {
        deviceApproved = status.Status == DeviceStatus.Approved;
        device.Text = status.Status switch
        {
            DeviceStatus.PendingApproval => "Device pending approval.",
            DeviceStatus.Approved => "Device approved.",
            DeviceStatus.Suspended => "Device suspended.",
            DeviceStatus.Revoked => "Device revoked.",
            _ => "Device not approved."
        };
        if (!deviceApproved)
            device.Text += $"\nInstallation ID {status.Id}";
    }

    private Task ConfigureDeviceAsync() => RunAsync(async () =>
    {
#if DEBUG
        string? entered = await DisplayPromptAsync("Mobile API", "Enter the private HTTPS API origin",
            initialValue: Origin.AbsoluteUri, keyboard: Keyboard.Url);
        if (entered is null) return;
        if (!Uri.TryCreate(entered.Trim(), UriKind.Absolute, out var origin) || !WarehouseApiClient.IsOrigin(origin))
        {
            message.Text = "Enter an HTTPS origin without a path.";
            return;
        }
        if (origin != Origin)
        {
            await EndSessionAsync(true);
            Preferences.Default.Set(OriginSetting, origin.AbsoluteUri);
            deviceApproved = false;
        }
#endif
        string? name = await DisplayPromptAsync("Device Registration", "Enter the tablet name from your administrator",
            initialValue: DeviceInfo.Current.Name, maxLength: 80);
        if (!string.IsNullOrWhiteSpace(name))
            SetDeviceStatus(await api.RegisterAsync(Origin, name.Trim()));
    });

    private async Task SaveAsync(EmployeeSession current)
    {
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(
            new SavedSession(Origin.AbsoluteUri, current.AccessToken, current.ExpiresAtUtc)));
        username.Text = "";
        var previous = session;
        session = current;
        forget.IsVisible = true;
        if (!active || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
        if (Content != login && previous is not null &&
            previous.AuthorizedModules.SequenceEqual(current.AuthorizedModules) && previous.DisplayName == current.DisplayName)
            return;

        var dashboard = new VerticalStackLayout
        {
            Padding = new Thickness(22), Spacing = 14,
            Children =
            {
                new Label { Text = profile.Title, FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = AppColors.Navy },
                new Label { Text = profile.Subtitle, FontSize = 21, TextColor = AppColors.Text },
                new Label { Text = current.DisplayName, TextColor = AppColors.Text }
            }
        };
        foreach (var (module, index) in profile.Modules.Select((module, index) => (module, index)))
            if (current.AuthorizedModules.Contains(ModuleIds[index], StringComparer.Ordinal))
                dashboard.Children.Add(new Label { Text = module.Title, BackgroundColor = Colors.White,
                    TextColor = AppColors.Navy, Padding = new Thickness(18), MinimumHeightRequest = 64,
                    VerticalTextAlignment = TextAlignment.Center });
        var logout = new Button { Text = "Sign Out", BackgroundColor = AppColors.Blue, TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Start };
        logout.Clicked += async (_, _) => await SignOutAsync();
        dashboard.Children.Add(logout);
        Content = new ScrollView { Content = dashboard };
    }

    private Task SignOutAsync() => RunAsync(async () =>
    {
        await EndSessionAsync(true);
        message.Text = "Signed out.";
        await UpdateDeviceAsync();
    });

    private async Task EndSessionAsync(bool clearUsername)
    {
        string? token = session?.AccessToken;
        if (token is null && await SecureStorage.Default.GetAsync(SessionKey) is { } stored &&
            JsonSerializer.Deserialize<SavedSession>(stored) is { } saved && saved.Origin == Origin.AbsoluteUri)
            token = saved.Token;
        session = null;
        SecureStorage.Default.Remove(SessionKey);
        forget.IsVisible = false;
        Content = login;
        if (clearUsername) username.Text = "";
        if (token is null) return;
        try { await api.LogoutAsync(Origin, token); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or MobileApiException) { }
    }

    private async Task RunAsync(Func<Task> action, bool showProgress = true)
    {
        if (busy || !active) return;
        busy = true;
        signIn.IsEnabled = settings.IsEnabled = false;
        progress.IsRunning = progress.IsVisible = showProgress;
        try
        {
            await action();
            network.Text = "Mobile API connected.";
        }
        catch (MobileApiException ex)
        {
            network.Text = ex.Code == "service-unavailable" ? "Mobile service unavailable." : "Mobile API connected.";
            Content = login;
            if (ex.EndsSession && (session is not null || !showProgress))
                await EndSessionAsync(true);
            message.Text = ex.Code switch
            {
                "employee-not-authorized" => "Check your credentials or contact your administrator.",
                "warehouse-access-denied" => "Your account has no Warehouse access.",
                "device-pending" => "This tablet is pending approval.",
                "device-suspended" => "This tablet is suspended.",
                "device-revoked" => "This tablet registration has been revoked.",
                "installation-proof-required" => "Check the tablet’s time and registration in Device Settings.",
                "session-expired" => "Your session has expired. Sign in again.",
                _ => "The Mobile API could not complete the request."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            network.Text = "Mobile API unavailable. Check your Wi-Fi or VPN connection.";
            Content = login;
        }
        catch (Exception ex) when (ex is JsonException or Java.Security.GeneralSecurityException or System.Security.Cryptography.CryptographicException)
        {
            SecureStorage.Default.Remove(SessionKey);
            session = null;
            Content = login;
            message.Text = "Could not verify the session or tablet identity.";
        }
        finally
        {
            progress.IsRunning = progress.IsVisible = false;
            settings.IsEnabled = true;
            signIn.IsEnabled = deviceApproved && Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            busy = false;
        }
    }

    private sealed record SavedSession(string Origin, string Token, DateTimeOffset ExpiresAt);
}

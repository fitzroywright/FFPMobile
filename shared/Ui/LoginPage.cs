using FFPMobile.Shared.Profiles;

namespace FFPMobile.Shared.Ui;

public sealed class LoginPage : ContentPage
{
    public LoginPage(AppProfile profile)
    {
        Title = "FFP Mobile";
        BackgroundColor = AppColors.Background;
        var username = new Entry { Placeholder = "User Name", Keyboard = Keyboard.Text };
        var password = new Entry { Placeholder = "Password", IsPassword = true };
        var preview = new Button
        {
            Text = "Explore Interface (Preview)",
            BackgroundColor = AppColors.Blue,
            TextColor = Colors.White,
            CornerRadius = 10,
            HeightRequest = 54
        };
        preview.Clicked += async (_, _) =>
            await Navigation.PushAsync(new DashboardPage(profile));
        var card = new VerticalStackLayout
        {
            Spacing = 18,
            Padding = new Thickness(34),
            BackgroundColor = Colors.White,
            MaximumWidthRequest = 520,
            Children =
            {
                new Label { Text = "FFP", FontSize = 44, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Blue, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Title, FontSize = 31, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Text, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = profile.Subtitle, FontSize = 21, TextColor = AppColors.Muted,
                    HorizontalTextAlignment = TextAlignment.Center },
                username,
                password,
                new Label { Text = "FFP Manager sign-in will be enabled after secure identity integration.",
                    FontSize = 13, TextColor = AppColors.Muted },
                preview,
                new Label { Text = "PREVIEW ONLY — no authentication, transactions or server access.",
                    FontSize = 12, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center }
            }
        };
        Content = new ScrollView
        {
            Content = new Grid
            {
                Padding = new Thickness(20, 36),
                RowDefinitions = { new RowDefinition { Height = GridLength.Star } },
                Children = { card },
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            }
        };
        card.HorizontalOptions = LayoutOptions.Center;
        card.VerticalOptions = LayoutOptions.Center;
    }
}

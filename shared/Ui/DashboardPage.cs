using FFPMobile.Shared.Profiles;

namespace FFPMobile.Shared.Ui;

public sealed class DashboardPage : ContentPage
{
    public DashboardPage(AppProfile profile)
    {
        Title = profile.Subtitle;
        BackgroundColor = AppColors.Background;
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(22),
            Spacing = 14,
            Children =
            {
                new Label { Text = "FFP Mobile", FontSize = 30, FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Navy },
                new Label { Text = profile.Subtitle, FontSize = 21, TextColor = AppColors.Text },
                new Label { Text = "Interface preview — no live ERP connection, authentication or offline synchronization.",
                    TextColor = AppColors.Muted, FontSize = 14 },
            }
        };
        foreach (var module in profile.Modules)
        {
            var button = new Button
            {
                Text = module.Title,
                BackgroundColor = Colors.White,
                TextColor = AppColors.Navy,
                CornerRadius = 12,
                HeightRequest = 64,
                HorizontalOptions = LayoutOptions.Fill
            };
            button.Clicked += async (_, _) => await DisplayAlertAsync(
                module.Title, module.Description + "\\n\\nModule implementation pending.", "OK");
            content.Children.Add(button);
        }
        Content = new ScrollView { Content = content };
    }
}

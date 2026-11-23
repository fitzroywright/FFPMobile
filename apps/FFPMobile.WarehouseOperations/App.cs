using FFPMobile.Shared.Profiles;
using FFPMobile.Shared.Ui;

namespace FFPMobile.App;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var profile = new AppProfile("FFP Mobile", "Warehouse & Operations",
            "FFP Mobile Warehouse & Operations Android tablet application",
            new ModuleDescriptor[]
            {
                new("Purchase Requisitions", "Create purchasing requests."),
                new("Payment Requests", "Prepare payment requisitions."),
                new("Delivery Orders", "Prepare stock deliveries."),
                new("Returns to Stock", "Record returns linked to original issues."),
                new("Count Sheets", "Capture physical inventory counts.")
            });
        return new Window(new NavigationPage(new LoginPage(profile, "warehouse-operations")));
    }
}

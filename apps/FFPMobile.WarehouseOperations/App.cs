using FFPMobile.Shared.Profiles;

namespace FFPMobile.App;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var profile = new AppProfile("FFP Mobile", "Warehouse & Operations",
            "Food For The Poor Jamaica", new ModuleDescriptor[]
            {
                new("Stock Counts", "Physical inventory counts."),
                new("Inventory Reports", "Warehouse inventory reports."),
                new("Delivery Orders", "Warehouse delivery orders."),
                new("Return to Stock", "Stock returned from delivery orders."),
                new("Goods Receival", "Warehouse goods receival notes.")
            });
        var page = new WarehousePage(profile);
        var window = new Window(new NavigationPage(page));
        window.Stopped += (_, _) => page.Pause();
        window.Resumed += async (_, _) => await page.ResumeAsync();
        return window;
    }
}

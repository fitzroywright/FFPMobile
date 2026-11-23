using FFPMobile.Shared.Profiles;
using FFPMobile.Shared.Ui;

namespace FFPMobile.App;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var profile = new AppProfile("FFP Mobile", "Security Post",
            "FFP Mobile Security Post Android tablet application",
            new ModuleDescriptor[]
            {
                new("Dispatch Verification", "Verify authorized delivery releases and record gate departure."),
                new("Vehicle Movements", "Record entry and exit events."),
                new("Visitor Log", "Capture checkpoint visitor events."),
                new("Pending Events", "Review events awaiting synchronization.")
            });
        return new Window(new NavigationPage(new LoginPage(profile, "security-post")));
    }
}

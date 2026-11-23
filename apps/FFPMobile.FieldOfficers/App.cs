using FFPMobile.Shared.Profiles;
using FFPMobile.Shared.Ui;

namespace FFPMobile.App;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var profile = new AppProfile("FFP Mobile", "Field Officers",
            "FFP Mobile Field Officers Android tablet application",
            new ModuleDescriptor[]
            {
                new("Field Visit", "Capture field visit observations and reports offline."),
                new("Client Follow-up", "Record client follow-ups and supporting evidence."),
                new("Assigned Work", "Review locally cached assigned work."),
                new("My Records", "Review local drafts and synchronization state.")
            });
        return new Window(new NavigationPage(new LoginPage(profile)));
    }
}

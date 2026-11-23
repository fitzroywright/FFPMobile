namespace FFPMobile.Shared.Profiles;

public sealed record AppProfile(
    string Title,
    string Subtitle,
    string Description,
    IReadOnlyList<ModuleDescriptor> Modules);

public sealed record ModuleDescriptor(string Title, string Description);

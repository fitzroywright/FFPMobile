using FFPMobile.FfpManagerBridge;
using FFPMobile.Shared.WarehouseProtocol;

namespace FFPMobile.Api;

internal sealed record MobileDemo(string LoginName, IReadOnlyList<string> Modules);

internal static class MobileDemos
{
    private static readonly MobileDemo Warehouse = new("warehouse-demo", WarehouseModules.All);
    private static readonly MobileDemo Field = new("field-demo", OtherMobileModules.FieldOfficers);
    private static readonly MobileDemo Security = new("security-demo", OtherMobileModules.SecurityPost);

    public static MobileDemo? ForApplication(string application) => application switch
    {
        Protocol.Application => Warehouse,
        Protocol.FieldOfficers => Field,
        Protocol.SecurityPost => Security,
        _ => null
    };
}

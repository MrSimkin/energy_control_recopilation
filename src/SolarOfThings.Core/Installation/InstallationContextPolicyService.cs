namespace SolarOfThings.Core.Installation;

public sealed record InstallationContextPolicy(
    double NormalGridTransferSocPercent,
    double EmergencyFloorSocPercent,
    double ReturnToBatterySocPercent,
    double RestartAfterLowSocPercent,
    bool ZeroExportExpected,
    bool SolarOnlyBatteryChargingExpected);

public sealed class InstallationContextPolicyService
{
    public const string ContextVersion = "family-manual-v2.0-2026-09-25";

    public InstallationContextPolicy Current { get; } = new(
        NormalGridTransferSocPercent: 20,
        EmergencyFloorSocPercent: 10,
        ReturnToBatterySocPercent: 50,
        RestartAfterLowSocPercent: 50,
        ZeroExportExpected: true,
        SolarOnlyBatteryChargingExpected: true);
}

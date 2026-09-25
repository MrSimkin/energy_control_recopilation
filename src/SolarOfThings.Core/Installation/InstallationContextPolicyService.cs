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
    public InstallationContextPolicy Current { get; } = new(
        NormalGridTransferSocPercent: 20,
        EmergencyFloorSocPercent: 10,
        ReturnToBatterySocPercent: 50,
        RestartAfterLowSocPercent: 50,
        ZeroExportExpected: true,
        SolarOnlyBatteryChargingExpected: true);
}

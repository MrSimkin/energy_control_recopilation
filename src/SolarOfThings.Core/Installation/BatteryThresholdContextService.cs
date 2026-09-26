using System.Globalization;
using System.Text.Json;

namespace SolarOfThings.Core.Installation;

public sealed record BatteryThresholdContext(double EmergencyFloorSocPercent, double NormalGridTransferSocPercent, double ReturnToBatterySocPercent, bool UsesObservedSettings, bool HasDrift, DateTimeOffset? ObservedAtUtc);

public sealed class BatteryThresholdContextService
{
    private readonly InstallationHealthRepository _repository;
    private readonly InstallationContextPolicyService _policy;
    public BatteryThresholdContextService(InstallationHealthRepository repository, InstallationContextPolicyService policy){_repository=repository;_policy=policy;}

    public BatteryThresholdContext Get(string deviceId)
    {
        var fallback=_policy.Current;
        if(string.IsNullOrWhiteSpace(deviceId)) return FromFallback(fallback);
        var summary=_repository.GetSummary(deviceId);
        if(summary is null) return FromFallback(fallback);
        var floor=ReadObserved(summary,"absolute-floor");
        var grid=ReadObserved(summary,"grid-transfer");
        var returning=ReadObserved(summary,"return-to-battery");
        if(!floor.HasValue||!grid.HasValue||!returning.HasValue) return FromFallback(fallback);
        var keys=new HashSet<string>(["absolute-floor","grid-transfer","return-to-battery"],StringComparer.Ordinal);
        var relevant=summary.Checks.Where(check=>keys.Contains(check.CheckKey)).ToArray();
        var times=relevant.Where(check=>check.ObservedAtUtc.HasValue).Select(check=>check.ObservedAtUtc!.Value).ToArray();
        return new BatteryThresholdContext(floor.Value,grid.Value,returning.Value,true,relevant.Any(check=>check.Status=="CONFIG_DRIFT"),times.Length==0?null:times.Max());
    }

    private static BatteryThresholdContext FromFallback(InstallationContextPolicy policy)=>new(policy.EmergencyFloorSocPercent,policy.NormalGridTransferSocPercent,policy.ReturnToBatterySocPercent,false,false,null);

    private static double? ReadObserved(InstallationConfigSummary summary,string checkKey)
    {
        var check=summary.Checks.FirstOrDefault(item=>string.Equals(item.CheckKey,checkKey,StringComparison.Ordinal));
        if(check is null||check.Status=="CONFIG_UNRESOLVED"||string.IsNullOrWhiteSpace(check.ObservedValueJson)) return null;
        try
        {
            using var document=JsonDocument.Parse(check.ObservedValueJson);
            var value=document.RootElement;
            var text=value.ValueKind switch{JsonValueKind.Number=>value.GetRawText(),JsonValueKind.String=>value.GetString(),_=>null};
            return double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)?parsed:null;
        }
        catch
        {
            return double.TryParse(check.ObservedValueJson.Trim().Trim('"'),NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)?parsed:null;
        }
    }
}

using System.Globalization;
using System.Text.Json;
using SolarOfThings.Core.Normalization;

namespace SolarOfThings.Core.Installation;

public sealed record CurrentHouseholdSnapshot(DateTimeOffset RetrievedUtc, DateTimeOffset? ObservedAtUtc, bool IsFresh, IReadOnlyDictionary<string, NormalizedMetricValue> Metrics);

public sealed class CurrentHouseholdSnapshotService
{
    private static readonly TimeSpan FreshnessLimit=TimeSpan.FromMinutes(20);
    private readonly InstallationHealthRepository _repository;
    public CurrentHouseholdSnapshotService(InstallationHealthRepository repository)=>_repository=repository;

    public CurrentHouseholdSnapshot? GetLatest(string deviceId)
    {
        var snapshot=_repository.GetLatestStateSnapshot(deviceId);
        if(snapshot is null) return null;
        try
        {
            using var document=JsonDocument.Parse(snapshot.StateJson);
            var root=document.RootElement;
            DateTimeOffset? observedAt=null;
            if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("time",out var time)&&time.ValueKind==JsonValueKind.String&&DateTimeOffset.TryParse(time.GetString(),CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var parsed)) observedAt=parsed;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("fields",out var fields)||fields.ValueKind!=JsonValueKind.Object)
                return new CurrentHouseholdSnapshot(snapshot.RetrievedUtc,observedAt,false,new Dictionary<string,NormalizedMetricValue>());
            var metrics=new Dictionary<string,NormalizedMetricValue>(StringComparer.Ordinal);
            if(TryReadAny(fields,["generationPower","pvPower"],out var pv,out var pvKey)) AddMetric(metrics,"pv_power_w",pv*1000.0,"W",pvKey,observedAt);
            if(TryReadAny(fields,["outputActivePower"],out var house,out var houseKey)) AddMetric(metrics,"house_load_power_w",house*1000.0,"W",houseKey,observedAt);
            if(TryReadAny(fields,["mainsPower"],out var grid,out var gridKey)&&grid>=0) AddMetric(metrics,"grid_import_power_w",grid*1000.0,"W",gridKey,observedAt);
            if(TryReadAny(fields,["bmsCurrentSOC","batteryCapacity"],out var soc,out var socKey)&&soc is >=0 and <=100) AddMetric(metrics,"battery_soc_pct",soc,"%",socKey,observedAt);
            if(TryReadAny(fields,["batteryVoltage"],out var voltage,out var voltageKey)&&voltage is >=0 and < 100) AddMetric(metrics,"battery_voltage_v",voltage,"V",voltageKey,observedAt);
            if(TryReadAny(fields,["bmsChargingCurrent","batteryChargingCurrent"],out var chargeCurrent,out var chargeKey)&&chargeCurrent is >=0 and < 500) AddMetric(metrics,"battery_charge_current_a",chargeCurrent,"A",chargeKey,observedAt);
            if(TryReadAny(fields,["batteryDischargeCurrent","bmsDischargeCurrent"],out var dischargeCurrent,out var dischargeKey)&&dischargeCurrent is >=0 and < 500) AddMetric(metrics,"battery_discharge_current_a",dischargeCurrent,"A",dischargeKey,observedAt);
            if(metrics.TryGetValue("battery_voltage_v",out var batteryVoltage)&&metrics.TryGetValue("battery_charge_current_a",out var liveCharge)&&metrics.TryGetValue("battery_discharge_current_a",out var liveDischarge))
            {
                var batteryPower=batteryVoltage.Value*(liveDischarge.Value-liveCharge.Value);
                metrics["battery_power_w"]=new NormalizedMetricValue("battery_power_w",observedAt!.Value,batteryPower,"W",$"{batteryVoltage.SourceAttributeKey}+{liveCharge.SourceAttributeKey}+{liveDischarge.SourceAttributeKey}","latest-state.v1","PROBABLE","CURRENT_STATE_DERIVED_VOLTAGE_X_DIRECTIONAL_CURRENTS");
            }
            var age=observedAt.HasValue?DateTimeOffset.UtcNow-observedAt.Value.ToUniversalTime():TimeSpan.MaxValue;
            var fresh=observedAt.HasValue&&age>=TimeSpan.FromMinutes(-5)&&age<=FreshnessLimit;
            return new CurrentHouseholdSnapshot(snapshot.RetrievedUtc,observedAt,fresh,metrics);
        }
        catch{return null;}
    }

    private static void AddMetric(IDictionary<string,NormalizedMetricValue> metrics,string metricKey,double value,string unit,string sourceKey,DateTimeOffset? observedAt)
    {
        if(!observedAt.HasValue)return;
        metrics[metricKey]=new NormalizedMetricValue(metricKey,observedAt.Value,value,unit,sourceKey,"latest-state.v1","CONFIRMED","CURRENT_STATE_SNAPSHOT");
    }

    private static bool TryReadAny(JsonElement fields,IReadOnlyList<string> keys,out double value,out string sourceKey)
    {
        foreach(var key in keys) if(TryReadField(fields,key,out value)){sourceKey=key;return true;}
        value=0;sourceKey=string.Empty;return false;
    }

    private static bool TryReadField(JsonElement fields,string key,out double value)
    {
        value=0;
        if(!fields.TryGetProperty(key,out var field)||field.ValueKind!=JsonValueKind.Object||!field.TryGetProperty("value",out var raw))return false;
        if(raw.ValueKind==JsonValueKind.Number)return raw.TryGetDouble(out value);
        if(raw.ValueKind==JsonValueKind.String)return double.TryParse(raw.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out value);
        return false;
    }
}

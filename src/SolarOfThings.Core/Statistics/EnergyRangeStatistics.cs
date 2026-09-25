namespace SolarOfThings.Core.Statistics;

public sealed record PowerMetricStatistics(
    string MetricKey,
    DateTimeOffset? FirstSampleUtc,
    DateTimeOffset? LastSampleUtc,
    int SampleCount,
    double? MinimumWatts,
    double? MaximumWatts,
    double? AverageWatts,
    double NetEnergyKwh,
    double PositiveEnergyKwh,
    double NegativeEnergyKwh,
    double CoveredHours,
    double UncoveredHours,
    double CoveragePercent,
    double ObservedMedianGapMinutes,
    double ContinuityThresholdMinutes);

public sealed record EnergyRangeSummary(
    string DeviceId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    PowerMetricStatistics PvPower,
    PowerMetricStatistics HouseLoadPower,
    PowerMetricStatistics GridImportPower,
    PowerMetricStatistics BatteryPower)
{
    public double PvEnergyKwh => PvPower.PositiveEnergyKwh;
    public double HouseEnergyKwh => HouseLoadPower.PositiveEnergyKwh;
    public double GridImportEnergyKwh => GridImportPower.PositiveEnergyKwh;
    public double BatteryDischargedEnergyKwh => BatteryPower.PositiveEnergyKwh;
    public double BatteryChargedEnergyKwh => BatteryPower.NegativeEnergyKwh;
}

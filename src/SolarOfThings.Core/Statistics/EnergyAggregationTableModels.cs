namespace SolarOfThings.Core.Statistics;

public sealed record EnergyAggregationRow(
    string LocalLabel,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    double PvEnergyKwh,
    double HouseEnergyKwh,
    double GridImportEnergyKwh,
    double BatteryDischargedEnergyKwh,
    double BatteryChargedEnergyKwh,
    double? SocAveragePercent,
    double? SocMinimumPercent,
    double? SocMaximumPercent,
    double? SocEndingPercent,
    double PvCoveragePercent,
    double HouseCoveragePercent,
    double GridCoveragePercent,
    double BatteryCoveragePercent,
    double SocCoveragePercent,
    double MinimumAvailableCoveragePercent)
{
    public double? PvEnergyDisplayKwh => PvCoveragePercent > 0 ? PvEnergyKwh : null;
    public double? HouseEnergyDisplayKwh => HouseCoveragePercent > 0 ? HouseEnergyKwh : null;
    public double? GridImportEnergyDisplayKwh => GridCoveragePercent > 0 ? GridImportEnergyKwh : null;
    public double? BatteryDischargedEnergyDisplayKwh => BatteryCoveragePercent > 0 ? BatteryDischargedEnergyKwh : null;
    public double? BatteryChargedEnergyDisplayKwh => BatteryCoveragePercent > 0 ? BatteryChargedEnergyKwh : null;
}

public sealed record EnergyAggregationTable(
    string DeviceId,
    AggregationPeriod Period,
    string TimeZoneId,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    IReadOnlyList<EnergyAggregationRow> Rows);

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
    double MinimumAvailableCoveragePercent);

public sealed record EnergyAggregationTable(
    string DeviceId,
    AggregationPeriod Period,
    string TimeZoneId,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    IReadOnlyList<EnergyAggregationRow> Rows);

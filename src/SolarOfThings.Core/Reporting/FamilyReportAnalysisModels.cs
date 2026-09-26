namespace SolarOfThings.Core.Reporting;

public sealed record FamilyEnergyEvent(
    string EventKind,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset StartLocal,
    DateTimeOffset EndLocal,
    double DurationMinutes,
    double StartSocPercent,
    double MinimumSocPercent,
    double NormalGridTransferSocPercent,
    double MaximumGridWatts,
    double MaximumPvWatts);

public sealed record FamilyNightObservation(
    DateOnly NightStartDate,
    double CoveragePercent,
    bool IsObservable,
    int ReserveGridEpisodeCount,
    double ReserveGridMinutes);

public sealed record FamilyHourlyPattern(
    string MetricKey,
    int StartHour,
    int EndHourExclusive,
    double TypicalWatts,
    int ObservedDays,
    int OpportunityDays);

public sealed record FamilyPeriodHighlight(
    string MetricKey,
    string LocalLabel,
    double Value,
    string Unit,
    double CoveragePercent);

public sealed record FamilyEvolutionRow(
    string LocalLabel,
    double? PvEnergyKwh,
    double? HouseEnergyKwh,
    double? GridImportEnergyKwh,
    double? BatteryDischargedEnergyKwh,
    double MinimumCoveragePercent);

public sealed record FamilyReportAnalysis(
    double NormalGridTransferSocPercent,
    double ReturnToBatterySocPercent,
    bool UsesObservedThresholds,
    double FrameCoveragePercent,
    int ObservableNightCount,
    int NightsWithReserveGridUse,
    int ReserveGridEpisodeCount,
    double ReserveGridTotalMinutes,
    TimeOnly? TypicalReserveTime,
    FamilyHourlyPattern? HighestHouseConsumptionWindow,
    FamilyHourlyPattern? HighestSolarGenerationWindow,
    FamilyHourlyPattern? HighestGridUseWindow,
    IReadOnlyList<FamilyEnergyEvent> Events,
    IReadOnlyList<FamilyNightObservation> Nights,
    IReadOnlyList<FamilyPeriodHighlight> Highlights,
    string EvolutionAggregation,
    IReadOnlyList<FamilyEvolutionRow> Evolution);

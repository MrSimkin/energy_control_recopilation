namespace SolarOfThings.Core.History;

/// <summary>
/// Deliberate user-requested local-day recheck. Distinct from automatic
/// history overlap, automatic retries and manual FROM-date backfill. A single
/// selected day never licenses scanning from that date through today.
/// </summary>
public static class HistoryDayRecheckPolicy
{
    public static IReadOnlyList<DateOnly> Resolve(
        DateOnly selectedDay, DateOnly stationToday,
        DateOnly? installationDay)
    {
        if (selectedDay > stationToday)
            throw new InvalidOperationException(
                "The selected recheck day is in the station's future.");
        if (installationDay.HasValue && selectedDay < installationDay.Value)
            throw new InvalidOperationException(
                "The selected recheck day precedes the inverter installation.");
        return [selectedDay];
    }
}

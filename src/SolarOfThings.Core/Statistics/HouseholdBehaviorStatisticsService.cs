using SolarOfThings.Core.Data;
using SolarOfThings.Core.Installation;

namespace SolarOfThings.Core.Statistics;

public sealed class HouseholdBehaviorStatisticsService
{
    private readonly SqliteDatabase _database;

    public HouseholdBehaviorStatisticsService(SqliteDatabase database)
    {
        _database = database;
    }

    public HouseholdBehaviorStatistics Get(
        string deviceId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT recorded_at_utc, state_key
            FROM household_behavior_sample
            WHERE device_id = $deviceId
              AND context_version = $contextVersion
              AND ($fromUtc IS NULL OR recorded_at_utc >= $fromUtc)
              AND ($toUtc IS NULL OR recorded_at_utc <= $toUtc)
            ORDER BY recorded_at_utc;
            """;

        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue(
            "$contextVersion",
            InstallationContextPolicyService.ContextVersion);
        command.Parameters.AddWithValue(
            "$fromUtc",
            fromUtc.HasValue ? fromUtc.Value.ToUniversalTime().ToString("O") : DBNull.Value);
        command.Parameters.AddWithValue(
            "$toUtc",
            toUtc.HasValue ? toUtc.Value.ToUniversalTime().ToString("O") : DBNull.Value);

        using var reader = command.ExecuteReader();
        var samples = new List<BehaviorPoint>();

        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(0), out var timestamp))
            {
                continue;
            }

            samples.Add(new BehaviorPoint(
                timestamp.ToUniversalTime(),
                reader.GetString(1)));
        }

        var stateSampleCounts = samples
            .GroupBy(sample => sample.StateKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

        if (samples.Count < 2)
        {
            return new HouseholdBehaviorStatistics(
                deviceId,
                InstallationContextPolicyService.ContextVersion,
                samples.FirstOrDefault()?.TimestampUtc,
                samples.LastOrDefault()?.TimestampUtc,
                samples.Count,
                0,
                0,
                0,
                0,
                0,
                0,
                new Dictionary<string, double>(StringComparer.Ordinal),
                stateSampleCounts);
        }

        var gaps = new List<double>(samples.Count - 1);
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var minutes =
                (samples[i + 1].TimestampUtc - samples[i].TimestampUtc).TotalMinutes;

            if (minutes > 0)
            {
                gaps.Add(minutes);
            }
        }

        var medianGap = Median(gaps);

        // Derive continuity tolerance from the actual observed cadence.
        // Never allow a long data hole to be interpreted as continuous state.
        var continuityThreshold = medianGap > 0
            ? Math.Clamp(medianGap * 3.0, 10.0, 20.0)
            : 15.0;

        var stateDurations = new Dictionary<string, double>(StringComparer.Ordinal);
        var coveredMinutes = 0.0;
        var uncoveredGapMinutes = 0.0;
        var transitions = 0;

        for (var i = 0; i < samples.Count - 1; i++)
        {
            var current = samples[i];
            var next = samples[i + 1];
            var gapMinutes = (next.TimestampUtc - current.TimestampUtc).TotalMinutes;

            if (gapMinutes <= 0)
            {
                continue;
            }

            if (gapMinutes > continuityThreshold)
            {
                uncoveredGapMinutes += gapMinutes;
                continue;
            }

            coveredMinutes += gapMinutes;
            stateDurations[current.StateKey] =
                stateDurations.TryGetValue(current.StateKey, out var existing)
                    ? existing + gapMinutes
                    : gapMinutes;

            if (!string.Equals(
                    current.StateKey,
                    next.StateKey,
                    StringComparison.Ordinal))
            {
                transitions++;
            }
        }

        var denominator = coveredMinutes + uncoveredGapMinutes;
        var coveragePercent = denominator > 0
            ? coveredMinutes / denominator * 100.0
            : 0;

        return new HouseholdBehaviorStatistics(
            deviceId,
            InstallationContextPolicyService.ContextVersion,
            samples[0].TimestampUtc,
            samples[^1].TimestampUtc,
            samples.Count,
            transitions,
            medianGap,
            continuityThreshold,
            coveredMinutes,
            uncoveredGapMinutes,
            coveragePercent,
            stateDurations,
            stateSampleCounts);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var middle = ordered.Length / 2;

        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }

    private sealed record BehaviorPoint(
        DateTimeOffset TimestampUtc,
        string StateKey);
}

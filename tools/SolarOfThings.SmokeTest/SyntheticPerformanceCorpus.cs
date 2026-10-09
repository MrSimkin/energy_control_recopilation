using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Normalization;
using SolarOfThings.Core.Statistics;

/// <summary>
/// Opt-in, isolated SQLite corpus to compare query plans and timings.
/// No production database, owner paths, telemetry, secrets, or live sources.
/// This is not a controlled benchmark of the owner's 2 GB Windows database.
/// </summary>
internal static class SyntheticPerformanceCorpus
{
    private const string Device = "synthetic-performance-device";
    private const int DefaultFrames = 24_000;
    private const int LargerFrames = 96_000;
    private const int WarmRepeats = 7;
    private static readonly string[] Metrics =
    [
        "pv_power_w", "house_load_power_w", "grid_import_power_w",
        "battery_power_w", "battery_soc_pct"
    ];

    // Preserved Build 700 reference query for both timings and full-record
    // parity checks after changing the production method to grouped MAX.
    // This SQL is never used by the production application.
    private const string BaselineSql = """
        WITH ranked AS (
            SELECT metric_key, recorded_at_utc, normalized_value,
                   normalized_unit, source_attribute_key,
                   normalization_rule_version, confidence, quality,
                   ROW_NUMBER() OVER (
                       PARTITION BY metric_key ORDER BY recorded_at_utc DESC
                   ) AS row_number
            FROM normalized_metric_sample
            WHERE device_id = $deviceId AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
        )
        SELECT metric_key, recorded_at_utc, normalized_value,
               normalized_unit, source_attribute_key,
               normalization_rule_version, confidence, quality
        FROM ranked WHERE row_number = 1;
        """;

    // Production candidate after measured synthetic parity on Build 702.
    // The composite PK makes the join unambiguous.
    private const string CandidateSql = """
        WITH newest AS (
            SELECT metric_key, MAX(recorded_at_utc) AS latest_utc
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
            GROUP BY metric_key
        )
        SELECT sample.metric_key, sample.recorded_at_utc,
               sample.normalized_value, sample.normalized_unit,
               sample.source_attribute_key, sample.normalization_rule_version,
               sample.confidence, sample.quality
        FROM newest
        JOIN normalized_metric_sample AS sample
          ON sample.device_id = $deviceId
         AND sample.metric_key = newest.metric_key
         AND sample.recorded_at_utc = newest.latest_utc;
        """;

    public static void Run(bool large = false)
    {
        var frames = large ? LargerFrames : DefaultFrames;
        var root = Path.Combine(Path.GetTempPath(),
            "SolarEnergyMonitorSyntheticPerformance",
            Guid.NewGuid().ToString("N"));
        try
        {
            // Always explicit synthetic root override. This cannot resolve
            // to the application's shared QA Data path on Windows.
            var database = new SqliteDatabase(new AppPaths(root));
            database.Initialize();
            Populate(database, frames);
            var file = new FileInfo(database.DatabasePath);
            Console.WriteLine("PERFORMANCE_CORPUS schema=" +
                SqliteDatabase.CurrentSchemaVersion + " frames=" + frames +
                " normalized_rows=" + (frames * Metrics.Length + Metrics.Length + 2) +
                " history_rows=" + frames + " db_bytes=" + file.Length +
                " corpus_scale=" + (large ? "large" : "base") +
                " measured_on=CI_synthetic_not_owner_PC");

            var repo = new NormalizationRepository(database);
            var before = GetReferenceWindow(database);
            var alternative = repo.GetLatestMetrics(Device);
            RequireParity(before, alternative);

            // A second independent device contains much later timestamps;
            // neither baseline nor alternative may leak cross-device values.
            if (before.Values.Any(value =>
                value.RecordedAtUtc >= new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)))
                throw new InvalidOperationException("Latest query leaked another device.");

            using (var connection = database.OpenConnection())
            {
                PrintPlan(connection, "existing_window", BaselineSql);
                PrintPlan(connection, "candidate_group_max", CandidateSql);
            }

            // Explicitly label first measured call as connection-warm / OS-cache-
            // unknown; neither a physical disk cold start nor a speed promise.
            Measure("latest_window_first", () => GetReferenceWindow(database));
            Measure("latest_max_first", () => repo.GetLatestMetrics(Device));
            var oldTimes = new double[WarmRepeats];
            var newTimes = new double[WarmRepeats];
            for (var i = 0; i < WarmRepeats; i++)
            {
                if (i % 2 == 0)
                {
                    oldTimes[i] = Measure("latest_window_warm", () =>
                        GetReferenceWindow(database), print: false);
                    newTimes[i] = Measure("latest_max_warm", () =>
                        repo.GetLatestMetrics(Device), print: false);
                }
                else
                {
                    newTimes[i] = Measure("latest_max_warm", () =>
                        repo.GetLatestMetrics(Device), print: false);
                    oldTimes[i] = Measure("latest_window_warm", () =>
                        GetReferenceWindow(database), print: false);
                }
            }
            PrintDistribution("latest_window_warm", oldTimes);
            PrintDistribution("latest_max_warm", newTimes);

            var coverage = new HistoryRepository(database);
            var summary = coverage.GetCoverageSummary(Device);
            if (summary.RawSampleCount != frames)
                throw new InvalidOperationException("Synthetic history count mismatch.");
            var normalizedCount = repo.GetNormalizedSampleCount(Device);
            if (normalizedCount != frames * Metrics.Length + 2)
                throw new InvalidOperationException("Synthetic normalized count mismatch.");
            Measure("history_coverage", () => coverage.GetCoverageSummary(Device));
            Measure("normalized_count", () => repo.GetNormalizedSampleCount(Device));

            var start = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var from = start.AddMinutes((frames - 481) * 3);
            var to = start.AddMinutes((frames - 1) * 3);
            var statistics = new EnergyRangeStatisticsService(database);
            var energy = statistics.Get(Device, from, to);
            if (energy.PvPower.SampleCount <= 0 ||
                !double.IsFinite(energy.PvEnergyKwh) ||
                energy.PvEnergyKwh < 0)
                throw new InvalidOperationException("Synthetic energy statistics invalid.");
            Measure("energy_day_four_metrics", () => statistics.Get(Device, from, to));
            var grouped = new EnergyAggregationTableService(
                new PowerAggregationService(database),
                new SocAggregationService(database));
            var groupedHistory = grouped.Get(Device, from, to, "UTC", AggregationPeriod.Day);
            if (groupedHistory.Rows.Count == 0 ||
                groupedHistory.Rows.Any(x => !double.IsFinite(x.PvEnergyKwh)))
                throw new InvalidOperationException("Synthetic grouped report data invalid.");
            Measure("report_day_five_streams", () =>
                grouped.Get(Device, from, to, "UTC", AggregationPeriod.Day));
            using var preCancelledAggregation = new CancellationTokenSource();
            preCancelledAggregation.Cancel();
            var cancelled = false;
            try { grouped.Get(Device, from, to, "UTC", AggregationPeriod.Day,
                preCancelledAggregation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            if (!cancelled)
                throw new InvalidOperationException("Grouped report ignored cancellation.");
            Console.WriteLine("PERFORMANCE_CORPUS PASS query_parity=ALL_FIELDS " +
                "sqlite_schema=17 real_user_data=NOT_ACCESSED");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void Populate(SqliteDatabase database, int frames)
    {
        var start = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var metric = connection.CreateCommand();
        metric.Transaction = transaction;
        metric.CommandText = """
            INSERT INTO normalized_metric_sample
                (device_id, metric_key, recorded_at_utc, normalized_value,
                 normalized_unit, source_attribute_key, source_value_json,
                 normalization_rule_version, confidence, quality, updated_utc)
            VALUES ($device, $key, $utc, $value, $unit, 'synthetic',
                    NULL, 'synthetic-v1', $confidence, 'SYNTHETIC', $utc);
            """;
        foreach (var key in new[] { "$device", "$key", "$utc", "$value", "$unit", "$confidence" })
            metric.Parameters.Add(new SqliteParameter(key, DBNull.Value));

        using var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = """
            INSERT INTO history_sample
                (device_id, attribute_key, recorded_at_utc, value_json,
                 is_missing, source, retrieved_utc)
            VALUES ($device, 'synthetic-attribute', $utc, '1000', 0,
                    'SYNTHETIC', $utc);
            """;
        history.Parameters.AddWithValue("$device", Device);
        history.Parameters.Add(new SqliteParameter("$utc", DBNull.Value));

        for (var i = 0; i < frames; i++)
        {
            var timestamp = start.AddMinutes(3 * i).ToString("O", CultureInfo.InvariantCulture);
            history.Parameters["$utc"].Value = timestamp;
            history.ExecuteNonQuery();

            for (var j = 0; j < Metrics.Length; j++)
            {
                metric.Parameters["$device"].Value = Device;
                metric.Parameters["$key"].Value = Metrics[j];
                metric.Parameters["$utc"].Value = timestamp;
                // The most recent PV reading is NULL and most recent house
                // reading is UNRESOLVED: return their earlier eligible values.
                metric.Parameters["$value"].Value = (i == frames - 1 && j == 0) ||
                    (i + j) % 41 == 0
                    ? DBNull.Value : (object)(j == 4 ? 50.0 + i % 40 : 500.0 + (i + j) % 750);
                metric.Parameters["$unit"].Value = j == 4 ? "%" : "W";
                metric.Parameters["$confidence"].Value =
                    (i == frames - 1 && j == 1) || (i + j) % 37 == 0
                        ? "UNRESOLVED" : "HIGH";
                metric.ExecuteNonQuery();
            }
        }
        // Two deliberately ineligible categories must never appear in latest.
        foreach (var (key, value, confidence) in new[]
        {
            (Key: "synthetic_null_only", Value: (object)DBNull.Value, Confidence: "HIGH"),
            (Key: "synthetic_unresolved_only", Value: (object)300.0, Confidence: "UNRESOLVED")
        })
        {
            metric.Parameters["$device"].Value = Device;
            metric.Parameters["$key"].Value = key;
            metric.Parameters["$utc"].Value =
                start.AddMinutes(3 * frames).ToString("O", CultureInfo.InvariantCulture);
            metric.Parameters["$value"].Value = value;
            metric.Parameters["$unit"].Value = "W";
            metric.Parameters["$confidence"].Value = confidence;
            metric.ExecuteNonQuery();
        }
        // Deliberate later timestamps in a different device, to catch
        // accidental loss of the device partition filter.
        for (var j = 0; j < Metrics.Length; j++)
        {
            metric.Parameters["$device"].Value = "synthetic-other-device";
            metric.Parameters["$key"].Value = Metrics[j];
            metric.Parameters["$utc"].Value = new DateTimeOffset(
                2030, 1, 1, 0, 0, j, TimeSpan.Zero).ToString("O");
            metric.Parameters["$value"].Value = 999999.0;
            metric.Parameters["$unit"].Value = j == 4 ? "%" : "W";
            metric.Parameters["$confidence"].Value = "HIGH";
            metric.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static IReadOnlyDictionary<string, NormalizedMetricValue> GetReferenceWindow(
        SqliteDatabase database)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = BaselineSql;
        command.Parameters.AddWithValue("$deviceId", Device);
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, NormalizedMetricValue>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(1), out var timestamp))
                continue;
            var key = reader.GetString(0);
            result[key] = new NormalizedMetricValue(
                key, timestamp, reader.GetDouble(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7));
        }
        return result;
    }

    private static void RequireParity(
        IReadOnlyDictionary<string, NormalizedMetricValue> baseline,
        IReadOnlyDictionary<string, NormalizedMetricValue> candidate)
    {
        if (baseline.Count != Metrics.Length || candidate.Count != baseline.Count ||
            baseline.Any(pair => !candidate.TryGetValue(pair.Key, out var value) ||
                                 value != pair.Value))
            throw new InvalidOperationException(
                "Latest-metric candidate changed eligible values, timestamps or metadata.");
    }

    private static void PrintPlan(SqliteConnection connection, string name, string query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + query;
        command.Parameters.AddWithValue("$deviceId", Device);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var detail = reader.GetString(3);
            // Explain output describes SQL planner operations, no actual data.
            Console.WriteLine("QUERY_PLAN " + name + ": " + detail);
        }
    }

    private static double Measure<T>(string name, Func<T> operation, bool print = true)
    {
        var clock = Stopwatch.StartNew();
        _ = operation();
        clock.Stop();
        var elapsed = clock.Elapsed.TotalMilliseconds;
        if (print)
            Console.WriteLine("OBSERVED " + name + " ms=" +
                elapsed.ToString("F2", CultureInfo.InvariantCulture));
        return elapsed;
    }

    private static void PrintDistribution(string name, double[] samples)
    {
        var sorted = samples.OrderBy(x => x).ToArray();
        var median = sorted[sorted.Length / 2];
        var p95 = sorted[(int)Math.Ceiling(0.95 * sorted.Length) - 1];
        Console.WriteLine("OBSERVED " + name +
            " iterations=" + sorted.Length +
            " median_ms=" + median.ToString("F2", CultureInfo.InvariantCulture) +
            " p95_ms=" + p95.ToString("F2", CultureInfo.InvariantCulture) +
            " max_ms=" + sorted[^1].ToString("F2", CultureInfo.InvariantCulture));
    }
}

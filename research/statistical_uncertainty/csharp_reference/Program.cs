using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.Utility;

const string timeZoneId = "America/Santiago";
var root = Path.Combine(
    Path.GetTempPath(),
    "SolarOfThingsStatisticalResearch",
    Guid.NewGuid().ToString("N"));

try
{
    var paths = new AppPaths(root);
    var database = new SqliteDatabase(paths);
    database.Initialize();

    ValidateBridged15MinuteCase(database);
    ValidateStatistical20MinuteCase(database);
    ValidateCompleteCase(database);

    Console.WriteLine("STATISTICAL_REFERENCE_VALIDATION_PASS");
}
finally
{
    try
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
    catch
    {
        // Research validation cleanup is best-effort.
    }
}

static void ValidateBridged15MinuteCase(SqliteDatabase database)
{
    const string deviceId = "research-bridge-15m";
    var localStart = new DateTimeOffset(
        2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-3));
    var localEnd = localStart.AddDays(2);

    InsertConstantSeries(
        database,
        deviceId,
        localStart,
        localEnd,
        watts: 1000,
        gapStartLocal: localStart.AddHours(12),
        gapDurationMinutes: 15);

    var statistics = new EnergyRangeStatisticsService(database);
    var completion = new UtilityGridImportStatisticalCompletionService(
        database,
        statistics).Analyze(
            deviceId,
            localStart.ToUniversalTime(),
            localEnd.ToUniversalTime(),
            timeZoneId);

    RequireNear(
        statistics.GetMetric(
            deviceId,
            "grid_import_power_w",
            localStart.ToUniversalTime(),
            localEnd.ToUniversalTime())
        .ContinuityThresholdMinutes,
        15.0,
        1e-9,
        "15m bridge threshold");

    Require(
        completion.Status == "COMPLETE_OBSERVATION",
        $"15m bridge should be COMPLETE_OBSERVATION, got {completion.Status}");
    RequireNear(completion.CoveragePercent, 100.0, 1e-9, "15m bridge coverage");
    RequireNear(completion.MissingHours, 0.0, 1e-9, "15m bridge missing hours");
    Require(completion.SimulationCount == 0, "15m bridge should not run simulations");
    RequireNear(completion.ObservedKwh, 48.0, 1e-9, "15m bridge observed kWh");
    RequireNear(completion.LowerKwh!.Value, 48.0, 1e-9, "15m bridge P5");
    RequireNear(completion.MedianKwh!.Value, 48.0, 1e-9, "15m bridge P50");
    RequireNear(completion.UpperKwh!.Value, 48.0, 1e-9, "15m bridge P95");

    Console.WriteLine(
        "PASS 15m bridged: source samples missing, but integration coverage=100% and no predictive interval");
}

static void ValidateStatistical20MinuteCase(SqliteDatabase database)
{
    const string deviceId = "research-gap-20m";
    var localStart = new DateTimeOffset(
        2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-3));
    var localEnd = localStart.AddDays(2);

    InsertConstantSeries(
        database,
        deviceId,
        localStart,
        localEnd,
        watts: 1000,
        gapStartLocal: localStart.AddHours(12),
        gapDurationMinutes: 20);

    var statistics = new EnergyRangeStatisticsService(database);
    var completion = new UtilityGridImportStatisticalCompletionService(
        database,
        statistics).Analyze(
            deviceId,
            localStart.ToUniversalTime(),
            localEnd.ToUniversalTime(),
            timeZoneId);

    var expectedObserved = 48.0 - 20.0 / 60.0;
    var expectedComplete = 48.0;

    Require(
        completion.Status == "EMPIRICAL_PREDICTIVE_INTERVAL",
        $"20m gap should be predictive, got {completion.Status}");
    Require(completion.SimulationCount == 2000, "20m gap should use current 2,000 simulations");
    RequireNear(completion.MissingHours, 20.0 / 60.0, 1e-9, "20m missing hours");
    RequireNear(completion.ObservedKwh, expectedObserved, 1e-9, "20m observed kWh");

    // Every donor is exactly 1 kW, so every simulated completion must equal truth.
    RequireNear(completion.LowerKwh!.Value, expectedComplete, 1e-9, "20m P5 exact fixture");
    RequireNear(completion.MedianKwh!.Value, expectedComplete, 1e-9, "20m P50 exact fixture");
    RequireNear(completion.UpperKwh!.Value, expectedComplete, 1e-9, "20m P95 exact fixture");
    RequireNear(completion.MeanKwh!.Value, expectedComplete, 1e-9, "20m mean exact fixture");
    RequireNear(completion.StandardDeviationKwh!.Value, 0.0, 1e-9, "20m sd exact fixture");

    Console.WriteLine(
        "PASS 20m predictive: direct C# service reconstructs analytically known 48.0 kWh exactly");
}

static void ValidateCompleteCase(SqliteDatabase database)
{
    const string deviceId = "research-complete";
    var localStart = new DateTimeOffset(
        2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-3));
    var localEnd = localStart.AddDays(1);

    InsertConstantSeries(
        database,
        deviceId,
        localStart,
        localEnd,
        watts: 750,
        gapStartLocal: null,
        gapDurationMinutes: 0);

    var statistics = new EnergyRangeStatisticsService(database);
    var completion = new UtilityGridImportStatisticalCompletionService(
        database,
        statistics).Analyze(
            deviceId,
            localStart.ToUniversalTime(),
            localEnd.ToUniversalTime(),
            timeZoneId);

    Require(completion.Status == "COMPLETE_OBSERVATION", "complete fixture status");
    RequireNear(completion.ObservedKwh, 18.0, 1e-9, "complete fixture observed");
    RequireNear(completion.LowerKwh!.Value, 18.0, 1e-9, "complete fixture P5");
    RequireNear(completion.MedianKwh!.Value, 18.0, 1e-9, "complete fixture P50");
    RequireNear(completion.UpperKwh!.Value, 18.0, 1e-9, "complete fixture P95");

    Console.WriteLine("PASS complete observation: P5=P50=P95=truth");
}

static void InsertConstantSeries(
    SqliteDatabase database,
    string deviceId,
    DateTimeOffset localStart,
    DateTimeOffset localEnd,
    double watts,
    DateTimeOffset? gapStartLocal,
    int gapDurationMinutes)
{
    using var connection = database.OpenConnection();
    using var transaction = connection.BeginTransaction();
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        INSERT INTO normalized_metric_sample (
            device_id,
            metric_key,
            recorded_at_utc,
            normalized_value,
            normalized_unit,
            source_attribute_key,
            source_value_json,
            normalization_rule_version,
            confidence,
            quality,
            updated_utc
        )
        VALUES (
            $deviceId,
            'grid_import_power_w',
            $recordedAtUtc,
            $value,
            'W',
            'research_fixture',
            NULL,
            'research.reference.v1',
            'CONFIRMED',
            'OK',
            $updatedUtc
        );
        """;

    var gapEnd = gapStartLocal?.AddMinutes(gapDurationMinutes);

    for (var cursor = localStart;
         cursor <= localEnd;
         cursor = cursor.AddMinutes(5))
    {
        var isInteriorGapPoint =
            gapStartLocal.HasValue &&
            gapEnd.HasValue &&
            cursor > gapStartLocal.Value &&
            cursor < gapEnd.Value;

        if (isInteriorGapPoint)
            continue;

        command.Parameters.Clear();
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue(
            "$recordedAtUtc",
            cursor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$value", watts);
        command.Parameters.AddWithValue(
            "$updatedUtc",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    transaction.Commit();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void RequireNear(
    double actual,
    double expected,
    double tolerance,
    string label)
{
    if (Math.Abs(actual - expected) > tolerance)
    {
        throw new InvalidOperationException(
            $"{label}: expected {expected:R}, got {actual:R}");
    }
}

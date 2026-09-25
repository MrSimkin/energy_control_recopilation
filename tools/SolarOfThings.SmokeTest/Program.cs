using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Security;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.SolarOfThings;

var root = Path.Combine(
    Path.GetTempPath(),
    "SolarEnergyMonitorSmoke",
    Guid.NewGuid().ToString("N"));

try
{
    var paths = new AppPaths(root);
    var database = new SqliteDatabase(paths);

    database.Initialize();

    if (!File.Exists(database.DatabasePath))
    {
        throw new InvalidOperationException("SQLite database file was not created.");
    }

    if (database.GetSchemaVersion() != SqliteDatabase.CurrentSchemaVersion ||
        SqliteDatabase.CurrentSchemaVersion != 8)
    {
        throw new InvalidOperationException("Unexpected SQLite schema version.");
    }

    var settings = new AppSettingsRepository(database);
    settings.Set("smoke.setting", "ok");
    if (settings.Get("smoke.setting") != "ok")
    {
        throw new InvalidOperationException("Application settings round-trip failed.");
    }
    settings.Delete("smoke.setting");

    var diagnostics = new DiagnosticsFileWriter(paths);
    diagnostics.Write("Information", "SmokeTest", "Diagnostics writer is operational.");
    if (Directory.GetFiles(paths.LogDirectory, "diagnostics-*.jsonl").Length != 1)
    {
        throw new InvalidOperationException("Diagnostics log file was not created.");
    }

    var signBody =
        "{\"account\":\"demo\",\"password\":\"5f4dcc3b5aa765d61d8327deb882cf99\"}";
    var signHash = IotOpenSigner.ComputeBodyHash(signBody);
    if (signHash != "a36cb269ad52226e354c9effbe1e9e73d2b2285c9ec5161513f5424a7e9bb71c")
    {
        throw new InvalidOperationException("IOT Open body-hash test vector failed.");
    }

    var sign = IotOpenSigner.ComputeSignature(
        "test-app",
        "00112233445566778899aabbccddeeff",
        signHash,
        "test-secret");

    if (sign != "43580b68bbd8ddbb7b10c7df4899dc89")
    {
        throw new InvalidOperationException("IOT Open signing test vector failed.");
    }

    var historyTime = SolarApiTime.FormatDateTime(
        DateTimeOffset.Parse("2026-09-25T19:53:37Z"),
        "America/Santiago");

    if (historyTime != "2026-09-25T16:53:37-03:00" ||
        historyTime.Contains('.', StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Unexpected Solar API datetime wire format: {historyTime}");
    }

    var historyDate = SolarApiTime.FormatDate(
        DateTimeOffset.Parse("2026-09-25T19:53:37Z"),
        "America/Santiago");

    if (historyDate != "2026-09-25")
    {
        throw new InvalidOperationException(
            $"Unexpected Solar API date wire format: {historyDate}");
    }

    var logoutBody = SolarOfThingsApiClient.SerializeCompact(new
    {
        accessToken = "ACCESS",
        userId = "491513787113766912"
    });

    if (logoutBody != "{\"accessToken\":\"ACCESS\",\"userId\":\"491513787113766912\"}")
    {
        throw new InvalidOperationException(
            "Logout contract did not preserve accessToken/userId names and string userId.");
    }

    var profileRepo = new CommissioningProfileRepository(database);
    var profile = new CommissioningProfile(
        "station-1",
        "Station",
        "America/Santiago",
        "device-1",
        "Device",
        "serial",
        "model",
        "manufacturer",
        "dtu",
        "protocol",
        "firmware",
        "pvInverter",
        "208",
        6200m,
        true,
        DateTimeOffset.Parse("2026-09-25T19:15:55Z"),
        "1",
        "SUPPORTED",
        12,
        "SUPPORTED",
        "SUPPORTED",
        "SUPPORTED",
        "SUPPORTED",
        "SUPPORTED",
        "{}",
        "[]",
        "{}",
        "{}",
        DateTimeOffset.UtcNow);

    profileRepo.Save(profile);
    var loadedProfile = profileRepo.Get();
    if (loadedProfile?.DeviceId != "device-1" ||
        loadedProfile.DataSource != "1" ||
        loadedProfile.GatherAttributeCount != 12 ||
        loadedProfile.DeviceSortKey != "pvInverter" ||
        loadedProfile.DeviceTypeNumber != "208" ||
        loadedProfile.RatedPower != 6200m ||
        loadedProfile.IsOnline != true ||
        loadedProfile.LastDataAt != DateTimeOffset.Parse("2026-09-25T19:15:55Z"))
    {
        throw new InvalidOperationException("Commissioning profile round-trip failed.");
    }

    var rangeSelection = new TimeRangeSelectionService();
    var calendarWeek = rangeSelection.ForCalendarWeek(
        new DateOnly(2026, 9, 25),
        "America/Santiago");

    if (calendarWeek.LocalStartDate != new DateOnly(2026, 9, 21) ||
        calendarWeek.LocalEndDate != new DateOnly(2026, 9, 27))
    {
        throw new InvalidOperationException(
            "Calendar-week time-range resolution failed.");
    }

    var completeMonths = rangeSelection.ForLastNCompleteCalendarMonths(
        new DateOnly(2026, 9, 25),
        2,
        "America/Santiago");

    if (completeMonths.LocalStartDate != new DateOnly(2026, 7, 1) ||
        completeMonths.LocalEndDate != new DateOnly(2026, 8, 31))
    {
        throw new InvalidOperationException(
            "Complete-calendar-month time-range resolution failed.");
    }

    var aggregationDeviceId = "aggregation-smoke-device";
    var aggregationStart =
        DateTimeOffset.Parse("2026-05-24T16:00:00Z");
    var aggregationEnd =
        DateTimeOffset.Parse("2026-05-24T16:59:59.9999999Z");

    var aggregationSamples = new[]
    {
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:00:00Z"), Pv: 1000.0, Soc: 50.0),
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:05:00Z"), Pv: 1000.0, Soc: 49.0),
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:10:00Z"), Pv: 1000.0, Soc: 48.0),
        // Intentional 30-minute hole: must not be integrated.
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:40:00Z"), Pv: 1000.0, Soc: 47.0),
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:45:00Z"), Pv: 1000.0, Soc: 46.0),
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:50:00Z"), Pv: 1000.0, Soc: 45.0),
        (Timestamp: DateTimeOffset.Parse("2026-05-24T16:59:59Z"), Pv: 1000.0, Soc: 44.0)
    };

    using (var connection = database.OpenConnection())
    using (var transaction = connection.BeginTransaction())
    {
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
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
                $metricKey,
                $recordedAtUtc,
                $value,
                $unit,
                $sourceKey,
                $sourceJson,
                'smoke.v1',
                'CONFIRMED',
                'OK',
                $updatedUtc
            );
            """;

        insert.Parameters.Add("$deviceId", SqliteType.Text);
        insert.Parameters.Add("$metricKey", SqliteType.Text);
        insert.Parameters.Add("$recordedAtUtc", SqliteType.Text);
        insert.Parameters.Add("$value", SqliteType.Real);
        insert.Parameters.Add("$unit", SqliteType.Text);
        insert.Parameters.Add("$sourceKey", SqliteType.Text);
        insert.Parameters.Add("$sourceJson", SqliteType.Text);
        insert.Parameters.Add("$updatedUtc", SqliteType.Text);

        foreach (var sample in aggregationSamples)
        {
            foreach (var metric in new[]
            {
                (Key: "pv_power_w", Value: sample.Pv, Unit: "W", Source: "smokePv"),
                (Key: "battery_soc_pct", Value: sample.Soc, Unit: "%", Source: "smokeSoc")
            })
            {
                insert.Parameters["$deviceId"].Value = aggregationDeviceId;
                insert.Parameters["$metricKey"].Value = metric.Key;
                insert.Parameters["$recordedAtUtc"].Value =
                    sample.Timestamp.ToUniversalTime().ToString("O");
                insert.Parameters["$value"].Value = metric.Value;
                insert.Parameters["$unit"].Value = metric.Unit;
                insert.Parameters["$sourceKey"].Value = metric.Source;
                insert.Parameters["$sourceJson"].Value =
                    metric.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                insert.Parameters["$updatedUtc"].Value =
                    DateTimeOffset.UtcNow.ToString("O");
                insert.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    var powerAggregation = new PowerAggregationService(database);
    var pvHourly = powerAggregation.GetSeries(
        aggregationDeviceId,
        "pv_power_w",
        aggregationStart,
        aggregationEnd,
        "America/Santiago",
        AggregationPeriod.Hour);

    if (pvHourly.Buckets.Count != 1)
    {
        throw new InvalidOperationException(
            $"Expected one hourly power bucket; got {pvHourly.Buckets.Count}.");
    }

    var pvBucket = pvHourly.Buckets[0];

    if (Math.Abs(pvBucket.PositiveEnergyKwh - 0.5) > 0.02 ||
        pvBucket.CoveragePercent is < 49 or > 51 ||
        pvHourly.ContinuityThresholdMinutes is < 14.9 or > 15.1)
    {
        throw new InvalidOperationException(
            $"Power aggregation/gap exclusion failed: " +
            $"energy={pvBucket.PositiveEnergyKwh:F4}, " +
            $"coverage={pvBucket.CoveragePercent:F2}, " +
            $"threshold={pvHourly.ContinuityThresholdMinutes:F2}.");
    }

    var socAggregation = new SocAggregationService(database);
    var socHourly = socAggregation.GetSeries(
        aggregationDeviceId,
        aggregationStart,
        aggregationEnd,
        "America/Santiago",
        AggregationPeriod.Hour);

    if (socHourly.Buckets.Count != 1)
    {
        throw new InvalidOperationException(
            $"Expected one hourly SOC bucket; got {socHourly.Buckets.Count}.");
    }

    var socBucket = socHourly.Buckets[0];

    if (socBucket.EndingPercent is null ||
        Math.Abs(socBucket.EndingPercent.Value - 44) > 0.01 ||
        socBucket.MinimumPercent is null ||
        Math.Abs(socBucket.MinimumPercent.Value - 44) > 0.01 ||
        socBucket.MaximumPercent is null ||
        Math.Abs(socBucket.MaximumPercent.Value - 50) > 0.01 ||
        socBucket.AveragePercent is null ||
        socBucket.AveragePercent.Value is < 46 or > 48 ||
        socBucket.CoveragePercent is < 49 or > 51)
    {
        throw new InvalidOperationException(
            "SOC aggregation/gap exclusion failed.");
    }

    var aggregationTable =
        new EnergyAggregationTableService(
            powerAggregation,
            socAggregation)
            .Get(
                aggregationDeviceId,
                aggregationStart,
                aggregationEnd,
                "America/Santiago",
                AggregationPeriod.Hour);

    if (aggregationTable.Rows.Count != 1 ||
        Math.Abs(aggregationTable.Rows[0].PvEnergyKwh - 0.5) > 0.02 ||
        aggregationTable.Rows[0].MinimumAvailableCoveragePercent is < 49 or > 51)
    {
        throw new InvalidOperationException(
            "Aligned aggregation table smoke test failed.");
    }

    var apiDiagnostics = new ApiDiagnosticsStore(paths);
    apiDiagnostics.Record(new ApiDiagnosticEntry(
        DateTimeOffset.UtcNow,
        "smoke",
        "Smoke",
        "Redaction",
        "POST",
        "https://example.invalid",
        1,
        200,
        "0",
        "ok",
        1,
        "SUCCESS",
        "{\"password\":\"secret\",\"deviceId\":\"123\"}",
        "{\"accessToken\":\"token-value\",\"data\":{\"deviceId\":\"123\",\"userName\":\"example-user\",\"userId\":\"999\",\"address\":\"example-address\",\"longitude\":1.2345,\"latitude\":2.3456,\"city\":\"example-city\",\"batteryCapacity\":30,\"monthlyBuyElectricityQuantity\":12.5,\"deviceModel\":\"HPVINV02\"}}",
        null,
        null)
    {
        RequestHeadersJson = "{\"IOT-Token\":\"header-token\",\"IOT-Time-Zone\":\"America/Santiago\"}",
        ResponseHeadersJson = "{\"X-Request-Id\":\"request-123\"}",
        ResponseLengthBytes = 42
    });

    var report = apiDiagnostics.BuildSanitizedReport();
    if (report.Contains("token-value", StringComparison.Ordinal) ||
        report.Contains("\"password\":\"secret\"", StringComparison.Ordinal) ||
        report.Contains("header-token", StringComparison.Ordinal) ||
        report.Contains("example-user", StringComparison.Ordinal) ||
        report.Contains("example-address", StringComparison.Ordinal) ||
        report.Contains("example-city", StringComparison.Ordinal) ||
        report.Contains("1.2345", StringComparison.Ordinal) ||
        report.Contains("2.3456", StringComparison.Ordinal) ||
        !report.Contains("deviceId", StringComparison.Ordinal) ||
        !report.Contains("\"batteryCapacity\":30", StringComparison.Ordinal) ||
        !report.Contains("\"monthlyBuyElectricityQuantity\":12.5", StringComparison.Ordinal) ||
        !report.Contains("HPVINV02", StringComparison.Ordinal) ||
        !report.Contains("request-123", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Diagnostic redaction smoke test failed.");
    }

    if (OperatingSystem.IsWindows())
    {
        ISecretStore secrets = new DpapiFileSecretStore(paths);

        var portalCredentialStore = new IotOpenCredentialStore(secrets);
        if (!portalCredentialStore.TryRead(out var portalCredential) ||
            portalCredential is null ||
            portalCredential.Source != "portal-default" ||
            !portalCredential.SecretIsEncrypted)
        {
            throw new InvalidOperationException("Default production client profile was not available.");
        }

        var decryptedPortalSecret = IotOpenSigner.DecryptEmbeddedSecret(
            portalCredential.AppId,
            portalCredential.SecretValue);

        if (string.IsNullOrWhiteSpace(decryptedPortalSecret) ||
            decryptedPortalSecret.Length < 16)
        {
            throw new InvalidOperationException("Default production client secret could not be decrypted.");
        }

        portalCredentialStore.Save(
            "override-app",
            "override-secret",
            secretIsEncrypted: false);

        if (!portalCredentialStore.TryRead(out var overrideCredential) ||
            overrideCredential?.Source != "local-override")
        {
            throw new InvalidOperationException("Local client-profile override did not take precedence.");
        }

        portalCredentialStore.Delete();

        if (!portalCredentialStore.TryRead(out var restoredDefault) ||
            restoredDefault?.Source != "portal-default")
        {
            throw new InvalidOperationException("Production client profile was not restored after deleting the override.");
        }

        secrets.Save("smoke.secret", "not-a-real-secret");

        if (!secrets.TryRead("smoke.secret", out var secretValue) ||
            secretValue != "not-a-real-secret")
        {
            throw new InvalidOperationException("DPAPI secret-store round-trip failed.");
        }

        secrets.Delete("smoke.secret");
        if (secrets.TryRead("smoke.secret", out _))
        {
            throw new InvalidOperationException("DPAPI secret-store delete failed.");
        }
    }

    Console.WriteLine(
        $"Smoke test passed. Schema v{SqliteDatabase.CurrentSchemaVersion}; " +
        "settings, diagnostics/redaction, production client profile, IOT Open signing/time formatting, " +
        "commissioning metadata/profile, protected secret storage, and Phase 5 time-range/aggregation math are operational.");
}
finally
{
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

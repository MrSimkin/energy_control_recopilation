using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Security;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Reporting;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Utility;

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
        SqliteDatabase.CurrentSchemaVersion != 14)
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

    var presetStore = new ReportPresetStore(settings);
    presetStore.Save(new ReportPreset(
        "Smoke preset",
        ReportKind.DetailedEnergy,
        "custom",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 1),
        AggregationPeriod.Hour,
        DateTimeOffset.UtcNow));

    if (presetStore.GetAll().Count != 1 ||
        presetStore.GetAll()[0].Name != "Smoke preset")
    {
        throw new InvalidOperationException(
            "Report preset persistence smoke test failed.");
    }

    var familySmokeDeviceId = "family-report-smoke-device";
    var familyLocalStart =
        new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.FromHours(-3));
    var familyLocalEnd =
        new DateTimeOffset(2026, 1, 13, 23, 55, 0, TimeSpan.FromHours(-3));

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
                'family-smoke.v1',
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

        for (var local = familyLocalStart;
             local <= familyLocalEnd;
             local = local.AddMinutes(5))
        {
            var hour = local.Hour;

            var houseWatts = hour is >= 19 and < 22
                ? 1500.0
                : 500.0;

            var pvWatts = hour is >= 11 and < 15
                ? 2500.0
                : hour is >= 7 and < 18
                    ? 600.0
                    : 0.0;

            var gridWatts = hour is >= 4 and < 7
                ? 600.0
                : 0.0;

            var socPercent = hour == 4 && local.Minute < 30
                ? 20.0
                : hour is >= 4 and < 7
                    ? 25.0
                    : 80.0;

            var batteryWatts = gridWatts > 0
                ? 0.0
                : houseWatts > pvWatts
                    ? houseWatts - pvWatts
                    : -Math.Min(800.0, pvWatts - houseWatts);

            foreach (var metric in new[]
            {
                (Key: "pv_power_w", Value: pvWatts, Unit: "W"),
                (Key: "house_load_power_w", Value: houseWatts, Unit: "W"),
                (Key: "grid_import_power_w", Value: gridWatts, Unit: "W"),
                (Key: "battery_power_w", Value: batteryWatts, Unit: "W"),
                (Key: "battery_soc_pct", Value: socPercent, Unit: "%")
            })
            {
                insert.Parameters["$deviceId"].Value = familySmokeDeviceId;
                insert.Parameters["$metricKey"].Value = metric.Key;
                insert.Parameters["$recordedAtUtc"].Value =
                    local.ToUniversalTime().ToString("O");
                insert.Parameters["$value"].Value = metric.Value;
                insert.Parameters["$unit"].Value = metric.Unit;
                insert.Parameters["$sourceKey"].Value = "familySmoke";
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

    var reportAggregation = new EnergyAggregationTableService(
        powerAggregation,
        socAggregation);
    var reportThresholds = new BatteryThresholdContextService(
        new InstallationHealthRepository(database),
        new InstallationContextPolicyService());
    var familyAnalysis = new FamilyReportAnalysisService(
        database,
        reportThresholds,
        reportAggregation);
    var sourceAttribution = new SourceAttributionService(
        database,
        new BatteryConfigurationService(settings));
    var reportStatistics =
        new EnergyRangeStatisticsService(database);
    var reportGridStatistical =
        new UtilityGridImportStatisticalCompletionService(
            database,
            reportStatistics);
    var reportExporter = new EnergyReportExportService(
        reportStatistics,
        reportAggregation,
        familyAnalysis,
        sourceAttribution,
        reportGridStatistical);

    var reportData = reportExporter.Build(new EnergyReportRequest(
        "Smoke family energy report",
        familySmokeDeviceId,
        new DateOnly(2026, 1, 10),
        new DateOnly(2026, 1, 13),
        familyLocalStart.ToUniversalTime(),
        familyLocalEnd.ToUniversalTime(),
        "America/Santiago",
        AggregationPeriod.Day,
        ReportKind.SimpleEnergy,
        "en"));

    if (reportData.Family.ReserveGridEpisodeCount != 3 ||
        reportData.Family.ObservableNightCount != 3 ||
        reportData.Family.NightsWithReserveGridUse != 3 ||
        reportData.Family.Events.Count != 3 ||
        reportData.Family.Events.Any(item => item.DurationMinutes < 150) ||
        reportData.Family.TypicalReserveTime != new TimeOnly(4, 0) ||
        reportData.Family.HighestHouseConsumptionWindow is null ||
        reportData.Family.HighestSolarGenerationWindow is null ||
        reportData.Family.HighestGridUseWindow is null ||
        !reportData.GridImportStatistical.HasPredictiveInterval ||
        reportData.GridImportStatistical.MedianKwh is null)
    {
        throw new InvalidOperationException(
            "Family event/pattern analysis smoke test failed.");
    }

    if (reportData.Attribution.Buckets.Count != 4 ||
        reportData.Attribution.SolarToHouseKwh <= 0 ||
        reportData.Attribution.BatteryToHouseKwh <= 0 ||
        reportData.Attribution.GridToHouseKwh <= 0 ||
        reportData.Attribution.AttributionCoverageOfObservedPercent < 95 ||
        reportData.Attribution.UnattributedHouseKwh > 0.2)
    {
        throw new InvalidOperationException(
            $"Source attribution smoke test failed: " +
            $"solar={reportData.Attribution.SolarToHouseKwh:F2}, " +
            $"battery={reportData.Attribution.BatteryToHouseKwh:F2}, " +
            $"grid={reportData.Attribution.GridToHouseKwh:F2}, " +
            $"coverage={reportData.Attribution.AttributionCoverageOfObservedPercent:F1}%, " +
            $"unattributed={reportData.Attribution.UnattributedHouseKwh:F2}.");
    }

    var xlsxPath = Path.Combine(root, "smoke-report.xlsx");
    reportExporter.ExportExcel(xlsxPath, reportData);

    if (!File.Exists(xlsxPath) ||
        new FileInfo(xlsxPath).Length < 1000)
    {
        throw new InvalidOperationException(
            "Excel report export smoke test failed.");
    }

    using (var exportedWorkbook = new XLWorkbook(xlsxPath))
    {
        var summarySheet = exportedWorkbook.Worksheet("Summary");
        var detailSheet = exportedWorkbook.Worksheet("Detail");
        var glossarySheet = exportedWorkbook.Worksheet("Glossary");
        _ = exportedWorkbook.Worksheet("Patterns");
        _ = exportedWorkbook.Worksheet("Events");
        var utilityUseSheet = exportedWorkbook.Worksheet("Utility Use");
        _ = exportedWorkbook.Worksheet("Evolution");
        _ = exportedWorkbook.Worksheet("Quality");

        if (summarySheet.Pictures.Count != 3 ||
            utilityUseSheet.LastRowUsed()?.RowNumber() is < 9 ||
            detailSheet.LastColumnUsed()?.ColumnNumber() is < 26 ||
            glossarySheet.LastRowUsed()?.RowNumber() is < 25 ||
            !string.Equals(
                summarySheet.Cell("A1").GetString(),
                reportData.Request.Title,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Family workbook product-structure smoke test failed.");
        }
    }

    if (OperatingSystem.IsWindows())
    {
        var pdfPath = Path.Combine(root, "smoke-report.pdf");
        reportExporter.ExportPdf(pdfPath, reportData);

        if (!File.Exists(pdfPath) ||
            new FileInfo(pdfPath).Length < 500)
        {
            throw new InvalidOperationException(
                "PDF report export smoke test failed.");
        }
    }

    var utilityRepository = new UtilityMeterRepository(database);
    var utilityStatistics = new EnergyRangeStatisticsService(database);
    var utilityReconciliation = new UtilityReconciliationService(
        utilityRepository,
        utilityStatistics);
    var utilityStatisticalCompletion =
        new UtilityGridImportStatisticalCompletionService(
            database,
            utilityStatistics);

    var utilityFromUtc = familyLocalStart.ToUniversalTime();
    var utilityToUtc = familyLocalEnd.ToUniversalTime();
    var expectedGridImport = utilityStatistics
        .GetMetric(
            familySmokeDeviceId,
            "grid_import_power_w",
            utilityFromUtc,
            utilityToUtc)
        .PositiveEnergyKwh;

    var completeStatistical =
        utilityStatisticalCompletion.Analyze(
            familySmokeDeviceId,
            utilityFromUtc,
            utilityToUtc,
            "America/Santiago");

    if (!completeStatistical.HasPredictiveInterval ||
        Math.Abs(
            completeStatistical.LowerKwh!.Value -
            expectedGridImport) > 0.01 ||
        Math.Abs(
            completeStatistical.MedianKwh!.Value -
            expectedGridImport) > 0.01 ||
        Math.Abs(
            completeStatistical.UpperKwh!.Value -
            expectedGridImport) > 0.01)
    {
        throw new InvalidOperationException(
            "Statistical completion complete-coverage smoke test failed.");
    }

    var firstReadingId = utilityRepository.AddReading(
        utilityFromUtc,
        12500.0,
        "smoke-personal-start",
        "Phase 8 exact personal reading",
        UtilityReadingSourceKind.Personal,
        UtilityTimePrecision.Exact,
        UtilityTimeAssumption.Exact);
    var secondReadingId = utilityRepository.AddReading(
        utilityToUtc,
        12500.0 + expectedGridImport,
        "smoke-personal-end",
        "Phase 8 exact personal reading",
        UtilityReadingSourceKind.Personal,
        UtilityTimePrecision.Exact,
        UtilityTimeAssumption.Exact);

    var utilityRows =
        utilityReconciliation.GetMeterReconciliations(
            familySmokeDeviceId);

    if (utilityRows.Count != 1 ||
        utilityRows[0].FromReadingId != firstReadingId ||
        utilityRows[0].ToReadingId != secondReadingId ||
        utilityRows[0].TimeBasis != "EXACT" ||
        !utilityRows[0].MeterConsumptionKwh.HasValue ||
        Math.Abs(
            utilityRows[0].MeterConsumptionKwh.Value -
            expectedGridImport) > 0.000001 ||
        Math.Abs(
            utilityRows[0].InverterGridImportKwh -
            expectedGridImport) > 0.000001 ||
        utilityRows[0].AbsoluteDifferenceKwh is null ||
        utilityRows[0].AbsoluteDifferenceKwh.Value > 0.000001 ||
        utilityRows[0].CoveragePercent < 95)
    {
        throw new InvalidOperationException(
            "Phase 8 utility meter reconciliation smoke test failed.");
    }

    var arbitrary = utilityReconciliation.ReconcileReadings(
        familySmokeDeviceId,
        firstReadingId,
        secondReadingId);
    if (arbitrary.FromReadingId != firstReadingId ||
        arbitrary.ToReadingId != secondReadingId ||
        arbitrary.TimeBasis != "EXACT" ||
        arbitrary.AbsoluteDifferenceKwh is null ||
        arbitrary.AbsoluteDifferenceKwh.Value > 0.000001 ||
        arbitrary.Sensitivity is null ||
        arbitrary.Sensitivity.IsFormalConfidenceInterval ||
        arbitrary.Sensitivity.LowerKwh >
            arbitrary.InverterGridImportKwh + 0.000001 ||
        (arbitrary.Sensitivity.UpperKwh.HasValue &&
         arbitrary.Sensitivity.UpperKwh.Value + 0.000001 <
            arbitrary.InverterGridImportKwh))
    {
        throw new InvalidOperationException(
            "Phase 8 arbitrary-reading reconciliation smoke test failed.");
    }

    var billId = utilityRepository.AddBill(
        utilityFromUtc,
        utilityToUtc,
        expectedGridImport,
        12345,
        "SMOKE-BILL",
        "Phase 8 linked optional bill",
        fromReadingId: firstReadingId,
        toReadingId: secondReadingId,
        meterStartKwh: 12500.0,
        meterEndKwh: 12500.0 + expectedGridImport,
        tariffPlan: "BT1-SMOKE",
        taxableAmountClp: 10000,
        ivaClp: 1900,
        exemptAmountClp: 445,
        grossBillAmountClp: 12345,
        otherChargesClp: -345,
        totalDueClp: 12000,
        periodPrecision: UtilityTimePrecision.Exact);

    var chargeLineId = utilityRepository.AddBillLine(
        billId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        10000,
        quantity: expectedGridImport,
        unit: "kWh",
        unitRateClp: expectedGridImport > 0
            ? 10000 / expectedGridImport
            : null,
        taxTreatment: "AFECTO",
        sortOrder: 10);
    var creditLineId = utilityRepository.AddBillLine(
        billId,
        "OTROS_CARGOS",
        "Subsidio smoke",
        -345,
        taxTreatment: "CREDITO",
        sortOrder: 20);

    var billRows =
        utilityReconciliation.GetBillReconciliations(
            familySmokeDeviceId);
    var billRecord = utilityRepository.GetBills().Single();
    var billLines = utilityRepository.GetBillLines(billId);

    if (billRows.Count != 1 ||
        billRows[0].BillId != billId ||
        billRows[0].TimeBasis != "EXACT" ||
        billRows[0].AbsoluteDifferenceKwh is null ||
        billRows[0].AbsoluteDifferenceKwh.Value > 0.000001 ||
        billRows[0].CoveragePercent < 95 ||
        billRecord.FromReadingId != firstReadingId ||
        billRecord.ToReadingId != secondReadingId ||
        billRecord.TotalDueClp != 12000 ||
        billRecord.OtherChargesClp != -345 ||
        billLines.Count != 2 ||
        !billLines.Any(line => line.BillLineId == chargeLineId && line.AmountClp == 10000) ||
        !billLines.Any(line => line.BillLineId == creditLineId && line.AmountClp == -345))
    {
        throw new InvalidOperationException(
            "Phase 8 linked bill/line persistence smoke test failed.");
    }

    var officialReadingId = utilityRepository.AddReading(
        utilityToUtc.AddDays(1),
        13000,
        "Lectura Enel smoke",
        "Official date-only evidence",
        UtilityReadingSourceKind.UtilityOfficial,
        UtilityTimePrecision.DateOnly,
        UtilityTimeAssumption.StartOfDayAssumed);
    var officialReading = utilityRepository.GetReading(officialReadingId);
    if (officialReading is null ||
        officialReading.SourceKind != UtilityReadingSourceKind.UtilityOfficial ||
        officialReading.TimePrecision != UtilityTimePrecision.DateOnly ||
        officialReading.TimeAssumption != UtilityTimeAssumption.StartOfDayAssumed)
    {
        throw new InvalidOperationException(
            "Phase 8 official/date-only reading metadata smoke test failed.");
    }

    var officialStartId = utilityRepository.AddReading(
        utilityFromUtc,
        14000,
        "Enel boundary start smoke",
        "Date-only start boundary",
        UtilityReadingSourceKind.UtilityOfficial,
        UtilityTimePrecision.DateOnly,
        UtilityTimeAssumption.StartOfDayAssumed);
    var officialEndId = utilityRepository.AddReading(
        utilityToUtc,
        14000 + expectedGridImport,
        "Enel boundary end smoke",
        "Date-only end boundary",
        UtilityReadingSourceKind.UtilityOfficial,
        UtilityTimePrecision.DateOnly,
        UtilityTimeAssumption.StartOfDayAssumed);
    var officialBoundaryComparison =
        utilityReconciliation.ReconcileReadings(
            familySmokeDeviceId,
            officialStartId,
            officialEndId);

    if (officialBoundaryComparison.TimeBasis != "DATE_ONLY_ASSUMED" ||
        officialBoundaryComparison.Sensitivity is null ||
        officialBoundaryComparison.Sensitivity.IsFormalConfidenceInterval ||
        !officialBoundaryComparison.Sensitivity.Basis.Contains(
            "ENEL",
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Phase 8 Enel boundary sensitivity smoke test failed.");
    }

    if (OperatingSystem.IsWindows())
    {
        var reconciliationReport =
            new UtilityReconciliationReportService(
                utilityRepository,
                utilityReconciliation);
        var utilityPdfPath =
            Path.Combine(root, "smoke-utility-reconciliation.pdf");
        reconciliationReport.ExportPdf(
            utilityPdfPath,
            familySmokeDeviceId,
            firstReadingId,
            secondReadingId,
            "America/Santiago",
            "es");

        if (!File.Exists(utilityPdfPath) ||
            new FileInfo(utilityPdfPath).Length < 500)
        {
            throw new InvalidOperationException(
                "Phase 8 reconciliation PDF smoke test failed.");
        }

        var smokeTariffRepository =
            new TariffPublicationRepository(database);
        var smokeCandidateRepository =
            new TariffRateCandidateRepository(database);
        var smokeVersionResolver =
            new TariffPublicationVersionResolver();
        var billAuditReport =
            new UtilityBillAuditReportService(
                utilityRepository,
                new TariffBillRateVerificationService(
                    utilityRepository,
                    smokeTariffRepository,
                    smokeCandidateRepository,
                    smokeVersionResolver),
                new UtilityBillGapStatisticalCompletionService(
                    database),
                new UtilityBillTariffScenarioAnalysisService(
                    utilityRepository,
                    smokeTariffRepository,
                    smokeCandidateRepository,
                    smokeVersionResolver));
        var billAuditPdfPath =
            Path.Combine(root, "smoke-enel-bill-audit.pdf");
        billAuditReport.ExportPdf(
            billAuditPdfPath,
            familySmokeDeviceId,
            billId,
            "America/Santiago",
            "es");

        if (!File.Exists(billAuditPdfPath) ||
            new FileInfo(billAuditPdfPath).Length < 500)
        {
            throw new InvalidOperationException(
                "Bill-first Enel audit PDF smoke test failed.");
        }

        var billAuditAnnexPath =
            Path.Combine(root, "smoke-enel-bill-annex.zip");
        var billAuditAnnex =
            new UtilityBillAuditAnnexExportService(
                database,
                utilityRepository,
                new UtilityBillGapStatisticalCompletionService(
                    database),
                new UtilityBillTariffScenarioAnalysisService(
                    utilityRepository,
                    smokeTariffRepository,
                    smokeCandidateRepository,
                    smokeVersionResolver));
        billAuditAnnex.ExportZip(
            billAuditAnnexPath,
            familySmokeDeviceId,
            billId,
            "America/Santiago",
            "es");

        if (!File.Exists(billAuditAnnexPath) ||
            new FileInfo(billAuditAnnexPath).Length < 500)
        {
            throw new InvalidOperationException(
                "Bill audit technical annex ZIP smoke test failed.");
        }

        using (var annex =
               System.IO.Compression.ZipFile.OpenRead(
                   billAuditAnnexPath))
        {
            var requiredAnnexEntries = new[]
            {
                "00-Anexo-Tecnico-Evidencia-Numerica.pdf",
                "01-telemetria-importacion-red.csv",
                "02-intervalos-sin-telemetria.csv",
                "03-escenarios-economicos.csv",
                "04-fuentes-y-metodo.txt",
                "05-cobertura-diaria.csv",
                "99-manifest-integridad.json"
            };

            foreach (var name in requiredAnnexEntries)
            {
                if (annex.GetEntry(name) is null)
                {
                    throw new InvalidOperationException(
                        $"Bill audit annex entry missing: {name}");
                }
            }
        }
    }

    if (string.Equals(
            Environment.GetEnvironmentVariable(
                "SOLAR_ENEL_LIVE_CATALOG"),
            "1",
            StringComparison.Ordinal))
    {
        var liveCatalog =
            await EnelTariffCaptureService
                .ProbeOfficialCatalogAsync(2026);

        Console.WriteLine(
            $"Live Enel catalog probe: HTTP {liveCatalog.HttpStatusCode}; " +
            $"{liveCatalog.HtmlLength:N0} chars; " +
            $"{liveCatalog.PdfHrefCount} PDF href(s); " +
            $"{liveCatalog.DiscoveredPublications} supply publication(s) for {liveCatalog.Year}.");

        if (liveCatalog.DiscoveredPublications < 1)
        {
            Console.WriteLine("----- BEGIN ENEL CATALOG RAW HTML -----");
            Console.WriteLine(liveCatalog.RawHtml);
            Console.WriteLine("----- END ENEL CATALOG RAW HTML -----");

            throw new InvalidOperationException(
                "Live Enel catalog probe returned no official supply publications; raw HTML dumped above.");
        }

        const string livePdfProbeUrl =
            "https://www.enel.cl/content/dam/enel-cl/es/personas/informacion-de-utilidad/" +
            "tarifas-y-reglamentos/tarifas/tarifas-reguladas/2026/" +
            "Enel%20Distribuci%C3%B3n%20Chile%20SA._Tarifas%20Suministro%20El%C3%A9ctrico%208T_" +
            "%20VAD%205T%20Septiembre%20de%202026.pdf";

        var livePdf =
            await EnelTariffCaptureService
                .ProbeOfficialPdfAsync(
                    livePdfProbeUrl);

        Console.WriteLine(
            $"Live Enel direct PDF probe: " +
            $"{livePdf.ContentLength:N0} bytes; " +
            $"SHA-256 {livePdf.Sha256}.");

        if (liveCatalog.HttpStatusCode != 200 ||
            liveCatalog.DiscoveredPublications < 10)
        {
            Console.WriteLine("----- BEGIN ENEL CATALOG RAW HTML -----");
            Console.WriteLine(liveCatalog.RawHtml);
            Console.WriteLine("----- END ENEL CATALOG RAW HTML -----");

            throw new InvalidOperationException(
                "Live Enel catalog probe did not discover a plausible 2026 official supply-publication set.");
        }

        if (livePdf.ContentLength < 100_000)
        {
            throw new InvalidOperationException(
                "Live Enel direct PDF probe returned an implausibly small document.");
        }

        var liveEnelRepository =
            new TariffPublicationRepository(database);
        var liveEnelCandidateRepository =
            new TariffRateCandidateRepository(database);
        var liveEnelParser =
            new EnelBt1TariffTextParser();
        var liveEnelNormalizer =
            new EnelTariffNormalizationService(
                liveEnelCandidateRepository,
                liveEnelParser);
        var liveEnelCapture =
            new EnelTariffCaptureService(
                liveEnelRepository,
                liveEnelNormalizer,
                paths);

        var liveEnelProgress = new Progress<string>(
            message =>
                Console.WriteLine(
                    $"Live Enel progress: {message}"));

        var liveEnelResult =
            await liveEnelCapture.CaptureSupplyTariffsAsync(
                2026,
                liveEnelProgress);

        Console.WriteLine(
            $"Live Enel full capture: " +
            $"{liveEnelResult.Captured}/{liveEnelResult.Discovered} captured; " +
            $"{liveEnelResult.Failed} download/capture failure(s); " +
            $"{liveEnelResult.NormalizedCandidates} normalized candidate(s); " +
            $"{liveEnelResult.NormalizationFailures} normalization failure(s); " +
            $"{liveEnelResult.RetroactiveDetected} retroactive publication(s); " +
            $"{liveEnelResult.MultiVersionPeriods} multi-version period(s).");

        if (liveEnelResult.Discovered < 10 ||
            liveEnelResult.Captured < 10 ||
            liveEnelResult.Failed != 0 ||
            liveEnelResult.NormalizedCandidates < 1)
        {
            foreach (var message in liveEnelResult.Messages)
                Console.WriteLine($"Live Enel capture detail: {message}");

            throw new InvalidOperationException(
                "Live Enel full-year capture did not complete with a plausible official 2026 evidence set.");
        }
    }

    if (string.Equals(
            Environment.GetEnvironmentVariable(
                "SOLAR_CNE_LIVE_CATALOG"),
            "1",
            StringComparison.Ordinal))
    {
        var cneProbe =
            await CneTariffEvidenceCaptureService
                .ProbeOfficialSourceAsync(2026);

        Console.WriteLine(
            $"Live CNE tariff-evidence probe: HTTP {cneProbe.HttpStatusCode}; " +
            $"{cneProbe.HtmlLength:N0} chars; " +
            $"{cneProbe.CandidatePdfLinks} candidate PDF link(s).");

        if (cneProbe.HttpStatusCode != 200 ||
            cneProbe.CandidatePdfLinks < 1)
        {
            throw new InvalidOperationException(
                "Live CNE tariff-evidence source did not expose any candidate PDFs.");
        }

        var liveCneRepository =
            new TariffPublicationRepository(database);
        var liveCneCapture =
            new CneTariffEvidenceCaptureService(
                liveCneRepository,
                paths);

        var liveCneProgress = new Progress<string>(
            message =>
                Console.WriteLine(
                    $"Live CNE progress: {message}"));

        var liveCneResult =
            await liveCneCapture.CaptureVadIndexEvidenceAsync(
                2026,
                liveCneProgress);

        Console.WriteLine(
            $"Live CNE capture: {liveCneResult.CapturedVadIndexDocuments} " +
            $"VAD document(s); {liveCneResult.Corrections} correction(s); " +
            $"{liveCneResult.Failed} failure(s).");

        foreach (var item in liveCneRepository
                     .GetAll()
                     .Where(item =>
                         item.Provider == "CNE_CHILE" &&
                         item.Category == "VAD_INDEX")
                     .OrderBy(item => item.EffectiveFrom)
                     .ThenBy(item => item.PublicationId))
        {
            Console.WriteLine(
                $"Live CNE captured source: " +
                $"{item.EffectiveFrom?.ToString("yyyy-MM-dd") ?? "no-date"}; " +
                $"correction={item.IsRetroactive}; {item.Title}; {item.SourceUrl}");
        }

        if (liveCneResult.CapturedVadIndexDocuments < 10 ||
            liveCneResult.Corrections < 1 ||
            !liveCneRepository.GetAll().Any(item =>
                item.Provider == "CNE_CHILE" &&
                item.Category == "VAD_INDEX" &&
                item.IsRetroactive &&
                item.EffectiveFrom == new DateOnly(2026, 8, 1) &&
                item.CaptureStatus == "CAPTURED"))
        {
            throw new InvalidOperationException(
                "Live CNE tariff-evidence capture did not preserve the expected 2026 VAD correction chain.");
        }
    }

    var tariffRepository =
        new TariffPublicationRepository(database);
    const string tariffFixtureHtml = """
        <html><body>
        <link rel="preload"
              href="/content/tarifas/Enel%20Distribuci%C3%B3n%20Chile%20SA._Tarifas%20Suministro%20El%C3%A9ctrico%208T_%20VAD%205T%20Septiembre%20de%202026.pdf">
        <a href="/content/tarifas/Enel%20Distribuci%C3%B3n%20Chile%20SA._Tarifas%20Suministro%20El%C3%A9ctrico%208T_%20VAD%205T%20Agosto%20de%202026_Retroactivo.pdf">
        Descargar
        </a>
        <a href="/content/tarifas/Enel%20Distribuci%C3%B3n%20Chile%20SA._Tarifas%20Suministro%20El%C3%A9ctrico%2024T_%20VAD%205T%20Agosto%20de%202026.pdf">
        Descargar
        </a>
        <a href="/content/tarifas/Enel%20Distribuci%C3%B3n%20Chile%20SA._Tarifas%20Suministro%20El%C3%A9ctrico%2014T_%20VAD%205T%20Diciembre%20de%202025.pdf">
        Descargar
        </a>
        </body></html>
        """;

    var tariffDiscovered =
        EnelTariffCaptureService.DiscoverSupplyTariffs(
            tariffFixtureHtml,
            new Uri("https://www.enel.cl/es/clientes/tarifas-y-regulacion/tarifas.html"),
            2026);

    if (tariffDiscovered.Count != 3 ||
        !tariffDiscovered.Any(item =>
            item.EffectiveFrom == new DateOnly(2026, 9, 1) &&
            !item.IsRetroactive) ||
        !tariffDiscovered.Any(item =>
            item.EffectiveFrom == new DateOnly(2026, 8, 1) &&
            item.IsRetroactive) ||
        tariffDiscovered.Count(item =>
            item.EffectiveFrom == new DateOnly(2026, 8, 1)) != 2)
    {
        throw new InvalidOperationException(
            "Phase 9 tariff catalog discovery smoke test failed.");
    }

    var tariffPublicationIds = tariffDiscovered
        .Select(tariffRepository.UpsertDiscovery)
        .ToArray();

    var versionResolver =
        new TariffPublicationVersionResolver();
    var versionResolutions =
        versionResolver.Resolve(
            tariffRepository.GetAll());

    var augustPublicationIds = tariffDiscovered
        .Select((item, index) => new
        {
            Item = item,
            PublicationId = tariffPublicationIds[index]
        })
        .Where(item =>
            item.Item.EffectiveFrom == new DateOnly(2026, 8, 1))
        .ToArray();

    var augustRetroactive = augustPublicationIds
        .Single(item => item.Item.IsRetroactive);
    var augustOriginal = augustPublicationIds
        .Single(item => !item.Item.IsRetroactive);

    if (!versionResolutions.Any(item =>
            item.PublicationId == augustRetroactive.PublicationId &&
            item.Status == "VERSION_PREFERRED_RETROACTIVE") ||
        !versionResolutions.Any(item =>
            item.PublicationId == augustOriginal.PublicationId &&
            item.Status == "VERSION_SUPERSEDED_BY_RETROACTIVE") ||
        !versionResolutions.Any(item =>
            item.EffectiveFrom == new DateOnly(2026, 9, 1) &&
            item.Status == "VERSION_SINGLE"))
    {
        throw new InvalidOperationException(
            "Phase 9 retroactive tariff version resolution smoke test failed.");
    }

    var cneOriginalId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "CNE_CHILE",
                "VAD_INDEX",
                "Resolución Exenta CNE N° 368 · índices VAD 2026-08",
                "https://www.cne.cl/smoke/rex-368-2026.pdf",
                new DateOnly(2026, 8, 1),
                false,
                "REX-368-2026",
                new DateOnly(2026, 7, 17),
                null,
                "SMOKE"));

    var cneCorrectionId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "CNE_CHILE",
                "VAD_INDEX",
                "Resolución Exenta CNE N° 380 · índices VAD 2026-08 · rectificación",
                "https://www.cne.cl/smoke/rex-380-2026.pdf",
                new DateOnly(2026, 8, 1),
                true,
                "REX-380-2026",
                new DateOnly(2026, 7, 24),
                "REX-368-2026",
                "SMOKE"));

    tariffRepository.UpsertRelation(
        new TariffPublicationRelationUpsert(
            cneCorrectionId,
            "CORRECTS",
            "CNE_CHILE",
            "VAD_INDEX",
            "REX-368-2026",
            "https://www.cne.cl/smoke/rex-380-2026.pdf",
            "REX-380-2026 CORRECTS REX-368-2026"));
    tariffRepository.ResolveRelationTargets();

    var cnePublications = tariffRepository
        .GetAll()
        .Where(item =>
            item.Provider == "CNE_CHILE" &&
            item.Category == "VAD_INDEX" &&
            item.EffectiveFrom ==
                new DateOnly(2026, 8, 1) &&
            item.OfficialDocumentNumber is
                "REX-368-2026" or "REX-380-2026")
        .ToArray();

    var cneVersionResolutions =
        versionResolver.Resolve(
            cnePublications,
            tariffRepository.GetRelations());

    if (!cneVersionResolutions.Any(item =>
            item.PublicationId == cneCorrectionId &&
            item.Status ==
                "VERSION_PREFERRED_OFFICIAL_CORRECTION") ||
        !cneVersionResolutions.Any(item =>
            item.PublicationId == cneOriginalId &&
            item.Status ==
                "VERSION_SUPERSEDED_BY_OFFICIAL_CORRECTION"))
    {
        throw new InvalidOperationException(
            "Official tariff correction-graph resolution smoke test failed.");
    }

    var tariffPublicationId = tariffPublicationIds[0];

    var tariffPageFixture = """
        Cargo en Boleta/Factura RED ETR UNIDAD $ Neto $ IVA
        Cargo fijo mensual ($/mes) 596,252 709,540 596,252 709,540
        Cargo por servicio público (No incorpora recargo FET según tramo de consumo)
        ($/kWh) 0,855 0,000 0,855 0,000
        Transporte de electricidad (2) ($/kWh) 13,415 15,964 13,415 15,964
        Cargo por energía (3)
        T1 -T6 Cargo por energía ($/kWh) 131,039 155,936 131,039 155,936
        Cargo por compras de potencia ($/kWh) 26,029 30,974 26,029 30,974
        BT_AA T1 19,212 22,862 19,212 22,862
        BT_SA T1 23,116 27,508 23,116 27,508
        BT_AS T1 23,301 27,728 23,301 27,728
        BT_SS T1 27,205 32,374 27,205 32,374
        BT_AA T1 176,2788 209,772 176,2788 209,772
        TOTAL TARIFA BASE BT1
        Electricidad consumida (6) se obtiene sumando (3), (4) y (5)
        Cargo por potencia base (5) en su componente de distribución
        """;

    tariffRepository.MarkCaptured(
        tariffPublicationId,
        Path.Combine(paths.TariffDirectory, "smoke.pdf"),
        "abc123",
        1234,
        2,
        new[]
        {
            tariffPageFixture,
            "Página 2 sin evidencia BT1 relevante"
        });

    var candidateRepository =
        new TariffRateCandidateRepository(database);
    var tariffParser =
        new EnelBt1TariffTextParser();
    var tariffNormalizer =
        new EnelTariffNormalizationService(
            candidateRepository,
            tariffParser);
    var normalizationResult =
        tariffNormalizer.NormalizePublication(
            tariffPublicationId,
            tariffRepository.GetPageTexts(tariffPublicationId));

    var storedTariff =
        tariffRepository.GetAll()
            .Single(item =>
                item.PublicationId == tariffPublicationId);
    var storedTariffPages =
        tariffRepository.GetPageTexts(
            tariffPublicationId);
    var storedCandidates =
        candidateRepository.GetForPublication(
            tariffPublicationId);

    var fixedCandidate = storedCandidates.FirstOrDefault(item =>
        item.ComponentKey == "FIXED_MONTHLY" &&
        item.CandidateIndex == 0);
    var publicServiceCandidate = storedCandidates.FirstOrDefault(item =>
        item.ComponentKey == "PUBLIC_SERVICE" &&
        item.CandidateIndex == 0);
    var powerBaseCandidate = storedCandidates.FirstOrDefault(item =>
        item.ComponentKey == "POWER_BASE_DISTRIBUTION" &&
        item.NetworkType == "BT_AA" &&
        item.EtrBand == "T1" &&
        item.CandidateIndex == 0);
    var electricityConsumedCandidate = storedCandidates.FirstOrDefault(item =>
        item.ComponentKey == "ELECTRICITY_CONSUMED" &&
        item.NetworkType == "BT_AA" &&
        item.EtrBand == "T1" &&
        item.CandidateIndex == 0);

    if (storedTariff.CaptureStatus != "CAPTURED" ||
        storedTariff.PageCount != 2 ||
        storedTariff.ContentSha256 != "abc123" ||
        storedTariffPages.Count != 2 ||
        storedTariff.NormalizationStatus != "CANDIDATES_EXTRACTED" ||
        storedTariff.NormalizationParserVersion !=
            EnelBt1TariffTextParser.ParserVersion ||
        normalizationResult.CandidateCount != storedCandidates.Count ||
        fixedCandidate is null ||
        Math.Abs((fixedCandidate.NetRateClp ?? -1) - 596.252) > 0.0001 ||
        Math.Abs((fixedCandidate.PublishedIvaColumnClp ?? -1) - 709.540) > 0.0001 ||
        publicServiceCandidate is null ||
        Math.Abs((publicServiceCandidate.NetRateClp ?? -1) - 0.855) > 0.0001 ||
        Math.Abs(publicServiceCandidate.PublishedIvaColumnClp ?? -1) > 0.0001 ||
        powerBaseCandidate is null ||
        Math.Abs((powerBaseCandidate.NetRateClp ?? -1) - 19.212) > 0.0001 ||
        powerBaseCandidate.ValidationState != "CLASSIFIED_BY_SUM_RULE_UNAPPLIED" ||
        electricityConsumedCandidate is null ||
        Math.Abs((electricityConsumedCandidate.NetRateClp ?? -1) - 176.2788) > 0.0001 ||
        electricityConsumedCandidate.ValidationState != "CLASSIFIED_BY_SUM_RULE_UNAPPLIED" ||
        storedCandidates.Any(item =>
            item.ValidationState is not
                ("EXTRACTED_UNAPPLIED" or
                 "CLASSIFIED_BY_SUM_RULE_UNAPPLIED")))
    {
        throw new InvalidOperationException(
            "Phase 9 tariff candidate normalization smoke test failed.");
    }


    var januaryPublicationId = tariffRepository.UpsertDiscovery(
        new TariffPublicationDiscovery(
            "ENEL_DISTRIBUCION_CHILE",
            "SUPPLY_REGULATED",
            "Smoke Tarifas Suministro Eléctrico Enero de 2026.pdf",
            "https://example.invalid/enel-january-2026.pdf",
            new DateOnly(2026, 1, 1),
            false));

    tariffRepository.MarkCaptured(
        januaryPublicationId,
        Path.Combine(paths.TariffDirectory, "smoke-january.pdf"),
        "jan123",
        1234,
        1,
        new[] { tariffPageFixture });

    tariffNormalizer.NormalizePublication(
        januaryPublicationId,
        tariffRepository.GetPageTexts(januaryPublicationId));

    var fixedAuditLineId = utilityRepository.AddBillLine(
        billId,
        "SERVICIO_ELECTRICO",
        "Cargo fijo mensual",
        596.252,
        categoryKey: "FIXED_MONTHLY",
        quantity: 1,
        unit: "mes",
        unitRateClp: 596.252,
        taxTreatment: "AFECTO",
        sortOrder: 30);


    var electricityAuditLineId = utilityRepository.AddBillLine(
        billId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        1762.788,
        categoryKey: "ELECTRICITY_CONSUMED",
        quantity: 10,
        unit: "kWh",
        unitRateClp: 176.2788,
        taxTreatment: "AFECTO",
        sortOrder: 31);

    var actualOnlyAuditLineId = utilityRepository.AddBillLine(
        billId,
        "OTROS_CARGOS",
        "Cargo común no tarifario",
        250,
        taxTreatment: "EXENTO",
        sortOrder: 40);

    var noPrintedRateAuditLineId = utilityRepository.AddBillLine(
        billId,
        "SERVICIO_ELECTRICO",
        "Transporte de electricidad",
        134.15,
        categoryKey: "ELECTRICITY_TRANSPORT",
        taxTreatment: "AFECTO",
        sortOrder: 41);

    var rateVerification =
        new TariffBillRateVerificationService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver);

    var verifiedLines = rateVerification.VerifyBill(
        billId,
        "America/Santiago");

    var verifiedFixed = verifiedLines.Single(item =>
        item.BillLineId == fixedAuditLineId);
    var verifiedElectricity = verifiedLines.Single(item =>
        item.BillLineId == electricityAuditLineId);
    var verifiedActualOnly = verifiedLines.Single(item =>
        item.BillLineId == actualOnlyAuditLineId);
    var pendingOfficialDerivation = verifiedLines.Single(item =>
        item.BillLineId == noPrintedRateAuditLineId);

    if (!verifiedFixed.Status.StartsWith(
            "VERIFIED_RECONSTRUCTED_",
            StringComparison.Ordinal) ||
        verifiedFixed.ReconstructedAmountClp is null ||
        Math.Abs(
            verifiedFixed.ReconstructedAmountClp.Value -
            596.252) > 0.0001 ||
        verifiedFixed.PublicationIds.Count != 1 ||
        verifiedFixed.PublicationIds[0] != januaryPublicationId ||
        !verifiedElectricity.Status.StartsWith(
            "VERIFIED_RECONSTRUCTED_",
            StringComparison.Ordinal) ||
        verifiedElectricity.ReconstructedAmountClp is null ||
        Math.Abs(
            verifiedElectricity.ReconstructedAmountClp.Value -
            1762.788) > 0.001 ||
        verifiedActualOnly.Status != "ACTUAL_ONLY_UNMAPPED" ||
        pendingOfficialDerivation.Status != "OFFICIAL_RATE_DERIVATION_PENDING" ||
        pendingOfficialDerivation.PrintedUnitRateClp is not null ||
        pendingOfficialDerivation.CalculationQuantity is null ||
        Math.Abs(
            pendingOfficialDerivation.CalculationQuantity.Value -
            expectedGridImport) > 0.000001 ||
        pendingOfficialDerivation.CalculationUnit != "kWh" ||
        pendingOfficialDerivation.CalculationQuantitySource != "BILL_BILLED_KWH" ||
        pendingOfficialDerivation.PublicationIds.Count != 1 ||
        pendingOfficialDerivation.PublicationIds[0] != januaryPublicationId)
    {
        throw new InvalidOperationException(
            "Phase 9 bill-line official-rate verification smoke test failed.");
    }


    var cneFebruaryId = tariffRepository.UpsertDiscovery(
        new TariffPublicationDiscovery(
            "CNE_CHILE",
            "VAD_INDEX",
            "Resolución Exenta CNE smoke · índices VAD 2026-02",
            "https://example.invalid/cne-vad-2026-02.pdf",
            new DateOnly(2026, 2, 1),
            false));

    tariffRepository.MarkCaptured(
        cneFebruaryId,
        Path.Combine(paths.TariffDirectory, "cne-vad-smoke.pdf"),
        "cne123",
        500,
        1,
        new[] { "CNE VAD boundary smoke evidence" });

    var boundaryBillId = utilityRepository.AddBill(
        new DateTimeOffset(
            2026, 1, 31, 0, 0, 0,
            TimeSpan.FromHours(-3)).ToUniversalTime(),
        new DateTimeOffset(
            2026, 2, 2, 0, 0, 0,
            TimeSpan.FromHours(-3)).ToUniversalTime(),
        10,
        1000,
        "CNE-BOUNDARY-SMOKE",
        "Must require the next Enel tariff table",
        tariffPlan: "BT1-SMOKE",
        periodPrecision: UtilityTimePrecision.DateOnly);

    var boundaryLineId = utilityRepository.AddBillLine(
        boundaryBillId,
        "SERVICIO_ELECTRICO",
        "Cargo fijo mensual",
        596.252,
        categoryKey: "FIXED_MONTHLY",
        quantity: 1,
        unit: "mes",
        unitRateClp: 596.252,
        taxTreatment: "AFECTO",
        sortOrder: 10);

    var boundaryVerification =
        rateVerification.VerifyBill(
            boundaryBillId,
            "America/Santiago")
        .Single(item =>
            item.BillLineId == boundaryLineId);

    if (boundaryVerification.Status != "MISSING_TARIFF_SOURCE" ||
        !boundaryVerification.Detail.Contains(
            "2026-02-01",
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Phase 9 CNE tariff-boundary guard smoke test failed.");
    }

    // Regression: real target databases can contain exact duplicate
    // normalized candidate rows from repeated official evidence capture.
    // Scenario reconstruction must collapse equivalent duplicates instead of
    // throwing "Sequence contains more than one matching element".
    if (electricityConsumedCandidate is null ||
        !electricityConsumedCandidate.PublishedIvaColumnClp.HasValue ||
        !storedTariff.EffectiveFrom.HasValue)
    {
        throw new InvalidOperationException(
            "Duplicate tariff regression prerequisites are missing.");
    }

    using (var duplicateConnection =
           database.OpenConnection())
    {
        using var duplicate =
            duplicateConnection.CreateCommand();
        duplicate.CommandText = """
            INSERT INTO tariff_rate_candidate (
                publication_id,
                page_number,
                tariff_plan,
                component_key,
                printed_description,
                unit,
                network_type,
                etr_band,
                candidate_index,
                net_rate_clp,
                published_iva_column_clp,
                source_text,
                parser_version,
                validation_state,
                created_utc
            )
            SELECT
                publication_id,
                page_number,
                tariff_plan,
                component_key,
                printed_description,
                unit,
                network_type,
                etr_band,
                candidate_index,
                net_rate_clp,
                published_iva_column_clp,
                source_text,
                parser_version,
                validation_state,
                created_utc
            FROM tariff_rate_candidate
            WHERE rate_candidate_id = $candidateId;
            """;
        duplicate.Parameters.AddWithValue(
            "$candidateId",
            electricityConsumedCandidate.RateCandidateId);
        duplicate.ExecuteNonQuery();
    }

    var duplicateTariffStart =
        storedTariff.EffectiveFrom.Value.AddDays(5);
    var duplicateTariffEnd =
        duplicateTariffStart.AddDays(5);
    var duplicateTariffKwh = 10.0;
    var duplicateTariffAmount =
        duplicateTariffKwh *
        electricityConsumedCandidate
            .PublishedIvaColumnClp.Value;

    var duplicateZone =
        SolarApiTime.GetTimeZoneInfo(
            "America/Santiago");
    var duplicateStartLocal =
        DateTime.SpecifyKind(
            duplicateTariffStart.ToDateTime(
                TimeOnly.MinValue),
            DateTimeKind.Unspecified);
    var duplicateEndLocal =
        DateTime.SpecifyKind(
            duplicateTariffEnd.ToDateTime(
                TimeOnly.MinValue),
            DateTimeKind.Unspecified);

    var duplicateTariffBillId =
        utilityRepository.AddBill(
            new DateTimeOffset(
                duplicateStartLocal,
                duplicateZone.GetUtcOffset(
                    duplicateStartLocal))
                .ToUniversalTime(),
            new DateTimeOffset(
                duplicateEndLocal,
                duplicateZone.GetUtcOffset(
                    duplicateEndLocal))
                .ToUniversalTime(),
            duplicateTariffKwh,
            duplicateTariffAmount,
            "DUPLICATE-CANDIDATE-SMOKE",
            "Equivalent candidate duplicates must not throw",
            tariffPlan: "BT1-T5",
            periodPrecision:
                UtilityTimePrecision.DateOnly);

    utilityRepository.AddBillLine(
        duplicateTariffBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        duplicateTariffAmount,
        categoryKey: "ELECTRICITY_CONSUMED",
        quantity: duplicateTariffKwh,
        unit: "kWh",
        taxTreatment: "AFECTO",
        sortOrder: 10);

    var duplicateScenario =
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver)
        .Analyze(
            duplicateTariffBillId,
            "America/Santiago",
            UtilityGridImportStatisticalCompletion
                .Insufficient(
                    9.0,
                    100.0,
                    0,
                    0,
                    "SMOKE"));

    if (!duplicateScenario.HasTariffModel)
    {
        throw new InvalidOperationException(
            "Equivalent duplicate tariff candidate regression failed.");
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
        "commissioning metadata/profile, protected secret storage, Phase 5 time-range/aggregation math, " +
        "Phase 7 report presets/family event-pattern analysis/source attribution/Excel/PDF export, " +
        "Phase 8 utility meter/bill reconciliation and " +
        "Phase 9 official tariff source capture and unapplied candidate normalization are operational.");
}
finally
{
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

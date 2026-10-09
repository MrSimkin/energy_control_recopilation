using System.IO.Compression;
using ClosedXML.Excel;
using SolarOfThings.Core.Backup;
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
    if (args.Contains("--performance-corpus-large", StringComparer.Ordinal))
    {
        SyntheticPerformanceCorpus.Run(large: true);
        return;
    }
    if (args.Contains("--performance-corpus", StringComparer.Ordinal))
    {
        SyntheticPerformanceCorpus.Run();
        return;
    }

    // Bounded non-sensitive performance recorder smoke. No timing target is
    // asserted (CI hardware is not a substitute for owner's Windows PC).
    var perf = new SolarOfThings.Core.Diagnostics.UiPerformanceRecorder();
    using (var sampled = perf.Measure("UI.Dashboard.Refresh"))
    {
        perf.Record("Startup.SQLiteInitialize", TimeSpan.FromMilliseconds(25));
        sampled.Dispose(); // repeated disposal must not add a second sample
    }
    var first = perf.Snapshot();
    if (first.Count != 2 ||
        first.Count(x => x.Operation == "UI.Dashboard.Refresh") != 1 ||
        first.Any(x => x.ElapsedMilliseconds < 0))
        throw new InvalidOperationException("Performance recorder scope semantics failed.");

    var syntheticTaskGroup = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
    {
        for (var sampleIndex = 0; sampleIndex < 50; sampleIndex++)
            perf.Record("UI.Navigation", TimeSpan.FromMilliseconds(sampleIndex));
    })).ToArray();
    await Task.WhenAll(syntheticTaskGroup);
    var bounded = perf.Snapshot();
    var stats = perf.Summaries();
    if (bounded.Count != SolarOfThings.Core.Diagnostics.UiPerformanceRecorder.MaximumSamples ||
        stats.Single(x => x.Operation == "UI.Navigation").Samples !=
            SolarOfThings.Core.Diagnostics.UiPerformanceRecorder.MaximumSamples ||
        stats.Any(x => x.P95Milliseconds > x.MaxMilliseconds ||
            x.MeanMilliseconds < 0))
        throw new InvalidOperationException("Performance recorder bounded concurrent stats failed.");

    var sensitiveLabelRejected = false;
    try { perf.Record("D:\\Users\\Owner\\PrivateSql", TimeSpan.Zero); }
    catch (ArgumentException) { sensitiveLabelRejected = true; }
    if (!sensitiveLabelRejected)
        throw new InvalidOperationException("Performance recorder accepted a path as operation label.");

    // A delayed DispatcherTimer tick is only a scheduling-lateness signal,
    // not proof of a specific UI freeze or its cause. Suspend/long gaps are ignored.
    var oneSecond = TimeSpan.FromSeconds(1);
    if (DispatcherTimingPolicy.ObserveLateness(TimeSpan.FromMilliseconds(950), oneSecond) is not null ||
        DispatcherTimingPolicy.ObserveLateness(TimeSpan.FromMilliseconds(1149), oneSecond) is not null ||
        DispatcherTimingPolicy.ObserveLateness(TimeSpan.FromMilliseconds(1150), oneSecond) != TimeSpan.FromMilliseconds(150) ||
        DispatcherTimingPolicy.ObserveLateness(TimeSpan.FromMilliseconds(1600), oneSecond) != TimeSpan.FromMilliseconds(600) ||
        DispatcherTimingPolicy.ObserveLateness(TimeSpan.FromSeconds(12), oneSecond) is not null)
        throw new InvalidOperationException("Dispatcher scheduling-lateness policy regressed.");
    var invalidDispatcherPeriodRejected = false;
    try { DispatcherTimingPolicy.ObserveLateness(oneSecond, TimeSpan.Zero); }
    catch (ArgumentOutOfRangeException) { invalidDispatcherPeriodRejected = true; }
    if (!invalidDispatcherPeriodRejected)
        throw new InvalidOperationException("Dispatcher lateness policy accepted zero interval.");

    // An old background read can complete after navigation or a new data
    // request. It must never be rendered over a newer device/page result.
    if (!DashboardRefreshPolicy.CanApply(10, 10, true, false, "A", "A") ||
        DashboardRefreshPolicy.CanApply(9, 10, true, false, "A", "A") ||
        DashboardRefreshPolicy.CanApply(10, 10, false, false, "A", "A") ||
        DashboardRefreshPolicy.CanApply(10, 10, true, true, "A", "A") ||
        DashboardRefreshPolicy.CanApply(10, 10, true, false, "A", "B") ||
        DashboardRefreshPolicy.CanApply(10, 10, true, false, "A", null))
        throw new InvalidOperationException("Dashboard stale-result policy regressed.");

    // Dashboard daily evidence: a visually healthy 99% metric must never
    // conceal a completely absent house or grid stream. This also covers
    // partial days, unknown/NaN data, and genuinely adequate three-stream days.
    var evidenceNone = DashboardDailyEvidencePolicy.Assess(0, 0, 1, 100, 0, 0);
    var evidenceMissing = DashboardDailyEvidencePolicy.Assess(100, 99, 0, 0, 100, 97);
    var evidenceLow = DashboardDailyEvidencePolicy.Assess(100, 99, 100, 72, 100, 92);
    var evidenceOk = DashboardDailyEvidencePolicy.Assess(100, 98, 120, 95, 50, 90);
    var evidenceInvalid = DashboardDailyEvidencePolicy.Assess(100, double.NaN, 100, 90, 100, 100);
    if (evidenceNone.State != "NO_SAMPLES" ||
        evidenceNone.AvailableStreams != 0 ||
        evidenceNone.MinimumAvailableCoveragePercent is not null ||
        evidenceMissing.State != "MISSING_STREAMS" ||
        evidenceMissing.AvailableStreams != 2 ||
        evidenceMissing.MissingStreams != 1 ||
        evidenceMissing.HasCompleteThreeStreamCoverage ||
        Math.Abs(evidenceMissing.MinimumAvailableCoveragePercent!.Value - 97) > 0.0001 ||
        evidenceLow.State != "LOW_COVERAGE" ||
        !evidenceLow.ShouldWarn ||
        Math.Abs(evidenceLow.MinimumAvailableCoveragePercent!.Value - 72) > 0.0001 ||
        evidenceOk.State != "ADEQUATE" ||
        evidenceOk.ShouldWarn ||
        !evidenceOk.HasCompleteThreeStreamCoverage ||
        evidenceInvalid.State != "MISSING_STREAMS" ||
        evidenceInvalid.AvailableStreams != 2)
        throw new InvalidOperationException(
            "Dashboard daily coverage concealed missing, invalid or incomplete measurements.");

    // Contextual report drafts are read-only source selections. They must
    // preserve date and supported aggregation, explicitly expand hours to
    // calendar-day reports, and use the installation's local timezone for
    // selected historical rows (NOT the Windows runner's local timezone).
    var sourceFrom = new DateOnly(2026, 9, 21);
    var sourceTo = new DateOnly(2026, 10, 8);
    var dayDraft = ReportContextNavigationPolicy.FromAnalysis(
        sourceFrom, sourceTo, AggregationPeriod.Day, ReportKind.DetailedEnergy);
    var weekDraft = ReportContextNavigationPolicy.FromAnalysis(
        sourceFrom, sourceTo, AggregationPeriod.Week, ReportKind.SimpleEnergy);
    var hourDraft = ReportContextNavigationPolicy.FromAnalysis(
        sourceFrom, sourceTo, AggregationPeriod.Hour, ReportKind.Battery);
    if (dayDraft.From != sourceFrom || dayDraft.To != sourceTo ||
        dayDraft.Aggregation != AggregationPeriod.Day ||
        dayDraft.Kind != ReportKind.DetailedEnergy ||
        dayDraft.ExpandedToCalendarDay ||
        weekDraft.Aggregation != AggregationPeriod.Week ||
        weekDraft.Kind != ReportKind.SimpleEnergy ||
        hourDraft.Aggregation != AggregationPeriod.Day ||
        hourDraft.Kind != ReportKind.Battery ||
        !hourDraft.ExpandedToCalendarDay)
        throw new InvalidOperationException(
            "Analysis report draft lost its range, kind or explicit hourly conversion.");

    var reverseRangeRejected = false;
    try
    {
        ReportContextNavigationPolicy.FromAnalysis(
            sourceTo, sourceFrom, AggregationPeriod.Day, ReportKind.SimpleEnergy);
    }
    catch (ArgumentOutOfRangeException) { reverseRangeRejected = true; }
    if (!reverseRangeRejected)
        throw new InvalidOperationException(
            "Context report accepted an inverted historical period.");

    var rowStart = DateTimeOffset.Parse("2026-10-08T14:00:00+00:00",
        System.Globalization.CultureInfo.InvariantCulture);
    var rowEnd = rowStart.AddHours(1);
    var syntheticRow = new EnergyAggregationRow(
        "Synthetic hourly row", rowStart, rowEnd,
        1, 2, 3, 4, 5, null, null, null, null,
        90, 95, 97, 92, 91, 90);
    var selectedDraft = ReportContextNavigationPolicy.FromSelectedRow(
        syntheticRow, "America/Santiago", ReportKind.DetailedEnergy);
    if (selectedDraft.From != new DateOnly(2026, 10, 8) ||
        selectedDraft.To != new DateOnly(2026, 10, 8) ||
        selectedDraft.Aggregation != AggregationPeriod.Day ||
        selectedDraft.Kind != ReportKind.DetailedEnergy ||
        !selectedDraft.ExpandedToCalendarDay ||
        selectedDraft.Source != "ANALYSIS_ROW")
        throw new InvalidOperationException(
            "Selected hourly row was not mapped to its own local calendar day.");

    // One selected historical period may feed three distinct report
    // families without losing hour-to-local-calendar-day expansion.
    foreach (var kind in new[] { ReportKind.SimpleEnergy, ReportKind.Battery })
    {
        var specialized = ReportContextNavigationPolicy.FromSelectedRow(
            syntheticRow, "America/Santiago", kind);
        if (specialized.Kind != kind ||
            specialized.From != selectedDraft.From ||
            specialized.To != selectedDraft.To ||
            specialized.Aggregation != AggregationPeriod.Day ||
            !specialized.ExpandedToCalendarDay)
            throw new InvalidOperationException(
                "Selected Analysis row lost its dates in another report family.");
    }
    var rowWindowRejected = false;
    try
    {
        ReportContextNavigationPolicy.FromSelectedRow(
            syntheticRow with { EndUtcExclusive = rowStart },
            "America/Santiago", ReportKind.DetailedEnergy);
    }
    catch (ArgumentException) { rowWindowRejected = true; }
    if (!rowWindowRejected)
        throw new InvalidOperationException("Invalid report row interval was accepted.");

    // Coverage must never paint a stale or different-device result after
    // navigating away, changing the current profile or starting a newer fetch.
    if (!DataCoverageRefreshPolicy.CanApply(4, 4, true, "A", "A") ||
        DataCoverageRefreshPolicy.CanApply(3, 4, true, "A", "A") ||
        DataCoverageRefreshPolicy.CanApply(4, 4, false, "A", "A") ||
        DataCoverageRefreshPolicy.CanApply(4, 4, true, "A", "B") ||
        DataCoverageRefreshPolicy.CanApply(4, 4, true, null, "A"))
        throw new InvalidOperationException("Data coverage stale-result policy regressed.");

    // Data coverage context uses actual UTC sample bounds and the station's
    // local civil dates, not the runner's timezone or today's date.
    var firstCoverage = DateTimeOffset.Parse("2026-10-08T02:30:00Z",
        System.Globalization.CultureInfo.InvariantCulture);
    var lastCoverage = DateTimeOffset.Parse("2026-10-09T03:30:00Z",
        System.Globalization.CultureInfo.InvariantCulture);
    var dataDraft = ReportContextNavigationPolicy.FromStoredCoverage(
        firstCoverage, lastCoverage, "America/Santiago", ReportKind.DetailedEnergy);
    if (dataDraft.From != new DateOnly(2026, 10, 7) ||
        dataDraft.To != new DateOnly(2026, 10, 9) ||
        dataDraft.Aggregation != AggregationPeriod.Day ||
        dataDraft.Kind != ReportKind.DetailedEnergy ||
        dataDraft.Source != "DATA_COVERAGE" ||
        dataDraft.ExpandedToCalendarDay)
        throw new InvalidOperationException(
            "Data coverage report context lost station-local date boundaries.");
    var invertedCoverageRejected = false;
    try
    {
        ReportContextNavigationPolicy.FromStoredCoverage(
            lastCoverage, firstCoverage, "America/Santiago", ReportKind.DetailedEnergy);
    }
    catch (ArgumentOutOfRangeException) { invertedCoverageRejected = true; }
    if (!invertedCoverageRejected)
        throw new InvalidOperationException("Inverted stored coverage was accepted.");

    // Selected-period energy preview must not show zero as an observed value
    // when a stream has no samples, a solitary sample, or no covered hours.
    static PowerMetricStatistics PreviewMetric(int samples, double coveredHours,
        double coverage, double positive, double negative) =>
        new("pv_power_w", null, null, samples, null, null, null,
            positive - negative, positive, negative,
            coveredHours, 24 - coveredHours, coverage, 5, 15);
    var noPreview = ReportPreviewEvidencePolicy.Evaluate(
        PreviewMetric(0, 0, 0, 0, 0));
    var singlePreview = ReportPreviewEvidencePolicy.Evaluate(
        PreviewMetric(1, 0, 0, 0, 0));
    var goodPreview = ReportPreviewEvidencePolicy.Evaluate(
        PreviewMetric(50, 22, 92, 3.5, 0.4));
    var invalidPreview = ReportPreviewEvidencePolicy.Evaluate(
        PreviewMetric(50, 22, double.NaN, 3.5, 0.4));
    var previewQualityEmpty = ReportPreviewEvidencePolicy.Summarize(new EnergyRangeSummary(
        "synthetic", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow,
        PreviewMetric(0, 0, 0, 0, 0), PreviewMetric(1, 0, 0, 0, 0),
        PreviewMetric(0, 0, 0, 0, 0), PreviewMetric(0, 0, 0, 0, 0)));
    var previewQualityPartial = ReportPreviewEvidencePolicy.Summarize(new EnergyRangeSummary(
        "synthetic", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow,
        PreviewMetric(50, 22, 92, 3.5, 0.4), PreviewMetric(0, 0, 0, 0, 0),
        PreviewMetric(50, 20, 80, 4, 0), PreviewMetric(1, 0, 0, 0, 0)));
    var previewQualityAll = ReportPreviewEvidencePolicy.Summarize(new EnergyRangeSummary(
        "synthetic", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow,
        PreviewMetric(50, 22, 92, 3.5, 0.4), PreviewMetric(50, 20, 80, 4, 0),
        PreviewMetric(50, 23, 96, 7, 0), PreviewMetric(50, 21, 88, 3, 1)));
    if (previewQualityEmpty.EligibleStreams != 0 ||
        previewQualityEmpty.MinimumEligibleCoveragePercent is not null ||
        previewQualityPartial.EligibleStreams != 2 ||
        previewQualityPartial.UnavailableStreams != 2 ||
        previewQualityPartial.MinimumEligibleCoveragePercent != 80 ||
        previewQualityAll.EligibleStreams != 4 ||
        previewQualityAll.UnavailableStreams != 0 ||
        previewQualityAll.MinimumEligibleCoveragePercent != 80)
        throw new InvalidOperationException(
            "Report preview quality concealed missing streams or distorted minimum coverage.");

    if (noPreview.PositiveEnergyKwh is not null ||
        singlePreview.PositiveEnergyKwh is not null ||
        invalidPreview.CoveragePercent is not null ||
        goodPreview.PositiveEnergyKwh != 3.5 ||
        goodPreview.NegativeEnergyKwh != 0.4 ||
        goodPreview.CoveragePercent != 92 ||
        !ReportPreviewEvidencePolicy.CanApply(3, 3, true, "A", "A") ||
        ReportPreviewEvidencePolicy.CanApply(2, 3, true, "A", "A") ||
        ReportPreviewEvidencePolicy.CanApply(3, 3, false, "A", "A") ||
        ReportPreviewEvidencePolicy.CanApply(3, 3, true, "A", "B"))
        throw new InvalidOperationException("Report preview missing-data or stale-result policy regressed.");

    // Report → Analysis must retain the user's selected local dates and
    // group size, and must reject an inverted range rather than silently swap.
    var reportSelection = new EnergyReportRequest(
        "synthetic", "synthetic-device",
        new DateOnly(2026, 9, 3), new DateOnly(2026, 10, 8),
        DateTimeOffset.Parse("2026-09-03T04:00:00Z"),
        DateTimeOffset.Parse("2026-10-09T03:00:00Z"),
        "America/Santiago", AggregationPeriod.Month,
        ReportKind.DetailedEnergy, "es");
    // Battery week/month shortcuts remain anchored to the last stored
    // historical date instead of the current date.
    var batteryLastStored = new DateOnly(2026, 8, 19);
    var batteryTimeResolver = new TimeRangeSelectionService();
    var batteryMonthWindow = batteryTimeResolver.ForMonth(
        batteryLastStored.Year, batteryLastStored.Month, "America/Santiago");
    var batteryWeekWindow = batteryTimeResolver.ForRolling7Days(
        batteryLastStored, "America/Santiago");
    if (batteryMonthWindow.LocalStartDate != new DateOnly(2026, 8, 1) ||
        batteryMonthWindow.LocalEndDate != new DateOnly(2026, 8, 31) ||
        batteryWeekWindow.LocalStartDate != new DateOnly(2026, 8, 13) ||
        batteryWeekWindow.LocalEndDate != batteryLastStored)
        throw new InvalidOperationException("Battery date shortcuts regressed.");

    var analysisSelection = ReportContextNavigationPolicy.FromReport(reportSelection);
    if (analysisSelection.From != reportSelection.LocalStartDate ||
        analysisSelection.To != reportSelection.LocalEndDate ||
        analysisSelection.Aggregation != AggregationPeriod.Month ||
        analysisSelection.Source != "REPORTS_RANGE")
        throw new InvalidOperationException("Reports-to-Analysis range changed in transit.");
    var invertedReportRejected = false;
    try
    {
        ReportContextNavigationPolicy.FromReport(reportSelection with
        {
            LocalStartDate = reportSelection.LocalEndDate,
            LocalEndDate = reportSelection.LocalStartDate
        });
    }
    catch (ArgumentOutOfRangeException) { invertedReportRejected = true; }
    if (!invertedReportRejected)
        throw new InvalidOperationException("Reports-to-Analysis accepted inverted dates.");

    // Stage-and-publish: a failed/empty export must leave an existing
    // destination unchanged; successful publication replaces it only after
    // a nonempty sibling has been fully generated.
    Directory.CreateDirectory(root);
    var destination = Path.Combine(root, "existing_report.pdf");
    File.WriteAllText(destination, "previous accepted report");
    var failedStage = ReportFilePublicationService.CreateStagingPath(destination);
    var rejectedUnfinished = false;
    try { ReportFilePublicationService.Publish(failedStage, destination); }
    catch (IOException) { rejectedUnfinished = true; }
    if (!rejectedUnfinished || File.ReadAllText(destination) != "previous accepted report")
        throw new InvalidOperationException("Incomplete report publish destroyed existing output.");
    File.WriteAllText(failedStage, string.Empty);
    var rejectedEmpty = false;
    try { ReportFilePublicationService.Publish(failedStage, destination); }
    catch (IOException) { rejectedEmpty = true; }
    if (!rejectedEmpty || File.ReadAllText(destination) != "previous accepted report")
        throw new InvalidOperationException("Empty report publish destroyed existing output.");
    ReportFilePublicationService.DiscardStaging(failedStage);
    var completeStage = ReportFilePublicationService.CreateStagingPath(destination);
    File.WriteAllText(completeStage, "completed synthetic report");
    ReportFilePublicationService.Publish(completeStage, destination);
    if (File.Exists(completeStage) ||
        File.ReadAllText(destination) != "completed synthetic report")
        throw new InvalidOperationException("Completed report was not published atomically.");
    File.Delete(destination);
    var invalidReportExtension = false;
    try { ReportFilePublicationService.CreateStagingPath(
        Path.Combine(root, "script.exe")); }
    catch (ArgumentException) { invalidReportExtension = true; }
    if (!invalidReportExtension)
        throw new InvalidOperationException("Unexpected output extension accepted for report.");

    // The post-export Open buttons must accept only existing user-requested
    // PDF/XLSX outputs, never arbitrary files or a stale missing target.
    Directory.CreateDirectory(root);
    var syntheticPdfPath = Path.Combine(root, "sample_report.PDF");
    var syntheticExecutablePath = Path.Combine(root, "not_a_report.exe");
    File.WriteAllText(syntheticPdfPath, "synthetic path guard only");
    File.WriteAllText(syntheticExecutablePath, "synthetic path guard only");
    if (!ReportExportLaunchPolicy.CanOpen(syntheticPdfPath) ||
        ReportExportLaunchPolicy.CanOpen(syntheticExecutablePath) ||
        ReportExportLaunchPolicy.CanOpen(Path.Combine(root, "missing.xlsx")) ||
        ReportExportLaunchPolicy.CanOpen(null))
        throw new InvalidOperationException("Export report open-file gate regressed.");
    File.Delete(syntheticPdfPath);
    File.Delete(syntheticExecutablePath);
    if (ReportExportLaunchPolicy.CanOpen(syntheticPdfPath))
        throw new InvalidOperationException("Stale exported report was considered available.");

    // Battery background-result guard: only the visible, current device and
    // request generation may be rendered.
    if (!BatteryRefreshPolicy.CanApply(4, 4, true, false, "A", "A") ||
        BatteryRefreshPolicy.CanApply(3, 4, true, false, "A", "A") ||
        BatteryRefreshPolicy.CanApply(4, 4, false, false, "A", "A") ||
        BatteryRefreshPolicy.CanApply(4, 4, true, true, "A", "A") ||
        BatteryRefreshPolicy.CanApply(4, 4, true, false, "A", "B") ||
        BatteryRefreshPolicy.CanApply(4, 4, true, false, "A", null))
        throw new InvalidOperationException("Battery stale-result policy regressed.");

    // An obsolete Analysis range or device must never replace newer charts.
    if (!AnalysisRefreshPolicy.CanApply(5, 5, true, false, "device-a", "device-a") ||
        AnalysisRefreshPolicy.CanApply(4, 5, true, false, "device-a", "device-a") ||
        AnalysisRefreshPolicy.CanApply(5, 5, false, false, "device-a", "device-a") ||
        AnalysisRefreshPolicy.CanApply(5, 5, true, true, "device-a", "device-a") ||
        AnalysisRefreshPolicy.CanApply(5, 5, true, false, "device-a", "device-b") ||
        AnalysisRefreshPolicy.CanApply(5, 5, true, false, "device-a", null))
        throw new InvalidOperationException("Analysis result-generation guard regressed.");

    // Re-rendering a different energy series must not query SQLite again if
    // the already displayed chart rows still belong to the current request.
    if (!AnalysisRefreshPolicy.CanReuseChart(11, 11, true, false, "A", "A") ||
        AnalysisRefreshPolicy.CanReuseChart(10, 11, true, false, "A", "A") ||
        AnalysisRefreshPolicy.CanReuseChart(11, 11, false, false, "A", "A") ||
        AnalysisRefreshPolicy.CanReuseChart(11, 11, true, true, "A", "A") ||
        AnalysisRefreshPolicy.CanReuseChart(11, 11, true, false, "A", "B") ||
        AnalysisRefreshPolicy.CanReuseChart(11, 11, true, false, null, null) ||
        AnalysisRefreshPolicy.CanReuseChart(-1, -1, true, false, "A", "A"))
        throw new InvalidOperationException("Analysis chart reuse guard regressed.");

    // A delayed Battery navigation snapshot can cause a network request only
    // if page, device, generation and session remain valid and it is stale.
    if (!BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, true, false, "A", "A", true, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            3, 4, true, false, "A", "A", true, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, false, false, "A", "A", true, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, true, true, "A", "A", true, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, true, false, "A", "B", true, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, true, false, "A", null, true, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, true, false, "A", "A", false, false) ||
        BatteryNavigationRefreshPolicy.ShouldRefresh(
            4, 4, true, false, "A", "A", true, true))
        throw new InvalidOperationException("Battery navigation refresh guard regressed.");

    // Diagnostics ranks observed p95 rather than alphabetical labels.
    var rankSample = new UiPerformanceRecorder();
    rankSample.Record("UI.Analysis.Refresh", TimeSpan.FromMilliseconds(5));
    rankSample.Record("UI.Dashboard.Refresh", TimeSpan.FromMilliseconds(30));
    rankSample.Record("UI.Battery.Refresh", TimeSpan.FromMilliseconds(30));
    var rankNames = rankSample.Summaries().Select(x => x.Operation).ToArray();
    if (!rankNames.SequenceEqual(new[]
        { "UI.Battery.Refresh", "UI.Dashboard.Refresh", "UI.Analysis.Refresh" }))
        throw new InvalidOperationException("Diagnostics p95 ordering regressed.");

    // Responsive breakpoint regression: same band must preserve layout
    // definitions even as the WPF window changes width by a pixel.
    foreach (var (width, columns) in new (double, int)[]
    {
        (double.NaN, 1), (double.NegativeInfinity, 1),
        (0, 1), (619.99, 1), (620, 2), (1039.99, 2),
        (1040, 4), (2400, 4)
    })
    {
        if (ResponsiveGridLayoutPolicy.ColumnsForUsableWidth(width) != columns)
            throw new InvalidOperationException(
                "Responsive WPF grid breakpoint policy unexpectedly changed.");
    }
    if (ResponsiveGridLayoutPolicy.ColumnsForUsableWidth(700) !=
        ResponsiveGridLayoutPolicy.ColumnsForUsableWidth(900))
        throw new InvalidOperationException(
            "Layout would be unnecessarily rebuilt within the same breakpoint.");

    // Deterministic nearest-rank p95 sanity: not a machine-speed assertion.
    var distribution = new UiPerformanceRecorder();
    for (var ms = 1; ms <= 20; ms++)
        distribution.Record("Data.Dashboard.LatestDayEnergy", TimeSpan.FromMilliseconds(ms));
    var measured = distribution.Summaries().Single();
    if (measured.Samples != 20 ||
        Math.Abs(measured.MeanMilliseconds - 10.5) > 0.0001 ||
        Math.Abs(measured.P95Milliseconds - 19) > 0.0001 ||
        Math.Abs(measured.MaxMilliseconds - 20) > 0.0001)
        throw new InvalidOperationException("Data/UI timing scope summary regression.");
    using (distribution.Measure("UI.Dashboard.Refresh"))
    using (distribution.Measure("Data.Dashboard.Coverage"))
    {
        // Nested UI and data timing scopes must retain independent samples.
    }
    if (distribution.Summaries().Count != 3 ||
        distribution.Snapshot().Any(x => x.ElapsedMilliseconds < 0))
        throw new InvalidOperationException("Nested data/UI timing scopes were lost.");

    var paths = new AppPaths(root);
    var database = new SqliteDatabase(paths);

    database.Initialize();

    if (!File.Exists(database.DatabasePath))
    {
        throw new InvalidOperationException("SQLite database file was not created.");
    }

    if (database.GetSchemaVersion() != SqliteDatabase.CurrentSchemaVersion ||
        SqliteDatabase.CurrentSchemaVersion != 17)
    {
        throw new InvalidOperationException("Unexpected SQLite schema version.");
    }

    // Phase 11: reporting views must be directly queryable, without
    // inventing grid-import kWh from potentially discontinuous power frames.
    using (var reporting = database.OpenConnection())
    using (var views = reporting.CreateCommand())
    {
        views.CommandText =
            """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'view' AND name IN (
                'reporting_grid_import', 'reporting_battery',
                'reporting_utility_bills', 'reporting_bill_line_evidence',
                'reporting_hourly_power_samples', 'reporting_daily_power_samples',
                'data_quality_summary');
            """;
        if (Convert.ToInt32(views.ExecuteScalar()) != 7)
            throw new InvalidOperationException(
                "Phase 11 canonical reporting views are missing.");

        views.CommandText = "SELECT COUNT(*) FROM reporting_hourly_power_samples;";
        if (Convert.ToInt32(views.ExecuteScalar()) != 0)
            throw new InvalidOperationException(
                "Empty hourly power-sample view unexpectedly has rows.");

        views.CommandText = "SELECT COUNT(*) FROM reporting_grid_import;";
        if (Convert.ToInt32(views.ExecuteScalar()) != 0)
            throw new InvalidOperationException(
                "Empty Phase 11 grid-import view unexpectedly has rows.");
    }

    // Verify the new views calculate honest arithmetic W statistics over
    // two known source samples (not kWh, not time-weighted integration).
    using (var reportingTest = database.OpenConnection())
    using (var addSamples = reportingTest.CreateCommand())
    {
        addSamples.CommandText =
            """
            INSERT INTO normalized_metric_sample(
                device_id,metric_key,recorded_at_utc,
                normalized_value,normalized_unit,source_attribute_key,
                source_value_json,normalization_rule_version,confidence,
                quality,updated_utc)
            VALUES
            ('PHASE11_SMOKE','grid_import_power_w',
             '2026-10-07T09:01:00.0000000+00:00',100,'W',
             'view-test','100','smoke-v1','HIGH','OBSERVED',
             '2026-10-08T00:00:00Z'),
            ('PHASE11_SMOKE','grid_import_power_w',
             '2026-10-07T09:11:00.0000000+00:00',200,'W',
             'view-test','200','smoke-v1','HIGH','OBSERVED',
             '2026-10-08T00:00:00Z');
            """;
        addSamples.ExecuteNonQuery();
        addSamples.CommandText =
            """
            SELECT sample_count, sample_average_w
            FROM reporting_hourly_power_samples
            WHERE device_id = 'PHASE11_SMOKE'
              AND metric_key = 'grid_import_power_w'
              AND utc_hour = '2026-10-07T09';
            """;
        using (var hourly = addSamples.ExecuteReader())
        {
            if (!hourly.Read() ||
                hourly.GetInt64(0) != 2 ||
                Math.Abs(hourly.GetDouble(1) - 150) > 0.001 ||
                hourly.Read())
            {
                throw new InvalidOperationException(
                    "Phase 11 measured hourly power view regression failed.");
            }
        }
        addSamples.CommandText =
            """
            SELECT sample_count, sample_average_w
            FROM reporting_daily_power_samples
            WHERE device_id = 'PHASE11_SMOKE'
              AND metric_key = 'grid_import_power_w'
              AND utc_day = '2026-10-07';
            """;
        using (var daily = addSamples.ExecuteReader())
        {
            if (!daily.Read() ||
                daily.GetInt64(0) != 2 ||
                Math.Abs(daily.GetDouble(1) - 150) > 0.001 ||
                daily.Read())
            {
                throw new InvalidOperationException(
                    "Phase 11 measured daily power view regression failed.");
            }
        }
    }

    // Exercise the backup with a committed write that may still reside
    // in WAL rather than the main .db file.
    using (var walSource = database.OpenConnection())
    using (var walWrite = walSource.CreateCommand())
    {
        walWrite.CommandText =
            """
            INSERT INTO app_setting(key,value,updated_utc)
            VALUES ('smoke.wal.snapshot','committed','2026-10-08T00:00:00Z');
            """;
        walWrite.ExecuteNonQuery();
    }

    // Phase 12: validate snapshots without restoring or replacing the
    // source DB. Verify backup bytes are independently readable.
    var backupService = new DatabaseBackupService(database, paths);
    var manualBackup = backupService.CreateVerifiedBackup();
    var automaticBackup = backupService.CreateAutomaticBackupIfDue();
    using (var copied = new SqliteConnection(
        new SqliteConnectionStringBuilder
        {
            DataSource = manualBackup.Path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString()))
    {
        copied.Open();
        using var checkWalCopy = copied.CreateCommand();
        checkWalCopy.CommandText =
            "SELECT value FROM app_setting WHERE key = 'smoke.wal.snapshot';";
        if (!string.Equals(
            Convert.ToString(checkWalCopy.ExecuteScalar()),
            "committed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Phase 12 backup lost a committed WAL-sourced record.");
        }
    }

    if (manualBackup.IntegrityStatus != "PASS" ||
        manualBackup.SchemaVersion != 17 ||
        automaticBackup is null ||
        backupService.CreateAutomaticBackupIfDue() is not null ||
        !File.Exists(manualBackup.Path + ".manifest.json") ||
        !File.Exists(automaticBackup.Path + ".manifest.json") ||
        database.GetSchemaVersion() != 17)
    {
        throw new InvalidOperationException(
            "Phase 12 WAL-consistent verified backup smoke test failed.");
    }

    // Complete package with WAL and documents; no access to owner's real DB.
    var syntheticBill = Path.Combine(paths.UtilityBillDirectory, "smoke-original-bill.txt");
    var syntheticTariff = Path.Combine(paths.TariffDirectory, "smoke-tariff.txt");
    File.WriteAllText(syntheticBill, "Synthetic bill proof");
    File.WriteAllText(syntheticTariff, "Synthetic tariff proof");

    // A genuine original-document reference in the snapshot. The package
    // must not be considered complete if its document later disappears.
    using (var witness = database.OpenConnection())
    using (var insert = witness.CreateCommand())
    {
        insert.CommandText = """
            INSERT INTO utility_bill_document
                (provider,original_file_name,local_pdf_path,content_sha256,
                 content_length,page_count,parser_version,extracted_text,imported_utc)
            VALUES ('TEST','smoke-original-bill.txt',$path,$sha,
                    20,1,'synthetic',NULL,'2026-10-08T00:00:00Z');
            """;
        insert.Parameters.AddWithValue("$path", syntheticBill);
        insert.Parameters.AddWithValue("$sha", Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(syntheticBill))).ToLowerInvariant());
        insert.ExecuteNonQuery();
    }

    // Seed stable source identities for the isolated, read-only recovery preview.
    using (var setup = database.OpenConnection())
    using (var insert = setup.CreateCommand())
    {
        insert.CommandText = """
            INSERT INTO app_setting(key,value,updated_utc)
            VALUES
                ('smoke.preview.source-only','only-in-backup','2026-10-08T00:00:00Z'),
                ('smoke.preview.conflict','source-value','2026-10-08T00:00:00Z');
            """;
        insert.ExecuteNonQuery();
    }

    var completeService = new FullBackupService(database, paths);
    var complete = completeService.Create("0.11.0-test", "synthetic", "smoke");
    var fullManifest = FullBackupService.VerifyArchive(complete.Path);
    if (complete.IntegrityStatus != "PASS" ||
        complete.SchemaVersion != 17 ||
        fullManifest.SchemaVersion != 17 ||
        fullManifest.AppVersion != "0.11.0-test" ||
        !fullManifest.Files.Any(f => f.RelativePath == "database/energy.db") ||
        !fullManifest.Files.Any(f => f.RelativePath == "documents/Bills/smoke-original-bill.txt") ||
        !fullManifest.Files.Any(f => f.RelativePath == "documents/Tariffs/smoke-tariff.txt"))
    {
        throw new InvalidOperationException("Complete backup package smoke failed.");
    }

    // Even perfectly valid ZIP paths and checksums are insufficient if the
    // entry metadata declares a symbolic link or another non-regular file.
    // No extraction is attempted; only a synthetic package clone is edited.
    foreach (var (unixType, label) in new[]
    {
        (0xA000u, "symlink"),
        (0x4000u, "directory")
    })
    {
        var disguised = Path.Combine(root, "unsafe-zip-entry-" + label + ".zip");
        File.Copy(complete.Path, disguised);
        using (var zip = ZipFile.Open(disguised, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("documents/Bills/smoke-original-bill.txt")!;
            entry.ExternalAttributes = unchecked((int)(unixType << 16));
        }
        var nonFileRejected = false;
        try { FullBackupService.VerifyArchive(disguised); }
        catch (InvalidDataException) { nonFileRejected = true; }
        if (!nonFileRejected)
            throw new InvalidOperationException(
                "Backup verifier accepted non-regular ZIP entry: " + label);
    }

    // On Windows the same ZIP can advertise a reparse point or directory
    // through DOS attributes even when POSIX type bits are absent.
    foreach (var (dosFlag, label) in new[]
    {
        (FileAttributes.ReparsePoint, "windows-reparse"),
        (FileAttributes.Directory, "windows-directory")
    })
    {
        var disguised = Path.Combine(root, "unsafe-dos-" + label + ".zip");
        File.Copy(complete.Path, disguised);
        using (var zip = ZipFile.Open(disguised, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("documents/Bills/smoke-original-bill.txt")!;
            entry.ExternalAttributes |= (int)dosFlag;
        }
        var nonFileRejected = false;
        try { FullBackupService.VerifyArchive(disguised); }
        catch (InvalidDataException) { nonFileRejected = true; }
        if (!nonFileRejected)
            throw new InvalidOperationException(
                "Backup verifier accepted unsafe Windows ZIP entry: " + label);
    }

    // Reject an otherwise hash-consistent ZIP with two document names that
    // differ only by case: on Windows they can map to the same physical path.
    var collidingArchive = Path.Combine(root, "case-collision-package.zip");
    File.Copy(complete.Path, collidingArchive);
    using (var collisionZip = System.IO.Compression.ZipFile.Open(
               collidingArchive, ZipArchiveMode.Update))
    {
        const string originalName = "documents/Bills/smoke-original-bill.txt";
        const string aliasName = "documents/Bills/SMOKE-original-bill.txt";
        var originalDocument = collisionZip.GetEntry(originalName)!;
        byte[] documentBytes;
        using (var input = originalDocument.Open())
        using (var buffer = new MemoryStream())
        {
            input.CopyTo(buffer);
            documentBytes = buffer.ToArray();
        }
        using (var aliasOutput = collisionZip.CreateEntry(aliasName).Open())
            aliasOutput.Write(documentBytes);
        var manifestEntry = collisionZip.GetEntry("manifest.json")!;
        CompleteBackupManifest revised;
        using (var manifestInput = manifestEntry.Open())
            revised = System.Text.Json.JsonSerializer
                .Deserialize<CompleteBackupManifest>(manifestInput)!;
        manifestEntry.Delete();
        revised = revised with { Files = revised.Files.Concat(
            [new CompleteBackupEntry(aliasName, documentBytes.Length,
                fullManifest.Files.Single(x =>
                    x.RelativePath == originalName).Sha256)]).ToArray() };
        using var manifestOutput = collisionZip.CreateEntry("manifest.json").Open();
        System.Text.Json.JsonSerializer.Serialize(manifestOutput, revised);
    }
    var caseCollisionRejected = false;
    try { FullBackupService.VerifyArchive(collidingArchive); }
    catch (InvalidDataException) { caseCollisionRejected = true; }
    if (!caseCollisionRejected)
        throw new InvalidOperationException(
            "Case-colliding ZIP entries were accepted as a complete backup.");

    // A forged self-consistent ZIP+manifest must be rejected if it drops
    // a bill document still referenced by the embedded SQLite snapshot.
    const string originalBillEntry = "documents/Bills/smoke-original-bill.txt";
    var omittedEvidenceArchive = Path.Combine(root, "omitted-evidence.zip");
    File.Copy(complete.Path, omittedEvidenceArchive);
    using (var omittedZip = System.IO.Compression.ZipFile.Open(
               omittedEvidenceArchive, ZipArchiveMode.Update))
    {
        omittedZip.GetEntry(originalBillEntry)!.Delete();
        var oldManifest = omittedZip.GetEntry("manifest.json")!;
        CompleteBackupManifest alteredManifest;
        using (var input = oldManifest.Open())
            alteredManifest = System.Text.Json.JsonSerializer
                .Deserialize<CompleteBackupManifest>(input)!;
        oldManifest.Delete();
        alteredManifest = alteredManifest with
        {
            Files = alteredManifest.Files.Where(x =>
                x.RelativePath != originalBillEntry).ToArray()
        };
        using var output = omittedZip.CreateEntry("manifest.json").Open();
        System.Text.Json.JsonSerializer.Serialize(output, alteredManifest);
    }
    var missingWitnessRejected = false;
    try { FullBackupService.VerifyArchive(omittedEvidenceArchive); }
    catch (InvalidDataException) { missingWitnessRejected = true; }
    if (!missingWitnessRejected)
        throw new InvalidOperationException(
            "A valid ZIP/manifest without the SQLite-referenced bill was accepted.");

    // A self-consistent cross-platform ZIP must still reject names that
    // Windows cannot safely create when a future recovery extracts documents.
    foreach (var (badName, index) in new[]
    {
        ("documents/Bills/CON.txt", 1),
        ("documents/Bills/trailing.", 2),
        ("documents/Bills/invalid?.txt", 3),
        ("documents/Bills/CONIN$.txt", 4),
        ("documents/Bills/CONOUT$.txt", 5),
        ("documents/Bills/COM¹.txt", 6),
        ("documents/Bills/LPT².txt", 7),
        ("documents/Bills/" + new string('x', 256) + ".txt", 8)
    })
    {
        var reservedZipPath = Path.Combine(root, $"reserved-name-{index}.zip");
        File.Copy(complete.Path, reservedZipPath);
        var payload = System.Text.Encoding.UTF8.GetBytes("synthetic unsafe name");
        using (var edited = System.IO.Compression.ZipFile.Open(
                   reservedZipPath, ZipArchiveMode.Update))
        {
            using (var added = edited.CreateEntry(badName).Open())
                added.Write(payload);
            var manifestEntry = edited.GetEntry("manifest.json")!;
            CompleteBackupManifest altered;
            using (var input = manifestEntry.Open())
                altered = System.Text.Json.JsonSerializer
                    .Deserialize<CompleteBackupManifest>(input)!;
            manifestEntry.Delete();
            altered = altered with { Files = altered.Files.Concat(
                [new CompleteBackupEntry(badName, payload.Length,
                    Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(payload))
                        .ToLowerInvariant())]).ToArray() };
            using var updated = edited.CreateEntry("manifest.json").Open();
            System.Text.Json.JsonSerializer.Serialize(updated, altered);
        }
        var unsafeNameRejected = false;
        try { FullBackupService.VerifyArchive(reservedZipPath); }
        catch (InvalidDataException) { unsafeNameRejected = true; }
        if (!unsafeNameRejected)
            throw new InvalidOperationException(
                "Unsafe Windows document entry was accepted: " + badName);
    }

    // The ZIP includes committed WAL records and a structurally readable DB.
    var smokeExtract = Path.Combine(root, "verify-complete-backup.db");
    using (var packageZip = System.IO.Compression.ZipFile.OpenRead(complete.Path))
    {
        packageZip.GetEntry("database/energy.db")!.ExtractToFile(smokeExtract);
    }
    using (var recovered = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = smokeExtract, Mode = SqliteOpenMode.ReadOnly, Pooling = false
    }.ToString()))
    {
        recovered.Open();
        using var query = recovered.CreateCommand();
        query.CommandText =
            "SELECT value FROM app_setting WHERE key = 'smoke.wal.snapshot';";
        if (Convert.ToString(query.ExecuteScalar()) != "committed")
            throw new InvalidOperationException("Full backup lost committed WAL record.");
    }

    // The unique marker is required for ALL synthetic recovery operations,
    // including the read-only preview, before any target DB is opened.
    IsolatedRecoveryAdditiveTestService.MarkSyntheticSmokeFixture(root);
    // Selective recovery PREVIEW reads package and isolated target, and makes NO writes.
    var previewRoot = Path.Combine(root, "isolated-preview-target");
    var previewPaths = new AppPaths(previewRoot);
    var previewDatabase = new SqliteDatabase(previewPaths);
    previewDatabase.Initialize();
    using (var target = previewDatabase.OpenConnection())
    using (var setup = target.CreateCommand())
    {
        setup.CommandText = """
            INSERT INTO app_setting(key,value,updated_utc)
            VALUES
                ('smoke.wal.snapshot','committed','2026-10-08T00:00:00Z'),
                ('smoke.preview.conflict','different-live-value','2026-10-08T00:00:00Z'),
                ('smoke.preview.target-only','preserve-me','2026-10-08T00:00:00Z');
            """;
        setup.ExecuteNonQuery();
    }
    var previewer = new IsolatedRecoveryPreviewService();
    // Regression: merely placing a target under OS temp is NOT authorization.
    var arbitraryPreviewRejected = false;
    try
    {
        previewer.Preview(complete.Path,
            Path.Combine(Path.GetTempPath(), "UNSAFE-user-database.db"));
    }
    catch (InvalidOperationException) { arbitraryPreviewRejected = true; }
    if (!arbitraryPreviewRejected)
        throw new InvalidOperationException(
            "Recovery preview accepted an unmarked temp database path.");

    var unmarkedRoot = Path.Combine(Path.GetTempPath(),
        "SolarEnergyMonitorSmoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(unmarkedRoot);
    try
    {
        var unmarkedPreviewRejected = false;
        try { previewer.Preview(complete.Path, Path.Combine(unmarkedRoot, "energy.db")); }
        catch (InvalidOperationException) { unmarkedPreviewRejected = true; }
        if (!unmarkedPreviewRejected)
            throw new InvalidOperationException(
                "Recovery preview accepted a fixture without its synthetic marker.");
    }
    finally { Directory.Delete(unmarkedRoot); }

    var summary = previewer.Preview(complete.Path, previewDatabase.DatabasePath);
    var previewSettings = summary.Categories.Single(c => c.Category == "SETTINGS");
    if (summary.Status != "READ_ONLY_PREVIEW" ||
        previewSettings.Missing < 1 || previewSettings.Identical < 1 ||
        previewSettings.Conflicts < 1 || previewSettings.TargetOnly < 1 ||
        previewSettings.SourceRecords != previewSettings.Missing +
            previewSettings.Identical + previewSettings.Conflicts ||
        previewSettings.TargetRecords != previewSettings.Identical +
            previewSettings.Conflicts + previewSettings.TargetOnly ||
        !previewSettings.ChangedFields.TryGetValue("value", out var changedValues) ||
        changedValues < 1 ||
        !summary.Categories.Any(c => c.Category == "BILLS_AND_CHARGES" &&
            c.UnsupportedReason is not null))
        throw new InvalidOperationException("Isolated selective recovery preview failed.");
    if (summary.Totals.ComparedCategories != 3 ||
        summary.Totals.BlockedCategories != 3 ||
        summary.Totals.Conflicts < 1 ||
        summary.Totals.OnlyInTarget < 1)
        throw new InvalidOperationException("Selective recovery totals conceal blocking categories.");
    // Test-only eight-stage plan. Only SETTINGS may be staged; no bill,
    // tariff, meter or telemetry row may be imported by this service.
    var planner = new IsolatedRecoveryPlanService();
    var planned = planner.CreatePlan(complete.Path, previewDatabase.DatabasePath);
    var repeatedPlan = planner.CreatePlan(complete.Path, previewDatabase.DatabasePath);
    if (planned.Status != "SYNTHETIC_SETTINGS_PLAN" ||
        planned.PlanId.Length != 64 || planned.TargetSettingsSha256.Length != 64 ||
        planned.TargetOtherTablesSha256.Length != 64 ||
        planned.SourcePackageSha256.Length != 64 ||
        planned.PlanId != repeatedPlan.PlanId ||
        planned.Steps.Count != 8 ||
        planned.Steps.Select(x => x.Order).Distinct().Count() != 8 ||
        !planned.Steps.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, 8)) ||
        planned.Steps.Count(x => x.Status == "SYNTHETIC_STAGE_ONLY") != 1 ||
        planned.Steps.Single(x => x.Category == "SETTINGS").Status != "SYNTHETIC_STAGE_ONLY" ||
        planned.Steps.Single(x => x.Category == "METER_READINGS").Status !=
            "BLOCKED_AMBIGUOUS_IDENTITY" ||
        planned.Steps.Single(x => x.Category == "TARIFF_RELATIONS").Status !=
            "BLOCKED_DEPENDENCIES" ||
        planned.Steps.Single(x => x.Category == "ENERGY_TELEMETRY").Status !=
            "BLOCKED_UNSUPPORTED" ||
        planned.MissingSettings != previewSettings.Missing ||
        planned.ConflictingSettings != previewSettings.Conflicts ||
        planned.TargetOnlySettings != previewSettings.TargetOnly)
        throw new InvalidOperationException(
            "Eight-stage synthetic recovery plan was incomplete or unstable.");
    if (planned.Steps.Any(x => x.Explanation.Contains("different-live-value",
            StringComparison.Ordinal)))
        throw new InvalidOperationException("Plan leaked original setting values.");

    var plannedStage = planner.StageSettingsOnly(
        planned, complete.Path, previewDatabase.DatabasePath);
    if (plannedStage.Status != "STAGED_SYNTHETIC_ONLY" ||
        plannedStage.Added != planned.MissingSettings ||
        plannedStage.Conflicts != planned.ConflictingSettings ||
        plannedStage.AlreadyPresent != planned.IdenticalSettings)
        throw new InvalidOperationException("Planned stage counters did not reconcile.");
    var stagedPlan = planner.CreatePlan(complete.Path, plannedStage.StagedDatabasePath);
    if (stagedPlan.TargetOtherTablesSha256 != planned.TargetOtherTablesSha256)
        throw new InvalidOperationException(
            "Synthetic stage modified a non-settings SQLite table.");
    using (var unchangedSettings = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = plannedStage.StagedDatabasePath,
        Mode = SqliteOpenMode.ReadOnly, Pooling = false
    }.ToString()))
    {
        unchangedSettings.Open();
        using var check = unchangedSettings.CreateCommand();
        check.CommandText =
            "SELECT value,updated_utc FROM app_setting WHERE key='smoke.preview.conflict';";
        using var record = check.ExecuteReader();
        if (!record.Read() ||
            record.GetString(0) != "different-live-value" ||
            record.GetString(1) != "2026-10-08T00:00:00Z")
            throw new InvalidOperationException(
                "Synthetic recovery stage altered conflict value or timestamp.");
    }
    var plannedOutput = previewer.Preview(complete.Path, plannedStage.StagedDatabasePath);
    var plannedSettings = plannedOutput.Categories.Single(x => x.Category == "SETTINGS");
    if (plannedSettings.Missing != 0 ||
        plannedSettings.Conflicts != planned.ConflictingSettings ||
        plannedSettings.TargetOnly != planned.TargetOnlySettings ||
        planner.CreatePlan(complete.Path, previewDatabase.DatabasePath).PlanId !=
            planned.PlanId)
        throw new InvalidOperationException(
            "Planned stage changed original synthetic target or discarded target-only settings.");
    File.Delete(plannedStage.StagedDatabasePath);

    // An unapproved package path cannot reuse this plan, even if the two
    // ZIPs have identical bytes; fixture target paths are equally bound.
    var wrongPairBlocked = false;
    try
    {
        planner.StageSettingsOnly(planned, Path.Combine(root, "other-package.zip"),
            previewDatabase.DatabasePath);
    }
    catch (InvalidOperationException) { wrongPairBlocked = true; }
    if (!wrongPairBlocked)
        throw new InvalidOperationException("Plan accepted a different source-package path.");

    // A forged checklist must fail even if it reuses a real plan's hashes
    // and retains the SETTINGS permission unchanged.
    var tamperedSteps = planned with
    {
        Steps = planned.Steps.Select(step =>
            step.Category == "ENERGY_TELEMETRY"
                ? step with { Status = "SYNTHETIC_STAGE_ONLY" }
                : step).ToArray()
    };
    var forgedPlanRejected = false;
    try { planner.StageSettingsOnly(tamperedSteps, complete.Path, previewDatabase.DatabasePath); }
    catch (InvalidOperationException) { forgedPlanRejected = true; }
    if (!forgedPlanRejected)
        throw new InvalidOperationException("Recovery accepted a forged telemetry stage.");

    // A change ONLY in linked destination data must expire an existing
    // synthetic plan even if no app_setting key or value changed.
    var relationStalePaths = new AppPaths(Path.Combine(root, "recovery-plan-relational-stale"));
    var relationStaleDb = new SqliteDatabase(relationStalePaths);
    relationStaleDb.Initialize();
    var relationalPlan = planner.CreatePlan(complete.Path, relationStaleDb.DatabasePath);
    using (var linked = relationStaleDb.OpenConnection())
    using (var insert = linked.CreateCommand())
    {
        insert.CommandText = """
            INSERT INTO utility_meter_reading(
                reading_at_utc,reading_kwh,created_utc,updated_utc)
            VALUES('2026-10-08T01:00:00Z',320,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        insert.ExecuteNonQuery();
    }
    var relationalNow = planner.CreatePlan(complete.Path, relationStaleDb.DatabasePath);
    if (relationalNow.TargetSettingsSha256 != relationalPlan.TargetSettingsSha256 ||
        relationalNow.TargetOtherTablesSha256 == relationalPlan.TargetOtherTablesSha256 ||
        relationalNow.PlanId == relationalPlan.PlanId)
        throw new InvalidOperationException(
            "Relational target change did not expire original plan.");
    var staleRelationBlocked = false;
    try { planner.StageSettingsOnly(relationalPlan, complete.Path, relationStaleDb.DatabasePath); }
    catch (InvalidOperationException) { staleRelationBlocked = true; }
    if (!staleRelationBlocked)
        throw new InvalidOperationException(
            "Recovery used plan after unrelated destination table changed.");

    // Source and destination have independent fingerprints. A WAL-visible
    // target setting update makes an existing plan STALE even if row counts
    // remain identical, and no new staged database may survive.
    var stalePaths = new AppPaths(Path.Combine(root, "recovery-plan-stale-target"));
    var staleDb = new SqliteDatabase(stalePaths);
    staleDb.Initialize();
    using (var conn = staleDb.OpenConnection())
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            INSERT INTO app_setting(key,value,updated_utc)
            VALUES('plan.stale','initial','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
    }
    var stalePlan = planner.CreatePlan(complete.Path, staleDb.DatabasePath);
    using (var conn = staleDb.OpenConnection())
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            UPDATE app_setting SET value='new-value'
            WHERE key='plan.stale';
            """;
        cmd.ExecuteNonQuery();
    }
    var planStageCount = Directory.GetFiles(root,
        "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length;
    var stalePlanRejected = false;
    try { planner.StageSettingsOnly(stalePlan, complete.Path, staleDb.DatabasePath); }
    catch (InvalidOperationException) { stalePlanRejected = true; }
    if (!stalePlanRejected ||
        Directory.GetFiles(root, "recovery-additive-staged-*.db",
            SearchOption.TopDirectoryOnly).Length != planStageCount ||
        planner.CreatePlan(complete.Path, staleDb.DatabasePath).PlanId ==
            stalePlan.PlanId)
        throw new InvalidOperationException(
            "Stale WAL-visible target plan was allowed to stage.");

    // Injected failure must roll back and leave the target untouched.
    var beforeInjected = Directory.GetFiles(root,
        "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length;
    var planInjected = false;
    try
    {
        planner.StageSettingsOnly(planned, complete.Path, previewDatabase.DatabasePath,
            simulateFailureAfterFirstInsert: true);
    }
    catch (InvalidOperationException ex)
    {
        planInjected = ex.Message == "SYNTHETIC_TEST_INJECTED_BEFORE_COMMIT";
    }
    if (!planInjected ||
        Directory.GetFiles(root, "recovery-additive-staged-*.db",
            SearchOption.TopDirectoryOnly).Length != beforeInjected ||
        planner.CreatePlan(complete.Path, previewDatabase.DatabasePath).PlanId !=
            planned.PlanId)
        throw new InvalidOperationException(
            "Planned synthetic rollback left an output or altered original.");

    // Fault injection AFTER the first complete stage write must delete
    // the generated stage, not just roll back SQL before its commit.
    var stageFilesBeforeLateFailure = Directory.GetFiles(root,
        "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length;
    var lateFailure = false;
    try
    {
        planner.StageSettingsOnly(planned, complete.Path, previewDatabase.DatabasePath,
            simulateFailureAfterStagedCopy: true);
    }
    catch (InvalidOperationException ex)
    {
        lateFailure = ex.Message == "SYNTHETIC_TEST_INJECTED_AFTER_STAGED_COPY";
    }
    if (!lateFailure ||
        Directory.GetFiles(root, "recovery-additive-staged-*.db",
            SearchOption.TopDirectoryOnly).Length != stageFilesBeforeLateFailure ||
        planner.CreatePlan(complete.Path, previewDatabase.DatabasePath).PlanId != planned.PlanId)
        throw new InvalidOperationException(
            "Late planned recovery failure left a stage or modified original.");

    // A target with a valid schema version but a broken bill foreign key
    // must be rejected before any recovery counts can be reported.
    var brokenRecoveryPaths = new AppPaths(Path.Combine(root, "broken-fk-target"));
    var brokenRecoveryDb = new SqliteDatabase(brokenRecoveryPaths);
    brokenRecoveryDb.Initialize();
    using (var brokenConn = brokenRecoveryDb.OpenConnection())
    using (var brokenCmd = brokenConn.CreateCommand())
    {
        brokenCmd.CommandText = "PRAGMA foreign_keys=OFF;";
        brokenCmd.ExecuteNonQuery();
        brokenCmd.CommandText = """
            INSERT INTO utility_bill_line(
                bill_id,section_key,description,amount_clp,created_utc,updated_utc)
            VALUES(987654321,'ELECTRICITY','Synthetic dangling FK',100,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        brokenCmd.ExecuteNonQuery();
        brokenCmd.CommandText = "PRAGMA foreign_key_check;";
        using var fk = brokenCmd.ExecuteReader();
        if (!fk.Read())
            throw new InvalidOperationException(
                "Synthetic dangling FK fixture did not violate foreign_key_check.");
    }
    var brokenPreviewRejected = false;
    try { previewer.Preview(complete.Path, brokenRecoveryDb.DatabasePath); }
    catch (InvalidDataException) { brokenPreviewRejected = true; }
    if (!brokenPreviewRejected)
        throw new InvalidOperationException("Preview accepted an inconsistent target database.");

    // A target can claim the current schema version while a required column
    // is missing. A partial comparison must not masquerade as a full PASS.
    var partialPaths = new AppPaths(Path.Combine(root, "partial-preview-schema"));
    var partialTarget = new SqliteDatabase(partialPaths);
    partialTarget.Initialize();
    using (var partialConnection = partialTarget.OpenConnection())
    using (var mutate = partialConnection.CreateCommand())
    {
        mutate.CommandText =
            "ALTER TABLE app_setting RENAME COLUMN value TO synthetic_missing_value;";
        mutate.ExecuteNonQuery();
    }
    var incomplete = previewer.Preview(complete.Path, partialTarget.DatabasePath);
    var incompletePlan = planner.CreatePlan(complete.Path, partialTarget.DatabasePath);
    if (incompletePlan.Status != "BLOCKED_INCOMPLETE_PLAN" ||
        incompletePlan.Steps.Any(step => step.Status == "SYNTHETIC_STAGE_ONLY"))
        throw new InvalidOperationException(
            "Unsupported synthetic schema was granted plan execution.");
    var blockedPlanRejected = false;
    try { planner.StageSettingsOnly(incompletePlan, complete.Path, partialTarget.DatabasePath); }
    catch (InvalidOperationException) { blockedPlanRejected = true; }
    if (!blockedPlanRejected)
        throw new InvalidOperationException(
            "Incomplete synthetic plan unexpectedly allowed staging.");
    if (incomplete.Status != "PARTIAL_PREVIEW" ||
        incomplete.Totals.BlockedCategories != 4 ||
        incomplete.Totals.ComparedCategories != 2 ||
        !incomplete.Categories.Any(c => c.Category == "SETTINGS" &&
            c.UnsupportedReason is not null) ||
        !incomplete.Disclaimer.Contains("INCOMPLETE", StringComparison.Ordinal))
        throw new InvalidOperationException(
            "Incomplete recovery category was incorrectly treated as full preview.");

    using (var unchanged = previewDatabase.OpenConnection())
    using (var check = unchanged.CreateCommand())
    {
        check.CommandText = "SELECT value FROM app_setting WHERE key='smoke.preview.conflict';";
        if (Convert.ToString(check.ExecuteScalar()) != "different-live-value")
            throw new InvalidOperationException("Read-only recovery preview mutated target.");
    }
    // Additive isolated staging without activation: v17 app_setting only.
    // Reuse the same already-marked synthetic root (never user Data).
    var additive = new IsolatedRecoveryAdditiveTestService();
    var stagedOnce = additive.ApplyToNewStagedFixture(
        complete.Path, previewDatabase.DatabasePath);
    if (stagedOnce.Status != "STAGED_SYNTHETIC_ONLY" ||
        stagedOnce.Added < 1 ||
        stagedOnce.AlreadyPresent < 1 ||
        stagedOnce.Conflicts < 1 ||
        !File.Exists(stagedOnce.StagedDatabasePath))
        throw new InvalidOperationException("Additive synthetic staging did not report expected counters.");

    using (var stagedRead = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = stagedOnce.StagedDatabasePath,
        Mode = SqliteOpenMode.ReadOnly, Pooling = false
    }.ToString()))
    {
        stagedRead.Open();
        using var check = stagedRead.CreateCommand();
        check.CommandText =
            "SELECT value FROM app_setting WHERE key='smoke.preview.source-only';";
        if (Convert.ToString(check.ExecuteScalar()) != "only-in-backup")
            throw new InvalidOperationException("Additive staging lost a missing source record.");
        check.CommandText =
            "SELECT value FROM app_setting WHERE key='smoke.preview.conflict';";
        if (Convert.ToString(check.ExecuteScalar()) != "different-live-value")
            throw new InvalidOperationException("Additive staging overwrote conflicting target values.");
    }

    // Staged additive recovery must reject the same broken foreign keys
    // without creating an output stage or changing the original fixture.
    var stagedBeforeBadTarget = Directory.GetFiles(root,
        "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length;
    var badStageRejected = false;
    try { additive.ApplyToNewStagedFixture(complete.Path, brokenRecoveryDb.DatabasePath); }
    catch (InvalidDataException) { badStageRejected = true; }
    if (!badStageRejected || Directory.GetFiles(root,
            "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length !=
        stagedBeforeBadTarget)
        throw new InvalidOperationException(
            "Additive staging accepted corrupt target or left an output stage.");

    // Running the same source again is idempotent: no double insertion.
    var stagedTwice = additive.ApplyToNewStagedFixture(
        complete.Path, stagedOnce.StagedDatabasePath);
    if (stagedTwice.Added != 0 ||
        stagedTwice.Conflicts != stagedOnce.Conflicts ||
        stagedTwice.AlreadyPresent <= stagedOnce.AlreadyPresent)
        throw new InvalidOperationException("Additive staging retry is not idempotent.");

    // Fault just after the first INSERT must roll back and remove its stage.
    var stageCountBefore = Directory.GetFiles(root,
        "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length;
    var rolledBack = false;
    try
    {
        additive.ApplyToNewStagedFixture(
            complete.Path, previewDatabase.DatabasePath,
            simulateFailureAfterFirstInsert: true);
    }
    catch (InvalidOperationException ex)
    {
        rolledBack = ex.Message == "SYNTHETIC_TEST_INJECTED_BEFORE_COMMIT";
    }
    if (!rolledBack || Directory.GetFiles(root,
        "recovery-additive-staged-*.db", SearchOption.TopDirectoryOnly).Length !=
        stageCountBefore)
        throw new InvalidOperationException("Fault injection did not cleanly roll back stage.");

    // Supplying an arbitrary path outside the synthetic fixture is rejected
    // before the API can inspect a user-owned database.
    var arbitraryTargetRejected = false;
    try
    {
        additive.ApplyToNewStagedFixture(complete.Path,
            Path.Combine(Path.GetTempPath(), "UNSAFE-user-database.db"));
    }
    catch (InvalidOperationException) { arbitraryTargetRejected = true; }
    if (!arbitraryTargetRejected)
        throw new InvalidOperationException("Additive service accepted arbitrary database path.");

    using (var unchangedAgain = previewDatabase.OpenConnection())
    using (var check = unchangedAgain.CreateCommand())
    {
        check.CommandText =
            "SELECT COUNT(*) FROM app_setting WHERE key='smoke.preview.source-only';";
        if (Convert.ToInt32(check.ExecuteScalar()) != 0)
            throw new InvalidOperationException("Original target was modified by staging.");
    }

    // A schema without a tested adapter remains explicitly unsupported.
    using (var legacyTarget = previewDatabase.OpenConnection())
    using (var lower = legacyTarget.CreateCommand())
    {
        lower.CommandText = "DELETE FROM schema_migration WHERE version=17;";
        lower.ExecuteNonQuery();
    }
    var unsupportedPreview = previewer.Preview(complete.Path, previewDatabase.DatabasePath);
    if (unsupportedPreview.Status != "UNSUPPORTED_TARGET_SCHEMA")
        throw new InvalidOperationException("Preview must reject unsupported target schema.");

    // Creating an apparently 'complete' ZIP must FAIL for an original PDF
    // referenced in SQLite but absent from the managed source directories.
    using (var missingRef = database.OpenConnection())
    using (var add = missingRef.CreateCommand())
    {
        add.CommandText = """
            INSERT INTO utility_bill_document
                (provider,original_file_name,local_pdf_path,content_sha256,
                 content_length,page_count,parser_version,extracted_text,imported_utc)
            VALUES ('TEST','missing.pdf',$path,$sha,7,1,'test',NULL,'2026-10-08T00:00:00Z');
            """;
        add.Parameters.AddWithValue("$path", Path.Combine(paths.UtilityBillDirectory, "missing.pdf"));
        add.Parameters.AddWithValue("$sha", new string('a', 64));
        add.ExecuteNonQuery();
        var missingRejected = false;
        try { completeService.Create("0.11.0-test", "synthetic", "missing-ref"); }
        catch (InvalidDataException) { missingRejected = true; }
        if (!missingRejected)
            throw new InvalidOperationException("Missing referenced original PDF was accepted.");
        add.CommandText = "DELETE FROM utility_bill_document WHERE content_sha256=$sha;";
        add.ExecuteNonQuery();
    }

    // Secondary package copy must verify byte-for-byte; no live DB access.
    var syntheticSecondary = Path.Combine(root, "other-storage", "backups");
    // A secondary copy must be outside both active application data and
    // local Backups, including nested paths and parent folders.
    if (!string.Equals(BackupDestinationPolicy.Validate(paths, syntheticSecondary),
            Path.GetFullPath(syntheticSecondary), StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Safe separate secondary path was rejected.");
    foreach (var invalidFolder in new[]
    {
        paths.DataDirectory,
        paths.BackupDirectory,
        Path.Combine(paths.DataDirectory, "subfolder"),
        Path.Combine(paths.BackupDirectory, "subfolder"),
        root
    })
    {
        var destinationBlocked = false;
        try { BackupDestinationPolicy.Validate(paths, invalidFolder); }
        catch (InvalidOperationException) { destinationBlocked = true; }
        if (!destinationBlocked)
            throw new InvalidOperationException("Secondary backup overlaps active data.");
    }
    var nestedMirrorRejected = false;
    try { completeService.CopyVerifiedToSecondary(complete.Path,
        Path.Combine(paths.DataDirectory, "secondary")); }
    catch (InvalidOperationException) { nestedMirrorRejected = true; }
    if (!nestedMirrorRejected || Directory.Exists(
            Path.Combine(paths.DataDirectory, "secondary")))
        throw new InvalidOperationException("Nested secondary backup was allowed.");

    var mirrored = completeService.CopyVerifiedToSecondary(complete.Path, syntheticSecondary);
    if (mirrored.IntegrityStatus != "PASS" ||
        !File.Exists(mirrored.Path) ||
        !string.Equals(mirrored.Sha256, complete.Sha256, StringComparison.OrdinalIgnoreCase) ||
        FullBackupService.VerifyArchive(mirrored.Path).Files.Count != fullManifest.Files.Count)
    {
        throw new InvalidOperationException("Secondary backup copy verification failed.");
    }
    if (completeService.CopyVerifiedToSecondary(complete.Path, syntheticSecondary).Path != mirrored.Path)
        throw new InvalidOperationException("Identical secondary copy must be reused safely.");

    // A mismatched existing destination can never be overwritten.
    var collisionFolder = Path.Combine(root, "secondary-collision");
    Directory.CreateDirectory(collisionFolder);
    var colliding = Path.Combine(collisionFolder, Path.GetFileName(complete.Path));
    File.WriteAllText(colliding, "OTHER BACKUP FILE: KEEP");
    var collisionRejected = false;
    try { completeService.CopyVerifiedToSecondary(complete.Path, collisionFolder); }
    catch (IOException) { collisionRejected = true; }
    if (!collisionRejected ||
        File.ReadAllText(colliding) != "OTHER BACKUP FILE: KEEP")
        throw new InvalidOperationException("Secondary collision replaced existing data.");

    // A pre-existing .inprogress file may belong to another/old attempt.
    // Failed CreateNew must preserve it; cleanup owns only new staging.
    var interruptedFolder = Path.Combine(root, "secondary-interrupted");
    Directory.CreateDirectory(interruptedFolder);
    var interim = Path.Combine(interruptedFolder,
        Path.GetFileName(complete.Path) + ".inprogress");
    File.WriteAllText(interim, "DO NOT REPLACE OR DELETE");
    var interimRejected = false;
    try { completeService.CopyVerifiedToSecondary(complete.Path, interruptedFolder); }
    catch (IOException) { interimRejected = true; }
    if (!interimRejected || !File.Exists(interim) ||
        File.ReadAllText(interim) != "DO NOT REPLACE OR DELETE")
        throw new InvalidOperationException("Secondary staging ownership regression.");

    // A single available complete copy must never be deletable.
    var blocked = false;
    try { completeService.DeleteSelectedLocal(complete.Path); }
    catch (InvalidOperationException) { blocked = true; }
    if (!blocked || !File.Exists(complete.Path))
        throw new InvalidOperationException("Last-valid-copy deletion guard failed.");

    // The historical local-only deletion API uses the SAME verified-copy
    // guard as the physical backup inventory. A corrupt second ZIP is not
    // sufficient; a separate valid ZIP may be deleted without deleting the
    // original or touching secondary destinations.
    var bogusLocal = Path.Combine(paths.BackupDirectory,
        "SolarEnergyMonitor-complete-smoke-corrupt.zip");
    File.WriteAllText(bogusLocal, "CORRUPT");
    var bogusCannotProtect = false;
    try { completeService.DeleteSelectedLocal(complete.Path); }
    catch (InvalidOperationException) { bogusCannotProtect = true; }
    if (!bogusCannotProtect || !File.Exists(complete.Path))
        throw new InvalidOperationException(
            "Legacy local deletion accepted a corrupt safety copy.");
    File.Delete(bogusLocal);

    var secondLocal = Path.Combine(paths.BackupDirectory,
        "SolarEnergyMonitor-complete-smoke-second.zip");
    File.Copy(complete.Path, secondLocal);
    completeService.DeleteSelectedLocal(secondLocal);
    if (File.Exists(secondLocal) || !File.Exists(complete.Path))
        throw new InvalidOperationException(
            "Legacy local deletion failed to preserve the valid remaining copy.");

    // A corrupted package must be rejected; original package is unchanged.
    var broken = Path.Combine(root, "truncated.zip");
    File.WriteAllBytes(broken, [1, 2, 3, 4, 5]);
    var badRejected = false;
    try { FullBackupService.VerifyArchive(broken); }
    catch (InvalidDataException) { badRejected = true; }
    if (!badRejected)
        throw new InvalidOperationException("Truncated backup was not rejected.");

    // Physical-copy inventory regression: one visible row per location,
    // no cascading deletion, last verified available copy cannot be removed.
    var inventory = new CompleteBackupInventoryService(paths);
    var bothLocations = inventory.List(syntheticSecondary);
    if (bothLocations.SecondaryWarning is not null ||
        bothLocations.Copies.Count(c => c.Kind == "COMPLETE") != 2 ||
        bothLocations.Copies.Count(c => c.Location == "SECONDARY") != 1 ||
        !bothLocations.Copies.Any(c => c.Kind == "LEGACY_SQLITE_ONLY"))
        throw new InvalidOperationException("Physical backup inventory classification failed.");
    var localPhysical = bothLocations.Copies.Single(c =>
        c.Kind == "COMPLETE" && c.Location == "LOCAL");
    var secondaryPhysical = bothLocations.Copies.Single(c =>
        c.Kind == "COMPLETE" && c.Location == "SECONDARY");
    // A finished backup-management workflow must reveal trustworthy
    // content categories and issue a portable, non-restorative receipt.
    var detailedBackup = inventory.VerifyDetails(localPhysical, syntheticSecondary);
    var spanishReceipt = detailedBackup.FormatReceipt(spanish: true);
    var englishReceipt = detailedBackup.FormatReceipt(spanish: false);
    if (detailedBackup.Summary.IntegrityStatus != "PASS" ||
        detailedBackup.Location != "LOCAL" ||
        detailedBackup.FileName != localPhysical.Name ||
        detailedBackup.DatabaseFiles != 1 ||
        detailedBackup.DatabaseBytes <= 0 ||
        detailedBackup.BillDocuments < 1 ||
        detailedBackup.TariffDocuments < 1 ||
        detailedBackup.BillBytes <= 0 || detailedBackup.TariffBytes <= 0 ||
        detailedBackup.Summary.FileCount !=
            detailedBackup.DatabaseFiles + detailedBackup.BillDocuments +
            detailedBackup.TariffDocuments ||
        detailedBackup.Summary.Sha256.Length != 64 ||
        !spanishReceipt.Contains("restauración NO probada", StringComparison.Ordinal) ||
        !spanishReceipt.Contains(detailedBackup.Summary.Sha256, StringComparison.Ordinal) ||
        !spanishReceipt.Contains("Esquema SQLite: 17", StringComparison.Ordinal) ||
        !englishReceipt.Contains("restoration NOT tested", StringComparison.Ordinal) ||
        !englishReceipt.Contains("Bills:", StringComparison.Ordinal) ||
        spanishReceipt.Contains("Synthetic bill proof", StringComparison.Ordinal) ||
        englishReceipt.Contains("Synthetic tariff proof", StringComparison.Ordinal))
        throw new InvalidOperationException(
            "Verified backup details or bilingual receipt contained missing/untrusted information.");

    if (inventory.Verify(secondaryPhysical, syntheticSecondary).IntegrityStatus != "PASS")
        throw new InvalidOperationException("Secondary copy integrity verification failed.");
    // No one may pair one backup path with a different display name and
    // obtain a successful verification or authorize that misleading selection.
    var misleadingSelection = secondaryPhysical with
    {
        Name = "SolarEnergyMonitor-complete-other.zip"
    };
    var misleadingVerifyBlocked = false;
    try { inventory.Verify(misleadingSelection, syntheticSecondary); }
    catch (InvalidOperationException) { misleadingVerifyBlocked = true; }
    if (!misleadingVerifyBlocked)
        throw new InvalidOperationException(
            "Inventory allowed a backup path/name identity mismatch.");

    // A user selection from an obsolete inventory cannot delete a changed ZIP.
    // No owner backups are used: only the two temporary synthetic copies.
    var wrongSize = secondaryPhysical with { SizeBytes = secondaryPhysical.SizeBytes + 1 };
    var staleVerifyBlocked = false;
    try { inventory.Verify(wrongSize, syntheticSecondary); }
    catch (InvalidOperationException) { staleVerifyBlocked = true; }
    if (!staleVerifyBlocked)
        throw new InvalidOperationException(
            "Stale inventory selection was falsely reverified as PASS.");
    var staleSizeBlocked = false;
    try { inventory.DeleteOne(wrongSize, syntheticSecondary); }
    catch (InvalidOperationException) { staleSizeBlocked = true; }
    if (!staleSizeBlocked || !File.Exists(mirrored.Path))
        throw new InvalidOperationException("Stale-size deletion guard failed.");

    var oldModified = File.GetLastWriteTimeUtc(mirrored.Path);
    File.SetLastWriteTimeUtc(mirrored.Path, oldModified.AddMinutes(5));
    var staleReverifyBlocked = false;
    try { inventory.Verify(secondaryPhysical, syntheticSecondary); }
    catch (InvalidOperationException) { staleReverifyBlocked = true; }
    if (!staleReverifyBlocked)
        throw new InvalidOperationException(
            "Changed backup timestamp was falsely reverified as PASS.");
    var staleTimeBlocked = false;
    try { inventory.DeleteOne(secondaryPhysical, syntheticSecondary); }
    catch (InvalidOperationException) { staleTimeBlocked = true; }
    if (!staleTimeBlocked || !File.Exists(mirrored.Path))
        throw new InvalidOperationException("Stale-timestamp deletion guard failed.");

    // Explicitly refresh inventory, then delete only that currently selected
    // secondary package. The local verified package remains untouched.
    var latestSecondary = inventory.List(syntheticSecondary).Copies.Single(c =>
        c.Kind == "COMPLETE" && c.Location == "SECONDARY");
    inventory.DeleteOne(latestSecondary, syntheticSecondary);
    if (File.Exists(mirrored.Path) || !File.Exists(complete.Path))
        throw new InvalidOperationException("Independent secondary delete cascaded or failed.");

    // With only the local full package left, deletion must be blocked.
    var finalCopyBlocked = false;
    try { inventory.DeleteOne(localPhysical, syntheticSecondary); }
    catch (InvalidOperationException) { finalCopyBlocked = true; }
    if (!finalCopyBlocked || !File.Exists(complete.Path))
        throw new InvalidOperationException("Final copy protection regression.");

    // Offline secondary must not be treated as a currently available safety copy.
    var unseen = inventory.List(Path.Combine(root, "offline-secondary-location"));
    if (unseen.SecondaryWarning is null)
        throw new InvalidOperationException("Offline secondary location not reported.");

    // Unknown files and paths outside configured backup destinations are never deleted.
    var unrelated = Path.Combine(root, "user-unrelated.zip");
    File.WriteAllText(unrelated, "NEVER DELETE");
    var unrecognizedBlocked = false;
    try
    {
        inventory.DeleteOne(localPhysical with { Path = unrelated }, syntheticSecondary);
    }
    catch (InvalidOperationException) { unrecognizedBlocked = true; }
    if (!unrecognizedBlocked || !File.Exists(unrelated))
        throw new InvalidOperationException("Unrecognized backup deletion allowed.");

    EnelBrowserCaptureTelemetry.Record(
        paths, "ZERO_CLICK_RANGE", "INCOMPLETE", 206, 1024);
    var evidenceZip = new PhaseDiagnosticsExportService(database, paths)
        .Export();
    using (var evidenceArchive = System.IO.Compression.ZipFile.OpenRead(
        evidenceZip))
    {
        var expected = new[]
        {
            "overview.json", "schema_inventory.json",
            "README.txt", "manifest.json",
            "enel_capture_events.json", "schema_columns.csv",
            "schema_foreign_keys.csv", "bill_reconciliation.json"
        };
        if (expected.Any(name => evidenceArchive.GetEntry(name) is null) ||
            evidenceArchive.Entries.Count != expected.Length)
        {
            throw new InvalidOperationException(
                "Phase 10-12 sanitized debug bundle smoke test failed.");
        }
    }

    // Regression: a real owner Data\ folder is currently schema v13.
    // Verify the in-place v13 -> v16 migration preserves existing tariff
    // evidence, creates the regulatory relation graph metadata and extends
    // canonical bill summary storage without losing legacy rows.
    var migration13Root = Path.Combine(
        root,
        "schema13-upgrade");
    var migration13Paths =
        new AppPaths(migration13Root);
    Directory.CreateDirectory(
        Path.GetDirectoryName(
            migration13Paths.DatabasePath)!);

    using (var legacyConnection =
           new SqliteConnection(
               new SqliteConnectionStringBuilder
               {
                   DataSource =
                       migration13Paths.DatabasePath
               }.ToString()))
    {
        legacyConnection.Open();

        using var create =
            legacyConnection.CreateCommand();
        create.CommandText = """
            CREATE TABLE schema_migration (
                version INTEGER PRIMARY KEY,
                applied_utc TEXT NOT NULL,
                description TEXT NOT NULL
            );
            INSERT INTO schema_migration(
                version, applied_utc, description
            )
            VALUES(
                13, '2026-10-06T00:00:00Z', 'schema13 fixture'
            );

            CREATE TABLE tariff_publication (
                publication_id INTEGER PRIMARY KEY AUTOINCREMENT,
                provider TEXT NOT NULL,
                category TEXT NOT NULL,
                title TEXT NOT NULL,
                source_url TEXT NOT NULL UNIQUE,
                effective_from TEXT NULL,
                is_retroactive INTEGER NOT NULL DEFAULT 0,
                local_pdf_path TEXT NULL,
                content_sha256 TEXT NULL,
                content_length INTEGER NULL,
                page_count INTEGER NULL,
                capture_status TEXT NOT NULL,
                captured_utc TEXT NULL,
                updated_utc TEXT NOT NULL,
                normalization_status TEXT NOT NULL DEFAULT 'NOT_NORMALIZED',
                normalization_parser_version TEXT NULL,
                normalized_utc TEXT NULL
            );

            CREATE TABLE utility_bill (
                bill_id INTEGER PRIMARY KEY AUTOINCREMENT,
                period_start_utc TEXT NOT NULL,
                period_end_utc TEXT NOT NULL,
                billed_consumption_kwh REAL NULL,
                amount_clp REAL NULL,
                invoice_reference TEXT NULL,
                notes TEXT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                from_reading_id INTEGER NULL,
                to_reading_id INTEGER NULL,
                meter_start_kwh REAL NULL,
                meter_end_kwh REAL NULL,
                tariff_plan TEXT NULL,
                taxable_amount_clp REAL NULL,
                iva_clp REAL NULL,
                exempt_amount_clp REAL NULL,
                gross_bill_amount_clp REAL NULL,
                other_charges_clp REAL NULL,
                total_due_clp REAL NULL,
                period_precision TEXT NOT NULL DEFAULT 'EXACT'
            );

            CREATE TABLE utility_bill_line (
                bill_line_id INTEGER PRIMARY KEY AUTOINCREMENT,
                bill_id INTEGER NOT NULL,
                section_key TEXT NOT NULL,
                category_key TEXT NULL,
                description TEXT NOT NULL,
                quantity REAL NULL,
                unit TEXT NULL,
                unit_rate_clp REAL NULL,
                amount_clp REAL NOT NULL,
                tax_treatment TEXT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            INSERT INTO tariff_publication(
                provider, category, title, source_url,
                effective_from, is_retroactive,
                capture_status, updated_utc,
                normalization_status
            )
            VALUES(
                'ENEL_DISTRIBUCION_CHILE',
                'SUPPLY_REGULATED',
                'Existing v13 tariff evidence',
                'https://example.invalid/v13-tariff.pdf',
                '2026-09-01',
                0,
                'CAPTURED',
                '2026-10-06T00:00:00Z',
                'CANDIDATES_EXTRACTED'
            );
            """;
        create.ExecuteNonQuery();
    }

    var migration13Database =
        new SqliteDatabase(
            migration13Paths);
    migration13Database.Initialize();

    if (migration13Database.GetSchemaVersion() != 17)
    {
        throw new InvalidOperationException(
            "Schema v13 -> v17 migration did not reach version 17.");
    }

    var migratedTariffRepository =
        new TariffPublicationRepository(
            migration13Database);
    var migratedTariffs =
        migratedTariffRepository.GetAll();

    if (migratedTariffs.Count != 1 ||
        migratedTariffs[0].Title !=
            "Existing v13 tariff evidence" ||
        migratedTariffs[0].OfficialDocumentNumber is not null ||
        migratedTariffRepository.GetRelations().Count != 0)
    {
        throw new InvalidOperationException(
            "Schema v13 -> v16 migration did not preserve prior tariff evidence cleanly.");
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

    // Cancellation is honored before computation and before creating an
    // export file; no output should be created by a cancelled request.
    using (var cancelledReport = new CancellationTokenSource())
    {
        cancelledReport.Cancel();
        var cancelledBuildRejected = false;
        try { reportExporter.Build(reportData.Request, cancelledReport.Token); }
        catch (OperationCanceledException) { cancelledBuildRejected = true; }
        if (!cancelledBuildRejected)
            throw new InvalidOperationException("Cancelled report Build still ran.");
        var cancelPower = false;
        try { powerAggregation.GetSeries(familySmokeDeviceId, "pv_power_w",
            familyLocalStart.ToUniversalTime(), familyLocalEnd.ToUniversalTime(),
            "America/Santiago", AggregationPeriod.Day, cancelledReport.Token); }
        catch (OperationCanceledException) { cancelPower = true; }
        var cancelSoc = false;
        try { socAggregation.GetSeries(familySmokeDeviceId,
            familyLocalStart.ToUniversalTime(), familyLocalEnd.ToUniversalTime(),
            "America/Santiago", AggregationPeriod.Day, cancelledReport.Token); }
        catch (OperationCanceledException) { cancelSoc = true; }
        var cancelTable = false;
        try { reportAggregation.Get(familySmokeDeviceId,
            familyLocalStart.ToUniversalTime(), familyLocalEnd.ToUniversalTime(),
            "America/Santiago", AggregationPeriod.Day, cancelledReport.Token); }
        catch (OperationCanceledException) { cancelTable = true; }
        if (!cancelPower || !cancelSoc || !cancelTable)
            throw new InvalidOperationException("Deep report aggregation cancellation regressed.");

        var cancelledOutput = Path.Combine(root, "cancelled-report.xlsx");
        var cancelledOutputRejected = false;
        try { reportExporter.ExportExcel(cancelledOutput, reportData, cancelledReport.Token); }
        catch (OperationCanceledException) { cancelledOutputRejected = true; }
        if (!cancelledOutputRejected || File.Exists(cancelledOutput))
            throw new InvalidOperationException("Cancelled Excel report wrote an output file.");

        if (OperatingSystem.IsWindows())
        {
            var cancelledPdf = Path.Combine(root, "cancelled-report.pdf");
            var cancelledPdfRejected = false;
            try { reportExporter.ExportPdf(cancelledPdf, reportData, cancelledReport.Token); }
            catch (OperationCanceledException) { cancelledPdfRejected = true; }
            if (!cancelledPdfRejected || File.Exists(cancelledPdf))
                throw new InvalidOperationException("Cancelled PDF report wrote an output file.");
        }
    }

    // Operational timings must stay short, bounded and free of sensitive
    // payloads. Report timing categories have only constant event names.
    var reportTimer = new UiPerformanceRecorder();
    reportTimer.Record("Data.Reports.Build", TimeSpan.FromMilliseconds(123));
    reportTimer.Record("Data.Reports.PdfRender", TimeSpan.FromMilliseconds(456));
    reportTimer.Record("Data.Reports.Publish", TimeSpan.FromMilliseconds(3));
    var reportTimingSamples = reportTimer.Snapshot();
    if (reportTimingSamples.Count != 3 ||
        reportTimer.Summaries().Any(x => x.Samples != 1) ||
        reportTimingSamples.Any(x => !x.Operation.StartsWith("Data.Reports.", StringComparison.Ordinal)))
        throw new InvalidOperationException("Report performance instrumentation regressed.");

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
        utilityStatistics,
        new UtilityBillGapStatisticalCompletionService(
            database));
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
            familySmokeDeviceId,
            "America/Santiago");
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
        TariffCatalogProbeResult? liveCatalog = null;
        try
        {
            liveCatalog =
                await EnelTariffCaptureService
                    .ProbeOfficialCatalogAsync(2026);

            Console.WriteLine(
                $"Live Enel catalog probe: HTTP {liveCatalog.HttpStatusCode}; " +
                $"{liveCatalog.HtmlLength:N0} chars; " +
                $"{liveCatalog.PdfHrefCount} PDF href(s); " +
                $"{liveCatalog.DiscoveredPublications} supply publication(s) for {liveCatalog.Year}.");

            if (liveCatalog.DiscoveredPublications == 0)
            {
                Console.WriteLine(
                    "Live Enel catalog is currently protected/opaque; " +
                    "continuing with direct static-asset validation.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Live Enel catalog unavailable ({ex.Message}); " +
                "continuing with direct static-asset validation.");
        }

        const string livePdfProbeUrl =
            "https://www.enel.cl/content/dam/enel-cl/es/personas/informacion-de-utilidad/" +
            "tarifas-y-reglamentos/tarifas/tarifas-reguladas/2026/" +
            "Enel%20Distribuci%C3%B3n%20Chile%20SA._Tarifas%20Suministro%20El%C3%A9ctrico%208T_" +
            "%20VAD%205T%20Septiembre%20de%202026.pdf";

        const string livePdfTitle =
            "Enel Distribución Chile SA._Tarifas Suministro Eléctrico 8T_ VAD 5T Septiembre de 2026.pdf";

        var livePdf =
            await EnelTariffCaptureService
                .ProbeOfficialPdfAsync(
                    livePdfProbeUrl);

        Console.WriteLine(
            $"Live Enel direct PDF probe: " +
            $"{livePdf.ContentLength:N0} bytes; " +
            $"SHA-256 {livePdf.Sha256}.");

        if (livePdf.ContentLength < 100_000)
        {
            throw new InvalidOperationException(
                "Live Enel direct PDF probe returned an implausibly small document.");
        }

        var liveEnelRepository =
            new TariffPublicationRepository(database);

        // Seed one official filename family exactly as an installation with
        // prior tariff evidence would have. The resilient pipeline must then
        // work even when the live HTML catalog is blocked by Imperva.
        var seededSeptemberId =
            liveEnelRepository.UpsertDiscovery(
                new TariffPublicationDiscovery(
                    "ENEL_DISTRIBUCION_CHILE",
                    "SUPPLY_REGULATED",
                    livePdfTitle,
                    livePdfProbeUrl,
                    new DateOnly(2026, 9, 1),
                    false,
                    RegulatoryMetadataSource:
                        "LIVE_SMOKE_KNOWN_OFFICIAL_ASSET"));

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
            $"Live Enel resilient capture: " +
            $"{liveEnelResult.Captured}/{liveEnelResult.Discovered} captured; " +
            $"{liveEnelResult.Failed} download/capture failure(s); " +
            $"{liveEnelResult.NormalizedCandidates} normalized candidate(s); " +
            $"{liveEnelResult.NormalizationFailures} normalization failure(s); " +
            $"{liveEnelResult.RetroactiveDetected} retroactive publication(s); " +
            $"{liveEnelResult.MultiVersionPeriods} multi-version period(s).");

        foreach (var message in liveEnelResult.Messages)
        {
            Console.WriteLine(
                $"Live Enel capture detail: {message}");
        }

        var seededSeptember =
            liveEnelRepository
                .GetAll()
                .Single(item =>
                    item.PublicationId ==
                    seededSeptemberId);

        if (!string.Equals(
                seededSeptember.CaptureStatus,
                "CAPTURED",
                StringComparison.Ordinal) ||
            !string.Equals(
                seededSeptember.ContentSha256,
                livePdf.Sha256,
                StringComparison.OrdinalIgnoreCase) ||
            liveEnelResult.Captured < 1 ||
            liveEnelResult.NormalizedCandidates < 1)
        {
            throw new InvalidOperationException(
                "Live Enel resilient pipeline did not capture and normalize " +
                "the known official September static asset while the catalog " +
                "was unavailable.");
        }

        if (liveCatalog is not null &&
            liveCatalog.DiscoveredPublications == 0 &&
            !liveEnelResult.Messages.Any(message =>
                message.Contains(
                    "content/dam",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Live Enel catalog was blocked but the resilient capture did " +
                "not report use of direct content/dam discovery.");
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

        var liveCnePublications =
            liveCneRepository
                .GetAll()
                .Where(item =>
                    item.Provider == "CNE_CHILE" &&
                    item.Category == "VAD_INDEX" &&
                    !string.Equals(
                        item.RegulatoryMetadataSource,
                        "SMOKE",
                        StringComparison.Ordinal))
                .ToArray();
        var liveCneRelations =
            liveCneRepository.GetRelations();

        foreach (var item in liveCnePublications
                     .Where(item =>
                         item.EffectiveFrom ==
                         new DateOnly(2026, 8, 1))
                     .OrderBy(item => item.PublicationId))
        {
            Console.WriteLine(
                $"Live CNE August metadata: id={item.PublicationId}; " +
                $"official={item.OfficialDocumentNumber ?? "-"}; " +
                $"officialDate={item.OfficialPublicationDate?.ToString("yyyy-MM-dd") ?? "-"}; " +
                $"corrects={item.CorrectsOfficialDocumentNumber ?? "-"}; " +
                $"retroactive={item.IsRetroactive}; title={item.Title}");
        }

        foreach (var relation in liveCneRelations)
        {
            Console.WriteLine(
                $"Live CNE relation: source={relation.SourcePublicationId}; " +
                $"type={relation.RelationType}; " +
                $"targetOfficial={relation.TargetOfficialDocumentNumber}; " +
                $"targetId={relation.TargetPublicationId?.ToString() ?? "-"}; " +
                $"evidence={relation.EvidenceText}");
        }

        var live380 =
            liveCnePublications.SingleOrDefault(item =>
                string.Equals(
                    item.OfficialDocumentNumber,
                    "REX-380-2026",
                    StringComparison.OrdinalIgnoreCase));
        var live368 =
            liveCnePublications.SingleOrDefault(item =>
                string.Equals(
                    item.OfficialDocumentNumber,
                    "REX-368-2026",
                    StringComparison.OrdinalIgnoreCase));

        var liveAugustResolution =
            new TariffPublicationVersionResolver()
                .Resolve(
                    liveCnePublications
                        .Where(item =>
                            item.EffectiveFrom ==
                            new DateOnly(2026, 8, 1))
                        .ToArray(),
                    liveCneRelations);

        if (liveCneResult.CapturedVadIndexDocuments < 10 ||
            liveCneResult.Corrections < 1 ||
            live380 is null ||
            live368 is null ||
            live380.OfficialPublicationDate !=
                new DateOnly(2026, 7, 24) ||
            live368.OfficialPublicationDate !=
                new DateOnly(2026, 7, 17) ||
            !liveCneRelations.Any(relation =>
                relation.SourcePublicationId ==
                    live380.PublicationId &&
                relation.RelationType == "CORRECTS" &&
                string.Equals(
                    relation.TargetOfficialDocumentNumber,
                    "REX-368-2026",
                    StringComparison.OrdinalIgnoreCase)) ||
            !liveAugustResolution.Any(item =>
                item.PublicationId ==
                    live380.PublicationId &&
                item.Status ==
                    "VERSION_PREFERRED_OFFICIAL_CORRECTION") ||
            !liveAugustResolution.Any(item =>
                item.PublicationId ==
                    live368.PublicationId &&
                item.Status ==
                    "VERSION_SUPERSEDED_BY_OFFICIAL_CORRECTION"))
        {
            throw new InvalidOperationException(
                "Live CNE source did not prove the expected official " +
                "REX-380-2026 -> REX-368-2026 correction graph.");
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
                "Resolución Exenta CNE N° 9368 · índices VAD 2026-08",
                "https://www.cne.cl/smoke/rex-9368-2026.pdf",
                new DateOnly(2026, 8, 1),
                false,
                "REX-9368-2026",
                new DateOnly(2026, 7, 17),
                null,
                "SMOKE"));

    var cneCorrectionId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "CNE_CHILE",
                "VAD_INDEX",
                "Resolución Exenta CNE N° 9380 · índices VAD 2026-08 · rectificación",
                "https://www.cne.cl/smoke/rex-9380-2026.pdf",
                new DateOnly(2026, 8, 1),
                true,
                "REX-9380-2026",
                new DateOnly(2026, 7, 24),
                "REX-9368-2026",
                "SMOKE"));

    tariffRepository.UpsertRelation(
        new TariffPublicationRelationUpsert(
            cneCorrectionId,
            "CORRECTS",
            "CNE_CHILE",
            "VAD_INDEX",
            "REX-9368-2026",
            "https://www.cne.cl/smoke/rex-9380-2026.pdf",
            "REX-9380-2026 CORRECTS REX-9368-2026"));
    tariffRepository.ResolveRelationTargets();

    var cnePublications = tariffRepository
        .GetAll()
        .Where(item =>
            item.Provider == "CNE_CHILE" &&
            item.Category == "VAD_INDEX" &&
            item.EffectiveFrom ==
                new DateOnly(2026, 8, 1) &&
            item.OfficialDocumentNumber is
                "REX-9368-2026" or "REX-9380-2026")
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


    const double smokeElectricityRate = 176.2788;
    const double smokeTransportRate = 13.415;
    const double smokePreservedCharges = 1000.0;

    var summaryElectricityAmount =
        expectedGridImport *
        smokeElectricityRate;
    var summaryTransportAmount =
        expectedGridImport *
        smokeTransportRate;
    var summaryBillTotal =
        summaryElectricityAmount +
        summaryTransportAmount +
        smokePreservedCharges;

    var summaryBillId = utilityRepository.AddBill(
        utilityFromUtc,
        utilityToUtc,
        expectedGridImport,
        summaryBillTotal,
        "PHASE10-SUMMARY-SMOKE",
        "Observed bill reconciliation summary",
        tariffPlan: "BT1-SMOKE",
        totalDueClp: summaryBillTotal,
        periodPrecision:
            UtilityTimePrecision.Exact);

    utilityRepository.AddBillLine(
        summaryBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        summaryElectricityAmount,
        categoryKey: "ELECTRICITY_CONSUMED",
        quantity: expectedGridImport,
        unit: "kWh",
        unitRateClp: smokeElectricityRate,
        taxTreatment: "AFECTO",
        sortOrder: 10);

    utilityRepository.AddBillLine(
        summaryBillId,
        "SERVICIO_ELECTRICO",
        "Transporte de electricidad",
        summaryTransportAmount,
        categoryKey: "ELECTRICITY_TRANSPORT",
        quantity: expectedGridImport,
        unit: "kWh",
        unitRateClp: smokeTransportRate,
        taxTreatment: "AFECTO",
        sortOrder: 20);

    utilityRepository.AddBillLine(
        summaryBillId,
        "OTROS_CARGOS",
        "Cargo preservado smoke",
        smokePreservedCharges,
        categoryKey: "CUSTOM_PRESERVED",
        sortOrder: 30);

    var productBillSummary =
        new UtilityBillReconciliationSummaryService(
            utilityRepository,
            utilityReconciliation,
            new UtilityBillGapStatisticalCompletionService(
                database),
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            familySmokeDeviceId,
            summaryBillId,
            "America/Santiago");

    if (!productBillSummary.HasObservedEconomicEstimate ||
        productBillSummary.ActualBillTotalClp is null ||
        Math.Abs(
            productBillSummary.ActualBillTotalClp.Value -
            summaryBillTotal) > 0.01 ||
        productBillSummary.BilledMinusObservedKwh is null ||
        Math.Abs(
            productBillSummary.BilledMinusObservedKwh.Value) > 0.000001 ||
        productBillSummary.EstimatedObservedTotalClp is null ||
        Math.Abs(
            productBillSummary.EstimatedObservedTotalClp.Value -
            summaryBillTotal) > 0.01 ||
        productBillSummary.ActualMinusEstimatedObservedClp is null ||
        Math.Abs(
            productBillSummary.ActualMinusEstimatedObservedClp.Value) > 0.01 ||
        productBillSummary.PreservedNonVariableClp is null ||
        Math.Abs(
            productBillSummary.PreservedNonVariableClp.Value -
            smokePreservedCharges) > 0.01 ||
        productBillSummary.CoveragePercent < 95)
    {
        throw new InvalidOperationException(
            "Phase 10 observed bill-reconciliation summary smoke test failed.");
    }


    // Phase 10 closure: reproduce the actual 2026 Enel bill grouping:
    // Administration = monthly fixed charge; Transport line can include the
    // tax-exempt public-service charge. The public-service amount is also
    // independently cross-checked against the printed exempt subtotal.
    const double smokeAdminBilledKwh = 10.0;
    const double smokeAdminObservedKwh = 8.0;
    const double smokeAdminFixedPrinted = 710.0;
    const double smokeAdminPublicServiceRate = 0.855;
    const double smokeAdminTransportRate = 15.964;
    const double smokeAdminElectricityRate = 176.2788;

    var adminBillStart =
        new DateTimeOffset(
            2026, 1, 10, 0, 0, 0,
            TimeSpan.FromHours(-3))
        .ToUniversalTime();
    var adminBillEnd =
        new DateTimeOffset(
            2026, 1, 20, 0, 0, 0,
            TimeSpan.FromHours(-3))
        .ToUniversalTime();

    var adminElectricityAmount =
        smokeAdminBilledKwh *
        smokeAdminElectricityRate;
    var adminTransportAmount =
        smokeAdminBilledKwh *
        (smokeAdminTransportRate +
         smokeAdminPublicServiceRate);
    var adminExemptAmount =
        smokeAdminBilledKwh *
        smokeAdminPublicServiceRate;

    var adminBillId = utilityRepository.AddBill(
        adminBillStart,
        adminBillEnd,
        smokeAdminBilledKwh,
        adminElectricityAmount +
        adminTransportAmount +
        smokeAdminFixedPrinted,
        "PHASE10-ADMIN-SMOKE",
        "Fixed administration plus transport/public-service reconstruction",
        tariffPlan: "BT1-T1",
        exemptAmountClp: adminExemptAmount,
        totalDueClp:
            adminElectricityAmount +
            adminTransportAmount +
            smokeAdminFixedPrinted,
        periodPrecision:
            UtilityTimePrecision.DateOnly);

    utilityRepository.AddBillLine(
        adminBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        adminElectricityAmount,
        categoryKey:
            UtilityBillLineCategory.ElectricityConsumed,
        sortOrder: 10);

    var adminTransportLineId =
        utilityRepository.AddBillLine(
            adminBillId,
            "SERVICIO_ELECTRICO",
            "Transporte de electricidad",
            adminTransportAmount,
            categoryKey:
                UtilityBillLineCategory.ElectricityTransport,
            sortOrder: 20);

    var adminLineId = utilityRepository.AddBillLine(
        adminBillId,
        "SERVICIO_ELECTRICO",
        "Administración del servicio",
        smokeAdminFixedPrinted,
        categoryKey:
            UtilityBillLineCategory.ServiceAdministration,
        sortOrder: 30);

    var adminAnalysis =
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver)
        .Analyze(
            adminBillId,
            "America/Santiago",
            UtilityGridImportStatisticalCompletion
                .Insufficient(
                    smokeAdminObservedKwh,
                    100.0,
                    0,
                    0,
                    "PHASE10-ADMIN-SMOKE"));

    var adminComponent =
        adminAnalysis.Components.SingleOrDefault(item =>
            item.ComponentKey ==
            UtilityBillLineCategory.ServiceAdministration);
    var transportComponent =
        adminAnalysis.Components.SingleOrDefault(item =>
            item.ComponentKey ==
            "ELECTRICITY_TRANSPORT_PLUS_PUBLIC_SERVICE");
    var adminObservedScenario =
        adminAnalysis.Scenarios.Single(item =>
            item.Key == "SOLAR_OBSERVED");

    var expectedAdminObservedSubtotal =
        smokeAdminFixedPrinted +
        smokeAdminObservedKwh *
        (smokeAdminElectricityRate +
         smokeAdminTransportRate +
         smokeAdminPublicServiceRate);

    if (!adminAnalysis.HasTariffModel ||
        adminComponent is null ||
        transportComponent is null ||
        Math.Abs(
            adminComponent.FixedAmountClp -
            smokeAdminFixedPrinted) > 0.0001 ||
        Math.Abs(
            adminComponent.RateClpPerKwh) > 0.000001 ||
        Math.Abs(
            adminComponent.ReconstructedAmountClp -
            smokeAdminFixedPrinted) > 0.001 ||
        string.IsNullOrWhiteSpace(
            adminComponent.CalculationBasis) ||
        !adminComponent.CalculationBasis.Contains(
            "Cargo fijo mensual",
            StringComparison.OrdinalIgnoreCase) ||
        Math.Abs(
            transportComponent.RateClpPerKwh -
            (smokeAdminTransportRate +
             smokeAdminPublicServiceRate)) > 0.000001 ||
        string.IsNullOrWhiteSpace(
            transportComponent.CalculationBasis) ||
        !transportComponent.CalculationBasis.Contains(
            "servicio público",
            StringComparison.OrdinalIgnoreCase) ||
        !transportComponent.CalculationBasis.Contains(
            "exento impreso",
            StringComparison.OrdinalIgnoreCase) ||
        adminAnalysis.SupportedFixedAmountClp is null ||
        Math.Abs(
            adminAnalysis.SupportedFixedAmountClp.Value -
            smokeAdminFixedPrinted) > 0.0001 ||
        Math.Abs(
            adminObservedScenario.SupportedTariffSubtotalClp -
            expectedAdminObservedSubtotal) > 0.001)
    {
        throw new InvalidOperationException(
            "Phase 10 real-structure administration/transport reconstruction smoke test failed.");
    }

    var adminAudit =
        new UtilityBillAuditV2Service(
            utilityRepository,
            rateVerification,
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            adminBillId,
            "America/Santiago");

    var adminAuditLine =
        adminAudit.Lines.Single(item =>
            item.BillLineId == adminLineId);
    var transportAuditLine =
        adminAudit.Lines.Single(item =>
            item.BillLineId == adminTransportLineId);

    if (!adminAuditLine.ReconstructedAmountClp.HasValue ||
        Math.Abs(
            adminAuditLine.ReconstructedAmountClp.Value -
            smokeAdminFixedPrinted) > 0.001 ||
        string.IsNullOrWhiteSpace(
            adminAuditLine.CalculationDetail) ||
        !adminAuditLine.CalculationDetail.Contains(
            "fijo",
            StringComparison.OrdinalIgnoreCase) ||
        !transportAuditLine.ReconstructedAmountClp.HasValue ||
        string.IsNullOrWhiteSpace(
            transportAuditLine.CalculationDetail))
    {
        throw new InvalidOperationException(
            "Phase 10 administration/transport audit-basis smoke test failed.");
    }

    // Phase 10 closure: a single bill crosses two effective tariff
    // publications with genuinely different variable rates while the monthly
    // fixed charge remains whole-peso equivalent across the periods.
    var mayFixture =
        tariffPageFixture;
    var juneFixture =
        tariffPageFixture
            .Replace(
                "0,855 0,000 0,855 0,000",
                "0,955 0,000 0,955 0,000",
                StringComparison.Ordinal)
            .Replace(
                "131,039 155,936 131,039 155,936",
                "141,039 155,936 141,039 155,936",
                StringComparison.Ordinal)
            .Replace(
                "176,2788 209,772 176,2788 209,772",
                "186,2788 209,772 186,2788 209,772",
                StringComparison.Ordinal);

    var mayPublicationId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                "Smoke Tarifas Mayo 2026",
                "https://example.invalid/enel-may-2026.pdf",
                new DateOnly(2026, 5, 1),
                false));
    tariffRepository.MarkCaptured(
        mayPublicationId,
        Path.Combine(
            paths.TariffDirectory,
            "smoke-may.pdf"),
        "may-phase10",
        1234,
        1,
        [mayFixture]);
    tariffNormalizer.NormalizePublication(
        mayPublicationId,
        tariffRepository.GetPageTexts(
            mayPublicationId));

    var junePublicationId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                "Smoke Tarifas Junio 2026",
                "https://example.invalid/enel-june-2026.pdf",
                new DateOnly(2026, 6, 1),
                false));
    tariffRepository.MarkCaptured(
        junePublicationId,
        Path.Combine(
            paths.TariffDirectory,
            "smoke-june.pdf"),
        "june-phase10",
        1234,
        1,
        [juneFixture]);
    tariffNormalizer.NormalizePublication(
        junePublicationId,
        tariffRepository.GetPageTexts(
            junePublicationId));

    const double multiBilledKwh = 100.0;
    const double multiObservedKwh = 80.0;
    const double mayElectricityRate = 176.2788;
    const double juneElectricityRate = 186.2788;
    const double mayServiceRate = 0.855;
    const double juneServiceRate = 0.955;
    const double multiTransportRate = 15.964;
    const int mayDays = 12;
    const int juneDays = 10;
    const int multiDays = mayDays + juneDays;

    var multiElectricityRate =
        (mayElectricityRate * mayDays +
         juneElectricityRate * juneDays) /
        multiDays;
    var multiServiceRate =
        (mayServiceRate * mayDays +
         juneServiceRate * juneDays) /
        multiDays;

    var multiElectricityAmount =
        multiBilledKwh *
        multiElectricityRate;
    var multiTransportAmount =
        multiBilledKwh *
        (multiTransportRate +
         multiServiceRate);
    var multiExemptAmount =
        multiBilledKwh *
        multiServiceRate;

    var multiBillId =
        utilityRepository.AddBill(
            new DateTimeOffset(
                2026, 5, 20, 0, 0, 0,
                TimeSpan.FromHours(-4))
                .ToUniversalTime(),
            new DateTimeOffset(
                2026, 6, 10, 0, 0, 0,
                TimeSpan.FromHours(-4))
                .ToUniversalTime(),
            multiBilledKwh,
            multiElectricityAmount +
            multiTransportAmount +
            smokeAdminFixedPrinted,
            "PHASE10-MULTI-PERIOD",
            "Two tariff periods in one bill",
            tariffPlan: "BT1-T1",
            exemptAmountClp:
                multiExemptAmount,
            totalDueClp:
                multiElectricityAmount +
                multiTransportAmount +
                smokeAdminFixedPrinted,
            periodPrecision:
                UtilityTimePrecision.DateOnly);

    utilityRepository.AddBillLine(
        multiBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        multiElectricityAmount,
        categoryKey:
            UtilityBillLineCategory.ElectricityConsumed,
        sortOrder: 10);
    utilityRepository.AddBillLine(
        multiBillId,
        "SERVICIO_ELECTRICO",
        "Transporte de electricidad",
        multiTransportAmount,
        categoryKey:
            UtilityBillLineCategory.ElectricityTransport,
        sortOrder: 20);
    utilityRepository.AddBillLine(
        multiBillId,
        "SERVICIO_ELECTRICO",
        "Administración del servicio",
        smokeAdminFixedPrinted,
        categoryKey:
            UtilityBillLineCategory.ServiceAdministration,
        sortOrder: 30);

    var multiAnalysis =
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver)
        .Analyze(
            multiBillId,
            "America/Santiago",
            UtilityGridImportStatisticalCompletion
                .Insufficient(
                    multiObservedKwh,
                    100.0,
                    0,
                    0,
                    "PHASE10-MULTI-PERIOD"));

    var multiAdmin =
        multiAnalysis.Components.SingleOrDefault(item =>
            item.ComponentKey ==
            UtilityBillLineCategory.ServiceAdministration);
    var multiTransport =
        multiAnalysis.Components.SingleOrDefault(item =>
            item.ComponentKey ==
            "ELECTRICITY_TRANSPORT_PLUS_PUBLIC_SERVICE");
    var multiObserved =
        multiAnalysis.Scenarios.Single(item =>
            item.Key == "SOLAR_OBSERVED");
    var expectedMultiObserved =
        smokeAdminFixedPrinted +
        multiObservedKwh *
        (multiElectricityRate +
         multiTransportRate +
         multiServiceRate);

    if (!multiAnalysis.HasTariffModel ||
        multiAnalysis.Status !=
            "SUPPORTED_MULTI_PERIOD_COMPONENT_MODEL" ||
        multiAnalysis.PublicationPeriods.Count != 2 ||
        multiAnalysis.PublicationPeriods[0].PublicationId !=
            mayPublicationId ||
        multiAnalysis.PublicationPeriods[1].PublicationId !=
            junePublicationId ||
        multiAdmin is null ||
        multiTransport is null ||
        Math.Abs(
            multiAdmin.FixedAmountClp -
            smokeAdminFixedPrinted) > 0.0001 ||
        Math.Abs(
            multiAdmin.RateClpPerKwh) > 0.000001 ||
        multiAdmin.PublicationIds.Count != 2 ||
        string.IsNullOrWhiteSpace(
            multiAdmin.RateBasis) ||
        !multiAdmin.RateBasis.Contains(
            "2026-06-01",
            StringComparison.Ordinal) ||
        Math.Abs(
            multiTransport.RateClpPerKwh -
            (multiTransportRate +
             multiServiceRate)) > 0.000001 ||
        Math.Abs(
            multiObserved.SupportedTariffSubtotalClp -
            expectedMultiObserved) > 0.001)
    {
        throw new InvalidOperationException(
            "Phase 10 multi-period tariff reconstruction smoke test failed.");
    }

    // Unlike earlier single-rate fixtures, this one contains competing
    // official monthly charges per effective period. A $727 gross rate is
    // present in both, but applicability must remain explicitly ambiguous.
    // Phase 10 real-bill tariff-structure regression using anonymous
    // September-2026 structural values. No customer-identifying data is stored.
    var augRealFixture =
        tariffPageFixture
            .Replace(
                "596,252 709,540 596,252 709,540",
                "430,000 512,000 610,588 726,600 754,600 897,974",
                StringComparison.Ordinal)
            .Replace(
                "13,415 15,964 13,415 15,964",
                "17,218 20,489 17,218 20,489",
                StringComparison.Ordinal)
            .Replace(
                "BT_AA T1 19,212 22,862 19,212 22,862",
                "BT_AA T1 19,212 33,237 19,212 33,237",
                StringComparison.Ordinal)
            .Replace(
                "176,2788 209,772 176,2788 209,772",
                "176,280 220,147 176,280 220,147",
                StringComparison.Ordinal);

    var sepRealFixture =
        tariffPageFixture
            .Replace(
                "596,252 709,540 596,252 709,540",
                "430,000 512,000 611,118 727,230 755,000 898,900",
                StringComparison.Ordinal)
            .Replace(
                "13,415 15,964 13,415 15,964",
                "17,218 20,489 17,218 20,489",
                StringComparison.Ordinal)
            .Replace(
                "BT_AA T1 19,212 22,862 19,212 22,862",
                "BT_AA T1 19,212 33,629 19,212 33,629",
                StringComparison.Ordinal)
            .Replace(
                "176,2788 209,772 176,2788 209,772",
                "176,280 220,539 176,280 220,539",
                StringComparison.Ordinal);

    var augRealId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                "Anonymous Agosto 2026 retroactivo structure",
                "https://example.invalid/anonymous-aug-2026.pdf",
                new DateOnly(2026, 8, 1),
                true));
    tariffRepository.MarkCaptured(
        augRealId,
        Path.Combine(
            paths.TariffDirectory,
            "anonymous-aug-2026.pdf"),
        "anonymous-aug-2026",
        1234,
        1,
        [augRealFixture]);
    tariffNormalizer.NormalizePublication(
        augRealId,
        tariffRepository.GetPageTexts(
            augRealId));

    var sepRealId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                "Anonymous Septiembre 2026 structure",
                "https://example.invalid/anonymous-sep-2026.pdf",
                new DateOnly(2026, 9, 1),
                false));
    tariffRepository.MarkCaptured(
        sepRealId,
        Path.Combine(
            paths.TariffDirectory,
            "anonymous-sep-2026.pdf"),
        "anonymous-sep-2026",
        1234,
        1,
        [sepRealFixture]);
    tariffNormalizer.NormalizePublication(
        sepRealId,
        tariffRepository.GetPageTexts(
            sepRealId));

    // Real official PDFs do not guarantee that the global fixed-monthly
    // row shares CandidateIndex with the independently classified RED/ETR
    // electricity row. Exercise that production shape explicitly; the
    // earlier fixture accidentally assigned zero to both indices.
    using (var mismatchedFixedIndices = database.OpenConnection())
    using (var shiftFixedIndex = mismatchedFixedIndices.CreateCommand())
    {
        shiftFixedIndex.CommandText =
            """
            UPDATE tariff_rate_candidate
            SET candidate_index = candidate_index + 100
            WHERE publication_id IN ($augId, $sepId)
              AND component_key = 'FIXED_MONTHLY';
            """;
        shiftFixedIndex.Parameters.AddWithValue("$augId", augRealId);
        shiftFixedIndex.Parameters.AddWithValue("$sepId", sepRealId);
        if (shiftFixedIndex.ExecuteNonQuery() < 2)
        {
            throw new InvalidOperationException(
                "Phase 10 fixed-component index divergence fixture was not applied.");
        }
    }

    const double realStructureBilledKwh = 97.0;
    const double realStructureObservedKwh = 88.065413;
    const double realStructureAdminActual = 727.0;
    const double realStructureTransportActual = 2072.0;
    const double realStructureExempt = 83.0;
    const double realStructureElectricityActual = 21389.0;

    var realStructureBillId =
        utilityRepository.AddBill(
            new DateTimeOffset(
                2026, 8, 28, 0, 0, 0,
                TimeSpan.FromHours(-4))
                .ToUniversalTime(),
            new DateTimeOffset(
                2026, 9, 28, 0, 0, 0,
                TimeSpan.FromHours(-3))
                .ToUniversalTime(),
            realStructureBilledKwh,
            26854,
            "PHASE10-REAL-STRUCTURE",
            "Anonymous real-bill tariff structure regression",
            tariffPlan: "BT1-T5",
            exemptAmountClp:
                realStructureExempt,
            totalDueClp: 26854,
            periodPrecision:
                UtilityTimePrecision.DateOnly);

    utilityRepository.AddBillLine(
        realStructureBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad Consumida (97kWh)",
        realStructureElectricityActual,
        categoryKey:
            UtilityBillLineCategory.ElectricityConsumed,
        sortOrder: 10);
    var realTransportLineId =
        utilityRepository.AddBillLine(
            realStructureBillId,
            "SERVICIO_ELECTRICO",
            "Transporte de electricidad",
            realStructureTransportActual,
            categoryKey:
                UtilityBillLineCategory.ElectricityTransport,
            sortOrder: 20);
    var realAdminLineId =
        utilityRepository.AddBillLine(
            realStructureBillId,
            "SERVICIO_ELECTRICO",
            "Administración del servicio",
            realStructureAdminActual,
            categoryKey:
                UtilityBillLineCategory.ServiceAdministration,
            sortOrder: 30);

    var realStructureAnalysis =
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver)
        .Analyze(
            realStructureBillId,
            "America/Santiago",
            UtilityGridImportStatisticalCompletion
                .Insufficient(
                    realStructureObservedKwh,
                    99.33,
                    0,
                    0,
                    "PHASE10-REAL-STRUCTURE"));

    var realTransport =
        realStructureAnalysis.Components.SingleOrDefault(item =>
            item.BillLineDescription ==
                "Transporte de electricidad");
    var realAdmin =
        realStructureAnalysis.Components.SingleOrDefault(item =>
            item.ComponentKey ==
                UtilityBillLineCategory.ServiceAdministration);

    if (!realStructureAnalysis.HasTariffModel ||
        realTransport is null ||
        realAdmin is null ||
        realTransport.ComponentKey !=
            "ELECTRICITY_TRANSPORT_PLUS_PUBLIC_SERVICE" ||
        Math.Abs(
            realTransport.ReconstructedAmountClp -
            2070.368) > 0.01 ||
        Math.Abs(
            realTransport.DifferenceClp -
            1.632) > 0.01 ||
        string.IsNullOrWhiteSpace(
            realTransport.CalculationBasis) ||
        !realTransport.CalculationBasis.Contains(
            "82.935",
            StringComparison.Ordinal) ||
        !realTransport.CalculationBasis.Contains(
            "83",
            StringComparison.Ordinal) ||
        Math.Abs(
            realAdmin.FixedAmountClp -
            727) > 0.001 ||
        Math.Abs(
            realAdmin.ReconstructedAmountClp -
            727) > 0.001 ||
        !realAdmin.EvidenceStatus.Contains(
            "APPLICABILITY_AMBIGUOUS", StringComparison.Ordinal) ||
        !realAdmin.CalculationBasis!.Contains(
            "no se ha demostrado", StringComparison.Ordinal) ||
        realStructureAnalysis.PublicationPeriods.Count != 2 ||
        realStructureAnalysis.PublicationPeriods[0].PublicationId !=
            augRealId ||
        realStructureAnalysis.PublicationPeriods[1].PublicationId !=
            sepRealId)
    {
        throw new InvalidOperationException(
            "Phase 10 anonymous Aug/Sep 2026 real-bill tariff structure regression failed.");
    }

    var realStructureAudit =
        new UtilityBillAuditV2Service(
            utilityRepository,
            rateVerification,
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            realStructureBillId,
            "America/Santiago");

    if (!realStructureAudit.Lines.Any(item =>
            item.BillLineId ==
                realTransportLineId &&
            item.ReconstructedAmountClp.HasValue) ||
        !realStructureAudit.Lines.Any(item =>
            item.BillLineId ==
                realAdminLineId &&
            item.ReconstructedAmountClp.HasValue))
    {
        throw new InvalidOperationException(
            "Phase 10 anonymous real-bill audit reconstruction regression failed.");
    }

    // Keep this exact Aug/Sep fixture isolated from later smoke scenarios
    // that intentionally create their own publications on the same effective
    // dates. The production engine is not changed to accommodate test-only
    // duplicate catalogs.
    using (var cleanupConnection = database.OpenConnection())
    using (var cleanupTransaction = cleanupConnection.BeginTransaction())
    {
        foreach (var publicationId in new[]
                 {
                     augRealId,
                     sepRealId
                 })
        {
            foreach (var table in new[]
                     {
                         "tariff_rate_candidate",
                         "tariff_publication_page_text"
                     })
            {
                using var deleteChild =
                    cleanupConnection.CreateCommand();
                deleteChild.Transaction =
                    cleanupTransaction;
                deleteChild.CommandText =
                    $"DELETE FROM {table} WHERE publication_id = $publicationId;";
                deleteChild.Parameters.AddWithValue(
                    "$publicationId",
                    publicationId);
                deleteChild.ExecuteNonQuery();
            }

            using var deletePublication =
                cleanupConnection.CreateCommand();
            deletePublication.Transaction =
                cleanupTransaction;
            deletePublication.CommandText =
                """
                DELETE FROM tariff_publication
                WHERE publication_id = $publicationId;
                """;
            deletePublication.Parameters.AddWithValue(
                "$publicationId",
                publicationId);
            deletePublication.ExecuteNonQuery();
        }

        cleanupTransaction.Commit();
    }

    // Phase 10 closure: when two normalized Enel versions share an effective
    // date, a retroactive replacement must be the publication used by the
    // product reconstruction rather than the superseded original.
    var julyOriginalFixture =
        tariffPageFixture
            .Replace(
                "131,039 155,936 131,039 155,936",
                "151,039 155,936 151,039 155,936",
                StringComparison.Ordinal)
            .Replace(
                "176,2788 209,772 176,2788 209,772",
                "196,2788 209,772 196,2788 209,772",
                StringComparison.Ordinal);
    var julyRetroFixture =
        tariffPageFixture
            .Replace(
                "131,039 155,936 131,039 155,936",
                "161,039 155,936 161,039 155,936",
                StringComparison.Ordinal)
            .Replace(
                "176,2788 209,772 176,2788 209,772",
                "206,2788 209,772 206,2788 209,772",
                StringComparison.Ordinal);

    var julyOriginalId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                "Smoke Julio 2026 original",
                "https://example.invalid/enel-july-original.pdf",
                new DateOnly(2026, 7, 1),
                false));
    tariffRepository.MarkCaptured(
        julyOriginalId,
        Path.Combine(
            paths.TariffDirectory,
            "smoke-july-original.pdf"),
        "july-original",
        1234,
        1,
        [julyOriginalFixture]);
    tariffNormalizer.NormalizePublication(
        julyOriginalId,
        tariffRepository.GetPageTexts(
            julyOriginalId));

    var julyRetroId =
        tariffRepository.UpsertDiscovery(
            new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                "Smoke Julio 2026 retroactivo",
                "https://example.invalid/enel-july-retro.pdf",
                new DateOnly(2026, 7, 1),
                true));
    tariffRepository.MarkCaptured(
        julyRetroId,
        Path.Combine(
            paths.TariffDirectory,
            "smoke-july-retro.pdf"),
        "july-retro",
        1234,
        1,
        [julyRetroFixture]);
    tariffNormalizer.NormalizePublication(
        julyRetroId,
        tariffRepository.GetPageTexts(
            julyRetroId));

    const double retroBilledKwh = 50.0;
    const double retroObservedKwh = 40.0;
    const double retroElectricityRate = 206.2788;
    var retroElectricityAmount =
        retroBilledKwh *
        retroElectricityRate;

    var retroBillId =
        utilityRepository.AddBill(
            new DateTimeOffset(
                2026, 7, 10, 0, 0, 0,
                TimeSpan.FromHours(-4))
                .ToUniversalTime(),
            new DateTimeOffset(
                2026, 7, 20, 0, 0, 0,
                TimeSpan.FromHours(-4))
                .ToUniversalTime(),
            retroBilledKwh,
            retroElectricityAmount,
            "PHASE10-RETROACTIVE",
            "Retroactive tariff version precedence",
            tariffPlan: "BT1-T1",
            totalDueClp:
                retroElectricityAmount,
            periodPrecision:
                UtilityTimePrecision.DateOnly);

    utilityRepository.AddBillLine(
        retroBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad consumida",
        retroElectricityAmount,
        categoryKey:
            UtilityBillLineCategory.ElectricityConsumed,
        sortOrder: 10);

    var retroAnalysis =
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver)
        .Analyze(
            retroBillId,
            "America/Santiago",
            UtilityGridImportStatisticalCompletion
                .Insufficient(
                    retroObservedKwh,
                    100.0,
                    0,
                    0,
                    "PHASE10-RETROACTIVE"));

    var retroElectricity =
        retroAnalysis.Components.Single(item =>
            item.ComponentKey ==
            UtilityBillLineCategory.ElectricityConsumed);

    if (!retroAnalysis.HasTariffModel ||
        retroAnalysis.PublicationPeriods.Count != 1 ||
        retroAnalysis.PublicationPeriods[0].PublicationId !=
            julyRetroId ||
        !retroAnalysis.PublicationPeriods[0].IsRetroactive ||
        retroElectricity.PublicationId !=
            julyRetroId ||
        !retroElectricity.IsRetroactive ||
        Math.Abs(
            retroElectricity.RateClpPerKwh -
            retroElectricityRate) > 0.000001 ||
        retroAnalysis.PublicationPeriods.Any(item =>
            item.PublicationId ==
            julyOriginalId))
    {
        throw new InvalidOperationException(
            "Phase 10 retroactive tariff reconstruction smoke test failed.");
    }


    var dateOnlyGap =
        new UtilityBillGapStatisticalCompletionService(
            database)
        .Analyze(
            familySmokeDeviceId,
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 13),
            "America/Santiago");

    var dateOnlyStoredEnd =
        new DateTimeOffset(
            2026, 1, 13, 0, 0, 0,
            TimeSpan.FromHours(-3))
        .ToUniversalTime();

    var dateOnlyBillId =
        utilityRepository.AddBill(
            utilityFromUtc,
            dateOnlyStoredEnd,
            dateOnlyGap.Completion.ObservedKwh,
            1000,
            "PHASE10-DATEONLY-SMOKE",
            "Date-only bill end date is inclusive",
            tariffPlan: "BT1-SMOKE",
            totalDueClp: 1000,
            periodPrecision:
                UtilityTimePrecision.DateOnly);

    var dateOnlySummary =
        new UtilityBillReconciliationSummaryService(
            utilityRepository,
            utilityReconciliation,
            new UtilityBillGapStatisticalCompletionService(
                database),
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            familySmokeDeviceId,
            dateOnlyBillId,
            "America/Santiago");

    if (Math.Abs(
            dateOnlySummary.ObservedInverterKwh -
            dateOnlyGap.Completion.ObservedKwh) >
        0.000001 ||
        Math.Abs(
            dateOnlySummary.CoveragePercent -
            dateOnlyGap.Completion.CoveragePercent) >
        0.000001)
    {
        throw new InvalidOperationException(
            "Phase 10 date-only inclusive-end reconciliation regression failed.");
    }


    var bridgeAudit = new UtilityBillAuditV2Service(
        utilityRepository,
        rateVerification,
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver))
        .Analyze(
            billId,
            "America/Santiago");

    if (!bridgeAudit.Lines.Any(item =>
            item.BillLineId == electricityAuditLineId &&
            item.ReconstructedAmountClp.HasValue) ||
        !bridgeAudit.Lines.Any(item =>
            item.BillLineId == noPrintedRateAuditLineId &&
            item.Status ==
                "OFFICIAL_RATE_DERIVATION_PENDING"))
    {
        throw new InvalidOperationException(
            "Phase 10 audit-v2 tariff-model bridge smoke test failed.");
    }

    var provenanceBillId = utilityRepository.AddBill(
        utilityFromUtc,
        utilityToUtc,
        expectedGridImport,
        11902,
        "PHASE10-V15-PROVENANCE",
        "VAT and simple-adjustment smoke",
        tariffPlan: "BT1-SMOKE",
        taxableAmountClp: 10000,
        ivaClp: 1900,
        grossBillAmountClp: 11900,
        totalDueClp: 11902,
        periodPrecision: UtilityTimePrecision.Exact,
        sourceKind: UtilityBillSourceKind.Manual,
        reviewState: UtilityBillReviewState.Reviewed,
        ivaRate: 0.19);

    utilityRepository.AddBillLine(
        provenanceBillId,
        "ACUMULADO",
        "IVA 19%",
        1900,
        categoryKey: UtilityBillLineCategory.Vat19,
        taxTreatment: "IVA",
        sortOrder: 80);

    utilityRepository.AddBillLine(
        provenanceBillId,
        "ACUMULADO",
        "Ajuste sencillo",
        2,
        categoryKey: UtilityBillLineCategory.SimpleAdjustment,
        sortOrder: 90);

    utilityRepository.UpsertBillFieldEvidence(
        provenanceBillId,
        "total_due_clp",
        UtilityBillSourceKind.Manual,
        UtilityBillEvidenceState.UserEntered,
        "11902",
        "11902");

    var documentId = utilityRepository.AddBillDocument(
        "ENEL_DISTRIBUCION_CHILE",
        "smoke-bill.pdf",
        Path.Combine(paths.UtilityBillEnelDirectory, "smoke-bill.pdf"),
        "0123456789abcdef",
        1234,
        2,
        EnelUtilityBillPdfImportService.ParserVersion,
        "smoke extracted bill text");

    utilityRepository.UpdateBill(
        provenanceBillId,
        utilityFromUtc,
        utilityToUtc,
        expectedGridImport,
        11902,
        "PHASE10-V15-PROVENANCE-REVIEWED",
        "Reviewed in place",
        tariffPlan: "BT1-SMOKE",
        taxableAmountClp: 10000,
        ivaClp: 1900,
        grossBillAmountClp: 11900,
        totalDueClp: 11902,
        periodPrecision: UtilityTimePrecision.Exact,
        sourceKind: UtilityBillSourceKind.PdfReviewed,
        sourceDocumentId: documentId,
        reviewState: UtilityBillReviewState.Reviewed,
        ivaRate: 0.19);

    var adjustmentLine = utilityRepository.GetBillLines(
            provenanceBillId)
        .Single(item =>
            item.CategoryKey ==
            UtilityBillLineCategory.SimpleAdjustment);
    utilityRepository.ConfirmBillLinePdfEvidence(
        adjustmentLine.BillLineId,
        UtilityBillLineCategory.SimpleAdjustment,
        2,
        "Ajuste sencillo $2");

    var reviewedBill = utilityRepository.GetBills()
        .Single(item =>
            item.BillId == provenanceBillId);
    var reviewedAdjustment = utilityRepository.GetBillLines(
            provenanceBillId)
        .Single(item =>
            item.BillLineId ==
            adjustmentLine.BillLineId);

    if (reviewedBill.SourceKind !=
            UtilityBillSourceKind.PdfReviewed ||
        reviewedBill.SourceDocumentId !=
            documentId ||
        reviewedBill.ReviewState !=
            UtilityBillReviewState.Reviewed ||
        reviewedAdjustment.EvidenceState !=
            UtilityBillEvidenceState.PdfExtractedConfirmed ||
        reviewedAdjustment.SourcePage != 2)
    {
        throw new InvalidOperationException(
            "Phase 10 v15 in-place bill review smoke test failed.");
    }

    var storedDocument =
        utilityRepository.GetBillDocument(documentId);
    var fieldEvidence =
        utilityRepository.GetBillFieldEvidence(
            provenanceBillId);

    if (storedDocument is null ||
        storedDocument.ContentSha256 != "0123456789abcdef" ||
        fieldEvidence.Count != 1 ||
        fieldEvidence[0].EvidenceState !=
            UtilityBillEvidenceState.UserEntered)
    {
        throw new InvalidOperationException(
            "Phase 10 v15 bill provenance repository smoke test failed.");
    }

    var v15Audit = new UtilityBillAuditV2Service(
        utilityRepository,
        rateVerification,
        new UtilityBillTariffScenarioAnalysisService(
            utilityRepository,
            tariffRepository,
            candidateRepository,
            versionResolver))
        .Analyze(
            provenanceBillId,
            "America/Santiago");

    if (v15Audit.ExpectedIvaClp != 1900 ||
        v15Audit.IvaDifferenceClp != 0 ||
        v15Audit.TaxStatus != "IVA_MATCH_19" ||
        Math.Abs(v15Audit.SimpleAdjustmentClp - 2) > 0.001 ||
        v15Audit.BalanceStatus != "MATERIAL_UNEXPLAINED_DIFFERENCE" ||
        !v15Audit.UnexplainedResidualClp.HasValue ||
        Math.Abs(v15Audit.UnexplainedResidualClp.Value - 10000) > 0.001 ||
        !v15Audit.Lines.Any(item =>
            item.Status == "VAT_RECONSTRUCTED_19") ||
        !v15Audit.Lines.Any(item =>
            item.Status == "PRINTED_SIMPLE_ADJUSTMENT"))
    {
        throw new InvalidOperationException(
            "Phase 10 v15 VAT/simple-adjustment audit smoke test failed.");
    }

    // A printed small adjustment must be explicit evidence, never a
    // synthetic line inserted merely to force the bill to balance.
    // Regression: an already-reviewed Build-632 style duplicate must
    // collapse back to one canonical PDF-backed subsidy line.
    var mergeBillId = utilityRepository.AddBill(
        utilityFromUtc,
        utilityToUtc,
        97,
        26854,
        "PHASE10-PDF-MERGE",
        "Anonymous PDF merge regression",
        sourceKind: UtilityBillSourceKind.PdfReviewed,
        reviewState: UtilityBillReviewState.Reviewed,
        ivaRate: 0.19);

    utilityRepository.AddBillLine(
        mergeBillId,
        "OTROS_CARGOS",
        "Subsidio Electrico Ley 21677 (4/6)",
        -3758,
        categoryKey: "CUSTOM",
        sortOrder: 10,
        sourceKind: UtilityBillSourceKind.LegacyManual,
        evidenceState: UtilityBillEvidenceState.LegacyUnreviewed);

    utilityRepository.AddBillLine(
        mergeBillId,
        "OTROS_CARGOS",
        "Subsidio Eléctrico Ley N° 21.667 (4/6)",
        -3758,
        categoryKey: UtilityBillLineCategory.Subsidy,
        sortOrder: 20,
        sourceKind: UtilityBillSourceKind.PdfReviewed,
        evidenceState: UtilityBillEvidenceState.PdfExtractedConfirmed,
        sourcePage: 2,
        sourceText: "old duplicate");

    var mergeDraft = new UtilityBillPdfDraft(
        SourcePath: "anonymous-source.pdf",
        StoredPath: "anonymous-stored.pdf",
        OriginalFileName: "anonymous.pdf",
        ContentSha256: "merge-smoke",
        ContentLength: 1,
        PageCount: 2,
        ParserVersion: EnelUtilityBillPdfImportService.ParserVersion,
        ExtractedText: string.Empty,
        PeriodStart: new DateOnly(2026, 8, 28),
        PeriodEndInclusive: new DateOnly(2026, 9, 28),
        BilledConsumptionKwh: 97,
        TaxableAmountClp: 20643,
        IvaClp: 3922,
        ExemptAmountClp: 83,
        GrossBillAmountClp: 24648,
        OtherChargesClp: 2206,
        PreviousBalanceClp: 0,
        MeterStartKwh: 79023,
        MeterEndKwh: 79120,
        TotalDueClp: 26854,
        TariffPlan: "BT1-T5",
        Lines:
        [
            new UtilityBillPdfDraftLine(
                "OTROS_CARGOS",
                UtilityBillLineCategory.Subsidy,
                "Subsidio Eléctrico Ley N° 21.667 (4/6)",
                -3758,
                2,
                "Subsidio Eléctrico Ley N° 21.667 (4/6) -3.758")
        ],
        Warnings: Array.Empty<string>());

    var mergeResult =
        new UtilityBillPdfReviewMergeService(
            utilityRepository)
        .Merge(
            mergeBillId,
            mergeDraft);

    var mergedLines =
        utilityRepository.GetBillLines(
            mergeBillId);

    if (mergeResult.RemovedDuplicates != 1 ||
        mergedLines.Count != 1 ||
        mergedLines[0].CategoryKey !=
            UtilityBillLineCategory.Subsidy ||
        mergedLines[0].SourceKind !=
            UtilityBillSourceKind.PdfReviewed ||
        mergedLines[0].EvidenceState !=
            UtilityBillEvidenceState.PdfExtractedConfirmed ||
        mergedLines[0].Description !=
            "Subsidio Eléctrico Ley N° 21.667 (4/6)")
    {
        throw new InvalidOperationException(
            "Phase 10 PDF review duplicate-line merge regression failed.");
    }

    var balancedAdjustmentBillId = utilityRepository.AddBill(
        utilityFromUtc,
        utilityToUtc,
        expectedGridImport,
        11902,
        "PHASE10-ADJUSTMENT-BALANCED",
        "Explicit $2 printed adjustment",
        taxableAmountClp: 10000,
        ivaClp: 1900,
        totalDueClp: 11902,
        sourceKind: UtilityBillSourceKind.Manual,
        reviewState: UtilityBillReviewState.Reviewed,
        ivaRate: 0.19);

    utilityRepository.AddBillLine(
        balancedAdjustmentBillId,
        "SERVICIO_ELECTRICO",
        "Base afecta smoke",
        10000,
        categoryKey: "CUSTOM_TAXABLE_BASE",
        taxTreatment: "AFECTO",
        sortOrder: 10);
    utilityRepository.AddBillLine(
        balancedAdjustmentBillId,
        "ACUMULADO",
        "IVA 19%",
        1900,
        categoryKey: UtilityBillLineCategory.Vat19,
        taxTreatment: "IVA",
        sortOrder: 20);
    utilityRepository.AddBillLine(
        balancedAdjustmentBillId,
        "ACUMULADO",
        "Ajuste sencillo",
        2,
        categoryKey: UtilityBillLineCategory.SimpleAdjustment,
        sortOrder: 30);

    var balancedAdjustmentAudit =
        new UtilityBillAuditV2Service(
            utilityRepository,
            rateVerification,
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            balancedAdjustmentBillId,
            "America/Santiago");

    if (balancedAdjustmentAudit.TaxStatus != "IVA_MATCH_19" ||
        balancedAdjustmentAudit.BalanceStatus != "BALANCED" ||
        !balancedAdjustmentAudit.UnexplainedResidualClp.HasValue ||
        Math.Abs(
            balancedAdjustmentAudit.UnexplainedResidualClp.Value) >
            0.001 ||
        Math.Abs(
            balancedAdjustmentAudit.SimpleAdjustmentClp - 2) >
            0.001)
    {
        throw new InvalidOperationException(
            "Phase 10 explicit printed-adjustment balance smoke test failed.");
    }

    var missingAdjustmentBillId = utilityRepository.AddBill(
        utilityFromUtc,
        utilityToUtc,
        expectedGridImport,
        11902,
        "PHASE10-ADJUSTMENT-MISSING",
        "Small residual must remain unexplained",
        taxableAmountClp: 10000,
        ivaClp: 1900,
        totalDueClp: 11902,
        sourceKind: UtilityBillSourceKind.Manual,
        reviewState: UtilityBillReviewState.Reviewed,
        ivaRate: 0.19);

    utilityRepository.AddBillLine(
        missingAdjustmentBillId,
        "SERVICIO_ELECTRICO",
        "Base afecta smoke",
        10000,
        categoryKey: "CUSTOM_TAXABLE_BASE",
        taxTreatment: "AFECTO",
        sortOrder: 10);
    utilityRepository.AddBillLine(
        missingAdjustmentBillId,
        "ACUMULADO",
        "IVA 19%",
        1900,
        categoryKey: UtilityBillLineCategory.Vat19,
        taxTreatment: "IVA",
        sortOrder: 20);

    var missingAdjustmentAudit =
        new UtilityBillAuditV2Service(
            utilityRepository,
            rateVerification,
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            missingAdjustmentBillId,
            "America/Santiago");

    if (missingAdjustmentAudit.BalanceStatus !=
            "SMALL_UNEXPLAINED_RESIDUAL" ||
        !missingAdjustmentAudit.UnexplainedResidualClp.HasValue ||
        Math.Abs(
            missingAdjustmentAudit.UnexplainedResidualClp.Value - 2) >
            0.001 ||
        Math.Abs(
            missingAdjustmentAudit.SimpleAdjustmentClp) >
            0.001)
    {
        throw new InvalidOperationException(
            "Phase 10 small unexplained residual smoke test failed.");
    }


    // Phase 10 regression derived from the structure of a real Enel bill,
    // with all customer-identifying data intentionally omitted.
    // Printed summary:
    // 20,643 + 3,922 + 83 = 24,648
    // 24,648 + 2,206 + 0 = 26,854
    // Printed detail lines sum to 26,857, so the detail has a small
    // unexplained -3 CLP residual and must NOT receive a synthetic adjustment.
    var printedStructureBillId = utilityRepository.AddBill(
        new DateTimeOffset(
            2026, 8, 28, 3, 0, 0,
            TimeSpan.Zero),
        new DateTimeOffset(
            2026, 9, 28, 3, 0, 0,
            TimeSpan.Zero),
        97,
        26854,
        "ANON-PRINTED-STRUCTURE",
        "Anonymous Phase 10 printed-bill structure regression",
        meterStartKwh: 79023,
        meterEndKwh: 79120,
        tariffPlan: "BT1-T5",
        taxableAmountClp: 20643,
        ivaClp: 3922,
        exemptAmountClp: 83,
        grossBillAmountClp: 24648,
        otherChargesClp: 2206,
        totalDueClp: 26854,
        periodPrecision: UtilityTimePrecision.DateOnly,
        sourceKind: UtilityBillSourceKind.Manual,
        reviewState: UtilityBillReviewState.Reviewed,
        ivaRate: 0.19,
        previousBalanceClp: 0);

    utilityRepository.AddBillLine(
        printedStructureBillId,
        "SERVICIO_ELECTRICO",
        "Administración del servicio",
        727,
        categoryKey: UtilityBillLineCategory.ServiceAdministration,
        sortOrder: 10);
    utilityRepository.AddBillLine(
        printedStructureBillId,
        "SERVICIO_ELECTRICO",
        "Electricidad Consumida (97kWh)",
        21389,
        categoryKey: UtilityBillLineCategory.ElectricityConsumed,
        sortOrder: 20);
    utilityRepository.AddBillLine(
        printedStructureBillId,
        "SERVICIO_ELECTRICO",
        "Transporte de electricidad",
        2072,
        categoryKey: UtilityBillLineCategory.ElectricityTransport,
        sortOrder: 30);
    utilityRepository.AddBillLine(
        printedStructureBillId,
        "SERVICIO_ELECTRICO",
        "Arriendo Medidor",
        463,
        categoryKey: UtilityBillLineCategory.MeterRental,
        sortOrder: 40);
    utilityRepository.AddBillLine(
        printedStructureBillId,
        "SERVICIO_ELECTRICO",
        "Servicio Común",
        5964,
        categoryKey: UtilityBillLineCategory.CommonService,
        sortOrder: 50);
    utilityRepository.AddBillLine(
        printedStructureBillId,
        "OTROS_CARGOS",
        "Subsidio Eléctrico (4/6)",
        -3758,
        categoryKey: UtilityBillLineCategory.Subsidy,
        sortOrder: 60);

    var printedStructureRecord =
        utilityRepository.GetBills()
            .Single(item =>
                item.BillId ==
                printedStructureBillId);

    if (printedStructureRecord.MeterStartKwh != 79023 ||
        printedStructureRecord.MeterEndKwh != 79120 ||
        printedStructureRecord.BilledConsumptionKwh != 97 ||
        printedStructureRecord.TaxableAmountClp != 20643 ||
        printedStructureRecord.IvaClp != 3922 ||
        printedStructureRecord.ExemptAmountClp != 83 ||
        printedStructureRecord.GrossBillAmountClp != 24648 ||
        printedStructureRecord.OtherChargesClp != 2206 ||
        printedStructureRecord.PreviousBalanceClp != 0 ||
        printedStructureRecord.TotalDueClp != 26854)
    {
        throw new InvalidOperationException(
            "Phase 10 printed bill summary persistence smoke test failed.");
    }

    var printedStructureAudit =
        new UtilityBillAuditV2Service(
            utilityRepository,
            rateVerification,
            new UtilityBillTariffScenarioAnalysisService(
                utilityRepository,
                tariffRepository,
                candidateRepository,
                versionResolver))
        .Analyze(
            printedStructureBillId,
            "America/Santiago");

    if (printedStructureAudit.TaxStatus != "IVA_MATCH_19" ||
        printedStructureAudit.SummaryBalanceStatus !=
            "SUMMARY_BALANCED" ||
        printedStructureAudit.GrossBillDifferenceClp != 0 ||
        printedStructureAudit.SummaryTotalDifferenceClp != 0 ||
        printedStructureAudit.BalanceStatus !=
            "SMALL_UNEXPLAINED_RESIDUAL" ||
        !printedStructureAudit.UnexplainedResidualClp.HasValue ||
        Math.Abs(
            printedStructureAudit.UnexplainedResidualClp.Value + 3) >
            0.001 ||
        Math.Abs(
            printedStructureAudit.SimpleAdjustmentClp) >
            0.001)
    {
        throw new InvalidOperationException(
            "Phase 10 anonymous printed-bill quadrature regression failed.");
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

    // Synthetic linked bill/tariff graph audit: portable evidence is matched
    // by archive category + SHA, never by source surrogate ID or date alone.
    var graphPaths = new AppPaths(Path.Combine(root, "relational-graph-source"));
    var graphDb = new SqliteDatabase(graphPaths);
    graphDb.Initialize();
    var billFile = Path.Combine(graphPaths.UtilityBillEnelDirectory,
        "test-linked-bill.pdf");
    var tariffFile = Path.Combine(graphPaths.TariffEnelDirectory,
        "test-tariff.pdf");
    var unlinkedBillFile = Path.Combine(graphPaths.UtilityBillEnelDirectory,
        "test-unlinked-bill.pdf");
    File.WriteAllText(unlinkedBillFile, "SYNTHETIC UNLINKED BILL DOCUMENT");
    var unlinkedBillHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(unlinkedBillFile)))
        .ToLowerInvariant();
    File.WriteAllText(billFile, "SYNTHETIC BILL EVIDENCE");
    File.WriteAllText(tariffFile, "SYNTHETIC TARIFF EVIDENCE");
    var billHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(billFile)))
        .ToLowerInvariant();
    var tariffHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tariffFile)))
        .ToLowerInvariant();
    const string testSourceUrl = "smoke://tariff-linkage-2026";
    long graphBillId, graphPublicationId;
    using (var connection = graphDb.OpenConnection())
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = """
            INSERT INTO utility_bill_document(
                provider,original_file_name,local_pdf_path,content_sha256,
                content_length,page_count,parser_version,imported_utc)
            VALUES('ENEL','test-linked-bill.pdf',$billFile,$billHash,23,1,
                   'smoke-v1','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$billFile", billFile);
        cmd.Parameters.AddWithValue("$billHash", billHash);
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO utility_bill_document(
                provider,original_file_name,local_pdf_path,content_sha256,
                content_length,page_count,parser_version,imported_utc)
            VALUES('ENEL','test-unlinked-bill.pdf',$unlinkedFile,$unlinkedHash,
                   32,1,'smoke-v1','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$unlinkedFile", unlinkedBillFile);
        cmd.Parameters.AddWithValue("$unlinkedHash", unlinkedBillHash);
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO utility_meter_reading(
                reading_at_utc,reading_kwh,created_utc,updated_utc)
            VALUES('2026-09-01T00:00:00Z',1000,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO utility_bill(
                period_start_utc,period_end_utc,billed_consumption_kwh,
                created_utc,updated_utc,source_document_id,from_reading_id)
            VALUES('2026-09-01T00:00:00Z','2026-09-30T00:00:00Z',97,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z',
                   (SELECT document_id FROM utility_bill_document WHERE content_sha256=$billHash),
                   (SELECT reading_id FROM utility_meter_reading LIMIT 1));
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT last_insert_rowid();";
        graphBillId = Convert.ToInt64(cmd.ExecuteScalar());
        cmd.Parameters.AddWithValue("$billId", graphBillId);
        cmd.CommandText = """
            INSERT INTO utility_bill_line(
                bill_id,section_key,description,amount_clp,created_utc,updated_utc)
            VALUES($billId,'ELECTRICITY','Synthetic charge',999,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO utility_bill_field_evidence(
                bill_id,field_key,source_kind,evidence_state,created_utc,updated_utc)
            VALUES($billId,'TOTAL','PDF','OBSERVED',
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();

        cmd.Parameters.AddWithValue("$tariffFile", tariffFile);
        cmd.Parameters.AddWithValue("$tariffHash", tariffHash);
        cmd.CommandText = """
            INSERT INTO tariff_publication(
                provider,category,title,source_url,effective_from,
                local_pdf_path,content_sha256,content_length,page_count,
                capture_status,updated_utc)
            VALUES('ENEL','REGULATED','Synthetic 2026',$url,'2026-09-01',
                   $tariffFile,$tariffHash,25,1,'CAPTURED','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$url", testSourceUrl);
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT last_insert_rowid();";
        graphPublicationId = Convert.ToInt64(cmd.ExecuteScalar());
        cmd.Parameters.AddWithValue("$publicationId", graphPublicationId);
        cmd.CommandText = """
            INSERT INTO tariff_publication_page_text(
                publication_id,page_number,page_text)
            VALUES($publicationId,1,'Synthetic document page');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO tariff_rate_candidate(
                publication_id,page_number,tariff_plan,component_key,
                printed_description,candidate_index,source_text,
                parser_version,validation_state,created_utc)
            VALUES($publicationId,1,'BT1','ENERGY','Example rate',1,
                   '100 CLP','smoke-v1','UNREVIEWED','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO tariff_publication(
                provider,category,title,source_url,capture_status,updated_utc)
            VALUES('ENEL','REGULATED','Incoming synthetic correction',
                   'smoke://incoming-source-2026','DISCOVERED','2026-10-08T00:00:00Z');
            INSERT INTO tariff_publication_relation(
                source_publication_id,relation_type,target_provider,target_category,
                target_official_document_number,target_publication_id,
                evidence_text,created_utc,updated_utc)
            VALUES(
                (SELECT publication_id FROM tariff_publication
                  WHERE source_url='smoke://incoming-source-2026'),
                'CORRECTS','ENEL','REGULATED','SYNTHETIC-2026',
                $publicationId,'Synthetic incoming correction',
                '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            INSERT INTO tariff_publication_relation(
                source_publication_id,relation_type,target_provider,target_category,
                target_official_document_number,target_publication_id,
                evidence_text,created_utc,updated_utc)
            VALUES(
                $publicationId,'REFERENCES','ENEL','REGULATED','INCOMING-2026',
                (SELECT publication_id FROM tariff_publication
                  WHERE source_url='smoke://incoming-source-2026'),
                'Synthetic outgoing reference',
                '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            INSERT INTO tariff_publication_relation(
                source_publication_id,relation_type,target_provider,target_category,
                target_official_document_number,target_publication_id,
                evidence_text,created_utc,updated_utc)
            VALUES(
                $publicationId,'REFERENCES','ENEL','REGULATED','UNRESOLVED-2026',
                NULL,'Synthetic unresolved external document',
                '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
    }
    // Bill-document reuse and two independent reading foreign keys must
    // remain visible independently; no surrogate-ID mapping is inferred.
    using (var connection = graphDb.OpenConnection())
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = """
            INSERT INTO utility_meter_reading(
                reading_at_utc,reading_kwh,created_utc,updated_utc)
            VALUES('2026-09-30T00:00:00Z',1097,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            UPDATE utility_bill SET to_reading_id=(
                SELECT reading_id FROM utility_meter_reading
                WHERE reading_at_utc='2026-09-30T00:00:00Z')
            WHERE bill_id=$billId;
            INSERT INTO utility_bill(
                period_start_utc,period_end_utc,billed_consumption_kwh,
                created_utc,updated_utc,source_document_id)
            VALUES('2026-09-01T00:00:00Z','2026-09-30T00:00:00Z',97,
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z',
                   (SELECT document_id FROM utility_bill_document
                    WHERE content_sha256=$billHash));
            """;
        cmd.Parameters.AddWithValue("$billId", graphBillId);
        cmd.Parameters.AddWithValue("$billHash", billHash);
        cmd.ExecuteNonQuery();
    }
    var graphZip = new FullBackupService(graphDb, graphPaths)
        .Create("0.11.0-test", "synthetic", "linked-graph");
    var graphTargetPaths = new AppPaths(Path.Combine(root, "relational-graph-target"));
    var graphTarget = new SqliteDatabase(graphTargetPaths);
    graphTarget.Initialize();
    var graphAuditor = new IsolatedRecoveryRelationAuditService();
    var graphAudit = graphAuditor.Audit(graphZip.Path, graphTarget.DatabasePath);
    var graphPlan = planner.CreatePlan(graphZip.Path, graphTarget.DatabasePath);
    if (graphPlan.Steps.Single(x => x.Category == "BILL_SOURCE_DOCUMENTS").Records != 2 ||
        graphPlan.Steps.Single(x => x.Category == "TARIFF_RELATIONS").Records != 3 ||
        graphPlan.Steps.Single(x => x.Category == "ENERGY_TELEMETRY").Status !=
            "BLOCKED_UNSUPPORTED")
        throw new InvalidOperationException(
            "Synthetic plan double-counted shared bill evidence or tariff graph edges.");
    // No diagnostic implies permission to import IDs, referenced PDFs,
    // meter readings or bill/tariff graphs into any target.
    var graphReadiness = IsolatedRecoveryReadinessReceiptService.Evaluate(
        graphPlan, graphAudit);
    if (!graphReadiness.SyntheticSettingsStageEligible ||
        graphReadiness.RealOrRelationalImportAuthorized ||
        graphReadiness.BlockedBillGroups != graphAudit.Bills.Count ||
        graphReadiness.BlockedTariffGroups != graphAudit.Tariffs.Count ||
        graphReadiness.UnlinkedBillDocumentsForReview !=
            graphAudit.UnlinkedBillDocuments.Count ||
        graphReadiness.BillGroupsWithMeterLinks != 1 ||
        graphReadiness.OutgoingTariffRelationEdges != 3 ||
        graphReadiness.UnresolvedTariffRelationEdges != 1)
        throw new InvalidOperationException(
            "Synthetic relational recovery diagnostic concealed blocking dependencies.");
    var forgedReadiness = IsolatedRecoveryReadinessReceiptService.Evaluate(
        graphPlan with { Steps = graphPlan.Steps.Select(step =>
            step.Category == "TARIFF_RELATIONS"
                ? step with { Status = "SYNTHETIC_STAGE_ONLY" } : step).ToArray() },
        graphAudit);
    var mismatchedSchemaReadiness = IsolatedRecoveryReadinessReceiptService.Evaluate(
        graphPlan, graphAudit with { TargetSchemaVersion = -1 });
    if (forgedReadiness.SyntheticSettingsStageEligible ||
        mismatchedSchemaReadiness.SyntheticSettingsStageEligible ||
        forgedReadiness.RealOrRelationalImportAuthorized ||
        mismatchedSchemaReadiness.RealOrRelationalImportAuthorized)
        throw new InvalidOperationException(
            "Forged or mismatched synthetic relation plan appeared eligible.");

    var linkedBill = graphAudit.Bills.Single(b => b.SourceBillId == graphBillId);
    var linkedTariff = graphAudit.Tariffs.Single(t =>
        t.SourcePublicationId == graphPublicationId);
    if (graphAudit.Status != "READ_ONLY_GRAPH_AUDIT" ||
        linkedBill.State != "READING_REMAP_REQUIRED" ||
        linkedBill.ChargeLineCount != 1 || linkedBill.FieldEvidenceCount != 1 ||
        !linkedBill.HasFromReading || !linkedBill.HasToReading ||
        linkedBill.OriginalDocumentSha256 != billHash ||
        linkedBill.SourceBillsForDocument != 2 ||
        linkedBill.TargetBillsForDocument != 0 ||
        linkedBill.FromReadingExactCandidates != 0 ||
        linkedBill.ToReadingExactCandidates != 0 ||
        linkedTariff.State != "DEPENDENT_TARIFF_GRAPH_REMAP_REQUIRED" ||
        linkedTariff.RateCandidates != 1 || linkedTariff.SourceTextPages != 1 ||
        linkedTariff.IncomingRelations != 1 ||
        linkedTariff.PublicationRelations != 2 ||
        linkedTariff.UnresolvedOutgoingRelations != 1 ||
        linkedTariff.TargetPdfMatches != 0 ||
        linkedTariff.ReferencedPublicationUrlsInTarget != 0 ||
        graphAudit.Totals.SourceBills != 2 ||
        graphAudit.Totals.BillsWithMeterLinks != 1 ||
        graphAudit.Totals.UnlinkedBillDocuments != 1 ||
        graphAudit.Totals.UnresolvedOutgoingLinks != 1 ||
        linkedBill.TargetHasOriginalDocument ||
        graphAudit.UnlinkedBillDocuments.Single(d =>
            d.OriginalDocumentSha256 == unlinkedBillHash).State !=
            "UNLINKED_DOCUMENT_CANDIDATE")
        throw new InvalidOperationException("Linked source graph dependency audit failed.");

    // The destination can contain the identical source document bytes while
    // having unrelated local IDs: mark OVERLAP, not 'identical full bill'.
    using (var connection = graphTarget.OpenConnection())
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = """
            INSERT INTO utility_bill_document(
                provider,original_file_name,local_pdf_path,content_sha256,
                content_length,page_count,parser_version,imported_utc)
            VALUES('ENEL','target-alias.pdf',$path,$billHash,23,1,
                   'smoke-v1','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$billHash", billHash);
        cmd.Parameters.AddWithValue("$path", Path.Combine(graphTargetPaths.UtilityBillDirectory,
            "target-alias.pdf"));
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO tariff_publication(
                provider,category,title,source_url,capture_status,updated_utc)
            VALUES('ENEL','REGULATED','Target other title',$url,
                   'DISCOVERED','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$url", testSourceUrl);
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO utility_bill_document(
                provider,original_file_name,local_pdf_path,content_sha256,
                content_length,page_count,parser_version,imported_utc)
            VALUES('ENEL','target-unlinked-alias.pdf',$unlinkedTargetPath,
                   $unlinkedHash,32,1,'smoke-v1','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$unlinkedTargetPath",
            Path.Combine(graphTargetPaths.UtilityBillDirectory,
                "target-unlinked-alias.pdf"));
        cmd.Parameters.AddWithValue("$unlinkedHash", unlinkedBillHash);
        cmd.ExecuteNonQuery();
    }
    graphAudit = graphAuditor.Audit(graphZip.Path, graphTarget.DatabasePath);
    if (graphAudit.UnlinkedBillDocuments.Single(d =>
            d.OriginalDocumentSha256 == unlinkedBillHash).State !=
        "UNLINKED_DOCUMENT_ALREADY_IN_TARGET")
        throw new InvalidOperationException(
            "Unlinked source document overlap was not detected by original hash.");
    if (graphAudit.Tariffs.Single(t => t.SourcePublicationId == graphPublicationId).State !=
        "TARIFF_SOURCE_OVERLAP_REVIEW")
        throw new InvalidOperationException("Source URL overlap without PDF must require review.");
    using (var overlappingTariff = graphTarget.OpenConnection())
    using (var update = overlappingTariff.CreateCommand())
    {
        update.CommandText =
            "UPDATE tariff_publication SET content_sha256=$sha WHERE source_url=$url;";
        update.Parameters.AddWithValue("$sha", tariffHash);
        update.Parameters.AddWithValue("$url", testSourceUrl);
        update.ExecuteNonQuery();
        var sameAudit = graphAuditor.Audit(graphZip.Path, graphTarget.DatabasePath);
        if (sameAudit.Tariffs.Single(t => t.SourcePublicationId == graphPublicationId).State !=
            "TARIFF_SOURCE_SAME_DOCUMENT_REVIEW" ||
            sameAudit.Tariffs.Single(t => t.SourcePublicationId == graphPublicationId)
                .TargetPdfMatches != 1)
            throw new InvalidOperationException(
                "Identical tariff PDF evidence was not counted and classified.");
        update.Parameters["$sha"].Value = new string('f', 64);
        update.ExecuteNonQuery();
        var conflictAudit = graphAuditor.Audit(graphZip.Path, graphTarget.DatabasePath);
        if (conflictAudit.Tariffs.Single(t => t.SourcePublicationId == graphPublicationId).State !=
            "TARIFF_SOURCE_CONTENT_CONFLICT_REVIEW" ||
            conflictAudit.Tariffs.Single(t => t.SourcePublicationId == graphPublicationId)
                .TargetPdfMatches != 0)
            throw new InvalidOperationException(
                "Conflicting tariff PDFs must not count as matching source content.");
    }
    if (graphAudit.Bills.Single(b => b.SourceBillId == graphBillId).State !=
        "READING_REMAP_REQUIRED" ||
        !graphAudit.Bills.Single(b => b.SourceBillId == graphBillId)
            .TargetHasOriginalDocument)
        throw new InvalidOperationException(
            "Bill reading FK remains blocked but original-document overlap must be visible.");
    using (var connection = graphTarget.OpenConnection())
    using (var check = connection.CreateCommand())
    {
        check.CommandText = "SELECT COUNT(*) FROM utility_bill;";
        if (Convert.ToInt32(check.ExecuteScalar()) != 0)
            throw new InvalidOperationException("Read-only graph audit wrote an active bill.");
    }
    // Inspect both meter endpoints with matching, conflicting and ambiguous
    // timestamp evidence; the candidate count must NOT imply auto identity.
    using (var conn = graphTarget.OpenConnection())
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            INSERT INTO utility_bill(
                period_start_utc,period_end_utc,created_utc,updated_utc,
                source_document_id)
            VALUES('2026-09-01T00:00:00Z','2026-09-30T00:00:00Z',
                   '2026-10-08T00:00:00Z','2026-10-08T00:00:00Z',
                   (SELECT document_id FROM utility_bill_document WHERE content_sha256=$billHash));
            INSERT INTO utility_meter_reading(
                reading_at_utc,reading_kwh,created_utc,updated_utc)
            VALUES
                ('2026-09-01T00:00:00Z',1000,'2026-10-08T00:00:00Z','2026-10-08T00:00:00Z'),
                ('2026-09-01T00:00:00Z',1001,'2026-10-08T00:00:00Z','2026-10-08T00:00:00Z'),
                ('2026-09-30T00:00:00Z',1097,'2026-10-08T00:00:00Z','2026-10-08T00:00:00Z');
            """;
        cmd.Parameters.AddWithValue("$billHash", billHash);
        cmd.ExecuteNonQuery();
    }
    var candidatesAudit = graphAuditor.Audit(graphZip.Path, graphTarget.DatabasePath);
    var billCandidates = candidatesAudit.Bills.Single(b => b.SourceBillId == graphBillId);
    if (billCandidates.TargetBillsForDocument != 1 ||
        billCandidates.SourceBillsForDocument != 2 ||
        billCandidates.FromReadingExactCandidates != 1 ||
        billCandidates.FromReadingTimestampCandidates != 2 ||
        billCandidates.ToReadingExactCandidates != 1 ||
        billCandidates.ToReadingTimestampCandidates != 1 ||
        billCandidates.State != "READING_REMAP_REQUIRED")
        throw new InvalidOperationException(
            "Bill recovery diagnostics mistook shared documents or readings for portable identities.");

    using (var conn = graphTarget.OpenConnection())
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            INSERT INTO tariff_publication(
                provider,category,title,source_url,capture_status,updated_utc)
            VALUES('ENEL','REGULATED','Synthetic referenced identity in target',
                   'smoke://incoming-source-2026','DISCOVERED','2026-10-08T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
    }
    var referenced = graphAuditor.Audit(graphZip.Path, graphTarget.DatabasePath);
    var mappedTariff = referenced.Tariffs.Single(t =>
        t.SourcePublicationId == graphPublicationId);
    if (mappedTariff.ReferencedPublicationUrlsInTarget != 1 ||
        mappedTariff.UnresolvedOutgoingRelations != 1 ||
        // Both ends of the reciprocal synthetic tariff graph have URLs in
        // destination: main -> correction and correction -> main.
        referenced.Tariffs.Single(t =>
            t.SourceUrl == "smoke://incoming-source-2026")
            .ReferencedPublicationUrlsInTarget != 1 ||
        referenced.Totals.ReferencedTariffUrlsInTarget != 2 ||
        referenced.Totals.IncomingTariffLinks != 2 ||
        referenced.Totals.OutgoingTariffLinks != 3)
        throw new InvalidOperationException(
            "Tariff dependency source-URL mapping hints are incomplete.");

    // A bad destination FK must never produce apparently trustworthy
    // bill/tariff graph classifications or write any copied records.
    var brokenRelationRejected = false;
    try { graphAuditor.Audit(graphZip.Path, brokenRecoveryDb.DatabasePath); }
    catch (InvalidDataException) { brokenRelationRejected = true; }
    if (!brokenRelationRejected)
        throw new InvalidOperationException(
            "Relationship audit accepted an inconsistent synthetic target.");

    var arbitraryAuditRejected = false;
    try { graphAuditor.Audit(graphZip.Path, Path.Combine(Path.GetTempPath(), "user-owned.db")); }
    catch (InvalidOperationException) { arbitraryAuditRejected = true; }
    if (!arbitraryAuditRejected)
        throw new InvalidOperationException("Graph audit accepted a non-fixture target.");

    // Safe SQL explorer integrated smoke: artificial local SQLite only.
    using (var writeFixture = database.OpenConnection())
    using (var seed = writeFixture.CreateCommand())
    {
        seed.CommandText = """
            WITH RECURSIVE nums(x) AS
            (SELECT 1 UNION ALL SELECT x+1 FROM nums WHERE x<215)
            INSERT INTO app_setting(key,value,updated_utc)
            SELECT 'smoke.sql.'||x,'value-'||x,'2026-10-08T00:00:00Z'
            FROM nums;
            """;
        seed.ExecuteNonQuery();
        seed.CommandText = """
            INSERT INTO app_setting(key,value,updated_utc)
            VALUES('smoke.sql.formula','=1+1','2026-10-08T00:00:00Z'),
                  ('smoke.sql.null',NULL,'2026-10-08T00:00:00Z');
            """;
        seed.ExecuteNonQuery();
    }

    var explorer = new SolarOfThings.Core.SqlExplorer.SafeSqlExplorerService(
        database.DatabasePath);
    var schema = await explorer.SchemaAsync();
    if (!schema.Rows.Any(row => row.Any(cell => cell.Text == "reporting_grid_import")))
        throw new InvalidOperationException("SQL explorer reporting schema discovery failed.");
    var sqlStatement = """
        SELECT key,value FROM app_setting WHERE key LIKE 'smoke.sql.%'
        ORDER BY key;
        """;
    var visible = await explorer.PreviewAsync(sqlStatement);
    if (visible.Rows.Count != 200 || !visible.HasMore ||
        visible.Columns.Count != 2)
        throw new InvalidOperationException("SQL preview must be bounded and indicate remaining rows.");

    // Quoted forbidden SQL words are harmless as literals; actual statements are rejected.
    var quoted = await explorer.PreviewAsync("SELECT 'DELETE' AS safe_word;");
    if (quoted.Rows.Count != 1 || quoted.Rows[0][0].Text != "DELETE")
        throw new InvalidOperationException("SQL literal scanning failed.");
    foreach (var badSql in new[]
    {
        "DELETE FROM app_setting;", "PRAGMA query_only=OFF;",
        "ATTACH DATABASE ':memory:' AS other;", "SELECT 1; DROP TABLE app_setting;",
        "WITH x AS (DELETE FROM app_setting RETURNING key) SELECT * FROM x;",
        "SELECT load_extension('x');",
        "SELECT 1; SELECT 2;"
    })
    {
        var rejected = false;
        try { await explorer.PreviewAsync(badSql); }
        catch (ArgumentException) { rejected = true; }
        catch (SqliteException) { rejected = true; }
        if (!rejected)
            throw new InvalidOperationException("Unsafe SQL was accepted: " + badSql);
    }
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        var cancelObserved = false;
        try { await explorer.PreviewAsync("SELECT 1", cancellationToken: cancelled.Token); }
        catch (OperationCanceledException) { cancelObserved = true; }
        if (!cancelObserved)
            throw new InvalidOperationException("Cancelled SQL preview did not abort.");
    }

    // Literal and CTE scanner regression: no false-positives inside strings.
    var legalCte = await explorer.PreviewAsync(
        "WITH sample AS (SELECT 'DROP; UPDATE -- harmless literal' AS phrase) SELECT phrase FROM sample;");
    if (legalCte.Rows.Count != 1 ||
        legalCte.Rows[0][0].Text != "DROP; UPDATE -- harmless literal")
        throw new InvalidOperationException("Read-only CTE/literal SQL incorrectly rejected.");

    // The read-only connection must never create a missing database.
    var absentDatabase = Path.Combine(root, "missing-sql-explorer.db");
    var noDatabaseOpened = false;
    try
    {
        await new SolarOfThings.Core.SqlExplorer.SafeSqlExplorerService(
            absentDatabase).PreviewAsync("SELECT 1;");
    }
    catch (FileNotFoundException) { noDatabaseOpened = true; }
    if (!noDatabaseOpened || File.Exists(absentDatabase))
        throw new InvalidOperationException("SQL explorer created a missing database.");

    // Non-whitelisted SQLite functions are denied by the native authorizer.
    var nativeFunctionBlocked = false;
    try { await explorer.PreviewAsync("SELECT randomblob(16);"); }
    catch (SqliteException) { nativeFunctionBlocked = true; }
    if (!nativeFunctionBlocked)
        throw new InvalidOperationException("SQLite authorizer accepted an unsafe function.");

    var csvOutput = Path.Combine(root, "sql-export-complete.csv");
    var xlsxOutput = Path.Combine(root, "sql-export-complete.xlsx");
    var csv = await explorer.ExportAsync(sqlStatement, csvOutput,
        SolarOfThings.Core.SqlExplorer.SqlExportFormat.Csv);
    var xlsx = await explorer.ExportAsync(sqlStatement, xlsxOutput,
        SolarOfThings.Core.SqlExplorer.SqlExportFormat.Xlsx);
    if (csv.Rows != 217 || xlsx.Rows != 217 ||
        !File.Exists(csvOutput) || !File.Exists(xlsxOutput))
        throw new InvalidOperationException("SQL export silently truncated the requested result.");
    var csvText = File.ReadAllText(csvOutput);
    if (!csvText.Contains("'=1+1", StringComparison.Ordinal) ||
        !csvText.Contains("\"\\N\"", StringComparison.Ordinal))
        throw new InvalidOperationException("CSV formula/NULL safety regression.");
    using (var exported = new XLWorkbook(xlsxOutput))
    {
        var sheet = exported.Worksheet("SQL");
        if (sheet.LastRowUsed()!.RowNumber() != xlsx.Rows + 1 ||
            sheet.Cell(1, 1).GetString() != "key")
            throw new InvalidOperationException("XLSX export range regression.");
    }
    // SQL structured diagnostic regression: do not invent parser offsets.
    var invalidTableSql = "SELECT 1\nFROM table_that_does_not_exist;";
    SqliteException? missingTableError = null;
    try { await explorer.PreviewAsync(invalidTableSql); }
    catch (SqliteException ex) { missingTableError = ex; }
    if (missingTableError is null)
        throw new InvalidOperationException("Expected invalid table SQL exception.");
    var detail = SolarOfThings.Core.SqlExplorer.SqlErrorDiagnostics.Describe(
        missingTableError, invalidTableSql);
    if (detail.SqliteCode != missingTableError.SqliteErrorCode ||
        detail.ApproximateLine != 2 ||
        detail.ApproximateColumn != 6 ||
        detail.LocationStatus != "APPROXIMATE_IDENTIFIER_REFERENCE_NOT_PARSER_ERROR_OFFSET")
        throw new InvalidOperationException(
            "SQL diagnostics did not provide a properly labelled approximate reference.");

    var ambiguousSql = "SELECT 1 FROM same_missing_table AS a CROSS JOIN same_missing_table AS b;";
    SqliteException? ambiguousError = null;
    try { await explorer.PreviewAsync(ambiguousSql); }
    catch (SqliteException ex) { ambiguousError = ex; }
    if (ambiguousError is null ||
        SolarOfThings.Core.SqlExplorer.SqlErrorDiagnostics.Describe(
            ambiguousError, ambiguousSql).ApproximateOffset is not null)
        throw new InvalidOperationException(
            "Ambiguous SQL identifier was incorrectly assigned a precise position.");

    if (csv.Elapsed < TimeSpan.Zero || xlsx.Elapsed < TimeSpan.Zero ||
        csv.FileSizeBytes != new FileInfo(csvOutput).Length ||
        xlsx.FileSizeBytes != new FileInfo(xlsxOutput).Length ||
        csv.FileSizeBytes <= 0 || xlsx.FileSizeBytes <= 0)
        throw new InvalidOperationException(
            "SQL export performance accounting mismatches actual output bytes.");

    // SQL million-row streaming XLSX regression: 1,000,000 synthetic data rows
    // plus header fits in Excel's 1,048,576-row worksheet. Do not load the
    // 1-million-row XLSX into ClosedXML or the smoke-test process RAM.
    var millionXlsx = Path.Combine(root, "sql-million-streaming.xlsx");
    var millionQuery = """
        WITH RECURSIVE tally(n) AS
        (SELECT 1 UNION ALL SELECT n + 1 FROM tally WHERE n < 1000000)
        SELECT n AS ordinal FROM tally;
        """;
    var million = await explorer.ExportAsync(millionQuery, millionXlsx,
        SolarOfThings.Core.SqlExplorer.SqlExportFormat.Xlsx);
    if (million.Rows != 1_000_000 || million.Columns != 1 ||
        !File.Exists(millionXlsx))
        throw new InvalidOperationException("One-million-row XLSX streaming export failed.");
    using (var workbook = ZipFile.OpenRead(millionXlsx))
    {
        var sheet = workbook.GetEntry("xl/worksheets/sheet1.xml") ??
            throw new InvalidOperationException("Streaming XLSX sheet is missing.");
        long counted = 0;
        string? lastRow = null;
        string? lastValue = null;
        using var data = sheet.Open();
        using var xml = System.Xml.XmlReader.Create(data);
        while (xml.Read())
        {
            if (xml.NodeType != System.Xml.XmlNodeType.Element) continue;
            if (xml.LocalName == "row")
            {
                counted++;
                lastRow = xml.GetAttribute("r");
            }
            else if (xml.LocalName == "v" && counted == 1_000_001)
                lastValue = xml.ReadElementContentAsString();
        }
        if (counted != 1_000_001 || lastRow != "1000001" ||
            lastValue != "1000000")
            throw new InvalidOperationException(
                "XLSX last row missing: expected 1,000,000 records plus one header.");
    }
    File.Delete(millionXlsx);

    using (var checkReadOnly = database.OpenConnection())
    using (var check = checkReadOnly.CreateCommand())
    {
        check.CommandText = "SELECT COUNT(*) FROM app_setting WHERE key LIKE 'smoke.sql.%';";
        if (Convert.ToInt32(check.ExecuteScalar()) != 217)
            throw new InvalidOperationException("SQL explorer unexpectedly changed SQLite.");
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

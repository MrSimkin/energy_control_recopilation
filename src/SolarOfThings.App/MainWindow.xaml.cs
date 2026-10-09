using System.Diagnostics;
using System.Data;
using System.Windows;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SolarOfThings.App.Localization;
using SolarOfThings.App.Controls;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Normalization;
using SolarOfThings.Core.Reporting;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Backup;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.SqlExplorer;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.Utility;

namespace SolarOfThings.App;

public partial class MainWindow : Window
{
    private const double DashboardLivePollSeconds = 150.0;
    private static readonly Brush ActiveNavigationBackground =
        new SolidColorBrush(Color.FromRgb(0x45, 0x51, 0x5B));
    private static readonly Brush ActiveNavigationAccent =
        new SolidColorBrush(Color.FromRgb(0x2F, 0x9B, 0xF4));

    private readonly AppPaths _paths;
    private readonly LocalizationService _localization;
    private readonly SolarOfThingsSessionManager _session;
    private readonly CommissioningProfileRepository _profiles;
    private readonly IServiceProvider _services;
    private readonly UiPerformanceRecorder _performance;
    private int _lastResponsiveColumnCount = -1;
    private readonly DispatcherTimer _dashboardLiveTimer;
    private readonly DispatcherTimer _dashboardProgressTimer;
    private readonly DispatcherTimer _dispatcherLatenessTimer;
    private long _previousDispatcherTick;
    private DateTimeOffset _dashboardLiveCycleStartedUtc = DateTimeOffset.UtcNow;
    private CancellationTokenSource? _syncCancellation;
    private bool _currentStateRefreshInProgress;
    private int _utilityAuditRefreshGeneration;
    private int _utilityGridRefreshGeneration;
    private (string DeviceId, long BillId, string TimeZoneId)?
        _utilityBillSummaryInFlightKey;
    private Task<UtilityBillReconciliationSummary>?
        _utilityBillSummaryInFlight;
    private UtilityBillPdfDraft? _pendingUtilityBillPdfDraft;
    private long? _editingUtilityBillId;
    private long? _editingUtilityBillLineId;
    private bool _dashboardVisible;
    private int _batteryRefreshGeneration;
    private bool _batteryDataLoading;
    private int _analysisRefreshGeneration;
    private bool _analysisDataLoading;
    private bool _analysisRangeInitializationPending;
    private int _analysisPresetGeneration;
    private bool _analysisPresetLoading;
    private bool _analysisCustomRangePendingApply;
    private EnergyAggregationTable? _analysisRenderedAggregation;
    private BatteryThresholdContext? _analysisRenderedThresholds;
    private string? _analysisRenderedDeviceId;
    private int _analysisRenderedGeneration = -1;
    private int _dashboardRefreshGeneration;
    private bool _dashboardDataLoading;
    private int _dataCoverageRefreshGeneration;
    private bool _windowClosed;
    private bool _suppressLanguageSelection;
    private bool _suppressAnalysisRangeSelection;
    private bool _suppressReportRangeSelection;
    private bool _suppressReportPresetSelection;
    private bool _suppressReportDatePartSelection;
    private IReadOnlyList<EnergyAggregationRow> _analysisAggregationRows =
        Array.Empty<EnergyAggregationRow>();
    private double[] _analysisChartPositions = Array.Empty<double>();

    public MainWindow(
        AppPaths paths,
        LocalizationService localization,
        SolarOfThingsSessionManager session,
        CommissioningProfileRepository profiles,
        IServiceProvider services,
        UiPerformanceRecorder performance)
    {
        _paths = paths;
        _localization = localization;
        _session = session;
        _profiles = profiles;
        _services = services;
        _performance = performance;

        _dashboardLiveTimer = new DispatcherTimer(
            DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(DashboardLivePollSeconds)
        };
        _dashboardLiveTimer.Tick += DashboardLiveTimer_Tick;
        _dashboardProgressTimer = new DispatcherTimer(
            DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _dashboardProgressTimer.Tick += DashboardProgressTimer_Tick;
        _dispatcherLatenessTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _dispatcherLatenessTimer.Tick += DispatcherLatenessTimer_Tick;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        Closed += MainWindow_Closed;

        InitializeComponent();
        // Keep live line/column feedback in sync with the WPF code editor.
        SqlStatementEditor.TextArea.Caret.PositionChanged += (_, _) =>
            UpdateSqlExplorerCaretStatus();
        LoadSqlExplorerShortcuts();

        AnalysisEnergyPlot.Plot.Axes.Link(
            AnalysisBatteryPlot,
            x: true,
            y: false);
        AnalysisBatteryPlot.Plot.Axes.Link(
            AnalysisEnergyPlot,
            x: true,
            y: false);

        DatabasePathText.Text = _paths.DatabasePath;
        InitializeProductExperience();

        TariffYearSelector.ItemsSource = Enumerable
            .Range(2020, Math.Max(1, DateTime.Now.Year - 2019))
            .Reverse()
            .ToArray();
        TariffYearSelector.SelectedItem = DateTime.Now.Year;

        _suppressLanguageSelection = true;
        LanguageSelector.SelectedValue = _localization.CurrentLanguage;
        _suppressLanguageSelection = false;

        CaptureStartModeSelector.SelectedValue = "auto";

        _suppressAnalysisRangeSelection = true;
        AnalysisRangePresetSelector.SelectedValue = "all";
        _suppressAnalysisRangeSelection = false;

        AnalysisAggregationSelector.SelectedValue = "Day";

        _suppressReportRangeSelection = true;
        ReportRangePresetSelector.SelectedValue = "all";
        _suppressReportRangeSelection = false;
        ReportAggregationSelector.SelectedValue = "Day";
        ReportTypeSelector.SelectedValue = "SimpleEnergy";
        ReportTitleTextBox.Text = GetDefaultReportTitle(ReportKind.SimpleEnergy);

        AutoConnectCheckBox.IsChecked = GetAutoConnectEnabled();
        RefreshConnectionStatus();
        RefreshBackupSecondaryPreference();
        RefreshCaptureStartOptions();
        // Dashboard is the only initially visible page. Battery/Data loads are
        // deferred until their first visit; never query invisible pages on startup.
        ShowPage("Dashboard");
        ApplyResponsiveCardLayouts();
        Loaded += MainWindow_Loaded;
    }

    // Observe only foreground Dispatcher timer scheduling delay. A late tick
    // can be caused by rendering, CPU pressure or OS scheduling; it does not
    // prove a particular UI operation blocked. No background polling thread.
    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        _previousDispatcherTick = Stopwatch.GetTimestamp();
        _dispatcherLatenessTimer.Start();
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        _dispatcherLatenessTimer.Stop();
        _previousDispatcherTick = 0;
    }

    private void DispatcherLatenessTimer_Tick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        if (_previousDispatcherTick == 0 ||
            !IsActive || WindowState == WindowState.Minimized)
        {
            _previousDispatcherTick = now;
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_previousDispatcherTick, now);
        _previousDispatcherTick = now;
        var lateness = DispatcherTimingPolicy.ObserveLateness(
            elapsed, _dispatcherLatenessTimer.Interval);
        if (lateness.HasValue)
            _performance.Record("UI.Dispatcher.TickLateness", lateness.Value);
    }

    private void MainWindow_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (IsInitialized)
            ApplyResponsiveCardLayouts();
    }

    private void ApplyResponsiveCardLayouts()
    {
        var usableWidth = Math.Max(0, ActualWidth - 285);
        var columns = ResponsiveGridLayoutPolicy.ColumnsForUsableWidth(usableWidth);

        // WPF already resizes Grid children as the window changes width.
        // Rebuilding RowDefinitions/ColumnDefinitions on every pixel of
        // SizeChanged causes avoidable measure/arrange passes and flicker.
        // Only rebuild when the actual responsive breakpoint changes.
        if (_lastResponsiveColumnCount == columns)
            return;
        _lastResponsiveColumnCount = columns;
        using var measure = _performance.Measure("UI.Layout.BreakpointChange");

        ApplyResponsiveCardGrid(DashboardSummaryCards, columns);
        ApplyResponsiveCardGrid(BatterySummaryCards, columns);
        ApplyResponsiveCardGrid(UtilitySummaryCards, columns);
        ApplyResponsiveCardGrid(AnalysisTimeSummaryCards, columns);
        ApplyResponsiveCardGrid(AnalysisEnergySummaryCards, columns);
        ApplyResponsiveCardGrid(DataCoverageSummaryCards, columns);
    }

    private static void ApplyResponsiveCardGrid(
        Grid grid,
        int columnCount)
    {
        if (grid is null || grid.Children.Count == 0)
            return;

        columnCount = Math.Clamp(
            columnCount,
            1,
            Math.Min(4, grid.Children.Count));

        var rowCount =
            (int)Math.Ceiling(grid.Children.Count / (double)columnCount);

        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        for (var i = 0; i < columnCount; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());

        for (var i = 0; i < rowCount; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < grid.Children.Count; i++)
        {
            var child = grid.Children[i];
            var row = i / columnCount;
            var column = i % columnCount;

            Grid.SetRow(child, row);
            Grid.SetColumn(child, column);

            if (child is FrameworkElement element)
            {
                var right =
                    column == columnCount - 1 ? 0 : 14;
                var bottom =
                    row == rowCount - 1 ? 0 : 14;
                element.Margin = new Thickness(0, 0, right, bottom);
            }
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        using var measure = _performance.Measure("UI.Loaded.Initialize");
        var profile = _profiles.Get();
        if (profile is null)
        {
            return;
        }

        SetGlobalOperation(
            true,
            _localization.CurrentLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "Preparando datos y conexión..."
                : "Preparing data and connection...");

        if (GetAutoConnectEnabled() && _session.HasSession)
        {
            await RefreshCurrentStateAsync(profile, showError: false);
        }

        var history = _services.GetRequiredService<HistoryRepository>();
        var normalized = _services.GetRequiredService<NormalizationRepository>();

        if (history.GetSampleCount(profile.DeviceId) == 0)
        {
            RefreshDashboardMetrics();
            SetGlobalOperation(false, string.Empty);
            return;
        }

        try
        {
            var normalizedRebuilt = false;

            if (!normalized.HasRuleVersion(
                    profile.DeviceId,
                    NormalizationService.RuleVersion))
            {
                var normalizer = _services.GetRequiredService<NormalizationService>();
                await normalizer.RebuildAsync(profile);
                normalizedRebuilt = true;
            }

            var behaviorRepository =
                _services.GetRequiredService<HouseholdBehaviorRepository>();

            if (normalizedRebuilt ||
                !behaviorRepository.HasContextVersion(
                    profile.DeviceId,
                    InstallationContextPolicyService.ContextVersion))
            {
                var behavior =
                    _services.GetRequiredService<HouseholdBehaviorService>();
                await behavior.RebuildAsync(profile.DeviceId);
            }

            EvaluateInstallationHealth(profile);
        }
        catch
        {
            // Detailed rebuild/configuration failures are kept in local diagnostics.
            // User-facing views remain conservative rather than guessing.
        }

        RefreshDashboardMetrics();
        SetGlobalOperation(false, string.Empty);
    }

    // Instrument service retrieval independently from UI rendering. These
    // observations can include CPU aggregation; they are not pure SQL timings.
    private T MeasureDataCall<T>(string operation, Func<T> call)
    {
        using var measure = _performance.Measure(operation);
        return call();
    }

    // Queue the latest Dashboard request without waiting on SQLite from the
    // Dispatcher. A single background reader coalesces rapidly repeated requests
    // (initialization, navigation, language switches and sync completion).
    private void RefreshDashboardMetrics()
    {
        _dashboardRefreshGeneration++;
        if (!_dashboardVisible || _windowClosed ||
            _dashboardDataLoading)
            return;

        _ = LoadDashboardMetricsAsync();
    }

    private string? _dashboardLastSavedDayDeviceId;

    private sealed record DashboardReadResult(
        IReadOnlyDictionary<string, NormalizedMetricValue> StoredMetrics,
        CurrentHouseholdSnapshot? CurrentSnapshot,
        DateOnly? LatestSavedDate,
        EnergyRangeSummary? LatestSavedEnergy);

    private DashboardReadResult ReadDashboardData(
        string deviceId,
        string? stationTimeZone,
        NormalizationRepository repository,
        CurrentHouseholdSnapshotService currentRepository,
        HistoryRepository history,
        EnergyRangeStatisticsService statistics)
    {
        using var measure = _performance.Measure("Data.Dashboard.BackgroundFetch");
        // Each repository opens its own short-lived SQLite connection. No
        // WPF control, DependencyObject or Dispatcher is accessed on this thread.
        var stored = MeasureDataCall(
            "Data.Dashboard.LatestMetrics",
            () => repository.GetLatestMetrics(deviceId));
        var current = currentRepository.GetLatest(deviceId);
        var coverage = MeasureDataCall(
            "Data.Dashboard.Coverage",
            () => history.GetCoverageSummary(deviceId));
        if (!coverage.LastSampleAtUtc.HasValue)
            return new DashboardReadResult(stored, current, null, null);

        var timeZone = string.IsNullOrWhiteSpace(stationTimeZone)
            ? "America/Santiago"
            : stationTimeZone;
        var date = SolarApiTime.GetLocalDate(
            coverage.LastSampleAtUtc.Value, timeZone);
        var window = SolarApiTime.GetLocalDayWindow(date, timeZone);
        var summary = MeasureDataCall(
            "Data.Dashboard.LatestDayEnergy",
            () => statistics.Get(deviceId, window.Start, window.End));
        return new DashboardReadResult(stored, current, date, summary);
    }

    private async Task LoadDashboardMetricsAsync()
    {
        _dashboardDataLoading = true;
        try
        {
            while (_dashboardVisible && !_windowClosed)
            {
                var requestedGeneration = _dashboardRefreshGeneration;
                try
                {
                    var profile = _profiles.Get();
                    if (profile is null)
                    {
                        ResetDashboardMetrics();
                        return;
                    }

                    // Resolve scoped/singleton dependencies on the UI thread;
                    // the worker receives only concrete services and immutable
                    // value arguments. It never touches WPF controls.
                    var normalized = _services.GetRequiredService<NormalizationRepository>();
                    var current = _services.GetRequiredService<CurrentHouseholdSnapshotService>();
                    var history = _services.GetRequiredService<HistoryRepository>();
                    var statistics = _services.GetRequiredService<EnergyRangeStatisticsService>();
                    var result = await Task.Run(() => ReadDashboardData(
                        profile.DeviceId, profile.StationTimeZone,
                        normalized, current, history, statistics));

                    if (_windowClosed || !_dashboardVisible)
                        return;
                    if (requestedGeneration != _dashboardRefreshGeneration)
                        continue;

                    var currentDeviceId = _profiles.Get()?.DeviceId;
                    if (!string.Equals(currentDeviceId, profile.DeviceId,
                            StringComparison.Ordinal))
                        _dashboardRefreshGeneration++;
                    if (!DashboardRefreshPolicy.CanApply(
                            requestedGeneration, _dashboardRefreshGeneration,
                            _dashboardVisible, _windowClosed,
                            profile.DeviceId, currentDeviceId))
                        continue;

                    using var measure = _performance.Measure("UI.Dashboard.Refresh");
                    var useCurrentSnapshot = result.CurrentSnapshot is
                        { Metrics.Count: > 0 };
                    var metrics = useCurrentSnapshot
                        ? result.CurrentSnapshot!.Metrics
                        : result.StoredMetrics;

                    SetPowerMetric(metrics, "pv_power_w", PvPowerValueText, PvPowerMetaText);
                    SetPowerMetric(metrics, "house_load_power_w", HouseLoadValueText, HouseLoadMetaText);
                    SetSocMetric(metrics, "battery_soc_pct", BatterySocValueText, BatterySocMetaText);
                    SetPowerMetric(metrics, "grid_import_power_w", GridImportValueText, GridImportMetaText);
                    RefreshOperatingState(metrics);
                    RefreshDashboardFreshness(
                        metrics,
                        useCurrentSnapshot ? result.CurrentSnapshot : null);
                    RenderDashboardLatestSavedDay(result, profile.DeviceId);
                    return;
                }
                catch (Exception ex)
                {
                    if (requestedGeneration != _dashboardRefreshGeneration)
                        continue;
                    if (_windowClosed || !_dashboardVisible)
                        return;

                    // Fail closed: never leave potentially stale energy values
                    // on-screen when the latest read cannot be completed.
                    ResetDashboardMetrics();
                    DashboardFreshnessText.Text =
                        _localization.CurrentLanguage.StartsWith(
                            "es", StringComparison.OrdinalIgnoreCase)
                            ? "No fue posible actualizar los datos del panel."
                            : "Dashboard data could not be refreshed.";
                    Debug.WriteLine(
                        $"Dashboard read failed ({ex.GetType().Name}); values hidden.");
                    return;
                }
            }
        }
        finally
        {
            _dashboardDataLoading = false;
        }
    }

    private void SetPowerMetric(
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics,
        string key,
        TextBlock valueText,
        TextBlock metaText)
    {
        if (!metrics.TryGetValue(key, out var metric))
        {
            valueText.Text = "— kW";
            metaText.SetResourceReference(TextBlock.TextProperty, "Metric.AwaitingSync");
            return;
        }

        valueText.Text = $"{metric.Value / 1000.0:F3} kW";
        metaText.Text = FormatMetricMetadata(metric);
    }

    private void SetSocMetric(
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics,
        string key,
        TextBlock valueText,
        TextBlock metaText)
    {
        if (!metrics.TryGetValue(key, out var metric))
        {
            valueText.Text = "— %";
            metaText.SetResourceReference(TextBlock.TextProperty, "Metric.AwaitingSync");
            return;
        }

        valueText.Text = $"{metric.Value:F0} %";
        metaText.Text = FormatMetricMetadata(metric);
    }

    private string FormatMetricMetadata(NormalizedMetricValue metric)
    {
        var confidence = metric.Confidence switch
        {
            "CONFIRMED" => _localization.GetString("Confidence.Confirmed"),
            "PROBABLE" => _localization.GetString("Confidence.Probable"),
            _ => _localization.GetString("Confidence.Unresolved")
        };

        if (string.Equals(
                metric.RuleVersion,
                "latest-state.v1",
                StringComparison.Ordinal))
        {
            return string.Format(
                _localization.GetString("Dashboard.LiveFrame"),
                metric.RecordedAtUtc.ToLocalTime().ToString("dd-MM HH:mm:ss"));
        }

        return $"{confidence} · {metric.RecordedAtUtc.ToLocalTime():dd-MM HH:mm}";
    }

    private void ResetDashboardMetrics()
    {
        PvPowerValueText.Text = "— kW";
        HouseLoadValueText.Text = "— kW";
        BatterySocValueText.Text = "— %";
        GridImportValueText.Text = "— kW";

        PvPowerMetaText.SetResourceReference(TextBlock.TextProperty, "Metric.AwaitingSync");
        HouseLoadMetaText.SetResourceReference(TextBlock.TextProperty, "Metric.AwaitingSync");
        BatterySocMetaText.SetResourceReference(TextBlock.TextProperty, "Metric.AwaitingSync");
        GridImportMetaText.SetResourceReference(TextBlock.TextProperty, "Metric.AwaitingSync");
        OperatingStateText.SetResourceReference(TextBlock.TextProperty, "Operating.NO_DATA");
        OperatingStateMetaText.Text = string.Empty;
        DashboardFreshnessText.Text = string.Empty;
        ResetDashboardLatestSavedDay();
    }

    private void DashboardOpenLatestDay_Click(object sender, RoutedEventArgs e)
    {
        // Never follow stale dashboard evidence to a different device, and
        // never select today's date when the latest STORED date is older.
        if (_windowClosed || !_dashboardVisible || _dashboardLastSavedDayDeviceId is null ||
            !string.Equals(_dashboardLastSavedDayDeviceId, _profiles.Get()?.DeviceId,
                StringComparison.Ordinal))
            return;
        ShowPage("Analysis");
        // Reapplying this preset is necessary even when the user previously
        // inspected a different day and the selector retained "latest-day".
        _suppressAnalysisRangeSelection = true;
        try { AnalysisRangePresetSelector.SelectedValue = null; }
        finally { _suppressAnalysisRangeSelection = false; }
        AnalysisRangePresetSelector.SelectedValue = "latest-day";
    }

    private void DashboardInspectCoverage_Click(object sender, RoutedEventArgs e) =>
        ShowPage("Data");

    private void RenderDashboardLatestSavedDay(DashboardReadResult result, string deviceId)
    {
        if (!result.LatestSavedDate.HasValue || result.LatestSavedEnergy is null)
        {
            ResetDashboardLatestSavedDay();
            return;
        }

        var localDate = result.LatestSavedDate.Value;
        var summary = result.LatestSavedEnergy;
        _dashboardLastSavedDayDeviceId = deviceId;
        DashboardLatestDayOpenButton.IsEnabled = true;

        DashboardLatestDayDateText.Text = string.Format(
            _localization.GetString("Dashboard.LatestDayDate"),
            localDate.ToString("dd-MM-yyyy"));

        SetDashboardDailyEnergy(
            DashboardLatestDaySolarText,
            summary.PvPower,
            summary.PvEnergyKwh);
        SetDashboardDailyEnergy(
            DashboardLatestDayHouseText,
            summary.HouseLoadPower,
            summary.HouseEnergyKwh);
        SetDashboardDailyEnergy(
            DashboardLatestDayGridText,
            summary.GridImportPower,
            summary.GridImportEnergyKwh);

        var coverages = new[]
            {
                summary.PvPower,
                summary.HouseLoadPower,
                summary.GridImportPower
            }
            .Where(metric => metric.SampleCount >= 2)
            .Select(metric => metric.CoveragePercent)
            .ToArray();

        var evidence = DashboardDailyEvidencePolicy.Assess(
            summary.PvPower.SampleCount, summary.PvPower.CoveragePercent,
            summary.HouseLoadPower.SampleCount, summary.HouseLoadPower.CoveragePercent,
            summary.GridImportPower.SampleCount, summary.GridImportPower.CoveragePercent);

        // The visible minimum percentage describes measured streams ONLY;
        // the evidence label makes missing streams explicit instead of
        // misleading the user with a reassuring aggregate percentage.
        DashboardLatestDayCoverageText.Text =
            evidence.MinimumAvailableCoveragePercent.HasValue
                ? $"{evidence.MinimumAvailableCoveragePercent.Value:F1} %"
                : "— %";
        DashboardLatestDayCoverageText.Foreground =
            evidence.ShouldWarn ? Brushes.DarkOrange : Brushes.Black;
        DashboardDailyEvidenceText.Text = string.Format(
            _localization.GetString("Dashboard.DailyEvidence." + evidence.State),
            evidence.AvailableStreams, evidence.MissingStreams);
        DashboardDailyEvidenceText.Foreground =
            evidence.ShouldWarn ? Brushes.DarkOrange : Brushes.DarkGreen;
    }

    private static void SetDashboardDailyEnergy(
        TextBlock target,
        PowerMetricStatistics metric,
        double energyKwh)
    {
        target.Text = metric.SampleCount >= 2
            ? $"{energyKwh:F2} kWh"
            : "— kWh";
    }

    private void ResetDashboardLatestSavedDay()
    {
        DashboardLatestDayDateText.SetResourceReference(
            TextBlock.TextProperty,
            "Dashboard.LatestDayNoData");
        DashboardLatestDaySolarText.Text = "— kWh";
        DashboardLatestDayHouseText.Text = "— kWh";
        DashboardLatestDayGridText.Text = "— kWh";
        DashboardLatestDayCoverageText.Text = "— %";
        DashboardLatestDayCoverageText.Foreground = Brushes.Black;
        DashboardDailyEvidenceText.SetResourceReference(
            TextBlock.TextProperty, "Dashboard.DailyEvidenceUnknown");
        DashboardDailyEvidenceText.Foreground = Brushes.DarkOrange;
        DashboardLatestDayOpenButton.IsEnabled = false;
        _dashboardLastSavedDayDeviceId = null;
    }

    private void RefreshDashboardFreshness(
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics,
        CurrentHouseholdSnapshot? currentSnapshot)
    {
        if (metrics.Count == 0)
        {
            DashboardFreshnessText.Text = string.Empty;
            return;
        }

        var latest = metrics.Values.Max(metric => metric.RecordedAtUtc);
        var sourceDisplay =
            latest.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss");
        var age = DateTimeOffset.UtcNow - latest;

        var recent = age >= TimeSpan.FromMinutes(-5) &&
                     age <= TimeSpan.FromMinutes(20);

        if (currentSnapshot is not null)
        {
            var checkedDisplay =
                currentSnapshot.RetrievedUtc.ToLocalTime()
                    .ToString("HH:mm:ss");

            DashboardFreshnessText.Text = string.Format(
                _localization.GetString(
                    recent
                        ? "Dashboard.LivePollingRecent"
                        : "Dashboard.LivePollingStale"),
                checkedDisplay,
                sourceDisplay);
            DashboardFreshnessText.Foreground =
                recent ? Brushes.Green : Brushes.DarkOrange;
            return;
        }

        DashboardFreshnessText.Text = string.Format(
            _localization.GetString(
                recent
                    ? "Dashboard.FreshnessRecent"
                    : "Dashboard.FreshnessStale"),
            sourceDisplay);

        DashboardFreshnessText.Foreground =
            recent ? Brushes.Green : Brushes.DarkOrange;
    }

    private void RefreshOperatingState(
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics)
    {
        var classifier = _services.GetRequiredService<HouseholdOperatingStateService>();
        var state = classifier.Evaluate(metrics);

        OperatingStateText.SetResourceReference(
            TextBlock.TextProperty,
            $"Operating.{state.StateKey}");

        OperatingStateMetaText.Text = state.ObservedAtUtc.HasValue
            ? string.Format(
                _localization.GetString("Operating.LastReading"),
                state.ObservedAtUtc.Value.ToLocalTime().ToString("dd-MM HH:mm"))
            : string.Empty;
    }

    private void RefreshBatteryView()
    {
        if (!IsInitialized || BatteryContent is null)
            return;
        _batteryRefreshGeneration++;
        if (_windowClosed || _batteryDataLoading ||
            BatteryContent.Visibility != Visibility.Visible)
            return;
        _ = LoadBatteryViewAsync();
    }

    private sealed record BatteryReadResult(
        IReadOnlyDictionary<string, NormalizedMetricValue> StoredMetrics,
        CurrentHouseholdSnapshot? CurrentSnapshot,
        BatteryConfiguration Configuration,
        BatteryThresholdContext Thresholds);

    private BatteryReadResult ReadBatteryData(
        string deviceId,
        NormalizationRepository repository,
        CurrentHouseholdSnapshotService currentRepository,
        BatteryConfigurationService configurationService,
        BatteryThresholdContextService thresholdService)
    {
        using var measure = _performance.Measure("Data.Battery.BackgroundFetch");
        // Worker receives only non-WPF services and value arguments.
        var stored = MeasureDataCall("Data.Battery.LatestMetrics",
            () => repository.GetLatestMetrics(deviceId));
        var current = MeasureDataCall("Data.Battery.CurrentSnapshot",
            () => currentRepository.GetLatest(deviceId));
        var configuration = MeasureDataCall("Data.Battery.Configuration",
            configurationService.Get);
        var thresholds = MeasureDataCall("Data.Battery.Thresholds",
            () => thresholdService.Get(deviceId));
        return new BatteryReadResult(stored, current, configuration, thresholds);
    }

    private async Task LoadBatteryViewAsync()
    {
        _batteryDataLoading = true;
        try
        {
            while (!_windowClosed && BatteryContent.Visibility == Visibility.Visible)
            {
                var generation = _batteryRefreshGeneration;
                try
                {
                    var profile = _profiles.Get();
                    if (profile is null)
                    {
                        ResetBatteryView();
                        return;
                    }

                    // Resolve services before Task.Run; no UI control is
                    // accessed on the worker thread.
                    var repository = _services.GetRequiredService<NormalizationRepository>();
                    var current = _services.GetRequiredService<CurrentHouseholdSnapshotService>();
                    var configuration = _services.GetRequiredService<BatteryConfigurationService>();
                    var thresholds = _services.GetRequiredService<BatteryThresholdContextService>();
                    var result = await Task.Run(() => ReadBatteryData(
                        profile.DeviceId, repository, current, configuration, thresholds));

                    if (_windowClosed || BatteryContent.Visibility != Visibility.Visible)
                        return;
                    if (generation != _batteryRefreshGeneration)
                        continue;

                    var deviceNow = _profiles.Get()?.DeviceId;
                    if (!string.Equals(deviceNow, profile.DeviceId, StringComparison.Ordinal))
                        _batteryRefreshGeneration++;
                    if (!BatteryRefreshPolicy.CanApply(
                        generation, _batteryRefreshGeneration,
                        BatteryContent.Visibility == Visibility.Visible,
                        _windowClosed, profile.DeviceId, deviceNow))
                        continue;

                    using var measure = _performance.Measure("UI.Battery.Refresh");
                    RenderBatteryData(result);
                    return;
                }
                catch (Exception ex)
                {
                    if (generation != _batteryRefreshGeneration)
                        continue;
                    if (_windowClosed || BatteryContent.Visibility != Visibility.Visible)
                        return;

                    ResetBatteryView();
                    BatteryLastReadingText.Text = _localization.CurrentLanguage.StartsWith(
                        "es", StringComparison.OrdinalIgnoreCase)
                        ? "No fue posible actualizar los datos de batería."
                        : "Battery data could not be refreshed.";
                    Debug.WriteLine($"Battery read failed ({ex.GetType().Name}); values hidden.");
                    return;
                }
            }
        }
        finally
        {
            _batteryDataLoading = false;
        }
    }

    private void RenderBatteryData(BatteryReadResult result)
    {
        var current = result.CurrentSnapshot;
        var useLive = current?.IsFresh == true &&
                      current.Metrics.ContainsKey("battery_soc_pct");
        var metrics = useLive ? current!.Metrics : result.StoredMetrics;
        var configuration = result.Configuration;
        var thresholds = result.Thresholds;

        RefreshBatteryTechnicalMetrics(metrics);

        BatteryThresholdSourceText.Text = string.Format(
            _localization.GetString(
                thresholds.UsesObservedSettings
                    ? thresholds.HasDrift ? "Battery.ThresholdSourceCurrentDrift" : "Battery.ThresholdSourceCurrent"
                    : "Battery.ThresholdSourceManual"),
            thresholds.NormalGridTransferSocPercent,
            thresholds.EmergencyFloorSocPercent,
            thresholds.ReturnToBatterySocPercent);

        BatteryConfiguredCapacityText.Text =
            $"{configuration.UsableCapacityKwh:F3} kWh";

        if (!metrics.TryGetValue("battery_soc_pct", out var soc))
        {
            ResetBatteryView(
                keepCapacity: true,
                keepTechnical: true);
            return;
        }

        var clampedSoc = Math.Clamp(soc.Value, 0, 100);
        var storedEnergy =
            configuration.UsableCapacityKwh * clampedSoc / 100.0;
        var ordinaryEnergy =
            configuration.UsableCapacityKwh *
            Math.Max(clampedSoc - thresholds.NormalGridTransferSocPercent, 0) /
            100.0;

        var emergencyReserveWidth =
            Math.Max(
                thresholds.NormalGridTransferSocPercent -
                thresholds.EmergencyFloorSocPercent,
                0);

        var emergencySocRemaining =
            Math.Min(
                emergencyReserveWidth,
                Math.Max(
                    clampedSoc - thresholds.EmergencyFloorSocPercent,
                    0));

        var emergencyEnergy =
            configuration.UsableCapacityKwh * emergencySocRemaining / 100.0;

        BatteryPageChargeText.Text = $"{clampedSoc:F0} %";
        BatteryStoredEnergyText.Text = $"{storedEnergy:F2} kWh";
        BatteryOrdinaryEnergyText.Text = $"{ordinaryEnergy:F2} kWh";
        BatteryEmergencyEnergyText.Text = $"{emergencyEnergy:F2} kWh";
        BatteryLastReadingText.Text =
            $"{soc.RecordedAtUtc.ToLocalTime():dd-MM-yyyy HH:mm} · " +
            _localization.GetString(
                useLive
                    ? "Battery.ReadingLive"
                    : "Battery.ReadingStored");

        if (!metrics.TryGetValue("battery_power_w", out var batteryPower))
        {
            BatteryActivityText.SetResourceReference(
                TextBlock.TextProperty,
                "Battery.Unknown");
        }
        else if (batteryPower.Value > 100)
        {
            BatteryActivityText.SetResourceReference(
                TextBlock.TextProperty,
                "Battery.Discharging");
        }
        else if (batteryPower.Value < -100)
        {
            BatteryActivityText.SetResourceReference(
                TextBlock.TextProperty,
                "Battery.Charging");
        }
        else
        {
            BatteryActivityText.SetResourceReference(
                TextBlock.TextProperty,
                "Battery.Resting");
        }
    }

    private void RefreshBatteryTechnicalMetrics(
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics)
    {
        BatteryVoltageTechnicalText.Text =
            metrics.TryGetValue("battery_voltage_v", out var voltage)
                ? $"{voltage.Value:F1} V"
                : "— V";

        BatteryChargeCurrentTechnicalText.Text =
            metrics.TryGetValue("battery_charge_current_a", out var chargeCurrent)
                ? $"{chargeCurrent.Value:F1} A"
                : "— A";

        BatteryDischargeCurrentTechnicalText.Text =
            metrics.TryGetValue("battery_discharge_current_a", out var dischargeCurrent)
                ? $"{dischargeCurrent.Value:F1} A"
                : "— A";

        if (metrics.TryGetValue("battery_power_w", out var batteryPower))
        {
            var prefix = string.Equals(
                batteryPower.Confidence,
                "CONFIRMED",
                StringComparison.Ordinal)
                ? string.Empty
                : "≈ ";

            BatteryPowerTechnicalText.Text =
                $"{prefix}{batteryPower.Value / 1000.0:F2} kW";
        }
        else
        {
            BatteryPowerTechnicalText.Text = "— kW";
        }
    }

    private void ResetBatteryView(
        bool keepCapacity = false,
        bool keepTechnical = false)
    {
        BatteryPageChargeText.Text = "— %";

        if (!keepTechnical)
        {
            BatteryVoltageTechnicalText.Text = "— V";
            BatteryChargeCurrentTechnicalText.Text = "— A";
            BatteryDischargeCurrentTechnicalText.Text = "— A";
            BatteryPowerTechnicalText.Text = "— kW";
        }
        BatteryStoredEnergyText.Text = "— kWh";
        BatteryOrdinaryEnergyText.Text = "— kWh";
        BatteryEmergencyEnergyText.Text = "— kWh";
        BatteryActivityText.SetResourceReference(
            TextBlock.TextProperty,
            "Battery.Unknown");
        BatteryLastReadingText.Text = "—";

        if (!keepCapacity)
        {
            var configuration =
                _services.GetRequiredService<BatteryConfigurationService>().Get();
            BatteryConfiguredCapacityText.Text =
                $"{configuration.UsableCapacityKwh:F3} kWh";
        }
    }

    private void EvaluateInstallationHealth(CommissioningProfile profile)
    {
        var history = _services.GetRequiredService<HistoryRepository>();
        if (history.GetSampleCount(profile.DeviceId) == 0)
        {
            return;
        }

        var evaluator = _services.GetRequiredService<InstallationHealthService>();
        evaluator.Evaluate(profile);
    }

    private void AnalysisApply_Click(object sender, RoutedEventArgs e)
    {
        _analysisPresetGeneration++;
        _analysisPresetLoading = false;
        _analysisCustomRangePendingApply = false;
        RefreshAnalysisView();
    }

    private async void AnalysisRangePresetSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressAnalysisRangeSelection || !IsInitialized ||
            _windowClosed || AnalysisContent is null)
            return;

        var preset = AnalysisRangePresetSelector.SelectedValue?.ToString() ?? "custom";
        _analysisCustomRangePendingApply = preset == "custom";
        var generation = ++_analysisPresetGeneration;
        // Immediately prevent any in-flight old Analysis range from painting
        // while the new date preset is resolving from historical coverage.
        _analysisRefreshGeneration++;
        if (string.Equals(preset, "custom", StringComparison.Ordinal))
        {
            _analysisPresetLoading = false;
            return;
        }

        _analysisPresetLoading = true;
        var shouldRefresh = false;
        try
        {
            shouldRefresh = await ApplyAnalysisRangePresetAsync(preset, generation);
        }
        catch (Exception ex)
        {
            if (generation == _analysisPresetGeneration &&
                AnalysisContent.Visibility == Visibility.Visible)
            {
                AnalysisStatusText.Text =
                    _localization.CurrentLanguage.StartsWith(
                        "es", StringComparison.OrdinalIgnoreCase)
                        ? "No fue posible seleccionar el rango histórico."
                        : "The historical range could not be selected.";
                Debug.WriteLine(
                    $"Analysis preset coverage failed ({ex.GetType().Name}).");
            }
        }
        finally
        {
            if (generation == _analysisPresetGeneration)
            {
                _analysisPresetLoading = false;
                if (shouldRefresh && !_windowClosed &&
                    AnalysisContent.Visibility == Visibility.Visible)
                    RefreshAnalysisView();
            }
        }
    }

    private void AnalysisDatePicker_SelectedDateChanged(
        object? sender,
        EventArgs e)
    {
        if (_suppressAnalysisRangeSelection ||
            !IsInitialized ||
            AnalysisRangePresetSelector is null)
        {
            return;
        }

        // A manual edit must not implicitly apply the new range.
        _analysisPresetGeneration++;
        _analysisPresetLoading = false;
        _analysisCustomRangePendingApply = true;
        _analysisRefreshGeneration++;
        _suppressAnalysisRangeSelection = true;
        try { AnalysisRangePresetSelector.SelectedValue = "custom"; }
        finally { _suppressAnalysisRangeSelection = false; }
        if (AnalysisContent.Visibility == Visibility.Visible)
            AnalysisStatusText.Text = _localization.CurrentLanguage == "es"
                ? "Rango modificado. Presiona Aplicar para calcularlo."
                : "Range changed. Select Apply to calculate it.";
    }

    private void AnalysisAggregationSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IsInitialized &&
            AnalysisContent is not null)
        {
            RefreshAnalysisView();
        }
    }

    private void AnalysisChartMetricSelection_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (!IsInitialized || _windowClosed || AnalysisContent is null ||
            AnalysisFromDatePicker is null || AnalysisToDatePicker is null ||
            AnalysisContent.Visibility != Visibility.Visible)
            return;

        if (_analysisCustomRangePendingApply) return;

        // Checkbox changes affect *only* the energy plot's visible series.
        // While an Analysis fetch is in progress the final UI renderer uses
        // the latest checkbox state, so no redundant query is necessary.
        if (_analysisDataLoading || _analysisPresetLoading)
            return;

        if (_analysisRenderedAggregation is not null &&
            AnalysisRefreshPolicy.CanReuseChart(
                _analysisRenderedGeneration, _analysisRefreshGeneration,
                true, false, _analysisRenderedDeviceId,
                _profiles.Get()?.DeviceId))
        {
            using var measure = _performance.Measure("UI.Analysis.SeriesToggle");
            RefreshAnalysisEnergyChart(_analysisRenderedAggregation.Rows);
            return;
        }

        RefreshAnalysisView();
    }

    private void AnalysisResetCharts_Click(
        object sender,
        RoutedEventArgs e)
    {
        AnalysisEnergyPlot.Plot.Axes.AutoScale();
        AnalysisBatteryPlot.Plot.Axes.AutoScale();
        AnalysisEnergyPlot.Plot.Axes.SetLimitsY(
            bottom: 0,
            top: AnalysisEnergyPlot.Plot.Axes.GetLimits().Top);
        AnalysisBatteryPlot.Plot.Axes.SetLimitsY(0, 100);
        AnalysisEnergyPlot.Refresh();
        AnalysisBatteryPlot.Refresh();
    }

    // Keep all UI selection, WPF chart operations and text rendering on the
    // Dispatcher. Only the existing data services run on background workers.
    // Consecutive requests share one reader and never paint stale ranges.
    private void RefreshAnalysisView(bool initializeRange = false)
    {
        if (!IsInitialized || AnalysisContent is null || _windowClosed)
            return;

        if (initializeRange)
        {
            _analysisRangeInitializationPending = true;
            _analysisCustomRangePendingApply = false;
        }
        if (_analysisCustomRangePendingApply) return;

        _analysisRefreshGeneration++;
        _analysisRenderedAggregation = null; // data/chart cache invalidated.
        _analysisRenderedThresholds = null;
        if (_analysisDataLoading || _analysisPresetLoading ||
            _analysisCustomRangePendingApply ||
            AnalysisContent.Visibility != Visibility.Visible)
            return;

        _ = LoadAnalysisViewAsync();
    }

    private sealed record AnalysisReadResult(
        EnergyRangeSummary EnergySummary,
        EnergyAggregationTable Aggregation,
        HouseholdBehaviorStatistics Behavior,
        BatteryThresholdContext Thresholds);

    private AnalysisReadResult ReadAnalysisData(
        string deviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        string timeZone,
        AggregationPeriod period,
        EnergyRangeStatisticsService energyService,
        EnergyAggregationTableService aggregationService,
        HouseholdBehaviorStatisticsService behaviorService,
        BatteryThresholdContextService thresholdService)
    {
        using var measure = _performance.Measure("Data.Analysis.BackgroundFetch");
        var energySummary = MeasureDataCall("Data.Analysis.EnergySummary",
            () => energyService.Get(deviceId, rangeStart, rangeEnd));
        var aggregation = MeasureDataCall("Data.Analysis.Aggregation",
            () => aggregationService.Get(
                deviceId, rangeStart, rangeEnd, timeZone, period));
        var behavior = MeasureDataCall("Data.Analysis.HouseholdStats",
            () => behaviorService.Get(deviceId, rangeStart, rangeEnd));
        var thresholds = MeasureDataCall("Data.Analysis.ChartThresholds",
            () => thresholdService.Get(deviceId));
        return new AnalysisReadResult(
            energySummary, aggregation, behavior, thresholds);
    }

    private async Task LoadAnalysisViewAsync()
    {
        _analysisDataLoading = true;
        try
        {
            while (!_windowClosed && AnalysisContent.Visibility == Visibility.Visible)
            {
                // A preset lookup owns calendar selection until it completes.
                // Avoid performing an obsolete query against the previous range.
                if (_analysisPresetLoading || _analysisCustomRangePendingApply)
                    return;
                var requestedGeneration = _analysisRefreshGeneration;
                var initializeRange = _analysisRangeInitializationPending;
                _analysisRangeInitializationPending = false;

                try
                {
                    var profile = _profiles.Get();
                    if (profile is null)
                    {
                        ResetAnalysisView();
                        return;
                    }

                    var history = _services.GetRequiredService<HistoryRepository>();
                    var coverage = await Task.Run(() => MeasureDataCall(
                        "Data.Analysis.Coverage",
                        () => history.GetCoverageSummary(profile.DeviceId)));

                    if (_windowClosed || AnalysisContent.Visibility != Visibility.Visible)
                        return;

                    if (requestedGeneration != _analysisRefreshGeneration)
                    {
                        if (initializeRange)
                            _analysisRangeInitializationPending = true;
                        continue;
                    }

                    var currentDeviceId = _profiles.Get()?.DeviceId;
                    if (!string.Equals(currentDeviceId, profile.DeviceId, StringComparison.Ordinal))
                    {
                        _analysisRefreshGeneration++;
                        if (initializeRange)
                            _analysisRangeInitializationPending = true;
                        continue;
                    }

                    if (!coverage.FirstSampleAtUtc.HasValue ||
                        !coverage.LastSampleAtUtc.HasValue)
                    {
                        ResetAnalysisView();
                        AnalysisStatusText.Text = _localization.GetString("Analysis.NoData");
                        return;
                    }

                    var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
                        ? "America/Santiago"
                        : profile.StationTimeZone;

                    var firstLocalDate = SolarApiTime.GetLocalDate(
                        coverage.FirstSampleAtUtc.Value, timeZone);
                    var lastLocalDate = SolarApiTime.GetLocalDate(
                        coverage.LastSampleAtUtc.Value, timeZone);

                    // DatePicker changes are initiated here (not by user
                    // selection); never interpret them as a custom-range edit.
                    _suppressAnalysisRangeSelection = true;
                    try
                    {
                        if (initializeRange)
                        {
                            AnalysisRangePresetSelector.SelectedValue = "all";
                            AnalysisFromDatePicker.SelectedDate =
                                firstLocalDate.ToDateTime(TimeOnly.MinValue);
                            AnalysisToDatePicker.SelectedDate =
                                lastLocalDate.ToDateTime(TimeOnly.MinValue);
                        }
                        else
                        {
                            if (!AnalysisFromDatePicker.SelectedDate.HasValue)
                                AnalysisFromDatePicker.SelectedDate =
                                    firstLocalDate.ToDateTime(TimeOnly.MinValue);
                            if (!AnalysisToDatePicker.SelectedDate.HasValue)
                                AnalysisToDatePicker.SelectedDate =
                                    lastLocalDate.ToDateTime(TimeOnly.MinValue);
                        }
                    }
                    finally
                    {
                        _suppressAnalysisRangeSelection = false;
                    }

                    var fromDate = DateOnly.FromDateTime(
                        AnalysisFromDatePicker.SelectedDate ??
                        firstLocalDate.ToDateTime(TimeOnly.MinValue));
                    var toDate = DateOnly.FromDateTime(
                        AnalysisToDatePicker.SelectedDate ??
                        lastLocalDate.ToDateTime(TimeOnly.MinValue));

                    if (fromDate > toDate)
                    {
                        (fromDate, toDate) = (toDate, fromDate);
                        _suppressAnalysisRangeSelection = true;
                        try
                        {
                            AnalysisFromDatePicker.SelectedDate =
                                fromDate.ToDateTime(TimeOnly.MinValue);
                            AnalysisToDatePicker.SelectedDate =
                                toDate.ToDateTime(TimeOnly.MinValue);
                        }
                        finally
                        {
                            _suppressAnalysisRangeSelection = false;
                        }
                    }

                    var fromWindow = SolarApiTime.GetLocalDayWindow(fromDate, timeZone);
                    var toWindow = SolarApiTime.GetLocalDayWindow(toDate, timeZone);
                    var period = GetSelectedAggregationPeriod();

                    // Resolve services on Dispatcher, then pass only plain
                    // value types and non-WPF services to the worker.
                    var energyService = _services.GetRequiredService<EnergyRangeStatisticsService>();
                    var aggregationService = _services.GetRequiredService<EnergyAggregationTableService>();
                    var behaviorService = _services.GetRequiredService<HouseholdBehaviorStatisticsService>();
                    var thresholdsService = _services.GetRequiredService<BatteryThresholdContextService>();

                    AnalysisStatusText.Text = _localization.CurrentLanguage.StartsWith(
                        "es", StringComparison.OrdinalIgnoreCase)
                        ? "Calculando análisis..."
                        : "Calculating analysis...";

                    var result = await Task.Run(() => ReadAnalysisData(
                        profile.DeviceId, fromWindow.Start, toWindow.End,
                        timeZone, period, energyService,
                        aggregationService, behaviorService, thresholdsService));

                    if (_windowClosed || AnalysisContent.Visibility != Visibility.Visible)
                        return;
                    if (requestedGeneration != _analysisRefreshGeneration)
                        continue;

                    currentDeviceId = _profiles.Get()?.DeviceId;
                    if (!string.Equals(currentDeviceId, profile.DeviceId, StringComparison.Ordinal))
                        _analysisRefreshGeneration++;

                    if (!AnalysisRefreshPolicy.CanApply(
                        requestedGeneration, _analysisRefreshGeneration,
                        AnalysisContent.Visibility == Visibility.Visible,
                        _windowClosed, profile.DeviceId, currentDeviceId))
                        continue;

                    using var measure = _performance.Measure("UI.Analysis.Refresh");
                    RefreshAnalysisEnergySummary(result.EnergySummary);
                    RenderAnalysisAggregationTable(result.Aggregation, result.Thresholds);
                    RenderAnalysisBehavior(result.Behavior);
                    _analysisRenderedAggregation = result.Aggregation;
                    _analysisRenderedThresholds = result.Thresholds;
                    _analysisRenderedDeviceId = profile.DeviceId;
                    _analysisRenderedGeneration = requestedGeneration;
                    return;
                }
                catch (Exception ex)
                {
                    if (requestedGeneration != _analysisRefreshGeneration)
                        continue;
                    if (_windowClosed || AnalysisContent.Visibility != Visibility.Visible)
                        return;

                    ResetAnalysisView();
                    AnalysisStatusText.Text = _localization.CurrentLanguage.StartsWith(
                        "es", StringComparison.OrdinalIgnoreCase)
                        ? "No fue posible actualizar el análisis."
                        : "Analysis could not be refreshed.";
                    Debug.WriteLine(
                        $"Analysis read failed ({ex.GetType().Name}); stale values hidden.");
                    return;
                }
            }
        }
        finally
        {
            _analysisDataLoading = false;
        }
    }

    private void RenderAnalysisBehavior(HouseholdBehaviorStatistics statistics)
    {
        if (statistics.SampleCount < 2)
        {
            ResetAnalysisValues();
            AnalysisStatusText.Text = _localization.GetString("Analysis.NoData");
            return;
        }

        var solarMinutes = GetStateDuration(
            statistics,
            "SOLAR_PRIMARY",
            "SOLAR_AND_CHARGING");

        var batteryMinutes = GetStateDuration(
            statistics,
            "BATTERY_SUPPLY",
            "OUTAGE_BATTERY",
            "OUTAGE_EMERGENCY_RESERVE",
            "OUTAGE_PROTECTED_FLOOR");

        var gridMinutes = GetStateDuration(
            statistics,
            "GRID_SUPPLY",
            "GRID_LOW_SOC",
            "GRID_RECOVERY");

        var recoveryMinutes = GetStateDuration(
            statistics,
            "GRID_RECOVERY");

        var emergencyMinutes = GetStateDuration(
            statistics,
            "OUTAGE_EMERGENCY_RESERVE",
            "OUTAGE_PROTECTED_FLOOR");

        AnalysisCoverageText.Text = string.Format(
            _localization.GetString("Analysis.Percent"),
            statistics.CoveragePercent);
        AnalysisSolarTimeText.Text = FormatAnalysisHours(solarMinutes);
        AnalysisBatteryTimeText.Text = FormatAnalysisHours(batteryMinutes);
        AnalysisGridTimeText.Text = FormatAnalysisHours(gridMinutes);
        AnalysisRecoveryTimeText.Text = FormatAnalysisHours(recoveryMinutes);
        AnalysisEmergencyTimeText.Text = FormatAnalysisHours(emergencyMinutes);
        AnalysisStateChangesText.Text =
            statistics.TransitionCount.ToString("N0");
        AnalysisUnknownTimeText.Text =
            FormatAnalysisHours(statistics.UncoveredGapMinutes);
        AnalysisStatusText.Text = string.Empty;
    }

    private async Task<bool> ApplyAnalysisRangePresetAsync(string preset, int generation)
    {
        var profile = _profiles.Get();
        if (profile is null || _windowClosed ||
            AnalysisContent.Visibility != Visibility.Visible)
        {
            return false;
        }

        var history =
            _services.GetRequiredService<HistoryRepository>();
        // Read-only historical coverage on a worker, never on Dispatcher.
        var coverage = await Task.Run(() => MeasureDataCall(
            "Data.Analysis.PresetCoverage",
            () => history.GetCoverageSummary(profile.DeviceId)));

        if (_windowClosed ||
            AnalysisContent.Visibility != Visibility.Visible ||
            generation != _analysisPresetGeneration ||
            !string.Equals(_profiles.Get()?.DeviceId,
                profile.DeviceId, StringComparison.Ordinal) ||
            !string.Equals(AnalysisRangePresetSelector.SelectedValue?.ToString(),
                preset, StringComparison.Ordinal))
            return false;

        if (!coverage.FirstSampleAtUtc.HasValue ||
            !coverage.LastSampleAtUtc.HasValue)
        {
            return false;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var firstLocalDate = SolarApiTime.GetLocalDate(
            coverage.FirstSampleAtUtc.Value,
            timeZone);
        var lastLocalDate = SolarApiTime.GetLocalDate(
            coverage.LastSampleAtUtc.Value,
            timeZone);

        if (string.Equals(
                preset,
                "custom",
                StringComparison.Ordinal))
        {
            return false;
        }

        var resolver =
            _services.GetRequiredService<TimeRangeSelectionService>();

        ResolvedTimeRange resolved = preset switch
        {
            "latest-day" =>
                resolver.ForDay(lastLocalDate, timeZone),
            "rolling-7" =>
                resolver.ForRolling7Days(lastLocalDate, timeZone),
            "latest-week" =>
                resolver.ForCalendarWeek(lastLocalDate, timeZone),
            "latest-month" =>
                resolver.ForMonth(
                    lastLocalDate.Year,
                    lastLocalDate.Month,
                    timeZone),
            "latest-year" =>
                resolver.ForYear(
                    lastLocalDate.Year,
                    timeZone),
            "rolling-12-months" =>
                resolver.ForRolling12Months(
                    lastLocalDate,
                    timeZone),
            _ =>
                resolver.ForArbitraryDateRange(
                    firstLocalDate,
                    lastLocalDate,
                    timeZone)
        };

        _suppressAnalysisRangeSelection = true;
        try
        {
            AnalysisFromDatePicker.SelectedDate =
                resolved.LocalStartDate.ToDateTime(TimeOnly.MinValue);
            AnalysisToDatePicker.SelectedDate =
                resolved.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        }
        finally
        {
            _suppressAnalysisRangeSelection = false;
        }

        return true;
    }

    private void RenderAnalysisAggregationTable(
        EnergyAggregationTable table, BatteryThresholdContext thresholds)
    {
        // ScottPlot and WPF ItemsSource are constructed only on Dispatcher.
        using var measure = _performance.Measure("UI.Analysis.ChartSetup");
        _analysisAggregationRows = table.Rows;
        AnalysisAggregationGrid.ItemsSource = table.Rows;
        RefreshAnalysisEnergyChart(table.Rows);
        RefreshAnalysisBatteryChart(table.Rows, thresholds);
    }

    private void AnalysisEnergyPlot_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_analysisAggregationRows.Count == 0)
        {
            return;
        }

        var p = e.GetPosition(AnalysisEnergyPlot);
        var pixel = new ScottPlot.Pixel(
            p.X * AnalysisEnergyPlot.DisplayScale,
            p.Y * AnalysisEnergyPlot.DisplayScale);
        var coordinates =
            AnalysisEnergyPlot.Plot.GetCoordinates(pixel);

        var index = FindNearestAnalysisRowIndex(coordinates.X);
        if (index < 0)
        {
            return;
        }

        var row = _analysisAggregationRows[index];
        var parts = new List<string>
        {
            string.Format(
                _localization.GetString("Analysis.Chart.PointPeriod"),
                row.LocalLabel)
        };

        if (AnalysisShowSolarCheckBox.IsChecked == true)
        {
            parts.Add(row.PvCoveragePercent > 0
                ? string.Format(_localization.GetString("Analysis.Chart.PointSolar"), row.PvEnergyKwh)
                : _localization.GetString("Analysis.Chart.PointSolarMissing"));
        }

        if (AnalysisShowHouseCheckBox.IsChecked == true)
        {
            parts.Add(row.HouseCoveragePercent > 0
                ? string.Format(_localization.GetString("Analysis.Chart.PointHouse"), row.HouseEnergyKwh)
                : _localization.GetString("Analysis.Chart.PointHouseMissing"));
        }

        if (AnalysisShowGridCheckBox.IsChecked == true)
        {
            parts.Add(row.GridCoveragePercent > 0
                ? string.Format(_localization.GetString("Analysis.Chart.PointGrid"), row.GridImportEnergyKwh)
                : _localization.GetString("Analysis.Chart.PointGridMissing"));
        }

        parts.Add(string.Format(
            _localization.GetString("Analysis.Chart.PointCoverage"),
            row.MinimumAvailableCoveragePercent));

        var detail = string.Join(" · ", parts);
        AnalysisEnergyHoverText.Text = detail;
        AnalysisEnergyPlot.ToolTip = detail;
    }

    private void AnalysisEnergyPlot_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        var hint =
            _localization.GetString("Analysis.Chart.HoverHint");
        AnalysisEnergyHoverText.Text = hint;
        AnalysisEnergyPlot.ToolTip = hint;
    }

    private void AnalysisBatteryPlot_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_analysisAggregationRows.Count == 0)
        {
            return;
        }

        var p = e.GetPosition(AnalysisBatteryPlot);
        var pixel = new ScottPlot.Pixel(
            p.X * AnalysisBatteryPlot.DisplayScale,
            p.Y * AnalysisBatteryPlot.DisplayScale);
        var coordinates =
            AnalysisBatteryPlot.Plot.GetCoordinates(pixel);

        var index = FindNearestAnalysisRowIndex(coordinates.X);
        if (index < 0)
        {
            return;
        }

        var row = _analysisAggregationRows[index];
        var parts = new List<string>
        {
            string.Format(
                _localization.GetString("Analysis.Chart.PointPeriod"),
                row.LocalLabel)
        };

        if (row.SocCoveragePercent <= 0)
        {
            parts.Add(_localization.GetString("Analysis.BatteryChart.PointMissing"));
        }
        else if (row.SocAveragePercent.HasValue)
        {
            parts.Add(string.Format(
                _localization.GetString("Analysis.BatteryChart.PointAverage"),
                row.SocAveragePercent.Value));
        }

        if (row.SocEndingPercent.HasValue)
        {
            parts.Add(string.Format(
                _localization.GetString("Analysis.BatteryChart.PointEnd"),
                row.SocEndingPercent.Value));
        }

        if (row.SocMinimumPercent.HasValue &&
            row.SocMaximumPercent.HasValue)
        {
            parts.Add(string.Format(
                _localization.GetString("Analysis.BatteryChart.PointRange"),
                row.SocMinimumPercent.Value,
                row.SocMaximumPercent.Value));
        }

        parts.Add(string.Format(
            _localization.GetString("Analysis.Chart.PointCoverage"),
            row.MinimumAvailableCoveragePercent));

        var detail = string.Join(" · ", parts);
        AnalysisBatteryHoverText.Text = detail;
        AnalysisBatteryPlot.ToolTip = detail;
    }

    private void AnalysisBatteryPlot_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        var hint =
            _localization.GetString("Analysis.BatteryChart.HoverHint");
        AnalysisBatteryHoverText.Text = hint;
        AnalysisBatteryPlot.ToolTip = hint;
    }

    private void RefreshAnalysisEnergyChart(
        IReadOnlyList<EnergyAggregationRow> rows)
    {
        using var measure = _performance.Measure("UI.Analysis.EnergyChart");
        var plot = AnalysisEnergyPlot.Plot;
        plot.Clear();
        _analysisChartPositions = BuildAnalysisChartPositions(rows);

        if (rows.Count == 0)
        {
            AnalysisChartStatusText.Text = _localization.GetString("Analysis.Chart.NoData");
            AnalysisEnergyPlot.Refresh();
            return;
        }

        var selectedSeries = new List<(Func<EnergyAggregationRow, double> Value, Func<EnergyAggregationRow, double> Coverage, string Label)>();
        if (AnalysisShowSolarCheckBox.IsChecked == true)
            selectedSeries.Add((row => row.PvEnergyKwh, row => row.PvCoveragePercent, _localization.GetString("Analysis.Chart.Solar")));
        if (AnalysisShowHouseCheckBox.IsChecked == true)
            selectedSeries.Add((row => row.HouseEnergyKwh, row => row.HouseCoveragePercent, _localization.GetString("Analysis.Chart.House")));
        if (AnalysisShowGridCheckBox.IsChecked == true)
            selectedSeries.Add((row => row.GridImportEnergyKwh, row => row.GridCoveragePercent, _localization.GetString("Analysis.Chart.Grid")));

        if (selectedSeries.Count == 0)
        {
            AnalysisChartStatusText.Text = _localization.GetString("Analysis.Chart.NoneSelected");
            AnalysisChartStatusText.Foreground = Brushes.Gray;
            AnalysisEnergyPlot.Refresh();
            return;
        }

        var warnings = new List<string>();
        if (rows.Any(row => row.MinimumAvailableCoveragePercent < 80))
            warnings.Add(_localization.GetString("Analysis.Chart.LowCoverage"));
        if (HasAnalysisTimeGaps(rows) || selectedSeries.Any(series => rows.Any(row => series.Coverage(row) <= 0)))
            warnings.Add(_localization.GetString("Analysis.Chart.MissingPeriods"));

        AnalysisChartStatusText.Text = string.Join(" ", warnings);
        AnalysisChartStatusText.Foreground = warnings.Count > 0 ? Brushes.DarkOrange : Brushes.Gray;

        var barWidth = selectedSeries.Count switch { 1 => 0.55, 2 => 0.32, _ => 0.22 };
        var spacing = barWidth + 0.04;
        var plottedAny = false;

        for (var seriesIndex = 0; seriesIndex < selectedSeries.Count; seriesIndex++)
        {
            var offset = (seriesIndex - (selectedSeries.Count - 1) / 2.0) * spacing;
            var validIndexes = Enumerable.Range(0, rows.Count).Where(index => selectedSeries[seriesIndex].Coverage(rows[index]) > 0).ToArray();
            if (validIndexes.Length == 0) continue;

            var positions = validIndexes.Select(index => _analysisChartPositions[index] + offset).ToArray();
            var values = validIndexes.Select(index => selectedSeries[seriesIndex].Value(rows[index])).ToArray();
            var bars = plot.Add.Bars(positions, values);
            bars.LegendText = selectedSeries[seriesIndex].Label;
            foreach (var bar in bars.Bars) bar.Size = barWidth;
            plottedAny = true;
        }

        if (!plottedAny)
        {
            AnalysisChartStatusText.Text = _localization.GetString("Analysis.Chart.NoData");
            AnalysisEnergyPlot.Refresh();
            return;
        }

        var tickGenerator = new ScottPlot.TickGenerators.NumericManual();
        var tickStep = Math.Max(1, (int)Math.Ceiling(rows.Count / 12.0));
        for (var index = 0; index < rows.Count; index += tickStep)
            tickGenerator.AddMajor(_analysisChartPositions[index], rows[index].LocalLabel);

        plot.Axes.Bottom.TickGenerator = tickGenerator;
        plot.YLabel("kWh");
        plot.Axes.Margins(bottom: 0, top: 0.15);
        plot.ShowLegend(ScottPlot.Alignment.UpperRight);
        AnalysisEnergyPlot.Refresh();
    }

    private void RefreshAnalysisBatteryChart(
        IReadOnlyList<EnergyAggregationRow> rows,
        BatteryThresholdContext thresholds)
    {
        using var measure = _performance.Measure("UI.Analysis.BatteryChart");
        var plot = AnalysisBatteryPlot.Plot;
        plot.Clear();

        var hasAverage = rows.Any(row => row.SocCoveragePercent > 0 && row.SocAveragePercent.HasValue);
        var hasEnding = rows.Any(row => row.SocCoveragePercent > 0 && row.SocEndingPercent.HasValue);
        if (!hasAverage && !hasEnding)
        {
            AnalysisBatteryChartStatusText.Text = _localization.GetString("Analysis.BatteryChart.NoData");
            AnalysisBatteryPlot.Refresh();
            return;
        }

        var warnings = new List<string>();
        if (rows.Any(row => row.MinimumAvailableCoveragePercent < 80))
            warnings.Add(_localization.GetString("Analysis.Chart.LowCoverage"));
        if (HasAnalysisTimeGaps(rows) || rows.Any(row => row.SocCoveragePercent <= 0))
            warnings.Add(_localization.GetString("Analysis.Chart.MissingPeriods"));

        AnalysisBatteryChartStatusText.Text = string.Join(" ", warnings);
        AnalysisBatteryChartStatusText.Foreground = warnings.Count > 0 ? Brushes.DarkOrange : Brushes.Gray;

        AddBatterySeriesSegments(plot, rows, row => row.SocAveragePercent, _localization.GetString("Analysis.BatteryChart.Average"));
        AddBatterySeriesSegments(plot, rows, row => row.SocEndingPercent, _localization.GetString("Analysis.BatteryChart.End"));

        var floor = plot.Add.HorizontalLine(
            thresholds.EmergencyFloorSocPercent);
        floor.LegendText = string.Format(
            _localization.GetString("Analysis.BatteryChart.Floor"),
            thresholds.EmergencyFloorSocPercent);

        var gridTransfer = plot.Add.HorizontalLine(
            thresholds.NormalGridTransferSocPercent);
        gridTransfer.LegendText = string.Format(
            _localization.GetString("Analysis.BatteryChart.GridTransfer"),
            thresholds.NormalGridTransferSocPercent);

        var normalReturn = plot.Add.HorizontalLine(
            thresholds.ReturnToBatterySocPercent);
        normalReturn.LegendText = string.Format(
            _localization.GetString("Analysis.BatteryChart.Return"),
            thresholds.ReturnToBatterySocPercent);

        var tickGenerator =
            new ScottPlot.TickGenerators.NumericManual();

        var tickStep =
            Math.Max(
                1,
                (int)Math.Ceiling(rows.Count / 12.0));

        for (var index = 0; index < rows.Count; index += tickStep)
        {
            tickGenerator.AddMajor(
                _analysisChartPositions[index],
                rows[index].LocalLabel);
        }

        plot.Axes.Bottom.TickGenerator = tickGenerator;
        plot.YLabel("%");
        plot.Axes.SetLimitsY(0, 100);
        plot.ShowLegend(ScottPlot.Alignment.UpperRight);

        AnalysisBatteryPlot.Refresh();
    }

    private AggregationPeriod GetSelectedAggregationPeriod()
    {
        var raw =
            AnalysisAggregationSelector.SelectedValue?.ToString();

        return Enum.TryParse<AggregationPeriod>(
            raw,
            ignoreCase: true,
            out var parsed)
            ? parsed
            : AggregationPeriod.Day;
    }

    private void AddBatterySeriesSegments(ScottPlot.Plot plot, IReadOnlyList<EnergyAggregationRow> rows, Func<EnergyAggregationRow, double?> selector, string legendText)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        var legendAssigned = false;

        void Flush()
        {
            if (xs.Count == 0) return;
            var scatter = plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            scatter.LegendText = legendAssigned ? string.Empty : legendText;
            scatter.LineWidth = 2;
            scatter.MarkerSize = 5;
            legendAssigned = true;
            xs.Clear();
            ys.Clear();
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var value = selector(row);
            var discontinuity = index > 0 && rows[index - 1].EndUtcExclusive < row.StartUtc - TimeSpan.FromSeconds(1);
            if (discontinuity || row.SocCoveragePercent <= 0 || !value.HasValue) Flush();
            if (row.SocCoveragePercent > 0 && value.HasValue)
            {
                xs.Add(_analysisChartPositions[index]);
                ys.Add(value.Value);
            }
        }
        Flush();
    }

    private static double[] BuildAnalysisChartPositions(IReadOnlyList<EnergyAggregationRow> rows)
    {
        if (rows.Count == 0) return Array.Empty<double>();
        var durations = rows.Select(row => (row.EndUtcExclusive - row.StartUtc).TotalSeconds)
            .Where(seconds => seconds > 0).OrderBy(seconds => seconds).ToArray();
        var nominalSeconds = durations.Length == 0 ? 1.0 : durations[durations.Length / 2];
        var origin = rows[0].StartUtc;
        return rows.Select(row => (row.StartUtc - origin).TotalSeconds / nominalSeconds).ToArray();
    }

    private static bool HasAnalysisTimeGaps(IReadOnlyList<EnergyAggregationRow> rows)
    {
        for (var index = 1; index < rows.Count; index++)
            if (rows[index - 1].EndUtcExclusive < rows[index].StartUtc - TimeSpan.FromSeconds(1)) return true;
        return false;
    }

    private int FindNearestAnalysisRowIndex(double x)
    {
        if (_analysisAggregationRows.Count == 0 || _analysisChartPositions.Length != _analysisAggregationRows.Count) return -1;
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < _analysisChartPositions.Length; index++)
        {
            var distance = Math.Abs(_analysisChartPositions[index] - x);
            if (distance < bestDistance) { bestDistance = distance; bestIndex = index; }
        }
        return bestDistance <= 0.75 ? bestIndex : -1;
    }

    private void AnalysisPlot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) return;
        AnalysisContent.ScrollToVerticalOffset(AnalysisContent.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void RefreshAnalysisEnergySummary(EnergyRangeSummary summary)
    {
        SetAnalysisEnergyValue(
            AnalysisPvEnergyText,
            summary.PvPower,
            summary.PvEnergyKwh);
        SetAnalysisEnergyValue(
            AnalysisHouseEnergyText,
            summary.HouseLoadPower,
            summary.HouseEnergyKwh);
        SetAnalysisEnergyValue(
            AnalysisGridEnergyText,
            summary.GridImportPower,
            summary.GridImportEnergyKwh);

        if (summary.BatteryPower.SampleCount >= 2)
        {
            AnalysisBatteryDeliveredText.Text = string.Format(
                _localization.GetString("Analysis.BatteryDelivered"),
                summary.BatteryDischargedEnergyKwh);
            AnalysisBatteryReceivedText.Text = string.Format(
                _localization.GetString("Analysis.BatteryReceived"),
                summary.BatteryChargedEnergyKwh);
        }
        else
        {
            AnalysisBatteryDeliveredText.Text = "—";
            AnalysisBatteryReceivedText.Text = "—";
        }

        var coverages = new[]
            {
                summary.PvPower,
                summary.HouseLoadPower,
                summary.GridImportPower,
                summary.BatteryPower
            }
            .Where(metric => metric.SampleCount >= 2)
            .Select(metric => metric.CoveragePercent)
            .ToArray();

        AnalysisEnergyCoverageText.Text = coverages.Length > 0
            ? string.Format(
                _localization.GetString("Analysis.EnergyCoverage"),
                coverages.Min())
            : string.Empty;
    }

    private static void SetAnalysisEnergyValue(
        TextBlock target,
        PowerMetricStatistics metric,
        double energyKwh)
    {
        target.Text = metric.SampleCount >= 2
            ? $"{energyKwh:N2} kWh"
            : "— kWh";
    }

    private static double GetStateDuration(
        HouseholdBehaviorStatistics statistics,
        params string[] stateKeys)
    {
        var total = 0.0;

        foreach (var key in stateKeys)
        {
            if (statistics.StateDurationMinutes.TryGetValue(
                    key,
                    out var minutes))
            {
                total += minutes;
            }
        }

        return total;
    }

    private string FormatAnalysisHours(double minutes)
    {
        return string.Format(
            _localization.GetString("Analysis.Hours"),
            minutes / 60.0);
    }

    private void ResetAnalysisView()
    {
        _analysisCustomRangePendingApply = false;
        _analysisRenderedAggregation = null;
        _analysisRenderedThresholds = null;
        _analysisRenderedDeviceId = null;
        _analysisRenderedGeneration = -1;
        _suppressAnalysisRangeSelection = true;
        try
        {
            AnalysisFromDatePicker.SelectedDate = null;
            AnalysisToDatePicker.SelectedDate = null;
        }
        finally { _suppressAnalysisRangeSelection = false; }
        ResetAnalysisValues();
        ResetAnalysisEnergyValues();
        _analysisAggregationRows = Array.Empty<EnergyAggregationRow>();
        AnalysisAggregationGrid.ItemsSource = null;
        AnalysisEnergyPlot.Plot.Clear();
        AnalysisEnergyPlot.Refresh();
        AnalysisChartStatusText.Text =
            _localization.GetString("Analysis.Chart.NoData");

        AnalysisBatteryPlot.Plot.Clear();
        AnalysisBatteryPlot.Refresh();
        AnalysisBatteryChartStatusText.Text =
            _localization.GetString("Analysis.BatteryChart.NoData");
        AnalysisEnergyHoverText.Text =
            _localization.GetString("Analysis.Chart.HoverHint");
        AnalysisBatteryHoverText.Text =
            _localization.GetString("Analysis.BatteryChart.HoverHint");
        AnalysisStatusText.Text = _localization.GetString("Analysis.NoData");
    }

    private void ResetAnalysisEnergyValues()
    {
        AnalysisPvEnergyText.Text = "— kWh";
        AnalysisHouseEnergyText.Text = "— kWh";
        AnalysisGridEnergyText.Text = "— kWh";
        AnalysisBatteryDeliveredText.Text = "—";
        AnalysisBatteryReceivedText.Text = "—";
        AnalysisEnergyCoverageText.Text = string.Empty;
    }

    private void ResetAnalysisValues()
    {
        AnalysisCoverageText.Text = "—";
        AnalysisSolarTimeText.Text = "—";
        AnalysisBatteryTimeText.Text = "—";
        AnalysisGridTimeText.Text = "—";
        AnalysisRecoveryTimeText.Text = "—";
        AnalysisEmergencyTimeText.Text = "—";
        AnalysisStateChangesText.Text = "—";
        AnalysisUnknownTimeText.Text = "—";
    }

    private async void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string pageKey)
        {
            return;
        }

        ShowPage(pageKey);

        var profile = _profiles.Get();
        if (profile is null || !_session.HasSession)
        {
            return;
        }

        if (string.Equals(
                pageKey,
                "Dashboard",
                StringComparison.Ordinal))
        {
            await RefreshCurrentStateAsync(
                profile,
                showError: false);
            return;
        }

        if (!string.Equals(
                pageKey,
                "Battery",
                StringComparison.Ordinal))
        {
            return;
        }

        // The navigation handler must not block WPF on a SQLite read.
        // A battery page load is already in flight; this separate lookup
        // decides only whether a remote current-state request is needed.
        var requestedBatteryGeneration = _batteryRefreshGeneration;
        CurrentHouseholdSnapshot? current;
        try
        {
            var reader = _services
                .GetRequiredService<CurrentHouseholdSnapshotService>();
            current = await Task.Run(() => MeasureDataCall(
                "Data.Battery.NavigationSnapshot",
                () => reader.GetLatest(profile.DeviceId)));
        }
        catch (Exception ex)
        {
            // Treat an unreadable snapshot conservatively. Never cause an
            // unhandled async-void navigation exception or expose its content.
            Debug.WriteLine(
                $"Battery navigation snapshot failed ({ex.GetType().Name}).");
            return;
        }

        if (!BatteryNavigationRefreshPolicy.ShouldRefresh(
                requestedBatteryGeneration, _batteryRefreshGeneration,
                BatteryContent.Visibility == Visibility.Visible,
                _windowClosed, profile.DeviceId, _profiles.Get()?.DeviceId,
                _session.HasSession, current?.IsFresh == true))
            return;

        await RefreshCurrentStateAsync(
            profile,
            showError: false);
    }

    private void PageScrollViewer_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        viewer.ScrollToVerticalOffset(
            Math.Max(
                0,
                viewer.VerticalOffset - e.Delta));
        e.Handled = true;
    }

    private void ShowPage(string pageKey)
    {
        using var measure = _performance.Measure("UI.Navigation");
        var buttons = new[]
        {
            DashboardNav,
            AnalysisNav,
            BatteryNav,
            GridUtilityNav,
            ReportsNav,
            DataNav,
            BackupNav,
            SqlNav,
            DiagnosticsNav,
            HelpNav,
            SettingsNav,
            AboutNav
        };

        foreach (var button in buttons)
        {
            button.ClearValue(BackgroundProperty);
            button.ClearValue(ForegroundProperty);
            button.ClearValue(BorderBrushProperty);
            button.ClearValue(BorderThicknessProperty);
        }

        var selectedButton = buttons.FirstOrDefault(
            button => string.Equals(button.Tag?.ToString(), pageKey, StringComparison.Ordinal));

        if (selectedButton is not null)
        {
            selectedButton.Background = ActiveNavigationBackground;
            selectedButton.Foreground = Brushes.White;
            selectedButton.BorderBrush = ActiveNavigationAccent;
            selectedButton.BorderThickness = new Thickness(4, 0, 0, 0);
        }

        PageTitle.SetResourceReference(TextBlock.TextProperty, $"Page.{pageKey}.Title");
        PageSubtitle.SetResourceReference(TextBlock.TextProperty, $"Page.{pageKey}.Subtitle");

        var isDashboard = string.Equals(pageKey, "Dashboard", StringComparison.Ordinal);
        var isAnalysis = string.Equals(pageKey, "Analysis", StringComparison.Ordinal);
        var isBattery = string.Equals(pageKey, "Battery", StringComparison.Ordinal);
        var isGridUtility = string.Equals(pageKey, "GridUtility", StringComparison.Ordinal);
        var isReports = string.Equals(pageKey, "Reports", StringComparison.Ordinal);
        var isData = string.Equals(pageKey, "Data", StringComparison.Ordinal);
        var isBackup = string.Equals(pageKey, "Backup", StringComparison.Ordinal);
        var isSqlExplorer = string.Equals(pageKey, "SqlExplorer", StringComparison.Ordinal);
        var isHelp = string.Equals(pageKey, "Help", StringComparison.Ordinal);
        var isSettings = string.Equals(pageKey, "Settings", StringComparison.Ordinal);
        var isAbout = string.Equals(pageKey, "About", StringComparison.Ordinal);

        _dashboardVisible = isDashboard;
        if (!isAnalysis)
        {
            _analysisRefreshGeneration++; // Invalidate pending Analysis work.
            _analysisPresetGeneration++;
            _analysisPresetLoading = false;
            _analysisRenderedAggregation = null;
            _analysisRenderedThresholds = null;
        }
        if (!isBattery) _batteryRefreshGeneration++;
        if (!isReports)
        {
            InvalidateReportPreview();
            _reportExportCancellation?.Cancel();
        }

        if (!isData) _dataCoverageRefreshGeneration++; // Discard stale coverage reads.
        if (!isDashboard)
            _dashboardRefreshGeneration++; // Discard in-flight Dashboard results.
        if (isDashboard)
        {
            ResetDashboardLiveCycle();
        }
        else
        {
            _dashboardLiveTimer.Stop();
            _dashboardProgressTimer.Stop();
            SetDashboardLiveCountdownVisibility(false);
        }

        DashboardContent.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;
        AnalysisContent.Visibility = isAnalysis ? Visibility.Visible : Visibility.Collapsed;
        BatteryContent.Visibility = isBattery ? Visibility.Visible : Visibility.Collapsed;
        GridUtilityContent.Visibility = isGridUtility ? Visibility.Visible : Visibility.Collapsed;
        ReportsContent.Visibility = isReports ? Visibility.Visible : Visibility.Collapsed;
        DataContent.Visibility = isData ? Visibility.Visible : Visibility.Collapsed;
        BackupContent.Visibility = isBackup ? Visibility.Visible : Visibility.Collapsed;
        SqlExplorerContent.Visibility = isSqlExplorer ? Visibility.Visible : Visibility.Collapsed;
        HelpContent.Visibility = isHelp ? Visibility.Visible : Visibility.Collapsed;
        SettingsContent.Visibility = isSettings ? Visibility.Visible : Visibility.Collapsed;
        AboutContent.Visibility = isAbout ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility =
            !isDashboard &&
            !isAnalysis &&
            !isBattery &&
            !isGridUtility &&
            !isReports &&
            !isData &&
            !isBackup &&
            !isSqlExplorer &&
            !isHelp &&
            !isSettings &&
            !isAbout
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (isDashboard)
        {
            RefreshDashboardMetrics();
        }
        else if (isAnalysis)
        {
            _analysisCustomRangePendingApply = false;
            RefreshAnalysisView();
        }
        else if (isBattery)
        {
            RefreshBatteryView();
        }
        else if (isGridUtility)
        {
            RefreshGridUtilityView();
        }
        else if (isReports)
        {
            // Arriving through general navigation clears old source context.
            _reportDraftOriginPage = null;
            ReportContextPanel.Visibility = Visibility.Collapsed;
            RefreshReportsView();
        }
        else if (isData)
        {
            RefreshDataCoverageView();
        }
        else if (isBackup)
        {
            _ = RefreshBackupInventoryAsync();
        }
        else if (isSqlExplorer)
        {
            _ = RefreshSqlSchemaAsync();
        }
        else if (isHelp || isAbout)
        {
            RefreshProductExperienceLocalization();
        }
        else if (!isDashboard)
        {
            PlaceholderTitle.SetResourceReference(TextBlock.TextProperty, $"Page.{pageKey}.Title");
            PlaceholderDescription.SetResourceReference(TextBlock.TextProperty, $"Page.{pageKey}.Placeholder");
        }

        ConnectionToolsPanel.Visibility =
            pageKey is "Diagnostics"
                ? Visibility.Visible
                : Visibility.Collapsed;
        var showPerformance = pageKey == "Diagnostics"
            ? Visibility.Visible : Visibility.Collapsed;
        PerformanceSummaryTitle.Visibility = showPerformance;
        PerformanceSummaryRefreshButton.Visibility = showPerformance;
        PerformanceSummaryScroll.Visibility = showPerformance;
        if (showPerformance == Visibility.Visible)
            RenderPerformanceSummary();

        if (isSettings)
        {
            RefreshSettingsSessionStatus();
            RefreshExportFolderPreference();
        }
    }

    private void PerformanceSummaryRefresh_Click(object sender, RoutedEventArgs e) =>
        RenderPerformanceSummary();

    private void RenderPerformanceSummary()
    {
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es", StringComparison.OrdinalIgnoreCase);
        var summaries = _performance.Summaries();
        if (summaries.Count == 0)
        {
            PerformanceSummaryText.Text = spanish
                ? "Sin observaciones todavía. Navega entre las pantallas y vuelve aquí."
                : "No observations yet. Visit other pages and return.";
            return;
        }
        var header = spanish
            ? "Mediciones locales de esta sesión; sin datos personales.\n" +
              "Promedio/p95/máximo observados (ms); no son una garantía ni una comparación entre Builds.\n" +
              "UI.Dispatcher.TickLateness: sólo retrasos de temporizador >150 ms con ventana activa; no demuestran bloqueos.\n\n"
            : "Local observations for this session; no personal information.\n" +
              "Observed mean/p95/max (ms); not a performance guarantee or a build comparison.\n" +
              "UI.Dispatcher.TickLateness: active-window timer delay >150 ms only; does not prove UI blocking.\n\n";
        // In-memory summaries are already ranked by observed p95, not name.
        var visible = summaries.Take(24).ToArray();
        var rows = visible.Select(x =>
            $"{x.Operation} | n={x.Samples} | " +
            $"avg={x.MeanMilliseconds:F0} p95={x.P95Milliseconds:F0} " +
            $"max={x.MaxMilliseconds:F0} ms");
        var guide = spanish
            ? "Ordenado por p95 observado. Con pocas muestras (n) el valor es inestable.\n"
            : "Sorted by observed p95. With small sample counts (n), estimates are unstable.\n";
        var hidden = summaries.Count - visible.Length;
        var suffix = hidden > 0
            ? (spanish ? $"\n{hidden} operaciones adicionales no mostradas."
                       : $"\n{hidden} additional operations not shown.")
            : string.Empty;
        PerformanceSummaryText.Text = header + guide +
            string.Join(Environment.NewLine, rows) + suffix;
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageSelection || LanguageSelector.SelectedValue is not string language)
        {
            return;
        }

        _localization.SetLanguage(language);
        RefreshConnectionStatus();
        RefreshCaptureStartOptions();
        // Inactive pages reload in ShowPage. Avoid expensive SQLite queries
        // and chart repaints for pages the user cannot see.
        if (_dashboardVisible) RefreshDashboardMetrics();
        if (BatteryContent.Visibility == Visibility.Visible) RefreshBatteryView();
        if (GridUtilityContent.Visibility == Visibility.Visible) RefreshGridUtilityView();
        if (DataContent.Visibility == Visibility.Visible) RefreshDataCoverageView();
        if (AnalysisContent.Visibility == Visibility.Visible) RefreshAnalysisView();
        if (ReportsContent.Visibility == Visibility.Visible) RefreshReportsView();
        RefreshProductExperienceLocalization();
    }


    private async void RefreshGridUtilityView()
    {
        using var measure = _performance.Measure("UI.GridUtility.Refresh");
        if (!IsInitialized || GridUtilityContent is null)
        {
            return;
        }

        var refreshGeneration =
            ++_utilityGridRefreshGeneration;

        var profile = _profiles.Get();
        if (profile is null)
        {
            UtilityTimeZoneText.Text =
                _localization.GetString("GridUtility.NoProfile");
            UtilityAddReadingButton.IsEnabled = false;
            UtilityAddBillButton.IsEnabled = false;
            UtilityReadingsGrid.ItemsSource = null;
            UtilityReconciliationGrid.ItemsSource = null;
            UtilityBillsGrid.ItemsSource = null;
            ClearUtilitySummary();
            return;
        }

        UtilityAddReadingButton.IsEnabled = true;
        UtilityAddBillButton.IsEnabled = true;

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        UtilityTimeZoneText.Text = string.Format(
            _localization.GetString("GridUtility.TimeZone"),
            timeZone);

        var nowLocal = SolarApiTime.ConvertToLocalTime(
            DateTimeOffset.UtcNow,
            timeZone);

        if (!UtilityReadingDatePicker.SelectedDate.HasValue)
        {
            UtilityReadingDatePicker.SelectedDate = nowLocal.Date;
            UtilityReadingTimeTextBox.Text = nowLocal.ToString("HH:mm");
        }

        var repository =
            _services.GetRequiredService<UtilityMeterRepository>();
        var reconciliation =
            _services.GetRequiredService<UtilityReconciliationService>();

        var readings = repository.GetReadings();
        var readingChoices = readings
            .OrderBy(item => item.ReadingAtUtc)
            .Select(item => new UtilityReadingChoice(
                item.ReadingId,
                $"{FormatUtilityReadingTimestamp(item, timeZone)} · {item.ReadingKwh:N3} kWh · {UtilityReadingSourceLabel(item.SourceKind)}"))
            .ToArray();

        var previousCompareFrom = UtilityCompareFromSelector.SelectedValue;
        var previousCompareTo = UtilityCompareToSelector.SelectedValue;
        UtilityCompareFromSelector.ItemsSource = readingChoices;
        UtilityCompareToSelector.ItemsSource = readingChoices;

        // These hidden selectors are retained only for legacy compatibility.
        // New bill entry no longer depends on pre-existing meter-reading rows.
        var previousBillFrom = UtilityBillFromReadingSelector.SelectedValue;
        var previousBillTo = UtilityBillToReadingSelector.SelectedValue;
        UtilityBillFromReadingSelector.ItemsSource = readingChoices;
        UtilityBillToReadingSelector.ItemsSource = readingChoices;
        if (previousBillFrom is long previousBillFromId &&
            readingChoices.Any(item => item.ReadingId == previousBillFromId))
        {
            UtilityBillFromReadingSelector.SelectedValue = previousBillFromId;
        }
        if (previousBillTo is long previousBillToId &&
            readingChoices.Any(item => item.ReadingId == previousBillToId))
        {
            UtilityBillToReadingSelector.SelectedValue = previousBillToId;
        }

        if (previousCompareFrom is long previousFrom &&
            readingChoices.Any(item => item.ReadingId == previousFrom))
        {
            UtilityCompareFromSelector.SelectedValue = previousFrom;
        }
        else if (readingChoices.Length >= 2)
        {
            UtilityCompareFromSelector.SelectedValue = readingChoices[^2].ReadingId;
        }

        if (previousCompareTo is long previousTo &&
            readingChoices.Any(item => item.ReadingId == previousTo))
        {
            UtilityCompareToSelector.SelectedValue = previousTo;
        }
        else if (readingChoices.Length >= 1)
        {
            UtilityCompareToSelector.SelectedValue = readingChoices[^1].ReadingId;
        }

        UtilityReadingsGrid.ItemsSource = readings
            .OrderByDescending(item => item.ReadingAtUtc)
            .Select(item => new UtilityReadingViewRow(
                item.ReadingId,
                UtilityReadingSourceLabel(item.SourceKind),
                FormatUtilityReadingTimestamp(item, timeZone),
                $"{item.ReadingKwh:N3}",
                item.Reference ?? string.Empty,
                item.Notes ?? string.Empty))
            .ToArray();

        if (UtilityReadingSourceSelector.SelectedValue is null)
        {
            UtilityReadingSourceSelector.SelectedValue =
                UtilityReadingSourceKind.Personal;
        }
        RefreshUtilityReadingSourceUi();

        var bills = repository.GetBills();
        var previousSelectedBillId =
            (UtilityBillsGrid.SelectedItem as UtilityBillViewRow)?.BillId;

        var previousAuditBill = UtilityAuditBillSelector.SelectedValue;
        var auditBillChoices = bills
            .OrderByDescending(item => item.PeriodEndUtc)
            .Select(item => new UtilityBillChoice(
                item.BillId,
                FormatAuditBillChoice(item, timeZone)))
            .ToArray();

        UtilityAuditBillSelector.ItemsSource = auditBillChoices;
        if (previousAuditBill is long previousAuditBillId &&
            auditBillChoices.Any(item => item.BillId == previousAuditBillId))
        {
            UtilityAuditBillSelector.SelectedValue = previousAuditBillId;
        }
        else if (auditBillChoices.Length > 0)
        {
            UtilityAuditBillSelector.SelectedValue = auditBillChoices[0].BillId;
        }

        UtilityBillsGrid.ItemsSource = bills
            .Select(bill =>
            {
                var totalDue = bill.TotalDueClp ?? bill.AmountClp;
                return new UtilityBillViewRow(
                    bill.BillId,
                    FormatUtilityInterval(
                        bill.PeriodStartUtc,
                        bill.PeriodEndUtc,
                        timeZone),
                    bill.BilledConsumptionKwh.HasValue
                        ? $"{bill.BilledConsumptionKwh.Value:N3}"
                        : "—",
                    "…",
                    "…",
                    "…",
                    totalDue.HasValue
                        ? $"$ {totalDue.Value:N0}"
                        : "—",
                    UtilityBillSourceLabel(bill.SourceKind),
                    UtilityBillReviewLabel(bill.ReviewState),
                    bill.InvoiceReference ?? string.Empty,
                    _localization.CurrentLanguage.StartsWith(
                        "es",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Calculando en segundo plano"
                        : "Calculating in background");
            })
            .ToArray();

        if (previousSelectedBillId.HasValue &&
            UtilityBillsGrid.ItemsSource is
                IEnumerable<UtilityBillViewRow> initialRows)
        {
            UtilityBillsGrid.SelectedItem =
                initialRows.FirstOrDefault(item =>
                    item.BillId == previousSelectedBillId.Value);
        }

        RefreshSelectedBillLines();
        RefreshTariffPublications();

        UtilityReconciliationGrid.ItemsSource = null;
        UtilityLatestIntervalText.Text =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase)
                ? "Calculando conciliaciones en segundo plano…"
                : "Calculating reconciliations in the background…";

        try
        {
            var snapshot = await Task.Run(() =>
            {
                var meter =
                    reconciliation.GetMeterReconciliations(
                        profile.DeviceId);
                var bill =
                    reconciliation.GetBillReconciliations(
                        profile.DeviceId,
                        timeZone);

                return (Meter: meter, Bill: bill);
            });

            if (refreshGeneration !=
                    _utilityGridRefreshGeneration ||
                !IsInitialized)
            {
                return;
            }

            var reconciliations = snapshot.Meter;
            UtilityReconciliationGrid.ItemsSource = reconciliations
                .OrderByDescending(item => item.ToUtc)
                .Select(item => new UtilityReconciliationViewRow(
                    FormatUtilityInterval(
                        item.FromUtc,
                        item.ToUtc,
                        timeZone),
                    item.MeterConsumptionKwh.HasValue
                        ? $"{item.MeterConsumptionKwh.Value:N3}"
                        : "—",
                    $"{item.InverterGridImportKwh:N3}",
                    item.SignedDifferenceKwh.HasValue
                        ? FormatSigned(item.SignedDifferenceKwh.Value, "kWh")
                        : "—",
                    item.DifferencePercent.HasValue
                        ? $"{item.DifferencePercent.Value:N2}%"
                        : "—",
                    $"{item.CoveragePercent:N1}%",
                    UtilityTimeBasisLabel(item.TimeBasis),
                    UtilityQualityLabel(item.Quality)))
                .ToArray();

            var latest = reconciliations.LastOrDefault();
            if (latest is null)
            {
                ClearUtilitySummary();
                UtilityLatestIntervalText.Text =
                    _localization.GetString(
                        "GridUtility.NoReconciliation");
            }
            else
            {
                DisplayUtilityReconciliation(
                    latest,
                    timeZone);
            }

            var billReconciliations = snapshot.Bill
                .ToDictionary(item => item.BillId);

            UtilityBillsGrid.ItemsSource = bills
                .Select(bill =>
                {
                    billReconciliations.TryGetValue(
                        bill.BillId,
                        out var comparison);

                    var totalDue = bill.TotalDueClp ?? bill.AmountClp;

                    return new UtilityBillViewRow(
                        bill.BillId,
                        FormatUtilityInterval(
                            bill.PeriodStartUtc,
                            bill.PeriodEndUtc,
                            timeZone),
                        bill.BilledConsumptionKwh.HasValue
                            ? $"{bill.BilledConsumptionKwh.Value:N3}"
                            : "—",
                        comparison is null
                            ? "—"
                            : $"{comparison.InverterGridImportKwh:N3}",
                        comparison?.SignedDifferenceKwh is double difference
                            ? FormatSigned(difference, "kWh")
                            : "—",
                        comparison is null
                            ? "—"
                            : $"{comparison.CoveragePercent:N1}%",
                        totalDue.HasValue
                            ? $"$ {totalDue.Value:N0}"
                            : "—",
                        UtilityBillSourceLabel(bill.SourceKind),
                        UtilityBillReviewLabel(bill.ReviewState),
                        bill.InvoiceReference ?? string.Empty,
                        comparison is null
                            ? string.Empty
                            : UtilityQualityLabel(comparison.Quality));
                })
                .ToArray();

            if (previousSelectedBillId.HasValue &&
                UtilityBillsGrid.ItemsSource is
                    IEnumerable<UtilityBillViewRow> finalRows)
            {
                UtilityBillsGrid.SelectedItem =
                    finalRows.FirstOrDefault(item =>
                        item.BillId == previousSelectedBillId.Value);
            }

            RefreshSelectedBillLines();
        }
        catch (Exception ex)
        {
            if (refreshGeneration !=
                _utilityGridRefreshGeneration)
            {
                return;
            }

            UtilityLatestIntervalText.Text =
                ex.Message;
        }
    }

    private void RefreshTariffPublications()
    {
        if (TariffPublicationsGrid is null)
        {
            return;
        }

        var publications = _services
            .GetRequiredService<TariffPublicationRepository>()
            .GetAll();

        var tariffRepository = _services
            .GetRequiredService<TariffPublicationRepository>();
        var resolutions = _services
            .GetRequiredService<TariffPublicationVersionResolver>()
            .Resolve(
                publications,
                tariffRepository.GetRelations())
            .ToDictionary(item => item.PublicationId);

        TariffPublicationsGrid.ItemsSource = publications
            .Select(item =>
            {
                resolutions.TryGetValue(
                    item.PublicationId,
                    out var resolution);

                var cne = string.Equals(
                    item.Provider,
                    "CNE_CHILE",
                    StringComparison.Ordinal);

                return new TariffPublicationViewRow(
                    cne ? "CNE" : "Enel",
                    item.EffectiveFrom.HasValue
                        ? item.EffectiveFrom.Value.ToString("dd-MM-yyyy")
                        : "—",
                    item.IsRetroactive
                        ? _localization.GetString(
                            cne
                                ? "GridUtility.TariffCorrection"
                                : "GridUtility.TariffRetroactive.Yes")
                        : _localization.GetString(
                            cne
                                ? "GridUtility.TariffStandard"
                                : "GridUtility.TariffRetroactive.No"),
                    TariffCaptureStatusLabel(item.CaptureStatus),
                    cne
                        ? "—"
                        : TariffNormalizationStatusLabel(
                            item.NormalizationStatus),
                    TariffVersionStatusLabel(
                        resolution?.Status,
                        cne),
                    item.PageCount?.ToString() ?? "—",
                    string.IsNullOrWhiteSpace(item.ContentSha256)
                        ? "—"
                        : item.ContentSha256[..Math.Min(
                            12,
                            item.ContentSha256.Length)],
                    item.Title);
            })
            .ToArray();

        if (publications.Count == 0 &&
            string.IsNullOrWhiteSpace(
                TariffCaptureStatusText.Text))
        {
            TariffCaptureStatusText.Text =
                _localization.GetString(
                    "GridUtility.TariffNotCaptured");
        }
    }

    private async void TariffCaptureButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var year = TariffYearSelector.SelectedItem is int selectedYear
            ? selectedYear
            : DateTime.Now.Year;

        TariffCaptureButton.IsEnabled = false;
        TariffBrowserEnelButton.IsEnabled = false;
        TariffImportEnelButton.IsEnabled = false;
        TariffYearSelector.IsEnabled = false;
        SetGlobalOperation(
            true,
            _localization.CurrentLanguage == "es"
                ? $"Actualizando evidencia tarifaria oficial {year}..."
                : $"Updating official tariff evidence for {year}...");

        try
        {
            var progress = new Progress<string>(
                message =>
                    TariffCaptureStatusText.Text = message);

            CneTariffEvidenceCaptureResult? cneResult = null;
            string? cneError = null;
            try
            {
                cneResult = await _services
                    .GetRequiredService<CneTariffEvidenceCaptureService>()
                    .CaptureVadIndexEvidenceAsync(
                        year,
                        progress);
            }
            catch (Exception ex)
            {
                cneError = ex.Message;
            }

            TariffCaptureResult? enelResult = null;
            string? enelError = null;
            string enelStatus;
            try
            {
                enelResult = await _services
                    .GetRequiredService<EnelTariffCaptureService>()
                    .CaptureSupplyTariffsAsync(
                        year,
                        progress);

                enelStatus = string.Format(
                    _localization.GetString(
                        "GridUtility.TariffEnelOk"),
                    enelResult.Captured,
                    enelResult.Discovered,
                    enelResult.NormalizedCandidates);
            }
            catch (Exception ex)
            {
                enelError = ex.Message;
                enelStatus = _localization.GetString(
                    "GridUtility.TariffEnelBlocked");
            }

            TariffCaptureStatusText.Text = string.Format(
                _localization.GetString(
                    "GridUtility.TariffAutoCaptureResult"),
                year,
                cneResult?.CapturedVadIndexDocuments ?? 0,
                cneResult?.Corrections ?? 0,
                cneResult?.Failed ?? (cneError is null ? 0 : 1),
                enelStatus);

            if (!string.IsNullOrWhiteSpace(cneError))
            {
                TariffCaptureStatusText.Text +=
                    $" CNE: {cneError}";
            }
            else if (cneResult is not null &&
                     cneResult.Messages.Count > 0)
            {
                TariffCaptureStatusText.Text +=
                    " CNE: " +
                    string.Join(
                        " | ",
                        cneResult.Messages.Take(3));
            }

            if (!string.IsNullOrWhiteSpace(enelError))
            {
                TariffCaptureStatusText.Text +=
                    $" Enel: {enelError}";
            }
            else if (enelResult is not null &&
                     enelResult.Messages.Count > 0)
            {
                TariffCaptureStatusText.Text +=
                    " Enel: " +
                    string.Join(
                        " | ",
                        enelResult.Messages.Take(4));
            }

            RefreshTariffPublications();
            RefreshUtilityAuditPreview();
        }
        finally
        {
            TariffCaptureButton.IsEnabled = true;
            TariffBrowserEnelButton.IsEnabled = true;
            TariffImportEnelButton.IsEnabled = true;
            TariffYearSelector.IsEnabled = true;
            SetGlobalOperation(false, string.Empty);
        }
    }

    private void TariffBrowserEnelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var window = _services
                .GetRequiredService<EnelTariffBrowserWindow>();
            window.Owner = this;
            window.TariffImported +=
                (_, _) =>
                {
                    RefreshTariffPublications();
                    RefreshUtilityAuditPreview();
                    TariffCaptureStatusText.Text =
                        _localization.CurrentLanguage == "es"
                            ? "PDF oficial Enel importado desde navegador y normalizado."
                            : "Official Enel PDF imported from browser and normalized.";
                };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            TariffCaptureStatusText.Text =
                ex.Message;
        }
    }

    private void TariffOpenEnelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(
                    EnelTariffCaptureService.OfficialArchiveUrl)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            TariffCaptureStatusText.Text = ex.Message;
        }
    }

    private async void TariffImportEnelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "PDF (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true ||
            dialog.FileNames.Length == 0)
        {
            return;
        }

        TariffCaptureButton.IsEnabled = false;
        TariffBrowserEnelButton.IsEnabled = false;
        TariffImportEnelButton.IsEnabled = false;
        TariffYearSelector.IsEnabled = false;
        SetGlobalOperation(
            true,
            _localization.CurrentLanguage == "es"
                ? "Importando PDFs oficiales Enel..."
                : "Importing official Enel PDFs...");

        try
        {
            var progress = new Progress<string>(
                message =>
                    TariffCaptureStatusText.Text = message);

            var importService = _services
                .GetRequiredService<EnelTariffPdfImportService>();
            var files = dialog.FileNames.ToArray();
            var result = await Task.Run(
                () => importService.ImportAsync(
                    files,
                    progress));

            TariffCaptureStatusText.Text = string.Format(
                _localization.GetString(
                    "GridUtility.TariffImportResult"),
                result.Imported,
                result.NormalizedCandidates,
                result.Failed);

            if (result.Messages.Count > 0)
            {
                TariffCaptureStatusText.Text +=
                    " " +
                    string.Join(
                        " | ",
                        result.Messages.Take(3));
            }

            RefreshTariffPublications();
            RefreshUtilityAuditPreview();
        }
        finally
        {
            TariffCaptureButton.IsEnabled = true;
            TariffBrowserEnelButton.IsEnabled = true;
            TariffImportEnelButton.IsEnabled = true;
            TariffYearSelector.IsEnabled = true;
            SetGlobalOperation(false, string.Empty);
        }
    }

    private string TariffCaptureStatusLabel(
        string status)
    {
        if (string.Equals(
                status,
                "CAPTURED",
                StringComparison.Ordinal))
        {
            return _localization.GetString(
                "GridUtility.TariffStatus.Captured");
        }

        if (string.Equals(
                status,
                "DISCOVERED",
                StringComparison.Ordinal))
        {
            return _localization.GetString(
                "GridUtility.TariffStatus.Discovered");
        }

        if (status.StartsWith(
                "FAILED",
                StringComparison.Ordinal))
        {
            return _localization.GetString(
                "GridUtility.TariffStatus.Failed");
        }

        return status;
    }

    private string TariffNormalizationStatusLabel(
        string status)
    {
        if (string.Equals(
                status,
                "CANDIDATES_EXTRACTED",
                StringComparison.Ordinal))
        {
            return _localization.GetString(
                "GridUtility.TariffNormalization.Candidates");
        }

        if (string.Equals(
                status,
                "NOT_NORMALIZED",
                StringComparison.Ordinal))
        {
            return _localization.GetString(
                "GridUtility.TariffNormalization.Pending");
        }

        if (status.StartsWith(
                "FAILED",
                StringComparison.Ordinal))
        {
            return _localization.GetString(
                "GridUtility.TariffNormalization.Failed");
        }

        return status;
    }

    private string TariffVersionStatusLabel(
        string? status,
        bool cne = false)
    {
        return status switch
        {
            "VERSION_SINGLE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.Single"),
            "VERSION_PREFERRED_OFFICIAL_CORRECTION" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.OfficialCorrectionPreferred"),
            "VERSION_SUPERSEDED_BY_OFFICIAL_CORRECTION" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.OfficialCorrectionSuperseded"),
            "VERSION_PREFERRED_OFFICIAL_DATE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.OfficialDatePreferred"),
            "VERSION_SUPERSEDED_BY_LATER_OFFICIAL_DATE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.OfficialDateSuperseded"),
            "VERSION_PREFERRED_EQUIVALENT_FILE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.EquivalentPreferred"),
            "VERSION_EQUIVALENT_DUPLICATE_FILE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.EquivalentDuplicate"),
            "VERSION_PREFERRED_RETROACTIVE" =>
                _localization.GetString(
                    cne
                        ? "GridUtility.TariffVersion.CneCorrection"
                        : "GridUtility.TariffVersion.RetroactivePreferred"),
            "VERSION_SUPERSEDED_BY_RETROACTIVE" =>
                _localization.GetString(
                    cne
                        ? "GridUtility.TariffVersion.CneSuperseded"
                        : "GridUtility.TariffVersion.Superseded"),
            "VERSION_AMBIGUOUS_MULTIPLE_RETROACTIVE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.Ambiguous"),
            "VERSION_AMBIGUOUS_MULTIPLE_VARIANTS" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.Ambiguous"),
            "NO_EFFECTIVE_DATE" =>
                _localization.GetString(
                    "GridUtility.TariffVersion.NoDate"),
            _ => "—"
        };
    }

    private void UtilityReadingSourceSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            RefreshUtilityReadingSourceUi();
        }
    }

    private void RefreshUtilityReadingSourceUi()
    {
        if (UtilityReadingSourceSelector is null ||
            UtilityReadingTimeTextBox is null ||
            UtilityReadingTimeHintText is null)
        {
            return;
        }

        var official = string.Equals(
            UtilityReadingSourceSelector.SelectedValue?.ToString(),
            UtilityReadingSourceKind.UtilityOfficial,
            StringComparison.Ordinal);

        UtilityReadingTimePanel.Visibility =
            official ? Visibility.Collapsed : Visibility.Visible;
        UtilityReadingTimeTextBox.IsEnabled = !official;

        if (official)
        {
            UtilityReadingTimeTextBox.Text = "00:00";
            UtilityReadingTimeHintText.Text =
                _localization.GetString(
                    "GridUtility.OfficialTimeHint");
        }
        else
        {
            UtilityReadingTimeHintText.Text =
                _localization.GetString(
                    "GridUtility.PersonalTimeHint");
        }
    }

    private void UtilityCompareReadings_Click(
        object sender,
        RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null ||
            UtilityCompareFromSelector.SelectedValue is not long fromId ||
            UtilityCompareToSelector.SelectedValue is not long toId ||
            fromId == toId)
        {
            UtilityComparisonStatusText.Text =
                _localization.GetString(
                    "GridUtility.CompareInvalid");
            return;
        }

        try
        {
            var result = _services
                .GetRequiredService<UtilityReconciliationService>()
                .ReconcileReadings(
                    profile.DeviceId,
                    fromId,
                    toId);

            if (result.ToUtc <= result.FromUtc)
            {
                UtilityComparisonStatusText.Text =
                    _localization.GetString(
                        "GridUtility.CompareInvalid");
                return;
            }

            var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
                ? "America/Santiago"
                : profile.StationTimeZone;

            DisplayUtilityReconciliation(result, timeZone);
            UtilityComparisonStatusText.Text = string.Format(
                _localization.GetString(
                    "GridUtility.CompareResult"),
                FormatUtilityInterval(
                    result.FromUtc,
                    result.ToUtc,
                    timeZone));
        }
        catch (Exception ex)
        {
            UtilityComparisonStatusText.Text = ex.Message;
        }
    }

    private void DisplayUtilityReconciliation(
        UtilityMeterReconciliation result,
        string timeZone)
    {
        UtilityLatestMeterValueText.Text =
            result.MeterConsumptionKwh.HasValue
                ? $"{result.MeterConsumptionKwh.Value:N2} kWh"
                : "—";
        UtilityLatestInverterValueText.Text =
            $"{result.InverterGridImportKwh:N2} kWh";
        UtilityLatestDifferenceValueText.Text =
            result.SignedDifferenceKwh.HasValue
                ? FormatSigned(
                    result.SignedDifferenceKwh.Value,
                    "kWh")
                : "—";
        UtilityLatestCoverageValueText.Text =
            $"{result.CoveragePercent:N1}%";
        UtilityLatestIntervalText.Text =
            $"{FormatUtilityInterval(result.FromUtc, result.ToUtc, timeZone)} · " +
            $"{UtilityTimeBasisLabel(result.TimeBasis)} · " +
            $"{UtilityQualityLabel(result.Quality)}" +
            (result.Sensitivity is null
                ? string.Empty
                : $"{Environment.NewLine}{UtilitySensitivityLabel(result.Sensitivity)}");
    }

    private async void UtilityExportReconciliation_Click(
        object sender,
        RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null ||
            UtilityCompareFromSelector.SelectedValue is not long fromId ||
            UtilityCompareToSelector.SelectedValue is not long toId ||
            fromId == toId)
        {
            UtilityComparisonStatusText.Text =
                _localization.GetString(
                    "GridUtility.CompareInvalid");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = _localization.GetString(
                "GridUtility.ExportReconciliationTitle"),
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            FileName = $"Conciliacion-Enel-{DateTime.Now:yyyyMMdd-HHmm}.pdf"
        };

        PrepareExportDialog(dialog);

        if (dialog.ShowDialog(this) != true)
            return;

        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);
        UtilityComparisonStatusText.Text = spanish
            ? "Generando PDF de conciliación..."
            : "Generating reconciliation PDF...";
        SetGlobalOperation(
            true,
            spanish ? "Exportando conciliación..." : "Exporting reconciliation...");

        try
        {
            var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
                ? "America/Santiago"
                : profile.StationTimeZone;
            var reportService =
                _services.GetRequiredService<UtilityReconciliationReportService>();

            await Task.Run(() =>
                reportService.ExportPdf(
                    dialog.FileName,
                    profile.DeviceId,
                    fromId,
                    toId,
                    timeZone,
                    _localization.CurrentLanguage));

            UtilityComparisonStatusText.Text = string.Format(
                _localization.GetString(
                    "GridUtility.ExportReconciliationSaved"),
                dialog.FileName);
        }
        catch (Exception ex)
        {
            UtilityComparisonStatusText.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                _localization.GetString(
                    "Page.GridUtility.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetGlobalOperation(false, string.Empty);
        }
    }

    private void UtilityAddReading_Click(
        object sender,
        RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null)
        {
            UtilityReadingStatusText.Text =
                _localization.GetString("GridUtility.NoProfile");
            return;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var sourceKind =
            UtilityReadingSourceSelector.SelectedValue?.ToString()
            ?? UtilityReadingSourceKind.Personal;
        var isOfficial = string.Equals(
            sourceKind,
            UtilityReadingSourceKind.UtilityOfficial,
            StringComparison.Ordinal);

        if (!TryParseUtilityLocalInstant(
                UtilityReadingDatePicker,
                UtilityReadingTimeTextBox,
                timeZone,
                out var readingAtUtc))
        {
            UtilityReadingStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidDateTime");
            return;
        }

        if (!TryParseRequiredNonNegative(
                UtilityReadingKwhTextBox.Text,
                out var readingKwh))
        {
            UtilityReadingStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidNumber");
            return;
        }

        try
        {
            _services
                .GetRequiredService<UtilityMeterRepository>()
                .AddReading(
                    readingAtUtc,
                    readingKwh,
                    UtilityReadingReferenceTextBox.Text,
                    UtilityReadingNotesTextBox.Text,
                    sourceKind,
                    isOfficial
                        ? UtilityTimePrecision.DateOnly
                        : UtilityTimePrecision.Exact,
                    isOfficial
                        ? UtilityTimeAssumption.StartOfDayAssumed
                        : UtilityTimeAssumption.Exact);

            UtilityReadingKwhTextBox.Clear();
            UtilityReadingReferenceTextBox.Clear();
            UtilityReadingNotesTextBox.Clear();
            UtilityReadingStatusText.Text =
                _localization.GetString(
                    "GridUtility.ReadingSaved");
            RefreshGridUtilityView();
        }
        catch (Exception ex)
        {
            UtilityReadingStatusText.Text = ex.Message;
        }
    }

    private void UtilityDeleteReading_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityReadingsGrid.SelectedItem
            is not UtilityReadingViewRow selected)
        {
            return;
        }

        if (MessageBox.Show(
                _localization.GetString(
                    "GridUtility.ConfirmDeleteReading"),
                _localization.GetString(
                    "Page.GridUtility.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            return;
        }

        _services
            .GetRequiredService<UtilityMeterRepository>()
            .DeleteReading(selected.ReadingId);

        UtilityReadingStatusText.Text =
            _localization.GetString(
                "GridUtility.ReadingDeleted");
        RefreshGridUtilityView();
    }

    private void UtilityBillDatePicker_SelectedDateChanged(
        object? sender,
        EventArgs e)
    {
        if (!IsInitialized)
            return;

        TryAutoMatchBillReadings();
    }

    private void TryAutoMatchBillReadings()
    {
        var profile = _profiles.Get();
        if (profile is null ||
            !UtilityBillStartDatePicker.SelectedDate.HasValue ||
            !UtilityBillEndDatePicker.SelectedDate.HasValue)
        {
            return;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var startDate = UtilityBillStartDatePicker.SelectedDate.Value.Date;
        var endDate = UtilityBillEndDatePicker.SelectedDate.Value.Date;
        if (endDate <= startDate)
            return;

        var readings = _services
            .GetRequiredService<UtilityMeterRepository>()
            .GetReadings()
            .Where(item => string.Equals(
                item.SourceKind,
                UtilityReadingSourceKind.UtilityOfficial,
                StringComparison.Ordinal))
            .ToArray();

        var from = readings
            .Where(item =>
                SolarApiTime.ConvertToLocalTime(
                    item.ReadingAtUtc,
                    timeZone).Date == startDate)
            .OrderByDescending(item => item.UpdatedUtc)
            .FirstOrDefault();

        var to = readings
            .Where(item =>
                SolarApiTime.ConvertToLocalTime(
                    item.ReadingAtUtc,
                    timeZone).Date == endDate)
            .OrderByDescending(item => item.UpdatedUtc)
            .FirstOrDefault();

        if (from is null || to is null || to.ReadingAtUtc <= from.ReadingAtUtc)
            return;

        UtilityBillFromReadingSelector.SelectedValue = from.ReadingId;
        UtilityBillToReadingSelector.SelectedValue = to.ReadingId;
        UtilityBillStatusText.Text =
            _localization.GetString(
                "GridUtility.BillReadingsSuggested");
    }

    private void UtilityAuditBillSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IsInitialized &&
            UtilityAuditVerificationGrid?.IsVisible == true)
        {
            RefreshUtilityAuditPreview();
        }
    }

    private void GridUtilityTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsInitialized ||
            e.Source != sender)
        {
            return;
        }

        if (ReferenceEquals(
                GridUtilityTabControl.SelectedItem,
                UtilityAuditTab))
        {
            RefreshUtilityAuditPreview();
        }
    }

    private async void RefreshUtilityAuditPreview()
    {
        if (UtilityAuditVerificationGrid is null ||
            UtilityAuditStatusText is null)
        {
            return;
        }

        if (UtilityAuditBillSelector.SelectedValue is not long billId)
        {
            UtilityAuditVerificationGrid.ItemsSource = null;
            ClearUtilityBillSummary();
            UtilityAuditStatusText.Text =
                _localization.GetString(
                    "GridUtility.AuditEvidencePending");
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
        {
            UtilityAuditVerificationGrid.ItemsSource = null;
            ClearUtilityBillSummary();
            UtilityAuditStatusText.Text =
                _localization.GetString("GridUtility.NoProfile");
            return;
        }

        var refreshGeneration =
            ++_utilityAuditRefreshGeneration;

        try
        {
            var timeZone = string.IsNullOrWhiteSpace(
                    profile.StationTimeZone)
                ? "America/Santiago"
                : profile.StationTimeZone;

            UtilityAuditStatusText.Text =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Calculando conciliación observada…"
                    : "Calculating observed reconciliation…";

            await RefreshUtilityBillSummaryAsync(
                profile.DeviceId,
                billId,
                timeZone,
                refreshGeneration);

            if (refreshGeneration !=
                    _utilityAuditRefreshGeneration ||
                UtilityAuditBillSelector.SelectedValue is not long
                    selectedBillId ||
                selectedBillId != billId)
            {
                return;
            }

            var repository =
                _services.GetRequiredService<UtilityMeterRepository>();
            var billLines = repository.GetBillLines(billId);

            if (billLines.Count == 0)
            {
                UtilityAuditVerificationGrid.ItemsSource = null;
                UtilityAuditStatusText.Text =
                    _localization.GetString(
                        "GridUtility.AuditNoBillLines");
                return;
            }

            var auditService = _services
                .GetRequiredService<UtilityBillAuditV2Service>();
            var audit = await Task.Run(
                () => auditService.Analyze(
                    billId,
                    timeZone));

            if (refreshGeneration !=
                    _utilityAuditRefreshGeneration ||
                UtilityAuditBillSelector.SelectedValue is not long
                    auditSelectedBillId ||
                auditSelectedBillId != billId)
            {
                return;
            }

            UtilityAuditVerificationGrid.ItemsSource =
                audit.Lines
                    .Select(item =>
                        new UtilityAuditVerificationViewRow(
                            item.Description,
                            item.PrintedUnitRateClp.HasValue
                                ? $"$ {item.PrintedUnitRateClp.Value:N3}"
                                : "—",
                            FormatAuditCalculationBasis(item),
                            AuditVerificationStatusLabel(item.Status),
                            item.Source,
                            UtilityBillAuditEvidenceLabel(item.Evidence),
                            $"$ {item.ActualAmountClp:+#,##0;-#,##0;0}",
                            item.ReconstructedAmountClp.HasValue
                                ? $"$ {item.ReconstructedAmountClp.Value:N0}"
                                : "—",
                            item.AmountDifferenceClp.HasValue
                                ? $"$ {item.AmountDifferenceClp.Value:+0;-0;0}"
                                : "—"))
                    .ToArray();

            var verified = audit.Lines.Count(item =>
                item.Status.StartsWith(
                    "VERIFIED_",
                    StringComparison.Ordinal) ||
                item.Status ==
                    "RATE_VERIFIED_SOURCE_UNIQUE" ||
                item.Status.StartsWith(
                    "BILL_AMOUNT_RECONCILED",
                    StringComparison.Ordinal) &&
                !item.Status.Contains(
                    "AMBIGUOUS",
                    StringComparison.Ordinal) ||
                item.Status.StartsWith(
                    "FIXED_AMOUNT_RECONCILED",
                    StringComparison.Ordinal) &&
                !item.Status.Contains(
                    "AMBIGUOUS", StringComparison.Ordinal) ||
                item.Status == "VAT_RECONSTRUCTED_19");

            var spanish =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase);

            UtilityAuditReconstructionCoverageText.Text =
                $"{audit.ReconstructionCoveragePercent:N1}%";

            UtilityAuditVatCheckText.Text =
                audit.TaxStatus switch
                {
                    "IVA_MATCH_19" =>
                        spanish
                            ? $"OK · $ {audit.PrintedIvaClp.GetValueOrDefault():N0} = 19% de la base afecta"
                            : $"OK · $ {audit.PrintedIvaClp.GetValueOrDefault():N0} = 19% of taxable base",
                    "IVA_DIFFERENCE" =>
                        spanish
                            ? $"Revisar · impreso $ {audit.PrintedIvaClp.GetValueOrDefault():N0} vs calculado $ {audit.ExpectedIvaClp.GetValueOrDefault():N0}"
                            : $"Review · printed $ {audit.PrintedIvaClp.GetValueOrDefault():N0} vs calculated $ {audit.ExpectedIvaClp.GetValueOrDefault():N0}",
                    "IVA_BASE_MISSING" =>
                        spanish
                            ? "Falta base afecta para reconstruir el 19%"
                            : "Taxable base missing for 19% reconstruction",
                    _ =>
                        spanish
                            ? "IVA no registrado en la boleta"
                            : "VAT not recorded on the bill"
                };

            var summaryBalance =
                audit.SummaryBalanceStatus switch
                {
                    "SUMMARY_BALANCED" =>
                        spanish
                            ? "Resumen impreso: OK"
                            : "Printed summary: OK",
                    "SUMMARY_GROSS_DIFFERENCE" =>
                        spanish
                            ? $"Resumen: Total boleta difiere $ {audit.GrossBillDifferenceClp.GetValueOrDefault():+0;-0;0}"
                            : $"Summary: gross bill differs $ {audit.GrossBillDifferenceClp.GetValueOrDefault():+0;-0;0}",
                    "SUMMARY_TOTAL_DIFFERENCE" =>
                        spanish
                            ? $"Resumen: Total a pagar difiere $ {audit.SummaryTotalDifferenceClp.GetValueOrDefault():+0;-0;0}"
                            : $"Summary: total due differs $ {audit.SummaryTotalDifferenceClp.GetValueOrDefault():+0;-0;0}",
                    _ =>
                        spanish
                            ? "Resumen impreso: datos incompletos"
                            : "Printed summary: incomplete data"
                };

            var detailBalance =
                audit.BalanceStatus switch
                {
                    "BALANCED" =>
                        Math.Abs(audit.SimpleAdjustmentClp) > 0.0001
                            ? spanish
                                ? $"Detalle: cuadra · ajuste impreso $ {audit.SimpleAdjustmentClp:+0;-0;0}"
                                : $"Detail: balanced · printed adjustment $ {audit.SimpleAdjustmentClp:+0;-0;0}"
                            : spanish
                                ? "Detalle: cuadra sin residuo"
                                : "Detail: balanced with no residual",
                    "SMALL_UNEXPLAINED_RESIDUAL" =>
                        audit.UnexplainedResidualClp.GetValueOrDefault() < 0
                            ? spanish
                                ? $"Detalle: excede el total por $ {Math.Abs(audit.UnexplainedResidualClp.GetValueOrDefault()):N0} · sin ajuste impreso"
                                : $"Detail: exceeds total by $ {Math.Abs(audit.UnexplainedResidualClp.GetValueOrDefault()):N0} · no printed adjustment"
                            : spanish
                                ? $"Detalle: faltan $ {audit.UnexplainedResidualClp.GetValueOrDefault():N0} · sin ajuste impreso"
                                : $"Detail: short by $ {audit.UnexplainedResidualClp.GetValueOrDefault():N0} · no printed adjustment",
                    "MATERIAL_UNEXPLAINED_DIFFERENCE" =>
                        spanish
                            ? $"Detalle: diferencia no explicada $ {audit.UnexplainedResidualClp.GetValueOrDefault():+0;-0;0}"
                            : $"Detail: unexplained difference $ {audit.UnexplainedResidualClp.GetValueOrDefault():+0;-0;0}",
                    _ =>
                        spanish
                            ? "Detalle: total insuficiente para cuadratura"
                            : "Detail: insufficient total for balance check"
                };

            UtilityAuditBalanceText.Text =
                $"{summaryBalance} · {detailBalance}";

            var reconstructed = audit.Lines.Count(item =>
                item.ReconstructedAmountClp.HasValue);
            var withAmbiguousApplicability = audit.Lines.Count(item =>
                item.ReconstructedAmountClp.HasValue &&
                item.Status.Contains("AMBIGUOUS", StringComparison.Ordinal));
            UtilityAuditStatusText.Text =
                string.Format(
                    _localization.GetString(
                        "GridUtility.AuditPreviewSummary"),
                    verified,
                    audit.Lines.Count - verified) +
                (spanish
                    ? $" · {reconstructed} línea(s) con cálculo; " +
                      $"{withAmbiguousApplicability} con aplicabilidad tarifaria pendiente"
                    : $" · {reconstructed} line(s) with calculation; " +
                      $"{withAmbiguousApplicability} with unresolved tariff applicability");
        }
        catch (Exception ex)
        {
            UtilityAuditVerificationGrid.ItemsSource = null;
            UtilityAuditStatusText.Text = ex.Message;
        }
    }

    private void ClearUtilityBillSummary()
    {
        if (UtilityBillSummaryActualTotalText is null)
            return;

        UtilityBillSummaryActualTotalText.Text = "—";
        UtilityBillSummaryBilledKwhText.Text = "—";
        UtilityBillSummaryObservedKwhText.Text = "—";
        UtilityBillSummaryEstimatedTotalText.Text = "—";
        UtilityBillSummaryDifferenceText.Text = "—";
        UtilityBillSummaryDetailText.Text = string.Empty;
        UtilityBillSummarySourceText.Text = string.Empty;

        if (UtilityAuditReconstructionCoverageText is not null)
            UtilityAuditReconstructionCoverageText.Text = "—";
        if (UtilityAuditVatCheckText is not null)
            UtilityAuditVatCheckText.Text = "—";
        if (UtilityAuditBalanceText is not null)
            UtilityAuditBalanceText.Text = "—";
    }

    private async Task RefreshUtilityBillSummaryAsync(
        string deviceId,
        long billId,
        string timeZoneId,
        int refreshGeneration)
    {
        if (UtilityBillSummaryActualTotalText is null)
            return;

        try
        {
            var summaryService = _services
                .GetRequiredService<
                    UtilityBillReconciliationSummaryService>();
            var key =
                (deviceId, billId, timeZoneId);

            Task<UtilityBillReconciliationSummary> summaryTask;
            if (_utilityBillSummaryInFlight is
                    { IsCompleted: false } currentTask &&
                _utilityBillSummaryInFlightKey == key)
            {
                summaryTask = currentTask;
            }
            else
            {
                summaryTask = Task.Run(
                    () => summaryService.Analyze(
                        deviceId,
                        billId,
                        timeZoneId));
                _utilityBillSummaryInFlightKey = key;
                _utilityBillSummaryInFlight = summaryTask;
            }

            var summary = await summaryTask;

            if (ReferenceEquals(
                    _utilityBillSummaryInFlight,
                    summaryTask))
            {
                _utilityBillSummaryInFlight = null;
                _utilityBillSummaryInFlightKey = null;
            }

            if (refreshGeneration !=
                    _utilityAuditRefreshGeneration ||
                UtilityAuditBillSelector.SelectedValue is not long
                    selectedBillId ||
                selectedBillId != billId)
            {
                return;
            }

            UtilityBillSummaryActualTotalText.Text =
                summary.ActualBillTotalClp.HasValue
                    ? $"$ {summary.ActualBillTotalClp.Value:N0}"
                    : "—";

            UtilityBillSummaryBilledKwhText.Text =
                summary.BilledKwh.HasValue
                    ? $"{summary.BilledKwh.Value:N3} kWh"
                    : "—";

            UtilityBillSummaryObservedKwhText.Text =
                $"{summary.ObservedInverterKwh:N3} kWh";

            UtilityBillSummaryEstimatedTotalText.Text =
                summary.EstimatedObservedTotalClp.HasValue
                    ? $"$ {summary.EstimatedObservedTotalClp.Value:N0}"
                    : "—";

            UtilityBillSummaryDifferenceText.Text =
                summary.ActualMinusEstimatedObservedClp.HasValue
                    ? $"$ {summary.ActualMinusEstimatedObservedClp.Value:+#,##0;-#,##0;0}"
                    : "—";

            var spanish =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase);

            var energyDifference =
                summary.BilledMinusObservedKwh.HasValue
                    ? $"{summary.BilledMinusObservedKwh.Value:+0.000;-0.000;0.000} kWh"
                    : "—";
            var energyPercent =
                summary.BilledMinusObservedPercent.HasValue
                    ? $"{summary.BilledMinusObservedPercent.Value:+0.00;-0.00;0.00}%"
                    : "—";
            var rate =
                summary.SupportedVariableRateClpPerKwh.HasValue
                    ? $"$ {summary.SupportedVariableRateClpPerKwh.Value:N3}/kWh"
                    : "—";

            var fixedSupported =
                summary.TariffAnalysis.SupportedFixedAmountClp.HasValue &&
                summary.TariffAnalysis.SupportedFixedAmountClp.Value > 0.0001
                    ? $"$ {summary.TariffAnalysis.SupportedFixedAmountClp.Value:N0}"
                    : "—";

            var ivaDetail =
                summary.PrintedIvaClp.HasValue &&
                summary.ExpectedIvaClp.HasValue
                    ? (spanish
                        ? $" · IVA impreso $ {summary.PrintedIvaClp.Value:N0} vs 19% reconstruido $ {summary.ExpectedIvaClp.Value:N0} (dif. $ {summary.IvaDifferenceClp.GetValueOrDefault():+0;-0;0})"
                        : $" · printed VAT $ {summary.PrintedIvaClp.Value:N0} vs reconstructed 19% $ {summary.ExpectedIvaClp.Value:N0} (diff. $ {summary.IvaDifferenceClp.GetValueOrDefault():+0;-0;0})")
                    : string.Empty;
            var adjustmentDetail =
                Math.Abs(summary.SimpleAdjustmentClp) > 0.0001
                    ? (spanish
                        ? $" · ajuste sencillo $ {summary.SimpleAdjustmentClp:+0;-0;0}"
                        : $" · simple adjustment $ {summary.SimpleAdjustmentClp:+0;-0;0}")
                    : string.Empty;

            UtilityBillSummaryDetailText.Text =
                spanish
                    ? $"Enel − inversor observado: {energyDifference} ({energyPercent}) · cobertura {summary.CoveragePercent:N2}% · tasa variable oficial soportada {rate} · cargo fijo oficial reconstruido {fixedSupported}. La estimación monetaria recalcula componentes tarifarios sustentados y preserva explícitamente los no reconstruibles; origen {summary.BillSourceKind}, revisión {summary.ReviewState}.{ivaDetail}{adjustmentDetail}"
                    : $"Utility − observed inverter: {energyDifference} ({energyPercent}) · coverage {summary.CoveragePercent:N2}% · supported official variable rate {rate} · reconstructed official fixed charge {fixedSupported}. The monetary estimate reconstructs supported tariff components and explicitly preserves non-reconstructable items; source {summary.BillSourceKind}, review {summary.ReviewState}.{ivaDetail}{adjustmentDetail}";

            if (!summary.TariffAnalysis.HasTariffModel)
            {
                UtilityBillSummarySourceText.Text =
                    spanish
                        ? "Estimación monetaria no disponible: falta un modelo tarifario oficial reconciliado para esta boleta."
                        : "Monetary estimate unavailable: no reconciled official tariff model is available for this bill.";
                return;
            }

            var sources = summary.TariffAnalysis.PublicationPeriods
                .Select(item =>
                {
                    var hash = string.IsNullOrWhiteSpace(
                            item.ContentSha256)
                        ? "SHA —"
                        : $"SHA {item.ContentSha256[..Math.Min(12, item.ContentSha256.Length)]}…";
                    return
                        $"{item.AppliedFrom:dd-MM-yyyy}→{item.AppliedTo:dd-MM-yyyy} · {item.PublicationTitle} · {hash}";
                })
                .ToArray();

            UtilityBillSummarySourceText.Text =
                (spanish
                    ? "Fuente tarifaria: "
                    : "Tariff source: ") +
                string.Join(" | ", sources);

            if (!string.IsNullOrWhiteSpace(
                    summary.Limitation))
            {
                UtilityBillSummarySourceText.Text +=
                    (spanish
                        ? " · Limitación: "
                        : " · Limitation: ") +
                    summary.Limitation;
            }
        }
        catch (Exception ex)
        {
            if (refreshGeneration !=
                _utilityAuditRefreshGeneration)
            {
                return;
            }

            ClearUtilityBillSummary();
            UtilityBillSummaryDetailText.Text =
                ex.Message;
        }
    }

    private string FormatAuditCalculationBasis(
        UtilityBillLineAuditV2 item)
    {
        if (!string.IsNullOrWhiteSpace(
                item.CalculationDetail))
        {
            return item.CalculationDetail;
        }

        if (!item.CalculationQuantity.HasValue)
        {
            return "—";
        }

        var unit = string.IsNullOrWhiteSpace(item.CalculationUnit)
            ? string.Empty
            : $" {item.CalculationUnit}";

        var source = item.CalculationQuantitySource switch
        {
            "BILL_BILLED_KWH" =>
                _localization.GetString(
                    "GridUtility.AuditQuantitySource.Bill"),
            "BILL_LINE" =>
                _localization.GetString(
                    "GridUtility.AuditQuantitySource.Line"),
            _ =>
                _localization.GetString(
                    "GridUtility.AuditQuantitySource.Derived")
        };

        return $"{item.CalculationQuantity.Value:N3}{unit} · {source}";
    }

    private string FormatAuditBillChoice(
        UtilityBillRecord bill,
        string timeZoneId)
    {
        var fromLocal = SolarApiTime.ConvertToLocalTime(
            bill.PeriodStartUtc,
            timeZoneId);
        var toLocal = SolarApiTime.ConvertToLocalTime(
            bill.PeriodEndUtc,
            timeZoneId);

        var interval =
            $"{fromLocal:dd-MM-yyyy} → {toLocal:dd-MM-yyyy}";

        var identityParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(bill.InvoiceReference))
        {
            identityParts.Add(bill.InvoiceReference.Trim());
        }

        if (bill.BilledConsumptionKwh.HasValue)
        {
            identityParts.Add(
                $"{bill.BilledConsumptionKwh.Value:N1} kWh");
        }

        var total = bill.TotalDueClp ?? bill.AmountClp;
        if (total.HasValue)
        {
            identityParts.Add(
                $"$ {total.Value:N0}");
        }

        if (identityParts.Count == 0)
        {
            identityParts.Add(
                _localization.GetString(
                    "GridUtility.AuditNoBillIdentity"));
        }

        return $"{interval} · {string.Join(" · ", identityParts)}";
    }

    private string FormatAuditTariffPublication(
        BillTariffPublicationEvidence publication)
    {
        var effective = publication.EffectiveFrom.HasValue
            ? publication.EffectiveFrom.Value.ToString("yyyy-MM")
            : _localization.GetString(
                "GridUtility.TariffVersion.NoDate");
        var revision = publication.IsRetroactive
            ? _localization.GetString(
                "GridUtility.TariffRetroactive.Yes")
            : _localization.GetString(
                "GridUtility.TariffRetroactive.No");

        return $"{effective} · {revision} · {publication.Title}";
    }

    private string AuditVerificationStatusLabel(
        string status) =>
        status switch
        {
            "VERIFIED_RECONSTRUCTED_SOURCE_UNIQUE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.Reconstructed"),
            "BILL_AMOUNT_RECONCILED" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.Reconstructed"),
            "BILL_AMOUNT_RECONCILED_MULTI_PERIOD" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.Reconstructed"),
            "BILL_AMOUNT_RECONCILED_COMPOSITE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.Reconstructed"),
            "BILL_AMOUNT_RECONCILED_MULTI_PERIOD_COMPOSITE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.Reconstructed"),
            "BILL_AMOUNT_RECONCILED_RATE_APPLICABILITY_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ReconstructedAmbiguous"),
            "BILL_AMOUNT_RECONCILED_MULTI_PERIOD_APPLICABILITY_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ReconstructedAmbiguous"),
            "FIXED_AMOUNT_RECONCILED_MULTI_PERIOD_APPLICABILITY_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ReconstructedAmbiguous"),
            "FIXED_AMOUNT_RECONCILED_APPLICABILITY_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ReconstructedAmbiguous"),
            "VERIFIED_RECONSTRUCTED_APPLICABILITY_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ReconstructedAmbiguous"),
            "RATE_VERIFIED_SOURCE_UNIQUE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.RateVerified"),
            "RATE_VERIFIED_APPLICABILITY_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.RateVerifiedAmbiguous"),
            "ACTUAL_ONLY_UNMAPPED" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ActualOnly"),
            "VAT_RECONSTRUCTED_19" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.Vat19"),
            "VAT_BASE_MISSING" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.VatBaseMissing"),
            "PRINTED_SIMPLE_ADJUSTMENT" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.SimpleAdjustment"),
            "ACTUAL_ONLY_NO_UNIT_RATE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.NoRate"),
            "OFFICIAL_RATE_DERIVATION_PENDING" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.DerivationPending"),
            "OFFICIAL_RATE_DERIVATION_MULTI_PERIOD" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.DerivationMultiPeriod"),
            "MISSING_TARIFF_SOURCE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.MissingSource"),
            "TARIFF_VERSION_AMBIGUOUS" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.AmbiguousVersion"),
            "TARIFF_NOT_NORMALIZED" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.NotNormalized"),
            "MISSING_COMPONENT_SOURCE" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.ComponentMissing"),
            "RATE_NOT_FOUND" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.RateNotFound"),
            "TARIFF_INTERVAL_INVALID" =>
                _localization.GetString(
                    "GridUtility.AuditStatus.InvalidInterval"),
            _ => status
        };

    private void SetAuditExportProgress(int progress, string label)
    {
        UtilityAuditExportProgressBar.Visibility = Visibility.Visible;
        UtilityAuditExportProgressText.Visibility = Visibility.Visible;
        UtilityAuditExportProgressBar.Value = progress;
        UtilityAuditExportProgressText.Text = label;
    }

    private async void UtilityExportBillAudit_Click(
        object sender,
        RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null ||
            UtilityAuditBillSelector.SelectedValue is not long billId)
        {
            UtilityAuditStatusText.Text =
                _localization.GetString("GridUtility.AuditSelectBill");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = _localization.GetString("GridUtility.ExportBillAuditTitle"),
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            FileName = $"Auditoria-Boleta-Enel-{DateTime.Now:yyyyMMdd-HHmm}.pdf"
        };

        PrepareExportDialog(dialog);

        if (dialog.ShowDialog(this) != true)
            return;

        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

        SetAuditExportProgress(15, spanish
            ? "Paso 1/3 · Preparando auditoría"
            : "Step 1/3 · Preparing audit");
        UtilityExportBillAuditButton.IsEnabled = false;
        UtilityExportBillAnnexButton.IsEnabled = false;
        UtilityAuditStatusText.Text = spanish
            ? "Generando auditoría de boleta..."
            : "Generating bill audit...";
        SetGlobalOperation(
            true,
            spanish
                ? "Exportando auditoría de boleta..."
                : "Exporting bill audit...");

        try
        {
            SetAuditExportProgress(50, spanish
                ? "Paso 2/3 · Generando archivo"
                : "Step 2/3 · Generating file");
            var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
                ? "America/Santiago"
                : profile.StationTimeZone;

            var service =
                _services.GetRequiredService<UtilityBillAuditReportService>();

            await Task.Run(() =>
                service.ExportPdf(
                    dialog.FileName,
                    profile.DeviceId,
                    billId,
                    timeZone,
                    _localization.CurrentLanguage,
                    ReportProducerIdentity()));

            SetAuditExportProgress(100, spanish
                ? "Paso 3/3 · PDF guardado: " + dialog.FileName
                : "Step 3/3 · PDF saved: " + dialog.FileName);
            System.Media.SystemSounds.Asterisk.Play();
            UtilityAuditStatusText.Text = string.Format(
                _localization.GetString("GridUtility.ExportBillAuditSaved"),
                dialog.FileName);
        }
        catch (Exception ex)
        {
            SetAuditExportProgress(100, spanish
                ? "Error de exportación: " + ex.Message
                : "Export failed: " + ex.Message);
            UtilityAuditStatusText.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                _localization.GetString("GridUtility.AuditHeading"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UtilityExportBillAuditButton.IsEnabled = true;
            UtilityExportBillAnnexButton.IsEnabled = true;
            SetGlobalOperation(false, string.Empty);
        }
    }

    private async void UtilityExportBillAnnex_Click(
        object sender,
        RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null ||
            UtilityAuditBillSelector.SelectedValue is not long billId)
        {
            UtilityAuditStatusText.Text =
                _localization.GetString(
                    "GridUtility.AuditSelectBill");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = _localization.GetString(
                "GridUtility.ExportBillAnnexTitle"),
            Filter = "ZIP (*.zip)|*.zip",
            DefaultExt = "zip",
            AddExtension = true,
            FileName =
                $"Anexo-Tecnico-Boleta-Enel-{DateTime.Now:yyyyMMdd-HHmm}.zip"
        };

        PrepareExportDialog(dialog);

        if (dialog.ShowDialog(this) != true)
            return;

        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

        SetAuditExportProgress(15, spanish
            ? "Paso 1/3 · Preparando anexo"
            : "Step 1/3 · Preparing annex");
        UtilityExportBillAnnexButton.IsEnabled = false;
        UtilityExportBillAuditButton.IsEnabled = false;
        UtilityAuditStatusText.Text =
            spanish
                ? "Generando anexo técnico y datos..."
                : "Generating technical annex and data...";
        SetGlobalOperation(
            true,
            spanish
                ? "Exportando anexo técnico..."
                : "Exporting technical annex...");

        try
        {
            SetAuditExportProgress(50, spanish
                ? "Paso 2/3 · Generando archivo"
                : "Step 2/3 · Generating file");
            var timeZone =
                string.IsNullOrWhiteSpace(
                    profile.StationTimeZone)
                    ? "America/Santiago"
                    : profile.StationTimeZone;

            var service =
                _services.GetRequiredService<
                    UtilityBillAuditAnnexExportService>();

            await Task.Run(
                () =>
                    service.ExportZip(
                        dialog.FileName,
                        profile.DeviceId,
                        billId,
                        timeZone,
                        _localization.CurrentLanguage,
                        ReportProducerIdentity()));

            SetAuditExportProgress(100, spanish
                ? "Paso 3/3 · ZIP guardado: " + dialog.FileName
                : "Step 3/3 · ZIP saved: " + dialog.FileName);
            System.Media.SystemSounds.Asterisk.Play();
            UtilityAuditStatusText.Text =
                string.Format(
                    _localization.GetString(
                        "GridUtility.ExportBillAnnexSaved"),
                    dialog.FileName);
        }
        catch (Exception ex)
        {
            SetAuditExportProgress(100, spanish
                ? "Error de exportación: " + ex.Message
                : "Export failed: " + ex.Message);
            UtilityAuditStatusText.Text =
                ex.Message;
            MessageBox.Show(
                ex.Message,
                _localization.GetString(
                    "GridUtility.AuditHeading"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UtilityExportBillAnnexButton.IsEnabled = true;
            UtilityExportBillAuditButton.IsEnabled = true;
            SetGlobalOperation(
                false,
                string.Empty);
        }
    }

    private static string ReportProducerIdentity()
    {
        var assembly =
            System.Reflection.Assembly.GetEntryAssembly();
        var informational =
            assembly?
                .GetCustomAttributes(
                    typeof(
                        System.Reflection.AssemblyInformationalVersionAttribute),
                    false)
                .OfType<
                    System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?
                .InformationalVersion;

        return !string.IsNullOrWhiteSpace(
                informational)
            ? informational
            : assembly?.GetName().Version?.ToString()
              ?? "unknown";
    }

    private void UtilityAddBill_Click(
        object sender,
        RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null)
        {
            UtilityBillStatusText.Text =
                _localization.GetString("GridUtility.NoProfile");
            return;
        }

        var repository =
            _services.GetRequiredService<UtilityMeterRepository>();
        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var periodPrecision =
            UtilityBillPeriodPrecisionSelector.SelectedValue?.ToString()
            ?? UtilityTimePrecision.DateOnly;

        DateTimeOffset startUtc;
        DateTimeOffset endUtc;

        if (string.Equals(
                periodPrecision,
                UtilityTimePrecision.DateOnly,
                StringComparison.Ordinal))
        {
            if (!TryParseUtilityLocalDateBoundary(
                    UtilityBillStartDatePicker,
                    timeZone,
                    out startUtc) ||
                !TryParseUtilityLocalDateBoundary(
                    UtilityBillEndDatePicker,
                    timeZone,
                    out endUtc) ||
                endUtc <= startUtc)
            {
                UtilityBillStatusText.Text =
                    _localization.GetString(
                        "GridUtility.InvalidDateTime");
                return;
            }
        }
        else if (!TryParseUtilityLocalInstant(
                     UtilityBillStartDatePicker,
                     UtilityBillStartTimeTextBox,
                     timeZone,
                     out startUtc) ||
                 !TryParseUtilityLocalInstant(
                     UtilityBillEndDatePicker,
                     UtilityBillEndTimeTextBox,
                     timeZone,
                     out endUtc) ||
                 endUtc <= startUtc)
        {
            UtilityBillStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidDateTime");
            return;
        }

        if (!TryParseOptionalNonNegative(
                UtilityBillMeterStartTextBox.Text,
                out var meterStart) ||
            !TryParseOptionalNonNegative(
                UtilityBillMeterEndTextBox.Text,
                out var meterEnd) ||
            !TryParseOptionalNonNegative(
                UtilityBillKwhTextBox.Text,
                out var billedKwh))
        {
            UtilityBillStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidNumber");
            return;
        }

        if (meterStart.HasValue &&
            meterEnd.HasValue)
        {
            var derivedConsumption =
                meterEnd.Value -
                meterStart.Value;

            if (derivedConsumption < -0.0005)
            {
                UtilityBillStatusText.Text =
                    _localization.CurrentLanguage.StartsWith(
                        "es",
                        StringComparison.OrdinalIgnoreCase)
                        ? "La lectura actual no puede ser menor que la lectura anterior."
                        : "The current reading cannot be lower than the previous reading.";
                return;
            }

            if (!billedKwh.HasValue)
            {
                billedKwh =
                    Math.Max(0, derivedConsumption);
                UtilityBillKwhTextBox.Text =
                    billedKwh.Value.ToString(
                        "0.###",
                        CultureInfo.CurrentCulture);
            }
            else if (Math.Abs(
                         billedKwh.Value -
                         derivedConsumption) >
                     0.01)
            {
                UtilityBillStatusText.Text =
                    _localization.CurrentLanguage.StartsWith(
                        "es",
                        StringComparison.OrdinalIgnoreCase)
                        ? $"Las lecturas implican {derivedConsumption:N3} kWh, pero la boleta indica {billedKwh.Value:N3} kWh. Revisa la transcripción antes de guardar."
                        : $"The readings imply {derivedConsumption:N3} kWh, but the bill says {billedKwh.Value:N3} kWh. Review the transcription before saving.";
                return;
            }
        }

        if (!TryParseOptionalNonNegative(
                UtilityBillTaxableTextBox.Text,
                out var taxable) ||
            !TryParseOptionalNonNegative(
                UtilityBillIvaTextBox.Text,
                out var iva) ||
            !TryParseOptionalNonNegative(
                UtilityBillExemptTextBox.Text,
                out var exempt) ||
            !TryParseOptionalNonNegative(
                UtilityBillGrossTextBox.Text,
                out var gross) ||
            !TryParseOptionalFinite(
                UtilityBillOtherChargesTextBox.Text,
                out var otherCharges) ||
            !TryParseOptionalFinite(
                UtilityBillPreviousBalanceTextBox.Text,
                out var previousBalance) ||
            !TryParseOptionalNonNegative(
                UtilityBillTotalDueTextBox.Text,
                out var totalDue))
        {
            UtilityBillStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidNumber");
            return;
        }

        try
        {
            var existingBill = _editingUtilityBillId.HasValue
                ? repository.GetBills().SingleOrDefault(item =>
                    item.BillId == _editingUtilityBillId.Value)
                : null;

            long? sourceDocumentId =
                existingBill?.SourceDocumentId;
            if (_pendingUtilityBillPdfDraft is not null)
            {
                sourceDocumentId = repository.AddBillDocument(
                    "ENEL_DISTRIBUCION_CHILE",
                    _pendingUtilityBillPdfDraft.OriginalFileName,
                    _pendingUtilityBillPdfDraft.StoredPath,
                    _pendingUtilityBillPdfDraft.ContentSha256,
                    _pendingUtilityBillPdfDraft.ContentLength,
                    _pendingUtilityBillPdfDraft.PageCount,
                    _pendingUtilityBillPdfDraft.ParserVersion,
                    _pendingUtilityBillPdfDraft.ExtractedText);
            }

            var sourceKind =
                _pendingUtilityBillPdfDraft is not null ||
                existingBill?.SourceKind ==
                    UtilityBillSourceKind.PdfReviewed
                    ? UtilityBillSourceKind.PdfReviewed
                    : UtilityBillSourceKind.Manual;

            var preservedFromReadingId =
                existingBill?.FromReadingId;
            var preservedToReadingId =
                existingBill?.ToReadingId;

            long billId;
            if (_editingUtilityBillId.HasValue)
            {
                billId = _editingUtilityBillId.Value;
                repository.UpdateBill(
                    billId,
                    startUtc,
                    endUtc,
                    billedKwh,
                    totalDue,
                    UtilityBillReferenceTextBox.Text,
                    UtilityBillNotesTextBox.Text,
                    preservedFromReadingId,
                    preservedToReadingId,
                    meterStart,
                    meterEnd,
                    UtilityBillTariffPlanTextBox.Text,
                    taxable,
                    iva,
                    exempt,
                    gross,
                    otherCharges,
                    totalDue,
                    periodPrecision,
                    sourceKind,
                    sourceDocumentId,
                    UtilityBillReviewState.Reviewed,
                    0.19,
                    previousBalance);
            }
            else
            {
                billId = repository.AddBill(
                    startUtc,
                    endUtc,
                    billedKwh,
                    totalDue,
                    UtilityBillReferenceTextBox.Text,
                    UtilityBillNotesTextBox.Text,
                    meterStartKwh: meterStart,
                    meterEndKwh: meterEnd,
                    tariffPlan: UtilityBillTariffPlanTextBox.Text,
                    taxableAmountClp: taxable,
                    ivaClp: iva,
                    exemptAmountClp: exempt,
                    grossBillAmountClp: gross,
                    otherChargesClp: otherCharges,
                    totalDueClp: totalDue,
                    periodPrecision: periodPrecision,
                    sourceKind: sourceKind,
                    sourceDocumentId: sourceDocumentId,
                    reviewState: UtilityBillReviewState.Reviewed,
                    ivaRate: 0.19,
                    previousBalanceClp: previousBalance);
            }

            SaveUtilityBillFieldEvidence(
                repository,
                billId,
                timeZone,
                startUtc,
                endUtc,
                periodPrecision,
                meterStart,
                meterEnd,
                billedKwh,
                taxable,
                iva,
                exempt,
                gross,
                otherCharges,
                previousBalance,
                totalDue,
                UtilityBillTariffPlanTextBox.Text);

            if (_pendingUtilityBillPdfDraft is not null &&
                UtilityBillPdfApplyLinesCheckBox.IsChecked == true)
            {
                var mergeResult = _services
                    .GetRequiredService<
                        UtilityBillPdfReviewMergeService>()
                    .Merge(
                        billId,
                        _pendingUtilityBillPdfDraft);

                if (mergeResult.RemovedDuplicates > 0)
                {
                    UtilityBillLineStatusText.Text =
                        _localization.CurrentLanguage.StartsWith(
                            "es",
                            StringComparison.OrdinalIgnoreCase)
                            ? $"Revisión PDF: {mergeResult.RemovedDuplicates} línea(s) legacy duplicada(s) fusionada(s)."
                            : $"PDF review: merged {mergeResult.RemovedDuplicates} duplicated legacy line(s).";
                }
            }

            var wasReview =
                _editingUtilityBillId.HasValue;
            ClearUtilityBillEditor();
            UtilityBillStatusText.Text =
                wasReview
                    ? (_localization.CurrentLanguage.StartsWith(
                            "es",
                            StringComparison.OrdinalIgnoreCase)
                        ? "Boleta revisada y actualizada en el mismo registro."
                        : "Bill reviewed and updated in the same record.")
                    : _localization.GetString(
                        "GridUtility.BillSaved");

            RefreshGridUtilityView();

            if (UtilityBillsGrid.ItemsSource is
                    IEnumerable<UtilityBillViewRow> refreshedBills)
            {
                var savedRow =
                    refreshedBills.FirstOrDefault(item =>
                        item.BillId == billId);
                if (savedRow is not null)
                {
                    UtilityBillsGrid.SelectedItem =
                        savedRow;
                    UtilityBillsGrid.ScrollIntoView(
                        savedRow);
                }
            }
        }
        catch (Exception ex)
        {
            UtilityBillStatusText.Text = ex.Message;
        }
    }

    private async void UtilityImportBillPdf_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar boleta Enel PDF",
            Filter = "PDF (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        UtilityImportBillPdfButton.IsEnabled = false;
        UtilityAddBillButton.IsEnabled = false;
        UtilityBillStatusText.Text =
            "Leyendo boleta PDF y preparando borrador revisable…";

        try
        {
            var service = _services
                .GetRequiredService<EnelUtilityBillPdfImportService>();
            var draft = await Task.Run(
                async () => await service.PrepareDraftAsync(
                    dialog.FileName));

            _pendingUtilityBillPdfDraft = draft;
            PopulateUtilityBillFormFromPdfDraft(draft);
        }
        catch (Exception ex)
        {
            ClearPendingUtilityBillPdfDraft();
            UtilityBillStatusText.Text = ex.Message;
        }
        finally
        {
            UtilityImportBillPdfButton.IsEnabled = true;
            UtilityAddBillButton.IsEnabled = true;
        }
    }

    private void PopulateUtilityBillFormFromPdfDraft(
        UtilityBillPdfDraft draft)
    {
        var reviewingExisting =
            _editingUtilityBillId.HasValue;

        var existingBill =
            reviewingExisting
                ? _services
                    .GetRequiredService<UtilityMeterRepository>()
                    .GetBills()
                    .SingleOrDefault(item =>
                        item.BillId ==
                        _editingUtilityBillId!.Value)
                : null;

        var reviewingLegacy =
            existingBill is not null &&
            string.Equals(
                existingBill.ReviewState,
                UtilityBillReviewState.LegacyUnreviewed,
                StringComparison.Ordinal);

        var conflicts =
            new List<string>();

        void ApplyDate(
            QuickDatePicker picker,
            DateOnly? proposed,
            string label)
        {
            if (!proposed.HasValue)
                return;

            var proposedDate =
                proposed.Value.ToDateTime(
                    TimeOnly.MinValue);

            if (!reviewingExisting ||
                !picker.SelectedDate.HasValue)
            {
                picker.SelectedDate =
                    proposedDate;
                return;
            }

            var stored =
                DateOnly.FromDateTime(
                    picker.SelectedDate.Value);
            if (stored == proposed.Value)
                return;

            conflicts.Add(
                reviewingLegacy
                    ? $"{label}: legacy {stored:dd-MM-yyyy} → PDF {proposed.Value:dd-MM-yyyy} (borrador actualizado)"
                    : $"{label}: guardado {stored:dd-MM-yyyy} → PDF {proposed.Value:dd-MM-yyyy} (borrador actualizado; base sin cambios hasta guardar)");

            picker.SelectedDate = proposedDate;
        }

        void ApplyNumber(
            TextBox box,
            double? proposed,
            string format,
            double tolerance,
            string label)
        {
            if (!proposed.HasValue)
                return;

            var formatted =
                proposed.Value.ToString(
                    format,
                    CultureInfo.CurrentCulture);

            if (!reviewingExisting ||
                string.IsNullOrWhiteSpace(
                    box.Text))
            {
                box.Text = formatted;
                return;
            }

            if (TryParseNumber(
                    box.Text,
                    out var current) &&
                Math.Abs(
                    current -
                    proposed.Value) <=
                tolerance)
            {
                return;
            }

            conflicts.Add(
                reviewingLegacy
                    ? $"{label}: legacy {box.Text} → PDF {formatted} (borrador actualizado)"
                    : $"{label}: guardado {box.Text} → PDF {formatted} (borrador actualizado; base sin cambios hasta guardar)");

            box.Text = formatted;
        }

        if (!reviewingExisting)
        {
            UtilityBillPeriodPrecisionSelector.SelectedValue =
                UtilityTimePrecision.DateOnly;
        }
        else if (!string.Equals(
                     UtilityBillPeriodPrecisionSelector
                         .SelectedValue?
                         .ToString(),
                     UtilityTimePrecision.DateOnly,
                     StringComparison.Ordinal))
        {
            conflicts.Add(
                "precisión del período: el PDF respalda fechas impresas; el borrador se cambió a fecha inclusiva y la base no cambia hasta guardar");

            UtilityBillPeriodPrecisionSelector.SelectedValue =
                UtilityTimePrecision.DateOnly;
        }

        ApplyDate(
            UtilityBillStartDatePicker,
            draft.PeriodStart,
            "inicio");
        ApplyDate(
            UtilityBillEndDatePicker,
            draft.PeriodEndInclusive,
            "fin");

        if (!reviewingExisting ||
            reviewingLegacy)
        {
            UtilityBillStartTimeTextBox.Text = "00:00";
            UtilityBillEndTimeTextBox.Text = "00:00";
        }

        ApplyNumber(
            UtilityBillMeterStartTextBox,
            draft.MeterStartKwh,
            "0.###",
            0.0005,
            "lectura anterior");
        ApplyNumber(
            UtilityBillMeterEndTextBox,
            draft.MeterEndKwh,
            "0.###",
            0.0005,
            "lectura actual");
        ApplyNumber(
            UtilityBillKwhTextBox,
            draft.BilledConsumptionKwh,
            "0.###",
            0.0005,
            "consumo kWh");
        ApplyNumber(
            UtilityBillTaxableTextBox,
            draft.TaxableAmountClp,
            "0",
            0.5,
            "monto afecto");
        ApplyNumber(
            UtilityBillIvaTextBox,
            draft.IvaClp,
            "0",
            0.5,
            "IVA");
        ApplyNumber(
            UtilityBillExemptTextBox,
            draft.ExemptAmountClp,
            "0",
            0.5,
            "monto exento");
        ApplyNumber(
            UtilityBillGrossTextBox,
            draft.GrossBillAmountClp,
            "0",
            0.5,
            "total boleta");
        ApplyNumber(
            UtilityBillOtherChargesTextBox,
            draft.OtherChargesClp,
            "0",
            0.5,
            "otros cargos/abonos");
        ApplyNumber(
            UtilityBillPreviousBalanceTextBox,
            draft.PreviousBalanceClp,
            "0",
            0.5,
            "saldo anterior");
        ApplyNumber(
            UtilityBillTotalDueTextBox,
            draft.TotalDueClp,
            "0",
            0.5,
            "total a pagar");

        if (!string.IsNullOrWhiteSpace(
                draft.TariffPlan))
        {
            if (!reviewingExisting ||
                string.IsNullOrWhiteSpace(
                    UtilityBillTariffPlanTextBox.Text))
            {
                UtilityBillTariffPlanTextBox.Text =
                    draft.TariffPlan;
            }
            else if (!string.Equals(
                         UtilityBillTariffPlanTextBox.Text.Trim(),
                         draft.TariffPlan,
                         StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(
                    reviewingLegacy
                        ? $"tarifa: legacy {UtilityBillTariffPlanTextBox.Text.Trim()} → PDF {draft.TariffPlan} (borrador actualizado)"
                        : $"tarifa: guardado {UtilityBillTariffPlanTextBox.Text.Trim()} → PDF {draft.TariffPlan} (borrador actualizado; base sin cambios hasta guardar)");

                UtilityBillTariffPlanTextBox.Text =
                    draft.TariffPlan;
            }
        }

        UtilityBillPdfPreviewGrid.ItemsSource =
            draft.Lines
                .Select(line =>
                    new UtilityBillPdfDraftLineViewRow(
                        line.Description,
                        $"$ {line.AmountClp:+#,##0;-#,##0;0}",
                        $"PDF p.{line.SourcePage} · revisar"))
                .ToArray();
        UtilityBillPdfPreviewGrid.Visibility =
            draft.Lines.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        UtilityBillPdfApplyLinesCheckBox.Visibility =
            draft.Lines.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        UtilityBillPdfApplyLinesCheckBox.IsChecked = true;

        RefreshUtilityBillPrintedFactsChecks();

        var detected =
            new List<string>();
        if (draft.PeriodStart.HasValue &&
            draft.PeriodEndInclusive.HasValue)
            detected.Add("período");
        if (draft.MeterStartKwh.HasValue &&
            draft.MeterEndKwh.HasValue)
            detected.Add("lecturas");
        if (draft.BilledConsumptionKwh.HasValue)
            detected.Add("kWh");
        if (draft.TaxableAmountClp.HasValue)
            detected.Add("monto afecto");
        if (draft.IvaClp.HasValue)
            detected.Add("IVA");
        if (draft.ExemptAmountClp.HasValue)
            detected.Add("monto exento");
        if (draft.GrossBillAmountClp.HasValue)
            detected.Add("total boleta");
        if (draft.OtherChargesClp.HasValue)
            detected.Add("otros cargos/abonos");
        if (draft.PreviousBalanceClp.HasValue)
            detected.Add("saldo anterior");
        if (draft.TotalDueClp.HasValue)
            detected.Add("total a pagar");
        if (!string.IsNullOrWhiteSpace(
                draft.TariffPlan))
            detected.Add("tarifa");
        if (draft.Lines.Count > 0)
            detected.Add(
                $"{draft.Lines.Count} línea(s)");

        var warning =
            draft.Warnings.Count == 0
                ? string.Empty
                : " " +
                  string.Join(
                      " ",
                      draft.Warnings);
        var conflictText =
            conflicts.Count == 0
                ? string.Empty
                : " DIFERENCIAS PARA REVISAR: " +
                  string.Join(
                      " | ",
                      conflicts);

        UtilityBillStatusText.Text =
            $"PDF preparado ({string.Join(", ", detected)}). " +
            (reviewingExisting
                ? "Las diferencias detectadas quedaron aplicadas sólo al borrador de revisión; la base no cambia hasta que pulses Guardar revisión."
                : "Revisa/corrige los campos antes de guardar.") +
            conflictText +
            warning;
    }

    private void ClearPendingUtilityBillPdfDraft()
    {
        _pendingUtilityBillPdfDraft = null;
        if (UtilityBillPdfPreviewGrid is not null)
        {
            UtilityBillPdfPreviewGrid.ItemsSource = null;
            UtilityBillPdfPreviewGrid.Visibility =
                Visibility.Collapsed;
        }

        if (UtilityBillPdfApplyLinesCheckBox is not null)
        {
            UtilityBillPdfApplyLinesCheckBox.Visibility =
                Visibility.Collapsed;
            UtilityBillPdfApplyLinesCheckBox.IsChecked = true;
        }
    }

    private void SaveUtilityBillFieldEvidence(
        UtilityMeterRepository repository,
        long billId,
        string timeZoneId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string periodPrecision,
        double? meterStart,
        double? meterEnd,
        double? billedKwh,
        double? taxable,
        double? iva,
        double? exempt,
        double? gross,
        double? otherCharges,
        double? previousBalance,
        double? totalDue,
        string? tariffPlan)
    {
        var draft = _pendingUtilityBillPdfDraft;

        (string Source, string State) Evidence(
            bool pdfMatch) =>
            pdfMatch
                ? (
                    UtilityBillSourceKind.PdfReviewed,
                    UtilityBillEvidenceState.PdfExtractedConfirmed)
                : (
                    UtilityBillSourceKind.Manual,
                    UtilityBillEvidenceState.UserEntered);

        var startDate =
            SolarApiTime.GetLocalDate(
                startUtc,
                timeZoneId);
        var endDate =
            SolarApiTime.GetLocalDate(
                endUtc,
                timeZoneId);

        var periodMatched =
            draft?.PeriodStart == startDate &&
            draft?.PeriodEndInclusive == endDate &&
            string.Equals(
                periodPrecision,
                UtilityTimePrecision.DateOnly,
                StringComparison.Ordinal);
        var periodEvidence = Evidence(periodMatched);

        repository.UpsertBillFieldEvidence(
            billId,
            "period_start",
            periodEvidence.Source,
            periodEvidence.State,
            startDate.ToString("dd-MM-yyyy"),
            startUtc.ToUniversalTime().ToString("O"));
        repository.UpsertBillFieldEvidence(
            billId,
            "period_end",
            periodEvidence.Source,
            periodEvidence.State,
            endDate.ToString("dd-MM-yyyy"),
            endUtc.ToUniversalTime().ToString("O"));
        repository.UpsertBillFieldEvidence(
            billId,
            "period_precision",
            periodEvidence.Source,
            periodEvidence.State,
            periodPrecision,
            periodPrecision);

        void Numeric(
            string key,
            double? value,
            double? pdfValue = null,
            double tolerance = 0.0005)
        {
            if (!value.HasValue)
            {
                repository.UpsertBillFieldEvidence(
                    billId,
                    key,
                    UtilityBillSourceKind.Manual,
                    UtilityBillEvidenceState.NotProvided,
                    null,
                    null);
                return;
            }

            var matched =
                pdfValue.HasValue &&
                Math.Abs(
                    pdfValue.Value -
                    value.Value) <= tolerance;
            var evidenceState = Evidence(matched);
            repository.UpsertBillFieldEvidence(
                billId,
                key,
                evidenceState.Source,
                evidenceState.State,
                value.Value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture),
                value.Value.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }

        Numeric(
            "meter_start_kwh",
            meterStart,
            draft?.MeterStartKwh);
        Numeric(
            "meter_end_kwh",
            meterEnd,
            draft?.MeterEndKwh);
        Numeric(
            "billed_consumption_kwh",
            billedKwh,
            draft?.BilledConsumptionKwh);
        Numeric(
            "taxable_amount_clp",
            taxable,
            draft?.TaxableAmountClp,
            0.5);
        Numeric(
            "iva_clp",
            iva,
            draft?.IvaClp,
            0.5);
        Numeric(
            "exempt_amount_clp",
            exempt,
            draft?.ExemptAmountClp,
            0.5);
        Numeric(
            "gross_bill_amount_clp",
            gross,
            draft?.GrossBillAmountClp,
            0.5);
        Numeric(
            "other_charges_clp",
            otherCharges,
            draft?.OtherChargesClp,
            0.5);
        Numeric(
            "previous_balance_clp",
            previousBalance,
            draft?.PreviousBalanceClp,
            0.5);
        Numeric(
            "total_due_clp",
            totalDue,
            draft?.TotalDueClp,
            0.5);

        if (!string.IsNullOrWhiteSpace(tariffPlan))
        {
            var matched =
                !string.IsNullOrWhiteSpace(draft?.TariffPlan) &&
                string.Equals(
                    draft!.TariffPlan,
                    tariffPlan.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            var evidenceState = Evidence(matched);
            repository.UpsertBillFieldEvidence(
                billId,
                "tariff_plan",
                evidenceState.Source,
                evidenceState.State,
                tariffPlan.Trim(),
                tariffPlan.Trim().ToUpperInvariant());
        }
        else
        {
            repository.UpsertBillFieldEvidence(
                billId,
                "tariff_plan",
                UtilityBillSourceKind.Manual,
                UtilityBillEvidenceState.NotProvided,
                null,
                null);
        }

        repository.UpsertBillFieldEvidence(
            billId,
            "iva_rate",
            UtilityBillSourceKind.Manual,
            UtilityBillEvidenceState.Derived,
            "19%",
            "0.19");
    }

    private void UtilityBillPrintedFacts_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!IsInitialized ||
            UtilityBillConsumptionCheckText is null ||
            UtilityBillFinancialCheckText is null)
        {
            return;
        }

        RefreshUtilityBillPrintedFactsChecks();
    }

    private void RefreshUtilityBillPrintedFactsChecks()
    {
        if (UtilityBillConsumptionCheckText is null ||
            UtilityBillFinancialCheckText is null)
        {
            return;
        }

        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

        static double? ReadValue(TextBox box)
        {
            if (string.IsNullOrWhiteSpace(box.Text))
                return null;

            return TryParseNumber(
                    box.Text,
                    out var value) &&
                   double.IsFinite(value)
                ? value
                : double.NaN;
        }

        var meterStart =
            ReadValue(UtilityBillMeterStartTextBox);
        var meterEnd =
            ReadValue(UtilityBillMeterEndTextBox);
        var billedKwh =
            ReadValue(UtilityBillKwhTextBox);

        if ((meterStart.HasValue && double.IsNaN(meterStart.Value)) ||
            (meterEnd.HasValue && double.IsNaN(meterEnd.Value)) ||
            (billedKwh.HasValue && double.IsNaN(billedKwh.Value)))
        {
            UtilityBillConsumptionCheckText.Text =
                spanish
                    ? "Revisa las lecturas o el consumo: hay un valor numérico no válido."
                    : "Review readings or consumption: a numeric value is invalid.";
        }
        else if (meterStart.HasValue &&
                 meterEnd.HasValue)
        {
            var derived =
                meterEnd.Value -
                meterStart.Value;

            if (derived < -0.0005)
            {
                UtilityBillConsumptionCheckText.Text =
                    spanish
                        ? "Revisar: la lectura actual es menor que la lectura anterior."
                        : "Review: current reading is lower than previous reading.";
            }
            else if (billedKwh.HasValue)
            {
                var difference =
                    billedKwh.Value -
                    derived;
                UtilityBillConsumptionCheckText.Text =
                    Math.Abs(difference) <= 0.01
                        ? (spanish
                            ? $"OK · {meterStart.Value:N3} → {meterEnd.Value:N3} = {derived:N3} kWh; coincide con el consumo impreso."
                            : $"OK · {meterStart.Value:N3} → {meterEnd.Value:N3} = {derived:N3} kWh; matches printed consumption.")
                        : (spanish
                            ? $"Revisar · lecturas = {derived:N3} kWh; consumo impreso = {billedKwh.Value:N3} kWh; diferencia {difference:+0.###;-0.###;0} kWh."
                            : $"Review · readings = {derived:N3} kWh; printed consumption = {billedKwh.Value:N3} kWh; difference {difference:+0.###;-0.###;0} kWh.");
            }
            else
            {
                UtilityBillConsumptionCheckText.Text =
                    spanish
                        ? $"Las lecturas implican {derived:N3} kWh. Si la boleta no imprime otro consumo, éste se usará al guardar."
                        : $"The readings imply {derived:N3} kWh. If the bill prints no separate consumption, this value will be used when saving.";
            }
        }
        else if (billedKwh.HasValue)
        {
            UtilityBillConsumptionCheckText.Text =
                spanish
                    ? $"Consumo impreso: {billedKwh.Value:N3} kWh. Sin ambas lecturas no hay control cruzado."
                    : $"Printed consumption: {billedKwh.Value:N3} kWh. Both readings are needed for a cross-check.";
        }
        else
        {
            UtilityBillConsumptionCheckText.Text =
                _localization.GetString(
                    "GridUtility.BillConsumptionCheckIdle");
        }

        var taxable =
            ReadValue(UtilityBillTaxableTextBox);
        var iva =
            ReadValue(UtilityBillIvaTextBox);
        var exempt =
            ReadValue(UtilityBillExemptTextBox);
        var gross =
            ReadValue(UtilityBillGrossTextBox);
        var other =
            ReadValue(UtilityBillOtherChargesTextBox);
        var previous =
            ReadValue(UtilityBillPreviousBalanceTextBox);
        var total =
            ReadValue(UtilityBillTotalDueTextBox);

        var financialValues =
            new[]
            {
                taxable,
                iva,
                exempt,
                gross,
                other,
                previous,
                total
            };

        if (financialValues.Any(value =>
                value.HasValue &&
                double.IsNaN(value.Value)))
        {
            UtilityBillFinancialCheckText.Text =
                spanish
                    ? "Revisa el resumen: hay un monto no válido."
                    : "Review the summary: an amount is invalid.";
            return;
        }

        var parts =
            new List<string>();

        if (taxable.HasValue &&
            iva.HasValue)
        {
            var expectedIva =
                Math.Round(
                    taxable.Value * 0.19,
                    0,
                    MidpointRounding.AwayFromZero);
            var ivaDifference =
                iva.Value -
                expectedIva;

            parts.Add(
                Math.Abs(ivaDifference) <= 1
                    ? (spanish
                        ? $"IVA OK: 19% de $ {taxable.Value:N0} ≈ $ {expectedIva:N0}"
                        : $"VAT OK: 19% of $ {taxable.Value:N0} ≈ $ {expectedIva:N0}")
                    : (spanish
                        ? $"IVA a revisar: impreso $ {iva.Value:N0}, 19% calculado $ {expectedIva:N0}"
                        : $"VAT review: printed $ {iva.Value:N0}, calculated 19% $ {expectedIva:N0}"));
        }

        if (taxable.HasValue &&
            iva.HasValue &&
            exempt.HasValue)
        {
            var expectedGross =
                taxable.Value +
                iva.Value +
                exempt.Value;

            if (gross.HasValue)
            {
                var grossDifference =
                    gross.Value -
                    expectedGross;
                parts.Add(
                    Math.Abs(grossDifference) <= 0.5
                        ? (spanish
                            ? $"Total boleta OK: $ {expectedGross:N0}"
                            : $"Gross bill OK: $ {expectedGross:N0}")
                        : (spanish
                            ? $"Total boleta difiere {grossDifference:+0;-0;0}: esperado $ {expectedGross:N0}, impreso $ {gross.Value:N0}"
                            : $"Gross bill differs {grossDifference:+0;-0;0}: expected $ {expectedGross:N0}, printed $ {gross.Value:N0}"));
            }
            else
            {
                parts.Add(
                    spanish
                        ? $"Afecto + IVA + exento = $ {expectedGross:N0}"
                        : $"Taxable + VAT + exempt = $ {expectedGross:N0}");
            }
        }

        if (gross.HasValue &&
            other.HasValue &&
            previous.HasValue)
        {
            var expectedTotal =
                gross.Value +
                other.Value +
                previous.Value;

            if (total.HasValue)
            {
                var totalDifference =
                    total.Value -
                    expectedTotal;
                parts.Add(
                    Math.Abs(totalDifference) <= 0.5
                        ? (spanish
                            ? $"Total a pagar OK: $ {expectedTotal:N0}"
                            : $"Total due OK: $ {expectedTotal:N0}")
                        : (spanish
                            ? $"Total a pagar difiere {totalDifference:+0;-0;0}: suma $ {expectedTotal:N0}, impreso $ {total.Value:N0}"
                            : $"Total due differs {totalDifference:+0;-0;0}: sum $ {expectedTotal:N0}, printed $ {total.Value:N0}"));
            }
            else
            {
                parts.Add(
                    spanish
                        ? $"Total boleta + otros cargos/abonos + saldo anterior = $ {expectedTotal:N0}"
                        : $"Gross bill + other charges/credits + previous balance = $ {expectedTotal:N0}");
            }
        }

        UtilityBillFinancialCheckText.Text =
            parts.Count > 0
                ? string.Join(" · ", parts)
                : _localization.GetString(
                    "GridUtility.BillFinancialCheckIdle");
    }

    private void UtilityNewManualBill_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearUtilityBillEditor();

        UtilityBillStatusText.Text =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase)
                ? "Nueva boleta manual. Completa sólo los datos que puedas respaldar con la cuenta; los campos vacíos quedarán registrados como sin dato ingresado."
                : "New manual bill. Enter only facts supported by the bill; blank optional fields will be recorded as no value entered.";
    }

    private void UtilityReviewBill_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityBillsGrid.SelectedItem
            is not UtilityBillViewRow selected)
        {
            UtilityBillStatusText.Text =
                "Selecciona una boleta para revisarla.";
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
            return;

        var repository =
            _services.GetRequiredService<UtilityMeterRepository>();
        var bill = repository.GetBills()
            .Single(item =>
                item.BillId == selected.BillId);
        var timeZone =
            string.IsNullOrWhiteSpace(
                profile.StationTimeZone)
                ? "America/Santiago"
                : profile.StationTimeZone;

        _editingUtilityBillId = bill.BillId;
        ClearPendingUtilityBillPdfDraft();

        UtilityBillPeriodPrecisionSelector.SelectedValue =
            bill.PeriodPrecision;
        var startLocal =
            SolarApiTime.ConvertToLocalTime(
                bill.PeriodStartUtc,
                timeZone);
        var endLocal =
            SolarApiTime.ConvertToLocalTime(
                bill.PeriodEndUtc,
                timeZone);
        UtilityBillStartDatePicker.SelectedDate =
            startLocal.Date;
        UtilityBillEndDatePicker.SelectedDate =
            endLocal.Date;
        UtilityBillStartTimeTextBox.Text =
            startLocal.ToString("HH:mm");
        UtilityBillEndTimeTextBox.Text =
            endLocal.ToString("HH:mm");

        UtilityBillFromReadingSelector.SelectedValue =
            bill.FromReadingId;
        UtilityBillToReadingSelector.SelectedValue =
            bill.ToReadingId;
        UtilityBillMeterStartTextBox.Text =
            FormatEditableNumber(
                bill.MeterStartKwh);
        UtilityBillMeterEndTextBox.Text =
            FormatEditableNumber(
                bill.MeterEndKwh);
        UtilityBillKwhTextBox.Text =
            FormatEditableNumber(
                bill.BilledConsumptionKwh);
        UtilityBillTariffPlanTextBox.Text =
            bill.TariffPlan ?? string.Empty;
        UtilityBillTaxableTextBox.Text =
            FormatEditableNumber(
                bill.TaxableAmountClp);
        UtilityBillIvaTextBox.Text =
            FormatEditableNumber(
                bill.IvaClp);
        UtilityBillExemptTextBox.Text =
            FormatEditableNumber(
                bill.ExemptAmountClp);
        UtilityBillGrossTextBox.Text =
            FormatEditableNumber(
                bill.GrossBillAmountClp);
        UtilityBillOtherChargesTextBox.Text =
            FormatEditableNumber(
                bill.OtherChargesClp);
        UtilityBillPreviousBalanceTextBox.Text =
            FormatEditableNumber(
                bill.PreviousBalanceClp);
        UtilityBillTotalDueTextBox.Text =
            FormatEditableNumber(
                bill.TotalDueClp ??
                bill.AmountClp);
        UtilityBillReferenceTextBox.Text =
            bill.InvoiceReference ?? string.Empty;
        UtilityBillNotesTextBox.Text =
            bill.Notes ?? string.Empty;

        UtilityAddBillButton.Content =
            "Guardar revisión";
        UtilityCancelBillReviewButton.Visibility =
            Visibility.Visible;
        RefreshUtilityBillPrintedFactsChecks();

        UtilityBillStatusText.Text =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase)
                ? $"Revisando boleta #{bill.BillId}. Puedes corregir lo transcrito o importar su PDF; ambos trabajan sobre este mismo registro."
                : $"Reviewing bill #{bill.BillId}. You can correct the transcription or import its PDF; both work on this same record.";
    }

    private void UtilityCancelBillReview_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearUtilityBillEditor();
        UtilityBillStatusText.Text =
            "Revisión cancelada; la boleta guardada no fue modificada.";
    }

    private void ClearUtilityBillEditor()
    {
        _editingUtilityBillId = null;
        _editingUtilityBillLineId = null;
        ClearPendingUtilityBillPdfDraft();

        UtilityBillMeterStartTextBox.Clear();
        UtilityBillMeterEndTextBox.Clear();
        UtilityBillKwhTextBox.Clear();
        UtilityBillTaxableTextBox.Clear();
        UtilityBillIvaTextBox.Clear();
        UtilityBillExemptTextBox.Clear();
        UtilityBillGrossTextBox.Clear();
        UtilityBillOtherChargesTextBox.Clear();
        UtilityBillPreviousBalanceTextBox.Clear();
        UtilityBillTotalDueTextBox.Clear();
        UtilityBillTariffPlanTextBox.Clear();
        UtilityBillReferenceTextBox.Clear();
        UtilityBillNotesTextBox.Clear();
        UtilityBillFromReadingSelector.SelectedIndex = -1;
        UtilityBillToReadingSelector.SelectedIndex = -1;
        UtilityBillStartDatePicker.SelectedDate = null;
        UtilityBillEndDatePicker.SelectedDate = null;
        UtilityBillStartTimeTextBox.Text = "00:00";
        UtilityBillEndTimeTextBox.Text = "00:00";
        UtilityBillPeriodPrecisionSelector.SelectedValue =
            UtilityTimePrecision.DateOnly;
        UtilityAddBillButton.Content =
            _localization.GetString(
                "GridUtility.AddBill");
        UtilityCancelBillReviewButton.Visibility =
            Visibility.Collapsed;

        if (UtilityBillConsumptionCheckText is not null)
        {
            UtilityBillConsumptionCheckText.Text =
                _localization.GetString(
                    "GridUtility.BillConsumptionCheckIdle");
        }
        if (UtilityBillFinancialCheckText is not null)
        {
            UtilityBillFinancialCheckText.Text =
                _localization.GetString(
                    "GridUtility.BillFinancialCheckIdle");
        }

        ClearUtilityBillLineEditor();
    }

    private static string FormatEditableNumber(
        double? value) =>
        value.HasValue
            ? value.Value.ToString(
                "0.###",
                CultureInfo.CurrentCulture)
            : string.Empty;

    private void UtilityOpenOriginalBillPdf_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityBillsGrid.SelectedItem
            is not UtilityBillViewRow selected)
        {
            UtilityBillStatusText.Text =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Selecciona una boleta guardada."
                    : "Select a saved bill.";
            return;
        }

        var repository =
            _services.GetRequiredService<UtilityMeterRepository>();
        var bill = repository.GetBills()
            .Single(item =>
                item.BillId == selected.BillId);

        if (!bill.SourceDocumentId.HasValue)
        {
            UtilityBillStatusText.Text =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Esta boleta no tiene un PDF original vinculado. Puedes revisarla y usar “Importar PDF de boleta…” para adjuntarlo sin crear un registro nuevo."
                    : "This bill has no linked original PDF. Review it and use “Import bill PDF…” to attach one without creating a new record.";
            return;
        }

        var document =
            repository.GetBillDocument(
                bill.SourceDocumentId.Value);

        if (document is null ||
            string.IsNullOrWhiteSpace(
                document.LocalPdfPath) ||
            !File.Exists(
                document.LocalPdfPath))
        {
            UtilityBillStatusText.Text =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase)
                    ? "El registro conserva la procedencia del PDF, pero el archivo local ya no está disponible."
                    : "The record retains PDF provenance, but the local file is no longer available.";
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo(
                    document.LocalPdfPath)
                {
                    UseShellExecute = true
                });

            UtilityBillStatusText.Text =
                _localization.CurrentLanguage.StartsWith(
                    "es",
                    StringComparison.OrdinalIgnoreCase)
                    ? $"Abriendo PDF original: {document.OriginalFileName}"
                    : $"Opening original PDF: {document.OriginalFileName}";
        }
        catch (Exception ex)
        {
            UtilityBillStatusText.Text =
                ex.Message;
        }
    }

    private void UtilityDeleteBill_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityBillsGrid.SelectedItem
            is not UtilityBillViewRow selected)
        {
            return;
        }

        if (MessageBox.Show(
                _localization.GetString(
                    "GridUtility.ConfirmDeleteBill"),
                _localization.GetString(
                    "Page.GridUtility.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            return;
        }

        _services
            .GetRequiredService<UtilityMeterRepository>()
            .DeleteBill(selected.BillId);

        if (_editingUtilityBillId == selected.BillId)
        {
            ClearUtilityBillEditor();
        }

        UtilityBillStatusText.Text =
            _localization.GetString(
                "GridUtility.BillDeleted");
        RefreshGridUtilityView();
    }

    private void UtilityBillsGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            RefreshSelectedBillLines();
        }
    }

    private void RefreshSelectedBillLines()
    {
        if (UtilityBillLinesGrid is null)
        {
            return;
        }

        if (UtilityBillsGrid.SelectedItem is not UtilityBillViewRow bill)
        {
            UtilityBillLinesGrid.ItemsSource = null;
            return;
        }

        var lines = _services
            .GetRequiredService<UtilityMeterRepository>()
            .GetBillLines(bill.BillId);

        UtilityBillLinesGrid.ItemsSource = lines
            .Select(item => new UtilityBillLineViewRow(
                item.BillLineId,
                BillSectionLabel(item.SectionKey),
                item.Description,
                item.Quantity.HasValue ? $"{item.Quantity.Value:N3}" : "—",
                item.Unit ?? string.Empty,
                item.UnitRateClp.HasValue ? $"$ {item.UnitRateClp.Value:N3}" : "—",
                $"$ {item.AmountClp:+0;-0;0}",
                item.TaxTreatment ?? string.Empty,
                UtilityBillSourceLabel(item.SourceKind),
                UtilityBillEvidenceLabel(
                    item.EvidenceState,
                    item.SourcePage)))
            .ToArray();
    }

    private void UtilityBillLineTypeSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsInitialized ||
            UtilityBillLineDescriptionTextBox is null ||
            UtilityBillLineSectionSelector is null)
        {
            return;
        }

        var categoryKey =
            UtilityBillLineTypeSelector.SelectedValue?.ToString();

        switch (categoryKey)
        {
            case "ELECTRICITY_CONSUMED":
                UtilityBillLineSectionSelector.SelectedValue =
                    "SERVICIO_ELECTRICO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.ElectricityConsumed");
                break;

            case "ELECTRICITY_TRANSPORT":
                UtilityBillLineSectionSelector.SelectedValue =
                    "SERVICIO_ELECTRICO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.ElectricityTransport");
                break;

            case "FIXED_MONTHLY":
                UtilityBillLineSectionSelector.SelectedValue =
                    "SERVICIO_ELECTRICO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.FixedMonthly");
                break;

            case "SUBSIDY":
                UtilityBillLineSectionSelector.SelectedValue =
                    "OTROS_CARGOS";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.Subsidy");
                break;

            case "SERVICE_ADMINISTRATION":
                UtilityBillLineSectionSelector.SelectedValue =
                    "SERVICIO_ELECTRICO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.ServiceAdministration");
                break;

            case "METER_RENTAL":
                UtilityBillLineSectionSelector.SelectedValue =
                    "SERVICIO_ELECTRICO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.MeterRental");
                break;

            case "COMMON_SERVICE":
                UtilityBillLineSectionSelector.SelectedValue =
                    "SERVICIO_ELECTRICO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.CommonService");
                break;

            case "VAT_19":
                UtilityBillLineSectionSelector.SelectedValue =
                    "ACUMULADO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.Vat19");
                UtilityBillLineTaxTextBox.Text = "IVA 19%";
                break;

            case "SIMPLE_ADJUSTMENT":
                UtilityBillLineSectionSelector.SelectedValue =
                    "ACUMULADO";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.SimpleAdjustment");
                break;
        }
    }

    private void UtilityAddBillLine_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityBillsGrid.SelectedItem is not UtilityBillViewRow bill)
        {
            UtilityBillLineStatusText.Text =
                _localization.GetString(
                    "GridUtility.SelectBillFirst");
            return;
        }

        var section =
            UtilityBillLineSectionSelector.SelectedValue?.ToString()
            ?? "OTRO";
        var description =
            UtilityBillLineDescriptionTextBox.Text?.Trim()
            ?? string.Empty;
        var selectedCategory =
            UtilityBillLineTypeSelector.SelectedValue?.ToString();
        var categoryKey =
            string.Equals(
                selectedCategory,
                "CUSTOM",
                StringComparison.Ordinal)
                ? null
                : selectedCategory;

        if (string.IsNullOrWhiteSpace(description) ||
            !TryParseRequiredFinite(
                UtilityBillLineAmountTextBox.Text,
                out var amount) ||
            !TryParseOptionalFinite(
                UtilityBillLineQuantityTextBox.Text,
                out var quantity) ||
            !TryParseOptionalFinite(
                UtilityBillLineRateTextBox.Text,
                out var unitRate))
        {
            UtilityBillLineStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidNumber");
            return;
        }

        try
        {
            var repository = _services
                .GetRequiredService<UtilityMeterRepository>();

            if (_editingUtilityBillLineId.HasValue)
            {
                repository.UpdateBillLine(
                    _editingUtilityBillLineId.Value,
                    section,
                    description,
                    amount,
                    categoryKey,
                    quantity,
                    UtilityBillLineUnitTextBox.Text,
                    unitRate,
                    UtilityBillLineTaxTextBox.Text,
                    UtilityBillSourceKind.Manual,
                    UtilityBillEvidenceState.UserEntered);
                UtilityBillLineStatusText.Text =
                    "Línea actualizada manualmente.";
            }
            else
            {
                repository.AddBillLine(
                    bill.BillId,
                    section,
                    description,
                    amount,
                    categoryKey: categoryKey,
                    quantity: quantity,
                    unit: UtilityBillLineUnitTextBox.Text,
                    unitRateClp: unitRate,
                    taxTreatment: UtilityBillLineTaxTextBox.Text);
                UtilityBillLineStatusText.Text =
                    _localization.GetString(
                        "GridUtility.BillLineSaved");
            }

            ClearUtilityBillLineEditor();
            RefreshSelectedBillLines();
            if (UtilityAuditVerificationGrid?.IsVisible == true)
            {
                RefreshUtilityAuditPreview();
            }
        }
        catch (Exception ex)
        {
            UtilityBillLineStatusText.Text = ex.Message;
        }
    }

    private void UtilityEditBillLine_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityBillsGrid.SelectedItem
                is not UtilityBillViewRow bill ||
            UtilityBillLinesGrid.SelectedItem
                is not UtilityBillLineViewRow selected)
        {
            UtilityBillLineStatusText.Text =
                "Selecciona una línea guardada para editarla.";
            return;
        }

        var line = _services
            .GetRequiredService<UtilityMeterRepository>()
            .GetBillLines(bill.BillId)
            .Single(item =>
                item.BillLineId ==
                selected.BillLineId);

        _editingUtilityBillLineId =
            line.BillLineId;
        UtilityBillLineTypeSelector.SelectedValue =
            string.IsNullOrWhiteSpace(
                line.CategoryKey)
                ? "CUSTOM"
                : line.CategoryKey;
        UtilityBillLineSectionSelector.SelectedValue =
            line.SectionKey;
        UtilityBillLineDescriptionTextBox.Text =
            line.Description;
        UtilityBillLineAmountTextBox.Text =
            line.AmountClp.ToString(
                "0.###",
                CultureInfo.CurrentCulture);
        UtilityBillLineQuantityTextBox.Text =
            FormatEditableNumber(
                line.Quantity);
        UtilityBillLineUnitTextBox.Text =
            line.Unit ?? string.Empty;
        UtilityBillLineRateTextBox.Text =
            FormatEditableNumber(
                line.UnitRateClp);
        UtilityBillLineTaxTextBox.Text =
            line.TaxTreatment ?? string.Empty;

        UtilitySaveBillLineButton.Content =
            "Guardar línea";
        UtilityCancelBillLineEditButton.Visibility =
            Visibility.Visible;
        UtilityBillLineStatusText.Text =
            $"Editando línea #{line.BillLineId}; cualquier cambio quedará como revisión manual.";
    }

    private void UtilityCancelBillLineEdit_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearUtilityBillLineEditor();
        UtilityBillLineStatusText.Text =
            "Edición de línea cancelada.";
    }

    private void ClearUtilityBillLineEditor()
    {
        _editingUtilityBillLineId = null;
        UtilityBillLineTypeSelector.SelectedValue =
            "CUSTOM";
        UtilityBillLineSectionSelector.SelectedValue =
            "OTRO";
        UtilityBillLineDescriptionTextBox.Clear();
        UtilityBillLineAmountTextBox.Clear();
        UtilityBillLineQuantityTextBox.Clear();
        UtilityBillLineUnitTextBox.Clear();
        UtilityBillLineRateTextBox.Clear();
        UtilityBillLineTaxTextBox.Clear();
        UtilitySaveBillLineButton.Content =
            _localization.GetString(
                "GridUtility.AddBillLine");
        UtilityCancelBillLineEditButton.Visibility =
            Visibility.Collapsed;
    }

    private void UtilityDeleteBillLine_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (UtilityBillLinesGrid.SelectedItem
            is not UtilityBillLineViewRow line)
        {
            return;
        }

        _services
            .GetRequiredService<UtilityMeterRepository>()
            .DeleteBillLine(line.BillLineId);

        if (_editingUtilityBillLineId == line.BillLineId)
        {
            ClearUtilityBillLineEditor();
        }

        UtilityBillLineStatusText.Text =
            _localization.GetString(
                "GridUtility.BillLineDeleted");
        RefreshSelectedBillLines();
        RefreshUtilityAuditPreview();
    }

    private string UtilityBillSourceLabel(
        string sourceKind)
    {
        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

        return sourceKind switch
        {
            UtilityBillSourceKind.Manual =>
                spanish ? "Manual" : "Manual",
            UtilityBillSourceKind.PdfReviewed =>
                spanish ? "PDF + revisión" : "PDF + review",
            UtilityBillSourceKind.LegacyManual =>
                spanish ? "Manual legacy" : "Legacy manual",
            _ => sourceKind
        };
    }

    private string UtilityBillReviewLabel(
        string reviewState)
    {
        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

        return reviewState switch
        {
            UtilityBillReviewState.Reviewed =>
                spanish ? "Revisada" : "Reviewed",
            UtilityBillReviewState.Draft =>
                spanish ? "Borrador" : "Draft",
            UtilityBillReviewState.LegacyUnreviewed =>
                spanish ? "Legacy · revisar" : "Legacy · review",
            _ => reviewState
        };
    }

    private string UtilityBillEvidenceLabel(
        string evidenceState,
        int? sourcePage)
    {
        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);
        var page =
            sourcePage.HasValue
                ? (spanish
                    ? $" · p.{sourcePage.Value}"
                    : $" · p.{sourcePage.Value}")
                : string.Empty;

        return evidenceState switch
        {
            UtilityBillEvidenceState.UserEntered =>
                spanish ? "Ingresado manualmente" : "User entered",
            UtilityBillEvidenceState.PdfExtractedConfirmed =>
                (spanish ? "PDF confirmado" : "PDF confirmed") + page,
            UtilityBillEvidenceState.PdfExtractedReviewRequired =>
                (spanish ? "PDF · revisar" : "PDF · review") + page,
            UtilityBillEvidenceState.NotPrinted =>
                spanish ? "No impreso" : "Not printed",
            UtilityBillEvidenceState.Derived =>
                spanish ? "Derivado" : "Derived",
            UtilityBillEvidenceState.NotProvided =>
                spanish ? "Sin dato ingresado" : "No value entered",
            UtilityBillEvidenceState.LegacyUnreviewed =>
                spanish ? "Legacy · revisar" : "Legacy · review",
            _ => evidenceState + page
        };
    }

    private string UtilityBillAuditEvidenceLabel(
        string raw)
    {
        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

        var value = raw
            .Replace(
                UtilityBillSourceKind.PdfReviewed,
                spanish ? "PDF + revisión" : "PDF + review",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillSourceKind.Manual,
                spanish ? "Manual" : "Manual",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillSourceKind.LegacyManual,
                spanish ? "Manual legacy" : "Legacy manual",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillEvidenceState.PdfExtractedConfirmed,
                spanish ? "PDF confirmado" : "PDF confirmed",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillEvidenceState.PdfExtractedReviewRequired,
                spanish ? "PDF · revisar" : "PDF · review",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillEvidenceState.UserEntered,
                spanish ? "Ingresado por usuario" : "User entered",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillEvidenceState.NotProvided,
                spanish ? "Sin dato ingresado" : "No value entered",
                StringComparison.Ordinal)
            .Replace(
                UtilityBillEvidenceState.LegacyUnreviewed,
                spanish ? "Legacy · revisar" : "Legacy · review",
                StringComparison.Ordinal);

        return value;
    }

    private string BillSectionLabel(string key) =>
        key switch
        {
            "SERVICIO_ELECTRICO" =>
                _localization.GetString(
                    "GridUtility.BillSection.Service"),
            "OTROS_CARGOS" =>
                _localization.GetString(
                    "GridUtility.BillSection.OtherCharges"),
            "ACUMULADO" =>
                _localization.GetString(
                    "GridUtility.BillSection.Accumulated"),
            _ =>
                _localization.GetString(
                    "GridUtility.BillSection.Other")
        };

    private static bool TryParseUtilityLocalDateBoundary(
        QuickDatePicker datePicker,
        string timeZoneId,
        out DateTimeOffset utc)
    {
        utc = default;
        if (!datePicker.SelectedDate.HasValue)
            return false;

        var local = DateTime.SpecifyKind(
            datePicker.SelectedDate.Value.Date,
            DateTimeKind.Unspecified);
        var zone = SolarApiTime.GetTimeZoneInfo(timeZoneId);

        if (zone.IsInvalidTime(local) ||
            zone.IsAmbiguousTime(local))
            return false;

        utc = new DateTimeOffset(
            local,
            zone.GetUtcOffset(local))
            .ToUniversalTime();
        return true;
    }

    private static bool TryParseUtilityLocalInstant(
        QuickDatePicker datePicker,
        TextBox timeTextBox,
        string timeZoneId,
        out DateTimeOffset utc)
    {
        utc = default;
        if (!datePicker.SelectedDate.HasValue)
        {
            return false;
        }

        var formats = new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss" };
        if (!TimeOnly.TryParseExact(
                timeTextBox.Text?.Trim() ?? string.Empty,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            return false;
        }

        var local = DateTime.SpecifyKind(
            datePicker.SelectedDate.Value.Date +
            time.ToTimeSpan(),
            DateTimeKind.Unspecified);
        var zone = SolarApiTime.GetTimeZoneInfo(timeZoneId);

        if (zone.IsInvalidTime(local) ||
            zone.IsAmbiguousTime(local))
        {
            return false;
        }

        utc = new DateTimeOffset(
            local,
            zone.GetUtcOffset(local))
            .ToUniversalTime();
        return true;
    }

    private static bool TryParseRequiredNonNegative(
        string? text,
        out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TryParseNumber(text, out value) &&
               double.IsFinite(value) &&
               value >= 0;
    }

    private static bool TryParseRequiredFinite(
        string? text,
        out double value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(text) &&
               TryParseNumber(text, out value) &&
               double.IsFinite(value);
    }

    private static bool TryParseOptionalFinite(
        string? text,
        out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!TryParseNumber(text, out var parsed) ||
            !double.IsFinite(parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseOptionalNonNegative(
        string? text,
        out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!TryParseNumber(text, out var parsed) ||
            !double.IsFinite(parsed) ||
            parsed < 0)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseNumber(
        string text,
        out double value)
    {
        if (double.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out value))
        {
            return true;
        }

        var normalized = text
            .Trim()
            .Replace(" ", string.Empty)
            .Replace(',', '.');

        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string FormatUtilityInstant(
        DateTimeOffset utc,
        string timeZoneId) =>
        SolarApiTime.ConvertToLocalTime(
            utc,
            timeZoneId)
        .ToString("dd-MM-yyyy HH:mm:ss");

    private static string FormatUtilityInterval(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string timeZoneId) =>
        $"{FormatUtilityInstant(fromUtc, timeZoneId)} → " +
        $"{FormatUtilityInstant(toUtc, timeZoneId)}";

    private static string FormatSigned(
        double value,
        string unit) =>
        $"{value:+0.00;-0.00;0.00} {unit}";

    private string UtilityReadingSourceLabel(string sourceKind) =>
        sourceKind switch
        {
            UtilityReadingSourceKind.UtilityOfficial =>
                _localization.GetString(
                    "GridUtility.ReadingSource.Enel"),
            UtilityReadingSourceKind.Personal =>
                _localization.GetString(
                    "GridUtility.ReadingSource.Personal"),
            _ => sourceKind
        };

    private string UtilitySensitivityLabel(
        UtilitySensitivityRange sensitivity)
    {
        var prefix = _localization.GetString(
            "GridUtility.Sensitivity");

        if (!sensitivity.UpperKwh.HasValue)
        {
            return $"{prefix}: ≥ {sensitivity.LowerKwh:N2} kWh · " +
                   _localization.GetString(
                       "GridUtility.SensitivityUpperUnknown");
        }

        return $"{prefix}: {sensitivity.LowerKwh:N2}–" +
               $"{sensitivity.UpperKwh.Value:N2} kWh · " +
               _localization.GetString(
                   "GridUtility.SensitivityNotConfidence");
    }

    private string UtilityTimeBasisLabel(string timeBasis)
    {
        var key = $"GridUtility.TimeBasis.{timeBasis}";
        var value = _localization.GetString(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? timeBasis
            : value;
    }

    private string FormatUtilityReadingTimestamp(
        UtilityMeterReading reading,
        string timeZoneId)
    {
        var local = SolarApiTime.ConvertToLocalTime(
            reading.ReadingAtUtc,
            timeZoneId);

        return reading.TimePrecision == UtilityTimePrecision.DateOnly
            ? $"{local:dd-MM-yyyy} · {_localization.GetString("GridUtility.OfficialBoundary")}"
            : $"{local:dd-MM-yyyy HH:mm:ss}";
    }

    private string UtilityQualityLabel(string quality)
    {
        var key = $"GridUtility.Quality.{quality}";
        var value = _localization.GetString(key);

        return string.Equals(
            value,
            key,
            StringComparison.Ordinal)
                ? quality
                : value;
    }

    private void ClearUtilitySummary()
    {
        UtilityLatestMeterValueText.Text = "— kWh";
        UtilityLatestInverterValueText.Text = "— kWh";
        UtilityLatestDifferenceValueText.Text = "— kWh";
        UtilityLatestCoverageValueText.Text = "— %";
    }

    private sealed record TariffPublicationViewRow(
        string Authority,
        string Effective,
        string Retroactive,
        string Status,
        string Normalization,
        string Version,
        string Pages,
        string Sha,
        string Title);

    private sealed record UtilityReadingChoice(
        long ReadingId,
        string Display);

    private sealed record UtilityReadingViewRow(
        long ReadingId,
        string Source,
        string LocalTimestamp,
        string ReadingKwh,
        string Reference,
        string Notes);

    private sealed record UtilityReconciliationViewRow(
        string Interval,
        string MeterKwh,
        string InverterKwh,
        string DifferenceKwh,
        string DifferencePercent,
        string Coverage,
        string TimeBasis,
        string Quality);

    private sealed record UtilityAuditVerificationViewRow(
        string Description,
        string PrintedRate,
        string CalculationBasis,
        string Status,
        string Source,
        string Evidence,
        string ActualAmount,
        string Reconstructed,
        string Difference);

    private sealed record UtilityBillChoice(
        long BillId,
        string Display);

    private sealed record UtilityBillViewRow(
        long BillId,
        string Interval,
        string BilledKwh,
        string InverterKwh,
        string DifferenceKwh,
        string Coverage,
        string TotalDue,
        string Source,
        string ReviewState,
        string Reference,
        string Quality);

    private sealed record UtilityBillLineViewRow(
        long BillLineId,
        string Section,
        string Description,
        string Quantity,
        string Unit,
        string UnitRate,
        string Amount,
        string TaxTreatment,
        string Source,
        string Evidence);

    private sealed record UtilityBillPdfDraftLineViewRow(
        string Description,
        string Amount,
        string Evidence);

    private string? _reportDraftOriginPage;
    private string? _lastExportedReportPath;
    private bool _reportExportInProgress;
    private CancellationTokenSource? _reportExportCancellation;
    private int _reportPreviewGeneration;
    private CancellationTokenSource? _reportPreviewCancellation;
    private (string DeviceId, ReportContextSelection Draft)? _dataCoverageContext;

    private void AnalysisToDetailedReport_Click(object sender, RoutedEventArgs e) =>
        OpenAnalysisReportDraft(ReportKind.DetailedEnergy);

    private void AnalysisToSimpleReport_Click(object sender, RoutedEventArgs e) =>
        OpenAnalysisReportDraft(ReportKind.SimpleEnergy);

    private void AnalysisToBatteryReport_Click(object sender, RoutedEventArgs e) =>
        OpenAnalysisReportDraft(ReportKind.Battery);

    private bool IsCurrentAnalysisReadyForReport()
    {
        var device = _profiles.Get()?.DeviceId;
        return !_windowClosed &&
               AnalysisContent.Visibility == Visibility.Visible &&
               _analysisRenderedAggregation is not null &&
               _analysisAggregationRows.Count > 0 &&
               !_analysisCustomRangePendingApply &&
               !_analysisPresetLoading && !_analysisDataLoading &&
               !string.IsNullOrEmpty(device) &&
               string.Equals(_analysisRenderedDeviceId, device,
                   StringComparison.Ordinal) &&
               _analysisRenderedGeneration == _analysisRefreshGeneration &&
               AnalysisFromDatePicker.SelectedDate.HasValue &&
               AnalysisToDatePicker.SelectedDate.HasValue;
    }

    private void OpenAnalysisReportDraft(ReportKind kind)
    {
        if (!IsCurrentAnalysisReadyForReport())
        {
            AnalysisStatusText.Text = _localization.GetString(
                "Reports.AnalysisNotReady");
            return;
        }

        var from = DateOnly.FromDateTime(AnalysisFromDatePicker.SelectedDate!.Value);
        var to = DateOnly.FromDateTime(AnalysisToDatePicker.SelectedDate!.Value);
        var rawAggregation = AnalysisAggregationSelector.SelectedValue?.ToString();
        if (!Enum.TryParse<AggregationPeriod>(rawAggregation, true,
                out var analysisAggregation))
            analysisAggregation = AggregationPeriod.Day;

        var draft = ReportContextNavigationPolicy.FromAnalysis(
            from, to, analysisAggregation, kind);
        OpenContextReportDraft(draft, "Analysis");
    }

    private void AnalysisSelectedRowReport_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedAnalysisRowReport(ReportKind.DetailedEnergy);

    private void AnalysisSelectedRowBatteryReport_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedAnalysisRowReport(ReportKind.Battery);

    private void AnalysisSelectedRowSimpleReport_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedAnalysisRowReport(ReportKind.SimpleEnergy);

    private void OpenSelectedAnalysisRowReport(ReportKind reportKind)
    {
        if (!IsCurrentAnalysisReadyForReport() ||
            AnalysisAggregationGrid.SelectedItem is not EnergyAggregationRow selected ||
            !_analysisAggregationRows.Contains(selected))
        {
            AnalysisStatusText.Text =
                _localization.GetString("Reports.SelectAnalysisRow");
            return;
        }
        var timeZone = _profiles.Get()?.StationTimeZone;
        var draft = ReportContextNavigationPolicy.FromSelectedRow(
            selected, string.IsNullOrWhiteSpace(timeZone)
                ? "America/Santiago" : timeZone, reportKind);
        OpenContextReportDraft(draft, "Analysis");
    }

    private void BatteryToReport_Click(object sender, RoutedEventArgs e) =>
        OpenBatteryReportPreset("latest-month");

    private void BatteryToWeekReport_Click(object sender, RoutedEventArgs e) =>
        OpenBatteryReportPreset("rolling-7");

    private void BatteryToAllReport_Click(object sender, RoutedEventArgs e) =>
        OpenBatteryReportPreset("all");

    private void OpenBatteryReportPreset(string preset)
    {
        if (_windowClosed || BatteryContent.Visibility != Visibility.Visible ||
            _profiles.Get() is null)
            return;
        ShowPage("Reports");
        if (!ReportExportExcelButton.IsEnabled) return;

        // Ranges are anchored in STORED data rather than today's date.
        _suppressReportRangeSelection = true;
        try
        {
            ReportTypeSelector.SelectedValue = ReportKind.Battery.ToString();
            ReportRangePresetSelector.SelectedValue = preset;
            ReportAggregationSelector.SelectedValue = AggregationPeriod.Day.ToString();
        }
        finally { _suppressReportRangeSelection = false; }

        if (!ApplyReportRangePreset())
        {
            ReportStatusText.Text = _localization.GetString("Reports.NoData");
            return;
        }
        SyncReportDatePartSelectorsFromDates();
        ReportTitleTextBox.Text = GetDefaultReportTitle(ReportKind.Battery);
        UpdateReportSelectionSummary();
        var bannerKey = preset switch
        {
            "rolling-7" => "Reports.DraftFromBatteryWeek",
            "all" => "Reports.DraftFromBatteryAll",
            _ => "Reports.DraftFromBattery"
        };
        ShowReportDraftSource("Battery", false, bannerKey);
    }

    private void BatteryToAnalysis_Click(object sender, RoutedEventArgs e) =>
        OpenBatteryAnalysisPreset("rolling-7");

    private void BatteryToMonthAnalysis_Click(object sender, RoutedEventArgs e) =>
        OpenBatteryAnalysisPreset("latest-month");

    private void OpenBatteryAnalysisPreset(string preset)
    {
        if (_windowClosed || BatteryContent.Visibility != Visibility.Visible ||
            _profiles.Get() is null)
            return;
        ShowPage("Analysis");
        // Re-select intentionally so a past Analysis visit cannot leave stale dates.
        _suppressAnalysisRangeSelection = true;
        try { AnalysisRangePresetSelector.SelectedValue = null; }
        finally { _suppressAnalysisRangeSelection = false; }
        AnalysisRangePresetSelector.SelectedValue = preset;
    }

    private void OpenContextReportDraft(ReportContextSelection context, string origin)
    {
        if (_windowClosed || _profiles.Get() is null)
            return;
        ShowPage("Reports");
        if (!ReportExportExcelButton.IsEnabled) return;

        // A programmatic context should update all selectors atomically.
        // It deliberately only PREFILLS; exporting still requires a click.
        _suppressReportRangeSelection = true;
        try
        {
            ReportRangePresetSelector.SelectedValue = "custom";
            ReportFromDatePicker.SelectedDate = context.From.ToDateTime(TimeOnly.MinValue);
            ReportToDatePicker.SelectedDate = context.To.ToDateTime(TimeOnly.MinValue);
            ReportTypeSelector.SelectedValue = context.Kind.ToString();
            ReportAggregationSelector.SelectedValue = context.Aggregation.ToString();
        }
        finally { _suppressReportRangeSelection = false; }

        SyncReportDatePartSelectorsFromDates();
        ReportTitleTextBox.Text = GetDefaultReportTitle(context.Kind);
        UpdateReportSelectionSummary();
        ShowReportDraftSource(origin, context.ExpandedToCalendarDay);
    }

    private void ShowReportDraftSource(
        string origin, bool expandedDay, string? localizedMessageKey = null)
    {
        _reportDraftOriginPage = origin;
        ReportContextText.Text = localizedMessageKey is not null
            ? _localization.GetString(localizedMessageKey)
            : _localization.GetString(origin switch
            {
                "Battery" => "Reports.DraftFromBattery",
                "Data" => "Reports.DraftFromData",
                _ when expandedDay => "Reports.DraftFromAnalysisExpanded",
                _ => "Reports.DraftFromAnalysis"
            });
        ReportContextPanel.Visibility = Visibility.Visible;
    }

    private void ReportsBackToSource_Click(object sender, RoutedEventArgs e)
    {
        var source = _reportDraftOriginPage;
        if (source is not ("Analysis" or "Battery" or "Data")) return;
        _reportDraftOriginPage = null;
        ReportContextPanel.Visibility = Visibility.Collapsed;
        ShowPage(source);
    }

    private void RefreshReportsView(bool initializeRange = false)
    {
        if (!IsInitialized || ReportsContent is null)
        {
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
        {
            ReportStatusText.Text = _localization.GetString("Reports.NoProfile");
            ReportExportExcelButton.IsEnabled = false;
            ReportExportPdfButton.IsEnabled = false;
            LoadSavedReportPresets();
            return;
        }

        var history = _services.GetRequiredService<HistoryRepository>();
        var coverage = history.GetCoverageSummary(profile.DeviceId);

        if (!coverage.FirstSampleAtUtc.HasValue ||
            !coverage.LastSampleAtUtc.HasValue)
        {
            ReportStatusText.Text = _localization.GetString("Reports.NoData");
            ReportExportExcelButton.IsEnabled = false;
            ReportExportPdfButton.IsEnabled = false;
            LoadSavedReportPresets();
            return;
        }

        if (initializeRange ||
            !ReportFromDatePicker.SelectedDate.HasValue ||
            !ReportToDatePicker.SelectedDate.HasValue)
        {
            var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
                ? "America/Santiago"
                : profile.StationTimeZone;

            var first = SolarApiTime.GetLocalDate(
                coverage.FirstSampleAtUtc.Value,
                timeZone);
            var last = SolarApiTime.GetLocalDate(
                coverage.LastSampleAtUtc.Value,
                timeZone);

            _suppressReportRangeSelection = true;
            ReportRangePresetSelector.SelectedValue = "all";
            ReportFromDatePicker.SelectedDate =
                first.ToDateTime(TimeOnly.MinValue);
            ReportToDatePicker.SelectedDate =
                last.ToDateTime(TimeOnly.MinValue);
            _suppressReportRangeSelection = false;
        }

        ReportExportExcelButton.IsEnabled = true;
        ReportExportPdfButton.IsEnabled = true;
        InitializeReportDatePartSelectors(profile, coverage);
        SyncReportDatePartSelectorsFromDates();
        if (string.IsNullOrWhiteSpace(ReportTitleTextBox.Text))
        {
            ReportTitleTextBox.Text = GetDefaultReportTitle(GetReportKind());
        }
        LoadSavedReportPresets();
        UpdateReportSelectionSummary();
    }

    private void LoadSavedReportPresets()
    {
        if (!IsInitialized || SavedReportPresetSelector is null)
        {
            return;
        }

        var selectedName =
            (SavedReportPresetSelector.SelectedItem as ReportPreset)?.Name;

        var presets =
            _services.GetRequiredService<ReportPresetStore>().GetAll();

        _suppressReportPresetSelection = true;
        SavedReportPresetSelector.ItemsSource = presets;

        if (!string.IsNullOrWhiteSpace(selectedName))
        {
            SavedReportPresetSelector.SelectedItem =
                presets.FirstOrDefault(item =>
                    string.Equals(
                        item.Name,
                        selectedName,
                        StringComparison.OrdinalIgnoreCase));
        }

        _suppressReportPresetSelection = false;
        ReportDeletePresetButton.IsEnabled =
            SavedReportPresetSelector.SelectedItem is ReportPreset;
    }

    private void ReportRangePresetSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressReportRangeSelection || !IsInitialized)
        {
            return;
        }

        ApplyReportRangePreset();
        SyncReportDatePartSelectorsFromDates();
        UpdateReportSelectionSummary();
    }

    private void ReportDatePicker_SelectedDateChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressReportRangeSelection || !IsInitialized)
        {
            return;
        }

        _suppressReportRangeSelection = true;
        ReportRangePresetSelector.SelectedValue = "custom";
        _suppressReportRangeSelection = false;
        SyncReportDatePartSelectorsFromDates();
        UpdateReportSelectionSummary();
    }

    private void ReportDatePartSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressReportDatePartSelection || !IsInitialized)
        {
            return;
        }

        var isFrom =
            ReferenceEquals(sender, ReportFromMonthSelector) ||
            ReferenceEquals(sender, ReportFromYearSelector);

        var picker = isFrom ? ReportFromDatePicker : ReportToDatePicker;
        var monthSelector =
            isFrom ? ReportFromMonthSelector : ReportToMonthSelector;
        var yearSelector =
            isFrom ? ReportFromYearSelector : ReportToYearSelector;

        if (monthSelector.SelectedValue is not int month ||
            yearSelector.SelectedItem is not int year)
        {
            return;
        }

        var current = picker.SelectedDate ?? DateTime.Today;
        var day = Math.Min(
            current.Day,
            DateTime.DaysInMonth(year, month));

        _suppressReportRangeSelection = true;
        picker.SelectedDate = new DateTime(year, month, day);
        ReportRangePresetSelector.SelectedValue = "custom";
        _suppressReportRangeSelection = false;

        SyncReportDatePartSelectorsFromDates();
        UpdateReportSelectionSummary();
    }

    private void InitializeReportDatePartSelectors(
        CommissioningProfile profile,
        HistoryCoverageSummary coverage)
    {
        if (!coverage.FirstSampleAtUtc.HasValue ||
            !coverage.LastSampleAtUtc.HasValue)
        {
            return;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;
        var first = SolarApiTime.GetLocalDate(
            coverage.FirstSampleAtUtc.Value,
            timeZone);
        var last = SolarApiTime.GetLocalDate(
            coverage.LastSampleAtUtc.Value,
            timeZone);

        var culture = CultureInfo.GetCultureInfo(
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase)
                ? "es-CL"
                : "en-US");

        var months = Enumerable.Range(1, 12)
            .Select(month => new KeyValuePair<int, string>(
                month,
                culture.TextInfo.ToTitleCase(
                    culture.DateTimeFormat.GetMonthName(month))))
            .ToArray();
        var years = Enumerable.Range(
                first.Year,
                Math.Max(1, last.Year - first.Year + 1))
            .ToArray();

        _suppressReportDatePartSelection = true;
        ReportFromMonthSelector.ItemsSource = months;
        ReportToMonthSelector.ItemsSource = months;
        ReportFromYearSelector.ItemsSource = years;
        ReportToYearSelector.ItemsSource = years;
        _suppressReportDatePartSelection = false;
    }

    private void SyncReportDatePartSelectorsFromDates()
    {
        if (ReportFromMonthSelector is null ||
            ReportFromYearSelector is null ||
            ReportToMonthSelector is null ||
            ReportToYearSelector is null)
        {
            return;
        }

        _suppressReportDatePartSelection = true;

        if (ReportFromDatePicker.SelectedDate.HasValue)
        {
            ReportFromMonthSelector.SelectedValue =
                ReportFromDatePicker.SelectedDate.Value.Month;
            ReportFromYearSelector.SelectedItem =
                ReportFromDatePicker.SelectedDate.Value.Year;
        }

        if (ReportToDatePicker.SelectedDate.HasValue)
        {
            ReportToMonthSelector.SelectedValue =
                ReportToDatePicker.SelectedDate.Value.Month;
            ReportToYearSelector.SelectedItem =
                ReportToDatePicker.SelectedDate.Value.Year;
        }

        _suppressReportDatePartSelection = false;
    }

    private void ReportAggregationSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateReportSelectionSummary();
        }
    }

    private void ReportTypeSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        var currentTitle = ReportTitleTextBox.Text?.Trim() ?? string.Empty;
        var knownDefaults = Enum.GetValues<ReportKind>()
            .Select(GetDefaultReportTitle)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        if (string.IsNullOrWhiteSpace(currentTitle) ||
            knownDefaults.Contains(currentTitle))
        {
            ReportTitleTextBox.Text =
                GetDefaultReportTitle(GetReportKind());
        }

        UpdateReportSelectionSummary();
    }

    private void ReportRefreshSelection_Click(
        object sender,
        RoutedEventArgs e)
    {
        ApplyReportRangePreset();
        SyncReportDatePartSelectorsFromDates();
        UpdateReportSelectionSummary();
    }

    private bool ApplyReportRangePreset()
    {
        var profile = _profiles.Get();
        if (profile is null)
        {
            return false;
        }

        var history = _services.GetRequiredService<HistoryRepository>();
        var coverage = history.GetCoverageSummary(profile.DeviceId);
        if (!coverage.FirstSampleAtUtc.HasValue ||
            !coverage.LastSampleAtUtc.HasValue)
        {
            return false;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var firstLocalDate = SolarApiTime.GetLocalDate(
            coverage.FirstSampleAtUtc.Value,
            timeZone);
        var lastLocalDate = SolarApiTime.GetLocalDate(
            coverage.LastSampleAtUtc.Value,
            timeZone);

        var preset =
            ReportRangePresetSelector.SelectedValue?.ToString() ??
            "custom";

        if (preset == "custom")
        {
            return false;
        }

        var resolver =
            _services.GetRequiredService<TimeRangeSelectionService>();

        ResolvedTimeRange resolved = preset switch
        {
            "latest-day" =>
                resolver.ForDay(lastLocalDate, timeZone),
            "rolling-7" =>
                resolver.ForRolling7Days(lastLocalDate, timeZone),
            "latest-month" =>
                resolver.ForMonth(
                    lastLocalDate.Year,
                    lastLocalDate.Month,
                    timeZone),
            "last-3-complete-months" =>
                resolver.ForLastNCompleteCalendarMonths(
                    lastLocalDate,
                    3,
                    timeZone),
            "rolling-12-months" =>
                resolver.ForRolling12Months(
                    lastLocalDate,
                    timeZone),
            "year-to-date" =>
                resolver.ForYearToDate(
                    lastLocalDate,
                    timeZone),
            _ =>
                resolver.ForArbitraryDateRange(
                    firstLocalDate,
                    lastLocalDate,
                    timeZone)
        };

        _suppressReportRangeSelection = true;
        ReportFromDatePicker.SelectedDate =
            resolved.LocalStartDate.ToDateTime(TimeOnly.MinValue);
        ReportToDatePicker.SelectedDate =
            resolved.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        _suppressReportRangeSelection = false;
        return true;
    }

    private AggregationPeriod GetReportAggregation()
    {
        var raw = ReportAggregationSelector.SelectedValue?.ToString();
        return Enum.TryParse<AggregationPeriod>(
            raw,
            ignoreCase: true,
            out var parsed)
            ? parsed
            : AggregationPeriod.Day;
    }

    private ReportKind GetReportKind()
    {
        var raw = ReportTypeSelector.SelectedValue?.ToString();
        return Enum.TryParse<ReportKind>(
            raw,
            ignoreCase: true,
            out var parsed)
            ? parsed
            : ReportKind.SimpleEnergy;
    }

    private string GetDefaultReportTitle(ReportKind kind) =>
        kind switch
        {
            ReportKind.DetailedEnergy =>
                _localization.GetString("Reports.DefaultTitle.Detailed"),
            ReportKind.Battery =>
                _localization.GetString("Reports.DefaultTitle.Battery"),
            _ =>
                _localization.GetString("Reports.DefaultTitle.Simple")
        };

    private EnergyReportRequest? GetCurrentReportRequest()
    {
        var profile = _profiles.Get();
        if (profile is null ||
            !ReportFromDatePicker.SelectedDate.HasValue ||
            !ReportToDatePicker.SelectedDate.HasValue)
        {
            return null;
        }

        var first = DateOnly.FromDateTime(
            ReportFromDatePicker.SelectedDate.Value);
        var second = DateOnly.FromDateTime(
            ReportToDatePicker.SelectedDate.Value);
        if (first > second)
            return null;

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var resolved =
            _services.GetRequiredService<TimeRangeSelectionService>()
                .ForArbitraryDateRange(
                    first,
                    second,
                    timeZone);

        var kind = GetReportKind();
        var title =
            string.IsNullOrWhiteSpace(ReportTitleTextBox.Text)
                ? GetDefaultReportTitle(kind)
                : ReportTitleTextBox.Text.Trim();

        return new EnergyReportRequest(
            title,
            profile.DeviceId,
            resolved.LocalStartDate,
            resolved.LocalEndDate,
            resolved.StartUtc,
            resolved.EndUtc,
            timeZone,
            GetReportAggregation(),
            kind,
            _localization.CurrentLanguage);
    }

    private void UpdateReportSelectionSummary()
    {
        if (!IsInitialized || ReportSelectionSummaryText is null)
        {
            return;
        }

        InvalidateReportPreview();
        var request = GetCurrentReportRequest();
        if (request is null)
        {
            ReportSelectionSummaryText.Text =
                ReportFromDatePicker.SelectedDate > ReportToDatePicker.SelectedDate
                    ? _localization.GetString("Reports.InvalidRange")
                    : _localization.GetString("Reports.NoData");
            return;
        }

        ReportSelectionSummaryText.Text = string.Format(
            _localization.GetString("Reports.SelectionSummary"),
            request.LocalStartDate.ToString("dd-MM-yyyy"),
            request.LocalEndDate.ToString("dd-MM-yyyy"),
            request.Aggregation,
            _localization.GetString(
                request.Kind switch
                {
                    ReportKind.DetailedEnergy => "Reports.Type.Detailed",
                    ReportKind.Battery => "Reports.Type.Battery",
                    _ => "Reports.Type.Simple"
                }));
        ReportStatusText.Text = string.Empty;
    }

    private void InvalidateReportPreview()
    {
        _reportPreviewGeneration++;
        _reportPreviewCancellation?.Cancel();
        _reportPreviewCancellation = null;
        if (ReportPreviewButton is null) return;
        ReportPreviewButton.IsEnabled = !_reportExportInProgress;
        ReportPreviewCancelButton.IsEnabled = false;
        ReportPreviewResultPanel.Visibility = Visibility.Collapsed;
        ReportPreviewStatusText.Text = string.Empty;
        ReportPreviewCoverageText.Text = string.Empty;
    }

    private void ReportInspectAnalysis_Click(object sender, RoutedEventArgs e)
    {
        var request = GetCurrentReportRequest();
        if (request is null || _windowClosed ||
            ReportsContent.Visibility != Visibility.Visible)
        {
            ReportStatusText.Text = _localization.GetString("Reports.InvalidSelection");
            return;
        }

        var context = ReportContextNavigationPolicy.FromReport(request);
        ShowPage("Analysis");
        // Explicitly override any retained Analysis preset instead of
        // reverting to the last saved day or to the computer's current date.
        _analysisPresetGeneration++;
        _analysisPresetLoading = false;
        _suppressAnalysisRangeSelection = true;
        try
        {
            AnalysisRangePresetSelector.SelectedValue = "custom";
            AnalysisFromDatePicker.SelectedDate =
                context.From.ToDateTime(TimeOnly.MinValue);
            AnalysisToDatePicker.SelectedDate =
                context.To.ToDateTime(TimeOnly.MinValue);
            AnalysisAggregationSelector.SelectedValue = context.Aggregation.ToString();
        }
        finally { _suppressAnalysisRangeSelection = false; }

        _analysisCustomRangePendingApply = false;
        RefreshAnalysisView();
    }

    private void ReportPreviewCancel_Click(object sender, RoutedEventArgs e)
    {
        InvalidateReportPreview();
        ReportPreviewStatusText.Text = _localization.GetString("Reports.PreviewCancelled");
    }

    private string FormatReportPreviewMetric(PowerMetricStatistics metric, bool signedBattery = false)
    {
        var evidence = ReportPreviewEvidencePolicy.Evaluate(metric);
        if (!evidence.PositiveEnergyKwh.HasValue || !evidence.CoveragePercent.HasValue)
            return string.Format(_localization.GetString("Reports.PreviewInsufficient"), evidence.SampleCount);
        return signedBattery
            ? string.Format(_localization.GetString("Reports.PreviewBatteryValue"),
                evidence.PositiveEnergyKwh.Value, evidence.NegativeEnergyKwh!.Value,
                evidence.CoveragePercent.Value, evidence.SampleCount)
            : string.Format(_localization.GetString("Reports.PreviewEnergyValue"),
                evidence.PositiveEnergyKwh.Value, evidence.CoveragePercent.Value, evidence.SampleCount);
    }

    private async void ReportPreview_Click(object sender, RoutedEventArgs e)
    {
        InvalidateReportPreview();
        if (_reportExportInProgress)
        {
            ReportPreviewStatusText.Text = _localization.GetString("Reports.PreviewExportBusy");
            return;
        }
        var request = GetCurrentReportRequest();
        if (request is null || ReportsContent.Visibility != Visibility.Visible)
        {
            ReportPreviewStatusText.Text = _localization.GetString("Reports.PreviewInvalid");
            return;
        }

        var generation = _reportPreviewGeneration;
        var cancellation = new CancellationTokenSource();
        _reportPreviewCancellation = cancellation;
        ReportPreviewButton.IsEnabled = false;
        ReportPreviewCancelButton.IsEnabled = true;
        ReportPreviewStatusText.Text = _localization.GetString("Reports.PreviewLoading");

        try
        {
            // Run only the existing report-equivalent range integrator.
            // Preview never saves or exports a file.
            var summary = await Task.Run(() =>
                _services.GetRequiredService<EnergyRangeStatisticsService>()
                    .Get(request.DeviceId, request.StartUtc, request.EndUtc, cancellation.Token),
                cancellation.Token);
            if (!ReportPreviewEvidencePolicy.CanApply(
                    generation, _reportPreviewGeneration,
                    !_windowClosed && ReportsContent.Visibility == Visibility.Visible,
                    request.DeviceId, _profiles.Get()?.DeviceId) ||
                request != GetCurrentReportRequest())
                return;

            ReportPreviewSolarText.Text = FormatReportPreviewMetric(summary.PvPower);
            ReportPreviewHouseText.Text = FormatReportPreviewMetric(summary.HouseLoadPower);
            ReportPreviewGridText.Text = FormatReportPreviewMetric(summary.GridImportPower);
            ReportPreviewBatteryText.Text = FormatReportPreviewMetric(summary.BatteryPower, true);
            var quality = ReportPreviewEvidencePolicy.Summarize(summary);
            ReportPreviewCoverageText.Text = quality.EligibleStreams switch
            {
                0 => _localization.GetString("Reports.PreviewQualityNone"),
                4 => string.Format(_localization.GetString("Reports.PreviewQualityAll"),
                    quality.MinimumEligibleCoveragePercent!.Value),
                _ => string.Format(_localization.GetString("Reports.PreviewQualityPartial"),
                    quality.EligibleStreams, quality.UnavailableStreams,
                    quality.MinimumEligibleCoveragePercent!.Value)
            };
            ReportPreviewResultPanel.Visibility = Visibility.Visible;
            ReportPreviewStatusText.Text = _localization.GetString("Reports.PreviewComplete");
        }
        catch (OperationCanceledException)
        {
            // The explicit Cancel button has already displayed its own state.
        }
        catch (Exception ex)
        {
            if (ReportPreviewEvidencePolicy.CanApply(
                generation, _reportPreviewGeneration,
                !_windowClosed && ReportsContent.Visibility == Visibility.Visible,
                request.DeviceId, _profiles.Get()?.DeviceId))
            {
                ReportPreviewStatusText.Text = _localization.GetString("Reports.PreviewFailed");
                Debug.WriteLine($"Report preview failed ({ex.GetType().Name}).");
            }
        }
        finally
        {
            if (generation == _reportPreviewGeneration)
            {
                ReportPreviewButton.IsEnabled = true;
                ReportPreviewCancelButton.IsEnabled = false;
                _reportPreviewCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private void ReportSavePreset_Click(
        object sender,
        RoutedEventArgs e)
    {
        var name = ReportPresetNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                _localization.GetString("Reports.PresetNameRequired"),
                _localization.GetString("Page.Reports.Title"));
            return;
        }

        var request = GetCurrentReportRequest();
        if (request is null)
        {
            return;
        }

        var preset = new ReportPreset(
            name,
            request.Kind,
            ReportRangePresetSelector.SelectedValue?.ToString() ?? "custom",
            request.LocalStartDate,
            request.LocalEndDate,
            request.Aggregation,
            DateTimeOffset.UtcNow);

        _services.GetRequiredService<ReportPresetStore>()
            .Save(preset);

        LoadSavedReportPresets();
        SavedReportPresetSelector.SelectedItem =
            (SavedReportPresetSelector.ItemsSource as IEnumerable<ReportPreset>)
                ?.FirstOrDefault(item =>
                    string.Equals(
                        item.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase));

        ReportStatusText.Text =
            _localization.GetString("Reports.PresetSaved");
    }

    private void SavedReportPresetSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressReportPresetSelection ||
            SavedReportPresetSelector.SelectedItem is not ReportPreset preset)
        {
            ReportDeletePresetButton.IsEnabled = false;
            return;
        }

        ReportDeletePresetButton.IsEnabled = true;
        ReportPresetNameTextBox.Text = preset.Name;
        ReportTypeSelector.SelectedValue = preset.Kind.ToString();

        _suppressReportRangeSelection = true;
        ReportRangePresetSelector.SelectedValue = preset.RangePreset;
        ReportFromDatePicker.SelectedDate =
            preset.LocalStartDate.ToDateTime(TimeOnly.MinValue);
        ReportToDatePicker.SelectedDate =
            preset.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        ReportAggregationSelector.SelectedValue =
            preset.Aggregation.ToString();
        _suppressReportRangeSelection = false;

        if (preset.RangePreset != "custom")
        {
            ApplyReportRangePreset();
        }

        SyncReportDatePartSelectorsFromDates();
        UpdateReportSelectionSummary();
    }

    private void ReportDeletePreset_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SavedReportPresetSelector.SelectedItem is not ReportPreset preset)
        {
            return;
        }

        _services.GetRequiredService<ReportPresetStore>()
            .Delete(preset.Name);
        ReportPresetNameTextBox.Clear();
        LoadSavedReportPresets();
        ReportStatusText.Text =
            _localization.GetString("Reports.PresetDeleted");
    }

    private void RefreshLastReportActions()
    {
        var ready = ReportExportLaunchPolicy.CanOpen(_lastExportedReportPath);
        ReportOpenLastFileButton.IsEnabled = ready;
        ReportOpenLastFolderButton.IsEnabled = ready;
    }

    private void ReportOpenLastFile_Click(object sender, RoutedEventArgs e) =>
        OpenLastReport(folderOnly: false);

    private void ReportOpenLastFolder_Click(object sender, RoutedEventArgs e) =>
        OpenLastReport(folderOnly: true);

    private void OpenLastReport(bool folderOnly)
    {
        // Only a successful explicit export can set this session's path.
        // Re-check that the file still exists before launching Explorer or its
        // registered PDF/XLSX viewer; never open arbitrary persisted paths.
        if (!ReportExportLaunchPolicy.CanOpen(_lastExportedReportPath))
        {
            RefreshLastReportActions();
            ReportStatusText.Text = _localization.GetString("Reports.LastFileUnavailable");
            return;
        }

        try
        {
            var selected = folderOnly
                ? Path.GetDirectoryName(Path.GetFullPath(_lastExportedReportPath!))
                : _lastExportedReportPath;
            if (string.IsNullOrWhiteSpace(selected))
                throw new IOException("The report output folder is unavailable.");
            Process.Start(new ProcessStartInfo
            {
                FileName = selected,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ReportStatusText.Text = _localization.GetString("Reports.OpenLastFailed");
            Debug.WriteLine($"Opening exported report failed ({ex.GetType().Name}).");
        }
    }

    private void ReportExportCancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_reportExportInProgress || _reportExportCancellation is null)
            return;
        _reportExportCancellation.Cancel();
        ReportExportCancelButton.IsEnabled = false;
        ReportStatusText.Text = _localization.GetString("Reports.ExportCancelPending");
    }

    private async void ReportExportExcel_Click(
        object sender,
        RoutedEventArgs e)
    {
        await ExportEnergyReportAsync("xlsx");
    }

    private async void ReportExportPdf_Click(
        object sender,
        RoutedEventArgs e)
    {
        await ExportEnergyReportAsync("pdf");
    }

    private async Task ExportEnergyReportAsync(string format)
    {
        if (_reportExportInProgress) return;
        var request = GetCurrentReportRequest();
        if (request is null)
        {
            ReportStatusText.Text = _localization.GetString("Reports.InvalidSelection");
            return;
        }

        var extension = format == "xlsx" ? "xlsx" : "pdf";
        var dialog = new SaveFileDialog
        {
            Title = _localization.GetString("Reports.SaveTitle"),
            Filter = format == "xlsx"
                ? "Excel (*.xlsx)|*.xlsx"
                : "PDF (*.pdf)|*.pdf",
            DefaultExt = extension,
            AddExtension = true,
            FileName =
                $"{SafeReportFileStem(request.Title)}_{request.LocalStartDate:yyyyMMdd}_{request.LocalEndDate:yyyyMMdd}.{extension}"
        };

        PrepareExportDialog(dialog);

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _reportExportInProgress = true;
        InvalidateReportPreview();
        using var cancellation = new CancellationTokenSource();
        _reportExportCancellation = cancellation;
        var cancellationToken = cancellation.Token;
        string? stagedPath = null;
        var overallWatch = Stopwatch.StartNew();
        var stageWatch = Stopwatch.StartNew();
        ReportExportCancelButton.IsEnabled = true;
        ReportExportExcelButton.IsEnabled = false;
        ReportExportPdfButton.IsEnabled = false;
        ReportExportProgressLabel.Visibility = Visibility.Visible;
        ReportExportProgressBar.Visibility = Visibility.Visible;
        // These stages represent progress through operations, not measured
        // percent complete. Keep the long-running indicator indeterminate.
        ReportExportProgressBar.IsIndeterminate = true;
        ReportExportProgressBar.Value = 0;
        ReportExportProgressLabel.Text =
            _localization.GetString("Reports.ExportPreparing");
        ReportStatusText.Text = string.Empty;
        ReportExportTimingText.Text = string.Empty;
        SetGlobalOperation(
            true,
            _localization.CurrentLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "Exportando informe..."
                : "Exporting report...");

        try
        {
            var exporter =
                _services.GetRequiredService<EnergyReportExportService>();

            var report = await Task.Run(() => exporter.Build(request, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var readElapsed = stageWatch.Elapsed;
            _performance.Record("Data.Reports.Build", readElapsed);
            stageWatch.Restart();

            ReportExportProgressLabel.Text =
                _localization.GetString("Reports.ExportGenerating");

            stagedPath = ReportFilePublicationService.CreateStagingPath(dialog.FileName);
            var outputStage = stagedPath;
            await Task.Run(() =>
            {
                if (format == "xlsx")
                {
                    exporter.ExportExcel(outputStage, report, cancellationToken);
                }
                else
                {
                    exporter.ExportPdf(outputStage, report, cancellationToken);
                }
            });

            cancellationToken.ThrowIfCancellationRequested();
            var renderElapsed = stageWatch.Elapsed;
            _performance.Record(format == "xlsx" ? "Data.Reports.ExcelRender" :
                "Data.Reports.PdfRender", renderElapsed);
            stageWatch.Restart();
            if (!ReportExportPublicationPolicy.CanPublish(
                    request, GetCurrentReportRequest(), !_windowClosed,
                    ReportsContent.Visibility == Visibility.Visible,
                    cancellation.IsCancellationRequested))
                throw new InvalidOperationException(
                    _localization.GetString("Reports.ExportSelectionChanged"));
            ReportFilePublicationService.Publish(outputStage, dialog.FileName);
            stagedPath = null;
            _performance.Record("Data.Reports.Publish", stageWatch.Elapsed);
            _performance.Record("Data.Reports.Total", overallWatch.Elapsed);
            ReportExportTimingText.Text = string.Format(
                _localization.GetString("Reports.ExportTiming"),
                readElapsed.TotalSeconds, renderElapsed.TotalSeconds,
                overallWatch.Elapsed.TotalSeconds);
            ReportExportProgressBar.IsIndeterminate = false;
            ReportExportProgressBar.Value = 100;
            ReportExportProgressLabel.Text =
                _localization.GetString("Reports.ExportComplete");
            System.Media.SystemSounds.Asterisk.Play();
            ReportStatusText.Text = string.Format(
                _localization.GetString("Reports.ExportSaved"),
                dialog.FileName);
            _lastExportedReportPath = dialog.FileName;
            RefreshLastReportActions();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _performance.Record("Data.Reports.CancelObserved", overallWatch.Elapsed);
            ReportExportTimingText.Text = string.Format(
                _localization.GetString("Reports.ExportCancelTiming"),
                overallWatch.Elapsed.TotalSeconds);
            ReportExportProgressBar.IsIndeterminate = false;
            ReportExportProgressBar.Value = 0;
            ReportExportProgressLabel.Text =
                _localization.GetString("Reports.ExportCancelled");
            ReportStatusText.Text =
                _localization.GetString("Reports.ExportCancelledExplanation");
        }
        catch (Exception ex)
        {
            _performance.Record("Data.Reports.Failed", overallWatch.Elapsed);
            ReportExportTimingText.Text = string.Format(
                _localization.GetString("Reports.ExportFailedTiming"),
                overallWatch.Elapsed.TotalSeconds);
            ReportExportProgressBar.IsIndeterminate = false;
            ReportExportProgressBar.Value = 0;
            ReportExportProgressLabel.Text =
                _localization.GetString("Reports.ExportFailed");
            ReportStatusText.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                _localization.GetString("Page.Reports.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            try
            {
                ReportFilePublicationService.DiscardStaging(stagedPath);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Incomplete report staging cleanup failed ({ex.GetType().Name}).");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Incomplete report staging cleanup failed ({ex.GetType().Name}).");
            }
            _reportExportInProgress = false;
            if (ReferenceEquals(_reportExportCancellation, cancellation))
                _reportExportCancellation = null;
            ReportExportCancelButton.IsEnabled = false;
            if (!_windowClosed && ReportsContent.Visibility == Visibility.Visible)
                ReportPreviewButton.IsEnabled = true;
            ReportExportExcelButton.IsEnabled = true;
            ReportExportPdfButton.IsEnabled = true;
            SetGlobalOperation(false, string.Empty);
        }
    }

    private static string SafeReportFileStem(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = title
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var stem = new string(chars)
            .Trim()
            .Trim('.');

        if (string.IsNullOrWhiteSpace(stem))
        {
            return "SolarEnergy";
        }

        return stem.Length <= 72 ? stem : stem[..72].TrimEnd();
    }

    private async void UpdateData_Click(object sender, RoutedEventArgs e)
    {
        if (_syncCancellation is not null)
        {
            return;
        }

        var profile = _profiles.Get();

        if (profile is null || !_session.HasSession)
        {
            OpenCommissioningWindow();
            return;
        }

        if (sender is Button button)
        {
            await RunHistorySyncAsync(profile, button);
        }
    }

    private async Task RunHistorySyncAsync(CommissioningProfile profile, Button button)
    {
        var history = _services.GetRequiredService<HistoryRepository>();
        var ingestion = _services.GetRequiredService<HistoryIngestionService>();

        _syncCancellation = new CancellationTokenSource();
        HistorySyncResult? syncResult = null;
        var syncFailed = false;
        var postProcessWarning = false;
        string? failureDetail = null;

        button.IsEnabled = false;
        button.Content = _localization.CurrentLanguage == "es"
            ? "Actualizando..."
            : "Updating...";
        SetGlobalOperation(
            true,
            _localization.CurrentLanguage == "es"
                ? "Actualizando datos..."
                : "Updating data...");

        HistorySyncProgressLabel.Visibility = Visibility.Visible;
        HistorySyncProgressBar.Visibility = Visibility.Visible;
        HistorySyncProgressText.Visibility = Visibility.Visible;
        StopHistorySyncButton.Visibility = Visibility.Visible;
        StopHistorySyncButton.IsEnabled = true;
        HistorySyncProgressBar.Minimum = 0;
        HistorySyncProgressBar.Maximum = 1;
        HistorySyncProgressBar.Value = 0;
        HistorySyncProgressBar.IsIndeterminate = true;
        HistorySyncProgressLabel.FontWeight = FontWeights.SemiBold;
        HistorySyncProgressLabel.Foreground = Brushes.DodgerBlue;
        HistorySyncProgressLabel.Text = _localization.CurrentLanguage == "es"
            ? "Preparando actualización..."
            : "Preparing update...";
        HistorySyncProgressText.Text = _localization.CurrentLanguage == "es"
            ? "1/3 · Leyendo el estado actual del inversor."
            : "1/3 · Reading current inverter state.";

        LastUpdatedText.Text = _localization.CurrentLanguage == "es"
            ? "Actualización en curso..."
            : "Update in progress...";

        var progress = new Progress<HistorySyncProgress>(p =>
        {
            HistorySyncProgressBar.IsIndeterminate = false;
            HistorySyncProgressBar.Maximum = Math.Max(1, p.TotalDays);
            HistorySyncProgressBar.Value = Math.Min(p.CompletedDays, p.TotalDays);
            HistorySyncProgressLabel.Text = _localization.CurrentLanguage == "es"
                ? "2/3 · Descargando historial"
                : "2/3 · Downloading history";
            HistorySyncProgressLabel.Foreground = Brushes.DodgerBlue;

            var dateText = p.LocalDate.HasValue
                ? p.LocalDate.Value.ToString("yyyy-MM-dd")
                : "—";
            HistorySyncProgressText.Text =
                $"{p.CompletedDays}/{p.TotalDays} días · fecha {dateText}\n" +
                $"{p.Frames} frames · {p.Samples} muestras\n" +
                p.Message;
        });

        try
        {
            try
            {
                var snapshot =
                    _services.GetRequiredService<CurrentStateSnapshotService>();
                if (await snapshot.RefreshAsync(
                        profile,
                        _syncCancellation.Token))
                {
                    EvaluateInstallationHealth(profile);
                }
            }
            catch (Exception snapshotError)
            {
                HistorySyncProgressText.Text +=
                    _localization.CurrentLanguage == "es"
                        ? $"\nAviso: no se pudo refrescar el contexto actual ({snapshotError.Message})."
                        : $"\nNotice: current context could not be refreshed ({snapshotError.Message}).";
            }

            HistorySyncProgressLabel.Text = _localization.CurrentLanguage == "es"
                ? "2/3 · Descargando historial"
                : "2/3 · Downloading history";
            HistorySyncProgressText.Text += _localization.CurrentLanguage == "es"
                ? "\nPreparando rango histórico..."
                : "\nPreparing historical range...";

            syncResult = await ingestion.SyncAsync(
                profile,
                history.GetSampleCount(profile.DeviceId) == 0,
                progress,
                _syncCancellation.Token,
                GetRequestedCaptureStartDate());
        }
        catch (Exception ex)
        {
            syncFailed = true;
            failureDetail = ex.Message;
            LastUpdatedText.Text = _localization.CurrentLanguage == "es"
                ? "Actualización detenida por seguridad/error"
                : "Update stopped for safety/error";
            MessageBox.Show(ex.Message, _localization.GetString("UpdateDialog.Title"));
        }
        finally
        {
            if (!syncFailed &&
                syncResult is not null &&
                !syncResult.Cancelled)
            {
                HistorySyncProgressBar.IsIndeterminate = true;
                HistorySyncProgressLabel.Text = _localization.CurrentLanguage == "es"
                    ? "3/3 · Procesando datos descargados"
                    : "3/3 · Processing downloaded data";
                HistorySyncProgressLabel.Foreground = Brushes.DodgerBlue;
                HistorySyncProgressText.Text =
                    _localization.CurrentLanguage == "es"
                        ? "La descarga terminó. Normalizando datos y reconstruyendo el contexto del hogar..."
                        : "Download finished. Normalizing data and rebuilding household context...";
            }

            try
            {
                if (history.GetSampleCount(profile.DeviceId) > 0)
                {
                    var normalizer = _services.GetRequiredService<NormalizationService>();
                    await normalizer.RebuildAsync(profile);

                    var behavior =
                        _services.GetRequiredService<HouseholdBehaviorService>();
                    await behavior.RebuildAsync(profile.DeviceId);

                    EvaluateInstallationHealth(profile);
                    // Hidden pages refresh on ShowPage, without redundant queries
                    // or chart work immediately after a data rebuild.
                    if (_dashboardVisible) RefreshDashboardMetrics();
                    if (BatteryContent.Visibility == Visibility.Visible) RefreshBatteryView();
                    if (AnalysisContent.Visibility == Visibility.Visible) RefreshAnalysisView();
                }
            }
            catch (Exception normalizationError)
            {
                postProcessWarning = true;
                failureDetail = normalizationError.Message;
            }

            HistorySyncProgressBar.IsIndeterminate = false;

            var coverage = history.GetCoverageSummary(profile.DeviceId);
            var latestSaved = coverage.LastSampleAtUtc.HasValue
                ? coverage.LastSampleAtUtc.Value.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss")
                : "—";

            if (syncFailed)
            {
                HistorySyncProgressLabel.Text = _localization.CurrentLanguage == "es"
                    ? "Actualización detenida"
                    : "Update stopped";
                HistorySyncProgressLabel.Foreground = Brushes.Firebrick;
                HistorySyncProgressText.Text =
                    (_localization.CurrentLanguage == "es"
                        ? "La actualización no terminó correctamente."
                        : "The update did not finish correctly.") +
                    (string.IsNullOrWhiteSpace(failureDetail)
                        ? string.Empty
                        : $"\n{failureDetail}");
            }
            else if (syncResult?.Cancelled == true)
            {
                HistorySyncProgressLabel.Text = _localization.CurrentLanguage == "es"
                    ? "Actualización detenida de forma segura"
                    : "Update stopped safely";
                HistorySyncProgressLabel.Foreground = Brushes.DarkOrange;
                HistorySyncProgressText.Text =
                    (_localization.CurrentLanguage == "es"
                        ? "Los datos descargados antes de detener se conservaron."
                        : "Data downloaded before stopping was kept.") +
                    $"\n{syncResult.DaysCompleted}/{syncResult.DaysAttempted} días · {syncResult.Frames} frames · {syncResult.SamplesUpserted} muestras" +
                    $"\nÚltimo dato guardado: {latestSaved}";
                LastUpdatedText.Text = _localization.CurrentLanguage == "es"
                    ? $"Detenida · último dato guardado {latestSaved}"
                    : $"Stopped · latest saved data {latestSaved}";
            }
            else if (syncResult is not null)
            {
                HistorySyncProgressBar.Maximum = Math.Max(1, syncResult.DaysAttempted);
                HistorySyncProgressBar.Value = Math.Max(1, syncResult.DaysAttempted);

                HistorySyncProgressLabel.Text = postProcessWarning
                    ? (_localization.CurrentLanguage == "es"
                        ? "Actualización completada con una observación"
                        : "Update completed with a notice")
                    : (_localization.CurrentLanguage == "es"
                        ? "Actualización completada ✓"
                        : "Update completed ✓");
                HistorySyncProgressLabel.Foreground =
                    postProcessWarning ? Brushes.DarkOrange : Brushes.ForestGreen;

                HistorySyncProgressText.Text =
                    $"{syncResult.DaysCompleted}/{syncResult.DaysAttempted} días · " +
                    $"{syncResult.Frames} frames · {syncResult.SamplesUpserted} muestras" +
                    $"\nÚltimo dato guardado: {latestSaved}" +
                    (postProcessWarning && !string.IsNullOrWhiteSpace(failureDetail)
                        ? $"\nAviso de procesamiento: {failureDetail}"
                        : string.Empty);

                LastUpdatedText.Text = _localization.CurrentLanguage == "es"
                    ? $"Completada · último dato guardado {latestSaved}"
                    : $"Completed · latest saved data {latestSaved}";
            }

            StopHistorySyncButton.IsEnabled = false;
            StopHistorySyncButton.Visibility = Visibility.Collapsed;
            button.IsEnabled = true;
            button.SetResourceReference(ContentControl.ContentProperty, "Action.UpdateData");

            _syncCancellation.Dispose();
            _syncCancellation = null;
            SetGlobalOperation(false, string.Empty);
            RefreshCaptureStartOptions();
            RefreshConnectionStatus();
            if (DataContent.Visibility == Visibility.Visible) RefreshDataCoverageView();
            if (AnalysisContent.Visibility == Visibility.Visible) RefreshAnalysisView();
            if (ReportsContent.Visibility == Visibility.Visible) RefreshReportsView();
        }
    }

    private void StopHistorySync_Click(object sender, RoutedEventArgs e)
    {
        if (_syncCancellation is null)
        {
            return;
        }

        StopHistorySyncButton.IsEnabled = false;
        HistorySyncProgressText.Text = _localization.CurrentLanguage == "es"
            ? "Deteniendo de forma segura... Los datos ya guardados se conservarán."
            : "Stopping safely... Already saved data will be kept.";

        _syncCancellation.Cancel();
    }

    private async void RefreshCurrentState_Click(object sender, RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null || !_session.HasSession)
        {
            OpenCommissioningWindow();
            return;
        }

        if (sender is Button button)
        {
            button.IsEnabled = false;
            SetGlobalOperation(
                true,
                _localization.CurrentLanguage == "es"
                    ? "Actualizando estado actual..."
                    : "Refreshing current state...");
            try
            {
                await RefreshCurrentStateAsync(profile, showError: true);
            }
            finally
            {
                button.IsEnabled = true;
                SetGlobalOperation(false, string.Empty);
            }
        }
    }

    private async Task<bool> RefreshCurrentStateAsync(
        CommissioningProfile profile,
        bool showError)
    {
        if (_currentStateRefreshInProgress)
        {
            return false;
        }

        _currentStateRefreshInProgress = true;

        try
        {
            var snapshot =
                _services.GetRequiredService<CurrentStateSnapshotService>();
            var refreshed =
                await snapshot.RefreshAsync(profile);

            if (refreshed)
            {
                EvaluateInstallationHealth(profile);
                // A hidden page is reloaded when visited by ShowPage.
                if (_dashboardVisible) RefreshDashboardMetrics();
                if (BatteryContent.Visibility == Visibility.Visible) RefreshBatteryView();
                if (DataContent.Visibility == Visibility.Visible) RefreshDataCoverageView();
            }

            RefreshConnectionStatus();
            return refreshed;
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus();
            if (showError)
            {
                MessageBox.Show(
                    ex.Message,
                    _localization.GetString("UpdateDialog.Title"));
            }

            return false;
        }
        finally
        {
            _currentStateRefreshInProgress = false;
            if (_dashboardVisible)
            {
                ResetDashboardLiveCycle();
            }
        }
    }

    private void ResetDashboardLiveCycle()
    {
        _dashboardLiveCycleStartedUtc = DateTimeOffset.UtcNow;

        _dashboardLiveTimer.Stop();
        _dashboardLiveTimer.Start();

        _dashboardProgressTimer.Stop();
        _dashboardProgressTimer.Start();

        UpdateDashboardLiveCountdown();
    }

    private void DashboardProgressTimer_Tick(
        object? sender,
        EventArgs e) =>
        UpdateDashboardLiveCountdown();

    private void UpdateDashboardLiveCountdown()
    {
        var visible =
            _dashboardVisible &&
            _session.HasSession;

        SetDashboardLiveCountdownVisibility(visible);

        if (!visible)
        {
            return;
        }

        var elapsed =
            (DateTimeOffset.UtcNow - _dashboardLiveCycleStartedUtc)
            .TotalSeconds;
        var value = Math.Clamp(elapsed / DashboardLivePollSeconds * 100.0, 0, 100);

        PvLiveCountdown.Value = value;
        HouseLiveCountdown.Value = value;
        BatteryLiveCountdown.Value = value;
        GridLiveCountdown.Value = value;
    }

    private void SetDashboardLiveCountdownVisibility(bool visible)
    {
        var value = visible
            ? Visibility.Visible
            : Visibility.Collapsed;

        PvLiveCountdown.Visibility = value;
        HouseLiveCountdown.Visibility = value;
        BatteryLiveCountdown.Visibility = value;
        GridLiveCountdown.Visibility = value;
    }

    private async void DashboardLiveTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (!_dashboardVisible ||
            !_session.HasSession)
        {
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
        {
            return;
        }

        await RefreshCurrentStateAsync(
            profile,
            showError: false);
    }

    private void MainWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _windowClosed = true;
        _reportExportCancellation?.Cancel();
        _reportPreviewCancellation?.Cancel();
        _dashboardRefreshGeneration++;
        _batteryRefreshGeneration++;
        _analysisRefreshGeneration++;
        _analysisPresetGeneration++;
        _analysisPresetLoading = false;
        _analysisRenderedAggregation = null;
        _analysisRenderedThresholds = null;
        _dashboardLiveTimer.Stop();
        _dashboardLiveTimer.Tick -= DashboardLiveTimer_Tick;
        _dashboardProgressTimer.Stop();
        _dashboardProgressTimer.Tick -= DashboardProgressTimer_Tick;
        _dispatcherLatenessTimer.Stop();
        _dispatcherLatenessTimer.Tick -= DispatcherLatenessTimer_Tick;
        Activated -= MainWindow_Activated;
        Deactivated -= MainWindow_Deactivated;
    }

    private CancellationTokenSource? _sqlRunning;
    private bool _sqlExplorerBusy;
    private const string SqlExecuteShortcutKey = "sql.explorer.shortcut.execute";
    private const string SqlCsvShortcutKey = "sql.explorer.shortcut.csv";
    private const string SqlExcelShortcutKey = "sql.explorer.shortcut.xlsx";
    private string _sqlExecuteShortcut = "Ctrl+Enter";
    private string _sqlCsvShortcut = "Ctrl+Shift+C";
    private string _sqlExcelShortcut = "Ctrl+Shift+E";
    private bool _loadingSqlShortcutSelectors;

    private void LoadSqlExplorerShortcuts()
    {
        _loadingSqlShortcutSelectors = true;
        try
        {
            var repo = _services.GetRequiredService<AppSettingsRepository>();
            _sqlExecuteShortcut = SelectAllowedGesture(SqlExecuteShortcutSelector,
                repo.Get(SqlExecuteShortcutKey), "Ctrl+Enter");
            _sqlCsvShortcut = SelectAllowedGesture(SqlCsvShortcutSelector,
                repo.Get(SqlCsvShortcutKey), "Ctrl+Shift+C");
            _sqlExcelShortcut = SelectAllowedGesture(SqlExcelShortcutSelector,
                repo.Get(SqlExcelShortcutKey), "Ctrl+Shift+E");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!seen.Add(_sqlExecuteShortcut) || !seen.Add(_sqlCsvShortcut) ||
                !seen.Add(_sqlExcelShortcut))
            {
                // Invalid persisted configuration must fail closed to distinct defaults.
                _sqlExecuteShortcut = "Ctrl+Enter";
                _sqlCsvShortcut = "Ctrl+Shift+C";
                _sqlExcelShortcut = "Ctrl+Shift+E";
                SqlExecuteShortcutSelector.SelectedValue = _sqlExecuteShortcut;
                SqlCsvShortcutSelector.SelectedValue = _sqlCsvShortcut;
                SqlExcelShortcutSelector.SelectedValue = _sqlExcelShortcut;
            }
        }
        finally { _loadingSqlShortcutSelectors = false; }
    }

    private static string SelectAllowedGesture(ComboBox selector,
        string? configured, string fallback)
    {
        var valid = selector.Items.OfType<ComboBoxItem>().Any(i =>
            string.Equals(i.Tag?.ToString(), configured, StringComparison.OrdinalIgnoreCase));
        var result = valid ? configured! : fallback;
        selector.SelectedValue = result;
        return result;
    }

    private void SqlExplorerShortcutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSqlShortcutSelectors || !IsLoaded ||
            sender is not ComboBox selector || selector.SelectedValue is not string chosen)
            return;

        string key, previous;
        if (ReferenceEquals(selector, SqlExecuteShortcutSelector))
        { key = SqlExecuteShortcutKey; previous = _sqlExecuteShortcut; }
        else if (ReferenceEquals(selector, SqlCsvShortcutSelector))
        { key = SqlCsvShortcutKey; previous = _sqlCsvShortcut; }
        else if (ReferenceEquals(selector, SqlExcelShortcutSelector))
        { key = SqlExcelShortcutKey; previous = _sqlExcelShortcut; }
        else return;

        var conflicts = new[]
        {
            ReferenceEquals(selector, SqlExecuteShortcutSelector) ? null : _sqlExecuteShortcut,
            ReferenceEquals(selector, SqlCsvShortcutSelector) ? null : _sqlCsvShortcut,
            ReferenceEquals(selector, SqlExcelShortcutSelector) ? null : _sqlExcelShortcut
        }.Any(assigned => string.Equals(assigned, chosen, StringComparison.OrdinalIgnoreCase));
        if (conflicts)
        {
            _loadingSqlShortcutSelectors = true;
            try { selector.SelectedValue = previous; }
            finally { _loadingSqlShortcutSelectors = false; }
            MessageBox.Show(
                SqlExplorerSpanish
                    ? "El atajo ya está asignado a otra acción SQL."
                    : "This shortcut is already assigned to another SQL action.",
                SqlExplorerSpanish ? "Conflicto de atajos" : "Shortcut conflict",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _services.GetRequiredService<AppSettingsRepository>().Set(key, chosen);
        if (key == SqlExecuteShortcutKey) _sqlExecuteShortcut = chosen;
        else if (key == SqlCsvShortcutKey) _sqlCsvShortcut = chosen;
        else _sqlExcelShortcut = chosen;
    }

    private static string? SqlGestureFromKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var normalized = key switch
        {
            Key.Enter => "Enter",
            Key.R => "R",
            Key.E => "E",
            Key.C => "C",
            _ => null
        };
        if (normalized is null) return null;
        var modifiers = Keyboard.Modifiers;
        if (!modifiers.HasFlag(ModifierKeys.Control)) return null;
        return "Ctrl+" +
            (modifiers.HasFlag(ModifierKeys.Shift) ? "Shift+" : "") +
            (modifiers.HasFlag(ModifierKeys.Alt) ? "Alt+" : "") +
            normalized;
    }

    private SafeSqlExplorerService CreateSqlExplorer() =>
        new(_paths.DatabasePath);

    private void SetSqlExplorerBusy(bool busy)
    {
        _sqlExplorerBusy = busy;
        SqlExplorerBusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SqlExecuteButton.IsEnabled = !busy;
        SqlExportCsvButton.IsEnabled = !busy;
        SqlExportXlsxButton.IsEnabled = !busy;
        SqlSchemaRefreshButton.IsEnabled = !busy;
        SqlCancelButton.IsEnabled = busy;
    }

    private bool SqlExplorerSpanish =>
        _localization.CurrentLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshSqlSchemaAsync()
    {
        if (_sqlExplorerBusy) return;
        SetSqlExplorerBusy(true);
        using var cancellation = new CancellationTokenSource();
        _sqlRunning = cancellation;
        try
        {
            var result = await CreateSqlExplorer().SchemaAsync(cancellation.Token);
            SqlSchemaList.ItemsSource = result.Rows.Select(row =>
                new SqlSchemaOption(row[0].Text ?? "", row[1].Text ?? "")).ToArray();
            SqlExplorerStatusText.Text = (SqlExplorerSpanish
                ? "Objetos SQL disponibles: " : "Available SQL objects: ") +
                result.Rows.Count;
        }
        catch (Exception ex)
        {
            SqlExplorerStatusText.Text = (SqlExplorerSpanish
                ? "No se pudo leer el esquema: " : "Unable to read schema: ") + ex.Message;
        }
        finally
        {
            _sqlRunning = null;
            SetSqlExplorerBusy(false);
        }
    }

    private void SqlExplorerFindNext_Click(object sender, RoutedEventArgs e) =>
        FindNextSqlText();

    private void SqlExplorerFindKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.F3)
        {
            e.Handled = true;
            FindNextSqlText();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SqlStatementEditor.Focus();
        }
    }

    private void FindNextSqlText()
    {
        var text = SqlFindTextBox.Text;
        var statement = SqlStatementEditor.Text;
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(statement))
        {
            SqlExplorerDiagnosticText.Text = SqlExplorerSpanish
                ? "Escribe un texto para buscar." : "Enter search text.";
            return;
        }
        var from = Math.Clamp(SqlStatementEditor.SelectionStart +
            SqlStatementEditor.SelectionLength, 0, statement.Length);
        var found = statement.IndexOf(text, from, StringComparison.OrdinalIgnoreCase);
        var wrapped = false;
        if (found < 0)
        {
            found = statement.IndexOf(text, 0, StringComparison.OrdinalIgnoreCase);
            wrapped = found >= 0;
        }
        if (found < 0)
        {
            SqlExplorerDiagnosticText.Text = SqlExplorerSpanish
                ? "Texto no encontrado en la consulta."
                : "Text not found in SQL statement.";
            return;
        }
        SqlStatementEditor.Select(found, text.Length);
        SqlStatementEditor.ScrollToLine(SqlStatementEditor.Document.GetLineByOffset(found).LineNumber);
        SqlStatementEditor.Focus();
        SqlExplorerDiagnosticText.Text = wrapped
            ? (SqlExplorerSpanish ? "Búsqueda reiniciada desde el principio."
                                  : "Search wrapped to the start.")
            : "";
    }

    private void ShowSqlExplorerError(Exception ex, string statement)
    {
        var diagnostic = SqlErrorDiagnostics.Describe(ex, statement);
        var position = diagnostic.ApproximateOffset;
        var prefix = SqlExplorerSpanish ? "Diagnóstico SQL" : "SQL diagnostic";
        var codes = diagnostic.SqliteCode is int sqliteCode
            ? $" [SQLite {sqliteCode}, extended {diagnostic.SqliteExtendedCode}]"
            : "";
        var location = "";
        if (position.HasValue && diagnostic.ApproximateLine.HasValue &&
            diagnostic.ApproximateColumn.HasValue &&
            position.Value >= 0 && position.Value < SqlStatementEditor.Text.Length)
        {
            location = SqlExplorerSpanish
                ? $" Referencia aproximada: línea {diagnostic.ApproximateLine}, " +
                  $"columna {diagnostic.ApproximateColumn}; no es la ubicación exacta del error."
                : $" Approximate reference: line {diagnostic.ApproximateLine}, " +
                  $"column {diagnostic.ApproximateColumn}; not an exact parser error location.";
            SqlStatementEditor.Select(position.Value, 1);
            SqlStatementEditor.ScrollToLine(diagnostic.ApproximateLine.Value);
        }
        else location = SqlExplorerSpanish
            ? " SQLite no proporcionó una ubicación verificable."
            : " SQLite did not provide a reliable error location.";
        SqlExplorerDiagnosticText.Text =
            prefix + codes + ": " + diagnostic.Message + location;
    }

    private async void SqlExplorerRefreshSchema_Click(object sender, RoutedEventArgs e) =>
        await RefreshSqlSchemaAsync();

    private void SqlExplorerSchemaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SqlSchemaList.SelectedItem is not SqlSchemaOption selected) return;
        SqlStatementEditor.Text = "SELECT * FROM \"" +
            selected.Name.Replace("\"", "\"\"") + "\" LIMIT 200;";
        SqlStatementEditor.Focus();
        SqlStatementEditor.CaretOffset = SqlStatementEditor.Text.Length;
    }

    private async void SqlExplorerExecute_Click(object sender, RoutedEventArgs e) =>
        await ExecuteSqlExplorerAsync();

    private async Task ExecuteSqlExplorerAsync()
    {
        if (_sqlExplorerBusy) return;
        var sql = SqlStatementEditor.Text;
        SetSqlExplorerBusy(true);
        using var cancellation = new CancellationTokenSource();
        _sqlRunning = cancellation;
        SqlExplorerStatusText.Text = SqlExplorerSpanish
            ? "Ejecutando SELECT en modo de solo lectura..."
            : "Executing read-only SELECT...";
        SqlExplorerDiagnosticText.Text = "";
        SqlExplorerPerformanceText.Text = "";
        try
        {
            var result = await CreateSqlExplorer().PreviewAsync(sql,
                cancellationToken: cancellation.Token);
            var table = new DataTable();
            foreach (var (label, index) in result.Columns.Select((label, index) => (label, index)))
            {
                var name = label.Length == 0 ? "Columna " + (index + 1) : label;
                if (table.Columns.Contains(name))
                    name += " (" + (index + 1) + ")";
                table.Columns.Add(name, typeof(string));
            }
            foreach (var values in result.Rows)
                table.Rows.Add(values.Select(cell =>
                    cell.IsNull ? (object)DBNull.Value : cell.Text ?? "").ToArray());
            SqlExplorerResultGrid.ItemsSource = table.DefaultView;
            SqlExplorerResultTitle.Text = SqlExplorerSpanish
                ? $"Vista previa: {result.Rows.Count} filas" +
                  (result.HasMore ? " (hay más; exportar para obtener todas)" : "")
                : $"Preview: {result.Rows.Count} rows" +
                  (result.HasMore ? " (more rows available; export for all)" : "");
            SqlExplorerStatusText.Text = SqlExplorerSpanish
                ? "Consulta completada. Base de datos sin modificaciones."
                : "Query completed. Database unchanged.";
            SqlExplorerPerformanceText.Text =
                (SqlExplorerSpanish ? "SQLite: " : "SQLite: ") +
                result.Elapsed.TotalMilliseconds.ToString("N0") + " ms · " +
                result.Rows.Count.ToString("N0") +
                (SqlExplorerSpanish ? " filas en vista previa" : " preview rows") +
                (result.HasMore ? (SqlExplorerSpanish ? " (hay más)" : " (more available)") : "");
        }
        catch (OperationCanceledException)
        {
            SqlExplorerStatusText.Text = SqlExplorerSpanish
                ? "Consulta cancelada." : "Query cancelled.";
        }
        catch (Exception ex)
        {
            SqlExplorerStatusText.Text = cancellation.IsCancellationRequested
                ? (SqlExplorerSpanish ? "Consulta cancelada." : "Query cancelled.")
                : (SqlExplorerSpanish ? "Error al ejecutar SQL." : "SQL execution error.");
            ShowSqlExplorerError(ex, sql);
        }
        finally
        {
            _sqlRunning = null;
            SetSqlExplorerBusy(false);
        }
    }

    private void SqlExplorerCancel_Click(object sender, RoutedEventArgs e) =>
        _sqlRunning?.Cancel();

    private async void SqlExplorerExportCsv_Click(object sender, RoutedEventArgs e) =>
        await ExportSqlExplorerAsync(SqlExportFormat.Csv);

    private async void SqlExplorerExportXlsx_Click(object sender, RoutedEventArgs e) =>
        await ExportSqlExplorerAsync(SqlExportFormat.Xlsx);

    private async Task ExportSqlExplorerAsync(SqlExportFormat format)
    {
        if (_sqlExplorerBusy) return;
        var excel = format == SqlExportFormat.Xlsx;
        var dialog = new SaveFileDialog
        {
            Filter = excel ? "Excel Workbook (*.xlsx)|*.xlsx" : "CSV UTF-8 (*.csv)|*.csv",
            DefaultExt = excel ? ".xlsx" : ".csv",
            AddExtension = true,
            FileName = excel ? "consulta-sql.xlsx" : "consulta-sql.csv",
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true) return;
        SetSqlExplorerBusy(true);
        using var cancellation = new CancellationTokenSource();
        _sqlRunning = cancellation;
        SqlExplorerStatusText.Text = SqlExplorerSpanish
            ? "Exportando consulta completa... No se publicarán archivos parciales."
            : "Exporting full query... Partial files will not be published.";
        SqlExplorerDiagnosticText.Text = "";
        SqlExplorerPerformanceText.Text = "";
        try
        {
            var result = await CreateSqlExplorer().ExportAsync(
                SqlStatementEditor.Text, dialog.FileName, format, cancellation.Token);
            SqlExplorerStatusText.Text = (SqlExplorerSpanish
                ? "Exportación completa, filas: " : "Full export, rows: ") +
                result.Rows + " — " + result.Path;
            SqlExplorerPerformanceText.Text =
                (SqlExplorerSpanish ? "Duración: " : "Duration: ") +
                result.Elapsed.TotalSeconds.ToString("N1") + " s · " +
                result.Rows.ToString("N0") +
                (SqlExplorerSpanish ? " filas · " : " rows · ") +
                result.FileSizeBytes.ToString("N0") +
                (SqlExplorerSpanish ? " bytes escritos" : " bytes written");
        }
        catch (Exception ex)
        {
            SqlExplorerStatusText.Text = SqlExplorerSpanish
                ? "No se completó la exportación." : "Export did not complete.";
            ShowSqlExplorerError(ex, SqlStatementEditor.Text);
        }
        finally
        {
            _sqlRunning = null;
            SetSqlExplorerBusy(false);
        }
    }

    private void SqlExplorerEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SqlFindTextBox.Focus();
            SqlFindTextBox.SelectAll();
            return;
        }
        if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            FindNextSqlText();
            return;
        }
        if (e.Key == Key.Escape && _sqlRunning is not null)
        {
            e.Handled = true;
            _sqlRunning.Cancel();
            return;
        }

        var gesture = SqlGestureFromKey(e);
        var runAliasF5 = e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None;
        if (runAliasF5 ||
            string.Equals(gesture, _sqlExecuteShortcut, StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            _ = ExecuteSqlExplorerAsync();
        }
        else if (string.Equals(gesture, _sqlCsvShortcut, StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            _ = ExportSqlExplorerAsync(SqlExportFormat.Csv);
        }
        else if (string.Equals(gesture, _sqlExcelShortcut, StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            _ = ExportSqlExplorerAsync(SqlExportFormat.Xlsx);
        }
    }

    private void SqlExplorerEditorTextChanged(object? sender, EventArgs e) =>
        UpdateSqlExplorerCaretStatus();

    private void UpdateSqlExplorerCaretStatus()
    {
        if (SqlStatementEditor is null || SqlExplorerStatusText is null ||
            _sqlExplorerBusy) return;
        var cursor = SqlStatementEditor.TextArea.Caret;
        SqlExplorerStatusText.Text =
            $"Ln {cursor.Line}, Col {cursor.Column} — SELECT only";
    }

    private sealed record SqlSchemaOption(string Kind, string Name)
    {
        public override string ToString() => Kind.ToUpperInvariant() + " · " + Name;
    }

    private bool _backupInventoryBusy;
    private VerifiedBackupInspection? _verifiedBackupInspection;

    private void BackupInventorySelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshBackupSelectionDetails();

    private void RefreshBackupSelectionDetails()
    {
        if (BackupInventoryGrid is null || BackupSelectionDetailsText is null ||
            BackupVerifiedContentsText is null)
            return;

        var selected = BackupInventoryGrid.SelectedItem as PhysicalBackupCopy;
        if (selected is null)
        {
            BackupSelectionDetailsText.Text = _localization.GetString("Backup.SelectionNone");
            BackupVerifiedContentsText.Text = "";
        }
        else
        {
            BackupSelectionDetailsText.Text =
                selected.Name + Environment.NewLine +
                selected.Location + " · " + selected.Kind + " · " +
                selected.SizeBytes.ToString("N0") + " bytes · " +
                selected.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
                Environment.NewLine + selected.Path;
            var verified = _verifiedBackupInspection;
            if (verified is not null &&
                selected.VerificationStatus == "PASS" &&
                string.Equals(selected.Path, verified.Summary.Path,
                    StringComparison.OrdinalIgnoreCase))
            {
                BackupVerifiedContentsText.Text =
                    _localization.GetString("Backup.VerifiedHeading") +
                    Environment.NewLine +
                    "SHA-256: " + verified.Summary.Sha256 + Environment.NewLine +
                    "UTC: " + verified.Summary.CreatedUtc.ToString("u") +
                    " · App: " + verified.Manifest.AppVersion +
                    " · Build: " + verified.Manifest.BuildNumber +
                    " · SQLite schema: " + verified.Summary.SchemaVersion +
                    Environment.NewLine +
                    "SQLite: " + verified.DatabaseFiles + " file(s), " +
                    verified.DatabaseBytes.ToString("N0") + " bytes" +
                    Environment.NewLine +
                    "Bills: " + verified.BillDocuments + " document(s), " +
                    verified.BillBytes.ToString("N0") + " bytes" +
                    Environment.NewLine +
                    "Tariffs: " + verified.TariffDocuments + " document(s), " +
                    verified.TariffBytes.ToString("N0") + " bytes";
            }
            else
            {
                BackupVerifiedContentsText.Text = selected.Kind == "COMPLETE"
                    ? selected.VerificationStatus == "FAIL"
                        ? _localization.GetString("Backup.VerificationFailed")
                        : _localization.GetString("Backup.SelectionUnverified")
                    : _localization.GetString("Backup.SelectionLegacy");
            }
        }
        BackupVerifyButton.IsEnabled = !_backupInventoryBusy && selected?.Kind == "COMPLETE";
        BackupDeleteButton.IsEnabled = !_backupInventoryBusy && selected?.Kind == "COMPLETE";
        BackupOpenSelectedButton.IsEnabled = !_backupInventoryBusy && selected is not null;
        BackupSaveReceiptButton.IsEnabled = !_backupInventoryBusy &&
            selected?.Kind == "COMPLETE" && selected.VerificationStatus == "PASS" &&
            _verifiedBackupInspection is not null &&
            string.Equals(selected.Path, _verifiedBackupInspection.Summary.Path,
                StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateBackupRowStatus(PhysicalBackupCopy selected, string status)
    {
        var rows = (BackupInventoryGrid.ItemsSource as IEnumerable<PhysicalBackupCopy>)?.ToList()
            ?? new List<PhysicalBackupCopy>();
        var index = rows.FindIndex(r => string.Equals(r.Path, selected.Path,
            StringComparison.OrdinalIgnoreCase) && r.Location == selected.Location);
        if (index < 0) return;
        rows[index] = selected with { VerificationStatus = status };
        BackupInventoryGrid.ItemsSource = rows;
        BackupInventoryGrid.SelectedItem = rows[index];
        RefreshBackupSelectionDetails();
    }

    private string? ConfiguredSecondaryBackupFolder() =>
        _services.GetRequiredService<AppSettingsRepository>().Get(BackupSecondaryPathKey);

    private void SetBackupInventoryBusy(bool busy)
    {
        _backupInventoryBusy = busy;
        BackupInventoryProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        BackupRefreshButton.IsEnabled = !busy;
        BackupInventoryGrid.IsEnabled = !busy;
        RefreshBackupSelectionDetails();
    }

    private async Task RefreshBackupInventoryAsync()
    {
        if (_backupInventoryBusy)
            return;
        SetBackupInventoryBusy(true);
        try
        {
            var service = _services.GetRequiredService<CompleteBackupInventoryService>();
            var previousSelection = (BackupInventoryGrid.SelectedItem as PhysicalBackupCopy);
            var snapshot = await Task.Run(() => service.List(ConfiguredSecondaryBackupFolder()));
            // A refreshed inventory invalidates any previous PASS: the files
            // must be explicitly verified again, even if their names match.
            _verifiedBackupInspection = null;
            BackupInventoryGrid.ItemsSource = snapshot.Copies;
            if (previousSelection is not null)
                BackupInventoryGrid.SelectedItem = snapshot.Copies.FirstOrDefault(c =>
                    c.Location == previousSelection.Location &&
                    string.Equals(c.Path, previousSelection.Path, StringComparison.OrdinalIgnoreCase));
            RefreshBackupSelectionDetails();
            var spanish = _localization.CurrentLanguage.StartsWith(
                "es", StringComparison.OrdinalIgnoreCase);
            BackupInventoryStatusText.Text = (spanish
                    ? "Copias reconocidas: " : "Recognized copies: ") +
                snapshot.Copies.Count +
                (string.IsNullOrEmpty(snapshot.SecondaryWarning)
                    ? string.Empty
                    : (spanish ? ". AVISO: " : ". WARNING: ") + snapshot.SecondaryWarning);
        }
        catch (Exception ex)
        {
            BackupInventoryStatusText.Text = "Error: " + ex.Message;
        }
        finally
        {
            SetBackupInventoryBusy(false);
        }
    }

    private async void BackupPageRefresh_Click(object sender, RoutedEventArgs e) =>
        await RefreshBackupInventoryAsync();

    private async void BackupPageCreate_Click(object sender, RoutedEventArgs e)
    {
        await CreateCompleteBackupAsync();
        await RefreshBackupInventoryAsync();
    }

    private void BackupPageSettings_Click(object sender, RoutedEventArgs e) =>
        ShowPage("Settings");

    private void BackupPageOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _paths.BackupDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            BackupInventoryStatusText.Text = ex.Message;
        }
    }

    private void BackupPageOpenSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_backupInventoryBusy || BackupInventoryGrid.SelectedItem is not PhysicalBackupCopy chosen)
            return;
        try
        {
            if (!File.Exists(chosen.Path))
                throw new FileNotFoundException("The selected backup is no longer available.");
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetDirectoryName(chosen.Path)!,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            BackupInventoryStatusText.Text = "Error: " + ex.Message;
        }
    }

    private async void BackupPageSaveReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (_backupInventoryBusy || BackupInventoryGrid.SelectedItem is not PhysicalBackupCopy chosen ||
            chosen.Kind != "COMPLETE" || chosen.VerificationStatus != "PASS" ||
            _verifiedBackupInspection is null ||
            !string.Equals(_verifiedBackupInspection.Summary.Path, chosen.Path,
                StringComparison.OrdinalIgnoreCase))
            return;
        var dialog = new SaveFileDialog
        {
            Title = _localization.GetString("Backup.ReceiptTitle"),
            FileName = Path.GetFileNameWithoutExtension(chosen.Name) + "-verification.txt",
            Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt"
        };
        PrepareExportDialog(dialog);
        if (dialog.ShowDialog(this) != true) return;
        SetBackupInventoryBusy(true);
        var archiveReverified = false;
        try
        {
            // Reverify independently BEFORE issuing a receipt, so an archive
            // modified since the prior PASS cannot generate stale evidence.
            var service = _services.GetRequiredService<CompleteBackupInventoryService>();
            var details = await Task.Run(() =>
                service.VerifyDetails(chosen, ConfiguredSecondaryBackupFolder()));
            archiveReverified = true;
            _verifiedBackupInspection = details;
            var spanish = _localization.CurrentLanguage.StartsWith("es",
                StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(dialog.FileName, details.FormatReceipt(spanish));
            BackupInventoryStatusText.Text =
                _localization.GetString("Backup.ReceiptSaved") + dialog.FileName;
        }
        catch (Exception ex)
        {
            // Failure to WRITE THE TEXT RECEIPT does not imply the ZIP failed
            // verification. Only a failed independent archive recheck clears
            // the previous PASS and marks the archive FAIL.
            if (!archiveReverified)
            {
                _verifiedBackupInspection = null;
                UpdateBackupRowStatus(chosen, "FAIL");
            }
            BackupInventoryStatusText.Text =
                _localization.GetString("Backup.ReceiptError") + ex.Message;
        }
        finally { SetBackupInventoryBusy(false); }
    }

    private async void BackupPageVerify_Click(object sender, RoutedEventArgs e)
    {
        if (_backupInventoryBusy || BackupInventoryGrid.SelectedItem is not PhysicalBackupCopy chosen)
            return;
        SetBackupInventoryBusy(true);
        try
        {
            var service = _services.GetRequiredService<CompleteBackupInventoryService>();
            var details = await Task.Run(() =>
                service.VerifyDetails(chosen, ConfiguredSecondaryBackupFolder()));
            _verifiedBackupInspection = details;
            UpdateBackupRowStatus(chosen, "PASS");
            BackupInventoryStatusText.Text = "PASS — " + details.Summary.Path +
                " · SHA-256 " + details.Summary.Sha256 +
                " · schema " + details.Summary.SchemaVersion +
                " · files " + details.Summary.FileCount;
        }
        catch (Exception ex)
        {
            _verifiedBackupInspection = null;
            UpdateBackupRowStatus(chosen, "FAIL");
            BackupInventoryStatusText.Text =
                _localization.GetString("Backup.VerificationFailed") + " " + ex.Message;
        }
        finally
        {
            SetBackupInventoryBusy(false);
        }
    }

    private async void BackupPageDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_backupInventoryBusy || BackupInventoryGrid.SelectedItem is not PhysicalBackupCopy chosen)
            return;
        if (chosen.Kind != "COMPLETE")
        {
            BackupInventoryStatusText.Text =
                "Los respaldos antiguos SQLite no pueden eliminarse desde el administrador de copias completas.";
            return;
        }
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es", StringComparison.OrdinalIgnoreCase);
        var warning = spanish
            ? "Eliminar ÚNICAMENTE esta copia física?\n\n" + chosen.Path +
              "\n\nLas otras ubicaciones no se modificarán. Se verificará que exista " +
              "otra copia completa disponible antes de permitirlo."
            : "Delete ONLY this physical copy?\n\n" + chosen.Path +
              "\n\nNo other destination will be changed. Another available " +
              "complete backup must pass verification before deletion.";
        if (MessageBox.Show(warning, spanish ? "Confirmar eliminación" : "Confirm deletion",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        SetBackupInventoryBusy(true);
        try
        {
            var service = _services.GetRequiredService<CompleteBackupInventoryService>();
            await Task.Run(() => service.DeleteOne(chosen, ConfiguredSecondaryBackupFolder()));
            BackupInventoryStatusText.Text = (spanish
                ? "Se eliminó únicamente la copia seleccionada: "
                : "Only the selected copy was deleted: ") + chosen.Path;
        }
        catch (Exception ex)
        {
            BackupInventoryStatusText.Text = (spanish
                ? "No se eliminó ninguna copia: " : "No copy was deleted: ") + ex.Message;
        }
        finally
        {
            SetBackupInventoryBusy(false);
        }
        await RefreshBackupInventoryAsync();
    }

    private const string BackupSecondaryPathKey = "backup.complete-secondary-dir";
    private const string BackupReminderNextKey = "backup.complete-reminder-next-utc";
    private const string BackupLastSuccessKey = "backup.complete-last-success-utc";
    private bool _creatingCompleteBackup;

    /// <summary>
    /// Shows a skippable reminder, not a scheduled backup job.
    /// A first installation with no successful complete backup is prompted
    /// when its normal window is ready; postponement survives restarts.
    /// </summary>
    public async Task ShowWeeklyBackupReminderIfDueAsync()
    {
        try
        {
            var settings = _services.GetRequiredService<AppSettingsRepository>();
            var full = _services.GetRequiredService<FullBackupService>();
            var now = DateTimeOffset.UtcNow;

            var lastSuccessful = settings.Get(BackupLastSuccessKey);
            var lastCompleteUtc = DateTimeOffset.TryParse(lastSuccessful, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTimeOffset.MinValue;
            if (!full.ListLocal().Any())
                lastCompleteUtc = DateTimeOffset.MinValue;

            var nextReminder = settings.Get(BackupReminderNextKey);
            if (DateTimeOffset.TryParse(nextReminder, out var due) && now < due)
                return;
            if (lastCompleteUtc > now.AddDays(-7))
                return;

            var spanish = _localization.CurrentLanguage.StartsWith(
                "es", StringComparison.OrdinalIgnoreCase);
            var choice = MessageBox.Show(
                spanish
                    ? "No hay constancia de un respaldo completo reciente.\n\n" +
                      "Sí: crear ahora un respaldo completo verificado.\n" +
                      "No: posponer el recordatorio 24 horas.\n" +
                      "Cancelar: omitirlo durante esta semana.\n\n" +
                      "La aplicación no crea respaldos diarios automáticamente."
                    : "No recent successful complete backup is recorded.\n\n" +
                      "Yes: create a verified complete backup now.\n" +
                      "No: remind me again in 24 hours.\n" +
                      "Cancel: skip this week's reminder.\n\n" +
                      "No daily backups are created automatically.",
                spanish ? "Recordatorio semanal de respaldo" : "Weekly backup reminder",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (choice == MessageBoxResult.Yes)
            {
                await CreateCompleteBackupAsync();
            }
            else if (choice == MessageBoxResult.No)
            {
                settings.Set(BackupReminderNextKey, now.AddDays(1).ToString("O"));
            }
            else
            {
                settings.Set(BackupReminderNextKey, now.AddDays(7).ToString("O"));
            }
        }
        catch (Exception ex)
        {
            var spanish = _localization.CurrentLanguage.StartsWith(
                "es", StringComparison.OrdinalIgnoreCase);
            MessageBox.Show((spanish
                ? "No se pudo revisar el estado de respaldos: "
                : "Unable to inspect backup status: ") + ex.Message,
                spanish ? "Protección de datos" : "Data protection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SettingsBackupNow_Click(object sender, RoutedEventArgs e)
    {
        await CreateCompleteBackupAsync();
    }

    private async Task CreateCompleteBackupAsync()
    {
        if (_creatingCompleteBackup)
            return;
        _creatingCompleteBackup = true;
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es", StringComparison.OrdinalIgnoreCase);
        SettingsBackupNowButton.IsEnabled = false;
        SettingsBackupProgressBar.Visibility = Visibility.Visible;
        SettingsBackupProgressBar.IsIndeterminate = true;
        SettingsBackupStatusText.Text = spanish
            ? "Creando paquete completo: SQLite, boletas y tarifas. Puede tardar..."
            : "Creating complete package: SQLite, bills and tariffs. This may take a while...";
        try
        {
            var service = _services.GetRequiredService<FullBackupService>();
            var result = await Task.Run(() => service.Create(
                ProductInfo.ProductVersion, ProductInfo.BuildNumber,
                ProductInfo.SourceRevision));
            _services.GetRequiredService<AppSettingsRepository>()
                .Set(BackupLastSuccessKey, DateTimeOffset.UtcNow.ToString("O"));
            _services.GetRequiredService<AppSettingsRepository>()
                .Set(BackupReminderNextKey, DateTimeOffset.UtcNow.AddDays(7).ToString("O"));
            string secondaryStatus = "";
            var configuredSecondary = _services.GetRequiredService<AppSettingsRepository>()
                .Get(BackupSecondaryPathKey);
            if (!string.IsNullOrWhiteSpace(configuredSecondary))
            {
                try
                {
                    var second = await Task.Run(() => service.CopyVerifiedToSecondary(
                        result.Path, configuredSecondary));
                    secondaryStatus = spanish
                        ? " Copia secundaria verificada en " + second.Path
                        : " Verified secondary copy at " + second.Path;
                }
                catch (Exception copyError)
                {
                    secondaryStatus = spanish
                        ? " AVISO: respaldo local verificado, pero falló la segunda copia: " +
                          copyError.Message
                        : " WARNING: local backup verified, secondary copy failed: " +
                          copyError.Message;
                }
            }
            SettingsBackupStatusText.Text = (spanish
                ? $"Respaldo completo verificado ({result.FileCount} archivos). " +
                  $"Guardado en {result.Path}"
                : $"Verified complete backup ({result.FileCount} files). " +
                  $"Saved to {result.Path}") + secondaryStatus;
            RefreshBackupSecondaryPreference();
            MessageBox.Show(SettingsBackupStatusText.Text,
                spanish ? "Respaldo completo creado" : "Complete backup created",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SettingsBackupStatusText.Text = (spanish
                ? "No se pudo crear el respaldo completo: "
                : "Unable to create complete backup: ") + ex.Message;
            MessageBox.Show(SettingsBackupStatusText.Text,
                spanish ? "Protección de datos" : "Data protection",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SettingsBackupProgressBar.IsIndeterminate = false;
            SettingsBackupProgressBar.Value = 100;
            SettingsBackupNowButton.IsEnabled = true;
            _creatingCompleteBackup = false;
        }
    }

    private void RefreshBackupSecondaryPreference()
    {
        var settings = _services.GetRequiredService<AppSettingsRepository>();
        var destination = settings.Get(BackupSecondaryPathKey);
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es", StringComparison.OrdinalIgnoreCase);
        SettingsBackupSecondaryFolderText.Text = string.IsNullOrWhiteSpace(destination)
            ? (spanish ? "No configurada: solo copia local." : "Not configured: local copy only.")
            : destination + (Directory.Exists(destination) ? "" :
                (spanish ? " — destino no disponible" : " — destination unavailable"));

        var packages = _services.GetRequiredService<FullBackupService>().ListLocal();
        SettingsBackupInventoryText.Text = packages.Count == 0
            ? (spanish ? "No hay respaldos completos locales reconocidos." :
                "No recognized local complete backups.")
            : (spanish ? "Respaldos completos locales: " : "Local complete backup packages: ") +
              packages.Count + (spanish ? " (sin revalidación al listar). " :
                                        " (not reverified during listing). ") +
              (spanish ? "Último archivo: " : "Most recent: ") +
              Path.GetFileName(packages[0].Path);
    }

    private void SettingsBackupChooseSecondary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = _localization.CurrentLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "Elegir ubicación secundaria para respaldos"
                : "Choose secondary backup folder",
            Multiselect = false
        };
        var settings = _services.GetRequiredService<AppSettingsRepository>();
        var existing = settings.Get(BackupSecondaryPathKey);
        if (!string.IsNullOrWhiteSpace(existing) && Directory.Exists(existing))
            dialog.InitialDirectory = existing;
        if (dialog.ShowDialog(this) != true)
            return;

        string destination;
        try
        {
            // Apply the same restriction used by backup creation and inventory.
            destination = BackupDestinationPolicy.Validate(_paths, dialog.FolderName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                  IOException or UnauthorizedAccessException)
        {
            var spanish = _localization.CurrentLanguage.StartsWith(
                "es", StringComparison.OrdinalIgnoreCase);
            MessageBox.Show(
                (spanish ? "La carpeta secundaria no es segura: "
                         : "Unsafe secondary backup folder: ") + ex.Message,
                spanish ? "Protección de datos" : "Data protection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        settings.Set(BackupSecondaryPathKey, destination);
        RefreshBackupSecondaryPreference();
    }

    private const string DefaultExportFolderKey = "exports.default-folder";

    private void PrepareExportDialog(SaveFileDialog dialog)
    {
        var folder = _services.GetRequiredService<AppSettingsRepository>()
            .Get(DefaultExportFolderKey);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            dialog.InitialDirectory = folder;
    }

    private void RefreshExportFolderPreference()
    {
        if (!IsInitialized || SettingsExportFolderText is null)
            return;
        var folder = _services.GetRequiredService<AppSettingsRepository>()
            .Get(DefaultExportFolderKey);
        SettingsExportFolderText.Text =
            string.IsNullOrWhiteSpace(folder)
                ? _localization.GetString("Settings.ExportFolderSystemDefault")
                : folder + (Directory.Exists(folder) ? string.Empty :
                    " — " + _localization.GetString("Settings.ExportFolderMissing"));
    }

    private void SettingsChooseExportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = _localization.GetString("Settings.ChooseExportFolder"),
            Multiselect = false
        };
        var current = _services.GetRequiredService<AppSettingsRepository>()
            .Get(DefaultExportFolderKey);
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
            dialog.InitialDirectory = current;

        if (dialog.ShowDialog(this) != true)
            return;

        // This preference only changes where user-triggered Save dialogs
        // start; it does not move the SQLite database or existing exports.
        _services.GetRequiredService<AppSettingsRepository>()
            .Set(DefaultExportFolderKey, dialog.FolderName);
        RefreshExportFolderPreference();
    }

    private void SettingsResetExportFolder_Click(object sender, RoutedEventArgs e)
    {
        _services.GetRequiredService<AppSettingsRepository>()
            .Delete(DefaultExportFolderKey);
        RefreshExportFolderPreference();
    }

    private bool GetAutoConnectEnabled()
    {
        var value = _services.GetRequiredService<AppSettingsRepository>().Get("session.auto-connect-on-startup");
        return bool.TryParse(value, out var enabled) && enabled;
    }

    private void AutoConnectCheckBox_Click(object sender, RoutedEventArgs e)
    {
        _services.GetRequiredService<AppSettingsRepository>()
            .Set("session.auto-connect-on-startup", (AutoConnectCheckBox.IsChecked == true).ToString());
        RefreshSettingsSessionStatus();
    }

    private void SettingsConnect_Click(object sender, RoutedEventArgs e) => OpenCommissioningWindow();

    private void SettingsForgetSession_Click(object sender, RoutedEventArgs e)
    {
        _session.ResetLocalSession(forgetRememberedCredentials: true);
        RefreshConnectionStatus();
    }

    private void RefreshSettingsSessionStatus()
    {
        if (!IsInitialized || SettingsSessionStatusText is null) return;
        var resourceKey = _session.IsSessionVerified
            ? "Settings.SessionVerified"
            : _session.HasRememberedSession
                ? "Settings.SessionRemembered"
                : _session.HasRememberedCredentials
                    ? "Settings.CredentialsRemembered"
                    : "Settings.SessionNotRemembered";
        SettingsSessionStatusText.SetResourceReference(TextBlock.TextProperty, resourceKey);
        SettingsSessionStatusText.Foreground =
            _session.IsSessionVerified ? Brushes.Green :
            _session.HasRememberedSession || _session.HasRememberedCredentials ? Brushes.DarkOrange : Brushes.Gray;
    }

    private void ConnectSolar_Click(object sender, RoutedEventArgs e)
    {
        OpenCommissioningWindow();
    }

    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<DeveloperDiagnosticsWindow>();
        window.SetSelectedBillId(
            UtilityAuditBillSelector.SelectedValue is long selectedBill
                ? selectedBill : null);
        window.Owner = this;
        window.ShowDialog();
    }

    private void OpenCommissioningWindow()
    {
        var window = _services.GetRequiredService<CommissioningWindow>();
        window.Owner = this;
        window.ShowDialog();

        var refreshedProfile = _profiles.Get();
        if (refreshedProfile is not null)
        {
            try
            {
                EvaluateInstallationHealth(refreshedProfile);
            }
            catch
            {
                // Contextual configuration health is non-blocking.
            }
        }

        RefreshConnectionStatus();
        RefreshCaptureStartOptions();
        RefreshDataCoverageView();
        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshAnalysisView(initializeRange: true);
    }

    private void CaptureStartModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshCaptureStartOptions();
    }

    private DateOnly? GetRequestedCaptureStartDate()
    {
        if (!string.Equals(
                CaptureStartModeSelector.SelectedValue?.ToString(),
                "manual",
                StringComparison.Ordinal))
        {
            return null;
        }

        return ManualCaptureStartDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(ManualCaptureStartDatePicker.SelectedDate.Value)
            : null;
    }

    private void RefreshCaptureStartOptions()
    {
        if (!IsInitialized ||
            CaptureStartModeSelector is null ||
            ManualCaptureStartDatePicker is null ||
            CaptureStartHintText is null)
        {
            return;
        }

        var manual = string.Equals(
            CaptureStartModeSelector.SelectedValue?.ToString(),
            "manual",
            StringComparison.Ordinal);

        ManualCaptureStartDatePicker.IsEnabled = manual;

        var profile = _profiles.Get();
        if (profile is null)
        {
            CaptureStartHintText.Text = manual
                ? _localization.GetString("DataSync.ManualHint")
                : string.Empty;
            return;
        }

        var ingestion = _services.GetRequiredService<HistoryIngestionService>();
        var installationDate = ingestion.GetInstallationDate(profile);
        var automaticStart = ingestion.GetSuggestedAutomaticStartDate(profile);
        var automaticDate = automaticStart.ToString("dd-MM-yyyy");
        var installationText = installationDate.HasValue
            ? string.Format(
                _localization.GetString("DataSync.InstallationDate"),
                installationDate.Value.ToString("dd-MM-yyyy"))
            : string.Empty;

        if (manual)
        {
            if (!ManualCaptureStartDatePicker.SelectedDate.HasValue)
            {
                ManualCaptureStartDatePicker.SelectedDate =
                    automaticStart.ToDateTime(TimeOnly.MinValue);
            }

            CaptureStartHintText.Text = string.IsNullOrWhiteSpace(installationText)
                ? _localization.GetString("DataSync.ManualHint")
                : $"{installationText}\n{_localization.GetString("DataSync.ManualHint")}";
        }
        else
        {
            var resumeText = string.Format(
                _localization.GetString("DataSync.AutomaticResumeFrom"),
                automaticDate);

            CaptureStartHintText.Text = string.IsNullOrWhiteSpace(installationText)
                ? resumeText
                : $"{installationText}\n{resumeText}";
        }
    }

    private void DataCoverageToAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDataCoverageContext(out _))
            return;
        ShowPage("Analysis");
        // Force re-resolution even when a previous visit already selected All.
        _suppressAnalysisRangeSelection = true;
        try { AnalysisRangePresetSelector.SelectedValue = null; }
        finally { _suppressAnalysisRangeSelection = false; }
        AnalysisRangePresetSelector.SelectedValue = "all";
    }

    private void DataCoverageToReport_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetDataCoverageContext(out var context))
            OpenContextReportDraft(context, "Data");
    }

    private bool TryGetDataCoverageContext(out ReportContextSelection context)
    {
        context = null!;
        if (_windowClosed || DataContent.Visibility != Visibility.Visible ||
            _dataCoverageContext is not { } current ||
            !string.Equals(current.DeviceId, _profiles.Get()?.DeviceId,
                StringComparison.Ordinal))
            return false;
        context = current.Draft;
        return true;
    }

    private sealed record DataCoverageSnapshot(
        DateTimeOffset? FirstSampleAtUtc,
        DateTimeOffset? LastSampleAtUtc,
        string ReviewedDays,
        string IssueDays,
        string EmptyDays,
        string PartialDays,
        string UnavailableDays,
        string SavedReadings,
        string ReadyReadings,
        string InstallationDate,
        string NextDownloadDate,
        string? HealthStatus,
        string HealthConfirmed,
        string HealthDrift,
        string HealthUnresolved);

    // Data queries can be expensive on multi-GB SQLite histories. Only the
    // current visible page and matching device may receive an async result.
    private async void RefreshDataCoverageView()
    {
        using var measure = _performance.Measure("UI.DataCoverage.Refresh");
        if (!IsInitialized || DataContent is null || _windowClosed ||
            DataContent.Visibility != Visibility.Visible)
            return;

        var generation = ++_dataCoverageRefreshGeneration;
        _dataCoverageContext = null;
        DataCoverageAnalyzeButton.IsEnabled = false;
        DataCoverageReportButton.IsEnabled = false;
        var none = _localization.GetString("Data.None");
        var profile = _profiles.Get();

        DataStoredFromText.Text = none;
        DataStoredToText.Text = none;
        DataInstallationDateText.Text = none;
        DataNextDownloadText.Text = none;
        DataReviewedDaysText.Text = "—";
        DataIssueDaysText.Text = "—";
        DataEmptyDaysText.Text = "—";
        DataPartialDaysText.Text = "—";
        DataUnavailableDaysText.Text = "—";
        DataSavedReadingsText.Text = "—";
        DataReadyReadingsText.Text = "—";
        ConfigurationHealthText.SetResourceReference(
            TextBlock.TextProperty, "Data.ConfigurationUnresolved");
        ConfigurationHealthDetailText.Text = string.Empty;
        ConfigurationHealthText.Foreground = Brushes.Gray;

        if (profile is null)
        {
            DataCoverageStatusText.Text = none;
            return;
        }

        var deviceId = profile.DeviceId;
        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;
        DataCoverageStatusText.Text = _localization.GetString("Data.CoverageLoading");
        try
        {
            var result = await Task.Run(() =>
            {
                var history = _services.GetRequiredService<HistoryRepository>();
                var normalized = _services.GetRequiredService<NormalizationRepository>();
                var ingestion = _services.GetRequiredService<HistoryIngestionService>();
                var coverage = MeasureDataCall(
                    "Data.Coverage.Summary",
                    () => history.GetCoverageSummary(deviceId));
                var ready = MeasureDataCall(
                    "Data.Coverage.NormalizedCount",
                    () => normalized.GetNormalizedSampleCount(deviceId));
                var installation = ingestion.GetInstallationDate(profile);
                var nextDownload = ingestion.GetSuggestedAutomaticStartDate(profile);
                var healthRepository =
                    _services.GetRequiredService<InstallationHealthRepository>();
                var health = MeasureDataCall(
                    "Data.Coverage.Health",
                    () => healthRepository.GetSummary(deviceId));

                return new DataCoverageSnapshot(
                    coverage.FirstSampleAtUtc,
                    coverage.LastSampleAtUtc,
                    (coverage.CompleteDays + coverage.EmptyDays + coverage.OpenDays)
                        .ToString("N0"),
                    (coverage.PartialDays + coverage.UnavailableDays).ToString("N0"),
                    coverage.EmptyDays.ToString("N0"),
                    coverage.PartialDays.ToString("N0"),
                    coverage.UnavailableDays.ToString("N0"),
                    coverage.RawSampleCount.ToString("N0"),
                    ready.ToString("N0"),
                    installation?.ToString("dd-MM-yyyy") ?? string.Empty,
                    nextDownload.ToString("dd-MM-yyyy"),
                    health?.OverallStatus,
                    health?.ConfirmedCount.ToString() ?? "0",
                    health?.DriftCount.ToString() ?? "0",
                    health?.UnresolvedCount.ToString() ?? "0");
            });

            if (!DataCoverageRefreshPolicy.CanApply(
                    generation, _dataCoverageRefreshGeneration,
                    DataContent.Visibility == Visibility.Visible && !_windowClosed,
                    deviceId, _profiles.Get()?.DeviceId))
                return;

            DataStoredFromText.Text = result.FirstSampleAtUtc.HasValue
                ? SolarApiTime.GetLocalDate(result.FirstSampleAtUtc.Value, timeZone)
                    .ToString("dd-MM-yyyy") : none;
            DataStoredToText.Text = result.LastSampleAtUtc.HasValue
                ? SolarApiTime.GetLocalDate(result.LastSampleAtUtc.Value, timeZone)
                    .ToString("dd-MM-yyyy") : none;
            DataInstallationDateText.Text = string.IsNullOrEmpty(result.InstallationDate)
                ? none : result.InstallationDate;
            DataNextDownloadText.Text = result.NextDownloadDate;
            DataReviewedDaysText.Text = result.ReviewedDays;
            DataIssueDaysText.Text = result.IssueDays;
            DataEmptyDaysText.Text = result.EmptyDays;
            DataPartialDaysText.Text = result.PartialDays;
            DataUnavailableDaysText.Text = result.UnavailableDays;
            DataSavedReadingsText.Text = result.SavedReadings;
            DataReadyReadingsText.Text = result.ReadyReadings;
            if (result.HealthStatus is null)
            {
                ConfigurationHealthText.SetResourceReference(
                    TextBlock.TextProperty, "Data.ConfigurationUnresolved");
            }
            else
            {
                ConfigurationHealthText.SetResourceReference(
                    TextBlock.TextProperty, result.HealthStatus switch
                    {
                        "CONFIG_CONFIRMED" => "Data.ConfigurationConfirmed",
                        "CONFIG_DRIFT" => "Data.ConfigurationDrift",
                        _ => "Data.ConfigurationUnresolved"
                    });
                ConfigurationHealthText.Foreground = result.HealthStatus switch
                {
                    "CONFIG_CONFIRMED" => Brushes.Green,
                    "CONFIG_DRIFT" => Brushes.DarkOrange,
                    _ => Brushes.Gray
                };
                ConfigurationHealthDetailText.Text = string.Format(
                    _localization.GetString("Data.ConfigurationDetail"),
                    result.HealthConfirmed, result.HealthDrift, result.HealthUnresolved);
            }

            if (result.FirstSampleAtUtc is { } firstStored &&
                result.LastSampleAtUtc is { } lastStored &&
                firstStored <= lastStored)
            {
                _dataCoverageContext = (deviceId,
                    ReportContextNavigationPolicy.FromStoredCoverage(
                        firstStored, lastStored, timeZone, ReportKind.DetailedEnergy));
                DataCoverageAnalyzeButton.IsEnabled = true;
                DataCoverageReportButton.IsEnabled = true;
            }
            DataCoverageStatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            if (!DataCoverageRefreshPolicy.CanApply(
                    generation, _dataCoverageRefreshGeneration,
                    DataContent.Visibility == Visibility.Visible && !_windowClosed,
                    deviceId, _profiles.Get()?.DeviceId))
                return;
            _dataCoverageContext = null;
            DataCoverageAnalyzeButton.IsEnabled = false;
            DataCoverageReportButton.IsEnabled = false;
            DataCoverageStatusText.Text =
                _localization.GetString("Data.CoverageLoadFailed");
            Debug.WriteLine($"Data coverage failed ({ex.GetType().Name}).");
        }
    }

    private void RefreshConnectionStatus()
    {
        RefreshSettingsSessionStatus();
        var profile = _profiles.Get();

        if (profile is not null && _session.IsSessionVerified)
        {
            SolarStatusText.Text =
                $"{_localization.GetString("Status.Commissioned")}: {profile.DeviceName ?? profile.DeviceId}";
            SolarStatusText.Foreground = Brushes.Green;
            return;
        }

        if (profile is not null && _session.HasSession)
        {
            SolarStatusText.SetResourceReference(
                TextBlock.TextProperty,
                "Status.SessionStoredUnverified");
            SolarStatusText.Foreground = Brushes.DarkOrange;
            return;
        }

        if (profile is not null)
        {
            SolarStatusText.SetResourceReference(
                TextBlock.TextProperty,
                "Status.LoginRequired");
            SolarStatusText.Foreground = Brushes.DarkOrange;
            return;
        }

        if (_session.IsSessionVerified)
        {
            SolarStatusText.SetResourceReference(TextBlock.TextProperty, "Status.SessionReady");
            SolarStatusText.Foreground = Brushes.DarkOrange;
            return;
        }

        if (_session.HasSession)
        {
            SolarStatusText.SetResourceReference(
                TextBlock.TextProperty,
                "Status.SessionStoredUnverified");
            SolarStatusText.Foreground = Brushes.DarkOrange;
            return;
        }

        SolarStatusText.SetResourceReference(TextBlock.TextProperty, "Status.NotConnected");
        SolarStatusText.Foreground = Brushes.Red;
    }
}

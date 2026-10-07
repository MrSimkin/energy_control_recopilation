using System.Diagnostics;
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
    private readonly DispatcherTimer _dashboardLiveTimer;
    private readonly DispatcherTimer _dashboardProgressTimer;
    private DateTimeOffset _dashboardLiveCycleStartedUtc = DateTimeOffset.UtcNow;
    private CancellationTokenSource? _syncCancellation;
    private bool _currentStateRefreshInProgress;
    private int _utilityAuditRefreshGeneration;
    private (string DeviceId, long BillId, string TimeZoneId)?
        _utilityBillSummaryInFlightKey;
    private Task<UtilityBillReconciliationSummary>?
        _utilityBillSummaryInFlight;
    private UtilityBillPdfDraft? _pendingUtilityBillPdfDraft;
    private long? _editingUtilityBillId;
    private long? _editingUtilityBillLineId;
    private bool _dashboardVisible;
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
        IServiceProvider services)
    {
        _paths = paths;
        _localization = localization;
        _session = session;
        _profiles = profiles;
        _services = services;

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
        Closed += MainWindow_Closed;

        InitializeComponent();

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
        RefreshCaptureStartOptions();
        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshGridUtilityView();
        RefreshDataCoverageView();
        RefreshAnalysisView(initializeRange: true);
        RefreshReportsView(initializeRange: true);
        ShowPage("Dashboard");
        ApplyResponsiveCardLayouts();
        Loaded += MainWindow_Loaded;
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
        var columns = usableWidth switch
        {
            < 620 => 1,
            < 1040 => 2,
            _ => 4
        };

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
            RefreshBatteryView();
            RefreshDataCoverageView();
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
        RefreshBatteryView();
        RefreshDataCoverageView();
        RefreshAnalysisView();
        RefreshReportsView();
        SetGlobalOperation(false, string.Empty);
    }

    private void RefreshDashboardMetrics()
    {
        var profile = _profiles.Get();
        if (profile is null)
        {
            ResetDashboardMetrics();
            return;
        }

        var repository = _services.GetRequiredService<NormalizationRepository>();
        var storedMetrics = repository.GetLatestMetrics(profile.DeviceId);
        var current = _services
            .GetRequiredService<CurrentHouseholdSnapshotService>()
            .GetLatest(profile.DeviceId);
        var useCurrentSnapshot =
            current is { Metrics.Count: > 0 };
        var metrics = useCurrentSnapshot
            ? current!.Metrics
            : storedMetrics;

        SetPowerMetric(metrics, "pv_power_w", PvPowerValueText, PvPowerMetaText);
        SetPowerMetric(metrics, "house_load_power_w", HouseLoadValueText, HouseLoadMetaText);
        SetSocMetric(metrics, "battery_soc_pct", BatterySocValueText, BatterySocMetaText);
        SetPowerMetric(metrics, "grid_import_power_w", GridImportValueText, GridImportMetaText);
        RefreshOperatingState(metrics);
        RefreshDashboardFreshness(
            metrics,
            useCurrentSnapshot ? current : null);
        RefreshDashboardLatestSavedDay(profile);
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

    private void RefreshDashboardLatestSavedDay(
        CommissioningProfile profile)
    {
        var history =
            _services.GetRequiredService<HistoryRepository>();
        var coverage =
            history.GetCoverageSummary(profile.DeviceId);

        if (!coverage.LastSampleAtUtc.HasValue)
        {
            ResetDashboardLatestSavedDay();
            return;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var localDate = SolarApiTime.GetLocalDate(
            coverage.LastSampleAtUtc.Value,
            timeZone);
        var window = SolarApiTime.GetLocalDayWindow(
            localDate,
            timeZone);

        var summary =
            _services.GetRequiredService<EnergyRangeStatisticsService>()
                .Get(
                    profile.DeviceId,
                    window.Start,
                    window.End);

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

        DashboardLatestDayCoverageText.Text =
            coverages.Length > 0
                ? $"{coverages.Min():F1} %"
                : "— %";

        DashboardLatestDayCoverageText.Foreground =
            coverages.Length > 0 &&
            coverages.Min() < 80
                ? Brushes.DarkOrange
                : Brushes.Black;
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
        DashboardLatestDayCoverageText.Foreground =
            Brushes.Black;
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
        {
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
        {
            ResetBatteryView();
            return;
        }

        var repository = _services.GetRequiredService<NormalizationRepository>();
        var storedMetrics = repository.GetLatestMetrics(profile.DeviceId);
        var current = _services.GetRequiredService<CurrentHouseholdSnapshotService>()
            .GetLatest(profile.DeviceId);
        var useLive = current?.IsFresh == true &&
                      current.Metrics.ContainsKey("battery_soc_pct");
        var metrics = useLive
            ? current!.Metrics
            : storedMetrics;
        var configuration = _services.GetRequiredService<BatteryConfigurationService>().Get();
        var thresholds = _services.GetRequiredService<BatteryThresholdContextService>().Get(profile.DeviceId);

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
        RefreshAnalysisView();
    }

    private void AnalysisRangePresetSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressAnalysisRangeSelection || !IsInitialized)
        {
            return;
        }

        if (ApplyAnalysisRangePreset())
        {
            RefreshAnalysisView();
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

        _suppressAnalysisRangeSelection = true;
        AnalysisRangePresetSelector.SelectedValue = "custom";
        _suppressAnalysisRangeSelection = false;
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
        if (IsInitialized &&
            AnalysisContent is not null &&
            AnalysisFromDatePicker is not null &&
            AnalysisToDatePicker is not null)
        {
            RefreshAnalysisView();
        }
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

    private void RefreshAnalysisView(bool initializeRange = false)
    {
        if (!IsInitialized || AnalysisContent is null)
        {
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
        {
            ResetAnalysisView();
            return;
        }

        var history = _services.GetRequiredService<HistoryRepository>();
        var coverage = history.GetCoverageSummary(profile.DeviceId);

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
            coverage.FirstSampleAtUtc.Value,
            timeZone);
        var lastLocalDate = SolarApiTime.GetLocalDate(
            coverage.LastSampleAtUtc.Value,
            timeZone);

        if (initializeRange)
        {
            _suppressAnalysisRangeSelection = true;
            AnalysisRangePresetSelector.SelectedValue = "all";
            AnalysisFromDatePicker.SelectedDate =
                firstLocalDate.ToDateTime(TimeOnly.MinValue);
            AnalysisToDatePicker.SelectedDate =
                lastLocalDate.ToDateTime(TimeOnly.MinValue);
            _suppressAnalysisRangeSelection = false;
        }
        else
        {
            if (!AnalysisFromDatePicker.SelectedDate.HasValue)
            {
                AnalysisFromDatePicker.SelectedDate =
                    firstLocalDate.ToDateTime(TimeOnly.MinValue);
            }

            if (!AnalysisToDatePicker.SelectedDate.HasValue)
            {
                AnalysisToDatePicker.SelectedDate =
                    lastLocalDate.ToDateTime(TimeOnly.MinValue);
            }
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
            AnalysisFromDatePicker.SelectedDate =
                fromDate.ToDateTime(TimeOnly.MinValue);
            AnalysisToDatePicker.SelectedDate =
                toDate.ToDateTime(TimeOnly.MinValue);
        }

        var fromWindow = SolarApiTime.GetLocalDayWindow(fromDate, timeZone);
        var toWindow = SolarApiTime.GetLocalDayWindow(toDate, timeZone);

        var energySummary =
            _services.GetRequiredService<EnergyRangeStatisticsService>()
                .Get(
                    profile.DeviceId,
                    fromWindow.Start,
                    toWindow.End);

        RefreshAnalysisEnergySummary(energySummary);
        RefreshAnalysisAggregationTable(
            profile.DeviceId,
            fromWindow.Start,
            toWindow.End,
            timeZone);

        var statistics =
            _services.GetRequiredService<HouseholdBehaviorStatisticsService>()
                .Get(
                    profile.DeviceId,
                    fromWindow.Start,
                    toWindow.End);

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

    private bool ApplyAnalysisRangePreset()
    {
        var profile = _profiles.Get();
        if (profile is null)
        {
            return false;
        }

        var history =
            _services.GetRequiredService<HistoryRepository>();
        var coverage =
            history.GetCoverageSummary(profile.DeviceId);

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
            AnalysisRangePresetSelector.SelectedValue?.ToString() ??
            "custom";

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
        AnalysisFromDatePicker.SelectedDate =
            resolved.LocalStartDate.ToDateTime(TimeOnly.MinValue);
        AnalysisToDatePicker.SelectedDate =
            resolved.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        _suppressAnalysisRangeSelection = false;

        return true;
    }

    private void RefreshAnalysisAggregationTable(
        string deviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        string timeZoneId)
    {
        var period = GetSelectedAggregationPeriod();

        var table =
            _services.GetRequiredService<EnergyAggregationTableService>()
                .Get(
                    deviceId,
                    rangeStart,
                    rangeEnd,
                    timeZoneId,
                    period);

        _analysisAggregationRows = table.Rows;
        AnalysisAggregationGrid.ItemsSource = table.Rows;
        RefreshAnalysisEnergyChart(table.Rows);
        RefreshAnalysisBatteryChart(table.Rows);
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
        IReadOnlyList<EnergyAggregationRow> rows)
    {
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

        var thresholds = _services.GetRequiredService<BatteryThresholdContextService>()
            .Get(_profiles.Get()?.DeviceId ?? string.Empty);

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
        AnalysisFromDatePicker.SelectedDate = null;
        AnalysisToDatePicker.SelectedDate = null;
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

        var current = _services
            .GetRequiredService<CurrentHouseholdSnapshotService>()
            .GetLatest(profile.DeviceId);

        if (current?.IsFresh == true)
        {
            return;
        }

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
        var buttons = new[]
        {
            DashboardNav,
            AnalysisNav,
            BatteryNav,
            GridUtilityNav,
            ReportsNav,
            DataNav,
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
        var isHelp = string.Equals(pageKey, "Help", StringComparison.Ordinal);
        var isSettings = string.Equals(pageKey, "Settings", StringComparison.Ordinal);
        var isAbout = string.Equals(pageKey, "About", StringComparison.Ordinal);

        _dashboardVisible = isDashboard;
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
            !isHelp &&
            !isSettings &&
            !isAbout
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (isAnalysis)
        {
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
            RefreshReportsView();
        }
        else if (isData)
        {
            RefreshDataCoverageView();
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

        if (isSettings) RefreshSettingsSessionStatus();
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
        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshGridUtilityView();
        RefreshDataCoverageView();
        RefreshAnalysisView();
        RefreshReportsView();
        RefreshProductExperienceLocalization();
    }


    private void RefreshGridUtilityView()
    {
        if (!IsInitialized || GridUtilityContent is null)
        {
            return;
        }

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

        var reconciliations =
            reconciliation.GetMeterReconciliations(profile.DeviceId);

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
            DisplayUtilityReconciliation(latest, timeZone);
        }

        if (UtilityReadingSourceSelector.SelectedValue is null)
        {
            UtilityReadingSourceSelector.SelectedValue =
                UtilityReadingSourceKind.Personal;
        }
        RefreshUtilityReadingSourceUi();

        var bills = repository.GetBills();

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

        var billReconciliations = reconciliation
            .GetBillReconciliations(profile.DeviceId)
            .ToDictionary(item => item.BillId);

        UtilityBillsGrid.ItemsSource = bills
            .Select(bill =>
            {
                billReconciliations.TryGetValue(
                    bill.BillId,
                    out var comparison);

                var effectiveFrom = comparison?.FromUtc ?? bill.PeriodStartUtc;
                var effectiveTo = comparison?.ToUtc ?? bill.PeriodEndUtc;
                var totalDue = bill.TotalDueClp ?? bill.AmountClp;

                return new UtilityBillViewRow(
                    bill.BillId,
                    FormatUtilityInterval(
                        effectiveFrom,
                        effectiveTo,
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

        RefreshSelectedBillLines();
        RefreshUtilityAuditPreview();
        RefreshTariffPublications();
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
        if (IsInitialized)
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

            var audit = _services
                .GetRequiredService<UtilityBillAuditV2Service>()
                .Analyze(
                    billId,
                    timeZone);

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
                item.Status.StartsWith(
                    "RATE_VERIFIED_",
                    StringComparison.Ordinal) ||
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

            UtilityAuditBalanceText.Text =
                audit.BalanceStatus switch
                {
                    "BALANCED" =>
                        Math.Abs(audit.SimpleAdjustmentClp) > 0.0001
                            ? spanish
                                ? $"Cuadra · ajuste impreso $ {audit.SimpleAdjustmentClp:+0;-0;0}"
                                : $"Balanced · printed adjustment $ {audit.SimpleAdjustmentClp:+0;-0;0}"
                            : spanish
                                ? "Cuadra sin residuo"
                                : "Balanced with no residual",
                    "SMALL_UNEXPLAINED_RESIDUAL" =>
                        spanish
                            ? $"Revisar residuo pequeño $ {audit.UnexplainedResidualClp.GetValueOrDefault():+0;-0;0}"
                            : $"Review small residual $ {audit.UnexplainedResidualClp.GetValueOrDefault():+0;-0;0}",
                    "MATERIAL_UNEXPLAINED_DIFFERENCE" =>
                        spanish
                            ? $"Diferencia no explicada $ {audit.UnexplainedResidualClp.GetValueOrDefault():+0;-0;0}"
                            : $"Unexplained difference $ {audit.UnexplainedResidualClp.GetValueOrDefault():+0;-0;0}",
                    _ =>
                        spanish
                            ? "Total insuficiente para cuadratura"
                            : "Insufficient total for balance check"
                };

            UtilityAuditStatusText.Text =
                string.Format(
                    _localization.GetString(
                        "GridUtility.AuditPreviewSummary"),
                    verified,
                    audit.Lines.Count - verified);
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
                    ? $"Enel − inversor observado: {energyDifference} ({energyPercent}) · cobertura {summary.CoveragePercent:N2}% · tasa variable oficial soportada {rate}. La estimación monetaria recalcula componentes tarifarios sustentados y preserva explícitamente los no reconstruibles; origen {summary.BillSourceKind}, revisión {summary.ReviewState}.{ivaDetail}{adjustmentDetail}"
                    : $"Utility − observed inverter: {energyDifference} ({energyPercent}) · coverage {summary.CoveragePercent:N2}% · supported official variable rate {rate}. The monetary estimate reconstructs supported tariff components and explicitly preserves non-reconstructable items; source {summary.BillSourceKind}, review {summary.ReviewState}.{ivaDetail}{adjustmentDetail}";

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

        if (dialog.ShowDialog(this) != true)
            return;

        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

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

            UtilityAuditStatusText.Text = string.Format(
                _localization.GetString("GridUtility.ExportBillAuditSaved"),
                dialog.FileName);
        }
        catch (Exception ex)
        {
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

        if (dialog.ShowDialog(this) != true)
            return;

        var spanish =
            _localization.CurrentLanguage.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

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

            UtilityAuditStatusText.Text =
                string.Format(
                    _localization.GetString(
                        "GridUtility.ExportBillAnnexSaved"),
                    dialog.FileName);
        }
        catch (Exception ex)
        {
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

        UtilityMeterReading? fromReading = null;
        UtilityMeterReading? toReading = null;
        DateTimeOffset startUtc;
        DateTimeOffset endUtc;
        var periodPrecision =
            UtilityBillPeriodPrecisionSelector.SelectedValue?.ToString()
            ?? UtilityTimePrecision.DateOnly;

        if (UtilityBillFromReadingSelector.SelectedValue is long fromId &&
            UtilityBillToReadingSelector.SelectedValue is long toId)
        {
            fromReading = repository.GetReading(fromId);
            toReading = repository.GetReading(toId);
            if (fromReading is null ||
                toReading is null ||
                toReading.ReadingAtUtc <= fromReading.ReadingAtUtc)
            {
                UtilityBillStatusText.Text =
                    _localization.GetString(
                        "GridUtility.CompareInvalid");
                return;
            }

            startUtc = fromReading.ReadingAtUtc;
            endUtc = toReading.ReadingAtUtc;
            if (fromReading.TimePrecision != UtilityTimePrecision.Exact ||
                toReading.TimePrecision != UtilityTimePrecision.Exact)
            {
                periodPrecision = UtilityTimePrecision.DateOnly;
            }
        }
        else if (string.Equals(
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
                UtilityBillKwhTextBox.Text,
                out var billedKwh))
        {
            UtilityBillStatusText.Text =
                _localization.GetString(
                    "GridUtility.InvalidNumber");
            return;
        }

        if (!billedKwh.HasValue &&
            fromReading is not null &&
            toReading is not null)
        {
            var derived =
                toReading.ReadingKwh -
                fromReading.ReadingKwh;
            if (derived >= 0)
                billedKwh = derived;
        }

        if (!TryParseOptionalNonNegative(UtilityBillTaxableTextBox.Text, out var taxable) ||
            !TryParseOptionalNonNegative(UtilityBillIvaTextBox.Text, out var iva) ||
            !TryParseOptionalNonNegative(UtilityBillExemptTextBox.Text, out var exempt) ||
            !TryParseOptionalNonNegative(UtilityBillGrossTextBox.Text, out var gross) ||
            !TryParseOptionalFinite(UtilityBillOtherChargesTextBox.Text, out var otherCharges) ||
            !TryParseOptionalNonNegative(UtilityBillTotalDueTextBox.Text, out var totalDue))
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
                    fromReading?.ReadingId,
                    toReading?.ReadingId,
                    fromReading?.ReadingKwh,
                    toReading?.ReadingKwh,
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
                    0.19);
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
                    fromReading?.ReadingId,
                    toReading?.ReadingId,
                    fromReading?.ReadingKwh,
                    toReading?.ReadingKwh,
                    UtilityBillTariffPlanTextBox.Text,
                    taxable,
                    iva,
                    exempt,
                    gross,
                    otherCharges,
                    totalDue,
                    periodPrecision,
                    sourceKind: sourceKind,
                    sourceDocumentId: sourceDocumentId,
                    reviewState: UtilityBillReviewState.Reviewed,
                    ivaRate: 0.19);
            }

            SaveUtilityBillFieldEvidence(
                repository,
                billId,
                timeZone,
                startUtc,
                endUtc,
                periodPrecision,
                billedKwh,
                taxable,
                iva,
                exempt,
                gross,
                otherCharges,
                totalDue,
                UtilityBillTariffPlanTextBox.Text);

            if (_pendingUtilityBillPdfDraft is not null &&
                UtilityBillPdfApplyLinesCheckBox.IsChecked == true)
            {
                MergeUtilityBillPdfLines(
                    repository,
                    billId,
                    _pendingUtilityBillPdfDraft);
            }

            var wasReview =
                _editingUtilityBillId.HasValue;
            ClearUtilityBillEditor();
            UtilityBillStatusText.Text =
                wasReview
                    ? "Boleta revisada y actualizada en el mismo registro."
                    : _localization.GetString(
                        "GridUtility.BillSaved");
            RefreshGridUtilityView();
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

            if (DateOnly.FromDateTime(
                    picker.SelectedDate.Value) !=
                proposed.Value)
            {
                conflicts.Add(
                    $"{label}: guardado " +
                    $"{picker.SelectedDate.Value:dd-MM-yyyy} " +
                    $"vs PDF {proposed.Value:dd-MM-yyyy}");
            }
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

            if (!reviewingExisting ||
                string.IsNullOrWhiteSpace(
                    box.Text))
            {
                box.Text =
                    proposed.Value.ToString(
                        format,
                        CultureInfo.CurrentCulture);
                return;
            }

            if (!TryParseNumber(
                    box.Text,
                    out var current) ||
                Math.Abs(
                    current -
                    proposed.Value) >
                tolerance)
            {
                conflicts.Add(
                    $"{label}: guardado " +
                    $"{box.Text} vs PDF " +
                    $"{proposed.Value.ToString(format, CultureInfo.CurrentCulture)}");
            }
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
                "precisión del período: el PDF sólo respalda fechas impresas, no una hora exacta");
        }

        ApplyDate(
            UtilityBillStartDatePicker,
            draft.PeriodStart,
            "inicio");
        ApplyDate(
            UtilityBillEndDatePicker,
            draft.PeriodEndInclusive,
            "fin");

        if (!reviewingExisting)
        {
            UtilityBillStartTimeTextBox.Text = "00:00";
            UtilityBillEndTimeTextBox.Text = "00:00";
        }

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
            UtilityBillTotalDueTextBox,
            draft.TotalDueClp,
            "0",
            0.5,
            "total");

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
                    $"tarifa: guardado {UtilityBillTariffPlanTextBox.Text.Trim()} " +
                    $"vs PDF {draft.TariffPlan}");
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

        var detected =
            new List<string>();
        if (draft.PeriodStart.HasValue &&
            draft.PeriodEndInclusive.HasValue)
            detected.Add("período");
        if (draft.BilledConsumptionKwh.HasValue)
            detected.Add("kWh");
        if (draft.TaxableAmountClp.HasValue)
            detected.Add("monto afecto");
        if (draft.IvaClp.HasValue)
            detected.Add("IVA");
        if (draft.ExemptAmountClp.HasValue)
            detected.Add("monto exento");
        if (draft.TotalDueClp.HasValue)
            detected.Add("total");
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
                : " CONFLICTOS PARA REVISAR: " +
                  string.Join(
                      " | ",
                      conflicts);

        UtilityBillStatusText.Text =
            $"PDF preparado ({string.Join(", ", detected)}). " +
            (reviewingExisting
                ? "Los valores guardados no fueron sobreescritos cuando discrepan del PDF."
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
        double? billedKwh,
        double? taxable,
        double? iva,
        double? exempt,
        double? gross,
        double? otherCharges,
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
            var evidence = Evidence(matched);
            repository.UpsertBillFieldEvidence(
                billId,
                key,
                evidence.Source,
                evidence.State,
                value.Value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture),
                value.Value.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }

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
            gross);
        Numeric(
            "other_charges_clp",
            otherCharges);
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
            var evidence = Evidence(matched);
            repository.UpsertBillFieldEvidence(
                billId,
                "tariff_plan",
                evidence.Source,
                evidence.State,
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

    private void MergeUtilityBillPdfLines(
        UtilityMeterRepository repository,
        long billId,
        UtilityBillPdfDraft draft)
    {
        var existing =
            repository.GetBillLines(billId)
                .ToList();
        var nextSortOrder =
            existing.Count == 0
                ? 10
                : existing.Max(item => item.SortOrder) + 10;

        foreach (var draftLine in draft.Lines)
        {
            var match = existing.FirstOrDefault(item =>
                Math.Abs(
                    item.AmountClp -
                    draftLine.AmountClp) <= 0.5 &&
                (
                    string.Equals(
                        item.CategoryKey,
                        draftLine.CategoryKey,
                        StringComparison.OrdinalIgnoreCase) ||
                    DescriptionsLikelySame(
                        item.Description,
                        draftLine.Description)));

            if (match is not null)
            {
                repository.ConfirmBillLinePdfEvidence(
                    match.BillLineId,
                    draftLine.CategoryKey,
                    draftLine.SourcePage,
                    draftLine.SourceText);
                continue;
            }

            var newId = repository.AddBillLine(
                billId,
                draftLine.SectionKey,
                draftLine.Description,
                draftLine.AmountClp,
                categoryKey: draftLine.CategoryKey,
                sortOrder: nextSortOrder,
                sourceKind:
                    UtilityBillSourceKind.PdfReviewed,
                evidenceState:
                    UtilityBillEvidenceState.PdfExtractedConfirmed,
                sourcePage: draftLine.SourcePage,
                sourceText: draftLine.SourceText);
            nextSortOrder += 10;

            existing.Add(
                repository.GetBillLines(billId)
                    .Single(item =>
                        item.BillLineId == newId));
        }
    }

    private static bool DescriptionsLikelySame(
        string left,
        string right)
    {
        static string Normalize(string value)
        {
            var form = value.Normalize(
                System.Text.NormalizationForm.FormD);
            var chars = form
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(
                        character) !=
                    UnicodeCategory.NonSpacingMark)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray();
            return new string(chars);
        }

        var a = Normalize(left);
        var b = Normalize(right);
        return a.Length >= 5 &&
               b.Length >= 5 &&
               (a.Contains(b, StringComparison.Ordinal) ||
                b.Contains(a, StringComparison.Ordinal));
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
        UtilityBillStatusText.Text =
            $"Revisando boleta #{bill.BillId} · origen {bill.SourceKind} · estado {bill.ReviewState}. " +
            "Puedes corregirla manualmente y/o importar el PDF para contrastar y vincular evidencia.";
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

        UtilityBillKwhTextBox.Clear();
        UtilityBillTaxableTextBox.Clear();
        UtilityBillIvaTextBox.Clear();
        UtilityBillExemptTextBox.Clear();
        UtilityBillGrossTextBox.Clear();
        UtilityBillOtherChargesTextBox.Clear();
        UtilityBillTotalDueTextBox.Clear();
        UtilityBillTariffPlanTextBox.Clear();
        UtilityBillReferenceTextBox.Clear();
        UtilityBillNotesTextBox.Clear();
        UtilityBillFromReadingSelector.SelectedIndex = -1;
        UtilityBillToReadingSelector.SelectedIndex = -1;
        UtilityAddBillButton.Content =
            _localization.GetString(
                "GridUtility.AddBill");
        UtilityCancelBillReviewButton.Visibility =
            Visibility.Collapsed;
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
                    "OTROS_CARGOS";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.ServiceAdministration");
                break;

            case "METER_RENTAL":
                UtilityBillLineSectionSelector.SelectedValue =
                    "OTROS_CARGOS";
                UtilityBillLineDescriptionTextBox.Text =
                    _localization.GetString(
                        "GridUtility.BillType.MeterRental");
                break;

            case "COMMON_SERVICE":
                UtilityBillLineSectionSelector.SelectedValue =
                    "OTROS_CARGOS";
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
            RefreshUtilityAuditPreview();
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

        var request = GetCurrentReportRequest();
        if (request is null)
        {
            ReportSelectionSummaryText.Text =
                _localization.GetString("Reports.NoData");
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
        var request = GetCurrentReportRequest();
        if (request is null)
        {
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

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ReportExportExcelButton.IsEnabled = false;
        ReportExportPdfButton.IsEnabled = false;
        ReportExportProgressLabel.Visibility = Visibility.Visible;
        ReportExportProgressBar.Visibility = Visibility.Visible;
        ReportExportProgressBar.IsIndeterminate = false;
        ReportExportProgressBar.Value = 15;
        ReportExportProgressLabel.Text =
            _localization.GetString("Reports.ExportPreparing");
        ReportStatusText.Text = string.Empty;
        SetGlobalOperation(
            true,
            _localization.CurrentLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "Exportando informe..."
                : "Exporting report...");

        try
        {
            var exporter =
                _services.GetRequiredService<EnergyReportExportService>();

            var report = await Task.Run(() => exporter.Build(request));

            ReportExportProgressBar.Value = 55;
            ReportExportProgressLabel.Text =
                _localization.GetString("Reports.ExportGenerating");

            await Task.Run(() =>
            {
                if (format == "xlsx")
                {
                    exporter.ExportExcel(dialog.FileName, report);
                }
                else
                {
                    exporter.ExportPdf(dialog.FileName, report);
                }
            });

            ReportExportProgressBar.Value = 100;
            ReportExportProgressLabel.Text =
                _localization.GetString("Reports.ExportComplete");
            ReportStatusText.Text = string.Format(
                _localization.GetString("Reports.ExportSaved"),
                dialog.FileName);
        }
        catch (Exception ex)
        {
            ReportExportProgressBar.Value = 100;
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
                    RefreshDashboardMetrics();
                    RefreshBatteryView();
                    RefreshAnalysisView();
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
            RefreshDataCoverageView();
            RefreshAnalysisView();
            RefreshReportsView();
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
                RefreshDashboardMetrics();
                RefreshBatteryView();
                RefreshDataCoverageView();
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
        _dashboardLiveTimer.Stop();
        _dashboardLiveTimer.Tick -= DashboardLiveTimer_Tick;
        _dashboardProgressTimer.Stop();
        _dashboardProgressTimer.Tick -= DashboardProgressTimer_Tick;
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

    private void RefreshDataCoverageView()
    {
        if (!IsInitialized || DataContent is null)
        {
            return;
        }

        var profile = _profiles.Get();
        if (profile is null)
        {
            var none = _localization.GetString("Data.None");
            DataStoredFromText.Text = none;
            DataStoredToText.Text = none;
            DataReviewedDaysText.Text = "0";
            DataIssueDaysText.Text = "0";
            DataInstallationDateText.Text = none;
            DataNextDownloadText.Text = none;
            DataEmptyDaysText.Text = "0";
            DataPartialDaysText.Text = "0";
            DataUnavailableDaysText.Text = "0";
            DataSavedReadingsText.Text = "0";
            DataReadyReadingsText.Text = "0";
            ConfigurationHealthText.SetResourceReference(
                TextBlock.TextProperty,
                "Data.ConfigurationUnresolved");
            ConfigurationHealthDetailText.Text = string.Empty;
            ConfigurationHealthText.Foreground = Brushes.Gray;
            return;
        }

        var history = _services.GetRequiredService<HistoryRepository>();
        var normalized = _services.GetRequiredService<NormalizationRepository>();
        var ingestion = _services.GetRequiredService<HistoryIngestionService>();
        var coverage = history.GetCoverageSummary(profile.DeviceId);

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        DataStoredFromText.Text = coverage.FirstSampleAtUtc.HasValue
            ? SolarApiTime.GetLocalDate(
                coverage.FirstSampleAtUtc.Value,
                timeZone).ToString("dd-MM-yyyy")
            : _localization.GetString("Data.None");

        DataStoredToText.Text = coverage.LastSampleAtUtc.HasValue
            ? SolarApiTime.GetLocalDate(
                coverage.LastSampleAtUtc.Value,
                timeZone).ToString("dd-MM-yyyy")
            : _localization.GetString("Data.None");

        var reviewedDays =
            coverage.CompleteDays +
            coverage.EmptyDays +
            coverage.OpenDays;

        var issueDays =
            coverage.PartialDays +
            coverage.UnavailableDays;

        DataReviewedDaysText.Text = reviewedDays.ToString("N0");
        DataIssueDaysText.Text = issueDays.ToString("N0");

        var installationDate = ingestion.GetInstallationDate(profile);
        DataInstallationDateText.Text = installationDate.HasValue
            ? installationDate.Value.ToString("dd-MM-yyyy")
            : _localization.GetString("Data.None");

        DataNextDownloadText.Text =
            ingestion.GetSuggestedAutomaticStartDate(profile)
                .ToString("dd-MM-yyyy");

        DataEmptyDaysText.Text = coverage.EmptyDays.ToString("N0");
        DataPartialDaysText.Text = coverage.PartialDays.ToString("N0");
        DataUnavailableDaysText.Text = coverage.UnavailableDays.ToString("N0");
        DataSavedReadingsText.Text = coverage.RawSampleCount.ToString("N0");
        DataReadyReadingsText.Text =
            normalized.GetNormalizedSampleCount(profile.DeviceId).ToString("N0");

        var healthRepository =
            _services.GetRequiredService<InstallationHealthRepository>();
        var health = healthRepository.GetSummary(profile.DeviceId);

        if (health is null)
        {
            ConfigurationHealthText.SetResourceReference(
                TextBlock.TextProperty,
                "Data.ConfigurationUnresolved");
            ConfigurationHealthDetailText.Text = string.Empty;
            ConfigurationHealthText.Foreground = Brushes.Gray;
        }
        else
        {
            var resourceKey = health.OverallStatus switch
            {
                "CONFIG_CONFIRMED" => "Data.ConfigurationConfirmed",
                "CONFIG_DRIFT" => "Data.ConfigurationDrift",
                _ => "Data.ConfigurationUnresolved"
            };

            ConfigurationHealthText.SetResourceReference(
                TextBlock.TextProperty,
                resourceKey);

            ConfigurationHealthText.Foreground = health.OverallStatus switch
            {
                "CONFIG_CONFIRMED" => Brushes.Green,
                "CONFIG_DRIFT" => Brushes.DarkOrange,
                _ => Brushes.Gray
            };

            ConfigurationHealthDetailText.Text = string.Format(
                _localization.GetString("Data.ConfigurationDetail"),
                health.ConfirmedCount,
                health.DriftCount,
                health.UnresolvedCount);
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Normalization;
using SolarOfThings.Core.Reporting;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.App;

public partial class MainWindow : Window
{
    private static readonly Brush ActiveNavigationBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x7B, 0xFF));

    private readonly AppPaths _paths;
    private readonly LocalizationService _localization;
    private readonly SolarOfThingsSessionManager _session;
    private readonly CommissioningProfileRepository _profiles;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _syncCancellation;
    private bool _suppressLanguageSelection;
    private bool _suppressAnalysisRangeSelection;
    private bool _suppressReportRangeSelection;
    private bool _suppressReportPresetSelection;
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

        AutoConnectCheckBox.IsChecked = GetAutoConnectEnabled();
        RefreshConnectionStatus();
        RefreshCaptureStartOptions();
        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshDataCoverageView();
        RefreshAnalysisView(initializeRange: true);
        RefreshReportsView(initializeRange: true);
        ShowPage("Dashboard");
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var profile = _profiles.Get();
        if (profile is null)
        {
            return;
        }

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
        var current = _services.GetRequiredService<CurrentHouseholdSnapshotService>().GetLatest(profile.DeviceId);
        var metrics = current is { IsFresh: true } && current.Metrics.Count > 0 ? current.Metrics : storedMetrics;

        SetPowerMetric(metrics, "pv_power_w", PvPowerValueText, PvPowerMetaText);
        SetPowerMetric(metrics, "house_load_power_w", HouseLoadValueText, HouseLoadMetaText);
        SetSocMetric(metrics, "battery_soc_pct", BatterySocValueText, BatterySocMetaText);
        SetPowerMetric(metrics, "grid_import_power_w", GridImportValueText, GridImportMetaText);
        RefreshOperatingState(metrics);
        RefreshDashboardFreshness(metrics);
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
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics)
    {
        if (metrics.Count == 0)
        {
            DashboardFreshnessText.Text = string.Empty;
            return;
        }

        var latest = metrics.Values.Max(metric => metric.RecordedAtUtc);
        var display = latest.ToLocalTime().ToString("dd-MM-yyyy HH:mm");
        var age = DateTimeOffset.UtcNow - latest;

        var recent = age >= TimeSpan.FromMinutes(-5) &&
                     age <= TimeSpan.FromMinutes(20);

        DashboardFreshnessText.Text = string.Format(
            _localization.GetString(
                recent
                    ? "Dashboard.FreshnessRecent"
                    : "Dashboard.FreshnessStale"),
            display);

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
        var metrics = repository.GetLatestMetrics(profile.DeviceId);
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
        BatteryLastReadingText.Text = soc.RecordedAtUtc
            .ToLocalTime()
            .ToString("dd-MM-yyyy HH:mm");

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
        SelectionChangedEventArgs e)
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

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string pageKey)
        {
            ShowPage(pageKey);
        }
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
            SettingsNav
        };

        foreach (var button in buttons)
        {
            button.ClearValue(BackgroundProperty);
            button.ClearValue(ForegroundProperty);
        }

        var selectedButton = buttons.FirstOrDefault(
            button => string.Equals(button.Tag?.ToString(), pageKey, StringComparison.Ordinal));

        if (selectedButton is not null)
        {
            selectedButton.Background = ActiveNavigationBackground;
            selectedButton.Foreground = Brushes.White;
        }

        PageTitle.SetResourceReference(TextBlock.TextProperty, $"Page.{pageKey}.Title");
        PageSubtitle.SetResourceReference(TextBlock.TextProperty, $"Page.{pageKey}.Subtitle");

        var isDashboard = string.Equals(pageKey, "Dashboard", StringComparison.Ordinal);
        var isAnalysis = string.Equals(pageKey, "Analysis", StringComparison.Ordinal);
        var isBattery = string.Equals(pageKey, "Battery", StringComparison.Ordinal);
        var isReports = string.Equals(pageKey, "Reports", StringComparison.Ordinal);
        var isData = string.Equals(pageKey, "Data", StringComparison.Ordinal);
        var isSettings = string.Equals(pageKey, "Settings", StringComparison.Ordinal);

        DashboardContent.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;
        AnalysisContent.Visibility = isAnalysis ? Visibility.Visible : Visibility.Collapsed;
        BatteryContent.Visibility = isBattery ? Visibility.Visible : Visibility.Collapsed;
        ReportsContent.Visibility = isReports ? Visibility.Visible : Visibility.Collapsed;
        DataContent.Visibility = isData ? Visibility.Visible : Visibility.Collapsed;
        SettingsContent.Visibility = isSettings ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility =
            !isDashboard && !isAnalysis && !isBattery && !isReports && !isData && !isSettings
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
        else if (isReports)
        {
            RefreshReportsView();
        }
        else if (isData)
        {
            RefreshDataCoverageView();
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
        RefreshDataCoverageView();
        RefreshAnalysisView();
        RefreshReportsView();
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
        UpdateReportSelectionSummary();
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
        if (IsInitialized)
        {
            UpdateReportSelectionSummary();
        }
    }

    private void ReportRefreshSelection_Click(
        object sender,
        RoutedEventArgs e)
    {
        ApplyReportRangePreset();
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
            string.IsNullOrWhiteSpace(ReportPresetNameTextBox.Text)
                ? GetDefaultReportTitle(kind)
                : ReportPresetNameTextBox.Text.Trim();

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

    private void ReportExportExcel_Click(
        object sender,
        RoutedEventArgs e)
    {
        ExportEnergyReport("xlsx");
    }

    private void ReportExportPdf_Click(
        object sender,
        RoutedEventArgs e)
    {
        ExportEnergyReport("pdf");
    }

    private void ExportEnergyReport(string format)
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
                $"SolarEnergy_{request.LocalStartDate:yyyyMMdd}_{request.LocalEndDate:yyyyMMdd}.{extension}"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var exporter =
                _services.GetRequiredService<EnergyReportExportService>();
            var report = exporter.Build(request);

            if (format == "xlsx")
            {
                exporter.ExportExcel(dialog.FileName, report);
            }
            else
            {
                exporter.ExportPdf(dialog.FileName, report);
            }

            ReportStatusText.Text = string.Format(
                _localization.GetString("Reports.ExportSaved"),
                dialog.FileName);
        }
        catch (Exception ex)
        {
            ReportStatusText.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                _localization.GetString("Page.Reports.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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

        button.IsEnabled = false;
        HistorySyncProgressLabel.Visibility = Visibility.Visible;
        HistorySyncProgressBar.Visibility = Visibility.Visible;
        HistorySyncProgressText.Visibility = Visibility.Visible;
        StopHistorySyncButton.Visibility = Visibility.Visible;
        StopHistorySyncButton.IsEnabled = true;
        HistorySyncProgressBar.Minimum = 0;
        HistorySyncProgressBar.Maximum = 1;
        HistorySyncProgressBar.Value = 0;

        LastUpdatedText.Text = _localization.CurrentLanguage == "es"
            ? "Actualizando..."
            : "Updating...";

        var progress = new Progress<HistorySyncProgress>(p =>
        {
            HistorySyncProgressBar.Maximum = Math.Max(1, p.TotalDays);
            HistorySyncProgressBar.Value = Math.Min(p.CompletedDays, p.TotalDays);
            HistorySyncProgressText.Text =
                $"{p.CompletedDays}/{p.TotalDays} · {p.Frames} frames · {p.Samples} muestras\n{p.Message}";
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
                        ? $"\nNo se pudo actualizar el contexto actual: {snapshotError.Message}"
                        : $"\nCurrent context could not be refreshed: {snapshotError.Message}";
            }

            var result = await ingestion.SyncAsync(
                profile,
                history.GetSampleCount(profile.DeviceId) == 0,
                progress,
                _syncCancellation.Token,
                GetRequestedCaptureStartDate());

            HistorySyncProgressBar.Maximum = Math.Max(1, result.DaysAttempted);
            HistorySyncProgressBar.Value = Math.Min(result.DaysCompleted, result.DaysAttempted);

            if (result.Cancelled)
            {
                LastUpdatedText.Text = _localization.CurrentLanguage == "es"
                    ? "Sincronización detenida; los datos descargados quedaron guardados"
                    : "Synchronization stopped; downloaded data was kept";

                HistorySyncProgressText.Text =
                    $"{result.DaysCompleted}/{result.DaysAttempted} · {result.Frames} frames · {result.SamplesUpserted} muestras";
            }
            else
            {
                LastUpdatedText.Text =
                    $"{result.DaysCompleted}/{result.DaysAttempted} días · {result.Frames} frames · {result.SamplesUpserted} muestras";
            }
        }
        catch (Exception ex)
        {
            LastUpdatedText.Text = _localization.CurrentLanguage == "es"
                ? "Sincronización detenida por seguridad/error"
                : "Synchronization stopped for safety/error";

            HistorySyncProgressText.Text = ex.Message;
            MessageBox.Show(ex.Message, _localization.GetString("UpdateDialog.Title"));
        }
        finally
        {
            try
            {
                if (history.GetSampleCount(profile.DeviceId) > 0)
                {
                    HistorySyncProgressText.Text +=
                        _localization.CurrentLanguage == "es"
                            ? "\nNormalizando corpus local..."
                            : "\nNormalizing local corpus...";

                    var normalizer = _services.GetRequiredService<NormalizationService>();
                    await normalizer.RebuildAsync(profile);

                    HistorySyncProgressText.Text +=
                        _localization.CurrentLanguage == "es"
                            ? "\nInterpretando comportamiento local..."
                            : "\nInterpreting local household behavior...";

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
                HistorySyncProgressText.Text +=
                    $"\nNormalization: {normalizationError.Message}";
            }

            StopHistorySyncButton.IsEnabled = false;
            StopHistorySyncButton.Visibility = Visibility.Collapsed;
            button.IsEnabled = true;

            _syncCancellation.Dispose();
            _syncCancellation = null;
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
            try { await RefreshCurrentStateAsync(profile, showError: true); }
            finally { button.IsEnabled = true; }
        }
    }

    private async Task<bool> RefreshCurrentStateAsync(CommissioningProfile profile, bool showError)
    {
        try
        {
            var snapshot = _services.GetRequiredService<CurrentStateSnapshotService>();
            var refreshed = await snapshot.RefreshAsync(profile);
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
            if (showError) MessageBox.Show(ex.Message, _localization.GetString("UpdateDialog.Title"));
            return false;
        }
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

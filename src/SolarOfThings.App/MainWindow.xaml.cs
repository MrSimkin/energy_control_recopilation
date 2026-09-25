using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Normalization;
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

        DatabasePathText.Text = _paths.DatabasePath;

        _suppressLanguageSelection = true;
        LanguageSelector.SelectedValue = _localization.CurrentLanguage;
        _suppressLanguageSelection = false;

        CaptureStartModeSelector.SelectedValue = "auto";

        _suppressAnalysisRangeSelection = true;
        AnalysisRangePresetSelector.SelectedValue = "all";
        _suppressAnalysisRangeSelection = false;

        AnalysisAggregationSelector.SelectedValue = "Day";
        RefreshConnectionStatus();
        RefreshCaptureStartOptions();
        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshDataCoverageView();
        RefreshAnalysisView(initializeRange: true);
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
        var metrics = repository.GetLatestMetrics(profile.DeviceId);

        SetPowerMetric(metrics, "pv_power_w", PvPowerValueText, PvPowerMetaText);
        SetPowerMetric(metrics, "house_load_power_w", HouseLoadValueText, HouseLoadMetaText);
        SetSocMetric(metrics, "battery_soc_pct", BatterySocValueText, BatterySocMetaText);
        SetPowerMetric(metrics, "grid_import_power_w", GridImportValueText, GridImportMetaText);
        RefreshOperatingState(metrics);
        RefreshDashboardFreshness(metrics);
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
        var policy = _services.GetRequiredService<InstallationContextPolicyService>().Current;

        BatteryConfiguredCapacityText.Text =
            $"{configuration.UsableCapacityKwh:F3} kWh";

        if (!metrics.TryGetValue("battery_soc_pct", out var soc))
        {
            ResetBatteryView(keepCapacity: true);
            return;
        }

        var clampedSoc = Math.Clamp(soc.Value, 0, 100);
        var storedEnergy =
            configuration.UsableCapacityKwh * clampedSoc / 100.0;
        var ordinaryEnergy =
            configuration.UsableCapacityKwh *
            Math.Max(clampedSoc - policy.NormalGridTransferSocPercent, 0) /
            100.0;

        var emergencyReserveWidth =
            Math.Max(
                policy.NormalGridTransferSocPercent -
                policy.EmergencyFloorSocPercent,
                0);

        var emergencySocRemaining =
            Math.Min(
                emergencyReserveWidth,
                Math.Max(
                    clampedSoc - policy.EmergencyFloorSocPercent,
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

    private void ResetBatteryView(bool keepCapacity = false)
    {
        BatteryPageChargeText.Text = "— %";
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
        if (IsInitialized)
        {
            RefreshAnalysisView();
        }
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

        AnalysisAggregationGrid.ItemsSource = table.Rows;
        RefreshAnalysisEnergyChart(table.Rows);
        RefreshAnalysisBatteryChart(table.Rows);
    }

    private void RefreshAnalysisEnergyChart(
        IReadOnlyList<EnergyAggregationRow> rows)
    {
        var plot = AnalysisEnergyPlot.Plot;
        plot.Clear();

        if (rows.Count == 0)
        {
            AnalysisChartStatusText.Text =
                _localization.GetString("Analysis.Chart.NoData");
            AnalysisEnergyPlot.Refresh();
            return;
        }

        AnalysisChartStatusText.Text = string.Empty;

        var positions =
            Enumerable.Range(0, rows.Count)
                .Select(index => (double)index)
                .ToArray();

        var solarPositions =
            positions.Select(position => position - 0.26).ToArray();
        var housePositions = positions;
        var gridPositions =
            positions.Select(position => position + 0.26).ToArray();

        var solar = plot.Add.Bars(
            solarPositions,
            rows.Select(row => row.PvEnergyKwh).ToArray());
        solar.LegendText =
            _localization.GetString("Analysis.Chart.Solar");

        var house = plot.Add.Bars(
            housePositions,
            rows.Select(row => row.HouseEnergyKwh).ToArray());
        house.LegendText =
            _localization.GetString("Analysis.Chart.House");

        var grid = plot.Add.Bars(
            gridPositions,
            rows.Select(row => row.GridImportEnergyKwh).ToArray());
        grid.LegendText =
            _localization.GetString("Analysis.Chart.Grid");

        foreach (var bar in solar.Bars)
        {
            bar.Size = 0.22;
        }

        foreach (var bar in house.Bars)
        {
            bar.Size = 0.22;
        }

        foreach (var bar in grid.Bars)
        {
            bar.Size = 0.22;
        }

        var tickGenerator =
            new ScottPlot.TickGenerators.NumericManual();

        var tickStep =
            Math.Max(
                1,
                (int)Math.Ceiling(rows.Count / 12.0));

        for (var index = 0; index < rows.Count; index += tickStep)
        {
            tickGenerator.AddMajor(
                index,
                rows[index].LocalLabel);
        }

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

        var averagePoints = rows
            .Select((row, index) => new
            {
                X = (double)index,
                Value = row.SocAveragePercent
            })
            .Where(point => point.Value.HasValue)
            .ToArray();

        var endingPoints = rows
            .Select((row, index) => new
            {
                X = (double)index,
                Value = row.SocEndingPercent
            })
            .Where(point => point.Value.HasValue)
            .ToArray();

        if (averagePoints.Length == 0 &&
            endingPoints.Length == 0)
        {
            AnalysisBatteryChartStatusText.Text =
                _localization.GetString("Analysis.BatteryChart.NoData");
            AnalysisBatteryPlot.Refresh();
            return;
        }

        AnalysisBatteryChartStatusText.Text = string.Empty;

        if (averagePoints.Length > 0)
        {
            var average = plot.Add.Scatter(
                averagePoints.Select(point => point.X).ToArray(),
                averagePoints.Select(point => point.Value!.Value).ToArray());
            average.LegendText =
                _localization.GetString("Analysis.BatteryChart.Average");
            average.LineWidth = 2;
            average.MarkerSize = 5;
        }

        if (endingPoints.Length > 0)
        {
            var ending = plot.Add.Scatter(
                endingPoints.Select(point => point.X).ToArray(),
                endingPoints.Select(point => point.Value!.Value).ToArray());
            ending.LegendText =
                _localization.GetString("Analysis.BatteryChart.End");
            ending.LineWidth = 2;
            ending.MarkerSize = 5;
        }

        var tickGenerator =
            new ScottPlot.TickGenerators.NumericManual();

        var tickStep =
            Math.Max(
                1,
                (int)Math.Ceiling(rows.Count / 12.0));

        for (var index = 0; index < rows.Count; index += tickStep)
        {
            tickGenerator.AddMajor(
                index,
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
        AnalysisAggregationGrid.ItemsSource = null;
        AnalysisEnergyPlot.Plot.Clear();
        AnalysisEnergyPlot.Refresh();
        AnalysisChartStatusText.Text =
            _localization.GetString("Analysis.Chart.NoData");

        AnalysisBatteryPlot.Plot.Clear();
        AnalysisBatteryPlot.Refresh();
        AnalysisBatteryChartStatusText.Text =
            _localization.GetString("Analysis.BatteryChart.NoData");
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
        var isData = string.Equals(pageKey, "Data", StringComparison.Ordinal);

        DashboardContent.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;
        AnalysisContent.Visibility = isAnalysis ? Visibility.Visible : Visibility.Collapsed;
        BatteryContent.Visibility = isBattery ? Visibility.Visible : Visibility.Collapsed;
        DataContent.Visibility = isData ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility =
            !isDashboard && !isAnalysis && !isBattery && !isData
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
            pageKey is "Settings" or "Diagnostics"
                ? Visibility.Visible
                : Visibility.Collapsed;
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

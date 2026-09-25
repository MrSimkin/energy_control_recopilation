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
        RefreshConnectionStatus();
        RefreshCaptureStartOptions();
        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshDataCoverageView();
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
            if (!normalized.HasRuleVersion(
                    profile.DeviceId,
                    NormalizationService.RuleVersion))
            {
                var normalizer = _services.GetRequiredService<NormalizationService>();
                await normalizer.RebuildAsync(profile);
            }

            EvaluateInstallationHealth(profile);
        }
        catch
        {
            // Detailed normalization/configuration failures are kept in local diagnostics.
            // User-facing views remain conservative rather than guessing.
        }

        RefreshDashboardMetrics();
        RefreshBatteryView();
        RefreshDataCoverageView();
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
            configuration.UsableCapacityKwh * Math.Max(clampedSoc - 20.0, 0) / 100.0;
        var emergencySocRemaining =
            Math.Min(10.0, Math.Max(clampedSoc - 10.0, 0));
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
        var isBattery = string.Equals(pageKey, "Battery", StringComparison.Ordinal);
        var isData = string.Equals(pageKey, "Data", StringComparison.Ordinal);

        DashboardContent.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;
        BatteryContent.Visibility = isBattery ? Visibility.Visible : Visibility.Collapsed;
        DataContent.Visibility = isData ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility =
            !isDashboard && !isBattery && !isData
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (isBattery)
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
                    EvaluateInstallationHealth(profile);
                    RefreshDashboardMetrics();
                    RefreshBatteryView();
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

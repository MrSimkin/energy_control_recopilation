using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.History;
using SolarOfThings.Core.SolarOfThings;

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

        RefreshConnectionStatus();
        ShowPage("Dashboard");
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
        DashboardContent.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility = isDashboard ? Visibility.Collapsed : Visibility.Visible;

        if (!isDashboard)
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
            var result = await ingestion.SyncAsync(
                profile,
                history.GetSampleCount(profile.DeviceId) == 0,
                progress,
                _syncCancellation.Token);

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
            StopHistorySyncButton.IsEnabled = false;
            StopHistorySyncButton.Visibility = Visibility.Collapsed;
            button.IsEnabled = true;

            _syncCancellation.Dispose();
            _syncCancellation = null;
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

        RefreshConnectionStatus();
    }

    private void RefreshConnectionStatus()
    {
        var profile = _profiles.Get();

        if (profile is not null)
        {
            SolarStatusText.Text =
                $"{_localization.GetString("Status.Commissioned")}: {profile.DeviceName ?? profile.DeviceId}";
            SolarStatusText.Foreground = Brushes.Green;
            return;
        }

        if (_session.HasSession)
        {
            SolarStatusText.SetResourceReference(TextBlock.TextProperty, "Status.SessionReady");
            SolarStatusText.Foreground = Brushes.DarkOrange;
            return;
        }

        SolarStatusText.SetResourceReference(TextBlock.TextProperty, "Status.NotConnected");
        SolarStatusText.Foreground = Brushes.Red;
    }
}

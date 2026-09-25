using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.App;

public partial class MainWindow : Window
{
    private static readonly Brush ActiveNavigationBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x7B, 0xFF));

    private readonly AppPaths _paths;
    private readonly LocalizationService _localization;
    private bool _suppressLanguageSelection;

    public MainWindow(AppPaths paths, LocalizationService localization)
    {
        _paths = paths;
        _localization = localization;

        InitializeComponent();

        DatabasePathText.Text = _paths.DatabasePath;

        _suppressLanguageSelection = true;
        LanguageSelector.SelectedValue = _localization.CurrentLanguage;
        _suppressLanguageSelection = false;

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
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageSelection || LanguageSelector.SelectedValue is not string language)
        {
            return;
        }

        _localization.SetLanguage(language);
    }

    private void UpdateData_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            _localization.GetString("UpdateDialog.Message"),
            _localization.GetString("UpdateDialog.Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

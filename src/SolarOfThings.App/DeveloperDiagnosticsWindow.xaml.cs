using System.Diagnostics;
using System.Windows;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.App;

public partial class DeveloperDiagnosticsWindow : Window
{
    private readonly ApiDiagnosticsStore _diagnostics;
    private readonly AppPaths _paths;

    public DeveloperDiagnosticsWindow(
        ApiDiagnosticsStore diagnostics,
        AppPaths paths)
    {
        _diagnostics = diagnostics;
        _paths = paths;

        InitializeComponent();
        RefreshReport();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshReport();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ReportTextBox.Text);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var path = _diagnostics.SaveSanitizedReport();
        MessageBox.Show(
            $"Informe guardado en:\n{path}",
            "Diagnóstico",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.LogDirectory,
            UseShellExecute = true
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshReport()
    {
        ReportTextBox.Text = _diagnostics.BuildSanitizedReport();
    }
}

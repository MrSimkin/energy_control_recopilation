using System.Diagnostics;
using System.Windows;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Backup;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.App;

public partial class DeveloperDiagnosticsWindow : Window
{
    private readonly ApiDiagnosticsStore _diagnostics;
    private readonly InvestigationDiagnosticsService _investigation;
    private readonly AppPaths _paths;
    private readonly FullBackupService _backup;
    private readonly PhaseDiagnosticsExportService _phaseEvidence;
    private bool _busy;
    private long? _selectedBillId;

    public void SetSelectedBillId(long? billId) =>
        _selectedBillId = billId;

    public DeveloperDiagnosticsWindow(
        ApiDiagnosticsStore diagnostics,
        InvestigationDiagnosticsService investigation,
        AppPaths paths,
        FullBackupService backup,
        PhaseDiagnosticsExportService phaseEvidence)
    {
        _diagnostics = diagnostics;
        _investigation = investigation;
        _paths = paths;
        _backup = backup;
        _phaseEvidence = phaseEvidence;

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
        ShowSaved(path);
    }

    private async void RunAll_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(
            async () =>
            {
                var result = await _investigation.RunCompleteAsync();
                var summary = string.Join(
                    Environment.NewLine,
                    result.Actions.Select(action =>
                        $"{action.Action}: {action.Outcome} — {action.Detail}"));

                ActionStatusText.Text =
                    summary + Environment.NewLine +
                    $"Paquete: {result.BundlePath}";

                ShowSaved(result.BundlePath);
            });
    }

    private async void CaptureState_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(() => _investigation.CaptureLatestStateAsync());

    private async void CaptureFlow_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(() => _investigation.CaptureEnergyFlowAsync());

    private async void ConfigCache_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(() => _investigation.CaptureConfigCacheAsync());

    private async void ConfigRead_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(() => _investigation.CaptureDirectConfigReadAsync());

    private async void GridEnergy_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(() => _investigation.CaptureGridImportEnergyEvidenceAsync());

    private async Task RunActionAsync(
        Func<Task<InvestigationActionResult>> action)
    {
        await RunAsync(
            async () =>
            {
                var result = await action();
                ActionStatusText.Text =
                    $"{result.Action}: {result.Outcome} — {result.Detail}";
            });
    }

    private async void PhaseEvidence_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            ActionStatusText.Text = "Paso 1/2: revisando esquema y metadatos...";
            var path = await Task.Run(
                () => _phaseEvidence.Export(_selectedBillId));
            ActionStatusText.Text = $"Paso 2/2: paquete guardado en {path}";
            System.Media.SystemSounds.Asterisk.Play();
        });
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            ActionStatusText.Text = "Creando respaldo COMPLETO (SQLite, boletas y tarifas)...";
            var result = await Task.Run(() =>
                _backup.Create(ProductInfo.ProductVersion, ProductInfo.BuildNumber, ProductInfo.SourceRevision));
            ActionStatusText.Text =
                $"Respaldo completo verificado ({result.FileCount} archivos), integridad {result.IntegrityStatus}; " +
                $"SHA-256 {result.Sha256}; guardado en {result.Path}";
            System.Media.SystemSounds.Asterisk.Play();
        });
    }

    private async void ExportBundle_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(
            async () =>
            {
                ActionStatusText.Text = "Creando paquete de investigación...";
                var path = await Task.Run(
                    () => _investigation.SaveInvestigationBundle());
                ActionStatusText.Text = $"Paquete guardado: {path}";
                ShowSaved(path);
            });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);

        try
        {
            ActionStatusText.Text = "Ejecutando diagnóstico read-only...";
            await action();
            RefreshReport();
        }
        catch (Exception ex)
        {
            ActionStatusText.Text = $"ERROR — {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        RunAllButton.IsEnabled = !busy;
        CaptureStateButton.IsEnabled = !busy;
        CaptureFlowButton.IsEnabled = !busy;
        ConfigCacheButton.IsEnabled = !busy;
        ConfigReadButton.IsEnabled = !busy;
        GridEnergyButton.IsEnabled = !busy;
        ExportBundleButton.IsEnabled = !busy;
        BackupButton.IsEnabled = !busy;
        PhaseEvidenceButton.IsEnabled = !busy;
        DiagnosticsBusyBar.Visibility =
            busy ? Visibility.Visible : Visibility.Collapsed;
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
        try
        {
            ReportTextBox.Text =
                _investigation.BuildOverview() +
                Environment.NewLine + Environment.NewLine +
                "RECENT SANITIZED API EVENTS" +
                Environment.NewLine +
                "===========================" +
                Environment.NewLine +
                _diagnostics.BuildSanitizedReport(80);
        }
        catch (Exception ex)
        {
            ReportTextBox.Text =
                "No se pudo construir el resumen de investigación." +
                Environment.NewLine +
                ex.Message +
                Environment.NewLine + Environment.NewLine +
                _diagnostics.BuildSanitizedReport(80);
        }
    }

    private static void ShowSaved(string path)
    {
        MessageBox.Show(
            $"Archivo guardado en:\n{path}",
            "Diagnóstico",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

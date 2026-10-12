using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SolarOfThings.Core.SqlExplorer;

namespace SolarOfThings.App;

public partial class MainWindow
{
    private SqlIntegratedEnergyTable? _sqlLastEnergyTable;
    private bool _sqlShowingIntegratedEnergy;
    private bool _sqlEnergyOperationRunning;

    // The SQL preview and integrated-energy table share a WPF DataGrid.
    // Clear the COMPLETE result identity; never leave a previous device,
    // statement or station-local period visible after invalidation.
    private void ClearSqlExplorerDisplayedResult()
    {
        _sqlLastEnergyTable = null;
        _sqlShowingIntegratedEnergy = false;
        _sqlLastPreviewSql = null;
        _sqlPreviewOffset = 0;
        _sqlPreviewHasMore = false;
        if (SqlExplorerResultGrid is null) return;
        SqlExplorerResultGrid.ItemsSource = null;
        SqlExplorerResultTitle.Text = string.Empty;
        SqlExplorerPageText.Text = string.Empty;
        SqlPreviousPageButton.IsEnabled = false;
        SqlNextPageButton.IsEnabled = false;
        SqlEnergyExportCsvButton.IsEnabled = false;
        SqlEnergyExportXlsxButton.IsEnabled = false;
    }

    private SqlIntegratedEnergyRequest? CurrentSqlEnergyRequest()
    {
        var profile = _profiles.Get();
        if (profile is null ||
            !SqlEnergyFromDatePicker.SelectedDate.HasValue ||
            !SqlEnergyToDatePicker.SelectedDate.HasValue)
            return null;

        return new SqlIntegratedEnergyRequest(
            profile.DeviceId,
            string.IsNullOrWhiteSpace(profile.StationTimeZone)
                ? "America/Santiago" : profile.StationTimeZone,
            DateOnly.FromDateTime(SqlEnergyFromDatePicker.SelectedDate.Value),
            DateOnly.FromDateTime(SqlEnergyToDatePicker.SelectedDate.Value));
    }

    private void InvalidateSqlEnergyResult()
    {
        // Date changes during an energy read/export must stop obsolete work.
        // The exporter's temporary file is removed on cancellation.
        if (_sqlEnergyOperationRunning) _sqlRunning?.Cancel();
        if (_sqlShowingIntegratedEnergy)
        {
            ClearSqlExplorerDisplayedResult();
            SqlExplorerStatusText.Text =
                _localization.GetString("SqlExplorer.EnergyRequeryRequired");
            return;
        }
        _sqlLastEnergyTable = null;
        if (SqlEnergyExportCsvButton is null) return;
        SqlEnergyExportCsvButton.IsEnabled = false;
        SqlEnergyExportXlsxButton.IsEnabled = false;
    }

    private void SqlEnergyDateChanged(object sender,
        SelectionChangedEventArgs e) => InvalidateSqlEnergyResult();

    private async void SqlEnergyQuery_Click(object sender, RoutedEventArgs e)
    {
        if (_sqlExplorerBusy) return;
        var request = CurrentSqlEnergyRequest();
        if (request is null)
        {
            SqlExplorerStatusText.Text =
                _localization.GetString("SqlExplorer.EnergySelectionRequired");
            return;
        }

        ClearSqlExplorerDisplayedResult();
        SetSqlExplorerBusy(true);
        using var cancel = new CancellationTokenSource();
        _sqlRunning = cancel;
        _sqlEnergyOperationRunning = true;
        SqlExplorerStatusText.Text =
            _localization.GetString("SqlExplorer.EnergyQueryLoading");
        SqlExplorerDiagnosticText.Text = "";
        SqlExplorerPerformanceText.Text = "";
        try
        {
            var result = await new SqlIntegratedEnergyQueryService(
                _paths.DatabasePath).QueryAsync(request, cancel.Token);
            if (!SqlExplorerResultPolicy.CanShowIntegratedEnergy(
                    request, CurrentSqlEnergyRequest(),
                    SqlExplorerContent.Visibility == Visibility.Visible,
                    _windowClosed, cancel.IsCancellationRequested))
                return;

            var grid = new DataTable();
            foreach (var column in result.Columns)
                grid.Columns.Add(column, typeof(string));
            foreach (var row in result.Rows)
                grid.Rows.Add(row.Select(cell =>
                    cell.IsNull ? (object)DBNull.Value : cell.Text ?? "").ToArray());

            _sqlLastPreviewSql = null;
            _sqlPreviewOffset = 0;
            _sqlPreviewHasMore = false;
            SqlExplorerResultGrid.ItemsSource = grid.DefaultView;
            SqlExplorerResultTitle.Text =
                _localization.GetString("SqlExplorer.EnergyResultTitle");
            SqlExplorerPageText.Text = string.Format(
                _localization.GetString("SqlExplorer.EnergyResultCount"),
                result.Rows.Count);
            SqlExplorerStatusText.Text =
                _localization.GetString("SqlExplorer.EnergyQueryComplete");
            _sqlLastEnergyTable = result;
            _sqlShowingIntegratedEnergy = true;
        }
        catch (OperationCanceledException)
        {
            SqlExplorerStatusText.Text = _localization.GetString(
                "SqlExplorer.EnergyQueryCancelled");
        }
        catch (Exception ex)
        {
            SqlExplorerStatusText.Text = _localization.GetString(
                "SqlExplorer.EnergyQueryFailed");
            SqlExplorerDiagnosticText.Text = ex.Message;
        }
        finally
        {
            _sqlEnergyOperationRunning = false;
            _sqlRunning = null;
            SetSqlExplorerBusy(false);
        }
    }

    private async void SqlEnergyExportCsv_Click(object sender, RoutedEventArgs e) =>
        await ExportSqlIntegratedEnergyAsync(SqlExportFormat.Csv);
    private async void SqlEnergyExportXlsx_Click(object sender, RoutedEventArgs e) =>
        await ExportSqlIntegratedEnergyAsync(SqlExportFormat.Xlsx);

    private async Task ExportSqlIntegratedEnergyAsync(SqlExportFormat format)
    {
        if (_sqlExplorerBusy || _sqlLastEnergyTable is null) return;
        var table = _sqlLastEnergyTable;
        if (_windowClosed || table.Request != CurrentSqlEnergyRequest())
        {
            InvalidateSqlEnergyResult();
            SqlExplorerStatusText.Text =
                _localization.GetString("SqlExplorer.EnergyRequeryRequired");
            return;
        }
        var excel = format == SqlExportFormat.Xlsx;
        var chooser = new SaveFileDialog
        {
            Filter = excel ? "Excel Workbook (*.xlsx)|*.xlsx" : "CSV UTF-8 (*.csv)|*.csv",
            DefaultExt = excel ? ".xlsx" : ".csv",
            AddExtension = true,
            OverwritePrompt = false,
            FileName = excel ? "energia-integrada-diaria.xlsx" : "energia-integrada-diaria.csv"
        };
        if (chooser.ShowDialog(this) != true) return;
        SetSqlExplorerBusy(true);
        using var cancel = new CancellationTokenSource();
        _sqlRunning = cancel;
        _sqlEnergyOperationRunning = true;
        SqlExplorerStatusText.Text = _localization.GetString(
            "SqlExplorer.EnergyExportLoading");
        SqlExplorerDiagnosticText.Text = "";
        try
        {
            var receipt = await new SqlIntegratedEnergyQueryService(
                _paths.DatabasePath).ExportAsync(table, chooser.FileName, format,
                    cancel.Token);
            SqlExplorerStatusText.Text = string.Format(
                _localization.GetString("SqlExplorer.EnergyExportComplete"),
                receipt.Rows, receipt.Path);
        }
        catch (OperationCanceledException)
        {
            SqlExplorerStatusText.Text = _localization.GetString(
                "SqlExplorer.EnergyQueryCancelled");
        }
        catch (Exception ex)
        {
            SqlExplorerStatusText.Text = _localization.GetString(
                "SqlExplorer.EnergyQueryFailed");
            SqlExplorerDiagnosticText.Text = ex.Message;
        }
        finally
        {
            _sqlEnergyOperationRunning = false;
            _sqlRunning = null;
            SetSqlExplorerBusy(false);
        }
    }
}

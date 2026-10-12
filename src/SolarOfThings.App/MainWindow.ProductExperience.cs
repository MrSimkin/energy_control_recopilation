using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SolarOfThings.Core.Help;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.App;

public partial class MainWindow
{
    private sealed record HelpSectionChoice(string Id, string Title);

    private void InitializeProductExperience()
    {
        VersionBuildText.Text = ProductInfo.Display;
        AboutVersionText.Text = ProductInfo.Display;
        RefreshProductExperienceLocalization();
        SetGlobalOperation(false, string.Empty);
    }

    private void RefreshProductExperienceLocalization()
    {
        if (!IsInitialized ||
            HelpSectionSelector is null ||
            HelpSectionBodyText is null ||
            AboutVersionText is null)
        {
            return;
        }

        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

        var previous = HelpSectionSelector.SelectedValue?.ToString() ?? "overview";
        HelpSectionSelector.ItemsSource = HelpManualCatalog.Sections
            .Select(section => new HelpSectionChoice(
                section.Id,
                section.Title(spanish)))
            .ToArray();

        HelpSectionSelector.SelectedValue =
            HelpManualCatalog.Sections.Any(section => section.Id == previous)
                ? previous
                : "overview";

        AboutVersionText.Text = ProductInfo.Display;
        RefreshSelectedHelpSection();
    }

    private void HelpSectionSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        RefreshSelectedHelpSection();
    }

    private void RefreshSelectedHelpSection()
    {
        if (HelpSectionBodyText is null)
            return;

        var id = HelpSectionSelector.SelectedValue?.ToString() ?? "overview";
        var section = HelpManualCatalog.Sections
            .FirstOrDefault(item => item.Id == id)
            ?? HelpManualCatalog.Sections[0];

        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

        HelpSectionBodyText.Text = section.Body(spanish);
    }

    private void AboutWhatsNew_Click(
        object sender,
        RoutedEventArgs e)
    {
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

        var text = new TextBox
        {
            Text = ProductInfo.ReleaseNotes(spanish),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(18),
            FontSize = 13,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var window = new Window
        {
            Owner = this,
            Title = spanish ? "Qué hay de nuevo" : "What's new",
            Width = 760,
            Height = 600,
            MinWidth = 620,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = text
        };

        window.ShowDialog();
    }

    private async void HelpExportSection_Click(
        object sender,
        RoutedEventArgs e)
    {
        var sectionId =
            HelpSectionSelector.SelectedValue?.ToString() ?? "overview";
        await ExportHelpManualAsync(sectionId);
    }

    private async void HelpExportAll_Click(
        object sender,
        RoutedEventArgs e)
    {
        await ExportHelpManualAsync(null);
    }

    private async Task ExportHelpManualAsync(string? sectionId)
    {
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

        var dialog = new SaveFileDialog
        {
            Title = spanish ? "Exportar ayuda a PDF" : "Export help to PDF",
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            FileName = sectionId is null
                ? $"Manual-SolarEnergyMonitor-{DateTime.Now:yyyyMMdd}.pdf"
                : $"Ayuda-SolarEnergyMonitor-{sectionId}-{DateTime.Now:yyyyMMdd}.pdf"
        };

        PrepareExportDialog(dialog);

        if (dialog.ShowDialog(this) != true)
            return;

        HelpExportStatusText.Text = string.Empty;
        SetGlobalOperation(
            true,
            spanish ? "Exportando ayuda a PDF..." : "Exporting help to PDF...");

        try
        {
            var exporter =
                _services.GetRequiredService<HelpManualExportService>();

            await Task.Run(() =>
                exporter.ExportPdf(
                    dialog.FileName,
                    _localization.CurrentLanguage,
                    sectionId,
                    ProductInfo.Display));

            HelpExportStatusText.Text = spanish
                ? $"PDF guardado: {dialog.FileName}"
                : $"PDF saved: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            HelpExportStatusText.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                spanish ? "Ayuda" : "Help",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetGlobalOperation(false, string.Empty);
        }
    }

    private async void DataImportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var spanish = _localization.CurrentLanguage.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

        var dialog = new OpenFolderDialog
        {
            Title = spanish
                ? "Seleccione la instalación anterior o su carpeta Data"
                : "Select the previous installation or its Data folder",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var confirm = MessageBox.Show(
            spanish
                ? "La importación copiará los datos de la instalación seleccionada, conservará un respaldo de los datos actuales y reiniciará la aplicación. La instalación de origen debería estar cerrada. ¿Continuar?"
                : "Import will copy data from the selected installation, keep a backup of current data and restart the application. The source installation should be closed. Continue?",
            spanish ? "Importar datos" : "Import data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        DataImportButton.IsEnabled = false;
        DataImportProgressBar.Visibility = Visibility.Visible;
        DataImportProgressBar.IsIndeterminate = false;
        DataImportProgressBar.Minimum = 0;
        DataImportProgressBar.Maximum = 100;
        DataImportProgressBar.Value = 0;
        DataImportStatusText.Text = spanish
            ? "Preparando importación..."
            : "Preparing import...";
        SetGlobalOperation(
            true,
            spanish ? "Importando datos..." : "Importing data...");

        var progress = new Progress<DataImportProgress>(item =>
        {
            var percent = item.TotalBytes > 0
                ? Math.Clamp(item.BytesCompleted * 100.0 / item.TotalBytes, 0, 100)
                : 0;

            DataImportProgressBar.Value = percent;
            DataImportStatusText.Text = spanish
                ? $"{item.FilesCompleted}/{item.TotalFiles} archivos · {percent:N1}% · {item.CurrentFile}"
                : $"{item.FilesCompleted}/{item.TotalFiles} files · {percent:N1}% · {item.CurrentFile}";
        });

        try
        {
            await _services
                .GetRequiredService<DataImportService>()
                .StageImportAsync(
                    dialog.FolderName,
                    progress);

            DataImportProgressBar.Value = 100;
            DataImportStatusText.Text = spanish
                ? "Importación preparada. Se reiniciará la aplicación para aplicar los datos antes de abrir SQLite."
                : "Import prepared. The application will restart to apply the data before SQLite opens.";

            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new InvalidOperationException(
                    "The current executable path could not be determined.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--apply-import",
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            DataImportStatusText.Text = ex.Message;
            MessageBox.Show(
                ex.Message,
                spanish ? "Importar datos" : "Import data",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            DataImportButton.IsEnabled = true;
            SetGlobalOperation(false, string.Empty);
        }
    }

    private void SetGlobalOperation(
        bool busy,
        string message)
    {
        if (GlobalOperationProgressBar is null ||
            GlobalOperationStatusText is null)
        {
            return;
        }

        GlobalOperationProgressBar.Visibility =
            busy ? Visibility.Visible : Visibility.Collapsed;
        GlobalOperationProgressBar.IsIndeterminate = busy;
        GlobalOperationStatusText.Text = message;
        Mouse.OverrideCursor = busy ? Cursors.Wait : null;
    }
}

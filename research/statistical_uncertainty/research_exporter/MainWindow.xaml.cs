using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Media;
using System.Windows;
using Microsoft.Win32;

namespace SolarOfThings.ResearchExporter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DatabasePathTextBox.Text = DetectDatabasePath() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(DatabasePathTextBox.Text))
        {
            OutputFolderTextBox.Text =
                Path.GetDirectoryName(DatabasePathTextBox.Text) ??
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }
        else
        {
            OutputFolderTextBox.Text =
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }
    }

    private void BrowseDatabase_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar energy.db",
            Filter = "SQLite energy.db|energy.db|SQLite (*.db)|*.db|Todos los archivos|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            DatabasePathTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text))
                OutputFolderTextBox.Text = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta de salida",
            Multiselect = false
        };

        if (Directory.Exists(OutputFolderTextBox.Text))
            dialog.InitialDirectory = OutputFolderTextBox.Text;

        if (dialog.ShowDialog(this) == true)
            OutputFolderTextBox.Text = dialog.FolderName;
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        var databasePath = DatabasePathTextBox.Text.Trim();
        var outputFolder = OutputFolderTextBox.Text.Trim();

        if (!File.Exists(databasePath))
        {
            MessageBox.Show(
                this,
                "No se encontró la base de datos indicada.",
                "Research Exporter",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            MessageBox.Show(
                this,
                "Selecciona una carpeta de salida.",
                "Research Exporter",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(outputFolder);

        GenerateButton.IsEnabled = false;
        ProgressLabel.Visibility = Visibility.Visible;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        StatusText.Text = string.Empty;

        var progress = new Progress<ExportProgress>(item =>
        {
            ProgressBar.Value = item.Percent;
            ProgressLabel.Text = $"Paso {item.Step}/4 · {item.Message}";
        });

        try
        {
            var result = await Task.Run(() =>
                ResearchPackageExporter.Export(
                    databasePath,
                    outputFolder,
                    progress));

            ProgressBar.Value = 100;
            ProgressLabel.Text = "Paso 4/4 · Paquete generado";
            StatusText.Text = $"Archivo: {result.ZipPath}";
            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            ProgressBar.Value = 100;
            ProgressLabel.Text = "Exportación fallida";
            StatusText.Text = ex.Message;
            MessageBox.Show(
                this,
                ex.Message,
                "Research Exporter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private static string? DetectDatabasePath()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SolarEnergyMonitor",
                "Data",
                "energy.db"),
            Path.Combine(AppContext.BaseDirectory, "Data", "energy.db"),
            Path.Combine(AppContext.BaseDirectory, "energy.db")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}

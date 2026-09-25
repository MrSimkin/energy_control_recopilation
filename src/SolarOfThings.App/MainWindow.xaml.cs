using System.Windows;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.App;

public partial class MainWindow : Window
{
    private readonly AppPaths _paths;

    public MainWindow(AppPaths paths)
    {
        _paths = paths;
        InitializeComponent();
        DatabasePathText.Text = $"Database: {_paths.DatabasePath}";
    }

    private void UpdateData_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Solar of Things synchronization is introduced in Phase 2.\n\n" +
            "The local database and application shell are active.",
            "Update Data",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SolarOfThings.Core.Utility;

namespace SolarOfThings.App;

/// <summary>
/// Browser-assisted fallback for Enel tariff evidence.
///
/// This intentionally uses a normal Edge/WebView2 browsing session instead of
/// attempting to bypass Enel's browser protection. The user navigates the
/// official site normally; when an official "Tarifas Suministro Eléctrico"
/// PDF download starts, the app captures the downloaded file and routes it
/// through the existing controlled import/hash/normalization pipeline.
/// </summary>
public sealed class EnelTariffBrowserWindow : Window
{
    private readonly EnelTariffPdfImportService _importService;
    private readonly TextBlock _statusText;
    private readonly ProgressBar _busyBar;
    private readonly WebView2 _browser;
    private readonly HashSet<string> _activeDownloads =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? TariffImported;

    public EnelTariffBrowserWindow(
        EnelTariffPdfImportService importService)
    {
        _importService = importService;

        Title = "Enel · captura asistida de tarifas oficiales";
        Width = 1180;
        Height = 820;
        MinWidth = 900;
        MinHeight = 650;
        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;
        Background =
            new SolidColorBrush(
                Color.FromRgb(0xF4, 0xF6, 0xF9));
        FontFamily =
            new FontFamily("Segoe UI");

        var root = new Grid
        {
            Margin = new Thickness(14)
        };
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        var heading = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        heading.Children.Add(
            new TextBlock
            {
                Text = "Captura Enel desde navegador",
                FontSize = 21,
                FontWeight = FontWeights.SemiBold
            });
        heading.Children.Add(
            new TextBlock
            {
                Text =
                    "Esta ventana usa Edge/WebView2 como un navegador normal. " +
                    "Si Enel muestra una verificación, complétala aquí. Luego abre " +
                    "“Tarifas suministro eléctrico” y pulsa Descargar. " +
                    "Los PDFs oficiales compatibles se importarán, hashearán y " +
                    "normalizarán automáticamente.",
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0x6C, 0x75, 0x7D)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var actions = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        var homeButton = new Button
        {
            Content = "Abrir tarifas Enel",
            Padding = new Thickness(12, 6, 12, 6)
        };
        homeButton.Click += (_, _) =>
            NavigateToArchive();

        var refreshButton = new Button
        {
            Content = "Recargar",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(8, 0, 0, 0)
        };
        refreshButton.Click += (_, _) =>
            _browser.Reload();

        var backButton = new Button
        {
            Content = "Atrás",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(8, 0, 0, 0)
        };
        backButton.Click += (_, _) =>
        {
            if (_browser.CanGoBack)
                _browser.GoBack();
        };

        buttonPanel.Children.Add(homeButton);
        buttonPanel.Children.Add(backButton);
        buttonPanel.Children.Add(refreshButton);
        DockPanel.SetDock(buttonPanel, Dock.Left);
        actions.Children.Add(buttonPanel);

        _statusText = new TextBlock
        {
            Text = "Preparando navegador…",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(0x49, 0x50, 0x57)),
            Margin = new Thickness(14, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        actions.Children.Add(_statusText);

        Grid.SetRow(actions, 1);
        root.Children.Add(actions);

        _browser = new WebView2();
        Grid.SetRow(_browser, 2);
        root.Children.Add(_browser);

        var footer = new DockPanel
        {
            Margin = new Thickness(0, 10, 0, 0)
        };

        _busyBar = new ProgressBar
        {
            Width = 140,
            Height = 7,
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_busyBar, Dock.Left);
        footer.Children.Add(_busyBar);

        var closeButton = new Button
        {
            Content = "Cerrar",
            Padding = new Thickness(14, 6, 14, 6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (_, _) =>
            Close();
        DockPanel.SetDock(closeButton, Dock.Right);
        footer.Children.Add(closeButton);

        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _statusText.Text =
                "Inicializando Edge/WebView2…";

            await _browser.EnsureCoreWebView2Async();

            _browser.CoreWebView2.Settings
                .AreDefaultContextMenusEnabled = true;
            _browser.CoreWebView2.Settings
                .AreDevToolsEnabled = false;
            _browser.CoreWebView2.DownloadStarting +=
                CoreWebView2_DownloadStarting;
            _browser.CoreWebView2.NewWindowRequested +=
                CoreWebView2_NewWindowRequested;
            _browser.CoreWebView2.NavigationStarting +=
                CoreWebView2_NavigationStarting;
            _browser.CoreWebView2.NavigationCompleted +=
                (_, args) =>
                {
                    _statusText.Text =
                        args.IsSuccess
                            ? "Navegador listo. Descarga un PDF oficial de “Tarifas Suministro Eléctrico”."
                            : $"La navegación terminó con {args.WebErrorStatus}. Puedes recargar o volver a la página de tarifas.";
                };

            NavigateToArchive();
        }
        catch (Exception ex)
        {
            _statusText.Text =
                "No fue posible iniciar WebView2: " +
                ex.Message;
            MessageBox.Show(
                "No fue posible iniciar el navegador integrado. " +
                "Puedes seguir usando “Abrir página oficial Enel” + " +
                "“Importar PDFs oficiales Enel”.\n\n" +
                ex.Message,
                "Solar Energy Monitor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void NavigateToArchive()
    {
        if (_browser.CoreWebView2 is null)
        {
            _browser.Source =
                new Uri(
                    EnelTariffCaptureService
                        .OfficialArchiveUrl);
            return;
        }

        _browser.CoreWebView2.Navigate(
            EnelTariffCaptureService
                .OfficialArchiveUrl);
    }

    private void CoreWebView2_NewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!Uri.TryCreate(
                e.Uri,
                UriKind.Absolute,
                out var uri) ||
            !IsOfficialEnelNavigation(uri))
        {
            return;
        }

        // Enel often opens tariff PDFs with target=_blank. If that request is
        // allowed to escape to external Edge, the subsequent PDF-viewer
        // download belongs to Edge and this app can no longer observe/import
        // it. Keep official Enel navigation inside this WebView2 session.
        e.Handled = true;
        _statusText.Text =
            "Abriendo publicación oficial dentro del navegador integrado…";
        _browser.CoreWebView2.Navigate(
            uri.AbsoluteUri);
    }

    private void CoreWebView2_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(
                e.Uri,
                UriKind.Absolute,
                out var uri))
        {
            return;
        }

        if (IsOfficialTariffPdfUri(uri))
        {
            _statusText.Text =
                "PDF oficial abierto dentro del navegador integrado. " +
                "Usa el icono Descargar del visor PDF; la app interceptará " +
                "esa descarga y la importará automáticamente.";
        }
    }

    private void CoreWebView2_DownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs e)
    {
        var uri = e.DownloadOperation.Uri;
        var suggested =
            Path.GetFileName(
                e.ResultFilePath);

        if (string.IsNullOrWhiteSpace(suggested) &&
            Uri.TryCreate(
                uri,
                UriKind.Absolute,
                out var parsedUri))
        {
            suggested =
                Uri.UnescapeDataString(
                    Path.GetFileName(
                        parsedUri.AbsolutePath));
        }

        if (!IsSupportedTariffPdf(
                suggested))
        {
            _statusText.Text =
                "Descarga no interceptada: no parece un PDF oficial “Tarifas Suministro Eléctrico”.";
            return;
        }

        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "SolarEnergyMonitor",
            "EnelTariffBrowser");
        Directory.CreateDirectory(
            tempDir);

        var targetPath = Path.Combine(
            tempDir,
            $"{Guid.NewGuid():N}-{suggested}");

        e.ResultFilePath =
            targetPath;
        e.Handled = true;

        var operation =
            e.DownloadOperation;

        if (!_activeDownloads.Add(
                targetPath))
        {
            return;
        }

        _statusText.Text =
            $"Descargando e importando {suggested}…";
        _busyBar.Visibility =
            Visibility.Visible;

        operation.StateChanged +=
            async (_, _) =>
            {
                if (operation.State ==
                    CoreWebView2DownloadState.InProgress)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(
                    async () =>
                    {
                        try
                        {
                            if (operation.State ==
                                CoreWebView2DownloadState.Completed)
                            {
                                await ImportDownloadedPdfAsync(
                                    targetPath,
                                    suggested);
                            }
                            else
                            {
                                _statusText.Text =
                                    $"Descarga interrumpida: {operation.InterruptReason}.";
                            }
                        }
                        finally
                        {
                            _activeDownloads.Remove(
                                targetPath);
                            if (_activeDownloads.Count == 0)
                            {
                                _busyBar.Visibility =
                                    Visibility.Collapsed;
                            }

                            TryDelete(
                                targetPath);
                        }
                    });
            };
    }

    private async Task ImportDownloadedPdfAsync(
        string path,
        string displayName)
    {
        var progress =
            new Progress<string>(
                message =>
                    _statusText.Text =
                        message);

        var result =
            await _importService.ImportAsync(
                [path],
                progress);

        if (result.Imported == 1 &&
            result.Failed == 0)
        {
            _statusText.Text =
                $"Importado: {displayName}. " +
                $"{result.NormalizedCandidates} candidato(s) tarifario(s) normalizado(s).";
            TariffImported?.Invoke(
                this,
                EventArgs.Empty);
            return;
        }

        _statusText.Text =
            result.Messages.Count > 0
                ? string.Join(
                    " | ",
                    result.Messages)
                : "La descarga terminó, pero el PDF no pudo importarse.";
    }

    private static bool IsOfficialEnelNavigation(
        Uri uri) =>
        (uri.Scheme.Equals(
             Uri.UriSchemeHttps,
             StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(
             Uri.UriSchemeHttp,
             StringComparison.OrdinalIgnoreCase)) &&
        (uri.Host.Equals(
             "enel.cl",
             StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(
             ".enel.cl",
             StringComparison.OrdinalIgnoreCase));

    private static bool IsOfficialTariffPdfUri(
        Uri uri)
    {
        if (!IsOfficialEnelNavigation(uri))
            return false;

        var fileName =
            Uri.UnescapeDataString(
                Path.GetFileName(
                    uri.AbsolutePath));

        return IsSupportedTariffPdf(
            fileName);
    }

    private static bool IsSupportedTariffPdf(
        string? fileName) =>
        !string.IsNullOrWhiteSpace(
            fileName) &&
        fileName.EndsWith(
            ".pdf",
            StringComparison.OrdinalIgnoreCase) &&
        fileName.Contains(
            "Tarifas Suministro Eléctrico",
            StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // The canonical imported copy already lives under Data/Tariffs.
            // Temporary cleanup failure must not invalidate captured evidence.
        }
    }
}

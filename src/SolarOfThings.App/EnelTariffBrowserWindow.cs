using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SolarOfThings.Core.Infrastructure;
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
    private readonly AppPaths _paths;
    private readonly TextBlock _statusText;
    private readonly TextBlock _receiptText;
    private readonly ProgressBar _busyBar;
    private readonly WebView2 _browser;
    private readonly HashSet<string> _activeDownloads =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _autoCapturedResponseUris =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PartialPdfCapture> _partialPdfCaptures =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? TariffImported;

    public EnelTariffBrowserWindow(
        EnelTariffPdfImportService importService,
        AppPaths paths)
    {
        _importService = importService;
        _paths = paths;

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
                    "“Tarifas suministro eléctrico”. La app intentará capturar el PDF " +
                    "automáticamente; si el visor lo entrega por rangos incompletos, " +
                    "el icono Descargar queda como fallback.",
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0x6C, 0x75, 0x7D)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

        _receiptText = new TextBlock
        {
            Text =
                "Última captura Enel desde navegador: " +
                "sin descarga interceptada en esta sesión.",
            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(0x34, 0x3A, 0x40)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        heading.Children.Add(_receiptText);
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
            _browser.CoreWebView2.WebResourceResponseReceived +=
                CoreWebView2_WebResourceResponseReceived;
            _browser.CoreWebView2.NewWindowRequested +=
                CoreWebView2_NewWindowRequested;
            _browser.CoreWebView2.NavigationStarting +=
                CoreWebView2_NavigationStarting;
            _browser.CoreWebView2.NavigationCompleted +=
                (_, args) =>
                {
                    if (!args.IsSuccess)
                    {
                        _statusText.Text =
                            $"La navegación terminó con {args.WebErrorStatus}. " +
                            "Puedes recargar o volver a la página de tarifas.";
                        return;
                    }

                    if (Uri.TryCreate(
                            _browser.Source?.AbsoluteUri,
                            UriKind.Absolute,
                            out var currentUri) &&
                        IsOfficialTariffPdfUri(
                            currentUri))
                    {
                        _statusText.Text =
                            "PDF oficial abierto dentro del navegador integrado. " +
                            "Intentando captura automática; si no se completa, usa " +
                            "el icono Descargar del visor PDF como fallback.";
                    }
                    else
                    {
                        _statusText.Text =
                            "Navegador listo. Descarga un PDF oficial de " +
                            "“Tarifas Suministro Eléctrico”.";
                    }
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

    private async void CoreWebView2_WebResourceResponseReceived(
        object? sender,
        CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (!Uri.TryCreate(
                e.Request.Uri,
                UriKind.Absolute,
                out var uri) ||
            !IsOfficialTariffPdfUri(uri) ||
            (e.Response.StatusCode != 200 &&
             e.Response.StatusCode != 206))
        {
            return;
        }

        var responseKey = uri.AbsoluteUri;
        if (_autoCapturedResponseUris.Contains(
                responseKey))
        {
            return;
        }

        var fileName =
            NormalizeBrowserDownloadFileName(
                Uri.UnescapeDataString(
                    Path.GetFileName(
                        uri.AbsolutePath)));

        string? targetPath = null;
        try
        {
            using var content =
                await e.Response.GetContentAsync();

            if (content is null)
                return;

            using var memory =
                new MemoryStream();
            await content.CopyToAsync(
                memory);
            var responseBytes =
                memory.ToArray();

            byte[] bytes;

            if (e.Response.StatusCode == 206)
            {
                if (!TryGetContentRange(
                        e.Response.Headers,
                        out var rangeStart,
                        out var rangeEnd,
                        out var totalLength) ||
                    responseBytes.LongLength !=
                        rangeEnd - rangeStart + 1)
                {
                    return;
                }

                if (!_partialPdfCaptures.TryGetValue(
                        responseKey,
                        out var partial))
                {
                    partial =
                        new PartialPdfCapture(
                            totalLength);
                    _partialPdfCaptures[
                        responseKey] =
                        partial;
                }

                if (partial.TotalLength !=
                    totalLength)
                {
                    _partialPdfCaptures.Remove(
                        responseKey);
                    return;
                }

                partial.Add(
                    rangeStart,
                    responseBytes);

                _statusText.Text =
                    $"PDF oficial recibido por rangos: {partial.CapturedBytes:N0}/{totalLength:N0} bytes. " +
                    "La app lo importará automáticamente si el visor entrega el archivo completo.";

                if (!partial.TryAssemble(
                        out bytes))
                {
                    return;
                }

                _partialPdfCaptures.Remove(
                    responseKey);
            }
            else
            {
                bytes = responseBytes;
            }

            if (!LooksLikePdf(
                    bytes))
            {
                return;
            }

            if (!_autoCapturedResponseUris.Add(
                    responseKey))
            {
                return;
            }

            var sessionDir = Path.Combine(
                _paths.TariffEnelIncomingDirectory,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                sessionDir);

            targetPath = Path.Combine(
                sessionDir,
                fileName);

            await File.WriteAllBytesAsync(
                targetPath,
                bytes);

            _busyBar.Visibility =
                Visibility.Visible;
            _statusText.Text =
                $"PDF oficial recibido automáticamente: {fileName}. Importando…";
            _receiptText.Text =
                "CoreWebView2.WebResourceResponseReceived capturado.\n" +
                $"Archivo oficial: {fileName}\n" +
                "Resultado: respuesta PDF completa recibida en la sesión WebView2; importando automáticamente.";

            await ImportDownloadedPdfAsync(
                targetPath,
                fileName,
                "CoreWebView2.WebResourceResponseReceived");
        }
        catch (Exception ex)
        {
            _autoCapturedResponseUris.Remove(
                responseKey);
            _partialPdfCaptures.Remove(
                responseKey);
            _statusText.Text =
                "La captura automática del PDF no pudo completarse. " +
                "Puedes usar el icono Descargar del visor como fallback. " +
                ex.Message;
            _receiptText.Text =
                "Captura automática WebView2 no completada.\n" +
                "Fallback disponible: icono Descargar del visor PDF.";
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(
                    targetPath))
            {
                TryDelete(
                    targetPath);
                TryDeleteDirectory(
                    Path.GetDirectoryName(
                        targetPath));
            }

            if (_activeDownloads.Count == 0)
            {
                _busyBar.Visibility =
                    Visibility.Collapsed;
            }
        }
    }

    private static bool LooksLikePdf(
        byte[] bytes) =>
        bytes.Length >= 5 &&
        bytes[0] == (byte)'%' &&
        bytes[1] == (byte)'P' &&
        bytes[2] == (byte)'D' &&
        bytes[3] == (byte)'F' &&
        bytes[4] == (byte)'-';

    private void CoreWebView2_DownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs e)
    {
        var uri =
            e.DownloadOperation.Uri;
        string? uriSuggested = null;

        if (Uri.TryCreate(
                uri,
                UriKind.Absolute,
                out var parsedUri))
        {
            uriSuggested =
                NormalizeBrowserDownloadFileName(
                    Uri.UnescapeDataString(
                        Path.GetFileName(
                            parsedUri.AbsolutePath)));
        }

        var suggested =
            IsSupportedTariffPdf(
                uriSuggested)
                ? uriSuggested!
                : NormalizeBrowserDownloadFileName(
                    Path.GetFileName(
                        e.ResultFilePath));

        _receiptText.Text =
            "CoreWebView2.DownloadStarting capturado.\n" +
            $"Archivo detectado: {suggested}\n" +
            $"Hora UTC: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}";

        if (!IsSupportedTariffPdf(
                suggested))
        {
            _statusText.Text =
                "Descarga no interceptada: no parece un PDF oficial “Tarifas Suministro Eléctrico”.";
            _receiptText.Text +=
                "\nResultado: descarga observada, pero nombre/URI no reconocido como tarifario Enel compatible.";
            return;
        }

        // Suppress WebView2's default download UI as early as possible, then
        // route the download directly into app-controlled incoming storage.
        e.Handled = true;

        var tempDir = Path.Combine(
            _paths.TariffEnelIncomingDirectory,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(
            tempDir);

        var targetPath = Path.Combine(
            tempDir,
            suggested);

        e.ResultFilePath =
            targetPath;

        var operation =
            e.DownloadOperation;

        if (!_activeDownloads.Add(
                targetPath))
        {
            return;
        }

        _statusText.Text =
            $"Descargando e importando {suggested}…";
        _receiptText.Text +=
            "\nResultado: descarga compatible interceptada; esperando finalización e importación.";
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
                            TryDeleteDirectory(
                                Path.GetDirectoryName(
                                    targetPath));
                        }
                    });
            };
    }

    private async Task ImportDownloadedPdfAsync(
        string path,
        string displayName,
        string route = "CoreWebView2.DownloadStarting")
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
            var receipt =
                result.Receipts.SingleOrDefault();

            _statusText.Text =
                $"Importado: {displayName}. " +
                $"{result.NormalizedCandidates} candidato(s) tarifario(s) normalizado(s).";

            if (receipt is not null)
            {
                _receiptText.Text =
                    FormatReceipt(
                        receipt,
                        route);
            }
            else
            {
                _receiptText.Text =
                    $"{route} → EnelTariffPdfImportService: COMPLETADO.\n" +
                    $"Archivo: {displayName}\n" +
                    $"Candidatos normalizados: {result.NormalizedCandidates}";
            }

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
        _receiptText.Text +=
            "\nImportación: FALLÓ. " +
            _statusText.Text;
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

    private static string FormatReceipt(
        EnelTariffPdfImportReceipt receipt,
        string route)
    {
        var outcome = receipt.Outcome switch
        {
            "NEW_PUBLICATION" =>
                "nueva publicación importada",
            "EXISTING_IDENTICAL_REIMPORT" =>
                "existente · reimportación byte-idéntica",
            "CAPTURED_EXISTING_DISCOVERY" =>
                "publicación ya descubierta · captura completada",
            "UPDATED_EXISTING_PUBLICATION" =>
                "publicación existente · contenido actualizado",
            _ => receipt.Outcome
        };

        return
            $"{route} → EnelTariffPdfImportService: COMPLETADO.\n" +
            $"Archivo oficial: {receipt.OfficialFileName}\n" +
            $"Resultado: {outcome}\n" +
            $"SHA-256: {receipt.Sha256}\n" +
            $"Publicación ID: {receipt.PublicationId} · páginas: {receipt.PageCount} · " +
            $"candidatos normalizados: {receipt.NormalizedCandidates}\n" +
            $"Hora UTC: {receipt.CompletedUtc:yyyy-MM-dd HH:mm:ss}";
    }

    private static string NormalizeBrowserDownloadFileName(
        string? fileName)
    {
        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            return string.Empty;
        }

        var trimmed =
            fileName.Trim();
        var extension =
            Path.GetExtension(
                trimmed);

        if (!string.Equals(
                extension,
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var stem =
            Path.GetFileNameWithoutExtension(
                trimmed);
        var marker =
            stem.LastIndexOf(
                " (",
                StringComparison.Ordinal);

        if (marker >= 0 &&
            stem.EndsWith(
                ")",
                StringComparison.Ordinal))
        {
            var numeric =
                stem[
                    (marker + 2)..
                    ^1];

            if (int.TryParse(
                    numeric,
                    out _))
            {
                stem =
                    stem[..marker];
            }
        }

        return stem +
               extension;
    }

    private static bool TryGetContentRange(
        CoreWebView2HttpResponseHeaders headers,
        out long start,
        out long end,
        out long total)
    {
        start = 0;
        end = 0;
        total = 0;

        string raw;
        try
        {
            raw =
                headers.GetHeader(
                    "Content-Range");
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                raw) ||
            !raw.StartsWith(
                "bytes ",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value =
            raw[6..].Trim();
        var slash =
            value.IndexOf('/');
        var dash =
            value.IndexOf('-');

        if (dash <= 0 ||
            slash <= dash + 1)
        {
            return false;
        }

        return long.TryParse(
                   value[..dash],
                   out start) &&
               long.TryParse(
                   value[(dash + 1)..slash],
                   out end) &&
               long.TryParse(
                   value[(slash + 1)..],
                   out total) &&
               start >= 0 &&
               end >= start &&
               total > end;
    }

    private sealed class PartialPdfCapture
    {
        private readonly SortedDictionary<long, byte[]> _parts =
            new();

        public PartialPdfCapture(
            long totalLength)
        {
            TotalLength =
                totalLength;
        }

        public long TotalLength { get; }

        public long CapturedBytes =>
            _parts.Sum(item =>
                (long)item.Value.Length);

        public void Add(
            long start,
            byte[] bytes)
        {
            _parts[start] =
                bytes;
        }

        public bool TryAssemble(
            out byte[] bytes)
        {
            bytes =
                Array.Empty<byte>();

            if (TotalLength <= 0 ||
                TotalLength > int.MaxValue)
            {
                return false;
            }

            var cursor = 0L;
            foreach (var part in _parts)
            {
                if (part.Key != cursor)
                    return false;

                cursor +=
                    part.Value.LongLength;
            }

            if (cursor != TotalLength)
                return false;

            bytes =
                new byte[
                    (int)TotalLength];

            foreach (var part in _parts)
            {
                Buffer.BlockCopy(
                    part.Value,
                    0,
                    bytes,
                    (int)part.Key,
                    part.Value.Length);
            }

            return true;
        }
    }

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

    private static void TryDeleteDirectory(
        string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) &&
                Directory.Exists(path) &&
                !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup only; provenance lives under Data/Tariffs.
        }
    }
}

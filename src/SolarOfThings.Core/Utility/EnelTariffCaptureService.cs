using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SolarOfThings.Core.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SolarOfThings.Core.Utility;

public sealed class EnelTariffCaptureService
{
    public const string OfficialArchiveUrl =
        "https://www.enel.cl/es/clientes/tarifas-y-regulacion/tarifas.html";

    private static readonly Regex AnchorRegex = new(
        "<a[^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex PdfHrefRegex = new(
        @"href\s*=\s*[""'](?<href>[^""']+?\.pdf(?:\?[^""']*)?)[""']",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.Compiled);

    private static readonly Regex RawPdfUrlRegex = new(
        @"(?<href>https?://[^\s""'<>]+?\.pdf(?:\?[^\s""'<>]*)?)",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.Compiled);

    private readonly TariffPublicationRepository _repository;
    private readonly EnelTariffNormalizationService _normalization;
    private readonly AppPaths _paths;

    public EnelTariffCaptureService(
        TariffPublicationRepository repository,
        EnelTariffNormalizationService normalization,
        AppPaths paths)
    {
        _repository = repository;
        _normalization = normalization;
        _paths = paths;
    }

    public static async Task<TariffCatalogProbeResult>
        ProbeOfficialCatalogAsync(
            int year,
            CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > DateTime.Now.Year + 1)
            throw new ArgumentOutOfRangeException(nameof(year));

        using var client = CreateClient();
        var catalog = await FetchCatalogAsync(
            client,
            cancellationToken);
        var discovered = DiscoverSupplyTariffs(
            catalog.Html,
            new Uri(OfficialArchiveUrl),
            year);

        return new TariffCatalogProbeResult(
            year,
            (int)catalog.StatusCode,
            catalog.Html.Length,
            catalog.PdfHrefCount,
            discovered.Count,
            discovered
                .Select(item => item.Title)
                .ToArray());
    }

    public async Task<TariffCaptureResult> CaptureSupplyTariffsAsync(
        int year,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > DateTime.Now.Year + 1)
            throw new ArgumentOutOfRangeException(nameof(year));
        using var client = CreateClient();

        progress?.Report("Consultando catálogo oficial de Enel...");
        var catalog = await FetchCatalogAsync(
            client,
            cancellationToken);

        var providerRoot = Path.Combine(
            _paths.TariffDirectory,
            "Enel");
        Directory.CreateDirectory(providerRoot);

        var catalogDiagnosticPath = Path.Combine(
            providerRoot,
            $"catalog-{year}-last.html");
        await File.WriteAllTextAsync(
            catalogDiagnosticPath,
            catalog.Html,
            cancellationToken);

        var discovered = DiscoverSupplyTariffs(
            catalog.Html,
            new Uri(OfficialArchiveUrl),
            year);

        progress?.Report(
            $"Catálogo recibido: HTTP {(int)catalog.StatusCode} · " +
            $"{catalog.Html.Length:N0} caracteres · " +
            $"{catalog.PdfHrefCount} enlace(s) PDF detectado(s) · " +
            $"{discovered.Count} publicación(es) para {year}.");

        if (discovered.Count == 0)
        {
            var bodyHint = DetectCatalogBlockPage(catalog.Html);
            throw new InvalidOperationException(
                $"Enel respondió HTTP {(int)catalog.StatusCode}, pero no se encontraron " +
                $"publicaciones oficiales de suministro para {year}. " +
                $"Contenido recibido: {catalog.Html.Length:N0} caracteres; " +
                $"enlaces PDF detectados: {catalog.PdfHrefCount}. " +
                $"{bodyHint} " +
                $"Se guardó una copia diagnóstica del catálogo en " +
                $"{catalogDiagnosticPath}.");
        }

        var messages = new List<string>();
        var captured = 0;
        var failed = 0;
        var normalizedCandidates = 0;
        var normalizationFailures = 0;

        foreach (var item in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publicationId = _repository.UpsertDiscovery(item);

            try
            {
                progress?.Report(
                    $"Capturando {item.Title}...");
                var bytes = await DownloadPdfAsync(
                    client,
                    item.SourceUrl,
                    cancellationToken);

                var providerDir = Path.Combine(
                    _paths.TariffDirectory,
                    "Enel",
                    year.ToString());
                Directory.CreateDirectory(providerDir);

                var fileName = SafeFileName(item.Title);
                if (!fileName.EndsWith(
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".pdf";
                }

                var localPath = Path.Combine(
                    providerDir,
                    fileName);
                await File.WriteAllBytesAsync(
                    localPath,
                    bytes,
                    cancellationToken);

                var sha = Convert.ToHexString(
                    SHA256.HashData(bytes))
                    .ToLowerInvariant();

                var pageTexts = ExtractPageText(localPath);
                _repository.MarkCaptured(
                    publicationId,
                    localPath,
                    sha,
                    bytes.LongLength,
                    pageTexts.Count,
                    pageTexts);

                captured++;

                try
                {
                    progress?.Report(
                        $"Normalizando candidatos BT1: {item.Title}...");
                    var normalization =
                        _normalization.NormalizePublication(
                            publicationId,
                            pageTexts);
                    normalizedCandidates += normalization.CandidateCount;
                    progress?.Report(
                        $"Candidatos extraídos: {normalization.CandidateCount} " +
                        $"en {normalization.PagesWithCandidates} página(s).");
                }
                catch (Exception normalizationEx)
                {
                    normalizationFailures++;
                    messages.Add(
                        $"{item.Title} [normalización]: {normalizationEx.Message}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                _repository.MarkFailed(
                    publicationId,
                    ex.Message);
                messages.Add($"{item.Title}: {ex.Message}");
            }
        }

        var retroactive = discovered.Count(item => item.IsRetroactive);
        var multiVersionPeriods = discovered
            .Where(item => item.EffectiveFrom.HasValue)
            .GroupBy(item => item.EffectiveFrom)
            .Count(group => group.Count() > 1);

        progress?.Report(
            $"Captura {year} terminada: {captured}/{discovered.Count} publicaciones · " +
            $"{retroactive} retroactivas · {multiVersionPeriods} períodos con múltiples versiones · " +
            $"{normalizedCandidates} candidatos tarifarios · {normalizationFailures} fallos de normalización.");

        return new TariffCaptureResult(
            discovered.Count,
            captured,
            failed,
            messages,
            retroactive,
            multiVersionPeriods,
            normalizedCandidates,
            normalizationFailures);
    }

    public static IReadOnlyList<TariffPublicationDiscovery>
        DiscoverSupplyTariffs(
            string html,
            Uri archiveUri,
            int year)
    {
        var result = new Dictionary<string, TariffPublicationDiscovery>(
            StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AnchorRegex.Matches(html))
        {
            var rawText = TagRegex.Replace(
                match.Groups["text"].Value,
                " ");
            var anchorText = WebUtility.HtmlDecode(rawText);
            anchorText = Regex.Replace(
                    anchorText,
                    @"\s+",
                    " ")
                .Trim();

            TryAddDiscovery(
                result,
                match.Groups["href"].Value,
                anchorText,
                archiveUri,
                year);
        }

        // Live Enel markup has changed over time. Do not require a complete
        // <a>...</a> pair: any href that points to an official PDF is enough
        // to recover the publication title from its filename.
        foreach (Match match in PdfHrefRegex.Matches(html))
        {
            TryAddDiscovery(
                result,
                match.Groups["href"].Value,
                null,
                archiveUri,
                year);
        }

        // Last-resort recovery for absolute PDF URLs embedded in scripts or
        // component data rather than rendered anchor tags.
        foreach (Match match in RawPdfUrlRegex.Matches(html))
        {
            TryAddDiscovery(
                result,
                match.Groups["href"].Value,
                null,
                archiveUri,
                year);
        }

        return result.Values
            .OrderBy(item => item.EffectiveFrom)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static void TryAddDiscovery(
        IDictionary<string, TariffPublicationDiscovery> result,
        string rawHrefValue,
        string? anchorText,
        Uri archiveUri,
        int year)
    {
        var rawHref = WebUtility.HtmlDecode(rawHrefValue)
            .Trim();

        if (!rawHref.Contains(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (rawHref.StartsWith(
                "//",
                StringComparison.Ordinal))
        {
            rawHref = $"{archiveUri.Scheme}:{rawHref}";
        }

        Uri uri;
        try
        {
            uri = Uri.TryCreate(
                    rawHref,
                    UriKind.Absolute,
                    out var absolute)
                ? absolute
                : new Uri(
                    archiveUri,
                    rawHref);
        }
        catch (UriFormatException)
        {
            return;
        }

        var fileName = Uri.UnescapeDataString(
            Path.GetFileName(
                uri.AbsolutePath));

        var title =
            !string.IsNullOrWhiteSpace(anchorText) &&
            anchorText.Contains(
                "Tarifas Suministro Eléctrico",
                StringComparison.OrdinalIgnoreCase)
                ? anchorText
                : fileName;

        if (!title.Contains(
                "Tarifas Suministro Eléctrico",
                StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(
                title,
                $@"(?<!\d){year}(?!\d)",
                RegexOptions.CultureInvariant))
        {
            return;
        }

        var discovery = new TariffPublicationDiscovery(
            "ENEL_DISTRIBUCION_CHILE",
            "SUPPLY_REGULATED",
            title,
            uri.ToString(),
            ParseEffectiveDate(
                title,
                year),
            title.Contains(
                "Retroactivo",
                StringComparison.OrdinalIgnoreCase));

        result[discovery.SourceUrl] = discovery;
    }

    private static async Task<CatalogFetchResult> FetchCatalogAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            OfficialArchiveUrl,
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var html = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"El catálogo oficial de Enel respondió HTTP " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}.",
                null,
                response.StatusCode);
        }

        return new CatalogFetchResult(
            response.StatusCode,
            html,
            PdfHrefRegex.Matches(html).Count +
            RawPdfUrlRegex.Matches(html).Count);
    }

    private static async Task<byte[]> DownloadPdfAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            url,
            "application/pdf,application/octet-stream;q=0.9,*/*;q=0.8");
        request.Headers.Referrer =
            new Uri(OfficialArchiveUrl);

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"La publicación oficial respondió HTTP " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}.",
                null,
                response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);

        if (bytes.Length < 5 ||
            bytes[0] != (byte)'%' ||
            bytes[1] != (byte)'P' ||
            bytes[2] != (byte)'D' ||
            bytes[3] != (byte)'F' ||
            bytes[4] != (byte)'-')
        {
            var contentType =
                response.Content.Headers.ContentType?.MediaType
                ?? "sin Content-Type";
            throw new InvalidDataException(
                $"Enel no devolvió un PDF válido " +
                $"({bytes.Length:N0} bytes; {contentType}).");
        }

        return bytes;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        string accept)
    {
        var request = new HttpRequestMessage(
            method,
            url);
        request.Headers.TryAddWithoutValidation(
            "Accept",
            accept);
        request.Headers.TryAddWithoutValidation(
            "Accept-Language",
            "es-CL,es;q=0.9,en;q=0.5");
        request.Headers.TryAddWithoutValidation(
            "Cache-Control",
            "no-cache");
        request.Headers.TryAddWithoutValidation(
            "Pragma",
            "no-cache");
        return request;
    }

    private static string DetectCatalogBlockPage(
        string html)
    {
        if (html.Contains(
                "Access Denied",
                StringComparison.OrdinalIgnoreCase) ||
            html.Contains(
                "Reference #",
                StringComparison.OrdinalIgnoreCase))
        {
            return "La respuesta parece ser una página de bloqueo del sitio.";
        }

        if (html.Contains(
                "captcha",
                StringComparison.OrdinalIgnoreCase) ||
            html.Contains(
                "challenge",
                StringComparison.OrdinalIgnoreCase))
        {
            return "La respuesta parece contener un desafío anti-bot.";
        }

        return "La estructura del catálogo puede haber cambiado o el sitio " +
               "puede haber entregado una versión distinta del HTML.";
    }

    private static IReadOnlyList<string> ExtractPageText(
        string localPath)
    {
        var pages = new List<string>();
        using var document = PdfDocument.Open(localPath);
        foreach (var page in document.GetPages())
        {
            pages.Add(
                ContentOrderTextExtractor.GetText(page));
        }

        return pages;
    }

    private static DateOnly? ParseEffectiveDate(
        string title,
        int year)
    {
        var months = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["enero"] = 1,
            ["febrero"] = 2,
            ["marzo"] = 3,
            ["abril"] = 4,
            ["mayo"] = 5,
            ["junio"] = 6,
            ["julio"] = 7,
            ["agosto"] = 8,
            ["septiembre"] = 9,
            ["octubre"] = 10,
            ["noviembre"] = 11,
            ["diciembre"] = 12
        };

        foreach (var pair in months)
        {
            if (title.Contains(
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new DateOnly(year, pair.Value, 1);
            }
        }

        return null;
    }

    private static string SafeFileName(
        string title)
    {
        var result = title;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return result;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/152.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "DNT",
            "1");
        return client;
    }

    private sealed record CatalogFetchResult(
        HttpStatusCode StatusCode,
        string Html,
        int PdfHrefCount);
}

public sealed record TariffCatalogProbeResult(
    int Year,
    int HttpStatusCode,
    int HtmlLength,
    int PdfHrefCount,
    int DiscoveredPublications,
    IReadOnlyList<string> Titles);

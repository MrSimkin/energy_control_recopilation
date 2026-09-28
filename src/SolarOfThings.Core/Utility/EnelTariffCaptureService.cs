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

    private readonly TariffPublicationRepository _repository;
    private readonly AppPaths _paths;

    public EnelTariffCaptureService(
        TariffPublicationRepository repository,
        AppPaths paths)
    {
        _repository = repository;
        _paths = paths;
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
        var html = await client.GetStringAsync(
            OfficialArchiveUrl,
            cancellationToken);

        var discovered = DiscoverSupplyTariffs(
            html,
            new Uri(OfficialArchiveUrl),
            year);

        var messages = new List<string>();
        var captured = 0;
        var failed = 0;

        foreach (var item in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publicationId = _repository.UpsertDiscovery(item);

            try
            {
                progress?.Report(
                    $"Capturando {item.Title}...");
                var bytes = await client.GetByteArrayAsync(
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
            $"{retroactive} retroactivas · {multiVersionPeriods} períodos con múltiples versiones.");

        return new TariffCaptureResult(
            discovered.Count,
            captured,
            failed,
            messages,
            retroactive,
            multiVersionPeriods);
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
            var rawHref = WebUtility.HtmlDecode(
                match.Groups["href"].Value);
            if (!rawHref.Contains(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var uri = Uri.TryCreate(
                rawHref,
                UriKind.Absolute,
                out var absolute)
                    ? absolute
                    : new Uri(archiveUri, rawHref);

            var rawText = TagRegex.Replace(
                match.Groups["text"].Value,
                " ");
            var anchorText = WebUtility.HtmlDecode(rawText);
            anchorText = Regex.Replace(anchorText, @"\s+", " ").Trim();

            var fileName = Uri.UnescapeDataString(
                Path.GetFileName(uri.AbsolutePath));
            var title = anchorText.Contains(
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
                continue;
            }

            var discovery = new TariffPublicationDiscovery(
                "ENEL_DISTRIBUCION_CHILE",
                "SUPPLY_REGULATED",
                title,
                uri.ToString(),
                ParseEffectiveDate(title, year),
                title.Contains(
                    "Retroactivo",
                    StringComparison.OrdinalIgnoreCase));

            result[discovery.SourceUrl] = discovery;
        }

        return result.Values
            .OrderBy(item => item.EffectiveFrom)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToArray();
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
            "SolarEnergyMonitor/1.0 (+tariff-source-capture)");
        return client;
    }
}

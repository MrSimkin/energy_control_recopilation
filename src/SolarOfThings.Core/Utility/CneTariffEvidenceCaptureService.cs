using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SolarOfThings.Core.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Captures machine-accessible official CNE regulatory evidence used by
/// distribution tariff formulas. This is not presented as an Enel final tariff
/// table and does not by itself establish bill applicability.
/// </summary>
public sealed class CneTariffEvidenceCaptureService
{
    public const string OfficialVadIndexPageUrl =
        "https://www.cne.cl/tarificacion/electrica/valor-agregado-de-distribucion/" +
        "opciones-tarifarias-a-usuarios-finales/";

    private static readonly Regex PdfHrefRegex = new(
        @"href\s*=\s*[""'](?<href>[^""']+?\.pdf(?:\?[^""']*)?)[""']",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.Compiled);

    private static readonly Regex ResolutionNumberRegex = new(
        @"RESOLUCI[ÓO]N\s+EXENTA\s+(?:N[°ºo.]?\s*)?(?<number>\d+)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex CurrentResolutionNumberRegex = new(
        @"RESOLUCI[ÓO]N\s+EXENTA\s+(?:N[°ºo.]?\s*)?(?<number>\d+)\s*/\s*20\d{2}",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex EffectivePeriodRegex = new(
        @"periodo\s+comprendido\s+entre\s+el\s+1\s+de\s+" +
        @"(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)" +
        @"\s+de\s+(?<year>20\d{2})",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex VadIndexPhraseRegex = new(
        @"[íi]ndices\s+contenidos\s+en\s+las\s+f[óo]rmulas\s+tarifarias",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex CorrectionRegex = new(
        @"Rectifica\s+(?:,\s*)?Resoluci[óo]n",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex EffectiveTableMonthRegex = new(
        @"(?<![A-Za-z])(?<month>ene|feb|mar|abr|may|jun|jul|ago|sep|oct|nov|dic)-(?<year>\d{2})(?!\d)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private readonly TariffPublicationRepository _repository;
    private readonly AppPaths _paths;

    public CneTariffEvidenceCaptureService(
        TariffPublicationRepository repository,
        AppPaths paths)
    {
        _repository = repository;
        _paths = paths;
    }

    public async Task<CneTariffEvidenceCaptureResult> CaptureVadIndexEvidenceAsync(
        int year,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > DateTime.Now.Year + 1)
            throw new ArgumentOutOfRangeException(nameof(year));

        using var client = CreateClient();

        progress?.Report($"Consultando evidencia regulatoria CNE {year}...");
        using var pageRequest = new HttpRequestMessage(
            HttpMethod.Get,
            OfficialVadIndexPageUrl);
        pageRequest.Headers.TryAddWithoutValidation(
            "Accept",
            "text/html,application/xhtml+xml,*/*;q=0.8");

        using var pageResponse = await client.SendAsync(
            pageRequest,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        pageResponse.EnsureSuccessStatusCode();

        var html = await pageResponse.Content.ReadAsStringAsync(
            cancellationToken);

        var candidates = DiscoverYearPdfUrls(
            html,
            new Uri(OfficialVadIndexPageUrl),
            year);

        var accepted = 0;
        var failed = 0;
        var corrections = 0;
        var messages = new List<string>();

        var targetDir = Path.Combine(
            _paths.TariffDirectory,
            "CNE",
            year.ToString());
        Directory.CreateDirectory(targetDir);

        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = candidates[index];

            progress?.Report(
                $"CNE {year}: revisando documento {index + 1}/{candidates.Count}...");

            try
            {
                var bytes = await DownloadPdfAsync(
                    client,
                    url,
                    cancellationToken);

                var tempPath = Path.Combine(
                    targetDir,
                    $".candidate-{Guid.NewGuid():N}.pdf");
                await File.WriteAllBytesAsync(
                    tempPath,
                    bytes,
                    cancellationToken);

                IReadOnlyList<string> pageTexts;
                try
                {
                    pageTexts = ExtractPageText(tempPath);
                }
                catch
                {
                    File.Delete(tempPath);
                    throw;
                }

                var fullText = string.Join(
                    "\n",
                    pageTexts);

                if (!IsVadIndexResolution(fullText))
                {
                    File.Delete(tempPath);
                    continue;
                }

                var effectiveFrom = ParseEffectiveDate(fullText);
                if (!effectiveFrom.HasValue ||
                    effectiveFrom.Value.Year != year)
                {
                    File.Delete(tempPath);
                    continue;
                }

                var isCorrection = IsCorrection(fullText);
                var title = BuildTitle(
                    fullText,
                    effectiveFrom,
                    isCorrection,
                    url);

                var publication = new TariffPublicationDiscovery(
                    "CNE_CHILE",
                    "VAD_INDEX",
                    title,
                    url,
                    effectiveFrom,
                    isCorrection);

                var publicationId = _repository.UpsertDiscovery(
                    publication);

                var fileName = SafeFileName(
                    $"{effectiveFrom?.ToString("yyyy-MM") ?? year.ToString()}_" +
                    $"{Path.GetFileName(new Uri(url).AbsolutePath)}");
                var finalPath = Path.Combine(
                    targetDir,
                    fileName);

                File.Move(
                    tempPath,
                    finalPath,
                    overwrite: true);

                var sha = Convert.ToHexString(
                        SHA256.HashData(bytes))
                    .ToLowerInvariant();

                _repository.MarkCaptured(
                    publicationId,
                    finalPath,
                    sha,
                    bytes.LongLength,
                    pageTexts.Count,
                    pageTexts);

                accepted++;
                if (isCorrection)
                    corrections++;
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add(
                    $"{url}: {ex.Message}");
            }
        }

        progress?.Report(
            $"CNE {year}: {accepted} documento(s) VAD capturado(s), " +
            $"{corrections} corrección(es), {failed} fallo(s).");

        return new CneTariffEvidenceCaptureResult(
            year,
            candidates.Count,
            accepted,
            failed,
            corrections,
            messages);
    }

    public static async Task<CneTariffEvidenceProbeResult>
        ProbeOfficialSourceAsync(
            int year,
            CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            OfficialVadIndexPageUrl,
            cancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            cancellationToken);
        var candidates = DiscoverYearPdfUrls(
            html,
            new Uri(OfficialVadIndexPageUrl),
            year);

        return new CneTariffEvidenceProbeResult(
            (int)response.StatusCode,
            html.Length,
            candidates.Count,
            candidates.Take(5).ToArray());
    }

    public static IReadOnlyList<string> DiscoverYearPdfUrls(
        string html,
        Uri pageUri,
        int year)
    {
        var result = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var yearPath = $"/{year}/";
        var priorDecemberPath =
            $"/{year - 1}/12/";

        foreach (Match match in PdfHrefRegex.Matches(html))
        {
            var href = WebUtility.HtmlDecode(
                    match.Groups["href"].Value)
                .Trim();

            Uri uri;
            try
            {
                uri = Uri.TryCreate(
                        href,
                        UriKind.Absolute,
                        out var absolute)
                    ? absolute
                    : new Uri(
                        pageUri,
                        href);
            }
            catch (UriFormatException)
            {
                continue;
            }

            if (!string.Equals(
                    uri.Host,
                    "www.cne.cl",
                    StringComparison.OrdinalIgnoreCase) ||
                (!uri.AbsolutePath.Contains(
                     yearPath,
                     StringComparison.OrdinalIgnoreCase) &&
                 !uri.AbsolutePath.Contains(
                     priorDecemberPath,
                     StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(uri.ToString());
        }

        return result
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsVadIndexResolution(
        string text) =>
        VadIndexPhraseRegex.IsMatch(text) &&
        text.Contains(
            "Decreto",
            StringComparison.OrdinalIgnoreCase) &&
        text.Contains(
            "5T",
            StringComparison.OrdinalIgnoreCase) &&
        (text.Contains(
             "periodo comprendido",
             StringComparison.OrdinalIgnoreCase) ||
         CorrectionRegex.IsMatch(text));

    private static bool IsCorrection(
        string text) =>
        CorrectionRegex.IsMatch(text);

    private static DateOnly? ParseEffectiveDate(
        string text)
    {
        var match = EffectivePeriodRegex.Match(text);
        if (match.Success &&
            int.TryParse(
                match.Groups["year"].Value,
                out var fullYear))
        {
            var month = MonthNumber(
                match.Groups["month"].Value);
            if (month.HasValue)
                return new DateOnly(fullYear, month.Value, 1);
        }

        // Rectification resolutions can replace the original table without
        // repeating the prose "periodo comprendido..." sentence. In those
        // cases the effective month remains explicit in the replacement table
        // (for example "ago-26").
        var shortMatch = EffectiveTableMonthRegex.Match(text);
        if (!shortMatch.Success ||
            !int.TryParse(
                shortMatch.Groups["year"].Value,
                out var shortYear))
        {
            return null;
        }

        var shortMonth = ShortMonthNumber(
            shortMatch.Groups["month"].Value);
        return shortMonth.HasValue
            ? new DateOnly(2000 + shortYear, shortMonth.Value, 1)
            : null;
    }

    private static string BuildTitle(
        string text,
        DateOnly? effectiveFrom,
        bool isCorrection,
        string sourceUrl)
    {
        var currentNumberMatch =
            CurrentResolutionNumberRegex.Match(text);
        var fallbackNumberMatch =
            ResolutionNumberRegex.Match(text);
        var numberMatch = currentNumberMatch.Success
            ? currentNumberMatch
            : fallbackNumberMatch;

        var resolution = numberMatch.Success
            ? $"Resolución Exenta CNE N° {numberMatch.Groups["number"].Value}"
            : Path.GetFileName(
                new Uri(sourceUrl).AbsolutePath);

        var period = effectiveFrom.HasValue
            ? $" · índices VAD {effectiveFrom.Value:yyyy-MM}"
            : " · índices VAD";
        var correction = isCorrection
            ? " · rectificación"
            : string.Empty;

        return resolution + period + correction;
    }

    private static int? ShortMonthNumber(
        string month) =>
        month.ToLowerInvariant() switch
        {
            "ene" => 1,
            "feb" => 2,
            "mar" => 3,
            "abr" => 4,
            "may" => 5,
            "jun" => 6,
            "jul" => 7,
            "ago" => 8,
            "sep" => 9,
            "oct" => 10,
            "nov" => 11,
            "dic" => 12,
            _ => null
        };

    private static int? MonthNumber(
        string month) =>
        month.ToLowerInvariant() switch
        {
            "enero" => 1,
            "febrero" => 2,
            "marzo" => 3,
            "abril" => 4,
            "mayo" => 5,
            "junio" => 6,
            "julio" => 7,
            "agosto" => 8,
            "septiembre" => 9,
            "octubre" => 10,
            "noviembre" => 11,
            "diciembre" => 12,
            _ => null
        };

    private static async Task<byte[]> DownloadPdfAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            url,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
        if (bytes.Length < 5 ||
            bytes[0] != (byte)'%' ||
            bytes[1] != (byte)'P' ||
            bytes[2] != (byte)'D' ||
            bytes[3] != (byte)'F' ||
            bytes[4] != (byte)'-')
        {
            throw new InvalidDataException(
                "CNE did not return a valid PDF document.");
        }

        return bytes;
    }

    private static IReadOnlyList<string> ExtractPageText(
        string path)
    {
        var result = new List<string>();
        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
        {
            result.Add(
                ContentOrderTextExtractor.GetText(page));
        }

        return result;
    }

    private static string SafeFileName(
        string value)
    {
        var result = value;
        foreach (var invalid in Path.GetInvalidFileNameChars())
            result = result.Replace(invalid, '_');

        return result;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SolarEnergyMonitor/1.0 (+official-cne-tariff-evidence)");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept-Language",
            "es-CL,es;q=0.9");
        return client;
    }
}

public sealed record CneTariffEvidenceCaptureResult(
    int Year,
    int CandidatePdfLinks,
    int CapturedVadIndexDocuments,
    int Failed,
    int Corrections,
    IReadOnlyList<string> Messages);

public sealed record CneTariffEvidenceProbeResult(
    int HttpStatusCode,
    int HtmlLength,
    int CandidatePdfLinks,
    IReadOnlyList<string> SampleUrls);

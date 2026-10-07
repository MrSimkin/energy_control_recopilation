using System.Globalization;
using System.Net;
using System.Text;
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

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

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

    private static readonly Regex ListingResolutionWithDateRegex = new(
        @"RESOLUCI[ÓO]N\s+EXENTA\s+(?:N[°ºo.]?\s*)?(?<number>\d+)\s*,?\s+DE\s+(?<day>\d{1,2})\s+DE\s+(?<month>ENERO|FEBRERO|MARZO|ABRIL|MAYO|JUNIO|JULIO|AGOSTO|SEPTIEMBRE|OCTUBRE|NOVIEMBRE|DICIEMBRE)\s+DE\s+(?<year>20\d{2})",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex CorrectionTargetWithDateRegex = new(
        @"RECTIFICA(?:\s*,)?\s+RESOLUCI[ÓO]N\s+EXENTA\s+(?:N[°ºo.]?\s*)?(?<number>\d+)(?:\s*,?\s+DE\s+(?<day>\d{1,2})\s+DE\s+(?<month>ENERO|FEBRERO|MARZO|ABRIL|MAYO|JUNIO|JULIO|AGOSTO|SEPTIEMBRE|OCTUBRE|NOVIEMBRE|DICIEMBRE)\s+DE\s+(?<year>20\d{2}))?",
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

        var candidates = DiscoverYearPdfCandidates(
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
            var candidate = candidates[index];
            var url = candidate.Url;

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

                if (!IsVadIndexResolution(
                        fullText,
                        candidate.ListingText))
                {
                    progress?.Report(
                        $"CNE: omitido (no clasificado como índice VAD): " +
                        $"{Path.GetFileName(new Uri(url).AbsolutePath)}");
                    File.Delete(tempPath);
                    continue;
                }

                var effectiveFrom = ParseEffectiveDate(fullText);
                if (!effectiveFrom.HasValue ||
                    effectiveFrom.Value.Year != year)
                {
                    progress?.Report(
                        $"CNE: omitido por vigencia " +
                        $"{(effectiveFrom.HasValue ? effectiveFrom.Value.ToString("yyyy-MM") : "desconocida")}: " +
                        $"{Path.GetFileName(new Uri(url).AbsolutePath)}");
                    File.Delete(tempPath);
                    continue;
                }

                var isCorrection =
                    IsCorrection(fullText) ||
                    IsCorrection(candidate.ListingText);
                var title = BuildTitle(
                    fullText,
                    candidate.ListingText,
                    effectiveFrom,
                    isCorrection,
                    url);

                var officialIdentity =
                    ParseOfficialResolutionIdentity(
                        candidate.ListingText,
                        fullText,
                        effectiveFrom);
                var correctedIdentity =
                    isCorrection
                        ? ParseCorrectedResolutionIdentity(
                            candidate.ListingText,
                            fullText,
                            officialIdentity.OfficialDate?.Year ??
                            effectiveFrom.Value.Year)
                        : null;

                var publication = new TariffPublicationDiscovery(
                    "CNE_CHILE",
                    "VAD_INDEX",
                    title,
                    url,
                    effectiveFrom,
                    isCorrection,
                    officialIdentity.DocumentNumber,
                    officialIdentity.OfficialDate,
                    correctedIdentity?.DocumentNumber,
                    "CNE_OFFICIAL_LISTING_AND_DOCUMENT");

                var publicationId = _repository.UpsertDiscovery(
                    publication);

                if (correctedIdentity is
                    { DocumentNumber: not null })
                {
                    _repository.UpsertRelation(
                        new TariffPublicationRelationUpsert(
                            publicationId,
                            "CORRECTS",
                            "CNE_CHILE",
                            "VAD_INDEX",
                            correctedIdentity.DocumentNumber,
                            url,
                            BuildCorrectionEvidenceText(
                                candidate.ListingText,
                                officialIdentity,
                                correctedIdentity)));
                }

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

        _repository.ResolveRelationTargets();

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
        var candidates = DiscoverYearPdfCandidates(
            html,
            new Uri(OfficialVadIndexPageUrl),
            year);

        return new CneTariffEvidenceProbeResult(
            (int)response.StatusCode,
            html.Length,
            candidates.Count,
            candidates
                .Select(item => item.Url)
                .Take(5)
                .ToArray());
    }

    public static IReadOnlyList<string> DiscoverYearPdfUrls(
        string html,
        Uri pageUri,
        int year) =>
        DiscoverYearPdfCandidates(
            html,
            pageUri,
            year)
        .Select(item => item.Url)
        .ToArray();

    private static IReadOnlyList<CnePdfCandidate>
        DiscoverYearPdfCandidates(
            string html,
            Uri pageUri,
            int year)
    {
        var result =
            new Dictionary<string, CnePdfCandidate>(
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

            result[uri.ToString()] =
                new CnePdfCandidate(
                    uri.ToString(),
                    ExtractListingContext(
                        html,
                        match.Index));
        }

        return result.Values
            .OrderBy(
                item => item.Url,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static string ExtractListingContext(
        string html,
        int linkIndex)
    {
        var start = Math.Max(
            0,
            linkIndex - 1800);
        var fragment = html.Substring(
            start,
            linkIndex - start);
        var text = WebUtility.HtmlDecode(
            TagRegex.Replace(
                fragment,
                " "));
        text = Regex.Replace(
                text,
                @"\s+",
                " ")
            .Trim();

        var normalized = NormalizeForMatch(text);

        // The current entry begins after the previous entry's Download link.
        // This keeps e.g. "Resolución 380 ... Rectifica Resolución 368 ..."
        // intact instead of accidentally trimming at the referenced 368.
        var lastDownload =
            normalized.LastIndexOf(
                "DESCARGAR",
                StringComparison.Ordinal);
        if (lastDownload >= 0)
        {
            var currentEntry = normalized[
                (lastDownload + "DESCARGAR".Length)..]
                .Trim();
            if (!string.IsNullOrWhiteSpace(currentEntry))
                return currentEntry;
        }

        var firstResolution =
            normalized.IndexOf(
                "RESOLUCION EXENTA",
                StringComparison.Ordinal);

        return firstResolution >= 0
            ? normalized[firstResolution..]
            : normalized;
    }

    private static bool IsVadIndexResolution(
        string text,
        string listingText)
    {
        var normalized = NormalizeForMatch(text);
        var listingNormalized =
            NormalizeForMatch(listingText);
        var isCorrection =
            IsCorrectionNormalized(normalized) ||
            IsCorrectionNormalized(listingNormalized);

        if (isCorrection)
        {
            // Official correction resolutions may fragment the standard
            // descriptive sentence under PDF text extraction. Require the
            // correction relationship plus the replacement index-table
            // evidence and an explicit effective month instead.
            var hasReplacementIndexTable =
                normalized.Contains("IPC", StringComparison.Ordinal) &&
                normalized.Contains("CPI", StringComparison.Ordinal) &&
                normalized.Contains("DOLAR OBSERVADO", StringComparison.Ordinal) &&
                EffectiveTableMonthRegex.IsMatch(text);

            var hasVadFormulaLanguage =
                normalized.Contains("INDICES", StringComparison.Ordinal) &&
                normalized.Contains("FORMULAS TARIFARIAS", StringComparison.Ordinal);

            var listingConfirmsVadCorrection =
                listingNormalized.Contains(
                    "RECTIFICA",
                    StringComparison.Ordinal) &&
                listingNormalized.Contains(
                    "INDICES",
                    StringComparison.Ordinal) &&
                listingNormalized.Contains(
                    "FORMULAS TARIFARIAS",
                    StringComparison.Ordinal);

            return hasReplacementIndexTable ||
                   hasVadFormulaLanguage ||
                   listingConfirmsVadCorrection;
        }

        var hasVadIndexPhrase =
            normalized.Contains(
                "INDICES CONTENIDOS EN LAS FORMULAS TARIFARIAS",
                StringComparison.Ordinal);
        if (!hasVadIndexPhrase)
            return false;

        return normalized.Contains(
                   "DECRETO",
                   StringComparison.Ordinal) &&
               Regex.IsMatch(
                   normalized,
                   @"5\s*T",
                   RegexOptions.CultureInvariant) &&
               normalized.Contains(
                   "PERIODO COMPRENDIDO",
                   StringComparison.Ordinal);
    }

    private static bool IsCorrection(
        string text) =>
        IsCorrectionNormalized(
            NormalizeForMatch(text));

    private static bool IsCorrectionNormalized(
        string normalized) =>
        normalized.Contains(
            "RECTIFICA",
            StringComparison.Ordinal) &&
        normalized.Contains(
            "RESOLUCION",
            StringComparison.Ordinal);

    private static string NormalizeForMatch(
        string value)
    {
        var decomposed = value.Normalize(
            NormalizationForm.FormD);
        var builder = new StringBuilder(
            decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                char.IsWhiteSpace(character)
                    ? ' '
                    : char.ToUpperInvariant(character));
        }

        return Regex.Replace(
                builder
                    .ToString()
                    .Normalize(NormalizationForm.FormC),
                @"\s+",
                " ")
            .Trim();
    }

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

    private static OfficialResolutionIdentity
        ParseOfficialResolutionIdentity(
            string listingText,
            string documentText,
            DateOnly? effectiveFrom)
    {
        var listing =
            ListingResolutionWithDateRegex.Match(
                listingText);
        if (listing.Success)
        {
            var date = ParseSpanishDate(
                listing.Groups["day"].Value,
                listing.Groups["month"].Value,
                listing.Groups["year"].Value);
            var number =
                listing.Groups["number"].Value;
            return new OfficialResolutionIdentity(
                BuildCanonicalResolutionNumber(
                    number,
                    date?.Year ??
                    effectiveFrom?.Year),
                date);
        }

        var listingNumber =
            ResolutionNumberRegex.Match(
                listingText);
        if (listingNumber.Success)
        {
            return new OfficialResolutionIdentity(
                BuildCanonicalResolutionNumber(
                    listingNumber.Groups["number"].Value,
                    effectiveFrom?.Year),
                null);
        }

        var documentNumber =
            CurrentResolutionNumberRegex.Match(
                documentText);
        if (documentNumber.Success)
        {
            return new OfficialResolutionIdentity(
                BuildCanonicalResolutionNumber(
                    documentNumber.Groups["number"].Value,
                    effectiveFrom?.Year),
                null);
        }

        return new OfficialResolutionIdentity(
            null,
            null);
    }

    private static OfficialResolutionIdentity?
        ParseCorrectedResolutionIdentity(
            string listingText,
            string documentText,
            int fallbackYear)
    {
        var match =
            CorrectionTargetWithDateRegex.Match(
                listingText);
        if (!match.Success)
        {
            match =
                CorrectionTargetWithDateRegex.Match(
                    documentText);
        }

        if (!match.Success)
            return null;

        var date =
            match.Groups["year"].Success
                ? ParseSpanishDate(
                    match.Groups["day"].Value,
                    match.Groups["month"].Value,
                    match.Groups["year"].Value)
                : null;

        var canonical =
            BuildCanonicalResolutionNumber(
                match.Groups["number"].Value,
                date?.Year ?? fallbackYear);

        return string.IsNullOrWhiteSpace(canonical)
            ? null
            : new OfficialResolutionIdentity(
                canonical,
                date);
    }

    private static DateOnly? ParseSpanishDate(
        string dayText,
        string monthText,
        string yearText)
    {
        if (!int.TryParse(
                dayText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var day) ||
            !int.TryParse(
                yearText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var year))
        {
            return null;
        }

        var month =
            MonthNumber(
                monthText);
        if (!month.HasValue)
            return null;

        try
        {
            return new DateOnly(
                year,
                month.Value,
                day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? BuildCanonicalResolutionNumber(
        string number,
        int? year)
    {
        if (string.IsNullOrWhiteSpace(number) ||
            !year.HasValue)
        {
            return null;
        }

        return $"REX-{number.Trim()}-{year.Value}";
    }

    private static string BuildCorrectionEvidenceText(
        string listingText,
        OfficialResolutionIdentity source,
        OfficialResolutionIdentity target)
    {
        var listing =
            Regex.Replace(
                listingText,
                @"\s+",
                " ")
            .Trim();

        var prefix =
            $"{source.DocumentNumber ?? "CNE resolution"} " +
            $"CORRECTS {target.DocumentNumber}";

        return string.IsNullOrWhiteSpace(listing)
            ? prefix
            : $"{prefix}. Official listing: {listing}";
    }

    private static string BuildTitle(
        string text,
        string listingText,
        DateOnly? effectiveFrom,
        bool isCorrection,
        string sourceUrl)
    {
        var listingNumberMatch =
            ResolutionNumberRegex.Match(listingText);
        var currentNumberMatch =
            CurrentResolutionNumberRegex.Match(text);
        var fallbackNumberMatch =
            ResolutionNumberRegex.Match(text);
        var numberMatch = listingNumberMatch.Success
            ? listingNumberMatch
            : currentNumberMatch.Success
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
    private sealed record OfficialResolutionIdentity(
        string? DocumentNumber,
        DateOnly? OfficialDate);

    private sealed record CnePdfCandidate(
        string Url,
        string ListingText);
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

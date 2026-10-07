using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SolarOfThings.Core.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Controlled fallback for official Enel PDFs obtained by the user through a
/// normal browser. It does not automate or bypass Enel's browser protection.
/// </summary>
public sealed class EnelTariffPdfImportService
{
    private static readonly Regex YearRegex = new(
        @"(?<!\d)(?<year>20\d{2})(?!\d)",
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex BrowserDuplicateSuffixRegex = new(
        @"\s+\(\d+\)(?=\.pdf$)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private readonly TariffPublicationRepository _repository;
    private readonly EnelTariffNormalizationService _normalization;
    private readonly AppPaths _paths;

    public EnelTariffPdfImportService(
        TariffPublicationRepository repository,
        EnelTariffNormalizationService normalization,
        AppPaths paths)
    {
        _repository = repository;
        _normalization = normalization;
        _paths = paths;
    }

    public async Task<EnelTariffPdfImportResult> ImportAsync(
        IReadOnlyList<string> files,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        var failed = 0;
        var normalizedCandidates = 0;
        var messages = new List<string>();
        var receipts = new List<EnelTariffPdfImportReceipt>();

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = files[index];

            try
            {
                progress?.Report(
                    $"Importando PDF Enel {index + 1}/{files.Count}: " +
                    $"{Path.GetFileName(sourcePath)}...");

                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException(
                        "The selected PDF no longer exists.",
                        sourcePath);

                var title = CanonicalOfficialTitle(
                    Path.GetFileName(sourcePath));
                if (!title.Contains(
                        "Tarifas Suministro Eléctrico",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "El archivo no parece ser un cuadro oficial 'Tarifas Suministro Eléctrico' de Enel.");
                }

                var year = ParseYear(title)
                    ?? throw new InvalidDataException(
                        "No se pudo determinar el año desde el nombre del PDF.");

                var bytes = await File.ReadAllBytesAsync(
                    sourcePath,
                    cancellationToken);

                ValidatePdf(bytes);

                var pageTexts = ExtractPageText(sourcePath);
                var joined = string.Join(
                    "\n",
                    pageTexts);

                if (!joined.Contains(
                        "Cargo fijo mensual",
                        StringComparison.OrdinalIgnoreCase) ||
                    !joined.Contains(
                        "BT1",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "El PDF no contiene la evidencia BT1 esperada de un tarifario Enel.");
                }

                var effectiveFrom = ParseEffectiveDate(
                    title,
                    year);
                var isRetroactive = title.Contains(
                    "Retroactivo",
                    StringComparison.OrdinalIgnoreCase);

                var sha = Convert.ToHexString(
                        SHA256.HashData(bytes))
                    .ToLowerInvariant();

                var existing = _repository.GetAll()
                    .FirstOrDefault(item =>
                        string.Equals(
                            item.Provider,
                            "ENEL_DISTRIBUCION_CHILE",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            item.Category,
                            "SUPPLY_REGULATED",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            item.Title,
                            title,
                            StringComparison.OrdinalIgnoreCase));

                var publicationId = existing?.PublicationId
                    ?? _repository.UpsertDiscovery(
                        new TariffPublicationDiscovery(
                            "ENEL_DISTRIBUCION_CHILE",
                            "SUPPLY_REGULATED",
                            title,
                            $"enel-official-user-import://sha256/{sha}",
                            effectiveFrom,
                            isRetroactive));

                var targetDir = Path.Combine(
                    _paths.TariffEnelDirectory,
                    year.ToString());
                Directory.CreateDirectory(targetDir);

                var targetPath = Path.Combine(
                    targetDir,
                    SafeFileName(title));

                await File.WriteAllBytesAsync(
                    targetPath,
                    bytes,
                    cancellationToken);

                _repository.MarkCaptured(
                    publicationId,
                    targetPath,
                    sha,
                    bytes.LongLength,
                    pageTexts.Count,
                    pageTexts);

                var normalization =
                    _normalization.NormalizePublication(
                        publicationId,
                        pageTexts);

                var outcome = existing is null
                    ? "NEW_PUBLICATION"
                    : string.Equals(
                        existing.ContentSha256,
                        sha,
                        StringComparison.OrdinalIgnoreCase)
                        ? "EXISTING_IDENTICAL_REIMPORT"
                        : string.IsNullOrWhiteSpace(
                            existing.ContentSha256)
                            ? "CAPTURED_EXISTING_DISCOVERY"
                            : "UPDATED_EXISTING_PUBLICATION";

                receipts.Add(
                    new EnelTariffPdfImportReceipt(
                        title,
                        publicationId,
                        sha,
                        pageTexts.Count,
                        normalization.CandidateCount,
                        outcome,
                        DateTimeOffset.UtcNow));

                normalizedCandidates +=
                    normalization.CandidateCount;
                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add(
                    $"{Path.GetFileName(sourcePath)}: {ex.Message}");
            }
        }

        progress?.Report(
            $"Importación Enel terminada: {imported}/{files.Count} PDF(s), " +
            $"{normalizedCandidates} candidatos tarifarios, {failed} fallo(s).");

        return new EnelTariffPdfImportResult(
            imported,
            failed,
            normalizedCandidates,
            messages,
            receipts);
    }

    private static string CanonicalOfficialTitle(
        string fileName) =>
        BrowserDuplicateSuffixRegex.Replace(
            fileName,
            string.Empty);

    private static void ValidatePdf(
        byte[] bytes)
    {
        if (bytes.Length < 5 ||
            bytes[0] != (byte)'%' ||
            bytes[1] != (byte)'P' ||
            bytes[2] != (byte)'D' ||
            bytes[3] != (byte)'F' ||
            bytes[4] != (byte)'-')
        {
            throw new InvalidDataException(
                "El archivo seleccionado no es un PDF válido.");
        }
    }

    private static int? ParseYear(
        string title)
    {
        var matches = YearRegex.Matches(title);
        if (matches.Count == 0)
            return null;

        return int.TryParse(
            matches[^1].Groups["year"].Value,
            out var year)
                ? year
                : null;
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
                return new DateOnly(
                    year,
                    pair.Value,
                    1);
            }
        }

        return null;
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
}

public sealed record EnelTariffPdfImportReceipt(
    string OfficialFileName,
    long PublicationId,
    string Sha256,
    int PageCount,
    int NormalizedCandidates,
    string Outcome,
    DateTimeOffset CompletedUtc);

public sealed record EnelTariffPdfImportResult(
    int Imported,
    int Failed,
    int NormalizedCandidates,
    IReadOnlyList<string> Messages,
    IReadOnlyList<EnelTariffPdfImportReceipt> Receipts);

using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SolarOfThings.Core.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Conservative assistant for Enel utility-bill ingestion.
/// It preserves the original PDF and only pre-fills values found with
/// high-confidence textual patterns. The user remains the final reviewer.
/// </summary>
public sealed partial class EnelUtilityBillPdfImportService
{
    public const string ParserVersion = "enel-utility-bill.v2";

    private readonly AppPaths _paths;

    public EnelUtilityBillPdfImportService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<UtilityBillPdfDraft> PrepareDraftAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("La boleta seleccionada no existe.", sourcePath);

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        if (bytes.Length < 5 ||
            bytes[0] != (byte)'%' ||
            bytes[1] != (byte)'P' ||
            bytes[2] != (byte)'D' ||
            bytes[3] != (byte)'F' ||
            bytes[4] != (byte)'-')
        {
            throw new InvalidDataException("El archivo seleccionado no es un PDF válido.");
        }

        var sha = Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();

        var pages = ExtractPages(sourcePath);
        var joined = string.Join("\n", pages.Select(item => item.Text));
        if (!joined.Contains("Enel", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "El PDF no contiene una referencia Enel reconocible; se evita clasificarlo automáticamente como boleta Enel.");
        }

        var period = ParsePeriod(joined);
        var kwh = ParseContextNumber(joined, ["consumo", "energía", "energia"], "kwh");
        var taxable = ParseMoneyAfterLabels(
            joined,
            ["monto afecto", "total afecto", "subtotal afecto"]);
        var iva = ParseMoneyAfterLabels(
            joined,
            ["iva 19%", "i.v.a. 19%", "iva"]);
        var exempt = ParseMoneyAfterLabels(
            joined,
            ["monto exento", "total exento", "subtotal exento"]);
        var total = ParseMoneyAfterLabels(
            joined,
            ["total a pagar", "total cuenta", "total boleta"]);
        var tariff = TariffRegex().Match(joined) is { Success: true } tm
            ? tm.Value.ToUpperInvariant()
            : null;

        var year = period.Start?.Year ??
                   period.End?.Year ??
                   DateTime.Now.Year;
        var targetDir = Path.Combine(
            _paths.UtilityBillEnelDirectory,
            year.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(targetDir);

        var safeName = SafeFileName(Path.GetFileName(sourcePath));
        var targetPath = Path.Combine(
            targetDir,
            $"{sha[..12]}-{safeName}");
        await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);

        var lines = ParseKnownLines(pages);
        var warnings = new List<string>();
        if (!period.Start.HasValue || !period.End.HasValue)
            warnings.Add("No se pudo detectar con confianza el período completo; revísalo manualmente.");
        if (!kwh.HasValue)
            warnings.Add("No se pudo detectar con confianza el consumo kWh; ingrésalo o confírmalo manualmente.");
        if (!total.HasValue)
            warnings.Add("No se pudo detectar con confianza el total a pagar; ingrésalo o confírmalo manualmente.");
        if (lines.Count == 0)
            warnings.Add("No se extrajeron líneas de cargo con confianza; usa el ingreso manual de líneas.");

        return new UtilityBillPdfDraft(
            sourcePath,
            targetPath,
            Path.GetFileName(sourcePath),
            sha,
            bytes.LongLength,
            pages.Count,
            ParserVersion,
            joined,
            period.Start,
            period.End,
            kwh,
            taxable,
            iva,
            exempt,
            total,
            tariff,
            lines,
            warnings);
    }

    private static IReadOnlyList<(int Page, string Text)> ExtractPages(string path)
    {
        var result = new List<(int, string)>();
        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
        {
            result.Add((
                page.Number,
                ContentOrderTextExtractor.GetText(page)));
        }
        return result;
    }

    private static (DateOnly? Start, DateOnly? End) ParsePeriod(string text)
    {
        foreach (Match match in PeriodRegex().Matches(text))
        {
            if (TryDate(match.Groups["start"].Value, out var start) &&
                TryDate(match.Groups["end"].Value, out var end) &&
                end > start &&
                (end.DayNumber - start.DayNumber) <= 60)
            {
                return (start, end);
            }
        }

        foreach (var rawLine in SplitLines(text))
        {
            var line = rawLine.Trim();
            if (!line.Contains(
                    "period",
                    StringComparison.OrdinalIgnoreCase) &&
                !line.Contains(
                    "lectura",
                    StringComparison.OrdinalIgnoreCase) &&
                !line.Contains(
                    "consumo",
                    StringComparison.OrdinalIgnoreCase) &&
                !line.Contains(
                    "desde",
                    StringComparison.OrdinalIgnoreCase) &&
                !line.Contains(
                    "hasta",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dates = DateRegex().Matches(line)
                .Select(match => match.Value)
                .Select(raw =>
                    TryDate(
                        raw,
                        out var date)
                        ? date
                        : (DateOnly?)null)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .Distinct()
                .OrderBy(date => date)
                .ToArray();

            for (var i = 0;
                 i < dates.Length - 1;
                 i++)
            {
                var days =
                    dates[i + 1].DayNumber -
                    dates[i].DayNumber;
                if (days is >= 20 and <= 45)
                    return (
                        dates[i],
                        dates[i + 1]);
            }
        }

        return (null, null);
    }

    private static double? ParseContextNumber(
        string text,
        IReadOnlyList<string> labels,
        string unit)
    {
        foreach (var rawLine in SplitLines(text))
        {
            var line = rawLine.Trim();
            if (!labels.Any(label =>
                    line.Contains(label, StringComparison.OrdinalIgnoreCase)) ||
                !line.Contains(unit, StringComparison.OrdinalIgnoreCase))
                continue;

            var matches =
                KwhValueRegex().Matches(line);
            foreach (Match match in matches.Reverse())
            {
                if (TryParseChileNumber(
                        match.Groups["value"].Value,
                        out var value) &&
                    value >= 0 &&
                    value < 100000)
                {
                    return value;
                }
            }
        }
        return null;
    }

    private static double? ParseMoneyAfterLabels(
        string text,
        IReadOnlyList<string> labels)
    {
        foreach (var rawLine in SplitLines(text))
        {
            var line = rawLine.Trim();
            if (!labels.Any(label =>
                    line.Contains(label, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (TryExtractPrintedMoney(
                    line,
                    out var value) &&
                value >= 0)
            {
                return value;
            }
        }
        return null;
    }

    private static IReadOnlyList<UtilityBillPdfDraftLine> ParseKnownLines(
        IReadOnlyList<(int Page, string Text)> pages)
    {
        var definitions = new[]
        {
            ("Electricidad consumida", "SERVICIO_ELECTRICO", UtilityBillLineCategory.ElectricityConsumed),
            ("Transporte de electricidad", "SERVICIO_ELECTRICO", UtilityBillLineCategory.ElectricityTransport),
            ("Cargo fijo", "SERVICIO_ELECTRICO", UtilityBillLineCategory.FixedMonthly),
            ("Subsidio Eléctrico", "OTROS_CARGOS", UtilityBillLineCategory.Subsidy),
            ("Subsidio Electrico", "OTROS_CARGOS", UtilityBillLineCategory.Subsidy),
            ("Administración del servicio", "OTROS_CARGOS", UtilityBillLineCategory.ServiceAdministration),
            ("Administracion del servicio", "OTROS_CARGOS", UtilityBillLineCategory.ServiceAdministration),
            ("Arriendo Medidor", "OTROS_CARGOS", UtilityBillLineCategory.MeterRental),
            ("Servicio Común", "OTROS_CARGOS", UtilityBillLineCategory.CommonService),
            ("Servicio Comun", "OTROS_CARGOS", UtilityBillLineCategory.CommonService),
            ("Ajuste", "ACUMULADO", UtilityBillLineCategory.SimpleAdjustment)
        };

        var result = new List<UtilityBillPdfDraftLine>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in pages)
        {
            foreach (var rawLine in SplitLines(page.Text))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                foreach (var definition in definitions)
                {
                    if (!line.Contains(
                            definition.Item1,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!TryExtractPrintedMoney(
                            line,
                            out var amount))
                    {
                        continue;
                    }

                    if (line.Contains("-", StringComparison.Ordinal) ||
                        line.Contains("descuento", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("subsidio", StringComparison.OrdinalIgnoreCase))
                    {
                        amount = -Math.Abs(amount);
                    }

                    var key = $"{definition.Item3}|{amount:N4}|{page.Page}";
                    if (!seen.Add(key))
                        continue;

                    result.Add(new UtilityBillPdfDraftLine(
                        definition.Item2,
                        definition.Item3,
                        definition.Item1,
                        amount,
                        page.Page,
                        line));
                    break;
                }
            }
        }

        return result;
    }

    private static bool TryExtractPrintedMoney(
        string line,
        out double amount)
    {
        amount = 0;

        // Prefer an explicit currency marker. This is the safest shape in
        // extracted bill text and prevents counters such as "(4/6)" from
        // being mistaken for pesos.
        var currencyMatches =
            CurrencyMoneyRegex().Matches(line);
        if (currencyMatches.Count > 0)
        {
            return TryParseChileNumber(
                currencyMatches[^1]
                    .Groups["value"].Value,
                out amount);
        }

        // Some PDF text extractors detach the "$" glyph. Accept only a
        // plausible CLP amount at the physical end of the line: at least
        // three digits, or a dotted-thousands representation. One/two-digit
        // counters and installment markers are deliberately rejected.
        var trailing =
            TrailingMoneyRegex().Match(line);
        return trailing.Success &&
               TryParseChileNumber(
                   trailing.Groups["value"].Value,
                   out amount);
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static bool TryDate(string raw, out DateOnly date) =>
        DateOnly.TryParseExact(
            raw.Trim().Replace('/', '-'),
            "dd-MM-yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static bool TryParseChileNumber(string raw, out double value)
    {
        var normalized = raw
            .Replace("$", string.Empty)
            .Replace(" ", string.Empty)
            .Trim();

        if (normalized.Contains(','))
        {
            normalized = normalized
                .Replace(".", string.Empty)
                .Replace(',', '.');
        }
        else if (Regex.IsMatch(normalized, @"^[-+]?\d{1,3}(\.\d{3})+$"))
        {
            normalized = normalized.Replace(".", string.Empty);
        }

        return double.TryParse(
            normalized,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string SafeFileName(string value)
    {
        var result = value;
        foreach (var invalid in Path.GetInvalidFileNameChars())
            result = result.Replace(invalid, '_');
        return result;
    }

    [GeneratedRegex(
        @"(?<start>\d{2}[/-]\d{2}[/-]20\d{2})\s*(?:a|al|hasta|[-–—→])\s*(?<end>\d{2}[/-]\d{2}[/-]20\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PeriodRegex();

    [GeneratedRegex(
        @"\b\d{2}[/-]\d{2}[/-]20\d{2}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();

    [GeneratedRegex(
        @"(?<value>\d{1,3}(?:\.\d{3})*(?:,\d+)?|\d+(?:[.,]\d+)?)\s*kWh\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KwhValueRegex();

    [GeneratedRegex(
        @"\$\s*(?<value>[-+]?\s*(?:\d{1,3}(?:\.\d{3})+|\d+)(?:,\d+)?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyMoneyRegex();

    [GeneratedRegex(
        @"(?<value>[-+]?\s*(?:\d{1,3}(?:\.\d{3})+|\d{3,})(?:,\d+)?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingMoneyRegex();

    [GeneratedRegex(
        @"\bBT1(?:[-\s][A-Z0-9]+)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TariffRegex();
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Conservative text parser for the BT1 evidence found in Enel's born-digital
/// supply-tariff PDFs. It extracts candidates only; it never decides that a
/// candidate applies to a specific customer/bill.
/// </summary>
public sealed class EnelBt1TariffTextParser
{
    public const string ParserVersion = "enel-bt1-text-v1";

    private static readonly Regex NumberRegex = new(
        @"(?<![A-Za-z0-9])(?<value>d+(?:[.,]d+)?)(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RedEtrRegex = new(
        @"^(?<network>BT_(?:AA|SA|AS|SS))s+(?<etr>T[1-6])s+(?<values>.+)$",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private static readonly ComponentDefinition[] ComponentDefinitions =
    [
        new(
            "FIXED_MONTHLY",
            ["Cargo fijo mensual"],
            "$/mes"),
        new(
            "PUBLIC_SERVICE",
            ["Cargo por servicio público", "Cargo por servicio publico"],
            "$/kWh"),
        new(
            "TRANSMISSION_SYSTEM_USE",
            ["Cargo por uso de sistema de transmisión", "Cargo por uso de sistema de transmision"],
            "$/kWh"),
        new(
            "TRANSMISSION_NATIONAL_INTERCONNECTION",
            ["Cargo transmisión nacional interconexión", "Cargo transmision nacional interconexion"],
            "$/kWh"),
        new(
            "TRANSMISSION_ZONAL_C",
            ["Cargo transmisión zonal sistema C", "Cargo transmision zonal sistema C"],
            "$/kWh"),
        new(
            "TRANSMISSION_ZONAL_D",
            ["Cargo transmisión zonal sistema D", "Cargo transmision zonal sistema D"],
            "$/kWh"),
        new(
            "TRANSMISSION_DEDICATED",
            ["Cargo transmisión dedicado", "Cargo transmision dedicado"],
            "$/kWh"),
        new(
            "ELECTRICITY_TRANSPORT",
            ["Transporte de electricidad"],
            "$/kWh"),
        new(
            "ENERGY_CHARGE",
            ["Cargo por energía", "Cargo por energia"],
            "$/kWh"),
        new(
            "POWER_PURCHASE",
            ["Cargo por compras de potencia"],
            "$/kWh")
    ];

    public IReadOnlyList<ParsedTariffRateCandidate> ParsePages(
        IReadOnlyList<string> pageTexts)
    {
        var result = new List<ParsedTariffRateCandidate>();

        for (var pageIndex = 0; pageIndex < pageTexts.Count; pageIndex++)
        {
            var pageText = pageTexts[pageIndex] ?? string.Empty;
            if (!LooksLikeBt1Evidence(pageText))
                continue;

            result.AddRange(
                ParsePage(
                    pageIndex + 1,
                    pageText));
        }

        return result;
    }

    private static IReadOnlyList<ParsedTariffRateCandidate> ParsePage(
        int pageNumber,
        string pageText)
    {
        var result = new List<ParsedTariffRateCandidate>();
        var lines = SplitLines(pageText);
        PendingComponent? pending = null;
        var redEtrOccurrences = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var blockContext = "RED_ETR_RATE_BLOCK";

        foreach (var rawLine in lines)
        {
            var line = CollapseWhitespace(rawLine);
            if (line.Length == 0)
                continue;

            if (ContainsAny(
                    line,
                    ["TOTAL TARIFA BASE BT1", "TOTAL TARIFA BASE BT 1"]))
            {
                blockContext = "TOTAL_BT1_BASE_RAW";
            }
            else if (ContainsAny(
                         line,
                         ["Cargo por potencia base", "CARGO POR POTENCIA BASE"]))
            {
                blockContext = "POWER_BASE_RAW";
            }

            var redMatch = RedEtrRegex.Match(line);
            if (redMatch.Success)
            {
                var network = redMatch.Groups["network"].Value.ToUpperInvariant();
                var etr = redMatch.Groups["etr"].Value.ToUpperInvariant();
                var values = ParsePublishedPairs(
                    redMatch.Groups["values"].Value);

                if (values.Count > 0)
                {
                    var occurrenceKey = $"{network}|{etr}";
                    redEtrOccurrences.TryGetValue(
                        occurrenceKey,
                        out var occurrence);
                    occurrence++;
                    redEtrOccurrences[occurrenceKey] = occurrence;

                    var componentKey =
                        blockContext == "RED_ETR_RATE_BLOCK"
                            ? $"RED_ETR_RATE_BLOCK_{occurrence}"
                            : blockContext;

                    AddPairs(
                        result,
                        pageNumber,
                        componentKey,
                        $"{network} {etr}",
                        "$/kWh",
                        network,
                        etr,
                        values,
                        line);
                }

                continue;
            }

            if (pending is not null)
            {
                pending.Source.Append(' ').Append(line);

                if (TryParseComponentValues(
                        line,
                        pending.Definition.Unit,
                        out var pendingValues))
                {
                    AddPairs(
                        result,
                        pageNumber,
                        pending.Definition.ComponentKey,
                        pending.Source.ToString(),
                        pending.Definition.Unit,
                        null,
                        null,
                        pendingValues,
                        pending.Source.ToString());
                    pending = null;
                    continue;
                }

                if (pending.Source.Length > 800)
                    pending = null;
            }

            var definition = ComponentDefinitions.FirstOrDefault(
                candidate => candidate.Labels.Any(
                    label => line.Contains(
                        label,
                        StringComparison.OrdinalIgnoreCase)));

            if (definition is null)
                continue;

            if (TryParseComponentValues(
                    line,
                    definition.Unit,
                    out var valuesOnLine))
            {
                AddPairs(
                    result,
                    pageNumber,
                    definition.ComponentKey,
                    line,
                    definition.Unit,
                    null,
                    null,
                    valuesOnLine,
                    line);
            }
            else
            {
                pending = new PendingComponent(
                    definition,
                    new StringBuilder(line));
            }
        }

        return result;
    }

    private static bool LooksLikeBt1Evidence(string text)
    {
        if (!text.Contains(
                "Cargo fijo mensual",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var redRows = Regex.Matches(
            text,
            @"BT_(?:AA|SA|AS|SS)s+T[1-6]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return redRows.Count >= 4;
    }

    private static bool TryParseComponentValues(
        string line,
        string unit,
        out IReadOnlyList<(double Net, double Iva)> values)
    {
        values = [];

        var unitIndex = IndexOfUnit(line, unit);
        if (unitIndex < 0)
            return false;

        var unitEnd = line.IndexOf(')', unitIndex);
        if (unitEnd < 0 || unitEnd + 1 >= line.Length)
            return false;

        var tail = line[(unitEnd + 1)..];
        var parsed = ParsePublishedPairs(tail);
        if (parsed.Count == 0)
            return false;

        values = parsed;
        return true;
    }

    private static int IndexOfUnit(string line, string unit)
    {
        var compact = unit.Replace(" ", string.Empty);
        var candidates = unit switch
        {
            "$/mes" => new[] { "($/mes)", "$/mes" },
            "$/kWh" => new[] { "($/kWh)", "($/KWh)", "$/kWh", "$/KWh" },
            _ => new[] { $"({compact})", compact }
        };

        foreach (var candidate in candidates)
        {
            var index = line.IndexOf(
                candidate,
                StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return index;
        }

        return -1;
    }

    private static IReadOnlyList<(double Net, double Iva)>
        ParsePublishedPairs(string text)
    {
        var values = NumberRegex
            .Matches(text)
            .Select(match => ParseChileanDecimal(
                match.Groups["value"].Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (values.Length < 2)
            return [];

        var result = new List<(double Net, double Iva)>();
        for (var index = 0; index + 1 < values.Length; index += 2)
        {
            result.Add((values[index], values[index + 1]));
        }

        return result;
    }

    private static double? ParseChileanDecimal(string text)
    {
        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : null;
    }

    private static void AddPairs(
        ICollection<ParsedTariffRateCandidate> target,
        int pageNumber,
        string componentKey,
        string printedDescription,
        string? unit,
        string? networkType,
        string? etrBand,
        IReadOnlyList<(double Net, double Iva)> values,
        string sourceText)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var pair = values[index];
            target.Add(new ParsedTariffRateCandidate(
                pageNumber,
                "BT1",
                componentKey,
                printedDescription,
                unit,
                networkType,
                etrBand,
                index,
                pair.Net,
                pair.Iva,
                sourceText,
                ParserVersion,
                "EXTRACTED_UNAPPLIED"));
        }
    }

    private static IReadOnlyList<string> SplitLines(string text) =>
        text.Replace("
", "
", StringComparison.Ordinal)
            .Replace('', '
')
            .Split(
                '
',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(
            value,
            @"s+",
            " ",
            RegexOptions.CultureInvariant)
        .Trim();

    private static bool ContainsAny(
        string value,
        IEnumerable<string> candidates) =>
        candidates.Any(
            candidate => value.Contains(
                candidate,
                StringComparison.OrdinalIgnoreCase));

    private sealed record ComponentDefinition(
        string ComponentKey,
        IReadOnlyList<string> Labels,
        string Unit);

    private sealed record PendingComponent(
        ComponentDefinition Definition,
        StringBuilder Source);
}

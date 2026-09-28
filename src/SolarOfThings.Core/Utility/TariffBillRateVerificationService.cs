using System.Globalization;
using System.Text;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Conservatively verifies bill-line unit rates against normalized official
/// tariff candidates. It never infers customer RED/ETR/commune applicability.
/// </summary>
public sealed class TariffBillRateVerificationService
{
    private const double AbsoluteRateTolerance = 0.001;

    private readonly UtilityMeterRepository _utilityRepository;
    private readonly TariffPublicationRepository _publicationRepository;
    private readonly TariffRateCandidateRepository _candidateRepository;
    private readonly TariffPublicationVersionResolver _versionResolver;

    public TariffBillRateVerificationService(
        UtilityMeterRepository utilityRepository,
        TariffPublicationRepository publicationRepository,
        TariffRateCandidateRepository candidateRepository,
        TariffPublicationVersionResolver versionResolver)
    {
        _utilityRepository = utilityRepository;
        _publicationRepository = publicationRepository;
        _candidateRepository = candidateRepository;
        _versionResolver = versionResolver;
    }

    public IReadOnlyList<BillLineTariffVerification> VerifyBill(
        long billId,
        string timeZoneId)
    {
        var bill = _utilityRepository.GetBills()
            .SingleOrDefault(item => item.BillId == billId)
            ?? throw new InvalidOperationException("Bill was not found.");

        var lines = _utilityRepository.GetBillLines(billId);
        if (lines.Count == 0)
            return [];

        var selection = ResolvePublicationsForInterval(
            bill,
            timeZoneId);

        return lines
            .Select(line => VerifyLine(line, selection))
            .ToArray();
    }

    private BillLineTariffVerification VerifyLine(
        UtilityBillLine line,
        TariffPeriodSelection selection)
    {
        var componentKey = MapComponentKey(line);

        if (componentKey is null)
        {
            return Result(
                line,
                null,
                "ACTUAL_ONLY_UNMAPPED",
                "No conservative tariff-component mapping exists for this bill line.");
        }

        if (!line.UnitRateClp.HasValue)
        {
            return Result(
                line,
                componentKey,
                "ACTUAL_ONLY_NO_UNIT_RATE",
                "The stored bill line has no printed unit rate to verify.");
        }

        if (selection.Status != "RESOLVED")
        {
            return Result(
                line,
                componentKey,
                selection.Status,
                selection.Detail,
                publicationIds: selection.Publications.Select(item => item.PublicationId).ToArray(),
                publications: selection.Publications);
        }

        var perPublicationMatches =
            new List<(long PublicationId, IReadOnlyList<RateCandidateMatch> Matches)>();

        foreach (var publication in selection.Publications)
        {
            var publicationId = publication.PublicationId;
            var candidates = _candidateRepository
                .GetForPublication(publicationId)
                .Where(candidate =>
                    string.Equals(
                        candidate.TariffPlan,
                        "BT1",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.ComponentKey,
                        componentKey,
                        StringComparison.Ordinal))
                .ToArray();

            if (candidates.Length == 0)
            {
                return Result(
                    line,
                    componentKey,
                    "MISSING_COMPONENT_SOURCE",
                    $"Publication {publicationId} contains no normalized {componentKey} candidate.",
                    publicationIds: selection.Publications.Select(item => item.PublicationId).ToArray(),
                    publications: selection.Publications);
            }

            var matches = candidates
                .SelectMany(candidate => CandidateMatches(
                    candidate,
                    line.UnitRateClp.Value))
                .ToArray();

            if (matches.Length == 0)
            {
                return Result(
                    line,
                    componentKey,
                    "RATE_NOT_FOUND",
                    $"The printed unit rate {line.UnitRateClp.Value.ToString("N3", CultureInfo.InvariantCulture)} was not found for {componentKey} in publication {publicationId}.",
                    publicationIds: selection.Publications.Select(item => item.PublicationId).ToArray(),
                    publications: selection.Publications);
            }

            perPublicationMatches.Add(
                (publicationId, matches));
        }

        var allMatches = perPublicationMatches
            .SelectMany(item => item.Matches)
            .ToArray();

        var distinctCandidateIdentities = allMatches
            .Select(match =>
                $"{match.NetworkType ?? "?"}|{match.EtrBand ?? "?"}|{match.CandidateIndex}|{match.Column}")
            .Distinct(StringComparer.Ordinal)
            .Count();

        var reconstructed = line.Quantity.HasValue
            ? line.Quantity.Value * line.UnitRateClp.Value
            : (double?)null;

        var amountDifference = reconstructed.HasValue
            ? line.AmountClp - reconstructed.Value
            : (double?)null;

        var status = reconstructed.HasValue
            ? distinctCandidateIdentities == 1
                ? "VERIFIED_RECONSTRUCTED_SOURCE_UNIQUE"
                : "VERIFIED_RECONSTRUCTED_APPLICABILITY_AMBIGUOUS"
            : distinctCandidateIdentities == 1
                ? "RATE_VERIFIED_SOURCE_UNIQUE"
                : "RATE_VERIFIED_APPLICABILITY_AMBIGUOUS";

        var detail = BuildMatchDetail(
            allMatches,
            selection.Publications.Count,
            reconstructed,
            line.AmountClp,
            amountDifference);

        return new BillLineTariffVerification(
            line.BillLineId,
            line.Description,
            componentKey,
            line.UnitRateClp,
            line.Quantity,
            line.AmountClp,
            status,
            selection.Publications.Select(item => item.PublicationId).ToArray(),
            selection.Publications,
            allMatches.Length,
            distinctCandidateIdentities,
            allMatches
                .Select(match => match.Column)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            reconstructed,
            amountDifference,
            detail);
    }

    private TariffPeriodSelection ResolvePublicationsForInterval(
        UtilityBillRecord bill,
        string timeZoneId)
    {
        var startDate = SolarApiTime.GetLocalDate(
            bill.PeriodStartUtc,
            timeZoneId);
        var endBoundaryDate = SolarApiTime.GetLocalDate(
            bill.PeriodEndUtc,
            timeZoneId);

        if (endBoundaryDate <= startDate)
        {
            return new TariffPeriodSelection(
                "TARIFF_INTERVAL_INVALID",
                [],
                "The bill interval is invalid after local-date conversion.");
        }

        var allPublications = _publicationRepository
            .GetAll();

        var publications = allPublications
            .Where(item =>
                string.Equals(
                    item.Provider,
                    "ENEL_DISTRIBUCION_CHILE",
                    StringComparison.Ordinal) &&
                string.Equals(
                    item.Category,
                    "SUPPLY_REGULATED",
                    StringComparison.Ordinal) &&
                item.EffectiveFrom.HasValue)
            .ToArray();

        if (publications.Length == 0)
        {
            return new TariffPeriodSelection(
                "MISSING_TARIFF_SOURCE",
                [],
                "No captured Enel regulated-supply publication has a parsed effective date.");
        }

        var resolutions = _versionResolver.Resolve(publications)
            .ToDictionary(item => item.PublicationId);

        var groups = publications
            .GroupBy(item => item.EffectiveFrom!.Value)
            .OrderBy(group => group.Key)
            .ToArray();

        var cneBoundaries = allPublications
            .Where(item =>
                string.Equals(
                    item.Provider,
                    "CNE_CHILE",
                    StringComparison.Ordinal) &&
                string.Equals(
                    item.Category,
                    "VAD_INDEX",
                    StringComparison.Ordinal) &&
                string.Equals(
                    item.CaptureStatus,
                    "CAPTURED",
                    StringComparison.Ordinal) &&
                item.EffectiveFrom.HasValue &&
                item.EffectiveFrom.Value > startDate &&
                item.EffectiveFrom.Value < endBoundaryDate)
            .Select(item => item.EffectiveFrom!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        foreach (var expectedBoundary in cneBoundaries)
        {
            if (!groups.Any(group =>
                    group.Key == expectedBoundary))
            {
                return new TariffPeriodSelection(
                    "MISSING_TARIFF_SOURCE",
                    [],
                    $"CNE evidence establishes a tariff-index boundary at " +
                    $"{expectedBoundary:yyyy-MM-dd}, but no final Enel supply " +
                    $"tariff table for that effective period is available locally.");
            }
        }

        var baseline = groups
            .Where(group => group.Key <= startDate)
            .LastOrDefault();

        if (baseline is null)
        {
            return new TariffPeriodSelection(
                "MISSING_TARIFF_SOURCE",
                [],
                $"No tariff publication is available on or before {startDate:yyyy-MM-dd}.");
        }

        var requiredGroups = new List<IGrouping<DateOnly, TariffPublication>>
        {
            baseline
        };

        requiredGroups.AddRange(
            groups.Where(group =>
                group.Key > startDate &&
                group.Key < endBoundaryDate));

        var selected = new List<BillTariffPublicationEvidence>();

        foreach (var group in requiredGroups)
        {
            var selectedPublication =
                SelectAuthoritativePublication(
                    group.ToArray(),
                    resolutions);

            if (selectedPublication is null)
            {
                return new TariffPeriodSelection(
                    "TARIFF_VERSION_AMBIGUOUS",
                    [],
                    $"Tariff publication precedence is unresolved for effective period {group.Key:yyyy-MM-dd}.");
            }

            resolutions.TryGetValue(
                selectedPublication.PublicationId,
                out var selectedResolution);
            var selectedEvidence = new BillTariffPublicationEvidence(
                selectedPublication.PublicationId,
                selectedPublication.EffectiveFrom,
                selectedPublication.IsRetroactive,
                selectedPublication.Title,
                selectedResolution?.Status ?? "VERSION_UNRESOLVED");

            if (!string.Equals(
                    selectedPublication.NormalizationStatus,
                    "CANDIDATES_EXTRACTED",
                    StringComparison.Ordinal))
            {
                return new TariffPeriodSelection(
                    "TARIFF_NOT_NORMALIZED",
                    [selectedEvidence],
                    $"Publication {selectedPublication.PublicationId} is not normalized into rate candidates.");
            }

            selected.Add(selectedEvidence);
        }

        return new TariffPeriodSelection(
            "RESOLVED",
            selected
                .GroupBy(item => item.PublicationId)
                .Select(group => group.First())
                .ToArray(),
            selected.Count > 1
                ? "The bill interval crosses more than one resolved tariff-effective period; the printed rate must be present in every selected period to be verified."
                : "A single resolved tariff-effective publication covers the bill interval.");
    }

    private static TariffPublication? SelectAuthoritativePublication(
        IReadOnlyList<TariffPublication> publications,
        IReadOnlyDictionary<long, TariffPublicationVersionResolution> resolutions)
    {
        if (publications.Count == 1)
        {
            var only = publications[0];
            if (resolutions.TryGetValue(only.PublicationId, out var resolution) &&
                resolution.Status == "VERSION_SINGLE")
            {
                return only;
            }

            return null;
        }

        return publications.SingleOrDefault(item =>
            resolutions.TryGetValue(item.PublicationId, out var resolution) &&
            resolution.Status == "VERSION_PREFERRED_RETROACTIVE");
    }

    private static IEnumerable<RateCandidateMatch> CandidateMatches(
        TariffRateCandidate candidate,
        double printedRate)
    {
        if (candidate.NetRateClp.HasValue &&
            RatesEqual(candidate.NetRateClp.Value, printedRate))
        {
            yield return new RateCandidateMatch(
                candidate.PublicationId,
                candidate.RateCandidateId,
                candidate.NetworkType,
                candidate.EtrBand,
                candidate.CandidateIndex,
                "NETO",
                candidate.NetRateClp.Value);
        }

        if (candidate.PublishedIvaColumnClp.HasValue &&
            RatesEqual(
                candidate.PublishedIvaColumnClp.Value,
                printedRate))
        {
            yield return new RateCandidateMatch(
                candidate.PublicationId,
                candidate.RateCandidateId,
                candidate.NetworkType,
                candidate.EtrBand,
                candidate.CandidateIndex,
                "IVA_COLUMN",
                candidate.PublishedIvaColumnClp.Value);
        }
    }

    private static bool RatesEqual(double officialRate, double printedRate)
    {
        var tolerance = Math.Max(
            AbsoluteRateTolerance,
            Math.Abs(printedRate) * 0.000001);
        return Math.Abs(officialRate - printedRate) <= tolerance;
    }

    private static string? MapComponentKey(UtilityBillLine line)
    {
        if (!string.IsNullOrWhiteSpace(line.CategoryKey))
        {
            var explicitKey = line.CategoryKey.Trim().ToUpperInvariant();
            if (KnownComponentKeys.Contains(explicitKey))
                return explicitKey;
        }

        var normalized = RemoveDiacritics(line.Description)
            .ToUpperInvariant();

        if (normalized.Contains("CARGO FIJO", StringComparison.Ordinal))
            return "FIXED_MONTHLY";
        if (normalized.Contains("SERVICIO PUBLICO", StringComparison.Ordinal))
            return "PUBLIC_SERVICE";
        if (normalized.Contains("TRANSPORTE", StringComparison.Ordinal))
            return "ELECTRICITY_TRANSPORT";
        if (normalized.Contains("COMPRAS DE POTENCIA", StringComparison.Ordinal))
            return "POWER_PURCHASE";
        if (normalized.Contains("USO DE SISTEMA DE TRANSMISION", StringComparison.Ordinal))
            return "TRANSMISSION_SYSTEM_USE";
        if (normalized.Contains("TRANSMISION NACIONAL", StringComparison.Ordinal))
            return "TRANSMISSION_NATIONAL_INTERCONNECTION";
        if (normalized.Contains("TRANSMISION ZONAL SISTEMA C", StringComparison.Ordinal))
            return "TRANSMISSION_ZONAL_C";
        if (normalized.Contains("TRANSMISION ZONAL SISTEMA D", StringComparison.Ordinal))
            return "TRANSMISSION_ZONAL_D";
        if (normalized.Contains("TRANSMISION DEDICADO", StringComparison.Ordinal))
            return "TRANSMISSION_DEDICATED";
        if (normalized.Contains("CARGO POR ENERGIA", StringComparison.Ordinal))
            return "ENERGY_CHARGE";
        if (normalized.Contains("POTENCIA BASE", StringComparison.Ordinal) &&
            normalized.Contains("DISTRIBUCION", StringComparison.Ordinal))
        {
            return "POWER_BASE_DISTRIBUTION";
        }
        if (normalized.Contains("ELECTRICIDAD CONSUMIDA", StringComparison.Ordinal))
            return "ELECTRICITY_CONSUMED";
        if (normalized.Contains("TOTAL TARIFA BASE BT1", StringComparison.Ordinal))
            return "TOTAL_BT1_BASE_RAW";

        return null;
    }

    private static readonly HashSet<string> KnownComponentKeys =
    [
        "FIXED_MONTHLY",
        "PUBLIC_SERVICE",
        "TRANSMISSION_SYSTEM_USE",
        "TRANSMISSION_NATIONAL_INTERCONNECTION",
        "TRANSMISSION_ZONAL_C",
        "TRANSMISSION_ZONAL_D",
        "TRANSMISSION_DEDICATED",
        "ELECTRICITY_TRANSPORT",
        "ENERGY_CHARGE",
        "POWER_PURCHASE",
        "POWER_BASE_DISTRIBUTION",
        "ELECTRICITY_CONSUMED",
        "TOTAL_BT1_BASE_RAW"
    ];

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category =
                CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private static string BuildMatchDetail(
        IReadOnlyList<RateCandidateMatch> matches,
        int publicationCount,
        double? reconstructed,
        double actualAmount,
        double? amountDifference)
    {
        var columns = string.Join(
            ", ",
            matches.Select(item => item.Column)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));

        var applicability = matches
            .Select(item => $"{item.NetworkType ?? "?"}/{item.EtrBand ?? "?"}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var detail =
            $"Printed rate verified in {publicationCount} resolved publication period(s); " +
            $"matched source column(s): {columns}; candidate applicability: " +
            $"{string.Join(", ", applicability)}.";

        if (reconstructed.HasValue && amountDifference.HasValue)
        {
            detail +=
                $" Quantity × verified printed rate = {reconstructed.Value:N2} CLP; " +
                $"actual line = {actualAmount:N2} CLP; difference = {amountDifference.Value:+0.00;-0.00;0.00} CLP.";
        }

        return detail;
    }

    private static BillLineTariffVerification Result(
        UtilityBillLine line,
        string? componentKey,
        string status,
        string detail,
        IReadOnlyList<long>? publicationIds = null,
        IReadOnlyList<BillTariffPublicationEvidence>? publications = null) =>
        new(
            line.BillLineId,
            line.Description,
            componentKey,
            line.UnitRateClp,
            line.Quantity,
            line.AmountClp,
            status,
            publicationIds ?? [],
            publications ?? [],
            0,
            0,
            [],
            null,
            null,
            detail);

    private sealed record TariffPeriodSelection(
        string Status,
        IReadOnlyList<BillTariffPublicationEvidence> Publications,
        string Detail);

    private sealed record RateCandidateMatch(
        long PublicationId,
        long RateCandidateId,
        string? NetworkType,
        string? EtrBand,
        int CandidateIndex,
        string Column,
        double Rate);
}

public sealed record BillLineTariffVerification(
    long BillLineId,
    string Description,
    string? ComponentKey,
    double? PrintedUnitRateClp,
    double? Quantity,
    double ActualAmountClp,
    string Status,
    IReadOnlyList<long> PublicationIds,
    IReadOnlyList<BillTariffPublicationEvidence> Publications,
    int MatchCount,
    int DistinctCandidateCount,
    IReadOnlyList<string> MatchedColumns,
    double? ReconstructedAmountClp,
    double? AmountDifferenceClp,
    string Detail);


public sealed record BillTariffPublicationEvidence(
    long PublicationId,
    DateOnly? EffectiveFrom,
    bool IsRetroactive,
    string Title,
    string VersionStatus);

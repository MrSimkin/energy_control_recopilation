using System.Globalization;
using System.Text;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Builds a bill-grounded tariff model by reconciling actual billed lines
/// against normalized official Enel candidates. It does not use Solar of Things
/// energy to choose the tariff; Solar values are applied only after the bill
/// tariff model is established.
/// </summary>
public sealed class UtilityBillTariffScenarioAnalysisService
{
    private const double BillReconciliationToleranceClp = 2.0;

    private readonly UtilityMeterRepository _utilityRepository;
    private readonly TariffPublicationRepository _publicationRepository;
    private readonly TariffRateCandidateRepository _candidateRepository;
    private readonly TariffPublicationVersionResolver _versionResolver;

    public UtilityBillTariffScenarioAnalysisService(
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

    public UtilityBillTariffScenarioAnalysis Analyze(
        long billId,
        string timeZoneId,
        UtilityGridImportStatisticalCompletion completion)
    {
        var bill = _utilityRepository.GetBills()
            .SingleOrDefault(item => item.BillId == billId)
            ?? throw new InvalidOperationException(
                "Bill was not found.");

        var billedKwh = bill.BilledConsumptionKwh;
        if (!billedKwh.HasValue ||
            billedKwh.Value <= 0)
        {
            return UtilityBillTariffScenarioAnalysis.Unavailable(
                "BILLED_KWH_MISSING");
        }

        var lines = _utilityRepository.GetBillLines(
            billId);

        var electricity = lines.FirstOrDefault(item =>
            IsComponent(
                item,
                "ELECTRICITY_CONSUMED",
                "ELECTRICIDAD CONSUMIDA"));
        var transport = lines.FirstOrDefault(item =>
            IsComponent(
                item,
                "ELECTRICITY_TRANSPORT",
                "TRANSPORTE"));

        if (electricity is null)
        {
            return UtilityBillTariffScenarioAnalysis.Unavailable(
                "ELECTRICITY_LINE_MISSING");
        }

        var publications = SelectPublications(
            bill,
            timeZoneId);

        if (publications.Count == 0)
        {
            return UtilityBillTariffScenarioAnalysis.Unavailable(
                "OFFICIAL_TARIFF_SOURCE_MISSING");
        }

        var electricityMatch = MatchElectricity(
            electricity,
            billedKwh.Value,
            publications);

        if (electricityMatch is null)
        {
            return UtilityBillTariffScenarioAnalysis.Unavailable(
                "ELECTRICITY_RATE_NOT_RECONCILED");
        }

        var components =
            new List<UtilityTariffComponentEvidence>
            {
                electricityMatch
            };

        UtilityTariffComponentEvidence? transportMatch = null;
        if (transport is not null)
        {
            transportMatch = MatchTransport(
                transport,
                billedKwh.Value,
                electricityMatch,
                publications);

            if (transportMatch is not null)
                components.Add(transportMatch);
        }

        var supportedRate =
            components.Sum(item =>
                item.RateClpPerKwh);

        var billedSupportedAmount =
            billedKwh.Value * supportedRate;
        var actualSupportedLines =
            components.Sum(item =>
                item.ActualLineAmountClp);

        var scenarios = new List<UtilityTariffScenario>
        {
            Scenario(
                "ENEL_BILLED",
                billedKwh.Value,
                supportedRate,
                billedKwh.Value),
            Scenario(
                "SOLAR_OBSERVED",
                completion.ObservedKwh,
                supportedRate,
                billedKwh.Value)
        };

        if (completion.LowerKwh.HasValue)
        {
            scenarios.Add(
                Scenario(
                    "SOLAR_LOWER",
                    completion.LowerKwh.Value,
                    supportedRate,
                    billedKwh.Value));
        }

        if (completion.MedianKwh.HasValue)
        {
            scenarios.Add(
                Scenario(
                    "SOLAR_CENTRAL",
                    completion.MedianKwh.Value,
                    supportedRate,
                    billedKwh.Value));
        }

        if (completion.UpperKwh.HasValue)
        {
            scenarios.Add(
                Scenario(
                    "SOLAR_UPPER",
                    completion.UpperKwh.Value,
                    supportedRate,
                    billedKwh.Value));
        }

        var selectedPublication =
            publications.Single(item =>
                item.PublicationId ==
                electricityMatch.PublicationId);

        return new UtilityBillTariffScenarioAnalysis(
            true,
            transport is null || transportMatch is not null
                ? "SUPPORTED_COMPONENT_MODEL"
                : "PARTIAL_COMPONENT_MODEL",
            supportedRate,
            billedKwh.Value,
            actualSupportedLines,
            billedSupportedAmount,
            actualSupportedLines - billedSupportedAmount,
            components,
            scenarios,
            selectedPublication.PublicationId,
            selectedPublication.EffectiveFrom,
            selectedPublication.IsRetroactive,
            selectedPublication.Title,
            electricityMatch.NetworkType,
            electricityMatch.EtrBand,
            electricityMatch.CandidateIndex,
            electricityMatch.Column,
            transport is not null &&
            transportMatch is null
                ? "The transport line is not fully reconciled by the supported official component model."
                : null);
    }

    private UtilityTariffComponentEvidence? MatchElectricity(
        UtilityBillLine line,
        double billedKwh,
        IReadOnlyList<TariffPublication> publications)
    {
        var matches = new List<RateAttempt>();

        foreach (var publication in publications)
        {
            foreach (var candidate in _candidateRepository
                         .GetForPublication(
                             publication.PublicationId)
                         .Where(item =>
                             item.TariffPlan.Equals(
                                 "BT1",
                                 StringComparison.OrdinalIgnoreCase) &&
                             item.ComponentKey ==
                                 "ELECTRICITY_CONSUMED"))
            {
                AddRateAttempts(
                    matches,
                    publication,
                    candidate,
                    line.AmountClp,
                    billedKwh);
            }
        }

        if (matches.Count == 0)
            return null;

        var ordered = matches
            .OrderBy(item =>
                item.AbsoluteDifferenceClp)
            .ThenByDescending(item =>
                item.Publication.EffectiveFrom)
            .ToArray();

        var best = ordered[0];

        if (best.AbsoluteDifferenceClp >
            BillReconciliationToleranceClp)
        {
            return null;
        }

        var sameRateMatches = ordered
            .Where(item =>
                Math.Abs(
                    item.RateClpPerKwh -
                    best.RateClpPerKwh) <= 0.0005 &&
                item.Publication.PublicationId ==
                    best.Publication.PublicationId)
            .ToArray();

        var distinctApplicability =
            sameRateMatches
                .Select(item =>
                    $"{item.Candidate.NetworkType ?? "?"}/" +
                    $"{item.Candidate.EtrBand ?? "?"}/" +
                    $"{item.Candidate.CandidateIndex}")
                .Distinct(StringComparer.Ordinal)
                .Count();

        return new UtilityTariffComponentEvidence(
            line.Description,
            "ELECTRICITY_CONSUMED",
            best.RateClpPerKwh,
            billedKwh,
            line.AmountClp,
            best.ReconstructedAmountClp,
            line.AmountClp -
                best.ReconstructedAmountClp,
            best.Publication.PublicationId,
            best.Publication.EffectiveFrom,
            best.Publication.IsRetroactive,
            best.Publication.Title,
            best.Candidate.NetworkType,
            best.Candidate.EtrBand,
            best.Candidate.CandidateIndex,
            best.Column,
            distinctApplicability > 1
                ? "BILL_AMOUNT_RECONCILED_RATE_APPLICABILITY_AMBIGUOUS"
                : "BILL_AMOUNT_RECONCILED",
            best.Candidate.PrintedDescription);
    }

    private UtilityTariffComponentEvidence? MatchTransport(
        UtilityBillLine line,
        double billedKwh,
        UtilityTariffComponentEvidence electricity,
        IReadOnlyList<TariffPublication> publications)
    {
        var publication = publications.SingleOrDefault(item =>
            item.PublicationId ==
            electricity.PublicationId);

        if (publication is null)
            return null;

        var candidates = _candidateRepository
            .GetForPublication(
                publication.PublicationId);

        var transport = candidates
            .Where(item =>
                item.TariffPlan.Equals(
                    "BT1",
                    StringComparison.OrdinalIgnoreCase) &&
                item.ComponentKey ==
                    "ELECTRICITY_TRANSPORT" &&
                item.CandidateIndex ==
                    electricity.CandidateIndex)
            .ToArray();

        if (transport.Length == 0)
            return null;

        var publicService = candidates
            .Where(item =>
                item.TariffPlan.Equals(
                    "BT1",
                    StringComparison.OrdinalIgnoreCase) &&
                item.ComponentKey ==
                    "PUBLIC_SERVICE" &&
                item.CandidateIndex ==
                    electricity.CandidateIndex)
            .ToArray();

        var attempts = new List<CompositeAttempt>();

        foreach (var candidate in transport)
        {
            foreach (var rate in RateValues(candidate))
            {
                attempts.Add(new CompositeAttempt(
                    rate.Rate,
                    rate.Column,
                    candidate,
                    null,
                    billedKwh * rate.Rate));
            }

            foreach (var service in publicService)
            {
                foreach (var transportRate in RateValues(candidate))
                {
                    foreach (var serviceRate in ConsumerRateValues(service))
                    {
                        attempts.Add(new CompositeAttempt(
                            transportRate.Rate +
                            serviceRate.Rate,
                            $"{transportRate.Column}+PUBLIC_SERVICE",
                            candidate,
                            service,
                            billedKwh *
                            (transportRate.Rate +
                             serviceRate.Rate)));
                    }
                }
            }
        }

        if (attempts.Count == 0)
            return null;

        var best = attempts
            .OrderBy(item =>
                Math.Abs(
                    line.AmountClp -
                    item.ReconstructedAmountClp))
            .First();

        var difference =
            line.AmountClp -
            best.ReconstructedAmountClp;

        if (Math.Abs(difference) >
            BillReconciliationToleranceClp)
        {
            return null;
        }

        var description =
            best.PublicService is null
                ? best.Transport.PrintedDescription
                : $"{best.Transport.PrintedDescription} + " +
                  $"{best.PublicService.PrintedDescription}";

        return new UtilityTariffComponentEvidence(
            line.Description,
            best.PublicService is null
                ? "ELECTRICITY_TRANSPORT"
                : "ELECTRICITY_TRANSPORT_PLUS_PUBLIC_SERVICE",
            best.RateClpPerKwh,
            billedKwh,
            line.AmountClp,
            best.ReconstructedAmountClp,
            difference,
            publication.PublicationId,
            publication.EffectiveFrom,
            publication.IsRetroactive,
            publication.Title,
            electricity.NetworkType,
            electricity.EtrBand,
            electricity.CandidateIndex,
            best.Column,
            best.PublicService is null
                ? "BILL_AMOUNT_RECONCILED"
                : "BILL_AMOUNT_RECONCILED_COMPOSITE",
            description);
    }

    private static UtilityTariffScenario Scenario(
        string key,
        double kwh,
        double supportedRate,
        double billedKwh)
    {
        var difference = kwh - billedKwh;
        return new UtilityTariffScenario(
            key,
            kwh,
            difference,
            billedKwh > 0
                ? difference /
                  billedKwh *
                  100.0
                : (double?)null,
            kwh * supportedRate);
    }

    private static void AddRateAttempts(
        ICollection<RateAttempt> target,
        TariffPublication publication,
        TariffRateCandidate candidate,
        double actualAmountClp,
        double quantityKwh)
    {
        foreach (var rate in RateValues(candidate))
        {
            var reconstructed =
                quantityKwh * rate.Rate;
            target.Add(new RateAttempt(
                publication,
                candidate,
                rate.Column,
                rate.Rate,
                reconstructed,
                Math.Abs(
                    actualAmountClp -
                    reconstructed)));
        }
    }

    private static IEnumerable<(string Column, double Rate)>
        RateValues(
            TariffRateCandidate candidate)
    {
        if (candidate.NetRateClp.HasValue)
        {
            yield return (
                "NETO",
                candidate.NetRateClp.Value);
        }

        if (candidate.PublishedIvaColumnClp.HasValue &&
            candidate.PublishedIvaColumnClp.Value > 0)
        {
            yield return (
                "IVA_COLUMN",
                candidate.PublishedIvaColumnClp.Value);
        }
    }

    private static IEnumerable<(string Column, double Rate)>
        ConsumerRateValues(
            TariffRateCandidate candidate)
    {
        if (candidate.PublishedIvaColumnClp.HasValue &&
            candidate.PublishedIvaColumnClp.Value > 0)
        {
            yield return (
                "IVA_COLUMN",
                candidate.PublishedIvaColumnClp.Value);
        }

        if (candidate.NetRateClp.HasValue)
        {
            yield return (
                "NETO",
                candidate.NetRateClp.Value);
        }
    }

    private IReadOnlyList<TariffPublication> SelectPublications(
        UtilityBillRecord bill,
        string timeZoneId)
    {
        var startDate = SolarApiTime.GetLocalDate(
            bill.PeriodStartUtc,
            timeZoneId);
        var endDate = SolarApiTime.GetLocalDate(
            bill.PeriodEndUtc,
            timeZoneId);

        var publications = _publicationRepository
            .GetAll()
            .Where(item =>
                item.Provider ==
                    "ENEL_DISTRIBUCION_CHILE" &&
                item.Category ==
                    "SUPPLY_REGULATED" &&
                item.EffectiveFrom.HasValue &&
                item.EffectiveFrom.Value <=
                    endDate &&
                item.NormalizationStatus ==
                    "CANDIDATES_EXTRACTED")
            .ToArray();

        if (publications.Length == 0)
            return [];

        var resolutions = _versionResolver
            .Resolve(publications)
            .ToDictionary(item =>
                item.PublicationId);

        var groups = publications
            .GroupBy(item =>
                item.EffectiveFrom!.Value)
            .OrderBy(item => item.Key)
            .ToArray();

        var baseline = groups
            .Where(item =>
                item.Key <= startDate)
            .LastOrDefault();

        if (baseline is null)
            return [];

        var required =
            new List<IGrouping<DateOnly, TariffPublication>>
            {
                baseline
            };

        required.AddRange(
            groups.Where(item =>
                item.Key > startDate &&
                item.Key <= endDate));

        var selected =
            new List<TariffPublication>();

        foreach (var group in required)
        {
            var options = group.ToArray();
            TariffPublication? chosen = null;

            if (options.Length == 1)
            {
                var only = options[0];
                if (resolutions.TryGetValue(
                        only.PublicationId,
                        out var resolution) &&
                    resolution.Status ==
                        "VERSION_SINGLE")
                {
                    chosen = only;
                }
            }
            else
            {
                chosen = options
                    .SingleOrDefault(item =>
                        resolutions.TryGetValue(
                            item.PublicationId,
                            out var resolution) &&
                        resolution.Status ==
                            "VERSION_PREFERRED_RETROACTIVE");
            }

            if (chosen is not null)
                selected.Add(chosen);
        }

        return selected;
    }

    private static bool IsComponent(
        UtilityBillLine line,
        string categoryKey,
        string descriptionToken)
    {
        if (string.Equals(
                line.CategoryKey,
                categoryKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized =
            RemoveDiacritics(
                line.Description)
            .ToUpperInvariant();

        return normalized.Contains(
            descriptionToken,
            StringComparison.Ordinal);
    }

    private static string RemoveDiacritics(
        string value)
    {
        var normalized =
            value.Normalize(
                NormalizationForm.FormD);
        var builder =
            new StringBuilder(
                normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo
                    .GetUnicodeCategory(
                        character) !=
                UnicodeCategory
                    .NonSpacingMark)
            {
                builder.Append(
                    character);
            }
        }

        return builder
            .ToString()
            .Normalize(
                NormalizationForm.FormC);
    }

    private sealed record RateAttempt(
        TariffPublication Publication,
        TariffRateCandidate Candidate,
        string Column,
        double RateClpPerKwh,
        double ReconstructedAmountClp,
        double AbsoluteDifferenceClp);

    private sealed record CompositeAttempt(
        double RateClpPerKwh,
        string Column,
        TariffRateCandidate Transport,
        TariffRateCandidate? PublicService,
        double ReconstructedAmountClp);
}

public sealed record UtilityTariffComponentEvidence(
    string BillLineDescription,
    string ComponentKey,
    double RateClpPerKwh,
    double QuantityKwh,
    double ActualLineAmountClp,
    double ReconstructedAmountClp,
    double DifferenceClp,
    long PublicationId,
    DateOnly? EffectiveFrom,
    bool IsRetroactive,
    string PublicationTitle,
    string? NetworkType,
    string? EtrBand,
    int CandidateIndex,
    string Column,
    string EvidenceStatus,
    string OfficialDescription);

public sealed record UtilityTariffScenario(
    string Key,
    double EnergyKwh,
    double DifferenceVsEnelKwh,
    double? DifferenceVsEnelPercent,
    double SupportedTariffSubtotalClp);

public sealed record UtilityBillTariffScenarioAnalysis(
    bool HasTariffModel,
    string Status,
    double? SupportedVariableRateClpPerKwh,
    double? BilledKwh,
    double? ActualSupportedLinesClp,
    double? ReconstructedBilledSupportedClp,
    double? ReconstructedVsActualDifferenceClp,
    IReadOnlyList<UtilityTariffComponentEvidence> Components,
    IReadOnlyList<UtilityTariffScenario> Scenarios,
    long? PublicationId,
    DateOnly? EffectiveFrom,
    bool IsRetroactive,
    string? PublicationTitle,
    string? NetworkType,
    string? EtrBand,
    int? CandidateIndex,
    string? Column,
    string? Limitation)
{
    public static UtilityBillTariffScenarioAnalysis Unavailable(
        string status) =>
        new(
            false,
            status,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            null);
}

using System.Globalization;
using System.Text;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Builds a bill-grounded tariff model by reconciling actual billed lines
/// against normalized official Enel candidates. It does not use inverter
/// energy to choose the tariff; inverter values are applied only after the
/// bill tariff model is established.
///
/// When a bill spans more than one tariff-effective period, the engine applies
/// the Chilean billing rule for fractions of two calendar months: billed
/// consumption is allocated in proportion to the number of local calendar
/// days covered by each effective tariff period.
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

        var periods = BuildPublicationPeriods(
            bill,
            timeZoneId,
            publications);

        if (periods.Count == 0 ||
            periods.Sum(item => item.Days) <= 0)
        {
            return UtilityBillTariffScenarioAnalysis.Unavailable(
                "TARIFF_PERIOD_ALLOCATION_FAILED");
        }

        var electricityMatch = MatchElectricity(
            electricity,
            billedKwh.Value,
            periods);

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
                periods);

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

        var representativePublication =
            periods
                .OrderBy(item => item.AppliedFrom)
                .Last()
                .Publication;

        var periodEvidence = periods
            .Select(item =>
                new UtilityTariffPublicationPeriodEvidence(
                    item.Publication.PublicationId,
                    item.Publication.EffectiveFrom,
                    item.Publication.IsRetroactive,
                    item.Publication.Title,
                    item.Publication.SourceUrl,
                    item.Publication.ContentSha256,
                    item.AppliedFrom,
                    item.AppliedTo,
                    item.Days,
                    item.Weight))
            .ToArray();

        var status =
            transport is null || transportMatch is not null
                ? periods.Count > 1
                    ? "SUPPORTED_MULTI_PERIOD_COMPONENT_MODEL"
                    : "SUPPORTED_COMPONENT_MODEL"
                : periods.Count > 1
                    ? "PARTIAL_MULTI_PERIOD_COMPONENT_MODEL"
                    : "PARTIAL_COMPONENT_MODEL";

        var limitation =
            transport is not null &&
            transportMatch is null
                ? "The transport line is not fully reconciled by the supported official component model."
                : periods.Count > 1
                    ? "The bill crosses multiple tariff-effective periods. Consumption is allocated by local calendar days and the same tariff identity is preserved across periods."
                    : null;

        return new UtilityBillTariffScenarioAnalysis(
            true,
            status,
            supportedRate,
            billedKwh.Value,
            actualSupportedLines,
            billedSupportedAmount,
            actualSupportedLines - billedSupportedAmount,
            components,
            scenarios,
            representativePublication.PublicationId,
            representativePublication.EffectiveFrom,
            representativePublication.IsRetroactive,
            representativePublication.Title,
            electricityMatch.NetworkType,
            electricityMatch.EtrBand,
            electricityMatch.CandidateIndex,
            electricityMatch.Column,
            limitation)
        {
            PublicationPeriods = periodEvidence
        };
    }

    private UtilityTariffComponentEvidence? MatchElectricity(
        UtilityBillLine line,
        double billedKwh,
        IReadOnlyList<PublicationPeriod> periods)
    {
        var first = periods[0];
        var seeds = _candidateRepository
            .GetForPublication(
                first.Publication.PublicationId)
            .Where(item =>
                item.TariffPlan.Equals(
                    "BT1",
                    StringComparison.OrdinalIgnoreCase) &&
                item.ComponentKey ==
                    "ELECTRICITY_CONSUMED")
            .ToArray();

        var attempts =
            new List<WeightedRateAttempt>();

        foreach (var seed in seeds)
        {
            foreach (var firstRate in RateValues(seed))
            {
                var attempt =
                    BuildWeightedRateAttempt(
                        seed,
                        "ELECTRICITY_CONSUMED",
                        firstRate.Column,
                        periods,
                        billedKwh,
                        line.AmountClp);

                if (attempt is not null)
                    attempts.Add(attempt);
            }
        }

        if (attempts.Count == 0)
            return null;

        var ordered = attempts
            .OrderBy(item =>
                item.AbsoluteDifferenceClp)
            .ThenByDescending(item =>
                item.RepresentativePublication.EffectiveFrom)
            .ToArray();

        var best = ordered[0];

        if (best.AbsoluteDifferenceClp >
            BillReconciliationToleranceClp)
        {
            return null;
        }

        var acceptable = ordered
            .Where(item =>
                item.AbsoluteDifferenceClp <=
                    BillReconciliationToleranceClp)
            .ToArray();

        var distinctApplicability =
            acceptable
                .Select(item =>
                    $"{item.RepresentativeCandidate.NetworkType ?? "?"}/" +
                    $"{item.RepresentativeCandidate.EtrBand ?? "?"}/" +
                    $"{item.RepresentativeCandidate.CandidateIndex}")
                .Distinct(StringComparer.Ordinal)
                .Count();

        var status =
            periods.Count > 1
                ? distinctApplicability > 1
                    ? "BILL_AMOUNT_RECONCILED_MULTI_PERIOD_APPLICABILITY_AMBIGUOUS"
                    : "BILL_AMOUNT_RECONCILED_MULTI_PERIOD"
                : distinctApplicability > 1
                    ? "BILL_AMOUNT_RECONCILED_RATE_APPLICABILITY_AMBIGUOUS"
                    : "BILL_AMOUNT_RECONCILED";

        var description =
            periods.Count > 1
                ? $"{best.RepresentativeCandidate.PrintedDescription} · day-weighted across {periods.Count} official tariff periods"
                : best.RepresentativeCandidate.PrintedDescription;

        return new UtilityTariffComponentEvidence(
            line.Description,
            "ELECTRICITY_CONSUMED",
            best.RateClpPerKwh,
            billedKwh,
            line.AmountClp,
            best.ReconstructedAmountClp,
            line.AmountClp -
                best.ReconstructedAmountClp,
            best.RepresentativePublication.PublicationId,
            best.RepresentativePublication.EffectiveFrom,
            best.RepresentativePublication.IsRetroactive,
            best.RepresentativePublication.Title,
            best.RepresentativeCandidate.NetworkType,
            best.RepresentativeCandidate.EtrBand,
            best.RepresentativeCandidate.CandidateIndex,
            best.Column,
            status,
            description)
        {
            PublicationIds = periods
                .Select(item =>
                    item.Publication.PublicationId)
                .Distinct()
                .ToArray(),
            RateBasis =
                BuildRateBasis(
                    periods,
                    best.PeriodRates)
        };
    }

    private UtilityTariffComponentEvidence? MatchTransport(
        UtilityBillLine line,
        double billedKwh,
        UtilityTariffComponentEvidence electricity,
        IReadOnlyList<PublicationPeriod> periods)
    {
        var first = periods[0];
        var firstCandidates = _candidateRepository
            .GetForPublication(
                first.Publication.PublicationId);

        var transportSeeds = firstCandidates
            .Where(item =>
                item.TariffPlan.Equals(
                    "BT1",
                    StringComparison.OrdinalIgnoreCase) &&
                item.ComponentKey ==
                    "ELECTRICITY_TRANSPORT" &&
                item.CandidateIndex ==
                    electricity.CandidateIndex)
            .ToArray();

        if (transportSeeds.Length == 0)
            return null;

        var publicServiceSeeds = firstCandidates
            .Where(item =>
                item.TariffPlan.Equals(
                    "BT1",
                    StringComparison.OrdinalIgnoreCase) &&
                item.ComponentKey ==
                    "PUBLIC_SERVICE" &&
                item.CandidateIndex ==
                    electricity.CandidateIndex)
            .ToArray();

        var attempts =
            new List<WeightedCompositeAttempt>();

        foreach (var transportSeed in transportSeeds)
        {
            foreach (var transportRate in
                     RateValues(transportSeed))
            {
                var transportOnly =
                    BuildWeightedCompositeAttempt(
                        transportSeed,
                        transportRate.Column,
                        null,
                        null,
                        periods,
                        billedKwh,
                        line.AmountClp);

                if (transportOnly is not null)
                    attempts.Add(transportOnly);

                foreach (var serviceSeed in
                         publicServiceSeeds)
                {
                    foreach (var serviceRate in
                             ConsumerRateValues(serviceSeed))
                    {
                        var composite =
                            BuildWeightedCompositeAttempt(
                                transportSeed,
                                transportRate.Column,
                                serviceSeed,
                                serviceRate.Column,
                                periods,
                                billedKwh,
                                line.AmountClp);

                        if (composite is not null)
                            attempts.Add(composite);
                    }
                }
            }
        }

        if (attempts.Count == 0)
            return null;

        var best = attempts
            .OrderBy(item =>
                item.AbsoluteDifferenceClp)
            .First();

        if (best.AbsoluteDifferenceClp >
            BillReconciliationToleranceClp)
        {
            return null;
        }

        var description =
            best.PublicServiceCandidate is null
                ? best.TransportCandidate.PrintedDescription
                : $"{best.TransportCandidate.PrintedDescription} + " +
                  $"{best.PublicServiceCandidate.PrintedDescription}";

        if (periods.Count > 1)
            description +=
                $" · day-weighted across {periods.Count} official tariff periods";

        return new UtilityTariffComponentEvidence(
            line.Description,
            best.PublicServiceCandidate is null
                ? "ELECTRICITY_TRANSPORT"
                : "ELECTRICITY_TRANSPORT_PLUS_PUBLIC_SERVICE",
            best.RateClpPerKwh,
            billedKwh,
            line.AmountClp,
            best.ReconstructedAmountClp,
            line.AmountClp -
                best.ReconstructedAmountClp,
            best.RepresentativePublication.PublicationId,
            best.RepresentativePublication.EffectiveFrom,
            best.RepresentativePublication.IsRetroactive,
            best.RepresentativePublication.Title,
            electricity.NetworkType,
            electricity.EtrBand,
            electricity.CandidateIndex,
            best.Column,
            periods.Count > 1
                ? best.PublicServiceCandidate is null
                    ? "BILL_AMOUNT_RECONCILED_MULTI_PERIOD"
                    : "BILL_AMOUNT_RECONCILED_MULTI_PERIOD_COMPOSITE"
                : best.PublicServiceCandidate is null
                    ? "BILL_AMOUNT_RECONCILED"
                    : "BILL_AMOUNT_RECONCILED_COMPOSITE",
            description)
        {
            PublicationIds = periods
                .Select(item =>
                    item.Publication.PublicationId)
                .Distinct()
                .ToArray(),
            RateBasis =
                BuildRateBasis(
                    periods,
                    best.PeriodRates)
        };
    }

    private WeightedRateAttempt? BuildWeightedRateAttempt(
        TariffRateCandidate seed,
        string componentKey,
        string column,
        IReadOnlyList<PublicationPeriod> periods,
        double billedKwh,
        double actualAmountClp)
    {
        var periodRates =
            new List<double>(periods.Count);
        TariffRateCandidate? representative = null;
        TariffPublication? representativePublication = null;

        foreach (var period in periods)
        {
            var candidate =
                FindSameCandidate(
                    period.Publication.PublicationId,
                    componentKey,
                    seed);

            if (candidate is null ||
                !TryRate(
                    candidate,
                    column,
                    out var rate))
            {
                return null;
            }

            periodRates.Add(rate);
            representative = candidate;
            representativePublication =
                period.Publication;
        }

        var weightedRate =
            WeightedRate(
                periods,
                periodRates);
        var reconstructed =
            billedKwh * weightedRate;

        return new WeightedRateAttempt(
            weightedRate,
            column,
            reconstructed,
            Math.Abs(
                actualAmountClp -
                reconstructed),
            representative!,
            representativePublication!,
            periodRates.ToArray());
    }

    private WeightedCompositeAttempt?
        BuildWeightedCompositeAttempt(
            TariffRateCandidate transportSeed,
            string transportColumn,
            TariffRateCandidate? serviceSeed,
            string? serviceColumn,
            IReadOnlyList<PublicationPeriod> periods,
            double billedKwh,
            double actualAmountClp)
    {
        var periodRates =
            new List<double>(periods.Count);
        TariffRateCandidate? representativeTransport = null;
        TariffRateCandidate? representativeService = null;
        TariffPublication? representativePublication = null;

        foreach (var period in periods)
        {
            var transport =
                FindSameCandidate(
                    period.Publication.PublicationId,
                    "ELECTRICITY_TRANSPORT",
                    transportSeed);

            if (transport is null ||
                !TryRate(
                    transport,
                    transportColumn,
                    out var transportRate))
            {
                return null;
            }

            var rate = transportRate;
            TariffRateCandidate? service = null;

            if (serviceSeed is not null &&
                serviceColumn is not null)
            {
                service =
                    FindSameCandidate(
                        period.Publication.PublicationId,
                        "PUBLIC_SERVICE",
                        serviceSeed);

                if (service is null ||
                    !TryRate(
                        service,
                        serviceColumn,
                        out var serviceRate,
                        allowZeroIva: true))
                {
                    return null;
                }

                rate += serviceRate;
            }

            periodRates.Add(rate);
            representativeTransport = transport;
            representativeService = service;
            representativePublication =
                period.Publication;
        }

        var weightedRate =
            WeightedRate(
                periods,
                periodRates);
        var reconstructed =
            billedKwh * weightedRate;

        return new WeightedCompositeAttempt(
            weightedRate,
            serviceSeed is null
                ? transportColumn
                : $"{transportColumn}+PUBLIC_SERVICE",
            reconstructed,
            Math.Abs(
                actualAmountClp -
                reconstructed),
            representativeTransport!,
            representativeService,
            representativePublication!,
            periodRates.ToArray());
    }

    private TariffRateCandidate? FindSameCandidate(
        long publicationId,
        string componentKey,
        TariffRateCandidate seed)
    {
        var candidates = _candidateRepository
            .GetForPublication(
                publicationId)
            .Where(item =>
                item.TariffPlan.Equals(
                    "BT1",
                    StringComparison.OrdinalIgnoreCase) &&
                item.ComponentKey ==
                    componentKey)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        // CandidateIndex is parser-local and may change when a later official
        // PDF changes column ordering. Cross-publication identity must prefer
        // the semantic RED/network + ETR pair, then use CandidateIndex only
        // as a last-resort fallback.
        if (!string.IsNullOrWhiteSpace(
                seed.NetworkType) ||
            !string.IsNullOrWhiteSpace(
                seed.EtrBand))
        {
            var sameIdentity =
                candidates
                    .Where(item =>
                        string.Equals(
                            item.NetworkType,
                            seed.NetworkType,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            item.EtrBand,
                            seed.EtrBand,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            if (sameIdentity.Length == 1)
                return sameIdentity[0];

            if (sameIdentity.Length > 1)
            {
                var sameIndexWithinIdentity =
                    sameIdentity
                        .Where(item =>
                            item.CandidateIndex ==
                            seed.CandidateIndex)
                        .ToArray();

                var collapsed =
                    CollapseEquivalentCandidates(
                        sameIndexWithinIdentity);
                if (collapsed is not null)
                    return collapsed;

                // More than one semantically matching candidate remains and
                // they are not rate-equivalent. Do not guess.
                if (sameIndexWithinIdentity.Length > 1)
                    return null;
            }
        }

        var sameIndex =
            candidates
                .Where(item =>
                    item.CandidateIndex ==
                    seed.CandidateIndex)
                .ToArray();

        var sameIndexCollapsed =
            CollapseEquivalentCandidates(
                sameIndex);
        if (sameIndexCollapsed is not null)
            return sameIndexCollapsed;

        return candidates.Length == 1
            ? candidates[0]
            : null;
    }

    private static TariffRateCandidate?
        CollapseEquivalentCandidates(
            IReadOnlyList<TariffRateCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var first = candidates[0];
        var allEquivalent = candidates.All(item =>
            string.Equals(
                item.ComponentKey,
                first.ComponentKey,
                StringComparison.Ordinal) &&
            string.Equals(
                item.NetworkType,
                first.NetworkType,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                item.EtrBand,
                first.EtrBand,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                item.Unit,
                first.Unit,
                StringComparison.OrdinalIgnoreCase) &&
            NullableRateEquals(
                item.NetRateClp,
                first.NetRateClp) &&
            NullableRateEquals(
                item.PublishedIvaColumnClp,
                first.PublishedIvaColumnClp));

        if (!allEquivalent)
            return null;

        return candidates
            .OrderBy(item =>
                item.PageNumber)
            .ThenBy(item =>
                item.CandidateIndex)
            .ThenBy(item =>
                item.RateCandidateId)
            .First();
    }

    private static bool NullableRateEquals(
        double? left,
        double? right)
    {
        if (!left.HasValue ||
            !right.HasValue)
        {
            return left.HasValue ==
                   right.HasValue;
        }

        return Math.Abs(
                   left.Value -
                   right.Value) <=
               0.000001;
    }

    private static bool TryRate(
        TariffRateCandidate candidate,
        string column,
        out double rate,
        bool allowZeroIva = false)
    {
        if (column == "NETO" &&
            candidate.NetRateClp.HasValue)
        {
            rate = candidate.NetRateClp.Value;
            return true;
        }

        if (column == "IVA_COLUMN" &&
            candidate.PublishedIvaColumnClp.HasValue &&
            (allowZeroIva ||
             candidate.PublishedIvaColumnClp.Value > 0))
        {
            rate =
                candidate.PublishedIvaColumnClp.Value;
            return true;
        }

        rate = 0;
        return false;
    }

    private static double WeightedRate(
        IReadOnlyList<PublicationPeriod> periods,
        IReadOnlyList<double> rates)
    {
        var totalDays =
            periods.Sum(item => item.Days);

        if (totalDays <= 0 ||
            rates.Count != periods.Count)
        {
            throw new InvalidOperationException(
                "Invalid tariff period allocation.");
        }

        var total = 0.0;
        for (var index = 0;
             index < periods.Count;
             index++)
        {
            total +=
                rates[index] *
                periods[index].Days;
        }

        return total / totalDays;
    }

    private static string BuildRateBasis(
        IReadOnlyList<PublicationPeriod> periods,
        IReadOnlyList<double> rates)
    {
        var parts =
            new List<string>();

        for (var index = 0;
             index < periods.Count;
             index++)
        {
            var period = periods[index];
            var rate = rates[index];
            parts.Add(
                $"{period.AppliedFrom:yyyy-MM-dd}.." +
                $"{period.AppliedTo:yyyy-MM-dd}: " +
                $"{period.Days} day(s) × " +
                $"{rate:0.###} CLP/kWh");
        }

        return string.Join(
            "; ",
            parts);
    }

    private IReadOnlyList<PublicationPeriod>
        BuildPublicationPeriods(
            UtilityBillRecord bill,
            string timeZoneId,
            IReadOnlyList<TariffPublication> publications)
    {
        var billStart =
            SolarApiTime.GetLocalDate(
                bill.PeriodStartUtc,
                timeZoneId);
        var billEnd =
            SolarApiTime.GetLocalDate(
                bill.PeriodEndUtc,
                timeZoneId);

        if (billEnd < billStart)
            return [];

        var ordered =
            publications
                .Where(item =>
                    item.EffectiveFrom.HasValue)
                .OrderBy(item =>
                    item.EffectiveFrom)
                .ToArray();

        if (ordered.Length == 0)
            return [];

        var result =
            new List<PublicationPeriod>();

        for (var index = 0;
             index < ordered.Length;
             index++)
        {
            var publication =
                ordered[index];
            var effectiveFrom =
                publication.EffectiveFrom!.Value;

            var appliedFrom =
                effectiveFrom > billStart
                    ? effectiveFrom
                    : billStart;

            var nextEffective =
                index + 1 < ordered.Length
                    ? ordered[index + 1]
                        .EffectiveFrom!.Value
                    : billEnd.AddDays(1);

            var candidateEnd =
                nextEffective.AddDays(-1);
            var appliedTo =
                candidateEnd < billEnd
                    ? candidateEnd
                    : billEnd;

            if (appliedTo < appliedFrom)
                continue;

            var days =
                appliedTo.DayNumber -
                appliedFrom.DayNumber +
                1;

            result.Add(
                new PublicationPeriod(
                    publication,
                    appliedFrom,
                    appliedTo,
                    days,
                    0));
        }

        var totalDays =
            result.Sum(item =>
                item.Days);

        if (totalDays <= 0)
            return [];

        return result
            .Select(item =>
                item with
                {
                    Weight =
                        (double)item.Days /
                        totalDays
                })
            .ToArray();
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
            .Resolve(
                publications,
                _publicationRepository.GetRelations())
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
                    TariffPublicationVersionResolver
                        .IsAuthoritativeStatus(
                            resolution.Status))
                {
                    chosen = only;
                }
            }
            else
            {
                var preferred = options
                    .Where(item =>
                        resolutions.TryGetValue(
                            item.PublicationId,
                            out var resolution) &&
                        TariffPublicationVersionResolver
                            .IsAuthoritativeStatus(
                                resolution.Status))
                    .ToArray();

                if (preferred.Length == 1)
                {
                    chosen = preferred[0];
                }
                else if (preferred.Length > 1)
                {
                    // Duplicate discoveries of the exact same official file
                    // may coexist in an older target DB. Collapse only when
                    // the preserved file hash proves equivalence.
                    var hashes = preferred
                        .Select(item =>
                            item.ContentSha256)
                        .Where(item =>
                            !string.IsNullOrWhiteSpace(item))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (hashes.Length == 1 &&
                        preferred.All(item =>
                            !string.IsNullOrWhiteSpace(
                                item.ContentSha256)))
                    {
                        chosen = preferred
                            .OrderByDescending(item =>
                                item.PublicationId)
                            .First();
                    }
                }
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

    private sealed record PublicationPeriod(
        TariffPublication Publication,
        DateOnly AppliedFrom,
        DateOnly AppliedTo,
        int Days,
        double Weight);

    private sealed record WeightedRateAttempt(
        double RateClpPerKwh,
        string Column,
        double ReconstructedAmountClp,
        double AbsoluteDifferenceClp,
        TariffRateCandidate RepresentativeCandidate,
        TariffPublication RepresentativePublication,
        IReadOnlyList<double> PeriodRates);

    private sealed record WeightedCompositeAttempt(
        double RateClpPerKwh,
        string Column,
        double ReconstructedAmountClp,
        double AbsoluteDifferenceClp,
        TariffRateCandidate TransportCandidate,
        TariffRateCandidate? PublicServiceCandidate,
        TariffPublication RepresentativePublication,
        IReadOnlyList<double> PeriodRates);
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
    string OfficialDescription)
{
    public IReadOnlyList<long> PublicationIds { get; init; } =
        [];
    public string? RateBasis { get; init; }
}

public sealed record UtilityTariffScenario(
    string Key,
    double EnergyKwh,
    double DifferenceVsEnelKwh,
    double? DifferenceVsEnelPercent,
    double SupportedTariffSubtotalClp);

public sealed record UtilityTariffPublicationPeriodEvidence(
    long PublicationId,
    DateOnly? EffectiveFrom,
    bool IsRetroactive,
    string PublicationTitle,
    string SourceUrl,
    string? ContentSha256,
    DateOnly AppliedFrom,
    DateOnly AppliedTo,
    int Days,
    double Weight);

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
    public IReadOnlyList<UtilityTariffPublicationPeriodEvidence>
        PublicationPeriods { get; init; } = [];

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

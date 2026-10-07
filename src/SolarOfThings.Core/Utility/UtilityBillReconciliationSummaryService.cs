using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Product-facing reconciliation summary for a stored utility bill.
///
/// This service intentionally uses observed inverter energy only. It does not
/// fill telemetry gaps or promote the bill-specific provisional P5/P50/P95
/// research method into the normal product workflow.
///
/// Supported official variable tariff components are recalculated at the
/// observed inverter energy while all other actual bill amounts are preserved.
/// </summary>
public sealed class UtilityBillReconciliationSummaryService
{
    public const string ObservedOnlyMethodVersion =
        "bill-reconciliation-observed-only.v1";

    private readonly UtilityMeterRepository _repository;
    private readonly UtilityReconciliationService _reconciliation;
    private readonly UtilityBillGapStatisticalCompletionService _billGapCompletion;
    private readonly UtilityBillTariffScenarioAnalysisService _tariffAnalysis;

    public UtilityBillReconciliationSummaryService(
        UtilityMeterRepository repository,
        UtilityReconciliationService reconciliation,
        UtilityBillGapStatisticalCompletionService billGapCompletion,
        UtilityBillTariffScenarioAnalysisService tariffAnalysis)
    {
        _repository = repository;
        _reconciliation = reconciliation;
        _billGapCompletion = billGapCompletion;
        _tariffAnalysis = tariffAnalysis;
    }

    public UtilityBillReconciliationSummary Analyze(
        string deviceId,
        long billId,
        string timeZoneId)
    {
        var bill = _repository.GetBills()
            .SingleOrDefault(item => item.BillId == billId)
            ?? throw new InvalidOperationException(
                "Bill was not found.");

        double observedKwh;
        double coveragePercent;
        double uncoveredHours;

        if (string.Equals(
                bill.PeriodPrecision,
                UtilityTimePrecision.DateOnly,
                StringComparison.Ordinal))
        {
            var startLocalDate =
                SolarApiTime.GetLocalDate(
                    bill.PeriodStartUtc,
                    timeZoneId);
            var endLocalDateInclusive =
                SolarApiTime.GetLocalDate(
                    bill.PeriodEndUtc,
                    timeZoneId);

            var observed =
                _billGapCompletion.AnalyzeObservedOnly(
                    deviceId,
                    startLocalDate,
                    endLocalDateInclusive,
                    timeZoneId);

            observedKwh =
                observed.ObservedKwh;
            coveragePercent =
                observed.CoveragePercent;
            uncoveredHours =
                observed.MissingHours;
        }
        else
        {
            var comparison = _reconciliation
                .GetBillReconciliations(deviceId)
                .Single(item =>
                    item.BillId == billId);

            observedKwh =
                comparison.InverterGridImportKwh;
            coveragePercent =
                comparison.CoveragePercent;
            uncoveredHours =
                comparison.Sensitivity
                    ?.UncoveredHours ??
                0;
        }

        var completion = new UtilityGridImportStatisticalCompletion(
            observedKwh,
            null,
            null,
            null,
            null,
            null,
            coveragePercent,
            uncoveredHours,
            0,
            0,
            ObservedOnlyMethodVersion,
            "OBSERVED_ONLY",
            0.05,
            0.95);

        var tariff = _tariffAnalysis.Analyze(
            billId,
            timeZoneId,
            completion);

        var actualTotal =
            bill.TotalDueClp ??
            bill.AmountClp;

        var billLines =
            _repository.GetBillLines(billId);
        var ivaRate =
            bill.IvaRate ??
            UtilityBillAuditV2Service.ChileStandardIvaRate;
        var expectedIva =
            bill.TaxableAmountClp.HasValue
                ? Math.Round(
                    bill.TaxableAmountClp.Value * ivaRate,
                    0,
                    MidpointRounding.AwayFromZero)
                : (double?)null;
        var ivaDifference =
            bill.IvaClp.HasValue &&
            expectedIva.HasValue
                ? bill.IvaClp.Value -
                  expectedIva.Value
                : (double?)null;
        var simpleAdjustment =
            billLines
                .Where(item =>
                    string.Equals(
                        item.CategoryKey,
                        UtilityBillLineCategory.SimpleAdjustment,
                        StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.AmountClp);

        var observedScenario = tariff.Scenarios
            .FirstOrDefault(item =>
                string.Equals(
                    item.Key,
                    "SOLAR_OBSERVED",
                    StringComparison.Ordinal));

        double? preservedNonVariable = null;
        double? reconstructedBilledTotal = null;
        double? estimatedObservedTotal = null;
        double? actualMinusObservedEstimate = null;

        if (tariff.HasTariffModel &&
            actualTotal.HasValue &&
            tariff.ActualSupportedLinesClp.HasValue)
        {
            preservedNonVariable =
                actualTotal.Value -
                tariff.ActualSupportedLinesClp.Value;

            if (tariff.ReconstructedBilledSupportedClp.HasValue)
            {
                reconstructedBilledTotal =
                    preservedNonVariable.Value +
                    tariff.ReconstructedBilledSupportedClp.Value;
            }

            if (observedScenario is not null)
            {
                estimatedObservedTotal =
                    preservedNonVariable.Value +
                    observedScenario.SupportedTariffSubtotalClp;

                actualMinusObservedEstimate =
                    actualTotal.Value -
                    estimatedObservedTotal.Value;
            }
        }

        var billedMinusObserved =
            bill.BilledConsumptionKwh.HasValue
                ? bill.BilledConsumptionKwh.Value -
                  observedKwh
                : (double?)null;

        var billedMinusObservedPercent =
            bill.BilledConsumptionKwh is > 0 &&
            billedMinusObserved.HasValue
                ? billedMinusObserved.Value /
                  bill.BilledConsumptionKwh.Value *
                  100.0
                : (double?)null;

        var status =
            !tariff.HasTariffModel
                ? "ENERGY_ONLY_TARIFF_MODEL_UNAVAILABLE"
                : !actualTotal.HasValue
                    ? "VARIABLE_COMPONENTS_ONLY_TOTAL_MISSING"
                    : coveragePercent < 98.0
                        ? "SUPPORTED_ESTIMATE_PARTIAL_COVERAGE"
                        : tariff.Status.StartsWith(
                            "PARTIAL_",
                            StringComparison.Ordinal)
                            ? "SUPPORTED_ESTIMATE_PARTIAL_TARIFF_MODEL"
                            : "SUPPORTED_OBSERVED_ESTIMATE";

        var limitations = new List<string>();
        if (coveragePercent < 98.0)
        {
            limitations.Add(
                "Observed inverter energy is incomplete; telemetry gaps are not filled in this product summary.");
        }

        if (!string.IsNullOrWhiteSpace(
                tariff.Limitation))
        {
            limitations.Add(
                tariff.Limitation);
        }

        if (string.Equals(
                bill.ReviewState,
                UtilityBillReviewState.LegacyUnreviewed,
                StringComparison.Ordinal))
        {
            limitations.Add(
                "This bill predates bill-ingestion v2 and has not yet been reviewed against a canonical source document.");
        }

        if (bill.IvaClp.HasValue &&
            !expectedIva.HasValue)
        {
            limitations.Add(
                "Printed VAT is stored, but the taxable base is not available for an independent 19% check.");
        }

        return new UtilityBillReconciliationSummary(
            billId,
            status,
            actualTotal,
            bill.BilledConsumptionKwh,
            observedKwh,
            billedMinusObserved,
            billedMinusObservedPercent,
            coveragePercent,
            tariff.SupportedVariableRateClpPerKwh,
            tariff.ActualSupportedLinesClp,
            preservedNonVariable,
            reconstructedBilledTotal,
            estimatedObservedTotal,
            actualMinusObservedEstimate,
            tariff,
            ivaRate,
            bill.IvaClp,
            expectedIva,
            ivaDifference,
            simpleAdjustment,
            bill.SourceKind,
            bill.ReviewState,
            limitations.Count == 0
                ? null
                : string.Join(" ", limitations));
    }
}

public sealed record UtilityBillReconciliationSummary(
    long BillId,
    string Status,
    double? ActualBillTotalClp,
    double? BilledKwh,
    double ObservedInverterKwh,
    double? BilledMinusObservedKwh,
    double? BilledMinusObservedPercent,
    double CoveragePercent,
    double? SupportedVariableRateClpPerKwh,
    double? ActualSupportedLinesClp,
    double? PreservedNonVariableClp,
    double? ReconstructedBilledTotalClp,
    double? EstimatedObservedTotalClp,
    double? ActualMinusEstimatedObservedClp,
    UtilityBillTariffScenarioAnalysis TariffAnalysis,
    double IvaRate,
    double? PrintedIvaClp,
    double? ExpectedIvaClp,
    double? IvaDifferenceClp,
    double SimpleAdjustmentClp,
    string BillSourceKind,
    string ReviewState,
    string? Limitation)
{
    public bool HasObservedEconomicEstimate =>
        EstimatedObservedTotalClp.HasValue;
}

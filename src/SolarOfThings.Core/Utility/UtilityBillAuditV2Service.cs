namespace SolarOfThings.Core.Utility;

/// <summary>
/// Product-facing line-by-line bill audit. It separates printed evidence,
/// official tariff reconstruction, the Chilean standard VAT check and simple
/// bill-end adjustments instead of hiding preserved amounts in one residual.
/// </summary>
public sealed class UtilityBillAuditV2Service
{
    public const double ChileStandardIvaRate = 0.19;
    public const double SimpleAdjustmentToleranceClp = 10.0;

    private readonly UtilityMeterRepository _repository;
    private readonly TariffBillRateVerificationService _rateVerification;
    private readonly UtilityBillTariffScenarioAnalysisService _tariffAnalysis;

    public UtilityBillAuditV2Service(
        UtilityMeterRepository repository,
        TariffBillRateVerificationService rateVerification,
        UtilityBillTariffScenarioAnalysisService tariffAnalysis)
    {
        _repository = repository;
        _rateVerification = rateVerification;
        _tariffAnalysis = tariffAnalysis;
    }

    public UtilityBillAuditV2 Analyze(
        long billId,
        string timeZoneId)
    {
        var bill = _repository.GetBills()
            .SingleOrDefault(item => item.BillId == billId)
            ?? throw new InvalidOperationException("Bill was not found.");

        var lines = _repository.GetBillLines(billId);
        var verifications = _rateVerification
            .VerifyBill(billId, timeZoneId)
            .ToDictionary(item => item.BillLineId);

        UtilityBillTariffScenarioAnalysis? reconciledTariff = null;
        if (bill.BilledConsumptionKwh is > 0)
        {
            var billedTruth =
                UtilityGridImportStatisticalCompletion.Insufficient(
                    bill.BilledConsumptionKwh.Value,
                    100.0,
                    0,
                    0,
                    "BILL_AUDIT_V2_BILLED_TRUTH");
            reconciledTariff =
                _tariffAnalysis.Analyze(
                    billId,
                    timeZoneId,
                    billedTruth);
        }

        var ivaRate =
            bill.IvaRate ??
            ChileStandardIvaRate;
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
            lines
                .Where(item =>
                    string.Equals(
                        item.CategoryKey,
                        UtilityBillLineCategory.SimpleAdjustment,
                        StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.AmountClp);

        var audited = new List<UtilityBillLineAuditV2>();
        foreach (var line in lines)
        {
            verifications.TryGetValue(
                line.BillLineId,
                out var verification);

            var evidence =
                $"{line.SourceKind} · {line.EvidenceState}" +
                (line.SourcePage.HasValue
                    ? $" · PDF p.{line.SourcePage.Value}"
                    : string.Empty);

            if (string.Equals(
                    line.CategoryKey,
                    UtilityBillLineCategory.Vat19,
                    StringComparison.OrdinalIgnoreCase))
            {
                audited.Add(new UtilityBillLineAuditV2(
                    line.BillLineId,
                    line.Description,
                    line.UnitRateClp,
                    bill.TaxableAmountClp,
                    "CLP afectos",
                    "BILL_TAXABLE_AMOUNT",
                    line.AmountClp,
                    expectedIva,
                    expectedIva.HasValue
                        ? line.AmountClp - expectedIva.Value
                        : null,
                    expectedIva.HasValue
                        ? "VAT_RECONSTRUCTED_19"
                        : "VAT_BASE_MISSING",
                    "IVA 19% sobre monto afecto registrado",
                    evidence));
                continue;
            }

            if (string.Equals(
                    line.CategoryKey,
                    UtilityBillLineCategory.SimpleAdjustment,
                    StringComparison.OrdinalIgnoreCase))
            {
                audited.Add(new UtilityBillLineAuditV2(
                    line.BillLineId,
                    line.Description,
                    line.UnitRateClp,
                    line.Quantity,
                    line.Unit,
                    "BILL_PRINTED",
                    line.AmountClp,
                    null,
                    null,
                    "PRINTED_SIMPLE_ADJUSTMENT",
                    "Ajuste final preservado explícitamente desde la boleta",
                    evidence));
                continue;
            }

            var categoryIsUnique =
                !string.IsNullOrWhiteSpace(
                    line.CategoryKey) &&
                lines.Count(other =>
                    string.Equals(
                        other.CategoryKey,
                        line.CategoryKey,
                        StringComparison.OrdinalIgnoreCase)) == 1;

            var reconciledComponent =
                reconciledTariff?
                    .Components
                    .FirstOrDefault(item =>
                        string.Equals(
                            item.BillLineDescription,
                            line.Description,
                            StringComparison.OrdinalIgnoreCase) ||
                        (categoryIsUnique &&
                         (
                             string.Equals(
                                 item.ComponentKey,
                                 line.CategoryKey,
                                 StringComparison.OrdinalIgnoreCase) ||
                             (string.Equals(
                                  line.CategoryKey,
                                  UtilityBillLineCategory.ElectricityTransport,
                                  StringComparison.OrdinalIgnoreCase) &&
                              item.ComponentKey.StartsWith(
                                  "ELECTRICITY_TRANSPORT",
                                  StringComparison.OrdinalIgnoreCase))
                         )));

            if (reconciledComponent is not null)
            {
                var source =
                    reconciledComponent.PublicationTitle;
                if (!string.IsNullOrWhiteSpace(
                        reconciledComponent.RateBasis))
                {
                    source +=
                        " · " +
                        reconciledComponent.RateBasis;
                }

                audited.Add(new UtilityBillLineAuditV2(
                    line.BillLineId,
                    line.Description,
                    line.UnitRateClp,
                    reconciledComponent.QuantityKwh,
                    "kWh",
                    "BILL_BILLED_KWH",
                    line.AmountClp,
                    reconciledComponent.ReconstructedAmountClp,
                    reconciledComponent.DifferenceClp,
                    reconciledComponent.EvidenceStatus,
                    source,
                    evidence));
                continue;
            }

            if (verification is null)
            {
                audited.Add(new UtilityBillLineAuditV2(
                    line.BillLineId,
                    line.Description,
                    line.UnitRateClp,
                    line.Quantity,
                    line.Unit,
                    line.Quantity.HasValue ? "BILL_LINE" : null,
                    line.AmountClp,
                    null,
                    null,
                    "ACTUAL_ONLY_UNMAPPED",
                    "Boleta real; sin reconstrucción oficial disponible",
                    evidence));
                continue;
            }

            var officialSource =
                verification.Publications.Count == 0
                    ? "Boleta real; sin fuente tarifaria resuelta"
                    : string.Join(
                        " · ",
                        verification.Publications.Select(item =>
                            $"{item.EffectiveFrom:yyyy-MM} · {item.Title}"));

            audited.Add(new UtilityBillLineAuditV2(
                line.BillLineId,
                line.Description,
                verification.PrintedUnitRateClp,
                verification.CalculationQuantity,
                verification.CalculationUnit,
                verification.CalculationQuantitySource,
                line.AmountClp,
                verification.ReconstructedAmountClp,
                verification.AmountDifferenceClp,
                verification.Status,
                officialSource,
                evidence));
        }

        var taxStatus =
            !bill.IvaClp.HasValue
                ? "IVA_NOT_RECORDED"
                : !expectedIva.HasValue
                    ? "IVA_BASE_MISSING"
                    : Math.Abs(ivaDifference ?? 0) <= 1
                        ? "IVA_MATCH_19"
                        : "IVA_DIFFERENCE";

        var expectedGross =
            bill.TaxableAmountClp.HasValue &&
            bill.IvaClp.HasValue &&
            bill.ExemptAmountClp.HasValue
                ? bill.TaxableAmountClp.Value +
                  bill.IvaClp.Value +
                  bill.ExemptAmountClp.Value
                : (double?)null;
        var grossDifference =
            bill.GrossBillAmountClp.HasValue &&
            expectedGross.HasValue
                ? bill.GrossBillAmountClp.Value -
                  expectedGross.Value
                : (double?)null;

        var expectedSummaryTotal =
            bill.GrossBillAmountClp.HasValue &&
            bill.OtherChargesClp.HasValue &&
            bill.PreviousBalanceClp.HasValue
                ? bill.GrossBillAmountClp.Value +
                  bill.OtherChargesClp.Value +
                  bill.PreviousBalanceClp.Value
                : (double?)null;

        var totalDue =
            bill.TotalDueClp ??
            bill.AmountClp;
        var summaryTotalDifference =
            totalDue.HasValue &&
            expectedSummaryTotal.HasValue
                ? totalDue.Value -
                  expectedSummaryTotal.Value
                : (double?)null;

        var summaryBalanceStatus =
            grossDifference.HasValue &&
            Math.Abs(grossDifference.Value) > 0.5
                ? "SUMMARY_GROSS_DIFFERENCE"
                : summaryTotalDifference.HasValue &&
                  Math.Abs(summaryTotalDifference.Value) > 0.5
                    ? "SUMMARY_TOTAL_DIFFERENCE"
                    : grossDifference.HasValue &&
                      summaryTotalDifference.HasValue
                        ? "SUMMARY_BALANCED"
                        : "SUMMARY_PARTIAL";

        var lineTotal =
            lines.Sum(item => item.AmountClp);
        var unexplainedResidual =
            totalDue.HasValue
                ? totalDue.Value - lineTotal
                : (double?)null;
        var balanceStatus =
            !totalDue.HasValue
                ? "TOTAL_MISSING"
                : Math.Abs(unexplainedResidual!.Value) <= 0.5
                    ? "BALANCED"
                    : Math.Abs(unexplainedResidual.Value) <=
                        SimpleAdjustmentToleranceClp
                        ? "SMALL_UNEXPLAINED_RESIDUAL"
                        : "MATERIAL_UNEXPLAINED_DIFFERENCE";

        var totalAbsolute =
            lines.Sum(item => Math.Abs(item.AmountClp));
        var reconstructedAbsolute =
            audited
                .Where(item =>
                    item.ReconstructedAmountClp.HasValue)
                .Sum(item =>
                    Math.Abs(item.ActualAmountClp));
        var reconstructionCoverage =
            totalAbsolute > 0
                ? reconstructedAbsolute /
                  totalAbsolute * 100.0
                : 0.0;

        return new UtilityBillAuditV2(
            billId,
            bill.SourceKind,
            bill.ReviewState,
            ivaRate,
            bill.TaxableAmountClp,
            bill.IvaClp,
            expectedIva,
            ivaDifference,
            taxStatus,
            simpleAdjustment,
            totalDue,
            lineTotal,
            unexplainedResidual,
            balanceStatus,
            reconstructionCoverage,
            audited)
        {
            ExpectedGrossBillClp = expectedGross,
            GrossBillDifferenceClp = grossDifference,
            ExpectedSummaryTotalClp = expectedSummaryTotal,
            SummaryTotalDifferenceClp = summaryTotalDifference,
            SummaryBalanceStatus = summaryBalanceStatus
        };
    }
}

public sealed record UtilityBillAuditV2(
    long BillId,
    string BillSourceKind,
    string ReviewState,
    double IvaRate,
    double? TaxableAmountClp,
    double? PrintedIvaClp,
    double? ExpectedIvaClp,
    double? IvaDifferenceClp,
    string TaxStatus,
    double SimpleAdjustmentClp,
    double? TotalDueClp,
    double LineTotalClp,
    double? UnexplainedResidualClp,
    string BalanceStatus,
    double ReconstructionCoveragePercent,
    IReadOnlyList<UtilityBillLineAuditV2> Lines)
{
    public double? ExpectedGrossBillClp { get; init; }
    public double? GrossBillDifferenceClp { get; init; }
    public double? ExpectedSummaryTotalClp { get; init; }
    public double? SummaryTotalDifferenceClp { get; init; }
    public string SummaryBalanceStatus { get; init; } =
        "SUMMARY_PARTIAL";
}

public sealed record UtilityBillLineAuditV2(
    long BillLineId,
    string Description,
    double? PrintedUnitRateClp,
    double? CalculationQuantity,
    string? CalculationUnit,
    string? CalculationQuantitySource,
    double ActualAmountClp,
    double? ReconstructedAmountClp,
    double? AmountDifferenceClp,
    string Status,
    string Source,
    string Evidence);

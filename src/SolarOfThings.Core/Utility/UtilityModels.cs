namespace SolarOfThings.Core.Utility;

public static class UtilityReadingSourceKind
{
    public const string UtilityOfficial = "UTILITY_OFFICIAL";
    public const string Personal = "PERSONAL";
    public const string Unspecified = "UNSPECIFIED";
}

public static class UtilityTimePrecision
{
    public const string Exact = "EXACT";
    public const string DateOnly = "DATE_ONLY";
}

public static class UtilityTimeAssumption
{
    public const string Exact = "EXACT";
    public const string StartOfDayAssumed = "START_OF_DAY_ASSUMED";
    public const string EndPreviousDayAssumed = "END_PREVIOUS_DAY_ASSUMED";
}

public sealed record UtilityMeterReading(
    long ReadingId,
    DateTimeOffset ReadingAtUtc,
    double ReadingKwh,
    string SourceKind,
    string TimePrecision,
    string TimeAssumption,
    string? Reference,
    string? Notes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UtilityBillRecord(
    long BillId,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    double? BilledConsumptionKwh,
    double? AmountClp,
    string? InvoiceReference,
    string? Notes,
    long? FromReadingId,
    long? ToReadingId,
    double? MeterStartKwh,
    double? MeterEndKwh,
    string? TariffPlan,
    double? TaxableAmountClp,
    double? IvaClp,
    double? ExemptAmountClp,
    double? GrossBillAmountClp,
    double? OtherChargesClp,
    double? TotalDueClp,
    string PeriodPrecision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UtilityBillLine(
    long BillLineId,
    long BillId,
    string SectionKey,
    string? CategoryKey,
    string Description,
    double? Quantity,
    string? Unit,
    double? UnitRateClp,
    double AmountClp,
    string? TaxTreatment,
    int SortOrder,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UtilityMeterReconciliation(
    long FromReadingId,
    long ToReadingId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    double FromMeterKwh,
    double ToMeterKwh,
    double? MeterConsumptionKwh,
    double InverterGridImportKwh,
    double? SignedDifferenceKwh,
    double? AbsoluteDifferenceKwh,
    double? DifferencePercent,
    double CoveragePercent,
    string TimeBasis,
    string Quality,
    string Detail);

public sealed record UtilityBillReconciliation(
    long BillId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    double? BilledConsumptionKwh,
    double InverterGridImportKwh,
    double? SignedDifferenceKwh,
    double? AbsoluteDifferenceKwh,
    double? DifferencePercent,
    double CoveragePercent,
    string TimeBasis,
    string Quality,
    string Detail);

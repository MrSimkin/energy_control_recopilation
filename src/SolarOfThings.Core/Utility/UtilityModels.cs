namespace SolarOfThings.Core.Utility;

public sealed record UtilityMeterReading(
    long ReadingId,
    DateTimeOffset ReadingAtUtc,
    double ReadingKwh,
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
    string Quality,
    string Detail);

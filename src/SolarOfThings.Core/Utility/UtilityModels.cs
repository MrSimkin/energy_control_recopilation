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

public static class UtilityBillSourceKind
{
    public const string Manual = "MANUAL";
    public const string PdfReviewed = "PDF_REVIEWED";
    public const string LegacyManual = "LEGACY_MANUAL";
}

public static class UtilityBillReviewState
{
    public const string Reviewed = "REVIEWED";
    public const string Draft = "DRAFT";
    public const string LegacyUnreviewed = "LEGACY_UNREVIEWED";
}

public static class UtilityBillEvidenceState
{
    public const string UserEntered = "USER_ENTERED";
    public const string PdfExtractedConfirmed = "PDF_EXTRACTED_CONFIRMED";
    public const string PdfExtractedReviewRequired = "PDF_EXTRACTED_REVIEW_REQUIRED";
    public const string NotPrinted = "NOT_PRINTED";
    public const string Derived = "DERIVED";
    public const string LegacyUnreviewed = "LEGACY_UNREVIEWED";
}

public static class UtilityBillLineCategory
{
    public const string ElectricityConsumed = "ELECTRICITY_CONSUMED";
    public const string ElectricityTransport = "ELECTRICITY_TRANSPORT";
    public const string FixedMonthly = "FIXED_MONTHLY";
    public const string Subsidy = "SUBSIDY";
    public const string ServiceAdministration = "SERVICE_ADMINISTRATION";
    public const string MeterRental = "METER_RENTAL";
    public const string CommonService = "COMMON_SERVICE";
    public const string Vat19 = "VAT_19";
    public const string SimpleAdjustment = "SIMPLE_ADJUSTMENT";
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
    string SourceKind,
    long? SourceDocumentId,
    string ReviewState,
    double? IvaRate,
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
    string SourceKind,
    string EvidenceState,
    int? SourcePage,
    string? SourceText,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UtilityBillDocument(
    long DocumentId,
    string Provider,
    string OriginalFileName,
    string LocalPdfPath,
    string ContentSha256,
    long ContentLength,
    int PageCount,
    string ParserVersion,
    string? ExtractedText,
    DateTimeOffset ImportedUtc);

public sealed record UtilityBillFieldEvidence(
    long EvidenceId,
    long BillId,
    string FieldKey,
    string SourceKind,
    string EvidenceState,
    string? PrintedValueText,
    string? NormalizedValueText,
    int? SourcePage,
    string? SourceText,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UtilityBillPdfDraftLine(
    string SectionKey,
    string CategoryKey,
    string Description,
    double AmountClp,
    int SourcePage,
    string SourceText);

public sealed record UtilityBillPdfDraft(
    string SourcePath,
    string StoredPath,
    string OriginalFileName,
    string ContentSha256,
    long ContentLength,
    int PageCount,
    string ParserVersion,
    string ExtractedText,
    DateOnly? PeriodStart,
    DateOnly? PeriodEndInclusive,
    double? BilledConsumptionKwh,
    double? TotalDueClp,
    string? TariffPlan,
    IReadOnlyList<UtilityBillPdfDraftLine> Lines,
    IReadOnlyList<string> Warnings);

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
    string Detail)
{
    public UtilitySensitivityRange? Sensitivity { get; init; }
}

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
    string Detail)
{
    public UtilitySensitivityRange? Sensitivity { get; init; }
}

public sealed record UtilitySensitivityRange(
    double LowerKwh,
    double? UpperKwh,
    double BaselineObservedKwh,
    double? BoundaryAlternativeKwh,
    double UncoveredHours,
    double? ObservedMaximumKw,
    string Basis,
    bool IsFormalConfidenceInterval);

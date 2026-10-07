using System.Globalization;
using System.Text;

namespace SolarOfThings.Core.Utility;

public sealed class UtilityBillPdfReviewMergeService
{
    private readonly UtilityMeterRepository _repository;

    public UtilityBillPdfReviewMergeService(
        UtilityMeterRepository repository)
    {
        _repository = repository;
    }

    public UtilityBillPdfLineMergeResult Merge(
        long billId,
        UtilityBillPdfDraft draft)
    {
        var existing =
            _repository.GetBillLines(billId)
                .ToList();

        var nextSortOrder =
            existing.Count == 0
                ? 10
                : existing.Max(item => item.SortOrder) + 10;

        var updated = 0;
        var added = 0;
        var removedDuplicates = 0;

        foreach (var draftLine in draft.Lines)
        {
            var draftCategory =
                CanonicalBillLineCategory(
                    draftLine.CategoryKey,
                    draftLine.Description);

            var candidates = existing
                .Where(item =>
                {
                    var existingCategory =
                        CanonicalBillLineCategory(
                            item.CategoryKey,
                            item.Description);

                    var sameCategory =
                        !string.IsNullOrWhiteSpace(
                            draftCategory) &&
                        string.Equals(
                            existingCategory,
                            draftCategory,
                            StringComparison.OrdinalIgnoreCase);

                    return sameCategory ||
                           DescriptionsLikelySame(
                               item.Description,
                               draftLine.Description);
                })
                .OrderByDescending(item =>
                    Math.Abs(
                        item.AmountClp -
                        draftLine.AmountClp) <= 0.5)
                .ThenByDescending(item =>
                    string.Equals(
                        item.SourceKind,
                        UtilityBillSourceKind.PdfReviewed,
                        StringComparison.Ordinal))
                .ToArray();

            var match =
                candidates.FirstOrDefault();

            if (match is not null)
            {
                _repository.UpdateBillLine(
                    match.BillLineId,
                    draftLine.SectionKey,
                    draftLine.Description,
                    draftLine.AmountClp,
                    categoryKey:
                        draftCategory ??
                        draftLine.CategoryKey,
                    quantity: match.Quantity,
                    unit: match.Unit,
                    unitRateClp: match.UnitRateClp,
                    taxTreatment: match.TaxTreatment,
                    sourceKind:
                        UtilityBillSourceKind.PdfReviewed,
                    evidenceState:
                        UtilityBillEvidenceState.PdfExtractedConfirmed,
                    sourcePage: draftLine.SourcePage,
                    sourceText: draftLine.SourceText);
                updated++;

                foreach (var duplicate in candidates
                    .Skip(1)
                    .Where(item =>
                        Math.Abs(
                            item.AmountClp -
                            draftLine.AmountClp) <= 0.5))
                {
                    _repository.DeleteBillLine(
                        duplicate.BillLineId);
                    existing.RemoveAll(item =>
                        item.BillLineId ==
                        duplicate.BillLineId);
                    removedDuplicates++;
                }

                existing.RemoveAll(item =>
                    item.BillLineId ==
                    match.BillLineId);

                existing.Add(
                    match with
                    {
                        SectionKey =
                            draftLine.SectionKey,
                        CategoryKey =
                            draftCategory ??
                            draftLine.CategoryKey,
                        Description =
                            draftLine.Description,
                        AmountClp =
                            draftLine.AmountClp,
                        SourceKind =
                            UtilityBillSourceKind.PdfReviewed,
                        EvidenceState =
                            UtilityBillEvidenceState.PdfExtractedConfirmed,
                        SourcePage =
                            draftLine.SourcePage,
                        SourceText =
                            draftLine.SourceText
                    });

                continue;
            }

            var newId = _repository.AddBillLine(
                billId,
                draftLine.SectionKey,
                draftLine.Description,
                draftLine.AmountClp,
                categoryKey:
                    draftCategory ??
                    draftLine.CategoryKey,
                sortOrder: nextSortOrder,
                sourceKind:
                    UtilityBillSourceKind.PdfReviewed,
                evidenceState:
                    UtilityBillEvidenceState.PdfExtractedConfirmed,
                sourcePage: draftLine.SourcePage,
                sourceText: draftLine.SourceText);

            nextSortOrder += 10;
            added++;

            existing.Add(
                _repository.GetBillLines(billId)
                    .Single(item =>
                        item.BillLineId == newId));
        }

        return new UtilityBillPdfLineMergeResult(
            updated,
            added,
            removedDuplicates);
    }

    internal static string? CanonicalBillLineCategory(
        string? categoryKey,
        string description)
    {
        if (!string.IsNullOrWhiteSpace(
                categoryKey) &&
            !string.Equals(
                categoryKey,
                "CUSTOM",
                StringComparison.OrdinalIgnoreCase))
        {
            return categoryKey.Trim();
        }

        var normalized =
            NormalizeBillLineText(description);

        if (normalized.Contains(
                "ELECTRICIDADCONSUM",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.ElectricityConsumed;
        if (normalized.Contains(
                "TRANSPORTEDEELECTRICIDAD",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.ElectricityTransport;
        if (normalized.Contains(
                "SUBSIDIO",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.Subsidy;
        if (normalized.Contains(
                "ADMINISTRACIONDELSERVICIO",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.ServiceAdministration;
        if (normalized.Contains(
                "ARRIENDOMEDIDOR",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.MeterRental;
        if (normalized.Contains(
                "SERVICIOCOMUN",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.CommonService;
        if (normalized.Contains(
                "IVA19",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.Vat19;
        if (normalized.Contains(
                "AJUSTE",
                StringComparison.Ordinal))
            return UtilityBillLineCategory.SimpleAdjustment;

        return null;
    }

    internal static bool DescriptionsLikelySame(
        string left,
        string right)
    {
        var a =
            NormalizeBillLineText(left);
        var b =
            NormalizeBillLineText(right);

        return a.Length >= 5 &&
               b.Length >= 5 &&
               (a.Contains(
                    b,
                    StringComparison.Ordinal) ||
                b.Contains(
                    a,
                    StringComparison.Ordinal));
    }

    private static string NormalizeBillLineText(
        string value)
    {
        var form =
            value.Normalize(
                NormalizationForm.FormD);

        var chars = form
            .Where(character =>
                CharUnicodeInfo.GetUnicodeCategory(
                    character) !=
                UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();

        return new string(chars);
    }
}

public sealed record UtilityBillPdfLineMergeResult(
    int Updated,
    int Added,
    int RemovedDuplicates);

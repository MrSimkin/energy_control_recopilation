namespace SolarOfThings.Core.Backup;

/// <summary>
/// READ-ONLY diagnostic assembled exclusively from verified synthetic recovery
/// plan and relationship-audit observations. This is NOT a restoration command,
/// authorization, planner, or instruction for actual owner databases.
/// </summary>
public static class IsolatedRecoveryReadinessReceiptService
{
    public static SyntheticRecoveryReadinessReceipt Evaluate(
        SyntheticRecoveryPlan plan, RecoveryRelationAudit graph)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(graph);

        // The graph and plan must describe the same reviewed source/target
        // schema family. The real staging service independently rechecks
        // package bytes and all target data before any synthetic write.
        var hasApprovedPlan = plan.Status == "SYNTHETIC_SETTINGS_PLAN" &&
            plan.Steps.Count == 8 &&
            plan.Steps.Select(step => step.Order)
                .SequenceEqual(Enumerable.Range(1, 8)) &&
            plan.Steps.Count(step => step.Status == "SYNTHETIC_STAGE_ONLY") == 1 &&
            plan.Steps.Single(step => step.Status == "SYNTHETIC_STAGE_ONLY").Category ==
                "SETTINGS" &&
            plan.Steps.Where(step => step.Category != "SETTINGS")
                .All(step => step.Status != "SYNTHETIC_STAGE_ONLY") &&
            HasSha256(plan.PlanId) && HasSha256(plan.SourcePackageSha256) &&
            HasSha256(plan.TargetSettingsSha256) &&
            HasSha256(plan.TargetOtherTablesSha256);

        var schemaCompatible = graph.Status == "READ_ONLY_GRAPH_AUDIT" &&
            graph.SourceSchemaVersion == graph.TargetSchemaVersion &&
            graph.SourceSchemaVersion > 0;
        var canConsiderSettingsStage = hasApprovedPlan && schemaCompatible;
        var bills = graph.Bills.Count;
        var tariffs = graph.Tariffs.Count;
        var unmatchedDocuments = graph.UnlinkedBillDocuments.Count;
        var billsWithReadingLinks = graph.Bills.Count(b =>
            b.HasFromReading || b.HasToReading);
        var billsWithPotentialDocumentOverlap = graph.Bills.Count(b =>
            b.TargetHasOriginalDocument);
        var tariffsWithPotentialPdfOverlap = graph.Tariffs.Count(t =>
            t.TargetPdfMatches > 0);

        return new SyntheticRecoveryReadinessReceipt(
            canConsiderSettingsStage ? "SYNTHETIC_SETTINGS_STAGE_ELIGIBLE" :
                "BLOCKED_FOR_SYNTHETIC_STAGE",
            canConsiderSettingsStage,
            false, // No real or relational import authorization, ever.
            bills, tariffs, unmatchedDocuments,
            billsWithReadingLinks, billsWithPotentialDocumentOverlap,
            tariffsWithPotentialPdfOverlap,
            graph.Totals.OutgoingTariffLinks,
            graph.Totals.UnresolvedOutgoingLinks,
            plan.ConflictingSettings, plan.TargetOnlySettings,
            "Read-only synthetic graph diagnosis. All bills, meter readings, tariff " +
            "relations, PDFs and telemetry remain blocked for import. Only the " +
            "separate synthetic settings staging API may be considered, and it " +
            "must reverify the original plan and both inputs before use.");
    }

    private static bool HasSha256(string? sha) =>
        sha is { Length: 64 } && sha.All(c =>
            c is >= '0' and <= '9' or >= 'A' and <= 'F');
}

public sealed record SyntheticRecoveryReadinessReceipt(
    string Status,
    bool SyntheticSettingsStageEligible,
    bool RealOrRelationalImportAuthorized,
    int BlockedBillGroups,
    int BlockedTariffGroups,
    int UnlinkedBillDocumentsForReview,
    int BillGroupsWithMeterLinks,
    int BillGroupsWithDocumentOverlap,
    int TariffGroupsWithPdfOverlap,
    long OutgoingTariffRelationEdges,
    long UnresolvedTariffRelationEdges,
    int ExistingSettingsConflicts,
    int ExistingTargetOnlySettings,
    string SafetyExplanation);

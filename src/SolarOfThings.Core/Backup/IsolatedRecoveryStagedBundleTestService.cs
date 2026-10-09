namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST-ONLY orchestration. Produces a NEW additive settings fixture plus
/// isolated verified document bytes. Relational links remain read-only
/// diagnostics. No live restore, activation, FK rewrite or PDF DB import.
/// </summary>
public sealed class IsolatedRecoveryStagedBundleTestService
{
    private readonly IsolatedRecoveryPlanService _planner = new();
    private readonly IsolatedRecoveryDocumentStageTestService _documents = new();
    private readonly IsolatedRecoveryRelationAuditService _relations = new();
    private readonly IsolatedRecoveryDocumentLinkPreviewService _links = new();

    public SyntheticRecoveryStagedBundle Stage(
        SyntheticRecoveryPlan approvedPlan, string completeBackupZip,
        string syntheticTargetDatabase, CancellationToken cancellationToken = default,
        bool simulateFailureAfterSettings = false,
        IReadOnlyCollection<string>? selectedDocumentHashes = null)
    {
        ArgumentNullException.ThrowIfNull(approvedPlan);
        cancellationToken.ThrowIfCancellationRequested();
        // Only the pre-existing synthetic planner may authorize additive
        // settings staging; it checks source ZIP, WAL-visible target hash,
        // all other-table hashes and eight-stage checklist anew.
        var stagedSettings = _planner.StageSettingsOnly(
            approvedPlan, completeBackupZip, syntheticTargetDatabase);
        SyntheticDocumentStageReceipt? evidence = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (simulateFailureAfterSettings)
                throw new InvalidOperationException(
                    "SYNTHETIC_INJECTED_AFTER_SETTINGS_STAGE");

            evidence = _documents.Stage(completeBackupZip,
                stagedSettings.StagedDatabasePath, cancellationToken,
                selectedDocumentHashes: selectedDocumentHashes);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_documents.VerifyAgainstArchive(evidence,
                    completeBackupZip, stagedSettings.StagedDatabasePath))
                throw new InvalidDataException("Staged documentary proof is inconsistent.");

            var audit = _relations.Audit(
                completeBackupZip, stagedSettings.StagedDatabasePath);
            var links = _links.Analyze(evidence, audit,
                stagedSettings.StagedDatabasePath, completeBackupZip);
            cancellationToken.ThrowIfCancellationRequested();

            // Revalidate original binding after the staged work; the source
            // or original synthetic target must not have been silently replaced.
            var again = _planner.CreatePlan(
                completeBackupZip, syntheticTargetDatabase);
            if (again.PlanId != approvedPlan.PlanId ||
                again.SourcePackageSha256 != approvedPlan.SourcePackageSha256 ||
                again.TargetOtherTablesSha256 != approvedPlan.TargetOtherTablesSha256 ||
                again.TargetSettingsSha256 != approvedPlan.TargetSettingsSha256)
                throw new InvalidDataException(
                    "Synthetic source or original target changed during staging.");

            return new SyntheticRecoveryStagedBundle(
                stagedSettings, evidence, links,
                "STAGED_SYNTHETIC_SETTINGS_AND_DOCUMENT_EVIDENCE",
                false,
                "Only a newly generated test SQLite and independent verified " +
                "document files were produced. Every source identity, bill, meter, " +
                "tariff and foreign-key relation is still REVIEW ONLY or BLOCKED.");
        }
        catch
        {
            if (evidence is not null &&
                Directory.Exists(evidence.StageDirectory))
                Directory.Delete(evidence.StageDirectory, recursive: true);
            if (File.Exists(stagedSettings.StagedDatabasePath))
                File.Delete(stagedSettings.StagedDatabasePath);
            throw;
        }
    }
}

public sealed record SyntheticRecoveryStagedBundle(
    SyntheticAdditiveImportResult Settings,
    SyntheticDocumentStageReceipt Evidence,
    SyntheticDocumentLinkPreview ReadOnlyLinks,
    string Status,
    bool RealRestoreAuthorized,
    string SafetyExplanation);

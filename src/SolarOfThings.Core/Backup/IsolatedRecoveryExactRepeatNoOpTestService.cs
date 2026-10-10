using System.Security.Cryptography;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST ONLY: completed, previously staged, identity-preserving recovery
/// graphs may be reapplied as an explicit NO-OP. Never performs INSERT,
/// UPDATE, DELETE, copies, activation or writes to SQLite. Any different
/// content, PDF or source ZIP is a conflict, not a new/merged import.
/// This is NOT general-purpose additive/idempotent restoration.
/// </summary>
public sealed class IsolatedRecoveryExactRepeatNoOpTestService
{
    private readonly IsolatedRecoveryReplayAuditService _auditor = new();

    public SyntheticExactRepeatNoOpResult ReapplyExistingBillGraph(
        string exactOriginalArchive,
        SyntheticLinkedBillGraphImport alreadyApplied,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alreadyApplied);
        var fingerprint = ReadFingerprint(alreadyApplied.StagedDatabasePath,
            "recovery-linked-bills-staged-", cancellationToken);
        var verified = _auditor.InspectBills(exactOriginalArchive, alreadyApplied,
            cancellationToken);
        if (verified.Status != "EXACT_SYNTHETIC_REPLAY_PREVIEW_ONLY" ||
            verified.ExactGraphs != alreadyApplied.AddedBills ||
            alreadyApplied.AddedBills < 1 || verified.RealRestoreAuthorized)
            throw new InvalidDataException("Bill graph replay identity cannot be established.");
        RequireUnchanged(alreadyApplied.StagedDatabasePath, fingerprint, cancellationToken);
        return new SyntheticExactRepeatNoOpResult(
            "ALREADY_APPLIED_EXACT_SYNTHETIC_NO_OP", fingerprint, verified.ExactGraphs,
            0, 0, false,
            "Same previously staged synthetic bill graph; no SQL or file writes. " +
            "Changing source, target or FK relations is a blocked conflict, not an import.");
    }

    public SyntheticExactRepeatNoOpResult ReapplyExistingTariffGraph(
        string exactOriginalArchive,
        SyntheticTariffGraphImport alreadyApplied,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alreadyApplied);
        var fingerprint = ReadFingerprint(alreadyApplied.StagedDatabasePath,
            "recovery-tariff-graph-staged-", cancellationToken);
        var verified = _auditor.InspectTariffs(exactOriginalArchive, alreadyApplied,
            cancellationToken);
        if (verified.Status != "EXACT_SYNTHETIC_REPLAY_PREVIEW_ONLY" ||
            verified.ExactGraphs != alreadyApplied.AddedPublications ||
            alreadyApplied.AddedPublications < 1 || verified.RealRestoreAuthorized)
            throw new InvalidDataException("Tariff graph replay identity cannot be established.");
        RequireUnchanged(alreadyApplied.StagedDatabasePath, fingerprint, cancellationToken);
        return new SyntheticExactRepeatNoOpResult(
            "ALREADY_APPLIED_EXACT_SYNTHETIC_NO_OP", fingerprint, verified.ExactGraphs,
            0, 0, false,
            "Same previously staged synthetic tariff graph; no SQL or file writes. " +
            "Changing source, target or correction evidence is a blocked conflict.");
    }

    private static string ReadFingerprint(string path, string prefix,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var full = Path.GetFullPath(path);
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(full);
        var name = Path.GetFileName(full);
        if (!string.Equals(Path.GetDirectoryName(full), root,
                StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(name[prefix.Length..^3], "N", out _) ||
            !File.Exists(full) ||
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Not a reviewed generated recovery fixture.");
        using var file = File.OpenRead(full);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static void RequireUnchanged(string path, string before,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!string.Equals(ReadFingerprint(path,
                Path.GetFileName(path).StartsWith("recovery-linked-bills-staged-",
                    StringComparison.Ordinal)
                    ? "recovery-linked-bills-staged-" : "recovery-tariff-graph-staged-",
                token), before, StringComparison.Ordinal))
            throw new InvalidDataException("Synthetic target changed during no-op reapplication.");
    }
}

public sealed record SyntheticExactRepeatNoOpResult(
    string Status, string OriginalDatabaseSha256, int VerifiedGraphs,
    int AddedRows, int ModifiedRows, bool RealRestoreAuthorized,
    string SafetyExplanation);

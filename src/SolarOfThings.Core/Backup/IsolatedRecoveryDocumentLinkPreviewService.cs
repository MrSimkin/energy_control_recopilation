namespace SolarOfThings.Core.Backup;

/// <summary>
/// Read-only classification of evidence already staged under a marked test
/// fixture. SHA-256 identity of bytes is NOT portable identity of a bill,
/// meter reading, tariff publication, or any foreign-key relation.
/// </summary>
public sealed class IsolatedRecoveryDocumentLinkPreviewService
{
    private readonly IsolatedRecoveryDocumentStageTestService _stages = new();

    public SyntheticDocumentLinkPreview Analyze(
        SyntheticDocumentStageReceipt receipt,
        RecoveryRelationAudit graph,
        string syntheticTargetDatabase,
        string verifiedSourceArchive)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(graph);
        if (!_stages.VerifyAgainstArchive(
                receipt, verifiedSourceArchive, syntheticTargetDatabase) ||
            graph.Status != "READ_ONLY_GRAPH_AUDIT" ||
            graph.SourceSchemaVersion != graph.TargetSchemaVersion)
            throw new InvalidOperationException(
                "Only verified synthetic document evidence and an audited graph may be compared.");

        var lookup = receipt.Documents
            .GroupBy(x => (x.Category, x.Sha256.ToUpperInvariant()))
            .ToDictionary(x => x.Key, x => x.Count());

        var repeatedSourceDigest = graph.Bills
            .Where(x => !string.IsNullOrWhiteSpace(x.OriginalDocumentSha256))
            .Select(x => ("Bills", x.OriginalDocumentSha256!.ToUpperInvariant()))
            .Concat(graph.UnlinkedBillDocuments
                .Where(x => !string.IsNullOrWhiteSpace(x.OriginalDocumentSha256))
                .Select(x => ("Bills", x.OriginalDocumentSha256!.ToUpperInvariant())))
            .Concat(graph.Tariffs
                .Where(x => !string.IsNullOrWhiteSpace(x.OriginalPdfSha256))
                .Select(x => ("Tariffs", x.OriginalPdfSha256!.ToUpperInvariant())))
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet();

        SyntheticDocumentLinkCandidate Classify(
            string kind, long sourceId, string category, string? sha,
            bool targetAlreadyHasEvidence)
        {
            var digest = sha?.ToUpperInvariant();
            var matches = digest is null ? 0 :
                lookup.TryGetValue((category, digest), out var n) ? n : 0;
            var state = string.IsNullOrWhiteSpace(digest) ? "NO_DOCUMENT_HASH" :
                matches == 0 ? "NOT_IN_SELECTED_STAGED_EVIDENCE" :
                matches == 1 && repeatedSourceDigest.Contains((category, digest!))
                    ? "SHARED_SOURCE_DOCUMENT_REVIEW" :
                matches == 1 ? "DOCUMENT_BYTES_STAGED_REVIEW_ONLY" :
                "AMBIGUOUS_STAGED_DOCUMENT_BYTES";
            return new SyntheticDocumentLinkCandidate(kind, sourceId,
                category, digest, matches, targetAlreadyHasEvidence, state);
        }

        var candidates = graph.Bills
            .Select(x => Classify("BILL", x.SourceBillId, "Bills",
                x.OriginalDocumentSha256, x.TargetHasOriginalDocument))
            .Concat(graph.UnlinkedBillDocuments.Select(x =>
                Classify("UNLINKED_BILL_DOCUMENT", x.SourceDocumentId,
                    "Bills", x.OriginalDocumentSha256,
                    x.TargetHasOriginalDocument)))
            .Concat(graph.Tariffs.Select(x =>
                Classify("TARIFF_PUBLICATION", x.SourcePublicationId,
                    "Tariffs", x.OriginalPdfSha256, x.TargetPdfMatches > 0)))
            .ToArray();

        return new SyntheticDocumentLinkPreview(
            "READ_ONLY_SYNTHETIC_DOCUMENT_LINK_AUDIT",
            receipt.Documents.Count,
            candidates,
            candidates.Count(x => x.State == "DOCUMENT_BYTES_STAGED_REVIEW_ONLY"),
            candidates.Count(x => x.State == "NOT_IN_SELECTED_STAGED_EVIDENCE"),
            candidates.Count(x => x.State == "AMBIGUOUS_STAGED_DOCUMENT_BYTES"),
            false,
            "All related bill, meter, tariff and publication identities remain " +
            "unmapped. Matching a SHA verifies bytes only, not a foreign-key identity.")
        {
            SharedSourceDocumentCandidates = candidates.Count(x =>
                x.State == "SHARED_SOURCE_DOCUMENT_REVIEW")
        };
    }
}

public sealed record SyntheticDocumentLinkCandidate(
    string SourceKind, long SourceLocalId, string DocumentCategory,
    string? Sha256, int StagedMatches, bool TargetHasPotentialOverlap,
    string State);

public sealed record SyntheticDocumentLinkPreview(
    string Status, int StagedFiles,
    IReadOnlyList<SyntheticDocumentLinkCandidate> Candidates,
    int CandidatesWithUniqueStagedBytes, int CandidatesWithoutSelectedBytes,
    int AmbiguousByteCandidates, bool RelationalRestoreAuthorized,
    string SafetyExplanation)
{
    public int SharedSourceDocumentCandidates { get; init; }
}

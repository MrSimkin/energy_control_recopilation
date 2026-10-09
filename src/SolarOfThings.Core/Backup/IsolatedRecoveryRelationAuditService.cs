using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST-ONLY, read-only dependency audit for v17 bill/tariff graphs.
/// Never treats surrogate IDs, equal timestamps or invoice dates as portable
/// record identity. Does not import, migrate, extract documents or alter Data.
/// </summary>
public sealed class IsolatedRecoveryRelationAuditService
{
    private const int MaxBills = 10_000;
    private const int MaxTariffs = 10_000;
    private const int MaxUnlinkedDocuments = 10_000;

    public RecoveryRelationAudit Audit(string fullPackage, string syntheticTargetDatabase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(syntheticTargetDatabase);
        var targetPath = Path.GetFullPath(syntheticTargetDatabase);
        var root = IsolatedRecoveryAdditiveTestService
            .RequireSyntheticFixtureRoot(targetPath);
        if (!File.Exists(targetPath) ||
            (File.GetAttributes(targetPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Target is not a regular synthetic fixture database.");

        // Validate hashes, ZIP inventory, embedded integrity, FK and schema before
        // trusting the source graph or creating a disposable extracted DB.
        var manifest = FullBackupService.VerifyArchive(fullPackage);
        if (manifest.SchemaVersion != SqliteDatabase.CurrentSchemaVersion)
            return new RecoveryRelationAudit("UNSUPPORTED_SOURCE_SCHEMA",
                manifest.SchemaVersion, -1, [], [],
                "No reviewed historical version adapter. No changes performed.");

        var unique = Guid.NewGuid().ToString("N");
        var staged = Path.Combine(root, ".relation-audit-source-" + unique + ".db");
        try
        {
            using (var zip = ZipFile.OpenRead(fullPackage))
            using (var input = (zip.GetEntry("database/energy.db") ??
                       throw new InvalidDataException("Missing embedded database.")).Open())
            using (var output = new FileStream(staged, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
                input.CopyTo(output);

            using var source = ReadOnly(staged);
            using var target = ReadOnly(targetPath);
            var sourceVersion = SchemaVersion(source);
            var targetVersion = SchemaVersion(target);
            if (sourceVersion != manifest.SchemaVersion ||
                targetVersion != SqliteDatabase.CurrentSchemaVersion)
                return new RecoveryRelationAudit("UNSUPPORTED_TARGET_SCHEMA",
                    sourceVersion, targetVersion, [], [],
                    "Source or destination schema differs from reviewed v17 adapter.");
            // A source archive has already passed its SQLite checks, but the
            // synthetic destination is independently mutable and must pass
            // integrity/FK checks before any relationship classification.
            IsolatedRecoveryTargetHealth.RequireHealthy(target);

            // Package data has verified bytes. Source document file paths are
            // device-local and may not exist on the destination computer;
            // match archived category + SHA, not an unportable absolute path.
            var billsByDigest = manifest.Files
                .Where(f => f.RelativePath.StartsWith("documents/Bills/", StringComparison.Ordinal))
                .GroupBy(f => f.Sha256, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            var tariffsByDigest = manifest.Files
                .Where(f => f.RelativePath.StartsWith("documents/Tariffs/", StringComparison.Ordinal))
                .GroupBy(f => f.Sha256, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            var bills = AuditBills(source, target, billsByDigest);
            var tariffs = AuditTariffs(source, target, tariffsByDigest);
            // An original source document may be valid yet not linked to a
            // bill. This separate bounded inventory prevents silently
            // omitting its evidence from a potential selective recovery.
            var unlinked = AuditUnlinkedBillDocuments(source, target, billsByDigest);
            var reviewCount = bills.Count(b => b.State != "DOCUMENT_ONLY_NEW_CANDIDATE") +
                              tariffs.Count(t => t.State != "NEW_TARIFF_SOURCE_CANDIDATE");
            return new RecoveryRelationAudit("READ_ONLY_GRAPH_AUDIT",
                sourceVersion, targetVersion, bills, tariffs,
                $"All bill/tariff graph groups require adapter review before import. " +
                $"{reviewCount} groups have explicit additional blockers or overlapping evidence. " +
                "No identity merge, file extraction, database write or activation occurred.")
            {
                UnlinkedBillDocuments = unlinked
            };
        }
        finally
        {
            try { if (File.Exists(staged)) File.Delete(staged); }
            catch (IOException) { /* disposable audit artifact only */ }
            catch (UnauthorizedAccessException) { /* owner DB never accessed */ }
        }
    }

    private static IReadOnlyList<BillGraphPreview> AuditBills(SqliteConnection source,
        SqliteConnection target, IReadOnlyDictionary<string, int> archived)
    {
        var result = new List<BillGraphPreview>();
        using var command = source.CreateCommand();
        command.CommandText = """
            SELECT b.bill_id, b.source_document_id, d.content_sha256,
                   b.from_reading_id, b.to_reading_id,
                   (SELECT COUNT(*) FROM utility_bill_line l WHERE l.bill_id=b.bill_id),
                   (SELECT COUNT(*) FROM utility_bill_field_evidence e WHERE e.bill_id=b.bill_id),
                   (SELECT COUNT(*) FROM utility_bill b2
                    WHERE b2.source_document_id=b.source_document_id
                      AND b.source_document_id IS NOT NULL)
              FROM utility_bill b
              LEFT JOIN utility_bill_document d ON d.document_id=b.source_document_id
             ORDER BY b.bill_id LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", MaxBills + 1);
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            if (result.Count >= MaxBills)
                throw new InvalidDataException("Too many bill groups for bounded audit.");
            var billId = rows.GetInt64(0);
            var documentId = rows.IsDBNull(1) ? (long?)null : rows.GetInt64(1);
            var sha = rows.IsDBNull(2) ? null : rows.GetString(2);
            var from = rows.IsDBNull(3) ? (long?)null : rows.GetInt64(3);
            var to = rows.IsDBNull(4) ? (long?)null : rows.GetInt64(4);
            var lineCount = rows.GetInt64(5);
            var evidenceCount = rows.GetInt64(6);
            // A source document is not necessarily one-to-one with a bill.
            // Identical source document IDs are not portable target bill IDs.
            var sourceBillsForDocument = rows.GetInt64(7);
            var targetBillsForDocument = !string.IsNullOrWhiteSpace(sha)
                ? CountTargetBillsByDocument(target, sha) : 0;
            var fromCandidates = ReadingCandidateCounts(source, target, from);
            var toCandidates = ReadingCandidateCounts(source, target, to);
            // Overlap is relevant even when a meter-reading foreign key
            // forces the bill itself into a blocked recovery category.
            var targetHasDocument = !string.IsNullOrWhiteSpace(sha) &&
                HasDocumentHash(target, "utility_bill_document", sha);

            string status, explanation;
            if (documentId is null)
            {
                status = "NO_PORTABLE_BILL_IDENTITY";
                explanation = "No original document hash; period/reference alone cannot identify this bill.";
            }
            else if (string.IsNullOrWhiteSpace(sha))
            {
                status = "BROKEN_DOCUMENT_LINK";
                explanation = "Bill references a source document that does not exist.";
            }
            else if (!archived.TryGetValue(sha, out var files))
            {
                status = "MISSING_ARCHIVED_BILL_DOCUMENT";
                explanation = "The referenced document SHA is absent from archived Bills files.";
            }
            else if (files != 1)
            {
                status = "AMBIGUOUS_ARCHIVED_DOCUMENT";
                explanation = "Multiple Bills files have the same content hash; mapping requires review.";
            }
            else if ((from.HasValue && !HasId(source, "utility_meter_reading", "reading_id", from.Value)) ||
                     (to.HasValue && !HasId(source, "utility_meter_reading", "reading_id", to.Value)))
            {
                status = "BROKEN_READING_LINK";
                explanation = "Referenced meter reading is missing from source snapshot.";
            }
            else if (from.HasValue || to.HasValue)
            {
                status = "READING_REMAP_REQUIRED";
                explanation = "Meter-reading IDs are local; identity and bill foreign-key mapping are unresolved.";
            }
            else if (targetHasDocument)
            {
                status = "DOCUMENT_ALREADY_IN_TARGET_REVIEW";
                explanation = "The same original document bytes exist in target; bill/lines may still conflict.";
            }
            else
            {
                status = "DOCUMENT_ONLY_NEW_CANDIDATE";
                explanation = "Archived original document is unique; associated bill, charge and field identities still require an adapter.";
            }

            result.Add(new BillGraphPreview(billId, sha, lineCount, evidenceCount,
                from.HasValue, to.HasValue, status, explanation)
            {
                TargetHasOriginalDocument = targetHasDocument,
                SourceBillsForDocument = sourceBillsForDocument,
                TargetBillsForDocument = targetBillsForDocument,
                FromReadingExactCandidates = fromCandidates.Exact,
                ToReadingExactCandidates = toCandidates.Exact,
                FromReadingTimestampCandidates = fromCandidates.Timestamp,
                ToReadingTimestampCandidates = toCandidates.Timestamp
            });
        }
        return result;
    }

    private static IReadOnlyList<UnlinkedBillDocumentPreview> AuditUnlinkedBillDocuments(
        SqliteConnection source, SqliteConnection target,
        IReadOnlyDictionary<string, int> archived)
    {
        var result = new List<UnlinkedBillDocumentPreview>();
        using var cmd = source.CreateCommand();
        cmd.CommandText = """
            SELECT d.document_id, d.content_sha256
              FROM utility_bill_document d
             WHERE NOT EXISTS (SELECT 1 FROM utility_bill b
                                WHERE b.source_document_id=d.document_id)
             ORDER BY d.document_id LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", MaxUnlinkedDocuments + 1);
        using var rows = cmd.ExecuteReader();
        while (rows.Read())
        {
            if (result.Count >= MaxUnlinkedDocuments)
                throw new InvalidDataException(
                    "Too many unlinked source documents for bounded recovery audit.");
            var id = rows.GetInt64(0);
            var hash = rows.IsDBNull(1) ? null : rows.GetString(1);
            var inTarget = !string.IsNullOrWhiteSpace(hash) &&
                HasDocumentHash(target, "utility_bill_document", hash);
            string state, reason;
            if (string.IsNullOrWhiteSpace(hash))
            {
                state = "UNLINKED_DOCUMENT_NO_HASH";
                reason = "No portable original-document digest is available.";
            }
            else if (!archived.TryGetValue(hash, out var archivedCount))
            {
                state = "UNLINKED_DOCUMENT_MISSING_ARCHIVE";
                reason = "Source document bytes are not included in the verified Bills archive.";
            }
            else if (archivedCount != 1)
            {
                state = "UNLINKED_DOCUMENT_AMBIGUOUS_ARCHIVE";
                reason = "Several archived Bills files share this digest.";
            }
            else if (inTarget)
            {
                state = "UNLINKED_DOCUMENT_ALREADY_IN_TARGET";
                reason = "Original document bytes already exist in destination; linked identity not inferred.";
            }
            else
            {
                state = "UNLINKED_DOCUMENT_CANDIDATE";
                reason = "Unique archived original with no bill association; document-only preview, NOT authorization to import.";
            }
            result.Add(new UnlinkedBillDocumentPreview(id, hash, inTarget,
                state, reason));
        }
        return result;
    }

    private static IReadOnlyList<TariffGraphPreview> AuditTariffs(SqliteConnection source,
        SqliteConnection target, IReadOnlyDictionary<string, int> archived)
    {
        var result = new List<TariffGraphPreview>();
        using var command = source.CreateCommand();
        command.CommandText = """
            SELECT p.publication_id, p.source_url, p.content_sha256,
                   p.local_pdf_path, p.capture_status,
                   (SELECT COUNT(*) FROM tariff_publication_page_text t
                     WHERE t.publication_id=p.publication_id),
                   (SELECT COUNT(*) FROM tariff_rate_candidate r
                     WHERE r.publication_id=p.publication_id),
                   (SELECT COUNT(*) FROM tariff_publication_relation x
                     WHERE x.source_publication_id=p.publication_id),
                   (SELECT COUNT(*) FROM tariff_publication_relation x
                     WHERE x.source_publication_id=p.publication_id
                       AND x.target_publication_id IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM tariff_publication y
                         WHERE y.publication_id=x.target_publication_id)),
                   (SELECT COUNT(*) FROM tariff_publication_relation x
                     WHERE x.target_publication_id=p.publication_id),
                   (SELECT COUNT(*) FROM tariff_publication_relation x
                     WHERE x.source_publication_id=p.publication_id
                       AND x.target_publication_id IS NULL)
              FROM tariff_publication p
             ORDER BY p.publication_id LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", MaxTariffs + 1);
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            if (result.Count >= MaxTariffs)
                throw new InvalidDataException("Too many tariff groups for bounded audit.");
            var id = rows.GetInt64(0);
            var sourceUrl = rows.GetString(1);
            var sha = rows.IsDBNull(2) ? null : rows.GetString(2);
            var hasPath = !rows.IsDBNull(3) && !string.IsNullOrWhiteSpace(rows.GetString(3));
            var captureStatus = rows.GetString(4);
            var pages = rows.GetInt64(5);
            var candidates = rows.GetInt64(6);
            var relations = rows.GetInt64(7);
            var brokenRelations = rows.GetInt64(8);
            // Corrections can refer TO this publication from another source;
            // outbound-only checks miss a necessary graph dependency.
            var incomingRelations = rows.GetInt64(9);
            var unresolvedOutgoingRelations = rows.GetInt64(10);
            // Hash overlap and portable source-URL overlap are distinct.
            // Matching either does NOT transfer surrogate publication IDs.
            var targetPdfMatches = CountTargetTariffPdfMatches(target, sha);
            var targetReferencedUrls = CountTargetReferencedTariffUrls(source, target, id);

            string status, explanation;
            if (brokenRelations > 0)
            {
                status = "BROKEN_TARIFF_RELATION";
                explanation = "A tariff reference points to a missing source publication.";
            }
            else if (hasPath && (string.IsNullOrWhiteSpace(sha) ||
                     !archived.TryGetValue(sha, out var files) || files != 1))
            {
                status = "TARIFF_PDF_NOT_PORTABLE";
                explanation = "Captured tariff PDF is missing or ambiguously mapped by SHA.";
            }
            else if (TryGetTariffDocumentHash(target, sourceUrl, out var targetSha))
            {
                if (!string.IsNullOrWhiteSpace(sha) &&
                    !string.IsNullOrWhiteSpace(targetSha))
                {
                    var same = string.Equals(sha, targetSha,
                        StringComparison.OrdinalIgnoreCase);
                    status = same
                        ? "TARIFF_SOURCE_SAME_DOCUMENT_REVIEW"
                        : "TARIFF_SOURCE_CONTENT_CONFLICT_REVIEW";
                    explanation = same
                        ? "Same source URL and original PDF bytes; dependent tariff records still require review."
                        : "Same source URL but different original PDF hashes; no overwrite or automatic merge.";
                }
                else
                {
                    status = "TARIFF_SOURCE_OVERLAP_REVIEW";
                    explanation = "Source URL already exists in target but document evidence is incomplete.";
                }
            }
            else if (candidates > 0 || relations > 0 || incomingRelations > 0)
            {
                status = "DEPENDENT_TARIFF_GRAPH_REMAP_REQUIRED";
                explanation = "Rate candidates or incoming/outgoing correction links use local publication IDs and need graph remapping.";
            }
            else
            {
                status = "NEW_TARIFF_SOURCE_CANDIDATE";
                explanation = "No matching source URL; only a preview, not permission to import.";
            }
            result.Add(new TariffGraphPreview(id, sourceUrl, sha,
                pages, candidates, relations, captureStatus, status, explanation)
            {
                IncomingRelations = incomingRelations,
                UnresolvedOutgoingRelations = unresolvedOutgoingRelations,
                TargetPdfMatches = targetPdfMatches,
                ReferencedPublicationUrlsInTarget = targetReferencedUrls
            });
        }
        return result;
    }

    private static long CountTargetBillsByDocument(SqliteConnection target, string digest)
    {
        using var cmd = target.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM utility_bill b
            JOIN utility_bill_document d ON d.document_id=b.source_document_id
            WHERE d.content_sha256=$hash;
            """;
        cmd.Parameters.AddWithValue("$hash", digest);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    // Exact timestamp and numeric reading candidates are diagnostic only.
    // A reading_id, source_kind, time precision and provenance still require
    // independent identity review; these counters never authorize remapping.
    private static (long Exact, long Timestamp) ReadingCandidateCounts(
        SqliteConnection source, SqliteConnection target, long? sourceReadingId)
    {
        if (!sourceReadingId.HasValue)
            return (0, 0);
        using var original = source.CreateCommand();
        original.CommandText = """
            SELECT reading_at_utc, reading_kwh
              FROM utility_meter_reading WHERE reading_id=$id;
            """;
        original.Parameters.AddWithValue("$id", sourceReadingId.Value);
        using var sourceRow = original.ExecuteReader();
        if (!sourceRow.Read())
            return (0, 0);
        var stamp = sourceRow.GetString(0);
        var value = sourceRow.GetDouble(1);
        using var candidates = target.CreateCommand();
        candidates.CommandText = """
            SELECT COUNT(*), COALESCE(SUM(CASE WHEN reading_kwh=$kwh
                THEN 1 ELSE 0 END), 0)
              FROM utility_meter_reading WHERE reading_at_utc=$stamp;
            """;
        candidates.Parameters.AddWithValue("$stamp", stamp);
        candidates.Parameters.AddWithValue("$kwh", value);
        using var counts = candidates.ExecuteReader();
        return counts.Read() ? (counts.GetInt64(1), counts.GetInt64(0)) : (0, 0);
    }

    private static bool HasId(SqliteConnection conn, string table, string field, long id)
    {
        using var command = conn.CreateCommand();
        command.CommandText = $"SELECT 1 FROM {table} WHERE {field}=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is not null;
    }

    private static bool HasDocumentHash(SqliteConnection conn, string table, string hash)
    {
        using var command = conn.CreateCommand();
        command.CommandText = $"SELECT 1 FROM {table} WHERE content_sha256=$hash LIMIT 1;";
        command.Parameters.AddWithValue("$hash", hash);
        return command.ExecuteScalar() is not null;
    }

    private static long CountTargetTariffPdfMatches(SqliteConnection target, string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return 0;
        using var cmd = target.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM tariff_publication
             WHERE content_sha256=$hash COLLATE NOCASE;
            """;
        cmd.Parameters.AddWithValue("$hash", hash);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    // A source relation's target is a local surrogate key. Resolve it only
    // inside its own source snapshot, then count URL identity hints in target.
    // No matching URL may be used as an automatic foreign-key remap.
    private static long CountTargetReferencedTariffUrls(
        SqliteConnection source, SqliteConnection target, long sourcePublicationId)
    {
        using var cmd = source.CreateCommand();
        cmd.CommandText = """
            SELECT p.source_url FROM tariff_publication_relation r
            JOIN tariff_publication p ON p.publication_id=r.target_publication_id
            WHERE r.source_publication_id=$id;
            """;
        cmd.Parameters.AddWithValue("$id", sourcePublicationId);
        long total = 0;
        using var rows = cmd.ExecuteReader();
        while (rows.Read())
        {
            using var lookup = target.CreateCommand();
            lookup.CommandText =
                "SELECT EXISTS(SELECT 1 FROM tariff_publication WHERE source_url=$url);";
            lookup.Parameters.AddWithValue("$url", rows.GetString(0));
            if (Convert.ToInt64(lookup.ExecuteScalar()) == 1) total++;
        }
        return total;
    }

    private static bool TryGetTariffDocumentHash(SqliteConnection conn, string url,
        out string? hash)
    {
        using var command = conn.CreateCommand();
        command.CommandText =
            "SELECT content_sha256 FROM tariff_publication WHERE source_url=$url LIMIT 1;";
        command.Parameters.AddWithValue("$url", url);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            hash = null;
            return false;
        }
        hash = reader.IsDBNull(0) ? null : reader.GetString(0);
        return true;
    }

    private static int SchemaVersion(SqliteConnection conn)
    {
        using var query = conn.CreateCommand();
        query.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(query.ExecuteScalar());
    }

    private static SqliteConnection ReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var enforce = connection.CreateCommand();
        enforce.CommandText = "PRAGMA query_only=ON;";
        enforce.ExecuteNonQuery();
        return connection;
    }
}

public sealed record BillGraphPreview(long SourceBillId, string? OriginalDocumentSha256,
    long ChargeLineCount, long FieldEvidenceCount, bool HasFromReading, bool HasToReading,
    string State, string Explanation)
{
    public bool TargetHasOriginalDocument { get; init; }
    public long SourceBillsForDocument { get; init; }
    public long TargetBillsForDocument { get; init; }
    public long FromReadingExactCandidates { get; init; }
    public long ToReadingExactCandidates { get; init; }
    public long FromReadingTimestampCandidates { get; init; }
    public long ToReadingTimestampCandidates { get; init; }
}

public sealed record TariffGraphPreview(long SourcePublicationId, string SourceUrl,
    string? OriginalPdfSha256, long SourceTextPages, long RateCandidates,
    long PublicationRelations, string CaptureStatus, string State, string Explanation)
{
    public long IncomingRelations { get; init; }
    public long UnresolvedOutgoingRelations { get; init; }
    public long TargetPdfMatches { get; init; }
    public long ReferencedPublicationUrlsInTarget { get; init; }
}

public sealed record RecoveryRelationAudit(string Status, int SourceSchemaVersion,
    int TargetSchemaVersion, IReadOnlyList<BillGraphPreview> Bills,
    IReadOnlyList<TariffGraphPreview> Tariffs, string SafetyDisclaimer)
{
    public IReadOnlyList<UnlinkedBillDocumentPreview> UnlinkedBillDocuments { get; init; } = [];
    // An overview of observed graph dependencies for a future read-only UI;
    // counts do not confer identity or restoration authorization.
    public RecoveryRelationTotals Totals => new(
        Bills.Count, Tariffs.Count, UnlinkedBillDocuments.Count,
        Bills.Count(b => b.HasFromReading || b.HasToReading),
        Bills.Sum(b => b.TargetBillsForDocument),
        Tariffs.Sum(t => t.IncomingRelations),
        Tariffs.Sum(t => t.PublicationRelations),
        Tariffs.Sum(t => t.UnresolvedOutgoingRelations),
        Tariffs.Sum(t => t.ReferencedPublicationUrlsInTarget));
}
public sealed record RecoveryRelationTotals(int SourceBills, int SourceTariffs,
    int UnlinkedBillDocuments, int BillsWithMeterLinks,
    long TargetBillDocumentLinks, long IncomingTariffLinks, long OutgoingTariffLinks,
    long UnresolvedOutgoingLinks, long ReferencedTariffUrlsInTarget);

public sealed record UnlinkedBillDocumentPreview(long SourceDocumentId,
    string? OriginalDocumentSha256, bool TargetHasOriginalDocument,
    string State, string Explanation);

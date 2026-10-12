using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST-ONLY additive import of UNLINKED bill-document catalog records.
/// Input PDFs have already been SHA-verified and staged independently.
/// It writes a NEW disposable SQLite, never changes/activates any target
/// or copies PDF files to a production location. Bill foreign keys, meter
/// readings, charges, tariffs and linked documents remain unsupported.
/// </summary>
public sealed class IsolatedRecoveryUnlinkedBillDocumentTestService
{
    private const int MaxRows = 2_000;
    private readonly IsolatedRecoveryPlanService _planner = new();
    private readonly IsolatedRecoveryDocumentStageTestService _evidence = new();

    public SyntheticUnlinkedDocumentImport Stage(
        SyntheticRecoveryPlan approvedPlan, SyntheticRecoveryStagedBundle bundle,
        string verifiedArchive, string originalSyntheticTarget,
        CancellationToken cancellationToken = default,
        bool simulateInterruptionAfterFirstInsert = false)
    {
        ArgumentNullException.ThrowIfNull(approvedPlan);
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();
        var original = Path.GetFullPath(originalSyntheticTarget);
        var stagedInput = Path.GetFullPath(bundle.Settings.StagedDatabasePath);
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(original);
        if (IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(stagedInput) != root ||
            !string.Equals(Path.GetDirectoryName(stagedInput), root, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(stagedInput).StartsWith("recovery-additive-staged-", StringComparison.Ordinal) ||
            !File.Exists(stagedInput) || IsLinked(stagedInput) ||
            bundle.RealRestoreAuthorized ||
            bundle.Status != "STAGED_SYNTHETIC_SETTINGS_AND_DOCUMENT_EVIDENCE" ||
            bundle.Settings.Status != "STAGED_SYNTHETIC_ONLY" ||
            bundle.ReadOnlyLinks.RelationalRestoreAuthorized)
            throw new InvalidOperationException("Untrusted synthetic staged bundle.");
        var current = _planner.CreatePlan(verifiedArchive, original);
        if (current.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            current.PlanId != approvedPlan.PlanId ||
            current.SourcePackageSha256 != approvedPlan.SourcePackageSha256 ||
            current.TargetSettingsSha256 != approvedPlan.TargetSettingsSha256 ||
            current.TargetOtherTablesSha256 != approvedPlan.TargetOtherTablesSha256 ||
            !current.Steps.SequenceEqual(approvedPlan.Steps))
            throw new InvalidOperationException("Synthetic recovery plan became stale.");
        if (!_evidence.VerifyAgainstArchive(
                bundle.Evidence, verifiedArchive, stagedInput))
            throw new InvalidDataException("Synthetic document evidence is not archive-bound.");

        // The settings staging step must have left ALL other tables intact.
        // Compare a fresh plan over that staged copy, not a caller-supplied
        // counter alone. app_setting can gain missing keys; all other rows
        // must retain the original fingerprint and supported schema.
        var stagedPlan = _planner.CreatePlan(verifiedArchive, stagedInput);
        if (stagedPlan.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            stagedPlan.TargetOtherTablesSha256 != current.TargetOtherTablesSha256 ||
            stagedPlan.MissingSettings != 0 ||
            stagedPlan.ConflictingSettings != current.ConflictingSettings ||
            stagedPlan.TargetOnlySettings != current.TargetOnlySettings)
            throw new InvalidDataException("Synthetic settings stage is incompatible.");

        // Fail closed on ambiguous byte identities and on any selected PDF
        // not uniquely represented by an unlinked source-document record.
        var bills = bundle.Evidence.Documents.Where(x => x.Category == "Bills").ToArray();
        if (bills.Length == 0 || bills.Length > MaxRows ||
            bills.Select(x => x.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() != bills.Length)
            throw new InvalidOperationException("Select unique Bills document evidence only.");
        var byHash = bills.ToDictionary(x => x.Sha256, StringComparer.OrdinalIgnoreCase);

        var tempSource = Path.Combine(root,
            ".unlinked-source-" + Guid.NewGuid().ToString("N") + ".db");
        var output = Path.Combine(root,
            "recovery-unlinked-documents-staged-" + Guid.NewGuid().ToString("N") + ".db");
        var success = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var archive = ZipFile.OpenRead(verifiedArchive))
            using (var input = (archive.GetEntry("database/energy.db") ??
                       throw new InvalidDataException("Archive has no SQLite snapshot.")).Open())
            using (var file = new FileStream(tempSource, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                input.CopyTo(file);
            using var source = OpenReadOnly(tempSource);
            using var destination = OpenReadOnly(stagedInput);
            if (Schema(source) != SqliteDatabase.CurrentSchemaVersion ||
                Schema(destination) != SqliteDatabase.CurrentSchemaVersion)
                throw new NotSupportedException("Only matching synthetic schema v17 is supported.");
            IsolatedRecoveryTargetHealth.RequireHealthy(destination);

            var originals = ReadDocuments(destination);
            var existingByHash = originals.Values.ToDictionary(x => x.Digest,
                StringComparer.OrdinalIgnoreCase);
            var incoming = ReadDocuments(source);
            var selected = incoming.Values.Where(x => byHash.ContainsKey(x.Digest)).ToArray();
            if (selected.Length != bills.Length)
                throw new InvalidDataException("Selected evidence has missing, linked or ambiguous source identity.");
            foreach (var row in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var linked = source.CreateCommand();
                linked.CommandText =
                    "SELECT COUNT(*) FROM utility_bill WHERE source_document_id=$id;";
                linked.Parameters.AddWithValue("$id", row.Id);
                if (Convert.ToInt64(linked.ExecuteScalar()) != 0)
                    throw new InvalidDataException("Linked bill documents require a relational adapter.");
                if (byHash[row.Digest].Size != row.Length)
                    throw new InvalidDataException("Catalog length disagrees with verified PDF bytes.");
                if (existingByHash.TryGetValue(row.Digest, out var prior) &&
                    !SameMetadata(prior, row))
                    throw new InvalidDataException("Conflicting existing document metadata.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Native backup preserves WAL-committed destination data. Input
            // stays read-only; output path is generated within marked temp.
            using (var copy = OpenWritable(output))
                destination.BackupDatabase(copy);
            var added = 0;
            var identical = 0;
            var mappings = new List<SyntheticDocumentIdMap>(selected.Length);
            using (var writable = OpenWritable(output))
            using (var transaction = writable.BeginTransaction())
            {
                foreach (var row in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (existingByHash.TryGetValue(row.Digest, out var existing))
                    {
                        identical++;
                        mappings.Add(new SyntheticDocumentIdMap(row.Id, existing.Id, row.Digest, false));
                        continue;
                    }
                    using var insert = writable.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO utility_bill_document(
                            provider,original_file_name,local_pdf_path,content_sha256,
                            content_length,page_count,parser_version,extracted_text,imported_utc)
                        VALUES($provider,$name,$path,$sha,$size,$pages,$parser,$text,$date);
                        """;
                    insert.Parameters.AddWithValue("$provider", row.Provider);
                    insert.Parameters.AddWithValue("$name", row.Name);
                    insert.Parameters.AddWithValue("$path", byHash[row.Digest].StageFilePath);
                    insert.Parameters.AddWithValue("$sha", row.Digest);
                    insert.Parameters.AddWithValue("$size", row.Length);
                    insert.Parameters.AddWithValue("$pages", row.Pages);
                    insert.Parameters.AddWithValue("$parser", row.Parser);
                    insert.Parameters.AddWithValue("$text", (object?)row.Text ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$date", row.ImportedUtc);
                    if (insert.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("Unexpected document insert count.");
                    using var key = writable.CreateCommand();
                    key.Transaction = transaction;
                    key.CommandText = "SELECT last_insert_rowid();";
                    var newId = Convert.ToInt64(key.ExecuteScalar());
                    mappings.Add(new SyntheticDocumentIdMap(row.Id, newId, row.Digest, true));
                    added++;
                    if (simulateInterruptionAfterFirstInsert)
                        throw new InvalidOperationException(
                            "SYNTHETIC_UNLINKED_DOCUMENT_INTERRUPTION");
                }
                transaction.Commit();
            }

            using (var check = OpenReadOnly(output))
            {
                IsolatedRecoveryTargetHealth.RequireHealthy(check);
                var after = ReadDocuments(check);
                if (after.Count != originals.Count + added ||
                    !originals.All(p => after.TryGetValue(p.Key, out var same) && same == p.Value))
                    throw new InvalidDataException("Destination-only document evidence changed.");
                foreach (var mapping in mappings)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!after.TryGetValue(mapping.StagedId, out var value) ||
                        value.Digest != mapping.Sha256 ||
                        (mapping.Added && value.Path != byHash[mapping.Sha256].StageFilePath))
                        throw new InvalidDataException("Synthetic document ID remap failed.");
                }
                // No bill was inserted or remapped: no imported document
                // may appear referenced by a newly fabricated bill.
                using var count = check.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM utility_bill;";
                using var previous = destination.CreateCommand();
                previous.CommandText = count.CommandText;
                if (Convert.ToInt64(count.ExecuteScalar()) !=
                    Convert.ToInt64(previous.ExecuteScalar()))
                    throw new InvalidDataException("Bill relationships unexpectedly changed.");
            }
            if (_planner.CreatePlan(verifiedArchive, original).PlanId != approvedPlan.PlanId ||
                !_evidence.VerifyAgainstArchive(bundle.Evidence, verifiedArchive, stagedInput))
                throw new InvalidDataException("Inputs changed during synthetic document staging.");
            success = true;
            return new SyntheticUnlinkedDocumentImport(output, added, identical, mappings,
                "STAGED_UNLINKED_BILL_DOCUMENTS_SYNTHETIC_ONLY", false,
                "Only unlinked document catalog records were inserted into a new test DB. " +
                "Generated PDF paths point to independent synthetic staging evidence. " +
                "No existing rows, bills, FKs, source or live data were changed.");
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (!success && File.Exists(output)) File.Delete(output);
        }
    }

    private static Dictionary<long, BillDocumentRow> ReadDocuments(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT document_id,provider,original_file_name,local_pdf_path,
                   content_sha256,content_length,page_count,parser_version,
                   extracted_text,imported_utc
            FROM utility_bill_document ORDER BY document_id LIMIT 2001;
            """;
        using var rows = cmd.ExecuteReader();
        var result = new Dictionary<long, BillDocumentRow>();
        while (rows.Read())
        {
            if (result.Count >= MaxRows)
                throw new InvalidDataException("Document catalog exceeds synthetic cap.");
            var document = new BillDocumentRow(rows.GetInt64(0),
                rows.GetString(1), rows.GetString(2), rows.GetString(3),
                rows.GetString(4).ToUpperInvariant(), rows.GetInt64(5),
                rows.GetInt64(6), rows.GetString(7),
                rows.IsDBNull(8) ? null : rows.GetString(8), rows.GetString(9));
            if (!result.TryAdd(document.Id, document))
                throw new InvalidDataException("Duplicated local document key.");
        }
        return result;
    }

    private static bool SameMetadata(BillDocumentRow a, BillDocumentRow b) =>
        a.Provider == b.Provider && a.Name == b.Name &&
        a.Length == b.Length && a.Pages == b.Pages &&
        a.Parser == b.Parser && a.Text == b.Text;

    private static SqliteConnection OpenReadOnly(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }
    private static SqliteConnection OpenWritable(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        conn.Open();
        return conn;
    }
    private static int Schema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
    private static bool IsLinked(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private sealed record BillDocumentRow(long Id, string Provider, string Name,
        string Path, string Digest, long Length, long Pages, string Parser,
        string? Text, string ImportedUtc);
}

public sealed record SyntheticDocumentIdMap(long SourceId, long StagedId,
    string Sha256, bool Added);
public sealed record SyntheticUnlinkedDocumentImport(
    string StagedDatabasePath, int Added, int Identical,
    IReadOnlyList<SyntheticDocumentIdMap> IdMap, string Status,
    bool RealRestoreAuthorized, string SafetyDisclaimer);

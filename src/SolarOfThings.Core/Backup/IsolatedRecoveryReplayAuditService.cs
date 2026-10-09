using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST-ONLY independent verification of whether a previously generated,
/// isolated bill/tariff graph is an exact replay candidate. This NEVER
/// executes replay DML or grants permission to restore/activate anything.
/// Unlinked/reused/ambiguous source identities cannot be inferred from IDs.
/// </summary>
public sealed class IsolatedRecoveryReplayAuditService
{
    public SyntheticRecoveryReplayAudit InspectBills(string archivePath,
        SyntheticLinkedBillGraphImport stage, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return Inspect(archivePath, stage.StagedDatabasePath,
            "recovery-linked-bills-staged-", stage.Status,
            "STAGED_LINKED_BILL_GRAPHS_SYNTHETIC_ONLY",
            stage.RealRestoreAuthorized, (source, target) =>
            {
                if (stage.IdMap.Count == 0 || stage.IdMap.Count != stage.AddedBills)
                    throw new InvalidDataException("Bill replay receipt count is unreliable.");
                var seen = new HashSet<long>();
                foreach (var map in stage.IdMap)
                {
                    token.ThrowIfCancellationRequested();
                    if (!seen.Add(map.SourceBillId) ||
                        !IsOriginalPdf(source, target, "utility_bill_document",
                            "document_id", map.SourceDocumentId,
                            map.StagedDocumentId, map.DocumentSha256, "Bills") ||
                        !SameRow(source, target, "utility_bill_document",
                            "document_id", map.SourceDocumentId, map.StagedDocumentId,
                            ["document_id", "local_pdf_path", "content_sha256"]) ||
                        !SameRow(source, target, "utility_bill",
                            "bill_id", map.SourceBillId, map.StagedBillId,
                            ["bill_id", "source_document_id", "from_reading_id", "to_reading_id"]))
                        throw new InvalidDataException("Bill/PDF replay is not an exact graph match.");

                    var bill = Row(source, "utility_bill", "bill_id", map.SourceBillId)
                        ?? throw new InvalidDataException("Source bill disappeared.");
                    var stagedBill = Row(target, "utility_bill", "bill_id", map.StagedBillId)
                        ?? throw new InvalidDataException("Staged bill disappeared.");
                    if (bill["source_document_id"] != map.SourceDocumentId.ToString(CultureInfo.InvariantCulture) ||
                        stagedBill["source_document_id"] != map.StagedDocumentId.ToString(CultureInfo.InvariantCulture))
                        throw new InvalidDataException("Bill document FK was rewritten incorrectly.");

                    foreach (var column in new[] { "from_reading_id", "to_reading_id" })
                    {
                        var oldId = bill[column];
                        var newId = stagedBill[column];
                        if ((oldId is null) != (newId is null))
                            throw new InvalidDataException("Meter FK disappeared on replay.");
                        if (oldId is not null &&
                            (!long.TryParse(oldId, out var oldKey) ||
                             !long.TryParse(newId, out var newKey) ||
                             !SameRow(source, target, "utility_meter_reading",
                                 "reading_id", oldKey, newKey, ["reading_id"])))
                            throw new InvalidDataException("Meter evidence changed on replay.");
                    }
                    if (!SameChildren(source, target, "utility_bill_line",
                            "bill_id", map.SourceBillId, map.StagedBillId,
                            ["bill_line_id", "bill_id"], map.AddedLines) ||
                        !SameChildren(source, target, "utility_bill_field_evidence",
                            "bill_id", map.SourceBillId, map.StagedBillId,
                            ["evidence_id", "bill_id"], map.AddedFieldEvidence))
                        throw new InvalidDataException("Bill charges or field evidence changed on replay.");
                }
                return stage.IdMap.Count;
            }, token);
    }

    public SyntheticRecoveryReplayAudit InspectTariffs(string archivePath,
        SyntheticTariffGraphImport stage, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return Inspect(archivePath, stage.StagedDatabasePath,
            "recovery-tariff-graph-staged-", stage.Status,
            "STAGED_CLOSED_TARIFF_GRAPH_SYNTHETIC_ONLY",
            stage.RealRestoreAuthorized, (source, target) =>
            {
                if (stage.IdMap.Count == 0 || stage.IdMap.Count != stage.AddedPublications)
                    throw new InvalidDataException("Tariff replay receipt count is unreliable.");
                var idMap = stage.IdMap.ToDictionary(x => x.SourceId, x => x.StagedId);
                foreach (var map in stage.IdMap)
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsOriginalPdf(source, target, "tariff_publication",
                            "publication_id", map.SourceId, map.StagedId,
                            map.PdfSha256, "Tariffs") ||
                        !SameRow(source, target, "tariff_publication",
                            "publication_id", map.SourceId, map.StagedId,
                            ["publication_id", "local_pdf_path", "content_sha256"]) ||
                        !SameChildren(source, target, "tariff_publication_page_text",
                            "publication_id", map.SourceId, map.StagedId,
                            ["publication_id"], expectedCount: null) ||
                        !SameChildren(source, target, "tariff_rate_candidate",
                            "publication_id", map.SourceId, map.StagedId,
                            ["publication_id", "rate_candidate_id"], expectedCount: null))
                        throw new InvalidDataException("Tariff document, page or candidate replay differs.");

                    var outgoing = Rows(source, "tariff_publication_relation",
                        "source_publication_id", map.SourceId);
                    var mappedOutgoing = Rows(target, "tariff_publication_relation",
                        "source_publication_id", map.StagedId);
                    if (outgoing.Count != mappedOutgoing.Count)
                        throw new InvalidDataException("Tariff correction graph changed.");
                    foreach (var relation in outgoing)
                    {
                        if (!long.TryParse(relation["target_publication_id"], out var sourceTarget) ||
                            !idMap.TryGetValue(sourceTarget, out var mappedTarget))
                            throw new InvalidDataException("Tariff correction reference escaped selected set.");
                        var expected = relation
                            .Where(x => x.Key != "relation_id" &&
                                        x.Key != "source_publication_id" &&
                                        x.Key != "target_publication_id")
                            .ToDictionary(x => x.Key, x => x.Value);
                        var actual = mappedOutgoing.Where(x =>
                            x["target_publication_id"] == mappedTarget.ToString(CultureInfo.InvariantCulture) &&
                            x.Where(c => c.Key != "relation_id" &&
                                         c.Key != "source_publication_id" &&
                                         c.Key != "target_publication_id")
                             .All(c => expected.TryGetValue(c.Key, out var v) && v == c.Value))
                            .ToArray();
                        if (actual.Length != 1)
                            throw new InvalidDataException("Tariff FK or official correction metadata changed.");
                    }
                }
                return stage.IdMap.Count;
            }, token);
    }

    private static SyntheticRecoveryReplayAudit Inspect(string archivePath,
        string generatedStage, string prefix, string actualStatus, string expectedStatus,
        bool realRestoreFlag, Func<SqliteConnection, SqliteConnection, int> compare,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var output = Path.GetFullPath(generatedStage);
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(output);
        var file = Path.GetFileName(output);
        if (!string.Equals(Path.GetDirectoryName(output), root, StringComparison.OrdinalIgnoreCase) ||
            !file.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(file[prefix.Length..^3], "N", out _) ||
            !file.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(output) || Linked(output) || realRestoreFlag ||
            actualStatus != expectedStatus)
            throw new InvalidOperationException("Untrusted temporary replay stage.");

        var archive = Path.GetFullPath(archivePath);
        if (IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(archive) != root ||
            !File.Exists(archive) || Linked(archive))
            throw new InvalidOperationException("Only same-root synthetic source archives are permitted.");
        var manifest = FullBackupService.VerifyArchive(archive);
        if (manifest.SchemaVersion != SqliteDatabase.CurrentSchemaVersion)
            throw new NotSupportedException("Replay audit cannot adapt historical schemas.");
        var sourceFile = Path.Combine(root,
            ".replay-audit-source-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            token.ThrowIfCancellationRequested();
            using (var zip = ZipFile.OpenRead(archive))
            using (var entry = (zip.GetEntry("database/energy.db") ??
                       throw new InvalidDataException("Missing verified source SQLite.")).Open())
            using (var targetFile = new FileStream(sourceFile, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
                entry.CopyTo(targetFile);
            using var source = Open(sourceFile);
            using var target = Open(output);
            if (Version(source) != SqliteDatabase.CurrentSchemaVersion ||
                Version(target) != SqliteDatabase.CurrentSchemaVersion)
                throw new NotSupportedException("Replay requires identical schema v17.");
            IsolatedRecoveryTargetHealth.RequireHealthy(source);
            IsolatedRecoveryTargetHealth.RequireHealthy(target);
            var groups = compare(source, target);
            token.ThrowIfCancellationRequested();
            return new SyntheticRecoveryReplayAudit(
                "EXACT_SYNTHETIC_REPLAY_PREVIEW_ONLY", groups, false,
                "Verified existing synthetic graph matches archived source evidence; " +
                "this receipt does NOT apply a second import or authorize owner-data restoration.");
        }
        finally
        {
            if (File.Exists(sourceFile)) File.Delete(sourceFile);
        }
    }

    private static bool IsOriginalPdf(SqliteConnection original,
        SqliteConnection staged, string table, string key,
        long sourceId, long stagedId, string digest, string category)
    {
        var left = Row(original, table, key, sourceId);
        var right = Row(staged, table, key, stagedId);
        if (left is null || right is null ||
            !string.Equals(left["content_sha256"], digest,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(right["content_sha256"], digest,
                StringComparison.OrdinalIgnoreCase) ||
            left["content_length"] != right["content_length"])
            return false;
        var path = Path.GetFullPath(right["local_pdf_path"] ?? "");
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(path);
        if (!File.Exists(path) || Linked(path) ||
            !Path.GetRelativePath(root, path).StartsWith(
                "recovery-documents-staged-", StringComparison.Ordinal))
            return false;
        using var pdf = File.OpenRead(path);
        return pdf.Length.ToString(CultureInfo.InvariantCulture) == right["content_length"] &&
            string.Equals(Convert.ToHexString(SHA256.HashData(pdf)), digest,
                StringComparison.OrdinalIgnoreCase) &&
            (category == "Bills" || category == "Tariffs");
    }

    private static bool SameRow(SqliteConnection source, SqliteConnection staged,
        string table, string key, long originalId, long stagedId,
        IReadOnlyCollection<string> ignore)
    {
        var a = Row(source, table, key, originalId);
        var b = Row(staged, table, key, stagedId);
        return a is not null && b is not null &&
            a.Where(x => !ignore.Contains(x.Key))
                .All(x => b.TryGetValue(x.Key, out var value) && value == x.Value);
    }

    private static bool SameChildren(SqliteConnection source, SqliteConnection target,
        string table, string foreignKey, long oldId, long newId,
        IReadOnlyCollection<string> ignore, int? expectedCount)
    {
        var left = Rows(source, table, foreignKey, oldId);
        var right = Rows(target, table, foreignKey, newId);
        if (left.Count != right.Count || (expectedCount.HasValue && left.Count != expectedCount.Value))
            return false;
        static string Key(Dictionary<string, string?> row,
            IReadOnlyCollection<string> skipped) =>
            System.Text.Json.JsonSerializer.Serialize(
                row.Where(x => !skipped.Contains(x.Key))
                   .OrderBy(x => x.Key, StringComparer.Ordinal)
                   .Select(x => new[] { x.Key, x.Value }).ToArray());
        return left.Select(x => Key(x, ignore)).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(right.Select(x => Key(x, ignore))
                .OrderBy(x => x, StringComparer.Ordinal));
    }

    private static Dictionary<string, string?>? Row(SqliteConnection c,
        string table, string key, long value) =>
        Rows(c, table, key, value).SingleOrDefault();

    private static List<Dictionary<string, string?>> Rows(SqliteConnection c,
        string table, string key, long value)
    {
        // All identifiers are constant internal callsite strings, never input.
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM " + table + " WHERE " + key + "=$id LIMIT 2001;";
        cmd.Parameters.AddWithValue("$id", value);
        using var rows = cmd.ExecuteReader();
        var output = new List<Dictionary<string, string?>>();
        while (rows.Read())
        {
            if (output.Count >= 2000)
                throw new InvalidDataException("Bounded replay graph limit exceeded.");
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < rows.FieldCount; i++)
            {
                var name = rows.GetName(i);
                row.Add(name, rows.IsDBNull(i) ? null :
                    Convert.ToString(rows.GetValue(i), CultureInfo.InvariantCulture));
            }
            output.Add(row);
        }
        return output;
    }

    private static SqliteConnection Open(string path)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=ON;";
        pragma.ExecuteNonQuery();
        return c;
    }

    private static int Version(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
    private static bool Linked(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}

public sealed record SyntheticRecoveryReplayAudit(
    string Status, int ExactGraphs, bool RealRestoreAuthorized,
    string SafetyExplanation);

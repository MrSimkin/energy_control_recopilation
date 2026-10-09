using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST ONLY: stage one-to-one PDF-backed bills, their charge lines and field
/// evidence, in a NEW marked temporary SQLite fixture. No production entry
/// point, restore/activation API, source writes or installation path is accepted.
/// Meter-reading FKs remain blocked by default; explicitly reviewed, exact,
/// provenance-bearing synthetic readings can be mapped opt-in. Shared bill
/// PDFs and tariff dependencies remain blocked.
/// </summary>
public sealed class IsolatedRecoveryLinkedBillGraphTestService
{
    private const int MaxBills = 64;
    private const int MaxChildren = 2_000;
    private readonly IsolatedRecoveryPlanService _planner = new();
    private readonly IsolatedRecoveryDocumentStageTestService _documents = new();

    // Reviewed schema-v17 column allowlists: no caller-supplied SQL identifiers.
    private static readonly string[] DocumentColumns =
        ["document_id","provider","original_file_name","local_pdf_path",
         "content_sha256","content_length","page_count","parser_version",
         "extracted_text","imported_utc"];
    private static readonly string[] ReadingColumns =
        ["reading_id","reading_at_utc","reading_kwh","reference","notes",
         "created_utc","updated_utc","source_kind","time_precision","time_assumption"];
    private static readonly string[] BillColumns =
        ["bill_id","period_start_utc","period_end_utc","billed_consumption_kwh",
         "amount_clp","invoice_reference","notes","created_utc","updated_utc",
         "from_reading_id","to_reading_id","meter_start_kwh","meter_end_kwh",
         "tariff_plan","taxable_amount_clp","iva_clp","exempt_amount_clp",
         "gross_bill_amount_clp","other_charges_clp","total_due_clp",
         "period_precision","source_kind","source_document_id","review_state",
         "iva_rate","previous_balance_clp"];
    private static readonly string[] LineColumns =
        ["bill_line_id","bill_id","section_key","category_key","description",
         "quantity","unit","unit_rate_clp","amount_clp","tax_treatment",
         "sort_order","created_utc","updated_utc","source_kind",
         "evidence_state","source_page","source_text"];
    private static readonly string[] EvidenceColumns =
        ["evidence_id","bill_id","field_key","source_kind","evidence_state",
         "printed_value_text","normalized_value_text","source_page",
         "source_text","created_utc","updated_utc"];

    public SyntheticLinkedBillGraphImport Stage(
        SyntheticRecoveryPlan approvedPlan, SyntheticRecoveryStagedBundle bundle,
        string verifiedArchive, string originalSyntheticTarget,
        CancellationToken cancellationToken = default,
        bool simulateInterruptionAfterFirstBill = false,
        bool allowExactReadingRemap = false)
    {
        ArgumentNullException.ThrowIfNull(approvedPlan);
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();
        var original = Path.GetFullPath(originalSyntheticTarget);
        var input = Path.GetFullPath(bundle.Settings.StagedDatabasePath);
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(original);
        if (IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(input) != root ||
            !string.Equals(Path.GetDirectoryName(input), root, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(input).StartsWith("recovery-additive-staged-", StringComparison.Ordinal) ||
            !File.Exists(input) || Linked(input) || Linked(original) ||
            bundle.RealRestoreAuthorized || bundle.ReadOnlyLinks.RelationalRestoreAuthorized ||
            bundle.Status != "STAGED_SYNTHETIC_SETTINGS_AND_DOCUMENT_EVIDENCE" ||
            bundle.Settings.Status != "STAGED_SYNTHETIC_ONLY")
            throw new InvalidOperationException("Untrusted synthetic recovery bundle.");

        var current = _planner.CreatePlan(verifiedArchive, original);
        if (current.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            current.PlanId != approvedPlan.PlanId ||
            current.SourcePackageSha256 != approvedPlan.SourcePackageSha256 ||
            current.TargetSettingsSha256 != approvedPlan.TargetSettingsSha256 ||
            current.TargetOtherTablesSha256 != approvedPlan.TargetOtherTablesSha256 ||
            !current.Steps.SequenceEqual(approvedPlan.Steps))
            throw new InvalidOperationException("Relational plan is stale or unreviewed.");
        if (!_documents.VerifyAgainstArchive(bundle.Evidence, verifiedArchive, input))
            throw new InvalidDataException("PDF bytes are not bound to the original verified ZIP.");

        var inputPlan = _planner.CreatePlan(verifiedArchive, input);
        if (inputPlan.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            inputPlan.TargetOtherTablesSha256 != current.TargetOtherTablesSha256 ||
            inputPlan.MissingSettings != 0 ||
            inputPlan.ConflictingSettings != current.ConflictingSettings ||
            inputPlan.TargetOnlySettings != current.TargetOnlySettings)
            throw new InvalidDataException("Settings stage changed a relational input.");

        var selected = bundle.Evidence.Documents.Where(x => x.Category == "Bills").ToArray();
        if (selected.Length == 0 || selected.Length > MaxBills ||
            selected.Select(x => x.Sha256)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
            throw new InvalidOperationException("Select 1-64 unique bill-PDF hashes.");
        var sourceCopy = Path.Combine(root, ".linked-bill-source-" + Guid.NewGuid().ToString("N") + ".db");
        var output = Path.Combine(root, "recovery-linked-bills-staged-" + Guid.NewGuid().ToString("N") + ".db");
        var success = false;
        try
        {
            using (var zip = ZipFile.OpenRead(verifiedArchive))
            using (var stream = (zip.GetEntry("database/energy.db") ??
                        throw new InvalidDataException("Missing verified SQLite snapshot.")).Open())
            using (var file = new FileStream(sourceCopy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.CopyTo(file);
            using var source = Open(sourceCopy, writable: false);
            using var destination = Open(input, writable: false);
            if (Version(source) != SqliteDatabase.CurrentSchemaVersion ||
                Version(destination) != SqliteDatabase.CurrentSchemaVersion)
                throw new NotSupportedException("Only identical synthetic v17 schemas are reviewed.");
            IsolatedRecoveryTargetHealth.RequireHealthy(source);
            IsolatedRecoveryTargetHealth.RequireHealthy(destination);
            foreach (var (table, columns) in new (string, string[])[]
            {
                ("utility_bill_document", DocumentColumns),
                ("utility_bill", BillColumns),
                ("utility_bill_line", LineColumns),
                ("utility_bill_field_evidence", EvidenceColumns),
                ("utility_meter_reading", ReadingColumns)
            })
            {
                CheckColumns(source, table, columns);
                CheckColumns(destination, table, columns);
            }

            var graphs = new List<BillGraph>(selected.Length);
            var uniquePeriods = new HashSet<string>(StringComparer.Ordinal);
            var readingPlans = new Dictionary<long, ReadingPlan>();
            foreach (var evidence in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var docs = Read(source, "utility_bill_document", "content_sha256=$v COLLATE NOCASE",
                    evidence.Sha256, 2);
                if (docs.Count != 1 ||
                    Convert.ToInt64(docs[0]["content_length"]) != evidence.Size)
                    throw new InvalidDataException("Source PDF identity or verified byte length is ambiguous.");
                if (Read(destination, "utility_bill_document", "content_sha256=$v COLLATE NOCASE",
                        evidence.Sha256, 2).Count != 0)
                    throw new InvalidDataException("Target already has these PDF bytes: bill conflict requires review.");
                var originalDocumentId = Convert.ToInt64(docs[0]["document_id"]);
                var bills = Read(source, "utility_bill", "source_document_id=$v",
                    originalDocumentId, 2);
                if (bills.Count != 1)
                    throw new InvalidDataException("PDF must identify exactly one source bill; shared or unlinked documents blocked.");
                var bill = bills[0];
                foreach (var key in new[] { "from_reading_id", "to_reading_id" })
                {
                    if (bill[key] is null) continue;
                    if (!allowExactReadingRemap)
                        throw new InvalidDataException(
                            "Meter-reading FK requires explicit isolated exact-evidence mode.");
                    var id = Convert.ToInt64(bill[key]);
                    if (!readingPlans.ContainsKey(id))
                        readingPlans.Add(id, ReviewReading(source, destination, id));
                }
                // The schema cannot prove that two differently scanned PDFs
                // with the same billing period represent separate accounts.
                // Block instead of silently duplicating an invoice.
                var start = Convert.ToString(bill["period_start_utc"])!;
                var end = Convert.ToString(bill["period_end_utc"])!;
                if (!uniquePeriods.Add(start + "|" + end) ||
                    HasPeriod(destination, start, end))
                    throw new InvalidDataException(
                        "Matching billing period requires explicit identity review.");
                var oldBillId = Convert.ToInt64(bill["bill_id"]);
                var lines = Read(source, "utility_bill_line", "bill_id=$v", oldBillId, MaxChildren);
                var fields = Read(source, "utility_bill_field_evidence", "bill_id=$v", oldBillId, MaxChildren);
                graphs.Add(new BillGraph(evidence, docs[0], bill, lines, fields,
                    originalDocumentId, oldBillId));
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (var copy = Open(output, writable: true))
                destination.BackupDatabase(copy);
            var remaps = new List<SyntheticLinkedBillIdMap>();
            var totalLines = 0;
            var totalFields = 0;
            var addedReadings = 0;
            var reusedReadings = 0;
            var readingMaps = new Dictionary<long, long>();
            using (var write = Open(output, writable: true))
            using (var tx = write.BeginTransaction())
            {
                // Dependencies are inserted only into the generated fixture,
                // in the same transaction as every bill and its evidence.
                foreach (var (sourceId, plan) in readingPlans.OrderBy(x => x.Key))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var stagedId = plan.IdenticalTargetId ?? Insert(write, tx,
                        "utility_meter_reading", ReadingColumns, "reading_id",
                        plan.Source, new Dictionary<string, object?>());
                    if (plan.IdenticalTargetId.HasValue) reusedReadings++;
                    else addedReadings++;
                    readingMaps.Add(sourceId, stagedId);
                }
                foreach (var graph in graphs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var newDocument = Insert(write, tx, "utility_bill_document",
                        DocumentColumns, "document_id", graph.Document,
                        new Dictionary<string, object?> { ["local_pdf_path"] = graph.Pdf.StageFilePath });
                    var newBill = Insert(write, tx, "utility_bill", BillColumns, "bill_id",
                        graph.Bill, new Dictionary<string, object?>
                        {
                            ["source_document_id"] = newDocument,
                            ["from_reading_id"] = graph.Bill["from_reading_id"] is null
                                ? null : readingMaps[Convert.ToInt64(graph.Bill["from_reading_id"])],
                            ["to_reading_id"] = graph.Bill["to_reading_id"] is null
                                ? null : readingMaps[Convert.ToInt64(graph.Bill["to_reading_id"])]
                        });
                    foreach (var row in graph.Lines)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Insert(write, tx, "utility_bill_line", LineColumns, "bill_line_id", row,
                            new Dictionary<string, object?> { ["bill_id"] = newBill });
                        totalLines++;
                    }
                    foreach (var row in graph.Fields)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Insert(write, tx, "utility_bill_field_evidence", EvidenceColumns,
                            "evidence_id", row, new Dictionary<string, object?> { ["bill_id"] = newBill });
                        totalFields++;
                    }
                    remaps.Add(new SyntheticLinkedBillIdMap(graph.SourceDocumentId,
                        newDocument, graph.SourceBillId, newBill, graph.Pdf.Sha256,
                        graph.Lines.Count, graph.Fields.Count));
                    if (simulateInterruptionAfterFirstBill)
                        throw new InvalidOperationException("SYNTHETIC_LINKED_BILL_INTERRUPTION");
                }
                cancellationToken.ThrowIfCancellationRequested();
                tx.Commit();
            }

            using (var check = Open(output, writable: false))
            {
                IsolatedRecoveryTargetHealth.RequireHealthy(check);
                foreach (var map in remaps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var actual = Read(check, "utility_bill", "bill_id=$v", map.StagedBillId, 2);
                    var doc = Read(check, "utility_bill_document", "document_id=$v",
                        map.StagedDocumentId, 2);
                    if (actual.Count != 1 || doc.Count != 1 ||
                        Convert.ToInt64(actual[0]["source_document_id"]) != map.StagedDocumentId ||
                        !string.Equals(Convert.ToString(doc[0]["content_sha256"]),
                            map.DocumentSha256, StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(Convert.ToString(doc[0]["local_pdf_path"])) ||
                        Read(check, "utility_bill_line", "bill_id=$v",
                            map.StagedBillId, MaxChildren).Count != map.AddedLines ||
                        Read(check, "utility_bill_field_evidence", "bill_id=$v",
                            map.StagedBillId, MaxChildren).Count != map.AddedFieldEvidence)
                        throw new InvalidDataException("Staged bill graph/FK remapping verification failed.");
                }
                foreach (var (sourceId, stagedId) in readingMaps)
                {
                    var before = readingPlans[sourceId].Source;
                    var staged = Read(check, "utility_meter_reading",
                        "reading_id=$v", stagedId, 2);
                    if (staged.Count != 1 ||
                        !SameReading(before, staged[0]))
                        throw new InvalidDataException("Staged reading is not the verified source reading.");
                }
                foreach (var map in remaps)
                {
                    var actual = Read(check, "utility_bill", "bill_id=$v", map.StagedBillId, 2).Single();
                    var sourceBill = graphs.Single(x => x.SourceBillId == map.SourceBillId).Bill;
                    foreach (var field in new[] { "from_reading_id", "to_reading_id" })
                    {
                        var originalKey = sourceBill[field];
                        var expectedKey = originalKey is null
                            ? (long?)null : readingMaps[Convert.ToInt64(originalKey)];
                        var actualKey = actual[field] is null
                            ? (long?)null : Convert.ToInt64(actual[field]);
                        if (actualKey != expectedKey)
                            throw new InvalidDataException("Bill-to-meter dependency remap failed.");
                    }
                }
                foreach (var (table, key, delta) in new[]
                {
                    ("utility_bill_document", "document_id", remaps.Count),
                    ("utility_bill", "bill_id", remaps.Count),
                    ("utility_bill_line", "bill_line_id", totalLines),
                    ("utility_bill_field_evidence", "evidence_id", totalFields),
                    ("utility_meter_reading", "reading_id", addedReadings)
                })
                {
                    if (Count(check, table) != Count(destination, table) + delta)
                        throw new InvalidDataException("Stage changed unexpected relational row counts.");
                    RequireOriginalRowsUnchanged(destination, check, table, key, cancellationToken);
                }
            }
            if (_planner.CreatePlan(verifiedArchive, original).PlanId != approvedPlan.PlanId ||
                !_documents.VerifyAgainstArchive(bundle.Evidence, verifiedArchive, input))
                throw new InvalidDataException("Original recovery inputs changed during graph staging.");
            success = true;
            return new SyntheticLinkedBillGraphImport(output, remaps.Count,
                totalLines, totalFields, remaps,
                "STAGED_LINKED_BILL_GRAPHS_SYNTHETIC_ONLY", false,
                "Only one-to-one PDF bills and their charge/evidence children are remapped; " +
                "exact provenance-bearing meter readings only when explicitly opted in, " +
                "in a disposable generated SQLite. No live records, tariff graph, " +
                "active installation or user-provided database was modified.")
            {
                AddedMeterReadings = addedReadings,
                ReusedMeterReadings = reusedReadings,
                ReadingIdMap = readingMaps
                    .Select(x => new SyntheticReadingIdMap(x.Key, x.Value,
                        !readingPlans[x.Key].IdenticalTargetId.HasValue)).ToArray(),
                SourcePackageSha256 = approvedPlan.SourcePackageSha256
            };
        }
        finally
        {
            if (File.Exists(sourceCopy)) File.Delete(sourceCopy);
            if (!success && File.Exists(output)) File.Delete(output);
        }
    }

    // Reject DATE_ONLY / assumed-midnight times, unspecified sources, absent
    // source references and every multi-candidate identity. A numerical
    // meter value or timestamp alone NEVER authorizes a portable FK.
    private static ReadingPlan ReviewReading(SqliteConnection source,
        SqliteConnection target, long id)
    {
        var rows = Read(source, "utility_meter_reading", "reading_id=$v", id, 2);
        if (rows.Count != 1)
            throw new InvalidDataException("Missing or ambiguous original meter reading.");
        var row = rows[0];
        var kind = Convert.ToString(row["source_kind"]);
        var utc = Convert.ToString(row["reading_at_utc"]);
        var reference = Convert.ToString(row["reference"]);
        if ((kind != "PERSONAL" && kind != "UTILITY_OFFICIAL") ||
            Convert.ToString(row["time_precision"]) != "EXACT" ||
            Convert.ToString(row["time_assumption"]) != "EXACT" ||
            string.IsNullOrWhiteSpace(utc) || string.IsNullOrWhiteSpace(reference) ||
            !double.IsFinite(Convert.ToDouble(row["reading_kwh"])))
            throw new InvalidDataException(
                "Reading lacks exact, finite and explicitly named source provenance.");
        var sourceAt = ReadReadingsAt(source, kind!, utc!);
        if (sourceAt.Count != 1 || !SameReading(sourceAt[0], row))
            throw new InvalidDataException("Source meter source/time has ambiguous provenance.");
        var targetAt = ReadReadingsAt(target, kind!, utc!);
        if (targetAt.Count > 1 || (targetAt.Count == 1 &&
            !SameReading(targetAt[0], row)))
            throw new InvalidDataException("Target meter source/time conflicts with original reading.");
        return new ReadingPlan(row, targetAt.Count == 1
            ? Convert.ToInt64(targetAt[0]["reading_id"]) : null);
    }

    private static List<Dictionary<string, object?>> ReadReadingsAt(
        SqliteConnection c, string kind, string utc)
    {
        using var command = c.CreateCommand();
        command.CommandText = """
            SELECT * FROM utility_meter_reading
            WHERE source_kind=$kind AND reading_at_utc=$utc LIMIT 3;
            """;
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$utc", utc);
        using var rows = command.ExecuteReader();
        var result = new List<Dictionary<string, object?>>();
        while (rows.Read())
        {
            if (result.Count >= 2)
                throw new InvalidDataException("Repeated meter provenance/time candidates.");
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < rows.FieldCount; i++)
                row.Add(rows.GetName(i), rows.IsDBNull(i) ? null : rows.GetValue(i));
            result.Add(row);
        }
        return result;
    }

    private static bool SameReading(IReadOnlyDictionary<string, object?> a,
        IReadOnlyDictionary<string, object?> b) =>
        ReadingColumns.Where(x => x != "reading_id")
            .All(x => Equals(a[x], b[x]));

    private static void CheckColumns(SqliteConnection c, string table, string[] expected)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(" + table + ");";
        var found = new List<string>();
        using var rows = cmd.ExecuteReader();
        while (rows.Read()) found.Add(rows.GetString(1));
        if (!found.SequenceEqual(expected))
            throw new NotSupportedException("Unreviewed relational table schema: " + table);
        // No fixture-defined DML triggers are permitted to mutate any other
        // table when inserting the four reviewed relational entity types.
        using var triggers = c.CreateCommand();
        triggers.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type='trigger' AND tbl_name=$table;";
        triggers.Parameters.AddWithValue("$table", table);
        if (Convert.ToInt64(triggers.ExecuteScalar()) != 0)
            throw new NotSupportedException("Unreviewed recovery triggers: " + table);
    }

    private static bool HasPeriod(SqliteConnection c, string start, string end)
    {
        using var command = c.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(SELECT 1 FROM utility_bill
            WHERE period_start_utc=$start AND period_end_utc=$end);
            """;
        command.Parameters.AddWithValue("$start", start);
        command.Parameters.AddWithValue("$end", end);
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }

    // The reviewed tables have AUTOINCREMENT primary keys, so inserted rows
    // appear after original keys. Compare all preexisting row values, not
    // only their totals, and stop on the first unexpected alteration.
    private static void RequireOriginalRowsUnchanged(SqliteConnection original,
        SqliteConnection staged, string table, string key, CancellationToken token)
    {
        using var oldQuery = original.CreateCommand();
        using var newQuery = staged.CreateCommand();
        oldQuery.CommandText = "SELECT * FROM " + table + " ORDER BY " + key + ";";
        newQuery.CommandText = oldQuery.CommandText;
        using var before = oldQuery.ExecuteReader();
        using var after = newQuery.ExecuteReader();
        var rowNumber = 0;
        while (before.Read())
        {
            if (++rowNumber % 256 == 0) token.ThrowIfCancellationRequested();
            if (!after.Read() || after.FieldCount != before.FieldCount)
                throw new InvalidDataException("Original relational row disappeared.");
            for (var i = 0; i < before.FieldCount; i++)
            {
                if (before.IsDBNull(i) && after.IsDBNull(i)) continue;
                if (before.IsDBNull(i) != after.IsDBNull(i) ||
                    !Equals(before.GetValue(i), after.GetValue(i)))
                    throw new InvalidDataException(
                        "Original relational row was modified: " + table);
            }
        }
    }

    private static List<Dictionary<string, object?>> Read(
        SqliteConnection c, string table, string where, object value, int max)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM " + table + " WHERE " + where + " LIMIT $limit;";
        cmd.Parameters.AddWithValue("$v", value);
        cmd.Parameters.AddWithValue("$limit", max + 1);
        using var rows = cmd.ExecuteReader();
        var result = new List<Dictionary<string, object?>>();
        while (rows.Read())
        {
            if (result.Count >= max)
                throw new InvalidDataException("Relational row cap exceeded: " + table);
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < rows.FieldCount; i++)
                row.Add(rows.GetName(i), rows.IsDBNull(i) ? null : rows.GetValue(i));
            result.Add(row);
        }
        return result;
    }

    private static long Insert(SqliteConnection c, SqliteTransaction tx,
        string table, string[] columns, string identity,
        IReadOnlyDictionary<string, object?> source,
        IReadOnlyDictionary<string, object?> changes)
    {
        var fields = columns.Where(x => x != identity).ToArray();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO " + table + "(" +
            string.Join(",", fields) + ") VALUES(" +
            string.Join(",", fields.Select((_, i) => "$p" + i)) + ");";
        for (var i = 0; i < fields.Length; i++)
        {
            var col = fields[i];
            cmd.Parameters.AddWithValue("$p" + i,
                (changes.TryGetValue(col, out var val) ? val : source[col]) ?? DBNull.Value);
        }
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Relational insert failed: " + table);
        using var key = c.CreateCommand();
        key.Transaction = tx;
        key.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(key.ExecuteScalar());
    }

    private static long Count(SqliteConnection c, string table)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + table + ";";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
    private static int Version(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
    private static SqliteConnection Open(string path, bool writable)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = writable ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly,
            Pooling = false, ForeignKeys = true
        }.ToString());
        c.Open();
        if (!writable)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA query_only=ON;";
            cmd.ExecuteNonQuery();
        }
        return c;
    }
    private static bool Linked(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private sealed record ReadingPlan(Dictionary<string, object?> Source, long? IdenticalTargetId);

    private sealed record BillGraph(SyntheticStagedDocument Pdf,
        Dictionary<string, object?> Document, Dictionary<string, object?> Bill,
        List<Dictionary<string, object?>> Lines, List<Dictionary<string, object?>> Fields,
        long SourceDocumentId, long SourceBillId);
}

public sealed record SyntheticLinkedBillIdMap(long SourceDocumentId,
    long StagedDocumentId, long SourceBillId, long StagedBillId,
    string DocumentSha256, int AddedLines, int AddedFieldEvidence);

public sealed record SyntheticReadingIdMap(long SourceId, long StagedId, bool Added);

public sealed record SyntheticLinkedBillGraphImport(
    string StagedDatabasePath, int AddedBills, int AddedLines,
    int AddedFieldEvidence, IReadOnlyList<SyntheticLinkedBillIdMap> IdMap,
    string Status, bool RealRestoreAuthorized, string SafetyDisclaimer)
{
    public int AddedMeterReadings { get; init; }
    public int ReusedMeterReadings { get; init; }
    public IReadOnlyList<SyntheticReadingIdMap> ReadingIdMap { get; init; } = [];
    public string SourcePackageSha256 { get; init; } = "";
}

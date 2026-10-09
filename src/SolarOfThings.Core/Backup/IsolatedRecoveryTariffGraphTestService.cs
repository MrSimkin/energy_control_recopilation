using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST ONLY. Copies a CLOSED, uniquely identifiable synthetic tariff-document
/// graph into a newly generated SQLite fixture, never to owner Data or any
/// existing database. An unselected or unresolved correction relation blocks
/// the whole operation; no partial graph is silently reconstructed.
/// </summary>
public sealed class IsolatedRecoveryTariffGraphTestService
{
    private const int MaxPublications = 32;
    private const int MaxChildren = 2_000;
    private readonly IsolatedRecoveryPlanService _planner = new();
    private readonly IsolatedRecoveryDocumentStageTestService _stages = new();

    private static readonly string[] PublicationColumns =
        ["publication_id","provider","category","title","source_url",
         "effective_from","is_retroactive","local_pdf_path","content_sha256",
         "content_length","page_count","capture_status","captured_utc",
         "updated_utc","normalization_status","normalization_parser_version",
         "normalized_utc","official_document_number","official_publication_date",
         "corrects_official_document_number","regulatory_metadata_source"];
    private static readonly string[] PageColumns =
        ["publication_id","page_number","page_text"];
    private static readonly string[] RateColumns =
        ["rate_candidate_id","publication_id","page_number","tariff_plan",
         "component_key","printed_description","unit","network_type","etr_band",
         "candidate_index","net_rate_clp","published_iva_column_clp",
         "source_text","parser_version","validation_state","created_utc"];
    private static readonly string[] RelationColumns =
        ["relation_id","source_publication_id","relation_type",
         "target_provider","target_category","target_official_document_number",
         "target_publication_id","evidence_source_url","evidence_text",
         "created_utc","updated_utc"];

    public SyntheticTariffGraphImport Stage(
        SyntheticRecoveryPlan approvedPlan, SyntheticRecoveryStagedBundle bundle,
        string verifiedArchive, string originalSyntheticTarget,
        CancellationToken cancellationToken = default,
        bool simulateInterruptionAfterFirstPublication = false)
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
            bundle.Status != "STAGED_SYNTHETIC_SETTINGS_AND_DOCUMENT_EVIDENCE" ||
            bundle.Settings.Status != "STAGED_SYNTHETIC_ONLY" ||
            bundle.RealRestoreAuthorized || bundle.ReadOnlyLinks.RelationalRestoreAuthorized)
            throw new InvalidOperationException("Not a reviewed isolated synthetic tariff bundle.");

        var plan = _planner.CreatePlan(verifiedArchive, original);
        if (plan.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            plan.PlanId != approvedPlan.PlanId ||
            plan.SourcePackageSha256 != approvedPlan.SourcePackageSha256 ||
            plan.TargetSettingsSha256 != approvedPlan.TargetSettingsSha256 ||
            plan.TargetOtherTablesSha256 != approvedPlan.TargetOtherTablesSha256 ||
            !plan.Steps.SequenceEqual(approvedPlan.Steps))
            throw new InvalidOperationException("Synthetic tariff plan became stale.");
        if (!_stages.VerifyAgainstArchive(bundle.Evidence, verifiedArchive, input))
            throw new InvalidDataException("Tariff PDF evidence is not ZIP-bound.");
        var staged = _planner.CreatePlan(verifiedArchive, input);
        if (staged.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            staged.TargetOtherTablesSha256 != plan.TargetOtherTablesSha256 ||
            staged.MissingSettings != 0 ||
            staged.ConflictingSettings != plan.ConflictingSettings ||
            staged.TargetOnlySettings != plan.TargetOnlySettings)
            throw new InvalidDataException("Settings staging modified unexpected tariff input.");

        var chosen = bundle.Evidence.Documents.Where(x => x.Category == "Tariffs").ToArray();
        if (chosen.Length is < 1 or > MaxPublications ||
            chosen.Select(x => x.Sha256)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != chosen.Length)
            throw new InvalidOperationException("Select 1-32 unique tariff PDFs.");
        var sourceCopy = Path.Combine(root, ".tariff-graph-source-" + Guid.NewGuid().ToString("N") + ".db");
        var output = Path.Combine(root, "recovery-tariff-graph-staged-" + Guid.NewGuid().ToString("N") + ".db");
        var success = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var zip = ZipFile.OpenRead(verifiedArchive))
            using (var bytes = (zip.GetEntry("database/energy.db") ??
                         throw new InvalidDataException("Missing verified source snapshot.")).Open())
            using (var file = new FileStream(sourceCopy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                bytes.CopyTo(file);
            using var source = Open(sourceCopy, writable: false);
            using var destination = Open(input, writable: false);
            if (Version(source) != SqliteDatabase.CurrentSchemaVersion ||
                Version(destination) != SqliteDatabase.CurrentSchemaVersion)
                throw new NotSupportedException("Only reviewed synthetic SQLite v17 graphs are supported.");
            IsolatedRecoveryTargetHealth.RequireHealthy(source);
            IsolatedRecoveryTargetHealth.RequireHealthy(destination);
            foreach (var (table, cols) in new (string, string[])[]
            {
                ("tariff_publication", PublicationColumns),
                ("tariff_publication_page_text", PageColumns),
                ("tariff_rate_candidate", RateColumns),
                ("tariff_publication_relation", RelationColumns)
            })
            {
                CheckColumns(source, table, cols);
                CheckColumns(destination, table, cols);
            }

            var selected = new List<TariffGraph>();
            var ids = new HashSet<long>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pdf in chosen)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var found = Query(source, """
                    SELECT * FROM tariff_publication
                    WHERE content_sha256=$sha COLLATE NOCASE LIMIT 3;
                    """, 2, ("$sha", pdf.Sha256));
                if (found.Count != 1 ||
                    found[0]["local_pdf_path"] is null ||
                    Convert.ToInt64(found[0]["content_length"]) != pdf.Size)
                    throw new InvalidDataException("Tariff source PDF hash/path/length not unambiguous.");
                var row = found[0];
                var id = Convert.ToInt64(row["publication_id"]);
                var provider = Convert.ToString(row["provider"])!;
                var category = Convert.ToString(row["category"])!;
                var number = Convert.ToString(row["official_document_number"]);
                var url = Convert.ToString(row["source_url"])!;
                if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(category) ||
                    string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(url) ||
                    !ids.Add(id) || !identities.Add(provider + "|" + category + "|" + number))
                    throw new InvalidDataException("No unique official tariff publication identity.");
                var originalMatches = Query(source, """
                    SELECT * FROM tariff_publication WHERE
                    provider=$provider AND category=$category
                    AND official_document_number=$number LIMIT 3;
                    """, 2, ("$provider", provider), ("$category", category), ("$number", number));
                if (originalMatches.Count != 1)
                    throw new InvalidDataException("Source official tariff identity is not unique.");
                var competing = Query(destination, """
                    SELECT * FROM tariff_publication WHERE source_url=$url
                    OR (provider=$provider AND category=$category
                        AND official_document_number=$number)
                    OR content_sha256=$sha COLLATE NOCASE LIMIT 3;
                    """, 2, ("$url", url), ("$provider", provider),
                    ("$category", category), ("$number", number),
                    ("$sha", pdf.Sha256));
                if (competing.Count != 0)
                    throw new InvalidDataException("Target tariff overlap requires conflict review.");
                var pages = Query(source, """
                    SELECT * FROM tariff_publication_page_text
                    WHERE publication_id=$id ORDER BY page_number LIMIT $limit;
                    """, MaxChildren, ("$id", id));
                var rates = Query(source, """
                    SELECT * FROM tariff_rate_candidate
                    WHERE publication_id=$id ORDER BY rate_candidate_id LIMIT $limit;
                    """, MaxChildren, ("$id", id));
                selected.Add(new TariffGraph(pdf, row, pages, rates, id));
            }
            // Every incoming AND outgoing relation must have both endpoints
            // in the selected set, with a resolved, matching official number.
            var relations = new Dictionary<long, Dictionary<string, object?>>();
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var incident = Query(source, """
                    SELECT * FROM tariff_publication_relation
                    WHERE source_publication_id=$id OR target_publication_id=$id
                    ORDER BY relation_id LIMIT $limit;
                    """, MaxChildren, ("$id", id));
                foreach (var relation in incident)
                {
                    var from = Convert.ToInt64(relation["source_publication_id"]);
                    var targetId = relation["target_publication_id"];
                    if (targetId is null || !ids.Contains(from) ||
                        !ids.Contains(Convert.ToInt64(targetId)))
                        throw new InvalidDataException(
                            "Tariff relation escapes selected complete graph or has unresolved target.");
                    var target = selected.Single(x => x.SourceId == Convert.ToInt64(targetId)).Publication;
                    if (Convert.ToString(relation["target_provider"]) !=
                            Convert.ToString(target["provider"]) ||
                        Convert.ToString(relation["target_category"]) !=
                            Convert.ToString(target["category"]) ||
                        Convert.ToString(relation["target_official_document_number"]) !=
                            Convert.ToString(target["official_document_number"]))
                        throw new InvalidDataException(
                            "Official relation target differs from its FK identity.");
                    relations[Convert.ToInt64(relation["relation_id"])] = relation;
                }
            }
            // Some official correction references are stored as metadata
            // or as a pending relation to an official number with a NULL FK.
            // Neither form may disappear from a supposedly complete graph.
            foreach (var graph in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = graph.Publication;
                var provider = Convert.ToString(row["provider"])!;
                var category = Convert.ToString(row["category"])!;
                var number = Convert.ToString(row["official_document_number"])!;
                var correcting = Convert.ToString(row["corrects_official_document_number"]);
                if (!string.IsNullOrWhiteSpace(correcting) &&
                    !selected.Any(x =>
                        Convert.ToString(x.Publication["provider"]) == provider &&
                        Convert.ToString(x.Publication["category"]) == category &&
                        Convert.ToString(x.Publication["official_document_number"]) == correcting))
                    throw new InvalidDataException(
                        "Tariff correction metadata points outside selected complete graph.");
                var referenced = Query(source, """
                    SELECT * FROM tariff_publication_relation
                    WHERE target_provider=$provider AND target_category=$category
                      AND target_official_document_number=$number
                    LIMIT $limit;
                    """, MaxChildren, ("$provider", provider),
                    ("$category", category), ("$number", number));
                foreach (var relation in referenced)
                {
                    var from = Convert.ToInt64(relation["source_publication_id"]);
                    var target = relation["target_publication_id"];
                    if (target is null || Convert.ToInt64(target) != graph.SourceId ||
                        !ids.Contains(from) ||
                        !relations.ContainsKey(Convert.ToInt64(relation["relation_id"])))
                        throw new InvalidDataException(
                            "Unresolved or unselected incoming official correction link.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (var copy = Open(output, writable: true))
                destination.BackupDatabase(copy);
            var idMap = new Dictionary<long, long>();
            var pageCount = 0;
            var rateCount = 0;
            var relationCount = 0;
            using (var write = Open(output, writable: true))
            using (var tx = write.BeginTransaction())
            {
                foreach (var graph in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = Insert(write, tx, "tariff_publication", PublicationColumns,
                        "publication_id", graph.Publication,
                        new Dictionary<string, object?>
                        { ["local_pdf_path"] = graph.Pdf.StageFilePath });
                    idMap.Add(graph.SourceId, id);
                    if (simulateInterruptionAfterFirstPublication)
                        throw new InvalidOperationException("SYNTHETIC_TARIFF_GRAPH_INTERRUPTION");
                }
                foreach (var graph in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = idMap[graph.SourceId];
                    foreach (var page in graph.Pages)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Insert(write, tx, "tariff_publication_page_text", PageColumns,
                            "", page, new Dictionary<string, object?>
                            { ["publication_id"] = id });
                        pageCount++;
                    }
                    foreach (var rate in graph.Rates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Insert(write, tx, "tariff_rate_candidate", RateColumns,
                            "rate_candidate_id", rate, new Dictionary<string, object?>
                            { ["publication_id"] = id });
                        rateCount++;
                    }
                }
                foreach (var relation in relations.Values.OrderBy(x => Convert.ToInt64(x["relation_id"])))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Insert(write, tx, "tariff_publication_relation", RelationColumns,
                        "relation_id", relation, new Dictionary<string, object?>
                        {
                            ["source_publication_id"] =
                                idMap[Convert.ToInt64(relation["source_publication_id"])],
                            ["target_publication_id"] =
                                idMap[Convert.ToInt64(relation["target_publication_id"])]
                        });
                    relationCount++;
                }
                tx.Commit();
            }

            using (var check = Open(output, writable: false))
            {
                IsolatedRecoveryTargetHealth.RequireHealthy(check);
                foreach (var graph in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = idMap[graph.SourceId];
                    var rows = Query(check, """
                        SELECT * FROM tariff_publication WHERE publication_id=$id;
                        """, 2, ("$id", id));
                    if (rows.Count != 1 ||
                        Convert.ToString(rows[0]["local_pdf_path"]) != graph.Pdf.StageFilePath ||
                        !File.Exists(graph.Pdf.StageFilePath) ||
                        Query(check, """
                            SELECT * FROM tariff_publication_page_text WHERE publication_id=$id;
                            """, MaxChildren, ("$id", id)).Count != graph.Pages.Count ||
                        Query(check, """
                            SELECT * FROM tariff_rate_candidate WHERE publication_id=$id;
                            """, MaxChildren, ("$id", id)).Count != graph.Rates.Count)
                        throw new InvalidDataException("Staged tariff document graph is incomplete.");
                }
                foreach (var relation in relations.Values)
                {
                    var result = Query(check, """
                        SELECT * FROM tariff_publication_relation
                        WHERE source_publication_id=$from AND target_publication_id=$to
                          AND relation_type=$type AND target_official_document_number=$number;
                        """, 2,
                        ("$from", idMap[Convert.ToInt64(relation["source_publication_id"])]),
                        ("$to", idMap[Convert.ToInt64(relation["target_publication_id"])]),
                        ("$type", Convert.ToString(relation["relation_type"])!),
                        ("$number", Convert.ToString(relation["target_official_document_number"])!));
                    if (result.Count != 1)
                        throw new InvalidDataException("Staged tariff relation remapping failed.");
                }
                foreach (var (table, key, expectedDelta) in new[]
                {
                    ("tariff_publication","publication_id",selected.Count),
                    ("tariff_publication_page_text","publication_id,page_number",pageCount),
                    ("tariff_rate_candidate","rate_candidate_id",rateCount),
                    ("tariff_publication_relation","relation_id",relationCount)
                })
                {
                    if (Count(check, table) != Count(destination, table) + expectedDelta)
                        throw new InvalidDataException("Unexpected tariff table row count.");
                    Preserve(destination, check, table, key, cancellationToken);
                }
            }
            if (_planner.CreatePlan(verifiedArchive, original).PlanId != approvedPlan.PlanId ||
                !_stages.VerifyAgainstArchive(bundle.Evidence, verifiedArchive, input))
                throw new InvalidDataException("Tariff source/target changed during staging.");
            success = true;
            return new SyntheticTariffGraphImport(output, selected.Count, pageCount,
                rateCount, relationCount,
                selected.Select(x => new SyntheticTariffIdMap(
                    x.SourceId, idMap[x.SourceId], x.Pdf.Sha256)).ToArray(),
                "STAGED_CLOSED_TARIFF_GRAPH_SYNTHETIC_ONLY", false,
                "Only uniquely verified complete official-tariff document graphs " +
                "were cloned into a NEW disposable SQLite; no live restore, " +
                "tariff calculation, official applicability claim or active Data change.");
        }
        finally
        {
            if (File.Exists(sourceCopy)) File.Delete(sourceCopy);
            if (!success && File.Exists(output)) File.Delete(output);
        }
    }

    private static void CheckColumns(SqliteConnection c, string table, string[] expected)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(" + table + ");";
        var cols = new List<string>();
        using (var rows = cmd.ExecuteReader())
            while (rows.Read()) cols.Add(rows.GetString(1));
        if (!cols.SequenceEqual(expected))
            throw new NotSupportedException("Unexpected tariff table layout: " + table);
        using var triggers = c.CreateCommand();
        triggers.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type='trigger' AND tbl_name=$table;";
        triggers.Parameters.AddWithValue("$table", table);
        if (Convert.ToInt64(triggers.ExecuteScalar()) != 0)
            throw new NotSupportedException("Unexpected tariff recovery trigger.");
    }

    private static List<Dictionary<string, object?>> Query(SqliteConnection c,
        string sql, int maximum, params (string Name, object Value)[] values)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in values)
            cmd.Parameters.AddWithValue(name, value);
        if (sql.Contains("$limit", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("$limit", maximum + 1);
        using var reader = cmd.ExecuteReader();
        var found = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            if (found.Count >= maximum)
                throw new InvalidDataException("Tariff dependency exceeds bounded synthetic staging.");
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
                row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
            found.Add(row);
        }
        return found;
    }

    private static void Insert(SqliteConnection c, SqliteTransaction tx,
        string table, string[] columns, string identity,
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyDictionary<string, object?> changes)
    {
        var cols = columns.Where(x => x != identity).ToArray();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO " + table + "(" + string.Join(",", cols) +
            ") VALUES (" + string.Join(",", cols.Select((_, i) => "$p" + i)) + ");";
        for (var i = 0; i < cols.Length; i++)
        {
            var key = cols[i];
            cmd.Parameters.AddWithValue("$p" + i,
                (changes.TryGetValue(key, out var v) ? v : row[key]) ?? DBNull.Value);
        }
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Unable to stage reviewed tariff row.");
    }

    private static long Insert(SqliteConnection c, SqliteTransaction tx,
        string table, string[] columns, string identity,
        IReadOnlyDictionary<string, object?> row,
        Dictionary<string, object?> changes)
    {
        // Parent/rate/relations have generated surrogate keys.
        Insert(c, tx, table, columns, identity, row,
            (IReadOnlyDictionary<string, object?>)changes);
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void Preserve(SqliteConnection original, SqliteConnection output,
        string table, string key, CancellationToken token)
    {
        using var l = original.CreateCommand();
        using var r = output.CreateCommand();
        l.CommandText = "SELECT * FROM " + table + " ORDER BY " + key + ";";
        r.CommandText = l.CommandText;
        using var left = l.ExecuteReader();
        using var right = r.ExecuteReader();
        var n = 0;
        while (left.Read())
        {
            if (++n % 256 == 0) token.ThrowIfCancellationRequested();
            if (!right.Read() || right.FieldCount != left.FieldCount)
                throw new InvalidDataException("Existing tariff evidence disappeared.");
            for (var i = 0; i < left.FieldCount; i++)
            {
                if (left.IsDBNull(i) && right.IsDBNull(i)) continue;
                if (left.IsDBNull(i) != right.IsDBNull(i) ||
                    !Equals(left.GetValue(i), right.GetValue(i)))
                    throw new InvalidDataException("Existing tariff evidence changed.");
            }
        }
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
            DataSource = path, Mode = writable ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly,
            Pooling = false, ForeignKeys = true
        }.ToString());
        c.Open();
        if (!writable)
        {
            using var pragma = c.CreateCommand();
            pragma.CommandText = "PRAGMA query_only=ON;";
            pragma.ExecuteNonQuery();
        }
        return c;
    }
    private static bool Linked(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private sealed record TariffGraph(SyntheticStagedDocument Pdf,
        Dictionary<string, object?> Publication,
        List<Dictionary<string, object?>> Pages,
        List<Dictionary<string, object?>> Rates, long SourceId);
}

public sealed record SyntheticTariffIdMap(long SourceId, long StagedId, string PdfSha256);
public sealed record SyntheticTariffGraphImport(
    string StagedDatabasePath, int AddedPublications, int AddedPages,
    int AddedRateCandidates, int AddedRelations,
    IReadOnlyList<SyntheticTariffIdMap> IdMap, string Status,
    bool RealRestoreAuthorized, string SafetyDisclaimer);

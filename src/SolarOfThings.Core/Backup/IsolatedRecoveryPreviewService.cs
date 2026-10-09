using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Synthetic-fixture-only, read-only preview of selected record categories.
/// It NEVER imports or updates target data. Older schema adapters are explicitly
/// unsupported until their mappings and dependencies are independently tested.
/// </summary>
public sealed class IsolatedRecoveryPreviewService
{
    private const int MaxRecordsPerCategory = 100_000;

    // Only tables with a stable natural identity and explicit field semantics.
    // Surrogate-ID-linked bill/line, tariff-line and telemetry tables require
    // dependency-aware adapters before even PREVIEW can claim safe merge.
    private static readonly CategorySpec[] Supported =
    [
        new("SETTINGS", "app_setting", "key",
            ["key", "value"]),
        new("TARIFF_SOURCES", "tariff_publication", "source_url",
            ["source_url", "provider", "category", "title", "effective_from",
             "is_retroactive", "content_sha256", "content_length", "page_count",
             "capture_status"]),
        new("BILL_SOURCE_DOCUMENTS", "utility_bill_document", "content_sha256",
            ["content_sha256", "provider", "content_length", "page_count",
             "parser_version"])
    ];

    public RecoveryPreviewResult Preview(string completePackage,
        string isolatedTargetDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completePackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(isolatedTargetDatabasePath);
        var target = Path.GetFullPath(isolatedTargetDatabasePath);
        // Match the stricter staging and relation-audit gates: OS temp alone is
        // not proof that a database belongs to a synthetic smoke fixture.
        // Check the unique fixture root, exact marker, and linked path segments
        // BEFORE opening or inspecting any target database.
        IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(target);
        var temp = Path.GetFullPath(Path.GetTempPath());
        if (!File.Exists(target))
            throw new FileNotFoundException("Isolated target database not found.", target);
        if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Linked target databases are not supported.");

        // A forged/unavailable package never enters category comparisons.
        var manifest = FullBackupService.VerifyArchive(completePackage);
        if (manifest.SchemaVersion != SqliteDatabase.CurrentSchemaVersion)
            return new RecoveryPreviewResult(manifest.SchemaVersion,
                SqliteDatabase.CurrentSchemaVersion, "UNSUPPORTED_SCHEMA",
                [new RecoveryCategoryResult("SCHEMA_ADAPTER", 0, 0, 0,
                    "No approved adapter for this historical backup schema.")],
                "No import is available. Original ZIP and target unchanged.");

        var stage = Path.Combine(temp,
            "SolarEnergyMonitor-recovery-preview-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var zip = ZipFile.OpenRead(completePackage))
            {
                var entry = zip.GetEntry("database/energy.db")
                    ?? throw new InvalidDataException("Missing package database.");
                using var source = entry.Open();
                using var staging = new FileStream(stage, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                source.CopyTo(staging);
            }

            using var from = OpenReadOnly(stage);
            using var to = OpenReadOnly(target);
            var targetVersion = GetSchemaVersion(to);
            if (targetVersion != SqliteDatabase.CurrentSchemaVersion)
                return new RecoveryPreviewResult(manifest.SchemaVersion, targetVersion,
                    "UNSUPPORTED_TARGET_SCHEMA",
                    [new RecoveryCategoryResult("SCHEMA_ADAPTER", 0, 0, 0,
                        "No approved adapter for this target database schema.")],
                    "No import is available. Original ZIP and target unchanged.");

            // A valid schema number does not imply valid pages or intact FKs.
            // A compromised synthetic target must not yield misleading counts.
            IsolatedRecoveryTargetHealth.RequireHealthy(to);
            var results = new List<RecoveryCategoryResult>();
            foreach (var specification in Supported)
            {
                try
                {
                    var originals = ReadCategory(from, specification);
                    var active = ReadCategory(to, specification);
                    var missing = 0;
                    var identical = 0;
                    var conflicts = 0;
                    var changedFields = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var (id, body) in originals)
                    {
                        if (!active.TryGetValue(id, out var existing))
                            missing++;
                        else if (string.Equals(existing, body, StringComparison.Ordinal))
                            identical++;
                        else
                        {
                            conflicts++;
                            var sourceValues = JsonSerializer.Deserialize<string?[]>(body)
                                ?? throw new InvalidDataException("Unreadable source row.");
                            var targetValues = JsonSerializer.Deserialize<string?[]>(existing)
                                ?? throw new InvalidDataException("Unreadable target row.");
                            if (sourceValues.Length != specification.Fields.Length ||
                                targetValues.Length != specification.Fields.Length)
                                throw new InvalidDataException("Unsupported preview field shape.");
                            for (var field = 0; field < specification.Fields.Length; field++)
                                if (!string.Equals(sourceValues[field], targetValues[field],
                                        StringComparison.Ordinal))
                                {
                                    var name = specification.Fields[field];
                                    changedFields[name] = changedFields.GetValueOrDefault(name) + 1;
                                }
                        }
                    }
                    var targetOnly = active.Keys.Count(id => !originals.ContainsKey(id));
                    results.Add(new RecoveryCategoryResult(specification.Name,
                        missing, identical, conflicts, null)
                    {
                        TargetOnly = targetOnly,
                        SourceRecords = originals.Count,
                        TargetRecords = active.Count,
                        ChangedFields = changedFields
                    });
                }
                catch (SqliteException ex)
                {
                    results.Add(new RecoveryCategoryResult(specification.Name,
                        0, 0, 0, "Category schema is unsupported: " + ex.SqliteErrorCode));
                }
                catch (InvalidDataException)
                {
                    results.Add(new RecoveryCategoryResult(specification.Name,
                        0, 0, 0, "Category exceeds bounded preview or contains duplicate identities."));
                }
            }
            // Unexpected missing tables, duplicate portable keys, or bounded-
            // preview limits are NOT a successful complete preview. Do not
            // let callers mistake zero counters for a validated clean category.
            var partial = results.Any(category => category.UnsupportedReason is not null);
            // Explicitly refuse any implied import of relationally coupled
            // categories until source identity/foreign keys are mapped.
            results.Add(new RecoveryCategoryResult("METER_READINGS", 0, 0, 0,
                "Schema v13+ allows distinct readings at the same timestamp; no stable portable natural key or foreign-key remapping has been approved."));
            results.Add(new RecoveryCategoryResult("BILLS_AND_CHARGES", 0, 0, 0,
                "Linked bill / reading / charge relationships need a reviewed adapter."));
            results.Add(new RecoveryCategoryResult("ENERGY_TELEMETRY", 0, 0, 0,
                "Large history, normalization rules, device identities and coverage need a reviewed adapter."));
            return new RecoveryPreviewResult(manifest.SchemaVersion, targetVersion,
                partial ? "PARTIAL_PREVIEW" : "READ_ONLY_PREVIEW", results,
                partial
                    ? "One or more categories could not be compared: counts are INCOMPLETE. No import is authorized; original files and target unchanged."
                    : "Counts are a dry run, NOT an import authorization; original files and target remain unchanged.");
        }
        finally
        {
            if (File.Exists(stage))
            {
                try { File.Delete(stage); }
                catch (IOException) { /* caller can locate incomplete temp by unique prefix */ }
            }
        }
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var queryOnly = connection.CreateCommand();
        queryOnly.CommandText = "PRAGMA query_only = ON;";
        queryOnly.ExecuteNonQuery();
        return connection;
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static Dictionary<string, string> ReadCategory(SqliteConnection connection,
        CategorySpec spec)
    {
        // Table and field names are hard-coded, not supplied by user or ZIP.
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT " + string.Join(",", spec.Fields) +
            " FROM " + spec.Table + ";";
        using var rows = command.ExecuteReader();
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        while (rows.Read())
        {
            if (map.Count >= MaxRecordsPerCategory)
                throw new InvalidDataException("Category exceeds supported preview row count.");
            var key = Convert.ToString(rows.GetValue(0), CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidDataException("Record has no stable natural identity.");
            var values = new string?[spec.Fields.Length];
            for (var index = 0; index < values.Length; index++)
                values[index] = rows.IsDBNull(index) ? null :
                    Convert.ToString(rows.GetValue(index), CultureInfo.InvariantCulture);
            if (!map.TryAdd(key, JsonSerializer.Serialize(values)))
                throw new InvalidDataException("Repeated natural key in category.");
        }
        return map;
    }

    private sealed record CategorySpec(string Name, string Table, string Identity,
        string[] Fields);
}

public sealed record RecoveryCategoryResult(string Category, int Missing,
    int Identical, int Conflicts, string? UnsupportedReason)
{
    // Records present ONLY in the synthetic destination must not be
    // silently treated as equivalent or candidates for deletion.
    public int TargetOnly { get; init; }
    public int SourceRecords { get; init; }
    public int TargetRecords { get; init; }
    // Field names and aggregate counts only; never surface source/target
    // values, paths, or settings keys in the preview.
    public IReadOnlyDictionary<string, int> ChangedFields { get; init; } =
        new Dictionary<string, int>();
}

public sealed record RecoveryPreviewResult(int BackupSchemaVersion, int TargetSchemaVersion,
    string Status, IReadOnlyList<RecoveryCategoryResult> Categories, string Disclaimer);

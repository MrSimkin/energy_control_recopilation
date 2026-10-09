using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Test-only recovery plan. The ONLY executable stage is an additive copy of
/// app_setting into a NEW synthetic database. No UI or live restore entrypoint.
/// All dependent entity stages are diagnostics, never imported or activated.
/// </summary>
public sealed class IsolatedRecoveryPlanService
{
    private const int MaxFingerprintSettings = 100_000;
    private const int MaxFingerprintOtherRows = 150_000;
    private readonly IsolatedRecoveryPreviewService _preview = new();
    private readonly IsolatedRecoveryRelationAuditService _relations = new();
    private readonly IsolatedRecoveryAdditiveTestService _staging = new();

    public SyntheticRecoveryPlan CreatePlan(string backupZip, string targetDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        var target = Path.GetFullPath(targetDatabasePath);
        var package = Path.GetFullPath(backupZip);
        // Reject ordinary OS temp paths and linked directory hops BEFORE
        // reading a supposed destination database.
        IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(target);

        var preview = _preview.Preview(package, target);
        var ready = preview.Status == "READ_ONLY_PREVIEW" &&
                    preview.BackupSchemaVersion == SqliteDatabase.CurrentSchemaVersion &&
                    preview.TargetSchemaVersion == SqliteDatabase.CurrentSchemaVersion;
        RecoveryRelationAudit? graph = null;
        if (ready)
        {
            graph = _relations.Audit(package, target);
            ready = graph.Status == "READ_ONLY_GRAPH_AUDIT";
        }

        var settings = preview.Categories.FirstOrDefault(c => c.Category == "SETTINGS");
        var settingsReady = ready && settings is { UnsupportedReason: null };
        var steps = new List<SyntheticRecoveryPlanStep>
        {
            new(1, "VERIFIED_SOURCE_PACKAGE", "READ_ONLY_VERIFIED", 1,
                "Complete ZIP, embedded SQLite and document references are checked."),
            new(2, "SETTINGS", settingsReady ? "SYNTHETIC_STAGE_ONLY" : "BLOCKED",
                settings?.Missing ?? 0,
                settingsReady
                    ? "Add missing settings only to a new synthetic DB; preserve conflicts and destination-only keys."
                    : "No settings stage unless both schemas and every supported comparison are valid."),
            new(3, "BILL_SOURCE_DOCUMENTS", "REVIEW_ONLY",
                graph is null ? 0 : graph.UnlinkedBillDocuments.Count +
                    graph.Bills.Where(b => !string.IsNullOrWhiteSpace(b.OriginalDocumentSha256))
                        .Select(b => b.OriginalDocumentSha256)
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                "Count unique original document hashes plus unlinked documents; no files are copied."),
            new(4, "BILLS_AND_CHARGES", "BLOCKED_DEPENDENCIES",
                graph?.Bills.Count ?? 0,
                "Original-document linkage, charge IDs and field evidence require an adapter."),
            new(5, "METER_READINGS", "BLOCKED_AMBIGUOUS_IDENTITY",
                graph?.Totals.BillsWithMeterLinks ?? 0,
                "Timestamp and numeric readings are hints, not unique portable meter identities."),
            new(6, "TARIFF_SOURCES", "REVIEW_ONLY",
                graph?.Tariffs.Count ?? 0,
                "Matching source URLs or PDF hashes never authorize changing target publications."),
            new(7, "TARIFF_RELATIONS", "BLOCKED_DEPENDENCIES",
                graph?.Totals.OutgoingTariffLinks ?? 0,
                "Count source relations once; incoming links refer to the same edges. FK remapping still blocked."),
            new(8, "ENERGY_TELEMETRY", "BLOCKED_UNSUPPORTED", 0,
                "Historical samples and device identity adapters are not implemented.")
        };
        var status = settingsReady ? "SYNTHETIC_SETTINGS_PLAN" : "BLOCKED_INCOMPLETE_PLAN";
        // Package bytes and WAL-visible logical app_setting rows both bind
        // this plan to the source and target at the time it is created.
        var packageFingerprint = Sha256File(package);
        var settingsFingerprint = settingsReady ? FingerprintSettings(target) : "";
        // Also bind to every NON-settings SQLite table (including schema
        // migrations, relational evidence and telemetry). A user could change
        // linked data without altering app_setting; plans then become stale.
        var otherTablesFingerprint = settingsReady ? FingerprintOtherTables(target) : "";
        var planId = Sha256Text(JsonSerializer.Serialize(new[]
        {
            packageFingerprint, settingsFingerprint, otherTablesFingerprint, status,
            settings?.Missing.ToString() ?? "0",
            settings?.Conflicts.ToString() ?? "0",
            settings?.TargetOnly.ToString() ?? "0"
        }));
        return new SyntheticRecoveryPlan(package, target, status, planId,
            packageFingerprint, settingsFingerprint, steps,
            settings?.Missing ?? 0, settings?.Conflicts ?? 0,
            settings?.Identical ?? 0, settings?.TargetOnly ?? 0,
            "Synthetic fixture only. Every stage except SETTINGS is review-only or blocked. " +
            "This plan never authorizes owner-data restoration or activation.")
        {
            TargetOtherTablesSha256 = otherTablesFingerprint
        };
    }

    /// <summary>
    /// Rechecks the plan's exact source/target bindings and WAL-aware settings
    /// fingerprint, stages the settings, reconciles counters and independently
    /// previews the staged result. A mismatch removes the new synthetic stage.
    /// </summary>
    public SyntheticAdditiveImportResult StageSettingsOnly(
        SyntheticRecoveryPlan plan, string backupZip, string targetDatabasePath,
        bool simulateFailureAfterFirstInsert = false,
        bool simulateFailureAfterStagedCopy = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        var package = Path.GetFullPath(backupZip);
        var target = Path.GetFullPath(targetDatabasePath);
        IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(target);
        if (!string.Equals(package, plan.PackagePath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(target, plan.TargetDatabasePath, StringComparison.OrdinalIgnoreCase) ||
            plan.Status != "SYNTHETIC_SETTINGS_PLAN")
            throw new InvalidOperationException("Plan is not authorized for this synthetic fixture pair.");

        var current = CreatePlan(package, target);
        if (current.Status != plan.Status ||
            current.PlanId != plan.PlanId ||
            current.SourcePackageSha256 != plan.SourcePackageSha256 ||
            current.TargetSettingsSha256 != plan.TargetSettingsSha256 ||
            current.TargetOtherTablesSha256 != plan.TargetOtherTablesSha256 ||
            // Verify the ENTIRE policy checklist, not only the SETTINGS row.
            // Synthetic callers can construct records with modified stages.
            !current.Steps.SequenceEqual(plan.Steps) ||
            current.MissingSettings != plan.MissingSettings ||
            current.ConflictingSettings != plan.ConflictingSettings ||
            current.TargetOnlySettings != plan.TargetOnlySettings ||
            !plan.Steps.Any(s => s.Category == "SETTINGS" &&
                                 s.Status == "SYNTHETIC_STAGE_ONLY"))
            throw new InvalidOperationException(
                "Recovery plan is stale or incomplete; create a new synthetic plan.");

        SyntheticAdditiveImportResult? staged = null;
        try
        {
            staged = _staging.ApplyToNewStagedFixture(
                package, target, simulateFailureAfterFirstInsert);
            if (simulateFailureAfterStagedCopy)
                throw new InvalidOperationException(
                    "SYNTHETIC_TEST_INJECTED_AFTER_STAGED_COPY");
            if (staged.Added != current.MissingSettings ||
                staged.Conflicts != current.ConflictingSettings ||
                staged.AlreadyPresent != current.IdenticalSettings)
                throw new InvalidDataException(
                    "Staged settings counters disagree with the approved synthetic preview.");

            // A fresh preview on the output proves no source setting remains
            // missing and the destination's conflicts/extra keys were kept.
            var verification = _preview.Preview(package, staged.StagedDatabasePath);
            var settings = verification.Categories.Single(s => s.Category == "SETTINGS");
            if (verification.Status != "READ_ONLY_PREVIEW" ||
                settings.Missing != 0 ||
                settings.Conflicts != current.ConflictingSettings ||
                settings.TargetOnly != current.TargetOnlySettings ||
                FingerprintSettings(target) != current.TargetSettingsSha256 ||
                FingerprintOtherTables(target) != current.TargetOtherTablesSha256 ||
                FingerprintOtherTables(staged.StagedDatabasePath) !=
                    current.TargetOtherTablesSha256 ||
                !PreservedOriginalSettings(target, staged.StagedDatabasePath))
                throw new InvalidDataException(
                    "Post-stage checks failed or the original synthetic target changed.");
            return staged;
        }
        catch
        {
            if (staged is not null && File.Exists(staged.StagedDatabasePath))
            {
                // This path was returned by the TEST-ONLY staging service,
                // under the previously verified marked synthetic root.
                File.Delete(staged.StagedDatabasePath);
            }
            throw;
        }
    }

    // All existing target settings, including updated_utc and null values,
    // must survive the additive synthetic stage byte-for-byte at field level.
    // A preview that compares key/value alone does not prove this invariant.
    private static bool PreservedOriginalSettings(string original, string staged)
    {
        static Dictionary<string, (string? Value, string Stamp)> Read(string path)
        {
            using var conn = OpenReadOnlyFixture(path);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key,value,updated_utc FROM app_setting;";
            using var rows = cmd.ExecuteReader();
            var records = new Dictionary<string, (string?, string)>(StringComparer.Ordinal);
            while (rows.Read())
            {
                if (records.Count >= MaxFingerprintSettings)
                    throw new InvalidDataException("Synthetic target settings exceed limit.");
                if (!records.TryAdd(rows.GetString(0),
                        (rows.IsDBNull(1) ? null : rows.GetString(1), rows.GetString(2))))
                    throw new InvalidDataException("Duplicate synthetic setting key.");
            }
            return records;
        }
        var earlier = Read(original);
        var later = Read(staged);
        return earlier.All(pair => later.TryGetValue(pair.Key, out var value) &&
                                   value == pair.Value);
    }

    private static SqliteConnection OpenReadOnlyFixture(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=ON;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    // Hash the actual, WAL-visible contents of ALL ordinary non-settings
    // tables. Bound row counts prevent accidental unbounded scans, and the
    // marked synthetic-root gate precedes every caller of this function.
    private static string FingerprintOtherTables(string path)
    {
        using var connection = OpenReadOnlyFixture(path);
        using var schema = connection.CreateCommand();
        schema.CommandText = """
            SELECT name,sql FROM sqlite_schema
            WHERE type='table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;
        var tables = new List<(string Name, string Sql)>();
        using (var records = schema.ExecuteReader())
            while (records.Read())
                tables.Add((records.GetString(0),
                    records.IsDBNull(1) ? "" : records.GetString(1)));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var scanned = 0;
        foreach (var (table, definition) in tables)
        {
            if (table == "app_setting") continue;
            hash.AppendData(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new[] { table, definition }) + "\n"));
            using var cmd = connection.CreateCommand();
            // The table name comes only from sqlite_schema, never directly
            // from a caller; quote defensively for arbitrary identifier text.
            var quoted = table.Replace("\"", "\"\"", StringComparison.Ordinal);
            cmd.CommandText = "SELECT * FROM \"" + quoted + "\" ORDER BY rowid;";
            using var rows = cmd.ExecuteReader();
            while (rows.Read())
            {
                if (++scanned > MaxFingerprintOtherRows)
                    throw new InvalidDataException(
                        "Synthetic dependency fingerprint exceeds safe row limit.");
                var cells = new string?[rows.FieldCount];
                for (var col = 0; col < cells.Length; col++)
                {
                    if (rows.IsDBNull(col)) continue;
                    var value = rows.GetValue(col);
                    cells[col] = value is byte[] bytes
                        ? Convert.ToBase64String(bytes)
                        : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                }
                hash.AppendData(Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(cells) + "\n"));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string FingerprintSettings(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=ON;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT key,value,updated_utc FROM app_setting ORDER BY key;";
        using var rows = cmd.ExecuteReader();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var count = 0;
        while (rows.Read())
        {
            if (++count > MaxFingerprintSettings)
                throw new InvalidDataException("Synthetic settings fingerprint exceeds limit.");
            // Structured JSON, including nulls, avoids delimiter collisions
            // and includes uncheckpointed but committed WAL-visible values.
            var body = JsonSerializer.Serialize(new string?[]
            {
                rows.GetString(0),
                rows.IsDBNull(1) ? null : rows.GetString(1),
                rows.GetString(2)
            }) + "\n";
            hash.AppendData(Encoding.UTF8.GetBytes(body));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Sha256Text(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public sealed record SyntheticRecoveryPlan(
    string PackagePath, string TargetDatabasePath, string Status, string PlanId,
    string SourcePackageSha256, string TargetSettingsSha256,
    IReadOnlyList<SyntheticRecoveryPlanStep> Steps, int MissingSettings,
    int ConflictingSettings, int IdenticalSettings, int TargetOnlySettings,
    string SafetyDisclaimer)
{
    public string TargetOtherTablesSha256 { get; init; } = "";
}

public sealed record SyntheticRecoveryPlanStep(
    int Order, string Category, string Status, long Records, string Explanation);

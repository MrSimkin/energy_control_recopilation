using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST ONLY: append verified, previously absent raw history samples to a NEW
/// disposable, marked SQLite fixture. Requires identical strong commissioning
/// identity in both fixtures and a fresh approved plan. No real restore, no
/// history-day coverage, normalized metrics or raw API capture reconstruction.
/// </summary>
public sealed class IsolatedRecoveryRawHistoryStageTestService
{
    private const int MaxSamplesPerDevice = 100_000;
    private readonly IsolatedRecoveryPlanService _planner = new();
    private readonly IsolatedRecoveryPreviewService _preview = new();

    public SyntheticRawHistoryStageResult Stage(
        SyntheticRecoveryPlan reviewedPlan, string completeBackupZip,
        string originalSyntheticTarget, string reviewedDeviceId,
        CancellationToken cancellationToken = default,
        bool simulateInterruptionAfterFirstInsert = false)
    {
        ArgumentNullException.ThrowIfNull(reviewedPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(completeBackupZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalSyntheticTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedDeviceId);
        cancellationToken.ThrowIfCancellationRequested();
        if (reviewedDeviceId.Length > 128)
            throw new InvalidOperationException("Reviewed synthetic device identity is too long.");

        var target = Path.GetFullPath(originalSyntheticTarget);
        var archive = Path.GetFullPath(completeBackupZip);
        var fixtureRoot = IsolatedRecoveryAdditiveTestService
            .RequireSyntheticFixtureRoot(target);
        if (!File.Exists(target) ||
            (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0 ||
            !string.Equals(target, reviewedPlan.TargetDatabasePath,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(archive, reviewedPlan.PackagePath,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a reviewed regular synthetic fixture is supported.");

        var current = _planner.CreatePlan(archive, target);
        if (current.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            reviewedPlan.Status != current.Status ||
            reviewedPlan.PlanId != current.PlanId ||
            reviewedPlan.SourcePackageSha256 != current.SourcePackageSha256 ||
            reviewedPlan.TargetSettingsSha256 != current.TargetSettingsSha256 ||
            reviewedPlan.TargetOtherTablesSha256 != current.TargetOtherTablesSha256 ||
            !reviewedPlan.Steps.SequenceEqual(current.Steps))
            throw new InvalidOperationException("Raw history plan is stale, forged or not approved.");

        var baseline = _preview.Preview(archive, target);
        var rawBefore = baseline.Categories.Single(c => c.Category == "HISTORY_RAW_SAMPLES");
        if (baseline.Status != "READ_ONLY_PREVIEW" ||
            rawBefore.UnsupportedReason is not null)
            throw new InvalidOperationException("Raw history comparison is not complete.");

        var manifest = FullBackupService.VerifyArchive(archive);
        if (manifest.SchemaVersion != SqliteDatabase.CurrentSchemaVersion)
            throw new NotSupportedException("Raw history staging supports v17 only.");

        var staging = Path.Combine(fixtureRoot,
            "recovery-raw-history-staged-" + Guid.NewGuid().ToString("N") + ".db");
        var extracted = Path.Combine(fixtureRoot,
            ".raw-history-source-" + Guid.NewGuid().ToString("N") + ".db");
        var keepStage = false;
        try
        {
            using (var package = ZipFile.OpenRead(archive))
            using (var from = (package.GetEntry("database/energy.db") ??
                throw new InvalidDataException("Verified package database missing.")).Open())
            using (var to = new FileStream(extracted, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
                from.CopyTo(to);

            using var source = OpenReadOnly(extracted);
            using var original = OpenReadOnly(target);
            IsolatedRecoveryTargetHealth.RequireHealthy(source);
            IsolatedRecoveryTargetHealth.RequireHealthy(original);
            if (Schema(source) != SqliteDatabase.CurrentSchemaVersion ||
                Schema(original) != SqliteDatabase.CurrentSchemaVersion)
                throw new NotSupportedException("Raw history requires matching v17 fixtures.");

            // Portable device_id alone is NOT evidence that these are the
            // same physical inverter. Match all selected stable identifiers.
            var sourceIdentity = ReadIdentity(source, reviewedDeviceId);
            var targetIdentity = ReadIdentity(original, reviewedDeviceId);
            if (sourceIdentity != targetIdentity)
                throw new InvalidOperationException(
                    "Commissioning evidence does not establish the same synthetic device.");

            // SQLite backup materializes committed target WAL rows into a new
            // unrelated DB. The original stays open read-only throughout.
            using (var fresh = OpenWritable(staging))
                original.BackupDatabase(fresh);

            var added = 0;
            var identical = 0;
            var conflicts = 0;
            var scanned = 0;
            using (var staged = OpenWritable(staging))
            using (var transaction = staged.BeginTransaction())
            using (var cmd = source.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT attribute_key,recorded_at_utc,value_json,is_missing,
                           source,retrieved_utc
                    FROM history_sample WHERE device_id=$device
                    ORDER BY attribute_key,recorded_at_utc;
                    """;
                cmd.Parameters.AddWithValue("$device", reviewedDeviceId);
                using var rows = cmd.ExecuteReader();
                while (rows.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++scanned > MaxSamplesPerDevice)
                        throw new InvalidDataException("Too many source rows for one synthetic device.");
                    var attribute = rows.GetString(0);
                    var when = rows.GetString(1);
                    var json = rows.IsDBNull(2) ? null : rows.GetString(2);
                    var missing = rows.GetInt64(3);
                    var origin = rows.GetString(4);
                    var retrieved = rows.GetString(5);

                    using var lookup = staged.CreateCommand();
                    lookup.Transaction = transaction;
                    lookup.CommandText = """
                        SELECT value_json,is_missing,source,retrieved_utc
                        FROM history_sample
                        WHERE device_id=$device AND attribute_key=$attribute
                              AND recorded_at_utc=$when;
                        """;
                    lookup.Parameters.AddWithValue("$device", reviewedDeviceId);
                    lookup.Parameters.AddWithValue("$attribute", attribute);
                    lookup.Parameters.AddWithValue("$when", when);
                    using (var present = lookup.ExecuteReader())
                    {
                        if (present.Read())
                        {
                            var equal = string.Equals(
                                    json, present.IsDBNull(0) ? null : present.GetString(0),
                                    StringComparison.Ordinal) &&
                                missing == present.GetInt64(1) &&
                                string.Equals(origin, present.GetString(2),
                                    StringComparison.Ordinal) &&
                                string.Equals(retrieved, present.GetString(3),
                                    StringComparison.Ordinal);
                            if (equal) identical++; else conflicts++;
                            continue;
                        }
                    }

                    using var insert = staged.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO history_sample(
                            device_id,attribute_key,recorded_at_utc,value_json,
                            is_missing,source,retrieved_utc)
                        VALUES($device,$attribute,$when,$json,$missing,$origin,$retrieved);
                        """;
                    insert.Parameters.AddWithValue("$device", reviewedDeviceId);
                    insert.Parameters.AddWithValue("$attribute", attribute);
                    insert.Parameters.AddWithValue("$when", when);
                    insert.Parameters.AddWithValue("$json", (object?)json ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$missing", missing);
                    insert.Parameters.AddWithValue("$origin", origin);
                    insert.Parameters.AddWithValue("$retrieved", retrieved);
                    if (insert.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("Raw history insert was not atomic.");
                    added++;
                    if (simulateInterruptionAfterFirstInsert)
                        throw new InvalidOperationException(
                            "SYNTHETIC_RAW_HISTORY_INTERRUPTED_BEFORE_COMMIT");
                }
                transaction.Commit();
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (var verified = OpenReadOnly(staging))
                IsolatedRecoveryTargetHealth.RequireHealthy(verified);
            var stagedPreview = _preview.Preview(archive, staging);
            var rawAfter = stagedPreview.Categories.Single(
                c => c.Category == "HISTORY_RAW_SAMPLES");
            if (stagedPreview.Status != "READ_ONLY_PREVIEW" ||
                rawAfter.UnsupportedReason is not null ||
                rawAfter.Missing != rawBefore.Missing - added ||
                rawAfter.Identical != rawBefore.Identical + added ||
                rawAfter.Conflicts != rawBefore.Conflicts ||
                rawAfter.TargetOnly != rawBefore.TargetOnly ||
                rawAfter.TargetRecords != rawBefore.TargetRecords + added ||
                rawAfter.SourceRecords != rawBefore.SourceRecords)
                throw new InvalidDataException("Raw history staging counters failed independent preview.");

            // Independently verify non-raw categories were neither inserted nor
            // modified, including day coverage and derived normalized metrics.
            foreach (var category in baseline.Categories
                         .Where(c => c.Category != "HISTORY_RAW_SAMPLES"))
            {
                var after = stagedPreview.Categories.Single(c => c.Category == category.Category);
                if (category != after &&
                    (category.Missing != after.Missing ||
                     category.Identical != after.Identical ||
                     category.Conflicts != after.Conflicts ||
                     category.TargetOnly != after.TargetOnly ||
                     category.SourceRecords != after.SourceRecords ||
                     category.TargetRecords != after.TargetRecords))
                    throw new InvalidDataException("Raw history stage changed unrelated category.");
            }

            // A changed source archive or original fixture invalidates receipt.
            var unchanged = _planner.CreatePlan(archive, target);
            if (unchanged.PlanId != current.PlanId ||
                unchanged.SourcePackageSha256 != current.SourcePackageSha256 ||
                unchanged.TargetSettingsSha256 != current.TargetSettingsSha256 ||
                unchanged.TargetOtherTablesSha256 != current.TargetOtherTablesSha256 ||
                !unchanged.Steps.SequenceEqual(current.Steps))
                throw new InvalidDataException("Original fixture or ZIP changed while staging.");

            keepStage = true;
            return new SyntheticRawHistoryStageResult(staging, reviewedDeviceId,
                added, identical, conflicts, rawAfter.Missing,
                "STAGED_RAW_HISTORY_SYNTHETIC_ONLY", false,
                "Only absent raw samples were appended to a NEW synthetic database. " +
                "Existing conflicts preserved. Daily coverage, raw captures, derived metrics " +
                "and owner database were not modified. No activation or live import API.");
        }
        finally
        {
            TryDelete(extracted);
            if (!keepStage)
            {
                TryDelete(staging);
                TryDelete(staging + "-wal");
                TryDelete(staging + "-shm");
            }
        }
    }

    private sealed record DeviceIdentity(string StationId, string Timezone,
        string DeviceId, string SerialNumber, string Model, string Manufacturer,
        string DtuId, string DataSource);

    private static DeviceIdentity ReadIdentity(SqliteConnection db, string device)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT station_id,station_timezone,device_id,serial_number,model,
                   manufacturer,dtu_id,data_source
            FROM commissioning_profile WHERE profile_id=1 AND device_id=$device;
            """;
        cmd.Parameters.AddWithValue("$device", device);
        using var rows = cmd.ExecuteReader();
        if (!rows.Read()) throw new InvalidOperationException(
            "No matching source and target commissioning evidence for selected device.");
        var identity = new string[8];
        for (var i = 0; i < identity.Length; i++)
        {
            if (rows.IsDBNull(i) ||
                string.IsNullOrWhiteSpace(rows.GetString(i)))
                throw new InvalidOperationException(
                    "Incomplete commissioning identity cannot authorize synthetic staging.");
            identity[i] = rows.GetString(i);
        }
        if (rows.Read())
            throw new InvalidDataException("Ambiguous commissioning identity.");
        return new DeviceIdentity(identity[0], identity[1], identity[2],
            identity[3], identity[4], identity[5], identity[6], identity[7]);
    }

    private static int Schema(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        using var pragma = db.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=ON;";
        pragma.ExecuteNonQuery();
        return db;
    }

    private static SqliteConnection OpenWritable(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* Isolated temp cleanup only. */ }
        catch (UnauthorizedAccessException) { /* Isolated temp cleanup only. */ }
    }
}

public sealed record SyntheticRawHistoryStageResult(
    string StagedDatabasePath, string DeviceId, int Added, int Identical,
    int Conflicts, int RemainingMissingAcrossDevices, string Status,
    bool RealRestoreAuthorized, string SafetyExplanation);

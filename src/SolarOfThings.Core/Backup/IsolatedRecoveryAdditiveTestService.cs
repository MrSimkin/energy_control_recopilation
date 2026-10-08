using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Explicitly TEST-ONLY v17 additive recovery of natural-key application settings.
/// NEVER updates a caller-supplied database. Writes a new disposable synthetic
/// staging database, with no API to activate/replace the installed database.
/// Other dependent entities are deliberately outside the supported import scope.
/// </summary>
public sealed class IsolatedRecoveryAdditiveTestService
{
    private const string TestRoot = "SolarEnergyMonitorSmoke";
    private const string FixtureMarker = ".solar-recovery-fixture";
    private const string FixtureMarkerValue = "SYNTHETIC-ONLY-DO-NOT-USE-LIVE-DATA";
    private const int MaxSettings = 100_000;

    /// <summary>
    /// Marker belongs to the smoke-fixture folder, NOT to the owner Data directory.
    /// This API itself must never be called by the shipping application or UI.
    /// </summary>
    public static void MarkSyntheticSmokeFixture(string root)
    {
        var folder = ValidateFixtureRoot(root, requireMarker: false);
        File.WriteAllText(Path.Combine(folder, FixtureMarker),
            FixtureMarkerValue);
    }

    /// <summary>
    /// Creates a fresh staged database after verifying the complete package.
    /// No writes to targetDatabasePath, no document copying and no activation.
    /// A simulated interruption can be requested to prove rollback and cleanup.
    /// </summary>
    public SyntheticAdditiveImportResult ApplyToNewStagedFixture(
        string backupZip, string targetDatabasePath,
        bool simulateFailureAfterFirstInsert = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        var target = Path.GetFullPath(targetDatabasePath);
        var root = FindFixtureRoot(target);
        if (!File.Exists(target))
            throw new FileNotFoundException("Synthetic target database missing.", target);
        if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Linked databases are not accepted.");
        var metadata = FullBackupService.VerifyArchive(backupZip);
        if (metadata.SchemaVersion != SqliteDatabase.CurrentSchemaVersion)
            throw new NotSupportedException("No approved adapter for this source schema.");

        var stagedTarget = Path.Combine(root,
            "recovery-additive-staged-" + Guid.NewGuid().ToString("N") + ".db");
        var sourceTemp = Path.Combine(root,
            "recovery-source-verified-" + Guid.NewGuid().ToString("N") + ".db");
        var keepStage = false;
        try
        {
            // The package was fully integrity-verified before this extraction.
            using (var zip = ZipFile.OpenRead(backupZip))
            using (var input = (zip.GetEntry("database/energy.db") ??
                       throw new InvalidDataException("No source database.")).Open())
            using (var output = new FileStream(sourceTemp, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
                input.CopyTo(output);

            using var source = OpenReadOnly(sourceTemp);
            using var targetReader = OpenReadOnly(target);
            if (ReadSchema(source) != SqliteDatabase.CurrentSchemaVersion ||
                ReadSchema(targetReader) != SqliteDatabase.CurrentSchemaVersion)
                throw new NotSupportedException("Only equal schema v17 fixture pairs are supported.");

            // SQLite's native backup reads target including committed WAL rows,
            // while original target is OPEN READ-ONLY and never written here.
            using (var staged = OpenWritableNew(stagedTarget))
                targetReader.BackupDatabase(staged);

            var missing = 0;
            var alreadyPresent = 0;
            var conflicts = 0;
            var firstInsert = false;
            using (var staged = OpenWritableExisting(stagedTarget))
            using (var transaction = staged.BeginTransaction())
            {
                using var scan = source.CreateCommand();
                scan.CommandText = "SELECT key,value,updated_utc FROM app_setting ORDER BY key;";
                using var rows = scan.ExecuteReader();
                var scanned = 0;
                while (rows.Read())
                {
                    if (++scanned > MaxSettings)
                        throw new InvalidDataException("Too many settings to safely stage.");
                    var key = rows.GetString(0);
                    var value = rows.IsDBNull(1) ? null : rows.GetString(1);
                    var stamp = rows.GetString(2);

                    using var find = staged.CreateCommand();
                    find.Transaction = transaction;
                    find.CommandText = "SELECT value FROM app_setting WHERE key=$key;";
                    find.Parameters.AddWithValue("$key", key);
                    using (var previous = find.ExecuteReader())
                    {
                        if (previous.Read())
                        {
                            var existing = previous.IsDBNull(0) ? null : previous.GetString(0);
                            if (string.Equals(value, existing, StringComparison.Ordinal))
                                alreadyPresent++;
                            else
                                conflicts++;
                            continue;
                        }
                    }

                    // Bound SQL parameters: no dynamic identifiers or DML from the ZIP.
                    using var insert = staged.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO app_setting(key,value,updated_utc)
                        VALUES ($key,$value,$stamp);
                        """;
                    insert.Parameters.AddWithValue("$key", key);
                    insert.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$stamp", stamp);
                    if (insert.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("Expected one inserted setting.");
                    missing++;
                    if (simulateFailureAfterFirstInsert && !firstInsert)
                    {
                        firstInsert = true;
                        throw new InvalidOperationException(
                            "SYNTHETIC_TEST_INJECTED_BEFORE_COMMIT");
                    }
                }
                // Never update existing keys. Conflicts are counted, not resolved.
                transaction.Commit();
            }

            using (var recheck = OpenReadOnly(stagedTarget))
            {
                using var check = recheck.CreateCommand();
                check.CommandText = "PRAGMA integrity_check;";
                if (Convert.ToString(check.ExecuteScalar()) != "ok")
                    throw new InvalidDataException("Staged SQLite integrity check failed.");
                check.CommandText = "PRAGMA foreign_key_check;";
                using var reader = check.ExecuteReader();
                if (reader.Read())
                    throw new InvalidDataException("Staged foreign keys are inconsistent.");
            }

            keepStage = true;
            return new SyntheticAdditiveImportResult(stagedTarget,
                missing, alreadyPresent, conflicts, "STAGED_SYNTHETIC_ONLY",
                "No live database, existing row or source package was changed. " +
                "Linked bills, meter readings, tariff documents and telemetry are NOT imported.");
        }
        finally
        {
            TryDelete(sourceTemp);
            if (!keepStage)
                TryDelete(stagedTarget);
        }
    }

    // Shared fail-closed gate for recovery graph auditing. Do not expose
    // owner database paths to synthetic test-only services.
    internal static string RequireSyntheticFixtureRoot(string targetPath) =>
        FindFixtureRoot(targetPath);

    private static string FindFixtureRoot(string path)
    {
        var absolute = Path.GetFullPath(path);
        var temp = Path.GetFullPath(Path.GetTempPath());
        var relative = Path.GetRelativePath(temp, absolute);
        var components = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 3 ||
            !string.Equals(components[0], TestRoot, StringComparison.Ordinal) ||
            !Guid.TryParseExact(components[1], "N", out _) ||
            components.Skip(2).Any(x => x == ".."))
            throw new InvalidOperationException(
                "Only uniquely named synthetic smoke-test fixtures under OS temp are supported.");
        var root = ValidateFixtureRoot(
            Path.Combine(temp, components[0], components[1]), requireMarker: true);
        // A valid fixture root is insufficient if a nested target directory
        // redirects outside it. Do not follow junctions or symbolic links
        // between the marked root and the supplied database path.
        var cursor = root;
        foreach (var component in components.Skip(2).SkipLast(1))
        {
            cursor = Path.Combine(cursor, component);
            if (!Directory.Exists(cursor) ||
                (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "Synthetic recovery target has a missing or linked parent directory.");
        }
        return root;
    }

    private static string ValidateFixtureRoot(string root, bool requireMarker)
    {
        var full = Path.GetFullPath(root);
        var parent = Path.GetDirectoryName(full);
        var grandparent = parent is null ? null : Path.GetDirectoryName(parent);
        if (grandparent is null ||
            !string.Equals(Path.GetFileName(parent), TestRoot, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFullPath(grandparent),
                Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(full), "N", out _) ||
            !Directory.Exists(full))
            throw new InvalidOperationException("Not a recognized synthetic smoke-test root.");
        if (requireMarker &&
            (!File.Exists(Path.Combine(full, FixtureMarker)) ||
             File.ReadAllText(Path.Combine(full, FixtureMarker)) != FixtureMarkerValue))
            throw new InvalidOperationException("Synthetic fixture marker not present.");
        // Fail closed for symbolic links/reparse points in the entire path.
        var cursor = Path.GetDirectoryName(Path.GetFullPath(root));
        while (cursor is not null &&
               cursor.Length >= Path.GetPathRoot(cursor)!.Length)
        {
            if (Directory.Exists(cursor) &&
                (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked path components are unsupported.");
            var above = Path.GetDirectoryName(cursor);
            if (above == cursor) break;
            cursor = above;
        }
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Linked fixture root is unsupported.");
        return full;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static SqliteConnection OpenWritableNew(string path)
    {
        if (File.Exists(path))
            throw new IOException("Generated staging path unexpectedly exists.");
        return OpenWritableExisting(path);
    }

    private static SqliteConnection OpenWritableExisting(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static int ReadSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* never affect source or target on cleanup failure */ }
        catch (UnauthorizedAccessException) { /* temp cleanup only */ }
    }
}

public sealed record SyntheticAdditiveImportResult(
    string StagedDatabasePath, int Added, int AlreadyPresent, int Conflicts,
    string Status, string SafetyDisclaimer);

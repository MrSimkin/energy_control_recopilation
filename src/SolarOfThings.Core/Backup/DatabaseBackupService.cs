using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Creates consistent, independently verified SQLite snapshots. This service
/// intentionally exposes NO restore or active-database replacement operation.
/// </summary>
public sealed class DatabaseBackupService
{
    private readonly SqliteDatabase _database;
    private readonly AppPaths _paths;

    public DatabaseBackupService(SqliteDatabase database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    public VerifiedDatabaseBackup? CreateAutomaticBackupIfDue()
    {
        Directory.CreateDirectory(_paths.BackupDirectory);
        var recent = Directory.EnumerateFiles(
                _paths.BackupDirectory, "energy-automatic-*.sqlite")
            .Any(path => File.GetLastWriteTimeUtc(path) >
                DateTime.UtcNow.AddHours(-24));
        return recent ? null : CreateVerifiedBackup("automatic");
    }

    public VerifiedDatabaseBackup CreateVerifiedBackup(string kind = "manual")
    {
        if (kind != "automatic" && kind != "manual")
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!File.Exists(_database.DatabasePath))
            throw new FileNotFoundException("Active SQLite database not found.");

        Directory.CreateDirectory(_paths.BackupDirectory);
        var id = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var backupPath = Path.Combine(
            _paths.BackupDirectory, $"energy-{kind}-{id}.sqlite");
        var temporary = backupPath + ".inprogress";
        try
        {
            var sourceOptions = new SqliteConnectionStringBuilder
            {
                DataSource = _database.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            var targetOptions = new SqliteConnectionStringBuilder
            {
                DataSource = temporary,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };
            using (var source = new SqliteConnection(sourceOptions.ToString()))
            using (var target = new SqliteConnection(targetOptions.ToString()))
            {
                source.Open();
                target.Open();
                // SQLite native backup API, not File.Copy: includes committed
                // WAL pages in one consistent snapshot.
                source.BackupDatabase(target);
            }

            int schemaVersion;
            using (var verified = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = temporary,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString()))
            {
                verified.Open();
                using var integrity = verified.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check;";
                var outcome = Convert.ToString(integrity.ExecuteScalar());
                if (outcome != "ok")
                    throw new InvalidDataException(
                        "Backup failed SQLite integrity_check.");

                using var schema = verified.CreateCommand();
                schema.CommandText =
                    "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
                schemaVersion = Convert.ToInt32(schema.ExecuteScalar());
                if (schemaVersion <= 0 ||
                    schemaVersion > SqliteDatabase.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        "Backup schema version is not compatible with this application.");
                }
            }

            var size = new FileInfo(temporary).Length;
            string digest;
            using (var input = File.OpenRead(temporary))
            {
                digest = Convert.ToHexString(
                    SHA256.HashData(input)).ToLowerInvariant();
            }
            File.Move(temporary, backupPath);
            var result = new VerifiedDatabaseBackup(
                backupPath,
                schemaVersion,
                size,
                digest,
                DateTimeOffset.UtcNow,
                kind,
                "PASS");

            // Metadata is safe to include in a diagnostic package; the
            // database bytes and their potentially sensitive contents are not.
            File.WriteAllText(
                backupPath + ".manifest.json",
                JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            return result;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); }
                catch (IOException) { /* Preserve the original exception. */ }
            }
        }
    }
}

public sealed record VerifiedDatabaseBackup(
    string Path,
    int SchemaVersion,
    long SizeBytes,
    string Sha256,
    DateTimeOffset VerifiedUtc,
    string Kind,
    string IntegrityStatus);

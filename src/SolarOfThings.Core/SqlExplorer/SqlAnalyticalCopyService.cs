using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Creates an explicit, consistent SQLite-only ANALYTICAL copy for external
/// read-only tools. Never an app-recognized full backup or restore source.
/// The source is opened READ ONLY; output stays unlisted until verified.
/// </summary>
public sealed class SqlAnalyticalCopyService
{
    private readonly string _source;
    private readonly string _dataDirectory;
    private readonly string _backupDirectory;

    public SqlAnalyticalCopyService(
        string sourceDatabasePath, string dataDirectory, string backupDirectory)
    {
        _source = Path.GetFullPath(sourceDatabasePath);
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _backupDirectory = Path.GetFullPath(backupDirectory);
    }

    public Task<SqlAnalyticalCopyReceipt> CreateAsync(
        string selectedTargetPath, CancellationToken cancellation = default)
    {
        // File chooser/confirmation are UI-owned. This backend NEVER picks
        // an output folder, schedules an automatic job or overwrites files.
        var target = ValidateTarget(selectedTargetPath);
        cancellation.ThrowIfCancellationRequested();
        if (!File.Exists(_source))
            throw new FileNotFoundException("Active source SQLite file is missing.", _source);
        if (File.Exists(target))
            throw new IOException("An existing file cannot be overwritten.");
        return Task.Run(() => CreateCore(target, cancellation), cancellation);
    }

    private string ValidateTarget(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            throw new ArgumentException("Choose an explicit output .sqlite file.", nameof(candidate));
        var target = Path.GetFullPath(candidate);
        if (!Path.GetExtension(target).Equals(".sqlite",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The analytical copy must use .sqlite extension.");
        if (string.Equals(target, _source, StringComparison.OrdinalIgnoreCase) ||
            IsUnder(target, _dataDirectory) || IsUnder(target, _backupDirectory))
            throw new InvalidOperationException(
                "Analytical copies must stay outside the application's active Data and backup folders.");
        var folder = Path.GetDirectoryName(target)!;
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException("The selected output folder is unavailable.");
        // Conservative symlink/junction policy: deny uncertain redirected
        // locations instead of accidentally writing into active Data.
        for (var directory = new DirectoryInfo(folder);
             directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "Analytical-copy destination cannot include a redirected directory.");
        return target;
    }

    private static bool IsUnder(string path, string directory)
    {
        var prefix = Path.TrimEndingDirectorySeparator(directory)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private SqlAnalyticalCopyReceipt CreateCore(string target, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        // A GUID-scoped candidate in the *same folder* permits atomic final
        // rename. No stage file can be mistaken for a .sqlite recovery point.
        var stage = target + "." + Guid.NewGuid().ToString("N") + ".inprogress";
        try
        {
            using (var source = Open(_source, SqliteOpenMode.ReadOnly))
            using (var destination = Open(stage, SqliteOpenMode.ReadWriteCreate))
            {
                // Native SQLite online backup, NOT File.Copy on a WAL database.
                // This call is synchronous within a thread pool worker. Once
                // started it is NOT cooperatively cancellable, so the UI
                // must never imply instant cancellation of this stage.
                source.BackupDatabase(destination);
            }
            cancellation.ThrowIfCancellationRequested();
            int schema;
            using (var verified = Open(stage, SqliteOpenMode.ReadOnly))
            {
                using var integrity = verified.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(Convert.ToString(integrity.ExecuteScalar()),
                        "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Analytical SQLite copy integrity_check failed.");
                using var relationships = verified.CreateCommand();
                relationships.CommandText = "PRAGMA foreign_key_check;";
                using var invalid = relationships.ExecuteReader();
                if (invalid.Read())
                    throw new InvalidDataException("Analytical SQLite copy contains invalid foreign keys.");
                using var version = verified.CreateCommand();
                version.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
                schema = Convert.ToInt32(version.ExecuteScalar());
                if (schema < 1 || schema > SqliteDatabase.CurrentSchemaVersion)
                    throw new InvalidDataException(
                        "Analytical SQLite copy schema is incompatible.");
            }
            cancellation.ThrowIfCancellationRequested();
            long size;
            string digest;
            using (var file = new FileStream(stage, FileMode.Open,
                       FileAccess.Read, FileShare.Read))
            {
                size = file.Length;
                digest = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
            }
            cancellation.ThrowIfCancellationRequested();
            // Atomic no-overwrite publication: if someone created target
            // while we were backing up, leave their file intact.
            File.Move(stage, target, overwrite: false);
            return new SqlAnalyticalCopyReceipt(target, schema, size, digest,
                DateTimeOffset.UtcNow,
                "ANALYTICAL_SQLITE_ONLY_NOT_A_COMPLETE_BACKUP");
        }
        finally
        {
            // Delete only this operation's GUID candidate and its sidecars.
            // Never touch target, source, WAL or any other backup.
            foreach (var temp in new[] { stage, stage + "-wal",
                         stage + "-shm", stage + "-journal" })
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static SqliteConnection Open(string file, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = file, Mode = mode,
            Pooling = false, Cache = SqliteCacheMode.Private
        }.ToString());
        connection.Open();
        return connection;
    }
}

public sealed record SqlAnalyticalCopyReceipt(string Path, int SchemaVersion,
    long SizeBytes, string Sha256, DateTimeOffset CreatedUtc, string Classification);

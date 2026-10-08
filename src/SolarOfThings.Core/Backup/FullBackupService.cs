using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Writes a complete, verifiable package without reading live SQLite files directly.
/// No restore, active database replacement or automated deletion is implemented here.
/// </summary>
public sealed class FullBackupService
{
    private const string ArchivePrefix = "SolarEnergyMonitor-complete-";
    private const string ArchiveSuffix = ".zip";
    private readonly SqliteDatabase _database;
    private readonly AppPaths _paths;
    private readonly object _operationLock = new();

    public FullBackupService(SqliteDatabase database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    public CompleteBackupResult Create(string appVersion, string buildNumber, string revision)
    {
        lock (_operationLock)
            return CreateCore(appVersion, buildNumber, revision);
    }

    private CompleteBackupResult CreateCore(string appVersion, string buildNumber, string revision)
    {
        if (!File.Exists(_database.DatabasePath))
            throw new FileNotFoundException("No active database exists.", _database.DatabasePath);

        Directory.CreateDirectory(_paths.BackupDirectory);
        var id = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
                 Guid.NewGuid().ToString("N");
        var package = Path.Combine(_paths.BackupDirectory, ArchivePrefix + id + ArchiveSuffix);
        var pending = package + ".inprogress";
        var stage = Path.Combine(_paths.BackupDirectory, ".complete-staging-" + id);
        Directory.CreateDirectory(stage);
        var dbCopy = Path.Combine(stage, "energy.db");
        try
        {
            // SQLite native backup captures all committed WAL changes as one coherent DB.
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _database.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString()))
            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbCopy, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
            }.ToString()))
            {
                source.Open();
                target.Open();
                source.BackupDatabase(target);
            }

            int schema;
            using (var verified = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbCopy, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString()))
            {
                verified.Open();
                using var check = verified.CreateCommand();
                check.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(Convert.ToString(check.ExecuteScalar()), "ok", StringComparison.Ordinal))
                    throw new InvalidDataException("SQLite snapshot integrity_check failed.");
                check.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
                schema = Convert.ToInt32(check.ExecuteScalar());
                if (schema <= 0 || schema > SqliteDatabase.CurrentSchemaVersion)
                    throw new InvalidDataException("Unsupported database schema version.");
            }

            var files = new List<CompleteBackupEntry>();
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
            {
                AddFile(archive, dbCopy, "database/energy.db", files);
                AddDirectory(archive, _paths.TariffDirectory, "documents/Tariffs/", files);
                AddDirectory(archive, _paths.UtilityBillDirectory, "documents/Bills/", files);

                // Portable app settings and report presets already live inside SQLite.
                // DPAPI secrets, logs and backup folders intentionally remain excluded.
                var manifest = new CompleteBackupManifest(
                    1, appVersion, buildNumber, revision, schema, DateTimeOffset.UtcNow,
                    "SQLite native snapshot + Bills + Tariffs; secrets excluded",
                    files.ToArray());
                var entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var json = entry.Open();
                JsonSerializer.Serialize(json, manifest, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }

            // Verify the finalized zip's actual bytes against the manifest before publishing it.
            var inspected = VerifyArchive(pending);
            if (inspected.SchemaVersion != schema || inspected.Files.Count != files.Count)
                throw new InvalidDataException("Package verification disagrees with source snapshot.");

            var size = new FileInfo(pending).Length;
            var digest = HashFile(pending);
            File.Move(pending, package);
            return new CompleteBackupResult(package, size, digest, inspected.CreatedUtc,
                inspected.SchemaVersion, inspected.Files.Count, "PASS");
        }
        finally
        {
            // Never remove or replace existing published backups on error.
            if (File.Exists(pending))
                TryRemoveFile(pending);
            if (Directory.Exists(stage))
            {
                try { Directory.Delete(stage, recursive: true); }
                catch (IOException) { /* staging is distinguishable from published packages */ }
                catch (UnauthorizedAccessException) { /* preserve failure for diagnosis */ }
            }
        }
    }

    public IReadOnlyList<CompleteBackupResult> ListLocal()
    {
        if (!Directory.Exists(_paths.BackupDirectory))
            return [];
        return Directory.EnumerateFiles(
                _paths.BackupDirectory, ArchivePrefix + "*" + ArchiveSuffix, SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => new CompleteBackupResult(info.FullName, info.Length, "",
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), 0, 0, "NOT_RECHECKED"))
            .ToArray();
    }

    public CompleteBackupResult VerifyLocal(string path)
    {
        var full = ValidateManagedLocalPath(path);
        var manifest = VerifyArchive(full);
        return new CompleteBackupResult(full, new FileInfo(full).Length, HashFile(full),
            manifest.CreatedUtc, manifest.SchemaVersion, manifest.Files.Count, "PASS");
    }

    public void DeleteSelectedLocal(string path)
    {
        var full = ValidateManagedLocalPath(path);
        var all = ListLocal();
        if (all.Count <= 1)
            throw new InvalidOperationException("Cannot delete the last available complete backup.");
        // Do not pretend that any local file is valid just because it has the right name.
        if (!all.Any(item => !string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase) &&
                             TryVerifyArchive(item.Path)))
            throw new InvalidOperationException("Verify another complete backup before deleting this copy.");
        File.Delete(full);
    }

    private bool TryVerifyArchive(string path)
    {
        try { VerifyArchive(path); return true; }
        catch (IOException) { return false; }
        catch (InvalidDataException) { return false; }
        catch (JsonException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private string ValidateManagedLocalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(full);
        if (!string.Equals(parent, Path.GetFullPath(_paths.BackupDirectory),
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith(ArchivePrefix, StringComparison.Ordinal) ||
            !full.EndsWith(ArchiveSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File is not a recognized local complete backup.");
        return full;
    }

    private static void AddDirectory(ZipArchive zip, string root, string target, List<CompleteBackupEntry> files)
    {
        if (!Directory.Exists(root))
            return;
        var rootFull = Path.GetFullPath(root);
        foreach (var source in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked files must not be packaged: " + source);
            var relative = Path.GetRelativePath(rootFull, source).Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || relative.Contains("/../", StringComparison.Ordinal))
                throw new InvalidDataException("Unsafe source path.");
            AddFile(zip, source, target + relative, files);
        }
    }

    private static void AddFile(ZipArchive zip, string source, string destination,
        List<CompleteBackupEntry> manifest)
    {
        var info = new FileInfo(source);
        var sizeBefore = info.Length;
        var writeBefore = info.LastWriteTimeUtc;
        var entry = zip.CreateEntry(destination, CompressionLevel.Fastest);
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destStream = entry.Open();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        long copied = 0;
        int read;
        while ((read = sourceStream.Read(buffer, 0, buffer.Length)) != 0)
        {
            destStream.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
            copied += read;
        }
        info.Refresh();
        if (copied != sizeBefore || info.Length != sizeBefore || info.LastWriteTimeUtc != writeBefore)
            throw new IOException("A source document changed while being backed up: " + destination);
        manifest.Add(new CompleteBackupEntry(destination, copied,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
    }

    public static CompleteBackupManifest VerifyArchive(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var manifestEntry = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException("Missing backup manifest.");
        CompleteBackupManifest manifest;
        using (var stream = manifestEntry.Open())
            manifest = JsonSerializer.Deserialize<CompleteBackupManifest>(stream)
                ?? throw new InvalidDataException("Unreadable backup manifest.");
        if (manifest.FormatVersion != 1 || manifest.SchemaVersion <= 0 ||
            manifest.Files.Count == 0 || manifest.Files.Count > 100_000)
            throw new InvalidDataException("Unsupported or incomplete package metadata.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in manifest.Files)
        {
            if (!names.Add(item.RelativePath) ||
                item.RelativePath.StartsWith("/", StringComparison.Ordinal) ||
                item.RelativePath.Contains("..", StringComparison.Ordinal) ||
                item.Size < 0)
                throw new InvalidDataException("Invalid package file inventory.");
            var entry = zip.GetEntry(item.RelativePath)
                ?? throw new InvalidDataException("Missing file: " + item.RelativePath);
            if (entry.Length != item.Size)
                throw new InvalidDataException("File size mismatch: " + item.RelativePath);
            using var entryStream = entry.Open();
            var actual = Convert.ToHexString(SHA256.HashData(entryStream)).ToLowerInvariant();
            if (!string.Equals(actual, item.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("File checksum mismatch: " + item.RelativePath);
        }
        if (!names.Contains("database/energy.db") || zip.Entries.Count != names.Count + 1)
            throw new InvalidDataException("Invalid or incomplete backup archive.");
        return manifest;
    }

    private static string HashFile(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void TryRemoveFile(string file)
    {
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed record CompleteBackupEntry(string RelativePath, long Size, string Sha256);
public sealed record CompleteBackupManifest(int FormatVersion, string AppVersion,
    string BuildNumber, string SourceRevision, int SchemaVersion,
    DateTimeOffset CreatedUtc, string Contents, IReadOnlyList<CompleteBackupEntry> Files);
public sealed record CompleteBackupResult(string Path, long SizeBytes,
    string Sha256, DateTimeOffset CreatedUtc, int SchemaVersion,
    int FileCount, string IntegrityStatus);

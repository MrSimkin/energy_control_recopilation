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
                // A healthy ZIP with missing original documents is NOT a
                // complete recovery package. Validate DB references now.
                ValidateReferencedDocuments(dbCopy, files);

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
        // Historical API retained for callers, but no longer bypasses the
        // physical inventory's stale-selection and verified-last-copy guards.
        // Only other LOCAL complete backups qualify for this local-only action.
        var full = ValidateManagedLocalPath(path);
        var inventory = new CompleteBackupInventoryService(_paths);
        var selected = inventory.List(null).Copies.SingleOrDefault(copy =>
            copy.Location == "LOCAL" && copy.Kind == "COMPLETE" &&
            string.Equals(copy.Path, full, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
            throw new InvalidOperationException(
                "No currently listed complete backup matches the selected local file.");
        inventory.DeleteOne(selected, configuredSecondary: null);
    }

    /// <summary>
    /// Creates a separately verified mirror only after the source archive has passed
    /// full manifest validation. Never copies active SQLite or overwrites destination.
    /// </summary>
    public CompleteBackupResult CopyVerifiedToSecondary(string localPackage, string secondaryFolder)
    {
        var source = ValidateManagedLocalPath(localPackage);
        var verified = VerifyLocal(source);
        // Do not mirror onto application Data/Backups or through junctions.
        var targetDirectory = BackupDestinationPolicy.Validate(_paths, secondaryFolder);
        Directory.CreateDirectory(targetDirectory);
        // Re-check after directory creation in case a stale/offline drive
        // appears as a redirect; never follow a secondary junction.
        BackupDestinationPolicy.Validate(_paths, targetDirectory);
        var destination = Path.Combine(targetDirectory, Path.GetFileName(source));
        if (File.Exists(destination))
        {
            if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "Refusing secondary backup at a symbolic-link destination.");
            if (!string.Equals(HashFile(destination), verified.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("A different backup file already exists at the secondary destination.");
            // Already copied and byte-identical; do not overwrite.
            return verified with { Path = destination };
        }
        var temporary = destination + ".inprogress";
        var ownsTemporary = false;
        try
        {
            // Clean up only a temp file CREATED by this attempt. A pre-existing
            // .inprogress path must never be overwritten or deleted.
            using (var input = File.OpenRead(source))
            using (var output = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                // A disconnected/full destination can fail during CopyTo.
                input.CopyTo(output);
            }

            if (!string.Equals(HashFile(temporary), verified.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Secondary copy SHA-256 does not match local verified backup.");
            VerifyArchive(temporary);
            // A disconnected destination must never become a redirect just
            // before publication; re-check folder and no-overwrite intent.
            BackupDestinationPolicy.Validate(_paths, targetDirectory);
            if (File.Exists(destination))
                throw new IOException("Secondary destination appeared during copy; refusing overwrite.");
            File.Move(temporary, destination);
            return verified with { Path = destination };
        }
        finally
        {
            if (ownsTemporary && File.Exists(temporary))
                TryRemoveFile(temporary);
        }
    }

    private string ValidateManagedLocalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(full);
        if (!string.Equals(parent, Path.GetFullPath(_paths.BackupDirectory),
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith(ArchivePrefix, StringComparison.Ordinal) ||
            !full.EndsWith(ArchiveSuffix, StringComparison.OrdinalIgnoreCase) ||
            (File.Exists(full) &&
             (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) ||
            (Directory.Exists(parent) &&
             (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0))
            throw new InvalidOperationException(
                "File is not a safe regular local complete backup.");
        return full;
    }

    private static void AddDirectory(ZipArchive zip, string root, string target,
        List<CompleteBackupEntry> files)
    {
        if (!Directory.Exists(root))
            return;
        var rootFull = Path.GetFullPath(root);
        var pending = new Stack<string>();
        pending.Push(rootFull);
        while (pending.Count != 0)
        {
            var dir = pending.Pop();
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked directories are not permitted in backups.");
            foreach (var child in Directory.EnumerateFileSystemEntries(dir).OrderBy(p => p, StringComparer.Ordinal))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked files or folders are not permitted in backups.");
                if (Directory.Exists(child))
                {
                    pending.Push(child);
                    continue;
                }
                var relative = Path.GetRelativePath(rootFull, child).Replace('\\', '/');
                if (relative.StartsWith("../", StringComparison.Ordinal) ||
                    relative.Contains("/../", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsafe source path.");
                AddFile(zip, child, target + relative, files);
            }
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

    private void ValidateReferencedDocuments(string databaseCopy,
        IReadOnlyList<CompleteBackupEntry> archived)
    {
        var byPath = archived.ToDictionary(
            item => item.RelativePath, StringComparer.OrdinalIgnoreCase);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databaseCopy,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        foreach (var (table, parent, archivePrefix) in new[]
        {
            ("utility_bill_document", _paths.UtilityBillDirectory, "documents/Bills/"),
            ("tariff_publication", _paths.TariffDirectory, "documents/Tariffs/")
        })
        {
            using var sql = connection.CreateCommand();
            sql.CommandText = $"""
                SELECT local_pdf_path, content_sha256
                  FROM {table}
                 WHERE local_pdf_path IS NOT NULL AND TRIM(local_pdf_path) <> '';
                """;
            using var rows = sql.ExecuteReader();
            while (rows.Read())
            {
                var raw = rows.GetString(0);
                var sha = rows.IsDBNull(1) ? null : rows.GetString(1);
                if (!Path.IsPathFullyQualified(raw))
                    throw new InvalidDataException("Database reference is not an absolute document path.");
                var relative = Path.GetRelativePath(Path.GetFullPath(parent),
                    Path.GetFullPath(raw)).Replace('\\', '/');
                if (relative == "." || relative == ".." ||
                    relative.StartsWith("../", StringComparison.Ordinal) ||
                    relative.Contains("/../", StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "A referenced source document lives outside its managed folder.");
                if (!byPath.TryGetValue(archivePrefix + relative, out var included))
                    throw new InvalidDataException(
                        "Missing referenced original document in complete package: " +
                        archivePrefix + relative);
                if (!string.IsNullOrWhiteSpace(sha) &&
                    !string.Equals(included.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Referenced document checksum does not match stored original.");
            }
        }
    }

    /// <summary>
    /// Validates package structure, all file digests AND actual SQLite
    /// schema/integrity in a disposable temporary database. Does not restore
    /// or modify the user's active database or source ZIP.
    /// </summary>
    public static CompleteBackupManifest VerifyArchive(string path)
    {
        var disposable = Path.Combine(Path.GetTempPath(),
            "SolarEnergyMonitor-verify-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var manifestEntry = zip.GetEntry("manifest.json")
                ?? throw new InvalidDataException("Missing backup manifest.");
            if (manifestEntry.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("Unreasonably large backup manifest.");
            CompleteBackupManifest manifest;
            using (var stream = manifestEntry.Open())
                manifest = JsonSerializer.Deserialize<CompleteBackupManifest>(stream)
                    ?? throw new InvalidDataException("Unreadable backup manifest.");
            if (manifest.FormatVersion != 1 || manifest.SchemaVersion <= 0 ||
                manifest.SchemaVersion > SqliteDatabase.CurrentSchemaVersion ||
                manifest.Files is null || manifest.Files.Count is < 1 or > 100_000 ||
                string.IsNullOrWhiteSpace(manifest.AppVersion) ||
                string.IsNullOrWhiteSpace(manifest.BuildNumber) ||
                string.IsNullOrWhiteSpace(manifest.SourceRevision))
                throw new InvalidDataException("Unsupported or incomplete backup metadata.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                if (!names.Add(entry.FullName) ||
                    !IsSafeZipPath(entry.FullName) ||
                    !IsAllowedCompleteBackupPath(entry.FullName) ||
                    !HasRegularArchiveEntryType(entry))
                    throw new InvalidDataException("Unsafe, non-file or duplicate ZIP entry.");
            }
            if (names.Count != manifest.Files.Count + 1 ||
                !names.Contains("manifest.json"))
                throw new InvalidDataException("Incomplete or extra ZIP content.");
            var inventoried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in manifest.Files)
            {
                if (item is null || !IsSafeZipPath(item.RelativePath) ||
                    !IsAllowedCompleteBackupPath(item.RelativePath) ||
                    !inventoried.Add(item.RelativePath) || item.Size < 0 ||
                    string.IsNullOrWhiteSpace(item.Sha256) || item.Sha256.Length != 64 ||
                    !item.Sha256.All(Uri.IsHexDigit))
                    throw new InvalidDataException("Unsafe or invalid backup inventory.");
                var entry = zip.GetEntry(item.RelativePath)
                    ?? throw new InvalidDataException("Missing file in package.");
                if (entry.Length != item.Size)
                    throw new InvalidDataException("Backup file size mismatch.");
                using var bytes = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1024 * 1024];
                var isDatabase = item.RelativePath == "database/energy.db";
                FileStream? destination = isDatabase
                    ? new FileStream(disposable, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None)
                    : null;
                try
                {
                    long total = 0;
                    int read;
                    while ((read = bytes.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        checked { total += read; }
                        if (total > item.Size)
                            throw new InvalidDataException("ZIP entry exceeds its manifest size.");
                        hash.AppendData(buffer, 0, read);
                        destination?.Write(buffer, 0, read);
                    }
                    if (total != item.Size ||
                        !string.Equals(Convert.ToHexString(hash.GetHashAndReset()),
                            item.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Package checksum verification failed.");
                }
                finally
                {
                    destination?.Dispose();
                }
            }
            if (!inventoried.Contains("database/energy.db") ||
                !File.Exists(disposable))
                throw new InvalidDataException("Package lacks SQLite snapshot.");

            using var verified = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = disposable, Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            verified.Open();
            using var command = verified.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok",
                    StringComparison.Ordinal))
                throw new InvalidDataException("Embedded SQLite integrity_check failed.");
            command.CommandText = "PRAGMA foreign_key_check;";
            using (var fk = command.ExecuteReader())
                if (fk.Read())
                    throw new InvalidDataException("Embedded SQLite foreign key check failed.");
            command.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
            if (Convert.ToInt32(command.ExecuteScalar()) != manifest.SchemaVersion)
                throw new InvalidDataException("SQLite schema differs from package manifest.");
            // Hashes must also satisfy the snapshot's document references.
            // ZIP hashes alone cannot detect a deliberately revised manifest
            // that omits an original bill or tariff still linked in SQLite.
            if (manifest.SchemaVersion == SqliteDatabase.CurrentSchemaVersion)
                VerifyEmbeddedDocumentHashes(verified, manifest.Files);
            return manifest;
        }
        finally
        {
            if (File.Exists(disposable))
                TryRemoveFile(disposable);
        }
    }

    private static void VerifyEmbeddedDocumentHashes(
        SqliteConnection database, IReadOnlyList<CompleteBackupEntry> files)
    {
        foreach (var (table, archivePrefix) in new[]
        {
            ("utility_bill_document", "documents/Bills/"),
            ("tariff_publication", "documents/Tariffs/")
        })
        {
            var available = files
                .Where(item => item.RelativePath.StartsWith(
                    archivePrefix, StringComparison.Ordinal))
                .Select(item => item.Sha256)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            using var query = database.CreateCommand();
            query.CommandText = $"""
                SELECT content_sha256 FROM {table}
                 WHERE local_pdf_path IS NOT NULL
                   AND TRIM(local_pdf_path) <> ''
                   AND content_sha256 IS NOT NULL
                   AND TRIM(content_sha256) <> '';
                """;
            using var rows = query.ExecuteReader();
            while (rows.Read())
            {
                if (!available.Contains(rows.GetString(0)))
                    throw new InvalidDataException(
                        "Snapshot references an original document missing from its backup category.");
            }
        }
    }

    // On Unix-originated ZIPs, upper 16 external-attribute bits carry the
    // POSIX file mode. Only regular files (0x8000) or absent type metadata
    // are acceptable; symbolic links (0xA000), directories (0x4000) and
    // devices must never be interpreted as recoverable backup documents.
    // Windows-created archives commonly omit these high mode bits.
    private static bool HasRegularArchiveEntryType(ZipArchiveEntry entry)
    {
        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        // Windows-produced ZIPs can also encode an NTFS reparse point or
        // directory in DOS attributes, with no POSIX file-type bits at all.
        var windowsFlags = (FileAttributes)(entry.ExternalAttributes & 0xFFFF);
        var invalidFlags = FileAttributes.ReparsePoint | FileAttributes.Directory |
                           FileAttributes.Device;
        return (unixType is 0 or 0x8000) &&
               (windowsFlags & invalidFlags) == 0;
    }

    // Format v1 is a closed archive: one database, one manifest and only
    // original bill/tariff documents. No executable, backup or settings files.
    private static bool IsAllowedCompleteBackupPath(string name) =>
        name == "manifest.json" || name == "database/energy.db" ||
        (name.StartsWith("documents/Bills/", StringComparison.Ordinal) &&
         name.Length > "documents/Bills/".Length) ||
        (name.StartsWith("documents/Tariffs/", StringComparison.Ordinal) &&
         name.Length > "documents/Tariffs/".Length);

    private static bool IsSafeZipPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') ||
            name.StartsWith("/", StringComparison.Ordinal) ||
            name.EndsWith("/", StringComparison.Ordinal) ||
            name.Contains(':') || name.Contains('\0'))
            return false;
        return name.Split('/').All(IsPortableFileNameSegment);
    }

    private static bool IsPortableFileNameSegment(string segment)
    {
        // ZIP entries may originate on another OS. Windows strips terminal
        // dots/spaces and reserves device names, even with extensions.
        if (segment.Length is 0 or > 255 || segment is "." or ".." ||
            segment.EndsWith('.') || segment.EndsWith(' ') ||
            segment.Any(ch => char.IsControl(ch) ||
                ch is '<' or '>' or '"' or '|' or '?' or '*'))
            return false;
        var stem = segment.Split('.')[0].TrimEnd(' ');
        var upper = stem.ToUpperInvariant();
        if (upper is "CON" or "PRN" or "AUX" or "NUL" or
            "CONIN$" or "CONOUT$")
            return false;
        // Windows also treats the superscript one/two/three characters
        // as digits in the legacy COM and LPT device namespaces.
        if (upper.Length == 4 &&
            (upper.StartsWith("COM", StringComparison.Ordinal) ||
             upper.StartsWith("LPT", StringComparison.Ordinal)) &&
            (upper[3] is >= '1' and <= '9' or '¹' or '²' or '³'))
            return false;
        return true;
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

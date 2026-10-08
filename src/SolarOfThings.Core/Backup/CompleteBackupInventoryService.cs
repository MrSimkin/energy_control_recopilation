using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Enumerates physical complete-backup copies without treating a file name as
/// proof of integrity. A delete is guarded by re-verifying at least one OTHER
/// available complete package; it never cascades to another destination.
/// </summary>
public sealed class CompleteBackupInventoryService
{
    private const string CompletePrefix = "SolarEnergyMonitor-complete-";
    private readonly AppPaths _paths;

    public CompleteBackupInventoryService(AppPaths paths) => _paths = paths;

    public BackupInventorySnapshot List(string? configuredSecondary)
    {
        var copies = new List<PhysicalBackupCopy>();
        AddRecognized(copies, _paths.BackupDirectory, "LOCAL", true);
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(configuredSecondary))
        {
            try
            {
                var secondary = ValidateSecondaryDirectory(configuredSecondary);
                if (Directory.Exists(secondary))
                    AddRecognized(copies, secondary, "SECONDARY", false);
                else
                    warning = "SECONDARY_OFFLINE: configured destination is unavailable.";
            }
            catch (IOException ex) { warning = "SECONDARY_UNAVAILABLE: " + ex.Message; }
            catch (UnauthorizedAccessException ex) { warning = "SECONDARY_UNAVAILABLE: " + ex.Message; }
            catch (InvalidOperationException ex) { warning = "SECONDARY_INVALID: " + ex.Message; }
        }

        return new BackupInventorySnapshot(
            copies.OrderByDescending(c => c.ModifiedUtc).ThenBy(c => c.Location).ToArray(),
            warning);
    }

    public CompleteBackupResult Verify(PhysicalBackupCopy copy, string? configuredSecondary)
    {
        var path = ValidateCopy(copy, configuredSecondary);
        if (copy.Kind != "COMPLETE")
            throw new InvalidOperationException("Legacy SQLite-only snapshots are not complete recovery packages.");
        var manifest = FullBackupService.VerifyArchive(path);
        using var source = File.OpenRead(path);
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source))
            .ToLowerInvariant();
        return new CompleteBackupResult(path, new FileInfo(path).Length, digest,
            manifest.CreatedUtc, manifest.SchemaVersion, manifest.Files.Count, "PASS");
    }

    /// <summary>
    /// Deletes exactly one user-selected physical ZIP, never a mirror or active data.
    /// It fails closed when all OTHER complete copies are missing, offline or corrupt.
    /// </summary>
    public void DeleteOne(PhysicalBackupCopy selected, string? configuredSecondary)
    {
        var path = ValidateCopy(selected, configuredSecondary);
        if (selected.Kind != "COMPLETE")
            throw new InvalidOperationException("Legacy backups are excluded from complete-backup deletion.");
        if (!File.Exists(path))
            throw new FileNotFoundException("The selected backup is no longer available.", path);

        var otherCopies = List(configuredSecondary).Copies
            .Where(c => c.Kind == "COMPLETE" &&
                !string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var anotherValidCopyExists = false;
        foreach (var other in otherCopies)
        {
            try
            {
                Verify(other, configuredSecondary);
                anotherValidCopyExists = true;
                break;
            }
            catch (IOException) { }
            catch (InvalidDataException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Text.Json.JsonException) { }
            catch (InvalidOperationException) { }
        }
        if (!anotherValidCopyExists)
            throw new InvalidOperationException(
                "Deletion blocked: no OTHER currently available, verified complete backup.");

        // Revalidate target just before the only destructive filesystem action.
        if (!string.Equals(ValidateCopy(selected, configuredSecondary), path,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected backup path changed.");
        File.Delete(path);
    }

    private string ValidateCopy(PhysicalBackupCopy selected, string? configuredSecondary)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var root = selected.Location switch
        {
            "LOCAL" => Path.GetFullPath(_paths.BackupDirectory),
            "SECONDARY" when !string.IsNullOrWhiteSpace(configuredSecondary) =>
                ValidateSecondaryDirectory(configuredSecondary),
            _ => throw new InvalidOperationException("Unrecognized backup destination.")
        };

        var full = Path.GetFullPath(selected.Path);
        if (!string.Equals(Path.GetDirectoryName(full), root,
                StringComparison.OrdinalIgnoreCase) ||
            !IsRecognizedCompleteFile(Path.GetFileName(full)) ||
            (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) ||
            (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0))
            throw new InvalidOperationException("The chosen path is not a safe recognized backup.");
        return full;
    }

    private string ValidateSecondaryDirectory(string destination)
    {
        var full = Path.GetFullPath(destination);
        var local = Path.GetFullPath(_paths.BackupDirectory);
        if (string.Equals(full, local, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Secondary and local locations must differ.");
        if (Directory.Exists(full) &&
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Linked backup directories are not supported.");
        return full;
    }

    private static void AddRecognized(List<PhysicalBackupCopy> copies,
        string directory, string location, bool listLegacy)
    {
        if (!Directory.Exists(directory))
            return;
        foreach (var path in Directory.EnumerateFiles(directory, "*",
            SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(path);
            var isComplete = IsRecognizedCompleteFile(name);
            var legacy = listLegacy &&
                name.StartsWith("energy-", StringComparison.Ordinal) &&
                name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase);
            if (!isComplete && !legacy)
                continue;
            var info = new FileInfo(path);
            copies.Add(new PhysicalBackupCopy(info.FullName, name,
                location, isComplete ? "COMPLETE" : "LEGACY_SQLITE_ONLY",
                info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                "NOT_RECHECKED"));
        }
    }

    private static bool IsRecognizedCompleteFile(string name) =>
        name.StartsWith(CompletePrefix, StringComparison.Ordinal) &&
        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
        name.Length > CompletePrefix.Length + 4;
}

public sealed record PhysicalBackupCopy(
    string Path, string Name, string Location, string Kind,
    long SizeBytes, DateTimeOffset ModifiedUtc, string VerificationStatus);

public sealed record BackupInventorySnapshot(
    IReadOnlyList<PhysicalBackupCopy> Copies, string? SecondaryWarning);

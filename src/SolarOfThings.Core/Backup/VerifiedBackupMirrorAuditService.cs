using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Explicit read-only, on-demand proof whether one user-selected LOCAL
/// complete archive has an independently verified byte-identical SECONDARY
/// physical copy. It never copies, deletes, restores or opens active Data.
/// Inventory metadata alone is NEVER a successful mirror receipt.
/// </summary>
public sealed class VerifiedBackupMirrorAuditService
{
    private readonly AppPaths _paths;
    private readonly CompleteBackupInventoryService _inventory;

    public VerifiedBackupMirrorAuditService(AppPaths paths)
    {
        _paths = paths;
        _inventory = new CompleteBackupInventoryService(paths);
    }

    public VerifiedBackupMirrorAudit Inspect(
        PhysicalBackupCopy selected, string? secondaryFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        cancellationToken.ThrowIfCancellationRequested();
        if (selected.Kind != "COMPLETE" || selected.Location != "LOCAL")
            throw new InvalidOperationException(
                "A mirror audit starts with a selected LOCAL complete backup.");

        // Verify local bytes and embedded SQLite/documents first, never infer
        // success from an inventory row's old PASS or ZIP-shaped file name.
        var local = _inventory.VerifyDetails(selected, configuredSecondary: null);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(secondaryFolder))
            return Failure("SECONDARY_NOT_CONFIGURED", local, null,
                "No secondary backup destination is configured.");

        string destinationFolder;
        try { destinationFolder = BackupDestinationPolicy.Validate(_paths, secondaryFolder); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                  or InvalidOperationException)
        {
            return Failure("SECONDARY_INVALID", local, null,
                "Secondary destination is not a safe accessible directory.");
        }
        if (!Directory.Exists(destinationFolder))
            return Failure("SECONDARY_OFFLINE", local,
                Path.Combine(destinationFolder, selected.Name),
                "Secondary destination is unavailable; local backup remains verified.");

        var secondaryPath = Path.Combine(destinationFolder, selected.Name);
        var listing = _inventory.List(secondaryFolder);
        cancellationToken.ThrowIfCancellationRequested();
        if (listing.SecondaryWarning is not null)
            return Failure("SECONDARY_UNAVAILABLE", local, secondaryPath,
                "Secondary destination cannot currently be inventoried.");
        var candidate = listing.Copies.SingleOrDefault(x =>
            x.Location == "SECONDARY" && x.Kind == "COMPLETE" &&
            string.Equals(x.Name, selected.Name, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
            return Failure("SECONDARY_MISSING", local, secondaryPath,
                "No secondary physical ZIP with this snapshot name is present.");

        try
        {
            var remote = _inventory.VerifyDetails(candidate, secondaryFolder);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(remote.Summary.Sha256, local.Summary.Sha256,
                    StringComparison.OrdinalIgnoreCase) ||
                remote.Summary.SizeBytes != local.Summary.SizeBytes)
                return new VerifiedBackupMirrorAudit(
                    "SECONDARY_DIFFERENT", false, local.Summary.Path,
                    remote.Summary.Path, local.Summary.Sha256,
                    remote.Summary.Sha256, local.Manifest.BuildNumber,
                    "Both packages were independently checked, but the bytes differ. " +
                    "Do not overwrite a conflicting archive.");

            return new VerifiedBackupMirrorAudit(
                "TWO_EXACT_COPIES_VERIFIED", true, local.Summary.Path,
                remote.Summary.Path, local.Summary.Sha256, remote.Summary.Sha256,
                local.Manifest.BuildNumber,
                "Two distinct complete ZIP files passed independent verification " +
                "and match SHA-256 at inspection time. No restoration tested; " +
                "separate folders do not prove independent physical disks.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                  or InvalidDataException or InvalidOperationException
                                  or System.Text.Json.JsonException)
        {
            return Failure("SECONDARY_FAILED_VERIFICATION", local, secondaryPath,
                "The secondary file could not be independently verified. " +
                "Local verification remains valid; no file was changed.");
        }
    }

    private static VerifiedBackupMirrorAudit Failure(
        string status, VerifiedBackupInspection local, string? secondary,
        string detail) => new(status, false, local.Summary.Path, secondary,
            local.Summary.Sha256, null, local.Manifest.BuildNumber, detail);
}

public sealed record VerifiedBackupMirrorAudit(
    string Status, bool TwoExactCopiesVerified, string LocalPath,
    string? SecondaryPath, string LocalSha256, string? SecondarySha256,
    string SourceBuild, string SafetyExplanation);

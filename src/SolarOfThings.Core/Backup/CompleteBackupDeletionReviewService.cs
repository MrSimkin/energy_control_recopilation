using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Two-step, explicit user-reviewed deletion of ONE physical full-backup ZIP.
/// The preview verifies selected and independent survivor bytes before asking
/// for confirmation. Execution re-verifies BOTH receipts and delegates the
/// final last-available-copy gate to CompleteBackupInventoryService.
/// No automatic cleanup/retention and no directory-wide file deletion.
/// </summary>
public sealed class CompleteBackupDeletionReviewService
{
    private readonly CompleteBackupInventoryService _inventory;

    public CompleteBackupDeletionReviewService(AppPaths paths) =>
        _inventory = new CompleteBackupInventoryService(paths);

    public ReviewedBackupDeletion Prepare(
        PhysicalBackupCopy selected, string? secondary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        cancellationToken.ThrowIfCancellationRequested();
        if (selected.Kind != "COMPLETE")
            throw new InvalidOperationException("Only full-backup ZIPs are eligible.");
        var chosen = _inventory.VerifyDetails(selected, secondary);
        cancellationToken.ThrowIfCancellationRequested();
        var alternatives = _inventory.List(secondary).Copies
            .Where(x => x.Kind == "COMPLETE" &&
                !string.Equals(x.Path, selected.Path, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var alternative in alternatives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var other = _inventory.VerifyDetails(alternative, secondary);
                if (string.Equals(other.Summary.Path, chosen.Summary.Path,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(other.Manifest.AppVersion.Split('.')[0],
                        chosen.Manifest.AppVersion.Split('.')[0], StringComparison.Ordinal))
                    continue;
                return new ReviewedBackupDeletion(
                    selected, chosen.Summary.Sha256, secondary,
                    alternative, other.Summary.Sha256,
                    "REVIEW_REQUIRED_TWO_VERIFIED_PHYSICAL_PACKAGES");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidDataException) { }
            catch (InvalidOperationException) { }
            catch (System.Text.Json.JsonException) { }
        }
        throw new InvalidOperationException(
            "Deletion blocked: no OTHER available, independently verified complete backup.");
    }

    /// <summary>
    /// Caller must obtain a visible affirmative user confirmation AFTER
    /// Prepare returns. No such confirmation can be inferred by this class.
    /// </summary>
    public void DeleteAfterExplicitConfirmation(
        ReviewedBackupDeletion reviewed, bool userConfirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        if (!userConfirmed)
            throw new InvalidOperationException("Explicit backup deletion confirmation is required.");
        cancellationToken.ThrowIfCancellationRequested();
        if (reviewed.Status != "REVIEW_REQUIRED_TWO_VERIFIED_PHYSICAL_PACKAGES" ||
            reviewed.Selected.Kind != "COMPLETE" ||
            reviewed.Survivor.Kind != "COMPLETE" ||
            string.Equals(reviewed.Selected.Path, reviewed.Survivor.Path,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unrecognized backup deletion preview.");
        var chosen = _inventory.VerifyDetails(reviewed.Selected, reviewed.SecondaryFolder);
        cancellationToken.ThrowIfCancellationRequested();
        var alternative = _inventory.VerifyDetails(reviewed.Survivor, reviewed.SecondaryFolder);
        if (!string.Equals(chosen.Summary.Sha256, reviewed.SelectedSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(alternative.Summary.Sha256, reviewed.SurvivorSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backups changed since the deletion review.");
        cancellationToken.ThrowIfCancellationRequested();
        _inventory.DeleteOne(reviewed.Selected, reviewed.SecondaryFolder);
    }
}

public sealed record ReviewedBackupDeletion(
    PhysicalBackupCopy Selected, string SelectedSha256, string? SecondaryFolder,
    PhysicalBackupCopy Survivor, string SurvivorSha256, string Status);

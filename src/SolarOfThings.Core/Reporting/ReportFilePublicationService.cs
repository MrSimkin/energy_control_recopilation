namespace SolarOfThings.Core.Reporting;

/// <summary>
/// A report is first generated alongside its chosen destination. On failure
/// the previously saved destination is retained; only a finished and nonempty
/// stage may be published over it. This is not a backup or rollback service.
/// </summary>
public static class ReportFilePublicationService
{
    public static string CreateStagingPath(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var full = Path.GetFullPath(destinationPath);
        var extension = Path.GetExtension(full);
        if (!AllowedExtension(extension))
            throw new ArgumentException("Only PDF and XLSX reports can be published.", nameof(destinationPath));

        var directory = Path.GetDirectoryName(full)
            ?? throw new ArgumentException("Report destination has no folder.", nameof(destinationPath));
        return Path.Combine(directory, $".report-{Guid.NewGuid():N}.inprogress{extension}");
    }

    public static void Publish(string stagedPath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var staged = Path.GetFullPath(stagedPath);
        var destination = Path.GetFullPath(destinationPath);
        if (!AllowedExtension(Path.GetExtension(staged)) ||
            !string.Equals(Path.GetExtension(staged), Path.GetExtension(destination),
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(staged), Path.GetDirectoryName(destination),
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(staged).StartsWith(".report-", StringComparison.Ordinal) ||
            !Path.GetFileName(staged).Contains(".inprogress.", StringComparison.Ordinal))
            throw new InvalidOperationException("Staged report must be in its destination folder.");

        if (!File.Exists(staged) || new FileInfo(staged).Length == 0)
            throw new IOException("Report output is missing or empty.");

        // Same-directory rename: the old destination is not touched until
        // successfully rendered; the export remains explicitly user initiated.
        File.Move(staged, destination, overwrite: true);
    }

    public static void DiscardStaging(string? stagedPath)
    {
        if (!string.IsNullOrEmpty(stagedPath) && File.Exists(stagedPath))
            File.Delete(stagedPath);
    }

    private static bool AllowedExtension(string? extension) =>
        string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
}

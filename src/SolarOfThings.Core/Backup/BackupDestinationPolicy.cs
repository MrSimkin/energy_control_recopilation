using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Rejects secondary backup destinations that overlap active Data/Backups or
/// traverse filesystem links. A separate directory on the same disk is NOT
/// guaranteed to survive disk failure; users should prefer another device.
/// </summary>
public static class BackupDestinationPolicy
{
    public static string Validate(AppPaths paths, string destination)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (!Path.IsPathFullyQualified(destination))
            throw new InvalidOperationException(
                "A secondary backup folder must be an absolute path.");

        var full = Path.GetFullPath(destination);
        var data = Path.GetFullPath(paths.DataDirectory);
        var local = Path.GetFullPath(paths.BackupDirectory);

        if (Overlaps(full, data) || Overlaps(full, local))
            throw new InvalidOperationException(
                "Secondary backups cannot be inside or contain application Data/Backups.");

        // Inspect every existing ancestor, not just the final directory.
        // This avoids following junctions into active application data.
        for (string? cursor = full; cursor is not null;
             cursor = Path.GetDirectoryName(cursor))
        {
            if (Directory.Exists(cursor) || File.Exists(cursor))
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException(
                        "Linked directories and paths are not valid secondary locations.");
            }
        }
        if (File.Exists(full))
            throw new InvalidOperationException(
                "Secondary destination must be a directory.");
        return full;
    }

    private static bool Overlaps(string first, string second) =>
        SameOrChild(first, second) || SameOrChild(second, first);

    private static bool SameOrChild(string path, string ancestor)
    {
        var relative = Path.GetRelativePath(ancestor, path);
        return relative == "." ||
            (relative != ".." &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar,
                 StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.AltDirectorySeparatorChar,
                 StringComparison.Ordinal) &&
             !Path.IsPathRooted(relative));
    }
}

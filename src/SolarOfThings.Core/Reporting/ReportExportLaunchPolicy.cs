using System.IO;

namespace SolarOfThings.Core.Reporting;

/// <summary>
/// UI-only gate for opening an explicitly generated report from this session.
/// Does not authorize reading or executing arbitrary files from disk.
/// </summary>
public static class ReportExportLaunchPolicy
{
    public static bool CanOpen(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                return false;
            return File.Exists(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                   or PathTooLongException or IOException)
        {
            return false;
        }
    }
}

namespace SolarOfThings.Core.Infrastructure;

public sealed class AppPaths
{
    public const string ProductFolderName = "SolarEnergyMonitor";

    public AppPaths(string? rootOverride = null)
    {
        RootDirectory = rootOverride ?? ResolveDefaultRoot();
        DataDirectory = Path.Combine(RootDirectory, "Data");
        BackupDirectory = Path.Combine(RootDirectory, "Backups");
        LogDirectory = Path.Combine(RootDirectory, "Logs");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public string RootDirectory { get; }
    public string DataDirectory { get; }
    public string BackupDirectory { get; }
    public string LogDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "energy.db");

    private static string ResolveDefaultRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var portableMarker = Path.Combine(baseDirectory, "portable.mode");

        if (File.Exists(portableMarker))
        {
            return baseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        }

        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonData))
        {
            throw new InvalidOperationException("Windows common application-data directory is unavailable.");
        }

        return Path.Combine(commonData, ProductFolderName);
    }
}

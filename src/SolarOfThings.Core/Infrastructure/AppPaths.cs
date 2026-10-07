namespace SolarOfThings.Core.Infrastructure;

public sealed class AppPaths
{
    public const string ProductFolderName = "SolarEnergyMonitor";
    public const string DataDirectoryEnvironmentVariable =
        "SOLAR_ENERGY_MONITOR_DATA_DIR";
    public const string DataPathConfigFileName =
        "data-path.txt";
    public const string QaDefaultDataDirectory =
        @"D:\SolarEnergyMonitorTest\Data";

    public AppPaths(
        string? rootOverride = null,
        string? dataDirectoryOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(rootOverride))
        {
            RootDirectory =
                Path.GetFullPath(rootOverride);
            DataDirectory =
                Path.Combine(RootDirectory, "Data");
            BackupDirectory =
                Path.Combine(RootDirectory, "Backups");
            LogDirectory =
                Path.Combine(RootDirectory, "Logs");
        }
        else
        {
            DataDirectory =
                ResolveDefaultDataDirectory(
                    dataDirectoryOverride);
            RootDirectory =
                Directory.GetParent(DataDirectory)?.FullName ??
                DataDirectory;

            // During QA all persistent state is intentionally kept below the
            // shared Data directory so changing executable builds never
            // changes the database/log/backup location.
            BackupDirectory =
                Path.Combine(DataDirectory, "Backups");
            LogDirectory =
                Path.Combine(DataDirectory, "Logs");
        }

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);

        if (string.IsNullOrWhiteSpace(rootOverride))
        {
            TryBootstrapSharedQaData(
                DataDirectory);
        }

        TariffDirectory =
            Path.Combine(DataDirectory, "Tariffs");
        TariffEnelDirectory =
            Path.Combine(TariffDirectory, "Enel");
        TariffEnelIncomingDirectory =
            Path.Combine(
                TariffEnelDirectory,
                "_incoming");
        UtilityBillDirectory =
            Path.Combine(DataDirectory, "Bills");
        UtilityBillEnelDirectory =
            Path.Combine(UtilityBillDirectory, "Enel");

        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(TariffDirectory);
        Directory.CreateDirectory(TariffEnelDirectory);
        Directory.CreateDirectory(
            TariffEnelIncomingDirectory);
        Directory.CreateDirectory(UtilityBillDirectory);
        Directory.CreateDirectory(UtilityBillEnelDirectory);
    }

    public string RootDirectory { get; }
    public string DataDirectory { get; }
    public string BackupDirectory { get; }
    public string LogDirectory { get; }
    public string TariffDirectory { get; }
    public string TariffEnelDirectory { get; }
    public string TariffEnelIncomingDirectory { get; }
    public string UtilityBillDirectory { get; }
    public string UtilityBillEnelDirectory { get; }
    public string DatabasePath =>
        Path.Combine(DataDirectory, "energy.db");

    private static string ResolveDefaultDataDirectory(
        string? explicitOverride)
    {
        var candidate =
            NormalizeConfiguredPath(
                explicitOverride);

        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;

        candidate =
            NormalizeConfiguredPath(
                Environment.GetEnvironmentVariable(
                    DataDirectoryEnvironmentVariable));

        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;

        var configPath =
            Path.Combine(
                AppContext.BaseDirectory,
                DataPathConfigFileName);

        if (File.Exists(configPath))
        {
            var configured =
                File.ReadLines(configPath)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line =>
                        !string.IsNullOrWhiteSpace(line) &&
                        !line.StartsWith('#'));

            candidate =
                NormalizeConfiguredPath(
                    configured);

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                if (!Path.IsPathRooted(candidate))
                {
                    candidate =
                        Path.GetFullPath(
                            Path.Combine(
                                AppContext.BaseDirectory,
                                candidate));
                }

                return candidate;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            return QaDefaultDataDirectory;
        }

        var commonData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .CommonApplicationData);

        if (string.IsNullOrWhiteSpace(commonData))
        {
            throw new InvalidOperationException(
                "Application data directory is unavailable.");
        }

        return Path.Combine(
            commonData,
            ProductFolderName,
            "Data");
    }

    private static string? NormalizeConfiguredPath(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Environment.ExpandEnvironmentVariables(
            value.Trim()
                .Trim('"'));
    }

    private static void TryBootstrapSharedQaData(
        string targetDataDirectory)
    {
        var targetDatabase =
            Path.Combine(
                targetDataDirectory,
                "energy.db");

        if (File.Exists(targetDatabase))
            return;

        var executableDirectory =
            new DirectoryInfo(
                AppContext.BaseDirectory
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

        var parent =
            executableDirectory.Parent;

        if (parent is null ||
            !parent.Exists)
        {
            return;
        }

        var source =
            parent
                .EnumerateDirectories(
                    "SolarEnergyMonitor-Build-*-win-x64",
                    SearchOption.TopDirectoryOnly)
                .Where(directory =>
                    !string.Equals(
                        directory.FullName,
                        executableDirectory.FullName,
                        StringComparison.OrdinalIgnoreCase))
                .Select(directory => new
                {
                    Directory = directory,
                    Build = TryParseBuildNumber(
                        directory.Name),
                    Data = Path.Combine(
                        directory.FullName,
                        "Data")
                })
                .Where(item =>
                    item.Build.HasValue &&
                    File.Exists(
                        Path.Combine(
                            item.Data,
                            "energy.db")))
                .OrderByDescending(item =>
                    item.Build!.Value)
                .FirstOrDefault();

        if (source is null)
            return;

        CopyDirectory(
            source.Data,
            targetDataDirectory);
    }

    private static int? TryParseBuildNumber(
        string directoryName)
    {
        const string prefix =
            "SolarEnergyMonitor-Build-";
        const string suffix =
            "-win-x64";

        if (!directoryName.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase) ||
            !directoryName.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw =
            directoryName[
                prefix.Length..
                ^suffix.Length];

        return int.TryParse(
            raw,
            out var build)
            ? build
            : null;
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string targetDirectory)
    {
        Directory.CreateDirectory(
            targetDirectory);

        foreach (var sourceFile in
                 Directory.EnumerateFiles(
                     sourceDirectory))
        {
            var targetFile =
                Path.Combine(
                    targetDirectory,
                    Path.GetFileName(
                        sourceFile));

            if (!File.Exists(targetFile))
            {
                File.Copy(
                    sourceFile,
                    targetFile,
                    overwrite: false);
            }
        }

        foreach (var sourceChild in
                 Directory.EnumerateDirectories(
                     sourceDirectory))
        {
            CopyDirectory(
                sourceChild,
                Path.Combine(
                    targetDirectory,
                    Path.GetFileName(
                        sourceChild)));
        }
    }
}

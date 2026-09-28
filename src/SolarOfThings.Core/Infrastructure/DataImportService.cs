using System.Text.Json;

namespace SolarOfThings.Core.Infrastructure;

public sealed record DataImportProgress(
    int FilesCompleted,
    int TotalFiles,
    long BytesCompleted,
    long TotalBytes,
    string CurrentFile);

public sealed record DataImportApplyResult(
    bool Applied,
    string? BackupPath,
    string? Detail);

public sealed class DataImportService
{
    private const string PendingMarkerFileName = "pending-data-import.json";
    private const string StagingDirectoryName = "ImportStaging";
    private readonly AppPaths _paths;

    public DataImportService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task StageImportAsync(
        string selectedPath,
        IProgress<DataImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sourceData = ResolveSourceDataDirectory(selectedPath);
        var currentData = Path.GetFullPath(_paths.DataDirectory)
            .TrimEnd(Path.DirectorySeparatorChar);

        if (string.Equals(
                Path.GetFullPath(sourceData).TrimEnd(Path.DirectorySeparatorChar),
                currentData,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected installation is the current installation.");
        }

        var sourceDatabase = Path.Combine(sourceData, "energy.db");
        if (!File.Exists(sourceDatabase))
        {
            throw new InvalidOperationException(
                "The selected folder does not contain Data\\energy.db.");
        }

        var stagingRoot = Path.Combine(_paths.RootDirectory, StagingDirectoryName);
        var stagingData = Path.Combine(stagingRoot, "Data");

        if (Directory.Exists(stagingRoot))
            Directory.Delete(stagingRoot, recursive: true);

        Directory.CreateDirectory(stagingData);

        var files = Directory
            .EnumerateFiles(sourceData, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .ToArray();

        if (files.Length == 0)
            throw new InvalidOperationException("The selected Data folder is empty.");

        var totalBytes = files.Sum(file => file.Length);
        long copiedBytes = 0;
        var completed = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourceData, file.FullName);
            var destination = Path.Combine(stagingData, relative);
            var destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            await using var source = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                useAsync: true);
            await using var target = new FileStream(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);

            var buffer = new byte[1024 * 1024];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copiedBytes += read;
                progress?.Report(new DataImportProgress(
                    completed,
                    files.Length,
                    copiedBytes,
                    totalBytes,
                    relative));
            }

            completed++;
            progress?.Report(new DataImportProgress(
                completed,
                files.Length,
                copiedBytes,
                totalBytes,
                relative));
        }

        var stagedDatabase = Path.Combine(stagingData, "energy.db");
        if (!File.Exists(stagedDatabase) || new FileInfo(stagedDatabase).Length == 0)
            throw new InvalidOperationException("The staged database is missing or empty.");

        var marker = new PendingDataImport(
            stagingData,
            sourceData,
            DateTimeOffset.UtcNow);

        await File.WriteAllTextAsync(
            Path.Combine(_paths.RootDirectory, PendingMarkerFileName),
            JsonSerializer.Serialize(marker),
            cancellationToken);
    }

    public static DataImportApplyResult ApplyPendingImport(
        AppPaths paths,
        Action<string>? status = null)
    {
        var markerPath = Path.Combine(paths.RootDirectory, PendingMarkerFileName);
        if (!File.Exists(markerPath))
            return new DataImportApplyResult(false, null, null);

        PendingDataImport marker;
        try
        {
            marker = JsonSerializer.Deserialize<PendingDataImport>(
                File.ReadAllText(markerPath))
                ?? throw new InvalidOperationException("Pending import marker is invalid.");
        }
        catch (Exception ex)
        {
            return new DataImportApplyResult(
                false,
                null,
                $"Could not read pending import: {ex.Message}");
        }

        if (!Directory.Exists(marker.StagedDataDirectory) ||
            !File.Exists(Path.Combine(marker.StagedDataDirectory, "energy.db")))
        {
            return new DataImportApplyResult(
                false,
                null,
                "Pending import staging data is missing.");
        }

        var backupPath = Path.Combine(
            paths.BackupDirectory,
            $"Data-before-import-{DateTime.Now:yyyyMMdd-HHmmss}");

        try
        {
            status?.Invoke("Respaldando datos actuales...");
            if (Directory.Exists(paths.DataDirectory))
            {
                Directory.Move(paths.DataDirectory, backupPath);
            }

            status?.Invoke("Aplicando datos importados...");
            Directory.Move(marker.StagedDataDirectory, paths.DataDirectory);

            Directory.CreateDirectory(paths.TariffDirectory);

            var stagingRoot = Path.Combine(paths.RootDirectory, StagingDirectoryName);
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);

            File.Delete(markerPath);

            return new DataImportApplyResult(
                true,
                backupPath,
                $"Imported from {marker.SourceDataDirectory}");
        }
        catch (Exception ex)
        {
            try
            {
                if (!Directory.Exists(paths.DataDirectory) &&
                    Directory.Exists(backupPath))
                {
                    Directory.Move(backupPath, paths.DataDirectory);
                }
            }
            catch
            {
                // Preserve the original failure; recovery detail remains on disk.
            }

            return new DataImportApplyResult(
                false,
                Directory.Exists(backupPath) ? backupPath : null,
                $"Data import failed: {ex.Message}");
        }
    }

    private static string ResolveSourceDataDirectory(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            throw new ArgumentException("A source folder is required.", nameof(selectedPath));

        var full = Path.GetFullPath(selectedPath);

        if (File.Exists(Path.Combine(full, "energy.db")))
            return full;

        var nested = Path.Combine(full, "Data");
        if (File.Exists(Path.Combine(nested, "energy.db")))
            return nested;

        throw new InvalidOperationException(
            "Choose either the old installation folder or its Data folder.");
    }

    private sealed record PendingDataImport(
        string StagedDataDirectory,
        string SourceDataDirectory,
        DateTimeOffset StagedUtc);
}

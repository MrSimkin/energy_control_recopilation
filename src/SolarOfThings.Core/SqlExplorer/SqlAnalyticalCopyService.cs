using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Creates an explicit, consistent SQLite-only ANALYTICAL copy for external
/// read-only tools. Never an app-recognized full backup or restore source.
/// The source is opened READ ONLY; output stays unlisted until verified.
/// </summary>
public sealed class SqlAnalyticalCopyService
{
    private readonly string _source;
    private readonly string _dataDirectory;
    private readonly string _backupDirectory;

    public SqlAnalyticalCopyService(
        string sourceDatabasePath, string dataDirectory, string backupDirectory)
    {
        _source = Path.GetFullPath(sourceDatabasePath);
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _backupDirectory = Path.GetFullPath(backupDirectory);
    }

    public Task<SqlAnalyticalCopyReceipt> CreateAsync(
        string selectedTargetPath, CancellationToken cancellation = default,
        SqlIntegratedEnergyRequest? integratedEnergy = null)
    {
        // File chooser/confirmation are UI-owned. This backend NEVER picks
        // an output folder, schedules an automatic job or overwrites files.
        var target = ValidateTarget(selectedTargetPath);
        cancellation.ThrowIfCancellationRequested();
        if (!File.Exists(_source))
            throw new FileNotFoundException("Active source SQLite file is missing.", _source);
        if (File.Exists(target))
            throw new IOException("An existing file cannot be overwritten.");
        if (integratedEnergy is not null)
            ValidateEnergyRequest(integratedEnergy);
        return Task.Run(() => CreateCore(target, cancellation, integratedEnergy), cancellation);
    }

    private string ValidateTarget(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            throw new ArgumentException("Choose an explicit output .sqlite file.", nameof(candidate));
        var target = Path.GetFullPath(candidate);
        if (!Path.GetExtension(target).Equals(".sqlite",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The analytical copy must use .sqlite extension.");
        if (string.Equals(target, _source, StringComparison.OrdinalIgnoreCase) ||
            IsUnder(target, _dataDirectory) || IsUnder(target, _backupDirectory))
            throw new InvalidOperationException(
                "Analytical copies must stay outside the application's active Data and backup folders.");
        var folder = Path.GetDirectoryName(target)!;
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException("The selected output folder is unavailable.");
        // Conservative symlink/junction policy: deny uncertain redirected
        // locations instead of accidentally writing into active Data.
        for (var directory = new DirectoryInfo(folder);
             directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "Analytical-copy destination cannot include a redirected directory.");
        return target;
    }

    private static bool IsUnder(string path, string directory)
    {
        var prefix = Path.TrimEndingDirectorySeparator(directory)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private SqlAnalyticalCopyReceipt CreateCore(string target, CancellationToken cancellation,
        SqlIntegratedEnergyRequest? integratedEnergy)
    {
        cancellation.ThrowIfCancellationRequested();
        // A GUID-scoped candidate in the *same folder* permits atomic final
        // rename. No stage file can be mistaken for a .sqlite recovery point.
        var stage = target + "." + Guid.NewGuid().ToString("N") + ".inprogress";
        try
        {
            using (var source = Open(_source, SqliteOpenMode.ReadOnly))
            using (var destination = Open(stage, SqliteOpenMode.ReadWriteCreate))
            {
                // Native SQLite online backup, NOT File.Copy on a WAL database.
                // This call is synchronous within a thread pool worker. Once
                // started it is NOT cooperatively cancellable, so the UI
                // must never imply instant cancellation of this stage.
                source.BackupDatabase(destination);
            }
            cancellation.ThrowIfCancellationRequested();
            if (integratedEnergy is not null)
                MaterializeIntegratedEnergy(stage, integratedEnergy, cancellation);
            cancellation.ThrowIfCancellationRequested();
            int schema;
            using (var verified = Open(stage, SqliteOpenMode.ReadOnly))
            {
                using var integrity = verified.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(Convert.ToString(integrity.ExecuteScalar()),
                        "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Analytical SQLite copy integrity_check failed.");
                using var relationships = verified.CreateCommand();
                relationships.CommandText = "PRAGMA foreign_key_check;";
                using var invalid = relationships.ExecuteReader();
                if (invalid.Read())
                    throw new InvalidDataException("Analytical SQLite copy contains invalid foreign keys.");
                using var version = verified.CreateCommand();
                version.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration;";
                schema = Convert.ToInt32(version.ExecuteScalar());
                if (schema < 1 || schema > SqliteDatabase.CurrentSchemaVersion)
                    throw new InvalidDataException(
                        "Analytical SQLite copy schema is incompatible.");
            }
            cancellation.ThrowIfCancellationRequested();
            long size;
            string digest;
            using (var file = new FileStream(stage, FileMode.Open,
                       FileAccess.Read, FileShare.Read))
            {
                size = file.Length;
                digest = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
            }
            cancellation.ThrowIfCancellationRequested();
            // Atomic no-overwrite publication: if someone created target
            // while we were backing up, leave their file intact.
            File.Move(stage, target, overwrite: false);
            return new SqlAnalyticalCopyReceipt(target, schema, size, digest,
                DateTimeOffset.UtcNow,
                "ANALYTICAL_SQLITE_ONLY_NOT_A_COMPLETE_BACKUP");
        }
        finally
        {
            // Delete only this operation's GUID candidate and its sidecars.
            // Never touch target, source, WAL or any other backup.
            foreach (var temp in new[] { stage, stage + "-wal",
                         stage + "-shm", stage + "-journal" })
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void ValidateEnergyRequest(SqlIntegratedEnergyRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TimeZoneId);
        if (request.FirstLocalDay > request.LastLocalDay ||
            request.LastLocalDay.DayNumber - request.FirstLocalDay.DayNumber >= 366)
            throw new ArgumentOutOfRangeException(nameof(request),
                "Integrated energy needs an ordered station-local window of at most 366 days.");
        // Resolving the timezone before any destination is written also
        // rejects unknown station time zones instead of assuming UTC.
        _ = new TimeRangeSelectionService().ForArbitraryDateRange(
            request.FirstLocalDay, request.LastLocalDay, request.TimeZoneId);
    }

    private static void MaterializeIntegratedEnergy(
        string stage, SqlIntegratedEnergyRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // The SAME WAL-consistent SQLite snapshot that is published is also
        // the input to the existing physically accepted gap-aware integrator.
        // NEVER compute against the mutable source and append to a different snapshot.
        var snapshot = SqliteDatabase.FromReadOnlySnapshot(stage);
        var resolver = new TimeRangeSelectionService();
        var range = resolver.ForArbitraryDateRange(
            request.FirstLocalDay, request.LastLocalDay, request.TimeZoneId);
        var rows = new EnergyAggregationTableService(
            new PowerAggregationService(snapshot),
            new SocAggregationService(snapshot))
            .Get(request.DeviceId, range.StartUtc, range.EndUtc.AddTicks(-1),
                request.TimeZoneId, AggregationPeriod.Day, token).Rows;

        token.ThrowIfCancellationRequested();
        using var target = Open(stage, SqliteOpenMode.ReadWrite);
        using var transaction = target.BeginTransaction();
        using (var define = target.CreateCommand())
        {
            define.Transaction = transaction;
            define.CommandText = """
                CREATE TABLE reporting_integrated_energy_day (
                    device_id TEXT NOT NULL,
                    station_local_date TEXT NOT NULL,
                    timezone TEXT NOT NULL,
                    start_utc TEXT NOT NULL,
                    end_utc_exclusive TEXT NOT NULL,
                    pv_energy_kwh REAL NULL,
                    house_energy_kwh REAL NULL,
                    grid_import_energy_kwh REAL NULL,
                    battery_discharge_energy_kwh REAL NULL,
                    battery_charge_energy_kwh REAL NULL,
                    pv_coverage_percent REAL NULL,
                    house_coverage_percent REAL NULL,
                    grid_coverage_percent REAL NULL,
                    battery_coverage_percent REAL NULL,
                    PRIMARY KEY (device_id, station_local_date)
                );
                CREATE TABLE reporting_integrated_energy_info (
                    device_id TEXT NOT NULL,
                    timezone TEXT NOT NULL,
                    first_local_date TEXT NOT NULL,
                    last_local_date TEXT NOT NULL,
                    integration_method TEXT NOT NULL,
                    warning TEXT NOT NULL
                );
                """;
            define.ExecuteNonQuery();
        }
        using (var meta = target.CreateCommand())
        {
            meta.Transaction = transaction;
            meta.CommandText = """
                INSERT INTO reporting_integrated_energy_info
                VALUES ($device, $zone, $from, $to, 'APP_EXISTING_GAP_AWARE_INTEGRATOR',
                    'Computed from the same verified snapshot; noncontinuous W samples are not summed; NULL means insufficient integrated evidence; coverage under 100% is incomplete; no tariff or CLP estimate');
                """;
            meta.Parameters.AddWithValue("$device", request.DeviceId);
            meta.Parameters.AddWithValue("$zone", request.TimeZoneId);
            meta.Parameters.AddWithValue("$from", request.FirstLocalDay.ToString("yyyy-MM-dd"));
            meta.Parameters.AddWithValue("$to", request.LastLocalDay.ToString("yyyy-MM-dd"));
            meta.ExecuteNonQuery();
        }
        using (var insert = target.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO reporting_integrated_energy_day VALUES
                ($device,$date,$zone,$from,$to,$pv,$house,$grid,$bout,$bin,
                 $pc,$hc,$gc,$bc);
                """;
            foreach (var name in new[] { "$device", "$date", "$zone", "$from", "$to",
                         "$pv", "$house", "$grid", "$bout", "$bin",
                         "$pc", "$hc", "$gc", "$bc" })
                insert.Parameters.Add(new SqliteParameter(name, DBNull.Value));
            var index = 0;
            foreach (var row in rows)
            {
                if ((++index & 31) == 0) token.ThrowIfCancellationRequested();
                insert.Parameters["$device"].Value = request.DeviceId;
                insert.Parameters["$date"].Value = row.LocalLabel;
                insert.Parameters["$zone"].Value = request.TimeZoneId;
                insert.Parameters["$from"].Value = row.StartUtc.ToString("O");
                insert.Parameters["$to"].Value = row.EndUtcExclusive.ToString("O");
                insert.Parameters["$pv"].Value = ToSql(row.PvEnergyDisplayKwh);
                insert.Parameters["$house"].Value = ToSql(row.HouseEnergyDisplayKwh);
                insert.Parameters["$grid"].Value = ToSql(row.GridImportEnergyDisplayKwh);
                insert.Parameters["$bout"].Value = ToSql(row.BatteryDischargedEnergyDisplayKwh);
                insert.Parameters["$bin"].Value = ToSql(row.BatteryChargedEnergyDisplayKwh);
                insert.Parameters["$pc"].Value = ToSqlCoverage(row.PvCoveragePercent);
                insert.Parameters["$hc"].Value = ToSqlCoverage(row.HouseCoveragePercent);
                insert.Parameters["$gc"].Value = ToSqlCoverage(row.GridCoveragePercent);
                insert.Parameters["$bc"].Value = ToSqlCoverage(row.BatteryCoveragePercent);
                insert.ExecuteNonQuery();
            }
        }
        token.ThrowIfCancellationRequested();
        transaction.Commit();
    }

    private static object ToSql(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? value.Value : DBNull.Value;

    private static object ToSqlCoverage(double value) =>
        double.IsFinite(value) && value > 0 && value <= 100 ? value : DBNull.Value;

    private static SqliteConnection Open(string file, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = file, Mode = mode,
            Pooling = false, Cache = SqliteCacheMode.Private
        }.ToString());
        connection.Open();
        return connection;
    }
}

public sealed record SqlAnalyticalCopyReceipt(string Path, int SchemaVersion,
    long SizeBytes, string Sha256, DateTimeOffset CreatedUtc, string Classification);

public sealed record SqlIntegratedEnergyRequest(
    string DeviceId, string TimeZoneId, DateOnly FirstLocalDay, DateOnly LastLocalDay);

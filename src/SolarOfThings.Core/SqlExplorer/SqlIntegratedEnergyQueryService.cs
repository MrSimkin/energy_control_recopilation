using System.Diagnostics;
using System.Globalization;
using System.Text;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Read-only energy integration available INSIDE the SQL explorer, without
/// persisting a view/table or cloning a potentially multi-GB active database.
/// Results come from the existing physical report/analysis interval integrator;
/// the returned bounded table is frozen for explicit CSV/XLSX export.
/// </summary>
public sealed class SqlIntegratedEnergyQueryService
{
    private readonly string _databasePath;
    public const int MaximumLocalDays = 366;

    public SqlIntegratedEnergyQueryService(string databasePath) =>
        _databasePath = Path.GetFullPath(databasePath);

    public static void ValidateRequest(SqlIntegratedEnergyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TimeZoneId);
        if (request.FirstLocalDay > request.LastLocalDay ||
            request.LastLocalDay.DayNumber - request.FirstLocalDay.DayNumber >= MaximumLocalDays)
            throw new ArgumentOutOfRangeException(nameof(request),
                "Choose an ordered station-local period of at most 366 days.");
        _ = new TimeRangeSelectionService().ForArbitraryDateRange(
            request.FirstLocalDay, request.LastLocalDay, request.TimeZoneId);
    }

    public Task<SqlIntegratedEnergyTable> QueryAsync(
        SqlIntegratedEnergyRequest request, CancellationToken token = default)
    {
        ValidateRequest(request);
        token.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            // No active DB mutations, staging or SQLite native file copies:
            // read-only, private-cache, non-pooled connections on the original.
            // Concurrent source acquisition may advance between metric reads;
            // unlike the optional external snapshot, this is NOT an atomic
            // full-DB point-in-time capture.
            var readOnly = SqliteDatabase.FromReadOnlySnapshot(_databasePath);
            var range = new TimeRangeSelectionService().ForArbitraryDateRange(
                request.FirstLocalDay, request.LastLocalDay, request.TimeZoneId);
            var stats = new EnergyAggregationTableService(
                new PowerAggregationService(readOnly),
                new SocAggregationService(readOnly));
            var table = stats.Get(request.DeviceId, range.StartUtc,
                range.EndUtc.AddTicks(-1), request.TimeZoneId,
                AggregationPeriod.Day, token);
            token.ThrowIfCancellationRequested();
            var rows = table.Rows.Select(row => ToCells(request, row)).ToArray();
            return new SqlIntegratedEnergyTable(request, Headers, rows, DateTimeOffset.UtcNow);
        }, token);
    }

    public static readonly IReadOnlyList<string> Headers = new[]
    {
        "device_id", "station_local_date", "timezone", "start_utc",
        "end_utc_exclusive", "pv_energy_kwh", "house_energy_kwh",
        "grid_import_energy_kwh", "battery_discharge_energy_kwh",
        "battery_charge_energy_kwh", "pv_coverage_percent",
        "house_coverage_percent", "grid_coverage_percent",
        "battery_coverage_percent"
    };

    private static IReadOnlyList<SqlCell> ToCells(
        SqlIntegratedEnergyRequest request, EnergyAggregationRow row) =>
    [
        Text(request.DeviceId), Text(row.LocalLabel), Text(request.TimeZoneId),
        Text(row.StartUtc.ToString("O")), Text(row.EndUtcExclusive.ToString("O")),
        Energy(row.PvEnergyDisplayKwh, row.PvCoveragePercent),
        Energy(row.HouseEnergyDisplayKwh, row.HouseCoveragePercent),
        Energy(row.GridImportEnergyDisplayKwh, row.GridCoveragePercent),
        Energy(row.BatteryDischargedEnergyDisplayKwh, row.BatteryCoveragePercent),
        Energy(row.BatteryChargedEnergyDisplayKwh, row.BatteryCoveragePercent),
        Coverage(row.PvCoveragePercent), Coverage(row.HouseCoveragePercent),
        Coverage(row.GridCoveragePercent), Coverage(row.BatteryCoveragePercent)
    ];

    private static SqlCell Text(string s) => new(s, "TEXT", false);
    private static SqlCell Coverage(double c) => IsCoverage(c)
        ? Numeric(c) : new(null, "REAL", true);
    private static SqlCell Energy(double? v, double c) =>
        IsCoverage(c) && v.HasValue && double.IsFinite(v.Value)
            ? Numeric(v.Value) : new(null, "REAL", true);
    private static bool IsCoverage(double c) => double.IsFinite(c) && c > 0 && c <= 100;
    private static SqlCell Numeric(double n) => new(
        n.ToString("R", CultureInfo.InvariantCulture), "REAL", false);

    /// <summary>
    /// Exports precisely the computed table the user saw, not a silently
    /// rerun query against an evolving source DB. Atomic and no overwrite.
    /// </summary>
    public Task<SqlExportResult> ExportAsync(SqlIntegratedEnergyTable table,
        string selectedFilename, SqlExportFormat format,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ValidateRequest(table.Request);
        if (table.Columns.Count != Headers.Count ||
            table.Rows.Count > MaximumLocalDays ||
            !table.Columns.SequenceEqual(Headers, StringComparer.Ordinal) ||
            table.Rows.Any(row => row.Count != Headers.Count))
            throw new InvalidDataException("Unexpected integrated-energy result shape.");
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedFilename);
        var output = Path.GetFullPath(selectedFilename);
        var extension = format switch
        {
            SqlExportFormat.Csv => ".csv",
            SqlExportFormat.Xlsx => ".xlsx",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        if (!Path.GetExtension(output).Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Export file extension and format do not match.");
        if (File.Exists(output))
            throw new IOException("Existing export must not be overwritten.");
        token.ThrowIfCancellationRequested();
        return Task.Run(() => ExportCore(table, output, format, token), token);
    }

    private static SqlExportResult ExportCore(SqlIntegratedEnergyTable table,
        string output, SqlExportFormat format, CancellationToken token)
    {
        var folder = Path.GetDirectoryName(output)!;
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException("Export destination unavailable.");
        var temporary = Path.Combine(folder, ".solar-integrated-energy-" +
            Guid.NewGuid().ToString("N") + Path.GetExtension(output));
        var watch = Stopwatch.StartNew();
        try
        {
            token.ThrowIfCancellationRequested();
            var rows = table.Rows.Count;
            if (format == SqlExportFormat.Csv)
            {
                using var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", table.Columns.Select(Csv)));
                foreach (var row in table.Rows)
                {
                    token.ThrowIfCancellationRequested();
                    writer.WriteLine(string.Join(",", row.Select(cell =>
                        Csv(cell.IsNull ? @"\N" : FormulaSafe(cell.Text ?? "")))));
                }
            }
            else
            {
                var i = 0;
                var written = StreamingSqlXlsxWriter.Write(temporary,
                    table.Columns, () =>
                    {
                        token.ThrowIfCancellationRequested();
                        return i < rows ? table.Rows[i++] : null;
                    }, MaximumLocalDays, token);
                if (written != rows)
                    throw new InvalidDataException("Excel energy export omitted rows.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
            return new SqlExportResult(output, format, rows, table.Columns.Count,
                "COMPLETE", watch.Elapsed, new FileInfo(output).Length);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string FormulaSafe(string value)
    {
        var trimmed = value.TrimStart();
        return value.Length > 0 &&
            (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ||
             trimmed.StartsWith('=') || trimmed.StartsWith('@'))
            ? "'" + value : value;
    }
}

public sealed record SqlIntegratedEnergyTable(
    SqlIntegratedEnergyRequest Request,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<SqlCell>> Rows,
    DateTimeOffset CalculatedUtc);

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SolarOfThings.ResearchExporter;

public sealed record ExportProgress(int Step, double Percent, string Message);

public sealed record ExportResult(string ZipPath);

public static class ResearchPackageExporter
{
    public const string ExporterVersion = "research-exporter.v1";

    private static readonly string[] MetricKeys =
    [
        "grid_import_power_w",
        "house_load_power_w",
        "pv_power_w",
        "battery_power_w",
        "battery_soc_pct"
    ];

    private static readonly string[] ModeKeys =
    [
        "workingMode",
        "chargingPriorityOrder",
        "pvEnergyFeedingPriority",
        "mode",
        "powerSupplyFromPVToLoadInACState",
        "mainsCurrentFlowDirection",
        "acChargingSwitch",
        "solarChargingSwitch",
        "chargingMainSwitch",
        "batteryStatus",
        "gridConnectionSign",
        "mainOutputRelayStatus"
    ];

    private static readonly string[] SocConfigKeys =
    [
        "bmsReturnsToMainsModeSOC",
        "bmsReturnsToBatteryModeSOC",
        "bmsLowPowerSOC"
    ];

    public static ExportResult Export(
        string databasePath,
        string outputFolder,
        IProgress<ExportProgress>? progress = null)
    {
        progress?.Report(new ExportProgress(
            1,
            8,
            "Validando energy.db en modo sólo lectura"));

        if (!File.Exists(databasePath))
            throw new FileNotFoundException("No se encontró energy.db.", databasePath);

        Directory.CreateDirectory(outputFolder);

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "SolarOfThingsResearchExport",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var schemaVersion = Convert.ToInt32(
                Scalar(connection, "SELECT COALESCE(MAX(version),0) FROM schema_migration;"),
                CultureInfo.InvariantCulture);

            var deviceId = SelectPrimaryDevice(connection)
                ?? throw new InvalidOperationException(
                    "No se encontró telemetría normalizada de ningún dispositivo.");

            var timeZone = SelectTimeZone(connection, deviceId) ?? "America/Santiago";

            progress?.Report(new ExportProgress(
                2,
                28,
                "Extrayendo telemetría normalizada"));

            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            var metricsPath = Path.Combine(tempRoot, "normalized_metrics.csv");
            var metricRange = ExportMetrics(connection, deviceId, metricsPath);
            counts["normalized_metrics"] = metricRange.RowCount;

            progress?.Report(new ExportProgress(
                3,
                58,
                "Extrayendo contexto operativo permitido"));

            var modePath = Path.Combine(tempRoot, "mode_context.csv");
            counts["mode_context"] =
                ExportModeContext(connection, deviceId, modePath);

            var configPath = Path.Combine(tempRoot, "historical_soc_config.csv");
            counts["historical_soc_config"] =
                ExportSocConfiguration(connection, deviceId, configPath);

            var dayStatusPath = Path.Combine(tempRoot, "history_day_status.csv");
            counts["history_day_status"] =
                ExportDayStatus(connection, deviceId, dayStatusPath);

            var settingsPath = Path.Combine(tempRoot, "research_settings.json");
            WriteResearchSettings(connection, settingsPath);

            WriteReadme(Path.Combine(tempRoot, "README.txt"));

            progress?.Report(new ExportProgress(
                4,
                84,
                "Generando manifiesto, hashes y ZIP"));

            var contentFiles = Directory.GetFiles(tempRoot)
                .Select(path => new
                {
                    Name = Path.GetFileName(path),
                    Sha256 = Sha256(path),
                    Bytes = new FileInfo(path).Length
                })
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ToArray();

            var manifest = new
            {
                package_version = "solar-of-things-research-package.v1",
                exporter_version = ExporterVersion,
                generated_utc = DateTimeOffset.UtcNow.ToString("O"),
                source_database_name = Path.GetFileName(databasePath),
                source_schema_version = schemaVersion,
                selected_device = "research-device-1",
                device_selection_rule = "device with most grid_import_power_w normalized samples",
                timezone = timeZone,
                telemetry_start_utc = metricRange.MinTimestampUtc,
                telemetry_end_utc = metricRange.MaxTimestampUtc,
                included_metric_keys = MetricKeys,
                included_mode_keys = ModeKeys,
                included_soc_config_keys = SocConfigKeys,
                row_counts = counts,
                excluded_by_design = new[]
                {
                    "original device/station identifiers",
                    "serial numbers",
                    "credentials",
                    "utility bills",
                    "utility meter readings",
                    "tariff publications and prices",
                    "raw API request/response payloads",
                    "source_value_json"
                },
                files = contentFiles
            };

            var manifestPath = Path.Combine(tempRoot, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));

            var fileStem =
                $"SolarOfThings-ResearchPackage-{DateTime.Now:yyyyMMdd-HHmmss}";
            var zipPath = UniquePath(
                Path.Combine(outputFolder, fileStem + ".zip"));

            ZipFile.CreateFromDirectory(
                tempRoot,
                zipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            progress?.Report(new ExportProgress(
                4,
                100,
                "Paquete generado"));

            return new ExportResult(zipPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only. Never touch the source database.
            }
        }
    }

    private static string? SelectPrimaryDevice(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT device_id
            FROM normalized_metric_sample
            WHERE metric_key = 'grid_import_power_w'
            GROUP BY device_id
            ORDER BY COUNT(*) DESC
            LIMIT 1;
            """;
        return command.ExecuteScalar() as string;
    }

    private static string? SelectTimeZone(
        SqliteConnection connection,
        string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timezone
            FROM history_day_status
            WHERE device_id = $deviceId
              AND timezone IS NOT NULL
              AND trim(timezone) <> ''
            GROUP BY timezone
            ORDER BY COUNT(*) DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        return command.ExecuteScalar() as string;
    }

    private static (long RowCount, string? MinTimestampUtc, string? MaxTimestampUtc)
        ExportMetrics(
            SqliteConnection connection,
            string deviceId,
            string path)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                recorded_at_utc,
                metric_key,
                normalized_value,
                normalized_unit,
                confidence,
                quality,
                source_attribute_key,
                normalization_rule_version
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key IN (
                  'grid_import_power_w',
                  'house_load_power_w',
                  'pv_power_w',
                  'battery_power_w',
                  'battery_soc_pct'
              )
            ORDER BY recorded_at_utc, metric_key;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var writer = CsvWriter(path);
        WriteCsvRow(
            writer,
            "research_device",
            "recorded_at_utc",
            "metric_key",
            "normalized_value",
            "normalized_unit",
            "confidence",
            "quality",
            "source_attribute_key",
            "normalization_rule_version");

        using var reader = command.ExecuteReader();
        long count = 0;
        string? min = null;
        string? max = null;

        while (reader.Read())
        {
            var timestamp = reader.GetString(0);
            min ??= timestamp;
            max = timestamp;

            WriteCsvRow(
                writer,
                "research-device-1",
                timestamp,
                reader.GetString(1),
                reader.IsDBNull(2)
                    ? string.Empty
                    : reader.GetDouble(2).ToString("R", CultureInfo.InvariantCulture),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7));
            count++;
        }

        if (count == 0)
            throw new InvalidOperationException(
                "La base no contiene las métricas normalizadas requeridas.");

        return (count, min, max);
    }

    private static long ExportModeContext(
        SqliteConnection connection,
        string deviceId,
        string path)
    {
        using var writer = CsvWriter(path);
        WriteCsvRow(
            writer,
            "research_device",
            "recorded_at_utc",
            "context_key",
            "context_value",
            "source");

        long count = 0;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT recorded_at_utc, attribute_key, value_json
                FROM history_sample
                WHERE device_id = $deviceId
                  AND is_missing = 0
                  AND attribute_key IN (
                    {string.Join(",", ModeKeys.Select((_, i) => $"$key{i}"))}
                  )
                ORDER BY recorded_at_utc, attribute_key;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            for (var i = 0; i < ModeKeys.Length; i++)
                command.Parameters.AddWithValue($"$key{i}", ModeKeys[i]);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                WriteCsvRow(
                    writer,
                    "research-device-1",
                    reader.GetString(0),
                    reader.GetString(1),
                    JsonScalar(reader.IsDBNull(2) ? null : reader.GetString(2)),
                    "history_sample");
                count++;
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT response_json, retrieved_utc
                FROM raw_api_capture
                WHERE device_id = $deviceId
                  AND operation = 'LatestStateSnapshot'
                ORDER BY retrieved_utc;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var responseJson = reader.GetString(0);
                var retrievedUtc = reader.GetString(1);

                foreach (var item in ExtractWhitelistedSnapshot(
                             responseJson,
                             retrievedUtc))
                {
                    WriteCsvRow(
                        writer,
                        "research-device-1",
                        item.TimestampUtc,
                        item.Key,
                        item.Value,
                        "latest_state_whitelist");
                    count++;
                }
            }
        }

        return count;
    }

    private static IEnumerable<(string TimestampUtc, string Key, string Value)>
        ExtractWhitelistedSnapshot(
            string raw,
            string fallbackTimestamp)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch
        {
            yield break;
        }

        using (document)
        {
            var root = document.RootElement;
            var timestamp = fallbackTimestamp;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("time", out var time) &&
                !string.IsNullOrWhiteSpace(time.ToString()))
            {
                timestamp = time.ToString();
            }

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("fields", out var fields) ||
                fields.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            foreach (var key in ModeKeys)
            {
                if (!fields.TryGetProperty(key, out var value))
                    continue;

                var display = SafeDisplayValue(value);
                if (!string.IsNullOrWhiteSpace(display))
                    yield return (timestamp, key, display);
            }
        }
    }

    private static string SafeDisplayValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "display", "value", "rawValue", "text" })
            {
                if (element.TryGetProperty(key, out var nested))
                {
                    var result = SafeDisplayValue(nested);
                    if (!string.IsNullOrWhiteSpace(result))
                        return result;
                }
            }

            return string.Empty;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                element.ToString(),
            _ => string.Empty
        };
    }

    private static long ExportSocConfiguration(
        SqliteConnection connection,
        string deviceId,
        string path)
    {
        using var writer = CsvWriter(path);
        WriteCsvRow(
            writer,
            "research_device",
            "recorded_at_utc",
            "config_key",
            "config_value");

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT recorded_at_utc, attribute_key, value_json
            FROM history_sample
            WHERE device_id = $deviceId
              AND is_missing = 0
              AND attribute_key IN (
                {string.Join(",", SocConfigKeys.Select((_, i) => $"$key{i}"))}
              )
            ORDER BY recorded_at_utc, attribute_key;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        for (var i = 0; i < SocConfigKeys.Length; i++)
            command.Parameters.AddWithValue($"$key{i}", SocConfigKeys[i]);

        using var reader = command.ExecuteReader();
        long count = 0;
        while (reader.Read())
        {
            WriteCsvRow(
                writer,
                "research-device-1",
                reader.GetString(0),
                reader.GetString(1),
                JsonScalar(reader.IsDBNull(2) ? null : reader.GetString(2)));
            count++;
        }

        return count;
    }

    private static long ExportDayStatus(
        SqliteConnection connection,
        string deviceId,
        string path)
    {
        using var writer = CsvWriter(path);
        WriteCsvRow(
            writer,
            "research_device",
            "local_date",
            "timezone",
            "source",
            "status",
            "frame_count",
            "page_count",
            "first_at_utc",
            "last_at_utc",
            "retry_count");

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                local_date,
                timezone,
                source,
                status,
                frame_count,
                page_count,
                first_at_utc,
                last_at_utc,
                retry_count
            FROM history_day_status
            WHERE device_id = $deviceId
            ORDER BY local_date;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        long count = 0;
        while (reader.Read())
        {
            WriteCsvRow(
                writer,
                "research-device-1",
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4).ToString(CultureInfo.InvariantCulture),
                reader.GetInt32(5).ToString(CultureInfo.InvariantCulture),
                reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                reader.GetInt32(8).ToString(CultureInfo.InvariantCulture));
            count++;
        }

        return count;
    }

    private static void WriteResearchSettings(
        SqliteConnection connection,
        string path)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT value
            FROM app_setting
            WHERE key = 'battery.usable-capacity-kwh';
            """;

        var raw = command.ExecuteScalar() as string;
        var hasConfigured = double.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var capacity);

        if (!hasConfigured)
            capacity = 11.776;

        var payload = new
        {
            battery_usable_capacity_kwh = capacity,
            battery_capacity_source =
                hasConfigured ? "app_setting" : "application_default",
            application_default_if_missing_kwh = 11.776
        };

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    private static void WriteReadme(string path)
    {
        File.WriteAllText(
            path,
            """
            Solar of Things — Research Package v1

            Purpose:
            statistical backtesting for Phase 3 and, where sufficient, source-attribution research for Phase 4.

            Included:
            - five normalized telemetry metrics;
            - quality/confidence and normalization provenance;
            - whitelisted operating-mode context only;
            - three historical SOC configuration values;
            - daily history-completeness records;
            - usable battery capacity;
            - manifest, hashes and row counts.

            Excluded by design:
            - original device/station identifiers;
            - serial numbers;
            - credentials;
            - Enel bills and meter readings;
            - tariff publications/prices;
            - raw API payloads;
            - unfiltered source JSON.

            The source energy.db is opened read-only and is not modified.
            """,
            new UTF8Encoding(false));
    }

    private static object Scalar(
        SqliteConnection connection,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() ??
               throw new InvalidOperationException("No se pudo leer el esquema SQLite.");
    }

    private static StreamWriter CsvWriter(string path) =>
        new(path, append: false, new UTF8Encoding(true));

    private static void WriteCsvRow(
        TextWriter writer,
        params string[] values) =>
        writer.WriteLine(string.Join(",", values.Select(CsvEscape)));

    private static string CsvEscape(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"'))
            value = value.Replace(""", """");

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $""{value}""
            : value;
    }

    private static string JsonScalar(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(raw);
            return SafeDisplayValue(document.RootElement);
        }
        catch
        {
            return raw.Trim().Trim('"');
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(
                directory,
                $"{stem}-{index}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }
}

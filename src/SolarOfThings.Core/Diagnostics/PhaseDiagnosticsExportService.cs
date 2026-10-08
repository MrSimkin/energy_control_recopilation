using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Backup;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Read-only structural and test-evidence export for Phases 10-12.
/// Deliberately excludes SQLite database bytes, bill PDFs, telemetry,
/// source credentials, cookies, tokens and API response payloads.
/// </summary>
public sealed class PhaseDiagnosticsExportService
{
    private readonly SqliteDatabase _database;
    private readonly AppPaths _paths;

    public PhaseDiagnosticsExportService(SqliteDatabase database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    public string Export()
    {
        Directory.CreateDirectory(_paths.LogDirectory);
        var target = Path.Combine(_paths.LogDirectory,
            $"phase10-12-debug-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _database.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
        connection.Open();

        var schema = new List<object>();
        var structuralObjects = new List<(string Type, string Name)>();
        var viewNames = new HashSet<string>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT type, name, sql
                FROM sqlite_master
                WHERE type IN ('table', 'view')
                  AND name NOT LIKE 'sqlite_%'
                ORDER BY type, name;
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var type = reader.GetString(0);
                var name = reader.GetString(1);
                structuralObjects.Add((type, name));
                if (type == "view")
                    viewNames.Add(name);
                schema.Add(new
                {
                    type,
                    name,
                    ddl = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }
        }

        // Runtime column dictionary and declared relationships come from
        // SQLite itself rather than a hand-maintained, potentially stale list.
        var columnsCsv = new StringBuilder(
            "object_type,table_or_view,column,sql_type,not_null,primary_key\n");
        var foreignKeysCsv = new StringBuilder(
            "child_table,foreign_key_id,sequence,parent_table,child_column,parent_column\n");
        foreach (var item in structuralObjects)
        {
            using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText =
                "SELECT name,type,\"notnull\",pk FROM pragma_table_info($name);";
            columnCommand.Parameters.AddWithValue("$name", item.Name);
            using (var reader = columnCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    columnsCsv.AppendLine(string.Join(",",
                        Csv(item.Type), Csv(item.Name),
                        Csv(reader.GetString(0)),
                        Csv(reader.IsDBNull(1) ? "" : reader.GetString(1)),
                        reader.GetInt64(2).ToString(),
                        reader.GetInt64(3).ToString()));
                }
            }

            if (item.Type != "table")
                continue;

            using var keyCommand = connection.CreateCommand();
            keyCommand.CommandText =
                "SELECT id,seq,\"table\",\"from\",\"to\" " +
                "FROM pragma_foreign_key_list($name);";
            keyCommand.Parameters.AddWithValue("$name", item.Name);
            using var keys = keyCommand.ExecuteReader();
            while (keys.Read())
            {
                foreignKeysCsv.AppendLine(string.Join(",",
                    Csv(item.Name),
                    keys.GetInt64(0).ToString(),
                    keys.GetInt64(1).ToString(),
                    Csv(keys.GetString(2)),
                    Csv(keys.GetString(3)),
                    Csv(keys.IsDBNull(4) ? "" : keys.GetString(4))));
            }
        }

        string integrity;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA quick_check;";
            integrity = Convert.ToString(command.ExecuteScalar()) ?? "UNAVAILABLE";
        }

        var views = new[]
        {
            "reporting_grid_import", "reporting_battery",
            "reporting_utility_bills", "reporting_bill_line_evidence",
            "data_quality_summary"
        };
        var viewChecks = views.ToDictionary(
            name => name,
            name => viewNames.Contains(name) ? "PASS" : "FAIL");

        var verifiedBackups = new List<object>();
        foreach (var manifest in Directory.EnumerateFiles(
            _paths.BackupDirectory, "*.sqlite.manifest.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(15))
        {
            try
            {
                var result = JsonSerializer.Deserialize<VerifiedDatabaseBackup>(
                    File.ReadAllText(manifest));
                if (result is not null)
                {
                    verifiedBackups.Add(new
                    {
                        file_name = Path.GetFileName(result.Path),
                        result.Kind,
                        result.SchemaVersion,
                        result.SizeBytes,
                        result.Sha256,
                        result.VerifiedUtc,
                        result.IntegrityStatus,
                        manifest_present = true
                    });
                }
            }
            catch
            {
                verifiedBackups.Add(new
                {
                    file_name = Path.GetFileName(manifest),
                    status = "INVALID_MANIFEST"
                });
            }
        }

        var details = new
        {
            generated_utc = DateTimeOffset.UtcNow,
            schema_version = _database.GetSchemaVersion(),
            database_quick_check = integrity,
            view_checks = viewChecks,
            phase_10_real_bill_reconstruction =
                "NOT_RUN: use the selected bill's Audit PDF and technical annex",
            enel_zero_click =
                "NOT_RUN: requires interactive WebView2/official site validation",
            ui_progress =
                "NOT_RUN: target-PC visual QA required",
            phase_12_restore =
                "OUT_OF_SCOPE: explicitly deferred by owner",
            phase_12_backups = verifiedBackups
        };

        var schemaJson = JsonSerializer.Serialize(schema,
            new JsonSerializerOptions { WriteIndented = true });
        var detailsJson = JsonSerializer.Serialize(details,
            new JsonSerializerOptions { WriteIndented = true });
        // The interactive official-site flow cannot be exercised by CI.
        // Export only explicit, allowlisted operational fields; never copy
        // arbitrary browser logs, headers or URLs into a support bundle.
        var enelEvents = new List<object>();
        var browserEventsPath = Path.Combine(
            _paths.LogDirectory, EnelBrowserCaptureTelemetry.FileName);
        if (File.Exists(browserEventsPath))
        {
            foreach (var raw in File.ReadLines(browserEventsPath).TakeLast(150))
            {
                try
                {
                    using var parsed = JsonDocument.Parse(raw);
                    var root = parsed.RootElement;
                    enelEvents.Add(new
                    {
                        utc = root.GetProperty("utc").GetString(),
                        step = root.GetProperty("step").GetString(),
                        outcome = root.GetProperty("outcome").GetString(),
                        http_status = root.TryGetProperty("http_status", out var code) &&
                            code.ValueKind == JsonValueKind.Number
                            ? code.GetInt32() : (int?)null,
                        bytes = root.TryGetProperty("bytes", out var count) &&
                            count.ValueKind == JsonValueKind.Number
                            ? count.GetInt64() : (long?)null
                    });
                }
                catch (Exception exception)
                    when (exception is JsonException or InvalidOperationException
                          or KeyNotFoundException)
                {
                    // Ignore malformed historical lines, do not export them.
                }
            }
        }
        var enelJson = JsonSerializer.Serialize(new
        {
            status = enelEvents.Count == 0 ? "NOT_RUN" : "CAPTURE_EVENTS_AVAILABLE",
            important = "An observed event is not proof that zero-click imported successfully.",
            events = enelEvents
        }, new JsonSerializerOptions { WriteIndented = true });

        var readme = """
            Phase 10-12 diagnostic export (explicit user action).
            No raw telemetry, source PDFs, active SQLite database,
            backups or credentials are included.
            Status NOT_RUN is not PASS.
            For real bill reconciliation attach the Audit PDF and
            its technical annex exported from the selected Enel bill.
            The source database has not been restored or replaced.
            """;

        try
        {
            using (var stream = File.Create(target))
            using (var zip = new ZipArchive(
                stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var hashes = new Dictionary<string, string>();
                Add(zip, "overview.json", detailsJson, hashes);
                Add(zip, "schema_inventory.json", schemaJson, hashes);
                Add(zip, "schema_columns.csv", columnsCsv.ToString(), hashes);
                Add(zip, "schema_foreign_keys.csv", foreignKeysCsv.ToString(), hashes);
                Add(zip, "enel_capture_events.json", enelJson, hashes);
                Add(zip, "README.txt", readme, hashes);
                Add(zip, "manifest.json", JsonSerializer.Serialize(
                    new
                    {
                        format = "phase10-12-diagnostic-v1",
                        entries_sha256 = hashes
                    },
                    new JsonSerializerOptions { WriteIndented = true }),
                    new Dictionary<string, string>());
            }
        }
        catch
        {
            if (File.Exists(target))
                File.Delete(target);
            throw;
        }

        return target;
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private static void Add(
        ZipArchive zip,
        string name,
        string value,
        IDictionary<string, string> hashes)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using (var writer = entry.Open())
            writer.Write(bytes);
        hashes[name] = Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

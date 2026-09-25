using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Diagnostics;

public sealed class ApiDiagnosticsStore
{
    private readonly object _sync = new();
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false
    };

    public ApiDiagnosticsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public void Record(ApiDiagnosticEntry entry)
    {
        var safeEntry = entry with
        {
            ApiMessage = DiagnosticSanitizer.SanitizeText(entry.ApiMessage),
            RequestJson = DiagnosticSanitizer.SanitizeJson(entry.RequestJson),
            ResponseJson = DiagnosticSanitizer.SanitizeJson(entry.ResponseJson),
            RequestHeadersJson = DiagnosticSanitizer.SanitizeJson(entry.RequestHeadersJson),
            ResponseHeadersJson = DiagnosticSanitizer.SanitizeJson(entry.ResponseHeadersJson),
            ExceptionMessage = DiagnosticSanitizer.SanitizeText(entry.ExceptionMessage),
            ExceptionStackTrace = DiagnosticSanitizer.SanitizeText(entry.ExceptionStackTrace)
        };

        var path = Path.Combine(
            _paths.LogDirectory,
            $"api-diagnostics-{DateTimeOffset.Now:yyyyMMdd}.jsonl");

        var line = JsonSerializer.Serialize(safeEntry, _jsonOptions);

        lock (_sync)
        {
            File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    public void RecordLocal(
        string operation,
        string step,
        string outcome,
        string? message = null,
        string? detailsJson = null)
    {
        Record(new ApiDiagnosticEntry(
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"),
            operation,
            step,
            "LOCAL",
            "local",
            1,
            null,
            null,
            message,
            0,
            outcome,
            null,
            detailsJson,
            null,
            null));
    }

    public IReadOnlyList<ApiDiagnosticEntry> ReadRecent(int maxEvents = 200)
    {
        var files = Directory.GetFiles(_paths.LogDirectory, "api-diagnostics-*.jsonl")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(7)
            .ToList();

        var result = new List<ApiDiagnosticEntry>();

        foreach (var path in files)
        {
            string[] lines;
            lock (_sync)
            {
                lines = File.ReadAllLines(path);
            }

            foreach (var line in lines.Reverse())
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<ApiDiagnosticEntry>(line);
                    if (entry is not null)
                    {
                        result.Add(entry);
                    }
                }
                catch
                {
                    // A partially written/corrupt diagnostic line should never block export.
                }

                if (result.Count >= maxEvents)
                {
                    return result.OrderBy(entry => entry.Timestamp).ToList();
                }
            }
        }

        return result.OrderBy(entry => entry.Timestamp).ToList();
    }

    public string BuildSanitizedReport(int maxEvents = 200)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";

        var sb = new StringBuilder();
        sb.AppendLine("SOLAR ENERGY MONITOR — SANITIZED DEVELOPMENT DIAGNOSTIC REPORT");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"App version/build: {infoVersion}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        var portable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.mode"));
        sb.AppendLine($"Portable mode: {(portable ? "YES" : "NO")}");
        sb.AppendLine($"Database path: {_paths.DatabasePath}");
        sb.AppendLine();
        sb.AppendLine("SECURITY:");
        sb.AppendLine("- Passwords, password hashes, access tokens, refresh tokens, cookies,");
        sb.AppendLine("  reusable client secrets and request signatures are automatically redacted.");
        sb.AppendLine("- Station/device identifiers and non-secret device metadata are retained");
        sb.AppendLine("  because they can be necessary to diagnose API argument/capability failures.");
        sb.AppendLine();

        var entries = ReadRecent(maxEvents);
        sb.AppendLine($"EVENTS INCLUDED: {entries.Count}");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            sb.AppendLine("----------------------------------------------------------------");
            sb.AppendLine($"UTC: {entry.Timestamp:O}");
            sb.AppendLine($"Correlation: {entry.CorrelationId}");
            sb.AppendLine($"Operation: {entry.Operation}");
            sb.AppendLine($"Commissioning step: {entry.Step}");
            sb.AppendLine($"Request: {entry.Method} {entry.Endpoint}");
            sb.AppendLine($"Attempt: {entry.Attempt}");
            sb.AppendLine($"Outcome: {entry.Outcome}");
            sb.AppendLine($"HTTP status: {entry.HttpStatus?.ToString() ?? "-"}");
            sb.AppendLine($"API code: {entry.ApiCode ?? "-"}");
            sb.AppendLine($"API message: {entry.ApiMessage ?? "-"}");
            sb.AppendLine($"Elapsed ms: {entry.DurationMilliseconds}");

            if (!string.IsNullOrWhiteSpace(entry.RequestHeadersJson))
            {
                sb.AppendLine("Sanitized request headers:");
                sb.AppendLine(entry.RequestHeadersJson);
            }

            if (!string.IsNullOrWhiteSpace(entry.RequestJson))
            {
                sb.AppendLine("Sanitized request JSON:");
                sb.AppendLine(entry.RequestJson);
            }

            if (!string.IsNullOrWhiteSpace(entry.ResponseHeadersJson))
            {
                sb.AppendLine("Sanitized response headers:");
                sb.AppendLine(entry.ResponseHeadersJson);
            }

            if (entry.ResponseLengthBytes.HasValue)
            {
                sb.AppendLine($"Response bytes: {entry.ResponseLengthBytes.Value}");
            }

            if (!string.IsNullOrWhiteSpace(entry.ResponseJson))
            {
                sb.AppendLine("Sanitized response JSON:");
                sb.AppendLine(entry.ResponseJson);
            }

            if (!string.IsNullOrWhiteSpace(entry.ExceptionType) ||
                !string.IsNullOrWhiteSpace(entry.ExceptionMessage))
            {
                sb.AppendLine($"Exception: {entry.ExceptionType ?? "-"}");
                sb.AppendLine($"Exception message: {entry.ExceptionMessage ?? "-"}");
                if (!string.IsNullOrWhiteSpace(entry.ExceptionStackTrace))
                {
                    sb.AppendLine("Exception stack trace:");
                    sb.AppendLine(entry.ExceptionStackTrace);
                }
            }
        }

        return sb.ToString();
    }

    public string SaveSanitizedReport()
    {
        var directory = Path.Combine(_paths.LogDirectory, "Exports");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(
            directory,
            $"sanitized-diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");

        File.WriteAllText(path, BuildSanitizedReport(), new UTF8Encoding(false));
        return path;
    }
}

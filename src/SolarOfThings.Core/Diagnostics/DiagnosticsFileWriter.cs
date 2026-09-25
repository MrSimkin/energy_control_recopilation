using System.Text;
using System.Text.Json;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Diagnostics;

public sealed class DiagnosticsFileWriter
{
    private readonly AppPaths _paths;
    private readonly object _sync = new();

    public DiagnosticsFileWriter(AppPaths paths)
    {
        _paths = paths;
    }

    public void Write(
        string level,
        string eventName,
        string message,
        string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var now = DateTimeOffset.UtcNow;
        var record = new DiagnosticRecord(
            now,
            level,
            eventName,
            message,
            detail);

        var json = JsonSerializer.Serialize(record);
        var path = Path.Combine(
            _paths.LogDirectory,
            $"diagnostics-{now:yyyyMMdd}.jsonl");

        lock (_sync)
        {
            File.AppendAllText(
                path,
                json + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private sealed record DiagnosticRecord(
        DateTimeOffset TimestampUtc,
        string Level,
        string Event,
        string Message,
        string? Detail);
}

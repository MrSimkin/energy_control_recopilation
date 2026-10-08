using System.Text.Json;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Safe operational evidence for Enel's ordinary WebView2 session.
/// Never records URLs, cookies, headers, document contents or credentials.
/// </summary>
public static class EnelBrowserCaptureTelemetry
{
    public const string FileName = "enel-browser-events.jsonl";

    public static void Record(
        AppPaths paths,
        string step,
        string outcome,
        int? statusCode = null,
        long? byteCount = null)
    {
        try
        {
            Directory.CreateDirectory(paths.LogDirectory);
            var item = new
            {
                utc = DateTimeOffset.UtcNow,
                step,
                outcome,
                http_status = statusCode,
                bytes = byteCount
            };
            File.AppendAllText(
                Path.Combine(paths.LogDirectory, FileName),
                JsonSerializer.Serialize(item) + Environment.NewLine);
        }
        catch (IOException)
        {
            // Diagnostic telemetry must never interrupt a valid official PDF import.
        }
        catch (UnauthorizedAccessException)
        {
            // Failed optional diagnostic logging must not break the viewer.
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SolarOfThings.Core.Diagnostics;

public static partial class DiagnosticSanitizer
{
    private static readonly string[] SecretKeyFragments =
    [
        "password",
        "accesstoken",
        "refreshtoken",
        "iottoken",
        "cookie",
        "secret",
        "authorization",
        "iotopensign",
        "signature"
    ];

    private static readonly HashSet<string> PersonalExactKeys =
    [
        "userid",
        "useraccount",
        "username",
        "address",
        "longitude",
        "latitude",
        "country",
        "province",
        "city",
        "area"
    ];

    public static string? SanitizeJson(string? json, int maxLength = 262144)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            var node = JsonNode.Parse(json);
            SanitizeNode(node);
            var sanitized = node?.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false
            }) ?? "null";
            return Truncate(sanitized, maxLength);
        }
        catch
        {
            return Truncate(SanitizeText(json), maxLength);
        }
    }

    public static string SanitizeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var result = BearerRegex().Replace(value, "Bearer [REDACTED]");
        result = TokenAssignmentRegex().Replace(result, "$1=[REDACTED]");
        return result;
    }

    private static void SanitizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSensitiveKey(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                }
                else
                {
                    SanitizeNode(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                SanitizeNode(item);
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return SecretKeyFragments.Any(fragment =>
                   normalized.Contains(fragment, StringComparison.Ordinal)) ||
               PersonalExactKeys.Contains(normalized);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength] + $"...[TRUNCATED {value.Length - maxLength} CHARS]";
    }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+\-/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)\b(accessToken|refreshToken|iotToken|password|secret|signature)\b\s*[=:]\s*[^\s,;]+")]
    private static partial Regex TokenAssignmentRegex();
}

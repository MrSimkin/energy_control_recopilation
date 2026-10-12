using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Synthetic-only, read-only comparison of source/target telemetry rows.
/// Source/target are already verified and opened read-only by recovery preview.
/// Does not perform W-to-kWh integration or authorize a real import.
/// </summary>
internal static class IsolatedRecoveryTelemetryPreviewService
{
    private const int MaxRowsPerSide = 100_000;
    private const int MaxCellLength = 64_000;

    private sealed record Category(string Label, string Table, int KeyFields,
        string[] Fields);

    private static readonly Category[] Categories =
    [
        new("HISTORY_RAW_SAMPLES", "history_sample", 3,
            ["device_id", "attribute_key", "recorded_at_utc", "value_json",
             "is_missing", "source", "retrieved_utc"]),
        new("HISTORY_DAY_COVERAGE", "history_day_status", 2,
            ["device_id", "local_date", "timezone", "source", "status",
             "frame_count", "page_count", "first_at_utc", "last_at_utc",
             "detail_json", "updated_utc", "retry_count"]),
        new("NORMALIZED_METRICS", "normalized_metric_sample", 3,
            ["device_id", "metric_key", "recorded_at_utc", "normalized_value",
             "normalized_unit", "source_attribute_key", "source_value_json",
             "normalization_rule_version", "confidence", "quality", "updated_utc"])
    ];

    internal static IReadOnlyList<RecoveryCategoryResult> Compare(
        SqliteConnection source, SqliteConnection target)
    {
        var results = new List<RecoveryCategoryResult>();
        foreach (var category in Categories)
        {
            try { results.Add(CompareOne(source, target, category)); }
            catch (SqliteException)
            {
                results.Add(new RecoveryCategoryResult(category.Label, 0, 0, 0,
                    "Unsupported telemetry schema; comparison incomplete."));
            }
            catch (InvalidDataException)
            {
                results.Add(new RecoveryCategoryResult(category.Label, 0, 0, 0,
                    "Unsupported or oversized telemetry evidence; comparison incomplete."));
            }
        }
        return results;
    }

    private static RecoveryCategoryResult CompareOne(
        SqliteConnection source, SqliteConnection target, Category category)
    {
        // Closed allowlist only; no user-supplied SQL identifiers.
        var sql = "SELECT " + string.Join(",", category.Fields) +
            " FROM " + category.Table + " ORDER BY " +
            string.Join(",", category.Fields.Take(category.KeyFields)) + ";";
        using var fromCommand = source.CreateCommand();
        using var toCommand = target.CreateCommand();
        fromCommand.CommandText = sql;
        toCommand.CommandText = sql;
        using var fromReader = fromCommand.ExecuteReader();
        using var toReader = toCommand.ExecuteReader();

        var sourceCount = 0;
        var targetCount = 0;
        var a = Next(fromReader, category, ref sourceCount);
        var b = Next(toReader, category, ref targetCount);
        var missing = 0;
        var same = 0;
        var conflicts = 0;
        var targetOnly = 0;
        var changed = new Dictionary<string, int>(StringComparer.Ordinal);
        while (a is not null || b is not null)
        {
            var ordering = a is null ? 1 : b is null ? -1 :
                CompareCompositeKey(a, b, category.KeyFields);
            if (ordering < 0)
            {
                missing++;
                a = Next(fromReader, category, ref sourceCount);
            }
            else if (ordering > 0)
            {
                targetOnly++;
                b = Next(toReader, category, ref targetCount);
            }
            else
            {
                var equal = true;
                for (var i = category.KeyFields; i < category.Fields.Length; i++)
                {
                    if (string.Equals(a![i], b![i], StringComparison.Ordinal))
                        continue;
                    equal = false;
                    changed[category.Fields[i]] = changed.GetValueOrDefault(category.Fields[i]) + 1;
                }
                if (equal) same++; else conflicts++;
                a = Next(fromReader, category, ref sourceCount);
                b = Next(toReader, category, ref targetCount);
            }
        }
        return new RecoveryCategoryResult(category.Label, missing, same,
            conflicts, null)
        {
            SourceRecords = sourceCount,
            TargetRecords = targetCount,
            TargetOnly = targetOnly,
            ChangedFields = changed
        };
    }

    private static string?[]? Next(SqliteDataReader reader, Category category,
        ref int seen)
    {
        if (!reader.Read()) return null;
        if (++seen > MaxRowsPerSide)
            throw new InvalidDataException("Telemetry preview exceeds row bound.");
        var values = new string?[category.Fields.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (reader.IsDBNull(i)) continue;
            var value = reader.GetValue(i);
            if (value is byte[])
                throw new InvalidDataException("Binary telemetry row unsupported.");
            values[i] = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (values[i] is null || values[i]!.Length > MaxCellLength)
                throw new InvalidDataException("Telemetry cell exceeds bound.");
        }
        for (var i = 0; i < category.KeyFields; i++)
            if (string.IsNullOrEmpty(values[i]) || values[i]!.Length > 1024)
                throw new InvalidDataException("Invalid composite telemetry key.");
        return values;
    }

    // SQLite BINARY ordering uses UTF-8 rather than .NET UTF-16 ordering.
    private static int CompareCompositeKey(string?[] a, string?[] b, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var comparison = Encoding.UTF8.GetBytes(a[i]!).AsSpan()
                .SequenceCompareTo(Encoding.UTF8.GetBytes(b[i]!));
            if (comparison != 0) return comparison;
        }
        return 0;
    }
}

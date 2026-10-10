using System.Text;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Pure, bounded SQL editor assistance: no database access, no SQL execution
/// and no implicit persistence. User's visible schema names only.
/// </summary>
public static class SqlEditorAssistance
{
    public const int MaximumSuggestions = 18;
    private static readonly string[] Keywords =
    [
        "SELECT", "FROM", "WHERE", "ORDER BY", "GROUP BY", "HAVING",
        "LIMIT", "OFFSET", "LEFT JOIN", "INNER JOIN", "ON", "AS",
        "COUNT", "SUM", "AVG", "MIN", "MAX", "DISTINCT", "AND",
        "OR", "IS NULL", "IS NOT NULL", "COALESCE", "WITH"
    ];

    public static IReadOnlyList<SqlEditorSuggestion> Suggest(string sql, int caret,
        IEnumerable<string> visibleSchemaNames)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(visibleSchemaNames);
        if (sql.Length > 20_000 || caret < 0 || caret > sql.Length ||
            InsideCommentOrQuotedText(sql, caret))
            return [];
        var start = caret;
        while (start > 0 && IsWord(sql[start - 1])) start--;
        var prefix = sql[start..caret];
        // Never replace quoted identifiers or full tokens in the middle.
        if (caret < sql.Length && IsWord(sql[caret])) return [];
        var options = new List<SqlEditorSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in visibleSchemaNames
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 200)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(name)) continue;
            options.Add(new SqlEditorSuggestion(name,
                "\"" + name.Replace("\"", "\"\"") + "\"", start, caret - start,
                "SCHEMA"));
            if (options.Count == MaximumSuggestions) return options;
        }
        foreach (var keyword in Keywords)
        {
            if (!keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(keyword)) continue;
            options.Add(new SqlEditorSuggestion(keyword, keyword, start,
                caret - start, "KEYWORD"));
            if (options.Count == MaximumSuggestions) break;
        }
        return options;
    }

    /// <summary>Changes only the selected logical lines, preserving CRLF.</summary>
    public static SqlLineCommentEdit ToggleLineComments(
        string sql, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (sql.Length > 20_000 || selectionStart < 0 || selectionLength < 0 ||
            selectionStart > sql.Length ||
            selectionLength > sql.Length - selectionStart)
            throw new ArgumentOutOfRangeException(nameof(selectionStart));
        var from = selectionStart == 0 ? 0 :
            sql.LastIndexOf('\n', selectionStart - 1) + 1;
        var ending = selectionStart + selectionLength;
        // A selection ending at the first column of another line does not
        // include that next line.
        if (selectionLength > 0 && ending > 0 && ending <= sql.Length &&
            sql[ending - 1] == '\n')
            ending--;
        var next = sql.IndexOf('\n', ending);
        var to = next < 0 ? sql.Length : next;
        var oldRegion = sql[from..to];
        var lines = oldRegion.Split('\n');
        static int Indent(string line)
        {
            var i = 0;
            while (i < line.Length && line[i] is ' ' or '\t') i++;
            return i;
        }
        var nonEmpty = lines.Where(line => line.Trim().Length > 0).ToArray();
        var remove = nonEmpty.Length > 0 && nonEmpty.All(line =>
        {
            var i = Indent(line);
            return i + 1 < line.Length && line[i] == '-' && line[i + 1] == '-';
        });
        var rewritten = lines.Select(line =>
        {
            var i = Indent(line);
            if (line.Trim().Length == 0) return line;
            if (remove)
            {
                var after = i + 2;
                if (after < line.Length && line[after] == ' ') after++;
                return line[..i] + line[after..];
            }
            return line[..i] + "-- " + line[i..];
        });
        var newRegion = string.Join("\n", rewritten);
        return new SqlLineCommentEdit(sql[..from] + newRegion + sql[to..],
            from, newRegion.Length);
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool InsideCommentOrQuotedText(string sql, int caret)
    {
        var block = false;
        var line = false;
        var quote = '\0';
        for (var i = 0; i < caret;)
        {
            var c = sql[i];
            if (line) { if (c == '\n') line = false; i++; continue; }
            if (block)
            {
                if (i + 1 < caret && c == '*' && sql[i + 1] == '/')
                { block = false; i += 2; }
                else i++;
                continue;
            }
            if (quote != '\0')
            {
                var close = quote == '[' ? ']' : quote;
                if (c == close)
                {
                    if (i + 1 < caret && sql[i + 1] == close) i += 2;
                    else { quote = '\0'; i++; }
                }
                else i++;
                continue;
            }
            if (i + 1 < caret && c == '-' && sql[i + 1] == '-')
            { line = true; i += 2; continue; }
            if (i + 1 < caret && c == '/' && sql[i + 1] == '*')
            { block = true; i += 2; continue; }
            if (c is '\'' or '\"' or '`' or '[') { quote = c; i++; continue; }
            i++;
        }
        return block || line || quote != '\0';
    }
}

public sealed record SqlEditorSuggestion(
    string Label, string Replacement, int Start, int Length, string Kind);
public sealed record SqlLineCommentEdit(string Text, int SelectionStart, int SelectionLength);

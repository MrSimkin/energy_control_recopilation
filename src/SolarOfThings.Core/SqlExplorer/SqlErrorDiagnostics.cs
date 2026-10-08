using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Structured error feedback. SQLite's managed exception does not guarantee
/// a parser byte offset; only point to an unambiguous occurrence of a named
/// table/column, and clearly label it as an approximate reference.
/// Never claim a guessed location is the exact SQL parser error position.
/// </summary>
public static class SqlErrorDiagnostics
{
    private static readonly Regex NamedObject = new(
        @"(?:no such (?:table|column)|ambiguous column name):\s*(?<name>[\w.]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public static SqlErrorDetail Describe(Exception error, string sql)
    {
        ArgumentNullException.ThrowIfNull(error);
        var isSQLite = error is SqliteException;
        var sqlite = error as SqliteException;
        var message = error.Message;
        var code = sqlite?.SqliteErrorCode;
        var extended = sqlite?.SqliteExtendedErrorCode;
        int? offset = null;
        int? line = null;
        int? column = null;

        if (isSQLite && !string.IsNullOrEmpty(sql))
        {
            var named = NamedObject.Match(message);
            if (named.Success)
            {
                var term = named.Groups["name"].Value;
                // Search exact token boundaries. Multiple references or
                // textual literals are not reliable places to highlight.
                if (term.Length > 0 && term.Length <= 200)
                {
                    var occurrences = FindNamedOccurrences(sql, term);
                    if (occurrences.Count == 1)
                    {
                        offset = occurrences[0];
                        line = 1;
                        column = 1;
                        for (var i = 0; i < offset; i++)
                        {
                            if (sql[i] == '\n')
                            {
                                line++;
                                column = 1;
                            }
                            else column++;
                        }
                    }
                }
            }
        }

        return new SqlErrorDetail(
            isSQLite ? "SQLITE_ERROR" : "QUERY_ERROR",
            message, code, extended, offset, line, column,
            offset.HasValue
                ? "APPROXIMATE_IDENTIFIER_REFERENCE_NOT_PARSER_ERROR_OFFSET"
                : "NO_RELIABLE_ERROR_LOCATION");
    }

    private static IReadOnlyList<int> FindNamedOccurrences(string sql, string term)
    {
        var found = new List<int>();
        // Do not provide pseudo-positions within comments or literal strings.
        bool lineComment = false;
        bool blockComment = false;
        char literal = '\0';
        for (var i = 0; i < sql.Length;)
        {
            if (lineComment)
            {
                if (sql[i] == '\n') lineComment = false;
                i++;
                continue;
            }
            if (blockComment)
            {
                if (i + 1 < sql.Length && sql[i] == '*' && sql[i + 1] == '/')
                { blockComment = false; i += 2; }
                else i++;
                continue;
            }
            if (literal != '\0')
            {
                if (sql[i] == literal)
                {
                    if (i + 1 < sql.Length && sql[i + 1] == literal) i += 2;
                    else { literal = '\0'; i++; }
                }
                else i++;
                continue;
            }
            if (i + 1 < sql.Length && sql[i] == '-' && sql[i + 1] == '-')
            { lineComment = true; i += 2; continue; }
            if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
            { blockComment = true; i += 2; continue; }
            if (sql[i] == '\'')
            { literal = '\''; i++; continue; }
            if (i + term.Length <= sql.Length &&
                string.Compare(sql, i, term, 0, term.Length,
                    StringComparison.OrdinalIgnoreCase) == 0 &&
                (i == 0 || !IsIdentifierPart(sql[i - 1])) &&
                (i + term.Length == sql.Length ||
                 !IsIdentifierPart(sql[i + term.Length])))
            {
                found.Add(i);
                if (found.Count > 1) return found;
                i += term.Length;
                continue;
            }
            i++;
        }
        return found;
    }

    private static bool IsIdentifierPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '.';
}

public sealed record SqlErrorDetail(
    string Category, string Message,
    int? SqliteCode, int? SqliteExtendedCode,
    int? ApproximateOffset, int? ApproximateLine,
    int? ApproximateColumn, string LocationStatus);

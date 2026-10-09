using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>Strict local SELECT console; never opens a writable connection.</summary>
public sealed class SafeSqlExplorerService
{
    public const int PreviewLimit = 200;
    public const int MaxExportRows = 1_000_000;
    // One worksheet allows 1,048,576 rows; row 1 is the header.
    public const int MaxExcelRows = 1_000_000;
    private const int MaxColumns = 128;
    private const int MaxCellBytes = 4 * 1024 * 1024;
    private readonly string _databasePath;

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "abs", "avg", "count", "sum", "total", "min", "max", "coalesce",
        "ifnull", "nullif", "round", "trim", "ltrim", "rtrim", "lower",
        "upper", "length", "substr", "substring", "replace", "instr",
        "hex", "typeof", "quote", "printf", "date", "time", "datetime",
        "julianday", "unixepoch", "strftime", "like", "glob", "group_concat",
        "iif", "sign", "floor", "ceil", "ceiling", "row_number",
        "rank", "dense_rank", "lag", "lead", "first_value",
        "last_value", "nth_value", "ntile", "percent_rank", "cume_dist",
        "json_extract", "json_type", "json_valid", "json_array_length"
    };
    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "REPLACE", "DROP", "ALTER",
        "CREATE", "ATTACH", "DETACH", "PRAGMA", "VACUUM", "REINDEX",
        "ANALYZE", "EXPLAIN", "BEGIN", "COMMIT", "ROLLBACK",
        "SAVEPOINT", "RELEASE", "TRIGGER", "LOAD_EXTENSION"
    };

    public SafeSqlExplorerService(string databasePath) =>
        _databasePath = Path.GetFullPath(databasePath);

    // Capped preview paging avoids materializing the user's potentially
    // multi-gigabyte dataset. Exact page order is only stable when the SQL
    // query itself uses a deterministic ORDER BY.
    public const int MaxPreviewOffset = 50_000;

    public Task<SqlPreviewResult> PreviewAsync(string sql,
        int maxRows = PreviewLimit, CancellationToken cancellationToken = default) =>
        PreviewPageAsync(sql, offset: 0, maxRows, cancellationToken);

    public Task<SqlPreviewResult> PreviewPageAsync(string sql, int offset,
        int maxRows = PreviewLimit, CancellationToken cancellationToken = default)
    {
        if (maxRows is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(maxRows));
        if (offset < 0 || offset > MaxPreviewOffset || offset % maxRows != 0)
            throw new ArgumentOutOfRangeException(nameof(offset),
                "Page offset must be a nonnegative multiple of page size, up to 50,000.");
        GuardSingleSelect(sql);
        return Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            using var scope = OpenProtected(cancellationToken, TimeSpan.FromSeconds(20));
            using var command = scope.Connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 15;
            using var reader = command.ExecuteReader();
            var names = GetColumns(reader);
            // Do not keep or parse the skipped rows. The SQLite progress
            // handler and explicit cancellation apply during the scan.
            for (var scanned = 0; scanned < offset; scanned++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!reader.Read())
                    return new SqlPreviewResult(names, [], false, watch.Elapsed);
            }
            var rows = new List<IReadOnlyList<SqlCell>>(maxRows);
            while (rows.Count < maxRows && reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(GetRow(reader));
            }
            // Do not scan beyond the configured upper offset, and do not
            // read a potentially large extra cell merely to find next page.
            var more = offset + maxRows <= MaxPreviewOffset &&
                       rows.Count == maxRows && reader.Read();
            return new SqlPreviewResult(names, rows, more, watch.Elapsed);
        }, cancellationToken);
    }

    public Task<SqlPreviewResult> SchemaAsync(CancellationToken cancellationToken = default) =>
        PreviewAsync("""
            SELECT type, name, tbl_name
            FROM sqlite_master
            WHERE type IN ('view','table') AND name NOT LIKE 'sqlite_%'
            ORDER BY type, name
            """, 250, cancellationToken);

    public Task<SqlExportResult> ExportAsync(string sql, string filename,
        SqlExportFormat format, CancellationToken cancellationToken = default)
    {
        GuardSingleSelect(sql);
        var target = Path.GetFullPath(filename);
        var ext = Path.GetExtension(target);
        if (format == SqlExportFormat.Csv &&
            !ext.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
            format == SqlExportFormat.Xlsx &&
            !ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Export extension does not match format.");
        if (File.Exists(target))
            throw new IOException("Refusing to overwrite an existing file.");
        return Task.Run(() => ExportCore(sql, target, format, cancellationToken),
            cancellationToken);
    }

    private SqlExportResult ExportCore(string sql, string output,
        SqlExportFormat format, CancellationToken token)
    {
        var parent = Path.GetDirectoryName(output)!;
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException("The selected output folder is unavailable.");
        var temporary = Path.Combine(parent, ".solar-sql-export-" +
            Guid.NewGuid().ToString("N") + Path.GetExtension(output));
        var operationWatch = Stopwatch.StartNew();
        try
        {
            using var scope = OpenProtected(token, TimeSpan.FromMinutes(10));
            using var cmd = scope.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 600;
            using var reader = cmd.ExecuteReader();
            var cols = GetColumns(reader);
            int count = 0;
            if (format == SqlExportFormat.Csv)
            {
                using var file = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(file, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", cols.Select(Csv)));
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    if (count >= MaxExportRows)
                        throw new InvalidOperationException("Row limit exceeded; export not published.");
                    var row = GetRow(reader);
                    writer.WriteLine(string.Join(",", row.Select(cell =>
                        Csv(cell.IsNull ? @"\N" : FormulaSafe(cell.Text)))));
                    count++;
                }
            }
            else
            {
                // Low-memory OOXML streaming; never materializes a 1M-row workbook.
                // One million data rows + headers fits under Excel's 1,048,576.
                count = StreamingSqlXlsxWriter.Write(temporary, cols,
                    () =>
                    {
                        token.ThrowIfCancellationRequested();
                        return reader.Read() ? GetRow(reader) : null;
                    },
                    MaxExcelRows, token);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, output); // no overwrite; only complete exports published
            return new SqlExportResult(output, format, count, cols.Count, "COMPLETE",
                operationWatch.Elapsed, new FileInfo(output).Length);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private ProtectedSession OpenProtected(CancellationToken cancellation, TimeSpan duration)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!File.Exists(_databasePath))
            throw new FileNotFoundException("Database does not exist.", _databasePath);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath, Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private, Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            using (var setup = connection.CreateCommand())
            {
                setup.CommandText = "PRAGMA query_only = ON;";
                setup.ExecuteNonQuery();
            }
            strdelegate_authorizer authorize = (_, action, a, b, db, _) =>
            {
                if (action == raw.SQLITE_SELECT || action == raw.SQLITE_RECURSIVE)
                    return raw.SQLITE_OK;
                if (action == raw.SQLITE_READ)
                    return string.IsNullOrEmpty(db) ||
                        db.Equals("main", StringComparison.OrdinalIgnoreCase)
                        ? raw.SQLITE_OK : raw.SQLITE_DENY;
                if (action == raw.SQLITE_FUNCTION)
                    return Functions.Contains(string.IsNullOrWhiteSpace(b) ? a : b)
                        ? raw.SQLITE_OK : raw.SQLITE_DENY;
                return raw.SQLITE_DENY;
            };
            if (raw.sqlite3_set_authorizer(connection.Handle, authorize, null!) != raw.SQLITE_OK)
                throw new InvalidOperationException("SQLite native authorizer is unavailable.");
            var watch = Stopwatch.StartNew();
            delegate_progress progress = _ =>
                cancellation.IsCancellationRequested || watch.Elapsed > duration ? 1 : 0;
            raw.sqlite3_progress_handler(connection.Handle, 1000, progress, null!);
            return new ProtectedSession(connection, authorize, progress);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<string> GetColumns(SqliteDataReader reader)
    {
        if (reader.FieldCount is < 1 or > MaxColumns)
            throw new InvalidDataException("No columns or too many result columns.");
        return Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
    }

    private static IReadOnlyList<SqlCell> GetRow(SqliteDataReader reader)
    {
        var result = new SqlCell[reader.FieldCount];
        for (var i = 0; i < result.Length; i++)
        {
            if (reader.IsDBNull(i)) { result[i] = new(null, "NULL", true); continue; }
            object value = reader.GetValue(i);
            switch (value)
            {
                case byte[] blob:
                    if (blob.Length > MaxCellBytes)
                        throw new InvalidDataException("Oversized binary value.");
                    result[i] = new(Convert.ToHexString(blob), "BLOB_HEX", false);
                    break;
                case long integer:
                    result[i] = new(integer.ToString(CultureInfo.InvariantCulture),
                        "INTEGER", false);
                    break;
                case double real:
                    result[i] = new(real.ToString("R", CultureInfo.InvariantCulture),
                        "REAL", false);
                    break;
                default:
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                    if (Encoding.UTF8.GetByteCount(text) > MaxCellBytes)
                        throw new InvalidDataException("Oversized text value.");
                    result[i] = new(text, "TEXT", false);
                    break;
            }
        }
        return result;
    }

    private static string Csv(string? text) =>
        "\"" + (text ?? "").Replace("\"", "\"\"") + "\"";
    private static string FormulaSafe(string? text)
    {
        var value = text ?? "";
        var trimmed = value.TrimStart();
        return value.Length > 0 &&
            (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ||
             trimmed.StartsWith('=') || trimmed.StartsWith('@'))
            ? "'" + value : value;
    }

    /// <summary>Scanner ignores strings/comments when checking one SELECT.</summary>
    public static void GuardSingleSelect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > 20_000 ||
            sql.IndexOf('\0') >= 0)
            throw new ArgumentException("Empty or invalid SQL.");
        var tokens = new List<string>();
        var ended = false;
        for (int i = 0; i < sql.Length;)
        {
            char c = sql[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (i + 1 < sql.Length && c == '-' && sql[i + 1] == '-')
            {
                i += 2;
                while (i < sql.Length && sql[i] != '\n') i++;
                continue;
            }
            if (i + 1 < sql.Length && c == '/' && sql[i + 1] == '*')
            {
                var close = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0) throw new ArgumentException("Unterminated SQL comment.");
                i = close + 2; continue;
            }
            if (c is '\'' or '"' or '[' || c == (char)96)
            {
                var delimiter = c == '[' ? ']' : c;
                i++;
                bool closed = false;
                while (i < sql.Length)
                {
                    if (sql[i] == delimiter)
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == delimiter)
                        { i += 2; continue; }
                        i++; closed = true; break;
                    }
                    i++;
                }
                if (!closed) throw new ArgumentException("Unterminated quoted text.");
                if (ended) throw new ArgumentException("Multiple statements are forbidden.");
                continue;
            }
            if (c == ';') { if (ended) throw new ArgumentException("Multiple statements.");
                ended = true; i++; continue; }
            if (ended) throw new ArgumentException("Multiple statements are forbidden.");
            if (char.IsLetter(c) || c == '_')
            {
                var start = i++;
                while (i < sql.Length &&
                    (char.IsLetterOrDigit(sql[i]) || sql[i] == '_')) i++;
                var token = sql[start..i];
                if (Forbidden.Contains(token))
                    throw new ArgumentException("Forbidden SQL operation: " + token);
                tokens.Add(token);
            }
            else i++;
        }
        if (tokens.Count == 0 ||
            !(tokens[0].Equals("SELECT", StringComparison.OrdinalIgnoreCase) ||
              tokens[0].Equals("WITH", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Only SELECT and read-only WITH queries are supported.");
    }

    private sealed class ProtectedSession : IDisposable
    {
        public SqliteConnection Connection { get; }
        private readonly strdelegate_authorizer _authorize;
        private readonly delegate_progress _progress;
        public ProtectedSession(SqliteConnection connection,
            strdelegate_authorizer authorize, delegate_progress progress)
        { Connection = connection; _authorize = authorize; _progress = progress; }
        public void Dispose()
        {
            Connection.Dispose();
            GC.KeepAlive(_authorize);
            GC.KeepAlive(_progress);
        }
    }
}
public enum SqlExportFormat { Csv, Xlsx }
public sealed record SqlCell(string? Text, string Type, bool IsNull);
public sealed record SqlPreviewResult(IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<SqlCell>> Rows, bool HasMore, TimeSpan Elapsed);
public sealed record SqlExportResult(string Path, SqlExportFormat Format,
    int Rows, int Columns, string Status, TimeSpan Elapsed, long FileSizeBytes);

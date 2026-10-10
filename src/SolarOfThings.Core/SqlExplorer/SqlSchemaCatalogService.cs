using Microsoft.Data.Sqlite;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Explicit column-level schema inspection; no SQL text from the user is
/// executed. This is metadata, NOT a table scan or an energy computation.
/// </summary>
public sealed class SqlSchemaCatalogService
{
    private readonly string _databasePath;
    private const int MaxColumns = 256;

    public SqlSchemaCatalogService(string databasePath) =>
        _databasePath = Path.GetFullPath(databasePath);

    public Task<SqlCatalogObject> DescribeAsync(
        string requestedName, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(requestedName) ||
            requestedName.Length > 200 || requestedName.StartsWith("sqlite_",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an ordinary table or view from the catalog.");
        token.ThrowIfCancellationRequested();
        return Task.Run(() => Read(requestedName, token), token);
    }

    private SqlCatalogObject Read(string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(_databasePath))
            throw new FileNotFoundException("Catalog SQLite source is missing.", _databasePath);
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath, Mode = SqliteOpenMode.ReadOnly,
            Pooling = false, Cache = SqliteCacheMode.Private
        }.ToString());
        db.Open();
        using (var safe = db.CreateCommand())
        {
            safe.CommandText = "PRAGMA query_only=ON;";
            safe.ExecuteNonQuery();
        }
        string kind;
        using (var verify = db.CreateCommand())
        {
            // Strictly bind the catalog object; do not interpolate a name
            // received from a script, user input, or a malicious database.
            verify.CommandText = """
                SELECT type FROM sqlite_master
                WHERE name=$name AND type IN ('table','view')
                  AND name NOT LIKE 'sqlite_%'
                LIMIT 1;
                """;
            verify.Parameters.AddWithValue("$name", name);
            kind = (string?)verify.ExecuteScalar() ??
                throw new ArgumentException("Table/view is not in this database catalog.");
        }
        token.ThrowIfCancellationRequested();
        var fields = new List<SqlCatalogColumn>();
        using (var metadata = db.CreateCommand())
        {
            // This fixed, SQLite-supported, read-only *metadata PRAGMA*
            // accepts a safely double-quoted validated catalog identifier.
            // It is NOT passed to the unrestricted SQL console.
            metadata.CommandText = "PRAGMA table_xinfo(\"" +
                name.Replace("\"", "\"\"") + "\");";
            using var reader = metadata.ExecuteReader();
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                if (fields.Count >= MaxColumns)
                    throw new InvalidDataException("Schema catalog column limit exceeded.");
                // table_xinfo: cid,name,type,notnull,dflt_value,pk,hidden
                fields.Add(new SqlCatalogColumn(
                    reader.GetString(1), reader.GetString(2),
                    reader.GetInt64(3) != 0, reader.GetInt64(5) != 0,
                    reader.GetInt64(6) != 0));
            }
        }
        return new SqlCatalogObject(kind, name, fields, Explain(name));
    }

    private static string? Explain(string name) => name switch
    {
        "reporting_grid_import" =>
            "Sampled grid power in W at original UTC timestamps; NOT integrated kWh.",
        "reporting_hourly_power_samples" =>
            "Arithmetic sample statistics in W by UTC hour; NOT energy.",
        "reporting_daily_power_samples" =>
            "Arithmetic sample statistics in W by UTC date; NOT a station-local day or kWh.",
        "reporting_battery" =>
            "Original battery SOC (%), voltage (V) and power (W) at stored timestamps.",
        "reporting_utility_bills" =>
            "Values printed on saved utility bills; NOT independently estimated tariffs.",
        "reporting_bill_line_evidence" =>
            "Stored utility-bill line amounts, categories and evidence states.",
        "data_quality_summary" =>
            "Recorded collection coverage per local day; NOT physical energy coverage.",
        _ => null
    };
}

public sealed record SqlCatalogObject(
    string Kind, string Name,
    IReadOnlyList<SqlCatalogColumn> Columns, string? SemanticWarning);

public sealed record SqlCatalogColumn(
    string Name, string DeclaredType, bool NotNull, bool PrimaryKey, bool Hidden);

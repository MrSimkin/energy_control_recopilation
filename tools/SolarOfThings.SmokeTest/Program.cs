using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;

var root = Path.Combine(
    Path.GetTempPath(),
    "SolarEnergyMonitorSmoke",
    Guid.NewGuid().ToString("N"));

try
{
    var paths = new AppPaths(root);
    var database = new SqliteDatabase(paths);

    database.Initialize();

    if (!File.Exists(database.DatabasePath))
    {
        throw new InvalidOperationException("SQLite database file was not created.");
    }

    if (database.GetSchemaVersion() != SqliteDatabase.CurrentSchemaVersion)
    {
        throw new InvalidOperationException("Unexpected SQLite schema version.");
    }

    Console.WriteLine($"Smoke test passed. Schema v{SqliteDatabase.CurrentSchemaVersion}.");
}
finally
{
    // Microsoft.Data.Sqlite connection pooling can keep the database file open
    // after individual connections are disposed. Clear the pool before deleting
    // this disposable smoke-test database.
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

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
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

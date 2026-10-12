using Microsoft.Data.Sqlite;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Validates a synthetic recovery target before dry-run comparison or staged
/// writes. This helper does not enable live recovery, change user databases,
/// or infer correctness solely from the schema_migration version.
/// </summary>
internal static class IsolatedRecoveryTargetHealth
{
    public static void RequireHealthy(SqliteConnection target)
    {
        ArgumentNullException.ThrowIfNull(target);
        using var command = target.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok",
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "Synthetic recovery target failed SQLite integrity_check.");

        command.CommandText = "PRAGMA foreign_key_check;";
        using var foreignKeys = command.ExecuteReader();
        if (foreignKeys.Read())
            throw new InvalidDataException(
                "Synthetic recovery target failed SQLite foreign_key_check.");
    }
}

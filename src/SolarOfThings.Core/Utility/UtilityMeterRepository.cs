using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Utility;

public sealed class UtilityMeterRepository
{
    private readonly SqliteDatabase _database;

    public UtilityMeterRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<UtilityMeterReading> GetReadings()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT reading_id, reading_at_utc, reading_kwh,
                   reference, notes, created_utc, updated_utc
            FROM utility_meter_reading
            ORDER BY reading_at_utc;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<UtilityMeterReading>();
        while (reader.Read())
        {
            if (!TryReadInstant(reader.GetString(1), out var readingAt) ||
                !TryReadInstant(reader.GetString(5), out var createdAt) ||
                !TryReadInstant(reader.GetString(6), out var updatedAt))
            {
                continue;
            }

            result.Add(new UtilityMeterReading(
                reader.GetInt64(0),
                readingAt,
                reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                createdAt,
                updatedAt));
        }

        return result;
    }

    public long AddReading(
        DateTimeOffset readingAtUtc,
        double readingKwh,
        string? reference,
        string? notes)
    {
        if (!double.IsFinite(readingKwh) || readingKwh < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readingKwh),
                "Meter reading must be a finite non-negative cumulative kWh value.");
        }

        var now = DateTimeOffset.UtcNow;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_meter_reading (
                reading_at_utc, reading_kwh, reference, notes,
                created_utc, updated_utc
            )
            VALUES (
                $readingAtUtc, $readingKwh, $reference, $notes,
                $createdUtc, $updatedUtc
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue(
            "$readingAtUtc",
            readingAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$readingKwh", readingKwh);
        command.Parameters.AddWithValue(
            "$reference",
            string.IsNullOrWhiteSpace(reference) ? DBNull.Value : reference.Trim());
        command.Parameters.AddWithValue(
            "$notes",
            string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", now.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void DeleteReading(long readingId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM utility_meter_reading
            WHERE reading_id = $readingId;
            """;
        command.Parameters.AddWithValue("$readingId", readingId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UtilityBillRecord> GetBills()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT bill_id, period_start_utc, period_end_utc,
                   billed_consumption_kwh, amount_clp,
                   invoice_reference, notes, created_utc, updated_utc
            FROM utility_bill
            ORDER BY period_start_utc DESC, bill_id DESC;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<UtilityBillRecord>();
        while (reader.Read())
        {
            if (!TryReadInstant(reader.GetString(1), out var start) ||
                !TryReadInstant(reader.GetString(2), out var end) ||
                !TryReadInstant(reader.GetString(7), out var createdAt) ||
                !TryReadInstant(reader.GetString(8), out var updatedAt))
            {
                continue;
            }

            result.Add(new UtilityBillRecord(
                reader.GetInt64(0),
                start,
                end,
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                createdAt,
                updatedAt));
        }

        return result;
    }

    public long AddBill(
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        double? billedConsumptionKwh,
        double? amountClp,
        string? invoiceReference,
        string? notes)
    {
        periodStartUtc = periodStartUtc.ToUniversalTime();
        periodEndUtc = periodEndUtc.ToUniversalTime();
        if (periodEndUtc <= periodStartUtc)
        {
            throw new ArgumentException(
                "Bill period end must be later than its start.");
        }

        ValidateOptionalNonNegative(
            billedConsumptionKwh,
            nameof(billedConsumptionKwh));
        ValidateOptionalNonNegative(
            amountClp,
            nameof(amountClp));

        var now = DateTimeOffset.UtcNow;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_bill (
                period_start_utc, period_end_utc,
                billed_consumption_kwh, amount_clp,
                invoice_reference, notes,
                created_utc, updated_utc
            )
            VALUES (
                $startUtc, $endUtc,
                $consumption, $amount,
                $reference, $notes,
                $createdUtc, $updatedUtc
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$startUtc", periodStartUtc.ToString("O"));
        command.Parameters.AddWithValue("$endUtc", periodEndUtc.ToString("O"));
        command.Parameters.AddWithValue(
            "$consumption",
            billedConsumptionKwh.HasValue ? billedConsumptionKwh.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "$amount",
            amountClp.HasValue ? amountClp.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "$reference",
            string.IsNullOrWhiteSpace(invoiceReference)
                ? DBNull.Value
                : invoiceReference.Trim());
        command.Parameters.AddWithValue(
            "$notes",
            string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", now.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void DeleteBill(long billId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM utility_bill
            WHERE bill_id = $billId;
            """;
        command.Parameters.AddWithValue("$billId", billId);
        command.ExecuteNonQuery();
    }

    private static void ValidateOptionalNonNegative(
        double? value,
        string parameterName)
    {
        if (value.HasValue &&
            (!double.IsFinite(value.Value) || value.Value < 0))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value must be finite and non-negative.");
        }
    }

    private static bool TryReadInstant(
        string raw,
        out DateTimeOffset value) =>
        DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
}

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
                   source_kind, time_precision, time_assumption,
                   reference, notes, created_utc, updated_utc
            FROM utility_meter_reading
            ORDER BY reading_at_utc;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<UtilityMeterReading>();
        while (reader.Read())
        {
            if (!TryReadInstant(reader.GetString(1), out var readingAt) ||
                !TryReadInstant(reader.GetString(8), out var createdAt) ||
                !TryReadInstant(reader.GetString(9), out var updatedAt))
            {
                continue;
            }

            result.Add(new UtilityMeterReading(
                reader.GetInt64(0),
                readingAt,
                reader.GetDouble(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                createdAt,
                updatedAt));
        }

        return result;
    }

    public UtilityMeterReading? GetReading(long readingId) =>
        GetReadings().FirstOrDefault(item => item.ReadingId == readingId);

    public long AddReading(
        DateTimeOffset readingAtUtc,
        double readingKwh,
        string? reference,
        string? notes,
        string sourceKind = UtilityReadingSourceKind.Personal,
        string timePrecision = UtilityTimePrecision.Exact,
        string timeAssumption = UtilityTimeAssumption.Exact)
    {
        ValidateReading(readingKwh, sourceKind, timePrecision, timeAssumption);

        var now = DateTimeOffset.UtcNow;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_meter_reading (
                reading_at_utc, reading_kwh,
                source_kind, time_precision, time_assumption,
                reference, notes, created_utc, updated_utc
            )
            VALUES (
                $readingAtUtc, $readingKwh,
                $sourceKind, $timePrecision, $timeAssumption,
                $reference, $notes, $createdUtc, $updatedUtc
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue(
            "$readingAtUtc",
            readingAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$readingKwh", readingKwh);
        command.Parameters.AddWithValue("$sourceKind", sourceKind);
        command.Parameters.AddWithValue("$timePrecision", timePrecision);
        command.Parameters.AddWithValue("$timeAssumption", timeAssumption);
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

    public void UpdateReading(
        long readingId,
        DateTimeOffset readingAtUtc,
        double readingKwh,
        string sourceKind,
        string timePrecision,
        string timeAssumption,
        string? reference,
        string? notes)
    {
        ValidateReading(readingKwh, sourceKind, timePrecision, timeAssumption);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE utility_meter_reading
            SET reading_at_utc = $readingAtUtc,
                reading_kwh = $readingKwh,
                source_kind = $sourceKind,
                time_precision = $timePrecision,
                time_assumption = $timeAssumption,
                reference = $reference,
                notes = $notes,
                updated_utc = $updatedUtc
            WHERE reading_id = $readingId;
            """;
        command.Parameters.AddWithValue("$readingId", readingId);
        command.Parameters.AddWithValue("$readingAtUtc", readingAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$readingKwh", readingKwh);
        command.Parameters.AddWithValue("$sourceKind", sourceKind);
        command.Parameters.AddWithValue("$timePrecision", timePrecision);
        command.Parameters.AddWithValue("$timeAssumption", timeAssumption);
        command.Parameters.AddWithValue("$reference", string.IsNullOrWhiteSpace(reference) ? DBNull.Value : reference.Trim());
        command.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
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
                   invoice_reference, notes,
                   from_reading_id, to_reading_id,
                   meter_start_kwh, meter_end_kwh, tariff_plan,
                   taxable_amount_clp, iva_clp, exempt_amount_clp,
                   gross_bill_amount_clp, other_charges_clp, total_due_clp,
                   period_precision,
                   created_utc, updated_utc
            FROM utility_bill
            ORDER BY period_start_utc DESC, bill_id DESC;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<UtilityBillRecord>();
        while (reader.Read())
        {
            if (!TryReadInstant(reader.GetString(1), out var start) ||
                !TryReadInstant(reader.GetString(2), out var end) ||
                !TryReadInstant(reader.GetString(19), out var createdAt) ||
                !TryReadInstant(reader.GetString(20), out var updatedAt))
            {
                continue;
            }

            result.Add(new UtilityBillRecord(
                reader.GetInt64(0),
                start,
                end,
                ReadNullableDouble(reader, 3),
                ReadNullableDouble(reader, 4),
                ReadNullableString(reader, 5),
                ReadNullableString(reader, 6),
                ReadNullableInt64(reader, 7),
                ReadNullableInt64(reader, 8),
                ReadNullableDouble(reader, 9),
                ReadNullableDouble(reader, 10),
                ReadNullableString(reader, 11),
                ReadNullableDouble(reader, 12),
                ReadNullableDouble(reader, 13),
                ReadNullableDouble(reader, 14),
                ReadNullableDouble(reader, 15),
                ReadNullableDouble(reader, 16),
                ReadNullableDouble(reader, 17),
                reader.GetString(18),
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
        string? notes,
        long? fromReadingId = null,
        long? toReadingId = null,
        double? meterStartKwh = null,
        double? meterEndKwh = null,
        string? tariffPlan = null,
        double? taxableAmountClp = null,
        double? ivaClp = null,
        double? exemptAmountClp = null,
        double? grossBillAmountClp = null,
        double? otherChargesClp = null,
        double? totalDueClp = null,
        string periodPrecision = UtilityTimePrecision.Exact)
    {
        periodStartUtc = periodStartUtc.ToUniversalTime();
        periodEndUtc = periodEndUtc.ToUniversalTime();
        if (periodEndUtc <= periodStartUtc)
            throw new ArgumentException("Bill period end must be later than its start.");

        foreach (var pair in new[]
        {
            (billedConsumptionKwh, nameof(billedConsumptionKwh)),
            (amountClp, nameof(amountClp)),
            (meterStartKwh, nameof(meterStartKwh)),
            (meterEndKwh, nameof(meterEndKwh)),
            (taxableAmountClp, nameof(taxableAmountClp)),
            (ivaClp, nameof(ivaClp)),
            (exemptAmountClp, nameof(exemptAmountClp)),
            (grossBillAmountClp, nameof(grossBillAmountClp)),
            (totalDueClp, nameof(totalDueClp))
        })
        {
            ValidateOptionalNonNegative(pair.Item1, pair.Item2);
        }

        if (otherChargesClp.HasValue && !double.IsFinite(otherChargesClp.Value))
            throw new ArgumentOutOfRangeException(nameof(otherChargesClp));

        var now = DateTimeOffset.UtcNow;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_bill (
                period_start_utc, period_end_utc,
                billed_consumption_kwh, amount_clp,
                invoice_reference, notes,
                from_reading_id, to_reading_id,
                meter_start_kwh, meter_end_kwh, tariff_plan,
                taxable_amount_clp, iva_clp, exempt_amount_clp,
                gross_bill_amount_clp, other_charges_clp, total_due_clp,
                period_precision,
                created_utc, updated_utc
            )
            VALUES (
                $startUtc, $endUtc,
                $consumption, $amount,
                $reference, $notes,
                $fromReadingId, $toReadingId,
                $meterStart, $meterEnd, $tariffPlan,
                $taxable, $iva, $exempt,
                $gross, $otherCharges, $totalDue,
                $periodPrecision,
                $createdUtc, $updatedUtc
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$startUtc", periodStartUtc.ToString("O"));
        command.Parameters.AddWithValue("$endUtc", periodEndUtc.ToString("O"));
        command.Parameters.AddWithValue("$consumption", billedConsumptionKwh.HasValue ? billedConsumptionKwh.Value : DBNull.Value);
        command.Parameters.AddWithValue("$amount", amountClp.HasValue ? amountClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$reference", string.IsNullOrWhiteSpace(invoiceReference) ? DBNull.Value : invoiceReference.Trim());
        command.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
        command.Parameters.AddWithValue("$fromReadingId", fromReadingId.HasValue ? fromReadingId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$toReadingId", toReadingId.HasValue ? toReadingId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$meterStart", meterStartKwh.HasValue ? meterStartKwh.Value : DBNull.Value);
        command.Parameters.AddWithValue("$meterEnd", meterEndKwh.HasValue ? meterEndKwh.Value : DBNull.Value);
        command.Parameters.AddWithValue("$tariffPlan", string.IsNullOrWhiteSpace(tariffPlan) ? DBNull.Value : tariffPlan.Trim());
        command.Parameters.AddWithValue("$taxable", taxableAmountClp.HasValue ? taxableAmountClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$iva", ivaClp.HasValue ? ivaClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$exempt", exemptAmountClp.HasValue ? exemptAmountClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$gross", grossBillAmountClp.HasValue ? grossBillAmountClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$otherCharges", otherChargesClp.HasValue ? otherChargesClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$totalDue", totalDueClp.HasValue ? totalDueClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$periodPrecision", periodPrecision);
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", now.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<UtilityBillLine> GetBillLines(long billId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT bill_line_id, bill_id, section_key, category_key,
                   description, quantity, unit, unit_rate_clp,
                   amount_clp, tax_treatment, sort_order,
                   created_utc, updated_utc
            FROM utility_bill_line
            WHERE bill_id = $billId
            ORDER BY sort_order, bill_line_id;
            """;
        command.Parameters.AddWithValue("$billId", billId);

        using var reader = command.ExecuteReader();
        var result = new List<UtilityBillLine>();
        while (reader.Read())
        {
            if (!TryReadInstant(reader.GetString(11), out var createdAt) ||
                !TryReadInstant(reader.GetString(12), out var updatedAt))
                continue;

            result.Add(new UtilityBillLine(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                ReadNullableString(reader, 3),
                reader.GetString(4),
                ReadNullableDouble(reader, 5),
                ReadNullableString(reader, 6),
                ReadNullableDouble(reader, 7),
                reader.GetDouble(8),
                ReadNullableString(reader, 9),
                reader.GetInt32(10),
                createdAt,
                updatedAt));
        }
        return result;
    }

    public long AddBillLine(
        long billId,
        string sectionKey,
        string description,
        double amountClp,
        string? categoryKey = null,
        double? quantity = null,
        string? unit = null,
        double? unitRateClp = null,
        string? taxTreatment = null,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
            throw new ArgumentException("Section is required.", nameof(sectionKey));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (!double.IsFinite(amountClp))
            throw new ArgumentOutOfRangeException(nameof(amountClp));

        var now = DateTimeOffset.UtcNow;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_bill_line (
                bill_id, section_key, category_key, description,
                quantity, unit, unit_rate_clp, amount_clp,
                tax_treatment, sort_order, created_utc, updated_utc
            )
            VALUES (
                $billId, $section, $category, $description,
                $quantity, $unit, $unitRate, $amount,
                $taxTreatment, $sortOrder, $createdUtc, $updatedUtc
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$billId", billId);
        command.Parameters.AddWithValue("$section", sectionKey.Trim());
        command.Parameters.AddWithValue("$category", string.IsNullOrWhiteSpace(categoryKey) ? DBNull.Value : categoryKey.Trim());
        command.Parameters.AddWithValue("$description", description.Trim());
        command.Parameters.AddWithValue("$quantity", quantity.HasValue ? quantity.Value : DBNull.Value);
        command.Parameters.AddWithValue("$unit", string.IsNullOrWhiteSpace(unit) ? DBNull.Value : unit.Trim());
        command.Parameters.AddWithValue("$unitRate", unitRateClp.HasValue ? unitRateClp.Value : DBNull.Value);
        command.Parameters.AddWithValue("$amount", amountClp);
        command.Parameters.AddWithValue("$taxTreatment", string.IsNullOrWhiteSpace(taxTreatment) ? DBNull.Value : taxTreatment.Trim());
        command.Parameters.AddWithValue("$sortOrder", sortOrder);
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", now.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void DeleteBillLine(long billLineId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM utility_bill_line WHERE bill_line_id = $id;";
        command.Parameters.AddWithValue("$id", billLineId);
        command.ExecuteNonQuery();
    }

    public void DeleteBill(long billId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM utility_bill WHERE bill_id = $billId;";
        command.Parameters.AddWithValue("$billId", billId);
        command.ExecuteNonQuery();
    }

    private static void ValidateReading(
        double readingKwh,
        string sourceKind,
        string timePrecision,
        string timeAssumption)
    {
        if (!double.IsFinite(readingKwh) || readingKwh < 0)
            throw new ArgumentOutOfRangeException(nameof(readingKwh));
        if (string.IsNullOrWhiteSpace(sourceKind) ||
            string.IsNullOrWhiteSpace(timePrecision) ||
            string.IsNullOrWhiteSpace(timeAssumption))
            throw new ArgumentException("Reading provenance metadata is required.");
    }

    private static void ValidateOptionalNonNegative(double? value, string parameterName)
    {
        if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0))
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static double? ReadNullableDouble(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetDouble(index);

    private static string? ReadNullableString(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);

    private static long? ReadNullableInt64(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt64(index);

    private static bool TryReadInstant(string raw, out DateTimeOffset value) =>
        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
}

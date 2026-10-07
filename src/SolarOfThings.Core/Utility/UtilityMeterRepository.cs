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
                   source_kind, source_document_id, review_state, iva_rate,
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
                !TryReadInstant(reader.GetString(23), out var createdAt) ||
                !TryReadInstant(reader.GetString(24), out var updatedAt))
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
                reader.GetString(19),
                ReadNullableInt64(reader, 20),
                reader.GetString(21),
                ReadNullableDouble(reader, 22),
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
        string periodPrecision = UtilityTimePrecision.Exact,
        string sourceKind = UtilityBillSourceKind.Manual,
        long? sourceDocumentId = null,
        string reviewState = UtilityBillReviewState.Reviewed,
        double? ivaRate = null)
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
                source_kind, source_document_id, review_state, iva_rate,
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
                $sourceKind, $sourceDocumentId, $reviewState, $ivaRate,
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
        command.Parameters.AddWithValue("$sourceKind", sourceKind);
        command.Parameters.AddWithValue("$sourceDocumentId", sourceDocumentId.HasValue ? sourceDocumentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$reviewState", reviewState);
        command.Parameters.AddWithValue("$ivaRate", ivaRate.HasValue ? ivaRate.Value : DBNull.Value);
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
                   source_kind, evidence_state, source_page, source_text,
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
            if (!TryReadInstant(reader.GetString(15), out var createdAt) ||
                !TryReadInstant(reader.GetString(16), out var updatedAt))
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
                reader.GetString(11),
                reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetInt32(13),
                ReadNullableString(reader, 14),
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
        int sortOrder = 0,
        string sourceKind = UtilityBillSourceKind.Manual,
        string evidenceState = UtilityBillEvidenceState.UserEntered,
        int? sourcePage = null,
        string? sourceText = null)
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
                tax_treatment, sort_order,
                source_kind, evidence_state, source_page, source_text,
                created_utc, updated_utc
            )
            VALUES (
                $billId, $section, $category, $description,
                $quantity, $unit, $unitRate, $amount,
                $taxTreatment, $sortOrder,
                $sourceKind, $evidenceState, $sourcePage, $sourceText,
                $createdUtc, $updatedUtc
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
        command.Parameters.AddWithValue("$sourceKind", sourceKind);
        command.Parameters.AddWithValue("$evidenceState", evidenceState);
        command.Parameters.AddWithValue("$sourcePage", sourcePage.HasValue ? sourcePage.Value : DBNull.Value);
        command.Parameters.AddWithValue("$sourceText", string.IsNullOrWhiteSpace(sourceText) ? DBNull.Value : sourceText.Trim());
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", now.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public long AddBillDocument(
        string provider,
        string originalFileName,
        string localPdfPath,
        string contentSha256,
        long contentLength,
        int pageCount,
        string parserVersion,
        string? extractedText)
    {
        using var connection = _database.OpenConnection();

        using (var existing = connection.CreateCommand())
        {
            existing.CommandText = """
                SELECT document_id
                FROM utility_bill_document
                WHERE content_sha256 = $sha;
                """;
            existing.Parameters.AddWithValue("$sha", contentSha256);
            var value = existing.ExecuteScalar();
            if (value is not null && value != DBNull.Value)
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_bill_document (
                provider, original_file_name, local_pdf_path,
                content_sha256, content_length, page_count,
                parser_version, extracted_text, imported_utc
            )
            VALUES (
                $provider, $fileName, $path,
                $sha, $length, $pages,
                $parser, $text, $utc
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$fileName", originalFileName);
        command.Parameters.AddWithValue("$path", localPdfPath);
        command.Parameters.AddWithValue("$sha", contentSha256);
        command.Parameters.AddWithValue("$length", contentLength);
        command.Parameters.AddWithValue("$pages", pageCount);
        command.Parameters.AddWithValue("$parser", parserVersion);
        command.Parameters.AddWithValue("$text", string.IsNullOrWhiteSpace(extractedText) ? DBNull.Value : extractedText);
        command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public UtilityBillDocument? GetBillDocument(long documentId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT document_id, provider, original_file_name, local_pdf_path,
                   content_sha256, content_length, page_count,
                   parser_version, extracted_text, imported_utc
            FROM utility_bill_document
            WHERE document_id = $id;
            """;
        command.Parameters.AddWithValue("$id", documentId);
        using var reader = command.ExecuteReader();
        if (!reader.Read() ||
            !TryReadInstant(reader.GetString(9), out var imported))
        {
            return null;
        }

        return new UtilityBillDocument(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetInt32(6),
            reader.GetString(7),
            ReadNullableString(reader, 8),
            imported);
    }

    public void UpsertBillFieldEvidence(
        long billId,
        string fieldKey,
        string sourceKind,
        string evidenceState,
        string? printedValueText,
        string? normalizedValueText,
        int? sourcePage = null,
        string? sourceText = null)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO utility_bill_field_evidence (
                bill_id, field_key, source_kind, evidence_state,
                printed_value_text, normalized_value_text,
                source_page, source_text, created_utc, updated_utc
            )
            VALUES (
                $billId, $fieldKey, $sourceKind, $state,
                $printed, $normalized,
                $page, $sourceText, $now, $now
            )
            ON CONFLICT(bill_id, field_key) DO UPDATE SET
                source_kind = excluded.source_kind,
                evidence_state = excluded.evidence_state,
                printed_value_text = excluded.printed_value_text,
                normalized_value_text = excluded.normalized_value_text,
                source_page = excluded.source_page,
                source_text = excluded.source_text,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$billId", billId);
        command.Parameters.AddWithValue("$fieldKey", fieldKey);
        command.Parameters.AddWithValue("$sourceKind", sourceKind);
        command.Parameters.AddWithValue("$state", evidenceState);
        command.Parameters.AddWithValue("$printed", string.IsNullOrWhiteSpace(printedValueText) ? DBNull.Value : printedValueText);
        command.Parameters.AddWithValue("$normalized", string.IsNullOrWhiteSpace(normalizedValueText) ? DBNull.Value : normalizedValueText);
        command.Parameters.AddWithValue("$page", sourcePage.HasValue ? sourcePage.Value : DBNull.Value);
        command.Parameters.AddWithValue("$sourceText", string.IsNullOrWhiteSpace(sourceText) ? DBNull.Value : sourceText);
        command.Parameters.AddWithValue("$now", now);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UtilityBillFieldEvidence> GetBillFieldEvidence(
        long billId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT evidence_id, bill_id, field_key, source_kind,
                   evidence_state, printed_value_text, normalized_value_text,
                   source_page, source_text, created_utc, updated_utc
            FROM utility_bill_field_evidence
            WHERE bill_id = $billId
            ORDER BY field_key;
            """;
        command.Parameters.AddWithValue("$billId", billId);
        using var reader = command.ExecuteReader();
        var result = new List<UtilityBillFieldEvidence>();
        while (reader.Read())
        {
            if (!TryReadInstant(reader.GetString(9), out var created) ||
                !TryReadInstant(reader.GetString(10), out var updated))
                continue;

            result.Add(new UtilityBillFieldEvidence(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                ReadNullableString(reader, 5),
                ReadNullableString(reader, 6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                ReadNullableString(reader, 8),
                created,
                updated));
        }
        return result;
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

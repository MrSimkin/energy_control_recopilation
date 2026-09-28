using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Utility;

public sealed class TariffRateCandidateRepository
{
    private readonly SqliteDatabase _database;

    public TariffRateCandidateRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public void ReplaceForPublication(
        long publicationId,
        IReadOnlyList<ParsedTariffRateCandidate> candidates,
        string parserVersion)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM tariff_rate_candidate
                WHERE publication_id = $publicationId;
                """;
            delete.Parameters.AddWithValue("$publicationId", publicationId);
            delete.ExecuteNonQuery();
        }

        var createdUtc = DateTimeOffset.UtcNow.ToString("O");

        foreach (var item in candidates)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO tariff_rate_candidate (
                    publication_id,
                    page_number,
                    tariff_plan,
                    component_key,
                    printed_description,
                    unit,
                    network_type,
                    etr_band,
                    candidate_index,
                    net_rate_clp,
                    published_iva_column_clp,
                    source_text,
                    parser_version,
                    validation_state,
                    created_utc
                )
                VALUES (
                    $publicationId,
                    $pageNumber,
                    $tariffPlan,
                    $componentKey,
                    $printedDescription,
                    $unit,
                    $networkType,
                    $etrBand,
                    $candidateIndex,
                    $netRate,
                    $ivaColumn,
                    $sourceText,
                    $parserVersion,
                    $validationState,
                    $createdUtc
                );
                """;

            insert.Parameters.AddWithValue("$publicationId", publicationId);
            insert.Parameters.AddWithValue("$pageNumber", item.PageNumber);
            insert.Parameters.AddWithValue("$tariffPlan", item.TariffPlan);
            insert.Parameters.AddWithValue("$componentKey", item.ComponentKey);
            insert.Parameters.AddWithValue("$printedDescription", item.PrintedDescription);
            insert.Parameters.AddWithValue("$unit", (object?)item.Unit ?? DBNull.Value);
            insert.Parameters.AddWithValue("$networkType", (object?)item.NetworkType ?? DBNull.Value);
            insert.Parameters.AddWithValue("$etrBand", (object?)item.EtrBand ?? DBNull.Value);
            insert.Parameters.AddWithValue("$candidateIndex", item.CandidateIndex);
            insert.Parameters.AddWithValue("$netRate", (object?)item.NetRateClp ?? DBNull.Value);
            insert.Parameters.AddWithValue("$ivaColumn", (object?)item.PublishedIvaColumnClp ?? DBNull.Value);
            insert.Parameters.AddWithValue("$sourceText", item.SourceText);
            insert.Parameters.AddWithValue("$parserVersion", item.ParserVersion);
            insert.Parameters.AddWithValue("$validationState", item.ValidationState);
            insert.Parameters.AddWithValue("$createdUtc", createdUtc);
            insert.ExecuteNonQuery();
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE tariff_publication
                SET normalization_status = 'CANDIDATES_EXTRACTED',
                    normalization_parser_version = $parserVersion,
                    normalized_utc = $normalizedUtc,
                    updated_utc = $normalizedUtc
                WHERE publication_id = $publicationId;
                """;
            update.Parameters.AddWithValue("$publicationId", publicationId);
            update.Parameters.AddWithValue("$parserVersion", parserVersion);
            update.Parameters.AddWithValue("$normalizedUtc", createdUtc);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void MarkFailed(
        long publicationId,
        string parserVersion,
        string detail)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var status = $"FAILED: {detail}";
        var normalizedUtc = DateTimeOffset.UtcNow.ToString("O");

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM tariff_rate_candidate
                WHERE publication_id = $publicationId;
                """;
            delete.Parameters.AddWithValue("$publicationId", publicationId);
            delete.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE tariff_publication
                SET normalization_status = $status,
                    normalization_parser_version = $parserVersion,
                    normalized_utc = $normalizedUtc,
                    updated_utc = $normalizedUtc
                WHERE publication_id = $publicationId;
                """;
            command.Parameters.AddWithValue("$publicationId", publicationId);
            command.Parameters.AddWithValue("$parserVersion", parserVersion);
            command.Parameters.AddWithValue(
                "$status",
                status[..Math.Min(500, status.Length)]);
            command.Parameters.AddWithValue("$normalizedUtc", normalizedUtc);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<TariffRateCandidate> GetForPublication(long publicationId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                rate_candidate_id,
                publication_id,
                page_number,
                tariff_plan,
                component_key,
                printed_description,
                unit,
                network_type,
                etr_band,
                candidate_index,
                net_rate_clp,
                published_iva_column_clp,
                source_text,
                parser_version,
                validation_state,
                created_utc
            FROM tariff_rate_candidate
            WHERE publication_id = $publicationId
            ORDER BY page_number, component_key, network_type, etr_band, candidate_index;
            """;
        command.Parameters.AddWithValue("$publicationId", publicationId);

        using var reader = command.ExecuteReader();
        var result = new List<TariffRateCandidate>();

        while (reader.Read())
        {
            DateTimeOffset.TryParse(
                reader.GetString(15),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdUtc);

            result.Add(new TariffRateCandidate(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetDouble(10),
                reader.IsDBNull(11) ? null : reader.GetDouble(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetString(14),
                createdUtc));
        }

        return result;
    }
}

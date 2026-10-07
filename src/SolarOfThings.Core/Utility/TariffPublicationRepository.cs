using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Utility;

public sealed class TariffPublicationRepository
{
    private readonly SqliteDatabase _database;

    public TariffPublicationRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<TariffPublication> GetAll()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT publication_id, provider, category, title, source_url,
                   effective_from, is_retroactive, local_pdf_path,
                   content_sha256, content_length, page_count,
                   capture_status, captured_utc, updated_utc,
                   normalization_status, normalization_parser_version,
                   normalized_utc,
                   official_document_number,
                   official_publication_date,
                   corrects_official_document_number,
                   regulatory_metadata_source
            FROM tariff_publication
            ORDER BY
                CASE WHEN effective_from IS NULL THEN 1 ELSE 0 END,
                effective_from DESC,
                publication_id DESC;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<TariffPublication>();
        while (reader.Read())
        {
            DateOnly? effective = null;
            if (!reader.IsDBNull(5) &&
                DateOnly.TryParseExact(
                    reader.GetString(5),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                effective = parsedDate;
            }

            DateTimeOffset? captured = null;
            if (!reader.IsDBNull(12) &&
                DateTimeOffset.TryParse(
                    reader.GetString(12),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedCaptured))
            {
                captured = parsedCaptured;
            }

            DateTimeOffset.TryParse(
                reader.GetString(13),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var updated);

            DateTimeOffset? normalized = null;
            if (!reader.IsDBNull(16) &&
                DateTimeOffset.TryParse(
                    reader.GetString(16),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedNormalized))
            {
                normalized = parsedNormalized;
            }

            DateOnly? officialPublicationDate = null;
            if (!reader.IsDBNull(18) &&
                DateOnly.TryParseExact(
                    reader.GetString(18),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedOfficialDate))
            {
                officialPublicationDate = parsedOfficialDate;
            }

            result.Add(new TariffPublication(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                effective,
                reader.GetInt64(6) != 0,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetInt64(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                reader.GetString(11),
                captured,
                updated,
                reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                normalized,
                reader.IsDBNull(17) ? null : reader.GetString(17),
                officialPublicationDate,
                reader.IsDBNull(19) ? null : reader.GetString(19),
                reader.IsDBNull(20) ? null : reader.GetString(20)));
        }

        return result;
    }

    public long UpsertDiscovery(TariffPublicationDiscovery item)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tariff_publication (
                provider, category, title, source_url,
                effective_from, is_retroactive,
                capture_status, updated_utc,
                official_document_number,
                official_publication_date,
                corrects_official_document_number,
                regulatory_metadata_source
            )
            VALUES (
                $provider, $category, $title, $sourceUrl,
                $effectiveFrom, $retroactive,
                'DISCOVERED', $updatedUtc,
                $officialDocumentNumber,
                $officialPublicationDate,
                $correctsOfficialDocumentNumber,
                $regulatoryMetadataSource
            )
            ON CONFLICT(source_url) DO UPDATE SET
                provider = excluded.provider,
                category = excluded.category,
                title = excluded.title,
                effective_from = excluded.effective_from,
                is_retroactive = excluded.is_retroactive,
                official_document_number = COALESCE(
                    excluded.official_document_number,
                    tariff_publication.official_document_number),
                official_publication_date = COALESCE(
                    excluded.official_publication_date,
                    tariff_publication.official_publication_date),
                corrects_official_document_number = COALESCE(
                    excluded.corrects_official_document_number,
                    tariff_publication.corrects_official_document_number),
                regulatory_metadata_source = COALESCE(
                    excluded.regulatory_metadata_source,
                    tariff_publication.regulatory_metadata_source),
                updated_utc = excluded.updated_utc;
            SELECT publication_id
            FROM tariff_publication
            WHERE source_url = $sourceUrl;
            """;
        command.Parameters.AddWithValue("$provider", item.Provider);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$sourceUrl", item.SourceUrl);
        command.Parameters.AddWithValue(
            "$effectiveFrom",
            item.EffectiveFrom.HasValue
                ? item.EffectiveFrom.Value.ToString("yyyy-MM-dd")
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$retroactive",
            item.IsRetroactive ? 1 : 0);
        command.Parameters.AddWithValue("$updatedUtc", now);
        command.Parameters.AddWithValue(
            "$officialDocumentNumber",
            (object?)item.OfficialDocumentNumber ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$officialPublicationDate",
            item.OfficialPublicationDate.HasValue
                ? item.OfficialPublicationDate.Value.ToString("yyyy-MM-dd")
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$correctsOfficialDocumentNumber",
            (object?)item.CorrectsOfficialDocumentNumber ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$regulatoryMetadataSource",
            (object?)item.RegulatoryMetadataSource ?? DBNull.Value);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<TariffPublicationRelation>
        GetRelations()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT relation_id,
                   source_publication_id,
                   relation_type,
                   target_provider,
                   target_category,
                   target_official_document_number,
                   target_publication_id,
                   evidence_source_url,
                   evidence_text,
                   created_utc,
                   updated_utc
            FROM tariff_publication_relation
            ORDER BY source_publication_id, relation_id;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<TariffPublicationRelation>();
        while (reader.Read())
        {
            DateTimeOffset.TryParse(
                reader.GetString(9),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var created);
            DateTimeOffset.TryParse(
                reader.GetString(10),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var updated);

            result.Add(
                new TariffPublicationRelation(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6)
                        ? null
                        : reader.GetInt64(6),
                    reader.IsDBNull(7)
                        ? null
                        : reader.GetString(7),
                    reader.GetString(8),
                    created,
                    updated));
        }

        return result;
    }

    public void UpsertRelation(
        TariffPublicationRelationUpsert item)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO tariff_publication_relation (
                source_publication_id,
                relation_type,
                target_provider,
                target_category,
                target_official_document_number,
                target_publication_id,
                evidence_source_url,
                evidence_text,
                created_utc,
                updated_utc
            )
            VALUES (
                $sourcePublicationId,
                $relationType,
                $targetProvider,
                $targetCategory,
                $targetOfficialDocumentNumber,
                (
                    SELECT publication_id
                    FROM tariff_publication
                    WHERE provider = $targetProvider
                      AND category = $targetCategory
                      AND official_document_number =
                          $targetOfficialDocumentNumber
                    ORDER BY publication_id DESC
                    LIMIT 1
                ),
                $evidenceSourceUrl,
                $evidenceText,
                $createdUtc,
                $updatedUtc
            )
            ON CONFLICT(
                source_publication_id,
                relation_type,
                target_provider,
                target_category,
                target_official_document_number
            )
            DO UPDATE SET
                target_publication_id = excluded.target_publication_id,
                evidence_source_url = excluded.evidence_source_url,
                evidence_text = excluded.evidence_text,
                updated_utc = excluded.updated_utc;
            """;

        command.Parameters.AddWithValue(
            "$sourcePublicationId",
            item.SourcePublicationId);
        command.Parameters.AddWithValue(
            "$relationType",
            item.RelationType);
        command.Parameters.AddWithValue(
            "$targetProvider",
            item.TargetProvider);
        command.Parameters.AddWithValue(
            "$targetCategory",
            item.TargetCategory);
        command.Parameters.AddWithValue(
            "$targetOfficialDocumentNumber",
            item.TargetOfficialDocumentNumber);
        command.Parameters.AddWithValue(
            "$evidenceSourceUrl",
            (object?)item.EvidenceSourceUrl ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$evidenceText",
            item.EvidenceText);
        command.Parameters.AddWithValue(
            "$createdUtc",
            now);
        command.Parameters.AddWithValue(
            "$updatedUtc",
            now);

        command.ExecuteNonQuery();
    }

    public void ResolveRelationTargets()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE tariff_publication_relation
            SET target_publication_id = (
                SELECT target.publication_id
                FROM tariff_publication AS target
                WHERE target.provider =
                          tariff_publication_relation.target_provider
                  AND target.category =
                          tariff_publication_relation.target_category
                  AND target.official_document_number =
                          tariff_publication_relation.target_official_document_number
                ORDER BY target.publication_id DESC
                LIMIT 1
            ),
            updated_utc = $updatedUtc
            WHERE EXISTS (
                SELECT 1
                FROM tariff_publication AS target
                WHERE target.provider =
                          tariff_publication_relation.target_provider
                  AND target.category =
                          tariff_publication_relation.target_category
                  AND target.official_document_number =
                          tariff_publication_relation.target_official_document_number
            );
            """;
        command.Parameters.AddWithValue(
            "$updatedUtc",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void MarkCaptured(
        long publicationId,
        string localPdfPath,
        string sha256,
        long contentLength,
        int pageCount,
        IReadOnlyList<string> pageTexts)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE tariff_publication
                SET local_pdf_path = $localPath,
                    content_sha256 = $sha,
                    content_length = $length,
                    page_count = $pageCount,
                    capture_status = 'CAPTURED',
                    captured_utc = $capturedUtc,
                    normalization_status = 'NOT_NORMALIZED',
                    normalization_parser_version = NULL,
                    normalized_utc = NULL,
                    updated_utc = $updatedUtc
                WHERE publication_id = $publicationId;
                """;
            update.Parameters.AddWithValue("$publicationId", publicationId);
            update.Parameters.AddWithValue("$localPath", localPdfPath);
            update.Parameters.AddWithValue("$sha", sha256);
            update.Parameters.AddWithValue("$length", contentLength);
            update.Parameters.AddWithValue("$pageCount", pageCount);
            update.Parameters.AddWithValue("$capturedUtc", DateTimeOffset.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            update.ExecuteNonQuery();
        }

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM tariff_publication_page_text
                WHERE publication_id = $publicationId;
                """;
            delete.Parameters.AddWithValue("$publicationId", publicationId);
            delete.ExecuteNonQuery();
        }

        for (var index = 0; index < pageTexts.Count; index++)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO tariff_publication_page_text (
                    publication_id, page_number, page_text
                )
                VALUES ($publicationId, $pageNumber, $pageText);
                """;
            insert.Parameters.AddWithValue("$publicationId", publicationId);
            insert.Parameters.AddWithValue("$pageNumber", index + 1);
            insert.Parameters.AddWithValue("$pageText", pageTexts[index]);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void MarkFailed(long publicationId, string detail)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tariff_publication
            SET capture_status = $status,
                updated_utc = $updatedUtc
            WHERE publication_id = $publicationId;
            """;
        command.Parameters.AddWithValue(
            "$status",
            $"FAILED: {detail}"[..Math.Min(500, $"FAILED: {detail}".Length)]);
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$publicationId", publicationId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<string> GetPageTexts(long publicationId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT page_text
            FROM tariff_publication_page_text
            WHERE publication_id = $publicationId
            ORDER BY page_number;
            """;
        command.Parameters.AddWithValue("$publicationId", publicationId);

        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }
}

namespace SolarOfThings.Core.Utility;

public sealed record TariffPublication(
    long PublicationId,
    string Provider,
    string Category,
    string Title,
    string SourceUrl,
    DateOnly? EffectiveFrom,
    bool IsRetroactive,
    string? LocalPdfPath,
    string? ContentSha256,
    long? ContentLength,
    int? PageCount,
    string CaptureStatus,
    DateTimeOffset? CapturedUtc,
    DateTimeOffset UpdatedUtc,
    string NormalizationStatus,
    string? NormalizationParserVersion,
    DateTimeOffset? NormalizedUtc,
    string? OfficialDocumentNumber,
    DateOnly? OfficialPublicationDate,
    string? CorrectsOfficialDocumentNumber,
    string? RegulatoryMetadataSource);

public sealed record TariffPublicationDiscovery(
    string Provider,
    string Category,
    string Title,
    string SourceUrl,
    DateOnly? EffectiveFrom,
    bool IsRetroactive,
    string? OfficialDocumentNumber = null,
    DateOnly? OfficialPublicationDate = null,
    string? CorrectsOfficialDocumentNumber = null,
    string? RegulatoryMetadataSource = null);

public sealed record TariffCaptureResult(
    int Discovered,
    int Captured,
    int Failed,
    IReadOnlyList<string> Messages,
    int RetroactiveDetected,
    int MultiVersionPeriods,
    int NormalizedCandidates,
    int NormalizationFailures);

public sealed record ParsedTariffRateCandidate(
    int PageNumber,
    string TariffPlan,
    string ComponentKey,
    string PrintedDescription,
    string? Unit,
    string? NetworkType,
    string? EtrBand,
    int CandidateIndex,
    double? NetRateClp,
    double? PublishedIvaColumnClp,
    string SourceText,
    string ParserVersion,
    string ValidationState);

public sealed record TariffRateCandidate(
    long RateCandidateId,
    long PublicationId,
    int PageNumber,
    string TariffPlan,
    string ComponentKey,
    string PrintedDescription,
    string? Unit,
    string? NetworkType,
    string? EtrBand,
    int CandidateIndex,
    double? NetRateClp,
    double? PublishedIvaColumnClp,
    string SourceText,
    string ParserVersion,
    string ValidationState,
    DateTimeOffset CreatedUtc);

public sealed record TariffNormalizationResult(
    long PublicationId,
    int CandidateCount,
    int PagesWithCandidates,
    string ParserVersion);


public sealed record TariffPublicationRelation(
    long RelationId,
    long SourcePublicationId,
    string RelationType,
    string TargetProvider,
    string TargetCategory,
    string TargetOfficialDocumentNumber,
    long? TargetPublicationId,
    string? EvidenceSourceUrl,
    string EvidenceText,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record TariffPublicationRelationUpsert(
    long SourcePublicationId,
    string RelationType,
    string TargetProvider,
    string TargetCategory,
    string TargetOfficialDocumentNumber,
    string? EvidenceSourceUrl,
    string EvidenceText);

public sealed record TariffPublicationVersionResolution(
    long PublicationId,
    DateOnly? EffectiveFrom,
    string Status,
    long? PreferredPublicationId,
    string Detail);

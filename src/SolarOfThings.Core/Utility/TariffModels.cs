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
    DateTimeOffset? NormalizedUtc);

public sealed record TariffPublicationDiscovery(
    string Provider,
    string Category,
    string Title,
    string SourceUrl,
    DateOnly? EffectiveFrom,
    bool IsRetroactive);

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

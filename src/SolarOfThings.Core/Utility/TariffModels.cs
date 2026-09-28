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
    DateTimeOffset UpdatedUtc);

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
    IReadOnlyList<string> Messages);

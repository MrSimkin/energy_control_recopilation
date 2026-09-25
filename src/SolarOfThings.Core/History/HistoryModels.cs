namespace SolarOfThings.Core.History;

public sealed record HistorySample(
    string DeviceId,
    string AttributeKey,
    DateTimeOffset RecordedAtUtc,
    string? ValueJson,
    bool IsMissing,
    string Source,
    DateTimeOffset RetrievedUtc);

public sealed record HistoryDayStatus(
    string DeviceId,
    DateOnly LocalDate,
    string TimeZone,
    string Source,
    string Status,
    int FrameCount,
    int PageCount,
    DateTimeOffset? FirstAtUtc,
    DateTimeOffset? LastAtUtc,
    string? DetailJson,
    DateTimeOffset UpdatedUtc);

public sealed record HistorySyncProgress(
    string Stage,
    DateOnly? LocalDate,
    int CompletedDays,
    int TotalDays,
    int Frames,
    int Samples,
    string Message);

public sealed record HistorySyncResult(
    bool Cancelled,
    int DaysAttempted,
    int DaysCompleted,
    int Frames,
    int SamplesUpserted,
    int Pages,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    string Source);

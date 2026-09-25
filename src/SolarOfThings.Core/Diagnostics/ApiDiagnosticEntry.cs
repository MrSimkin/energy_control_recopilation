namespace SolarOfThings.Core.Diagnostics;

public sealed record ApiDiagnosticEntry(
    DateTimeOffset Timestamp,
    string CorrelationId,
    string Operation,
    string Step,
    string Method,
    string Endpoint,
    int Attempt,
    int? HttpStatus,
    string? ApiCode,
    string? ApiMessage,
    long DurationMilliseconds,
    string Outcome,
    string? RequestJson,
    string? ResponseJson,
    string? ExceptionType,
    string? ExceptionMessage)
{
    public string? RequestHeadersJson { get; init; }
    public string? ResponseHeadersJson { get; init; }
    public long? ResponseLengthBytes { get; init; }
    public string? ExceptionStackTrace { get; init; }
}

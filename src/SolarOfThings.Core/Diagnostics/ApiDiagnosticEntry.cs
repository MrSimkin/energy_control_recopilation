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
    string? ExceptionMessage);

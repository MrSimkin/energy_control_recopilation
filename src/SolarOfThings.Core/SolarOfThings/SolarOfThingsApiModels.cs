using System.Text.Json;

namespace SolarOfThings.Core.SolarOfThings;

public sealed record SolarApiResponse(
    int HttpStatus,
    string? Code,
    string? Message,
    JsonElement Data,
    JsonElement Root,
    string RawJson)
{
    public bool IsSuccess =>
        HttpStatus is >= 200 and < 300 &&
        (string.IsNullOrWhiteSpace(Code) || Code == "0");
}

public sealed record SolarSessionTokens(
    string AccessToken,
    string RefreshToken,
    string? AccessExpiresAt,
    string? RefreshExpiresAt,
    long? AccessExpiresInMilliseconds,
    string? UserId = null);

public sealed class SolarApiException : Exception
{
    public SolarApiException(
        string message,
        int? httpStatus = null,
        string? apiCode = null,
        string? apiMessage = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        HttpStatus = httpStatus;
        ApiCode = apiCode;
        ApiMessage = apiMessage;
    }

    public int? HttpStatus { get; }
    public string? ApiCode { get; }
    public string? ApiMessage { get; }
}

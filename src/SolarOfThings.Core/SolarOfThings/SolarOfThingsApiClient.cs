using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SolarOfThings.Core.Diagnostics;

namespace SolarOfThings.Core.SolarOfThings;

public sealed class SolarOfThingsApiClient : IDisposable
{
    public const string ProductionBaseUrl = "https://solar.siseli.com/apis/";

    private readonly HttpClient _httpClient;
    private readonly ApiDiagnosticsStore _diagnostics;
    private readonly IotOpenCredentialStore _credentialStore;

    public SolarOfThingsApiClient(
        ApiDiagnosticsStore diagnostics,
        IotOpenCredentialStore credentialStore)
    {
        _diagnostics = diagnostics;
        _credentialStore = credentialStore;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ProductionBaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<SolarSessionTokens> LoginAsync(
        string account,
        string password,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        if (!_credentialStore.TryRead(out var credential) || credential is null)
        {
            throw new SolarApiException(
                "Solar of Things IoT Open client credential is not configured locally. " +
                "Configure it in Advanced / Diagnostics before account login.");
        }

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        string passwordMd5;

        try
        {
            passwordMd5 = Convert.ToHexString(MD5.HashData(passwordBytes)).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }

        var body = SerializeCompact(new
        {
            account,
            password = passwordMd5
        });

        var secret = credential.SecretIsEncrypted
            ? IotOpenSigner.DecryptEmbeddedSecret(credential.AppId, credential.SecretValue)
            : credential.SecretValue;

        var nonce = IotOpenSigner.CreateNonce();
        var bodyHash = IotOpenSigner.ComputeBodyHash(body);
        var sign = IotOpenSigner.ComputeSignature(credential.AppId, nonce, bodyHash, secret);

        var headers = new Dictionary<string, string>
        {
            ["IOT-Open-AppID"] = credential.AppId,
            ["IOT-Open-Nonce"] = nonce,
            ["IOT-Open-Body-Hash"] = bodyHash,
            ["IOT-Open-Sign"] = sign,
            ["IOT-Time-Zone"] = timeZone,
            ["Origin"] = "https://solar.siseli.com",
            ["Referer"] = "https://solar.siseli.com/"
        };

        var response = await SendAsync(
            "Login",
            "Authenticate",
            HttpMethod.Post,
            "login/account",
            body,
            headers,
            token: null,
            cancellationToken);

        EnsureSuccess(response, "Solar of Things login failed.");

        var payload = response.Data.ValueKind == JsonValueKind.Object
            ? response.Data
            : response.Root;

        var access = GetString(payload, "accessToken", "iotToken", "token");
        var refresh = GetString(payload, "refreshToken");

        if (string.IsNullOrWhiteSpace(access))
        {
            throw new SolarApiException(
                "Login succeeded but no access token was present in the response.",
                response.HttpStatus,
                response.Code,
                response.Message);
        }

        return new SolarSessionTokens(
            access,
            refresh ?? string.Empty,
            GetString(payload, "accessTokenWillExpiredAt", "accessTokenExpiredAt"),
            GetString(payload, "refreshTokenWillExpiredAt", "refreshTokenExpiredAt"),
            GetInt64(payload, "accessTokenWillExpiredInMillis"));
    }

    public async Task<SolarSessionTokens> RefreshAsync(
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var body = SerializeCompact(new
        {
            accessToken,
            refreshToken
        });

        var response = await SendAsync(
            "Session",
            "RefreshToken",
            HttpMethod.Post,
            "login/refresh/access/token",
            body,
            headers: null,
            token: null,
            cancellationToken);

        EnsureSuccess(response, "Solar of Things token refresh failed.");

        var payload = response.Data.ValueKind == JsonValueKind.Object
            ? response.Data
            : response.Root;

        var newAccess = GetString(payload, "accessToken", "iotToken", "token");
        var newRefresh = GetString(payload, "refreshToken");

        if (string.IsNullOrWhiteSpace(newAccess))
        {
            throw new SolarApiException(
                "Refresh response did not contain an access token.",
                response.HttpStatus,
                response.Code,
                response.Message);
        }

        return new SolarSessionTokens(
            newAccess,
            newRefresh ?? refreshToken,
            GetString(payload, "accessTokenWillExpiredAt", "accessTokenExpiredAt"),
            GetString(payload, "refreshTokenWillExpiredAt", "refreshTokenExpiredAt"),
            GetInt64(payload, "accessTokenWillExpiredInMillis"));
    }

    public Task<SolarApiResponse> GetAuthorizedAsync(
        string operation,
        string step,
        string pathAndQuery,
        string accessToken,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            operation,
            step,
            HttpMethod.Get,
            pathAndQuery,
            body: null,
            headers: new Dictionary<string, string>
            {
                ["IOT-Time-Zone"] = timeZone,
                ["Accept-Language"] = "es-CL"
            },
            token: accessToken,
            cancellationToken);
    }

    public Task<SolarApiResponse> PostAuthorizedAsync(
        string operation,
        string step,
        string pathAndQuery,
        object? body,
        string accessToken,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            operation,
            step,
            HttpMethod.Post,
            pathAndQuery,
            body is null ? null : SerializeCompact(body),
            headers: new Dictionary<string, string>
            {
                ["IOT-Time-Zone"] = timeZone,
                ["Accept-Language"] = "es-CL"
            },
            token: accessToken,
            cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<SolarApiResponse> SendAsync(
        string operation,
        string step,
        HttpMethod method,
        string pathAndQuery,
        string? body,
        IReadOnlyDictionary<string, string>? headers,
        string? token,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var request = new HttpRequestMessage(method, pathAndQuery);

                if (body is not null)
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.TryAddWithoutValidation("IOT-Token", token);
                }

                if (headers is not null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken);

                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                stopwatch.Stop();

                var parsed = ParseResponse((int)response.StatusCode, raw);

                IEnumerable<KeyValuePair<string, IEnumerable<string>>> requestHeaderPairs =
                    request.Headers;

                if (request.Content is not null)
                {
                    requestHeaderPairs = requestHeaderPairs.Concat(request.Content.Headers);
                }

                var requestHeaders = requestHeaderPairs.ToDictionary(
                    header => header.Key,
                    header => string.Join(",", header.Value),
                    StringComparer.OrdinalIgnoreCase);

                var responseHeaders = response.Headers
                    .Concat(response.Content.Headers)
                    .ToDictionary(
                        header => header.Key,
                        header => string.Join(",", header.Value),
                        StringComparer.OrdinalIgnoreCase);

                _diagnostics.Record(new ApiDiagnosticEntry(
                    DateTimeOffset.UtcNow,
                    correlationId,
                    operation,
                    step,
                    method.Method,
                    new Uri(_httpClient.BaseAddress!, pathAndQuery).ToString(),
                    attempt,
                    (int)response.StatusCode,
                    parsed.Code,
                    parsed.Message,
                    stopwatch.ElapsedMilliseconds,
                    parsed.IsSuccess ? "SUCCESS" : "API_ERROR",
                    body,
                    raw,
                    null,
                    null)
                {
                    RequestHeadersJson = JsonSerializer.Serialize(requestHeaders),
                    ResponseHeadersJson = JsonSerializer.Serialize(responseHeaders),
                    ResponseLengthBytes = Encoding.UTF8.GetByteCount(raw)
                });

                if (IsTransient(response.StatusCode) && attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
                    continue;
                }

                return parsed;
            }
            catch (Exception ex) when (
                (ex is HttpRequestException or TaskCanceledException) &&
                !cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                lastException = ex;

                _diagnostics.Record(new ApiDiagnosticEntry(
                    DateTimeOffset.UtcNow,
                    correlationId,
                    operation,
                    step,
                    method.Method,
                    new Uri(_httpClient.BaseAddress!, pathAndQuery).ToString(),
                    attempt,
                    null,
                    null,
                    null,
                    stopwatch.ElapsedMilliseconds,
                    "TRANSPORT_ERROR",
                    body,
                    null,
                    ex.GetType().FullName,
                    ex.Message)
                {
                    ExceptionStackTrace = ex.StackTrace
                });

                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
                    continue;
                }
            }
        }

        throw new SolarApiException(
            "Solar of Things request failed after bounded transient retry.",
            innerException: lastException);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 500 ||
               statusCode == HttpStatusCode.RequestTimeout ||
               (int)statusCode == 429;
    }

    private static SolarApiResponse ParseResponse(int httpStatus, string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement.Clone();

            var code = GetString(root, "code");
            var message = GetString(root, "message", "msg");

            var data = root.ValueKind == JsonValueKind.Object &&
                       root.TryGetProperty("data", out var dataElement)
                ? dataElement.Clone()
                : default;

            return new SolarApiResponse(
                httpStatus,
                code,
                message,
                data,
                root,
                raw);
        }
        catch (JsonException)
        {
            using var emptyDocument = JsonDocument.Parse("{}");
            var empty = emptyDocument.RootElement.Clone();

            return new SolarApiResponse(
                httpStatus,
                null,
                "Non-JSON response",
                default,
                empty,
                raw);
        }
    }

    public static void EnsureSuccess(SolarApiResponse response, string prefix)
    {
        if (response.IsSuccess)
        {
            return;
        }

        throw new SolarApiException(
            $"{prefix} HTTP={response.HttpStatus} API={response.Code ?? "-"} {response.Message ?? string.Empty}".Trim(),
            response.HttpStatus,
            response.Code,
            response.Message);
    }

    public static string SerializeCompact(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        });
    }

    public static string? GetString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => property.GetRawText()
            };
        }

        return null;
    }

    private static long? GetInt64(JsonElement element, params string[] names)
    {
        var value = GetString(element, names);
        return long.TryParse(value, out var parsed) ? parsed : null;
    }
}

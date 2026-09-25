using System.Text.Json;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Security;

namespace SolarOfThings.Core.SolarOfThings;

public sealed class SolarOfThingsSessionManager
{
    private const string AccessTokenKey = "solar.session.access-token";
    private const string RefreshTokenKey = "solar.session.refresh-token";
    private const string AccountKey = "solar.session.account";
    private const string PasswordKey = "solar.session.password";
    private const string AccessExpiryKey = "solar.session.access-expiry";
    private const string RefreshExpiryKey = "solar.session.refresh-expiry";
    private const string TimeZoneKey = "solar.session.time-zone";
    private const string UserIdKey = "solar.session.user-id";

    private static readonly TimeSpan RefreshLeadTime = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SolarOfThingsApiClient _api;
    private readonly ISecretStore _secrets;
    private readonly ApiDiagnosticsStore _diagnostics;

    private SolarSessionTokens? _tokens;
    private string? _account;
    private string? _password;
    private string _timeZone = "America/Santiago";
    private bool _remember;
    private DateTimeOffset? _tokensReceivedUtc;
    private bool _isVerified;

    public SolarOfThingsSessionManager(
        SolarOfThingsApiClient api,
        ISecretStore secrets,
        ApiDiagnosticsStore diagnostics)
    {
        _api = api;
        _secrets = secrets;
        _diagnostics = diagnostics;
        TryRestore();
    }

    public bool HasSession => _tokens is not null &&
                              !string.IsNullOrWhiteSpace(_tokens.AccessToken);

    public string? Account => _account;
    public bool IsSessionVerified => HasSession && _isVerified;
    public bool CanServerLogout =>
        _tokens is not null &&
        !string.IsNullOrWhiteSpace(_tokens.AccessToken) &&
        !string.IsNullOrWhiteSpace(_tokens.UserId);

    public async Task LoginAsync(
        string account,
        string password,
        bool remember,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _api.LoginAsync(
            account,
            password,
            timeZone,
            cancellationToken);

        _tokens = tokens;
        _tokensReceivedUtc = DateTimeOffset.UtcNow;
        _account = account;
        _password = remember ? password : null;
        _timeZone = timeZone;
        _remember = remember;
        _isVerified = true;

        if (remember)
        {
            PersistSession(account, password, timeZone, tokens);
        }
        else
        {
            DeletePersistedSession();
        }

        _diagnostics.RecordLocal(
            "Session",
            "LoginComplete",
            "SUCCESS",
            "Account/password login completed.",
            JsonSerializer.Serialize(new
            {
                remember,
                accessExpiryKnown = !string.IsNullOrWhiteSpace(tokens.AccessExpiresAt) ||
                                    tokens.AccessExpiresInMilliseconds.HasValue,
                refreshTokenReturned = !string.IsNullOrWhiteSpace(tokens.RefreshToken)
            }));
    }

    public void UseTokenPair(
        string accessToken,
        string refreshToken,
        bool remember,
        string timeZone)
    {
        _tokens = new SolarSessionTokens(
            accessToken,
            refreshToken,
            null,
            null,
            null,
            null);
        _tokensReceivedUtc = DateTimeOffset.UtcNow;

        _account = null;
        _password = null;
        _timeZone = timeZone;
        _remember = remember;
        _isVerified = false;

        if (remember)
        {
            PersistTokenOnlySession(_tokens, timeZone);
        }
        else
        {
            DeletePersistedSession();
        }

        _diagnostics.RecordLocal(
            "Session",
            "ManualTokenPair",
            "SUCCESS",
            "Existing token pair loaded locally. Token values are never written to diagnostics.");
    }

    public async Task<SolarApiResponse> GetAsync(
        string operation,
        string step,
        string pathAndQuery,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var response = await _api.GetAuthorizedAsync(
            operation,
            step,
            pathAndQuery,
            token,
            timeZone,
            cancellationToken);

        if (IsAuthExpired(response))
        {
            _isVerified = false;
            token = await RefreshAsync(cancellationToken);
            response = await _api.GetAuthorizedAsync(
                operation,
                step,
                pathAndQuery,
                token,
                timeZone,
                cancellationToken);
        }

        _isVerified = response.IsSuccess;
        return response;
    }

    public async Task<SolarApiResponse> PostAsync(
        string operation,
        string step,
        string pathAndQuery,
        object? body,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var response = await _api.PostAuthorizedAsync(
            operation,
            step,
            pathAndQuery,
            body,
            token,
            timeZone,
            cancellationToken);

        if (IsAuthExpired(response))
        {
            _isVerified = false;
            token = await RefreshAsync(cancellationToken);
            response = await _api.PostAuthorizedAsync(
                operation,
                step,
                pathAndQuery,
                body,
                token,
                timeZone,
                cancellationToken);
        }

        _isVerified = response.IsSuccess;
        return response;
    }

    public void ResetLocalSession(bool forgetRememberedCredentials)
    {
        _tokens = null;
        _tokensReceivedUtc = null;
        _remember = false;
        _isVerified = false;

        if (forgetRememberedCredentials)
        {
            _account = null;
            _password = null;
            DeletePersistedSession();
        }

        _diagnostics.RecordLocal(
            "Session",
            "Reset",
            "SUCCESS",
            forgetRememberedCredentials
                ? "Local session and remembered account credentials removed."
                : "In-memory local session reset.");
    }

    private bool TryRestore()
    {
        if (!_secrets.TryRead(AccessTokenKey, out var access) ||
            string.IsNullOrWhiteSpace(access))
        {
            return false;
        }

        _secrets.TryRead(RefreshTokenKey, out var refresh);
        _secrets.TryRead(AccessExpiryKey, out var accessExpiry);
        _secrets.TryRead(RefreshExpiryKey, out var refreshExpiry);
        _secrets.TryRead(AccountKey, out _account);
        _secrets.TryRead(PasswordKey, out _password);
        _secrets.TryRead(TimeZoneKey, out var timeZone);
        _secrets.TryRead(UserIdKey, out var userId);

        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            _timeZone = timeZone;
        }

        _tokens = new SolarSessionTokens(
            access,
            refresh ?? string.Empty,
            accessExpiry,
            refreshExpiry,
            null,
            userId);
        _tokensReceivedUtc = null;

        _remember = true;
        _isVerified = false;

        _diagnostics.RecordLocal(
            "Session",
            "Restore",
            "SUCCESS",
            "Protected remembered session restored from local Windows storage.");

        return true;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokens is null || string.IsNullOrWhiteSpace(_tokens.AccessToken))
        {
            if (!string.IsNullOrWhiteSpace(_account) &&
                !string.IsNullOrWhiteSpace(_password))
            {
                await LoginAsync(
                    _account,
                    _password,
                    _remember,
                    _timeZone,
                    cancellationToken);

                return _tokens!.AccessToken;
            }

            throw new SolarApiException(
                "No Solar of Things session is active. Connect first.");
        }

        if (ShouldRefreshProactively(_tokens))
        {
            return await RefreshAsync(cancellationToken);
        }

        return _tokens.AccessToken;
    }

    private bool ShouldRefreshProactively(SolarSessionTokens tokens)
    {
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tokens.AccessExpiresAt) &&
            DateTimeOffset.TryParse(tokens.AccessExpiresAt, out var expiresAt))
        {
            return DateTimeOffset.UtcNow >= expiresAt.ToUniversalTime() - RefreshLeadTime;
        }

        if (tokens.AccessExpiresInMilliseconds is > 0 &&
            _tokensReceivedUtc.HasValue)
        {
            var relativeExpiry = _tokensReceivedUtc.Value +
                                 TimeSpan.FromMilliseconds(tokens.AccessExpiresInMilliseconds.Value);

            return DateTimeOffset.UtcNow >= relativeExpiry - RefreshLeadTime;
        }

        return false;
    }

    private async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            if (_tokens is null ||
                string.IsNullOrWhiteSpace(_tokens.AccessToken) ||
                string.IsNullOrWhiteSpace(_tokens.RefreshToken))
            {
                throw new SolarApiException(
                    "The session cannot be refreshed because a complete token pair is unavailable.");
            }

            try
            {
                var refreshed = await _api.RefreshAsync(
                    _tokens.AccessToken,
                    _tokens.RefreshToken,
                    cancellationToken);

                refreshed = refreshed with
                {
                    UserId = refreshed.UserId ?? _tokens.UserId
                };

                _tokens = refreshed;
                _tokensReceivedUtc = DateTimeOffset.UtcNow;

                _diagnostics.RecordLocal(
                    "Session",
                    "ProactiveOrReactiveRefreshComplete",
                    "SUCCESS",
                    "Access/refresh token pair rotated successfully.");

                if (_remember)
                {
                    if (!string.IsNullOrWhiteSpace(_account) &&
                        !string.IsNullOrWhiteSpace(_password))
                    {
                        PersistSession(_account, _password, _timeZone, refreshed);
                    }
                    else
                    {
                        PersistTokenOnlySession(refreshed, _timeZone);
                    }
                }

                return refreshed.AccessToken;
            }
            catch when (!string.IsNullOrWhiteSpace(_account) &&
                        !string.IsNullOrWhiteSpace(_password))
            {
                await LoginAsync(
                    _account,
                    _password,
                    _remember,
                    _timeZone,
                    cancellationToken);

                return _tokens!.AccessToken;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool IsAuthExpired(SolarApiResponse response)
    {
        if (response.HttpStatus is 401 or 403)
        {
            return true;
        }

        return response.Code is "9" or "401" or "1001" or "1002";
    }

    private void PersistSession(
        string account,
        string password,
        string timeZone,
        SolarSessionTokens tokens)
    {
        _secrets.Save(AccountKey, account);
        _secrets.Save(PasswordKey, password);
        PersistTokenOnlySession(tokens, timeZone);
    }

    private void PersistTokenOnlySession(
        SolarSessionTokens tokens,
        string timeZone)
    {
        _secrets.Save(AccessTokenKey, tokens.AccessToken);
        _secrets.Save(TimeZoneKey, timeZone);

        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            _secrets.Save(RefreshTokenKey, tokens.RefreshToken);
        }

        if (!string.IsNullOrWhiteSpace(tokens.AccessExpiresAt))
        {
            _secrets.Save(AccessExpiryKey, tokens.AccessExpiresAt);
        }

        if (!string.IsNullOrWhiteSpace(tokens.RefreshExpiresAt))
        {
            _secrets.Save(RefreshExpiryKey, tokens.RefreshExpiresAt);
        }

        if (!string.IsNullOrWhiteSpace(tokens.UserId))
        {
            _secrets.Save(UserIdKey, tokens.UserId);
        }
        else
        {
            _secrets.Delete(UserIdKey);
        }
    }

    public async Task<bool> LogoutFromServerAsync(
        CancellationToken cancellationToken = default)
    {
        if (_tokens is null ||
            string.IsNullOrWhiteSpace(_tokens.AccessToken) ||
            string.IsNullOrWhiteSpace(_tokens.UserId))
        {
            return false;
        }

        var succeeded = false;

        try
        {
            await _api.LogoutAsync(
                _tokens.AccessToken,
                _tokens.UserId,
                _timeZone,
                cancellationToken);

            succeeded = true;

            _diagnostics.RecordLocal(
                "Session",
                "ServerLogout",
                "SUCCESS",
                "Solar of Things server logout completed.");
        }
        catch (Exception ex)
        {
            _diagnostics.RecordLocal(
                "Session",
                "ServerLogout",
                "WARN",
                $"Server logout failed; local session will still be cleared. {ex.Message}");
        }
        finally
        {
            ResetLocalSession(forgetRememberedCredentials: true);
        }

        return succeeded;
    }

    private void DeletePersistedSession()
    {
        foreach (var key in new[]
        {
            AccessTokenKey,
            RefreshTokenKey,
            AccountKey,
            PasswordKey,
            AccessExpiryKey,
            RefreshExpiryKey,
            TimeZoneKey,
            UserIdKey
        })
        {
            _secrets.Delete(key);
        }
    }
}

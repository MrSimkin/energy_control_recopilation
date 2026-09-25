using SolarOfThings.Core.Security;

namespace SolarOfThings.Core.SolarOfThings;

public sealed record IotOpenCredential(
    string AppId,
    string SecretValue,
    bool SecretIsEncrypted);

public sealed class IotOpenCredentialStore
{
    private const string AppIdKey = "solar.iot-open.app-id";
    private const string SecretKey = "solar.iot-open.secret";
    private const string SecretModeKey = "solar.iot-open.secret-mode";

    private readonly ISecretStore _secrets;

    public IotOpenCredentialStore(ISecretStore secrets)
    {
        _secrets = secrets;
    }

    public bool TryRead(out IotOpenCredential? credential)
    {
        if (!_secrets.TryRead(AppIdKey, out var appId) ||
            string.IsNullOrWhiteSpace(appId) ||
            !_secrets.TryRead(SecretKey, out var secret) ||
            string.IsNullOrWhiteSpace(secret))
        {
            credential = null;
            return false;
        }

        _secrets.TryRead(SecretModeKey, out var mode);

        credential = new IotOpenCredential(
            appId,
            secret,
            !string.Equals(mode, "plain", StringComparison.OrdinalIgnoreCase));

        return true;
    }

    public void Save(string appId, string secretValue, bool secretIsEncrypted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretValue);

        _secrets.Save(AppIdKey, appId.Trim());
        _secrets.Save(SecretKey, secretValue.Trim());
        _secrets.Save(SecretModeKey, secretIsEncrypted ? "encrypted" : "plain");
    }

    public bool TryImportFromEnvironment(bool persist, out string source)
    {
        var appId = Environment.GetEnvironmentVariable("SOLAR_OF_THINGS_APP_ID");
        var encryptedSecret = Environment.GetEnvironmentVariable("SOLAR_OF_THINGS_APP_SECRET_ENC");
        var plainSecret = Environment.GetEnvironmentVariable("SOLAR_OF_THINGS_APP_SECRET");

        if (string.IsNullOrWhiteSpace(appId))
        {
            source = "none";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(encryptedSecret))
        {
            if (persist)
            {
                Save(appId, encryptedSecret, secretIsEncrypted: true);
            }

            source = "environment-encrypted";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(plainSecret))
        {
            if (persist)
            {
                Save(appId, plainSecret, secretIsEncrypted: false);
            }

            source = "environment-plain";
            return true;
        }

        source = "none";
        return false;
    }

    public void Delete()
    {
        _secrets.Delete(AppIdKey);
        _secrets.Delete(SecretKey);
        _secrets.Delete(SecretModeKey);
    }
}

using SolarOfThings.Core.Security;

namespace SolarOfThings.Core.SolarOfThings;

public sealed record IotOpenCredential(
    string AppId,
    string SecretValue,
    bool SecretIsEncrypted,
    string Source);

public sealed class IotOpenCredentialStore
{
    private const string AppIdKey = "solar.iot-open.app-id";
    private const string SecretKey = "solar.iot-open.secret";
    private const string SecretModeKey = "solar.iot-open.secret-mode";

    // Public client-side material used by the production Solar of Things web client
    // ecosystem. It is not a user credential. Keep it encrypted in the same form
    // used by the upstream client and allow a DPAPI-protected local override.
    private const string PortalDefaultAppId = "rBrTRfAPXz";
    private const string PortalDefaultEncryptedSecret =
        "I4D0KRr2339z3pQ/at91V9BpFAOe54DaTafwSm6suIQ=";

    private readonly ISecretStore _secrets;

    public IotOpenCredentialStore(ISecretStore secrets)
    {
        _secrets = secrets;
    }

    public bool HasLocalOverride =>
        _secrets.TryRead(AppIdKey, out var appId) &&
        !string.IsNullOrWhiteSpace(appId) &&
        _secrets.TryRead(SecretKey, out var secret) &&
        !string.IsNullOrWhiteSpace(secret);

    public bool TryRead(out IotOpenCredential? credential)
    {
        if (HasLocalOverride &&
            _secrets.TryRead(AppIdKey, out var appId) &&
            _secrets.TryRead(SecretKey, out var secret))
        {
            _secrets.TryRead(SecretModeKey, out var mode);

            credential = new IotOpenCredential(
                appId!,
                secret!,
                !string.Equals(mode, "plain", StringComparison.OrdinalIgnoreCase),
                "local-override");

            return true;
        }

        credential = new IotOpenCredential(
            PortalDefaultAppId,
            PortalDefaultEncryptedSecret,
            SecretIsEncrypted: true,
            Source: "portal-default");

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

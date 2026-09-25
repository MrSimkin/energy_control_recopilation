using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiFileSecretStore : ISecretStore
{
    private static readonly byte[] Entropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("SolarEnergyMonitor|SecretStore|v1"));

    private readonly string _secretDirectory;

    public DpapiFileSecretStore(AppPaths paths)
    {
        _secretDirectory = Path.Combine(paths.DataDirectory, "Secrets");
        Directory.CreateDirectory(_secretDirectory);
    }

    public void Save(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var plaintext = Encoding.UTF8.GetBytes(value);
        try
        {
            var protectedBytes = ProtectedData.Protect(
                plaintext,
                Entropy,
                DataProtectionScope.CurrentUser);

            var path = GetPath(key);
            var tempPath = path + ".tmp";

            File.WriteAllBytes(tempPath, protectedBytes);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public bool TryRead(string key, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetPath(key);
        if (!File.Exists(path))
        {
            value = null;
            return false;
        }

        var protectedBytes = File.ReadAllBytes(path);
        byte[]? plaintext = null;

        try
        {
            plaintext = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            value = Encoding.UTF8.GetString(plaintext);
            return true;
        }
        catch (CryptographicException)
        {
            value = null;
            return false;
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    public void Delete(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetPath(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var fileName = Convert.ToHexString(hash).ToLowerInvariant() + ".secret";
        return Path.Combine(_secretDirectory, fileName);
    }
}

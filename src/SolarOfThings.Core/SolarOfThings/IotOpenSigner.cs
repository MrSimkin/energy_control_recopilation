using System.Security.Cryptography;
using System.Text;

namespace SolarOfThings.Core.SolarOfThings;

public static class IotOpenSigner
{
    public static string ComputeBodyHash(string compactJson)
    {
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(compactJson)))
            .ToLowerInvariant();
    }

    public static string ComputeSignature(
        string appId,
        string nonce,
        string bodyHash,
        string secret)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["IOT-Open-AppID"] = appId,
            ["IOT-Open-Body-Hash"] = bodyHash,
            ["IOT-Open-Nonce"] = nonce
        };

        var canonical = string.Join(
            "&",
            fields.Select(pair => $"{pair.Key}={pair.Value}"));

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(canonical));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hmacBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(base64));

        return Convert.ToHexString(MD5.HashData(hmacBytes)).ToLowerInvariant();
    }

    public static string DecryptEmbeddedSecret(string appId, string encryptedBase64Secret)
    {
        var md5Hex = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(appId)))
            .ToLowerInvariant();

        var key = Encoding.ASCII.GetBytes(md5Hex[..16]);
        var iv = Encoding.ASCII.GetBytes(md5Hex[16..]);
        var ciphertext = Convert.FromBase64String(encryptedBase64Secret);

        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        try
        {
            var length = plaintext.Length;
            while (length > 0 && plaintext[length - 1] == 0)
            {
                length--;
            }

            return Encoding.UTF8.GetString(plaintext, 0, length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static string CreateNonce()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}

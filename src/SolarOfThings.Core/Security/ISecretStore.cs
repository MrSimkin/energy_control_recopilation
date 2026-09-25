namespace SolarOfThings.Core.Security;

public interface ISecretStore
{
    void Save(string key, string value);
    bool TryRead(string key, out string? value);
    void Delete(string key);
}

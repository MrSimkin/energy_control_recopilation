using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Security;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.SolarOfThings;

var root = Path.Combine(
    Path.GetTempPath(),
    "SolarEnergyMonitorSmoke",
    Guid.NewGuid().ToString("N"));

try
{
    var paths = new AppPaths(root);
    var database = new SqliteDatabase(paths);

    database.Initialize();

    if (!File.Exists(database.DatabasePath))
    {
        throw new InvalidOperationException("SQLite database file was not created.");
    }

    if (database.GetSchemaVersion() != SqliteDatabase.CurrentSchemaVersion ||
        SqliteDatabase.CurrentSchemaVersion != 2)
    {
        throw new InvalidOperationException("Unexpected SQLite schema version.");
    }

    var settings = new AppSettingsRepository(database);
    settings.Set("smoke.setting", "ok");
    if (settings.Get("smoke.setting") != "ok")
    {
        throw new InvalidOperationException("Application settings round-trip failed.");
    }
    settings.Delete("smoke.setting");

    var diagnostics = new DiagnosticsFileWriter(paths);
    diagnostics.Write("Information", "SmokeTest", "Diagnostics writer is operational.");
    if (Directory.GetFiles(paths.LogDirectory, "diagnostics-*.jsonl").Length != 1)
    {
        throw new InvalidOperationException("Diagnostics log file was not created.");
    }

    var signBody =
        "{\"account\":\"demo\",\"password\":\"5f4dcc3b5aa765d61d8327deb882cf99\"}";
    var signHash = IotOpenSigner.ComputeBodyHash(signBody);
    if (signHash != "a36cb269ad52226e354c9effbe1e9e73d2b2285c9ec5161513f5424a7e9bb71c")
    {
        throw new InvalidOperationException("IOT Open body-hash test vector failed.");
    }

    var sign = IotOpenSigner.ComputeSignature(
        "test-app",
        "00112233445566778899aabbccddeeff",
        signHash,
        "test-secret");

    if (sign != "43580b68bbd8ddbb7b10c7df4899dc89")
    {
        throw new InvalidOperationException("IOT Open signing test vector failed.");
    }

    var profileRepo = new CommissioningProfileRepository(database);
    var profile = new CommissioningProfile(
        "station-1",
        "Station",
        "America/Santiago",
        "device-1",
        "Device",
        "serial",
        "model",
        "manufacturer",
        "dtu",
        "protocol",
        "firmware",
        "1",
        "SUPPORTED",
        12,
        "SUPPORTED",
        "SUPPORTED",
        "SUPPORTED",
        "SUPPORTED",
        "SUPPORTED",
        "{}",
        "[]",
        "{}",
        "{}",
        DateTimeOffset.UtcNow);

    profileRepo.Save(profile);
    var loadedProfile = profileRepo.Get();
    if (loadedProfile?.DeviceId != "device-1" ||
        loadedProfile.DataSource != "1" ||
        loadedProfile.GatherAttributeCount != 12)
    {
        throw new InvalidOperationException("Commissioning profile round-trip failed.");
    }

    var apiDiagnostics = new ApiDiagnosticsStore(paths);
    apiDiagnostics.Record(new ApiDiagnosticEntry(
        DateTimeOffset.UtcNow,
        "smoke",
        "Smoke",
        "Redaction",
        "POST",
        "https://example.invalid",
        1,
        200,
        "0",
        "ok",
        1,
        "SUCCESS",
        "{\"password\":\"secret\",\"deviceId\":\"123\"}",
        "{\"accessToken\":\"token-value\",\"data\":{\"deviceId\":\"123\"}}",
        null,
        null)
    {
        RequestHeadersJson = "{\"IOT-Token\":\"header-token\",\"IOT-Time-Zone\":\"America/Santiago\"}",
        ResponseHeadersJson = "{\"X-Request-Id\":\"request-123\"}",
        ResponseLengthBytes = 42
    });

    var report = apiDiagnostics.BuildSanitizedReport();
    if (report.Contains("token-value", StringComparison.Ordinal) ||
        report.Contains("\"password\":\"secret\"", StringComparison.Ordinal) ||
        report.Contains("header-token", StringComparison.Ordinal) ||
        !report.Contains("deviceId", StringComparison.Ordinal) ||
        !report.Contains("request-123", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Diagnostic redaction smoke test failed.");
    }

    if (OperatingSystem.IsWindows())
    {
        ISecretStore secrets = new DpapiFileSecretStore(paths);
        secrets.Save("smoke.secret", "not-a-real-secret");

        if (!secrets.TryRead("smoke.secret", out var secretValue) ||
            secretValue != "not-a-real-secret")
        {
            throw new InvalidOperationException("DPAPI secret-store round-trip failed.");
        }

        secrets.Delete("smoke.secret");
        if (secrets.TryRead("smoke.secret", out _))
        {
            throw new InvalidOperationException("DPAPI secret-store delete failed.");
        }
    }

    Console.WriteLine(
        $"Smoke test passed. Schema v{SqliteDatabase.CurrentSchemaVersion}; " +
        "settings, diagnostics/redaction, IOT Open signing, commissioning profile and protected secret storage are operational.");
}
finally
{
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

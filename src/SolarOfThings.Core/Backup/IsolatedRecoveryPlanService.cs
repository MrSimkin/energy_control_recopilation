using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// Test-only recovery plan. The ONLY executable stage is an additive copy of
/// app_setting into a NEW synthetic database. No UI or live restore entrypoint.
/// All dependent entity stages are diagnostics, never imported or activated.
/// </summary>
public sealed class IsolatedRecoveryPlanService
{
    private const int MaxFingerprintSettings = 100_000;
    private readonly IsolatedRecoveryPreviewService _preview = new();
    private readonly IsolatedRecoveryRelationAuditService _relations = new();
    private readonly IsolatedRecoveryAdditiveTestService _staging = new();

    public SyntheticRecoveryPlan CreatePlan(string backupZip, string targetDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        var target = Path.GetFullPath(targetDatabasePath);
        var package = Path.GetFullPath(backupZip);
        // Reject ordinary OS temp paths and linked directory hops BEFORE
        // reading a supposed destination database.
        IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(target);

        var preview = _preview.Preview(package, target);
        var ready = preview.Status == "READ_ONLY_PREVIEW" &&
                    preview.BackupSchemaVersion == SqliteDatabase.CurrentSchemaVersion &&
                    preview.TargetSchemaVersion == SqliteDatabase.CurrentSchemaVersion;
        RecoveryRelationAudit? graph = null;
        if (ready)
        {
            graph = _relations.Audit(package, target);
            ready = graph.Status == "READ_ONLY_GRAPH_AUDIT";
        }

        var settings = preview.Categories.FirstOrDefault(c => c.Category == "SETTINGS");
        var settingsReady = ready && settings is { UnsupportedReason: null };
        var steps = new List<SyntheticRecoveryPlanStep>
        {
            new(1, "VERIFIED_SOURCE_PACKAGE", "READ_ONLY_VERIFIED", 1,
                "Complete ZIP, embedded SQLite and document references are checked."),
            new(2, "SETTINGS", settingsReady ? "SYNTHETIC_STAGE_ONLY" : "BLOCKED",
                settings?.Missing ?? 0,
                settingsReady
                    ? "Add missing settings only to a new synthetic DB; preserve conflicts and destination-only keys."
                    : "No settings stage unless both schemas and every supported comparison are valid."),
            new(3, "BILL_SOURCE_DOCUMENTS", "REVIEW_ONLY",
                graph is null ? 0 : graph.UnlinkedBillDocuments.Count + graph.Bills.Count,
                "Source bill documents require identity/document linkage review; no files are copied."),
            new(4, "BILLS_AND_CHARGES", "BLOCKED_DEPENDENCIES",
                graph?.Bills.Count ?? 0,
                "Original-document linkage, charge IDs and field evidence require an adapter."),
            new(5, "METER_READINGS", "BLOCKED_AMBIGUOUS_IDENTITY",
                graph?.Totals.BillsWithMeterLinks ?? 0,
                "Timestamp and numeric readings are hints, not unique portable meter identities."),
            new(6, "TARIFF_SOURCES", "REVIEW_ONLY",
                graph?.Tariffs.Count ?? 0,
                "Matching source URLs or PDF hashes never authorize changing target publications."),
            new(7, "TARIFF_RELATIONS", "BLOCKED_DEPENDENCIES",
                graph is null ? 0 : graph.Totals.OutgoingTariffLinks +
                    graph.Totals.IncomingTariffLinks,
                "Correction and supersession graph requires reviewed FK remapping."),
            new(8, "ENERGY_TELEMETRY", "BLOCKED_UNSUPPORTED", 0,
                "Historical samples and device identity adapters are not implemented.")
        };
        var status = settingsReady ? "SYNTHETIC_SETTINGS_PLAN" : "BLOCKED_INCOMPLETE_PLAN";
        // Package bytes and WAL-visible logical app_setting rows both bind
        // this plan to the source and target at the time it is created.
        var packageFingerprint = Sha256File(package);
        var settingsFingerprint = settingsReady ? FingerprintSettings(target) : "";
        var planId = Sha256Text(JsonSerializer.Serialize(new[]
        {
            packageFingerprint, settingsFingerprint, status,
            settings?.Missing.ToString() ?? "0",
            settings?.Conflicts.ToString() ?? "0",
            settings?.TargetOnly.ToString() ?? "0"
        }));
        return new SyntheticRecoveryPlan(package, target, status, planId,
            packageFingerprint, settingsFingerprint, steps,
            settings?.Missing ?? 0, settings?.Conflicts ?? 0,
            settings?.Identical ?? 0, settings?.TargetOnly ?? 0,
            "Synthetic fixture only. Every stage except SETTINGS is review-only or blocked. " +
            "This plan never authorizes owner-data restoration or activation.");
    }

    /// <summary>
    /// Rechecks the plan's exact source/target bindings and WAL-aware settings
    /// fingerprint, stages the settings, reconciles counters and independently
    /// previews the staged result. A mismatch removes the new synthetic stage.
    /// </summary>
    public SyntheticAdditiveImportResult StageSettingsOnly(
        SyntheticRecoveryPlan plan, string backupZip, string targetDatabasePath,
        bool simulateFailureAfterFirstInsert = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        var package = Path.GetFullPath(backupZip);
        var target = Path.GetFullPath(targetDatabasePath);
        IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(target);
        if (!string.Equals(package, plan.PackagePath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(target, plan.TargetDatabasePath, StringComparison.OrdinalIgnoreCase) ||
            plan.Status != "SYNTHETIC_SETTINGS_PLAN")
            throw new InvalidOperationException("Plan is not authorized for this synthetic fixture pair.");

        var current = CreatePlan(package, target);
        if (current.Status != plan.Status ||
            current.PlanId != plan.PlanId ||
            current.SourcePackageSha256 != plan.SourcePackageSha256 ||
            current.TargetSettingsSha256 != plan.TargetSettingsSha256 ||
            current.MissingSettings != plan.MissingSettings ||
            current.ConflictingSettings != plan.ConflictingSettings ||
            current.TargetOnlySettings != plan.TargetOnlySettings ||
            !plan.Steps.Any(s => s.Category == "SETTINGS" &&
                                 s.Status == "SYNTHETIC_STAGE_ONLY"))
            throw new InvalidOperationException(
                "Recovery plan is stale or incomplete; create a new synthetic plan.");

        SyntheticAdditiveImportResult? staged = null;
        try
        {
            staged = _staging.ApplyToNewStagedFixture(
                package, target, simulateFailureAfterFirstInsert);
            if (staged.Added != current.MissingSettings ||
                staged.Conflicts != current.ConflictingSettings ||
                staged.AlreadyPresent != current.IdenticalSettings)
                throw new InvalidDataException(
                    "Staged settings counters disagree with the approved synthetic preview.");

            // A fresh preview on the output proves no source setting remains
            // missing and the destination's conflicts/extra keys were kept.
            var verification = _preview.Preview(package, staged.StagedDatabasePath);
            var settings = verification.Categories.Single(s => s.Category == "SETTINGS");
            if (verification.Status != "READ_ONLY_PREVIEW" ||
                settings.Missing != 0 ||
                settings.Conflicts != current.ConflictingSettings ||
                settings.TargetOnly != current.TargetOnlySettings ||
                FingerprintSettings(target) != current.TargetSettingsSha256)
                throw new InvalidDataException(
                    "Post-stage checks failed or the original synthetic target changed.");
            return staged;
        }
        catch
        {
            if (staged is not null && File.Exists(staged.StagedDatabasePath))
            {
                // This path was returned by the TEST-ONLY staging service,
                // under the previously verified marked synthetic root.
                File.Delete(staged.StagedDatabasePath);
            }
            throw;
        }
    }

    private static string FingerprintSettings(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=ON;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT key,value,updated_utc FROM app_setting ORDER BY key;";
        using var rows = cmd.ExecuteReader();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var count = 0;
        while (rows.Read())
        {
            if (++count > MaxFingerprintSettings)
                throw new InvalidDataException("Synthetic settings fingerprint exceeds limit.");
            // Structured JSON, including nulls, avoids delimiter collisions
            // and includes uncheckpointed but committed WAL-visible values.
            var body = JsonSerializer.Serialize(new string?[]
            {
                rows.GetString(0),
                rows.IsDBNull(1) ? null : rows.GetString(1),
                rows.GetString(2)
            }) + "\n";
            hash.AppendData(Encoding.UTF8.GetBytes(body));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Sha256Text(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public sealed record SyntheticRecoveryPlan(
    string PackagePath, string TargetDatabasePath, string Status, string PlanId,
    string SourcePackageSha256, string TargetSettingsSha256,
    IReadOnlyList<SyntheticRecoveryPlanStep> Steps, int MissingSettings,
    int ConflictingSettings, int IdenticalSettings, int TargetOnlySettings,
    string SafetyDisclaimer);

public sealed record SyntheticRecoveryPlanStep(
    int Order, string Category, string Status, long Records, string Explanation);

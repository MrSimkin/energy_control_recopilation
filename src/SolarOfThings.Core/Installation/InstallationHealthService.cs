using System.Globalization;
using System.Text.Json;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Diagnostics;

namespace SolarOfThings.Core.Installation;

public sealed class InstallationHealthService
{
    public const string ContractVersion = InstallationContextPolicyService.ContextVersion;

    private sealed record Rule(
        string CheckKey,
        string SourceAttributeKey,
        string ExpectedRawValue,
        string? ExpectedAlternateValue,
        string ExpectedDisplay,
        string ConfirmedDetail,
        string DriftDetail,
        bool Numeric = false);

    private static Rule[] BuildRules(InstallationContextPolicy policy) =>
    [
        new(
            "source-priority",
            "workingMode",
            "1",
            "SBU",
            "SBU — sol primero, batería después y red al final",
            "La prioridad de energía coincide con la configuración familiar.",
            "La prioridad de energía no coincide con SBU."),
        new(
            "battery-protocol",
            "batteryType",
            "4",
            "PYL",
            "PYL — batería administrada por BMS",
            "El protocolo de batería coincide con PYL.",
            "El protocolo de batería no coincide con PYL."),
        new(
            "charge-source",
            "chargingPriorityOrder",
            "2",
            "OSO",
            "OSO — batería cargada sólo desde solar",
            "La prioridad de carga coincide con solar solamente.",
            "La prioridad de carga no coincide con OSO."),
        new(
            "bms-communication",
            "bmsCommunicationNormal",
            "1",
            "Yes",
            "Comunicación de batería activa",
            "La batería está entregando información BMS al inversor.",
            "La comunicación BMS no aparece activa."),
        new(
            "absolute-floor",
            "bmsLowPowerSOC",
            policy.EmergencyFloorSocPercent.ToString(CultureInfo.InvariantCulture),
            null,
            $"{policy.EmergencyFloorSocPercent:0.#}% — reserva mínima durante un corte",
            "El piso mínimo coincide con 10%.",
            "El piso mínimo no coincide con 10%.",
            Numeric: true),
        new(
            "grid-transfer",
            "bmsReturnsToMainsModeSOC",
            policy.NormalGridTransferSocPercent.ToString(CultureInfo.InvariantCulture),
            null,
            $"{policy.NormalGridTransferSocPercent:0.#}% — punto normal para empezar a usar la red",
            "El cambio normal a red coincide con 20%.",
            "El punto de cambio a red no coincide con 20%.",
            Numeric: true),
        new(
            "return-to-battery",
            "bmsReturnsToBatteryModeSOC",
            policy.ReturnToBatterySocPercent.ToString(CultureInfo.InvariantCulture),
            null,
            $"{policy.ReturnToBatterySocPercent:0.#}% — vuelve al uso normal de sol y batería",
            "El retorno a batería coincide con 50%.",
            "El retorno desde red no coincide con 50%.",
            Numeric: true),
        new(
            "restart-after-low",
            "bmsAutomaticallyStartsSOCAfterLow",
            policy.RestartAfterLowSocPercent.ToString(CultureInfo.InvariantCulture),
            null,
            $"{policy.RestartAfterLowSocPercent:0.#}% — recuperación mínima después de apagado por batería baja",
            "El reinicio después de batería baja coincide con 50%.",
            "El umbral de reinicio no coincide con 50%.",
            Numeric: true),
        new(
            "solar-priority",
            "pvEnergyFeedingPriority",
            "1",
            "LBU",
            "LBU — solar alimenta primero la casa",
            "La prioridad solar coincide con casa primero.",
            "La prioridad solar no coincide con LBU."),
        new(
            "zero-export",
            "gridConnectionFunction",
            "0",
            "Off",
            "Inyección a red deshabilitada",
            "La función de inyección a red aparece deshabilitada.",
            "La función de conexión/inyección a red no aparece deshabilitada."),
        new(
            "equalization",
            "batteryEqualizationMode",
            "0",
            "Disable",
            "Ecualización deshabilitada",
            "La ecualización permanece deshabilitada para la batería LiFePO₄.",
            "La ecualización no aparece deshabilitada; requiere revisión.")
    ];

    private readonly InstallationHealthRepository _repository;
    private readonly InstallationContextPolicyService _policy;
    private readonly ApiDiagnosticsStore _diagnostics;

    public InstallationHealthService(
        InstallationHealthRepository repository,
        InstallationContextPolicyService policy,
        ApiDiagnosticsStore diagnostics)
    {
        _repository = repository;
        _policy = policy;
        _diagnostics = diagnostics;
    }

    public InstallationConfigSummary Evaluate(CommissioningProfile profile)
    {
        var now = DateTimeOffset.UtcNow;
        var rules = BuildRules(_policy.Current);
        var snapshot = _repository.GetLatestStateSnapshot(profile.DeviceId);
        var values = snapshot is null
            ? new Dictionary<string, RawInstallationValue>(StringComparer.Ordinal)
            : ReadSnapshotValues(snapshot, rules);

        var checks = new List<InstallationConfigCheck>(rules.Length);

        foreach (var rule in rules)
        {
            if (!values.TryGetValue(rule.SourceAttributeKey, out var observed))
            {
                checks.Add(new InstallationConfigCheck(
                    rule.CheckKey,
                    rule.SourceAttributeKey,
                    "CONFIG_UNRESOLVED",
                    null,
                    null,
                    rule.ExpectedDisplay,
                    snapshot is null
                        ? "Todavía no hay una comprobación actual guardada para este punto."
                        : "La comprobación actual no incluyó este valor.",
                    now));
                continue;
            }

            var comparable = ReadComparableValue(observed.ValueJson);
            var confirmed = rule.Numeric
                ? NumericEquals(comparable, rule.ExpectedRawValue)
                : string.Equals(
                      comparable,
                      rule.ExpectedRawValue,
                      StringComparison.OrdinalIgnoreCase) ||
                  (!string.IsNullOrWhiteSpace(rule.ExpectedAlternateValue) &&
                   string.Equals(
                       comparable,
                       rule.ExpectedAlternateValue,
                       StringComparison.OrdinalIgnoreCase));

            checks.Add(new InstallationConfigCheck(
                rule.CheckKey,
                rule.SourceAttributeKey,
                confirmed ? "CONFIG_CONFIRMED" : "CONFIG_DRIFT",
                observed.RecordedAtUtc,
                observed.ValueJson,
                rule.ExpectedDisplay,
                confirmed ? rule.ConfirmedDetail : rule.DriftDetail,
                now));
        }

        var drift = checks.Count(check => check.Status == "CONFIG_DRIFT");
        var unresolved = checks.Count(check => check.Status == "CONFIG_UNRESOLVED");
        var confirmedCount = checks.Count(check => check.Status == "CONFIG_CONFIRMED");

        var overall = drift > 0
            ? "CONFIG_DRIFT"
            : unresolved > 0
                ? "CONFIG_UNRESOLVED"
                : "CONFIG_CONFIRMED";

        var summary = new InstallationConfigSummary(
            profile.DeviceId,
            overall,
            confirmedCount,
            drift,
            unresolved,
            now,
            checks);

        _repository.SaveSummary(summary);

        _diagnostics.RecordLocal(
            "InstallationHealth",
            "BehaviorContractEvaluation",
            overall == "CONFIG_CONFIRMED" ? "SUCCESS" : overall,
            $"Installation behavior contract: {confirmedCount} confirmed, {drift} drift, {unresolved} unresolved.",
            JsonSerializer.Serialize(new
            {
                contractVersion = ContractVersion,
                summary.OverallStatus,
                summary.ConfirmedCount,
                summary.DriftCount,
                summary.UnresolvedCount,
                checks = checks.Select(check => new
                {
                    check.CheckKey,
                    check.SourceAttributeKey,
                    check.Status,
                    check.ObservedAtUtc,
                    check.ExpectedDisplay
                })
            }));

        return summary;
    }

    private static IReadOnlyDictionary<string, RawInstallationValue> ReadSnapshotValues(
        InstallationStateSnapshot snapshot,
        IReadOnlyList<Rule> rules)
    {
        var result = new Dictionary<string, RawInstallationValue>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(snapshot.StateJson);
            var root = document.RootElement;

            var observedAt = snapshot.RetrievedUtc;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("time", out var timeElement) &&
                timeElement.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(timeElement.GetString(), out var parsedTime))
            {
                observedAt = parsedTime;
            }

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("fields", out var fields) ||
                fields.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var rule in rules)
            {
                if (!fields.TryGetProperty(rule.SourceAttributeKey, out var field) ||
                    field.ValueKind != JsonValueKind.Object ||
                    !field.TryGetProperty("value", out var value))
                {
                    continue;
                }

                result[rule.SourceAttributeKey] = new RawInstallationValue(
                    rule.SourceAttributeKey,
                    observedAt,
                    value.GetRawText());
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    private static string? ReadComparableValue(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var value = document.RootElement;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => value.ToString()
            };
        }
        catch
        {
            return valueJson.Trim().Trim('"');
        }
    }

    private static bool NumericEquals(string? actual, string expected)
    {
        return double.TryParse(
                   actual,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var actualNumber) &&
               double.TryParse(
                   expected,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var expectedNumber) &&
               Math.Abs(actualNumber - expectedNumber) < 0.01;
    }
}

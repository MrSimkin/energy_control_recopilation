using System.Globalization;
using System.Text.Json;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Diagnostics;

namespace SolarOfThings.Core.Installation;

public sealed class InstallationHealthService
{
    public const string ContractVersion = "family-manual-v2.0-2026-09-25";

    private sealed record Rule(
        string CheckKey,
        string SourceAttributeKey,
        string ExpectedRawValue,
        string? ExpectedAlternateValue,
        string ExpectedDisplay,
        string ConfirmedDetail,
        string DriftDetail,
        bool Numeric = false);

    private static readonly Rule[] Rules =
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
            "10",
            null,
            "10% — reserva mínima durante un corte",
            "El piso mínimo coincide con 10%.",
            "El piso mínimo no coincide con 10%.",
            Numeric: true),
        new(
            "grid-transfer",
            "bmsReturnsToMainsModeSOC",
            "20",
            null,
            "20% — punto normal para empezar a usar la red",
            "El cambio normal a red coincide con 20%.",
            "El punto de cambio a red no coincide con 20%.",
            Numeric: true),
        new(
            "return-to-battery",
            "bmsReturnsToBatteryModeSOC",
            "50",
            null,
            "50% — vuelve al uso normal de sol y batería",
            "El retorno a batería coincide con 50%.",
            "El retorno desde red no coincide con 50%.",
            Numeric: true),
        new(
            "restart-after-low",
            "bmsAutomaticallyStartsSOCAfterLow",
            "50",
            null,
            "50% — recuperación mínima después de apagado por batería baja",
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
    private readonly ApiDiagnosticsStore _diagnostics;

    public InstallationHealthService(
        InstallationHealthRepository repository,
        ApiDiagnosticsStore diagnostics)
    {
        _repository = repository;
        _diagnostics = diagnostics;
    }

    public InstallationConfigSummary Evaluate(CommissioningProfile profile)
    {
        var now = DateTimeOffset.UtcNow;
        var values = _repository.GetLatestRawValues(
            profile.DeviceId,
            Rules.Select(rule => rule.SourceAttributeKey).Distinct().ToArray());

        var checks = new List<InstallationConfigCheck>(Rules.Length);

        foreach (var rule in Rules)
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
                    "Todavía no hay una lectura local suficiente para comprobar este punto.",
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

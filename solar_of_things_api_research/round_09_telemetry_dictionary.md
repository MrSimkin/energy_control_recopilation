# Round 09 Companion — Cloud Telemetry Normalization Dictionary

Date: 2026-09-24

Status: COMPLETE

Purpose: compact implementation-oriented dictionary for normalizing Solar of Things / SiSeLi cloud telemetry without erasing device/protocol differences.

## Normalization rule

Never normalize from raw key alone.

Use this tuple:

`(device model, gather protocol/version, raw key, server unit, raw value)`

and retain the original tuple beside the normalized measurement.

## Canonical concepts

| Canonical metric | Canonical unit | Preferred evidence source | Known raw keys / rules | Main caveat |
|---|---:|---|---|---|
| battery_soc | % | server attribute metadata / confirmed current capture | `remainingBatteryCapacity`; `batterySOC`; `batteryPercentage`; `bmsSOC`; model-specific `batteryCapacity` | `batteryCapacity` can also mean Ah capacity |
| battery_capacity | Ah or Wh | server unit mandatory | `batteryCapacity`, protocol-specific capacity keys | Never infer concept without unit/protocol |
| battery_voltage | V | direct server field | `batteryVoltage`; `bmsBatteryVoltage`; `positiveTerminalBatteryVoltage`; `lithiumBatteryVoltage` | Prefer direct measurement |
| battery_current | A | direct server field | `batteryCurrent`; protocol-specific charge/discharge fields | Sign semantics vary |
| battery_charge_current | A | confirmed per-protocol mapping | UWB1: negative half of `positiveTerminalBatteryCurrent`; other families: `batteryChargingCurrent` | No global sign rule |
| battery_discharge_current | A | confirmed per-protocol mapping | UWB1: positive half of `positiveTerminalBatteryCurrent`; other families: `batteryDischargeCurrent` | No global sign rule |
| battery_power | W | direct field preferred | `batteryPower`; `batteryChargeDischargeRealTimePower`; otherwise derived | Derived formulas depend on topology |
| pv_power | W | direct metadata + protocol rule | `pvInputPower`; `pvPower`; sum `pv1Power..pv4Power`; `generationPower`; `pv1RealTimePower` etc. | Raw unit varies by model, including W vs kW |
| ac_input_power | W | direct field | `exchangeChargingPower`; `inputMainsPower`; `mainsInputRealTimePower` | Exact physical meaning can be charger input vs site grid input |
| ac_output_power | W | direct field | `outputPower`; `acOutputActivePower`; `outputActivePower`; UWB1 `load_power ×1000` | Same key `acOutputActivePower` has different units across models |
| load_power | W | direct load field preferred | `load_power`; `loadPower`; `outputPower` on some topologies | AC output may not equal whole-site load |
| grid_import_power | W | confirmed signed grid field | UWB1: negative half of phase mains sum; MEGA-ECO: positive half of `GridPower` | Sign is opposite across these families |
| grid_export_power | W | confirmed signed grid field | UWB1: positive half of phase mains sum; MEGA-ECO: negative half of `GridPower` | Sign is opposite across these families |
| ac_input_voltage | V | direct metadata | `l1AcInputVoltage`; `inputVoltage` | multi-phase devices may expose phase-specific fields |
| ac_input_frequency | Hz | direct metadata | `acInputFrequency`; `inputFrequency` | — |
| ac_output_voltage | V | direct metadata | `acOutputVoltage`; `inverterVoltage`; `aPhaseOutputVoltage` etc. | phase-aware normalization may be needed |
| ac_output_frequency | Hz | direct metadata | `acOutputFrequency`; `aPhaseOutputFrequency` | — |
| pv_voltage | V | direct metadata | `solarInputVoltage`; `pv1Voltage`, `pv2Voltage` | preserve string/MPPT identity |
| pv_current | A | direct metadata | `pv1Current`, `pv2Current` | preserve string/MPPT identity |
| battery_temperature | °C | direct metadata | `cellTemperature1`; `lithiumBatteryTemperature`; protocol-specific | cell vs pack vs ambient are different concepts |
| inverter_temperature | °C | direct metadata | `inverterTemperature` | — |
| mppt_temperature | °C | direct metadata | `mpptTemperature` | — |
| pv_energy_daily | kWh | direct counter/aggregate | `pvGeneratedEnergyOfDay`; station/device aggregates | Key meaning has varied by model/display |
| pv_energy_total | kWh | direct counter/aggregate | `totalPVGeneratedEnergy`; `totalProducedQuantity`; summary properties | determine reset/rollover behavior before using deltas |
| grid_import_energy | kWh | server summary | `buyElectricityQuantity` | compare against integrated power later |
| grid_export_energy | kWh | server summary | `sellElectricityQuantity` | compare against integrated power later |
| consumption_energy | kWh | server summary | `consumeElectricityQuantity` | server formula unknown |
| battery_charge_energy | kWh | server summary | `chargeElectricityQuantity` | server formula unknown |
| battery_discharge_energy | kWh | server summary | `dischargeElectricityQuantity` | server formula unknown |

## Protocol-specific confirmed mappings

### UWB1 / related energy-flow family

| Raw field | Raw unit | Canonical interpretation |
|---|---:|---|
| `pv1Power`..`pv4Power` | W | sum → PV power W |
| `bmsBatteryVoltage` | V | battery voltage |
| `positiveTerminalBatteryVoltage` | V | battery voltage fallback |
| `batteryPercentage` | % | SOC |
| `bmsSOC` | % | SOC fallback |
| `batteryPower` | W | battery power |
| `load_power` | kW | ×1000 → AC output/load W |
| phase `*MainsPower` | W | negative import, positive export |
| `positiveTerminalBatteryCurrent` | A | negative charge, positive discharge |

### HPVINV02 / Inverter Top One

| Raw field | Meaning | Observed scaling |
|---|---|---|
| `pvPower` | PV power | kW → W |
| `outputActivePower` | AC output power | kW-path behavior in confirmed integration |
| `batteryCapacity` | SOC | % |

### MEGA-ECO machineType 5

| Raw field | Raw unit | Meaning |
|---|---:|---|
| `acOutputActivePower` | W | AC output |
| `pvInputPower` | kW | PV1 |
| `pv2InputPower` | kW | PV2 |
| `generationPower` | kW | total PV |
| `GridPower` | W | positive import, negative export |
| `batteryCapacity` | % | SOC |
| `pvGeneratedEnergyOfDay` | kWh | daily PV generation |

### Current Sierro-style captured family

| Raw field | Unit / meaning |
|---|---|
| `remainingBatteryCapacity` | SOC % |
| `batteryCapacity` | Ah capacity |
| `batteryCurrent` | A |
| `exchangeChargingPower` | W AC charging/input |
| `generationPower` | W PV charging |
| `outputPower` | W AC output |
| `l1AcInputVoltage` | V |
| `pvGeneratedEnergyOfDay` | kWh daily PV |
| `totalPVGeneratedEnergy` | kWh total PV |

## Semantic collisions — mandatory guardrails

| Raw key | Collision |
|---|---|
| `batteryCapacity` | SOC % on HPVINV02/MEGA-ECO; capacity Ah on current Sierro-style catalog |
| `generationPower` | W on current Sierro-style mapping; kW on MEGA-ECO |
| `acOutputActivePower` | W on MEGA-ECO; historical integrations have treated it as kW elsewhere |
| signed grid power | opposite import/export sign conventions between UWB1 and MEGA-ECO |
| `ratedPower` | cloud device values may be kW while local/spec values are commonly W |

## Storage rule

Every normalized measurement should retain:

- raw key;
- raw value;
- raw unit;
- raw display/name;
- deviceId;
- stationId;
- model;
- gather protocol number/version;
- source endpoint;
- source timestamp;
- normalization rule/version;
- measured vs derived flag.

## Data quality flags

Suggested flags:

- `DIRECT_METADATA_CONFIRMED`
- `DIRECT_CAPTURE_CONFIRMED`
- `PROTOCOL_RULE_CONFIRMED`
- `DERIVED`
- `UNIT_AMBIGUOUS`
- `SIGN_AMBIGUOUS`
- `MISSING`
- `STALE`
- `OUT_OF_RANGE`

If a raw field cannot be normalized safely, store it and mark the canonical concept unknown rather than guessing.

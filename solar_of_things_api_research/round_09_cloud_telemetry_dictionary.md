# Round 09 Companion — Cloud Telemetry Dictionary

Date: 2026-09-24

Status: COMPLETE

Companion to `round_09_cloud_data_model_telemetry_dictionary.md`.

## Purpose

This is a cloud-only telemetry dictionary for the Windows/dashboard project.

It deliberately excludes BLE, MQTT interception, raw serial, Android-local protocols, provisioning and device-control research.

The key rule is:

> **Never infer a normalized measurement from the field name alone. Keep the raw field, raw value, API-provided unit/label, endpoint, device model and gather-protocol context.**

The mappings below are evidence-backed examples and normalization rules, not a claim that every inverter exposes the same fields.

## Canonical metric dictionary

| Canonical concept | Observed source field(s) | Source / family | Raw unit / sign | Normalization | Confidence / caution |
|---|---|---|---|---|---|
| PV input power | `pvInputPower` | historical selected-key path on already-supported devices | W | identity | High for those devices |
| PV input power | `pvPower` | HPVINV02 / Inverter Top One historical path | kW | ×1000 → W | Live-confirmed alias |
| PV input power | `pv1Power`..`pv4Power` | UWB1/RWB1-family energy-flow path | W | sum present strings | Live-confirmed |
| PV input power | `pvInputPower`, `pv2InputPower` | MEGA-ECO energy-flow capture | kW | each ×1000 if normalized to W; preserve channels | Model-specific; same key `pvInputPower` differs from historical-path evidence |
| Combined PV generation power | `generationPower` | several energy-flow families | kW in observed MEGA-ECO/UWB-style evidence | ×1000 if used | Preserve separately from PV-channel sum; not universally needed |
| PV channel power | `pv1RealTimePower`, `pv2RealTimePower` | Maniy 11 kW / dual-MPPT energy-flow family | unresolved here | preserve raw + API unit | Strong key evidence; scaling not normalized without own metadata |
| AC output / load power | `acOutputActivePower` | historical selected-key path | kW in Conexo-supported historical path | ×1000 → W | High on that path |
| AC output / load power | `outputActivePower` | HPVINV02 historical alias | kW | alias to `acOutputActivePower`, then ×1000 | Live-confirmed alias |
| AC output / load power | `load_power` | UWB/RWB energy-flow family | kW | ×1000 → W | Unit and mapping confirmed by live captures |
| AC output power | `acOutputActivePower` | MEGA-ECO energy-flow family | W | identity | Critical counterexample: same key, different endpoint/model context |
| Load power | `loadPower` | multiple families incl. Maniy | varies / not globally pinned | preserve raw + supplied unit | Do not assume same scale as `load_power` |
| Battery SOC | `batterySOC` | documented/historical path | % | identity | Common canonical key |
| Battery SOC | `batteryCapacity` | HPVINV02 and MEGA-ECO families | % | alias to SOC | Live-confirmed |
| Battery SOC | `batteryPercentage`, `bmsSOC` | UWB/RWB energy-flow family | % | first available | Live-confirmed |
| Lithium battery remaining capacity | `lithiumBatteryRemainingCapacity` | Maniy family | API metadata required | preserve separately until semantics confirmed | Could be percent/capacity depending protocol |
| Battery voltage | `batteryVoltage` | historical path / many families | V | identity | Common |
| Battery voltage | `bmsBatteryVoltage`, `positiveTerminalBatteryVoltage` | UWB/RWB energy-flow family | V | first available | Live-confirmed |
| Battery power | `batteryPower` | energy-flow family | W in confirmed UWB case | identity | Live-confirmed; preserve sign |
| Battery power | derived from voltage/current | historical path when direct power absent | W | `(dischargeCurrent - chargeCurrent) * voltage` | **Derived estimate**, not raw measurement |
| Battery charging current | `batteryChargingCurrent` | historical path | A | identity | Common historical key |
| Battery discharge current | `batteryDischargeCurrent` | historical path | A | identity | Common historical key |
| Battery current, bidirectional | `positiveTerminalBatteryCurrent` | UWB/RWB energy-flow family | A; negative=charging, positive=discharging | charging=max(0,-x); discharge=max(0,x) | Sign directly confirmed across charge/discharge captures |
| Alternate terminal current | `negativeTerminalBatteryCurrent` | UWB/RWB capture | remained 0 in evidence | do not use generically | Unverified semantic role |
| Grid bidirectional power | `aPhaseMainsPower` + B/C | UWB/RWB energy-flow family | W; **negative=import, positive=export** | import=max(0,-sum); export=max(0,sum) | Directly confirmed including export case |
| Grid bidirectional power | `GridPower` | MEGA-ECO | W; **positive=import, negative=export** | import=max(0,x); export=max(0,-x) | Directly reported model-specific opposite sign |
| Grid feed-in | `feedInPower` | historical path on some families | model/path specific | preserve direct measurement | May be absent entirely on other models |
| Grid import | derived `gridPower` | some historical integrations | W | power-balance estimate | **Derived**, never confuse with measured mains field |
| PV daily energy | `pvGeneratedEnergyOfDay` | MEGA-ECO | kWh; displayed as “Daily Power Gen.” | identity | Confirmed on MEGA-ECO; meaning reportedly differs on other families |
| Station/device daily production | `dailyProducedQuantity` | device/station discovery records | aggregate quantity | preserve API value/unit context | Good dashboard metadata, but exact source semantics should be checked per account |
| Station/device total production | `totalProducedQuantity` | device/station discovery records | cumulative quantity | preserve API value/unit context | Same caution |
| Station PV generated energy | `pvGeneratedEnergy` | `pvInverterElectricityQuantityClass` summaries | kWh in observed summaries | identity when `isRealValue != false` | Strong aggregate candidate |
| Battery charge energy | `chargeElectricityQuantity` | station category summaries | kWh-style aggregate | preserve; validate on target account | Known key, quality can vary |
| Battery discharge energy | `dischargeElectricityQuantity` | station category summaries | kWh-style aggregate | preserve; validate on target account | Known key |
| Consumption energy | `consumeElectricityQuantity` | station category summaries | kWh-style aggregate | **do not trust blindly** | At least one real collector found these summary buckets to be placeholders |
| Grid import energy | `buyElectricityQuantity` | station category summaries | kWh-style aggregate | **do not trust blindly** | Placeholder behavior observed in one real corpus |
| Grid export energy | `sellElectricityQuantity` | station category summaries | kWh-style aggregate | preserve + validate | Known key; target-account validation required |
| Device online state | `state`, `stateDict`, `isOnline` | device list/details | state enum / boolean | preserve both code and label | Device metadata, not telemetry |
| Device current production | `producingPower` | device/station list | API-defined power | preserve supplied unit/context | Useful quick summary; not a substitute for raw telemetry |
| Firmware/software | `softwareVersion`, sometimes state fields such as `firmwareVersion` | device metadata/state | text | preserve text | Never coerce version strings to numbers |

## Confirmed state/metadata envelope

The latest-state representation can carry:

- `deviceId`
- `dtuID`
- `time`
- `stationId`
- `gatherProtocolNumber`
- `gatherProtocolVersionCode`
- `fields`
- `groups`
- `firingAlarms`

Each state field may carry:

- `key`
- `name`
- `nameDisplay`
- `value`
- `valueDisplay`
- `unit`
- `valueType`
- `category`
- `isHidden`

The schema-discovery endpoints additionally expose flags such as:

- `isStateAttribute`
- `isEventAttribute`
- `isConfigAttribute`
- `isReadableConfigAttribute`
- `isWritableConfigAttribute`
- enum/value lookup metadata.

## Safe normalization rules

1. Preserve the **raw API field** before any conversion.
2. Preserve `unit` exactly as returned where available.
3. Preserve `valueDisplay` independently from numeric `value`.
4. Record the source endpoint.
5. Record the device's:
   - model;
   - manufacturer;
   - `deviceSortKey`;
   - `gatherProtocolNumber`;
   - `gatherProtocolVersionId/code`;
   - software version.
6. Apply a normalization rule only when its **endpoint + device/protocol context** matches confirmed evidence.
7. Keep derived metrics marked as derived.
8. Do not turn missing/null telemetry into zero.
9. Do not treat future/placeholder aggregate buckets as real zeroes when `isRealValue=false`.
10. Preserve unknown fields rather than discarding them; they may be useful for the target inverter later.

## Key counterexamples that justify the rules

### Same concept, different keys

Battery SOC can appear as:

- `batterySOC`
- `batteryCapacity`
- `batteryPercentage`
- `bmsSOC`

### Same-looking field, different units

`acOutputActivePower` has been handled as kW in the historical time-series path but was captured as W on MEGA-ECO's energy-flow payload.

### Same concept, opposite sign convention

Grid power:

- UWB/RWB per-phase mains fields: negative import, positive export.
- MEGA-ECO `GridPower`: positive import, negative export.

### Same key, potentially different meaning

`pvGeneratedEnergyOfDay` is genuinely daily energy on MEGA-ECO, while other family evidence has shown different display/semantic treatment.

## Dashboard-facing implication

The future dashboard should never query “give me `pvInputPower` and call it Solar Power” as a universal rule.

The correct flow is:

`device identity/protocol → attribute metadata → raw measurements → device-specific normalization → dashboard metric`.

That design prevents silent 1000× scaling errors, inverted import/export, and mislabelled battery behavior from poisoning historical statistics.

# Round 09 — Cloud Data Model and Telemetry Dictionary

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_09_telemetry_dictionary.md`

## Scope

This is the first round under the project's newly narrowed **cloud-data-only** research scope.

The project goal is not yet fully specified, but the relevant constraint is now authoritative:

- data will be obtained from the Solar of Things / SiSeLi **cloud**;
- the eventual consumer is a Windows dashboard/statistics project;
- dongle-to-cloud interception, BLE, Wi-Fi provisioning, Android-local behavior, raw serial/Modbus and device-control research are out of scope unless the user explicitly reopens them.

This round therefore reconstructs only:

1. the cloud data hierarchy;
2. identifiers and relationships;
3. the shape of device telemetry;
4. the safest canonical meanings for dashboard-relevant measurements;
5. known model/protocol aliases, unit differences and sign differences;
6. which metadata must be retained so later statistics remain correct.

No authenticated request was made against the user's account.

---

## 1. Canonical cloud hierarchy

The evidence supports this practical hierarchy:

`Account/User → Station → DTU / collector / logger → Device / inverter → telemetry attributes`

### Account / user

The authenticated account can own or have access to one or more stations.

Device list/detail objects can include:

- `ownerUserId`;
- `ownerUserName`.

These identifiers are account/platform metadata, not telemetry.

### Station

A station is the cloud-level installation/site container.

Important station fields include:

- `id` / `stationId`;
- name;
- timezone;
- UTC-offset identifier;
- country/province/city/area/address;
- latitude/longitude;
- station type;
- grid-connection type;
- online/state flags;
- installed capacity;
- current/total power fields;
- daily/total produced quantity;
- currency and energy-income price;
- owner identity.

A station can contain multiple devices.

### DTU / logger / collector

A device can expose both:

- `dtuId` — cloud database/platform identifier;
- `dtuDtuid` — collector/logger identifier;
- `dtuName`.

These are **not the inverter serial number**.

The DTU/logger is the communications intermediary between the physical device and SiSeLi.

For the Windows cloud project, DTU fields should be stored as metadata because they are useful for diagnostics and device identity, but the dashboard normally keys telemetry by `deviceId`.

### Device / inverter

Important device metadata includes:

- `id` / `deviceId`;
- name;
- serial number;
- model;
- device sort/type;
- manufacturer ID/name;
- gather-protocol ID/number/name;
- gather-protocol version ID/code;
- station ID/name/timezone;
- DTU identifiers;
- state and online/alarm status;
- software/firmware version;
- rated power;
- producing power;
- daily/total produced quantity;
- installation and last-data/last-online timestamps.

This level is the principal home of realtime and historical telemetry.

---

## 2. Identifier rule

All platform identifiers should be treated as **opaque strings** in the Windows project.

This applies to at least:

- userId;
- stationId;
- deviceId;
- dtuId;
- dtuDtuid;
- gatherProtocolId/versionId;
- alarm/report/batch identifiers.

Reasons:

1. examples are often 18–20 digit values;
2. JavaScript clients have already encountered precision problems with large numeric IDs;
3. IDs have no arithmetic meaning;
4. Swagger numeric typing is not a safe storage instruction for clients.

Never use ID length as validation. Public integrations once described station IDs as “18 digits” and later corrected that wording: IDs of other lengths exist.

---

## 3. Device schema is dynamic

The central design fact is:

**Solar of Things does not expose one universal telemetry schema shared by every inverter.**

The API itself provides device-specific attribute metadata:

`GET /apis/deviceState/simple/gatherAttributes/v1`

with parameters including:

- `deviceId`;
- `category`;
- `renderIn`.

A fresh production capture returned objects shaped approximately as:

`{ key, valueType, name, unit, ... }`

and contained 37 attributes for that particular device.

A richer metadata path can also expose:

- display name;
- value type;
- category;
- operation mode;
- hidden status;
- config/state/event flags;
- readable/writable config flags;
- enum values.

### Engineering consequence

The Windows application should store the server-provided attribute catalog for each device/protocol version and treat it as the first authority for:

- available keys;
- display labels;
- units;
- type information;
- enum interpretation.

A hard-coded dictionary can supply normalized dashboard concepts, but it should never replace raw metadata.

---

## 4. Realtime/current-state data shapes

Strong cloud sources include:

### Latest state

`GET /apis/deviceState/simple/state/latest/v1`

Response conceptually contains:

- timestamp;
- `fields` map keyed by telemetry attribute.

A field can carry:

- key;
- unit;
- raw `value`;
- `valueDisplay`;
- display/name metadata;
- hidden flags.

### Energy flow

`GET /apis/deviceState/simple/energy/flow/v1`

Contains:

- `deviceAttributeState.fields`;
- flow nodes such as PV, grid, battery, load, generator, UPS and CT;
- per-node value, extra values, direction, enabled/light flags.

Some firmware families populate useful realtime values here even when the normal historical/latest path does not.

### Alternate remote latest-state surface

`/remote/device/state/latest`

is used by some contemporary clients and exposes rich battery/portable-power-style fields.

It should be treated as an alternate model-specific source, not assumed to be identical to the simple device-state endpoint.

---

## 5. Historical telemetry data shape

Preferred current production path:

`POST /apis/deviceState/simple/attribute/keys/history/v1`

The response is **columnar**:

`timeSeries[i]`

aligns with:

`fields.<key>[i]`

for every requested key.

A missing measurement at a report frame is represented as `null`.

### Critical statistics rule

**Missing/null is not zero.**

For charts and statistics:

- preserve null as missing;
- do not interpolate automatically unless the statistic explicitly requires it;
- do not turn missing PV/load/current readings into zero;
- do not draw a straight line across long missing periods without marking the gap.

A fresh production capture confirmed that report frames can continue while a particular field becomes null for hours.

---

## 6. Canonical telemetry concepts for a dashboard

The dashboard should normalize raw protocol fields into concepts rather than expose one vendor spelling as the universal truth.

Recommended canonical concepts:

### Power

- PV/solar input power — W
- AC/grid input power — W
- AC/load/output power — W
- grid import power — W
- grid export/feed-in power — W
- battery power — W, with an explicitly documented sign convention

### Battery

- state of charge — %
- battery voltage — V
- battery current — A
- charging current — A
- discharge current — A
- capacity — Ah or Wh **with type/unit retained**
- cycle count — count
- battery/cell temperatures — °C

### AC / PV electrical

- AC input voltage — V
- AC input frequency — Hz
- AC output voltage — V
- AC output frequency — Hz
- PV/string voltage — V
- PV/string current — A
- MPPT/string power — W

### Energy

- daily PV generation — kWh
- total PV generation — kWh
- grid import energy — kWh
- grid export energy — kWh
- consumption — kWh
- battery charged energy — kWh
- battery discharged energy — kWh

### State/quality

- device online/offline;
- last data timestamp;
- operating mode;
- active alarm/fault status;
- source endpoint;
- raw key;
- raw server unit;
- device model/protocol/version.

---

## 7. High-value telemetry family: current Sierro-style fields

A contemporary production-connected client uses this field family:

| Raw field | Normalized concept | Observed/intended unit |
|---|---|---|
| `remainingBatteryCapacity` | battery SOC | % |
| `batteryCapacity` | battery capacity | Ah on captured attribute catalog |
| `batteryCurrent` | battery current | A |
| `numberOfBatteryUsageCycles` | cycle count | cycles |
| `exchangeChargingPower` | AC charging / AC input power | W |
| `generationPower` | solar/PV charging power | W for this family |
| `outputPower` | AC output/load power | W |
| `batteryPower` | battery power | W when directly supplied |
| `l1AcInputVoltage` | AC input voltage | V |
| `acInputFrequency` | AC input frequency | Hz |
| `acOutputVoltage` | AC output voltage | V |
| `acOutputFrequency` | AC output frequency | Hz |
| `solarInputVoltage` | PV input voltage | V |
| `cellTemperature1` | battery/cell temperature | °C |
| `cellTemperature2` | cell temperature | °C |
| `cellTemperature3` | cell temperature | °C |
| `mpptTemperature` | MPPT temperature | °C |
| `dcdcTemperature` | DCDC temperature | °C |
| `pvGeneratedEnergyOfDay` | today's PV energy | kWh in captured attribute catalog |
| `totalPVGeneratedEnergy` | total PV energy | kWh |
| `accumulatedChargingTime` | accumulated charge duration | duration, exact display/base unit should be retained from metadata |
| `accumulatedDischargeTime` | accumulated discharge duration | duration, exact display/base unit should be retained from metadata |

Historical production data directly confirmed:

- `remainingBatteryCapacity`;
- `batteryCurrent`.

The captured attribute catalog directly confirmed units for:

- `remainingBatteryCapacity` = %;
- `batteryCapacity` = Ah;
- `batteryCurrent` = A;
- cell-voltage attributes = V;
- `l1AcInputVoltage` = V;
- `pvGeneratedEnergyOfDay` = kWh;
- `totalPVGeneratedEnergy` = kWh.

Other mappings in the table are strongly supported by current client code but should still defer to the target device's returned metadata.

---

## 8. Cross-model aliases and incompatibilities

This is the highest-risk area for dashboard correctness.

### 8.1 PV power

Observed families:

- `pvInputPower`;
- `pvPower`;
- `pv1Power`, `pv2Power`, up to at least `pv4Power`;
- `pv1RealTimePower`, `pv2RealTimePower`;
- `generationPower`.

Examples:

- documented `pvInputPower` has been observed in native **W** on some integrations;
- HPVINV02 `pvPower` was observed in **kW**;
- UWB1 `pv1Power`/`pv2Power` are reported in **W**;
- MEGA-ECO `pvInputPower`, `pv2InputPower`, and `generationPower` were reported in **kW**;
- the current Sierro-style client treats `generationPower` as **W**.

**Conclusion:** even `generationPower` cannot be assigned a global unit by field name.

### 8.2 AC/load output power

Observed families:

- `acOutputActivePower`;
- `outputActivePower`;
- `outputPower`;
- `load_power`;
- `loadPower`;
- `loadPowerGenerationPower`.

Confirmed contradictions:

- some historical time-series paths use `acOutputActivePower` in kW;
- MEGA-ECO returns the same `acOutputActivePower` key in **W**;
- UWB1 `load_power` is confirmed **kW**;
- current Sierro-style `outputPower` is treated as W.

Again, field name alone is insufficient.

### 8.3 Battery SOC vs capacity — severe semantic collision

Observed SOC keys:

- `batterySOC`;
- `batteryCapacity`;
- `batteryPercentage`;
- `bmsSOC`;
- `remainingBatteryCapacity`;
- `lithiumBatteryRemainingCapacity`.

Critical contradiction:

- HPVINV02 and MEGA-ECO evidence uses `batteryCapacity` as **state of charge (%)**;
- a current Sierro-style attribute catalog uses `batteryCapacity` as actual **battery capacity in Ah**.

This is not merely a unit variation: **the same raw key can represent two different concepts depending on device protocol.**

A generic dashboard must never normalize `batteryCapacity` without the per-device metadata/protocol context.

### 8.4 Grid power — opposite sign conventions

UWB1-family evidence:

- phase fields such as `aPhaseMainsPower`;
- unit W;
- **negative = grid import**;
- **positive = grid export/feed-in**.

The export sign was confirmed with a real export capture and power-balance arithmetic.

MEGA-ECO evidence:

- `GridPower`;
- unit W;
- **positive = import**;
- **negative = export**.

The two families therefore use opposite signs for conceptually similar grid power.

Never derive grid import/export from sign without a protocol-specific rule.

### 8.5 Battery current

UWB1-family evidence:

`positiveTerminalBatteryCurrent`

was observed:

- negative while charging;
- positive while discharging.

A separate `negativeTerminalBatteryCurrent` stayed zero in the confirmed captures and should not be given an assumed meaning.

Other device families expose:

- `batteryChargingCurrent`;
- `batteryDischargeCurrent`;
- `batteryCurrent`;
- `batteryChargingAndDischargingCurrent`;
- `lithiumBatteryChargeDischargeCurrent`.

Direction/sign must be learned per protocol or confirmed from the metadata/display semantics.

---

## 9. Derived values: useful but lower authority

Some applications derive values when a direct server measurement is absent.

Examples:

### Battery power

One integration derives:

`(dischargeCurrent - chargeCurrent) × batteryVoltage`

A contemporary client derives:

`AC charging power + solar charging power - output power`

These are **not equivalent physical models in every topology**.

If the API supplies a direct `batteryPower`, prefer it.

If deriving battery power:

- store that it is derived;
- record formula/source fields;
- do not mix derived and measured series invisibly.

### Load power

Some clients use AC output power as load power.

That is reasonable on certain inverter topologies but may not equal whole-site consumption when there are bypass/grid paths or additional loads.

### Grid import

One integration estimates grid import from:

`AC output - PV + batteryPower + feedIn`

when no direct grid reading exists.

A direct signed grid-power field is preferable when its unit/sign has been confirmed for that model.

---

## 10. Energy totals and station summaries

The cloud exposes pre-aggregated energy quantities.

A known station summary family uses:

`summaryCategoryKey=pvInverterElectricityQuantityClass`

Observed properties include:

- `pvGeneratedEnergy`;
- `chargeElectricityQuantity`;
- `dischargeElectricityQuantity`;
- `consumeElectricityQuantity`;
- `buyElectricityQuantity`;
- `sellElectricityQuantity`.

These correspond approximately to:

- PV generated energy;
- battery charged energy;
- battery discharged energy;
- total consumption;
- grid import;
- grid export.

They are highly relevant to a statistics dashboard because the server may already provide monthly/yearly aggregates.

However:

**Do not yet assume they are mathematically equivalent to integrating the realtime power series.**

That comparison belongs to the later aggregation/statistics round.

---

## 11. Device list/detail data useful for statistics

Even before requesting telemetry, device metadata contains useful summary/state fields.

Observed examples:

- `producingPower`;
- `ratedPower`;
- `dailyProducedQuantity`;
- `totalProducedQuantity`;
- direct-today/direct-total PV-generation helpers on some clients;
- `lastDataAt`;
- `lastOnlineAt`;
- `lastOfflineAt`;
- `state`;
- `isOnline`;
- `isAlarmed`.

### Rated power warning

A contemporary project found published device records where `ratedPower = 5.0` represented a 5 kW inverter and therefore treats device-list `ratedPower` as kW.

Other local application model types describe rated power in W.

Therefore the cloud `ratedPower` field must retain its raw value/context and should not be merged blindly with local model specifications expressed in W.

---

## 12. Status codes and device-state model

One contemporary device-list model records platform state values:

- 10 = Alarm;
- 20 = Online;
- 30 = Offline;
- 40 = Fault.

It also retains:

- `stateDict`;
- `isOnline`;
- `isAlarmed`.

For dashboard availability/freshness, prefer explicit server fields plus timestamps rather than infer online status solely from whether the latest numeric telemetry value is nonzero.

---

## 13. Model/protocol identity must be persisted with data

At minimum, store these alongside every device:

- device ID;
- station ID;
- DTU ID/DTUID;
- model;
- manufacturer;
- device sort/type;
- gather protocol number;
- gather protocol version ID/code;
- software/firmware version;
- station timezone.

Why:

A future schema mapping can change when firmware/protocol changes.

Without this metadata, historical readings can become impossible to reinterpret correctly after discovering that:

- a key changed meaning;
- the same key used another unit;
- a sign convention differed;
- a new firmware changed aliases.

---

## 14. Recommended raw + normalized storage model

For every telemetry point, preserve **both** raw and normalized representations.

Suggested conceptual record:

`timestamp_utc`
`station_timezone`
`station_id`
`device_id`
`dtu_id / dtuDtuid`
`model`
`gather_protocol_number`
`gather_protocol_version`
`source_endpoint`
`raw_key`
`raw_value`
`raw_unit`
`raw_name/display`
`normalized_metric`
`normalized_value`
`normalized_unit`
`normalization_rule_version`
`quality/confidence`

### Why keep raw data

Suppose six months from now we discover that one firmware's `generationPower` was kW rather than W.

If raw key/value/unit/protocol metadata were retained, old data can be re-normalized.

If only the normalized W number was stored, the historical database may be permanently corrupted.

---

## 15. Suggested normalization-confidence levels

### Level A — server metadata + observed nonzero behavior

Best.

Examples:

- UWB1 `load_power` = kW;
- UWB1 phase mains power = W with confirmed sign;
- current captured `remainingBatteryCapacity` = %;
- current captured `batteryCurrent` = A.

### Level B — server metadata only

Usually safe for units/types, but physical interpretation may still need confirmation.

### Level C — strong cross-client mapping

Multiple clients agree, but no captured nonzero sample/unit metadata has been preserved.

### Level D — inferred/derived

Examples:

- battery power calculated from currents/voltage;
- load inferred from AC output;
- grid power reconstructed by energy balance.

Derived values must be marked as such.

---

## 16. Canonical mapping strategy for the future Windows dashboard

Do **not** write code resembling:

`if key == "generationPower": value_is_watts`

Instead use:

1. device/protocol identity;
2. returned attribute metadata and unit;
3. raw key;
4. per-protocol mapping rule;
5. validation constraints;
6. optional arithmetic consistency checks.

Conceptually:

`normalizer(protocol, model, key, serverUnit, value) -> canonical measurement`

If no safe rule exists:

**retain raw value but leave canonical metric unknown.**

Unknown is preferable to contaminating statistics.

---

## 17. Data-quality checks worth implementing later

For statistics, useful sanity tests include:

### Bounds

- SOC normally 0–100%;
- negative physical voltage is suspicious;
- impossible powers above a reasonable multiple of rated capacity should be flagged;
- U16-like sentinel values such as 65534 should not be treated as real power.

### Power balance

Where topology permits, compare approximately:

`PV + grid import + battery discharge ≈ load + battery charge + grid export + conversion losses`

This should be a diagnostic, not a forced correction.

### Timestamp/freshness

Compare:

- telemetry timestamp;
- device `lastDataAt`;
- current clock;
- online status.

A stale nonzero value must not be labeled “live”.

### Unit consistency

If metadata says kW but previous firmware mapping said W, log the schema change instead of silently applying the old rule.

---

## 18. Current telemetry families with especially strong evidence

### Current Sierro-style family

Strongly useful fields include:

- remainingBatteryCapacity;
- batteryCurrent;
- exchangeChargingPower;
- generationPower;
- outputPower;
- cellTemperature1;
- pvGeneratedEnergyOfDay;
- totalPVGeneratedEnergy.

### UWB1 / related energy-flow family

Confirmed mappings include:

- sum `pv1Power`..`pv4Power` → PV power in W;
- `bmsBatteryVoltage` or `positiveTerminalBatteryVoltage` → battery voltage;
- `batteryPercentage` or `bmsSOC` → SOC;
- `batteryPower` → battery power W;
- `load_power × 1000` → AC output/load W;
- phase mains W values:
  - negative half → grid import;
  - positive half → grid export;
- `positiveTerminalBatteryCurrent`:
  - negative half → charging current magnitude;
  - positive half → discharge current magnitude.

### HPVINV02

Confirmed aliases:

- `pvPower` → canonical PV power, captured in kW;
- `outputActivePower` → AC output power;
- `batteryCapacity` → SOC %.

### MEGA-ECO machineType 5

Confirmed observations:

- `acOutputActivePower` = W;
- `pvInputPower` = kW;
- `pv2InputPower` = kW;
- `generationPower` = kW;
- `GridPower` = W:
  - positive import;
  - negative export;
- `batteryCapacity` = SOC %;
- `pvGeneratedEnergyOfDay` = kWh and explicitly displayed as daily generation on that model.

### Maniy 11 kW / dual-MPPT family

A live report exposed 104 fields, including:

- `pv1RealTimePower`, `pv2RealTimePower`;
- PV voltages/currents;
- `generationPower`;
- `gridConnectedPower`;
- `inputMainsPower`;
- `loadPower`;
- `batteryChargeDischargeRealTimePower`;
- battery voltage/current/capacity variants;
- extensive flag/mode/temperature fields.

The field inventory is strong evidence of richness, but the semantics/units are not sufficiently mapped yet to normalize the whole set.

It should be used as evidence that discovery must remain dynamic, not as a source for guessed conversions.

---

## 19. Important semantic collisions

The following are specifically unsafe to normalize globally:

| Raw field | Why unsafe globally |
|---|---|
| `batteryCapacity` | Ah capacity on one device family; SOC % on others |
| `generationPower` | W on current Sierro-style mapping; kW on MEGA-ECO |
| `acOutputActivePower` | documented/time-series integrations have used kW; MEGA-ECO reports W |
| `pvGeneratedEnergyOfDay` | genuinely daily on MEGA-ECO; evidence from another family suggests display/meaning can differ |
| grid signed power | UWB1 and MEGA-ECO use opposite import/export sign conventions |
| `ratedPower` | cloud device records can express kW while local model definitions often use W |

This table should be treated as a mandatory design constraint for any statistical database.

---

## 20. What this round establishes

The cloud data model is mature enough for later dashboard design.

We now know:

1. what the main cloud entities are;
2. which identifiers link them;
3. where realtime and historical telemetry live;
4. how schema metadata can be discovered dynamically;
5. which normalized metrics are useful for dashboard/statistics;
6. why raw field/unit/protocol metadata must be preserved;
7. which important field collisions and aliases already exist across inverter families;
8. which server-side energy totals are promising for later statistical analysis.

---

## 21. Remaining uncertainties relevant to this scope

These are deliberately carried into later cloud-only rounds:

- exact retention depth of high-resolution historical telemetry;
- actual sampling/report cadence by device and online state;
- pagination semantics and practical maximum points;
- server-side downsampling over long ranges;
- whether historical field units can change after firmware/protocol changes;
- consistency between device daily/total counters and station summary aggregates;
- reset/rollover behavior of cumulative counters;
- timezone boundary behavior for “day”, “month” and “year” aggregates;
- whether station-level consumption/import/export totals reconcile with raw power integration;
- handling of gaps/offline intervals;
- exact telemetry dictionary for the user's own inverter.

Those belong to the surviving historical/statistics/reliability/live-validation rounds rather than further local-protocol research.

---

## 22. Sources deeply examined

- `vvkor/python-siseli`: device/station/state/history models and parsers.
- `Conexo-Casa/solar-of-things-ha`: current mappings, tests, releases and issues #7, #13, #16, #19, #21, #24 and #32.
- `Hyllesen/solar-of-things-solar-usage`: station energy-summary property usage.
- `lujian1324-spec/energy-app`: fresh production history capture, attribute catalog, current field models and device metadata contracts.
- Existing Rounds 02, 03, 05 and 06 in this repository.

No MQTT, BLE, Android-local or raw-serial evidence was used as a dependency for the cloud telemetry model.

---

## 23. Round-close assessment

**Round 09 is complete.**

Data model and telemetry dictionary belong together: separating them would reduce rigor because field meaning depends directly on the device/protocol identity model.

### Next surviving investigation

Proceed separately to:

**Round 10 — Historical Data Granularity, Retention, Pagination and Gaps**

This is now the highest-value next round because a statistics dashboard depends not only on knowing what a field means, but on knowing:

- how often it is recorded;
- how far back it can be retrieved;
- whether long queries are downsampled/truncated;
- how pages work;
- how null/offline periods are represented;
- where local-day boundaries occur.

The server-side aggregation/statistics investigation should remain a later separate round so raw-history behavior is understood before comparing it with platform-calculated totals.

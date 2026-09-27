# Source Attribution and Historical Configuration Investigation — 2026-09-27

Status: **ACTIVE / CANONICAL CONTINUITY CHECKPOINT**

Purpose: preserve the investigation required to compute family-facing household energy attribution correctly:

- Solar → House;
- Battery → House;
- Utility/Enel → House;
- Solar → Battery where supportable;

without treating today's inverter configuration as if it had always been active.

This document consolidates:
- real target-PC SQLite evidence supplied through Navicat queries;
- the earlier Solar of Things / SiSeLi API research already in this repository;
- current application implementation findings;
- locked product decisions from the reporting review;
- next investigation and implementation steps.

It exists specifically so a new chat can resume this work without reconstructing the reasoning.

---

## 1. Product requirement

The family report must answer where household consumption came from.

Required charts include:

1. stacked household-consumption columns split into:
   - direct solar → house;
   - battery → house;
   - utility/Enel → house;
2. solar production + household consumption + estimated battery energy stored;
3. household consumption + one series per supply origin.

These charts must respect the report aggregation selected by the user.

Source attribution is **not optional/deferred product scope**. If attribution is temporarily unavailable, the report should retain the intended chart/section and state why it is unavailable, but resolving attribution is a priority.

Do not substitute:
- total PV generation for direct PV → house;
- total battery discharge for battery → house;
- total grid import for grid → house when grid → battery is possible.

---

## 2. Battery chart decision

Do not use SOC (%) as the primary family-facing battery series when a meaningful energy estimate can be calculated.

Configured useful battery capacity:
- default/current family reference: **11.776 kWh**;
- configurable through `BatteryConfigurationService`.

Estimated energy stored at a valid SOC sample:

`estimated_stored_kwh = usable_capacity_kwh × SOC / 100`

This remains explicitly labeled **estimated**.

For grouped reports, the intended battery line is the estimated stored energy at the end of each selected aggregation bucket, subject to data coverage and continuity rules.

SOC remains valid technical evidence and may stay in technical/detail views.

---

## 3. Battery family-label decisions

Keep these concepts visible in the family Battery page.

Preferred wording direction:
- **CARGA ACTUAL DE LA BATERÍA**;
- **ENERGÍA GUARDADA EN LA BATERÍA (ESTIMADA)**;
- **ENERGÍA DISPONIBLE ANTES DE PASAR A ENEL (ESTIMADA)**;
- **RESERVA PARA CORTES DE LUZ (ESTIMADA)**;
- **NIVEL MÍNIMO PROTEGIDO DE LA BATERÍA**.

Do **not** remove the minimum protected level from the family page.

The minimum protected level needs a plain-language explanation equivalent to:
- it is the lower battery level the system attempts not to cross in order to protect the battery;
- it is especially relevant during outages or exceptional low-energy operation;
- it is not ordinary nightly usable capacity.

Keep an explicit “estimated” marker on derived energy values.

The previously recorded Battery Live/freshness requirement remains active:
- when a fresh authenticated current snapshot exists, current-state cards should prefer it;
- historical calculations remain tied to the stored validated corpus.

---

## 4. Real target SQLite evidence — first inventory

Earlier Navicat extracts established:

### 4.1 Target device/profile

- model: `HPVINV02`;
- gather protocol number: `MH2083139`;
- dataSource: `1`;
- gather attributes: `SUPPORTED`;
- energy flow: `SUPPORTED`.

The commissioned attribute catalog contains 87 historical/telemetry/config-style keys in the first full inventory.

Examples confirmed by catalog:
- `generationPower`;
- `pvPower`;
- `outputActivePower`;
- `mainsPower`;
- `batteryCapacity`;
- `bmsCurrentSOC`;
- battery voltage/current fields;
- `bmsReturnsToBatteryModeSOC`;
- `bmsReturnsToMainsModeSOC`;
- voltage return thresholds;
- charging-current limits;
- charging-time settings.

No explicit historical key named SBU, LBU, OSO, output-source priority or charger-source priority was present in the 87-key historical attribute catalog.

This does **not** prove those modes are unavailable from Solar of Things; it proves they are not present as ordinary `history_sample` keys in the currently discovered catalog.

### 4.2 Historical power fields already normalized

Current model-specific normalization uses:
- `generationPower` or `pvPower` → `pv_power_w`;
- `outputActivePower` → `house_load_power_w`;
- `mainsPower` → `grid_import_power_w`;
- battery voltage and directional charge/discharge currents → derived `battery_power_w`;
- BMS SOC → `battery_soc_pct`.

For HPVINV02, the repository research independently confirms:
- `pvPower` captured in kW;
- `outputActivePower` is the AC output/load power;
- `batteryCapacity` is SOC %.

---

## 5. Real target SQLite evidence — completed-backfill low-cardinality scan

After the user completed the historical update, Navicat query 4 returned 56 attributes with <=20 distinct non-missing values.

Many configuration-like parameters now contain **43,499 historical rows**, confirming the completed corpus is materially larger than the earlier partial extract.

Important low-cardinality values:

- `bmsReturnsToBatteryModeSOC`: **50, 95**;
- `bmsReturnsToMainsModeSOC`: **20**;
- `bmsLowPowerSOC`: **10**;
- `bmsAutomaticallyStartsSOCAfterLow`: **50**;
- `returnToBatteryModeVoltage`: **53, 54 V**;
- `returnToMainsModeVoltage`: **46, 51 V**;
- `maxUtilityChargeCurrent`: **30 A**;
- `maximumTotalChargingCurrent`: **60 A**;
- `mainsChargingStartingTime`: **0**;
- `mainsChargingEndingTime`: **0**.

Many other protection/charging settings remained constant in the scanned period.

This proves that some inverter configuration values are stored repeatedly in historical frames and that at least some changed over time.

---

## 6. Real target SQLite evidence — exact detected changes

Navicat query 5 used `LAG()` and returned the configuration transitions below.

Initial observed values at:

`2026-04-20T20:55:36.1860000+00:00`

included:
- `bmsAutomaticallyStartsSOCAfterLow = 50`;
- `bmsLowPowerSOC = 10`;
- `bmsReturnsToBatteryModeSOC = 95`;
- `bmsReturnsToMainsModeSOC = 20`;
- `mainsChargingStartingTime = 0`;
- `mainsChargingEndingTime = 0`;
- `maxUtilityChargeCurrent = 30`;
- `maximumTotalChargingCurrent = 60`;
- `returnToBatteryModeVoltage = 54`;
- `returnToMainsModeVoltage = 46`.

Detected changes:

### 2026-08-09T17:23:27.7130000+00:00
- `returnToBatteryModeVoltage: 54 → 53 V`;
- `returnToMainsModeVoltage: 46 → 51 V`.

### 2026-08-18T14:03:07.0420000+00:00
- `bmsReturnsToBatteryModeSOC: 95 → 50 %`.

No change was detected in the scanned history for:
- `bmsReturnsToMainsModeSOC = 20 %`;
- `bmsLowPowerSOC = 10 %`;
- the listed charging-current/time values.

Therefore historical attribution/behavior analysis must use **as-of configuration**, not current constants.

The current family policy 20/10/50 is not valid as a blanket retrospective assumption for the whole downloaded history.

---

## 7. Real target SQLite evidence — raw captures

Navicat query 6 grouped `raw_api_capture` and found only:

### LatestStateSnapshot
- source: `state/latest/v1`;
- 12 captures;
- first capture: `2026-09-26T16:52:27.9085770+00:00`;
- last capture: `2026-09-27T01:14:34.0428811+00:00`.

### selected-key-history
- source: `selected-key-v1`;
- 167 captures;
- first capture: `2026-09-25T20:49:04.3166139+00:00`;
- last capture: `2026-09-27T01:16:05.9482978+00:00`.

There is **no persisted EnergyFlow raw capture** in the current database.

---

## 8. Critical implementation finding — EnergyFlow response is currently discarded

Current `CommissioningService` does call:

`GET deviceState/simple/energy/flow/v1?deviceId=<id>&dataSource=<selected>`

For the target profile this call succeeded, which is why:
- `energy_flow_status = SUPPORTED`.

However, unlike the successful latest-state path, commissioning currently does **not** call `HistoryRepository.CaptureRaw(...)` for the EnergyFlow response.

It only stores the capability status.

This is a concrete implementation gap.

The earlier API research had explicitly recommended that target commissioning record:
- source timestamp;
- raw fields;
- API units;
- flow nodes;
- capability/business errors.

The next technical tranche must preserve successful energy-flow JSON snapshots.

---

## 9. Relevant findings recovered from the original API research

The initial multi-round Solar of Things / SiSeLi research remains directly useful.

### 9.1 Energy flow is a separate structured current surface

Current production officially calls:

`GET /apis/deviceState/simple/energy/flow/v1?deviceId=<id>&dataSource=1`

The response can contain:
- `deviceAttributeState.fields`;
- flow nodes such as:
  - PV;
  - grid;
  - battery;
  - load;
  - generator;
  - UPS;
  - CT;
- per-node:
  - value;
  - extra values;
  - direction;
  - enabled/light flags.

Research explicitly notes that some firmware families populate useful realtime values in EnergyFlow even when ordinary latest/history surfaces do not.

For the target device, EnergyFlow is already commissioned as **SUPPORTED**.

### 9.2 EnergyFlow has no established historical endpoint

Research Round 10 concluded:
- structured EnergyFlow is current-only;
- no historical endpoint for its complete structured view was established;
- raw constituent fields may exist in historical telemetry.

Therefore:
- we cannot assume we can retroactively query historical flow-node structures;
- we should begin snapshotting EnergyFlow locally going forward;
- past attribution must use historical constituent fields + historical configuration + validated target-specific derivation.

### 9.3 Current configuration is also a separate surface

The API research catalog found read-only/current configuration routes, including:
- `POST /apis/remote/device/configs/cache/get`;
- `POST /apis/remote/device/configs/read`;
- `GET /apis/remote/device/configs/read/details`;
- individual config read routes.

The earlier research considered current config outside the then-minimal dashboard scope.

That scope decision must now be revisited because historical/current source attribution depends on knowing inverter operating configuration.

Important:
- configuration writes remain strictly out of scope;
- only read-only capture is relevant;
- no historical config API was established;
- current config snapshots should therefore be persisted locally if future changes matter.

### 9.4 Dynamic schema remains the correct design

The research repeatedly established:

`attribute metadata + current state + device/protocol context`

must be preferred over a universal inverter field list.

This remains true for:
- mode/status keys;
- energy flow;
- grid sign;
- power units;
- configuration semantics.

### 9.5 Power balance is a diagnostic, not a forced correction

Research Round 09 recommends checking approximately:

`PV + grid import + battery discharge ≈ load + battery charge + grid export + conversion losses`

This must be used to:
- validate attribution;
- estimate residual/loss magnitude;
- flag inconsistent intervals.

It must **not** be used to force a fabricated perfect balance.

---

## 10. Installation-specific operating contract already in repo

The family installation contract documents the intended/current configuration:

- P01 source priority: **SBU** = solar → battery → grid;
- P16 charging source priority: **OSO** = intended solar-only battery charging;
- P43 solar allocation priority: **LBU** = house/load before battery charging;
- P39 normal transfer to grid: about **20% SOC**;
- P40 return from grid to SBU: current target about **50% SOC**;
- P41 restart after low SOC: about **50% SOC**;
- mandatory zero export.

Family behavior:
1. solar supplies house first;
2. solar surplus charges battery;
3. battery supplies deficit while in normal SBU operation;
4. at the configured low-SOC transfer threshold, house can transfer to grid;
5. grid can remain the house source while solar recovers battery toward the return threshold;
6. intended configuration does not use grid to charge battery;
7. zero export is an invariant.

This contract is evidence for the **current/intended installation**, not permission to rewrite earlier history as though it always had the same settings.

---

## 11. Source-attribution design direction

Historical attribution must be **dynamic and evidence-driven**.

### 11.1 Build an as-of configuration timeline

For each configuration parameter available historically:
- sort by timestamp;
- detect transitions;
- create effective intervals;
- for each power frame, use the latest known value at or before that timestamp.

Do not apply current 20/10/50 thresholds retrospectively when historical values differ.

### 11.2 Prefer direct flow evidence

Evidence hierarchy:

1. validated direct EnergyFlow/source field when available;
2. validated target-device-specific derivation;
3. unresolved/unattributed.

### 11.3 Candidate derived attribution under validated SBU/LBU/OSO intervals

Where the historical/current configuration and measured behavior support SBU + load-first solar + no grid charging:

- direct solar → house is limited by both available PV and house demand;
- battery → house can fill remaining house demand while battery is discharging and grid is not materially supplying;
- grid → house supplies house during validated grid-transfer/grid-supply intervals;
- solar surplus can charge battery;
- measured battery charge/discharge is evidence and must be reconciled;
- significant residual or contradictory direction/state invalidates confident attribution for that interval.

Do not freeze the final formulas until they are validated against target data.

### 11.4 Grid-total distinction remains important

`mainsPower` / total grid import is not automatically identical to grid → house.

If grid-to-battery charging is positively excluded by:
- configuration;
- battery power direction;
- PV conditions;
- observed behavior;

then grid import may be attributed to the house with stronger confidence.

If not, preserve the distinction.

### 11.5 Coverage becomes source-attribution coverage

Each attribution bucket should carry:
- observable/attributable duration;
- unattributed duration;
- attribution coverage %;
- quality/confidence;
- residual/balance diagnostics.

Family totals/charts must not hide unattributed intervals by treating them as zero.

---

## 12. Important unresolved historical-mode issue

The ordinary 87-key historical attribute catalog does **not** expose explicit:
- SBU;
- LBU;
- OSO;
- output priority;
- charger source priority.

The original target-model research established that electrically similar SUNPRO/SPRO-family equipment supports source-priority modes such as:
- UTL;
- SOL;
- SBU;
- SUB.

But that earlier research did not prove the exact SiSeLi key for the commissioned HPVINV02 target.

Therefore historical priority-mode reconstruction is still unresolved.

Possible evidence paths, in order:

1. inspect fields in the 12 persisted `LatestStateSnapshot` JSON objects for hidden/current mode/status keys;
2. start persisting successful `EnergyFlow` responses;
3. add read-only current-config snapshotting from the remote-config routes;
4. inspect whether any mode/status key exists in current state/config but not gather-history;
5. for past history, infer only where electrical behavior + historical thresholds make the operating state unambiguous;
6. leave ambiguous periods unattributed.

Do not manufacture historical SBU/LBU state solely from today's manual settings.

---

## 13. Next Navicat query — inspect current LatestStateSnapshot field keys

The next useful DB query is:

```sql
SELECT
    j.key AS field_key,
    COUNT(*) AS snapshots,
    COUNT(DISTINCT json_extract(j.value, '$.value')) AS valores_distintos,
    GROUP_CONCAT(DISTINCT json_extract(j.value, '$.value')) AS valores
FROM raw_api_capture AS r,
     json_each(json_extract(r.response_json, '$.fields')) AS j
WHERE r.operation = 'LatestStateSnapshot'
GROUP BY j.key
ORDER BY j.key;
```

Purpose:
- discover fields returned by current state that are not in the historical gather catalog;
- look specifically for mode/source/priority/status/config-related values;
- determine whether any useful current attribution field has already been captured.

If the target SQLite build lacks JSON1 support, fall back to exporting one sanitized `LatestStateSnapshot.response_json` value.

---

## 14. Next implementation tranche — required before another manual Reporting QA

Do not request another target-PC Reporting build for tiny visual fixes.

The next consolidated technical tranche should include, at minimum:

### Data acquisition / provenance
- persist successful EnergyFlow snapshots;
- inspect and normalize target energy-flow node structure;
- investigate/read current configuration using read-only endpoints if required;
- persist current config snapshots if safely obtainable;
- preserve raw provenance.

### Dynamic attribution
- add a versioned target-specific source-attribution service;
- build/use historical as-of configuration timelines;
- compute attribution coverage and balance diagnostics;
- integrate attribution by the report-selected aggregation;
- retain unresolved intervals.

### Reporting
- enable the three agreed charts when evidence supports them;
- keep blocked chart spaces visible with a clear explanation until attribution is validated;
- replace SOC family line with estimated stored battery kWh;
- ensure selected report aggregation governs charts;
- complete the consolidated Excel/PDF redesign already recorded elsewhere.

### Battery UI
- prefer fresh/live current state where safe;
- keep explicit estimate labels;
- retain minimum protected level with improved explanation;
- apply the approved clearer family terminology.

---

## 15. QA sequencing

The user has completed the historical backfill before this checkpoint.

The next human semantic QA should use the completed/high-coverage corpus.

Before asking for that QA:
1. resolve/implement target source attribution as far as supported;
2. run deterministic synthetic tests;
3. validate attribution against real historical balance/statistics where possible;
4. complete reporting/chart/glossary/layout tranche;
5. CI green;
6. produce one consolidated portable.

Do not return to one-small-fix-per-build QA.

---

## 16. Key continuity warning

The most important facts to preserve across chats are:

1. **Historical configuration is demonstrably dynamic.**
2. **Current SBU/LBU/OSO family policy cannot be applied blindly to the whole history.**
3. **EnergyFlow is supported on the target device but its successful JSON is currently discarded by commissioning.**
4. **Original API research already mapped EnergyFlow and read-only current-config surfaces.**
5. **No full historical EnergyFlow/config endpoint was established.**
6. **Past attribution therefore needs historical constituent telemetry + as-of configuration + target-specific validation.**
7. **Source attribution is now a priority blocker for the family report, not a deferred nice-to-have.**
8. **Battery family charts should use estimated stored kWh, not SOC %, while SOC remains technical evidence.**


---

## 17. LatestStateSnapshot hidden/current configuration fields — confirmed from target DB

A Navicat JSON scan over the 12 persisted `LatestStateSnapshot` captures revealed that the current-state payload contains a much richer field set than the 87-key historical gather catalog.

This directly invalidates the earlier working assumption that priority/mode fields may simply be unavailable from Solar of Things.

Important current-state-only/config-like fields observed:

| field | observed value(s) in 12 snapshots | interpretation status |
|---|---:|---|
| `chargingPriorityOrder` | `2` | likely charger-source-priority enum; exact enum mapping not yet proven |
| `pvEnergyFeedingPriority` | `1` | likely PV allocation/load-vs-battery priority enum; exact enum mapping not yet proven |
| `workingMode` | `1` | operating-mode enum; exact meaning not yet proven |
| `outputModel` | `0` | output/source-related enum; exact meaning not yet proven |
| `mode` | `B` | current operating state/mode code; exact meaning not yet proven |
| `acChargingSwitch` | `0` | AC charging disabled in all captured snapshots |
| `solarChargingSwitch` | `0,1` | solar charging state/switch changed across snapshots |
| `chargingMainSwitch` | `0,1` | charging master state/switch changed |
| `powerSupplyFromPVToLoadInACState` | `0` | PV→load behavior-related flag; exact semantics require enum/field metadata |
| `mainsCurrentFlowDirection` | `+` | current flow direction marker |
| `mainsPower` | `0` | no material grid power in these 12 specific snapshots |
| `pvEnergyFeedingPriority` | `1` | stable in these snapshots |
| `chargingPriorityOrder` | `2` | stable in these snapshots |

Other useful current-state evidence observed:
- `generationPower` / `pvPower`;
- `outputActivePower`;
- battery charge/discharge currents;
- BMS charge/discharge currents;
- BMS SOC;
- battery voltage;
- grid input voltage;
- current thresholds;
- current charging switches and lights;
- current battery/working status.

### Why this matters

The historical `gatherAttributes` catalog does not include the priority/mode fields above, but `state/latest/v1` does.

Therefore:
- current operating/configuration interpretation can be materially richer than historical raw-key history;
- current snapshots should preserve these fields as evidence;
- future snapshots can build a local time series of mode/priority changes even if the cloud offers no historical API for them;
- historical attribution before local snapshotting still requires measured-flow inference + historically available numeric settings.

### External/manual corroboration

Public manuals/catalogs for this inverter family confirm the relevant configuration concepts exist:
- output source priority modes including Utility/Solar/SBU/SUB-family behavior;
- charger-source priority including Solar First / Solar+Utility / Only Solar;
- solar/load priority where solar can feed load before battery charging;
- target-family SUNPRO/SPRO documentation explicitly lists multiple output priorities including UTL, SOL, SBU and SUB.

However, **the numeric JSON enum mapping is not yet proven**.

Do not freeze:
- `chargingPriorityOrder=2 → OSO`;
- `pvEnergyFeedingPriority=1 → LBU`;
- `workingMode=1 → ...`;
- `outputModel=0 → ...`;

until the target response metadata, remote-config display values, official-client mapping, or controlled read-only comparison proves the mapping.

### Next evidence query: inspect candidate field objects, not only `.value`

Run this against the target DB:

```sql
SELECT
    r.retrieved_utc,
    json_extract(r.response_json, '$.fields.chargingPriorityOrder') AS chargingPriorityOrder_object,
    json_extract(r.response_json, '$.fields.pvEnergyFeedingPriority') AS pvEnergyFeedingPriority_object,
    json_extract(r.response_json, '$.fields.workingMode') AS workingMode_object,
    json_extract(r.response_json, '$.fields.outputModel') AS outputModel_object,
    json_extract(r.response_json, '$.fields.mode') AS mode_object,
    json_extract(r.response_json, '$.fields.powerSupplyFromPVToLoadInACState') AS pvToLoadInAc_object
FROM raw_api_capture AS r
WHERE r.operation = 'LatestStateSnapshot'
ORDER BY r.retrieved_utc;
```

Purpose:
- determine whether each field object contains `valueDisplay`, label, enum text, unit or other metadata;
- avoid inferring enum meanings from current settings alone.

If these objects contain only `value`, the next evidence source is read-only remote configuration / official-client mapping.

### Battery-energy chart remains locked

For family reporting:
- use estimated stored battery energy in kWh as the main battery quantity;
- retain `SOC` as technical evidence;
- formula remains configured useful capacity × SOC fraction;
- keep explicit “(estimado)” labeling.



---

## 18. Recovered enum evidence from original SiSeLi/API research

A re-read of `solar_of_things_api_research/round_02_cloud_rest_api_repository_archaeology.md` recovered an important mapping that predates this Reporting investigation.

Cross-repository SiSeLi issue captures documented:

- `outputSourcePrioritySetting`:
  - `0 = USO`;
  - `1 = SUB`;
  - `2 = SBU`.

- `chargerSourcePrioritySetting`:
  - `0 = CSO` — Solar First;
  - `1 = SNU` — Solar + Utility;
  - `2 = OSO` — Only Solar.

Model aliases were also observed in the earlier research:
- FCHAO `setOutputSourcePriority`;
- FCHAO `chargeSourcePrioirty` typo/alias.

### Relationship to the target LatestState fields

Target current-state snapshots expose:

- `chargingPriorityOrder = 2`;
- `pvEnergyFeedingPriority = 1`;
- `outputModel = 0`;
- `workingMode = 1`;
- `mode = B`.

Interpretation status:

#### `chargingPriorityOrder = 2`
There is now **strong corroboration** that this may be a model-specific alias for charger-source priority, because:
- the field name is semantically equivalent;
- the known SiSeLi charger-priority enum uses `2 = OSO`;
- the family installation's intended/current charger mode is OSO;
- target snapshots also show `acChargingSwitch = 0`.

However, the alias itself is not yet proven by target metadata/config-read output.

Until proven, label as:
- **PROBABLE: OSO / solar-only battery charging**.

#### `pvEnergyFeedingPriority = 1`
Public manuals for this inverter family identify the PV allocation setting as:
- BLU = battery before load;
- LBU = load before battery.

External inverter-family protocol documentation commonly encodes:
- `0 = BLU`;
- `1 = LBU`.

The target installation's documented current setting is LBU and the target snapshot shows `pvEnergyFeedingPriority = 1`.

Therefore:
- **PROBABLE: LBU / PV feeds household load before battery charging**.

This is a stronger inference than before, but the target field's own enum metadata remains preferable evidence.

#### `outputModel = 0`
Do **not** equate this automatically with `outputSourcePrioritySetting`.

If it shared the earlier SiSeLi output-priority enum, `0` would imply USO, which conflicts with the documented current target SBU configuration. The field name is different and may represent another output mode/function.

Status:
- **UNRESOLVED**.

#### `workingMode = 1` / `mode = B`
These appear more likely to describe current operating state than persistent priority configuration.

The twelve snapshots were captured while `mainsPower = 0` and several show battery discharge / PV behavior, which is compatible with a battery/inverter operating state. That is not sufficient to define the enum.

Status:
- **UNRESOLVED operating-state enum**.

### Do not conflate configuration with current operating mode

The attribution model needs both:

1. persistent/effective configuration:
   - output source priority;
   - charger source priority;
   - PV allocation priority;
   - SOC/voltage transfer thresholds;

2. current observed operating state:
   - grid active/bypass;
   - inverter/battery supplying;
   - PV active;
   - battery charging/discharging.

A current `mode=B` observation must not replace the historical configuration timeline.

### Remaining best evidence step

Inspect the full target field objects and/or read-only config cache before hard-coding aliases.

Preferred target evidence order:

1. saved `state/latest/v1` field object with display/enum metadata;
2. `remote/device/configs/cache/get` read-only response;
3. `remote/device/configs/read` + details read-only response;
4. official-client mapping;
5. only then cross-family/manual inference.



---

## 19. Target enum mappings confirmed by Solar of Things `valueDisplay`

This section **supersedes** the provisional enum interpretations in §§17–18.

A Navicat extraction of the complete JSON objects for the target `LatestStateSnapshot` fields shows that Solar of Things itself returns `valueDisplay` labels.

Confirmed target mappings:

| field | raw value | Solar of Things `valueDisplay` | status |
|---|---:|---|---|
| `chargingPriorityOrder` | `2` | `OSO` | **CONFIRMED TARGET** |
| `pvEnergyFeedingPriority` | `1` | `LBU` | **CONFIRMED TARGET** |
| `workingMode` | `1` | `SBU` | **CONFIRMED TARGET** |
| `mode` | `B` | `Battery Mode` | **CONFIRMED TARGET** |
| `outputModel` | `0` | `SIG` | **CONFIRMED TARGET LABEL** |
| `powerSupplyFromPVToLoadInACState` | `0` | `No` | **CONFIRMED TARGET** |

Across all 12 saved current-state snapshots:
- `chargingPriorityOrder` remained OSO;
- `pvEnergyFeedingPriority` remained LBU;
- `workingMode` remained SBU;
- `outputModel` remained SIG;
- `powerSupplyFromPVToLoadInACState` remained No;
- `mode` remained Battery Mode.

### Consequences

The current target configuration is now directly confirmed by Solar of Things as:
- **SBU** working mode;
- **OSO** charging priority;
- **LBU** PV energy feeding priority.

This is no longer an inference from the family manual.

The current operating-state code:
- `mode = B` means **Battery Mode** in the saved snapshots.

The `outputModel = SIG` field must not be treated as output-source priority. External/manual evidence for this inverter family uses `SIG` as the single-unit/parallel-mode selection alongside values such as PAR / 3P1 / 3P2 / 3P3, which is consistent with `outputModel` being a topology/output-configuration field rather than SBU/utility priority.

### Historical limitation remains

These confirmed mode/priority fields are present in `state/latest/v1`, but they are **not present in the ordinary historical 87-key gather catalog currently stored in `history_sample`**.

Therefore:
- current SBU/OSO/LBU is confirmed;
- future local snapshots can preserve changes in these fields;
- past SBU/OSO/LBU changes cannot yet be reconstructed directly from the existing historical DB;
- historical attribution before local snapshot coverage must still use:
  - historically available thresholds/config values;
  - measured PV/load/grid/battery telemetry;
  - validated behavioral inference;
  - attribution coverage/confidence;
  - unresolved state when evidence is insufficient.

### Next implementation priority

The next consolidated technical tranche should:
1. promote these current-state fields into a versioned current-configuration/context snapshot model;
2. persist successful EnergyFlow raw JSON;
3. begin locally timestamping SBU/OSO/LBU changes going forward;
4. attempt a safe read-only historical/API capability probe for these keys only if supported by the platform;
5. never backfill historical mode labels by assuming the current setting applied earlier.



---

## 20. Reproducible debug harness requirement

The user explicitly requires the next clean-build QA to avoid ad-hoc Navicat/manual-query cycles.

Developer Diagnostics must provide buttons/functions that both:
1. export the evidence already present locally; and
2. actively resolve remaining read-only uncertainties by querying the target.

Required read-only probes:
- LatestState refresh/capture;
- EnergyFlow capture;
- remote config cache capture;
- direct/batch remote configuration read + details capture.

Required export bundle:
- historical attribute inventory;
- low-cardinality values;
- detected historical configuration/value changes;
- history-day coverage;
- normalized metrics;
- raw-capture inventory;
- LatestState all fields + SBU/OSO/LBU candidate subset;
- installation config checks;
- behavior state counts;
- full observable power-balance matrix and worst residuals;
- latest sanitized raw evidence for LatestState/EnergyFlow/config;
- commissioned schema/capability metadata;
- recent sanitized API diagnostics.

The one-click “complete diagnostics” path should execute the safe probes and then create the bundle.

Safety invariant:
- no config write;
- no cache clear;
- no passthrough;
- no DTU restart;
- no fast-report start/stop;
- no mutation endpoint.

This harness exists specifically so a future chat can request one exported ZIP rather than reconstructing SQL manually.


---

## 21. First high-coverage QA + structured EnergyFlow finding — 2026-09-27

The first consolidated high-coverage 7-day report reached ~99.7% metric coverage and ~92% household-source attribution coverage.

### Report QA
Confirmed:
- all three required family charts render;
- the stacked chart closes to household consumption by keeping `Sin atribuir` visible;
- technical Detail exposes attribution and balance diagnostics;
- Quality and Glossary are materially expanded.

Corrections identified:
- Excel Patrones event-summary merged rows can visually overlap without explicit heights;
- PDF used the misleading label `Tiempo total observado` for the total duration of reserve+grid episodes even when the actual value was zero.

Both are queued in the next consolidated build.

### EnergyFlow target evidence
The saved target EnergyFlow response at the investigated frame exposed:
- `pvPanelFlow = 0.000 kW`;
- `gridFlow = 0.567 kW`, active, direction `1`;
- `loadFlow = 0.446 kW`, active, direction `2`;
- `batteryFlow`, active, direction `2`;
- `mode = Mains Mode`;
- `workingMode = SBU`;
- `chargingPriorityOrder = OSO`;
- `pvEnergyFeedingPriority = LBU`;
- battery voltage `51.4 V`;
- BMS charging current `1.8 A`;
- BMS discharge current `0 A`;
- SOC `20%`.

Derived battery power from the state fields is about:
`51.4 V × (0 - 1.8 A) ≈ -92.5 W`, where negative is charging.

At the same frame:
- grid minus household load ≈ `121 W`;
- PV is zero.

This is physically consistent with a small utility-supported battery charge/maintenance flow plus conversion losses.

Important:
- do not reinterpret OSO configuration solely from one frame;
- do not silently claim billing-grade Grid→Battery energy;
- repeat/quantify this behavior from historical physical evidence and future EnergyFlow captures.

The next debug bundle will therefore include:
- candidate grid-charge frames requiring PV≈0, grid active, battery charging and grid surplus over house;
- gap-aware candidate duration and integrated diagnostic energy;
- parsed current EnergyFlow summary.

### Debug config-read correction
The first target run showed:
- `configs/cache/get`: HTTP/backend error because request body was absent;
- `configs/read`: same error.

Research recorded these calls as POSTs with no JSON *fields*, but the live Spring controller now requires a body object.

Next implementation sends `{}` rather than no body and preserves the response.

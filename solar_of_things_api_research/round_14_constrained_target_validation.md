# Round 14 — Constrained Target-Hardware Validation and First-Run Self-Discovery Contract

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE WITH EXPLICIT LIVE-VALIDATION LIMITATION

Companion: `round_14_first_run_self_validation_matrix.md`

## Purpose

This is the final planned round of the Solar of Things / SiSeLi cloud API research.

The originally intended final phase was a controlled read-only validation against the user's actual Solar of Things account/device.

That is **not possible in the present research environment** because the user cannot provide:

- account credentials;
- access/refresh tokens;
- cookies;
- HAR/session capture;
- device/station IDs;
- any other authenticated account artifact.

The limitation is accepted as a project constraint.

This round therefore performs the maximum useful final work that remains possible:

1. record and investigate the known target hardware;
2. determine whether public evidence can map it to a known SiSeLi device/protocol family;
3. explicitly identify what cannot be proven without the account;
4. convert the missing live validation into a **first-run self-discovery contract** for the future Windows client;
5. close the research without pretending account-specific validation occurred.

No authenticated Solar of Things request was made.

---

## 1. Known target hardware

User-supplied target description:

> Inversor SPRO-6200, 230 V, sistema de batería nominal de 48 V.

The user also states that this inverter is monitored through Solar of Things using its 2.4 GHz Wi-Fi connection.

For project purposes, the physical target is therefore:

- approximately 6.2 kW inverter;
- 220/230 VAC class;
- nominal 48 V battery system;
- Wi-Fi cloud monitoring through Solar of Things / SiSeLi.

---

## 2. Public hardware identification

### 2.1 Strong Chilean retail match

SPRO Energy Chile currently lists:

**INVERSOR ONDA PURA SUNPRO 6.200W 48VDC 220VAC BUILT IN 100A MPPT 60-500V**

SPRO Energy product code:

`INVER-0053`

The product page describes:

- SUNPRO brand;
- 6,200 W;
- 48 VDC battery side;
- 220 VAC;
- integrated 100 A MPPT;
- PV range 60–500 VDC;
- lithium/BMS compatibility;
- optional Wi-Fi module for remote monitoring;
- dimensions approximately 495 × 312 × 125 mm;
- weight approximately 8.5 kg.

A SPRO Energy 6,000 Wh/day kit also pairs its system with a:

**SUNPRO 6.2 kW 48 V inverter**

This is a strong commercial match to the user's description.

Sources:

- https://www.sproenergy.cl/inversor-onda-pura-sunpro-6200w-48vdc-220vac-built-in-100a-mppt-60-500v
- https://www.sproenergy.cl/kit-de-energia-solar-6000whdia-mppt-litio

### Identification level

**HIGH confidence that the user's “SPRO-6200” description refers to this SUNPRO 6.2 kW / 48 V product family.**

It is **not** yet sufficient to prove the exact internal/OEM model identifier or SiSeLi gather protocol.

---

## 3. Candidate deeper model identities

Public sources expose at least two plausible model identifiers for essentially the same 6.2 kW / 48 V product class.

### 3.1 SUNPRO catalog identity: SP6200-48L

A SUNPRO manufacturer catalog lists:

`SP6200-48L`

with:

- 6.2 kVA / 6.2 kW;
- nominal 230 VAC input;
- 220/230 VAC output;
- 48 V battery;
- PV input 60–500 VDC;
- MPPT;
- lithium battery activation;
- lithium BMS communication via RS485;
- RS232 / RS485;
- optional Wi-Fi remote monitoring;
- output-priority modes including UTL/SOL/SBU/SUB.

This is an excellent electrical/specification match.

Source:

- SUNPRO SMART catalog, manufacturer domain `sunpropower.com`.

### 3.2 Retail/OEM identity: GA6248MH

A Chilean marketplace listing for the SUNPRO 6,200 W / 48 V product identifies its model as:

`GA6248MH`

A separate manufacturer/OEM product page from Wenzhou Smartdrive also lists `GA6248MH` with:

- 6,200 W rated power;
- 48 VDC;
- 220/230/240 VAC;
- 100 A charge/MPPT class;
- Wi-Fi expansion support;
- BMS communication support;
- the same approximate 495 × 312 × 125 mm physical format.

Sources:

- MercadoLibre Chile SUNPRO 6,200 W listing;
- Wenzhou Smartdrive GA6248MH product page.

### 3.3 Interpretation

These identifiers may represent:

- retail versus OEM naming;
- regional branding;
- hardware revision;
- shared chassis/platform sold under multiple model names;
- or closely related but non-identical 6.2 kW products.

The evidence is **not strong enough** to declare:

`SP6200-48L == GA6248MH == user's exact inverter`

as a proven identity.

### Canonical rule

Store both as **candidate hardware-family identifiers**, not as confirmed SiSeLi model names.

---

## 4. Search for Solar of Things / SiSeLi model mapping

Targeted searches were performed for:

- `SPRO-6200`;
- `SPRO 6200`;
- `SP6200-48L`;
- `GA6248MH`;
- SUNPRO 6.2 kW;
- each model combined with Solar of Things / SiSeLi;
- public GitHub code for those exact identifiers.

### Result

No credible source was found that maps:

- `SP6200-48L`;
- `GA6248MH`;
- or “SPRO-6200”

to a specific:

- Solar of Things `deviceSortKey`;
- `gatherProtocolNumber`;
- `gatherProtocolVersion`;
- `dataSource`;
- telemetry alias family.

The exact model strings also do not appear in the public Solar of Things integration/reverse-engineering repositories searched.

### Conclusion

**PUBLIC HARDWARE IDENTITY IS NOT A SAFE SUBSTITUTE FOR CLOUD PROTOCOL IDENTITY.**

Even with the likely SUNPRO family identified, the Windows client must still discover the actual SiSeLi identity from the authenticated cloud account.

---

## 5. What the public hardware match does tell us

Although it cannot determine the cloud schema, the hardware match provides useful plausibility constraints.

A 6.2 kW / 48 V off-grid hybrid inverter of this class can reasonably expose cloud concepts such as:

- PV input voltage/current/power;
- AC input/grid voltage/frequency;
- AC output/load voltage/frequency/power;
- battery voltage;
- battery SOC;
- charge/discharge current;
- battery charge/discharge power;
- daily/lifetime PV generation;
- operating/source-priority mode;
- alarms/fault state;
- BMS-related state when a communicating lithium battery is present.

These are **expected concept categories**, not guaranteed API keys.

The exact SiSeLi field names, units and sign conventions remain dynamic and must be read from metadata/state.

---

## 6. What cannot be validated in Round 14

Without authenticated account/device access, this round cannot prove:

### Cloud identity

- station ID;
- device ID;
- DTU/logger ID;
- serial as stored by SiSeLi;
- SiSeLi model/manufacturer string;
- device sort/type;
- gather protocol;
- gather protocol version;
- firmware/software version;
- correct `dataSource`.

### Telemetry schema

- exact PV keys;
- exact load/output keys;
- exact grid keys;
- exact battery keys;
- field units;
- sign conventions;
- which fields are historized.

### Historical behavior

- actual reporting cadence for this installation;
- oldest recoverable raw date;
- pagination behavior on this specific account;
- null/offline pattern;
- late-upload delay.

### Aggregates

- which summary category properties exist;
- which carry `isRealValue=true`;
- whether load/grid/battery summary properties are placeholders;
- agreement with Solar of Things UI.

### Reliability/session

- exact access-token TTL;
- exact refresh behavior for this account/session;
- cloud lag between physical inverter and API;
- device online/offline lag.

These remain **UNVALIDATED TARGET-ACCOUNT FACTS**.

---

## 7. Final research substitution: local first-run self-validation

Because the user cannot export account artifacts to the research process, the future Windows program itself must perform the missing validation **locally after the user logs in on their own computer**.

This is a better solution than hard-coding assumptions from public hardware labels.

The program should execute an automatic, read-only commissioning sequence and persist the resulting capability profile locally.

No account data needs to leave the user's Windows machine.

---

## 8. First-run self-discovery sequence

### Phase 1 — Authenticate locally

1. User enters Solar of Things credentials into the Windows application.
2. Application MD5-processes password as required by the protocol.
3. Application calls `POST /login/account`.
4. Tokens are stored using Windows-appropriate protected storage.
5. Password is not logged.
6. Application records server-provided token expiry metadata.

### Phase 2 — Discover account hierarchy

Read:

- `POST /station/list`;
- `GET /station/details`;
- `POST /device/list`;
- `GET /device/details`.

Persist locally:

- station ID/name/timezone;
- device ID/name/serial;
- model;
- manufacturer if returned;
- device sort;
- DTU identity;
- gather protocol/version;
- software version;
- installation/creation dates;
- current online/status fields.

All platform IDs stored as strings.

### Phase 3 — Discover telemetry schema

Call:

`GET /deviceState/simple/gatherAttributes/v1`

Persist every returned attribute:

- key;
- name/display name;
- unit;
- value type;
- category;
- hidden/display flags;
- state/config/event role.

Do not initially discard unknown attributes.

### Phase 4 — Discover current state

Try:

`GET /deviceState/simple/state/latest/v1`

with evidence-backed `dataSource` choices.

Use the first valid source as the device profile's current-state source.

Then call:

`GET /deviceState/simple/energy/flow/v1`

when supported.

Record:

- source timestamp;
- raw fields;
- API units;
- flow nodes;
- capability/business errors.

If `70132` occurs, mark energy flow unsupported/unconfigured instead of failing commissioning.

### Phase 5 — Discover history

For a narrow recent local-time period:

`POST /deviceState/simple/attribute/keys/history/v1`

using a small set of available attributes selected from the discovered schema.

Validate:

- response shape;
- page semantics;
- cadence;
- null behavior;
- station timezone;
- newest source timestamp.

Then optionally compare with:

`/deviceState/simple/attribute/record/list/v1`

during commissioning only.

### Phase 6 — Discover server aggregates

Probe read-only station/device summary families for:

- PV generation;
- load/consumption;
- grid import;
- grid export;
- battery charge;
- battery discharge.

Persist:

- property key;
- unit;
- `isRealValue`;
- `hasRealTimePoints`;
- bucket granularity.

A property with `isRealValue=false` is disabled as an authoritative dashboard source.

### Phase 7 — Build local capability profile

Create a local profile such as:

```text
device:
  physical_family_hint: SUNPRO 6.2kW / 48V
  candidate_public_models:
    - SP6200-48L
    - GA6248MH

cloud_identity:
  device_id: <string>
  model: <from SiSeLi>
  gather_protocol: <from SiSeLi>
  gather_protocol_version: <from SiSeLi>
  data_source: <validated>

capabilities:
  latest_state: true/false
  energy_flow: true/false
  selected_key_history: true/false
  record_history: true/false
  alarms: true/false

metric_rules:
  pv_power: ...
  load_power: ...
  grid_import_power: ...
  grid_export_power: ...
  battery_soc: ...
  battery_power: ...

aggregate_rules:
  pv_energy: ...
  load_energy: ...
  grid_import_energy: ...
  grid_export_energy: ...
  battery_charge_energy: ...
  battery_discharge_energy: ...
```

This becomes the authoritative profile for that physical installation.

---

## 9. Safe automatic metric matching

The commissioning process may propose metric candidates based on known aliases from Round 09.

It must **not silently activate a rule solely because a key name looks familiar**.

A metric rule can be promoted only after checking:

1. key exists;
2. unit is compatible;
3. value range is physically plausible;
4. endpoint/context matches a known mapping or UI-equivalent behavior;
5. sign convention is established where direction matters.

### Example: PV power

Candidate keys might include:

- `pvInputPower`;
- `pvPower`;
- `pv1Power`...`pv4Power`;
- `pv1RealTimePower`;
- `generationPower`.

The program should inspect the actual returned metadata instead of assuming one.

### Example: battery SOC

Candidates:

- `batterySOC`;
- `batteryCapacity`;
- `batteryPercentage`;
- `bmsSOC`.

Again, API unit/context decides.

---

## 10. Local plausibility checks for this 6.2 kW hardware family

The public hardware family gives useful sanity bounds.

These are **diagnostic bounds**, not exact clipping limits.

### AC/PV power

A reported normal continuous AC output far above the 6.2 kW class is suspicious and may indicate:

- wrong unit;
- wrong field;
- scaling error.

### Battery voltage

Nominal system is 48 V.

A normal battery-voltage measurement should therefore be in the broad 48 V battery-system range, not:

- 4–5 V;
- 480–500 V.

Values around the mid-40s to high-50s V are physically plausible depending on battery chemistry/state.

### PV voltage

The matched hardware family supports high-voltage MPPT input, publicly advertised broadly in the 60–500 VDC class.

A PV voltage in that range is plausible.

### Battery SOC

If the API metadata labels a candidate as percent, plausible range is 0–100.

### Purpose

These checks can catch:

- 1000× unit mistakes;
- accidental wrong-field selection;
- string parsing errors.

They must not replace API metadata.

---

## 11. What the client should do if metric identity remains ambiguous

Do not guess.

Possible states:

- `CONFIRMED`;
- `PROBABLE`;
- `UNRESOLVED`;
- `UNAVAILABLE`.

For an unresolved metric:

1. preserve raw telemetry;
2. omit or label the normalized dashboard statistic;
3. continue collecting data;
4. allow later rule updates without losing the original data.

This is preferable to silently showing the wrong number.

---

## 12. Automatic history-retention discovery

Because target-account retention cannot be measured here, the Windows application can determine it locally.

Recommended:

1. identify device installation/creation date;
2. probe recent history;
3. test increasingly older local dates;
4. find a lower boundary;
5. refine around first available history;
6. backfill one local day at a time;
7. paginate;
8. record completeness.

An empty historical day must not automatically be interpreted as “older than retention”; it may represent an offline/no-production period.

Use multiple neighboring days when finding the boundary.

---

## 13. Automatic aggregate trust calibration

For the first few complete days after commissioning/backfill, compare candidate totals.

### PV generation

Compare:

- server `pvGeneratedEnergy` aggregate;
- device daily generation counter;
- integral of raw PV power.

If server aggregate is real-valued and agrees reasonably, promote it as primary.

### Load/grid/battery

Repeat with whichever aggregate properties and telemetry counters exist.

If an aggregate is:

- `isRealValue=false`;
- permanently zero while telemetry clearly shows activity;
- grossly inconsistent;

demote it and use the validated fallback.

The chosen source rule should be persisted per device/protocol.

---

## 14. Optional user-visible verification without exporting data

If the future implementation needs one human cross-check, the program can show locally:

```text
Solar of Things API says:
PV Power: X
Load Power: Y
Battery SOC: Z
Grid: import/export Q
```

and ask the user to compare those figures with the official Solar of Things screen.

The comparison result can remain local.

No screenshot/HAR/token needs to be sent anywhere.

This provides the missing semantic validation while respecting the user's inability to provide account artifacts.

---

## 15. Candidate target identity — final status

| Fact | Status |
|---|---|
| User description: SPRO-6200, 230 V, nominal 48 V battery | CONFIRMED BY USER |
| Chilean SPRO Energy sells SUNPRO 6,200 W / 48 V / 220 V inverter | PUBLICLY CONFIRMED |
| Product uses optional Wi-Fi remote monitoring | PUBLICLY CONFIRMED |
| SUNPRO catalog has 6.2 kW `SP6200-48L` / closely named BIS6200-48L family | PUBLICLY CONFIRMED |
| Chilean marketplace lists SUNPRO 6,200 W as `GA6248MH` | PUBLICLY OBSERVED |
| Exact equality of these model codes | UNRESOLVED |
| Exact user's internal model code | UNVALIDATED |
| Exact SiSeLi model/gather protocol | UNVALIDATED |
| Exact telemetry schema | UNVALIDATED |
| Solar of Things cloud compatibility of user's installed unit | CONFIRMED BY USER'S ACTUAL USE |
| Need for Android to collect cloud data | NO |
| Need to intercept 2.4 GHz Wi-Fi traffic | NO |

---

## 16. Impact on implementation risk

The inability to perform live Round 14 validation does **not** block development of the generic cloud client.

It changes one architectural choice:

### Unsafe design

```text
SPRO-6200 detected → assume hard-coded field list X
```

### Safe design

```text
login
→ discover actual SiSeLi device identity
→ download attribute schema
→ validate current data source
→ inspect actual fields/units
→ build local normalization profile
→ validate aggregates
→ collect
```

The latter architecture was already favored by Rounds 09–13 and is now mandatory.

---

## 17. What is sufficiently known to begin implementation later

The research has established enough to implement:

- cloud login/signing;
- token/session handling;
- station/device discovery;
- device attribute discovery;
- latest-state acquisition;
- energy-flow acquisition when available;
- historical data;
- pagination/backfill;
- alarms;
- server aggregation;
- freshness/retry behavior;
- dynamic normalization;
- local capability profiling.

The missing account-specific information can be discovered by those same APIs at runtime.

---

## 18. What should NOT be hard-coded for the SPRO/SUNPRO target

Do not hard-code:

- `SP6200-48L` as the cloud model;
- `GA6248MH` as the cloud model;
- one gather protocol;
- one `dataSource`;
- one PV-power key;
- one battery-SOC key;
- one grid sign convention;
- one energy-summary property;
- a fixed retention period;
- a fixed five-minute exact sampling grid.

All are discoverable or testable at runtime.

---

## 19. Final research status

### Public cloud platform research

**COMPLETE FOR THE STATED PROJECT SCOPE**

### Target hardware identification

**PARTIALLY VALIDATED**

Strong likely family:

**SUNPRO / SPRO Energy 6.2 kW, 48 V, 220/230 V off-grid hybrid inverter**

Candidate public model codes:

- `SP6200-48L` / `BIS6200-48L`;
- `GA6248MH`.

### Target Solar of Things cloud profile

**NOT LIVE-VALIDATED**

Reason:

user cannot provide authenticated account/session/device artifacts.

This is a factual limitation, not a failed research task.

### Mitigation

**FIRST-RUN LOCAL SELF-DISCOVERY CONTRACT DEFINED**

The future Windows application can perform the missing validation locally, automatically and read-only.

---

## 20. Round-close decision

**Round 14 is complete under the constrained scope.**

No additional investigation round is justified now.

The research phase is closed.

The next project activity should occur only after the user explains the full application/dashboard requirements.

At that point the implementation/design phase can select:

- which discovered API capabilities are actually needed;
- which statistics to persist;
- required UI/dashboard views;
- database architecture;
- runtime commissioning/self-validation flow.

If implementation-time self-discovery uncovers one genuinely unmapped cloud value, open a narrow targeted research task for that value only.

Do not reopen BLE, MQTT interception, local serial, Android-local internals, provisioning or device-control research unless the project scope is explicitly changed.

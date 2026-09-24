# Round 14 Companion — First-Run Self-Validation Matrix

Date: 2026-09-24

Status: COMPLETE

Companion to `round_14_constrained_target_validation.md`.

## Known target-hardware clues

| Item | Current status |
|---|---|
| User-described model | SPRO-6200 |
| AC class | 230 V |
| Battery system | nominal 48 V |
| Cloud platform in actual use | Solar of Things |
| Communication path relevant to project | cloud via Wi-Fi |
| Strong retail family match | SUNPRO 6.2 kW / 48 V / 220 V |
| Candidate public model code | SP6200-48L / BIS6200-48L |
| Alternate candidate public/OEM code | GA6248MH |
| Exact SiSeLi model string | unknown until local login |
| Exact gather protocol/version | unknown until local login |
| Exact dataSource | unknown until local validation |
| Exact telemetry alias family | unknown until local validation |

## First-run read-only commissioning sequence

| Step | Endpoint / action | What to persist | Pass condition |
|---|---|---|---|
| 1 | Login | token expiry metadata | successful session |
| 2 | `station/list` | station IDs/names/timezones | ≥1 accessible station |
| 3 | `station/details` | station metadata | valid station detail |
| 4 | `device/list` | device IDs/names/serial/model/protocol context | target device visible |
| 5 | `device/details` | full device identity | usable device metadata |
| 6 | `gatherAttributes/v1` | raw attribute catalog, names, units, types | non-empty schema |
| 7 | `state/latest/v1` | source timestamp + raw state | one valid dataSource found |
| 8 | `energy/flow/v1` | raw flow nodes/fields | success OR clean capability failure |
| 9 | selected-key history | recent raw samples | valid aligned history |
| 10 | optional record-list history | broad raw state | commissioning comparison only |
| 11 | station/device aggregates | properties + units + isRealValue | aggregate capability map |
| 12 | alarms | current/history capability | query succeeds or no alarms |
| 13 | calibration | normalized metric candidates | no ambiguous metric silently promoted |
| 14 | retention discovery | oldest useful date | bounded backfill start |
| 15 | persist profile | local device capability profile | future collection can run without rediscovery every cycle |

## Metric-validation rules

| Metric | Candidate evidence | Promote only when |
|---|---|---|
| PV power | pvInputPower, pvPower, pv1..pvNPower, generationPower | unit/context plausible and source validated |
| Load/output power | acOutputActivePower, outputActivePower, load_power, loadPower | unit/context plausible |
| Battery SOC | batterySOC, batteryCapacity, batteryPercentage, bmsSOC | metadata says % / range plausible |
| Battery voltage | batteryVoltage, BMS/terminal voltage aliases | plausible 48-V-system range |
| Battery power | direct batteryPower or validated derived value | direction/sign established |
| Grid import/export | phase mains fields, GridPower, other model-specific keys | sign convention explicitly established |
| PV daily energy | real server aggregate / device daily counter | agrees reasonably with independent source |
| Load energy | validated aggregate or device daily counter | placeholder behavior excluded |
| Grid import energy | validated aggregate or daily purchase counter | placeholder behavior excluded |
| Grid export energy | validated aggregate/counter | target-device evidence exists |
| Battery charge/discharge energy | validated aggregate/counter/direct-power integration | target-device evidence exists |

## Capability-state vocabulary

Use:

- `CONFIRMED`
- `PROBABLE`
- `UNRESOLVED`
- `UNAVAILABLE`

Never silently convert `UNRESOLVED` into `CONFIRMED`.

## Local plausibility checks for the likely 6.2 kW / 48 V family

| Quantity | Diagnostic expectation |
|---|---|
| AC output power | normal continuous values should be in a 6.2-kW-class range |
| Battery voltage | broad 48-V-system range; mid-40s to high-50s V physically plausible depending chemistry/state |
| Battery SOC | 0–100 if API metadata defines percent |
| PV voltage | high-voltage MPPT family, broadly within advertised 60–500 VDC class |
| 1000× scaling error | suspect when power greatly exceeds plausible inverter rating without corresponding unit metadata |

These are sanity checks, not substitutes for API-provided units.

## Account-specific facts that remain unavailable before implementation

- station/device IDs;
- cloud model/manufacturer;
- gather protocol/version;
- exact firmware;
- correct dataSource;
- actual raw field catalog;
- actual field units/signs;
- raw retention depth;
- current aggregate property quality;
- cloud lag;
- token/session timing for the target account.

## Final decision

The research phase is complete.

The future Windows client must treat its first authenticated run as a **local, read-only commissioning/validation phase** and build a per-device capability profile before normal dashboard collection begins.

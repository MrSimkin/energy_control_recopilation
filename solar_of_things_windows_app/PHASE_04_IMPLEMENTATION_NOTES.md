# Phase 4 — Normalization and Installation Context — Implementation Notes

Date started: 2026-09-25

Status: **IN PROGRESS**

## Purpose

Phase 4 converts the Phase 3 raw SiSeLi corpus into trustworthy household-energy metrics.

As of 2026-09-25 this phase has two explicit layers: **protocol/device normalization** and **installation-specific contextual interpretation**.

Canonical target-house reference:

- `INSTALLATION_BEHAVIOR_CONTRACT.md`
- `MANUAL_FAMILIAR_INTEGRATION_REVIEW_2026-09-25.md`

## Already implemented before / during manual integration

- SQLite schema v6 normalized metric layer;
- versioned normalization rule provenance;
- target-device HPVINV02 normalizer;
- target-device fields aligned to live energy-flow evidence;
- normalized:
  - PV power;
  - house/load power;
  - grid import power;
  - battery SOC;
  - battery voltage;
  - probable derived battery power;
  - PV daily/total counters;
- confidence / quality states;
- rebuild normalization locally from raw history without another cloud download;
- dashboard cards can read normalized local metrics;
- local Data & Updates coverage screen;
- parent-friendly wording and tooltips;
- local battery useful-capacity setting defaulting to 11.776 kWh.

Recent code builds through the manual-driven battery wording changes are CI green.

## Family manual integration

Source:
`Manual_Familiar_SPRO_6200_LC230_Midea_v2_ES.docx`

Source SHA-256:
`f519a39be14950258ce51d3cbb3a7e69cbc6b23769b2ae9e47c77ca71f5a3bde`

User confirmation:
the recommended inverter changes are currently applied.

### Architectural boundary

The inverter data does not change because of this household's chosen setup.

- raw telemetry remains raw evidence;
- normalized watts/volts/SOC/energy retain device/protocol physical meaning;
- the family manual does **not** alter those values;
- the manual/settings add context for reserve semantics, expected behavior, configuration comparison and family-facing explanations.

This separation is canonical.

### Important semantic corrections

1. Do not call `SOC × 11.776 kWh` simply “available energy”.
2. Separate:
   - estimated stored energy;
   - ordinary-use energy above 20%;
   - 20%→10% outage reserve;
   - protected ~10% floor.
3. Grid use from ~20% SOC while the battery solar-recovers toward ~50% can be expected behavior.
4. Current baseline expects no grid battery charging.
5. Zero export is a household invariant.
6. Solar should feed house/load before surplus charges battery.
7. Seasonal operation changes load timing more than inverter protection thresholds.
8. P40=60% is only a measured-response contingency for repeated oscillation, not a seasonal default.
9. Midea 1.5 kW schedule is useful contextual load metadata but is not a control-integration requirement.

## Installation-aware tranche implemented — 2026-09-25

Implemented after the family-manual review:

- SQLite schema v7 `installation_config_check` snapshot;
- read-only `InstallationHealthService` using the latest saved **current-state snapshot** for contextual configuration comparison; it does not alter normalized telemetry;
- expected-state checks for:
  - SBU source priority;
  - PYL battery protocol;
  - OSO solar-only charging priority;
  - normal BMS communication;
  - 10% absolute floor;
  - 20% normal transfer-to-grid threshold;
  - 50% return-to-battery/SBU threshold;
  - 50% restart-after-low threshold;
  - LBU house-first solar priority;
  - grid connection/injection disabled;
  - battery equalization disabled;
- statuses persisted as:
  - `CONFIG_CONFIRMED`;
  - `CONFIG_DRIFT`;
  - `CONFIG_UNRESOLVED`;
- target normalizer advanced through `hpvinv02.v3`;
- `acInputVoltage` normalized as `grid_voltage_v` so outage explanations require real grid-availability evidence rather than assuming zero import means outage;
- measured directional currents preserved independently as `battery_charge_current_a` and `battery_discharge_current_a`;
- latest-household operating-state classifier;
- Home view now has a plain-language “Qué está pasando ahora” explanation;
- Battery page is now a real local-data view rather than a placeholder;
- Battery page distinguishes:
  - current charge;
  - estimated stored energy;
  - estimated ordinary-use energy above the 20% normal grid-transfer reserve;
  - remaining 20%→10% emergency outage reserve;
  - 10% protected floor;
  - charging / supplying house / resting;
- Data & Updates page now shows a read-only inverter-configuration health summary;
- all new household-facing labels/tooltips have Spanish-first older-family wording with English alternatives.

The UI does not write inverter settings.

## Required Phase 4 next steps

### A. Read-only configuration health — IMPLEMENTED FOUNDATION

A versioned installation-policy evaluator uses a saved current-state SiSeLi snapshot. Historical telemetry is not treated as the current configuration.

Target expected state where evidence is available:
- SBU source priority;
- PYL/BMS battery protocol;
- OSO solar-only charge-source policy;
- BMS communication active;
- 10% absolute floor;
- 20% transfer-to-grid threshold;
- 50% return threshold;
- 50% restart threshold;
- LBU house-first solar priority;
- zero-export disabled/Grd;
- equalization disabled.

Output:
- CONFIG_CONFIRMED;
- CONFIG_DRIFT;
- CONFIG_UNRESOLVED.

Never write settings.

### B. Battery page semantics — IMPLEMENTED FOUNDATION

The first family-facing Battery page is now wired. Before marking it final:
- show charge percentage in plain language;
- show estimated stored energy;
- show estimated ordinary-use energy before normal grid transfer;
- show emergency 20→10% outage reserve separately;
- explain 10% protected floor;
- show charging / supplying house / resting in family language;
- keep voltage/current details secondary.

### C. Behavior classifier — LATEST-STATE FOUNDATION IMPLEMENTED

The latest-state classifier is implemented. Phase 5 must extend this over historical time ranges using actual timestamps:
- battery-normal operation;
- grid recovery 20→50%;
- outage emergency reserve;
- restart recovery;
- suspected grid charging with PV absent;
- repeated transfer oscillation;
- export-like anomaly;
- PV curtailment possibility under zero-export.

No single-sample fault declarations.

### D. Continue normalization validation

- compare normalized power balance against live energy-flow evidence;
- keep negative/export-like grid readings raw and unresolved until proven;
- validate battery-current direction over real charging/discharging periods;
- preserve rule version and source field for every normalized output.

## Phase 3 relationship

Phase 3 continues independently.

No raw historical data needs to be redownloaded merely because Phase 4 interpretation changes.

When normalization rules change:
- rebuild from local raw history;
- preserve old rule/run audit;
- do not mutate raw source observations.

## Exit criterion — updated

Phase 4 completes only when:

- target-device normalized metrics are generated from real raw history without household-policy-dependent measurement semantics;
- installation expected-state checks are read-only and evidence-backed;
- battery reserve semantics match the family manual;
- expected grid-recovery behavior is distinguishable from anomalies;
- zero-export assumptions are encoded as guardrails, not fabricated export metrics;
- uncertainty is explicit;
- raw values remain recoverable;
- normal household UI avoids technical jargon and remains understandable to older family members.

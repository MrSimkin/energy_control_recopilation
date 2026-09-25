# Phase 4 — Installation-Aware Normalization — Implementation Notes

Date started: 2026-09-25

Status: **IN PROGRESS**

## Purpose

Phase 4 converts the Phase 3 raw SiSeLi corpus into trustworthy household-energy metrics.

As of 2026-09-25 this phase is explicitly **installation-aware**.

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

## Required Phase 4 next steps

### A. Read-only configuration health

Build a versioned installation-policy evaluator from available SiSeLi state fields.

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

### B. Battery page semantics

Before marking the Battery page complete:
- show charge percentage in plain language;
- show estimated stored energy;
- show estimated ordinary-use energy before normal grid transfer;
- show emergency 20→10% outage reserve separately;
- explain 10% protected floor;
- show charging / supplying house / resting in family language;
- keep voltage/current details secondary.

### C. Behavior classifier

Using actual timestamps:
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

- target-device normalized metrics are generated from real raw history;
- installation expected-state checks are read-only and evidence-backed;
- battery reserve semantics match the family manual;
- expected grid-recovery behavior is distinguishable from anomalies;
- zero-export assumptions are encoded as guardrails, not fabricated export metrics;
- uncertainty is explicit;
- raw values remain recoverable;
- normal household UI avoids technical jargon and remains understandable to older family members.

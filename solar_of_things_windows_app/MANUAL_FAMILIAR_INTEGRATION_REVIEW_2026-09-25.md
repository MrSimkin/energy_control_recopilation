# Family Manual Integration Review — 2026-09-25

Status: **ROADMAP UPDATE REQUIRED / NO PHASE RENUMBERING REQUIRED**

Source manual:
`Manual_Familiar_SPRO_6200_LC230_Midea_v2_ES.docx`

Original SHA-256:
`f519a39be14950258ce51d3cbb3a7e69cbc6b23769b2ae9e47c77ca71f5a3bde`

Canonical extracted installation contract:
`INSTALLATION_BEHAVIOR_CONTRACT.md`

## Executive conclusion

The manual materially improves development and should change how Phase 4+ are implemented.

It does **not** require restarting Phase 2/3, discarding the raw corpus, or renumbering all later phases.

The correct change is to make Phase 4 installation-aware before expanding generic normalization and analytics.

## Material corrections / confirmations

### 1. Battery “available energy” must be split

The earlier generic idea “estimated usable battery energy remaining = SOC × capacity” is incomplete for this household.

The interface and analytics must distinguish:

- energy physically estimated to be stored;
- ordinary-use energy above the 20% grid-transfer reserve;
- emergency 20→10% outage reserve;
- protected floor near 10%.

This is a real product/roadmap correction.

### 2. Grid use at SOC 20–50 can be expected

After the house transfers to grid at ~20%, it can remain on grid while solar recovers the battery toward ~50%.

Analytics must not label this interval as abnormal grid dependence by itself.

### 3. Grid charging is a behavior to verify, not assume

P16=OSO is intended to prevent grid battery charging.

Therefore the app should detect whether SOC rises materially at night / with PV absent while grid is supplying the house.

A raw “AC charging switch” field by itself is not enough to conclude that grid charging is occurring.

### 4. Zero export is an invariant

The household is deliberately zero-export.

The app should not create an ordinary “grid export” accounting feature for this installation.

Export-like readings should be preserved as evidence and treated as unresolved/anomalous until proven.

### 5. House-first solar priority is now installation policy

P43=LBU means solar should feed the house before surplus charges the battery.

This strengthens the interpretation of the target device's energy-flow fields and should be used in Phase 4 plausibility/consistency checks.

### 6. Seasonal logic should be data-driven, not calendar-driven configuration changes

The manual explicitly warns against changing P40 automatically by season.

The app may analyze seasonal behavior, but it must not imply that inverter protection/transfer thresholds should change merely because the month changed.

### 7. The water heater matters as context

The 1.5 kW Midea heater is a known flexible load with documented seasonal schedules.

It is useful for later explanations/reports and load-timing analysis.

It does not justify adding household-device control to the current Solar of Things app.

## Roadmap delta

### Phase 4 — expand before generic metrics

Add an **Installation Behavior Contract** sub-layer:

- load the expected installation policy;
- compare live/read-only configuration evidence with the expected baseline;
- record CONFIG_CONFIRMED / CONFIG_DRIFT / CONFIG_UNRESOLVED states;
- never write inverter settings;
- normalize battery reserve zones;
- normalize grid flow with zero-export guardrails;
- use target-device energy-flow semantics;
- maintain confidence and evidence source for every interpretation.

### Phase 5 — aggregation/statistics

Add behavior-aware statistics:

- time on battery;
- time on grid;
- time recovering from 20% toward 50%;
- number of SBU↔grid transfers;
- emergency-reserve time below 20% during outages;
- count/duration of suspected grid-charging episodes;
- count/duration of export-like anomalies;
- time-series coverage for those classifications.

Do not derive these from fixed 5-minute assumptions; continue using actual timestamps.

### Phase 6 — dashboard / analysis UX

Add plain-language operational state:

- “La casa está usando principalmente el sol”;
- “La batería está ayudando a la casa”;
- “La casa está usando la red mientras la batería se recupera”;
- “Reserva de emergencia por corte”;
- “La batería está casi en su reserva mínima”.

Battery page should show distinct energy/reserve concepts rather than one ambiguous available-energy number.

### Phase 7 — reports

Family reports should explain:

- why grid use happened;
- whether it was expected at the documented SOC thresholds;
- whether large-load timing overlapped the best solar period;
- configuration-drift warnings in plain language.

### Phase 8+ — no structural change required

Utility reconciliation and tariff work remain valid.

Zero-export means tariff/export-credit logic must remain disabled/unavailable unless the installation's physical/legal state changes in the future.

## Development priority after this review

1. Finish integrating the installation contract into Phase 4.
2. Correct battery UI semantics before exposing the Battery page.
3. Add read-only configuration/baseline checks from existing SiSeLi fields.
4. Finish target-specific normalized power/SOC metrics.
5. Continue Phase 3 historical filling independently; normalization can rebuild locally without redownloading.
6. Then proceed to Phase 5 aggregation using installation-aware state classifications.

## No regression required

Raw Phase 3 history remains authoritative and unchanged.

This manual adds interpretation; it does not invalidate the raw ingestion architecture.

Because normalization is versioned and rebuildable, existing raw data can be reinterpreted locally without another full SiSeLi download.

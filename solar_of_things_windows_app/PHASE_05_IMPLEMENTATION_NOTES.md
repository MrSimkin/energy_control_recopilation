# Phase 5 — Aggregation / Statistics Engine — Implementation Notes

Date started: 2026-09-25

Status: **IN PROGRESS — CORE FOUNDATION IMPLEMENTED**

## Canonical calculation boundary

The user's household setup/manual does **not** redefine inverter telemetry or ordinary physical calculations.

Layers remain:

1. raw Solar of Things / SiSeLi evidence;
2. normalized physical metrics based on device/protocol evidence;
3. aggregation/statistics over those normalized metrics;
4. separate household-context interpretation.

Household context is allowed to affect:
- explanation of observed behavior;
- configuration-health comparison;
- battery reserve semantics in the family UI:
  - estimated stored energy;
  - ordinary-use energy above the configured normal reserve;
  - emergency outage reserve;
  - protected floor.

Household context must **not** rewrite:
- PV power;
- house/load power;
- grid power;
- timestamps;
- physical energy integration;
- SOC measurements;
- source raw values.

## Implemented core

### Time ranges

TimeRangeSelectionService supports:

- day;
- Monday–Sunday calendar week;
- rolling 7 days;
- calendar month;
- last N months rolling;
- last N complete calendar months;
- range of months;
- calendar year;
- rolling 12 months;
- year-to-date;
- arbitrary date range.

The normal UI also provides parent-friendly quick ranges ending at the latest locally stored date while leaving exact From/To dates visible and editable.

### Aggregation periods

Shared timezone-aware bucket planning supports:

- hour;
- day;
- week;
- month;
- year.

Power and SOC use the same bucket planner.

DST/timezone handling:
- station timezone is authoritative;
- local calendar bucket boundaries are converted to real elapsed UTC intervals;
- repeated/skipped clock hours remain real elapsed-time intervals;
- the collector/statistics engine never assumes every day is exactly 24 clock-hours.

### Power / energy

Supported normalized power metrics:

- PV power;
- house/load power;
- grid-import power;
- derived battery power.

For each selected range/bucket:

- sample count;
- minimum power;
- maximum power / peak;
- time-weighted average power;
- signed net energy;
- positive energy;
- negative energy;
- covered elapsed time;
- uncovered elapsed time;
- coverage percentage.

Energy integration:
- trapezoidal integration over actual timestamps;
- segments crossing zero are split correctly;
- accepted segments are split at aggregation bucket boundaries;
- no fixed five-minute multiplier;
- long gaps are not bridged.

### Battery SOC

For each bucket:

- sample count;
- minimum SOC;
- maximum SOC;
- time-weighted average SOC;
- latest/ending observed SOC;
- covered elapsed time;
- uncovered elapsed time;
- coverage percentage.

SOC is never converted into kWh except in the separate battery UI estimate that uses configured battery capacity.

### Contextual behavior duration

Separate versioned household-context samples support:

- mainly solar;
- battery supplying house;
- grid supplying house;
- grid recovery below the documented return threshold;
- outage battery operation;
- outage emergency reserve;
- protected-floor context;
- mixed/charging states.

Duration statistics use actual timestamp differences.

Long holes are reported as unknown/uncovered time rather than extending the previous state.

### History & charts screen

Current UI supports:

- exact From/To dates;
- quick date presets based on locally saved history;
- whole-period physical energy totals with coverage;
- contextual duration summaries;
- hour/day/week/month/year aggregation selector;
- auditable table containing:
  - solar kWh;
  - house kWh;
  - grid kWh;
  - battery supplied/received kWh;
  - SOC average/min/max/end;
  - minimum available coverage.

## Automated deterministic validation

CI smoke vector intentionally contains a 30-minute telemetry hole inside a five-minute-ish synthetic series.

Validated:

- calendar week resolution;
- complete-calendar-month resolution;
- power integration;
- observed-cadence continuity threshold;
- long-hole exclusion;
- coverage calculation;
- SOC min/max/average/end;
- aligned aggregation table.

The smoke vector is designed to fail if a later change silently bridges the intentional hole.

CI:
- run 221 (36199251583) — **SUCCESS**.

## Phase 6 bridge

The chart UI must consume the same EnergyAggregationTable rows as the auditable table.

No chart may:
- query raw telemetry through a separate calculation path;
- use a different aggregation rule;
- hide low coverage;
- fabricate missing intervals.

ScottPlot.WPF 5.1.59 was selected as the first interactive chart surface:
- MIT license;
- WPF-specific package;
- pan/zoom interaction;
- current Phase 6 work begins with solar / house / grid energy by aggregation bucket.

## Remaining Phase 5 work

Still required before formal Phase 5 closure:

- validate aggregation results against the real target corpus on the next combined target-PC test;
- decide/validate safe flow-attribution formulas before exposing:
  - PV contribution to load;
  - battery contribution percentage;
  - percentage supplied without grid;
  - solar self-consumption;
- add specific historical anomaly/episode statistics only where evidence supports them:
  - suspected grid charging with PV absent;
  - repeated grid/battery transfer cycles;
  - export-like anomalies;
- expose SQL/reporting views later under Phase 11 rather than duplicating them prematurely.

Do not invent contribution percentages by subtracting totals when flow attribution is not yet proven.

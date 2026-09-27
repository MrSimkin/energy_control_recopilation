# Phase 8 — Utility Meter Readings and Consumption Reconciliation — Implementation Notes

Date started: 2026-09-27

Status: **IN PROGRESS — FIRST FUNCTIONAL CHECKPOINT**

## Goal

Compare real utility-meter/bill evidence with inverter-derived **total grid import** over exactly the same interval.

Canonical semantic rule:
- `Utility / Enel → House` is a household-source attribution metric and is **not** the meter/bill comparison value;
- `total grid import` is the inverter-derived quantity used for utility-meter/bill reconciliation.

## First implementation checkpoint

Database:
- schema v9;
- `utility_meter_reading`:
  - exact UTC timestamp;
  - cumulative kWh;
  - optional reference;
  - optional notes;
- `utility_bill`:
  - exact start/end UTC;
  - optional billed kWh;
  - optional CLP amount;
  - optional invoice/reference;
  - optional notes.

Core:
- `UtilityMeterRepository`;
- `UtilityReconciliationService`;
- consecutive meter readings are converted into interval consumption by cumulative difference;
- inverter grid import uses `EnergyRangeStatisticsService` over the exact same timestamps;
- outputs:
  - signed inverter-minus-meter difference;
  - absolute difference;
  - percentage difference;
  - inverter coverage;
  - quality label;
- a decreasing cumulative meter value is treated as possible reset/replacement and is not silently converted into consumption;
- bill records can also be compared against inverter import over their exact recorded interval.

UI:
- the existing `Red eléctrica / Grid & Utility` navigation item now has a real page;
- cumulative-reading entry uses date + exact local `HH:mm`;
- ambiguous/invalid DST-local times are rejected rather than guessed;
- reading history;
- automatic reading-to-reading reconciliation table;
- latest reconciliation summary cards;
- optional utility-bill entry and comparison table;
- delete actions for corrections.

## Validation

A deterministic Windows smoke test now covers:
- schema v9;
- two cumulative meter readings;
- expected meter consumption;
- inverter total-grid-import integration over the exact same interval;
- near-zero reconciliation difference in a synthetic matched case;
- optional bill persistence and reconciliation.

## Target-PC acceptance still required

Phase 8 is **not complete** until real household evidence is entered.

Next target test should:
1. reuse the existing real portable `Data\`;
2. open `Red eléctrica`;
3. enter at least two real cumulative Enel meter readings with the real reading timestamps;
4. verify calculated meter consumption;
5. compare against Solar of Things total grid import and coverage;
6. optionally enter a historical bill with exact period timestamps;
7. report semantic/UX anomalies.

Do not fabricate a meter reading or force a comparison across mismatched time windows.

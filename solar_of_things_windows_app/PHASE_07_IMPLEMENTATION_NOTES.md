# Phase 7 — Reporting — Implementation Notes

Date started: 2026-09-26

Status: **IN PROGRESS — FIRST REPORTING CHECKPOINT**

## Scope

This phase implements Product Functional Specification Milestone F without creating a second calculation path.

Canonical rule:
- reports consume the same validated `EnergyRangeStatisticsService` and `EnergyAggregationTableService` used by History & Charts;
- missing data remains missing;
- 0%-coverage periods are not exported as measured zero;
- long telemetry gaps are not extrapolated.

## First checkpoint

Implemented:
- real Reports page in the desktop UI;
- quick report periods plus explicit From/To dates;
- day/week/month/year aggregation selection;
- named report presets persisted locally;
- relative presets preserve their relative definition when reopened;
- custom presets preserve explicit dates;
- Excel `.xlsx` export with:
  - Summary sheet;
  - full Detail sheet;
  - Quality sheet;
- printable PDF export with:
  - title and selected period;
  - physical energy summary;
  - per-metric coverage;
  - explicit missing-is-not-zero warning;
  - compact detail table;
  - page numbering;
- report exports use the same gap-aware statistics as the application UI.

Dependencies:
- ClosedXML 0.105.1;
- PDFsharp-MigraDoc 6.2.4.

Both are stable packages selected for the current .NET 10 codebase; no prerelease reporting dependency is used.

## Automated validation

The existing Windows smoke test now validates:
- report-preset persistence;
- generation of a non-empty XLSX;
- generation of a non-empty PDF on Windows.

## Remaining before Reporting can be declared complete

- add the specification's built-in named household report templates/variants beyond the generic configurable report;
- add simple charts to exported reports where they materially improve readability;
- validate one combined real-PC reporting session (preset save/reopen + Excel open + PDF open/printability);
- assess whether any additional glossary/context sections are needed for family-facing reports.

Do not ask the user to micro-test each exporter change. The next manual reporting test is one combined checkpoint after CI is green.


## Second reporting checkpoint — built-in family reports

Added after the first green export checkpoint:
- built-in selectable report types:
  - Simple Energy Summary;
  - Detailed Energy Report;
  - Battery Report;
- report type is persisted as part of named presets;
- Spanish/English export labels follow the active application language;
- printable energy chart using only buckets where solar/home/grid all have measurements;
- printable battery SOC chart using only measured SOC buckets;
- readable glossary in PDF and Excel;
- simple report explicitly marks unsupported flow-attribution percentage and utility comparison as unavailable instead of inventing them;
- detailed PDF includes the audit table;
- battery PDF focuses on SOC and battery delivered energy.

Grid/Utility Reconciliation and Financial/Bill reports remain intentionally deferred until their required source subsystems (utility meter observations and tariff/billing configuration) exist. They must not be fabricated from inverter data alone.

The next manual checkpoint remains one combined reporting validation, not separate tests for each report type.

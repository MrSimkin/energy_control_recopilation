# Solar of Things Windows App — Requirements Clarifications and Decisions 05

Date: 2026-09-24

Status: USER-APPROVED GAP-CLOSURE DECISIONS

This document closes the user-facing items from `FUNCTIONAL_SPEC_GAP_REVIEW_01.md`.

## 1. Rolling week

Confirmed:

- **Rolling week = exact trailing 7-day window** ending at the selected/current local timestamp.
- Calendar week remains a separate option defined as Monday through Sunday.

## 2. Current-only telemetry

Confirmed:

- Manual/explicit Update Data remains the operating model.
- Current-only Solar of Things fields that are not historized by the cloud may be stored as **sparse snapshots** captured when Update Data runs.
- The application must not pretend such fields have continuous historical coverage.
- No background Windows service is required solely to collect nonessential current-only telemetry.

## 3. Utility meter timestamp precision

Accepted model:

- utility readings may be stored with an exact timestamp when known;
- date-only readings are also supported;
- the database must record the precision of each observation;
- reconciliation must disclose boundary uncertainty when only a date is known rather than invent an exact time.

## 4. Date/time display

The application is for use in Chile by Spanish-speaking users, even though the UI language is English.

Confirmed display convention:

- date: `dd-MM-yyyy`
- time: 24-hour clock, `HH:mm`

Example:

`24-09-2026 22:35`

Internal storage remains timezone-safe and precision-preserving.

## 5. Installed data location

User delegates the exact implementation choice.

Design authority:

- choose the safest Windows 11 x64 approach that preserves easy database access, backup reliability and normal non-admin operation;
- the user does not require Program Files or co-location if a better technical solution exists;
- database/data path must be clearly documented and accessible.

The functional specification may therefore choose the implementation-default location.

## 6. Software updates

Confirmed:

- no silent automatic application updates;
- the application may check for an available new version;
- user explicitly chooses whether to update;
- database backup must occur before schema-changing upgrades/migrations;
- portable and installer variants must preserve data safely across upgrades.

## 7. Gap-review engineering defaults

The user delegates the remaining technical holes identified in `FUNCTIONAL_SPEC_GAP_REVIEW_01.md` to implementation/design judgment.

The canonical specification should adopt the recommended solutions unless later real-device evidence requires refinement, including:

- timezone/DST-safe aggregation;
- partial-bucket labeling;
- trustworthy power-to-energy integration with gap handling;
- measured/derived confidence states;
- separate House Load / Grid → House / Grid → Battery / Total Grid Import;
- raw preservation of unexpected signed values;
- estimated battery energy from SOC × configured usable capacity;
- no double-counting in savings;
- unexplained bill residual preserved explicitly;
- SQLite WAL/read-only external-query guidance;
- versioned reprocessing from raw data;
- late-arrival overlap/upsert behavior;
- backup/restore defaults;
- sanitized diagnostics;
- accessibility baseline;
- automated correctness tests;
- broad but relevant telemetry collection.


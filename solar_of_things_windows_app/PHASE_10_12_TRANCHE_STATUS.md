# Consolidated tranche 10–12 — execution and acceptance record

Status: **WORK IN PROGRESS — NOT ACCEPTED, NOT READY FOR MERGE**.
Owner authorization: combine Phase 10 correction + Phase 11 SQL + Phase 12
backup/protection plus UI/UX and Enel zero-click improvements, but defer
**all restore / destructive import / live DB replacement**.

## Phase 10 known target-PC QA (Build 652 FAIL)

Observed user evidence:
- Electricity and Transport reconstructed.
- Administration printed CLP 727 preserved only; supported fixed amount 0.
- In-app observed estimate CLP 24,690 vs Audit PDF/annex CLP 24,693.
- Printed summary OK; real detail residual -3 CLP must remain visible.
- One line verified reported despite two reconstructed components.
- Enel automatic PDF zero-click remains open; one-click Download fallback PASS.

Actions in this draft:
- decouple global `FIXED_MONTHLY` publication candidates from RED/ETR
  `CandidateIndex`. Require uniquely reconciled whole-peso value across
  all applicable effective publication periods; remain conservative on ambiguity.
- add anonymous smoke regression shifting global fixed indices to differ
  from electricity indices.
- align the product monetary comparison with the Audit/export reconstructed
  billed baseline, preserving the real rounding residual, not inventing a
  synthetic printed adjustment.

Build 682 owner QA FAILED on the actual bill: administration remains
actual-only. The owner's sanitized debug ZIP proves 136 FIXED_MONTHLY
candidate rows in *each* selected official publication. The alternative
gross monthly rates are 512, 727, 898 CLP (Aug) and 512, 727, 899 CLP
(Sep); the older resolver rejected every period because it required all
alternatives to have the same amount.

Correction in this draft: match the **gross IVA-included** amount of 727
only if both publication periods independently contain that official
whole-peso value, while explicitly marking RED/ETR applicability as
AMBIGUOUS. Do not call printed-amount matching independent verification.
Do not mark ambiguous lines as verified, and do not invent any fixed-charge
proration. Add an anonymous competing-rate regression fixture.

Actual Windows QA after this correction is still PENDING; the build must
not be labeled Phase 10 PASS until the user verifies the new Audit evidence.

## Phase 11 scope

Schema v17 introduces five queryable read-only views with documented
granularity and units. Guide:
`PHASE_11_SQL_GUIDE.md`.

DO NOT invent daily kWh from incomplete power samples. Proposed daily-energy
and hourly-energy reporting views remain pending trusted parity with the
PowerAggregationService's real-time integration and coverage logic.
Seven queryable read-only reporting views now include UTC daily/hourly
arithmetic power-sample statistics (explicitly not kWh), while the
remaining true energy integration and modeled bill SQL parity are pending.
This phase remains **partial** until source-equivalent reporting semantics
and target-PC usefulness are validated.

## Phase 12 scope — explicitly excluding restoration

Supported:
- manually initiated consistent SQLite native backups, including WAL pages;
- daily-on-launch backup if no recent automatic snapshot;
- independent snapshot `PRAGMA integrity_check`;
- schema version guard and SHA-256;
- manifest sidecar; atomic move from `.inprogress` to final snapshot;
- read-only diagnostics exporting backup metadata only;
- same shared data location `D:\\SolarEnergyMonitorTest\\Data`.

Excluded by owner:
- Restore, Replace, Import into production DB and destructive migration.
- Automatic deletion of backups (retention cap needs a separate approval).

Backups run before the application applies new schema migrations. If an
automatic backup cannot be verified, startup stops rather than risking
an unprotected migration.

`DeveloperDiagnosticsWindow` now offers manual verified backup and sanitized
phase 10–12 evidence ZIP. The ZIP deliberately excludes the active database,
backup data, passwords, tokens, PDF bill contents and raw API payloads.

Evidence coverage limitations: the generic ZIP reports NOT_RUN for real
WebView2 zero-click and real bill proof; those features need the existing bill
Audit PDF + technical annex and targeted owner-PC validation.

## UI/UX and Enel status

Implemented in work branch, pending target-PC verification:
- persisted Settings > default user-export folder and initial directory in PDF,
  Excel, audit annex, reconciliation and Help Save dialogs;
- local staged progress and completion ping/path for Audit PDF/ZIP, Reports,
  and phase/debug/backup operations;
- independent reconstructed vs verified/ambiguous audit line counts;
- WebView2 official-PDF response evidence, overlapping HTTP 206 range
  reconstruction within a 64 MiB cap, and sanitized zero-click/fallback
  event log in the debug evidence ZIP;
- bill-specific structured reconciliation evidence selected from Audit;
- SQLite schema columns and foreign-key dictionaries in the debug ZIP.

Zero-click success on the real official site is NOT YET PROVEN and remains
non-blocking; the accepted one-click Download fallback must not regress.
Broader responsive visual polish remains subject to focused Windows QA.

## QA/CI and release gate

- All internal CI build and smoke steps must succeed.
- Work branch and draft PR only; never merge to `main` on partial results.
- When a coherent tranche is truly ready, provide the owner a direct
  `SolarEnergyMonitor-Build-XXX-win-x64.zip` download before test instructions.
- Preserve shared Data without overwriting the real DB; request one focused
  Windows QA with separate acceptance statuses by phase.
- No one may mark Phase 10, 11 or 12 fully closed from this document alone.

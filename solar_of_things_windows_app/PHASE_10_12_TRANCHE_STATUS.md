# Consolidated tranche 10–12 — execution and acceptance record

Status: **PHASE 10 OWNER FUNCTIONAL QA PASS (BUILD 684); PHASE 11/12 PARTIAL — NOT READY FOR MERGE**.
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

At the time of this correction the next Windows QA was pending. The subsequent Build 684 owner evidence and functional acceptance are recorded below.

## Build 684 — owner-accepted functional QA (2026-10-07, Chile)

Owner supplied a target-PC Audit screenshot, the nine-page Audit PDF,
the technical annex ZIP and a sanitized phases 10–12 diagnostic ZIP,
then explicitly authorized functional Phase 10 acceptance and
continuation. **Phase 10 functional QA: PASS.** This is not a
claim of independently established RED/ETR tariff applicability.

Accepted bill evidence (28-Aug to 28-Sep 2026):
- 97.000 billed kWh; 88.065413 observed inverter kWh;
  coverage 99.3349%.
- Actual printed total CLP 26,854; observed comparable scenario
  CLP 24,693.31; difference CLP 2,160.69 (display CLP 2,161).
- Official-source mathematical reconstruction: Electricity
  CLP 21,387.53 vs printed 21,389; Transport + Public Service
  CLP 2,070.368 vs printed 2,072; monthly fixed Administration
  CLP 727 vs printed 727.
- Three reconstructed lines, of which **one is independently
  marked verified and two retain explicit applicability ambiguity**.
  Matching printed charges does not establish unique RED/ETR.
- Printed summary balances; real line-detail residual CLP -3
  remains disclosed with no fabricated adjustment.
- Reconstructed line coverage 70.369...% = CLP 24,188 of reconstructed
  **actual absolute line amounts** / CLP 34,373 of **all actual
  absolute line amounts**, including the subsidy absolute value.
  This is *not* a share of net bill total.
- Audit PDF and annex match the visible estimate and reconstruction.
  The economic CSV retains supported variable and fixed amounts
  separately.

Build 684: successful Windows CI run 37713227484 (branch commit
`c4ade0094dcbbe788a5f6a72547ecd32d7212a0c`).
The PDF identifies the CI pull-request synthetic merge revision as
`37c7166d...`; that revision differs from the PR branch tip by design.

Non-blocking presentation refinements included in the next
work-branch code change: rename PDF scenario column "Variable" to
"Subtotal modelado" (it includes fixed charge), align explanatory text,
and show the reconstruction-coverage denominator on screen in both
languages. CI and any focused checks for that follow-up code are
separate from the owner acceptance of Build 684.

Phase 10 economic QA has an accepted baseline; do **not** reopen
earlier manual QA or assert tariff identity unique. Global consolidated
10–12 tranche is still WIP, and merge to `main` is not authorized.

## Phase 11 scope

Schema v17 introduces seven queryable read-only views with documented
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

## Build 685 — post-acceptance presentation correction, CI PASS

Code commit: `b31d97d94f0cba6a18f2ea7e4d0a225470bfac95`.
Windows Actions: run `37720787982`, Build **685**, completed **SUCCESS**:
restore, Release build, SQLite smoke, win-x64 portable publish,
portable marker and artifact upload all PASS. The live Enel/CNE probes
were conditionally skipped (not a live-validation PASS).

Artifact: `SolarEnergyMonitor-Build-685-win-x64.zip`, GitHub artifact ID
`11525996688`, SHA-256
`67f10d350f9c342bc6d759563ef30861ef48311111a146384a02437be7e81ae9`.
Downloaded ZIP checksum matched GitHub; ZIP integrity PASS, 490 entries,
`SolarEnergyMonitor.exe` and `portable.mode` present, no bundled `energy.db`.

This code change only corrects the PDF financial-scenario subtotal label
and explanation and adds a bilingual visible/tooltip denominator hint
to the on-screen reconstruction-coverage percentage. It does not alter
the tariff engine, numeric calculations, schema, backups or real data.

**Release gate:** owner acceptance of Build 684 Phase 10 *functionality*
remains valid. The Build 685 label/layout refinement is CI-verified but
its Windows visual output has not yet been checked by the owner. Phases
11 and 12 remain PARTIAL and are not declared closed. No merge to `main`.

## Next tranche — owner design and development decisions (2026-10-08)

The owner has approved the direction of ongoing UI/UX evolution, performance/refactoring, an integrated **and** externally accessible SQL workflow, and further backup-development work. The owner also postponed Build-685 manual visual QA and is not ready for v1.0; additional Enel-report data are expected on 2026-10-12. **No next-tranche code, schema, backup restore, destructive operation, version bump, merge or new build is authorized merely by this record.**

See [`NEXT_TRANCHE_DECISIONS_2026-10-08.md`](NEXT_TRANCHE_DECISIONS_2026-10-08.md) for confirmed decisions, remaining layperson backup/UI design discussions, and the proposed consolidated QA cadence. Phase 10 Build-684 functional acceptance remains intact; Phases 11 and 12 remain partial.

## v0.11.0 work begun on 2026-10-08 (not yet compiled/QA)

After the owner's explicit choice **B**, the additional Enel evidence expected 2026-10-12 is deferred from the current independent implementation work; return to remaining Enel and closure decisions when the owner provides the source material. The owner authorized starting the approved non-destructive v0.11.0 implementation now while keeping the current PR draft and `main` untouched.

Initial actual code change: bilingual five-group scrollable sidebar navigation and clearer navigation labels in `MainWindow.xaml` + ES/EN string dictionaries; original ten navigation buttons, route identifiers and handlers remain intact. Changes were committed `[skip ci]`, with re-fetched static structure checks only: **no Windows compile, no new downloadable Build, no owner visual QA**. See `NEXT_TRANCHE_DECISIONS_2026-10-08.md` for approved requirements and tracked open work. Existing Phase 11/12 implementation remains partial and cannot be called complete.

Crucial safety gate: existing SQLite-only daily/pre-migration backup code has **not yet been removed**, because doing so before a verified complete-recovery replacement would reduce protection. Never describe full-backup/restore implementation as complete at this point.

## Urgent complete-backup implementation started (2026-10-08; CI pending)

Owner explicitly reports **no known actual recoverable backups** on the target PC and requests **immediate priority for the weekly complete-backup workflow**, ahead of SQL/UX. This is not evidence that any copy exists on the owner workstation.

Implemented on draft work branch after the owner's authorization:
- New `FullBackupService`: native SQLite WAL-consistent temporary snapshot, schema/integrity check, `Bills` and `Tariffs` source files, SHA-256 manifest including producing app/build/schema/format, ZIP64-capable archive, post-creation full content verification, atomic `.inprogress` publication. Explicitly excludes `Secrets`, logs and existing backup directory. **No real restore implemented**.
- New user-facing **weekly optional** reminder on app startup for missing/stale complete package: create, postpone 24h or skip one week, persisted across restarts; manual complete-backup action now available through Settings, plus secondary-folder selection. No daily automatic copy should run from `App.OnStartup`.
- If an actual schema upgrade is required, user can postpone; otherwise a **verified complete** package is required before migration. Preexisting data are not programmatically accessed from the development environment.
- Developer Diagnostics manual backup action now calls full package service instead of an SQLite-only service. Existing legacy technical `DatabaseBackupService` still exists for historical/testing compatibility; it is no longer a routine startup task.
- Optional secondary destination copies only completed, fully verified full packages; SHA checked again. Status shows secondary copy failure separately from local success; no assumption that a failed mirror succeeded.
- Synthetic smoke case exercises WAL data in ZIP, source document inventory, checksum verification, copy to secondary, recognition of corruption and blocking deletion of the last available complete local copy.

**Key limitations/status:**
- New code was committed and Windows CI triggered; **do not claim CI PASS, user-PC backup creation, installation, on-disk confirmation or verified actual recovery until evidence**.
- Full backup is a safely formed *recoverable package*, but **selective restore/import of historical versions is NOT implemented**; safe restoration testing remains future work.
- The full approved inventory and individually-deletable local/secondary list **is not yet fully wired into end-user UI**; no auto cleanup is permitted.
- Existing program still displays `v0.10.0` until consolidation; no branch merge. Full-backup secrets/raw SQLite and original bill PDFs are sensitive and must not be shared publicly.
- Do not rerun pre-migration import/recovery against the owner's real `D:\\SolarEnergyMonitorTest\\Data\\energy.db` or real backup files without later explicit and safe owner action.

## Build 688 — urgent weekly complete backup code CI PASS (2026-10-08)

**Build source commit:** `eae20e1fe9d29210fa685a6b18586fb7b46c9287`; GitHub Actions run `37824066560`, number **688**, result **SUCCESS**. Compilation and SQLite smoke tests PASS; opt-in live Enel/CNE probes skipped (not verified). Portable win-x64 publish and artifact upload PASS.

**Artifact:** `SolarEnergyMonitor-Build-688-win-x64.zip`, artifact id `11570981549`, GitHub artifact name `SolarEnergyMonitor-win-x64-dev`, downloaded SHA-256 `d1839f526d853c66d979c8e09dc09fbf3b648b9a552435a63b08db4e85e21a7d`; ZIP test PASS, 490 entries, executable and portable marker present, **no bundled energy.db**. Direct ZIP provided in chat for optional owner-PC verification. No deployed application/install inferred.

**Functional code included:** verified complete ZIP creation (native WAL-consistent SQLite snapshot, tariffication and bill source directories, SHA manifest and full archive recheck), backup-before-migration gate, voluntary weekly reminder (create now / postpone 24h / skip week), user-triggered Settings and Developer Diagnostics full ZIP, optional second destination and reverified second copy, local recognized package count, no auto daily backup creation. Build 688 product display remains `v0.10.0` (v0.11.0 is later target).

**Smoke testing:** synthetic SQLite/database + synthetic original bill/tariff documents, ZIP provenance and hash integrity, WAL committed-record recovered from packaged SQLite, secondary copy comparison, rejection of invalid archive and protection against deleting last only full package. **No owner-PC backup created or verified, no real database read or write by developer**, and no real Windows GUI visual QA yet.

**Still pending in broader v0.11.0 scope:** first real user-initiated full backup and optionally secondary verified copy, complete user-facing inventory/per-copy deletion UI, version-aware selective additive restore implementation and synthetic safety tests, more SQL/UX/performance tasks, additional Enel evidence on Oct 12. This is an early urgent safety build, not final v0.11.0 acceptance nor permission to merge draft PR to main.

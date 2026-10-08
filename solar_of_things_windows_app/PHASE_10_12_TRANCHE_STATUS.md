# Consolidated tranche 10–12 — execution and acceptance record

> **Current consolidated operative baseline (owner-confirmed, 2026-10-08):** [**V011_BASELINE_CONSOLIDADO_2026-10-08.md**](V011_BASELINE_CONSOLIDADO_2026-10-08.md). Use it for the current v0.11.0 implementation scope, the owner-supplied provisional Data copy, Build 688's unaccepted QA status, and the conditional 2026-10-12 reassessment of Enel continuity. Older dated paragraphs below remain factual history and may reflect previously pending decisions; do not infer that today's code is released or owner-accepted.

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

## Owner QA scheduling correction — Build 688 not requested for immediate test (2026-10-08)

The owner confirms a separate **manual folder copy of `Data`** is already held as a provisional safeguard. The owner explicitly redirects the first real full-backup exercise, weekly reminder and second-location verification to the **NEXT consolidated Build** rather than conducting an immediate standalone QA with Build 688. Build 688's Windows CI success is code/test evidence **only** and does not constitute owner acceptance or evidence of a verified recovery point on the target machine. The source/SQLite WAL consistency of the manual copy is **unknown**, so preserve it without deleting or renaming as "verified". Continue approved implementation on draft PR; no extra owner-facing emergency ZIP needed and no merge to `main`.

## v0.11.0 implementation slice 1: backup inventory UI (2026-10-08; NOT CI-VALIDATED)

Owner confirmed consolidated functional requirements and asked to start actual development. Continued on the existing work branch after the owner-confirmed baseline `V011_BASELINE_CONSOLIDADO_2026-10-08.md`, without altering `main` or touching the real QA data.

**New code:**
- `src/SolarOfThings.Core/Backup/CompleteBackupInventoryService.cs`: physical local/secondary copy inventory; legacy `.sqlite` classified visibly as legacy-only; fresh integrity+hash recheck of selected complete ZIP; manual one-copy deletion with normalized/configured parent-path check, reparse-point safeguards, and re-verification that at least one **OTHER available** full package exists (not an offline remembered mirror). No automatic pruning; local and secondary delete targets remain independent.
- `src/SolarOfThings.App/MainWindow.xaml`: a real first-class **Protección de datos / Data protection** page and sidebar navigation, a scrollable physical-copy inventory, create/secondary settings/open-local actions and explicit **Refresh / Verify / Delete selected** controls, integrity and offline destination status messages.
- `MainWindow.xaml.cs` + `App.xaml.cs`: page visibility and navigation, asynchronous inventory/verify/delete operations, confirmation before deletion, service registration and links to the previously implemented full-backup creator/settings. Integrity verification and deletion checks run off the UI thread.
- `Strings.es.xaml` / `Strings.en.xaml`: bilingual page texts and safety labels.
- `tools/SolarOfThings.SmokeTest/Program.cs`: isolated synthetic fixture checks for full/legacy classification, secondary verification, local-vs-secondary independent deletion, last-copy protection, offline secondary warning and rejection of unrelated-file paths.

**Check performed now:** Re-fetched source from GitHub and verified all 20 referenced page/column localization keys in BOTH languages, 6 new XAML-to-code click handlers, named page/grid/buttons, navigation route, DI registration and synthetic-test case wiring (static structural check PASS).

**DO NOT claim:** compiled, Windows CI PASS for these new commits, end-user visual QA PASS, actual backed-up owner data, safe real restore, final inventory acceptance or released v0.11.0. Build 688 remains previous CI evidence only and requires no separate owner test. These new commits use `[skip ci]`; a single combined CI build and synthetic regression set are reserved for a genuinely consolidated release candidate.

**Next development:** harden full-package referential consistency and failure handling, test the complete inventory/delete feature in actual Windows CI on synthetic files, implement the approved version-aware non-destructive selective-recovery preview/engine and SQL explorer. The decision about continuity after actual new Enel evidence arrives on 2026-10-12 remains open; do not presume that input.

## v0.11.0 implementation slice 2 — verified backup semantics + isolated preview (2026-10-08; Windows CI validation underway)

**After owner request to continue approved v0.11.0 development**, the following work has been committed exclusively to the draft work PR (main remains unchanged):

1. `FullBackupService.cs`: creation now verifies that bill/tariff original file paths referenced by the staged SQLite snapshot are truly packaged under the correct managed directories, with SHA-256 parity when original hash exists; unavailable/mismatched originals fail the complete-package creation rather than reporting a false PASS. Independent ZIP re-verification now checks entry names and counts against the manifest, entry sizes, SHA-256 bytes, and an extracted disposable SQLite snapshot's **PRAGMA integrity_check**, **PRAGMA foreign_key_check**, and actual schema vs manifest. The disposable verification database is removed, source ZIP/active DB are never replaced.
2. `IsolatedRecoveryPreviewService.cs`: isolated synthetic-fixture-only **read-only DRY RUN** comparing selected stable-identity records: settings, meter readings, tariff publication identities, and original bill documents. Returns **missing / identical / conflicting** counts, with bounded row counts. Linked bills/charges and massive energy telemetry explicitly remain UNSUPPORTED pending relational adapters. Rejects versions without a reviewed schema adapter, prevents scanning the owner's live QA DB by requiring a synthetic target path under OS temp. It **does not import, restore, replace or write target records**.
3. `tools/SolarOfThings.SmokeTest/Program.cs`: new artificial-fixture checks for preview counts and source-vs-target difference while proving target values unchanged, unsupported target schema fail-closed, and missing registered PDF fail-closed; uses the existing independent physical-copy deletion safety tests.

**Known limits:** No automatic active restoration/import exists. Preview only supports same-schema v17. New archive verification requires temporary disk space for SQLite extraction, potentially several GB with owner's ~2 GB DB, and may be long-running; the UI must retain async/progress behavior and communicate failures accurately. No real owner backup has yet been created or verified; the user's manual `Data` copy remains provisional and untouched.

**CI history of this slice:** run #689 initially FAILed to compile due to a test-local variable name collision (the core new backup/recovery services compiled). That test-only collision was corrected in subsequent commit `645353f43c605f1b1d80ea8fd43b4569279b3b5a`. The corrected run #690 is being checked separately. **Do not claim success until its actual Windows job and smoke tests finish.** No new owner-facing Build is requested or delivered; the next user QA remains one consolidated v0.11.0 handoff after the 2026-10-12 continuity discussion where applicable.

**Source status:** Draft PR #1, with `main` base SHA `59120a630b0f56684ba7672d960673f5c5c1797b`; documentation-only checkpoint.

## Build 690 — full ZIP/reference integrity and synthetic recovery preview CI PASS (2026-10-08)

**Windows GitHub Actions run `37828322217`, build number `690`: completed `SUCCESS`.** Source code/test commit `645353f43c605f1b1d80ea8fd43b4569279b3b5a`; Windows Release build, SQLite smoke (including newly added isolated preview and missing-source-reference rejection), win-x64 portable publish/marker and upload all PASS. Conditional live Enel/CNE probes were **SKIPPED**, not certified. Build 689 failed to compile due only to test variable collision and was superseded by 690.

**No owner QA/handoff for Build 690.** The package is a CI artifact, not the next mutually agreed user-testing delivery, and the visible executable version remains `v0.10.0`. The v0.11.0 consolidated implementation continues, PR remains Draft, no merge, no access to live owner `Data` or provisional folder copy.

**Implementation gaps remain:** full backup package/category/identity metadata and cross-version adapters, safe additive importer/rollback on isolated synthetic databases, relationally coherent imports of bills/lines, large telemetry and date-quality rules, UI/UX for recovery preview, verified deletion interface target-PC QA, integrated SQL console/editor and reporting semantics, further WPF/performance work. Real active data import/restore still requires separate explicit future authorization.

**Do not reclassify synthetic tests as owner's physical backup/recovery proof.** Current user-owned `Data` folder duplicate is a provisional unverified safeguard to be retained.

## Build 691 — isolated additive recovery staging CI PASS (2026-10-08)

**Owner asked to continue the approved v0.11.0 recovery track.** The resulting isolated work was completed under draft PR #1, without merging `main` or reading/writing the owner's real `Data`.

**Implemented source:**
- `src/SolarOfThings.Core/Backup/IsolatedRecoveryAdditiveTestService.cs`: strictly synthetic-test-only, schema-v17, **staged-copy output** adapter for stable-key `app_setting` records. Reads verified full ZIP and synthetic target with SQLite read-only connections; creates a NEW SQLite-native consistent copy of the target and inserts missing settings only using a transaction. Reports already-present identical values and unresolved conflicts without UPDATE/REPLACE of existing keys; rollback/failure disposes unfinished stage. A uniquely generated OS-temp `SolarEnergyMonitorSmoke/<guid>` fixture plus explicit synthetic marker is required. No shipping app/UI integration or method to activate the stage.
- `IsolatedRecoveryPreviewService.cs`: corrects a risky earlier assumption. **Meter readings cannot be treated as uniquely identified by timestamp** in schema v13+, which allows multiple readings at the same instant. Therefore `METER_READINGS` is explicitly UNSUPPORTED rather than claiming inaccurate duplicate/conflict counts until provenance-based identity and bill-linked FK remapping are defined and tested.
- `tools/SolarOfThings.SmokeTest/Program.cs`: synthetic end-to-end asserts **missing inserted into a new stage / identical skipped / conflict untouched / idempotent re-run adds zero / injected pre-commit failure rolls back and cleans stage / arbitrary non-fixture path rejected / original synthetic target unchanged**. Real meter-reading, bill/line, tariff-document and telemetry import remain deliberately blocked.

**Validation:** GitHub Actions Windows **run `37829312825`, Build `691`, source commit `f6b0e974ecf6567231959995064adb695dfb0c04` finished `SUCCESS`**. .NET Release build, SQLite smoke test, portable win-x64 publish and upload PASS. Opt-in live Enel/CNE probes SKIPPED, not a source-site PASS. No new user-QA build is handed off.

**Explicit non-claims:** This is **not** a released or operational full recovery feature; no active DB import, no user-verified backup, no schema 13–16 adapters, no real-document restoration, no relationally coherent meter/bill merge, and no authorization to modify the owner's active SQLite. The owner's provisional copy of `Data` must stay unmodified. The final target remains **v0.11.0** after all independent work and the owner-controlled continuity discussion upon receipt of new Enel data on 2026-10-12.

**Next technical work:** model true stable identities, multi-table FK dependencies, archive-relative source documents, versioned adapters and explicit staged dry-run conflict reports before even considering a user-facing selective recovery action. Read-only integrated SQL explorer and UX/performance are independent approved tracks. No repeated design questions are necessary unless a safety/semantics contradiction is encountered.

## Build 692 — synthetic bill/tariff relationship audit CI PASS (2026-10-08)

**Owner requested next recovery step; changes are code/fixture-only on draft PR #1.** Source commit `8c257d27efeb2e817b416428180470b26f532069`, Windows Actions run `37830387619`, Build **692**, completed **SUCCESS**: Restore, Release Build, SQLite smoke, portable win-x64 publish and upload PASS; live Enel/CNE probes SKIPPED. No owner-facing ZIP sent or requested.

**New technical component:** `IsolatedRecoveryRelationAuditService.cs`: verifies a COMPLETE v17 package using `FullBackupService.VerifyArchive`, extracts a read-only disposable source DB inside a strictly guarded synthetic smoke fixture, opens the synthetic target read-only, audits bill->source document SHA, optional linked meter-reading foreign keys, charge lines and field evidence, and audits tariff publication->PDF SHA, publication page text, candidate rates and inter-publication relations. Each source bill/tariff produces an explicit category state and counts. A matching original-document SHA is **NOT** treated as evidence that all associated bill amounts, lines or readings are identical; local integer IDs are **NOT** presumed portable. Missing/orphan links, ambiguous PDF identity, target source overlap and dependent tariff graphs are clearly flagged for manual/reviewed adapters.

**Identity fix preserved:** SQLite migration 13 removed uniqueness by timestamp for utility_meter_reading; duplicate-looking timestamps may refer to separate readings. No automatic identity merge or imported linked bill/tariff row is permitted. The prior `IsolatedRecoveryAdditiveTestService` was extended only to expose its strict synthetic fixture gate to this second audit; it still imports nothing into live data.

**Synthetic smoke evidence:** two freshly initialized v17 fixture DBs, bill document + matching actual SHA file in Bills directory, bill linked to one reading, charge line + field evidence, tariff PDF + matching actual SHA file, publication with extracted page text and rate candidate. ZIP is produced and independently verified. Assertions show billed source graph state `READING_REMAP_REQUIRED`, preserved line/evidence counts, tariff dependency state `DEPENDENT_TARIFF_GRAPH_REMAP_REQUIRED`, overlapping source URL requiring review, zero target bills after audit, and rejection of arbitrary non-fixture paths.

**Explicitly NOT implemented:** version migration adapters beyond v17, identity-safe cross-database bill/meter graph merge, original PDF path remapping/materialization, live/owner-PC backup or recovery, restore UI, approval to replace/modify the owner's `D:\SolarEnergyMonitorTest\Data\energy.db`, final consolidated v0.11.0 owner QA/merge. The manually copied `Data` directory remains provisional and must not be changed.

**Next independent implementation workstream:** read-only integrated SQL explorer (query authorization, bounded execution, real XLSX/CSV, editor UX) and measured responsiveness. Recovery remains PARTIAL; additional relational adaptation requires exact identity / FK mapping before code can write even a synthetic bill group.

**Continuity gate:** new Enel evidence expected 2026-10-12; re-study project continuity only once evidence exists, preserving accepted Build 684 Phase-10 facts. Build 692 CI success is NOT full release/owner QA.

## Build 694 — integrated safe SQLite SQL Explorer UI/CSV/XLSX CI PASS (2026-10-08)

**Owner expressly requested proceeding to the next approved SQL Explorer workstream.** Source/test commit `a08e9593aa980d69db07f4c371f54fde420437cc`, Windows Actions run `37832208849`, build number **694**, completed **SUCCESS**. Windows WPF compile, synthetic SQLite smoke, portable win-x64 publish and artifact upload PASS; opt-in live Enel/CNE probes SKIPPED, not passed. **No owner-facing standalone QA release; next handoff remains one consolidated v0.11.0 package after outstanding work and continuity review.**

**Core SQL engine added:** `src/SolarOfThings.Core/SqlExplorer/SafeSqlExplorerService.cs`. No write APIs. Opens existing database physically read-only with no creation and `PRAGMA query_only=ON`; installs native `sqlite3_set_authorizer` callback to reject DML, DDL, ATTACH, PRAGMA and unapproved functions, plus a separate lexical one-statement SELECT/readonly-WITH gate. Read-only query session has native SQLite VM progress callback with token cancellation and deadline; it is run off the WPF UI thread. Preview is capped at 200 rows by default, maximum 500, with an explicit `HasMore` flag, type/NULL-aware `SqlCell` values and no false kWh calculations. `SchemaAsync` shows actual table/view names but does not load rows from each table.

**Exports:** `ExportAsync` reruns the ORIGINAL query, not the truncated preview. CSV UTF-8 with BOM, quoting, formula-injection defense, explicit NULL sentinel `\\N`, up to 1,000,000 rows; native ClosedXML XLSX with typed finite numeric cells and text safety, max 100,000 rows (bounded to limit memory pressure). A safety limit breach or cancellation aborts without publishing an incomplete export; existing filenames are never overwritten.

**Integrated WPF page:** `MainWindow.xaml` sidebar entry **Explorador SQL** under Datos y herramientas, schema object list, multiline SQL textbox with basic caret line/column, run/cancel, bounded virtualized result grid, export full CSV/XLSX and schema refresh. `MainWindow.xaml.cs` uses async operations, supports Ctrl+Enter/F5 to execute and Esc to request cancellation, navigates via an existing page-content pattern and respects ES/EN language. `Strings.es.xaml` and `Strings.en.xaml` cover page labels and guardrails. Static source check PASS for declared WPF names, navigation and locale keys. Build 694 Windows WPF compilation PASS; **visual owner-PC interaction still unverified**.

**Synthetic SQLite smoke exercised:** schema reporting view list, preview truncation at 200 vs full export 217 rows, actual CSV/XLSX row counts, malicious SQL rejection (including multiple statements and an unapproved native function), harmless quoted keywords, read-only CTE, cancellation requested before query start, database non-creation when nonexistent, CSV spreadsheet-formula and NULL handling, Excel worksheet content, and no modification to the source DB. No real user's database was opened for these tests.

**Remaining for completed SQL Explorer UX:** syntax color highlighting, editor gutter line numbers and trustworthy SQLite error-location guidance, custom shortcut mappings/duplicates, completion/find/comment toggles, further result usability/performance work, and export large-data profiling. The present native SQL functionality is the *first implementation slice*, not all accepted Phase-11 features. Preserve correct distinction between sample W statistics and integrated kWh.

**Repository/release:** PR #1 remains Draft, `main` unchanged from the recorded base at this checkpoint, product executable version still v0.10.0 until final coordinated v0.11.0 version bump. No active restore/import or owner-PC backup was performed. Owner's provisional manual `Data` copy remains untouched. On actual Enel inputs expected 2026-10-12, re-study continuity as directed; do not assume receipt or closure.

## Builds 695–696 — SQL million-row streaming XLSX and syntax-highlighted editor CI PASS (2026-10-08)

**Owner correction of requested Excel capacity:** a modern Excel worksheet supports **1,048,576 physical rows**. The earlier arbitrary `MaxExcelRows=100_000` was an implementation-memory guard, not an Excel product limitation. **Approved target: 1,000,000 data rows + 1 header row**. The owner explicitly requested continued independent development of the professional SQL editor.

**Implemented core export:**
- `src/SolarOfThings.Core/SqlExplorer/StreamingSqlXlsxWriter.cs`: new low-memory, forward-only XLSX/OOXML ZIP+XmlWriter output with inline text cells; no ClosedXML million-row object graph, no eager shared-string table. Writes a worksheet's actual rows/cells progressively to temporary output; Excel's maximum 1,048,576 rows is respected. NULL remains blank, high-precision integers preserved as text, representable finite numeric cells typed, inline strings never evaluated as Excel formulas. Invalid XML or overlong text cells fail closed instead of publishing misleading output.
- `SafeSqlExplorerService.cs`: SQL XLSX now streams through this writer; **MaxExcelRows = 1,000,000** records, CSV maximum stays 1,000,000. A larger result fails explicitly; temporary output removed and published destination not created. Cancellation, SQLite native authorizer, read-only connection and one SELECT/WITH guard remain in place; bounded export deadline 10 minutes and explicit user cancellation, not a claim that every 2GB query can complete in 10 minutes.
- `tools/SolarOfThings.SmokeTest/Program.cs`: a real **1,000,000-row synthetic recursive SELECT** generates XLSX, then `XmlReader` streams the ZIP worksheet and asserts **1,000,001 rows including header and the last cell value 1,000,000**, without opening an in-memory million-row workbook. Existing CSV/XLSX and SQL safety/regression smoke tests retained.

**Professional WPF editor additions:**
- App now references official WPF `AvalonEdit` NuGet `6.3.1.120` in centralized packages and app project. `MainWindow.xaml` replaces plain SQL TextBox with AvalonEdit's **TSQL syntax highlighting, real line gutter, caret navigation, multiline editor**, retaining query execution, schema navigation and cancellation. TSQL highlighting is a **visual aid**, not the SQLite execution parser or assurance of SQLite grammar equivalence.
- `MainWindow.xaml.cs`: caret line/column feedback updated for AvalonEdit, existing run/cancel/export preserved, plus three **selectable/persisted shortcuts** for run/CSV/XLSX with conflict rejection and F5 as execute alias. `Strings.es.xaml` and `Strings.en.xaml` provide localized shortcut controls and fixed F5 button label.
- **Not yet implemented:** fully reliable SQLite engine error-offset/token diagnostics, completion/find/comment toggles, accessibility and live visual owner-QA, performance profiling on realistic 2GB corpora, full v0.11.0 completion.

**Validation:**
- Windows GitHub Actions **Build 695**, source `6a0d23397f29d8a9bd2a9b90577106896a50b2e4`, run `37833863050` => **SUCCESS**: new streaming XLSX, synthetic million-row export/row count, SQLite smoke, Windows publish/upload.
- Windows GitHub Actions **Build 696**, source `81466076bd8fac2879d409124dc1eb4668d55d6a`, run `37834511903` => **SUCCESS**: AvalonEdit package restore, WPF/.NET compile, synthetic SQLite smoke including XLSX last-data-cell check, Windows publish/upload. Live Enel/CNE probes **SKIPPED**. A subsequent pair of minor ES/EN button-label documentation/UI text commits was made **[skip ci]**, without code-path changes.
- CI success is **not** owner-PC visual QA, user-DB performance certification, official v0.11.0 completion or acceptance of a downloadable Build. As previously agreed, **do not require a separate user test for Builds 695 or 696**.

**Repository and safety:** work stays on `work/phase10-12-consolidated-20261007`, PR #1 DRAFT, `main` unchanged, no reading or writing `D:\SolarEnergyMonitorTest\Data\energy.db`, no change to the owner's provisional manual `Data` copy. No real restore actions enabled. Upon actual supply of new Enel evidence expected on **2026-10-12**, **re-study continuity** with owner before final project closure.

**Next work:** further SQL usability (SQLite-specific error positioning only where reliable; editor search/find, undo, shortcuts review), WPF responsiveness/performance instrumentation and remaining v0.11.0 agreed UX. Heavy SQL/Excel stress should be measured on representative synthetic corpora without claiming performance of the owner's workstation.

## Build 697 — SQL find, conservative SQLite diagnostics and duration/bytes CI PASS (2026-10-08)

**Owner requested continuation of v0.11.0 development**, explicitly previously preserving the one consolidated owner-QA handoff. Work remains on draft PR #1; no merge to `main`, no owner `Data` or manually copied `Data` accessed, modified, restored or reclassified as a verified backup.

**New implementation:**
- `SqlErrorDiagnostics.cs`: structured descriptions of SQLite errors containing base and extended error codes and error text. Only when a named table/column reference is **unique in the SQL text outside comments and string literals** does it provide an **approximate identifier reference** (line, column, character offset), explicitly **NOT** a SQLite parser's exact error position. Ambiguous or unavailable location returns `NO_RELIABLE_ERROR_LOCATION`; UI does not invent exact caret locations.
- `MainWindow.xaml` + `MainWindow.xaml.cs`: find bar for SQL editor, next-match and wraparound behavior, **Ctrl+F / F3** shortcuts, dedicated diagnostics and query/export performance displays that cannot be overwritten by ordinary caret movement. Where a unique probable identifier exists, a highlighted position is labelled *approximate only*. ES/EN localized controls in both resource dictionaries. Existing query security, bounded previews, custom run/CSV/XLSX shortcuts and native syntax-highlighted AvalonEdit remain.
- `SafeSqlExplorerService.cs`: export completion result now provides observed end-to-end duration and resulting file byte count, without persisting SQL text, sensitive source records or secrets. Preview already had measured engine-side elapsed time and bounded returned-row count. These are **observations**, not guaranteed workstation performance.
- `tools/SolarOfThings.SmokeTest/Program.cs`: synthetic SQLite tests for unique named-object error with labelled approximate line/column, multiple matches returning **no claimed position**, and export-duration/size assertions against actual output files. All previous SQLite tests including streaming XLSX **one million records + header** remain included and passed.

**Windows validation:** GitHub Actions source/test commit `ede2ac0c6908ed9007d16177416b52b2cbd82159`, run `37835624871`, **Build 697**, completed **SUCCESS**. Windows/.NET/WPF build, SQLite smoke including the newly introduced diagnostics and existing one-million-row XLSX check, portable win-x64 publish/marker and CI artifact upload PASS. Optional live Enel and CNE probes **SKIPPED**, not certified; CI cannot replace user-PC visual QA or performance measurement on a representative large database.

**Still pending:** live visual QA, editor text search UX interaction review, deeper verified SQLite parser-specific error offset support (not represented as implemented), optimization and benchmark baselines for cold/warm startup, navigation, charts, SQL plans and UI latency on synthetic representative 2GB-equivalent corpus, the other agreed v0.11.0 screens/redesign, final safe restore adapters and owner acceptance. No new user-facing Build was delivered or requested. The owner must re-study continuity when new Enel evidence actually arrives on **2026-10-12** before final project closure.

## Builds 698–699 — bounded UI startup/navigation timings and lazy page rendering (2026-10-08)

**Owner authorized proceeding with v0.11.0 WPF responsiveness/performance work.** Scope remained code and synthetic CI only, on draft PR #1, with no access to owner's real `D:\SolarEnergyMonitorTest\Data\energy.db` or provisional manual `Data` copy. No integration with the public `main` branch and no standalone owner-QA Build delivered.

**Instrumentation added:**
- `src/SolarOfThings.Core/Diagnostics/UiPerformanceRecorder.cs`: thread-safe, **in-memory only**, 256-sample FIFO of fixed non-sensitive operation labels and elapsed milliseconds; bounded aggregation of sample count, observed mean, p95 and max. No SQL text, disk paths, device IDs, user data, secrets, telemetry values or writing to any persistent database/log. Sampled observations are not controlled benchmarks.
- `App.xaml.cs`: measures startup import check, SQLite initialize, WPF MainWindow constructor and time until the main window is shown. Measurement does not assert that the app is fully data-ready or that all background initializations have completed.
- `MainWindow.xaml.cs`: measures loaded initialization, navigation and synchronous dashboard/battery/data coverage/analysis refresh; grid utility view refresh is instrumented across its async workflow. Analysis refresh includes chart work. These timings mix query and UI rendering and are **not** a specialized query-plan profiler.
- Diagnostics page shows a local, refreshable, ES/EN timing summary, clearly labelled as current session only, with observed sample counts/mean/p95/max and explicit warning that numbers are not performance guarantees or a controlled comparison. No automatic export of timings containing private data.
- `tools/SolarOfThings.SmokeTest/Program.cs`: synthetic concurrency tests, FIFO capacity 256, double-disposal safety and rejection of path-like/unsafe operation names. No fixed milliseconds targets or false claims about speed on an unknown PC.

**Conservative, result-preserving optimizations:**
- MainWindow constructor still populates the **visible Dashboard** but no longer pre-queries invisible Battery and Data Coverage pages. Loaded initialization retains the existing dashboard refresh and normalizer/behavior rebuild conditions, but avoids duplicate invisible-page queries. When the user navigates to Battery/Data, their existing `ShowPage` handlers populate the screen using the same functions and calculations. The visible Dashboard also refreshes whenever entered. On language change, only visible result views are reloaded rather than querying and replotting hidden sections; localization resources remain bilingual and screens refresh upon activation.
- `src/SolarOfThings.Core/Infrastructure/ResponsiveGridLayoutPolicy.cs`: pure breakpoint selection 1/2/4 columns at usable widths `<620`, `620..1039.999`, and `>=1040`. The WPF layout now skips clearing/rebuilding every grid's definitions while resizing within the same column-count band; regular Grid adaptive sizing still works. The first constructor layout always builds. Synthetic tests assert precise threshold cases, repeat-width stability and invalid-width fallback.

**Evidence:** Windows GitHub Actions **Build 698**, source `cb5224dcb58a946ed8259ed75d49a0ad28b67cc1`, run `37836575872`: SUCCESS, WPF compile, SQLite smoke, portable package upload. **Build 699**, source `490023b67d832d5588e8fd213f28f2efe2a3b0b8`, run `37836773506`: **SUCCESS**, WPF compile plus extended breakpoint and recorder synthetic tests, SQLite smoke, portable packaging and upload. Opt-in live Enel/CNE probes SKIPPED, not certified. These are CI validations, **NOT** visual owner-PC acceptance or quantified before/after performance proof.

**Still open:** collect repeated startup/navigation/chart/SQL benchmark observations from controlled representative synthetic corpora, cold vs warm and v0.10 Build-685 reference where feasible; review large-DB UI stalls and optimize heavy analysis queries after measurements; remaining agreed v0.11.0 page redesign, safe recovery adapters and full owner QA. Preserve all accepted calculations and WAL/backup gates. Owner asked to re-study continuity once new Enel data actually arrive on **2026-10-12**, before final v0.11.0 closure.

## Build 700 — visibility-gated data refresh and split service timings (2026-10-08)

**Code commit:** `de3ef46cd3ed77efee6c42bcd88c924d745ff077`. **CI:** Windows workflow run [37838877802](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37838877802), Build **700**; **SUCCESS**, verified against the source commit above. Windows/.NET WPF build PASS, SQLite smoke tests PASS (including deterministic timing statistics and the existing SQLite/XLSX regressions), portable Windows x64 publish PASS and artifact upload PASS (artifact ID `11576577586`, `SolarEnergyMonitor-win-x64-dev`). Live Enel/CNE probes **SKIPPED**. This is CI evidence only, not owner QA. PR #1 remains Draft, with `main` unchanged.

- `MainWindow.xaml.cs`: adds bounded in-memory **service retrieval** timing scopes for Dashboard latest stored metrics, coverage, latest-day energy statistics, and Data Coverage summary/normalized count/configuration health. These scoped measurements complement the existing whole-view UI timings. Service duration can include C# computation; it is **not** equivalent to raw SQL execution time or a controlled workstation benchmark. Timing labels contain no user SQL, private paths, IDs or energy data.
- After full history synchronization and after current-state refresh, heavy Dashboard/Battery/Analysis/Data/Reports data refresh now runs only when the corresponding page is visible. Existing `ShowPage` handlers populate pages when activated; stored calculations and UI data formats are unchanged. Commissioning's special range-initialization behavior remains as it was.
- `tools/SolarOfThings.SmokeTest/Program.cs`: deterministic synthetic timing-summary regression verifies sample count, mean, nearest-rank p95 and maximum (20 supplied durations) and nested data/UI scopes, without asserting a machine-specific speed target.
- These changes do **not** modify SQLite schema, WAL/backup/restore behavior, Enel reconstruction, billing rates, exports or owner files. Real `D:\SolarEnergyMonitorTest\Data\energy.db` and the owner's manual Data copy remain untouched. No real recovery enabled.

**Validation limits:** CI PASS does not establish a before/after speedup percentage, and no QA has been performed on the approximately 2 GB real database, the owner's Windows device, or its visual interface. Future performance work: controlled representative synthetic-corpus startup/navigation/queries, query-plan evidence where useful, cold/warm comparisons and WPF UI-response review. Continue the one consolidated owner-QA handoff policy and 2026-10-12 conditional Enel reassessment.

## Builds 701–703 — synthetic SQLite corpus and verified latest-metric optimization (2026-10-08)

**Scope and security:** This tranche performed only approved independent v0.11.0 optimization work in draft PR #1. No real owner's database, copied Data folder, Enel source, secrets, migration, restore or backup was touched. SQLite schema remains v17; existing composite index/primary key are reused. No change to energy integration/calculation algorithms or public normalization model.

**Build 701:** initial synthetic corpus commit 54e3bee05467afabe97fd65bb19af5edf9aa4636, CI FAIL (C# CS1674: FileInfo is not IDisposable). Corrected in commit 0d5558c5d421fb826f4c78a06cde2fc48882ee42. Build 701 is *not* PASS.

**Build 702:** [Windows CI run 37839766156](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37839766156), source 0d5558c5d421fb826f4c78a06cde2fc48882ee42, **SUCCESS**. Added isolated temporary SQLite performance corpus to existing smoke project and a dedicated CI step. Fixture: 24,000 artificial 3-minute timestamps, 120,005 normalized records (including five other-device samples), 24,000 history records, file size 61,288,448 bytes. All-record-field parity for old window ROW_NUMBER lookup versus candidate grouped MAX/join lookup PASS. Existing SQLite smoke, WPF compile, win-x64 publish, artifact upload PASS; live Enel/CNE probes SKIPPED.

**Build 702 observed timings only on hosted Windows CI** (7 alternating warmed runs each; not cold-disk data or a guarantee): original ROW_NUMBER median **248.93 ms**, p95 250.99 ms; grouped MAX/join median **178.09 ms**, p95 187.46 ms, roughly **28.5% lower observed median** for this artificial corpus. Both plans used ix_normalized_metric_device_metric_time. Separate single measurements: history coverage 5.38 ms, normalized count 20.83 ms, four-metric daily energy 3.12 ms. These observations do NOT establish workstation behavior against the real ~2 GB database.

**Build 703:** [Windows CI run 37840198047](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37840198047), source 0b66185373fcc2a98f12e000c7fd46833bbe0b37, **SUCCESS**. Production NormalizationRepository.GetLatestMetrics now uses grouped MAX of the last eligible timestamp and indexed join, retaining identical field projections, confidence/null eligibility, device filter and API. No DDL/index or structural migration. The benchmark now executes the original window query as an independent reference against the actual production code. Added latest-record NULL / UNRESOLVED cases, two wholly ineligible metric types and later other-device values. Full-record comparison **PASS**, including metadata; no rows were written to or read from the real owner's database.

**Build 703 synthetic observations** (24,000 frames, 120,007 normalized rows, 24,000 history rows, SQLite file 61,292,544 bytes, 7 alternating warmed runs each): old reference window median **265.62 ms**, p95 273.15 ms; new production grouped MAX median **189.63 ms**, p95 199.34 ms, roughly **28.6% lower observed median** *within the same CI run*. WPF build, existing smoke, extra corpus tests, portable win-x64 publication and artifact upload PASS (CI artifact 11576983575); live Enel/CNE SKIPPED.

**Limitations and next work:** No controlled Windows owner-device testing, cold/warm user startups, 2 GB representative workload, freeze/frame-latency study or before/after end-user QA has been performed. Synthetic benchmarks cannot certify a real-world percentage improvement. Future work may add a controlled larger artificial corpus / workload profiles and inspect specific WPF UI-thread stalls without inventing improvements or recalculating accepted physics. Continue one consolidated owner-QA handoff, no merge and 2026-10-12 Enel continuity reassessment only after actual new owner evidence.

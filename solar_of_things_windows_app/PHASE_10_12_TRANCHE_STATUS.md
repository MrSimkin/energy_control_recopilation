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

## Build 704 — scalable synthetic corpus and foreground Dispatcher scheduling telemetry (2026-10-08)

**Source commit:** a61b95ce33e7046b1ee8c99f550a08015473ebc3. **Windows CI:** [run 37841089951](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37841089951), **SUCCESS**, Build 704. .NET 10/WPF compile, existing full SQLite smoke, two independent isolated synthetic performance-corpus passes, portable Windows x64 publish and artifact upload PASS (CI artifact 11577478097). Optional live Enel/CNE validation SKIPPED, not equivalent to owner QA.

**Engineering changes:**
- SyntheticPerformanceCorpus.cs now parameterizes the same deterministic fixture at **24,000** and **96,000** artificial 3-minute frames; separate CLI modes and explicit Windows CI steps. Isolation remains via a randomized temporary directory and explicit AppPaths(rootOverride), with cleanup, no owner's real DB/backup, no secret or production data. The future-dated second-device sentinel was moved to 2030 so it stays outside both tested fixture periods. Existing full-record parity compares the independently preserved historical ROW_NUMBER reference query against the production optimized MAX/join lookup, including NULL, UNRESOLVED and device separation. No new index, DDL, migration or changed physics.
- New DispatcherTimingPolicy.cs filters observed DispatcherTimer scheduling lateness: record only if an expected one-second active-window callback arrives at least **150 ms late**, while ignoring gaps greater than 10 seconds as ambiguous possible OS sleep/suspension. WPF MainWindow starts/stops a background-priority DispatcherTimer on window activation/deactivation, reports fixed operation UI.Dispatcher.TickLateness to the existing bounded in-memory recorder, and disposes subscriptions on close. This does **not** create a background polling thread, perform SQL, collect sensitive source data, or prove the cause of an unresponsive interface. Ordinary ticks and inactive/minimized windows produce no delay samples.
- The existing ES/EN Diagnostics summary explicitly labels scheduling lateness as an observation only, not a certified UI freeze. Synthetic deterministic tests cover threshold boundaries, ordinary ticks, OS-suspension-sized gaps and invalid interval rejection.

**Actual Build 704 CI observation — hosted Windows runner, same run, seven alternating warm repetitions per query**:

| Isolated corpus | SQLite file bytes | Window ROW_NUMBER median / p95 | Production grouped MAX median / p95 | Full-record parity |
|---|---:|---:|---:|---|
| 24,000 frames; 120,007 normalized samples | 61,292,544 | 244.82 / 249.46 ms | 179.27 / 187.65 ms | PASS |
| 96,000 frames; 480,007 normalized samples | 245,469,184 | 973.55 / 983.22 ms | 718.90 / 725.48 ms | PASS |

The production query was approximately **26.8%** and **26.2%** lower in observed median than the old reference, respectively, on these two synthetic hosted-CI configurations. This does not demonstrate a real-user speedup, particularly not with the owner's 2 GB SQLite WAL database. Both query plans still referenced the existing ix_normalized_metric_device_metric_time index. Separate one-off observations for coverage/count/energy are preserved in CI logs; they are not a controlled latency distribution. Fourfold growth in synthetic rows coincided with roughly fourfold growth in lookup median duration; do not extrapolate linearly to owner hardware or claim scalability certification.

**Still open:** target-PC screenshot/visual QA, observing Dispatcher delays under representative interactions, controlled OS cold vs warm startup, memory pressure, larger/more representative synthetic corpus, and root-cause isolation of any UI freeze. The dispatcher monitor by itself cannot distinguish UI thread work from scheduling delays; it is collected automatically only while the window is foreground-active, is visible through Diagnostics and is never persisted. Keep the agreed single consolidated owner-QA delivery, draft PR #1, main intact, never touch the owner's real Data or provisional copy, and revisit Enel continuity only after actual new material is received on or after 2026-10-12.

## Build 705 — coalesced background Dashboard data reads (2026-10-08)

**Code/validation:** Source commit e97793502facab4886306bf43bf5701a55cb4de5. [GitHub Actions run 37842736276](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37842736276), Build **705 SUCCESS**. WPF/.NET 10 Windows compile, existing SQLite smoke checks, synthetic 24,000-frame and 96,000-frame performance-corpus/field-parity steps, portable win-x64 packaging and CI artifact upload PASS. CI artifact id **11578641907**, for consolidated development only; no per-block owner-QA download requested. Live Enel/CNE probes skipped.

**Issue isolated by code inspection:** MainWindow.RefreshDashboardMetrics previously executed stored latest-metric retrieval, latest current-state snapshot and historical coverage/last-day EnergyRangeStatisticsService.Get synchronously on the WPF Dispatcher. Build 704's Dispatcher lateness telemetry reports only active-window tick scheduling delay; CI cannot reproduce the real owner's visual navigation or claim a freeze root cause.

**Change:** Dashboard refresh now captures the configured device and required repository services on the UI thread; the existing read-only per-service calls, local-day timezone selection and signed-energy statistics execute in a Task.Run worker with independently created SQLite connections. No WPF DispatcherObject or visual element is touched by worker logic. Only the final SetPowerMetric/SetSocMetric, operating/freshness texts and daily-energy TextBlock updates run after await on the UI Dispatcher. A single-flight, generation-guarded refresh loop coalesces overlapping startup/navigation/language/sync requests and discards superseded results. Navigating away or closing the window invalidates pending results; before applying, it re-checks generation, page visibility and configured device. On an active read failure, previous metrics are cleared and a short localized message is shown instead of leaving potentially stale values. No database writes, backup/restore actions or schema/index changes were added.

**Regression coverage:** Introduced pure DashboardRefreshPolicy.CanApply with deterministic smoke assertions covering current request, obsolete generation, hidden page, closed window, changed device and missing current device. Existing synthetic corpus independently verifies the optimized latest-metric SQL returns every field of the previous reference on both sizes, including NULL/UNRESOLVED states. Both corpus passes remain CI SUCCESS. Build 705 hosted CI lookup observations: 24,000-frame original window median 209.30 ms vs production grouped MAX 151.77 ms; 96,000-frame original 863.66 ms vs grouped MAX 665.22 ms (seven warmed observations each, not real-owner timings). These SQL comparisons are carry-forward regressions, **not** a measured WPF responsiveness improvement from moving the query off-thread.

**Observability limitation:** The formerly inclusive UI.Dashboard.Refresh measurement now times the UI rendering phase only, so its values are not comparable directly to older builds. New Data.Dashboard.BackgroundFetch covers the background service retrieval/aggregation interval; existing nested Data.Dashboard.LatestMetrics, Data.Dashboard.Coverage and Data.Dashboard.LatestDayEnergy timing labels remain. The user-device CPU/RAM, frame rates, actual before/after UI stalls, cancellation mid-SQL and real SQLite ~2 GB corpus have not been tested; Build 705 is CI acceptance, **not owner-QA approval**.

**Boundaries and next gate:** main and Draft PR #1 unchanged as workflow policy; preserve Build 684 Enel Fase 10 accepted accounting observations, PDF uncertainty and the post-2026-10-12 evidence-only reassessment. Do not access the real D:\SolarEnergyMonitorTest\Data\energy.db or owner's backup/copy, claim a real recovery, merge main or request piecemeal application tests. Next performance work: selective page-load measurement and visual target-Windows QA in the eventual single consolidated v0.11.0 delivery, followed by correction only with real evidence.

## Build 706 — Battery background reads and Analysis cost-center instrumentation (2026-10-08)

**Evidence:** source commit 62feb290a5ab97160552046c3358c8a5a796a275, [GitHub Actions run 37843510252](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37843510252), **Build 706 SUCCESS**. Windows WPF/.NET 10 compilation, full SQLite smoke, both 24,000-frame and 96,000-frame synthetic performance-corpus full-record parity, Windows portable x64 publish and artifact upload PASS (developer artifact id 11578876349). Optional live Enel and CNE validations SKIPPED, not accepted as live regression evidence.

**Battery change:** previously RefreshBatteryView ran NormalizationRepository.GetLatestMetrics, CurrentHouseholdSnapshotService.GetLatest, BatteryConfigurationService.Get, and BatteryThresholdContextService.Get synchronously on the UI Dispatcher. The new single-flight LoadBatteryViewAsync resolves non-WPF services on the UI thread and retrieves all four existing values within a Task.Run worker using their existing separate SQLite connections. Results carry immutable records and are rendered back on the WPF Dispatcher using exactly the pre-existing charge, stored/ordinary/emergency reserve kWh, activity and technical power/SOC formulas. Background calls are recorded with fixed non-sensitive Data.Battery.BackgroundFetch, Data.Battery.LatestMetrics, Data.Battery.CurrentSnapshot, Data.Battery.Configuration and Data.Battery.Thresholds labels. UI.Battery.Refresh now represents UI rendering only and **cannot** be compared naively with the old inclusive timing label.

A new BatteryRefreshPolicy.CanApply deterministic smoke guard rejects old request generations, changed device IDs, a hidden Battery page, closed window or missing device. ShowPage invalidates Battery results on navigation away; MainWindow_Closed invalidates them on shutdown. Simultaneous requests coalesce into a single active read and a latest-generation retry; on a current read failure all visible Battery metrics are reset and a localized message is presented rather than leaving stale energy values. This is a concurrency-risk reduction but not a complete integration/UI-stress test; no cancellation of an already executing SQLite query is asserted.

**Analysis instrumentation only:** original analysis calculations still execute on the Dispatcher and remain mathematically unchanged. New scoped service-duration labels Data.Analysis.Coverage, Data.Analysis.EnergySummary, Data.Analysis.Aggregation, Data.Analysis.HouseholdStats and Data.Analysis.ChartThresholds identify which existing repository or aggregation steps dominate. UI.Analysis.ChartSetup independently observes the WPF-bound grid and ScottPlot chart setup, although this span still nests the ChartThresholds retrieval. ScottPlot graph operations and WPF controls were **not** moved off the Dispatcher; deciding whether to offload them requires representative on-device evidence and a separately tested async design.

**No safety boundary changed:** SQLite schema remains v17; no database/index migration, re-normalization, Enel tariff, physics, backup/restore or user-file change. The real approximately 2 GB D:\SolarEnergyMonitorTest\Data\energy.db and owner's manually copied Data folder are untouched; main remains unchanged and PR #1 stays Draft. The Build 706 CI PASS cannot establish actual battery-page responsiveness, frame rate, Analysis chart latency or owner's real-world QA acceptance.

**Next gate:** use Diagnostics' bounded in-session observations during the eventual *single consolidated* v0.11.0 owner-QA exercise to rank Analysis hot spots, then select a narrowly scoped async computation/refactor only where supported. Continue original conditional 2026-10-12 Enel data reassessment solely if new owner evidence is provided; do not download a per-block build or merge to main.

## Build 707 — Analysis background data preparation and stale-range guard (2026-10-08)

**Commit/CI:** code f3289dc0d8a2984b1dc83af022013f423eb643f0, [Build 707 GitHub Actions run 37844297192](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37844297192) **SUCCESS**. Windows .NET 10/WPF build, complete SQLite smoke, both existing isolated 24,000-frame and 96,000-frame synthetic corpus full-field lookup parity, portable win-x64 publish and upload PASS (CI artifact 11578379665). Optional live Enel/CNE probes SKIPPED; no per-block owner QA requested.

**Code diagnosis:** RefreshAnalysisView previously fetched historical coverage, energy range statistics, five-source energy/SOC aggregation, household behavioral stats and chart thresholds on the WPF Dispatcher before rendering ScottPlot. Existing Build 706 timing labels distinguished Data.Analysis.Coverage, Data.Analysis.EnergySummary, Data.Analysis.Aggregation, Data.Analysis.HouseholdStats and Data.Analysis.ChartThresholds but did not eliminate synchronous waiting.

**Change:** The Analysis loader is now an async single-flight/coalesced worker. It queries historical coverage via Task.Run and, after checking generation/device/current page and selecting a calendar range exclusively on the UI thread, fetches the same EnergyRangeStatisticsService, EnergyAggregationTableService, HouseholdBehaviorStatisticsService and BatteryThresholdContextService outputs in a Task.Run worker. ReadAnalysisData performs no WPF operations; it accepts only time-zone/period/device value arguments and concrete non-UI data services. The existing energy coverage mathematics, hourly state-duration classification, local-day start/end semantics, chart axes and chart rendering functions are retained. RenderAnalysisEnergySummary, RenderAnalysisAggregationTable and RenderAnalysisBehavior update controls only after UI-Dispatcher await and validation. ScottPlot rendering remains on WPF Dispatcher; chart threshold values are now retrieved in the worker and handed to the existing chart setup method.

**Concurrency/UX:** Refresh requests from navigation, language changes, calendar filters and post-sync completion are coalesced, with incrementing generation. Leaving the Analysis page or closing the MainWindow invalidates in-flight results. The pure AnalysisRefreshPolicy.CanApply check also enforces matching configured device and visible page; deterministic smoke test cases cover active/stale requests, different devices, hidden/closed UI and missing device. Pending initialize-range requests are deferred when Analysis is hidden; calendar initialization is performed on the Dispatcher, suppressing internal DatePicker selection events. A visible localized calculating status is used; read/render failures reset the visible Analysis values instead of leaving stale results.

**Timing labels:** UI.Analysis.Refresh now observes only post-fetch rendering; it is not directly comparable with the former inclusive synchronous duration. Data.Analysis.BackgroundFetch now observes background energy/aggregation/behavior/threshold service time, while Data.Analysis.Coverage remains measured separately as an asynchronous read. UI.Analysis.ChartSetup continues timing WPF-bound ScottPlot/grid construction. **No measured owner-PC UI latency or responsiveness improvement is claimed** by the Windows CI PASS.

**Still outstanding:** actual on-device navigation and chart responsiveness, manual calendar-selection flows in real WPF, chart refresh UI thread costs, background-query cancellation during rapid navigation, and any measurable regression under the real ~2 GB database. ApplyAnalysisRangePreset still obtains coverage synchronously when a preset is selected and may merit independent optimization if target evidence justifies it. No SQLite schema/migration/index, normalization, energy physics, Enel accepted invoice findings, backup/restore or production user Data were modified; the owner's D:\SolarEnergyMonitorTest\Data\energy.db and copied Data remain untouched. Continue a single eventual v0.11.0 owner-QA handoff. PR #1 stays open and Draft, main unmodified, with conditional Enel continuity reassessment only if new evidence arrives on or after 2026-10-12.

## Build 708 — asynchronous Analysis range presets and reuse of chart data (2026-10-08)

**Verified source and run:** code commit 154cbf2e1e6980508475fad23250dd2fec738bcc. [GitHub Windows CI Build 708, run 37846363354](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37846363354) **SUCCESS**: .NET 10/WPF compile, SQLite smoke, both isolated synthetic SQLite performance corpora (24,000 and 96,000 frames) with all-field lookup parity, portable win-x64 publish, portable marker and artifact upload PASS (development artifact 11579947840). Live Enel/CNE steps SKIPPED, not falsely claimed tested.

**Targeted responsiveness changes, without changed energy calculations:**
- A chart energy-series checkbox no longer triggers a full refreshed historical analysis: when the last successfully rendered EnergyAggregationTable is still for the current device and generation, refresh just the *energy* ScottPlot using its already computed rows. It does not re-query SQLite, re-run EnergyRangeStatisticsService/HouseholdBehaviorStatisticsService/EnergyAggregationTableService or redraw the unrelated battery plot merely to toggle solar, house or grid series. The current selected checkbox states still determine visible plotted series. During a running data fetch, changes require no new query: the final renderer sees the latest checkbox values. If there is no matching cached result, the pre-existing full Analysis refresh path remains the fallback. Pure AnalysisRefreshPolicy.CanReuseChart smoke tests check invalid generation, invisible page, loading state, different/missing device and uninitialized generation. Cache is in-memory and invalidated when a fresh Analysis data request begins, when navigating away, on window close and on view reset. No external data are persisted.
- Analysis preset selection previously called HistoryRepository.GetCoverageSummary synchronously in the WPF SelectionChanged handler. It now captures the intended preset and generation and executes the same read-only coverage query inside Task.Run; after await, it checks the still-current device, preset, window and page before computing the existing calendar range (TimeRangeSelectionService.ForDay/ForRolling7Days/ForCalendarWeek/ForMonth/ForYear/ForRolling12Months/ForArbitraryDateRange) and updating WPF DatePickers on the Dispatcher. A new preset or explicit date edit supersedes outstanding preset reads. Analysis background refresh avoids starting a query against the old selected range while a preset read remains outstanding. User-facing selection failures are localized. This is not guaranteed SQL-query cancellation mid-read.
- Added nested in-memory, non-sensitive UI.Analysis.EnergyChart and UI.Analysis.BatteryChart scopes alongside existing UI.Analysis.ChartSetup and UI.Analysis.SeriesToggle for diagnosing chart preparation in the eventual target-device QA. These duration scopes may contain synchronous ScottPlot Refresh() work; they are **not** frame-latency or a measured owner-PC speedup.

**Boundaries and outstanding validation:** Build 708 CI PASS validates compilation and synthetic regression checks, not actual UI responsiveness or full WPF interaction with the owner's real 2 GB SQLite file. No changed schemas/indexes, charging physics, energy integrations, invoice tariff math, backup/restore, data recovery, Enel accepted Phase 10 outcomes, or access to D:\SolarEnergyMonitorTest\Data\energy.db / copied owner Data folder. Main remains unchanged, PR #1 stays Draft; there is no interim owner download or per-block QA requirement. Manual target-Windows checks remain necessary for rapid preset switching, custom-date edits, chart checkbox toggles, visible-series correctness, locale selection, and real navigation under heavy historical data. Do not extrapolate speed from the synthetic CI to the owner's computer. Continue single consolidated v0.11.0 QA handoff; Enel reevaluation only after real new owner evidence dated on/after 2026-10-12.

## Build 709 — Battery navigation snapshot off Dispatcher and stale-poll rejection (2026-10-08)

**Evidence:** Source commit 742aeb3b0c26fc9aade271b3022ccc0b9e0b7ac7; [Windows CI Build 709 run 37847090702](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37847090702) **SUCCESS**. WPF/.NET 10 build, full SQLite smoke (schema v17), 24,000-frame and 96,000-frame isolated synthetic performance corpus full-record query parity PASS, Windows portable win-x64 packaging and developer-artifact upload PASS (artifact id 11580078528). Live Enel/CNE steps SKIPPED, not treated as validation.

**Residual synchronous navigation identified and corrected:** In Navigation_Click, after ShowPage("Battery") initiated Build 706's nonblocking Battery data fetch, the navigation event still called CurrentHouseholdSnapshotService.GetLatest(profile.DeviceId) synchronously on the WPF Dispatcher to determine whether the current-state snapshot was fresh. This could block navigation independently of the already offloaded page refresh, and could decide to request live data after the user navigated elsewhere.

**Change:** Resolve the snapshot service on the UI thread, then call its existing read-only GetLatest via Task.Run with a fixed Data.Battery.NavigationSnapshot in-memory timing label. Capture the Battery page's current refresh generation immediately after navigation. After the lookup completes, BatteryNavigationRefreshPolicy.ShouldRefresh ensures request/latest generation still match, Battery remains visible, window not closed, configured device unchanged, session still exists, and snapshot is not already fresh. Only then call existing RefreshCurrentStateAsync(profile, showError:false). A failed snapshot lookup is caught/logged by exception type only and never escalates into an unhandled async-void navigation exception or initiates speculative polling. A deterministic core smoke suite checks the allowed stale-snapshot path plus superseded generation, hidden/closed window, device mismatch/missing device, disconnected session, and fresh snapshot. No new write query or table was introduced.

**Boundary:** BatteryNavigationRefreshPolicy is only a pure decision guard. Its smoke assertions do **not** represent a real WPF end-to-end navigation race, remote session/network test, or measured owner-device latency. An already executing Task.Run SQLite read is not forcibly cancelled. The live refresh function itself retains its existing behavior once legitimately started. UI-state check happens before that call, not necessarily throughout subsequent remote I/O.

**Safety/next gate:** No owner Data, secrets, energy calculations, normalization, Enel billing interpretations, databases/schema/indexes or backups touched. The real D:\SolarEnergyMonitorTest\Data\energy.db (~2 GB) and manually copied Data folder were not read or used in synthetic CI. PR #1 remains Draft and main stays unmodified. Continue consolidated v0.11.0 QA only, without per-block owner downloads. Recommended next step is an explicit, minimal Windows UI/navigation test plan covering rapid page switches, Battery freshness, Analysis preset toggles, and compare bounded UI.Dispatcher.TickLateness to individual Data/UI operation timings, without asserting performance causality or a measured improvement before owner-machine evidence.

## Build 711 — Analysis manual range edit stale-request protection (2026-10-08)

**Verified CI:** source commit 34b6a5be7db1d3e298a238405f67257bc3701a8e, [Windows Build 711 / run 37849700137](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37849700137), **SUCCESS**. Build 710 (commit cb1c55a5a50479193cbd801052185f8b97258c74) was an intermediate staging-only commit introducing a private state flag and is NOT the final functional acceptance gate. Windows WPF/.NET 10 compilation, SQLite smoke, isolated 24k/96k synthetic corpora, portable win-x64 publish and upload PASS; optional live Enel/CNE probes SKIPPED.

**Race fixed:** An Analysis manual date edit could occur while an asynchronous old-range query ran; the previous generation could paint the old chart after the new DatePicker selection. Manual date editing has explicit Apply semantics, not automatic SQL query semantics.

**New behavior:** Manual unsuppressed DatePicker edits invalidate the pending Analysis generation/preset, set _analysisCustomRangePendingApply, select Custom and show a localized "range changed; press Apply" message. A running asynchronous Analysis worker does not continue to another fetch while custom dates are unapplied. Chart checkbox toggles do not implicitly apply manual date changes. Explicit Apply invalidates pending preset reads, clears the guard and starts the existing background refresh. Selecting a preset supersedes pending custom changes, while choosing Custom alone still waits for Apply. Programmatic date resets now suppress events so they cannot accidentally be treated as manual selections; entering Analysis on navigation remains an explicit reload.

**Limits:** No WPF UI automation/test against the owner's local ~2 GB SQLite database has been performed; Build 711 CI does not establish owner-device responsiveness or prove all fast-selection UI races resolved. No energy formulas, indexes, schema, billing, Enel data, backups, restore or actual owner Data were accessed or modified. Main unchanged and PR #1 remains Draft. Confirm rapid edits/preset switching/Apply in eventual **one consolidated v0.11.0 QA**.

## Build 714 — prioritized safe performance diagnostics and consolidated QA guide (2026-10-08)

**Definitive CI:** [Build 714 / GitHub Actions run 37850199861](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37850199861) **SUCCESS** for source commit 828a7f0af94461d1b2803688f0a8e0f2e174caae. Windows .NET 10/WPF compilation PASS, SQLite smoke PASS (including deterministic sorting check), 24,000-frame and 96,000-frame isolated synthetic corpus parity PASS, portable win-x64 publishing and artifact upload PASS (artifact 11581677589). Enel/CNE live validations SKIPPED, not PASS. Earlier Builds 712 (core sorting only) and 713 (UI wording before smoke) are staging/incremental commits; use Build 714 as final verified source for this specific tranche.

**Diagnosis:** Diagnostics previously displayed the first 24 operation summaries ordered alphabetically by label; with increasing measured labels from Dashboard, Battery, Analysis and navigation, this could obscure slow operations in a future session. The in-memory UiPerformanceRecorder.Summaries() now sorts by **observed p95 descending** and then operation name as a deterministic tie-breaker. Existing bounded 256-sample retention, names-only safe fixed labels, average/p95/max definitions and SQLite-free design remain unchanged. The WPF Diagnostics summary still displays only 24 lines to avoid unbounded output; it now explains the observed ranking, warns about low sample counts and states the number of additional operation summaries omitted. A synthetic smoke adds reproducible slower-operation first and alphabetical tie expectations; no absolute latency target is asserted.

**Manual single-session QA guide:** The new [V011_QA_CONSOLIDADA_NAVEGACION_RENDIMIENTO.md](V011_QA_CONSOLIDADA_NAVEGACION_RENDIMIENTO.md), documentation commit 0d67dfec0b1468b6e2e74fe4b40547c3b1ec74ff [skip ci], defines the eventual non-destructive owner QA path for page switching, Battery fresh/stale navigation, rapid Analysis presets, explicit custom date Apply, chart series toggles, ES/EN, Diagnostics p95 sampling and PASS/FAIL/NO PROBADO records. This is a **prepared QA protocol, not a performed or accepted owner-device test**. No interim per-Build download required.

**Safeguards and limitations:** Build 711 independently validated the prior manual-date race correction (source 34b6a5be7db1d3e298a238405f67257bc3701a8e; [CI 37849700137](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37849700137)). Builds 710/712/713 were intermediate commits and not substitutes for final CI outcomes. No real owner SQLite database, owner Data/Backups folder, secrets, schema, migration, energy formulas, tariff calculations, billing, reports, restorations or external data were modified; app remains v0.11.0 development, no final QA acceptance or demonstrated real PC speedup. UI.Dispatcher.TickLateness is only a timer-delay signal and is not an independent proof or cause of a freeze. The entire PR #1 must remain Draft; main stays unchanged. Full v0.11.0 testing/release, live Enel evidence after 2026-10-12, and owner merge authorization remain separate gates.

## Build 717 — secondary full-backup staging ownership / retry resilience (2026-10-08)

**Verified evidence:** core code commits 3a4c8e3eac7b1a566a6027662320b27e059b8416, 43494ea2ec04576e2672285293dc82e3a8a53a59 and regression smoke commit 39f22b697606b84e3bb4c438d9a7bb38ebf8a51b; [GitHub Windows Build 717 / run 37850931367](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37850931367) **SUCCESS**. Windows .NET/WPF compile, SQLite smoke (including secondary copy collision and staging-path sentinel checks), synthetic 24k/96k frames parity, portable x64 packaging/upload PASS. Live Enel/CNE checks skipped. Builds 715–716 were intermediate revisions, not the final regression reference.

**Bug addressed:** FullBackupService.CopyVerifiedToSecondary previously started the source/destination stream-copy **before** entering the try/finally that cleans up *.inprogress. A destination disappearing or running out of space mid-copy could leave an incomplete .inprogress file, preventing the next FileMode.CreateNew retry. The copy now runs inside the protected region. Cleanup is limited to an **owned newly created staging file**; if the .inprogress path already existed, its FileMode.CreateNew failure cannot cause deletion or replacement of that file. Existing completed destination ZIP with different bytes remains unmodified. Final destination is still published only after SHA-256 equality and full embedded archive/SQLite verification.

**Synthetic regression evidence:** Added a mismatched-existing-ZIP rejection and pre-existing .inprogress sentinel rejection/preservation check. Both pass in Windows CI. **CI did not inject a live disk disconnect or actually interrupt a transfer after partially writing; that failure behavior remains a code-level mitigation pending isolated fault-injection validation.** No real owner database/backups accessed or modified. No automatic deletion or restoration was introduced, and Build 684 Enel facts are unchanged. PR #1 Draft, main untouched.

**Next independent safety task:** validate the configured secondary directory consistently across settings, mirror and backup inventory; reject paths overlapping active app Data or Backup directories and filesystem redirects, using only synthetic fixtures.

## Build 718 — safe secondary backup destination across create, inventory and UI (2026-10-08)

**CI evidence:** code commit 55986d521a0ac00d0d66301765c094873d770649; [Windows Build 718 GitHub Actions run 37851302021](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37851302021) **SUCCESS**. Windows WPF/.NET 10 compile, SQLite smoke (including new nested Data/Backups/ancestor path rejection), both 24k/96k synthetic corpus all-field parity, portable x64 publish and developer artifact upload PASS (artifact 11581819709). Enel/CNE live checks SKIPPED.

**Scope:** Added shared BackupDestinationPolicy.Validate(AppPaths, destination) with absolute-path requirement, path/ancestor overlap checks and rejection of existing filesystem reparse points along the destination path. A secondary backup folder may not coincide with, contain, or be nested within active application Data or Backups. Paths pointing at files are rejected. FullBackupService.CopyVerifiedToSecondary, CompleteBackupInventoryService (list/verify/delete validation) and the WPF Settings folder-picker use the same decision gate before moving/copying any ZIP. A rejected user-selected folder is not saved and gives an error; a previously configured invalid directory is no longer used for inventory/mirror and is reported unavailable/invalid rather than silently accepted. The app still supports an ordinary secondary folder outside these protected areas, with the pre-existing SHA-256 and full ZIP/SQLite verification required before reporting a verified copy.

**Synthetic regression:** Smoke checks a valid separate destination, Data and Backups themselves, nested subfolders, the fixture-root ancestor and that direct mirroring to active Data is blocked before any nested destination directory is created. The pre-existing full-copy and physical-copy inventory tests continue to pass. **CI did not create a real Windows junction or unplug a removable disk; those outcomes are guarded in code but are not claimed tested as filesystem faults.** Merely placing a second folder on the same volume/disk does **not** provide device-level independence; the user must select independent physical storage for that protection. This policy is path/redirect safety, not a drive-identity or media-integrity certification.

**Boundaries:** No original user database at D:\SolarEnergyMonitorTest\Data\energy.db, secrets, backup file or manually copied Data folder was accessed, replaced, deleted or imported. No schedule change, retention/automatic deletion, active restore, SQLite schema/index migration, energy model, or accepted Enel results changed. Main unchanged and PR #1 remains Draft; this is CI-verified defensive implementation, not actual owner QA. Further Phase 12 work remains in the approved full-package completeness/inventory/recovery sequence, with synthetic-fixture gates and specific owner authorization before any real active-DB import.

## Builds 720, 723, 725 and 727 — four consecutive integrity hardening tranches (2026-10-08)

**Boundary:** development exclusively under Draft PR #1 branch work/phase10-12-consolidated-20261007; main remains at 59120a630b0f56684ba7672d960673f5c5c1797b. These four rounds improve the safety of complete backup packages and manually selected copies using temporary synthetic fixtures; **none is owner-on-device QA, real restoration, full Phase 12 acceptance, release or merge authorization**. No real user Data or backups (including the approximately 2 GB D:\SolarEnergyMonitorTest\Data\energy.db or any provisional folder copies) were read, deleted, overwritten, migrated or tested. The app/Enel accepted mathematical semantics and schema v17 were not changed. Optional live Enel/CNE workflow steps SKIPPED.

### Round 1 — Build 720, closed ZIP namespace

Code: 6ae2618941f55e04084d0addf0bbbf74a5bf61f7; regression commit fef5afd387e0d8fad942d37dc9307ab87a0c3c4d. [CI run 37852365733](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37852365733) **SUCCESS** (Windows WPF/.NET 10 build, SQLite smoke, 24k/96k synthetic parity, portable x64 publish/upload; artifact 11582177412). FullBackupService.VerifyArchive now rejects case-insensitive collisions in both the ZIP and manifest, plus files outside the format-v1 closed namespace: one database/energy.db and manifest.json, document files under documents/Bills/ or documents/Tariffs/ only. Synthetic test for an internally hash-consistent duplicate bill filename differing only in case passes by rejecting it. This prevents an archive that passes exact-string checks yet maps ambiguously to Windows extraction paths; **no extraction/recovery is authorized**.

### Round 2 — Build 723, source evidence checksum completeness

Code: 397fe20d32c9315e8a9744abe3d212456844b275 and witness seed 89476b8d73caa422e6cec0485cc83a4978201e6a; full regression commit cd25e4140975eae1967e17c3395a780198c54fb5. [CI run 37852488250](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37852488250) **SUCCESS** (WPF compile, SQLite smoke, 24k/96k corpus, portable publish/upload; artifact 11583370311). For the current schema v17, VerifyArchive now consults the independently integrity-checked embedded SQLite snapshot and compares nonblank SHA-256 values of Bills and Tariffs source documents with file hashes present in the corresponding archive document category. An otherwise valid ZIP and recomputed manifest **cannot** remove an original bill while leaving that SHA linked in utility_bill_document. The synthetic test seeds a real document SHA in a synthetic DB, intentionally strips that document from a copied ZIP and revises the manifest to look internally consistent; verification rejects it. **Limit:** missing/blank source SHA references and older schemas cannot be proved recoverable by this extra predicate. The previous original-path/source check during Create remains separate. This does not prove global document-to-DB transactional consistency under concurrent external file edits or provide a historic-schema adapter.

### Round 3 — Build 725, stale manual deletion selection

Code: dd7a3817a2837b43be8d1986d6c020a1237438f8; regression commit 6612455e61dcb811276698ba19cacc349c2e3659. [CI run 37852567249](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37852567249) **SUCCESS** (Windows build, SQLite smoke, synthetic corpus 24k/96k, portable publish/upload; artifact 11583046325). CompleteBackupInventoryService.DeleteOne already verifies that another full, currently available physical copy is valid, refuses legacy SQLite-only deletion, and changes no other destination. It now additionally rechecks selected filename, size and last-modified UTC against the originally selected inventory row immediately before File.Delete. A stale selection aborts with a request to refresh. Synthetic tests reject a forged stale size and an actual synthetic timestamp change; they then refresh inventory and permit the separately confirmed single-copy deletion while retaining the local original. **Limit:** file metadata comparison is not cryptographic identity and is not an atomic compare-and-delete against a concurrent adversary; real manually requested deletion still requires owner confirmation/UI QA and independent safeguards. No automatic pruning was added.

### Round 4 — Build 727, Windows-portable archive entry names

Code: b5ab3670f2f7e83fed8f02be904ee9be8677fc58; regression commit 4bf9e61167a4a7583806f5e0c1223761f6e4ca9b. [CI run 37852679518](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37852679518) **SUCCESS** (Windows WPF build, SQLite smoke, 24k/96k corpus, portable packaging/upload; artifact 11582263652). In addition to existing normalized ZIP path restrictions, VerifyArchive rejects individual path segments that Windows cannot safely create: trailing period/space, control/invalid filename characters, reserved device names (CON, PRN, AUX, NUL, COM1–9, LPT1–9, including extensions). Synthetic ZIP fixtures with recomputed manifest and plausible hashes containing CON.txt, trailing period and invalid question-mark filenames are rejected. **Limit:** this protects a future extractor's basic path namespace but it is not proof of full safe, selective recovery. No source file is extracted from a user archive into owner Data by these tests.

**Consolidation and next gates:** all four targeted code + smoke pairs pass their own definitive CI builds; intermediate CI numbers may exist because commits were incremental, and must not replace the definitive Build references. No owner per-build downloads or UI tests are requested. Future Phase 12 work should continue with version-aware recovery preview/import adapters only on specially marked isolated synthetic fixtures, independent safety/path testing including real fault injection, no active-DB restore or merge without the owner's separate authorization. The eventual single v0.11.0 QA remains pending, as does the evidence-conditional post-2026-10-12 Enel continuity review.

## Builds 728–729 — synthetic recovery gate and complete-ZIP entry-type hardening (2026-10-08)

**Status:** both **Windows CI PASS**; only targeted synthetic safety controls are accepted as CI-verified. There is NO owner Windows QA or active-DB restoration authorization. Work remains on Draft PR #1; `main` stays at `59120a630b0f56684ba7672d960673f5c5c1797b`.

- **Build 728:** source commit `c2ce9aff5af181252b282706b4708fe7b1229e23`, [GitHub Actions run 37861247784](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37861247784) **SUCCESS** (Windows WPF build, SQLite smoke, both synthetic performance corpora, portable publish and artifact upload). The read-only `IsolatedRecoveryPreviewService` no longer considers arbitrary OS-temp database paths acceptable. It now requires the same uniquely named, explicitly marked `SolarEnergyMonitorSmoke/<guid>` fixture as synthetic staged recovery and relationship audit. Shared fixture validation rejects missing and linked intermediate target directories, not just linked root/target files. Smoke regressions reject arbitrary temp paths and a lookalike unmarked fixture, then verify that the real synthetic preview remains read-only and functional. This is an accidental-misuse safeguard, NOT a general-purpose OS sandbox for untrusted callers.
- **Build 729:** source commit `1384ebe9f2059a91b05b9bda263cc9b270cea259`, [GitHub Actions run 37861459101](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37861459101) **SUCCESS** (Windows WPF build, SQLite smoke, both synthetic performance corpora, portable publish and artifact upload). `FullBackupService.VerifyArchive` checks POSIX file-type bits in ZIP entry external attributes: only regular files or entries without POSIX type metadata may pass. Symbolic-link and directory masquerades are rejected even when ZIP file names, payloads and manifests otherwise remain valid. Isolated synthetic tests forge these metadata attributes in cloned ZIPs and expect rejection. This prevents treating such entries as approved recoverable package files; no user documents were extracted.
- Optional live Enel/CNE probes were **SKIPPED**, not accepted as source validation. No changes to Enel phase-10 accounting, SQL energy units, schema v17, application version shown to users, owner's `Data` directory or manual provisional copy. No target-PC QA was requested for these two development builds, and neither is the planned single consolidated `v0.11.0` handoff.
- **Remaining gates unchanged:** reviewed historical-schema adapters, complete bill/meter/tariff/telemetry identity and dependency-safe staged recovery, fixture-only failure testing, coherent UX/performance completion, user Windows QA, and continuity review once the additional Enel evidence actually arrives on or after **2026-10-12**. No real import/restore, release or merge without explicit owner authorization.

## Build 730 — four safety advances in one CI run (2026-10-08)

**Windows CI PASS:** [run 37862071121](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37862071121), code/test HEAD `2d5d80a67820024773af3b3ea8b65438890fa16c`. Build, SQLite smoke (including the four new regression groups), 24k/96k synthetic performance corpora, portable win-x64 publish and artifact upload succeeded. Live Enel/CNE probes remain conditional and were not independently verified by these changes.

Four independent commits intentionally used `[skip ci]` where applicable so one integrated Build could verify all changes:

1. **Windows ZIP metadata:** `6f31d296dee3adcf1d088a3a95dd49cdbd356a43`. `FullBackupService.VerifyArchive` now refuses ZIP entries marked with Windows reparse/directory/device flags even if the POSIX type and manifest appear acceptable. Artificial ZIP metadata regressions for reparse points and directories were added; existing Unix symlink rejection retained.
2. **Recoverable Windows file names:** `eebea2cf318937eaa7972d2ba0c6de95aae20e9e`. Reject segments longer than 255 UTF-16 code units and reserved `CONIN$`, `CONOUT$`, plus COM/LPT names with legacy superscript digits. Artificial manifest-consistent ZIP regression inputs exercise new rejection conditions. No actual bill/tariff source PDF renamed.
3. **Safe legacy local deletion:** `0f25dc51c21f905eb63db940ba2ebf6491a8130a`. Historical `FullBackupService.DeleteSelectedLocal` delegates to `CompleteBackupInventoryService.DeleteOne`, ensuring current inventory selection, fresh metadata check, and proof of a distinct valid local complete package before a selected file is deleted. Synthetic smoke checks corrupt alternate refusal and precise deletion of a valid second local ZIP while keeping the original. No owner files touched.
4. **Honest recovery preview completeness:** `2d5d80a67820024773af3b3ea8b65438890fa16c`. If any normally supported recovery category cannot be compared because of schema/error/size conditions, preview status is `PARTIAL_PREVIEW`, with explicit incomplete disclaimer instead of `READ_ONLY_PREVIEW`. Artificial v17 target with intentionally renamed app-setting column checks this classification. Read-only status and unsupported bill/telemetry dependency restrictions remain in place.

**Interpretation and gates:** This is CI-only safety validation with synthetic fixtures, NOT Windows target-PC QA, full selective restore, schema compatibility acceptance, performance certification, completed v0.11.0 or permission to merge `main`. The owner's ~2 GB active SQLite and provisional manually copied `Data` folder remain out of scope. Main branch remains unchanged at `59120a630b0f56684ba7672d960673f5c5c1797b`. Preserve the single future consolidated user QA handoff and the evidence-conditioned 2026-10-12 Enel continuity review. Future restoration of real data still requires separate explicit owner authorization.

## Build 731 — six guarded recovery and inventory advances (2026-10-08, Chile)

**Verification:** [GitHub Actions Windows Build 731](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37862665927) **SUCCESS**, tested source `861b2976419925ffc50c64857db1e0f9145c13b4`. Release compilation, SQLite smoke (including new regression assertions), 24k/96k isolated synthetic performance corpora, portable win-x64 publish and artifact upload passed. Conditional live Enel/CNE tests were not part of the accepted evidence. All code remains in Draft PR #1; `main` stays at `59120a630b0f56684ba7672d960673f5c5c1797b`.

Six individually committed improvements were verified together to avoid separate user-facing builds:

1. **Shared target health preflight**, `9daff575e8ce712970e4225e604e26c50d8f5eb1`. Added `IsolatedRecoveryTargetHealth` to check actual SQLite `PRAGMA integrity_check` and `foreign_key_check` of a marked synthetic target, not merely its declared schema version. This does not enable real-database import.
2. **Read-only preview health**, `2538ebe23e75346ace9e6d77c556360cf79f8b51`. `IsolatedRecoveryPreviewService` rejects a schema-v17 synthetic target with deliberately orphaned `utility_bill_line` before reporting any counts. Synthetic fixture asserts the FK violation first.
3. **Additive staging health**, `6fb2c395aba7b2b1a57456c146574c98a991ccd7`. `IsolatedRecoveryAdditiveTestService` refuses this inconsistent target before creating a staged copy; regression checks that no new recovery stage survives. Existing stage-only, idempotent, rollback and real-data restrictions remain in force.
4. **Read-only bill/tariff relation health**, `d12d28a5c40d1b7c0169c34922bac0cf01d40a48`. `IsolatedRecoveryRelationAuditService` rejects the same bad synthetic FK before classifying source graphs. Existing portable-identity and relational-remap limitations are unchanged.
5. **Inventory identity validation**, `b23ccf020f76aa9c43796d124d9853dc5e0d5fbe`. `CompleteBackupInventoryService.ValidateCopy` now confirms that the selected row's display name matches its actual recognized physical ZIP filename; regression rejects a deliberately mismatched selection.
6. **Stale inventory verification protection**, `861b2976419925ffc50c64857db1e0f9145c13b4`. `Verify` now checks selected file name, size and modified-UTC both before and after costly ZIP/hash verification; the manual deletion path reuses the same helper. Synthetic regressions reject stale size or modified time before the UI can report PASS. These metadata checks are not atomic protection against concurrent adversaries; independent complete-ZIP hash verification still applies.

**Acceptance boundaries:** Six commits plus one CI validation are not full Phase-12 completion. No owner DB, owner provisional Data copy, actual source files or installed backups were read or modified; tests ran only with synthetic SQLite data. Restoration of linked bills/meter readings, tariff graph identity, historical schemas and telemetry remains unsupported pending reviewed adapters. No true real-data restore, final v0.11.0 user QA, release, or merge to main is authorized. Maintain a single future consolidated Windows QA handoff and review project continuity only if the anticipated additional Enel material is actually received on/after 2026-10-12.

## Build 732 — eight read-only recovery insights in one consolidated CI run (2026-10-08, Chile)

**CI accepted, not owner QA:** [Windows Build 732](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37863634309) **SUCCESS** for source SHA `142ca608f9bb01ef08845fca08c12275c0582b01`. Windows Release build, SQLite smoke/regressions, 24k/96k synthetic performance corpora and portable win-x64 artifact publication all succeeded. Live Enel/CNE probes remained opt-in and are NOT considered PASS. Commits 1–7 used `[skip ci]` to run one combined validation, while commit 8 triggered Build 732.

Eight improvements with primary source commits and the safety interpretation:

1. **Target-only identities**, `12799e230dfbba1894f4100e82a98239d0b75a23`: each supported recovery-preview category now reports count of rows present only in the synthetic target; these are never assumed disposable. Smoke includes destination-only setting.
2. **Source/target denominators**, `3dfab1b8abaa513b2f7b3b0fa9accbe98cbccc20`: category preview reports both source and target record counts, with regressions that source = missing + identical + conflicts and target = identical + conflicts + target-only.
3. **Value-free field differences**, `cb36bc8e37b315a02c3e00f8e875cee01c6a1c08`: preview counts which configured *field names* differ among overlapping identities, without exposing source/target values, paths or setting keys.
4. **Aggregate user-facing totals**, `607c019f233d551381d139ad30d2d2b9e79cb0c1`: preview exposes compared/blocked category counts and missing/identical/conflicting/target-only record totals. Unsupported relational types and failed supported categories are not silently counted as zero; regression covers `PARTIAL_PREVIEW`.
5. **Bill overlap despite meter blockers**, `fea8a0e0ff8277f748aebacb3c7b9ca2beb73f7b`: bill graph preview separately indicates that the original document digest already exists in the target even while bill remains `READING_REMAP_REQUIRED`. No surrogate-ID mapping or merge inference occurs.
6. **Tariff source-content comparison**, `ca28c76c188f911899bc90b54acd98c543ee38e1`: for the same source URL, a matching verified digest, conflicting PDF digest, or missing digest is distinguished with explicit review-only statuses. Synthetic tests exercise all three; neither publication nor tariff rate graph is imported.
7. **Incoming tariff corrections**, `a2a928452026b6f3105f98dfa1defd8a75f34f6f`: tariff graph now counts relations *targeting* a publication as well as outbound links. Incoming dependencies block naive standalone recovery; synthetic source correction-chain fixture exercises the incoming counter.
8. **Orphan/unlinked original bill documents**, `142ca608f9bb01ef08845fca08c12275c0582b01`: a new bounded, read-only inventory lists source documents not referenced by any bill, their archived evidence uniqueness and whether the target already has matching bytes. Smoke checks candidate versus already-in-target statuses; no document files are extracted or imported.

**Persistent limitations:** No live database or real customer documents touched. Recovering linked bills/readings, charge/evidence graph, tariff correction chains, telemetry and historical schemas remains **unimplemented/unauthorized** beyond preview and already existing synthetic settings-only staging. Neither this CI PASS nor the document candidate states approve a live restore, phase completion, production version bump or merge. The real ~2 GB SQLite database and provisional manual `Data` copy remain protected, main remains `59120a630b0f56684ba7672d960673f5c5c1797b` and PR #1 remains Draft. Keep one future owner Windows consolidated QA handoff, and re-evaluate Enel-related project continuity only when additional material is actually received at/after the planned 2026-10-12 checkpoint.

## Builds 733–734 — eight relationship-mapping diagnostic advances (2026-10-08 / 09 UTC)

**Consolidated result:** [Build 734 (run 37864351556)](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37864351556) **SUCCESS**, tested code SHA `ab7b38d806e8cf392f7fd9f4a0812c38ce1c9705`: Windows Release compile, synthetic SQLite smoke, 24k and 96k synthetic performance corpora, portable win-x64 publish and artifact upload all passed. These operations did not use the owner's ~2 GB DB or backup. Live Enel/CNE probes not exercised. Main remains `59120a630b0f56684ba7672d960673f5c5c1797b`; PR #1 remains Draft.

The first consolidated [Build 733 (run 37864196824)](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37864196824), for `e55c5d223998b8983c686535cae50c6a41829ebd`, **FAILED** at SQLite smoke due to a synthetic test expectation of only one portable tariff relation URL match. The fixture had two reciprocal links (main -> correction, correction -> main). Source code and compile passed; correction `ab7b38d806e8cf392f7fd9f4a0812c38ce1c9705` asserts both links and exact aggregate counts; rerun as Build 734 then succeeded. Do not misreport 733 as PASS.

**Eight functional diagnostic improvements across two code commits:**

1. **Shared original bill-document multiplicity**: for each source bill, report how many source bills reference its same original document; never assume a one-to-one source-document/bill relationship.
2. **Destination bill associations**: report how many destination bills are linked to original document bytes with the same SHA-256. This is an identity hint, not bill equivalence.
3. **Source from/to meter candidates**: separately count target meter rows matching the source timestamp AND numeric meter reading, without transferring surrogate IDs.
4. **Ambiguous timestamp-only meter candidates**: count target readings sharing each timestamp even when consumption differs. A synthetic duplicate timestamp with distinct kWh proves timestamp alone is insufficient; bill state remains `READING_REMAP_REQUIRED`.
5. **Cross-URL tariff-PDF SHA matches**: count all target tariff publications with the same PDF-content digest; test same-hash vs conflicting-hash cases without automatic merge.
6. **Unresolved outgoing correction references**: count explicit source relations lacking a linked `target_publication_id` so unreviewed external references are visible.
7. **Portable linked-tariff URL hints**: resolve each link's local source target ID inside the source snapshot, then count whether its publication URL appears in the destination. A reciprocal synthetic correction graph verifies both directions; no automatic foreign-key remap occurs.
8. **Read-only aggregate dependency totals**: expose bounded overall bill/tariff counts, meter-linked bills, target bill-document links, incoming/outgoing tariff links, unresolved links and URL hints for eventual user-facing diagnostic summaries.

**Source commits:** `7570a2b53957253f984894128f9bf4079f47ca71` (bill + meter advances 1–4, skipped intermediate CI), `e55c5d223998b8983c686535cae50c6a41829ebd` (tariff + totals advances 5–8; Build 733), and `ab7b38d806e8cf392f7fd9f4a0812c38ce1c9705` (synthetic test correction; Build 734 PASS).

**Boundaries:** All eight outputs are review-only **candidate counts**. Matching date, kWh, ZIP/PDF SHA or official URL is not a validated portable identity for bill, meter, tariff-rate or correction-graph records. No database writes to the active owner environment, file restoration, historical-schema adapter, graph rekey/import, release or merge is included. Existing synthetic settings-only additive stage remains separate. User Windows v0.11.0 consolidated QA remains required; any actual restore demands additional explicit approval. Enel continuity review remains conditional on receiving new material on or after the planned 2026-10-12 checkpoint.

## Build 735 — eight-stage synthetic selective-recovery plan (2026-10-08, Chile)

**CI:** [Build 735, run 37865044175](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37865044175) **SUCCESS** for code SHA `2130f302bbc72f07c8e07eae072d46499a32e142`. .NET 10 Windows compilation, SQLite smoke (including plan validity, rollback, freshness, conflict preservation), 24k/96k isolated synthetic performance corpora, portable Windows x64 publish and uploaded test artifact passed. This is CI-only, not owner Windows QA; optional live Enel/CNE probes were not run.

**Implementation:** Added `src/SolarOfThings.Core/Backup/IsolatedRecoveryPlanService.cs` and expanded `tools/SolarOfThings.SmokeTest/Program.cs`. The service is **synthetic fixture only**: the same marked-root gate applies, no API exists to activate or replace an installed database, and linked entities are never imported.

Eight ordered plan steps:
1. `VERIFIED_SOURCE_PACKAGE` — read-only verification gate using the existing complete-ZIP validator.
2. `SETTINGS` — the *only* executable stage, adding missing `app_setting` keys into a NEW synthetic staging database, while preserving conflicting and target-only values.
3. `BILL_SOURCE_DOCUMENTS` — evidence inventory/review only; no PDF extraction.
4. `BILLS_AND_CHARGES` — blocked: bill, line, field provenance and FK mapping unresolved.
5. `METER_READINGS` — blocked: timestamps/reading values do not prove portable identity.
6. `TARIFF_SOURCES` — review only: source URLs/PDF digests are evidence, not import approval.
7. `TARIFF_RELATIONS` — blocked: correction/supersession dependencies need reviewed FK mapping.
8. `ENERGY_TELEMETRY` — blocked: history and device identity adapters not available.

Additional lifecycle controls (all covered by SQLite smoke): the plan records SHA-256 of exact ZIP bytes plus a **WAL-visible, structured fingerprint** of `app_setting` in the marked synthetic target; reuses the existing preview and relation audit; rejects incomplete supported categories and non-v17 schemas; ties its plan token to the exact source/target paths; regenerates and compares preflight evidence before executing a staged settings copy; reconciles added/identical/conflicting counters against the plan; previews the staged output to require zero missing settings and preservation of conflicts and destination-only keys; rechecks original target fingerprint; deletes the generated stage on verification failure. Fault injection tests transaction rollback and generated-stage cleanup. Test also modifies a target setting while preserving row count and confirms the plan becomes stale; alternative source path is rejected.

**Limitations:** This is not a user-facing import wizard or a complete restored dataset. No historical-schema adapter, PDF/file importer, reading/bill/tariff graph remap, telemetry import or live restore exists here. Package/setting SHA-256 checks are plan freshness evidence, not a general concurrency guarantee for the full database. No active owner SQLite (~2 GB), provisional manual `Data` copy, or original utility documents were accessed or modified. `main` remains `59120a630b0f56684ba7672d960673f5c5c1797b`; PR #1 stays Draft. Do not merge, version-bump, release or ask for intermediate PC tests: one consolidated v0.11.0 QA handoff remains the owner-approved course. Enel 2026-10-12 review still depends on actual availability of new official material.

## Build 736 — eight synthetic recovery-plan integrity refinements (2026-10-08, Chile)

**Result:** [Build 736, Actions run 37866358215](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37866358215) **SUCCESS** at code commit `ceba6e43b213a8262722f7f7be9c9857dd02ecf5`. Windows Release build, synthetic SQLite smoke with new regression assertions, 24k/96k isolated synthetic performance corpora, portable win-x64 publish and artifact upload passed. Live Enel/CNE probes were not evaluated. No owner-PC QA or actual restore was performed.

**Eight improvements implemented and checked** in core commit `1aab07f010d24acbc7a9b60fe8f8dd7ceaca2684` and synthetic-regression commit `ceba6e43b213a8262722f7f7be9c9857dd02ecf5`:

1. **Full synthetic destination relational-state fingerprint.** Hash SQLite schema definitions and WAL-visible, bounded rows of all ordinary tables other than `app_setting`; record a separate SHA-256 in the plan. This is a synthetic-only bounded snapshot, not a tested large owner-DB benchmark.
2. **Fail-closed preflight for dependent changes.** Tie the plan token to the relational-state digest and re-create the plan before any stage. A change in `utility_meter_reading` with unchanged `app_setting` now expires the plan; covered by smoke.
3. **Post-stage no-write evidence for other tables.** Compare the non-settings digest of the fresh synthetic output with the original plan, in addition to rechecking the original destination; recovery must not alter bills, meter readings, tariff graphs, telemetry or schema.
4. **Target-setting metadata preservation.** Independently check every pre-existing destination `app_setting` key and its original value, NULL semantics and `updated_utc` on the staged copy, not just the key/value preview counters.
5. **Complete stage-policy binding.** Require exact equality of all eight stage descriptors before executing settings; reject an otherwise plausible forged plan whose blocked telemetry step is marked executable.
6. **Late post-copy failure injection and cleanup.** Deliberately fail after creation of an additive stage, then assert that no newly staged file survives and that the original target plan remains unchanged. Existing pre-commit rollback test remains in place.
7. **Non-duplicated original-bill evidence count.** Count distinct linked source document SHA-256s plus independently unlinked originals, rather than counting multiple bills using the same PDF as multiple independent documents. Synthetic graph checks two bills sharing one PDF plus one unlinked original as two documents.
8. **Tariff graph edge count once.** Count outbound relation records in the plan instead of summing the same internal graph edges again as incoming links; synthetic reciprocal correction fixture verifies three edges, not a doubled total.

**Limits and acceptance gates:** All methods continue to enforce marked synthetic fixture roots before touching a destination; only missing settings can be added to a **new disposable synthetic stage**. No linked bills, charges, readings, PDF files, tariff corrections, telemetry or historical-schema adapters were imported. Hash checks do not imply strong concurrent-change isolation for the full live database; the routine deliberately bounds its synthetic row scan. Source owner DB and provisional `Data` copy untouched; no live restore, PR merge, v0.11.0 release or final QA. Main remains at `59120a630b0f56684ba7672d960673f5c5c1797b` and PR #1 remains Draft. Preserve one future consolidated owner QA handoff.

**Overall progress estimate (non-normative):** approximately **65% of target v0.11.0 development** at this checkpoint, not a measured engineering KPI and NOT release-readiness or owner acceptance. Larger outstanding work includes versioned relational recovery adapters, final WPF screen/SQL usability and real-PC performance/backup QA. The 2026-10-12 Enel review remains conditional on receiving actual new official material.

## Builds 737–738 — Backup Manager verified-details and receipt UI (2026-10-08 Chile)

**Functional milestone:** Move Phase 12 from repeated internal preflight guards to a complete, user-oriented **inspect → verify → inspect verified contents → save evidence** path in the existing **Protección de datos / Data protection** backup inventory page. Draft PR #1 only; all code changes on `work/phase10-12-consolidated-20261007`; **no merge, actual restore, user-DB operation, deletion of owner files, final release or user-PC QA**.

**CI evidence:** [Build 737 (run 37867173639)](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37867173639) **SUCCESS**, source SHA `de5f7a50863dfe93d85b286b66d8df9fc93e56b0`; [Build 738 (run 37867426184)](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/37867426184) **SUCCESS**, final code SHA `13d92a0c8deaafa29c6c83b4aa95e00eff86a1ca`. Both complete .NET 10 WPF Windows Release compile, synthetic SQLite smoke including new manifest/detail/receipt regressions, 24k and 96k synthetic performance corpora, portable win-x64 publish and artifact upload. CI is NOT owner acceptance; WPF interaction/accessibility and real PC backup verification remain untested. Enel/CNE opt-in probes SKIPPED, not live-data validation.

**Eight practical improvements:**
1. **Trusted manifest details API**: `CompleteBackupInventoryService.VerifyDetails()` validates an actual selected recognized physical ZIP with all existing integrity, metadata and SHA-256 gates before returning data for UI. `Verify()` delegates to the same gate, so the deletion path retains its verified-last-copy rule.
2. **Actual categorized contents**: display database file, original bill documents and original tariff documents with each category's count and uncompressed bytes. Synthetic smoke checks counts sum to the verified manifest's content file count.
3. **Traceable backup identity**: display verified SHA-256, package UTC creation date, app/build and schema from the manifest, not guessed from filename or modified date.
4. **Portable ES/EN verification receipt**: save an explicit user-requested local TXT with verified ZIP SHA-256, version/build/revision, schema, physical size and category inventory. Does not include document bodies, secrets, app setting values or user credentials; says that verified ZIP is NOT a tested restore.
5. **Reverification before receipt**: the export handler re-verifies the exact selected package and refuses stale/mutated archive metadata rather than issuing an obsolete PASS.
6. **Selection-aware backup details and buttons**: new panel displays the selected physical copy's location/path/date/size, only enables verify/delete for COMPLETE packages and receipt for a selection verified in this view, while legacy SQLite remains informational.
7. **Truthful PASS/FAIL feedback and inventory refresh**: verified contents update only after PASS, failures mark the selected row FAIL and clear the verified details, while refreshing retains the same selected physical copy when present but resets stale PASS, requiring explicit fresh verification.
8. **Physical destination workflow and correct save errors**: user can open the selected file's containing folder. A text receipt save error is reported separately and does not falsely demote an otherwise correctly verified ZIP to FAIL (Build 738 follow-up fix).

**Changed files:** `src/SolarOfThings.Core/Backup/CompleteBackupInventoryService.cs`, `src/SolarOfThings.App/MainWindow.xaml`, `src/SolarOfThings.App/MainWindow.xaml.cs`, `src/SolarOfThings.App/Resources/Strings.es.xaml`, `src/SolarOfThings.App/Resources/Strings.en.xaml`, `tools/SolarOfThings.SmokeTest/Program.cs`. Tested backup receipt metadata and both localized safety notices; Windows WPF compile verified event handlers and XAML bindings.

**Limits:** This is an integrated verified-backup UI **source change**, not an owner-tested production workflow. It does not provide a restore wizard, actual selective import, historical schema adapters, comprehensive owner-PC benchmarking or release 0.11.0. The owner preserves the unverified manual `Data` copy, and the real `D:\SolarEnergyMonitorTest\Data\energy.db` (~2 GB) remains untouched. Future work should continue converting the approved functional UX and relational recovery into end-to-end capabilities; plan a single consolidated target-machine QA handoff. `main` remains `59120a630b0f56684ba7672d960673f5c5c1797b` and PR #1 remains Draft. Enel review on 2026-10-12 is conditional on actual new official material.

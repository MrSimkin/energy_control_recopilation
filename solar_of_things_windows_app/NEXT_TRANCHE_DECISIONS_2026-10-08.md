# Next consolidated tranche — owner decisions and open design discussions

Date: 2026-10-08 (Chile). Status: **DESIGN / PLANNING; NOT AN IMPLEMENTATION AUTHORIZATION**.
Owner discussion after Build 685. Authority for completed history: `PHASE_10_12_TRANCHE_STATUS.md`, `CONTINUITY_STATUS.md` (latest sections), `PHASE_11_SQL_GUIDE.md` and `BUILD_HANDOFF_RULE.md`.

## Immutable baseline

- Work branch: `work/phase10-12-consolidated-20261007`; PR #1 remains Draft and unmerged; no merge without owner approval; never develop directly on `main`.
- Build 684: **Phase 10 functional OWNER QA PASS**, excluding any independent certification of RED/ETR tariff identity.
- Build 685 (code `b31d97d94f0cba6a18f2ea7e4d0a225470bfac95`, workflow `37720787982`): Windows CI PASS, visual OWNER QA **not performed, postponed**. Its presentation-only corrections remain pending focused visual confirmation in a future consolidated owner-QA build. Live official-site probes were skipped, not PASS.
- Phase 11 and Phase 12: **PARTIAL**; no full owner acceptance.
- Protect active `D:\\SolarEnergyMonitorTest\\Data\\energy.db` and all real Data. No restore, active database replacement, destructive import, unauthorized migration, or automatic backup pruning.
- No new user QA while owner is unable to test. One coherent next test build, with ZIP directly handed off under `BUILD_HANDOFF_RULE.md`, not multiple tiny QA cycles.
- No code changes, new build, version change, schema mutation or production data operations are authorized by **this document**. Finish owner discussions and obtain implementation authorization separately.

## Decisions confirmed by owner on 2026-10-08

1. **SQL / Phase 11:** support both an application-integrated SQL query experience **and** external SQLite client access. Finish missing trustworthy reporting views. Keep physical semantics: sampled W are not integrated kWh; SQL views for integrated energy / monetary estimates require parity with statistical and billing engines. Integrated queries must not have write capability.
2. **Performance and technical debt:** make performance and responsiveness a first-class next-tranche workstream. Instrument startup and expensive screens; diagnose UI-thread blocking vs slow SQLite queries; remove duplicate loads, lazily load nonessential data, preserve safe asynchronous behavior, evaluate WPF virtualization, and add indexes only after query-plan/measurement evidence. Refactor when justified instead of layering workarounds; preserve the tested numerical/physical semantics.
3. **UI/UX:** ongoing improvements, including proactive assistant proposals for *specific menus and sections* through wireframes discussed with the owner before implementation. Avoid reimplementing existing tabs: Analysis, Battery and Grid & Utility already have task tabs. Maintain bilingual ES/EN, readable professional dashboard presentation, clear busy feedback, appropriate responsive layouts.
4. **Backups / Phase 12:** the owner does **not** consider the backup feature user-verified or complete and expects **more programming**. Existing automatic pre-migration/daily and manual SQLite native verified snapshot mechanisms and technical diagnostics are not equivalent to accepted user-facing workflows. Keep Phase 12 PARTIAL pending discussion, implementation and focused owner QA.
5. **QA cadence:** group performance, UI/UX, SQL and backup work into the **next coherent build to be tested** after discussions and implementation. Keep Build 685 QA deferred; do not claim acceptance via CI.
6. **Enel and release:** owner expects to provide additional data **2026-10-12** for further strengthening the Enel report; not yet received or validated. Do not rush v1.0 before that input and subsequent work/validation. Do not reopen accepted Build 684 Phase-10 baseline merely because this further work is pending.
7. **Versioning:** current app code displays `v0.10.0`; owner favors a noticeable version step in addition to ongoing independent CI build numbering. **Proposed, not yet approved:** `v0.11.0` for the next substantial tranche. Actual future build number must be determined by CI rather than promised as 686.

## Open questions — require owner discussion, not tacit approval

- **Backups: layperson workflow.** What should be easy to see and do under an ordinary “Datos y respaldos”/“Protección de datos” screen? Suggested design topics: verified snapshot list, creation, visible error/success, available recovery guidance, integrity checks, destination/space, retention. No deletion automation or restore without explicit separate safety decision.
- **Navigation/UI:** agree on wireframes for sidebar, Dashboard, Energy/Analysis, Grid & Utility, Data, SQL and Backups, including hierarchy and progressive disclosure. Do not mistake earlier UX proposals for current implementation gaps.
- **Performance criteria:** select objective startup and UI-interaction measurements with representative corpus size, avoid guessing performance improvements from index counts, and preserve responsive status indicators; implementation details delegated to developer once authorized.
- **Integrated SQL:** design exact UI, query-only scope, limits/cancellation, exported results and external access instructions before implementation.
- **Version step:** owner confirmation of semver candidate v0.11.0 and scope, without treating it as v1.0 readiness.
- **October 12 Enel input:** assess only once owner supplies the promised data; no claim about contents or readiness now.
- **Restore/retention:** still excluded from approved tranche. Any change of scope requires explicit design, authorization and non-destructive testing plan; no real DB restore as QA by default.

## Suggested tranche implementation order (proposal only)

1. Instrument/repair UI responsiveness and startup, retaining safe schema migration gates.
2. Focused UI/UX redesign via owner-approved wireframes and localized controls.
3. Complete backup management UX and safety features after owner discussion.
4. Integrated read-only SQL explorer, external-access workflow and source-faithful views.
5. Integrate additional Enel-report work when the October 12 data and requirements are available.
6. One consolidated Windows CI build, static/smoke regression evidence and later minimal, owner-timed target-PC QA by functional area.
7. Release candidate / installer / v1.0 gates remain later, without prematurely closing Phases 11/12.

## Decision status

Only items under **Decisions confirmed** and subsequent sections explicitly marked **APPROVED** are accepted requirements/constraints. Wireframes, backups workflow, restoration policy, precise version, implementation steps and dates remain **OPEN**. This is a continuity checkpoint, not QA evidence or a release authorization.

## Backup-design decision B — owner confirmation (2026-10-08)

**APPROVED PRODUCT REQUIREMENT:** Owner selects backup policy B: **automatic local backups plus a user-configurable second backup destination, with visible warnings on failures**. The design should be simple for a nontechnical user; avoid presenting backup management only inside developer diagnostics. This confirmation is NOT authorization to implement yet.

**Current evidence/limits (read-only code review):** `DatabaseBackupService` already supports native SQLite consistent snapshots, automatic daily-if-due creation, schema-migration preflight backups, manual creation, verification and manifest. `AppPaths` uses `Data/Backups` under the shared QA directory. There is no demonstrated configurable secondary-copy service nor user-facing list/management workflow; do not report them as implemented. The current DB-only snapshot does not itself guarantee protection of external `Data/Tariffs`, `Data/Bills`, settings, logs, imported documents, etc.

**Initial engineering recommendations (PROPOSED, not yet approved):**
- Local verified snapshot first, secondary copy of a *completed* snapshot plus manifest only afterward; verify destination copy by checksum and integrity. Never raw-copy active SQLite plus WAL as the backup mechanism.
- If secondary USB/network folder is unavailable, normal usage remains possible: preserve verified local backups, show a clear persistent nonblocking warning, and retry transfer on subsequent availability; do not silently report both copies as verified.
- Help user choose an **independent physical storage device**; a second folder/partition on the same failing disk does not provide protection against disk loss. No hard-coded secondary destination, cloud service, or credentials.
- UI draft: `Protección de datos` showing last successful local and secondary backups, pending/error status, destination selection, manual verified backup, and open backup folder.
- Avoid unlimited backup growth and intrusive startup background work: inventory disk-space needs, propose non-destructive safeguards. **Automatic retention/deletion is still prohibited** pending owner approval and a tested retention policy.
- Backup restore, destructive imports and active database replacement remain **OUT OF SCOPE** unless owner separately authorizes a new safety-tested design.

**Scope decision:** resolved in the subsequent approved decision for a full recoverable package. Exact safe file inventory/exclusions require engineering design. Next discuss cadence/retention and recovery separately. UI wireframes follow design decisions.

No code, schema, branch merge, build, QA, destructive operations, restore or numeric/report changes authorized by this decision.

## Backup scope decision — owner confirmation (2026-10-08)

**APPROVED PRODUCT REQUIREMENT:** Owner approves **combined recovery-capable** backups, with both:
- **Fast, regular SQLite backups** using the safe native snapshot approach already implemented as a baseline.
- **Full, recoverable application-data backup packages** containing SQLite **and** the necessary external source documents and configuration to restore the useful local working environment (e.g. referenced original Enel bills, official tariff documents, required settings). Scope must be based on an explicit inventory; merely copying `energy.db` is insufficient.
- Both fit the already approved policy of **automatic local copies plus a user-configurable second destination and clear failure notices**.

**Safety and implementation considerations, subject to design:**
- Consistency between the SQLite snapshot and separately stored documents is essential: use controlled staging, validated file hashes and a manifest; distinguish missing source files from successful recoverability. No claim of full coverage without a validated inventory.
- Do not pack passwords, reusable sessions/tokens, secrets or arbitrary diagnostics indiscriminately into a recoverable archive. Plan safe treatment of portable settings and Windows-bound encrypted credentials separately.
- Performance: avoid blocking launch or foreground UI; avoid copying unchanged large sources needlessly; design bounded storage accounting before implementation.
- **Not yet decided:** exact cadence for fast versus full packages, retention and any deletion policy, automated copy retry semantics, UI wireframes, recovery/restore authorization or procedure, and detailed directory inventory.
- No automatic backup deletion, real active-DB restore, destructive operations, merges, new builds or code changes are authorized by this documentation decision.

**Next owner discussion:** choose default backup frequencies and safe retention approach. Restore/recovery design to be discussed independently before any authorization.

## Owner update — full backup weekly reminder and deletable backup inventory (2026-10-08)

**APPROVED — supersedes any earlier suggested automatic weekly full backup:**
- A **full recoverable backup is offered through a once-per-week reminder**, not performed unconditionally on a weekly timer.
- The reminder must provide user choices to **run now**, **skip this occurrence** or **postpone/snooze** without blocking use of the application. Exact postpone durations are a minor UX default to settle during design.
- Provide a **human-readable recognized inventory/list of available backups**, including local and configured secondary-destination copies (when accessible), type (fast SQLite versus full package), date, size, verification status and location. Do not equate 'previously verified' with a fresh recheck.
- Owner explicitly requests that listed backups be **manually deletable**, not merely viewable. Manual deletion must be deliberately invoked, indicate which specific local/secondary files will be removed, and request confirmation; use safety guards around protecting the last known-valid recovery point. Details of whether mirrors are deleted together, safeguards for the final viable backup, and abandoned/missing manifests remain to design.
- **No automatic pruning/retention deletion is approved.** Deletion functionality requires deliberate implementation and separate focused tests with synthetic backups first. Do not touch real QA database/backup files during development.
- This does not grant permission for restore, live DB replacement, code changes, merges or builds yet.

**Open frequency question:** owner described prior cadence as too much but explicitly specified only the weekly full-backup reminder. **Fast SQLite cadence remains undecided**; current implementation automatically creates a SQLite backup if none of kind automatic is less than 24 hours old, plus a pre-migration safety copy. Confirm whether that background daily behavior remains acceptable before changing it.

**Implementation constraint:** avoid costly filesystem scans or snapshot creation on UI thread or in startup-critical path. A disconnected second destination should not trigger UI freezes; report its last verified state honestly.

## Backup policy supersession — full backups only + selective recovery (owner, 2026-10-08)

**LATEST APPROVED PRODUCT DECISION — SUPERSEDES earlier combined SQLite-fast + full-backup recommendations:**
- The actual database is already almost **2 GB** and contains **less than one year** of data according to the owner. Daily SQLite snapshotting is unacceptable due to backup size/storage growth.
- **ONLY COMPLETE RECOVERABLE BACKUPS** are to be offered/retained by the redesigned user backup policy: one verified recovery-capable package including a consistent SQLite snapshot AND necessary external source documents / configurations with a manifest. **No separate periodic SQLite-only snapshots** in the next design.
- Creation frequency is deliberately **limited**, by owner choice: retain the already approved **weekly skippable/postponable reminder**, with **Run now / Postpone / Skip this week**, rather than automatic weekly full backup execution. Allow manual full backup. Do not invent an additional fixed automatic daily/weekly schedule.
- The previously approved **secondary configurable destination and failure notices** apply to these full packages, with safe asynchronous copy and verification; do not force copying while removable destination is disconnected.
- The previously approved **recognized, human-readable, manually deletable inventory** applies only to full packages going forward; show/handle legacy existing SQLite-only snapshots as legacy, rather than mislabeling them complete or deleting them automatically. Any deletion remains explicit, confirmed and guarded; automatic cleanup remains UNAUTHORIZED.
- **SELECTIVE RESTORATION REQUIRED BY PRODUCT DESIGN:** when recovering from a complete package, the owner chooses **which categories of information to recover** rather than necessarily replacing everything. Provide simple per-category preview, compatibility/dependency checks, reversible staging and transaction-safe import/merge procedures. Never describe selective recovery as replacing arbitrary SQLite tables/files: internally coupled relational data (e.g. meter readings + bill/lines, tariff evidence + documents) must remain consistent. If safe partial recovery is impossible for a category, explain that and offer a safer isolated/full recovery route.
- **Full recovery** may also be discussed, but no real-DB replacement/destructive restore operation is yet authorized for implementation or QA.

**Current Build 685 legacy behavior / resolved policy:** `DatabaseBackupService.CreateAutomaticBackupIfDue()` currently creates daily SQLite-only snapshots, and `App.OnStartup` currently creates a mandatory SQLite-only pre-migration snapshot. These are facts about existing code, not the approved future policy. The owner has now resolved the pre-migration decision: require a **complete, verified** pre-migration backup and allow postponing the update rather than attempting an unprotected schema migration. See the subsequent approval section. Preserve current protections until the verified replacement is implemented.

**Performance/storage engineering note:** evaluate storage utilization before full capture (DB almost 2 GB plus external files), time/space cost, off-UI capture, consistent file/data snapshot, manifest hashes, duplicate destination copies, and failure handling. Do not promise small files or a compression ratio.

**State:** Design approval only. No implementation, new build, real files copied/deleted, schema changes, restore execution or merge.

## Owner confirms mandatory full pre-migration snapshot and schema-aware recovery (2026-10-08)

**APPROVED PRODUCT DECISIONS:**
1. **A verified COMPLETE recoverable backup is mandatory before any application update/migration that changes the SQLite schema.** Prompt clearly and allow the owner to postpone the update. If owner postpones, backup fails, is incomplete, or cannot be verified, **NO database migration proceeds**. The existing working version and original data must remain safe. The update/launcher design must not strand the user in a new executable incompatible with the old schema: keep or offer the prior executable/install state, and avoid forcing incompatible open/migration. Backup on this exceptional event is distinct from the ordinary skippable weekly reminder.
2. **Recovery/import MUST identify the version that CREATED the backup and use its matching schema/format when reading and recovering data.** Do not assume current app schema when restoring old snapshots. At minimum retain in every new manifest: application semantic version, CI build number and source revision, SQLite schema version, independent backup/package format version, UTC creation timestamp, source data identity and relevant station/device identifiers, categories/files included, integrity hashes and completeness/verification results. Validate actual embedded DB schema independently rather than trusting manifest text alone. Format version != SQLite schema version != application/build version.
3. Build a **version-aware recovery pipeline**: identify backup/legacy type, verify manifest/hash/SQLite integrity, select a compatible importer/adapter, open original snapshot read-only in isolation, apply required supported stepwise conversions ONLY to a staging copy (never rewrite the archive/source), resolve selective-category dependencies and conflicts, preview changes, and request explicit approval before any activation/import. Preserve accurate original timestamps, units, UTC/local dates, source evidence, relational identifiers, uncertainty, and raw records.
4. **Fail closed and explain** if a snapshot is from a newer unsupported format/schema, if required files are missing, or if a safe conversion path is unavailable. Do NOT silently downgrade schemas, erase newer data, blindly overwrite tables or certify an older package as complete if it was database-only. Existing SQLite-only historic backups remain identifiable as **legacy database-only**, with limited recovery possibilities, not relabeled full recoverable.
5. Restore/recovery remains a **design requirement, not operational authorization** to restore the user's active `energy.db`; until separately permitted and non-destructively validated on synthetic fixtures, do not implement or execute real-DB replacement. No change to current Build 685 has occurred.

**Selective recovery policy:** resolved by the owner as **A: add missing data and surface conflicts; never replace whole categories**. See the subsequent approval. Exact category-specific identity, dependency, and merge rules still require careful design.

## Selective recovery policy A — owner approved (2026-10-08)

**APPROVED (supersedes suggested replacement modes):** Recovery from recognized, version-aware complete backups is **non-destructive and additive by default**: import **missing information only**, and **display conflicts** where backup and active data overlap but disagree. No option to bulk replace complete categories is authorized, including as an advanced mode.

**Required semantics and safeguards:**
- Show per-category dry-run preview: genuinely absent records, already-present equivalents, discrepancies/conflicts, unsupported dependencies and expected file additions; avoid an automatic merge based only on timestamps.
- Detect same-identity/same-content items and skip duplicates idempotently. When two records conflict, **do not overwrite the live record automatically**; preserve both sources as evidence in an isolated preview/audit trail and offer safe, explicit next steps for owner review. No destructive resolution is implied by approval A.
- Recover related data coherently (meter reading, bill and lines, original PDF, tariff source metadata and document, or linked metric/provenance groups) without breaking foreign keys, chronology, source identity, raw evidence, quality/uncertainty semantics or installed-version compatibility.
- Stage and validate in isolation, then apply only verified additions with transaction/rollback safety. Never assume changing isolated SQLite tables independently is safe, and never touch active `energy.db` during the design phase.
- If an imported backup is too old, too new or missing conversion rules, clearly explain limitations. Legacy DB-only backups cannot claim to restore missing external evidence.
- Respect the user-approved *full backups only*, weekly skippable reminder, second user-configurable destination, manually deletable recognized inventory with confirmation and mandatory verified **complete** backup before a schema-changing update.
- Design approved; **no code implementation, active DB restore, destructive operations, branch merge or build authorized yet.**

**Next product discussion:** menu/navigation structure and screen-level UI/UX wireframes for Data Protection, SQL explorer and remaining application pages. The app currently has Dashboard, Analysis, Battery, Grid/Utility, Reports, Data, Diagnostics, Help, Settings and About; Analysis, Battery and Grid/Utility already use tabs, so refinement must preserve functionality and avoid duplicating existing structure.

## Owner approves grouped sidebar wireframe (2026-10-08)

**APPROVED UX DIRECTION:** Owner likes and accepts the proposed grouped left navigation. Preserve all existing workflows and improve their location:
- **INICIO:** Panel principal (Dashboard).
- **ENERGÍA:** Análisis de energía, Batería, Red eléctrica y Enel.
- **INFORMES:** Informes y exportaciones.
- **DATOS Y HERRAMIENTAS:** Datos recopilados, Protección de datos (first-class section instead of only Diagnostics), Explorador SQL (first-class section; integrated read-only querying alongside external SQLite access).
- **SISTEMA Y AYUDA:** Configuración, Diagnósticos, Ayuda, Acerca de.

Keep existing Analysis, Battery, and Grid & Utility task tabs. Localization ES/EN, a clear active selection, keyboard access, narrow-window behavior and no loss of functionality are required. Exact copy/iconography/layout refinement stays subject to wireframe review, not blind implementation.

**UX proposals to discuss NEXT (NOT YET APPROVED):**
1. **Dashboard**: place a very light/fast-rendering, immediately responsive status and freshness header with Update Data; keep four existing last-observed PV/house/battery/grid cards; plainly distinguish observed instant W/kW from latest stored full/partial-day kWh; put household operating explanation and data coverage below; defer expensive daily computations and optional panels until after first paint; avoid duplicated startup reload and show 'loading' without fabricating zero.
2. **Grid & Enel**: preserve six implemented tabs (Overview, Readings, Compare, Bills, Audit, Tariffs). Refine Overview into clear actionable summary; Bills into list + selected-bill details; Audit into sequential evidence and source/uncertainty sections, with actions for missing inputs; avoid long permanently expanded forms or materializing unsupported RED/ETR applicability.
3. **Reports**: separate choosing a report/template and date range from export actions, progress and output; retained report semantics must be preserved.
4. Performance: measure startup and page latency before adding indexes; never use UI decoration to mask a blocked UI thread.

The owner requested more concrete per-screen wireframes; these are proposals to show, revise and approve in discussion. **Only grouped-sidebar direction has been approved** in this section.

**No development/build/real-data changes authorized; work branch PR stays draft.**

## Owner screen-design decisions: Dashboard and Reports approved; Grid/Enel needs visual clarification (2026-10-08)

**APPROVED — proposal 1, Dashboard:** Preserve the existing four instant observed-value cards (PV, house load, battery, grid import); promptly display last observation timestamp and freshness; show plain-language current operating explanation; put last available stored-day energy (kWh) with explicit date/coverage below. Keep charts/long analysis in the dedicated Analysis page. Minimize first-paint latency by deferring optional/heavy reads, avoid duplicate startup computations, provide honest loading states instead of zero or fabricated measurements. Preserve live vs stored distinction, W/kW vs integrated kWh and explicit uncertainty.

**APPROVED — proposal 3, Reports:** Separate report/template selection and date-range/presentation options from preview/coverage and PDF/Excel export. Keep all existing report types, formulas, presets and provenance; show staged async progress, persistent completion/failure state and output location. Avoid freezing the GUI during export.

**APPROVED — proposal 4, transversal product UX/performance criteria:** Lazy/nonblocking screen loading; visible local/global progress and recoverable errors; one clear primary action per task; progressive disclosure for advanced diagnostic details; no loss of controls, mathematical validity, or source traceability; measured query/UI responsiveness and indexing/refactoring based on actual evidence. Preserve bilingual ES/EN and keyboard navigation.

**RESOLVED, APPROVED LATER — proposal 2, Grid & Enel:** Following a concrete interactive six-tab example and the Boletas→Auditoría journey, the owner approved this design direction. See subsequent dated approval section. The initial difficulty visualizing it has been resolved.

### Grid/Enel clarification concept to show (proposal, awaiting owner feedback)

Existing six sub-tabs stay: **Resumen / Lecturas / Comparar lecturas / Boletas / Auditoría / Tarifas**. Demonstrate three distinct example screens rather than abstract cards:
- **Resumen** = read-only status and direct shortcuts to latest boleta, missing evidence, tariff state, and comparison; no huge edit form.
- **Boletas** = visible list of saved bills on the left, selected real bill details on right with subtabs `Resumen / Lecturas / Cargos`, plus `Auditar esta boleta`, `Ver PDF` and `Editar` distinct actions.
- **Auditoría** = selected bill pinned at top, then explicit sections in order `1. Boleta original`, `2. Energía (Enel vs inverter + coverage)`, `3. Cargos/tarifa (verified vs ambiguous)`, `4. Conclusión + PDF/anexo`. Show source/RED-ETR uncertainty instead of treating mathematical matching as tariff certification; do not claim a bill discrepancy proves distributor error.
- **Lecturas** is capture/manage readings; **Comparar** is freely selecting two meter observations and matching inverter interval, not the official bill audit; **Tarifas** is official evidence acquisition/validity and diagnostic state.
- Show the interaction sequence `Resumen → Boletas → seleccionar una → Auditar` using one concrete example and clear navigation. Do not duplicate already-implemented sub-tabs or eliminate existing features.

These Grid/Enel details were subsequently **approved by the owner** as the next-tranche UI/UX direction. **No code, new build, real data operations, backup restore or main merge authorized merely by this approval.**

## Grid & Enel concrete tabbed wireframe APPROVED (2026-10-08)

**OWNER APPROVAL:** After viewing a navigable conceptual wireframe with the six existing tabs and the concrete `Resumen → Boletas → seleccionar boleta → Auditar esta boleta` journey, the owner explicitly replied **"de acuerdo"**. This resolves the prior pending proposal 2.

**Approved screen-level direction:**
- Preserve six subtabs **Resumen, Lecturas, Comparar, Boletas, Auditoría, Tarifas**, their existing behavior and source-specific semantics; refactor presentation without erasing features.
- **Resumen**: read-only hub with latest bill, audit status, evidence/readings and tariff shortcuts, avoiding giant editing forms.
- **Lecturas**: list and detail/editor for meter readings, distinguishing Enel date-only readings from exact-time personal measurements.
- **Comparar**: two chosen readings, equivalent inverter import, gaps/coverage and a report independent from official bill audit.
- **Boletas**: saved-bills list (left) + selected-bill details (right), with detail subtabs **Resumen / Lecturas / Cargos** and separate actions to view original PDF, edit fields, and `Auditar esta boleta`. Responsively stack panes where necessary.
- **Auditoría**: keep selected bill context visible and organize evidence in four logical sections: **1. Original bill; 2. Energy—Enel vs inverter and coverage; 3. Tariff and item-by-item reconstruction, including explicit verified vs ambiguous RED/ETR applicability; 4. Conclusions, PDF and technical annex**. No unproven metrological certification or tariff-applicability claims.
- **Tarifas**: official document discovery/validity, original-source evidence, parser status and missing-applicability diagnostics, independent of bill audit conclusions.
- Honor the transversal previously approved goals: nonblocking UI, measured performance, keyboard accessible navigation, ES/EN localization, preserved source traceability and calculations.

**Status:** All four specific UX proposals (Dashboard, Grid/Enel, Reports, transversal criteria) now have owner **design approval**. Detailed implementation remains future work; version bump, build, code changes, real database actions and merge are **NOT** yet authorized.

**Next review:** Design `Protección de datos` screen and `Explorador SQL` screen using equally concrete wireframes, before commencing any new code work.

## Backup inventory: individual-copy deletion APPROVED (2026-10-08)

**OWNER APPROVES OPTION A:** The user deletes **one selected physical copy at a time**, NOT every mirror/destination linked to the same logical full backup. Deleting the local copy must not automatically delete a secondary external copy, and vice versa. The backup list should distinguish logical backup identity, physical copy/destination and most recent integrity verification; it must plainly show the post-deletion protection status.

**Safety guards required by owner:**
- No deleting the **last known-valid available complete recovery copy**; block that action, explain why, and allow the owner to create and verify a replacement first. Do not assume an unplugged disk still has a healthy copy just because it was formerly verified; label unavailable/last-seen status honestly. Never erase a verified copy solely because a secondary destination is offline.
- Present specific date/identity, size, location, type, verification status and only the selected target in a confirmation dialog; deletion must be deliberate, with success/failure recorded. Never cascade across separate locations or silently remove unrelated files.
- Only recognized, safely identified backup-package files and associated manifests may be managed. Validate normalized paths and prevent escape from configured backup directories; handle partial packages, permission failures and interrupted deletes conservatively. Treat legacy SQLite-only backups explicitly as **legacy**, not as full recovery points.
- No automated retention purge, no mass deletion by default, no deletion of the active DB or user-supplied unrelated files. Development and QA deletion testing must use artificial files, not the owner's real backups.
- This is an **approved design requirement**, not authorization to program, run backups, delete owner files, restore, merge or issue a build.

**Next UX review:** Integrated read-only SQL explorer and its external-client workflow, with a concrete preview; performance and SQL safety guards remain essential given a nearly 2 GB database.

## Integrated SQL explorer UI APPROVED with Excel and configurable shortcuts (owner, 2026-10-08)

**APPROVED UX + functional requirements:**
- Integrated **read-only** SQL explorer as a first-class sidebar section, with schema tree/catalog and read-only view descriptions, central **code-editor/console-style SQL workspace**, results/diagnostics pane, and an explicit safe workflow for external SQLite tools accessing a consistent user-requested snapshot or safe read-only connection.
- Export query results to **both CSV and native Excel .xlsx** (not a CSV renamed as XLSX), with typed/named columns, sensible date-time and unit metadata, streamed/bounded exports that do not freeze the WPF UI, explicit limits and warnings for very large datasets, and spreadsheet-formula injection defense. Preserve NULL distinct from numeric zero, UTC/local dates, and measured W vs derived kWh. Do not claim to export arbitrary sensitive raw API JSON without safeguards.
- Editor should look and behave like a real **SQL console/code editor**, not a plain multiline text box: **line-number gutter** (optionally column position in status bar), monospace typeface, SQL syntax highlighting, indentation, selection, scroll/resize, find, bracket matching if supported, and visible line/column in the editor status area.
- **Configurable keyboard shortcuts** in a discoverable settings/keybindings screen. Suggested defaults to discuss/implement: `Ctrl+Enter` execute current statement/selection, `F5` execute, `Esc` cancel in-progress query, `Ctrl+Space` autocomplete SQL/schema names, `Ctrl+F` find, `Ctrl+/` toggle line comment, `Ctrl+S` save SQL script to a user-chosen file if supported, plus an export shortcut. Detect shortcut conflicts, offer reset-to-default and preserve normal text-edit and OS accessibility bindings; persist mappings outside sensitive DB recovery artifacts only if compatible with user settings design.
- **Syntax diagnostics with accurate location wherever possible:** highlight bad token/identifier with error underline and gutter marker; panel must show meaningful message, line/column or at minimum best supported token span, context and a click-to-jump action. Distinguish parser syntax errors, schema/unknown-column errors, execution failure, query cancellation and timeout. Do not manufacture pinpoint line/column where SQLite/driver supplies only a message or `near token`; add a robust SQL parser/lexer and source-offset mapping or equivalent validation layer and indicate approximation/unknown location when appropriate. No false claim of IDE-level syntax resolution from SQLite alone.
- Execution safety: open SQLite in **read-only** mode plus enforce query-only/authorizer controls, allow only explicitly vetted read-only single statements, no DML/DDL/PRAGMA modification/ATTACH/extension loading/multiple hidden statements, no unsafe functions; bounded query rows and elapsed time, real cancellation, async execution off UI thread, paginated/virtualized results. Safe defaults should account for the owner's nearly 2-GB live database, and explicit user-requested CSV/XLSX export must not silently export only preview rows.
- Reflect existing schema v17 `reporting_*` and `data_quality_summary` views. Additional genuine W→kWh integrated-energy views and bill-estimate views stay contingent on demonstrated fidelity with application interval/tariff calculations; do not mislabel W sample aggregation as kWh.
- External-client access should not generate periodic SQLite-only snapshots, contrary to approved **full-backups-only** policy; explicit disposable read-only analytical copies are different from the recognized complete-backup inventory and must be labeled/managed as such.

**Design status:** Owner approves overall explorer and enhancements (XLSX, keyboard bindings, console line-number display, and source-located syntax errors). Choice of specific WPF editor component, parser library, default shortcuts, limits, export schema, and error-location fidelity are implementation design/QA tasks. This is **not authorization to modify code, build, merge, restore, or alter the live DB**.

**Next UI/UX topics:** Analysis and Battery screen refinements, plus Data screen and settings/diagnostics if needed; once screen designs and remaining design questions are closed, assemble a single coherent next build for owner QA.

## Analysis and Battery screen wireframes APPROVED (owner, 2026-10-08)

**APPROVED — user replied "sí" to both concrete interactive screen mockups.** Do not mistake this design approval for implementation permission.

### Análisis de energía
- Keep three existing tabs **Resumen, Gráficos, Detalle** and **one shared date-range/preset filter** across them, including currently supported quick ranges and custom dates.
- **Resumen**: observed/derived solar/house/grid/battery kWh summaries with visible temporal coverage, missing-interval and uncertainty states; existing solar/battery/grid operating times, unknown time, recovery/emergency time and state changes remain available rather than disappearing.
- **Gráficos**: retain current energy/solar/house/grid plot and battery plot with interactive toggles, hover, zoom/reset; keep correct measured-vs-derived energy semantics, separate kW instantaneous from integrated kWh, and do not replace actual calculations with mockup numbers.
- **Detalle**: preserve all existing Hour/Day/Week/Month/Year aggregation and columns (solar, house, grid, battery in/out, SOC average/min/max/end, coverage), readable/resizable/virtualized tables with horizontal scrolling and data provenance when drilling down.
- Load heavy ranges/plots lazily off UI thread, with clear progress/cancel where meaningful and no false zeros. Preserve prior accepted math/gap-handling/timezone behavior.

### Batería
- Keep two existing tabs **Resumen and Técnico**.
- **Resumen**: prominent latest SOC and observation freshness, estimated stored energy, ordinary-use energy, emergency reserve, configured capacity, activity, protected floor/threshold notes. Clearly mark manually configured thresholds as configuration, not certified hardware values. Do not claim autonomy in hours without validated load methodology.
- **Técnico**: voltage (V), charge/discharge current (A), power (kW), raw-source freshness/quality, device/config context; absent measurements must show unavailable rather than zero. Preserve existing expander/details and tooltips.
- Uniform responsive cards, clean hierarchy, async/lazy heavy reads and honest loading states. These reorganizations do not change actual formula semantics.

**Current source review**: `MainWindow.xaml` already has shared Analysis quick-range selector/date pickers, Analysis Summary/Charts/Detail tabs, power/energy/coverage/status indicators, WpfPlot chart controls and aggregation table, Battery Summary/Technical tabs with existing SOC, remaining/ordinary/emergency/reserve and technical readings. The next tranche should improve layout/handling, **not rebuild already implemented functionality**.

**NEXT PROPOSALS ONLY (not yet approved):** improve `Datos recopilados` (current `Cobertura` and `Importar` tabs); reorganize `Configuración` (current connection/session, export folder, manual SQLite backup controls) to avoid duplicating the approved first-class `Protección de datos`; keep `Diagnósticos` an advanced troubleshooting tool with source/operational evidence and safe export, not the home of routine backup operations. Present clear concrete wireframes for user review.

**Status:** Documentation-only design approval; no code or build, no restore, no merge, no action on live QA SQLite.

## Owner approves Data, Settings and Diagnostics wireframes (2026-10-08)

**APPROVED UX DESIGN:** Owner replied **"sí"** to all three proposed page wireframes, completing the requested broad navigation/screen UX review. No programming or build authorization is implied.

### Datos recopilados
- Keep existing **Cobertura** and **Importar / Recopilación** tabs; summarize first/last stored observations, reviewed/problem days, completeness/partial/unavailable days, installation and configuration status, with honest uncertainty and provenance.
- Separate coverage reporting from user-triggered import/collection; show nonblocking progress, current period and outcomes, with clear errors. Do not substitute restoration-from-backup for normal data collection; recovery lives in Protección de datos.
- Preserve all prior ingestion behaviors and safety checks.

### Configuración
- Organize settings by **Conexión al inversor** (secure session and auto-connect), **Exportaciones** (destination folder), **Atajos de teclado** (configurable SQL/editor and other valid shortcut bindings), **Idioma y apariencia** (existing ES/EN and compatible presentation settings), plus a simple link to **Protección de datos**.
- Do **not** duplicate full backup management in Settings. The current Settings manual SQLite-only backup action must be rerouted/replaced coherently as part of the approved complete-backups-only implementation; no accidental creation of database-only 'complete' backups. Preserve secure credential controls; do not imply existing UI implements theme settings if not verified.

### Diagnósticos
- Position as **advanced, opt-in investigation**, with system/database/source freshness, connection and sync checks, current diagnostics and evidence/export/log controls, readable statuses, sanitized diagnostics bundles and provenance. Preserve existing investigation tools (general run, capture state/flow, configuration cache/read, grid-energy diagnostics, phase evidence, report/log export).
- The existing Developer Diagnostics manual database-only backup control must not remain a confusing or contradictory parallel end-user backup path after migration to the approved `Protección de datos` page.
- Avoid expensive all-check scans and technically detailed fields on routine startup; execute diagnostic actions explicitly and asynchronously with accurate status/error displays.

### UX approval boundaries
- Previous approvals also cover grouped left sidebar; Dashboard; Analysis; Battery; Grid & Utility six-tab journey; Reports; Protection of Data's backup policy + per-copy deletion requirements; integrated read-only SQL explorer with native XLSX export, keyboard mapping, numbered editor and location-aware errors; transversal responsive/performance principles.
- The full `Protección de datos` layout was demonstrated, and specific backup behavior was approved; implementation choices/restore safety specifics still require a final engineering gate. Do not falsely claim real database restoration is authorized or tested.
- **All three wireframes are approved as design direction only**. No schema/data changes, branch merge, app code edits, real backup deletion/restore, CI build or QA started by this consent.

### Remaining pre-implementation gating discussion (not yet user-approved)
- Next milestone product semantic version: `0.11.0` was previously proposed (current `0.10.0`), while CI build ID remains independent; owner confirmation still needed.
- Determine compatibility/safety contracts for full backup/restore/import, backup-format manifest and handling app/schema versions, pre-migration complete-backup gate, test-only restore validation, with no active QA database replacement.
- Agree measurable startup/UI responsiveness acceptance and approach for ~2 GB SQLite without degrading correct aggregation; source-faithful SQL view roadmap and editor/export safety.
- October 12 additional Enel information is still outstanding and must not be guessed; decide if a consolidated next build waits for it or if new work integrates later.
- Developer to propose one coherent implementation plan and acceptance matrix, then obtain explicit user authorization **before code/build**.

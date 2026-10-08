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

**Next design discussion (OPEN):** On selective recovery, choose a default when an old backup and today's data both contain records for the same category: safely import missing records only by default, replace a selected category only under a distinct explicit guarded flow, or another owner-preferred behavior. Exact category-specific equivalence/merge rules are an engineering design task, not permission to assume uniqueness or discard history.

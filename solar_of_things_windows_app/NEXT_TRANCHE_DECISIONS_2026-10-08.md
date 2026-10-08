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

Only the items under **Decisions confirmed** are accepted requirements/constraints. Wireframes, backups workflow, restoration policy, precise version, implementation steps and dates remain **OPEN**. This is a continuity checkpoint, not QA evidence or a release authorization.

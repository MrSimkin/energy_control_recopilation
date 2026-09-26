# Solar of Things Windows App — Continuity / Resume Status

Date: 2026-09-25
Status: PHASE 1 COMPLETE — PHASE 3 BACKFILL IN PROGRESS / PHASE 4–6 ANALYSIS STACK IN PROGRESS

This file is the canonical continuity note.

## Repository / branch

- Repository: `MrSimkin/energy_control_recopilation`
- Canonical branch: `main`
- Pre-refinement HEAD: `fe8e319fbe4470a5eae260a44706dd20325d4bc7`
- Original formal CI validation run: `36085763308`
- Original validated source commit: `778abaab896d6e211fdc658aa56e80bb6947e7bd`

## Completed and still authoritative

- Solar of Things / SiSeLi public/API research through Round 14.
- Product/functional specification v1, subject to the approved 2026-09-25 language change note.
- .NET 10 / C# / WPF x64 architecture.
- separate Core library.
- SQLite + WAL + migration foundation.
- SQL-backed non-secret settings.
- portable/installed path strategy.
- JSONL diagnostics.
- Windows DPAPI secret-store abstraction.
- GitHub Actions Windows build/smoke/publish workflow.

Do not repeat this foundation unless evidence shows a regression.

## Original Windows 11 manual checkpoint — PASS

On 2026-09-25 the user tested the original portable development artifact on the actual Windows 11 x64 target machine.

Confirmed:
- shell launched and rendered correctly;
- normal launch did not require UAC/elevation;
- application closed normally;
- `Data\\energy.db` was created;
- `Backups\\` was created;
- `Logs\\` was created;
- JSONL diagnostics were created;
- displayed database path was the portable `...\\Data\\energy.db` path;
- SmartScreen unknown-publisher warning was expected for the unsigned development build.

The user had manually selected Run as administrator on an earlier launch; its UAC dialog was user-initiated and is not evidence of an application elevation requirement.

## Approved requirement change — 2026-09-25

Earlier files correctly record the former instruction that the UI be English.

The user has now explicitly changed the canonical requirement:

**Spanish is the default UI language. English is selectable as an alternative.**

The preference must persist locally.

Historical requirement files remain historical and should not be rewritten to pretend this was always the requirement.

Current product name `Solar Energy Monitor / SolarEnergyMonitor` is provisional.

**Final product name = TBD.**

## Refined Phase 1 build — CI PASS

Implementation commit:
- `ee8e8200f132be9aef42875c6ae92eb1f10adc66`

GitHub Actions:
- workflow: Windows Build
- run ID: `36171030035`
- conclusion: **SUCCESS**

Fresh portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10881030047`
- SHA-256 digest reported by GitHub: `4389db735ab205698c014bc97e505191b0601ad2075988f926ef441642281cde`
- source commit: `ee8e8200f132be9aef42875c6ae92eb1f10adc66`

Validated by CI:
- .NET 10 restore;
- WPF Release build;
- existing SQLite smoke test;
- self-contained win-x64 publish;
- portable-mode marker;
- artifact upload.

## Final refined-build user revalidation — PASS

On 2026-09-25 the user revalidated artifact ID `10881030047` on the target Windows 11 x64 machine and reported **all checks passed**:

- normal launch without UAC;
- Spanish shown by default on a fresh portable database;
- English switching works;
- English preference persists after restart;
- switching back to Spanish persists after restart;
- every sidebar entry visibly navigates to its distinct placeholder;
- portable database remains at `...\\Data\\energy.db`;
- clean shutdown.

**Phase 1 is formally COMPLETE.**

Canonical closure receipt:
`PHASE_01_ACCEPTANCE_RECEIPT.md`.

## Current development frontier

Phase 1 is complete.

Phase 2 has been validated against the user's real production account far enough to establish:
- normal account/password login using the built-in production client profile: PASS;
- station discovery: PASS;
- device discovery/details: PASS;
- gather-attribute discovery: PASS;
- `dataSource=1` live-state validation: PASS;
- energy-flow read: PASS;
- daily aggregate read: PASS;
- commissioning-profile persistence: PASS;
- remembered session/reconnect after restart: PASS;
- evidence-backed server logout: PASS.

The live report exposed one shared protocol mismatch in history + alarm queries: fractional-second ISO timestamps were rejected by production as invalid `fromTime`. That wire format was corrected to station-local `yyyy-MM-ddTHH:mm:sszzz`. Its final production verification is intentionally folded into the next Phase 3 live test rather than spending a separate user test cycle.

Phase 3 raw-data ingestion is now **IN PROGRESS**.

Implemented Phase 3 foundation:
- SQLite schema v4 raw-history corpus;
- raw `device + attribute + actual source timestamp` storage;
- explicit null/missing preservation;
- raw API-page capture;
- per-local-day completeness/audit state;
- selected-key history ingestion;
- `record/list` fallback for any incomplete selected-key day;
- local-day timezone-aware windows;
- page size **300** for both raw-history endpoints;
- stop on short page or positive page-count `total`, with bounded safety cap;
- actual returned timestamps only — no synthetic five-minute grid;
- daily median/p90/max gap metrics from real timestamps;
- idempotent upsert;
- initial lower bound from real device `installedAt` metadata when available;
- incremental reread overlap from the newest locally stored timestamp;
- sync-run audit;
- core progress/cancellation support;
- `Actualizar datos` wired to the history-ingestion engine.

Canonical data rule:
Solar of Things raw telemetry is commonly around five-minute cadence, but cadence is not exact. The collector must persist every real timestamp, preserve gaps/nulls, and never fabricate missing 5-minute rows or integrate power using a fixed 5-minute multiplier.

## Current combined Phase 2/3 live-test candidate — CI PASS

Windows Build:
- run ID: `36186838776`
- source commit: `57f9b8b2912b5834edff7355f49d1566f3606bd6`
- conclusion: **SUCCESS**

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10887225685`
- SHA-256: `fb7dc54e3255a5494d82ea32f1dbde5b3fcaa5be40aacc8a1b61fc2079681ee7`

This candidate contains:
- timestamp-corrected Phase 2 history/alarm probes;
- Phase 3 schema v4 raw corpus;
- daily station-local raw-history ingestion;
- 300-frame pagination;
- selected-key primary history + record/list fallback;
- first-backfill cross-source comparison;
- idempotent persistence;
- actual-timestamp gap metrics;
- retry of historical PARTIAL days;
- visible progress bar;
- safe Stop/Detener preserving already committed data;
- hard request budget + pacing + rate/auth/server circuit breakers;
- automatic bounded start range based on device installation/local continuity;
- manual start-date override;
- Phase 3 status shown in the desktop shell.

The next real-PC run is intentionally a **combined Phase 2/Phase 3 acceptance/development test**:
1. authenticate/reconnect normally;
2. use the timestamp-corrected commissioning probes;
3. run `Actualizar datos`;
4. attempt daily backfill from the device installation date through today;
5. inspect the resulting diagnostic/sync evidence and local corpus;
6. correct any live pagination/shape/retention mismatch found.

## Installation-specific family manual integrated — 2026-09-25

The user supplied the family-specific manual:

`Manual_Familiar_SPRO_6200_LC230_Midea_v2_ES.docx`

Source metadata:
- edition: v2.0;
- manual date: 2026-08-14;
- SHA-256: `f519a39be14950258ce51d3cbb3a7e69cbc6b23769b2ae9e47c77ca71f5a3bde`;
- user confirms the manual's recommended inverter changes are currently applied.

Canonical repo integration:
- `INSTALLATION_BEHAVIOR_CONTRACT.md`;
- `MANUAL_FAMILIAR_INTEGRATION_REVIEW_2026-09-25.md`;
- `reference/FAMILY_MANUAL_SOURCE.md`.

Roadmap effect:
- no phase renumbering;
- Phase 4 now separates protocol/device normalization from installation-specific contextual interpretation;
- current configuration becomes a read-only behavior/compliance contract;
- battery UI must distinguish stored energy, ordinary-use energy above 20%, emergency 20→10% reserve and protected 10% floor;
- grid use while recovering from 20% toward 50% can be expected;
- current policy expects solar-only battery charging;
- zero export is an invariant;
- seasonal analysis may move load timing but must not automatically change protection thresholds;
- known 1.5 kW Midea heater schedule is contextual metadata, not a new control integration.

Important protected/unknown areas:
- exact firmware remains unknown;
- exact CT/zero-export meter topology remains unknown;
- second AC output is observed enabled but its physical circuit mapping remains unknown;
- grid profile/CT/BMS/protection writes remain outside application scope.

The manual **does not invalidate or redefine Phase 3 raw history**. Inverter measurements keep their device/protocol meaning. The manual adds a separate contextual layer for expected behavior, reserve semantics and configuration comparison.

Phase 4 work already underway before this manual remains useful, but battery semantics and behavior classification must follow the installation contract before the Battery page is finalized.

## Phase 4 installation-aware implementation checkpoint — 2026-09-25

Implemented and CI-compiling:

- schema v7 configuration-health snapshot;
- local read-only installation behavior evaluator;
- HPVINV02 normalization rule v2 with AC grid voltage;
- evidence-based latest household operating-state classifier;
- real Battery page with 20% / 10% / 50% family-manual semantics;
- Home plain-language operating explanation;
- Data & Updates read-only inverter configuration-health summary.

These views use local raw/normalized data and do not add background Solar of Things polling.

The next substantive target-PC validation should verify this combined Phase 4 tranche together; do not ask for a separate micro-test for each individual UI/card change.

## Phase 5/6 statistics + interactive analysis checkpoint — 2026-09-25

Implemented after the installation-context separation was clarified:

- raw and normalized physical calculations remain household-setup independent;
- household manual/settings remain a separate context layer except battery reserve/capacity presentation;
- schema v8 contextual behavior samples remain separate from normalized metrics;
- canonical time-range resolver;
- hour/day/week/month/year aggregation;
- real-timestamp power integration with long-gap exclusion;
- SOC min/max/time-weighted average/end with coverage;
- deterministic CI smoke vector with intentional 30-minute hole;
- run 221 PASS proves the hole is not bridged;
- History & Charts real date-range UI;
- quick range presets with visible exact dates;
- contextual duration summaries;
- physical energy summaries with coverage;
- auditable aggregation table;
- ScottPlot.WPF interactive chart dependency;
- solar/house/grid energy chart;
- separate battery SOC chart;
- low-coverage warnings;
- reset-view control;
- battery 10/20/50 contextual reference lines;
- source-selection metric picker is the current code checkpoint.

Important separation:

- charts consume the same `EnergyAggregationTable` rows as the detailed table;
- no chart has a separate raw-calculation path;
- household setup does not change PV/house/grid arithmetic;
- 10/20/50 battery lines are visual/contextual overlays only.

Do not ask the user to manually validate each intermediate chart commit.
The next manual test is one combined Phase 4–6 checkpoint using the existing real `Data\energy.db`.
## Extended Phase 6 household UI checkpoint — 2026-09-25

Additional implementation after the first interactive checkpoint:

- energy and battery charts now support readable mouse-hover bucket inspection;
- hover detail and WPF tooltip show exact aggregation-row values and coverage;
- energy and battery chart X axes are linked, while their kWh and % Y axes remain independent;
- target normalization advanced to `hpvinv02.v3`;
- measured battery charging current and measured battery discharge current are preserved as separate normalized physical metrics;
- Battery page has a collapsed **Información técnica** section for:
  - measured battery voltage;
  - measured charge current;
  - measured discharge current;
  - derived battery power;
- missing SOC no longer erases independently available technical measurements;
- Home replaces the obsolete chart-placeholder area with a truthful **latest saved day** summary:
  - exact saved date;
  - solar kWh;
  - house kWh;
  - grid kWh;
  - minimum calculation coverage;
- that summary does not extrapolate missing hours;
- Home includes an obvious button to open History & Charts.

Validated checkpoints already green:
- chart hover inspection: run 257;
- linked chart time axes: run 258;
- normalization v3 current preservation: run 259;
- technical battery panel/value wiring: run 263;
- latest-saved-day Dashboard summary passed build + smoke in run 267 while artifact publishing continued.

The next manual target-PC validation remains one **combined Phase 4–6 test**, not a micro-test for each addition.

## Combined Phase 4–6 target-PC candidate — CI PASS

Windows Build:
- run ID: `36202641150` (run 271);
- source commit: `b1e71151f4acf19c00aae4719ebcf4d1b465ce32`;
- conclusion: **SUCCESS**.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10892690906`;
- size: 74,848,255 bytes;
- SHA-256: `a7e1a6a70f7b90e56aa9eeb23af0f454a913db477713a0b6e6ac8d8f6e4e66b1`;
- expires: 2026-12-24.

This is the intended single combined Windows validation candidate for the current Phase 4–6 tranche.

The test must reuse/preserve the existing portable `Data\energy.db` corpus. It does **not** require completing the full historical backfill. A short Update Data continuation followed by safe Stop is sufficient to verify the resume frontier.

## Resume rule

Resume with **Phase 3 backfill continuing independently while the Phase 4–6 local analysis stack advances**.

Canonical architecture:
1. raw SiSeLi evidence;
2. normalized physical metrics independent of household setup;
3. timestamp-aware statistics/aggregation independent of household setup;
4. separate household-context interpretation and battery reserve presentation;
5. charts/tables built from the same aggregation rows.

Immediate order after the current combined checkpoint:
1. validate the latest combined Dashboard/Battery/History build in CI;
2. perform one substantive target-PC Phase 4–6 validation using the existing database;
3. fix only concrete real-data/UI issues found;
4. continue Phase 6 chart interaction/analysis UX;
5. keep evidence-dependent flow-attribution percentages deferred until validated;
6. continue historical backfill independently as convenient.

Do not perform micro-tests for individual cards/charts.

Do not restart completed API research, Phase 0 specification, or Phase 1 architecture/localization/navigation work unless a concrete regression or implementation-time evidence requires a narrow correction.

## Partial combined Phase 4–6 real-PC validation — 2026-09-25

The user began the intended combined target-PC validation using artifact `10892690906` and the real portable database.

Important recovery note:
- `Data\energy.db` had been accidentally deleted before the test but was successfully recovered before validation;
- old Logs were not recovered and are not required for continuation;
- the recovered database opened normally with no visible startup/migration error.

Validated in this session:

### Startup / Home
- normal launch: PASS;
- recovered real database opened and remained usable;
- Home correctly uses stale-safe wording: **Última información disponible** rather than claiming old data is live;
- latest saved reading shown: `25-09-2026 17:45`;
- stale warning is visible;
- latest values shown from local data: PV 0.000 kW, house 0.313 kW, battery 31%, grid 0.419 kW;
- latest-saved-day summary shown for `25-09-2026`: solar 4.96 kWh, house 15.77 kWh, grid 9.45 kWh, minimum coverage 74.0%;
- low coverage is visibly highlighted;
- obvious History & Charts navigation is present;
- installation date `20-04-2026` remains distinct from next automatic download date `25-05-2026`.

### Battery
- family-facing battery page renders real data;
- SOC: 31%;
- estimated stored energy: 3.65 kWh;
- estimated ordinary-use energy above the 20% reserve: 1.30 kWh;
- emergency 20→10% reserve estimate: 1.18 kWh;
- configured usable capacity displayed: 11.776 kWh;
- activity shown as charging;
- technical panel PASS with real normalized measurements:
  - battery voltage 52.8 V;
  - charge current 3.2 A;
  - discharge current 0.0 A;
  - derived battery power approximately -0.17 kW.

### Data & Updates
- first saved date: `20-04-2026`;
- last saved date: `25-09-2026`;
- reviewed days: 38;
- download-problem days: 0;
- next automatic historical date: `25-05-2026`;
- raw readings: 848,772;
- normalized/display-ready readings: 106,612;
- configuration-health summary currently shows 0 checked / 0 different / 11 unconfirmed before a fresh current-state refresh.

### History & Charts
- all-saved-history range renders from `20-04-2026` through `25-09-2026`;
- whole-period coverage is visibly low (~21%);
- physical energy totals and contextual duration summaries render;
- low-coverage warning is visible;
- detailed audit table renders;
- energy and battery charts render.

### Concrete issues discovered — must be corrected after validation
1. **Battery reserve thresholds should prefer current inverter settings when available.**
   - The app already captures current settings such as `bmsReturnsToMainsModeSOC` and `bmsReturnsToBatteryModeSOC` in the latest-state snapshot.
   - The Battery page currently calculates/displays 20/10/50 from `InstallationContextPolicyService`, and several Spanish strings hard-code those values.
   - Current inverter settings should be the primary source when available/validated; family-manual policy should remain expected configuration/fallback/context.
   - A mismatch must be visible rather than silently presenting the manual value as the current device setting.

2. **Home should support truly current/live household readings while authenticated.**
   - Desired current values: PV production, house consumption, battery charge (% and estimated kWh), and grid use.
   - Live/current wording must only be used after a sufficiently recent Solar of Things latest-state read.
   - Stale/local fallback behavior must remain exactly as currently validated.
   - Do not add aggressive background polling.

3. **Charts currently violate missing-is-not-zero / do-not-bridge-gaps visually.**
   - Battery scatter plotting filters missing SOC points and then connects the remaining points, causing lines to bridge long unknown periods (observed visually across the May→September gap).
   - Energy buckets with 0% coverage can appear as ordinary 0.00 kWh bars/points, which visually implies measured zero rather than unknown.
   - Fix chart rendering so unknown periods remain visibly discontinuous/absent and are never presented as measured zero.

4. **Chart usability is poor for long sparse ranges.**
   - Full-history labels/data are difficult to read when long gaps exist.
   - Mouse wheel over the chart currently scrolls the page as well as/instead of providing predictable chart zoom/navigation.
   - Improve wheel capture/zoom behavior, time-axis readability, and sparse-range presentation without changing the underlying calculations.

These are concrete real-PC findings, not speculative redesign requests.

### Validation still pending
The session was intentionally stopped before the final Update Data block.

Next session:
1. do not repeat already-passed Home/Battery/Data screens unless a fix affects them;
2. complete a short `Actualizar datos` run;
3. verify authentication/current-state refresh;
4. verify resume starts from a sensible historical frontier (expected around `25-05-2026`, subject to actual local corpus state);
5. verify progress;
6. deliberately Stop/Detener;
7. verify already committed data remains saved and the next resume frontier persists;
8. then implement the concrete Phase 6 fixes above as a coherent tranche and validate with CI.

No new artifact is required before completing the pending Update Data portion unless code changes are made first.


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



## Combined Phase 4–6 real-PC QA completion — 2026-09-26

The pending synchronization/Stop block was completed on the real Windows 11 x64 target PC using the same portable corpus.

Pre-sync: 38 reviewed days; 848,772 raw readings; 106,612 normalized readings; automatic resume frontier 2026-05-25; 0/0/0 download problems/retries/terminal-unavailable days; configuration health 11 confirmed / 0 drift / 0 unresolved.

First deliberate short sync + Stop: reviewed days 38 → 49; raw readings 848,772 → 1,114,383; normalized readings 106,612 → 140,162; resume frontier 2026-05-25 → 2026-06-05; problems/retries/terminal-unavailable remained 0/0/0.

Restart persistence: 49 reviewed days, 1,114,383 raw readings, 140,162 normalized readings and the 2026-06-05 frontier all persisted unchanged after closing/reopening.

Second deliberate short sync + Stop: resumed from the persisted frontier; reviewed days 49 → 61; raw readings 1,114,383 → 1,389,390; normalized readings 140,162 → 174,889; resume frontier 2026-06-05 → 2026-06-17; problems/retries/terminal-unavailable remained 0/0/0.

PASS: safe Stop, committed-data preservation, restart persistence, persisted resume frontier and subsequent resume from that frontier. Full April→current backfill remains independent.

### Finding 5 — login/session UX

Real-PC verification confirmed remembered session/credentials work: after restart the application connected without re-entering the password. The defect is UX clarity, not credential persistence. Corrective direction: never refill/display the protected password; explicitly show remembered/restored/verified state; remove DPAPI jargon from normal UI; distinguish remembering from auto-connect; add optional one-shot startup verification/current-state refresh; keep an obvious sign-out/forget action.


## Phase 6 corrective tranche started — 2026-09-26

Five real-PC findings are addressed together: observed battery thresholds before manual fallback; explicit recent Home refresh; missing-data gaps not zero/bridged; sparse-range/wheel UX; clarified remembered-session UX with optional startup auto-connect. After CI, manual validation targets only changed behavior. The next product milestone is Reporting (presets + Excel + printable PDF), while Phase 3 backfill continues independently.


## Corrective Phase 6 tranche — CI PASS — 2026-09-26

Commit:
- `2ddaac4bff2a0c04607aff262b8833897b67ad2b`

Windows Build:
- run ID: `36265992006` (run 272);
- restore/build: PASS;
- SQLite smoke: PASS;
- self-contained win-x64 publish: PASS;
- artifact upload: PASS.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10913613069`;
- size: 74,862,280 bytes;
- SHA-256: `09207a7499145b756c85be749a53a64acb4517e2af2a10d81cb9b551c77170e5`;
- expires: 2026-12-25.

This validates compilation and automated smoke coverage for the five corrective findings. A later manual checkpoint should target only the changed UX/data-presentation behavior.

## Phase 7 Reporting started — 2026-09-26

Reporting is now the active next product milestone while Phase 3 historical backfill remains independent.

First checkpoint scope:
- real Reports page;
- named persistent presets;
- configurable period/aggregation;
- Excel export;
- printable PDF export;
- automated generation smoke tests.

Reporting remains IN PROGRESS until built-in report variants/charts and one combined real-PC report validation are complete.


## Phase 7 first reporting checkpoint — CI PASS — 2026-09-26

Commit:
- `4a224e1c5c45d4c0f6aa926631894a3ba6f2ed6d`

Windows Build:
- run ID: `36266430815` (run 273);
- restore/build: PASS;
- reporting smoke including XLSX generation: PASS;
- reporting smoke including PDF generation: PASS;
- self-contained win-x64 publish: PASS.

Portable artifact:
- artifact ID: `10914352694`;
- size: 78,588,758 bytes;
- SHA-256: `07f1f4ff3eb8b548859dcf60b0dd5aa022ba21d1f86049445769536e01e7d7cd`;
- expires: 2026-12-25.

The next Reporting commit expands the green foundation with built-in Simple/Detailed/Battery report variants, printable charts, glossary and export localization before requesting one combined target-PC validation.


## Phase 7 built-in reporting checkpoint — final CI PASS — 2026-09-26

Second-checkpoint implementation:
- built-in Simple Energy, Detailed Energy and Battery report types;
- report type saved with presets;
- Spanish/English export labeling;
- printable energy and battery-SOC charts;
- PDF/Excel glossary;
- explicit conservative treatment of unavailable flow-attribution and utility-comparison metrics.

The initial second-checkpoint commit `12742e20e2ebde4884c23fc830297297e2dccb96` failed Build only because of a MigraDoc chart-legend API mismatch. It was repaired narrowly in:

`78ebc4ddbd66ba6b40b57de7ba7d8ebc8d6da9ef`

Final Windows Build:
- run ID: `36266851482` (run 275);
- restore/build: PASS;
- SQLite/reporting smoke: PASS;
- XLSX generation smoke: PASS;
- PDF generation smoke: PASS;
- win-x64 self-contained publish: PASS;
- artifact upload: PASS.

Final combined-QA portable:
- artifact ID: `10913787724`;
- size: 78,595,326 bytes;
- SHA-256: `8860e64b6f962bc268e64b1ce2682bdf84bfe4722cfc2495d99220105bf80202`;
- expires: 2026-12-25.

Current Reporting state:
- automated implementation checkpoint: GREEN;
- Phase 7 remains open only for one combined real-PC reporting/corrective-UX validation;
- do not repeat the already-passed historical-sync Stop/resume QA;
- utility reconciliation and financial/bill reports remain deferred until utility-meter/tariff source subsystems exist;
- Phase 3 backfill remains independent.


## Explicit re-entry checkpoint — 2026-09-26

This block is the canonical re-entry point after the Phase 6 corrective tranche and the Phase 7 reporting implementation pass.

Repository state before this documentation-only checkpoint:
- main HEAD: `7177de8a5122eb0f1d62ecdb5c33a4a55269d330`;
- latest code-bearing green commit: `78ebc4ddbd66ba6b40b57de7ba7d8ebc8d6da9ef`;
- Windows Build run: `36266851482` (run 275), conclusion SUCCESS;
- final QA artifact: `SolarEnergyMonitor-win-x64-dev`, artifact ID `10913787724`;
- artifact SHA-256: `8860e64b6f962bc268e64b1ce2682bdf84bfe4722cfc2495d99220105bf80202`;
- artifact expiry: 2026-12-25.

Direct GitHub references:
- CI run: https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36266851482
- artifact page/download: https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36266851482/artifacts/10913787724

Operational state:
- Phase 4–6 sync/Stop/resume QA: PASS and must not be repeated;
- Phase 6 five-finding corrective implementation: CI GREEN, combined target-PC validation still pending;
- Phase 7 Reporting implementation: CI GREEN, one combined target-PC validation still pending;
- Phase 3 historical backfill remains independent and must not block the combined QA;
- Grid/Utility Reconciliation and Financial/Bill reports remain deferred until utility-meter/tariff source subsystems exist.

Next user action:
1. download artifact `10913787724`;
2. extract it to a new folder;
3. copy the proven portable **entire `Data\` folder** into the new candidate before launch, replacing the candidate's empty/new `Data\` folder. This preserves both `Data\energy.db` and `Data\Secrets\`, which is required to validate remembered-session/autologon behavior under the same Windows user;
4. run `SolarOfThings.App.exe`;
5. perform the single combined QA defined in `PHASE_07_IMPLEMENTATION_NOTES.md`;
6. report anomalies only. If no anomaly is found, close Phase 7 for currently available subsystems.

Combined QA scope:
- login/session remembered-state UX and optional auto-connect;
- one-shot current Home refresh;
- current inverter battery threshold wording/fallback/drift behavior;
- chart missing-data gaps and wheel/Ctrl+wheel behavior;
- save one relative report preset, restart, verify persistence;
- open one exported Simple Energy XLSX;
- open one exported Detailed or Battery PDF;
- verify period, coverage note, charts, glossary, readability/printability and no fabricated zeros across missing data.


### Re-entry correction — portable credentials

For the combined QA, migrate the **entire portable `Data\` directory**, not only `energy.db`.

Reason:
- `Data\energy.db` contains the local database and report presets/settings;
- `Data\Secrets\` contains the DPAPI-protected remembered Solar of Things session/credential material;
- DPAPI uses the current Windows user, so copying `Data\Secrets\` is appropriate only on the same Windows account that created it;
- never publish or share the contents of `Data\Secrets\`.

This correction supersedes any earlier instruction to copy only `Data\energy.db`.


## Combined real-PC QA progress — 2026-09-26

### Phase 6 Finding 2 / remembered-session current refresh — PASS

Using final combined-QA portable artifact `10913787724` under the same Windows user with the prior portable `Data\` directory preserved:

- application started with remembered-account state visible;
- user did not re-enter the Solar of Things password;
- explicit `Actualizar estado actual` succeeded;
- status changed to `Equipo conectado: Solar202`;
- Home switched from stale local reading `2026-09-25 17:45` to a recent live/current reading `2026-09-26 17:17`;
- observed current values on that refresh:
  - PV: 0.366 kW;
  - home: 0.307 kW;
  - battery SOC: 36%;
  - grid: 0.000 kW;
- Home summary updated coherently to indicate the house was mainly using solar energy.

Result:
- remembered credentials/session reuse without password re-entry: PASS;
- one-shot authenticated current-state refresh: PASS;
- stale/local fallback → fresh/current presentation transition: PASS.

### New real-PC UX finding — responsive layout / text truncation

At a normal non-maximized desktop window size, the Home right-side `Estado del sistema` column is too narrow and some long status text and controls become clipped/truncated unless the user enlarges the window.

Concrete examples from the real-PC screenshot:
- the post-stop synchronization status is visibly cut off;
- the manual-date control is cramped/cropped;
- the right status card has insufficient width relative to its content.

This is a real usability issue, not a data correctness defect.

Corrective direction for the next UX tranche:
- make Home responsive at ordinary desktop widths;
- prefer text wrapping over clipping for status messages;
- avoid fixed-width competition between the main summary and right-side status panel;
- allow the status panel to move below/stack when horizontal space is insufficient, or otherwise provide a minimum usable width;
- ensure date controls/buttons remain fully visible without requiring window maximization;
- preserve accessibility/readability when Windows text scaling is above 100%.

This finding does not block the current combined QA. Continue the QA and batch the responsive-layout fix with the next UI correction tranche.


### Combined real-PC QA — Battery thresholds PASS; cross-page freshness finding

Target-PC Battery screen after a successful authenticated Home current-state refresh showed:

- observed current thresholds: grid 20% · protected floor 10% · return 50%;
- wording explicitly says they match the expected family policy;
- ordinary-use and outage-reserve estimates are described as using current inverter thresholds when available.

Result for the Phase 6 battery-threshold finding:
- observed inverter thresholds preferred when validated: PASS;
- manual/family policy retained as context/fallback: PASS;
- match/drift wording visible: PASS.

New non-blocking freshness/coherence finding:
- immediately before entering Battery, Home had a fresh current snapshot at 2026-09-26 17:17 with battery SOC 36%;
- Battery still showed the stored/local snapshot: SOC 31%, latest reading 2026-09-25 17:45;
- Battery text is technically truthful because it says it is based on the last locally saved reading, but the cross-page experience is confusing after the user just requested a current refresh.

Corrective direction for the next UI/data-coherence tranche:
- when a fresh authenticated current snapshot exists, Battery should prefer that current SOC/technical state where safe and clearly label it as current;
- retain stored/local fallback when no fresh snapshot exists;
- keep historical/energy calculations tied to the validated stored corpus;
- never silently mix live and historical values without labels.


### Combined real-PC QA — missing-data chart semantics PASS

Target-PC History & Charts screen with the full saved-history range showed:
- explicit warning that periods without measurements remain gaps and are not drawn as 0;
- the energy chart visually preserves empty horizontal regions where measurements are absent;
- observed bars exist only where measured period data exists;
- no visual bridge or fabricated zero bars were observed across the large missing-history interval;
- overall screen continues to expose minimum coverage context (46.7% for the selected range).

Result for the Phase 6 missing-data chart correctness finding:
- missing/unknown is not rendered as measured zero: PASS;
- missing periods remain discontinuities/gaps: PASS;
- coverage warning remains visible: PASS.

The remaining chart-interaction check is mouse-wheel behavior: normal wheel should scroll the page, while Ctrl+wheel should zoom the chart.


### Combined real-PC QA — chart wheel interaction, part 1 PASS

With the pointer over the History & Charts graph, normal mouse-wheel movement scrolled the page vertically and did not zoom the chart.

Result:
- normal wheel → page scroll: PASS.

Remaining interaction check:
- Ctrl+wheel over the chart should zoom the chart rather than scroll the page.


### Combined real-PC QA — chart wheel interaction, Ctrl+wheel FAIL

Evidence:
- user reproduced behavior on both the energy chart and the battery chart;
- a ~20-second target-PC screen recording was reviewed during QA.

Observed:
- normal wheel over a chart scrolls the page only: PASS;
- Ctrl+wheel does zoom the chart: PASS;
- however Ctrl+wheel also scrolls the containing page at the same time: FAIL.

Therefore the Phase 6 chart interaction finding is only partially resolved.

Expected final behavior:
- normal wheel over chart → scroll page;
- Ctrl+wheel over chart → zoom chart only;
- page position must remain stable while Ctrl+wheel zoom is active;
- same behavior must apply consistently to both energy and battery charts.

Corrective direction:
- consume/suppress the ScrollViewer wheel path when Ctrl is pressed without blocking ScottPlot zoom;
- verify routed PreviewMouseWheel/MouseWheel handling order so the chart receives the zoom gesture but the parent ScrollViewer does not act on the same event;
- target both chart controls through the shared handler;
- revalidate on the real Windows app after the next UI correction tranche.

This issue is non-destructive and does not block continuing the combined QA.


### Combined real-PC QA — Settings/session UX clarity PASS

Target-PC Settings screen after successful authenticated current refresh showed:
- explicit green state: `Conectado y verificado con Solar of Things.`;
- separate checkbox: `Conectarme automáticamente al iniciar`;
- explanatory text that auto-connect performs one startup check and does not continuously poll;
- explicit `Cerrar y olvidar sesión` action;
- explicit privacy wording that a remembered password is never refilled or displayed.

Result for the login/session UX finding:
- remembered/restored/verified session state is now understandable: PASS;
- remember-vs-auto-connect distinction is visible: PASS;
- sign-out/forget action is obvious: PASS;
- password privacy behavior is explained: PASS.

Functional auto-connect behavior still requires one restart test with the checkbox enabled.


### Combined real-PC QA — startup auto-connect PASS

With startup auto-connect enabled, the application was fully closed and reopened. Without user interaction, the remembered session was restored and verified, Home refreshed to a recent 2026-09-26 18:02 current-state snapshot, and no historical backfill started.

Observed current snapshot:
- PV 0.183 kW;
- home 0.418 kW;
- battery SOC 35%;
- grid 0.000 kW.

Result:
- remembered-session startup auto-connect: PASS;
- one-shot startup current-state refresh: PASS;
- no unintended historical backfill: PASS.

New non-blocking persistence/UX finding:
- the historical resume frontier remained persisted at 2026-07-06;
- Home nevertheless showed "Última descarga de datos: Nunca" after restart.

Corrective direction:
- persist or truthfully derive the last synchronization outcome/status;
- do not show "Nunca" when existing sync metadata proves prior historical synchronization;
- distinguish no prior sync, stopped sync, completed sync, and existing imported/copied corpus.

This does not block the current Reporting QA.


### Combined real-PC QA — Reports initial screen PASS with minor localization finding

Target-PC Reports screen loaded successfully with:
- report type selector;
- quick period selector;
- explicit From/To dates;
- aggregation selector;
- named preset controls;
- Excel and PDF export actions;
- saved-history range 2026-04-20 through 2026-09-25.

Initial Reports UX is usable and the expected controls are present.

Minor non-blocking localization finding:
- the human-readable selection summary in Spanish shows the enum value `Day` in `Agrupación: Day`;
- user-facing summary should localize this to `Día` (and equivalently Semana/Mes/Año for other values) rather than expose internal enum names.

Continue combined QA with named preset persistence.


### Combined real-PC QA — report preset save PASS

User created and saved a named Reports preset for the relative period "Últimos 7 días del historial" with:
- report type: Simple Energy Summary;
- aggregation: Day;
- relative range semantics retained by the preset model.

Initial save operation completed successfully on the target PC.

Remaining preset check:
- close/reopen the application;
- verify the named preset remains available;
- load it and confirm it resolves against the current saved-history frontier rather than becoming a fixed stale date range.


### Combined real-PC QA — report preset restart persistence PASS

After fully closing and reopening the target-PC application, the previously saved named Reports preset "Últimos 7 días" remained present in the saved-preset list.

Result:
- local report-preset persistence across application restart: PASS.

Remaining relative-preset check:
- select the saved preset;
- verify its quick-period semantics remain "last 7 days of saved history";
- confirm the resolved From/To dates are derived from the current saved-history endpoint, not merely replayed as the original fixed dates.


### Combined real-PC QA — relative report preset semantics PASS

After restart, the saved preset "Últimos 7 días" was selected on the target PC.

Observed:
- quick period restored as "Últimos 7 días del historial";
- resolved date range: 2026-09-19 through 2026-09-25;
- current saved-history endpoint remains 2026-09-25;
- report type restored as Simple Energy Summary;
- aggregation restored as Day.

Result:
- named preset persistence: PASS;
- relative-period semantics preserved: PASS;
- preset re-resolves against the current saved-history endpoint instead of behaving as a fixed stale date pair: PASS.

Minor localization issue remains: Spanish summary still exposes the internal aggregation label "Day".


## Phase 7 second-reading functional clarification — 2026-09-26

Real-PC Simple Energy XLSX QA produced an important split result.

### Proven and retained

- named relative preset persistence: PASS;
- relative period re-resolution: PASS;
- XLSX file generation/open in Excel: PASS;
- `Resumen`, `Detalle`, `Calidad`, `Glosario` foundation: PASS;
- missing periods remain blank/unmeasured rather than fabricated zero: PASS;
- coverage data is present.

### Product result

Family-facing Simple Energy summary: **FAIL / redesign required**.

The workbook is technically useful as a second-level/technical report, but it does not let the target older/nontechnical readers answer their actual questions at a glance.

Canonical family priority:
1. household energy from Enel/grid;
2. household energy directly from solar;
3. household energy from battery;
4. count and duration of nights where battery reached normal reserve and grid was needed before sufficient solar returned.

Secondary family conversation:
- total PV generation;
- total house consumption;
- battery movement/context;
- unused/curtailed solar only when genuinely measurable.

Required third page:
- patterns and events over the **selected period**, regardless of whether that period is 7 days or many months;
- preserve all repeated event occurrences;
- robust hourly tendencies;
- night behavior;
- evolution through longer selected periods;
- observable-opportunity denominators so missing data is never treated as “event did not occur”.

The full canonical clarification is:
- `solar_of_things_windows_app/REPORTING_FAMILY_DESIGN_2026-09-26.md`.

Phase 7 must no longer be closed by merely opening the existing PDF. Preserve existing exporter infrastructure and implement the clarified family report/pattern layer first.

### Pending corrective tranche still batched

Unrelated/non-blocking findings already recorded remain:
- Home responsive-layout clipping at ordinary window width;
- Battery page stale stored snapshot vs fresh Home current snapshot;
- Ctrl+wheel zoom also scrolls parent page;
- Home “Última descarga de datos: Nunca” after restart despite persisted historical frontier;
- Spanish aggregation summary exposing `Day` instead of `Día`.

Do not repeat already-passed historical sync/Stop/restart QA.


## Phase 7 family-report development resumed — 2026-09-26

Implementation now follows `REPORTING_FAMILY_DESIGN_2026-09-26.md`.

New code checkpoint adds:
- event detection for battery-normal-reserve + grid-use episodes;
- observable-night denominators;
- all event occurrences retained;
- robust cross-day time-of-day patterns;
- adaptive period evolution;
- family XLSX sheets `Resumen`, `Patrones`, `Eventos` plus technical annex;
- family PDF Page 1 / Page 2 / Page 3 structure;
- no invented direct-solar/battery source attribution or curtailed-solar kWh.

Next gate is Windows CI. Do not ask for another target-PC test until the code is green and any build-only issues are repaired.


## Phase 7 family-report redesign — CI GREEN — 2026-09-26

Canonical family design from `REPORTING_FAMILY_DESIGN_2026-09-26.md` is now implemented for the currently supportable metrics.

Green HEAD:
- `e02df70812af3a02f7800e4637e38d877f460ac9`.

Windows Build run 280 / `36277693543`:
- restore PASS;
- build PASS;
- deterministic family event/pattern smoke PASS;
- Simple Energy XLSX PASS;
- Simple Energy PDF PASS;
- portable publish/upload PASS.

New portable:
- artifact `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `10917741876`;
- size 78,620,828 bytes;
- SHA256 `86c12cf99cb3b0c56879861c6bcd2baab64bc3f2053da4e06b67d62b9f8e6eab`;
- expires 2026-12-25;
- run URL: `https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36277693543`.

Implemented family report behavior:
- `Resumen` now prioritizes utility/grid use, unavailable-but-required direct-solar and battery-to-house answers, night reserve/grid episodes, then whole-system totals;
- `Patrones` contains robust recurring time-of-day patterns, highlights and adaptive evolution;
- `Eventos` preserves all reserve+grid occurrences and night observability;
- PDF uses Page 1 household questions, Page 2 whole-system totals, Page 3 patterns/events;
- complete nights only are used in the family night denominator;
- event continuation persists after SOC begins recovering if grid is still required and solar remains insufficient;
- hourly patterns require repeated days and sufficient samples per local hour;
- missing/unknown is never treated as event absence or measured zero;
- existing `Detalle`, `Calidad`, `Glosario` remain as technical annex.

The next manual action is a **focused Reporting readability/semantic QA only** using artifact `10917741876`. Do not repeat historical sync or previously passed Phase 6 checks.


## Reporting QA correction — family cover still too technical — 2026-09-26

Target-PC XLSX review of `SolarEnergy_20260919_20260925.xlsx` confirmed:
- new sheets exist and mechanics work;
- but `Resumen` did not yet visually match the approved family wireframe;
- 38.9% coverage was not prominent enough before headline energy figures;
- “total” wording was misleading for a highly partial period;
- hourly “habitual” patterns were being shown from only 3 observed days in a 7-day selected period.

The next code checkpoint corrects these issues by restoring the wireframe-like family cover, explicitly separating observed/unknown nights, making partial-data status prominent, and suppressing “habitual” hourly patterns unless at least 60% of selected days (minimum 3) have usable observations.

Do not treat the prior XLSX screenshot as final family-report acceptance.


## Current Reporting QA artifact after family-cover correction — 2026-09-27

Use Windows Build run `36281469442`, artifact `10919445323` (SHA256 `829858fe447acb1124ff1db5e13c46ff831c9eaf14da448a0603db48cb3ee75a`).

This replaces artifact `10917741876` for the next target-PC test.

The correction was triggered by the real exported workbook `SolarEnergy_20260919_20260925.xlsx`, where the user correctly observed that key visual elements from the approved wireframe were missing.

The next QA action is narrowly scoped:
- reuse the full prior `Data\` directory;
- open `SolarEnergyMonitor.exe`;
- select the existing `Últimos 7 días` preset;
- export Simple Energy XLSX;
- inspect only `Resumen` first.

Do not repeat prior sync, preset-persistence, Phase 6, or old exporter-mechanics QA.


## Current target-PC Reporting finding — 2026-09-27

Latest real XLSX inspection confirms the family `Resumen` now materially matches the approved wireframe hierarchy.

Do not revert:
- partial-summary banner;
- three source cards;
- prominent home consumption;
- observable/shortfall/unknown night cards;
- plain-language interpretation;
- evidence-gated direct solar and battery→house placeholders.

One remaining Excel-only mechanical defect was observed at the bottom of Page 2: wrapped text in merged cells overlapped because merged rows were not auto-heighted. Explicit row heights/vertical alignment were added. Next target-PC check should only confirm that the bottom of `Resumen` is now readable before moving to `Patrones`.


## Current QA artifact — merged family Summary text repair — 2026-09-27

Use artifact `10919181734` from Windows Build run `36281821463`, SHA256 `1823018f6ec5aaabde9b7f24ae3fc3efab4822559bcfda5db68ed03abd9ffd44`.

It contains the already-approved family-cover hierarchy plus explicit row-height/vertical-alignment repair for the bottom Page 2 narrative blocks.

Next manual check:
- migrate complete prior `Data\` directory;
- run `SolarEnergyMonitor.exe`;
- export the same Simple Energy preset;
- inspect only the bottom of `Resumen` and confirm that “Solar que no pudimos aprovechar” and the final Patterns/Events/Quality note no longer overlap.

After that, proceed to `Patrones`; do not repeat earlier checks.


## Consolidated export-review decisions + carried Phase 6 commitments — 2026-09-27

The complete XLSX and 3-page PDF were reviewed as whole artifacts.

Canonical product decisions were appended to `REPORTING_FAMILY_DESIGN_2026-09-26.md` §16.

Key locked decisions:
- main title `Reporte de Uso de Energia - Tipo : <preset>`;
- Page 2 visually similar to Page 1;
- Page 3 family patterns/events; evolution table moves to technical annex;
- all relevant repeated highlights retained;
- positive night label `SIN PROBLEMAS DE ALIMENTACION`;
- PDF mirrors Excel hierarchy;
- 3 target charts:
  1. stacked household-consumption sources;
  2. battery SOC + PV + household use;
  3. household use + each supply origin;
- report aggregation governs charts;
- Aptos Narrow text / Aptos Mono values;
- glossary must become a full reading guide including Detail columns;
- full visual-formatting pass across every family/technical sheet;
- next manual semantic Reporting acceptance should use completed/high-coverage backfill rather than the current 38.9% corpus.

Still open before implementation:
- grouped line-chart y-value semantics under non-hourly aggregation;
- hidden vs explicit-unavailable behavior for charts blocked by unvalidated source attribution;
- exact parent-facing replacement labels on Battery.

Important carried Phase 6 finding:
- Battery page still uses stored normalized metrics while Home can show fresh current metrics;
- next combined build must make current Battery state Live/fresh where safe and clearly labelled, while historical calculations remain stored-corpus based.

Do not ask the user for another portable QA until the consolidated tranche is implemented and CI green.


## Critical continuity checkpoint — dynamic source attribution — 2026-09-27

A dedicated canonical investigation has been added:

`solar_of_things_windows_app/SOURCE_ATTRIBUTION_INVESTIGATION_2026-09-27.md`

Read it before resuming Reporting/source-attribution work.

Key state:
- historical update/backfill is complete for the current QA corpus;
- historical config is demonstrably dynamic;
- target profile: HPVINV02 / MH2083139 / dataSource 1 / EnergyFlow SUPPORTED;
- downloaded history contains changing transfer thresholds but no explicit SBU/LBU/OSO key;
- current raw DB has 12 LatestStateSnapshot captures and 167 selected-key-history captures;
- no EnergyFlow raw JSON is persisted;
- code inspection proves CommissioningService currently calls EnergyFlow but stores only status, discarding successful response data;
- original API research confirms EnergyFlow can expose PV/grid/battery/load nodes, direction and values;
- no historical endpoint for the full structured EnergyFlow view was established;
- source attribution must therefore become dynamic/as-of and evidence-driven;
- family battery graph uses estimated stored kWh, not SOC %, while retaining estimate labeling;
- Battery page keeps the minimum protected level with a clearer family explanation;
- do not request another manual Reporting build until the consolidated attribution/reporting/Battery tranche is implemented and CI green.

Immediate next evidence query:
- inspect the keys/values inside the 12 persisted LatestStateSnapshot JSON objects, as documented in the dedicated investigation file.


## Latest-state priority/mode fields discovered — 2026-09-27

Target DB inspection of the 12 persisted `LatestStateSnapshot` responses confirms that `state/latest/v1` exposes configuration/operating fields not present in the historical gather catalog.

Confirmed current-state keys include:
- `chargingPriorityOrder = 2`;
- `pvEnergyFeedingPriority = 1`;
- `workingMode = 1`;
- `outputModel = 0`;
- `mode = B`;
- `acChargingSwitch = 0`;
- `solarChargingSwitch = 0/1`;
- `chargingMainSwitch = 0/1`;
- `powerSupplyFromPVToLoadInACState = 0`;
- `mainsCurrentFlowDirection = +`.

This is important:
- current mode/priority information **is available** from Solar of Things;
- it is simply not part of the ordinary historical 87-key gather catalog;
- exact numeric enum mapping is still unresolved and must not be guessed;
- future current snapshots should preserve these fields and can establish a local mode/config history.

Canonical detailed investigation:
`SOURCE_ATTRIBUTION_INVESTIGATION_2026-09-27.md`, §17.

Immediate next evidence step:
- inspect the full JSON objects for the candidate priority/mode fields to see whether `valueDisplay`/labels/enums are already present in saved snapshots.


## Target SBU / OSO / LBU mappings confirmed — 2026-09-27

The full `LatestStateSnapshot` field objects were inspected.

Solar of Things itself provides these `valueDisplay` mappings on the target device:

- `workingMode = 1` → **SBU**;
- `chargingPriorityOrder = 2` → **OSO**;
- `pvEnergyFeedingPriority = 1` → **LBU**;
- `mode = B` → **Battery Mode**;
- `outputModel = 0` → **SIG**;
- `powerSupplyFromPVToLoadInACState = 0` → **No**.

Therefore current SBU/OSO/LBU is **CONFIRMED TARGET EVIDENCE**, not a family-manual inference.

Important remaining limitation:
- these priority/mode fields are in current `state/latest/v1`;
- they are not present in the ordinary historical gather catalog already stored;
- past priority-mode changes therefore remain unresolved from the existing DB alone.

Canonical detail:
- `SOURCE_ATTRIBUTION_INVESTIGATION_2026-09-27.md` §19.



## Debug investigation harness implementation — 2026-09-27

The development diagnostics window is being promoted from a log viewer to a reproducible read-only investigation harness for clean-build QA.

New intended controls:
- run complete diagnostics + export bundle;
- capture current LatestState;
- capture structured EnergyFlow;
- read remote config cache;
- trigger a direct/batch configuration read and preserve details;
- export an investigation ZIP.

The investigation ZIP includes:
- sanitized API diagnostics;
- DB/table inventory;
- historical attribute inventory;
- low-cardinality values;
- automatically detected historical low-cardinality changes;
- day-status coverage;
- normalized metric inventory;
- raw-capture inventory;
- all LatestState fields and priority/mode subset;
- installation configuration checks;
- behavior-state counts;
- all observable PV/house/grid/battery power-balance frames plus worst residuals;
- latest sanitized LatestState/EnergyFlow/config raw evidence;
- commissioned attribute catalog/capabilities/device/station metadata.

No write/config mutation endpoint is used.
The direct configuration read is classified by the research corpus as ACTIVE_DEVICE_READ: it can ask the device to report current configuration but does not write configuration.

Commissioning also now preserves successful EnergyFlow JSON instead of discarding it after setting capability status.


## Dynamic household source-attribution engine — implementation checkpoint — 2026-09-27

A new reporting service is introduced:
- `SourceAttributionService`;
- rule version `hpvinv02.source-attribution.v1`.

It derives, per selected report aggregation bucket:
- direct Solar → House kWh;
- Battery → House kWh;
- Grid/Enel → House kWh;
- measured house energy that remains unattributed;
- attribution coverage of observed house energy;
- observed-time coverage;
- ending battery SOC;
- estimated battery stored energy at bucket end using configured usable kWh;
- balance residual diagnostics.

Evidence policy:
- does NOT assume today's SBU/OSO/LBU for pre-snapshot history;
- uses as-of current-mode snapshots only from the point they actually exist;
- uses as-of historical SOC thresholds where present;
- uses physical flow inference for older intervals;
- ambiguous intervals remain unattributed;
- source components never silently absorb an unresolved residual.

Current-state mode evidence recognized:
- SBU;
- OSO;
- LBU;
- PV→load-in-AC flag.

This engine is integrated into `EnergyReportData` but visual/chart export wiring is the next step of the same consolidated tranche.


## Battery Live + family terminology implementation — 2026-09-27

Battery current-state page now prefers one coherent fresh `CurrentHouseholdSnapshot` when available instead of mixing fresh and stored values.

The current snapshot parser now promotes:
- battery SOC;
- battery voltage;
- battery charge current;
- battery discharge current;
- derived current battery power.

When a fresh current battery snapshot is not available, the whole Battery current-state block falls back to the stored normalized metrics and labels the reading as stored.

Family labels were revised:
- Carga actual de la batería;
- Energía guardada en la batería (estimada);
- Energía disponible antes de pasar a Enel (estimada);
- Reserva para cortes de luz (estimada);
- Nivel mínimo protegido de la batería.

The protected minimum remains visible and its explanatory copy now states that it is the lower level the system tries not to cross to protect the battery, especially during outages/exceptional operation.


## Consolidated attribution + family-report + Battery Live checkpoint — CI GREEN — 2026-09-27

Authoritative code checkpoint before this documentation-only update:
- HEAD: `41a3b6da4877ab4df9c229095bb7f445f9aef136`;
- Windows Build run: 305 / `36288909496`;
- restore: PASS;
- build: PASS;
- SQLite + deterministic reporting/source-attribution smoke: PASS;
- XLSX generation/product-structure checks: PASS;
- PDF generation: PASS;
- self-contained win-x64 publish/upload: PASS.

Portable artifact:
- `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10920998947`;
- size: 78,678,970 bytes;
- SHA-256: `27b9ec92fede8a8dc9a13954d8f6c1c67ea440670ef3828ff1c8a35cc0e3e975`;
- expires: 2026-12-26;
- run: `https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36288909496`.

### What is now implemented

**Dynamic household source attribution**
- `SourceAttributionService` / rule `hpvinv02.source-attribution.v1`;
- per selected aggregation bucket:
  - Solar → House kWh;
  - Battery → House kWh;
  - Grid/Enel → House kWh;
  - observed household energy left unattributed;
  - attribution coverage of observed household energy;
  - observed-time coverage;
  - ending SOC;
  - estimated stored battery kWh from configured usable capacity × ending SOC;
  - mean/max absolute balance residual diagnostics;
- as-of historical SOC configuration is used where available;
- current SBU/OSO/LBU snapshot evidence is used only from timestamps where it actually exists;
- ambiguous historical grid+solar frames remain unattributed instead of forcing a split;
- unresolved negative grid-sign semantics remain unresolved and are not clamped into false import;
- source components do not absorb a residual merely to make the balance close.

**Family Simple Energy export**
- title now uses `Reporte de Uso de Energia - Tipo : <preset/title>`;
- Page 1 cards use actual attribution for:
  - Enel/Grid → House;
  - Solar → House;
  - Battery → House;
- positive night wording is `SIN PROBLEMAS DE ALIMENTACION`;
- Page 2 uses family-style metric cards instead of a plain technical table;
- three report charts are rendered with the report's selected aggregation:
  1. stacked household consumption by source;
  2. solar production, household consumption and estimated stored battery energy;
  3. household consumption together with Solar→House, Battery→House and Enel→House;
- unresolved household source energy remains explicitly visible as `Sin atribuir`;
- `Patrones` is family-facing and includes a concise event summary;
- `Evolución` is a separate technical annex sheet;
- `Eventos` omits an empty event table and gives a plain-language no-event message when appropriate;
- `Detalle` now includes source-attribution, estimated stored battery and balance-residual columns;
- `Calidad` explains coverage, attribution, observability and balance residuals;
- `Glosario` is now a real reading guide, including family concepts, units, Detail columns and interpretation limits;
- workbook product smoke verifies:
  - 3 embedded charts;
  - the Evolution sheet;
  - >=26 Detail columns;
  - expanded glossary;
  - family title prefix.

**Typography/readability**
- Excel text: Aptos Narrow;
- Excel numeric values: Aptos Mono, including family-card values;
- explicit row heights added to family cards, night cards, section headers, Quality and Glossary;
- PDF mirrors the same family hierarchy and charts;
- PDF prefers Aptos-family fonts when safely resolvable and uses deterministic PDF-safe fallbacks without breaking export.

**Battery Live**
- current snapshot now promotes SOC, battery voltage, charge current, discharge current and derived current battery power;
- Battery page uses one coherent fresh current snapshot when available;
- if no fresh current snapshot exists, the whole current-state block falls back to stored normalized data and labels it as stored;
- entering the Battery page while authenticated triggers a read-only current-state refresh when the existing snapshot is stale;
- family labels were rewritten and the protected minimum remains visible with a plain-language explanation.

**Debug**
- investigation ZIP now includes source-attribution summary and daily attribution evidence across the available normalized history, in addition to the raw power-balance evidence.

### Status after this tranche

Phase 7 remains **IN PROGRESS**, not because the export mechanics are missing, but because final human family-readability/semantic acceptance should now be performed once against a genuinely high-coverage selected period.

Do **not** ask for another micro-fix test. The next target-PC Reporting validation should be consolidated:
1. use the new portable;
2. use a completed/high-coverage period;
3. export both XLSX and PDF;
4. review the complete artifacts in one pass.


## Home four-card Live polling correction — 2026-09-27

User QA found that the four Home cards were not truly Live:
- they displayed the most recent current-state snapshot after a manual/startup update;
- they did not keep checking for a newer cloud frame while Home remained visible.

This violated the intended Live requirement.

Correction:
- Home now performs a read-only current-state check immediately when entered while authenticated;
- while Home remains visible, it polls `state/latest` every **60 seconds**;
- polling stops when leaving Home;
- overlapping manual/navigation/timer current-state reads are serialized by an in-window guard;
- the four cards use one coherent latest current-state snapshot when available rather than silently mixing with stored normalized data;
- each current-state card labels the actual inverter frame timestamp;
- the Home freshness line distinguishes:
  - last automatic HTTP check time;
  - actual inverter/cloud frame time;
- repeated identical values across polls are expected when SiSeLi has not received a newer inverter frame.

Rationale:
- API research Round 12 recommends 60–120 seconds for an interactive visible dashboard;
- target/source telemetry typically advances around every 5 minutes;
- therefore this is “check every minute for the newest cloud frame”, not fabricated one-minute inverter telemetry.

The historical/latest-day summary below the four cards remains stored-history based and is not converted to Live.


## Consolidated QA findings after first high-coverage report — 2026-09-27

Reviewed together:
- target investigation ZIP;
- high-coverage 7-day XLSX;
- matching PDF.

Confirmed good:
- 7-day data coverage ~99.7%;
- three required charts are generated and legible;
- household-source stack preserves unattributed energy;
- source attribution coverage is explicitly reported (~92% for this sample);
- Detail/Quality/Glossary contain the intended audit fields;
- structured EnergyFlow succeeds on target HPVINV02.

Defects found and folded into the next consolidated build:
1. Home Live countdown:
   - four subtle 60-second progress lines share the same polling cycle;
   - they reset after each current-state check;
   - they remain graphical only, with no numerical countdown.
2. Debug config probes:
   - current endpoints reject a truly absent POST body;
   - research HAR had “no JSON fields” but still represented a POST body;
   - probes now send an empty JSON object rather than no body.
3. Debug energy behavior:
   - export strong grid->battery candidate frames;
   - export integrated candidate duration/energy summary;
   - export a parsed latest EnergyFlow interpretation.
4. Reporting:
   - PDF no longer labels reserve+grid episode duration as “Tiempo total observado”;
   - the duration row appears only when actual episodes exist;
   - Excel Patterns event summary receives explicit heights to prevent merged-cell overlap.

Material investigation finding:
- latest EnergyFlow snapshot showed PV=0 kW, Grid=0.567 kW,
  House=0.446 kW, battery voltage=51.4 V and BMS charging current=1.8 A,
  while mode=Mains Mode, working mode=SBU and charging priority=OSO.
- this is physically consistent with roughly 93 W of battery charging plus losses
  while grid power exceeded house load.
- treat this as diagnostic evidence of possible utility-supported battery
  maintenance/charging, not yet as a final configuration verdict.


## Consolidated QA correction build — GREEN — 2026-09-27

Validated code checkpoint:
- HEAD: `298844d1f200d41dd53b735ceaac9fc4ae28b3eb`;
- Windows Build 308 / run `36291695463`;
- restore PASS;
- build PASS;
- SQLite/reporting/source-attribution smoke PASS;
- portable win-x64 publish PASS;
- artifact upload PASS.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10922233048`;
- size: 78,686,064 bytes;
- expires: 2026-12-26.

This build consolidates the first high-coverage QA findings rather than issuing separate micro-builds:
- Home four-card true 60-second Live polling from Build 306;
- subtle synchronized 60-second progress line at the bottom of each Home Live card;
- progress indicator is only next-check timing, not source-frame age;
- Debug config-cache and batch-read probes send an empty JSON object instead of an absent POST body;
- Debug exports strict possible Grid→Battery candidate frames + summary;
- Debug exports parsed latest structured EnergyFlow interpretation;
- Excel Patrones event-summary merged rows receive explicit heights/wrap;
- PDF no longer mislabels reserve+grid episode duration as total observed time;
- duration is omitted when no episode exists.

Next manual validation remains one consolidated pass:
1. new portable with copied Data folder;
2. verify Home Live polling/progress behavior;
3. run complete Debug and return its ZIP;
4. export the same 7-day Simple Energy XLSX + PDF and return them.


## Target Live cadence and config-batch QA confirmation — 2026-09-27

User compared the Home Live cards against the official Solar of Things mobile app and confirmed:
- the new Home cards do refresh from current cloud state;
- the official mobile app itself receives/refreshes new inverter information approximately once every five minutes.

This supports the implemented model:
- client checks every 60 seconds while Home is visible;
- actual source-frame timestamps may remain unchanged across several checks;
- the 60-second progress line represents time until the next cloud check, not a promise of a new physical measurement.

The first Build 308 debug bundle also proved:
- config cache POST with an empty JSON body succeeds;
- direct batch config read starts successfully;
- the first details response can legitimately return isFinished=false.

Correction for next build:
- explicitly honor the isFinished boolean;
- poll batch details for up to 60 seconds at 1-second cadence;
- return WARN if still unfinished rather than falsely reporting success;
- run config-cache capture after the direct batch read so a populated post-read cache can be preserved if the backend provides it.


## Config-batch wait fix — GREEN — 2026-09-27

Validated code checkpoint:
- HEAD: `bd14dc7d198fe3e62ed2f0b0d79ac4505f79cd36`;
- Windows Build 309 / run `36292792063`;
- restore PASS;
- build PASS;
- SQLite/reporting/source-attribution smoke PASS;
- portable win-x64 publish PASS;
- artifact `10922976498`.

This build is the next target-PC diagnostic build.

It additionally incorporates the user-observed Live validation:
- Windows Home Live cards track official Solar of Things mobile state;
- official mobile/source frames advance roughly every five minutes;
- Windows polls every 60 seconds only to detect the next cloud frame sooner;
- the subtle card progress line represents time to next cloud check, not new measurement cadence.

Reporting status from the returned high-coverage XLSX/PDF:
- family report layout accepted for the current tranche;
- three charts present and coherent;
- Patrones overlap corrected;
- PDF reserve+grid duration label corrected;
- no report re-export is required solely for this config-batch fix.

Next manual test:
- use Build 309;
- verify the four 60-second progress lines;
- run only complete Debug;
- return the new investigation ZIP.


## Canonical target QA consolidation document — 2026-09-27

A dedicated canonical recovery document now exists:

`TARGET_QA_CONSOLIDATION_2026-09-27.md`

It consolidates:
- exact reviewed target artifacts;
- accepted 7-day report metrics;
- visual/product QA result;
- structured EnergyFlow evidence;
- repeated possible Grid→Battery maintenance candidates;
- full-history source-attribution checkpoint;
- current SBU/OSO/LBU state;
- Build 307 failure and Build 308/309 green recovery;
- Home Live cadence/progress semantics;
- the single next target action.

For new-chat recovery, read that document after this continuity file instead of reconstructing the QA from chat history.


## Exhaustive investigation + Live/update UX build — GREEN — 2026-09-27

Validated code checkpoint:
- HEAD: `1f942ad2435a2835ab78eea471b3b8f8f40aacda`;
- Windows Build 310 / run `36343560419`;
- restore PASS;
- build PASS;
- SQLite/reporting/source-attribution smoke PASS;
- self-contained win-x64 publish PASS;
- artifact upload PASS.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10939387805`;
- size: 78,698,067 bytes;
- SHA-256: `0975d5ccdcae0e65a1f85c4fd88ed69043f3a5c69b20a9f1750ab5ddeb49661c`;
- expires: 2026-12-26;
- run: `https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36343560419`.

Purpose of Build 310:
- supersedes Build 309 as the next target-PC test;
- does **not** change `SourceAttributionService` rules;
- turns complete Debug into an exhaustive read-only evidence capture intended to discover historical/current fields that may help reduce currently unattributed household-source energy.

New Debug research capture:
- preserves a research manifest and full sanitized raw request/response evidence;
- chooses up to four representative local days from the existing corpus:
  - latest available data;
  - strict possible Grid→Battery candidate;
  - simultaneous mixed-source frame;
  - detected historical battery-return configuration change;
- probes, read-only:
  - live gather-attribute metadata;
  - alternate remote latest-state surface;
  - selected-key history with the full commissioned catalog plus research aliases;
  - simple record-list v1;
  - full record-list v1;
  - full record-list v2;
  - non-simple keys/history;
  - daily generated-energy aggregate;
- successful and unsuccessful responses are both retained as evidence;
- no write/config mutation, cache clear, restart, passthrough or fast-report control is used.

Bundle additions:
- live source-frame cadence CSV + summary;
- latest research-run capture inventory;
- normalized JSON-path/field inventory across research requests/responses;
- complete sanitized raw research request/response files for the latest run.

Home Live:
- automatic check interval changed from 60 s to **150 s (2.5 min)**;
- the progress line now represents the 150-second client-check cycle;
- bundle cadence analysis separates client poll timestamps from actual inverter/cloud source-frame timestamps so future cadence can be chosen from measured evidence rather than assuming exactly five minutes.

Update Data UX:
- explicit three-stage presentation:
  1. current-state preparation;
  2. historical download with day/frame/sample progress;
  3. local normalization/context processing;
- clear terminal states for success, safe stop, warning and error;
- final status remains visible and includes the latest locally saved data timestamp;
- already-downloaded data remains preserved on Stop.

Next target action:
1. use Build 310 artifact `10939387805`;
2. copy the existing portable `Data\` folder as usual;
3. observe Home Live long enough to collect several 150-second checks / more than one real source frame if convenient;
4. optionally exercise `Actualizar datos` once to review the clarified UX;
5. run **Ayuda técnica → Abrir diagnóstico técnico → Ejecutar diagnóstico exhaustivo + paquete**;
6. return the new investigation ZIP.

Do not re-export XLSX/PDF and do not alter source-attribution rules until this new evidence is reviewed.


## Canonical build handoff rule — 2026-09-27

A new canonical operational rule is stored in:

`BUILD_HANDOFF_RULE.md`

From this checkpoint onward, whenever a build is handed to the user for target-PC/manual testing:
1. provide the **direct downloadable artifact in the chat first** whenever tooling permits;
2. then identify the build/commit/CI/artifact for traceability;
3. then provide the exact minimal test steps and required return evidence.

Do not require the user to navigate through GitHub Actions merely to obtain a build that the assistant requested them to test.

This rule applies to Build 310 immediately and to every later manual-test build.


## Build 310 target evidence review + reporting UX tranche — Build 316 GREEN — 2026-09-27

Returned target artifacts reviewed:
- `investigation-bundle-20260927-164316.zip`;
- `SolarEnergy_20260826_20260926.xlsx`;
- `SolarEnergy_20260826_20260926.pdf`.

User QA:
- the redesigned `Actualizar ahora` experience from Build 310 is accepted as materially clearer;
- Home was left running through multiple 150-second cycles;
- report UX/readability observations were returned together with XLSX/PDF.

### Investigation findings

The exhaustive Build 310 probe materially changed the historical-attribution evidence picture.

Selected-key history can return historical fields that are absent from the commissioned 87-key gather catalog when they are explicitly requested. Confirmed non-null on representative historical days:
- `workingMode`;
- `chargingPriorityOrder`;
- `pvEnergyFeedingPriority`;
- `mode`;
- `mainsCurrentFlowDirection`;
- `acChargingSwitch`;
- `solarChargingSwitch`;
- `chargingMainSwitch`;
- `powerSupplyFromPVToLoadInACState`.

The selected-key endpoint remained compact (~116–159 KB for the sampled full days) and is therefore preferred over using full `record/list/v2` for normal operation.

`record/list/v2` confirmed a much richer historical object:
- 160 fields observed;
- 73 fields outside the gather catalog;
- useful corroborating fields include `batteryStatus`, `gridConnectionSign`, `mainOutputRelayStatus`, explicit display mappings, and operating-mode state.

Direct-power aliases investigated such as `exchangeChargingPower`, `batteryPower`, `batteryChargeDischargeRealTimePower`, `gridConnectedPower`, `inputMainsPower` and `mainsInputRealTimePower` were present in the requested selected-key shape but returned null on the sampled target days. Therefore no direct historical Grid→Battery wattage field was found.

Historical state evidence:
- 2026-09-27: 130 sampled frames in `Mains Mode`, 77 in `Battery Mode`;
- Mains Mode frames had grid input and no battery discharge; many showed the small battery-charging behavior already suspected;
- Battery Mode frames in the sample had grid power = 0;
- 2026-08-18 sampled history was Battery Mode for the day and showed a historical PV-feeding-priority transition BLU → LBU.

Direct config-batch read is now proven end-to-end:
- `isFinished=true`;
- concrete `targetConfig` and `configAttributeStates` returned;
- examples include charger priority OSO, PV feeding priority LBU, output-source priority setting, 30 A maximum mains charging current, 20% restore-mains-charging battery threshold, 20/50/10 battery thresholds and grid-connection disabled.

Possible utility-supported battery-maintenance detector in the returned corpus:
- candidate frames: 4,907;
- gap-aware duration: 407.834 h;
- integrated derived battery charge: 375.908 kWh;
- integrated Grid-minus-House surplus: 363.250 kWh.
This remains diagnostic/corroborating evidence rather than a billing-grade source fact.

Live cadence evidence:
- 36 polls analyzed;
- 22 distinct source frames;
- 14 repeated polls;
- target-PC 150-second polling median ~157 s;
- when long app-off/session gaps are excluded, the normal distinct source-frame cadence clusters around ~300 s.
Keep 150 seconds as the current interactive check cadence for now.

### Historical-attribution implementation

Build 311 introduced the evidence-gated historical-context ingestion and passed CI.

Normal selected-key history now explicitly includes the useful operating-context keys above plus:
- `batteryStatus`;
- `gridConnectionSign`;
- `mainOutputRelayStatus`.

A versioned one-time enrichment backfill is automatically planned over already stored history. It is marked complete only after all planned days complete; later updates return to normal incremental behavior.

`SourceAttributionService` is now `hpvinv02.source-attribution.v2`:
- historical operating context is read directly from `history_sample`;
- latest-state context uses the actual source-frame timestamp rather than retrieval time;
- mode context is not projected more than 20 minutes beyond an observed frame;
- explicit grid-mode attribution requires actual `Mains Mode` plus the relevant SBU/PV-to-load evidence;
- an explicit `Battery Mode` frame with simultaneous grid power is left unresolved unless route evidence exists.

This is intended to reduce unattributed energy through newly available evidence while remaining more conservative in contradictory mixed-mode cases.

### Reporting/UI changes

The returned PDF visually confirmed:
- three charts compressed onto one page were too short;
- legends overlapped/competed with data;
- daily x-axis labels crowded at the right edge;
- the third line chart duplicated much of the first source-attribution chart;
- PDF should be optimized as a readable narrative, not mirror the Excel workbook.

The returned Excel confirmed that its overall exploration format is good, but long/wrapped rows need more automatic height.

Build 316 implements:
- report name is now independent from preset name;
- presets store/reload configuration only;
- export always uses the currently configured report, with no preset required;
- configured report name is used directly as the Excel/PDF title and output filename stem;
- each From/To date keeps a calendar field plus separate fast Month and Year selectors;
- export runs off the UI thread with a visible staged 1/3 → 3/3 progress line and explicit terminal state;
- Excel applies larger automatic minimum row heights based on content length;
- report charts are now non-redundant:
  1. stacked household consumption by source;
  2. solar production vs household consumption;
  3. estimated stored battery energy at bucket end;
- chart PNGs are taller (1200×520);
- daily x-axis labels use shorter dates and fewer major ticks;
- PDF spreads chart content across pages:
  - page 2: source-attribution story + one large chart;
  - page 3: production/consumption and stored-battery evolution;
  - page 4: patterns/events;
  - page 5: technical quality/glossary.

Validated final code checkpoint:
- code HEAD: `1699ce50ade5f3202116475678f2e45b30773bff`;
- Windows Build 316 / run `36347515656`;
- restore PASS;
- build PASS;
- smoke PASS;
- portable publish PASS;
- artifact upload PASS;
- artifact ID: `10941500961`;
- SHA-256: `3bdb51301a3be7f8491ff4f9f7513b6723649873ad39d5992521350624e04aa3`.

Next target-PC validation:
1. use Build 316;
2. reuse the existing portable `Data\` folder;
3. run `Actualizar datos` once and allow the one-time historical context enrichment to finish;
4. generate one representative XLSX + PDF from current configuration **without requiring a preset**;
5. return both report files;
6. return a new investigation ZIP after enrichment so attribution coverage/reasons can be compared against v1.

No need to repeat the old Build 310 exhaustive API-discovery probe; its purpose is complete.


## Build 317 — PDF Page 1 family-card redesign — GREEN — 2026-09-27

Trigger:
- target-PC review found the XLSX family summary substantially easier to scan than PDF Page 1;
- PDF Page 1 was still a plain two-column value table with large unused whitespace, despite the canonical requirement that PDF mirror the Excel family hierarchy.

Implemented:
- replaced the Page 1 value table with a family dashboard/card hierarchy;
- first row: total household consumption + percentage of source attribution identified;
- second row: three source cards side-by-side:
  - Enel / grid → house;
  - direct solar → house;
  - battery → house;
  - each card also shows its share of observed household consumption;
- third row: three night-status cards:
  - SIN PROBLEMAS DE ALIMENTACIÓN;
  - QUEDAMOS CORTOS;
  - SIN DATOS SUFICIENTES;
- typical reserve-arrival time remains visible when available;
- Page 1 retains the conservative explanatory note about what qualifies as a shortfall;
- PDF metric cards now use a light card background and subtle border so Page 2 uses the same visual language.

Validation:
- code commit: `0ee5cdd52bf5756d27f95ac750de1f82114ecaec`;
- Windows Build 317 / run `36348470736`;
- restore PASS;
- build PASS;
- smoke PASS;
- portable publish PASS;
- artifact upload PASS;
- artifact ID: `10941730597`;
- SHA-256: `29513638634efb86e8777970bfe4cda059aa6f8a7c5590c8c5a70a55c627d986`.

Build 317 supersedes Build 316 for the next target-PC report validation. All Build 316 attribution/history/reporting changes are included.

Next target action remains:
1. reuse existing portable `Data\`;
2. run `Actualizar datos` once and allow the one-time attribution-context enrichment to complete;
3. generate one representative XLSX + matching PDF directly from current report configuration;
4. return XLSX + PDF + new investigation ZIP.

Visual QA must explicitly compare PDF Page 1 against the Excel family summary/card hierarchy.


## Build 325 — separate utility-use days from nighttime reserve episodes — GREEN — 2026-09-27

Trigger from returned Build 317 artifacts:
- PDF/XLSX period 2026-08-26 through 2026-09-26 showed six `quedamos cortos` episodes;
- user correctly observed many more daily bars containing Enel→House and a special 2026-08-26 day that was almost entirely supplied by Enel.

Reconciliation from returned XLSX/diagnostic evidence:
- days in report period: 32;
- days with Enel→House > 0.01 kWh: 15;
- of those, 14 are mixed-source **daily totals**;
- 1 day is near-exclusive Enel by daily household-source attribution:
  - 2026-08-26;
  - Enel→House ≈ 13.54 kWh of ≈ 13.56 kWh observed house consumption;
  - ≈99.9% of household consumption;
- six nighttime reserve+grid episodes are a subset of the 15 Enel-use days:
  - 2026-08-28;
  - 2026-08-31;
  - 2026-09-02;
  - 2026-09-09;
  - 2026-09-11;
  - 2026-09-12;
- additional mixed Enel-use days without a reserve-night episode:
  - 2026-08-27;
  - 2026-09-10;
  - 2026-09-19;
  - 2026-09-20;
  - 2026-09-23;
  - 2026-09-24;
  - 2026-09-25;
  - 2026-09-26;
- 2026-09-20 has only ≈0.72 kWh Enel→House and can be visually easy to miss in the stacked daily chart.

2026-08-26 frame evidence in returned diagnostic:
- 286 observable power-balance frames for that local date;
- positive grid import in all 286;
- battery discharge in 0 frames;
- battery charge >50 W in 262 frames;
- therefore this is not semantically a `battery ran short at night` event even though Enel supplied essentially the entire day's house consumption.

Root cause of prior reporting ambiguity:
- `FamilyReportAnalysisService.DetectReserveGridEvents` intentionally detects only nighttime (18:00–09:00) frames with:
  - grid >=100 W;
  - PV absent/insufficient;
  - SOC at/below normal grid-transfer threshold;
  - repeated samples;
- therefore the six count was correct for that narrow event family, but the report presentation made it too easy to interpret it as the count of all Enel-use occurrences.

Build 325 correction:
- do not change the reserve-event detector;
- add daily source-attribution view to `EnergyReportData` independent of selected report aggregation;
- add Excel sheet `Uso de Enel` / `Utility Use`;
- add PDF Page 4 `Uso de Enel / red`;
- show:
  - days with Enel→House;
  - mixed-source daily totals;
  - near-exclusive Enel days (>=99% of observed household consumption);
  - which days also contain a nighttime reserve+grid episode;
- explicitly state that a mixed daily bar means several sources contributed within the day and does **not** prove simultaneous supply;
- Page 1 now states that `quedamos cortos` is not the count of every day with Enel;
- Page 2 chart note now explains the same distinction;
- the old event table is renamed `Episodios nocturnos de batería en reserva + red`;
- PDF sequence becomes:
  1. household dashboard;
  2. whole-energy story + source chart;
  3. solar/home/battery evolution;
  4. utility/grid-use days;
  5. patterns + nighttime reserve episodes;
  6. technical annex.

Validation:
- code commit: `6c8835e7fe55be70d192a803052fd34144e4b734`;
- Windows Build 325 / run `36351556974`;
- build PASS;
- smoke PASS, including required `Utility Use` workbook sheet;
- portable publish/upload PASS;
- artifact ID `10942945322`;
- SHA-256 `f6217e693154e662944afb74660dd384c45aab662dfdb33c3ec82f1a5d0a51bb`.

Build 325 supersedes Build 317 for the next report validation.


## Build 329 — clarify utility bill comparison + exact report window — GREEN — 2026-09-27

Trigger:
- report showed `Enel → Casa = 81.71 kWh` and `Importación total desde Enel = 94.54 kWh` without enough visual explanation;
- user requested the billing-comparable figure duplicated on Page 1 / first Excel summary page;
- report date range needed explicit start/end times, not dates only.

Canonical semantics:
- `Enel → Casa`: household-consumption attribution only; **do not compare directly with Enel bill/meter**;
- `Importación total desde Enel`: all measured energy entering the system from the utility; **this is the report metric to compare with Enel bill/meter**, provided the bill uses the same time window and report coverage is considered;
- the difference may include battery charging/maintenance and internal conversion/consumption/losses and is not automatically assigned to one destination.

Reporting changes:
- PDF Page 1 keeps the existing `Enel → Casa` card and explicitly labels it as not the billing figure;
- PDF Page 1 adds a separate large `Importación total desde Enel — comparar con medidor/boleta` card;
- PDF Page 2 retains its total-import card and now explains the same distinction;
- Excel Summary Page 1 keeps `Enel → Casa`, adds a separate total-import card, and retains the existing Page 2 total-import card;
- both PDF and Excel glossaries define the two concepts independently;
- PDF header shows selected dates plus exact local comparison window;
- Excel Summary shows exact local comparison window;
- technical Excel reports show the exact local comparison window directly.

Window semantics:
- date-range reports use complete local civil days;
- example 2026-08-26 through 2026-09-26 displays as `26-08-2026 00:00:00 — 26-09-2026 23:59:59` in the station time zone;
- report explicitly states that the final day is included in full.

Validation:
- code commit: `b5997c63e1b80697a500298c6cd742bf7ff133e4`;
- Windows Build 329 / run `36353099852`;
- restore PASS;
- build PASS;
- smoke PASS;
- publish PASS;
- artifact upload PASS;
- artifact ID: `10942618101`;
- SHA-256: `5a57a9d0359a2e49ea66a1f46dbe4819ea3fc6c288dd0953bfe6759989a5cd73`.

Build 329 supersedes Build 325 for the next report validation.


## Formal transition — Phase 7 CLOSED / Phase 8 STARTED — 2026-09-27

User decision:
- close Phase 7 now;
- proceed with Phase 8.

Phase 7:
- status: COMPLETE / ACCEPTED;
- Build 329 remains the final reporting checkpoint entering Phase 8;
- its deferred XLSX/PDF re-export is optional/non-blocking.

Phase 8 first code checkpoint currently on main:
- schema upgraded to v9;
- new persistent `utility_meter_reading` and `utility_bill` tables;
- cumulative meter readings preserve exact timestamps;
- optional bill period/consumption/amount/reference/notes supported;
- reconciliation compares cumulative-meter consumption against Solar of Things **total grid import** over the exact same interval;
- signed/absolute/% difference and data coverage are calculated;
- dedicated Grid & Utility page replaces the prior placeholder;
- deterministic Phase 8 smoke added.

Phase 8 target acceptance will require real meter data; no invented meter reading is acceptable.


## Build 342 — Phase 8 evidence-model expansion — GREEN — 2026-09-27

Target feedback from the first real Grid & Utility test has been incorporated.

Current Phase 8 code checkpoint:
- code commit `7ac676812497679402beb4593773b3cd3c3c6419`;
- Windows Build 342 / run `36361589847`;
- artifact `10945173432`;
- SHA-256 `eb877687fb5491e1ae662caa4acc9f9c36e309f6730dd24590902e461df040aa`;
- CI GREEN.

Key changes:
- schema v10;
- official/date-only Enel readings separated from personal/exact-time readings;
- visible time-boundary assumption for date-only utility evidence;
- migration of recognizable prior Phase 8 reading provenance without requiring re-entry;
- arbitrary reading-pair reconciliation;
- consecutive reconciliation retained as overview;
- reusable quick day/month/year selector used transversally outside Reports;
- utility bills can link directly to saved reading pairs;
- expanded bill totals;
- flexible signed bill-line evidence;
- PDF reconciliation report for external/utility technical review.

No personal meter values or bill amounts are stored in repository documentation.

Next target gate:
- validate Build 342 against the user's existing real `Data\`;
- confirm migrated official/personal provenance;
- compare at least one arbitrary official reading pair;
- link/recreate one bill using its two official readings;
- store representative bill lines without forcing arithmetic reinterpretation;
- export and return one reconciliation PDF for visual/semantic QA.

Phase 9 official-tariff acquisition and Phase 10 bill reconstruction remain queued immediately after Phase 8 acceptance.


## Build 349 — current checkpoint — GREEN — 2026-09-27

Artifact:
- Build 349;
- code `74a69282fa912c148ca8dedbed6495018bfd0560`;
- run `36363960224`;
- artifact `10946815978`;
- SHA-256 `cbdd9cb34bba48afc9056e799d30e59bdd1dcaad59c283ecd64542afe783e10d`.

Changes since Build 342:
- mouse wheel scrolls outer application pages, including when pointer is over nested DataGrids;
- reconciliation PDF is redesigned for external readability and evidence traceability;
- tariff-source evidence capture begins Phase 9:
  - schema v11;
  - official Enel archive discovery;
  - official 2026 tariff PDFs cached locally;
  - hashes/metadata/effective month/retroactivity/page text persisted;
  - capture status visible in Grid & Utility.

Next target QA:
1. reuse current real `Data\`;
2. confirm wheel scrolling over tables;
3. export reconciliation for the **official-to-official bill pair**, not an unrelated personal-reading endpoint;
4. review the new two-page PDF;
5. run `Capturar / actualizar fuentes oficiales 2026`;
6. report publication count/failures and confirm retroactive July/August entries appear.

Next development tranche after QA:
- normalize official tariff components and resolve service applicability/version/supersession;
- only then compute tariff-aware expected bill components.

## Build 349 target feedback — 2026-09-28 — must not be lost

The Build 349 target-PC QA exposed a product-boundary issue in Grid & Utility. The current generic reading-pair reconciliation is useful for personal investigation, but it is not sufficient for an Enel-facing bill audit.

Observed exported evidence:
- one generated reconciliation used an official Enel reading at 2026-08-27 as the start and a personal reading at 2026-09-27 17:56 as the end;
- because the pair did not exactly match a stored bill, the PDF correctly stated that no bill was linked, but the UI made it too easy to produce the wrong audit artifact;
- the same report showed 97.400 kWh from meter difference vs 84.080 kWh from Solar of Things, a -13.320 kWh / 13.68% difference, with 99.3% telemetry coverage and at least one assumed boundary time;
- this numerical difference must NOT be treated as proof of an Enel billing error.

Required redesign for the Enel-facing audit:
1. keep personal/free reconciliation as its own tool;
2. add a separate bill-specific audit/export that begins from one selected bill and automatically uses only its linked official readings / billing interval;
3. audit energy, tariff components and monetary charges together;
4. add explicit uncertainty accounting beyond telemetry coverage:
   - time-boundary uncertainty;
   - measurement/sensor uncertainty when defensible evidence exists;
   - confidence interval or sensitivity range for the Solar of Things interval total;
   - an appropriate statistical/sensitivity comparison before classifying a discrepancy as materially inconsistent;
5. explain in plain language whether the observed difference is compatible with known uncertainty rather than equating “different totals” with provider error;
6. investigate and model how an Enel bill is actually constructed, preserving every bill component and effective tariff version needed to reproduce it;
7. make the resulting PDF suitable as a traceable technical document for discussion with Enel, while clearly distinguishing certified meter evidence from independent inverter telemetry.

Tariff acquisition defect / scope correction:
- Build 349 target capture returned 0/0 publications;
- the current implementation is hard-coded to 2026 in discovery, effective-date parsing, storage path and UI;
- official tariff acquisition must be investigated and generalized to historical years required by stored bills, not only the current calendar year;
- capture alone is still not “tariff applied to this bill”; normalization, service applicability, version/supersession and effective interval remain required.

These requirements are the next Grid/Utility functional tranche and are not to be silently collapsed into the generic personal reconciliation.

## Product-experience tranche queued for the next build — 2026-09-28

Approved for the next code build:
- permanent version + CI build + source revision identifier in the footer and About page;
- wider default main window;
- visible busy state for every export path;
- startup “opening” indicator;
- new About navigation page with a What's New dialog and patch notes;
- new Help navigation page with an integrated manual and PDF export for one section or the complete manual;
- guided “Import data from another installation” flow:
  - accepts the prior installation root or Data directory;
  - stages the copy with visible progress;
  - restarts before SQLite opens;
  - moves the current Data to a timestamped backup before applying the import;
  - preserves imported Secrets;
  - when imported DPAPI-protected session material is decryptable by the current Windows user, automatic reconnection is enabled.

The Enel audit/statistics/tariff redesign above remains a separate substantive tranche after this product-experience build.

## Build 350 — product experience / portability checkpoint — GREEN — 2026-09-28

Code commit:
- 45f1811a549d0d8c5c15823c4d67f255eeadcdc3

Windows Build:
- run 350 / ID 36454383190
- conclusion: SUCCESS
- restore: PASS
- build: PASS
- SQLite smoke: PASS
- self-contained win-x64 publish: PASS
- artifact upload: PASS

Portable artifact:
- SolarEnergyMonitor-win-x64-dev
- artifact ID: 10984702443
- SHA-256: 8c23ee335ed6824214b67f4ff02efbd23bd6418e86932d63b9a260e5e21bd631
- expires: 2026-12-27

Implemented:
- product version 0.10.0 with CI build number and short source revision in assembly informational version;
- footer and About page display the exact running version/build/revision;
- default main window widened to 1440x820;
- startup splash with visible opening/import/database/UI stages;
- global busy indicator in the footer;
- generic utility reconciliation PDF export now runs asynchronously with visible export state;
- report Excel/PDF export also mirrors its in-page progress into the global busy indicator;
- new About page with What's New patch notes;
- new Help page with integrated bilingual manual;
- Help can export the selected section or the complete manual to a readable PDF;
- new guided import from another installation:
  - select prior installation root or Data folder;
  - stage all Data files with byte/file progress;
  - restart before SQLite opens;
  - move current Data to a timestamped backup;
  - apply imported Data atomically by directory move;
  - preserve imported Secrets;
  - if the imported DPAPI session is decryptable, enable automatic reconnect.

Target QA for Build 350:
1. confirm footer shows v0.10.0 · Build 350 · 45f1811a;
2. confirm the wider initial window removes the common horizontal clipping;
3. confirm the startup opening indicator appears before the main window;
4. open About -> What's New and review the patch notes;
5. open Help, switch sections, export one section to PDF and export the full manual to PDF;
6. verify report export and generic reconciliation export both show visible activity while running;
7. test Import data from another installation using a safe copy/previous portable folder:
   - observe progress;
   - allow restart;
   - confirm the old current Data was backed up under Backups;
   - confirm the imported database opens;
   - confirm remembered Solar of Things login reconnects automatically when the imported DPAPI secrets are valid for the current Windows user.

The separate Enel bill-audit / statistical uncertainty / historical tariff acquisition redesign remains the next substantive Grid & Utility tranche.



## Build 350 target-PC QA feedback — 2026-09-28 — recorded before fixes

User priority for this checkpoint:
- preserve the observed results immediately in the repository;
- do not implement fixes yet;
- review/correct the issues only after the observations are safely recorded.

Results against the Build 350 target QA list:

1. Build identity / footer: **PASS**.
   - Version/build/revision presentation accepted.

2. Startup / window behavior: **PARTIAL / ISSUE RECORDED**.
   - Startup indicator appears.
   - During startup it appears blocked/frozen and does not visibly advance through progress.
   - After startup the main application opens minimized.
   - The initial window width is otherwise sufficient; horizontal width/clipping is not currently a concern.

3. About / What's New: **PASS**.

4. Help / manual / PDF path: **FUNCTIONALLY PASS, CONTENT DEFERRED**.
   - Help flow works.
   - Current manual/help content is incomplete.
   - User explicitly wants comprehensive Help/manual content completion deferred until the end of the overall project, rather than expanded now.

5. Export activity indicator: **NOT TESTED**.
   - Cannot be validated on the current target-PC state because usable data is unavailable.
   - Do not infer PASS or FAIL.

6. Guided import discoverability: **BLOCKED / UX ISSUE RECORDED**.
   - User cannot locate the import button/entry point.
   - Treat this as a discoverability/navigation finding.
   - Do not implement a fix yet.

7. Import execution / restart / restored data: **NOT TESTED**.
   - Cannot currently validate the import flow end-to-end.
   - User expectation recorded: after importing, the application should refresh/restart/reinitialize itself appropriately so imported data becomes the active state without requiring an unclear manual recovery sequence.
   - Do not implement or redesign this behavior yet.

Build 350 QA status after this feedback:
- not closed;
- PASS: 1, 3;
- partial/issue: 2;
- functional path accepted but content intentionally deferred: 4;
- not testable now: 5, 7;
- blocked by discoverability: 6.

Deferred work explicitly recorded:
- fix startup progress/frozen appearance and unintended minimized launch;
- review import entry-point discoverability;
- validate and, if needed, refine post-import application refresh/restart behavior;
- complete Help/manual content comprehensively only near final product completion;
- re-run export/import QA when target-PC data/environment is available.

No code fix is authorized by this QA note itself.


### Build 350 QA follow-up — import entry point located

The user has now located the guided import control under the Data page.

Update to item 6:
- no longer blocked;
- the earlier finding remains useful as initial discoverability friction, but the control is confirmed present and reachable;
- execution behavior is still pending validation.

Planned remaining manual test order:
1. item 6 — import discoverability/execution entry point;
2. item 5 — export activity indicator, after data is available;
3. item 7 — end-to-end import/restart/active-data validation.

No fix authorized yet.


### Build 350 QA follow-up — items 6 and 5 validated

Manual target-PC results:

6. Guided import entry point / start of import flow: **PASS**.
   - User located the control and confirmed the entry path works.
   - Earlier discoverability friction remains recorded as UX feedback, but this item is no longer blocked.

5. Export activity indicator: **PASS**.
   - User confirmed the export activity feedback behaves as expected once data was available.

Remaining Build 350 manual validation:
- item 7 — end-to-end import/restart/active-data validation.

No code fix authorized by this update.


### Build 350 QA completion + Grid/Utility UX requirements — 2026-09-28

Final remaining Build 350 manual result:

7. End-to-end import / restart / active-data validation: **PASS**.

Build 350 QA status:
- 1 PASS;
- 2 PARTIAL / issue recorded (startup progress appears blocked/frozen; app opens minimized; width otherwise acceptable);
- 3 PASS;
- 4 functional PASS, Help/manual content intentionally deferred until near final product completion;
- 5 PASS;
- 6 PASS, with earlier discoverability friction retained as UX feedback;
- 7 PASS.

Build 350 is therefore functionally validated for the tested product-experience tranche, with the recorded startup/minimized-launch issue and deferred Help-content completion still open.

#### New target-PC feedback — Enel / Grid & Utility must be redesigned before the next substantive build

The following user requirements must be preserved before further Grid/Utility development:

1. No meaningful Enel-specific QA has yet been completed.
   - Do not treat the current Enel/tariff/report path as accepted.
   - Previously planned report changes related to Enel/bill audit still require explicit design and validation.

2. Readability/navigation is poor inside the current combined options.
   - Where concepts are distinct, separate them into tabs or clearly separated task surfaces.
   - Avoid presenting personal readings, Enel official readings, bills, tariff capture and reconciliation as one dense undifferentiated workflow.

3. Responsive/layout behavior is not currently adequate in many parts of the UX.
   - Analyze layout behavior, clipping, density, scrolling and control grouping before the next substantive build.
   - Correct the UX where information becomes unreadable, overly compressed or visually confusing.

4. Official tariff capture still does not work on the target PC.
   - This remains an unresolved functional defect.
   - Do not mark tariff acquisition as complete.

5. Tariff capture must not be hard-coded to 2026.
   - The user needs tariff acquisition driven by a selected period / bill history and capable of retrieving historical periods beyond the current year.
   - At minimum, the UI must allow the user to request the relevant period(s) rather than presenting a fixed “2026” operation.

6. Bill/period comparison is currently extremely confusing.
   - The product goal is to let the user compare:
     - arbitrary personal meter readings;
     - official Enel readings;
     - one or more Enel bills / billing periods;
     - Solar of Things inverter-derived grid import over the comparable interval;
     - the official tariffs applicable to those periods;
     - expected/reconstructed bill components versus actual Enel bill components.
   - The tariff and bill-structure evidence previously supplied by the user exists specifically to support this workflow.
   - The next design must prioritize intuitive comparison and traceability rather than exposing raw internal entities as the primary UX.

7. Enel boundary-time convention must be modeled explicitly:
   - for Enel-style date-only boundaries, date X at 00:00 is operationally equivalent to date X-1 at 23:59 for interval-boundary interpretation;
   - the implementation/reporting model must represent this convention consistently rather than making the user manually reason about an apparent one-day discrepancy.

8. Before the next substantive Grid/Utility build, produce a UI/wireframe proposal and verify the intended workflow with the user.
   - If material ambiguities remain, ask targeted product questions before implementation.
   - Do not silently assume the current UI model is the intended final interaction design.

No Grid/Utility fix/build is authorized by this note itself. This note records the target-PC product requirements that must drive the next design tranche.


### Grid/Utility redesign clarification — 2026-09-28

Additional user requirements before the next substantive implementation tranche:

1. **Tabbed task separation is a general UX rule, not only a Grid/Utility exception.**
   - Relevant application windows/pages with multiple distinct user tasks should use tabs or equivalent clearly separated task surfaces.
   - Avoid long vertically stacked pages that combine unrelated workflows.
   - Apply this rule wherever it materially improves readability and navigation.

2. The user wants **two distinct comparison/report workflows**:
   - **Reading vs inverter**:
     - choose utility/personal meter readings;
     - compare meter-derived consumption against Solar of Things inverter-derived total grid import over the same interval;
     - generate its own report.
   - **Enel bill vs inverter / reconstructed bill**:
     - start from one actual Enel bill;
     - compare the bill interval and energy evidence against Solar of Things;
     - reconstruct verifiable tariff-based components;
     - generate a separate bill-audit report.

3. For official Enel readings, the user prefers **no editable time field**.
   - Store/display them as date-only interval boundaries.
   - Apply the canonical boundary convention internally: date X at 00:00 is equivalent to the end of X-1 for interval interpretation.
   - Do not imply that Enel supplied an exact clock time.

4. Monetary audit must be **line/component aware**, but only where evidence supports reconstruction.
   - Components such as electricity supply/energy charge and transport/network charges should be reconstructed when official tariff evidence/rules permit.
   - Items that are not derivable from the tariff/rules (for example certain common-service or external charges) must remain actual-bill evidence and must not be fabricated.
   - Total-only comparison is insufficient.

5. Real Enel bills already reviewed are considered broadly sufficient as representative examples for the product design.
   - VAT is 19% of the taxable base; determining which reconstructed components belong to that taxable base is a research/rules problem and must not be guessed.
   - Prior tariff/bill-structure research should be used to define this correctly.

6. **QA cadence rule**:
   - implementation may go through multiple internal build/CI iterations;
   - do not ask the user to perform many small/manual mini-QA cycles;
   - only hand off a build for target-PC QA when a coherent tranche is ready and the checks can be bundled into one meaningful validation pass;
   - intermediate builds may be used for development/CI without user involvement.

7. The UX proposal must be updated so tab/task separation is applied consistently across all materially complex pages, not only Red eléctrica / Grid & Utility.

Two product questions remain intentionally open and must be asked in plain language before implementation:
- how the app should automatically connect a bill with the correct saved Enel readings;
- how the tariff-download date range should be chosen by default.

No code implementation is authorized by this note alone.


### Grid/Utility product decisions closed — 2026-09-28

Two previously open product questions are now resolved:

1. **Automatic bill-to-reading matching: YES.**
   - When an Enel bill is added/selected, the application should automatically search the saved official Enel readings that best match the bill period.
   - The UI should propose the matching start/end readings.
   - The user may confirm or correct the proposed linkage when needed.
   - The default workflow should not require manually hunting for both readings every time.

2. **Official tariff acquisition UX: year-based.**
   - The user prefers selecting a calendar year.
   - The application should then discover/download/update **all official tariff material it can obtain for that year**.
   - The action must not be hard-coded to 2026.
   - Re-running the same year should update/complete the local evidence set, including newly discovered, corrected, retroactive or superseding official publications where applicable.
   - The UI should expose per-year acquisition status and failures clearly.

These decisions supersede the earlier proposal in which the default tariff range was derived from saved bills. Bill-driven tariff applicability remains required for audit/reconstruction, but acquisition itself should be initiated by explicit year selection.


### Retroactive tariff verification rule — 2026-09-28

User requirement:

If an official tariff publication changes a tariff retroactively, the application must explicitly verify and resolve that change.

Required behavior:
- year-based tariff acquisition must detect multiple publications that affect the same effective period;
- preserve every official publication and its provenance rather than overwriting prior evidence;
- identify retroactive/corrective/superseding publications;
- determine which publication/version is authoritative for each affected effective interval;
- re-evaluate any bill audit/reconstruction whose period is affected by a later retroactive correction;
- make the applied tariff version and the superseded version(s) traceable in the UI/report;
- never silently keep using an older locally cached tariff after an official retroactive replacement is discovered.

A tariff PDF being newer by publication date is not by itself sufficient: applicability must be resolved from effective dates, retroactive language/version relationships and the official source evidence.

This rule applies both when first acquiring a year and when re-running an update for a year already stored locally.


### Transversal visual-feedback + pre-build review rules — 2026-09-28

User requirements now apply across the application:

1. **Every materially non-instant process must provide visible on-screen feedback while work is occurring.**
   - This applies transversally to exports, imports, data updates/backfills, tariff acquisition, diagnostics generation, report generation, long calculations, migrations/reloads where visible, and any other operation that can make the UI appear idle/frozen.
   - The Reports export behavior is the reference interaction: the user must be able to tell that work is actively in progress.
   - Prefer one consistent global busy/progress language plus local contextual progress when useful.
   - Avoid silent work and avoid making Windows look hung.
   - Where determinate progress is available, show it; otherwise show an indeterminate activity indicator with a clear action label.
   - Completion/failure should also provide visible feedback rather than silently returning to idle.

2. **Graphical attractiveness / visual quality requires an explicit review pass.**
   - Evaluate the application not only for correctness but for perceived polish, hierarchy, readability, spacing, density, consistency, visual balance, empty states, interaction affordance and modern Windows usability.
   - The current app should move from a functional engineering/dashboard prototype toward a coherent, attractive desktop product without sacrificing data density or auditability.
   - Findings/recommendations must be documented and then applied incrementally during the UX redesign.

3. **Pre-build / pre-fix observation review is mandatory.**
   - Before every new build, fix, redesign tranche or similar code change, review the prior relevant target-PC observations and canonical product rules in the repository.
   - Do not implement a local fix in isolation if it reintroduces or contradicts an earlier requirement.
   - Relevant observations include at minimum the latest Build QA notes, phase implementation notes, UX redesign proposal, tariff/audit rules, and any still-open target-PC defects that intersect the files/features being changed.
   - This review is a development gate, not a user task.

4. **User-facing QA cadence remains bundled.**
   - Multiple internal code/CI builds and fixes are encouraged.
   - Do not hand each internal iteration to the user.
   - The next target-PC handoff should bundle a coherent UX/functional tranche and a concise meaningful QA checklist.

These rules are effective immediately for the work leading to the next target-PC QA.


## Internal development checkpoint — Build 386 GREEN — 2026-09-28

This is an **internal development checkpoint, not a target-PC QA handoff**.

Latest validated aggregate code through:
- responsive summary-card layout commit `88aa6fd1d0b067e7ff8a8e4d286c3ca45bb1d505`;
- Windows CI Build 386: SUCCESS.

Implemented since Build 350 target feedback:
- startup no longer intentionally blocks the UI thread during import/startup staging; main window is explicitly restored/activated;
- transversal visible work feedback expanded to:
  - startup data preparation;
  - Update Data/history sync;
  - manual current-state refresh;
  - official tariff capture;
  - report/help/reconciliation/bill-audit exports;
  - guided data import;
  - commissioning/discovery;
  - developer diagnostics;
- year-selectable Enel official tariff acquisition replaces fixed 2026 UI;
- source discovery now falls back to official PDF filename/URL when anchor text is generic (for example “Descargar”);
- tariff source smoke covers:
  - selected-year filtering;
  - realistic URL-encoded official filenames;
  - retroactive publication detection;
  - multiple publications affecting one effective month;
- complex pages now use task tabs where materially useful:
  - Analysis;
  - Battery;
  - Grid & Utility;
  - Data;
  - Reports;
- card visual language refined (spacing/radius/border);
- summary-card grids adapt from 4 columns to 2x2 / 1-column layouts as usable width narrows;
- official Enel reading entry hides the editable time control and is displayed as a date boundary;
- bill dates can automatically propose matching saved official Enel start/end readings, while remaining user-correctable;
- reports are now separated semantically:
  - arbitrary reading comparison PDF;
  - dedicated bill-first Enel audit PDF;
- bill-audit export starts from a selected stored bill and requires linked official readings;
- audit PDF preserves actual bill lines and does not fabricate expected tariff charges while applicability remains unresolved.

Still not ready for target-PC QA:
- tariff source capture must be exercised against the real Enel site after the discovery fix;
- normalized tariff candidates/applicability/supersession resolution are not yet implemented;
- bill audit does not yet reconstruct verified tariff components line-by-line;
- explicit uncertainty/sensitivity interval is still pending;
- final visual polish remains pending after functional layout stabilizes.

Canonical next design:
- `TARIFF_NORMALIZATION_DESIGN_2026-09-28.md`.

Do not hand Build 386 to the user merely because it is green. Continue internal iterations until the Enel/UX tranche is coherent enough for one bundled target-PC QA.


## Target-PC QA candidate — Build 441 GREEN — 2026-09-28

Build 441 is the next **bundled target-PC QA candidate**.

Validated build:
- code commit: `e4ae3b4700d3a6f4be2401f57aec92158998f6ac`;
- workflow run: `36490027783`;
- CI: GREEN;
- restore/build/smoke/publish/artifact upload: PASS;
- artifact: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11001317265`;
- SHA-256: `3b957efe709f82b955c5dc9ef087ba4db0fd1b714c211584da85fd7743e7cba3`.

This candidate supersedes the prior “continue internally; do not hand off yet” checkpoint.

Included in the QA tranche:
- responsive/foreground startup changes;
- transversal visible busy/progress feedback;
- task tabs across materially complex pages;
- responsive summary-card layout;
- semantic primary/secondary/destructive action styling;
- consistent dense-table styling;
- persistent active-sidebar accent;
- date-only official Enel reading UX;
- automatic bill-to-reading proposal;
- year-based official Enel tariff acquisition;
- source discovery compatible with generic Download/Descargar anchors;
- immutable official PDF/hash/page-text evidence;
- normalized BT1 tariff candidates;
- retroactive/multi-version resolution states;
- schema v13 allowing distinct personal/official reading evidence at the same timestamp;
- data/boundary sensitivity range;
- separate reading-comparison and bill-audit PDFs;
- bill-first audit preview;
- conservative printed-rate verification against official candidates;
- human-readable official publication traceability;
- parser v2 algebraic classification of `POWER_BASE_DISTRIBUTION` and `ELECTRICITY_CONSUMED` only when the official (6)=(3)+(4)+(5) identity is satisfied;
- corrected 2026 ETR evidence: T1–T6.

Known intentional limits entering QA:
- customer commune/service column, RED and ETR are **not** silently inferred;
- historical territorial column maps are not assumed identical across 2020–2026;
- unsupported/account-specific bill lines remain actual-only evidence;
- FET/tax treatment is not fabricated;
- Help/manual completeness remains deferred until near project end.

Canonical manual QA checklist:
- `TARGET_QA_BUILD_441_2026-09-28.md`.

Do not request the previously accepted family-report or exhaustive-diagnostics QA again unless a new defect specifically requires it.

## Build 441 target-PC partial QA — tariff gate failed — 2026-09-28

User results:
- item 6 official tariff acquisition: **FAIL** — target UI remains blank/no tariffs downloaded;
- item 7 bill audit: **NOT TESTABLE**, blocked by missing tariff evidence;
- item 8 exports: **PARTIAL** — reading-comparison PDF returned; bill-audit PDF unavailable because audit path is blocked.

The returned reading-comparison PDF confirms the intended semantic separation:
- physical reading comparison, explicitly not a bill audit;
- 27-08-2026 Enel date boundary -> 27-09-2026 17:56 personal reading;
- meter consumption 97.400 kWh;
- Solar of Things total grid import 84.080 kWh;
- raw difference -13.320 kWh / 13.68%;
- coverage 99.3%;
- explicit sensitivity range 84.080–98.032 kWh;
- Enel date-boundary caveat preserved.

New live defect:
- fixture-based discovery/smoke is insufficient;
- the real Enel archive integration still returns no usable publications on the target PC;
- Phase 9 source acquisition remains open and is now the first blocking defect before further bill-audit QA.

Applicability clue supplied by user:
- service location Ñuñoa / Villa Olímpica, apartment building;
- use only to research official service/territorial mapping;
- never infer RED/ETR/tariff applicability from this clue without official evidence.

QA cadence:
- after fixing tariff acquisition, re-test only the blocked tariff/audit subset unless the fix materially affects another prior check.


## Live Enel acquisition root cause — Imperva challenge — 2026-09-28

Build 441 target failure was reproduced independently from a Windows CI runner using the same .NET acquisition path.

Observed live-source evidence:
- Enel archive request returns HTTP 200 but only a ~6.2 KB HTML interstitial;
- returned page title/content is `Pardon Our Interruption`;
- page contains Imperva/Reese browser-protection JavaScript and explicitly requires JavaScript/cookies;
- no tariff PDF links are present in that response;
- direct request to a known official Enel `content/dam/...pdf` URL returns the same HTML interstitial instead of a PDF;
- therefore the failure is not specific to the user's laptop and is not a parser-only defect.

Internal live validation:
- normal fixture/smoke path remains GREEN after markup-independent discovery hardening;
- live catalog probe reproduces 0 PDF links / 0 publications;
- direct-PDF probe receives HTML rather than `%PDF-`.

Product decision:
- **do not attempt to bypass, emulate or defeat Enel's anti-bot challenge**;
- direct unattended HTTP acquisition from the protected Enel web surface is not a reliable primary acquisition channel;
- Phase 9 acquisition must pivot to an accessible official source (preferably CNE/open official data) and/or a controlled user-assisted official-document import fallback;
- preserve Enel publication provenance/cross-checking where official documents can be obtained legitimately;
- the UI must report a protection/source-access failure explicitly rather than leaving the tariff table blank.

QA impact:
- Build 441 item 6 remains FAIL;
- items 7 and bill-audit PDF remain blocked;
- do not ask the user to repeat unrelated Build 441 QA;
- next target QA should cover only the repaired tariff acquisition -> audit -> bill-audit PDF chain unless the redesign materially changes another screen.

## Live-source tariff acquisition finding — Enel Imperva / CNE accessible — 2026-09-28

Research and live CI established the real source behavior:

### Enel
- the official Enel tariff webpage visibly exposes the 2026 regulated-supply publications in a normal browser;
- direct .NET HttpClient access receives HTTP 200 but an **Imperva/Reese browser challenge page** instead of the tariff catalog;
- observed live response:
  - 6,183 HTML characters;
  - 0 PDF hrefs;
  - title/body: "Pardon Our Interruption";
  - JavaScript challenge and cookie requirement;
- direct PDF request without a browser-valid session also returned HTML instead of PDF;
- therefore automated raw-HTTP scraping/downloading from Enel is not a reliable desktop-app path;
- this must **not** be treated as a regex/parser defect and the application must not attempt to bypass Imperva.

### CNE
- official CNE tariff-regulation pages are accessible from .NET/CI;
- live capture discovered 14 candidate PDFs and successfully downloaded/classified 11 VAD-index documents for 2026 with 0 transport/download failures;
- CNE therefore becomes the **automatic programmatic regulatory-evidence source**;
- Enel remains the final distributor tariff-table source and can be ingested through controlled browser/manual PDF import.

### Product-source strategy
1. "Actualizar evidencia oficial" by year:
   - automatically capture CNE evidence and corrections;
   - attempt Enel only opportunistically;
   - if Imperva blocks Enel, say so explicitly rather than leaving an empty grid.
2. Provide:
   - "Abrir página oficial Enel";
   - "Importar PDFs oficiales Enel…".
3. Imported Enel PDFs must retain:
   - original file;
   - SHA-256;
   - effective period;
   - retroactive/version state;
   - extracted text;
   - normalized candidates.
4. Do not automate or emulate the Imperva browser challenge.
5. Bill audit may use CNE as regulatory provenance but final Enel-rate verification still requires Enel tariff-table evidence until a complete CNE-derived final-tariff engine is proven.

This finding supersedes the assumption that the Enel archive itself can be fetched reliably with raw HttpClient from the desktop application.

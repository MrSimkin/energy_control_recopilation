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


## Build 486 — repaired tariff-source QA candidate — GREEN — 2026-09-28

Build 486 is the scoped follow-up to Build 441 items 6/7/8B.

Identity:
- code commit: `788171056ccd438f19e9836acce856466e76a49e`;
- workflow run: `36498902491`;
- artifact ID: `11004089059`;
- SHA-256: `bc921d563059beb59db93cd5c7887d9dbd9a43ef95a20a862567ba6721140623`;
- CI/build/smoke/publish/upload: PASS;
- **live official CNE capture: PASS**.

Live CNE 2026 evidence before handoff:
- 14 candidate PDFs reviewed;
- 12 VAD documents captured;
- 2 corrections;
- 0 failures;
- Jan original 816 + correction 819 retained;
- Aug original 368 + correction 380 retained;
- correction 380 resolved to effective month 2026-08 and retains its own resolution identity;
- Sep 440 and Oct 506 retained.

Root-cause handling from Build 441:
- Enel raw HTTP remains blocked by Imperva/Reese and is not bypassed;
- automatic year update now has machine-accessible CNE official evidence;
- UI explains Enel protection rather than remaining blank;
- UI provides browser opening + controlled multi-PDF Enel import;
- imported Enel official PDFs preserve hash/effective period/retroactivity/text/normalization.

Additional report correction:
- reading-comparison PDF quality label no longer says "Hora asumida" for date-only Enel evidence;
- it now uses "Límite de fecha Enel", consistent with the canonical boundary model.

Canonical scoped target checklist:
- `TARGET_QA_BUILD_486_2026-09-28.md`.

Do not repeat unrelated Build 441 QA.

## Build 486 target-PC tariff QA — still failing — 2026-09-28

User result:
- tariff update on the target PC still produces no useful visible outcome (“sigue sin pasar nada”);
- therefore item 6 remains FAIL;
- items 7 and bill-audit PDF remain blocked.

Important distinction:
- Build 486 live CI proved that CNE is reachable and capturable from .NET on Windows;
- the target-PC failure therefore moves the investigation away from source availability and toward application execution/UI integration, state refresh, exception visibility, or build/runtime path behavior.

Next action:
- inspect the exact target-side button/event/service path;
- make failures impossible to swallow silently;
- add durable local diagnostic output for the tariff update path;
- validate the same packaged application path as closely as possible before another user handoff.


## Correction from target-PC video evidence — 2026-09-28

The previously recorded statement that Build 486 itself had failed tariff QA is **not supported**.

User-supplied screen recording `Grabación 2026-09-28 205723.mp4` shows:
- footer identity: **v0.10.0 · Build 441 · e4ae3b47**;
- therefore the executable under test was Build 441, not Build 486;
- tariff year selector shown in the recording: **2025**;
- UI shown is the older Build 441 Enel-only tariff surface:
  - heading `Tarifas oficiales Enel`;
  - action `Descargar / actualizar año`;
  - no CNE evidence controls / no Enel browser+PDF-import fallback introduced later;
- pressing the button completes with the old `0/0` result, consistent with the already-known Build 441 Imperva failure.

QA status correction:
- **Build 441 item 6 remains FAIL** as previously established;
- **Build 486 item 6 is NOT YET TESTED on the target PC**;
- Build 486 items 7/8B remain pending, not failed;
- do not perform further code fixes based on the mistaken premise that Build 486 showed the same target behavior.

Likely operational cause:
- an older extracted folder/executable or shortcut was launched instead of the Build 486 executable.

Next target action:
- launch a clean Build 486 folder and verify the footer says `Build 486 · 78817105` before running tariff QA.


## Target-PC result — Build 486 — QA 6 PASS — 2026-09-28

User-supplied screenshot confirms Build 486 is actually running:
- footer: `v0.10.0 · Build 486 · 78817105`;
- selected year: 2026.

Official tariff evidence update result:
- **CNE 12 VAD documents captured**;
- **2 corrections**;
- **0 failures**;
- Enel automatic acquisition explicitly reports unavailable due to web protection and directs the user to:
  - `Abrir página oficial Enel`;
  - `Importar PDFs oficiales Enel...`.

Visible version/correction handling:
- 2026-08 Resolution 380 appears as **Corrección vigente**;
- 2026-08 Resolution 368 appears as **Rectificada**;
- 2026-09 Resolution 440 appears as unique/current;
- 2026-10 Resolution 506 appears as unique/current.

UI result:
- tariff/evidence table is populated;
- source, effective date, correction state, capture state, version state, pages, hash and official publication title are visible;
- the previous blank/0-0 behavior is resolved for the automatic official-evidence path.

QA status:
- Build 486 item 6: **PASS**.
- Proceed to item 7: browser-assisted official Enel PDF import + bill audit.


## Target-PC result — Build 486 — QA 7 partial evidence — 2026-09-28

User screenshot confirms:
- footer identity: `v0.10.0 · Build 486 · 78817105`;
- **Auditoría de boleta** tab opens;
- bill selector is populated with multiple stored bill intervals;
- selected example: `29-07-2026 00:00:00 -> 27-08-2026 23:59:00 · sin referencia`;
- audit verification grid is visible but contains **no rows** for the selected bill;
- no visible tariff-verification outcome is therefore available yet.

QA interpretation at this point:
- bill-audit surface/navigation: functionally reachable;
- QA 7 is **PARTIAL / BLOCKED** until the empty verification grid is explained;
- do not infer tariff verification PASS or FAIL yet;
- investigate whether the selected bill lacks stored bill lines, lacks imported Enel tariff-table evidence, has an interval/source applicability issue, or the preview refresh path is defective.

Do not ask the user to repeat prior QA while diagnosing this empty-grid condition.


## Build 486 QA 7 additional screenshot findings — 2026-09-28

Second target-PC screenshot confirms additional audit UX/data-quality issues:

1. **Empty audit grid remains visually unexplained.**
   - The selected bill shows no verification rows.
   - The UI does not clearly tell the user whether the selected bill lacks stored charge lines, lacks tariff evidence, or has another prerequisite problem.

2. **Duplicate / near-duplicate bill periods are visible in the selector.**
   - Multiple entries overlap the same July-August 2026 period.
   - One pair differs only by an end-boundary timestamp representation (e.g. 27-08 23:59 vs 27-08 00:00), which is especially confusing given the canonical Enel date-boundary model.
   - Audit UX must not force the user to distinguish records by raw timestamp artifacts.

3. **Bill selector remains too technical.**
   - It displays full raw timestamps with seconds.
   - It should display human-readable billing dates/boundaries and, where possible, bill reference / billed kWh / total to disambiguate records.

Required UX change before next handoff:
- when a selected bill has zero stored charge lines, show an explicit empty-state explanation and direct action to complete bill evidence;
- simplify bill labels to date/boundary semantics;
- expose enough identifying evidence to distinguish duplicate/overlapping bill records;
- investigate duplicate/near-duplicate stored bill records as a separate data-quality issue rather than hiding them.



## Build 486 QA 7 clarification — bill lines exist but tariff inputs are incomplete — 2026-09-28

Target evidence now clarifies the audit state:
- the selected bill DOES have stored charge lines;
- audit PDF shows three lines:
  - Electricidad consumida: actual amount 58,559 CLP;
  - Transporte de electricidad: actual amount 5,679 CLP;
  - Subsidio: actual amount -3,758 CLP;
- however the two tariff-verifiable lines have no stored quantity and no stored unit rate;
- therefore the audit correctly reports:
  - Electricidad consumida -> no printed unit rate;
  - Transporte de electricidad -> no printed unit rate;
  - Subsidio -> actual-only evidence.

The problem is not "no bill lines"; it is incomplete per-line bill evidence for tariff reconstruction.

UX requirement:
- Boletas Enel must clearly explain which line fields are needed for verification:
  - description;
  - quantity where printed on bill;
  - unit;
  - printed unit rate;
  - actual line amount;
- do not require the user to know internal category keys;
- for lines such as Subsidio or other non-reconstructable adjustments, amount-only evidence is acceptable;
- audit empty/incomplete states must distinguish "no lines" from "lines present but missing quantity/rate".

## Bill-line evidence UX correction — 2026-09-28

The Build 486 audit clarification established that the selected bill has three stored charge lines; the issue is incomplete calculation evidence, not zero bill lines.

Additional official-format review:
- Enel's public “Entendiendo mi boleta” example presents “Electricidad consumida” with billed kWh and a line amount, and “Transporte de electricidad” with a line amount, without presenting a unit-rate field in that charge-detail block;
- final per-unit tariff values belong to the official tariff publications, not necessarily to the customer's bill detail.

Design consequence:
- the user must **not** be asked to invent quantity/unit-rate values that are not printed on the bill;
- description + actual amount are sufficient minimum evidence for preserving a bill line;
- quantity/unit/unit-rate/tax-treatment remain optional evidence only when explicitly present;
- for recognized tariff lines, verification must report missing official source/version/component evidence before treating the absence of a printed unit rate as a blocker;
- once official tariff candidates exist and no bill rate is printed, the UI/PDF must say that official derivation is pending rather than instructing manual entry;
- if the bill crosses multiple effective tariff periods, expected-amount reconstruction remains pending until a supported split/allocation rule is implemented; do not multiply the whole billed consumption by one arbitrary rate.

Implementation in the following code commit:
- audit source checks precede the non-printed-rate state;
- new explicit derivation-pending states distinguish single-period and multi-period evidence;
- “Boletas Enel” explains which fields are optional and says not to invent a rate;
- audit preview and PDF use the same semantics;
- smoke coverage protects the non-printed-rate path.

Official explanatory source used for the format check:
- https://www.enel.cl/es/clientes/informacion-util/entendiendo-mi-boleta.html

## Build 490 — bill-line evidence semantics / audit guidance — GREEN — 2026-09-28

Identity:
- code commit: `ee5eb1bbfaea31db7eec4f80c838ff04c39f2f85`;
- workflow run: `36507194871`;
- Windows Build: **490**;
- CI build: PASS;
- deterministic SQLite smoke: PASS, including recognized tariff line with actual amount but no printed unit rate;
- portable publish/upload: PASS;
- artifact: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11007447748`;
- downloaded handoff ZIP: `SolarEnergyMonitor-Build-490-win-x64.zip`;
- ZIP SHA-256: `7961cd04e7866674ea4627518586c81eb2ac023d3286c28ccc66a95cdd3490d0`.

Behavioral correction:
- existing bill lines are **not** to be re-entered merely because Quantity/Unit price are blank;
- description + actual amount remain sufficient minimum captured evidence;
- quantity/unit/unit price/tax treatment are optional and must be entered only when explicitly present on the bill;
- recognized tariff lines now report missing Enel source/version/component evidence before discussing a non-printed rate;
- when authoritative Enel tariff candidates are available but the bill itself has no stored unit rate:
  - single tariff period -> `OFFICIAL_RATE_DERIVATION_PENDING`;
  - multiple effective tariff periods -> `OFFICIAL_RATE_DERIVATION_MULTI_PERIOD`;
- the July/August target bill must not be reconstructed by multiplying all 266 kWh by one arbitrary rate; it crosses tariff-effective periods and requires a supported split/allocation rule;
- UI and bill-audit PDF explicitly tell the user not to invent unprinted values.

Target-PC QA for Build 490 is intentionally narrow:
1. reuse the existing Build 486 `Data` folder so the real bill/readings/evidence remain intact;
2. confirm footer `Build 490 · ee5eb1bb`;
3. in **Boletas Enel**, select the July/August bill and confirm the existing three lines remain; do **not** add/re-enter quantity or rate just because the fields are blank;
4. in **Auditoría de boleta**:
   - without relevant imported Enel final tariff tables, recognized tariff lines should say **Falta fuente tarifaria**;
   - after importing the relevant July/August Enel PDFs and successful normalization, recognized non-printed-rate lines should advance to official-source derivation states, with the multi-period bill explicitly requiring a tariff-period split;
   - Subsidio remains actual-only unless an authoritative reconstruction rule is available;
5. export one bill-audit PDF after the source state is meaningful.

Do NOT repeat:
- startup;
- broad visual/responsive QA;
- data import;
- reading-comparison PDF;
- CNE 2026 automatic evidence QA;
- family reports;
- Help/manual.

Build 490 does not yet implement the formal multi-period charge-allocation rule. It makes the evidence state truthful and removes the incorrect expectation that the user must supply a unit rate that the bill may not print.

## Target-PC observation before Build 490 QA — billed kWh is already bill-level evidence — 2026-09-28

User clarification before testing Build 490:
- the bill already contains its primary billed-consumption value in kWh;
- for the target July/August bill this is the existing bill-level billed consumption, not a value that should be re-entered per line;
- the user identified the two tariff-relevant bill lines conceptually as:
  - “cobro de electricidad” / **Electricidad consumida**;
  - “coste de transporte” / **Transporte de electricidad**.

Design question raised by this observation:
- do **not** ask the user to re-enter the bill merely to repeat the same billed kWh on both lines;
- investigate whether recognized per-kWh components can derive their quantity basis from `utility_bill.billed_consumption_kwh` when the line itself has no printed quantity;
- preserve the distinction between:
  - bill-level kWh actually printed/stored as evidence;
  - a line-level quantity explicitly printed on the bill;
  - a quantity basis derived by the application because an official tariff component is denominated in $/kWh;
- multi-period tariff splitting remains a separate requirement: using bill-level kWh as the quantity basis does not justify applying one tariff rate across the whole bill interval.

## Billed-kWh automatic line mapping implementation — 2026-09-28

User clarification:
- “Electricidad consumida” for the target bill is the already-stored **266 kWh billed consumption**;
- the user does not know/need to calculate “Transporte de electricidad”; it should come from the tariff engine;
- future bill entry should provide explicit semantic labels so tariff calculation does not depend on free-text recognition.

Implementation:
- `ELECTRICITY_CONSUMED` and `ELECTRICITY_TRANSPORT` are explicit bill-line choices in the UI;
- selecting either stores the normalized `category_key` and supplies the human description;
- the user still enters the actual line amount exactly as billed;
- the user does **not** repeat bill-level kWh on those lines;
- when the normalized official candidates for either component are consistently `$/kWh`, the audit derives its calculation quantity from `utility_bill.billed_consumption_kwh`;
- provenance is explicit:
  - `BILL_LINE` when quantity was actually stored on the line;
  - `BILL_BILLED_KWH` when the app reuses billed kWh from the bill header;
- actual-evidence displays continue to distinguish an unprinted line quantity from this derived calculation basis;
- multi-period bills remain unreconstructed until a supported tariff-period allocation rule exists.

For the target July/August bill:
- **Electricidad consumida** may use 266 kWh as its calculation basis;
- **Transporte de electricidad** may also use 266 kWh only because its normalized official component is confirmed as `$/kWh`;
- this does not yet authorize applying one rate across the entire July/August interval.

## Build 491 — semantic bill-line labels + billed-kWh calculation basis — GREEN — 2026-09-28

Identity:
- code commit: `2039e7e1b70f5c56c08b0932ade744fcdd1c9d0b`;
- workflow run: `36508322865`;
- Windows Build: **491**;
- CI build: PASS;
- deterministic SQLite smoke: PASS;
- billed-kWh derivation smoke: PASS;
- portable publish/upload: PASS;
- artifact: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11007774143`;
- handoff ZIP: `SolarEnergyMonitor-Build-491-win-x64.zip`;
- ZIP SHA-256: `36e5692534e2983825fd054db2353f9bebac227cdad17e996e8f6a3f04ea0a19`.

Build 490 is superseded for the next target-PC QA.

New behavior:
- bill-line entry has explicit **Tipo de cargo** choices:
  - Electricidad consumida;
  - Transporte de electricidad;
  - Otro / texto libre;
- recognized choices persist normalized category keys instead of relying on exact free-text matching;
- the line amount remains actual bill evidence;
- bill-level billed kWh is not re-entered on recognized lines;
- audit adds **Base de cálculo** with provenance:
  - e.g. `266.000 kWh · consumo boleta` when derived from the bill header;
- derivation from billed kWh is allowed only for recognized Electricidad consumida / Transporte de electricidad when normalized official candidates are consistently `$/kWh`;
- actual-line evidence remains separate: a blank printed line quantity remains blank in the actual-evidence section;
- tariff rate still comes from official tariff evidence, not from amount/kWh reverse-engineering;
- multi-period July/August reconstruction remains pending until the allocation rule is supported.

Target-PC QA for Build 491:
1. reuse the existing Data folder;
2. confirm footer `Build 491 · 2039e7e1`;
3. do **not** re-enter the existing July/August bill;
4. open Auditoría de boleta and select it;
5. confirm recognized Electricidad consumida / Transporte lines show the billed kWh as **Base de cálculo** when tariff-source prerequisites allow line evaluation;
6. in Boletas Enel, verify the new **Tipo de cargo** selector exists for future line entry; no need to save a duplicate line merely to test the selector;
7. continue with Enel tariff-PDF import only if the audit still reports missing final tariff source.

Do not test Build 490 separately.

## Target-PC observation after Build 491 — audit report needs multi-level evidence and financial scenarios — 2026-09-28

User reviewed `Auditoria-Boleta-Enel-20260928-2238.pdf` and rejected the current report as insufficient.

Observed defects:
- tariff-reconstruction table is visually unreadable and splits badly across pages;
- long source filenames dominate the main table;
- technical detail leaks in English;
- report says no rates were found/reconstructed even though official July/August Enel tariff sources and bill-level kWh are present;
- the report focuses almost entirely on the utility/billed consumption and does not translate Solar of Things consumption into tariff-based financial scenarios.

User expectation:
- presentation and evidence hierarchy should reach the standard already used by the internal/family energy-audit XLSX;
- the reader must be able to follow conclusions from summary -> comparison -> evidence -> quality -> technical traceability;
- show at least three parallel energy/financial views:
  1. Enel billed/meter consumption;
  2. Solar of Things observed grid import;
  3. Solar of Things sensitivity range;
- for each view, derive what the applicable official tariff implies in CLP where evidence supports it;
- show both energy variance and monetary variance;
- preserve a clear distinction between measured/printed values, derived values, scenario/counterfactual values, and unresolved values;
- do not call the current sensitivity range probabilistic/confidence-based unless a probabilistic model is actually implemented.

Evidence from the target PDF:
- billed / meter consumption: 266.000 kWh;
- Solar of Things observed import: 204.260 kWh;
- observed difference: -61.740 kWh / 23.21%;
- coverage: 90.3%;
- sensitivity range: 204.260–505.168 kWh;
- actual billed total: CLP 60,104;
- actual gross bill: CLP 65,422.

Official-tariff cross-check performed against Enel's 2026 retroactive publications:
- August 2026 BT1 publishes ELECTRICITY_CONSUMED for BT_AA/T5 at 220.147 CLP/kWh in the published IVA column;
- 266 kWh × 220.147 = 58,559.102 CLP, essentially exactly the actual `Electricidad consumida` line of CLP 58,559;
- July 2026 equivalent BT_AA/T5 published IVA value is 220.115 CLP/kWh, which does not reconcile as tightly;
- this creates strong bill-derived evidence that the August publication/rate is the rate actually represented by that line, without requiring a generic unproven proration rule;
- official August table defines T5 as prior-year average consumption >230 and <=240 kWh; current bill kWh must not be used to define ETR;
- official August `Transporte de electricidad` is 20.489 CLP/kWh in published IVA column and public-service charge is 0.855 CLP/kWh; the residential bill line may require composite treatment, not a naive single-component match.

Required redesign direction:
- Page/level 1: executive energy + money comparison and concise audit conclusion;
- Page/level 2: scenario comparison table (billed, Solar observed, sensitivity bounds) with tariff-driven components and CLP differences;
- Page/level 3: actual-vs-reconstructed bill charges, explicit unresolved residual;
- Page/level 4: data quality/sensitivity/observability;
- Page/level 5: sources, version/retroactivity, inferred applicability and reading traceability;
- long raw filenames belong in evidence/annex, not headline tables.

## Statistical correction — Build 491 sensitivity bound is not a probable variance range — 2026-09-28

User challenged the current `204.260–505.168 kWh` “sensitivity” range as physically/statistically misleading for the bill-audit use case.

Correction:
- current `UtilityReconciliationService.BuildSensitivity` is **not probabilistic**;
- it fills every uncovered hour at the maximum observed grid-import power and therefore produces an engineering stress upper bound, not a likely missing-energy estimate;
- for the target bill the PDF reports 90.3% temporal coverage of `grid_import_power_w`, while a separately remembered ~99.3% figure may belong to another coverage concept/report and must not be conflated without evidence;
- source-attribution coverage of observed consumption and time coverage of the grid-import metric are different quantities.

New audit requirement:
- remove the max-power stress bound from the headline “probable variance” presentation;
- preserve it only, if useful, as a clearly labelled worst-case diagnostic bound in a technical annex;
- build an **independent statistical missing-energy model** from Solar of Things telemetry, without conditioning on the Enel billed/meter value;
- model missing intervals using comparable observed intervals (at minimum local time-of-day and gap structure; stronger contextual matching where supportable);
- use empirical resampling / multiple imputation to obtain:
  - observed energy;
  - expected/median completed energy;
  - spread/standard deviation where meaningful;
  - empirical percentile interval(s), e.g. P5–P95, clearly labelled as simulation/predictive intervals rather than metrological confidence;
- compare Enel's 266 kWh **after** the independent Solar distribution is constructed, so the audit can state whether the utility-meter value falls inside/outside the telemetry-implied range;
- do not hard-cap the Solar distribution at 266 kWh, because that would make the comparison circular;
- financial scenarios must use the observed Solar value and statistically completed Solar distribution/range, not the previous max-power stress bound.

Presentation consequence:
- distinguish four evidence classes:
  1. Enel measured/billed value;
  2. Solar of Things directly observed/integrated value;
  3. Solar of Things statistical completion for missing telemetry;
  4. worst-case engineering stress bound (technical appendix only, if retained).

## Audit presentation requirement — explicit lower/central/upper Solar sensitivity vs Enel — 2026-09-28

User clarification:
- the bill-audit report must show the Solar of Things estimate as an explicit **lower / central / upper** sensitivity band and compare every point directly with Enel;
- the comparison must be readable enough to present to another person without interpreting engineering/debug tables;
- evidence/provenance must accompany each displayed value.

Required headline comparison:
- Enel measured/billed kWh;
- Solar of Things directly observed kWh;
- Solar statistical lower bound;
- Solar statistical central estimate;
- Solar statistical upper bound;
- for each Solar value:
  - difference vs Enel in kWh;
  - difference vs Enel in percent;
  - tariff-derived CLP scenario where tariff evidence is sufficient.

Evidence labels must distinguish:
- **Measured / printed by Enel**;
- **Directly observed by Solar of Things**;
- **Statistically completed from Solar of Things telemetry**;
- **Official tariff-derived**;
- **Unresolved / insufficient evidence**.

Presentation rule:
- these values belong in the executive comparison layer, not only in the quality annex;
- the old max-observed-power stress bound must not occupy the lower/central/upper statistical slots;
- the technical annex must explain the statistical method, sample/context basis, coverage and percentile semantics used to construct the lower/central/upper band.

## Statistical lower/central/upper audit implementation — 2026-09-28

Implementation tranche:
- added `UtilityGridImportStatisticalCompletionService`;
- the independent Solar of Things completion model does **not** use the Enel billed/meter value;
- uncovered `grid_import_power_w` intervals are completed by deterministic empirical bootstrap:
  - local hour;
  - weekday/weekend class;
  - controlled widening of donor pool when a strict cell is sparse;
  - 2,000 simulations;
- headline interval is P5 / P50 / P95;
- observed integration remains a separate value and is never silently replaced;
- previous maximum-observed-power missing-data bound is no longer used as a probable range.

Tariff/financial layer:
- added `UtilityBillTariffScenarioAnalysisService`;
- tariff is selected from official normalized Enel candidates by reconciliation against the **actual bill line**, before Solar energy values are applied;
- `Electricidad consumida` rate matching is bill-grounded;
- `Transporte de electricidad` can reconcile either directly or, when supported by the bill arithmetic, as a composite with published public-service charge;
- the same supported component subtotal is then applied to:
  - Enel billed kWh;
  - Solar observed kWh;
  - Solar P5;
  - Solar P50;
  - Solar P95;
- scenarios explicitly remain a supported tariff subtotal, not a fabricated full final bill where subsidies/FET/fixed or other adjustments are unresolved.

PDF redesign:
- executive energy comparison first;
- explicit Enel vs Solar observed/P5/P50/P95 deltas;
- direct statement whether Enel falls inside/outside the independent Solar predictive interval;
- financial scenario table in CLP;
- actual-vs-reconstructed component reconciliation;
- quality/statistical-method section;
- tariff evidence and full reading traceability in the technical evidence layer;
- long source filenames no longer dominate the headline tariff table;
- English internal detail is removed from the principal audit narrative.

## Build 492 — statistical Solar margins + financial tariff audit — GREEN — 2026-09-28

Identity:
- code commit: `fbcc5c9d7c347e2b083660f912889d874caec35a`;
- workflow run: `36512369715`;
- Windows Build: **492**;
- CI build: PASS;
- SQLite smoke: PASS;
- statistical-completion smoke: PASS;
- bill-audit PDF smoke: PASS;
- portable publish/upload: PASS;
- artifact ID: `11010445054`;
- handoff ZIP: `SolarEnergyMonitor-Build-492-win-x64.zip`;
- ZIP SHA-256: `5fa51e3dd5c39edb8a5fb40e2262ae0862957b9b0d26b615ae5fc8ea4c3f4c2c`.

Target-PC QA is intentionally focused on the real July/August bill:
1. reuse the same existing `Data` folder from Build 491;
2. confirm footer `Build 492 · fbcc5c9d`;
3. do not re-enter the bill, lines, readings, tariff PDFs or historical Solar data;
4. open **Auditoría de boleta**, select the existing 29-07-2026 -> 27-08-2026 bill, and export one new audit PDF;
5. return that PDF for review.

Expected high-level PDF structure:
- executive energy comparison:
  - Enel measured/billed;
  - Solar observed;
  - Solar lower P5;
  - Solar central P50;
  - Solar upper P95;
  - kWh and percentage deltas vs Enel;
- explicit statement whether Enel falls inside/outside the independent Solar predictive interval;
- official-tariff financial scenario table for the same energy references;
- actual-charge vs reconstructed-component reconciliation;
- data-quality/statistical-method evidence;
- tariff source/version/applicability evidence;
- reading traceability.

Important:
- Build 492 must not present the old maximum-observed-power missing-data stress bound as a probable lower/central/upper range;
- P5/P50/P95 are empirical predictive-imputation percentiles, not meter calibration confidence;
- exact target-PC P5/P50/P95 and tariff reconstruction values are intentionally not pre-assumed; they must be read from the user's real database output.

## Target-PC observation after Build 492 — adopt card-based report grammar in Audit and Informes — 2026-09-28

User provided two reference PDFs:
- `Auditoria-Boleta-Enel-20260928-2351.pdf` (Build 492 audit output);
- `Resumen simple de energía_20260420_20260927.pdf` (existing Informes visual reference).

Observed presentation problem:
- the audit now contains useful P5/P50/P95 and financial calculations, but its primary sections still read like dense technical tables;
- the audit's tariff-evidence area leaks raw parser/source text and internal status tokens, making it unsuitable for presentation;
- the existing Informes PDF demonstrates the preferred visual grammar: cards with short labels, large values, concise context and clear grouping.

Required cross-report design rule:
- use the existing Informes card grammar for headline audit metrics;
- P5/P50/P95 must be visually presented as first-class cards, not buried in rows;
- P50 is the central estimate and should be visually identified as such;
- each percentile needs a short plain-language explanation;
- technical parser strings/status codes must not appear in the main presentation layer;
- raw tariff evidence belongs in a concise evidence/annex layer with human-readable status.

Informes extension:
- the existing “Importación total desde Enel” export card must be expanded to communicate:
  - directly observed import;
  - P5 lower predictive completion;
  - P50 central predictive completion;
  - P95 upper predictive completion;
- explain that P5/P50/P95 complete missing telemetry statistically and are not Enel meter readings or metrological confidence intervals;
- preserve the distinction between temporal grid coverage and source-attribution coverage.

Audit layout target:
- executive consumption comparison as cards:
  - Enel billed/meter;
  - Solar observed;
  - Solar P5;
  - Solar P50;
  - Solar P95;
- financial comparison as matching cards;
- keep concise comparison deltas versus Enel under the Solar cards;
- actual/reconstructed charges may remain tabular where tabular comparison is genuinely clearer;
- tariff evidence must show only concise fields: effective period/version, RED/ETR, official rate, composition and human-readable reconciliation status.

## Card-based audit and Informes percentile presentation implementation — 2026-09-28

Implementation after review of the user's Build 492 audit PDF and existing Informes PDF:
- audit headline energy comparison changed from a dense table to cards;
- Enel and directly observed Solar values are separate primary cards;
- P5/P50/P95 are a dedicated 3-card row;
- P50 uses the central/highlight treatment;
- every Solar card carries its delta vs Enel plus a short evidence explanation;
- financial scenarios use the same card grammar for Enel, observed, P5, P50 and P95;
- statistical quality metrics use card groups plus a plain-language percentile explanation;
- tariff evidence no longer emits raw parser source text or internal status tokens in the presentation layer;
- tariff evidence uses concise cards for effective period, RED/ETR, supported modeled rate and reconciled components;
- actual bill section labels are human-readable and signed CLP formatting is cleaned up.

Informes PDF extension:
- `EnergyReportData` now carries the same independent grid-import statistical completion used by the bill audit;
- the existing “Importación total desde Enel” card remains the directly observed value;
- immediately below it, P5/P50/P95 cards communicate lower / central / upper statistical completion;
- P50 is explicitly the central estimate;
- each card explains its percentile meaning;
- a note states the number of uncovered hours completed statistically and clarifies that these are not Enel meter readings or metrological confidence intervals;
- the distinction between grid temporal coverage and source-attribution coverage remains intact.

## Build 493 — card-based audit + Informes P5/P50/P95 presentation — GREEN — 2026-09-28

Identity:
- code commit: `0da377bce777d814b17ef32e7ca21d4fc516aa5f`;
- workflow run: `36515330128`;
- Windows Build: **493**;
- CI build: PASS;
- SQLite smoke: PASS;
- Informes statistical-model smoke: PASS;
- bill-audit PDF smoke: PASS;
- portable publish/upload: PASS;
- artifact ID: `11011390406`;
- handoff ZIP: `SolarEnergyMonitor-Build-493-win-x64.zip`;
- ZIP SHA-256: `2f6b247ea8f99fd87bd4869aac9f44228867fceb8881a635d1dcedb028ce2cc1`.

Presentation changes:
- Audit:
  - Enel and observed Solar are primary cards;
  - P5/P50/P95 are a dedicated predictive-range card row;
  - P50 is visually highlighted as central estimate;
  - each Solar card shows delta vs Enel plus a concise meaning;
  - monetary scenarios use the same card grammar;
  - quality/evidence uses cards and a plain-language percentile explanation;
  - tariff evidence no longer prints raw parser text/status tokens;
  - tariff components appear as concise reconciliation cards;
  - human-readable bill section labels and signed currency formatting.
- Informes / Resumen simple PDF:
  - existing Importación total desde Enel card is explicitly the directly observed amount;
  - P5/P50/P95 cards appear immediately below it;
  - each percentile explains its meaning;
  - note states uncovered hours completed statistically and distinguishes this from Enel readings/metrological confidence;
  - same independent statistical service as bill audit, avoiding divergent calculations.

Target-PC QA for Build 493:
1. reuse the same existing `Data` folder;
2. confirm footer `Build 493 · 0da377bc`;
3. do not re-enter any bill, tariff, reading or Solar historical data;
4. export exactly:
   - one new Enel bill-audit PDF for the existing July/August bill;
   - one new **Resumen simple de energía** PDF using the same report range previously used for the visual-reference PDF, if practical;
5. return both PDFs.

Review focus:
- cards are readable and visually hierarchical;
- P5/P50/P95 meanings are understandable without technical interpretation;
- P50 reads as the central estimate;
- audit monetary cards remain traceable to official tariff evidence;
- tariff evidence contains no raw parser dumps/internal status codes;
- Informes clearly distinguishes observed grid import, P5/P50/P95, grid temporal coverage and attribution coverage.

Do not repeat:
- data import;
- tariff import;
- startup QA;
- broad responsive QA;
- manual bill entry;
- unrelated report pages unless visual pagination is obviously broken.

## Build 493 target-PDF QA refinements — 2026-09-29

Reviewed actual target outputs:
- `Auditoria-Boleta-Enel-20260929-0011.pdf`;
- `Resumen simple de energía_20260420_20260927.pdf`.

QA conclusion:
- card-based presentation is materially clearer and accepted as the visual baseline;
- Audit page 1 is now presentation-grade for the main Enel vs Solar and financial comparison;
- Informes correctly shows observed grid import plus P5/P50/P95 as first-class cards;
- remaining issues are minor presentation semantics, not calculation defects.

Refinement tranche:
- explain P5–P95 as the **central 90% of simulated completions**;
- P50 explicitly remains the median / central estimate;
- simplify Audit page 2 reconstructed-components table by removing the narrow Evidence column;
- move reconciliation caveat into a readable callout;
- label RED/ETR as **inferred** rather than as if printed on the bill;
- normalize near-zero signed currency to `$ 0` instead of visually misleading `- $ 0`;
- no changes to the statistical model, P5/P50/P95 values, tariff rates or reconciliation logic.

## Build 494 — final percentile wording + audit reconciliation cleanup — GREEN — 2026-09-29

Identity:
- code commit: `a0eea98e513f1ce822073c0c02a4f2a104ffc9a3`;
- workflow run: `36517130642`;
- Windows Build: **494**;
- CI build: PASS;
- SQLite smoke: PASS;
- bill/report export smoke: PASS;
- portable publish/upload: PASS;
- artifact ID: `11011373590`;
- handoff ZIP: `SolarEnergyMonitor-Build-494-win-x64.zip`.

Changes are presentation-only:
- P5–P95 is described as the **central 90% of simulated completions**;
- P50 is explicitly the median / central estimate;
- Audit page 2 reconstructed-component table drops the narrow Evidence column;
- reconciliation caveats move into a readable callout;
- RED/ETR is labelled **inferred**, not printed evidence;
- near-zero signed currency displays as `$ 0` instead of `- $ 0`;
- no changes to statistical values, tariff rates, scenario costs or reconciliation logic.

Target-PC QA:
1. reuse Build 493 Data unchanged;
2. confirm footer `Build 494 · a0eea98e`;
3. export:
   - the same Enel bill-audit PDF;
   - the same Resumen simple de energía PDF;
4. visual-only review:
   - Audit page 2 table/readability;
   - RED/ETR inferred wording;
   - zero residual formatting;
   - P5/P50/P95 explanations in Audit and Informes.

No need to repeat any data, tariff or calculation validation already passed on Build 493.

## CIERRE DE JORNADA / PUNTO CANÓNICO DE REANUDACIÓN — 2026-09-29

Estado al cerrar:
- `main` antes de este cierre documental: `4bed74361bc01c1c43434bb8516934506e0e4348`;
- última build de código: **Build 494**;
- commit de código de Build 494: `a0eea98e513f1ce822073c0c02a4f2a104ffc9a3`;
- workflow: `36517130642`;
- artifact ID: `11011373590`;
- Build / smoke / report export / publish / upload: **GREEN**;
- no hay cambios funcionales pendientes de compilar después de Build 494;
- este cierre es **documental solamente** y NO requiere una Build 495.

### Estado funcional consolidado

Boleta / auditoría Enel:
- la boleta objetivo 29-07-2026 -> 27-08-2026 conserva 266.000 kWh facturados a nivel de boleta;
- las líneas `Electricidad consumida` y `Transporte de electricidad` pueden reutilizar ese kWh facturado como base derivada cuando el componente oficial está respaldado en $/kWh;
- el usuario no debe reingresar kWh por línea ni inventar tasas;
- futuras líneas pueden marcarse semánticamente como:
  - Electricidad consumida;
  - Transporte de electricidad;
  - Otro / texto libre;
- la auditoría conserva separación entre evidencia impresa, derivación oficial, escenario estadístico y valor no resuelto.

Modelo estadístico:
- el antiguo bound basado en máxima potencia observada NO es el rango probable;
- el rango principal es independiente de Enel y usa `grid_import_power_w`:
  - observado;
  - P5 = margen inferior;
  - P50 = mediana / estimación central;
  - P95 = margen superior;
- P5–P95 se comunica como el 90% central de las completaciones simuladas;
- Enel se usa sólo después como referencia externa de contraste;
- cobertura temporal de red y cobertura de atribución son métricas distintas y permanecen separadas.

Modelo tarifario / financiero:
- la tarifa se selecciona/reconcilia contra líneas reales de la boleta y fuentes oficiales antes de aplicar escenarios Solar of Things;
- los escenarios monetarios se muestran para:
  - Enel facturado;
  - Solar observado;
  - Solar P5;
  - Solar P50;
  - Solar P95;
- los montos son subtotales de componentes tarifarios respaldados, no una falsa boleta total cuando existen subsidios/FET/cargos no reconstruidos;
- RED/ETR debe presentarse como **inferido** cuando no está impreso y la conciliación no lo demuestra de forma única.

### QA real ya realizado en Build 493

Se revisaron los PDFs reales del usuario:
- `Auditoria-Boleta-Enel-20260929-0011.pdf`;
- `Resumen simple de energía_20260420_20260927.pdf`.

Conclusiones aceptadas:
- la gramática visual por cards es el baseline vigente;
- la primera página de Auditoría es presentable y jerárquica;
- Informes muestra correctamente observado + P5/P50/P95;
- Informes mantiene separadas cobertura temporal de red y cobertura de atribución;
- la evidencia tarifaria dejó de mostrar dumps crudos del parser;
- los valores/cálculos de Build 493 se consideraron correctos para este tranche;
- las observaciones restantes eran de presentación, no de cálculo.

### Build 494 — única validación pendiente

Build 494 contiene sólo refinamientos de presentación sobre Build 493:
- P5–P95 explicado como 90% central;
- P50 explicado como mediana / estimación central;
- tabla de componentes reconstruidos de Auditoría simplificada;
- caveat de conciliación movido a callout;
- RED/ETR rotulado como inferido;
- residual cercano a cero mostrado como `$ 0`, no `- $ 0`.

Mañana NO repetir:
- importación de datos;
- importación de tarifas;
- ingreso de boleta;
- ingreso de líneas;
- lecturas Enel;
- startup QA;
- responsive QA amplio;
- validación estadística ya aprobada en Build 493;
- validación tarifaria/cálculos ya aprobados en Build 493.

Mañana hacer únicamente:
1. reutilizar la misma carpeta `Data`;
2. abrir Build 494 y confirmar footer `Build 494 · a0eea98e`;
3. exportar:
   - la misma Auditoría de boleta Enel;
   - el mismo Resumen simple de energía;
4. revisar visualmente:
   - página 2 de Auditoría;
   - texto RED/ETR inferido;
   - residual `$ 0`;
   - explicación P5/P50/P95 en ambos PDFs.

Si esos cuatro puntos son correctos:
- cerrar el tranche de Auditoría/Informes como **QA visual PASS**;
- no generar otra build sólo por cierre documental;
- continuar al siguiente objetivo funcional desde Build 494.

### Regla de reanudación

Al comenzar la próxima sesión:
- leer primero esta sección de cierre;
- verificar que `main` sólo haya avanzado por este commit documental de cierre;
- NO reconstruir ni reanalizar la historia de Builds 486–493;
- tratar Build 494 como baseline vigente;
- pedir/usar únicamente los dos PDFs de Build 494 si el usuario ya los generó;
- si no existen aún, solicitar sólo esa validación visual mínima;
- cualquier nueva observación del usuario debe registrarse antes de abrir un nuevo tranche de código.


## Nuevo tranche canónico — incertidumbre estadística / reportes Enel-SEC — 2026-09-30

Observación del usuario que abre el tranche:
- antes de probar Build 494, el usuario requiere que los PDFs expliquen P5/P50/P95 de forma comprensible para terceros de Enel/SEC;
- la explicación debe dejar claro por qué se calcula un rango y qué incertidumbre representa;
- además debe revisarse el tratamiento estadístico de los kWh observados que permanecen sin atribución de origen;
- antes de cualquier cambio productivo, se exige investigación/prueba estadística y desarrollo por fases.

Plan canónico nuevo:
- `STATISTICAL_UNCERTAINTY_DEVELOPMENT_PLAN_2026-09-30.md`.

Estado:
- **Phase 0 COMPLETE**;
- Build 494 permanece congelada como baseline histórico;
- el usuario no dispone actualmente de Build 494 localmente;
- la antigua validación visual inmediata de Build 494 queda pospuesta mientras se ejecuta este tranche estadístico;
- NO se modifica código productivo ni se genera Build 495 en Phase 0.

Separación canónica confirmada:
- `grid_import_power_w` / P5-P50-P95 actual responde a incertidumbre temporal de **importación total desde red**;
- `UnattributedHouseKwh` responde a incertidumbre de **atribución de origen dentro del consumo observado de la casa**;
- ambas magnitudes NO son equivalentes;
- queda prohibido sumar directamente `UnattributedHouseKwh` a P5/P50/P95 por riesgo de doble conteo;
- `Importación total desde Enel` sigue siendo la magnitud destinada a comparación con medidor/boleta;
- `Enel -> Casa` sigue siendo una métrica analítica de flujo doméstico y no se intercambia con importación total.

Siguiente paso autorizado:
- **Phase 1 — auditoría estadística del método actual**;
- ejecutar pruebas reproducibles del método `grid-import-empirical-bootstrap.v1`;
- todavía NO reemplazar algoritmo, NO cambiar PDF y NO crear build de usuario.


## Statistical uncertainty Phase 1 — Lot 1 — 2026-09-30

UX observation recorded before the statistical lot:
- Reports > Export PDF is now the canonical long-operation progress pattern;
- transversal requirement persisted in `PRE_BUILD_OBSERVATION_REVIEW_RULE.md`;
- future operations should use local bar/status under the action, Paso n/X when staged, completion ping, and explicit output path;
- Settings must eventually expose a persistent default export folder;
- this UX work is deferred from the statistical tranche and does not justify an isolated build.

Phase 1 Lot 1:
- harness: `research/statistical_uncertainty/phase1_current_method_audit.py`;
- results: `research/statistical_uncertainty/PHASE1_LOT1_RESULTS_2026-09-30.md`;
- harness default `simulation_count=2000`, parameterizable for later convergence analysis;
- 1,000 repetitions per scenario with a 2-hour hidden interval.

Observed empirical P5-P95 coverage:
- stable: 89.5%;
- day/night: 87.8%;
- moderate temporal autocorrelation: 61.7%;
- high temporal autocorrelation: 30.6%.

Interpretation:
- P50 showed no material directional bias in this lot;
- the dominant observed weakness is interval undercoverage under temporal dependence;
- increasing Monte Carlo draws alone cannot repair missing serial dependence;
- Phase 1 remains open.

Execution caveat:
- this lot used a behavioral Python mirror of the current C# algorithm because the local analysis runtime could not clone/execute .NET;
- no production C# algorithm was modified;
- no Windows build was triggered by the research-path commits.

Next proposed Phase 1 lot:
- vary gap duration while holding process behavior controlled;
- then test multiple gaps and contiguous appliance-like spikes.


## Safety checkpoint — Phase 1 statistical audit — 2026-09-30

User temporarily stepped away and requested repository persistence before further work.

Current state is fully persisted.

Last completed statistical work:
- Phase 1 Lot 1 is complete;
- 2,000 simulations per reconstruction;
- 1,000 repetitions per scenario;
- 2-hour controlled gap;
- empirical P5-P95 coverage:
  - stable 89.5%;
  - day/night 87.8%;
  - moderate autocorrelation 61.7%;
  - high autocorrelation 30.6%;
- P50 mean directional bias remained near zero;
- current concern is interval undercoverage under serial dependence, not primarily central-estimate bias.

New design clarification persisted:
- telemetry cadence is nominally ~5 minutes;
- adjacent 5-minute observations may be serially dependent;
- point-wise independent resampling can destroy realistic contiguous load episodes;
- this can narrow P5-P95 even when P50 remains reasonable.

Existing gap-detection behavior to audit:
- continuity threshold = median sample gap ×3, clamped ~10–20 minutes;
- with normal 5-minute cadence this is typically ~15 minutes;
- separations greater than the threshold are treated as uncovered/statistically completable;
- isolated missing samples can remain bridgeable by ordinary integration and therefore are not equivalent to one long gap.

Next proposed Phase 1 lot, NOT YET EXECUTED:
- test 10m, 15m, 20m, 30m, 1h, 2h, 4h, 8h, 12h gap/separation cases;
- record classification as continuous vs uncovered;
- measure P5-P95 coverage, P50 error, interval width and donor behavior;
- compare one long contiguous loss with the same nominal count of distributed 5-minute losses, explicitly accounting for continuity-threshold behavior;
- after that, test multiple gaps and appliance-like contiguous spikes.

No further test lot has been executed after this checkpoint.
No production C# was modified.
No PDF/report code was modified.
No Windows build was generated.

UX requirement already persisted earlier today:
- Reports > Export PDF pattern is canonical for long button operations;
- local progress bar + stage text/Paso n/X + final path + completion ping;
- Settings must later provide persistent default export folder.


## Statistical uncertainty Phase 1 — Lot 2 complete — 2026-09-30

Artifacts:
- `research/statistical_uncertainty/phase1_gap_duration_audit.py`;
- `research/statistical_uncertainty/PHASE1_LOT2_RESULTS_2026-09-30.csv`;
- `research/statistical_uncertainty/PHASE1_LOT2_REPORT_2026-09-30.md`.

Design:
- 4 process families: stable, day/night, moderate autocorrelation, high autocorrelation;
- separations: 10m, 15m, 20m, 30m, 1h, 2h, 4h, 8h, 12h;
- 1,000 repetitions per cell;
- 2,000 current-method simulations per statistical reconstruction.

Confirmed threshold behavior:
- nominal 5-minute cadence -> continuity threshold ≈15 minutes;
- 10m and 15m are bridged as continuous;
- 20m+ becomes an uncovered statistical gap.

Important semantic finding:
- 10m/15m cases may contain missing source samples but still return 100% temporal integration coverage / COMPLETE_OBSERVATION;
- synthetic absolute integration error was small, but “100% coverage” is therefore not the same as raw source-sample completeness;
- later report wording should distinguish those concepts if exposed.

Empirical P5-P95 coverage for statistical gaps:
- stable: 86.5%–93.5% across 20m–12h;
- day/night: 87.2%–91.4%;
- moderate autocorrelation: 58.9%–72.4%;
- high autocorrelation: 22.7%–56.5%.

Interpretation:
- hour/day-type stratification is adequate for these simple stable/day-night synthetic processes;
- serial dependence is the dominant structural weakness;
- P50 remains generally near unbiased;
- P5-P95 is too narrow under autocorrelation;
- more Monte Carlo draws alone will not correct missing covariance.

Phase 1 remains open.

Next proposed Lot 3, NOT YET EXECUTED:
- compare one contiguous gap vs several medium gaps vs distributed losses at similar total missing duration;
- explicitly separate isolated 5-minute losses that are bridged by integration;
- then add appliance-like persistent high-load episodes.

No production C# changed.
No report/PDF code changed.
No Windows build generated.


## Phase 1 statistical audit — CLOSED — 2026-09-30

Phase 1 has been closed after:
- Lot 1 baseline process-family audit;
- Lot 2 gap-duration/boundary audit;
- formal exact-binomial + Holm inference;
- direct C# analytical fixture validation in GitHub Actions;
- Lot 3 paired missingness-topology audit;
- Lot 4 donor-time-resolution causal audit with exact oracle;
- Lot 5 persistent-regime/appliance-like analytical stress test.

Key conclusions:
- P50 can remain near unbiased while P5-P95 is badly under-calibrated;
- serial dependence causes omitted covariance under independent 5-minute resampling;
- 2h AR-style analytical examples predict coverage losses of the same order observed in simulation;
- whole-hour donor bins can miscalibrate short gaps on intrahour ramps even with more donor days;
- a narrow time-position pool restored controlled day/night calibration while the exact oracle stayed near 90%;
- persistent ON/OFF episodes provide an exact variance-ratio counterexample:
  - point-wise variance / persistent variance = 1/m;
  - for a 60m / 12-segment episode, only 1/12 of the regime variance remains;
- isolated missing 5-minute samples can be bridged and still report 100% integration coverage.

Direct C# research workflow:
- run 36781580855;
- job 110112872942;
- SUCCESS.

No production statistical algorithm changed.
No PDF/report code changed.
No Windows build generated.

Next authorized work:
- Phase 2 only: compare candidate dependent-data completion methods;
- keep Enel completely outside candidate construction/tuning;
- retain current 2,000 simulation baseline during initial method comparison;
- use predeclared metrics: empirical coverage + Wilson CI, exact binomial calibration test, Holm correction, P50 bias/MAE/RMSE, interval width, and alpha=0.10 interval score;
- simulation-count convergence remains a separate later Phase 2 subtest.


## Phase 2 statistical method comparison — CLOSED — 2026-09-30

Phase 2 holdout completed.

Provisional Phase 3 candidate:
- `grid-import-context-block-bootstrap-120m.candidate-v1` / C2-120.

Untouched holdout summary:
- 8 scenario families;
- 1,000 paired repetitions each;
- 2,000 simulated completions per candidate;
- 90-day synthetic history.

C2-120:
- Holm-corrected undercoverage failures: 1/8;
- worst coverage: 86.9% (AR phi=0.95);
- mean coverage: 90.8%;
- mean normalized interval score: 1.059;
- paired interval-score improvement vs current C0: -0.764, 95% bootstrap CI [-0.895,-0.646].

Current C0:
- corrected failures: 7/8;
- worst coverage: 35.6%;
- mean coverage: 63.84%;
- mean normalized interval score: 1.822.

Known C2 limitation:
- very strong persistence;
- AR95 diagnostic coverage:
  - 30m 91.1%;
  - 60m 87.6%;
  - 120m 88.3%;
  - 240m 80.3%;
- 240m requires stitching two independent 120m blocks, reintroducing lost dependence.

Other discovery candidates:
- C3/C4 achieved slightly better average interval scores but failed holdout structural contexts (intrahour ramp and/or weekend shift);
- they do not advance.

No production code or PDF changed.
No Windows application build generated.
No Enel value used.

Next authorized work:
- Phase 3 real-data backtesting;
- first finalize/build a read-only research exporter that extracts only the whitelisted telemetry/context required from the user's latest `energy.db`;
- user should need to run that exporter once and return one research ZIP;
- Phase 3 must reject C2-120 and return to Phase 2 if real telemetry shows material undercoverage.


## Phase 3 research-exporter gate — READY / awaiting real-data package — 2026-09-30

Canonical statistical state:
- Phase 1: COMPLETE;
- Phase 2: COMPLETE;
- sole provisional Phase 3 candidate: `grid-import-context-block-bootstrap-120m.candidate-v1` / C2-120;
- candidate is not production-approved and must be rejected if real-data backtesting shows material undercoverage.

Research Exporter R1:
- source path: `research/statistical_uncertainty/research_exporter/`;
- purpose: create the minimal pseudonymized package required for Phase 3 and, where sufficient, Phase 4;
- source database is opened SQLite `Mode=ReadOnly`;
- excludes original identifiers, serials, credentials, Enel bills, utility readings, tariff data and raw API payloads from output;
- UX follows canonical progress pattern: local progress, Paso n/4, completion ping, exact output path.

Final green build:
- workflow: `Research Exporter Build`;
- run number: 4;
- run ID: `36786984422`;
- source commit: `716b87808f5183692de237205cc2a67c0b44d9c5`;
- conclusion: **SUCCESS**;
- artifact: `SolarOfThings-ResearchExporter-R1-win-x64`;
- artifact ID: `11130595515`;
- artifact size: 66,136,544 bytes;
- artifact SHA-256: `af325caf9aff4dc806fe2db5327ff48bcc7e6232f3f7008105251533bf800cf5`;
- artifact expiry: 2026-12-29.

Next owner action / Phase 3 gate:
1. extract the Research Exporter ZIP;
2. run `SolarOfThings.ResearchExporter.exe`;
3. use the latest `energy.db` (auto-detected when possible; otherwise select it manually);
4. choose an output folder;
5. generate one research package;
6. return only the generated `SolarOfThings-ResearchPackage-*.zip`.

No Build 494 visual QA, Enel bill entry, PDF generation or Windows-app regression QA is required for this extractor gate.

After the research ZIP is returned:
- verify manifest and SHA-256 entries;
- profile actual telemetry cadence/completeness/effective donor days/autocorrelation;
- execute Phase 3 paired masking/backtesting of C0 vs frozen C2-120 on real known telemetry;
- stratify by gap duration, hour/day type and load regime where sample size permits;
- pay particular attention to >=120-minute and >=240-minute gaps because C2-120's synthetic limitation is block stitching under extreme persistence;
- STOP and return to Phase 2 redesign if real-data calibration is materially inadequate.

No production statistical code or PDF wording changes are authorized before that gate passes.


## Statistical Phase 3 real-data gate — C2 REJECTED — 2026-09-30

Research package received and validated:
- `SolarOfThings-ResearchPackage-20260930-214934.zip`;
- package SHA-256 `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`;
- all manifest file hashes/lengths PASS.

Phase 2 omitted simulation-count gate repaired before scoring:
- 2k/5k/10k/20k/50k/100k tested against 500k numerical reference on realistic C2 donor geometries;
- 100k has best tail stability and remains preferred production-count candidate if runtime permits;
- Phase 3 structural comparison intentionally retained 2,000 simulations.

Real profile:
- 43,741 confirmed grid-import samples;
- 140/161 days with >=280 grid samples;
- residual autocorrelation ~0.955 at 5m, 0.803 at 30m, 0.659 at 60m, 0.449 at 120m.

Frozen 90-day historical-only backtest:
- C2 overall coverage:
  - 20m 93.0%;
  - 30m 93.0%;
  - 60m 92.1%;
  - 120m 93.4%;
  - 240m 92.7%;
  - 480m 83.3%.
- C0 is materially worse at every duration.

Critical conditional defect:
- when the observable gap-start grid value is >100 W, C2 coverage:
  - 20m 68.5%;
  - 30m 68.5%;
  - 60m 66.7%;
  - 120m 74.5%;
  - 240m 78.0%;
- 20–120m clustered 95% upper bounds remain below nominal 90%;
- P50 bias becomes increasingly negative with duration.

Hard active/inactive donor filtering was diagnostic only and did not rescue calibration; effective donor days collapsed to around 8 in many active cases.

Actual corpus has 28 source separations >15m, including 10 >240m and 4 >1440m, so long-gap behavior is operationally relevant.

Decision:
- **REJECT C2-120 as a general production model**;
- retain it only as research baseline;
- do not change production C# or PDF language;
- return to Phase 2R redesign.

Active next protocol:
- `research/statistical_uncertainty/PHASE2R_REAL_DATA_REDESIGN_PROTOCOL_2026-09-30.md`;
- first candidate R1 = boundary-state whole-gap day bootstrap;
- development dates through 2026-09-11;
- locked new-candidate holdout = 2026-09-13 through 2026-09-26;
- do not inspect R1 holdout unless development gate passes.


## Phase 2R — R2 locked holdout — REJECTED — 2026-09-30

Canonical base:
- Phase 3 v2 harness commit: `6bddba0325057bcff1bcc85e0b49e12bc6bdf9aa`;
- canonical per-case SHA-256: `726ae954afeb996bece4e3f30c4d9fc6f8275a4783691393cba0390dbb64f03f`;
- C2 rejection confirmed reproducibly.

R2:
- candidate: `grid-import-c2-grouped-day-calibrated.candidate-v3`;
- harness commit: `f56e91261380d7e499fd80772973a2af0ef7e7ce`;
- development cross-fit: PASS all seven frozen gates;
- frozen parameter commit: `5cc12e78bf7517c36114ab24d9fed238499e8036`;
- locked holdout 2026-09-13 through 2026-09-26 opened exactly once after development PASS.

Holdout result:
- active coverage: 100%;
- all duration coverages >=94.7%;
- however overall interval-score ratio vs raw C2 = **1.2187**, exceeding the <=1.10 gate;
- raw C2 active P50 bias = -0.6513 kWh;
- R2 active P50 bias = +0.8990 kWh;
- absolute active bias therefore worsened by ~38%;
- R2 FAILS the frozen proper-score and active-bias gates.

Decision:
- **R2 REJECTED**;
- do not retune R2 on the consumed holdout;
- high/100% coverage is not accepted when achieved by excessive width;
- fixed development-period calibration is not stable under observed temporal drift.

Observed diagnostic:
- active-short day-median residual rate shifted from development median ~+1.378 kW to holdout median ~+0.500 kW;
- active-long shifted from ~+0.510 kW to ~+0.380 kW;
- recent-prior residuals were materially lower than the full-development correction.

Next authorized research:
- Phase 2R R3 adaptive/rolling calibration design;
- use only prior observations at each prediction time;
- retain day-level weighting to avoid 5-minute pseudo-replication;
- evaluate proper interval score as well as coverage;
- current corpus may be used only for R3 **development/diagnostics**, because all existing dates have now participated in prior model design/evaluation;
- final R3 production approval requires a genuinely future real-data validation period not used to design R3.

No production statistical C# or PDF wording change is authorized.


## Statistical uncertainty consolidated interruption handoff — 2026-09-30

Canonical consolidated references:
- `research/statistical_uncertainty/STATISTICAL_RESEARCH_CONSOLIDATED_HANDOFF_2026-09-30.md`;
- `research/statistical_uncertainty/PROMPT_CONTINUE_STATISTICAL_RESEARCH_2026-09-30.md`.

Verified pre-handoff research HEAD:
- `13d1c307cfbebba7fb0e6b794c3a42e42303f86e`;
- latest research action at that point: freeze R3 boundary-residual future-validation protocol.

Interruption reconciliation:
- the earlier apparently interrupted Phase 3 v2 harness work did in fact complete;
- Phase 3 v2 is reproducible and C2 rejection is confirmed;
- R1 is rejected at development;
- R2 passed development but failed locked holdout and is rejected;
- R2 holdout must never be reused for retuning;
- no duplicate/partial build, publication or research operation was found at the verified pre-handoff HEAD.

Active statistical state:
- Phase 2R / R3;
- frozen candidate: `grid-import-boundary-residual-dayweighted-240m.candidate-v4`;
- canonical protocol: `PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`;
- R3 is **not validated and not production-approved**.

Existing owner package:
- ends 2026-09-27;
- is development/calibration-only for R3;
- cannot provide pristine R3 validation outcomes.

Future-validation epoch:
- starts 2026-09-28.

Frozen outcome-independent stopping evidence:
- >=15 distinct future dates with eligible ACTIVE-start targets;
- >=100 ACTIVE-start single-gap cases;
- >=10 ACTIVE-start cases in each duration group G20_30, G60_120, G240.

Current repo does not contain an executable R3 future-validation harness.
It is permissible to implement that harness from the already-frozen R3 protocol **without scoring any future outcomes**, so validation mechanics are fixed before a future package is evaluated.

After harness freeze:
- obtain/ingest a newer Research Exporter package containing post-2026-09-27 telemetry;
- validate integrity;
- count eligibility without looking at outcomes;
- if stopping evidence is insufficient, record INSUFFICIENT and do not score;
- only once stopping conditions are met, score R3 future validation exactly once.

No production C# statistical replacement, PDF wording change or Solar of Things user build is authorized before R3 future validation passes.


## Statistical uncertainty R3 harness freeze — 2026-10-06

The frozen future-validation harness for R3 is now implemented and persisted.

Canonical candidate:
- `grid-import-boundary-residual-dayweighted-240m.candidate-v4`.

Frozen protocol:
- `research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`.

Harness:
- `research/statistical_uncertainty/phase2r_r3_future_validation.py`;
- version: `phase2r-r3-future-validation.v1`;
- implementation commit: `0d7ebb7bf9ef648eae37bc4efd4a518ca42af759`;
- Git blob SHA: `8e50f134f35c83774edca47652ff22b8315acd1c`;
- file SHA-256: `39012e73ac5638f8b37f765d370ea91b1f0d994c21127c1e9a5d17e696504e57`.

Freeze receipt:
- `research/statistical_uncertainty/PHASE2R_R3_HARNESS_FREEZE_REPORT_2026-10-06.md`;
- report commit: `cc04aef844cfcf32a1e5dec7e0e1035ce41022b1`.

Important state change from the 2026-09-30 handoff:
- it is no longer correct to say that no executable R3 harness exists;
- the harness exists and is frozen before owner future outcomes have been scored;
- `count` is outcome-blind and may be run on future Research Exporter data;
- `score` is blocked until the frozen stopping rule is satisfied and then scores only through the first stopping date;
- no Enel/utility value enters construction, calibration, stopping, scoring or selection;
- no owner future validation result currently exists.

No newer owner Research Exporter ZIP was available in the 2026-10-06 continuation session, so no real future count/scoring outputs were persisted.

Calendar lower bound:
- validation starts 2026-09-28;
- 15 distinct future local dates cannot exist before 2026-10-12;
- therefore R3 cannot legally reach its stopping rule before 2026-10-12, and it may remain INSUFFICIENT after that if case/group counts are short.

Resume statistical work from:
1. obtain a newer genuine Research Exporter ZIP;
2. run R3 `count` only;
3. if `INSUFFICIENT`, stop without scoring;
4. only if `READY`, run frozen scoring exactly once and then perform structural gate-10 review;
5. only after a full PASS consider later production implementation.

Do not repeat Phase 1, Phase 2 candidate selection, Phase 3 v2, R1 or R2.
Do not modify production C#/PDF wording/builds merely because the R3 harness now exists.


## Statistical uncertainty R3 first future count checkpoint — 2026-10-06

A newer cumulative Research Exporter package was supplied and evaluated with the frozen R3 **count-only / outcome-blind** logic.

Input:
- `SolarOfThings-ResearchPackage-20261006-154413.zip`;
- SHA-256: `3a7bc7edb9e264333cd6da73b85f9f2d805b6eb4f5ef20594ddc34ffdbdd6295`;
- cumulative telemetry span: 2026-04-20 through 2026-10-06;
- manifest/internal hashes: PASS.

Canonical count receipt:
- `research/statistical_uncertainty/PHASE2R_R3_FUTURE_COUNT_REPORT_2026-10-06.md`;
- report commit: `68d4ee2262b84d7b4b423a2f070a628fe59295d1`.

Important result:
- `INSUFFICIENT`;
- stopping rule NOT met;
- no R3 truth, P5/P50/P95, coverage, bias, interval score or C2 outcome comparison was computed/inspected;
- no Enel/utility evidence was used.

Current outcome-independent evidence counts:
- 178 structural future targets;
- 178/178 have sufficient calibration history;
- 4 distinct future dates contain at least one eligible ACTIVE-start target (need 15);
- 36 ACTIVE-start cases (need 100);
- ACTIVE `G20_30`: 15 (minimum 10 reached);
- ACTIVE `G60_120`: 19 (minimum 10 reached);
- ACTIVE `G240`: 2 (need 10).

Future dates currently passing the >=280 grid-sample day-quality threshold:
- 2026-09-28;
- 2026-10-01;
- 2026-10-02;
- 2026-10-03;
- 2026-10-04;
- 2026-10-05.

2026-09-29 (240 samples) and 2026-09-30 (132) currently fail that threshold.
2026-10-06 had 191 samples at export time and was still a partial day.

Resume rule:
- later cumulative Research Exporter ZIPs are valid inputs;
- rerun only frozen `count` while status remains INSUFFICIENT;
- do not score until `READY`;
- do not synthetically fill deficient days;
- a later legitimate historical backfill may make a previously deficient future date eligible if its real source evidence reaches the frozen quality requirement.


## Enel bill-audit urgent evidence path — owner decision 2026-10-06

Canonical specification:
- `solar_of_things_windows_app/ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md`;
- freeze commit: `38e77899bf3c88ab8336169fd02c2e79cd2401e1`.

Owner decision:
- the immediate critical path is now a self-contained, printable Enel bill-audit evidence package;
- the document must stand on its own when handed to a third party without relying on the primary operator being present;
- the first real acceptance case is the 2026-08-28 through 2026-09-28 bill interval with 97 kWh billed;
- do not commit unnecessary customer PII/address/account identifiers to the public repository.

Product split remains mandatory:
1. arbitrary reading/period comparison;
2. Enel bill audit starting from one actual bill.

Canonical report structure:
1. executive energy summary + plain-language P5/P50/P95 + Enel-vs-P5/P50/P95 deltas;
2. economic comparison;
3. full economic decomposition;
4. energy evidence + early explanation of frames/gaps;
5. hard numerical data-quality/coverage evidence;
6. findings/inconsistencies;
7. sources/provenance;
8. technical methodology.

Separate deliverable:
- exhaustive technical evidence annex;
- printable if desired;
- CSV/XLSX/raw tabular export alongside it.

Temporal Enel convention, until contrary evidence appears:
- printed date range X–Y is treated internally as full local days;
- use [X 00:00, Y+1 00:00) as the preferred calculation representation;
- do not force the user to resolve 00:00/23:59 timestamp artifacts manually.

Economic rule:
- never scale the whole bill by kWh;
- classify lines as VARIABLE_POR_CONSUMO / FIJO / CONDICIONAL_REGULADO / NO_RECONSTRUIBLE;
- fixed lines remain invariant across Enel/P5/P50/P95;
- conditional lines are recalculated only when the rule is supported;
- preserve printed, reconstructed and counterfactual amounts as distinct semantics.

Statistical rule:
- observed inverter energy and estimated gap contribution must be shown separately;
- gaps are acknowledged, quantified and never silently treated as zero;
- Enel values remain excluded from statistical construction/calibration;
- R3 remains frozen and future validation remains INSUFFICIENT.

### Controlled exception to prior R3 production/PDF gate

The earlier rule that no PDF/build work may proceed until R3 passes is now narrowed by explicit owner decision.

Authorized in parallel with frozen R3 validation:
- bill-audit calculation development;
- bill-audit PDF/report implementation;
- report-specific UX improvements;
- tariff/economic reconstruction work;
- evidence annex/export work;
- work builds needed to complete the urgent evidence package.

Still forbidden:
- retuning R3 from future outcomes;
- scoring R3 before its stopping rule is READY;
- presenting R3 as validated before it actually passes;
- calibrating/selecting a statistical model against the Enel bill;
- weakening R3 gates;
- calling another provisional method “R3”.

The final urgent report may use a provisional statistical method only if:
- the exact method/version is stated;
- its real validation status is stated accurately;
- no stronger claim is made than the evidence supports.

### Immediate implementation sequence

Do not begin with visual PDF polishing.

First build the authoritative truth table for the real bill case:
1. bill evidence;
2. exact comparable interval;
3. inverter frames;
4. data coverage/gaps;
5. directly observed grid-import energy;
6. selected statistical completion method and P5/P50/P95;
7. official applicable tariff evidence;
8. bill-line classification and reconstruction;
9. energy/monetary inconsistencies;
10. provenance/version/hash metadata.

Only after those numbers are internally validated:
- render the main report;
- render the technical evidence annex;
- improve/reuse the demonstrated workflow in application UX.

### Open technical items, not owner-decision blockers

- choose the provisional statistical method for the urgent report while R3 remains unvalidated;
- determine whether target HPVINV02 exposes a trustworthy grid-import energy counter/aggregate;
- resolve exact Enel tariff version/applicability for the bill interval;
- resolve subsidy/conditional-charge rules;
- calculate final observed/P5/P50/P95/CLP results.

### Working relationship for this tranche

The assistant acts as technical director:
- orders dependencies;
- proposes and executes the technical sequence;
- surfaces only material owner decisions.

The owner acts primarily as:
- requester;
- priority/requirements adjuster;
- source of real-world evidence;
- human intermediary for actions that require local machine/browser/account access;
- final acceptance authority.

Do not turn ordinary implementation details into repeated owner approval gates.


## Urgent Enel audit truth-table execution — 2026-10-06

The urgent audit path has moved from specification into evidence construction.

Canonical product specification:
- `solar_of_things_windows_app/ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md`.

### Deterministic interval truth table

Harness:
- `research/bill_audit/bill_interval_truth_table.py`;
- version: `bill-interval-truth-table.v1`;
- implementation commit: `03dac66fd6f8510d49583f3c6577c23e14a4e3ae`.

Checkpoint:
- `research/bill_audit/BILL_INTERVAL_DETERMINISTIC_CHECKPOINT_2026-10-06.md`;
- commit: `d95195ce8d79b2be40a1dc53eb4221d2e79769b5`.

Real target bill interval:
- local `[2026-08-28 00:00, 2026-09-29 00:00)`;
- elapsed real time: 767 h because the Chile DST transition occurs inside the interval;
- 9,180 valid `grid_import_power_w` samples;
- unresolved/non-finite: 0;
- directly observed grid import: **88.065413 kWh**;
- covered time: **761.898707 h**;
- uncovered time: **5.101293 h**;
- temporal coverage: **99.334903%**;
- five uncovered intervals;
- dominant gap: 2026-09-12 15:09:33.752 to 19:32:00.957 local, **262.453 min**, boundaries 0 W / 0 W.

Important consequence:
- frozen R3 is inapplicable to the complete bill because the dominant individual gap exceeds its <=240m applicability limit;
- do not weaken R3 to make the bill fit.

### Provisional report-specific gap model

Harness:
- `research/bill_audit/bill_gap_calendar_window_empirical.py`;
- version: `bill-gap-calendar-window-empirical.v1`;
- implementation commit: `236ce274634a0a17cb3e0f0268981a6ceec4ba01`.

Research report:
- `research/bill_audit/BILL_GAP_CALENDAR_WINDOW_EMPIRICAL_REPORT_2026-10-06.md`;
- commit: `4c46b81ec796691650d5668da898eb1017e6f450`.

Method:
- complete historical windows rather than independent 5-minute donor frames;
- same local clock time + actual gap duration + weekday/weekend class;
- strictly prior dates;
- latest 15 eligible dates, minimum 10;
- equal empirical weight per date;
- inverse empirical q05/q50/q95;
- target boundary state intentionally NOT used to narrow the primary interval;
- short bill-edge slivers completed deterministically from nearest observed boundary;
- exact Cartesian aggregation across 3 internal gaps = 3,375 combinations;
- Enel billed kWh never used in construction/selection/backtest.

Provisional bill result:
- observed: **88.065413 kWh**;
- P5: **88.103333 kWh**;
- P50: **88.103333 kWh**;
- P95: **96.606322 kWh**;
- empirical mean: 89.100796 kWh;
- empirical maximum combination: 97.669742 kWh.

Post-model comparison only:
- printed Enel: 97.000 kWh;
- Enel - P50: 8.896667 kWh / 9.1718%;
- Enel - P95: **0.393678 kWh / 0.4059%**.

Interpret carefully:
- Enel is slightly above provisional P95;
- it is NOT correct to claim that 97 kWh is physically impossible;
- the high-side difference is small after conservative gap treatment;
- the method is `PROVISIONAL REPORT METHOD / RESEARCH ONLY`, not R3 and not production approval.

Topology-specific prequential historical coverage:
- 15.0m weekend: 92.6% (27 cases);
- 262.5m weekend: 96.0% (25 cases);
- 20.0m weekday: 95.9% (98 cases).

Supporting boundary-state diagnostic, not used to shrink the headline interval:
- 15m target start/end 0/0 W; 34 prior same-daytype INACTIVE-start windows, 0 non-zero;
- 262m target start/end 0/0 W; 33 prior same-daytype INACTIVE-start windows, 0 non-zero.

### Bill arithmetic / tariff clues

Checkpoint:
- `research/bill_audit/BILL_ARITHMETIC_AND_TARIFF_CLUES_2026-10-06.md`;
- commit: `6d7bedea48f29439cd8d30deeade48d4231217fa`.

Printed summary arithmetic:
- taxable 20,643 + IVA 3,922 + exempt 83 = total bill 24,648;
- 20,643 × 19% = 3,922.17 -> printed IVA 3,922;
- Servicio Común 5,964 - Subsidio 3,758 = Otros cargos/abonos 2,206;
- 24,648 + 2,206 = total due 26,854 exactly.

Main detailed service-line display:
- administration 727;
- electricity consumed 21,389;
- transport 2,072;
- meter rent 463;
- visible sum 24,651 vs printed total bill 24,648: 3 CLP display-level difference, treat as rounding/reconstruction issue rather than automatic error.

Bill-implied verification clues only:
- electricity: 21,389 / 97 = **220.505154639 CLP/kWh**;
- transport: 2,072 / 97 = **21.360824742 CLP/kWh**.

Do not use bill-implied rates as final tariff authority.

Official archive remains the authority.
Current public Enel archive exposes both:
- August 2026 retroactive regulated-supply tariff PDF;
- September 2026 regulated-supply tariff PDF.

Earlier repo evidence established August BT_AA/T5 electricity at 220.147 CLP/kWh; this cannot reproduce the new 21,389 line over 97 kWh, making a September ~220.505 candidate the leading hypothesis pending exact official-row confirmation.

### Immediate next order

1. close exact official tariff/component applicability for this bill;
2. resolve printed transport composition;
3. investigate target HPVINV02 grid-import energy counter/aggregate as an independent cross-check;
4. freeze economic truth table;
5. only then implement/report PDF and annex rendering.

Do not visually polish the PDF ahead of these truth-table gates.


## Urgent Enel audit owner-evidence handoff — 2026-10-06

Canonical handoff receipt:
- `research/bill_audit/OWNER_EVIDENCE_HANDOFF_CHECKPOINT_2026-10-06.md`;
- commit: `5ae21936a402324609465d2c4ac5ab531e4a3c28`.

Current invariant energy result:
- observed inverter import: 88.065413 kWh;
- temporal coverage: 99.334903%;
- provisional report P5/P50/P95: 88.103333 / 88.103333 / 96.606322 kWh;
- printed Enel: 97.000 kWh;
- Enel - provisional P95: 0.393678 kWh / 0.4059%.

Sensitivity:
- 10/12/15 comparable-date histories retain the exact same P95;
- longer histories lower P95;
- no-daytype stress lowers P95;
- current provisional high side is conservative relative to tested alternatives.

Economic classification:
- administration: fixed;
- electricity consumed: kWh-variable;
- transport: kWh-variable/composite official components;
- meter rent: fixed;
- service common: invariant actual-only for inverter counterfactual;
- subsidy -3,758: official second-semester monthly regulated benefit, invariant for this bill counterfactual;
- preserve an explicit rounding/reconciliation residual where needed.

Target-device evidence probe now exists:
- UI action: `Comprobar energía comprada a red`;
- reads monthly `pvInverterElectricityQuantityClass` aggregates and selected-key purchase counters;
- preserves `buyElectricityQuantity` reality flags and `dayPurchaseElectricityConsumption` candidates;
- read-only.

Validated portable:
- Windows Build **499**;
- run ID `37532423163`;
- tested HEAD `34d36042ef642493cd7880bf6d775e0bdb9f363c`;
- CI/SQLite smoke/publish: PASS;
- artifact `SolarEnergyMonitor-win-x64-dev`;
- artifact ID **11444757944**.

Next owner evidence pass:
1. run Build 499 with the existing complete portable `Data\` folder;
2. Technical diagnostics -> `Comprobar energía comprada a red`;
3. export investigation bundle and return the ZIP;
4. download and return the official Enel September 2026 regulated-supply PDF.

Once both files arrive:
- classify target HPVINV02 import-energy counter as real/placeholder/unusable;
- close official tariff/component rates;
- freeze economic truth table;
- proceed to audit PDF + technical annex implementation.


## Enel bill-audit economic truth table frozen — 2026-10-06

Canonical economic reconstruction:
- `research/bill_audit/BILL_OFFICIAL_TARIFF_RECONSTRUCTION_2026-10-06.md`;
- freeze commit: `06925faf4709602d06878c1c79440d1a89890f5a`.

Reproducible engine:
- `research/bill_audit/bill_economic_truth_table.py`;
- version: `bill-economic-truth-table.v1`;
- implementation commit: `38afcefc6d55ce5b6102608746c46f44c22c86be`.

Canonical case config:
- `research/bill_audit/BILL_ECONOMIC_CASE_2026-10-06.json`;
- commit: `d1aac9d19d50dd081fcda6e08fe58127c20cf394`.

Economic gate result:
- official tariff/component applicability is now sufficiently closed for implementation;
- do not continue treating September tariff or transport composition as open blockers.

Official cross-month rule:
- bill period 2026-08-28 through 2026-09-28 = 32 calendar days;
- August allocation = 4/32;
- September allocation = 28/32;
- apply calendar-day proportional allocation across the two tariff months.

Canonical official rates for the current case:
- August 2026 BT_AA/T5 electricity, published IVA column: 220.147 CLP/kWh;
- September 2026 BT_AA/T5 electricity, published IVA column: 220.539 CLP/kWh;
- transport, published IVA column: 20.489 CLP/kWh;
- public-service charge, exempt: 0.855 CLP/kWh;
- administration fixed official IVA-column amount: 727.230 CLP/month;
- FET <=350 kWh: no surcharge for all current scenarios.

Source-driven Enel control:
- reconstructed regulated bill subtotal = 24,648.128 CLP -> 24,648 printed;
- + Servicio Común 5,964;
- - Subsidio 3,758;
- reconstructed total due = 26,854.128 CLP -> 26,854 printed;
- printed total due = 26,854;
- result: exact agreement at printed-peso precision.

Audit conclusion:
- current evidence does NOT support a material tariff/rate error;
- the urgent dispute should focus on whether the billed **97 kWh** is the correct metered energy;
- do not dilute the report by alleging a tariff defect.

Canonical energy / monetary scenarios:
- directly observed inverter: 88.065413 kWh -> 24,693 CLP rounded total due;
- provisional P5: 88.103333 kWh -> 24,703 CLP;
- provisional P50: 88.103333 kWh -> 24,703 CLP;
- provisional P95: 96.606322 kWh -> 26,759 CLP;
- Enel control: 97.000000 kWh -> 26,854 CLP.

Differences versus printed Enel:
- observed: about -2,161 CLP;
- P5/P50: about -2,151 CLP;
- P95: about -95 CLP.

Interpretation remains conservative:
- Enel is 8.897 kWh above provisional P50;
- Enel is only 0.394 kWh above provisional P95;
- 97 kWh is not physically impossible under the historical empirical support;
- do not present the P95 comparison as proof of a large high-confidence overcharge.

Remaining owner-side evidence gate:
- target HPVINV02 independent grid-import energy counter/aggregate cross-check;
- Build 499 read-only action: `Comprobar energía comprada a red`;
- return exported investigation ZIP after running it on the target installation.

This counter is corroborating evidence only:
- it is not required to derive the existing 88.065 / P5 / P50 / P95 results;
- a placeholder/unavailable result will not invalidate the time-series evidence.

Next authorized sequence after counter classification:
1. freeze final source/provenance table;
2. implement source-driven multi-period tariff reconstruction in production bill-audit path;
3. implement the canonical 8-page report;
4. implement technical annex/raw-data export;
5. run one bundled target-PC PDF QA.

R3 remains separate, frozen and INSUFFICIENT.
Do not retune or score R3 from this bill work.


## Urgent Enel bill-audit implementation tranche ready for target QA — 2026-10-06

Evidence gates are now closed for the urgent current-bill tranche.

### Target-device counter result

Canonical classification:
- `research/bill_audit/HPVINV02_GRID_IMPORT_COUNTER_CLASSIFICATION_2026-10-06.md`;
- evidence ZIP SHA-256:
  `c7044f5eeb2bc6e970fa726c39877e77db395260428531d30c1f33065cd22f24`.

Result on the actual HPVINV02:
- `buyElectricityQuantity`: placeholder / `isRealValue=false`;
- `dayPurchaseElectricityConsumption`: not populated;
- no usable independent grid-import energy counter exists on the validated target surfaces.

Therefore:
- `mainsPower` -> `grid_import_power_w` timestamp integration remains canonical inverter-side energy evidence;
- this negative counter result does not invalidate the time-series evidence;
- do not spend more urgent-path time on undocumented counter aliases without new evidence.

### Final provenance table

Canonical:
- `research/bill_audit/BILL_AUDIT_FINAL_PROVENANCE_TABLE_2026-10-06.md`.

Source classes S1-S8 remain distinct:
- printed Enel bill;
- inverter telemetry;
- provisional bill-specific statistical completion;
- official Enel tariffs;
- regulatory rules;
- negative target-counter cross-check;
- economic truth table;
- separate frozen R3 stream.

### Bill-specific statistical production path

New service:
- `src/SolarOfThings.Core/Utility/UtilityBillGapStatisticalCompletionService.cs`;
- method: `bill-gap-calendar-window-empirical.v1`;
- status in external report: `PROVISIONAL / RESEARCH ONLY`.

The bill-audit report now:
- uses the full printed bill date interval as complete local days;
- no longer uses the arbitrary linked-reading timestamp interval for inverter energy integration;
- does not use the generic production pointwise bootstrap;
- keeps arbitrary reading comparison unchanged;
- preserves R3 unchanged/frozen.

Current canonical acceptance-case numbers remain:
- observed: 88.065413 kWh;
- P5: 88.103333 kWh;
- P50: 88.103333 kWh;
- P95: 96.606322 kWh;
- Enel: 97.000000 kWh;
- coverage: 99.334903%.

### Canonical eight-section report implemented

`UtilityBillAuditReportService` now renders the approved evidence hierarchy:

1. executive energy discrepancy;
2. economic effect;
3. economic decomposition/reconstruction;
4. energy evidence + treatment of gaps;
5. hard numerical quality/coverage evidence, including daily coverage;
6. findings/inconsistencies;
7. sources/provenance;
8. technical methodology/limitations.

Presentation changes:
- primary narrative says inverter records/telemetry rather than “Solar of Things”;
- P5/P50/P95 are explained in layman language;
- Enel-vs-P5/P50/P95 differences are explicit;
- old bootstrap/simulation wording removed;
- report preserves the conclusion that official tariff reconstruction is coherent and the dispute is centered on billed kWh;
- footer carries the producing application build/revision identity.

### Technical annex implemented

New service:
- `src/SolarOfThings.Core/Utility/UtilityBillAuditAnnexExportService.cs`;
- export version: `bill-audit-evidence-annex.v1`.

The audit UX now exposes two separate actions:
- `Exportar informe de auditoría PDF`;
- `Exportar anexo técnico + datos`.

The annex ZIP contains:
- `00-Anexo-Tecnico-Evidencia-Numerica.pdf` — printable raw numeric sheet;
- `01-telemetria-importacion-red.csv` — frame-level grid-import evidence;
- `02-intervalos-sin-telemetria.csv`;
- `03-escenarios-economicos.csv`;
- `04-fuentes-y-metodo.txt`;
- `05-cobertura-diaria.csv`;
- `99-manifest-integridad.json` with per-file SHA-256.

The printable annex includes every valid grid-import frame for the audited period, real timestamps, W, next-link delta, observed Wh contribution, link status, confidence and normalization source/rule.

Export is off the UI thread and report/annex actions mutually disable while one export is running.

### Target QA build

Windows Build **527**:
- workflow run: `37538625099`;
- tested code HEAD:
  `c3ba58b371c6a9331416f0aae91061d8005f6b02`;
- artifact: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11447302070`;
- artifact digest:
  `sha256:0c9a91c03e0601e3ad6f51642c5c458df39c7748b32351e45be5ef5bf33ba68e`;
- Build: PASS;
- SQLite smoke: PASS;
- bill-audit PDF smoke: PASS through the consolidated smoke;
- annex ZIP smoke: PASS through the consolidated smoke;
- portable publish/upload: PASS.

Build 527 is the coherent owner QA handoff for this tranche.

### Next owner QA — one bundled pass only

Reuse/copy the existing complete portable `Data\` folder.

In Build 527:
1. open `Red eléctrica` -> `Auditoría de boleta`;
2. select the current bill with period 28-08-2026 -> 28-09-2026 / 97 kWh if already present;
3. export `informe de auditoría PDF`;
4. export `anexo técnico + datos`;
5. return the PDF and annex ZIP together.

If the current bill is not present in the selector:
- do not create a duplicate blindly;
- report that fact and stop that QA path so bill-entry/import can be corrected deliberately.

If the PDF reports missing official tariff source instead of reconstructing the current bill:
- return the PDF as-is;
- do not manually invent/import rates just to make the test pass;
- the next fix must close the source-presence path from evidence.

Do not ask the owner to repeat the HPVINV02 counter probe.

### Next assistant action after returned artifacts

- inspect PDF visually/semantically;
- inspect annex integrity and exact numbers;
- verify target-PC output against canonical 88.065 / P5-P50 88.103 / P95 96.606 and economic truth table;
- fix any layout/source-presence defects in one internal tranche;
- then produce the final printable evidence package.


## Build 527 returned QA + P95 validation + Build 529 handoff — 2026-10-06

Owner returned:
- `Auditoria-Boleta-Enel-20261006-1918.pdf`;
- `Anexo-Tecnico-Boleta-Enel-20261006-1921.zip`.

### Returned PDF findings

Confirmed on rendered output:
- real text overlap in Section 5 gap-detail rows, especially `BOUNDARY_START` / `BOUNDARY_END` against local timestamps;
- methodological-controls callout was orphaned onto a near-empty following page;
- page 2 reported `TARIFA NO RESUELTA`;
- page 3/4 official-rate verification still showed multi-period derivation pending;
- page 9 source table reported no resolved tariff publication;
- annex economic CSV contained header only.

The energy/statistical sections otherwise reproduced the canonical values.

### Annex integrity

All manifest-listed files:
- byte-count PASS;
- SHA-256 PASS.

### Independent P95 re-validation

Canonical validation report:
- `research/bill_audit/BILL_P95_POST_QA_VALIDATION_2026-10-06.md`.

Independent reproduction from the original Research Package confirms:
- observed = 88.06541295361109 kWh;
- deterministic bill-edge completion = 0.03791997222222222 kWh;
- exact combinations = 3,375;
- P5 = 88.10333292583331 kWh;
- P50 = 88.10333292583331 kWh;
- P95 = 96.60632217736110 kWh;
- maximum empirical combination = 97.66974164694443 kWh.

Decision:
- KEEP provisional P95 = 96.606322 kWh;
- do not retune post hoc;
- do not claim 97 kWh is statistically impossible/excluded;
- report that Enel is only 0.394 kWh above provisional P95 under the stated independent-gap aggregation;
- disclose full empirical support reaching 97.670 kWh;
- preserve the robust central discrepancy Enel vs P50 = 8.897 kWh.

Method caveat strengthened:
- each gap uses 15 empirical windows;
- inverse empirical q05 = minimum and q95 = maximum of those 15 at gap level;
- aggregate P95 is the 95th percentile of 3,375 exact combinations;
- gap independence is provisional and not a metrological probability model.

### Fixes after returned QA

Tariff:
- cross-publication candidate identity now prefers semantic RED/network + ETR identity over parser-local `CandidateIndex`;
- commit `c8657204f84af8839462ec9349808ab07785814e`;
- Build 528 PASS.

Report/P95/layout:
- executive P95 language no longer implies statistical exclusion;
- full empirical maximum is shown/used in interpretation;
- H02 changed to a minor high-side difference rather than strong review finding;
- gap-kind labels shortened/localized;
- gap table columns rebalanced;
- methodological controls moved before daily coverage;
- daily coverage intentionally starts on a dedicated continuation page;
- resolved tariff scenario uses compact source evidence instead of obsolete multi-period-pending table;
- technical methodology now discloses discrete quantile behavior and gap-independence limitation;
- commit `78881b61f12a6ff3c37ccd920317ae8b14fca71e`;
- Windows Build 529 PASS.

### Build 529 owner QA handoff

Workflow run:
- `37541249420`.

Artifact:
- `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `11448468241`;
- digest `sha256:fbe3a4d88c4fdef876323a3a931ad5427ff984dc62e4d6bd5b93905411c87678`.

Code HEAD in build:
- `78881b61f12a6ff3c37ccd920317ae8b14fca71e`.

Next owner pass:
1. reuse/copy the same complete `Data\` folder;
2. run Build 529;
3. select the same 28-08-2026 -> 28-09-2026 / 97 kWh bill;
4. export audit PDF;
5. export technical annex ZIP;
6. return both files.

Owner does not need to manually validate numbers/layout before returning them.
Assistant will inspect both artifacts.

Acceptance checks on return:
- no text overlap;
- no orphaned controls page;
- tariff/economic model resolves on target DB;
- page 2 contains economic scenarios;
- annex economic CSV contains scenario rows;
- energy values remain canonical;
- P95 wording includes dependence caveat / empirical maximum;
- source/provenance pages remain consistent.


## Build 530 supersedes Build 529 for owner QA — 2026-10-06

Build 530 contains all Build 529 fixes plus one final semantic correction:
- every executive delta now uses the same convention:
  `Enel - escenario`;
- this removes the Build 527 contradiction where metric cards used scenario-minus-Enel while the comparison table used Enel-minus-scenario.

Workflow run:
- `37541568659`.

Artifact:
- `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11448681116`;
- digest:
  `sha256:a69255169ec4578b4cd060ca57ba65895313f9c96e77eb0702ceb8d6a8a5072d`.

Code HEAD:
- `3ef48ad991c2339f853c6f6c08f598c132687f9c`.

Build: PASS.
SQLite smoke: PASS.
Portable publish/upload: PASS.

Use Build 530, not Build 529, for the next owner PDF/anexo QA.


## Build 530 export crash fixed — Build 533 QA handoff — 2026-10-06

Owner QA on Build 530 hit both audit export actions with:
- `Sequence contains more than one matching element`.

Root cause:
- the Build 528 semantic cross-publication tariff matching correctly stopped trusting parser-local `CandidateIndex`;
- the real target DB contains duplicate normalized tariff candidates with the same semantic RED/ETR identity and, in at least one path, the same candidate index;
- `FindSameCandidate` still used `SingleOrDefault`, which throws when more than one equivalent row exists.

Fix:
- equivalent duplicate tariff candidates are collapsed deterministically only when component, RED/ETR identity, unit, net rate and published IVA-column rate all agree;
- if duplicate semantic candidates disagree materially in rates, the tariff remains unresolved/ambiguous rather than selecting one silently;
- duplicate preferred retroactive publications are also hardened: exact same-file duplicates may collapse only when preserved SHA-256 proves equivalence; otherwise no silent choice is made;
- code commit:
  `143cb605c7e16438aa33699d05bf0eb90b5c74af`.

Regression coverage:
- smoke test now deliberately inserts an exact duplicate normalized `ELECTRICITY_CONSUMED` tariff candidate;
- scenario reconstruction must complete without `Sequence contains more than one matching element`;
- smoke commit:
  `04450c38b8ce3031abf5bdca3392d528d66325bc`;
- timezone-safe regression adjustment:
  `bb125601004660a3038e27bb87f9502c865ac50e`.

Build 533:
- workflow run: `37542899070`;
- code HEAD:
  `bb125601004660a3038e27bb87f9502c865ac50e`;
- artifact: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11449352529`;
- digest:
  `sha256:59c65d4f722df3024f321f37282ee499e10d39b71af8a7a41dada96f420c4054`;
- Build: PASS;
- SQLite smoke: PASS;
- duplicate tariff-candidate regression: PASS;
- portable publish/upload: PASS.

Build 533 supersedes Build 530 for owner QA.

Next owner action:
1. reuse/copy the same complete `Data\` folder;
2. run Build 533;
3. select the same 28-08-2026 -> 28-09-2026 / 97 kWh bill;
4. export the audit PDF;
5. export the technical annex ZIP;
6. return both generated artifacts.

Do not repeat the HPVINV02 purchased-energy counter probe.


## Build 533 returned QA hardening — Build 538 final-visual-QA handoff — 2026-10-06

Returned artifacts:
- `Auditoria-Boleta-Enel-20261006-2102.pdf`
  - SHA-256 `152f32e14e5fc687c4ab4a15a24e66e144a177729a1925b518d0ebaaf02f1b76`;
- `Anexo-Tecnico-Boleta-Enel-20261006-2105.zip`
  - SHA-256 `99f23cd0240d79f2c98ebbf7f7eb731fcdd2e44c1f084530ccac2e91ba31f85f`.

Canonical returned-QA report:
- `research/bill_audit/BUILD_533_RETURNED_QA_CHECKPOINT_2026-10-06.md`.

Build 533 content status:
- energy values PASS;
- provisional P95 semantics PASS;
- tariff/economic scenario reconstruction PASS;
- digital annex hashes/integrity PASS;
- previous export crash PASS/fixed;
- previous report gap overlap PASS/fixed.

Additional defects found from the real returned files:
1. printable 128-page annex footer overlaps the last data row on ordinary full pages;
2. page-8 tariff summary incorrectly looks September-only even though the model correctly uses August retroactive + September;
3. aggregated OtherCharges captured value has opposite sign from printed bill arithmetic; report must derive net other charges from TotalDue - GrossBill when possible;
4. blank local tariff-plan capture must not visually imply the original bill omitted the printed tariff;
5. P5=P50 should be explicitly explained;
6. fixed/conditional charge treatment should be explicit on the economic decomposition page.

Hardening commits:
- `68d70f3866821bf2e7a186be13efd89ca8b6e8e8` — carry tariff URL/hash into bill evidence;
- `9587da658cd26e5525137a70aa788b82a1c4a97c` — fix aggregate sign semantics and multi-period tariff provenance;
- `4a926640a55f41804cab5e83c9581c2aa226468f` — reserve printable-annex footer space and include tariff source hashes;
- `259df01c5f172411e9d4de7e25e6a4fab26e7ac0` — clarify P5=P50 and variable/non-variable scenario wording;
- `c21e07a3f67f44c589ab5a076142f622e218517b` — add explicit fixed/conditional-charge treatment table.

Build 538:
- workflow run `37551188265`;
- code HEAD `c21e07a3f67f44c589ab5a076142f622e218517b`;
- artifact `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `11453110906`;
- digest `sha256:acb0854f6ba3ec06852b2869a38920705ae768699efb221d1cb6182e93bf6ba1`;
- Build PASS;
- SQLite smoke PASS;
- portable publish/upload PASS.

Build 538 is the final visual-target-QA handoff for this hardening tranche.

Owner action:
1. reuse/copy the same complete `Data\` folder;
2. run Build 538;
3. select the same current bill;
4. export audit PDF;
5. export technical annex ZIP;
6. return both artifacts.

Owner does not need to manually inspect/validate before returning them.

Assistant acceptance checks on return:
- audit report has no overlaps;
- page 3 shows net other charges/credits with correct arithmetic sign;
- page 3 includes explicit fixed/conditional charge treatment;
- page 8 shows both August retroactive and September tariff periods;
- source/provenance includes tariff hashes;
- annex printable footer no longer overlaps last data row;
- annex manifest lists tariff source URL/hash provenance;
- canonical energy/economic numbers remain unchanged.


## Build 538 returned acceptance + mandatory chat-handoff rule — 2026-10-06

Returned owner artifacts:
- `Auditoria-Boleta-Enel-20261006-2123.pdf`;
- `Anexo-Tecnico-Boleta-Enel-20261006-2130.zip`.

Canonical acceptance report:
- `research/bill_audit/BUILD_538_FINAL_RETURNED_ACCEPTANCE_2026-10-06.md`.

Returned artifact hashes:
- audit PDF:
  `0030557b15821ba6ec2c489f2a93dc3fb1d5a9fa39f8dc065c683df84fa35f40`;
- annex ZIP:
  `633e65bf897a60a8c6183761510432a321c362fbf7007540ed158c3fa73f3844`.

Decision:
- Build 538 returned PDF + ZIP are **ACCEPTED FOR CURRENT ENEL EVIDENCE PACKAGE**;
- no further statistical retuning is authorized merely to widen/narrow the discrepancy;
- current method remains provisional/research-only;
- current bill evidence package is usable independently of future R3 validation.

Acceptance highlights:
- main PDF visual QA PASS;
- canonical energy numbers unchanged;
- P95 semantics PASS;
- multi-period economic reconstruction PASS;
- fixed/conditional charge treatment explicit;
- August retroactive + September tariff provenance explicit;
- tariff hashes present;
- annex manifest integrity PASS;
- 132-page printable annex footer no longer overlaps rows on sampled beginning/middle/end pages;
- telemetry/daily/economic CSVs reconcile with report.

Separate future technical tranche (not required for current evidence package):
1. explicit official tariff supersession/correction graph;
2. semantic duplicate resolution;
3. direct Enel catalog -> static `content/dam` PDF acquisition;
4. CNE regulatory correction graph/cross-check;
5. browser-assisted import only as fallback.

### Mandatory owner handoff rule for future assistant turns

Owner explicitly requested that, because chat termination may occur without warning, every substantial future work cycle must end with BOTH:

1. **Repository consolidation**
   - update canonical continuity/status with:
     - exact phase/state;
     - relevant branch/HEAD or immutable commits;
     - owner decisions/observations;
     - completed work;
     - pending gates;
     - next authorized step;
     - hashes/build IDs when relevant.

2. **Copy/paste continuation prompt in the user-facing response**
   - provide one clearly boxed/code-block prompt suitable for a brand-new chat;
   - it must be self-contained and instruct the new chat to recover canonical references from repo rather than trusting chat memory;
   - include current work objective, accepted evidence, active constraints, pending work and next safe action;
   - explicitly warn against repeating already closed probes/builds/statistical retuning;
   - if a build/artifact is awaiting owner QA, include exact build/run/artifact details;
   - if no owner action is pending, state the next assistant-directed tranche instead.

This handoff rule remains active until the owner explicitly revokes it.


## Tariff precedence + acquisition tranche owner-QA handoff — 2026-10-06

Canonical tranche report:
- `research/bill_audit/TARIFF_PRECEDENCE_AND_ACQUISITION_TRANCHE_2026-10-06.md`.

Accepted Build 538 current-bill evidence package remains frozen and unchanged.
Do not regenerate or retune it merely because tariff infrastructure advanced.

### Implemented

Schema:
- schema version 14;
- official tariff document identity metadata;
- explicit `tariff_publication_relation` graph;
- in-place v13 -> v14 migration.

Precedence:
- explicit official correction/supersession graph is highest authority;
- byte-identical duplicate evidence may collapse by SHA-256;
- unique later official date may decide only for explicitly corrective/retroactive evidence;
- unique retroactive remains fallback;
- ambiguous/conflicting evidence stays ambiguous;
- never select by SQLite ID, captured/updated time, CandidateIndex or filename recency alone.

CNE:
- stable official IDs such as `REX-380-2026`;
- official publication date persisted;
- `CORRECTS` relation persisted/resolved;
- live validation PASS:
  - 12 2026 VAD documents;
  - 2 corrections;
  - 0 failures in validated capture;
  - 368 dated 2026-07-17;
  - 380 dated 2026-07-24;
  - 380 CORRECTS 368;
  - resolver selects 380 and supersedes 368.
- January correction chain 819 -> 816 is also captured by the same mechanism.

Production:
- bill verification and economic-scenario analysis both consume the relation graph;
- Tariffs UI now consumes the same resolver;
- UI labels explicit official correction/supersession states.

Enel acquisition:
1. live official catalog;
2. last valid official catalog cache;
3. migrate legacy cache only if it actually parses as valid;
4. filename-family probing from already-known/manual-imported official Enel PDFs;
5. opportunistic direct `content/dam` probing;
6. PDF magic validation;
7. declared-effective-month validation;
8. manual browser/import remains last fallback.

Important external limitation:
- GitHub Actions live tests received Imperva/Reese HTML challenge for both catalog and direct `content/dam` PDF request;
- direct asset probing is NOT a guaranteed bypass;
- do not implement unsupported anti-bot circumvention;
- CNE remains automatically accessible;
- owner residential/target network must be classified by target QA.

Validation:
- Build 546: schema14 + synthetic correction graph PASS;
- Build 558: v13 -> v14 in-place migration PASS;
- Build 561: live CNE correction graph PASS;
- Build 562: CNE live probe independent of Enel live outcome;
- Build 566: current owner-QA code Build + SQLite smoke + portable PASS.

Build 566:
- workflow run `37555998081`;
- app code HEAD `6f8128db814edfa0832eeb3cac991fdc6f041878`;
- artifact `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `11455095513`;
- digest `sha256:885789f95e02759436b8d18e85d2b4ad4655b173dea095e94023d49bf3c3fe23`.

### Next owner action

Use a COPY of the existing Build-538-era complete `Data\`.

Build 566:
1. launch app; successful startup validates real target migration v13 -> v14;
2. `Red eléctrica -> Tarifas oficiales`;
3. select 2026;
4. click `Descargar / actualizar año`;
5. DO NOT manually import Enel PDFs yet if automatic Enel fails;
6. return:
   - screenshot with full tariff status line;
   - screenshot of grid around August CNE rows showing 368/380 version states if possible;
   - note whether UI remained responsive.

Expected:
- CNE automatic update works;
- 380 shows as current official correction;
- 368 shows superseded/corrected;
- Enel may succeed via available target-network route OR may report Imperva; either outcome is valid diagnostic evidence.

After owner QA:
- if pass, freeze tranche;
- do not regenerate accepted Build 538 bill evidence solely for this infrastructure update;
- optional future enhancement: explicit Enel regulatory-basis parsing only if future multi-retroactive ambiguity makes it necessary.

Mandatory owner handoff rule remains active:
every substantial cycle ends with repo consolidation + self-contained new-chat continuation prompt.


## Build 575 supersedes Build 566 for tariff owner QA — 2026-10-06

Build 575 is the canonical current owner-QA handoff for the tariff
precedence/acquisition tranche.

Why it supersedes Build 566:
- it contains all schema14 / correction-graph / resilient-acquisition work;
- it also contains the browser-assisted Enel fallback based on Edge WebView2;
- the browser-assisted action is wired into the Tariffs UI and mutually
  exclusive with other import/update operations;
- Spanish/English guidance is updated.

Build 575:
- workflow run `37556413195`;
- code HEAD `1bda3f73a647c9cc6b5d02f73cd5d968f19570fb`;
- artifact `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `11454832323`;
- digest `sha256:fca91116ea603e786dee7aead5a1fab3c6107d66c5a273dcf85931cdcda26aa4`;
- Build PASS;
- SQLite smoke PASS;
- portable publish/upload PASS.

Browser-assisted semantics:
- normal WebView2 browser session to the official Enel archive;
- no unsupported anti-bot circumvention;
- only official tariff-PDF-looking downloads are intercepted;
- downloaded PDF is routed into existing controlled import/hash/normalization;
- temporary browser file is removed after canonical import;
- manual import remains fallback.

Owner QA now:
1. COPY accepted Build-538-era `Data\`;
2. launch Build 575 (migration v13 -> v14 target gate);
3. `Red eléctrica -> Tarifas oficiales`;
4. year 2026;
5. `Descargar / actualizar año`;
6. screenshot full status + August CNE 368/380 version rows;
7. report responsiveness;
8. if automatic Enel succeeds: stop;
9. if Enel automatic reports web protection:
   - `Capturar Enel en navegador…`;
   - navigate normally;
   - download ONE known official September-2026 tariff PDF;
   - app should auto-import/normalize;
   - return browser/status screenshot;
10. do not use manual PDF import during this QA unless requested later.

Do NOT regenerate the accepted Build 538 bill audit/annex for this infrastructure QA.

Canonical tranche report has been updated with Build 575 handoff.


## Build 575 target QA partial PASS; Build 577 is current browser-assisted QA — 2026-10-06

Owner evidence from Build 575 established:

PASS:
- real target app startup with copied Build-538-era `Data\`;
- schema v13 -> v14 target migration;
- CNE 2026 automatic capture:
  - 12 VAD documents;
  - 2 corrections;
  - 0 failures;
- REX 380 / August = `Corrección oficial vigente`;
- REX 368 / August = `Rectificada por corrección`;
- UI remained responsive;
- Enel automated HTTP correctly reported web-protection blocking;
- integrated browser could reach the official September-2026 Enel tariff PDF.

FAIL/DEFECT:
- Enel's ordinary `DESCARGAR` action opened the official PDF in external Edge;
- the subsequent PDF-viewer download belonged to external Edge;
- Solar therefore received no `CoreWebView2.DownloadStarting`;
- no automatic import/normalization occurred after owner saved the PDF.

Owner asked whether to bypass the web protection.

Decision:
- NO anti-bot circumvention;
- retain official/browser-normal flow;
- fix browser-window ownership instead.

Fix:
- official Enel `NewWindowRequested` requests are now handled inside the same WebView2;
- official tariff-PDF navigation stays inside the integrated browser;
- PDF-specific download/import guidance remains visible;
- `Atrás` action added.

Build 577 is current QA handoff and supersedes Build 575 for this final gate.

Build 577:
- workflow `37558525946`;
- code HEAD `0d66e6147c641ca42578cf0ba17ae0df626ee4e3`;
- artifact `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `11455624411`;
- digest `sha256:07f08bc4416187fae4d36d1ad5c90309161ae523cc1283e2a1f006045cd43d99`;
- Build PASS;
- SQLite smoke PASS;
- portable PASS.

Next owner QA:
1. reuse same copied QA `Data\`;
2. Build 577;
3. `Red eléctrica -> Tarifas oficiales -> Capturar Enel en navegador…`;
4. press same September-2026 `DESCARGAR` action;
5. expected Gate A: PDF remains inside integrated Solar browser, not external Edge;
6. click integrated PDF viewer download icon once;
7. expected Gate B: app reports import/normalization; remains responsive;
8. return screenshots/status/error verbatim.

No need to repeat:
- CNE capture;
- 380/368 precedence proof;
- migration v13->v14 proof;
unless Build 577 fails to start.

Do NOT regenerate accepted Build 538 bill evidence.
Do NOT bypass Imperva/Reese.

Mandatory owner handoff rule remains active:
end every substantial cycle with repo consolidation + self-contained new-chat continuation prompt.

## Build 577 owner QA — integrated PDF/download reached; import status still unproven — 2026-10-06

Owner returned screenshots from Build 577.

Evidence observed:

PASS / strongly demonstrated:
- Build 577 starts and runs on the copied QA Data folder;
- previously validated CNE/migration gates remain intact;
- official September-2026 Enel tariff PDF is now opened within the browser-assisted flow rather than being handed off to a separate external Edge workflow;
- the PDF viewer's own download action was used;
- the browser download flyout shows the official file:
  Enel Distribución Chile SA._Tarifas Suministro Eléctrico 8T_ VAD 5T Septiembre de 2026.pdf;
- the main tariff grid remains responsive and still contains the September Enel row with SHA prefix 27b65928d47c.

Important limitation:
- the returned screenshots do NOT show the integrated-window status text after the viewer-download action;
- because September 2026 already existed in the QA database and re-import is idempotent, the unchanged grid row/hash cannot by itself prove that CoreWebView2.DownloadStarting routed the file through EnelTariffPdfImportService;
- therefore Gate A (integrated navigation / viewer download) is effectively PASS;
- Gate B (automatic import + normalization triggered by the viewer download) remains NOT YET CONCLUSIVELY PROVEN.

Do not ask the owner to repeat:
- schema v13 -> v14 migration;
- CNE 12-doc / 2-correction capture;
- 380 -> 368 precedence;
- Enel HTTP-protection classification;
- proof that integrated WebView2 can open the official September PDF.

Next safe action in a new chat:
1. recover repo authority first;
2. inspect current browser-assisted code and Build-577 handoff;
3. decide the least-cost way to prove Gate B:
   - ideally obtain/observe the integrated status after one viewer download;
   - or add a deterministic visible/auditable import receipt in UI (filename, SHA prefix, candidates count, timestamp) so idempotent re-import can still be proven;
   - do not infer success from an unchanged grid row;
4. if Gate B is proven, freeze the tariff precedence/acquisition tranche;
5. if not, fix only the WebView2 viewer-download -> import handoff; do not revisit CNE, schema migration or tariff precedence.

Accepted Build-538 bill evidence remains frozen and must not be regenerated for this infrastructure gate.


## Build 578 — auditable browser-import receipt candidate — 2026-10-06 / 2026-10-07

Gate B remains the only open owner gate, but the code path is now instrumented so one
viewer-download click can prove or disprove it without relying on an idempotent grid row.

During source inspection a provenance defect was found in the Build-577 browser path:
the temporary download filename was prefixed with a GUID before being passed to
`EnelTariffPdfImportService`. Because that service derives the canonical official title
from the source filename, a successful browser import could have persisted the temporary
GUID-prefixed name rather than the original official Enel filename.

Build 578 fixes that defect by:
- putting uniqueness in a GUID-named temporary directory;
- preserving the downloaded official PDF filename unchanged;
- deleting the temporary file/directory after the canonical copy is created.

Build 578 also adds an explicit, persistent in-window receipt:
- any `CoreWebView2.DownloadStarting` event is visibly recorded;
- a compatible download records that interception is active;
- successful `EnelTariffPdfImportService` completion records:
  - official filename;
  - publication id;
  - full SHA-256;
  - page count;
  - normalized-candidate count;
  - outcome:
    `NEW_PUBLICATION`,
    `EXISTING_IDENTICAL_REIMPORT`,
    `CAPTURED_EXISTING_DISCOVERY`, or
    `UPDATED_EXISTING_PUBLICATION`;
  - UTC completion timestamp;
- the receipt remains separate from transient navigation/status text.

Microsoft WebView2 documentation confirms that `DownloadStarting` is raised when a
WebView2 download begins and that setting `Handled=true` hides the default download UI
while allowing the download to continue. Therefore the Build-577 download flyout alone
was not sufficient proof that the host handler ran; the Build-578 receipt is the direct
gate evidence.

Implementation commit:
- `53a7fa184c7d90e8242ec32b762060f2a9f1c108`
  — `Add auditable Enel browser import receipt`.

Windows Build:
- run ID: `37561379685`;
- run number / build: **578**;
- Build: **PASS**;
- SQLite smoke: **PASS**;
- portable publish: **PASS**;
- live Enel/CNE probes: intentionally skipped because this change does not alter
  HTTP acquisition, CNE capture, tariff precedence, or the already accepted live-source
  classifications.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11457196560`;
- digest:
  `sha256:db76026303a07256a034e5ed5aa9dce415ba9ab3e7ecd4f6c8354bd602fce676`.

### Minimum owner QA for Build 578

Do not repeat migration, CNE, 380/368, HTTP-protection, annual refresh, or the proof that
the official PDF can open inside integrated WebView2.

Use the same copied QA `Data\` folder and:
1. run Build 578;
2. open `Red eléctrica -> Tarifas oficiales -> Capturar Enel en navegador…`;
3. navigate to the same September-2026 official PDF;
4. click the integrated PDF viewer download icon **once**;
5. return one screenshot containing the persistent receipt text.

Gate B PASS requires the receipt to show:
- `CoreWebView2.DownloadStarting → EnelTariffPdfImportService: COMPLETADO`;
- the official Enel filename without a GUID prefix;
- SHA-256;
- page count;
- normalized-candidate count;
- an import outcome;
- UTC timestamp.

For the already-present September file, the ideal expected outcome is
`existente · reimportación byte-idéntica`; an unchanged tariff-grid row is not required
and is not gate evidence.

If no `DownloadStarting` receipt appears, or a receipt appears but the filename is
classified as incompatible, that single screenshot is sufficient diagnostic evidence for
the next fix. Do not repeat the broader QA.

Accepted Build-538 bill evidence remains frozen and must not be regenerated.


## Build 578 owner QA — Gate B PASS / tariff acquisition tranche CLOSED — 2026-10-07

Owner returned the Build-578 target-PC screenshot after one click on the integrated
WebView2 PDF viewer download action.

Direct visible receipt proved the full browser-assisted path:

`CoreWebView2.DownloadStarting → EnelTariffPdfImportService: COMPLETADO`

Receipt evidence:
- official file:
  `Enel Distribución Chile SA._Tarifas Suministro Eléctrico 8T_ VAD 5T Septiembre de 2026.pdf`;
- result:
  `existente · reimportación byte-idéntica`;
- SHA-256:
  `27b65928d47c34d46da4afa25c094734432be860750dac31e1deddb6a4d3ab17`;
- publication id: `29`;
- pages: `18`;
- normalized candidates: `7786`;
- UTC completion time: `2026-10-07 02:42:31`.

The transient status line showed the browser download name with `(1).pdf`, which is the
normal browser duplicate-download suffix. The canonical receipt correctly normalized that
suffix away and retained the official filename, so provenance is preserved.

Therefore:
- Gate browser-assisted PDF import: **PASS**;
- SHA/provenance preservation: **PASS**;
- candidate normalization: **PASS**;
- idempotent re-import classification: **PASS**;
- application responsiveness during this QA: visually maintained;
- tariff precedence/acquisition tranche: **CLOSED / FROZEN**.

No further owner QA is required for this tranche unless a new defect appears.

Already accepted Build-538 bill evidence remains frozen and unchanged.

Owner instruction update:
- do not emit new-chat continuity prompts until the owner explicitly re-enables them;
- canonical user-facing QA build download filename remains:
  `SolarEnergyMonitor-Build-XXX-win-x64.zip`.


## Owner batching decision — combined next tranche — 2026-10-07

Owner decision:
- do **not** create a dedicated build/QA cycle solely for the Enel browser-assisted UX refinement;
- advance that refinement in parallel with the next substantive Grid Utility / bill-reconciliation work;
- hand off a new build only when the combined tranche contains enough user-visible functional value to justify one QA cycle, unless a blocking data-integrity/provenance defect requires an earlier stop.

Combined tranche direction:
1. Enel acquisition UX refinement:
   - stage browser-captured PDFs under app-controlled `Data/Tariffs/Enel` rather than generic OS temp;
   - preserve canonical official PDFs locally;
   - reduce per-PDF manual clicks where supported by normal WebView2 session behavior;
   - retain the validated Build-578 `DownloadStarting` route as a fallback;
   - no Imperva/Reese bypass.
2. Grid Utility / billing productization:
   - build on the already-existing utility bill, tariff verification, tariff scenario and audit infrastructure;
   - move from one-off evidence-package capability toward normal in-app bill/reconciliation workflow;
   - preserve the accepted Build-538 statistical/economic semantics rather than recalibrating them.

QA policy for this tranche:
- batch both areas into the same owner QA where practical;
- do not stop merely to validate cosmetic/UX-only progress if the next functional work can continue safely.


## Build 583 — combined Enel acquisition UX + Phase 10 bill reconciliation candidate — 2026-10-07

Owner batching decision has been implemented: this build combines the Enel browser-assisted
UX refinement with substantive bill-reconciliation product work rather than spending a
separate QA cycle on browser UX alone.

Code HEAD:
- `ac29e94fcc43bda80875b3e6c1a617f9c493f9bd`.

Windows Build:
- workflow run: `37566514802`;
- run/build number: **583**;
- Build: **PASS**;
- SQLite smoke: **PASS**;
- portable publish/upload: **PASS**;
- live CNE/Enel HTTP probes intentionally skipped because this tranche does not alter the
  already accepted live CNE correction graph or HTTP-protection classification.

Artifact:
- GitHub artifact name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11459340572`;
- digest:
  `sha256:c92e76d024b93e7a730adef06c261d1e7cdd9d8e45d48f1289620347b47e3392`;
- user-facing handoff filename:
  `SolarEnergyMonitor-Build-583-win-x64.zip`.

### Enel browser-assisted acquisition changes

- app-controlled staging now lives under:
  `Data/Tariffs/Enel/_incoming`;
- canonical PDFs remain under:
  `Data/Tariffs/Enel/<year>/<official filename>.pdf`;
- the validated Build-578 `DownloadStarting` import route remains as fallback;
- WebView2 now also observes `WebResourceResponseReceived`;
- when the integrated session receives an official tariff-PDF URI as a complete HTTP 200
  PDF response, the app writes it to app-controlled incoming storage and imports it
  automatically without requiring the PDF-viewer download click;
- non-PDF/challenge HTML is rejected by PDF magic validation;
- partial/range responses are not promoted by this automatic route; the already-validated
  viewer-download fallback remains available;
- no Imperva/Reese bypass is attempted.

### Phase 10 product-facing bill reconciliation

New service:
`UtilityBillReconciliationSummaryService`.

Normal product semantics deliberately remain separate from the frozen Build-538 provisional
P5/P50/P95 research method:
- uses directly observed inverter grid-import energy;
- reports coverage;
- does not fill telemetry gaps in the normal summary;
- applies the supported official variable tariff model;
- preserves all other actual bill charges/credits;
- computes:
  - actual bill total;
  - billed Enel kWh;
  - observed inverter kWh;
  - Enel minus observed energy difference;
  - supported variable official rate;
  - estimated total using observed inverter energy;
  - actual minus estimated amount;
  - tariff publication periods and SHA provenance.

The Audit tab now shows a visible five-card reconciliation summary above the line-level
verification grid.

Tariff scenario matching was also hardened:
- explicit `category_key` classification now takes precedence over fallback description-text
  matching when both are present.

### CI smoke coverage

A dedicated Phase-10 smoke fixture proves:
- observed energy equals billed energy in the controlled fixture;
- supported official electricity/transport components are recalculated;
- non-variable charges are preserved;
- estimated observed total reconstructs the actual total within tolerance;
- amount difference is zero within tolerance;
- coverage remains high.

### Combined owner QA requested

Use the same copied QA `Data\` used for the Build-578 tariff/browser validation.

A. Bill reconciliation:
1. launch Build 583;
2. open `Red eléctrica -> Auditoría de boleta Enel`;
3. select the accepted 28-08-2026 -> 28-09-2026 bill;
4. return one screenshot containing the new reconciliation summary and the source/detail text.

Expected approximate values from already accepted evidence:
- actual bill: $26,854;
- Enel energy: 97.000 kWh;
- observed inverter: 88.065 kWh;
- estimated according to observed energy: approximately $24,693;
- actual minus observed estimate: approximately +$2,161;
- coverage approximately 99.33%.

These values are expected from the frozen accepted evidence, but QA should report the
actual displayed values verbatim rather than force them.

B. Enel browser-assisted automatic capture:
1. open `Tarifas oficiales -> Capturar Enel en navegador…`;
2. navigate to the same September-2026 official tariff PDF;
3. after the PDF opens, **do not click the PDF viewer download icon initially**;
4. inspect the receipt/status:
   - if it shows
     `CoreWebView2.WebResourceResponseReceived → EnelTariffPdfImportService: COMPLETADO`,
     automatic no-viewer-click capture is PASS;
   - if no automatic receipt appears, click the viewer download icon once and report the
     resulting Build-578-style receipt; that confirms fallback remains intact.

No migration/CNE/380-368/Imperva/bill-report regeneration QA is required.


## Build 583 owner QA findings -> Build 585 corrective candidate — 2026-10-07

Owner returned the combined Build-583 QA.

### Phase 10 reconciliation finding

Build 583 displayed for the accepted 28-08-2026 -> 28-09-2026 bill:
- actual bill: $26,854;
- billed Enel energy: 97.000 kWh;
- observed inverter energy: 80.907 kWh;
- estimated observed total: $22,959;
- actual minus estimated: +$3,895;
- coverage: 99.32%.

This was **not accepted** because the frozen Build-538 observed energy for the same bill
period is 88.065413 kWh.

Root cause:
- the normal product reconciliation used the stored DATE_ONLY end instant directly;
- that interpreted 28-09 as 28-09 00:00 instead of the accepted complete-local-day bill
  semantics [28-08 00:00, 29-09 00:00);
- the frozen bill audit method already uses the correct inclusive printed end date.

Build 585 correction:
- DATE_ONLY product summaries now use the same complete-local-day interval truth as the
  accepted bill-gap analysis for observed energy + coverage;
- the normal summary still exposes observed-only product semantics: no P5/P50/P95 value is
  promoted into the product estimate;
- exact-timestamp bills continue to use exact reconciliation;
- a dedicated smoke regression verifies inclusive DATE_ONLY end semantics.

### Enel browser finding

Build 583:
- opening the official September-2026 PDF did not produce automatic
  `WebResourceResponseReceived -> EnelTariffPdfImportService` completion;
- clicking the PDF viewer download icon did fire `CoreWebView2.DownloadStarting`;
- the owner observed a native Save As dialog whose default location was the Downloads folder.

Build 585 correction/diagnostics:
- automatic response capture now accepts both HTTP 200 and PDF range HTTP 206 responses;
- 206 Content-Range fragments are accumulated and auto-imported only if they form the full
  PDF byte sequence;
- challenge/non-PDF content is never promoted;
- the viewer-download fallback remains;
- compatible DownloadStarting handling now derives the clean official filename from the URI
  first and sets `Handled=true` before assigning app-controlled incoming storage, to suppress
  default download UI as early as WebView2 allows;
- browser duplicate suffixes such as `(1)` are normalized away for the app-controlled file.

### Shared QA Data path — owner decision

Starting with the next owner QA build, all persistent QA state defaults to:

`D:\SolarEnergyMonitorTest\Data`

This is intentionally independent of the executable/build folder.

Resolution precedence:
1. explicit AppPaths data override;
2. environment variable `SOLAR_ENERGY_MONITOR_DATA_DIR`;
3. optional `data-path.txt` beside the executable;
4. QA default `D:\SolarEnergyMonitorTest\Data`.

For default QA layout, database, tariffs, logs and backups all live below the shared Data
directory.

First-run bootstrap:
- if the shared target has no `energy.db`, the app scans sibling
  `SolarEnergyMonitor-Build-XXX-win-x64` folders and copies the highest available prior
  build Data tree into the shared target;
- once the shared database exists, future builds reuse it directly and no per-build Data
  copying is required.

Constructor root overrides used by smoke tests retain the legacy isolated test layout.

### Build 585

Code HEAD:
- `4eafed811851605b32588371c2b4988607d05600`.

Workflow:
- run `37569074353`;
- build number **585**;
- Build PASS;
- SQLite smoke PASS, including DATE_ONLY inclusive-end regression;
- portable PASS;
- artifact ID `11460086995`;
- artifact digest
  `sha256:35f6cbf8bd821d26c3ad414465a570bf3d9a64e4e644d41199cb245f69863e49`.

Owner QA should verify:
1. app opens without copying Data into the Build-585 folder;
2. footer/database path is `D:\SolarEnergyMonitorTest\Data\energy.db`;
3. accepted bill summary returns approximately 88.065 kWh observed and the accepted economic
   neighborhood (~$24.693 observed estimate, ~+$2.161 actual-minus-estimate);
4. browser PDF open attempts automatic 200/206 capture before manual fallback;
5. if fallback click is needed, report whether the native Save As dialog still appears.


## Build 585 owner QA BLOCKED — UI unresponsive regression — 2026-10-07

Owner attempted the Build-585 QA and could not complete it.

Observed on the real target PC:
- the application repeatedly enters Windows “(No responde)” after clicks/interactions;
- this prevents meaningful validation of the shared Data path, corrected bill reconciliation,
  and Enel browser-assisted behavior;
- Build 585 is therefore **NOT ACCEPTED** and its QA is **BLOCKED**.

Do not infer PASS from CI:
- Build 585 CI/SQLite smoke/portable publication passed;
- the target-PC responsiveness regression is material and overrides any assumption that the
  build is suitable for owner QA.

Next cycle must begin with:
1. reproduce/inspect the Build-585 responsiveness regression from source;
2. compare the Build-585 changes against the last responsive owner-tested build;
3. identify whether work is running synchronously on the WPF UI thread or whether the new
   shared-data initialization/path behavior is causing blocking I/O/DB work;
4. in particular inspect the new product bill-summary path, because DATE_ONLY reconciliation
   now invokes bill-gap analysis and may be triggered from UI refresh/selection handlers;
5. also inspect any shared-Data startup/bootstrap work and WebView2 changes for synchronous
   blocking;
6. fix the regression without changing accepted Build-538 statistical/economic truth;
7. produce a new build (586 or later);
8. require Build PASS + SQLite smoke PASS + portable PASS;
9. only then return to owner QA.

Owner instruction for the next cycle:
- review first;
- rebuild only after the responsiveness defect is addressed;
- then test again;
- do not ask the owner to continue testing Build 585.

Persistent QA Data target remains:
`D:\SolarEnergyMonitorTest\Data`

Build-585 source HEAD:
`4eafed811851605b32588371c2b4988607d05600`

Build-585 documentation checkpoint before this blocked-QA record:
`785a7a61ef962955872198493f1d7b329d1e1aa2`


## Build 587 — responsiveness corrective candidate — 2026-10-07

Build 585 remains **NOT ACCEPTED / QA BLOCKED**. Do not resume its QA.

Source diagnosis against responsive Build 583 confirmed the primary regression path:
- `UtilityAuditBillSelector_SelectionChanged` called `RefreshUtilityAuditPreview()` on the WPF Dispatcher;
- the Build-585 DATE_ONLY product summary synchronously called
  `UtilityBillGapStatisticalCompletionService.Analyze(...)`;
- that frozen research analysis loads the device grid-import history and performs statistical
  calibration/completion work that the normal product summary does not consume;
- therefore bill selection/refresh could block the UI thread.

Shared QA Data bootstrap was inspected and is not the repeated-click cause:
- when `D:\\SolarEnergyMonitorTest\\Data\\energy.db` already exists,
  `TryBootstrapSharedQaData` returns before sibling-build enumeration or tree copying;
- no AppPaths behavior was changed in this corrective tranche.

Enel/browser-assisted code was also hardened for responsiveness:
- PDF extraction/normalization import work now runs off the WPF Dispatcher;
- the already validated `DownloadStarting` fallback remains intact;
- no Imperva/Reese bypass was added.

Corrective implementation:
- product DATE_ONLY reconciliation now uses a lightweight observed-only interval path that
  reuses the same inclusive local-day interval truth as the frozen bill-gap method but does
  **not** execute P5/P50/P95 calibration/completion;
- the full frozen statistical method remains unchanged and available for the accepted
  research/evidence workflow;
- product bill-summary analysis runs through `Task.Run` and stale async selection results
  are discarded by a refresh-generation guard;
- manual and browser-assisted Enel PDF import extraction/normalization also run off the UI
  Dispatcher.

Build 586:
- source commit `a0d2c8b9ef0404557c1939eaf77515aaef4fab6e`;
- workflow run `37648337839`;
- failed compilation because the new observed-only helper omitted the existing
  `timeZoneId` argument to `BuildIntervalTruth`;
- no artifact/QA candidate produced.

Build 587:
- source commit `74040567f26371927d072c22aaf614e6779548f1`;
- workflow run `37648734731`;
- run/build number **587**;
- Build: **PASS**;
- SQLite smoke: **PASS**, including the existing DATE_ONLY regression that compares the
  product summary observed kWh/coverage against the frozen full bill-gap analysis;
- live Enel/CNE probes: intentionally skipped by workflow conditions;
- portable publish: **PASS**;
- portable marker: **PASS**;
- artifact upload: **PASS**.

Artifact:
- GitHub name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11496255650`;
- digest:
  `sha256:be4246c9f505cf7df860a1ed93f7839ccbe285650cc7a50f90ce6054404dc2da`;
- user-facing filename:
  `SolarEnergyMonitor-Build-587-win-x64.zip`.

Build 587 is a **QA candidate, not yet accepted**.

Minimum owner QA:
1. launch Build 587 without copying a per-build Data folder;
2. confirm footer points to
   `D:\\SolarEnergyMonitorTest\\Data\\energy.db`;
3. navigate/click between pages and confirm Windows does not enter “(No responde)”;
4. open `Red eléctrica -> Auditoría de boleta` and select
   `28-08-2026 -> 28-09-2026`;
5. report the values actually displayed; expected neighborhood remains approximately:
   - bill: $26,854;
   - Enel: 97.000 kWh;
   - observed inverter: 88.065 kWh;
   - coverage: 99.33%;
   - estimated observed total: $24,693;
   - actual minus estimated: +$2,161;
6. only after responsiveness + bill reconciliation are usable, test the September-2026
   Enel browser-assisted PDF path: first without viewer Download, then one Download fallback
   click only if automatic 200/206 capture does not complete.

Do not reopen Build-538 evidence, P5/P50/P95 calibration, HPVINV02, CNE 380->368,
schema migration, tariff precedence, or Imperva classification.


## Build 587 owner QA findings -> Build 589 combined corrective candidate — 2026-10-07

Owner completed enough Build-587 target-PC QA to separate correctness from performance.

### Build 587 bill reconciliation

Real target-PC result for bill 28-08-2026 -> 28-09-2026:
- actual bill: $26,854;
- billed Enel energy: 97.000 kWh;
- observed inverter energy: 88.065 kWh;
- coverage: 99.33%;
- estimated observed total: $24,690;
- actual minus estimated: +$2,164.

This confirms the DATE_ONLY inclusive-end correction is materially correct and remains in the
accepted economic neighborhood. The product summary did not promote P5/P50/P95.

However Build 587 is **NOT ACCEPTED** for usability:
- reconciliation remained on “Calculando conciliación observada...” for roughly 1-2 minutes;
- the UI no longer entered Windows “No responde”, proving the Dispatcher-blocking regression
  was mitigated;
- but the latency is still unacceptable for normal bill selection.

Root cause of residual latency:
- the observed-only helper still called `LoadAllSamples(deviceId)`, loading/parsing the
  complete `grid_import_power_w` history before filtering to the selected bill month;
- refresh could also trigger duplicate identical summary tasks.

Build 588 optimization:
- commit `f224db809c39033892afbd8bf6b260194ff8dfd7`;
- workflow `37651036708`, run/build **588**;
- Build PASS;
- SQLite smoke PASS including DATE_ONLY regression;
- portable PASS;
- observed-only query is now SQL-bounded to the selected bill interval;
- duplicate identical in-flight summary calculations are deduplicated;
- Build 588 was not handed to owner because Enel Save As defect was diagnosed immediately
  afterward and batched into the next candidate.

### Build 587 Enel browser-assisted finding

Owner evidence:
- opening the September-2026 official PDF still did not complete automatic
  WebResourceResponseReceived capture;
- pressing the integrated PDF-viewer Download action opened a native Windows “Guardar como”
  dialog pointed at the user's Downloads folder;
- after resolving that dialog, the existing `CoreWebView2.DownloadStarting` route completed
  successfully:
  - official filename normalized correctly;
  - existing byte-identical re-import detected;
  - SHA-256 preserved:
    `27b65928d47c34d46da4afa25c094734432be860750dac31e1deddb6a4d3ab17`;
  - publication id 29;
  - 18 pages;
  - 7,786 normalized candidates.

Therefore provenance/import integrity remains PASS; the remaining defect is Save As UX/routing.

Microsoft WebView2 distinguishes Save As UI from DownloadStarting UI. The PDF viewer invokes
Save As before DownloadStarting, so `DownloadStarting.Handled=true` is too late to suppress
that native picker.

Build 589 correction:
- commit `9b5edf9d54d3e6c58faf427bfaa72bbf114db74b`;
- subscribes to `CoreWebView2.SaveAsUIShowing`;
- for the official Enel tariff PDF:
  - sets `SuppressDefaultDialog=true`;
  - sets `SaveAsFilePath` to app-controlled `Data/Tariffs/Enel/_incoming`;
  - uses `CoreWebView2SaveAsKind.Default`;
  - keeps the already validated DownloadStarting import path as downstream fallback;
- no Imperva/Reese bypass.

Build 589 CI:
- workflow `37651835979`;
- run/build **589**;
- Build PASS;
- SQLite smoke PASS;
- portable publish PASS;
- portable marker PASS;
- artifact upload PASS;
- artifact ID `11496569011`;
- digest:
  `sha256:faa6e8ca76bbe015fa5c94b85d18919482fa8d4d3022e9ed6d0407e2c909f147`;
- user-facing filename:
  `SolarEnergyMonitor-Build-589-win-x64.zip`.

Build 589 is the current **QA candidate, not yet accepted**.

Minimum owner QA for Build 589:
1. launch without copying Data; confirm shared database path remains
   `D:\\SolarEnergyMonitorTest\\Data\\energy.db`;
2. open the same bill and observe practical calculation latency; expected values remain the
   Build-587 values/neighborhood above;
3. open the same September-2026 Enel PDF;
4. first observe whether automatic WebResourceResponseReceived capture completes;
5. if not, click Download once;
6. PASS for Save As UX requires **no native Guardar como dialog** and a successful
   SaveAsUIShowing/DownloadStarting/import receipt using app-controlled storage.

Do not repeat Build-538 evidence, statistical recalibration, CNE, migration, precedence,
Imperva classification, or SHA/provenance proof unless a new defect appears.


## Build 612 — consolidated Phase-10 bill-ingestion/audit UX candidate — 2026-10-07

Owner explicitly requested **batched QA rather than repeated mini-QA cycles** and asked that this
tranche be the first deliberate UI/UX redesign of the bill workflow.

Build 589 is therefore superseded as a QA handoff. Its fixes remain included:
- DATE_ONLY observed reconciliation query bounded to the selected bill interval;
- duplicate in-flight summary work deduplicated;
- WebView2 `SaveAsUIShowing` interception for Enel PDF-viewer Save As;
- app-controlled Enel incoming storage;
- validated `DownloadStarting` import fallback retained;
- no Imperva/Reese bypass.

### Bill ingestion v2 / schema v15

The bill path was rebuilt around one canonical bill model with **two first-class entry routes**:

1. PDF-assisted entry/review;
2. manual entry.

Neither input route defines a weaker/different bill shape.

Schema v15 adds:
- `utility_bill_document`: preserved original PDF, local path, SHA-256, size, pages,
  parser version, extracted text and import time;
- `utility_bill_field_evidence`: per-field source/evidence state plus printed/normalized
  representation and optional source-page/text;
- bill-level source kind, source-document link, review state and IVA rate;
- bill-line source kind, evidence state, source page and source text.

Evidence semantics now distinguish:
- MANUAL / user-entered;
- PDF reviewed / PDF-extracted confirmed;
- PDF review required;
- derived;
- not printed;
- not provided;
- legacy unreviewed.

Legacy bills remain visible and are not silently promoted to reviewed.

### PDF-assisted bill ingestion

New `EnelUtilityBillPdfImportService`:
- validates PDF magic;
- preserves original PDF under app-controlled bill storage;
- SHA-256 provenance;
- text extraction without OCR;
- conservative high-confidence prefill only;
- review conflicts do not silently overwrite existing saved values;
- PDF and manual review can update the **same bill record** rather than duplicating it;
- canonical line categories include:
  - electricity consumed;
  - electricity transport;
  - fixed monthly;
  - subsidy/credit;
  - service administration;
  - meter rental;
  - common service;
  - IVA 19%;
  - simple/rounding adjustment;
- printed bill-line descriptions are preserved where extractable instead of replacing them
  with generic normalized names;
- amount sign detection uses the printed amount or explicit subsidy/discount/credit wording,
  not arbitrary hyphens;
- applying reviewed PDF lines saves them as confirmed evidence after the user commits the
  review.

A saved PDF-backed bill can now open its preserved original PDF directly from the UI.
A manual/legacy bill can later be reviewed and linked to a PDF without creating a new bill.

### Manual entry

Manual entry remains a first-class workflow:
- explicit `Nueva boleta manual` action;
- uses the same canonical fields and lines as PDF-assisted entry;
- optional blank canonical fields are recorded as `NOT_PROVIDED` rather than silently lacking
  provenance;
- save/cancel resets the editor including dates/times/precision to prevent accidental reuse
  of a prior bill period;
- after save, the bill remains selected so charge-line entry/review can continue immediately;
- deleting a bill/line being edited clears the corresponding editor state.

### IVA and end-of-bill adjustments

Phase-10 product audit uses Chile standard IVA = **19%** when there is a sufficient taxable
base.

Simple adjustment policy:
- a printed adjustment/rounding line is preserved explicitly as bill evidence;
- the application never creates a fake adjustment merely to force a total to balance;
- an unexplained residual of up to CLP 10 is classified as a small residual for review;
- larger unexplained differences remain material.

New deterministic smoke cases prove:
1. taxable base 10,000 + IVA 1,900 + printed adjustment 2 = total 11,902 => BALANCED;
2. same bill without the printed adjustment line => CLP 2
   `SMALL_UNEXPLAINED_RESIDUAL`, with no synthetic adjustment.

### Phase-10 line-by-line audit v2

`UtilityBillAuditV2Service` now separates:
- actual printed amount;
- printed rate when present;
- calculation quantity/basis;
- official tariff reconstruction when supported;
- amount difference;
- actual-only/unmapped evidence;
- IVA 19% reconstruction;
- explicit printed adjustments;
- reconstruction coverage;
- bill balance/residual status.

The official tariff bridge retains:
- authoritative tariff-version precedence;
- tariff changes within the bill interval;
- local-day weighting across multiple effective tariff periods;
- electricity + transport/public-service reconstruction where the captured official evidence
  supports it;
- fixed/other known component rate verification when printed quantity/rate provide sufficient
  evidence;
- no invented customer applicability.

Accepted Build-538 statistical/economic research remains frozen; no P5/P50/P95 recalibration
or HPVINV02 reopening occurred.

### UI/UX redesign

The Enel-bill tab was reorganized from one large technical `WrapPanel` into a guided workspace:

1. **Start from PDF / first-class manual entry**;
2. **Period, consumption and identity**;
3. **Printed totals and taxes**;
4. **PDF evidence review** when present;
5. **Saved bills** with human-readable source/review labels;
6. **Charges, credits and adjustments** editor.

Additional UX changes:
- clear hierarchy and helper copy;
- PDF/manual paths visually distinct but converge to the same editor;
- raw internal state codes are replaced in product UI with labels such as
  `PDF + revisión`, `Ingresado manualmente`, `PDF confirmado · p.N`,
  `Legacy · revisar`;
- original PDF can be reopened from the saved-bill area;
- audit table now includes **actual amount** alongside reconstructed amount/difference;
- audit top-level cards explicitly surface:
  - monetary reconstruction coverage;
  - IVA 19% check;
  - balance/adjustment status;
- Spanish and English resource dictionaries remain key-aligned with no duplicate keys;
- static check confirms all 46 bill/audit named controls referenced by code exist exactly once
  in XAML.

### Build 612

Source commit:
- `a263b5a0e403723461c07348e852613bd82cec17`.

Workflow:
- run `37675306167`;
- run/build number **612**;
- Build: **PASS**;
- SQLite smoke: **PASS**, including:
  - DATE_ONLY inclusive-end regression;
  - schema v15 provenance/review;
  - tariff-model bridge;
  - IVA 19%;
  - printed simple adjustment;
  - explicit-adjustment balanced case;
  - missing-adjustment small-residual case;
- live Enel/CNE probes: skipped by workflow conditions, not failures;
- portable publish: **PASS**;
- portable marker: **PASS**;
- artifact upload: **PASS**.

Artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11507002349`;
- digest:
  `sha256:29e6b993c6966ecb88ef4abea1cb24f02abfb4057ecf81aadce45a55334ff6f4`;
- user-facing filename:
  `SolarEnergyMonitor-Build-612-win-x64.zip`.

Build 612 is the current **consolidated QA candidate, not yet accepted**.

### Consolidated owner QA scope

Use the shared data directory; do not copy per-build Data.

The QA should intentionally cover several pieces in one pass:

1. launch/overall responsiveness and shared DB path;
2. review the redesigned `Boletas Enel` UX;
3. exercise manual entry workflow (no need to create junk data solely for cosmetic testing;
   owner may review/update the existing bill);
4. use the real September-2026 bill PDF when available to review the existing canonical bill
   in place and compare extracted vs saved facts;
5. confirm the canonical real bill still represents:
   - printed period 28-08-2026 -> 28-09-2026;
   - 97.000 kWh;
   - total CLP 26,854;
   - existing bill-line wording/amounts corrected against the PDF rather than assumed;
6. confirm bill reconciliation is practically fast and retains approximately:
   - observed inverter 88.065 kWh;
   - coverage ~99.33%;
   - accepted observed-estimate neighborhood unless richer reviewed bill evidence legitimately
     changes a product-facing reconstruction, in which case record the actual basis/result;
7. inspect line-by-line audit:
   - actual amount;
   - reconstructed amount where supported;
   - difference;
   - source/evidence;
   - monetary reconstruction coverage;
   - IVA card;
   - balance/adjustment card;
8. Enel browser-assisted tariff PDF:
   - observe automatic capture first;
   - if not, press Download once;
   - Save-As fallback PASS requires no native folder picker and app-controlled import.

Do not regenerate Build-538 evidence or reopen frozen CNE/380-368/migration/precedence/Imperva
work unless a genuinely new defect appears.

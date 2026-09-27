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

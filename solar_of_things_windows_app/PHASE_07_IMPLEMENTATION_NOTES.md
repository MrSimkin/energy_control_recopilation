# Phase 7 — Reporting — Implementation Notes

Date started: 2026-09-26

Status: **IN PROGRESS — FAMILY REPORT REDESIGN IMPLEMENTED; TARGET-PC READABILITY QA PENDING**

## Scope

This phase implements Product Functional Specification Milestone F without creating a second calculation path.

Canonical rule:
- reports consume the same validated `EnergyRangeStatisticsService` and `EnergyAggregationTableService` used by History & Charts;
- missing data remains missing;
- 0%-coverage periods are not exported as measured zero;
- long telemetry gaps are not extrapolated.

## First checkpoint

Implemented:
- real Reports page in the desktop UI;
- quick report periods plus explicit From/To dates;
- day/week/month/year aggregation selection;
- named report presets persisted locally;
- relative presets preserve their relative definition when reopened;
- custom presets preserve explicit dates;
- Excel `.xlsx` export with:
  - Summary sheet;
  - full Detail sheet;
  - Quality sheet;
- printable PDF export with:
  - title and selected period;
  - physical energy summary;
  - per-metric coverage;
  - explicit missing-is-not-zero warning;
  - compact detail table;
  - page numbering;
- report exports use the same gap-aware statistics as the application UI.

Dependencies:
- ClosedXML 0.105.1;
- PDFsharp-MigraDoc 6.2.4.

Both are stable packages selected for the current .NET 10 codebase; no prerelease reporting dependency is used.

## Automated validation

The existing Windows smoke test now validates:
- report-preset persistence;
- generation of a non-empty XLSX;
- generation of a non-empty PDF on Windows.

## Remaining before Reporting can be declared complete

- add the specification's built-in named household report templates/variants beyond the generic configurable report;
- add simple charts to exported reports where they materially improve readability;
- validate one combined real-PC reporting session (preset save/reopen + Excel open + PDF open/printability);
- assess whether any additional glossary/context sections are needed for family-facing reports.

Do not ask the user to micro-test each exporter change. The next manual reporting test is one combined checkpoint after CI is green.


## Second reporting checkpoint — built-in family reports

Added after the first green export checkpoint:
- built-in selectable report types:
  - Simple Energy Summary;
  - Detailed Energy Report;
  - Battery Report;
- report type is persisted as part of named presets;
- Spanish/English export labels follow the active application language;
- printable energy chart using only buckets where solar/home/grid all have measurements;
- printable battery SOC chart using only measured SOC buckets;
- readable glossary in PDF and Excel;
- simple report explicitly marks unsupported flow-attribution percentage and utility comparison as unavailable instead of inventing them;
- detailed PDF includes the audit table;
- battery PDF focuses on SOC and battery delivered energy.

Grid/Utility Reconciliation and Financial/Bill reports remain intentionally deferred until their required source subsystems (utility meter observations and tariff/billing configuration) exist. They must not be fabricated from inverter data alone.

The next manual checkpoint remains one combined reporting validation, not separate tests for each report type.


## Final automated state for this reporting pass — 2026-09-26

The second checkpoint initially hit a compile-only MigraDoc API mismatch in commit:
- `12742e20e2ebde4884c23fc830297297e2dccb96`

The mismatch was narrowly repaired in:
- `78ebc4ddbd66ba6b40b57de7ba7d8ebc8d6da9ef`
- message: `Fix MigraDoc report legend API`

Final Windows Build:
- run ID: `36266851482` (run 275);
- restore: PASS;
- build: PASS;
- SQLite/reporting smoke: PASS;
- XLSX generation smoke: PASS;
- PDF generation smoke: PASS;
- self-contained win-x64 publish: PASS;
- artifact upload: PASS.

Final portable artifact for the combined reporting QA:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10913787724`;
- size: 78,595,326 bytes;
- SHA-256: `8860e64b6f962bc268e64b1ce2682bdf84bfe4722cfc2495d99220105bf80202`;
- expires: 2026-12-25.

## Single combined real-PC checkpoint still required

Do not re-run the old synchronization/backfill validation.

Use the final portable above and validate in one session:
1. confirm the five Phase 6 corrective findings in normal use:
   - current inverter thresholds vs fallback/drift wording;
   - explicit current-state refresh on Home;
   - missing periods shown as gaps/blank rather than measured zero;
   - mouse wheel scrolls the page and Ctrl+wheel zooms charts;
   - remembered-session/auto-connect wording and behavior are understandable;
2. open Reports and save one named relative preset;
3. close/reopen the application and confirm the preset is still present and resolves sensibly against the current saved-history frontier;
4. export one Simple Energy report to Excel and open it;
5. export one Detailed or Battery report to PDF and open it;
6. confirm the PDF is readable/printable, includes charts/glossary/period/quality note, and does not fabricate missing periods;
7. report only anomalies. If no anomaly is found, Phase 7 can be closed for the currently available subsystems.

Grid/Utility Reconciliation and Financial/Bill reports are not blockers for Phase 7 at this point because their required source subsystems do not yet exist. They remain future work tied to utility-meter/tariff functionality.


## Re-entry reference

The canonical next-session state, download identifiers and combined QA steps are mirrored in `CONTINUITY_STATUS.md` under **Explicit re-entry checkpoint — 2026-09-26**.

Use Windows Build run `36266851482` / artifact `10913787724`. Do not substitute the earlier Phase 7 artifact `10914352694`.


### Portable migration note for combined QA

Reuse the entire prior portable `Data\` directory, including `energy.db` and `Secrets\`, on the same Windows user. Copying only the database would invalidate remembered-session/autologon validation because the DPAPI-protected session files live under `Data\Secrets\`.


### Combined QA update — current refresh PASS + responsive-layout finding

The target-PC current-state test passed with remembered credentials and no password re-entry. Home refreshed to a recent 2026-09-26 17:17 snapshot and showed connected state.

A new non-blocking UX finding was observed: Home's right-side status panel clips long text and controls at normal non-maximized desktop widths. Record this for the next UI correction tranche; do not interrupt the current combined reporting QA.


### Combined QA update — battery thresholds PASS

Observed inverter thresholds 20/10/50 were shown as current and matching family policy. This closes the battery-threshold corrective finding.

A new non-blocking cross-page freshness issue was observed: Home showed fresh SOC 36% at 2026-09-26 17:17 after current refresh, while Battery still showed stored SOC 31% from 2026-09-25 17:45. Record for the next coherence/UX tranche; continue current QA.


### Combined QA update — missing-data chart semantics PASS

History & Charts on the real PC visibly preserved gaps for unmeasured periods and did not draw them as zero or bridge them. Coverage warning remained visible. Only the wheel/Ctrl+wheel interaction check remains for the chart UX finding.


### Combined QA update — chart wheel part 1 PASS

Normal mouse wheel over the chart scrolls the page instead of zooming. Ctrl+wheel zoom remains to be checked.


### Combined QA update — Ctrl+wheel interaction FAIL

Video-confirmed on both charts: Ctrl+wheel zooms the chart but also scrolls the page. Normal wheel scrolling remains correct. Record as a non-blocking UI bug for the next correction tranche; continue the combined QA.


### Combined QA update — Settings/session UX clarity PASS

Settings clearly communicates verified connection state, optional startup auto-connect, sign-out/forget, and password privacy. Only the functional restart test with auto-connect enabled remains.


### Combined QA update — startup auto-connect PASS

Restart with startup auto-connect enabled restored and verified the remembered session and refreshed Home to a recent 2026-09-26 18:02 snapshot without starting historical backfill.

New non-blocking finding: Home showed "Última descarga de datos: Nunca" after restart even though the persisted historical frontier is 2026-07-06. Record for the next persistence/UX correction tranche; continue Reporting QA.


### Combined QA update — Reports initial screen PASS

Reports loaded with all expected controls and export actions. Minor localization finding: Spanish selection summary exposes internal aggregation value `Day` instead of `Día`. Continue with preset persistence test.


### Combined QA update — preset save PASS

A named relative "last 7 days" Reports preset was saved successfully. Restart persistence and relative-range re-resolution remain to be checked.


### Combined QA update — preset restart persistence PASS

The saved "Últimos 7 días" preset remained available after full application restart. Relative-range re-resolution is the remaining preset check.


### Combined QA update — relative preset semantics PASS

The saved "Últimos 7 días" preset reloaded as a relative quick period and resolved to 2026-09-19 through 2026-09-25 against the current saved-history endpoint. Preset persistence and relative semantics are PASS.


## Real-PC XLSX QA — mechanical PASS, family-facing FAIL — 2026-09-26

The combined QA progressed through:
- Reports screen: PASS;
- named relative preset save: PASS;
- restart persistence: PASS;
- relative re-resolution against saved-history endpoint: PASS;
- Simple Energy XLSX generation/open in Excel: mechanical PASS.

Observed workbook properties:
- opened normally in Excel;
- sheets included `Resumen`, `Detalle`, `Calidad`, `Glosario`;
- requested 2026-09-19 through 2026-09-25 period was represented;
- daily detail existed;
- missing days were not fabricated as measured zero;
- coverage information was exported.

The target family review nevertheless declared the Simple Energy report **functionally unsuitable** as a handoff to the user's parents.

This is not an exporter-engine failure. It is a report hierarchy/interpretation failure.

The family priority questions, in order, are:
1. how much household energy came from Enel/grid;
2. how much household energy came directly from the panels;
3. how much came from the battery;
4. how often/how long the battery did not cover the night before grid was needed.

Secondary conversation questions include:
- total PV generation;
- total house consumption regardless of source;
- energy that could not be used, but only if measurable without fabrication.

A required Page 3 must detect **patterns and events over any selected period**, including all repeated event occurrences, robust time-of-day tendencies, night behavior and evolution across longer ranges.

Canonical specification:
- `REPORTING_FAMILY_DESIGN_2026-09-26.md`.

Implementation must preserve the already-proven export/preset/missing-data infrastructure while replacing the family-facing report model/presentation.

Do not continue the old “open one PDF and close Phase 7” checkpoint. Phase 7 now requires the family-report redesign and a later focused real-PC readability validation.


## Family-report implementation checkpoint — 2026-09-26

Development resumed from canonical design commit `dc5dc1e5fbf71e4bf7f4707c063ba135634b89c6`.

Implemented in this checkpoint:
- dedicated `FamilyReportAnalysisService` over the existing normalized corpus;
- observed/fallback battery threshold context reused for reserve-event detection;
- complete reserve+grid episode occurrence list;
- night observability calculation so missing nights do not become false negatives;
- robust three-hour time-of-day patterns built from per-day/per-hour averages and cross-day medians;
- adaptive evolution granularity (day/week/month according to selected-period length);
- highlighted maximum solar/home/grid days only when coverage is sufficient;
- Simple Energy XLSX redesigned into:
  - `Resumen` family Page 1/2;
  - `Patrones`;
  - `Eventos`;
  - existing `Detalle`, `Calidad`, `Glosario` annex sheets;
- Simple Energy PDF redesigned into family Page 1, Page 2, Page 3 plus quality/glossary;
- direct PV→house and battery→house remain explicitly unavailable rather than fabricated;
- “unused solar” remains explicitly unavailable rather than calculated as a residual;
- user-facing aggregation/report-kind enum leakage removed from the redesigned summary;
- family-facing and detail numeric precision formatted for readability.

The existing physical integration/statistics path is retained. No second energy-calculation path was introduced.

This checkpoint still requires Windows CI/build/smoke validation before target-PC QA.


### Build-only repair after family-report checkpoint

Windows Build run 276 reached compilation and failed on one C# custom-format escape in the PDF typical-reserve-time string. No semantic/reporting logic executed yet. The format expression was replaced with an explicit `TimeOnly.ToString("HH:mm")` call; no functional behavior changed.


### Family-event semantic refinement

Before target-PC QA, reserve+grid episodes were refined so that:
- entry still requires battery SOC at/below the validated normal transfer threshold;
- once the episode has started, duration continues while the house is still using material grid power and PV remains absent/insufficient, even if SOC starts recovering above the threshold;
- the episode ends when grid/PV shortfall evidence ends, night ends, or continuity is broken by a data gap.

Hourly pattern cells now also require at least roughly 50% of the expected samples for that local hour (derived from observed median cadence) before that day/hour can contribute to a “typical” pattern.


### Stronger automated family-analysis smoke

Added a deterministic four-day normalized corpus to the Windows smoke test. It contains three complete nights inside the selected period where:
- SOC reaches the 20% normal-transfer threshold at 04:00;
- grid supplies the shortfall until solar becomes sufficient at 07:00;
- SOC begins recovering above 20% before the episode ends.

The smoke now requires:
- exactly 3 complete observable nights;
- exactly 3 retained reserve+grid episodes;
- all 3 nights marked with a qualifying episode;
- episode duration remains >150 minutes despite SOC recovery;
- typical reserve time = 04:00;
- robust house/solar/grid time-of-day patterns are produced.

Partial first/last nights are excluded from family event denominators and event counts.


### Build-only repair after stronger smoke checkpoint

Windows Build run 279 failed before smoke because the filtered event collection is now an array and one constructor argument still used `events.Count` as though it were a list property. Replaced with `events.Length`; no analysis semantics changed.


## Family-report redesigned checkpoint — CI GREEN — 2026-09-26

Final code HEAD for this checkpoint:
- `e02df70812af3a02f7800e4637e38d877f460ac9`
- message: `Fix filtered family event count`
- substantive implementation commits immediately before it:
  - `de4fa94fab484e7184d308d9652e2fb5b5473145` — family report patterns/events + XLSX/PDF redesign;
  - `7bd548290f0fb062f8c51538adad652ae0880654` — build-only time-format repair;
  - `a15cada160b1ba991f40d94f86d7049aec29beb1` — refined event duration + hourly evidence;
  - `bd722f84326e4ae5dad3c7885168bc505e6b6549` — stronger deterministic smoke;
  - `e02df70812af3a02f7800e4637e38d877f460ac9` — build-only filtered-array count repair.

Windows Build:
- run ID: `36277693543`;
- run number: 280;
- restore: PASS;
- build: PASS;
- deterministic family event/pattern smoke: PASS;
- Simple Energy XLSX generation: PASS;
- Simple Energy PDF generation: PASS;
- self-contained win-x64 publish: PASS;
- portable marker: PASS;
- artifact upload: PASS.

Smoke explicitly reports:
`Phase 7 report presets/family event-pattern analysis/Excel/PDF export are operational.`

Portable target-PC QA artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10917741876`;
- size: 78,620,828 bytes;
- SHA-256: `86c12cf99cb3b0c56879861c6bcd2baab64bc3f2053da4e06b67d62b9f8e6eab`;
- expires: 2026-12-25;
- Actions run: `https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36277693543`.

The pre-existing NU1701 warning for `SkiaSharp.Views.WPF 3.119.0` remains non-blocking and was not introduced by Reporting.

### Next target-PC validation

Do not repeat the broad Phase 6 combined QA.

Use the new portable with the proven entire prior `Data\` directory on the same Windows user, then validate only the redesigned Reporting behavior:

1. open Reports;
2. use the already saved relative preset if present;
3. export **Simple Energy** to XLSX;
4. inspect `Resumen`, `Patrones`, `Eventos` plus the retained technical annex;
5. export **Simple Energy** to PDF and inspect the Page 1 / Page 2 / Page 3 hierarchy;
6. report readability/semantic anomalies, especially:
   - whether the first page answers the family questions rapidly;
   - whether observable-night/event counts make sense on the real corpus;
   - whether patterns look plausible and are not overconfident;
   - whether all repeated reserve+grid events are present;
   - whether low/missing coverage is expressed clearly.

Direct PV→house, battery→house, percent-without-grid and curtailed-solar kWh remain deliberately unavailable until their evidence path is validated. That is expected, not a QA failure.


## Real workbook review — visual hierarchy / partial-data correction — 2026-09-26

The first redesigned XLSX was opened on the target PC and the actual workbook was reviewed.

Mechanical content was present, but the family `Resumen` still looked like a technical table rather than the approved wireframe. More importantly, a 38.9% coverage report displayed 27.94 kWh grid import and 48.60 kWh home use without making “partial period” prominent enough.

Corrective implementation:
- Simple Energy `Resumen` rebuilt as a visual nine-column family cover;
- prominent PARTIAL SUMMARY banner when minimum family coverage is <80%;
- three large Page-1 source cards:
  - Enel/grid measured value;
  - direct solar visibly reserved but evidence-gated;
  - battery→house visibly reserved but evidence-gated;
- large recorded/total home-consumption band;
- night block split into:
  - observable nights without reserve+grid episode;
  - nights where reserve+grid episode occurred;
  - complete nights without sufficient data;
- natural-language “¿Qué significa esto?” interpretation;
- Page 2 labels change from “total” to “registrado” when coverage is partial;
- Page 2 keeps battery movement distinct from household-source attribution;
- pattern detector now requires usable day coverage on at least 60% of selected days (minimum 3) before saying “habitual”;
- if this evidence threshold is not met, `Patrones` and PDF explicitly say there are not enough observable days instead of producing an overconfident pattern;
- pattern wording reports days with data against total selected-period days.

The 2026-09-19→2026-09-25 real workbook that triggered this correction had:
- minimum family coverage 38.9%;
- only 3 days with meaningful energy observations out of 7;
- 2 observable complete nights out of 6;
- therefore the old labels “total” and “habitual” were too strong.


### Build-only repair after visual family-cover checkpoint

Windows Build run 281 failed before smoke because `spanDays` was declared twice after moving selected-period length earlier for pattern-evidence gating. Removed the duplicate declaration; no reporting semantics changed.


## Family-cover visual correction — CI GREEN — 2026-09-27

Green code HEAD:
- `04e64f3580b9571f5526322c8e580710f4f359d4`.

Windows Build:
- run ID: `36281469442`;
- run number: 282;
- restore: PASS;
- build: PASS;
- deterministic family event/pattern smoke: PASS;
- XLSX generation: PASS;
- PDF generation: PASS;
- self-contained win-x64 publish: PASS;
- artifact upload: PASS.

Portable:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `10919445323`;
- size: 78,625,154 bytes;
- SHA-256: `829858fe447acb1124ff1db5e13c46ff831c9eaf14da448a0603db48cb3ee75a`;
- expires: 2026-12-26;
- run: `https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36281469442`.

This build supersedes artifact `10917741876` for family-report QA.

Target-PC validation should re-export the same saved `Últimos 7 días` Simple Energy preset and inspect `Resumen` first. Expected changes:
- visible partial-summary banner for the 38.9% real corpus;
- three large source cards;
- large home-consumption band;
- explicit observed / shortfall / unknown night cards;
- plain-language interpretation;
- “registrado” rather than “total” wording under partial coverage;
- no “habitual” hourly patterns when only 3 of 7 selected days contain usable observations.


## Target-PC family Summary review — visual hierarchy substantially aligned; one Excel layout defect — 2026-09-27

The corrected target-PC workbook was reviewed directly.

Positive result:
- prominent 38.9% PARTIAL SUMMARY banner is present;
- three family source cards are present;
- Enel/grid value is immediately visible;
- unavailable direct-solar and battery→house answers remain visibly reserved rather than substituted;
- recorded home consumption is prominently separated;
- night section clearly shows:
  - 2 observable nights without reserve+grid episode;
  - 0 nights/episodes where the battery ran short under the current event rule;
  - 4 complete nights without sufficient observations;
- natural-language interpretation is present;
- Page 2 uses “registrada/registrado” instead of pretending 38.9% coverage represents full-period totals.

Remaining mechanical defect observed:
- the final Page 2 “Solar que no pudimos aprovechar” block and following explanatory note visually overlap/cut off in Excel because wrapped text sits inside merged rows whose height is not automatically expanded.

Repair:
- explicit row heights added to both merged narrative blocks;
- explicit vertical centering added;
- no data/reporting semantics changed.

Page 1 is now materially aligned with the approved wireframe, but final family-readability acceptance remains with the target user after the repaired build is viewed.


## Merged-row readability repair — CI GREEN — 2026-09-27

Green code HEAD:
- `3c21743bd59bfbb87fdf0506470fb591db1b9f0c`.

Windows Build run 283 / `36281821463`:
- restore PASS;
- build PASS;
- deterministic Reporting smoke PASS;
- XLSX/PDF generation PASS;
- portable publish/upload PASS.

Portable:
- artifact `SolarEnergyMonitor-win-x64-dev`;
- ID `10919181734`;
- size 78,625,291 bytes;
- SHA256 `1823018f6ec5aaabde9b7f24ae3fc3efab4822559bcfda5db68ed03abd9ffd44`;
- expires 2026-12-26;
- run `https://github.com/MrSimkin/energy_control_recopilation/actions/runs/36281821463`.

This artifact supersedes `10919445323` only to repair the Excel merged-row overlap at the bottom of `Resumen`. No report semantics changed.

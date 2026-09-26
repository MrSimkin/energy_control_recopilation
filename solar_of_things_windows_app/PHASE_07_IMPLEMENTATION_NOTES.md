# Phase 7 — Reporting — Implementation Notes

Date started: 2026-09-26

Status: **IN PROGRESS — CI GREEN, COMBINED REAL-PC REPORTING QA PENDING**

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

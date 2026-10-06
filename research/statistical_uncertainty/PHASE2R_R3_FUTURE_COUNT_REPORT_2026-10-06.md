# Phase 2R — R3 future-validation count checkpoint

Date: 2026-10-06

Status: **INSUFFICIENT — STOPPING RULE NOT MET; NO OUTCOME SCORING PERFORMED**

Candidate:
`grid-import-boundary-residual-dayweighted-240m.candidate-v4`

Frozen harness:
`research/statistical_uncertainty/phase2r_r3_future_validation.py`

Harness version:
`phase2r-r3-future-validation.v1`

## Input package

File:
`SolarOfThings-ResearchPackage-20261006-154413.zip`

SHA-256:
`3a7bc7edb9e264333cd6da73b85f9f2d805b6eb4f5ef20594ddc34ffdbdd6295`

Exporter metadata:
- package version: `solar-of-things-research-package.v1`;
- exporter version: `research-exporter.v1`;
- generated UTC: `2026-10-06T18:44:13.2558437+00:00`;
- timezone: `America/Santiago`;
- telemetry range declared by manifest: `2026-04-20T20:55:36.1860000+00:00` through `2026-10-06T18:41:34.6800000+00:00`.

Observed valid finite non-UNRESOLVED `grid_import_power_w` range:
- first: 2026-04-20 20:55:36.186 UTC;
- last: 2026-10-06 18:41:34.680 UTC;
- local-date range: 2026-04-20 through 2026-10-06;
- valid grid rows: 46,133;
- represented local dates: 170.

This package is cumulative, not merely a delta. It already contains the historical calibration span plus new future telemetry.

## Package integrity

PASS.

Validated:
- ZIP SHA-256 computed;
- `manifest.json` readable;
- every file declared by the manifest exists;
- every declared byte length matches;
- every declared SHA-256 matches.

Declared files verified:
- `README.txt`;
- `historical_soc_config.csv`;
- `history_day_status.csv`;
- `mode_context.csv`;
- `normalized_metrics.csv`;
- `research_settings.json`.

No Enel bill, meter reading, tariff or external utility value was loaded.

## Outcome-blind count only

The frozen count-stage logic was applied only to:
- timestamps;
- valid observed boundaries;
- start state;
- duration group;
- prior calibration-date availability.

The following were **not** calculated or inspected:
- target truth energy;
- R3 P5/P50/P95;
- coverage;
- P50 error/bias;
- interval score;
- comparison outcome versus C2;
- any Enel comparison.

## Future-date source-sample counts

R3 future validation starts on 2026-09-28.

Valid grid samples by future local date in this package:

| Local date | Grid samples | >=280 day-quality threshold |
|---|---:|---|
| 2026-09-28 | 294 | YES |
| 2026-09-29 | 240 | NO |
| 2026-09-30 | 132 | NO |
| 2026-10-01 | 290 | YES |
| 2026-10-02 | 293 | YES |
| 2026-10-03 | 291 | YES |
| 2026-10-04 | 294 | YES |
| 2026-10-05 | 292 | YES |
| 2026-10-06 | 191 | NO / partial day at export time |

Therefore six future local dates currently satisfy the >=280 sample construction threshold:
- 2026-09-28;
- 2026-10-01;
- 2026-10-02;
- 2026-10-03;
- 2026-10-04;
- 2026-10-05.

## Constructed future targets

Total structural future single-gap targets:
**178**

Calibration-date eligibility:
- OK: **178**;
- insufficient calibration: **0**;
- every constructed target has the frozen maximum 15 selected prior calibration dates.

By state:
- ACTIVE: **36**;
- INACTIVE: **142**.

ACTIVE targets by duration group:
- `G20_30`: **15**;
- `G60_120`: **19**;
- `G240`: **2**.

ACTIVE target dates:
- 2026-09-28: 7;
- 2026-10-01: 15;
- 2026-10-02: 6;
- 2026-10-03: 0;
- 2026-10-04: 0;
- 2026-10-05: 8.

Therefore only **4 distinct future dates** currently contain at least one eligible ACTIVE-start target.

## Frozen stopping rule

Required simultaneously:

1. >=15 distinct future local dates with at least one eligible ACTIVE-start target;
2. >=100 ACTIVE-start single-gap cases;
3. >=10 ACTIVE-start cases in each:
   - `G20_30`;
   - `G60_120`;
   - `G240`.

Current:

- ACTIVE eligible dates: **4 / 15**;
- ACTIVE eligible cases: **36 / 100**;
- `G20_30`: **15 / 10** — count threshold reached;
- `G60_120`: **19 / 10** — count threshold reached;
- `G240`: **2 / 10** — still below threshold.

Stopping-rule result:

`INSUFFICIENT`

`stopping_rule_met = false`

No stopping date exists yet.

## Interpretation

The current limiting evidence is not calibration history: all 178 constructed targets already have sufficient prior calibration support.

The stopping rule remains blocked by:
- too few distinct future ACTIVE dates;
- too few total ACTIVE cases;
- especially too few ACTIVE `G240` cases.

Dates 2026-09-29 and 2026-09-30 are future-validation dates but currently fail the >=280 grid-sample day-quality threshold. They may become usable only if later legitimate telemetry/backfill increases their source evidence; they must not be synthetically repaired.

2026-10-06 is incomplete at this export timestamp and therefore is not currently a qualifying full day.

No scoring is authorized from this checkpoint.

Next operation:
- continue accumulating/exporting genuine telemetry;
- on a later cumulative Research Exporter package, rerun `count` only;
- score exactly once only after the frozen stopping rule returns READY.

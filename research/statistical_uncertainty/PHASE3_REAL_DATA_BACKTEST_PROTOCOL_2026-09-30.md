# Phase 3 — Real-data backtesting protocol

Date: 2026-09-30

Status: **FROZEN BEFORE REAL-DATA C0/C2 SCORING**

Candidate under test:
- `grid-import-context-block-bootstrap-120m.candidate-v1`;
- research alias: C2-120.

Baseline:
- production `grid-import-empirical-bootstrap.v1`;
- research alias: C0.

Enel/utility values are excluded from construction, selection and scoring.

## 1. Research package integrity

Input:
- `SolarOfThings-ResearchPackage-20260930-214934.zip`;
- package SHA-256:
  `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`;
- package size: 4,067,226 bytes;
- package version: `solar-of-things-research-package.v1`;
- exporter: `research-exporter.v1`;
- source schema: 13;
- timezone: `America/Santiago`.

Manifest validation:
- all declared files present;
- all declared byte lengths match;
- all declared per-file SHA-256 hashes match.

Telemetry:
- range: 2026-04-20 through 2026-09-27;
- five normalized metrics: 218,705 rows;
- `grid_import_power_w`: 43,741 rows;
- grid metric confidence/quality: all exported rows CONFIRMED / OFFICIAL_ENERGY_FLOW_GRID_IMPORT.

## 2. Pre-score telemetry profile

Grid cadence:
- median observed gap: 5.0043 min;
- 99.5th percentile gap: 5.3207 min;
- 99.10% of consecutive gaps fall between 4.5 and 5.5 minutes;
- 28 observed separations exceed 15 minutes;
- 20 exceed 20 minutes;
- 12 exceed 120 minutes;
- 10 exceed 240 minutes.

Daily evidence:
- 161 local dates represented;
- 140 dates contain at least 280 grid-import samples.

Grid import is strongly zero-inflated:
- approximately 16.2% of raw grid-import samples are strictly positive.

Observed temporal persistence in regular-cadence stretches:
- raw lag ~5 min: correlation about 0.960;
- ~30 min: about 0.822;
- ~60 min: about 0.684;
- ~120 min: about 0.468.

After subtracting the empirical local clock-slot/day-type mean:
- ~5 min residual correlation: about 0.955;
- ~30 min: about 0.803;
- ~60 min: about 0.659;
- ~120 min: about 0.449.

These are descriptive installation-specific statistics, not an AR-model assertion.

## 3. Truth-window eligibility

A target backtest window is eligible only when:
- its local date has at least 280 grid-import samples;
- the entire hidden interval remains within one local date;
- every original consecutive link inside the target interval is between 4.5 and 5.5 minutes;
- all target values are CONFIRMED / not unresolved.

The 4.5–5.5 minute rule is a research backtesting quality gate. It isolates intervals whose hidden truth is well observed and excludes duplicate/burst anomalies and actual source outages from the truth set.

## 4. Target durations and time strata

Nominal durations:
- 20 min;
- 30 min;
- 60 min;
- 120 min;
- 240 min;
- 480 min.

For 20–240 minute tests:
- select at most one eligible target per qualifying local date within each local 6-hour band:
  - 00:00–05:59;
  - 06:00–11:59;
  - 12:00–17:59;
  - 18:00–23:59.

For 480-minute tests:
- use 12-hour bands because an 8-hour target cannot fit inside a 6-hour band.

Within each day/band/duration, target selection is deterministic and timestamp-based; target power values are not used to choose the target.

This deliberately balances time-of-day rather than allowing the many zero-import periods to dominate the sample.

## 5. Masking and anti-leakage

For each target:
- preserve the complete original series as truth;
- hide the interior target observations from both methods;
- target endpoints remain available as they would around an observed source gap;
- no C2 donor block may intersect a hidden interior sample;
- C0 donor pools exclude hidden interior values;
- utility/Enel evidence is never loaded.

Truth hidden energy:
- calculated from original target samples using the application's signed/trapezoidal time-aware integration semantics;
- positive grid-import energy is the evaluation target.

## 6. C0 baseline

C0 mirrors the current production donor hierarchy:
1. same local hour + same weekend flag if >=12 samples;
2. local hour distance <=1 + same weekend flag if >=12;
3. same hour ignoring weekend if >=8;
4. all available samples.

Missing time is completed point-wise in 5-minute segments with independent draws.

Simulation count:
- 2,000.

## 7. Frozen C2-120 candidate

Block length:
- maximum nominal 120 minutes / 24 five-minute values.

For each target cursor:
- choose contiguous donor block starts within +/-15 local clock minutes and same weekday/weekend type;
- require at least 8 eligible starts;
- otherwise widen deterministically to +/-30, then +/-60 minutes;
- then same local hour/day type;
- finally all valid block starts if necessary.

Donor block:
- must contain a full 24-value regular-cadence sequence for the primary 120-minute block;
- may not overlap hidden target interior.

Gaps longer than 120 nominal minutes:
- stitch additional independently sampled contextual blocks;
- this is a known synthetic-risk area and is explicitly tested at 240 and 480 minutes.

Simulation count:
- 2,000 for structural comparability with Phase 2.

## 8. Real-time jitter handling

Actual telemetry is near, but not exactly, five minutes apart.

For each target:
- nominal duration strata are defined by consecutive sample count;
- true energy uses actual timestamps;
- predictive sampled energy uses the candidate's nominal 5-minute segmentation, scaled only for the final target-duration accounting needed to compare with the actual endpoint span.

Timing-jitter sensitivity is recorded separately and must not be silently absorbed into model-quality claims.

## 9. Metrics

Per method and stratum:
- empirical P5-P95 coverage;
- P50 signed bias;
- P50 MAE;
- P50 RMSE;
- P5-P95 width;
- alpha=0.10 interval score;
- donor-block/start count;
- C2 fallback level.

Because multiple targets come from the same day, target cases are not treated as independent Bernoulli trials.

Primary uncertainty intervals:
- 5,000-replicate bootstrap clustered by local date.

Naive Wilson/binomial intervals may be shown only as secondary diagnostics, not as the principal inferential evidence.

## 10. Load-regime reporting

Report separately:
- grid-inactive / near-zero windows: true average import <=100 W;
- active import windows: >100 W.

The 100 W split aligns with the existing application grid-active contextual threshold and is fixed before model scoring.

Where sample size permits, active windows are additionally divided into empirical thirds by true average power for descriptive low/medium/high-active diagnostics. Those post-hoc descriptive thirds are not used to select or tune the method.

## 11. Advancement / STOP logic

C2-120 remains provisional.

Evidence considered:
- overall calibration;
- duration-specific calibration;
- time-of-day/day-type strata;
- active-import strata;
- proper interval score relative to C0;
- P50 bias/error;
- donor evidence;
- long-gap stitching behavior.

A real-data stratum whose cluster-bootstrap 95% coverage interval lies wholly below 90% is evidence of undercoverage.

Phase 3 may produce an applicability limit rather than an all-duration approval if:
- shorter gaps are defensible;
- longer stitched gaps fail;
- the limit is predeclared from evidence rather than chosen to improve agreement with Enel.

If material real-data undercoverage cannot be bounded by a defensible applicability rule:
- reject C2-120;
- return to Phase 2 redesign;
- do not relax acceptance criteria.


# Phase 1 statistical audit — Lot 3: missingness topology

Date: 2026-09-30

Status: **PHASE 1 IN PROGRESS — LOT 3 COMPLETE**

## Design

Question:

> With approximately the same total statistical gap duration, does it matter whether missing telemetry is concentrated in one long interval or distributed across shorter intervals?

Statistical-gap topologies:
- 1 × 120 min;
- 2 × 60 min;
- 4 × 30 min;
- 6 × 20 min.

Each topology totals 2 hours of uncovered statistical-gap duration.

Controls:
- 4 synthetic process families;
- 1,000 repetitions per scenario;
- same synthetic series reused across all four topologies within each repetition;
- 2,000 current-method simulated completions;
- 95% Wilson coverage intervals;
- one-sided exact binomial undercoverage test against 90%;
- Holm correction across the 16 statistical cells;
- P50 bias/MAE/RMSE;
- interval width;
- alpha=0.10 proper interval score;
- paired bootstrap confidence intervals for interval-score differences versus 1×120.

Artifacts:
- `phase1_missingness_topology_audit.py`;
- `PHASE1_LOT3_RESULTS_2026-09-30.csv`;
- `PHASE1_LOT3_PAIRED_INTERVAL_SCORE_2026-09-30.csv`;
- `PHASE1_LOT3_BRIDGED_ISOLATED_RESULTS_2026-09-30.csv`.

## Coverage results

| Process | 1×120 | 2×60 | 4×30 | 6×20 |
|---|---:|---:|---:|---:|
| Stable | 88.8% | 89.1% | 90.5% | 90.4% |
| Day/night | 89.2% | 90.4% | 83.6% | 80.3% |
| Moderate autocorrelation | 61.4% | 64.2% | 68.1% | 72.6% |
| High autocorrelation | 28.6% | 38.9% | 51.3% | 59.1% |

All moderate/high-autocorrelation cells reject nominal 90% coverage after Holm correction.

Stable cells do not reject nominal coverage in this paired lot.

Day/night:
- 1×120 and 2×60 do not reject 90%;
- 4×30 and 6×20 materially under-cover even after Holm correction.

## Interpretation 1 — autocorrelation mechanism confirmed again

When missing time is divided into shorter, well-separated gaps, empirical coverage improves substantially for autocorrelated processes.

This is consistent with the analytical covariance mechanism:
- one long gap contains many positively correlated adjacent missing segments;
- the current method resamples those segments independently and omits more covariance;
- shorter separated gaps reduce the amount of within-gap covariance omitted.

The proper interval score also improves significantly as autocorrelated gaps are fragmented.

For moderate autocorrelation, paired mean interval score improves relative to 1×120 by:
- 2×60: -0.124, bootstrap 95% CI [-0.224, -0.023];
- 4×30: -0.256, CI [-0.361, -0.149];
- 6×20: -0.366, CI [-0.469, -0.267].

For high autocorrelation:
- 2×60: -1.706, CI [-2.033, -1.386];
- 4×30: -3.000, CI [-3.328, -2.666];
- 6×20: -3.556, CI [-3.890, -3.237].

Negative difference means better interval score than one 120-minute gap.

## Interpretation 2 — a second issue appears in the day/night process

For the non-autocorrelated day/night generator:
- 1×120: 89.2%;
- 2×60: 90.4%;
- 4×30: 83.6%;
- 6×20: 80.3%.

P50 directional bias remains near zero, but coverage and interval score worsen for many short distributed gaps.

This is **not explained by serial autocorrelation**, because the day/night residual noise is independent.

Working hypotheses requiring a targeted next test:
1. the current donor stratification uses whole local hour and does not distinguish minute-within-hour;
2. a short gap located on a rising/falling part of the deterministic daily profile may be matched against donor values from the entire clock hour;
3. using more distinct short gaps also uses more finite empirical donor pools, so uncertainty in the donor distributions may accumulate.

These are hypotheses, not yet final conclusions.

A targeted oracle/time-bin test is required before attributing causality.

## Isolated 5-minute source losses

A separate experiment removed 24 isolated 5-minute source samples, corresponding nominally to 2 hours of missing source points.

Because each isolated loss leaves a 10-minute endpoint separation:
- current integration bridges every loss;
- reported uncovered hours = 0;
- reported temporal coverage = 100%;
- no P5/P50/P95 interval is produced.

Across 1,000 repetitions, RMSE of the bridged total was:
- stable: 0.0727 kWh;
- day/night: 0.0594 kWh;
- moderate autocorrelation: 0.0665 kWh;
- high autocorrelation: 0.0321 kWh.

The errors are modest in these synthetic cases, but the semantic conclusion remains:

> 100% integration coverage is not the same thing as 100% source-sample completeness.

## Lot 3 conclusion

The current model has at least two separable calibration risks:

1. **serial dependence risk** — mathematically supported and repeatedly demonstrated;
2. **conditional donor-resolution / finite-donor risk** — indicated by day/night short-gap undercoverage and requiring a targeted causal audit.

No production replacement is authorized yet.

## Next validation before appliance-event tests

Run a targeted donor-resolution experiment on the day/night process:

- current donor pool: same hour + same weekend type;
- finer oracle/time-position pool: matching 5-minute clock slot or a narrow minute band;
- true generator oracle as a calibration control;
- vary donor-history length to separate finite-donor uncertainty from time-bin mismatch.

Only after this mechanism is identified should Phase 1 move to persistent appliance-like load episodes.

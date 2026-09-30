# Phase 1 statistical audit — Lot 4: donor time-resolution causal test

Date: 2026-09-30

Status: **PHASE 1 IN PROGRESS — LOT 4 COMPLETE**

## Question

Lot 3 found unexpected undercoverage in a non-autocorrelated day/night process when 2 hours of missing data were fragmented into six 20-minute gaps.

Two candidate explanations were:
1. finite donor-pool size;
2. current donor matching by whole clock hour is too coarse for short gaps on a rising/falling intrahour profile.

Lot 4 separates these mechanisms.

## Controlled design

Process:
- deterministic day/night profile;
- independent Gaussian residual noise;
- no serial autocorrelation.

Topology:
- six 20-minute gaps;
- total statistical-gap duration = 2 hours.

Repetitions:
- 1,000 per cell.

Current Monte Carlo baseline:
- 2,000 completions.

Available report/history lengths:
- 14 days;
- 30 days;
- 60 days.

Methods:

### Current-hour method
Matches the current production concept:
- same local hour;
- same weekend/weekday type;
- all minutes within that hour pooled together.

### Fine ±15-minute method
Experimental causal control:
- same weekend/weekday type;
- donor clock position restricted to ±15 minutes around target position.

This is not yet a production recommendation.

### Exact generator oracle
Analytical control:
- synthetic data-generating process is known exactly;
- the true missing-energy distribution under the controlled Gaussian model is calculated analytically;
- no empirical donor pool is required.

If the oracle failed to approach nominal 90% coverage, the audit itself would be suspect.

## Results

| Available days | Current hour | Fine ±15 min | Exact oracle |
|---:|---:|---:|---:|
| 14 | 83.9% | 89.9% | 90.4% |
| 30 | 82.2% | 94.0% | 91.9% |
| 60 | 83.3% | 92.8% | 91.3% |

95% Wilson intervals and raw metrics are in:
- `PHASE1_LOT4_DONOR_RESOLUTION_RESULTS_2026-09-30.csv`.

Exact one-sided binomial tests against nominal 90% show:
- current-hour undercoverage is strongly significant at 14, 30 and 60 days;
- fine ±15-minute method does not show undercoverage;
- exact oracle does not show undercoverage.

## Causal interpretation

Increasing available data from 14 to 60 days does **not** repair the current-hour method:
- 83.9% -> 82.2% -> 83.3%.

Therefore simple donor-count scarcity is not a sufficient explanation.

Changing only temporal matching resolution from a 60-minute clock-hour bucket to a narrow ±15-minute clock-position neighborhood materially restores calibration:
- 89.9% / 94.0% / 92.8%.

The exact oracle remains near the nominal target:
- 90.4% / 91.9% / 91.3%.

This supports the following mechanism:

> Whole-hour donor pooling can mix materially different points on a rising or falling intrahour load profile. Mean positive/negative errors may cancel across repetitions, leaving P50 global bias near zero, while conditional local mismatch still causes P5-P95 to miss truth too often.

That explains why prior lots could show:
- near-zero average P50 bias;
- yet substantial interval undercoverage.

## What Lot 4 proves and does not prove

Supported:
- whole-hour donor resolution is a real calibration risk in the controlled day/night experiment;
- adding more days alone does not solve it;
- finer time-position matching can restore nominal coverage in this controlled process;
- the audit framework is internally calibrated because the exact oracle behaves as expected.

Not yet established:
- ±15 minutes is the optimal production bandwidth;
- the user's real telemetry needs exactly this bandwidth;
- minute matching alone solves serial dependence;
- block bootstrap is unnecessary.

A future candidate model likely needs both:
1. finer contextual matching;
2. preservation of temporal dependence across contiguous missing segments.

## Decision

Lot 4 closes the causal question raised by Lot 3 sufficiently for Phase 1.

Next:
- test persistent appliance-like load episodes as the final synthetic stress family before deciding whether Phase 1 synthetic audit is complete;
- then move to Phase 2 method comparison, subject to Phase 3 real-data backtesting later.

# Phase 1 Lot 1-2 validation checkpoint

Date: 2026-09-30

Status: **VALIDATION GATE PASS — PROCEED TO LOT 3**

## Direct production-code reference

GitHub Actions workflow:
- `.github/workflows/statistical-research-validation.yml`;
- run: `36781580855`;
- job: `110112872942`;
- result: **SUCCESS**.

The workflow executes the actual production:
- `EnergyRangeStatisticsService`;
- `UtilityGridImportStatisticalCompletionService`.

Direct analytical fixtures passed:

1. 15-minute separation:
   - source samples are missing;
   - current integration bridges the interval;
   - reported integration coverage = 100%;
   - no predictive interval is executed.

2. 20-minute separation at constant 1 kW:
   - statistical completion activates;
   - current 2,000 simulations execute;
   - analytically true total = 48.0 kWh;
   - P5=P50=P95=48.0 kWh exactly.

3. complete 750 W day:
   - true total = 18.0 kWh;
   - P5=P50=P95=18.0 kWh.

This removes the main semantic uncertainty between the Python audit mirror and the production gap/completion behavior.

## Formal coverage inference on Lot 2

For each statistical-gap cell:
- `R=1000`;
- nominal coverage `p0=0.90`;
- 95% Wilson interval reported;
- one-sided exact binomial test:
  - H0: p >= 0.90
  - H1: p < 0.90;
- Holm correction applied across the 28 statistical-gap cells at family-wise alpha=0.05.

Full results:
- `PHASE1_LOT2_INFERENTIAL_VALIDATION_2026-09-30.csv`.

### Main result

All moderate- and high-autocorrelation cells reject nominal 90% coverage by very large margins, including after Holm correction.

The result is therefore not a chance fluctuation from testing many scenarios.

The stable/day-night families are much closer to nominal coverage, but two cells also show corrected evidence of undercoverage:
- stable, 8 h gap: 86.5%, Holm-adjusted p ≈ 0.0034;
- day/night, 30 min gap: 87.2%, Holm-adjusted p ≈ 0.0326.

These smaller deviations require further investigation and should not be conflated with the much larger serial-dependence failure.

## Analytical covariance cross-check

The separate mathematical validation note derives the variance of a sum under AR(1) dependence.

For a 2-hour gap (24 five-minute segments):
- phi=0.55 predicts approximate nominal-interval true coverage ~64.1%;
- phi=0.93 predicts ~33.4%.

Observed Phase 1 synthetic coverage was of the same order:
- moderate autocorrelation: ~59.6%-61.7%;
- high autocorrelation: ~28.3%-30.6%.

Therefore the undercoverage mechanism is supported independently by covariance mathematics.

## Decision

Do **not** rerun all Lot 1-2 cells.

Reason:
- production boundary semantics are now directly validated in C#;
- the primary autocorrelation finding has both repeated-simulation and analytical support;
- rerunning identical experiments would add computation but little information.

Proceed to Lot 3 with stronger metrics:
- Wilson interval;
- exact binomial calibration test;
- Holm family-wise correction;
- P50 bias/MAE/RMSE;
- interval width;
- alpha=0.10 interval score for statistical predictive intervals.

Phase 1 remains open.

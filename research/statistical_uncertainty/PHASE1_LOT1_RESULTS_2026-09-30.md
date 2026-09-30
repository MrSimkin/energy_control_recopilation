# Phase 1 statistical audit — Lot 1

Date: 2026-09-30

Status: **PHASE 1 IN PROGRESS — LOT 1 COMPLETE**

Method under audit:
- `grid-import-empirical-bootstrap.v1`;
- current production simulation count: **2,000**;
- audit simulation count for this lot: **2,000**;
- simulation count is parameterized in the research harness for later convergence testing.

## Purpose of this lot

Test only four core synthetic behaviors before expanding the audit matrix:

1. stable consumption;
2. day/night consumption pattern;
3. moderate temporal autocorrelation;
4. high temporal autocorrelation.

Each scenario used:
- 14 days of synthetic 5-minute data;
- one interior 2-hour gap;
- **1,000 independent repetitions**;
- **2,000 current-method simulated completions per repetition**;
- P5/P50/P95 produced with the same percentile definition as the current C# service.

The complete synthetic series is known before the gap is hidden, so the audit can directly measure whether the true complete-period kWh falls inside the model's P5-P95 interval.

## Important execution note

The local analysis environment could not clone/execute the .NET repository directly.

Therefore this lot uses:
- a behavioral Python mirror of the current C# algorithm;
- the same 5-minute completion segmentation;
- the same candidate-pool hierarchy by local hour and weekend status;
- the same independent point-wise donor sampling behavior;
- the same P5/P50/P95 linear percentile interpolation;
- the same deterministic FNV-style seed principle;
- the same observed-energy rule that does not integrate across gaps above the continuity threshold.

This is strong algorithm-design evidence, but it is **not yet a direct invocation of the compiled C# service**. A later implementation/validation gate must confirm equivalence against the actual service before production conclusions are finalized.

## Results

| Scenario | Empirical P5-P95 coverage | 95% CI for coverage | P50 mean bias | P50 MAE | Median abs. P50 error vs missing kWh | Median interval width | Width vs true missing kWh |
|---|---:|---:|---:|---:|---:|---:|---:|
| Stable | 89.5% | 87.45%–91.25% | +0.0004 kWh | 0.0467 kWh | 2.52% | 0.1865 kWh | 11.67% |
| Day/night | 87.8% | 85.63%–89.69% | -0.0009 kWh | 0.0439 kWh | 2.70% | 0.1712 kWh | 12.39% |
| Moderate autocorrelation (AR≈0.55) | 61.7% | 58.65%–64.66% | -0.0004 kWh | 0.1326 kWh | 7.81% | 0.2914 kWh | 21.17% |
| High autocorrelation (AR≈0.93) | 30.6% | 27.82%–33.53% | +0.0054 kWh | 0.3783 kWh | 22.03% | 0.3627 kWh | 25.81% |

Median true missing energy in these experiments was approximately:
- Stable: 1.601 kWh;
- Day/night: 1.325 kWh;
- Moderate autocorrelation: 1.371 kWh;
- High autocorrelation: 1.408 kWh.

## Interpretation

### Result 1 — the central estimate is not showing a material directional bias in this lot

P50 mean bias remained very close to zero in all four scenarios.

This is important: the first observed problem is **not primarily that the current method systematically pushes the estimate up or down**.

### Result 2 — P5-P95 behaves approximately as intended for near-independent data

Stable consumption produced 89.5% empirical coverage, compatible with a nominal central 90% interval.

The day/night case was slightly lower at 87.8%, already suggesting that even ordinary temporal structure can matter.

### Result 3 — temporal dependence materially breaks nominal interval coverage

With moderate autocorrelation, coverage fell to 61.7%.

With high autocorrelation, coverage fell to 30.6%.

The most plausible mechanism is the known structural property of the current algorithm: each missing 5-minute segment is sampled independently from a donor pool. This preserves marginal level by hour/day type, but does not preserve runs of high or low consumption. Summing many independent draws can therefore make the completed-total distribution substantially too concentrated when the real process is serially dependent.

### Result 4 — more Monte Carlo draws alone would not fix this failure

This lot deliberately keeps 2,000 simulations because Phase 1 is auditing the current production behavior.

If the model structure produces an interval that is too narrow under temporal dependence, increasing 2,000 to 100,000 would estimate that narrow distribution more precisely; it would not restore the missing serial dependence.

The separate convergence study remains required in Phase 2.

## What this lot does NOT establish

It does not yet establish:
- that real household grid-import telemetry has AR=0.55 or AR=0.93;
- that block bootstrap is definitely the final replacement;
- the correct block size;
- behavior under short/long/multiple gaps;
- behavior under weekday/weekend differences;
- behavior under appliance spikes or regime changes;
- behavior on the user's real database;
- production acceptance thresholds.

Those remain later Phase 1/2/3 work.

## Lot 1 conclusion

The current method is **not rejected in full**: it can produce sensible nominal coverage for near-independent synthetic data and its P50 is not materially directionally biased in these tests.

However, Lot 1 provides strong evidence that the current independent point-wise resampling can **severely understate interval uncertainty when the underlying process is temporally autocorrelated**.

This is sufficient to justify continuing the Phase 1 audit before any P5/P50/P95 wording is strengthened for Enel/SEC.

Next proposed Phase 1 lot:
- hold the process family fixed;
- vary gap duration (short, medium, long, very long);
- then separately test multiple distributed gaps and appliance-like contiguous spikes.

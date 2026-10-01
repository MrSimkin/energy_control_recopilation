# Phase 3 — Real-data backtest report

Date: 2026-09-30

Status: **COMPLETE — C2-120 REJECTED AS A GENERAL PRODUCTION METHOD**

Input package:
- `SolarOfThings-ResearchPackage-20260930-214934.zip`;
- package SHA-256 `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`;
- all manifest file hashes and byte counts validated.

Frozen protocol:
- `PHASE3_REAL_DATA_BACKTEST_PROTOCOL_2026-09-30.md`.

No Enel bill, meter reading, tariff or utility comparison value was loaded or used.

## 1. Real installation profile

`grid_import_power_w`:
- 43,741 confirmed normalized samples;
- 161 local dates;
- 140 dates with >=280 grid samples;
- median cadence 5.0043 minutes;
- 99.10% of links between 4.5 and 5.5 minutes;
- positive raw samples about 16.2%.

Temporal persistence is strong even after removing clock-slot/day-type mean:
- residual ~5 min correlation: ~0.955;
- ~30 min: ~0.803;
- ~60 min: ~0.659;
- ~120 min: ~0.449.

This validates the Phase 1 concern that adjacent 5-minute values are not independent.

## 2. Backtest design actually executed

- rolling 90-day historical-only donor horizon;
- no future observations;
- targets only on well-observed dates/windows;
- strict regular-cadence truth windows;
- nominal durations 20, 30, 60, 120, 240 and 480 min;
- balanced local time bands;
- C0 and C2 receive the same donor history;
- 2,000 simulations each;
- true hidden energy from original time-aware trapezoidal integration;
- 5,000-replicate confidence intervals clustered by local date.

Target counts:
- 20 min: 242;
- 30 min: 242;
- 60 min: 241;
- 120 min: 241;
- 240 min: 192;
- 480 min: 84.

C2 used the primary +/-15-minute contextual pool in every tested chunk; its failures are therefore not caused by widening to a coarse fallback.

## 3. Overall duration results

| Duration | C0 coverage | C2 coverage | C2 cluster 95% CI |
|---:|---:|---:|---:|
| 20 min | 78.9% | **93.0%** | 88.9–96.7% |
| 30 min | 72.7% | **93.0%** | 89.2–96.3% |
| 60 min | 67.6% | **92.1%** | 88.0–95.9% |
| 120 min | 41.5% | **93.4%** | 89.9–96.3% |
| 240 min | 20.8% | **92.7%** | 89.1–95.9% |
| 480 min | 13.1% | **83.3%** | 75.6–90.4% |

C2 is a very large structural improvement over C0.

Proper interval score also materially improves for all durations >=30 min. Paired clustered C2-C0 score differences:
- 30m: -0.402, 95% CI [-0.626,-0.182];
- 60m: -1.786, [-2.274,-1.341];
- 120m: -5.520, [-6.484,-4.570];
- 240m: -14.211, [-16.843,-11.498];
- 480m: -31.844, [-37.754,-26.130].

At 20 minutes the score difference is not materially distinguishable.

## 4. Why the overall result is not sufficient

Real grid import is strongly zero-inflated.

An unconditional central interval can achieve apparently good overall coverage by covering near-zero periods very well while missing persistent active-import episodes.

This possibility was predeclared in the Phase 3 protocol, which required load-regime stratification.

### Hidden truth >100 W average

C2 coverage:
- 20m: 69.6%;
- 30m: 69.6%;
- 60m: 66.7%;
- 120m: 74.6%;
- 240m: 77.8%;
- 480m: 75.9%.

For every duration, the clustered 95% upper confidence bound remains below 90%.

Outcome-conditioned coverage alone is not enough to prove an operational defect, so an observable pre-gap diagnostic was also run.

## 5. Observable start-state diagnostic

The grid-import value at the first endpoint of a real gap is observed before the missing interval and is therefore legitimate conditioning information.

When start grid import is >100 W, frozen C2 coverage is:

| Duration | Cases | Coverage | Cluster 95% CI | P50 mean bias |
|---:|---:|---:|---:|---:|
| 20m | 54 | 68.5% | 55.2–82.4% | -0.183 kWh |
| 30m | 54 | 68.5% | 54.8–81.6% | -0.290 kWh |
| 60m | 51 | 66.7% | 53.4–80.0% | -0.673 kWh |
| 120m | 51 | 74.5% | 62.7–85.1% | -1.355 kWh |
| 240m | 41 | 78.0% | 62.9–90.2% | -2.448 kWh |
| 480m | 13 | 76.9% | 53.9–100% | -2.442 kWh |

For 20–120 minutes the clustered upper bound is clearly below the nominal 90%, and the negative P50 bias grows with duration.

This is an operationally observable structural defect:
- C2 knows clock time and weekday/weekend;
- it does not condition on the already-observed fact that the grid is currently active;
- the real process is highly persistent;
- therefore the sampled distribution too often returns toward the dominant zero-import regime too quickly.

## 6. Hard state-filter diagnostic does not rescue the method

A post-failure diagnostic required the first donor block to start in the same active/inactive state.

This was **not** used to approve or retune C2.

Result:
- inactive cases became nearly degenerate/easy;
- active-case coverage did not improve materially and often worsened;
- state-matched contextual pools frequently had only about 8 independent donor dates.

Therefore a hard active/inactive filter creates an effective-sample-size problem and is not an adequate redesign.

The next model needs soft/contextual conditioning and explicit effective independent donor evidence rather than treating many 5-minute starts from a small number of days as independent evidence.

## 7. Long real gaps matter

The actual dataset contains 28 observed source separations >15 min:
- 10 exceed 240 min;
- 6 exceed 480 min;
- 4 exceed 1,440 min.

Therefore the 480-minute weakness cannot be dismissed as an irrelevant synthetic edge case.

C2's 120-minute stitching limitation remains material.

## 8. Timing-jitter sensitivity

Backtest truth was recomputed under a nominal fixed-5-minute left-rectangle sensitivity check.

C2 coverage changed by:
- 0.0 pp at 20m;
- -0.4 pp at 30m;
- -0.4 pp at 60m;
- 0.0 pp at 120m;
- -0.5 pp at 240m;
- 0.0 pp at 480m.

The Phase 3 decision is not caused by timestamp jitter or trapezoid-vs-nominal integration detail.

## 9. Phase 3 decision

**Reject C2-120 as a general production predictive interval.**

Do not:
- weaken the 90% interpretation;
- hide the active-regime failure;
- approve C2 based only on its overall average;
- use Enel values to retune it.

Retain C2 as a strong research baseline because it proves that dependence-preserving contextual blocks are substantially better than C0.

Return to Phase 2 redesign.

Required redesign properties:
1. use observable boundary/state context without hard low-sample stratification;
2. preserve effective independent donor units;
3. address dependence beyond 120 minutes rather than stitching independent blocks blindly;
4. re-run untouched validation before returning to Phase 3.

No production C# or PDF wording is approved by this report.

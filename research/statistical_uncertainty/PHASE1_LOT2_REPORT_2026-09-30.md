# Phase 1 statistical audit — Lot 2: five-minute cadence and gap duration

Date: 2026-09-30

Status: **PHASE 1 IN PROGRESS — LOT 2 COMPLETE**

Method under audit:
- production method: `grid-import-empirical-bootstrap.v1`;
- production simulation count preserved: **2,000**;
- **1,000 repetitions per scenario/duration cell**;
- nominal source cadence: **5 minutes**;
- synthetic series length: 14 days.

Artifacts:
- harness: `research/statistical_uncertainty/phase1_gap_duration_audit.py`;
- raw results: `research/statistical_uncertainty/PHASE1_LOT2_RESULTS_2026-09-30.csv`.

## Questions tested

1. How does the current continuity threshold interact with 5-minute source cadence?
2. How does P5-P95 empirical coverage change as one contiguous missing interval grows?
3. Does temporal autocorrelation remain the dominant weakness?
4. Does P50 remain approximately unbiased while interval coverage fails?

Tested separations:
- 10 min;
- 15 min;
- 20 min;
- 30 min;
- 1 h;
- 2 h;
- 4 h;
- 8 h;
- 12 h.

Synthetic process families:
- stable;
- day/night;
- moderate autocorrelation;
- high autocorrelation.

## Confirmed production boundary

The current `EnergyRangeStatisticsService` uses:

> continuity threshold = median sample gap × 3, clamped to 10–20 minutes.

With nominal 5-minute cadence, the effective threshold is **15 minutes**.

Therefore:
- 10-minute separation: bridged as continuous;
- 15-minute separation: bridged as continuous;
- 20 minutes or more: classified as uncovered and statistically completed.

This means the distinction is not merely “missing sample” vs “no missing sample”. A short source-sample loss can still be treated as covered by the integration layer.

## Bridged 10–15 minute cases

These cases do not receive a predictive distribution. The output collapses to one integrated value and the current service can return `COMPLETE_OBSERVATION`.

| Process | Separation | Missing source samples | Reported temporal coverage | Mean abs. reconstruction error |
|---|---:|---:|---:|---:|
| Stable | 10 min | 1 | 100% | 0.0116 kWh |
| Stable | 15 min | 2 | 100% | 0.0190 kWh |
| Day/night | 10 min | 1 | 100% | 0.0093 kWh |
| Day/night | 15 min | 2 | 100% | 0.0164 kWh |
| Moderate autocorrelation | 10 min | 1 | 100% | 0.0109 kWh |
| Moderate autocorrelation | 15 min | 2 | 100% | 0.0207 kWh |
| High autocorrelation | 10 min | 1 | 100% | 0.0052 kWh |
| High autocorrelation | 15 min | 2 | 100% | 0.0106 kWh |

Because the output interval has zero width, exact empirical P5-P95 coverage against the hidden full 5-minute truth is approximately zero. That should **not** be interpreted as a large energy error: absolute errors were small in these synthetic cases.

The relevant finding is semantic:

> current “100% temporal coverage / complete observation” means the energy integration can bridge the interval under its continuity rule; it does not necessarily mean every nominal 5-minute source sample existed.

A later reporting/design phase should distinguish **integration coverage** from **raw/source sample completeness** if both are exposed to third parties.

## P5-P95 empirical coverage once statistical completion activates

### Stable

| Separation | Coverage |
|---|---:|
| 20 min | 93.5% |
| 30 min | 90.5% |
| 1 h | 88.5% |
| 2 h | 89.3% |
| 4 h | 87.4% |
| 8 h | 86.5% |
| 12 h | 88.7% |

The current method behaves broadly near the nominal 90% target for an approximately independent stable process.

### Day/night pattern

| Separation | Coverage |
|---|---:|
| 20 min | 88.9% |
| 30 min | 87.2% |
| 1 h | 88.1% |
| 2 h | 87.8% |
| 4 h | 88.3% |
| 8 h | 88.7% |
| 12 h | 91.4% |

The hour/day-type donor stratification handles this synthetic day/night structure reasonably well.

### Moderate temporal autocorrelation

| Separation | Coverage |
|---|---:|
| 20 min | 72.4% |
| 30 min | 70.3% |
| 1 h | 62.0% |
| 2 h | 59.6% |
| 4 h | 61.6% |
| 8 h | 59.3% |
| 12 h | 58.9% |

The nominal 90% interval materially undercovers as soon as serial dependence is introduced.

### High temporal autocorrelation

| Separation | Coverage |
|---|---:|
| 20 min | 56.5% |
| 30 min | 47.4% |
| 1 h | 35.6% |
| 2 h | 28.3% |
| 4 h | 25.9% |
| 8 h | 22.7% |
| 12 h | 25.0% |

The current independent 5-minute resampling strongly understates uncertainty under persistent serial dependence.

## P50 behavior

Across the tested cells, mean P50 directional bias remained generally close to zero.

Absolute error grows naturally with gap length, but the dominant structural problem remains interval width rather than a simple systematic upward/downward shift.

This reinforces the Lot 1 conclusion:

> a reasonable P50 does not imply that P5-P95 is calibrated.

## Why longer gaps expose the problem

The current method samples each missing 5-minute segment independently.

For positively autocorrelated real processes, neighboring segments tend to remain jointly high or jointly low for some duration.

Independent resampling removes much of that covariance. As more 5-minute segments are summed, independent highs and lows cancel each other, producing a completed-total distribution that can be too concentrated.

Therefore:
- P50 can remain stable;
- P5 and P95 can be too close to P50;
- empirical coverage can fall far below the intended central 90%.

Increasing Monte Carlo count from 2,000 to 100,000 would estimate this same structurally narrow distribution more precisely; it would not restore the missing temporal covariance.

## Lot 2 conclusions

1. The 5-minute cadence itself is not a problem; **ignoring dependence between adjacent 5-minute samples is**.
2. The current 15-minute continuity threshold creates a second issue: short missing-source episodes can be classified as fully covered by integration.
3. Stable/day-night synthetic cases are handled reasonably after statistical completion activates.
4. Moderate/high serial dependence causes substantial P5-P95 undercoverage across every tested statistical-gap length.
5. P50 remains comparatively well centered, so the principal defect is uncertainty calibration.
6. No production replacement is authorized yet.

## Execution caveat

As in Lot 1, this is a behavioral Python mirror of the production C# algorithm, not a direct invocation of the compiled service.

The harness preserves:
- 5-minute completion segments;
- 15-minute effective threshold under nominal 5-minute cadence;
- same-hour + same-weekend donor logic where the strict pool is sufficiently populated;
- independent point-wise donor draws;
- P5/P50/P95 percentile behavior;
- 2,000 simulated completions.

Direct C# equivalence remains a later validation gate.

## Next proposed Lot 3 — not yet executed

Compare missingness topology while holding total missing duration approximately constant:

- one contiguous gap;
- several medium contiguous gaps;
- many distributed losses that exceed the continuity threshold;
- isolated 5-minute source losses that remain bridged.

Then add appliance-like contiguous high-load episodes to determine whether missingness aligned with persistent load events creates additional bias or undercoverage.

Phase 1 remains open.

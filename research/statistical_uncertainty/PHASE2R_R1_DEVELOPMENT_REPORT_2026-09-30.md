# Phase 2R — R1 development result

Date: 2026-09-30

Status: **R1 FAILED DEVELOPMENT GATE — LOCKED HOLDOUT NOT INSPECTED**

Candidate:
`grid-import-boundary-state-whole-gap-day-bootstrap.candidate-v2`.

Frozen design:
- whole-gap contiguous donor blocks;
- same observable start/end active-state transition;
- one retained block per donor local day;
- rolling 90-day prior-only history;
- >=20 distinct donor days required;
- deterministic clock/day-type widening.

Development dates:
- 2026-07-19 through 2026-09-11;
- exact full 90-day history horizon required;
- same Phase 3 truth-quality windows.

Locked candidate holdout:
- 2026-09-13 through 2026-09-26;
- **not inspected for R1**, because the development gate failed.

## Development result

Overall empirical coverage among intervals for which R1 had sufficient evidence:

| Duration | Coverage | Median donor days |
|---:|---:|---:|
| 20m | 90.86% | 54.5 |
| 30m | 90.86% | 55.0 |
| 60m | 90.81% | 52.0 |
| 120m | 89.73% | 46.0 |
| 240m | 88.96% | 40.5 |
| 480m | 93.65% | 44.5 |

The aggregate result is misleading unless conditioned on information observable at the gap boundary.

### Observable active-start subset

When the first observed boundary value was >100 W:

| Duration | Cases | Coverage | P50 bias |
|---:|---:|---:|---:|
| 20m | 41 | 58.54% | +0.299 kWh |
| 30m | 41 | 58.54% | +0.457 kWh |
| 60m | 40 | 57.50% | +0.968 kWh |
| 120m | 40 | 55.00% | +1.840 kWh |
| 240m | 35 | 51.43% | +3.111 kWh |

The 480-minute active subset is too small for interpretation:
- 4 active cases with sufficient evidence;
- 7 additional active->active cases failed the >=20-day evidence floor.

## Interpretation

R1 solves two real weaknesses of C2:
- it does not stitch independent 120-minute blocks;
- it makes the independent donor unit explicit at the day level.

However, a binary >100 W active/inactive transition is not rich enough to describe the persistent grid-import state.

The method still mixes materially different active regimes:
- weak vs strong import;
- short vs long active episodes;
- rising vs falling import;
- distinct persistence trajectories.

The overall near-90% result is again dominated by easier inactive-grid intervals.

## Decision

**R1 is rejected before holdout.**

Per the predeclared protocol:
- do not inspect R1 holdout outcomes;
- do not weaken the gate;
- do not treat aggregate coverage as sufficient;
- redesign using a calibration mechanism that can correct the known conditional failure without splitting the donor corpus into tiny hard state pools.

Next research direction:
- retain C2 as the substantially improved dependence-preserving base distribution;
- evaluate a separately calibrated predictive interval using fixed observable groups;
- use day-level calibration units to respect within-day dependence;
- retain an untouched new-candidate holdout;
- do not claim iid/exchangeable conformal guarantees for the real time series without additional assumptions.

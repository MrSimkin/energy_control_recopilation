# Phase 2 numerical simulation-count convergence gate

Date: 2026-09-30

Status: **COMPLETE — omitted Phase 2 gate repaired before Phase 3 scoring**

## Why this gate was run

The canonical statistical plan required the simulation count to remain parameterizable and required a convergence study at:

- 2,000;
- 5,000;
- 10,000;
- 20,000;
- 50,000;
- 100,000 simulations.

Phase 2 had been marked complete before a persisted convergence artifact existed. This checkpoint repairs that omission before any real-data model scoring is performed.

This numerical gate does **not** use:
- Enel bills;
- utility-meter values;
- hidden-truth coverage;
- Phase 3 acceptance results.

The now-available research package is used only to supply realistic C2-120 donor-pool geometry and zero-inflated empirical block-energy distributions.

## Design

Candidate:
- frozen `C2-120` / `grid-import-context-block-bootstrap-120m.candidate-v1`.

Representative windows:
- 40 deterministic timestamp-selected donor geometries;
- 10 each at nominal 30, 120, 240 and 480 minutes;
- selected without looking at hidden energy truth.

For every geometry:
- a 500,000-simulation run is used as the numerical reference distribution;
- each requested simulation count is run under 5 independent deterministic seeds;
- P5, P50 and P95 are compared with the 500,000-run reference.

Because the real empirical distribution is highly discrete/zero-inflated, percentile estimates can jump between adjacent empirical mass points. Therefore absolute kWh error is the primary convergence diagnostic; normalized errors near zero-width intervals are not used as a sole criterion.

## Aggregate error versus 500,000-simulation reference

Maximum absolute P5/P95 endpoint error per run:

| Simulations | Median | P90 | P95 | Maximum |
|---:|---:|---:|---:|---:|
| 2,000 | 0.0347 kWh | 0.2692 | 0.3684 | 1.0115 |
| 5,000 | 0.0153 kWh | 0.1790 | 0.2398 | 1.0115 |
| 10,000 | 0.0088 kWh | 0.1192 | 0.1689 | 1.0115 |
| 20,000 | 0.0087 kWh | 0.0892 | 0.1147 | 0.5898 |
| 50,000 | 0.0063 kWh | 0.0475 | 0.0922 | 1.0115 |
| 100,000 | **0.0000 kWh** | **0.0396** | **0.0595** | **0.2800** |

P50 is substantially more stable than the tail percentiles at every count.

The occasional non-monotone maximum is expected for a discrete empirical distribution: a finite Monte Carlo estimate can cross a percentile mass-point boundary and jump to the adjacent block-energy value. This is precisely why tail convergence is judged across many windows/seeds rather than by a single run.

## Decision

- **2,000 simulations remain the Phase 3 structural-validation baseline**, because all Phase 2 candidate comparisons used 2,000 and changing the count before real backtesting would confound model-structure and numerical-precision effects.
- **100,000 is the preferred production-count candidate** if runtime remains operationally acceptable.
- The final production default is not frozen until the implementation phase benchmarks actual runtime and confirms that increasing beyond the selected count does not materially change report percentiles.
- More simulations do not repair structural undercoverage; they only reduce Monte Carlo numerical error.

This gate closes the previously omitted Phase 2 simulation-count requirement.


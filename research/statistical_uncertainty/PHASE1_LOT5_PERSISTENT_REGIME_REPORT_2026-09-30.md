# Phase 1 statistical audit — Lot 5: persistent regime / appliance-like load

Date: 2026-09-30

Status: **PHASE 1 SYNTHETIC AUDIT — FINAL STRESS FAMILY COMPLETE**

## Purpose

Test a non-Gaussian dependence pattern that resembles a persistent appliance or operating regime:

- on a given comparable day, a 1.5 kW load is either ON for the entire missing episode or OFF for the entire episode;
- across days, ON probability is q;
- donor days represent historical comparable days;
- the current algorithm nevertheless samples each missing 5-minute segment independently.

This is a controlled stress model, not a claim that any specific household appliance follows these exact probabilities.

## Analytical model

Let:
- B ~ Bernoulli(q) be the true episode state;
- P be appliance power;
- m be the number of 5-minute segments;
- Delta = 1/12 hour.

True persistent missing appliance energy is:

```
Y = B * P * m * Delta
```

Its variance is:

```
Var(Y) = P^2 * (m*Delta)^2 * q(1-q)
```

The current point-wise resampling model effectively replaces the common episode state B with independent segment states B_1,...,B_m:

```
Y_ind = P * Delta * sum(B_i)
```

with:

```
Var(Y_ind) = P^2 * Delta^2 * m * q(1-q)
```

Therefore:

```
Var(Y_ind) / Var(Y) = 1/m
SD(Y_ind) / SD(Y) = 1/sqrt(m)
```

So for:
- 20 min, m=4: variance ratio 1/4, SD ratio 0.50;
- 30 min, m=6: variance ratio 1/6, SD ratio 0.408;
- 60 min, m=12: variance ratio 1/12, SD ratio 0.289.

This is an exact result for the controlled regime model.

## Ideal point-wise percentile consequence

If the empirical ON fraction were exactly q, the point-wise completion distribution is proportional to Binomial(m,q), while truth is all-OFF or all-ON.

For q=0.5:
- m=6 (30 min): the central 90% Binomial interval excludes both persistent extremes -> idealized coverage 0%;
- m=12 (60 min): same -> idealized coverage 0%.

This demonstrates that a well-estimated marginal ON frequency is not enough. The missing information is episode-level dependence.

## Finite-donor simulation

Simulation design:
- 40 comparable donor days;
- 1,000 repeated datasets per cell;
- current 2,000 Monte Carlo completions;
- q in {0.2, 0.5, 0.8};
- episode duration 20, 30, 60 minutes.

Results:

| q | 20 min | 30 min | 60 min |
|---:|---:|---:|---:|
| 0.2 | 80.2% | 79.5% | 51.3% |
| 0.5 | 66.4% | 8.8% | 0.0% |
| 0.8 | 79.1% | 77.8% | 52.0% |

The simulation can be worse than the idealized fixed-q calculation because the effective ON frequency is itself estimated from a finite number of independent donor days, while the algorithm treats many within-day 5-minute samples as if they were independent donor evidence.

## Interpretation

This stress family independently confirms the core structural defect:

> A point-wise bootstrap can preserve the correct marginal frequency of high/low samples while severely understating uncertainty about persistent episodes.

It also exposes pseudo-replication:
- a 60-minute ON donor day contributes 12 high 5-minute samples;
- those 12 values are not 12 independent observations of episode occurrence;
- the effective independent unit for that regime is closer to the day/episode.

This mechanism is distinct from but compatible with the AR(1) covariance finding.

## Phase 1 synthetic conclusion

Across five audit lots, the current method has now been challenged by:
- stable iid-like data;
- deterministic day/night structure;
- moderate/high serial correlation;
- varying gap duration;
- different missingness topology;
- donor time-resolution mismatch;
- finite donor-history tests;
- persistent regime/appliance-like episodes.

The evidence is sufficient to conclude that the current method can provide a reasonable P50 in several conditions but **cannot be relied on as a calibrated P5-P95 predictive range in the presence of realistic temporal persistence or coarse conditional donor matching**.

No claim is made yet about the user's real telemetry. That belongs to real-data backtesting.

## Decision

The synthetic audit portion of Phase 1 is complete.

Before production replacement:
- Phase 2 must compare candidate dependent-data completion methods using the same predeclared metrics;
- Phase 3 must backtest the selected candidate on real Solar of Things telemetry;
- P5/P50/P95 must not be strengthened in Enel/SEC-facing language until those gates pass.

# Phase 1 mathematical validation protocol and analytical basis

Date: 2026-09-30

Status: **CANONICAL VALIDATION GATE BEFORE LOT 3**

This note strengthens the synthetic audit so later Enel/SEC-facing conclusions are based on:
1. direct production-code reference fixtures;
2. analytical mathematics;
3. repeated simulation;
4. explicit inferential criteria;
5. reproducible artifacts.

It does not modify the production statistical algorithm.

---

# 1. Quantity being calibrated

The current model produces a central 90% predictive completion interval:

- lower = P5;
- centre = P50;
- upper = P95.

For a controlled experiment whose hidden truth is known, define:

```
I_r = 1{ P5_r <= Y_r <= P95_r }
```

where `Y_r` is the true complete-period energy in repetition `r`.

Empirical coverage over `R` repetitions is:

```
p_hat = (1/R) * sum(I_r)
```

The nominal target is:

```
p0 = 0.90
```

This is the primary calibration statistic.

---

# 2. Coverage uncertainty and formal test

For every scenario cell:
- report empirical coverage `p_hat`;
- report a 95% Wilson confidence interval for the Bernoulli coverage probability;
- report the calibration gap in percentage points: `100*(p_hat - 0.90)`.

For formal undercoverage testing:

```
H0: p >= 0.90
H1: p < 0.90
```

Use a one-sided exact binomial test.

When a family of cells is evaluated together, control family-wise type-I error with the Holm step-down correction at alpha=0.05.

This prevents selecting isolated low-coverage cells merely because many scenarios were tested.

Statistical significance and practical effect size must both be reported. A tiny but statistically detectable deviation is not to be described as equivalent to severe undercoverage.

---

# 3. P50 point-estimate diagnostics

For each scenario cell report:
- mean P50 error: `mean(P50 - truth)`;
- MAE;
- RMSE;
- median absolute error relative to true hidden energy;
- 95% uncertainty interval for mean bias in the strengthened harness.

This separates central-estimate bias from interval-calibration failure.

---

# 4. Interval sharpness and proper interval score

Coverage alone is insufficient because an arbitrarily wide interval can cover nearly everything.

Record:
- P95-P5 width;
- width relative to true hidden energy.

For method comparison in Phase 2, add the central-interval score for alpha=0.10:

```
IS_0.10(L,U;y)
 = (U-L)
 + (2/0.10)*(L-y)*1{y<L}
 + (2/0.10)*(y-U)*1{y>U}
```

Lower interval score is better.

This proper score rewards narrow intervals when they cover the truth and strongly penalizes intervals that are too narrow and miss it.

Candidate methods must therefore be judged on calibration **and** sharpness, not coverage alone.

---

# 5. Analytical reason independent 5-minute resampling can understate uncertainty

Let the hidden 5-minute power/energy contributions be:

```
X_1, X_2, ..., X_k
```

Assume for explanation that:
- each has variance `sigma^2`;
- serial correlation is approximately AR(1):
  `Corr(X_i,X_j)=phi^|i-j|`.

The true variance of their sum is:

```
Var(sum X_i)
 = sigma^2 [
     k + 2 * sum_{h=1}^{k-1} (k-h) phi^h
   ]
```

If the completion algorithm resamples every 5-minute segment independently, it approximately uses:

```
Var_independent(sum X_i) = k * sigma^2
```

and omits the covariance term:

```
2 * sigma^2 * sum_{h=1}^{k-1} (k-h) phi^h
```

For positive temporal correlation, that omitted term is positive.

Therefore independent point-wise resampling produces a variance that is systematically too small whenever persistent serial dependence is material.

## Two-hour example

A two-hour gap at 5-minute cadence has `k=24` segments.

For `phi=0.55`:

```
true variance factor        = 77.2346 * sigma^2
independent variance factor = 24.0000 * sigma^2
independent / true variance = 0.3107
independent / true SD       = 0.5574
```

For `phi=0.93`:

```
true variance factor        = 348.6356 * sigma^2
independent variance factor = 24.0000 * sigma^2
independent / true variance = 0.06884
independent / true SD       = 0.2624
```

Thus in these stylized cases the current independence assumption can preserve only about:
- 56% of the correct standard deviation at phi=0.55;
- 26% at phi=0.93.

Under a normal approximation, a nominal central 90% interval built with those reduced standard deviations would have approximate true coverage:

```
coverage ~= 2*Phi(z_0.95 * SD_ratio) - 1
```

where `z_0.95 ~= 1.64485`.

That gives approximately:
- phi=0.55 -> **64.1%** predicted coverage;
- phi=0.93 -> **33.4%** predicted coverage.

Phase 1 synthetic results for a 2-hour gap were of the same order:
- moderate autocorrelation -> about 59.6%-61.7%;
- high autocorrelation -> about 28.3%-30.6%.

The simulation result is therefore supported by the covariance mathematics; it is not merely a numerical artifact.

The analytical calculation is an explanatory approximation, not a claim that the user's real telemetry follows an exact AR(1) process.

---

# 6. Finite donor-distribution uncertainty

Even if source values were independent, the donor distribution is estimated from a finite historical sample.

The current bootstrap conditions on that empirical donor distribution as if it were the true distribution.

Therefore two uncertainty layers exist:

1. variation of future/hidden values conditional on the estimated empirical donor distribution;
2. uncertainty because the donor distribution itself was estimated from finite data.

Ordinary point-wise empirical resampling primarily represents layer 1.

Observed undercoverage in some nominally independent synthetic cells, especially longer gaps, must therefore be investigated rather than automatically attributed to serial correlation.

This is a separate hypothesis from the autocorrelation mechanism.

---

# 7. Direct C# production-reference gate

Before Lot 3 is accepted as evidence, a direct .NET research fixture must execute the real:

- `EnergyRangeStatisticsService`;
- `UtilityGridImportStatisticalCompletionService`.

Required analytical fixtures:

1. complete constant 750 W day:
   - true energy = 18 kWh;
   - P5=P50=P95=18 kWh;
   - status COMPLETE_OBSERVATION.

2. constant 1 kW with a 15-minute endpoint separation:
   - missing nominal source samples exist;
   - continuity threshold = 15 min;
   - integration bridges the separation;
   - reported coverage = 100%;
   - no Monte Carlo completion is executed.

3. constant 1 kW with a 20-minute endpoint separation:
   - statistical gap is activated;
   - observed energy excludes 20 min;
   - 2,000 current simulations execute;
   - every donor is exactly 1 kW;
   - analytically every simulated completion equals 48.0 kWh;
   - therefore P5=P50=P95=48.0 kWh exactly.

This gate validates the production boundary and completion semantics without relying on the Python mirror.

---

# 8. Monte Carlo count is a separate numerical question

The audit retains 2,000 simulations because that is the current production baseline.

Increasing the number of Monte Carlo trials reduces random numerical error in estimated percentiles, but does not repair:
- omitted temporal covariance;
- donor-distribution misspecification;
- insufficient donor data;
- biased conditional structure.

Simulation-count convergence is therefore tested separately in Phase 2.

JCGM 101:2008 is retained as a methodological reference for Monte Carlo numerical adequacy. It states that required trial count depends on output-distribution shape and requested coverage probability and recommends large/adaptive trial counts rather than assuming one universal number.

---

# 9. Literature basis

Primary / methodological references:

- JCGM 101:2008, *Evaluation of measurement data — Supplement 1 to the GUM — Propagation of distributions using a Monte Carlo method*, DOI 10.59161/JCGM101-2008.
- Kreiss, J.-P. & Paparoditis, E. (2011), *Bootstrap methods for dependent data: A review*, Journal of the Korean Statistical Society 40(4), 357-378, DOI 10.1016/j.jkss.2011.08.009.
- Politis, D. N. & White, H. (2004), *Automatic Block-Length Selection for the Dependent Bootstrap*, Econometric Reviews 23(1), 53-70, DOI 10.1081/ETC-120028836.
- NIST/SEMATECH e-Handbook / Dataplot bootstrap documentation for percentile-bootstrap definitions and percentile extraction.

These references support the general methodological direction; they do not by themselves validate a specific Solar of Things model. Validation remains empirical and installation-specific in later phases.

---

# 10. Gate decision

**Do not proceed to interpret Lot 3 as validated evidence until the direct C# reference workflow is GREEN.**

Once GREEN:
- preserve Lot 1/2 as baseline evidence;
- proceed to Lot 3 with the strengthened metrics above;
- do not rerun all Lot 1/2 cells merely for repetition unless the C# gate reveals semantic disagreement;
- rerun only cells whose implementation assumptions are invalidated by that gate.


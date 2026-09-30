# Phase 2 discovery checkpoint and protocol amendment

Date: 2026-09-30

Status: **DISCOVERY COMPLETE — HOLDOUT NOT YET EXECUTED**

This document is intentionally explicit that one selection-rule defect was discovered *after* inspecting discovery results.

## 1. Discovery result that exposed a protocol defect

The original lexicographic rule counted statistically significant undercoverage failures before considering proper interval score.

That rule can reward a method for making intervals excessively wide:
- an over-wide interval rarely undercovers;
- but it is still a poor probabilistic forecast.

The unstratified long-block candidate C1-120 exposed exactly this failure mode.

Therefore the original Gate A/B ordering is withdrawn for final selection.

This is **not** treated as if it had been predeclared. The amendment is recorded after discovery and before untouched holdout execution.

## 2. Corrected selection principle

For holdout selection:

1. **Primary:** mean normalized alpha=0.10 interval score.
   - This is a proper score for the central predictive interval.
   - It penalizes unnecessary width and misses.

2. **Calibration diagnostics remain mandatory:**
   - empirical P5-P95 coverage;
   - Wilson 95% interval;
   - exact one-sided binomial undercoverage test;
   - Holm correction across declared holdout cells;
   - worst-case coverage.

3. **Point-estimate diagnostics:**
   - normalized P50 RMSE;
   - signed P50 bias.

4. A candidate is not allowed to advance merely because it has the best average interval score if holdout reveals a clear new structural failure.

The untouched holdout, not the amended discovery score, is the decisive anti-overfitting gate.

## 3. Candidate families discovered

C0:
- current independent 5-minute point-wise bootstrap.

C1:
- unstratified contiguous moving blocks.

C2:
- contiguous block bootstrap with +/-15-minute primary context and weekday/weekend stratification.

C3:
- local/seasonal blocks with exact 5-minute clock slot + same day-type primary matching, then deterministic widening.

C4:
- local/seasonal blocks with exact 5-minute clock slot across all days as the primary donor set, then deterministic time widening only if necessary.

C4 was added after C2/C3 stress tests showed that hard day-type stratification can leave too few independent donor days for persistent episode probabilities.

Literature motivation:
- local block bootstrap for slowly varying/nonstationary structure;
- seasonal block bootstrap for periodic series;
- dependent-data bootstrap principles.

## 4. Discovery aggregate

Across 33 declared discovery cells per configuration (24 continuous-process cells + 9 persistent-regime cells):

| Method | Corrected undercoverage failures | Worst coverage | Mean normalized interval score | Mean normalized P50 RMSE |
|---|---:|---:|---:|---:|
| C4-120 | 7 | 83.0% | **1.333** | 0.703 |
| C2-120 | 15 | 70.4% | 1.371 | 0.656 |
| C3-120 | 19 | 81.2% | 1.456 | 0.703 |
| C4-60 | 10 | 74.2% | 1.458 | 0.693 |
| C2-60 | 15 | 61.6% | 1.487 | 0.643 |
| C3-60 | 19 | 71.2% | 1.574 | 0.695 |
| C2-30 | 16 | 17.2% | 1.790 | 0.640 |
| C4-30 | 14 | 25.4% | 1.799 | 0.683 |
| C3-30 | 21 | 29.4% | 1.913 | 0.684 |
| C2-20 | 20 | 4.0% | 2.081 | 0.635 |
| C4-20 | 20 | 1.8% | 2.143 | 0.675 |
| C3-20 | 23 | 6.0% | 2.240 | 0.677 |
| C1-120 | 12 | 18.6% | 3.240 | 0.957 |
| C1-60 | 15 | 18.8% | 3.424 | 0.964 |
| C0 | 22 | 0.2% | 3.536 | **0.633** |
| C1-30 | 18 | 18.2% | 3.783 | 0.972 |
| C1-20 | 24 | 18.2% | 4.195 | 0.977 |

Interpretation:
- C4-120 currently gives the best interval forecast quality among tested candidates.
- C0 can have a competitive central P50 RMSE while having unusably miscalibrated uncertainty; this re-confirms that P50 accuracy alone is not enough.
- C1 demonstrates why coverage alone is not an adequate selection objective.

## 5. Effective-history sensitivity

A targeted full-block persistent-regime experiment varied independent donor-day count.

With exact-time full blocks, empirical coverage moved toward the nominal 90% as independent donor days increased:
- ~30–42 days: often mid/high-80s;
- 60–90 days: mostly high-80s/near-90;
- 120–180 days: generally near 89–90%.

This identifies **effective independent donor-day count** as an evidence-quality variable that must be exposed in Phase 3.

No hard production minimum donor-day threshold is fixed in Phase 2 discovery.

## 6. Frozen holdout candidates

The untouched holdout will compare exactly:

- C0 — current baseline;
- C1-120 — best long unstratified block benchmark;
- C2-120 — best original contextual block candidate;
- C3-120 — exact-slot + same-day-type local block candidate;
- C4-120 — exact-slot all-days seasonal/local block candidate.

No block length or matching rule may be retuned after holdout results are observed.

## 7. Holdout history

Holdout synthetic histories use **90 complete donor days** before masking.

Reason:
- discovery separately characterized small-history degradation;
- 90 days is large enough to test model structure without letting tiny empirical donor sets dominate every cell;
- Phase 3 will report the user's actual effective donor-day count and may STOP if evidence is insufficient.

## 8. Frozen holdout cells

Each holdout cell uses:
- 1,000 paired repetitions;
- 2,000 simulations per candidate;
- same hidden truth across candidates.

Declared cells:

1. AR phi=0.30:
   - one random gap duration from {30,60,120,240} min.

2. AR phi=0.70:
   - same duration rule.

3. AR phi=0.95:
   - same duration rule.

4. Day/night + weekend shift:
   - weekend baseline multiplier 1.35;
   - random gap duration from {30,60,120,240} min.

5. Smooth intrahour ramp:
   - deterministic clock structure containing strong within-hour slopes;
   - iid residual noise;
   - random gap from {20,30,60} min.

6. Persistent random episode:
   - episode start and duration vary by day;
   - duration from {20,30,60,90,120} min;
   - occurrence probability drawn per synthetic dataset from [0.2,0.8].

7. Mixed process:
   - day/night baseline;
   - AR phi=0.70 residual;
   - occasional persistent high-load episodes;
   - random gap from {60,120,240} min.

8. Multiple unequal gaps:
   - day/night + AR phi=0.70;
   - four gaps with durations {20,45,90,180} min at separated positions.

Holdout settings are frozen by this document.

## 9. Advancement interpretation

Phase 2 advances at most one candidate to Phase 3.

Advancement requires:
- materially better proper interval score than C0;
- materially improved calibration profile;
- no new holdout structural failure that makes the candidate unsuitable;
- no material systematic P50 bias.

If no candidate satisfies this, Phase 2 ends in redesign/STOP.


# Phase 2 — Candidate-method comparison protocol

Date: 2026-09-30

Status: **PREDECLARED BEFORE PHASE 2 NUMERICAL RESULTS**

Purpose:
- compare replacement candidates for `grid-import-empirical-bootstrap.v1`;
- preserve Phase 1 evidence rather than tune toward Enel;
- choose at most one candidate family to advance to real-data Phase 3;
- STOP if no candidate is adequately calibrated.

The utility bill / Enel value is excluded from all candidate construction, tuning, scoring and selection.

---

## 1. Candidate families

### C0 — current point-wise empirical bootstrap

Production behavioral baseline:
- each missing 5-minute segment is resampled independently;
- donor hierarchy uses same local hour + weekday/weekend context;
- 2,000 simulations.

Purpose:
- baseline only; not presumed acceptable.

### C1 — unstratified contiguous moving-block bootstrap

For each missing interval:
- sample contiguous observed blocks from eligible donor history;
- preserve original sequence inside each sampled block;
- stitch blocks until the missing interval is filled;
- no time-of-day matching beyond basic donor validity.

Purpose:
- isolate the value of preserving serial dependence without contextual time matching.

### C2 — context-stratified contiguous moving-block bootstrap

For each cursor position inside a missing interval:
- choose an observed contiguous block whose start clock-position is close to the target cursor;
- match weekday/weekend type;
- preserve the observed sequence inside the block;
- stitch additional blocks only when the gap exceeds the selected block length.

Clock-position rule for Phase 2 discovery:
- circular time-of-day distance <= 15 minutes;
- if insufficient blocks, widen deterministically to <= 30 minutes, then <= 60 minutes;
- only after those fallbacks may the method use same-hour context;
- donor blocks may not cross unavailable/source gaps.

This combines the two Phase 1 findings:
1. preserve contiguous dependence;
2. avoid whole-hour pooling when a finer time-position match is available.

The +/-15 minute primary context is a Phase 1-supported candidate setting, not yet a production conclusion.

---

## 2. Block-length discovery grid

No single block length is assumed optimal.

Phase 2 discovery compares fixed block lengths:
- 20 min (4 samples);
- 30 min (6 samples);
- 60 min (12 samples);
- 120 min (24 samples).

For a missing interval shorter than the candidate block length:
- use only the required prefix of the sampled contiguous block.

For a longer interval:
- stitch independently sampled contiguous blocks of the selected length;
- each new C2 block is context-matched to the target clock position at the new cursor.

No candidate may inspect the hidden truth when selecting a block.

Automatic block-length selection is **not** implemented in the first Phase 2 discovery pass. Literature-based selectors, including corrected Politis/White-type procedures, may be evaluated only after the fixed-length benchmark reveals whether such complexity is necessary.

---

## 3. Simulation count

All discovery and holdout comparisons use **2,000 simulated completions**, matching the current production baseline.

Reason:
- Phase 2 first compares statistical model structure;
- Monte Carlo trial-count convergence is a separate subtest;
- a better model must not win merely because it used more simulations.

After one candidate family/setting survives holdout validation, perform simulation-count convergence:
- 2,000;
- 5,000;
- 10,000;
- 20,000;
- 50,000;
- 100,000.

---

## 4. Discovery scenarios

Use controlled synthetic families already motivated/validated in Phase 1:
- stable iid-like;
- deterministic day/night + iid residual;
- moderate AR-style serial dependence;
- high AR-style serial dependence;
- persistent episode / appliance-like regime.

Gap families:
- 20 min;
- 30 min;
- 60 min;
- 120 min;
- 240 min;
- equal-total-duration fragmented topology where relevant.

The same underlying synthetic series and hidden intervals must be reused across candidate methods within each repetition to create paired comparisons.

---

## 5. Holdout scenarios

Holdout scenarios are declared before discovery results are inspected and are not used to choose block length.

Minimum holdout families:
- AR phi=0.30;
- AR phi=0.70;
- AR phi=0.95;
- day/night profile with a weekend level shift;
- smooth within-hour ramp with iid noise;
- persistent episodes with random 20–120 minute duration;
- mixed process combining day/night baseline + serial residual + occasional persistent episode;
- multiple separated gaps with unequal lengths.

Candidate settings are frozen before holdout execution.

---

## 6. Primary metrics

Replication counts are fixed before numerical discovery:
- discovery: **500 paired repetitions per declared cell**;
- holdout: **1,000 paired repetitions per declared cell**.

Rationale:
- discovery estimates are used only to freeze candidate settings;
- the larger untouched holdout is the stronger confirmation gate;
- all coverage results retain Wilson intervals and exact-binomial inference.

For C2, "insufficient contextual blocks" means fewer than **8 eligible contiguous donor starts**. Fallback widening is therefore deterministic: +/-15 min -> +/-30 min -> +/-60 min -> same hour -> all valid contiguous starts.

For every scenario/method cell:
- empirical P5-P95 coverage;
- 95% Wilson interval for coverage;
- one-sided exact binomial calibration test:
  - H0: p >= 0.90
  - H1: p < 0.90;
- Holm family-wise correction within the declared comparison family;
- P50 mean bias;
- P50 MAE;
- P50 RMSE;
- median P5-P95 width;
- width relative to true hidden energy;
- alpha=0.10 interval score;
- normalized interval score = interval score / max(true hidden kWh, epsilon).

Paired differences in normalized interval score use paired bootstrap confidence intervals because every candidate sees the same underlying repetition.

---

## 7. Predeclared selection rule

Selection is lexicographic, not subjective.

### Gate A — calibration failures
For each candidate configuration, count cells with:
- Holm-adjusted p < 0.05 for undercoverage.

Lower count is better.

### Gate B — worst-case calibration
Among configurations tied on Gate A:
- compare worst empirical undercoverage magnitude relative to 90%.

Smaller worst deficit is better.

### Gate C — probabilistic sharpness
Among configurations still tied:
- compare mean normalized alpha=0.10 interval score across discovery cells.

Lower is better.

### Gate D — P50 quality
If still tied:
- compare normalized P50 RMSE.

Lower is better.

A candidate that obtains good coverage merely by becoming extremely wide should be penalized by interval score.

---

## 8. Freeze before holdout

After discovery:
- choose at most the best C1 block setting and best C2 block setting under the predeclared rule;
- freeze those settings;
- do not retune them after seeing holdout results.

C0 baseline always proceeds to holdout for comparison.

---

## 9. Holdout advancement rule

A replacement candidate advances to Phase 3 only if, relative to C0:
- it materially reduces corrected undercoverage failures;
- it improves or preserves worst-case calibration;
- its normalized interval score is not materially worse overall;
- it does not introduce a material P50 bias pattern;
- no holdout failure reveals a new structural defect that invalidates the method.

If C1 and C2 both fail materially:
- Phase 2 ends in STOP / redesign rather than selecting a weak winner.

---

## 10. Literature basis

Dependent-data bootstrap principles:
- Kreiss, J.-P. & Paparoditis, E. (2011), *Bootstrap methods for dependent data: A review*, Journal of the Korean Statistical Society 40(4), 357–378. DOI: 10.1016/j.jkss.2011.08.009.
- Politis, D. N. & Romano, J. P. (1994), *The Stationary Bootstrap*, JASA 89(428), 1303–1313. DOI: 10.1080/01621459.1994.10476870.
- Politis, D. N. & White, H. (2004), *Automatic Block-Length Selection for the Dependent Bootstrap*, Econometric Reviews 23(1), 53–70. DOI: 10.1081/ETC-120028836.
- Patton, A., Politis, D. N. & White, H. (2009), correction to the above block-length-selection article. DOI: 10.1080/07474930802459016.

These sources support preserving dependence and treating block length as a statistical design problem. They do not select the Solar of Things production method by authority; empirical validation does.

---

## 11. Phase 2 non-goals

Phase 2 does not:
- use Enel values;
- change production C#;
- change PDF wording;
- create a Windows user build;
- claim validation on the user's real telemetry;
- model source-attribution uncertainty.

Those remain later phases.

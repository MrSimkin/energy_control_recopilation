# Phase 2 holdout report and candidate decision

Date: 2026-09-30

Status: **PHASE 2 COMPLETE — ONE PROVISIONAL CANDIDATE ADVANCES TO PHASE 3**

## 1. Holdout integrity

Holdout design was frozen before execution in:
- `PHASE2_DISCOVERY_CHECKPOINT_AND_AMENDMENT_2026-09-30.md`.

No candidate setting was retuned after holdout results were observed.

Each holdout family used:
- 1,000 paired repetitions;
- 2,000 simulated completions per stochastic candidate;
- the same hidden truth for all candidates;
- 90-day synthetic histories;
- Wilson 95% coverage intervals;
- exact one-sided binomial calibration tests;
- Holm family-wise correction.

## 2. Holdout candidates

- C0: current independent 5-minute point-wise bootstrap.
- C1-120: unstratified contiguous 120-minute moving blocks.
- C2-120: +/-15-minute contextual + weekday/weekend contiguous blocks, 120-minute maximum block.
- C3-120: exact-slot + same-day-type local/seasonal block candidate.
- C4-120: exact-slot all-days local/seasonal block candidate.

## 3. Aggregate holdout results

| Method | Holm undercoverage failures / 8 | Worst coverage | Mean coverage | Mean normalized interval score | Mean normalized P50 RMSE |
|---|---:|---:|---:|---:|---:|
| C3-120 | 4 | 74.1% | 86.15% | **1.029** | 0.388 |
| C4-120 | 2 | 76.5% | 87.05% | 1.038 | 0.391 |
| C2-120 | **1** | **86.9%** | **90.80%** | 1.059 | 0.399 |
| C0 | 7 | 35.6% | 63.84% | 1.822 | 0.453 |
| C1-120 | 5 | 74.1% | 84.64% | 2.429 | 1.019 |

C3/C4 have marginally better mean interval scores than C2, but they do so with identifiable structural failures:
- C3: intrahour ramp 74.1%, weekend shift 86.8%, mixed 86.9%, AR95 86.0%.
- C4: intrahour ramp 76.5%, weekend shift 84.8%.

Under the frozen post-discovery rule, a small average score advantage cannot override such contextual failures.

## 4. C2-120 holdout profile

Coverage by holdout family:

| Holdout family | C0 | C2-120 |
|---|---:|---:|
| AR phi=0.30 | 78.2% | **90.7%** |
| AR phi=0.70 | 54.1% | **89.9%** |
| AR phi=0.95 | 35.6% | **86.9%** |
| Weekend level shift | 89.9% | **94.0%** |
| Strong intrahour ramp | 75.8% | **95.3%** |
| Random persistent episode | 62.3% | **91.6%** |
| Mixed process | 57.2% | **88.9%** |
| Multiple unequal gaps | 57.6% | **89.1%** |

Only AR phi=0.95 remains a Holm-corrected undercoverage failure.

## 5. Proper-score improvement versus current production method

Paired, cell-stratified bootstrap differences in normalized interval score relative to C0:

- C1-120 minus C0: +0.606, 95% bootstrap CI [+0.531, +0.682] -> materially worse.
- C2-120 minus C0: **-0.764**, 95% CI **[-0.895, -0.646]** -> materially better.
- C3-120 minus C0: -0.793, 95% CI [-0.927, -0.674].
- C4-120 minus C0: -0.784, 95% CI [-0.914, -0.664].

Pairwise:
- C2 minus C3: +0.0297, 95% CI [+0.0181,+0.0411].
- C2 minus C4: +0.0203, 95% CI [+0.0047,+0.0376].

Thus C3/C4 are slightly sharper on average, but the difference is small relative to their structural calibration failures.

## 6. Extreme-AR diagnostic

After C2's AR95 holdout failure was observed, a diagnostic rerun decomposed that already-used holdout cell by gap duration.

This diagnostic is **not** treated as new validation evidence.

| Gap | C2-120 coverage |
|---:|---:|
| 30 min | 91.1% |
| 60 min | 87.6% |
| 120 min | 88.3% |
| 240 min | **80.3%** |

The strongest failure is concentrated in 240-minute gaps, where C2-120 must stitch two independent 120-minute blocks and therefore again loses cross-block dependence.

The shorter-gap results also show that extremely persistent AR structure remains a stress condition even without stitching.

## 7. Phase 2 decision

**Advance C2-120 to Phase 3 as the sole provisional candidate.**

Research label:
`grid-import-context-block-bootstrap-120m.candidate-v1`

This is **not** production approval.

Reason:
- it substantially improves proper predictive score over the production baseline;
- it reduces corrected undercoverage failures from 7/8 to 1/8;
- it survives weekend shifts, intrahour slopes, persistent episodes, mixed behavior and multiple unequal gaps;
- its remaining failure mode is explicit and testable against real telemetry.

## 8. Mandatory Phase 3 checks

Phase 3 must determine from real Solar of Things data:
- empirical autocorrelation / persistence over relevant lags;
- backtested P5-P95 coverage by gap duration;
- P50 bias/MAE/RMSE;
- interval score;
- effective donor evidence;
- behavior of 120+ minute gaps;
- behavior of 240-minute and longer gaps where block stitching may matter.

If real-data backtesting reproduces material undercoverage:
- candidate is rejected;
- return to Phase 2 redesign;
- do not weaken acceptance criteria.

## 9. What remains unchanged

No production C# statistical algorithm changed.
No PDF wording changed.
No Windows application build was created.
Enel values were not used in design, tuning, scoring or selection.

## 10. Methodological context

The result is consistent with the dependent-data bootstrap literature:
- contiguous blocks preserve serial dependence that iid resampling destroys;
- local/seasonal context can matter when stochastic structure varies with time;
- block length is a genuine statistical design parameter rather than a cosmetic simulation setting.

The candidate is selected by the project's controlled evidence, not merely because a published block-bootstrap method exists.

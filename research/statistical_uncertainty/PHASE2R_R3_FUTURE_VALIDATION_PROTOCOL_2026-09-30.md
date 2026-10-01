# Phase 2R — R3 boundary-residual predictive model / future validation protocol

Date: 2026-09-30

Status: **FROZEN BEFORE ANY FUTURE VALIDATION OUTCOME EXISTS**

Research label:
`grid-import-boundary-residual-dayweighted-240m.candidate-v4`

This candidate is selected from the already-consumed real corpus as **development only**.
Nothing in the existing 2026-04-20 through 2026-09-27 package is a pristine validation set for R3.

No Enel bill, meter reading, tariff or utility comparison value is used in construction, calibration, scoring or selection.

---

## 1. Motivation

R1 and R2 showed that a fixed correction on top of C2 is not temporally stable.

The strongest development signal is instead based on information genuinely observed around an internal telemetry gap:
- grid-import power immediately before the gap;
- grid-import power immediately after the gap;
- actual elapsed gap duration.

For bounded gaps <=240 minutes, trapezoidal interpolation between those two observed boundaries is a substantially more stable central estimate than raw C2 P50 in active-grid conditions.

R3 therefore models the **historical residual around the observed boundary bridge**, rather than trying to repair C2's center after the fact.

---

## 2. Applicability

R3 is eligible only when all are true:
- the gap is internal/bounded by an observed grid-import value at both ends;
- both boundary values are valid/confidence != UNRESOLVED;
- actual gap duration is >15 minutes and <=240 minutes;
- sufficient prior calibration history exists.

R3 must return an explicit insufficient/unvalidated status when:
- either boundary is absent;
- the individual gap exceeds 240 minutes;
- fewer than 10 distinct comparable calibration dates are available.

No validated P5/P50/P95 or calibrated 90% range may be fabricated for an ineligible gap.

This 240-minute limit is evidence-driven:
- current development and alternate-window stress are strong through 240 minutes;
- 480-minute active-gap behavior is sparse and unstable;
- the real installation contains gaps longer than 240 minutes, so this limitation must remain visible rather than hidden.

---

## 3. Boundary bridge

For a bounded gap:
- observed start power: `x0` W;
- observed end power: `x1` W;
- actual elapsed duration: `H` hours.

Boundary-bridge energy:

```
B = max(0, (x0 + x1)/2) * H / 1000   [kWh]
```

This is a deterministic trapezoidal interpolation using only observed boundary evidence.

It is not itself treated as certainty.

---

## 4. Historical residual calibration

For a fully observed historical comparison window:

```
e = (Y - B) / H   [kW]
```

where:
- `Y` is true time-aware trapezoidal grid-import energy over the complete window;
- `B` is the same boundary-bridge calculation;
- `H` is actual duration.

The residual rate captures curvature, temporary shutdown/startup, and within-gap dynamics not explained by the two boundaries.

### Observable start state

- INACTIVE: observed start boundary <=100 W;
- ACTIVE: observed start boundary >100 W.

The 100 W threshold is inherited from the existing application context rule and was fixed before R3.

### Duration groups

Development-selected groups, now frozen for future validation:

1. `G20_30`
   - benchmark calibration windows: 20 and 30 minutes;
   - operational gap range: >15 through 45 minutes.

2. `G60_120`
   - benchmark calibration windows: 60 and 120 minutes;
   - operational gap range: >45 through 180 minutes.

3. `G240`
   - benchmark calibration window: 240 minutes;
   - operational gap range: >180 through 240 minutes.

Future validation additionally tests 45, 90 and 180-minute interpolation/boundary cases so the operational ranges are not approved only at their training benchmark durations.

---

## 5. Rolling calibration window

For each target gap:
- use only historical calibration dates strictly earlier than the target local date;
- select the most recent **15 distinct local dates** containing at least one eligible calibration window in the same start-state and duration group;
- require at least **10 distinct dates**;
- never use target-day truth in its own calibration.

Every selected date receives equal total weight.

If date `d` contributes `n_d` calibration windows, each window receives weight:

```
w_i = 1 / (N_days * n_d)
```

This prevents a high-density day from masquerading as many independent days.

---

## 6. Single-gap predictive distribution

Within the weighted historical residual-rate sample, calculate weighted empirical:
- q05;
- q50;
- q95.

Candidate outputs:

```
P5  = max(0, B + q05 * H)
P50 = max(0, B + q50 * H)
P95 = max(P5, B + q95 * H)
```

These are empirical predictive quantiles conditional on:
- observed boundaries;
- start state;
- duration group;
- rolling prior-day evidence.

They are not meter calibration/tolerance bounds.

For one gap, the weighted empirical quantiles are calculated directly; Monte Carlo is not required merely to approximate a quantile that can be computed exactly.

---

## 7. Multiple bounded gaps in one report period

If every individual gap is R3-eligible:
- each gap is represented by its boundary bridge plus a sampled residual rate from its own weighted historical empirical distribution;
- residual draws are independent **across distinct gaps**, because dependence within each gap is already represented by the whole-gap residual rather than independent five-minute draws;
- use **100,000 report-level simulations** for the total missing-energy contribution.

This independence is a candidate assumption, not a theorem.

It is allowed into future validation because development stress tests on multiple separated <=120-minute gaps did not show material undercoverage, and same-day bridge-residual correlations were generally low after conditioning on the observed boundaries.

Future validation must include multiple-gap totals. If aggregate undercoverage appears, this assumption is rejected.

If any individual gap is >240 minutes or otherwise ineligible:
- do not publish a fully calibrated total P5/P50/P95 for the period;
- report observed energy and the evidence insufficiency explicitly.

---

## 8. Future validation start and stopping rule

Future validation starts with local date:
- **2026-09-28**.

The current owner package ends 2026-09-27 and may be used only as historical calibration/development evidence.

The validation period ends on the first future local date for which all of the following **outcome-independent evidence-count conditions** are satisfied:

1. at least **15 distinct future local dates** contain at least one eligible ACTIVE-start single-gap validation target;
2. at least **100 ACTIVE-start single-gap validation cases** exist in total;
3. each main duration group `G20_30`, `G60_120`, and `G240` has at least **10 ACTIVE-start validation cases**.

The stopping rule uses only timestamps, observed boundary state and target eligibility.
It does not inspect coverage, errors, Enel values or whether R3 is passing.

If those evidence conditions are not yet met, validation remains INSUFFICIENT and more future telemetry is required.

---

## 9. Future target construction

Use the same high-quality truth requirements as canonical Phase 3 v2:
- local date >=280 grid-import samples;
- complete target remains within one local date;
- every truth link between 4.5 and 5.5 minutes;
- finite/non-unresolved values;
- target selection timestamp-based, never value-based.

Future single-gap nominal durations:
- 20;
- 30;
- 45;
- 60;
- 90;
- 120;
- 180;
- 240 minutes.

For each local date/duration/time band:
- select the earliest eligible target.

All calibration evidence for a target must be strictly prior to that target local date.

---

## 10. Future multiple-gap validation

Minimum frozen scenarios where targets exist:
- four separated 60-minute gaps;
- two separated 120-minute gaps;
- mixed 20 + 30 + 60 + 120-minute gaps.

All component gaps must be individually bounded and <=240 minutes.

Truth is the sum of the known hidden component energies.
Predictive total uses the frozen 100,000-draw aggregation rule.

---

## 11. Validation metrics

Single gap and multiple gap:
- empirical P5-P95 coverage;
- 95% bootstrap interval clustered by local date;
- P50 signed bias;
- P50 MAE;
- P50 RMSE;
- interval width;
- alpha=0.10 interval score;
- calibration-day counts;
- insufficiency rate.

Report strata:
- ACTIVE / INACTIVE observed start;
- duration / duration group;
- weekday/weekend;
- local time band.

C2 remains a research comparator on the same future cases; it is not the production fallback for R3 failure.

---

## 12. Frozen advancement gates

R3 may advance toward implementation only if all are true on future data:

1. eligible single-gap overall coverage >=85%;
2. ACTIVE-start pooled coverage >=85%;
3. INACTIVE-start pooled coverage >=85%;
4. no main duration group with >=10 cases has point coverage <80%;
5. no primary ACTIVE/INACTIVE group has a clustered 95% coverage upper bound below 90%;
6. mean alpha=0.10 interval score is <= raw C2 on the same eligible cases;
7. ACTIVE-start absolute P50 signed bias is lower than raw C2;
8. multiple-gap pooled coverage >=85% where at least 10 multi-gap validation cases exist;
9. calibration insufficiency among otherwise eligible <=240-minute gaps is <=5%;
10. no new structural failure is identified.

Small cells are reported as uncertain; they are not converted into artificial PASS/FAIL claims.

Failure of any material gate returns the method to research/redesign.
Acceptance criteria must not be weakened after future outcomes are viewed.

---

## 13. Development evidence already consumed

R3 architecture was selected using the current package only as development evidence.

Development diagnostics included:
- causal/prequential earliest-window evaluation;
- alternate latest-window stress on different windows within the same dates;
- 10/15/20 rolling-history sensitivity;
- multi-gap stress;
- examination of bridge-residual dependence.

These results may motivate R3 but are **not final validation evidence**.

---

## 14. Reporting semantics if R3 later passes

For an eligible gap, P5/P50/P95 describe the weighted empirical predictive distribution of boundary-bridge residuals.

The report must still explain in plain language:
- what was observed;
- what was estimated;
- why a range is used;
- what P5/P50/P95 mean;
- how many distinct historical dates support the estimate;
- that Enel was not used to construct it.

For >240-minute or unbounded gaps:
- explicitly state that the validated statistical method does not cover that gap;
- do not silently extrapolate.

No production C# or PDF wording is authorized until this future validation gate passes.

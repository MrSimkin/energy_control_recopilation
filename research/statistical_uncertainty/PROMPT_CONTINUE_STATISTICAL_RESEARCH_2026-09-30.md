# Prompt — Continue statistical uncertainty research from canonical R3 future-validation gate

Use this prompt in a new chat/session.

---

Continue the **Solar of Things statistical-uncertainty / Enel-SEC reporting research** in repository:

`MrSimkin/energy_control_recopilation`

## Mandatory authority / recovery rule

Do **not** trust chat memory, prior summaries or this prompt as sole authority.

Before acting, recover and verify the current repository state from `main`.

Read, at minimum:

1. `solar_of_things_windows_app/CONTINUITY_STATUS.md`
2. `solar_of_things_windows_app/STATISTICAL_UNCERTAINTY_DEVELOPMENT_PLAN_2026-09-30.md`
3. `research/statistical_uncertainty/STATISTICAL_RESEARCH_CONSOLIDATED_HANDOFF_2026-09-30.md`
4. `research/statistical_uncertainty/PHASE3_V2_RERUN_REPORT_2026-09-30.md`
5. `research/statistical_uncertainty/PHASE2R_R2_HOLDOUT_REPORT_2026-09-30.md`
6. `research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`

Verify:
- canonical branch;
- exact current HEAD;
- last relevant commits;
- whether any newer statistical artifacts supersede these references;
- whether any operation that was believed complete is actually partial;
- whether a newer Research Exporter package has been supplied.

If HEAD has materially advanced, reconcile against newer canonical evidence before proceeding.

Do not duplicate commits, builds, publications or analysis already present.

---

## Owner requirements

The owner requires the statistical work to be:
- mathematically firm;
- demonstrable;
- reproducible;
- presentable to a third party;
- validated as much as necessary before strengthening report claims.

Do not assume a reader understands P5/P50/P95.

Any third-party report using P5/P50/P95 must immediately explain:
- what was done;
- why a range is used;
- what the range is for;
- what P5/P50/P95 mean;
- that P5-P95 represents the central 90% under the stated model.

Do not call these percentiles “tests” unless discussing an actual statistical hypothesis test.

Never tune toward Enel:
- Enel bill;
- utility meter;
- tariffs;
- any external utility target

must not be used to construct, calibrate, select or score the statistical model.

Keep separate:
- temporal uncertainty in total grid import;
- source-attribution uncertainty / `UnattributedHouseKwh`.

Never double count them.

Do not create a user-facing Solar of Things build merely for research phases.

---

## Historical production baseline

Build 494 remains the frozen historical app baseline.

Current production statistical model remains:
- `grid-import-empirical-bootstrap.v1`;
- independent point-wise 5-minute resampling;
- 2,000 simulations.

No replacement statistical model is production-approved.

No PDF uncertainty language has been strengthened based on unvalidated research.

---

## Completed research — do not repeat

### Phase 1 — COMPLETE

Current bootstrap was audited synthetically, analytically and against direct C# fixtures.

Key result:
- P50 can look reasonable while P5-P95 severely undercovers under temporal dependence.

Other established issues:
- independent 5-minute sampling drops covariance;
- whole-hour donor bins can be too coarse;
- effective independent donor-day count matters;
- persistent episodes create pseudo-replication;
- short missing-source episodes can be bridged and still report 100% integration coverage.

### Phase 2 — COMPLETE

Candidate comparison selected C2-120 for real-data Phase 3:

`grid-import-context-block-bootstrap-120m.candidate-v1`

C2 strongly outperformed C0 synthetically but retained an extreme-persistence / long-gap weakness.

Simulation-count convergence has been repaired/completed.

100,000 remains preferred where final report-level simulation is necessary and operationally acceptable.

### Owner real research package

Historical package:
- `SolarOfThings-ResearchPackage-20260930-214934.zip`
- SHA-256:
  `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`

Coverage:
- 2026-04-20 through 2026-09-27.

This historical package is fully consumed for model development/evaluation after R2.

Do **not** treat it as pristine R3 validation.

### Phase 3 canonical v2 — COMPLETE

Canonical harness:
- `research/statistical_uncertainty/phase3_canonical_v2_backtest.py`
- harness version `phase3-real-backtest.v2.1`

Canonical rerun:
- `PHASE3_V2_RERUN_REPORT_2026-09-30.md`

Per-case output:
- 1,242 targets;
- SHA-256:
  `726ae954afeb996bece4e3f30c4d9fc6f8275a4783691393cba0390dbb64f03f`.

C2 rejection was confirmed reproducibly.

Observable ACTIVE-start C2 coverage remained materially low and P50 bias negative.

Do not repeat the historical Phase 3 backtest.

### Phase 2R R1 — REJECTED

R1 whole-gap boundary-state bootstrap failed development.

Its locked holdout was never inspected.

Do not inspect/reuse it now.

### Phase 2R R2 — REJECTED

R2 grouped/day-cluster calibrated C2:
- passed cross-fitted development;
- parameters frozen;
- failed locked holdout.

Decisive failures:
- interval-score ratio vs raw C2 = 1.2187, required <=1.10;
- active-start P50 bias overshot from raw C2 -0.6513 kWh to R2 +0.8990 kWh.

Do not:
- retune R2;
- reuse its holdout to validate a later candidate;
- weaken gates because coverage was high.

---

## ACTIVE STATE — R3

Current frozen candidate:

`grid-import-boundary-residual-dayweighted-240m.candidate-v4`

Canonical protocol:

`research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`

At the last verified pre-handoff state, the protocol was frozen at commit:

`13d1c307cfbebba7fb0e6b794c3a42e42303f86e`

But verify current HEAD before relying on that SHA.

R3 architecture:

For a bounded internal gap with observed start/end grid-import power:
- start `x0`;
- end `x1`;
- actual gap duration `H`.

Boundary bridge:

```
B = max(0,(x0+x1)/2) * H / 1000
```

Historical residual rate:

```
e = (Y-B)/H
```

Condition on:
- ACTIVE start >100 W;
- INACTIVE start <=100 W;
- frozen duration group.

Groups:
- G20_30: operational >15–45 min;
- G60_120: >45–180 min;
- G240: >180–240 min.

Calibration:
- only dates strictly prior to target local date;
- most recent 15 distinct eligible local dates;
- minimum 10 dates;
- equal total weight per date.

Single-gap output:
- weighted empirical q05/q50/q95 residual rates;
- no Monte Carlo merely to estimate these directly computable quantiles.

Eligible R3 gap:
- internal/bounded;
- both boundaries valid/non-unresolved;
- >15 and <=240 minutes;
- sufficient prior dates.

Ineligible:
- unbounded;
- >240 min;
- insufficient prior calibration evidence.

For ineligible:
- return explicit insufficient/unvalidated status;
- do not fabricate P5/P50/P95.

Multiple eligible gaps in one report:
- draw each gap residual from its weighted empirical residual distribution;
- 100,000 report-level simulations;
- independence across distinct gaps is a candidate assumption that must itself be future-validated.

---

## CRITICAL VALIDATION STATUS

R3 is **NOT validated**.

The historical owner package ends 2026-09-27.

R3 future validation begins 2026-09-28.

Therefore the existing package contains zero pristine future-validation outcomes.

The existing corpus may be used only as historical calibration/development evidence.

Future validation stopping conditions are outcome-independent.

Do not score future outcomes until there is enough future evidence:

1. >=15 distinct future local dates with at least one eligible ACTIVE-start validation target;
2. >=100 ACTIVE-start single-gap validation cases total;
3. each main duration group G20_30, G60_120, G240 has >=10 ACTIVE-start validation cases.

Future target durations:
- 20;
- 30;
- 45;
- 60;
- 90;
- 120;
- 180;
- 240 minutes.

Future multiple-gap tests:
- 4x60;
- 2x120;
- 20+30+60+120.

The stop rule may inspect only:
- timestamps;
- observed boundary state;
- eligibility.

It must not inspect:
- whether truth falls in interval;
- prediction error;
- coverage;
- Enel values;
- whether R3 would pass.

If evidence counts are insufficient:
- record R3 validation = INSUFFICIENT;
- do not score;
- require more future telemetry.

---

## Frozen R3 advancement gates

Do not change these after viewing future outcomes.

R3 may advance only if all material gates pass:

1. eligible single-gap overall coverage >=85%;
2. ACTIVE-start pooled coverage >=85%;
3. INACTIVE-start pooled coverage >=85%;
4. no main duration group with >=10 cases has point coverage <80%;
5. no primary ACTIVE/INACTIVE group has clustered 95% coverage upper bound below 90%;
6. mean alpha=.10 interval score <= raw C2 on same eligible cases;
7. ACTIVE-start absolute P50 signed bias < raw C2;
8. multiple-gap pooled coverage >=85% when >=10 multi-gap cases exist;
9. calibration insufficiency <=5% among otherwise eligible <=240m gaps;
10. no new structural defect.

Failure returns to research/redesign.

Acceptance gates must not be weakened.

---

## Exact next step

First verify whether the user has supplied a **new Research Exporter ZIP with telemetry extending beyond 2026-09-27**.

If not:
- do not re-run old Phase 3/R1/R2;
- do not pretend R3 can be validated;
- explain that R3 is waiting for genuinely future evidence;
- if appropriate, use the existing green Research Exporter R1 to obtain a newer package.

If a newer package exists:
1. validate package SHA-256 / manifest / file hashes;
2. confirm it contains post-2026-09-27 telemetry;
3. construct frozen R3 future targets;
4. count outcome-independent eligibility only;
5. compare those counts to the frozen stopping rule;
6. if insufficient: record INSUFFICIENT and stop scoring;
7. if sufficient: execute the frozen future validation exactly once;
8. persist full per-case evidence, aggregates, clustered uncertainty, metadata and decision before making any production recommendation.

Do not implement production statistical code or change PDF wording before R3 future validation passes.

---

## Interrupted-task reconciliation

Before this handoff, the thread appeared to be mid-way through implementing the canonical Phase 3 v2 harness.

Repository inspection proved that task later completed:
- harness committed;
- rerun completed;
- R2 completed/rejected;
- R3 frozen.

Therefore the continuation point is **not** “implement Phase 3 v2 harness”.

It is:

**R3 future validation gate, awaiting genuinely future telemetry after 2026-09-27.**


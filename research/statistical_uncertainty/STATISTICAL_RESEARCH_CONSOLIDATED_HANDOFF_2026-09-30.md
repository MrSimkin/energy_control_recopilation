# Statistical uncertainty research — consolidated handoff

Date: 2026-09-30
Status: **CANONICAL HANDOFF — ACTIVE STATE = PHASE 2R / R3 FUTURE VALIDATION PENDING**

This document consolidates the complete statistical-uncertainty work completed before the latest interruption.

Repository is the authority.

---

# 1. Repository / branch / HEAD

Repository:
- `MrSimkin/energy_control_recopilation`

Canonical branch:
- `main`

Verified HEAD before this handoff:
- `13d1c307cfbebba7fb0e6b794c3a42e42303f86e`

Latest canonical commit:
- `Freeze R3 boundary-residual future validation protocol`

No partial/duplicate operation was found after this HEAD.

---

# 2. Owner requirements that remain binding

## 2.1 Mathematical / evidentiary standard

The owner requires the statistical work to be:
- mathematically firm;
- reproducible;
- demonstrable;
- presentable to a third party;
- validated as much as necessary before strengthening Enel/SEC-facing claims.

Do not accept a model because it merely produces visually plausible ranges or a desirable comparison with Enel.

## 2.2 Reader explanation

Do not assume a reader knows P5/P50/P95.

Whenever these quantities appear in a third-party report, immediately explain:
- what was done;
- why a range was required;
- what the range is for;
- what P5, P50 and P95 mean;
- that P5-P95 is the central 90% under the stated model.

Do not call P5/P50/P95 “tests” unless an actual hypothesis test is being discussed.

## 2.3 Monte Carlo count

Simulation count must be parameterizable.

The convergence study required:
- 2,000;
- 5,000;
- 10,000;
- 20,000;
- 50,000;
- 100,000.

That gate has now been repaired/completed.

100,000 remains the preferred report-level count where simulation is still necessary and runtime is acceptable.

More simulations reduce numerical Monte Carlo error; they do not repair structural model misspecification.

## 2.4 No Enel tuning

Never use:
- Enel billed kWh;
- meter reading;
- tariff;
- any external utility target

to construct, tune, select, calibrate or score the Solar statistical distribution.

External utility evidence is compared only after the Solar distribution exists.

## 2.5 Distinct uncertainty problems

Keep separate:
- temporal uncertainty in **total grid import**;
- source-attribution uncertainty inside observed household load.

`UnattributedHouseKwh` is not missing grid-import energy.

Never add/subtract unattributed house energy directly to/from total-grid P5/P50/P95 without a validated joint model proving no double counting.

## 2.6 Build / UX constraints

Research/design phases do not justify an isolated Windows app build.

The accepted long-operation UX standard is Reports > Export PDF:
- progress directly below action;
- visible bar;
- `Paso n/X`;
- current-stage text;
- completion ping;
- exact final output path;
- visible failure state.

Settings must eventually provide a persistent default export folder.

---

# 3. Frozen baseline / production state

Historical app baseline:
- Build 494;
- code commit `a0eea98e513f1ce822073c0c02a4f2a104ffc9a3`;
- workflow `36517130642`;
- artifact `11011373590`;
- CI/smoke/publish GREEN.

Production statistical method remains unchanged:
- `grid-import-empirical-bootstrap.v1`;
- 2,000 simulations;
- independent five-minute point-wise resampling;
- no production statistical replacement has been approved.

No PDF statistical language has been strengthened based on unvalidated research.

---

# 4. Phase 1 — COMPLETE

Purpose:
- audit current point-wise bootstrap mathematically and empirically.

Canonical evidence includes:
- `PHASE1_LOT1_RESULTS_2026-09-30.md`;
- `PHASE1_LOT2_REPORT_2026-09-30.md`;
- `PHASE1_LOT2_INFERENTIAL_VALIDATION_2026-09-30.csv`;
- `PHASE1_LOT3_REPORT_2026-09-30.md`;
- `PHASE1_LOT4_DONOR_RESOLUTION_REPORT_2026-09-30.md`;
- `PHASE1_LOT5_PERSISTENT_REGIME_REPORT_2026-09-30.md`;
- `PHASE1_MATHEMATICAL_VALIDATION_PROTOCOL_2026-09-30.md`;
- `PHASE1_VALIDATION_CHECKPOINT_2026-09-30.md`.

Direct production-code reference:
- workflow run `36781580855`;
- job `110112872942`;
- SUCCESS.

Main conclusions:
- P50 can remain approximately centered while P5-P95 badly undercovers;
- independent 5-minute resampling destroys positive covariance;
- whole-hour donor matching can be too coarse for intrahour ramps;
- finite effective donor-day count matters;
- persistent whole-episode ON/OFF regimes create exact pseudo-replication counterexamples;
- 10–15 minute missing-source episodes can be bridged and still show 100% integration coverage, which is not the same as source-sample completeness.

---

# 5. Phase 2 — COMPLETE

Synthetic candidate comparison.

Candidates included:
- C0 current point-wise baseline;
- C1 unstratified contiguous blocks;
- C2 contextual contiguous blocks;
- C3/C4 local/seasonal variants.

Frozen holdout selected:
- **C2-120**
- label `grid-import-context-block-bootstrap-120m.candidate-v1`.

Holdout summary:
- C2 corrected undercoverage failures: 1/8;
- C0: 7/8;
- C2 mean normalized interval score: 1.059;
- C0: 1.822.

Known synthetic weakness:
- extreme persistence / long gaps;
- AR phi=.95 240-minute diagnostic around 80.3% coverage because of independent 120-minute block stitching.

Canonical evidence:
- `PHASE2_CANDIDATE_COMPARISON_PROTOCOL_2026-09-30.md`;
- `PHASE2_DISCOVERY_CHECKPOINT_AND_AMENDMENT_2026-09-30.md`;
- `PHASE2_HOLDOUT_REPORT_2026-09-30.md`;
- `PHASE2_HOLDOUT_RESULTS_2026-09-30.csv`;
- `PHASE2_AR095_DIAGNOSTIC_2026-09-30.csv`.

Simulation-count convergence gate was later repaired:
- `PHASE2_SIMULATION_COUNT_CONVERGENCE_2026-09-30.md`.

---

# 6. Owner research package / real data

Owner package:
- `SolarOfThings-ResearchPackage-20260930-214934.zip`;
- SHA-256:
  `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`.

Integrity:
- manifest PASS;
- all declared files present;
- all file hashes and lengths PASS.

Package telemetry:
- 2026-04-20 through 2026-09-27;
- source schema 13;
- timezone America/Santiago;
- 218,705 normalized rows;
- 43,741 confirmed `grid_import_power_w` samples;
- 140/161 local dates with >=280 grid samples.

Real grid persistence is strong.

Residual approximate correlation after local clock-slot/day-type mean removal:
- 5m ~0.955;
- 30m ~0.803;
- 60m ~0.659;
- 120m ~0.449.

The package intentionally excludes Enel bills/readings/tariffs and identifying/raw payload material.

Research Exporter R1:
- green run `36786984422`;
- artifact `11130595515`;
- source commit `716b87808f5183692de237205cc2a67c0b44d9c5`.

---

# 7. Phase 3 original + canonical v2 — COMPLETE / C2 REJECTED

The original Phase 3 real-data analysis rejected C2, but its exact harness was initially not persisted.

A reproducibility correction was therefore made.

Canonical v2 protocol:
- `PHASE3_CANONICAL_REPRODUCIBLE_RERUN_PROTOCOL_2026-09-30.md`.

Committed harness:
- `phase3_canonical_v2_backtest.py`;
- version `phase3-real-backtest.v2.1`;
- harness commit `6bddba0325057bcff1bcc85e0b49e12bc6bdf9aa`.

Target counts reproduce exactly:
- 20m: 242;
- 30m: 242;
- 60m: 241;
- 120m: 241;
- 240m: 192;
- 480m: 84.

Canonical per-case output:
- 1,242 targets;
- SHA-256:
  `726ae954afeb996bece4e3f30c4d9fc6f8275a4783691393cba0390dbb64f03f`.

Canonical report:
- `PHASE3_V2_RERUN_REPORT_2026-09-30.md`.

Overall C2 coverage is high at <=240m, but observable active-start behavior is materially deficient.

Active-start (>100 W) C2 coverage:
- 20m: 80.0%;
- 30m: 78.0%;
- 60m: 70.0%;
- 120m: 78.4%;
- 240m: 81.1%;
- 480m: 68.4%.

P50 bias is consistently negative and worsens with duration.

Conclusion:
- **C2-120 rejection is reproducibly confirmed**;
- no production approval;
- return to Phase 2R.

---

# 8. Phase 2R R1 — REJECTED BEFORE HOLDOUT

Protocol:
- `PHASE2R_REAL_DATA_REDESIGN_PROTOCOL_2026-09-30.md`.

R1:
- `grid-import-boundary-state-whole-gap-day-bootstrap.candidate-v2`.

Design:
- whole-gap contiguous donor blocks;
- start/end active-state transition;
- one retained block per donor day;
- rolling prior-only 90-day history;
- >=20 donor days.

Development aggregate looked close to 90%, but active-start conditional coverage remained roughly 51–59% for 20–240m.

Decision:
- R1 failed development;
- locked holdout was **not inspected**;
- R1 rejected.

Canonical report:
- `PHASE2R_R1_DEVELOPMENT_REPORT_2026-09-30.md`.

---

# 9. Phase 2R R2 — DEVELOPMENT PASS, LOCKED HOLDOUT FAIL, REJECTED

R2:
- `grid-import-c2-grouped-day-calibrated.candidate-v3`.

Frozen protocol:
- `PHASE2R_R2_GROUPED_DAY_CALIBRATION_PROTOCOL_2026-09-30.md`.

Base:
- canonical Phase 3 v2 per-case C2 output.

Four groups:
- inactive-short;
- active-short;
- inactive-long;
- active-long.

Calibration:
- day-level location correction;
- day-clustered conformal-style interval widening;
- leave-one-local-day-out development evaluation.

Development:
- PASS all 7 gates.

Frozen parameters:
- inactive-short: b=0, q=0;
- active-short: b=1.377832 kW, q=1.340016 kW;
- inactive-long: b=0, q=0.043215 kW;
- active-long: b=0.510132 kW, q=0.626787 kW.

Holdout:
- 2026-09-13 through 2026-09-26;
- parameters frozen before inspection.

Holdout coverage was very high, but candidate failed decisively:

1. interval-score ratio vs raw C2:
   - 1.2187;
   - required <=1.10;
   - FAIL.

2. active-start P50 signed bias:
   - raw C2: -0.6513 kWh;
   - R2: +0.8990 kWh;
   - absolute bias worsened ~38%;
   - FAIL.

Decision:
- **R2 rejected**;
- do not retune on this holdout;
- existing corpus is now consumed for post-R2 candidate development;
- any later candidate requires genuinely future validation.

Canonical evidence:
- `PHASE2R_R2_DEVELOPMENT_REPORT_2026-09-30.md`;
- `PHASE2R_R2_FROZEN_PARAMS_2026-09-30.json`;
- `PHASE2R_R2_HOLDOUT_REPORT_2026-09-30.md`;
- `PHASE2R_R2_HOLDOUT_GATE_2026-09-30.json`.

---

# 10. Active state — Phase 2R R3

Frozen protocol:
- `research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`.

Research label:
- `grid-import-boundary-residual-dayweighted-240m.candidate-v4`.

Status:
- **FROZEN BEFORE ANY FUTURE VALIDATION OUTCOME EXISTS**.

R3 is a development-selected candidate from the consumed historical corpus.

It is not validated and is not production-approved.

## R3 model

Applicable only to:
- internal gaps bounded by valid observed grid-import values on both ends;
- actual duration >15 and <=240 minutes;
- sufficient prior calibration history.

Boundary bridge:

```
B = max(0,(x0+x1)/2) * H / 1000
```

Historical residual rate:

```
e = (Y-B)/H
```

Conditioning:
- observed start state ACTIVE >100 W / INACTIVE <=100 W;
- duration group.

Frozen duration groups:
- G20_30: benchmark 20/30m; operational >15–45m;
- G60_120: benchmark 60/120m; operational >45–180m;
- G240: benchmark 240m; operational >180–240m.

Rolling calibration:
- most recent 15 distinct eligible prior local dates;
- minimum 10 distinct dates;
- equal total weight per date.

Single-gap outputs:
- weighted empirical q05/q50/q95 of residual rate;
- no Monte Carlo needed for a single gap.

Multiple-gap report total:
- each eligible gap has its own residual distribution;
- 100,000 report-level simulations;
- independent residual draws across distinct gaps is a candidate assumption that must be future-validated.

Ineligible:
- unbounded gap;
- >240m;
- insufficient calibration days.

For ineligible gaps:
- do not fabricate a calibrated P5/P50/P95;
- show explicit insufficient/unvalidated evidence status.

---

# 11. R3 future validation — mandatory / not yet possible from current package

Future validation begins:
- local date **2026-09-28**.

The owner package ends:
- **2026-09-27**.

Therefore the current package contains **zero pristine future-validation outcomes for R3**.

Existing corpus may be used only as development/calibration history.

Validation must continue until outcome-independent evidence conditions are met:

1. >=15 distinct future local dates with at least one eligible ACTIVE-start target;
2. >=100 ACTIVE-start single-gap validation cases;
3. >=10 ACTIVE-start validation cases in each of G20_30, G60_120, G240.

Stop rule may inspect only:
- timestamps;
- observed boundary state;
- target eligibility.

It must not inspect:
- coverage outcome;
- model error;
- Enel values;
- pass/fail status.

Future target durations:
- 20, 30, 45, 60, 90, 120, 180, 240 minutes.

Future multiple-gap scenarios:
- 4×60m;
- 2×120m;
- 20+30+60+120m.

Frozen advancement gates are defined in the R3 protocol and must not be changed after outcomes are viewed.

---

# 12. Current real decision boundary

Do not:
- implement R3 in production;
- modify PDF uncertainty wording;
- retune R2;
- reuse R2 holdout as R3 validation;
- call existing historical data “validation” for R3;
- use Enel to select or calibrate R3;
- extrapolate R3 above 240 minutes;
- weaken future gates.

Current next step:
- obtain a **new Research Exporter package containing telemetry after 2026-09-27**;
- validate package integrity;
- count future R3-eligible evidence **without scoring outcomes**;
- if frozen evidence-count stop conditions are not met, record INSUFFICIENT and wait for more future telemetry;
- only once conditions are satisfied, execute the frozen R3 future-validation scoring exactly once.

No new user-facing Solar of Things build is needed for this statistical gate; Research Exporter R1 remains the extraction tool unless a concrete schema/export deficiency is discovered.

---

# 13. Work interrupted immediately before this handoff

The prior thread was originally about to implement the canonical Phase 3 v2 harness.

Repository verification showed that this work **did complete after the apparent interruption**:
- v2 harness exists;
- canonical rerun exists;
- R2 development/holdout exists;
- R2 was rejected;
- R3 protocol was subsequently frozen.

Therefore:
- do not repeat v2;
- do not repeat R2;
- do not rerun old holdouts;
- resume from R3 future-validation gate only.

There is no known partial commit, build, publication or pending duplicate operation at verified HEAD `13d1c307...`.

---

# 14. Canonical references to read on resume

Minimum:
1. `solar_of_things_windows_app/CONTINUITY_STATUS.md`;
2. `solar_of_things_windows_app/STATISTICAL_UNCERTAINTY_DEVELOPMENT_PLAN_2026-09-30.md`;
3. `research/statistical_uncertainty/PHASE3_V2_RERUN_REPORT_2026-09-30.md`;
4. `research/statistical_uncertainty/PHASE2R_R2_HOLDOUT_REPORT_2026-09-30.md`;
5. `research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`;
6. this handoff.

For any production/build work, additionally read:
- `PRE_BUILD_OBSERVATION_REVIEW_RULE.md`;
- `BUILD_HANDOFF_RULE.md`.


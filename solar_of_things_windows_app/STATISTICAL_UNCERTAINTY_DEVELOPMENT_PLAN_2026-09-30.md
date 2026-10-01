# Statistical Uncertainty / Enel-SEC Reporting Development Plan

Date: 2026-09-30

Status: **ACTIVE CANONICAL DEVELOPMENT TRANCHE**

Scope:
- Enel bill-audit PDF;
- Simple Energy Report PDF where grid-import uncertainty is shown;
- statistical completion of missing `grid_import_power_w` telemetry;
- treatment and presentation of `UnattributedHouseKwh`;
- validation/backtesting of P5/P50/P95 before those values are presented as strong third-party evidence.

This tranche starts from the accepted visual/reporting baseline already present in the repository. It does **not** reopen unrelated tariff, bill-entry, ingestion, dashboard or family-report work.

---

# Governing principles

1. **Do not tune the model toward the Enel bill.**
   - The Enel billed/meter value must not participate in constructing the Solar of Things statistical distribution.
   - Enel is compared only after the Solar distribution is produced.

2. **Do not treat missing data as zero.**

3. **Do not merge different uncertainty sources without a model that prevents double counting.**

4. **Do not describe P5/P50/P95 as meter accuracy, calibration limits or Enel measurement error.**

5. **Validate the method before strengthening the report language.**

6. **Preserve the accepted card-based PDF visual grammar.**
   - Statistical clarity may add explanatory text, validation metadata or technical annex content.
   - It must not revert the reports to dense raw technical tables.

7. **No user-facing build is required during research/design phases 0-5.**
   - Internal commits/tests are allowed.
   - The first coherent user-testable implementation after this tranche should become the next Windows build after Build 494.

---

# Phase 0 — Freeze baseline and define the uncertainty problem

Status: **COMPLETE — 2026-09-30**

## 0.1 Frozen baseline

Repository baseline at tranche opening:
- default branch: `main`;
- Build 494 code commit: `a0eea98e513f1ce822073c0c02a4f2a104ffc9a3`;
- Build 494 workflow: `36517130642`;
- Build 494 artifact ID: `11011373590`;
- Build 494 CI / SQLite smoke / bill-report export smoke / portable publish: GREEN;
- Build 494 changes were presentation-only relative to the prior statistical implementation.

Build 494 remains the historical baseline even if the user has not downloaded/tested that ZIP locally.

The new statistical tranche supersedes the immediate need to perform the old Build-494-only visual QA before research begins. No Build 495 is created merely for Phase 0 documentation.

## 0.2 Existing statistical behavior

Current service:
- `UtilityGridImportStatisticalCompletionService`;
- method version: `grid-import-empirical-bootstrap.v1`;
- target metric: `grid_import_power_w`;
- current simulation count: 2,000;
- uncovered grid-import intervals are completed statistically;
- the Enel bill value is intentionally excluded from distribution construction;
- output includes:
  - observed kWh;
  - P5/lower;
  - P50/median;
  - P95/upper;
  - simulated mean and standard deviation;
  - temporal coverage;
  - missing hours;
  - donor sample count.

Current P5-P95 therefore addresses **missing temporal coverage of total grid import**.

## 0.3 Existing source-attribution behavior

Current source-attribution model separately produces:
- `SolarToHouseKwh`;
- `BatteryToHouseKwh`;
- `GridToHouseKwh`;
- `UnattributedHouseKwh`;
- observed-house kWh;
- attribution coverage of observed household energy.

`UnattributedHouseKwh` means:

> household energy was observed, but the available evidence was insufficient to assign that household energy confidently to Solar, Battery or Grid.

It does **not** mean:
- missing `grid_import_power_w` telemetry;
- unmeasured energy;
- automatically imported Enel energy.

## 0.4 Two formal uncertainty problems

### Problem A — temporal uncertainty in total utility import

Question:

> How much **total grid-import energy** could have occurred during periods where `grid_import_power_w` telemetry is missing or discontinuous?

Target quantity:
- total `grid_import_power_w` energy over the selected interval.

Primary output:
- observed grid-import kWh;
- completed P5;
- completed P50;
- completed P95.

This is the quantity intended for comparison with the utility meter/bill, subject to aligned time windows and evidence quality.

### Problem B — source-attribution uncertainty inside observed household load

Question:

> Of the household energy that was observed, how much cannot be assigned with sufficient evidence to Solar, Battery or Enel?

Target quantities:
- `GridToHouseKwh`;
- `SolarToHouseKwh`;
- `BatteryToHouseKwh`;
- `UnattributedHouseKwh`.

This is an analytical energy-flow problem. It is not the same as total grid import.

## 0.5 Canonical anti-double-counting rule

The following identity is now mandatory:

> **Missing total grid-import energy is not the same quantity as observed household energy with unresolved source attribution.**

Therefore:

- never add `UnattributedHouseKwh` directly to P5/P50/P95 total-grid-import values;
- never assume all unattributed household kWh came from Enel;
- never subtract all unattributed household kWh from Enel;
- never use source-attribution uncertainty to alter directly measured `grid_import_power_w` without explicit evidence;
- any future combined uncertainty model must identify overlap and prove that energy is not counted twice.

## 0.6 Canonical metric semantics for third-party reporting

### Importación total desde Enel

Meaning:
- all measured/estimated energy entering the installation from the utility grid.

Use:
- metric intended for comparison with Enel meter/bill.

Uncertainty addressed by Phase 1-3:
- missing/discontinuous grid-import telemetry.

### Enel -> Casa

Meaning:
- the portion of observed household consumption that can be attributed to the utility.

Use:
- household energy-flow interpretation.

Not directly interchangeable with:
- total utility import;
- utility bill consumption.

### Sin atribuir

Meaning:
- observed household energy for which source allocation could not be supported sufficiently.

Use:
- explicit evidence-quality disclosure.

It must remain visible unless a later validated model can allocate part of it probabilistically without fabrication.

## 0.7 What Phase 0 deliberately does not decide

Phase 0 does not yet decide:
- whether the current empirical bootstrap is adequate;
- the correct block length for a block bootstrap;
- whether moving/stationary/circular block bootstrap is preferable;
- whether 2,000, 10,000 or another simulation count is appropriate;
- whether attribution uncertainty can be modeled probabilistically;
- whether some attribution-reason categories must remain permanently unresolved;
- the final wording that Enel/SEC will see;
- acceptance thresholds for production use.

Those belong to later phases.

## 0.8 Phase 0 exit criteria

All are satisfied:

- [x] Build 494 frozen as historical baseline.
- [x] Current P5/P50/P95 target quantity identified.
- [x] Current `UnattributedHouseKwh` semantics identified.
- [x] Temporal coverage and attribution coverage explicitly separated.
- [x] Total grid import and Grid -> House explicitly separated.
- [x] Anti-double-counting rule documented.
- [x] Enel bill excluded from construction of the Solar statistical distribution.
- [x] No production code modified.
- [x] No user-facing build generated.

**PHASE 0 COMPLETE.**

---

## Mandatory reader-explanation rule — user requirement 2026-09-30

The final PDF/report must never assume that a reader understands P5/P50/P95 merely because the labels are present.

Whenever P5/P50/P95 are shown to a third-party reader, the presentation must include an immediately adjacent plain-language explanation covering all four questions:

1. **What was done?**
   - missing intervals were reconstructed statistically using comparable observed telemetry through repeated simulations/resampling;

2. **Why was it done?**
   - because missing telemetry cannot honestly be treated as zero and a single invented replacement value would hide uncertainty;

3. **What is it for?**
   - to produce a plausible range for the missing-data contribution and then compare the independently constructed Solar of Things range with the external utility value;

4. **What do P5/P50/P95 mean?**
   - P5: lower percentile / lower plausible edge under the model;
   - P50: median / central estimate;
   - P95: upper percentile / upper plausible edge under the model;
   - P5-P95: central 90% of simulated/completed outcomes under the stated model.

Reader wording must avoid assuming prior statistical training. The technical annex may be more formal, but the primary explanatory paragraph must remain understandable to an educated non-specialist.

Do not call P5/P50/P95 “tests” unless a real statistical hypothesis test is actually being performed. In this tranche they are percentiles/scenarios from a simulated completion distribution.

---

## Simulation-count parameterization requirement — user decision 2026-09-30

The Monte Carlo/resampling trial count must be parameterizable.

Rules:
- Phase 1 audits the current production method at its existing baseline of **2,000 simulations** so the current algorithm is measured without changing it.
- The test harness must accept an explicit simulation-count parameter so convergence can later be tested without redesigning the harness.
- Phase 2 must compare at least: 2,000; 5,000; 10,000; 20,000; 50,000; and 100,000 simulations.
- No final production default is fixed in Phase 1.
- Candidate production counts must be selected from observed convergence of P5/P50/P95, computational cost, and backtesting behavior rather than by convention alone.
- 100,000 simulations is a serious candidate for final report generation if runtime remains operationally acceptable.
- Increasing trial count reduces Monte Carlo numerical error only; it does not correct a misspecified statistical model or insufficient donor data.

---

# Phase 1 — Audit the current statistical method

Status: **COMPLETE — 2026-09-30**

Lot 1 evidence:
- research harness: `research/statistical_uncertainty/phase1_current_method_audit.py`;
- results: `research/statistical_uncertainty/PHASE1_LOT1_RESULTS_2026-09-30.md`;
- 1,000 repetitions per scenario;
- 2,000 simulated completions per repetition;
- 2-hour controlled gap;
- stable: 89.5% empirical P5-P95 coverage;
- day/night: 87.8%;
- moderate autocorrelation: 61.7%;
- high autocorrelation: 30.6%;
- P50 mean directional bias remained near zero in all four Lot 1 scenarios;
- finding: current point-wise resampling can materially understate uncertainty under serial dependence;
- Phase 1 remains open.

Lot 2 completed 2026-09-30:
- harness: `research/statistical_uncertainty/phase1_gap_duration_audit.py`;
- raw results: `research/statistical_uncertainty/PHASE1_LOT2_RESULTS_2026-09-30.csv`;
- report: `research/statistical_uncertainty/PHASE1_LOT2_REPORT_2026-09-30.md`;
- 1,000 repetitions per scenario/duration cell;
- 2,000 simulations retained for every statistical completion;
- confirmed 15-minute effective continuity threshold under nominal 5-minute cadence;
- 10/15-minute separations are bridged and can report 100% temporal coverage despite missing source samples;
- 20 minutes and above activate statistical completion;
- stable/day-night scenarios remain broadly near nominal coverage;
- moderate/high autocorrelation materially undercovers across all tested statistical gap lengths;
- P50 remains comparatively centered, reinforcing that the main defect is interval calibration, not simple directional bias.

Remaining Phase 1 work:
- missingness topology: one long gap vs multiple/distributed gaps;
- appliance-like/persistent load episodes;
- any additional scenario checks required before method-comparison Phase 2.



Goal:
- measure objectively where `grid-import-empirical-bootstrap.v1` works and fails.

Required controlled scenarios:
- stable consumption;
- day/night pattern;
- weekday/weekend regimes;
- weak temporal autocorrelation;
- strong temporal autocorrelation;
- load spikes;
- short gaps;
- long gaps;
- multiple distributed gaps.

Required metrics:
- empirical P5-P95 coverage;
- P50 bias;
- interval width;
- error by gap duration;
- stability/reproducibility.

No production replacement is selected in this phase.

Exit:
- reproducible evidence table describing current-method behavior.

---

## Phase 1 design note — 5-minute cadence and next-lot boundary tests

Recorded: 2026-09-30

The source telemetry has a nominal cadence near **5 minutes**. This materially affects both statistical dependence and gap detection.

### Why cadence matters

Adjacent 5-minute grid-import values can be positively serially dependent because household loads often persist across several consecutive samples.

The current point-wise completion method samples missing 5-minute segments independently. Therefore it may preserve the marginal level by time-of-day while failing to preserve realistic contiguous high/low-load episodes. This is a plausible mechanism for the severe P5-P95 undercoverage observed in Lot 1 under synthetic autocorrelation.

This is not an argument against 5-minute data. The cadence is useful; the model must respect dependence across adjacent samples.

### Existing continuity/gap policy that must be audited separately

The current integration logic derives a continuity threshold from the median sample gap:
- median gap × 3;
- clamped to approximately 10–20 minutes;
- only separations **greater than** the threshold are treated as uncovered gaps rather than integrated continuously.

With a typical 5-minute median cadence, the effective threshold is normally around **15 minutes**.

Therefore the audit must distinguish:
1. a missing source sample that is still bridged by the ordinary integration rule;
2. a discontinuity that crosses the uncovered-gap threshold and enters statistical completion.

### Revised next Phase 1 lot

Before broader scenario expansion, test the gap-detection and gap-duration boundary explicitly.

Candidate controlled separations/durations:
- 10 minutes;
- 15 minutes;
- 20 minutes;
- 30 minutes;
- 1 hour;
- 2 hours;
- 4 hours;
- 8 hours;
- 12 hours.

For each case record:
- whether the current integration layer classifies it as continuous or uncovered;
- number of missing 5-minute segments actually statistically completed;
- P5-P95 empirical coverage;
- P50 bias/MAE;
- interval width;
- donor-pool behavior.

Also compare, with careful interpretation:
- one long contiguous missing period;
- the same nominal number of missing 5-minute samples distributed across the period.

This comparison must explicitly account for the fact that isolated missing samples may remain below the continuity threshold and therefore may be bridged by trapezoidal integration rather than sent to the statistical completion service.

No production change is authorized by this note.

---

# Phase 2 — Design and compare candidate completion methods

Status: **COMPLETE — 2026-09-30**

Selected Phase 3 candidate:
- `grid-import-context-block-bootstrap-120m.candidate-v1`;
- research alias: **C2-120**;
- context: +/-15 minutes, weekday/weekend matched;
- contiguous block length up to 120 minutes;
- longer gaps are stitched from additional contextual blocks;
- current 2,000 simulations retained during comparison.

Holdout:
- 8 untouched scenario families;
- 1,000 paired repetitions per family;
- C2-120: 1 corrected undercoverage failure / 8;
- C0 current baseline: 7 / 8;
- C2-120 mean normalized interval score 1.059 vs C0 1.822;
- paired score improvement vs C0: -0.764, bootstrap 95% CI [-0.895,-0.646].

Known candidate limitation:
- extreme AR phi=0.95;
- overall holdout coverage 86.9%;
- diagnostic 240-minute gaps 80.3% when two 120-minute blocks must be stitched.

Canonical evidence:
- `research/statistical_uncertainty/PHASE2_CANDIDATE_COMPARISON_PROTOCOL_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE2_DISCOVERY_CHECKPOINT_AND_AMENDMENT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE2_HOLDOUT_REPORT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE2_HOLDOUT_RESULTS_2026-09-30.csv`;
- `research/statistical_uncertainty/PHASE2_AR095_DIAGNOSTIC_2026-09-30.csv`.

C2-120 is a **provisional Phase 3 candidate only**, not production approval.


Minimum candidates:
1. current point-wise empirical bootstrap;
2. contiguous block bootstrap;
3. context-stratified contiguous block bootstrap.

Selection rules:
- do not select based on producing a larger discrepancy with Enel;
- select using predeclared reconstruction/backtesting metrics;
- preserve time dependence where evidence shows it is material.

Exit:
- one justified candidate method or a documented STOP if none is adequate.

---

# Phase 3 — Backtest on real Solar of Things telemetry

Status: **COMPLETE — C2-120 REJECTED; RETURNED TO PHASE 2R REDESIGN — 2026-09-30**

Input gate:
- use `SolarOfThings-ResearchExporter-R1-win-x64`;
- green run `36786984422`, artifact `11130595515`;
- source commit `716b87808f5183692de237205cc2a67c0b44d9c5`;
- artifact SHA-256 `af325caf9aff4dc806fe2db5327ff48bcc7e6232f3f7008105251533bf800cf5`;
- owner returns only the generated pseudonymized research ZIP;
- no Enel bill/meter value is part of Phase 3 model construction or scoring.

Goal:
- validate the candidate using real known data.

Method:
- choose periods with adequate real `grid_import_power_w` coverage;
- hide known segments;
- reconstruct them without access to the hidden values;
- compare P5/P50/P95 and P50 with the true hidden total.

Required stratification where sample size permits:
- hour-of-day;
- weekday/weekend;
- gap duration;
- low/normal/high load regimes.

Exit:
- measured real-data interval coverage and bias;
- evidence-based applicability limits;
- STOP if the method is not sufficiently defensible.

---

# Phase 4 — Model source-attribution uncertainty

Status: PENDING

Goal:
- determine whether any portion of `UnattributedHouseKwh` can be handled statistically without fabrication or double counting.

Required work:
- classify unresolved attribution reasons;
- identify which reason families have comparable resolved historical cases;
- test any probabilistic allocation only against cases with known/resolved outcomes;
- preserve an explicit unresolved remainder.

Outputs must remain separate from total-grid-import P5/P50/P95 unless a formally validated joint model is later justified.

Exit:
- validated attribution uncertainty method, or explicit decision to keep categories unresolved.

---

# Phase 5 — Design Enel/SEC narrative and audit presentation

Status: PENDING

Goal:
- explain the statistical evidence to a non-specialist technical/regulatory reader without overstating what it proves.

Mandatory content:
- why missing data require a range instead of zero/single invented value;
- plain-language P5 explanation;
- plain-language P50 explanation;
- plain-language P95 explanation;
- what the central 90% means;
- method summary;
- temporal coverage;
- missing duration;
- donor evidence;
- validation/backtesting result;
- explicit statement that Enel was not used to construct the interval;
- explicit statement that the interval is not meter calibration/tolerance;
- separate explanation of attribution uncertainty.

Preserve:
- current card hierarchy;
- readable PDF layout;
- technical annex/provenance where needed.

Exit:
- wording and information architecture approved before code integration.

---

# Phase 6 — Implement the validated statistical model

Status: PENDING

Goal:
- implement only methods accepted in Phases 1-5.

Requirements:
- method versioning;
- deterministic/reproducible seed behavior where applicable;
- sufficient provenance metadata;
- automated statistical regression tests;
- existing smoke tests remain green;
- legacy method retained as test baseline until replacement is proven.

No user-facing build is handed off until the implementation forms a coherent tranche.

---

# Phase 7 — Integrate into PDFs without visual regression

Status: PENDING

Targets:
- Enel bill-audit PDF;
- Simple Energy Report where applicable.

Preserve:
- accepted card-based presentation;
- existing hierarchy;
- readability at normal screen/print size.

Add only validated:
- P5/P50/P95 explanations;
- validation evidence;
- method/coverage context;
- attribution-uncertainty disclosure;
- neutral interpretation against Enel.

Exit:
- export tests green;
- visual QA-ready PDFs.

---

# Phase 8 — Adversarial QA and next build

Status: PENDING

Minimum cases:
- 100% grid coverage;
- small gap;
- large gap;
- multiple gaps;
- insufficient donors;
- atypical behavior;
- Enel inside interval;
- Enel outside interval;
- high unattributed household energy;
- attribution not modelable;
- report without Enel reference value.

Required final gates:
- statistical tests PASS;
- real-data backtesting accepted;
- no double counting;
- smoke PASS;
- PDF export PASS;
- visual QA PASS.

Only then:
- publish the next coherent Windows test build after Build 494;
- follow `BUILD_HANDOFF_RULE.md`;
- ask the user only for the bundled target-PC QA needed for that coherent tranche.


## Phase 1 closure — 2026-09-30

Phase 1 is closed after five synthetic audit lots plus a direct production-code validation gate.

Canonical evidence:
- `research/statistical_uncertainty/PHASE1_LOT1_RESULTS_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE1_LOT2_REPORT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE1_LOT2_INFERENTIAL_VALIDATION_2026-09-30.csv`;
- `research/statistical_uncertainty/PHASE1_LOT3_REPORT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE1_LOT4_DONOR_RESOLUTION_REPORT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE1_LOT5_PERSISTENT_REGIME_REPORT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE1_MATHEMATICAL_VALIDATION_PROTOCOL_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE1_VALIDATION_CHECKPOINT_2026-09-30.md`.

Direct C# validation:
- workflow run `36781580855`;
- job `110112872942`;
- result **SUCCESS**;
- confirmed 15-minute bridge behavior, 20-minute statistical activation, and exact constant-energy fixtures.

Phase 1 conclusion:
- current P50 can remain approximately centered in many scenarios;
- current P5-P95 is not reliably calibrated under serial dependence;
- point-wise resampling omits covariance across persistent 5-minute segments;
- whole-hour donor bins can be too coarse for intrahour ramps;
- finite effective donor units matter when repeated 5-minute samples belong to one persistent episode;
- short source-sample losses may still be reported as 100% integration coverage because they are bridged rather than statistically completed.

The synthetic audit does not claim the user's real telemetry has any particular AR coefficient or regime probability.

**Next authorized phase: Phase 2 — compare candidate dependent-data completion methods using the same predeclared calibration metrics.**


## Phase 3 real-data decision — 2026-09-30

Canonical evidence:
- `research/statistical_uncertainty/PHASE3_REAL_DATA_BACKTEST_PROTOCOL_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE3_REAL_DATA_BACKTEST_REPORT_2026-09-30.md`;
- `research/statistical_uncertainty/PHASE3_DURATION_RESULTS_2026-09-30.csv`;
- `research/statistical_uncertainty/PHASE3_LOAD_REGIME_RESULTS_2026-09-30.csv`;
- `research/statistical_uncertainty/PHASE3_START_STATE_RESULTS_2026-09-30.csv`;
- `research/statistical_uncertainty/PHASE3_ACTUAL_GAP_PROFILE_2026-09-30.csv`.

Decision:
- C2-120 strongly improves on C0;
- C2-120 is nevertheless rejected as a general production interval because observable active-start gaps remain materially under-covered and long-gap behavior is inadequate;
- no production statistical/PDF change is authorized;
- Phase 2R redesign is active under `PHASE2R_REAL_DATA_REDESIGN_PROTOCOL_2026-09-30.md`.


## Phase 2R status update — R2 rejected after locked holdout

R1:
- rejected at development gate;
- holdout never inspected.

R2:
- cross-fitted development passed;
- parameters frozen before holdout;
- locked holdout failed proper interval-score and active-start P50-bias gates;
- R2 is rejected and must not be retuned on that holdout.

Active research state:
- return to Phase 2R redesign;
- next candidate must address temporal calibration drift using prior-only adaptive evidence;
- existing corpus is development-only for any post-R2 method;
- a new future real-data validation period is mandatory before production approval.

Phase 3 remains closed as a rejection gate until a redesigned candidate survives both development and genuinely future validation.


## Current active research state after Phase 3 / Phase 2R — 2026-09-30

Phase 3:
- canonical v2 rerun COMPLETE;
- C2-120 rejection confirmed reproducibly.

Phase 2R:
- R1 REJECTED at development;
- R2 REJECTED after locked holdout;
- R3 protocol FROZEN before future validation outcomes.

Active candidate:
- `grid-import-boundary-residual-dayweighted-240m.candidate-v4`.

Authority:
- `research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`;
- `research/statistical_uncertainty/STATISTICAL_RESEARCH_CONSOLIDATED_HANDOFF_2026-09-30.md`.

R3 requires genuinely future telemetry beginning 2026-09-28.
The current owner package ends 2026-09-27 and is not a future-validation set.

Before any future outcome scoring:
- an executable R3 validation harness may be implemented and frozen from the existing protocol;
- future evidence stopping counts must be evaluated without inspecting model outcomes;
- no acceptance gate may be changed after future outcomes are viewed.

Production implementation remains blocked until R3 passes its frozen future-validation gates.

# Phase 2R — R2 locked holdout result

Date: 2026-09-30

Status: **R2 REJECTED — LOCKED HOLDOUT FAILED; DO NOT RETUNE ON THIS HOLDOUT**

Candidate:
`grid-import-c2-grouped-day-calibrated.candidate-v3`.

Frozen before holdout:
- R2 harness commit: `f56e91261380d7e499fd80772973a2af0ef7e7ce`;
- parameter commit: `5cc12e78bf7517c36114ab24d9fed238499e8036`;
- development PASS report: `PHASE2R_R2_DEVELOPMENT_REPORT_2026-09-30.md`;
- locked dates: 2026-09-13 through 2026-09-26;
- no R2 parameter was changed after holdout inspection.

## Holdout coverage

R2 coverage is high:
- active-short: 100% (44 cases / 6 days);
- active-long: 100% (8 cases / 4 days; too small for the >=10-case group gate);
- inactive-short: 100%;
- inactive-long: 95.45%.

By duration:
- 20m: 100%;
- 30m: 100%;
- 60m: 100%;
- 120m: 100%;
- 240m: 94.74%;
- 480m: 100%.

This is not sufficient for acceptance because an excessively wide interval can trivially over-cover.

## Decisive failures

### Proper interval score

Overall R2 interval-score ratio versus raw C2:
- **1.2187**.

Frozen holdout gate:
- required <=1.10.

Result:
- **FAIL**.

R2 widened the predictive range enough that its probabilistic forecast quality was about 21.9% worse than raw C2 on this holdout.

### Active-start P50 bias

Raw C2 active-start signed bias:
- **-0.6513 kWh**.

R2 corrected active-start signed bias:
- **+0.8990 kWh**.

The absolute bias increased by about 38%.

Frozen holdout gate:
- R2 active-start absolute bias must be lower than raw C2.

Result:
- **FAIL**.

The development-derived location correction overshot on the later holdout period.

## Interpretation

R2 demonstrates that:
- grouped day-level calibration can repair the development-period active-start failure;
- but a fixed calibration learned over the full development interval is not stable enough under temporal distribution shift;
- the correction can change from underestimation to overestimation;
- widening the interval to force coverage is not acceptable because proper interval score deteriorates.

The holdout must not be reused to tune R2.

## Decision

**Reject R2.**

Do not:
- weaken the interval-score gate;
- keep R2 merely because holdout coverage reached 100%;
- tune b/q on 2026-09-13 through 2026-09-26;
- use Enel values.

Next redesign must explicitly address temporal calibration drift.

Because the existing real-data corpus has now been used for R1/R2 design or evaluation, any future candidate tuned after this result requires a new genuinely future validation period before production approval.

No production C# or PDF wording changes are authorized.

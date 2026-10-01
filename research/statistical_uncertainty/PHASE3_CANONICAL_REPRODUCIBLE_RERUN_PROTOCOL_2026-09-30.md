# Phase 3 v2 — Canonical reproducible real-data rerun protocol

Date: 2026-09-30

Status: **FROZEN BEFORE V2 OUTCOME SCORING**

## 1. Why this rerun is required

The original Phase 3 real-data analysis persisted:
- protocol;
- aggregate CSVs;
- report.

However, the exact executable harness that produced those tables was not persisted.

A reconstruction from the written protocol reproduces the target-case counts exactly, but does not reproduce the original C2 aggregate coverage/percentile results within reasonable Monte Carlo variation.

Therefore:
- the original Phase 3 report remains historical evidence;
- its exact numerical tables are **superseded for final inference**;
- no new calibration/model layer may use those old aggregate numbers as its computational base;
- a canonical executable harness must be committed before any new outcomes are accepted.

This is a reproducibility correction, not a relaxation of acceptance criteria.

## 2. Input

Research package:
- file supplied by owner: `SolarOfThings-ResearchPackage-20260930-214934.zip`;
- package SHA-256:
  `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`;
- manifest/hash validation: PASS;
- timezone: `America/Santiago`.

No Enel bill, utility-meter value, tariff or other external utility target is loaded.

## 3. Canonical target construction

Metric:
- `grid_import_power_w`;
- use only finite values with confidence != UNRESOLVED.

Local-day eligibility:
- at least 280 grid-import observations on that local date.

Nominal target durations:
- 20, 30, 60, 120, 240, 480 minutes.

For a nominal duration D:
- `k = D/5` consecutive **intervals**;
- target truth therefore requires `k+1` original observed points;
- every consecutive link in those `k+1` points must be between 4.5 and 5.5 minutes;
- all points remain within one local date.

Time balancing:
- D <=240m: four local bands [00:00,06:00), [06:00,12:00), [12:00,18:00), [18:00,24:00);
- D=480m: two bands [00:00,12:00), [12:00,24:00).

Within each date/band/duration:
- sort eligible starts by timestamp;
- choose the **earliest eligible start** in that band;
- target power values are not used for target selection.

History requirement:
- target start must have a complete 90-calendar-day package horizon available before it.

This deterministic rule is the canonical v2 target-selection rule.

## 4. Truth calculation

For target points `(t_0,x_0),...,(t_k,x_k)`:

```
truth_kwh =
 sum_{j=0}^{k-1}
   max(0,(x_j+x_{j+1})/2) *
   hours(t_{j+1}-t_j) / 1000
```

This is actual-timestamp trapezoidal integration.

Observable boundary values:
- start value = `x_0`;
- end value = `x_k`;
- target interior points `x_1...x_{k-1}` are hidden from candidate construction.

## 5. Donor-history horizon and anti-leakage

For every target:
- donor history begins at `target_start - 90 calendar days`;
- donor observations must be strictly earlier than `target_start`;
- no future data are allowed;
- C0 and C2 receive exactly the same temporal history horizon.

For a C2 block, the **entire donor block** must:
- start inside the 90-day history;
- end strictly before target start;
- contain only regular 4.5–5.5 minute links;
- never use any target interior value.

## 6. Canonical C0 v2 baseline

C0 reproduces the production donor hierarchy but uses the same external 90-day historical horizon as C2:

For each missing nominal five-minute segment:
1. same local hour + same weekday/weekend type if >=12 donor points;
2. circular hour distance <=1 + same day type if >=12;
3. same local hour ignoring day type if >=8;
4. all eligible historical donor points.

Each segment samples independently.

The total target actual duration is partitioned into `k` equal-duration evaluation pieces, so the sampled watt values integrate to the target's **actual endpoint duration**, not an assumed exact D minutes.

Simulation count:
- 2,000.

## 7. Canonical C2-120 v2 base candidate

Label:
`grid-import-context-block-bootstrap-120m.candidate-v1-v2harness`.

Maximum block:
- 24 nominal intervals = 120 minutes.

For each target chunk:
- chunk interval count `c = min(24, remaining target intervals)`;
- a donor block requires **c+1 observed points** and therefore represents exactly c consecutive energy intervals;
- all c donor links must each be 4.5–5.5 minutes.

Context matching uses donor block **start**:
1. circular local clock-time distance <=15 minutes + same weekday/weekend type, if >=8 eligible donor starts;
2. <=30 minutes + same day type, if >=8;
3. <=60 minutes + same day type, if >=8;
4. same local hour + same day type, if >=8;
5. all valid historical contiguous block starts.

No hidden truth is inspected when choosing donors.

Each selected donor block energy is computed by actual-timestamp trapezoidal integration over its c intervals.

To compare fairly with the actual target chunk duration:
```
sampled_chunk_kwh =
 donor_block_kwh *
 target_chunk_actual_hours /
 donor_block_actual_hours
```

For targets longer than 120 minutes:
- advance to the next target chunk;
- independently sample another contextual donor block;
- sum chunk energies.

This intentionally preserves the known C2 stitching behavior so long-gap failure remains testable.

## 8. Deterministic Monte Carlo

Simulation count:
- 2,000.

Each target/method uses a deterministic seed derived from SHA-256 of:
- canonical harness version;
- method label;
- target start UTC;
- nominal duration;
- simulation count.

The seed is reduced to a 64-bit unsigned integer for NumPy `default_rng`.

Rerunning the same package/harness must reproduce the same per-case percentiles bit-for-bit subject to the pinned runtime/library percentile semantics.

Percentiles:
- NumPy linear percentile interpolation;
- P5/P50/P95.

## 9. Metrics and strata

Per case persist:
- local date;
- target UTC/local start/end;
- nominal and actual duration;
- truth kWh;
- start/end observed watts;
- active-start flag (>100 W);
- C0 P5/P50/P95;
- C2 P5/P50/P95;
- interval score alpha=0.10;
- donor counts;
- C2 fallback level for each chunk.

Aggregate by:
- nominal duration;
- active/inactive observable start state;
- weekday/weekend;
- local time band.

Primary coverage uncertainty:
- 5,000-replicate bootstrap clustered by local date using a fixed deterministic seed.

## 10. Canonical artifacts

The committed harness must accept:
`--package <research ZIP>`.

It must write:
- per-case CSV;
- duration aggregate CSV;
- start-state aggregate CSV;
- run metadata JSON containing:
  - input package SHA-256;
  - harness version;
  - simulation count;
  - target counts;
  - runtime/library versions;
  - deterministic seed rule.

No final conclusion may be recorded without these artifacts.

## 11. Relationship to original Phase 3

Original Phase 3 artifacts are retained for audit trail but are marked:
**HISTORICAL / SUPERSEDED FOR FINAL NUMERICAL INFERENCE**.

The v2 rerun is not required to match the original table.

It is required to:
- implement this frozen protocol exactly;
- be executable/reproducible;
- produce auditable per-case evidence.

If the qualitative active-state undercoverage disappears under v2, the project must revise the prior rejection honestly.

If it persists, the rejection becomes reproducible evidence and R2 may use the v2 per-case C2 outputs as its base.

## 12. R2 dependency

`PHASE2R_R2_GROUPED_DAY_CALIBRATION_PROTOCOL_2026-09-30.md` is frozen conceptually but **must not be executed** until this v2 rerun is complete.

Before R2 execution:
- amend its computational input reference to the canonical v2 per-case CSV;
- do not alter its four groups, correction formulas, development/holdout split, or acceptance gates.

# Phase 3 v2 — canonical reproducible rerun result

Date: 2026-09-30

Status: **COMPLETE — C2-120 REJECTION CONFIRMED REPRODUCIBLY**

Input:
- owner package: `SolarOfThings-ResearchPackage-20260930-214934.zip`;
- SHA-256: `277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec`;
- manifest and declared file hashes: PASS;
- no Enel bill, meter, tariff or other utility target loaded.

Canonical protocol:
- `PHASE3_CANONICAL_REPRODUCIBLE_RERUN_PROTOCOL_2026-09-30.md`.

Committed executable harness:
- `phase3_canonical_v2_backtest.py`;
- harness version `phase3-real-backtest.v2.1`;
- commit `6bddba0325057bcff1bcc85e0b49e12bc6bdf9aa`.

The deterministic target constructor reproduces the frozen target counts exactly:
- 20m: 242;
- 30m: 242;
- 60m: 241;
- 120m: 241;
- 240m: 192;
- 480m: 84.

The generated per-case CSV contains 1,242 targets and has SHA-256:
`726ae954afeb996bece4e3f30c4d9fc6f8275a4783691393cba0390dbb64f03f`.

It is deterministically regenerable from the committed harness and owner package.

## Overall duration coverage

| Duration | C0 | C2-120 | C2 clustered 95% CI |
|---:|---:|---:|---:|
| 20m | 89.7% | 95.9% | 93.0–98.3% |
| 30m | 87.6% | 95.5% | 92.6–97.9% |
| 60m | 66.0% | 93.8% | 90.9–96.3% |
| 120m | 32.4% | 94.6% | 91.7–97.1% |
| 240m | 18.2% | 94.3% | 91.1–97.0% |
| 480m | 9.5% | 88.1% | 81.3–94.3% |

The v2 numerical values differ from the historical Phase 3 table, as expected from the reproducibility correction, but the principal structural conclusion remains.

## Observable active-start defect

When the grid-import value at the observed start boundary is >100 W:

| Duration | Cases | C2 coverage | clustered 95% CI | C2 P50 bias |
|---:|---:|---:|---:|---:|
| 20m | 50 | 80.0% | 65.8–91.1% | -0.376 kWh |
| 30m | 50 | 78.0% | 63.0–89.5% | -0.561 kWh |
| 60m | 50 | 70.0% | 55.0–82.7% | -1.131 kWh |
| 120m | 51 | 78.4% | 65.3–88.9% | -1.914 kWh |
| 240m | 37 | 81.1% | 66.7–92.5% | -2.358 kWh |
| 480m | 19 | 68.4% | 44.4–88.9% | -3.249 kWh |

For 30m, 60m and 120m the clustered upper confidence limit is below 90%; 20m is materially low in point coverage and remains uncertain at its available day count.

Inactive-start coverage is near/at 100% for C2 at most durations, confirming that aggregate coverage is dominated by easier inactive periods.

The active-start P50 bias is consistently negative and grows with gap duration.

## Other strata

Day type:
- weekday C2: 94.37%;
- weekend C2: 94.35%.

Time-band diagnostics show a weakness in the 12-hour band used for afternoon/evening 480-minute targets:
- `12-24` C2: 78.95% (38 cases);
- other principal bands are materially higher.

This is consistent with the long-gap/persistence risk and does not rescue the candidate.

## Decision

The original Phase 3 tables are retained as historical evidence but are superseded for final numerical inference by this v2 rerun.

**C2-120 remains rejected as a general production model.**

Reason:
- aggregate coverage is good;
- observable active-start conditional coverage is materially inadequate;
- the bias is directionally persistent;
- 480-minute behavior remains weak;
- the defect survives a deterministic, committed, reproducible harness.

Therefore the return to Phase 2R remains valid.

Next authorized step:
- amend R2 computational input to this canonical v2 per-case output without changing R2 groups, formulas, development/holdout split or acceptance gates;
- execute R2 cross-fitted development;
- inspect the locked R2 holdout only if the development gate passes.

No production C# or PDF wording is authorized by this result.

# Phase 2R — R2 cross-fitted development result

Date: 2026-09-30

Status: **DEVELOPMENT PASS — LOCKED HOLDOUT AUTHORIZED**

Candidate:
`grid-import-c2-grouped-day-calibrated.candidate-v3`.

Canonical computational base:
- Phase 3 v2 per-case SHA-256: `726ae954afeb996bece4e3f30c4d9fc6f8275a4783691393cba0390dbb64f03f`;
- R2 harness: `phase2r_r2_grouped_day_calibration.py`;
- harness commit: `f56e91261380d7e499fd80772973a2af0ef7e7ce`.

Development:
- 2026-07-19 through 2026-09-11;
- leave-one-local-day-out cross fitting;
- no case is calibrated using its own local day;
- locked 2026-09-13 through 2026-09-26 outcomes were not inspected before this PASS.

## Primary group results

| Group | Cases | Days | Coverage | cluster 95% CI | R2 P50 bias | raw C2 bias |
|---|---:|---:|---:|---:|---:|---:|
| active-short | 157 | 23 | 96.18% | 90.45–100% | +0.153 kWh | -1.150 kWh |
| active-long | 48 | 20 | 95.83% | 88.89–100% | -0.076 kWh | -2.826 kWh |
| inactive-short | 585 | 46 | 99.66% | 99.13–100% | -0.028 kWh | -0.028 kWh |
| inactive-long | 176 | 46 | 97.16% | 93.96–99.46% | +0.600 kWh | +0.600 kWh |

## Duration coverage

- 20m: 99.46%;
- 30m: 98.92%;
- 60m: 98.92%;
- 120m: 98.38%;
- 240m: 97.40%;
- 480m: 95.71%.

## Frozen development gates

All seven pass:
1. active pooled coverage >=85%: **96.10% PASS**;
2. active-short/active-long each >=85%: **PASS**;
3. no primary group has cluster-bootstrap 95% upper bound below 90%: **PASS**;
4. active-start absolute P50 bias materially reduced: **93.54% reduction PASS**;
5. interval score <=110% raw C2: ratio **0.883 PASS**;
6. no duration below 80%: minimum **95.71% PASS**;
7. calibration insufficiency <=5%: **0% PASS**.

## Parameters frozen before holdout

- inactive-short: b=0 kW, q=0 kW;
- active-short: b=1.377832 kW, q=1.340016 kW;
- inactive-long: b=0 kW, q=0.043215 kW;
- active-long: b=0.510132 kW, q=0.626787 kW.

These values are now immutable for the locked holdout.

Decision:
- development PASS authorizes exactly one evaluation of the locked 2026-09-13 through 2026-09-26 holdout;
- do not retune after viewing holdout.

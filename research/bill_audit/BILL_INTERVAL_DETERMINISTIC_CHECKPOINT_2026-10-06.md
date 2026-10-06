# Enel bill audit — deterministic energy truth-table checkpoint

Date: 2026-10-06

Status: **ENERGY BASELINE ESTABLISHED / STATISTICAL COMPLETION STILL OPEN**

Canonical product specification:
- `solar_of_things_windows_app/ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md`.

Deterministic harness:
- `research/bill_audit/bill_interval_truth_table.py`;
- version: `bill-interval-truth-table.v1`;
- implementation commit: `03dac66fd6f8510d49583f3c6577c23e14a4e3ae`.

Input package:
- `SolarOfThings-ResearchPackage-20261006-154413.zip`;
- SHA-256: `3a7bc7edb9e264333cd6da73b85f9f2d805b6eb4f5ef20594ddc34ffdbdd6295`;
- package integrity already validated before this checkpoint.

No Enel value was used to calculate the inverter energy baseline.

---

## 1. Billing interval convention

Printed bill period:

- start local date: 2026-08-28;
- end local date: 2026-09-28.

Canonical internal representation:

```
[2026-08-28 00:00:00 America/Santiago,
 2026-09-29 00:00:00 America/Santiago)
```

UTC representation:

```
[2026-08-28 04:00:00Z,
 2026-09-29 03:00:00Z)
```

Elapsed real time:
- **767.0 hours**.

The calendar span covers 32 local dates but contains 767 real hours because the America/Santiago DST transition occurs inside the billing interval.

---

## 2. Deterministic inverter evidence

Metric:
- `grid_import_power_w`.

Source semantics:
- normalized from HPVINV02 `mainsPower`;
- current target normalization labels it `CONFIRMED` / `OFFICIAL_ENERGY_FLOW_GRID_IMPORT`.

Rows inside the interval:
- grid-import source rows: **9,180**;
- valid finite/non-UNRESOLVED samples: **9,180**;
- unresolved/non-finite rows: **0**.

Cadence:
- median positive gap: **5.004283 min**;
- production-equivalent continuity threshold:
  **15.012850 min**;
- rule: median gap × 3, clamped to 10–20 minutes.

Observed integration:
- covered time: **761.898707 h**;
- uncovered time: **5.101293 h**;
- temporal coverage: **99.334903%**;
- directly observed positive grid-import energy:
  **88.065413 kWh**.

This value is deterministic and does not include any statistical completion.

---

## 3. Uncovered intervals

Five uncovered intervals exist under the production-equivalent continuity rule.

| # | Type | Local start | Local end | Duration | Start W | End W |
|---|---|---|---|---:|---:|---:|
| 1 | boundary start | 2026-08-28 00:00:00 | 00:03:38.563 | 3.643 min | — | 0 |
| 2 | internal | 2026-09-05 16:15:16.877 | 16:30:17.984 | 15.018 min | 0 | 0 |
| 3 | internal | 2026-09-12 15:09:33.752 | 19:32:00.957 | **262.453 min** | **0** | **0** |
| 4 | internal | 2026-09-24 23:20:17.212 | 23:40:18.227 | 20.017 min | 676 | 646 |
| 5 | boundary end | 2026-09-28 23:55:03.235 | 2026-09-29 00:00:00 | 4.946 min | 460 | — |

The dominant missing interval is therefore:
- 2026-09-12;
- 4 h 22 min 27 s;
- observed grid-import boundary values are 0 W at both sides.

The other four gaps together account for less than 44 minutes.

---

## 4. Daily quality concentration

The coverage deficit is strongly concentrated rather than broadly distributed.

Lowest daily coverage:
- 2026-09-12: **81.774%**;
- 2026-09-24: **98.610%**;
- 2026-09-05: **98.957%**.

All other interval days are at or near complete coverage under the same deterministic rule.

The daily allocation in the harness is derived from the same period-level links and splits intervals at local midnight with linear interpolation. Therefore daily covered/uncovered hours and observed energy sum exactly to the period totals, including across DST.

---

## 5. Relationship with the 97 kWh bill value

Only after independently establishing the inverter baseline, the printed bill value may be compared.

Printed billed consumption:
- **97.000 kWh**.

Directly observed inverter import:
- **88.065 kWh**.

Uncompleted difference:
- Enel minus directly observed inverter = **8.935 kWh**;
- equivalent to **9.211%** of the billed 97 kWh.

This is **not yet the final discrepancy**, because 5.101 h remain statistically uncompleted.

For the missing periods alone to bridge the entire 8.935 kWh difference, they would need to average:
- approximately **1.751 kW** across all uncovered time.

If all other missing intervals contributed zero, the 262.453-minute gap alone would need to average:
- approximately **2.043 kW**.

These are deterministic reconciliation requirements, not probability claims.

---

## 6. R3 applicability consequence

Frozen R3 applicability requires every individual bounded gap to be:
- >15 minutes;
- <=240 minutes;
- plus its other frozen eligibility/calibration rules.

This bill interval contains one gap of:
- **262.453 minutes**.

Therefore the full bill period is:

`INELIGIBLE_GAP_GT_240M`

under frozen R3 duration applicability.

Consequences:
- R3 future validation remains valuable for the product;
- but even a future R3 PASS would not authorize silently publishing a fully calibrated R3 P5/P50/P95 for this bill as currently observed;
- the urgent bill report requires an explicit method for the >240-minute inactive-boundary gap, or an explicit insufficiency treatment.

Do not weaken the R3 240-minute limit to make this bill fit.

---

## 7. Why the current production v1 interval is not automatically accepted

Current production statistical completion:
- `grid-import-empirical-bootstrap.v1`;
- pointwise 5-minute donor sampling;
- local-hour + weekday/weekend conditioning;
- 2,000 simulations.

Existing statistical research already proved:
- persistent/serially dependent consumption can make this pointwise method materially under-cover;
- P50 may look reasonable while P5–P95 is too narrow.

Therefore:
- current production v1 remains a useful comparator;
- it must **not** be promoted uncritically as the final external-evidence interval for this bill.

The bill-specific statistical method remains an explicit open gate.

---

## 8. HPVINV02 import-energy counter status

Prior API research demonstrates that the SiSeLi platform can expose properties such as:
- `buyElectricityQuantity`;
- `dayPurchaseElectricityConsumption`.

However, preserved target-device evidence currently establishes only:
- target model: HPVINV02;
- gather protocol: MH2083139;
- `mainsPower` -> `grid_import_power_w`;
- no validated target-specific grid-import energy counter is presently preserved as an authoritative normalized metric.

The Research Package intentionally excludes raw API payloads and utility/tariff evidence, so it cannot answer this target-specific counter question by itself.

Next technical action:
- inspect target current/aggregate/raw evidence for a real grid-import energy counter;
- validate any candidate against `mainsPower` integration before using it.

---

## 9. Tariff-source checkpoint

Official-source architecture remains valid.

For the billing interval, the Enel official 2026 archive contains:
- August 2026 retroactive regulated-supply publication;
- September 2026 regulated-supply publication.

The bill itself identifies:
- BT1-T5;
- Área Típica 1A.

Exact rate applicability/reconstruction is not frozen by this checkpoint.
It must be resolved from the official Enel publication(s), the service geography/configuration and bill-line behavior.

No single `CLP/kWh` shortcut is permitted.

---

## 10. Immediate next research gate

The next gate is statistical, not visual.

Before P5/P50/P95 can enter the final report:

1. characterize the 262.453-minute gap using only inverter history and observed boundaries;
2. predeclare a bill-gap method that does not use the 97 kWh value for construction/selection;
3. backtest that method on fully observed historical windows with comparable topology;
4. quantify calibration, bias and interval behavior;
5. either:
   - approve a clearly labelled provisional external-report method; or
   - declare the long gap statistically insufficient and use a different conservative presentation.

In parallel:
- resolve official tariff candidates;
- determine whether HPVINV02 exposes a trustworthy grid-import energy counter.

No production PDF/UI implementation should outrun this truth-table/statistical gate.

# Enel bill audit — final source / provenance table

Date: 2026-10-06

Status: **FROZEN FOR URGENT REPORT IMPLEMENTATION**

Canonical product specification:
- `solar_of_things_windows_app/ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md`.

This table defines the evidence identity and semantics that the urgent Enel bill-audit PDF and technical annex must use.

No customer PII is reproduced here.

---

## S1 — Printed Enel bill

Role:
- external billed/meter reference;
- source for printed billing period, meter readings, billed kWh, tariff label, Área Típica, printed charge lines, tax summary, subsidy/common-service amounts and printed totals.

Current acceptance case:
- printed period: 2026-08-28 through 2026-09-28;
- billed consumption: 97 kWh;
- tariff printed: BT1-T5;
- Área Típica printed: 1A;
- total due: 26,854 CLP.

Semantics:
- printed evidence;
- never used to construct/calibrate the inverter statistical distribution;
- may be used after the independent inverter distribution exists for reconciliation/comparison.

Report label:
- **Boleta Enel / valor facturado**

---

## S2 — Inverter time-series telemetry

Primary source:
- target HPVINV02 telemetry;
- raw field `mainsPower`;
- normalized metric `grid_import_power_w`.

Canonical deterministic interval evidence:
- harness: `research/bill_audit/bill_interval_truth_table.py`;
- version: `bill-interval-truth-table.v1`;
- checkpoint: `research/bill_audit/BILL_INTERVAL_DETERMINISTIC_CHECKPOINT_2026-10-06.md`.

Canonical current bill result:
- directly observed grid import: **88.065413 kWh**;
- covered time: **761.898707 h**;
- uncovered time: **5.101293 h**;
- temporal coverage: **99.334903%**;
- valid samples: **9,180**;
- unresolved/non-finite rows: **0**.

Semantics:
- directly observed / timestamp-integrated inverter evidence;
- power samples are converted to energy using actual elapsed time;
- gaps are not treated as zero;
- no Enel value participates in the integration.

Report label:
- **Importación directamente observada por el inversor**

---

## S3 — Provisional bill-specific gap completion

Canonical method:
- `bill-gap-calendar-window-empirical.v1`.

Research harness:
- `research/bill_audit/bill_gap_calendar_window_empirical.py`.

Research report:
- `research/bill_audit/BILL_GAP_CALENDAR_WINDOW_EMPIRICAL_REPORT_2026-10-06.md`.

Production implementation path:
- `src/SolarOfThings.Core/Utility/UtilityBillGapStatisticalCompletionService.cs`.

Method semantics:
- same local clock time;
- actual missing-window duration;
- same weekday/weekend class;
- strictly prior historical dates;
- latest 15 eligible comparable dates;
- minimum 10;
- inverse empirical q05/q50/q95;
- exact Cartesian aggregation across distinct internal gaps;
- short boundary slivers completed from nearest observed boundary;
- target boundary state is diagnostic only and does not narrow the primary interval;
- Enel billed kWh is excluded from construction/calibration/selection.

Canonical current bill result:
- P5: **88.103333 kWh**;
- P50: **88.103333 kWh**;
- P95: **96.606322 kWh**;
- mean: **89.100796 kWh**;
- maximum exact historical combination: **97.669742 kWh**.

Validation status:
- **PROVISIONAL REPORT METHOD / RESEARCH ONLY**;
- distinct from frozen R3;
- do not call it R3;
- do not call the P5–P95 range a metrological tolerance or certified confidence interval.

Report label:
- **Estimación estadística de períodos sin telemetría**

---

## S4 — Official Enel tariff publications

Canonical economic reconstruction:
- `research/bill_audit/BILL_OFFICIAL_TARIFF_RECONSTRUCTION_2026-10-06.md`.

Official evidence set:
- Enel Distribución Chile S.A. regulated-supply tariff archive;
- August 2026 retroactive publication;
- September 2026 publication.

Current bill tariff identity:
- printed BT1-T5;
- normal Ñuñoa service column;
- BT_AA/T5 rate identity supported by official table and bill arithmetic;
- RED/network type remains inferred rather than printed evidence and must be labelled accordingly.

Official rates used for current bill:
- August electricity, IVA column: **220.147 CLP/kWh**;
- September electricity, IVA column: **220.539 CLP/kWh**;
- transport, IVA column: **20.489 CLP/kWh**;
- public-service charge, exempt: **0.855 CLP/kWh**;
- administration fixed charge, IVA column: **727.230 CLP/month**;
- FET <=350 kWh: no surcharge for current scenarios.

Cross-month application:
- 4/32 of billed energy allocated to August;
- 28/32 allocated to September;
- rule is calendar-day proportional allocation across the two tariff months.

Report label:
- **Tarifas oficiales Enel aplicables**

---

## S5 — Regulatory / rule evidence

Current rules used:
- tariff application rule for billing periods spanning fractions of two calendar months;
- official public-service component treatment;
- official FET threshold behavior;
- official Ley 21.667 subsidy installment evidence.

Subsidy:
- current printed installment: **-3,758 CLP**;
- fixed monthly installment for this bill;
- preserved across current inverter counterfactual scenarios because all modeled totals remain above the credit.

Common service:
- **5,964 CLP** actual bill evidence;
- not reconstructed from the apartment's disputed kWh;
- preserved as scenario-invariant actual-only evidence.

Report label:
- **Reglas regulatorias y cargos no dependientes del consumo discutido**

---

## S6 — Target-device counter cross-check

Evidence package:
- `investigation-bundle-20261006-183622.zip`;
- SHA-256:
  `c7044f5eeb2bc6e970fa726c39877e77db395260428531d30c1f33065cd22f24`.

Canonical classification:
- `research/bill_audit/HPVINV02_GRID_IMPORT_COUNTER_CLASSIFICATION_2026-10-06.md`.

Result:
- `buyElectricityQuantity`: placeholder, zero-only, `hasRealTimePoints=false`, `isRealValue=false`;
- `dayPurchaseElectricityConsumption`: not populated;
- historical `buyElectricityQuantity`: not populated.

Conclusion:
- no usable independent grid-import energy counter is available from the validated target surfaces;
- `mainsPower` integration remains the canonical inverter-side energy source.

Report treatment:
- this negative result belongs in technical methodology/limitations;
- do not show placeholder zeros as grid energy;
- do not imply that a second counter corroborated the result.

---

## S7 — Economic truth table

Reproducible engine:
- `research/bill_audit/bill_economic_truth_table.py`;
- version: `bill-economic-truth-table.v1`.

Canonical case:
- `research/bill_audit/BILL_ECONOMIC_CASE_2026-10-06.json`.

Economic freeze:
- `research/bill_audit/BILL_OFFICIAL_TARIFF_RECONSTRUCTION_2026-10-06.md`.

Control reconstruction:
- reconstructed total bill: **24,648.128 CLP -> 24,648 CLP printed**;
- reconstructed total due: **26,854.128 CLP -> 26,854 CLP printed**;
- result: exact printed-peso agreement.

Canonical counterfactual totals:
- observed inverter: **24,693 CLP**;
- P5: **24,703 CLP**;
- P50: **24,703 CLP**;
- P95: **26,759 CLP**;
- Enel control: **26,854 CLP**.

Main interpretation:
- tariff/rate structure is internally coherent;
- urgent dispute is about the **97 kWh energy quantity**, not a demonstrated tariff error.

---

## S8 — Frozen R3 research stream

R3 remains separate:
- frozen future-validation candidate;
- current validation state: **INSUFFICIENT**;
- no gate weakening;
- no scoring before stopping rule;
- no use of this bill to calibrate or retune R3.

Report rule:
- do not describe the urgent bill-specific method as R3;
- R3 may be referenced only in technical development traceability if useful.

---

# Evidence-status vocabulary for report

Use only these semantics:

- **IMPRESO / MEDIDO POR ENEL**
- **DIRECTAMENTE OBSERVADO POR EL INVERSOR**
- **ESTIMADO ESTADÍSTICAMENTE DESDE TELEMETRÍA DEL INVERSOR**
- **DERIVADO DE TARIFA OFICIAL**
- **CARGO REAL PRESERVADO / NO DEPENDIENTE DEL kWh DISCUTIDO**
- **INFERIDO / NO IMPRESO**
- **NO DISPONIBLE / NO UTILIZABLE**
- **PROVISIONAL / RESEARCH ONLY**

Do not collapse these evidence classes.

---

# Final provenance gate

The final PDF/anex must allow every material number to answer:

1. what is the number?;
2. which source S1–S8 supports it?;
3. is it printed, directly observed, statistically estimated, tariff-derived, preserved actual-only, inferred, or unavailable?;
4. what code/method/version reproduces it?;
5. what limitations apply?

This provenance table is frozen for the urgent report unless contradictory new source evidence appears.

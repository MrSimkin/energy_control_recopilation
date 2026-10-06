# Urgent Enel audit — execution checkpoint before owner evidence handoff

Date: 2026-10-06

Status: **READY FOR ONE OWNER EVIDENCE COLLECTION PASS**

This checkpoint consolidates the urgent bill-audit work completed after the canonical report specification was frozen.

## Statistical / energy evidence

Canonical deterministic interval harness:
- `research/bill_audit/bill_interval_truth_table.py`
- version `bill-interval-truth-table.v1`.

Canonical provisional report-gap harness:
- `research/bill_audit/bill_gap_calendar_window_empirical.py`
- version `bill-gap-calendar-window-empirical.v1`.

Current bill-period inverter result:
- directly observed import: 88.065413 kWh;
- temporal coverage: 99.334903%;
- uncovered time: 5.101293 h;
- P5: 88.103333 kWh;
- P50: 88.103333 kWh;
- P95: 96.606322 kWh;
- printed Enel billed energy: 97.000 kWh;
- Enel minus provisional P95: 0.393678 kWh / 0.4059%.

R3 remains separate, frozen and INSUFFICIENT.
The report-specific method is not R3 and does not alter R3.

Sensitivity analysis:
- 10/12/15-day historical windows give the same P95 = 96.606322 kWh;
- 20–30-day histories lower P95 materially;
- removing weekday/weekend conditioning also lowers P95;
- the frozen 15-day/daytype method is therefore conservative on the high side rather than selected to maximize disagreement with Enel.

Canonical robustness receipt:
- `research/bill_audit/BILL_GAP_METHOD_SENSITIVITY_2026-10-06.md`;
- commit `164d8d48be704888ac439a462282328bb3d6c88f`.

## Economic evidence already closed

Printed bill arithmetic is internally coherent at peso/display precision:
- taxable + IVA + exempt = bill subtotal;
- Servicio Común - Subsidio = other charges/credits;
- bill subtotal + other charges/credits = total due.

The second-semester 2026 official Ministry of Energy subsidy schedule confirms:
- 2–3-person household monthly installment = CLP 3,758;
- the benefit is a six-installment semestral subsidy;
- therefore the printed -3,758 subsidy is not a per-kWh charge and remains invariant in the inverter energy counterfactual, provided the bill remains large enough to absorb the credit.

Working economic classes for this bill:
- Administración del servicio: FIJO;
- Electricidad consumida: VARIABLE_POR_CONSUMO;
- Transporte de electricidad: VARIABLE_POR_CONSUMO / composite official components;
- Arriendo medidor: FIJO;
- Servicio Común: actual-only invariant for the inverter counterfactual;
- Subsidio eléctrico: fixed/regulated credit for this bill's counterfactual;
- display/reconciliation residual: preserve explicitly rather than fabricate a charge.

Bill-implied verification clues only:
- electricity = 220.505154639 CLP/kWh;
- transport line = 21.360824742 CLP/kWh.

Leading official-source hypothesis:
- September electricity gross/IVA-column rate approximately 220.505 CLP/kWh;
- September transport line likely reconciles as transport + public-service components around 21.361 CLP/kWh.

These remain hypotheses until the official September PDF row is captured and parsed.
Do not publish reverse-calculated rates as official tariff evidence.

## New target-device counter probe

A new read-only diagnostic action was added:

Spanish UI:
- **Comprobar energía comprada a red**

English:
- **Check grid-purchase energy**

Core method:
- `InvestigationDiagnosticsService.CaptureGridImportEnergyEvidenceAsync`.

It performs only authenticated reads and preserves sanitized raw responses.

It queries:
1. device monthly category aggregates for the three most recent months using:
   - endpoint `deviceOverView/stateAttributeSummary/category/monthly`;
   - category `pvInverterElectricityQuantityClass`;
   - important property `buyElectricityQuantity`;
   - preserve `isRealValue`.
2. selected-key history samples for:
   - `dayPurchaseElectricityConsumption`;
   - `buyElectricityQuantity`.

Purpose:
- determine whether the target HPVINV02 exposes a trustworthy independent grid-import energy counter/aggregate;
- detect explicit placeholders;
- compare any real candidate against `mainsPower` integration before use.

No write/control endpoint is used.

Code commits:
- core probe: `8573f1955e54fb7e332d1140f5a988c8a77ce0c0`;
- diagnostics XAML: `41a07af293df534ce5e084af55cceb31e7db07c7`;
- code-behind: `d2009c9e024c718d50aea700214b7366bbd717fd`;
- Spanish resource: `bd9957e74679cadd509152a8ffc61eed2a2c7034`;
- English resource / tested HEAD: `34d36042ef642493cd7880bf6d775e0bdb9f363c`.

## CI / portable build

GitHub Actions Windows Build:
- run number: **499**;
- run ID: `37532423163`;
- head SHA: `34d36042ef642493cd7880bf6d775e0bdb9f363c`;
- conclusion: **SUCCESS**;
- restore: PASS;
- build: PASS;
- SQLite smoke: PASS;
- portable publish/upload: PASS.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: **11444757944**;
- size: 80,784,698 bytes;
- expires: 2027-01-04.

## Required owner evidence pass

One owner/local-machine pass is now justified.

A. Target-device counter evidence:
1. download/extract Build 499 portable;
2. migrate the existing complete portable `Data\` directory into the new folder so the same database and DPAPI session remain available under the same Windows user;
3. launch app and confirm Build 499 identity;
4. open technical diagnostics;
5. click **Comprobar energía comprada a red**;
6. after it completes, click **Exportar paquete de investigación**;
7. return the resulting investigation ZIP.

B. Official tariff source:
1. from Enel's official tariff archive, download:
   - `Enel Distribución Chile SA._Tarifas Suministro Eléctrico 8T_ VAD 5T Septiembre de 2026.pdf`;
2. return that PDF.
3. August retroactive PDF is optional at this gate because earlier evidence is already preserved; attach it too only if convenient.

With those two owner-provided files, the assistant can:
- validate/accept or reject the independent inverter counter;
- close official September tariff/component applicability;
- freeze the economic truth table;
- proceed to report and annex implementation.

No additional owner decisions are required before those evidence files are analyzed.

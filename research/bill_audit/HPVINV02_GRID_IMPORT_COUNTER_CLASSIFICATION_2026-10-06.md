# HPVINV02 grid-import energy counter classification — 2026-10-06

Status: **COUNTER CROSS-CHECK CLOSED / NO USABLE INDEPENDENT GRID-IMPORT ENERGY COUNTER FOUND**

Evidence package supplied from target-PC Build 499:
- filename: `investigation-bundle-20261006-183622.zip`;
- SHA-256: `c7044f5eeb2bc6e970fa726c39877e77db395260428531d30c1f33065cd22f24`;
- size: 5,946,835 bytes.

No customer PII is reproduced in this checkpoint.

## 1. Probe scope

Build 499 queried two target-specific candidate surfaces:

1. monthly category aggregate:
   - category: `pvInverterElectricityQuantityClass`;
   - candidate property: `buyElectricityQuantity`;
   - months: 2026-08, 2026-09, 2026-10.

2. selected-key history:
   - `dayPurchaseElectricityConsumption`;
   - `buyElectricityQuantity`;
   - representative dates across August, September and October 2026.

The probe was read-only.

## 2. Monthly aggregate result

The endpoint itself is functional.

Control property:
- `pvGeneratedEnergy` returns real non-zero daily values;
- `hasRealTimePoints=true`;
- real values are present for the actual days in August, September and October.

Grid-import candidate:
- `buyElectricityQuantity` returns only zero values;
- `hasRealTimePoints=false`;
- every time point is marked `isRealValue=false`.

Same placeholder pattern is also present for:
- `sellElectricityQuantity`;
- `chargeElectricityQuantity`;
- `dischargeElectricityQuantity`;
- `consumeElectricityQuantity`.

Therefore this is not a failed endpoint or empty month. It is property-specific placeholder behavior.

### Classification

`buyElectricityQuantity` on this actual HPVINV02 installation:

**PLACEHOLDER / NOT USABLE AS GRID-IMPORT ENERGY EVIDENCE**

Do not interpret its zero as zero grid import.

## 3. Historical daily-counter result

Selected-key history was requested for:
- `dayPurchaseElectricityConsumption`;
- `buyElectricityQuantity`.

Observed responses:
- some sampled dates return no payload;
- some return one or more timestamps;
- whenever timestamps exist, both requested fields are null;
- one sampled day with 40 returned timestamps still contains 40 null values for both fields.

### Classification

`dayPurchaseElectricityConsumption` on this actual HPVINV02 installation:

**NOT POPULATED / NOT USABLE**

Historical `buyElectricityQuantity`:

**NOT POPULATED / NOT USABLE**

## 4. Consequence for bill audit

No second independent energy counter is available from these validated SiSeLi surfaces for this target device.

The canonical inverter-side grid-import evidence therefore remains:
- raw/normalized `mainsPower` -> `grid_import_power_w`;
- deterministic timestamp-based integration;
- explicit gap accounting;
- provisional report-specific statistical completion for uncovered time.

This negative counter result does **not** weaken or invalidate the existing time-series evidence.

It only means the hoped-for independent same-device counter corroboration is unavailable.

## 5. Current canonical bill-energy result remains unchanged

- directly observed import: **88.065413 kWh**;
- temporal coverage: **99.334903%**;
- provisional report P5: **88.103333 kWh**;
- provisional report P50: **88.103333 kWh**;
- provisional report P95: **96.606322 kWh**;
- printed Enel: **97.000000 kWh**.

The Enel value was not used to construct/calibrate the inverter distribution.

## 6. Implementation consequence

The target-counter gate is now CLOSED.

Do not spend further urgent-path time probing additional undocumented counter aliases unless new evidence specifically justifies it.

Next authorized work:
1. freeze final report provenance/source table;
2. implement the already-frozen energy/economic truth tables in the production bill-audit path;
3. implement the canonical 8-page report;
4. implement the technical annex/raw-data export;
5. bundle one target-PC QA tranche.

## 7. Build 499 export defect discovered during this pass

Two implementation defects were identified while obtaining the evidence:
- bundle export ran synchronously on the WPF UI thread, causing apparent `(No responde)` behavior on large databases;
- Build 499 did not explicitly add `GridImportEnergy*` raw captures to the investigation ZIP.

Both were corrected after evidence collection:
- off-UI-thread export commit: `d706731769c60fd6a0a3d17bd85ab34efdd48fa7`;
- grid-import raw-capture inclusion commit: `7282dfe6170abf32cad0e50277f34f13f3c1f796`.

Windows Builds 501 and 502 completed successfully.

The supplied Build 499 ZIP was still sufficient to classify the counter because the sanitized API diagnostics preserved the relevant request/response bodies.

# Build 533 returned QA checkpoint — 2026-10-06

Status: **ENERGY/ECONOMIC CONTENT PASS / FINAL-PACKAGE DEFECTS FOUND**

Owner returned:

1. `Auditoria-Boleta-Enel-20261006-2102.pdf`
   - bytes: 135,886
   - SHA-256:
     `152f32e14e5fc687c4ab4a15a24e66e144a177729a1925b518d0ebaaf02f1b76`

2. `Anexo-Tecnico-Boleta-Enel-20261006-2105.zip`
   - bytes: 1,547,346
   - SHA-256:
     `99f23cd0240d79f2c98ebbf7f7eb731fcdd2e44c1f084530ccac2e91ba31f85f`

Producer identity inside annex:
- `0.10.0+build.533.bb125601004660a3038e27bb87f9502c865ac50e`.

---

## 1. Main PDF — content verification

Pages:
- 9.

Canonical energy values:
- Enel: **97.000000 kWh** — PASS;
- observed inverter: **88.065413 kWh** — PASS;
- P5: **88.103333 kWh** — PASS;
- P50: **88.103333 kWh** — PASS;
- P95: **96.606322 kWh** — PASS;
- empirical maximum: **97.669742 kWh** — PASS;
- Enel − P50: **8.896667 kWh / 9.17%** — PASS;
- Enel − P95: **0.393678 kWh / 0.41%** — PASS.

P95 interpretation:
- Build 533 correctly no longer presents 97 kWh as statistically impossible/excluded;
- report states full empirical support reaches 97.670 kWh;
- PASS relative to `BILL_P95_POST_QA_VALIDATION_2026-10-06.md`.

Temporal evidence:
- valid frames: 9,180 — PASS;
- median cadence: 5.004 min — PASS;
- continuity threshold: 15.013 min — PASS;
- covered time: 761.899 h — PASS;
- missing time: 5.101 h — PASS;
- temporal coverage: 99.33% displayed / 99.334903% raw — PASS;
- five gaps, boundaries and backtests match canonical evidence — PASS.

Layout:
- previous Build 527 gap-table text overlap is resolved — PASS;
- methodological-controls orphan page is resolved — PASS;
- daily coverage receives a deliberate continuation page — PASS.

---

## 2. Main PDF — economic verification

Tariff model:
- status: `SUPPORTED_MULTI_PERIOD_COMPONENT_MODEL` — PASS;
- August share: 4/32 = 12.50% — PASS;
- September share: 28/32 = 87.50% — PASS;
- August displayed as retroactive — PASS;
- RED: BT_AA inferred;
- ETR: T5 inferred;
- IVA-column model.

Blended modeled rates:
- electricity: **220.490 CLP/kWh**;
- transport + public service: **21.344 CLP/kWh**;
- supported variable total: **241.834 CLP/kWh**.

Enel modeled variable subtotal:
- **23,457.898 CLP** -> displayed **23,458** — PASS.

Comparable total:
- Enel: **26,854 CLP** — PASS;
- observed: **24,693.313 CLP** -> displayed **24,693** — PASS;
- P5/P50: **24,702.483 CLP** -> displayed **24,702** — PASS;
- P95: **26,758.795 CLP** -> displayed **26,759** — PASS.

Therefore the previously failing tariff/economic path is now populated and internally consistent.

---

## 3. Returned annex integrity

Manifest:
- export version: `bill-audit-evidence-annex.v1`;
- statistical method:
  `bill-gap-calendar-window-empirical.v1`;
- status:
  `PROVISIONAL_REPORT_METHOD_RESEARCH_ONLY`;
- `enel_value_used_in_construction=false`.

Every manifest-listed file:
- byte count: PASS;
- SHA-256: PASS.

Files:
- printable numerical PDF;
- frame-level telemetry CSV;
- gap CSV;
- economic scenario CSV;
- source/method text;
- daily-coverage CSV.

Telemetry:
- rows: 9,180;
- sum of `observed_energy_wh_to_next`:
  **88.065412990 kWh**;
- agrees with canonical observed energy at export precision — PASS.

Daily totals:
- frames sum: 9,180;
- covered: 761.898707 h;
- uncovered: 5.101294 h;
- observed: 88.065414 kWh;
- reconstructed coverage: 99.334903% — PASS.

Economic CSV:
- now contains all five scenarios — PASS.

---

## 4. Defects discovered in Build 533

### D1 — printable annex footer overlaps data rows

The 128-page printable numerical annex has insufficient reserved footer space.

Rendered page 1 and page 2 show:
- footer text crossing the last table row;
- data itself remains present in CSV, but the printable annex is not presentation-safe.

Fix:
- increase section bottom margin;
- set explicit footer distance;
- re-render on target for final verification.

Code fix initiated after QA:
- `UtilityBillAuditAnnexExportService`.

### D2 — tariff provenance summary incorrectly looks September-only

Page 3 correctly shows:
- August retroactive + September.

Page 8, however, summarizes:
- `VIGENCIA OFICIAL 2026-09`;
- source text only names September.

This is misleading because the modeled blended rate uses both periods.

Fix:
- multi-period effective label;
- multi-period source summary;
- carry official source URL + content SHA-256 into period evidence;
- append short hashes in report source table;
- include full tariff source hashes/URLs in annex manifest/provenance.

### D3 — aggregate other-charges sign conflicts with printed arithmetic

Build 533 page 3 displays:
- `Otros cargos/ajustes - $2,206`.

But:
- Total bill = 24,648;
- Total due = 26,854;
- therefore net other charges/credits = **+2,206**.

Detailed lines already show:
- Servicio Común +5,964;
- subsidy -3,758;
- net = +2,206.

The negative aggregate came from a manually captured summary value, not from the bill arithmetic.

Fix:
- when Gross Bill and Total Due are available, derive:
  `net other charges = Total Due - Gross Bill`;
- if manually captured aggregate disagrees, do not use it as report authority;
- optionally show a reconciliation note.

### D4 — printed tariff field not captured in local bill record

Page 1 shows an em dash for `Tarifa impresa`, although the external bill evidence is known to contain a printed tariff.

The report must not imply that the original bill omitted the field merely because the local record did not capture it.

Fix:
- show `No capturada en el registro local` instead of an em dash when local bill data is blank;
- keep RED/ETR clearly marked as inferred/model evidence.

### D5 — wording clarity

Fixes initiated:
- explain why P5 = P50 for this interval:
  empirical median contribution of each internal gap = 0 kWh;
- clarify that fixed/subsidy/etc. are outside the **variable subtotal**, not outside the comparable scenario total;
- make H03 sign convention explicit:
  `Enel - P50`;
- add explicit fixed/conditional-charge treatment table on economic decomposition page.

---

## 5. Overall Build 533 decision

**NOT FINAL FOR DELIVERY YET**

But:
- deterministic energy evidence: PASS;
- provisional statistical result: PASS;
- P95 semantics: PASS;
- multi-period economic reconstruction: PASS;
- annex digital-data integrity: PASS;
- previous export crash: PASS/fixed;
- previous gap-table overlap: PASS/fixed.

Remaining work is:
- provenance/presentation hardening;
- annex-print footer repair;
- aggregate charge-sign reconciliation;
- one final target-render QA.

No recalibration or retuning of P5/P50/P95 is authorized from these findings.

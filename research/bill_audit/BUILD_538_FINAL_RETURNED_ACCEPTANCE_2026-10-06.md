# Build 538 final returned acceptance — 2026-10-06

Status: **ACCEPTED FOR CURRENT ENEL EVIDENCE PACKAGE**

Owner returned:

1. `Auditoria-Boleta-Enel-20261006-2123.pdf`
   - bytes: 138,305
   - SHA-256:
     `0030557b15821ba6ec2c489f2a93dc3fb1d5a9fa39f8dc065c683df84fa35f40`

2. `Anexo-Tecnico-Boleta-Enel-20261006-2130.zip`
   - bytes: 1,464,023
   - SHA-256:
     `633e65bf897a60a8c6183761510432a321c362fbf7007540ed158c3fa73f3844`

Producer:
- `0.10.0+build.538.c21e07a3f67f44c589ab5a076142f622e218517b`.

---

## 1. Main audit PDF

Pages:
- 9.

Visual QA:
- no text overlap detected;
- previous gap-table overlap remains fixed;
- page-3 economic tables fit;
- daily coverage continuation is clean;
- tariff-provenance page is clean;
- footer is outside content on all nine rendered pages.

Canonical energy values remain unchanged:
- Enel: 97.000000 kWh;
- observed inverter: 88.065413 kWh;
- P5: 88.103333 kWh;
- P50: 88.103333 kWh;
- P95: 96.606322 kWh;
- maximum empirical combination: 97.669742 kWh.

Displayed comparisons:
- Enel - observed: +8.935 kWh / +9.21%;
- Enel - P50: +8.897 kWh / +9.17%;
- Enel - P95: +0.394 kWh / +0.41%.

P95 semantics:
- PASS;
- 97 kWh is not presented as statistically impossible/excluded;
- the report discloses full empirical support to 97.670 kWh;
- P5=P50 is now explicitly explained by the zero empirical median gap contribution.

Method status remains:
- `bill-gap-calendar-window-empirical.v1`;
- `PROVISIONAL / RESEARCH ONLY`;
- R3 remains separate and frozen.

---

## 2. Economic model

Multi-period model:
- PASS;
- status `SUPPORTED_MULTI_PERIOD_COMPONENT_MODEL`.

Applied periods:
- 2026-08-28..2026-08-31:
  August retroactive, 4 days, 12.50%;
- 2026-09-01..2026-09-28:
  September standard, 28 days, 87.50%.

Modeled rates:
- electricity consumed: 220.490 CLP/kWh;
- transport + public service: 21.344 CLP/kWh;
- supported variable subtotal rate: 241.834 CLP/kWh.

Comparable totals:
- Enel: 26,854 CLP;
- observed: 24,693 CLP;
- P5/P50: 24,702 CLP;
- P95: 26,759 CLP.

Fixed/conditional treatment is now explicit:
- subsidy installment: conditional/regulatory, preserved actual credit;
- administration: fixed, preserved;
- meter rent: fixed, preserved;
- common service: preserved actual, not recalculated from disputed individual kWh.

Aggregate bill arithmetic:
- gross bill: 24,648 CLP;
- total due: 26,854 CLP;
- net other charges/credits is correctly derived as +2,206 CLP;
- the old manually captured -2,206 value is explicitly rejected as authority because it does not reconcile with bill totals.

---

## 3. Tariff provenance

Main report now shows:
- August retroactive + September effective periods;
- official publication titles;
- short SHA-256 identifiers.

Annex manifest/provenance includes:
- publication IDs;
- effective dates;
- retroactive flag;
- applied date ranges;
- weights;
- title;
- source URI;
- full content SHA-256.

Tariff hashes:
- August retroactive:
  `176dfe6e268e2fd918b1f4f21ebf0f18bfa01c7811acab4bedc571de6a01fa9f`;
- September:
  `27b65928d47c34d46da4afa25c094734432be860750dac31e1deddb6a4d3ab17`.

Known data-capture limitation:
- local bill record does not contain the printed tariff-plan field;
- report now says `No capturada en el registro local` rather than falsely implying the original bill omitted it;
- this is not used to select P5/P50/P95.

---

## 4. Annex integrity

Printable annex:
- 132 landscape pages;
- selected visual checks at beginning, middle and final pages PASS;
- footer no longer overlaps table rows;
- final page renders cleanly.

Manifest:
- every listed file byte count PASS;
- every listed SHA-256 PASS.

Telemetry CSV:
- rows: 9,180;
- observed-energy sum:
  88.065412990 kWh;
- agrees with canonical report value at export precision.

Daily coverage CSV:
- samples: 9,180;
- covered: 761.898707 h;
- uncovered: 5.101294 h;
- observed: 88.065414 kWh;
- coverage: 99.334903%.

Economic scenario CSV:
- all five scenarios present;
- values agree with PDF.

---

## 5. Current decision

The Build 538 returned PDF + ZIP are accepted as the current evidence package for the live Enel dispute.

No further statistical retuning is authorized merely to widen or narrow the discrepancy.

The current evidence statement is intentionally conservative:
- central independent estimate is materially below the billed value;
- provisional P95 is very close to Enel;
- full empirical support includes 97 kWh;
- tariff reconstruction does not indicate a material tariff-rate error;
- the dispute is centered on billed energy quantity and its meter/read traceability.

---

## 6. Separate next technical tranche — NOT required to use the current report

After the current bill package is stabilized, implement a tariff-publication provenance/resolution improvement:

### Tariff duplicate/version precedence

Do not select by:
- SQLite row ID;
- capture time;
- update time;
- parser-local CandidateIndex.

Prefer:
1. effective period;
2. official publication identity;
3. explicit official correction/rectification relationship;
4. latest official correction that supersedes the prior publication;
5. semantic tariff identity:
   municipality/territory + tariff + RED + ETR + component + unit + rate column.

Equivalent duplicate rows:
- collapse only if rates and semantic identity agree.

Conflicting rows within the same selected publication:
- do not choose arbitrarily;
- mark ambiguous and inspect parser/table identity.

Successive official corrections:
- build an explicit supersession chain.

Recommended future metadata:
- official_publication_date;
- official_resolution_number;
- is_correction;
- corrects_resolution_number;
- supersedes_publication_id;
- supersession_source;
- is_current_for_period.

### Tariff acquisition improvement

Preferred pipeline:
1. official Enel tariff catalog discovery;
2. extract official PDF hrefs;
3. direct-download static `content/dam` PDF assets;
4. validate HTTP/PDF magic/hash/page count;
5. normalize;
6. use CNE as regulatory cross-check / correction graph;
7. browser-assisted import only as fallback.

Do not infer tariff precedence from filename recency alone.

This tranche is separate from the accepted Build 538 current-bill evidence package.

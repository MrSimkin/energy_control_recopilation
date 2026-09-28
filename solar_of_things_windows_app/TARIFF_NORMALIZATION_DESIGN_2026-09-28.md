# Enel tariff normalization and bill-audit design

Date: 2026-09-28

Status: **ACTIVE DESIGN — IMPLEMENT AFTER SOURCE-CAPTURE FOUNDATION**

## Evidence confirmed from official Enel material

The official Enel regulated-supply archive is organized by year and can contain:
- one normal publication for a month;
- later retroactive publications affecting the same effective month;
- multiple publication variants for the same month in older years.

The official residential tariff PDF is not a single CLP/kWh value.

For BT1/residential material it exposes, among other evidence:
- fixed monthly charge;
- public-service charge;
- transmission-system components;
- total electricity transport;
- energy charge;
- power/purchase components;
- total/electricity-consumed rates;
- RED/network type;
- ETR residential-equity tranche;
- FET consumption-band recarges;
- commune-dependent columns;
- paired official columns labelled Neto / IVA.

The document itself states that BT1 total tariff selection depends on:
- client connection-network type (RED);
- residential tariff-equity tranche (ETR);
- applicable commune/service column.

Therefore no parser may promote one numeric rate to “the applicable tariff” merely because it appears in the PDF.

## Tax/VAT rule for implementation

The user confirmed Chilean VAT is 19%, but does not want taxable applicability guessed.

Preferred evidence order:

1. where the official Enel tariff publication provides paired columns labelled Neto / IVA, preserve both published values exactly as source evidence;
2. do not assume the published IVA-column value is simply a gross/net×1.19 value unless the document/rule for that component establishes that interpretation;
3. do not independently infer that every line is taxable merely by multiplying by 1.19;
4. preserve actual bill taxable/exempt/IVA totals as printed evidence;
5. only calculate VAT independently where an authoritative rule clearly defines the taxable base.

This avoids inventing tax treatment.

## Required normalization layers

### Layer 1 — publication evidence

Already captured / in progress:
- provider;
- source URL;
- official title;
- effective month;
- retroactive flag;
- local original PDF;
- SHA-256;
- page count;
- extracted page text;
- capture state.

### Layer 2 — normalized tariff candidates

A parser should extract every candidate official rate without yet claiming service applicability.

Candidate record should preserve:
- publication ID;
- page;
- tariff family/plan (for example BT1);
- component key;
- printed component description;
- unit;
- RED when the row is RED-specific;
- ETR when the row is ETR-specific;
- commune/service column identity when deterministically parsed;
- net rate;
- raw published IVA-column value when supplied, without prematurely interpreting it as gross;
- source coordinates/text evidence or equivalent parser provenance;
- parser version;
- validation state.

### Layer 3 — supersession / retroactivity resolution

For each effective period:
- preserve all publications;
- explicitly mark retroactive/corrective relationships when determinable;
- do not delete the superseded publication;
- re-evaluate audits affected by a newly discovered retroactive publication.

Safe rule:
- a later publication marked Retroactivo for the same effective month may supersede the earlier same-family publication only after document-family/effective-period validation;
- multiple non-retroactive variants for the same month remain unresolved variants until their applicability is established;
- never choose merely by filename sort or capture time.

### Layer 4 — service applicability

The app must resolve enough evidence to select a rate:
- tariff plan printed on the bill;
- commune/service territory;
- RED/network type;
- ETR tranche;
- FET/consumption band where applicable;
- effective tariff version.

The UX should minimize manual configuration.

Preferred workflow:
1. use bill evidence when it contains an explicit value;
2. compare printed bill unit rates with official candidate rates as a verification clue;
3. auto-propose service/RED/ETR only when evidence is sufficiently unique;
4. otherwise ask the user to choose/confirm from official options;
5. never silently infer a unique service configuration from an ambiguous numeric match.

## Rate verification before full service resolution

A useful intermediate audit state is **official-rate verified**.

If a bill line contains a printed unit rate and that exact rate is found in the authoritative publication for the correct component/effective period:
- report that the printed unit rate is supported by official source evidence;
- if several service columns share the same value, say the rate is verified but service-column identity is not unique;
- do not claim commune/RED/ETR certainty solely from a non-unique rate match.

If the printed rate does not appear among valid candidates:
- flag it for review;
- do not automatically call it an Enel error.

## Bill reconstruction scope

Reconstruct only components with authoritative rules/evidence.

Target examples:
- fixed monthly charge when applicable;
- electricity consumed / regulated energy component;
- electricity transport / transmission component;
- public-service/FET components where rules are determinable;
- other official regulated components when the parser/rule model supports them.

Actual-only evidence remains actual-only where no independent rule exists:
- common-service charges;
- external/third-party charges;
- discretionary or account-specific adjustments;
- other lines lacking authoritative derivation.

For every line, the audit should allow states such as:
- VERIFIED / reconstructed;
- RATE VERIFIED, applicability partially unresolved;
- ACTUAL ONLY / not independently reconstructable;
- MISSING SOURCE;
- AMBIGUOUS;
- MISMATCH requiring review.

## Bill interval and tariff changes

A bill can span more than one tariff-effective period.

The eventual engine must:
- identify each tariff version effective inside the bill interval;
- split calculations when required by the official billing rule;
- use Solar of Things time-resolved import only where it is semantically appropriate to allocate energy;
- not assume the entire bill uses the end-date tariff unless evidence/rules say so.

This must be researched/validated before claiming authoritative reconstruction.

## Reporting

### Reading comparison PDF

Purpose:
- arbitrary saved reading A -> reading B;
- meter difference vs Solar of Things total grid import;
- quality/coverage/boundary assumptions;
- no bill/tariff conclusion.

### Enel bill audit PDF

Purpose:
- starts from one selected bill;
- linked official reading boundaries;
- billed energy + meter delta + Solar of Things comparison;
- uncertainty/sensitivity interpretation;
- applicable official tariff version(s);
- reconstructed verifiable components;
- actual-only components preserved;
- actual vs expected line comparison;
- explicit source/version traceability;
- no claim of provider error beyond evidence.

## Next implementation gate

Before declaring tariff reconstruction usable:
1. year-based source acquisition must work on target PC;
2. parser must be tested against multiple real official publications, including a retroactive case;
3. normalized candidates must retain provenance;
4. applicability must not be guessed;
5. bill-audit PDF must distinguish verified, ambiguous and actual-only lines.


## 2026 cross-publication BT1 validation — 2026-09-28

Official Enel January 2026 (24T), August 2026 retroactive (8T) and September 2026 (8T) publications were cross-checked.

Confirmed across all three:
- BT1 residential tables use ETR **T1 through T6**:
  - T1 <= 200 kWh prior-year average;
  - T2 >200 <=210;
  - T3 >210 <=220;
  - T4 >220 <=230;
  - T5 >230 <=240;
  - T6 >240;
- connection-network types remain BT_AA / BT_SA / BT_AS / BT_SS;
- the official table says BT1 selection depends on RED + ETR;
- FET bands/rates shown in these three publications are the same;
- the table presents repeated territorial/service columns with paired published Neto / IVA values.

Important parser consequence:
- PdfPig content-order text does not necessarily emit the explanatory labels before the RED/ETR numeric rows.
- Therefore RED/ETR row semantics must not be assigned solely from nearest preceding text.

Safe semantic identity available in the official table:
- **Electricidad consumida (6) = Cargo por energía (3) + Cargo por compras de potencia (4) + Cargo por potencia base / distribución (5).**

Parser v2 may use this arithmetic identity to classify a pair of previously raw RED/ETR blocks only when the published values satisfy the identity within a small numeric tolerance.
- the earlier row becomes `POWER_BASE_DISTRIBUTION`;
- the matching sum row becomes `ELECTRICITY_CONSUMED`;
- any row that does not satisfy the identity remains raw/unapplied.

This is a semantic classification of official evidence, **not** customer applicability. It still does not identify the user's commune/service column, RED or ETR.

Historical caution:
- 2020–2026 archive titles/layouts include different decree/vintage and T1/T2/T3 document variants;
- do not assume a 2026 territorial column map applies unchanged to every historical year;
- commune/service-column resolution requires separate version-aware validation.

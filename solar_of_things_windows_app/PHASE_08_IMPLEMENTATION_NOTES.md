# Phase 8 — Utility Meter Readings and Consumption Reconciliation — Implementation Notes

Date started: 2026-09-27

Status: **IN PROGRESS — FIRST FUNCTIONAL CHECKPOINT**

## Goal

Compare real utility-meter/bill evidence with inverter-derived **total grid import** over exactly the same interval.

Canonical semantic rule:
- `Utility / Enel → House` is a household-source attribution metric and is **not** the meter/bill comparison value;
- `total grid import` is the inverter-derived quantity used for utility-meter/bill reconciliation.

## First implementation checkpoint

Database:
- schema v9;
- `utility_meter_reading`:
  - exact UTC timestamp;
  - cumulative kWh;
  - optional reference;
  - optional notes;
- `utility_bill`:
  - exact start/end UTC;
  - optional billed kWh;
  - optional CLP amount;
  - optional invoice/reference;
  - optional notes.

Core:
- `UtilityMeterRepository`;
- `UtilityReconciliationService`;
- consecutive meter readings are converted into interval consumption by cumulative difference;
- inverter grid import uses `EnergyRangeStatisticsService` over the exact same timestamps;
- outputs:
  - signed inverter-minus-meter difference;
  - absolute difference;
  - percentage difference;
  - inverter coverage;
  - quality label;
- a decreasing cumulative meter value is treated as possible reset/replacement and is not silently converted into consumption;
- bill records can also be compared against inverter import over their exact recorded interval.

UI:
- the existing `Red eléctrica / Grid & Utility` navigation item now has a real page;
- cumulative-reading entry uses date + exact local `HH:mm`;
- ambiguous/invalid DST-local times are rejected rather than guessed;
- reading history;
- automatic reading-to-reading reconciliation table;
- latest reconciliation summary cards;
- optional utility-bill entry and comparison table;
- delete actions for corrections.

## Validation

A deterministic Windows smoke test now covers:
- schema v9;
- two cumulative meter readings;
- expected meter consumption;
- inverter total-grid-import integration over the exact same interval;
- near-zero reconciliation difference in a synthetic matched case;
- optional bill persistence and reconciliation.

## Target-PC acceptance still required

Phase 8 is **not complete** until real household evidence is entered.

Next target test should:
1. reuse the existing real portable `Data\`;
2. open `Red eléctrica`;
3. enter at least two real cumulative Enel meter readings with the real reading timestamps;
4. verify calculated meter consumption;
5. compare against Solar of Things total grid import and coverage;
6. optionally enter a historical bill with exact period timestamps;
7. report semantic/UX anomalies.

Do not fabricate a meter reading or force a comparison across mismatched time windows.


## Target feedback tranche — Build 342 — 2026-09-27

Real target-PC use of the first Phase 8 build exposed several product requirements that are now implemented.

### Meter reading evidence

Readings now distinguish:
- `UTILITY_OFFICIAL`: utility/Enel reading whose source provides a calendar date but no trustworthy reading time;
- `PERSONAL`: user-observed physical-meter reading with an exact observed time.

Timestamp evidence is separate from the numerical cumulative kWh reading:
- `EXACT`;
- `DATE_ONLY` with an explicit calculation assumption.

Current default for official date-only readings:
- preserve them as date-only evidence;
- use local `00:00` as a visible calculation boundary assumption;
- never present the assumed hour as if Enel supplied it.

Migration v10 classifies prior Phase 8 rows conservatively from their existing reference text when recognizable; uncertain rows remain unspecified rather than guessed.

### Arbitrary reconciliation

The user is no longer limited to consecutive readings.

The Grid & Utility page now supports selecting any stored:
- start reading;
- end reading;

and recalculates:
- utility-meter cumulative difference;
- Solar of Things total grid import over exactly that selected interval;
- signed kWh difference;
- percentage difference;
- coverage;
- timestamp-evidence basis;
- quality state.

The consecutive-reading table remains as a convenient historical overview.

### Bill evidence model

A utility bill can now:
- link directly to its saved previous/current meter readings;
- use those linked readings as the authoritative comparison interval;
- preserve manually entered dates only as a fallback when no reading linkage is available;
- store meter start/end values;
- store tariff-plan text as printed;
- preserve billed kWh;
- preserve taxable amount, VAT, exempt amount, gross bill amount, signed other charges and total due;
- preserve arbitrary charge/credit lines in `utility_bill_line`.

Bill-line storage is intentionally flexible rather than a closed list. It can preserve, without reinterpretation:
- service/fixed charges;
- electricity-consumption charges;
- network/transmission charges;
- meter/services charges;
- common-service items;
- subsidies/credits;
- accumulated/tax lines;
- future provider-specific line descriptions.

Signed credits/debits are preserved as entered. The application must not silently make bill arithmetic reconcile by changing line signs or meanings.

### Transversal date UX

A reusable `QuickDatePicker` now combines:
- normal precise day selection;
- direct month selection;
- direct year selection.

It is applied to:
- manual historical capture start;
- Analysis from/to;
- Grid & Utility reading dates;
- Grid & Utility bill fallback dates.

Reports retain their already-implemented fast month/year selectors.

### External reconciliation report

The selected arbitrary reading range can export a PDF intended for technical review with the utility.

It includes:
- the two meter readings and their evidence source;
- exact or assumed timestamp basis;
- meter consumption;
- Solar of Things total grid import;
- signed/percentage difference;
- inverter-data coverage;
- quality state;
- linked bill summary and stored bill lines when available;
- explicit limitations and calculation convention.

It explicitly states that:
- the inverter comparison uses total grid import, not `Enel → Casa`;
- missing telemetry is not extrapolated;
- date-only utility timestamps are assumptions, not official times;
- inverter telemetry is comparison evidence and does not replace the certified utility meter.

### Build validation

Validated code checkpoint:
- code commit: `7ac676812497679402beb4593773b3cd3c3c6419`;
- Windows Build 342 / run `36361589847`;
- restore PASS;
- build PASS;
- schema-v10 smoke PASS;
- arbitrary reconciliation smoke PASS;
- linked-bill + signed-credit-line smoke PASS;
- reconciliation-PDF smoke PASS;
- publish PASS;
- artifact upload PASS;
- artifact ID: `10945173432`;
- SHA-256: `eb877687fb5491e1ae662caa4acc9f9c36e309f6730dd24590902e461df040aa`.

Phase 8 remains IN PROGRESS pending target-PC validation of this evidence model and report.

### Phase 9 / 10 continuity

The tariff/bill-audit scope is not removed or folded into a single CLP/kWh setting.

After Phase 8 meter/bill evidence capture is accepted:
- Phase 9 remains the official tariff acquisition/versioning engine;
- Phase 10 remains tariff-aware bill reconstruction and actual-bill reconciliation.

The expanded Phase 8 bill-line evidence is intentionally designed as the actual-bill side of the later Phase 10 audit.


## Build 349 — scroll, reconciliation PDF clarity, official tariff source capture — GREEN — 2026-09-27

Target feedback from Build 342:
- outer-page mouse-wheel scrolling was expected even when the pointer is over nested grids;
- the one-page reconciliation PDF was not sufficiently clear for external presentation;
- previous tariff research existed but no official tariff source had yet been captured in-app.

Build 349 response:
- outer page ScrollViewers now explicitly consume mouse-wheel input and scroll the containing page;
- reconciliation PDF redesigned as a two-layer document:
  - Page 1 executive result, plain-language interpretation, coverage/time-assumption caveats and comparison definitions;
  - Page 2 evidence, reading traceability, linked bill detail and methodology;
- internal quality token `ASSUMED_TIME` is no longer shown raw in the PDF;
- if no bill is linked to the exact selected reading pair, the PDF says so explicitly.

Important evidence rule:
- a linked bill is included only when the exported pair exactly matches its saved from/to reading IDs;
- therefore an export 29/07 → 27/09 does not include a bill linked 29/07 → 27/08.

Phase 9 foundation is now started ahead of full Phase 8 closure because tariff-source visibility is required by current bill QA:
- schema v11;
- official Enel 2026 supply-tariff archive discovery;
- official PDF download into portable `Data/Tariffs/Enel/2026`;
- SHA-256/content length/page count;
- effective month and retroactive flag;
- per-page born-digital PDF text extraction using PdfPig;
- persistent `tariff_publication` and `tariff_publication_page_text`;
- visible `Tarifas oficiales Enel` panel in Grid & Utility.

Deliberate boundary:
- **captured official source != normalized/applicable tariff**;
- the next Phase 9 tranche must determine/normalize applicable values by plan, commune/network classification, ETR/protection classification and publication version/supersession;
- no rate is labeled as “applied to this bill” before that evidence exists.

Validation:
- code commit: `74a69282fa912c148ca8dedbed6495018bfd0560`;
- Windows Build 349 / run `36363960224`;
- restore PASS;
- build PASS;
- schema-v11 smoke PASS;
- Phase 8 reconciliation/PDF smoke PASS;
- deterministic tariff catalog discovery + retroactive flag smoke PASS;
- tariff source persistence/page-text smoke PASS;
- publish/upload PASS;
- artifact ID: `10946815978`;
- SHA-256: `cbdd9cb34bba48afc9056e799d30e59bdd1dcaad59c283ecd64542afe783e10d`.

## Build 349 target QA correction — 2026-09-28

The current Phase 8 generic reading-pair export is NOT the final Enel audit workflow.

Target QA generated a report from:
- official Enel reading: 2026-08-27, date-only boundary;
- personal reading: 2026-09-27 17:56;
- meter delta: 97.400 kWh;
- Solar of Things total grid import: 84.080 kWh;
- signed difference: -13.320 kWh (-13.68%);
- Solar of Things coverage: 99.3%;
- no bill linked to that exact reading pair.

This proves the generic selector can produce an analytically valid personal comparison that is nevertheless the wrong artifact for bill audit.

Required Phase 8/9/10 separation:
- Personal reconciliation: arbitrary reading pair, exploratory/private analysis.
- Enel bill audit: selected bill -> linked official readings / exact audit interval -> Solar of Things comparison -> tariff reconstruction -> actual-vs-expected bill components -> uncertainty/statistical interpretation -> external PDF.

The bill-audit path must not require the user to manually select the correct reading pair when the bill already owns that relationship.

### Confidence / uncertainty requirement

CoveragePercent is not a confidence interval and must not be presented as metrological confidence.

Before a kWh difference can be described as materially inconsistent, the audit must account for the evidence that can affect the comparison, including:
- missing telemetry / coverage;
- uncertainty from date-only Enel reading boundaries;
- timestamp alignment/cadence;
- justified measurement/sensor uncertainty where evidence exists;
- integration sensitivity around gaps and boundaries.

The audit needs an explicit uncertainty/sensitivity interval and a documented comparison test or decision rule. If available evidence cannot support a formal probabilistic confidence interval, the UI/report must say so and use a transparent sensitivity bound instead of inventing precision.

### Tariff acquisition correction

Build 349 returned 0 discovered / 0 captured publications on the target PC.

The current code is also explicitly year-locked:
- Capture2026SupplyTariffsAsync;
- Discover2026SupplyTariffs;
- title filter requires 2026;
- effective-date parser constructs dates with year 2026;
- local cache path includes Enel/2026;
- UI button says Capturar / actualizar fuentes oficiales 2026.

This must be replaced by historical, bill-driven official tariff acquisition. The system should request the years/versions needed by stored bills and permit explicit broader historical capture. Do not assume only the current year matters.

### Enel-facing report requirement

The Enel-facing PDF is a contraloría/audit artifact, not merely a household dashboard export. It must:
- identify the exact bill, official readings, assumptions and sources;
- reconstruct applicable tariff components with source/version traceability;
- compare actual bill lines to expected values;
- show energy comparison with uncertainty/sensitivity and statistical interpretation;
- distinguish certified Enel meter evidence from independent inverter telemetry;
- explain findings in language suitable for a technically informed dispute or inquiry without claiming provider error beyond the evidence.


# Family Reporting Functional Design — second-reading clarification

Date: 2026-09-26

Status: **CANONICAL Phase 7 functional clarification**

Applies to:
- Simple Energy Report;
- family-facing pages of PDF/XLSX exports;
- report narrative/insight generation;
- event and pattern analysis over any selected period.

This document refines the reporting requirements in `PRODUCT_FUNCTIONAL_SPEC_V1.md` after real-PC QA of the first working exporter.

## 1. Product principle

The family-facing report must **explain how the house behaved**, not merely enumerate what the inverter measured.

The first reporting implementation proved that presets, XLSX/PDF generation, detailed tables, coverage and missing-data semantics can work mechanically. Real-PC review also showed that a technically correct summary can still fail the actual household use case if the reader must interpret engineering metrics to answer ordinary questions.

The family report is therefore not just a shorter technical report. It is a separate presentation/interpretation layer backed by the same validated data.

## 2. Audience and selected-period rule

Primary family audience:
- older/nontechnical household readers;
- the target household's recurring questions must be answerable with a quick glance.

All wording and analysis must be relative to **the selected period**.

Never assume that a report is a week merely because a QA example used seven days. A selected period may be days, weeks, months, many months or an arbitrary custom range.

The report engine must adapt aggregation, event counts and trend granularity to the selected period.

## 3. Page 1 — the four primary household questions

Page 1 exists to answer, in this order:

1. **How much did we use from Enel / the grid?**
2. **How much of the house consumption came directly from the solar panels?**
3. **How much of the house consumption came from the battery?**
4. **How often, and for how long, did the battery not cover the night before the house needed the grid?**

For the configured target household, “Enel” is acceptable family wording when the configured utility is Enel. Generic/product wording remains “red eléctrica” or the configured utility name.

### 3.1 Preferred source-attribution presentation

When attribution is supportable, show three large household-source quantities side by side:

- From utility/grid → house
- Direct solar → house
- Battery → house

Also show:
- total house consumption for the selected period;
- each source's share of house consumption when supportable.

These values answer “where did the energy used by the house come from?” They are not the same as:
- total PV generation;
- total grid import when grid-to-battery or losses may exist;
- total battery charge/discharge movement.

Do not replace an unavailable primary answer with a different metric merely because that different metric is available.

If a source-attribution value is not yet supportable, show it as unavailable/insufficiently supported rather than fabricating it.

### 3.2 “Battery did not cover the night” event

Do not define this as “any grid use at night”.

For the target installation, a strong candidate event is:

1. the battery reaches the validated normal transfer-to-grid SOC threshold (currently approximately 20%);
2. grid use begins/continues while PV is absent or insufficient;
3. this occurs before adequate solar returns or before the installation returns to normal battery/SBU behavior.

The implementation must use validated inverter settings when available, with the family policy only as expected/fallback context.

Report both:
- number of qualifying episodes;
- total qualifying duration.

Where useful also show:
- each episode date/time;
- SOC at event;
- start/end time;
- duration;
- recovery time/condition.

Do not call this “battery failure”. Family wording should be equivalent to:
“the battery reached its normal reserve and the house needed the grid before sufficient solar returned.”

## 4. Page 2 — whole-system conversation

Page 2 answers the secondary questions that naturally follow after Page 1.

Priority quantities:

### 4.1 Total solar generation

“How much did the panels produce in total?”

Show total PV energy generated over the selected period with coverage/provenance handled by the normal reporting quality rules.

This is distinct from direct PV → house.

### 4.2 Total house consumption

“How much energy did the house use, regardless of source?”

Show total house energy consumption over the selected period.

### 4.3 Battery movement

Secondary, not headline-source attribution:
- battery energy charged;
- battery energy discharged/delivered;
- useful SOC context.

This belongs on Page 2 or the Battery report, not as the main substitute for “how much of our house consumption came from the battery?”

### 4.4 Energy that could not be used

The household question “how much solar energy could we not take advantage of?” is valid, but a numeric answer is allowed only when supported.

Do **not** calculate unused/lost solar as a naive remainder such as:

`PV generated - PV used`

With zero export, full battery and curtailed PV, potential solar that was never produced cannot be reconstructed from generated energy alone.

Only report lost/curtailed solar kWh if:
- the device/API exposes a validated curtailment/potential metric; or
- a later validated model can estimate it with explicit provenance and uncertainty.

Until then, show the question as not currently measurable with sufficient confidence rather than inventing a number.

## 5. Page 3 — Patterns and events of the selected period

Page 3 is **required**, not optional decoration.

Purpose:
- explain recurring behavior;
- expose repeated events;
- show robust time-of-day tendencies;
- show how behavior changes over the selected period;
- create useful household conversation without forcing the reader into raw tables.

The page title may be equivalent to:
**“Patrones y eventos del período”**.

### 5.1 Events are not patterns

An **event** is one concrete occurrence.

Example:
- battery reached 20% on 2026-09-21 at 04:12.

A **pattern** is a supported conclusion from repeated observations.

Example:
- when the battery reaches the normal reserve, it tends to do so around 04:00–05:00.

The system must never turn a single event into a “typical” pattern.

### 5.2 Preserve all event occurrences

If an event occurs multiple times, keep all occurrences.

Do not reduce repeated events to only:
- the first;
- the last;
- the most extreme example.

The summary may condense them, for example:
- “battery reached 20% 73 times”;
- “typical time: 04:31”;
- “80% of observed cases occurred between 03:42 and 05:26”.

But the report/export must preserve the underlying occurrence list, directly or in the technical/events annex.

If there are too many events for the family page:
- summarize count/distribution on Page 3;
- include the complete list in an Events sheet/table/annex.

### 5.3 Candidate event families

Where supported by the measured corpus:

- battery reaches normal transfer threshold;
- battery reaches emergency/protected thresholds;
- return to battery/SBU after low-SOC transfer;
- qualifying night-shortfall/grid episodes;
- periods of material grid use;
- high/low house-load episodes;
- high PV production episodes;
- battery charge/discharge episodes;
- alarms/faults when later included and relevant.

### 5.4 Robust hourly patterns

The engine should detect recurring time-of-day behavior such as:

- hours of highest **typical** household consumption;
- hours of highest **typical** solar generation;
- hours with greatest **typical** grid dependence/use;
- typical time the battery reaches normal reserve;
- typical recovery/return time;
- typical nighttime consumption profile;
- typical SOC entering and leaving the night.

Do not infer “typical” from one maximum or a raw arithmetic average alone.

Prefer robust summaries such as:
- median;
- quantiles/ranges;
- frequency of occurrence across eligible days/nights;
- number of observed opportunities;
- sufficiently populated time buckets.

Example family wording:

“Between 19:00 and 22:00 the house was in one of its higher-consumption periods on 72% of observable days.”

### 5.5 Night-pattern analysis

Useful night-level comparisons include:

- observable nights;
- nights fully covered without reaching normal reserve;
- nights reaching the normal transfer threshold;
- qualifying grid-use duration after threshold;
- SOC at the beginning of the night;
- SOC before sufficient solar returns;
- nighttime house consumption;
- time of reserve event;
- time/condition of recovery.

A later analysis may compare nights where the battery covered the full night with nights where it did not, for example:
- typical starting SOC;
- typical nighttime consumption;
- typical reserve-arrival time.

The report must not imply causality where only association is demonstrated.

### 5.6 Evolution across the selected period

For longer ranges, detect meaningful changes over time.

Granularity should adapt to the selected period and available coverage:
- daily for short ranges;
- weekly or monthly for longer ranges;
- never hard-code “week”.

Candidate evolution metrics:
- house consumption;
- PV generation;
- grid import/dependence;
- battery reserve-event frequency;
- night coverage;
- SOC behavior;
- other validated source-attribution metrics.

This allows statements such as:
- reserve reached on X of Y observable nights in each month;
- grid usage increased/decreased across months;
- solar generation shifted materially across the period.

### 5.7 Dynamic number of insights

Do not force a fixed count of insights.

If only two patterns are statistically/evidentially supportable, show two.

If eight are strong and useful, show eight.

Never fill visual space with weak or speculative conclusions.

## 6. Coverage and observability are part of the analysis

Coverage is not only a footer.

Every event-frequency or pattern statement must use **observable opportunities**, not the nominal calendar denominator.

Example:

Selected period:
- 30 nights total;
- 19 nights sufficiently observable;
- 8 observed reserve events;
- 11 nights insufficiently observed.

Correct:
- “battery reached the reserve in 8 of 19 observable nights.”

Incorrect:
- “8 of 30 nights” if the other 11 are unknown.

Unknown/missing is never evidence that an event did not occur.

Pattern detection should have minimum evidence/coverage requirements. If the evidence is insufficient:
- omit the pattern; or
- explicitly mark it as insufficient/inconclusive.

## 7. Evidence and language strength

Family wording must match the evidence.

Use:
- “occurred” for directly observed events;
- “usually/tends to” only after repeated robust observations;
- “coincided with” when two observed behaviors overlap but causation is unproven;
- “because/due to” only when the causal interpretation is genuinely supportable by the installation behavior contract and measurements.

Never use confidence language to hide a fabricated calculation.

## 8. Metrics currently supportable vs conditional

### 8.1 Already supported or strongly supportable from current normalized corpus

Depending on coverage:
- total PV generation/energy;
- total house energy consumption;
- total grid import energy;
- battery charged/discharged energy;
- battery SOC samples;
- SOC minimum/maximum and their occurrence times;
- PV/house/grid instantaneous maxima/minima and occurrence times;
- time-of-day profiles for PV/house/grid;
- battery threshold crossings;
- durations/episodes derived from sufficiently continuous samples;
- observable-night statistics;
- period evolution of the above.

### 8.2 Conditional on validated flow attribution

Do not publish as factual household-source numbers until validated:
- direct PV → house;
- battery → house contribution;
- grid → house where total grid import cannot safely be equated with house supply;
- percentage of house consumption supplied without grid;
- direct-solar share of house consumption.

These remain high-priority family requirements. “Unavailable” is temporary product incompleteness, not permission to substitute misleading metrics.

### 8.3 Not currently supportable as a numeric value

Without additional evidence:
- unproduced/curtailed solar potential;
- “wasted solar kWh”;
- inverter/system losses as a balancing residual;
- financial savings without tariff/billing support;
- utility meter reconciliation without meter observations.

## 9. Family report page architecture

### Page 1 — Immediate household answers
- From Enel/grid → house
- Direct solar → house
- Battery → house
- total house consumption
- night coverage / reserve episodes / qualifying grid-use duration

### Page 2 — Whole-system totals
- total PV generation
- total house consumption
- total grid import
- battery charged/discharged
- solar-use/curtailment only when supportable
- simple explanatory charts

### Page 3 — Patterns and events
- records/highlights;
- repeated events;
- robust hourly patterns;
- night patterns;
- evolution through the selected period;
- observability/coverage context.

### Technical annex
Preserve the value of the current technical export:
- detailed time buckets;
- per-metric coverage;
- SOC metrics;
- battery charge/discharge;
- timestamps;
- quality information;
- glossary;
- provenance/audit detail where appropriate;
- complete event list when Page 3 condenses occurrences.

## 10. Excel structure target

The current XLSX exporter is reusable infrastructure, but the family-facing workbook should evolve toward sheets equivalent to:

1. `Resumen` — Pages 1–2 family summary;
2. `Patrones` — Page 3 robust patterns;
3. `Eventos` — complete event occurrence table;
4. `Detalle` — time-bucket technical data;
5. `Calidad` — metric coverage/observability/provenance;
6. `Glosario` — plain-language terms.

Exact sheet names may follow active localization.

Do not expose internal enum identifiers such as `SimpleEnergy` or `Day` as normal Spanish user-facing labels.

Round family-facing numbers sensibly; detailed precision may remain in technical sheets where useful.

## 11. PDF structure target

PDF should present:
- Page 1 immediate household answers;
- Page 2 whole-system totals/context;
- Page 3 patterns/events;
- optional following technical/event annex pages.

Prioritize:
- large readable text;
- concise language;
- simple charts;
- selected-period wording;
- explicit partial-data warnings when needed;
- no technical identifiers in family-facing sections.

## 12. Real-PC QA result that triggered this clarification

QA selected a relative preset:
- “Últimos 7 días del historial”;
- 2026-09-19 through 2026-09-25;
- Simple Energy Summary;
- daily aggregation.

### Mechanical XLSX result — PASS

The workbook:
- generated successfully;
- opened normally in Excel;
- contained `Resumen`, `Detalle`, `Calidad` and `Glosario`;
- represented the requested period;
- contained daily buckets;
- preserved missing periods as blank/unmeasured rather than measured zero;
- included coverage information.

This exporter foundation should be preserved.

### Family-facing Simple Report result — FAIL

The workbook did not allow the target readers to quickly answer their real priority questions.

Problems included:
- technical totals dominated the family summary;
- battery charged/discharged values were presented where the family priority is battery contribution to household consumption;
- no immediate ordered answer to grid / direct solar / battery / night-shortfall questions;
- insufficient interpretation of patterns and repeated events;
- user-facing technical enum/precision leakage.

This is a product/interpretation failure, not an XLSX file-generation failure.

## 13. Implementation direction

Do not create a second physical-energy calculation path.

Reuse the validated normalized corpus and aggregation/statistics services, then add dedicated layers for:
- event detection;
- observability/eligibility calculation;
- robust pattern analysis;
- family-facing report view models/narratives.

Candidate service boundaries (names are not mandatory):
- `EnergyEventDetectionService`;
- `EnergyPatternAnalysisService`;
- `ReportObservabilityService`;
- `FamilyReportModelBuilder`.

Pattern/event outputs should carry enough metadata to audit:
- selected period;
- metric/source;
- eligible opportunities;
- observed occurrences;
- coverage;
- thresholds/rules used;
- event timestamps;
- supporting values;
- interpretation strength/provenance.

## 14. Phase 7 closure rule after this clarification

Phase 7 is not complete merely because:
- XLSX/PDF files are generated;
- presets reload;
- technical tables are correct.

For the currently available source subsystems, Phase 7 closes only when:
- the family-facing Simple Energy Report follows the Page 1–3 architecture;
- unsupported primary metrics remain explicit rather than fabricated;
- event/pattern analysis obeys observability and evidence rules;
- the technical annex preserves missing-is-not-zero semantics;
- Excel and PDF remain mechanically valid/readable;
- a final real-PC family-readability validation passes.

Utility-meter/tariff-dependent pages remain separate future work and do not authorize fabricated data in Phase 7.


## 15. Implementation checkpoint — 2026-09-26

The first implementation of this canonical clarification is green in Windows CI at HEAD `e02df70812af3a02f7800e4637e38d877f460ac9`, run `36277693543`.

Implemented now:
- dedicated family analysis service;
- observable-night accounting;
- repeated reserve+grid event preservation;
- robust cross-day three-hour patterns;
- adaptive day/week/month evolution;
- family `Resumen` / `Patrones` / `Eventos` XLSX structure;
- Page 1 / Page 2 / Page 3 Simple Energy PDF structure;
- deterministic smoke proving three synthetic complete nights, three events and expected time-of-day patterns.

Still evidence-gated:
- direct PV→house;
- battery→house;
- grid-free share;
- numeric curtailed/unused solar.

Those remain explicitly unavailable until their measurement/derivation path is validated.


## 16. Consolidated full-export review decisions — 2026-09-27

This section records the user's decisions after reviewing the **complete XLSX and PDF exports together**, not one sheet/page at a time.

### 16.1 Report title

Spanish family report title is:

`Reporte de Uso de Energia - Tipo : XXXX`

where `XXXX` is the saved preset name currently used to generate the report.

The preset/range information may still appear as secondary metadata, but the preset name is no longer used alone as the main document title.

### 16.2 Page 2 visual hierarchy

Page 2 must use a visual language **similar to Page 1**, not fall back to a plain technical table.

Family-facing totals such as:
- solar generated;
- house consumption;
- utility/grid energy;
- battery movement/context;
- future measurable unused/curtailed solar;

should use clearly separated cards/blocks with large values, short explanations and sensible whitespace.

### 16.3 Page 3 scope

Page 3 remains **Patterns and Events**.

The detailed `Evolución (Día/Semana/Mes)` machine-readable table should move out of the family-facing Page 3 and into the technical annex.

Page 3 should prioritize:
- notable facts;
- robust natural-language patterns;
- repeated events;
- family-readable comparisons.

### 16.4 Repeated highlights

For “best/worst/highest/lowest” highlights, preserve **all relevant occurrences**, not arbitrarily one occurrence.

This extends the previously agreed event rule:
- summaries may condense;
- the underlying relevant occurrences remain visible/auditable.

The exact near-tie tolerance may be defined technically, but ties/repeated qualifying highlights must not be silently collapsed to one example.

### 16.5 Family night wording

Preferred positive family label:

`SIN PROBLEMAS DE ALIMENTACION`

This replaces technical wording such as:
- “sin episodio reserva + red”.

Other states remain clearly separated, for example:
- shortfall / reserve+grid episode;
- insufficient observations / unknown.

The detailed technical definition remains available in glossary/annex.

### 16.6 Excel/PDF relationship

PDF follows the same semantic hierarchy as Excel:

1. family Page 1;
2. family Page 2;
3. family Page 3;
4. technical annex as needed.

PDF is not a different, more technical summary. It is the printable version of the same family report, optimized for page layout.

### 16.7 Required charts

The family report target contains **three charts**.

#### Chart A — stacked columns: source of household consumption

Each x-axis bucket is one report aggregation period.

The full stacked column represents **total household consumption** for that bucket, split into:
- direct solar → house;
- battery → house;
- configured utility/company → house (Enel for the target household).

This chart is evidence-gated by validated source attribution.

Until direct-solar and battery-to-house attribution is validated:
- do not fabricate stacked components;
- do not infer them as naive residuals;
- the chart may be withheld/marked unavailable according to the final display decision.

#### Chart B — lines: battery charge + solar production + household consumption

Time-series chart with:
- battery SOC;
- solar production;
- household consumption.

Recommended visual axes:
- power/energy series on the left axis according to the selected aggregation semantics;
- battery SOC on a separate right-side percent axis.

#### Chart C — lines: household consumption + each supply origin

Time-series chart with:
- total household consumption;
- direct solar → house;
- battery → house;
- utility/company → house.

Direct-solar and battery-to-house series share the same evidence gate as Chart A.

### 16.8 Report aggregation governs charts

The aggregation selected while configuring the report must govern report tables **and charts**.

Examples:
- Hour;
- Day;
- Week;
- Month;
- Year where supported/useful.

No chart should silently use a different time bucketing from the configured report without an explicit, justified exception.

Resolved implementation rule:
- household/source/solar report series use **energy per selected aggregation bucket (kWh)**;
- the battery stored-energy series uses **estimated stored kWh at bucket end**;
- charts never substitute average power when the report is configured as an energy report;
- unknown buckets remain gaps/unavailable rather than measured zero.

### 16.9 Typography

Family-facing exports should use:

- **Aptos Narrow** for normal text, labels, headings and explanatory copy;
- **Aptos Mono** for numeric values.

Excel may set these font names directly.

PDF must not fail if the fonts are unavailable on a machine. Use a deterministic compatible fallback while preserving the intended distinction between narrow family text and monospaced numeric values.

### 16.10 Visual formatting rules

All exported sheets/pages require deliberate visual formatting:
- readable row heights;
- readable column widths;
- wrapped text;
- clear table headers;
- visible hierarchy;
- consistent number formats;
- adequate whitespace;
- no clipped text;
- no overlapping merged cells.

Merged cells are allowed and encouraged for family-facing narrative/card areas when they improve readability.

Do **not** merge machine-processable data cells in technical tables such as:
- `Detalle`;
- `Eventos`;
- `Evolución` annex tables;
- quality/provenance tables intended for filtering/export.

### 16.11 Glossary redesign

The existing glossary is insufficient and must be replaced by a real report-reading guide.

It must explain at minimum:

#### Family concepts
- household consumption;
- solar production;
- direct solar → house;
- utility/grid / Enel;
- battery → house;
- battery charge/SOC;
- stored battery energy;
- normal reserve;
- outage reserve;
- observable night;
- reserve+grid/night-shortfall event;
- data coverage;
- unknown/missing period;
- pattern vs event.

#### Units
- W;
- kW;
- kWh;
- % / SOC.

#### Technical annex / Detail fields
Explain what the major `Detalle` columns mean, including:
- period;
- start/end UTC;
- solar/home/grid kWh;
- battery delivered/received kWh;
- SOC average/min/max/end;
- per-metric coverage;
- minimum coverage.

The glossary must also explain important interpretation limits:
- missing is not zero;
- battery discharged energy is not automatically equal to battery→house;
- total PV generation is not the same as direct PV→house;
- unused/curtailed solar is not currently measurable as a naive residual.

### 16.12 Data-completeness gate for next manual report acceptance

The 38.9% corpus has now adequately tested:
- partial-data warnings;
- unknown-vs-zero behavior;
- suppression of weak patterns.

Before the **next human semantic/readability acceptance** of Reporting, complete the historical capture/backfill (or otherwise use a genuinely well-covered selected period).

Development/CI remains independent of backfill, but judging:
- patterns;
- events;
- evolution;
- chart usefulness;
- family conclusions;

against a 38.9% corpus is no longer the preferred acceptance path.

### 16.13 Cross-page live Battery commitment

The previously recorded Phase 6 freshness finding remains active and is explicitly carried into the next combined UI tranche.

Current implementation still reads Battery page primary metrics from the latest locally normalized/stored samples.

When a fresh authenticated `CurrentHouseholdSnapshotService` snapshot exists, Battery should prefer current data for **current-state fields**, including where supportable:
- battery SOC;
- estimated stored energy derived from current SOC;
- normal-use energy remaining derived from current SOC and validated thresholds;
- outage/emergency reserve remaining derived from current SOC and validated thresholds;
- current battery activity;
- current technical battery voltage/current/power;
- displayed last/current reading timestamp.

Historical energy/statistical calculations remain tied to the stored validated corpus.

Never silently mix live/current and historical values without labels.

### 16.14 Battery family terminology requires a second pass

Current Spanish labels such as:
- `Carga actual`;
- `Energía almacenada (estimada)`;
- `Disponible antes de usar la red (estimado)`;
- `Reserva para cortes`;
- `Reserva mínima protegida`;

must be reviewed for clearer parent-facing language in the next UI tranche.

Exact replacement wording is still an explicit user decision before implementation.


## 17. Source-attribution investigation escalation — 2026-09-27

Source attribution is now a **priority implementation dependency**, not a tolerated long-term unavailable field.

Canonical investigation detail:
- `SOURCE_ATTRIBUTION_INVESTIGATION_2026-09-27.md`.

Locked additions:
- family battery chart uses estimated stored battery energy in kWh, not SOC %, while retaining an explicit estimate label;
- historical inverter settings are treated as time-varying where the downloaded corpus proves changes;
- report source attribution must use historical/as-of configuration where available;
- current EnergyFlow support must be investigated and persisted because the target commissioning profile reports `SUPPORTED`;
- if a source-attribution chart is temporarily blocked, retain its intended report location with a clear unavailable/evidence message, but prioritize resolving it.

Do not use today's 20/10/50 family policy as a blanket retrospective assumption.

The completed backfill should be used for the next semantic QA after the consolidated attribution/reporting tranche.


## 18. Implemented reporting decisions — 2026-09-27

The previously open chart/source-attribution decisions are now resolved and implemented.

### 18.1 Source attribution

Family Page 1 and family charts use `SourceAttributionService`:
- Solar → House;
- Battery → House;
- Enel/Grid → House;
- Unattributed household energy.

The system is deliberately conservative:
- current SBU/OSO/LBU evidence is time-scoped;
- historical threshold changes use as-of context where available;
- old grid+solar frames with more than one physically plausible allocation remain unattributed;
- negative grid-sign semantics that are not validated remain unresolved;
- residual balance is diagnostic and is never distributed merely to make the equation close.

### 18.2 Required charts

The Simple Energy Report now contains three charts, using the report's selected aggregation:
1. stacked household consumption by source — Solar, Battery, Enel and Unattributed;
2. solar produced, household consumption and estimated stored battery energy;
3. household consumption plus Solar→House, Battery→House and Enel→House.

The stored battery-energy line is:
- configured usable battery capacity × ending SOC;
- expressed in kWh;
- explicitly labeled as an estimate.

### 18.3 Excel/PDF hierarchy

Excel:
- Page 1 and Page 2 use family card hierarchy;
- embedded chart images preserve the intended product layout even though ClosedXML does not provide reliable native chart creation;
- Patrones stays family-facing;
- Eventos preserves full occurrence/night evidence;
- Evolución is a separate annex;
- Detalle, Calidad and Glosario remain technical/auditable.

PDF:
- mirrors the same family hierarchy and chart semantics;
- uses deterministic font fallbacks so export cannot fail solely because Aptos is unavailable.

### 18.4 Typography

Implemented:
- Excel family/body text: Aptos Narrow;
- Excel values/numerics: Aptos Mono;
- PDF prefers the corresponding Aptos family when resolvable and otherwise uses PDF-safe system fallbacks.

### 18.5 Cross-page Battery Live

The Battery page now satisfies the current-state commitment:
- entering Battery with an authenticated session refreshes current state if the cached snapshot is stale;
- one coherent fresh snapshot supplies current SOC/voltage/currents/power;
- stored normalized values are used only as an explicitly labeled whole-block fallback;
- no silent mixing of current and stale primary battery values.

### 18.6 Next acceptance rule

The next manual acceptance is **one consolidated XLSX + PDF review using a high-coverage period**.

Do not return to one-cell / one-layout-fix manual build cycles. Inspect the complete exported artifacts, batch all defects detectable without new evidence, then create a single corrective tranche if needed.

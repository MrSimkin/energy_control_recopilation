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

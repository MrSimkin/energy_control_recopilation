# Solar of Things Windows App — Requirements Clarifications and Decisions 01

Date: 2026-09-24

Status: USER-APPROVED CLARIFICATIONS / ARCHITECTURE INPUT

This document supplements `INITIAL_REQUIREMENTS.md`. It does not replace the original user-stated requirements.

## 1. Electrical topology

Confirmed by user:

- All household electrical loads pass through the inverter.
- Grid export is not available / does not occur.
- The inverter can technically charge the battery from the grid.
- Grid charging was used historically for a period.
- The inverter is currently configured so the battery does **not** charge from the grid.

Implication:

The installation is much easier to model than one with partial loads outside the inverter or bidirectional grid export.

The application should explicitly support the following household-source flows:

- **Load supplied directly by PV**
- **Load supplied by battery**
- **Load supplied by grid**

The app should not assume export-to-grid flows.

## 2. Battery

Confirmed battery:

**SPRO/Techfine LC230-512**

User-provided specifications:

- chemistry: LiFePO₄;
- nominal voltage: 51.2 V;
- useful capacity indicated on label: **11.776 kWh**.

Battery configuration must remain editable/configurable in the application rather than being hard-coded.

At minimum, battery settings should allow:

- display name/model;
- chemistry;
- nominal voltage;
- usable capacity in kWh;
- optional future fields if useful.

The application may therefore support:

- battery state of charge (%);
- battery charge/discharge power (W/kW);
- estimated stored usable energy (kWh), derived from configured usable capacity × SOC when appropriate.

## 3. Source-attribution terminology

User accepts these primary terms:

- Load supplied directly by PV
- Load supplied by battery
- Load supplied by grid

The application must use tooltips/legends to explain these in simple language.

Reports must include a glossary written in extremely simple, layman/basic-human terms.

## 4. Battery-origin accounting

Historical context:

- grid charging was enabled in the past;
- it is currently disabled;
- grid export does not occur.

The exact treatment of “how much battery discharge originally came from solar versus grid charging” must be designed carefully.

It must not be presented as an exact historical measurement unless the available history and configuration timeline support that claim.

## 5. Date selections

The application must support both user-selectable month modes:

- last **N months counting backwards from today**;
- last **N complete calendar months**.

It must also support:

- rolling 12 months;
- year to date.

The Spanish concept “año móvil” is understood as **rolling 12 months**.

## 6. Utility-company meter readings

The utility-company data is based on cumulative meter readings taken at irregular dates.

The user has historical cumulative readings at specific dates.

Billing consumption is obtained from the difference between cumulative readings at two dates.

Therefore the application must store individual meter observations, not merely “one monthly consumption number”.

Recommended storage concept:

- reading date/time;
- cumulative utility meter reading;
- optional bill/reference note;
- optional manually entered billed consumption for comparison;
- optional billing-period start/end if present.

The application should calculate utility-grid consumption between two readings as:

`later cumulative reading - earlier cumulative reading`

The application must allow comparisons against Solar of Things / local calculated grid-import energy over the **same irregular date interval**.

## 7. Local operation / refresh model

Application is for:

- one local Windows computer;
- one local user/household.

User preference:

- **do not require continuous background operation**;
- provide an explicit **Update Data** / synchronization button;
- all available historical Solar of Things data should be backfilled into the local database;
- normal reports/charts should read from the local database, not repeatedly query the Solar of Things API.

Preferred operating model:

1. user opens app;
2. user presses Update Data, or app optionally offers update on startup;
3. app queries only missing/recent cloud data;
4. app stores/upserts it locally;
5. all reporting works against local storage.

Automatic background/start-with-Windows operation is not a current requirement.

## 8. Local database

The user already has PostgreSQL installed and explicitly wants:

- PostgreSQL as a strong candidate/default local database;
- ability to inspect the SQL schema;
- ability to run their own SQL queries directly against stored data.

The final database design must therefore be human-readable and documented.

Requirements:

- normal relational schema;
- clear table/column names;
- database views for useful normalized/reporting data;
- raw/source data retained where practical;
- schema documentation;
- no deliberate obfuscation or inaccessible proprietary storage.

## 9. Export policy

User preference:

- **Do not export CSV when Excel is available.**
- Excel is the preferred tabular export.

Static exports are acceptable; only the in-app experience needs to remain interactive.

Expected export directions:

- Excel `.xlsx` for tables/data/reports;
- printable static report format suitable for parents aged 70+.

HTML is rejected as the primary printable-report format.

The printable report must prioritize:

- readability;
- large/clear labels;
- plain-language explanations;
- simple summaries;
- glossary;
- charts/tables suitable for paper.

Exact printable-report technology/format remains to be finalized, with PDF the natural candidate.

## 10. Reporting usability

Reports and in-app views must include:

- tooltips;
- legends;
- plain-language descriptions;
- glossary in exported reports;
- user-selectable metrics;
- table/summary/graph combinations;
- saved report presets.

The app should prefer simple human-facing terms even if the underlying API/database terminology is technical.

## 11. Data layers — confirmed

User agrees with a three-layer model:

1. **Raw** — preserve Solar of Things source data as closely as practical.
2. **Normalized** — interpret raw fields into canonical metrics with units/sign rules.
3. **Calculated/reporting** — aggregates, source attribution, summaries, utility comparisons, etc.

The raw layer must be retained so normalization/calculation rules can be corrected later without losing historical source data.

## 12. Architecture constraints still active

- Windows desktop application.
- Zero monetary cost.
- Not Java.
- English UI.
- Cloud/API data acquisition only.
- Local storage.
- User-accessible SQL schema.
- Interactive app; static exports.
- No dependency on BLE/MQTT interception/local serial/device-control research.

## 13. Open architecture decision

WPF has been proposed but is **not yet approved by the user**.

User requests a plain-language explanation of WPF before accepting that UI technology.

Other candidate architecture choices should be evaluated only if they provide a concrete advantage for this Windows-only, local PostgreSQL, interactive-chart/reporting application.


## 14. UI visual direction — approved reference

The user provided AdminLTE Dashboard v3 as a **visual/UX reference**, not as a template to copy exactly:

`https://adminlte.io/themes/v4/index3.html`

Desired direction:

- traditional/professional dashboard application;
- compact and information-dense;
- persistent left-side navigation;
- clear top-level summary cards;
- tables and charts visible together where useful;
- conventional desktop controls and filters;
- restrained spacing;
- good visual hierarchy;
- mouse/keyboard-friendly;
- no touch-first oversized controls;
- avoid Microsoft Store / WinUI / Fluent-style visual language;
- avoid excessive whitespace and oversized rounded-card design.

The reference is flexible: the application should feel **similar in organization and density**, not be a pixel-perfect AdminLTE clone.

WPF remains an appropriate UI technology for reproducing this style using native desktop controls, custom styles/templates and chart components without embedding a web frontend.

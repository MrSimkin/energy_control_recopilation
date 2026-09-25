# Solar of Things Windows App — Development Roadmap v1

Date: 2026-09-24

Status: ACTIVE DEVELOPMENT ROADMAP

Authority:
- `INITIAL_REQUIREMENTS.md`
- `REQUIREMENTS_CLARIFICATIONS_01.md`
- `REQUIREMENTS_CLARIFICATIONS_02.md`
- `REQUIREMENTS_CLARIFICATIONS_03.md`
- `REQUIREMENTS_CLARIFICATIONS_04.md`
- `CHILE_ELECTRICITY_TARIFF_RESEARCH_INITIAL.md`
- completed Solar of Things API research under `solar_of_things_api_research/`

## Guiding principles

- Windows 11 x64 only.
- Zero-cost software stack.
- No Java.
- C# / .NET with WPF unless later evidence gives a concrete reason to change.
- AdminLTE-like compact professional dashboard style.
- Local-first operation.
- Manual/explicit **Update Data** synchronization.
- SQLite by default unless later scale/architecture testing disproves the choice.
- SQL schema must remain clear, documented and directly queryable by the user.
- Collect broadly, present selectively.
- Preserve raw data before normalization/calculation.
- Never silently convert missing/uncertain data into zero or fact.
- Financial/tariff calculations are optional and must never block the core energy application.
- Read-only Solar of Things integration.
- Both installer and portable distributions.

---

# Phase 0 — Product / Functional Specification v1 — COMPLETE

Goal: turn the accumulated research and requirements into one canonical implementation specification before significant coding.

Deliverables:

1. Metric dictionary:
   - raw metric;
   - normalized metric;
   - unit;
   - measured vs derived;
   - aggregation behavior;
   - confidence/quality rules.

2. Energy-flow definitions:
   - PV generation;
   - PV → Load;
   - Battery → Load;
   - Grid → Load;
   - Grid → Battery when supported;
   - battery charge/discharge;
   - total house load;
   - grid import;
   - no grid export.

3. Time model:
   - day;
   - calendar week Monday–Sunday;
   - rolling 7 days;
   - month;
   - rolling N months;
   - N complete calendar months;
   - month range;
   - year;
   - rolling 12 months;
   - year-to-date;
   - arbitrary date range;
   - aggregation by hour/day/week/month/year.

4. Screen/navigation specification.

5. Database-layer specification:
   - raw;
   - normalized;
   - calculated/reporting;
   - configuration;
   - synchronization/audit;
   - utility/tariff data.

6. Report/export specification:
   - interactive app views;
   - saved report presets;
   - Excel;
   - PDF;
   - Simple Energy Report;
   - glossary/tooltips.

7. Data-quality/confidence model.

8. Synchronization/backfill rules.

9. First-run commissioning/self-discovery flow.

10. Tariff and actual-bill reconciliation model.

Exit criterion:

**COMPLETE — PRODUCT_FUNCTIONAL_SPEC_V1.md approved/frozen on 2026-09-24.**

---

# Phase 1 — Technical Skeleton / Proof of Architecture — REFINED BUILD CI PASS / FINAL USER REVALIDATION PENDING

Goal: prove the chosen desktop stack and project structure before building business logic.

Current checkpoint: core architecture passed CI and the original Windows 11 x64 manual checkpoint. The narrow localization/navigation refinement is implemented and passed Windows CI in run `36171030035`; only final user revalidation of artifact `10881030047` remains before formal Phase 1 closure. See `CONTINUITY_STATUS.md`.

Manual checkpoint already passed on 2026-09-25:
- normal non-admin launch;
- clean shutdown;
- acceptable shell rendering;
- portable `Data\\energy.db`, `Backups\\`, and `Logs\\` creation;
- displayed portable database path;
- expected SmartScreen warning for the unsigned development build.

Current product name is provisional: **final product name = TBD**.

Build:

- .NET / C# solution structure;
- WPF shell;
- AdminLTE-like navigation layout;
- theme infrastructure;
- Spanish-default / English-selectable localization infrastructure with persisted language preference;
- functional placeholder sidebar navigation for shell validation;
- dependency injection/configuration/logging;
- SQLite access layer;
- migration/versioning mechanism;
- secure Windows credential storage abstraction;
- application settings;
- installer/portable path strategy prototype.

Prototype UI:

- left sidebar;
- top/status area;
- Dashboard placeholder;
- Data placeholder;
- Reports placeholder;
- Utility/Billing placeholder;
- Settings placeholder.

Database proof:

- create SQLite database;
- expose documented path;
- create first schema migration;
- verify external query access using a normal SQLite client.

Exit criterion:

- app launches on Windows 11 x64;
- database is created/readable;
- configuration persists;
- no admin rights needed for normal execution;
- project can be built reproducibly.

---

# Phase 2 — Solar of Things Authentication and Commissioning

Goal: make the application connect safely to the user's own Solar of Things account and discover the actual inverter/cloud profile.

Implement:

- login/signing;
- remember/not-remember credentials;
- secure secret storage;
- token/session handling;
- serialized refresh;
- logout/session reset;
- station discovery;
- device discovery;
- exact SiSeLi model/protocol discovery;
- gather-attribute discovery;
- dataSource validation;
- device capability profile.

UI:

- first-run connection wizard;
- account status;
- discovered station/device summary;
- connection diagnostics;
- commissioning result screen.

Safety:

- read-only API calls only;
- no configuration mutation;
- no firmware/device control.

Exit criterion:

- application can authenticate;
- discover the target SPRO/SUNPRO installation;
- persist a local capability profile;
- reconnect safely later.

---

# Phase 3 — Raw Data Ingestion and Full Historical Backfill

Goal: build the trustworthy local historical corpus.

Implement:

- raw API response/storage model;
- selected-key history ingestion;
- record-list fallback;
- pagination;
- timezone-aware local-day windows;
- idempotent upsert;
- late-arrival overlap;
- source timestamps;
- retrieval timestamps;
- null/missing preservation;
- sync audit log;
- progress/cancellation.

First-run backfill:

- determine oldest available history;
- backfill all available historical data;
- preserve completeness/gap statistics.

Normal operation:

- **Update Data** button;
- fetch only missing/recent ranges;
- show last update timestamp;
- optional update-on-app-open preference.

Exit criterion:

- all available history is stored locally;
- repeated updates do not duplicate data;
- app can operate/report without querying Solar of Things until next update.

---

# Phase 4 — Normalization and Metric Engine

Goal: transform raw SiSeLi fields into trustworthy canonical household-energy metrics.

Implement:

- schema-driven alias mapping;
- unit normalization;
- sign normalization;
- per-device/protocol rules;
- plausibility checks using inverter/battery configuration;
- confidence states:
  - CONFIRMED;
  - PROBABLE;
  - UNRESOLVED;
  - UNAVAILABLE.

Primary normalized metrics:

- PV power;
- house/load power;
- grid import power;
- battery SOC;
- battery voltage/current;
- battery charge/discharge power;
- PV energy;
- house energy;
- grid-import energy;
- battery charged energy;
- battery discharged energy.

Flow derivation:

- PV → Load;
- Battery → Load;
- Grid → Load;
- Grid → Battery when supported;
- no Grid Export.

Battery configuration:

- default initial battery:
  - SPRO/Techfine LC230-512;
  - LiFePO4;
  - 51.2 V nominal;
  - 11.776 kWh usable;
- editable settings.

Derived:
- estimated usable battery energy remaining.

Exit criterion:

- normalized metrics are generated from real target-device data;
- uncertainty is explicit;
- raw values remain recoverable.

---

# Phase 5 — Aggregation / Statistics Engine

Goal: support all requested time ranges and aggregation modes.

Implement time selections:

- day;
- calendar week Monday–Sunday;
- rolling 7 days;
- month;
- last N months rolling;
- last N complete calendar months;
- range of months;
- year;
- rolling 12 months;
- year-to-date;
- arbitrary date range.

Aggregation:

- hour;
- day;
- week;
- month;
- year.

Metric-aware math:

- power:
  - average;
  - minimum;
  - maximum;
  - peak;
- energy:
  - totals;
- SOC:
  - average;
  - min;
  - max;
  - ending value;
- coverage:
  - data completeness;
  - missing intervals.

Derived household statistics:

- total household energy consumption;
- PV contribution;
- battery contribution;
- grid contribution;
- percentage supplied without grid;
- grid dependency;
- solar self-consumption where meaningful;
- battery contribution percentage.

Exit criterion:

- selected range and aggregation produce reproducible SQL-backed results;
- calculations can be independently queried/checked.

---

# Phase 6 — Core Dashboard and Interactive Analysis UI

Goal: make the stored data useful interactively.

Dashboard:

- compact summary cards;
- current/last-known values;
- selected period summary;
- data freshness/status;
- Update Data control.

Analysis screen:

- metric picker;
- date-range selector;
- aggregation selector;
- interactive charts;
- zoom/pan;
- legend;
- tooltips;
- multiple Y axes where useful;
- synchronized separate panels when multiple axes would become confusing;
- data table under/alongside chart.

Advanced data:

- engineering values available in detail/advanced screens;
- not prominent on main dashboard.

Themes:

- small set of themes;
- AdminLTE-like density/organization;
- avoid Fluent/Store-app look.

Exit criterion:

- user can perform normal household-energy analysis without SQL.

---

# Phase 7 — Saved Reports and Export

Goal: provide reusable analyses and printable outputs.

Implement report presets:

- chosen metrics;
- table/summary/chart;
- aggregation;
- relative/fixed date settings;
- graph/table layout options.

Built-in presets:

- Simple Energy Report;
- detailed energy report;
- battery report;
- grid/utility comparison report.

Excel export:

- readable `.xlsx`;
- multiple worksheets where useful;
- summary + detailed data;
- no user-facing CSV requirement.

PDF export:

- printable;
- large/readable text;
- clear charts/tables;
- nontechnical wording;
- glossary;
- suited for older/nontechnical readers.

Exit criterion:

- a report can be recreated from a saved preset and exported consistently to Excel/PDF.

---

# Phase 8 — Utility Meter Readings and Consumption Reconciliation

Goal: compare the electricity company's meter history with inverter-derived grid import.

Implement:

- cumulative meter reading entry;
- irregular reading dates;
- optional:
  - billed consumption;
  - billing dates;
  - invoice/reference;
  - bill amount;
  - notes.

Calculate:

- consumption between any two utility readings;
- Solar of Things grid import over the exact same interval;
- absolute difference;
- percentage difference.

Display:

- utility meter;
- inverter estimate;
- difference;
- coverage/quality warning if needed.

Exit criterion:

- user can reconcile historical utility meter readings against locally stored inverter data.

---

# Phase 9 — Chile Tariff Acquisition Engine

Goal: automatically maintain official tariff data needed for optional financial analysis.

Implement provider/source adapters.

Preferred sources:

1. official distributor tariff publications;
2. CNE regulatory sources;
3. other official sources when required.

Capabilities:

- discover tariff publications;
- download official documents;
- parse HTML/structured files/PDF tables;
- avoid OCR unless unavoidable;
- retain source metadata and hash;
- validate headings/units/effective dates;
- version schedules;
- support superseding/retroactive corrections;
- expose **Update Tariffs**.

Data model:

- provider;
- service;
- tariff plan;
- schedule;
- components;
- classifications;
- effective dates;
- provenance.

Failure behavior:

- never silently accept an invalid parser result;
- keep last valid tariff;
- flag missing future tariff data.

Exit criterion:

- application can populate and update the applicable official tariff schedule locally without paid services.

---

# Phase 10 — Estimated Bill / Actual Bill Reconciliation

Goal: calculate a transparent estimated bill according to published rules and compare it with the actual bill.

Implement:

- tariff-aware billing intervals;
- tariff changes inside one bill interval;
- fixed charges;
- variable regulated energy components;
- applicable classifications;
- taxes/percent components where determinable;
- retroactive corrections.

Actual bill entry:

- total amount;
- stated consumption;
- billing interval;
- additional charges;
- credits;
- arbitrary bill line descriptions.

Outputs:

1. estimated regulated/standard bill;
2. known additional charges/credits;
3. actual bill;
4. differences;
5. source/provenance used for calculation.

Optional financial metrics:

- estimated grid-energy cost;
- avoided grid-energy cost;
- estimated savings;
- effective grid cost per kWh.

Exit criterion:

- every estimate is reproducible and auditable;
- differences are explained without assuming meter error.

---

# Phase 11 — SQL Usability / Documentation

Goal: make the database useful directly to the user.

Deliver:

- schema documentation;
- entity/relationship diagram;
- table dictionary;
- column descriptions;
- sample queries;
- stable reporting views.

Recommended views may include:

- `reporting_daily_energy`;
- `reporting_hourly_energy`;
- `reporting_battery`;
- `reporting_grid_import`;
- `reporting_utility_reconciliation`;
- `reporting_bill_estimates`;
- `data_quality_summary`.

Exit criterion:

- user can inspect the SQLite file using a normal SQL client and answer common questions without reverse-engineering internal raw tables.

---

# Phase 12 — Backup, Restore, Import/Export of App Data

Goal: protect the local historical corpus.

Implement:

- automatic safe database backup;
- manual **Backup Now**;
- restore workflow;
- portable data-folder support;
- database integrity check;
- version compatibility;
- optional archive export.

Installed version:

- application-owned writable folder outside the user profile and without requiring admin rights during normal use.

Portable version:

- local `data\` and `backups\` folders beside the application.

Exit criterion:

- reinstalling/upgrading the application does not risk historical data.

---

# Phase 13 — Packaging and Release Candidate

Goal: produce a usable Windows application.

Deliver:

- Windows 11 x64 installer;
- portable package;
- application icon/versioning;
- upgrade behavior;
- clean uninstall rules that protect user data unless explicitly requested;
- diagnostics/log export with secrets removed.

Test:

- clean-machine installation;
- portable run;
- database path permissions;
- credential storage;
- update/backfill;
- export;
- backup/restore.

Exit criterion:

**Release Candidate usable on the user's personal Windows 11 laptop.**

---

# Phase 14 — Validation Against Real Household Operation

Goal: validate results over real operating periods.

Compare:

- Solar of Things UI;
- application current values;
- historical daily totals;
- battery behavior;
- utility meter readings;
- actual bills;
- estimated tariff calculations.

Investigate:

- gaps;
- sign conventions;
- unusual operating modes;
- periods when historical grid charging was enabled;
- tariff/parser discrepancies.

Adjust normalization/calculation rules without losing raw data.

Exit criterion:

**Household metrics and billing comparisons are trusted for normal personal use.**

---

# Phase 15 — Stable v1 Release

Goal: formalize the first stable version.

Requirements:

- core energy collection stable;
- full available history local;
- reports reliable;
- exports readable;
- backups proven;
- SQL schema documented;
- tariff module either validated or clearly marked optional/beta if still incomplete;
- no known secret leakage;
- no device mutation functions.

Result:

**Solar of Things Windows App v1.0**

---

# Recommended delivery order / dependency chain

```text
Specification
  ↓
Technical shell
  ↓
Login + target-device discovery
  ↓
Raw ingestion + historical backfill
  ↓
Normalization
  ↓
Statistics
  ↓
Dashboard
  ↓
Reports / Excel / PDF
  ↓
Utility meter reconciliation
  ↓
Tariff acquisition
  ↓
Bill estimation/reconciliation
  ↓
SQL docs + backup/restore
  ↓
Packaging
  ↓
Real-world validation
  ↓
v1.0
```

## Important sequencing rule

Do **not** start with charts, PDF design, tariff scraping or bill calculations before raw ingestion and normalization are proven against the real inverter.

The first meaningful development milestone is not “a pretty dashboard”.

It is:

> **The app can log in, discover the user's actual device, download all available history, store it correctly in the local SQL database, and update it idempotently.**

Once that is reliable, the rest of the product becomes much safer to build.

## Scope control

Features that should not delay v1 core:

- multiple-household management;
- cloud-hosted sync;
- mobile app;
- remote web dashboard;
- BLE/local serial support;
- inverter control/settings;
- grid-export analytics;
- machine-learning forecasting.

They can be reconsidered only if the user later changes scope.

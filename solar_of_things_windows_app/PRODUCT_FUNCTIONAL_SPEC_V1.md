# Solar of Things Windows App — Product / Functional Specification v1

Date: 2026-09-24

Status: CANONICAL V1 — APPROVED / FROZEN · approved change note applied 2026-09-25

## 0. Purpose and authority

This document consolidates the approved requirements, research conclusions, gap-review decisions, and implementation defaults for the Solar of Things Windows application.

This document is approved/frozen and is the primary product/functional authority for implementation. Earlier requirements and clarification files remain historical evidence of how decisions were reached.

## Approved change note — 2026-09-25

The user explicitly changed the UI-language requirement after the original v1 approval.

- Historical requirement remains preserved in earlier requirement/clarification files: English UI.
- Current canonical requirement from 2026-09-25 onward: **Spanish is the default UI language; English must be selectable as an alternative.**
- Language preference must persist locally.
- The current product name **Solar Energy Monitor / SolarEnergyMonitor is a working development name only; final product name = TBD.**
- This change does not reopen Phase 0 or invalidate the already validated technical architecture.

Source authority:
- solar_of_things_api_research/
- INITIAL_REQUIREMENTS.md
- REQUIREMENTS_CLARIFICATIONS_01.md
- REQUIREMENTS_CLARIFICATIONS_02.md
- REQUIREMENTS_CLARIFICATIONS_03.md
- REQUIREMENTS_CLARIFICATIONS_04.md
- REQUIREMENTS_CLARIFICATIONS_05.md
- CHILE_ELECTRICITY_TARIFF_RESEARCH_INITIAL.md
- FUNCTIONAL_SPEC_GAP_REVIEW_01.md
- DEVELOPMENT_ROADMAP_V1.md

---

# 1. Product goal

Build a zero-cost Windows 11 x64 desktop application for one household that:

1. connects read-only to Solar of Things / SiSeLi cloud services;
2. discovers the user's actual inverter/cloud schema dynamically;
3. downloads and backfills all useful historical energy data available from the cloud;
4. stores that data locally in a transparent SQL database;
5. preserves raw source data as well as normalized and calculated data;
6. produces trustworthy household-energy statistics;
7. supports interactive charts, tables and summaries;
8. supports reusable report presets;
9. exports detailed Excel workbooks and clear printable PDF reports;
10. stores electricity-utility cumulative meter readings and actual bills;
11. optionally retrieves official Chilean tariff data and estimates bills according to published rules;
12. allows reconciliation between inverter data, utility meter readings, tariff estimates and actual bills.

The application is for personal/local use, not a multi-user cloud service.

---

# 2. Hard constraints

- Platform: Windows 11 x64.
- Language/framework: no Java.
- Preferred implementation: C# / modern .NET / WPF.
- UI language: Spanish by default; English selectable as an alternative. Preference persists locally.
- Local display convention: Chile-friendly date/time:
  - date: dd-MM-yyyy
  - time: HH:mm
- Zero paid software/services required.
- Solar of Things integration is cloud/API based.
- No BLE/local serial/MQTT interception dependency.
- No device settings/control/firmware mutation.
- Solar of Things operations are read-only.
- Both installer and portable distributions are required.
- No mandatory background Windows service.
- Normal collection is user-triggered through Update Data.
- Data remains available locally without needing a live API query for every report.

---

# 3. Product design principles

## 3.1 Collect broadly, present selectively

The collector should retain useful read-only telemetry and technical metadata exposed by Solar of Things, not merely the small set of headline metrics shown in the main dashboard.

The main UI should remain selective and understandable.

## 3.2 Raw first

No normalized/calculated value may replace the source observation.

Raw source data must remain available so later interpretation fixes can rebuild derived history.

## 3.3 Measured is different from derived

The system must distinguish:
- MEASURED / CONFIRMED
- DERIVED / HIGH CONFIDENCE
- UNRESOLVED
- UNAVAILABLE

The UI can simplify this when confidence is healthy, but the information must remain queryable and visible in detail/tooltips when relevant.

## 3.4 Missing is not zero

Null, missing, unavailable and stale values are not measured zero.

## 3.5 Local first

The application reports from the local database.

Solar of Things is a synchronization source, not a live dependency for historical analysis.

## 3.6 Auditable calculations

Important calculated values must retain enough provenance to determine:
- which source fields were used;
- which normalization rule was used;
- which calculation method/version was used;
- data coverage/quality;
- which tariff publication was used where applicable.

---

# 4. Household / hardware context

Current known installation:

Inverter:
- user description: SPRO-6200
- 230 V class
- nominal 48 V battery system
- strong public-family match: SUNPRO/SPRO Energy 6.2 kW 48 V 220/230 V family
- candidate public identifiers: SP6200-48L / BIS6200-48L / GA6248MH
- exact SiSeLi cloud identity remains commissioning-resolved

Battery:
- SPRO/Techfine LC230-512
- LiFePO4
- nominal voltage: 51.2 V
- user-provided useful capacity: 11.776 kWh
- battery settings must be editable

Topology:
- all household loads pass through the inverter
- grid export does not occur
- inverter can technically charge battery from grid
- grid charging occurred historically
- grid charging is currently disabled

The application must not hard-code candidate public inverter model identifiers as cloud protocol identity.

---

# 5. Primary household-energy concepts

The user-facing application should center on the following concepts.

## 5.1 PV Power

Instantaneous/average rate of solar generation.

Canonical display unit:
- W or kW

## 5.2 PV Energy Generated

Energy produced by solar panels over a selected period.

Canonical report unit:
- kWh

Preferred source hierarchy:
1. validated real server aggregate/counter;
2. validated device cumulative/daily counter;
3. integration of normalized PV power;
4. unavailable.

## 5.3 House Load Power

Power currently consumed by household loads passing through the inverter.

Unit:
- W / kW

## 5.4 House Energy Consumption

Total household energy consumed over a selected period.

Unit:
- kWh

Preferred source hierarchy:
1. validated household-load energy counter/aggregate;
2. validated counter delta;
3. integration of house-load power;
4. unavailable.

## 5.5 Load supplied directly by PV

Portion of household load supplied directly by PV at the relevant time/bucket.

This may be:
- directly measured by the Solar of Things energy-flow model;
- derived with high confidence after commissioning;
- unresolved if losses/topology prevent trustworthy allocation.

Never silently assume losses are zero merely to force an energy-balance equation.

## 5.6 Load supplied by Battery

Portion of household load supplied by battery discharge.

Same measured/derived confidence rules apply.

## 5.7 Load supplied by Grid

Portion of household load supplied directly by grid import.

This is distinct from Total Grid Import.

## 5.8 Grid to Battery

Grid energy/power used to charge the battery.

Historically relevant because grid charging previously occurred.

It should be reported only when:
- directly measured; or
- credibly derived with high confidence.

## 5.9 Total Grid Import

All electricity entering the installation from the utility grid.

This is the canonical inverter-side quantity to compare with the utility meter.

Total Grid Import may include:
- Grid → House
- Grid → Battery
- measurable/implicit inverter/system losses

It must not be assumed identical to Grid → House.

## 5.10 Grid Export

Not applicable for the known installation.

Do not present a permanent zero as a meaningful metric.

If raw API values appear negative or export-like:
- preserve them;
- investigate sign/noise/meaning;
- do not clamp to zero before normalization validation.

## 5.11 Battery State of Charge

Battery fullness.

Unit:
- %

## 5.12 Estimated usable battery energy remaining

Derived value:

configured usable battery capacity × normalized SOC fraction

Initial configured capacity:
- 11.776 kWh

This must be labeled as an estimate, not as a separately measured energy counter.

## 5.13 Battery charge/discharge power

Rate of energy entering/leaving the battery.

Canonical normalized sign convention:
- positive = discharge / energy leaving battery
- negative = charge / energy entering battery

Raw sign is preserved separately.

Unit:
- W / kW

## 5.14 Battery energy charged/discharged

Energy charged into / discharged from the battery over the selected period.

Unit:
- kWh

Source hierarchy:
1. validated server aggregate/counter;
2. validated cumulative counter delta;
3. integration of normalized battery power separated by direction;
4. unavailable.

## 5.15 Inverter/system losses

Only expose as a normalized metric if they are:
- directly measured; or
- credibly derivable after commissioning.

Do not create a fake “losses” value merely as a balancing residual unless explicitly labeled as such and validated.

---

# 6. Energy-flow attribution policy

## 6.1 Preferred evidence order

For PV → Load, Battery → Load, Grid → Load and Grid → Battery:

1. use a validated Solar of Things energy-flow value if available;
2. use a validated target-device-specific derivation;
3. mark unresolved/unavailable.

## 6.2 Commissioning-resolved formulas

Exact formulas must not be frozen before the actual SiSeLi model, dataSource, gather protocol and field catalog are known.

The commissioning phase must determine:
- which fields are directly measured;
- their units;
- their signs;
- whether flow values already include losses;
- whether power balance is consistent;
- which derived formulas are safe for this installation.

The resulting mapping becomes a versioned per-device normalization profile.

## 6.3 Battery provenance

“Battery → Load” describes where household power came from at the moment of use.

It does not necessarily prove where the energy originally stored in the battery came from.

For historical grid-charging periods:
- do not label battery discharge as solar-origin unless provenance is supportable.

For current periods where grid charging is disabled:
- battery discharge is operationally useful as a non-grid-at-consumption contribution;
- stronger “solar-origin” claims require provenance confidence.

---

# 7. Time model

All reporting boundaries use the household/station local timezone.

Internally:
- preserve UTC source timestamp;
- preserve timezone/source context where available.

Chile DST must be handled correctly:
- some local days may contain 23 or 25 hours;
- repeated/missing local hours must not corrupt aggregates.

## 7.1 Selectable periods

Required:
- Day
- Calendar Week
- Rolling Week
- Month
- Last N Months — Rolling
- Last N Complete Calendar Months
- Range of Months
- Calendar Year
- Rolling 12 Months
- Year to Date
- Arbitrary Date Range

## 7.2 Definitions

Calendar Week:
- Monday 00:00 through Sunday 23:59:59... local time.

Rolling Week:
- trailing seven local-date days ending at the selected/current local timestamp, preserving the same local clock endpoint where possible across DST.

Last N Months — Rolling:
- selected/current local timestamp minus N calendar months through selected/current timestamp.

Last N Complete Calendar Months:
- N complete months immediately preceding the current/selected month.

Range of Months:
- inclusive complete calendar months selected by user.

Calendar Year:
- Jan 1 00:00 through end of Dec 31 local time.

Rolling 12 Months:
- selected/current local timestamp minus 12 calendar months through selected/current timestamp.

Year to Date:
- Jan 1 00:00 through selected/current local timestamp.

Arbitrary Date Range:
- user-selected start/end date/time, with date-only UI allowed where appropriate.

---

# 8. Aggregation model

User-selectable aggregation, where meaningful:
- Hour
- Day
- Week
- Month
- Year

## 8.1 Metric-aware aggregation

Power metrics:
- average
- minimum
- maximum
- peak
- optional ending/last-known value where useful

Energy metrics:
- sum/total

SOC:
- average
- minimum
- maximum
- ending SOC

Cumulative counters:
- interval delta after reset/rollover validation

Status/categorical values:
- last state
- time-in-state summaries if historized and useful

## 8.2 Partial buckets

Partial first/last aggregation buckets are included.

They must carry:
- partial flag;
- coverage percentage.

Do not silently discard partial periods.

## 8.3 Missing-data integration

When power must be integrated into energy:
- use timestamp-aware integration;
- trapezoidal integration is the default where appropriate;
- enforce a maximum gap threshold;
- do not bridge long missing intervals as constant power;
- calculate coverage;
- mark incomplete/low-quality totals when appropriate.

Exact target-specific gap thresholds may be tuned after observing real cadence.

---

# 9. Data quality

Every important derived/aggregated result should be able to expose:

- confidence state;
- data coverage;
- source method;
- stale/missing state;
- calculation version.

Suggested freshness/quality language:
- Fresh
- Delayed
- Stale
- Partial
- Unavailable

Avoid alarming technical detail in normal UI unless relevant.

---

# 10. Solar of Things synchronization

## 10.1 User workflow

Normal flow:
1. open app;
2. local data loads immediately;
3. user presses Update Data;
4. app authenticates if needed;
5. app discovers/checks account/device capability state;
6. app fetches missing/recent cloud data;
7. app upserts local database;
8. app recalculates affected normalized/aggregated data;
9. UI refreshes.

Optional setting:
- update automatically when app opens.

No mandatory continuous background collector.

## 10.2 First historical backfill

On initial commissioning:
- determine oldest useful available history;
- backfill all available historized data;
- paginate fully;
- use local-day/timezone-aware windows;
- preserve gaps and nulls;
- record completeness.

## 10.3 Incremental sync

Every Update Data:
- continue from last source timestamp;
- use overlap window to catch late arrivals/changes;
- upsert idempotently;
- re-read a recent trailing interval;
- refresh current-day aggregates;
- periodically revalidate a somewhat longer recent interval.

## 10.4 Current-only telemetry

On Update Data:
- capture current-state snapshot;
- store current-only fields as sparse snapshots when the cloud does not historize them.

Do not imply continuous history.

No background service is added solely to improve sparse engineering telemetry.

## 10.5 Failure behavior

Network/API failure:
- existing local data remains usable;
- sync failure is clearly shown;
- missing update is not converted into zero data.

Authentication:
- one controlled refresh-and-retry sequence;
- no infinite retry loops.

---

# 11. Authentication and secrets

Support:
- Remember credentials/session
- Do not remember

Stored secrets:
- use Windows-protected secure storage;
- never store plaintext credentials/tokens in SQLite;
- never export them;
- never log them.

Solar of Things tokens/signing/session behavior follows the completed API research.

---

# 12. First-run commissioning

The application must perform local read-only self-discovery.

Sequence:
1. login;
2. station discovery;
3. device discovery;
4. device details;
5. gather-attribute schema;
6. current-state/dataSource validation;
7. energy-flow capability;
8. history capability;
9. server aggregate capability;
10. alarm capability;
11. local capability profile persistence.

Commissioning records:
- exact SiSeLi model;
- manufacturer if returned;
- station/device/logger IDs;
- gather protocol/version;
- dataSource;
- raw attribute catalog;
- units/types;
- supported endpoints;
- normalization mappings;
- confidence.

The user should not have to know protocol numbers or API IDs manually.

---

# 13. Data architecture

Three primary data layers are mandatory.

## 13.1 Raw layer

Purpose:
preserve source evidence.

Retain where applicable:
- endpoint/source family;
- station/device IDs;
- source timestamp;
- retrieval timestamp;
- raw attribute key;
- raw value;
- raw unit;
- raw metadata;
- raw categorical/status values;
- original response/payload where useful and safe;
- source record/page identity when available.

Never retain reusable secrets in raw storage.

## 13.2 Normalized layer

Purpose:
canonical device/energy semantics.

Examples:
- pv_power_w
- house_load_power_w
- grid_import_power_w
- battery_power_w
- battery_soc_pct
- battery_voltage_v
- pv_energy_kwh
- grid_import_energy_kwh

Normalized records should preserve:
- source mapping;
- normalization rule version;
- confidence.

## 13.3 Calculated/reporting layer

Purpose:
user-facing analytics and performance.

Examples:
- hourly/daily/monthly energy summaries;
- source contributions;
- utility reconciliation;
- report-ready views;
- bill estimates.

These values must be reproducible from lower layers.

---

# 14. Reprocessing

The app must support rule-version changes.

If normalization/calculation logic changes:
- retain raw history;
- rebuild affected normalized/calculated history locally;
- do not require re-downloading old cloud data solely because interpretation changed.

Database records should support calculation/normalization version traceability.

---

# 15. Local database

Preferred database:
- SQLite

Reason:
- local single-user workload;
- zero cost;
- embedded;
- easy backup;
- user can query it directly with a SQL client.

Required behavior:
- clear documented relational schema;
- stable reporting views;
- SQLite WAL mode;
- concurrent read access from external SQL clients;
- external direct writes to internal tables are unsupported;
- user-facing SQL documentation and sample queries.

The database must not be encrypted in a way that prevents ordinary SQL-client access.

Secrets live outside it.

---

# 16. Database/data location

The user delegates exact path choice.

Design priority:
- safe Windows permissions;
- no admin rights for normal operation;
- easy discovery by user;
- reliable backup/upgrade.

Recommended installed layout:
- application binaries may use conventional install location;
- writable data should use a clearly documented application-owned writable location;
- if co-location is technically safe, use App/Data/Backups grouping;
- otherwise prefer a documented writable machine-level application-data directory over the user profile.

Portable:
- portable root
  - App/
  - Data/
  - Backups/

The app must show the database path in Settings/Data information.

---

# 17. Backup and restore

Required:
- automatic backup before schema migration/app upgrade;
- automatic periodic backup after substantial successful sync, rate-limited;
- rotating retention;
- manual Backup Now;
- Restore workflow;
- integrity check;
- preserve at least one known-valid backup;
- upgrades must not destroy historical data.

Exact retention counts can be implementation defaults in v1.

---

# 18. Main UI / UX

Technology direction:
- WPF
- compact professional dashboard style
- AdminLTE-like organization/density
- not a WinUI/Fluent/Microsoft Store visual style

Characteristics:
- persistent left navigation;
- compact summary cards;
- charts and tables visible efficiently;
- mouse/keyboard first;
- restrained spacing;
- themes supported;
- meaning not encoded by color alone.

Suggested navigation:
- Dashboard
- Analysis
- Battery
- Grid & Utility
- Reports
- Data
- Advanced / Diagnostics
- Settings

Exact names/layout may evolve during UI prototyping without changing product behavior.

---

# 19. Dashboard

Dashboard should show a selective set of high-value information.

Candidate cards:
- Last update
- Current/last-known PV power
- Current/last-known house load
- Battery SOC
- Current/last-known grid import
- Selected-period PV energy
- Selected-period house consumption
- Selected-period grid energy
- Percentage supplied without grid

Also:
- Update Data button
- data freshness/quality indicator
- selected date period
- principal chart(s)

Do not overload dashboard with engineering telemetry.

---

# 20. Interactive analysis

User must be able to choose:
- date period;
- aggregation level;
- metrics;
- table/summary/graph presentation;
- combinations.

Graphs:
- interactive zoom/pan;
- tooltip values;
- legend;
- multiple Y axes when useful;
- synchronized separate panels when too many axes would harm readability.

Tables:
- readable;
- sortable where useful;
- exportable;
- coverage/partial indicators when relevant.

---

# 21. Report presets

Users can save named reusable presets.

Preset can store:
- selected metrics;
- presentation types;
- aggregation;
- graph/table settings;
- fixed or relative period definition;
- summary options;
- theme/report styling where relevant.

Relative examples:
- rolling 7 days;
- current month;
- last 3 complete months;
- rolling 12 months;
- YTD.

---

# 22. Built-in reports

At minimum:

## 22.1 Simple Energy Report

For nontechnical/older readers.

Should contain:
- plain-language headline summary;
- house energy used;
- solar energy generated;
- electricity taken from grid;
- battery contribution;
- percent supplied without grid;
- utility comparison when available;
- one/two simple charts;
- readable glossary.

## 22.2 Detailed Energy Report

More technical metrics/tables.

## 22.3 Battery Report

SOC, charge/discharge power and energy.

## 22.4 Grid / Utility Reconciliation Report

Utility meter vs inverter total grid import.

## 22.5 Financial/Bill Report

Optional, only when tariff/billing data is configured.

---

# 23. Tooltips, legends and glossary

Human-facing terms must be explained in basic language.

The app should include:
- metric tooltips;
- chart legends;
- report glossary.

Example style:

PV generation:
“Electricity produced by the solar panels.”

House consumption:
“Electricity used by everything in the house.”

Battery SOC:
“How full the battery is, shown as a percentage.”

Total grid import:
“All electricity taken from the electricity company, including electricity used directly by the house and electricity that may have charged the battery.”

Technical API names should not be the main user-facing labels.

---

# 24. Excel export

Required:
- .xlsx
- no user-facing CSV requirement

Exports may include:
- summary sheet;
- detailed data sheet;
- selected-period table;
- metadata/quality sheet where useful;
- utility/billing reconciliation sheets.

Excel export should preserve numeric values, dates and readable units.

---

# 25. PDF export

Required printable format.

Priorities:
- large/readable text;
- simple headings;
- charts suitable for print;
- clear tables;
- glossary;
- page numbers;
- selected period;
- generated timestamp;
- data-quality note where relevant.

PDF is static; interactivity remains in the app.

---

# 26. Utility meter observations

Store cumulative utility meter readings.

Fields:
- reading date;
- optional exact time;
- timestamp precision:
  - DATE_ONLY
  - EXACT_TIME
  - ESTIMATED_TIME
- cumulative meter value;
- source/reference;
- notes.

Consumption between observations:
later cumulative value - earlier cumulative value

If boundary times are uncertain:
- show boundary uncertainty;
- do not invent precision.

Utility reconciliation compares against:
- Total Grid Import over the best-matching interval.

---

# 27. Utility bills

Optional fields:
- billing period start/end;
- stated billed consumption;
- actual total amount;
- invoice/reference;
- notes.

Additional bill lines:
- free-text description;
- amount;
- charge/credit sign;
- optional category;
- notes.

Unknown items remain unknown rather than being forced into a wrong category.

---

# 28. Chile tariff acquisition

Financial functionality is optional.

The normal workflow should automatically obtain applicable tariff schedules from official sources.

Preferred hierarchy:
1. official distributor final tariff publications;
2. CNE;
3. other official regulatory/government sources;
4. manual import/entry fallback.

Acquisition may use:
- API/structured data;
- HTML tables;
- spreadsheets;
- deterministic PDF extraction.

Avoid OCR unless unavoidable.

Every tariff import must retain:
- source organization;
- source document/URL;
- retrieval date;
- publication/effective dates;
- distributor;
- commune/network applicability;
- tariff plan;
- classification;
- components/rates;
- parser version;
- content hash;
- active/superseded/retroactive status.

---

# 29. Tariff parser safety

A new tariff publication becomes authoritative only after validation.

Checks should include:
- expected document identity;
- headings/columns;
- units;
- numeric formats;
- applicable plan;
- commune/network row;
- effective dates;
- duplicates/supersession;
- plausibility/range checks.

Failure:
- keep last known valid tariff;
- mark future/unknown period as needing tariff update;
- never silently reuse an inappropriate old rate as current.

---

# 30. Estimated bill calculation

The application should calculate:

Estimated bill according to applicable published tariff rules.

Use where relevant:
- actual grid-import consumption over the billing interval;
- fixed charges;
- variable energy charges;
- transmission/public-service components;
- applicable regulated classifications;
- taxes/percentage components where determinable;
- tariff changes within interval;
- retroactive corrections.

The calculation must be componentized and auditable.

---

# 31. Actual vs estimated bill reconciliation

Report separately:

1. Estimated standard/regulated bill according to published rules
2. User-entered additional charges/credits
3. Actual bill total
4. Residual difference

The residual difference is allowed to remain unexplained.

Do not automatically interpret bill differences as inverter/meter error.

---

# 32. Financial savings semantics

Avoid double counting.

Canonical concepts:

Estimated grid-energy cost avoided:
- estimated value of grid energy that was not imported because demand was served by PV/battery.

Direct-PV contribution:
- subset attributable to direct solar supply where supportable.

Battery-mediated contribution:
- subset attributable to battery discharge only where provenance is supportable.

Do not add overlapping totals.

Historical grid-charged battery discharge must not be labeled solar savings unless provenance supports it.

Financial values must distinguish:
- measured input;
- tariff-derived estimate;
- actual user-entered bill value.

---

# 33. Advanced engineering data

Collect/store when exposed:
- inverter temperature;
- battery voltage/current;
- PV voltage/current/string metrics;
- AC voltage/frequency;
- operating mode/state;
- alarms/faults;
- device online/status;
- raw counters.

These belong in:
- raw/normalized SQL;
- Advanced/Diagnostics views;
- optional detailed reports.

They should not dominate the main dashboard.

---

# 34. Alarms and diagnostics

Store useful read-only alarms/history when available.

Diagnostics logs should include:
- synchronization attempts;
- API/network errors;
- parser failures;
- migrations;
- report-generation failures;
- tariff update outcomes.

Never log:
- passwords;
- tokens;
- cookies;
- reusable signing secrets.

Provide sanitized Export Diagnostics.

---

# 35. Software updates

v1 policy:
- no silent automatic update;
- app may check for a newer official release;
- user explicitly chooses update;
- database backup before schema-changing upgrade;
- migrations versioned;
- portable upgrade preserves Data/Backups.

---

# 36. Installer

Required:
- Windows 11 x64 installer;
- shortcuts;
- correct writable data setup;
- safe upgrade path;
- uninstall should not delete user data by default without explicit confirmation.

---

# 37. Portable distribution

Required:
- unpack and run;
- no formal installation;
- local Data/Backups folder;
- secrets still use appropriate Windows-protected storage where possible;
- portable app data must remain usable across app binary replacement/upgrades.

---

# 38. SQL usability

The database is part of the user's accessible data environment.

Deliver:
- schema documentation;
- table dictionary;
- relationship diagram;
- reporting views;
- example queries.

Likely stable views:
- reporting_hourly_energy
- reporting_daily_energy
- reporting_weekly_energy
- reporting_monthly_energy
- reporting_battery
- reporting_grid_import
- reporting_utility_reconciliation
- reporting_bill_estimates
- data_quality_summary

Exact SQL names will be frozen during database design.

---

# 39. Accessibility/readability baseline

- scalable UI text;
- keyboard-accessible common controls;
- no color-only meaning;
- adequate contrast;
- chart legends/tooltips;
- printable reports optimized for older/nontechnical readers.

No touch-first design requirement.

---

# 40. Testing requirements

Automated tests must cover at minimum:

Time:
- DST transition days;
- week/month/year boundaries;
- rolling windows;
- partial buckets.

Data:
- idempotent sync;
- pagination;
- duplicate prevention;
- null/missing handling;
- late-arrival updates.

Math:
- power-to-energy integration;
- gap thresholds;
- cumulative counter deltas;
- unit conversions;
- sign normalization;
- energy-flow derivations.

Billing:
- tariff effective-date splitting;
- retroactive schedule replacement;
- fixed/variable components;
- additional bill lines;
- residual reconciliation.

Database:
- migrations;
- backup/restore integrity;
- reprocessing from raw.

Real sanitized target-device fixtures should be added after commissioning.

---

# 41. Acceptance milestones

## Milestone A — Architecture proof

Pass when:
- WPF app launches;
- SQLite database works;
- external SQL read access works;
- settings/credential abstraction works;
- installer/portable path strategy is proven.

## Milestone B — Real Solar of Things connection

Pass when:
- user can authenticate;
- actual station/device is discovered;
- capability profile is persisted.

## Milestone C — Trustworthy local history

Pass when:
- all available history is backfilled;
- repeated Update Data is idempotent;
- local reporting works offline.

## Milestone D — Trusted normalization

Pass when:
- primary metrics are mapped/validated;
- units/signs are correct;
- confidence is explicit.

## Milestone E — Core analysis

Pass when:
- required date ranges and aggregations work;
- graphs/tables/summaries use local data.

## Milestone F — Reporting

Pass when:
- presets work;
- Excel export works;
- printable PDF reports work.

## Milestone G — Utility reconciliation

Pass when:
- cumulative meter readings compare correctly with Total Grid Import.

## Milestone H — Financial module

Pass when:
- official tariff acquisition works for the configured service;
- estimated bill is auditable;
- actual bill/additional charges reconcile transparently.

## Milestone I — Release candidate

Pass when:
- backup/restore proven;
- installer and portable proven;
- real household validation completed.

---

# 42. Out of scope for v1

Unless explicitly added later:

- mobile application;
- hosted/cloud dashboard;
- multi-household service;
- multiple human-user accounts/permissions;
- inverter configuration/control;
- firmware update;
- BLE/local serial data acquisition;
- MQTT interception;
- grid-export analytics;
- machine-learning forecasting;
- paid external tariff/data services.

---

# 43. Open items after specification freeze

These are not missing requirements; they are expected commissioning/design outcomes:

1. exact SiSeLi target-device model/protocol/dataSource;
2. exact raw field aliases;
3. exact direct-vs-derived flow availability;
4. target-device historical retention depth;
5. exact data cadence/gap thresholds;
6. target service distributor/tariff/commune configuration;
7. exact UI colors/fonts/theme details;
8. final SQL table names/indexes after schema design;
9. exact installer technology;
10. exact PDF/Excel library choice after proof-of-architecture testing.

These do not block Phase 1.

---

# 44. Specification freeze rule

After user approval, changes to this specification should be treated as one of:

- CLARIFICATION — makes existing behavior more precise without changing scope;
- CHANGE REQUEST — alters or adds product behavior;
- IMPLEMENTATION DECISION — technical choice that preserves specified behavior;
- TARGET-DEVICE RESOLUTION — fills a commissioning-resolved field without changing product intent.

This prevents implementation details from silently changing product requirements.

---

# 45. Current recommended implementation stack

Subject to Phase 1 proof:

- C# / .NET
- WPF
- SQLite
- Microsoft.Data.Sqlite or equivalent supported .NET provider
- ScottPlot or equivalent zero-cost WPF charting library
- ClosedXML or equivalent zero-cost .xlsx library
- PDFsharp/MigraDoc or equivalent zero-cost PDF/reporting library
- Windows protected secret storage
- versioned SQL migrations
- GitHub-based source/release workflow

No paid runtime dependency is intended.

---

# 46. Product definition summary

The product is a local Windows household-energy analysis application that:

- synchronizes Solar of Things cloud data on demand;
- preserves all useful history locally;
- translates device-specific telemetry into understandable household-energy metrics;
- lets the user explore that history over flexible periods and aggregations;
- shows where household energy came from when the data supports it;
- tracks battery behavior;
- reconciles inverter grid import with utility meter readings;
- optionally reconstructs Chilean tariff-based bill estimates from official published tariff data;
- compares those estimates with actual bills;
- exports readable Excel and PDF reports;
- keeps the underlying SQL data accessible for independent querying.

The application prioritizes correctness, traceability, local ownership of data, and understandable presentation over real-time operation or decorative UI.


---

# 47. Approval record

Approved by the user/product owner on 2026-09-24.

Phase 0 is complete. Subsequent requirement changes follow the classification in Section 44.

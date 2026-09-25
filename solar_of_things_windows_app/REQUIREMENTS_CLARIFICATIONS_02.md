# Solar of Things Windows App — Requirements Clarifications and Decisions 02

Date: 2026-09-24

Status: USER-APPROVED DECISIONS

This document supplements:
- `INITIAL_REQUIREMENTS.md`
- `REQUIREMENTS_CLARIFICATIONS_01.md`

## 1. Week definitions

Confirmed:

- Standard week = **Monday through Sunday**.
- A **rolling week** must also be available.

Therefore date-selection/reporting must support both:
- calendar week (Monday–Sunday);
- rolling 7-day period.

## 2. Export formats

Confirmed:

- Excel `.xlsx` is the preferred tabular/data export.
- PDF is the mandatory printable report format.
- CSV is not a user-facing requirement when Excel export is available.
- In-app charts remain interactive; exported reports may be static.

Printable PDF reports must prioritize:
- clarity;
- large/readable labels;
- simple language;
- clear tables/charts;
- glossary in layman terms;
- suitability for older/nontechnical readers.

## 3. Database requirement clarified

The user does **not** specifically require PostgreSQL.

The actual requirement is:

- local SQL database;
- clear, documented schema;
- ability to connect with a normal SQL client;
- ability for the user to run their own SQL queries;
- raw, normalized and reporting data accessible through SQL;
- no opaque/proprietary data store.

PostgreSQL is already installed on the user's computer, but this is context rather than a mandatory dependency.

### Current architecture recommendation

Use **SQLite** as the default application database unless later design work reveals a concrete reason to require PostgreSQL.

Reasons:
- zero cost;
- embedded/local;
- no database server/service required;
- very easy deployment/backup;
- fully queryable with standard SQLite SQL clients;
- appropriate scale for a single household/inverter with ~5-minute telemetry;
- supports views, indexes, joins, window functions and documented relational schemas;
- keeps the application self-contained.

The schema must remain human-readable and documented.

The database file location should be visible/configurable or at least clearly documented so the user can open it with tools such as SQLiteStudio, DB Browser for SQLite, DBeaver, or another compatible SQL client.

PostgreSQL remains a possible alternative if later requirements justify a server database.

## 4. Historical Grid → Battery behavior

Confirmed:

- Historical grid charging should be represented when the data supports it.
- The application must **not invent** battery provenance when it cannot establish it.

The implementation must determine, from actual Solar of Things telemetry/flows/counters/configuration evidence, whether Grid → Battery can be measured or credibly derived for a given period.

Possible states should be explicit, for example:
- measured/confirmed;
- derived with high confidence;
- uncertain;
- unavailable.

The app/report must distinguish these states rather than presenting an uncertain estimate as fact.

## 5. Utility-company input

Confirmed:

Minimum required input:
- reading date/time;
- cumulative utility meter reading.

Optional supported fields should include:
- billed consumption stated by utility;
- bill amount;
- billing period start/end if present;
- invoice/reference number;
- notes.

The app should calculate consumption between arbitrary cumulative readings and compare that interval to local Solar of Things/grid-import data for the exact same dates.

## 6. Battery information

Confirmed desired battery outputs:

- State of charge (%);
- Estimated usable energy remaining (kWh);
- Charging/discharging power (kW);
- Energy charged over selected period (kWh), when supported/derivable;
- Energy discharged over selected period (kWh), when supported/derivable.

Battery settings remain configurable.

Known initial battery:
- SPRO/Techfine LC230-512;
- LiFePO₄;
- nominal 51.2 V;
- usable capacity: 11.776 kWh (label value).

## 7. Simple Energy Report

User accepts the recommendation to include a built-in **Simple Energy Report** for nontechnical readers.

This report is separate from the fully customizable report system.

Purpose:
- summarize the most important household energy results;
- use plain/basic human language;
- be suitable for printing and sharing with older/nontechnical family members.

Likely content:
- household energy used;
- solar energy generated;
- electricity taken from grid;
- battery contribution;
- percentage of household demand supplied without grid;
- utility-company comparison when readings cover the period;
- one or two simple charts;
- plain-language glossary.

Exact wording/layout will be finalized during product specification and UI design.

## 8. Current UI direction

Confirmed visual direction:

- WPF remains acceptable/currently preferred;
- AdminLTE-like dashboard organization/density;
- compact, professional, information-dense;
- left navigation;
- charts/tables/summary cards;
- avoid Microsoft Store / WinUI / Fluent visual language;
- mouse/keyboard-first, not touch-first.

## 9. Current architecture direction

Current preferred stack, subject to final functional specification:

- C# / .NET
- WPF
- SQLite
- documented SQL schema/views
- ScottPlot (or equivalent) for interactive charts
- Excel `.xlsx` export
- PDF printable reports
- Solar of Things cloud API
- fully local data storage/reporting
- explicit Update Data synchronization workflow

No background Windows service is required.

# Product / Functional Specification — Gap Review 01

Date: 2026-09-24

Status: REVIEW BEFORE SPECIFICATION FREEZE

Purpose: identify unresolved definitions, edge cases, and implementation-policy decisions across the current requirements before creating the canonical Product / Functional Specification v1.

Authority reviewed:
- INITIAL_REQUIREMENTS.md
- REQUIREMENTS_CLARIFICATIONS_01.md
- REQUIREMENTS_CLARIFICATIONS_02.md
- REQUIREMENTS_CLARIFICATIONS_03.md
- REQUIREMENTS_CLARIFICATIONS_04.md
- CHILE_ELECTRICITY_TARIFF_RESEARCH_INITIAL.md
- DEVELOPMENT_ROADMAP_V1.md
- completed Solar of Things API research

## Overall result

No major product-goal gap was found.

The project is sufficiently defined to proceed, but several definitions should be frozen before coding because different reasonable interpretations would produce different statistics or UX.

The most important gaps are:
1. exact time-window semantics;
2. historical integration/aggregation math and gap behavior;
3. energy-flow derivation and measured-vs-derived confidence;
4. distinction between household load, grid import and inverter/system losses;
5. current-only telemetry when the app is not continuously running;
6. financial-savings definitions and double-counting avoidance;
7. utility-meter timestamp precision;
8. installed/portable data-folder behavior;
9. SQLite external-query policy;
10. backup/update/recovery policy.

---

## G01 — Timezone and daylight-saving behavior

Solar of Things history requires station timezone/offset discipline. Chile has daylight-saving transitions, so local days can contain 23 or 25 hours.

Recommended rule:
- Store source timestamps internally as UTC plus the original/source timezone context.
- Present/report household time using the station/home IANA timezone.
- Define hour/day/week/month/year boundaries in local household time.
- Do not force every day to contain exactly 24 one-hour buckets.
- DST transition days are legitimate 23/25-hour days.
- Preserve repeated local-hour disambiguation internally by UTC timestamp.

Status: recommended specification default.

---

## G02 — Exact rolling-period semantics

Recommended definitions:
- Rolling 7 days: exact trailing seven-day/time window ending at the selected/current local timestamp.
- Last N months — rolling: subtract N calendar months from the selected/current date/time; do not approximate a month as 30 days.
- Last N complete calendar months: full months immediately preceding the current month.
- Rolling 12 months: current date/time minus 12 calendar months through current date/time.
- YTD: January 1 00:00 local time through selected/current time.
- Calendar year: January 1 through December 31 local time.
- Month range: complete selected calendar months, inclusive.

Open point: whether rolling 7 days should instead mean seven calendar dates.

---

## G03 — Partial aggregation buckets

Recommended rule:
- Include partial first/last buckets.
- Label them as partial.
- Calculate data coverage.
- Do not silently discard them.
- A later optional “complete periods only” switch may be added if useful.

Status: recommended default.

---

## G04 — Power-to-energy integration and missing data

Solar of Things raw history is approximately five-minute cadence with jitter and gaps.

Recommended rule:
- Prefer a validated real server aggregate/counter when trustworthy.
- Else prefer a validated cumulative device-counter delta.
- Else integrate normalized timestamped power.
- Use trapezoidal integration where appropriate.
- Enforce a maximum bridgeable gap.
- Never assume power stayed constant across a long missing interval.
- Calculate coverage and mark incomplete totals accordingly.
- Preserve the calculation method/provenance.

Status: freeze in metric dictionary.

---

## G05 — Measured vs derived energy flows

PV → Load, Battery → Load, Grid → Load and Grid → Battery may not all be directly reported.

Recommended confidence vocabulary:
- MEASURED / CONFIRMED
- DERIVED / HIGH CONFIDENCE
- UNRESOLVED
- UNAVAILABLE

Never solve a power-balance equation by silently assuming unmeasured losses are zero.

Status: exact formulas depend on first real-device commissioning.

---

## G06 — Household load vs grid import vs system losses

Canonical distinctions:
- House Load / Household Consumption
- Grid → House
- Grid → Battery
- Total Grid Import
- Inverter/System Losses — only if directly measurable or credibly derivable

Utility-meter reconciliation must compare the utility meter to Total Grid Import, not merely Grid → House.

Status: recommended terminology.

---

## G07 — No-grid-export handling

“No export” is a topology/configuration fact, not permission to clamp every negative grid reading to zero.

If the API reports a negative grid value:
- preserve it raw;
- determine whether it is sign convention, noise, calibration or another meaning;
- normalize only after validation.

Normal UI should show Grid Export as not applicable/unsupported rather than as a prominent permanent zero.

Status: recommended default.

---

## G08 — Battery energy remaining

Estimated usable energy remaining = configured usable capacity × normalized SOC.

Clearly label it as an estimate derived from BMS SOC and configured usable capacity.

Status: already directionally approved.

---

## G09 — Current-only telemetry without continuous background collection

Some Solar of Things fields may be current-only and absent from historical endpoints.

Recommended rule:
- On every Update Data, capture one current-state snapshot.
- Backfill all historized fields.
- Preserve current-only fields as sparse snapshots.
- Do not imply continuous historical coverage for them.
- Do not add a Windows background service unless a later concrete requirement depends on a current-only metric.

Status: user acceptance useful because this follows directly from the manual-update preference.

---

## G10 — Utility-meter reading timestamp precision

Recommended data model:
- reading date;
- optional exact time;
- timestamp precision: DATE_ONLY / EXACT_TIME / ESTIMATED_TIME;
- cumulative value;
- source/reference.

If only a date is known, reconciliation should disclose boundary uncertainty rather than invent an exact midnight reading.

Open point: whether the user's historical records normally have date only or exact time.

---

## G11 — Unit and display conventions

Recommended defaults:
- UI language: English.
- Power: W below 1 kW where useful; otherwise kW.
- Energy: normally kWh.
- Battery SOC: %.
- Voltage: V.
- Current: A.
- Frequency: Hz.
- Money: CLP with Chilean thousands formatting and no artificial decimal cents unless required.
- Database: canonical values remain unrounded.
- UI rounding is display-only.

Open point: date/time display format. Recommended: dd-MM-yyyy + 24-hour time.

---

## G12 — Financial savings semantics

This is a material conceptual gap.

The battery does not create energy; it time-shifts energy. Solar stored in the battery and later discharged must not be counted twice as both solar savings and separate battery-created savings.

Recommended rules:
- Estimated grid-energy cost avoided = value of grid energy not imported because demand was served by PV/battery.
- Direct-PV contribution to avoided import may be shown.
- Battery-mediated contribution may be shown only where provenance is supportable.
- Total avoided-import value is one total and must not double-count subcomponents.
- For historical grid-charging periods, do not treat battery discharge as solar savings unless provenance is established.

Status: must be explicit in final specification.

---

## G13 — Actual bill vs estimated bill

Recommended rule:
Maintain:
- estimated tariff-rule components;
- user-entered additional charges/credits;
- actual total;
- unexplained residual difference.

Do not force the app to explain every residual.

Status: recommended addition.

---

## G14 — Installed vs portable data location

Recommended design:

Portable:
- portable-root\\App\\
- portable-root\\Data\\energy.db
- portable-root\\Backups\\

Installed:
- default to a writable application-owned folder such as C:\\SolarEnergyMonitor\\ rather than Program Files;
- same App/Data/Backups structure;
- installer handles permissions;
- user may choose another writable location.

Alternative: Program Files + ProgramData, but that violates the user's preference for co-location.

Open point: acceptance of non-Program-Files install-folder approach.

---

## G15 — SQLite external SQL access

Recommended rule:
- Enable SQLite WAL mode.
- Document database path/schema.
- Encourage external SQL clients to use read-only mode while the app runs.
- Direct external writes to internal tables are unsupported because they can violate invariants.
- Provide stable reporting views.
- Do not encrypt the whole DB if that blocks ordinary SQL-client access.
- Credentials remain outside SQLite in Windows secure storage.

Status: recommended product rule.

---

## G16 — Raw storage definition

Retain:
- endpoint/source family;
- device/station IDs;
- source timestamp;
- retrieval timestamp;
- raw key;
- raw value;
- raw unit/metadata where supplied;
- normalization version;
- optionally original JSON payloads where useful.

Never retain reusable authentication secrets in raw payloads/logs.

Status: freeze in DB spec.

---

## G17 — Reprocessing after rule changes

Required capability:
- version normalization/calculation rules;
- rebuild normalized/calculated history from retained raw data;
- do not redownload history merely because a rule was corrected.

Status: important missing explicit requirement; recommend adding.

---

## G18 — Data revision and late-arrival policy

Every Update Data should:
- continue from the last source timestamp with overlap;
- re-read a recent trailing period;
- upsert changed samples;
- refresh current-day aggregates;
- periodically revalidate a somewhat longer recent period.

Do not assume previously downloaded cloud data is immutable.

Status: research-backed sync policy.

---

## G19 — Database backup and recovery defaults

Recommended:
- automatic backup before schema migration/app upgrade;
- automatic periodic backup after substantial successful sync, rate-limited;
- rotating backup retention;
- manual Backup Now;
- restore workflow;
- integrity checks;
- never auto-delete the only valid backup.

Status: implementation default unless user wants different behavior.

---

## G20 — Application software updates

Recommended v1:
- no silent auto-update;
- optionally check official project/GitHub release source when requested;
- show available version;
- user explicitly chooses to update;
- back up database before upgrade/migration;
- portable upgrade preserves Data/Backups.

Status: user preference useful.

---

## G21 — Diagnostics/logging

Maintain local structured diagnostics for sync, API errors, tariff parser failures, migrations and report failures.

Never log passwords, tokens, cookies or reusable signing secrets.

Provide sanitized Export Diagnostics.

Status: recommended.

---

## G22 — Accessibility/readability

Recommended baseline:
- scalable UI text;
- keyboard navigation for common controls;
- do not encode meaning by color alone;
- chart legends/tooltips;
- adequate contrast.

Status: recommended baseline.

---

## G23 — Testing/correctness gates

Automated tests should cover:
- timestamp/DST boundaries;
- aggregation/integration;
- missing-data behavior;
- flow equations;
- unit/sign normalization;
- tariff-date splitting;
- bill arithmetic;
- database migrations;
- idempotent sync.

Use sanitized real-device fixtures after commissioning.

Status: engineering requirement.

---

## G24 — Scope of “collect broadly”

Collect/store useful read-only:
- telemetry;
- state;
- historical samples;
- energy counters;
- alarms;
- device/station technical metadata needed for interpretation.

Do not deliberately archive unrelated account/profile data, secrets or irrelevant mutation/configuration payloads.

Status: recommended.

---

# Gap-review conclusion

No additional major feature-discovery round is needed.

Most gaps can be incorporated into Functional Specification v1 using the recommended defaults.

The remaining user-facing clarifications are:

A. Rolling week: exact trailing seven-day/time window versus seven calendar dates.
B. Current-only telemetry: accept sparse snapshots when using manual Update Data instead of a background collector.
C. Utility meter reading precision: historical readings are date-only, exact-time, or mixed.
D. Date/time display: recommended dd-MM-yyyy + 24-hour clock.
E. Installed data location: accept writable app-owned folder such as C:\\SolarEnergyMonitor\\ rather than Program Files.
F. Software updates: recommended explicit/manual update, no silent auto-update.

All other gaps should be incorporated into the canonical specification unless the user objects.

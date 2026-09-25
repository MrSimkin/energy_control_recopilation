# Solar of Things Windows App — Requirements Clarifications and Decisions 03

Date: 2026-09-24

Status: USER-APPROVED DECISIONS

This document supplements:
- `INITIAL_REQUIREMENTS.md`
- `REQUIREMENTS_CLARIFICATIONS_01.md`
- `REQUIREMENTS_CLARIFICATIONS_02.md`

## 1. Windows support

Target platform is:

- Windows 11
- x64 only

No requirement for Windows 10, ARM64, macOS or Linux.

## 2. Credentials

Application will run only on the user's personal laptop.

Requirements:

- Support **Remember credentials/session** and **Do not remember** modes.
- Stored credentials/session secrets must use Windows-protected secure storage.
- Secrets must never be written to logs, reports, exported files or the application database in plaintext.

## 3. Multiple devices

The application is for one household.

Multiple-device support is acceptable at the data-model level for future-proofing, but is not a prominent product requirement and should not complicate the normal user experience.

Current expected usage:
- one household;
- one inverter installation;
- multiple devices only if Solar of Things internally exposes associated logger/battery/device objects or if the home setup changes in the future.

## 4. Database location and backups

User preference:

- do **not** place the database under the Windows user-profile folders;
- keep application data together with the installation/portable application location when technically safe.

Windows permission constraint:

- a normal installer placed under `C:\Program Files\...` should not rely on writing its live SQLite database directly inside Program Files, because standard Windows permissions can prevent normal writes without elevation.

Therefore the implementation must preserve the user's intent without requiring administrator rights for normal app operation.

Preferred direction:
- portable build: database can live in a writable `data\` folder beside the executable;
- installed build: installer should use a writable application-owned location outside the user profile, such as a user-selected folder (for example `C:\SolarOfThingsApp\`) or another explicitly writable shared application-data location;
- exact path/backup strategy will be finalized in the product specification;
- database file location must be documented and easy for the user to open with a SQL client.

Automatic backup/restore strategy is delegated to application design.

## 5. Appearance

Visual direction is already sufficiently defined.

Reference:
- AdminLTE-like organization/density;
- compact, professional dashboard;
- a small set of themes is acceptable;
- exact typography/icons/colors can be finalized during UI implementation.

No further visual-design requirement is needed before specification.

## 6. Distribution

Both distribution modes are required:

1. **Installer**
   - normal Windows installation experience;
   - creates shortcuts and required directories/configuration.

2. **Portable**
   - unpack/run version;
   - no formal installation required;
   - self-contained local data folder where practical.

## 7. Utility prices and cost/savings

Financial analysis is now an optional feature requirement.

The application should be capable of supporting:

- electricity cost estimates;
- grid electricity cost over a selected period;
- estimated savings attributable to solar/battery operation;
- comparisons between measured/imported grid energy and billed utility values.

However:

- Chilean residential electricity tariffs are not a single flat universal rate;
- tariffs vary by distribution company, tariff option, regulated components, taxes, fixed charges, billing period and regulatory updates;
- exact tariff modelling therefore requires a configurable tariff model rather than one hard-coded CLP/kWh value.

A separate tariff research/design note will define the Chilean billing/tariff data model before implementation.

Financial features must be optional and must not block the core energy-monitoring/statistics application.

## 8. Advanced engineering values

Confirmed principle:

**Collect broadly, present selectively.**

The collector should retain useful read-only telemetry exposed by Solar of Things even when a field is not part of the main dashboard.

Examples may include:
- inverter temperature;
- battery voltage/current;
- PV voltage/current;
- AC voltage/frequency;
- inverter operating mode/state;
- alarms/faults;
- device status;
- additional raw energy counters.

These values:
- should remain available in raw/normalized SQL data;
- may be available in advanced/detail views;
- should not clutter the primary household-energy dashboard.

## 9. Current product principle

The application should optimize for:

- one household;
- one Windows 11 x64 laptop;
- local/offline historical access;
- explicit manual data synchronization;
- broad data preservation;
- simple, selective presentation;
- zero paid software/services required.

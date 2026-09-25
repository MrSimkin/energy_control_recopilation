# Solar of Things Windows App — Requirements Clarifications and Decisions 04

Date: 2026-09-24

Status: USER-APPROVED DECISION

This document supplements:
- `INITIAL_REQUIREMENTS.md`
- `REQUIREMENTS_CLARIFICATIONS_01.md`
- `REQUIREMENTS_CLARIFICATIONS_02.md`
- `REQUIREMENTS_CLARIFICATIONS_03.md`
- `CHILE_ELECTRICITY_TARIFF_RESEARCH_INITIAL.md`

## 1. Automatic Chilean tariff acquisition

The user does not want the normal workflow to require manually entering every tariff component.

The application should, when the optional financial module is enabled:

1. identify/configure the user's electricity distributor, tariff option and geographic applicability;
2. retrieve from the Internet the official tariff schedules needed for the applicable service;
3. parse and store those tariff schedules locally;
4. preserve the original source reference/document metadata;
5. version every tariff by effective period;
6. automatically use the tariff version(s) applicable to the requested historical billing interval;
7. handle retroactive tariff publications/corrections without overwriting the historical evidence trail;
8. allow the user to update tariff data with an explicit **Update Tariffs** operation (and optionally as part of Update Data).

Preferred source hierarchy:

1. **Official electricity distributor final published tariff schedules** for rates actually applicable to the service/commune.
2. **Comisión Nacional de Energía (CNE)** regulatory publications for authority, validation, classifications and cross-checking.
3. Other official government/regulatory sources only when required.
4. Manual entry/import as a fallback, not the normal workflow.

The implementation may use an API, structured download, HTML parsing, PDF table extraction or another deterministic retrieval method depending on what each official source provides.

The application must not depend on a paid tariff-data service.

## 2. Tariff acquisition must be auditable

Every imported tariff/rule should retain metadata such as:

- source organization;
- source URL/document;
- retrieval date;
- publication/effective date;
- distributor;
- commune/network applicability;
- tariff option;
- tariff/classification band;
- parsed rate components;
- parser/import version;
- whether the row is active, superseded or retroactively corrected.

The app should make it possible to answer:

> “Which official tariff publication was used to calculate this estimated bill?”

## 3. Estimated bill according to published rules

The desired financial result is not merely an energy-cost estimate.

The application should calculate an:

**Estimated bill according to the applicable published tariff rules**

using, where applicable:

- cumulative/grid-import consumption over the bill interval;
- fixed charges;
- variable energy charges;
- transmission/public-service components;
- applicable tariff and consumption classifications;
- taxes or regulated percentage components when applicable and determinable;
- tariff changes within a billing interval;
- retroactive corrections when applicable.

The exact calculation must remain transparent and broken down into components.

## 4. Actual bill and additional charges

The actual electricity bill remains separate evidence.

The user can enter:

- actual total bill amount;
- cumulative meter reading(s);
- billed consumption;
- billing period;
- optional invoice/reference information.

The bill can contain additional charges that are not predicted by the standard energy-tariff engine.

Therefore the application must also support **additional/other bill lines**, for example:

- regulated ancillary services;
- reconnection/disconnection or meter-related services;
- interest/late-payment charges;
- prior-period adjustments;
- credits;
- subsidies/benefits;
- third-party charges;
- other distributor-specific items.

Each optional bill line should support at least:

- description;
- amount in CLP;
- sign (charge/credit);
- optional category;
- optional notes.

Do not force an unknown line into an incorrect tariff category.

## 5. Reconciliation

Reports should distinguish clearly between:

1. **Estimated regulated/standard bill according to published rules**
2. **Additional/other bill charges or credits**
3. **Actual bill total entered by user**

The app should calculate the differences between them and explain them in plain language.

A difference between estimated and actual bill must never automatically be interpreted as an inverter/meter error.

## 6. Offline behavior

Once tariff schedules have been downloaded and stored locally:

- historical financial reports should work without Internet access;
- the application should not re-fetch tariff data for every report;
- tariff data should be updated incrementally/versioned, like Solar of Things data.

## 7. Failure behavior

If automatic retrieval/parsing of an official tariff publication fails:

- do not silently invent or reuse an inappropriate rate;
- retain the last known valid tariff;
- mark the affected future/unknown period as needing tariff update;
- permit manual review/import as fallback;
- never present an uncertain bill estimate as exact.


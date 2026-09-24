# Round 13 — Cloud-UI Coverage Audit

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_13_cloud_ui_coverage_matrix.md`

## Scope

This round asks one final architectural question before live account validation:

> Of the useful monitoring/statistics information visible in the Solar of Things cloud experience, what can already be retrieved through known cloud API surfaces?

The audit covers only read-only cloud/dashboard information relevant to a future Windows statistics application.

Explicitly out of scope:

- BLE / Proximal Monitoring;
- Wi-Fi provisioning;
- raw serial / Modbus;
- MQTT interception;
- firmware/control/settings mutation;
- peak-valley configuration;
- device diagnosis that changes network/device state;
- account-maintenance UI;
- report-generation jobs except as optional audit/export surfaces.

No authenticated request was made against the user's account.

---

## 1. Executive conclusion

**No major dashboard-relevant cloud-data surface remains unmapped.**

The useful Solar of Things UI families all correspond to already-known cloud API families:

- home/dashboard headline statistics;
- project/station counts and status;
- station/device lists;
- current producing power and generation totals;
- energy-flow diagram;
- detailed device state;
- device attribute metadata;
- alarms;
- parameter/history curves;
- daily/monthly/yearly/cumulative generation;
- station/device/owner aggregates;
- station income/value series;
- environmental-impact summary metrics.

What remains is not “find another hidden API family”.

The remaining work is:

1. validate which of these surfaces are populated for the user's actual inverter/account;
2. determine the exact field aliases/units/signs for that device;
3. determine which aggregate properties are real versus placeholders;
4. compare UI values and API values at the same timestamps;
5. select the minimal production subset for the eventual Windows dashboard.

Therefore the planned research program does **not** need another broad public-source round after this one.

---

## 2. UI structure evidence

A current publicly mirrored Solar of Things owner guide describes two high-level modes.

### Classic Mode

The Home page exposes:

- aggregated device statistics;
- realtime device status;
- device status/filtering;
- total generation data;
- operating/working status;
- navigation into a device's detailed monitoring page.

The Device page exposes:

- all added devices;
- device statuses;
- search/filter;
- navigation into detailed device monitoring.

### Advanced Mode

The Home page exposes:

- project/station count;
- device count;
- monitoring/statistical data;
- today's generation;
- current-month generation;
- current-year generation;
- cumulative generation;
- monthly generation chart.

The Project page exposes:

- station/project operating state;
- generation information;
- counts of normal/warning/offline devices;
- search/filter;
- navigation into project operation/monitoring.

### Device detail / Overview

The guide describes:

- equipment information;
- detailed state information;
- energy-flow diagram;
- drill-down on Grid, Inverter, Photovoltaic, Battery and Load;
- current device alarm list.

### Data / Analysis

The guide describes:

- parameter curves;
- user-selectable parameters;
- realtime data/statistics;
- daily/monthly/annual generation;
- “Data Board” current device state;
- historical collected device data.

These descriptions match the current production/HAR/API surfaces already reconstructed in earlier rounds.

---

## 3. Coverage status by UI family

### 3.1 Home / dashboard headline statistics

**COVERED**

Known API:

`POST /apis/dashboard/summary/commons`

Known fields include:

- `dailyProducedQuantity`;
- `totalProducedQuantity`;
- `totalPower`;
- `devicesNumber`;
- `allInstalledCapacity`;
- `stationTotalNumber`;
- `stationStateSummary`;
- `savingStandardCarbon`;
- `co2EmissionReduction`;
- `so2EmissionReduction`;
- `noxEmissionReduction`.

This covers:

- project/station count;
- device count;
- current/summary power;
- today's generation;
- cumulative generation;
- station-state distribution;
- environmental-impact widgets.

### Remaining validation

- exact unit/display scaling for `totalPower`;
- exact units for all environmental metrics on the target account;
- whether all environmental values are populated rather than zero/default.

---

## 4. Monthly generation chart

**COVERED**

Known dashboard API:

`POST /apis/dashboard/summary/station/generatedEnergy/monthly`

Station/device alternatives also exist:

- `stationOverView/*`;
- `deviceOverView/*`;
- category summary APIs.

The dashboard-level monthly series is sufficient to reproduce the portal's account/portfolio monthly-generation chart.

For a one-station Windows dashboard, station-level aggregate sources are usually preferable because their scope is explicit.

---

## 5. Daily / monthly / yearly / cumulative generation

**COVERED**

Known families:

### Device

- `deviceOverView/generatedEnergy/daily`;
- `deviceOverView/generatedEnergy/monthly`;
- `deviceOverView/generatedEnergy/yearly`;
- `deviceOverView/generatedEnergy/total`.

### Station

- station overview generation/summary endpoints at daily/monthly/yearly/total levels.

### Owner/account

- owner overview station aggregate family.

### Dashboard

- dashboard monthly/summary routes.

Round 11 already established that real-valued PV generated-energy aggregates are the strongest period-total source currently known.

---

## 6. Project/station list and operational status

**COVERED**

Known APIs:

- `POST /apis/station/list`;
- `GET /apis/station/details`;
- `GET /apis/station/state/count`;
- `POST /apis/dashboard/summary/commons`.

Station metadata covers:

- ID/name;
- timezone;
- location;
- station type;
- grid-connection type;
- online/state;
- installed capacity;
- total/current power;
- daily produced quantity;
- total produced quantity.

Dashboard `stationStateSummary` and station-state count cover normal/warning/offline-style portfolio counts.

### Remaining validation

Exact numeric-to-display state mapping should be verified from the target account/dictionaries rather than hard-coded from assumptions.

---

## 7. Device list and status

**COVERED**

Known APIs:

- `POST /apis/device/list`;
- `GET /apis/device/details`;
- `GET /apis/device/state/count`.

Device records can expose:

- name;
- serial;
- model;
- online/state;
- station;
- rated power;
- producing power;
- daily produced quantity;
- total produced quantity;
- software version;
- last-data and last-online timestamps.

This covers the Classic Device page's read-only cards and filtering needs.

---

## 8. Energy-flow diagram

**COVERED**

Known APIs:

### Device

`GET /apis/deviceState/simple/energy/flow/v1`

### Station

`GET /apis/station/energy/flow`

Known flow-node concepts include:

- PV;
- grid;
- battery;
- load;
- generator;
- UPS;
- CT/current-transformer where supported.

Current production device-page evidence directly observed the device energy-flow call.

### Important caveat

Some devices return:

`70132 Energy flow rule not exists`.

That means energy-flow visualization is not universally supported/configured.

It is a capability difference, not a missing API.

---

## 9. Drill-down: Grid / Inverter / PV / Battery / Load details

**COVERED, SCHEMA-DRIVEN**

The UI guide describes clicking nodes of the energy-flow diagram to view detailed information.

Known supporting APIs:

- `GET /deviceState/simple/state/latest/v1`;
- `GET /deviceState/simple/gatherAttributes/v1`;
- attribute group/query APIs;
- energy-flow state itself.

Round 09 established that the field set is model/protocol dependent.

Therefore the correct Windows implementation is dynamic:

`attribute metadata + current state + device/protocol context`

rather than a universal fixed list.

### Remaining validation

Which exact fields appear under each visual category for the user's inverter must be learned from the user's attribute metadata.

---

## 10. “Data Board” current device state

**COVERED**

The guide says the Data Board displays current device status.

Known API:

`GET /apis/deviceState/simple/state/latest/v1?deviceId=<id>&dataSource=<n>`

Possible fallback/complement:

`GET /apis/deviceState/simple/energy/flow/v1`

Some firmware families populate one path more usefully than the other.

This is a target-device selection question, not an API-coverage gap.

---

## 11. Realtime parameter curves

**COVERED**

Current production Device details → Data Analysis was directly observed using:

`POST /apis/deviceState/simple/attribute/keys/history/v1`

with:

- selected attribute keys;
- explicit local-day range;
- station/local timezone;
- pagination;
- aligned timestamp/field arrays.

A fresh captured point was previously verified against the official chart tooltip.

Thus the same data used by the cloud UI is available programmatically.

---

## 12. Historical device data

**COVERED**

Known history families:

1. `deviceState/simple/attribute/keys/history/v1`;
2. `deviceState/simple/attribute/record/list/v1`;
3. row-oriented `deviceState/attribute/record/list` fallback.

Round 10 established:

- pagination works;
- daily backfill works;
- several months of raw history have been demonstrated;
- missing/null values must not become zero;
- station timezone matters.

No separate hidden UI-only history mechanism is needed for the dashboard.

---

## 13. User-selectable parameter catalog

**COVERED**

Known API:

`GET /apis/deviceState/simple/gatherAttributes/v1`

Current production evidence observed this directly on the Data Analysis page.

It exposes device-specific:

- key;
- name/display name;
- unit;
- value type;
- category;
- visibility;
- read/config characteristics.

This is sufficient to populate a “choose data to graph” UI dynamically.

---

## 14. Alarm list / current abnormal state

**COVERED**

Known APIs:

- `POST /apis/alarm/getLatestAlarm`;
- `POST /apis/alarm/query/list`.

Alarm objects can contain:

- device;
- station;
- severity/level;
- state/category;
- title/name;
- content/message;
- fired/created time;
- processed/restored state/time depending representation.

This covers the read-only alarm information described in the device UI.

Alarm exports exist but are unnecessary for normal dashboard ingestion.

---

## 15. Normal / warning / offline counts

**COVERED**

Candidate/observed APIs:

- `dashboard/summary/commons.stationStateSummary`;
- `GET /station/state/count`;
- `GET /device/state/count`;
- filtered station/device lists.

This is enough to reproduce project-page operational counts.

### Remaining validation

Use API-provided state labels/dictionaries where available rather than assuming hard-coded numeric enum meanings.

---

## 16. Current producing power / working status

**COVERED**

Useful sources:

- device list `producingPower`;
- station list/detail `totalPower` / production fields;
- latest state;
- energy flow;
- `generationPower/daily` / power-class detail for time series.

Working/operating mode can appear as a dynamic device-state attribute.

### Caveat

“Working status” is device/protocol-specific and should be treated as a state attribute, not one universal enum.

---

## 17. Station income / monetary-value charts

**API-MAPPED, SEMANTICS NOT YET VALIDATED**

Known endpoints:

- `stationOverView/income/daily`;
- `stationOverView/income/monthly`;
- `stationOverView/income/yearly`;
- `stationOverView/income/total`.

Station metadata also includes:

- currency;
- energy-income price.

Thus the data surface is mapped.

However, Round 11 could not establish the exact formula or billing meaning.

### Status

Not a coverage gap.

It is a **semantic-validation gap** and only matters if the future dashboard wants SiSeLi's monetary-income metric.

---

## 18. Environmental-impact metrics

**COVERED AS API DATA**

Dashboard/device data exposes fields such as:

- `savingStandardCarbon`;
- `co2EmissionReduction`;
- `so2EmissionReduction`;
- `noxEmissionReduction`.

Independent client documentation interprets at least:

- standard-carbon saving;
- CO₂ reduction

as kg-scale environmental metrics.

### Status

API mapped, optional.

Exact units/factors for all environmental metrics should be validated before displaying them as authoritative statistics.

They are not necessary for the core energy dashboard.

---

## 19. Station ranking

**API-MAPPED, OPTIONAL**

Known API:

`POST /apis/dashboard/summary/station/dailyGenerationTimeRank`

Returns station ranking entries including:

- name;
- `fullTime`.

This is useful for multi-station portfolio dashboards.

It has almost no value for a single-installation Windows dashboard.

No further investigation is warranted unless the eventual project requires portfolio ranking.

---

## 20. Geographic station distribution

**API-MAPPED, OPTIONAL**

Known API:

`POST /apis/dashboard/summary/station/distribution/location`

Supports geographic bounds/level and returns station locations.

Useful for:

- multi-site map/dashboard;
- fleet/portfolio view.

Not needed for a single-site local statistics dashboard.

No gap remains.

---

## 21. Equipment information

**COVERED**

The UI's equipment-information page is backed by:

- `GET /device/details`;
- station/device list records;
- gather-protocol/device-sort metadata;
- current state metadata where needed.

Relevant fields include:

- model;
- serial;
- software version;
- station;
- protocol;
- rated power;
- last-data timestamps.

Editing/deleting the device is out of project scope.

---

## 22. Search and filtering

**COVERED, BUT MOSTLY A UI CONCERN**

The cloud list APIs themselves support filters such as:

### Stations

- name;
- state;
- station type;
- grid type;
- ordering.

### Devices

- name;
- serial;
- station;
- state;
- sort key;
- gather protocol;
- software version;
- ordering.

For a small Windows installation, fetching the user's limited station/device inventory and filtering locally may be simpler.

No data gap exists.

---

## 23. API data not prominently required by the user's dashboard goal

The cloud API exposes several read-only surfaces that are mapped but should remain optional.

### Portfolio/environmental

- carbon/CO₂/SO₂/NOx savings;
- station geographic distribution;
- station generation-time ranking;
- account-wide owner summaries.

### Administrative/diagnostic reads

- dictionaries;
- protocol metadata;
- detailed report/export metadata;
- firmware lists/logs;
- SIM metadata;
- organization/user hierarchy.

### Read-only configuration

Some cached device configuration is readable, but configuration-history/statistics are not part of the current project goal.

These do not justify further broad investigation.

---

## 24. UI-visible features intentionally excluded

The manual also describes features that are visible in Solar of Things but irrelevant to the stated dashboard acquisition goal.

They remain officially out of scope:

- Add Device;
- Wi-Fi Configuration;
- Device Diagnosis that modifies network parameters;
- Peak Shaving & Valley Filling configuration;
- control commands;
- Proximal Monitoring / Bluetooth;
- Debug;
- account/security/profile modification;
- device edit/delete.

Their presence in the official UI does **not** count as a coverage gap.

---

## 25. Coverage matrix summary

### Fully mapped for API access

- authentication/session;
- station/project inventory;
- device inventory;
- online/status metadata;
- current producing power;
- current device state;
- energy flow;
- dynamic attribute catalog;
- alarms;
- selected parameter curves;
- historical telemetry;
- daily/monthly/yearly/cumulative PV generation;
- server summary categories;
- station/device/owner aggregation levels;
- environmental summary metrics;
- station income series;
- state counts;
- dashboard monthly generation;
- station ranking;
- station location distribution.

### API family mapped, exact semantics/device behavior still require validation

- working-mode labels/enums;
- grid/load/battery field aliases and signs;
- consumption/grid-buy/grid-sell aggregate quality;
- battery charge/discharge aggregate quality;
- income calculation semantics;
- environmental metric units/factors;
- energy-flow availability;
- device-specific `dataSource`;
- state-count labels;
- exact current official UI choice between overlapping API sources.

### No important dashboard-relevant cloud family remains unknown

This is the central result of Round 13.

---

## 26. Reconstructability assessment

The known cloud API is now sufficient to reconstruct, in an independent Windows client, the read-only data portions of the Solar of Things experience relevant to statistics:

```text
Home / portfolio summary
  ├── station/device counts and status
  ├── current + cumulative generation
  ├── monthly generation chart
  └── optional environmental metrics

Station
  ├── identity / location / timezone
  ├── online + production state
  ├── energy flow
  ├── generation aggregates
  └── income aggregates

Device
  ├── identity/model/protocol
  ├── current state
  ├── energy flow
  ├── alarms
  ├── dynamic attribute catalog
  ├── selected parameter history
  └── generated-energy aggregates
```

Nothing in that read-only hierarchy requires Android.

Nothing in that hierarchy requires intercepting the inverter's 2.4-GHz Wi-Fi communication.

---

## 27. What final live validation must prove

Public research is now at diminishing returns.

The remaining high-value questions can only be answered reliably against the user's own account/device.

### Identity

- station ID(s);
- device ID(s);
- model;
- manufacturer;
- gather protocol/version;
- station timezone;
- device `dataSource`.

### Current telemetry

Compare the same moment between official UI and API for:

- PV power;
- load/output power;
- grid import/export power where present;
- battery power/current/SOC where present;
- operating state.

### History

Confirm:

- which keys are actually returned;
- cadence;
- oldest raw date;
- pagination;
- null/gap behavior;
- timezone/day boundaries.

### Aggregates

Compare official UI with API for:

- daily PV generation;
- monthly PV generation;
- yearly PV generation;
- lifetime generation;
- consumption if shown;
- grid import/export if shown;
- battery charge/discharge energy if shown.

### Quality

Determine which properties are:

- real;
- placeholder;
- absent;
- inconsistent.

### Reliability

Measure:

- normal source lag;
- token expiry metadata;
- refresh behavior;
- late-arrival overlap requirement.

---

## 28. Final validation should remain read-only

The final phase does not need:

- setting changes;
- control writes;
- device restarts;
- firmware actions;
- BLE;
- local interception.

It can be completed with:

- user-owned credentials/session;
- read-only API calls;
- optional official-UI side-by-side comparison;
- sanitized capture/results.

No device behavior needs to be changed.

---

## 29. Evidence sources used

### Current/public UI structure

A publicly mirrored Solar of Things Owner/User Guide documents:

- Classic Mode Home/Device;
- Advanced Mode Home/Project;
- device Overview;
- energy-flow drill-down;
- alarms;
- Analysis;
- Realtime Data;
- Historical Data.

The guide references the official web portal `https://solar.siseli.com/`.

### API / current production evidence

- fresh production browser capture preserved in Round 03;
- current first-party-derived API/OpenAPI corpus;
- `vvkor/python-siseli` HAR-derived route catalog and models;
- `lujian1324-spec/energy-app` current production history capture;
- aggregate/history/telemetry findings from Rounds 09–12.

The current public portal itself remains a JavaScript shell when opened without an authenticated browser session, so unauthenticated HTML cannot enumerate its internal cards.

---

## 30. Round-close conclusion

**Round 13 is complete.**

There is no evidence-based reason to continue broad public archaeology before using the user's own account.

The API coverage problem is solved to a sufficient level:

> **Every major read-only cloud data family relevant to the planned Windows statistics dashboard has a known API path or API family.**

The remaining uncertainty is installation-specific behavior, not platform discovery.

---

## 31. Next-round decision

No additional broad research round is warranted.

The only planned surviving phase is:

# Final controlled read-only validation against the user's own Solar of Things account/device

That phase should be run only when the user is ready to provide or authorize a safe read-only way to access their account/session.

If final validation reveals one concrete unmapped cloud statistic, create a **targeted gap round for that statistic only**.

Do not reopen BLE, MQTT, raw serial, Android-local internals, provisioning or controls unless the user explicitly changes project scope.

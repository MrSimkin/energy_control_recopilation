# Round 11 — Server-Side Statistics, Aggregations and Reports

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_11_aggregation_trust_matrix.md`

## Scope

This round investigates only **cloud-side calculated statistics, aggregates and report surfaces** that may be useful to the future Windows dashboard.

It answers:

- what SiSeLi calculates server-side;
- the aggregation levels available;
- which time bucket each endpoint represents;
- which properties have strong real-world evidence;
- how `isRealValue` must be interpreted;
- when server totals should be preferred over calculations from raw ~5-minute telemetry;
- whether report/export endpoints are useful as a normal data-ingestion path.

Out of scope remains:

- MQTT/uplink interception;
- BLE;
- local serial/Modbus;
- Android-local behavior;
- provisioning;
- device/control/firmware mutation.

No request was made against the user's own account.

---

## 1. Executive conclusion

The cloud aggregation APIs are highly useful, but **trust must be assigned per metric/property, not per endpoint family**.

### Strongest current conclusion

For **PV generated energy**, the server-side aggregation is currently the best candidate for authoritative dashboard totals.

Evidence:

1. a real month (October 2025) produced:
   - raw-history integration: **200.58 kWh**;
   - SiSeLi monthly aggregate: **201.06 kWh**;
   - difference: **0.48 kWh**;
2. another real installation compared one day's cumulative telemetry-counter delta:
   - `MAX(totalPowerGeneration) - MIN(totalPowerGeneration)` = **6.131 kWh**;
   - station summary = **5.581 kWh**;
   - device snapshot also = **5.581 kWh**;
   - that dashboard therefore stopped using the cumulative-counter delta for daily generation.

These independent examples support:

> **Use SiSeLi's real-valued server aggregate for PV energy totals when available; use raw power history mainly for curves, sub-period analysis and validation.**

### Important exception

The same confidence does **not** extend automatically to every property in the aggregate category.

On one real installation:

- `consumeElectricityQuantity` was placeholder/unusable;
- `buyElectricityQuantity` was placeholder/unusable.

That dashboard instead used telemetry cumulative daily counters:

- `loadDayElectricityConsumption`;
- `dayPurchaseElectricityConsumption`.

Therefore:

> **A category response can contain trustworthy PV generation and untrustworthy consumption/grid fields at the same time.**

---

## 2. Aggregation hierarchy

SiSeLi exposes closely related aggregate APIs at several hierarchy levels.

### 2.1 Device level

`/deviceOverView/*`

Documented current family includes:

- `POST /deviceOverView/generationPower/daily`
- `POST /deviceOverView/generatedEnergy/monthly`
- `POST /deviceOverView/generatedEnergy/yearly`
- `POST /deviceOverView/generatedEnergy/total`
- `POST /deviceOverView/stateAttributeSummary/category/daily`
- `POST /deviceOverView/stateAttributeSummary/category/monthly`
- `POST /deviceOverView/stateAttributeSummary/category/yearly`
- `POST /deviceOverView/stateAttributeSummary/category/total`
- matching property-level `stateAttributeSummary/{daily|monthly|yearly|total}`.

Production/HAR evidence also includes:

- `POST /deviceOverView/generatedEnergy/daily`;
- category daily/monthly/yearly/total;
- `POST /deviceOverView/pvInverterPowerClass/daily/detail` from a fresh production Device details → Data Analysis capture.

A July 2026 live validation in an independent project confirmed that ordinary consumer-account tokens can access at least:

- `generationPower/daily`;
- `generatedEnergy/monthly`.

No installer/manufacturer role was needed for those tested reads.

### 2.2 Station level

`/stationOverView/*`

Documented family:

- `generationPower/daily`;
- `generatedEnergy/monthly`;
- `generatedEnergy/yearly`;
- `generatedEnergy/total`;
- property-level state summaries;
- category-level state summaries.

Production HAR also observed:

- category daily/monthly/yearly;
- `income/daily`;
- `income/monthly`;
- `income/yearly`;
- `income/total`.

This is the most useful aggregation level for a normal single-site dashboard because it naturally combines all devices belonging to one installation.

### 2.3 Owner/account level

`/ownerOverView/station/*`

Documented family provides:

- station generation power / generated energy at daily/monthly/yearly/total levels;
- property-level state summaries;
- category-level summaries.

These omit a station ID and aggregate the stations visible to the authenticated owner.

Useful if an account owns several stations.

Not necessary for a one-station dashboard.

### 2.4 Dashboard/global summary level

Observed production routes include:

- `POST /dashboard/summary/commons`;
- `POST /dashboard/summary/station/generatedEnergy/monthly`;
- station rankings/distribution.

These appear designed for the portal's portfolio/dashboard pages.

They are useful for multi-station overview, but are not preferable to station/device-specific endpoints when building a precise single-site history.

---

## 3. Two different aggregate concepts

The API exposes two broad statistical families that should not be mixed.

### 3.1 Generated-energy series

Examples:

- `generatedEnergy/monthly`;
- `generatedEnergy/yearly`;
- `generatedEnergy/total`.

These return simple time points whose value may appear as:

- `generatedEnergy`; or
- generic `value`.

A public client normalizes both into:

`TimePoint { time, timeDisplay, value, isRealValue }`.

### 3.2 State-attribute summary

Examples:

`stateAttributeSummary/category/{daily|monthly|yearly|total}`

Response shape is richer:

```text
category
properties[]
  property
    key
    name
    unit
  timePoints[]
    time
    timeDisplay
    value
    isRealValue
  hasRealTimePoints
hasRealTimePoints
```

This allows one category to return multiple energy/statistical properties together.

The category family is particularly useful because the response supplies:

- canonical property key;
- display name;
- unit;
- per-time-point reality flag.

---

## 4. Time-level semantics

The endpoint suffix does **not** mean the returned point itself always has the same name as the suffix.

Observed usage establishes the practical pattern.

### Category daily

Example:

`stateAttributeSummary/category/daily`

with:

`summaryCategoryKey = pvInverterPowerClass`

and body:

`{ time: "YYYY-MM-DD" }`

This is used for within-day **power/statistical detail**.

### Category monthly

Example:

`stateAttributeSummary/category/monthly`

with:

`summaryCategoryKey = pvInverterElectricityQuantityClass`

and body:

`{ time: "YYYY-MM" }`

returns **daily energy buckets within that month**.

A real stored example:

```text
property.key = pvGeneratedEnergy
time = 2026-08-01
value = 21.042
unit = kWh
isRealValue = true
```

Therefore this is the correct source for **daily generated-energy totals**.

### Category yearly

`stateAttributeSummary/category/yearly`

with body:

`{ time: "YYYY" }`

returns **monthly energy buckets within the year**.

A real dashboard uses this source for monthly PV-generation totals.

### Total

The `total` family represents longer/lifetime aggregation.

The separate `generatedEnergy/total` endpoint has been observed to return year-labelled points and is used by a real dashboard to calculate lifetime generated energy.

### Design implication

Never label stored data only “daily/monthly/yearly”.

Preserve:

- API source endpoint;
- requested period;
- returned `time` key;
- category/property;
- actual bucket granularity.

---

## 5. Important category keys

### `pvInverterPowerClass`

Used for power-oriented summaries such as daily intra-day behavior.

### `pvInverterElectricityQuantityClass`

Used for energy-quantity summaries.

Known properties include at least:

- `pvGeneratedEnergy`;
- `chargeElectricityQuantity`;
- `dischargeElectricityQuantity`;
- `consumeElectricityQuantity`;
- `buyElectricityQuantity`;
- `sellElectricityQuantity`.

Availability and trustworthiness are device/account specific.

### Core storage rule

Store every returned property dynamically using its:

- category key;
- property key;
- unit;
- display name;
- time key;
- value;
- `isRealValue`.

Do not define a fixed six-column summary schema and discard unfamiliar properties.

---

## 6. `isRealValue` is a critical quality flag

The aggregate APIs can return:

`isRealValue: true|false`

per time point.

Current first-party-derived documentation describes it as distinguishing:

- genuine device/server-backed data;
- backend-filled placeholder points.

A real collector found future aggregate buckets populated by the upstream service and explicitly refuses to interpret them as measured zero.

### Canonical rule

- `isRealValue === true` → strongest aggregate evidence.
- `isRealValue === false` → placeholder; **never treat it as a real zero/value**.
- `isRealValue == null` → unlabelled/unknown; preserve it, but do not silently upgrade it to the same confidence as explicit `true`.

The broader summary object can also contain:

`hasRealTimePoints`.

This should be preserved as summary-level quality metadata.

---

## 7. PV generation — strongest server aggregate

### Monthly server aggregate versus raw integration

Independent project validation for October 2025:

- raw ~5-minute time-series integrated with actual time deltas: **200.58 kWh**;
- server monthly PV aggregate: **201.06 kWh**.

The difference is only 0.48 kWh.

This is strong evidence that the server aggregate is a good PV-energy reference for that installation.

### Daily server aggregate versus cumulative telemetry counter

Another real installation:

- daily generation calculated as `MAX(totalPowerGeneration)-MIN(totalPowerGeneration)`: **6.131 kWh**;
- station summary: **5.581 kWh**;
- device snapshot: **5.581 kWh**.

Two independent cloud surfaces agreed against the locally calculated cumulative-counter delta.

That system therefore made the station summary authoritative for the displayed daily generation.

### Recommended dashboard precedence for PV energy

1. real-valued station/device server aggregate;
2. device/station snapshot daily total when it independently agrees;
3. integrated raw PV power as validation/fallback;
4. cumulative-counter delta only if specifically validated for that device.

Raw power remains the preferred source for power curves.

---

## 8. Consumption energy — aggregate property not universally trustworthy

The category vocabulary contains:

`consumeElectricityQuantity`.

However, one real installation found that the station-summary values were placeholders rather than useful consumption measurements.

Its dashboard instead uses:

`loadDayElectricityConsumption`

from raw/history telemetry and takes the day's maximum cumulative daily value.

### Consequence

Do **not** assume:

`consumeElectricityQuantity` = authoritative load consumption

for every inverter.

For the user's device, final validation should compare:

- category aggregate;
- daily cumulative telemetry counter;
- integration of load power if needed.

---

## 9. Grid import energy — same caution

The aggregate vocabulary includes:

`buyElectricityQuantity`.

On the same real installation it was placeholder/unusable.

The project instead uses:

`dayPurchaseElectricityConsumption`

from telemetry as the daily grid-import energy counter.

For lifetime grid import it uses:

`totalPurchaseElectricityConsumption`.

### Recommendation

Treat `buyElectricityQuantity` as a candidate until target-device validation.

Do not display its zero as “no grid import” solely because the aggregate endpoint returned zero.

---

## 10. Grid export energy

The category vocabulary includes:

`sellElectricityQuantity`.

No equally strong public target-system validation surfaced in this round.

Status:

**KNOWN PROPERTY / TARGET-DEVICE VALIDATION REQUIRED**

Potential cross-checks later:

- daily export telemetry counter if available;
- integrated signed grid-power history;
- Solar of Things UI;
- category aggregate with explicit `isRealValue=true`.

---

## 11. Battery charge/discharge energy

Known aggregate properties include:

- `chargeElectricityQuantity`;
- `dischargeElectricityQuantity`.

They are semantically attractive for dashboard statistics because integrating battery power/current from sparse samples can introduce error.

But this round did not find enough independent real-device comparison to label them universally authoritative.

Status:

**PROMISING SERVER AGGREGATES / VALIDATE ON TARGET DEVICE**

If they are explicit real values and match reasonable raw-history integration, they should be preferred for daily/monthly energy totals.

---

## 12. Daily cumulative telemetry counters as a second statistical layer

Some devices expose counters directly in ordinary telemetry, such as:

- `pvGeneratedEnergyOfDay`;
- `loadDayElectricityConsumption`;
- `dayPurchaseElectricityConsumption`;
- cumulative lifetime counters such as `totalPVGeneratedEnergy` or `totalPowerGeneration`.

These are neither raw instantaneous power nor server overview aggregates.

They are **device-reported counters**.

They can be extremely useful where an overview property is absent/placeholder.

### Safe interpretation

For a daily cumulative counter:

- use the last/max valid value in the station-local day;
- do not sum every 5-minute copy of the counter.

For lifetime counters:

- use latest value;
- do not assume `end-start` is a reliable daily total without validation.

The 6.131-versus-5.581 kWh example proves that cumulative-counter subtraction can disagree materially with server daily totals.

---

## 13. Power versus energy

Do not mix:

- instantaneous/average **power** (W/kW);
- integrated/accumulated **energy** (Wh/kWh).

### Best sources

For a power curve:

- timestamped raw history;
- possibly `generationPower/daily` or power-class daily detail where appropriate.

For daily/monthly energy totals:

- real-valued energy aggregate preferred when validated;
- device daily counter as fallback;
- integrate raw power only if necessary.

This avoids presenting a sampled power average as if it were the platform's authoritative energy meter.

---

## 14. Station versus device totals

For one station with one inverter, device and station totals may coincide.

That must not be assumed structurally.

A station can contain multiple devices.

### Recommended meaning

- **device aggregate** = statistic for one physical/cloud device;
- **station aggregate** = statistic for the installation/site;
- **owner aggregate** = statistic across stations visible to the account.

For a household/site dashboard, station-level energy totals are generally the natural top-level statistic.

Keep device-level data underneath for diagnostics and device-specific charts.

---

## 15. Owner-level aggregation

The API contains owner-level forms for:

- generation power;
- generated energy;
- property summaries;
- category summaries.

This is potentially useful if the user's account later contains several stations.

It is not necessary to implement initially for a single-station project.

### Rule

Never sum owner aggregate + station aggregates together.

They are alternate aggregation levels over overlapping underlying data.

---

## 16. Dashboard summary APIs

Production evidence includes:

- `/dashboard/summary/commons`;
- `/dashboard/summary/station/generatedEnergy/monthly`;
- station ranking/distribution routes.

These are likely optimized for the portal's high-level dashboard rather than precise per-installation archival ingestion.

They may be useful for:

- account-wide headline figures;
- multi-station comparisons.

They should not replace station/device detail endpoints for the core Windows collector.

---

## 17. Income/revenue endpoints

Production HAR observed:

- `/stationOverView/income/daily`;
- `/stationOverView/income/monthly`;
- `/stationOverView/income/yearly`;
- `/stationOverView/income/total`.

Station metadata includes:

- currency;
- energy-income price.

This strongly suggests server-calculated monetary income/value series.

However, this round did not establish:

- exact tariff formula;
- whether it represents export revenue, generation value or another configured notion;
- handling of changing tariffs;
- taxes or utility billing rules.

Status:

**AVAILABLE BUT SEMANTICS NOT VALIDATED**

Use only if a future dashboard requirement explicitly needs SiSeLi's own displayed income metric.

---

## 18. Report/export APIs

Production HAR exposes a separate asynchronous report/export subsystem.

### Device reports

Observed:

- `POST /device/report/daily`;
- `POST /device/report/monthly`;
- `POST /device/report/yearly`;
- header-definition GET endpoints;
- export-record list/details.

Bodies include fields such as:

- `id`;
- `fromTime`;
- `toTime`;
- `fieldNames`;
- `remark`.

### Station reports

Observed:

- `POST /station/report/monthly`;
- corresponding yearly/report-header/export-record endpoints.

### Alarm/SIM report systems also exist

These reinforce the pattern: “report” endpoints create/export server-side report artifacts.

### Dashboard relevance

They are **not the preferred ingestion API**.

Reasons:

1. direct time-series and aggregation APIs already expose structured JSON;
2. report generation creates server-side jobs/records despite being informational;
3. polling export status/download adds complexity;
4. report column sets may be presentation-oriented;
5. generated files are harder to process incrementally than JSON.

### Decision

**Optional / on-demand only.**

Potential future use:

- human-downloadable Excel/CSV compatibility;
- auditing against a vendor-generated report;
- discovering a statistic unavailable through JSON endpoints.

Do not use reports as the normal Windows collector path.

---

## 19. Proposed statistic-source precedence

For each dashboard statistic, select a source explicitly rather than using one universal priority.

### PV power curve

1. raw timestamped power telemetry.

### PV daily/monthly/yearly energy

1. server aggregate with explicit real value;
2. trustworthy device daily/snapshot counter;
3. raw-power integration.

### Load consumption

1. validated server aggregate if target device supplies real data;
2. daily cumulative telemetry counter;
3. raw load-power integration.

Current public evidence makes #2 preferable to #1 on at least one installation.

### Grid import

1. validated server aggregate if real;
2. daily cumulative grid-purchase counter;
3. signed raw grid-power integration.

Again, current public evidence favors #2 on one installation.

### Grid export

1. validated real aggregate;
2. device export counter if available;
3. signed grid-power integration.

### Battery charge/discharge energy

1. validated real aggregate;
2. direct energy counter if exposed;
3. integrate direct battery power;
4. integrate voltage × current only as a derived fallback.

### Lifetime generation

1. validated `generatedEnergy/total`;
2. current trustworthy lifetime counter;
3. sum validated lower-granularity server aggregates.

---

## 20. Cross-validation should be retained permanently

The Windows dashboard should store more than the displayed value.

For important energy totals, retain:

- chosen/displayed source;
- alternative available sources;
- discrepancy;
- quality flag;
- timestamp/bucket;
- `isRealValue`.

This makes silent API behavior changes detectable.

Example:

```text
metric: pv_generated_energy
period: 2025-10
chosen: station aggregate = 201.06 kWh
raw integration: 200.58 kWh
difference: 0.48 kWh
quality: server_real_value
```

A future firmware/backend change can then be detected rather than silently rewriting historical meaning.

---

## 21. Placeholder handling

The correct model is not:

`missing/placeholder → 0`

It is:

- measured zero;
- placeholder;
- missing;
- unlabelled;
- real nonzero

as distinct states.

### Why this matters

If a future date bucket is returned as value 0 + `isRealValue=false`, displaying it as “0 kWh” implies that the date occurred and the system produced nothing.

That is false.

The UI should display:

- no value;
- future/not available;
- or omit the bucket.

---

## 22. Aggregate storage model

Recommended generic shape:

```text
scope_type       device | station | owner | dashboard
scope_id
source_endpoint
aggregation_request   daily | monthly | yearly | total
category_key
property_key
bucket_key
bucket_start
value
unit
is_real_value
has_real_time_points
retrieved_at
raw_json
```

This design handles new summary categories/properties without schema migration.

---

## 23. Aggregate update behavior

Current-period aggregates can change as new telemetry arrives.

Therefore:

- today's daily energy is mutable until the local day closes;
- current month's daily buckets may be revised;
- current year's monthly buckets may be revised.

Historic closed buckets should still be rechecked occasionally during initial validation because the cloud may correct late uploads.

### Practical recommendation

For eventual collector design:

- refresh current day frequently enough for the UI;
- refresh recent closed days with a small lag window;
- upsert all aggregate buckets;
- do not treat first-seen value as immutable.

Exact cadence belongs to the next polling/freshness round.

---

## 24. Server aggregate vs local integration decision

### Prefer server aggregate when

- the point is marked real;
- the semantic property is verified for this device/station;
- it agrees reasonably with another trustworthy source;
- the desired statistic matches the server bucket exactly.

### Prefer local/raw calculation when

- server property is placeholder;
- no server aggregate exists;
- a custom sub-day/custom-date-range statistic is required;
- the dashboard needs a metric the vendor does not aggregate.

### Keep both when practical

Raw telemetry and server aggregates answer different questions:

- raw = what the device reported over time;
- aggregate = what SiSeLi considers the official period total.

A good analytical dashboard benefits from having both.

---

## 25. Confidence matrix by currently known metric

### High confidence

- PV generated energy from validated real server aggregate.
- `isRealValue=false` means the point must not be treated as a real measurement.
- category-monthly provides daily buckets for the selected month.
- category-yearly provides monthly buckets for the selected year.
- total/generated-total provides long-term/year buckets.
- station/device/owner are distinct aggregation levels.

### Medium / target-dependent

- battery charge energy aggregate.
- battery discharge energy aggregate.
- grid export aggregate.
- server income series.
- device-level versus station-level equality in a one-inverter installation.

### Low until target validation

- `consumeElectricityQuantity` as universal consumption total.
- `buyElectricityQuantity` as universal grid-import total.
- cumulative-lifetime counter subtraction as a daily-energy calculation.
- report-generated files as a primary ingestion source.

---

## 26. Read-only/safety classification

The direct aggregate endpoints are passive cloud reads even though most use HTTP POST.

They do not imply inverter/device mutation.

Report-generation endpoints are different:

- they do not change inverter configuration;
- but they may create server-side report/export records/jobs.

For the project's normal read-only collector, avoid report creation unless explicitly needed.

---

## 27. Sources examined

Primary evidence:

- first-party Swagger-derived contract preserved by `lujian1324-spec/energy-app`;
- fresh production endpoint observations in that project;
- `vvkor/python-siseli` aggregate models and HAR-derived API catalog;
- `Hyllesen/solar-of-things-solar-usage` real raw-vs-monthly validation;
- `dvgamerr-app/aide-collector` station summary collection and persistence;
- `dvgamerr-app/home-assistant` real database/source-selection decisions after backfill.

Current direct official public-web search did not surface separate vendor documentation describing these aggregate semantics beyond the already-preserved first-party API material.

---

## 28. Round-close conclusion

**Round 11 is complete.**

The Windows dashboard should not recalculate every energy statistic from raw five-minute power.

The strongest architecture is hybrid:

- **raw history** for curves, custom intervals and forensic validation;
- **validated SiSeLi aggregates** for canonical daily/monthly/yearly energy totals;
- **device cumulative counters** as targeted fallback where server summary properties are placeholders.

PV generated energy is already strong enough to prefer the server aggregate in the generic design.

Consumption, grid import/export and battery energy must be selected per target device after controlled validation.

Report/export jobs are not required for the dashboard ingestion path.

---

## 29. Next-round decision

Proceed separately to:

**Round 12 — Errors, Polling Frequency, Freshness and Reliability**

It should determine:

- API/business error taxonomy relevant to read-only collection;
- token expiry/refresh failure behavior;
- recommended polling interval by data class;
- actual cloud reporting lag/freshness;
- late-arriving frames and overlap size;
- retry/backoff policy;
- rate-limit evidence;
- timeout/5xx behavior;
- partial history page failures;
- stale/offline detection;
- how often current aggregates versus closed aggregates should be refreshed;
- whether concurrent requests or refresh races create reliability problems.

Keep the Cloud-UI coverage audit as the following separate round.

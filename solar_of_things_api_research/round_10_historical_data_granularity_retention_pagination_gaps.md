# Round 10 — Historical Data Granularity, Retention, Pagination and Gaps

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_10_history_behavior_matrix.md`

## Scope

This round investigates only Solar of Things / SiSeLi **cloud historical data** for the future Windows dashboard/statistics project.

Questions:

- how often raw telemetry is recorded;
- how far back it can be recovered;
- which historical endpoints exist and how they differ;
- page-size and pagination behavior;
- whether wide windows truncate or lose data;
- how nulls, missing frames and offline periods appear;
- timestamp/time-zone/day-boundary behavior;
- practical backfill strategy.

Out of scope:

- MQTT interception;
- BLE;
- local serial/Modbus;
- Android-local behavior;
- provisioning;
- configuration/control/firmware mutation.

No authenticated request was made against the user's account.

---

## 1. Executive conclusion

The Solar of Things cloud can support a serious historical dashboard.

The strongest current model is:

1. raw inverter telemetry is commonly reported around every **5 minutes**, but cadence is not exact and is device/network dependent;
2. raw history is paginated and can be backfilled day-by-day;
3. a page can contain up to **2,000** records according to the first-party-derived contract, while the current web console uses **1,500** for its selected-key day query;
4. the early claim that >4–7 day requests “lose data” came from a client that always requested **page 1 only**, so it must not be interpreted as proof that the server irreversibly downsamples long windows;
5. newer collectors successfully paginate historical endpoints and backfill one day at a time;
6. one real collector recovered raw telemetry on 2026-08-24 back to **2026-05-11**, proving at least ~105 days of raw-history retention for that installation at that time;
7. no reliable public evidence establishes the platform's absolute maximum raw-history retention period;
8. null/missing values are real gaps, not zeroes;
9. timestamps are UTC instants in responses, but query-day boundaries must be expressed using the station/local time zone with explicit offset plus the `IOT-Time-Zone` header.

For a Windows collector, the safest policy is:

> **backfill in station-local one-day windows, paginate every page, upsert by device + attribute + timestamp, and preserve missing data as missing.**

---

## 2. Historical API families

There are three materially different raw-history representations.

### 2.1 Selected-key columnar history — preferred

Current production web route:

`POST /deviceState/simple/attribute/keys/history/v1`

Request supports:

- `deviceId`;
- `keys[]`;
- `fromTime`;
- `toTime`;
- `page`;
- `count`;
- `orderByTimeAsc`.

Current production console behavior:

- `count = 1500`;
- one local calendar day;
- explicit UTC offset in from/to;
- `IOT-Time-Zone` header;
- multiple keys in one call.

Response:

```text
data.page
data.count
data.total
data.payload.timeSeries[]
data.payload.fields.<key>[]
data.payload.formatters
data.payload.fieldInfo
```

Every requested field array aligns by index with the shared `timeSeries`.

This is the best default history source when the dashboard knows which raw keys it wants.

### 2.2 Simple record-list v1 — broader columnar history

Observed route:

`POST /deviceState/simple/attribute/record/list/v1`

A real collector uses the same pagination machinery as selected-key history but does not supply a `keys[]` list.

Its payload is also columnar, but each field element can carry richer per-value objects such as:

- raw value;
- display value;
- unit;
- name.

This makes it useful for:

- schema discovery;
- preserving nonnumeric/text state;
- detecting fields the selected-key query did not ask for.

A real collector deliberately ingests **both** this source and selected-key history, then deduplicates by:

`(device_id, attr, recorded_at)`.

That is evidence that the two sources overlap substantially but are not assumed identical.

### 2.3 Row-oriented record list — compatibility fallback

Observed route:

`POST /deviceState/attribute/record/list`

A current downstream client identifies this as the Siseli app's `doGetDeviceHistory` route.

It returns one record/frame at a time:

```text
record.time
record.deviceId
record.dtuID
record.fields.<key>.value
...
```

The reference-client behavior uses:

- `count = 80`;
- normal page numbers;
- one record per report frame.

This is a useful fallback if the current selected-key v1 endpoint is refused for a device/session.

### Recommendation

Use:

1. selected-key v1 for normal dashboard metrics;
2. simple record-list v1 during backfill/discovery if broader raw preservation is desired;
3. non-simple record/list only as a compatibility fallback.

Do not query all three continuously merely because they exist.

---

## 3. Actual telemetry cadence

### Large real-world corpus

A read-only collector/database analysis performed in August 2026 measured:

- **26,417 distinct source timestamps**;
- from 2026-05-11 through 2026-08-17;
- around 24 attributes;
- approximately 23 attributes arriving together in a typical current frame.

For the most recent 14 days:

- **median interval: 301 seconds**;
- **90th percentile: 360 seconds**;
- a full day usually contained **269–278 source timestamps**.

This is strong empirical evidence that one important device family reports at approximately five-minute cadence, with ordinary timing jitter and missing frames.

A mathematically perfect five-minute day would contain 288 intervals/frames depending boundary treatment; 269–278 observed frames means a dashboard should expect gaps rather than assume a perfect 288-point grid.

### Other corroboration

Independent clients and user reports also describe approximately 3–5 minute cloud updates.

A current Home Assistant integration deliberately polls on a five-minute schedule.

### Important limitation

Five minutes is **not a universal protocol guarantee**.

A fresh production capture from another device contained only 47 report frames from roughly 16:07 to 23:44 local time, showing that:

- device operating state;
- battery/BMS availability;
- connectivity;
- firmware/reporting policy

can produce much sparser histories.

### Design rule

Never fabricate a fixed five-minute row when the cloud did not report one.

Store actual timestamps.

For graphing, an application may bucket later, but raw persistence should preserve the irregular source cadence.

---

## 4. The “2,000 sample limit” — corrected interpretation

First-party-derived API documentation describes common pagination with:

- `page` starting at 1;
- `count` up to **2000**.

An early independent history tool used:

```json
{
  "count": 2000,
  "page": 1
}
```

for every request.

That client observed:

- roughly 288 expected five-minute samples per day;
- four-day windows around 1,152 samples;
- larger windows losing substantial data;
- seven-or-more-day queries performing badly enough that it standardized on four-day chunks.

Its README describes a 2000-sample limit and “downsampling/data loss.”

### Critical correction

The source code confirms that the client **never requested page 2**.

Therefore its experiment proves:

> one unpaginated 2000-row page is insufficient for some long windows.

It does **not** prove:

> the server permanently downsamples all long date ranges even when correctly paginated.

Later collectors explicitly paginate.

### Current best conclusion

Treat **2000 as a maximum/request page-size boundary**, not as a maximum recoverable history-window size.

For robustness, do not depend on giant date windows even with pagination. Daily windows are easier to validate, retry and reconcile.

---

## 5. Current production page size

The current Solar of Things web console was captured requesting:

`count = 1500`

for a complete local day of selected-key history.

The captured day returned:

- `data.page = 1`;
- `data.count = 47`;
- `data.total = 1`;
- 47 shared timestamps.

The exact semantic meaning of `data.total` was not encoded in official prose, but current implementations treat it as **total pages** for this columnar endpoint.

Because that capture had one short page, it does not independently prove the multi-page semantics.

### Safe pagination stop rule

Use both signals:

1. stop when returned timestamps/records are fewer than requested page size;
2. if `total` behaves as a positive page count, stop when `page >= total`.

Also impose a high safety page cap so a malformed API response cannot loop forever.

---

## 6. Demonstrated pagination

A later real collector uses:

- page size **300**;
- pages 1 through a safety maximum of 500;
- both simple record-list v1 and selected-key history v1.

For each page it counts:

`payload.timeSeries.length`

and continues until the page/total or short-page condition indicates completion.

This implementation successfully performed a large historical backfill.

Another current client uses:

- selected-key v1: 1,500/page, up to 10 pages;
- row-oriented fallback: 80/page, up to 60 pages.

These independent implementations establish that **pagination is a practical supported behavior**, not merely a field present in documentation.

---

## 7. Retention — what is actually known

### Strong lower bound from real backfill

On **2026-08-24**, one collector's initial backfill recovered raw record/key telemetry beginning:

`2026-05-11T10:51:36Z`

That is data roughly **105 days old at retrieval time**.

Therefore:

> the cloud retained at least ~105 days of raw telemetry for that account/device.

The same corpus contained 26,417 source timestamps through August 17.

### Supporting older evidence

A separate tool committed on 2026-01-31 contains real-world analyses for September, October and November 2025 and describes validating time-series results against monthly cloud summaries.

This is consistent with several months of recoverable historical data, but the exact dates on which each historical query was executed are not preserved well enough to turn that source into a stricter retention guarantee.

### Official/public product description

Solar of Things documentation advertises long-term/historical storage and daily/monthly/yearly historical views, but no precise retention duration surfaced.

### What is NOT known

There is no credible evidence yet for a universal rule such as:

- 90 days;
- 6 months;
- 1 year;
- forever.

Retention may also vary by:

- device;
- account/role;
- backend migration;
- time period;
- raw telemetry versus summaries.

### Windows-project implication

At first installation, the collector should **discover the oldest recoverable raw date empirically**, rather than assume a retention period.

A binary/date search can be used later during controlled validation, followed by daily backfill from the first available date.

---

## 8. Raw-history retention differs from aggregate-history availability

One real backfill found:

- raw telemetry available starting **May 11**;
- valid station daily summary buckets beginning only **May 26**.

This proves that different history products do not necessarily begin on the same date.

Conversely, monthly/yearly/total summaries may remain useful where raw telemetry has eventually aged out.

Therefore the dashboard should keep distinct notions of:

- raw telemetry retention;
- station/device daily summary history;
- monthly summary history;
- yearly/total summary history.

Do not use the oldest date from one source as the retention rule for another.

---

## 9. Missing values and missing frames

### Null field in an existing frame

Selected-key history uses a shared time axis.

If:

`fields.someKey[i] = null`

then the device produced a report frame at:

`timeSeries[i]`

but that specific attribute was absent.

That means:

**null ≠ zero.**

A current production capture showed frames continuing for hours after battery SOC stopped appearing. Converting those null SOC values to 0% would invent a battery depletion event that did not occur.

### No frame at all

If there is a long gap between timestamps, there was no cloud frame in that interval.

Possible causes include:

- inverter/logger offline;
- network interruption;
- reporting suppression;
- server ingestion gap.

Again:

**no frame ≠ zero power.**

### No historical rows before first available date

A real collector explicitly displays “No data” before upstream history begins rather than manufacturing zero rows.

That is the correct rule for our future dashboard.

---

## 10. Gaps and graph continuity

A downstream current client uses a useful graphing heuristic:

1. calculate the median positive source-timestamp gap;
2. define maximum drawable continuity as roughly three times that median;
3. clamp that threshold between 15 and 60 minutes;
4. split the line whenever:
   - the metric is null; or
   - the timestamp silence exceeds the threshold.

This is not a server rule, but it is a sound presentation principle.

For a nominal five-minute device, blindly drawing a straight line through a two-hour outage would imply measurements the cloud never produced.

---

## 11. Duplicate / overlapping data

Multiple API sources can produce the same:

`device + raw attribute + timestamp`

A real collector combines:

- key history;
- record history;
- latest state;
- energy-flow state

and deduplicates on:

`(device_id, attr, recorded_at)`.

Its database primary key uses the same grain, making backfill idempotent.

### Windows-project recommendation

Use a uniqueness key at least equivalent to:

`device_id + raw_attribute + recorded_at`

and either:

- preserve source provenance separately; or
- establish source-precedence rules if only one value can survive.

For historical backfill, upsert rather than append blindly.

---

## 12. Late uploads / overlapping refreshes

A current history client rereads the final **10 minutes** of an already-loaded live window when refreshing.

Its rationale is to catch late uploads.

This implies an important collection pattern:

> do not always resume exactly after the most recently stored timestamp.

Use a small overlap window and idempotent upserts.

For a five-minute-reporting device, 10–15 minutes is a reasonable starting overlap to validate later against the user's device.

---

## 13. Selected-key vs record history — measured overlap

One real collector's ordinary three-hour refresh example returned approximately:

- selected-key history: **131 frames**;
- record history: **130 frames**.

This is useful evidence:

- they are highly overlapping;
- they are not guaranteed frame-for-frame identical;
- each may preserve data the other lacks.

The collector merges both.

### Project implication

For the eventual dashboard:

- normal ongoing collection may use one preferred source;
- initial research/live validation should compare both on the user's device;
- if selected-key history proves complete for needed metrics, there is no need to permanently double API traffic.

---

## 14. Record history versus selected-key history — structural difference

### Selected-key history

Advantages:

- efficient when only a few metrics matter;
- current web-console source;
- multiple requested keys share one timestamp array;
- large page size;
- good for charts.

Disadvantages:

- only contains requested keys;
- primitive arrays often lack unit/name metadata in the historical response itself.

### Simple record-list v1

Advantages:

- broader/raw field coverage;
- richer per-field metadata can be preserved;
- useful for discovery/backfill.

Disadvantages:

- potentially much larger storage/traffic footprint.

### Row-oriented record/list fallback

Advantages:

- another compatible route;
- one record has its field dictionary together;
- known app/reference-client behavior.

Disadvantages:

- small observed page size;
- less efficient for long history;
- older/fallback path rather than preferred current web route.

---

## 15. Time-zone and day-boundary behavior

Current production history queries use:

- `IOT-Time-Zone: <IANA timezone>`;
- `fromTime` and `toTime` in local time;
- explicit numeric UTC offset.

Example shape:

```text
2026-09-24T00:00:00+08:00
2026-09-24T23:59:59+08:00
```

Returned `timeSeries` entries are UTC ISO instants.

### Confirmed failure mode

A client formatter once omitted the minus sign for western UTC offsets.

That caused:

`20101 Illegal argument`

and empty history for affected users.

### Correct rule

Historical day boundaries must be generated from the **station's timezone**, not by naïvely appending the Windows machine's current offset.

The application should retain:

- station IANA timezone;
- UTC timestamp;
- station-local calendar date/time.

### DST implication

For zones with daylight-saving changes, a local calendar day can map to 23, 24 or 25 UTC hours.

Therefore do not assume:

`one local day = exactly 86,400 UTC seconds`

when constructing backfill boundaries.

Use a timezone-aware library to generate each local day's start/end.

---

## 16. Backfill strategy demonstrated in practice

A real collector uses exactly the strategy we should prefer conceptually:

1. start from today's local date;
2. for each day back to a target date:
   - query record history for that local day;
   - query selected-key history for that local day;
   - query the day's station summary;
3. upsert/deduplicate;
4. pause between days;
5. separately retrieve monthly/yearly/total aggregates.

This is robust because a failure affects one day rather than a giant multi-month response.

### Important improvement for our future Windows collector

We probably will **not** need both raw-history APIs forever.

During initial live validation:

- compare selected-key and record history;
- identify which one is sufficient for the required dashboard metrics;
- then minimize ongoing API traffic.

---

## 17. Practical initial-backfill algorithm

Recommended research-derived design:

### Step 1 — determine station timezone

Read from station/device metadata.

### Step 2 — discover oldest available raw date

Do not brute-force every day from many years ago.

Use an expanding/binary search pattern:

- today;
- 7 days ago;
- 30 days ago;
- 90 days ago;
- 180 days ago;
- etc., until empty;
- binary search between last nonempty and first empty date.

This should be validated against the actual account because “empty day” can also mean the system was genuinely offline.

Device installation/created dates can provide a useful lower bound.

### Step 3 — backfill daily

For each local calendar day:

- query selected-key history with pagination;
- optionally record-list history during the discovery phase;
- validate page completeness;
- save raw timestamps exactly;
- upsert idempotently.

### Step 4 — verify daily completeness

Record:

- number of frames;
- first timestamp;
- last timestamp;
- median gap;
- p90 gap;
- largest gap;
- requested pages;
- successfully received pages.

Do not require 288 points as a pass condition.

### Step 5 — retry holes cautiously

A day with unusually low frame count can be re-requested once.

If the same holes remain, preserve them as upstream gaps rather than invent values.

---

## 18. Ongoing collection strategy

Once initial backfill is complete:

### Raw historical polling

A trailing history window is safer than relying only on “latest state”.

A real collector uses three hours by default; another live client rereads a 10-minute overlapping tail.

For our eventual dashboard, the exact interval should be decided in the later polling/reliability round.

### Minimum integrity behavior

Every collection cycle should:

- overlap the newest stored timestamp by a small margin;
- deduplicate/upsert;
- preserve source timestamps;
- never overwrite a known real value with an absent/null frame without a deliberate precedence rule.

---

## 19. Partial-page failure behavior

If page 1 succeeds and page 2 fails, the application has **partial history**, not a complete period.

A current downstream project explicitly warns:

“Some history for this period couldn't be loaded, so totals may be low.”

That is the correct semantic behavior.

### Rule

A history request result needs a completeness state:

- complete;
- partial;
- unavailable.

Do not calculate a period total from partial data and present it as authoritative without a warning/provenance flag.

---

## 20. Energy integration from raw power history

An older client uses trapezoidal integration:

`Σ ((P[i] + P[i+1]) / 2 × Δt) / 1000`

to estimate kWh from irregular W samples.

The method correctly uses the **actual timestamp interval**, not a fixed five minutes.

That is superior to:

`sum(power) × 5min`

when intervals vary.

### However

The later server-aggregation investigation must determine when server-computed energy totals are preferable.

Raw integration can be biased by:

- long missing intervals;
- inverter reporting behavior;
- interpolation assumptions;
- mixed raw/derived power metrics.

A real dashboard project found that a daily total derived from a cumulative telemetry counter differed from station/device summary by a material amount.

Therefore Round 10 does **not** declare raw integration the authoritative energy-total source.

---

## 21. Data volume implication

One real backfill produced approximately:

- 5.44 million attribute rows;
- from ~26,417 source timestamps;
- across ~24 attributes;
- about 1.4 GB including database indexes.

This is manageable on a normal Windows machine/database, but preserving every raw attribute creates much more data than storing only dashboard metrics.

### Approximate order of magnitude

At ~275 frames/day:

- 1 device-year ≈ 100,000 source frames;
- 20 attributes ≈ 2 million attribute-value rows/year;
- 30 attributes ≈ 3 million rows/year.

This argues for:

- keeping raw data if future flexibility matters;
- indexing by device/time;
- using aggregated/bucketed views for charts;
- not loading months of raw EAV rows directly into the UI.

The later project design can decide whether all attributes or a selected subset deserve permanent local storage.

---

## 22. Retention status matrix

| Historical source | Backfillable? | Proven history depth | Notes |
|---|---|---|---|
| Selected-key raw history | Yes | at least ~105 days in one 2026 installation | Current web-preferred route |
| Simple record-list v1 | Yes | same daily backfill corpus demonstrates multi-month use | Broader field representation |
| Row-oriented record/list | Yes | no independent maximum established | Compatibility fallback |
| Alarm history | Yes via pagination | not measured in this round | Separate alarm query |
| Latest state | No historical API known | current only | Must snapshot locally going forward |
| Energy flow | No historical endpoint established for its full structured view | current only | Raw constituent fields may exist in history |
| Device details | No historical API known | current only | Snapshot locally if changes matter |
| Config cache | No historical API known | current only | Snapshot locally if changes matter |
| Station daily summaries | Yes | in one corpus began later than raw telemetry | `isRealValue` matters |
| Monthly/yearly/total summaries | Yes | multiple historical periods demonstrated | Separate aggregation round next |

---

## 23. What remains genuinely unknown

1. Absolute maximum raw-telemetry retention.
2. Whether retention is identical for all accounts/device families.
3. Whether old raw data is eventually downsampled server-side.
4. Whether a very large date window is fully recoverable by pagination alone or should always be date-chunked.
5. Exact endpoint-specific semantics of `data.total` under multiple pages.
6. Whether selected-key and simple record-list histories have systematically different retention.
7. Whether some firmware batches upload late enough that a 10-minute overlap is insufficient.
8. Exact behavior during long cloud outages.
9. Whether repeated querying of old history can return corrected/backfilled server data later.
10. Exact raw-history start date for the user's inverter/account.

These are ideal candidates for final controlled read-only validation rather than more speculative public archaeology.

---

## 24. Recommended Windows historical-storage rules

The future implementation should encode these rules from day one:

1. IDs as strings.
2. UTC timestamps stored losslessly.
3. Station timezone stored separately.
4. Raw timestamps, never synthetic 5-minute timestamps.
5. Null = missing, zero = measured zero.
6. Unique/upsert key based on device + raw field + timestamp.
7. Preserve source endpoint.
8. Track backfill completeness per day/source.
9. Track page failures.
10. Use daily local-time backfill windows.
11. Add an overlap window during ongoing refresh.
12. Calculate integrations using actual elapsed time.
13. Never bridge long graph gaps visually.
14. Never report a partial-period calculated total as complete.

---

## 25. Sources examined

Primary technical evidence:

- `Hyllesen/solar-of-things-solar-usage`
  - initial commit 2026-01-31;
  - selected-key history;
  - 2000-row page;
  - page fixed at 1;
  - 4-day chunking;
  - real Sep–Nov 2025 energy analyses.

- `dvgamerr-app/aide-collector`
  - paginated key + record history;
  - page size 300;
  - daily idempotent bulk backfill;
  - timezone-bounded days;
  - current-only source distinction;
  - dedupe/upsert model.

- `dvgamerr-app/home-assistant`
  - measured 26,417 source timestamps;
  - May–August 2026 retained history;
  - 301-second median / 360-second p90 cadence;
  - 269–278 full-day frames.

- `lujian1324-spec/energy-app`
  - fresh current production history capture;
  - 1500-page console behavior;
  - selected-key pagination;
  - row-record fallback;
  - null-gap behavior;
  - timezone-offset bug evidence;
  - late-tail reread behavior.

- first-party-derived Swagger/OpenAPI contract preserved in prior rounds.

Public product/user documentation additionally confirms that Solar of Things exposes historical daily/monthly/yearly data. Exact retention is not published in the material found.

---

## 26. Round-close assessment

**Round 10 is complete.**

The cloud history path is viable for a Windows statistics dashboard.

The most important correction to earlier assumptions is:

> **2,000 is a page-size boundary, not evidence of a 2,000-point total-history ceiling.**

The best architecture is daily, timezone-correct, paginated, idempotent backfill.

The platform has demonstrated at least several months of retained raw history, but the universal maximum retention remains unknown and should be measured against the user's actual account in the later read-only validation round.

### Next surviving investigation

Proceed separately to:

**Round 11 — Server-Side Statistics, Aggregations and Reports**

Questions:

- which daily/monthly/yearly values are calculated by SiSeLi;
- device vs station vs owner aggregation levels;
- which properties are trustworthy;
- `isRealValue` placeholder semantics;
- whether server energy totals are preferable to integrating raw power;
- daily/monthly/yearly/total bucket definitions;
- grid import/export, load, battery charge/discharge and PV generation coverage;
- report/export endpoints relevant to data retrieval;
- discrepancies between server aggregates and raw telemetry-derived totals.

Keep polling/freshness/reliability as the separate following round.

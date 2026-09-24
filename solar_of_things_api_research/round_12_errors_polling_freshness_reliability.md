# Round 12 — Errors, Polling Frequency, Freshness and Reliability

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_12_reliability_polling_matrix.md`

## Scope

This round investigates the operational reliability of the Solar of Things / SiSeLi **cloud read path** for the future Windows dashboard.

Questions:

- what errors matter for read-only collection;
- token expiry and refresh behavior;
- appropriate polling cadence by data class;
- cloud reporting freshness;
- late-arriving frames and overlap windows;
- retry/backoff policy;
- rate-limit evidence;
- timeout and 5xx behavior;
- partial-page handling;
- stale/offline detection;
- refresh policy for current versus closed aggregates;
- concurrency and token-refresh races.

Out of scope remains all local/device communication and all control/mutation research.

No authenticated request was made against the user's own account.

---

## 1. Executive conclusion

The cloud is suitable for a durable Windows collector if the client is designed as a **polling/cache system**, not as a realtime socket.

The important operational facts are:

1. device cloud telemetry commonly advances around every **5 minutes**, with jitter and gaps;
2. multiple mature integrations therefore use 5-minute archival/device polling;
3. more interactive clients sometimes check the cloud every **60 seconds**, but they usually observe the same source timestamp until a new device frame reaches SiSeLi;
4. no credible public source establishes a vendor rate-limit quota or a safe requests-per-minute number;
5. no confirmed production push/WebSocket path exists for the needed telemetry;
6. refresh tokens rotate and should be treated as single-use session-chain credentials;
7. concurrent refresh attempts must be serialized;
8. read retries are safe only for transient transport/5xx failures, not arbitrary business errors;
9. stale data must be detected from the **source timestamp / lastDataAt**, not from the time the Windows app last performed a successful HTTP request;
10. history polling should overlap recent stored data and upsert, because late frames can arrive after an earlier read.

A robust collector can therefore be conservative without losing useful resolution.

---

## 2. Three different notions of “fresh”

The future dashboard must distinguish:

### 2.1 HTTP freshness

“When did our Windows program last successfully call SiSeLi?”

This only proves the cloud server answered.

It does **not** prove the inverter supplied a new measurement.

### 2.2 Cloud source freshness

“When was the newest device frame timestamp returned by SiSeLi?”

This is the important freshness measure for telemetry.

Examples:

- `deviceAttributeState.time`;
- newest `timeSeries[]` timestamp;
- device `lastDataAt`.

### 2.3 Local display freshness

“When did our application last update the screen/cache?”

This may be much newer than the actual device data.

### Canonical rule

Every displayed live metric should retain at least:

- source timestamp;
- collection/retrieval timestamp.

Never stamp an old cloud value with “now” and present it as a fresh measurement.

---

## 3. Observed reporting cadence

Round 10 established a large real-world dataset with:

- median source interval **301 seconds**;
- p90 **360 seconds**;
- about 269–278 source frames per full day.

That is strong evidence for a nominal ~5-minute cloud-report cadence on that device family.

Other public integrations independently use:

- Home Assistant device update: **5 minutes**;
- independent scraper/pusher: **5 minutes**;
- a Windows-oriented Siseli client: API data fetch every **1 minute**;
- a current mobile/web history view: tail re-read every **60 seconds** while visible.

### Interpretation

The one-minute clients are checking for a newly arrived cloud sample.

They are not evidence that the physical inverter generates a unique new cloud sample every minute.

### Dashboard implication

A one-minute HTTP poll may reduce **detection latency** after a five-minute frame arrives, but cannot create higher underlying measurement resolution.

---

## 4. Recommended polling classes

These are engineering recommendations derived from observed source cadence and working public clients. They are **not vendor-published limits**.

### 4.1 Archival raw telemetry

Recommended baseline:

**every 5 minutes**

Request a trailing history window rather than only a “latest” field.

Suggested window:

**last 15 minutes**

then upsert/deduplicate.

Why:

- matches nominal source cadence;
- captures delayed frames;
- tolerates one missed collection cycle;
- does not require fabricating timestamps.

A three-hour trailing window, as used by one real collector, is even more resilient but creates substantially more repeat transfer. It is useful for infrequent scheduled collectors rather than a continuously running desktop process.

### 4.2 Interactive “live” dashboard

If the dashboard is visible and the user expects timely updates:

**60–120 seconds**

is reasonable for a lightweight latest/history-tail request.

Do not label this “1-minute inverter data”.

It is “check every minute for the newest ~5-minute cloud frame”.

### 4.3 Background live dashboard

If no live page is visible:

**5 minutes**

is sufficient for most telemetry/statistics acquisition.

### 4.4 Device/station list and metadata

Recommended:

- startup;
- manual refresh;
- every **15–30 minutes** if needed;
- immediately after an operation that is expected to alter cloud topology, though such operations are out of project scope.

Device identity/model/protocol metadata changes rarely.

### 4.5 Attribute/schema catalog

Recommended:

- initial discovery;
- software/firmware/model change;
- perhaps once daily as a low-cost safety refresh.

No need to query it every telemetry cycle.

### 4.6 Alarms

If timely alarms matter to the dashboard:

**1–5 minutes**.

A separate public server-side poller uses one minute for notification detection because no confirmed vendor push channel was available to it.

If the dashboard only displays an alarm history page, 5 minutes is enough.

### 4.7 Current-day aggregates

Recommended:

**5 minutes** while the current local day is open.

Server totals can change as device frames arrive.

### 4.8 Closed daily buckets

Recommended:

- re-read shortly after local midnight;
- re-read recent closed days later to catch delayed corrections;
- after settling, cache for hours/day rather than every poll.

A practical starting policy:

- yesterday: every 30–60 minutes for several hours after midnight;
- previous 2–7 days: once every 6–24 hours;
- older closed days: only on backfill/audit/manual repair.

Exact late-correction behavior should be measured in final account validation.

### 4.9 Monthly/yearly totals

Recommended:

- current month: every 15–30 minutes or after new daily aggregate;
- previous month: recheck for several days after month close, then cache;
- historical years: on startup/backfill or occasional audit only.

---

## 5. Trailing overlap and late-arriving frames

A current client re-reads the last **10 minutes** of a live history window.

Round 10 established that upserts make overlap cheap and safe.

### Recommended starting overlap

For nominal ~5-minute devices:

**15 minutes**

This covers roughly three expected report intervals.

Use:

`[newest_stored_source_time - 15 min, now]`

then deduplicate by:

`device_id + raw_attribute + source_timestamp`.

### Why overlap is better than “start exactly after last timestamp”

A device frame may:

- be delayed in the inverter/logger;
- arrive late at the cloud;
- become queryable after the collector already passed that timestamp range.

An exact cursor risks permanently missing it.

---

## 6. Token lifecycle

Login returns:

- access token;
- refresh token;
- access expiry metadata;
- refresh expiry metadata.

The best current production evidence says the refresh operation uses:

`POST /login/refresh/access/token`

with the **current access + refresh token pair**.

It returns a replacement token pair.

### Important behavior

Current projects with direct live experience report that the refresh token is effectively **single-use/rotating**:

- refresh succeeds;
- old pair should be considered invalid;
- new access + refresh pair must be stored atomically.

### Session-chain consequence

Two applications/processes must not independently share and rotate the same refresh-token chain.

If two Windows processes need independent access, each should establish or be given its own independent authenticated session rather than racing one pair.

### Known approximate access-token lifetime

A current independent cloud client uses the server field:

`accessTokenWillExpiredInMillis`

and falls back to approximately **2 hours** if the field is missing.

Another historical integration previously refreshed tokens externally about every 110 minutes.

This supports a ~2-hour observed access-token lifetime, but the **server-returned expiry is authoritative**.

Never hard-code two hours as the protocol guarantee.

---

## 7. Proactive refresh

The official-web-derived Home Assistant client refreshes around **5 minutes before expiry**, mirroring behavior it attributes to portal JS.

Another production client refreshes one minute before expiry.

### Recommendation

Use server expiry metadata and refresh:

**2–5 minutes before access-token expiry**.

The exact lead is not important compared with these rules:

1. use server expiry when present;
2. serialize refresh;
3. save both new tokens atomically;
4. retry the original read at most once after successful refresh.

---

## 8. Single-flight refresh is mandatory

Two independent implementations protect refresh with:

- a mutex/lock; or
- a single shared `refreshPromise`.

This prevents parallel telemetry/history/alarm requests from simultaneously using the same rotating refresh token.

### Canonical Windows rule

Within one authenticated session:

```text
if refresh already in progress:
    await it
else:
    acquire refresh lock
    re-check expiry
    refresh once
    atomically save new token pair
```

Do not let each HTTP request perform its own independent refresh.

---

## 9. Authentication-expiry signals

Different API areas do not report expired authentication identically.

Observed signals include:

### HTTP

- HTTP **401**
- HTTP **403** in some clients/contexts

### Business codes/messages

- code **9** + “Token expired”
- code/business values **401**, **1001**, **1002** in a current client
- endpoints whose message contains variants of:
  - token expired;
  - token invalid;
  - missing token;
  - not logged in.

A missing token can also cause an endpoint to return a misleading non-auth error such as `20101 Illegal argument`.

### Recommended classifier

Treat auth expiry as a combination of:

- HTTP status;
- known business code;
- narrowly matched token/session message.

Then perform **one serialized refresh + one original-request retry**.

If that fails, mark the session as requiring re-authentication.

Never recursively retry refresh forever.

---

## 10. Login/signing errors

Known codes:

| Code | Meaning | Retry? |
|---|---|---|
| 7 | login password input/protocol failure; plaintext password is rejected | NO; correct credentials/protocol |
| 44 | IoT Open signing error | NO automatic retry unless local signing input changed/fixed |
| 20007 | account/environment mismatch observed when test/prod tuple is wrong | NO; verify environment |

These are deterministic configuration/protocol errors.

Backoff does not solve them.

---

## 11. Argument/schema errors

### 20101 — Illegal argument

This is broad and can mean different client mistakes.

Confirmed examples include:

- invalid/malformed timezone;
- malformed western UTC offset;
- unsupported body field;
- rounded/corrupted long identifier;
- some session-scoped endpoints when called without a token.

### Handling

Do not blindly retry.

Log, in redacted form:

- endpoint;
- argument names/types;
- timezone;
- identifier string length/value-safe representation;
- server message.

Then classify/fix the request.

---

## 12. Capability/data errors

### 70132 — Energy flow rule not exists

Meaning:

the device/account has no matching energy-flow rule configured server-side.

Handling:

- mark energy-flow feature unavailable for that device;
- continue other telemetry/history collection.

Not a transport failure.

### 70134 — Config attribute not exists

Configuration/control specific and outside active project scope.

Operational lesson:

- device/protocol capability differs;
- never treat missing feature as server outage.

### 70247 — manufacturer-disabled instruction service

Control-specific and out of scope.

Operational lesson is the same: capability denial is not a retryable network error.

### 71301

Observed on a remote config read; precise meaning unresolved.

Out of active scope.

Preserve raw code/message; do not guess.

---

## 13. Business-error rule

Default behavior for:

`HTTP success + API code != 0`

should be:

**do not automatic-retry**, unless the code/message is specifically classified as authentication expiry or a known transient server condition.

Why:

- many business errors are deterministic;
- retrying `20101` hundreds of times only hammers the server;
- capability failures remain failures no matter how long we wait.

---

## 14. Network/HTTP retry behavior

Independent current clients converge on modest transient retries.

Examples:

- one client: default **2 retries**, 600 ms exponential delay;
- another: realtime request timeout around **15 s**, retry once on timeout/connection failure;
- Home Assistant client: **30 s** request timeout and coordinator-level later retry;
- current server client: **8 s** timeout.

### Recommended Windows policy for read-only requests

#### Interactive/current-state request

- timeout: **10–15 seconds**;
- retry: **1 time** for connection/timeout/HTTP 5xx;
- small randomized backoff.

#### Background/history/aggregate request

- timeout: **20–30 seconds**;
- retry: **2 times** for connection/timeout/HTTP 5xx;
- exponential backoff with jitter.

Example:

- first retry ~1 s;
- second retry ~2–3 s.

### Do not retry automatically

- most HTTP 4xx;
- invalid arguments;
- unsupported capability;
- invalid credentials;
- signing errors.

Exception:

- authentication expiry → refresh once, then retry once.

---

## 15. HTTP 5xx

A current API client explicitly treats **5xx** as transient and retries with exponential backoff.

This is reasonable for read-only requests.

### Rule

If retries exhaust:

- preserve existing cached data;
- mark refresh attempt failed;
- do not replace last known values with zeros/nulls merely because the request failed.

The UI should show:

- stale/offline/error state;
- last successful source timestamp.

---

## 16. Rate limits

### What was searched

Public GitHub implementations, issues/docs, web search for:

- 429;
- Too Many Requests;
- rate limits;
- polling intervals;
- throttling.

### Result

No credible Solar of Things/SiSeLi source found in this research publishes:

- requests/minute;
- requests/hour;
- burst limit;
- 429 retry-after contract.

No reliable public incident was found that establishes a throttle threshold.

### Conclusion

**RATE LIMIT: UNKNOWN**

Do not invent one.

### Safe engineering response

- poll no faster than the use case requires;
- cache invariant metadata;
- combine multiple history keys into one request;
- use pagination/date chunking rather than parallel request floods;
- cap concurrency;
- back off on 429 if one is ever observed;
- honor `Retry-After` if the server supplies it.

For one household account, a concurrency cap around **2–4 simultaneous reads** is a conservative starting point.

This is a design choice, not a vendor limit.

---

## 17. Why 1-minute polling is not inherently abusive — but is often redundant

At least one substantial public SiSeLi application polls live device state every minute.

Another server-side poller also uses one-minute ticks with bounded concurrency and backs offline devices down to five minutes.

This demonstrates that one-minute polling has been used successfully.

But our source cadence evidence says raw cloud frames usually advance around every five minutes.

### Recommendation

Use one minute only when:

- a visible live dashboard benefits from detecting the new cloud frame quickly;
- alarms need faster detection.

Use five minutes for normal archival/statistical collection.

---

## 18. Adaptive polling

A public server implementation uses:

- 1-minute polling for online devices;
- 5-minute polling for offline/idle devices;
- bounded parallelism.

This is a useful design pattern.

### Windows adaptation

For a single user's desktop collector:

#### Device currently online + dashboard visible

60–120 s live check.

#### Device online + background

5 min.

#### Device offline/stale

5–15 min is enough until activity resumes.

#### PC wakes/resumes/network reconnects

Perform one immediate refresh/backfill of recent history, then resume normal cadence.

Adaptive polling reduces unnecessary calls without losing meaningful device data.

---

## 19. Offline versus stale

Do not make one boolean carry all meanings.

### Cloud says offline

Device metadata may provide:

- `isOnline`;
- state;
- `lastDataAt`;
- `lastOnlineAt`;
- `lastOfflineAt`.

### Stale telemetry

A source timestamp is old relative to that device's normal reporting cadence.

The two can disagree temporarily.

### Phone/PC network offline

The Windows machine cannot reach the internet.

This says nothing by itself about inverter state.

### Recommended quality model

Track separately:

- `collector_online`;
- `cloud_request_ok`;
- `device_is_online`;
- `source_age_seconds`;
- `telemetry_quality = fresh | delayed | stale | unavailable`.

---

## 20. Suggested stale thresholds

There is no vendor-defined universal stale threshold in the corpus.

A current client uses a **15-minute maximum age** for some live status.

Another analytical UI breaks graph continuity when silence exceeds approximately:

`3 × typical cadence`

clamped to **15–60 minutes**.

A different automation project considered >30 minutes without realtime data dangerously stale for its control use case.

### Dashboard recommendation

Learn the device's median cadence from recent history.

Starting classification:

- **fresh:** source age ≤ 2 × median cadence;
- **delayed:** 2–3 × median cadence;
- **stale:** > max(15 min, 3 × median cadence);
- **unavailable/offline:** cloud explicitly says offline or no useful source for a much longer interval.

For a ~5-minute device:

- fresh ≈ ≤10–12 min;
- stale ≈ >15–18 min.

These are UI/data-quality thresholds, not claims about inverter failure.

---

## 21. Partial history page failure

Round 10 established that history can require pagination.

If pages 1–3 succeed and page 4 fails:

the period is **partial**.

### Correct behavior

- keep/upsert successful pages;
- mark requested window incomplete;
- retry missing page/window later;
- do not present totals computed from the partial period as fully authoritative.

### Retry granularity

Retry the failed page or the bounded day/window.

Do not restart months of already successful backfill.

---

## 22. Cache last-known-good data

For any transient outage:

- keep last known value;
- preserve source timestamp;
- mark stale;
- do not zero it;
- do not overwrite it with a network-error null.

This is especially important for:

- PV/load/grid power;
- battery SOC;
- daily counters;
- server aggregates.

A request failure is not a physical zero.

---

## 23. Current-only API snapshots

Some useful surfaces lack historical APIs:

- latest state;
- structured energy flow;
- device details;
- current config.

If their changes matter later, the local Windows database must snapshot them over time.

### Reliability policy

Snapshot only when:

- source timestamp changes; or
- content hash/value changes; or
- a defined low-frequency audit interval expires.

Do not create duplicate “new history” every minute from an unchanged cloud object.

---

## 24. Current aggregate refresh strategy

Current-period server aggregates can revise as new data arrives.

Recommended:

### Today

5-minute refresh, aligned with normal telemetry collection.

### Yesterday

refresh several times after midnight because delayed frames may update the closed day.

### Older closed days

treat as stable after a settling period, but allow later audit/backfill to overwrite by upsert.

### Current month/year

periodically refresh.

### Historical closed month/year

cache aggressively.

---

## 25. Concurrency

### Token concurrency

Must be serialized, as above.

### API request concurrency

There is no demonstrated need for high parallelism in a one-account Windows client.

Use:

- small request queue;
- prioritize visible/live data;
- background history second;
- metadata/closed aggregates last.

Suggested initial cap:

**2–4 concurrent requests**

This minimizes accidental bursts and simplifies token refresh.

### Backfill

Prefer sequential or very lightly parallel daily windows.

A long initial backfill can run slowly; it is a one-time operation and completeness matters more than speed.

---

## 26. Scheduling drift

A fixed timer such as:

“every 5 minutes from program start”

does not necessarily align to device report timestamps.

That is fine.

Because we use an overlapping trailing window, no exact synchronization with the inverter is required.

### Better collector loop

```text
wake
→ fetch trailing window
→ upsert
→ record newest source timestamp
→ wait nominal interval
```

If the previous cycle takes unusually long, do not launch an overlapping second copy of the same cycle.

---

## 27. Prevent overlapping collection runs

Use one collection lock per account/device or a globally serialized collection pipeline.

If a 5-minute cycle is still running when the next timer fires:

- skip/coalesce the new trigger;
- do not run duplicate backfills concurrently.

This avoids:

- duplicated traffic;
- token-refresh races;
- database contention;
- confusing “newer run finished before older run” state.

---

## 28. Recovery after Windows sleep/network loss

A desktop machine may sleep for hours.

Do not attempt to replay each missed scheduled poll.

On resume:

1. check authentication;
2. discover current device status;
3. query historical data from:
   `last stored source timestamp - overlap`
   through now;
4. paginate/upsert;
5. refresh current aggregate;
6. resume normal timer.

The cloud history is the recovery mechanism.

This is an important advantage over a system that depends only on live snapshots.

---

## 29. Startup behavior

Recommended sequence:

1. load cached data immediately;
2. validate/refresh token if near expiry;
3. station/device metadata refresh;
4. current/latest state;
5. trailing recent-history reconciliation;
6. current-day aggregate;
7. alarms;
8. slower metadata/schema checks;
9. any scheduled background backfill.

The UI should not wait for a full historical backfill before showing the last locally cached dashboard.

---

## 30. Recommended failure state machine

### Success

- update cache/database;
- update source timestamp;
- clear transient network failure state.

### Timeout / DNS / connection / 5xx

- retry boundedly;
- if still failing:
  - keep cache;
  - mark cloud refresh failed;
  - increase backoff.

### HTTP/business auth expired

- single-flight token refresh;
- retry original request once;
- if still rejected, require re-auth.

### 20101 or other deterministic argument error

- no retry;
- record request-class diagnostic;
- disable/bypass the broken path until corrected.

### Capability-specific error

- mark feature unavailable for that device;
- continue other endpoints.

### Partial paginated history

- commit successful pages;
- mark window partial;
- schedule targeted retry.

---

## 31. Error logging requirements

Logs should preserve enough to debug without leaking secrets.

Log:

- endpoint path;
- method;
- HTTP status;
- API code;
- sanitized server message;
- attempt number;
- timeout duration;
- source device/station ID in safe string form if acceptable locally;
- requested time window/page/count;
- timezone;
- elapsed time.

Never log:

- password;
- access token;
- refresh token;
- cookies;
- reusable application secret;
- full signed authentication headers.

---

## 32. Suggested collection cadences — concise reference

| Data | Foreground/visible | Background | Notes |
|---|---:|---:|---|
| Latest/live telemetry | 60–120 s | 5 min | Source usually changes ~5 min |
| Raw history tail | 60–120 s if live graph | 5 min | Re-read trailing ~15 min |
| Alarms | 1–2 min if timely alerts matter | 5 min | No confirmed push path |
| Current-day aggregate | 5 min | 5–15 min | Mutable until day closes |
| Device list/details | manual/startup | 15–30 min | Low volatility |
| Attribute/schema metadata | on model change/startup | daily | Cache heavily |
| Yesterday aggregate | n/a | 30–60 min for first hours after midnight | Catch delayed corrections |
| Older daily history | n/a | 6–24 h / audit only | Closed/stable |
| Current month/year aggregate | page-dependent | 15–60 min | Can revise |
| Closed months/years | cache | daily/startup/audit | Very low volatility |

These values are conservative engineering defaults, not vendor-published quotas.

---

## 33. Rate-limit response policy if discovered later

Because no quota is known, implement generic support now.

If HTTP **429** is observed:

1. honor `Retry-After` if present;
2. stop that request class temporarily;
3. exponentially back off;
4. reduce concurrency;
5. log the event;
6. never busy-loop.

This makes the collector robust even though no public 429 evidence currently exists.

---

## 34. Reliability lessons from existing clients

### Home Assistant integration

Uses:

- 5-minute device coordinator;
- 30-minute station coordinator;
- 30-second HTTP timeout;
- proactive token refresh ~5 min before expiry;
- a refresh mutex;
- one retry after HTTP 401;
- coordinator-level failure recovery.

This is conservative and aligns well with normal background dashboard operation.

### Current Sierro/SiSeLi application project

Uses:

- 10-second generic API timeout;
- up to two transient retries;
- 600 ms exponential retry base;
- one shared refresh promise;
- auth recognition by HTTP status, code and message;
- live history tail reread every 60 s.

This is a good model for an interactive UI.

### Independent Windows-oriented client

Uses:

- one-minute data fetch;
- 15-second connect/receive timeout;
- one transient retry for realtime DNS/timeout failures.

This shows that frequent read-only cloud polling can work, but not that the underlying data cadence is one minute.

### Real archival collector

Uses:

- three-hour trailing raw-history window during ordinary refresh;
- paginated upsert;
- current snapshots plus aggregate collection;
- local dashboard caches according to volatility.

This is a strong model for data durability.

---

## 35. Important auth implementation conflict to preserve

One Home Assistant integration currently sends only:

`{ refreshToken }`

to the refresh endpoint.

More recent/current live evidence in the energy-app project says the backend requires:

`{ accessToken, refreshToken }`

as a pair and rotates both.

Round 03/06 already favored the access+refresh pair.

### Decision

For the future Windows client:

**use the current access+refresh pair**.

Do not copy the refresh-token-only implementation merely because it exists publicly.

This is exactly why evidence precedence matters.

---

## 36. Known error taxonomy relevant to this project

| Signal | Meaning | Retry/action |
|---|---|---|
| HTTP 401 | session/auth rejected | serialized refresh once, retry once |
| HTTP 403 | auth/authorization/timezone seen in public clients | inspect context; refresh if session-related; do not loop |
| code 7 | login/password protocol failure | no retry; correct auth |
| code 9 | token expired | refresh once |
| business 401/1001/1002 | token/session expiry in current client | refresh once |
| code 44 | signing error | no blind retry |
| code 20007 | wrong account/environment combination | configuration fix |
| code 20101 | illegal argument | validate ID/timezone/offset/body/auth |
| code 70132 | no energy-flow rule | feature unavailable, continue |
| code 70134 | config key unavailable | out of scope; capability issue |
| code 70247 | manufacturer-disabled instruction | out of scope; capability issue |
| code 71301 | config-read error, exact meaning unresolved | out of scope; preserve raw |
| timeout/DNS/connect | transient | 1–2 retries + backoff |
| HTTP 5xx | transient server failure | 1–2 retries + backoff |
| HTTP 429 | not observed/contract unknown | if seen, honor Retry-After/back off |
| partial history page | incomplete data | preserve pages, targeted retry |

Unknown codes must always be preserved and surfaced diagnostically.

---

## 37. What this means for statistical integrity

Reliability behavior is not merely a networking issue.

A bad retry/cache implementation can produce mathematically wrong dashboards.

Examples:

- network failure converted to zero → fake power outage;
- stale SOC stamped with current time → fake fresh battery reading;
- missing history page silently accepted → understated energy total;
- duplicate late frame appended instead of upserted → overstated aggregates;
- two refreshers race → broken session and missing collection interval;
- placeholder server aggregate treated as zero → false consumption statistic.

Therefore data-quality state belongs alongside every statistical source.

---

## 38. Minimum durability metadata

For each collection run/window, record:

- requested_at;
- completed_at;
- source endpoint;
- target device/station;
- requested time range;
- pages expected/received when known;
- HTTP/API outcome;
- newest source timestamp;
- completeness status;
- retry count.

For each displayed current value retain:

- source timestamp;
- collected timestamp;
- quality/staleness state.

This is enough to distinguish a real measurement from an acquisition problem later.

---

## 39. Remaining unknowns

Public research cannot reliably determine:

1. formal vendor rate limits;
2. actual `Retry-After` behavior;
3. backend maintenance windows/SLA;
4. universal token TTL;
5. maximum late-arrival delay;
6. how long an `isOnline` change lags physical connectivity;
7. whether all device families follow ~5-minute cadence;
8. whether the backend revises old raw telemetry after very long delays;
9. exact error behavior for the user's account/model.

These belong to controlled read-only validation, not more speculative source searching.

---

## 40. Round-close conclusion

**Round 12 is complete.**

A Windows collector does not need aggressive polling.

The recommended architecture is:

`~5-minute durable historical collection + 15-minute overlap + optional 60–120-second foreground freshness checks + bounded retries + single-flight token refresh + explicit stale/completeness metadata`.

There is no evidence-based reason to poll the full API every few seconds.

There is also no evidence-based vendor rate-limit number, so the implementation must remain conservative and adaptive.

---

## 41. Next-round decision

Proceed separately to:

**Round 13 — Cloud-UI Coverage Audit**

This should answer:

> Of the useful information visible in the Solar of Things web/cloud experience, what can the API retrieve, and are there any dashboard-relevant cloud values or statistics we have not yet mapped?

The audit should:

- inventory cloud UI screens/cards/charts/data views;
- map each useful display to a known API endpoint/property where possible;
- identify UI-visible but API-unmapped metrics;
- identify API-accessible cloud data not exposed prominently in the UI;
- ignore settings/control/local/BLE/provisioning screens because they are out of project scope;
- produce a gap list for final read-only validation.

After Round 13, the only surviving planned investigation should be controlled read-only validation against the user's own account/device, unless the coverage audit uncovers a specific cloud-data gap requiring one targeted research round.

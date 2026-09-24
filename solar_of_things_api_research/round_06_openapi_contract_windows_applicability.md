# Round 06 — OpenAPI Contract Reconstruction and Windows-Client Applicability

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_06_windows_api_canonical_map.md`

## Purpose

This round converts the research corpus into a canonical HTTP/API contract specifically for the intended **Windows client**.

It reconciles:

- the 221 distinct method/path pairs reproducibly extracted from the preserved first-party Swagger-derived API documentation;
- the 115 distinct production method/path pairs accumulated from HAR, current browser captures, login permissions and later live implementations;
- the authentication/token corrections from Rounds 02–03;
- the environment and role/capability findings from Round 05.

The result is not implementation code. It is a decision-ready contract describing what a Windows program actually needs, what is optional, what is dangerous/mutating, what is role/device gated, and what remains uncertain.

---

## 1. Fundamental project conclusion

### Android is not a dependency

The Solar of Things cloud API is an independent HTTPS backend.

A Windows program can communicate directly with:

`https://solar.siseli.com/apis`

using the same cloud authentication/session protocol used by other clients.

The Android application is one client of this backend; it is not the backend and is not required to operate it.

Therefore the main Windows project can proceed without:

- launching Android;
- emulating Android;
- automating the Android UI;
- embedding an APK;
- depending on Android Bluetooth code.

Android remains useful only as optional corroboration for mobile-only/local behavior.

---

## 2. Canonical HTTP surface size

The canonical union now contains:

- **221** Swagger-derived method/path pairs;
- **115** production-observed method/path pairs;
- **62** exact overlaps;
- **159** Swagger-only routes;
- **53** production-only routes;
- **274 distinct method/path contracts in the union**.

The complete 274-route matrix is stored in:

`round_06_windows_api_canonical_map.md`

Every route in that file has:

- method;
- relative path;
- source/provenance;
- operation class;
- Windows-project priority;
- Swagger summary where available;
- documented contract fragment where available.

### Why 274 does not mean “implement 274 endpoints”

Most of the platform is irrelevant to the initial Windows objective.

The route union includes:

- manufacturer administration;
- user hierarchy;
- firmware management;
- SIM-card administration;
- station/device CRUD;
- exports;
- QR/app/captcha functions;
- peak-valley scheduling;
- automation/instructions;
- low-level passthrough;
- Bluetooth/local protocol helpers.

A monitoring/analytics client needs only a small, evidence-rich subset.

---

## 3. Windows-client priority model

### P0 — minimal cloud client

Only **12 contracts** are designated P0.

#### Session

1. `POST /login/account`
2. `POST /login/refresh/access/token`
3. `POST /login/logout`

#### Station/device discovery

4. `POST /station/list`
5. `GET /station/details`
6. `POST /device/list`
7. `GET /device/details`

#### Device schema and current state

8. `GET /deviceState/simple/gatherAttributes/v1`
9. `GET /deviceState/simple/state/latest/v1`
10. `GET /deviceState/simple/energy/flow/v1`

#### History and alarms

11. `POST /deviceState/simple/attribute/keys/history/v1`
12. `POST /alarm/query/list`

That is enough to build a useful first Windows application that:

- authenticates;
- discovers the user's stations and devices;
- understands the device's available attributes;
- reads current state;
- reads the power-flow view;
- retrieves historical series;
- displays alarms.

No Android integration is necessary.

### P1 — useful optional reads

P1 contains analytics, summaries, reports, dictionaries, device metadata, user/profile reads, configuration reads and alternative/fallback state/history representations.

Examples:

- station/device overview daily/monthly/yearly totals;
- station energy flow;
- owner overviews;
- current remote-device state;
- cached configuration;
- asynchronous configuration reads;
- alarm/report details;
- firmware lists/logs;
- device/gather-protocol metadata;
- dictionaries;
- portal/router metadata;
- user information;
- production report/export metadata.

These should be implemented only when the UI or analysis actually needs them.

### P2 — mutations/control

P2 is disabled by default for a monitoring client.

It includes:

- station/device add/edit/delete/pin;
- account/profile changes;
- config writes;
- raw passthrough;
- DTU restart;
- fast-report start/stop;
- peak-valley writes/enabling;
- automation/instruction creation/update/deletion;
- device offsets;
- firmware upgrade creation;
- resource uploads;
- alarm deletion/ignore operations.

These routes require deliberate safety design and device-specific validation before use.

### P3 — specialized/admin/local

P3 contains:

- manufacturer/integrator/admin functions;
- SIM administration;
- user groups/hierarchy;
- `/near/dtu/*` local protocol-generation/parsing services.

They should not be part of the normal Windows cloud client unless the project later acquires a concrete need for them.

---

## 4. Recommended Windows architecture

The API evidence naturally separates into modules.

### 4.1 Transport / signer

Responsibilities:

- production base URL;
- compact JSON serialization;
- IoT Open signing for routes that require it;
- `IOT-Token` for authenticated session traffic;
- `IOT-Time-Zone` where appropriate;
- normal JSON envelope handling;
- HTTP timeout/retry rules.

Do not hard-code a reusable application secret into source control.

The signing credential should be supplied through a local secret/configuration mechanism if/when implementation begins.

### 4.2 Session manager

Responsibilities:

- account/password login;
- MD5 password preprocessing required by the protocol;
- access/refresh token storage;
- server-returned expiry handling;
- single-flight token refresh;
- atomic replacement of both tokens;
- explicit logout.

Current best refresh contract:

`POST /login/refresh/access/token`

with:

`{ accessToken, refreshToken }`

Current evidence says refresh tokens rotate/single-use.

Therefore two independent processes must not race to refresh the same token chain.

### 4.3 Discovery/model layer

Responsibilities:

- station list/details;
- device list/details;
- DTU/logger identifiers;
- machine/model/protocol metadata;
- capability metadata;
- all platform IDs represented losslessly as strings.

This layer should be authoritative for IDs.

Never parse station/device/user identifiers through a floating-point numeric type that can lose 64-bit precision.

### 4.4 Schema/telemetry layer

The key design principle is:

**device telemetry is schema-driven, not one universal hard-coded structure.**

Use:

`GET /deviceState/simple/gatherAttributes/v1`

to learn available attributes and metadata.

Then use:

`GET /deviceState/simple/state/latest/v1`

for current state.

The Windows model should preserve at least:

- raw key;
- raw value;
- display value if supplied;
- unit;
- human label;
- timestamp;
- source endpoint;
- device/model/protocol context.

Do not assume that identical physical concepts always use identical field names.

### 4.5 Energy-flow layer

Primary:

`GET /deviceState/simple/energy/flow/v1`

The correct `dataSource` can vary by device.

Known behavior includes error:

`70132 — Energy flow rule not exists`

This should be treated as a capability/schema issue, not as a generic networking failure.

### 4.6 History layer

Preferred current production route:

`POST /deviceState/simple/attribute/keys/history/v1`

Current portal behavior supports requests with:

- `deviceId`;
- selected `keys[]`;
- `fromTime`;
- `toTime`;
- `page`;
- `count`;
- `orderByTimeAsc`.

The reply is columnar:

- `timeSeries[i]`;
- `fields.<key>[i]`.

The implementation must align each field array by index with `timeSeries` and preserve null/missing points rather than converting them silently to zero.

Current production has been seen using `count = 1500` for daily history.

Alternative/fallback history routes exist, including record-list forms, but should remain fallback paths until a target device actually needs them.

### 4.7 Analytics layer

Use only when needed.

Potentially useful families:

- `deviceOverView/*`;
- `stationOverView/*`;
- `ownerOverView/*`;
- `dashboard/*`.

They can provide:

- daily generation;
- monthly/yearly totals;
- category summaries;
- station-level statistics;
- income series;
- historical aggregates.

These are preferable to reconstructing long-term totals locally when the server already exposes the desired aggregate.

### 4.8 Configuration-read layer

Separate passive telemetry from configuration acquisition.

Useful read routes include:

- cached config retrieval;
- single config read;
- batch config read + details polling;
- write-record history.

Some configuration reads may cause an actual device/logger interaction.

Therefore they are classified separately as **ACTIVE_DEVICE_READ** rather than ordinary passive reads.

### 4.9 Optional control gateway

If control is later required, put every mutation behind one explicit module with:

- opt-in enabling;
- capability checking;
- per-device/model allowlists;
- value validation;
- read-before-write;
- post-write verification;
- audit logging;
- no automatic retry for ambiguous writes.

Do not mix control calls into ordinary telemetry code.

---

## 5. High-confidence authentication contract

### Login

`POST /login/account`

Password preprocessing:

`MD5(UTF-8 plaintext password).lowercaseHex`

Open signing model:

1. serialize exact compact body;
2. non-GET body hash = SHA-256 of exact body;
3. GET body hash = empty string;
4. collect URL query parameters plus:
   - `IOT-Open-AppID`;
   - `IOT-Open-Nonce`;
   - `IOT-Open-Body-Hash`;
5. sort keys lexicographically;
6. join as `key=value&...`;
7. Base64 encode UTF-8 canonical string;
8. HMAC-SHA256 with application secret;
9. MD5 raw HMAC bytes;
10. lowercase hexadecimal -> `IOT-Open-Sign`.

This is now supported by:

- historical official web-bundle reverse engineering;
- live production interoperability;
- first-party Swagger-derived documentation.

The competing hex-preimage interpretation is not recommended.

### Normal authenticated calls

Core session header:

`IOT-Token: <access-token>`

Open-sign headers are **not proven necessary on every authenticated route**.

The official web client has post-login operations evidenced with the session token as the central auth mechanism.

### Refresh

Preferred current contract:

`POST /login/refresh/access/token`

body:

`{ accessToken, refreshToken }`

Refresh results must replace the token pair atomically.

---

## 6. Time handling

Time-zone correctness is a first-class protocol requirement.

Use:

- an IANA zone in `IOT-Time-Zone`;
- ISO-8601 timestamps with explicit local UTC offset where the endpoint expects date-time ranges.

Malformed offset/time-zone combinations have produced:

`20101 — Illegal argument`

The application should keep:

- user/site timezone;
- UTC instant;
- local display time;

as distinct concepts.

Do not derive a station's historical day solely from the Windows machine's local timezone if the station belongs to another timezone.

---

## 7. Identifier handling

Platform identifiers may exceed JavaScript's safe integer range and should be treated generically as opaque identifiers.

For Windows code, the safest application-level representation is:

**string**

for:

- userId;
- authId;
- stationId;
- deviceId;
- dtuId;
- report IDs;
- batch-read IDs;
- instruction IDs.

If a JSON library receives a large bare numeric token, configure it for lossless parsing or capture it before conversion to IEEE-754 floating point.

---

## 8. Device/schema variability rules

Never assume:

- one field dictionary across every inverter;
- one power unit;
- one sign convention;
- one PV channel count;
- one battery-current convention;
- one `dataSource`;
- one configuration-key vocabulary.

Known public examples include:

- W vs kW differences;
- opposite import/export signs between device families;
- different battery SOC keys;
- different PV-power keys;
- firmware-specific aliases;
- config keys that return `70134 Config attribute not exists` on another model.

### Windows-model rule

A measurement should retain:

`device + raw key + raw value + unit + timestamp + metadata`

before projecting it into a normalized concept such as:

- grid import;
- grid export;
- PV power;
- battery SOC;
- load power.

Normalization must be device/schema aware.

---

## 9. Capability discovery

Capability should be inferred from multiple sources:

1. login role flags;
2. station/device detail metadata;
3. gather-protocol/device model;
4. attribute metadata;
5. endpoint-specific capability endpoints;
6. actual structured API errors.

Do not infer support merely because Swagger documents a route.

Examples:

- `70132` — energy-flow rule absent;
- `70134` — config attribute absent;
- `70247` — manufacturer disabled automation/instruction capability.

A capability failure should generally hide/disable that feature for the relevant device rather than make the entire client fail.

---

## 10. REST alternatives and fallback policy

Where multiple routes expose similar information, use evidence precedence rather than trying all variants indiscriminately.

### Current state

Preferred:

`/deviceState/simple/state/latest/v1`

Alternative:

`/remote/device/state/latest`

Use an alternative only when a concrete device/client need justifies it.

### History

Preferred current portal route:

`/deviceState/simple/attribute/keys/history/v1`

Fallback candidates:

- `/deviceState/attribute/record/list`;
- `/deviceState/attribute/record/list/v2`;
- simple record-list forms.

Do not probe every history endpoint on every request.

### Energy flow

Preferred:

`/deviceState/simple/energy/flow/v1`

The `dataSource` value should be learned/validated per device.

---

## 11. Polling and realtime behavior

No sufficiently authoritative public evidence establishes a universal Solar of Things rate limit.

Third-party projects have used aggressive intervals such as 5–15 seconds, but those are implementation choices rather than vendor guarantees.

For a Windows client:

- start conservatively;
- make refresh and telemetry polling independent;
- cache metadata/schema;
- avoid repeatedly fetching invariant dictionaries;
- back off on transport/server failures;
- avoid retrying mutating calls automatically;
- add faster polling only when a concrete use case requires it.

### WebSocket

Do **not** implement the previously claimed `/openapis/ws` WebSocket based on current evidence.

It remains unverified and was later removed as dead code by a downstream project.

REST/polling has much stronger evidence.

---

## 12. What the Windows project can ignore initially

For the first cloud-monitoring implementation, ignore:

- Android APK internals;
- BLE GATT;
- Wi-Fi provisioning;
- raw inverter serial protocol;
- `/near/dtu/*`;
- firmware upgrade;
- station/device CRUD;
- timed instructions;
- peak-valley control;
- passthrough;
- SIM administration;
- manufacturer/integrator dashboards;
- user hierarchy;
- report-export jobs;
- WebSocket.

None is required to authenticate and retrieve useful solar/inverter data.

---

## 13. What may become valuable later

### Configuration inspection

If the project needs to show actual inverter configuration:

- cached configuration is a relatively low-impact first step;
- active config reads should be explicit.

### Cloud passthrough

`POST /remote/device/passthrough`

is strategically important because it provides a cloud-mediated lower-level device transport.

A future Windows control project may therefore reach functionality normally associated with local/device protocols **without Android**.

It remains a high-risk control surface and should not be part of the monitoring MVP.

### Near/DTU protocol helpers

The 17 documented `/near/dtu/*` routes expose server-side generation/parsing helpers for local/proximal device frames.

These could become extremely useful if the Windows project later talks directly to a logger/inverter over a local transport.

They are not needed for cloud-only monitoring.

---

## 14. Canonical route-source authority

For future implementation decisions, use this evidence order:

1. current production browser capture;
2. live production interoperability with controlled read-only requests;
3. first-party Swagger-derived contract;
4. production HAR;
5. historical official-client reverse engineering;
6. independent third-party implementations;
7. unvalidated documentation/comments.

When sources disagree, do not silently merge them.

Record the disagreement and choose the highest-quality current evidence.

---

## 15. Safe validation strategy for a future user account

When the project reaches live validation, the safest progression is:

### Stage A — authentication only

- login;
- inspect role/session metadata;
- logout;
- verify refresh behavior.

### Stage B — discovery

- station list/details;
- device list/details.

### Stage C — passive telemetry

- gather attributes;
- latest state;
- energy flow;
- alarms.

### Stage D — history

- one known device;
- narrow date range;
- small selected key set;
- verify timestamps/units against Solar of Things UI.

### Stage E — optional read-only capability

- summaries/analytics;
- cached configuration;
- reports.

Only after those are stable should any mutation/control route be considered.

---

## 16. Error handling baseline

Known useful application/business codes include:

| Code | Meaning/evidence | Suggested handling |
|---|---|---|
| 0 | success | process response |
| 7 | password/auth input failure observed with wrong plaintext handling | fail login; do not retry blindly |
| 9 | token expired | perform one serialized refresh |
| 44 | signing error | configuration/protocol error, not credential retry |
| 20007 | account error in wrong test/prod environment combination | verify environment tuple |
| 20101 | illegal argument | validate IDs, timezone, offsets, body/query types |
| 70132 | energy-flow rule absent | mark feature unavailable |
| 70134 | config attribute absent | mark key unsupported for that device |
| 70247 | manufacturer disabled instruction capability | feature unavailable |
| 71301 | seen on config read; exact meaning unresolved | surface raw code/message and avoid guessing |

HTTP 401/403 should also be treated as authentication/session failures where applicable.

Preserve unknown API error codes and server messages rather than mapping all nonzero responses into one generic exception.

---

## 17. Security rules for implementation

1. Never commit passwords, tokens, cookies or reusable application secrets.
2. Store user credentials/tokens using Windows-appropriate secure storage.
3. Treat refresh tokens as rotating secrets.
4. Redact tokens from logs.
5. Do not log Wi-Fi passwords if local provisioning is ever added.
6. Keep mutation/control support disabled until explicitly configured.
7. Require HTTPS.
8. Preserve exact request serialization for signed requests.
9. Do not expose raw low-level passthrough to an ordinary UI without validation.
10. Separate telemetry logs from authentication/security logs.

---

## 18. Recommended first implementation milestone

A Windows MVP can be limited to:

1. secure configuration/session storage;
2. login + refresh + logout;
3. station/device discovery;
4. device schema discovery;
5. current telemetry;
6. energy flow;
7. historical selected-key charts/data;
8. alarms;
9. local persistence/cache;
10. export performed locally by the Windows application if desired.

That avoids all cloud mutations while already providing most of the useful monitoring/analysis value.

---

## 19. Canonical route matrix statistics

The companion map classifies all **274** method/path pairs.

Current engineering-class counts:

- 101 `OPTIONAL_READ`;
- 59 `TELEMETRY_ANALYTICS_READ`;
- 15 `ADMIN_READ`;
- 9 `DEVICE_CONTROL_READ`;
- 17 `LOCAL_PROTOCOL_HELPER`;
- 3 `ACTIVE_DEVICE_READ`;
- 4 `DEVICE_READ`;
- 20 `MANAGEMENT_MUTATION`;
- 3 `AUTH_SUPPORT`;
- 17 `DEVICE_CONTROL_MUTATION`;
- 4 `ADMIN_MUTATION`;
- 7 `AUTH_SESSION`;
- 15 `ACCOUNT_MUTATION`.

Priority counts:

- **P0: 12**
- **P1: 170**
- **P2: 60**
- **P3: 32**

The large P1 count does not mean those routes should all be implemented. P1 means “non-core read functionality that is reasonably compatible with a Windows client if a use case appears.”

---

## 20. Round-close assessment

Round 06 completes the cloud HTTP contract reconstruction necessary to begin designing a Windows client.

The key result is that the API surface is no longer an undifferentiated collection of reverse-engineered endpoints:

- a small 12-contract P0 core is identified;
- the larger read surface is separated from mutations;
- active device reads are separated from passive telemetry;
- specialized local/admin services are isolated;
- each of 274 routes carries provenance and a Windows-project disposition.

### Next-round evaluation

The next originally planned branches are MQTT/uplink, BLE and raw serial.

For the stated Windows cloud-API project, **BLE is not the next priority**.

The most useful next independent round is:

### Round 07 — Dongle-to-cloud MQTT/uplink archaeology

Why:

- it is independent of Android;
- it shows what the physical logger sends to SiSeLi before the REST API normalizes it;
- it can reveal fields/telemetry unavailable through a particular REST endpoint;
- it can clarify reporting cadence, payload block structure, firmware/model differences and cloud transport;
- it may provide an alternative local/passive acquisition path for a Windows machine if direct cloud REST later proves insufficient.

BLE and raw serial should remain separate later rounds.

Round 07 should therefore deeply analyze the RWB1/SiSeLi MQTT/uplink projects and packet structure, while keeping the primary Windows MVP cloud-REST design unchanged.

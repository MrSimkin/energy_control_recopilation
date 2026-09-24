# Round 02 — Cloud REST API Repository Archaeology

Date: 2026-09-24

Status: COMPLETE

Companion catalog: `round_02_endpoint_catalog.md`

## Purpose

This round reconstructs the Solar of Things / SiSeLi cloud-facing REST API from public client implementations, repository history, issue reports, release notes, tests, and captured-browser documentation. It deliberately does not analyze the official production JavaScript bundle or Android APK in depth; those are reserved for later source-of-truth rounds.

The principal goal is not merely to list route strings. It is to determine which routes and protocol details are genuinely observed, which are independently corroborated, which were later corrected, which vary by device/firmware, and which are only historical guesses.

## Evidence labels used here

- **HAR-OBSERVED** — present in a captured solar.siseli.com browser HAR.
- **LIVE-CAPTURED** — documented from a specific browser/network capture with response evidence.
- **LIVE-TESTED** — repository history explicitly records working/failing live-server behavior.
- **INDEPENDENT-IMPLEMENTATION** — implemented by a project that predates or is demonstrably independent of another source.
- **CROSS-CORROBORATED** — supported by multiple materially independent sources.
- **IMPLEMENTED** — present in a functioning client, but independence or live validation is weaker.
- **PERMISSION-DISCOVERED** — exposed in permissions returned by login, but not seen in the original HAR traffic.
- **LEGACY/INVALID** — historical route or behavior later shown to be wrong.
- **UNRESOLVED** — conflicting evidence remains and must be settled against an official client or a controlled live capture.

---

## 1. Executive findings

### 1.1 The public cloud API is much larger than the handful of routes used by Home Assistant clients

A captured Solar of Things web-session HAR, preserved and documented by `vvkor/python-siseli`, contained **405 API requests and 108 unique /apis/ routes**. The current document contains **112 unique routes** because four routes were later added from the permissions returned by `/apis/login/account`, not from the original HAR traffic.

The complete 112-route inventory, methods, observed query/body fields and evidence classification is in `round_02_endpoint_catalog.md`.

### 1.2 Authentication is a two-layer scheme

The evidence strongly supports two distinct mechanisms:

1. **IOT Open signing**, required at least for account login and possibly for some other unauthenticated/open-platform calls.
2. **Session access tokens**, returned by login and sent afterward as `IOT-Token` for normal authenticated API traffic.

Login uses `POST /apis/login/account`. Multiple independent clients hash the plaintext password with MD5 before sending it.

### 1.3 The strongest evidence currently favors a Base64 signing preimage

Windear/techfine_cloud (2025), X-c0d3/solar-of-things-hack, Conexo-Casa/solar-of-things-ha, and yuraantonov11/siseli-app independently or semi-independently converge on the following signing construction:

- derive/decrypt the application secret using AES-128-CBC and an MD5-derived key/IV from the application ID;
- compute `IOT-Open-Body-Hash` as a SHA-256 hex digest of the serialized request body for signed POSTs;
- build the signing fields in sorted-key order;
- serialize as `key=value&key=value...`;
- Base64-encode that serialized string;
- HMAC-SHA256 the Base64 text with the decrypted application secret;
- MD5 the raw HMAC bytes and encode lowercase hexadecimal as `IOT-Open-Sign`.

A later `vvkor/python-siseli` implementation instead hex-encodes the UTF-8 signing string before HMAC and also merges URL query parameters into the signing input. It has deterministic unit tests, but the repository does not show those vectors being validated against a live Solar of Things server. Because the Base64 construction is present in the oldest source found in this round (2025) and in several later independent clients, **Base64 is currently higher-confidence**. The conflict remains explicitly UNRESOLVED until the current official web bundle is examined.

No production application secret, decrypted secret, user token, cookie, password, or third-party account/device identifier is stored in this research repository.

### 1.4 Token refresh exists, but implementations evolved unevenly

Login responses can contain `accessToken`, `refreshToken`, and expiry timestamps. Conexo later corrected its refresh route to `POST /apis/login/refresh/access/token`, with body `{ refreshToken }`.

Older clients instead re-login when access tokens expire. Windear/techfine_cloud records API code `9` with message `Token expired`; 0leg7/ha-siseli-solar used browser automation to obtain a fresh token periodically. `python-siseli` parses a refresh token but does not implement refresh in its current authentication module.

### 1.5 Device telemetry is schema-driven and model/firmware dependent

There is no safe universal assumption that a field name, unit, sign convention, or endpoint payload is identical across all SiSeLi devices. Public issue captures show different inverter families using different field names, different units, different signs for grid power, and different supported setting keys.

The API exposes metadata routes such as `gatherAttributes/v1` and device/gather-protocol information. A robust client should prefer server-returned attribute metadata and explicit per-device capability discovery over hard-coded assumptions.

### 1.6 The cloud control surface is real and broader than the early HA integrations suggested

Observed remote-configuration functionality includes individual config reads/writes, cached config retrieval, asynchronous batch reads, write-history records, cache clearing, fast-state support checks, and DTU restart. This round cataloged these routes but **did not execute any mutating request**.

---

## 2. Repository/source lineage

| Source | First public activity observed | Independence / role | Main value |
|---|---:|---|---|
| `Windear/techfine_cloud` | 2025-11-25 | Early independent implementation | Login signing, MD5 password, token expiry code, DTU→device lookup, latest-state route |
| `Hyllesen/solar-of-things-solar-usage` | 2026-01-31 | Independent data client, predates Conexo | History endpoint, monthly overview endpoint, pagination/chunking behavior |
| `Conexo-Casa/solar-of-things-ha` | 2026-03 (history imported from Feb) | Major evolving integration | Auth corrections, refresh, discovery, telemetry fallbacks, controls, device-specific issue evidence |
| `cchampsan/SiseliSolarApp-HA` | 2026-03-05 | Independent project shell | Early statement that signature/nonce security exists; little source implementation |
| `yuraantonov11/siseli-app` | 2026-04 | Large independent Flutter client | Current telemetry, history, monthly aggregation, remote config batch read/write, signing |
| `0leg7/ha-siseli-solar` | 2026-04-08 | Historical implementation | Puppeteer login/token extraction; older token-management model |
| `yuraantonov11/ha-smart-inverter` | 2026-06 | Related later client | Async config read/write and token-expiry handling |
| `X-c0d3/solar-of-things-hack` | 2026-06-21 | Independent portal-JS reverse engineering | App-ID/secret discovery method, signing implementation, device-details route |
| `vvkor/python-siseli` | 2026-07-21 | HAR-driven SDK | 108-route captured API baseline; later 112-route catalog; typed SDK subset |
| `vvkor/ha-siseli` | 2026-07 | Companion integration | Open-auth adoption history and auth failure evidence |
| `aliwadah/solar-ac-monitor` | 2026-09 | Later small client | Operational example; largely corroborative rather than independent discovery |
| `lujian1324-spec/energy-app` | active 2026-09 | Current live browser capture | Fresh 2026-09 history request/response structure and extra overview route |

Forks/copies of Conexo were not counted as independent corroboration unless they contained materially divergent evidence.

---

## 3. Base URL and response conventions

Primary cloud base URL:

`https://solar.siseli.com`

The dominant JSON envelope observed in the HAR and clients is:

```json
{
  "code": 0,
  "message": "Success",
  "localMessage": "Success",
  "data": {}
}
```

Common conventions:

- success is normally API `code = 0`, even when HTTP is 200 for application-level errors;
- normal authenticated calls use `IOT-Token`;
- `IOT-Time-Zone` is important and commonly set to an IANA timezone name;
- pagination commonly uses `page` and `count`;
- time ranges commonly use `fromTime`, `toTime`, `createdFromTime`, `createdToTime`, or `time`;
- sorting flags often use `orderBy...Asc`, `orderBy...Desc`, `timeAsc`, or `asc`;
- report/download endpoints may return raw/binary data rather than the normal JSON envelope.

---

## 4. Authentication in detail

### 4.1 Account login

**Endpoint:** `POST /apis/login/account`

Strongly corroborated request body:

```json
{
  "account": "<account>",
  "password": "<md5-of-plaintext-password>"
}
```

Evidence:

- Windear/techfine_cloud (2025) sends compact JSON and MD5-lowercase password.
- Conexo v2.3.1 records that plaintext password returned code 7 while MD5 worked with the production application identity.
- X-c0d3 hashes password with MD5.
- yuraantonov11/siseli-app hashes plaintext unless already given a 32-character hash.
- python-siseli hashes plaintext with MD5.

Windear also contains account normalization for an 11-digit Chinese mobile number by prefixing `86-`. This is an implementation observation, not yet a general account-format specification.

### 4.2 Signed login headers

High-confidence header names:

- `IOT-Open-AppID`
- `IOT-Open-Nonce`
- `IOT-Open-Body-Hash`
- `IOT-Open-Sign`
- commonly `IOT-Time-Zone`

Conexo additionally mimics portal `Origin` and `Referer`; whether those are server-required is not established.

### 4.3 Application-secret decryption

Multiple sources agree on the structural algorithm:

1. MD5 the application ID to lowercase hex.
2. First 16 hex characters, interpreted as ASCII bytes, become the AES-128 key.
3. Last 16 hex characters, interpreted as ASCII bytes, become the CBC IV.
4. Base64-decode the embedded encrypted application-secret blob.
5. AES-128-CBC decrypt.
6. Remove zero/null padding.

The actual production values are intentionally omitted from this research repository.

### 4.4 Signing-preimage disagreement

**Higher-confidence Base64 family:** Windear, X-c0d3, Conexo, yuraantonov11/siseli-app.

Base64-family pseudocode:

```text
bodyHash = SHA256(serializedBody).hex
fields = {IOT-Open-AppID, IOT-Open-Body-Hash, IOT-Open-Nonce}
canonical = sortByKey(fields).join('&', 'key=value')
preimage = Base64(UTF8(canonical))
hmacRaw = HMAC-SHA256(secret, UTF8(preimage))
sign = MD5(hmacRaw).hexLower
```

**Conflicting later implementation:** vvkor/python-siseli.

It uses a canonical string containing URL query parameters plus the open-auth fields, UTF-8→hex encodes that string, and HMACs the hexadecimal text. Its tests prove internal consistency, but not live acceptance. The associated PR says the algorithm mirrors the official CryptoJS client, yet no captured request/signature pair is preserved there.

**Decision for this round:** do not collapse the two algorithms into one. Mark Base64 as higher-confidence and defer definitive resolution to the official production web-JavaScript round.

### 4.5 GET body-hash disagreement

Another smaller contradiction exists:

- python-siseli signs GET with an empty `IOT-Open-Body-Hash`;
- yuraantonov11/siseli-app computes SHA-256 of `{}` when method is GET or body is absent;
- older working clients often do not generate fresh open-sign headers for ordinary token-authenticated GET calls at all.

This suggests the open-signing layer may matter primarily for login/open endpoints rather than every token-authenticated request, or that different client generations behave differently. Official-client analysis is required.

---

## 5. Token lifecycle

### Login response

Observed token-related fields include:

- `accessToken`
- `accessTokenWillExpiredAt`
- `refreshToken`
- `refreshTokenWillExpiredAt`
- `authId`
- account/user metadata.

### Authenticated header

`IOT-Token: <access token>`

In the captured HAR documented by python-siseli, `IOT-Token` appeared on 372 post-login requests.

### Refresh

**Endpoint:** `POST /apis/login/refresh/access/token`

Conexo current body:

```json
{
  "refreshToken": "<refresh-token>"
}
```

Conexo history is especially useful here: an earlier route omitted `/apis/` and produced a live 404; v2.4.1 corrected it to the current path.

### Expiration / reauthentication

Windear records application code `9` with message `Token expired`. Some clients also treat HTTP 401/403 as token rejection. Older clients simply perform a fresh login rather than refresh.

0leg7/ha-siseli-solar used headless Chromium against the real web portal and refreshed its locally stored token on an approximately 110-minute schedule. That interval should be treated as client policy, not a confirmed server lifetime.

---

## 6. Account, station, DTU and device hierarchy

Evidence supports a hierarchy roughly of:

`account/user → station → DTU/logger → device/inverter`

but not every deployment exposes all relationships identically.

### Station discovery

**POST `/apis/station/list`**

Observed filters include page/count, state, station type, grid type, name and multiple sort flags. The response is paginated.

### Device discovery

**POST `/apis/device/list`**

Can be filtered by `stationId` and numerous device/DTU/model/state fields. Conexo uses this route for automatic inverter discovery under a station.

### Device details

**GET `/apis/device/details?deviceId=<id>`**

Independent X-c0d3 documentation uses this response to recover a station ID from a known device ID.

### DTU-to-device lookup outside the July HAR

Windear/techfine_cloud uses:

**GET `/apis/device/dtu/info?dtuDtuid=<logger-id>`**

and reads `data.devicesAlreadyAdded[]` to obtain associated device IDs. This route is strong IMPLEMENTED evidence but only one independent implementation was located in this round.

---

## 7. Real-time device telemetry

### 7.1 Latest-state endpoint

**GET `/apis/deviceState/simple/state/latest/v1?deviceId=<id>&dataSource=1`**

This is one of the strongest routes in the corpus: it is present in the HAR and implemented independently by X-c0d3, 0leg7, Windear, aliwadah and later clients.

Commonly observed state fields across devices include variants of:

- grid voltage/frequency;
- AC output voltage/frequency/active power;
- battery voltage;
- battery capacity / state of charge;
- charge and discharge current;
- PV input voltage/current/power;
- generation power;
- inverter/transformer temperature;
- bus voltage;
- working/charging mode;
- source-priority/configuration status.

The exact key names vary by model.

### 7.2 Energy-flow endpoint

**GET `/apis/deviceState/simple/energy/flow/v1?deviceId=<id>&dataSource=<n>`**

Observed in the HAR, Conexo, X-c0d3 and fresh 2026-09 browser evidence.

Important variability:

- most current examples use `dataSource=1`; X-c0d3 used `dataSource=2` on its hardware;
- Conexo commonly parses `data.deviceAttributeState.fields`;
- missing portal-side energy-flow configuration can return code `70132` (`Energy flow rule not exists`).

### 7.3 Attribute catalog

**GET `/apis/deviceState/simple/gatherAttributes/v1?deviceId=<id>&category=<n>&renderIn=<n>`**

Fresh 2026-09 capture shows the response can be a list containing metadata such as:

`{ key, valueType, name, unit }`

A captured device exposed 37 entries. This endpoint is strategically important for building a model-adaptive client.

### 7.4 Attribute grouping

**GET `/apis/device/query/attribute/group`** with category/device/render parameters is also HAR-observed and can expose grouping/render metadata.

---

## 8. Historical telemetry

### 8.1 Selected-key history

**POST `/apis/deviceState/simple/attribute/keys/history/v1`**

Corroboration is exceptionally strong:

- present in the 2026-07 HAR;
- used by Hyllesen from 2026-01;
- used by yuraantonov11/siseli-app;
- captured live again from the production portal on 2026-09-24/25 by the Energy App project.

Observed body:

```json
{
  "deviceId": "<id>",
  "keys": ["fieldA", "fieldB"],
  "fromTime": "<ISO-8601 with local offset>",
  "toTime": "<ISO-8601 with local offset>",
  "page": 1,
  "count": 1500,
  "orderByTimeAsc": true
}
```

Fresh live response structure:

```text
data.page
data.count
data.total
data.payload.timeSeries[]
data.payload.fields.<key>[]
data.payload.formatters
data.payload.fieldInfo
```

`timeSeries[i]` aligns with every requested field array at index `i`; null means the key was absent in that report frame.

The fresh capture verified one timestamp/value against the browser chart tooltip. That is unusually strong semantic evidence.

### Pagination / density behavior

Hyllesen's client found that oversized requests can silently lose/downsample data and uses date chunking. Its README describes a 2000-record ceiling and recommends four-day chunks; yura uses roughly five-day chunks around 1500 records for 5-minute telemetry. Treat these as empirical client findings rather than an official hard limit.

The meaning of `data.total` in fresh history responses is not fully proven; one downstream client interprets it as pages but explicitly marks that assumption unverified.

### 8.2 Full record history

**POST `/apis/deviceState/simple/attribute/record/list/v1`** is HAR-observed and implemented by python-siseli.

A path variant without `/simple/.../v1` appears in downstream documentation as an older fallback, but it was not independently validated during this round.

---

## 9. Energy/statistical aggregation

### Station overview

A particularly useful endpoint is:

**POST `/apis/stationOverView/stateAttributeSummary/category/yearly`**

with query parameters such as `stationId` and `summaryCategoryKey`, and body containing a year/time selector.

Hyllesen independently used `summaryCategoryKey=pvInverterElectricityQuantityClass` before Conexo's later implementation, making this route cross-corroborated.

Observed property names in summary data include:

- `pvGeneratedEnergy`;
- `chargeElectricityQuantity`;
- `dischargeElectricityQuantity`;
- `consumeElectricityQuantity`;
- `buyElectricityQuantity`;
- `sellElectricityQuantity`.

The HAR also contains daily/monthly/yearly overview families at device, station and owner scopes, plus station income series.

### Fresh extra device-overview route

A live production browser capture on 2026-09-24/25 found:

**POST `/apis/deviceOverView/pvInverterPowerClass/daily/detail?deviceId=<id>`**

This route was not in the July 108-route HAR baseline and is therefore valuable evidence that the API surface changes or that the earlier HAR did not exercise every page.

---

## 10. Remote configuration and control

### Correct current families

Observed routes include:

- `POST /apis/remote/device/config/read?deviceId=<id>` — single config read, body includes `id` and `key`;
- `POST /apis/remote/device/config/write?deviceId=<id>` — config write;
- `POST /apis/remote/device/configs/cache/get?deviceId=<id>` — cached configuration map;
- `POST /apis/remote/device/configs/read?deviceId=<id>` — begin asynchronous batch read;
- `GET /apis/remote/device/configs/read/details?batchReadId=<id>` — poll batch-read completion/details;
- `POST /apis/remote/device/config/write/records` — write-operation history;
- `POST /apis/remote/device/configs/cache/clear?deviceId=<id>` — clear cache;
- `GET /apis/remote/device/state/report/fast/supported?deviceId=<id>` — capability check;
- `POST /apis/remote/dtu/restart?dtuId=<id>` — remote DTU restart.

Some of these were present in the HAR; four were only discovered later in login permissions. See the endpoint appendix for exact provenance.

### Config-write payload discrepancy

Different clients send closely related but not identical bodies:

- Conexo: `{ deviceId, key, value }`;
- python-siseli: `{ id, key, value }` with optional config ID;
- yuraantonov11/siseli-app: `{ id: deviceId, key, value }`.

The official portal/client round needs to establish whether `id`, `deviceId`, or both are canonical and whether the body differs for particular device families.

### Known setting keys and model aliases

Cross-repository issue captures show examples including:

- `outputSourcePrioritySetting`: commonly 0=USO, 1=SUB, 2=SBU;
- FCHAO alias `setOutputSourcePriority`;
- `chargerSourcePrioritySetting`: commonly 0=CSO, 1=SNU, 2=OSO;
- FCHAO typo/alias `chargeSourcePrioirty`;
- `acInputRangeSetting`: commonly Appliance/UPS modes;
- `batteryPowerLimitingSetting`: used by one integration as a grid-feed-in control;
- numerous model-specific charge voltage/current, battery-type, buzzer, restart, output voltage/frequency, low-battery and fault-record keys.

Generic-looking keys such as `batteryChargeLimit`, `batteryDischargeLimit`, and `gridChargeLimit` are **not universal**; real devices have returned code `70134` when those keys are absent.

### Safety note

All write/restart/cache-clear routes were cataloged from code/HAR/history only. This research round did not send control, write, restart, firmware, account-change, or other mutating requests.

---

## 11. Device/firmware field variability

Public issues provide strong evidence that a correct client must be model-aware.

Examples:

- HPVINV02-family devices may expose `pvPower` instead of `pvInputPower`, `outputActivePower` instead of `acOutputActivePower`, and `batteryCapacity` instead of `batterySOC`.
- Energy-flow payloads may expose PV as `pv1Power`..`pv4Power`, battery voltage as `bmsBatteryVoltage` or terminal-voltage fields, and SOC as `batteryPercentage` or `bmsSOC`.
- Some three-phase/grid payloads use signed `aPhaseMainsPower` style values where negative indicates import and positive export.
- PowMr MEGA-ECO machineType 5 has been observed using `GridPower` with the opposite sign convention: negative export, positive import.
- On the same MEGA-ECO evidence, `acOutputActivePower` is W while PV/generation fields can be kW.
- `positiveTerminalBatteryCurrent` has been observed negative during charging and positive during discharge.
- A current Maniy 11 kW / dual-MPPT case exposes a substantially different set of flow/anti-backflow fields and remains incompletely mapped.

Therefore:

**Do not infer unit or sign solely from a field name.** Prefer metadata (`unit`, attribute definitions), machine type, gather protocol/version, and device-specific validation.

---

## 12. Error codes observed or reported

| Code / status | Evidence / interpretation | Confidence |
|---|---|---|
| `0` | success | very high |
| `7` | password/auth failure when plaintext password was tried in Conexo's live debugging | high |
| `9` | `Token expired` in Windear client; also handled by later clients | high |
| `36` | `IOT-Open-AppID missing` reported in vvkor auth-development history | medium |
| `44` | signing error referenced in Conexo auth debugging/changelog | medium-high |
| `20007` | account error observed by Conexo when using test environment/application identity | high for that context |
| `20101` | `Illegal argument`; observed for invalid timezone/argument formatting | high |
| `70132` | `Energy flow rule not exists` | high |
| `70134` | `Config attribute not exists` | high |
| `71301` | observed in HAR for a config-read attempt; precise semantic meaning unresolved | medium |
| HTTP 401/403 | treated as auth/token rejection by multiple clients | normal HTTP behavior / implementation evidence |

0leg7 also treats API codes 30–35 as token-invalid conditions, but this round did not find independent evidence for those mappings; they remain IMPLEMENTATION ASSUMPTIONS.

---

## 13. Conexo chronology: why old code must not be treated as documentation

Conexo's history is a useful record of reverse-engineering corrections:

- **v2.2.0 (2026-03-06):** introduced credential login but used guessed/missing route prefixes and incomplete signing assumptions.
- **v2.3.0 (2026-03-07):** corrected login to `/apis/login/account` and added IOT-Open signing; a test environment/application identity could produce a valid signature but not authenticate production accounts.
- **v2.3.1:** moved to production application identity and MD5 password handling; plaintext password behavior was shown to be wrong.
- **v2.3.3:** replaced nonexistent settings routes with remote-config routes observed from the real portal.
- **v2.4.1 (2026-05-31):** corrected refresh from `/login/refresh/access/token` to `/apis/login/refresh/access/token` after live 404 failures.

This means old forks, READMEs or copied snippets can contain routes that once looked plausible but were later disproven.

---

## 14. HAR-derived API surface

`vvkor/python-siseli` is the strongest broad-surface artifact found in this round because its API documentation explicitly states that it was reconstructed from a Solar of Things browser HAR.

Original documented capture:

- 405 API requests;
- 108 unique `/apis/` endpoints;
- `IOT-Token` on 372 requests after login;
- 25 functional domains including alarms, dashboard, devices, device state, DTU, station, reports, remote configuration, resources, SIM/SIM-card operations, user/profile and dictionaries.

Important provenance correction:

The repository's current `docs/api.md` still says “108 unique endpoints,” but parsing its current table gives **112 unique routes**. Comparing the original HAR-document commit with current main shows exactly four later additions:

- `/apis/dictionary/data/dtu`;
- `/apis/remote/device/config/write`;
- `/apis/remote/device/config/write/records`;
- `/apis/remote/device/configs/cache/clear`.

The document itself notes these were discovered in permissions returned by `/apis/login/account`. They must therefore be classified separately from HAR-observed traffic.

See `round_02_endpoint_catalog.md` for the full route-level matrix.

---

## 15. Additional routes not in the original July HAR baseline

Strong evidence found in later/other sources:

1. `POST /apis/login/refresh/access/token` — Conexo live-tested correction and current implementation.
2. `GET /apis/device/dtu/info?dtuDtuid=...` — Windear 2025 implementation; maps a logger/DTU to devices.
3. `POST /apis/deviceOverView/pvInverterPowerClass/daily/detail?deviceId=...` — fresh 2026-09 production-browser capture.

These demonstrate why the 108-route HAR should be treated as a large snapshot, not a complete immutable API specification.

---

## 16. Historical / unverified route names

### Explicitly disproven historical Conexo routes

- `/api/device/settings/v1`
- `/api/device/settings/update/v1`

Conexo later recorded that these were nonexistent/404 and replaced them with the `/apis/remote/device/...` configuration family.

### Older documentation/constant-only leads not promoted to confirmed

- `/apis/device/realTime`
- `/apis/device/control`
- `/apis/device/history`

These appear in older yura documentation/constants, but the current `siseli-app` implementation no longer calls them. Current code uses `deviceState/simple/energy/flow/v1`, `state/latest/v1`, selected-key history, and remote-config routes.

---

## 17. Confidence matrix for the most important client-building capabilities

| Capability | Best route/mechanism known now | Confidence |
|---|---|---|
| Account login | `POST /apis/login/account` + MD5 password + signed headers | very high |
| Access-token auth | `IOT-Token` | very high |
| Token refresh | `POST /apis/login/refresh/access/token` | high |
| List stations | `POST /apis/station/list` | very high |
| List devices | `POST /apis/device/list` | very high |
| Device details | `GET /apis/device/details` | very high |
| Latest telemetry | `GET /apis/deviceState/simple/state/latest/v1` | very high |
| Energy-flow telemetry | `GET /apis/deviceState/simple/energy/flow/v1` | very high, device rules vary |
| Attribute metadata | `GET /apis/deviceState/simple/gatherAttributes/v1` | very high |
| Selected-key history | `POST /apis/deviceState/simple/attribute/keys/history/v1` | very high |
| Full record history | `POST /apis/deviceState/simple/attribute/record/list/v1` | high |
| Monthly/yearly station aggregates | `stationOverView/stateAttributeSummary/category/*` | very high |
| Cached config map | `POST /apis/remote/device/configs/cache/get` | very high |
| Async full-config read | `configs/read` + `configs/read/details` | very high |
| Config write | `POST /apis/remote/device/config/write` | very high route confidence; payload/key semantics device-dependent |
| DTU→device lookup | `GET /apis/device/dtu/info` | high but single independent source |
| Exact signing preimage | Base64 family currently favored | high but not final; official-client verification required |

---

## 18. What remains unresolved after Round 02

1. Exact current production JavaScript signing code, especially the Base64-vs-hex contradiction.
2. Whether IOT-Open signing is required on every request or only login/open-platform operations.
3. Correct GET body-hash behavior if GET requests are open-signed.
4. Exact token lifetimes and whether refresh responses rotate the refresh token.
5. Complete semantics of login-returned permissions and whether they enumerate routes available to that user/role.
6. Role/tenant differences: owner, installer, distributor/admin and guest may expose materially different API surfaces.
7. Whether all current 112 catalog routes are still live in production in September 2026.
8. Additional routes added since the July HAR, beyond the three later routes found here.
9. Exact semantics of error 71301 and any undocumented error-code table.
10. Canonical config-write payload (`id` vs `deviceId`) and per-device setting schemas/value domains.
11. Complete firmware/model matrix for field names, units, sign conventions and dataSource selection.
12. Whether older route variants such as non-simple record history remain live aliases.
13. Email/phone/QR/guest authentication endpoints visible in the portal UI but not captured in the account-login-focused client corpus.
14. Firmware upgrade endpoints beyond list/filter/report surfaces, if the user's role exposes them.
15. Rate limits, server-side polling guidance and anti-abuse behavior.

---

## 19. Sources deeply examined

Primary repositories:

- https://github.com/Windear/techfine_cloud
- https://github.com/Hyllesen/solar-of-things-solar-usage
- https://github.com/Conexo-Casa/solar-of-things-ha
- https://github.com/X-c0d3/solar-of-things-hack
- https://github.com/yuraantonov11/siseli-app
- https://github.com/yuraantonov11/ha-smart-inverter
- https://github.com/vvkor/python-siseli
- https://github.com/vvkor/ha-siseli
- https://github.com/0leg7/ha-siseli-solar
- https://github.com/cchampsan/SiseliSolarApp-HA
- https://github.com/aliwadah/solar-ac-monitor
- https://github.com/lujian1324-spec/energy-app

Relevant source history included commits, releases, issues, pull requests, tests and current source rather than relying only on README summaries.

---

## 20. Round-close assessment and next-round decision

Round 02 is complete for repository-based cloud REST archaeology.

The next planned work should **remain separate** and should be:

### Round 03 — Official production web-application archaeology

Why it should not be merged with Android or with the alternate test/doc environments:

- Round 02 leaves a concrete signing contradiction that the current production JavaScript is the best source to settle.
- We now have a 112-route baseline plus later-route candidates, which gives a precise search dictionary for minified bundles.
- Production-web analysis can independently validate route names, HTTP methods, auth helpers, application-signing behavior, field constants, role/permission handling and currently shipped UI features.
- Mixing Android analysis into the same round would make it harder to distinguish cloud-web behavior from mobile-only/local/BLE behavior.
- Mixing `test.solar`, `doc.solar` and `demo.doc.solar` immediately into production analysis could blur environment differences; those can be compared in a dedicated environment-diff round after the production bundle is mapped.

Round 03 should therefore focus only on the current `solar.siseli.com` production web application and its loaded JavaScript/assets, with passive/static retrieval only unless a later step explicitly requires the user's own authenticated capture.

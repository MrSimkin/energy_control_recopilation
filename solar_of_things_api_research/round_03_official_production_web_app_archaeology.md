# Round 03 — Official Production Web-Application Archaeology

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE WITH EXPLICIT STATIC-ASSET RETRIEVAL LIMITATION

Companion: `round_03_protocol_delta_matrix.md`

## Purpose

This round investigates the current production Solar of Things web application at `https://solar.siseli.com`, using the Round 02 endpoint/auth vocabulary as a search dictionary. The aim is to cross-check third-party REST archaeology against the official production web client, resolve authentication/signing contradictions where evidence permits, and identify current web-client behavior that changes the protocol model.

This round is intentionally separate from Android package archaeology and from `test.solar`, `doc.solar`, or `demo.doc.solar` environment comparison.

## Evidence classes

- **DIRECT-PRODUCTION** — directly observed from the current production site during this round.
- **FRESH-PRODUCTION-CAPTURE** — current production browser/DevTools request or source snippet captured and preserved by another researcher at the time of this round.
- **HISTORICAL-OFFICIAL-BUNDLE** — behavior reverse-engineered from an older official production Umi bundle and preserved in source/commit history.
- **LIVE-PRODUCTION-INTEROP** — third-party code explicitly tested against the live production backend with recorded outcomes.
- **CURRENT-CLIENT** — contemporary third-party implementation using production, useful but not equivalent to official-client source.
- **DOC-ENV** — information sourced from the platform's separate OpenAPI/documentation environment; not promoted to production-web fact here unless independently corroborated.
- **INFERENCE** — conclusion drawn from multiple observations; stated as such.

---

## 1. Direct current-production observations

### 1.1 The production site is a JavaScript-only SPA

A direct fetch of `https://solar.siseli.com/` on 2026-09-24 returned only the application shell message:

`Sorry, we need js to run correctly!`

This confirms the production surface remains a JavaScript SPA rather than a server-rendered application.

### 1.2 Direct retrieval of the current hashed JavaScript asset was not possible in this environment

The available web fetcher exposes the SPA shell but not its script-tag asset inventory, and direct retrieval of candidate hashed Umi assets was unavailable. The container network also could not independently fetch the host.

Therefore this report does **not** claim that the current bundle bytes were downloaded directly by this investigation.

That limitation matters because it constrains what can be called DIRECT-PRODUCTION. Current-bundle facts below come from a fresh production DevTools capture preserved in a public repository, and historical signing internals come from older official Umi-bundle reverse engineering with live-server verification.

---

## 2. Current production bundle fingerprint

A fresh browser capture from the production Solar of Things console, made during this research window and committed on 2026-09-24 UTC / 2026-09-25 in Asia/Taipei, identifies the active front-end source bundle as:

`umi.6d0aa871.js`

The same capture identifies the framework/request layer as UmiJS + `umi-request`.

This is consistent with older production reverse engineering:

- X-c0d3 documented an earlier randomly hashed production asset such as `umi.0dddcf2d.js` and instructed inspection through Chrome DevTools.
- Conexo's March 2026 auth reconstruction explicitly refers to the portal `umi.js` bundle.

**Conclusion:** the production application uses hashed Umi bundle filenames that change across deployments. A hard-coded bundle filename is therefore not a stable discovery mechanism.

---

## 3. Fresh production browser capture: Device details → Data Analysis

The strongest current official-client evidence in this round is a fresh DevTools capture of the production page **Device details → Data Analysis**.

### 3.1 Current history request

Observed endpoint:

`POST /apis/deviceState/simple/attribute/keys/history/v1`

Preserved front-end source fragment:

```text
request(`${base}/deviceState/simple/attribute/keys/history/v1`,
        { method: "POST", headers: { "IOT-Token": getToken() }, body: payload })
```

Observed request body includes:

```json
{
  "deviceId": "<device-id>",
  "keys": ["remainingBatteryCapacity", "batteryCurrent"],
  "fromTime": "<local-day-start-with-explicit-offset>",
  "toTime": "<local-day-end-with-explicit-offset>",
  "page": 1,
  "count": 1500,
  "orderByTimeAsc": true
}
```

Observed request headers documented in the capture include:

- `IOT-Token`;
- `IOT-Time-Zone`;
- `Accept-Language`;
- `Content-Type`;
- `Accept`.

The captured example used an IANA time zone and explicit local UTC offset in the request times.

### 3.2 Current history response

The response is columnar rather than a list of timestamp/value tuples:

```text
data.page
data.count
data.total
data.payload.timeSeries[]
data.payload.fields.<requested-key>[]
data.payload.formatters
data.payload.fieldInfo
```

`timeSeries[i]` aligns with every requested field array at index `i`. A `null` value means that the report frame did not contain that attribute.

The capture independently checked one timestamp/SOC value against the production chart tooltip, providing semantic validation rather than route-name evidence alone.

### 3.3 Current attribute metadata

Also observed on the production device page:

`GET /apis/deviceState/simple/gatherAttributes/v1?deviceId=<id>&category=1&renderIn=2`

The captured device returned 37 attribute descriptions containing fields such as:

`key`, `valueType`, `name`, `unit`

This strengthens Round 02's conclusion that a robust client should build its schema from server-provided metadata rather than rely exclusively on fixed field dictionaries.

### 3.4 Other endpoints observed on the same current page

- `GET /apis/deviceState/simple/energy/flow/v1?deviceId=<id>&dataSource=1`
- `POST /apis/deviceOverView/pvInverterPowerClass/daily/detail?deviceId=<id>`
- `POST /apis/deviceOverView/generatedEnergy/daily?deviceId=<id>`
- `POST /apis/deviceOverView/stateAttributeSummary/category/total?deviceId=<id>&summaryCategoryKey=<key>`

The `pvInverterPowerClass/daily/detail` route was absent from the original July 108-route HAR baseline, so this is direct evidence that the earlier HAR was a snapshot rather than a complete or immutable API specification.

---

## 4. IOT Open signing: Round 02 conflict substantially resolved

Round 02 preserved a contradiction:

- several clients and historical portal-bundle reconstructions used **Base64** of the canonical parameter string before HMAC;
- `vvkor/python-siseli` implemented a **hex-encoded UTF-8** preimage.

Round 03 adds enough evidence to materially resolve this.

### 4.1 Historical official-bundle reconstruction

Conexo commit `88a26e36b0842684f1f86bf9d4c8ce9027526dad` (2026-03-07) states that the complete signing algorithm was reverse-engineered from the portal Umi bundle and live-tested.

Its preserved interpretation of the official code is:

1. derive/decrypt the embedded application secret with AES-128-CBC;
2. compute a SHA-256 body hash;
3. canonicalize signing fields in sorted key order as `key=value&key=value...`;
4. Base64-encode the canonical UTF-8 string;
5. HMAC-SHA256 the Base64 text using the decrypted application secret;
6. MD5 the raw HMAC bytes to lowercase hexadecimal.

The implementation comments identify the portal functions as `qe()` for secret decryption and `Ye()` for signature generation.

Conexo then live-tested the signature. Before the fix the server returned signing error `44`; after implementing the Base64 pipeline the request progressed to credential-level error `20007` on the test environment. That establishes server acceptance of the signing construction independently of credential validity.

X-c0d3 independently reconstructed the same Base64 construction from a production Umi bundle later in 2026.

### 4.2 Production interoperability in a newer client

A separate contemporary client (`lujian1324-spec/energy-app`) uses the following signing algorithm against production:

```text
bodyHash = GET ? "" : SHA256(body).hexLower
params = URL query parameters +
         IOT-Open-AppID + IOT-Open-Nonce + IOT-Open-Body-Hash
canonical = sort(params by key).join('&', 'key=value')
preimage = Base64(UTF8(canonical))
hmacRaw = HMAC-SHA256(secret, preimage)
signature = MD5(hmacRaw).hexLower
```

Its May 2026 history records live production outcomes:

- after correcting signing/application identity, the server stopped returning signing error `44` and instead returned password error `7`;
- after adding MD5 password handling, `POST /login/account` returned `code 0` with access and refresh tokens;
- authenticated device-list and device-state calls then succeeded normally.

This is strong **LIVE-PRODUCTION-INTEROP** evidence for the Base64 construction.

### 4.3 Round 03 conclusion on Base64 vs hex

**The Base64 construction is now the high-confidence protocol model.**

The hex-preimage implementation in `vvkor/python-siseli` remains useful evidence for many endpoints, but its signing variant has deterministic local unit tests rather than a preserved live-server validation. In light of older official-bundle reconstruction plus independent production interoperability, the hex interpretation should be treated as likely incorrect or at least unproven.

Definitive byte-for-byte confirmation from the current `umi.6d0aa871.js` would still be preferable, so this is recorded as **substantially resolved**, not metaphysically proven.

---

## 5. Query parameters: apparent disagreement explained

Round 02 also noted that some signing implementations canonicalized only the three Open-signing fields while others included URL query parameters.

These implementations were not testing equivalent requests.

- Conexo's reverse-engineered signer was used for `/login/account`, which has no URL query parameters.
- X-c0d3 likewise primarily open-signed the queryless login request.
- The contemporary generic signer includes all URL query parameters before adding the three Open-signing fields, sorting and Base64-encoding the result.

Therefore the older three-field login signer is **not evidence that query parameters are excluded globally**. It only proves that no query parameters had to be included for that login request.

Current best protocol model:

**When an Open-signed request has URL query parameters, include their decoded/raw values in the canonical parameter map before sorting.**

This remains LIVE-PRODUCTION-INTEROP rather than directly extracted from the current official bundle.

---

## 6. Body hashing and serialization

Current best-supported generic behavior:

- GET: `IOT-Open-Body-Hash` is the empty string;
- non-GET: SHA-256 lowercase hexadecimal of the exact serialized request-body bytes/text;
- JSON bodies should be compact/deterministic because the hash covers the transmitted representation.

A later third-party implementation that used SHA-256 of `{}` for GET is weaker evidence and should not be treated as canonical.

The current official Data Analysis capture does not establish GET open-sign behavior because it is a token-authenticated page request, not necessarily an Open-signed operation.

---

## 7. Signing scope: Open signature and session token are separate layers

Round 02 described the platform as a two-layer scheme. Round 03 strengthens that distinction.

### 7.1 Login

`POST /apis/login/account` is strongly evidenced as requiring IOT Open signing and **not** requiring an existing `IOT-Token`.

Password authentication uses:

`MD5(plaintext-password).hexLower`

Plaintext password has repeatedly produced application error `7` in live testing.

### 7.2 Post-login official web traffic

The fresh production source fragment for historical telemetry explicitly supplies `IOT-Token` at the call site. The captured request documentation lists normal session/time-zone/content headers, not Open-sign fields.

Historical official-bundle archaeology independently found that remote-config endpoints were called with a plain `IOT-Token` header and no Open signing.

Therefore:

**Open signing is demonstrably required for login/open-platform operations, while `IOT-Token` is the core post-login session mechanism. Open signing is not proven necessary for every authenticated endpoint.**

A third-party client that signs every request demonstrates that the server accepts those signatures; it does not prove that the official client or server requires them for every token-authenticated route.

Global Umi request middleware in the current production bundle could still add headers not visible in the preserved call-site snippet, so the exact current per-route signing policy remains partially unresolved.

---

## 8. Token lifecycle: important new production behavior

Round 03 materially improves the refresh-token model using contemporary live-production experiments.

### 8.1 Refresh route

`POST /apis/login/refresh/access/token`

The route itself is now high confidence.

### 8.2 Current production-tested refresh payload

A contemporary client records that the backend requires the **current access token and current refresh token as a pair**:

```json
{
  "accessToken": "<current-access-token>",
  "refreshToken": "<current-refresh-token>"
}
```

Its current server-side implementation states that a lone refresh token was rejected as `illegal argument`.

This conflicts with Conexo's older/current integration code, which sends only `refreshToken`. Possible explanations include backend evolution, multiple accepted application contracts at different times, or incomplete validation in that integration.

**Round 03 current-preference:** send the access+refresh pair and treat refresh-token-only behavior as historical/unresolved.

### 8.3 Refresh tokens rotate

A July 2026 controlled live probe found:

- successful refresh returns a new token pair;
- a refresh token is single-use/rotating;
- using a token pair changes/invalidate the prior session pair;
- one account can nevertheless have multiple independent concurrent login sessions.

This is operationally significant. A client must persist refresh results atomically and must never assume that an old refresh token can be reused indefinitely.

Two components that need independent background access should use **independent login sessions**, not share one rotating refresh-token chain.

### 8.4 Access-token lifetime

Login/refresh responses expose `accessTokenWillExpiredInMillis` and/or absolute expiry fields. Contemporary live-client code describes the observed access lifetime as approximately two hours and uses two hours only as a fallback when the server omits the value.

**Implementation rule:** trust the server-provided expiry value; do not hard-code a universal two-hour lifetime.

---

## 9. Current login modes visible through production-web comparison

Recent September 2026 comparison work against the official web login page adds useful behavior beyond account/password login.

### Account/password

High confidence:

- endpoint: `/login/account` under the `/apis` base;
- payload uses `account` + MD5 password;
- successful response yields access/refresh session material.

### Email-code login

A 2026-09-10 production-page comparison records that the **official web login uses captcha intent 6 for email login**, not 3.

Contemporary client route model:

- send code: `/user/send/email/captcha`;
- request field for the destination is `address`;
- `intent` is sent as a number;
- login: `/login/email` with `email`, `captchaId`, `verifyCode`.

The intent value `6` is specifically tied to official-web observation. The exact route/payload implementation is strongly supported by current client/doc/live work but was not directly extracted from the current production bundle bytes in this round.

### SMS-code login

The same current comparison records that the **official web uses intent 5 for SMS login**.

Contemporary route model uses `/user/send/sms/captcha` followed by `/login/sms`.

Again, the official-web observation is strongest for the intent value; exact payload fields remain CURRENT-CLIENT/DOC-ENV evidence unless captured directly in a future production session.

---

## 10. Current API/client architecture inferred from the production web surface

### Base

The production application communicates with the same-origin API under:

`https://solar.siseli.com/apis`

### Response envelope

The familiar envelope remains:

`{ code, message, localMessage, data }`

with `code = 0` as success in the captured current history call.

### Session

`IOT-Token` is the session header used by post-login official page calls.

### Time zone

`IOT-Time-Zone` and explicit local offsets in date-time bodies are not decorative. Real clients have received `20101 Illegal argument` from malformed/mismatched time-zone/time-range requests.

### Large identifiers

Contemporary live integration work shows that station/user/device identifiers can exceed JavaScript's `Number.MAX_SAFE_INTEGER` when the backend emits them as bare JSON numbers. Rounding an identifier and sending it back can produce `20101 Illegal argument` or address the wrong resource.

**Implementation rule:** parse platform IDs losslessly and preserve them as strings end-to-end.

---

## 11. What the fresh production capture confirms from Round 02

| Round 02 item | Round 03 status |
|---|---|
| `/deviceState/simple/attribute/keys/history/v1` | CONFIRMED CURRENT by fresh production capture |
| Columnar `timeSeries` + aligned `fields` arrays | CONFIRMED CURRENT and tooltip-validated |
| `count=1500` usable for one-day history | CONFIRMED CURRENT portal behavior in captured page |
| `gatherAttributes/v1` metadata | CONFIRMED CURRENT; 37 entries on captured device |
| `energy/flow/v1` with `dataSource=1` | CONFIRMED CURRENT on captured page |
| `deviceOverView/generatedEnergy/daily` | CONFIRMED CURRENT on captured page |
| `stateAttributeSummary/category/total` | CONFIRMED CURRENT on captured page |
| `pvInverterPowerClass/daily/detail` | NEW CURRENT route absent from original July HAR |
| `IOT-Token` post-login auth | CONFIRMED CURRENT |
| Time-zone sensitivity | STRENGTHENED |

---

## 12. Round 02 issues now resolved, strengthened, or still open

### Substantially resolved

1. **Base64 vs hex signing preimage** — Base64 now has historical official-bundle evidence plus independent live-production interoperability. Hex is demoted to unverified/likely incorrect.
2. **Why login-only signers omit query parameters** — their target request has none; this does not define generic canonicalization.
3. **Refresh-token mutability** — current live testing shows rotation/single-use semantics.
4. **Concurrent sessions** — multiple independent sessions for the same account are supported in live testing.

### Strengthened but not fully direct-current-bundle proven

1. Generic signing canonicalization should include URL query parameters.
2. GET Open-sign body hash should be empty string.
3. Open signing is not required for at least some authenticated token routes; official web primarily relies on `IOT-Token` after login.
4. Refresh should send both access and refresh tokens.

### Still unresolved

1. Byte-for-byte current `umi.6d0aa871.js` auth helper code.
2. Exact current application ID/secret used by the official web client. Multiple public application identities are accepted by production; this report intentionally stores none of their secrets.
3. Exact list of routes for which current production requires Open signing versus accepts token-only auth.
4. Whether refresh-token-only requests still work for some application identities or backend versions.
5. Complete current route inventory beyond pages exercised by the fresh capture.
6. Current production role/permission routing (`owner`, dealer/integrator/admin) without an authenticated multi-role capture.
7. Whether `data.total` in history is definitively total pages, records, or context-dependent.

---

## 13. Security-relevant architecture observations

### Embedded application credentials are not user secrets

Historical production bundles exposed enough material to reconstruct an Open API application secret client-side. Contemporary third-party web clients also note that any application credential embedded in a browser bundle must be treated as recoverable.

That does **not** make user access tokens, refresh tokens, passwords or cookies public. Those remain per-user credentials and were not collected or stored by this research.

### MD5 here is protocol compatibility, not secure password storage

The platform expects the MD5 digest of the plaintext password over HTTPS for account login. MD5 should not be copied into unrelated password-storage designs.

### Rotating refresh tokens require careful storage

Because refresh tokens rotate, concurrent refresh attempts against the same session can race. A production client should serialize refresh operations, atomically replace the token pair, and avoid sharing one refresh chain across independent services.

---

## 14. Minimal high-confidence web/cloud client model after Round 03

Without embedding any platform application credential, the protocol shape is now:

```text
LOGIN
  compactBody = JSON({ account, password: md5(password) })
  bodyHash = sha256(compactBody)
  canonical = sorted(open-sign params [+ URL params if present])
  sign = md5(raw_hmac_sha256(secret, base64(utf8(canonical))))
  POST /apis/login/account
  -> accessToken + refreshToken + expiry metadata

NORMAL SESSION CALL
  IOT-Token: accessToken
  IOT-Time-Zone: <IANA zone when relevant>
  preserve all IDs losslessly

REFRESH
  POST /apis/login/refresh/access/token
  body { accessToken, refreshToken }
  atomically replace the returned token pair

HISTORY
  POST /apis/deviceState/simple/attribute/keys/history/v1
  body { deviceId, keys[], fromTime, toTime, page, count, orderByTimeAsc }
  zip payload.timeSeries[i] with payload.fields[key][i]
```

This is a protocol research model, not a promise that every account/role/device exposes every endpoint.

---

## 15. Sources deeply examined for this round

### Direct production

- `https://solar.siseli.com/` — current JS-only SPA shell, checked 2026-09-24.

### Fresh current-production capture

- `lujian1324-spec/energy-app` — `docs/siseli-api.md`, commit lineage around `b74d8a8c05ac7f534761656a00627e0d130aac1c` and merge `ab392d9de34cf645d01e741345c2a5eeb1a82a02`.

### Historical official-bundle reconstruction

- `Conexo-Casa/solar-of-things-ha` — especially commit `88a26e36b0842684f1f86bf9d4c8ce9027526dad` and later release corrections.
- `X-c0d3/solar-of-things-hack` — production Umi-bundle discovery instructions and signing implementation.

### Live production interoperability / current client experiments

- `lujian1324-spec/energy-app` — signing implementation and live login/device tests (`cb268b428f6774abbbb97e2093781c47f39caaa4`, `7d50b976a5e0039521f9c68db6a7c6c7b1237727`); rotating-token/concurrent-session experiment (`c8a5c76c72a7d350c9578fed2b48ee04d4289ec9`); September official-web login intent comparison (`a263752cb48f3251e9a8407cecf3a49655bdf3e8`).

The platform's separate OpenAPI documentation was inspected only as secondary context and is intentionally not folded into this production-web round as if it were the same evidence source.

---

## 16. Round-close assessment

Round 03 reaches a useful closure despite not directly downloading the current hashed Umi bundle.

The most important Round 02 ambiguity — Base64 versus hex signing — is now substantially resolved by combining:

- historical official-bundle reverse engineering;
- independent production-bundle reverse engineering;
- production server error progression;
- successful current production login/device interoperability.

The current production browser capture also independently validates the most important telemetry/history path and exposes a route absent from the July HAR baseline.

### Next-round decision

Proceed with a **separate Round 04 — Official Android Application Package Archaeology**.

Why Android next rather than immediately merging alternate web environments:

- Android is the other official client and can independently confirm cloud authentication/routes while exposing mobile-only behavior.
- It may contain network constants, service definitions, DTO names and protocol assets that are not reachable from the web SPA.
- BLE/provisioning code discovered there should be recorded as source-of-truth clues, but detailed BLE protocol archaeology should remain its own later round rather than being mixed into Android package inventory.
- `test.solar`, `doc.solar` and `demo.doc.solar` remain useful for a later environment-diff round, but production behavior should first be cross-checked against the second official client.

Round 04 should therefore analyze official Android packages/static assets and historical versions without sending device-changing commands.

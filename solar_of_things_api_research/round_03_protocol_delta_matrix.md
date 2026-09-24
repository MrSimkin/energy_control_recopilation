# Round 03 Companion — Production Web Protocol Delta Matrix

Date: 2026-09-24

Status: COMPLETE

Companion to `round_03_official_production_web_app_archaeology.md`.

This file records only the protocol conclusions that changed, sharpened, or remained unresolved after comparing the Round 02 repository archaeology with current production-web evidence.

## Evidence shorthand

- **DIRECT** — current production page observed directly during Round 03.
- **FRESH CAPTURE** — current production browser/DevTools capture preserved during this research window.
- **OFFICIAL-BUNDLE RE** — reverse-engineered from an official production Umi bundle.
- **LIVE PROD** — explicitly exercised successfully or diagnostically against the production backend.
- **CURRENT CLIENT** — implemented by a contemporary production client but not itself official source.
- **UNRESOLVED** — evidence still conflicts or current official bytes were unavailable.

## Authentication and signing delta

| Question | Round 02 state | Round 03 evidence | Round 03 conclusion | Implementation consequence |
|---|---|---|---|---|
| Signing preimage encoding | Base64 favored; hex implementation conflicted | OFFICIAL-BUNDLE RE independently yields Base64; LIVE PROD client using Base64 progressed from sign error to credential error then successful login | **Base64 high confidence; hex demoted** | Use Base64(UTF-8(canonical string)) before HMAC |
| AES secret derivation | Strong multi-client agreement | Historical official Umi reconstruction names the decrypt helper and matches independent bundle RE | **Strengthened** | MD5(appId) ASCII halves → AES-128-CBC key/IV; zero/null padding |
| URL query params in signature | Conflicting/unclear | Generic LIVE PROD signer includes URL query params; older official-bundle signers were exercised on queryless login | **Apparent conflict explained** | Include query params for generic Open-signed requests |
| GET body hash | Empty vs SHA256("{}") conflict | Contemporary generic signer/API reference uses empty string; weaker client used SHA256("{}") | **Empty string preferred** | For an Open-signed GET, use empty body hash unless current official bundle later proves otherwise |
| Login password | MD5 strongly supported | LIVE PROD login succeeds with MD5; plaintext had produced code 7 | **Confirmed** | Send lowercase MD5 hex of plaintext over HTTPS |
| Open signature required on every authenticated request? | Unclear | FRESH CAPTURE history call uses IOT-Token; historical official bundle called remote config with plain IOT-Token; third-party all-request signing is accepted | **No evidence it is universally required** | Treat IOT-Token as post-login session auth; do not infer a universal Open-sign requirement from permissive server behavior |

## Session/token delta

| Question | Round 02 state | Round 03 evidence | Round 03 conclusion | Implementation consequence |
|---|---|---|---|---|
| Refresh route | High confidence `/login/refresh/access/token` | Current LIVE PROD client uses it | **Confirmed** | Keep route |
| Refresh payload | Conexo used only `refreshToken`; uncertain | Current live-tested client says lone refresh token returns illegal argument and sends both tokens | **Current preference: access+refresh pair** | Send both current tokens; preserve old behavior only as compatibility fallback if deliberately tested |
| Refresh-token rotation | Unknown | Controlled LIVE PROD probe found refresh token single-use/rotating | **Resolved: rotates** | Serialize refresh; atomically replace pair |
| Multiple sessions/account | Unknown | LIVE PROD experiment found independent concurrent sessions coexist | **Resolved: supported** | Background services should mint an independent session instead of sharing one refresh chain |
| Access-token lifetime | Vague hours/days | Current response exposes millisecond expiry; contemporary observation approximately 2 h | **Server value authoritative** | Never hard-code 2 h; use returned expiry |
| Expired-token signal | code 9 seen; other client assumptions | Contemporary clients also encounter HTTP/auth-text variants | **Endpoint-dependent handling advisable** | Detect HTTP 401 plus explicit token-expired messages/codes; refresh once, then fail closed |

## Current production telemetry delta

| Surface | Round 02 | Round 03 |
|---|---|---|
| Selected-key history | Strong repository/HAR evidence | **FRESH CAPTURE current production** |
| History page size | 1500 known from clients | **Current portal itself observed using 1500** |
| History response | Columnar inferred/documented | **Current response captured and one point verified against chart tooltip** |
| Attribute metadata | Known route | **Current capture returned 37 metadata entries for one device** |
| Energy flow | Strong evidence | **Current production page observed route with dataSource=1** |
| Device PV daily detail | Outside original HAR, recent lead | **Current production page observed** |
| Time-zone sensitivity | Known issue evidence | **Strengthened: current portal sends IANA zone + local offset time range** |

## Current web login delta

| Behavior | Evidence | Status |
|---|---|---|
| Account/password login | Historical bundle + LIVE PROD | High confidence |
| Email-login captcha intent | September 2026 official-web comparison: intent **6** | High confidence for intent value |
| SMS-login captcha intent | Same comparison: intent **5** | High confidence for intent value |
| Email send-code route/payload | Contemporary production client + docs/live work | Strong, but not directly extracted from current bundle bytes here |
| SMS send-code route/payload | Contemporary production client + docs/live work | Strong, but not directly extracted from current bundle bytes here |

## Current official-client architecture

| Item | Evidence | Conclusion |
|---|---|---|
| Front-end framework | FRESH CAPTURE source named `umi.6d0aa871.js`, UmiJS + umi-request | Current production remains Umi-based |
| Bundle filename stability | Older bundle had a different hash | Hashed Umi filename changes by deployment |
| SPA rendering | DIRECT current root returns JS-required shell | Server HTML is only the app shell |
| Session header | FRESH CAPTURE | `IOT-Token` current |
| Time-zone header | FRESH CAPTURE | `IOT-Time-Zone` current |
| IDs | LIVE PROD current-client failures after JS rounding | Preserve platform IDs losslessly as strings |

## Routes whose confidence changed in Round 03

| Method | Path | Round 03 status | Why |
|---|---|---|---|
| POST | `/apis/deviceState/simple/attribute/keys/history/v1` | CURRENT CONFIRMED | Fresh production DevTools capture |
| GET | `/apis/deviceState/simple/gatherAttributes/v1` | CURRENT CONFIRMED | Fresh production page |
| GET | `/apis/deviceState/simple/energy/flow/v1` | CURRENT CONFIRMED | Fresh production page |
| POST | `/apis/deviceOverView/generatedEnergy/daily` | CURRENT CONFIRMED | Fresh production page |
| POST | `/apis/deviceOverView/stateAttributeSummary/category/total` | CURRENT CONFIRMED | Fresh production page |
| POST | `/apis/deviceOverView/pvInverterPowerClass/daily/detail` | CURRENT CONFIRMED / POST-HAR ADDITION | Fresh production page; absent from original 108-route HAR |
| POST | `/apis/login/refresh/access/token` | CURRENT LIVE-PRODUCTION CONFIRMED | Live refresh implementation/experiment |
| POST | `/apis/login/account` | CURRENT LIVE-PRODUCTION CONFIRMED | Successful contemporary login testing |

## Remaining protocol questions to carry forward

1. Direct byte-level extraction of the current `umi.6d0aa871.js` auth/request middleware.
2. Exact current official-web application identity and whether it has changed from earlier Umi bundles.
3. Which current production routes actually *require* Open signing after login.
4. Whether refresh-token-only is still accepted for any application identity or compatibility path.
5. Current complete role-aware route/menu/permission inventory.
6. Definitive semantics of history `data.total`.
7. Current official Android client's signing/auth implementation for independent source-of-truth comparison.

## Practical protocol baseline after Round 03

For future implementation work, prefer this order of authority:

1. fresh current production browser captures;
2. direct official-client static source/package analysis;
3. live production interoperability with controlled read-only probes;
4. historical official-bundle reverse engineering;
5. HAR snapshots;
6. third-party implementation assumptions.

Do not silently elevate local unit-test vectors or old copied routes above current production evidence.

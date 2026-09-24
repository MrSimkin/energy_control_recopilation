# Round 12 Companion — Reliability and Polling Matrix

Date: 2026-09-24

Status: COMPLETE

Companion to `round_12_errors_polling_freshness_reliability.md`.

## Recommended polling matrix

| Data class | Visible/foreground | Background | Overlap/cache | Rationale |
|---|---:|---:|---|---|
| Latest/live telemetry | 60–120 s | 5 min | keep last-known-good | Cloud source commonly changes ~5 min |
| Raw history tail | 60–120 s live graph | 5 min | reread last ~15 min, upsert | catches late frames |
| Alarms | 1–2 min if timely alerts matter | 5 min | paginate/dedupe | no confirmed push path |
| Current-day aggregate | 5 min | 5–15 min | overwrite/upsert current bucket | mutable during day |
| Device/station metadata | startup/manual | 15–30 min | heavy cache | low volatility |
| Attribute/schema metadata | startup/model change | daily | heavy cache | rarely changes |
| Yesterday aggregate | n/a | 30–60 min shortly after midnight | upsert | catch late corrections |
| Older closed days | n/a | 6–24 h or audit-only | cache aggressively | low volatility |
| Current month/year | page-dependent | 15–60 min | upsert | still changes |
| Closed month/year | n/a | startup/daily/audit | long cache | mostly stable |

These are project engineering defaults, not vendor-published quotas.

## Retry matrix

| Condition | Retry? | Recommended behavior |
|---|---|---|
| DNS/connect failure | YES | 1–2 retries, exponential backoff+jitter |
| Timeout | YES | 1–2 retries |
| HTTP 5xx | YES | 1–2 retries |
| HTTP 429 | UNKNOWN/IF SEEN | honor Retry-After, reduce concurrency, back off |
| HTTP 401 | AUTH FLOW | serialized refresh, retry original once |
| HTTP 403 | CONTEXTUAL | auth refresh only if session-related; otherwise surface |
| code 9 / token-expired message | AUTH FLOW | serialized refresh once |
| business 401/1001/1002 | AUTH FLOW | serialized refresh once |
| code 7 | NO | credential/protocol correction |
| code 44 | NO | signing/configuration correction |
| code 20007 | NO | environment/account correction |
| code 20101 | NO | fix request/ID/timezone/body/auth |
| code 70132 | NO | mark energy-flow unavailable |
| code 70134 | NO | unsupported config capability; out of active scope |
| code 70247 | NO | manufacturer-disabled capability; out of active scope |
| code 71301 | NO BLIND RETRY | preserve raw code/message; out of active scope |
| partial history pages | TARGETED | keep successful pages, retry missing page/window |

## Freshness matrix

| State | Suggested interpretation |
|---|---|
| collector request succeeded | cloud reachable; says nothing about new device data |
| newest source timestamp advanced | genuine new device/cloud frame |
| source age ≤ 2× recent median cadence | fresh |
| source age 2–3× median cadence | delayed |
| source age > max(15 min, 3× cadence) | stale |
| device metadata says offline | cloud reports device offline |
| PC/network offline | collector unavailable; do not infer inverter state |

For a ~5-minute source this yields approximately:

- fresh ≤10–12 min;
- stale >15–18 min.

These are UI/data-quality heuristics, not vendor failure definitions.

## Token/session rules

1. use server-returned token expiry when available;
2. use current access + refresh token pair for refresh;
3. treat refreshed pair as rotated/single-use;
4. save access + refresh atomically;
5. serialize refresh with a lock/single shared promise;
6. proactively refresh 2–5 min before expiry;
7. after an auth failure, refresh once and retry original request once;
8. if that fails, require re-auth;
9. never share one rotating refresh chain across independent processes.

## Concurrency

For one household/account Windows collector:

- initial read concurrency cap: **2–4**;
- no overlapping collection cycles;
- backfill sequential or lightly parallel;
- visible/live reads get priority;
- historical backfill yields to live collection;
- token refresh always single-flight.

No official rate quota was discovered.

## Durable collection loop

```text
timer/wake/resume
  → ensure token
  → fetch trailing history window
  → paginate
  → upsert by device+raw-field+source-time
  → refresh current state/aggregate as needed
  → record newest source timestamp + completeness
  → sleep
```

## Windows resume/recovery

After sleep or network loss:

1. do not replay missed timers;
2. refresh/authenticate if needed;
3. read current device status;
4. request history from newest stored source timestamp minus overlap through now;
5. paginate/upsert;
6. refresh current aggregate;
7. resume normal cadence.

## Rate-limit status

**No credible vendor quota found.**

No reliable public evidence currently establishes:

- requests/minute;
- burst limit;
- 429 threshold;
- Retry-After behavior.

Therefore the client should be conservative by construction and support generic 429 handling if encountered later.

## Statistical-integrity rules

- HTTP failure ≠ zero.
- stale value ≠ current measurement.
- null field ≠ zero.
- partial pagination ≠ complete period.
- duplicate late sample must upsert, not append.
- placeholder aggregate ≠ real zero.
- source timestamp and retrieval timestamp must remain separate.

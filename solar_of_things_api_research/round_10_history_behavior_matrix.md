# Round 10 Companion — Historical Data Behavior Matrix

Date: 2026-09-24

Status: COMPLETE

Companion to `round_10_historical_data_granularity_retention_pagination_gaps.md`.

## Historical endpoint matrix

| Surface | Method/path | Shape | Typical page size observed | Pagination | Historical depth evidence | Best use |
|---|---|---|---:|---|---|---|
| Selected-key history | POST `/deviceState/simple/attribute/keys/history/v1` | columnar shared `timeSeries[]` + primitive field arrays | 1500 in current web; 2000 used by older client | Yes | at least ~105 days in real 2026 backfill corpus | Preferred charts / known metrics |
| Simple record history | POST `/deviceState/simple/attribute/record/list/v1` | columnar, richer field objects | 300 in proven collector | Yes | multi-month backfill in same corpus | Broad raw preservation/discovery |
| Row record fallback | POST `/deviceState/attribute/record/list` | one frame/record with nested fields | 80 in reference-client behavior | Yes | max depth not independently established | Compatibility fallback |
| Alarm history | POST `/alarm/query/list` | row/page list | 100 in proven collector | Yes | not measured here | Alarm timeline |
| Latest state | GET `/deviceState/simple/state/latest/v1` | current state | n/a | n/a | current only | Current snapshot |
| Energy flow | GET `/deviceState/simple/energy/flow/v1` | structured current flow/state | n/a | n/a | current structured flow only | Live flow |
| Device details | GET `/device/details` | current metadata | n/a | n/a | current only | Snapshot locally |
| Config cache | POST `/remote/device/configs/cache/get` | current config map | n/a | n/a | current only | Snapshot locally |
| Station summaries | multiple overview/category routes | daily/monthly/yearly/total buckets | period-based | period-based | months/years available; source-dependent | Aggregates, separate Round 11 |

## Cadence evidence

| Evidence | Observation | Interpretation |
|---|---|---|
| Real 26,417-timestamp corpus | median 301 s, p90 360 s | Approx. 5-minute nominal cadence with jitter |
| Full days in same corpus | 269–278 frames/day | Missing frames are normal; do not require 288 |
| Current production capture | only 47 frames over a partial day | Device/state-dependent sparsity can be much greater |
| Public app feedback | cloud updates commonly described around 3–5 min | Supporting, lower-confidence evidence |

## Pagination interpretation

| Claim | Verdict | Reason |
|---|---|---|
| 2000 is the maximum page size | **Strong** | Swagger-derived common contract + working clients |
| 2000 is the maximum recoverable points in a date range | **False / unsupported** | Pagination exists and later collectors successfully use multiple pages |
| 7+ day windows are permanently server-downsampled | **Unproven** | Original client reporting loss always used page 1 only |
| Current web uses 1500/page | **Confirmed current capture** | Production DevTools request |
| `data.total` is total pages on selected-key endpoint | **Probable, not fully proven** | Fresh capture had total=1 and current clients treat it as page count |
| Daily chunking is safer than giant windows | **Strong engineering conclusion** | Proven backfill pattern; easier retry/completeness validation |

## Retention evidence

| Finding | Strength |
|---|---|
| Raw telemetry retrieved on 2026-08-24 back to 2026-05-11 | **Strong: at least ~105 days retained for that device/account** |
| Another Jan 2026 project contains Sep–Nov 2025 real analyses | Supporting evidence for several-month history |
| Product docs advertise long-term / daily-monthly-yearly history | General product claim |
| Universal raw retention limit | **Unknown** |
| Raw and station-summary history start on same date | **False in at least one corpus** — raw May 11, valid station daily buckets May 26 |

## Missing-data semantics

| Situation | Correct meaning |
|---|---|
| field array entry = `null` | frame exists, attribute absent |
| timestamp missing entirely | no source report frame |
| API returns measured numeric 0 | real zero |
| no rows before earliest available date | no data, not zero |
| page 1 succeeds, later page fails | partial period |
| long silence | do not draw continuous line across it |
| future/aggregate bucket with `isRealValue=false` | placeholder, not real measurement |

## Recommended future collector behavior

1. Use station-local calendar-day windows.
2. Generate boundaries with an IANA timezone-aware library.
3. Include explicit UTC offset and `IOT-Time-Zone`.
4. Paginate until short page / reliable total-page termination.
5. Keep a hard safety page cap.
6. Upsert by `device_id + raw_attr + recorded_at`.
7. Preserve actual source timestamps.
8. Keep null distinct from 0.
9. Reread a small trailing overlap for late uploads.
10. Store completeness status per requested window.
11. Retry suspiciously sparse days once; do not invent missing data.
12. Use actual time deltas for raw-power integration.
13. Prefer server aggregates where later validation proves them more authoritative.

## Initial backfill recommendation

A future live implementation should:

- determine device installation/creation date;
- probe increasingly old days to find the actual raw-history boundary;
- avoid assuming a fixed vendor retention period;
- backfill daily from the first useful date;
- retrieve server aggregates separately;
- record frame count, first/last timestamp, median/p90/max gap and page count per day.

## Remaining unknowns to validate later

- maximum raw retention for the user's account;
- whether very old raw history is downsampled;
- endpoint-specific `total` semantics over multi-page real responses;
- selected-key vs record-list retention differences;
- late-upload maximum delay;
- correction/backfill behavior if an old day is queried again later.

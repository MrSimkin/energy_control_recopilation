# Solar of Things API Research

This folder is the single canonical location for the results of the Solar of Things / SiSeLi cloud API investigation.

## Research policy

- Keep the investigation evidence-driven and as exhaustive as practical within the active scope.
- Default to separate, focused investigation rounds so each topic receives dedicated attention and its evidence remains easy to audit.
- Combine investigation topics only when doing so is genuinely necessary for rigor, tooling/context limits, prevention of evidence loss, or because the subjects cannot meaningfully be separated.
- Distinguish confirmed facts, code-derived evidence, live observations, third-party claims, hypotheses, contradictions, and unresolved questions.
- Preserve source references and enough context to reproduce important findings.
- Do not perform device-changing/control actions merely to investigate the API.
- Do not store credentials, access tokens, refresh tokens, cookies, passwords, application secrets, Wi-Fi passwords, or other reusable secrets in this repository.

## Active project scope — cloud data only

The intended project will obtain Solar of Things / SiSeLi data from the **cloud** for a Windows dashboard/statistics application.

The following research themes remain in scope:

1. cloud data model and telemetry dictionary — **complete**;
2. historical-data granularity, retention, pagination and gaps — **complete**;
3. server-side statistics, aggregations and reports — **complete**;
4. errors, polling frequency, freshness and reliability — **complete**;
5. cloud-UI coverage audit — **complete**;
6. final controlled read-only validation against the user's own account/device when appropriate.

## Officially out of scope

The following branches are **officially out of scope for this project unless the user explicitly reopens them**:

- dongle-to-cloud MQTT/uplink interception;
- BLE / Proximal Monitoring;
- raw inverter serial / Modbus / RS232;
- Wi-Fi provisioning;
- Android-local/application-binary research except as an on-demand clue for a cloud ambiguity;
- firmware/control/settings mutation research;
- cloud passthrough and low-level device control;
- `/near/dtu/*` local protocol helpers;
- broad cross-surface reconciliation involving local protocols.

Existing Round 07/08 material is preserved as historical research but is not part of the active project path.

## Completed rounds

1. [Round 00 — Identity and Ecosystem Mapping](round_00_identity_ecosystem.md)
2. [Round 01 — Public-Source Census](round_01_public_source_census.md)
3. [Round 02 — Cloud REST API Repository Archaeology](round_02_cloud_rest_api_repository_archaeology.md)
   - [Round 02 Appendix — Cloud REST Endpoint Catalog](round_02_endpoint_catalog.md)
4. [Round 03 — Official Production Web-Application Archaeology](round_03_official_production_web_app_archaeology.md)
   - [Round 03 Companion — Production Web Protocol Delta Matrix](round_03_protocol_delta_matrix.md)
5. [Round 04 — Official Android Application Package Archaeology](round_04_official_android_package_archaeology.md)
   - [Round 04 Companion — Android Release, Package and Permission Matrix](round_04_android_release_permission_matrix.md)
   - Closed as sufficient for the cloud/Windows project; binary extraction is not required.
6. [Round 05 — Alternate Web Environment and OpenAPI Documentation Comparison](round_05_alternate_web_openapi_comparison.md)
   - [Round 05 Companion — Swagger vs Production Endpoint Delta](round_05_swagger_production_delta.md)
7. [Round 06 — OpenAPI Contract Reconstruction and Windows-Client Applicability](round_06_openapi_contract_windows_applicability.md)
   - [Round 06 Companion — Canonical Windows API Route Map](round_06_windows_api_canonical_map.md)
8. [Round 07 — BLE / Proximal Monitoring Archaeology](round_07_ble_proximal_monitoring_archaeology.md)
   - **Historical / now out of scope.**
9. [Round 08 — Cross-Surface Reconciliation for the Windows Project](round_08_cross_surface_windows_reconciliation.md)
   - **Historical / local-protocol portions now out of scope.**
10. [Round 09 — Cloud Data Model and Telemetry Dictionary](round_09_cloud_data_model_telemetry_dictionary.md)
    - [Round 09 Companion — Cloud Telemetry Normalization Dictionary](round_09_telemetry_dictionary.md)
11. [Round 10 — Historical Data Granularity, Retention, Pagination and Gaps](round_10_historical_data_granularity_retention_pagination_gaps.md)
    - [Round 10 Companion — Historical Data Behavior Matrix](round_10_history_behavior_matrix.md)
12. [Round 11 — Server-Side Statistics, Aggregations and Reports](round_11_server_side_statistics_aggregations_reports.md)
    - [Round 11 Companion — Aggregate Trust Matrix](round_11_aggregation_trust_matrix.md)
13. [Round 12 — Errors, Polling Frequency, Freshness and Reliability](round_12_errors_polling_freshness_reliability.md)
    - [Round 12 Companion — Reliability and Polling Matrix](round_12_reliability_polling_matrix.md)
14. [Round 13 — Cloud-UI Coverage Audit](round_13_cloud_ui_coverage_audit.md)
    - [Round 13 Companion — Cloud UI Coverage Matrix](round_13_cloud_ui_coverage_matrix.md)

## Current cloud-only evidence baseline after Round 13

- The cloud API is independent of Android and can be consumed directly by a Windows program.
- Production REST base: `https://solar.siseli.com/apis`.
- Practical hierarchy: **account/user → station → DTU/logger → device/inverter → telemetry attributes**.
- Platform IDs must be stored losslessly as strings.
- Device telemetry is dynamically described by device/protocol-specific attribute metadata; there is no universal field schema.
- Historical selected-key data is columnar: shared timestamps align by index with each field series; null means missing at that report frame and must not be converted to zero.
- Real-world raw telemetry commonly has an approximately five-minute cadence rather than an exact five-minute grid: one large corpus measured median 301 s, p90 360 s, and 269–278 source frames on full days.
- A 2,000-record figure is best understood as a page-size boundary, not a total-history ceiling. Older “long-window data loss” evidence came from page-1-only requests; newer collectors successfully paginate.
- Current production web history uses `count=1500` for a local-day selected-key request.
- One real 2026 backfill recovered raw cloud telemetry roughly 105 days old. A universal maximum retention period remains unknown.
- Raw telemetry and station summaries can begin on different dates.
- Missing field, missing frame and measured zero are three distinct states.
- Historical queries must use the station's IANA timezone and explicit local UTC offsets. Returned timestamps are UTC instants.
- Daily, timezone-correct, paginated, idempotent backfill is the preferred architecture.
- Current-only surfaces such as latest state, energy-flow structure, device details and config must be snapshotted locally if their change history matters.
- Server aggregates exist at device, station and owner/account levels.
- PV generated-energy aggregates have the strongest validation: one real month differed from raw-history integration by only 0.48 kWh, while another real installation showed station summary and device snapshot agreeing against a misleading cumulative-counter delta.
- Aggregate trust is property-specific: `consumeElectricityQuantity` and `buyElectricityQuantity` were placeholders in one real corpus, so that dashboard used device-reported daily counters instead.
- `isRealValue=false` means placeholder and must never be interpreted as measured zero; `true` is the strongest aggregate quality signal, while null is unlabelled.
- Category-monthly responses provide daily energy buckets; category-yearly responses provide monthly buckets; long-term generated-energy endpoints provide longer/year-labelled totals.
- Raw history remains preferable for curves and custom sub-period analysis; validated server aggregates are preferable for canonical period totals.
- Device/station report/export endpoints exist but create server-side report artifacts and are not required for normal dashboard ingestion.
- Cloud telemetry commonly advances around every five minutes; 60–120 s foreground polling improves detection latency but does not create higher measurement resolution.
- Recommended durable collection is approximately every 5 minutes with a ~15-minute trailing-history overlap and idempotent upserts.
- Source timestamp and HTTP retrieval timestamp must remain separate; successful HTTP does not imply a new inverter frame.
- Auth refresh must be single-flight. Current best evidence favors refreshing with the current access+refresh pair and atomically persisting the rotated replacement pair.
- Authentication expiry can surface as HTTP 401/403, code 9, business 401/1001/1002, or token-expiry text; only one refresh + one original-request retry should occur.
- Transient DNS/connect/timeout/5xx failures can be retried boundedly; deterministic business errors such as 20101 must not be blindly retried.
- No credible vendor request quota or 429 threshold was found. Concurrency and cadence must therefore be conservative by design, with generic Retry-After handling if 429 is ever observed.
- For a single-account Windows collector, 2–4 concurrent reads is a conservative starting cap; do not overlap collection cycles.
- Offline, stale telemetry, and collector/network failure are distinct states and must be represented separately.
- Cloud-UI coverage is now sufficient: every major read-only dashboard/statistics surface has a known API path or API family.
- Home/dashboard headline metrics map to `dashboard/summary/commons`, including station/device counts, generation totals, capacity/state summary and optional environmental-impact fields.
- Project/station lists, device lists, current state, energy flow, alarms, selectable analysis parameters and historical curves all have mapped read-only cloud APIs.
- Daily/monthly/yearly/cumulative generation is covered at device, station and owner levels.
- Optional portal features such as income, environmental savings, station ranking and geographic distribution are API-mapped but do not justify additional broad research.
- Remaining uncertainty is installation-specific: the user's model/protocol fields, units/signs, aggregate placeholder behavior, source lag, actual history retention and exact UI/API correspondence.

## Next-round decision

No additional broad public-source research round is warranted.

The only planned surviving phase is **final controlled read-only validation against the user's own Solar of Things account/device**.

That phase should validate:

- station/device/model/protocol identity;
- target-device field catalog and `dataSource`;
- current telemetry against the official UI;
- history cadence, pagination, retention and timezone behavior;
- daily/monthly/yearly/lifetime aggregate correspondence;
- placeholder versus real summary properties;
- token expiry/refresh behavior and cloud-source lag.

If that validation reveals one specific dashboard-relevant cloud value that is still unmapped, create a targeted gap round for that value only. Do not reopen the officially out-of-scope local/BLE/MQTT/control branches unless the user explicitly changes scope.

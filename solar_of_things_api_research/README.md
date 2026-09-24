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

1. cloud data model and telemetry dictionary;
2. historical-data granularity, retention, pagination and gaps;
3. server-side statistics, aggregations and reports;
4. errors, polling frequency, freshness and reliability;
5. cloud-UI coverage audit — whether useful cloud-visible information can be retrieved through the API;
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

## Current cloud-only evidence baseline after Round 09

- The cloud API is independent of Android and can be consumed directly by a Windows program.
- Production REST base: `https://solar.siseli.com/apis`.
- Practical hierarchy: **account/user → station → DTU/logger → device/inverter → telemetry attributes**.
- Platform IDs must be stored losslessly as strings.
- Device telemetry is dynamically described by device/protocol-specific attribute metadata; there is no universal field schema.
- Historical selected-key data is columnar: shared timestamps align by index with each field series; null means missing at that report frame and must not be converted to zero.
- Raw field names are not globally trustworthy indicators of unit or meaning.
- Critical semantic collisions are confirmed:
  - `batteryCapacity` can mean Ah capacity on one family and SOC % on another;
  - `generationPower` can be W or kW depending on protocol;
  - `acOutputActivePower` has different units across device families;
  - signed grid-power conventions are opposite on at least two known families.
- Any future statistical store should preserve raw key/value/unit plus model/gather-protocol metadata alongside normalized measurements.
- Server-side station summaries expose promising energy properties including PV generation, battery charge/discharge energy, consumption, grid import and grid export.

## Next-round decision

**Round 10 — Historical Data Granularity, Retention, Pagination and Gaps** remains standalone and is the next active investigation.

It should determine:

- actual reporting/sample cadence;
- how much high-resolution history can be retrieved;
- paging semantics and practical page-size limits;
- whether long date ranges are truncated, downsampled or silently lose samples;
- null/missing/offline behavior;
- timestamp/timezone/day-boundary behavior;
- differences between selected-key history and record-list history.

The later server-side statistics/aggregation round should remain separate so raw-history behavior is understood before platform-calculated totals are compared with it.

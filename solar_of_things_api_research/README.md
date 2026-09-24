# Solar of Things API Research

This folder is the single canonical location for the results of the Solar of Things API investigation.

## Research policy

- Keep the investigation evidence-driven and as exhaustive as practical.
- Default to separate, focused investigation rounds so each topic receives dedicated attention and its evidence remains easy to audit.
- Combine multiple investigation topics into one round only when doing so is genuinely necessary to preserve rigor, avoid tooling/context limits, prevent evidence loss, or because the topics cannot be meaningfully investigated independently.
- Do not combine rounds merely for convenience, speed, or brevity.
- Each completed round will be recorded as a separate file in this folder.
- Distinguish confirmed facts, code-derived evidence, live observations, third-party claims, hypotheses, contradictions, and unresolved questions.
- Preserve source references and enough context to reproduce important findings.
- Do not perform device-changing/control actions merely to investigate the API.
- Do not store credentials, access tokens, refresh tokens, cookies, passwords, application secrets, or other reusable secrets in this repository.

## Completed rounds

1. [Round 00 — Identity and Ecosystem Mapping](round_00_identity_ecosystem.md) — product identity, official surfaces, app IDs, developer/publisher, sibling/white-label ecosystem, privacy evidence, discovered subdomains, and unresolved infrastructure questions.
2. [Round 01 — Public-Source Census](round_01_public_source_census.md) — systematic inventory of public reverse-engineering projects, cloud API clients, MQTT interception work, BLE research, raw serial protocol work, manuals, application archives, issue histories, hardware variants and community evidence.
3. [Round 02 — Cloud REST API Repository Archaeology](round_02_cloud_rest_api_repository_archaeology.md) — deep source/history analysis of independent cloud clients: authentication, IOT Open signing, token lifecycle, station/device discovery, realtime/history data, remote configuration, field/model variability, errors and evidence chronology.
   - [Round 02 Appendix — Cloud REST Endpoint Catalog](round_02_endpoint_catalog.md) — route-level inventory: 108 HAR-observed endpoints, four permission-discovered additions, later live/implemented routes, and rejected legacy/unverified route names.
4. [Round 03 — Official Production Web-Application Archaeology](round_03_official_production_web_app_archaeology.md) — current production SPA fingerprint, fresh Device details/Data Analysis capture, official Umi-bundle signing archaeology, current authentication/token behavior, signing-conflict resolution and current-web protocol constraints.
   - [Round 03 Companion — Production Web Protocol Delta Matrix](round_03_protocol_delta_matrix.md) — compact audit trail of what Round 03 resolved, strengthened, changed or left open from Round 02.
5. [Round 04 — Official Android Application Package Archaeology](round_04_official_android_package_archaeology.md) — package identity, release/signing lineage, Flutter architecture, permissions, mobile SDKs and Android-specific surface mapping.
   - [Round 04 Companion — Android Release, Package and Permission Matrix](round_04_android_release_permission_matrix.md) — auditable release/hash/signature/permission matrix.
   - **Closed as SUFFICIENT-FOR-PROJECT / BINARY-NOT-REQUIRED.** The intended implementation is a Windows client of the independent cloud API; Android binary extraction remains optional future corroboration.
6. [Round 05 — Alternate Web Environment and OpenAPI Documentation Comparison](round_05_alternate_web_openapi_comparison.md) — production/test separation, first-party Swagger documentation recovery, signing/refresh corroboration, role model, documented capabilities, disabled/gated features, and environment-specific conclusions.
   - [Round 05 Companion — Swagger vs Production Endpoint Delta](round_05_swagger_production_delta.md) — exact method/path comparison between the preserved Swagger-derived contract and the production corpus.
7. [Round 06 — OpenAPI Contract Reconstruction and Windows-Client Applicability](round_06_openapi_contract_windows_applicability.md) — canonical Windows architecture, 12-route P0 core, auth/session rules, telemetry/schema design, safety boundaries and implementation priorities.
   - [Round 06 Companion — Canonical Windows API Route Map](round_06_windows_api_canonical_map.md) — full 274-route union with provenance, operation class and Windows-project priority.

## Current evidence baseline after Round 06

- The Solar of Things cloud API is independent of Android and can be used directly by a Windows client.
- Production REST base: `https://solar.siseli.com/apis`.
- The canonical method+path union now contains **274 distinct routes**: 221 Swagger-derived, 115 production-corpus, 62 overlapping, 159 Swagger-only, and 53 production-only.
- A **12-contract P0 Windows core** is sufficient for authentication/session, station/device discovery, schema discovery, current telemetry, energy flow, historical selected-key data, and alarms.
- The current best signing model is Base64(UTF-8(sorted URL params + IoT Open params)) → HMAC-SHA256 → MD5, with MD5 password preprocessing for account login.
- `IOT-Token` is the core post-login session header; Open signing is not proven necessary on every authenticated route.
- Refresh uses `/login/refresh/access/token`; current evidence favors sending the current access+refresh pair, with rotating/single-use refresh tokens and atomic token replacement.
- Platform IDs must be preserved losslessly as strings.
- Device telemetry is schema/model/firmware dependent; field name, unit, sign convention, dataSource and writable config keys cannot be assumed globally.
- Time-zone handling is part of the protocol, not only presentation: use IANA zones and explicit ISO-8601 offsets.
- Control/mutation routes are separated from passive telemetry and are disabled by default in the proposed Windows architecture.
- The previously claimed `/openapis/ws` WebSocket remains unverified and should not be implemented from current evidence.

## Remaining dedicated research branches

- dongle-to-cloud MQTT/uplink archaeology;
- BLE / Proximal Monitoring archaeology;
- raw inverter serial protocol archaeology;
- cross-surface reconciliation and controlled read-only live validation after static evidence is mature;
- optional Android binary archaeology only if a later Android-specific question requires it.

## Next-round decision

**Round 07 — Dongle-to-cloud MQTT/uplink archaeology** should remain standalone.

This is the highest-value next independent surface for the Windows project because it is Android-independent and exposes the telemetry/protocol stream produced by the physical logger before the REST API normalizes it.

Round 07 should reconstruct broker behavior, transport/framing, device/topic identity, proprietary payload blocks, decoded measurements, reporting cadence, firmware/model variability, and the relationship between raw uplink fields and REST API fields. It should remain read/passive in research scope and should not interfere with the device's normal cloud connectivity.

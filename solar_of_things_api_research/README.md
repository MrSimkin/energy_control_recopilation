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

## Current evidence baseline after Round 05

- The Solar of Things cloud API is independent of Android and can be used directly by a Windows client.
- Production REST base: `https://solar.siseli.com/apis`.
- `test.solar.siseli.com` is a distinct live JavaScript environment and historically used a different IoT Open application identity/account environment from production.
- `doc.solar.siseli.com` hosts/hosted an IoT Open Swagger service at `/openapi/`, including `swagger2/api-docs?group=openApis`.
- The preserved Swagger-derived contract reproducibly contains **221 distinct method/path pairs**, despite a stale/unreconciled footer that states 227 endpoints.
- The Round 02 production corpus currently contains **115** method/path entries.
- Exact Swagger/production overlap is only **62** routes; **159** are Swagger-only and **53** production-corpus-only. Neither source is a superset.
- Swagger independently corroborates the Base64 IoT Open signing algorithm, inclusion of URL parameters, empty GET body hash, MD5 password preprocessing, and the access+refresh token refresh DTO.
- Login response schema exposes role/capability flags such as admin, dealer, manufacturer, integrator, official staff and station owner.
- The documented platform includes significant API families absent from the captured owner-session HAR, including device lifecycle, account management, peak-valley scheduling, firmware/upgrade, raw passthrough, fast reporting and a 17-route `/near/dtu/*` local/protocol-generation service.
- Documented routes can be role/manufacturer/device gated. The timed `/instruction/*` service is a concrete example: the backend accepts/parses the contract but returned manufacturer-disabled error 70247 in live testing for one product family.
- Production traffic contains routes missing from the preserved Swagger snapshot, including portal/router metadata, reporting/export, station income, user currency/theme/logging, DTU variants and newer device-overview routes.
- The previously proposed `wss://solar.siseli.com/openapis/ws` WebSocket remains **unverified**; its downstream implementation was later removed as dead code and no first-party/current production confirmation has been found.
- `demo.doc.solar.siseli.com` remains an unresolved lead; current evidence does not justify a dedicated round.

## Remaining dedicated research branches

- OpenAPI contract normalization for the Windows client;
- dongle-to-cloud MQTT/uplink archaeology;
- BLE / Proximal Monitoring archaeology;
- raw inverter serial protocol archaeology;
- cross-surface reconciliation and controlled read-only live validation after the static contract is mature;
- optional Android binary archaeology only if a later Android-specific question requires it.

## Next-round decision

**Round 06 — OpenAPI Contract Reconstruction and Windows-Client Applicability** should remain standalone.

Round 05 materially expanded the known HTTP API surface. Before moving into MQTT/BLE/serial protocols, normalize the 221-route Swagger-derived contract together with production evidence into a canonical Windows-oriented API map.

Round 06 should classify each route by service group, method, parameters/body DTO, read vs mutation risk, production evidence, role/manufacturer/device gating and practical relevance to the Windows project. It should also identify a minimal safe/read-only core and optional management/control layers, without writing implementation code yet.

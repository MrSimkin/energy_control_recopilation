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
- Do not store credentials, access tokens, refresh tokens, cookies, passwords, application secrets, Wi-Fi passwords, or other reusable secrets in this repository.

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
8. [Round 07 — BLE / Proximal Monitoring Archaeology](round_07_ble_proximal_monitoring_archaeology.md) — RWB1 GATT profile, encrypted envelope/framing, DTUID-derived transport-key model, separate local security password, CID command catalog, UART passthrough, Windows BLE applicability and relation to `/near/dtu/*`.
9. [Round 08 — Cross-Surface Reconciliation for the Windows Project](round_08_cross_surface_windows_reconciliation.md) — reconciles cloud REST, Swagger/OpenAPI, current web evidence and BLE into one capability map; identifies real implementation blockers and the point of research saturation before project requirements are known.

## Explicitly skipped branch

**Dongle-to-cloud MQTT/uplink archaeology — SKIPPED BY PROJECT CONSTRAINT.**

The project cannot intercept, proxy, redirect, or otherwise intervene in the logger-to-cloud MQTT communication path. This is an intentional scope decision, not an unresolved research failure.

## Current evidence baseline after Round 08

- The Solar of Things cloud API is independent of Android and can be used directly by a Windows client.
- Production REST base: `https://solar.siseli.com/apis`.
- The canonical HTTP union contains **274 distinct routes**, while a **12-contract P0 core** is sufficient for generic Windows authentication, discovery, telemetry, history and alarms.
- Cloud authentication/signing, session refresh, station/device discovery, schema discovery, latest telemetry, history and alarms are mature enough for a prototype.
- Platform IDs must be preserved losslessly as strings; device field names, units, signs, dataSource and configuration keys remain device/model/firmware dependent.
- BLE / Proximal Monitoring is a technically viable **optional Windows-local path**, not a dependency of the cloud client.
- The RWB1 local transport is strongly evidenced as FEE7/FED5/FED6 with AES-128-CBC, key=IV, zero padding, Base64 framing and a generic UART passthrough at CID 30024/30025.
- The preserved BLE implementation derives transport encryption from `MD5(DTUID + "SEC_")`, separately from the optional user-set local security password.
- MQTT/uplink interception is explicitly **SKIPPED BY PROJECT CONSTRAINT** and no longer belongs to the critical research path.
- Raw serial is optional and should be reopened only if a project requirement cannot be satisfied by cloud REST, cloud passthrough or BLE.
- The most important remaining uncertainty is now the **actual Windows project requirements**, followed by targeted read-only validation against the user's own account/device.
- Broad protocol discovery has reached diminishing returns: further research should be selected from concrete product requirements rather than completeness.

## Remaining targeted work

- controlled read-only validation with the user's own Solar of Things account/device when the project design makes it useful;
- targeted validation of specific P1/P2 endpoints selected by project requirements;
- optional BLE + `/near/dtu/*` validation if local/offline operation is required;
- optional raw inverter serial archaeology only if direct inverter-level communication becomes necessary;
- optional Android binary archaeology only if an Android-specific question later matters.

## Next-step decision

**Pause broad research until the Windows project's actual functional requirements are defined.**

At this point the useful next action is not another generic investigation round. Once the user explains the project, map those requirements against Round 06's 274-route contract and Round 08's cloud/local capability map.

Then proceed selectively:

- cloud monitoring/analytics → read-only live validation, then implementation design;
- cloud control/management → targeted capability/safety validation for the exact mutation endpoints needed;
- local/offline Windows behavior → targeted BLE + `/near/dtu/*` validation, with raw serial only if proven necessary.
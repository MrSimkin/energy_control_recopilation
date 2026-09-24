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

## Round in progress

5. [Round 04 — Official Android Application Package Archaeology](round_04_official_android_package_archaeology.md) — package identity, current/recent release artifacts, signing lineage, Flutter architecture, permissions, mobile SDKs and Android-specific surface mapping.
   - [Round 04 Companion — Android Release, Package and Permission Matrix](round_04_android_release_permission_matrix.md) — auditable release/hash/signature/permission matrix.
   - **Status:** public package/manifest architecture phase complete; direct binary extraction remains blocked because the current XAPK cannot be materialized through the available CDN/tool path.

## Current evidence baseline after the Round 04 public-static phase

- Official Android package: `com.ssli.sise_solar`.
- Android **3.1.12 / version code 86** is now distributed: APKPure's live latest redirect names the 3.1.12 XAPK and code 86, Softonic exposes a 3.1.12 XAPK hash, and Google Play's current page reports a September 16, 2026 update.
- 3.1.11 / code 85 remains the newest release with fully indexed split/signature metadata.
- The APKPure signature fingerprint is stable from at least 2.4.7 through 3.1.11, supporting one Android signing lineage across those releases.
- The official privacy policy explicitly states that Solar of Things is **Flutter-based**.
- Current indexed Android permissions establish cloud networking, Wi-Fi control, BLE scanning/connection/advertising, coarse/fine location, camera/storage access, foreground-service capability and system settings access.
- The official privacy-policy appendix identifies Baidu Map SDK, Baidu Location SDK and the Flutter `com.baseflow.geolocator` plugin/library.
- Official Android functionality clearly spans cloud API, BLE/Wi-Fi provisioning, local/proximal monitoring/debugging and map/location/weather surfaces.
- The actual 3.1.12/3.1.11 package bytes have not yet been extracted here, so no claim is made about direct AndroidManifest parsing, `libapp.so` strings, current Android endpoint constants, embedded application credentials or official BLE UUIDs.

## Remaining dedicated research branches

- **Round 04 continuation:** Android XAPK acquisition, manifest/native/Flutter AOT static extraction;
- alternate web environment comparison (`test.solar`, `doc.solar`, `demo.doc.solar`);
- dongle-to-cloud MQTT/uplink archaeology;
- BLE / Proximal Monitoring archaeology;
- raw inverter serial protocol archaeology;
- cross-surface reconciliation and controlled read-only live validation after static evidence is mature.

## Next-run decision

**Remain in Round 04. Do not advance to Round 05 yet.**

Direct package-byte inspection is material to the purpose of Android application-package archaeology. The next run should make one dedicated acquisition/static-extraction attempt using 3.1.12 first and 3.1.11 as the stable fallback.

If package materialization remains impossible after that dedicated attempt, Round 04 should be closed explicitly as **metadata-complete / binary-blocked**, with the precise missing checks recorded, rather than consuming repeated rounds on the same tooling limitation.

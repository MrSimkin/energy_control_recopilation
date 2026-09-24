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

## Current evidence baseline after Round 03

- The production site is a JavaScript-only Umi-based SPA. A fresh production capture in this research window identifies a current hashed bundle as `umi.6d0aa871.js`; older captures used different hashes, so the filename is deployment-specific.
- The original broad browser HAR remains a strong 108-route production snapshot; its later catalog has 112 routes after four login-permission additions, but it is not complete or immutable.
- A fresh current production Device details → Data Analysis capture confirms selected-key history, columnar history responses, attribute metadata, energy flow, several device-overview routes and a post-HAR PV-inverter daily-detail route.
- The Round 02 signing disagreement is substantially resolved: historical official-bundle reverse engineering plus independent live-production interoperability strongly support **Base64(UTF-8(canonical parameters)) → HMAC-SHA256 → MD5**, not the later hex-preimage implementation.
- Generic Open signing should include URL query parameters. Older three-field signing examples targeted queryless login and therefore did not disprove this.
- `IOT-Token` remains the core post-login session mechanism. Open signing is clearly required for account login/open operations but is not proven necessary for every authenticated route; official portal evidence includes token-only post-login calls.
- Contemporary production testing shows refresh uses `/login/refresh/access/token`, refresh tokens rotate/single-use, multiple independent sessions can coexist for one account, and the current production-tested refresh model sends the current access+refresh token pair.
- Platform IDs must be preserved losslessly as strings; 64-bit numeric identifiers can exceed JavaScript's safe-integer range.
- Device fields, units, signs, data-source choices and writable configuration keys still vary materially across hardware/firmware families.

## Remaining dedicated research branches

- official Android application package/static archaeology;
- alternate web environment comparison (`test.solar`, `doc.solar`, `demo.doc.solar`);
- dongle-to-cloud MQTT/uplink archaeology;
- BLE / Proximal Monitoring archaeology;
- raw inverter serial protocol archaeology;
- cross-surface reconciliation and controlled read-only live validation after static evidence is mature.

## Next-round decision

**Round 04 — Official Android Application Package Archaeology** should remain standalone.

Android is now the highest-value independent official-client source: it can cross-check the cloud auth/route model while exposing mobile-only network constants, DTO/service names, provisioning assets and protocol clues not reachable from the web SPA.

Round 04 should inventory and statically analyze official Android packages and historical versions without performing device-changing actions. BLE details discovered there should be preserved as source-of-truth clues but deferred to the later dedicated BLE / Proximal Monitoring round, rather than mixing two protocol investigations.

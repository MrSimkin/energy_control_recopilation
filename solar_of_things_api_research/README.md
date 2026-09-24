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
- Do not store credentials, access tokens, refresh tokens, cookies, passwords, or other secrets in this repository.

## Completed rounds

1. [Round 00 — Identity and Ecosystem Mapping](round_00_identity_ecosystem.md) — product identity, official surfaces, app IDs, developer/publisher, sibling/white-label ecosystem, privacy evidence, discovered subdomains, and unresolved infrastructure questions.
2. [Round 01 — Public-Source Census](round_01_public_source_census.md) — systematic inventory of public reverse-engineering projects, cloud API clients, MQTT interception work, BLE research, raw serial protocol work, manuals, application archives, issue histories, hardware variants and community evidence.
3. [Round 02 — Cloud REST API Repository Archaeology](round_02_cloud_rest_api_repository_archaeology.md) — deep source/history analysis of independent cloud clients: authentication, IOT Open signing, token lifecycle, station/device discovery, realtime/history data, remote configuration, field/model variability, errors and evidence chronology.
   - [Round 02 Appendix — Cloud REST Endpoint Catalog](round_02_endpoint_catalog.md) — route-level inventory: 108 HAR-observed endpoints, four permission-discovered additions, later live/implemented routes, and rejected legacy/unverified route names.

## Current evidence baseline after Round 02

- A captured Solar of Things web session documents 405 API requests and 108 unique `/apis/` routes.
- The current upstream HAR-derived catalog contains 112 unique routes because four additional routes were recovered from permissions returned by login rather than from the original HAR traffic.
- Later independent/live sources add at least three important routes outside that original 108-route snapshot: token refresh, DTU-to-device lookup, and a device PV-inverter daily-detail overview route.
- Account login, `IOT-Token` session authentication, station/device discovery, latest telemetry, energy flow, attribute metadata, selected-key historical telemetry, statistical overviews and remote device configuration are all supported by substantial public evidence.
- The exact IOT Open signing preimage still has a documented implementation disagreement. Multiple older/independent clients support the Base64 construction; one later SDK implements a hex construction. This is intentionally unresolved pending official-client verification.
- Device fields, units, signs, data-source choices and writable configuration keys vary materially across hardware/firmware families.

## Remaining dedicated research branches

- official production web-application archaeology;
- alternate web environment comparison (`test.solar`, `doc.solar`, `demo.doc.solar`) if still useful after production mapping;
- Android static/historical package archaeology;
- dongle-to-cloud MQTT/uplink archaeology;
- BLE / Proximal Monitoring archaeology;
- raw inverter serial protocol archaeology;
- cross-surface reconciliation and controlled read-only live validation after static evidence is mature.

## Next-round decision

**Round 03 — Official production web-application archaeology** remains standalone.

It should focus on the current production `solar.siseli.com` JavaScript/assets. Round 02 has provided a precise route/header/auth dictionary, and the official web client is now the strongest next source for resolving the signing contradiction, validating the current endpoint set, exposing route/permission definitions and identifying functionality not exercised by the July HAR.

Round 03 should not be merged with Android analysis or alternate-environment comparison: keeping those sources separate preserves provenance and makes later cross-checking stronger.
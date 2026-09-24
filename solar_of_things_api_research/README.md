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

## Investigation structure after Round 01

Round 01 showed that the previously planned broad "Deep GitHub archaeology" round would combine four materially different protocol surfaces. To preserve rigor, repository archaeology is split rather than merged:

- **Round 02 — Cloud REST API repository archaeology**: cloud clients, authentication, tokens/signatures, endpoints, models, history and implementation disagreements.
- Later dedicated rounds will separately cover:
  - dongle-to-cloud MQTT/uplink archaeology;
  - BLE / Proximal Monitoring archaeology;
  - raw inverter serial protocol archaeology.

The web application and Android application will also retain their own later source-of-truth analysis rounds so third-party implementations can be cross-checked against official clients.

## Next-round decision

Round 02 remains standalone and should **not** be merged with web-app or Android archaeology. The independent cloud-client corpus is already large enough to justify a dedicated deep pass, and its extracted endpoint/header/auth vocabulary will make the later official-client analysis more systematic and less speculative.

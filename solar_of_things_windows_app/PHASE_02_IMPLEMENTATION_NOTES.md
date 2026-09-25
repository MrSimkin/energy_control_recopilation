# Phase 2 — Authentication and Commissioning Implementation Notes

Date started: 2026-09-25

Status: IMPLEMENTATION IN PROGRESS

## Scope

Build the first real read-only connection to Solar of Things and create a per-device capability profile. Do not perform full historical backfill yet.

## Authentication/session implementation

Implemented:

- production REST base `https://solar.siseli.com/apis/`;
- compact JSON request serialization;
- protocol-required MD5 password preprocessing;
- IoT Open Base64/HMAC signing model from completed research;
- 32-character random hexadecimal nonce;
- signed `POST /login/account`;
- authenticated `IOT-Token` calls;
- preferred refresh body `{ accessToken, refreshToken }` with `{ refreshToken }` compatibility fallback;
- proactive refresh five minutes before known expiry;
- single-flight refresh;
- bounded transient retry;
- Windows DPAPI protected session/credential storage;
- optional remembered account/password/session;
- advanced manual access/refresh token-pair bootstrap.

No user password, token or reusable client secret is stored in SQLite.

## IoT Open client material

The production web-client ecosystem exposes the client App ID and encrypted client secret as public client-side material. The Windows app now carries that same encrypted production client profile so normal account/password login does not require manual DevTools token extraction.

Supported client-profile behavior:

- built-in production client profile = normal default;
- App ID + encrypted/plain client secret can be overridden locally using Windows DPAPI;
- local environment import using `SOLAR_OF_THINGS_APP_ID` plus `SOLAR_OF_THINGS_APP_SECRET_ENC` or `SOLAR_OF_THINGS_APP_SECRET`;
- one-click return to the built-in production profile;
- existing access/refresh token pair remains an advanced development bootstrap.

User account passwords/tokens are never committed to source control or written to diagnostic exports.

## Commissioning implementation

Read-only workflow:

1. authenticate / restore local session;
2. paginated `station/list`;
3. `station/details`;
4. paginated `device/list`;
5. `device/details`;
6. `gatherAttributes/v1`;
7. test evidence-backed `dataSource` values 1 and 2 using latest-state;
8. energy-flow capability probe;
9. narrow recent selected-key history capability probe;
10. daily aggregate capability probe;
11. alarm-query capability probe;
12. persist capability profile in SQLite schema v3.

All Solar of Things IDs are handled as strings to avoid JavaScript-style precision loss.

No device mutation, configuration write, firmware operation or control call is part of the commissioning path.

## Sanitized development diagnostics

The user explicitly requested a development/debug surface capable of giving the developer the detailed evidence needed to understand real-account behavior without relying on the user to manually interpret API responses.

This is a first-class Phase 2 feature.

Each API event can preserve:

- UTC timestamp;
- correlation ID;
- operation and commissioning step;
- exact method and endpoint;
- attempt number;
- HTTP status;
- Solar API code and message;
- elapsed milliseconds;
- sanitized request JSON;
- sanitized response JSON;
- exception type and message.

The exported report also includes:

- application/build version when available;
- OS/runtime/architecture;
- portable-mode state;
- database path.

Automatic redaction occurs **before the API diagnostic JSONL is written** and removes fields matching:

- password / password hash;
- access token;
- refresh token;
- IOT token;
- authorization;
- cookies;
- secrets;
- signatures / IOT Open sign.

Non-secret station/device IDs and metadata remain in the report because exact identifiers can be necessary to diagnose errors such as `20101 Illegal argument` and account/device capability differences.

UI actions:

- copy sanitized diagnostic report;
- save sanitized diagnostic report under `Logs/Exports`;
- open local Logs folder;
- commissioning progress/evidence view.

## Automated validation added

Smoke-test coverage now includes:

- database schema v3 and v2→v3 migration path;
- deterministic IoT Open body-hash/signing vector;
- commissioning-profile SQLite round trip;
- diagnostic redaction test proving tokens/passwords do not survive while device metadata remains;
- existing settings/JSONL/DPAPI tests.

## Windows CI validation — PASS

Workflow:
- Windows Build
- run `36177864396`
- source commit `a013708164b593bc1d072f94eec083216a4181c2`
- conclusion: **SUCCESS**

Artifact:
- `SolarEnergyMonitor-win-x64-dev`
- ID `10883196259`
- SHA-256 `9879c97adeb37b025e95c595b566e4a7939d0342b6c7745fc7bcfad0a5add9ac`

Passed:
- restore/build;
- schema v2 smoke test;
- deterministic IoT Open body hash/signing vector;
- commissioning-profile round trip;
- DPAPI secret-store test;
- API diagnostic JSON/body redaction;
- API header redaction while retaining safe response/request metadata;
- self-contained win-x64 publish.

## Remaining checkpoint

- Run the portable artifact against the user's real account/device using only local credentials/session material.
- Generate a fresh portable development artifact.
- User performs local read-only connection/commissioning.
- User copies the sanitized diagnostic report into the development conversation.
- Repair any target-account/device-specific discrepancy shown by that report.
- Phase 2 closes only when authentication/discovery/profile persistence/reconnect behavior are validated.

Do not begin Phase 3 full history backfill until Phase 2 is accepted.


## First real-account checkpoint — 2026-09-25

The user ran the portable Phase 2 build locally using an existing Solar of Things access token copied from the official portal. The token itself was not shared with the developer.

Observed from the sanitized report:

- application startup on schema v2: PASS;
- manual local token-pair bootstrap: PASS;
- production `POST /apis/station/list`: PASS, HTTP 200 / API code 0;
- exactly one accessible station discovered;
- production `POST /apis/device/list` with the discovered station ID: PASS, HTTP 200 / API code 0;
- exactly one device discovered;
- production response identifies the expected station timezone and supplies real inverter/device metadata including model, manufacturer, gather protocol, software version, online state and direct-generation fields;
- platform IDs remain correctly preserved as strings;
- diagnostic IOT-Token redaction worked.

The report stopped after device discovery; full commissioning probes have not yet been exercised.

### Privacy correction discovered from the first report

The first diagnostic exporter correctly removed credentials/tokens but retained unnecessary personal account/location fields present in the raw platform response.

Before requesting another pasted report, diagnostics were hardened to redact personal account/user identifiers and location/address/coordinate fields, and to avoid exporting absolute local filesystem paths.

Device/station IDs and non-secret technical device metadata remain available because they are needed for protocol debugging.

Commissioning was also made tolerant of a failure/mismatch in station/device detail endpoints: it now falls back to the already-valid list payloads and continues later capability probes so one optional endpoint cannot prematurely end the diagnostic run.


## Phase 2 development advance after first live discovery

The first production response exposed additional device metadata that is now promoted into the structured commissioning profile:

- device sort key;
- device type number;
- rated power;
- online state;
- source freshness via `lastDataAt`.

The raw payload remains preserved as well.

The alarm capability probe now supplies the discovered device serial/DTU context and a bounded recent time window.

Station/device detail endpoint failures are non-fatal during commissioning: list payloads are used as fallback evidence so later capability probes still execute.

## Latest automated validation — PASS

Workflow:
- Windows Build
- run `36180218439`
- source commit `a357acce1b299b5b743275675bc094f775053ca6`
- conclusion: **SUCCESS**

Artifact:
- `SolarEnergyMonitor-win-x64-dev`
- ID `10883354925`
- SHA-256 `cea58c38ef384712a9e1ada3a0137ccafb6d80ec96018a50c384612cc572df1a`

This supersedes the earlier Phase 2 development artifact for live testing.

Next live test should use normal account/password login and run the entire read-only commissioning sequence.

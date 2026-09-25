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
- current preferred refresh body `{ accessToken, refreshToken }`;
- single-flight refresh;
- bounded transient retry;
- Windows DPAPI protected session/credential storage;
- optional remembered account/password/session;
- advanced manual access/refresh token-pair bootstrap.

No user password, token or reusable client secret is stored in SQLite.

## IoT Open client material

The research deliberately did not commit a reusable application secret.

The application therefore supports local-only configuration:

- App ID + encrypted client secret stored with Windows DPAPI;
- local environment import using `SOLAR_OF_THINGS_APP_ID` plus `SOLAR_OF_THINGS_APP_SECRET_ENC` or `SOLAR_OF_THINGS_APP_SECRET`;
- existing access/refresh token pair as an advanced development bootstrap.

No client-secret value is written to source control or diagnostics.

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
12. persist capability profile in SQLite schema v2.

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

- database schema v2;
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

# Phase 2 — Automated Validation Receipt

Date: 2026-09-25

Status: **AUTOMATED PASS / REAL-ACCOUNT COMMISSIONING PENDING**

Repository:
`MrSimkin/energy_control_recopilation`

## Validated source

- source commit: `a013708164b593bc1d072f94eec083216a4181c2`
- workflow: Windows Build
- run ID: `36177864396`
- conclusion: **SUCCESS**

## Portable artifact

- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10883196259`
- SHA-256: `9879c97adeb37b025e95c595b566e4a7939d0342b6c7745fc7bcfad0a5add9ac`

## Automated checks passed

- .NET 10 restore;
- Windows x64 WPF Release compile;
- SQLite schema v2 creation/migration;
- app-setting round trip;
- JSONL diagnostics;
- deterministic IoT Open body-hash/signature test vector;
- commissioning capability-profile SQLite round trip;
- Windows DPAPI secret save/read/delete;
- sanitized diagnostic response/body redaction;
- sanitized request-header redaction;
- preservation of safe device/request metadata in diagnostic output;
- self-contained win-x64 publish;
- portable marker;
- artifact upload.

## Security properties validated mechanically

The development diagnostic report does not retain tested password/token values.

The diagnostic model is designed to redact:
- password/password hash;
- access/refresh/IOT tokens;
- authorization/cookies;
- reusable client secrets;
- request signatures.

It deliberately retains non-secret device/station identifiers and safe HTTP/API metadata needed for debugging.

## Remaining live acceptance

Automated CI cannot prove the Solar of Things account/device-specific protocol.

Next test must run locally on the user's Windows PC:

1. establish a local Solar of Things session without sharing credentials in chat;
2. discover station(s);
3. discover device(s);
4. run read-only commissioning;
5. copy the sanitized development diagnostic report;
6. inspect/repair any target-device-specific mismatch;
7. prove reconnect/session behavior.

Phase 2 remains **IN PROGRESS** until those checks pass.

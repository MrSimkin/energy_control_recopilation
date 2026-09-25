# Phase 2 — Automated Validation Receipt

Date: 2026-09-25

Status: **AUTOMATED PASS / REAL-ACCOUNT COMMISSIONING PENDING**

Repository:
`MrSimkin/energy_control_recopilation`

## Validated source

- source commit: `a357acce1b299b5b743275675bc094f775053ca6`
- workflow: Windows Build
- run ID: `36180218439`
- conclusion: **SUCCESS**

## Portable artifact

- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10883354925`
- SHA-256: `cea58c38ef384712a9e1ada3a0137ccafb6d80ec96018a50c384612cc572df1a`

## Automated checks passed

- .NET 10 restore;
- Windows x64 WPF Release compile;
- SQLite schema v3 creation/migration;
- app-setting round trip;
- JSONL diagnostics;
- deterministic IoT Open body-hash/signature test vector;
- built-in production client profile decryption/default/override behavior;
- commissioning capability-profile SQLite round trip including promoted device metadata;
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


## Superseding Phase 2 build

This receipt now points to the later green build that includes:

- normal account/password login using the production client profile;
- dual-form refresh compatibility;
- proactive pre-expiry refresh;
- schema v3 commissioning metadata;
- richer alarm probe context;
- non-fatal detail-endpoint fallback.

The prior live token-bootstrap discovery remains valid evidence, but the next acceptance test should use normal account/password login.

# Phase 2 — Automated Validation Receipt

Date: 2026-09-25

Status: **AUTOMATED PASS / REAL-ACCOUNT COMMISSIONING PENDING**

Repository:
`MrSimkin/energy_control_recopilation`

## Validated source

- source commit: `7fc2d5701f34948e8181c3e555c2d3cdaadcde9b`
- workflow: Windows Build
- run ID: `36180523465`
- conclusion: **SUCCESS**

## Portable artifact

- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10883867535`
- SHA-256: `f315669a111af582e390c50289ba38b2a04e96d1a3e38bbb0d811fa686dfc8c1`

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
7. close/reopen and prove protected-session restore/reconnect behavior;
8. exercise server logout and confirm local session cleanup.

Phase 2 remains **IN PROGRESS** until those checks pass.


## Superseding Phase 2 build

This receipt now points to the later green build that includes:

- normal account/password login using the production client profile;
- dual-form refresh compatibility;
- proactive pre-expiry refresh;
- schema v3 commissioning metadata;
- richer alarm probe context;
- non-fatal detail-endpoint fallback;
- proactive refresh lifecycle;
- safe server logout lifecycle;
- string-preserving logout/user-ID contract.

The prior live token-bootstrap discovery remains valid evidence, but the next acceptance test should use normal account/password login.

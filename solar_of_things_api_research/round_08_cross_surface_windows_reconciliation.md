# Round 08 — Cross-Surface Reconciliation for the Windows Project

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

## Purpose

This round reconciles the usable evidence from production REST traffic, first-party Swagger/OpenAPI documentation, current web-client archaeology, Android/mobile context, and BLE / Proximal Monitoring into one project-oriented capability map.

The goal is not to discover more endpoints for their own sake. It is to determine:

- which functions are genuinely usable from Windows;
- which require only the cloud API;
- which are local/BLE alternatives;
- which functions have dual cloud/local paths;
- which researched branches are irrelevant because of project constraints;
- what still materially blocks implementation;
- whether more broad protocol research is justified before the user explains the actual Windows project.

---

## 1. Primary architectural conclusion

The strongest architecture for the unspecified Windows project is:

**Windows application -> Solar of Things HTTPS API**

with an optional independent local path:

**Windows application -> BLE GATT -> RWB1 logger -> inverter**

The cloud path is primary because it is substantially reconstructed, does not depend on Android, does not require physical interception, and already exposes monitoring, history, alarms, analytics, configuration, management and control surfaces.

The BLE path is optional because it is local/range-bound and device/protocol dependent, but it can provide cloud-independent diagnostics, provisioning and direct telemetry without interfering with the logger-to-cloud MQTT path.

---

## 2. Explicitly excluded dependency/path

### Dongle-to-cloud MQTT interception

Disposition:

**SKIPPED BY PROJECT CONSTRAINT**

The project cannot intercept, proxy, redirect or otherwise intervene in the logger's cloud communication.

Consequences:

- no ARP-spoof/proxy architecture;
- no MQTT broker substitution;
- no requirement to decode cloud uplink packets;
- no dependency on packet interception for telemetry;
- MQTT archaeology is removed from the critical research path.

This does not reduce the viability of the Windows project because both the REST API and BLE local path are independent of MQTT interception.

---

## 3. Capability map

| Capability | Cloud REST | BLE/local | Best current project path |
|---|---|---|---|
| Account login/session | yes | no | cloud |
| Token refresh/logout | yes | no | cloud |
| Station discovery | yes | no | cloud |
| Device discovery/details | yes | limited logger identity only | cloud |
| Current inverter telemetry | yes | yes on supported serial protocol | cloud primary, BLE optional |
| Energy-flow model | yes | can be derived locally on supported protocol | cloud primary |
| Attribute/schema metadata | yes | protocol-specific | cloud |
| Historical telemetry | yes | not intrinsically stored by BLE transport | cloud |
| Alarms/history | yes | current status/fault bits possible locally | cloud primary |
| Aggregate daily/monthly/yearly energy | yes | limited device counters possible | cloud |
| Configuration cache/read | yes | possible through UART/local protocol | cloud primary |
| Configuration writes | yes | possible through BLE UART | optional; explicit control module only |
| Raw passthrough | yes, cloud-mediated | yes, BLE CID 30024 | depends on future requirement |
| Logger version | indirectly/device metadata | yes | either |
| Logger Wi-Fi status | cloud metadata indirect | yes | BLE if local diagnostics needed |
| Wi-Fi AP scan/provisioning | no ordinary cloud equivalent | yes | BLE only |
| Local security password | no | yes | BLE only |
| Firmware/admin management | documented cloud APIs | not needed | cloud, if ever required |
| Device/station CRUD | yes | no | cloud |
| Peak-valley / automation | documented/gated cloud APIs | raw local control could emulate pieces | cloud if supported |
| Local protocol generation/parsing | `/near/dtu/*` cloud helper service | BLE carries frames | hybrid, unvalidated |
| MQTT traffic inspection | intentionally excluded | no | not available to project |

---

## 4. Cloud-only functions that are already sufficiently understood

These functions have no material dependency on Android, BLE or raw serial:

### Authentication/session

- account/password login;
- MD5 password preprocessing required by the platform;
- IoT Open request signing;
- `IOT-Token` authenticated session;
- access+refresh token refresh;
- rotating refresh token semantics;
- logout.

### Discovery

- stations;
- station details;
- devices;
- device details;
- account/role metadata;
- device/model/gather-protocol metadata.

### Monitoring

- latest state;
- simple energy flow;
- device attribute metadata;
- current alarms;
- alarm history/reporting.

### History and analytics

- selected-key historical telemetry;
- record-list alternatives;
- device/station/owner daily/monthly/yearly aggregates;
- generated energy summaries;
- reporting/export APIs.

### Management

- device/station CRUD;
- user/account operations;
- configuration read/write;
- firmware/upgrade APIs;
- automation and peak-valley APIs where role/manufacturer enabled.

These areas no longer justify broad reverse-engineering rounds unless the eventual project selects a feature whose exact contract remains uncertain.

---

## 5. Minimal Windows cloud implementation surface

Round 06 identified a 12-contract P0 core:

1. `POST /login/account`
2. `POST /login/refresh/access/token`
3. `POST /login/logout`
4. `POST /station/list`
5. `GET /station/details`
6. `POST /device/list`
7. `GET /device/details`
8. `GET /deviceState/simple/gatherAttributes/v1`
9. `GET /deviceState/simple/state/latest/v1`
10. `GET /deviceState/simple/energy/flow/v1`
11. `POST /deviceState/simple/attribute/keys/history/v1`
12. `POST /alarm/query/list`

This subset is enough for a useful Windows monitoring/analytics application.

Everything beyond it should be pulled in by project requirements, not by completeness pressure.

---

## 6. Cloud authentication facts now mature enough for implementation

Production base:

`https://solar.siseli.com/apis`

Login:

`POST /login/account`

Password transformation:

`MD5(UTF-8 plaintext password).lowercaseHex`

Current high-confidence Open-sign algorithm:

1. serialize the exact compact request body;
2. non-GET body hash = SHA-256 of exact serialized body;
3. GET body hash = empty string;
4. combine URL query parameters with `IOT-Open-AppID`, `IOT-Open-Nonce`, `IOT-Open-Body-Hash`;
5. sort by parameter name;
6. join as `key=value&...` without URL-encoding the canonical values;
7. Base64 encode the UTF-8 canonical string;
8. HMAC-SHA256 using the application secret;
9. MD5 the raw HMAC bytes;
10. lowercase hex becomes `IOT-Open-Sign`.

Normal authenticated traffic uses:

`IOT-Token`

Refresh:

`POST /login/refresh/access/token`

Preferred current body:

`{ accessToken, refreshToken }`

Refresh-token chain must be serialized and atomically replaced because current live evidence indicates rotation/single-use semantics.

---

## 7. What still needs per-device validation in the cloud API

The cloud protocol is understood, but the user's actual hardware can still vary.

Per-device facts that should be learned dynamically rather than assumed:

- exact device model;
- machine type;
- gather protocol number/version;
- supported `dataSource` value;
- telemetry field names;
- units;
- grid/battery sign conventions;
- available PV channels;
- writable configuration keys;
- feature/manufacturer flags;
- energy-flow availability;
- supported analytics categories.

Therefore the Windows implementation should preserve raw metadata before normalizing it.

---

## 8. BLE/local path after Round 07

BLE is technically viable from Windows and independent of Android.

High-confidence transport profile:

- GATT service FEE7;
- write FED5;
- indicate FED6;
- AES-128-CBC;
- IV = key;
- zero padding;
- Base64;
- three-byte fragment header;
- sequential request/response command model.

Preserved protocol implementation derives the transport key as:

`MD5(DTUID + "SEC_")`

while an optional user-defined Proximal Monitoring security password is a separate gate.

BLE also exposes CID 30024/30025 as a generic UART tunnel.

This makes BLE useful as an optional local subsystem, but it is not needed for the main cloud implementation.

---

## 9. Cloud/local dual-path functions

Several functions exist in both worlds.

### Current telemetry

Cloud:

`/deviceState/simple/state/latest/v1`

Local:

BLE UART request(s) appropriate to the device gather protocol.

Recommendation: cloud primary; local only if latency/offline/diagnostic requirements justify it.

### Energy flow

Cloud:

`/deviceState/simple/energy/flow/v1`

Local:

derive from serial fields where protocol semantics are known.

Recommendation: prefer cloud because server-side model rules already absorb device differences.

### Configuration

Cloud:

`/remote/device/config/*`

Local:

BLE UART tunnel plus device-specific serial protocol.

Recommendation: cloud first; local configuration only if the project explicitly needs offline control.

### Raw device protocol

Cloud:

`/remote/device/passthrough`

Local:

BLE CID 30024/30025.

Recommendation: treat both as advanced control surfaces, not monitoring primitives.

---

## 10. The `/near/dtu/*` bridge is strategically interesting but not a blocker

The first-party documented `/near/dtu/*` service can generate and parse low-level device protocol messages.

This may bridge cloud knowledge and local BLE transport:

`Windows -> /near/dtu generator -> BLE UART -> device -> BLE response -> /near/dtu parser`

If that workflow works for the user's device, it could eliminate much custom serial-protocol implementation.

However, it is not required for the P0 cloud client and has not been end-to-end validated.

Do not spend another broad round on it until the project requirements indicate a need for local/device-level operation.

---

## 11. Raw serial should remain optional

A dedicated raw-serial archaeology round is no longer justified merely because it was in the original research plan.

Reasons:

- direct serial access may be physically unavailable or unacceptable;
- protocols vary by inverter family;
- the cloud API already provides normalized monitoring;
- cloud passthrough and BLE passthrough provide two alternative ways to reach lower-level functionality;
- `/near/dtu/*` may provide protocol generation/parsing.

Reopen raw serial only if the project needs a function that cannot be met by cloud REST or BLE transport.

---

## 12. Material implementation blockers

After eight research stages, only a small number of unknowns actually block a production implementation.

### Blocker A — actual project requirements are intentionally not yet known

Without knowing what the Windows application must do, it is impossible to decide which of the 274 routes beyond P0 are relevant.

This is now the largest uncertainty, not API discovery.

### Blocker B — live validation against the user's account/device

Public evidence reconstructs the protocol well, but the user's own account/device must eventually confirm:

- login works with the chosen application identity;
- role flags;
- station/device IDs;
- model/gather protocol;
- actual telemetry schema;
- valid `dataSource`;
- history behavior;
- alarms;
- any optional configuration/control capabilities the project needs.

This validation can be read-only until a specific control feature is deliberately authorized.

### Blocker C — application signing credential handling

A Windows client needs an accepted IoT Open application identity/credential to perform signed operations such as login.

Public clients have reconstructed working application identities, but reusable application secret values have intentionally not been copied into this research repository.

Implementation must define how that public-client credential is supplied/stored without committing it to source control.

### Blocker D — undefined polling/performance target

No authoritative universal rate limit has been found.

The correct polling cadence depends on whether the project needs:

- dashboard-scale monitoring;
- near-realtime control;
- periodic archival;
- background alarms;
- or occasional diagnostics.

This is a project requirement, not something further generic research can resolve.

---

## 13. Important unknowns that are NOT implementation blockers

These remain unresolved but should not delay a normal Windows cloud prototype:

- exact current Android binary internals;
- dongle-to-cloud MQTT encoding;
- complete raw inverter protocol catalog;
- unverified WebSocket endpoint;
- purpose of `demo.doc.solar.siseli.com`;
- every one of the 274 routes being currently enabled;
- exact semantics of every rare business error;
- direct byte extraction of current Umi auth middleware;
- every historical application identity.

Researching these before project requirements are known would have sharply diminishing returns.

---

## 14. Evidence maturity by subsystem

| Subsystem | Maturity | Ready for prototype? |
|---|---|---|
| Cloud auth/signing | high | yes |
| Session/refresh | high | yes |
| Station/device discovery | high | yes |
| Current telemetry | high | yes |
| Attribute/schema discovery | high | yes |
| History | very high | yes |
| Energy-flow API | high, device-dependent | yes with capability fallback |
| Alarms | high | yes |
| Aggregate analytics | high | yes when needed |
| Config read | high | yes, explicit feature |
| Config write/control | medium-high contract knowledge, device-dependent risk | not by default |
| Device/station management | documented/high | only if required |
| Automation/peak-valley | documented but gated | only after device validation |
| BLE transport | high | yes as optional local prototype |
| BLE user-password semantics | high-medium | yes with respectful prompt flow |
| BLE UART protocol for all device families | variable | only per-device |
| `/near/dtu/*` hybrid workflow | promising but unvalidated end-to-end | not required |
| Raw serial | family-specific | optional |
| MQTT interception | excluded by project constraint | no |
| WebSocket | unverified | no |

---

## 15. Recommended implementation boundary before requirements are known

If implementation had to begin without any more project detail, the safest generic boundary would be:

### Include

- secure local configuration;
- login/refresh/logout;
- station/device discovery;
- lossless ID handling;
- attribute/schema discovery;
- latest telemetry;
- energy flow with graceful unsupported handling;
- historical selected-key data;
- alarms;
- conservative polling;
- raw-response diagnostics with secrets redacted.

### Keep feature-flagged / optional

- aggregate analytics;
- cached configuration reads;
- active configuration reads;
- BLE diagnostics.

### Exclude until explicitly required

- configuration writes;
- cloud passthrough;
- BLE serial writes;
- firmware upgrade;
- station/device CRUD;
- scheduled automation;
- peak-valley writes;
- Wi-Fi provisioning;
- account mutations.

---

## 16. Research saturation assessment

Broad API/protocol discovery has reached the point of diminishing returns for the Windows objective.

We now have:

- product/ecosystem identity;
- a broad public-source census;
- deep cloud-client archaeology;
- current production-web verification;
- first-party Swagger/OpenAPI reconstruction;
- 274-route canonical map;
- a 12-route Windows core;
- role/capability/gating model;
- mature auth/refresh model;
- BLE local-transport reconstruction;
- explicit exclusions for Android binary and MQTT interception;
- a rational reason not to force raw serial research.

Another generic discovery round is less valuable than learning what the actual Windows project must accomplish.

---

## 17. Next-step decision

**Do not automatically launch another broad research round.**

The next productive step should be driven by the user's project definition.

Once the project requirements are known, select one of three paths:

### A. Cloud monitoring/analytics project

Proceed directly to a read-only live-validation plan and then implementation design using the P0/P1 REST contract.

### B. Cloud control/management project

Perform a targeted capability-and-safety validation for only the required mutation endpoints before implementation.

### C. Local/offline Windows project

Perform a targeted BLE + `/near/dtu/*` validation for the user's actual logger/inverter. Only add raw-serial archaeology if that targeted work proves necessary.

This preserves research rigor while avoiding work on protocols the eventual product will never use.

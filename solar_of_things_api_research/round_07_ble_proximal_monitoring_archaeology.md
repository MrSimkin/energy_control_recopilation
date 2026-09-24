# Round 07 — BLE / Proximal Monitoring Archaeology

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

## Scope and project routing

The previously planned dongle-to-cloud MQTT/uplink round is intentionally **SKIPPED BY PROJECT CONSTRAINT**. The Windows project cannot intercept, proxy, redirect, or otherwise intervene in that communication path.

BLE / Proximal Monitoring is a separate local path between a computer/phone and the Solar Plug / RWB1 logger. It does not require Android at runtime and does not require MQTT interception.

No BLE command was sent to a real device during this research. Device-changing actions remain out of scope.

---

## 1. Official functional context

The Solar of Things owner documentation describes Proximal Monitoring as direct Bluetooth access to a nearby supported device, including live data, control, alarms and debugging. It also describes a user-configurable local security password.

Solar Plug RWB1 documentation identifies the logger as a BLE/Wi-Fi data collector bridging the inverter serial interface to the Solar of Things ecosystem.

This establishes that BLE is a supported first-party local access path rather than an artifact of third-party reverse engineering.

---

## 2. GATT profile reconstructed from official-app traffic

The strongest independent capture is `keiprojects/solar-of-things-ha-ble`, reconstructed from an Android Bluetooth HCI snoop capture of Solar of Things 3.1.11 communicating with an RWB1 logger.

Observed GATT endpoints:

- service: `0000fee7-0000-1000-8000-00805f9b34fb`
- write characteristic: `0000fed5-0000-1000-8000-00805f9b34fb`
- indication/response characteristic: `0000fed6-0000-1000-8000-00805f9b34fb`

Independent provisioning code in `lujian1324-spec/energy-app` uses the same FEE7/FED5/FED6 profile.

Confidence: VERY HIGH.

---

## 3. BLE application framing

Each BLE application fragment uses a three-byte header:

`[index, total, payloadLength]`

Properties:

- `index` is 1-based;
- `total` is the number of fragments;
- `payloadLength` is the number of following application bytes;
- fragments must be reassembled in sequence-number order, not notification-arrival order.

The Android HCI capture showed a small-MTU stage carrying about 18 application bytes per fragment and a later negotiated ATT MTU of 247 where a 216-byte application payload could fit in one fragment.

A robust implementation should derive fragment size from the negotiated MTU rather than assume one constant.

Public implementations also found that long replies can arrive out of order. Correct reassembly therefore waits until every fragment 1..N is present before concatenation and decryption.

---

## 4. Application encryption

Two independent implementation lines converge on:

- AES-128-CBC;
- IV equal to the AES key bytes;
- zero-byte padding to a 16-byte boundary;
- Base64 ASCII encoding after encryption;
- encrypted/Base64 application data fragmented only after encryption.

The preserved provisioning implementation additionally derives the transport key as:

`AES_KEY_HEX = MD5(DTUID + "SEC_")`

The 32-character MD5 hex output represents the 16 AES key bytes, and the same bytes are used as the IV.

This derivation appeared in the energy-app history on 2026-05-22, before the later HCI-capture Home Assistant project. The HCI project independently confirms the encryption mode, IV=key, padding and Base64 but originally required the AES key to be entered manually.

Confidence in the DTUID derivation: HIGH, but not first-party-binary verified in this investigation.

---

## 5. Transport AES key is not the user BLE password

Two separate layers exist.

### Transport encryption

Machine-derived key:

`MD5(DTUID + "SEC_")`

This protects/encodes the BLE application envelope.

### Optional Proximal Monitoring security password

The official user documentation describes a user-configurable security password. The preserved protocol implementation models its verification separately:

- request CID: `30050`
- response CID: `30051`
- payload field: `BleKey`
- response `RC=0`: success
- response `RC=9000`: security password required
- response `RC=9001`: incorrect password
- response `RC=1`: generic execution failure

Therefore automatically deriving the transport AES key does not bypass the user's configured Proximal Monitoring security password. A future Windows client must respect that gate.

---

## 6. Logger identity exposed through BLE

The preserved protocol implementation models the advertised device name as:

`<prefix><status><base64(DTUID bytes)>`

Observed/default prefix:

`SSL_`

Status digit semantics:

- `0`: Wi-Fi not connected
- `1`: Wi-Fi connected
- `2`: reserved
- `3`: MQTT connected

The payload resolves to a 20-digit DTUID/logger identifier.

If the scan advertisement does not contain a complete parseable name, the implementation can read the standard GAP Device Name characteristic after connecting:

- Generic Access service: `00001800-0000-1000-8000-00805f9b34fb`
- Device Name characteristic: `00002a00-0000-1000-8000-00805f9b34fb`

This gives a Windows implementation two ways to obtain the logger identity.

---

## 7. BLE command/CID catalog

The preserved provisioning implementation contains these request/response pairs:

| Request | Response | Function | Risk class |
|---:|---:|---|---|
| 30001 | 30002 | device software/hardware version | read |
| 30003 | 30004 | scan Wi-Fi access points | read |
| 30005 | 30006 | configure Wi-Fi SSID/password | mutation |
| 30007 | 30008 | restart logger/device | mutation |
| 30009 | 30010 | get Wi-Fi configuration | read |
| 30011 | 30012 | get network configuration | read |
| 30013 | 30014 | diagnostics | read |
| 30015 | 30016 | UART configuration | read |
| 30017 | 30018 | BLE/security-key state | read |
| 30020 | 30021 | Wi-Fi/cloud connection status | read |
| 30022 | 30023 | network status | read |
| 30024 | 30025 | UART passthrough | read or mutation depending payload |
| 30050 | 30051 | verify local BLE security password | authentication |

No mutating CID was executed in this research.

---

## 8. Wi-Fi/cloud status available locally without MQTT interception

The CID 30020/30021 model exposes fields including:

- `WConn`: Wi-Fi connection state;
- `R`: Wi-Fi progress/error reason;
- `SConn`: MQTT/cloud connection state;
- `HSConn`: HTTP connection state;
- `RSSI`: Wi-Fi signal level;
- `SSID`: connected network.

Preserved reason-code semantics include:

- 1: connecting;
- 2: AP not found;
- 3: Wi-Fi password wrong;
- 4: other failure.

This is directly relevant to the project constraint: a Windows tool can inspect whether the logger believes MQTT/HTTP connectivity is healthy over BLE without intercepting the actual MQTT transport.

---

## 9. UART passthrough architecture

CID 30024/30025 is a generic serial tunnel.

Conceptually:

`Windows -> BLE -> RWB1 -> serial/UART -> inverter`

and the reverse path carries the response.

The BLE envelope transports raw serial request bytes in hexadecimal form and returns raw serial response bytes. BLE therefore does not define the inverter protocol itself.

This separates three layers:

1. BLE GATT transport;
2. RWB1 application envelope/UART bridge;
3. inverter-family serial protocol.

---

## 10. Multiple lower-level serial protocols exist

### Captured H-family example

For an RWB1 + POW-HVM6.2KP / HPVINV02-family capture:

- UART: 2400 baud, 8 data bits, no parity, 1 stop bit;
- request CID 30024 / response CID 30025;
- read-command metadata for gather protocol version code 44:

| Command | CmdNo | Data class |
|---|---|---|
| HSTS | eo8w | status, mode, fault bits |
| HGRID | WdRR | grid voltage/frequency/power |
| HOP | 2l0E | AC output/load |
| HBAT | 2ONL | battery voltage/SOC/currents |
| HPV | Mpod | PV voltage/current/power |
| HTEMP | V4W3 | temperatures/fan speeds |
| HGEN | COST | generation totals |

### Modbus-style example

Another public implementation uses the same BLE CID 30024/30025 tunnel to carry raw Modbus RTU frames for a different product family and configures different UART parameters, including 9600 baud in its direct-mode implementation.

This is not a contradiction. It demonstrates that the UART tunnel is generic and the serial protocol depends on inverter/gather protocol.

---

## 11. Read-only telemetry demonstrated by the captured H-family protocol

A local read-only client for the captured H-family can potentially expose:

- grid voltage/frequency/power and import/export direction;
- output voltage/frequency/apparent/active power and load percentage;
- battery type, voltage, SOC, charge current and discharge current;
- bus voltage;
- PV voltage/current/power;
- inverter, boost, transformer and PV temperatures;
- fan speeds;
- daily/monthly/yearly/total generation;
- operating mode;
- status/fault bit fields.

The keiprojects integration deliberately enables reads only.

Its battery-power normalization is:

`batteryVoltage * (chargingCurrent - dischargeCurrent)`

so positive means charging and negative means discharging in that implementation.

---

## 12. Relationship to the cloud `/near/dtu/*` service

Round 05 found 17 first-party-documented cloud routes under `/near/dtu/*` for local/proximal protocol support.

They include operations to:

- detect gather protocol;
- obtain gather-protocol metadata;
- generate config-read commands;
- generate config-write commands;
- generate batch reads;
- parse device state;
- parse events;
- parse energy flow;
- parse config read/write responses;
- retrieve protocol scripts and display assets.

This suggests a powerful possible Windows architecture:

1. discover/connect RWB1 over BLE;
2. obtain DTUID/device/gather-protocol metadata;
3. use `/near/dtu/*` to generate a low-level command;
4. transport it through BLE CID 30024;
5. submit the raw response to a `/near/dtu/*` parsing route.

That could let the SiSeLi backend perform protocol-specific command generation/parsing while Windows provides only the local transport.

This end-to-end sequence is an **architectural inference**, not a workflow executed or live-proven in this investigation.

---

## 13. Windows compatibility

BLE is not Android-specific.

Windows desktop applications can use the Windows Bluetooth LE / GATT APIs to discover devices, obtain GATT services and characteristics, write characteristic values and subscribe to notifications/indications.

A future native Windows implementation can therefore, in principle:

1. discover a device advertising FEE7 or an SSL_-style name;
2. connect to it;
3. obtain its DTUID;
4. derive the transport AES key;
5. subscribe to FED6 indications;
6. encrypt/frame requests and write them to FED5;
7. reassemble/decrypt responses.

No Android emulator or Android application is required.

Chromium-based Web Bluetooth is another possible Windows transport, but native Windows GATT is the cleaner OS-level architecture if local BLE becomes a product requirement.

---

## 14. Command sequencing requirements

Public implementations serialize BLE commands because every request/response shares the same FED5/FED6 channel.

A robust client should:

- permit only one in-flight command per logger;
- correlate the expected response CID;
- ignore stale/unsolicited indications when no request is pending;
- reset partial reassembly state on reconnect or a new response;
- use bounded timeouts;
- reconnect/retry safe reads where appropriate;
- never automatically retry an ambiguous write/control command.

Real implementations have encountered idle GATT disconnects and immediate replies arriving during the final write, so the response waiter should be armed before sending the command.

---

## 15. Security implications

### Deterministic transport encryption

If the DTUID key formula is correct, BLE transport encryption should not be treated as a strong user-authentication boundary. The DTUID is device identity material and the derivation is deterministic.

### User local-security password

The separate Proximal Monitoring password remains a meaningful authorization control. A Windows client should request and validate it when required and must not bypass or brute-force it.

### Wi-Fi provisioning secrets

Wi-Fi SSID/password can be carried by the provisioning protocol. Any future implementation must redact them from application/debug logs and keep them out of repository artifacts.

---

## 16. Confidence matrix

| Finding | Confidence | Basis |
|---|---|---|
| FEE7/FED5/FED6 GATT profile | very high | Android HCI capture + independent implementation |
| AES-128-CBC | very high | two implementation lines |
| IV equals key | very high | HCI protocol notes + second implementation |
| zero padding | very high | two implementation lines |
| Base64 ciphertext transport | very high | two implementation lines |
| 3-byte index/total/length framing | very high | two implementation lines |
| CID 30024/30025 UART tunnel | very high | HCI capture + separate implementation |
| DTUID encoded in BLE name | high | preserved protocol implementation + later device-debugging history |
| AES key = MD5(DTUID + SEC_) | high | preserved v2.9-style implementation; crypto mode independently corroborated |
| optional BLE password via 30050/30051 | high | preserved protocol implementation + official feature documentation |
| RC 9000/9001 meanings | high-medium | preserved implementation/spec-derived evidence |
| H-family 2400 8N1 command map | high for captured device family | Android HCI capture |
| same serial protocol across all SiSeLi devices | unsupported/false assumption | public evidence shows protocol variation |
| BLE can be implemented on Windows | very high | standard Windows BLE/GATT support |
| BLE requires Android | false | ordinary GATT transport |
| BLE requires MQTT interception | false | separate local path |

---

## 17. Project relevance

For the current Windows project, BLE should be classified as:

**OPTIONAL LOCAL FALLBACK / DIAGNOSTIC TRANSPORT**

not as a dependency of the main cloud client.

BLE becomes valuable if the eventual project requires:

- operation during cloud outages;
- lower-latency/fresher local telemetry;
- on-site diagnostics;
- logger Wi-Fi/cloud status inspection;
- local Wi-Fi provisioning;
- raw serial access through the existing RWB1 without replacing it.

If cloud access is sufficient, the Round 06 REST architecture remains simpler and primary.

---

## 18. Round-close decision

### MQTT/uplink branch

Disposition:

**SKIPPED BY PROJECT CONSTRAINT**

The project cannot intervene in the dongle-to-cloud MQTT communication path. This is an intentional scope decision, not an unresolved research requirement.

### Raw serial branch

Do **not** make raw serial the next mandatory round.

Reasons:

- direct serial work is inverter-family/protocol specific;
- it usually requires physical serial access, replacing/parallelizing the logger, or deliberately using the RWB1 passthrough;
- it is unnecessary for the cloud REST client;
- the BLE UART tunnel already exposes a possible local path without replacing the logger;
- `/near/dtu/*` may eventually reduce the need to implement every serial protocol locally.

Raw serial remains an optional branch to reopen only if the eventual project requires direct inverter-level communication.

### Recommended next round

**Round 08 — Cross-Surface Reconciliation for the Windows Project**

Goals:

- reconcile production REST, Swagger/OpenAPI, current web-client evidence and BLE;
- separate cloud-only, local-only and dual-path capabilities;
- identify what still materially blocks a Windows implementation;
- identify which unknowns require the user's actual project requirements or a controlled read-only account validation;
- avoid spending further research effort on transport surfaces the project cannot or will not use.

---

## Sources

Primary technical repositories:

- `https://github.com/keiprojects/solar-of-things-ha-ble`
- `https://github.com/lujian1324-spec/energy-app`

Official/vendor documentation examined:

- Solar of Things owner/user guide, Proximal Monitoring sections;
- Solar Plug RWB1 user manuals.

Platform documentation examined:

- Microsoft Windows Bluetooth LE / GATT documentation.

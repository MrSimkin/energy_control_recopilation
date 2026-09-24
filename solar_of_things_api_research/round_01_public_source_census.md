# Round 01 — Public-Source Census

Date: 2026-09-24

Status: COMPLETE

Purpose: discover and classify the publicly visible research corpus around Solar of Things / SiSeLi before deep analysis of any one implementation. This round is a census: it records where useful evidence exists, how the sources relate, and which branches deserve dedicated follow-up. It does not yet treat every implementation claim as independently verified protocol fact.

## Scope and method

Searches covered:
- Solar of Things and SiSeLi naming variants;
- the official portal/domain;
- Android package identifiers;
- GitHub repositories and code search;
- Home Assistant integrations and issue histories;
- inverter/dongle model identifiers;
- Solar Plug / RWB1 manuals;
- vendor and reseller documentation;
- Android package mirrors and historical versions;
- community reports and hardware compatibility discussions;
- terms already surfaced by independent reverse engineering such as IOT-Token, openAppSecret, RWB1, HPVINVxx, BLE service identifiers, MQTT cloud IPs and protocol command names.

The search deliberately remained broad. Deep validation of code, endpoint behavior, cryptographic/signature construction, commits, and runtime traffic is deferred to dedicated later rounds.

---

## 1. Main result: four distinct technical research branches exist

The public corpus is not a single body of duplicated work. It divides into four complementary protocol surfaces:

1. **Cloud REST / web API**
   - authentication and token handling;
   - station/device discovery;
   - telemetry and historical data;
   - configuration/control endpoints.

2. **Dongle-to-cloud MQTT/uplink traffic**
   - passive interception of the inverter Wi-Fi logger's traffic to SiSeLi;
   - MQTT framing and proprietary encoded payload blocks.

3. **Bluetooth / Proximal Monitoring**
   - BLE GATT services used by the Solar Plug RWB1;
   - encrypted application frames;
   - local read commands and UART tunneling.

4. **Raw inverter serial protocol**
   - RS232/UART commands beneath the Solar Plug logger;
   - direct local read-only replacements and protocol decoding.

This separation is important: cloud API investigation alone will not describe the full Solar of Things communications stack.

---

## 2. Highest-value cloud API sources

### 2.1 Conexo-Casa/solar-of-things-ha
Repository:
https://github.com/Conexo-Casa/solar-of-things-ha

Classification: **PRIMARY THIRD-PARTY REVERSE-ENGINEERING TARGET**

Why it matters:
- active Home Assistant integration;
- user/password authentication with automatic token refresh in recent versions;
- station/device discovery;
- real-time telemetry;
- monthly statistics;
- configuration/control support;
- extensive release history showing endpoint corrections based on actual portal behavior;
- public issues covering model/firmware incompatibilities.

High-value historical clues already visible in release notes:
- settings read endpoint was corrected to:
  POST /apis/remote/device/configs/cache/get?deviceId=<id>
- settings write endpoint was corrected to:
  POST /apis/remote/device/config/write?deviceId=<id>
  with a body containing deviceId, key and value;
- token refresh path corrected to:
  /apis/login/refresh/access/token
- login requires password preprocessing consistent with MD5 in the implementation/release notes; plaintext attempts were reported with API error code 7;
- fallback live-data endpoint:
  /apis/deviceState/simple/energy/flow/v1
- firmware/model variants can expose different field names;
- timezone/header mistakes can return error 20101 "Illegal argument";
- unsupported energy-flow rules can return 70132;
- unsupported configuration keys can return 70134.

These are leads, not yet independently revalidated in this census.

Public issues expose additional model-dependent behavior:
- Maniy 11 kW / 2-MPPT sensor mappings;
- FCHAO settings keys;
- UWB1/UWB1-02 differences;
- HPVINV04 / DatouBoss mapping issues;
- EASUN devices with RWB1-04 and RWB1-06;
- PowMr MEGA-ECO machineType=5;
- a live-data path under /state/latest/v1 on some devices.

### 2.2 X-c0d3/solar-of-things-hack
Repository:
https://github.com/X-c0d3/solar-of-things-hack

Classification: **PRIMARY INDEPENDENT CLOUD-API SOURCE**

README evidence:
- base URL is solar.siseli.com;
- uses account/password, device ID, timezone, app ID and an application secret;
- documents:
  /apis/device/details?deviceId=<DEVICE_ID>
- says stationId can be recovered from the device-details response;
- says appId/openAppSecret were recoverable from the web application's JavaScript bundle.

Important: no actual secret value is preserved in this research repository. Later analysis should document the mechanism and provenance, not publish credentials unnecessarily.

### 2.3 Hyllesen/solar-of-things-solar-usage
Repository:
https://github.com/Hyllesen/solar-of-things-solar-usage

Classification: **PRIMARY INDEPENDENT CLOUD-DATA SOURCE**

Observed capabilities:
- Node.js CLI;
- uses IOT token, station ID and device ID;
- reads time-series and monthly energy data;
- exposes metrics including PV input and AC output power;
- implements date-range chunking, reportedly because larger requests lose data;
- useful for reconstructing historical-data endpoints and response semantics.

### 2.4 cchampsan/SiseliSolarApp-HA
Repository:
https://github.com/cchampsan/SiseliSolarApp-HA

Classification: **HIGH-VALUE INDEPENDENT CLOUD-AUTH SOURCE**

README claims:
- direct SiSeLi cloud integration;
- handles signature and nonce security;
- targets the same white-label application family identified in Round 00.

This implementation is especially useful as an independent cross-check against the Conexo code path.

### 2.5 0leg7/ha-siseli-solar
Repository:
https://github.com/0leg7/ha-siseli-solar

Classification: **HISTORICAL AUTH/TOKEN SOURCE**

Observed design:
- Home Assistant integration uses IOT-Token;
- a separate updater performs browser login with headless Chromium/Puppeteer;
- token is refreshed on an approximately 110-minute cycle.

This may preserve behavior from an earlier stage before direct login/refresh was fully reconstructed.

### 2.6 aliwadah/solar-ac-monitor
Repository:
https://github.com/aliwadah/solar-ac-monitor

Classification: **SMALL INDEPENDENT CLOUD CLIENT**

Uses Solar of Things battery data for automation and accepts SiSeLi user/password/device ID configuration. It may provide an independently written minimal authentication/data path worth comparing to larger integrations.

### 2.7 Fork/copy family around Conexo
Observed repositories include:
- https://github.com/filipsworks/solar-of-things-ha
- https://github.com/GreyPeter/solar-of-things-ha
- https://github.com/whalejason-creator/solar-of-things-ha
- https://github.com/muradjabir/solar-of-things-ha
- https://github.com/augustynski-lukasz/solar-of-things-ha
- https://github.com/Nope-o/Solar-of-Things

Classification: **LINEAGE/HISTORY TARGETS**

They should not be treated as independent confirmation simply because they are separate repositories. Round 02 must determine ancestry, divergence and whether any preserve deleted or unreleased behavior.

---

## 3. Dongle-to-cloud MQTT/uplink sources

### 3.1 yuraantonov11/siseli-ha
Repository:
https://github.com/yuraantonov11/siseli-ha

Classification: **ORIGINAL/FOUNDATIONAL LOCAL-INTERCEPTION SOURCE**

Observed behavior:
- intercepts traffic from PowMr/RWB1-style inverter logger to SiSeLi cloud;
- can use ARP interception or manual routing;
- points to cloud target IP 8.212.18.157;
- decodes data while forwarding traffic so the official app continues to work.

### 3.2 fadmaz/siseli-ha
Repository:
https://github.com/fadmaz/siseli-ha

Classification: **PRIMARY MQTT/UPLINK REVERSE-ENGINEERING SOURCE**

README reports:
- Wi-Fi dongle publishes over MQTT to 8.212.18.157:1883;
- bridge reassembles TCP and extracts MQTT PUBLISH frames;
- proprietary payload contains base64 data blocks identified by four-character names such as 2ONL, WdRR and Yavb;
- current implementation exposes up to 207 sensors across seven logical devices;
- detailed validation exists for HPVINV04 firmware 0010.11 with byte-faithful capture fixtures.

This branch is especially valuable because it observes the data generated by hardware itself rather than the web REST API.

### 3.3 Descendants / related forks
Observed:
- https://github.com/fedora144/siseli-ha
- https://github.com/gravyflex/siseli-ha
- https://github.com/siselilocal/siseli-ha

Classification: **LINEAGE/HARDWARE-VARIANT TARGETS**

Some later forks mention additional UDP telemetry and stricter validation. They require history comparison to distinguish novel work from inherited code.

---

## 4. Bluetooth / Proximal Monitoring source

### keiprojects/solar-of-things-ha-ble
Repository:
https://github.com/keiprojects/solar-of-things-ha-ble

Classification: **PRIMARY BLE REVERSE-ENGINEERING SOURCE**

The README says an Android HCI capture establishes the following for an RWB1 logger / HPVINV02-type setup:

- BLE service: FEE7
- write characteristic: FED5
- indication characteristic: FED6
- UART tunnel: 2400 baud, 8-N-1
- request CID: 30024
- response CID: 30025
- AES-128-CBC
- Base64 encoding
- fragment header containing index, total and payload length.

Captured read-command mappings include:
- HSTS -> eo8w
- HGRID -> WdRR
- HOP -> 2l0E
- HBAT -> 2ONL
- HPV -> Mpod
- HTEMP -> V4W3
- HGEN -> COST

The project has a keyless diagnostic mode and a decoded mode when the application AES credential is available. It intentionally does not enable inverter setting writes.

This is unusually strong public evidence because it reportedly derives from an official-app HCI capture rather than guessed protocol names. It needs artifact/code validation in its own later round.

---

## 5. Raw serial / Solar Plug protocol sources

### 5.1 kOld/solarplug-esphome
Repository:
https://github.com/kOld/solarplug-esphome

Classification: **PRIMARY RAW-SERIAL REVERSE-ENGINEERING SOURCE**

Observed scope:
- direct ESPHome/local replacement for Solar Plug-compatible logging;
- developed from serial captures/read-only probes;
- hardware references include PowMr/MrPow POW-HVM6.2KP, HPVINV02, VMII-6200 firmware variants and WIFI-RELAB;
- UART: 2400 baud, 8-N-1;
- command families include HSTS, HGRID, HOP, HBAT, HPV, HTEMP, HGEN, QPRTL and HIMSG1;
- repository/issues document additional inverter compatibility tests.

Public issues currently include:
- PowMr POW-HVM6.2KP firmware 40.09;
- EASUN SMT 4 kW;
- Techfine qd6248mhg with RWB1-06R.

### 5.2 PurpleAlien/SYG-MPPT-120A_grafana
Repository:
https://github.com/PurpleAlien/SYG-MPPT-120A_grafana

Classification: **RELATED RAW-SERIAL / HARDWARE SOURCE**

Uses a Solar Plug RWB1-connected inverter locally and includes Solar Plug documentation. It is relevant for distinguishing logger protocol from inverter-model-specific serial protocol.

---

## 6. Manuals and user documentation

### 6.1 Solar of Things APP Owner User Guide
A substantial English/Chinese guide is publicly mirrored by Manualzz.

English:
https://manualzz.com/doc/81084541/solar-of-things-app-owner-user-guide

Classification: **HIGH-VALUE DOCUMENTATION MIRROR**

Indexed history/content includes:
- app 2.x and 3.x evolution;
- web login;
- Proximal Monitoring;
- device detail and alarms;
- analysis and historical data;
- Wi-Fi configuration;
- Device Diagnosis;
- Classic/Advanced UI modes;
- Peak Valley/time-of-use functions.

Most importantly, Proximal Monitoring documentation describes direct Bluetooth operation with:
- live data;
- control;
- alarms;
- debugging;
- raw command sending;
- configurable send/receive data formats;
- optional security password.

This manual should be used later as a feature-coverage checklist against API and BLE findings.

### 6.2 Solar Plug RWB1 manual family
Public manual mirrors expose multiple revisions, including approximately:
- V1.43;
- V1.50;
- V1.52.

Examples:
https://manuals.plus/m/0e6f43b859542d92976524602749673bd52e1d7d655f15a1186a9bde663d0f66
https://manualzz.com/doc/82546909/solar-plug-rwb1-user-manual

Classification: **HIGH-VALUE HARDWARE DOCUMENTATION**

Common characteristics:
- Wi-Fi 802.11 b/g/n;
- Bluetooth 5.0;
- RS232-family connectivity;
- FreeRTOS;
- Solar of Things platform;
- multiple hardware subtypes/revisions, including RWB1-01/-02/-03/-04 and later variants such as -06.

Variant-specific manuals are important because pinout, interface and protocol behavior may differ.

### 6.3 PowMr pairing/module documentation
Official PowMr documentation links multiple Wi-Fi module families and inverter lines to Solar of Things.

Example:
https://powmr.com/blogs/news/which-wifi-module-should-i-choose-and-how-to-connect-it-to-my-inverter

Classification: **VENDOR HARDWARE-MAPPING SOURCE**

It identifies Solar of Things-compatible WIFI-RELAB families and other module variants, with RS232/RS485 differences and Bluetooth-based provisioning/diagnosis.

### 6.4 Other manufacturer/reseller documentation
Public manuals and support pages from Techfine, EASUN, DatouBoss and other inverter sellers reference Solar of Things, Solar Plug/RWB1 modules, local setup and remote configuration.

Classification: **HARDWARE-COMPATIBILITY CORPUS**

These do not independently prove API behavior but are valuable for building the model/firmware matrix.

---

## 7. Android/iOS application archive sources

### 7.1 APKPure historical Android packages
Package:
com.ssli.sise_solar

Example:
https://apkpure.com/solar-of-things/com.ssli.sise_solar

Classification: **HIGH-VALUE STATIC-ANALYSIS SOURCE**

Publicly indexed APK/XAPK history includes multiple 3.1.x builds and older 2.x builds. Examples observed in the census include 3.1.11 and 2.4.7.

This means future Android archaeology can compare multiple generations rather than only the latest package, potentially exposing:
- removed endpoints;
- old hosts;
- signature/auth changes;
- protobuf/JSON models;
- BLE implementation changes;
- feature flags;
- white-label configuration.

A third-party signature fingerprint is visible in package metadata, but it should be verified directly from downloaded APKs before relying on it.

### 7.2 Apple App Store version history
App ID:
6445806075

Classification: **FIRST-PARTY RELEASE-HISTORY SOURCE**

The App Store currently exposes extensive version history, including 3.1.12 dated 2026-09-16 and earlier 2.x releases.

An App Store review explicitly requests official API access. This is anecdotal but consistent with the census finding no obvious public vendor API documentation.

---

## 8. Community and compatibility sources

Useful communities include:
- Home Assistant Community;
- PowerForum;
- TheBackShed;
- Reddit;
- GitHub issue discussions.

These sources mention real hardware combinations including:
- EASUN / Axpert-family systems;
- DatouBoss and RWB1-06R;
- PowMr systems;
- Tiger Head;
- SolarMax;
- other rebranded inverter families.

Classification: **LOWER-CONFIDENCE BUT HIGH-DIVERSITY FIELD EVIDENCE**

Their main value is not defining protocol truth. Their value is finding:
- model numbers;
- firmware versions;
- dongle variants;
- failures;
- features visible to real users;
- cross-brand compatibility;
- clues requiring controlled verification later.

---

## 9. Public API documentation search result

During this census, no obvious official public Swagger/OpenAPI/API-reference documentation for the Solar of Things cloud API surfaced.

This is **not** a claim that no such documentation exists. It means the broad public searches used in this round did not reveal one.

The strongest available API corpus currently appears to be independently reverse-engineered client code plus the web/mobile applications themselves.

---

## 10. Historical/archive limitation

A direct attempt to query web-archive/CDX material was not usable through the available browsing path in this round. Therefore, historical-web absence must not be inferred.

Known historical leads, including the older direct-IP privacy-policy URL identified in Round 00, remain queued for a later dedicated historical/infrastructure round.

---

## 11. Source-priority map for future work

### Tier A — strongest immediate technical targets
1. Conexo-Casa/solar-of-things-ha
2. X-c0d3/solar-of-things-hack
3. Hyllesen/solar-of-things-solar-usage
4. cchampsan/SiseliSolarApp-HA
5. official/current web application JavaScript
6. current + historical Android packages
7. keiprojects/solar-of-things-ha-ble
8. fadmaz/siseli-ha
9. kOld/solarplug-esphome

### Tier B — lineage and independent corroboration
- 0leg7/ha-siseli-solar
- aliwadah/solar-ac-monitor
- yuraantonov11/siseli-ha
- fedora144/siseli-ha
- gravyflex/siseli-ha
- siselilocal/siseli-ha
- Conexo forks/copies
- PurpleAlien/SYG-MPPT-120A_grafana

### Tier C — coverage/hardware context
- Solar of Things owner guide mirrors;
- Solar Plug RWB1 manual revisions;
- PowMr module documentation;
- EASUN/Techfine/DatouBoss manuals;
- Home Assistant/community forum reports;
- app-store reviews.

---

## 12. Noise and false-positive handling

Broad GitHub searches also return unrelated repositories containing generic phrases such as "Solar of Things" or coincidental numbers/terms. These were not elevated to the research corpus unless there was an explicit SiSeLi/Solar-of-Things/RWB1/API relationship.

Similarly, forks are not counted as independent corroboration until lineage analysis proves meaningful divergence.

---

## 13. What Round 01 changed about the investigation plan

The original planned "Deep GitHub archaeology" round is now too broad.

The census demonstrates that repository research spans at least four technically independent protocol layers. Combining them into one archaeology round would reduce rigor and make provenance harder to track.

Therefore the next repository-analysis work should be split by protocol surface.

### Next round: Round 02 — Cloud REST API repository archaeology

Primary targets:
- Conexo-Casa/solar-of-things-ha;
- X-c0d3/solar-of-things-hack;
- Hyllesen/solar-of-things-solar-usage;
- cchampsan/SiseliSolarApp-HA;
- 0leg7/ha-siseli-solar;
- aliwadah/solar-ac-monitor;
- relevant Conexo forks only where history/divergence adds evidence.

Goals:
- reconstruct lineage and chronology;
- extract every cloud endpoint, method, header and request/response model represented in source/history;
- reconstruct authentication/token/signature evolution;
- identify disagreements between implementations;
- link endpoint discoveries to commits/releases/issues;
- distinguish verified live captures from assumptions or placeholder code;
- build a candidate endpoint matrix for later cross-check against the official web and Android clients.

The MQTT-uplink, BLE and raw-serial repositories should each retain their own later dedicated rounds rather than being folded into Round 02.

---

## 14. Round-close assessment

Round 01 is complete for its purpose: a broad public-source map now exists and the highest-value evidence branches are identified.

The next round should **not** be merged with web-application archaeology or Android static analysis. The independent cloud-client repositories already contain enough material for a dedicated deep archaeology pass, and understanding their claims first will give precise strings/endpoints/headers to search for in official JavaScript and APKs afterward.

No credentials, tokens, cookies, application secrets or other user secrets were collected or stored during this round.

# Round 04 Companion — Android Release, Package and Permission Matrix

Date: 2026-09-24

Status: PUBLIC-METADATA PHASE COMPLETE

Companion to `round_04_official_android_package_archaeology.md`.

## Current release matrix

| Evidence source | Version | Version code | Date shown | Artifact / note |
|---|---:|---:|---|---|
| Google Play current rendered page | not rendered | not rendered | 2026-09-16 | Official store now shows September update date |
| APKPure live latest redirect | 3.1.12 | 86 | current redirect | `Solar of Things_3.1.12_APKPure.xapk`, 188,126,310 bytes |
| Softonic current Android download | 3.1.12 | not shown | 2026-09-19 | `com.ssli.sise_solar_3.1.12.xapk`, 188.13 MB |
| Chrome-Stats indexed Play metadata | 3.1.11 | 85 | 2026-07-30 | crawler is one release behind |
| APKPure indexed package page | 3.1.11 | 85 | 2026-07-30 | detailed signer/split data available |

## Artifact integrity identifiers

| Version | Variant/source | SHA-256 | Signature metadata |
|---|---|---|---|
| 2.4.7 | arm64 XAPK / APKPure | `f26ecd6748f458502c3419761237066e13a2b5a34a1e6522b772a96698a68e92` | `e44e7f2091ce50d8ab30f64de89bb098cb815564` |
| 3.0.6 | indexed XAPK / APKPure | `5e45a6775788497d072f1f37718e99637820355818f14c2a6a97cd347e63149a` | same |
| 3.1.8 | indexed XAPK / APKPure | `65c5e092d1eb3cd22d9c5520bf449f9daab78c05219d2c34e85700659fae4f41` | same |
| 3.1.9 | indexed XAPK / APKPure | `8f2860b612474837698655b0ae2e8a34e1bbaa71c6bb6511c78053b6dadd1073` | same |
| 3.1.10 | indexed XAPK / APKPure | `52055f328ba5b66ddbe814f9dd021677318d114cbc05adab34040d774acb8c50` | same |
| 3.1.11 | arm64 XAPK / APKPure | `6cd561addca2b8d908b27c1368600d4094c16099bdadb202b6d15f2efb87a4cb` | same |
| 3.1.12 | XAPK / Softonic | `09fba80f7ae977d06eb72bbdc9eada3ecea766335d20bdf5b5a1f4c55aa7b7b7` | not independently exposed/verified in this run |

## Current indexed 3.1.11 permissions

| Permission | Functional class | What can safely be inferred |
|---|---|---|
| `INTERNET` | network | app can communicate over network |
| `ACCESS_NETWORK_STATE` | network | app can inspect connectivity state |
| `CHANGE_NETWORK_STATE` | network | app requests ability to change network connectivity |
| `ACCESS_WIFI_STATE` | Wi-Fi | app can inspect Wi-Fi state/info |
| `CHANGE_WIFI_STATE` | Wi-Fi | app can alter Wi-Fi state/configuration where Android permits |
| `BLUETOOTH` | legacy Bluetooth | compatibility with pre-Android-12 Bluetooth model |
| `BLUETOOTH_ADMIN` | legacy Bluetooth | legacy scan/admin capability |
| `BLUETOOTH_SCAN` | BLE | current Android nearby-device scanning capability |
| `BLUETOOTH_CONNECT` | BLE | current Android device connection capability |
| `BLUETOOTH_ADVERTISE` | BLE | requests Bluetooth advertising capability |
| `ACCESS_COARSE_LOCATION` | location | coarse location |
| `ACCESS_FINE_LOCATION` | location | precise location / historical BLE-Wi-Fi scan dependency |
| `CAMERA` | camera | camera-dependent feature possible |
| `READ_EXTERNAL_STORAGE` | storage | legacy external-storage read |
| `WRITE_EXTERNAL_STORAGE` | storage | legacy external-storage write |
| `WRITE_SETTINGS` | system | requests modify-system-settings capability |
| `VIBRATE` | device | vibration/haptic support |
| `FOREGROUND_SERVICE` | runtime | may run foreground Android service |

**Important:** declared permission ≠ observed runtime use, data collection, or data sharing.

## Historical-only permission leads

An older all-version package listing additionally reports:

- `READ_PHONE_STATE`-type phone identity access;
- microphone/record-audio;
- wake/prevent-sleep capability.

These are **not promoted to current manifest facts**.

## Official privacy / SDK evidence

| Item | Official statement | Confidence |
|---|---|---|
| Framework | product is based on Flutter | very high |
| Baidu Map SDK | Android/iOS; device map/location/region | very high |
| Baidu Location SDK | nearby Wi-Fi provisioning, weather, navigation/location | very high |
| `com.baseflow.geolocator` | Flutter geolocation plugin/library, coarse location | very high |
| Wi-Fi provisioning data | SSID, BSSID, Wi-Fi MAC, Wi-Fi password may be handled | very high as developer disclosure; runtime path not independently traced |
| Device identifiers | multiple device/mobile identifiers may be handled | high as developer disclosure; exact current runtime flow unverified |
| Contacts/SMS claim | policy text says these may be collected in Flutter/statistics section | policy claim only; **not supported by current permission metadata found here** |

## Package structure from indexed XAPK variants

Known distribution components include:

- base APK: `com.ssli.sise_solar.apk`;
- ABI splits: arm64-v8a, armeabi-v7a; x86 existed in 2.4.7 metadata;
- language splits across many locales;
- density splits.

For future Flutter analysis, the base APK plus relevant ABI split must be preserved together.

## Binary extraction status

| Task | Status |
|---|---|
| identify current binary | PASS |
| identify current version code | PASS |
| establish artifact hashes | PASS |
| historical signer continuity | PASS through 3.1.11 |
| materialize XAPK in analysis container | BLOCKED |
| direct AndroidManifest parse | PENDING |
| signer verification for 3.1.12 | PENDING |
| Flutter asset inventory | PENDING |
| `libapp.so` extraction | PENDING |
| endpoint/header string extraction | PENDING |
| official Android auth/signing reconstruction | PENDING |
| BLE UUID/class inventory | PENDING, intentionally shallow in this round |

## Round routing

Round 04 is closed as **SUFFICIENT-FOR-PROJECT / BINARY-NOT-REQUIRED**.

The intended implementation is a Windows client of the independent Solar of Things cloud API. Android binary extraction is therefore optional corroboration, not a runtime or protocol prerequisite. Reopen this branch only if a later unresolved question is specifically Android-only.

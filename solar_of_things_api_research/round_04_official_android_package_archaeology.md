# Round 04 — Official Android Application Package Archaeology

Date: 2026-09-24 (America/Santiago)

Status: **IN PROGRESS — public package/manifest architecture phase complete; binary extraction phase blocked by package materialization**

## Purpose

This round investigates the official Android Solar of Things application as an independent official-client surface.

The intended scope is:

1. establish package identity, version lineage, signing continuity and distributable artifacts;
2. determine the Android framework/runtime architecture;
3. inventory Android permissions and declared third-party/native capabilities;
4. separate cloud/API behavior from mobile-only provisioning/local-monitoring behavior;
5. acquire and statically inspect the actual Android package where tooling permits;
6. preserve mobile-specific BLE/provisioning clues without performing the later dedicated BLE protocol investigation here.

No device-changing request, account mutation, inverter control, Bluetooth command, Wi-Fi provisioning action or credentialed API request was performed.

No user credentials, tokens, cookies, passwords or reusable application secrets are stored in this repository.

---

## 1. Official Android identity

### Package

Official Google Play package:

`com.ssli.sise_solar`

Application name:

**Solar of Things**

Publisher shown by Google Play:

**SiSeLi**

Legal developer entity shown by Google Play:

**上海四色礼新能源合伙企业（有限合伙） / Shanghai Siseli New Energy Cooperative Enterprise**

Google Play application URL:

`https://play.google.com/store/apps/details?id=com.ssli.sise_solar`

The official store description identifies the Android application's major functions as:

- equipment monitoring;
- power-station monitoring;
- Bluetooth distribution/provisioning;
- local monitoring and debugging;
- photovoltaic cloud monitoring.

This is important architecturally: the Android client is not merely a mobile wrapper around the cloud dashboard. It intentionally spans cloud, proximal/Bluetooth, Wi-Fi provisioning and local-debugging surfaces.

---

## 2. Current release state — September 2026

There is a short-lived indexing mismatch across public stores/crawlers because version 3.1.12 is a very recent release.

### Current Android distribution

Evidence for **3.1.12**:

- Google Play's current rendered page now reports the app was **updated September 16, 2026**.
- APKPure's live `version=latest` XAPK redirect resolves to a file explicitly named:
  `Solar of Things_3.1.12_APKPure.xapk`
- that redirect embeds package version code **86**;
- redirect metadata reports a full XAPK size of **188,126,310 bytes**;
- Softonic currently identifies its Android artifact as:
  `com.ssli.sise_solar_3.1.12.xapk`
  and reports version **3.1.12**, 188.13 MB, update date September 19, 2026;
- Softonic reports SHA-256:
  `09fba80f7ae977d06eb72bbdc9eada3ecea766335d20bdf5b5a1f4c55aa7b7b7`
- Softonic reports SHA-1:
  `2113e34c9d3cf14d2190582519142e25b13baa25`

Fresh Apple App Store pages independently show Solar of Things **3.1.12** in September 2026, with the usual release note “fix online bugs”. This is useful cross-platform chronology but is not used as proof of Android package contents.

### Why older public indexes still say 3.1.11

Some indexed Android metadata sources, notably Chrome-Stats and APKPure's ordinary app page, still describe **3.1.11** as latest because their crawl/index predates the 3.1.12 propagation.

Therefore:

**Current best release model:** Android 3.1.12 / version code 86 is now distributed, while some metadata indexes remain one release behind.

---

## 3. Last fully indexed Android package: 3.1.11

Version **3.1.11**, version code **85**, remains the newest release for which multiple package scanners expose detailed split/signature metadata.

Observed metadata:

- package: `com.ssli.sise_solar`;
- version: 3.1.11;
- version code: 85;
- XAPK / split-APK distribution;
- Android requirement reported by APKPure: Android 5.1+ / API 22;
- arm64-v8a and armeabi-v7a variants;
- base APK name: `com.ssli.sise_solar.apk`;
- language / ABI / DPI configuration splits;
- arm64 XAPK approximately 169.1 MB in APKPure's indexed artifact;
- SHA-256:
  `6cd561addca2b8d908b27c1368600d4094c16099bdadb202b6d15f2efb87a4cb`
- SHA-1:
  `0cf787b171c56a4bd0cd69a3fd375b6b0fb68c8c`
- APKPure signature fingerprint representation:
  `e44e7f2091ce50d8ab30f64de89bb098cb815564`

The split list demonstrates that the distribution is an Android App Bundle-derived package rather than one monolithic universal APK.

---

## 4. Package lineage and signing continuity

Public package metadata shows a stable package name and the same APKPure signature fingerprint across several generations.

Examples:

| Version | Version code | Date | Minimum Android reported | Signature fingerprint |
|---|---:|---|---|---|
| 2.4.7 | 62 | 2025-06-24 | Android 5.0 / API 21 | `e44e7f2091ce50d8ab30f64de89bb098cb815564` |
| 3.0.6 | 68 | 2025-09-23/24 | Android 5.0 / API 21 | same |
| 3.1.8 | 82 | 2026-04-03 | Android 5.1 / API 22 | same |
| 3.1.9 | 83 | 2026-07-29 | Android 5.1 / API 22 | same |
| 3.1.10 | 84 | 2026-07-29 | Android 5.1 / API 22 | same |
| 3.1.11 | 85 | 2026-07-30 | Android 5.1 / API 22 | same |
| 3.1.12 | 86 | September 2026 | not independently parsed | signing fingerprint not yet independently exposed |

This is strong lineage evidence from 2.4.7 through 3.1.11.

Do **not** infer the 3.1.12 signing fingerprint merely from continuity: the current XAPK bytes were not materialized in this environment, so its certificate/signature remains to be verified directly.

### Selected historical hashes

- 2.4.7 arm64 XAPK SHA-256:
  `f26ecd6748f458502c3419761237066e13a2b5a34a1e6522b772a96698a68e92`
- 3.0.6 indexed XAPK SHA-256:
  `5e45a6775788497d072f1f37718e99637820355818f14c2a6a97cd347e63149a`
- 3.1.8 indexed XAPK SHA-256:
  `65c5e092d1eb3cd22d9c5520bf449f9daab78c05219d2c34e85700659fae4f41`
- 3.1.9 indexed XAPK SHA-256:
  `8f2860b612474837698655b0ae2e8a34e1bbaa71c6bb6511c78053b6dadd1073`
- 3.1.10 indexed XAPK SHA-256:
  `52055f328ba5b66ddbe814f9dd021677318d114cbc05adab34040d774acb8c50`
- 3.1.11 indexed XAPK SHA-256:
  `6cd561addca2b8d908b27c1368600d4094c16099bdadb202b6d15f2efb87a4cb`
- 3.1.12 Softonic XAPK SHA-256:
  `09fba80f7ae977d06eb72bbdc9eada3ecea766335d20bdf5b5a1f4c55aa7b7b7`

Hashes identify exact distribution artifacts; they do not by themselves prove source-code equivalence across mirrors.

---

## 5. Release chronology

The App Store version history gives a useful cross-platform release chronology and APK mirrors corroborate many Android counterparts.

Recent sequence:

- 3.1.12 — September 2026
- 3.1.11 — 2026-07-30
- 3.1.10 — 2026-07-29
- 3.1.9 — 2026-04-24 on Apple; Android mirrors expose build 83 around July indexing
- 3.1.8 — 2026-04-03
- 3.1.7 — 2026-03-05
- 3.1.6 — 2026-02-13 / mirror dates vary
- 3.1.5 — 2026-01-20/22
- 3.1.4 — 2026-01-09
- 3.1.3 — 2025-12-12
- 3.1.2 — 2025-11-17
- 3.1.1 — 2025-10-28
- 3.1.0 — 2025-10-21
- 3.0.6 — 2025-09-24
- 3.0.5 — 2025-08-26
- 3.0.4 — 2025-08-13
- 3.0.3 — 2025-07-16
- 3.0.2 — 2025-06-30
- 3.0.1 — 2025-06-26
- 2.4.7 — 2025-06-23/24
- 2.4.6 — 2025-05-29
- 2.4.5 — 2025-05-23
- 2.4.4 — 2025-05-21
- 2.4.3 — 2025-04-14
- 2.4.2 — 2025-03-27

The public change log is generally non-specific (“fix online bugs” / minor fixes), so package comparison is more informative than release notes for protocol evolution.

---

## 6. Official framework architecture: Flutter

The official Solar of Things privacy policy explicitly states:

**“我们的产品基于Flutter开发” — the product is developed based on Flutter.**

This is a major source-of-truth result for Android archaeology.

It means the Android package should be analyzed as a Flutter release, not as a conventional Java/Kotlin-only application.

### Consequences for binary analysis

When package bytes are available, a rigorous static pass should prioritize:

1. Android manifest and native plugin registration;
2. `lib/<abi>/libapp.so` / Flutter AOT application code, if present;
3. Flutter engine/runtime libraries;
4. `flutter_assets` and asset manifests;
5. embedded JSON/configuration/resources;
6. strings and URL/path constants in native and Dart AOT artifacts;
7. plugin/channel names linking Dart to Android native functionality;
8. native SDKs such as map/location/Bluetooth helpers.

A JADX-only review would likely miss much of the application logic because release Flutter Dart code is normally AOT-compiled.

This section describes the appropriate analysis model. The current package bytes were not available to verify the exact internal file list.

---

## 7. Android permission surface — current indexed 3.1.11

A package-metadata scan for 3.1.11 exposes the following declared permissions:

### Network / cloud

- `android.permission.INTERNET`
- `android.permission.ACCESS_NETWORK_STATE`
- `android.permission.CHANGE_NETWORK_STATE`

These are consistent with cloud REST communication and network-state management.

### Wi-Fi provisioning

- `android.permission.ACCESS_WIFI_STATE`
- `android.permission.CHANGE_WIFI_STATE`

These strongly support the official description/privacy-policy claim that the app performs Wi-Fi device provisioning.

### Bluetooth / BLE

Legacy:

- `android.permission.BLUETOOTH`
- `android.permission.BLUETOOTH_ADMIN`

Android 12+ permissions:

- `android.permission.BLUETOOTH_SCAN`
- `android.permission.BLUETOOTH_CONNECT`
- `android.permission.BLUETOOTH_ADVERTISE`

This is direct package-metadata evidence that Android supports active Bluetooth interaction, not merely displaying Bluetooth-derived cloud data.

### Location

- `android.permission.ACCESS_COARSE_LOCATION`
- `android.permission.ACCESS_FINE_LOCATION`

These support both map/location features and older Android BLE/Wi-Fi scan requirements.

### Camera / storage

- `android.permission.CAMERA`
- `android.permission.READ_EXTERNAL_STORAGE`
- `android.permission.WRITE_EXTERNAL_STORAGE`

Possible use cases include QR/device scanning, profile/media selection, export/import or diagnostic artifacts. Exact call sites require binary/static extraction.

### System/runtime

- `android.permission.WRITE_SETTINGS`
- `android.permission.VIBRATE`
- `android.permission.FOREGROUND_SERVICE`

The declared permission alone does not prove how frequently or in what component it is used.

### Evidence caution

Softonic groups permissions into labels such as “Privacy”, “Data collected” and “Data shared”. Those categories are automated classifications and **must not be interpreted as proof that the application actually transmits all data accessible through a permission**.

The permission names themselves are useful static metadata; the scanner's sharing categorization is not a data-flow audit.

---

## 8. Historical Android permission clues

An older multi-version package-information source reports additional permissions/capabilities for historical Solar of Things builds, including:

- read phone status/identity;
- microphone/audio recording;
- prevent device from sleeping;
- storage read/write;
- location;
- camera;
- Wi-Fi and Bluetooth control.

Because that source aggregates “all versions”, these must be classified as **HISTORICAL-PERMISSION LEADS**, not current 3.1.12 manifest facts.

This historical difference is worth retaining for future package-diff work: permissions may have been removed as Android APIs and the application architecture evolved.

---

## 9. Official privacy-policy architecture evidence

Official privacy policy:

`https://solar.siseli.com/docs/app/privacy_policy/new_index.htm`

The policy provides unusually detailed architectural clues.

### 9.1 Core application purposes

It explicitly names:

- account creation;
- discovering and configuring devices;
- adding and controlling devices;
- geolocation-related services;
- application usage/service feedback.

### 9.2 Mobile/device information

The policy says the app may receive/record mobile information including classes such as:

- IMSI;
- IMEI;
- MEID;
- hardware serial;
- SIM identifier;
- OAID;
- MAC;
- Android ID;
- model/OS/language/region;
- app-store version;
- screen/CPU/display characteristics.

These are developer privacy disclosures, not independently verified runtime data flows.

### 9.3 Provisioning data

The policy explicitly says that while connecting smart devices the application may collect:

- Wi-Fi SSID;
- BSSID;
- Wi-Fi MAC;
- **Wi-Fi password**;
- device MAC;
- device ID;
- device Bluetooth MAC;
- unique device identifier.

This strongly confirms that device provisioning is implemented inside the official mobile client and explains the Wi-Fi/Bluetooth permission combination.

### 9.4 Usage/debug data

The policy also names:

- software/hardware usage records;
- fault information;
- IP;
- access date/time;
- language;
- hardware/software characteristics;
- geolocation.

### 9.5 Flutter statement and broad collection claim

The policy's Flutter section makes broad claims about device identifiers, installed-app list, contacts and SMS in connection with statistics/error analysis.

The current 3.1.11 permission metadata found in this round does **not** expose `READ_CONTACTS` or `READ_SMS`.

Therefore:

**Do not convert the policy's broad “may collect” text into a statement that the current Android package has contacts/SMS access.** The package evidence available here does not establish that.

This is precisely why package-level binary/manifest verification matters.

---

## 10. Third-party/mobile SDK evidence

The official privacy-policy appendix explicitly lists:

### Baidu Map SDK — Android/iOS

Purposes include:

- displaying device location;
- geolocation services;
- selecting device region.

Scenarios include add-device and device-information location screens.

### Baidu Location SDK — Android/iOS

Declared purposes include:

- finding nearby Wi-Fi information for device provisioning;
- weather;
- remote device location/navigation.

The policy states that location used for Wi-Fi provisioning is read for provisioning and is not uploaded to the server in that scenario.

### `com.baseflow.geolocator`

The policy names the Flutter geolocation plugin/library and `ACCESS_COARSE_LOCATION`.

This is strong corroboration of the Flutter/native-plugin architecture.

---

## 11. Android product surfaces inferred from official evidence

The official Android application clearly spans at least four technical surfaces:

### A. Cloud/session API

Used for account, device/station, monitoring, history, alarms, configuration and cloud operation.

Round 02/03 already reconstruct the main REST surface.

### B. BLE/Wi-Fi provisioning

The Android permission set plus privacy policy establish scanning/connection/provisioning capability.

This includes handling network credentials and device identifiers.

Detailed packet/GATT/crypto analysis is deliberately deferred.

### C. Local / proximal monitoring and debugging

The official store description explicitly advertises **local monitoring and debugging**.

This suggests a device-near path separate from ordinary cloud telemetry and is consistent with the community-discovered BLE/raw/passthrough ecosystem.

The exact official implementation remains binary-analysis work.

### D. Map/location/weather

Baidu Map/Location SDK declarations and location permissions support device/station map, region, navigation and weather functions.

---

## 12. Cloud API implications from Android evidence

Round 04 does **not** yet provide a byte-extracted Android endpoint catalog.

However, several implications are now stronger:

1. Android is an independently implemented Flutter client, so matching cloud behavior between Android and the Umi web portal would be especially valuable cross-validation.
2. Android must possess some form of account/session/cloud request stack in Dart/native code.
3. Android includes mobile-only setup flows that necessarily bridge local device identity to cloud ownership/binding.
4. Device-add/bind routes found in the broader API corpus are therefore strategically important targets for binary searching once package bytes are available.
5. Mobile history behavior may differ from the current web console. Public downstream work refers to a Dart “reference app” pattern using `/deviceState/attribute/record/list`, but the provenance is not strong enough in this round to declare that the official Solar of Things Android binary itself contains that method. It remains a search target, not a confirmed official-app fact.

### Priority string dictionary for binary analysis

Once materialized, search the Android/Flutter package for:

- `solar.siseli.com`
- `/apis/`
- `IOT-Token`
- `IOT-Open-AppID`
- `IOT-Open-Nonce`
- `IOT-Open-Body-Hash`
- `IOT-Open-Sign`
- `login/account`
- `login/refresh/access/token`
- `deviceState`
- `remote/device`
- `passthrough`
- `addStationTogether`
- `dtu`
- `dtuid`
- `gatherProtocol`
- `doGetDeviceHistory`
- `attribute/record/list`
- `energy/flow`

The Round 02 112-route catalog can also serve as a complete automated string dictionary.

---

## 13. Public Android package distribution structure

Indexed XAPK variants show:

- a base APK named `com.ssli.sise_solar.apk`;
- architecture splits such as `config.arm64_v8a`, `config.armeabi_v7a`, and historically x86;
- language splits including combinations of:
  `ar`, `de`, `en`, `es`, `fr`, `hi`, `in`, `it`, `ja`, `ko`, `my`, `pt`, `ru`, `th`, `tr`, `vi`, `zh`;
- DPI splits.

This is useful for reproducible extraction because the base APK and the ABI split are both needed when inspecting Flutter native/AOT code.

---

## 14. Store-data/privacy disclosure discrepancy

Google Play currently says, based on the developer's Data Safety declaration:

- no data shared with third parties;
- the app may collect **Personal info** and **Photos and videos**;
- data is encrypted in transit;
- users can request deletion.

The official privacy policy describes a much broader possible data universe, including device identifiers, location, Wi-Fi provisioning data/passwords, device identifiers, usage/fault logs and third-party map/location SDK processing.

This should be recorded as a **disclosure-scope discrepancy**, not automatically as a contradiction or policy violation.

Possible reasons include:

- Google Play taxonomy/definitions differ from the privacy-policy categories;
- some information is processed locally rather than “collected” under Play's definition;
- some privacy-policy language is generic or legacy;
- behavior may depend on region/features/version;
- the declarations may be updated on different schedules.

Only binary/runtime data-flow analysis could establish actual current collection/transmission behavior.

---

## 15. White-label ecosystem relevance

Google Play lists numerous other SiSeLi applications such as LeiLing, Queen Tech, SUN WISE, LUMINOUS NEO, ECOmenic and others.

Related applications use closely similar Solar/Siseli privacy-policy infrastructure.

This supports the Round 00/01 conclusion that Solar of Things sits inside a wider white-label/vendor platform ecosystem.

For Android archaeology this creates a potentially valuable future technique:

**cross-package differential analysis**.

If an older or sibling package is easier to acquire/decompile, common endpoint strings, Flutter assets, native plugins, DTO names or provisioning logic may reveal shared SiSeLi platform code.

No claim of byte-identical code is made without package comparison.

---

## 16. Binary acquisition attempt and tooling boundary

This round attempted to materialize the Android XAPK for direct static analysis.

### What succeeded

The public download infrastructure exposed:

- exact package identity;
- version names/codes;
- current XAPK filename;
- file sizes;
- historical hashes;
- signing fingerprint for indexed releases;
- split/package architecture.

APKPure's current redirect explicitly resolved to a 3.1.12 version-code-86 XAPK object.

### What failed

The actual CDN object could not be downloaded into the analysis container:

- the web fetcher follows the redirect far enough to expose its target metadata, but does not make the binary object available;
- the container's external DNS/network path cannot resolve/download the package;
- the Softonic browser download flow starts a client download but does not expose the raw CDN file to the available extraction tool.

### Consequence

No claim is made that this round:

- ran JADX;
- ran apktool;
- parsed the current binary AndroidManifest directly;
- extracted `libapp.so`;
- recovered Flutter Dart symbols;
- enumerated actual packaged certificates from 3.1.12;
- searched current APK bytes for endpoints/secrets;
- extracted BLE GATT UUIDs from the official binary.

Those tasks remain necessary before Round 04 can be considered fully closed.

---

## 17. Security / integrity observations

### Package authenticity and lineage

3.1.11 and historical versions are reported by APK mirrors as carrying the same signature fingerprint across multiple releases, strengthening confidence that those mirrored packages belong to one signing lineage.

3.1.12's exact signer remains to be directly verified.

### Antivirus scan results

APKPure and Softonic report no detection in their scans for the indexed artifacts.

These results are useful integrity metadata but are **not** equivalent to a source-code security audit.

### Application secrets

No public Open API application secret encountered elsewhere in this research was copied into this report.

When Android bytes are eventually extracted, any recoverable embedded application credential must be treated as public-client material and documented structurally rather than copied into this repository unless absolutely necessary—and user/session secrets must never be stored.

---

## 18. Evidence confidence matrix

| Finding | Confidence | Evidence |
|---|---|---|
| Android package is `com.ssli.sise_solar` | Very high | Google Play + multiple package mirrors |
| Official Android is Flutter-based | Very high | Official Solar of Things privacy policy |
| Current distributed Android release is 3.1.12 / code 86 | High | live APKPure latest redirect + Softonic 3.1.12 artifact + current Google Play Sep-16 update date |
| 3.1.11 is version code 85 | Very high | APKPure + Chrome-Stats |
| Signing fingerprint continuity 2.4.7→3.1.11 | Very high | APKPure historical package metadata |
| Android supports BLE scan/connect/advertise | Very high | current package permission metadata |
| Android supports Wi-Fi provisioning | Very high | package permissions + official privacy policy + store feature description |
| Android uses Baidu map/location SDKs | Very high | official privacy-policy appendix |
| Android uses Baseflow geolocator | Very high | official privacy-policy appendix |
| Android contains current web's exact REST signing implementation | Unresolved | requires binary extraction |
| Android uses `doGetDeviceHistory` exactly | Unresolved | downstream Dart-reference claim, not official binary evidence |
| Current 3.1.12 manifest equals 3.1.11 manifest | Unresolved | binary not extracted |
| Current Android contains embedded Open API credentials | Likely but unverified | architecture requires auth mechanism; exact storage requires binary |
| Current Android BLE packet/GATT protocol | Unresolved here | deliberately deferred + binary unavailable |

---

## 19. Sources examined

Official:

- Google Play Solar of Things:
  `https://play.google.com/store/apps/details?id=com.ssli.sise_solar`
- official Solar of Things privacy policy:
  `https://solar.siseli.com/docs/app/privacy_policy/new_index.htm`
- Apple App Store Solar of Things version history:
  `https://apps.apple.com/.../solar-of-things/id6445806075`

Package/distribution metadata:

- APKPure Solar of Things package/version pages:
  `https://apkpure.net/solar-of-things/com.ssli.sise_solar/`
- APKPure latest XAPK redirect:
  `https://d.apkpure.net/b/XAPK/com.ssli.sise_solar?version=latest`
- Softonic Solar of Things Android package/download pages:
  `https://solar-of-things.en.softonic.com/android`
- Chrome-Stats:
  `https://chrome-stats.com/d/com.ssli.sise_solar`
- APKCombo:
  `https://apkcombo.com/solar-of-things/com.ssli.sise_solar/`

Historical package-permission lead:

- Napkforpc package history:
  `https://napkforpc.com/apk/com.ssli.sise_solar/`

Repository evidence used only as a search-target aid, not as proof of official Android bytes:

- `lujian1324-spec/energy-app`
- prior Round 02 public-source corpus.

---

## 20. Round-close assessment

### What this run accomplished

The public/static metadata phase of Android archaeology is complete enough to establish:

- exact official package identity;
- very recent 3.1.12 distribution and build-code progression;
- historical signing lineage;
- Android App Bundle/XAPK split structure;
- Flutter as the official app framework;
- current Android permission surface;
- mobile-only BLE/Wi-Fi/local-monitoring/location boundaries;
- declared native/third-party SDKs;
- precise binary-analysis targets.

### Scope decision for the Windows project

The user's intended implementation will live on Windows and does not depend on launching, embedding, automating, or otherwise using the Android application.

Rounds 02 and 03 already establish that the Solar of Things cloud API is an independent HTTP backend used by multiple clients. A Windows program can authenticate and communicate directly with `https://solar.siseli.com/apis`; the Android APK is not a runtime dependency.

Therefore Android binary extraction would be **additional reverse-engineering evidence**, not a prerequisite for the Windows API client.

The missing binary work could still reveal:
- mobile-only endpoints;
- provisioning details;
- embedded DTO/service names;
- implementation-specific auth constants;
- BLE/local protocol clues.

Those are useful but no longer justify blocking the main investigation.

### Round-close decision

**Round 04 is closed as SUFFICIENT-FOR-PROJECT / BINARY-NOT-REQUIRED.**

The public/static phase established the Android application's role and confirmed that it is one client of the wider SiSeLi platform rather than the API itself.

Binary extraction remains an optional future branch if later evidence creates a concrete Android-only question. It should not consume another round merely for completeness.

### Next-round decision

Proceed to **Round 05 — Alternate Web Environment Comparison**.

Targets:
- `test.solar.siseli.com`;
- `doc.solar.siseli.com`;
- `demo.doc.solar.siseli.com`;
- any additional first-party environment/tenant hosts discovered from those surfaces.

Goals:
- determine whether these are staging, documentation, demo, tenant/white-label, or aliases;
- compare public application shells, routes, bundle/config clues and backend/base URLs against production;
- identify API versions/features present outside production;
- look for documentation, schemas or dormant functionality that can strengthen the Windows API implementation;
- avoid authenticated/mutating activity.

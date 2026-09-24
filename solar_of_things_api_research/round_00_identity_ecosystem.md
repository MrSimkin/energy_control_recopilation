# Round 00 — Identity and Ecosystem Mapping

Date: 2026-09-24

Status: COMPLETE

Purpose: establish the exact identity, official surfaces, publisher/developer, product family, and immediately visible ecosystem around Solar of Things before deeper API-specific investigation.

## Confidence labels

- **OFFICIAL-CONFIRMED**: supported directly by SiSeLi/official Solar of Things web properties or first-party app-store listings.
- **CROSS-CONFIRMED**: independently supported by multiple strong sources, including at least one official source.
- **THIRD-PARTY-OBSERVED**: useful external observation not yet independently proven from first-party code or live traffic.
- **INFERENCE**: conclusion drawn from converging evidence; not an explicit vendor statement.
- **UNRESOLVED**: discovered but purpose/relationship is not yet fully established.

---

## 1. Core product identity

### Product name
**Solar of Things**

Confidence: OFFICIAL-CONFIRMED.

Evidence:
- Web portal: https://solar.siseli.com/
- Google Play: https://play.google.com/store/apps/details?id=com.ssli.sise_solar
- Apple App Store: https://apps.apple.com/cl/app/solar-of-things/id6445806075

### Developer / publisher
Chinese legal name shown by Google Play:
**上海四色礼新能源合伙企业（有限合伙）**

English App Store seller/developer name:
**Shanghai Siseli New Energy Cooperative Enterprise**

Confidence: CROSS-CONFIRMED.

### Public developer/publisher label
**SiSeLi**

Confidence: OFFICIAL-CONFIRMED via Google Play developer catalog.

### Android application ID
**com.ssli.sise_solar**

Confidence: OFFICIAL-CONFIRMED from the Google Play canonical URL.

### iOS App Store ID
**6445806075**

Confidence: OFFICIAL-CONFIRMED from the Apple App Store canonical URL.

### iOS bundle identifier
A third-party App Store metadata source reports:
**com.ssli.sise-solar**

Confidence: THIRD-PARTY-OBSERVED. This must be verified later from an IPA/App Store metadata endpoint before being treated as canonical.

Source:
- https://foxdata.com/en/app-marketing-analytics/6445806075/as/GE/solar-of-things/

---

## 2. Current application status

At the time of this round, the Apple App Store exposes Solar of Things version **3.1.12**, dated **2026-09-16**. A Chinese Tencent app listing also reports version 3.1.12 / a September 2026 update.

Confidence: CROSS-CONFIRMED.

Sources:
- https://apps.apple.com/cl/app/solar-of-things/id6445806075
- https://sj.qq.com/appdetail/com.ssli.sise_solar

The Google Play listing identifies the same Android app and reports 100K+ downloads. Store crawl/update metadata varies by locale/crawl date, so exact store statistics should not be treated as stable API facts.

---

## 3. Officially advertised capabilities

The official store description states that Solar of Things includes:

- equipment monitoring;
- power-station monitoring;
- Bluetooth network/provisioning;
- local monitoring;
- local debugging;
- photovoltaic cloud monitoring;
- viewing equipment generation and operating status.

Confidence: OFFICIAL-CONFIRMED.

Sources:
- https://play.google.com/store/apps/details?id=com.ssli.sise_solar
- https://apps.apple.com/cl/app/solar-of-things/id6445806075

This establishes, before any reverse engineering, that the ecosystem has at least two distinct communications/control surfaces:

1. a remote/cloud path;
2. a local/Bluetooth path.

That distinction must be preserved throughout later rounds.

---

## 4. Primary official web surfaces discovered

### 4.1 Main Solar of Things web application
**https://solar.siseli.com/**

Observed public login methods include:
- Account
- Cellphone Number
- Email
- QR Code
- Guest

The page also exposes:
- Sign In
- Register
- Forgot Password
- Scan It
- Download APP

Confidence: OFFICIAL-CONFIRMED.

The portal footer exposes Chinese ICP registration:
**沪ICP备2023003269号-1**

### 4.2 Corporate site
**https://www.siseli.com/**

Confidence: OFFICIAL-CONFIRMED as the website linked from SiSeLi app-store listings.

The public page is highly image-driven and yielded little machine-readable text during this round; deeper web-asset analysis is deferred.

### 4.3 App download surface
**https://download.app.solar.siseli.com/**

Observed text includes:
- Solar of Things
- Test Version
- iOS Apple Store download
- Android Google Play download

The page also exposes **沪ICP备2023003269号-1**.

Confidence: OFFICIAL-CONFIRMED.

Important future target: determine whether "Test Version" exposes alternate packages/builds or staging infrastructure.

### 4.4 Documentation/alternate-login surface
**https://doc.solar.siseli.com/**

Search indexing shows a page titled **HuTong-1-English** with the same account/cellphone/email/QR/guest style login interface.

Confidence: OFFICIAL-DOMAIN / PURPOSE UNRESOLVED.

This is important because it may be:
- a white-label customer portal;
- a documentation/demo instance;
- a parallel deployment of the same frontend;
- or a separate product environment.

No assumption is made yet.

### 4.5 Demo documentation/alternate surface
**https://demo.doc.solar.siseli.com/**

Observed as a JavaScript application shell.

Confidence: OFFICIAL-DOMAIN / PURPOSE UNRESOLVED.

### 4.6 Test portal
**https://test.solar.siseli.com/**

Observed as a JavaScript application shell.

Confidence: OFFICIAL-DOMAIN / PURPOSE UNRESOLVED.

This is a high-value future target for environment comparison, but no probing beyond ordinary public retrieval was performed in this round.

---

## 5. Official privacy-policy evidence

Canonical Solar of Things privacy policy discovered at:

https://solar.siseli.com/docs/app/privacy_policy/new_index.htm

The policy explicitly names Solar of Things and Shanghai Siseli as the developer/operator.

It says processing purposes include:
- creating a Solar of Things account;
- discovering/configuring devices;
- adding and controlling devices;
- location-related services;
- feedback/service improvement.

It describes possible collection of:
- account information;
- phone/email;
- device identifiers and hardware/software attributes;
- Wi-Fi SSID/BSSID/MAC and Wi-Fi password during device connection/configuration;
- device MAC address;
- device ID;
- Bluetooth MAC address;
- device unique identifiers;
- IP address;
- timestamps;
- language;
- hardware/software characteristics;
- geolocation;
- usage/click/navigation statistics;
- device inventory and function-use parameters.

It also describes use of:
- Baidu Maps SDK;
- Baidu Location SDK;
- Flutter/geolocation-related components.

The policy gives the contact:
**melon@siseli.com**

Confidence: OFFICIAL-CONFIRMED.

This information is relevant to later Android/static/local-protocol research because it independently confirms that the app handles Wi-Fi provisioning, device IDs, Bluetooth identifiers, network information, and geolocation.

---

## 6. Privacy metadata contradiction

There is a material discrepancy that must remain explicitly unresolved.

### Apple App Store declaration
The current Apple listing says:
**"No data collected"** (developer-declared, Apple notes that the information is not independently verified).

### SiSeLi's own privacy policy
The Solar of Things privacy policy describes collection/processing of multiple categories of personal, device, network, Wi-Fi, location, and usage information.

### Google Play declaration
The current Google Play presentation states that the app may collect categories including personal information and photos/videos, and says data is encrypted in transit and deletion can be requested.

Confidence: OFFICIAL-CONFIRMED CONTRADICTION.

This round does not attempt to determine which representation most accurately describes current runtime behavior. That requires later static/dynamic application analysis.

---

## 7. Publisher ecosystem: sibling / likely white-label applications

The SiSeLi Google Play developer catalog currently exposes eleven Android applications. The Apple developer catalog also exposes eleven iOS applications, with a largely overlapping set.

### Android catalog observed

| App | Android package | Evidence |
|---|---|---|
| Solar of Things | com.ssli.sise_solar | official Google Play |
| Sun house | com.ssli.next.solar | official Google Play |
| SUN WISE | com.ssli.sun_wise | official Google Play |
| LUMINOUS NEO | com.ssli.srd | official Google Play |
| LeiLing | com.ssli.leiling | official Google Play |
| Queen Tech | com.ssli.sunline2.queenTech | official Google Play |
| SunSaviour | com.ssli.invert.android | official Google Play |
| ECOmenic | com.ssli.shenglai | official Google Play |
| LIB Life | com.ssli.shenghong.android | official Google Play |
| 沐能低碳 | com.ssli.mndt.android | official Google Play |
| HC solar | com.ssli.hcsolar | official Google Play |

Primary catalog:
https://play.google.com/store/apps/developer?id=SiSeLi

### iOS catalog observed

Apple lists:
- LeiLing
- Queen Tech
- LUMINOUS NEO
- SUN WISE
- ECOmenic
- Moonlc
- HC Solar
- LIB Life
- SunSaviour
- Sun House
- Solar of Things

Primary catalog:
https://apps.apple.com/us/developer/shanghai-siseli-new-energy-cooperative-enterprise/id1673700559

### Why this matters

Several sibling applications use effectively the same official description as Solar of Things: equipment monitoring, power-station monitoring, Bluetooth configuration, local monitoring/debugging, and photovoltaic cloud monitoring.

Examples:
- SUN WISE: https://play.google.com/store/apps/details?id=com.ssli.sun_wise
- LUMINOUS NEO: https://play.google.com/store/apps/details?id=com.ssli.srd
- LeiLing: https://play.google.com/store/apps/details?id=com.ssli.leiling
- Queen Tech: https://play.google.com/store/apps/details?id=com.ssli.sunline2.queenTech
- SunSaviour: https://play.google.com/store/apps/details?id=com.ssli.invert.android

SUN WISE additionally carries a customer/company attribution to **Guangdong Bifu New Energy Co., Ltd.**, while LUMINOUS NEO links to a non-SiSeLi customer-facing website (luminous-global.com) and support contact.

### Interpretation
**INFERENCE, high confidence:** SiSeLi appears to operate a reusable/white-label solar-monitoring platform distributed under multiple partner or customer brands.

This inference is not yet an official architectural statement, so later rounds should verify whether these applications:
- use the same backend host;
- use the same API paths;
- share authentication/signing;
- differ only by branding/configuration;
- or target separate tenants/environments.

This sibling-app family is potentially one of the richest sources for API archaeology because older or less-obfuscated variants may retain API details removed from Solar of Things.

---

## 8. Account/platform interoperability clue

A public Google Play review for **Sun house** states that the reviewer can log into Solar of Things using the same account and see their records there.

Source:
https://play.google.com/store/apps/details?id=com.ssli.next.solar

Confidence: THIRD-PARTY-OBSERVED.

This is not sufficient to establish universal cross-app account interoperability, but it is consistent with the shared-platform/white-label inference and should be tested only later with the user's own account if needed.

---

## 9. Registration and support identifiers

Observed repeatedly in official store listings:

- Support email: **siseli@siseli.com**
- Developer-account email shown by Google Play: **heng_zhang@hi-flying.com**
- Support phone commonly shown: **+86 158 3619 3137** / formatting varies
- Developer address shown by Google Play: Chongming District / Changxing Town / Panyuan Road 1800, Shanghai, China 200000

The portal/app distribution surfaces expose ICP registration:
- website: **沪ICP备2023003269号-1**
- Tencent Android listing reports app filing: **沪ICP备2023003269号-2A**

Confidence: OFFICIAL-CONFIRMED for store/portal-display facts. No claim is made here about corporate ownership relationships beyond the displayed publisher information.

---

## 10. Historical / infrastructure clues worth preserving

A third-party metadata source records an older Solar of Things privacy-policy URL at:
**http://116.62.125.198/privacy/policy/index.html**

Confidence: THIRD-PARTY-OBSERVED.

This may indicate an older direct-IP web origin or historical hosting layout. It should be investigated later through passive historical sources rather than assumed current.

A domain-information service reports siseli.com using AliDNS/HiChina-related DNS infrastructure. This is low-priority contextual evidence only and should not be treated as an API finding.

---

## 11. What this round establishes for later API work

Confirmed strategic facts:

1. Solar of Things has a first-party web portal and first-party Android/iOS apps.
2. The Android package is com.ssli.sise_solar.
3. The live canonical cloud portal is https://solar.siseli.com/.
4. The system explicitly supports remote cloud monitoring plus local/Bluetooth provisioning/monitoring/debugging.
5. SiSeLi operates many closely related branded apps.
6. Those sibling apps are valid future reverse-engineering targets.
7. Multiple official Solar-of-Things subdomains/environments exist (solar, download.app.solar, doc.solar, demo.doc.solar, test.solar).
8. The privacy policy confirms handling of device IDs, Wi-Fi information, Bluetooth identifiers, location, and device-control functionality.
9. Privacy disclosures are internally inconsistent across Apple/Google/vendor policy and need later technical verification.
10. No API endpoint, auth algorithm, token format, or control command is declared "confirmed" by this round; those belong to later rounds.

---

## 12. Explicit unresolved questions

- What exact backend hosts are used by the Android, iOS, web, and sibling applications?
- Are test.solar.siseli.com, doc.solar.siseli.com, and demo.doc.solar.siseli.com separate backend environments, frontend aliases, tenant deployments, or staging/demo systems?
- Does the "Test Version" download page expose test APK/IPA builds?
- Do sibling apps share the same API, credentials, signing secret, and station/device identifiers?
- Is the older direct-IP privacy URL evidence of a historical API/web host or only static policy hosting?
- What are the exact current Android and iOS build metadata, signing certificates, minimum OS levels, embedded SDKs, and network-security configuration?
- Which privacy disclosure reflects actual current runtime behavior?
- Is cross-login across SiSeLi-branded apps generally supported or only true for particular tenants/devices?

---

## 13. Sources used in this round

First-party / official:
- https://solar.siseli.com/
- https://www.siseli.com/
- https://download.app.solar.siseli.com/
- https://doc.solar.siseli.com/
- https://demo.doc.solar.siseli.com/
- https://test.solar.siseli.com/
- https://solar.siseli.com/docs/app/privacy_policy/new_index.htm
- https://play.google.com/store/apps/details?id=com.ssli.sise_solar
- https://play.google.com/store/apps/developer?id=SiSeLi
- https://apps.apple.com/cl/app/solar-of-things/id6445806075
- https://apps.apple.com/us/developer/shanghai-siseli-new-energy-cooperative-enterprise/id1673700559
- https://play.google.com/store/apps/details?id=com.ssli.next.solar
- https://play.google.com/store/apps/details?id=com.ssli.sun_wise
- https://play.google.com/store/apps/details?id=com.ssli.srd
- https://play.google.com/store/apps/details?id=com.ssli.leiling
- https://play.google.com/store/apps/details?id=com.ssli.sunline2.queenTech
- https://play.google.com/store/apps/details?id=com.ssli.invert.android
- https://play.google.com/store/apps/details?id=com.ssli.shenglai
- https://play.google.com/store/apps/details?id=com.ssli.shenghong.android
- https://play.google.com/store/apps/details?id=com.ssli.mndt.android
- https://play.google.com/store/apps/details?id=com.ssli.hcsolar

Secondary / contextual:
- https://sj.qq.com/appdetail/com.ssli.sise_solar
- https://foxdata.com/en/app-marketing-analytics/6445806075/as/GE/solar-of-things/
- https://www.ipaddress.com/website/siseli.com/

---

## 14. Round-close assessment

This round is complete enough to serve as the identity/ecosystem baseline.

No merge with the next planned round is warranted.

The next round should remain a separate **public-source census**: systematically enumerate repositories, code fragments, forum posts, documentation, archived material, package mirrors, API traces, and other externally visible sources mentioning Solar of Things / SiSeLi. The purpose of that round should be source discovery and classification, not yet deep code archaeology. Keeping it separate prevents a few prominent repositories from prematurely narrowing the search space before the corpus is mapped.

# Round 05 — Alternate Web Environment and OpenAPI Documentation Comparison

Date: 2026-09-24 (America/Santiago)

Status: COMPLETE

Companion: `round_05_swagger_production_delta.md`

## Purpose

This round compares first-party non-production/documentation surfaces with the production Solar of Things environment, with a specific goal relevant to the Windows project:

- determine which hosts are actual alternate API environments versus documentation/distribution surfaces;
- recover first-party API contract information that is not visible from ordinary production traffic;
- compare documented API contracts against the production routes observed in Round 02/03;
- distinguish live/current capabilities from documented-but-disabled, role-gated, stale, or environment-specific functionality;
- avoid authenticated, mutating, or device-changing probing.

The cloud API remains independent of the Android app. All findings in this round concern HTTP/web platform surfaces usable in principle from Windows.

---

## 1. Environment map

### Production

`https://solar.siseli.com/`

Direct current retrieval returns the same JavaScript-only SPA shell observed in Round 03.

Production REST base established by previous rounds:

`https://solar.siseli.com/apis`

### Test

`https://test.solar.siseli.com/`

Direct current retrieval succeeds and returns the same minimal JavaScript SPA shell text as production:

`Sorry, we need js to run correctly!`

This confirms that `test.solar.siseli.com` is a live web application surface, not merely a historical DNS name.

Historical official-bundle reverse engineering and live server testing establish that test and production were configured with **different IoT Open application identities**.

A March 2026 Conexo investigation found:

- the test application identity was accepted by `test.solar.siseli.com`;
- real production-user credentials sent against that test combination returned API code **20007, “account error”**;
- switching to the production application identity and `solar.siseli.com` allowed authentication to progress normally;
- plaintext password then produced code **7**, and MD5(password) resolved that issue.

### Interpretation

**High-confidence conclusion:** test and production are logically distinct environments at least at the IoT Open application/authentication layer. They must not be mixed in a Windows client.

The Windows client should treat:

- host;
- application identity;
- user/account namespace;
- tokens;

as one environment-specific tuple, not interchangeable components.

---

## 2. Documentation/OpenAPI environment

First-party documentation host:

`http://doc.solar.siseli.com/openapi/#/`

Preserved Swagger endpoint:

`http://doc.solar.siseli.com/openapi/swagger2/api-docs?group=openApis`

Direct retrieval of the documentation host currently times out / returns a gateway error through the available browser path, so this round does **not** claim to have downloaded the live Swagger JSON on 2026-09-24.

However, a substantial Swagger-derived transcription was preserved in the public `lujian1324-spec/energy-app` repository and explicitly records those first-party documentation URLs.

The repository history is important:

- on 2026-05-12 it corrected its API layer and route names against the IoT-Open Swagger specification;
- its API reference records the production REST base as `https://solar.siseli.com/apis`;
- subsequent July updates added live validation notes for selected documented endpoints.

This gives us a usable documentation snapshot with provenance, while keeping a clear distinction between “Swagger-documented” and “production-observed”.

---

## 3. The OpenAPI documentation confirms the Round 03 signing model

The Swagger-derived reference records the generic IoT Open signing contract as:

1. SHA-256 the body for non-GET requests;
2. GET uses an empty body-hash string;
3. combine URL parameters with:
   - `IOT-Open-AppID`;
   - `IOT-Open-Nonce`;
   - `IOT-Open-Body-Hash`;
4. sort parameters lexicographically by name;
5. serialize as `key=value&key2=value2...` using original UTF-8 values;
6. Base64-encode the UTF-8 canonical string;
7. HMAC-SHA256 the Base64 text with the application secret;
8. MD5 the raw HMAC bytes to lowercase hexadecimal.

This independently strengthens Round 03.

### Round 05 conclusion

The current best Windows-client signing model is now supported by three evidence families:

- historical official Umi-bundle reverse engineering;
- live-production client interoperability;
- first-party Swagger-derived documentation.

The later hex-preimage implementation remains demoted.

---

## 4. The Swagger refresh schema corroborates the newer live behavior

Swagger-derived `RefreshTokenDtio` requires:

```json
{
  "accessToken": "string",
  "refreshToken": "string"
}
```

This is significant because Round 03 found a contemporary live client reporting that a refresh-token-only request was rejected as an illegal argument, while sending the current access+refresh pair worked.

Older Conexo code sent only `refreshToken`.

### Round 05 conclusion

For a new Windows client:

**Preferred refresh contract: current access token + current refresh token.**

Refresh-token-only behavior should be considered historical/compatibility behavior unless specifically verified.

Combined with Round 03's live experiment showing refresh-token rotation, the Windows client should:

- serialize refresh attempts;
- submit the current pair;
- atomically replace both returned tokens;
- never allow two workers to refresh the same session concurrently.

---

## 5. Role information exposed by the documented login response

The documented login response contains explicit role flags including:

- `isAdmin`;
- `isDealer`;
- `isDeviceManufacturer`;
- `isIntegrator`;
- `isOfficialStaff`;
- `isStationOwner`;
- plus `userType`, `authId`, `userId`, `ticket`, and theme/account metadata.

This provides a strong explanation for why the documented API surface is much broader than what an ordinary owner session exercises.

### Implication

Many Swagger routes should be modeled as:

**documented platform capability, potentially role-gated**

rather than as guaranteed owner-account functionality.

A Windows client should build capabilities from:

- login role metadata;
- endpoint response/error behavior;
- device metadata/capabilities;

rather than assume every documented route is callable by every account.

---

## 6. Endpoint-count correction: 221 tabled routes, not blindly 227

The preserved API reference footer says:

**41 service groups, 227 endpoints.**

This round parsed the current Markdown contract rather than trusting the footer.

Result:

- **221 distinct method/path pairs** are actually present in endpoint tables;
- 40 sections contain endpoint tables;
- section 41 is a `StationEnergyFlowDtoo` data-model section rather than an endpoint group;
- a broad method/path regex also produces the same 221 unique pairs.

Therefore:

**221 is the independently reproducible endpoint-table count in the preserved document.**

The stated 227 may reflect:

- six endpoints omitted during Markdown transcription;
- an earlier/later Swagger revision;
- a stale footer count;
- or counting rules different from “unique method + path”.

No missing six are invented.

---

## 7. Swagger contract versus production corpus

Round 02's endpoint catalog currently contains **115 distinct method/path entries** when the 108 original HAR routes, permission-discovered additions, and later supported routes are combined.

Exact method/path comparison against the 221 Swagger-tabled routes gives:

- Swagger documented: **221**
- Round 02 production corpus: **115**
- exact overlap: **62**
- Swagger-only: **159**
- production-corpus-only: **53**

This is a major architectural result.

### Neither corpus is a superset

The Swagger documentation is much broader in administrative, provisioning, local/near, account-management, firmware and control capabilities.

The production HAR/current captures expose many routes that are absent from the preserved Swagger transcription, especially:

- portal/router metadata;
- reports/exports;
- station income;
- dashboard variants;
- user currency/theme/logging;
- DTU search/model variants;
- newer device-overview routes.

Therefore the Windows API specification should never be generated solely from either source.

The correct model is a **union with provenance**.

---

## 8. Major Swagger-only capability families

### 8.1 Alternate login and account-management flows

Swagger documents routes absent from the production HAR snapshot including:

- `/login/email`;
- `/login/sms`;
- `/login/inviteCode`;
- `/login/passwordless/login`;
- email/SMS registration;
- password reset;
- account existence checks;
- profile/credential update flows;
- subordinate-user login/account operations.

This aligns with UI features not necessarily exercised in the captured owner session.

### 8.2 Device lifecycle

Swagger documents:

- add device;
- add device + station;
- delete/unbind;
- update metadata;
- pin/unpin;
- replace DTU;
- external-device management;
- query by DTU;
- gather-protocol discovery.

These are important for a full management client, but many are mutating and should remain outside read-only validation until explicitly needed.

### 8.3 Non-simple device-state APIs

Swagger contains both:

- `deviceState/simple/*`; and
- fuller `deviceState/attribute/*` APIs.

Notable documented full routes include:

- `/deviceState/attribute/record/list`;
- `/deviceState/attribute/record/list/v2`;
- `/deviceState/attribute/record/time/list`;
- `/deviceState/attribute/keys/history`;
- `/deviceState/gatherAttributes`.

This is consistent with later evidence that different clients/pages choose different history/state representations.

### 8.4 Rich remote-device control

Swagger documents:

- latest remote state;
- config read/write;
- batch config read;
- config cache;
- config write records;
- remote energy flow;
- raw passthrough;
- fast-report start/stop/support.

The raw passthrough request contract is explicitly modeled as:

```json
{
  "base64Input": "string",
  "noOutput": false
}
```

This is especially relevant for future Windows work because it exposes a cloud-mediated transport for lower-level device commands without requiring Android.

### 8.5 Peak-valley / smart scheduling

Eight Swagger routes describe:

- supported scheduling types;
- general peak-valley configuration;
- customized configuration;
- enable/disable;
- device attribute groups.

These were not exercised in the original production HAR subset.

### 8.6 Firmware and upgrade management

Swagger documents:

- firmware lists;
- manufacturer firmware lists;
- creating device upgrades;
- upgrade lists/logs;
- upgrade-script file information;
- gather-protocol upgrade bindings.

These are high-risk/mutating administration surfaces and are cataloged only.

### 8.7 Local/near DTU service

One of the richest Swagger-only groups contains **17 `/near/dtu/*` endpoints**.

The documentation describes them explicitly as being used for **Bluetooth/local proximal collection scenarios**.

They include server-side generation and parsing helpers for:

- gather-protocol detection;
- attribute groups;
- config-read command generation;
- config-write command generation;
- batch config generation;
- parsing device state;
- parsing events;
- parsing energy flow;
- parsing config responses;
- HJS/HIS script retrieval;
- HFMI/smart-screen assets.

This is strategically important for the Windows project.

It means the platform contains a documented **protocol-generation/parsing service** that can potentially translate high-level device operations into low-level frames and parse returned local-device frames.

That can be relevant even without Android, especially if Windows later communicates directly with the logger/inverter.

No endpoint in this group was executed in this round.

### 8.8 QR/app-version/captcha/system services

Swagger also includes:

- app QR-login confirmation;
- app version checking;
- graphical captcha generation/verification;
- currency and telephone-code lists;
- geolocation/reverse-geocoding helpers;
- resource upload;
- dictionaries;
- sensitive-country services.

---

## 9. Documented functionality can be live yet disabled by vendor/device policy

A documented route should not be equated with usable capability.

The strongest example is the **Auto Instruction Service**:

- `GET /instruction`
- `GET /instruction/list`
- `POST /instruction/add`
- `POST /instruction/update`
- `POST /instruction/updateStatus`
- `POST /instruction/delete`
- history routes.

A July 2026 live investigation reconstructed a valid timed-instruction schema and got far enough for the backend to return a domain-specific error:

**70247 — “This instruction's manufacturer have been close instructionOpen!”**

Other malformed variants produced domain errors such as:

- 70224 for invalid/missing time range;
- 70260 for invalid trigger mode.

### Interpretation

The service exists and parses the contract, but a manufacturer-level feature flag can disable timed automation for a particular product family.

This is a useful general rule for the Windows client:

**documented + routable ≠ enabled for this device/manufacturer/account.**

Capability discovery and graceful error handling are mandatory.

---

## 10. Some Swagger-only endpoints have been live-validated with ordinary consumer accounts

The preserved API reference records July 2026 live validation of device-overview routes including:

- `/deviceOverView/generationPower/daily`;
- `/deviceOverView/generatedEnergy/monthly`.

The test notes state they worked with an ordinary consumer account, not an installer/manufacturer role.

Responses included an `isRealValue` boolean distinguishing real device reports from backend-filled placeholder values.

This metadata is valuable for analytics because it can prevent synthetic/backend-filled points from being mistaken for actual measurements.

These routes were not part of the original HAR overlap but therefore should be promoted above “documentation only” where the preserved live evidence applies.

---

## 11. Production-only routes prove the Swagger snapshot is incomplete/stale in places

The Round 02 production corpus includes **53 method/path entries absent from the preserved Swagger tables**.

Examples include:

### Platform/router metadata

- `GET /getInfo`
- `GET /getRouters`
- `GET /portal/info`
- `POST /rest/api/list`

These look particularly useful for understanding dynamic routing/permissions/platform capabilities.

### Reporting/export surfaces

Multiple owner/station/device report endpoints appear in production traffic but are not in the preserved Swagger transcription.

### Dashboard/station-owner additions

Production contains additional dashboard summaries, station state counts and station income endpoints.

### User/session/UI metadata

Production traffic includes:

- `GET /user/currency`;
- `POST /user/currency/setting`;
- personal log search;
- direct superior lookup;
- theme-color lookup.

### Newer device overview

Round 03's fresh production capture added:

`POST /deviceOverView/pvInverterPowerClass/daily/detail`

which is absent from the preserved Swagger reference.

### Conclusion

The documentation snapshot is valuable but not current-complete.

---

## 12. Test environment: what is actually established

Directly:

- `test.solar.siseli.com` is alive;
- it serves a JavaScript application shell similar to production.

Historically/live-tested:

- a test IoT Open application identity existed;
- that identity was accepted on the test host;
- production real-user credentials did not authenticate there and returned 20007;
- production uses a different application identity/host pairing.

Not established:

- current test bundle filename;
- current test backend route inventory;
- whether test mirrors production data/schema today;
- whether test accounts can be self-created;
- whether test currently runs the same backend release.

No authenticated test probing was performed.

---

## 13. Documentation and demo hosts

### `doc.solar.siseli.com`

Strongly established purpose:

**OpenAPI/Swagger documentation host.**

Known paths preserved in public source:

- `/openapi/#/`
- `/openapi/swagger2/api-docs?group=openApis`

Current direct fetch through the available environment times out / returns gateway failure, so availability to a normal browser should be rechecked later if raw Swagger retrieval becomes necessary.

### `demo.doc.solar.siseli.com`

The host was discovered in Round 00 and has appeared as a JavaScript application shell through earlier indexing, but:

- current direct retrieval times out in this environment;
- no high-quality indexed or repository evidence was found that establishes its function;
- it is not safe to label it a Swagger demo, API sandbox, customer tenant, or staging environment yet.

Status:

**EXISTS AS A LEAD / PURPOSE UNRESOLVED.**

It does not justify another dedicated round unless later evidence points to a unique API artifact there.

---

## 14. Other first-party “test version” distribution surfaces

Search indexing exposes:

- `https://download.app.solar.siseli.com/`
- `https://sqr.app.siseli.com/`

Both have surfaced with the label **“Test Version.”**

These appear to be application/download or QR/distribution surfaces, not evidence of additional REST API environments.

They remain useful for release/package archaeology but are not promoted into the API host map.

---

## 15. The `/openapis` naming trap

An early third-party API reference incorrectly used:

`https://solar.siseli.com/openapis`

as the REST base.

On 2026-05-12 that project corrected its implementation against the Swagger specification to:

`https://solar.siseli.com/apis`

The string `openApis` also appears as the Swagger documentation group name.

Separately, an early third-party WebSocket prototype used:

`wss://solar.siseli.com/openapis/ws`

### Important distinction

- REST production base: **`/apis`**
- Swagger group label: **`openApis`**
- alleged WebSocket path: **`/openapis/ws`**, currently unverified

Do not derive REST URLs mechanically from the phrase “Open API”.

---

## 16. WebSocket claim is demoted to UNVERIFIED

One downstream project once implemented:

`wss://solar.siseli.com/openapis/ws?token=...`

with guessed message types and heartbeat behavior.

Its own later engineering cleanup removed that hook as dead code.

No first-party Swagger evidence, production HAR evidence, or current official-client capture confirming that WebSocket protocol was found in this round.

Therefore:

**Do not implement the WebSocket path in the Windows client based on current evidence.**

If realtime push becomes important, it should receive a dedicated verification task.

Polling/fast-report REST endpoints have materially stronger evidence.

---

## 17. Quantitative domain comparison

The companion file contains the full route-by-route delta.

Notable domain-level patterns:

- `near/*`: 17 Swagger routes, zero in captured production traffic;
- `peakValley/*`: 8 Swagger routes, zero in the production corpus;
- `instruction/*`: 8 documented routes, zero HAR routes, but later live tests prove the service exists and can be manufacturer-disabled;
- `login/*`: 7 documented, only 2 represented in the production corpus;
- `user/*`: 26 documented, 7 production-corpus routes;
- `remote/*`: 16 documented, 9 production-corpus routes, all 9 overlap;
- `deviceState/*`: 10 documented, 5 production-corpus routes, all 5 overlap;
- `device/*`: both sources are broad but diverge heavily;
- reporting/portal metadata is richer in production traffic than in the preserved Swagger reference.

This shape is consistent with the Swagger describing the wider platform/API product while the HAR represents what one production web session and account actually exercised.

---

## 18. Windows-project implications

The user's Windows application does **not** need Android.

After Round 05 the preferred architecture is even clearer:

### Core cloud integration

Use the independent HTTPS API directly.

The highest-confidence core remains:

- account login;
- session token;
- rotating refresh;
- station/device discovery;
- current state;
- energy flow;
- attribute metadata;
- historical telemetry;
- aggregate statistics;
- alarms;
- configuration read/write where explicitly desired.

### Capability layers

Treat API features in tiers:

1. **CURRENT-PRODUCTION-CONFIRMED**
   - observed in current/relatively fresh production traffic.

2. **LIVE-VALIDATED**
   - successfully exercised by independent clients with documented outcomes.

3. **SWAGGER-DOCUMENTED**
   - defined by first-party contract, but not yet proven for the user's role/device.

4. **ROLE/MANUFACTURER-GATED**
   - service exists but availability depends on account/device/manufacturer flags.

5. **HISTORICAL/UNVERIFIED**
   - old route, guessed behavior, or unsupported prototype.

### IDs

Continue to preserve IDs as strings/lossless integers.

### Endpoint discovery

The Windows client should eventually capture and cache:

- login roles;
- device details;
- gather protocol/version;
- attribute metadata;
- supported fast-report capability;
- writable config attributes;
- feature-specific server errors.

---

## 19. What Round 05 changes in the master API model

Before this round, the strongest broad inventory was the 108-route production HAR / 112-route expanded catalog.

After this round:

- we have a second first-party-derived corpus with **221 tabled API contracts**;
- the two corpora overlap on only 62 exact method/path pairs;
- the union is far larger and more heterogeneous;
- the signing and refresh models are more strongly corroborated;
- the platform's role model is explicit;
- local/proximal protocol-generation APIs are documented independently of Android;
- documented services can be gated at manufacturer level;
- the production API evolves beyond the preserved Swagger snapshot.

This materially reduces the value of further Android reverse engineering for the Windows objective.

---

## 20. Sources used

First-party/current web surfaces:

- `https://solar.siseli.com/`
- `https://test.solar.siseli.com/`
- `http://doc.solar.siseli.com/openapi/#/` — known documentation location; current fetch unavailable in this environment
- `http://doc.solar.siseli.com/openapi/swagger2/api-docs?group=openApis` — known Swagger endpoint; current fetch unavailable
- `https://download.app.solar.siseli.com/`
- `https://sqr.app.siseli.com/`

Preserved documentation and history:

- `lujian1324-spec/energy-app/API_REFERENCE.md`
- commit `d5eee2e86fcb17b916a368c3ca8f085001cf0f2c` — Swagger route correction/integration
- later July API-reference/live-validation commits
- `Conexo-Casa/solar-of-things-ha` commit `757e24297cd6330f4d00acf22566d936d467f433` — production-vs-test environment correction
- Round 02 and Round 03 canonical reports in this repository.

---

## 21. Round-close assessment

Round 05 is complete.

A separate round devoted only to `demo.doc.solar.siseli.com` is **not justified** by current evidence. Its purpose remains unresolved, but no unique evidence surfaced that would outweigh more important API work.

### Next-round decision

Do **not** jump to MQTT/BLE yet.

The discovery of a first-party Swagger-derived contract with 221 tabled routes materially changes the highest-value next step.

Proceed to:

## Round 06 — OpenAPI Contract Reconstruction and Windows-Client Applicability

Keep it separate because it is now a large, coherent API-contract task.

Goals:

1. transform the Swagger-derived 221-route document into a machine-auditable endpoint catalog;
2. attach method, parameters, body DTO, response DTO/schema clues, mutation/read classification and service group;
3. merge provenance from:
   - Swagger;
   - production HAR;
   - fresh production capture;
   - live independent validation;
4. identify the **minimum safe/read-only Windows subset**;
5. identify optional management/control subsets;
6. classify role/manufacturer/device gating;
7. capture known error semantics;
8. isolate endpoints whose contracts are stale or contradicted by current production;
9. preserve local/near-DTU endpoints as a separate capability layer rather than confusing them with ordinary cloud telemetry;
10. produce a Windows-oriented canonical API map without writing implementation code yet.

Only after that contract normalization should the investigation move outward to MQTT, BLE, or raw serial layers.

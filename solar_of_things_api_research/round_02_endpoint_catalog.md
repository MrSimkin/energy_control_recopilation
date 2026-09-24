# Round 02 Appendix — Cloud REST Endpoint Catalog

Date: 2026-09-24

Status: COMPLETE — companion to `round_02_cloud_rest_api_repository_archaeology.md`

## Evidence model

- **HAR-OBSERVED** — route is present in the original 108-route catalog reconstructed from a captured `solar.siseli.com` browser HAR (405 API requests total).
- **LOGIN-PERMISSION** — route was added later to the catalog because it appeared in permissions returned by `/apis/login/account`; it was not one of the original 108 HAR-observed routes.
- **LATER-LIVE/IMPLEMENTED** — outside that 108-route HAR baseline but supported by later live capture/testing or a working client implementation.
- **LEGACY/UNVERIFIED** — route name appears in code/docs but lacks sufficient current evidence; do not build against it without verification.

Original HAR baseline: **108 unique routes**. Current python-siseli catalog: **112 unique routes**. Difference: **4 permission-discovered routes**.

The current upstream document still says “108 unique endpoints” in its prose, but its table now contains 112 unique routes. The four additions are marked below.

## alarm

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/alarm/report/alarmList/headers` | — | — | list |
| HAR-OBSERVED | GET | `/apis/alarm/report/record/details` | id | — | dict |
| HAR-OBSERVED | POST | `/apis/alarm/getLatestAlarm` | — | certificateDtuID, count, deviceSerialNumber, page | dict |
| HAR-OBSERVED | POST | `/apis/alarm/query/list` | — | certificateDtuID, count, deviceSerialNumber, fromTime, isProcessed, level, orderByCreatedTimeDesc, page, toTime | dict |
| HAR-OBSERVED | POST | `/apis/alarm/report/alarmList/export` | — | dtuID, fieldNames, fromTime, remark, toTime | dict |
| HAR-OBSERVED | POST | `/apis/alarm/report/record/list` | — | count, createdFromTime, createdToTime, dtuID, orderByCreatedAtAsc, page, state | dict |

## currency

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/currency/list` | — | — | list |

## dashboard

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/dashboard/summary/commons` | — | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/dashboard/summary/station/dailyGenerationTimeRank` | asc | — | list; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/dashboard/summary/station/distribution/location` | — | eastLongitude, level, northLatitude, southLatitude, westLongitude | list |
| HAR-OBSERVED | POST | `/apis/dashboard/summary/station/generatedEnergy/monthly` | — | — | list; POST без JSON-полей в теле в наблюдаемом вызове |

## device

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/device/details` | deviceId | — | dict |
| HAR-OBSERVED | GET | `/apis/device/linkage/rule/detail` | attributeCategory, deviceId | — | dict |
| HAR-OBSERVED | GET | `/apis/device/query/attribute/group` | category, deviceId, renderIn | — | dict |
| HAR-OBSERVED | GET | `/apis/device/report/export/records/details` | id | — | dict |
| HAR-OBSERVED | GET | `/apis/device/report/get/daily/excel/header` | deviceId | — | list |
| HAR-OBSERVED | GET | `/apis/device/report/get/list/excel/header` | — | — | list; Колонки экспортного отчета |
| HAR-OBSERVED | GET | `/apis/device/report/get/monthly/excel/header` | deviceId | — | list |
| HAR-OBSERVED | GET | `/apis/device/report/get/yearly/excel/header` | deviceId | — | list |
| HAR-OBSERVED | GET | `/apis/device/state/count` | — | — | list |
| HAR-OBSERVED | GET | `/apis/device/upgrade/device/names` | — | — | list |
| HAR-OBSERVED | GET | `/apis/device/upgrade/firmware/names` | — | — | list |
| HAR-OBSERVED | GET | `/apis/device/upgrade/protocol/names` | — | — | list |
| HAR-OBSERVED | POST | `/apis/device/external/list` | — | mainDeviceId | list |
| HAR-OBSERVED | POST | `/apis/device/gather/protocol/open/search` | — | applyModeCategory, count, page | dict |
| HAR-OBSERVED | POST | `/apis/device/list` | — | applyModeCategory, count, deviceSortKey, dtuDtuid, dtuId, exportType, fieldNames, gatherProtocolNumber, name, orderByCreatedAtAsc, orderByInstalledAtAsc, orderByNameAsc, orderByProducingPowerAsc, orderBySerialNumberAsc, orderByStateAsc, page, remark, serialNumber, softwareVersion, state, stationId | dict |
| HAR-OBSERVED | POST | `/apis/device/report/daily` | — | fieldNames, fromTime, id, remark, toTime | dict |
| HAR-OBSERVED | POST | `/apis/device/report/export/records/list` | — | count, deviceName, deviceSerialNumber, fromTime, page, state, timeAsc, toTime, type | dict |
| HAR-OBSERVED | POST | `/apis/device/report/monthly` | — | fieldNames, fromTime, id, remark, toTime | dict |
| HAR-OBSERVED | POST | `/apis/device/report/yearly` | — | fieldNames, fromTime, id, remark, toTime | dict |
| HAR-OBSERVED | POST | `/apis/device/upgrade/list` | — | count, deviceName, deviceSerialNumber, firmwareName, fromTime, page, protocolName, status, toTime | dict |

## deviceOffset

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/deviceOffset/totally` | — | deviceId | list |

## deviceOverView

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/deviceOverView/generatedEnergy/daily` | deviceId | time | list |
| HAR-OBSERVED | POST | `/apis/deviceOverView/stateAttributeSummary/category/daily` | deviceId, summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/deviceOverView/stateAttributeSummary/category/monthly` | deviceId, summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/deviceOverView/stateAttributeSummary/category/total` | deviceId, summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/deviceOverView/stateAttributeSummary/category/yearly` | deviceId, summaryCategoryKey | time | dict |

## deviceSort

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/deviceSort/sorts/all` | — | — | list |

## deviceState

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/deviceState/simple/energy/flow/v1` | dataSource, deviceId | — | dict |
| HAR-OBSERVED | GET | `/apis/deviceState/simple/gatherAttributes/v1` | category, deviceId, renderIn | — | list |
| HAR-OBSERVED | GET | `/apis/deviceState/simple/state/latest/v1` | dataSource, deviceId | — | dict |
| HAR-OBSERVED | POST | `/apis/deviceState/simple/attribute/keys/history/v1` | — | count, deviceId, fromTime, keys, orderByTimeAsc, page, toTime | dict |
| HAR-OBSERVED | POST | `/apis/deviceState/simple/attribute/record/list/v1` | — | count, deviceId, fromTime, orderByTimeAsc, page, toTime | dict |

## dictionary

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/dictionary/data/alarm` | — | — | dict |
| HAR-OBSERVED | GET | `/apis/dictionary/data/device` | — | — | dict |
| LOGIN-PERMISSION | GET | `/apis/dictionary/data/dtu` | — | — | dict; Обнаружен в permissions ответа /apis/login/account |
| HAR-OBSERVED | GET | `/apis/dictionary/data/report` | — | — | dict |
| HAR-OBSERVED | GET | `/apis/dictionary/data/simcard` | — | — | dict |
| HAR-OBSERVED | GET | `/apis/dictionary/data/station` | — | — | dict |

## dtu

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/dtu/count/general` | — | — | dict |
| HAR-OBSERVED | GET | `/apis/dtu/models` | — | — | list |
| HAR-OBSERVED | POST | `/apis/dtu/query/list` | — | count, dtuid, isActived, isOnline, isUpgradeAuto, model, page | dict |
| HAR-OBSERVED | POST | `/apis/dtu/replace/list` | — | count, deviceId, deviceSerialNumber, dtuID, orderByReplacedAtAsc, page | dict |
| HAR-OBSERVED | POST | `/apis/dtu/select/dtu` | dtuId | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |

## geo

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/geo/location/ip/lookup/lite` | ip | — | dict |
| HAR-OBSERVED | GET | `/apis/geo/location/ip/lookup/myip/lite` | — | — | dict |

## getInfo

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/getInfo` | — | — | dict |

## getRouters

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/getRouters` | — | — | list |

## login

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/login/account` | — | account, password | dict |

## owner

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/owner/report/export/records/details` | id | — | dict |
| HAR-OBSERVED | GET | `/apis/owner/report/get/monthly/excel/header` | — | — | list; Колонки экспортного отчета |
| HAR-OBSERVED | GET | `/apis/owner/report/get/yearly/excel/header` | — | — | list; Колонки экспортного отчета |
| HAR-OBSERVED | POST | `/apis/owner/report/export/records/list` | — | count, fromTime, page, state, timeAsc, toTime, type | dict |
| HAR-OBSERVED | POST | `/apis/owner/report/monthly` | — | fieldNames, fromTime, id, remark, toTime | dict |
| HAR-OBSERVED | POST | `/apis/owner/report/yearly` | — | fieldNames, fromTime, id, remark, toTime | dict |

## ownerOverView

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/ownerOverView/select/ownerStatistics` | — | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/ownerOverView/station/stateAttributeSummary/category/daily` | summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/ownerOverView/station/stateAttributeSummary/category/monthly` | summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/ownerOverView/station/stateAttributeSummary/category/total` | summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/ownerOverView/station/stateAttributeSummary/category/yearly` | summaryCategoryKey | time | dict |

## portal

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/portal/info` | — | — | dict |

## remote

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/remote/device/configs/read/details` | batchReadId | — | dict |
| HAR-OBSERVED | GET | `/apis/remote/device/state/report/fast/supported` | deviceId | — | bool |
| HAR-OBSERVED | POST | `/apis/remote/device/config/read` | deviceId | id, key | dict; В HAR встречался код ошибки 71301 |
| LOGIN-PERMISSION | POST | `/apis/remote/device/config/write` | deviceId | id, key, value | dict; Обнаружен в permissions ответа /apis/login/account |
| LOGIN-PERMISSION | POST | `/apis/remote/device/config/write/records` | — | count, deviceId, fromTime, key, orderByCreatedAtAsc, page, state, toTime | dict; Обнаружен в permissions ответа /apis/login/account |
| LOGIN-PERMISSION | POST | `/apis/remote/device/configs/cache/clear` | deviceId | — | NoneType; Обнаружен в permissions ответа /apis/login/account |
| HAR-OBSERVED | POST | `/apis/remote/device/configs/cache/get` | deviceId | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/remote/device/configs/read` | deviceId | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/remote/dtu/restart` | dtuId | — | NoneType; POST без JSON-полей в теле в наблюдаемом вызове |

## resource

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/resource/download/media` | resid, save | — | empty, raw; Защищенная загрузка файла |
| HAR-OBSERVED | GET | `/apis/resource/download/public/media` | resid | — | raw; Публичная загрузка файла |

## rest

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/rest/api/list` | — | count, isLoggable, page | dict |

## sim

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/sim/card/report/details` | id | — | dict |
| HAR-OBSERVED | GET | `/apis/sim/card/report/headers` | — | — | list |
| HAR-OBSERVED | POST | `/apis/sim/card/report/export` | — | fieldNames, iccids, remark | dict |
| HAR-OBSERVED | POST | `/apis/sim/card/report/list` | — | count, createdFromTime, createdToTime, page, state | dict |

## simcard

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/simcard/search` | — | count, dataBalanceWarningCode, expiryWarningCode, isActived, page, status, supplierCode | dict |

## station

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/station/details` | stationId | — | dict |
| HAR-OBSERVED | GET | `/apis/station/energy/flow` | isManualRefresh, stationId | — | dict |
| HAR-OBSERVED | GET | `/apis/station/report/export/records/details` | id | — | dict |
| HAR-OBSERVED | GET | `/apis/station/report/get/monthly/excel/header` | — | — | list; Колонки экспортного отчета |
| HAR-OBSERVED | GET | `/apis/station/report/get/station/list/excel/header` | — | — | list; Колонки экспортного отчета |
| HAR-OBSERVED | GET | `/apis/station/report/get/yearly/excel/header` | — | — | list; Колонки экспортного отчета |
| HAR-OBSERVED | GET | `/apis/station/state/count` | — | — | list |
| HAR-OBSERVED | POST | `/apis/station/list` | — | connectedGridType, count, name, orderByConnectedGridTypeAsc, orderByCreatedAtAsc, orderByInstalledAtAsc, orderByInstalledCapacityAsc, orderByNameAsc, orderByStateAsc, orderByStationTypeAsc, page, state, stationType | dict |
| HAR-OBSERVED | POST | `/apis/station/report/export/records/list` | — | count, fromTime, page, state, stationName, timeAsc, toTime, type | dict |
| HAR-OBSERVED | POST | `/apis/station/report/monthly` | — | fieldNames, fromTime, id, remark, toTime | dict |

## stationOverView

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | POST | `/apis/stationOverView/income/daily` | stationId | time | list |
| HAR-OBSERVED | POST | `/apis/stationOverView/income/monthly` | stationId | time | list |
| HAR-OBSERVED | POST | `/apis/stationOverView/income/total` | stationId | time | list |
| HAR-OBSERVED | POST | `/apis/stationOverView/income/yearly` | stationId | time | list |
| HAR-OBSERVED | POST | `/apis/stationOverView/stateAttributeSummary/category/daily` | stationId, summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/stationOverView/stateAttributeSummary/category/monthly` | stationId, summaryCategoryKey | time | dict |
| HAR-OBSERVED | POST | `/apis/stationOverView/stateAttributeSummary/category/yearly` | stationId, summaryCategoryKey | time | dict |

## user

| Evidence | Method | Path | Query parameters observed | JSON/body fields observed | Response data shape / note |
|---|---|---|---|---|---|
| HAR-OBSERVED | GET | `/apis/user/currency` | — | — | dict |
| HAR-OBSERVED | POST | `/apis/user/currency/setting` | currencyCode | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/user/log/personal/search` | — | count, fromTime, orderByTimeDesc, page, toTime | dict |
| HAR-OBSERVED | POST | `/apis/user/personal/superiors/direct` | — | name, uid | list |
| HAR-OBSERVED | POST | `/apis/user/select/iotUserInfo` | — | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/user/select/userThemeColors` | — | — | dict; POST без JSON-полей в теле в наблюдаемом вызове |
| HAR-OBSERVED | POST | `/apis/user/update/iotUserInfo` | — | iconResid, name, remark | NoneType |

## Additional routes outside the original 108-route HAR baseline

| Evidence | Method | Path | What supports it | Notes |
|---|---|---|---|---|
| LATER-LIVE/IMPLEMENTED | POST | `/apis/login/refresh/access/token` | Conexo-Casa release history and current implementation after a live 404 correction | Body uses `refreshToken`; returns renewed token material. Not present in the July HAR catalog. |
| LATER-LIVE/IMPLEMENTED | GET | `/apis/device/dtu/info` | Windear/techfine_cloud (2025) | Query `dtuDtuid`; observed client extracts device IDs from `data.devicesAlreadyAdded`. Single independent implementation in this round, so confidence is high-but-not-cross-confirmed. |
| LATER-LIVE/IMPLEMENTED | POST | `/apis/deviceOverView/pvInverterPowerClass/daily/detail` | 2026-09-24/25 live browser capture documented in lujian1324-spec/energy-app | Query `deviceId`; seen on Device details → Data Analysis. |

## Legacy / unverified route names found during archaeology

| Route | Why it is not promoted to the confirmed catalog |
|---|---|
| `/api/device/settings/v1` | Historical Conexo implementation; later documented as wrong/nonexistent (404) and replaced by remote-config routes. |
| `/api/device/settings/update/v1` | Historical Conexo implementation; later documented as wrong/nonexistent (404). |
| `/apis/device/realTime` | Appears in older yuraantonov11/siseli-app documentation, but current client code no longer calls it. |
| `/apis/device/control` | Appears in older documentation/constants; no current implementation evidence located in this round. |
| `/apis/device/history` | Appears as a constant in ha-smart-inverter; no live/HAR/current-call evidence located. |
| `/apis/deviceState/attribute/record/list` | Mentioned as a fallback/older path in downstream app documentation, while HAR/current SDK use `/apis/deviceState/simple/attribute/record/list/v1`; exact alternate route remains unresolved. |

## Catalog cautions

- Presence in the HAR proves that the web portal issued the request in that capture; it does **not** prove every account/role/device may call it.
- Some endpoints are destructive or mutating (configuration writes, cache clear, DTU restart, user profile/currency changes, exports). They are cataloged from evidence only; this investigation did not execute them.
- Route availability, required fields, permissions, field names, units and semantics can vary by device model, firmware, gather protocol and account permissions.
- The later official-client rounds should verify this catalog against current production JavaScript and Android packages, because the July HAR is a snapshot rather than a timeless specification.

## Provenance

Primary catalog source: `vvkor/python-siseli`, `docs/api.md`. The original 108-route state is commit `7bb24e3da8aebf8422d1e75525c5141a95380efb`; current catalog inspected at commit `3493800699b6ea56d2e8cb0b4a680d9f8502277b`. Additional-route evidence is documented in the main Round 02 report.

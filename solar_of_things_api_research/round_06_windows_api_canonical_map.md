# Round 06 Companion — Canonical Windows API Route Map

Date: 2026-09-24

Status: COMPLETE

Companion to `round_06_openapi_contract_windows_applicability.md`.

## Scope

This is the canonical **method+path union** of:

- the 221-route preserved first-party Swagger-derived contract; and
- the 115-route production-observed corpus accumulated through Rounds 02–03.

The resulting union contains **274 distinct method/path contracts**.

Base URL for production REST calls:

`https://solar.siseli.com/apis`

Paths below are relative to that base.

No reusable platform application secret is reproduced in this file.

## Source labels

- **CURRENT-PRODUCTION+...** — directly/currently corroborated in Round 03 or current live-production work.
- **SWAGGER+PRODUCTION** — appears in both the preserved first-party contract and production evidence.
- **SWAGGER-ONLY** — documented but not present in the captured production corpus.
- **PRODUCTION-ONLY** — observed/implemented in production but absent from the preserved Swagger tables.

A Swagger-only route is not assumed to be enabled for an ordinary owner account.

## Priority labels

- **P0** — minimal Windows cloud-client core: authentication/session, discovery, current telemetry, metadata, history and alarms.
- **P1** — useful optional read/analytics/configuration capabilities.
- **P2** — mutations or device/control actions; disabled by default in a monitoring client.
- **P3** — specialized administrative or local/protocol-generation surfaces.

## Operation classes

| Class | Meaning |
|---|---|
| AUTH_SESSION | Login/refresh/logout/session lifecycle |
| AUTH_SUPPORT | Captcha/telephone/auth-support metadata |
| TELEMETRY_ANALYTICS_READ | Passive state/history/statistical analytics |
| DEVICE_READ | Read-oriented device API |
| ACTIVE_DEVICE_READ | May actively request/read configuration from device/logger |
| DEVICE_CONTROL_READ | Read capability/settings for a control subsystem |
| OPTIONAL_READ | Other metadata/search/report/profile reads |
| ACCOUNT_MUTATION | Account/profile/credential/captcha/account-management mutation |
| MANAGEMENT_MUTATION | Station/device/alarm/resource administrative mutation |
| DEVICE_CONTROL_MUTATION | Settings, passthrough, restart, scheduling, firmware/control action |
| ADMIN_READ / ADMIN_MUTATION | Role-oriented administration/manufacturer/integrator/SIM functions |
| LOCAL_PROTOCOL_HELPER | /near/dtu protocol-generation/parsing service |

## Counts

| Measure | Count |
|---|---:|
| Canonical union | 274 |
| P0 | 12 |
| P1 | 170 |
| P2 | 60 |
| P3 | 32 |


## admin

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | ADMIN_READ | POST | `/admin/region/coding/reverse` | SWAGGER-ONLY | 将gis点反转为管理区域 | GisPointDtio |
| P3 | ADMIN_READ | POST | `/admin/region/coding/reverse/ip` | SWAGGER-ONLY | 将IP地址反转为管理区域 | Query: ip* |
| P3 | ADMIN_READ | POST | `/admin/region/coding/reverse/myip` | SWAGGER-ONLY | 将我的IP地址反转为管理区域 | - |

## alarm

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/alarm/report/alarmList/headers` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取告警列表报表头 | - |
| P1 | OPTIONAL_READ | GET | `/alarm/report/record/details` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取告警报表详情 | Query: id* |
| P2 | MANAGEMENT_MUTATION | POST | `/alarm/delete/alarm` | SWAGGER-ONLY | 删除告警 | Query: id* |
| P1 | OPTIONAL_READ | POST | `/alarm/getLatestAlarm` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取最近一条告警 | AlarmSearchDtio |
| P0 | OPTIONAL_READ | POST | `/alarm/query/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询告警列表 | AlarmSearchDtio |
| P1 | OPTIONAL_READ | POST | `/alarm/report/alarmList/export` | SWAGGER+PRODUCTION:HAR-OBSERVED | 导出告警列表报表 | ExportAlarmListReportDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/alarm/report/record/delete/batch` | SWAGGER-ONLY | 批量删除告警报表 | - |
| P1 | OPTIONAL_READ | POST | `/alarm/report/record/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询告警报表 | AlarmReportSearchDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/alarm/update/isProcessed` | SWAGGER-ONLY | 忽略告警 | UpdateAlarmDtio |

## app

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/app/version/check` | SWAGGER-ONLY | 根据appId检查App版本（已废弃） | appId*, versionCode* |
| P1 | OPTIONAL_READ | GET | `/app/version/check/v2` | SWAGGER-ONLY | 根据包名检查APP版本 | packageName*, platform*, versionCode* |
| P1 | OPTIONAL_READ | POST | `/app/scan/qrcode/login` | SWAGGER-ONLY | 二维码登录确认 | QRCodeLoginDtio |

## currency

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/currency/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 货币列表 | — ; — ; list |

## dashboard

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/dashboard/summary/commons` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; dict; POST без JSON-полей в теле в наблюдаемом вызове |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/dashboard/summary/station/dailyGenerationTimeRank` | PRODUCTION-ONLY:HAR-OBSERVED | — | asc ; — ; list; POST без JSON-полей в теле в наблюдаемом вызове |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/dashboard/summary/station/distribution/location` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; eastLongitude, level, northLatitude, southLatitude, westLongitude ; list |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/dashboard/summary/station/generatedEnergy/monthly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 汇总当月每天的发电量 | — ; — ; list; POST без JSON-полей в теле в наблюдаемом вызове |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/dashboard/summary/station/generatedEnergy/total` | SWAGGER-ONLY | 汇总每年的发电量 | — |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/dashboard/summary/station/generatedEnergy/yearly` | SWAGGER-ONLY | 汇总当年每月的发电量 | — |

## device

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P0 | OPTIONAL_READ | GET | `/device/details` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查看设备详情 | deviceId* ; - |
| P1 | OPTIONAL_READ | GET | `/device/dtu/info` | SWAGGER+PRODUCTION:LATER-LIVE/IMPLEMENTED | 获取设备采集器信息 | dtuDtuid* ; - |
| P1 | OPTIONAL_READ | GET | `/device/linkage/rule/detail` | PRODUCTION-ONLY:HAR-OBSERVED | — | attributeCategory, deviceId ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/device/query/attribute/group` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询设备属性分组列表 | deviceId*, category, renderIn ; - |
| P1 | OPTIONAL_READ | GET | `/device/query/by/dtuId` | SWAGGER-ONLY | 根据dtuId查设备 | dtuId* ; - |
| P1 | OPTIONAL_READ | GET | `/device/query/dtuids` | SWAGGER-ONLY | 查询设备采集器列表 | dtuids* ; - |
| P1 | OPTIONAL_READ | GET | `/device/report/export/records/details` | PRODUCTION-ONLY:HAR-OBSERVED | — | id ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/device/report/get/daily/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | deviceId ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/report/get/list/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list; Колонки экспортного отчета |
| P1 | OPTIONAL_READ | GET | `/device/report/get/monthly/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | deviceId ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/report/get/yearly/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | deviceId ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/state/count` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/upgrade/device/names` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/upgrade/firmware/names` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/upgrade/logs` | SWAGGER-ONLY | 获取设备升级日志 | deviceUpgradeId* ; - |
| P1 | OPTIONAL_READ | GET | `/device/upgrade/protocol/names` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |
| P1 | OPTIONAL_READ | GET | `/device/upgrade/script/file/info` | SWAGGER-ONLY | 获取设备升级脚本文件信息 | protocolId*, deviceId, certificateDtuID ; - |
| P2 | MANAGEMENT_MUTATION | POST | `/device/add/single` | SWAGGER-ONLY | 添加单个设备 | - ; AddSingleMainDeviceDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/device/add/single/addStationTogether` | SWAGGER-ONLY | 添加设备同时创建电站 | - ; AddSingleDeviceAndStationDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/device/delete` | SWAGGER-ONLY | 删除设备 | - ; DeleteDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/device/dtu/replace` | SWAGGER-ONLY | 更换设备采集器 | - ; DeviceReplaceDtuDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/device/external/add/single` | SWAGGER-ONLY | 添加单个外挂设备 | - ; AddSingleExternalDeviceDtio |
| P1 | OPTIONAL_READ | POST | `/device/external/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取主设备的外挂设备列表 | - ; ExternalDeviceListDtio |
| P1 | OPTIONAL_READ | POST | `/device/firmware/list` | SWAGGER-ONLY | 查询设备固件列表 | - ; DeviceFirmwareSearchDtio |
| P1 | OPTIONAL_READ | POST | `/device/firmware/list/fromManufacturer` | SWAGGER-ONLY | 根据设备id查询厂家固件列表 | deviceId, certificateDtuID ; DeviceFirmwareSearchDtio |
| P1 | OPTIONAL_READ | POST | `/device/gather/protocol/open/search` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询设备采集协议开放信息 | - ; GatherProtocolOpenSearchDtio |
| P0 | OPTIONAL_READ | POST | `/device/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询设备列表 | - ; DeviceListDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/device/pin` | SWAGGER-ONLY | 置顶设备 | - ; GeneralIdsDtio |
| P1 | OPTIONAL_READ | POST | `/device/report/daily` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; fieldNames, fromTime, id, remark, toTime ; dict |
| P1 | OPTIONAL_READ | POST | `/device/report/export/records/list` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, deviceName, deviceSerialNumber, fromTime, page, state, timeAsc, toTime, type ; dict |
| P1 | OPTIONAL_READ | POST | `/device/report/monthly` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; fieldNames, fromTime, id, remark, toTime ; dict |
| P1 | OPTIONAL_READ | POST | `/device/report/yearly` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; fieldNames, fromTime, id, remark, toTime ; dict |
| P2 | MANAGEMENT_MUTATION | POST | `/device/unpin` | SWAGGER-ONLY | 取消置顶设备 | - ; GeneralIdsDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/device/update` | SWAGGER-ONLY | 编辑设备信息 | - ; DeviceUpdateDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/device/upgrade/create` | SWAGGER-ONLY | 创建设备升级 | - ; DeviceUpgradeCreateDtio |
| P1 | OPTIONAL_READ | POST | `/device/upgrade/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询设备升级列表 | - ; DeviceUpgradeSearchDtio |

## deviceApplyMode

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/deviceApplyMode/modes/external` | SWAGGER-ONLY | 获取设备外挂应用模式列表 | - |
| P1 | OPTIONAL_READ | GET | `/deviceApplyMode/modes/main` | SWAGGER-ONLY | 获取设备主应用模式列表 | - |

## deviceManufacturer

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P2 | ADMIN_MUTATION | POST | `/deviceManufacturer/add` | SWAGGER-ONLY | 创建厂家 | DeviceManufacturerAddDtio |

## deviceOffset

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/deviceOffset/details` | SWAGGER-ONLY | 查看设备偏移量详情 | deviceId* ; - |
| P1 | OPTIONAL_READ | GET | `/deviceOffset/totally` | SWAGGER-ONLY | 获取总偏移量 | deviceId* ; - |
| P1 | OPTIONAL_READ | POST | `/deviceOffset/list` | SWAGGER-ONLY | 查询设备偏移量列表 | - ; DeviceOffsetSearchDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/deviceOffset/set` | SWAGGER-ONLY | 设置设备偏移量 | - ; DeviceOffsetSetDtio |
| P1 | OPTIONAL_READ | POST | `/deviceOffset/totally` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; deviceId ; list |

## deviceOverView

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/generatedEnergy/daily` | CURRENT-PRODUCTION+PRODUCTION-ONLY | — | deviceId ; time ; list |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/generatedEnergy/monthly` | SWAGGER-ONLY | 获取设备当月每天发电量 | deviceId* ; {time: "yyyy-mm"} |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/generatedEnergy/total` | SWAGGER-ONLY | 获取设备历年发电量 | deviceId* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/generatedEnergy/yearly` | SWAGGER-ONLY | 获取设备当年每月发电量 | deviceId* ; {time: "yyyy"} |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/generationPower/daily` | SWAGGER-ONLY | 获取设备当日发电功率明细 | deviceId* ; {time: "yyyy-mm-dd"} |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/pvInverterPowerClass/daily/detail` | CURRENT-PRODUCTION+PRODUCTION-ONLY | — | 2026-09-24/25 live browser capture documented in lujian1324-spec/energy-app ; Query deviceId; seen on Device details → Data Analysis. |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/category/daily` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目设备日汇总 | deviceId*, summaryCategoryKey* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/category/monthly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目设备月度汇总 | deviceId*, summaryCategoryKey* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/category/total` | CURRENT-PRODUCTION+SWAGGER+PRODUCTION | 获取状态属性统计类目设备年总计 | deviceId*, summaryCategoryKey* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/category/yearly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目设备年度汇总 | deviceId*, summaryCategoryKey* ; YearlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/daily` | SWAGGER-ONLY | 获取设备状态属性日度汇总 | deviceId*, summaryPropertyKey* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/monthly` | SWAGGER-ONLY | 获取设备状态属性月度汇总 | deviceId*, summaryPropertyKey* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/total` | SWAGGER-ONLY | 获取设备状态属性年总计 | deviceId*, summaryPropertyKey* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceOverView/stateAttributeSummary/yearly` | SWAGGER-ONLY | 获取设备状态属性年度汇总 | deviceId*, summaryPropertyKey* ; YearlyDtio |

## deviceSort

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/deviceSort/sorts/all` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取所有设备种类 | - |
| P1 | OPTIONAL_READ | GET | `/deviceSort/sorts/details` | SWAGGER-ONLY | 查看设备种类详情 | id* |

## deviceState

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | TELEMETRY_ANALYTICS_READ | GET | `/deviceState/gatherAttributes` | SWAGGER-ONLY | 获取设备属性列表 | deviceId*, category*, renderIn ; - |
| P0 | TELEMETRY_ANALYTICS_READ | GET | `/deviceState/simple/energy/flow/v1` | CURRENT-PRODUCTION+SWAGGER+PRODUCTION | 获取设备简单能量流动 | deviceId*, dataSource |
| P0 | TELEMETRY_ANALYTICS_READ | GET | `/deviceState/simple/gatherAttributes/v1` | CURRENT-PRODUCTION+SWAGGER+PRODUCTION | 获取设备属性简单列表 | deviceId*, category*, renderIn |
| P0 | TELEMETRY_ANALYTICS_READ | GET | `/deviceState/simple/state/latest/v1` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取设备最新状态简单数据 | deviceId*, dataSource |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceState/attribute/keys/history` | SWAGGER-ONLY | 获取设备指定属性历史数据 | - ; DeviceAttributeKeysHistoryPageDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceState/attribute/record/list` | SWAGGER-ONLY | 获取设备状态明细数据 | - ; DeviceAttributeStatePageDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceState/attribute/record/list/v2` | SWAGGER-ONLY | 获取设备状态明细数据V2 | - ; DeviceAttributeStatePageDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceState/attribute/record/time/list` | SWAGGER-ONLY | 获取设备状态数据更新时间 | - ; DeviceAttributeStatePageDtio |
| P0 | TELEMETRY_ANALYTICS_READ | POST | `/deviceState/simple/attribute/keys/history/v1` | CURRENT-PRODUCTION+SWAGGER+PRODUCTION | 获取设备指定属性简单历史数据 | Body: DeviceAttributeKeysHistoryPageDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/deviceState/simple/attribute/record/list/v1` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取设备状态简单数据 | Body: DeviceAttributeStatePageDtio |

## dictionary

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/dictionary/data/alarm` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取告警数据字典 | — ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/device` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取设备数据字典 | — ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/dtu` | PRODUCTION-ONLY:LOGIN-PERMISSION | — | — ; — ; dict; Обнаружен в permissions ответа /apis/login/account |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/hjsa/version` | SWAGGER-ONLY | 获取HJSA脚本数据字典 | — |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/report` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取报表数据字典 | — ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/sensitive/country` | SWAGGER-ONLY | 获取敏感国家数据字典 | — |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/simcard` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取SIM卡数据字典 | — ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/dictionary/data/station` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取站点数据字典 | — ; — ; dict |

## dtu

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/dtu/count/general` | SWAGGER+PRODUCTION:HAR-OBSERVED | 采集器常规统计 | - ; - |
| P1 | OPTIONAL_READ | GET | `/dtu/models` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |
| P1 | OPTIONAL_READ | POST | `/dtu/query/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询采集器列表 | - ; DtuSearchDtio |
| P1 | OPTIONAL_READ | POST | `/dtu/replace/list` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, deviceId, deviceSerialNumber, dtuID, orderByReplacedAtAsc, page ; dict |
| P1 | OPTIONAL_READ | POST | `/dtu/select/dtu` | PRODUCTION-ONLY:HAR-OBSERVED | — | dtuId ; — ; dict; POST без JSON-полей в теле в наблюдаемом вызове |
| P1 | OPTIONAL_READ | POST | `/dtu/select/dtu/withDtuID` | SWAGGER-ONLY | 根据DtuID查看采集器详情 | DtuID* ; - |

## factoryOverView

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | ADMIN_READ | POST | `/factoryOverView/select/alertsHistorical` | SWAGGER-ONLY | 厂家设备告警排行 | — |
| P3 | ADMIN_READ | POST | `/factoryOverView/select/deviceDistributionRanking` | SWAGGER-ONLY | 厂家设备分布排名 | — |
| P3 | ADMIN_READ | POST | `/factoryOverView/select/deviceOnlineRate` | SWAGGER-ONLY | 厂家设备在线率 | — |
| P3 | ADMIN_READ | POST | `/factoryOverView/select/manufacturerStatistics` | SWAGGER-ONLY | 厂家统计信息 | — |

## gather

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | ADMIN_READ | GET | `/gather/protocol/manufacturerDeviceUpgradeProtocol/overviews` | SWAGGER-ONLY | 获取厂家设备固件升级协议概述列表 | gatherProtocolId* ; - |
| P2 | ADMIN_MUTATION | POST | `/gather/protocol/manufacturerDeviceUpgradeProtocol/bind` | SWAGGER-ONLY | 绑定厂家设备固件升级协议 | - ; GatherProtocolManufacturerUpgradeProtocolBindDtio |

## geo

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/geo/location/ip/lookup/lite` | SWAGGER+PRODUCTION:HAR-OBSERVED | 根据IP查询地理位置-精简版 | ip* |
| P1 | OPTIONAL_READ | GET | `/geo/location/ip/lookup/myip/lite` | SWAGGER+PRODUCTION:HAR-OBSERVED | 根据当前IP查询地理位置-精简版 | - |

## getInfo

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/getInfo` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; dict |

## getRouters

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/getRouters` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |

## graphic

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | AUTH_SUPPORT | POST | `/graphic/validation/code/generate` | SWAGGER-ONLY | 生成滑块拼图验证码 | Query: intent* |
| P1 | AUTH_SUPPORT | POST | `/graphic/validation/code/verify` | SWAGGER-ONLY | 校验滑块拼图验证码 | VerifyGraphicVerificationCodeDtio |

## instruction

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | DEVICE_CONTROL_READ | GET | `/instruction` | SWAGGER-ONLY | 获取自动化指令详情 | id* ; - |
| P1 | DEVICE_CONTROL_READ | GET | `/instruction/historyListOfinstruction` | SWAGGER-ONLY | 分页查询指令执行历史（按指令ID） | instructionId*, pageNo, pageSize ; - |
| P1 | DEVICE_CONTROL_READ | GET | `/instruction/list` | SWAGGER-ONLY | 分页查询自动化指令 | deviceId*, pageNo, pageSize ; - |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/instruction/add` | SWAGGER-ONLY | 添加自动化指令 | - ; InstructionVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/instruction/delete` | SWAGGER-ONLY | 删除自动化指令 | id* ; - |
| P1 | DEVICE_CONTROL_READ | POST | `/instruction/historyList` | SWAGGER-ONLY | 分页查询指令执行历史 | - ; AutoInstructionLogDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/instruction/update` | SWAGGER-ONLY | 更新自动化指令 | - ; InstructionVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/instruction/updateStatus` | SWAGGER-ONLY | 更新自动化指令状态 | id*, status* ; - |

## integratorsOverView

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | ADMIN_READ | POST | `/integratorsOverView/select/integratorsStatistics` | SWAGGER-ONLY | 获取集成商资源详情 | - |
| P3 | ADMIN_READ | POST | `/integratorsOverView/select/powerStationRanking` | SWAGGER-ONLY | 获取电站满发时间排名 | asc |

## login

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P0 | AUTH_SESSION | POST | `/login/account` | CURRENT-PRODUCTION+SWAGGER+PRODUCTION | 账号密码登录 | UserAccountLoginDtio |
| P2 | AUTH_SESSION | POST | `/login/email` | SWAGGER-ONLY | 邮箱验证码登录 | UserEmailLoginDtio |
| P2 | AUTH_SESSION | POST | `/login/inviteCode` | SWAGGER-ONLY | 邀请码登录 | InviteCodeLoginDtio |
| P0 | AUTH_SESSION | POST | `/login/logout` | SWAGGER-ONLY | 退出登录 | LogoutDtio |
| P2 | AUTH_SESSION | POST | `/login/passwordless/login` | SWAGGER-ONLY | 免密登录下级用户 | UserLoginDirectlyDtio |
| P0 | AUTH_SESSION | POST | `/login/refresh/access/token` | CURRENT-PRODUCTION+SWAGGER+PRODUCTION | 刷新 Token | RefreshTokenDtio |
| P2 | AUTH_SESSION | POST | `/login/sms` | SWAGGER-ONLY | 手机短信验证码登录 | UserSmsLoginDtio |

## near

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | LOCAL_PROTOCOL_HELPER | GET | `/near/dtu/device/attribute/groups` | SWAGGER-ONLY | 获取采集属性分组列表 | protocolNo*, verCode*, category ; - |
| P3 | LOCAL_PROTOCOL_HELPER | GET | `/near/dtu/gatherProtocol` | SWAGGER-ONLY | 获取采集协议 | protocolNo*, verCode* ; - |
| P3 | LOCAL_PROTOCOL_HELPER | GET | `/near/dtu/hfmi/detail` | SWAGGER-ONLY | 获取智慧屏hfmi文件详情 | langCode*, projID*, projVerCode* ; - |
| P3 | LOCAL_PROTOCOL_HELPER | GET | `/near/dtu/hfmi/overviews` | SWAGGER-ONLY | 获取智慧屏hfmi文件概览列表 | - ; - |
| P3 | LOCAL_PROTOCOL_HELPER | GET | `/near/dtu/hisScript` | SWAGGER-ONLY | 获取dtu的His脚本 | - ; - |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/checkin` | SWAGGER-ONLY | 签到 | - ; - |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/detectGatherProtocol` | SWAGGER-ONLY | 侦测采集协议 | - ; NearDetectGatherProtocolVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/gen/device/config/pre/write` | SWAGGER-ONLY | 生成预设置设备配置项指令 | protocolNo*, verCode* ; WriteRemoteDeviceConfigVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/gen/device/config/read` | SWAGGER-ONLY | 生成读取设备配置项指令 | protocolNo*, verCode* ; ReadRemoteDeviceConfigVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/gen/device/config/write` | SWAGGER-ONLY | 生成设置设备配置项指令 | protocolNo*, verCode* ; NearWriteDeviceConfigVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/gen/device/configs/read` | SWAGGER-ONLY | 生成批量读取设备配置项指令 | protocolNo*, verCode* ; ReadRemoteDeviceConfigsVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/parse/device/config/read` | SWAGGER-ONLY | 解析设备配置项读取响应 | protocolNo*, verCode* ; NearParseDeviceConfigVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/parse/device/config/write` | SWAGGER-ONLY | 解析设备配置项设置响应 | protocolNo*, verCode* ; NearParseDeviceConfigWriteVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/parse/device/configs/read` | SWAGGER-ONLY | 解析设备配置项批量读取响应 | protocolNo*, verCode* ; NearParseDeviceConfigsVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/parse/device/energy/flow` | SWAGGER-ONLY | 解析设备能量流动数据 | protocolNo*, verCode* ; NearParseDeviceStateVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/parse/device/event` | SWAGGER-ONLY | 解析设备事件数据 | protocolNo*, verCode* ; NearParseDeviceStateVijo |
| P3 | LOCAL_PROTOCOL_HELPER | POST | `/near/dtu/parse/device/state` | SWAGGER-ONLY | 解析设备状态数据 | protocolNo*, verCode* ; NearParseDeviceStateVijo |

## owner

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/owner/report/export/records/details` | PRODUCTION-ONLY:HAR-OBSERVED | — | id ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/owner/report/get/monthly/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list; Колонки экспортного отчета |
| P1 | OPTIONAL_READ | GET | `/owner/report/get/yearly/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list; Колонки экспортного отчета |
| P1 | OPTIONAL_READ | POST | `/owner/report/export/records/list` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, fromTime, page, state, timeAsc, toTime, type ; dict |
| P1 | OPTIONAL_READ | POST | `/owner/report/monthly` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; fieldNames, fromTime, id, remark, toTime ; dict |
| P1 | OPTIONAL_READ | POST | `/owner/report/yearly` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; fieldNames, fromTime, id, remark, toTime ; dict |

## ownerOverView

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/select/ownerStatistics` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取站点业主资源详情 | - ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/generatedEnergy/monthly` | SWAGGER-ONLY | 获取电站月发电量 | - ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/generatedEnergy/total` | SWAGGER-ONLY | 获取电站总发电量 | - ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/generatedEnergy/yearly` | SWAGGER-ONLY | 获取电站年发电量 | - ; YearlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/generationPower/daily` | SWAGGER-ONLY | 获取电站日发电量 | - ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/category/daily` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目业主日汇总 | summaryCategoryKey* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/category/monthly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目业主月度汇总 | summaryCategoryKey* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/category/total` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目业主年总计 | summaryCategoryKey* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/category/yearly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目业主年度汇总 | summaryCategoryKey* ; YearlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/daily` | SWAGGER-ONLY | 获取设备状态属性业主日度汇总 | summaryPropertyKey* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/monthly` | SWAGGER-ONLY | 获取设备状态属性业主月度汇总 | summaryPropertyKey* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/total` | SWAGGER-ONLY | 获取设备状态属性业主年总计 | summaryPropertyKey* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/ownerOverView/station/stateAttributeSummary/yearly` | SWAGGER-ONLY | 获取设备状态属性业主年度汇总 | summaryPropertyKey* ; YearlyDtio |

## peakValley

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | DEVICE_CONTROL_READ | GET | `/peakValley/device/attribute/group` | SWAGGER-ONLY | 获取设备削峰填谷采集属性分组 | deviceId* ; - |
| P1 | DEVICE_CONTROL_READ | GET | `/peakValley/device/general/get` | SWAGGER-ONLY | 获取设备常规削峰填谷 | deviceId* ; - |
| P1 | DEVICE_CONTROL_READ | GET | `/peakValley/device/get` | SWAGGER-ONLY | 获取设备削峰填谷 | deviceId* ; - |
| P1 | DEVICE_CONTROL_READ | GET | `/peakValley/types/all` | SWAGGER-ONLY | 获取所有削峰填谷类型 | - ; - |
| P1 | DEVICE_CONTROL_READ | GET | `/peakValley/types/device` | SWAGGER-ONLY | 获取设备支持的削峰填谷类型 | deviceId*, includeDefault ; - |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/peakValley/device/customized/set` | SWAGGER-ONLY | 设置自定义削峰填谷 | - ; DeviceCustomizedPeakValleyDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/peakValley/device/enable` | SWAGGER-ONLY | 使能设备削峰填谷 | - ; DevicePeakValleyEnableDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/peakValley/device/general/set` | SWAGGER-ONLY | 设置设备常规削峰填谷 | - ; DeviceGeneralPeakValleyDtio |

## portal

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/portal/info` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; dict |

## remote

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | ACTIVE_DEVICE_READ | GET | `/remote/device/configs/read/details` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取批量读取详情 | batchReadId* ; - |
| P1 | DEVICE_READ | GET | `/remote/device/energy/flow` | SWAGGER-ONLY | 获取设备能量流动 | deviceId*, dataSource ; - |
| P1 | DEVICE_READ | GET | `/remote/device/state/latest` | SWAGGER-ONLY | 获取设备最近状态数据 | deviceId*, dataSource ; - |
| P1 | DEVICE_READ | GET | `/remote/device/state/report/fast/supported` | SWAGGER+PRODUCTION:HAR-OBSERVED | 检查是否支持速报 | deviceId* ; - |
| P1 | ACTIVE_DEVICE_READ | POST | `/remote/device/config/read` | SWAGGER+PRODUCTION:HAR-OBSERVED | 读取设备配置项 | deviceId* ; ReadRemoteDeviceConfigVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/device/config/write` | SWAGGER+PRODUCTION:LOGIN-PERMISSION | 设置设备配置项 | deviceId* ; WriteRemoteDeviceConfigVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/device/config/write/records` | SWAGGER+PRODUCTION:LOGIN-PERMISSION | 查询设备配置项写记录 | - ; DeviceConfigWriteRecordSearchDtio |
| P1 | OPTIONAL_READ | POST | `/remote/device/configs/cache/clear` | SWAGGER+PRODUCTION:LOGIN-PERMISSION | 清空设备配置项缓存 | deviceId* ; - |
| P1 | DEVICE_READ | POST | `/remote/device/configs/cache/get` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取设备配置项缓存 | deviceId* ; ReadRemoteDeviceConfigsVijo |
| P1 | ACTIVE_DEVICE_READ | POST | `/remote/device/configs/read` | SWAGGER+PRODUCTION:HAR-OBSERVED | 批量读取设备配置项 | deviceId* ; ReadRemoteDeviceConfigsVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/device/passthrough` | SWAGGER-ONLY | 透传数据 | deviceId* ; DeviceRemotePassthroughVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/device/state/report/fast/start` | SWAGGER-ONLY | 启动速报（实时数据快速上报） | deviceId* ; DeviceAttributeStateFastReportStartDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/device/state/report/fast/stop` | SWAGGER-ONLY | 停止速报 | deviceId* ; DeviceAttributeStateFastReportStopDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/dtu/hfmi/language` | SWAGGER-ONLY | 设置智慧屏设备hfmi文件语言 | - ; DtuHfmiLanguageSetDtio |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/dtu/passthrough/salve/single` | SWAGGER-ONLY | 透传数据至下位机 | dtuId, certificateDtuID ; DtuRemotePassthroughVijo |
| P2 | DEVICE_CONTROL_MUTATION | POST | `/remote/dtu/restart` | SWAGGER+PRODUCTION:HAR-OBSERVED | 重启采集器 | dtuId, certificateDtuID ; - |

## resource

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P2 | MANAGEMENT_MUTATION | GET | `/resource/download/media` | PRODUCTION-ONLY:HAR-OBSERVED | — | resid, save ; — ; empty, raw; Защищенная загрузка файла |
| P2 | MANAGEMENT_MUTATION | GET | `/resource/download/public/media` | PRODUCTION-ONLY:HAR-OBSERVED | — | resid ; — ; raw; Публичная загрузка файла |
| P2 | MANAGEMENT_MUTATION | POST | `/resource/upload/icon` | SWAGGER-ONLY | 上传公共资源图片 | category, deleteResid, downloadName, isInternal ; - |
| P2 | MANAGEMENT_MUTATION | POST | `/resource/upload/media` | SWAGGER-ONLY | 上传公共资源媒体 | category, deleteResid, downloadName, isInternal ; - |

## rest

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | POST | `/rest/api/list` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, isLoggable, page ; dict |

## sensitive

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/sensitive/country/details` | SWAGGER-ONLY | 查询敏感国家详情 | id* ; - |
| P1 | OPTIONAL_READ | POST | `/sensitive/country/reverse/gis` | SWAGGER-ONLY | 逆解析gis点为敏感国家 | - ; GisPointDtio |

## sim

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | ADMIN_READ | GET | `/sim/card/report/details` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取SIM卡报表详情 | id* ; - |
| P3 | ADMIN_READ | GET | `/sim/card/report/headers` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取SIM卡列表报表头 | - ; - |
| P2 | ADMIN_MUTATION | POST | `/sim/card/report/delete/batch` | SWAGGER-ONLY | 批量删除SIM卡报表 | - ; - |
| P2 | ADMIN_MUTATION | POST | `/sim/card/report/export` | SWAGGER+PRODUCTION:HAR-OBSERVED | 导出SIM卡列表报表 | - ; ExportSimCardListExcelDtio |
| P3 | ADMIN_READ | POST | `/sim/card/report/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询SIM卡报表 | - ; SimCardReportSearchDtio |

## simcard

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | POST | `/simcard/search` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, dataBalanceWarningCode, expiryWarningCode, isActived, page, status, supplierCode ; dict |

## station

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P0 | OPTIONAL_READ | GET | `/station/details` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查询电站详情 | stationId* ; - |
| P1 | OPTIONAL_READ | GET | `/station/energy/flow` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取电站简单能量流动 | stationId*, isManualRefresh ; - |
| P1 | OPTIONAL_READ | GET | `/station/report/export/records/details` | PRODUCTION-ONLY:HAR-OBSERVED | — | id ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/station/report/get/monthly/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list; Колонки экспортного отчета |
| P1 | OPTIONAL_READ | GET | `/station/report/get/station/list/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list; Колонки экспортного отчета |
| P1 | OPTIONAL_READ | GET | `/station/report/get/yearly/excel/header` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list; Колонки экспортного отчета |
| P1 | OPTIONAL_READ | GET | `/station/state/count` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; list |
| P2 | MANAGEMENT_MUTATION | POST | `/station/add` | SWAGGER-ONLY | 创建电站 | - ; StationAddDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/station/delete` | SWAGGER-ONLY | 删除电站 | stationId* ; - |
| P0 | OPTIONAL_READ | POST | `/station/list` | SWAGGER+PRODUCTION:HAR-OBSERVED | 搜索电站列表 | - ; StationListDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/station/pin` | SWAGGER-ONLY | 置顶电站 | - ; GeneralIdsDtio |
| P1 | OPTIONAL_READ | POST | `/station/report/export/records/list` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, fromTime, page, state, stationName, timeAsc, toTime, type ; dict |
| P1 | OPTIONAL_READ | POST | `/station/report/monthly` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; fieldNames, fromTime, id, remark, toTime ; dict |
| P2 | MANAGEMENT_MUTATION | POST | `/station/unpin` | SWAGGER-ONLY | 取消置顶电站 | - ; GeneralIdsDtio |
| P2 | MANAGEMENT_MUTATION | POST | `/station/update` | SWAGGER-ONLY | 编辑电站 | - ; StationUpdateDtio |

## stationOverView

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/generatedEnergy/monthly` | SWAGGER-ONLY | 获取电站月发电量 | stationId* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/generatedEnergy/total` | SWAGGER-ONLY | 获取电站总发电量 | stationId* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/generatedEnergy/yearly` | SWAGGER-ONLY | 获取电站年发电量 | stationId* ; YearlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/generationPower/daily` | SWAGGER-ONLY | 获取电站日发电量 | stationId* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/income/daily` | PRODUCTION-ONLY:HAR-OBSERVED | — | stationId ; time ; list |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/income/monthly` | PRODUCTION-ONLY:HAR-OBSERVED | — | stationId ; time ; list |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/income/total` | PRODUCTION-ONLY:HAR-OBSERVED | — | stationId ; time ; list |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/income/yearly` | PRODUCTION-ONLY:HAR-OBSERVED | — | stationId ; time ; list |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/category/daily` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目电站日汇总 | stationId*, summaryCategoryKey* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/category/monthly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目电站月度汇总 | stationId*, summaryCategoryKey* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/category/total` | SWAGGER-ONLY | 获取状态属性统计类目电站年总计 | stationId*, summaryCategoryKey* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/category/yearly` | SWAGGER+PRODUCTION:HAR-OBSERVED | 获取状态属性统计类目电站年度汇总 | stationId*, summaryCategoryKey* ; YearlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/daily` | SWAGGER-ONLY | 获取设备状态属性电站日度汇总 | stationId*, summaryPropertyKey* ; DailyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/monthly` | SWAGGER-ONLY | 获取设备状态属性电站月度汇总 | stationId*, summaryPropertyKey* ; MonthlyDtio |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/total` | SWAGGER-ONLY | 获取设备状态属性电站年总计 | stationId*, summaryPropertyKey* ; - |
| P1 | TELEMETRY_ANALYTICS_READ | POST | `/stationOverView/stateAttributeSummary/yearly` | SWAGGER-ONLY | 获取设备状态属性电站年度汇总 | stationId*, summaryPropertyKey* ; YearlyDtio |

## telephone

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | AUTH_SUPPORT | GET | `/telephone/area/code/all/country/code/values` | SWAGGER-ONLY | 获取所有国家电话区号码值 | — |

## user

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P1 | OPTIONAL_READ | GET | `/user/account/check` | SWAGGER-ONLY | 校验账户是否存在 | Query: account* |
| P1 | OPTIONAL_READ | GET | `/user/app/applyMode/` | SWAGGER-ONLY | 获取APP应用模式 | - |
| P1 | OPTIONAL_READ | GET | `/user/bindableSuperior` | SWAGGER-ONLY | 查询可绑定的上级用户 | uid* ; - |
| P1 | OPTIONAL_READ | GET | `/user/currency` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; dict |
| P1 | OPTIONAL_READ | GET | `/user/email/check` | SWAGGER-ONLY | 校验邮箱是否存在 | Query: email* |
| P1 | OPTIONAL_READ | GET | `/user/group/list` | SWAGGER-ONLY | 查询用户分组列表 | account, count*, groupName, page* ; - |
| P1 | OPTIONAL_READ | GET | `/user/group/user/list` | SWAGGER-ONLY | 分组查询用户列表 | account, count*, page*, userGroupId* ; - |
| P1 | OPTIONAL_READ | GET | `/user/owner` | SWAGGER-ONLY | 查询可绑定的站点业主用户 | account, cellphone, countryTelephoneCode, email, uid ; - |
| P1 | OPTIONAL_READ | GET | `/user/type` | SWAGGER-ONLY | 查询下级用户类型 | - ; - |
| P2 | ACCOUNT_MUTATION | POST | `/user/app/applyMode/update` | SWAGGER-ONLY | 更新APP应用模式 | AppApplyModeDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/create/account` | SWAGGER-ONLY | 创建下级用户账号 | - ; CreateUserDtio |
| P1 | OPTIONAL_READ | POST | `/user/currency/setting` | PRODUCTION-ONLY:HAR-OBSERVED | — | currencyCode ; — ; dict; POST без JSON-полей в теле в наблюдаемом вызове |
| P2 | ACCOUNT_MUTATION | POST | `/user/group/add/user` | SWAGGER-ONLY | 添加分组用户 | - ; UserGroupAddUserDtio |
| P1 | OPTIONAL_READ | POST | `/user/log/personal/search` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; count, fromTime, orderByTimeDesc, page, toTime ; dict |
| P2 | ACCOUNT_MUTATION | POST | `/user/logout/account` | SWAGGER-ONLY | 注销账户 | - |
| P1 | OPTIONAL_READ | POST | `/user/personal/superiors/direct` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; name, uid ; list |
| P2 | ACCOUNT_MUTATION | POST | `/user/register/cellphone` | SWAGGER-ONLY | 手机注册账户 | UserRegisterDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/register/email` | SWAGGER-ONLY | 邮箱注册账户 | UserRegisterDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/reset/password` | SWAGGER-ONLY | 找回密码 | UserRetrievePasswordDtio |
| P1 | OPTIONAL_READ | POST | `/user/search/stationUser` | SWAGGER-ONLY | 根据账号搜索电站用户 | - ; UniqueUserDtio |
| P1 | OPTIONAL_READ | POST | `/user/select/iotUserInfo` | SWAGGER+PRODUCTION:HAR-OBSERVED | 查看个人用户信息 | - |
| P1 | OPTIONAL_READ | POST | `/user/select/userThemeColors` | PRODUCTION-ONLY:HAR-OBSERVED | — | — ; — ; dict; POST без JSON-полей в теле в наблюдаемом вызове |
| P2 | ACCOUNT_MUTATION | POST | `/user/send/email/captcha` | SWAGGER-ONLY | 发送邮箱验证码 | SendCaptchaDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/send/sms/captcha` | SWAGGER-ONLY | 发送短信验证码 | SendCaptchaDtio |
| P1 | OPTIONAL_READ | POST | `/user/subordinate/list` | SWAGGER-ONLY | 查询下级用户列表 | - ; UserListDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/update/authPassword` | SWAGGER-ONLY | 更新密码 | UserUpdatePasswordDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/update/cellphoneVerify` | SWAGGER-ONLY | 更新用户手机验证 | UpdateTelephoneVerifyDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/update/iotUserCellphone` | SWAGGER-ONLY | 修改手机号 | UpdateTelephoneDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/update/iotUserEmail` | SWAGGER-ONLY | 更新用户邮箱 | UserUpdateByEmailDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/update/iotUserInfo` | SWAGGER+PRODUCTION:HAR-OBSERVED | 更新个人用户信息 | UserUpdateInfoDtio |
| P2 | ACCOUNT_MUTATION | POST | `/user/verify/account` | SWAGGER-ONLY | 验证账户名 | VerifyAccountDtio |

## userGroup

| Priority | Class | Method | Path | Source/evidence | Swagger summary | Contract fragment |
|---|---|---|---|---|---|---|
| P3 | ADMIN_READ | POST | `/userGroup/insert/userGroup` | SWAGGER-ONLY | 添加用户分组 | InsertUserGroupDtio |
| P3 | ADMIN_READ | POST | `/userGroup/query/list` | SWAGGER-ONLY | 用户分组列表 | UserGroupDtio |

## Classification cautions

- The class/priority columns are **Windows-project engineering classifications**, not vendor authorization labels.
- A POST can still be read-only (many search/history APIs use POST bodies), while some GETs may expose sensitive/admin data.
- `ACTIVE_DEVICE_READ` is separated from passive reads because configuration-read endpoints may cause the cloud/logger to issue a live device read even though they do not intentionally change a setting.
- Report/export endpoints can create server-side export jobs despite being informational in intent.
- Device/manufacturer/account capability gating must be respected even for documented routes.
- For any future control implementation, endpoint-level validation must be performed against the user's own device/account and known-safe values before enabling writes.

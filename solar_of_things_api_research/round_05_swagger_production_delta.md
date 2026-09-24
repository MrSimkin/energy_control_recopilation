# Round 05 Companion — Swagger vs Production Endpoint Delta

Date: 2026-09-24

Status: COMPLETE

Companion to `round_05_alternate_web_openapi_comparison.md`.

## Scope

This is a method+path comparison between:

- the preserved first-party-Swagger-derived contract in `lujian1324-spec/energy-app/API_REFERENCE.md`; and
- the canonical Round 02 production endpoint catalog, which combines the original HAR, login-permission additions, and later live/implemented routes.

It is a provenance matrix, **not** a statement that every documented endpoint is currently enabled for every account/device.

## Counts

| Corpus | Distinct method/path pairs |
|---|---:|
| Swagger-derived tables | 221 |
| Round 02 production corpus | 115 |
| Exact overlap | 62 |
| Swagger-only | 159 |
| Production-only | 53 |

The Swagger-derived Markdown footer says 227 endpoints, but its current endpoint tables reproducibly contain 221 distinct method/path pairs. No missing routes are invented to reconcile that discrepancy.

## Domain-level comparison

| Domain | Swagger | Production corpus | Exact overlap | Swagger-only | Production-only |
|---|---:|---:|---:|---:|---:|
| `admin` | 3 | 0 | 0 | 3 | 0 |
| `alarm` | 9 | 6 | 6 | 3 | 0 |
| `app` | 3 | 0 | 0 | 3 | 0 |
| `currency` | 1 | 1 | 1 | 0 | 0 |
| `dashboard` | 3 | 4 | 1 | 2 | 3 |
| `device` | 22 | 21 | 7 | 15 | 14 |
| `deviceApplyMode` | 2 | 0 | 0 | 2 | 0 |
| `deviceManufacturer` | 1 | 0 | 0 | 1 | 0 |
| `deviceOffset` | 4 | 1 | 0 | 4 | 1 |
| `deviceOverView` | 12 | 6 | 4 | 8 | 2 |
| `deviceSort` | 2 | 1 | 1 | 1 | 0 |
| `deviceState` | 10 | 5 | 5 | 5 | 0 |
| `dictionary` | 7 | 6 | 5 | 2 | 1 |
| `dtu` | 3 | 5 | 2 | 1 | 3 |
| `factoryOverView` | 4 | 0 | 0 | 4 | 0 |
| `gather` | 2 | 0 | 0 | 2 | 0 |
| `geo` | 2 | 2 | 2 | 0 | 0 |
| `getInfo` | 0 | 1 | 0 | 0 | 1 |
| `getRouters` | 0 | 1 | 0 | 0 | 1 |
| `graphic` | 2 | 0 | 0 | 2 | 0 |
| `instruction` | 8 | 0 | 0 | 8 | 0 |
| `integratorsOverView` | 2 | 0 | 0 | 2 | 0 |
| `login` | 7 | 2 | 2 | 5 | 0 |
| `near` | 17 | 0 | 0 | 17 | 0 |
| `owner` | 0 | 6 | 0 | 0 | 6 |
| `ownerOverView` | 13 | 5 | 5 | 8 | 0 |
| `peakValley` | 8 | 0 | 0 | 8 | 0 |
| `portal` | 0 | 1 | 0 | 0 | 1 |
| `remote` | 16 | 9 | 9 | 7 | 0 |
| `resource` | 2 | 2 | 0 | 2 | 2 |
| `rest` | 0 | 1 | 0 | 0 | 1 |
| `sensitive` | 2 | 0 | 0 | 2 | 0 |
| `sim` | 5 | 4 | 4 | 1 | 0 |
| `simcard` | 0 | 1 | 0 | 0 | 1 |
| `station` | 8 | 10 | 3 | 5 | 7 |
| `stationOverView` | 12 | 7 | 3 | 9 | 4 |
| `telephone` | 1 | 0 | 0 | 1 | 0 |
| `user` | 26 | 7 | 2 | 24 | 5 |
| `userGroup` | 2 | 0 | 0 | 2 | 0 |

## Swagger-only method/path pairs (159)

These are documented in the preserved Swagger-derived contract but do not appear as exact method/path pairs in the Round 02 production corpus.

| Method | Path | Swagger summary |
|---|---|---|
| POST | `/admin/region/coding/reverse` | 将gis点反转为管理区域 |
| POST | `/admin/region/coding/reverse/ip` | 将IP地址反转为管理区域 |
| POST | `/admin/region/coding/reverse/myip` | 将我的IP地址反转为管理区域 |
| POST | `/alarm/delete/alarm` | 删除告警 |
| POST | `/alarm/report/record/delete/batch` | 批量删除告警报表 |
| POST | `/alarm/update/isProcessed` | 忽略告警 |
| POST | `/app/scan/qrcode/login` | 二维码登录确认 |
| GET | `/app/version/check` | 根据appId检查App版本（已废弃） |
| GET | `/app/version/check/v2` | 根据包名检查APP版本 |
| POST | `/dashboard/summary/station/generatedEnergy/total` | 汇总每年的发电量 |
| POST | `/dashboard/summary/station/generatedEnergy/yearly` | 汇总当年每月的发电量 |
| POST | `/device/add/single` | 添加单个设备 |
| POST | `/device/add/single/addStationTogether` | 添加设备同时创建电站 |
| POST | `/device/delete` | 删除设备 |
| POST | `/device/dtu/replace` | 更换设备采集器 |
| POST | `/device/external/add/single` | 添加单个外挂设备 |
| POST | `/device/firmware/list` | 查询设备固件列表 |
| POST | `/device/firmware/list/fromManufacturer` | 根据设备id查询厂家固件列表 |
| POST | `/device/pin` | 置顶设备 |
| GET | `/device/query/by/dtuId` | 根据dtuId查设备 |
| GET | `/device/query/dtuids` | 查询设备采集器列表 |
| POST | `/device/unpin` | 取消置顶设备 |
| POST | `/device/update` | 编辑设备信息 |
| POST | `/device/upgrade/create` | 创建设备升级 |
| GET | `/device/upgrade/logs` | 获取设备升级日志 |
| GET | `/device/upgrade/script/file/info` | 获取设备升级脚本文件信息 |
| GET | `/deviceApplyMode/modes/external` | 获取设备外挂应用模式列表 |
| GET | `/deviceApplyMode/modes/main` | 获取设备主应用模式列表 |
| POST | `/deviceManufacturer/add` | 创建厂家 |
| GET | `/deviceOffset/details` | 查看设备偏移量详情 |
| POST | `/deviceOffset/list` | 查询设备偏移量列表 |
| POST | `/deviceOffset/set` | 设置设备偏移量 |
| GET | `/deviceOffset/totally` | 获取总偏移量 |
| POST | `/deviceOverView/generatedEnergy/monthly` | 获取设备当月每天发电量 |
| POST | `/deviceOverView/generatedEnergy/total` | 获取设备历年发电量 |
| POST | `/deviceOverView/generatedEnergy/yearly` | 获取设备当年每月发电量 |
| POST | `/deviceOverView/generationPower/daily` | 获取设备当日发电功率明细 |
| POST | `/deviceOverView/stateAttributeSummary/daily` | 获取设备状态属性日度汇总 |
| POST | `/deviceOverView/stateAttributeSummary/monthly` | 获取设备状态属性月度汇总 |
| POST | `/deviceOverView/stateAttributeSummary/total` | 获取设备状态属性年总计 |
| POST | `/deviceOverView/stateAttributeSummary/yearly` | 获取设备状态属性年度汇总 |
| GET | `/deviceSort/sorts/details` | 查看设备种类详情 |
| POST | `/deviceState/attribute/keys/history` | 获取设备指定属性历史数据 |
| POST | `/deviceState/attribute/record/list` | 获取设备状态明细数据 |
| POST | `/deviceState/attribute/record/list/v2` | 获取设备状态明细数据V2 |
| POST | `/deviceState/attribute/record/time/list` | 获取设备状态数据更新时间 |
| GET | `/deviceState/gatherAttributes` | 获取设备属性列表 |
| GET | `/dictionary/data/hjsa/version` | 获取HJSA脚本数据字典 |
| GET | `/dictionary/data/sensitive/country` | 获取敏感国家数据字典 |
| POST | `/dtu/select/dtu/withDtuID` | 根据DtuID查看采集器详情 |
| POST | `/factoryOverView/select/alertsHistorical` | 厂家设备告警排行 |
| POST | `/factoryOverView/select/deviceDistributionRanking` | 厂家设备分布排名 |
| POST | `/factoryOverView/select/deviceOnlineRate` | 厂家设备在线率 |
| POST | `/factoryOverView/select/manufacturerStatistics` | 厂家统计信息 |
| POST | `/gather/protocol/manufacturerDeviceUpgradeProtocol/bind` | 绑定厂家设备固件升级协议 |
| GET | `/gather/protocol/manufacturerDeviceUpgradeProtocol/overviews` | 获取厂家设备固件升级协议概述列表 |
| POST | `/graphic/validation/code/generate` | 生成滑块拼图验证码 |
| POST | `/graphic/validation/code/verify` | 校验滑块拼图验证码 |
| GET | `/instruction` | 获取自动化指令详情 |
| POST | `/instruction/add` | 添加自动化指令 |
| POST | `/instruction/delete` | 删除自动化指令 |
| POST | `/instruction/historyList` | 分页查询指令执行历史 |
| GET | `/instruction/historyListOfinstruction` | 分页查询指令执行历史（按指令ID） |
| GET | `/instruction/list` | 分页查询自动化指令 |
| POST | `/instruction/update` | 更新自动化指令 |
| POST | `/instruction/updateStatus` | 更新自动化指令状态 |
| POST | `/integratorsOverView/select/integratorsStatistics` | 获取集成商资源详情 |
| POST | `/integratorsOverView/select/powerStationRanking` | 获取电站满发时间排名 |
| POST | `/login/email` | 邮箱验证码登录 |
| POST | `/login/inviteCode` | 邀请码登录 |
| POST | `/login/logout` | 退出登录 |
| POST | `/login/passwordless/login` | 免密登录下级用户 |
| POST | `/login/sms` | 手机短信验证码登录 |
| POST | `/near/dtu/checkin` | 签到 |
| POST | `/near/dtu/detectGatherProtocol` | 侦测采集协议 |
| GET | `/near/dtu/device/attribute/groups` | 获取采集属性分组列表 |
| GET | `/near/dtu/gatherProtocol` | 获取采集协议 |
| POST | `/near/dtu/gen/device/config/pre/write` | 生成预设置设备配置项指令 |
| POST | `/near/dtu/gen/device/config/read` | 生成读取设备配置项指令 |
| POST | `/near/dtu/gen/device/config/write` | 生成设置设备配置项指令 |
| POST | `/near/dtu/gen/device/configs/read` | 生成批量读取设备配置项指令 |
| GET | `/near/dtu/hfmi/detail` | 获取智慧屏hfmi文件详情 |
| GET | `/near/dtu/hfmi/overviews` | 获取智慧屏hfmi文件概览列表 |
| GET | `/near/dtu/hisScript` | 获取dtu的His脚本 |
| POST | `/near/dtu/parse/device/config/read` | 解析设备配置项读取响应 |
| POST | `/near/dtu/parse/device/config/write` | 解析设备配置项设置响应 |
| POST | `/near/dtu/parse/device/configs/read` | 解析设备配置项批量读取响应 |
| POST | `/near/dtu/parse/device/energy/flow` | 解析设备能量流动数据 |
| POST | `/near/dtu/parse/device/event` | 解析设备事件数据 |
| POST | `/near/dtu/parse/device/state` | 解析设备状态数据 |
| POST | `/ownerOverView/station/generatedEnergy/monthly` | 获取电站月发电量 |
| POST | `/ownerOverView/station/generatedEnergy/total` | 获取电站总发电量 |
| POST | `/ownerOverView/station/generatedEnergy/yearly` | 获取电站年发电量 |
| POST | `/ownerOverView/station/generationPower/daily` | 获取电站日发电量 |
| POST | `/ownerOverView/station/stateAttributeSummary/daily` | 获取设备状态属性业主日度汇总 |
| POST | `/ownerOverView/station/stateAttributeSummary/monthly` | 获取设备状态属性业主月度汇总 |
| POST | `/ownerOverView/station/stateAttributeSummary/total` | 获取设备状态属性业主年总计 |
| POST | `/ownerOverView/station/stateAttributeSummary/yearly` | 获取设备状态属性业主年度汇总 |
| GET | `/peakValley/device/attribute/group` | 获取设备削峰填谷采集属性分组 |
| POST | `/peakValley/device/customized/set` | 设置自定义削峰填谷 |
| POST | `/peakValley/device/enable` | 使能设备削峰填谷 |
| GET | `/peakValley/device/general/get` | 获取设备常规削峰填谷 |
| POST | `/peakValley/device/general/set` | 设置设备常规削峰填谷 |
| GET | `/peakValley/device/get` | 获取设备削峰填谷 |
| GET | `/peakValley/types/all` | 获取所有削峰填谷类型 |
| GET | `/peakValley/types/device` | 获取设备支持的削峰填谷类型 |
| GET | `/remote/device/energy/flow` | 获取设备能量流动 |
| POST | `/remote/device/passthrough` | 透传数据 |
| GET | `/remote/device/state/latest` | 获取设备最近状态数据 |
| POST | `/remote/device/state/report/fast/start` | 启动速报（实时数据快速上报） |
| POST | `/remote/device/state/report/fast/stop` | 停止速报 |
| POST | `/remote/dtu/hfmi/language` | 设置智慧屏设备hfmi文件语言 |
| POST | `/remote/dtu/passthrough/salve/single` | 透传数据至下位机 |
| POST | `/resource/upload/icon` | 上传公共资源图片 |
| POST | `/resource/upload/media` | 上传公共资源媒体 |
| GET | `/sensitive/country/details` | 查询敏感国家详情 |
| POST | `/sensitive/country/reverse/gis` | 逆解析gis点为敏感国家 |
| POST | `/sim/card/report/delete/batch` | 批量删除SIM卡报表 |
| POST | `/station/add` | 创建电站 |
| POST | `/station/delete` | 删除电站 |
| POST | `/station/pin` | 置顶电站 |
| POST | `/station/unpin` | 取消置顶电站 |
| POST | `/station/update` | 编辑电站 |
| POST | `/stationOverView/generatedEnergy/monthly` | 获取电站月发电量 |
| POST | `/stationOverView/generatedEnergy/total` | 获取电站总发电量 |
| POST | `/stationOverView/generatedEnergy/yearly` | 获取电站年发电量 |
| POST | `/stationOverView/generationPower/daily` | 获取电站日发电量 |
| POST | `/stationOverView/stateAttributeSummary/category/total` | 获取状态属性统计类目电站年总计 |
| POST | `/stationOverView/stateAttributeSummary/daily` | 获取设备状态属性电站日度汇总 |
| POST | `/stationOverView/stateAttributeSummary/monthly` | 获取设备状态属性电站月度汇总 |
| POST | `/stationOverView/stateAttributeSummary/total` | 获取设备状态属性电站年总计 |
| POST | `/stationOverView/stateAttributeSummary/yearly` | 获取设备状态属性电站年度汇总 |
| GET | `/telephone/area/code/all/country/code/values` | 获取所有国家电话区号码值 |
| GET | `/user/account/check` | 校验账户是否存在 |
| GET | `/user/app/applyMode/` | 获取APP应用模式 |
| POST | `/user/app/applyMode/update` | 更新APP应用模式 |
| GET | `/user/bindableSuperior` | 查询可绑定的上级用户 |
| POST | `/user/create/account` | 创建下级用户账号 |
| GET | `/user/email/check` | 校验邮箱是否存在 |
| POST | `/user/group/add/user` | 添加分组用户 |
| GET | `/user/group/list` | 查询用户分组列表 |
| GET | `/user/group/user/list` | 分组查询用户列表 |
| POST | `/user/logout/account` | 注销账户 |
| GET | `/user/owner` | 查询可绑定的站点业主用户 |
| POST | `/user/register/cellphone` | 手机注册账户 |
| POST | `/user/register/email` | 邮箱注册账户 |
| POST | `/user/reset/password` | 找回密码 |
| POST | `/user/search/stationUser` | 根据账号搜索电站用户 |
| POST | `/user/send/email/captcha` | 发送邮箱验证码 |
| POST | `/user/send/sms/captcha` | 发送短信验证码 |
| POST | `/user/subordinate/list` | 查询下级用户列表 |
| GET | `/user/type` | 查询下级用户类型 |
| POST | `/user/update/authPassword` | 更新密码 |
| POST | `/user/update/cellphoneVerify` | 更新用户手机验证 |
| POST | `/user/update/iotUserCellphone` | 修改手机号 |
| POST | `/user/update/iotUserEmail` | 更新用户邮箱 |
| POST | `/user/verify/account` | 验证账户名 |
| POST | `/userGroup/insert/userGroup` | 添加用户分组 |
| POST | `/userGroup/query/list` | 用户分组列表 |

## Production-only method/path pairs (53)

These appear in HAR/current/live production evidence but are absent from the preserved Swagger tables.

| Evidence | Method | Path |
|---|---|---|
| HAR-OBSERVED | POST | `/dashboard/summary/commons` |
| HAR-OBSERVED | POST | `/dashboard/summary/station/dailyGenerationTimeRank` |
| HAR-OBSERVED | POST | `/dashboard/summary/station/distribution/location` |
| HAR-OBSERVED | GET | `/device/linkage/rule/detail` |
| HAR-OBSERVED | POST | `/device/report/daily` |
| HAR-OBSERVED | GET | `/device/report/export/records/details` |
| HAR-OBSERVED | POST | `/device/report/export/records/list` |
| HAR-OBSERVED | GET | `/device/report/get/daily/excel/header` |
| HAR-OBSERVED | GET | `/device/report/get/list/excel/header` |
| HAR-OBSERVED | GET | `/device/report/get/monthly/excel/header` |
| HAR-OBSERVED | GET | `/device/report/get/yearly/excel/header` |
| HAR-OBSERVED | POST | `/device/report/monthly` |
| HAR-OBSERVED | POST | `/device/report/yearly` |
| HAR-OBSERVED | GET | `/device/state/count` |
| HAR-OBSERVED | GET | `/device/upgrade/device/names` |
| HAR-OBSERVED | GET | `/device/upgrade/firmware/names` |
| HAR-OBSERVED | GET | `/device/upgrade/protocol/names` |
| HAR-OBSERVED | POST | `/deviceOffset/totally` |
| HAR-OBSERVED | POST | `/deviceOverView/generatedEnergy/daily` |
| LATER-LIVE/IMPLEMENTED | POST | `/deviceOverView/pvInverterPowerClass/daily/detail` |
| LOGIN-PERMISSION | GET | `/dictionary/data/dtu` |
| HAR-OBSERVED | GET | `/dtu/models` |
| HAR-OBSERVED | POST | `/dtu/replace/list` |
| HAR-OBSERVED | POST | `/dtu/select/dtu` |
| HAR-OBSERVED | GET | `/getInfo` |
| HAR-OBSERVED | GET | `/getRouters` |
| HAR-OBSERVED | GET | `/owner/report/export/records/details` |
| HAR-OBSERVED | POST | `/owner/report/export/records/list` |
| HAR-OBSERVED | GET | `/owner/report/get/monthly/excel/header` |
| HAR-OBSERVED | GET | `/owner/report/get/yearly/excel/header` |
| HAR-OBSERVED | POST | `/owner/report/monthly` |
| HAR-OBSERVED | POST | `/owner/report/yearly` |
| HAR-OBSERVED | GET | `/portal/info` |
| HAR-OBSERVED | GET | `/resource/download/media` |
| HAR-OBSERVED | GET | `/resource/download/public/media` |
| HAR-OBSERVED | POST | `/rest/api/list` |
| HAR-OBSERVED | POST | `/simcard/search` |
| HAR-OBSERVED | GET | `/station/report/export/records/details` |
| HAR-OBSERVED | POST | `/station/report/export/records/list` |
| HAR-OBSERVED | GET | `/station/report/get/monthly/excel/header` |
| HAR-OBSERVED | GET | `/station/report/get/station/list/excel/header` |
| HAR-OBSERVED | GET | `/station/report/get/yearly/excel/header` |
| HAR-OBSERVED | POST | `/station/report/monthly` |
| HAR-OBSERVED | GET | `/station/state/count` |
| HAR-OBSERVED | POST | `/stationOverView/income/daily` |
| HAR-OBSERVED | POST | `/stationOverView/income/monthly` |
| HAR-OBSERVED | POST | `/stationOverView/income/total` |
| HAR-OBSERVED | POST | `/stationOverView/income/yearly` |
| HAR-OBSERVED | GET | `/user/currency` |
| HAR-OBSERVED | POST | `/user/currency/setting` |
| HAR-OBSERVED | POST | `/user/log/personal/search` |
| HAR-OBSERVED | POST | `/user/personal/superiors/direct` |
| HAR-OBSERVED | POST | `/user/select/userThemeColors` |

## Interpretation rules

- Swagger-only does not mean dead; it means “documented but not present in the captured production corpus”. Some are later live-validated.
- Production-only does not mean undocumented in every possible vendor source; it means absent from this preserved Swagger transcription.
- Different path versions may describe related functionality and are intentionally kept distinct.
- Mutating routes are cataloged only. No mutating endpoint was called to build this matrix.
- Role, manufacturer and device capability gating can make a syntactically valid route unavailable to a given account/device.

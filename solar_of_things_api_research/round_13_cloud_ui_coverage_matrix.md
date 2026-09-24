# Round 13 Companion — Cloud UI Coverage Matrix

Date: 2026-09-24

Status: COMPLETE

Companion to `round_13_cloud_ui_coverage_audit.md`.

| UI / information surface | Known cloud source | Coverage | Remaining issue |
|---|---|---|---|
| Classic home aggregated statistics | `dashboard/summary/commons`, device/station lists | COVERED | exact target-account population |
| Advanced home project/device counts | `dashboard/summary/commons` | COVERED | none material |
| Today generation | dashboard/station/device generation aggregates | COVERED | select authoritative target-device source |
| Month generation | station/device category/monthly aggregates | COVERED | target validation |
| Year generation | category/yearly / generatedEnergy/yearly | COVERED | target validation |
| Cumulative generation | generatedEnergy/total, device/station totals | COVERED | target validation |
| Monthly generation chart | `dashboard/summary/station/generatedEnergy/monthly` or station aggregate | COVERED | choose explicit scope |
| Project/station list | `station/list` | COVERED | none |
| Project details | `station/details` | COVERED | none |
| Normal/warning/offline counts | dashboard `stationStateSummary`, station/device state-count APIs | COVERED | enum/display labels |
| Device list | `device/list` | COVERED | none |
| Device details | `device/details` | COVERED | none |
| Current device state / Data Board | `deviceState/simple/state/latest/v1` | COVERED | target `dataSource`, model fields |
| Energy-flow diagram | device `energy/flow/v1`, station `energy/flow` | COVERED | some devices return 70132 |
| PV/Grid/Battery/Load drill-down | latest state + gather attributes + energy flow | COVERED | dynamic device-specific fields |
| User-selectable analysis parameters | `gatherAttributes/v1` | COVERED | target field catalog |
| Realtime/history curves | `attribute/keys/history/v1` | COVERED | target cadence/aliases |
| Broad historical state | simple record list / row fallback | COVERED | target retention |
| Current alarm list | `alarm/getLatestAlarm`, `alarm/query/list` | COVERED | target alarm semantics |
| Device operating/working status | dynamic state attributes + device state metadata | COVERED | device-specific enum |
| Station income/value | `stationOverView/income/*` | API-MAPPED | formula/meaning not validated |
| Environmental savings | dashboard/device fields: carbon, CO2, SO2, NOx | API-MAPPED | exact units/factors |
| Station ranking | `dashboard/.../dailyGenerationTimeRank` | API-MAPPED / OPTIONAL | single-site value negligible |
| Station map/distribution | `dashboard/.../distribution/location` | API-MAPPED / OPTIONAL | multi-site only |
| Reports/exports | device/station/alarm report endpoints | MAPPED / NOT CORE | creates server report artifacts |
| Add/edit/delete device | CRUD APIs | OUT OF SCOPE | mutation |
| Peak/valley settings | peakValley APIs | OUT OF SCOPE | control/config |
| Wi-Fi provisioning | local/BLE path | OUT OF SCOPE | explicitly excluded |
| Proximal Monitoring | BLE/local | OUT OF SCOPE | explicitly excluded |
| Device diagnosis/network changes | diagnostic/local/control | OUT OF SCOPE | explicitly excluded |
| Account/profile/security changes | user mutation APIs | OUT OF SCOPE | not dashboard data |

## Material gap status

### Platform-discovery gaps

**None material remain for the dashboard goal.**

### Installation-specific validation gaps

The final read-only validation should resolve:

1. user's station/device identity;
2. model/manufacturer/gather protocol;
3. correct `dataSource`;
4. actual field catalog and aliases;
5. units/scaling/sign conventions;
6. historical cadence/retention for that device;
7. which aggregate properties are real vs placeholders;
8. exact correspondence of API values to official UI;
9. cloud-source lag and late-arrival behavior;
10. state/alarm enum labels.

## Optional API data that does not justify another broad round

- environmental-impact figures;
- monetary income;
- station rankings;
- geographic distribution;
- portfolio/owner-wide summaries;
- report/export metadata.

Investigate any of these further only if the eventual dashboard specification explicitly needs them.

## Final research decision

Proceed to **controlled read-only validation against the user's own account/device**.

Create another research round only if that validation exposes one specific dashboard-relevant cloud metric that remains unmapped.

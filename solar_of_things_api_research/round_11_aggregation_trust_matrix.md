# Round 11 Companion — Aggregate Trust Matrix

Date: 2026-09-24

Status: COMPLETE

Companion to `round_11_server_side_statistics_aggregations_reports.md`.

## Source trust matrix

| Statistic | Preferred cloud source | Current trust | Known fallback | Main caveat |
|---|---|---|---|---|
| PV instantaneous power | raw timestamped telemetry | HIGH | power-class daily detail | Use actual timestamps/units |
| PV daily energy | station/device category-monthly `pvGeneratedEnergy` with real flag | HIGH | device daily counter; raw integration | Validate target device and ignore placeholders |
| PV monthly energy | category-yearly `pvGeneratedEnergy` | HIGH | sum validated daily buckets; raw integration | Keep bucket/source granularity explicit |
| PV lifetime/annual totals | `generatedEnergy/total` / generated-energy series | HIGH-MEDIUM | trustworthy lifetime device counter | Validate equality on target account |
| Load consumption | device daily counter `loadDayElectricityConsumption` on known real installation | MEDIUM-HIGH for that family | validated aggregate; load-power integration | `consumeElectricityQuantity` was placeholder in one corpus |
| Grid import | daily counter `dayPurchaseElectricityConsumption` on known real installation | MEDIUM-HIGH for that family | validated aggregate; signed grid-power integration | `buyElectricityQuantity` was placeholder in one corpus |
| Grid export | `sellElectricityQuantity` candidate | MEDIUM-LOW until target validation | export counter; signed grid-power integration | Public cross-validation insufficient |
| Battery charge energy | `chargeElectricityQuantity` candidate | MEDIUM-LOW until target validation | direct battery-power integration | Availability/quality device-specific |
| Battery discharge energy | `dischargeElectricityQuantity` candidate | MEDIUM-LOW until target validation | direct battery-power integration | Availability/quality device-specific |
| Income/revenue | station `income/*` | LOW-MEDIUM | calculate locally from validated tariff/energy | Exact vendor formula not established |
| Device daily/total produced quantity | device snapshot metadata | MEDIUM | station aggregate | Useful cross-check, not universal primary source |
| Custom sub-day energy | integrate raw power using actual timestamps | MEDIUM-HIGH when data complete | none | Gaps can bias integration |

## Aggregation-level matrix

| Scope | Endpoint family | Best use |
|---|---|---|
| Device | `/deviceOverView/*` | one inverter/device |
| Station | `/stationOverView/*` | one site/installation; preferred top-level for household dashboard |
| Owner | `/ownerOverView/station/*` | aggregate multiple stations owned by account |
| Dashboard | `/dashboard/summary/*` | portfolio/headline widgets, rankings, multi-station summary |

## Bucket semantics

| Request family | Request time | Returned bucket interpretation |
|---|---|---|
| category/daily | YYYY-MM-DD | intra-day power/statistical points |
| category/monthly | YYYY-MM | daily energy/statistical buckets |
| category/yearly | YYYY | monthly energy/statistical buckets |
| category/total | none / long-total | long-term/year-total representation depending response |
| generatedEnergy/monthly | YYYY-MM | daily generated-energy series for month |
| generatedEnergy/yearly | YYYY | monthly generated-energy series for year |
| generatedEnergy/total | none | long-term/year-labelled generated-energy totals |

## Quality flags

| API condition | Interpretation | Dashboard behavior |
|---|---|---|
| `isRealValue=true` | explicitly real point | eligible as primary metric after semantic validation |
| `isRealValue=false` | backend placeholder | omit / show unavailable; never display as measured zero |
| `isRealValue=null` | unlabelled | preserve and cross-check before promoting to authoritative |
| `hasRealTimePoints=true` | summary contains real points | preserve as category-level quality metadata |
| Missing property | unsupported/not returned | use validated fallback, not zero |

## Real cross-checks

| Comparison | Result | Interpretation |
|---|---|---|
| October 2025 raw PV integration vs server monthly aggregate | 200.58 vs 201.06 kWh | strong agreement; server PV aggregate useful as canonical total |
| One-day cumulative `totalPowerGeneration` delta vs station summary/device snapshot | 6.131 vs 5.581 / 5.581 kWh | cumulative-counter subtraction can be misleading |
| Station `consumeElectricityQuantity` | placeholder in one real corpus | not universally trustworthy |
| Station `buyElectricityQuantity` | placeholder in same corpus | not universally trustworthy |

## Report/export decision

| Surface | Role | Use in Windows dashboard |
|---|---|---|
| JSON history endpoints | direct structured data | CORE |
| JSON aggregate endpoints | direct server-calculated statistics | CORE / validated per metric |
| device report daily/monthly/yearly | creates report job/artifact | OPTIONAL, not normal ingestion |
| station report monthly/yearly | creates report job/artifact | OPTIONAL |
| export-record list/details | retrieve status/details of report jobs | OPTIONAL |
| Excel-header endpoints | describe export columns | DISCOVERY/AUDIT only |

## Recommended metric selection algorithm

For each metric:

1. identify the exact semantic quantity desired;
2. query candidate server aggregate;
3. reject `isRealValue=false`;
4. compare with a second independent source during initial validation;
5. assign a source rule per device/protocol;
6. store the chosen source and discrepancy metadata;
7. fall back to device cumulative counter or raw integration only when needed.

## Critical rule

**Do not trust or distrust an entire summary endpoint as one unit.**

Trust decisions must be:

`scope + category + property + device/protocol + quality flag`.

One response can contain a trustworthy PV-generation property and placeholder consumption/grid properties at the same time.

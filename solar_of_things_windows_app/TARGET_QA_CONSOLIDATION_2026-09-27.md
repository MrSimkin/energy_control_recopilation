# TARGET QA CONSOLIDATION — 2026-09-27

Status: CANONICAL TARGET-PC CHECKPOINT

This document consolidates the first high-coverage end-to-end QA of:
- current-state/Debug evidence;
- family XLSX;
- matching family PDF;
- subsequent corrective build lineage.

It exists so a new chat can recover the real target evidence without reconstructing it from conversation history.

---

## 1. Reviewed target artifacts

Artifacts supplied from the target Windows machine:

1. `SolarEnergy_20260920_20260926.xlsx`
2. `SolarEnergy_20260920_20260926.pdf`
3. `investigation-bundle-20260927-004439.zip`

The PDF was generated for:
- period: 2026-09-20 through 2026-09-26;
- aggregation: Day;
- generated: 2026-09-27 00:47 local.

The Debug bundle reports:
- database schema: 8;
- database size: ~1349.6 MB;
- model: HPVINV02;
- gather protocol: MH2083139;
- dataSource: 1;
- LatestState: SUPPORTED;
- EnergyFlow: SUPPORTED;
- History: SUPPORTED;
- historical raw rows: 3,797,724;
- normalized rows: 478,720;
- behavior rows: 43,520;
- raw API captures: 199.

---

## 2. Family report — accepted quantitative checkpoint

### 2.1 Page 1 household-source answers

For 2026-09-20 — 2026-09-26:

- Enel / Grid → House: **24.27 kWh**
- Solar → House: **40.88 kWh**
- Battery → House: **44.56 kWh**
- problem-free observable nights: **6 of 6**
- nights where battery ran short under current event rule: **0**
- detected reserve+grid episodes: **0**
- nights with insufficient evidence: **0**
- source-attribution coverage of observed household energy: **92.0%**
- observed household energy left unattributed: **9.54 kWh**

These source-attribution values are intentionally distinct from total PV/grid/battery movements.

### 2.2 Page 2 total energy movements

- total PV generated: **77.32 kWh**
- total household consumption: **119.24 kWh**
- total grid import: **39.65 kWh**
- total battery discharged: **50.38 kWh**
- total battery charged: **39.62 kWh**
- metric coverage: **99.7%**
- source attribution: **92.0%**
- unattributed household energy: **9.54 kWh**

### 2.3 Page 3 robust patterns

- highest typical household consumption window:
  **12:00–15:00 · 1.78 kW · 7 valid days / 7 with data**
- highest typical PV-production window:
  **12:00–15:00 · 1.30 kW · 7 valid days / 7 with data**
- highest typical grid-use window:
  **23:00–02:00 · 0.45 kW · 7 valid days / 7 with data**
- highest daily PV production:
  **2026-09-20 · 19.27 kWh**
- highest daily household consumption:
  **2026-09-25 · 18.67 kWh**
- highest daily grid use:
  **2026-09-25 · 12.98 kWh**
- no reserve+grid episode met the current evidence threshold.

### 2.4 Page 4 quality

- PV coverage: **99.7%**
- household coverage: **99.7%**
- grid coverage: **99.7%**
- battery coverage: **99.7%**
- attribution coverage: **92.0%**
- household energy left unattributed: **9.54 kWh**
- observable nights: **6/6**

The report correctly preserves:
- missing != zero;
- total battery discharge != Battery→House;
- total PV generation != Solar→House;
- total grid import != Enel→House;
- unresolved balance residuals are diagnostic rather than silently redistributed.

---

## 3. Visual/product QA result

Confirmed good:
- all three required charts are present and legible;
- chart aggregation matches the selected Day grouping;
- the stacked household-source chart preserves `Sin atribuir`;
- estimated stored battery energy is shown in kWh rather than SOC %;
- Page 2 uses a family-card hierarchy;
- Page 3 remains family-facing;
- Detail/Quality/Glossary contain the intended audit material;
- Excel and PDF communicate the same semantic hierarchy.

Defects found in this artifact set:
1. Excel `Patrones` event-summary merged rows can overlap without explicit heights.
2. PDF used `Tiempo total observado` for reserve+grid episode duration, which is semantically wrong and especially misleading when episode duration is zero.

Both defects were corrected in the consolidated corrective build line described below.

No re-export is required solely to rediscover those two defects.

---

## 4. Structured EnergyFlow target evidence

The target EnergyFlow endpoint succeeds and exposes structured flow.

A first reviewed snapshot showed approximately:
- PV: 0.000 kW
- Grid: 0.567 kW
- House/load: 0.446 kW
- battery voltage: 51.4 V
- BMS charging current: 1.8 A
- BMS discharge current: 0 A
- SOC: 20%
- mode: Mains Mode
- working mode: SBU
- charging priority: OSO
- PV feeding priority: LBU

Derived battery power:
- 51.4 × (0 − 1.8) ≈ **−92.5 W**
- negative means charging under the project's battery-power convention.

Grid exceeded house load by roughly 121 W while PV was zero.

A later Build-308 target bundle repeated the same pattern:
- PV: 0.000 kW
- Grid: 1.049 kW
- House/load: 0.944 kW
- battery voltage: 51.5 V
- BMS charging current: 1.8 A
- derived battery power: **−92.7 W**
- Grid − House: ~105 W
- mode: Mains Mode
- SBU / OSO / LBU.

Conclusion:
- this is repeated physical evidence consistent with **small utility-supported battery charging/maintenance plus conversion losses**;
- it is **not yet promoted to billing-grade Grid→Battery attribution**;
- do not reinterpret OSO solely from these frames.

---

## 5. Historical candidate detector result

Build-308 Debug introduced a deliberately strict diagnostic detector:

Candidate conditions:
- PV <= 50 W;
- grid import >= 100 W;
- derived battery charging >= 50 W;
- grid import exceeds household load by >= 25 W.

Across the full available normalized corpus, the returned bundle contained:
- candidate frames: **4,802**
- gap-aware candidate duration: **399.076 h**
- integrated derived battery charge over contiguous candidates:
  **375.011 kWh**
- integrated Grid-minus-House surplus over contiguous candidates:
  **362.388 kWh**

Interpretation:
- this makes the phenomenon materially repeated, not a one-frame curiosity;
- values remain diagnostic because battery power is derived and inverter losses/timing can matter;
- do not expose these totals as billing facts yet.

---

## 6. Source attribution — full available history checkpoint

The same Debug bundle reported:

Range:
- 2026-04-20T20:55:36.186Z
- through 2026-09-27T02:57:33.143Z

Rule:
- `hpvinv02.source-attribution.v1`

Totals:
- Solar → House: **583.022019 kWh**
- Battery → House: **1169.595779 kWh**
- Grid/Utility → House: **268.315679 kWh**
- household energy left unattributed: **164.898432 kWh**
- observed household energy: **2185.831909 kWh**
- attribution coverage of observed energy: **92.456%**
- observed-time coverage: **94.991%**
- explicit mode snapshots: **23**
- detected historical configuration changes used by this context: **1**

These totals are an audit checkpoint, not a claim that all historical ambiguity has disappeared.

---

## 7. Current configuration / state checkpoint

Latest saved state in the returned bundle confirmed:
- `workingMode = SBU`
- `chargingPriorityOrder = OSO`
- `pvEnergyFeedingPriority = LBU`
- `mode = Mains Mode`
- `bmsReturnsToMainsModeSOC = 20`
- `bmsReturnsToBatteryModeSOC = 50`
- `bmsLowPowerSOC = 10`
- `returnToMainsModeVoltage = 51`
- `returnToBatteryModeVoltage = 53`
- `acChargingSwitch = Open`
- `solarChargingSwitch = Close`
- `chargingMainSwitch = Open`
- `powerSupplyFromPVToLoadInACState = No`
- `outputModel = SIG`.

Do not collapse:
- SBU = output-source priority;
- OSO = charging-source priority;
- LBU = PV allocation priority;
- Mains Mode = current operating state.

---

## 8. Debug config-read lifecycle

### Build 307
The first consolidated implementation commit:
- `3d44a2f95c52978d7e61cca77b5e9cbe69e074f0`
failed compilation.

This failed build is historical and must not be referenced as a usable artifact.

### Build 308 — green
Fix:
- `298844d1f200d41dd53b735ceaac9fc4ae28b3eb`
- Windows Build 308 / run `36291695463`
- artifact `10922233048`

This established:
- empty-object POST body fixes the previous Spring `HttpMessageNotReadableException`;
- config cache call succeeds;
- cache data was empty at that moment;
- direct batch read starts successfully;
- first details response returned:
  - `isFinished=false`;
  - targetConfig structurally present;
  - config values still null;
  - configAttributeStates empty.

Important implementation bug found:
- old completion helper did not honor explicit `isFinished=false`.

### Build 309 — green current diagnostic build
Fix:
- `bd14dc7d198fe3e62ed2f0b0d79ac4505f79cd36`
- Windows Build 309 / run `36292792063`
- artifact `10922976498`

Direct config batch probe now:
- honors `isFinished=false`;
- polls once per second;
- waits up to 60 seconds;
- preserves every details response;
- reports WARN on timeout rather than false SUCCESS;
- captures config cache after the batch lifecycle.

The next target ZIP should answer whether completion populates:
- targetConfig values;
- configAttributeStates;
- post-read config cache.

---

## 9. Home Live behavior

Target user validation established:
- Windows Home cards do read current Solar of Things state;
- official Solar of Things mobile/source frames themselves advance roughly every five minutes.

Implemented behavior:
- check current cloud state immediately on entering Home;
- check every 60 seconds while Home remains visible;
- stop polling after leaving Home;
- display actual source-frame time;
- never imply a new physical measurement merely because a new HTTP check occurred.

Build 308+ also adds a subtle synchronized progress line at the bottom of each of the four Home cards:
- fills over the 60-second client-check cycle;
- graphical only;
- no numerical countdown;
- resets after the next current-state check;
- indicates **next client check**, not source-frame age.

---

## 10. Current next action

Do **not** re-run the XLSX/PDF simply because of the config-batch lifecycle fix.

Next manual target action is intentionally narrow:

1. use **Build 309** artifact `10922976498`;
2. copy the existing portable `Data\` folder as usual;
3. verify the four subtle Home 60-second progress lines;
4. run **Ayuda técnica → Abrir diagnóstico técnico → Ejecutar diagnóstico completo + paquete**;
5. return only the new investigation ZIP.

That ZIP is the next evidence gate.

After reviewing it, decide whether:
- config-batch reads have finally exposed concrete configuration values;
- the possible utility-supported battery-maintenance behavior can be refined;
- any source-attribution rule needs revision.

Do not request Navicat queries unless the new Debug bundle still lacks evidence that cannot reasonably be captured in-app.

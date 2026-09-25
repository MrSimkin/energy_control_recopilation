# Installation Behavior Contract — SPRO-6200 / LC230-512 / Midea 120 L

Status: **CANONICAL INSTALLATION-SPECIFIC DEVELOPMENT REFERENCE**

Date integrated: 2026-09-25

## Source

Original user-supplied manual:

- filename: `Manual_Familiar_SPRO_6200_LC230_Midea_v2_ES.docx`
- edition: v2.0
- manual date: 2026-08-14
- original file SHA-256: `f519a39be14950258ce51d3cbb3a7e69cbc6b23769b2ae9e47c77ca71f5a3bde`
- original size: 546,109 bytes
- language: Spanish
- source purpose: family-specific operating manual for this exact household installation.

User confirmation on 2026-09-25:

> the recommended inverter changes documented by this manual are currently applied.

This confirmation supersedes the manual's older “current observed” pre-change values when they conflict with the recommended final state.

The original DOCX is the source authority. This Markdown file is the development-facing extraction of facts that materially affect the Windows application.

## Evidence / authority rule

Use this order when interpreting the installation:

1. live read-only Solar of Things / SiSeLi evidence from the commissioned target device;
2. physical equipment plate limits and the family manual's installation-specific confirmed facts;
3. user-confirmed current configuration state;
4. compatible-platform documentation quoted by the family manual;
5. inference.

Do not let a generic inverter-family assumption override contradictory target-device evidence.

The family manual itself notes that the exact inverter firmware version and exact CT/meter wiring are still unconfirmed.

## Confirmed installation

- inverter: SPRO-6200, nominal 6.2 kW;
- PV array: 8 × 585 W bifacial TOPCon = 4.68 kWp nominal;
- battery: SPRO/Techfine LC230-512;
- chemistry: LiFePO4;
- battery nominal voltage: 51.2 V DC;
- battery useful capacity indicated: 11.776 kWh;
- battery plate continuous current limit: 200 A charge / 200 A discharge, while inverter/cabling may impose lower limits;
- whole house is on the backed-up output;
- mandatory zero-export installation;
- physical zero-export hardware exists;
- Midea MWH120-15EFG(CL)W water heater, 120 L, 1.5 kW.

## Canonical inverter operating baseline

The application is read-only. These values are an expected-state contract for interpretation and drift detection, **not** permission for the application to write inverter settings.

| Program | Meaning | Baseline | Development meaning |
| --- | --- | --- | --- |
| P01 | source priority for the house | SBU | solar → battery → grid |
| P05 | battery protocol | PYL | BMS-managed battery; do not infer SOC from voltage alone |
| P16 | charging source priority | OSO | intended solar-only battery charging |
| P25 | fault logging | FEN | expected enabled after recommended changes |
| P37 | BMS communication | ON | BMS data/limits should be active |
| P38 | absolute minimum SOC with BMS | 10% | emergency floor during outage |
| P39 | transfer house to grid | 20% | normal grid-transfer threshold |
| P40 | return from grid to SBU | 50% | recovery threshold; forms 20↔50 hysteresis |
| P41 | minimum restart SOC | 50% | restart after low-battery shutdown |
| P43 | solar allocation priority | LBU | house/load before battery charging |
| P44 | grid export permission | Grd / disabled | zero export is mandatory |

### P23 overload bypass

The family manual treats P23/byE as optional rather than a mandatory baseline setting.

Do not turn P23 into a hard compliance failure unless target-device evidence and explicit family choice establish the intended state.

Live SiSeLi evidence may still be used to describe the observed state.

## Expected energy behavior

### Daytime

1. Solar supplies the house first.
2. Solar surplus charges the battery.
3. If house + battery cannot absorb additional solar, production is curtailed.
4. Solar must not be exported to the utility grid.

### Normal night with grid available

1. Battery supplies the house.
2. At approximately 20% SOC, P39 transfers the house to the grid.
3. Battery charging from the grid is not intended; recovery should come from solar.
4. When solar raises SOC to approximately 50%, P40 allows return to normal SBU behavior.
5. Battery may continue charging from 50% toward 100% using solar.

### Grid outage

1. The house can continue on battery below the normal 20% transfer threshold because grid is unavailable.
2. 20% → 10% is emergency reserve, not normal nightly usable capacity.
3. At approximately 10% SOC, P38 may stop the output.
4. After a low-SOC shutdown, P41 requires approximately 50% SOC before restart.

## Battery semantics for UI and analytics

Do **not** show one ambiguous “energy available” number.

Keep these concepts distinct:

1. **Estimated energy currently stored**
   - approximately `SOC × 11.776 kWh`;
   - an estimate, because SOC comes from BMS estimation.

2. **Estimated ordinary-use energy before normal grid transfer**
   - approximately `max(SOC - 20%, 0) × 11.776 kWh`;
   - relevant when grid is available.

3. **Emergency outage reserve**
   - 20% → 10% SOC band;
   - approximately 10% of reference useful capacity (~1.178 kWh nominal-equivalent);
   - not intended for normal nightly consumption.

4. **Protected floor**
   - around 10% SOC;
   - should not be presented as usable household energy.

Do not use battery voltage as a replacement for BMS SOC when valid BMS SOC exists.

## Zero-export semantics

Zero export is an installation invariant.

Consequences for normalization:

- positive grid power can represent import;
- a significant sustained export-like value must not silently become an “export energy” feature;
- negative/export-like readings should be retained raw and classified as unresolved/anomalous until sign/sensor behavior is proven;
- financial logic must never assume export revenue.

The app may later monitor for possible zero-export violations but must remain read-only.

## Expected-behavior analytics

The analysis engine should distinguish normal behavior from anomalies.

### Normal / explainable

- grid begins supplying the house around 20% SOC with valid grid present;
- grid may continue supplying while solar raises battery from 20% toward 50%;
- return to SBU near 50%;
- during an outage, SOC can legitimately fall below 20% toward 10%;
- PV curtailment can occur when battery is full and house demand is low because export is prohibited.

### Worth flagging for review

- SOC rises materially at night while the house is on grid and PV is absent, because OSO is intended to prevent grid battery charging;
- repeated 20↔50 grid/SBU transfers within a short cloudy interval;
- significant sustained export-like grid flow;
- battery routinely entering the 20→10% emergency band while grid is healthy;
- BMS communication loss or SOC/limit fields disappearing;
- equalization becoming enabled for this LiFePO4 battery.

Do not call these faults solely from one sample. Use time-series evidence and explicit confidence.

## Seasonal rule

Do not change battery protection thresholds just because the calendar season changes.

The manual's seasonal strategy primarily changes **large-load timing**.

P40=60% is a conditional experiment only if measured data shows repeated oscillation between grid and SBU on variable-cloud days. It is not a default winter setting.

## Midea water heater context

The Midea water heater is an important known 1.5 kW flexible household load.

Development implications:

- later analytics may use the documented schedule as contextual metadata;
- charts/reports may explain large daytime load blocks in relation to the heater schedule;
- the app may evaluate whether heavy-load timing aligns with solar availability;
- do not add Midea control/write integration merely because the schedule is documented;
- the current Solar of Things application remains read-only with respect to inverter and household equipment.

The manual's core scheduling idea is to heat primarily in solar hours and use stored thermal energy later.

## Family-facing terminology

The manual itself establishes a useful UX pattern: technical terms should be translated into normal household language.

Examples:

- SOC → “Carga de la batería”;
- PV/FV power → “Solar ahora”;
- grid import → “Red eléctrica ahora”;
- SBU → explain as “sol primero, batería después y red al final”;
- 20% → “punto normal para empezar a usar la red”;
- 10% → “reserva mínima durante un corte”;
- 50% return threshold → “cuando la batería se recupera hasta la mitad, vuelve al uso normal de sol y batería”.

Technical codes remain available only in Technical Help / diagnostics.

## Known unknowns that the app must preserve as unknown

- exact inverter firmware version;
- exact CT / zero-export meter model and wiring;
- exact use of the second AC output;
- PV orientation;
- PV inclination;
- shading;
- PV series/parallel string arrangement;
- long-term real seasonal production/consumption curves.

Do not invent these values.

## Read-only safety invariant

The Windows application must not write inverter programs or protection settings.

Configuration compliance features are monitoring/explanation only.

If a future scope ever includes write actions, that would require a separate explicit product decision, safety review, authorization model and implementation phase.

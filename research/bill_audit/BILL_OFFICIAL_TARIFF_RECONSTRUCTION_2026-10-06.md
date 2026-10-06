# Enel bill audit — official tariff reconstruction and economic truth table

Date: 2026-10-06

Status: **ECONOMIC TRUTH TABLE FROZEN / TARGET INVERTER COUNTER CROSS-CHECK PENDING**

Canonical bill case:
- printed period: 2026-08-28 through 2026-09-28;
- billed energy: 97 kWh;
- printed tariff: BT1-T5;
- printed Área Típica: 1A;
- total due: CLP 26,854.

No customer PII is persisted here.

This checkpoint supersedes the earlier working assumption that one September-like rate should explain the full 97 kWh line.

---

## 1. Official rule for a bill spanning two calendar months

The current Chilean distribution tariff decree establishes that when a billing period contains fractions of two calendar months, calendar-month energy consumption is estimated **in proportion to the number of days belonging to each month**.

Authoritative rule:
- Decreto 5T de 2024, Ministerio de Energía;
- section 5.1, general tariff application conditions;
- official consolidated text available through Biblioteca del Congreso Nacional.

For this bill:
- August fraction: 2026-08-28 through 2026-08-31 = **4 days**;
- September fraction: 2026-09-01 through 2026-09-28 = **28 days**;
- total = **32 days**.

For printed 97 kWh:

```
August assigned energy   = 97 × 4 / 32  = 12.125 kWh
September assigned energy = 97 × 28 / 32 = 84.875 kWh
```

This allocation is a tariff/facturation rule. It is not inferred from inverter behavior.

---

## 2. Official Enel tariff evidence

Official Enel archive:
- `https://www.enel.cl/es/clientes/tarifas-y-regulacion/tarifas.html`

The archive currently exposes:
- **Agosto 2026 Retroactivo** — Enel Distribución Chile S.A., Tarifas Suministro Eléctrico 8T / VAD 5T;
- **Septiembre 2026** — Enel Distribución Chile S.A., Tarifas Suministro Eléctrico 8T / VAD 5T.

September direct official PDF:
- Enel Distribución Chile S.A. — Tarifas Suministro Eléctrico 8T / VAD 5T Septiembre de 2026;
- effective from **2026-09-01**.

September official BT1 evidence for the normal Ñuñoa column:
- Administración / cargo fijo monthly IVA column: **727.230 CLP/month**;
- Cargo por servicio público: **0.855 CLP/kWh**, published with zero IVA column;
- Transporte de electricidad IVA column: **20.489 CLP/kWh**;
- Cargo por energía IVA column: **162.041 CLP/kWh**;
- Cargo por compras de potencia IVA column: **29.843 CLP/kWh**;
- BT_AA / T5 cargo por potencia base IVA column: **28.655 CLP/kWh**;
- therefore official `Electricidad consumida (6)` BT_AA/T5 IVA column:
  **220.539 CLP/kWh**.

The same official document defines:
- `Electricidad consumida (6) = (3) + (4) + (5)`;
- BT_AA as aerial high- and low-voltage distribution network;
- T5 as prior-year average monthly residential consumption >230 and <=240 kWh;
- FET <=350 kWh: no surcharge.

Prior repository evidence from the already-captured official August-2026 retroactive publication established for the corresponding BT_AA/T5 normal service column:
- Electricidad consumida IVA column: **220.147 CLP/kWh**;
- Transporte de electricidad IVA column: **20.489 CLP/kWh**;
- Cargo por servicio público: **0.855 CLP/kWh**.

Final external provenance should preserve the locally cached official PDFs and hashes, but the tariff values and cross-month rule are now technically resolved.

---

## 3. Effective electricity rate for this bill

Using the decree-mandated day allocation:

```
(4 × 220.147 + 28 × 220.539) / 32
= 220.490 CLP/kWh
```

Equivalent reconstruction at 97 kWh:

```
12.125 × 220.147
+ 84.875 × 220.539
= 21,387.530 CLP
```

Printed visible line:
- Electricidad consumida: **21,389 CLP**.

Display-level difference:
- **+1.470 CLP** versus the official-rate reconstruction.

This is not treated as a billing error.

---

## 4. Transport and public-service composition

Official September/normal service values:
- Transporte de electricidad: **20.489 CLP/kWh**;
- Cargo por servicio público: **0.855 CLP/kWh**;
- the public-service charge is published separately and with zero IVA column.

At 97 kWh:

```
Transport:
97 × 20.489 = 1,987.433 CLP

Public service:
97 × 0.855 = 82.935 CLP

Combined:
1,987.433 + 82.935 = 2,070.368 CLP
```

Printed visible `Transporte de electricidad` line:
- **2,072 CLP**.

Visible-line difference versus official component reconstruction:
- **+1.632 CLP**.

Important consistency check:
- printed `Monto exento` is **83 CLP**;
- official public-service amount is **82.935 CLP**, which rounds to **83 CLP**.

This is strong evidence that the public-service component is present in the bill economics even though it is not presented as its own visible detailed line.

Do not treat the roughly 2 CLP detailed-line difference as a substantive discrepancy.

---

## 5. Full source-driven bill reconstruction

Use:
- Administration official fixed charge: **727.230 CLP**;
- Electricity consumed: **21,387.530 CLP**;
- Transport: **1,987.433 CLP**;
- Public service: **82.935 CLP**;
- Meter rent: **463 CLP actual-only fixed service charge**.

Reconstructed pre-other-charges subtotal:

```
727.230
+ 21,387.530
+ 1,987.433
+ 82.935
+ 463.000
= 24,648.128 CLP
```

Rounded to peso:
- **24,648 CLP**

Printed `Total boleta`:
- **24,648 CLP**

Result:
- **exact agreement at printed-peso precision**.

This also explains why summing the individually displayed detailed lines gives 24,651 CLP while the actual printed subtotal is 24,648 CLP: source-rate reconstruction supports the printed subtotal, not the naive sum of rounded/grouped visible line displays.

Do not label the 3 CLP visible-line residual as an inconsistency against Enel.

---

## 6. Other charges / credits

Printed:
- Servicio Común: **+5,964 CLP**;
- Subsidio Eléctrico Ley N° 21.667: **-3,758 CLP**.

Official 2026 second-semester subsidy evidence:
- households with 2–3 members: **22,548 CLP** total;
- six monthly installments;
- **3,758 CLP per installment**.

For the counterfactual energy scenarios in this audit:
- Servicio Común is preserved as **actual-only / invariant relative to the apartment's modeled grid-import kWh**;
- Subsidio is **CONDICIONAL_REGULADO**, but the applicable installment is a fixed 3,758 CLP credit for this bill and all modeled totals remain well above the credit amount;
- therefore the full -3,758 CLP is preserved in every current scenario.

Other charges/credits net:

```
5,964 - 3,758 = 2,206 CLP
```

---

## 7. Control reconstruction of the printed total due

```
24,648.128
+ 5,964
- 3,758
= 26,854.128 CLP
```

Rounded:
- **26,854 CLP**

Printed total due:
- **26,854 CLP**

Result:
- **exact agreement at printed-peso precision**.

### Main audit consequence

The bill's tariff/rate structure and monetary total are internally coherent with the official tariff sources and tariff-application rule.

The urgent technical dispute should therefore be concentrated on:
- whether **97 kWh** is the correct metered energy for the billing interval.

Do not dilute the report by suggesting a tariff error that current evidence does not support.

---

## 8. Counterfactual rate model

For any hypothetical total grid-import energy K over the same 32-day billing interval, apply the same official day allocation:

```
August kWh = K × 4 / 32
September kWh = K × 28 / 32
```

Therefore the effective variable rates for this exact interval are:

- Electricity consumed: **220.490 CLP/kWh**;
- Transport: **20.489 CLP/kWh**;
- Public service: **0.855 CLP/kWh**;
- total kWh-sensitive source-driven amount:
  **241.834 CLP/kWh**.

Fixed / scenario-invariant amount currently carried into the total-due counterfactual:

```
Administration  727.230
Meter rent       463.000
Servicio Común 5,964.000
Subsidy        -3,758.000
-------------------------
Fixed net       3,396.230 CLP
```

---

## 9. Energy scenarios carried from the canonical provisional inverter model

Independent inverter-derived values:
- observed: **88.065413 kWh**;
- P5: **88.103333 kWh**;
- P50: **88.103333 kWh**;
- P95: **96.606322 kWh**;
- Enel printed: **97.000000 kWh**.

The Enel value was not used to construct or calibrate the inverter distribution.

---

## 10. Official-source monetary scenarios

| Scenario | kWh | Electricity | Transport | Public service | Reconstructed subtotal | Reconstructed total due |
|---|---:|---:|---:|---:|---:|---:|
| Observed inverter | 88.065413 | 19,417.543 | 1,804.372 | 75.296 | 22,487.441 | **24,693.441** |
| P5 | 88.103333 | 19,425.904 | 1,805.149 | 75.328 | 22,496.611 | **24,702.611** |
| P50 | 88.103333 | 19,425.904 | 1,805.149 | 75.328 | 22,496.611 | **24,702.611** |
| P95 | 96.606322 | 21,300.728 | 1,979.367 | 82.598 | 24,552.923 | **26,758.923** |
| Enel control | 97.000000 | 21,387.530 | 1,987.433 | 82.935 | 24,648.128 | **26,854.128** |

All values above are pre-rounding CLP source reconstructions.

Rounded headline comparison:

| Scenario | Total due rounded | Difference vs printed Enel |
|---|---:|---:|
| Observed inverter | 24,693 | -2,161 CLP |
| P5 | 24,703 | -2,151 CLP |
| P50 | 24,703 | -2,151 CLP |
| P95 | 26,759 | **-95 CLP** |
| Enel control | 26,854 | 0 CLP |

Interpretation:
- at the provisional P50, the modeled total is roughly 2,151 CLP below the printed bill;
- at provisional P95, the monetary difference is only about 95 CLP;
- this mirrors the energy result, where 97 kWh sits only 0.394 kWh above provisional P95.

Do not present P95 as proving a large monetary overcharge.
The stronger technical issue is the meter-energy discrepancy relative to the central inverter estimate, while acknowledging the conservative high-side interval.

---

## 11. Final economic classification

| Printed concept | Canonical class | Counterfactual treatment |
|---|---|---|
| Administración del servicio | FIJO | official 727.230; unchanged |
| Electricidad consumida | VARIABLE_POR_CONSUMO | recompute from Aug/Sep official tariffs using day allocation |
| Transporte de electricidad | VARIABLE_POR_CONSUMO / COMPOSITE_DISPLAY | recompute transport; preserve public-service component explicitly |
| Arriendo medidor | FIJO / ACTUAL_ONLY | preserve 463 |
| Servicio Común | NO_RECONSTRUIBLE / SCENARIO_INVARIANT | preserve 5,964 |
| Subsidio eléctrico | CONDICIONAL_REGULADO | preserve -3,758 for this bill/scenario range |

---

## 12. Remaining evidence gate

The economic truth table is now sufficiently closed for implementation.

The only still-useful owner-side evidence gate before report rendering is the **target HPVINV02 grid-import energy counter/aggregate cross-check**.

Build 499 already exposes the read-only action:
- `Comprobar energía comprada a red`.

That probe can determine whether:
- `buyElectricityQuantity`;
- `dayPurchaseElectricityConsumption`

are real/useful on this actual installation or merely placeholders/unavailable.

This counter is a corroborating inverter-side cross-check.
It is **not required to derive the existing 88.065 / P5 / P50 / P95 values**, and a negative result will not invalidate the current time-series evidence.

---

## 13. Next implementation gate

After target-counter evidence is classified:
1. freeze final source/provenance table;
2. implement the source-driven multi-period tariff reconstruction in the production bill-audit path;
3. render the 8-page report structure;
4. render the technical annex/raw-data export;
5. run one real target-PC PDF QA.

Do not reopen tariff discovery unless new contradictory official evidence appears.

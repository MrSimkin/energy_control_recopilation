# Enel bill audit — bill arithmetic and tariff-clue checkpoint

Date: 2026-10-06

Status: **BILL ARITHMETIC VERIFIED / OFFICIAL TARIFF APPLICABILITY STILL OPEN**

Canonical case:
- printed billing period: 2026-08-28 through 2026-09-28;
- billed energy: 97 kWh;
- printed tariff: BT1-T5;
- printed Área Típica: 1A;
- total due: CLP 26,854.

No customer name, address, account number, RUT or other unnecessary PII is persisted in this checkpoint.

---

## 1. Printed bill structure

Main electricity-service lines:

| Line | Printed amount CLP | Working class |
|---|---:|---|
| Administración del servicio | 727 | fixed / non-kWh |
| Electricidad consumida (97 kWh) | 21,389 | variable-by-consumption |
| Transporte de electricidad | 2,072 | variable-by-consumption |
| Arriendo medidor | 463 | fixed / non-kWh |

Visible-line sum:

```
727 + 21,389 + 2,072 + 463 = 24,651 CLP
```

Printed `Total boleta`:

```
24,648 CLP
```

Difference:

```
-3 CLP
```

Interpretation:
- do not label this as a billing error;
- the amount is consistent with ordinary display-level rounding from underlying decimal rates/tax bases;
- the audit must reconstruct from authoritative decimal source rates rather than expect visible rounded lines to add byte-for-byte to the printed subtotal.

---

## 2. Tax arithmetic

Printed:
- taxable amount: 20,643 CLP;
- IVA: 3,922 CLP;
- exempt amount: 83 CLP;
- total bill subtotal: 24,648 CLP.

Check:

```
20,643 × 19% = 3,922.17
```

Printed IVA:
- 3,922 CLP.

And:

```
20,643 + 3,922 + 83 = 24,648
```

Result:
- tax subtotal arithmetic is internally coherent at printed-peso precision.

Do not infer the tax treatment of each detailed charge merely from this total; exact component taxability still requires source/rule evidence.

---

## 3. Other charges / credits

Printed:
- Servicio Común: +5,964 CLP;
- Subsidio Eléctrico Ley N° 21.667 (4/6): -3,758 CLP.

Check:

```
5,964 - 3,758 = 2,206
```

Printed `Otros cargos/abonos`:
- **2,206 CLP**.

And:

```
24,648 + 2,206 = 26,854
```

Printed total due:
- **26,854 CLP**.

Result:
- the bill total is exactly reconciled at the summary level.

For the inverter counterfactual:
- `Servicio Común` remains actual-only/non-disputed unless evidence proves it depends on this apartment's billed kWh;
- the subsidy remains `CONDICIONAL_REGULADO` until its rule is checked;
- neither should be mechanically scaled by inverter kWh.

---

## 4. Bill-implied variable rates — verification clues only

These are **not authoritative tariff rates**.
They are reverse-calculated clues from the printed line amounts and 97 kWh to help identify the official source row.

### Electricity consumed

```
21,389 / 97 = 220.505154639 CLP/kWh
```

Working clue:
- approximately **220.505 CLP/kWh**.

### Transport

```
2,072 / 97 = 21.360824742 CLP/kWh
```

Working clue:
- approximately **21.361 CLP/kWh**.

Combined printed variable-line ratio:

```
(21,389 + 2,072) / 97
= 241.865979381 CLP/kWh
```

Again:
- do not promote these ratios to tariff authority;
- use them to verify candidate official Enel rows/components.

---

## 5. Relevant prior official-source evidence

The repository already preserves a prior official cross-check against the 2026 Enel regulated-supply tariff publications:

- August 2026 retroactive BT1 candidate for `BT_AA / T5`:
  **220.147 CLP/kWh** in the published IVA column;
- August published `Transporte de electricidad`:
  **20.489 CLP/kWh** in the published IVA column;
- published public-service charge:
  **0.855 CLP/kWh**;
- prior July/August bill evidence strongly selected the August `BT_AA/T5` electricity rate by bill arithmetic.

This evidence belongs to the earlier July/August bill and must not be blindly reused for the new bill.

Current official archive evidence shows:
- an August 2026 retroactive regulated-supply publication;
- a September 2026 regulated-supply publication.

The new bill interval crosses both calendar months and the bill is issued 2026-10-01.

---

## 6. Strong current clue

The new bill's printed electricity amount implies:

**220.505154639 CLP/kWh**.

This is materially different from the prior August `BT_AA/T5` clue:

**220.147 CLP/kWh**.

Difference:

```
220.505154639 - 220.147 = 0.358154639 CLP/kWh
```

If the August rate alone were applied to all 97 kWh:

```
97 × 220.147 = 21,354.259 CLP
```

That would not reproduce the printed 21,389 CLP line.

Therefore:
- the new electricity line is not explained by simply reusing the prior August 220.147 rate;
- a September tariff candidate around 220.505 CLP/kWh is the leading hypothesis;
- exact publication row/applicability must still be verified directly from the official September table.

This inference does **not** determine RED/ETR by itself.

---

## 7. Interim monetary counterfactual using bill-implied ratios

This section is diagnostic only.
It answers: if the exact printed variable-line ratios were held constant, what would the same two lines imply under the independently derived inverter-energy scenarios?

It does **not** replace the official-tariff reconstruction required for the final report.

Provisional inverter energy:
- observed: 88.065413 kWh;
- P5: 88.103333 kWh;
- P50: 88.103333 kWh;
- P95: 96.606322 kWh.

Using bill-implied ratios:
- electricity: 220.505154639 CLP/kWh;
- transport: 21.360824742 CLP/kWh.

| Scenario | Electricity CLP | Transport CLP | Variable subtotal CLP |
|---|---:|---:|---:|
| Enel 97 kWh | 21,389.00 | 2,072.00 | 23,461.00 |
| Observed inverter | 19,418.88 | 1,881.15 | 21,300.03 |
| P5 | 19,427.24 | 1,881.96 | 21,309.20 |
| P50 | 19,427.24 | 1,881.96 | 21,309.20 |
| P95 | 21,302.19 | 2,063.59 | 23,365.78 |

Difference in the two kWh-sensitive lines versus printed Enel:

| Scenario | Difference CLP |
|---|---:|
| Observed inverter | -2,160.97 |
| P5 | -2,151.80 |
| P50 | -2,151.80 |
| P95 | -95.22 |

If every non-variable printed component were mechanically held unchanged, the corresponding total-due diagnostic would be approximately:
- observed: 24,693.03 CLP;
- P5/P50: 24,702.20 CLP;
- P95: 26,758.78 CLP;
- printed: 26,854 CLP.

These totals are **not yet final report amounts** because:
- subsidy treatment remains conditional;
- official tariff rates/applicability are not yet frozen;
- the printed subtotal contains display-level rounding;
- tariff-period treatment must be established.

---

## 8. Required official-tariff closure

Before financial values become external-report evidence:

1. verify the exact September 2026 Enel BT1 table row that reproduces the electricity line;
2. resolve or transparently label:
   - RED/network type;
   - ETR T5 applicability;
   - commune/territory;
3. identify the exact components included in the printed `Transporte de electricidad` line;
4. determine whether the bill uses:
   - one effective publication for the whole billed energy;
   - a supported period allocation;
   - another billing rule documented by Enel;
5. preserve the official PDF/page/source/hash;
6. reconstruct 97 kWh first;
7. only then apply the identical supported variable component rule to observed/P5/P50/P95.

No bill-implied ratio may be silently substituted for the official-source rate in the final report.

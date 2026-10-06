# Post-QA validation of provisional P95 methodology — 2026-10-06

Status: **INDEPENDENT REPRODUCTION PASS / SEMANTIC CAVEAT STRENGTHENED**

Context:
- owner returned the first real Build 527 audit PDF and technical-annex ZIP;
- owner explicitly requested re-validation of the P95 methodology;
- this check is performed against the exported evidence and the original Research Package, not by trusting the rendered PDF value.

Inputs:
- Research Package: `SolarOfThings-ResearchPackage-20261006-154413.zip`;
- SHA-256: `3a7bc7edb9e264333cd6da73b85f9f2d805b6eb4f5ef20594ddc34ffdbdd6295`;
- returned annex: `Anexo-Tecnico-Boleta-Enel-20261006-1921.zip`;
- returned audit PDF: `Auditoria-Boleta-Enel-20261006-1918.pdf`;
- returned annex manifest producer:
  `0.10.0+build.527.c3ba58b371c6a9331416f0aae91061d8005f6b02`.

No Enel billed-kWh value is used in any reconstruction below.

---

## 1. Returned annex integrity

Every manifest-listed annex member was recomputed and matched:
- byte length: PASS;
- SHA-256: PASS.

The returned annex therefore has internally consistent integrity metadata.

---

## 2. Deterministic energy reproduction

From the original Research Package:

- directly observed grid import:
  **88.06541295361109 kWh**;
- deterministic completion of the two bill-edge slivers:
  **0.03791997222222222 kWh**;
- covered time:
  **761.8987069444471 h**;
- uncovered time:
  **5.101293055555556 h**.

These values reproduce the returned annex/report at displayed precision.

---

## 3. Internal-gap calibration reproduced independently

Frozen report method:

`bill-gap-calendar-window-empirical.v1`

### Gap 2 — 15.01845 min weekend

Most recent 15 eligible prior weekend-window energies:

- 12 zeros;
- 0.450404568 kWh;
- 0.857621495 kWh;
- 0.882829382 kWh.

Inverse empirical:
- q05 = 0;
- q50 = 0;
- q95 = **0.882829382 kWh**.

### Gap 3 — 262.453417 min weekend

Most recent 15 eligible prior weekend-window energies:

- 13 zeros;
- 4.087507396 kWh;
- 8.502989252 kWh.

Inverse empirical:
- q05 = 0;
- q50 = 0;
- q95 = **8.502989252 kWh**.

Target observed boundaries are 0 W / 0 W.

Supporting diagnostic:
- every available prior same-daytype window starting INACTIVE integrates to zero;
- this boundary state is intentionally **not** used to narrow the headline interval.

Therefore the 8.503 kWh q95 is deliberately conservative on the high side.

### Gap 4 — 20.016917 min weekday

Most recent 15 eligible prior weekday-window energies:

- 14 zeros;
- 0.180590088 kWh.

Inverse empirical:
- q05 = 0;
- q50 = 0;
- q95 = **0.180590088 kWh**.

Target boundaries are 676 W / 646 W.

A simple trapezoidal boundary bridge would be approximately:
- **0.220520 kWh**.

This confirms that the primary calendar-window method does not condition on target boundary power.
That is an intentional simplification/conservatism decision and is now disclosed more clearly.

---

## 4. Exact aggregate P95 reproduced

Three internal gaps × 15 empirical outcomes each:

`15^3 = 3,375 exact combinations`

Each total is:

`observed + deterministic bill edges + gap2 + gap3 + gap4`

Inverse empirical aggregate results reproduced independently:

- P5: **88.10333292583331 kWh**;
- P50: **88.10333292583331 kWh**;
- P95: **96.60632217736110 kWh**;
- maximum empirical combination:
  **97.66974164694443 kWh**.

Therefore:

**96.606322 kWh is arithmetically correct under the frozen method.**

The returned Build 527 value is not a coding/transcription error.

---

## 5. Why aggregate P95 equals 96.606 rather than the maximum

With 3,375 exact totals:
- aggregate q95 is the inverse empirical 95th percentile;
- it is not the maximum;
- the top empirical support extends above q95.

Diagnostics:
- 57 combinations are strictly above 96.606322 kWh;
- 45 combinations reach or exceed 97.000000 kWh;
- maximum = 97.669742 kWh.

These frequencies are **not** to be presented as calibrated probabilities of Enel being correct/incorrect because the gap-independence assumption is provisional.

---

## 6. Dependence assumption is the material caveat

The aggregate exact Cartesian distribution treats separated gaps as independent empirical draws.

This is a reasonable provisional engineering assumption because:
- the gaps occur on distinct dates;
- each uses its own historical topology;
- no Enel value enters the construction.

But it is not externally validated as a metrological probability model.

A perfect positive-dependence/comonotonic stress could move the high-side total toward the joint empirical maximum:
- **97.669742 kWh**.

Therefore the correct external conclusion is **not**:

> “97 kWh is statistically impossible because it is above P95.”

The correct conclusion is:

> “97 kWh is 0.394 kWh above the provisional P95 under the stated gap-aggregation assumption, while the full empirical support reaches 97.670 kWh. The exceedance above P95 is therefore a small, model-dependent difference rather than statistical exclusion.”

This semantic correction is mandatory for the next report build.

---

## 7. Sensitivity checks remain favorable/conservative

Previously frozen sensitivity was independently re-reviewed:

Rolling comparable-date histories:
- 10 dates -> P95 96.606322;
- 12 dates -> P95 96.606322;
- 15 dates -> P95 96.606322;
- 20 dates -> P95 93.254260;
- 25 dates -> P95 92.320635;
- 30 dates -> P95 92.190840.

Removing weekday/weekend conditioning:
- aggregate P95 falls to **90.256913 kWh**.

Conditioning more strongly on target boundary state:
- also narrows the high side materially;
- a separate boundary/state-conditioned experimental method produced a total high-side value far below the frozen calendar-window P95.

Therefore:
- the selected 96.606 kWh is not produced by an aggressive narrowing rule;
- among the tested reasonable variants it is intentionally wide/conservative.

---

## 8. Discrete empirical-quantile disclosure

With exactly 15 calibration windows per internal gap and inverse empirical quantiles:
- individual-gap q05 = minimum of the 15;
- individual-gap q95 = maximum of the 15.

Thus the gap-level P5–P95 support spans the complete recent empirical range.

The aggregate P95 is then taken over all 3,375 combinations.

The next PDF must disclose this discrete property explicitly.

---

## 9. Owner-PDF QA findings

The first real PDF showed:
- genuine text overlap in the gap-detail table, especially long labels such as `BOUNDARY_START` / `BOUNDARY_END`;
- section-5 pagination left methodological controls orphaned on the following page;
- economic scenario page reported `TARIFA NO RESUELTA` despite the already frozen official economic truth table;
- annex economic CSV contained headers only.

These are implementation/presentation defects, not failures of the deterministic energy result.

Corrective actions initiated:
1. shorten/localize gap-kind labels and widen relevant columns;
2. intentionally split daily coverage onto a dedicated continuation page;
3. harden P95 semantics with maximum empirical support / dependence caveat;
4. fix cross-publication tariff identity matching to prefer semantic RED/ETR identity over parser-local `CandidateIndex`;
5. suppress the obsolete multi-period “pending” tariff table when the official scenario model is successfully reconciled.

---

## 10. Decision

P95 result:

**KEEP 96.606322 kWh as the provisional-method P95.**

Do not replace it post hoc merely because another model would strengthen the discrepancy.

But:
- weaken any wording that implies statistical exclusion of 97 kWh;
- show full empirical maximum 97.670 kWh in methodology/interpretation;
- preserve the stronger and more robust finding:
  **P50 = 88.103333 kWh, 8.897 kWh below the billed 97 kWh**.

Method status remains:

**PROVISIONAL / RESEARCH ONLY**

R3 remains separate and unchanged.

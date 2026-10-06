# Bill audit — provisional whole-window empirical gap method

Date: 2026-10-06

Status: **PROVISIONAL REPORT METHOD / RESEARCH ONLY / NOT PRODUCTION R3**

Method label:

`bill-gap-calendar-window-empirical.v1`

Canonical harness:

`research/bill_audit/bill_gap_calendar_window_empirical.py`

Harness commit:

`236ce274634a0a17cb3e0f0268981a6ceec4ba01`

Input package:

`SolarOfThings-ResearchPackage-20261006-154413.zip`

Input SHA-256:

`3a7bc7edb9e264333cd6da73b85f9f2d805b6eb4f5ef20594ddc34ffdbdd6295`

This method is specific to the urgent bill-audit evidence path.
It does **not** replace the frozen R3 research candidate, does **not** alter production C#, and does **not** constitute R3 validation.

The Enel billed value is excluded from:
- model construction;
- historical-window selection;
- quantile calculation;
- backtesting;
- parameter selection.

The billed value is compared only after the inverter-derived distribution has been constructed.

---

## 1. Why another method is necessary for this bill

The deterministic bill interval contains one internal telemetry gap of:

- **262.453 minutes**;
- local time 2026-09-12 15:09:33.752 through 19:32:00.957;
- observed grid-import boundary values: **0 W / 0 W**.

Frozen R3 is intentionally limited to individual gaps <=240 minutes.

Therefore:
- the complete bill period cannot receive a fully calibrated R3 interval;
- the R3 limit must not be weakened to make the bill fit;
- current production `grid-import-empirical-bootstrap.v1` is also unsuitable as an unquestioned external-evidence interval because prior research showed material undercoverage under serial dependence.

A separate, explicitly provisional report method is therefore evaluated.

---

## 2. Design goal

The urgent report needs a method that:

1. preserves real temporal episodes rather than resampling independent five-minute frames;
2. uses only historical inverter evidence strictly prior to each real target gap;
3. remains reproducible and simple enough to explain to a third party;
4. avoids using the Enel billed value;
5. is conservative rather than aggressively narrowing around the target boundaries;
6. can be backtested on fully observed historical windows with the same topology.

---

## 3. Frozen provisional algorithm

For every **internal** gap:

1. preserve the target local wall-clock start time;
2. preserve the actual target duration;
3. determine target day type:
   - WEEKDAY;
   - WEEKEND;
4. search historical local dates strictly earlier than the target date;
5. retain only windows:
   - at the same local wall-clock start;
   - with the same duration;
   - with the same day type;
   - with a source sample within 5.5 minutes of each window boundary;
   - with every internal telemetry link within the deterministic continuity threshold;
6. take the most recent **15** eligible historical dates;
7. require at least **10** eligible dates;
8. integrate every historical window using actual timestamps and trapezoidal integration;
9. give each historical date one equal empirical outcome;
10. calculate inverse empirical-CDF q05/q50/q95.

Important conservative choice:
- the primary interval does **not** condition on the target's observed start/end power;
- target boundary state is retained only as supporting diagnostic evidence;
- this intentionally allows historical high-import episodes into the interval even where the target's observed boundaries are 0 W.

For bill-boundary slivers <=5.5 minutes:
- use the nearest observed boundary power deterministically;
- this avoids inventing a statistical model for a few minutes at the outer bill boundary.

For multiple internal gaps:
- each gap keeps its own empirical historical distribution;
- this provisional method assumes independent draws across distinct gaps;
- because this bill has 15 outcomes for each of 3 internal gaps, the total is calculated exactly over all:
  **15³ = 3,375 combinations**;
- no Monte Carlo approximation is needed.

---

## 4. Deterministic base

Directly observed inverter import:

**88.065413 kWh**

Temporal coverage:

**99.334903%**

Covered time:

**761.898707 h**

Uncovered time:

**5.101293 h**

Deterministic completion of the two very short bill-boundary slivers:

**0.037920 kWh**

No Enel value enters these calculations.

---

## 5. Gap-specific historical calibration

### Gap 1 — bill start boundary

- duration: 3.643 min;
- nearest observed import: 0 W;
- deterministic contribution: 0 kWh.

No statistical distribution required.

### Gap 2 — 2026-09-05, 15.018 min

Target:
- WEEKEND;
- start/end observed import: 0 W / 0 W.

Calibration:
- 15 most recent eligible weekend dates;
- range: 2026-07-12 through 2026-08-30.

Empirical contribution:
- q05: **0.000000 kWh**;
- q50: **0.000000 kWh**;
- q95: **0.882829 kWh**.

Prequential historical backtest:
- cases: **27**;
- P5–P95 empirical coverage: **92.59%**;
- mean P50 signed bias: **-0.0811 kWh**;
- mean P50 MAE: **0.0811 kWh**;
- mean interval width: **0.2933 kWh**.

Supporting boundary-state diagnostic, **not used to narrow the primary interval**:
- prior same-daytype windows starting INACTIVE: **34**;
- same-start-state windows with non-zero grid-import energy: **0**.

### Gap 3 — 2026-09-12, 262.453 min

Target:
- WEEKEND;
- start/end observed import: 0 W / 0 W.

Calibration:
- 15 most recent eligible weekend dates;
- range: 2026-07-12 through 2026-09-06.

Empirical contribution:
- q05: **0.000000 kWh**;
- q50: **0.000000 kWh**;
- q95: **8.502989 kWh**.

Prequential historical backtest:
- cases: **25**;
- P5–P95 empirical coverage: **96.00%**;
- mean P50 signed bias: **-0.5036 kWh**;
- mean P50 MAE: **0.5036 kWh**;
- mean interval width: **3.0611 kWh**.

Supporting boundary-state diagnostic, **not used to narrow the primary interval**:
- prior same-daytype windows starting INACTIVE: **33**;
- same-start-state windows with non-zero grid-import energy: **0**.

This is important supporting evidence:
- the target itself has 0 W boundaries;
- every available prior comparable same-daytype window with the same INACTIVE start state also integrated to zero grid import;
- nevertheless the primary q95 intentionally keeps high-import historical weekend windows that began ACTIVE, making the headline interval more conservative.

### Gap 4 — 2026-09-24, 20.017 min

Target:
- WEEKDAY;
- start/end observed import: 676 W / 646 W.

Calibration:
- 15 most recent eligible weekday dates;
- range: 2026-09-03 through 2026-09-23.

Empirical contribution:
- q05: **0.000000 kWh**;
- q50: **0.000000 kWh**;
- q95: **0.180590 kWh**.

Prequential historical backtest:
- cases: **98**;
- P5–P95 empirical coverage: **95.92%**;
- mean P50 signed bias: **-0.0401 kWh**;
- mean P50 MAE: **0.0401 kWh**;
- mean interval width: **0.3123 kWh**.

Supporting start-state diagnostic:
- prior same-daytype ACTIVE-start windows: **10**;
- non-zero: **10**.

The primary interval still does not condition on that state.

### Gap 5 — bill end boundary

- duration: 4.946 min;
- nearest observed import: 460 W;
- deterministic contribution: **0.037920 kWh**.

No statistical distribution required.

---

## 6. Aggregate provisional inverter distribution

Exact Cartesian aggregation over the 3 internal empirical distributions:

- exact combinations: **3,375**;
- observed deterministic energy: **88.065413 kWh**;
- deterministic bill-edge completion: **0.037920 kWh**;
- **P5: 88.103333 kWh**;
- **P50: 88.103333 kWh**;
- **P95: 96.606322 kWh**;
- empirical mean: **89.100796 kWh**;
- maximum empirical combination: **97.669742 kWh**.

P5 and P50 coincide because the empirical gap distributions are heavily zero-inflated.

This equality is a genuine property of the discrete empirical distribution, not a formatting error.

---

## 7. Comparison with the printed 97 kWh — post-model only

Only after constructing the inverter-derived distribution independently:

Printed Enel consumption:

**97.000000 kWh**

Differences:

| Comparison | Enel minus inverter | Relative to 97 kWh |
|---|---:|---:|
| Directly observed | 8.934587 kWh | 9.2109% |
| P5 | 8.896667 kWh | 9.1718% |
| P50 | 8.896667 kWh | 9.1718% |
| P95 | **0.393678 kWh** | **0.4059%** |

Interpretation:

- the bill value is above the provisional P95;
- however, the excess above P95 is small, approximately 0.39 kWh;
- therefore this provisional interval does **not** support presenting a large high-confidence statistical separation between 97 kWh and all plausible inverter completions;
- it does support the statement that 97 kWh is slightly above the upper 95th empirical quantile under this conservative historical-window method.

The maximum historical empirical combination reaches 97.670 kWh.

Therefore:
- it would be incorrect to claim that 97 kWh is physically impossible under the inverter history;
- it would also be incorrect to ignore that most of the provisional empirical distribution lies materially below 97 kWh.

No tail frequency from the 3,375 combinations is to be presented as a calibrated probability of Enel being correct/incorrect; independence between gaps and the report-specific model are not validated to that standard.

---

## 8. Backtest interpretation

The three actual internal gap topologies have prequential historical P5–P95 point coverage:

- 15.0 min weekend: **92.6%**;
- 262.5 min weekend: **96.0%**;
- 20.0 min weekday: **95.9%**.

These values are encouraging relative to a nominal 90% central interval.

But they are **development/backtest evidence**, not a pristine future holdout.

Limitations:
- historical windows overlap calendar regimes and are not independent experimental trials;
- target-specific long-gap truth is unknowable because the telemetry is actually missing;
- gap-to-gap independence in the aggregate is a provisional assumption;
- only one installation/device family is represented;
- the method was developed specifically to address this bill-gap topology;
- no legal/metrological status is implied.

Accordingly, the correct label remains:

**PROVISIONAL REPORT METHOD / RESEARCH ONLY**

---

## 9. Relationship with R3

Do not confuse this method with R3.

R3:
- models residual energy around observed gap boundaries;
- is limited to <=240 minutes;
- has a frozen future-validation protocol;
- remains INSUFFICIENT and unscored.

This provisional method:
- uses complete historical calendar windows;
- permits the real 262.453-minute gap because it is explicitly designed/backtested for that topology;
- deliberately does not use boundary state in its primary quantiles;
- exists only to support the urgent bill-audit evidence path.

A future R3 PASS does not automatically validate this report method.
A future report-method decision does not alter R3.

---

## 10. Proposed report semantics

If this method is accepted for the urgent external report, the plain-language wording should be approximately:

> El inversor registró directamente 88,07 kWh de importación desde la red durante el 99,33% del tiempo analizado. Los períodos sin telemetría no fueron tratados como consumo cero. Para estimarlos se utilizaron ventanas históricas completas del mismo horario, duración y tipo de día, conservando la variación temporal dentro de cada episodio. El resultado central es 88,10 kWh y el rango P5–P95 es 88,10–96,61 kWh.

Then explain P5/P50/P95 using the canonical layman definitions.

Do **not** describe the interval as:
- meter tolerance;
- measurement-error specification;
- certified confidence interval;
- R3-validated result.

---

## 11. Decision status

For the urgent evidence package this method is now technically frozen as:

`bill-gap-calendar-window-empirical.v1`

but remains **provisional** until the remaining evidence package is assembled.

No retuning should be performed against the 97 kWh bill value.

Next work:
1. reconstruct official tariff evidence for the bill interval;
2. investigate a target-specific HPVINV02 grid-import energy counter/aggregate;
3. build the economic truth table;
4. then decide whether this provisional statistical method is sufficiently defensible for the final printed report or whether the long gap must instead be presented as an explicit statistical insufficiency.

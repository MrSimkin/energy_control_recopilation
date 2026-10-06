# Bill-gap provisional method — sensitivity analysis

Date: 2026-10-06

Status: **ROBUSTNESS CHECK / DOES NOT RETUNE THE FROZEN PROVISIONAL METHOD**

Canonical provisional method:
- `bill-gap-calendar-window-empirical.v1`;
- harness: `research/bill_audit/bill_gap_calendar_window_empirical.py`;
- primary rolling history: 15 comparable prior dates;
- same local wall-clock start + actual duration + weekday/weekend class;
- target boundary state not used to narrow headline quantiles.

Purpose:
- test whether the target P5/P50/P95 conclusion is an artifact of the chosen 15-day history;
- do not select an alternate window based on its relationship with the 97 kWh Enel value.

The primary 15-day rule was fixed before this sensitivity check and is not changed by the results below.

---

## 1. Rolling-history length sensitivity

The exact same method was rerun with:
- 10;
- 12;
- 15;
- 20;
- 25;
- 30
most recent comparable prior dates.

No Enel value enters any rerun.

| Prior dates | Aggregate P5 kWh | P50 kWh | P95 kWh | Mean kWh | Max kWh |
|---:|---:|---:|---:|---:|---:|
| 10 | 88.103333 | 88.103333 | **96.606322** | 89.599527 | 97.669742 |
| 12 | 88.103333 | 88.103333 | **96.606322** | 89.350161 | 97.669742 |
| 15 | 88.103333 | 88.103333 | **96.606322** | 89.100796 | 97.669742 |
| 20 | 88.103333 | 88.103333 | 93.254260 | 88.851430 | 97.669742 |
| 25 | 88.103333 | 88.103333 | 92.320635 | 88.718412 | 97.669742 |
| 30 | 88.103333 | 88.103333 | 92.190840 | 88.622899 | 97.699143 |

Finding:
- P5/P50 are invariant across all tested windows;
- P95 is exactly invariant from 10 through 15 days;
- longer 20–30 day histories make P95 **lower**, not higher;
- therefore the frozen 15-day choice is not generating a more favorable discrepancy against Enel than longer-history alternatives;
- among these tested reasonable windows, 15 days is materially more conservative on the high side.

---

## 2. Gap-specific q95 sensitivity

### 15.018-minute weekend gap

| Prior dates | q95 kWh |
|---:|---:|
| 10 | 0.882829 |
| 12 | 0.882829 |
| 15 | 0.882829 |
| 20 | 0.857621 |
| 25 | 0.857621 |
| 30 | 0.857621 |

### 262.453-minute weekend gap

| Prior dates | q95 kWh |
|---:|---:|
| 10 | 8.502989 |
| 12 | 8.502989 |
| 15 | 8.502989 |
| 20 | 4.087507 |
| 25 | 4.087507 |
| 30 | 4.087507 |

### 20.017-minute weekday gap

| Prior dates | q95 kWh |
|---:|---:|
| 10 | 0.180590 |
| 12 | 0.180590 |
| 15 | 0.180590 |
| 20 | 0.000000 |
| 25 | 0.147327 |
| 30 | 0.180590 |

The dominant long-gap high-side estimate is therefore driven by a rare high-import comparable weekend window retained inside the most recent 15 dates.

This is deliberate conservatism:
- the target itself has 0 W / 0 W grid-import boundaries;
- primary quantiles intentionally do not use those boundaries to remove the high-import historical episode.

---

## 3. Historical backtest sensitivity

### 15-minute weekend topology

Coverage stays:
- **92.59%** for every tested history length.

### 262-minute weekend topology

- 10 dates: **96.00%**;
- 15 dates: **96.00%**;
- 20 dates: **92.00%**;
- 25 dates: **92.00%**;
- 30 dates: **92.00%**.

The 15-day rule therefore has:
- higher historical point coverage;
- a wider/more conservative interval;
than the 20–30 day variants for the dominant gap.

### 20-minute weekday topology

- 10 dates: **95.92%**;
- 15 dates: **95.92%**;
- 20–30 dates: **94.90%**.

No tested history length produces a material calibration advantage over 15 days.

---

## 4. Day-type conditioning stress

A separate diagnostic removed the weekday/weekend condition while retaining:
- same wall-clock start;
- same gap duration;
- latest 15 prior complete windows.

Result:
- aggregate P5: 88.103333 kWh;
- aggregate P50: 88.103333 kWh;
- aggregate P95: **90.256913 kWh**;
- maximum empirical total: 90.600824 kWh.

Therefore:
- same-daytype conditioning materially widens the high side;
- removing day type would make the discrepancy against Enel much larger.

The canonical provisional method keeps daytype conditioning because:
- household/grid-use regimes differ between weekdays and weekends;
- the target dominant gap occurs on a weekend;
- preserving daytype is a pre-outcome contextual rule and is more conservative for this case.

Do not remove daytype merely because it would strengthen the claim against Enel.

---

## 5. Discrete empirical-quantile consequence

With only 15 calibration dates per gap and inverse empirical quantiles:

- q05 resolves to the minimum observed calibration value;
- q95 resolves to the maximum observed calibration value.

Thus, at the individual-gap level, the frozen 15-day P5–P95 bounds span the complete empirical range of those 15 recent comparable dates.

This is conservative and should be disclosed in the technical methodology.

At the total-bill level:
- the exact Cartesian convolution has 3,375 combinations;
- aggregate P95 is the empirical 95th percentile of those total combinations, not their maximum.

---

## 6. Conclusion

The provisional P95 of **96.606322 kWh** is not a fragile artifact of using exactly 15 dates:
- it is unchanged at 10 and 12 dates;
- longer histories lower it substantially;
- removing daytype lowers it substantially;
- same-start-state diagnostics would also narrow the interval, but are intentionally excluded from the headline method.

Therefore the existing 15-day/daytype-only specification remains the preferred conservative report candidate.

This robustness check does not upgrade the method to:
- production;
- R3;
- certified validation;
- legal meter tolerance.

It only supports the claim that the provisional report interval was not chosen by a parameter tweak that artificially maximized the discrepancy with the bill.

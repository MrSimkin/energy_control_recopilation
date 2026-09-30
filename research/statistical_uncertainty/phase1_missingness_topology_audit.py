#!/usr/bin/env python3
"""Phase 1 Lot 3: paired missingness-topology audit.

Compares equal total statistical gap duration (2h) distributed as:
- 1x120m
- 2x60m
- 4x30m
- 6x20m

The same synthetic underlying series is reused across topologies inside each
repetition. Current method remains 2,000 simulated completions.
"""

import math
from datetime import datetime, timedelta

import numpy as np
import pandas as pd
from scipy.signal import lfilter
from scipy.stats import binomtest

DT_HOURS = 5.0 / 60.0
START = datetime(2026, 1, 5)
N_INTERVALS = 14 * 24 * 12
TIMES = [START + timedelta(minutes=5*i) for i in range(N_INTERVALS + 1)]
HOURS = np.array([t.hour for t in TIMES], dtype=np.int16)
WEEKENDS = np.array([t.weekday() >= 5 for t in TIMES], dtype=np.int8)
CAT = HOURS + 24 * WEEKENDS
PROFILE = np.array([
    320
    + 700*math.exp(-0.5*(((t.hour+t.minute/60)-8)/1.5)**2)
    + 300*math.exp(-0.5*(((t.hour+t.minute/60)-13)/2.5)**2)
    + 1050*math.exp(-0.5*(((t.hour+t.minute/60)-20)/2)**2)
    for t in TIMES
], dtype=float)

SLOTS = np.arange(2*24*12, 12*24*12, 36)  # 3h apart
TOPOLOGIES = {
    "1x120": [24],
    "2x60": [12, 12],
    "4x30": [6, 6, 6, 6],
    "6x20": [4, 4, 4, 4, 4, 4],
}

def ar_res(phi, sd, rng, n):
    innov = rng.normal(0, sd*math.sqrt(1-phi**2), n)
    return lfilter([1.0], [1.0, -phi], innov)

def build_series(name, rng):
    if name == "stable":
        v = 800 + rng.normal(0, 140, len(TIMES))
    elif name == "day_night":
        v = PROFILE + rng.normal(0, 120, len(TIMES))
    elif name == "ar_moderate":
        v = PROFILE + ar_res(.55, 220, rng, len(TIMES))
    elif name == "ar_high":
        v = PROFILE + ar_res(.93, 300, rng, len(TIMES))
    else:
        raise ValueError(name)
    return np.clip(v, 20, None)

def full_energy(v):
    return float(np.sum((v[:-1] + v[1:]) * 0.5 * DT_HOURS) / 1000)

def interval_energy(v, s, k):
    return float(np.sum((v[s:s+k] + v[s+1:s+k+1]) * 0.5 * DT_HOURS) / 1000)

def gaps_from_permutation(topology, permutation):
    lengths = TOPOLOGIES[topology]
    starts = np.sort(permutation[:len(lengths)])
    return [(int(s), int(k)) for s, k in zip(starts, lengths)]

def simulate_current(v, gaps, simulations, rng):
    keep = np.ones(len(v), dtype=bool)
    true_missing = 0.0
    segment_categories = []

    for s, k in gaps:
        keep[s+1:s+k] = False
        true_missing += interval_energy(v, s, k)
        for offset in range(k):
            mid = TIMES[s+offset] + timedelta(minutes=2.5)
            segment_categories.append(mid.hour + 24*(mid.weekday() >= 5))

    outside = full_energy(v) - true_missing
    segment_categories = np.asarray(segment_categories, dtype=np.int16)
    missing = np.zeros(simulations)

    for cat in np.unique(segment_categories):
        m = int(np.sum(segment_categories == cat))
        pool = v[keep & (CAT == cat)]
        if len(pool) < 12:
            raise RuntimeError("strict candidate pool unexpectedly insufficient")
        indexes = rng.integers(0, len(pool), size=(simulations, m))
        missing += pool[indexes].sum(axis=1) * DT_HOURS / 1000

    outcomes = outside + missing
    q = np.quantile(outcomes, [.05, .50, .95], method="linear")
    return q, true_missing

def interval_score(lower, upper, truth, alpha=.10):
    score = upper - lower
    if truth < lower:
        score += (2/alpha) * (lower-truth)
    if truth > upper:
        score += (2/alpha) * (truth-upper)
    return score

def wilson(successes, n):
    z = 1.959963984540054
    p = successes/n
    d = 1 + z*z/n
    c = (p + z*z/(2*n))/d
    h = z*math.sqrt(p*(1-p)/n + z*z/(4*n*n))/d
    return c-h, c+h

def holm_adjust(pvalues):
    order = sorted(range(len(pvalues)), key=lambda i: pvalues[i])
    adjusted = [0.0]*len(pvalues)
    previous = 0.0
    m = len(pvalues)
    for rank, i in enumerate(order):
        value = min(1.0, (m-rank)*pvalues[i])
        value = max(previous, value)
        adjusted[i] = value
        previous = value
    return adjusted

def run(repetitions=1000, simulations=2000, seed=20260930):
    raw = []
    paired_scores = []

    for scenario in ["stable", "day_night", "ar_moderate", "ar_high"]:
        master = np.random.default_rng(seed + sum(map(ord, scenario))*1000)
        data = {
            t: {"covered": [], "error": [], "width": [], "score": [], "missing": []}
            for t in TOPOLOGIES
        }

        for _ in range(repetitions):
            series_seed = int(master.integers(0, 2**63-1))
            v = build_series(scenario, np.random.default_rng(series_seed))
            permutation = master.permutation(SLOTS)
            truth = full_energy(v)

            for ti, topology in enumerate(TOPOLOGIES):
                gaps = gaps_from_permutation(topology, permutation)
                q, true_missing = simulate_current(
                    v,
                    gaps,
                    simulations,
                    np.random.default_rng((series_seed + (ti+1)*104729) % (2**63-1))
                )
                lower, median, upper = q
                data[topology]["covered"].append(int(lower <= truth <= upper))
                data[topology]["error"].append(median-truth)
                data[topology]["width"].append(upper-lower)
                data[topology]["score"].append(interval_score(lower, upper, truth))
                data[topology]["missing"].append(true_missing)

        for topology, d in data.items():
            successes = sum(d["covered"])
            lo, hi = wilson(successes, repetitions)
            err = np.asarray(d["error"])
            width = np.asarray(d["width"])
            score = np.asarray(d["score"])
            missing = np.asarray(d["missing"])

            raw.append({
                "scenario": scenario,
                "topology": topology,
                "repetitions": repetitions,
                "simulations": simulations,
                "coverage_pct": successes/repetitions*100,
                "wilson_low_pct": lo*100,
                "wilson_high_pct": hi*100,
                "calibration_gap_pp": successes/repetitions*100 - 90,
                "p50_bias_kwh": err.mean(),
                "p50_mae_kwh": np.abs(err).mean(),
                "p50_rmse_kwh": np.sqrt(np.mean(err**2)),
                "median_interval_width_kwh": np.median(width),
                "mean_interval_score": score.mean(),
                "median_interval_score": np.median(score),
                "median_true_missing_kwh": np.median(missing),
                "successes": successes,
            })

        base = np.asarray(data["1x120"]["score"])
        for topology in ["2x60", "4x30", "6x20"]:
            diff = np.asarray(data[topology]["score"]) - base
            brng = np.random.default_rng(
                seed + sum(map(ord, scenario)) + sum(map(ord, topology))
            )
            means = np.empty(5000)
            for b in range(5000):
                idx = brng.integers(0, repetitions, size=repetitions)
                means[b] = diff[idx].mean()
            paired_scores.append({
                "scenario": scenario,
                "comparison": f"{topology}-1x120",
                "mean_interval_score_difference": diff.mean(),
                "bootstrap95_low": np.quantile(means, .025),
                "bootstrap95_high": np.quantile(means, .975),
            })

    pvalues = [
        binomtest(int(r["successes"]), repetitions, .90, alternative="less").pvalue
        for r in raw
    ]
    adjusted = holm_adjust(pvalues)
    for r, p, a in zip(raw, pvalues, adjusted):
        r["one_sided_binomial_p"] = p
        r["holm_adjusted_p"] = a
        r["undercoverage_reject_holm_0_05"] = a < .05

    return pd.DataFrame(raw), pd.DataFrame(paired_scores)

if __name__ == "__main__":
    result, paired = run()
    result.to_csv("PHASE1_LOT3_RESULTS_2026-09-30.csv", index=False)
    paired.to_csv("PHASE1_LOT3_PAIRED_INTERVAL_SCORE_2026-09-30.csv", index=False)
    print(result.to_string(index=False))

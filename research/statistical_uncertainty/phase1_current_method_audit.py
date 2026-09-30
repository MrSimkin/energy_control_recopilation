#!/usr/bin/env python3
"""
Phase 1 synthetic audit harness for grid-import-empirical-bootstrap.v1.

Purpose:
- reproduce the current UtilityGridImportStatisticalCompletionService behavior
  against synthetic series whose true complete energy is known;
- keep the current production baseline at 2,000 simulations;
- allow --simulation-count to vary in later convergence work.

This is a behavioral mirror for research/audit. It does not call the C# service
or SQLite directly. Production code is not modified by this harness.
"""

from __future__ import annotations

import argparse
import math
from dataclasses import dataclass
from datetime import datetime, timedelta

import numpy as np
from scipy.signal import lfilter

METHOD_VERSION = "grid-import-empirical-bootstrap.v1"
SEGMENT_MINUTES = 5.0
DT_HOURS = SEGMENT_MINUTES / 60.0

START = datetime(2026, 1, 5, 0, 0)  # Monday
N_INTERVALS = 14 * 24 * 12
TIMES = np.array(
    [START + timedelta(minutes=5 * i) for i in range(N_INTERVALS + 1)],
    dtype=object,
)
HOURS = np.array([item.hour for item in TIMES], dtype=np.int16)
WEEKENDS = np.array([item.weekday() >= 5 for item in TIMES], dtype=bool)


@dataclass(frozen=True)
class AuditResult:
    scenario: str
    repetitions: int
    simulation_count: int
    gap_hours: float
    coverage_percent: float
    coverage_ci95_low_percent: float
    coverage_ci95_high_percent: float
    p50_bias_kwh: float
    p50_mae_kwh: float
    p50_rmse_kwh: float
    p50_median_abs_percent_missing: float
    median_true_missing_kwh: float
    median_interval_width_kwh: float
    median_width_percent_true_missing: float


def daily_profile(timestamp: datetime) -> float:
    hour = timestamp.hour + timestamp.minute / 60.0
    base = 320.0
    morning = 700.0 * math.exp(-0.5 * ((hour - 8.0) / 1.5) ** 2)
    midday = 300.0 * math.exp(-0.5 * ((hour - 13.0) / 2.5) ** 2)
    evening = 1050.0 * math.exp(-0.5 * ((hour - 20.0) / 2.0) ** 2)
    return base + morning + midday + evening


PROFILE = np.array([daily_profile(item) for item in TIMES])


def build_series(name: str, rng: np.random.Generator) -> np.ndarray:
    count = len(TIMES)

    if name == "stable":
        values = 800.0 + rng.normal(0.0, 140.0, count)

    elif name == "day_night":
        values = PROFILE + rng.normal(0.0, 120.0, count)

    elif name == "ar_moderate":
        phi = 0.55
        target_sd = 220.0
        innovation_sd = target_sd * math.sqrt(1.0 - phi**2)
        innovations = rng.normal(0.0, innovation_sd, count)
        residual = lfilter([1.0], [1.0, -phi], innovations)
        values = PROFILE + residual

    elif name == "ar_high":
        phi = 0.93
        target_sd = 300.0
        innovation_sd = target_sd * math.sqrt(1.0 - phi**2)
        innovations = rng.normal(0.0, innovation_sd, count)
        residual = lfilter([1.0], [1.0, -phi], innovations)
        values = PROFILE + residual

    else:
        raise ValueError(f"Unknown scenario: {name}")

    return np.clip(values, 20.0, None)


def full_energy_kwh(values: np.ndarray) -> float:
    return float(
        np.sum((values[:-1] + values[1:]) / 2.0 * DT_HOURS) / 1000.0
    )


def observed_energy_kwh(
    values: np.ndarray,
    keep_mask: np.ndarray,
    continuity_threshold_intervals: int = 3,
) -> float:
    indexes = np.flatnonzero(keep_mask)
    differences = np.diff(indexes)
    valid = differences <= continuity_threshold_intervals
    left = indexes[:-1][valid]
    right = indexes[1:][valid]
    hours = (right - left) * DT_HOURS

    return float(
        np.sum((values[left] + values[right]) / 2.0 * hours) / 1000.0
    )


def stable_seed(text: str) -> int:
    value = 2166136261
    for character in text:
        value ^= ord(character)
        value = (value * 16777619) & 0xFFFFFFFF
    return value & 0x7FFFFFFF


def candidate_pool(
    kept_indexes: np.ndarray,
    local_midpoint: datetime,
) -> np.ndarray:
    target_hour = local_midpoint.hour
    target_weekend = local_midpoint.weekday() >= 5

    candidate_hours = HOURS[kept_indexes]
    candidate_weekends = WEEKENDS[kept_indexes]

    strict = kept_indexes[
        (candidate_hours == target_hour)
        & (candidate_weekends == target_weekend)
    ]
    if len(strict) >= 12:
        return strict

    distance = np.minimum(
        np.abs(candidate_hours - target_hour),
        24 - np.abs(candidate_hours - target_hour),
    )
    nearby = kept_indexes[
        (distance <= 1)
        & (candidate_weekends == target_weekend)
    ]
    if len(nearby) >= 12:
        return nearby

    same_hour = kept_indexes[candidate_hours == target_hour]
    if len(same_hour) >= 8:
        return same_hour

    return kept_indexes


def analyze_current_method(
    values: np.ndarray,
    gap_start_index: int,
    gap_intervals: int,
    simulation_count: int,
    repetition_id: int,
) -> tuple[float, np.ndarray]:
    gap_end_index = gap_start_index + gap_intervals

    keep = np.ones(len(values), dtype=bool)
    keep[gap_start_index + 1 : gap_end_index] = False
    kept_indexes = np.flatnonzero(keep)

    observed = observed_energy_kwh(values, keep)

    seed_text = (
        f"phase1-{repetition_id}|"
        f"{TIMES[0].isoformat()}|{TIMES[-1].isoformat()}|"
        f"{METHOD_VERSION}"
    )
    rng = np.random.default_rng(stable_seed(seed_text))
    missing_kwh = np.zeros(simulation_count)

    for offset in range(gap_intervals):
        midpoint = TIMES[gap_start_index + offset] + timedelta(minutes=2.5)
        pool = candidate_pool(kept_indexes, midpoint)
        selected = pool[rng.integers(0, len(pool), size=simulation_count)]
        missing_kwh += values[selected] * DT_HOURS / 1000.0

    outcomes = np.sort(observed + missing_kwh)
    percentiles = np.quantile(
        outcomes,
        [0.05, 0.50, 0.95],
        method="linear",
    )
    return observed, percentiles


def wilson_interval(successes: int, total: int) -> tuple[float, float]:
    z = 1.959963984540054
    estimate = successes / total
    denominator = 1.0 + z * z / total
    center = (estimate + z * z / (2.0 * total)) / denominator
    half = (
        z
        * math.sqrt(
            estimate * (1.0 - estimate) / total
            + z * z / (4.0 * total * total)
        )
        / denominator
    )
    return center - half, center + half


def run_scenario(
    scenario: str,
    repetitions: int,
    simulation_count: int,
    gap_intervals: int,
    seed: int,
) -> AuditResult:
    master = np.random.default_rng(seed)
    covered = 0
    p50_errors: list[float] = []
    relative_missing_errors: list[float] = []
    true_missing_values: list[float] = []
    widths: list[float] = []

    for repetition in range(repetitions):
        values = build_series(
            scenario,
            np.random.default_rng(master.integers(0, 2**63 - 1)),
        )

        first_allowed = 2 * 24 * 12
        last_allowed = 12 * 24 * 12 - gap_intervals
        gap_start = int(master.integers(first_allowed, last_allowed))

        truth = full_energy_kwh(values)
        observed, percentiles = analyze_current_method(
            values,
            gap_start,
            gap_intervals,
            simulation_count,
            repetition,
        )
        p5, p50, p95 = percentiles

        if p5 <= truth <= p95:
            covered += 1

        error = p50 - truth
        true_missing = max(1e-9, truth - observed)
        estimated_missing = p50 - observed

        p50_errors.append(error)
        relative_missing_errors.append(
            (estimated_missing - true_missing) / true_missing * 100.0
        )
        true_missing_values.append(true_missing)
        widths.append(p95 - p5)

    ci_low, ci_high = wilson_interval(covered, repetitions)
    p50_error_array = np.asarray(p50_errors)
    relative_array = np.asarray(relative_missing_errors)
    true_missing_array = np.asarray(true_missing_values)
    width_array = np.asarray(widths)

    return AuditResult(
        scenario=scenario,
        repetitions=repetitions,
        simulation_count=simulation_count,
        gap_hours=gap_intervals / 12.0,
        coverage_percent=covered / repetitions * 100.0,
        coverage_ci95_low_percent=ci_low * 100.0,
        coverage_ci95_high_percent=ci_high * 100.0,
        p50_bias_kwh=float(np.mean(p50_error_array)),
        p50_mae_kwh=float(np.mean(np.abs(p50_error_array))),
        p50_rmse_kwh=float(np.sqrt(np.mean(p50_error_array**2))),
        p50_median_abs_percent_missing=float(
            np.median(np.abs(relative_array))
        ),
        median_true_missing_kwh=float(np.median(true_missing_array)),
        median_interval_width_kwh=float(np.median(width_array)),
        median_width_percent_true_missing=float(
            np.median(width_array / true_missing_array * 100.0)
        ),
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--simulation-count", type=int, default=2000)
    parser.add_argument("--repetitions", type=int, default=1000)
    parser.add_argument("--gap-hours", type=float, default=2.0)
    parser.add_argument(
        "--scenarios",
        nargs="+",
        default=["stable", "day_night", "ar_moderate", "ar_high"],
    )
    parser.add_argument("--seed", type=int, default=1234)
    args = parser.parse_args()

    gap_intervals = int(round(args.gap_hours * 12.0))
    if gap_intervals <= 0:
        raise SystemExit("gap-hours must be positive")

    print(
        "scenario,repetitions,simulation_count,gap_hours,"
        "coverage_pct,coverage_ci95_low_pct,coverage_ci95_high_pct,"
        "p50_bias_kwh,p50_mae_kwh,p50_rmse_kwh,"
        "p50_median_abs_pct_missing,median_true_missing_kwh,"
        "median_interval_width_kwh,median_width_pct_true_missing"
    )

    for scenario in args.scenarios:
        result = run_scenario(
            scenario,
            args.repetitions,
            args.simulation_count,
            gap_intervals,
            args.seed,
        )
        print(
            f"{result.scenario},"
            f"{result.repetitions},"
            f"{result.simulation_count},"
            f"{result.gap_hours:.3f},"
            f"{result.coverage_percent:.4f},"
            f"{result.coverage_ci95_low_percent:.4f},"
            f"{result.coverage_ci95_high_percent:.4f},"
            f"{result.p50_bias_kwh:.6f},"
            f"{result.p50_mae_kwh:.6f},"
            f"{result.p50_rmse_kwh:.6f},"
            f"{result.p50_median_abs_percent_missing:.4f},"
            f"{result.median_true_missing_kwh:.6f},"
            f"{result.median_interval_width_kwh:.6f},"
            f"{result.median_width_percent_true_missing:.4f}"
        )


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Frozen Phase 2R R3 future-validation harness.

Implements PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md.
The count stage is outcome-blind: it validates Research Exporter packages,
constructs future targets, checks structural/calibration eligibility, and
evaluates the frozen stopping rule without computing target truth, coverage,
errors, P50, interval score, or any Enel/utility comparison.

The score stage is intentionally gated. It runs only when the stopping rule
has been met and scores only through the first local date on which it became met.
No Enel bill, meter reading, tariff, or utility value is loaded by this program.
"""
from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import math
import platform
import sys
import zipfile
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np
import pandas as pd

HARNESS_VERSION = "phase2r-r3-future-validation.v1"
PROTOCOL_FILE = "PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md"
CANDIDATE = "grid-import-boundary-residual-dayweighted-240m.candidate-v4"
FUTURE_START = date(2026, 9, 28)
ACTIVE_WATTS = 100.0
REGULAR_MIN = 4.5
REGULAR_MAX = 5.5
MIN_DATE_SAMPLES = 280
MIN_CALIBRATION_DAYS = 10
MAX_CALIBRATION_DAYS = 15
ALPHA = 0.10
CLUSTER_BOOTSTRAP_REPS = 5000
C2_SIMULATIONS = 2000
DEFAULT_MC_SIMULATIONS = 100_000
FROZEN_MC_SIMULATIONS = 100_000
COVERAGE_EPS_KWH = 1e-9
DURATIONS_MIN = (20, 30, 45, 60, 90, 120, 180, 240)
BENCHMARK_DURATIONS = (20, 30, 60, 120, 240)
TIME_BANDS = ((0, 6), (6, 12), (12, 18), (18, 24))


@dataclass(frozen=True)
class Target:
    target_id: str
    local_date: str
    start_index: int
    end_index: int
    nominal_duration_min: int
    actual_duration_hours: float
    start_watts: float
    end_watts: float
    start_utc: pd.Timestamp
    end_utc: pd.Timestamp
    start_local: pd.Timestamp
    end_local: pd.Timestamp
    time_band: str
    is_weekend: bool
    start_state: str
    duration_group: str


@dataclass(frozen=True)
class PackageInfo:
    path: str
    sha256: str
    timezone: str
    manifest_file_count: int
    min_grid_utc: str | None
    max_grid_utc: str | None
    grid_rows: int


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def seed64(*parts: object) -> int:
    raw = "|".join(str(p) for p in parts).encode("utf-8")
    return int.from_bytes(hashlib.sha256(raw).digest()[:8], "big", signed=False)


def is_covered(lower: float, upper: float, truth: float) -> bool:
    return (lower - COVERAGE_EPS_KWH) <= truth <= (upper + COVERAGE_EPS_KWH)


def interval_score(lower: float, upper: float, truth: float, alpha: float = ALPHA) -> float:
    score = upper - lower
    if truth < lower - COVERAGE_EPS_KWH:
        score += (2.0 / alpha) * (lower - truth)
    elif truth > upper + COVERAGE_EPS_KWH:
        score += (2.0 / alpha) * (truth - upper)
    return float(score)


def json_ready(value):
    if isinstance(value, dict):
        return {str(k): json_ready(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [json_ready(v) for v in value]
    if isinstance(value, np.generic):
        value = value.item()
    if isinstance(value, float) and not math.isfinite(value):
        return None
    return value


def duration_group(duration_min: int) -> str:
    if 15 < duration_min <= 45:
        return "G20_30"
    if 45 < duration_min <= 180:
        return "G60_120"
    if 180 < duration_min <= 240:
        return "G240"
    raise ValueError(f"duration outside R3 scope: {duration_min}")


def benchmark_group(duration_min: int) -> str:
    if duration_min in (20, 30):
        return "G20_30"
    if duration_min in (60, 120):
        return "G60_120"
    if duration_min == 240:
        return "G240"
    raise ValueError(f"not an R3 calibration benchmark duration: {duration_min}")


def start_state(watts: float) -> str:
    return "ACTIVE" if watts > ACTIVE_WATTS else "INACTIVE"


def boundary_bridge_kwh(x0_w: float, x1_w: float, hours: float) -> float:
    return max(0.0, (x0_w + x1_w) / 2.0) * hours / 1000.0


def weighted_empirical_quantile(values: np.ndarray, weights: np.ndarray, q: float) -> float:
    """Inverse weighted empirical CDF: smallest value whose CDF reaches q."""
    if not 0.0 <= q <= 1.0:
        raise ValueError("q must lie in [0,1]")
    values = np.asarray(values, dtype=float)
    weights = np.asarray(weights, dtype=float)
    if len(values) == 0 or len(values) != len(weights):
        raise ValueError("values/weights must be non-empty and equal length")
    if not np.all(np.isfinite(values)) or not np.all(np.isfinite(weights)):
        raise ValueError("weighted quantile inputs must be finite")
    if np.any(weights < 0) or float(weights.sum()) <= 0:
        raise ValueError("weights must be non-negative with positive total")
    order = np.argsort(values, kind="mergesort")
    v = values[order]
    w = weights[order]
    cdf = np.cumsum(w) / float(w.sum())
    idx = int(np.searchsorted(cdf, q, side="left"))
    return float(v[min(idx, len(v) - 1)])


def percentile_triplet(values: np.ndarray) -> tuple[float, float, float]:
    q = np.quantile(values, [0.05, 0.50, 0.95], method="linear")
    return float(q[0]), float(q[1]), float(q[2])


def _manifest_entries(manifest: dict) -> list[dict]:
    files = manifest.get("files")
    if not isinstance(files, list) or not files:
        raise RuntimeError("manifest.json has no non-empty files array")
    return files


def _entry_field(entry: dict, *names: str):
    for name in names:
        if name in entry:
            return entry[name]
    raise KeyError(f"manifest entry missing one of: {names}")


def validate_and_read_package(path: Path, package_order: int) -> tuple[pd.DataFrame, dict, PackageInfo]:
    package_sha = sha256_file(path)
    with zipfile.ZipFile(path) as z:
        if "manifest.json" not in z.namelist():
            raise RuntimeError(f"{path}: manifest.json missing")
        manifest = json.loads(z.read("manifest.json"))
        entries = _manifest_entries(manifest)
        names = set(z.namelist())
        declared_names: list[str] = []
        for entry in entries:
            name = str(_entry_field(entry, "Name", "name"))
            declared_names.append(name)
            if name not in names:
                raise RuntimeError(f"{path}: declared file missing from ZIP: {name}")
            raw = z.read(name)
            expected_bytes = int(_entry_field(entry, "Bytes", "bytes"))
            expected_sha = str(_entry_field(entry, "Sha256", "sha256")).lower()
            if len(raw) != expected_bytes:
                raise RuntimeError(f"{path}: manifest length mismatch: {name}")
            actual_sha = hashlib.sha256(raw).hexdigest()
            if actual_sha != expected_sha:
                raise RuntimeError(f"{path}: manifest SHA-256 mismatch: {name}")
        if "normalized_metrics.csv" not in declared_names:
            raise RuntimeError(f"{path}: normalized_metrics.csv not declared in manifest")
        usecols = [
            "recorded_at_utc",
            "metric_key",
            "normalized_value",
            "confidence",
            "quality",
        ]
        metrics = pd.read_csv(z.open("normalized_metrics.csv"), usecols=usecols)

    grid = metrics[
        (metrics["metric_key"] == "grid_import_power_w")
        & (metrics["confidence"] != "UNRESOLVED")
    ].copy()
    grid["normalized_value"] = pd.to_numeric(grid["normalized_value"], errors="coerce")
    grid = grid[np.isfinite(grid["normalized_value"])].copy()
    grid["ts_utc"] = pd.to_datetime(grid["recorded_at_utc"], utc=True)
    grid["_package_order"] = package_order

    timezone = manifest.get("timezone") or "America/Santiago"
    info = PackageInfo(
        path=str(path),
        sha256=package_sha,
        timezone=str(timezone),
        manifest_file_count=len(entries),
        min_grid_utc=None if grid.empty else grid["ts_utc"].min().isoformat(),
        max_grid_utc=None if grid.empty else grid["ts_utc"].max().isoformat(),
        grid_rows=int(len(grid)),
    )
    return grid, manifest, info


def load_packages(paths: Sequence[Path]) -> tuple[pd.DataFrame, list[PackageInfo], str]:
    if not paths:
        raise RuntimeError("at least one Research Exporter ZIP is required")
    frames: list[pd.DataFrame] = []
    infos: list[PackageInfo] = []
    timezones: set[str] = set()
    for i, path in enumerate(paths):
        frame, _manifest, info = validate_and_read_package(path, i)
        frames.append(frame)
        infos.append(info)
        timezones.add(info.timezone)
    if len(timezones) != 1:
        raise RuntimeError(f"package timezone mismatch: {sorted(timezones)}")
    timezone = next(iter(timezones))
    grid = pd.concat(frames, ignore_index=True)
    if grid.empty:
        raise RuntimeError("no valid grid_import_power_w rows found")
    grid = (
        grid.sort_values(["ts_utc", "_package_order"])
        .drop_duplicates("ts_utc", keep="last")
        .sort_values("ts_utc")
        .reset_index(drop=True)
    )
    grid["ts_local"] = grid["ts_utc"].dt.tz_convert(timezone)
    grid["local_date"] = grid["ts_local"].dt.date.astype(str)
    return grid, infos, timezone


def build_arrays(grid: pd.DataFrame, with_energy: bool) -> dict[str, np.ndarray]:
    ts_ns = grid["ts_utc"].astype("int64").to_numpy()
    values = grid["normalized_value"].to_numpy(dtype=float)
    local = grid["ts_local"]
    link_minutes = np.diff(ts_ns) / 60e9
    regular = (link_minutes >= REGULAR_MIN) & (link_minutes <= REGULAR_MAX)
    result: dict[str, np.ndarray] = {
        "ts_ns": ts_ns,
        "values": values,
        "regular": regular,
        "hour": local.dt.hour.to_numpy(dtype=np.int16),
        "weekend": (local.dt.dayofweek.to_numpy() >= 5),
        "minute_of_day": (
            local.dt.hour.to_numpy(dtype=float) * 60.0
            + local.dt.minute.to_numpy(dtype=float)
            + local.dt.second.to_numpy(dtype=float) / 60.0
            + local.dt.microsecond.to_numpy(dtype=float) / 60_000_000.0
        ),
    }
    if with_energy:
        link_hours = link_minutes / 60.0
        link_energy = (
            np.maximum(0.0, (values[:-1] + values[1:]) / 2.0)
            * link_hours
            / 1000.0
        )
        result["prefix_energy"] = np.concatenate([[0.0], np.cumsum(link_energy)])
    return result


def _window_fits_band(start_local: pd.Timestamp, end_local: pd.Timestamp, lo: int, hi: int) -> bool:
    h0 = start_local.hour + start_local.minute / 60.0 + start_local.second / 3600.0
    h1 = end_local.hour + end_local.minute / 60.0 + end_local.second / 3600.0
    if not (lo <= h0 < hi):
        return False
    if h1 > hi:
        return False
    if h1 == hi and (end_local.minute or end_local.second or end_local.microsecond):
        return False
    return True


def construct_future_targets(grid: pd.DataFrame, arrays: dict[str, np.ndarray]) -> list[Target]:
    counts = grid.groupby("local_date").size()
    eligible_dates = set(counts[counts >= MIN_DATE_SAMPLES].index)
    regular = arrays["regular"]
    values = arrays["values"]
    targets: list[Target] = []
    by_date = grid.groupby("local_date", sort=True).indices

    for duration in DURATIONS_MIN:
        k = duration // 5
        for local_date, raw_idxs in by_date.items():
            if local_date not in eligible_dates or pd.Timestamp(local_date).date() < FUTURE_START:
                continue
            idxs = np.asarray(raw_idxs, dtype=int)
            for lo, hi in TIME_BANDS:
                chosen: tuple[int, int] | None = None
                for pos in range(0, len(idxs) - k):
                    s = int(idxs[pos])
                    e = int(idxs[pos + k])
                    if e != s + k:
                        continue
                    start_local = grid.at[s, "ts_local"]
                    end_local = grid.at[e, "ts_local"]
                    if str(end_local.date()) != local_date:
                        continue
                    if not _window_fits_band(start_local, end_local, lo, hi):
                        continue
                    if not bool(np.all(regular[s:e])):
                        continue
                    chosen = (s, e)
                    break
                if chosen is None:
                    continue
                s, e = chosen
                start_utc = grid.at[s, "ts_utc"]
                end_utc = grid.at[e, "ts_utc"]
                start_local = grid.at[s, "ts_local"]
                end_local = grid.at[e, "ts_local"]
                hours = (end_utc - start_utc).total_seconds() / 3600.0
                state = start_state(float(values[s]))
                targets.append(
                    Target(
                        target_id=f"{start_utc.isoformat()}|{duration}",
                        local_date=local_date,
                        start_index=s,
                        end_index=e,
                        nominal_duration_min=duration,
                        actual_duration_hours=hours,
                        start_watts=float(values[s]),
                        end_watts=float(values[e]),
                        start_utc=start_utc,
                        end_utc=end_utc,
                        start_local=start_local,
                        end_local=end_local,
                        time_band=f"{lo:02d}-{hi:02d}",
                        is_weekend=bool(start_local.dayofweek >= 5),
                        start_state=state,
                        duration_group=duration_group(duration),
                    )
                )
    return targets


def build_calibration_windows(
    grid: pd.DataFrame,
    arrays: dict[str, np.ndarray],
    with_residuals: bool,
) -> pd.DataFrame:
    counts = grid.groupby("local_date").size()
    eligible_dates = set(counts[counts >= MIN_DATE_SAMPLES].index)
    regular = arrays["regular"]
    values = arrays["values"]
    prefix = arrays.get("prefix_energy")
    rows: list[dict] = []
    by_date = grid.groupby("local_date", sort=True).indices

    for local_date, raw_idxs in by_date.items():
        if local_date not in eligible_dates:
            continue
        idxs = np.asarray(raw_idxs, dtype=int)
        for duration in BENCHMARK_DURATIONS:
            k = duration // 5
            group = benchmark_group(duration)
            for pos in range(0, len(idxs) - k):
                s = int(idxs[pos])
                e = int(idxs[pos + k])
                if e != s + k:
                    continue
                if not bool(np.all(regular[s:e])):
                    continue
                if grid.at[e, "local_date"] != local_date:
                    continue
                start_utc = grid.at[s, "ts_utc"]
                end_utc = grid.at[e, "ts_utc"]
                hours = (end_utc - start_utc).total_seconds() / 3600.0
                row = {
                    "local_date": local_date,
                    "start_index": s,
                    "end_index": e,
                    "benchmark_duration_min": duration,
                    "duration_group": group,
                    "start_state": start_state(float(values[s])),
                    "actual_duration_hours": hours,
                }
                if with_residuals:
                    if prefix is None:
                        raise RuntimeError("prefix energy required for residual construction")
                    bridge = boundary_bridge_kwh(float(values[s]), float(values[e]), hours)
                    truth = float(prefix[e] - prefix[s])
                    row["residual_kw"] = (truth - bridge) / hours
                rows.append(row)
    if not rows:
        return pd.DataFrame(
            columns=[
                "local_date",
                "start_index",
                "end_index",
                "benchmark_duration_min",
                "duration_group",
                "start_state",
                "actual_duration_hours",
                "residual_kw",
            ]
        )
    return pd.DataFrame(rows)


def calibration_dates_for_target(calibration: pd.DataFrame, target: Target) -> list[str]:
    if calibration.empty:
        return []
    mask = (
        (calibration["start_state"] == target.start_state)
        & (calibration["duration_group"] == target.duration_group)
        & (calibration["local_date"] < target.local_date)
    )
    dates = sorted(calibration.loc[mask, "local_date"].unique())
    return dates[-MAX_CALIBRATION_DAYS:]


def equal_day_weighted_pool(calibration: pd.DataFrame, target: Target) -> tuple[np.ndarray, np.ndarray, list[str]]:
    dates = calibration_dates_for_target(calibration, target)
    if len(dates) < MIN_CALIBRATION_DAYS:
        raise RuntimeError(f"insufficient calibration dates for {target.target_id}: {len(dates)}")
    pool = calibration[
        (calibration["start_state"] == target.start_state)
        & (calibration["duration_group"] == target.duration_group)
        & (calibration["local_date"].isin(dates))
    ].copy()
    if "residual_kw" not in pool.columns:
        raise RuntimeError("residual_kw missing from scoring calibration pool")
    n_days = len(dates)
    per_day = pool.groupby("local_date").size().to_dict()
    weights = np.array(
        [1.0 / (n_days * per_day[d]) for d in pool["local_date"]], dtype=float
    )
    return pool["residual_kw"].to_numpy(dtype=float), weights, dates


def build_eligibility_frame(targets: Sequence[Target], calibration: pd.DataFrame) -> pd.DataFrame:
    rows: list[dict] = []
    for t in targets:
        dates = calibration_dates_for_target(calibration, t)
        status = "OK" if len(dates) >= MIN_CALIBRATION_DAYS else "INSUFFICIENT_CALIBRATION_DAYS"
        rows.append(
            {
                "target_id": t.target_id,
                "local_date": t.local_date,
                "start_utc": t.start_utc.isoformat(),
                "end_utc": t.end_utc.isoformat(),
                "nominal_duration_min": t.nominal_duration_min,
                "actual_duration_min": t.actual_duration_hours * 60.0,
                "time_band": t.time_band,
                "is_weekend": t.is_weekend,
                "start_state": t.start_state,
                "duration_group": t.duration_group,
                "start_watts": t.start_watts,
                "end_watts": t.end_watts,
                "calibration_dates": len(dates),
                "selected_calibration_dates": "|".join(dates),
                "r3_status": status,
            }
        )
    return pd.DataFrame(rows)


def stopping_rule(eligibility: pd.DataFrame) -> dict:
    if eligibility.empty:
        return {
            "status": "INSUFFICIENT",
            "stopping_rule_met": False,
            "stopping_date": None,
            "active_eligible_dates": 0,
            "active_eligible_cases": 0,
            "active_group_cases": {g: 0 for g in ("G20_30", "G60_120", "G240")},
        }
    ok = eligibility[eligibility["r3_status"] == "OK"].copy()
    dates = sorted(eligibility["local_date"].unique())
    stop_date: str | None = None
    snapshot: dict | None = None
    for d in dates:
        cur = ok[ok["local_date"] <= d]
        active = cur[cur["start_state"] == "ACTIVE"]
        active_dates = int(active["local_date"].nunique())
        active_cases = int(len(active))
        group_counts = {
            g: int((active["duration_group"] == g).sum())
            for g in ("G20_30", "G60_120", "G240")
        }
        met = (
            active_dates >= 15
            and active_cases >= 100
            and all(group_counts[g] >= 10 for g in group_counts)
        )
        snapshot = {
            "status": "READY" if met else "INSUFFICIENT",
            "stopping_rule_met": bool(met),
            "stopping_date": d if met else None,
            "active_eligible_dates": active_dates,
            "active_eligible_cases": active_cases,
            "active_group_cases": group_counts,
        }
        if met:
            stop_date = d
            break
    if snapshot is None:
        raise AssertionError("stopping rule snapshot unexpectedly missing")
    if stop_date is None:
        snapshot["stopping_date"] = None
    return snapshot


def harness_sha256() -> str:
    try:
        return sha256_file(Path(__file__).resolve())
    except OSError:
        return "UNAVAILABLE"


def write_count_outputs(
    output_dir: Path,
    eligibility: pd.DataFrame,
    stop: dict,
    package_infos: Sequence[PackageInfo],
    timezone: str,
    grid: pd.DataFrame,
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    eligibility.to_csv(output_dir / "PHASE2R_R3_FUTURE_ELIGIBILITY.csv", index=False)
    structural_total = int(len(eligibility))
    insufficient = int((eligibility["r3_status"] != "OK").sum()) if structural_total else 0
    metadata = {
        "harness_version": HARNESS_VERSION,
        "harness_sha256": harness_sha256(),
        "protocol_file": PROTOCOL_FILE,
        "candidate": CANDIDATE,
        "stage": "count",
        "outcome_blind": True,
        "external_utility_inputs_used": False,
        "future_validation_start": FUTURE_START.isoformat(),
        "timezone": timezone,
        "grid_min_utc": grid["ts_utc"].min().isoformat(),
        "grid_max_utc": grid["ts_utc"].max().isoformat(),
        "grid_min_local_date": str(grid["ts_local"].min().date()),
        "grid_max_local_date": str(grid["ts_local"].max().date()),
        "packages": [asdict(x) for x in package_infos],
        "structural_future_targets": structural_total,
        "calibration_insufficient_targets": insufficient,
        "calibration_insufficiency_pct": None if structural_total == 0 else 100.0 * insufficient / structural_total,
        "stopping_rule": stop,
        "forbidden_outcomes_computed": [],
    }
    (output_dir / "PHASE2R_R3_FUTURE_COUNT_METADATA.json").write_text(
        json.dumps(json_ready(metadata), indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8"
    )


def precompute_valid_block_starts(arrays: dict[str, np.ndarray]) -> dict[int, np.ndarray]:
    regular = arrays["regular"]
    n = len(arrays["values"])
    bad = (~regular).astype(np.int32)
    bad_prefix = np.concatenate([[0], np.cumsum(bad)])
    chunk_sizes: set[int] = set()
    for duration in DURATIONS_MIN:
        remaining = duration // 5
        while remaining > 0:
            c = min(24, remaining)
            chunk_sizes.add(c)
            remaining -= c
    result: dict[int, np.ndarray] = {}
    for c in sorted(chunk_sizes):
        starts = np.arange(0, n - c, dtype=int)
        good = (bad_prefix[starts + c] - bad_prefix[starts]) == 0
        result[c] = starts[good]
    return result


def c2_predict(
    arrays: dict[str, np.ndarray],
    valid_starts: dict[int, np.ndarray],
    target: Target,
) -> tuple[float, float, float, int, int, str]:
    ts_ns = arrays["ts_ns"]
    minute = arrays["minute_of_day"]
    weekends = arrays["weekend"]
    hours = arrays["hour"]
    prefix = arrays["prefix_energy"]
    k_total = target.nominal_duration_min // 5
    hist_start_ns = (target.start_utc - pd.Timedelta(days=90)).value
    target_start_ns = target.start_utc.value
    rng = np.random.default_rng(
        seed64(
            "phase3-real-backtest.v2.1",
            "C2_120",
            target.start_utc.isoformat(),
            target.nominal_duration_min,
            C2_SIMULATIONS,
        )
    )
    totals = np.zeros(C2_SIMULATIONS, dtype=float)
    remaining = k_total
    consumed = 0
    first_level_candidates = 0
    distinct_days: set[str] = set()
    fallback_levels: list[str] = []

    while remaining > 0:
        c = min(24, remaining)
        starts_all = valid_starts[c]
        mask = (ts_ns[starts_all] >= hist_start_ns) & (ts_ns[starts_all + c] < target_start_ns)
        starts = starts_all[mask]
        if len(starts) == 0:
            raise RuntimeError(f"C2 has no valid donor blocks for {target.target_id}")

        frac = consumed / k_total
        chunk_start = target.start_utc + pd.Timedelta(hours=target.actual_duration_hours * frac)
        chunk_local = chunk_start.tz_convert(target.start_local.tz)
        target_minute = (
            chunk_local.hour * 60
            + chunk_local.minute
            + chunk_local.second / 60.0
            + chunk_local.microsecond / 60_000_000.0
        )
        diff = np.abs(minute[starts] - target_minute)
        clock_dist = np.minimum(diff, 1440.0 - diff)
        same_day_type = weekends[starts] == bool(chunk_local.dayofweek >= 5)

        level = "all"
        pool = starts
        for width, label in ((15.0, "pm15"), (30.0, "pm30"), (60.0, "pm60")):
            cand = starts[(clock_dist <= width) & same_day_type]
            if len(cand) >= 8:
                pool = cand
                level = label
                break
        else:
            cand = starts[(hours[starts] == chunk_local.hour) & same_day_type]
            if len(cand) >= 8:
                pool = cand
                level = "same_hour"

        if consumed == 0:
            first_level_candidates = int(len(pool))
            dts = pd.to_datetime(ts_ns[pool], utc=True).tz_convert(target.start_local.tz)
            distinct_days = set(str(x.date()) for x in dts)
        fallback_levels.append(level)

        block_energy = prefix[pool + c] - prefix[pool]
        block_hours = (ts_ns[pool + c] - ts_ns[pool]) / 3.6e12
        chunk_hours = target.actual_duration_hours * (c / k_total)
        scaled = block_energy * (chunk_hours / block_hours)
        draws = rng.integers(0, len(pool), size=C2_SIMULATIONS)
        totals += scaled[draws]

        consumed += c
        remaining -= c

    p5, p50, p95 = percentile_triplet(totals)
    return p5, p50, p95, first_level_candidates, len(distinct_days), "+".join(fallback_levels)


def clustered_coverage_ci(df: pd.DataFrame, covered_col: str, label: str) -> tuple[float, float, float]:
    d = df.dropna(subset=[covered_col]).copy()
    if d.empty:
        return math.nan, math.nan, math.nan
    point = float(d[covered_col].astype(float).mean())
    by_day = d.groupby("local_date")[covered_col].agg(["sum", "count"]).to_numpy(dtype=float)
    if len(by_day) <= 1:
        return point, point, point
    rng = np.random.default_rng(seed64(HARNESS_VERSION, "cluster", label, CLUSTER_BOOTSTRAP_REPS))
    reps = np.empty(CLUSTER_BOOTSTRAP_REPS, dtype=float)
    n = len(by_day)
    for i in range(CLUSTER_BOOTSTRAP_REPS):
        idx = rng.integers(0, n, size=n)
        s = by_day[idx].sum(axis=0)
        reps[i] = s[0] / s[1]
    lo, hi = np.quantile(reps, [0.025, 0.975], method="linear")
    return point, float(lo), float(hi)


def aggregate(df: pd.DataFrame, group_cols: Sequence[str]) -> pd.DataFrame:
    if group_cols:
        grouped: Iterable[tuple[object, pd.DataFrame]] = df.groupby(list(group_cols), dropna=False, sort=True)
    else:
        grouped = [((), df)]
    rows: list[dict] = []
    for keys, g in grouped:
        if not isinstance(keys, tuple):
            keys = (keys,)
        base = {col: key for col, key in zip(group_cols, keys)}
        cov, lo, hi = clustered_coverage_ci(g, "r3_covered", "|".join(map(str, keys)) or "overall")
        err = g["r3_p50"] - g["truth_kwh"]
        rows.append(
            {
                **base,
                "cases": int(len(g)),
                "distinct_dates": int(g["local_date"].nunique()),
                "coverage_pct": 100.0 * cov,
                "cluster_ci_low_pct": 100.0 * lo,
                "cluster_ci_high_pct": 100.0 * hi,
                "p50_bias_kwh": float(err.mean()),
                "p50_mae_kwh": float(np.abs(err).mean()),
                "p50_rmse_kwh": float(np.sqrt(np.mean(err**2))),
                "mean_width_kwh": float((g["r3_p95"] - g["r3_p5"]).mean()),
                "mean_interval_score": float(g["r3_interval_score"].mean()),
                "mean_calibration_dates": float(g["calibration_dates"].mean()),
            }
        )
    return pd.DataFrame(rows)


def _non_overlapping(targets: Sequence[Target]) -> bool:
    ordered = sorted(targets, key=lambda t: t.start_utc)
    return all(a.end_utc < b.start_utc for a, b in zip(ordered, ordered[1:]))


def _first_combo(candidates: dict[int, list[Target]], requirements: tuple[int, ...]) -> list[Target] | None:
    if len(set(requirements)) == 1:
        duration = requirements[0]
        need = len(requirements)
        pool = candidates.get(duration, [])
        for combo in itertools.combinations(pool, need):
            if _non_overlapping(combo):
                return sorted(combo, key=lambda t: t.start_utc)
        return None

    pools = [candidates.get(d, []) for d in requirements]
    if any(not p for p in pools):
        return None
    combos = itertools.product(*pools)
    valid: list[tuple[pd.Timestamp, ...] | tuple] = []
    best: list[Target] | None = None
    best_key: tuple | None = None
    for combo in combos:
        if len({t.target_id for t in combo}) != len(combo):
            continue
        if not _non_overlapping(combo):
            continue
        ordered = sorted(combo, key=lambda t: t.start_utc)
        key = tuple(t.start_utc for t in ordered) + tuple(t.nominal_duration_min for t in ordered)
        if best_key is None or key < best_key:
            best_key = key
            best = ordered
    return best


def construct_multi_scenarios(targets: Sequence[Target]) -> list[dict]:
    scenarios = {
        "4x60": (60, 60, 60, 60),
        "2x120": (120, 120),
        "20+30+60+120": (20, 30, 60, 120),
    }
    by_date: dict[str, list[Target]] = {}
    for t in targets:
        by_date.setdefault(t.local_date, []).append(t)
    rows: list[dict] = []
    for local_date in sorted(by_date):
        candidates: dict[int, list[Target]] = {}
        for t in sorted(by_date[local_date], key=lambda x: (x.nominal_duration_min, x.start_utc)):
            candidates.setdefault(t.nominal_duration_min, []).append(t)
        for scenario_name, req in scenarios.items():
            combo = _first_combo(candidates, req)
            if combo is None:
                continue
            rows.append(
                {
                    "scenario_id": f"{local_date}|{scenario_name}",
                    "local_date": local_date,
                    "scenario": scenario_name,
                    "targets": combo,
                }
            )
    return rows


def score_multi_scenarios(
    scenarios: Sequence[dict],
    scored_by_id: dict[str, dict],
    residual_pools: dict[str, tuple[np.ndarray, np.ndarray]],
    simulations: int,
) -> pd.DataFrame:
    rows: list[dict] = []
    for scenario in scenarios:
        rng = np.random.default_rng(seed64(HARNESS_VERSION, "multi", scenario["scenario_id"], simulations))
        totals = np.zeros(simulations, dtype=float)
        truth = 0.0
        component_ids: list[str] = []
        for t in scenario["targets"]:
            rec = scored_by_id[t.target_id]
            residuals, weights = residual_pools[t.target_id]
            idx = rng.choice(len(residuals), size=simulations, replace=True, p=weights / weights.sum())
            bridge = float(rec["bridge_kwh"])
            hours = float(rec["actual_duration_min"]) / 60.0
            totals += np.maximum(0.0, bridge + residuals[idx] * hours)
            truth += float(rec["truth_kwh"])
            component_ids.append(t.target_id)
        p5, p50, p95 = percentile_triplet(totals)
        rows.append(
            {
                "scenario_id": scenario["scenario_id"],
                "local_date": scenario["local_date"],
                "scenario": scenario["scenario"],
                "component_target_ids": "|".join(component_ids),
                "components": len(component_ids),
                "truth_kwh": truth,
                "r3_p5": p5,
                "r3_p50": p50,
                "r3_p95": p95,
                "r3_covered": is_covered(p5, p95, truth),
                "r3_interval_score": interval_score(p5, p95, truth),
                "r3_width_kwh": p95 - p5,
                "mc_simulations": simulations,
            }
        )
    return pd.DataFrame(rows)


def score_future(
    grid: pd.DataFrame,
    package_infos: Sequence[PackageInfo],
    timezone: str,
    targets: Sequence[Target],
    eligibility: pd.DataFrame,
    stop: dict,
    output_dir: Path,
    mc_simulations: int,
) -> dict:
    if not stop.get("stopping_rule_met"):
        raise RuntimeError("future stopping rule is not met; scoring is forbidden")
    if mc_simulations != FROZEN_MC_SIMULATIONS:
        raise RuntimeError(
            f"frozen future validation requires exactly {FROZEN_MC_SIMULATIONS} multiple-gap simulations"
        )
    stopping_date = str(stop["stopping_date"])
    score_lock = output_dir / "PHASE2R_R3_SCORE_ATTEMPT_LOCK.json"
    if score_lock.exists():
        raise RuntimeError(
            "score attempt lock already exists; do not rescore the frozen future validation casually"
        )
    output_dir.mkdir(parents=True, exist_ok=True)
    lock = {
        "harness_version": HARNESS_VERSION,
        "harness_sha256": harness_sha256(),
        "packages": [asdict(x) for x in package_infos],
        "stopping_date": stopping_date,
        "note": "Written before outcome scoring. If scoring fails because of a demonstrated harness bug, fix/version/document the bug instead of silently deleting this lock.",
    }
    score_lock.write_text(json.dumps(json_ready(lock), indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8")

    arrays = build_arrays(grid, with_energy=True)
    calibration = build_calibration_windows(grid, arrays, with_residuals=True)
    valid_starts = precompute_valid_block_starts(arrays)
    eligible_ids = set(
        eligibility[
            (eligibility["r3_status"] == "OK")
            & (eligibility["local_date"] <= stopping_date)
        ]["target_id"]
    )
    scoring_targets = [t for t in targets if t.target_id in eligible_ids]
    scoring_targets.sort(key=lambda t: (t.local_date, t.nominal_duration_min, t.start_utc))

    records: list[dict] = []
    residual_pools: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    for i, t in enumerate(scoring_targets, 1):
        residuals, weights, dates = equal_day_weighted_pool(calibration, t)
        q05 = weighted_empirical_quantile(residuals, weights, 0.05)
        q50 = weighted_empirical_quantile(residuals, weights, 0.50)
        q95 = weighted_empirical_quantile(residuals, weights, 0.95)
        bridge = boundary_bridge_kwh(t.start_watts, t.end_watts, t.actual_duration_hours)
        p5 = max(0.0, bridge + q05 * t.actual_duration_hours)
        p50 = max(0.0, bridge + q50 * t.actual_duration_hours)
        p95 = max(p5, bridge + q95 * t.actual_duration_hours)
        truth = float(arrays["prefix_energy"][t.end_index] - arrays["prefix_energy"][t.start_index])
        try:
            c2_p5, c2_p50, c2_p95, c2_starts, c2_days, c2_fallback = c2_predict(arrays, valid_starts, t)
            c2_status = "OK"
            c2_score = interval_score(c2_p5, c2_p95, truth)
        except RuntimeError as exc:
            c2_p5 = c2_p50 = c2_p95 = math.nan
            c2_starts = c2_days = 0
            c2_fallback = str(exc)
            c2_status = "INSUFFICIENT"
            c2_score = math.nan
        rec = {
            "target_id": t.target_id,
            "local_date": t.local_date,
            "start_utc": t.start_utc.isoformat(),
            "end_utc": t.end_utc.isoformat(),
            "nominal_duration_min": t.nominal_duration_min,
            "actual_duration_min": t.actual_duration_hours * 60.0,
            "duration_group": t.duration_group,
            "time_band": t.time_band,
            "is_weekend": t.is_weekend,
            "start_state": t.start_state,
            "start_watts": t.start_watts,
            "end_watts": t.end_watts,
            "calibration_dates": len(dates),
            "selected_calibration_dates": "|".join(dates),
            "calibration_windows": len(residuals),
            "bridge_kwh": bridge,
            "residual_q05_kw": q05,
            "residual_q50_kw": q50,
            "residual_q95_kw": q95,
            "truth_kwh": truth,
            "r3_p5": p5,
            "r3_p50": p50,
            "r3_p95": p95,
            "r3_covered": is_covered(p5, p95, truth),
            "r3_interval_score": interval_score(p5, p95, truth),
            "r3_width_kwh": p95 - p5,
            "c2_status": c2_status,
            "c2_p5": c2_p5,
            "c2_p50": c2_p50,
            "c2_p95": c2_p95,
            "c2_covered": is_covered(c2_p5, c2_p95, truth) if c2_status == "OK" else np.nan,
            "c2_interval_score": c2_score,
            "c2_contextual_starts_first_chunk": c2_starts,
            "c2_distinct_donor_days_first_chunk": c2_days,
            "c2_fallback_levels": c2_fallback,
        }
        records.append(rec)
        residual_pools[t.target_id] = (residuals, weights)
        if i % 100 == 0 or i == len(scoring_targets):
            print(f"scored {i}/{len(scoring_targets)} single-gap targets", flush=True)

    cases = pd.DataFrame(records)
    cases.to_csv(output_dir / "PHASE2R_R3_FUTURE_PER_CASE.csv", index=False)
    overall = aggregate(cases, [])
    state_agg = aggregate(cases, ["start_state"])
    group_agg = aggregate(cases, ["duration_group"])
    duration_agg = aggregate(cases, ["nominal_duration_min"])
    daytype_agg = aggregate(cases, ["is_weekend"])
    band_agg = aggregate(cases, ["time_band"])
    overall.to_csv(output_dir / "PHASE2R_R3_FUTURE_OVERALL.csv", index=False)
    state_agg.to_csv(output_dir / "PHASE2R_R3_FUTURE_START_STATE.csv", index=False)
    group_agg.to_csv(output_dir / "PHASE2R_R3_FUTURE_DURATION_GROUP.csv", index=False)
    duration_agg.to_csv(output_dir / "PHASE2R_R3_FUTURE_DURATION.csv", index=False)
    daytype_agg.to_csv(output_dir / "PHASE2R_R3_FUTURE_DAYTYPE.csv", index=False)
    band_agg.to_csv(output_dir / "PHASE2R_R3_FUTURE_TIME_BAND.csv", index=False)

    target_by_id = {t.target_id: t for t in scoring_targets}
    scenarios = construct_multi_scenarios(list(target_by_id.values()))
    scored_by_id = {r["target_id"]: r for r in records}
    multi = score_multi_scenarios(scenarios, scored_by_id, residual_pools, mc_simulations)
    multi.to_csv(output_dir / "PHASE2R_R3_FUTURE_MULTI_GAP.csv", index=False)

    overall_cov = float(cases["r3_covered"].mean())
    active = cases[cases["start_state"] == "ACTIVE"]
    inactive = cases[cases["start_state"] == "INACTIVE"]
    active_cov = float(active["r3_covered"].mean()) if len(active) else math.nan
    inactive_cov = float(inactive["r3_covered"].mean()) if len(inactive) else math.nan
    active_ci = clustered_coverage_ci(active, "r3_covered", "gate|ACTIVE")
    inactive_ci = clustered_coverage_ci(inactive, "r3_covered", "gate|INACTIVE")

    group_cov = {
        str(r.duration_group): (int(r.cases), float(r.coverage_pct) / 100.0)
        for r in group_agg.itertuples()
    }
    c2_complete = bool((cases["c2_status"] == "OK").all())
    r3_score = float(cases["r3_interval_score"].mean())
    c2_score = float(cases["c2_interval_score"].mean()) if c2_complete else math.nan
    r3_active_bias = float((active["r3_p50"] - active["truth_kwh"]).mean()) if len(active) else math.nan
    c2_active_bias = float((active["c2_p50"] - active["truth_kwh"]).mean()) if len(active) and c2_complete else math.nan

    validation_elig = eligibility[eligibility["local_date"] <= stopping_date]
    insuff_rate = float((validation_elig["r3_status"] != "OK").mean()) if len(validation_elig) else math.nan
    multi_cov = float(multi["r3_covered"].mean()) if len(multi) else math.nan

    gates = {
        "validation_start": FUTURE_START.isoformat(),
        "validation_stop_date": stopping_date,
        "single_gap_cases": int(len(cases)),
        "multiple_gap_cases": int(len(multi)),
        "overall_coverage": overall_cov,
        "active_coverage": active_cov,
        "inactive_coverage": inactive_cov,
        "active_cluster_ci_95": [active_ci[1], active_ci[2]],
        "inactive_cluster_ci_95": [inactive_ci[1], inactive_ci[2]],
        "duration_group_coverage": group_cov,
        "r3_mean_interval_score": r3_score,
        "raw_c2_mean_interval_score": c2_score,
        "r3_active_p50_bias_kwh": r3_active_bias,
        "raw_c2_active_p50_bias_kwh": c2_active_bias,
        "multiple_gap_coverage": multi_cov,
        "calibration_insufficiency_rate": insuff_rate,
        "raw_c2_complete_same_cases": c2_complete,
    }
    gates["gate1_overall_coverage_ge85"] = overall_cov >= 0.85
    gates["gate2_active_coverage_ge85"] = bool(len(active)) and active_cov >= 0.85
    gates["gate3_inactive_coverage_ge85"] = bool(len(inactive)) and inactive_cov >= 0.85
    gates["gate4_no_main_duration_group_ge10_below80"] = all(
        cases_n < 10 or cov >= 0.80 for cases_n, cov in group_cov.values()
    )
    gates["gate5_no_primary_state_cluster_upper_below90"] = (
        bool(len(active))
        and bool(len(inactive))
        and active_ci[2] >= 0.90
        and inactive_ci[2] >= 0.90
    )
    gates["gate6_interval_score_le_raw_c2"] = c2_complete and r3_score <= c2_score
    gates["gate7_active_abs_p50_bias_lt_raw_c2"] = (
        c2_complete
        and bool(len(active))
        and abs(r3_active_bias) < abs(c2_active_bias)
    )
    gates["gate8_multi_gap_coverage_ge85_when_ge10"] = (
        True if len(multi) < 10 else multi_cov >= 0.85
    )
    gates["gate8_applicable"] = len(multi) >= 10
    gates["gate9_calibration_insufficiency_le5"] = insuff_rate <= 0.05
    gates["gate10_no_new_structural_failure"] = "REQUIRES_REVIEW"

    quantitative_keys = [
        "gate1_overall_coverage_ge85",
        "gate2_active_coverage_ge85",
        "gate3_inactive_coverage_ge85",
        "gate4_no_main_duration_group_ge10_below80",
        "gate5_no_primary_state_cluster_upper_below90",
        "gate6_interval_score_le_raw_c2",
        "gate7_active_abs_p50_bias_lt_raw_c2",
        "gate8_multi_gap_coverage_ge85_when_ge10",
        "gate9_calibration_insufficiency_le5",
    ]
    quantitative_pass = all(bool(gates[k]) for k in quantitative_keys)
    gates["quantitative_gates_1_to_9_pass"] = quantitative_pass
    gates["decision"] = "PENDING_STRUCTURAL_REVIEW" if quantitative_pass else "REJECTED_RETURN_TO_RESEARCH"

    (output_dir / "PHASE2R_R3_FUTURE_GATES.json").write_text(
        json.dumps(json_ready(gates), indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8"
    )
    score_metadata = {
        "harness_version": HARNESS_VERSION,
        "harness_sha256": harness_sha256(),
        "protocol_file": PROTOCOL_FILE,
        "candidate": CANDIDATE,
        "stage": "score",
        "external_utility_inputs_used": False,
        "packages": [asdict(x) for x in package_infos],
        "timezone": timezone,
        "stopping_rule": stop,
        "scored_through_first_stopping_date_only": True,
        "multiple_gap_simulations": mc_simulations,
        "c2_simulations": C2_SIMULATIONS,
        "cluster_bootstrap_reps": CLUSTER_BOOTSTRAP_REPS,
        "weighted_quantile_definition": "inverse weighted empirical CDF",
        "multi_gap_target_selection": "lexicographically earliest non-overlapping combination from frozen per-band single-gap targets",
        "coverage_numeric_epsilon_kwh": COVERAGE_EPS_KWH,
        "gates": gates,
        "python": sys.version,
        "platform": platform.platform(),
        "numpy": np.__version__,
        "pandas": pd.__version__,
    }
    (output_dir / "PHASE2R_R3_FUTURE_SCORE_METADATA.json").write_text(
        json.dumps(json_ready(score_metadata), indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8"
    )
    return gates


def prepare_count(packages: Sequence[Path], output_dir: Path) -> tuple[
    pd.DataFrame,
    list[PackageInfo],
    str,
    list[Target],
    pd.DataFrame,
    dict,
]:
    grid, package_infos, timezone = load_packages(packages)
    arrays = build_arrays(grid, with_energy=False)
    targets = construct_future_targets(grid, arrays)
    calibration = build_calibration_windows(grid, arrays, with_residuals=False)
    eligibility = build_eligibility_frame(targets, calibration)
    stop = stopping_rule(eligibility)
    write_count_outputs(output_dir, eligibility, stop, package_infos, timezone, grid)
    return grid, package_infos, timezone, targets, eligibility, stop


def run_self_test() -> None:
    assert duration_group(20) == "G20_30"
    assert duration_group(45) == "G20_30"
    assert duration_group(60) == "G60_120"
    assert duration_group(180) == "G60_120"
    assert duration_group(240) == "G240"
    assert math.isclose(boundary_bridge_kwh(1000.0, 1000.0, 1.0), 1.0)
    v = np.array([1.0, 2.0, 3.0])
    w = np.array([0.2, 0.6, 0.2])
    assert weighted_empirical_quantile(v, w, 0.50) == 2.0
    assert weighted_empirical_quantile(v, w, 0.05) == 1.0
    assert weighted_empirical_quantile(v, w, 0.95) == 3.0
    assert math.isclose(interval_score(1.0, 3.0, 2.0), 2.0)
    assert interval_score(1.0, 3.0, 4.0) > 2.0
    assert seed64("a", 1) == seed64("a", 1)
    print("SELF_TEST_PASS")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", choices=["count", "score", "self-test"], required=True)
    ap.add_argument(
        "--package",
        dest="packages",
        action="append",
        type=Path,
        help="Research Exporter ZIP. Repeat to merge validated historical + future packages.",
    )
    ap.add_argument("--output-dir", type=Path, default=Path("phase2r_r3_output"))
    ap.add_argument(
        "--mc-simulations",
        type=int,
        default=DEFAULT_MC_SIMULATIONS,
        help="Multiple-gap simulation count; frozen official scoring requires 100000.",
    )
    args = ap.parse_args()

    if args.stage == "self-test":
        run_self_test()
        return
    if not args.packages:
        raise SystemExit("at least one --package is required for count/score")

    grid, infos, timezone, targets, eligibility, stop = prepare_count(args.packages, args.output_dir)
    print(json.dumps(json_ready(stop), indent=2, ensure_ascii=False, allow_nan=False))
    if args.stage == "count":
        return
    gates = score_future(
        grid,
        infos,
        timezone,
        targets,
        eligibility,
        stop,
        args.output_dir,
        args.mc_simulations,
    )
    print(json.dumps(json_ready(gates), indent=2, ensure_ascii=False, allow_nan=False))


if __name__ == "__main__":
    main()

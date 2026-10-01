#!/usr/bin/env python3
"""Canonical reproducible Phase 3 v2 real-data backtest.

Implements PHASE3_CANONICAL_REPRODUCIBLE_RERUN_PROTOCOL_2026-09-30.md.
No Enel/meter/tariff value is read or used.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import platform
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import pandas as pd

HARNESS_VERSION = "phase3-real-backtest.v2.1"
SIMULATION_COUNT = 2000
CLUSTER_BOOTSTRAP_REPS = 5000
DURATIONS_MIN = (20, 30, 60, 120, 240, 480)
REGULAR_MIN = 4.5
REGULAR_MAX = 5.5
ACTIVE_WATTS = 100.0
ALPHA = 0.10
EXPECTED_PACKAGE_SHA256 = "277c0d6a4707deb225562b567f349ddb658cbc4b1b72f53bc863e8f16f9f46ec"


@dataclass(frozen=True)
class Target:
    target_id: str
    local_date: str
    start_index: int
    end_index: int
    nominal_duration_min: int
    actual_duration_hours: float
    truth_kwh: float
    start_watts: float
    end_watts: float
    start_utc: pd.Timestamp
    end_utc: pd.Timestamp
    start_local: pd.Timestamp
    end_local: pd.Timestamp
    time_band: str
    is_weekend: bool


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def seed64(*parts: object) -> int:
    text = "|".join(str(p) for p in parts)
    digest = hashlib.sha256(text.encode("utf-8")).digest()
    return int.from_bytes(digest[:8], "big", signed=False)


def percentile_triplet(values: np.ndarray) -> tuple[float, float, float]:
    q = np.quantile(values, [0.05, 0.50, 0.95], method="linear")
    return float(q[0]), float(q[1]), float(q[2])


def interval_score(lower: float, upper: float, truth: float, alpha: float = ALPHA) -> float:
    score = upper - lower
    if truth < lower:
        score += (2.0 / alpha) * (lower - truth)
    elif truth > upper:
        score += (2.0 / alpha) * (truth - upper)
    return float(score)


def is_weekend_ts(ts: pd.Timestamp) -> bool:
    return ts.dayofweek >= 5


def load_package(package_path: Path) -> tuple[pd.DataFrame, dict, str]:
    package_sha = sha256_file(package_path)
    with zipfile.ZipFile(package_path) as z:
        manifest = json.loads(z.read("manifest.json"))
        declared = {item["Name"]: item for item in manifest["files"]}
        for name, meta in declared.items():
            raw = z.read(name)
            if len(raw) != int(meta["Bytes"]):
                raise RuntimeError(f"manifest length mismatch: {name}")
            actual = hashlib.sha256(raw).hexdigest()
            if actual != meta["Sha256"]:
                raise RuntimeError(f"manifest SHA-256 mismatch: {name}")

        usecols = [
            "recorded_at_utc", "metric_key", "normalized_value",
            "confidence", "quality"
        ]
        metrics = pd.read_csv(z.open("normalized_metrics.csv"), usecols=usecols)

    grid = metrics[
        (metrics["metric_key"] == "grid_import_power_w") &
        (metrics["confidence"] != "UNRESOLVED")
    ].copy()
    grid["normalized_value"] = pd.to_numeric(grid["normalized_value"], errors="coerce")
    grid = grid[np.isfinite(grid["normalized_value"])].copy()
    grid["ts_utc"] = pd.to_datetime(grid["recorded_at_utc"], utc=True)
    grid = grid.sort_values("ts_utc").drop_duplicates("ts_utc", keep="last").reset_index(drop=True)

    timezone = manifest.get("timezone") or "America/Santiago"
    grid["ts_local"] = grid["ts_utc"].dt.tz_convert(timezone)
    grid["local_date"] = grid["ts_local"].dt.date.astype(str)
    return grid, manifest, package_sha


def build_arrays(grid: pd.DataFrame) -> dict[str, np.ndarray]:
    ts_ns = grid["ts_utc"].astype("int64").to_numpy()
    local = grid["ts_local"]
    values = grid["normalized_value"].to_numpy(dtype=float)
    link_minutes = np.diff(ts_ns) / 60e9
    regular = (link_minutes >= REGULAR_MIN) & (link_minutes <= REGULAR_MAX)
    link_hours = link_minutes / 60.0
    link_energy = np.maximum(0.0, (values[:-1] + values[1:]) / 2.0) * link_hours / 1000.0
    prefix_energy = np.concatenate([[0.0], np.cumsum(link_energy)])
    minute_of_day = (
        local.dt.hour.to_numpy(dtype=float) * 60.0 +
        local.dt.minute.to_numpy(dtype=float) +
        local.dt.second.to_numpy(dtype=float) / 60.0 +
        local.dt.microsecond.to_numpy(dtype=float) / 60_000_000.0
    )
    weekend = (local.dt.dayofweek.to_numpy() >= 5)
    hour = local.dt.hour.to_numpy(dtype=np.int16)
    return {
        "ts_ns": ts_ns,
        "values": values,
        "regular": regular,
        "prefix_energy": prefix_energy,
        "minute_of_day": minute_of_day,
        "weekend": weekend,
        "hour": hour,
    }


def construct_targets(grid: pd.DataFrame, arrays: dict[str, np.ndarray]) -> list[Target]:
    counts = grid.groupby("local_date").size()
    eligible_dates = set(counts[counts >= 280].index)
    package_start = grid["ts_utc"].iloc[0]
    regular = arrays["regular"]
    prefix = arrays["prefix_energy"]
    values = arrays["values"]

    targets: list[Target] = []
    by_date = grid.groupby("local_date", sort=True).indices

    for duration in DURATIONS_MIN:
        k = duration // 5
        bands = ((0, 12), (12, 24)) if duration == 480 else ((0, 6), (6, 12), (12, 18), (18, 24))
        for local_date, idxs in by_date.items():
            if local_date not in eligible_dates:
                continue
            idxs = np.asarray(idxs, dtype=int)
            for lo, hi in bands:
                chosen: tuple[int, int] | None = None
                for pos in range(0, len(idxs) - k):
                    s = int(idxs[pos])
                    e = int(idxs[pos + k])
                    if e != s + k:
                        continue
                    start_local = grid.at[s, "ts_local"]
                    end_local = grid.at[e, "ts_local"]
                    h0 = start_local.hour + start_local.minute / 60.0 + start_local.second / 3600.0
                    h1 = end_local.hour + end_local.minute / 60.0 + end_local.second / 3600.0
                    if not (lo <= h0 < hi):
                        continue
                    if h1 > hi or (h1 == hi and (end_local.minute or end_local.second or end_local.microsecond)):
                        continue
                    if str(end_local.date()) != local_date:
                        continue
                    if not bool(np.all(regular[s:e])):
                        continue
                    start_utc = grid.at[s, "ts_utc"]
                    if start_utc - pd.Timedelta(days=90) < package_start:
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
                actual_hours = (end_utc - start_utc).total_seconds() / 3600.0
                truth = float(prefix[e] - prefix[s])
                target_id = f"{start_utc.isoformat()}|{duration}"
                targets.append(Target(
                    target_id=target_id,
                    local_date=local_date,
                    start_index=s,
                    end_index=e,
                    nominal_duration_min=duration,
                    actual_duration_hours=actual_hours,
                    truth_kwh=truth,
                    start_watts=float(values[s]),
                    end_watts=float(values[e]),
                    start_utc=start_utc,
                    end_utc=end_utc,
                    start_local=start_local,
                    end_local=end_local,
                    time_band=f"{lo:02d}-{hi:02d}",
                    is_weekend=is_weekend_ts(start_local),
                ))
    return targets


def history_bounds(arrays: dict[str, np.ndarray], target: Target) -> tuple[int, int]:
    ts_ns = arrays["ts_ns"]
    start_ns = target.start_utc.value
    hist_start_ns = (target.start_utc - pd.Timedelta(days=90)).value
    lo = int(np.searchsorted(ts_ns, hist_start_ns, side="left"))
    hi = int(np.searchsorted(ts_ns, start_ns, side="left"))
    return lo, hi


def c0_predict(arrays: dict[str, np.ndarray], target: Target) -> tuple[float,float,float,int]:
    values = arrays["values"]
    hours = arrays["hour"]
    weekends = arrays["weekend"]
    lo, hi = history_bounds(arrays, target)
    hist_idx = np.arange(lo, hi, dtype=int)
    hist_values = np.maximum(0.0, values[hist_idx])
    hist_hours = hours[hist_idx]
    hist_weekends = weekends[hist_idx]
    donor_count = int(len(hist_idx))
    k = target.nominal_duration_min // 5
    piece_hours = target.actual_duration_hours / k
    rng = np.random.default_rng(seed64(
        HARNESS_VERSION, "C0", target.start_utc.isoformat(),
        target.nominal_duration_min, SIMULATION_COUNT
    ))
    totals = np.zeros(SIMULATION_COUNT, dtype=float)

    for j in range(k):
        midpoint = target.start_utc + pd.Timedelta(hours=piece_hours * (j + 0.5))
        local = midpoint.tz_convert(target.start_local.tz)
        th = local.hour
        tw = is_weekend_ts(local)
        strict = np.flatnonzero((hist_hours == th) & (hist_weekends == tw))
        if len(strict) >= 12:
            pool = strict
        else:
            dist = np.minimum(np.abs(hist_hours - th), 24 - np.abs(hist_hours - th))
            near = np.flatnonzero((dist <= 1) & (hist_weekends == tw))
            if len(near) >= 12:
                pool = near
            else:
                same = np.flatnonzero(hist_hours == th)
                pool = same if len(same) >= 8 else np.arange(len(hist_idx))
        draws = rng.integers(0, len(pool), size=SIMULATION_COUNT)
        totals += hist_values[pool[draws]] * piece_hours / 1000.0

    p5,p50,p95 = percentile_triplet(totals)
    return p5,p50,p95,donor_count


def precompute_valid_block_starts(arrays: dict[str, np.ndarray]) -> dict[int, np.ndarray]:
    regular = arrays["regular"]
    n = len(arrays["values"])
    result: dict[int, np.ndarray] = {}
    bad = (~regular).astype(np.int32)
    bad_prefix = np.concatenate([[0], np.cumsum(bad)])
    for c in (4, 6, 12, 24):
        starts = np.arange(0, n - c, dtype=int)
        good = (bad_prefix[starts + c] - bad_prefix[starts]) == 0
        result[c] = starts[good]
    return result


def c2_predict(arrays: dict[str, np.ndarray], valid_starts: dict[int,np.ndarray], target: Target) -> tuple[float,float,float,int,int,str]:
    ts_ns = arrays["ts_ns"]
    minute = arrays["minute_of_day"]
    weekends = arrays["weekend"]
    hours = arrays["hour"]
    prefix = arrays["prefix_energy"]
    k_total = target.nominal_duration_min // 5
    hist_start_ns = (target.start_utc - pd.Timedelta(days=90)).value
    target_start_ns = target.start_utc.value
    rng = np.random.default_rng(seed64(
        HARNESS_VERSION, "C2_120", target.start_utc.isoformat(),
        target.nominal_duration_min, SIMULATION_COUNT
    ))
    totals = np.zeros(SIMULATION_COUNT, dtype=float)
    remaining = k_total
    consumed = 0
    total_candidates_first_level = 0
    distinct_days: set[str] = set()
    fallback_levels: list[str] = []

    while remaining > 0:
        c = min(24, remaining)
        starts_all = valid_starts[c]
        m = (
            (ts_ns[starts_all] >= hist_start_ns) &
            (ts_ns[starts_all + c] < target_start_ns)
        )
        starts = starts_all[m]
        if len(starts) == 0:
            raise RuntimeError(f"C2 has no valid donor blocks for {target.target_id}")

        frac = consumed / k_total
        chunk_start = target.start_utc + pd.Timedelta(hours=target.actual_duration_hours * frac)
        chunk_local = chunk_start.tz_convert(target.start_local.tz)
        target_minute = chunk_local.hour * 60 + chunk_local.minute + chunk_local.second / 60.0 + chunk_local.microsecond / 60_000_000.0
        diff = np.abs(minute[starts] - target_minute)
        clock_dist = np.minimum(diff, 1440.0 - diff)
        same_day_type = weekends[starts] == is_weekend_ts(chunk_local)

        level = "all"
        pool = starts
        for width, label in ((15.0,"pm15"),(30.0,"pm30"),(60.0,"pm60")):
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
            total_candidates_first_level = int(len(pool))
            dts = pd.to_datetime(ts_ns[pool], utc=True).tz_convert(target.start_local.tz)
            distinct_days = set(str(x.date()) for x in dts)
        fallback_levels.append(level)

        block_energy = prefix[pool + c] - prefix[pool]
        block_hours = (ts_ns[pool + c] - ts_ns[pool]) / 3.6e12
        chunk_hours = target.actual_duration_hours * (c / k_total)
        scaled = block_energy * (chunk_hours / block_hours)
        draws = rng.integers(0, len(pool), size=SIMULATION_COUNT)
        totals += scaled[draws]

        consumed += c
        remaining -= c

    p5,p50,p95 = percentile_triplet(totals)
    return p5,p50,p95,total_candidates_first_level,len(distinct_days),"+".join(fallback_levels)


def clustered_coverage_ci(df: pd.DataFrame, method_prefix: str, seed_label: str) -> tuple[float,float,float]:
    covered_col = f"{method_prefix}_covered"
    by_day = df.groupby("local_date")[covered_col].agg(["sum","count"])
    point = float(df[covered_col].mean())
    if len(by_day) == 1:
        return point, point, point
    rng = np.random.default_rng(seed64(HARNESS_VERSION, "cluster", seed_label, CLUSTER_BOOTSTRAP_REPS))
    arr = by_day[["sum","count"]].to_numpy(dtype=float)
    n = len(arr)
    reps = np.empty(CLUSTER_BOOTSTRAP_REPS, dtype=float)
    for b in range(CLUSTER_BOOTSTRAP_REPS):
        idx = rng.integers(0, n, size=n)
        samp = arr[idx].sum(axis=0)
        reps[b] = samp[0] / samp[1]
    lo, hi = np.quantile(reps, [0.025,0.975], method="linear")
    return point, float(lo), float(hi)


def aggregate_stratum(df: pd.DataFrame, group_cols: list[str]) -> pd.DataFrame:
    rows=[]
    for keys,g in df.groupby(group_cols, dropna=False, sort=True):
        if not isinstance(keys, tuple):
            keys=(keys,)
        base={col:key for col,key in zip(group_cols,keys)}
        for method in ("c0","c2"):
            point,lo,hi=clustered_coverage_ci(g,method,"|".join(map(str,keys))+"|"+method)
            err=g[f"{method}_p50"]-g["truth_kwh"]
            rows.append({
                **base,
                "method":method.upper(),
                "cases":len(g),
                "distinct_dates":g["local_date"].nunique(),
                "coverage_pct":100*point,
                "cluster_ci_low_pct":100*lo,
                "cluster_ci_high_pct":100*hi,
                "p50_bias_kwh":float(err.mean()),
                "p50_mae_kwh":float(np.abs(err).mean()),
                "p50_rmse_kwh":float(np.sqrt(np.mean(err**2))),
                "median_width_kwh":float((g[f"{method}_p95"]-g[f"{method}_p5"]).median()),
                "mean_interval_score":float(g[f"{method}_interval_score"].mean()),
            })
    return pd.DataFrame(rows)


def run(package: Path, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    grid,manifest,package_sha=load_package(package)
    arrays=build_arrays(grid)
    targets=construct_targets(grid,arrays)
    expected={20:242,30:242,60:241,120:241,240:192,480:84}
    actual=pd.Series([t.nominal_duration_min for t in targets]).value_counts().to_dict()
    if actual != expected:
        raise RuntimeError(f"canonical target counts mismatch: expected {expected}, got {actual}")

    valid_starts=precompute_valid_block_starts(arrays)
    records=[]
    for i,t in enumerate(targets,1):
        c0_p5,c0_p50,c0_p95,c0_donors=c0_predict(arrays,t)
        c2_p5,c2_p50,c2_p95,c2_starts,c2_days,c2_fallback=c2_predict(arrays,valid_starts,t)
        rec={
            "target_id":t.target_id,
            "local_date":t.local_date,
            "start_utc":t.start_utc.isoformat(),
            "end_utc":t.end_utc.isoformat(),
            "start_local":t.start_local.isoformat(),
            "end_local":t.end_local.isoformat(),
            "nominal_duration_min":t.nominal_duration_min,
            "actual_duration_min":t.actual_duration_hours*60.0,
            "time_band":t.time_band,
            "is_weekend":t.is_weekend,
            "truth_kwh":t.truth_kwh,
            "start_watts":t.start_watts,
            "end_watts":t.end_watts,
            "active_start":t.start_watts>ACTIVE_WATTS,
            "c0_p5":c0_p5,"c0_p50":c0_p50,"c0_p95":c0_p95,
            "c0_covered":c0_p5<=t.truth_kwh<=c0_p95,
            "c0_interval_score":interval_score(c0_p5,c0_p95,t.truth_kwh),
            "c0_donor_points":c0_donors,
            "c2_p5":c2_p5,"c2_p50":c2_p50,"c2_p95":c2_p95,
            "c2_covered":c2_p5<=t.truth_kwh<=c2_p95,
            "c2_interval_score":interval_score(c2_p5,c2_p95,t.truth_kwh),
            "c2_contextual_starts_first_chunk":c2_starts,
            "c2_distinct_donor_days_first_chunk":c2_days,
            "c2_fallback_levels":c2_fallback,
        }
        records.append(rec)
        if i%100==0 or i==len(targets):
            print(f"processed {i}/{len(targets)}", flush=True)

    cases=pd.DataFrame(records).sort_values(["nominal_duration_min","start_utc"]).reset_index(drop=True)
    cases.to_csv(output_dir/"PHASE3_V2_PER_CASE_2026-09-30.csv",index=False)

    duration=aggregate_stratum(cases,["nominal_duration_min"])
    duration.to_csv(output_dir/"PHASE3_V2_DURATION_AGGREGATE_2026-09-30.csv",index=False)
    start_state=aggregate_stratum(cases,["nominal_duration_min","active_start"])
    start_state.to_csv(output_dir/"PHASE3_V2_START_STATE_AGGREGATE_2026-09-30.csv",index=False)
    weekday=aggregate_stratum(cases,["is_weekend"])
    weekday.to_csv(output_dir/"PHASE3_V2_DAYTYPE_AGGREGATE_2026-09-30.csv",index=False)
    bands=aggregate_stratum(cases,["time_band"])
    bands.to_csv(output_dir/"PHASE3_V2_TIME_BAND_AGGREGATE_2026-09-30.csv",index=False)

    metadata={
        "harness_version":HARNESS_VERSION,
        "input_package":package.name,
        "input_package_sha256":package_sha,
        "expected_package_sha256":EXPECTED_PACKAGE_SHA256,
        "manifest_package_version":manifest.get("package_version"),
        "timezone":manifest.get("timezone"),
        "simulation_count":SIMULATION_COUNT,
        "cluster_bootstrap_reps":CLUSTER_BOOTSTRAP_REPS,
        "target_counts":{str(k):int(v) for k,v in actual.items()},
        "seed_rule":"uint64 big-endian first 8 bytes of SHA256('|'.join(parts)); parts include harness_version, method/aggregate label, target start UTC, nominal duration, simulation count",
        "percentile_semantics":"numpy.quantile(method='linear')",
        "python_version":sys.version,
        "platform":platform.platform(),
        "numpy_version":np.__version__,
        "pandas_version":pd.__version__,
        "package_hash_match_expected":package_sha==EXPECTED_PACKAGE_SHA256,
    }
    (output_dir/"PHASE3_V2_RUN_METADATA_2026-09-30.json").write_text(json.dumps(metadata,indent=2),encoding="utf-8")

    print("\nDURATION\n",duration.to_string(index=False))
    print("\nSTART STATE\n",start_state.to_string(index=False))


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--package",required=True,type=Path)
    ap.add_argument("--output-dir",type=Path,default=Path("phase3_v2_output"))
    args=ap.parse_args()
    run(args.package,args.output_dir)


if __name__=="__main__":
    main()

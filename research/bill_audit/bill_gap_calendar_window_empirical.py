#!/usr/bin/env python3
import argparse, csv, hashlib, itertools, json, math, zipfile
from dataclasses import asdict, dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

import pandas as pd

VERSION = "bill-gap-calendar-window-empirical.v1"
DAY_COUNT = 15
MIN_DAYS = 10
EDGE_TOLERANCE_MINUTES = 5.5
ALPHA = 0.10


@dataclass
class GapResult:
    gap_index: int
    kind: str
    start_local: str
    end_local: str
    duration_minutes: float
    daytype: str
    calibration_days: int
    calibration_first_date: str | None
    calibration_last_date: str | None
    q05_kwh: float | None
    q50_kwh: float | None
    q95_kwh: float | None
    calibration_max_kwh: float | None
    backtest_cases: int
    backtest_coverage: float | None
    backtest_p50_bias_kwh: float | None
    backtest_p50_mae_kwh: float | None
    backtest_mean_width_kwh: float | None
    backtest_mean_interval_score: float | None
    target_start_w: float | None
    target_end_w: float | None
    supporting_same_start_state_prior_days: int | None
    supporting_same_start_state_nonzero_days: int | None


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def invq(values, q):
    vals = sorted(float(x) for x in values)
    if not vals:
        return None
    idx = max(0, math.ceil(q * len(vals)) - 1)
    return vals[idx]


def validate(path):
    outer = sha256_file(path)
    with zipfile.ZipFile(path) as z:
        m = json.loads(z.read("manifest.json"))
        names = set(z.namelist())
        for item in m.get("files", []):
            blob = z.read(item["Name"])
            if len(blob) != int(item["Bytes"]):
                raise ValueError(f"bytes mismatch {item['Name']}")
            if hashlib.sha256(blob).hexdigest().lower() != item["Sha256"].lower():
                raise ValueError(f"sha mismatch {item['Name']}")
        if "normalized_metrics.csv" not in names:
            raise ValueError("normalized_metrics.csv missing")
    return outer, m


def load_grid(path):
    outer, m = validate(path)
    with zipfile.ZipFile(path) as z:
        d = pd.read_csv(
            z.open("normalized_metrics.csv"),
            usecols=[
                "recorded_at_utc",
                "metric_key",
                "normalized_value",
                "confidence",
            ],
        )
    d = d[
        (d.metric_key == "grid_import_power_w") & (d.confidence != "UNRESOLVED")
    ].copy()
    d["value"] = pd.to_numeric(d.normalized_value, errors="coerce")
    d = d[d.value.notna()].copy()
    d["ts"] = pd.to_datetime(d.recorded_at_utc, utc=True, format="mixed")
    d = d.sort_values("ts").drop_duplicates("ts", keep="last")
    gaps = [
        (b - a).total_seconds() / 60
        for a, b in zip(d.ts, d.ts.iloc[1:])
        if b > a
    ]
    med = pd.Series(gaps).median() if gaps else 5.0
    threshold = min(20.0, max(10.0, float(med) * 3.0))
    return outer, m, d[["ts", "value"]], threshold


def parse_local_day(s, tz):
    return datetime.strptime(s, "%Y-%m-%d").replace(tzinfo=tz)


def interval_truth(grid, tz, start_day, end_day, threshold):
    start = parse_local_day(start_day, tz)
    end = parse_local_day(end_day, tz) + timedelta(days=1)
    a = pd.Timestamp(start.astimezone(timezone.utc))
    b = pd.Timestamp(end.astimezone(timezone.utc))
    s = grid[(grid.ts >= a) & (grid.ts <= b)].copy()
    if s.empty:
        raise ValueError("no interval samples")
    times = s.ts.tolist()
    vals = s.value.astype(float).tolist()
    observed = 0.0
    gaps = []
    covered = 0.0
    uncovered = 0.0

    if times[0] > a:
        h = (times[0] - a).total_seconds() / 3600
        uncovered += h
        gaps.append((a, times[0], h * 60, "BOUNDARY_START", None, vals[0]))

    for i, (x, y) in enumerate(zip(times, times[1:])):
        h = (y - x).total_seconds() / 3600
        if h * 60 > threshold:
            uncovered += h
            gaps.append((x, y, h * 60, "INTERNAL", vals[i], vals[i + 1]))
        else:
            covered += h
            observed += (
                (max(0, vals[i]) + max(0, vals[i + 1])) / 2 * h / 1000
            )

    if times[-1] < b:
        h = (b - times[-1]).total_seconds() / 3600
        uncovered += h
        gaps.append((times[-1], b, h * 60, "BOUNDARY_END", vals[-1], None))

    return start, end, observed, covered, uncovered, gaps


def integrate_calendar_window(grid, tz, day, start_clock, duration_min, threshold):
    start = datetime.combine(day, start_clock, tzinfo=tz)
    end = start + timedelta(minutes=duration_min)
    a = pd.Timestamp(start.astimezone(timezone.utc))
    b = pd.Timestamp(end.astimezone(timezone.utc))
    s = grid[(grid.ts >= a) & (grid.ts <= b)].copy()
    if len(s) < 2:
        return None

    pre = (s.ts.iloc[0] - a).total_seconds() / 60
    post = (b - s.ts.iloc[-1]).total_seconds() / 60
    if (
        pre < 0
        or post < 0
        or pre > EDGE_TOLERANCE_MINUTES
        or post > EDGE_TOLERANCE_MINUTES
    ):
        return None

    energy = 0.0
    for i in range(len(s) - 1):
        h = (s.ts.iloc[i + 1] - s.ts.iloc[i]).total_seconds() / 3600
        if h <= 0 or h * 60 > threshold:
            return None
        energy += (
            (max(0, float(s.value.iloc[i])) + max(0, float(s.value.iloc[i + 1])))
            / 2
            * h
            / 1000
        )

    energy += max(0, float(s.value.iloc[0])) * pre / 60 / 1000
    energy += max(0, float(s.value.iloc[-1])) * post / 60 / 1000
    return {
        "day": day,
        "energy_kwh": energy,
        "start_w": float(s.value.iloc[0]),
        "end_w": float(s.value.iloc[-1]),
    }


def historical_series(grid, tz, target_local, duration_min, threshold, first_day):
    rows = []
    day = first_day
    clock = target_local.timetz().replace(tzinfo=None)
    while day < target_local.date():
        r = integrate_calendar_window(grid, tz, day, clock, duration_min, threshold)
        if r:
            r["daytype"] = "WEEKEND" if day.weekday() >= 5 else "WEEKDAY"
            rows.append(r)
        day += timedelta(days=1)
    return rows


def backtest(rows, target_daytype):
    sub = [r for r in rows if r["daytype"] == target_daytype]
    out = []
    for i, r in enumerate(sub):
        cal = sub[max(0, i - DAY_COUNT) : i]
        if len(cal) < MIN_DAYS:
            continue
        vals = [x["energy_kwh"] for x in cal]
        q05, q50, q95 = invq(vals, 0.05), invq(vals, 0.50), invq(vals, 0.95)
        y = r["energy_kwh"]
        score = q95 - q05
        if y < q05:
            score += (2 / ALPHA) * (q05 - y)
        elif y > q95:
            score += (2 / ALPHA) * (y - q95)
        out.append(
            {
                "day": r["day"].isoformat(),
                "truth_kwh": y,
                "q05_kwh": q05,
                "q50_kwh": q50,
                "q95_kwh": q95,
                "covered": q05 <= y <= q95,
                "p50_error_kwh": q50 - y,
                "interval_score": score,
            }
        )
    return out


def analyze(package, start_day, end_day):
    outer, m, grid, threshold = load_grid(package)
    tz = ZoneInfo(m["timezone"])
    start, end, observed, covered, uncovered, gaps = interval_truth(
        grid, tz, start_day, end_day, threshold
    )
    first_day = (
        grid.ts.iloc[0].to_pydatetime().astimezone(tz).date() + timedelta(days=1)
    )
    gap_results = []
    gap_distributions = []
    backtests = []
    deterministic_boundary = 0.0

    for idx, (a, b, dur, kind, start_w, end_w) in enumerate(gaps, 1):
        al = a.to_pydatetime().astimezone(tz)
        bl = b.to_pydatetime().astimezone(tz)

        if kind != "INTERNAL":
            if dur > EDGE_TOLERANCE_MINUTES:
                raise ValueError(f"boundary gap {idx} exceeds edge tolerance")
            watts = float(end_w if kind == "BOUNDARY_START" else start_w)
            deterministic_boundary += max(0, watts) * dur / 60 / 1000
            gap_results.append(
                GapResult(
                    idx,
                    kind,
                    al.isoformat(),
                    bl.isoformat(),
                    dur,
                    "N/A",
                    0,
                    None,
                    None,
                    None,
                    None,
                    None,
                    None,
                    0,
                    None,
                    None,
                    None,
                    None,
                    None,
                    start_w,
                    end_w,
                    None,
                    None,
                )
            )
            continue

        daytype = "WEEKEND" if al.weekday() >= 5 else "WEEKDAY"
        series = historical_series(grid, tz, al, dur, threshold, first_day)
        same = [r for r in series if r["daytype"] == daytype]
        cal = same[-DAY_COUNT:]
        if len(cal) < MIN_DAYS:
            raise ValueError(f"gap {idx} has only {len(cal)} comparable dates")

        vals = [r["energy_kwh"] for r in cal]
        q05, q50, q95 = invq(vals, 0.05), invq(vals, 0.5), invq(vals, 0.95)
        bt = backtest(series, daytype)
        backtests.extend(dict(gap_index=idx, **row) for row in bt)
        coverage = (
            sum(1 for r in bt if r["covered"]) / len(bt)
            if bt
            else None
        )
        bias = (
            sum(r["p50_error_kwh"] for r in bt) / len(bt)
            if bt
            else None
        )
        mae = (
            sum(abs(r["p50_error_kwh"]) for r in bt) / len(bt)
            if bt
            else None
        )
        width = (
            sum(r["q95_kwh"] - r["q05_kwh"] for r in bt) / len(bt)
            if bt
            else None
        )
        score = (
            sum(r["interval_score"] for r in bt) / len(bt)
            if bt
            else None
        )

        target_state = "ACTIVE" if (start_w or 0) > 100 else "INACTIVE"
        prior_same_state = [
            r
            for r in same
            if ("ACTIVE" if r["start_w"] > 100 else "INACTIVE") == target_state
        ]
        nonzero = sum(
            1 for r in prior_same_state if r["energy_kwh"] > 1e-12
        )

        gap_results.append(
            GapResult(
                idx,
                kind,
                al.isoformat(),
                bl.isoformat(),
                dur,
                daytype,
                len(cal),
                cal[0]["day"].isoformat(),
                cal[-1]["day"].isoformat(),
                q05,
                q50,
                q95,
                max(vals),
                len(bt),
                coverage,
                bias,
                mae,
                width,
                score,
                start_w,
                end_w,
                len(prior_same_state),
                nonzero,
            )
        )
        gap_distributions.append(vals)

    combinations = 1
    for arr in gap_distributions:
        combinations *= len(arr)
    if combinations > 1_000_000:
        raise ValueError("exact aggregation too large")

    totals = []
    for combo in itertools.product(*gap_distributions):
        totals.append(observed + deterministic_boundary + sum(combo))
    totals.sort()

    p05, p50, p95 = invq(totals, 0.05), invq(totals, 0.5), invq(totals, 0.95)

    return {
        "version": VERSION,
        "package_file": Path(package).name,
        "package_sha256": outer,
        "timezone": m["timezone"],
        "interval": {
            "start_local": start.isoformat(),
            "end_local_exclusive": end.isoformat(),
        },
        "continuity_threshold_minutes": threshold,
        "observed_kwh": observed,
        "covered_hours": covered,
        "uncovered_hours": uncovered,
        "deterministic_boundary_completion_kwh": deterministic_boundary,
        "gap_results": [asdict(x) for x in gap_results],
        "aggregation": {
            "assumption": "independent empirical draws across distinct internal gaps",
            "exact_combinations": combinations,
            "p05_kwh": p05,
            "p50_kwh": p50,
            "p95_kwh": p95,
            "mean_kwh": sum(totals) / len(totals),
            "max_kwh": max(totals),
        },
        "backtests": backtests,
        "enel_value_used_in_construction": False,
        "status": "PROVISIONAL_REPORT_METHOD_RESEARCH_ONLY",
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--package", required=True, type=Path)
    ap.add_argument("--start", required=True)
    ap.add_argument("--end", required=True)
    ap.add_argument("--output-dir", type=Path)
    a = ap.parse_args()
    r = analyze(a.package, a.start, a.end)

    if a.output_dir:
        a.output_dir.mkdir(parents=True, exist_ok=True)
        (a.output_dir / "BILL_GAP_EMPIRICAL_RESULT.json").write_text(
            json.dumps(r, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        with (a.output_dir / "BILL_GAP_EMPIRICAL_BACKTEST.csv").open(
            "w", newline="", encoding="utf-8"
        ) as f:
            rows = r["backtests"]
            w = csv.DictWriter(
                f,
                fieldnames=list(rows[0].keys()) if rows else ["gap_index"],
            )
            w.writeheader()
            w.writerows(rows)

    print(
        json.dumps(
            {
                k: r[k]
                for k in [
                    "version",
                    "observed_kwh",
                    "covered_hours",
                    "uncovered_hours",
                    "deterministic_boundary_completion_kwh",
                    "gap_results",
                    "aggregation",
                    "enel_value_used_in_construction",
                    "status",
                ]
            },
            indent=2,
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()

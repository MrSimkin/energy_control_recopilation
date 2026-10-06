#!/usr/bin/env python3
import argparse, csv, hashlib, json, zipfile
from dataclasses import dataclass, asdict
from datetime import datetime, timedelta, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

import pandas as pd

VERSION = "bill-interval-truth-table.v1"


@dataclass
class Gap:
    start_utc: str
    end_utc: str
    start_local: str
    end_local: str
    duration_minutes: float
    kind: str
    start_w: float | None
    end_w: float | None


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def median(values):
    ordered = sorted(values)
    if not ordered:
        return 0.0
    middle = len(ordered) // 2
    return (
        ordered[middle]
        if len(ordered) % 2
        else (ordered[middle - 1] + ordered[middle]) / 2
    )


def parse_day(value: str, tz: ZoneInfo) -> datetime:
    return datetime.strptime(value, "%Y-%m-%d").replace(tzinfo=tz)


def validate_package(path: Path):
    outer_sha = sha256_file(path)
    with zipfile.ZipFile(path) as archive:
        names = set(archive.namelist())
        if "manifest.json" not in names:
            raise ValueError("manifest.json missing")
        manifest = json.loads(archive.read("manifest.json"))
        for item in manifest.get("files", []):
            name = item["Name"]
            if name not in names:
                raise ValueError(f"{name} missing")
            blob = archive.read(name)
            if len(blob) != int(item["Bytes"]):
                raise ValueError(f"{name} bytes mismatch")
            if hashlib.sha256(blob).hexdigest().lower() != item["Sha256"].lower():
                raise ValueError(f"{name} sha mismatch")
        if "normalized_metrics.csv" not in names:
            raise ValueError("normalized_metrics.csv missing")
    return outer_sha, manifest


def integrate(package: Path, start_day: str, end_day_inclusive: str):
    outer_sha, manifest = validate_package(package)
    tz = ZoneInfo(manifest["timezone"])
    start_local = parse_day(start_day, tz)
    end_local = parse_day(end_day_inclusive, tz) + timedelta(days=1)
    start_utc = start_local.astimezone(timezone.utc)
    end_utc = end_local.astimezone(timezone.utc)

    with zipfile.ZipFile(package) as archive:
        data = pd.read_csv(
            archive.open("normalized_metrics.csv"),
            usecols=[
                "recorded_at_utc",
                "metric_key",
                "normalized_value",
                "confidence",
                "quality",
            ],
        )

    all_grid = data[data.metric_key.eq("grid_import_power_w")].copy()
    all_grid["ts"] = pd.to_datetime(all_grid.recorded_at_utc, utc=True)
    all_grid["value"] = pd.to_numeric(all_grid.normalized_value, errors="coerce")
    period = all_grid[
        (all_grid.ts >= pd.Timestamp(start_utc))
        & (all_grid.ts <= pd.Timestamp(end_utc))
    ]
    valid = period[
        (period.confidence != "UNRESOLVED") & period.value.notna()
    ].copy()
    valid = valid.sort_values("ts").drop_duplicates("ts", keep="last")

    if valid.empty:
        raise ValueError("no valid grid samples in interval")

    times = valid.ts.tolist()
    values = valid.value.astype(float).tolist()

    positive_gaps = []
    for left, right in zip(times, times[1:]):
        minutes = (right - left).total_seconds() / 60
        if minutes > 0:
            positive_gaps.append(minutes)

    median_gap = median(positive_gaps)
    continuity_threshold = (
        min(20.0, max(10.0, median_gap * 3.0))
        if median_gap > 0
        else 15.0
    )

    positive_wh = 0.0
    negative_wh = 0.0
    covered_hours = 0.0
    uncovered_hours = 0.0
    gaps = []
    first, last = times[0], times[-1]

    if first > pd.Timestamp(start_utc):
        minutes = (first - pd.Timestamp(start_utc)).total_seconds() / 60
        uncovered_hours += minutes / 60
        gaps.append(
            Gap(
                start_utc.isoformat(),
                first.isoformat(),
                start_local.isoformat(),
                first.to_pydatetime().astimezone(tz).isoformat(),
                minutes,
                "BOUNDARY_START",
                None,
                float(values[0]),
            )
        )

    for index, (left, right) in enumerate(zip(times, times[1:])):
        hours = (right - left).total_seconds() / 3600
        if hours <= 0:
            continue

        if hours * 60 > continuity_threshold:
            uncovered_hours += hours
            gaps.append(
                Gap(
                    left.isoformat(),
                    right.isoformat(),
                    left.to_pydatetime().astimezone(tz).isoformat(),
                    right.to_pydatetime().astimezone(tz).isoformat(),
                    hours * 60,
                    "INTERNAL",
                    float(values[index]),
                    float(values[index + 1]),
                )
            )
            continue

        covered_hours += hours
        start_w = values[index]
        end_w = values[index + 1]

        if start_w >= 0 and end_w >= 0:
            positive_wh += (start_w + end_w) / 2 * hours
        elif start_w <= 0 and end_w <= 0:
            negative_wh += (abs(start_w) + abs(end_w)) / 2 * hours
        else:
            magnitude = abs(start_w) + abs(end_w)
            fraction_to_zero = abs(start_w) / magnitude if magnitude else 0
            first_hours = hours * fraction_to_zero
            second_hours = hours - first_hours
            if start_w > 0:
                positive_wh += start_w * first_hours / 2
                negative_wh += abs(end_w) * second_hours / 2
            else:
                negative_wh += abs(start_w) * first_hours / 2
                positive_wh += end_w * second_hours / 2

    if last < pd.Timestamp(end_utc):
        minutes = (pd.Timestamp(end_utc) - last).total_seconds() / 60
        uncovered_hours += minutes / 60
        gaps.append(
            Gap(
                last.isoformat(),
                end_utc.isoformat(),
                last.to_pydatetime().astimezone(tz).isoformat(),
                end_local.isoformat(),
                minutes,
                "BOUNDARY_END",
                float(values[-1]),
                None,
            )
        )

    denominator = covered_hours + uncovered_hours

    # Allocate the exact period-level links into local days. Covered-link energy
    # is split at local midnight by linear interpolation so daily rows sum to
    # the period result even across DST transitions.
    day_map = {}
    day = start_local
    while day < end_local:
        day_map[day.date().isoformat()] = {
            "local_date": day.date().isoformat(),
            "valid_samples": 0,
            "covered_hours": 0.0,
            "uncovered_hours": 0.0,
            "observed_positive_kwh": 0.0,
        }
        day += timedelta(days=1)

    for timestamp in times:
        local = timestamp.to_pydatetime().astimezone(tz)
        key = local.date().isoformat()
        if key in day_map:
            day_map[key]["valid_samples"] += 1

    def allocate_interval(left, right, covered, start_w=None, end_w=None):
        if right <= left:
            return

        total_seconds = (right - left).total_seconds()
        cursor = left

        while cursor < right:
            local = (
                cursor.to_pydatetime().astimezone(tz)
                if hasattr(cursor, "to_pydatetime")
                else cursor.astimezone(tz)
            )
            next_midnight_local = datetime.combine(
                local.date() + timedelta(days=1),
                datetime.min.time(),
                tzinfo=tz,
            )
            next_midnight = pd.Timestamp(
                next_midnight_local.astimezone(timezone.utc)
            )
            piece_end = min(right, next_midnight)
            hours = (piece_end - cursor).total_seconds() / 3600
            key = local.date().isoformat()

            if key not in day_map:
                cursor = piece_end
                continue

            if covered:
                day_map[key]["covered_hours"] += hours
                f0 = (cursor - left).total_seconds() / total_seconds
                f1 = (piece_end - left).total_seconds() / total_seconds
                p0 = start_w + (end_w - start_w) * f0
                p1 = start_w + (end_w - start_w) * f1
                day_map[key]["observed_positive_kwh"] += (
                    (max(0, p0) + max(0, p1)) / 2 * hours / 1000
                )
            else:
                day_map[key]["uncovered_hours"] += hours

            cursor = piece_end

    if first > pd.Timestamp(start_utc):
        allocate_interval(pd.Timestamp(start_utc), first, False)

    for index, (left, right) in enumerate(zip(times, times[1:])):
        hours = (right - left).total_seconds() / 3600
        if hours <= 0:
            continue
        if hours * 60 > continuity_threshold:
            allocate_interval(left, right, False)
        else:
            allocate_interval(
                left,
                right,
                True,
                float(values[index]),
                float(values[index + 1]),
            )

    if last < pd.Timestamp(end_utc):
        allocate_interval(last, pd.Timestamp(end_utc), False)

    daily = []
    for key in sorted(day_map):
        row = day_map[key]
        daily_denominator = row["covered_hours"] + row["uncovered_hours"]
        row["coverage_percent"] = (
            row["covered_hours"] / daily_denominator * 100
            if daily_denominator
            else 0.0
        )
        daily.append(row)

    return {
        "version": VERSION,
        "package_file": package.name,
        "package_sha256": outer_sha,
        "package_generated_utc": manifest.get("generated_utc"),
        "timezone": manifest["timezone"],
        "interval": {
            "start_local": start_local.isoformat(),
            "end_local_exclusive": end_local.isoformat(),
            "start_utc": start_utc.isoformat(),
            "end_utc_exclusive": end_utc.isoformat(),
            "duration_hours": (end_utc - start_utc).total_seconds() / 3600,
        },
        "metric": "grid_import_power_w",
        "source_rows_in_interval": len(period),
        "valid_samples": len(valid),
        "unresolved_or_nonfinite_rows": len(period) - len(valid),
        "median_gap_minutes": median_gap,
        "continuity_threshold_minutes": continuity_threshold,
        "covered_hours": covered_hours,
        "uncovered_hours": uncovered_hours,
        "coverage_percent": (
            covered_hours / denominator * 100 if denominator else 0.0
        ),
        "observed_positive_energy_kwh": positive_wh / 1000,
        "observed_negative_energy_kwh": negative_wh / 1000,
        "min_w": float(min(values)),
        "max_w": float(max(values)),
        "gap_count": len(gaps),
        "gaps": [asdict(gap) for gap in gaps],
        "daily": daily,
        "r3_full_period_applicability": {
            "all_individual_gaps_le_240m": all(
                gap.duration_minutes <= 240.0 for gap in gaps
            ),
            "max_gap_minutes": (
                max(gap.duration_minutes for gap in gaps) if gaps else 0.0
            ),
            "status": (
                "ELIGIBLE_BY_DURATION_ONLY"
                if all(gap.duration_minutes <= 240.0 for gap in gaps)
                else "INELIGIBLE_GAP_GT_240M"
            ),
        },
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--start", required=True, help="YYYY-MM-DD local, inclusive")
    parser.add_argument("--end", required=True, help="YYYY-MM-DD local, inclusive")
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    result = integrate(args.package, args.start, args.end)
    output = args.output_dir

    if output:
        output.mkdir(parents=True, exist_ok=True)
        (output / "BILL_INTERVAL_TRUTH_TABLE.json").write_text(
            json.dumps(result, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )

        with (output / "BILL_INTERVAL_GAPS.csv").open(
            "w", newline="", encoding="utf-8"
        ) as stream:
            rows = result["gaps"]
            writer = csv.DictWriter(
                stream,
                fieldnames=list(rows[0].keys()) if rows else ["start_utc"],
            )
            writer.writeheader()
            writer.writerows(rows)

        with (output / "BILL_INTERVAL_DAILY.csv").open(
            "w", newline="", encoding="utf-8"
        ) as stream:
            rows = result["daily"]
            writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)

    summary_keys = [
        "version",
        "package_sha256",
        "interval",
        "valid_samples",
        "coverage_percent",
        "covered_hours",
        "uncovered_hours",
        "observed_positive_energy_kwh",
        "gap_count",
        "r3_full_period_applicability",
    ]
    print(
        json.dumps(
            {key: result[key] for key in summary_keys},
            indent=2,
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()

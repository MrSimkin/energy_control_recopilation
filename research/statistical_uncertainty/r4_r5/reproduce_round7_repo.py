#!/usr/bin/env python3
"""Reproduce the R4/R5 round-7 key sensitivity, DEVELOPMENT ONLY.

Requires the private, SHA-locked Round-5 ZIP. R3 future data are never read.
"""
import argparse
import hashlib
import io
import json
import zipfile
import numpy as np
import pandas as pd

EXPECTED = "5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc"
CUTOFF = "2026-09-28"


def run(source):
    if hashlib.sha256(source.read_bytes()).hexdigest() != EXPECTED:
        raise ValueError("Round-5 source hash mismatch")
    with zipfile.ZipFile(source) as z:
        assert z.testzip() is None
        m = json.loads(z.read("MANIFIESTO_SHA256.json"))
        for e in m["files"]:
            b = z.read(e["path"])
            assert len(b) == e["bytes"]
            assert hashlib.sha256(b).hexdigest() == e["sha256"]
        a = pd.read_csv(io.BytesIO(z.read("results/R4_DAY_EVENTS.csv")))
        b = pd.read_csv(io.BytesIO(z.read("results/R5_AGE_MARGINALIZED_DEV.csv")))
    assert (a.local_date < CUTOFF).all() and (b.local_date < CUTOFF).all()
    assert not a.duplicated(["local_date", "duration_min"]).any()
    assert not b.duplicated(["utc_start", "duration_min"]).any()
    assert len(b) == 1302 and int((b.truth_kwh > 0).sum()) == 96
    a = a[a.duration_min == 240].sort_values("local_date")
    rare = a[a.event_day == 1]
    assert len(a) == 139 and len(rare) == 5
    b = b.copy()
    p = b.pattern.astype(str).str.zfill(2)
    use = p.ne("00") & b.start_age_exact.astype(bool)
    assert int(use.sum()) == 64
    b["hybrid"] = np.where(use, b.r5_prior_age_lower_kwh, b.bridge_kwh)
    b["delta"] = abs(b.hybrid - b.truth_kwh) - abs(b.bridge_kwh - b.truth_kwh)
    top = b.loc[b.delta.abs().idxmax()]
    groups = {str(d): float(g.delta.mean()) for d, g in b.groupby("duration_min")}
    days = b.groupby("local_date").delta.mean().to_numpy()
    rng = np.random.default_rng(20261028)
    boot = days[rng.integers(0, len(days), size=(10000, len(days)))].mean(axis=1)
    return {"scope": "development_only", "r3_outcomes_used": False,
            "r4": {"comparable_days": len(a), "event_days": len(rare),
                   "total_00_windows": int(a.n_windows.sum()),
                   "hidden_windows": int(rare.event_windows.sum()),
                   "event_kwh": rare.max_hidden_kwh.tolist()},
            "r5": {"cases": len(b), "dates": b.local_date.nunique(),
                   "selected": int(use.sum()),
                   "delta_MAE_hybrid_minus_bridge": float(b.delta.mean()),
                   "delta_by_duration": groups,
                   "largest_influential_case_date": str(top.local_date),
                   "largest_case_duration_min": int(top.duration_min),
                   "delta_without_largest_case": float(b.drop(top.name).delta.mean()),
                   "bootstrap_equal_day_95pct": np.quantile(boot, [0.025,0.975]).tolist()}}


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--round5-zip", required=True, type=__import__("pathlib").Path)
    args = p.parse_args()
    print(json.dumps(run(args.round5_zip), indent=2, ensure_ascii=False))

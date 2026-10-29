#!/usr/bin/env python3
"""Round 10 research replay, only private pre-2026-09-28 development ZIP.

Never import new R3 ResearchPackage or Enel data. Not for future validation.
"""
import argparse
import hashlib
import io
import json
import zipfile
import numpy as np
import pandas as pd

SOURCE_SHA="5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc"
CUTOFF="2026-09-28"

def run(path):
    if hashlib.sha256(path.read_bytes()).hexdigest()!=SOURCE_SHA:
        raise ValueError("Wrong source hash")
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None: raise ValueError("ZIP CRC failure")
        m=json.loads(z.read("MANIFIESTO_SHA256.json"))
        for item in m["files"]:
            b=z.read(item["path"])
            if len(b)!=item["bytes"] or hashlib.sha256(b).hexdigest()!=item["sha256"]:
                raise ValueError("Manifest integrity failure")
        a=pd.read_csv(io.BytesIO(z.read("results/R4_DAY_EVENTS.csv")))
        b=pd.read_csv(io.BytesIO(z.read("results/R5_AGE_MARGINALIZED_DEV.csv")))
    if not a.local_date.lt(CUTOFF).all() or not b.local_date.lt(CUTOFF).all():
        raise ValueError("Reserved R3 dates rejected")
    if a.duplicated(["local_date","duration_min"]).any() or b.duplicated(["utc_start","duration_min"]).any():
        raise ValueError("Duplicate target")
    a=a[a.duration_min.eq(240)].copy()
    a["month"]=a.local_date.str.slice(0,7)
    months=[{"month":month,"days":int(len(g)),"event_days":int(g.event_day.sum())}
            for month,g in a.groupby("month",sort=True)]
    selected=(b.pattern.astype(str).str.zfill(2)!="00") & b.start_age_exact.astype(bool)
    hybrid=np.where(selected,b.r5_prior_age_lower_kwh,b.bridge_kwh)
    delta=np.abs(hybrid-b.truth_kwh)-np.abs(b.bridge_kwh-b.truth_kwh)
    b=b.assign(delta=delta)
    day_exclusions=[float(g.delta.mean()) for dt in b[b.truth_kwh.gt(0)].local_date.unique()
                    for g in [b[b.local_date.ne(dt)]]]
    return {"stage":"RESEARCH_DEVELOPMENT_ONLY","R3_outcomes_used":False,
      "R4":{"months":months,"comparable_dates":int(len(a)),"event_dates":int(a.event_day.sum())},
      "R5":{"cases":int(len(b)),"dates":int(b.local_date.nunique()),
            "positive_cases":int(b.truth_kwh.gt(0).sum()),
            "positive_dates":int(b.loc[b.truth_kwh.gt(0),"local_date"].nunique()),
            "hybrid_applied":int(selected.sum()),"positive_day_exclusions_reverse_advantage":int(sum(x>0 for x in day_exclusions))}}
if __name__=="__main__":
    ap=argparse.ArgumentParser();ap.add_argument("--round5-zip",required=True)
    args=ap.parse_args()
    print(json.dumps(run(__import__("pathlib").Path(args.round5_zip)),indent=2,ensure_ascii=False))

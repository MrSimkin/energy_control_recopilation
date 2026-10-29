#!/usr/bin/env python3
"""R4/R5 round-8 reproducibility digest; development ONLY. Requires private round-5 ZIP."""
import argparse, hashlib, io, json, zipfile
from pathlib import Path
import numpy as np
import pandas as pd
from scipy.stats import beta, betabinom

EXPECTED="5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc"
CUTOFF="2026-09-28"

def run(path):
    assert hashlib.sha256(path.read_bytes()).hexdigest()==EXPECTED, "SHA mismatch"
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None
        manifest=json.loads(z.read("MANIFIESTO_SHA256.json"))
        for row in manifest["files"]:
            payload=z.read(row["path"])
            assert len(payload)==row["bytes"]
            assert hashlib.sha256(payload).hexdigest()==row["sha256"]
        a=pd.read_csv(io.BytesIO(z.read("results/R4_DAY_EVENTS.csv")))
        b=pd.read_csv(io.BytesIO(z.read("results/R5_AGE_MARGINALIZED_DEV.csv")))
    assert (a.local_date<CUTOFF).all() and (b.local_date<CUTOFF).all()
    assert not a.duplicated(["local_date","duration_min"]).any()
    assert not b.duplicated(["utc_start","duration_min"]).any()
    r4=[]
    for d,g in a.groupby("duration_min"):
        n,k=len(g),int(g.event_day.sum())
        assert (g.event_day==(g.event_windows>0)).all()
        row={"duration_min":int(d),"days":n,"event_days":k,
             "windows":int(g.n_windows.sum()),"positive_windows":int(g.event_windows.sum()),
             "Jeffreys_90pct":[float(v) for v in beta.ppf([.05,.95],k+.5,n-k+.5)]}
        if d==240:
            row["planning_extra_comparable_days_for_5_new_events"]={}
            for q in (.5,.8,.9):
                val=next((m for m in range(1,20001)
                    if betabinom.sf(4,m,k+.5,n-k+.5)>=q),None)
                row["planning_extra_comparable_days_for_5_new_events"][str(q)]=val
        r4.append(row)
    assert [(x["duration_min"],x["event_days"]) for x in r4]==[(30,0),(120,0),(240,5)]
    assert len(b)==1302 and b.local_date.nunique()==22
    use=(b.pattern.astype(str).str.zfill(2)!="00")&b.start_age_exact.astype(bool)
    assert int(use.sum())==64
    b["hybrid"]=np.where(use,b.r5_prior_age_lower_kwh,b.bridge_kwh)
    b["delta"]=abs(b.hybrid-b.truth_kwh)-abs(b.bridge_kwh-b.truth_kwh)
    groups=[]
    for key,part in [("all",b),("positive_DEV_ONLY",b[b.truth_kwh>0]),
                     ("R5_selected",b[use])]:
        groups.append({"group":key,"cases":len(part),"days":part.local_date.nunique(),
           "bridge_MAE":float(abs(part.bridge_kwh-part.truth_kwh).mean()),
           "hybrid_MAE":float(abs(part.hybrid-part.truth_kwh).mean())})
    influential=b.loc[b.delta.abs().idxmax()]
    assert b.delta.mean()<0 and b.drop(influential.name).delta.mean()>0
    return {"status":"DEVELOPMENT_ONLY_NOT_VALIDATION","future_R3_outcomes_used":False,
        "source_SHA256":EXPECTED,"r4":r4,
        "r5":{"groups":groups,"positive_days":b[b.truth_kwh>0].local_date.nunique(),
              "influential_duration_min":int(influential.duration_min),
              "delta_MAE_without_influential_case":float(b.drop(influential.name).delta.mean())},
        "warning":"Stationary beta-binomial planning is illustrative; not a future stop rule."}

if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--round5-zip",required=True,type=Path)
    args=p.parse_args()
    print(json.dumps(run(args.round5_zip),ensure_ascii=False,indent=2))

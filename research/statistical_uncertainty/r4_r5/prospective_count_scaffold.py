#!/usr/bin/env python3
"""Safeguarded structural-only scaffold. DRAFT, NOT an approved R4/R5 harness."""
import argparse, json, re
from pathlib import Path
import pandas as pd
FIELDS={"local_date","regular_samples","r4_zero_zero_240_candidates",
        "r5_active_endpoint_candidates","r5_known_age_candidates"}
FORBIDDEN=re.compile(
    r"truth|energy|kwh|coverage|score|residual|actual|observed_inside|label|outcome|target_value|error|mae|rmse|p50|p95",re.I)

def structural_count(csv:Path,freeze_date:str)->dict:
    raw=pd.read_csv(csv)
    if set(raw.columns)!=FIELDS or any(FORBIDDEN.search(col) for col in raw.columns):
        raise ValueError("Unsafe/non-blind schema")
    if raw.empty or raw.local_date.isna().any() or raw.local_date.duplicated().any():
        raise ValueError("Bad date set")
    dates=pd.to_datetime(raw.local_date,errors="raise")
    if not (dates>pd.Timestamp(freeze_date)).all():
        raise ValueError("Date on/before proposed freeze")
    for col in FIELDS-{"local_date"}:
        v=pd.to_numeric(raw[col],errors="raise")
        if v.isna().any() or (v<0).any() or not (v==v.round()).all():
            raise ValueError("Bad structural count")
    if not (raw.r5_known_age_candidates<=raw.r5_active_endpoint_candidates).all():
        raise ValueError("Known age exceeds active endpoint count")
    return {"status":"DESCRIPTIVE_ONLY_NOT_READY","protocol_state":"UNFROZEN_DRAFT",
            "future_outcomes_read":False,"target_truths_computed":False,
            "eligible_dates":int(len(raw)),
            "regular_sample_dates_ge280":int((raw.regular_samples>=280).sum()),
            "r4_candidate_dates":int((raw.r4_zero_zero_240_candidates>0).sum()),
            "r4_candidates":int(raw.r4_zero_zero_240_candidates.sum()),
            "r5_active_endpoint_dates":int((raw.r5_active_endpoint_candidates>0).sum()),
            "r5_known_age_candidates":int(raw.r5_known_age_candidates.sum())}

if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--input",type=Path,required=True)
    p.add_argument("--freeze-date",required=True)
    args=p.parse_args()
    print(json.dumps(structural_count(args.input,args.freeze_date),
                     ensure_ascii=False,indent=2))

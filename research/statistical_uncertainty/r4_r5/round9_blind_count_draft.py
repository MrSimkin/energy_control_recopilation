#!/usr/bin/env python3
"""R4/R5 *proposed* blind structural count, ONLY synthetic QA at this stage.

Fail closed on unknown columns and any hints at outcomes. This is NOT an
approved, frozen, signed, or production-capable statistical validation harness.
It must NOT be pointed at R3 cohort or any real future records prior to protocol
review and independent assurance that the upstream exporter is outcome-blind.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import pandas as pd

ALLOWED=(
 'local_date','regular_samples', 'r4_zero_zero_240_candidates',
 'r5_active_endpoint_candidates','r5_known_age_candidates',
)


def count_structural(csv:Path, freeze_date:str)->dict:
    df=pd.read_csv(csv, dtype={'local_date':'string'})
    if list(df.columns)!=list(ALLOWED):
        raise ValueError('Unsupported schema, refuse extra fields or modified order')
    if df.empty or df.isna().any().any():
        raise ValueError('Empty or missing values')
    date=pd.to_datetime(df.local_date,format='%Y-%m-%d',errors='raise')
    freeze=pd.Timestamp(freeze_date)
    if not bool((date>freeze).all()) or bool(df.local_date.duplicated().any()):
        raise ValueError('Pre-freeze or duplicate date')
    if not date.is_monotonic_increasing:
        raise ValueError('Dates must be strictly increasing')
    for col in ALLOWED[1:]:
        value=pd.to_numeric(df[col],errors='raise')
        if not bool(((value>=0)&(value==value.round())).all()):
            raise ValueError('Negative or non-integer structural measure')
        df[col]=value.astype('int64')
    if not bool((df.r5_known_age_candidates<=df.r5_active_endpoint_candidates).all()):
        raise ValueError('Known-age candidates > active endpoint candidates')
    if not bool((df.r4_zero_zero_240_candidates<=df.regular_samples).all()):
        raise ValueError('R4 candidates > regular samples')
    if not bool((df.r5_active_endpoint_candidates<=df.regular_samples).all()):
        raise ValueError('R5 candidates > regular samples')
    # Labels / outcomes / future truth are neither read nor inferred.
    return {
      'status':'DRAFT_NO_READY_OR_SCORE', 'version':'round9-structural-scaffold-draft.v1',
      'unfrozen':True, 'outcomes_used':False,'score_computed':False,
      'n_dates':len(df), 'full_280sample_dates':int((df.regular_samples>=280).sum()),
      'r4_candidate_days':int((df.r4_zero_zero_240_candidates>0).sum()),
      'r4_candidate_count':int(df.r4_zero_zero_240_candidates.sum()),
      'r5_endpoint_active_days':int((df.r5_active_endpoint_candidates>0).sum()),
      'r5_endpoint_active_count':int(df.r5_active_endpoint_candidates.sum()),
      'r5_known_age_days':int((df.r5_known_age_candidates>0).sum()),
      'r5_known_age_count':int(df.r5_known_age_candidates.sum()),
      'next_step':'review and freeze independent prospective protocol; NEVER automatic READY'
    }

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--synthetic-structural-csv',type=Path,required=True)
    p.add_argument('--proposed-freeze-date',required=True)
    args=p.parse_args()
    print(json.dumps(count_structural(args.synthetic_structural_csv,args.proposed_freeze_date),indent=2,ensure_ascii=False))

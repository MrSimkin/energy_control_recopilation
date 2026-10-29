#!/usr/bin/env python3
"""Round 9 (development only) R4 risk abstention / R5 structural evidence audit.

Read ONLY the exact private Round-5 development ZIP (SHA + internal manifest).
This tool MUST NOT read ResearchPackage, R3 future targets, Enel, or production SQLite.
No source data are changed. Printed or saved findings are aggregate, not per-case.
"""
from __future__ import annotations
import argparse
import hashlib
import io
import json
import zipfile
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd
from scipy.stats import beta

SOURCE_SHA256='5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc'
CUTOFF='2026-09-28'
ALLOW={'results/R4_DAY_EVENTS.csv','results/R5_AGE_MARGINALIZED_DEV.csv'}


def sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def load_fixed_source(p: Path)-> tuple[pd.DataFrame,pd.DataFrame]:
    # Avoid ZIP file names like ../ or symlinks by never extracting file paths.
    if sha256(p.read_bytes()) != SOURCE_SHA256:
        raise ValueError('source ZIP SHA-256 mismatch: refuse different data')
    with zipfile.ZipFile(p) as z:
        if z.testzip() is not None:
            raise ValueError('invalid source ZIP CRC')
        manifest=json.loads(z.read('MANIFIESTO_SHA256.json'))
        rows=manifest['files']
        if len({entry['path'] for entry in rows})!=len(rows):
            raise ValueError('duplicate file in manifest')
        for entry in rows:
            raw=z.read(entry['path'])
            if len(raw)!=entry['bytes'] or sha256(raw)!=entry['sha256']:
                raise ValueError('invalid manifest entry '+entry['path'])
        a=pd.read_csv(io.BytesIO(z.read('results/R4_DAY_EVENTS.csv')))
        b=pd.read_csv(io.BytesIO(z.read('results/R5_AGE_MARGINALIZED_DEV.csv')))
    if not bool((a.local_date < CUTOFF).all() and (b.local_date<CUTOFF).all()):
        raise ValueError('R3 reserved future date detected')
    if bool(a.duplicated(['local_date','duration_min']).any()) or bool(b.duplicated(['utc_start','duration_min']).any()):
        raise ValueError('duplicate development target')
    if len(a)!=419 or len(b)!=1302 or int((b.truth_kwh>0).sum())!=96:
        raise ValueError('unexpected source cohort dimensions')
    if set(a.duration_min)!={30,120,240} or set(b.duration_min)!={30,120,240}:
        raise ValueError('unexpected duration')
    if not (a.event_windows.ge(0)&a.n_windows.ge(a.event_windows)).all():
        raise ValueError('impossible R4 day event count')
    if not (a.event_day == (a.event_windows>0)).all():
        raise ValueError('inconsistent R4 outcomes')
    return a,b


def one_sided_upper(k: int, n: int, confidence:float=.95)->float:
    """Jeffreys Beta posterior ONE-SIDED upper; hypothetical and not calibrated."""
    if n<=0 or k<0 or k>n or not 0<confidence<1:
        raise ValueError('bad posterior inputs')
    return float(beta.ppf(confidence,k+.5,n-k+.5))


def evaluate_r4(day:pd.DataFrame, floors=(20,50,100),limits=(.01,.03,.05,.10))->dict[str,Any]:
    rows=[]
    for duration,g in day.groupby('duration_min',sort=True):
        g=g.sort_values('local_date').reset_index(drop=True)
        assert g.local_date.is_unique
        n=len(g); total_event=int(g.event_day.sum());events=g[g.event_day==1]
        row={'duration_min':int(duration),'comparable_days':n,
             'event_days':total_event,'zero_endpoint_windows':int(g.n_windows.sum()),
             'event_windows':int(g.event_windows.sum()),
             'posterior_95pct_one_sided_upper_at_end':one_sided_upper(total_event,n),
             'event_severity_kwh_min':None if events.empty else float(events.max_hidden_kwh.min()),
             'event_severity_kwh_max':None if events.empty else float(events.max_hidden_kwh.max())}
        rows.append(row)
    long=day[day.duration_min==240].sort_values('local_date').reset_index(drop=True)
    e=long.event_day.to_numpy(dtype=int);m=len(e)
    scenarios=[]
    for floor in floors:
        for tol in limits:
            decisions=[]
            for i in range(floor,m):
                # Only data prior to evaluation date. No information from future dates.
                ub=one_sided_upper(int(e[:i].sum()),i)
                low=ub<tol
                decisions.append((int(e[i]),bool(low)))
            arr=np.array(decisions,dtype=int).reshape(-1,2)
            scenarios.append({'prior_min_days':int(floor),'risk_tolerance_pct':int(tol*100),
             'evaluated_days':int(len(arr)),
             'LOW_RISK_SIGNAL_days':int(arr[:,1].sum()),
             'FALSE_REASSURANCE_event_days':int((arr[:,0]*arr[:,1]).sum()),
             'non_event_low_signal_days':int(((1-arr[:,0])*arr[:,1]).sum()),
             'ABSTAIN_days':int((1-arr[:,1]).sum()),
             'events_on_evaluated_days':int(arr[:,0].sum())})
    # Always warn would warn 119 dates from day 20 with 5 events, 114 false warnings
    return {'events_by_duration':rows,'chronological_policy_scenarios':scenarios,
            'always_warn_comparator_start_20':{'warnings':m-20,
            'false_warnings':int(m-20-e[20:].sum()),'events':int(e[20:].sum())},
            'policy_statement':'Illustrative decision-stress-test only; NO validated low-risk or warning classifier.'}


def evaluate_r5(cases:pd.DataFrame,thresholds=(10,15,20))->dict[str,Any]:
    b=cases.copy()
    pattern=b.pattern.astype(str).str.zfill(2)
    selected=pattern.ne('00') & b.start_age_exact.astype(bool)
    b['hybrid']=np.where(selected,b.r5_prior_age_lower_kwh,b.bridge_kwh)
    b['bridge_abs_err']=(b.bridge_kwh-b.truth_kwh).abs()
    b['hybrid_abs_err']=(b.hybrid-b.truth_kwh).abs()
    b['delta']=b.hybrid_abs_err-b.bridge_abs_err
    base=b.delta.mean()
    all_dates=sorted(b.local_date.unique())
    pos=b[b.truth_kwh>0]
    selected_b=b[selected]
    dates_pos=pos.local_date.nunique()
    date_deltas=b.groupby('local_date')['delta'].mean()
    loo=[]
    for date in all_dates:
        sub=b[b.local_date!=date]
        loo.append(float(sub.delta.mean()))
    bydur=[]
    for dur,g in b.groupby('duration_min',sort=True):
        chosen=selected.loc[g.index]
        positive=g[g.truth_kwh>0]
        bydur.append({'duration_min':int(dur),'n_cases':len(g),'n_dates':g.local_date.nunique(),
            'positive_cases':int((g.truth_kwh>0).sum()),'positive_dates':int(positive.local_date.nunique()),
            'hybrid_applied_cases':int(chosen.sum()),
            'hybrid_applied_dates':int(g.loc[chosen,'local_date'].nunique()),
            'bridge_mae_kwh':float(g.bridge_abs_err.mean()),
            'hybrid_mae_kwh':float(g.hybrid_abs_err.mean())})
    rng=np.random.default_rng(20261028)
    # Equal-day bootstrap; each calendar day is a cluster. Comparisons can also be expressed case-weighted.
    values=date_deltas.to_numpy()
    samples=values[rng.integers(0,len(values),size=(10000,len(values)))].mean(axis=1)
    return {'cases':len(b),'dates':len(all_dates),'positive_cases':len(pos),
            'positive_dates':int(dates_pos),'hybrid_applied_cases':int(selected.sum()),
            'hybrid_applied_dates':int(selected_b.local_date.nunique()),
            'bridge_mae_kwh':float(b.bridge_abs_err.mean()),
            'hybrid_mae_kwh':float(b.hybrid_abs_err.mean()),
            'case_weighted_delta_hybrid_minus_bridge':float(base),
            'dates_improve_LOO_cases':int(sum(x<0 for x in loo)),
            'dates_worsen_LOO_cases':int(sum(x>0 for x in loo)),
            'case_weighted_loo_delta_range':[float(min(loo)),float(max(loo))],
            'equal_day_bootstrap_delta_95pct':[float(x) for x in np.quantile(samples,[.025,.975])],
            'duration_strata':bydur,
            'illustrative_positive_date_gates':[{'min_positive_dates':int(t),'meets':bool(dates_pos>=t)} for t in thresholds],
            'warning':'Development outcomes used for retrospective diagnosis only. Positive-energy eligibility cannot be computed by prospective blind count.'}


def run(path:Path)->dict[str,Any]:
    a,b=load_fixed_source(path)
    return {'phase':'ROUND9_DEVELOPMENT_ONLY','source_zip_sha256':SOURCE_SHA256,
           'cutoff_exclusive':CUTOFF,'future_R3_outcomes_used':False,
           'R4':evaluate_r4(a),'R5':evaluate_r5(b)}

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--round5-zip',type=Path,required=True)
    parser.add_argument('--output',type=Path)
    args=parser.parse_args(); result=run(args.round5_zip)
    output=json.dumps(result,indent=2,ensure_ascii=False,allow_nan=False)
    if args.output:args.output.write_text(output+'\n',encoding='utf-8')
    else:print(output)

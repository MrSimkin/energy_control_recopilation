#!/usr/bin/env python3
"""R4/R5 round-6 retrospective diagnostic. Research-only; no R3 scoring.

Input: two *local private* CSV files exported from the audited round-5 package.
Never reads the SQLite production database or the R3 future cohort.
"""
import argparse
import json
from pathlib import Path
import numpy as np
import pandas as pd

CUTOFF = '2026-09-28'

def run(folder):
    events = pd.read_csv(folder / 'R4_DAY_EVENTS.csv')
    cases = pd.read_csv(folder / 'R5_AGE_MARGINALIZED_DEV.csv')
    assert (events.local_date < CUTOFF).all() and (cases.local_date < CUTOFF).all()
    assert not events.duplicated(['local_date','duration_min']).any()
    assert not cases.duplicated(['utc_start','duration_min']).any()
    assert len(cases) == 1302 and (cases.truth_kwh > 0).sum() == 96
    r4 = events[events.duration_min == 240].sort_values('local_date')
    assert len(r4) == 139 and int(r4.event_day.sum()) == 5
    predictions = []
    for window in (30,60,999999):
        p=[]; y=[]
        values = r4.event_day.to_numpy(dtype=int)
        for i in range(20,len(values)):
            old = values[max(0,i-window):i]
            p.append((sum(old)+0.5)/(len(old)+1))
            y.append(values[i])
        predictions.append({'window': 'ALL' if window == 999999 else window,
                            'days':len(y), 'event_days':int(sum(y)),
                            'brier':float(np.mean((np.array(p)-np.array(y))**2))})
    cases = cases.copy()
    active = cases.pattern.astype(str).str.zfill(2) != '00'
    known = cases.start_age_exact.astype(bool)
    cases['hybrid'] = np.where(active & known, cases.r5_prior_age_lower_kwh, cases.bridge_kwh)
    def mae(col, frame=cases):return float(np.mean(np.abs(frame[col]-frame.truth_kwh)))
    by_date = cases.groupby('local_date')
    deltas=np.array([np.mean(abs(g.hybrid-g.truth_kwh)-abs(g.bridge_kwh-g.truth_kwh)) for _,g in by_date])
    rng=np.random.default_rng(20261028)
    boots=deltas[rng.integers(0,len(deltas),(2000,len(deltas)))].mean(axis=1)
    return {'stage':'development_only','future_r3_outcomes_used':False,
            'R4':{'comparable_days':len(r4),'hidden_event_days':int(r4.event_day.sum()),
                  'event_dates':r4[r4.event_day==1].local_date.tolist(),
                  'chronological_lookback_comparators':predictions},
            'R5':{'cases':len(cases),'dates':cases.local_date.nunique(),
                  'positive_cases':int((cases.truth_kwh>0).sum()),
                  'positive_dates':cases[cases.truth_kwh>0].local_date.nunique(),
                  'hybrid_selected_cases':int((active & known).sum()),
                  'bridge_mae_kwh':mae('bridge_kwh'),
                  'old_r5_mae_kwh':mae('r5_prior_age_lower_kwh'),
                  'hybrid_mae_kwh':mae('hybrid'),
                  'hybrid_minus_bridge_equal_day_95pct':np.quantile(boots,[.025,.975]).tolist()}}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--round5-results',required=True,type=Path)
    args=parser.parse_args()
    print(json.dumps(run(args.round5_results),ensure_ascii=False,indent=2))

if __name__ == '__main__':main()

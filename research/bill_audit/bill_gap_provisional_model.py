#!/usr/bin/env python3
import argparse, hashlib, json, math, zipfile
from dataclasses import dataclass, asdict
from datetime import datetime, timedelta, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

import numpy as np
import pandas as pd

VERSION = 'bill-gap-boundary-day-empirical.provisional-v1'
STATE_THRESHOLD_W = 100.0
HISTORY_DAYS = 90
MIN_DONOR_DAYS = 10
MC_SIMULATIONS = 100000
SEARCH_WINDOWS_MIN = (30, 120, 240)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def validate_package(path: Path):
    outer = sha256_file(path)
    with zipfile.ZipFile(path) as z:
        names = set(z.namelist())
        manifest = json.loads(z.read('manifest.json'))
        for item in manifest.get('files', []):
            blob = z.read(item['Name'])
            if len(blob) != int(item['Bytes']):
                raise ValueError(f"{item['Name']} bytes mismatch")
            if hashlib.sha256(blob).hexdigest().lower() != item['Sha256'].lower():
                raise ValueError(f"{item['Name']} sha mismatch")
    return outer, manifest


def state(w: float) -> str:
    return 'INACTIVE' if w <= STATE_THRESHOLD_W else 'ACTIVE'


def stable_seed(*parts) -> int:
    text = '|'.join(str(p) for p in parts)
    h = hashlib.sha256(text.encode()).digest()
    return int.from_bytes(h[:8], 'big') & 0x7fffffff


def pct(values, q):
    if len(values) == 0:
        return None
    return float(np.quantile(np.asarray(values, float), q, method='linear'))


class GridSeries:
    def __init__(self, package: Path):
        self.package = package
        self.package_sha, self.manifest = validate_package(package)
        self.tz = ZoneInfo(self.manifest['timezone'])
        with zipfile.ZipFile(package) as z:
            df = pd.read_csv(z.open('normalized_metrics.csv'), usecols=[
                'recorded_at_utc','metric_key','normalized_value','confidence'])
        g = df[(df.metric_key == 'grid_import_power_w') & (df.confidence != 'UNRESOLVED')].copy()
        g['ts'] = pd.to_datetime(g.recorded_at_utc, utc=True)
        g['w'] = pd.to_numeric(g.normalized_value, errors='coerce')
        g = g[g.w.notna()].sort_values('ts').drop_duplicates('ts', keep='last')
        self.ts = g.ts.astype('int64').to_numpy()
        self.w = g.w.to_numpy(float)

    def local_day_bounds(self, date_text):
        d = datetime.strptime(date_text, '%Y-%m-%d')
        a = datetime(d.year,d.month,d.day,tzinfo=self.tz)
        b = a + timedelta(days=1)
        return pd.Timestamp(a.astimezone(timezone.utc)), pd.Timestamp(b.astimezone(timezone.utc))

    def period_threshold(self, start_utc, end_utc):
        s, e = int(start_utc.value), int(end_utc.value)
        lo = np.searchsorted(self.ts, s, side='left')
        hi = np.searchsorted(self.ts, e, side='right')
        arr = self.ts[lo:hi]
        gaps = np.diff(arr) / 60e9
        gaps = gaps[gaps > 0]
        med = float(np.median(gaps)) if len(gaps) else 5.0
        return med, min(20.0, max(10.0, med * 3.0))

    def window(self, start_local, duration_min, threshold_min):
        start_local = pd.Timestamp(start_local)
        if start_local.tzinfo is None:
            start_local = start_local.tz_localize(self.tz)
        else:
            start_local = start_local.tz_convert(self.tz)
        start = start_local.tz_convert('UTC')
        end = start + pd.Timedelta(minutes=float(duration_min))
        s, e = int(start.value), int(end.value)
        i = np.searchsorted(self.ts, s, side='right') - 1
        j = np.searchsorted(self.ts, s, side='left')
        ie = np.searchsorted(self.ts, e, side='right') - 1
        je = np.searchsorted(self.ts, e, side='left')
        if i < 0 or j >= len(self.ts) or ie < 0 or je >= len(self.ts):
            return None
        if (self.ts[j] - self.ts[i]) / 60e9 > threshold_min:
            return None
        if (self.ts[je] - self.ts[ie]) / 60e9 > threshold_min:
            return None

        def interp(t, lo, hi):
            if self.ts[lo] == self.ts[hi]:
                return float(self.w[lo])
            f = (t - self.ts[lo]) / (self.ts[hi] - self.ts[lo])
            return float(self.w[lo] + f * (self.w[hi] - self.w[lo]))

        sw, ew = interp(s,i,j), interp(e,ie,je)
        lo = np.searchsorted(self.ts, s, side='right')
        hi = np.searchsorted(self.ts, e, side='left')
        pts_t = np.concatenate(([s], self.ts[lo:hi], [e]))
        pts_w = np.concatenate(([sw], self.w[lo:hi], [ew]))
        links_min = np.diff(pts_t) / 60e9
        if len(links_min) and np.max(links_min) > threshold_min:
            return None
        hours = np.diff(pts_t) / 3.6e12
        wh = np.sum((np.maximum(0,pts_w[:-1]) + np.maximum(0,pts_w[1:])) / 2 * hours)
        return dict(start_w=sw,end_w=ew,energy_kwh=float(wh/1000),max_link_min=float(np.max(links_min) if len(links_min) else 0))

    def gaps(self, start_local, end_local_exclusive, threshold_min):
        a = pd.Timestamp(start_local).tz_convert('UTC')
        b = pd.Timestamp(end_local_exclusive).tz_convert('UTC')
        s,e = int(a.value), int(b.value)
        lo = np.searchsorted(self.ts,s,side='left')
        hi = np.searchsorted(self.ts,e,side='right')
        tt = self.ts[lo:hi]
        ww = self.w[lo:hi]
        out=[]
        if len(tt)==0:
            return out
        if tt[0] > s:
            out.append(dict(kind='BOUNDARY_START',start_ns=s,end_ns=int(tt[0]),start_w=None,end_w=float(ww[0])))
        for k in range(len(tt)-1):
            minutes=(tt[k+1]-tt[k])/60e9
            if minutes > threshold_min:
                out.append(dict(kind='INTERNAL',start_ns=int(tt[k]),end_ns=int(tt[k+1]),start_w=float(ww[k]),end_w=float(ww[k+1])))
        if tt[-1] < e:
            out.append(dict(kind='BOUNDARY_END',start_ns=int(tt[-1]),end_ns=e,start_w=float(ww[-1]),end_w=None))
        for q in out:
            q['duration_min']=(q['end_ns']-q['start_ns'])/60e9
            q['start_utc']=pd.Timestamp(q['start_ns'],tz='UTC').isoformat()
            q['end_utc']=pd.Timestamp(q['end_ns'],tz='UTC').isoformat()
            q['start_local']=pd.Timestamp(q['start_ns'],tz='UTC').tz_convert(self.tz).isoformat()
            q['end_local']=pd.Timestamp(q['end_ns'],tz='UTC').tz_convert(self.tz).isoformat()
        return out


def bridge(start_w,end_w,duration_min):
    return max(0.0,(start_w+end_w)/2.0) * (duration_min/60.0) / 1000.0


def donor_dates(target_start_local, history_days):
    td=pd.Timestamp(target_start_local)
    start=(td-pd.Timedelta(days=history_days)).date()
    end=(td-pd.Timedelta(days=1)).date()
    return pd.date_range(start,end,freq='D')


def search_donors(series: GridSeries, gap, threshold_min):
    target_start=pd.Timestamp(gap['start_local'])
    target_weekend=target_start.dayofweek >= 5
    duration=gap['duration_min']
    dates=donor_dates(target_start,HISTORY_DAYS)

    if gap['kind']=='INTERNAL':
        transition=(state(gap['start_w']),state(gap['end_w']))
        mode='RESIDUAL_RATE'
    elif gap['kind']=='BOUNDARY_START':
        observed_side='end'; observed_state=state(gap['end_w']); mode='DIRECT_ENERGY'
    else:
        observed_side='start'; observed_state=state(gap['start_w']); mode='DIRECT_ENERGY'

    def collect(same_daytype, window_min):
        rows=[]
        for d in dates:
            if same_daytype and ((d.dayofweek>=5) != target_weekend):
                continue
            best=None
            for off in range(-window_min,window_min+1,5):
                st=datetime(d.year,d.month,d.day,target_start.hour,target_start.minute,target_start.second,
                            target_start.microsecond,tzinfo=series.tz) + timedelta(minutes=off)
                w=series.window(st,duration,threshold_min)
                if not w:
                    continue
                if gap['kind']=='INTERNAL':
                    if (state(w['start_w']),state(w['end_w'])) != transition:
                        continue
                else:
                    side = w['end_w'] if observed_side=='end' else w['start_w']
                    if state(side) != observed_state:
                        continue
                key=(abs(off),off)
                if best is None or key < best[0]:
                    best=(key,off,w,st)
            if best:
                row=dict(date=str(d.date()),offset_min=best[1],start_w=best[2]['start_w'],end_w=best[2]['end_w'],
                         energy_kwh=best[2]['energy_kwh'])
                if gap['kind']=='INTERNAL':
                    h=duration/60.0
                    b=bridge(row['start_w'],row['end_w'],duration)
                    row['bridge_kwh']=b
                    row['residual_rate_kw']=(row['energy_kwh']-b)/h
                rows.append(row)
        return rows

    for same in (True,False):
        for win in SEARCH_WINDOWS_MIN:
            rows=collect(same,win)
            if len(rows) >= MIN_DONOR_DAYS:
                return rows, f"{'SAME' if same else 'ALL'}_DAYTYPE_PM{win}", mode
    return rows, 'INSUFFICIENT', mode


def gap_support(gap, donors, mode):
    if mode=='DIRECT_ENERGY':
        return np.asarray([max(0.0,d['energy_kwh']) for d in donors],float)
    h=gap['duration_min']/60.0
    b=bridge(gap['start_w'],gap['end_w'],gap['duration_min'])
    return np.asarray([max(0.0,b + d['residual_rate_kw']*h) for d in donors],float)


def analyze(package: Path,start_date: str,end_date: str):
    s=GridSeries(package)
    start_local=datetime.strptime(start_date,'%Y-%m-%d').replace(tzinfo=s.tz)
    end_local=(datetime.strptime(end_date,'%Y-%m-%d')+timedelta(days=1)).replace(tzinfo=s.tz)
    start_utc=pd.Timestamp(start_local.astimezone(timezone.utc)); end_utc=pd.Timestamp(end_local.astimezone(timezone.utc))
    median_gap, threshold=s.period_threshold(start_utc,end_utc)
    gaps=s.gaps(pd.Timestamp(start_local),pd.Timestamp(end_local),threshold)

    results=[]; supports=[]
    for idx,gap in enumerate(gaps,1):
        donors,fallback,mode=search_donors(s,gap,threshold)
        if len(donors) < MIN_DONOR_DAYS:
            raise RuntimeError(f"gap {idx} insufficient donors: {len(donors)}")
        support=gap_support(gap,donors,mode)
        supports.append(support)
        g=dict(gap)
        g.update(dict(index=idx,donor_count=len(donors),fallback=fallback,model=mode,
                      p05_kwh=pct(support,.05),p50_kwh=pct(support,.50),p95_kwh=pct(support,.95),
                      min_kwh=float(np.min(support)),max_kwh=float(np.max(support)),donors=donors))
        if gap['kind']=='INTERNAL':
            g['target_bridge_kwh']=bridge(gap['start_w'],gap['end_w'],gap['duration_min'])
            g['transition']=f"{state(gap['start_w'])}->{state(gap['end_w'])}"
        else:
            g['observed_boundary_state']=state(gap['end_w'] if gap['kind']=='BOUNDARY_START' else gap['start_w'])
        results.append(g)

    rng=np.random.default_rng(stable_seed(VERSION,s.package_sha,start_date,end_date))
    draws=np.zeros(MC_SIMULATIONS,float)
    for support in supports:
        draws += rng.choice(support,size=MC_SIMULATIONS,replace=True)

    return dict(
        version=VERSION,status='PROVISIONAL_EXTERNAL_REPORT_METHOD',package_file=package.name,
        package_sha256=s.package_sha,timezone=str(s.tz),
        interval=dict(start_local=start_local.isoformat(),end_local_exclusive=end_local.isoformat()),
        rules=dict(state_threshold_w=STATE_THRESHOLD_W,history_days=HISTORY_DAYS,min_donor_days=MIN_DONOR_DAYS,
                   search_windows_min=list(SEARCH_WINDOWS_MIN),same_daytype_preferred=True,
                   one_closest_window_per_donor_day=True,utility_value_used=False,
                   combination='independent empirical donor draw across separated gaps',simulations=MC_SIMULATIONS),
        source_cadence=dict(median_gap_minutes=median_gap,continuity_threshold_minutes=threshold),
        gaps=results,
        missing_energy_distribution=dict(p05_kwh=pct(draws,.05),p50_kwh=pct(draws,.50),p95_kwh=pct(draws,.95),
                                         mean_kwh=float(draws.mean()),min_kwh=float(draws.min()),max_kwh=float(draws.max())),
    )


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--package',required=True,type=Path)
    ap.add_argument('--start',required=True)
    ap.add_argument('--end',required=True)
    ap.add_argument('--output',type=Path)
    a=ap.parse_args()
    result=analyze(a.package,a.start,a.end)
    text=json.dumps(result,indent=2,ensure_ascii=False)
    if a.output:
        a.output.parent.mkdir(parents=True,exist_ok=True)
        a.output.write_text(text,encoding='utf-8')
    print(text)

if __name__=='__main__':
    main()

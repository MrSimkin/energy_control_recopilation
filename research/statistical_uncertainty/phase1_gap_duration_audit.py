import numpy as np, math, pandas as pd
from datetime import datetime, timedelta
try:
    from scipy.signal import lfilter
except Exception:
    lfilter=None

SEGMENT_MINUTES=5.0
DT_HOURS=SEGMENT_MINUTES/60
START=datetime(2026,1,5)
N_INTERVALS=14*24*12
TIMES=[START+timedelta(minutes=5*i) for i in range(N_INTERVALS+1)]
HOURS=np.array([t.hour for t in TIMES],dtype=np.int16)
WEEKENDS=np.array([t.weekday()>=5 for t in TIMES],dtype=np.int8)
PROFILE=np.array([
    320+700*math.exp(-0.5*(((t.hour+t.minute/60)-8)/1.5)**2)
    +300*math.exp(-0.5*(((t.hour+t.minute/60)-13)/2.5)**2)
    +1050*math.exp(-0.5*(((t.hour+t.minute/60)-20)/2)**2)
    for t in TIMES],dtype=float)
CAT=HOURS+24*WEEKENDS

def ar_res(phi,sd,rng,n):
    innov=rng.normal(0,sd*math.sqrt(1-phi**2),n)
    if lfilter is not None:
        return lfilter([1.0],[1.0,-phi],innov)
    out=np.empty(n);out[0]=innov[0]
    for i in range(1,n):out[i]=phi*out[i-1]+innov[i]
    return out

def build_series(name,rng):
    if name=='stable': v=800+rng.normal(0,140,len(TIMES))
    elif name=='day_night': v=PROFILE+rng.normal(0,120,len(TIMES))
    elif name=='ar_moderate': v=PROFILE+ar_res(.55,220,rng,len(TIMES))
    elif name=='ar_high': v=PROFILE+ar_res(.93,300,rng,len(TIMES))
    else: raise ValueError(name)
    return np.clip(v,20,None)

def full_energy(v): return float(np.sum((v[:-1]+v[1:])*0.5*DT_HOURS)/1000)
def interval_energy(v,s,k): return float(np.sum((v[s:s+k]+v[s+1:s+k+1])*0.5*DT_HOURS)/1000)
def bridge_energy(v,s,k): return float((v[s]+v[s+k])*0.5*(k*DT_HOURS)/1000)

def simulate_gap_fast(v,s,k,sims,rng):
    e=s+k
    truegap=interval_energy(v,s,k)
    outside=full_energy(v)-truegap
    keep=np.ones(len(v),dtype=bool); keep[s+1:e]=False
    missing=np.zeros(sims)
    seg_cats=np.empty(k,dtype=np.int16)
    for off in range(k):
        mid=TIMES[s+off]+timedelta(minutes=2.5)
        seg_cats[off]=mid.hour+24*(mid.weekday()>=5)
    for cat in np.unique(seg_cats):
        m=int(np.sum(seg_cats==cat))
        pool=v[keep & (CAT==cat)]
        if len(pool)<12:
            raise RuntimeError(f'strict pool insufficient: {len(pool)}')
        idx=rng.integers(0,len(pool),size=(sims,m))
        missing += pool[idx].sum(axis=1)*DT_HOURS/1000
    q=np.quantile(outside+missing,[.05,.5,.95],method='linear')
    return q,truegap

def wilson(x,n):
    z=1.959963984540054;p=x/n;d=1+z*z/n
    c=(p+z*z/(2*n))/d
    h=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
    return c-h,c+h

def run_cell(scenario,sep,reps=1000,sims=2000,seed=1234):
    k=sep//5
    master=np.random.default_rng(seed + sum(map(ord,scenario))*1000 + sep)
    stat=sep>15
    covered=0;errs=[];rel=[];width=[];gaps=[]
    for rep in range(reps):
        v=build_series(scenario,np.random.default_rng(master.integers(0,2**63-1)))
        s=int(master.integers(2*24*12,12*24*12-k))
        truth=full_energy(v)
        if stat:
            q,truegap=simulate_gap_fast(v,s,k,sims,np.random.default_rng(master.integers(0,2**63-1)))
        else:
            truegap=interval_energy(v,s,k)
            p=truth-truegap+bridge_energy(v,s,k)
            q=np.array([p,p,p])
        p5,p50,p95=q
        covered += int(p5 <= truth <= p95)
        err=p50-truth
        errs.append(err)
        rel.append(abs(err)/max(truegap,1e-12)*100)
        width.append(p95-p5)
        gaps.append(truegap)
    lo,hi=wilson(covered,reps)
    reported_cov=100.0 if not stat else (14*24-sep/60)/(14*24)*100
    return dict(
        scenario=scenario,separation_min=sep,missing_source_samples=k-1,
        classification='STATISTICAL_GAP' if stat else 'BRIDGED_CONTINUOUS',
        statistical_segments=k if stat else 0,repetitions=reps,
        simulations=sims if stat else 0,empirical_coverage_pct=covered/reps*100,
        ci_low=lo*100,ci_high=hi*100,p50_bias_kwh=float(np.mean(errs)),
        p50_mae_kwh=float(np.mean(np.abs(errs))),
        median_abs_error_pct_true_gap=float(np.median(rel)),
        median_true_gap_kwh=float(np.median(gaps)),
        median_interval_width_kwh=float(np.median(width)),
        reported_temporal_coverage_pct=reported_cov)

DURS=[10,15,20,30,60,120,240,480,720]

if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('--scenario', choices=['stable','day_night','ar_moderate','ar_high'], required=True)
    parser.add_argument('--repetitions', type=int, default=1000)
    parser.add_argument('--simulation-count', type=int, default=2000)
    parser.add_argument('--output')
    args=parser.parse_args()

    rows=[run_cell(args.scenario,d,args.repetitions,args.simulation_count) for d in DURS]
    df=pd.DataFrame(rows)
    if args.output:
        df.to_csv(args.output,index=False)
    print(df.to_string(index=False))

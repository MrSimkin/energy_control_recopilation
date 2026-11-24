"""R4-v3 frozen research reference; not production."""
import argparse, hashlib, json, zipfile, itertools
from pathlib import Path
from types import SimpleNamespace
import numpy as np
import pandas as pd
VERSION='grid-import-boundary-hurdle-dayweighted.candidate-v5.r4-v3'
FUTURE='2026-11-25'
DURS=(20,30,45,60,90,120,180,240)
MC=100000

def load(package):
 with zipfile.ZipFile(package) as z:
  m=json.loads(z.read('manifest.json')); assert m['timezone']=='America/Santiago'
  for f in m['files']:
   raw=z.read(f['Name']);assert len(raw)==f['Bytes'] and hashlib.sha256(raw).hexdigest()==f['Sha256']
  g=pd.read_csv(z.open('normalized_metrics.csv'),usecols=['recorded_at_utc','metric_key','normalized_value','confidence'])
 g=g[(g.metric_key=='grid_import_power_w')&(g.confidence!='UNRESOLVED')].copy();g['t']=pd.to_datetime(g.recorded_at_utc,utc=True);g['local']=g.t.dt.tz_convert(m['timezone']);g['date']=g.local.dt.strftime('%Y-%m-%d');g=g[np.isfinite(g.normalized_value)].sort_values('t').drop_duplicates('t',keep='last').reset_index(drop=True)
 return g

def windows(g,truth=True,operational=False):
 v=g.normalized_value.to_numpy();assert (v>=0).all();ns=g.t.astype('int64').to_numpy();mins=np.diff(ns)/60e9;bad=np.r_[0,np.cumsum((mins<4.5)|(mins>5.5))];pref=np.r_[0,np.cumsum((v[:-1]+v[1:])/2*mins/60000)] if truth else None;pos=np.r_[0,np.cumsum(v>0)] if truth else None;frames=[];targets=[]
 for d,idx in g.groupby('date',sort=True).indices.items():
  if len(idx)<280:continue
  l=g.local.iloc[idx];h=l.dt.hour.to_numpy()+l.dt.minute.to_numpy()/60+l.dt.second.to_numpy()/3600;us=l.dt.microsecond.to_numpy()
  for dur in (DURS+(225,) if operational else DURS):
   k=dur//5;s=idx[:-k];e=idx[k:];ok=(e==s+k)&(bad[e]==bad[s]);group=0 if dur<=45 else 1 if dur<=180 else 2
   for lo in (0,6,12,18):
    hi=lo+6;sel=np.flatnonzero(ok&(h[:-k]>=lo)&(h[:-k]<hi)&(h[k:]<=hi)&~((h[k:]==hi)&(us[k:]!=0)))
    if not len(sel):continue
    a,b=s[sel[::3]],e[sel[::3]];hrs=(ns[b]-ns[a])/3.6e12;br=(v[a]+v[b])/2000
    f=pd.DataFrame(dict(date=d,duration=dur,group=group,band=lo,weekend=l.iloc[0].dayofweek>=5,boundaries=(v[a]>0).astype(int)+(v[b]>0).astype(int),active=v[a]>100,hours=hrs,bridge_rate=br,start=ns[a],end=ns[b]))
    if truth:
     zero=pos[b+1]==pos[a];y=np.where(zero,0,np.maximum(0,pref[b]-pref[a]));f["truth"]=y;f["rate"]=y/hrs
     if dur in DURS:frames.append(f.copy())
    if operational:
     nearest=np.array([min(DURS,key=lambda d:abs(d-h*60)) for h in hrs]);mapped=240 if dur==225 else dur;f=f[(hrs*60>15)&(hrs*60<=240)&(nearest==mapped)].copy();f["source_duration"]=dur;f["duration"]=mapped
    if len(f):targets.append(f.iloc[[0]])
 return (pd.concat(frames,ignore_index=True) if frames else pd.DataFrame(),pd.concat(targets,ignore_index=True).drop_duplicates(["date","duration","band"],keep="first") if targets else pd.DataFrame())

def wt(p):return 1/p.groupby('date')['date'].transform('size').to_numpy()/p.date.nunique()
def q(a,ww,ps):
 ix=np.argsort(a,kind='stable');return a[ix][np.minimum(np.searchsorted(np.cumsum(ww[ix])/ww.sum(),ps),len(a)-1)]
def pools(b,r):
 return [('duration_band_daytype',b[(b.duration==r.duration)&(b.band==r.band)&(b.weekend==r.weekend)]),('duration_band',b[(b.duration==r.duration)&(b.band==r.band)]),('duration',b[b.duration==r.duration]),('group',b[b.group==r.group]),('all_durations',b)]
def choose(b,r,min_days,allow_all=False):
 for level,p in pools(b,r):
  if level=='all_durations' and not allow_all:break
  if p.date.nunique()>=min_days:return level,p
 return 'INSUFFICIENT',None

def pool(hist,recent,r):
 bp=recent[recent.boundaries==r.boundaries];bfull=hist[hist.boundaries==r.boundaries];lp,prob=choose(bp,r,20,True)
 if r.boundaries>0:p0=0.
 elif prob is not None:p0=float(np.clip(wt(prob)[prob.rate.to_numpy()==0].sum(),0,1))
 else:p0=None
 lm,mag=choose(bp[bp.rate>0],r,10)
 if mag is None:lm,mag=choose(bfull[bfull.rate>0],r,10,True);lm='historical_'+lm
 if mag is not None:
  ma=mag.rate.to_numpy() if r.boundaries==0 else (mag.rate-mag.bridge_rate).to_numpy();mw=wt(mag)
 else:ma=mw=None
 # unconditional empirical comparator with same time/duration fallback; no boundary conditioning
 lu,un=choose(recent,r,20);ua=un.rate.to_numpy() if un is not None else None;uw=wt(un) if un is not None else None
 return lp,p0,lm,mag,ma,mw,lu,ua,uw

def prediction(p,r):
 lp,p0,lm,mag,ma,mw,lu,ua,uw=p
 if p0 is None or ma is None:return None
 ps=np.array([.05,.5,.95]);qs=np.zeros(3);ix=ps>p0
 if ix.any():qs[ix]=np.maximum(1e-12,q(ma,mw,(ps[ix]-p0)/(1-p0))+(r.bridge_rate if r.boundaries else 0))
 return qs*r.hours

def draw(p,r,rng):
 _,p0,_,_,ma,mw,_,_,_=p
 if p0==1:return np.zeros(MC)
 x=np.maximum(1e-12,rng.choice(ma,MC,p=mw/mw.sum())+(r.bridge_rate if r.boundaries else 0))*r.hours
 x[rng.random(MC)<p0]=0;return x

def metrics(rec,qs,y):
 lo,med,hi=qs;err=med-y
 return dict(rec,p5=lo,p50=med,p95=hi,covered=int(lo-1e-9<=y<=hi+1e-9),err=err,ae=abs(err),width=hi-lo,score=hi-lo+20*max(lo-y,y-hi,0))

def seed(*parts):return int.from_bytes(hashlib.sha256('|'.join(map(str,parts)).encode()).digest()[:8],'big')
def evaluate(don,w,start,end=None,aggregate=False):
 rows=[];multi=[]
 for day,targ in w[(w.date>=start)&(w.date<=end if end else True)].groupby('date'):
  hist=don[don.date<day];recent=hist[hist.date.isin(sorted(hist.date.unique())[-60:])];cache={};by={d:[] for d in DURS}
  for r in targ.itertuples():
   key=(r.duration,r.band,r.weekend,r.boundaries)
   if key not in cache:cache[key]=pool(hist,recent,r)
   p=cache[key];qs=prediction(p,r);base=None if p[7] is None else q(p[7],p[8],[.05,.5,.95])*r.hours
   rec=dict(date=day,duration=r.duration,band=r.band,active=r.active,boundaries=r.boundaries,truth=r.truth,bridge=r.bridge_rate*r.hours,hours=r.hours,start=r.start,end=r.end,source_duration=getattr(r,"source_duration",r.duration))
   for name,a in [('R4',qs),('unconditional',base)]:
    z=dict(rec,model=name,status='OK' if a is not None else 'INSUFFICIENT')
    if name=='R4' and a is not None:z.update(p0=p[1],prob_level=p[0],mag_level=p[2],positive_days=p[3].date.nunique())
    rows.append(z if a is None else metrics(z,a,r.truth))
   by[r.duration].append((r,p,qs is not None))
  if aggregate:
   for label,req in [('4x60',(60,)*4),('2x120',(120,)*2),('20+30+60+120',(20,30,60,120))]:
    candidates=[]
    for combo in itertools.product(*(by[d] for d in req)):
     a=sorted(combo,key=lambda x:x[0].start)
     if all(x[0].end<y[0].start for x,y in zip(a,a[1:])):candidates.append(a)
    if not candidates:continue
    a=min(candidates,key=lambda z:tuple(x[0].start for x in z)+tuple(x[0].duration for x in z));y=sum(x[0].truth for x in a);rec=dict(date=day,scenario=label,truth=y,active=any(x[0].active for x in a),status='OK')
    if not all(x[2] for x in a):multi.append(dict(rec,status='INSUFFICIENT'));continue
    rng=np.random.default_rng(seed(VERSION,day,label,MC));tot=np.zeros(MC)
    for r,p,_ in a:tot+=draw(p,r,rng)
    multi.append(metrics(rec,np.quantile(tot,[.05,.5,.95]),y))
 return pd.DataFrame(rows),pd.DataFrame(multi)

def summary(rows):
 out=[]
 for name,p in rows[rows.status=='OK'].groupby('model'):
  for label,x in [('all',p),('active',p[p.active]),('inactive',p[~p.active]),('positive',p[p.truth>0]),('zero',p[p.truth==0]),('short',p[p.duration<=45]),('medium',p[(p.duration>45)&(p.duration<=180)]),('long',p[p.duration==240]),('active_long',p[p.active&(p.duration==240)])]:
   if x.empty:continue
   by=x.groupby('date').covered.agg(['sum','count']).to_numpy();rng=np.random.default_rng(seed(VERSION,name,label));a=by[rng.integers(0,len(by),(5000,len(by)))].sum(axis=1);ci=np.quantile(a[:,0]/a[:,1],[.025,.975]);out.append(dict(model=name,stratum=label,n=len(x),days=x.date.nunique(),coverage=x.covered.mean(),ci_low=ci[0],ci_high=ci[1],bias=x.err.mean(),mae=x.ae.mean(),rmse=np.sqrt((x.err**2).mean()),width=x.width.mean(),score=x.score.mean(),bridge_mae=(x.bridge-x.truth).abs().mean()))
 return pd.DataFrame(out)

def count(w):
 r=dict(status='INSUFFICIENT',stop_date=None,active_days=0,active_cases=0,groups=[0,0,0])
 if w.empty:return r
 w=w[w.date>=FUTURE];active=w[w.active]
 for day in sorted(w.date.unique()):
  a=active[active.date<=day];groups=[int((a.group==i).sum()) for i in range(3)];n=a.date.nunique();ok=n>=10 and len(a)>=80 and min(groups)>=5
  r=dict(status='READY' if ok else 'INSUFFICIENT',stop_date=day if ok else None,active_days=int(n),active_cases=len(a),groups=groups)
  if ok:break
 return r

def gates(rows,s,multi):
 r=rows[rows.model=='R4'];p=r[r.status=='OK'];b=rows[(rows.model=='unconditional')&(rows.status=='OK')];common=p.merge(b,on=['date','duration','band'],suffixes=('_r','_b'));s=s[s.model=='R4'].set_index('stratum');checks={}
 for name in ['all','active','inactive','positive','short','medium','long']:
  checks['coverage_'+name]=bool(name in s.index and s.loc[name,'n']>=10 and s.loc[name,'coverage']>=(.85 if name in ['all','active','inactive','positive'] else .80))
 checks['insufficient_le_5pct']=bool(len(r) and (r.status!='OK').mean()<=.05)
 checks['paired_baseline_score']=bool(len(common)>=.95*len(p) and len(common) and common.score_r.mean()<=common.score_b.mean())
 checks['point_mae_vs_bridge']=bool(len(p) and p.ae.mean()<=(p.bridge-p.truth).abs().mean()*1.20+1e-9)
 a=common[common.active_r];checks['active_bias_vs_baseline']=bool(len(a) and abs(a.err_r.mean())<=abs(a.err_b.mean())+1e-9)
 m=multi[multi.status=='OK'] if len(multi) else multi
 mg=bool(len(m) and m.date.nunique()>=10 and m.covered.mean()>=.85 and len(m)>=.95*len(multi))
 return dict(single_gap_gates=checks,single_gap_status='PASS_PENDING_STRUCTURAL_REVIEW' if all(checks.values()) else 'FAIL',multiple_gap_status='PASS_PENDING_STRUCTURAL_REVIEW' if mg else 'NOT_ACCEPTED',structural_review='REQUIRED')

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--stage',choices=['historical','operational-historical','count','score','predict','self-test'],required=True);ap.add_argument('--package');ap.add_argument('--out',default='r4_output');ap.add_argument('--start');ap.add_argument('--end');ap.add_argument('--start-w',type=float);ap.add_argument('--end-w',type=float);a=ap.parse_args()
 if a.stage=='self-test':
  assert q(np.array([1.,2.]),np.array([.5,.5]),[.05,.5,.95]).tolist()==[1.,1.,2.];assert count(pd.DataFrame())['status']=='INSUFFICIENT';print('PASS');return
 out=Path(a.out);out.mkdir(parents=True,exist_ok=True);g=load(a.package);meta=dict(version=VERSION,package_sha256=hashlib.sha256(Path(a.package).read_bytes()).hexdigest(),code_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),numpy=np.__version__,pandas=pd.__version__)
 if a.stage=='count':
  _,t=windows(g[g.date>=FUTURE].reset_index(drop=True),False,True);r=dict(meta,**count(t));(out/'count.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2));return
 if a.stage=='predict':
  start=pd.Timestamp(a.start).tz_convert('America/Santiago');end=pd.Timestamp(a.end).tz_convert('America/Santiago');h=(end-start).total_seconds()/3600;band=start.hour//6*6
  if not 15<h*60<=240 or start.date()!=end.date() or end.hour+end.minute/60+end.second/3600+end.microsecond/3.6e9>band+6:raise ValueError('NOT_APPLICABLE: outside scope')
  if not all(x is not None and np.isfinite(x) and x>=0 for x in (a.start_w,a.end_w)):raise ValueError('Valid observed boundaries required')
  day=str(start.date());dur=min(DURS,key=lambda d:abs(d-h*60));don,_=windows(g[g.date<day].reset_index(drop=True));r=SimpleNamespace(duration=dur,group=0 if dur<=45 else 1 if dur<=180 else 2,band=band,weekend=start.dayofweek>=5,boundaries=int(a.start_w>0)+int(a.end_w>0),hours=h,bridge_rate=(a.start_w+a.end_w)/2000);p=pool(don,don[don.date.isin(sorted(don.date.unique())[-60:])],r);qs=prediction(p,r);print(json.dumps(dict(meta,status='EXPERIMENTAL' if qs is not None else 'INSUFFICIENT',p5_p50_p95_kwh=None if qs is None else qs.tolist(),p0=p[1]),indent=2));return
 if a.stage in ('historical','operational-historical'):
  g=g[g.date<'2026-09-28'].reset_index(drop=True);don,t=windows(g)
  if a.stage=='historical':
   r,_=evaluate(don,t,'2026-07-01');r.to_csv(out/'historical_cases.csv',index=False);summary(r).to_csv(out/'historical_summary.csv',index=False)
  _,t=windows(g,True,True);r,m=evaluate(don,t,'2026-07-01',aggregate=True);prefix='operational_historical'
 else:
  _,t=windows(g[g.date>=FUTURE].reset_index(drop=True),False,True);c=count(t)
  if c['status']!='READY':raise RuntimeError('INSUFFICIENT: score forbidden')
  with (out/'score_attempt_lock.json').open('x') as f:json.dump(dict(meta,**c),f,indent=2)
  g=g[g.date<=c['stop_date']].reset_index(drop=True);don,t=windows(g,True,True);r,m=evaluate(don,t,FUTURE,c['stop_date'],True);prefix='prospective'
 s=summary(r);r.to_csv(out/(prefix+'_cases.csv'),index=False);s.to_csv(out/(prefix+'_summary.csv'),index=False);m.to_csv(out/(prefix+'_multi.csv'),index=False);result=dict(meta,**gates(r,s,m));(out/(prefix+'_gates.json')).write_text(json.dumps(result,indent=2));print(s.to_string(index=False));print(json.dumps(result,indent=2))
if __name__=='__main__':main()

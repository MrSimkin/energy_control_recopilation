"""Experimental two-state semi-Markov bridge; no future scoring."""
import argparse, hashlib, zipfile, json
from pathlib import Path
import numpy as np
import pandas as pd
import r4_reference as ref
CUTOFF='2026-09-28'; N=2000; CAP=576
BINS=[1,3,6,12,24,48,72,96,144,216,288,432,576,10000000]

def runs(v,ns):
 st=v>0;reg=(np.diff(ns)>=4.5*60e9)&(np.diff(ns)<=5.5*60e9)
 a=np.r_[0,np.flatnonzero(~reg|(st[:-1]!=st[1:]))+1];b=np.r_[a[1:]-1,len(v)-1]
 left=a==0;left[a>0]=~reg[a[a>0]-1];right=b==len(v)-1;right[b<len(v)-1]=~reg[b[b<len(v)-1]]
 return a,b,st[a].astype(int),left,right

def fit(v,ns,dates):
 a,b,s,left,right=runs(v,ns);n=b-a+1;haz=[];mem=[];agep=[];counts=[];ages=np.arange(1,CAP+1)
 for state in (0,1):
  ix=(s==state)&~left;L=n[ix];event=~right[ix];counts.append((int(event.sum()),len(set(dates[a[ix&~right]]))))
  exp=L-1+event;gh=(event.sum()+.5)/(exp.sum()+1);mem.append(np.full(CAP,gh));h=np.zeros(CAP);hist=np.zeros(CAP)
  for lo,hi in zip(BINS[:-1],BINS[1:]):
   risk=np.maximum(0,np.minimum(exp,hi-1)-lo+1).sum();ev=((L>=lo)&(L<hi)&event).sum();h[(ages>=lo)&(ages<hi)]=(ev+20*gh)/(risk+20)
  for l in L:hist[:min(l,CAP)]+=1;hist[-1]+=max(0,l-CAP)
  haz.append(np.clip(h,1e-7,1-1e-7));agep.append(hist)
 ix=(s==1)&~left&(n>=3);means=np.array([v[x:y+1].mean() for x,y in zip(a[ix],b[ix])]);_,inv,c=np.unique(dates[a[ix]],return_inverse=True,return_counts=True);w=1/c[inv];w=w/w.sum()
 return np.array(haz),np.array(mem),np.array(agep),means,w,counts

def backward(h,k,end):
 nxt=np.minimum(np.arange(CAP)+1,CAP-1);f=np.zeros((k+1,2,CAP));f[0,end]=1
 for j in range(1,k+1):f[j]=(1-h)*f[j-1][:,nxt]+h*f[j-1,::-1,0][:,None]
 return f

def simulate(h,agep,means,mw,resid,rw,k,age,known,x0,x1,hours,rng,f,n=N):
 s0=int(x0>0);s1=int(x1>0);lower=min(age,CAP)-1
 if known:p=np.eye(1,CAP,lower)[0]
 else:
  p=agep[s0].copy();p[:lower]=0
  if not p.sum():p[lower]=1
 p*=f[k,s0]
 if p.sum()<=0:return None
 ar=rng.choice(CAP,n,p=p/p.sum());s=np.full(n,s0,dtype=np.int8);paths=np.empty((n,k+1),dtype=np.int8);paths[:,0]=s
 for j in range(k):
  rem=k-j;stay=np.minimum(ar+1,CAP-1);den=f[rem,s,ar];switch=h[s,ar]*f[rem-1,1-s,0]/np.maximum(den,1e-300);flip=rng.random(n)<np.clip(switch,0,1);s=np.where(flip,1-s,s);ar=np.where(flip,0,stay);paths[:,j+1]=s
 assert (paths[:,-1]==s1).all()
 active=paths[:,1:-1].sum(axis=1);same=(paths==1).all(axis=1);first=np.cumprod(paths,axis=1)[:,1:-1].sum(axis=1);last=np.cumprod(paths[:,::-1],axis=1)[:,1:-1].sum(axis=1);last=np.where(same,0,last);middle=active-first-last
 eps=rng.choice(resid,n,p=rw);level=rng.choice(means,n,p=mw);startlevel=np.where(same,(x0+x1)/2,x0)
 return (.5*(x0+x1)+first*np.maximum(0,startlevel+eps)+last*np.maximum(0,x1+eps)+middle*level)*hours/k/1000

def main():
 global N
 ap=argparse.ArgumentParser();ap.add_argument('--package',required=True);ap.add_argument('--stage',choices=['historical','count','score'],required=True);ap.add_argument('--sensitivity',action='store_true');ap.add_argument('--out',required=True);ap.add_argument('--simulations',type=int,default=10000);args=ap.parse_args();N=args.simulations;assert N>0;out=Path(args.out);out.mkdir(exist_ok=True,parents=True)
 g=ref.load(args.package);g=g[g.date<CUTOFF].reset_index(drop=True) if args.stage=='historical' else g;v=g.normalized_value.to_numpy();ns=g.t.astype('int64').to_numpy();dates=g.date.to_numpy();assert (v>=0).all()
 # Timestamp-only selection before any target truth or score.
 target=[]; bad0=np.r_[0,np.cumsum((np.diff(ns)<4.5*60e9)|(np.diff(ns)>5.5*60e9))]
 for day,idx in g.groupby('date').indices.items():
  if day<('2026-07-01' if args.stage=='historical' else '2026-11-25') or len(idx)<280:continue
  local=g.local.iloc[idx];hh=local.dt.hour.to_numpy()+local.dt.minute.to_numpy()/60+local.dt.second.to_numpy()/3600+local.dt.microsecond.to_numpy()/3.6e9
  for dur in (255,300,360,420,480):
   for band in (0,12):
    for src in ((480,465) if dur==480 else (dur,)):
     k=src//5;x=idx[:-k];y=idx[k:];real=(ns[y]-ns[x])/60e9
     ok=np.flatnonzero((y==x+k)&(bad0[y]==bad0[x])&(hh[:-k]>=band)&(hh[k:]<band+12)&(real>240)&(real<=480))
     if not len(ok):continue
     i,j=x[ok[0]],y[ok[0]];hrs=(ns[j]-ns[i])/3.6e12;target.append(dict(date=day,duration=dur,source_duration=src,band=band,active=bool(v[i]>100),boundaries=int(v[i]>0)+int(v[j]>0),bridge=(v[i]+v[j])/2000*hrs,hours=hrs,start=ns[i],end=ns[j]));break
 r4=pd.DataFrame(target,columns=['date','duration','source_duration','band','active','boundaries','bridge','hours','start','end'])
 if args.stage!='historical':
  if N!=10000 or args.sensitivity:raise ValueError('frozen prospective settings required')
  s=dict(status='INSUFFICIENT',stop_date=None,active_days=0,active_cases=0,groups=[0,0])
  for day in sorted(r4.date.unique()):
   x=r4[(r4.date<=day)&r4.active];groups=[int((x.duration<=360).sum()),int((x.duration>360).sum())];days=x.date.nunique();ok=days>=10 and len(x)>=30 and min(groups)>=5
   s=dict(status='READY' if ok else 'INSUFFICIENT',stop_date=day if ok else None,active_days=days,active_cases=len(x),groups=groups)
   if ok:break
  s['package_sha256']=hashlib.sha256(Path(args.package).read_bytes()).hexdigest();(out/'count.json').write_text(json.dumps(s,indent=2));r4.to_csv(out/'eligibility.csv',index=False);print(s,flush=True)
  if args.stage=='count':return
  if s['status']!='READY':raise SystemExit('INSUFFICIENT; no scoring')
  with (out/'score_attempt_lock.json').open('x') as f:json.dump(dict(s,code_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()),f,indent=2)
  r4=r4[r4.date<=s['stop_date']].copy();cut=np.searchsorted(dates,s['stop_date'],side='right');v,ns,dates=v[:cut],ns[:cut],dates[:cut]
 if r4.empty:raise SystemExit('No targets')
 a,b,s,l,r=runs(v,ns);runstart=np.empty(len(v),int);known=np.empty(len(v),bool)
 for x,y,z in zip(a,b,l):runstart[x:y+1]=x;known[x:y+1]=not z
 prefix=np.r_[0,np.cumsum((v[:-1]+v[1:])/2*np.diff(ns)/3.6e15)]
 r4['truth']=np.maximum(0,prefix[np.searchsorted(ns,r4.end)]-prefix[np.searchsorted(ns,r4.start)])
 pref=np.r_[0,np.cumsum((v[:-1]+v[1:])/2*np.diff(ns)/3.6e15)];bad=np.r_[0,np.cumsum((np.diff(ns)<4.5*60e9)|(np.diff(ns)>5.5*60e9))];pos=np.r_[0,np.cumsum(v<=0)];rows=[]
 for dur in ref.DURS:
  k=dur//5;x=np.arange(len(v)-k);y=x+k;ok=(dates[x]==dates[y])&(bad[y]==bad[x])&(pos[y+1]==pos[x]);x=x[ok][::3];y=x+k;hrs=(ns[y]-ns[x])/3.6e12
  ep=((pref[y]-pref[x])*1000/hrs*k-.5*(v[x]+v[y]))/(k-1)-(v[x]+v[y])/2
  rows.append(pd.DataFrame(dict(date=dates[x],duration=dur,residual=ep)))
 donor=pd.concat(rows,ignore_index=True);output=[];fits=[]
 for day,t in r4.groupby('date'):
  cut=np.searchsorted(dates,day);h,hm,agep,means,mw,counts=fit(v[:cut],ns[:cut],dates[:cut]);fits.append(dict(date=day,inactive_complete=counts[0][0],inactive_dates=counts[0][1],positive_complete=counts[1][0],positive_dates=counts[1][1]));prior=donor[donor.date<day];cache={};fb={}
  for z in t.itertuples():
   i=np.searchsorted(ns,z.start);j=np.searchsorted(ns,z.end);assert ns[i]==z.start and ns[j]==z.end and bad[i]==bad[j];assert abs(pref[j]-pref[i]-z.truth)<1e-7
   k=j-i;group=0 if z.duration<=45 else 1 if z.duration<=180 else 2
   if group not in cache:
    dd=prior[(prior.duration<=45) if group==0 else ((prior.duration>45)&(prior.duration<=180)) if group==1 else (prior.duration>180)]
    if dd.date.nunique()<10:dd=prior
    cache[group]=(dd.residual.to_numpy(),ref.wt(dd)) if dd.date.nunique()>=10 else (np.array([]),np.array([]))
   residual,rw=cache[group];rec=dict(date=day,duration=z.duration,source_duration=z.source_duration,age_known=bool(known[i]),age_steps=int(i-runstart[i]+1),band=z.band,active=z.active,boundaries=z.boundaries,truth=z.truth,bridge=z.bridge,hours=z.hours,start=z.start,end=z.end)
   for name,H in [('R5_semimarkov',h),('Markov_control',hm)]+([('R5_age_min',h),('R5_no_residual',h)] if args.sensitivity else []):
    key=(name,k,int(v[j]>0))
    if key not in fb:fb[key]=backward(H,k,key[-1])
    base=dict(rec,model=name,status='OK')
    if min(c[1] for c in counts)<10 or len(residual)==0 or len(means)==0:output.append(dict(base,status='INSUFFICIENT'));continue
    rng=np.random.default_rng(ref.seed('R5-dev-v1',name if name in ('R5_semimarkov','Markov_control') else 'R5_semimarkov',z.start,z.end,N));draw=simulate(H,agep,means,mw,np.array([0.]) if name=='R5_no_residual' else residual,np.array([1.]) if name=='R5_no_residual' else rw,k,i-runstart[i]+1,known[i] or name=='R5_age_min',v[i],v[j],z.hours,rng,fb[key],n=N);output.append(dict(base,status='INSUFFICIENT') if draw is None else ref.metrics(base,np.quantile(draw,[.05,.5,.95]),z.truth))
  if len(fits)%20==0:print('dates',len(fits),flush=True)
 cases=pd.DataFrame(output);cases.to_csv(out/'cases.csv',index=False);pd.DataFrame(fits).to_csv(out/'fit_support.csv',index=False);s=ref.summary(cases);
 if True:
  extra=[]
  for (name,dur),p in cases[cases.status=='OK'].groupby(['model','duration']):
   for label,x in [('all',p),('active',p[p.active]),('positive',p[p.truth>0])]:
    if len(x):extra.append(dict(model=name,stratum=f'{dur}_{label}',n=len(x),days=x.date.nunique(),coverage=x.covered.mean(),mae=x.ae.mean(),width=x.width.mean(),score=x.score.mean(),bias=x.err.mean(),bridge_mae=(x.bridge-x.truth).abs().mean()))
  s=pd.concat([s,pd.DataFrame(extra)],ignore_index=True)
 s.to_csv(out/'summary.csv',index=False);print(s[['model','stratum','n','days','coverage','mae','width','score']].to_string(index=False));print(cases.groupby(['model','status']).size());print('package_sha256',hashlib.sha256(Path(args.package).read_bytes()).hexdigest())
 # Acceptance criteria fixed before prospective scoring; sensitivity variants are descriptive.
 r=cases[cases.model=='R5_semimarkov'];m=cases[cases.model=='Markov_control'];a=r[r.status=='OK'];b=m[m.status=='OK'];p=a.merge(b,on=['start','end'],suffixes=('_r','_m'));gate={}
 for label,x,th in [('global',a,.85),('active',a[a.active],.85),('inactive',a[~a.active],.85),('positive',a[a.truth>0],.85),('lower',a[a.duration<=360],.80),('upper',a[a.duration>360],.80)]:gate[label]=bool(len(x)>=10 and x.covered.mean()>=th)
 gate['availability']=bool(len(r)>0 and len(a)/len(r)>=.95);gate['paired']=bool(len(a)>0 and len(p)/len(a)>=.95)
 q=p[p.active_r] if len(p) else p
 gate['global_score']=bool(len(p)>0 and p.score_r.mean()<=1.10*p.score_m.mean());gate['active_score']=bool(len(q)>=10 and q.score_r.mean()<=q.score_m.mean());gate['mae_vs_bridge']=bool(len(a)>0 and a.ae.mean()<=1.2*(a.bridge-a.truth).abs().mean())
 (out/'gates.json').write_text(json.dumps(dict(gates=gate,all_pass=all(gate.values()),structural_review='REQUIRED'),indent=2));print(gate,flush=True)
if __name__=='__main__':main()

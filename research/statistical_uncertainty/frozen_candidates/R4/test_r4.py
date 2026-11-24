"""Synthetic mechanical checks, plus optional historical parity."""
import sys
from types import SimpleNamespace
from pathlib import Path
import numpy as np
import pandas as pd
import r4_frozen as r

def main():
 a=np.array([1.,2.]);w=np.array([.5,.5]);p=(None,.8,None,None,a,w,None,None,None)
 target=SimpleNamespace(boundaries=0,bridge_rate=0,hours=1)
 np.testing.assert_array_equal(r.prediction(p,target),[0,0,2])
 assert r.prediction((None,None,None,None,a,w,None,None,None),target) is None
 x=r.draw(p,target,np.random.default_rng(42));assert abs((x==0).mean()-.8)<.01 and np.all(x>=0)
 dates=pd.date_range('2026-11-25',periods=10).strftime('%Y-%m-%d')
 t=pd.DataFrame([(d,True,j%3) for d in dates for j in range(8)],columns=['date','active','group'])
 assert r.count(t.iloc[:-1])['status']=='INSUFFICIENT';assert r.count(t)['stop_date']=='2026-12-04'
 # A sampling period slightly above 5 min makes a nominal 240m exceed 240m.
 ts=pd.date_range('2026-11-25 00:00',periods=288,freq='301s',tz='America/Santiago')
 g=pd.DataFrame(dict(t=ts.tz_convert('UTC'),local=ts,date=ts.strftime('%Y-%m-%d'),normalized_value=500.))
 _,a=r.windows(g,False,True);_,b=r.windows(g,True,True)
 cols=['date','duration','band','start','end','source_duration'];pd.testing.assert_frame_equal(a[cols].reset_index(drop=True),b[cols].reset_index(drop=True));assert (a[a.duration==240].source_duration==225).all() and len(a[a.duration==240])>0
 assert (a.hours*60<=240).all()
 if len(sys.argv)>1:
  old=pd.read_csv(sys.argv[1]);new=pd.read_csv(Path(__file__).parent/'results/historical_cases.csv');old=old[old.model=='hurdle_v3'];new=new[new.model=='R4'];keys=['date','duration','band'];p=old.merge(new,on=keys,validate='one_to_one');assert len(p)==len(old)==len(new)==2490
  for k in ['truth','p5','p50','p95','score']:np.testing.assert_array_equal(p[k+'_x'],p[k+'_y'])
 print('PASS: mixture quantiles, simulation, abstention, stopping rule, duration guard, count/score target parity, optional historical parity')
if __name__=='__main__':main()

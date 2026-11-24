import numpy as np
from r5_bridge import CAP, backward, simulate, runs

def test():
 h=np.full((2,CAP),.02);p=np.ones_like(h);a=np.array([500.,1500.]);w=np.array([.5,.5]);e=np.array([-100.,100.]);
 for x,y in [(0,0),(1000,0),(0,1000),(1000,2000)]:
  f=backward(h,12,int(y>0));z=simulate(h,p,a,w,e,w,12,5,True,x,y,1.,np.random.default_rng(42),f,n=1000)
  assert np.isfinite(z).all() and (z>=.5*(x+y)/12/1000-1e-12).all()
  assert np.array_equal(z,simulate(h,p,a,w,e,w,12,5,True,x,y,1.,np.random.default_rng(42),f,n=1000))
 # gap splits episodes and must censor both sides; transitions on regular links are observed
 a,b,s,l,r=runs(np.array([0.,0.,100.,100.,0.]),np.array([0,5,10,40,45],dtype=np.int64)*60_000_000_000)
 assert list(a)==[0,2,3,4] and list(l)==[True,False,True,False] and list(r)==[False,True,False,True]
 print('PASS: four endpoint combinations, nonnegative energy, reproducibility and episode censoring')
if __name__=='__main__':test()

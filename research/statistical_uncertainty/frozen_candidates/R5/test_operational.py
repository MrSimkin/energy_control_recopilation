import subprocess,sys,tempfile,json
from pathlib import Path
import numpy as np,pandas as pd
from r5_frozen import runs,backward,simulate,CAP
# Core consistency plus long real duration check and first stopping day via synthetic target fixture.
# The main uses timestamp-only selection; verify its results through a synthetic Research Exporter fixture.
import zipfile,hashlib,io
with tempfile.TemporaryDirectory() as tmp:
 p=Path(tmp);z=p/'fixture.zip';frames=[]
 for day in pd.date_range('2026-11-25',periods=11,tz='America/Santiago'):
  for t in pd.date_range(day,periods=288,freq='5min'):
   frames.append((t.tz_convert('UTC').isoformat(),'grid_import_power_w',500,'CONFIRMED'))
 raw=pd.DataFrame(frames,columns=['recorded_at_utc','metric_key','normalized_value','confidence']).to_csv(index=False).encode();m={'timezone':'America/Santiago','files':[{'Name':'normalized_metrics.csv','Bytes':len(raw),'Sha256':hashlib.sha256(raw).hexdigest()}]}
 with zipfile.ZipFile(z,'w') as f:f.writestr('normalized_metrics.csv',raw);f.writestr('manifest.json',json.dumps(m))
 subprocess.run([sys.executable,'r5_frozen.py','--stage','count','--package',str(z),'--out',str(p/'out')],check=True)
 s=json.loads((p/'out/count.json').read_text());t=pd.read_csv(p/'out/eligibility.csv');assert s['status']=='READY' and s['stop_date']=='2026-12-04';assert (t.hours*60>240).all() and (t.hours*60<=480).all();assert 'truth' not in t and 'p50' not in t;assert not t.duplicated(['date','duration','band']).any()
 print('PASS: synthetic timestamp-only selection, actual duration, first stop, no outcomes')

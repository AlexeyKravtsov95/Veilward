"""Sequential actual EditMode class runs; API is partial-match, not regex/OR."""
import json,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent/'reports';UNITY=str(Path.home()/'.unity/bin/unity')
base=[UNITY,'command'];common=['--caller','plugin','--skill','unity-cli','--project-path',str(ROOT),'--format','json']
rows=[]
for name in ('BattleCheckpointControllerTests','BattleReactionEventTests','ElementProgressionControllerTests','NativeEightWaveFileTests','EnemyModelAssetTests'):
 command=base+['test run']+common+['--mode','editmode','--filter',name,'--async-tests','true']
 result=subprocess.run(command,cwd=ROOT,capture_output=True,text=True,check=True);(OUT/(name+'-start.json')).write_text(result.stdout)
 deadline=time.monotonic()+180
 while True:
  status=subprocess.run(base+['test status']+common,cwd=ROOT,capture_output=True,text=True,check=True);r=json.loads(status.stdout)
  if r.get('status')=='completed':break
  assert time.monotonic()<deadline,name+' timed out'
  time.sleep(.5)
 (OUT/(name+'-final.json')).write_text(status.stdout)
 s=r['summary'];assert s['total']>0 and s['passed']==s['total'] and all(s[k]==0 for k in ('failed','skipped','inconclusive')),(name,s,[x for x in r.get('results',[]) if x['Status']!='Passed'])
 rows.append(dict(name=name,summary=s,command=command));print(name,s,flush=True)
(OUT/'fixed-focused-classes.json').write_text(json.dumps(rows,indent=2)+'\n')

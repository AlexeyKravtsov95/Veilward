"""Small in-memory positive/negative controls. No Unity, source writes, project copies or personal prerequisites."""
import contextlib
import copy
import importlib.util
import io
import sys
sys.dont_write_bytecode=True
from pathlib import Path
spec=importlib.util.spec_from_file_location('delivery',Path(__file__).with_name('verify_delivery.py'))
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)

def rejected(name, action):
 try:action()
 except (ValueError,AssertionError,KeyError):return name
 raise AssertionError('tamper accepted: '+name)

def main():
 with contextlib.redirect_stdout(io.StringIO()):v.main()
 controls=[]
 suite=v.read('reports/edit-mode.json')
 for name,mutate in [('empty',lambda r:r['Summary'].update(Total=0,Passed=0)),
                     ('skipped',lambda r:r['Summary'].update(Passed=r['Summary']['Passed']-1,Skipped=1)),
                     ('filtered',lambda r:r.update(FilterApplied='Vfx1B')),
                     ('per-case failure',lambda r:r['Results'][0].update(Status='Failed'))]:
  bad=copy.deepcopy(suite);mutate(bad);controls.append(rejected(name,lambda:v.positive_suite(bad)))
 p=v.read('reports/video-proof.json');source=v.read('reports/video-source-frames.json');shots=v.read('reports/video-shots.json');decoded=v.read('reports/video-decoded.json')
 for name,mutate in [
  ('missing TIDE motion',lambda rows:[shot.update(kind='None') for row in rows for shot in row['shots'] or [] if shot['kind']=='Tide']),
  ('fake test clock',lambda rows:rows[0].update(captureFramerate=30)),
  ('Update disabled',lambda rows:next(r for r in rows if r['shots'] is not None).update(productionUpdate=False)),
  ('third status slot',lambda rows:next(c for r in rows for c in r['cues'] or []).update(slots=3)),
  ('pause geometry drift',lambda rows:next(s for r in reversed(rows) if r['paused'] for s in r['shots'] if s['remaining']>0).update(head='(999,999,999)'))]:
  bad=copy.deepcopy(shots);mutate(bad);controls.append(rejected(name,lambda:v.video(p,source,bad,decoded)))
 controls.append(rejected('source hash',lambda:v.exact_files({'Assets/_Game/Scripts/Presentation/BasicStrikeView.cs':'0'*64},'negative control')))
 old=v.tree
 def tampered(ref):
  out=old(ref)
  if ref=='HEAD':out['Assets/_Game/Scripts/Domain/BattleSimulation.cs']='0'*40
  return out
 v.tree=tampered
 try:controls.append(rejected('protected Git blob',lambda:v.main()))
 finally:v.tree=old
 oldread=v.read
 def bad_render(path):
  value=oldread(path)
  if path=='reports/actual-render-inputs.json':value['capacity']=13
  return value
 v.read=bad_render
 try:controls.append(rejected('actual pool cap',lambda:v.main()))
 finally:v.read=oldread
 print('PASS:',len(controls),'negative controls rejected; read-only positive delivery PASS; no project copy')
if __name__=='__main__':main()

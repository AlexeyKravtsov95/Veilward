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
 # Review regressions: absence/reorder/substitution must fail despite valid counts/PTS.
 bad=copy.deepcopy(p);bad['picks']={}
 controls.append(rejected('required PNG set',lambda:v.video(bad,source,shots,decoded)))
 controls.append(rejected('telemetry order',lambda:v.video(p,source,list(reversed(shots)),decoded)))
 bad=copy.deepcopy(p);bad['picks']['video-pause.png']=copy.deepcopy(p['picks']['video-menu.png'])
 controls.append(rejected('system image stage substitution',lambda:v.video(bad,source,shots,decoded)))
 bad=copy.deepcopy(p);bad['picks']['tide-flight.png']['event']['enemy']+=999
 controls.append(rejected('attack image event substitution',lambda:v.video(bad,source,shots,decoded)))
 for name,mutate in [('duplicate test names',lambda r:r.update(results=[r['results'][0]]*r['summary']['total'])),
                     ('running suite',lambda r:r.update(status='running'))]:
  bad=v.read('reports/play-mode.json');mutate(bad);controls.append(rejected(name,lambda:v.positive_suite(bad,'PlayMode')))
 base_report=v.read('reports/before-gale.json');raw=v.gzip.decompress((v.PACKET/'reports/before-gale.trace.gz').read_bytes())
 for name,mutate in [('wrong replay start',lambda r:r.update(start='flame')),
                     ('wrong rawBytes',lambda r:r.update(rawBytes=r['rawBytes']-1)),
                     ('wrong trace digest',lambda r:r.update(traceSha256='0'*64)),
                     ('incomplete Victory8',lambda r:r['waves'][-1].update(phase='WaveComplete'))]:
  bad=copy.deepcopy(base_report);mutate(bad);controls.append(rejected(name,lambda:v.replay('gale',bad,bad,raw,raw)))
 for name,field,value in [('empty source inventory','source',{}),('empty artifact inventory','artifacts',{}),
                         ('invalid evidence commit','evidenceSource','invalid'),('wrong evidence anchor','evidenceSource',v.BASE)]:
  original=v.read
  def read(path):
   result=original(path)
   if path=='manifest.json':result[field]=value
   return result
  v.read=read
  try:controls.append(rejected(name,lambda:v.main()))
  finally:v.read=original
 original=v.read
 def bad_harness(path):
  result=original(path)
  if path=='reports/media-source.json':result['harnesses']['ArtSource/Review/RecordBasicVfx.cs']='0'*64
  return result
 v.read=bad_harness
 try:controls.append(rejected('capture harness receipt hash',lambda:v.main()))
 finally:v.read=original
 camera=v.read('reports/actual-render-inputs.json')['productionCamera']
 hidden=next(shot for shot in shots[70]['shots'] if shot['kind']=='Gale' and shot['impact'])
 controls.append(rejected('occluded impact rejected',lambda:v.require(v.visible_impact_pixels(v.PACKET/'review-r1/gale-impact.png','Gale',hidden['target'],camera)>=3,'occluded impact')))
 print('PASS:',len(controls),'negative controls rejected; read-only positive delivery PASS; no project copy')
if __name__=='__main__':main()

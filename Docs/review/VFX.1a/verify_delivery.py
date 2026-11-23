"""Read-only delivery audit VFX1a. No Unity, writes, personal-file prerequisite or project copies."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
PACKET = Path(__file__).resolve().parent
BASE = '45efa5676ac5c27ee70d4a13b1fbe2622d3f82f6'
RUNTIME = ('Assets/', 'Packages/', 'ProjectSettings/', 'Scripts/')
# Exact existing-file exceptions; new files still require inventory and Git/artifact hashes.
CHANGES = {
 'Assets/_Game/Scenes/Battle.unity',
 'Assets/_Game/Scripts/Presentation/BattleSceneController.cs',
 'Assets/_Game/Scripts/Presentation/FlameStrikeView.cs',
 'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs',
 'Assets/_Game/Scripts/Editor/BattleAssetValidator.cs',
 'Assets/_Game/Tests/PlayMode/AbilityBossIntegrationPlayModeTests.cs',
 'README.md', 'Docs/TECH_DESIGN.md', 'Docs/ROADMAP.md',
}
CARRY = {
 'Assets/Settings/Mobile_RPAsset.asset':'2d5182d2b8884ac88bc6ed033a0a722c23cfb15a7bc8b7daa38db37227bafd09',
 'Assets/_Game/Art/Materials/GrayboxSelection.mat':'6107f7e1b6deb79ceb4489599637fdb16c5e01870013e0cc3a71d2c50b35ad65',
 'Assets/_Game/Art/Materials/GrayboxTower.mat':'71ced636160a68dca7cf26fd2338913b0d0620aeb891c4ea106d7cacba94f275',
 'Assets/_Game/Art/Materials/GrayboxTowerPreview.mat':'1b49d268ffb66f7e898a81d0a4a7b02e404d14dc7d884cae98cac05bdfa4ce70',
}

def require(ok, why):
 if not ok: raise ValueError(why)
def sha(data): return hashlib.sha256(data).hexdigest()
def blob(data): return hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
def git(*args): return subprocess.check_output(['git', *args], cwd=ROOT)
def read(path): return json.loads((PACKET/path).read_text())
def tree(ref):
 out={}
 for row in filter(None, git('ls-tree','-r','-z',ref).split(b'\0')):
  meta,path=row.split(b'\t');mode,kind,digest=meta.split();require(kind==b'blob','non-file tree');out[path.decode()]=digest.decode()
 return out
def positive_suite(r):
 require(r.get('success',True) is True, 'failed suite envelope')
 s=r.get('Summary',r.get('summary',{}));s={k.lower():v for k,v in s.items()}
 require(all(type(s.get(k)) is int and s[k]>=0 for k in ('total','passed','failed','skipped','inconclusive')), 'integer suite counts')
 require(s['total']>0 and s['passed']==s['total'] and s['failed']==s['skipped']==s['inconclusive']==0, 'nonempty full suite PASS')
 require(not r.get('filter') and not r.get('FilterApplied'), 'filtered full suite')
 rows=r.get('results',r.get('Results',[]));require(len(rows)==s['total'] and all(x['Status']=='Passed' for x in rows),'per-case full PASS')
 return s['total']
def exact_files(files, label):
 for p,h in files.items(): require((ROOT/p).is_file() and sha((ROOT/p).read_bytes())==h, label+': '+p)
def video(proof, sources, shots, decoded):
 require(proof['resolution']==[1600,900] and proof['editor'] is True and proof['device'] is False,'actual Editor media context')
 require(proof['sourceFrames']==proof['outputFrames']==len(sources)==len(shots)>100 and proof['speed']==1 and proof['splices']==0,'complete continuous video')
 pts=[float(f['best_effort_timestamp_time']) for f in decoded['frames']]
 require(len(pts)==len(sources) and all(b>a for a,b in zip(pts,pts[1:])), 'strict advancing decoded PTS')
 require(len({f['frame'] for f in sources})==len(sources), 'duplicate captured frame')
 require(max(abs(t-(f['elapsed']-sources[0]['elapsed'])) for t,f in zip(pts,sources))<.002,'video speed/source PTS mismatch')
 require(decoded['streams'][0]['width']==1600 and decoded['streams'][0]['height']==900,'original GameView aspect')
 require(any('Pause / frozen' in s['stage'] and s['paused'] for s in shots),'pause proof')
 frozen=[s for s in shots if s['stage']=='Pause / frozen FLAME flight']
 require(len(frozen)>=2 and all(s['tick']==frozen[0]['tick'] and s['shots']==frozen[0]['shots'] for s in frozen),'geometry changed during pause')
 for level in (1,2):
  active=[v for s in shots for v in s['shots'] or [] if v['level']==level and v['remaining']>0]
  require(any(v['flying'] and v['head']!=v['source'] and v['head']!=v['target'] for v in active), 'intermediate L'+str(level))
  require(any(v['impact'] for v in active), 'impact L'+str(level))
 clean=[s for s in shots if s['stage'].startswith('Continue / Preparation2')]
 require(clean and all(all(v['remaining']==0 and v['enemy']==0 for v in s['shots']) for s in clean),'Continue ghosts')
 require(any('Death' in r[11] for r in [x.split('|') for x in (PACKET/'reports/video-frames.txt').read_text().splitlines()]),'actual death frame')

def main(preservation=False):
 m=read('manifest.json');require(m['baseline']==BASE and set(m['changedExisting'])==CHANGES,'scope exceptions changed')
 current=tree('HEAD');base=tree(BASE);tested=tree(m['testedSource']);media=tree(m['mediaSource'])
 require(set(current)==set(m['trackedInventory']), 'tracked inventory drift')
 for p,h in base.items():
  if p in CHANGES or p in CARRY:continue
  require(p in current and current[p]==h and blob((ROOT/p).read_bytes())==h, 'protected input drift: '+p)
 for p,h in CARRY.items():
  require(base[p]==tested[p]==media[p]==current[p], 'committed user carry-in: '+p)
 exact_files(CARRY,'actual rendering carry-in')
 exact_files(m['source'],'source'); exact_files(m['artifacts'],'artifact')
 for p in m['source']:
  require(p in current and (p in CARRY or blob((ROOT/p).read_bytes())==current[p]),'source Git blob mismatch '+p)
  if p.startswith(RUNTIME) and p not in CARRY:
   require(current[p]==tested.get(p)==media.get(p), 'tested/media source mismatch '+p)
 # Personal files are neither delivery prerequisites nor allowed arbitrary source inventory.
 private=read('reports/local-preservation.json')
 for row in filter(None,git('status','--porcelain=v1','-z','--untracked-files=all').split(b'\0')):
  p=row.decode()[3:];require(p in CARRY or p in private or p in ('Docs/review/VFX.1a/reports/delivery.json',), 'unexpected dirty/untracked input: '+p)
 if preservation:exact_files(private,'explicit local preservation')
 gates=read('reports/gates-byte-audit.json')
 require(gates['testedSource']==m['testedSource'] and gates['allOriginalInputsRestored'] is True and gates['unexpected']==[] and gates['newInputs']==[], 'gate input/source restoration')
 require(gates['commandDiagnostics']==[] and gates['editorSceneDetached'] is True and gates['editorInputsRefreshed'] is True and gates['recoveryNeeded'] is False,'gate Editor recovery')
 require(gates['results']=={'static':0,'edit-mode':0,'play-mode':0},'full quality gates')
 totals={mode:positive_suite(read('reports/'+mode+'.json')) for mode in ('edit-mode','play-mode')}
 require(totals==m['totals'], 'suite totals drift')
 for start in ('flame','gale'):
  require(read('reports/before-'+start+'.json')==read('reports/after-'+start+'.json'), 'whole replay report changed '+start)
  a=gzip.decompress((PACKET/('reports/before-'+start+'.trace.gz')).read_bytes());b=gzip.decompress((PACKET/('reports/after-'+start+'.trace.gz')).read_bytes())
  require(a==b and len(a)>100000, 'complete event trace changed '+start)
 actual=read('reports/actual-render-inputs.json')
 require(actual['actualPlayMode'] is True and actual['capacity']==8 and actual['renderers']==actual['slots']==104,'actual bounded pool')
 require(actual['graphics']=='Metal' and actual['quality']=='Mobile' and actual['pipelinePath']=='Assets/Settings/Mobile_RPAsset.asset' and actual['pipelineSerialized'], 'actual rendering inputs')
 require(len(actual['materials'])==2 and all(x['shader']=='Veilward/FlameImpulse' and x['path'] and x['passes']==1 for x in actual['materials']),'actual shared projectile material inputs')
 exact_files(actual['dependencies'],'actual loaded dependency')
 require(read('reports/video-completion.json')['success'] is True and read('reports/earned-combat-completion.json')['success'] is True,'actual media flows completed')
 l3=read('reports/earned-l3-proof.json')
 require(l3['success'] is True and l3['level']==3 and l3['liveResourceMutation'] is False and l3['testClock'] is True and l3['domainUnchangedAtArrival'] is True,'earned L3 cosmetic flight proof')
 video(read('reports/video-proof.json'),read('reports/video-source-frames.json'),read('reports/video-shots.json'),read('reports/video-decoded.json'))
 require((PACKET/'reports/video-safe-wire.txt').read_bytes()==(PACKET/'reports/video-rollback-wire.txt').read_bytes(),'whole snapshot/Grid Continue')
 proof=read('reports/video-proof.json');require(sha((PACKET/'editor-flow.mp4').read_bytes())==proof['outputSha256'],'video bytes')
 for name,pick in proof['picks'].items():require(sha((PACKET/'playmode'/name).read_bytes())==pick['sha256'],'exact source PNG pick '+name)
 compile=read('reports/compile.json');require(compile.get('failed') is False and compile.get('compilationFailed',False) is False and compile.get('status') in ('completed','up_to_date'),'final compile')
 require(read('reports/console.json')['groundTruth']['consoleErrors']==0,'final console errors')
 print(json.dumps(dict(status='PASS',sourceFiles=len(m['source']),protectedFiles=len(base)-len(CHANGES)-len(CARRY),artifacts=len(m['artifacts']),totals=totals,testedSource=m['testedSource'],mediaSource=m['mediaSource'],localPreservation='PASS' if preservation else 'NOT_REQUESTED'),ensure_ascii=False))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check-local-preservation',action='store_true');a=p.parse_args()
 try:main(a.check_local_preservation)
 except (ValueError,KeyError,AssertionError) as e:print('FAIL:',e,file=sys.stderr);sys.exit(1)

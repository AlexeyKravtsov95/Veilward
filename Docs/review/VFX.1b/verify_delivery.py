"""Read-only delivery audit VFX1b. No Unity, writes, personal-file prerequisite or project copies."""
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
BASE = '8a9e3cb172ab6abe55efafbbf1232a2a5b53912d'
RUNTIME = ('Assets/', 'Packages/', 'ProjectSettings/', 'Scripts/')
# Exact existing-file exceptions; new files still require inventory and Git/artifact hashes.
CHANGES = {
 'Assets/_Game/Prefabs/Shade.prefab',
 'Assets/_Game/Prefabs/ART5/P_Enemy_RiftWarden.prefab',
 'Assets/_Game/Scripts/Presentation/BattleSceneController.cs',
 'Assets/_Game/Scripts/Presentation/EnemyStatusView.cs',
 'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs',
 'Assets/_Game/Scripts/Editor/BattleAssetValidator.cs',
 'Assets/_Game/Tests/EditMode/ProductionFlameProjectileTests.cs',
 'Assets/_Game/Tests/PlayMode/AbilityBossIntegrationPlayModeTests.cs',
 'README.md','Docs/TECH_DESIGN.md','Docs/ROADMAP.md',
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
def read(path):
 p=PACKET/path
 return json.loads(p.read_text() if p.is_file() else gzip.decompress(p.with_suffix(p.suffix+'.gz').read_bytes()))
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
 for p,h in files.items():
  path=ROOT/p
  # Unity exposes virtual Packages paths; read the exact locked version in this checkout's Library.
  if not path.is_file() and p.startswith('Packages/com.'):
   package,relative=p[len('Packages/'):].split('/',1)
   locked=json.loads((ROOT/'Packages/packages-lock.json').read_text())['dependencies'][package]['version']
   candidates=[q/relative for q in (ROOT/'Library/PackageCache').glob(package+'@*') if (q/'package.json').is_file() and json.loads((q/'package.json').read_text())['version']==locked]
   require(len(candidates)==1,'unambiguous locked package cache: '+p)
   path=candidates[0]
  require(path.is_file() and sha(path.read_bytes())==h, label+': '+p)
def video(proof, sources, shots, decoded):
 require(proof['resolution']==[1600,900] and proof['editor'] is True and proof['device'] is False,'actual Editor media context')
 require(proof['sourceFrames']==proof['outputFrames']==len(sources)==len(shots)>100 and proof['speed']==1 and proof['splices']==0,'complete continuous video')
 pts=[float(f['best_effort_timestamp_time']) for f in decoded['frames']]
 require(len(pts)==len(sources) and all(b>a for a,b in zip(pts,pts[1:])), 'strict advancing decoded PTS')
 require(len({f['frame'] for f in sources})==len(sources), 'duplicate captured frame')
 require(max(abs(t-(f['elapsed']-sources[0]['elapsed'])) for t,f in zip(pts,sources))<.002,'video speed/source PTS mismatch')
 require(decoded['streams'][0]['width']==1600 and decoded['streams'][0]['height']==900,'original GameView aspect')
 require(all(s['timeScale']==1 and s['captureFramerate']==0 for s in shots),'realtime clock contract')
 live=[s for s in shots if s['shots'] is not None]
 require(live and all(s['productionUpdate'] for s in live),'production Update disabled during recording')
 frozen=[s for s in shots if s['stage']=='Pause / frozen basic shot and status geometry']
 require(len(frozen)>=2 and all(s['paused'] and s['tick']==frozen[0]['tick'] and s['shots']==frozen[0]['shots'] and s['cues']==frozen[0]['cues'] for s in frozen),'geometry/status changed during pause')
 for kind in ('Tide','Gale','Stone'):
  active=[v for s in shots if not s['paused'] for v in s['shots'] or [] if v['kind']==kind and v['remaining']>0]
  require(any(v['flying'] and v['head']!=v['source'] and v['head']!=v['target'] for v in active),'intermediate '+kind)
  require(any(v['impact'] for v in active),'impact '+kind)
  events={(v['tower'],v['enemy'],v['attackTick']) for v in active}
  require(len(events)>=2,'actual cadence '+kind)
 for kind in ('Wet','Gust','Crack'):
  require(any(c['first']==kind or c['second']==kind for s in shots for c in s['cues'] or []),'live status '+kind)
 require(all(c['slots']<=2 for s in shots for c in s['cues'] or []),'extra status slots')
 clean=[s for s in shots if s['stage'].startswith('Continue8 / clean')]
 require(clean and all(all(v['remaining']==0 and v['enemy']==0 for v in s['shots']) and not s['cues'] for s in clean),'Continue ghosts')
 for name,pick in proof['picks'].items():
  source=sources[pick['manifestIndex']]
  require(source['sha256']==pick['sha256'] and source['frame']==pick['gameFrame'] and source['stage']==pick['stage'],'PNG/source attribution '+name)

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
 private=read('reports/initial-audit.json')['untrackedPreservation']
 for row in filter(None,git('status','--porcelain=v1','-z','--untracked-files=all').split(b'\0')):
  p=row.decode()[3:];require(p in CARRY or p in private or p in ('Docs/review/VFX.1b/reports/delivery.json',), 'unexpected dirty/untracked input: '+p)
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
 require(actual['actualPlayMode'] is True and actual['capacity']==12 and actual['renderers']==actual['slots']==48,'actual bounded new pool')
 require(actual['flameRenderers']==104 and actual['statusSlotsPerOwner']==2 and actual['statusGraphics']==2*actual['liveStatusOwners'],'unchanged FLAME / two status graphics per live owner')
 require(actual['graphics']=='Metal' and actual['quality']=='Mobile' and actual['pipelinePath']=='Assets/Settings/Mobile_RPAsset.asset' and actual['pipelineSerialized'],'actual rendering inputs')
 require(len(actual['materials'])==1 and actual['materials'][0]['path']=='Assets/_Game/Art/Materials/ReactionEffect.mat' and actual['materials'][0]['shader']=='Universal Render Pipeline/Unlit' and actual['materials'][0]['passes']>0,'existing shared material/shader')
 exact_files(actual['dependencies'],'actual loaded dependency')
 require(read('reports/video-completion.json')['success'] is True and read('reports/earned-combat-completion.json')['success'] is True,'actual media flows completed')
 require(read('reports/video-origin.json')['liveResourceMutation'] is False and read('reports/video-origin.json')['testClock'] is False,'earned origin / realtime video')
 require(read('reports/synthetic-statuses.json')['slotsPerOwner']==2,'synthetic four statuses retain two slots')
 video(read('reports/video-proof.json'),read('reports/video-source-frames.json'),read('reports/video-shots.json'),read('reports/video-decoded.json'))
 require((PACKET/'reports/video-safe-wire.txt').read_bytes()==(PACKET/'reports/video-rollback-wire.txt').read_bytes(),'whole snapshot/Grid Continue')
 proof=read('reports/video-proof.json');require(sha((PACKET/'editor-flow.mp4').read_bytes())==proof['outputSha256'],'video bytes')
 for name,pick in proof['picks'].items():require(sha((PACKET/'playmode'/name).read_bytes())==pick['sha256'],'exact source PNG pick '+name)
 compile=read('reports/compile.json');require(compile.get('failed') is False and compile.get('compilationFailed',False) is False and compile.get('status') in ('completed','up_to_date'),'final compile')
 require(read('reports/final-new-errors.json')['returned']==0,'new final console errors')
 print(json.dumps(dict(status='PASS',sourceFiles=len(m['source']),protectedFiles=len(base)-len(CHANGES)-len(CARRY),artifacts=len(m['artifacts']),totals=totals,testedSource=m['testedSource'],mediaSource=m['mediaSource'],localPreservation='PASS' if preservation else 'NOT_REQUESTED'),ensure_ascii=False))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check-local-preservation',action='store_true');a=p.parse_args()
 try:main(a.check_local_preservation)
 except (ValueError,KeyError,AssertionError) as e:print('FAIL:',e,file=sys.stderr);sys.exit(1)

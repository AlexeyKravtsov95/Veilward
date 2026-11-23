"""Read-only ART4 source/artifact/protected-baseline audit. Never runs Unity or rewrites carry-in."""
import argparse,hashlib,json,subprocess,sys,gzip
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
PACKET=Path(__file__).resolve().parent
BASE='80d453fcbf234e46f35f6c07c5018e68c371eb7c'
EXCLUSIONS={
 'Assets/_Game/Scenes/Battle.unity':'ART4 static environment and explicitly measured lighting/background, gameplay refs retained',
 'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs':'Replace only production environment, preserve legacy geometry APIs',
 'README.md':'Current ART4 contract/evidence links',
 'Docs/TECH_DESIGN.md':'Static ART4 boundary and actual input contract',
 'Docs/ROADMAP.md':'ART4 review status, no owner merge claim',
}
CARRY={
 'Assets/Settings/Mobile_RPAsset.asset':'2d5182d2b8884ac88bc6ed033a0a722c23cfb15a7bc8b7daa38db37227bafd09',
 'Assets/_Game/Art/Materials/GrayboxSelection.mat':'6107f7e1b6deb79ceb4489599637fdb16c5e01870013e0cc3a71d2c50b35ad65',
 'Assets/_Game/Art/Materials/GrayboxTower.mat':'71ced636160a68dca7cf26fd2338913b0d0620aeb891c4ea106d7cacba94f275',
 'Assets/_Game/Art/Materials/GrayboxTowerPreview.mat':'1b49d268ffb66f7e898a81d0a4a7b02e404d14dc7d884cae98cac05bdfa4ce70',
}
def require(ok,message):
 if not ok:raise ValueError(message)
def sha(data):return hashlib.sha256(data).hexdigest()
def blob(data):return hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
def digest_files(root,expected,label):
 for path,digest in expected.items():require((root/path).is_file() and sha((root/path).read_bytes())==digest,label+' digest mismatch: '+path)
def protected_files(root,expected):
 for p,d in expected.items():require((root/p).is_file() and blob((root/p).read_bytes())==d,'protected baseline mismatch: '+p)
def local_preservation(root,known,*,enabled):
 # Исторические личные файлы не являются delivery inputs; их bytes читает только явный local audit.
 if not enabled:return dict(status='NOT_REQUESTED',expectedFiles=len(known))
 digest_files(root,known,'local preservation')
 return dict(status='PASS',expectedFiles=len(known))
def dirty_inputs(rows,known):
 for row in rows:require(row[3:] in known,'unexpected dirty/untracked delivery input: '+row)
def inventory(expected,actual,label):require(set(expected)==set(actual),label+' inventory mismatch: '+str(sorted(set(expected)^set(actual))))
def full_pass(report):
 s=report.get('Summary',report.get('summary',{}));s={k.lower():v for k,v in s.items()}
 require(isinstance(s.get('total'),int) and not isinstance(s['total'],bool) and s['total']>0,'positive non-empty suite required')
 require(s.get('passed')==s['total'] and all(s.get(k)==0 for k in ('failed','skipped','inconclusive')),'full positive suite without skips required')
 require(report.get('success',True) is True,'explicit failed envelope')
 require(not report.get('FilterApplied') and not report.get('filter'),'filtered suite cannot be full PASS')
 rows=report.get('Results',report.get('results',[]))
 require(len(rows)==s['total'] and all(r.get('Status')=='Passed' for r in rows),'per-case results mismatch')
 return s['total'],rows
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
def tree(ref):
 rows=git('ls-tree','-r','-z',ref).split(b'\0');out={}
 for row in filter(None,rows):
  meta,path=row.split(b'\t');mode,kind,digest=meta.split();require(kind==b'blob','unexpected tree entry')
  out[path.decode()]=digest.decode()
 return out

def main(*,check_local_preservation=False):
 m=json.loads((PACKET/'manifest.json').read_text())
 require(m['baseline']==BASE and m['protectedExclusions']==EXCLUSIONS,'unexplained baseline exclusions')
 tracked=[p for p in git('ls-files','-z').decode().split('\0') if p]
 inventory(m['trackedInventory'],tracked,'tracked delivery')
 tested=tree(m['testedSource']);media=tree(m['mediaSource'])
 # Media tree is exact except a new test-only aspect/byte guard and removal of six unused Editor writers.
 media_exclusions={'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs','Assets/_Game/Tests/EditMode/BattleEnvironmentTests.cs'}
 def media_path(p):return p.startswith(('Assets/','Packages/','ProjectSettings/','ArtSource/Blender/ART4/')) and p not in media_exclusions
 rendering={p:d for p,d in tested.items() if media_path(p)}
 require(rendering=={p:d for p,d in media.items() if media_path(p)},'media rendering inputs changed after capture')
 old_builder=git('show',m['mediaSource']+':Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs').decode()
 new_builder=git('show',m['testedSource']+':Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs').decode()
 writer_names=('groundMaterial','pathMaterial','spawnMaterial','heartMaterial','availableMaterial','blockedMaterial')
 expected=''.join(line for line in old_builder.splitlines(keepends=True) if not any('var '+name+' = CreateOrUpdateMaterial(' in line for name in writer_names))
 require(new_builder==expected,'unexplained Editor builder media delta')
 source={p for p in tracked if p.startswith(('Assets/','Packages/','ProjectSettings/','ArtSource/','Scripts/'))}
 inventory(m['source'],source,'tested source')
 digest_files(ROOT,m['source'],'source')
 for p in source:
  require(p in tested,'source absent from tested commit: '+p)
  require(blob((ROOT/p).read_bytes())==tested[p] if p not in CARRY else sha((ROOT/p).read_bytes())==CARRY[p],'tested source Git blob mismatch: '+p)
 baseline=tree(BASE);protected={p:d for p,d in baseline.items() if p not in EXCLUSIONS and p not in CARRY}
 require(m['protectedCount']==len(protected),'protected inventory count')
 protected_files(ROOT,protected)
 require(m['carryIn']==CARRY,'carry-in policy changed');digest_files(ROOT,CARRY,'exact user carry-in')
 known=json.loads((PACKET/'reports/carry-in.json').read_text())
 preservation=local_preservation(ROOT,known,enabled=check_local_preservation)
 dirty=[row.decode() for row in git('status','--porcelain=v1','-z','--untracked-files=all').split(b'\0') if row]
 dirty_inputs(dirty,known)
 digest_files(ROOT,m['artifacts'],'artifact')
 for start in ('flame','gale'):
  before=json.loads((PACKET/f'reports/before-{start}.json').read_text());after=json.loads((PACKET/f'reports/after-{start}.json').read_text())
  require(before==after and before['outcome']=='Victory','production replay mismatch')
  for phase in ('before','after'):
   data=gzip.decompress((PACKET/f'reports/{phase}-{start}.trace.gz').read_bytes())
   require(sha(data)==before['traceSha256'] and len(data)==before['rawBytes'],'full replay artifact mismatch')
 require((PACKET/'reports/before-geometry.json').read_bytes()==(PACKET/'reports/after-geometry.json').read_bytes(),'gameplay geometry/camera mapping mismatch')
 before=json.loads((PACKET/'reports/reexport-before.json').read_text());after=json.loads((PACKET/'reports/reexport-after.json').read_text())
 require(before==after and len(before['models'])==6,'real source roundtrip mismatch')
 canonical=json.loads((PACKET/'reports/reexport-canonical-readback.json').read_text());require(canonical==after,'post-suite canonical geometry/ref readback mismatch')
 counts={}
 for suite in ('edit-mode','play-mode'):
  report=json.loads((PACKET/f'reports/{suite}.json').read_text());total,rows=full_pass(report);counts[suite]=total
  require(total==m['suiteTotals'][suite],'suite totals changed')
  require(any('BattleEnvironment' in r['FullName'] for r in rows),'new environment regressions absent')
  require(any('StormModel' in r['FullName'] for r in rows),'sixth accepted family regressions absent')
 compile_audit=json.loads((PACKET/'reports/compile.json').read_text())
 require(compile_audit.get('success') is True and compile_audit['data']['failed'] is False and compile_audit['data']['errors']==[],'compile audit failed')
 classification=json.loads((PACKET/'reports/console-classification.json').read_text());require(not classification['unknown'] and classification['consoleCleared'] is False,'unclassified Console messages')
 audit=json.loads((PACKET/'reports/gates-byte-audit.json').read_text())
 require(audit['testedSource']==m['testedSource'] and audit['allOriginalInputsRestored'] is True and not audit['unexpected'] and not audit['newInputs'],'gates byte restoration incomplete')
 require(all(v==0 for v in audit['results'].values()) and len(audit['results'])==3,'mandatory gate failed')
 actual=json.loads((PACKET/'reports/actual-render-inputs.json').read_text());digest_files(ROOT,actual['inputSha256'],'actual rendering inputs')
 env=actual['environment'];require(env['colliders']==0 and env['runtimeScripts']==0 and env['renderers']<=160 and env['geometryTriangleInstances']<=25000,'environment budgets/ownership')
 input_commands=json.loads((PACKET/'reports/input-commands.json').read_text())
 require(all(r['exit']==0 and not (isinstance(r['result'],dict) and r['result'].get('success') is False) for r in input_commands),'input capture command failed')
 waits=[r['result'] for r in input_commands if r['args'][1:3]==['job','wait']]
 require(len(waits)>=5 and all(w.get('met') is True and w.get('timedOut') is False and w.get('framesObserved',0)>0 for w in waits),'actual input-capture liveness missing')
 proof=json.loads((PACKET/'reports/video-proof.json').read_text());require(proof['speed']==1 and proof['splices']==0 and proof['sourceFrames']==proof['outputFrames']>50,'video integrity')
 require(proof['outputSha256']==sha((PACKET/'editor-flow.mp4').read_bytes()),'video hash')
 probe=json.loads(subprocess.check_output(['ffprobe','-v','quiet','-select_streams','v:0','-show_entries','stream=width,height:frame=best_effort_timestamp_time','-show_frames','-of','json',str(PACKET/'editor-flow.mp4')]))
 pts=[float(f['best_effort_timestamp_time']) for f in probe['frames']]
 require(len(pts)==proof['outputFrames'] and all(b>a for a,b in zip(pts,pts[1:])),'video decoded frame count/PTS')
 require(probe['streams'][0]['width']==1600 and probe['streams'][0]['height']==900,'video camera aspect changed')
 sources=json.loads((PACKET/'reports/video-source-frames.json').read_text())
 require(len(sources)==len(pts) and len({r['frame'] for r in sources})==len(pts),'actual unique player-loop source frames required')
 require(max(abs(p-(r['elapsed']-sources[0]['elapsed'])) for p,r in zip(pts,sources))<.002,'real capture PTS changed')
 picks=proof['picks'];require(len(picks)==7 and len({p['sourceFrameIndex'] for p in picks.values()})==7,'distinct exact-stage picks required')
 for name,p in picks.items():
  require(sha((PACKET/'playmode'/name).read_bytes())==p['sha256'],'video pick pixels mismatch')
  original=sources[p['manifestIndex']]
  require(original['stage']==p['stage'] and original['sha256']==p['sha256'] and int(Path(original['file']).stem)==p['sourceFrameIndex'],'exact stage source pick mismatch')
 require((PACKET/'reports/video-safe-wire.txt').read_bytes()==(PACKET/'reports/video-rollback-wire.txt').read_bytes(),'whole live Grid/snapshot rollback mismatch')
 require(json.loads((PACKET/'reports/video-completion.json').read_text())['success'] is True,'video flow failed')
 require(json.loads((PACKET/'reports/earned-combat-completion.json').read_text())['success'] is True,'earned test-clock flow failed')
 completion=json.loads((PACKET/'reports/earned-combat.json').read_text())
 require(completion['testClock'] is True and completion['liveResourceMutation'] is False and completion['boss'] and completion['castBoss'],'boss evidence contract')
 require(any(r.get('bossDamage',0)>0 for r in completion['rows']),'positive boss ability HP loss missing')
 git('diff','--check',BASE,'HEAD');git('diff','--check')
 receipt=dict(status='PASS',head=git('rev-parse','HEAD').decode().strip(),testedSource=m['testedSource'],source=len(source),protected=len(protected),artifacts=len(m['artifacts']),suites=counts,exactCarryIn=len(CARRY),localPreservation=preservation,device='Deferred QA53/54/55',newUnityRun=False,readOnly=True)
 print(json.dumps(receipt,indent=2));return receipt
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--check-local-preservation',action='store_true',help='Also require all seven historical local user files to exist with their exact original bytes; not needed for delivery verification.')
 args=parser.parse_args()
 try:main(check_local_preservation=args.check_local_preservation)
 except (ValueError,KeyError,OSError,subprocess.CalledProcessError) as error:print('FAIL: '+str(error),file=sys.stderr);sys.exit(1)

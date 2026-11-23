"""Read-only ART5 committed source/artifact audit. No Unity run, project copy or writes."""
import argparse,gzip,hashlib,json,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];PACKET=Path(__file__).resolve().parent
BASE='cb9101c571f1c411464836ea02a1877acc92364d'
EXCLUSIONS={
 'Assets/_Game/Prefabs/Shade.prefab':'Authored child/cached clips/cues replace visible capsule; GUID/HP/status retained',
 'Assets/_Game/Scenes/Battle.unity':'One optional bossPrefab reference; existing scene refs/camera/environment retained',
 'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs':'Bind authored Shade/boss while retaining full ART4/18tower rebuild',
 'Assets/_Game/Scripts/Editor/FlameModelBuilder.cs':'Idempotent multipart enemy cues; existing tower/HP cues retained',
 'Assets/_Game/Scripts/Presentation/BattleSceneController.cs':'Definition-ID boss selection and pause-aware visual-only hit/death/leak lifecycle',
 'Assets/_Game/Tests/EditMode/FlameModelAssetTests.cs':'Superseded exact graybox mesh count only; same mesh/material/HP semantics',
 'Assets/_Game/Tests/EditMode/TideModelAssetTests.cs':'Measure projected authored model and actual count; retain HP2/status2/ratio assertions',
 'README.md':'ART5 scope/evidence links',
 'Docs/TECH_DESIGN.md':'Narrow presentation/animation/lifetime boundary',
 'Docs/ROADMAP.md':'ART5 implementation/review status, no merge claim',
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
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
def read(name):return json.loads((PACKET/name).read_text())
def tree(ref):
 out={}
 for row in filter(None,git('ls-tree','-r','-z',ref).split(b'\0')):
  meta,path=row.split(b'\t');mode,kind,digest=meta.split();require(kind==b'blob','unexpected tree object');out[path.decode()]=digest.decode()
 return out
def inventory(a,b,label):require(set(a)==set(b),label+' inventory mismatch: '+str(sorted(set(a)^set(b))))
def digest_files(root,expected,label):
 for p,h in expected.items():require((root/p).is_file() and sha((root/p).read_bytes())==h,label+' digest mismatch: '+p)
def protected_files(root,expected):
 for p,h in expected.items():require((root/p).is_file() and blob((root/p).read_bytes())==h,'protected baseline mismatch: '+p)
def dirty_inputs(rows,known):
 for row in rows:require(row[3:] in known,'unexpected dirty/untracked delivery input: '+row)
def local_preservation(root,known,*,enabled):
 if not enabled:return dict(status='NOT_REQUESTED',expectedFiles=len(known))
 digest_files(root,known,'local preservation');return dict(status='PASS',expectedFiles=len(known))
def full_pass(report):
 s={k.lower():v for k,v in report.get('Summary',report.get('summary',{})).items()}
 require(all(type(s.get(k)) is int and s[k]>=0 for k in ('total','passed','failed','skipped','inconclusive')) and s['total']>0,'positive exact integer suite counts required')
 require(s['total']==sum(s[k] for k in ('passed','failed','skipped','inconclusive')),'suite count sum mismatch')
 require(s.get('passed')==s['total'] and all(s.get(k)==0 for k in ('failed','skipped','inconclusive')),'full positive suite without skips required')
 require(report.get('success',True) is True,'explicit failed envelope')
 require(not report.get('FilterApplied') and not report.get('filter'),'filtered suite cannot be full PASS')
 rows=report.get('Results',report.get('results',[]));require(len(rows)==s['total'] and all(r.get('Status')=='Passed' for r in rows),'per-case results mismatch');return s['total'],rows
def roundtrip(before,after):
 require(before==after and len(before['models'])==2,'real source roundtrip mismatch')
 require({m['name'] for m in before['models']}=={'Enemy_Shade','Enemy_RiftWarden'},'roster readback mismatch')
 require(sum(len(m['meshes']) for m in before['models'])==8 and sum(len(m['clips']) for m in before['models'])==10,'actual animation/mesh totals')
 for model in before['models']:
  require({c['name'] for c in model['clips']}=={'Idle','Move','Hit','Death','Leak'},'clip roster')
  for c in model['clips']:require(c['keyframes']>0 and c['curves']>=36 and len(c['poses'])==4 and c['legacy'],'sampled animated clip required')
def video_proof(proof):
 require(proof['speed']==1 and proof['splices']==0 and proof['sourceFrames']==proof['outputFrames']>50,'video integrity')
 require(proof['resolution']==[1600,900] and proof['editor'] is True and proof['device'] is False,'actual production camera video')
PICK_STAGES={
 'video-menu.png':'MainMenu / fresh NewGame',
 'video-preview.png':'Build preview / selected socket + range / no purchase',
 'video-combat.png':'Combat1 / production Update / moving Shade + HP + attacks',
 'video-ability.png':'Ability preview / live combat / public Confirm',
 'video-upgrade.png':'Preparation2 / earned gold / Upgrade FLAME L2',
 'video-continue.png':'Continue / restored Preparation2 + FLAME L2 + Grid',
 'video-sold.png':'Sold / public flow complete / no resource substitution',
}
def compile_status(report):
 d=report.get('data',{})
 require(report.get('success') is True and report.get('errors')==[] and d.get('status') in ('up_to_date','completed') and d.get('compiling') is False and d.get('failed') is False and d.get('errors')==[] and d.get('summary',{}).get('errors')==0,'actual compile status failed or incomplete')
def actual_inputs(actual):
 require(actual.get('actualPlayMode') is True and actual.get('unity')=='6000.3.28f1' and actual.get('graphics')=='Metal' and actual.get('quality')=='Mobile' and actual.get('pipelinePath')=='Assets/Settings/Mobile_RPAsset.asset' and isinstance(actual.get('pipelineSerialized'),dict) and bool(actual['pipelineSerialized']),'actual pipeline/runtime context incomplete')
 enemies=actual.get('enemies',[])
 require(len(enemies)==2 and {'Enemy_Shade','Enemy_RiftWarden'}=={Path(e['model']).stem for e in enemies},'both actual model consumers required')
 refs=[]
 for e in enemies:
  require(set(e.get('clips',{}))=={'idle','move','hit','death','leak'} and e['renderers']==8 and len(e.get('sharedMeshes',[]))==8,'actual animated model inputs incomplete')
  for ref in e['clips'].values():
   fields=ref.rsplit(':',2);require(len(fields)==3 and fields[0]==e['model'] and len(fields[1])==32 and int(fields[2])!=0,'null/mismatched actual clip reference');refs.append(ref)
 require(len(set(refs))==10,'ten unique actual animation clip references required')
 materials=actual.get('materials',[])
 require(materials and all(m.get('shader') and type(m.get('passes')) is int and m['passes']>0 for m in materials),'actual loaded material/shader/pass inputs required')
 require({'MAT_Art1_Surface','MAT_Art1_Glow','MAT_Art21_ShadeOcclusion'}<={m['name'] for m in materials},'actual shared enemy materials missing')
def decoded_video(proof,probe,sources):
 video_proof(proof)
 streams=probe.get('streams',[]);require(len(streams)==1 and streams[0]['width']==1600 and streams[0]['height']==900,'decoded video dimensions changed')
 pts=[float(f['best_effort_timestamp_time']) for f in probe['frames']]
 require(len(pts)==len(sources)==proof['outputFrames'] and len({f['frame'] for f in sources})==len(pts) and all(b>a for a,b in zip(pts,pts[1:])),'actual advancing source/video frames')
 require(max(abs(p-(f['elapsed']-sources[0]['elapsed'])) for p,f in zip(pts,sources))<.002,'captured PTS changed')
 return pts
def stage_picks(proof,sources):
 picks=proof.get('picks',{});require(set(picks)==set(PICK_STAGES) and len({p['sourceFrameIndex'] for p in picks.values()})==7,'complete distinct exact stage picks required')
 for filename,pick in picks.items():
  require(type(pick['manifestIndex']) is int and 0<=pick['manifestIndex']<len(sources),'invalid pick index')
  source=sources[pick['manifestIndex']]
  require(pick['stage']==source['stage']==PICK_STAGES[filename] and int(Path(source['file']).stem)==pick['sourceFrameIndex'] and source['frame']==pick['gameFrame'] and source['sha256']==pick['sha256'],'exact stage/index/frame/hash mismatch')
def gate_status(gates,tested_source):
 require(gates.get('testedSource')==tested_source and gates.get('allOriginalInputsRestored') is True and gates.get('unexpected')==[] and gates.get('newInputs')==[],'source byte restoration incomplete')
 require(gates.get('recoveryNeeded') is False and gates.get('commandDiagnostics')==[] and gates.get('editorSceneDetached') is True and gates.get('editorInputsRefreshed') is True,'Editor restoration not verified')
 require(set(gates.get('results',{}))=={'static','edit-mode','play-mode'} and all(type(v) is int and v==0 for v in gates['results'].values()),'mandatory gates failed')
def carry_commit_scope(baseline,tested,media,committed):
 for p in CARRY:require(baseline.get(p) is not None and baseline[p]==tested.get(p)==media.get(p)==committed.get(p),'user rendering carry-in was committed: '+p)
COMBAT_STAGES={'Combat1 / production Update / moving Shade + HP + attacks','Ability preview / live combat / public Confirm','Ability cast / production Combat1 continues','Combat2 / production Update / interruption before Continue'}
def video_initial_and_transition_rows(rows):
 require(rows and len(rows[0])==12,'complete actual video telemetry required')
 first=rows[0]
 require(first[3] in ('fresh MainMenu / NewGame','MainMenu / fresh NewGame') and first[4:9]==['']*5 and first[10]=='' and first[11]=='','initial frame must be clean preloaded MainMenu, not previous combat')
 require(any(r[3] in COMBAT_STAGES and 'Move' in r[11] for r in rows),'production Combat movement missing')
 require(any(r[3] in COMBAT_STAGES and 'Death' in r[11] for r in rows),'production Combat death transition missing')
def main(*,check_local_preservation=False):
 m=read('manifest.json');require(m['baseline']==BASE and m['protectedExclusions']==EXCLUSIONS,'unexplained protected exclusions')
 tracked=[p for p in git('ls-files','-z').decode().split('\0') if p];inventory(m['trackedInventory'],tracked,'tracked Git')
 source={p for p in tracked if p.startswith(('Assets/','Packages/','ProjectSettings/','ArtSource/','Scripts/'))};inventory(m['source'],source,'source')
 tested=tree(m['testedSource']);media=tree(m['mediaSource']);digest_files(ROOT,m['source'],'source')
 for p in source:
  require(p in tested,'source absent from tested commit: '+p)
  require(blob((ROOT/p).read_bytes())==tested[p] if p not in CARRY else sha((ROOT/p).read_bytes())==CARRY[p],'tested source Git blob mismatch: '+p)
  require(tested[p]==media.get(p),'source changed after media: '+p)
 baseline=tree(BASE);committed=tree('HEAD');carry_commit_scope(baseline,tested,media,committed)
 protected={p:h for p,h in baseline.items() if p not in EXCLUSIONS and p not in CARRY}
 require(m['protectedCount']==len(protected),'protected inventory count');protected_files(ROOT,protected)
 require(m['carryIn']==CARRY,'carry policy changed');digest_files(ROOT,CARRY,'exact rendering carry-in')
 known=read('reports/carry-in-sha256.json');preservation=local_preservation(ROOT,known,enabled=check_local_preservation)
 dirty_inputs([r.decode() for r in git('status','--porcelain=v1','-z','--untracked-files=all').split(b'\0') if r],known)
 digest_files(ROOT,m['artifacts'],'artifact')
 require(set(m['artifacts'])=={p for p in tracked if (p.startswith('Docs/review/ART.5/') or p in m['documentation']) and p!='Docs/review/ART.5/manifest.json'},'artifact inventory mismatch')
 for p,h in m['artifacts'].items():require(blob((ROOT/p).read_bytes())==committed.get(p),'artifact not committed: '+p)
 for start in ('flame','gale'):
  a=read('reports/before-'+start+'.json');b=read('reports/after-'+start+'.json');require(a==b and a['outcome']=='Victory','production replay changed')
  for phase in ('before','after'):
   trace=gzip.decompress((PACKET/f'reports/{phase}-{start}.trace.gz').read_bytes());require(sha(trace)==a['traceSha256'] and len(trace)==a['rawBytes'],'full replay trace mismatch')
 require((PACKET/'reports/before-geometry.json').read_bytes()==(PACKET/'reports/after-geometry.json').read_bytes(),'camera/grid/path mapping changed')
 roundtrip(read('reports/reexport-before.json'),read('reports/reexport-after.json'))
 require(read('reports/reexport-canonical-readback.json')==read('reports/reexport-after.json'),'post-suite canonical readback changed')
 commands=read('reports/source-reexport-commands.json');require(len(commands)==2 and {c['source'] for c in commands}=={'Enemy_Shade.blend','Enemy_RiftWarden.blend'} and all(c['exit']==0 for c in commands),'two actual source exports required')
 audit=read('reports/source-audit.json');require(audit['status']=='PASS' and audit['sourceCount']==2 and audit['actualSavedSourceReadback'] is True,'saved source audit required')
 for row in audit['sources']:require(sha((ROOT/row['source']).read_bytes())==row['sha256'] and row['meters']==1 and row['skinBones']==0 and len(row['parts'])==4,'actual editable source mismatch')
 totals={}
 for suite in ('edit-mode','play-mode'):
  total,rows=full_pass(read('reports/'+suite+'.json'));totals[suite]=total;require(total==m['suiteTotals'][suite],'suite count drift')
  for required in ('EnemyModel','BattleEnvironment','StormModel'):require(any(required in r['FullName'] for r in rows),'required regression absent: '+required)
 gates=read('reports/gates-byte-audit.json');gate_status(gates,m['testedSource'])
 require('Ran 38 tests' in (PACKET/'reports/static-command.txt').read_text() and 'Static Unity checks: PASS' in (PACKET/'reports/static-command.txt').read_text(),'static positive38 required')
 actual=read('reports/actual-render-inputs.json');digest_files(ROOT,actual['inputSha256'],'actual rendering inputs');require(actual['actualPlayMode'] is True,'PlayMode inputs required')
 actual_inputs(actual)
 compile_status(read('reports/compile.json'))
 classification=read('reports/console-classification.json');require(not classification['unknown'] and classification['consoleCleared'] is False,'unclassified Console')
 proof=read('reports/video-proof.json');video_proof(proof);require(sha((PACKET/'editor-flow.mp4').read_bytes())==proof['outputSha256'],'video hash')
 probe=json.loads(subprocess.check_output(['ffprobe','-v','quiet','-select_streams','v:0','-show_entries','stream=width,height:frame=best_effort_timestamp_time','-show_frames','-of','json',str(PACKET/'editor-flow.mp4')]))
 sources=read('reports/video-source-frames.json');decoded_video(proof,probe,sources);stage_picks(proof,sources)
 for filename,pick in proof['picks'].items():require(sha((PACKET/'playmode'/filename).read_bytes())==pick['sha256'],'video stage pixels mismatch')
 require(read('reports/video-completion.json')['success'] is True and read('reports/video-origin.json')['productionUpdate'] is True,'real continuous production Update flow failed')
 rows=[r.split('|') for r in (PACKET/'reports/video-frames.txt').read_text().splitlines()];video_initial_and_transition_rows(rows)
 first=proof['firstFrame'];require(first['sourceFrameIndex']==0 and first['sha256']==sources[0]['sha256'] and sha((PACKET/'playmode/video-first-frame.png').read_bytes())==first['sha256'],'exact clean first source frame missing')
 require((PACKET/'reports/video-safe-wire.txt').read_bytes()==(PACKET/'reports/video-rollback-wire.txt').read_bytes(),'whole snapshot/Grid file rollback mismatch')
 earned=read('reports/earned-combat.json');require(read('reports/earned-combat-completion.json')['success'] is True and earned['testClock'] is True and earned['liveResourceMutation'] is False and earned['boss'] and earned['castBoss'] and earned['bossDeath'],'earned boss flow incomplete')
 require(any(r.get('bossDamage',0)>0 for r in earned['rows']),'positive boss ability HP loss required')
 death=read('reports/earned-boss-death.json');require(death['domainRemoved'] is True and .2<=death['tailClock']<=.6,'real bounded boss death required')
 probes=read('reports/synthetic-probes.json');require(probes['synthetic'] is True and probes['foreignRendererHiding'] is False and len(probes['receipts'])==30,'new visual matrix incomplete')
 git('diff','--check',BASE,'HEAD');git('diff','--check')
 receipt=dict(status='PASS',head=git('rev-parse','HEAD').decode().strip(),testedSource=m['testedSource'],mediaSource=m['mediaSource'],source=len(source),protected=len(protected),artifacts=len(m['artifacts']),suites=totals,exactCarryIn=len(CARRY),localPreservation=preservation,readOnly=True,newUnityRun=False,device='Deferred QA53/54/55')
 print(json.dumps(receipt,indent=2));return receipt
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check-local-preservation',action='store_true');args=parser.parse_args()
 try:main(check_local_preservation=args.check_local_preservation)
 except Exception as e:print('FAIL:',e,file=sys.stderr);sys.exit(1)

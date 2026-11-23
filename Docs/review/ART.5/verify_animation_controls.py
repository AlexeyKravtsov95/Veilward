"""Small in-memory controls for new ART5 source/animation/compile/render/video guards."""
import copy,importlib.util,unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('art5',Path(__file__).with_name('verify_delivery.py'));v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Controls(unittest.TestCase):
 def test_boolean_suite_counts_reject(self):
  for key in ('total','passed','failed','skipped','inconclusive'):
   s=dict(total=1,passed=1,failed=0,skipped=0,inconclusive=0);s[key]=bool(s[key])
   with self.assertRaises(ValueError):v.full_pass(dict(summary=s,results=[dict(Status='Passed')]))
 def test_full_suite_exits_do_not_hide_failed_editor_refresh(self):
  r=dict(testedSource='head',allOriginalInputsRestored=True,unexpected=[],newInputs=[],recoveryNeeded=False,commandDiagnostics=[],editorSceneDetached=True,editorInputsRefreshed=True,results={'static':0,'edit-mode':0,'play-mode':0})
  v.gate_status(r,'head')
  for change in (dict(recoveryNeeded=True),dict(commandDiagnostics=['timeout']),dict(editorSceneDetached=False),dict(editorInputsRefreshed=False),dict(results={'static':0,'edit-mode':0,'play-mode':False})):
   x=copy.deepcopy(r);x.update(change)
   with self.assertRaises(ValueError):v.gate_status(x,'head')
 def test_carry_in_disk_hash_cannot_allow_accidental_committed_user_materials(self):
  baseline={p:'base' for p in v.CARRY};v.carry_commit_scope(baseline,baseline,baseline,baseline)
  for p in v.CARRY:
   changed=dict(baseline);changed[p]='user blob'
   with self.assertRaises(ValueError):v.carry_commit_scope(baseline,changed,changed,changed)
 def test_old_combat_first_frame_and_noncombat_death_do_not_prove_fresh_flow(self):
  menu=['0','.001','100','fresh MainMenu / NewGame','','','','','','0','','']
  combat=['1','1','150','Combat1 / production Update / moving Shade + HP + attacks','20','80','20','1','2','0','','Move;Death']
  v.video_initial_and_transition_rows([menu,combat])
  old=list(menu);old[4:9]=['3805','136','16','64','0'];old[-1]='Death'
  with self.assertRaises(ValueError):v.video_initial_and_transition_rows([old,combat])
  death=list(combat);death[3]='Previous earned replay';move=list(combat);move[-1]='Move'
  with self.assertRaises(ValueError):v.video_initial_and_transition_rows([menu,move,death])
 def test_compile_transport_success_does_not_hide_failures(self):
  r=dict(success=True,errors=[],data=dict(status='up_to_date',compiling=False,failed=False,errors=[],summary=dict(errors=0)))
  v.compile_status(r)
  for change in (dict(failed=True),dict(errors=['CS error']),dict(compiling=True),dict(status='running'),dict(summary=dict(errors=1))):
   x=copy.deepcopy(r);x['data'].update(change)
   with self.assertRaises(ValueError):v.compile_status(x)
 def test_roundtrip_requires_roster_and_real_clips(self):
  a=dict(models=[dict(name=n,meshes=[{}]*4,clips=[dict(name=c,keyframes=100,curves=36,poses=[{}]*4,legacy=True) for c in ('Idle','Move','Hit','Death','Leak')]) for n in ('Enemy_Shade','Enemy_RiftWarden')])
  v.roundtrip(a,a)
  for mutate in (lambda x:x['models'].pop(),lambda x:x['models'][0]['clips'].pop(),lambda x:x['models'][0]['clips'][0].update(keyframes=0),lambda x:x['models'][0]['clips'][0].update(poses=[])):
   b=copy.deepcopy(a);mutate(b)
   with self.assertRaises(ValueError):v.roundtrip(b,b)
  b=copy.deepcopy(a);b['models'][0]['clips'][0]['poses'][0]['position']='tampered'
  with self.assertRaises(ValueError):v.roundtrip(a,b)
 def video(self):
  sources=[dict(frame=i+1,elapsed=i*.1,file=f'{i:05d}.png',stage='stage',sha256=str(i)) for i in range(100)]
  proof=dict(speed=1,splices=0,sourceFrames=100,outputFrames=100,resolution=[1600,900],editor=True,device=False,picks={})
  probe=dict(streams=[dict(width=1600,height=900)],frames=[dict(best_effort_timestamp_time=i*.1) for i in range(100)])
  for i,(filename,stage) in enumerate(v.PICK_STAGES.items()):
   sources[i]['stage']=stage;proof['picks'][filename]=dict(manifestIndex=i,sourceFrameIndex=i,gameFrame=i+1,sha256=str(i),stage=stage)
  return proof,probe,sources
 def test_decoded_dimensions_pts_and_splices_are_strict(self):
  proof,probe,sources=self.video();v.decoded_video(proof,probe,sources)
  for mutate in (lambda p,r,s:p.update(speed=2),lambda p,r,s:p.update(splices=1),lambda p,r,s:r['streams'][0].update(width=640),lambda p,r,s:r['frames'][2].update(best_effort_timestamp_time=0),lambda p,r,s:s[2].update(elapsed=8)):
   p,r,s=copy.deepcopy((proof,probe,sources));mutate(p,r,s)
   with self.assertRaises(ValueError):v.decoded_video(p,r,s)
 def test_stage_picks_empty_missing_duplicate_stale_reject(self):
  proof,_,sources=self.video();v.stage_picks(proof,sources)
  for mutate in (lambda p:p.update(picks={}),lambda p:p['picks'].pop('video-menu.png'),lambda p:p['picks']['video-menu.png'].update(sourceFrameIndex=1),lambda p:p['picks']['video-menu.png'].update(stage='foreign')):
   p=copy.deepcopy(proof);mutate(p)
   with self.assertRaises(ValueError):v.stage_picks(p,sources)
 def test_actual_clip_null_or_wrong_roster_reject(self):
  r=dict(actualPlayMode=True,unity='6000.3.28f1',graphics='Metal',quality='Mobile',pipelinePath='Assets/Settings/Mobile_RPAsset.asset',pipelineSerialized={'loaded':True},materials=[dict(name=n,shader='Veilward',passes=1) for n in ('MAT_Art1_Surface','MAT_Art1_Glow','MAT_Art21_ShadeOcclusion')],enemies=[])
  for i,n in enumerate(('Enemy_Shade','Enemy_RiftWarden')):
   path='Assets/'+n+'.fbx';r['enemies'].append(dict(model=path,renderers=8,sharedMeshes=[{}]*8,clips={c:path+':'+str(i)*32+':'+str(j+1) for j,c in enumerate(('idle','move','hit','death','leak'))}))
  v.actual_inputs(r)
  for mutate in (lambda x:x['enemies'][0]['clips'].update(idle='null'),lambda x:x['enemies'][0]['clips'].pop('hit'),lambda x:x['enemies'].pop(),lambda x:x.update(pipelineSerialized={}),lambda x:x.update(materials=[])):
   x=copy.deepcopy(r);mutate(x)
   with self.assertRaises((ValueError,KeyError)):v.actual_inputs(x)
if __name__=='__main__':unittest.main(verbosity=2)

"""Only tiny temporary bytes and mocked Editor commands; never a Unity project copy."""
import importlib.util,hashlib,subprocess,tempfile,unittest
from pathlib import Path
from unittest import mock
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('gate',ROOT/'ArtSource/Review/run_art5_gates.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Controls(unittest.TestCase):
 def probe(self,failed_stage):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);(root/'Assets').mkdir();p=root/'Assets/known.asset';p.write_bytes(b'original carry bytes')
   saved={'Assets/known.asset':p.read_bytes()};before={'Assets/known.asset':v.sha(p.read_bytes())};p.write_bytes(b'generated builder bytes');after={'Assets/known.asset':v.sha(p.read_bytes())};calls=[]
   def execute(stage,code):
    calls.append(stage)
    if stage==failed_stage:raise subprocess.TimeoutExpired(['fixture Editor request'],45)
    return {'success':True}
   with mock.patch.object(v,'ROOT',root):r=v.restore_known_inputs(saved,before,after,execute)
   self.assertEqual(p.read_bytes(),b'original carry bytes');self.assertTrue(r['allOriginalInputsRestored']);self.assertTrue(r['recoveryNeeded']);self.assertTrue(r['commandDiagnostics']);self.assertFalse(r['unexpected'])
   return r,calls
 def test_detach_timeout_still_restores_and_does_not_enqueue_refresh(self):
  r,calls=self.probe('pre-restore-empty-scene');self.assertEqual(calls,['pre-restore-empty-scene']);self.assertFalse(r['editorSceneDetached']);self.assertFalse(r['editorInputsRefreshed'])
 def test_post_restore_refresh_failure_still_has_exact_byte_audit(self):
  r,calls=self.probe('restored-refresh');self.assertEqual(calls,['pre-restore-empty-scene','restored-refresh']);self.assertTrue(r['editorSceneDetached']);self.assertFalse(r['editorInputsRefreshed'])
if __name__=='__main__':unittest.main(verbosity=2)

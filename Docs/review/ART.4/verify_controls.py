"""Small in-memory/temporary-file controls; no Unity project copy or mutation."""
import importlib.util,unittest,tempfile
from pathlib import Path
spec=importlib.util.spec_from_file_location('art4_delivery',Path(__file__).with_name('verify_delivery.py'))
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Controls(unittest.TestCase):
 def test_positive_inventory_and_source_artifact(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'source').write_bytes(b'original');v.digest_files(p,{'source':v.sha(b'original')},'source');v.inventory(['source'],['source'],'source')
 def test_source_tamper(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'source').write_bytes(b'tamper')
   with self.assertRaises(ValueError):v.digest_files(p,{'source':v.sha(b'original')},'source')
 def test_artifact_tamper(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'png').write_bytes(b'tamper')
   with self.assertRaises(ValueError):v.digest_files(p,{'png':v.sha(b'pixels')},'artifact')
 def test_committed_foreign_inventory(self):
  with self.assertRaises(ValueError):v.inventory(['Assets/good.cs'],['Assets/good.cs','Assets/foreign.cs'],'source')
 def test_protected_git_blob_tamper(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'baseline').write_bytes(b'protected');expected={'baseline':v.blob(b'protected')};v.protected_files(p,expected)
   (p/'baseline').write_bytes(b'protected tampered')
   with self.assertRaises(ValueError):v.protected_files(p,expected)
 def test_exact_carry_in_not_any_dirty_bytes(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'user').write_bytes(b'changed user')
   with self.assertRaises(ValueError):v.digest_files(p,{'user':v.sha(b'exact user')},'carry-in')
 def test_positive_full_suite(self):
  count,_=v.full_pass({'Summary':{'Total':1,'Passed':1,'Failed':0,'Skipped':0,'Inconclusive':0},'Results':[{'Status':'Passed'}]});self.assertEqual(count,1)
 def test_empty_or_skipped_filtered_failed_suites_rejected(self):
  for report in [
   {'Summary':{'Total':0,'Passed':0,'Failed':0,'Skipped':0,'Inconclusive':0},'Results':[]},
   {'Summary':{'Total':1,'Passed':1,'Failed':0,'Skipped':1,'Inconclusive':0},'Results':[{'Status':'Passed'}]},
   {'success':False,'Summary':{'Total':1,'Passed':1,'Failed':0,'Skipped':0,'Inconclusive':0},'Results':[{'Status':'Passed'}]},
   {'FilterApplied':'only one','Summary':{'Total':1,'Passed':1,'Failed':0,'Skipped':0,'Inconclusive':0},'Results':[{'Status':'Passed'}]}]:
   with self.assertRaises(ValueError):v.full_pass(report)
if __name__=='__main__':unittest.main(verbosity=2)

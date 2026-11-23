"""Small in-memory/temporary-file controls; no Unity project copy or mutation."""
import contextlib,importlib.util,io,json,sys,unittest,tempfile
from unittest import mock
from pathlib import Path
spec=importlib.util.spec_from_file_location('art5_delivery',Path(__file__).with_name('verify_delivery.py'))
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Controls(unittest.TestCase):
 def foreign_fixture(self,root):
  rendering={}
  for name in v.CARRY:
   path=root/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b'rendering:'+name.encode())
   rendering[name]=v.sha(path.read_bytes())
  private={'cache/first.pyc':v.sha(b'first'),'cache/second.pyc':v.sha(b'second'),'personal.txt':v.sha(b'personal')}
  return rendering,{**rendering,**private}
 def test_default_delivery_does_not_require_or_read_private_three(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);rendering,known=self.foreign_fixture(root)
   v.digest_files(root,rendering,'exact rendering')
   with mock.patch.object(v,'digest_files',wraps=v.digest_files) as digest:
    result=v.local_preservation(root,known,enabled=False)
   digest.assert_not_called();self.assertEqual(result['status'],'NOT_REQUESTED')
 def test_explicit_preservation_missing_private_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);_,known=self.foreign_fixture(root)
   with self.assertRaisesRegex(ValueError,'local preservation digest mismatch'):
    v.local_preservation(root,known,enabled=True)
 def test_explicit_preservation_changed_private_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);_,known=self.foreign_fixture(root)
   for name,data in [('cache/first.pyc',b'first'),('cache/second.pyc',b'second'),('personal.txt',b'personal')]:
    path=root/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
   self.assertEqual(v.local_preservation(root,known,enabled=True)['status'],'PASS')
   for name in ('cache/first.pyc','cache/second.pyc','personal.txt'):
    original=(root/name).read_bytes();(root/name).write_bytes(b'changed')
    with self.assertRaisesRegex(ValueError,'local preservation digest mismatch'):
     v.local_preservation(root,known,enabled=True)
    (root/name).write_bytes(original)
 def test_default_does_not_relax_rendering_four_tamper(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);rendering,known=self.foreign_fixture(root)
   for name in rendering:
    original=(root/name).read_bytes();(root/name).write_bytes(b'tamper')
    with self.assertRaises(ValueError):v.digest_files(root,rendering,'exact rendering')
    (root/name).write_bytes(original)
   v.local_preservation(root,known,enabled=False)
 def test_missing_rendering_four_still_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);rendering,_=self.foreign_fixture(root)
   for name in rendering:
    original=(root/name).read_bytes();(root/name).unlink()
    with self.assertRaises(ValueError):v.digest_files(root,rendering,'exact rendering')
    (root/name).write_bytes(original)
 def test_unexpected_source_and_evidence_still_rejected(self):
  known={'personal.txt':'historical digest'}
  for path in ('Assets/foreign.cs','Docs/review/ART.5/foreign.json'):
   with self.assertRaises(ValueError):v.dirty_inputs(['?? '+path],known)
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
class DeliveryEntrypointControls(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  known=json.loads((v.PACKET/'reports/carry-in-sha256.json').read_text())
  cls.private=tuple(v.ROOT/name for name in known if name not in v.CARRY)
  if len(cls.private)!=3:raise AssertionError('Expected the exact three historical non-rendering paths')
 def test_full_default_delivery_without_private_three(self):
  is_file=Path.is_file;read_bytes=Path.read_bytes;git=v.git;private=self.private
  def hidden_is_file(path):return False if path in private else is_file(path)
  def guarded_read(path):
   if path in private:raise AssertionError('Default delivery must not read private bytes: '+str(path))
   return read_bytes(path)
  def missing_status(*args):
   data=git(*args)
   if args[0]=='status':
    rows=[row for row in data.split(b'\0') if row and v.ROOT/row.decode()[3:] not in private]
    return b'\0'.join(rows)+(b'\0' if rows else b'')
   return data
  with mock.patch.object(Path,'is_file',hidden_is_file),mock.patch.object(Path,'read_bytes',guarded_read),mock.patch.object(v,'git',missing_status),contextlib.redirect_stdout(io.StringIO()):
   receipt=v.main()
  self.assertEqual(receipt['status'],'PASS');self.assertEqual(receipt['exactCarryIn'],4)
  self.assertEqual(receipt['localPreservation']['status'],'NOT_REQUESTED')
  type(self).missing_private_receipt=receipt
 def test_full_explicit_preservation_rejects_missing_private_three(self):
  original=Path.is_file
  with mock.patch.object(Path,'is_file',lambda path:False if path in self.private else original(path)),contextlib.redirect_stdout(io.StringIO()):
   with self.assertRaisesRegex(ValueError,'local preservation digest mismatch'):
    v.main(check_local_preservation=True)
 def test_full_explicit_preservation_rejects_changed_first_private_file(self):
  original_read=Path.read_bytes;original_is_file=Path.is_file;changed=self.private[0]
  # Малый virtual input не требует исходного личного файла даже в reviewer checkout.
  with mock.patch.object(Path,'is_file',lambda path:True if path==changed else original_is_file(path)),mock.patch.object(Path,'read_bytes',lambda path:b'tampered local fixture' if path==changed else original_read(path)),contextlib.redirect_stdout(io.StringIO()):
   with self.assertRaisesRegex(ValueError,'local preservation digest mismatch'):
    v.main(check_local_preservation=True)
if __name__=='__main__':
 entrypoint='--delivery-entrypoint' in sys.argv
 if entrypoint:sys.argv.remove('--delivery-entrypoint')
 program=unittest.main(defaultTest='DeliveryEntrypointControls' if entrypoint else 'Controls',verbosity=2,exit=False)
 if entrypoint and program.result.wasSuccessful():
  print(json.dumps(dict(control='full delivery with all three private paths absent/unreadable; mandatory checks unchanged',receipt=DeliveryEntrypointControls.missing_private_receipt),indent=2))
 sys.exit(0 if program.result.wasSuccessful() else 1)

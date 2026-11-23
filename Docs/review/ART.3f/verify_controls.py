"""Small stdlib fixtures; no copy/worktree and no mutation of Unity checkout/evidence."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('delivery_controls',HERE/'verify_delivery.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Controls(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='veilward-art3f-control-');self.root=Path(self.temp.name);self.old=v.ROOT;v.ROOT=self.root
        def git(*a):return subprocess.check_output(['git',*a],cwd=self.root,stderr=subprocess.DEVNULL).decode().strip()
        self.git=git;git('init','-q');git('config','user.name','fixture');git('config','user.email','fixture@example.invalid')
        (self.root/'source.cs').write_text('original');(self.root/'protected.asset').write_text('protected')
        (self.root/'proof.txt').write_text('artifact');git('add','.');git('commit','-qm','small fixture');self.pin=git('rev-parse','HEAD')
    def tearDown(self):v.ROOT=self.old;self.temp.cleanup()
    def test_positive_inventory(self):self.assertEqual(len(v.verify_inventory(self.pin,v.files(self.pin),{})),3)
    def test_source_drift_rejected(self):
        (self.root/'source.cs').write_text('tampered')
        with self.assertRaisesRegex(AssertionError,'source/protected drift'):v.verify_inventory(self.pin,v.files(self.pin),{})
    def test_committed_foreign_source_rejected(self):
        (self.root/'foreign.cs').write_text('foreign');self.git('add','.');self.git('commit','-qm','foreign')
        with self.assertRaisesRegex(AssertionError,'exact source inventory'):v.verify_inventory(self.pin,v.files('HEAD'),{})
    def test_protected_baseline_drift_rejected(self):
        (self.root/'protected.asset').write_text('wrong')
        with self.assertRaisesRegex(AssertionError,'source/protected drift'):v.verify_blobs(self.pin,['protected.asset'],{})
    def test_artifact_tamper_rejected(self):
        digest=v.sha((self.root/'proof.txt').read_bytes());(self.root/'proof.txt').write_text('altered')
        with self.assertRaisesRegex(AssertionError,'artifact hash'):v.verify_artifacts({'proof.txt':digest},{'proof.txt'})
    def test_untracked_artifact_rejected(self):
        with self.assertRaisesRegex(AssertionError,'tracked regular artifact'):v.verify_artifacts({'proof.txt':'x'},set())
    def test_only_exact_carry_in_allowed(self):
        old=(self.root/'protected.asset').read_bytes();new=b'exact carry-in';(self.root/'protected.asset').write_bytes(new)
        rendering={'protected.asset':{'canonicalSha256':v.sha(old),'carryInSha256':v.sha(new)}}
        v.verify_blobs(self.pin,['protected.asset'],rendering)
        (self.root/'protected.asset').write_bytes(new+b'!')
        with self.assertRaisesRegex(AssertionError,'exact rendering carry-in'):v.verify_blobs(self.pin,['protected.asset'],rendering)
    def test_suite_needs_nonempty_full_pass_and_typed_counts(self):
        p=self.root/'result.json'
        valid={'status':'completed','summary':{'total':1,'passed':1,'failed':0,'skipped':0,'inconclusive':0},'results':[{'Status':'Passed'}]}
        p.write_text(json.dumps(valid));v.suite(p,1)
        for field,value in [('total',True),('passed',True),('failed',1),('skipped',1),('inconclusive',1),('passed',0)]:
            d=json.loads(json.dumps(valid));d['summary'][field]=value;p.write_text(json.dumps(d))
            with self.subTest(field=field,value=value),self.assertRaises(AssertionError):v.suite(p,1)
        p.write_text(json.dumps(valid))
        with self.assertRaises(AssertionError):v.suite(p,0)
if __name__=='__main__':unittest.main(verbosity=2)

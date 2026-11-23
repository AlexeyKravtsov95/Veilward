"""Small stdlib fixtures; no Unity/project copy, no production-input mutations."""
import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import sys
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('delivery',Path(__file__).with_name('verify_delivery.py'));v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Controls(unittest.TestCase):
 def suite(self):return dict(summary=dict(total=1,passed=1,failed=0,skipped=0,inconclusive=0),results=[dict(Status='Passed')])
 def test_positive_suite(self):self.assertEqual(v.positive_suite(self.suite()),1)
 def test_empty_suite(self):
  r=self.suite();r['summary'].update(total=0,passed=0)
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_failed_suite(self):
  r=self.suite();r['summary'].update(passed=0,failed=1)
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_skipped_suite(self):
  r=self.suite();r['summary'].update(passed=0,skipped=1)
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_inconclusive_suite(self):
  r=self.suite();r['summary'].update(passed=0,inconclusive=1)
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_boolean_count(self):
  r=self.suite();r['summary']['total']=True
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_filtered_suite(self):
  r=self.suite();r['FilterApplied']=True
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_false_envelope(self):
  r=self.suite();r['success']=False
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_missing_case(self):
  r=self.suite();r['results']=[]
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_case_status_tamper(self):
  r=self.suite();r['results'][0]['Status']='Skipped'
  with self.assertRaises(ValueError):v.positive_suite(r)
 def test_exact_input_and_missing_tamper(self):
  with tempfile.TemporaryDirectory() as d,patch.object(v,'ROOT',Path(d)):
   p=Path(d)/'small.txt';p.write_bytes(b'expected');inputs={'small.txt':v.sha(b'expected')};v.exact_files(inputs,'fixture')
   p.write_bytes(b'tamper')
   with self.assertRaises(ValueError):v.exact_files(inputs,'fixture')
   p.unlink()
   with self.assertRaises(ValueError):v.exact_files(inputs,'fixture')
 def test_artifact_video_tamper(self):
  with tempfile.TemporaryDirectory() as d,patch.object(v,'ROOT',Path(d)):
   p=Path(d)/'movie.mp4';p.write_bytes(b'fixture-media');inputs={'movie.mp4':v.sha(p.read_bytes())}
   p.write_bytes(b'changed')
   with self.assertRaises(ValueError):v.exact_files(inputs,'media')
if __name__=='__main__':unittest.main()

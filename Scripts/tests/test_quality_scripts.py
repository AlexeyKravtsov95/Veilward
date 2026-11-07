#!/usr/bin/env python3
import json
from pathlib import Path
import sys
import tempfile
import textwrap
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from check_unity_project import check_project
from verify_unity_test_results import verify_path


def write_meta(path: Path, guid: str) -> None:
    path.write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8")


class UnityProjectChecksTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.baseline = self.root / "baseline.json"
        self.baseline.write_text(json.dumps({
            "unityVersion": "6000.3.28f1",
            "unityRevision": "8f082d2fe476",
            "packages": {"com.unity.test": "1.0.0"}
        }), encoding="utf-8")
        (self.root / "ProjectSettings").mkdir()
        (self.root / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 6000.3.28f1\n"
            "m_EditorVersionWithRevision: 6000.3.28f1 (8f082d2fe476)\n",
            encoding="utf-8")
        (self.root / "Packages").mkdir()
        (self.root / "Packages/manifest.json").write_text(json.dumps({
            "dependencies": {"com.unity.test": "1.0.0"}
        }), encoding="utf-8")
        (self.root / "Packages/packages-lock.json").write_text(json.dumps({
            "dependencies": {"com.unity.test": {"version": "1.0.0", "depth": 0}}
        }), encoding="utf-8")
        (self.root / "Assets/_Game").mkdir(parents=True)
        write_meta(self.root / "Assets/_Game.meta", "a" * 32)
        self.write_asset("Assets/_Game/Game.cs", "// test", "b" * 32)
        self.write_asmdef("Veilward", ["Veilward.Domain"], "c" * 32)
        self.write_asmdef("Veilward.Domain", [], "d" * 32, no_engine=True)
        self.write_asmdef("Veilward.Editor", ["Veilward"], "e" * 32, editor=True)
        self.write_asmdef("Veilward.Tests.EditMode", ["Veilward.Editor"], "f" * 32, editor=True)
        write_meta(self.root / "Assets/Tests.meta", "0" * 32)
        write_meta(self.root / "Assets/Tests/EditMode.meta", "2" * 32)
        write_meta(self.root / "Assets/_Game/Domain.meta", "3" * 32)
        write_meta(self.root / "Assets/_Game/Editor.meta", "4" * 32)

    def tearDown(self):
        self.temp.cleanup()

    def write_asset(self, relative, content, guid):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        write_meta(path.with_name(path.name + ".meta"), guid)

    def write_asmdef(self, name, references, guid, no_engine=False, editor=False):
        folder = "Editor" if name == "Veilward.Editor" else (
            "Domain" if name == "Veilward.Domain" else (
                "../Tests/EditMode" if name == "Veilward.Tests.EditMode" else ""))
        relative = Path("Assets/_Game") / folder / f"{name}.asmdef"
        self.write_asset(relative.as_posix(), json.dumps({
            "name": name,
            "references": references,
            "includePlatforms": ["Editor"] if editor else [],
            "excludePlatforms": [],
            "noEngineReferences": no_engine
        }), guid)

    def check(self):
        return check_project(self.root, self.baseline)

    def test_valid_project_passes(self):
        self.assertEqual([], self.check())

    def test_missing_file_meta_is_rejected(self):
        (self.root / "Assets/_Game/New.cs").write_text("// new", encoding="utf-8")
        self.assertTrue(any("missing .meta" in error for error in self.check()))

    def test_missing_directory_meta_is_rejected(self):
        (self.root / "Assets/_Game/NewFolder").mkdir()
        self.assertTrue(any("missing .meta" in error for error in self.check()))

    def test_orphan_meta_is_rejected(self):
        write_meta(self.root / "Assets/_Game/Orphan.cs.meta", "1" * 32)
        self.assertTrue(any("orphan .meta" in error for error in self.check()))

    def test_duplicate_guid_is_rejected(self):
        self.write_asset("Assets/_Game/Duplicate.cs", "// duplicate", "b" * 32)
        self.assertTrue(any("duplicate GUID" in error for error in self.check()))

    def test_unity_version_drift_is_rejected(self):
        (self.root / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 6000.3.29f1\n", encoding="utf-8")
        self.assertTrue(any("Unity version" in error for error in self.check()))

    def test_package_manifest_drift_is_rejected(self):
        (self.root / "Packages/manifest.json").write_text(json.dumps({
            "dependencies": {"com.unity.test": "2.0.0"}
        }), encoding="utf-8")
        self.assertTrue(any("package baseline" in error for error in self.check()))

    def test_direct_lock_drift_is_rejected(self):
        (self.root / "Packages/packages-lock.json").write_text(json.dumps({
            "dependencies": {"com.unity.test": {"version": "2.0.0", "depth": 0}}
        }), encoding="utf-8")
        self.assertTrue(any("lock version" in error for error in self.check()))

    def test_missing_local_assembly_reference_is_rejected(self):
        self.write_asmdef("Veilward", ["Veilward.Missing"], "c" * 32)
        self.assertTrue(any("unknown local assembly" in error for error in self.check()))

    def test_domain_engine_reference_is_rejected(self):
        self.write_asmdef("Veilward.Domain", [], "d" * 32, no_engine=False)
        self.assertTrue(any("noEngineReferences" in error for error in self.check()))

    def test_editor_assembly_must_be_editor_only(self):
        self.write_asmdef("Veilward.Editor", ["Veilward"], "e" * 32, editor=False)
        self.assertTrue(any("Editor-only" in error for error in self.check()))

    def test_play_mode_assembly_is_not_forced_to_be_editor_only(self):
        self.write_asmdef("Veilward.Tests.PlayMode", [], "5" * 32, editor=False)
        self.assertEqual([], self.check())


class UnityResultChecksTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def write_result(self, content, name="result.xml"):
        path = self.root / name
        path.write_text(textwrap.dedent(content).strip(), encoding="utf-8")
        return path

    def test_passing_junit_report_is_accepted(self):
        path = self.write_result("""
            <testsuites tests="2" failures="0" errors="0" skipped="0">
              <testsuite tests="2" failures="0" errors="0" skipped="0" />
            </testsuites>
        """)
        self.assertEqual([], verify_path(path))

    def test_skipped_junit_test_is_rejected(self):
        path = self.write_result("""
            <testsuite tests="2" failures="0" errors="0" skipped="1" />
        """)
        self.assertTrue(any("skipped" in error.lower() for error in verify_path(path)))

    def test_empty_junit_suite_is_rejected(self):
        path = self.write_result('<testsuite tests="0" failures="0" errors="0" skipped="0" />')
        self.assertTrue(any("Total" in error for error in verify_path(path)))

    def test_failed_junit_test_is_rejected(self):
        path = self.write_result('<testsuite tests="1" failures="1" errors="0" skipped="0" />')
        self.assertTrue(any("Failed" in error for error in verify_path(path)))

    def test_passing_nunit_report_is_accepted(self):
        path = self.write_result("""
            <test-run testcasecount="2" total="2" passed="2" failed="0"
                      inconclusive="0" skipped="0" result="Passed" />
        """)
        self.assertEqual([], verify_path(path))

    def test_inconclusive_nunit_test_is_rejected(self):
        path = self.write_result("""
            <test-run testcasecount="1" total="1" passed="0" failed="0"
                      inconclusive="1" skipped="0" result="Inconclusive" />
        """)
        self.assertTrue(any("Inconclusive" in error for error in verify_path(path)))

    def test_malformed_report_is_rejected(self):
        path = self.write_result("<test-run>")
        self.assertTrue(any("malformed XML" in error for error in verify_path(path)))

    def test_missing_report_is_rejected(self):
        self.assertTrue(any("No XML" in error for error in verify_path(self.root)))

    def test_directory_requires_every_report_to_pass(self):
        self.write_result('<testsuite tests="1" failures="0" errors="0" skipped="0" />', "pass.xml")
        self.write_result('<testsuite tests="1" failures="1" errors="0" skipped="0" />', "fail.xml")
        self.assertTrue(any("fail.xml" in error for error in verify_path(self.root)))


if __name__ == "__main__":
    unittest.main(verbosity=2)

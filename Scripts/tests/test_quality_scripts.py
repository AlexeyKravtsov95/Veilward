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


class UnityRunnerTests(unittest.TestCase):
    def run_play_mode_with_fake_unity(self, total, passed):
        import os
        import subprocess

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            bin_dir = root / "bin"
            bin_dir.mkdir()
            log_path = root / "commands.jsonl"
            fake_unity = bin_dir / "unity"
            fake_unity.write_text(textwrap.dedent("""\
                #!/usr/bin/env python3
                import json
                import os
                import sys

                with open(os.environ["FAKE_UNITY_LOG"], "a", encoding="utf-8") as stream:
                    stream.write(json.dumps(sys.argv[1:]) + "\\n")
                if sys.argv[1] == "status":
                    print(json.dumps({"success": True}))
                elif sys.argv[1:3] == ["command", "test run"]:
                    print(json.dumps({"success": True, "result": {
                        "status": "running", "result": "running"
                    }}))
                elif sys.argv[1:3] == ["command", "test status"]:
                    total = int(os.environ["FAKE_TOTAL"])
                    passed = int(os.environ["FAKE_PASSED"])
                    print(json.dumps({"success": True, "result": {
                        "status": "completed",
                        "summary": {
                            "total": total, "passed": passed,
                            "failed": total - passed, "skipped": 0, "inconclusive": 0
                        }
                    }}))
                else:
                    raise SystemExit(2)
                """), encoding="utf-8")
            fake_unity.chmod(0o755)
            environment = dict(os.environ)
            environment["PATH"] = str(bin_dir) + os.pathsep + environment["PATH"]
            environment["FAKE_UNITY_LOG"] = str(log_path)
            environment["FAKE_TOTAL"] = str(total)
            environment["FAKE_PASSED"] = str(passed)
            completed = subprocess.run(
                [str(SCRIPTS / "run-unity-tests.sh"), "PlayMode"],
                cwd=SCRIPTS.parent,
                env=environment,
                capture_output=True,
                text=True,
                timeout=20,
                check=False)
            commands = [json.loads(line) for line in log_path.read_text(encoding="utf-8").splitlines()]
            return completed, commands

    def test_play_mode_uses_async_run_and_polls_completed_result(self):
        completed, commands = self.run_play_mode_with_fake_unity(total=1, passed=1)
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr + str(commands))
        run_command = next(command for command in commands if command[:2] == ["command", "test run"])
        self.assertIn("--async-tests", run_command)
        self.assertTrue(any(command[:2] == ["command", "test status"] for command in commands))

    def test_play_mode_empty_completed_suite_is_rejected(self):
        completed, _ = self.run_play_mode_with_fake_unity(total=0, passed=0)
        self.assertNotEqual(0, completed.returncode)


class UnityWorkflowContractTests(unittest.TestCase):
    def setUp(self):
        workflows = SCRIPTS.parent / ".github" / "workflows"
        self.quality = (workflows / "quality-gates.yml").read_text(encoding="utf-8")
        self.unity = (workflows / "unity-personal-tests.yml").read_text(encoding="utf-8")

    def test_static_quality_gates_keep_hosted_pr_and_push_coverage(self):
        self.assertIn("\n  pull_request:\n    branches: [main]", self.quality)
        self.assertIn("\n  push:\n    branches: [main]", self.quality)
        self.assertIn("runs-on: ubuntu-latest", self.quality)
        self.assertIn("./Scripts/run-static-checks.sh", self.quality)
        self.assertNotIn("runs-on: [self-hosted", self.quality)

    def test_unity_tests_are_manual_main_on_dedicated_macos_runner(self):
        self.assertIn("\n  workflow_dispatch:", self.unity)
        self.assertNotIn("\n  pull_request:", self.unity)
        self.assertNotIn("\n  push:", self.unity)
        self.assertNotIn("pull_request_target", self.unity)
        self.assertIn("github.event_name == 'workflow_dispatch'", self.unity)
        self.assertIn("github.ref == 'refs/heads/main'", self.unity)
        self.assertIn("runs-on: [self-hosted, macOS, veilward-unity-personal]", self.unity)
        self.assertIn("environment: unity-personal-ci", self.unity)
        self.assertIn("ref: main", self.unity)
        self.assertIn("permissions:\n  contents: read", self.unity)

    def test_unity_ci_does_not_use_license_secrets_or_game_ci(self):
        combined = self.quality + self.unity
        for forbidden in (
            "game-ci/",
            "secrets.",
            "UNITY_LICENSE",
            "UNITY_EMAIL",
            "UNITY_PASSWORD",
            "UNITY_SERIAL",
            "createManualActivationFile",
            "manualLicenseFile",
            "VEILWARD_UNITY_CI",
        ):
            self.assertNotIn(forbidden, combined)

    def test_unity_tests_validate_and_publish_both_reports(self):
        self.assertIn("testMode: [EditMode, PlayMode]", self.unity)
        self.assertIn("max-parallel: 1", self.unity)
        self.assertIn('./Scripts/run-unity-tests.sh "${{ matrix.testMode }}"', self.unity)
        self.assertIn(
            'python3 Scripts/verify_unity_test_results.py "TestResults/${{ matrix.testMode }}.xml"',
            self.unity,
        )
        self.assertIn("if: always()", self.unity)
        self.assertIn("TestResults/${{ matrix.testMode }}.log", self.unity)
        self.assertIn("if-no-files-found: error", self.unity)


if __name__ == "__main__":
    unittest.main(verbosity=2)

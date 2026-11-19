"""QA1-01…05: оркестрация с fake Unity, без заявления реального export."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("ios_export", Path(__file__).parents[1] / "export-ios.py")
ios = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ios)


class IosExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve() / "project"
        self.root.mkdir()
        self.editor = self.root.parent / "Unity.app/Contents/MacOS/Unity"
        self.editor.parent.mkdir(parents=True)
        self.editor.write_text("#!/bin/sh\nexit 0\n")
        self.editor.chmod(0o755)
        self.support = self.editor.parent.parent / "PlaybackEngines/iOSSupport"
        self.support.mkdir(parents=True)
        (self.root / ".gitignore").write_text("Builds/\nLibrary/\nTemp/\n")
        (self.root / "scene.meta").write_text("unchanged GUID\n")
        for args in [("init", "-q"), ("add", "."),
                     ("-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-qm", "fixture")]:
            ios.git(self.root, *args)
        self.real_run = subprocess.run

    def fake_unity(self, command, **kwargs):
        if command[0] != str(self.editor):
            return self.real_run(command, **kwargs)
        env = kwargs["env"]
        out = Path(env["VEILWARD_IOS_OUTPUT"])
        (out / "Unity-iPhone.xcodeproj").mkdir(parents=True)
        (out / "Unity-iPhone.xcodeproj/project.pbxproj").write_text("mock project")
        (out / "veilward-build.json").write_text(json.dumps({
            "sourceSHA": env["VEILWARD_SOURCE_SHA"], "version": env["VEILWARD_IOS_VERSION"],
            "buildNumber": env["VEILWARD_IOS_BUILD"], "contentId": ios.CONTENT,
            "unityVersion": "6000.3.28f1", **ios.production_receipt_fields()}))
        return subprocess.CompletedProcess(command, 0)

    def export(self, runner=None):
        with patch.object(ios.subprocess, "run", side_effect=runner or self.fake_unity):
            return ios.run_export(self.root, self.editor, "0.1.0", "42")

    def test_art_profile_uses_separate_entry_output_and_real_scenario(self):
        before = ios.snapshot(self.root)
        production = self.export()
        receipt = (production / "Xcode/veilward-build.json").read_bytes()

        def art(command, **kwargs):
            result = self.fake_unity(command, **kwargs)
            if command[0] == str(self.editor):
                self.assertEqual("Veilward.Editor.IosBuild.ExportArtReference", command[command.index("-executeMethod") + 1])
                out = Path(kwargs["env"]["VEILWARD_IOS_OUTPUT"])
                data = json.loads((out / "veilward-build.json").read_text())
                data.update(ios.art_receipt_fields())
                (out / "veilward-build.json").write_text(json.dumps(data))
            return result

        with patch.object(ios.subprocess, "run", side_effect=art):
            output = ios.run_export(self.root, self.editor, "0.1.0", "43", profile="art-reference")
        self.assertEqual(self.root / "Builds/iOS/art-reference/0.1.0-43", output)
        self.assertEqual(receipt, (production / "Xcode/veilward-build.json").read_bytes())
        self.assertEqual(before, ios.snapshot(self.root))

    def test_art_profile_rejects_production_receipt(self):
        with patch.object(ios.subprocess, "run", side_effect=self.fake_unity):
            with self.assertRaisesRegex(ValueError, "receipt does not match"):
                ios.run_export(self.root, self.editor, "0.1.0", "43", profile="art-reference")

    def test_production_profile_rejects_wrong_profile_scene_scenario_and_purpose(self):
        for field, value in ios.art_receipt_fields().items():
            def wrong(command, **kwargs):
                result = self.fake_unity(command, **kwargs)
                if command[0] == str(self.editor):
                    out = Path(kwargs["env"]["VEILWARD_IOS_OUTPUT"]) / "veilward-build.json"
                    data = json.loads(out.read_text()); data[field] = value
                    out.write_text(json.dumps(data))
                return result
            with self.subTest(field=field), patch.object(ios.subprocess, "run", side_effect=wrong):
                with self.assertRaisesRegex(ValueError, "receipt does not match"):
                    ios.run_export(self.root, self.editor, "0.1.0", "42")

    def test_unknown_profile_rejected_before_output_or_unity(self):
        with self.assertRaisesRegex(ValueError, "profile"):
            ios.run_export(self.root, self.editor, "0.1.0", "43", profile="anything")
        self.assertFalse((self.root / "Builds").exists())

    def test_art_profile_symlink_directory_rejected(self):
        (self.root / "Builds/iOS").mkdir(parents=True)
        target = self.root.parent / "external-art"
        target.mkdir()
        (self.root / "Builds/iOS/art-reference").symlink_to(target)
        with self.assertRaisesRegex(ValueError, "symlink"):
            ios.run_export(self.root, self.editor, "0.1.0", "43", profile="art-reference")
        self.assertEqual([], list(target.iterdir()))

    def test_invalid_version_and_build(self):
        for version, number in [("1.0", "42"), ("1.0.0\n", "42"), ("1.0.0", "0"),
                                ("1.0.0", "01"), ("1.0.0", "10000"), ("../1", "42")]:
            with self.subTest(version=version, number=number), self.assertRaises(ValueError):
                ios.validate_version(version, number)

    def test_missing_support(self):
        self.support.rmdir()
        with self.assertRaisesRegex(ValueError, "selected Editor layouts"):
            self.export()

    def test_adjacent_support_layout(self):
        adjacent = self.editor.parents[3] / "PlaybackEngines/iOSSupport"
        adjacent.parent.mkdir(parents=True)
        self.support.rename(adjacent)
        before = ios.snapshot(self.root)
        self.export()
        self.assertEqual(before, ios.snapshot(self.root))

    def test_dirty_source(self):
        (self.root / "scene.meta").write_text("dirty")
        with self.assertRaisesRegex(ValueError, "clean checkout"):
            self.export()

    def test_resident_editor(self):
        descriptor = self.root / "Library/Pipeline/.unity-pipeline-port"
        descriptor.parent.mkdir(parents=True)
        descriptor.touch()
        with self.assertRaisesRegex(ValueError, "close the Editor"):
            self.export()

    def test_repeated_exports_preserve_source(self):
        before = ios.snapshot(self.root)
        first = self.export()
        (first / "obsolete-file").touch()
        second = self.export()
        self.assertEqual(first, second)
        self.assertFalse((second / "obsolete-file").exists())
        self.assertEqual(before, ios.snapshot(self.root))
        self.assertEqual(before, json.loads((second / "source-audit.json").read_text()))

    def test_import_migration_stays_in_isolated_project(self):
        before = ios.snapshot(self.root)

        def migration(command, **kwargs):
            result = self.fake_unity(command, **kwargs)
            if command[0] == str(self.editor):
                project = Path(command[command.index("-projectPath") + 1])
                self.assertNotEqual(self.root, project)
                (project / "scene.meta").write_text("imported serialization")
            return result

        output = self.export(migration)
        self.assertEqual(before, ios.snapshot(self.root))
        self.assertIn("scene.meta", json.loads((output / "import-audit.json").read_text()))
        self.assertIn(b"imported serialization", (output / "import-diff.patch").read_bytes())
        self.assertFalse((output / "Project").exists())

    def test_failed_unity_preserves_previous(self):
        first = self.export()
        before = (first / "Xcode/veilward-build.json").read_bytes()

        def fail(command, **kwargs):
            if command[0] == str(self.editor):
                return subprocess.CompletedProcess(command, 1)
            return self.real_run(command, **kwargs)

        with self.assertRaisesRegex(ValueError, "Unity export failed"):
            self.export(fail)
        self.assertEqual(before, (first / "Xcode/veilward-build.json").read_bytes())

    def log_unity(self, command, **kwargs):
        result = self.fake_unity(command, **kwargs)
        if command[0] == str(self.editor):
            Path(command[command.index("-logFile") + 1]).write_text("new Unity log\n")
        return result

    def test_log_symlink_to_source_preserves_clean_source(self):
        before = ios.snapshot(self.root)
        (self.root / "Builds/iOS").mkdir(parents=True)
        log = self.root / "Builds/iOS/last-export.log"
        log.symlink_to(self.root / "scene.meta")
        self.export(self.log_unity)
        self.assertEqual(before, ios.snapshot(self.root))
        self.assertEqual(b"", ios.git(self.root, "status", "--porcelain", "--untracked-files=normal"))
        self.assertFalse(log.is_symlink())
        self.assertEqual("new Unity log\n", log.read_text())

    def test_log_symlink_to_external_target_preserves_target(self):
        target = self.root.parent / "external.txt"
        target.write_text("external original\n")
        (self.root / "Builds/iOS").mkdir(parents=True)
        (self.root / "Builds/iOS/last-export.log").symlink_to(target)
        self.export(self.log_unity)
        self.assertEqual("external original\n", target.read_text())
        self.assertEqual(b"", ios.git(self.root, "status", "--porcelain", "--untracked-files=normal"))

    def test_failed_unity_log_symlink_preserves_targets_and_previous(self):
        previous = self.export()
        previous_bytes = (previous / "Xcode/veilward-build.json").read_bytes()
        target = self.root.parent / "external.txt"
        target.write_text("external original\n")
        log = self.root / "Builds/iOS/last-export.log"

        def fail_with_log(command, **kwargs):
            result = self.log_unity(command, **kwargs)
            if command[0] == str(self.editor):
                return subprocess.CompletedProcess(command, 1)
            return result

        for target in (self.root / "scene.meta", target):
            with self.subTest(target=target.name):
                before = ios.snapshot(self.root)
                target_bytes = target.read_bytes()
                log.unlink(missing_ok=True)
                log.symlink_to(target)
                with self.assertRaisesRegex(ValueError, "Unity export failed"):
                    self.export(fail_with_log)
                self.assertEqual(target_bytes, target.read_bytes())
                self.assertEqual(before, ios.snapshot(self.root))
                self.assertEqual(b"", ios.git(self.root, "status", "--porcelain", "--untracked-files=normal"))
                self.assertEqual(previous_bytes, (previous / "Xcode/veilward-build.json").read_bytes())

    def test_empty_success_is_failure(self):
        def empty(command, **kwargs):
            if command[0] == str(self.editor):
                return subprocess.CompletedProcess(command, 0)
            return self.real_run(command, **kwargs)

        with self.assertRaisesRegex(ValueError, "did not produce"):
            self.export(empty)

    def test_source_drift_is_failure(self):
        def drift(command, **kwargs):
            result = self.fake_unity(command, **kwargs)
            if command[0] == str(self.editor):
                (self.root / "scene.meta").write_text("mutated")
            return result

        with self.assertRaisesRegex(ValueError, "changed source"):
            self.export(drift)
        self.assertFalse((self.root / "Builds/iOS/0.1.0-42").exists())

    def test_wrong_receipt_is_failure(self):
        def wrong(command, **kwargs):
            result = self.fake_unity(command, **kwargs)
            if command[0] == str(self.editor):
                p = Path(kwargs["env"]["VEILWARD_IOS_OUTPUT"]) / "veilward-build.json"
                data = json.loads(p.read_text())
                data["sourceSHA"] = "0" * 40
                p.write_text(json.dumps(data))
            return result

        with self.assertRaisesRegex(ValueError, "receipt does not match"):
            self.export(wrong)

    def test_non_object_receipt_is_failure(self):
        for payload in ([], None, "unexpected"):
            def wrong_type(command, **kwargs):
                result = self.fake_unity(command, **kwargs)
                if command[0] == str(self.editor):
                    (Path(kwargs["env"]["VEILWARD_IOS_OUTPUT"]) / "veilward-build.json").write_text(json.dumps(payload))
                return result

            with self.subTest(payload=payload), self.assertRaisesRegex(ValueError, "receipt does not match"):
                self.export(wrong_type)

    def test_symlink_output_is_rejected(self):
        target = self.root.parent / "private-output"
        target.mkdir()
        (self.root / "Builds").symlink_to(target, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlink"):
            self.export()
        self.assertEqual([], list(target.iterdir()))

    def test_concurrent_export_is_rejected(self):
        (self.root / "Builds/iOS/.export-lock").mkdir(parents=True)
        with self.assertRaises(FileExistsError):
            self.export()


if __name__ == "__main__":
    unittest.main()

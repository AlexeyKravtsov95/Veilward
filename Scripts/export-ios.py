#!/usr/bin/env python3
"""Чистый локальный export. Signing выполняется отдельно в Xcode."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

CONTENT = "eight-wave-ability-boss-v2"


def validate_version(version, number):
    if not re.fullmatch(r"[0-9]{1,4}\.[0-9]{1,2}\.[0-9]{1,2}", version):
        raise ValueError("version must be major.minor.patch (4/2/2 digits maximum)")
    if not re.fullmatch(r"[1-9][0-9]{0,3}", number):
        raise ValueError("build number must be 1..9999")


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args], stderr=subprocess.PIPE)


def snapshot(root):
    paths = git(root, "ls-files", "-z").decode().split("\0")
    return {p: hashlib.sha256((root / p).read_bytes()).hexdigest()
            for p in paths if p}


def production_receipt_fields():
    return {"profile": "production", "purpose": "QA.1 production smoke",
            "scenes": ["Assets/_Game/Scenes/Bootstrap.unity", "Assets/_Game/Scenes/MainMenu.unity", "Assets/_Game/Scenes/Battle.unity"],
            "scenarioPath": "Assets/_Game/Configs/EightWaveAbilityBossV2.asset",
            "contentId": CONTENT}


def art_receipt_fields():
    return {"profile": "art-reference", "purpose": "ART.2b device microbenchmark",
            "scenes": ["Assets/_Game/Scenes/ArtReference.unity"],
            "scenarioPath": "Assets/_Game/Configs/TestWave.asset",
            "contentId": "legacy-single-wave-v1"}


def run_export(root, editor, version, number, profile="production"):
    if profile not in ("production", "art-reference"):
        raise ValueError("unknown iOS build profile")
    validate_version(version, number)
    if not editor.is_file() or not os.access(editor, os.X_OK):
        raise ValueError("Unity executable unavailable; set UNITY_EDITOR_PATH")
    editor = editor.resolve()
    # Hub встречается с bundle и adjacent layout. Capability проверяет сам Editor.
    contents = editor.parent.parent
    support = (contents / "PlaybackEngines/iOSSupport",
               contents.parent.parent / "PlaybackEngines/iOSSupport")
    if not any(path.is_dir() for path in support):
        raise ValueError("BLOCKED: iOS support not found in selected Editor layouts; compare Hub/executable/capability before reinstalling")
    if (root / "Library/Pipeline/.unity-pipeline-port").exists() or (root / "Temp/UnityLockfile").exists():
        raise ValueError("BLOCKED: close the Editor for this checkout")
    base = root / "Builds/iOS"
    if base.resolve() != root / "Builds/iOS":
        raise ValueError("symlink build directories are not supported")
    if profile == "art-reference":
        base = base / "art-reference"
        if base.resolve() != root / "Builds/iOS/art-reference":
            raise ValueError("symlink build directories are not supported")
    destination = base / f"{version}-{number}"
    if destination.is_symlink():
        raise ValueError("symlink output is not supported")
    if git(root, "status", "--porcelain", "--untracked-files=normal").strip():
        raise ValueError("BLOCKED: commit changes/use a clean checkout before export")
    source = git(root, "rev-parse", "HEAD").decode().strip()
    base.mkdir(parents=True, exist_ok=True)
    lock = base / ".export-lock"
    lock.mkdir()  # Параллельный export не должен менять тот же output.
    before = snapshot(root)
    try:
        with tempfile.TemporaryDirectory(prefix=".export-", dir=base) as temp:
            stage = Path(temp)
            project = stage / "Project"
            git(root, "worktree", "add", "--quiet", "--detach", str(project), source)
            env = dict(os.environ, VEILWARD_IOS_OUTPUT=str(stage / "Xcode"),
                       VEILWARD_IOS_VERSION=version, VEILWARD_IOS_BUILD=number,
                       VEILWARD_SOURCE_SHA=source)
            try:
                result = subprocess.run([
                    str(editor), "-batchmode", "-nographics", "-quit",
                    "-projectPath", str(project), "-buildTarget", "iOS",
                    "-executeMethod", ("Veilward.Editor.IosBuild.ExportArtReference" if profile == "art-reference" else "Veilward.Editor.IosBuild.Export"),
                    "-logFile", str(stage / "Unity-export.log")], env=env,
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                imported = snapshot(project)
                # Импорт может мигрировать Unity serialization; сохраняем derived inputs.
                delta = {p: {"sourceSHA256": before.get(p), "importedSHA256": h}
                         for p, h in imported.items() if before.get(p) != h}
                (stage / "import-audit.json").write_text(json.dumps(delta, indent=2, sort_keys=True) + "\n")
                (stage / "import-diff.patch").write_bytes(git(project, "diff", "--binary", "HEAD"))
            finally:
                # Удаляется только созданный этой попыткой изолированный worktree.
                git(root, "worktree", "remove", "--force", str(project))
            # Log остаётся локальным: может содержать private paths и signing metadata.
            if (stage / "Unity-export.log").exists():
                # os.replace заменяет entry, не следует symlink/hardlink назначения.
                temp_log = None
                try:
                    with tempfile.NamedTemporaryFile(dir=base, prefix=".last-export-", delete=False) as stream:
                        temp_log = Path(stream.name)
                        with (stage / "Unity-export.log").open("rb") as source_log:
                            shutil.copyfileobj(source_log, stream)
                    os.replace(temp_log, base / "last-export.log")
                finally:
                    if temp_log is not None:
                        temp_log.unlink(missing_ok=True)
            if snapshot(root) != before or git(root, "status", "--porcelain", "--untracked-files=normal").strip():
                raise ValueError("FAIL: Unity changed source files; inspect git diff locally")
            if result.returncode != 0:
                raise ValueError("FAIL: Unity export failed; inspect Builds/iOS/last-export.log locally")
            receipt = stage / "Xcode/veilward-build.json"
            expected = {"sourceSHA": source, "version": version, "buildNumber": number,
                        "contentId": CONTENT, "unityVersion": "6000.3.28f1"}
            if not (stage / "Xcode/Unity-iPhone.xcodeproj/project.pbxproj").is_file() or not receipt.is_file():
                raise ValueError("FAIL: Unity did not produce an Xcode project and receipt")
            expected.update(art_receipt_fields() if profile == "art-reference" else production_receipt_fields())
            actual = json.loads(receipt.read_text())
            if not isinstance(actual, dict) or any(actual.get(k) != v for k, v in expected.items()):
                raise ValueError("FAIL: export receipt does not match requested source/version/content")
            (stage / "source-audit.json").write_text(json.dumps(before, indent=2, sort_keys=True) + "\n")
            previous = base / f".{version}-{number}.previous"
            if previous.exists():
                raise ValueError("previous export backup exists; inspect it locally before retry")
            if destination.exists():
                destination.rename(previous)
            try:
                stage.rename(destination)
            except OSError:
                if previous.exists():
                    previous.rename(destination)
                raise
            if previous.exists():
                shutil.rmtree(previous)
        print(f"EXPORTED: {destination.relative_to(root)}/Xcode profile={profile} sourceSHA={source}; review import-audit.json before device acceptance")
        return destination
    finally:
        lock.rmdir()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--build-number", required=True)
    parser.add_argument("--profile", choices=("production", "art-reference"), default="production")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    match = re.search(r"m_EditorVersion: (.+)", (root / "ProjectSettings/ProjectVersion.txt").read_text())
    editor = Path(os.environ.get("UNITY_EDITOR_PATH", f"/Applications/Unity/Hub/Editor/{match[1]}/Unity.app/Contents/MacOS/Unity"))
    try:
        run_export(root, editor, args.version, args.build_number, args.profile)
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        # Не печатать exception с произвольными абсолютными путями/командами.
        print(str(error) if isinstance(error, ValueError) else f"FAIL: {type(error).__name__}; inspect local environment/output", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())

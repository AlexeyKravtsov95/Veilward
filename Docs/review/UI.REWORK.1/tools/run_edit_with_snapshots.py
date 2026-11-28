"""Run EditMode and preserve project files written by existing scene-builder tests."""
import json
import subprocess
import tempfile
from pathlib import Path

RESULT_DIR = Path("TestResults/UIREWORK1")
EDITOR = ["unity", "command"]
PIPELINE_ARGS = ["--caller", "plugin", "--skill", "unity-cli", "--format", "json"]
MUTABLE_PATHS = [
    Path("Assets/Settings/Mobile_RPAsset.asset"),
    Path("Assets/_Game/Scenes/Battle.unity"),
    Path("Assets/_Game/Scenes/MainMenu.unity"),
    Path("Assets/_Game/Prefabs/Shade.prefab"),
    Path("Assets/_Game/Prefabs/TestTower.prefab"),
]


def main():
    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    paths = list(Path("Assets/_Game/Art/Materials").glob("*.mat")) + MUTABLE_PATHS
    originals = {path: path.read_bytes() for path in paths}
    backup = Path(tempfile.mkdtemp(prefix="edit-snapshot-", dir=RESULT_DIR))
    for path, content in originals.items():
        saved = backup / path
        saved.parent.mkdir(parents=True, exist_ok=True)
        saved.write_bytes(content)
    print(f"EditMode snapshot: {backup}", flush=True)
    try:
        command = EDITOR + ["test", "run"] + PIPELINE_ARGS + ["--timeout", "600", "--", "--mode", "editor"]
        report_path = RESULT_DIR / "EditMode.json"
        with report_path.open("w") as report:
            result = subprocess.run(command, stdout=report, check=False)
        if result.returncode != 0:
            return result.returncode
        summary = json.loads(report_path.read_text())["Summary"]
        print(summary, flush=True)
        passed = summary["Passed"] == summary["Total"] > 0
        return 0 if passed and all(summary[key] == 0 for key in ("Failed", "Skipped", "Inconclusive")) else 1
    finally:
        try:
            # Switch away from the generated scenes before restoring them on disk.
            switch = EDITOR + ["scene", "open"] + PIPELINE_ARGS + ["--", "--path", "Assets/_Game/Scenes/Bootstrap.unity"]
            subprocess.run(switch, check=True, timeout=30)
        finally:
            restore_errors = []
            changed = []
            for path, content in originals.items():
                try:
                    if not path.exists() or path.read_bytes() != content:
                        path.write_bytes(content)
                        changed.append(str(path))
                except OSError as error:
                    restore_errors.append(f"{path}: {error}")
            (RESULT_DIR / "edit-restored.tsv").write_text("\n".join(changed) + "\n")
            if restore_errors:
                raise RuntimeError(f"Unable to restore from {backup}: " + "\n".join(restore_errors))
            print(f"Restored test-produced files: {len(changed)}", flush=True)


if __name__ == "__main__":
    raise SystemExit(main())

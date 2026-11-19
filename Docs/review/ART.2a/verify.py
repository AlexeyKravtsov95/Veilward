"""Read-only ART.2a delivery audit; stdlib + Git, Unity не запускается."""
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[3]
PACKAGE = ROOT / "Docs/review/ART.2a"
HISTORY = ROOT / "Docs/review/ART.1.1"
KEY_FRAMES = [
    "after-01-island-game-camera.png",
    "after-04-shade-light-game-scale.png",
    "after-05-shade-dark-game-scale.png",
    "after-08-shade-dense-game-scale.png",
    "after-03-flame-behind.png",
]


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    manifest = json.loads((PACKAGE / "provenance.json").read_text())
    source = manifest["historicalSourceCommit"]
    tracked = set(git("ls-files").decode().splitlines())
    for group, records in manifest["groups"].items():
        require(records, f"Empty manifest: {group}")
        for path, expected in records.items():
            require(path in tracked, f"Not tracked: {path}")
            actual = hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
            historical = hashlib.sha256(git("show", f"{source}:{path}")).hexdigest()
            require(actual == expected == historical, f"Historical drift: {path}")
        print(f"PASS {group}: {len(records)}/{len(records)} byte-identical, tracked")

    changed = git("diff", "--name-only", manifest["deliveryBaseCommit"]).decode().splitlines()
    require(all(p.startswith("Docs/review/ART.2a/") for p in changed), "Scope drift")
    package_files = [p for p in PACKAGE.rglob("*") if p.is_file() and "__pycache__" not in p.parts]
    require(all(str(p.relative_to(ROOT)) in tracked for p in package_files), "Untracked package file")
    links = 0
    for doc in PACKAGE.glob("*.md"):
        for target in re.findall(r"!?\[[^\]]*\]\(([^)]+)\)", doc.read_text()):
            url = urlsplit(target)
            if url.scheme or not url.path:
                continue
            path = (doc.parent / unquote(url.path)).resolve()
            require(path.is_file() and path.is_relative_to(ROOT), f"Missing/unsafe link: {target}")
            require(str(path.relative_to(ROOT)) in tracked, f"Untracked link: {target}")
            links += 1
    print(f"PASS delivery: {len(package_files)} package files tracked, {links} local links, scope ART.2a only")

    captures = {x["file"]: x for x in map(json.loads, (HISTORY / "capture-log.jsonl").read_text().splitlines())}
    for name in KEY_FRAMES:
        data = (HISTORY / name).read_bytes()
        require(data[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", data[16:24]) == (1600, 900), f"PNG dimensions: {name}")
        entry = captures[name]
        require(entry["playMode"] is True and entry["ortho"] == 8.5, f"Capture scale/mode: {name}")
        require(entry["cameraPosition"] == {"x": 8, "y": 11, "z": 8}, f"Camera: {name}")
    video = json.loads((HISTORY / "video-capture.json").read_text())
    require(video["playMode"] is True and video["frames"] == 240 and video["firstTick"] == 0
            and video["lastTick"] == 239 and video["attacks"] == 8
            and video["lastFrame"] > video["firstFrame"], "Historical advancing video telemetry")
    console = json.loads((HISTORY / "console-audit.json").read_text())["groundTruth"]
    require(console["consoleErrors"] == console["consoleWarnings"] == 0
            and console["compilationFailed"] is False, "Historical Console audit")
    print("PASS historical media: 5/5 game-scale PNG, advancing video telemetry, Console audit")
    print("NOT VERIFIED: new Unity run, owner ART approval, ART.2b iPhone readability/performance")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)

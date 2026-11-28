"""Verify published UI.REWORK.2 source, original GameView media and full Unity receipts."""
import hashlib
import json
import re
import struct
import subprocess
from pathlib import Path

PACKET = Path(__file__).resolve().parents[1]
ROOT = PACKET.parents[2]
REPORTS = PACKET / "reports"
BASE = "51a7c3792b5b1570ce2aa33390b0b0b157a37704"
RECORD = re.compile(rb"^--- !u!(\d+) &(-?\d+)(?: stripped)?$", re.M)
LOCAL = re.compile(rb"{fileID: (-?\d+)}")
PNG = b"\x89PNG\r\n\x1a\n"
PROFILE = {"baseline": (1600, 900), "narrow": (1136, 640), "modern": (2556, 1179), "ipad": (2048, 1536)}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verify_inputs():
    manifest = json.loads((REPORTS / "inputs.json").read_bytes())
    source = manifest["source"]
    assert git("merge-base", "--is-ancestor", BASE, source) == b""
    assert git("merge-base", "--is-ancestor", source, "HEAD") == b""
    assert git("diff", "--name-only", source, "HEAD", "--", "Assets/", "Packages/", "ProjectSettings/") == b""
    for name, expected in manifest["runtimeTrees"].items():
        assert git("rev-parse", f"{source}:{name}").decode().strip() == expected, name
    for name, expected in manifest["tracked"].items():
        blob = git("show", f"{source}:{name}")
        assert digest(blob) == expected, name
        assert digest((ROOT / name).read_bytes()) in {expected, manifest["renderCarry"].get(name)}, name
    for name, expected in manifest["overlays"].items():
        assert digest((ROOT / name).read_bytes()) == expected, name
    return source


def scene_records(scene):
    markers = list(RECORD.finditer(scene))
    return {marker.group(2): scene[marker.start():markers[i+1].start() if i+1 < len(markers) else len(scene)]
            for i, marker in enumerate(markers)}


def verify_scene():
    filename = "Assets/_Game/Scenes/Battle.unity"
    before = scene_records(git("show", f"{BASE}:{filename}"))
    after = scene_records((ROOT / filename).read_bytes())
    assert before.keys() <= after.keys()
    changed = [after[i] for i in before if before[i] != after[i]]
    assert len(changed) == 2 and any(b"  badges:\n" in row for row in changed)
    new = set(after) - set(before)
    assert len(new) == 13
    assert sum(bool(re.search(rb"  m_Name: (Boss Badge [12]|Inspect Warden)\n", after[i])) for i in new) == 3
    for record in after.values():
        assert set(LOCAL.findall(record)) <= {*after, b"0"}
    menu = "Assets/_Game/Scenes/MainMenu.unity"
    assert (ROOT / menu).read_bytes() == git("show", f"{BASE}:{menu}")
    return {"battleIdenticalRecords": len(before) - len(changed), "battleChangedRecords": len(changed),
            "battleNewRecords": len(new), "mainMenuIdentical": True}


def verify_suites():
    results = {}
    for mode in ("EditMode", "PlayMode"):
        report = json.loads((REPORTS / f"{mode}.json").read_bytes())
        if mode == "EditMode":
            assert report["success"] is True and report["Mode"] == mode and not report["FilterApplied"]
            summary, rows = report["Summary"], report["Results"]
        else:
            assert report["status"] == "completed" and not report.get("filter")
            summary, rows = report["summary"], report["results"]
        normalized = {key.lower(): value for key, value in summary.items()}
        total = normalized["total"]
        assert total > 0 and normalized == dict(total=total, passed=total, failed=0, skipped=0, inconclusive=0)
        assert len(rows) == len({case["FullName"] for case in rows}) == total
        assert all(case["Status"] == "Passed" for case in rows)
        results[mode] = normalized
    assert "Ran 38 tests" in (REPORTS / "static.txt").read_text()
    assert "Static Unity checks: PASS" in (REPORTS / "static.txt").read_text()
    assert json.loads((REPORTS / "compile.json").read_text())["compilationFailed"] is False
    return results


def verify_media():
    before = json.loads((REPORTS / "before-capture-console.json").read_bytes())
    after = json.loads((REPORTS / "final-capture-console.json").read_bytes())
    assert before["session"] == after["session"] and before["counts"]["error"] == after["counts"]["error"]
    assert after["groundTruth"]["consoleErrors"] == 0
    required = {
        "earned": {"shade-two", "boss-combat", "boss-two", "victory"},
        "fixture": {"two-slots", "shade-inspector", "steam", "pause", "resumed-cue", "cue-expired",
                    "storm", "inspector-full", "x2", "status-expired", "dense-fixture"},
        "profiles": set(PROFILE),
    }
    images = {}
    for folder, expected in required.items():
        events = (PACKET / folder / "events.tsv").read_text().splitlines()
        captured = {row.split("\t")[7] for row in events if row.startswith("png\t")}
        media = set()
        for path in sorted((PACKET / folder).glob("*.png")):
            data = path.read_bytes()
            assert data.startswith(PNG)
            size = struct.unpack(">II", data[16:24])
            assert size == (PROFILE[path.stem] if folder == "profiles" else (1600, 900)), (path, size)
            media.add(path.stem)
            images[str(path.relative_to(PACKET))] = {"pixels": size, "sha256": digest(data)}
        assert media == captured and expected <= media, (folder, expected - media)
        assert events[-1].startswith("done\t") and events[-1].endswith("\tFalse"), folder

    path = PACKET / "fixture/gameview.mp4"
    command = ["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
               "-show_entries", "stream=codec_name,width,height,nb_read_frames,duration", "-of", "json", str(path)]
    stream, = json.loads(subprocess.check_output(command))["streams"]
    assert (stream["width"], stream["height"], stream["codec_name"]) == (1600, 900, "h264")
    events = (path.parent / "events.tsv").read_text().splitlines()
    frames = [row for row in events if row.startswith("frame\t")]
    assert int(stream["nb_read_frames"]) == len(frames) > 60
    badges = {row[7]: row for row in (line.split("\t") for line in events) if row[0] == "badges"}
    steam, pause, resumed, expired = (badges[name] for name in ("steam", "pause", "resumed-cue", "cue-expired"))
    assert steam[3] == pause[3] and steam[12] == pause[12]
    assert steam[8:11] == resumed[8:11] and steam[8:10] == expired[8:10]
    assert steam[10] == "GustBadge,SteamBadge" and expired[10] == "GustBadge,WetBadge"
    assert badges["storm"][10] == "BurnBadge,StormBadge"
    assert badges["status-expired"][8] == steam[8] and not badges["status-expired"][10]
    assert badges["x2"][13] == "scale=2"
    subprocess.run(["ffmpeg", "-nostdin", "-v", "error", "-xerror", "-i", str(path), "-f", "null", "-"], check=True)
    return {"images": images, "video": {**stream, "sha256": digest(path.read_bytes())}}


if __name__ == "__main__":
    print(json.dumps({"testedSource": verify_inputs(), "scene": verify_scene(), "suites": verify_suites(),
                      "media": verify_media()}, indent=2))

"""Read-only checks for the committed ART.REWORK.0a evidence."""

import csv
import gzip
import hashlib
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image

EVIDENCE = Path(__file__).resolve().parent
ROOT = EVIDENCE.parents[2]
BASE = "bfaed60e64a64c48457b4e8eb0c81166765b6d1d"


def check(condition, message):
    if not condition:
        raise SystemExit(message)


def test_totals(mode):
    with gzip.open(EVIDENCE / "reports" / (mode + ".json.gz"), "rt") as report:
        payload = json.load(report)
    if "success" in payload:
        check(payload["success"] is True, mode + " failed")
        payload = payload.get("result") or payload.get("data", {}).get("result") or payload
    if mode == "PlayMode":
        check(payload.get("status") == "completed", "Incomplete PlayMode run")
    counts = {name.lower(): value for name, value in (payload.get("Summary") or payload["summary"]).items()}
    check(counts["total"] == counts["passed"] > 0, mode + " lacks a full PASS")
    check(all(counts[k] == 0 for k in ("failed", "skipped", "inconclusive")), mode + " has non-passes")
    leaves = payload.get("Results") or payload["results"]
    check(len(leaves) == len({leaf["FullName"] for leaf in leaves}) == counts["total"], mode + " incomplete case inventory")
    check(all(leaf["Status"] == "Passed" for leaf in leaves), mode + " contains a non-passing case")
    print(f"{mode}: {counts['passed']} passed")


def media(mode):
    folder = EVIDENCE / mode
    lines = (folder / "frames.tsv").read_text().splitlines()
    header = next(line for line in lines if line.startswith("frame\t"))
    rows = list(csv.DictReader([header] + [line for line in lines if line[:1].isdigit()], delimiter="\t"))
    count = len(rows)
    check([int(row["frame"]) for row in rows] == list(range(count)), mode + " frame indices are discontinuous")
    probe = subprocess.check_output(
        ["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_streams", "-of", "json", folder / "battle.mp4"],
        text=True,
    )
    stream, = json.loads(probe)["streams"]
    check((stream["width"], stream["height"], stream["avg_frame_rate"]) == (1600, 900, "24/1"), mode + " video format")
    check(int(stream["nb_frames"]) == count > 400, mode + " video frame count")
    paused = [row for row in rows if row["paused"] == "True"]
    check(len(paused) > 15 and len({row["tick"] for row in paused}) == 1, mode + " pause advanced")
    check(all(any(row[column] == "True" for row in rows) for column in ("flight", "impact")), mode + " missing projectile")
    check(any(int(row["tails"]) > 0 for row in rows), mode + " missing exit animation")
    check("failed=False encoder=0" in lines[-1], mode + " incomplete recording")
    if mode == "earned":
        check("phase=Victory" in lines[-1], "Production run did not reach Victory")
    stills = {path.name: path for path in folder.glob("*.png")}
    required = {"preparation", "shade-warden", "turn", "flight", "impact", "pause", "death"}
    if mode == "stress":
        required.update(("heart", "overlap"))
    check(stills.keys() == {name + ".png" for name in required}, mode + " GameView still inventory")
    for png in stills.values():
        with Image.open(png) as image:
            check(image.size == (1600, 900), str(png) + " dimensions")
            image.verify()
    subprocess.run(["ffmpeg", "-v", "error", "-xerror", "-i", folder / "battle.mp4", "-f", "null", "-"], check=True)
    print(f"{mode}: {count} decoded GameView frames")


def main():
    for line in (EVIDENCE / "SHA256SUMS").read_text().splitlines():
        digest, relative = line.split("  ", 1)
        committed = subprocess.check_output(["git", "show", f"HEAD:{relative}"], cwd=ROOT)
        check(hashlib.sha256(committed).hexdigest() == digest, "Commit mismatch: " + relative)
        data = (ROOT / relative).read_bytes()
        check(hashlib.sha256(data).hexdigest() == digest, "SHA-256 mismatch: " + relative)
    subprocess.run([sys.executable, ROOT / "ArtSource/InkAndLanterns/prepare_sprites.py", "--check"], check=True)
    for mode in ("EditMode", "PlayMode"):
        test_totals(mode)
    for mode in ("earned", "stress"):
        media(mode)
    subprocess.run(
        ["git", "diff", "--exit-code", BASE, "HEAD", "--", "Packages", "ProjectSettings",
         "Assets/_Game/Configs", "Assets/_Game/Scripts/Domain", "Assets/_Game/Scripts/Application",
         "Assets/_Game/Scripts/Infrastructure", "Assets/_Game/Art/Models", "Assets/_Game/Prefabs/ART3",
         "Assets/_Game/Prefabs/ART5", "Assets/_Game/Scenes/MainMenu.unity",
         "Assets/_Game/Scripts/Presentation/FlameProjectilePool.cs", "Assets/_Game/Scripts/Presentation/FlameStrikeView.cs"],
        cwd=ROOT, check=True,
    )


if __name__ == "__main__":
    main()

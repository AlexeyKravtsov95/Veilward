"""Package or verify UI.1 GameView evidence without changing project inputs."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "TestResults/ui1/media"
DELIVERY = ROOT / "Docs/review/UI.1"
PROFILES = {
    "small": (1136, 640, 2, 0, 0),
    "wide": (2556, 1179, 3, 132, 63),
    "tablet": (2048, 1536, 2, 0, 40),
    "final": (2556, 1179, 3, 132, 63),
}
COMMON = {"menu-no-save", "starting-choice", "starting-choice-preview", "preparation-hud",
          "build-locked", "build-preview", "tower-upgrade-unaffordable", "pause"}
REQUIRED = {
    "small": COMMON,
    "tablet": COMMON,
    "wide": COMMON | {"menu-corrupt-save", "combat-hud", "ability-invalid", "ability-cancelled", "ability-valid",
                     "ability-cooldown", "next-wave", "tower-upgraded", "build-low-gold", "sell-confirmation",
                     "menu-continue", "continue-rollback", "save-failed-fixture", "defeat-undefended-replay"},
    "final": {"menu-backup-recovery", "warden-incoming", "warden-combat", "reaction-inspector", "dense-fight-fixture", "victory"},
}


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_bytes())


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def png_size(data):
    require(data.startswith(b"\x89PNG\r\n\x1a\n") and data[12:16] == b"IHDR", "Expected a PNG header")
    return struct.unpack(">II", data[16:24])


def valid_button(button, safe, dpr):
    x, y, width, height = button["pixels"]
    left, bottom, area_width, area_height = safe
    return (min(width, height) / dpr >= 44 and
            x >= left - 1 and y >= bottom - 1 and x + width <= left + area_width + 1
            and y + height <= bottom + area_height + 1)


def same_group_overlap(buttons, min_gap=0):
    for index, first in enumerate(buttons):
        ax, ay, aw, ah = first["pixels"]
        for second in buttons[index + 1:]:
            if first["parent"] != second["parent"]:
                continue
            bx, by, bw, bh = second["pixels"]
            x_overlap = min(ax + aw, bx + bw) - max(ax, bx)
            y_overlap = min(ay + ah, by + bh) - max(ay, by)
            if (x_overlap > 1 and y_overlap > -min_gap + 1) or (y_overlap > 1 and x_overlap > -min_gap + 1):
                return first["name"], second["name"]
    return None


def verify_profile(name, manifest, folder):
    require(manifest["complete"] and not manifest["errors"], name + " incomplete")
    width, height, dpr, side, bottom = PROFILES[name]
    expected_area = [side, bottom, width - 2 * side, height - bottom]
    unique = set()
    minimum = float("inf")
    for shot in manifest["shots"]:
        filename = shot["name"]
        require(filename not in unique, f"{name}/{filename}: duplicate")
        unique.add(filename)
        data = (folder / (filename + ".png")).read_bytes()
        require(png_size(data) == (width, height) and shot["sha256"] == sha(data), f"{name}/{filename}: PNG attribution")
        require(shot["safe"] == expected_area, f"{name}/{filename}: safe area")
        require(abs(shot["scaleFactor"] - min(width / 740, height / 400)) < 0.002, f"{name}/{filename}: CanvasScaler")
        for button in shot["buttons"]:
            require(valid_button(button, expected_area, dpr), f"{name}/{filename}: {button['name']} bounds or points")
            minimum = min(minimum, min(button["points"]))
        require(not same_group_overlap(shot["buttons"], 4 * shot["scaleFactor"]), f"{name}/{filename}: overlapping sibling controls")
        require(not same_group_overlap(shot["panels"]), f"{name}/{filename}: overlapping HUD surfaces")
        for label in shot["texts"]:
            require(label["preferredHeight"] <= label["available"] + 1, f"{name}/{filename}: {label['name']} clips")
    require(REQUIRED[name] <= unique, f"{name}: missing {REQUIRED[name] - unique}")
    return {"pixelSize": [width, height], "logicalSize": [width / dpr, height / dpr], "safePixels": expected_area,
            "minTouchPoints": round(minimum, 2), "originalPngs": len(unique)}


def ffprobe(path):
    raw = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
        "stream=codec_name,pix_fmt,width,height:frame=best_effort_timestamp_time",
        "-show_frames", "-of", "json", str(path)]))
    require(len(raw["streams"]) == 1, "Expected one video stream")
    stream = {key: raw["streams"][0][key] for key in ("codec_name", "pix_fmt", "width", "height")}
    pts = [float(frame["best_effort_timestamp_time"]) for frame in raw["frames"]]
    return stream, pts


def verify_video(frames, path):
    stream, pts = ffprobe(path)
    require(stream == {"codec_name": "h264", "pix_fmt": "yuv420p", "width": 1280, "height": 590},
            "Unexpected video raster/format")
    require(len(frames) >= 80 and len(frames) == len(pts), "Missing recorded video frames")
    require(all(first < second for first, second in zip(pts, pts[1:])), "Non-increasing video timestamps")
    require(all(first["frame"] < second["frame"] for first, second in zip(frames, frames[1:])), "Duplicate Unity frames")
    drift = max(abs(frame["elapsed"] - frames[0]["elapsed"] - stamp) for frame, stamp in zip(frames, pts))
    require(drift < .005, f"Video timestamp drift {drift}")
    stages = {frame["stage"] for frame in frames}
    require({"menu-no-save", "build-preview", "tower-upgraded", "sell-confirmation", "ability-cooldown",
             "pause", "menu-continue", "continue-rollback"} <= stages, "The continuous command flow is incomplete")
    scenes = {frame["scene"] for frame in frames}
    require(scenes == {"MainMenu", "Battle"}, "Expected production scenes in the continuous take")
    return {"frames": len(frames), "seconds": round(pts[-1], 3), "maxTimestampDrift": drift, **stream,
            "sha256": sha(path.read_bytes()), "stages": sorted(stages)}


def make_video(frames, path):
    take = RAW / "wide"
    lines = ["ffconcat version 1.0"]
    for index, frame in enumerate(frames):
        original = take / "frames" / f"{index:05d}.png"
        require(frame["index"] == index and sha(original.read_bytes()) == frame["sha256"], "GameView frame mismatch")
        lines.extend([f"file frames/{original.name}", "option framerate 1000"])
        if index + 1 < len(frames):
            lines.append(f"duration {frames[index + 1]['elapsed'] - frame['elapsed']:.9f}")
    sequence = take / "timeline.ffconcat"
    sequence.write_text("\n".join(lines) + "\n")
    command = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-safe", "0", "-i", str(sequence), "-fps_mode", "vfr", "-c:v", "libx264", "-preset", "fast", "-crf", "18",
               "-pix_fmt", "yuv420p", "-video_track_timescale", "1000", "-movflags", "+faststart", str(path)]
    subprocess.run(command, check=True)


def verify_tests(reports):
    counts = {}
    for mode in ("edit-mode", "play-mode"):
        report = json.loads(gzip.decompress((reports / (mode + ".json.gz")).read_bytes()))
        summary = {key.lower(): value for key, value in report.get("Summary", report.get("summary")).items()}
        tests = report.get("Results", report.get("results"))
        total = summary["total"]
        require(total == summary["passed"] == len(tests) == len({test["FullName"] for test in tests}) > 0,
                mode + " requires complete, unique cases")
        require(not any(summary[key] for key in ("failed", "skipped", "inconclusive")) and
                all(test["Status"] == "Passed" for test in tests), mode + " did not pass completely")
        require(not report.get("FilterApplied"), mode + " was filtered")
        counts[mode] = total
    return counts


def verify_scope(base, source):
    names = subprocess.check_output(["git", "diff", "--name-only", base, source], cwd=ROOT, text=True).splitlines()
    allowed = ("Assets/_Game/Scenes/Battle.unity", "Assets/_Game/Scenes/MainMenu.unity",
               "Assets/_Game/Scripts/Editor/Battle", "Assets/_Game/Scripts/Presentation/",
               "Assets/_Game/Tests/", "ArtSource/Review/", "Docs/")
    require(names and all(path.startswith(allowed) for path in names), "Unexpected source scope")
    return names


def verify_inputs(manifests, source, carry):
    expected = None
    for manifest in manifests.values():
        paths = {entry["path"]: entry["sha256"] for entry in manifest["hashes"] if entry["sha256"]}
        for filename in ("Assets/_Game/Scenes/MainMenu.unity", "Assets/_Game/Scenes/Battle.unity",
                         "Packages/manifest.json", "Packages/packages-lock.json",
                         "Assets/_Game/Scripts/Presentation/VeilwardUiGlyph.cs", "Packages/com.unity.ugui/package.json"):
            require(filename in paths, "Missing actual render input " + filename)
        require(expected is None or paths == expected, "Rendering inputs changed during the profile matrix")
        expected = paths
    for name, digest in expected.items():
        if name in carry:
            require(digest == carry[name], "Capture did not use documented carry-in file " + name)
        elif name.startswith(("Assets/", "ProjectSettings/", "Packages/manifest", "Packages/packages-lock")):
            content = subprocess.check_output(["git", "show", source + ":" + name], cwd=ROOT)
            require(sha(content) == digest, "Source/media hash mismatch " + name)
    return len(expected)


def negative_controls(safe, dpr):
    normal = {"pixels": [safe[0] + 1, safe[1] + 1, 60 * dpr, 60 * dpr]}
    require(valid_button(normal, safe, dpr), "Bad control fixture")
    for pixels in ([safe[0] + 1, safe[1] + 1, 43 * dpr, 60 * dpr],
                   [safe[0] - 2, safe[1], 60 * dpr, 60 * dpr]):
        require(not valid_button({"pixels": pixels}, safe, dpr), "Invalid bounds/target control evasion")
    buttons = [{"name": name, "pixels": normal["pixels"], "parent": "shared"} for name in ("Close", "Sell")]
    require(same_group_overlap(buttons), "Overlap control evasion")
    try:
        png_size(b"not a GameView PNG")
    except ValueError:
        return 4
    raise ValueError("Corrupt PNG control evasion")


def main(args):
    reports = DELIVERY / "reports"
    manifests = {}
    if args.package:
        reports.mkdir(parents=True, exist_ok=True)
    for name in PROFILES:
        if args.package:
            manifest = read(RAW / name / "manifest.json")
            folder = DELIVERY / "playmode" / name
            folder.mkdir(parents=True, exist_ok=True)
            for shot in manifest["shots"]:
                filename = shot["name"] + ".png"
                shutil.copyfile(RAW / name / filename, folder / filename)
            data = (RAW / name / "manifest.json").read_bytes()
            (reports / (name + ".json.gz")).write_bytes(gzip.compress(data, mtime=0))
        manifests[name] = json.loads(gzip.decompress((reports / (name + ".json.gz")).read_bytes()))
    video = DELIVERY / "production-flow.mp4"
    if args.package:
        make_video(manifests["wide"]["frames"], video)
    profile_reports = {name: verify_profile(name, manifest, DELIVERY / "playmode" / name)
                       for name, manifest in manifests.items()}
    inputs = read(args.gates / "byte-audit.json")
    require(inputs["allOriginalInputsRestored"] and inputs["editorInputsRefreshed"] and
            inputs["previousTestReportsRestored"] and set(inputs["results"].values()) == {0}, "Complete gates and original bytes required")
    counts = verify_tests(args.gates)
    source = inputs["testedSource"]
    scope = verify_scope(args.base, source)
    result = {"source": source, "base": args.base, "profiles": profile_reports,
              "video": verify_video(manifests["wide"]["frames"], video),
              "tests": counts, "renderInputHashes": verify_inputs(manifests, source, inputs["carryInSha256"]),
              "sourceScope": scope, "negativeControls": negative_controls(profile_reports["wide"]["safePixels"], 3)}
    if args.package:
        write(reports / "audit.json", result)
    else:
        require(result == read(reports / "audit.json"), "Recorded semantic audit differs")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", action="store_true")
    parser.add_argument("--gates", type=Path, required=True)
    parser.add_argument("--base", required=True)
    main(parser.parse_args())

"""Encode the original GameView frames in their captured order at realtime PTS."""
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "TestResults/vfx1c/video"
OUT = ROOT / "Docs/review/VFX.1c"
KINDS = {"Steam": (.22, .40), "Storm": (.14, .30)}
PHASES = ("launch", "flight", "impact")
STAGES = {
    "pause": "Pause / frozen hybrid geometry",
    "continue": "Continue / preparation and clean effects",
}
EVENT_KEYS = ("kind", "tick", "tower", "enemy", "level")
RASTER_SIZE = (64, 36)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def select_event(shot):
    return {key: shot[key] for key in EVENT_KEYS}


def attack_phase(shot):
    flight, lifetime = KINDS[shot["kind"]]
    elapsed = lifetime - shot["Remaining"]
    if not shot["IsFlying"]:
        return "impact"
    if elapsed <= .34 * flight:
        return "launch"
    if .37 * flight <= elapsed <= .94 * flight:
        return "flight"
    return None


def visibility(path, lines):
    """Count palette-matching raster pixels contiguous with projected effect strokes."""
    with Image.open(path) as image:
        if image.size != (1600, 900) or image.format != "PNG":
            raise ValueError(f"Unexpected source PNG shape {path}")
        pixels = image.convert("RGB")
    counts = {}
    for line in lines:
        tint = line["tint"]
        screen = line["screen"]
        if len(tint) != 3 or not all(0 <= channel <= 1 for channel in tint) or not 2 <= len(screen) <= 32 or not all(
                len(point) == 2 and all(-1 <= coordinate <= 2 for coordinate in point) for point in screen):
            raise ValueError("invalid projected stroke")
        expected = [tuple(round(x * 255) for x in tint)]
        expected.append(tuple(round((12.92 * x if x <= .0031308 else 1.055 * x ** (1 / 2.4) - .055) * 255) for x in tint))
        points = [(x * 1600, (1 - y) * 900) for x, y in screen]
        sampled = set()
        for (x0, y0), (x1, y1) in zip(points, points[1:]):
            steps = max(1, round(max(abs(x1 - x0), abs(y1 - y0))))
            for i in range(steps + 1):
                x = round(x0 + (x1 - x0) * i / steps)
                y = round(y0 + (y1 - y0) * i / steps)
                sampled.update((x + dx, y + dy) for dx in range(-3, 4) for dy in range(-3, 4)
                               if 0 <= x + dx < 1600 and 0 <= y + dy < 900)
        counts[line["name"]] = sum(
            any(all(abs(a - b) <= 13 for a, b in zip(pixels.getpixel(point), rgb)) for rgb in expected)
            for point in sampled
        )
    return counts


def source_raster(files):
    rows = []
    for path in files:
        with Image.open(path) as image:
            rows.append(image.convert("RGB").resize(RASTER_SIZE, Image.Resampling.BOX).tobytes())
    return b"".join(rows)


def verify_raster(video, source, count):
    width, height = RASTER_SIZE
    stride = width * height * 3
    command = ["ffmpeg", "-v", "error", "-i", str(video), "-map", "0:v:0", "-vf", f"scale={width}:{height}:flags=area",
               "-fps_mode", "passthrough", "-pix_fmt", "rgb24", "-f", "rawvideo", "-"]
    decoded = subprocess.check_output(command)
    if len(decoded) != len(source) or len(source) != stride * count:
        raise ValueError("complete video raster frame count")
    errors = [sum(abs(a - b) for a, b in zip(decoded[i:i + stride], source[i:i + stride])) / stride
              for i in range(0, len(source), stride)]
    worst = max(errors)
    if worst > 3.5:
        raise ValueError(f"decoded raster differs from captured source frames: {worst:.3f}")
    return worst


def probe(video):
    command = ["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
               "stream=width,height,pix_fmt,codec_name:frame=best_effort_timestamp_time", "-show_frames", "-of", "json", str(video)]
    return json.loads(subprocess.check_output(command))


def choose_frames(rows, sources):
    picks = {}
    for kind in KINDS:
        by_event = {}
        for row in rows:
            if row["paused"]:
                continue
            for shot in row["shots"] or []:
                if shot["kind"] != kind:
                    continue
                phase = attack_phase(shot)
                if phase:
                    key = tuple(select_event(shot).values())
                    by_event.setdefault(key, {}).setdefault(phase, []).append((row["index"], shot))
        candidates = []
        for phases in by_event.values():
            if set(phases) != set(PHASES):
                continue
            series = []
            for phase in PHASES:
                ranked = []
                for index, shot in phases[phase]:
                    counts = visibility(sources[index], shot["lines"])
                    ranked.append((sum(counts.values()), index, shot, counts))
                series.append(max(ranked, key=lambda item: item[0]))
            if min(item[0] for item in series) >= 4:
                candidates.append(series)
        if not candidates:
            raise ValueError(f"No visible same-event launch/flight/impact for {kind}")
        selected = max(candidates, key=lambda c: min(item[0] for item in c))
        for phase, (_, index, shot, counts) in zip(PHASES, selected):
            picks[f"realtime-{kind.lower()}-{phase}"] = dict(index=index, event=select_event(shot), phase=phase, pixels=counts)
    for name, stage in STAGES.items():
        indices = [r["index"] for r in rows if r["stage"] == stage]
        if not indices:
            raise ValueError(f"Missing recorded stage {name}")
        picks[f"realtime-{name}"] = dict(index=indices[len(indices) // 2])
    return picks


def main():
    completion = json.loads((RAW / "completion.json").read_bytes())
    rows = json.loads((RAW / "frames.json").read_bytes())
    if not completion["ok"] or completion["frames"] != len(rows) or len(rows) < 100:
        raise ValueError("Incomplete realtime recorder")
    files = sorted((RAW / "frames").glob("*.png"))
    if len(files) != len(rows) or any(i != r["index"] or f.stem != f"{i:05d}" or digest(f) != r["sha256"]
                                     for i, (f, r) in enumerate(zip(files, rows))):
        raise ValueError("Source frame inventory or SHA mismatch")
    picks = choose_frames(rows, files)
    concat = RAW / "source.ffconcat"
    lines = ["ffconcat version 1.0"]
    for i, file in enumerate(files):
        lines.extend([f"file frames/{file.name}", "option framerate 1000"])
        if i < len(rows) - 1:
            lines.append(f'duration {rows[i + 1]["elapsed"] - rows[i]["elapsed"]:.9f}')
    concat.write_text("\n".join(lines) + "\n")
    video = OUT / "editor-flow.mp4"
    command = ["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", str(concat.relative_to(ROOT)),
               "-fps_mode", "vfr", "-enc_time_base", "1:1000", "-c:v", "libx264", "-preset", "medium", "-crf", "16",
               "-pix_fmt", "yuv420p", "-video_track_timescale", "1000", "-movflags", "+faststart", str(video.relative_to(ROOT))]
    subprocess.run(command, cwd=ROOT, check=True)
    decoded = probe(video)
    timestamps = [float(frame["best_effort_timestamp_time"]) for frame in decoded["frames"]]
    if len(timestamps) != len(rows) or any(b <= a for a, b in zip(timestamps, timestamps[1:])):
        raise ValueError("Decoded frames are missing, duplicated or out of order")
    error = max(abs(t - (r["elapsed"] - rows[0]["elapsed"])) for t, r in zip(timestamps, rows))
    if error > .002:
        raise ValueError(f"Source timestamps changed: {error}")
    raster = source_raster(files)
    raster_error = verify_raster(video, raster, len(rows))
    (OUT / "playmode").mkdir(exist_ok=True)
    for name, pick in picks.items():
        source = files[pick["index"]]
        shutil.copyfile(source, OUT / "playmode" / f"{name}.png")
        pick["sha256"] = digest(source)
    reports = OUT / "reports"
    for name in ("console.txt", "completion.json", "render-inputs.json", "earned-preparation8.json", "earned-state.txt",
                 "continued-state.txt", "continued-file.json", "combat-safe-file.json", "source.ffconcat"):
        shutil.copyfile(RAW / name, reports / name)
    (reports / "video-frames.json.gz").write_bytes(gzip.compress((RAW / "frames.json").read_bytes(), mtime=0))
    (reports / "video-source-raster.rgb.gz").write_bytes(gzip.compress(raster, mtime=0))
    (reports / "video-proof.json").write_text(json.dumps(dict(
        command=command, executableSha256=digest(Path(shutil.which("ffmpeg"))),
        versions=[subprocess.check_output([program, "-version"], text=True).splitlines()[0] for program in ("ffmpeg", "ffprobe")],
        maxPtsError=error, maxRasterError=raster_error, rasterSha256=hashlib.sha256(raster).hexdigest(),
        frames=len(rows), lastPts=timestamps[-1], sha256=digest(video),
        splices=0, speed=1, decoded=decoded, picks=picks,
    ), indent=2) + "\n")
    print(f"{len(rows)} real-time Editor frames / {timestamps[-1]:.3f}s / max PTS error {error:.6f}s")


if __name__ == "__main__":
    sys.exit(main())

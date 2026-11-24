"""Package original #93 GameView PNGs, vfr MP4s and machine-readable attestations."""
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True
from encode_hybrid_video import source_raster, verify_raster, visibility

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "TestResults/vfx1d"
OUT = ROOT / "Docs/review/VFX.1d"
SCOPES = ("earned", "dense-all-open")


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_bytes())


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")


def stable_probe(video):
    # FFprobe emits version-specific optional keys, including inside a selected frame entry.
    command = ["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
               "stream=width,height,pix_fmt,codec_name:frame=best_effort_timestamp_time",
               "-show_frames", "-of", "json", str(video)]
    return probe_semantics(json.loads(subprocess.check_output(command)))


def probe_semantics(raw):
    require(len(raw["streams"]) == 1, "Exactly one selected video stream.")
    stream = {key: raw["streams"][0][key] for key in ("width", "height", "pix_fmt", "codec_name")}
    pts = [float(row["best_effort_timestamp_time"]) for row in raw["frames"]]
    return stream, pts


def visible(path, lines):
    return sum(visibility(path, lines).values()) if lines else 0


def choose(scope, rows, sources):
    picks = {}

    def pick(name, candidates, geometry=None):
        candidates = list(candidates)
        require(candidates, f"{scope}: missing {name}")
        if geometry is None:
            row = candidates[len(candidates) // 2]
            count = 0
        else:
            count, _, row = max((geometry(sources[r["index"]], r), -r["index"], r) for r in candidates)
            require(count >= 6, f"{scope}: {name} only {count} palette pixels near its stroke")
        picks[name] = {"index": row["index"], "stage": row["stage"], "tick": row["tick"],
                       "sourceSha256": row["sha256"], "palettePixels": count}

    def hit_pixels(path, row):
        return max((visible(path, effect["lines"]) for effect in row["seal"] or []
                    if effect["loss"] is not None and effect["loss"] > 0), default=0)

    if scope == "earned":
        pick("earned-pause", (r for r in rows if r["stage"] == "pause"))
        pick("file-continue", (r for r in rows if r["stage"] == "file-continue"))
        pick("earned-impact", (r for r in rows if not r["paused"] and r["seal"] and any(
            s["lethal"] is True for s in r["seal"])), hit_pixels)
    else:
        for stage in ("invalid-preview", "valid-preview"):
            pick(stage, (r for r in rows if r["stage"] == stage and r["preview"]),
                 lambda p, r: visible(p, r["preview"]))
        for stage in ("cancel", "dense-range", "dense-inspector", "four-status-inspector"):
            pick(stage, (r for r in rows if r["stage"] == stage and (not stage.endswith("inspector") or r["inspector"])))
        pick("dense-impact", (r for r in rows if r["stage"] == "dense-impact" and r["boss"] and r["enemies"] >= 6 and
                              r["seal"] and any(h["loss"] > 0 for rx in r["reactions"] or [] for h in rx["hits"])),
             hit_pixels)
        pick("dense-combat", (r for r in rows if r["stage"] in ("dense-combat", "dense-live") and r["productionUpdate"] and r["boss"] and r["enemies"] >= 6 and
                              len(r["shots"] or []) >= 2 and r["reactions"] and any(s["slots"] == 2 for s in r["slots"] or [])))
    wanted = (("Death",) if scope == "earned" else ("Spawn", "Threat", "Leak"))
    for kind in wanted:
        pick("boss-" + kind.lower(), (r for r in rows if any(b["kind"] == kind for b in r["cues"] or [])),
             lambda p, r: max(visible(p, b["lines"]) for b in r["cues"] if b["kind"] == kind))
    return picks


def encode(scope):
    folder = RAW / scope
    completion = read(folder / "completion.json")
    rows = read(folder / "frames.json")
    source = sorted((folder / "frames").glob("*.png"))
    require(completion["ok"] and not completion["errors"] and len(rows) == completion["frames"] == len(source) >= 100,
            f"{scope}: complete capture")
    require(all(i == r["index"] and p.stem == f"{i:05d}" and digest(p.read_bytes()) == r["sha256"]
                for i, (r, p) in enumerate(zip(rows, source))), f"{scope}: raw frame identities")
    picks = choose(scope, rows, source)
    timing = [r["elapsed"] for r in rows]
    lines = ["ffconcat version 1.0"]
    for i, path in enumerate(source):
        lines.extend([f"file frames/{path.name}", "option framerate 1000"])
        if i + 1 < len(source):
            lines.append(f"duration {timing[i + 1] - timing[i]:.9f}")
    (folder / "source.ffconcat").write_text("\n".join(lines) + "\n")
    video = OUT / f"{scope}.mp4"
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-safe", "0", "-i", str(folder / "source.ffconcat"),
                    "-fps_mode", "vfr", "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
                    "-video_track_timescale", "1000", "-movflags", "+faststart", str(video)], check=True)
    stream, pts = stable_probe(video)
    require(len(pts) == len(rows) and all(a < b for a, b in zip(pts, pts[1:])), "Decoded frame count and order.")
    error = max(abs(value - (elapsed - timing[0])) for elapsed, value in zip(timing, pts))
    require(error < .003, f"{scope}: PTS error {error:.6f}s")
    raster = source_raster(source)
    worst_raster = verify_raster(video, raster, len(rows))
    reports = OUT / "reports"
    (reports / f"{scope}-source-raster.rgb.gz").write_bytes(gzip.compress(raster, mtime=0))
    (reports / f"{scope}-frames.json.gz").write_bytes(gzip.compress((folder / "frames.json").read_bytes(), mtime=0))
    for path in ("completion.json", "render-inputs.json"):
        shutil.copyfile(folder / path, reports / f"{scope}-{path}")
    for name, pick in picks.items():
        index = pick["index"]
        shutil.copyfile(source[index], OUT / "playmode" / f"{name}.png")
        pick["pts"] = pts[index]
    write(reports / f"{scope}-video.json", {"sha256": digest(video.read_bytes()), "source": (RAW / "media-head.txt").read_text().strip(),
          "frameCount": len(rows), "stream": stream, "pts": pts, "maxPtsError": error, "maxRasterError": worst_raster, "picks": picks})
    print(scope, len(rows), f"{pts[-1]:.2f}s", sorted(picks))


def replay():
    report = {}
    for start in ("flame", "gale"):
        traces = [RAW / "replay" / f"{phase}-{start}.trace.gz" for phase in ("before", "after")]
        before, after = [gzip.decompress(path.read_bytes()) for path in traces]
        require(before == after, f"{start}: production trace bytes differ!")
        proof = read(RAW / "replay" / f"after-{start}.json")
        require(proof["outcome"] == "Victory" and proof["waves"][-1]["wave"] == 8 and proof["abilityKills"] > 0 and
                proof["bossKills"] == 1 and proof["rawBytes"] == len(after) and proof["traceSha256"] == digest(after) and
                len(proof["cells"]) == 4 and all(c["applied"] > 0 for c in proof["cells"]), f"{start}: REG214 coverage")
        for path in traces:
            shutil.copyfile(path, OUT / "reports" / path.name)
        report[start] = {k: proof[k] for k in ("outcome", "entries", "rawBytes", "traceSha256", "abilityKills", "bossKills", "cells")}
    write(OUT / "reports/replay-equality.json", report)


def main():
    (OUT / "reports").mkdir(parents=True, exist_ok=True)
    (OUT / "playmode").mkdir(exist_ok=True)
    for scope in SCOPES:
        encode(scope)
    replay()


if __name__ == "__main__":
    main()

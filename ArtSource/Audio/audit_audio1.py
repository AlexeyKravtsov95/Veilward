"""Read-only verification of source WAVs, Unity references and Editor evidence."""
from array import array
from hashlib import sha256
import argparse
import json
from math import isfinite, log10, sqrt
from pathlib import Path
import re
import subprocess
import wave

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / "Assets/_Game/Audio"


def need(condition, reason):
    if not condition:
        raise ValueError(reason)


def decibels(value):
    return round(20 * log10(value), 2) if value else None


def digest(path):
    return sha256(path.read_bytes()).hexdigest()


def sources():
    rows = []
    catalog = (AUDIO / "VeilwardAudioCatalog.asset").read_text()
    mixer = (AUDIO / "Veilward.mixer").read_text()
    for name in ("MasterVolume", "MusicVolume", "SfxVolume"):
        need(f"name: {name}\n" in mixer, "Missing exposed parameter " + name)
    for path in sorted((AUDIO / "Clips").glob("*.wav")):
        music = path.stem in ("MenuMusic", "BattleMusic")
        meta = path.with_suffix(".wav.meta").read_text()
        guid = re.search(r"^guid: ([0-9a-f]+)$", meta, re.M)[1]
        need(catalog.count("guid: " + guid) == 1, "Catalog cue mapping " + path.name)
        with wave.open(str(path)) as wav:
            channels, width, rate, frames = (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes())
            need((channels, width, rate) == ((2 if music else 1), 2, 48000), f"Source format {path.name}")
            samples = array("h", wav.readframes(frames))
        peak = max(abs(item) for item in samples) / 32768
        need(.05 < peak < .7, path.name + " sound range")
        join = max(abs(samples[channel] - samples[-channels + channel]) for channel in range(channels))
        need(join < (500 if music else 2), path.name + " boundary transient")
        need(("loadType: 2" if music else "loadType: 0") in meta, path.name + " load type")
        need(("compressionFormat: 1" if music else "compressionFormat: 2") in meta, path.name + " import codec")
        rows.append(dict(name=path.stem, guid=guid, sha256=digest(path), pcmBytes=path.stat().st_size,
                         sourceChannels=channels, sourceRate=rate, seconds=round(frames/rate, 5),
                         sourcePeakDbfs=decibels(peak), boundaryDeltaPcm16=join,
                         decodedMonoBytesAt32kFloat=round(frames/rate*32000*4) if not music else None))
    need(len(rows) == 50 and len({row["guid"] for row in rows}) == 50, "Expected 50 distinct audio assets")
    return rows


def require_stream(data):
    streams = data["streams"]
    audio = next((stream for stream in streams if stream["codec_type"] == "audio"), None)
    video = next((stream for stream in streams if stream["codec_type"] == "video"), None)
    need(audio and video and audio["channels"] == 2, "Expected Editor video with stereo audio")
    return audio, video


def verify_peak(peak):
    need(isfinite(peak) and .005 < peak < .999, "Editor mix is silent, invalid or clipped")


def evidence(video, manifest):
    data = json.loads(manifest.read_text())
    metadata = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-show_streams", "-of", "json", str(video)]))
    audio, picture = require_stream(metadata)
    rate = int(audio["sample_rate"])
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(video), "-map", "0:a:0", "-f", "f32le", "-"])
    samples = array("f")
    samples.frombytes(raw)
    peak = max(abs(value) for value in samples)
    verify_peak(peak)
    verify_peak(data["peak"])
    need(not data["errors"], "Capture console errors")
    need(abs(float(audio["duration"]) - float(picture["duration"])) <= .075, "Audio/video duration drift")
    marks = {mark["label"]: mark["frame"] / data["frameRate"] for mark in data["marks"]}
    for label in ("MenuMusic-wrap", "BattleMusic-wrap", "accepted-seal", "resume", "file-continue",
                  "synthetic-six-tower-density", "earned-victory", "synthetic-terminal-seal-victory",
                  "synthetic-leak-defeat"):
        need(label in marks, "Missing capture marker " + label)
    mute, unmute = marks["menu-mute-on"], marks["menu-mute-off-restored"]

    def rms(start, end):
        segment = samples[round(start * rate) * 2:round(end * rate) * 2]
        need(segment, "Empty measurement segment")
        return sqrt(sum(value*value for value in segment) / len(segment))

    muted_level = rms(mute + .26, unmute - .15)
    restored_level = rms(unmute + .26, unmute + .9)
    need(restored_level > .001 and muted_level < restored_level * .04, "No measurable mute / restore")
    need(max(data["activeVoices"].values()) <= 14, "Voice cap exceeded in game")
    budget = data["budgetAtPeak"]
    for stage in ("ordinary-wave-one", "synthetic-six-tower-density", "earned-wave-eight-continue"):
        need(budget[stage] == dict(sources=15, registeredClips=50, loadedSfx=48, streamingReady=1, musicPlaying=True),
             "Unexpected scene audio budget: " + stage)
    return dict(codec=audio["codec_name"], channels=audio["channels"], sampleRate=rate,
                duration=float(audio["duration"]), videoCodec=picture["codec_name"], width=picture["width"],
                height=picture["height"], videoSha256=digest(video), rawPeakDbfs=decibels(data["peak"]),
                peakDbfs=decibels(peak), mutedRmsDbfs=decibels(muted_level), restoredRmsDbfs=decibels(restored_level),
                marks=marks, activeVoices=data["activeVoices"], budgetAtPeak=budget)


def negative_controls():
    controls = ((require_stream, {"streams": [{"codec_type": "video"}]}),
                (verify_peak, 0), (verify_peak, 1), (verify_peak, float("nan")))
    for check, value in controls:
        try:
            check(value)
        except ValueError:
            continue
        raise AssertionError("Negative control was accepted")
    return len(controls)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--video", type=Path)
    parser.add_argument("--manifest", type=Path)
    args = parser.parse_args()
    clips = sources()
    report = dict(clips=clips, negativeControls=negative_controls(),
                  decodedSfxBytesUpperEstimate=sum(row["decodedMonoBytesAt32kFloat"] or 0 for row in clips))
    if args.video and args.manifest:
        report["editor"] = evidence(args.video, args.manifest)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()

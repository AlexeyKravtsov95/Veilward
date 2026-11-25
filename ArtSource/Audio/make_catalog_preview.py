"""Sequence the actual source WAV cues with gaps and write an AAC preview plus cue times."""
from pathlib import Path
import json
import subprocess
import sys
import wave

ROOT = Path(__file__).resolve().parents[2]
NAMES = (
    "FlameShot1", "FlameImpact1", "TideShot1", "TideImpact1", "GaleShot1", "GaleImpact1",
    "StoneShot1", "StoneImpact1", "SteamShot1", "SteamImpact1", "StormShot1", "StormImpact1",
    "SteamBurst", "StormChain", "StormHop", "SealTarget", "SealCast", "SealHit",
    "BossSpawn", "BossThreat", "BossDeath", "BossLeak", "WaveStart", "WaveEnd", "Victory", "Defeat",
    "Select", "Confirm", "Cancel", "Reject", "Build", "Upgrade", "Sell", "Unlock", "SaveError", "SaveRecovered",
)


def main(out):
    out.mkdir(exist_ok=True, parents=True)
    catalog = ROOT / "Assets/_Game/Audio/Clips"
    pcm, marks = bytearray(), {}
    for name in NAMES:
        with wave.open(str(catalog / (name + ".wav"))) as source:
            rate = source.getframerate()
            assert (source.getnchannels(), source.getsampwidth(), rate) == (1, 2, 48000)
            marks[name] = round(len(pcm) / (rate * 2), 3)
            pcm.extend(source.readframes(source.getnframes()))
        pcm.extend(bytes(round(rate * .24) * 2))
    subprocess.run(["ffmpeg", "-v", "error", "-f", "s16le", "-ac", "1", "-ar", "48000", "-i", "-",
                    "-c:a", "aac", "-b:a", "112k", str(out / "cue-catalog.m4a")], input=pcm, check=True)
    (out / "cue-times.json").write_text(json.dumps(marks, indent=2) + "\n")


if __name__ == "__main__":
    main(Path(sys.argv[1]))

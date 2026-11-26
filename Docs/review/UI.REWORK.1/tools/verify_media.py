"""Проверить размеры PNG, полное декодирование видео и число кадров по записи."""
import hashlib
import json
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
EXPECTED = {
    "story": ("menu-new", "menu-load-error", "first-element", "element-preview", "save-error",
              "preparation", "build-locked", "build-preview", "tower-low-gold", "sell-confirmation",
              "pause", "menu-continue"),
    "earned": ("menu-earned", "earned-preparation", "ability-preview", "boss-combat",
               "enemy-inspector", "victory", "menu-finished"),
    "profiles": tuple(f"{profile}-{screen}" for profile in ("narrow", "modern", "ipad")
                      for screen in ("menu", "hud", "build", "pause")),
    "defeat": ("defeat-save-error", "defeat"),
    "stress": ("upgrade", "dense-wave"),
    "milestones": ("after-wave-3", "after-wave-6"),
}
SCREEN = {"narrow": (1136, 640), "modern": (2556, 1179), "ipad": (2048, 1536)}


def checksum(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_video(path, rows):
    result = subprocess.check_output(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=codec_name,width,height,r_frame_rate,nb_read_frames,duration",
        "-of", "json", str(path)], text=True)
    stream, = json.loads(result)["streams"]
    assert (stream["width"], stream["height"]) == (1600, 900)
    assert stream["codec_name"] == "h264" and stream["r_frame_rate"] == "20/1"
    assert int(stream["nb_read_frames"]) == sum(row.startswith("frame\t") for row in rows)
    assert int(stream["nb_read_frames"]) > 100
    subprocess.run(["ffmpeg", "-nostdin", "-v", "error", "-xerror", "-i", str(path),
                   "-map", "0:v:0", "-f", "null", "-"], check=True)
    return {"path": str(path.relative_to(ROOT)), "sha256": checksum(path), **stream}


def verify():
    images = []
    video = []
    for folder, expected in EXPECTED.items():
        rows = (ROOT / folder / "events.tsv").read_text().splitlines()
        assert rows[-1].startswith("done\t") and rows[-1].endswith("\tFalse")
        for name in expected:
            path = ROOT / folder / f"{name}.png"
            with Image.open(path) as image:
                dimensions = SCREEN.get(name.split("-", 1)[0], (1600, 900))
                assert image.size == dimensions and image.format == "PNG", path
                image.verify()
            assert any(row.startswith("png\t") and f"\t{name}\t" in row for row in rows), path
            images.append({"path": str(path.relative_to(ROOT)), "size": dimensions, "sha256": checksum(path)})
        if folder in ("story", "earned"):
            video.append(verify_video(ROOT / folder / "gameview.mp4", rows))
    return {"png": images, "video": video}


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))

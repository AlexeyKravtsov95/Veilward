"""Rebuild runtime sprite PNGs from the registered RGBA masters (Pillow 12.3)."""

import argparse
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

SOURCE = Path(__file__).resolve().parent
OUTPUT = SOURCE.parents[1] / "Assets/_Game/Art/Sprites/InkAndLanterns"
HEIGHT = 384
MARGIN = 12

# Inverse-warp control points for one extreme of the gait, in normalized canvas units.
# Each pair moves a point in the 3-by-3 interior of the sampling grid.
GAIT = {
    "Shade": (
        ((.018, -.003), (.007, .003), (0, .005)),
        ((.043, -.017), (.010, .010), (-.018, .022)),
        ((.052, -.008), (.039, -.025), (-.046, .035)),
    ),
    "Warden": (
        ((.010, -.004), (0, 0), (0, 0)),
        ((-.033, .019), (.002, .005), (.012, -.008)),
        ((-.051, -.018), (.022, .018), (-.025, -.024)),
    ),
}

# Roof edge in the left half of Flame3.png. Remove only the duplicate fox above it.
FLAME3_ROOF = (
    (0, 275), (240, 275), (267, 268), (300, 269), (335, 266),
    (363, 259), (396, 250), (441, 244), (468, 241), (482, 230),
    (503, 224), (531, 225), (552, 229), (565, 215), (585, 204),
    (609, 211), (646, 235), (887, 235),
)


def cutout(image):
    silhouette = image.getchannel("A").point(lambda value: 255 if value >= 72 else 0)
    x0, y0, x1, y1 = silhouette.getbbox()
    image = image.crop((x0 - 3, y0 - 3, x1 + 3, y1 + 3))
    image.putalpha(image.getchannel("A").point(lambda value: value if value >= 10 else 0))
    return image


def register(image):
    image = cutout(image)
    scale = min((HEIGHT - 2 * MARGIN) / image.height, (512 - 2 * MARGIN) / image.width)
    size = (round(image.width * scale), round(image.height * scale))
    resized = image.resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (size[0] + 2 * MARGIN, HEIGHT))
    canvas.paste(resized, (MARGIN, HEIGHT - MARGIN - resized.height))
    return canvas


def stride(image, control, sign):
    width, height = image.size
    xs = [round(width * n / 4) for n in range(5)]
    ys = [round(height * n / 4) for n in range(5)]
    points = []
    for row, y in enumerate(ys):
        line = []
        for column, x in enumerate(xs):
            dx, dy = control[row - 1][column - 1] if 0 < row < 4 and 0 < column < 4 else (0, 0)
            line.append((x + sign * dx * width, y + sign * dy * height))
        points.append(line)

    mesh = []
    for row in range(4):
        for column in range(4):
            rect = (xs[column], ys[row], xs[column + 1], ys[row + 1])
            quad = (points[row][column] + points[row + 1][column]
                    + points[row + 1][column + 1] + points[row][column + 1])
            mesh.append((rect, quad))
    return image.convert("RGBa").transform(image.size, Image.Transform.MESH, mesh,
                                           Image.Resampling.BICUBIC).convert("RGBA")


def build():
    for level in range(1, 4):
        name = f"Flame{level}"
        with Image.open(SOURCE / "Masters" / (name + ".png")) as image:
            middle = image.width // 2
            shrine = image.crop((0, 0, middle, image.height))
            if level == 3:
                mask = Image.new("L", shrine.size, 255)
                ImageDraw.Draw(mask).polygon(((0, 0), (middle, 0), *reversed(FLAME3_ROOF)), fill=0)
                shrine.putalpha(ImageChops.multiply(shrine.getchannel("A"), mask))
            yield name + "Shrine", register(shrine)
            yield name + "Fox", register(image.crop((middle, 0, image.width, image.height)))

    for name, control in GAIT.items():
        with Image.open(SOURCE / "Masters" / (name + ".png")) as image:
            pose = register(image)
        yield name + "Stride0", pose
        yield name + "Stride1", stride(pose, control, 1)
        yield name + "Stride2", stride(pose, control, -1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Check existing PNG bytes without writing")
    options = parser.parse_args()
    for name, image in build():
        path = OUTPUT / (name + ".png")
        encoded = BytesIO()
        image.save(encoded, format="PNG", optimize=True)
        if options.check:
            if path.read_bytes() != encoded.getvalue():
                raise SystemExit(f"Runtime PNG differs from master: {path}")
        else:
            OUTPUT.mkdir(parents=True, exist_ok=True)
            path.write_bytes(encoded.getvalue())


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Rasterize the vector masters and paper grain into the uGUI sprite sheet."""
import io
import json
import random
from pathlib import Path

import cairosvg
from PIL import Image, ImageOps

HERE = Path(__file__).resolve().parent
DESTINATION = HERE.parents[2] / "Assets/_Game/Art/UI/InkAndLanterns"

# All icons use the same 80-unit artboard. The shapes and gradient stops are the vector masters.
FLAME = '<path d="M41 7C48 23 66 29 62 50C60 66 49 72 36 70C20 68 14 59 16 47C17 35 27 28 27 18C36 22 36 37 33 45C46 37 51 25 41 7Z" fill="url(#ember)"/><path d="M42 31C46 44 27 47 30 58C33 69 51 63 49 54C48 50 43 48 40 53C47 53 43 61 38 59C31 56 46 45 42 31Z" fill="#ffdf8d" stroke="none"/><path d="M21 35Q11 48 19 55M49 17Q56 21 58 28" fill="none" stroke="#ffbe81" stroke-width="2"/>'
CLOUD = '<path d="M8 57Q19 50 34 54C26 51 32 43 42 45C49 34 63 41 62 48Q70 44 75 53L78 58Q65 62 50 59L57 64Q41 67 29 63L35 67Q14 68 8 63Z" fill="url(#jade)"/>'
ICONS = {
    "Seal": '<circle cx="40" cy="40" r="34" fill="#122b38" stroke="#402526" stroke-width="4"/><circle cx="40" cy="40" r="31" fill="url(#pool)" stroke="#e5b477" stroke-width="2"/><circle cx="40" cy="40" r="26" fill="none" stroke="#90b6a4" stroke-dasharray="15 4 2 3" stroke-width="1"/><g transform="translate(12 4) scale(.70)">' + FLAME + '</g><g transform="translate(9 23) scale(.76 .67)">' + CLOUD + '</g><path d="M5 39l6-7m59 16 5-8M19 65l-5-5m48-42 5 4" stroke="#fac783" fill="none"/>',
    "Heart": '<path d="M39 69 12 40 9 22 24 13 40 22 56 13 70 23 68 40Z" fill="url(#ember)" stroke="#f8d59f" stroke-width="3"/><path d="m16 25 10-7 14 11 15-11 10 7-3 12-23 26L17 37Z" fill="none" stroke="#8a3b47"/><path d="m39 36-7 10 7 9 8-9z" fill="#ffdf9c" stroke="none"/>',
    "Gold": '<circle cx="39" cy="42" r="28" fill="#9b522c" stroke="#493032" stroke-width="3"/><circle cx="40" cy="38" r="27" fill="url(#gold)" stroke="#ffdd96" stroke-width="3"/><circle cx="40" cy="38" r="20" fill="none" stroke="#b9703d" stroke-width="2"/><path d="m40 24 13 14-13 14-13-14Z" fill="#663e37" stroke="#ffe19a" stroke-width="2"/><path d="m21 30 3-6 8-4m25 27-6 7" fill="none" stroke="#fff0bd" stroke-width="2"/>',
    "Wave": '<path d="m6 49 21-21 13 11 15-19 18 14-5 8-12-8-16 18-13-11L11 56Z" fill="#9bc9b1"/><path d="m9 60 19-16 12 10 16-16 16 10-4 8-11-6-16 15-13-10-15 12Z" fill="url(#jade)"/><path d="m16 45 11-12 13 11 15-19 10 8" fill="none" stroke="#d5e4bd" stroke-width="1.4"/>',
    "Pause": '<path d="m24 14 11 2 1 45-12 5-4-12Zm20 3 13-3 1 38-7 12-6-3Z" fill="#32242a" stroke="#714434"/><path d="M27 21l-2 33m24-30 2 33" fill="none" stroke="#976754" stroke-width="1.1"/>',
    "Flame": FLAME,
    "Tide": '<path d="M39 7C42 25 66 40 63 54C60 74 19 76 17 54C14 39 34 24 39 7Z" fill="url(#water)" stroke="#d0e8e1"/><path d="M37 36C16 48 30 66 47 60C56 56 50 47 45 51C50 54 44 59 38 56C30 53 31 46 37 36Z" fill="#cbecef" stroke="none"/><path d="M28 33 33 25M57 44l2 7" stroke="#daf4e8" stroke-width="2" fill="none"/>',
    "Gale": '<path d="M7 48C18 28 39 31 50 23C53 19 45 13 37 19C45 6 62 13 61 24C62 35 38 36 31 42C48 44 58 28 70 35C81 42 69 56 61 54C68 49 65 43 58 47C45 57 39 52 25 56L11 64C14 55 26 49 34 48C22 45 14 49 7 48Z" fill="url(#jade)" stroke="#d0d9a1" stroke-width="1.5"/><path d="M21 37Q30 31 40 30M35 62Q44 58 49 60Q46 68 34 69Z" fill="#d7d59e"/>',
    "Stone": '<path d="m13 58 3-24 12 9 4 24-8 4zM56 44l10-13 4 28-10 12-10-5z" fill="url(#sand)"/><path d="M27 48 29 22 42 8 53 24 57 60 42 73 27 67Z" fill="url(#sand)" stroke="#f0ddba"/><path d="m42 8 2 31-15-17m15 17 9-15m-9 15-2 34" fill="none" stroke="#79595a"/><path d="m31 23 10 15-4 25-6-4z" fill="#f3dbb4" opacity=".72" stroke="none"/>',
    "Steam": '<g transform="translate(-7 -6) scale(.79)">' + FLAME + '</g><path d="M22 60C7 51 27 41 41 44C37 38 42 30 49 32C58 32 63 39 58 45C70 41 76 53 69 59C64 65 55 65 48 62L53 69C43 73 28 70 22 60Z" fill="url(#sand)" stroke="#f3c2a0"/><path d="M23 55C32 50 47 55 48 50C48 45 43 46 42 48m15 8q8-4 10 0" fill="none" stroke="#a45b55" stroke-width="2"/>',
    "Storm": '<path d="M7 45Q26 18 51 20Q63 20 69 11L66 24 76 29 67 32 68 43 56 35Q39 37 22 53L9 56 15 47Z" fill="url(#water)" stroke="#ebdfc2"/><path d="M61 20Q37 10 23 24L19 21 12 23 25 27Q40 23 48 25Z" fill="#9cc4c9"/><path d="m51 38-16 18 13-2-8 19 27-28-15 3 8-12Z" fill="url(#gold)" stroke="#fce29a"/><circle cx="20" cy="43" r="2.3" fill="#ffdd8e" stroke="none"/>',
    "Lock": '<path d="M24 36V27C24 5 56 5 56 27V36H49V27C49 15 31 15 31 27V36Z" fill="#c8b7a3" stroke="#342632"/><path d="m16 35 8-5h32l8 5v27l-7 7H23l-7-7Z" fill="url(#sand)" stroke="#43323c" stroke-width="3"/><path d="M44 49a5 5 0 1 0-8 0l-1 9h10z" fill="#54424c" stroke="none"/>',
    "Gate": '<path d="M9 21Q40 31 71 21L68 31Q36 37 11 30Z" fill="#5b3737" stroke="#d78c6c"/><path d="M18 39h44v6H18Z" fill="#b85b50"/><path d="M22 30h9l-2 36H18Zm27 0h9l4 36H51Z" fill="url(#ember)"/><path d="M35 31h10v10H35z" fill="url(#gold)"/>',
    "Warden": '<path d="M8 38 13 15 37 28 62 12 67 35 75 45 60 48 57 60 43 73 28 56 17 53 21 42Z" fill="url(#sand)" stroke="#382635" stroke-width="3"/><path d="m13 19 11 22 15-11-5 17-9 3 14 14 18-17-10-6 16-24-2 25-9 12L43 72 30 56l-6-3-6-2 4-12Z" fill="url(#ember)"/><path d="m25 37 12 6-10 1m18-2 14-8-7 10M39 59l9-1-5 6Z" fill="#3a2734" stroke="none"/>',
    "Blossom": '<path d="M3 66Q25 40 53 32L72 13M22 48l-1-20m22 8 14 10m3-23 1-11" fill="none" stroke="#513139" stroke-width="4"/>' + ''.join(
        f'<g transform="translate({x} {y}) scale({scale})" stroke="#b86365" stroke-width="1.2"><path d="M0-3C-13-11-6-17 0-14C7-18 13-9 6-3C17-6 18 4 11 7C17 14 6 19 1 10C-5 19-15 13-10 6C-19 4-16-6-7-4Z" fill="url(#ember)"/><circle cx="0" cy="1" r="3" fill="#ffe0a5" stroke="none"/></g>'
        for x, y, scale in [(19, 29, .72), (40, 32, .88), (61, 10, .60), (65, 27, .62), (52, 50, .60), (8, 51, .56)]),
    "FoxCrest": '<circle cx="27" cy="32" r="22" fill="url(#ember)" stroke="none"/><path d="M5 53C18 43 25 45 36 43C37 38 42 32 53 32L48 16Q62 20 64 29L70 34L78 38 72 45 62 48Q54 53 49 61C36 64 25 62 19 67L6 67 18 58 3 60 16 53Z" fill="url(#sand)" stroke="#512c37"/><path d="M9 56Q27 48 42 48L35 44 50 40l-4 8-10 6 10 3-9 3-13-2-14 6Z" fill="#b65b54" stroke="none"/><path d="m55 21 6 10-6-2-4-10m10 17 7 1-5 3z" fill="#b65b54" stroke="none"/><path d="m61 36 5 2-5 1zM74 38l4 1-3 3z" fill="#492831" stroke="none"/>',
}

# Small combat seals use broad, distinct contours that survive downsampling.
BADGES = {
    "BurnBadge": '<path d="M34 12C34 23 16 27 16 43C16 54 23 59 34 59C45 59 52 51 50 40C49 31 41 27 34 12Z" fill="url(#ember)"/><path d="M34 34C36 40 43 43 42 49C41 57 26 57 26 49C26 44 32 41 34 34Z" fill="#ffe0a3" stroke="none"/>',
    "WetBadge": '<path d="M34 12C29 24 17 33 17 44C17 64 51 64 51 44C51 33 39 24 34 12Z" fill="url(#water)"/><path d="M26 43Q23 49 31 52" fill="none" stroke="#eff8d9" stroke-width="3"/>',
    "GustBadge": '<path d="M12 30H37Q49 30 49 22Q49 16 43 17M11 40H49Q57 40 56 34M19 50H37Q44 50 44 55" fill="none" stroke="#bde3c9" stroke-width="5"/>',
    "CrackBadge": '<path d="M14 23 33 13 51 21 55 45 43 55 21 54 13 40Z" fill="url(#sand)"/><path d="M35 14 29 29 39 33 28 43 26 55M39 33 52 27M28 43 17 39" fill="none" stroke="#4e3748" stroke-width="4"/>',
    "SteamBadge": '<path d="M19 39C9 28 29 28 22 17M34 39C24 30 43 27 35 13M48 39C39 30 55 28 51 18M16 48Q34 53 52 48" fill="none" stroke="#f9dbba" stroke-width="5"/>',
    "StormBadge": '<path d="M39 11 17 38 32 38 25 58 51 29 36 29 43 13Z" fill="url(#gold)" stroke="#ffe5aa" stroke-width="2"/>',
}

GRADIENTS = {
    "ember": ("#ffbb85", "#c1534d", "#822f43"),
    "water": ("#ddf3d6", "#4c9fb0", "#225273"),
    "jade": ("#c5cc92", "#74a598", "#3c6465"),
    "sand": ("#fff1cd", "#d9ae93", "#82636a"),
    "gold": ("#fff1b6", "#e5a34d", "#b0663b"),
    "pool": ("#3a726d", "#23444d", "#142b40"),
}

# Each silhouette has its own corners. The central nine-slice region carries only a soft wash.
SURFACES = {
    "InkStrip": ("#1c2939", "#121e2e", "#bd8f78", 30,
        "M12 27C18 14 32 14 51 14C99 14 150 15 198 14C214 13 226 17 234 26"
        "Q227 29 234 37C225 51 232 74 228 98Q218 111 195 110C144 110 81 110 41 110"
        "Q17 110 7 96Q21 92 12 81Q5 74 14 61Q18 48 8 38Z"),
    "InkCard": ("#253437", "#19242f", "#9a816e", 26,
        "M11 27Q9 18 24 15L89 15Q162 14 221 15Q230 15 230 28"
        "L231 89Q233 105 217 109Q185 111 158 111L32 111Q12 110 10 97Q10 58 11 27Z"),
    "PaperCard": ("#f3dfbc", "#e8cba5", "#785246", 28,
        "M11 23Q10 16 26 14Q61 13 93 14L204 14Q229 10 230 27Q231 55 230 81"
        "Q233 102 223 110Q206 113 191 110L48 112Q41 110 24 110Q10 110 10 96Q9 51 11 23Z"),
    "CoralButton": ("#b25455", "#8e404a", "#e8b888", 28,
        "M16 29Q20 16 42 15Q102 15 176 15Q211 11 227 20Q234 24 227 34"
        "Q220 42 226 53Q231 64 223 81Q218 91 220 97Q211 111 193 110"
        "L40 110Q25 111 12 102Q7 98 13 86Q17 75 12 64Q8 56 13 43Z"),
    "PaperButton": ("#f2dfbd", "#e4c29d", "#765245", 30,
        "M14 31Q17 19 33 16Q74 15 112 15Q164 17 211 14Q227 14 231 24"
        "Q232 31 227 43Q224 54 229 68Q231 80 224 91Q227 103 211 108"
        "L47 110Q22 112 13 104Q8 98 13 85Q18 76 11 61Q8 52 14 31Z"),
    "JadeButton": ("#416b65", "#2e504f", "#ab9476", 27,
        "M11 31Q12 19 30 15Q63 14 96 15L208 14Q223 14 230 25L229 85"
        "Q232 104 216 109L37 110Q15 110 10 101L11 31Z"),
    "DarkButton": ("#2a3d48", "#202d3b", "#7e796f", 24,
        "M11 28Q10 19 23 16L213 16Q228 14 230 29L230 93Q229 109 214 109"
        "Q154 110 102 109L29 110Q12 109 10 96Z"),
    "HealthTrack": ("#432e39", "#342431", "#856051", 16,
        "M10 41Q10 17 37 16L199 16Q229 16 230 45L230 77Q230 106 202 108"
        "L41 108Q10 108 10 84Z"),
}


def svg_image(body, width, height):
    stops = "".join(
        f'<linearGradient id="{name}" x2=".18" y2="1"><stop stop-color="{top}"/>'
        f'<stop offset=".55" stop-color="{mid}"/><stop offset="1" stop-color="{bottom}"/></linearGradient>'
        for name, (top, mid, bottom) in GRADIENTS.items()
    )
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}"><defs>{stops}</defs>{body}</svg>'
    png = cairosvg.svg2png(bytestring=svg.encode(), output_width=width * 3, output_height=height * 3)
    return Image.open(io.BytesIO(png)).convert("RGBA").resize((width, height), Image.Resampling.LANCZOS)


def surface(name, colors):
    upper, lower, line, _, path = colors
    GRADIENTS["wash"] = (upper, upper, lower)
    # Faint dry pigment stays in the fixed-size corners, outside the stretch region.
    rng = random.Random(name)
    scuffs = []
    for x_edge in (12, 220):
        for y_edge in (16, 105):
            for _ in range(14):
                x = x_edge + rng.uniform(-5, 5)
                y = y_edge + rng.uniform(-4, 4)
                length = rng.uniform(.8, 3.8)
                scuffs.append(f'<path d="M{x:.2f} {y:.2f}q{length / 2:.2f} -.7 {length:.2f} -.5"/>')
    body = (
        f'<clipPath id="edge"><path d="{path}"/></clipPath>'
        f'<path d="{path}" transform="translate(0 2)" fill="#161725" opacity=".43"/>'
        f'<path d="{path}" fill="url(#wash)" stroke="{line}" stroke-width="1.45" stroke-linejoin="round"/>'
        f'<g clip-path="url(#edge)" fill="none" stroke="{line}" opacity=".21" stroke-width=".6">{"".join(scuffs)}</g>'
    )
    image = svg_image(body, 240, 124)
    del GRADIENTS["wash"]
    return image


def render():
    atlas = Image.new("RGBA", (1024, 512))
    slices = []

    def place(name, image, x, y, border=0):
        atlas.paste(image, (x, y))
        slices.append(dict(name=name, x=x, y=atlas.height - y - image.height,
                           width=image.width, height=image.height, border=border))

    for index, (name, body) in enumerate(ICONS.items()):
        body = f'<g stroke="#38263b" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">{body}</g>'
        place(name, svg_image(body, 80, 80), 3 + (index % 12) * 84, 4 + (index // 12) * 84)
    for index, (name, colors) in enumerate(SURFACES.items()):
        place(name, surface(name, colors), 5 + (index % 4) * 251, 178 + (index // 4) * 130, colors[3])
    for index, (name, body) in enumerate(BADGES.items()):
        seal = '<path d="M17 3H51L65 17V51L51 65H17L3 51V17Z" fill="#182331" stroke="#dfc4a0" stroke-width="2.5"/>'
        body = f'{seal}<g stroke="#38263b" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">{body}</g>'
        place(name, svg_image(body, 68, 68), 5 + index * 74, 438)

    DESTINATION.mkdir(parents=True, exist_ok=True)
    atlas.save(DESTINATION / "InkUi.png", optimize=True)
    (HERE / "slices.json").write_text(json.dumps(slices, indent=2) + "\n")
    if (HERE / "menu-master.png").exists():
        source = Image.open(HERE / "menu-master.png").convert("RGB")
        ImageOps.contain(source, (1792, 1024), Image.Resampling.LANCZOS).save(
            DESTINATION / "MenuBackdrop.jpg", quality=92, optimize=True
        )


if __name__ == "__main__":
    render()

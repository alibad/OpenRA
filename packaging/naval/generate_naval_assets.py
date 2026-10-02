#!/usr/bin/env python3
"""Generate deterministic OpenRA naval sprites, icons, effects, and normalized WAV audio."""

from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
import random
import struct
import subprocess
import wave

from PIL import Image, ImageDraw, ImageFont


FACINGS = 32
CANVAS = (96, 72)
ROOT = Path(__file__).resolve().parents[2]
FRAME_ROOT = ROOT / "artifacts" / "naval-sprites" / "generated"
AUDIT_ROOT = ROOT / "artifacts" / "naval-audit" / "custom"
BITS_ROOT = ROOT / "mods" / "ra" / "bits"
SOUND_ROOT = BITS_ROOT / "naval"
PALETTE_PATH = ROOT / "mods" / "ra" / "maps" / "chernobyl" / "temperat.pal"
UTILITY = ROOT / "bin" / "OpenRA.Utility.exe"


ICON_LABELS = {
    "sa_frgt": "AL RIYADH",
    "sa_intc": "AL SADIQ",
    "sa_fss": "AL JUBAIL",
    "ye_mslc": "HODEIDAH",
    "ye_usv": "SAHAAB USV",
    "ye_surve": "MOKHA",
}
FONT_PATH = ROOT / "mods" / "common" / "FreeSansBold.ttf"
PORTRAIT_YAW = math.radians(118)  # bow toward the lower left; no world facing uses it

VESSELS = {
    "sa_frgt": dict(length=68, width=17, bow=0.15, bridge=-4, bridge_len=17, mast=-7, deck=18, accent="saudi", turret=True, ciws=True),
    "sa_intc": dict(length=43, width=13, bow=0.05, bridge=-1, bridge_len=12, mast=-3, deck=11, accent="saudi", turret=True),
    "sa_fss": dict(length=63, width=20, bow=0.18, bridge=-12, bridge_len=17, mast=-13, deck=25, accent="saudi", crane=True),
    "ye_mslc": dict(length=47, width=14, bow=0.08, bridge=-3, bridge_len=14, mast=-5, deck=12, accent="yemen", missiles=True, turret=True),
    "ye_usv": dict(length=31, width=11, bow=0.02, bridge=2, bridge_len=7, mast=1, deck=8, accent="yemen", usv=True),
    "ye_surve": dict(length=41, width=14, bow=0.10, bridge=-2, bridge_len=13, mast=-5, deck=14, accent="yemen", radar=True, turret=True),
}


def load_palette() -> tuple[list[int], list[tuple[int, int, int]]]:
    raw = PALETTE_PATH.read_bytes()
    if len(raw) < 768:
        raise ValueError(f"Palette is too small: {PALETTE_PATH}")
    colors = [tuple(min(255, c * 4) for c in raw[i:i + 3]) for i in range(0, 768, 3)]
    flat = [channel for color in colors for channel in color]
    return flat, colors


PALETTE, COLORS = load_palette()


def nearest(rgb: tuple[int, int, int], excluded: set[int] | None = None) -> int:
    excluded = excluded or set()
    return min(
        (i for i in range(5, 256) if i not in excluded),
        key=lambda i: sum((COLORS[i][c] - rgb[c]) ** 2 for c in range(3)),
    )


IDX = {
    "shadow": 4,
    "water_dark": nearest((25, 59, 76)),
    "hull_dark": nearest((52, 63, 67), set(range(80, 96))),
    "hull": nearest((116, 126, 126), set(range(80, 96))),
    "hull_light": nearest((181, 187, 180), set(range(80, 96))),
    "deck": nearest((78, 85, 84), set(range(80, 96))),
    "window": nearest((24, 51, 63)),
    "black": nearest((15, 17, 18)),
    "white": nearest((225, 226, 211)),
    "rust": nearest((116, 55, 37), set(range(80, 96))),
    "saudi_accent": nearest((184, 155, 76), set(range(80, 96))),
    "yemen_accent": nearest((142, 58, 44), set(range(80, 96))),
    "fire": nearest((244, 118, 36)),
    "foam": nearest((174, 206, 205)),
    "smoke": nearest((57, 56, 53)),
}


# Player-remap ramp entries for the painted hull and superstructure (80 is the
# brightest shade).  These reproduce the ownership colors that were previously
# patched into the shipped SHPs outside this generator.
TEAM = {"hull": 87, "hull_light": 82}


def paint(role: str, team: bool) -> int:
    return TEAM[role] if team else IDX[role]


def blank(size: tuple[int, int] = CANVAS) -> Image.Image:
    image = Image.new("P", size, 0)
    image.putpalette(PALETTE)
    image.info["transparency"] = 0
    return image


# OpenRA's classic perspective draws a ground offset of one unit north as
# sin(40 deg) of a unit up the screen (BodyOrientationInfo.LocalToWorld), so the
# hull art uses the same squash and turret offsets line up at every facing.
GROUND_SQUASH = 658 / 1024


def project(local_x: float, local_y: float, yaw: float, z: float = 0, center: tuple[float, float] = (48, 39),
            scale: float = 1.0) -> tuple[int, int]:
    """Project a hull-local point (x starboard, y toward the bow) to the canvas.

    ``yaw`` is the OpenRA facing angle in radians, counter-clockwise from north:
    the bow points up at facing 0, left at facing 8, down at 16, right at 24.
    """

    sin_yaw, cos_yaw = math.sin(yaw), math.cos(yaw)
    east = local_x * cos_yaw - local_y * sin_yaw
    north = local_x * sin_yaw + local_y * cos_yaw
    return round(center[0] + east * scale), round(center[1] - north * GROUND_SQUASH * scale - z * scale)


VIEW = {"center": (48, 39), "scale": 1.0}


def polygon(draw: ImageDraw.ImageDraw, points: list[tuple[float, float]], yaw: float, fill: int, z: float = 0) -> None:
    draw.polygon([project(x, y, yaw, z, VIEW["center"], VIEW["scale"]) for x, y in points], fill=fill)


def line(draw: ImageDraw.ImageDraw, points: list[tuple[float, float]], yaw: float, fill: int, width: int = 1, z: float = 0) -> None:
    draw.line([project(x, y, yaw, z, VIEW["center"], VIEW["scale"]) for x, y in points], fill=fill, width=width)


def vessel_frame(spec: dict, facing: int, damage: int = 0, sink: int = 0, *, yaw: float | None = None,
                 canvas: tuple[int, int] = CANVAS, center: tuple[float, float] = (48, 39), scale: float = 1.0,
                 team: bool = True) -> Image.Image:
    image = blank(canvas)
    draw = ImageDraw.Draw(image)
    if yaw is None:
        yaw = facing * 2 * math.pi / FACINGS
    VIEW["center"], VIEW["scale"] = center, scale

    def at(x: float, y: float, z: float = 0) -> tuple[int, int]:
        return project(x, y, yaw, z, center, scale)

    length, width = spec["length"], spec["width"]
    half_l, half_w = length / 2, width / 2
    sink_drop = sink * 1.25
    sink_tilt = (sink / 7) * 4

    shadow = [(-half_w, -half_l + 5), (-half_w * 0.9, half_l - 8), (0, half_l + 1), (half_w * 0.9, half_l - 8), (half_w, -half_l + 5)]
    # The light is fixed on screen, so the shadow is offset in screen space
    # (down-right) rather than in hull space, where it rotated with the ship.
    shadow_points = [(px + round(3 * scale), py + round(4 * scale)) for px, py in (at(x, y, -sink_drop) for x, y in shadow)]
    draw.polygon(shadow_points, fill=IDX["shadow"])

    hull = [(-half_w * 0.85, -half_l), (-half_w, half_l - 10), (0, half_l), (half_w, half_l - 10), (half_w * 0.85, -half_l)]
    polygon(draw, hull, yaw, IDX["hull_dark"], z=2 - sink_drop)
    inner = [(x * 0.83, y * 0.91) for x, y in hull]
    polygon(draw, inner, yaw, paint("hull", team), z=4 - sink_drop)
    foredeck = [(-half_w * 0.67, 2), (-half_w * 0.55, half_l - 10), (0, half_l - 4), (half_w * 0.55, half_l - 10), (half_w * 0.67, 2)]
    polygon(draw, foredeck, yaw, IDX["deck"], z=5 - sink_drop)

    bridge_y = spec["bridge"]
    bridge_len = spec["bridge_len"]
    bw = half_w * (0.60 if not spec.get("usv") else 0.42)
    box = [(-bw, bridge_y - bridge_len / 2), (-bw, bridge_y + bridge_len / 2), (bw, bridge_y + bridge_len / 2), (bw, bridge_y - bridge_len / 2)]
    polygon(draw, box, yaw, paint("hull_light", team), z=8 - sink_drop + sink_tilt)
    front_y = bridge_y + bridge_len / 2
    line(draw, [(-bw * 0.72, front_y), (bw * 0.72, front_y)], yaw, IDX["window"], width=2, z=9 - sink_drop + sink_tilt)

    mast_y = spec["mast"]
    mast_base = at(0, mast_y, 10 - sink_drop + sink_tilt)
    mast_top = at(0, mast_y, 21 - sink_drop + sink_tilt)
    draw.line([mast_base, mast_top], fill=IDX["black"], width=2)
    draw.line([(mast_top[0] - 4, mast_top[1] + 2), (mast_top[0] + 4, mast_top[1] + 2)], fill=paint("hull_light", team), width=1)
    if spec.get("radar") or spec["deck"] >= 18:
        draw.rectangle((mast_top[0] - 4, mast_top[1] - 1, mast_top[0] + 4, mast_top[1] + 1), fill=paint("hull_light", team))

    if spec.get("missiles"):
        for x in (-5, -2, 2, 5):
            p1 = at(x, -12, 9 - sink_drop)
            p2 = at(x, -21, 12 - sink_drop)
            draw.line([p1, p2], fill=IDX["rust"], width=2)
    if spec.get("crane"):
        base = at(-4, -8, 10 - sink_drop)
        top = at(-4, -8, 21 - sink_drop)
        boom = at(7, -18, 19 - sink_drop)
        draw.line([base, top, boom], fill=paint("hull_light", team), width=2)
        service = [(-half_w * 0.75, -half_l + 4), (-half_w * 0.75, -half_l + 4), (-half_w * 0.75, -10), (half_w * 0.75, -10)]
        polygon(draw, service, yaw, IDX["deck"], z=7 - sink_drop)

    accent_index = IDX["saudi_accent"] if spec["accent"] == "saudi" else IDX["yemen_accent"]
    line(draw, [(-half_w * 0.92, -half_l + 7), (-half_w * 0.98, half_l - 12)], yaw, accent_index, width=2, z=4 - sink_drop)
    if spec.get("usv"):
        dome = at(0, mast_y, 15 - sink_drop)
        draw.ellipse((dome[0] - 3, dome[1] - 3, dome[0] + 3, dome[1] + 3), fill=IDX["window"])

    if damage:
        rng = random.Random(facing * 101 + damage * 991)
        for _ in range(3 + damage * 2):
            x = rng.uniform(-half_w * 0.6, half_w * 0.6)
            y = rng.uniform(-half_l * 0.5, half_l * 0.6)
            p = at(x, y, 8 - sink_drop)
            draw.rectangle((p[0] - 1, p[1] - 1, p[0] + 1, p[1] + 1), fill=IDX["rust"] if damage == 1 else IDX["black"])
        if damage == 2:
            smoke = at(-2, bridge_y - 2, 19 - sink_drop)
            draw.ellipse((smoke[0] - 3, smoke[1] - 6, smoke[0] + 3, smoke[1]), fill=IDX["smoke"])
            draw.point((smoke[0], smoke[1]), fill=IDX["fire"])

    if sink:
        water_y = 44
        draw.rectangle((0, water_y + 7, canvas[0] - 1, canvas[1] - 1), fill=0)
        foam = project(0, 0, yaw, -2, (center[0], center[1] + 6 * scale), scale)
        radius = max(2, 13 - sink)
        draw.arc((foam[0] - radius, foam[1] - radius // 2, foam[0] + radius, foam[1] + radius // 2), 0, 360, fill=IDX["foam"], width=1)

    return image


def turret_frame(size: str, facing: int) -> Image.Image:
    """A turret rotating in place about its own mount.

    The mount is the projection origin; ``Turreted.Offset`` places it on the
    deck.  The earlier art drew the mount 15 px ahead of the origin, so the
    whole turret orbited the hull centre as it turned.
    """

    image = blank()
    draw = ImageDraw.Draw(image)
    yaw = facing * 2 * math.pi / FACINGS
    if size == "ciws":
        # Compact close-in weapon: a white dome, a dark rear housing and a
        # barrel long enough that all 32 facings stay distinct.
        dome = project(0, 0, yaw, 6)
        draw.line([project(0, -3, yaw, 6), project(0, -1, yaw, 6)], fill=IDX["hull_dark"], width=2)
        draw.ellipse((dome[0] - 3, dome[1] - 3, dome[0] + 3, dome[1] + 2), fill=IDX["white"], outline=IDX["hull_dark"])
        draw.line([project(0, 1, yaw, 7), project(0, 9, yaw, 7)], fill=IDX["black"], width=1)
        return image
    radius = 5 if size == "large" else 4
    center = project(0, 0, yaw, 10)
    # A rear housing and a barrel long enough to separate adjacent facings.
    draw.line([project(0, -2, yaw, 10), project(0, -radius - 1, yaw, 10)], fill=IDX["hull_dark"], width=3)
    draw.ellipse((center[0] - radius, center[1] - radius // 2, center[0] + radius, center[1] + radius // 2 + 2), fill=TEAM["hull_light"])
    barrel_start = project(0, 0, yaw, 12)
    barrel_end = project(0, 12 if size == "large" else 10, yaw, 12)
    draw.line([barrel_start, barrel_end], fill=IDX["black"], width=2 if size == "large" else 1)
    return image


def wake_frame(facing: int, frame: int) -> Image.Image:
    image = blank((64, 48))
    draw = ImageDraw.Draw(image)
    yaw = facing * 2 * math.pi / FACINGS
    center = (32, 24)
    spread = 5 + frame * 3
    for side in (-1, 1):
        pts = []
        for step in range(5):
            x = side * (spread + step * 2)
            y = -4 - step * 4 - frame
            pts.append(project(x, y, yaw, 0, center))
        draw.line(pts, fill=IDX["foam"], width=1)
    return image


def effect_frames() -> dict[str, list[Image.Image]]:
    chaff = []
    radar = []
    flooding = []
    for frame in range(12):
        im = blank((64, 48))
        d = ImageDraw.Draw(im)
        rng = random.Random(750 + frame)
        radius = 7 + frame * 2
        for _ in range(18):
            angle = rng.random() * math.tau
            x = 32 + math.cos(angle) * radius
            y = 25 + math.sin(angle) * radius * 0.45
            d.rectangle((round(x), round(y), round(x) + 1, round(y) + 1), fill=IDX["white"])
        chaff.append(im)

        im = blank((64, 48))
        d = ImageDraw.Draw(im)
        angle = frame * math.tau / 12
        d.arc((12, 8, 52, 40), int(math.degrees(angle) - 25), int(math.degrees(angle) + 25), fill=nearest((80, 230, 176)), width=2)
        d.line((32, 24, 32 + math.cos(angle) * 20, 24 + math.sin(angle) * 10), fill=nearest((80, 230, 176)), width=1)
        radar.append(im)

    for frame in range(6):
        im = blank((64, 48))
        d = ImageDraw.Draw(im)
        for i in range(4):
            x = 24 + ((frame * 5 + i * 11) % 18)
            y = 31 + ((frame + i) % 3)
            d.arc((x - 5, y - 2, x + 5, y + 3), 180, 350, fill=IDX["foam"])
        flooding.append(im)
    return {"naval_chaff": chaff, "naval_radar": radar, "naval_flood": flooding}


def recovery_marker(kind: str) -> Image.Image:
    image = blank((32, 24))
    draw = ImageDraw.Draw(image)
    if kind == "survivor":
        draw.ellipse((7, 9, 25, 18), fill=IDX["saudi_accent"], outline=IDX["white"])
        draw.ellipse((14, 6, 18, 10), fill=IDX["hull_light"])
    else:
        draw.polygon([(7, 9), (23, 9), (26, 16), (5, 16)], fill=IDX["hull_dark"], outline=IDX["rust"])
        draw.line((10, 8, 20, 17), fill=IDX["hull_light"], width=2)
    draw.arc((3, 13, 29, 21), 0, 180, fill=IDX["foam"])
    return image


def nearest_static(rgb: tuple[int, int, int]) -> int:
    """Nearest palette entry that is safe in the opaque chrome-palette cameo.

    Excludes transparent 0, the reserved/shadow indices 1, 3 and 4, and the
    remap and palette-animated range 80..103.
    """

    return min(
        (i for i in range(5, 256) if not 80 <= i <= 103),
        key=lambda i: sum((COLORS[i][c] - rgb[c]) ** 2 for c in range(3)),
    )


def icon_for(name: str, spec: dict) -> Image.Image:
    """Opaque 64x48 production cameo with a dedicated three-quarter portrait.

    The earlier cameo pasted an enlarged facing-4 world frame over a
    transparent canvas; its shadow index rendered bright green in the chrome
    palette and every vessel read as the same flat wedge.
    """

    art = vessel_frame(spec, 0, yaw=PORTRAIT_YAW, canvas=(64, 48), center=(32, 25),
                       scale=min(1.25, 50 / spec["length"]), team=False)
    backdrop = Image.new("RGB", (64, 48))
    draw = ImageDraw.Draw(backdrop)
    for y in range(48):
        if y < 18:
            t = y / 18
            color = (round(98 + 34 * t), round(118 + 30 * t), round(132 + 20 * t))
        else:
            t = (y - 18) / 30
            color = (round(44 - 20 * t), round(84 - 34 * t), round(104 - 40 * t))
        draw.line((0, y, 63, y), fill=color)
    for x in range(0, 64, 5):
        draw.point((x + (x // 5) % 3, 28 + (x // 5) % 4), fill=(96, 138, 150))
    art_rgb = art.convert("RGBA")
    pixels = art.load()
    rgba = art_rgb.load()
    for y in range(48):
        for x in range(64):
            index = pixels[x, y]
            if index == 0:
                rgba[x, y] = (0, 0, 0, 0)
            elif index == IDX["shadow"]:
                rgba[x, y] = (0, 0, 0, 110)
    composed = backdrop.convert("RGBA")
    composed.alpha_composite(art_rgb)
    band = Image.new("RGBA", (64, 14), (0, 0, 0, 185))
    composed.alpha_composite(band, (0, 34))
    label = ICON_LABELS[name]
    text = ImageDraw.Draw(composed)
    font_size = 9
    while True:
        font = ImageFont.truetype(str(FONT_PATH), font_size)
        box = text.textbbox((0, 0), label, font=font, stroke_width=1)
        if box[2] - box[0] <= 60 or font_size <= 6:
            break
        font_size -= 1
    text.text(((64 - (box[2] - box[0])) // 2, 36), label, font=font, fill=(245, 245, 236), stroke_width=1, stroke_fill=(10, 10, 10))
    icon = blank((64, 48))
    source = composed.convert("RGB").load()
    target = icon.load()
    cache: dict[tuple[int, int, int], int] = {}
    for y in range(48):
        for x in range(64):
            rgb = source[x, y]
            if rgb not in cache:
                cache[rgb] = nearest_static(rgb)
            target[x, y] = cache[rgb]
    icon.info.pop("transparency", None)
    return icon


def save_frames(name: str, frames: list[Image.Image]) -> list[Path]:
    directory = FRAME_ROOT / name
    directory.mkdir(parents=True, exist_ok=True)
    for stale in directory.glob(f"{name}-*.png"):
        stale.unlink()
    paths = []
    for i, image in enumerate(frames):
        path = directory / f"{name}-{i:04d}.png"
        image.save(path, transparency=0)
        paths.append(path)
    return paths


def build_shp(name: str, frames: list[Image.Image]) -> None:
    paths = save_frames(name, frames)
    destination = BITS_ROOT / f"{name}.shp"
    destination.unlink(missing_ok=True)
    env = os.environ.copy()
    env["ENGINE_DIR"] = str(ROOT)
    result = subprocess.run([str(UTILITY), "ra", "--shp", *(p.name for p in paths)], cwd=paths[0].parent, env=env, text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    generated = paths[0].with_suffix(".shp")
    if not generated.exists():
        candidates = sorted(paths[0].parent.glob("*.shp"), key=lambda p: p.stat().st_mtime, reverse=True)
        if not candidates:
            raise RuntimeError(f"OpenRA.Utility did not create an SHP for {name}")
        generated = candidates[0]
    generated.replace(destination)


def synth(name: str, duration: float, fn) -> None:
    rate = 22050
    rng = random.Random(name)
    samples = []
    for i in range(round(duration * rate)):
        t = i / rate
        value = fn(t, rng)
        envelope = min(1.0, t / 0.012, max(0.0, (duration - t) / 0.035))
        samples.append(value * envelope)
    peak = max(abs(v) for v in samples) or 1
    gain = 0.70 / peak
    pcm = b"".join(struct.pack("<h", round(max(-1, min(1, v * gain)) * 32767)) for v in samples)
    SOUND_ROOT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(SOUND_ROOT / f"{name}.wav"), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(rate)
        out.writeframes(pcm)


def build_audio() -> None:
    tau = math.tau
    synth("engine-fast", 1.25, lambda t, r: .34 * math.sin(tau * 78 * t) + .15 * math.sin(tau * 156 * t) + .05 * (r.random() * 2 - 1))
    synth("engine-heavy", 1.50, lambda t, r: .42 * math.sin(tau * 45 * t) + .20 * math.sin(tau * 90 * t) + .04 * (r.random() * 2 - 1))
    synth("radar-sweep", .82, lambda t, r: .38 * math.sin(tau * (420 + 720 * t) * t) * (1 - t / .82))
    synth("naval-alarm", 1.05, lambda t, r: .42 * math.sin(tau * (630 if int(t * 6) % 2 else 470) * t))
    synth("ciws-burst", .34, lambda t, r: .52 * (r.random() * 2 - 1) * (1 if int(t * 58) % 2 == 0 else .18))
    synth("missile-launch", .72, lambda t, r: (.48 * (r.random() * 2 - 1) + .24 * math.sin(tau * (120 - 70 * t) * t)) * (1 - t / .72))
    synth("naval-impact", .55, lambda t, r: (.55 * (r.random() * 2 - 1) + .25 * math.sin(tau * 62 * t)) * math.exp(-5 * t))
    synth("flooding", 1.35, lambda t, r: .25 * (r.random() * 2 - 1) * (0.35 + .65 * abs(math.sin(tau * 3.2 * t))))
    synth("sinking", 2.10, lambda t, r: (.28 * math.sin(tau * (74 - 22 * t) * t) + .17 * (r.random() * 2 - 1)) * (1 - t / 2.1))
    synth("rearm", .44, lambda t, r: .35 * math.sin(tau * (520 + 240 * int(t * 12)) * t))
    synth("chaff", .58, lambda t, r: .42 * (r.random() * 2 - 1) * math.exp(-3.8 * t))
    synth("rescue", .72, lambda t, r: .32 * math.sin(tau * (510 + 190 * t) * t))


def atlas(images: dict[str, list[Image.Image]]) -> None:
    AUDIT_ROOT.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGB", (8 * 128, len(VESSELS) * 4 * 112), (18, 22, 28))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for row, (name, frames) in enumerate(images.items()):
        for facing in range(FACINGS):
            col, subrow = facing % 8, facing // 8
            x, y = col * 128, (row * 4 + subrow) * 112
            rgba = frames[facing].convert("RGBA")
            rgba = rgba.resize((96, 72), Image.Resampling.NEAREST)
            sheet.paste(rgba, (x + 16, y + 22), rgba)
            draw.text((x + 6, y + 6), f"{name} {facing:02d} yaw={facing * 32}/1024", fill=(224, 232, 240), font=font)
    sheet.save(AUDIT_ROOT / "all-facing-hulls.png")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--no-package", action="store_true", help="Only render source PNGs and audit sheets")
    args = parser.parse_args()
    BITS_ROOT.mkdir(parents=True, exist_ok=True)
    body_images = {}
    for name, spec in VESSELS.items():
        idle = [vessel_frame(spec, facing) for facing in range(FACINGS)]
        damaged = [vessel_frame(spec, facing, damage=1) for facing in range(FACINGS)]
        critical = [vessel_frame(spec, facing, damage=2) for facing in range(FACINGS)]
        sinking = [vessel_frame(spec, facing, damage=2, sink=frame) for facing in range(FACINGS) for frame in range(8)]
        body_images[name] = idle
        if not args.no_package:
            build_shp(name, idle + damaged + critical + sinking)
            build_shp(f"{name}_icon", [icon_for(name, spec)])
        if spec.get("turret") and not args.no_package:
            build_shp(f"{name}_turret", [turret_frame("large" if name == "sa_frgt" else "small", f) for f in range(FACINGS)])
        if spec.get("ciws") and not args.no_package:
            build_shp(f"{name}_ciws", [turret_frame("ciws", f) for f in range(FACINGS)])

    wake = [wake_frame(facing, frame) for facing in range(FACINGS) for frame in range(4)]
    effects = effect_frames()
    if not args.no_package:
        build_shp("naval_wake", wake)
        for name, frames in effects.items():
            build_shp(name, frames)
        build_shp("naval_survivor", [recovery_marker("survivor")])
        build_shp("naval_salvage", [recovery_marker("salvage")])
        build_audio()
    atlas(body_images)
    report = {
        "facings": FACINGS,
        "canvas": list(CANVAS),
        "ordering": "linear yaw; facing-major animation frames",
        "vessels": {name: {"body_frames": 352, "unique_idle_facings": len({im.tobytes() for im in body_images[name]})} for name in VESSELS},
        "fixed_canvas": all(all(im.size == CANVAS for im in frames) for frames in body_images.values()),
        "runtime_rotation": False,
    }
    (AUDIT_ROOT / "audit-report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Generated naval assets under {BITS_ROOT}")
    print(f"Audit report: {AUDIT_ROOT / 'audit-report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

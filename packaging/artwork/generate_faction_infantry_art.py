#!/usr/bin/env python3
"""Build role-readable custom infantry for China, Iran, Red Sea, and Turkey.

Every output uses the complete 713-frame Red Alert infantry contract, but no
actor is a palette-only copy.  The normalized Iran infantry are used as human
anatomy and motion references, then deterministic pixel equipment is authored
onto every reachable movement and combat state.  Headgear, carried equipment,
packs, antennas, armor, and weapon length are selected per actor so faction and
role remain readable at native zoom.

The generator also verifies that all generated action silhouettes differ from
each other and from the four Iran source actors.  This prevents a future
palette-swap regression from silently restoring duplicated soldiers.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw


TARGET_SIZE = (50, 39)
EXPECTED_FRAME_COUNT = 713
ACTION_FRAME_COUNT = 280
SOURCE_ACTORS = ("irbas", "iratgm", "irdc", "shadowone")


@dataclass(frozen=True)
class InfantryArtProfile:
	source_asset: str
	faction: str
	headgear: str
	pack: str
	weapon: str
	body: str = "standard"


# The source actor supplies role-correct anatomy and motion.  The remaining
# fields author the equipment that makes each actor recognizable in play.
ART_PROFILES = {
	"irbas": InfantryArtProfile("irbas", "iran", "headwrap", "ammo", "long-rifle", "shawl"),
	"iratgm": InfantryArtProfile("iratgm", "iran", "scarf", "battery", "atgm-tripod"),
	"irdc": InfantryArtProfile("irdc", "iran", "headset", "twin-antenna", "drone-tablet"),
	"shadowone": InfantryArtProfile("shadowone", "iran", "hood", "remote-charge", "suppressed-smg", "armor"),
	"cnrifle": InfantryArtProfile("irbas", "china", "helmet-visor", "small-radio", "bullpup", "armor"),
	"cnnetwork": InfantryArtProfile("irdc", "china", "visor", "antenna", "terminal"),
	"cnportable": InfantryArtProfile("iratgm", "china", "helmet", "battery", "twin-launcher", "armor"),
	"redspear": InfantryArtProfile("shadowone", "china", "officer-cap", "command", "precision-rifle"),
	"sang": InfantryArtProfile("irbas", "saudi", "heavy-helmet", "ammo", "burst-rifle", "heavy-armor"),
	"sajtac": InfantryArtProfile("irdc", "saudi", "headset", "large-radio", "designator"),
	"saat": InfantryArtProfile("iratgm", "saudi", "optic-helmet", "ammo", "atgm-tripod", "armor"),
	"falcon1": InfantryArtProfile("shadowone", "saudi", "field-cap", "command", "precision-rifle", "armor"),
	"ymr": InfantryArtProfile("irbas", "yemen", "headwrap", "satchel", "long-rifle", "shawl"),
	"yrpg": InfantryArtProfile("iratgm", "yemen", "scarf", "rocket-pack", "rpg", "shawl"),
	"yspot": InfantryArtProfile("irdc", "yemen", "headwrap", "twin-antenna", "drone-tablet", "shawl"),
	"wadighost": InfantryArtProfile("shadowone", "yemen", "hood", "remote-charge", "suppressed-smg", "shawl"),
	"trrifle": InfantryArtProfile("irbas", "turkey", "helmet", "large-radio", "compact-rifle", "armor"),
	"trat": InfantryArtProfile("iratgm", "turkey", "camo-hood", "satchel", "long-launcher", "shawl"),
	"trdroneop": InfantryArtProfile("irdc", "turkey", "headset", "twin-antenna", "drone-tablet"),
	"greywolf": InfantryArtProfile("shadowone", "turkey", "beret", "command", "command-carbine", "armor"),
}


FACTION_ACCENT_REMAPS = {
	"iran": {},
	"china": {154: 182, 155: 190},
	"turkey": {168: 205, 169: 206, 170: 207, 182: 205, 190: 206},
	"saudi": {168: 154, 169: 155, 170: 155, 182: 160, 190: 154},
	"yemen": {168: 205, 169: 207, 170: 155, 182: 154, 190: 155},
}


# Outline, equipment shadow, equipment highlight, and screen/optic highlight.
FACTION_DRAW_INDEXES = {
	"iran": (12, 155, 154, 224),
	"china": (12, 190, 182, 96),
	"turkey": (12, 207, 205, 160),
	"saudi": (12, 155, 154, 160),
	"yemen": (12, 155, 205, 224),
}


# These palette indexes are absent from the source art.  One marker is written
# inside an opaque body pixel on authored action frames so regenerating Iran's
# self-sourced sheets is idempotent instead of adding another equipment layer.
IRAN_PROFILE_MARKERS = {
	"irbas": 230,
	"iratgm": 231,
	"irdc": 232,
	"shadowone": 233,
}


CUSTOM_ICON_BASES = {
	"irbasicon": "e1icon",
	"iratgmicon": "e3icon",
	"irdcicon": "e6icon",
	"shadowoneicon": "spyicon",
	"sangicon": "e1icon",
	"sajtacicon": "e6icon",
	"saaticon": "e3icon",
	"falcon1icon": "spyicon",
	"ymricon": "e1icon",
	"yrpgicon": "e3icon",
	"yspoticon": "e6icon",
	"wadighosticon": "spyicon",
	"trrifleicon": "e1icon",
	"traticon": "e3icon",
	"trdroneopicon": "e6icon",
	"greywolficon": "spyicon",
}


ICON_BADGES = {
	"irbasicon": ("burst", 224),
	"iratgmicon": ("tracked", 224),
	"irdcicon": ("antenna", 224),
	"shadowoneicon": ("demolition", 224),
	"sangicon": ("shield", 160),
	"sajtacicon": ("designator", 160),
	"saaticon": ("launcher", 160),
	"falcon1icon": ("airstrike", 160),
	"ymricon": ("ambush", 205),
	"yrpgicon": ("rocket", 205),
	"yspoticon": ("drone", 224),
	"wadighosticon": ("remote", 224),
	"trrifleicon": ("mechanized", 205),
	"traticon": ("launcher", 205),
	"trdroneopicon": ("drone", 205),
	"greywolficon": ("command", 205),
}


# (start, frames per facing).  Idle frames are authored facing south; death
# frames retain the source animation so body motion stays natural.
DIRECTIONAL_BLOCKS = (
	(0, 1),
	(8, 1),
	(16, 6),
	(64, 8),
	(128, 1),
	(136, 4),
	(168, 2),
	(184, 2),
	(200, 8),
)


def parse_args() -> argparse.Namespace:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument(
		"--engine-root",
		type=Path,
		default=Path(__file__).resolve().parents[2],
		help="OpenRA checkout root (defaults to this script's checkout).",
	)
	parser.add_argument(
		"--palette",
		type=Path,
		help="Indexed RA palette (defaults to the Chernobyl temperate palette).",
	)
	return parser.parse_args()


def run_utility(utility: Path, engine_root: Path, cwd: Path, *args: str) -> None:
	environment = os.environ.copy()
	environment["ENGINE_DIR"] = str(engine_root)
	environment.setdefault("DOTNET_ROLL_FORWARD", "Major")
	result = subprocess.run(
		[str(utility), "ra", *args],
		cwd=cwd,
		env=environment,
		text=True,
		capture_output=True,
	)
	if result.returncode:
		detail = "\n".join(part for part in (result.stdout.strip(), result.stderr.strip()) if part)
		raise RuntimeError(f"OpenRA.Utility failed for {' '.join(args)}\n{detail}")


def extract_pngs(
	utility: Path,
	engine_root: Path,
	palette: Path,
	asset: str,
	destination: Path,
) -> list[Path]:
	destination.mkdir(parents=True, exist_ok=True)
	run_utility(utility, engine_root, destination, "--extract", f"{asset}.shp")
	run_utility(utility, engine_root, destination, "--png", f"./{asset}.shp", str(palette))
	paths = sorted(destination.glob(f"{asset}-*.png"))
	if not paths:
		raise RuntimeError(f"No PNG frames were extracted for {asset}.")
	return paths


def preserve_indexed_metadata(source: Image.Image, output: Image.Image) -> None:
	palette = source.getpalette()
	if palette is not None:
		output.putpalette(palette)
	if "transparency" in source.info:
		output.info["transparency"] = source.info["transparency"]


def save_indexed(image: Image.Image, path: Path) -> None:
	save_args = {}
	if "transparency" in image.info:
		save_args["transparency"] = image.info["transparency"]
	image.save(path, **save_args)


def facing_for_frame(frame_index: int) -> int | None:
	for start, length in DIRECTIONAL_BLOCKS:
		if start <= frame_index < start + length * 8:
			return (frame_index - start) // length
	if 264 <= frame_index < ACTION_FRAME_COUNT or frame_index == 712:
		return 0
	return None


def solid_points(image: Image.Image) -> list[tuple[int, int]]:
	alpha = image.convert("RGBA").getchannel("A")
	return [
		(x, y)
		for y in range(image.height)
		for x in range(image.width)
		if alpha.getpixel((x, y)) >= 220
	]


def clamp(value: int, lower: int, upper: int) -> int:
	return max(lower, min(upper, value))


def set_pixel(draw: ImageDraw.ImageDraw, x: int, y: int, color: int) -> None:
	if 0 <= x < TARGET_SIZE[0] and 0 <= y < TARGET_SIZE[1]:
		draw.point((x, y), fill=color)


def draw_line(
	draw: ImageDraw.ImageDraw,
	start: tuple[int, int],
	end: tuple[int, int],
	color: int,
) -> None:
	x1, y1 = start
	x2, y2 = end
	draw.line(
		(clamp(x1, 0, TARGET_SIZE[0] - 1), clamp(y1, 0, TARGET_SIZE[1] - 1),
		 clamp(x2, 0, TARGET_SIZE[0] - 1), clamp(y2, 0, TARGET_SIZE[1] - 1)),
		fill=color,
		width=1,
	)


def row_span(points: list[tuple[int, int]], y: int, fallback: tuple[int, int]) -> tuple[int, int]:
	xs = [x for x, py in points if abs(py - y) <= 1]
	return (min(xs), max(xs)) if xs else fallback


def draw_headgear(
	draw: ImageDraw.ImageDraw,
	profile: InfantryArtProfile,
	cx: int,
	top: int,
	front_side: int,
	colors: tuple[int, int, int, int],
) -> None:
	outline, dark, light, screen = colors
	style = profile.headgear
	if style in ("helmet", "heavy-helmet", "optic-helmet", "helmet-visor"):
		draw_line(draw, (cx - 2, top - 1), (cx + 2, top - 1), outline)
		draw_line(draw, (cx - 1, top - 1), (cx + 1, top - 1), light)
		set_pixel(draw, cx + 2 * front_side, top, dark)
		if style == "heavy-helmet":
			set_pixel(draw, cx - 2, top, dark)
			set_pixel(draw, cx + 2, top, dark)
		elif style == "optic-helmet":
			set_pixel(draw, cx + 2 * front_side, top + 1, screen)
		elif style == "helmet-visor":
			draw_line(draw, (cx - 1, top + 1), (cx + 1, top + 1), screen)
	elif style in ("officer-cap", "field-cap"):
		draw_line(draw, (cx - 1, top - 1), (cx + 1, top - 1), light)
		set_pixel(draw, cx + 2 * front_side, top - 1, outline)
		if style == "officer-cap":
			set_pixel(draw, cx, top - 2, dark)
	elif style == "beret":
		draw_line(draw, (cx - 2, top - 1), (cx + 1, top - 1), dark)
		set_pixel(draw, cx - 2 * front_side, top, light)
	elif style in ("hood", "camo-hood"):
		set_pixel(draw, cx - 2, top + 1, outline)
		set_pixel(draw, cx + 2, top + 1, outline)
		set_pixel(draw, cx, top - 1, dark)
		if style == "camo-hood":
			set_pixel(draw, cx - front_side, top + 2, light)
	elif style in ("headwrap", "scarf"):
		draw_line(draw, (cx - 1, top - 1), (cx + 1, top - 1), light)
		set_pixel(draw, cx - 2, top + 2, dark)
		set_pixel(draw, cx + 2, top + 3, dark)
	elif style == "headset":
		set_pixel(draw, cx - 2 * front_side, top + 1, outline)
		set_pixel(draw, cx - 2 * front_side, top + 2, screen)
		draw_line(draw, (cx - 2 * front_side, top + 2), (cx - 3 * front_side, top + 3), dark)
	elif style == "visor":
		draw_line(draw, (cx - 1, top + 1), (cx + 1, top + 1), screen)
		set_pixel(draw, cx - 2 * front_side, top + 2, dark)


def draw_body_profile(
	draw: ImageDraw.ImageDraw,
	profile: InfantryArtProfile,
	points: list[tuple[int, int]],
	cx: int,
	top: int,
	bottom: int,
	colors: tuple[int, int, int, int],
) -> None:
	outline, dark, light, _ = colors
	height = bottom - top
	if height < 8:
		return
	shoulder_y = top + max(4, height // 3)
	left, right = row_span(points, shoulder_y, (cx - 2, cx + 2))
	if profile.body in ("armor", "heavy-armor"):
		set_pixel(draw, left - 1, shoulder_y, outline)
		set_pixel(draw, right + 1, shoulder_y, outline)
		set_pixel(draw, left, shoulder_y, light)
		set_pixel(draw, right, shoulder_y, light)
	if profile.body == "heavy-armor":
		set_pixel(draw, left - 1, shoulder_y + 1, dark)
		set_pixel(draw, right + 1, shoulder_y + 1, dark)
	if profile.body == "shawl":
		for y in range(shoulder_y, min(bottom - 2, shoulder_y + 4)):
			row_left, row_right = row_span(points, y, (cx - 2, cx + 2))
			set_pixel(draw, row_left - 1, y, dark)
			if (y - shoulder_y) % 2 == 0:
				set_pixel(draw, row_right + 1, y, light)


def draw_pack(
	draw: ImageDraw.ImageDraw,
	profile: InfantryArtProfile,
	points: list[tuple[int, int]],
	cx: int,
	top: int,
	bottom: int,
	rear_side: int,
	colors: tuple[int, int, int, int],
) -> None:
	outline, dark, light, screen = colors
	height = bottom - top
	if height < 5:
		return
	pack_y = top + max(4, height // 3)
	left, right = row_span(points, pack_y, (cx - 2, cx + 2))
	anchor = left - 1 if rear_side < 0 else right + 1
	inside = anchor + rear_side
	style = profile.pack
	if style in ("small-radio", "antenna", "large-radio", "twin-antenna", "command"):
		draw_line(draw, (anchor, pack_y), (anchor, pack_y + 2), outline)
		set_pixel(draw, inside, pack_y + 1, dark)
		set_pixel(draw, anchor, pack_y + 1, light)
		antenna_height = 2 if style == "small-radio" else 4
		draw_line(draw, (anchor, pack_y - 1), (anchor, pack_y - antenna_height), outline)
		if style in ("twin-antenna", "large-radio"):
			second = anchor - rear_side
			draw_line(draw, (second, pack_y - 1), (second, pack_y - antenna_height + 1), dark)
		if style == "command":
			set_pixel(draw, anchor, pack_y, screen)
	elif style == "battery":
		draw_line(draw, (anchor, pack_y - 1), (anchor, pack_y + 3), outline)
		draw_line(draw, (inside, pack_y), (inside, pack_y + 2), dark)
		set_pixel(draw, anchor, pack_y, screen)
	elif style in ("satchel", "ammo", "rocket-pack", "remote-charge"):
		low_y = min(bottom - 3, pack_y + 2)
		draw.rectangle((min(anchor, inside), low_y, max(anchor, inside), low_y + 1), fill=outline)
		set_pixel(draw, anchor, low_y, light)
		if style == "ammo":
			set_pixel(draw, inside, low_y + 2, light)
		elif style == "rocket-pack":
			draw_line(draw, (anchor, low_y - 1), (anchor + 2 * rear_side, low_y + 1), dark)
		elif style == "remote-charge":
			set_pixel(draw, inside, low_y, screen)
			draw_line(draw, (anchor, low_y - 1), (anchor + rear_side, low_y - 2), dark)


def draw_weapon_signature(
	draw: ImageDraw.ImageDraw,
	profile: InfantryArtProfile,
	cx: int,
	top: int,
	bottom: int,
	facing: int,
	front_side: int,
	colors: tuple[int, int, int, int],
) -> None:
	outline, dark, light, screen = colors
	height = bottom - top
	weapon_y = top + (max(3, height // 2) if height >= 7 else max(1, height // 2))
	style = profile.weapon
	lengths = {
		"bullpup": 2,
		"burst-rifle": 3,
		"long-rifle": 4,
		"compact-rifle": 1,
		"precision-rifle": 5,
		"command-carbine": 3,
		"suppressed-smg": 3,
		"designator": 4,
		"terminal": 1,
		"drone-tablet": 1,
		"twin-launcher": 4,
		"long-launcher": 5,
		"atgm-tripod": 5,
		"rpg": 5,
	}
	length = lengths[style]
	# OpenRA infantry facings progress south, south-west, west, north-west,
	# north, north-east, east, south-east.  Project the equipment along that
	# vector so a launcher never becomes the same horizontal bar in every pose.
	direction_x, direction_y = (
		(0, 1), (-1, 1), (-1, 0), (-1, -1),
		(0, -1), (1, -1), (1, 0), (1, 1),
	)[facing]
	start_x = cx + direction_x * 2
	start_y = weapon_y + direction_y
	end_x = start_x + direction_x * length
	end_y = start_y + direction_y * length
	draw_line(draw, (start_x, start_y), (end_x, end_y), outline)
	if length >= 3:
		perpendicular_x = -direction_y
		perpendicular_y = direction_x
		draw_line(
			draw,
			(start_x + perpendicular_x, start_y + perpendicular_y),
			(end_x - direction_x + perpendicular_x, end_y - direction_y + perpendicular_y),
			dark,
		)
	set_pixel(draw, start_x, start_y, light)

	if style in ("terminal", "drone-tablet"):
		tablet_x = cx + 2 * front_side
		draw.rectangle((min(tablet_x, tablet_x + front_side), weapon_y - 1,
			max(tablet_x, tablet_x + front_side), weapon_y), fill=dark)
		set_pixel(draw, tablet_x, weapon_y - 1, screen)
		if style == "drone-tablet":
			set_pixel(draw, tablet_x + front_side, weapon_y, screen)
	elif style == "designator":
		set_pixel(draw, end_x, end_y, screen)
		set_pixel(draw, end_x - direction_x, end_y - direction_y, light)
	elif style == "twin-launcher":
		perpendicular_x = -direction_y
		perpendicular_y = direction_x
		draw_line(draw, (start_x + perpendicular_x, start_y + perpendicular_y),
			(end_x + perpendicular_x, end_y + perpendicular_y), outline)
		set_pixel(draw, end_x - perpendicular_x, end_y - perpendicular_y, screen)
	elif style == "atgm-tripod" and height >= 7:
		draw_line(draw, (start_x, start_y + 1),
			(start_x - direction_x + front_side, min(bottom, start_y + 4)), dark)
		set_pixel(draw, end_x - direction_x, end_y - direction_y - 1, screen)
	elif style == "rpg":
		set_pixel(draw, end_x - direction_y, end_y + direction_x, light)
		set_pixel(draw, end_x + direction_y, end_y - direction_x, light)
	elif style == "precision-rifle":
		set_pixel(draw, start_x + 2 * direction_x - direction_y,
			start_y + 2 * direction_y + direction_x, screen)
	elif style == "suppressed-smg":
		set_pixel(draw, end_x, end_y, dark)


def author_frame(
	path: Path,
	frame_index: int,
	profile: InfantryArtProfile,
	marker: int | None,
) -> bool:
	with Image.open(path) as source_image:
		source = source_image.copy()
		if source.mode != "P" or source.size != TARGET_SIZE:
			raise RuntimeError(f"Unexpected detailed source frame: {path}")
		if marker is not None and marker in source.tobytes():
			return False
		facing = facing_for_frame(frame_index)
		if marker is not None and facing is None:
			return False

		translation = bytes(FACTION_ACCENT_REMAPS[profile.faction].get(index, index) for index in range(256))
		output = Image.frombytes("P", source.size, source.tobytes().translate(translation))
		preserve_indexed_metadata(source, output)
		if facing is not None:
			points = solid_points(output)
			if not points:
				raise RuntimeError(f"Empty action frame: {path}")
			left = min(x for x, _ in points)
			right = max(x for x, _ in points)
			top = min(y for _, y in points)
			bottom = max(y for _, y in points)
			height = bottom - top
			head_band = [(x, y) for x, y in points if y <= top + max(2, height // 5)]
			cx = round(sum(x for x, _ in head_band) / len(head_band)) if head_band else (left + right) // 2
			front_side = 1 if facing in (0, 1, 2, 7) else -1
			rear_side = -front_side
			draw = ImageDraw.Draw(output)
			colors = FACTION_DRAW_INDEXES[profile.faction]
			if height >= 7:
				draw_headgear(draw, profile, cx, top, front_side, colors)
			draw_body_profile(draw, profile, points, cx, top, bottom, colors)
			draw_pack(draw, profile, points, cx, top, bottom, rear_side, colors)
			draw_weapon_signature(draw, profile, cx, top, bottom, facing, front_side, colors)
			if marker is not None:
				marker_point = min(points, key=lambda point: abs(point[0] - cx) + abs(point[1] - (top + height // 2)))
				set_pixel(draw, marker_point[0], marker_point[1], marker)
		save_indexed(output, path)
	return True


def mask_digest(paths: list[Path]) -> str:
	digest = hashlib.sha256()
	for path in paths[:ACTION_FRAME_COUNT]:
		with Image.open(path) as frame:
			digest.update(frame.convert("RGBA").getchannel("A").tobytes())
	return digest.hexdigest()


def alpha_difference(before: bytes, after: bytes) -> int:
	return sum(left != right for left, right in zip(before, after))


def rebuild_detailed_frames(
	paths: list[Path],
	profile: InfantryArtProfile,
	marker: int | None,
) -> int:
	if len(paths) != EXPECTED_FRAME_COUNT:
		raise RuntimeError(f"Expected {EXPECTED_FRAME_COUNT} detailed frames, found {len(paths)}.")

	changed_alpha_pixels = 0
	authored_frames = 0
	for frame_index, path in enumerate(paths):
		with Image.open(path) as source:
			before = source.convert("RGBA").getchannel("A").tobytes()
		if not author_frame(path, frame_index, profile, marker):
			continue
		authored_frames += 1
		with Image.open(path) as authored:
			after = authored.convert("RGBA").getchannel("A").tobytes()
		changed_alpha_pixels += alpha_difference(before, after)

	if authored_frames and changed_alpha_pixels < 200:
		raise RuntimeError(
			f"{profile.faction} {profile.source_asset} changed only {changed_alpha_pixels} silhouette pixels."
		)

	for path in paths:
		with Image.open(path) as frame:
			bounds = frame.convert("RGBA").getchannel("A").getbbox()
			if bounds is None:
				raise RuntimeError(f"Empty authored frame: {path}")
			if bounds[0] <= 0 or bounds[1] <= 0 or bounds[2] >= TARGET_SIZE[0] or bounds[3] >= TARGET_SIZE[1]:
				raise RuntimeError(f"Authored frame clips the fixed canvas: {path} {bounds}")

	return changed_alpha_pixels


def draw_icon_badge(image: Image.Image, icon: str) -> None:
	style, color = ICON_BADGES[icon]
	draw = ImageDraw.Draw(image)
	draw.rectangle((45, 2, 62, 17), fill=12)
	dark = 155
	light = color
	if style == "burst":
		for offset in (0, 3, 6):
			draw.line((48 + offset, 13, 53 + offset, 7), fill=light, width=1)
	elif style == "tracked":
		draw.line((48, 13, 59, 5), fill=light, width=2)
		draw.line((50, 15, 60, 15), fill=dark, width=1)
	elif style == "antenna":
		draw.rectangle((49, 9, 57, 15), fill=dark)
		draw.line((51, 9, 48, 3), fill=light, width=1)
		draw.line((55, 9, 59, 2), fill=light, width=1)
		draw.point((48, 3), fill=160)
		draw.point((59, 2), fill=160)
	elif style in ("demolition", "remote"):
		draw.rectangle((49, 8, 58, 15), fill=dark)
		draw.rectangle((51, 10, 56, 13), fill=light)
		draw.line((58, 9, 61, 5), fill=light, width=1)
		if style == "remote":
			draw.point((61, 4), fill=160)
	elif style == "shield":
		draw.polygon(((53, 4), (60, 7), (58, 14), (53, 17), (48, 14), (47, 7)), fill=dark)
		draw.line((53, 6, 53, 14), fill=light, width=1)
	elif style == "designator":
		draw.ellipse((48, 4, 60, 16), outline=light, width=1)
		draw.line((54, 2, 54, 17), fill=light, width=1)
		draw.line((46, 10, 62, 10), fill=light, width=1)
	elif style == "launcher":
		draw.line((48, 14, 60, 5), fill=dark, width=3)
		draw.line((48, 13, 59, 4), fill=light, width=1)
	elif style == "airstrike":
		draw.polygon(((47, 11), (53, 9), (58, 3), (59, 9), (62, 11), (58, 12), (56, 16), (53, 12)), fill=light)
	elif style == "ambush":
		draw.polygon(((47, 14), (51, 5), (56, 3), (61, 14), (57, 11), (53, 16)), fill=dark)
		draw.line((49, 14, 59, 7), fill=light, width=1)
	elif style == "rocket":
		draw.line((47, 14, 58, 5), fill=dark, width=3)
		draw.polygon(((58, 3), (62, 5), (58, 8)), fill=light)
	elif style == "drone":
		draw.rectangle((52, 8, 57, 12), fill=light)
		draw.line((48, 5, 61, 15), fill=dark, width=1)
		draw.line((48, 15, 61, 5), fill=dark, width=1)
		for point in ((48, 5), (61, 5), (48, 15), (61, 15)):
			draw.ellipse((point[0] - 1, point[1] - 1, point[0] + 1, point[1] + 1), outline=light)
	elif style == "mechanized":
		draw.rectangle((47, 8, 58, 14), fill=dark)
		draw.ellipse((48, 12, 52, 16), outline=light)
		draw.ellipse((54, 12, 58, 16), outline=light)
		draw.line((57, 8, 61, 5), fill=light, width=1)
	elif style == "command":
		draw.polygon(((54, 3), (56, 8), (62, 8), (57, 11), (59, 16), (54, 13), (49, 16), (51, 11), (46, 8), (52, 8)), fill=light)


def compose_icon(custom_path: Path, native_path: Path, output_path: Path, icon: str) -> None:
	with Image.open(custom_path) as custom_source, Image.open(native_path) as native_source:
		custom = custom_source.copy()
		native = native_source.copy()
		if custom.mode != "P" or native.mode != "P" or custom.size != (64, 48) or native.size != (64, 48):
			raise RuntimeError(f"Unexpected icon format for {icon}.")

		output = native.copy()
		output.paste(custom.crop((0, 35, 64, 48)), (0, 35))
		draw_icon_badge(output, icon)
		preserve_indexed_metadata(native_source, output)
		save_indexed(output, output_path)


def pack_shp(utility: Path, engine_root: Path, directory: Path, prefix: str) -> Path:
	run_utility(utility, engine_root, directory, "--shp", f"{prefix}-*.png")
	result = directory / f"{prefix}.shp"
	if not result.is_file():
		raise RuntimeError(f"SHP packing produced no {result}.")
	return result


def main() -> int:
	args = parse_args()
	engine_root = args.engine_root.resolve()
	utility = engine_root / "bin" / "OpenRA.Utility.exe"
	bits = engine_root / "mods" / "ra" / "bits"
	palette = (args.palette or engine_root / "mods" / "ra" / "maps" / "chernobyl" / "temperat.pal").resolve()
	if not utility.is_file() or not palette.is_file() or not bits.is_dir():
		raise RuntimeError("OpenRA.Utility, RA palette, or RA bits directory was not found.")

	rebuilt: list[str] = []
	silhouette_changes: dict[str, int] = {}
	mask_digests: dict[str, str] = {}
	with tempfile.TemporaryDirectory(prefix="openra-faction-infantry-") as temporary:
		workspace = Path(temporary)

		for source_actor in SOURCE_ACTORS:
			reference_paths = extract_pngs(
				utility, engine_root, palette, source_actor, workspace / "references" / source_actor
			)
			mask_digests[source_actor] = mask_digest(reference_paths)

		for unit, profile in ART_PROFILES.items():
			unit_directory = workspace / unit
			frames = extract_pngs(utility, engine_root, palette, profile.source_asset, unit_directory)
			silhouette_changes[unit] = rebuild_detailed_frames(
				frames, profile, IRAN_PROFILE_MARKERS.get(unit)
			)
			mask_digests[unit] = mask_digest(frames)
			source_shp = pack_shp(utility, engine_root, unit_directory, profile.source_asset)
			shutil.copy2(source_shp, bits / f"{unit}.shp")
			rebuilt.append(unit)

		duplicates: list[str] = []
		items = list(mask_digests.items())
		for index, (left_name, left_digest) in enumerate(items):
			for right_name, right_digest in items[index + 1:]:
				if left_digest == right_digest:
					duplicates.append(f"{left_name}/{right_name}")
		if duplicates:
			raise RuntimeError(f"Duplicate action silhouettes remain: {', '.join(duplicates)}")

		for icon, native_icon in CUSTOM_ICON_BASES.items():
			icon_directory = workspace / icon
			custom_path = extract_pngs(utility, engine_root, palette, icon, icon_directory)[0]
			native_path = extract_pngs(utility, engine_root, palette, native_icon, icon_directory)[0]
			final_png = icon_directory / f"{icon}-0000.png"
			compose_icon(custom_path, native_path, final_png, icon)
			shutil.copy2(pack_shp(utility, engine_root, icon_directory, icon), bits / f"{icon}.shp")

	print(f"Rebuilt {len(rebuilt)} unique 713-frame infantry sheets: {', '.join(rebuilt)}.")
	print("Silhouette pixels changed: " + ", ".join(f"{unit}={count}" for unit, count in silhouette_changes.items()))
	print("Verified unique action silhouettes across all 20 custom infantry actors.")
	print("Rebuilt 16 role-badged production icons over role-matched native portrait bases.")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())

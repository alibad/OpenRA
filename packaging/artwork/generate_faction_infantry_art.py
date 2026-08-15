#!/usr/bin/env python3
"""Rebuild custom-faction infantry against the native Red Alert art contract.

China and Turkey were originally authored as tiny low-detail silhouettes.
Scaling those sheets corrected their footprint but magnified the missing human
anatomy. Their world art is rebuilt from detailed, role-matched custom infantry
bases with 713 frames per actor: eight facings for every directional action,
complete prone transitions, facing-specific deaths, and a parachute frame.

The Red Sea troops already have distinct articulated art, so this tool only
normalizes their low ground pivot onto the native 50x39 infantry canvas.

Turkey's original production portraits were also tiny world sprites placed on
dark cards. They are rebuilt over role-matched native portrait bases while
retaining the custom unit labels.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image


TARGET_SIZE = (50, 39)


@dataclass(frozen=True)
class NormalizationProfile:
	width: int
	height: int
	offset_x: int
	offset_y: int


@dataclass(frozen=True)
class DetailedSourceProfile:
	source_asset: str
	faction: str


# These custom Iran sheets are the project's complete articulated infantry
# family. Each role has a readable human silhouette and appropriate equipment,
# and each sheet supplies the same audited 713-frame action contract. China and
# Turkey receive separate fixed-color accent treatments while preserving the
# player-remap ramp (palette indices 80..95).
DETAILED_SOURCE_PROFILES = {
	"cnrifle": DetailedSourceProfile("irbas", "china"),
	"cnnetwork": DetailedSourceProfile("irdc", "china"),
	"cnportable": DetailedSourceProfile("iratgm", "china"),
	"redspear": DetailedSourceProfile("shadowone", "china"),
	"trrifle": DetailedSourceProfile("irbas", "turkey"),
	"trat": DetailedSourceProfile("iratgm", "turkey"),
	"trdroneop": DetailedSourceProfile("irdc", "turkey"),
	"greywolf": DetailedSourceProfile("shadowone", "turkey"),
}

FACTION_ACCENT_REMAPS = {
	# Replace fixed olive equipment shadows with dark blue-green technology
	# accents. Player-color uniform pixels are deliberately not touched.
	"china": {
		154: 182,
		155: 190,
	},
	# Replace fixed blue/cyan equipment highlights with Turkish red accents.
	# The dark olive webbing remains intact and the uniform still remaps.
	"turkey": {
		168: 205,
		169: 206,
		170: 207,
		182: 205,
		190: 206,
	},
}

EXPECTED_DETAILED_FRAME_COUNT = 713


# The Red Sea sprites are already detailed, but their baseline was five pixels
# too low. A vertical-only normalization retains their distinctive equipment.
WORLD_PROFILES = {
	"sang": NormalizationProfile(50, 32, 0, 0),
	"sajtac": NormalizationProfile(50, 32, 0, 0),
	"saat": NormalizationProfile(50, 32, 0, 0),
	"falcon1": NormalizationProfile(50, 32, 0, 0),
	"ymr": NormalizationProfile(50, 32, 0, 0),
	"yrpg": NormalizationProfile(50, 32, 0, 0),
	"yspot": NormalizationProfile(50, 32, 0, 0),
	"wadighost": NormalizationProfile(50, 32, 0, 0),
}

TURKEY_ICON_BASES = {
	"trrifleicon": "e1icon",
	"traticon": "e3icon",
	"trdroneopicon": "e6icon",
	"greywolficon": "spyicon",
}


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


def stand_bounds(paths: list[Path]) -> list[tuple[int, int, int, int]]:
	bounds: list[tuple[int, int, int, int]] = []
	for path in paths[:8]:
		with Image.open(path) as frame:
			frame_bounds = frame.convert("RGBA").getchannel("A").getbbox()
			if frame_bounds is None:
				raise RuntimeError(f"Empty stand frame: {path}")
			bounds.append(frame_bounds)
	return bounds


def already_normalized(paths: list[Path]) -> bool:
	with Image.open(paths[0]) as frame:
		if frame.size != TARGET_SIZE:
			return False

	bounds = stand_bounds(paths)
	return min(top for _, top, _, _ in bounds) <= 6 and max(bottom for _, _, _, bottom in bounds) <= 23


def normalize_frame(path: Path, profile: NormalizationProfile) -> None:
	with Image.open(path) as source_image:
		source = source_image.copy()
		if source.mode != "P":
			raise RuntimeError(f"Expected an indexed PNG: {path}")

		scaled = source.resize((profile.width, profile.height), Image.Resampling.NEAREST)

		if scaled.size != (profile.width, profile.height):
			raise RuntimeError(f"Unexpected scaled dimensions for {path}: {scaled.size}")

		output = Image.new("P", TARGET_SIZE, 0)
		preserve_indexed_metadata(source, output)
		output.paste(scaled, (profile.offset_x, profile.offset_y))
		save_indexed(output, path)


def normalize_frames(paths: list[Path], profile: NormalizationProfile) -> bool:
	if already_normalized(paths):
		return False
	for path in paths:
		normalize_frame(path, profile)
	return True


def remap_palette_indexes(path: Path, replacements: dict[int, int]) -> None:
	with Image.open(path) as source_image:
		source = source_image.copy()
		if source.mode != "P" or source.size != TARGET_SIZE:
			raise RuntimeError(f"Unexpected detailed source frame: {path}")

		translation = bytes(replacements.get(index, index) for index in range(256))
		output = Image.frombytes("P", source.size, source.tobytes().translate(translation))
		preserve_indexed_metadata(source, output)
		save_indexed(output, path)


def rebuild_detailed_frames(paths: list[Path], faction: str) -> None:
	if len(paths) != EXPECTED_DETAILED_FRAME_COUNT:
		raise RuntimeError(
			f"Expected {EXPECTED_DETAILED_FRAME_COUNT} detailed frames, found {len(paths)}."
		)

	replacements = FACTION_ACCENT_REMAPS[faction]
	for path in paths:
		remap_palette_indexes(path, replacements)

	bounds = stand_bounds(paths)
	if min(top for _, top, _, _ in bounds) > 6 or max(bottom for _, _, _, bottom in bounds) > 23:
		raise RuntimeError(f"Detailed {faction} infantry does not match the native ground contract.")


def compose_icon(custom_path: Path, native_path: Path, output_path: Path, icon: str) -> None:
	with Image.open(custom_path) as custom_source, Image.open(native_path) as native_source:
		custom = custom_source.copy()
		native = native_source.copy()
		if custom.mode != "P" or native.mode != "P" or custom.size != (64, 48) or native.size != (64, 48):
			raise RuntimeError(f"Unexpected icon format for {icon}.")

		# The native portrait restores the established painted RA treatment. The
		# authored bottom strip keeps each Turkish unit's correct custom label.
		output = native.copy()
		output.paste(custom.crop((0, 35, 64, 48)), (0, 35))
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

	normalized: list[str] = []
	rebuilt: list[str] = []
	with tempfile.TemporaryDirectory(prefix="openra-faction-infantry-") as temporary:
		workspace = Path(temporary)

		for unit, profile in DETAILED_SOURCE_PROFILES.items():
			unit_directory = workspace / unit
			frames = extract_pngs(utility, engine_root, palette, profile.source_asset, unit_directory)
			rebuild_detailed_frames(frames, profile.faction)
			source_shp = pack_shp(utility, engine_root, unit_directory, profile.source_asset)
			shutil.copy2(source_shp, bits / f"{unit}.shp")
			rebuilt.append(unit)

		for unit, profile in WORLD_PROFILES.items():
			unit_directory = workspace / unit
			frames = extract_pngs(utility, engine_root, palette, unit, unit_directory)
			if normalize_frames(frames, profile):
				normalized.append(unit)
			shutil.copy2(pack_shp(utility, engine_root, unit_directory, unit), bits / f"{unit}.shp")

		for icon, native_icon in TURKEY_ICON_BASES.items():
			icon_directory = workspace / icon
			custom_path = extract_pngs(utility, engine_root, palette, icon, icon_directory)[0]
			native_path = extract_pngs(utility, engine_root, palette, native_icon, icon_directory)[0]
			final_png = icon_directory / f"{icon}-0000.png"
			compose_icon(custom_path, native_path, final_png, icon)
			shutil.copy2(pack_shp(utility, engine_root, icon_directory, icon), bits / f"{icon}.shp")

	print(f"Rebuilt {len(rebuilt)} detailed 713-frame infantry sheets: {', '.join(rebuilt)}.")
	print(f"Normalized {len(normalized)} Red Sea sheets: {', '.join(normalized) or 'already current'}.")
	print("Rebuilt four Turkish production icons over role-matched native portrait bases.")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())

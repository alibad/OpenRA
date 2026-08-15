#!/usr/bin/env python3
"""Normalize custom-faction infantry against the native Red Alert art contract.

The faction infantry were authored on three different sprite canvases. China
and Turkey were exported too small for the game camera, while the Red Sea
troops stood below the native ground pivot. This tool preserves their original
uniforms, equipment, player-color indices, and full animation sets while
normalizing them onto the native 50x39 infantry canvas.

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
	use_scale2x: bool = False


# China was authored at roughly half native height. Scale2x preserves hard
# palette edges while reconstructing diagonals more cleanly than a raw 2x
# nearest-neighbour resize. Turkey needs a smaller proportional correction.
# The Red Sea sprites are already detailed, but their baseline is five pixels
# too low; a vertical-only normalization retains their distinctive equipment.
WORLD_PROFILES = {
	"cnrifle": NormalizationProfile(48, 48, 1, -16, True),
	"cnnetwork": NormalizationProfile(48, 48, 1, -16, True),
	"cnportable": NormalizationProfile(48, 48, 1, -16, True),
	"redspear": NormalizationProfile(48, 48, 1, -16, True),
	"sang": NormalizationProfile(50, 32, 0, 0),
	"sajtac": NormalizationProfile(50, 32, 0, 0),
	"saat": NormalizationProfile(50, 32, 0, 0),
	"falcon1": NormalizationProfile(50, 32, 0, 0),
	"ymr": NormalizationProfile(50, 32, 0, 0),
	"yrpg": NormalizationProfile(50, 32, 0, 0),
	"yspot": NormalizationProfile(50, 32, 0, 0),
	"wadighost": NormalizationProfile(50, 32, 0, 0),
	"trrifle": NormalizationProfile(30, 30, 10, -4),
	"trat": NormalizationProfile(30, 30, 10, -4),
	"trdroneop": NormalizationProfile(30, 30, 10, -4),
	"greywolf": NormalizationProfile(30, 30, 10, -4),
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


def scale2x(source: Image.Image) -> Image.Image:
	"""Apply the Scale2x pixel-art transform without changing palette indices."""
	if source.mode != "P":
		raise RuntimeError("Scale2x requires an indexed image.")

	width, height = source.size
	output = Image.new("P", (width * 2, height * 2), 0)
	preserve_indexed_metadata(source, output)
	source_pixels = source.load()
	output_pixels = output.load()

	for y in range(height):
		for x in range(width):
			center = source_pixels[x, y]
			up = source_pixels[x, y - 1] if y else center
			left = source_pixels[x - 1, y] if x else center
			right = source_pixels[x + 1, y] if x + 1 < width else center
			down = source_pixels[x, y + 1] if y + 1 < height else center

			top_left = left if left == up and left != down and up != right else center
			top_right = right if up == right and up != left and right != down else center
			bottom_left = left if left == down and left != up and down != right else center
			bottom_right = right if down == right and left != down and up != right else center

			output_pixels[x * 2, y * 2] = top_left
			output_pixels[x * 2 + 1, y * 2] = top_right
			output_pixels[x * 2, y * 2 + 1] = bottom_left
			output_pixels[x * 2 + 1, y * 2 + 1] = bottom_right

	return output


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

		if profile.use_scale2x:
			scaled = scale2x(source)
		else:
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
	with tempfile.TemporaryDirectory(prefix="openra-faction-infantry-") as temporary:
		workspace = Path(temporary)

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

	print(f"Normalized {len(normalized)} infantry sheets: {', '.join(normalized) or 'already current'}.")
	print("Rebuilt four Turkish production icons over role-matched native portrait bases.")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())

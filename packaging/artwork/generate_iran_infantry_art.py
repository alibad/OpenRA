#!/usr/bin/env python3
"""Rebuild Iran infantry art against the native Red Alert visual contract.

The authored Iran infantry frames deliberately retain their unique uniforms and
equipment. This tool normalizes their proportions and ground pivot against the
native infantry, then rebuilds their production icons over native RA portrait
bases so that they no longer look like flat vector placeholders.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw


UNITS = ("irbas", "iratgm", "irdc", "shadowone")
ICON_BASES = {
    "irbasicon": "e1icon",
    "iratgmicon": "e3icon",
    "irdcicon": "e6icon",
    "shadowoneicon": "spyicon",
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


def normalize_infantry_frame(path: Path) -> None:
    with Image.open(path) as source:
        if source.mode != "P":
            raise RuntimeError(f"Expected an indexed PNG: {path}")

        # Native RA infantry are shorter, broader, and stand on a baseline near
        # y=22 in the shared 50x39 canvas. The authored Iran frames average
        # y=8..27. A fixed-canvas transform avoids per-frame crop jitter while
        # moving the average silhouette to y=5..22.
        scaled_width = round(source.width * 1.2)
        scaled_height = round(source.height * 0.9)
        scaled = source.resize((scaled_width, scaled_height), Image.Resampling.NEAREST)
        output = Image.new("P", source.size, 0)
        preserve_indexed_metadata(source, output)
        output.paste(scaled, ((source.width - scaled_width) // 2, -2))
        save_args = {}
        if "transparency" in output.info:
            save_args["transparency"] = output.info["transparency"]
        output.save(path, **save_args)


def normalize_infantry_frames(paths: list[Path]) -> bool:
    """Normalize once, using the stand facings as the idempotence marker."""
    stand_bounds: list[tuple[int, int, int, int]] = []
    for path in paths[:8]:
        with Image.open(path) as stand:
            bounds = stand.convert("RGBA").getchannel("A").getbbox()
            if bounds is None:
                raise RuntimeError(f"Empty stand frame: {path}")
            stand_bounds.append(bounds)

    # The target native contract is a top near y=5 and a foot/shadow baseline
    # at y=22..23. Assets already at that contract must not be transformed a
    # second time when the generator is rerun.
    if min(top for _, top, _, _ in stand_bounds) <= 5 and max(bottom for _, _, _, bottom in stand_bounds) <= 23:
        return False

    for path in paths:
        normalize_infantry_frame(path)
    return True


def compose_icon(custom_path: Path, native_path: Path, output_path: Path, icon: str) -> None:
    with Image.open(custom_path) as custom_source, Image.open(native_path) as native_source:
        custom = custom_source.copy()
        native = native_source.copy()
        if custom.mode != "P" or native.mode != "P" or custom.size != (64, 48) or native.size != (64, 48):
            raise RuntimeError(f"Unexpected icon format for {icon}.")

        # Native portrait art supplies the established RA rendering style. The
        # existing bottom strip preserves each custom localized pixel label.
        output = native.copy()
        output.paste(custom.crop((0, 35, 64, 48)), (0, 35))

        if icon == "irdcicon":
            # Replace the engineer wrench badge with a readable drone/control
            # terminal: teal screen, bright status pixel, and radio antenna.
            draw = ImageDraw.Draw(output)
            draw.rectangle((46, 2, 62, 17), fill=12)
            draw.rectangle((48, 5, 59, 14), fill=225)
            draw.rectangle((49, 6, 58, 12), fill=224)
            draw.line((53, 4, 58, 0), fill=96, width=1)
            draw.point((59, 0), fill=160)
            draw.line((50, 9, 57, 9), fill=160, width=1)

        if icon == "shadowoneicon":
            # A compact cyan signature distinguishes the stealth commando from
            # the stock spy without replacing the native portrait treatment.
            draw = ImageDraw.Draw(output)
            draw.rectangle((45, 3, 61, 8), fill=12)
            draw.line((47, 5, 58, 5), fill=96, width=1)
            draw.point((60, 5), fill=224)

        preserve_indexed_metadata(native_source, output)
        save_args = {}
        if "transparency" in output.info:
            save_args["transparency"] = output.info["transparency"]
        output.save(output_path, **save_args)


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

    with tempfile.TemporaryDirectory(prefix="openra-iran-infantry-") as temporary:
        workspace = Path(temporary)

        for unit in UNITS:
            unit_directory = workspace / unit
            frames = extract_pngs(utility, engine_root, palette, unit, unit_directory)
            normalize_infantry_frames(frames)
            shutil.copy2(pack_shp(utility, engine_root, unit_directory, unit), bits / f"{unit}.shp")

        for icon, native_icon in ICON_BASES.items():
            icon_directory = workspace / icon
            custom_path = extract_pngs(utility, engine_root, palette, icon, icon_directory)[0]
            native_path = extract_pngs(utility, engine_root, palette, native_icon, icon_directory)[0]
            rebuilt_png = icon_directory / f"{icon}-rebuilt-0000.png"
            compose_icon(custom_path, native_path, rebuilt_png, icon)
            # The packer derives its output name from the first hyphen, so use a
            # clean one-frame prefix after composing the image.
            final_png = icon_directory / f"{icon}-0000.png"
            rebuilt_png.replace(final_png)
            shutil.copy2(pack_shp(utility, engine_root, icon_directory, icon), bits / f"{icon}.shp")

    print("Rebuilt Iran infantry sprites and native-style production icons.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

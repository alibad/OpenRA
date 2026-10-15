#!/usr/bin/env python3
"""Assemble a disposable packaged-style RA2 resources dir for a source engine build.

The product's RA2 validators expect Engine.EngineDir to contain mods/ra2 next to the
engine's own mods (as in a packaged build). This links the engine checkout's mods, glsl
and data files plus a data-only RA2 overlay produced by prepare-ra2.py --data-only.
"""
import argparse
from pathlib import Path
import shutil
import subprocess


def junction(link: Path, target: Path) -> None:
    subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], check=True, capture_output=True)


parser = argparse.ArgumentParser()
parser.add_argument("--engine", type=Path, required=True)
parser.add_argument("--stage", type=Path, required=True)
parser.add_argument("--out", type=Path, required=True)
args = parser.parse_args()
engine, stage, out = args.engine.resolve(), args.stage.resolve(), args.out.resolve()
(out / "mods").mkdir(parents=True)
for mod in sorted((engine / "mods").iterdir()):
    if mod.is_dir() and mod.name != "ra2":
        junction(out / "mods" / mod.name, mod)
junction(out / "mods" / "ra2", stage / "mods" / "ra2")
junction(out / "glsl", engine / "glsl")
for name in ("VERSION", "AUTHORS", "COPYING", "global mix database.dat"):
    shutil.copy2(engine / name, out / name)
shutil.copy2(stage / "RA2-BUILD.json", out / "RA2-BUILD.json")
print(out)

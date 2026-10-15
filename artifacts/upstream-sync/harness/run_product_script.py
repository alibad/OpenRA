#!/usr/bin/env python3
"""Run an OpenRA-AI validation script unchanged, on a Windows account without symlink rights.

The product scripts create per-run `Content` directory symlinks (developed on macOS).
Unprivileged Windows accounts cannot create symlinks, so directory links are created as
NTFS junctions instead. Nothing else about the script is altered.
"""
import pathlib
import runpy
import subprocess
import sys


def _link(self, target, target_is_directory=False):
    if not target_is_directory:
        raise OSError("Only directory links are supported by this shim")
    subprocess.run(["cmd", "/c", "mklink", "/J", str(self), str(target)], check=True, capture_output=True)


pathlib.Path.symlink_to = _link
script = sys.argv[1]
sys.argv = [script] + sys.argv[2:]
runpy.run_path(script, run_name="__main__")

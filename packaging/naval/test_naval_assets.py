#!/usr/bin/env python3
"""Regression checks for the Naval Systems authored asset contract."""

from __future__ import annotations

import json
from pathlib import Path
import re
import struct
import sys
import unittest
import wave


ROOT = Path(__file__).resolve().parents[2]
BITS = ROOT / "mods" / "ra" / "bits"
SOUNDS = BITS / "naval"
SEQUENCES = ROOT / "mods" / "ra" / "sequences" / "naval-systems.yaml"
AUDIT = ROOT / "artifacts" / "naval-audit" / "custom" / "audit-report.json"

VESSELS = ("sa_frgt", "sa_intc", "sa_fss", "ye_mslc", "ye_usv", "ye_surve")
TURRETS = ("sa_frgt_turret", "sa_frgt_ciws", "sa_intc_turret", "ye_mslc_turret", "ye_surve_turret")
SOUND_NAMES = (
    "engine-fast", "engine-heavy", "radar-sweep", "naval-alarm", "ciws-burst",
    "missile-launch", "naval-impact", "flooding", "sinking", "rearm", "chaff", "rescue",
)


sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_naval_assets as naval  # noqa: E402


def shp_frame_count(path: Path) -> int:
    with path.open("rb") as stream:
        header = stream.read(2)
    if len(header) != 2:
        raise AssertionError(f"Invalid SHP header: {path}")
    return struct.unpack("<H", header)[0]


class NavalAssetTests(unittest.TestCase):
    def test_hulls_have_authored_damage_and_directional_sink_frames(self) -> None:
        for vessel in VESSELS:
            with self.subTest(vessel=vessel):
                self.assertEqual(352, shp_frame_count(BITS / f"{vessel}.shp"))

    def test_independent_turrets_have_32_authored_facings(self) -> None:
        for turret in TURRETS:
            with self.subTest(turret=turret):
                self.assertEqual(32, shp_frame_count(BITS / f"{turret}.shp"))

    def test_sequences_use_fixed_linear_32_facing_layouts(self) -> None:
        text = SEQUENCES.read_text(encoding="utf-8")
        for vessel in VESSELS:
            block = re.search(rf"(?ms)^{vessel}:\n(?P<body>.*?)(?=^[a-z0-9_]+:|\Z)", text)
            self.assertIsNotNone(block, vessel)
            self.assertIn("Facings: 32", block.group("body"))
            self.assertIn("Start: 96", block.group("body"))
            self.assertIn("Length: 8", block.group("body"))
            self.assertNotIn("UseClassicFacingFudge", block.group("body"))

    def test_audit_records_unique_fixed_canvas_facings(self) -> None:
        audit = json.loads(AUDIT.read_text(encoding="utf-8"))
        self.assertEqual(32, audit["facings"])
        self.assertEqual([96, 72], audit["canvas"])
        self.assertTrue(audit["fixed_canvas"])
        self.assertFalse(audit["runtime_rotation"])
        for vessel in VESSELS:
            self.assertEqual(32, audit["vessels"][vessel]["unique_idle_facings"])

    def test_bow_follows_openra_counter_clockwise_facings(self) -> None:
        # Facing 0 is north (bow up), 8 west (bow left), 16 south, 24 east.
        center = (48, 39)
        for facing, (dx, dy) in {0: (0, -1), 8: (-1, 0), 16: (0, 1), 24: (1, 0)}.items():
            x, y = naval.project(0, 20, facing * 2 * naval.math.pi / naval.FACINGS)
            with self.subTest(facing=facing):
                self.assertEqual(dx, (x > center[0]) - (x < center[0]))
                self.assertEqual(dy, (y > center[1]) - (y < center[1]))

    def test_turrets_rotate_in_place_about_their_mount(self) -> None:
        for size in ("large", "small", "ciws"):
            centers = []
            for facing in range(naval.FACINGS):
                image = naval.turret_frame(size, facing)
                pixels = [(x, y) for y in range(image.height) for x in range(image.width) if image.getpixel((x, y)) not in (0, naval.IDX["black"])]
                centers.append((sum(p[0] for p in pixels) / len(pixels), sum(p[1] for p in pixels) / len(pixels)))
            with self.subTest(size=size):
                xs, ys = [c[0] for c in centers], [c[1] for c in centers]
                self.assertLess(max(xs) - min(xs), 2.5)
                self.assertLess(max(ys) - min(ys), 2.5)

    def test_production_icons_are_opaque_authored_cameos(self) -> None:
        for vessel in VESSELS:
            icon = naval.icon_for(vessel, naval.VESSELS[vessel])
            values = set(icon.tobytes())
            with self.subTest(vessel=vessel):
                self.assertEqual((64, 48), icon.size)
                self.assertFalse(values & {0, 1, 3, 4}, "cameos never use transparent/shadow/reserved indices")
                self.assertFalse(values & set(range(80, 104)), "cameos avoid remap and animated indices")

    def test_audio_is_normalized_game_ready_pcm(self) -> None:
        for name in SOUND_NAMES:
            with self.subTest(sound=name), wave.open(str(SOUNDS / f"{name}.wav"), "rb") as stream:
                self.assertEqual(1, stream.getnchannels())
                self.assertEqual(2, stream.getsampwidth())
                self.assertEqual(22050, stream.getframerate())
                frames = stream.readframes(stream.getnframes())
                samples = struct.unpack(f"<{len(frames) // 2}h", frames)
                peak = max(abs(sample) for sample in samples) / 32767
                self.assertGreaterEqual(peak, 0.70)
                self.assertLessEqual(peak, 0.96)


if __name__ == "__main__":
    unittest.main()

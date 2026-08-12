#!/usr/bin/env python3
"""Generate original, deterministic, seamless environment ambience for the RA mod."""

from __future__ import annotations

import math
import random
import struct
import wave
from functools import lru_cache
from pathlib import Path


RATE = 44_100
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "mods" / "ra" / "bits" / "environment"


@lru_cache(maxsize=None)
def noise_components(seed: int, low: int, high: int, voices: int) -> tuple[tuple[int, float, float], ...]:
    rng = random.Random(seed)
    components = []
    for _ in range(voices):
        cycles = rng.randint(low, high)
        amplitude = rng.uniform(0.25, 1.0) / math.sqrt(cycles)
        components.append((cycles, amplitude, rng.uniform(0, 2 * math.pi)))
    return tuple(components)


def periodic_noise(t: float, duration: float, seed: int, low: int, high: int, voices: int) -> float:
    components = noise_components(seed, low, high, voices)
    value = 0.0
    normalizer = 0.0
    for cycles, amplitude, phase in components:
        value += amplitude * math.sin(2 * math.pi * cycles * t / duration + phase)
        normalizer += amplitude
    return value / max(normalizer, 0.001)


def soft_clip(value: float) -> float:
    return math.tanh(value * 1.3) * 0.78


def write_loop(name: str, duration: float, synth) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    frame_count = int(RATE * duration)
    path = OUTPUT / name
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        frames = bytearray()
        for frame in range(frame_count):
            sample = soft_clip(synth(frame / RATE, duration))
            frames.extend(struct.pack("<h", int(max(-1, min(1, sample)) * 32767)))
        output.writeframes(frames)


def shamal(t: float, duration: float) -> float:
    gust = 0.58 + 0.20 * math.sin(2 * math.pi * 3 * t / duration) + 0.13 * math.sin(2 * math.pi * 7 * t / duration + 1.1)
    body = periodic_noise(t, duration, 1101, 12, 180, 34)
    grit = periodic_noise(t, duration, 1102, 180, 940, 26)
    return gust * (0.95 * body + 0.30 * grit)


def oil_fire(t: float, duration: float) -> float:
    roar = periodic_noise(t, duration, 2201, 8, 120, 36)
    hiss = periodic_noise(t, duration, 2202, 220, 1500, 34)
    pulse = 0.72 + 0.18 * math.sin(2 * math.pi * 5 * t / duration + 0.4)
    crackle = max(0, periodic_noise(t, duration, 2203, 70, 620, 18)) ** 5
    return pulse * (0.82 * roar + 0.24 * hiss) + 0.52 * crackle


def squall(t: float, duration: float) -> float:
    rain = periodic_noise(t, duration, 3301, 300, 2600, 56)
    sheets = periodic_noise(t, duration, 3302, 30, 380, 30)
    swell = 0.72 + 0.14 * math.sin(2 * math.pi * 4 * t / duration)
    return swell * (0.98 * rain + 0.34 * sheets)


def thunder(t: float, duration: float) -> float:
    attack = min(1.0, t * 18)
    decay = math.exp(-1.15 * t)
    rumble = periodic_noise(t, duration, 4401, 1, 45, 40)
    crack = periodic_noise(t, duration, 4402, 90, 900, 35) * math.exp(-5.5 * t)
    bass = math.sin(2 * math.pi * (42 - 4 * t) * t) * math.exp(-0.9 * t)
    return attack * decay * (1.05 * rumble + 0.85 * bass) + 0.75 * crack


def mirage(t: float, duration: float) -> float:
    shimmer = 0.30 * math.sin(2 * math.pi * 173 * t) + 0.22 * math.sin(2 * math.pi * 181 * t + 0.9)
    air = periodic_noise(t, duration, 5501, 80, 780, 28)
    lfo = 0.55 + 0.25 * math.sin(2 * math.pi * 2 * t / duration)
    return 0.67 * (lfo * shimmer + 0.20 * air)


def blackout(t: float, duration: float) -> float:
    mains = 0.22 * math.sin(2 * math.pi * 50 * t) + 0.10 * math.sin(2 * math.pi * 100 * t + 0.4)
    distant = periodic_noise(t, duration, 6601, 6, 90, 26)
    warning = 0.13 * math.sin(2 * math.pi * 2 * t / duration) * math.sin(2 * math.pi * 712 * t)
    return 0.58 * (mains + 0.28 * distant + warning)


def main() -> None:
    write_loop("env-shamal-wind.wav", 6.0, shamal)
    write_loop("env-oil-fire.wav", 5.0, oil_fire)
    write_loop("env-squall-rain.wav", 6.0, squall)
    write_loop("env-thunder.wav", 4.0, thunder)
    write_loop("env-mirage-hum.wav", 5.0, mirage)
    write_loop("env-blackout.wav", 5.0, blackout)
    for path in sorted(OUTPUT.glob("env-*.wav")):
        print(f"{path.relative_to(ROOT)} {path.stat().st_size} bytes")


if __name__ == "__main__":
    main()

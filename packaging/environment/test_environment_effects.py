#!/usr/bin/env python3
"""Fast structural regression checks for the World at War environment system."""

from __future__ import annotations

import re
import wave
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
RULES = ROOT / "mods" / "ra" / "rules" / "environment.yaml"
DEFAULTS = ROOT / "mods" / "ra" / "rules" / "defaults.yaml"
MOD = ROOT / "mods" / "ra" / "mod.yaml"
CODE = ROOT / "OpenRA.Mods.Common" / "Traits" / "World" / "EnvironmentDirector.cs"
SOUNDS = ROOT / "mods" / "ra" / "bits" / "environment"


EVENTS = {
    "ShamalFront": "environment-shamal",
    "OilFireSmoke": "environment-oilfire",
    "CoastalSquall": "environment-squall",
    "HeatMirage": "environment-mirage",
    "NightBlackout": "environment-blackout",
}

FILES = {
    "env-shamal-wind.wav",
    "env-oil-fire.wav",
    "env-squall-rain.wav",
    "env-thunder.wav",
    "env-mirage-hum.wav",
    "env-blackout.wav",
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    rules = RULES.read_text(encoding="utf-8")
    defaults = DEFAULTS.read_text(encoding="utf-8")
    mod = MOD.read_text(encoding="utf-8")
    code = CODE.read_text(encoding="utf-8")

    require("ra|rules/environment.yaml" in mod, "environment rules are not loaded by the RA mod")
    require(rules.count("WeatherOverlay@") == 4, "expected four particle overlay profiles")
    require(rules.count("AmbientSound@") == 5, "expected one looping ambience per event")
    require("FlashPostProcessEffect@ENVIRONMENTLIGHTNING" in rules, "squall lightning is missing")
    require("ENV.OILFIRE:" in rules and "ENV.MIRAGE-CONTACT:" in rules, "event decoration actors are missing")

    for event, condition in EVENTS.items():
        require(event in code, f"event enum/mapping missing: {event}")
        require(condition in code, f"condition mapping missing: {condition}")
        require(condition in rules, f"rules do not react to: {condition}")

    for domain in ("Ground", "Infantry", "Naval", "Air", "Building"):
        require(re.search(rf"EnvironmentResponse:\s+Domain: {domain}", defaults), f"domain not integrated: {domain}")

    for faction in ("saudi", "yemen", "turkey", "iran"):
        require(code.count(f'"{faction}"') >= 2, f"faction lacks a balanced adaptation allocation: {faction}")

    for filename in FILES:
        path = SOUNDS / filename
        require(path.exists(), f"missing sound: {filename}")
        with wave.open(str(path), "rb") as source:
            require(source.getnchannels() == 1, f"{filename} must be mono")
            require(source.getsampwidth() == 2, f"{filename} must be 16-bit")
            require(source.getframerate() == 44_100, f"{filename} must be 44.1 kHz")
            require(source.getnframes() >= 44_100 * 4, f"{filename} is too short")

    print("Environment regression checks passed: 5 events, 5 actor domains, 6 original sound assets.")


if __name__ == "__main__":
    main()

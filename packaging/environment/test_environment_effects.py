#!/usr/bin/env python3
"""Fast structural regression checks for the World at War environment system."""

from __future__ import annotations

import re
import sys
import wave
from array import array
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
    require("ra|bits/environment" in mod, "environment sound package is not mounted")
    require(rules.count("WeatherOverlay@") == 4, "expected four particle overlay profiles")
    require(rules.count("AmbientSound@") == 5, "expected one looping ambience per event")
    require("FlashPostProcessEffect@ENVIRONMENTLIGHTNING" in rules, "squall lightning is missing")
    require("ENV.OILFIRE:" in rules and "ENV.MIRAGE-CONTACT:" in rules, "event decoration actors are missing")
    require("EnvironmentDirectorInfo : TraitInfo, ILobbyOptions" in code, "environment lobby option is missing")
    require('CheckboxEnabled = false' in code, "dynamic environment must default to disabled")
    require('new LobbyBooleanOption(map, "dynamicenvironment"' in code, "dynamic environment checkbox is missing")
    require(
        'OptionOrDefault("dynamicenvironment", info.CheckboxEnabled)' in code,
        "environment runtime is not gated by the lobby option",
    )

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

            samples = array("h", source.readframes(source.getnframes()))
            if sys.byteorder != "little":
                samples.byteswap()

            peak = max(abs(value) for value in samples) / 32767
            rms = (sum(value * value for value in samples) / len(samples)) ** 0.5 / 32767
            require(0.03 <= rms <= 0.25, f"{filename} has an unsafe or inaudible RMS level: {rms:.3f}")
            require(peak < 0.98, f"{filename} clips: {peak:.3f}")
            if filename != "env-thunder.wav":
                seam = abs(samples[0] - samples[-1]) / 32767
                require(seam < 0.02, f"{filename} has an audible loop seam: {seam:.3f}")

    print(
        "Environment regression checks passed: opt-in lobby gate, "
        "5 events, 5 actor domains, 6 original sound assets."
    )


if __name__ == "__main__":
    main()

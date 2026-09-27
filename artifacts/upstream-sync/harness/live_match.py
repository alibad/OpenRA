#!/usr/bin/env python3
"""Run one isolated headless OpenRA match against an engine build and record evidence.

- Private support dir (settings, maps, logs, replays) under --out; owned game content is
  linked read-only through a directory junction when --content is given.
- Optional faction locks via a private copy of the map (never touches engine maps).
- Optional companion bridge checks (ra/ra2): live observation, a confirmed MCV move
  round-trip through ExecuteCompanionActions, then delegation to native AUTO.
- Tick progress comes from the bridge when enabled, otherwise from the replay stream.
"""
from __future__ import annotations

import argparse
import io
import json
import os
from pathlib import Path
import re
import shutil
import socket
import struct
import subprocess
import time
import zipfile


def junction(link: Path, target: Path) -> None:
    subprocess.run(["powershell.exe", "-NoProfile", "-Command",
                    f"New-Item -ItemType Junction -Path '{link}' -Target '{target}' | Out-Null"], check=True)


def prepare_map(source: Path, destination: Path, locks: dict[str, str], extra_rules: str | None) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        for item in source.iterdir():
            if item.is_file():
                shutil.copy2(item, destination / item.name)
    else:
        with zipfile.ZipFile(source) as archive:
            archive.extractall(destination)
    map_yaml = destination / "map.yaml"
    text = map_yaml.read_text(encoding="utf-8")
    for slot, faction in locks.items():
        pattern = rf"(\tPlayerReference@{slot}:\n(?:\t\t[^\n]*\n)*?)"
        block = re.search(rf"\tPlayerReference@{slot}:\n((?:\t\t[^\n]*\n)*)", text)
        if block is None:
            raise ValueError(f"Map has no {slot}")
        body = block.group(1)
        body = re.sub(r"\t\tFaction: [^\n]*\n", "", body)
        body = re.sub(r"\t\tLockFaction: [^\n]*\n", "", body)
        body += f"\t\tFaction: {faction}\n\t\tLockFaction: True\n"
        text = text[:block.start(1)] + body + text[block.end(1):]
    if extra_rules:
        text = text.rstrip("\n") + "\n\nRules:\n" + extra_rules
    map_yaml.write_text(text, encoding="utf-8", newline="\n")


def replay_max_frame(replays: Path) -> int:
    best = 0
    for replay in replays.rglob("*.orarep"):
        try:
            data = replay.read_bytes()
        except PermissionError:
            continue
        offset = 0
        while offset + 8 <= len(data):
            client, length = struct.unpack_from("<ii", data, offset)
            if length < 0 or offset + 8 + length > len(data):
                break
            if length >= 4:
                frame = struct.unpack_from("<i", data, offset + 8)[0]
                if 0 < frame < 10_000_000:
                    best = max(best, frame)
            offset += 8 + length
    return best


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--engine", type=Path, required=True)
    parser.add_argument("--mod", required=True)
    parser.add_argument("--map", type=Path, required=True, help="Map .oramap or directory to copy privately")
    parser.add_argument("--version", default="{DEV_VERSION}")
    parser.add_argument("--lock", default="", help="Multi0:turkey,Multi1:china")
    parser.add_argument("--bots", default="", help="Launch.Bots value")
    parser.add_argument("--experience-profile")
    parser.add_argument("--content", type=Path, help="Directory containing <mod> content (linked as Content)")
    parser.add_argument("--mod-search-paths")
    parser.add_argument("--bridge", action="store_true")
    parser.add_argument("--verify-move", action="store_true")
    parser.add_argument("--auto", default="", help="Delegate the human slot to native AUTO with this strategy")
    parser.add_argument("--ticks", type=int, default=4000)
    parser.add_argument("--timeout", type=float, default=600)
    parser.add_argument("--duration", type=float, default=240, help="Wall seconds for runs without the bridge")
    parser.add_argument("--extra-rules")
    parser.add_argument("--no-copy", action="store_true", help="Launch the engine's own map by package name")
    parser.add_argument("--verify-script", type=Path, help="Run the product live-match verifier against the bridge")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()

    out = args.out.resolve()
    if out.exists():
        raise SystemExit(f"{out} already exists")
    support = out / "support"
    support.mkdir(parents=True)
    if args.content:
        junction(support / "Content", args.content.resolve())
    if args.experience_profile:
        (support / "settings.yaml").write_text(
            f"Experience@{args.mod}:\n\tProfile: {args.experience_profile}\n\tUseCustomComponents: False\n", encoding="utf-8")
    locks = dict(entry.split(":", 1) for entry in args.lock.split(",") if entry)
    if args.no_copy:
        map_name = args.map.name
    else:
        map_name = args.map.stem + "-validation"
        prepare_map(args.map.resolve(), support / "maps" / args.mod / args.version / map_name, locks, args.extra_rules)

    engine = args.engine.resolve()
    env = {**os.environ, "OPENRA_AI_DISABLE_AUTOSTART": "1"}
    port = 0
    if args.bridge:
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            port = listener.getsockname()[1]
        env.update({"OPENRA_AI_COMPANION": "1", "OPENRA_AI_COMPANION_READY": "1", "OPENRA_AI_STARTUP_ENABLED": "1",
                    "OPENRA_AI_STARTUP_MUTED": "1", "OPENRA_AI_STARTUP_AUTO_ACT": "0", "OPENRA_AI_GRPC_PORT": str(port)})
    else:
        env.pop("OPENRA_AI_COMPANION", None)
    command = ["dotnet", str(engine / "bin" / "OpenRA.dll"), f"Engine.EngineDir={engine}",
               f"Engine.SupportDir={support}", f"Game.Mod={args.mod}", "Game.Platform=Null",
               "Game.FetchNews=false", f"Launch.Map={map_name}"]
    if args.bots:
        command.append(f"Launch.Bots={args.bots}")
    if args.mod_search_paths:
        command.append(f"Engine.ModSearchPaths={args.mod_search_paths}")

    result: dict[str, object] = {"mod": args.mod, "map": str(args.map), "locks": locks, "bots": args.bots,
                                 "profile": args.experience_profile, "passed": False, "out": str(out)}
    samples: list[dict[str, object]] = []
    start = time.monotonic()
    with (out / "game.log").open("w", encoding="utf-8") as log:
        process = subprocess.Popen(command, cwd=engine / "bin", env=env, stdout=log, stderr=subprocess.STDOUT)
        result["pid"] = process.pid
        try:
            if args.bridge:
                from openra_ai_companion.bridge import OpenRABridge
                from openra_ai_companion.models import ActionCommand
                bridge = OpenRABridge(f"127.0.0.1:{port}", timeout=2.0)
                phase = "observe"
                move = None
                delegated = False
                last_sample = 0.0
                while process.poll() is None and time.monotonic() - start < args.timeout:
                    try:
                        snap = bridge.observe()
                    except RuntimeError as error:
                        result["last_error"] = str(error)
                        time.sleep(0.5)
                        continue
                    if snap.tick <= 0:
                        time.sleep(0.5)
                        continue
                    if phase == "observe":
                        result["first_observation"] = {"tick": snap.tick, "map": snap.map_name, "mod": snap.mod_id,
                                                       "units": len(snap.units), "buildings": len(snap.buildings),
                                                       "explored_percent": round(snap.explored_percent, 1),
                                                       "state": bridge.state()}
                        phase = "move" if args.verify_move else "auto"
                        if args.verify_script:
                            import sys as _sys
                            verifier = subprocess.run([_sys.executable, str(args.verify_script), "--bridge", f"127.0.0.1:{port}",
                                                       "--timeout", "60"], capture_output=True, text=True, env=os.environ)
                            result["live_match_verifier"] = {"exit": verifier.returncode, "stdout": verifier.stdout.strip(),
                                                             "stderr": verifier.stderr.strip()[-2000:]}
                            if verifier.returncode != 0:
                                raise RuntimeError("Product live-match verifier failed")
                    if phase == "move":
                        mcv = next((u for u in snap.units if u.kind in {"mcv", "amcv", "smcv"}), None)
                        if move is None:
                            if mcv is None:
                                raise RuntimeError("No starting MCV for the confirmed-move round-trip")
                            target = (mcv.cell_x + 2, mcv.cell_y + 1)
                            receipt = bridge.execute_actions("upstream-sync-move", snap.tick, (
                                ActionCommand("move", actor_id=mcv.actor_id, target_x=target[0], target_y=target[1]),))
                            move = {"actor_id": mcv.actor_id, "from": [mcv.cell_x, mcv.cell_y], "target": list(target),
                                    "receipt": receipt.as_dict(), "issued_tick": snap.tick}
                            result["move"] = move
                            if not receipt.accepted:
                                raise RuntimeError("Confirmed move was rejected")
                        elif mcv is not None and [mcv.cell_x, mcv.cell_y] == move["target"]:
                            move["arrived_tick"] = snap.tick
                            phase = "auto"
                        elif snap.tick - move["issued_tick"] > 1500:
                            raise RuntimeError("MCV did not arrive at the confirmed move target")
                    if phase == "auto" and args.auto and not delegated:
                        delegated = bridge.update_companion_status(f"auto-active:{args.auto}", "upstream-sync AUTO validation", muted=True)
                        result["auto_delegated_tick"] = snap.tick
                        result["auto_delegation_accepted"] = delegated
                    if time.monotonic() - last_sample > 5:
                        last_sample = time.monotonic()
                        samples.append({"wall": round(time.monotonic() - start, 1), "tick": snap.tick, "cash": snap.cash,
                                        "buildings": sorted(a.kind for a in snap.buildings),
                                        "units": len(snap.units), "done": snap.done})
                    result["final_tick"] = snap.tick
                    if snap.tick >= args.ticks:
                        result["final_state"] = bridge.state()
                        break
                    time.sleep(0.25)
                bridge.close()
            else:
                # The replay is exclusively locked while the game runs; run for the wall-clock
                # budget and read the replay stream after the process has stopped.
                while process.poll() is None and time.monotonic() - start < args.duration:
                    time.sleep(2)
        except Exception as error:  # noqa: BLE001 - recorded in the evidence
            result["error"] = f"{type(error).__name__}: {error}"
        finally:
            result["exit_code_before_stop"] = process.poll()
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
    result["wall_seconds"] = round(time.monotonic() - start, 1)
    result["samples"] = samples
    result["replay_max_frame"] = replay_max_frame(support / "Replays")
    logs = support / "Logs"
    exceptions = []
    for path in sorted(logs.glob("*.log")) if logs.exists() else []:
        text = path.read_text(encoding="utf-8", errors="replace")
        lines = text.splitlines()
        skips = [line for line in lines if line.startswith(("Skipping trait ", "Skipping WorldLoaded "))]
        if skips:
            result.setdefault("headless_skips", []).extend(skips)
        hits = [line for line in lines if re.search(r"Exception|Unhandled|FATAL", line) and line not in skips]
        if path.name.startswith("exception"):
            hits = hits or lines[:5]
        if hits:
            exceptions.append({"file": path.name, "count": len(hits), "first": hits[:5]})
    console = (out / "game.log").read_text(encoding="utf-8", errors="replace")
    console_hits = [line for line in console.splitlines() if re.search(r"Exception|Unhandled|FATAL", line)]
    if console_hits:
        exceptions.append({"file": "game.log(console)", "count": len(console_hits), "first": console_hits[:5]})
    result["exceptions"] = exceptions
    # One replay net frame covers three world ticks (NetTickScale) in these local games.
    result["replay_ticks_estimate"] = result["replay_max_frame"] * 3
    reached = max([s.get("tick", 0) or 0 for s in samples] + [result.get("final_tick", 0), result["replay_ticks_estimate"]])
    result["max_tick_or_frame"] = reached
    result["passed"] = "error" not in result and not exceptions and reached >= args.ticks \
        and result["exit_code_before_stop"] is None
    (out / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    summary = {k: result.get(k) for k in ("mod", "passed", "max_tick_or_frame", "wall_seconds", "error", "exit_code_before_stop",
                                          "auto_delegation_accepted", "move")}
    summary["exceptions"] = len(exceptions)
    print(json.dumps(summary))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

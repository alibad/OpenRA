#!/usr/bin/env python3
"""Launch a rendered (Game.Platform=Default) OpenRA window from an engine build and screenshot it.

Uses a private support dir, a private bridge port and never starts the companion or any
local model (OPENRA_AI_DISABLE_AUTOSTART=1). Screenshots come from the product's
capture-openra-window.ps1 (desktop capture of the game window) and, when the bridge is
enabled, from the engine's own CaptureCompanionFrame render-target readback.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import socket
import subprocess
import time


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--engine", type=Path, required=True)
    parser.add_argument("--support", type=Path, required=True)
    parser.add_argument("--capture-script", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--mod", default="ra")
    parser.add_argument("--map")
    parser.add_argument("--bots", default="")
    parser.add_argument("--bridge", action="store_true")
    parser.add_argument("--auto", default="")
    parser.add_argument("--wait", type=float, default=25)
    parser.add_argument("--run-seconds", type=float, default=0, help="Keep the game running this long after the capture")
    args = parser.parse_args()

    engine = args.engine.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
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
    command = ["dotnet", str(engine / "bin" / "OpenRA.dll"), f"Engine.EngineDir={engine}", f"Engine.SupportDir={args.support}",
               f"Game.Mod={args.mod}", "Game.Platform=Default", "Game.FetchNews=false", "Graphics.Mode=Windowed",
               "Graphics.WindowedSize=1280,800", "Sound.Mute=True"]
    if args.map:
        command.append(f"Launch.Map={args.map}")
    if args.bots:
        command.append(f"Launch.Bots={args.bots}")
    result: dict[str, object] = {"command": command, "port": port}
    with (args.output / "game.log").open("w", encoding="utf-8") as log:
        process = subprocess.Popen(command, cwd=engine / "bin", env=env, stdout=log, stderr=subprocess.STDOUT)
        result["pid"] = process.pid
        try:
            if args.bridge and args.auto:
                from openra_ai_companion.bridge import OpenRABridge
                with OpenRABridge(f"127.0.0.1:{port}", timeout=2.0) as bridge:
                    deadline = time.monotonic() + 90
                    while time.monotonic() < deadline:
                        try:
                            snap = bridge.observe()
                            if snap.tick > 0:
                                result["auto_accepted"] = bridge.update_companion_status(
                                    f"auto-active:{args.auto}", "upstream-sync rendered AUTO", muted=True)
                                break
                        except RuntimeError:
                            pass
                        time.sleep(0.5)
            time.sleep(args.wait)
            capture = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(args.capture_script),
                                      "-ProcessId", str(process.pid), "-Output", str(args.output / "window.png"),
                                      "-TimeoutSeconds", "60"], capture_output=True, text=True)
            result["capture"] = {"exit": capture.returncode, "stdout": capture.stdout.strip(), "stderr": capture.stderr.strip()[-1500:]}
            if args.bridge:
                from openra_ai_companion.bridge import OpenRABridge
                with OpenRABridge(f"127.0.0.1:{port}", timeout=10.0) as bridge:
                    try:
                        frame = bridge.capture_frame()
                        (args.output / "engine-frame.png").write_bytes(frame.png)
                        result["engine_frame"] = {"tick": frame.tick, "width": frame.width, "height": frame.height, "scope": frame.scope}
                        snap = bridge.observe()
                        result["observation"] = {"tick": snap.tick, "units": len(snap.units), "buildings": sorted(a.kind for a in snap.buildings)}
                    except RuntimeError as error:
                        result["engine_frame_error"] = str(error)
            if args.run_seconds:
                time.sleep(args.run_seconds)
            result["alive_at_end"] = process.poll() is None
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
    logs = args.support / "Logs"
    result["exception_logs"] = sorted(p.name for p in logs.glob("exception*")) if logs.exists() else []
    (args.output / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "command"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

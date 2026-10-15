#!/usr/bin/env bash
# usage: CONTENT=<dir containing ra2/*.mix> ra2lint.sh <engine-dir> <ra2-stage-dir> <outdir>
# <ra2-stage-dir> is the --resources output of OpenRA-AI scripts/prepare-ra2.py --data-only.
CONTENT="${CONTENT:?set CONTENT to a directory that contains ra2/ra2.mix}"
ENGINE="$(cygpath -m "$1")"; STAGE="$(cygpath -m "$2")"; OUT="$3"; mkdir -p "$OUT"
SUP="$OUT/support-$(date +%s)"; mkdir -p "$SUP"
powershell.exe -NoProfile -Command "New-Item -ItemType Junction -Path '$(cygpath -w "$SUP/Content")' -Target '$(cygpath -w "$CONTENT")' | Out-Null"
cd "$ENGINE" && ENGINE_DIR="$ENGINE" SUPPORT_DIR="$(cygpath -m "$SUP")" MOD_SEARCH_PATHS="$ENGINE/mods,$STAGE/mods" \
  dotnet "$ENGINE/bin/OpenRA.Utility.dll" ra2 --check-yaml > "$OUT/ra2.log" 2>&1; echo "exit=$?" >> "$OUT/ra2.log"
grep -c "Testing map" "$OUT/ra2.log"; tail -4 "$OUT/ra2.log"
mkdir -p "$OUT/norm"; grep -E '^(Warning|Error):' "$OUT/ra2.log" | sed -E 's/\r$//' | sort > "$OUT/norm/ra2.txt"; wc -l < "$OUT/norm/ra2.txt"

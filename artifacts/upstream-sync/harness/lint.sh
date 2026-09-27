#!/usr/bin/env bash
# usage: lint.sh <engine-dir> <outdir>
ENGINE="$(cygpath -m "$1")"; OUT="$(cygpath -m "$2")"; mkdir -p "$OUT"
SUP="$OUT/support"; rm -rf "$SUP"; mkdir -p "$SUP"
U="$ENGINE/bin/OpenRA.Utility.dll"
run() { # name, profile, args...
  local name="$1" prof="$2"; shift 2
  local sup="$SUP/$name"; mkdir -p "$sup"
  ( cd "$ENGINE" && ENGINE_DIR="$ENGINE" SUPPORT_DIR="$sup" OPENRA_UTILITY_EXPERIENCE_PROFILE="$prof" dotnet "$U" "$@" > "$OUT/$name.log" 2>&1; echo "exit=$?" >> "$OUT/$name.log" )
}
run ra-wwiii world-war-iii ra --check-yaml &
run ra-classic ai-assistant-only ra --check-yaml &
run cnc "" cnc --check-yaml &
run d2k "" d2k --check-yaml &
wait
run ts "" ts --check-yaml &
run content "" ra-content --check-yaml &
run explicit "" all --check-explicit-interfaces &
run condoverrides "" all --check-conditional-trait-interface-overrides &
wait
for m in cnc-content d2k-content ts-content; do run $m "" $m --check-yaml; done
for f in "$OUT"/*.log; do echo "$(basename $f): $(grep -c '^Warning\|Warning:' $f) warn-lines, $(grep -ci 'error' $f) error-lines, $(tail -1 $f)"; done

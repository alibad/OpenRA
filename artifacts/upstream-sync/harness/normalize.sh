#!/usr/bin/env bash
# usage: normalize.sh <lintdir> -> writes <lintdir>/norm/<name>.txt (sorted warning+error first lines)
D="$1"; mkdir -p "$D/norm"
for f in "$D"/*.log; do n=$(basename "$f" .log); grep -E '^(Warning|Error):' "$f" | sed -E 's/\r$//' | sort > "$D/norm/$n.txt"; done
wc -l "$D"/norm/*.txt

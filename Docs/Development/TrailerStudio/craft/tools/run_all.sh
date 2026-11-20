#!/bin/sh
# Analyze every downloaded trailer (and optional extra mp4s given as slug=path args).
ROOT="C:/Users/evanp/Documents/Codex/Deliverables/TrailerCraft-Reference"
cd "$ROOT"
run() {
  slug="$1"; vid="$2"
  [ -f "analysis/$slug/summary.json" ] && [ -z "$FORCE" ] && return
  echo "== $slug"
  python -P scripts/analyze.py "$vid" "analysis/$slug" $DETECT_ARGS 2>/dev/null | head -1
  dur=$(python -P -c "import json;print(json.load(open('analysis/$slug/summary.json'))['duration'])")
  if python -P -c "import sys;sys.exit(0 if $dur<=95 else 1)"; then
    python -P scripts/sheets.py "$vid" "analysis/$slug" filmstrip
  else
    python -P scripts/sheets.py "$vid" "analysis/$slug" filmstrip --from 0 --to 60
    python -P scripts/sheets.py "$vid" "analysis/$slug" filmstrip --from $(python -P -c "print(int($dur-20))") --to 9999
  fi
}
for f in videos/*.mp4; do
  slug=$(basename "$f" .mp4)
  run "$slug" "$f"
done
for kv in "$@"; do
  run "${kv%%=*}" "${kv#*=}"
done

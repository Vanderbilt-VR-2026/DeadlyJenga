#!/bin/sh
# Analyze one video: run_one.sh <slug> <video.mp4>  (detector + audio + filmstrips)
ROOT="C:/Users/evanp/Documents/Codex/Deliverables/TrailerCraft-Reference"
cd "$ROOT"
slug="$1"; vid="$2"
python -P scripts/analyze.py "$vid" "analysis/$slug" $DETECT_ARGS 2>/dev/null | head -1
rm -f analysis/$slug/sheets/film_*.jpg
dur=$(python -P -c "import json;print(json.load(open('analysis/$slug/summary.json'))['duration'])")
if python -P -c "import sys;sys.exit(0 if $dur<=95 else 1)"; then
  python -P scripts/sheets.py "$vid" "analysis/$slug" filmstrip
else
  python -P scripts/sheets.py "$vid" "analysis/$slug" filmstrip --from 0 --to 60
  python -P scripts/sheets.py "$vid" "analysis/$slug" filmstrip --from $(python -P -c "print(int($dur-20))") --to 9999
fi
echo "done $slug"

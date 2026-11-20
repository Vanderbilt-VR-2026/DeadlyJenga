#!/bin/sh
# Downloads each listed official trailer one at a time at <=480p (with audio).
ROOT="C:/Users/evanp/Documents/Codex/Deliverables/TrailerCraft-Reference"
tail -n +2 "$ROOT/scripts/trailers.csv" | while IFS=, read slug id type vr genre; do
  out="$ROOT/videos/$slug.mp4"
  [ -f "$out" ] && continue
  echo "== $slug"
  yt-dlp --no-warnings --no-playlist --no-cookies-from-browser \
    -f "bv*[height<=480][ext=mp4]+ba[ext=m4a]/b[height<=480][ext=mp4]/bv*[height<=480]+ba/b" \
    --merge-output-format mp4 --write-info-json --no-write-comments \
    -o "$ROOT/videos/$slug.%(ext)s" -- "$id" || echo "FAILED $slug"
  sleep 3
done

#!/bin/bash
# Build layout.json, render it in Blender, and make a side-by-side with the approved picture.
LAB="$(cd "$(dirname "$0")" && pwd -W 2>/dev/null || pwd)"
REF="$LAB/../approved-reference.png"
python "$LAB/design.py" || exit 1
"/c/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P "$LAB/render.py" -- "$LAB/layout.json" "$LAB/out" "$LAB/cam.json" ${1:-ref,top} 2>&1 | grep -E "RENDERED|Error|error|Traceback" | head -20
python - "$LAB" "$REF" <<'EOF'
import sys
from PIL import Image
lab, ref = sys.argv[1], sys.argv[2]
r = Image.open(ref).convert("RGB").resize((1600, 900))
m = Image.open(lab + "/out/ref.png").convert("RGB")
side = Image.new("RGB", (1600, 1800))
side.paste(r, (0, 0)); side.paste(m, (0, 900))
side.thumbnail((1200, 1400)); side.save(lab + "/out/side.jpg", quality=85)
Image.blend(r, m, .5).save(lab + "/out/overlay.jpg", quality=85)
EOF

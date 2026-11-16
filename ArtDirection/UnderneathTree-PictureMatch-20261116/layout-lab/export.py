"""Writes the layout as C# data for UnderneathTreeMap. Run after design.py."""
import json, os, sys
lab = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(lab, "layout.json")))
out = sys.argv[1]
alias = {"Left Tower / Lintel 1": "Train Tunnel / Gold Lintel"}
name = lambda n: alias.get(n, n)
f = lambda v: ("%.3f" % v).rstrip("0").rstrip(".") + "f"
q = lambda s: "null" if not s else '"%s"' % name(s)
lines = ["// Generated from the layout lab (ArtDirection/UnderneathTree-PictureMatch-20261116/layout-lab).",
         "// Change the layout there and export again; do not edit the numbers by hand.",
         "using UnityEngine;", "",
         "namespace DeadlyJenga.CastlePrototype", "{",
         "    public sealed partial class UnderneathTreeMap", "    {",
         "        // Rug size and how far its centre sits behind the train loop, in course units.",
         "        const float ApprovedRugRadius = %s, ApprovedRugBack = %s;" % (f(data["rug_radius"]), f(data["rug_z"])),
         "        const float ApprovedRailHeight = %s;" % f(data["track_height"]), "",
         "        readonly struct GiftSpec",
         "        {",
         "            public readonly string name, wrap, ribbon, on, on2;",
         "            public readonly float x, z, y, w, h, d, turn;",
         "            public readonly bool round, rail;", "",
         "            public GiftSpec(string name, float x, float z, float y, float w, float h, float d, float turn,",
         "                string wrap, string ribbon, string on = null, string on2 = null, bool round = false, bool rail = false)",
         "            {",
         "                this.name = name; this.x = x; this.z = z; this.y = y; this.w = w; this.h = h; this.d = d;",
         "                this.turn = turn; this.wrap = wrap; this.ribbon = ribbon; this.on = on; this.on2 = on2;",
         "                this.round = round; this.rail = rail;",
         "            }",
         "        }", "",
         "        // x = right, z = back, y = height of the gift's underside above the rug.",
         "        static readonly GiftSpec[] ApprovedGifts =", "        {"]
for g in data["gifts"]:
    extra = ""
    if g.get("on"): extra += ", on: " + q(g["on"])
    if g.get("on2"): extra += ", on2: " + q(g["on2"])
    if g.get("shape") == "round": extra += ", round: true"
    if g.get("role") == "rail": extra += ", rail: true"
    lines.append('            new GiftSpec("%s", %s, %s, %s, %s, %s, %s, %s, "%s", "%s"%s),' % (
        name(g["name"]), f(g["x"]), f(g["z"]), f(g["y"]), f(g["w"]), f(g["h"]), f(g["d"]), f(g["turn"]),
        g["color"], g["ribbon"], extra))
lines += ["        };", "", "        // Centre line of the rails, one point per rung.",
          "        static readonly Vector2[] ApprovedRail =", "        {"]
tr = data["track"]
for i in range(0, len(tr), 4):
    lines.append("            " + " ".join("new Vector2(%s, %s)," % (f(u), f(v)) for u, v in tr[i:i + 4]))
lines += ["        };", "    }", "}", ""]
open(out, "w", newline="\n").write("\n".join(lines))
print("wrote", out, len(data["gifts"]), "gifts")

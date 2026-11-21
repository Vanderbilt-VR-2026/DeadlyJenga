"""Stage 4 driver: estimated layouts (optional) -> assembler -> review renders.

    python pipeline/assemble.py B_544397715 --kit victorian_gothic --level 3+ --estimate kirkland --render kirkland
    python pipeline/assemble.py B_544413714 --kit brick_institutional --level 3 --estimate grid --render tibbot
    python pipeline/assemble.py B_X --kit georgian --level 3            # use the newest saved layouts
    python pipeline/assemble.py --index                                 # rebuild derived/buildings/lod3/index.json

--estimate writes new layout versions only when they differ from the newest saved one.
--render NAME writes Docs/Development/VanderbiltCampus/progress/images/stage4-NAME-<n>.png
(six views) and, for Kirkland, the side-by-side with the Commons photo.
"""
import argparse
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "blender"))
from common import data_root, fail  # noqa: E402

COMMONS = {"path": "raw/capture/commons-2026-11-19/wikimedia/Kirkland_Hall/2019_Sit-in.jpg",
           "blurred": "derived/capture/commons-2026-11-19/blurred/wikimedia/Kirkland_Hall/2019_Sit-in.jpg",
           "license": "Public domain (Wikimedia Commons, File:2019_Sit-in.jpg, author unknown)",
           "use": "visual reference only: main stair, pointed portal, brick walls with stone trim, raised stone "
                  "basement with arched windows, iron stair rails (not modeled). Not aligned (single photo)."}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("building", nargs="?")
    ap.add_argument("--kit")
    ap.add_argument("--level", choices=["3", "3+"], default="3")
    ap.add_argument("--estimate", choices=["grid", "kirkland"], help="make estimated layouts first")
    ap.add_argument("--render", metavar="NAME", help="review renders named stage4-NAME-<n>.png")
    ap.add_argument("--no-blend", action="store_true")
    ap.add_argument("--version", type=int, help="write into lod3_v<N> (default: next free number)")
    ap.add_argument("--index", action="store_true", help="only rebuild the lod3 index")
    args = ap.parse_args(argv)
    import assemble_building as ab
    ab._imports()
    root = data_root()
    if args.index:
        print(ab.write_index(root))
        return
    if not args.building or not args.kit:
        fail("give a building id and --kit")
    t0 = time.time()
    roof, layouts, extra, notes = None, None, {}, []
    if args.estimate:
        import estimate_layouts as el
        ctx = el.load_context(args.building, root)
        roof = ctx["roof"]
        lays = el.kirkland_layouts(ctx) if args.estimate == "kirkland" else el.grid_layouts(ctx, args.kit)
        layouts = el.save_if_changed(lays, root)
        notes.append(f"layouts estimated by estimate_layouts.py rule '{args.estimate}' (source=estimated); "
                     f"LiDAR step clusters used: {json.dumps(ctx['clusters'])}")
        if args.estimate == "kirkland":
            extra = {"reference_images": [COMMONS], "public_facts": el.KIRKLAND_FACTS}
            notes.append("Kirkland stand-in (D009): no on-site photos; features are estimated from LiDAR "
                         "(tower heights, stairs, ground), the one Commons photo (visual reference) and public "
                         "facts. Re-run with measured layouts when the capture exists.")
    s = ab.assemble(args.building, args.kit, args.level, root=root, layouts=layouts, roof=roof,
                    blend=not args.no_blend, extra_sources=extra, notes=notes, version=args.version)
    print(json.dumps({k: s[k] for k in ("building", "kit", "level", "triangles", "ledger_rows", "lod3_problems",
                                         "folder", "measured")}, indent=1))
    print("collider:", json.dumps(s["collider"]))
    for k, r in sorted(s["facades"].items()):
        if r.get("error") or r.get("skipped_outside_wall"):
            print(" ", k, r)
    if args.render:
        import render_building as rb
        paths = rb.review_set(Path(s["folder"]), args.render, commons=(args.estimate == "kirkland"))
        for p in paths:
            print("render:", p)
    print(f"done in {time.time() - t0:.0f} s")
    if s["lod3_problems"] or s["collider"]["problems"]:
        fail(f"mesh problems: {s['lod3_problems']} collider: {s['collider']['problems']}")


if __name__ == "__main__":
    main()

"""Stage 5 realism readiness: is every LoD3 building ready for a later photo bake?

For every building in derived/buildings/lod3/index.json (its listed lod3 folder):
  uv0          lod3.npz has a UV0 triangle for every face, inside 0..1, none with zero area
  material_ids every face has a material index from config/material_ids.json
  ledger       ledger.json present with an "entries" list
  sources      sources.json present
  photos       registered photos: the building's Stage 3 sources list photos whose alignment
               cameras.json gives them a campus pose marked reliable (at least one)
Anything missing is listed per building; estimated stand-ins are reported like any other
building (nothing is hidden).

Writes reports/stage5/readiness.json and readiness.md in the archive (manifest) and a copy of
the Markdown at Docs/Development/VanderbiltCampus/progress/stage5-readiness.md.

    python pipeline/readiness_check.py
Exit code 1 when any LoD3 building has a missing item.
"""
import argparse
import datetime
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REPO, data_root, load_json, save_json  # noqa: E402

DOC = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "stage5-readiness.md"


def check_building(row, root, cams):
    import build_all as ba
    import vcmesh
    folder = Path(root) / row["folder"]
    items, missing = {}, []
    lod3 = folder / "lod3.npz"
    if lod3.exists():
        m = vcmesh.load_mesh(lod3)
        uv = m["uv0"].astype(np.float64)
        ok = uv.shape == (len(m["faces"]), 3, 2) and uv.min() >= -1e-6 and uv.max() <= 1 + 1e-6
        a, b = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
        zero = int((np.abs(a[:, 0] * b[:, 1] - a[:, 1] * b[:, 0]) < 1e-14).sum()) if ok else len(m["faces"])
        items["uv0"] = {"ok": bool(ok and zero == 0), "faces": int(len(m["faces"])), "zero_area": zero}
        known = set(vcmesh.material_index().values())
        bad = int((~np.isin(m["face_mat"], list(known))).sum())
        items["material_ids"] = {"ok": bad == 0, "unknown_faces": bad}
    else:
        items["uv0"] = items["material_ids"] = {"ok": False, "note": "lod3.npz missing"}
    lp = folder / "ledger.json"
    items["ledger"] = {"ok": lp.exists() and isinstance(load_json(lp).get("entries"), list),
                       "rows": len(load_json(lp)["entries"]) if lp.exists() else 0}
    items["sources"] = {"ok": (folder / "sources.json").exists()}
    fph = ba.facade_photos(row["id"], root, cams)
    rel = len({ph for v in fph.values() for ph in v["reliable_ids"]})      # distinct photos
    items["photos"] = {"ok": rel > 0, "registered_reliable_photos": rel,
                       "facades_with_photos": sorted(k for k, v in fph.items() if v["reliable"] > 0)}
    for k, v in items.items():
        if not v["ok"]:
            missing.append(k)
    return {"id": row["id"], "name": row.get("name"), "folder": row["folder"], "measured": row.get("measured"),
            "items": items, "missing": missing}


def run(root=None):
    import build_all as ba
    root = Path(root or data_root())
    idx = load_json(root / "derived" / "buildings" / "lod3" / "index.json")
    cams = ba.Cameras(root)
    rows = [check_building(r, root, cams) for r in idx["buildings"]]
    res = {"made": datetime.datetime.now().isoformat(timespec="seconds"), "buildings": len(rows),
           "with_missing_items": sum(1 for r in rows if r["missing"]),
           "missing_items_total": sum(len(r["missing"]) for r in rows), "rows": rows}
    out = root / "reports" / "stage5"
    save_json(out / "readiness.json", res)
    lines = ["# Stage 5 realism readiness", "",
             f"Written by `pipeline/readiness_check.py` on {res['made']}. LoD3 buildings: {len(rows)}; "
             f"with missing items: {res['with_missing_items']}; missing items: {res['missing_items_total']}.", "",
             "| Building | Name | Measured | UV0 | Material IDs | Ledger | Sources | Registered photos | Missing |",
             "|---|---|---|---|---|---|---|---|---|"]
    yn = lambda b: "yes" if b else "**MISSING**"  # noqa: E731
    for r in rows:
        it = r["items"]
        lines.append(f"| {r['id']} | {r['name'] or ''} | {'yes' if r['measured'] else 'no (estimated)'} | "
                     f"{yn(it['uv0']['ok'])} | {yn(it['material_ids']['ok'])} | {yn(it['ledger']['ok'])} | "
                     f"{yn(it['sources']['ok'])} | {yn(it['photos']['ok'])} ({it['photos']['registered_reliable_photos']}) | "
                     f"{', '.join(r['missing']) or 'none'} |")
    md = "\n".join(lines) + "\n"
    (out / "readiness.md").write_text(md, encoding="utf-8", newline="\n")
    DOC.parent.mkdir(parents=True, exist_ok=True)
    if not __import__("os").environ.get("VC_DATA_ROOT"):
        DOC.write_text(md, encoding="utf-8", newline="\n")
    import manifest
    for p in (out / "readiness.json", out / "readiness.md"):
        manifest.add_file(p, "pipeline/readiness_check.py", "derived", stage=5, root=root)
    return res


def main(argv=None):
    argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter).parse_args(argv)
    res = run()
    for r in res["rows"]:
        print(f"{r['id']:14s} missing: {', '.join(r['missing']) or 'none'}")
    print(f"{res['buildings']} LoD3 buildings, {res['missing_items_total']} missing items")
    if res["missing_items_total"]:
        sys.exit(1)


if __name__ == "__main__":
    main()

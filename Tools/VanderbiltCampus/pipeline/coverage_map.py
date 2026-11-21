"""Stage 2 coverage map: one self-contained HTML page Evan opens on his phone.

Reads config/shot_list.json, config/capture_days.json (routes, optional) and
config/coverage_status.json (written by intake; missing = everything not started).
Writes Docs/Development/VanderbiltCampus/capture/coverage/index.html:
  - campus top-down, north up, NAIP 2021 background (public domain) as an inline JPEG;
  - every facade as a thick line colored AND dashed by status, other entries as lines/markers;
  - tap an entry -> bottom sheet with its shot-list entry and a small plan-view framing sketch;
  - filters (day, priority, status), capture-day routes, progress bars, optional "you are here".
No external requests, no frameworks, no photos. Intake calls build_page() after each import.

coverage_status.json format:
  {"version": 1, "updated": "...", "entries": {"<entry id>": {"status": "captured", "photos": 52,
   "last": "2026-12-02", "note": ""}}}
  status is one of not_started, captured, aligned, needs_reshoot; a bare status string also works.

Commands:
  coverage_map.py build [--no-background]
"""
import argparse
import base64
import gzip
import io
import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, REPO, data_root, fail, load_json  # noqa: E402

SHOT_LIST = CONFIG / "shot_list.json"
DAYS_JSON = CONFIG / "capture_days.json"
STATUS_JSON = CONFIG / "coverage_status.json"
OUT = REPO / "Docs" / "Development" / "VanderbiltCampus" / "capture" / "coverage" / "index.html"
STATUSES = ["not_started", "captured", "aligned", "needs_reshoot"]
PRIORITIES = ["hero", "normal", "background"]
KINDS = ["facade", "courtyard", "plaza", "path", "stairs", "detail", "swatch"]
METHODS = ["photo_set", "360_walk", "detail_set", "swatch"]
ACCESS = ["public", "restricted", "blocked"]
MAX_IMAGE = 700_000
MAX_PAGE = 3_000_000
PLAIN_LIMIT = 1_500_000     # inline JSON as text up to this size, else gzip + base64


def load_status(path=None):
    """{id: {"status", "photos", "last", "note"}} from coverage_status.json (lenient)."""
    p = Path(path or STATUS_JSON)
    if not p.exists():
        return {}, None
    doc = load_json(p)
    raw = doc.get("entries", doc) if isinstance(doc, dict) else doc
    out = {}
    if isinstance(raw, list):
        raw = {r.get("id"): r for r in raw if isinstance(r, dict)}
    for k, v in (raw or {}).items():
        if k in ("version", "updated"):
            continue
        if isinstance(v, str):
            v = {"status": v}
        st = (v or {}).get("status", "not_started")
        if st not in STATUSES:
            st = "not_started"
        # Accept intake's field names too (usable_count / photo_count, last_capture, reason).
        photos = next((v[f] for f in ("photos", "usable_count", "photo_count") if v.get(f) is not None), None)
        out[k] = {"status": st, "photos": photos, "last": v.get("last") or v.get("last_capture"),
                  "note": v.get("note") or v.get("reason")}
    return out, (doc.get("updated") if isinstance(doc, dict) else None)


def frame_affine(frame=None):
    """Affine lon/lat -> campus X, Y fitted around the origin; returns coefficients and max error (m)."""
    import frame as fr
    f = frame or fr.load_frame()
    lon0, lat0 = f["origin"]["lon"], f["origin"]["lat"]
    g = np.linspace(-1, 1, 9)
    dlon, dlat = np.meshgrid(g * 0.016, g * 0.014)
    dlon, dlat = dlon.ravel(), dlat.ravel()
    X, Y, _ = fr.to_campus(lon0 + dlon, lat0 + dlat, np.zeros_like(dlon), 4326, frame=f)
    A = np.column_stack([np.ones_like(dlon), dlon, dlat, dlon * dlon, dlon * dlat, dlat * dlat])
    cx, *_ = np.linalg.lstsq(A, X, rcond=None)
    cy, *_ = np.linalg.lstsq(A, Y, rcond=None)
    err = float(np.max(np.hypot(A @ cx - X, A @ cy - Y)))
    return {"lon0": lon0, "lat0": lat0, "cx": [float(c) for c in cx], "cy": [float(c) for c in cy],
            "max_err_m": round(err, 3)}


def flat(coords, nd=1):
    out = []
    for p in coords or []:
        out += [round(float(p[0]), nd), round(float(p[1]), nd)]
    return out


def background(root, bounds, max_bytes=MAX_IMAGE):
    """NAIP 2021 crop of the page area as a JPEG data URI (<= max_bytes)."""
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = None
    meta = load_json(root / "derived" / "ortho" / "v1" / "naip2021_campus_0.6m.json")
    img = Image.open(root / "derived" / "ortho" / "v1" / meta["file"]).convert("RGB")
    x0, y0, x1, y1 = bounds
    c0 = max(0, int((x0 - meta["origin_x"]) / meta["cell_m"]))
    c1 = min(meta["width"], int(math.ceil((x1 - meta["origin_x"]) / meta["cell_m"])))
    r0 = max(0, int((meta["origin_y"] - y1) / meta["cell_m"]))
    r1 = min(meta["height"], int(math.ceil((meta["origin_y"] - y0) / meta["cell_m"])))
    crop = img.crop((c0, r0, c1, r1))
    real = [meta["origin_x"] + c0 * meta["cell_m"], meta["origin_y"] - r1 * meta["cell_m"],
            meta["origin_x"] + c1 * meta["cell_m"], meta["origin_y"] - r0 * meta["cell_m"]]
    for width, q in ((1800, 62), (1600, 58), (1400, 55), (1200, 50), (1000, 45)):
        k = width / crop.width
        small = crop.resize((width, max(1, int(round(crop.height * k)))), Image.LANCZOS) if k < 1 else crop
        buf = io.BytesIO()
        small.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
        if buf.tell() <= max_bytes:
            break
    data = buf.getvalue()
    if len(data) > max_bytes:
        fail("background image could not be made small enough")
    return {"uri": "data:image/jpeg;base64," + base64.b64encode(data).decode("ascii"), "bounds": real,
            "bytes": len(data), "license": meta.get("license", "")}


def page_data(shot, days, status, updated, frame_aff, bg_bounds=None):
    strings, sidx = [], {}

    def S(s):
        s = s or ""
        if s not in sidx:
            sidx[s] = len(strings)
            strings.append(s)
        return sidx[s]

    entries, xs, ys = [], [], []
    for e in shot["entries"]:
        st = status.get(e["id"], {})
        rec = {"i": e["id"], "k": KINDS.index(e["kind"]), "p": PRIORITIES.index(e["priority"]),
               "m": METHODS.index(e["method"]), "e": e["est_photos"], "mn": e["min_photos"], "d": e.get("day"),
               "a": ACCESS.index(e["access"]), "b": S(e["best_time"].get("summary")), "an": S(e["access_notes"]),
               "f": e["example_framing"], "g": flat(e["centerline"]), "c": flat(e.get("camera_path"))}
        name = e.get("building_name") or e.get("name") or ""
        rec["n"] = S(name)
        if e.get("facade"):
            rec["fa"] = e["facade"]
        if e.get("normal"):
            rec["nr"] = [round(e["normal"][0], 3), round(e["normal"][1], 3)]
        for k, key in (("standoff_m", "so"), ("bearing_deg", "br"), ("height_m", "h"), ("length_m", "L"),
                       ("day_order", "o")):
            if e.get(k) is not None:
                rec[key] = e[k]
        if e.get("first_capture"):
            rec["fc"] = 1
        if e.get("approx"):
            rec["ap"] = 1
        if st.get("status", "not_started") != "not_started":
            rec["s"] = STATUSES.index(st["status"])
        if st.get("photos") is not None:
            rec["ph"] = st["photos"]
        if st.get("last"):
            rec["la"] = st["last"]
        if st.get("note"):
            rec["no"] = st["note"]
        entries.append(rec)
        xs += rec["g"][0::2]
        ys += rec["g"][1::2]
    unknown = sorted(set(status) - {e["id"] for e in shot["entries"]})
    buildings = []
    for b in shot.get("buildings", []):
        rings = [r for r in b.get("outlines", []) if len(r) >= 3]
        if not rings:
            continue
        c = np.mean(np.array(max(rings, key=len)), axis=0)
        buildings.append({"i": b["id"], "n": S(b.get("name") or ""), "g": [flat(r) for r in rings],
                          "c": [round(c[0], 1), round(c[1], 1)], "h": 1 if b.get("hero") else 0})
    routes = []
    for d in (days or {}).get("days", []):
        routes.append({"d": d["day"], "o": 1 if d["optional"] else 0, "r": flat(d["route"]), "h": d["hours"],
                       "ph": d["photos"], "s": d["session"], "w": d["walk_m"], "n": len(d["stops"])})
    labels = [{"t": a["name"] + (" (approx.)" if a["approx"] else ""),
               "c": [round(float(np.mean([p[0] for p in a["polygon"]])), 1),
                     round(float(np.mean([p[1] for p in a["polygon"]])), 1)]}
              for a in shot.get("hero_areas", [])]
    if bg_bounds is None:
        bg_bounds = [min(xs) - 60, min(ys) - 60, max(xs) + 60, max(ys) + 60] if xs else [-100, -100, 100, 100]
    return {"meta": {"generated": shot.get("generated"), "season": shot.get("season_date"), "status_updated": updated,
                     "days_generated": (days or {}).get("generated"), "frame": frame_aff, "bounds": bg_bounds,
                     "unknown_status_ids": unknown[:50], "statuses": STATUSES, "priorities": PRIORITIES,
                     "kinds": KINDS, "methods": METHODS, "access": ACCESS},
            "strings": strings, "entries": entries, "buildings": buildings, "routes": routes, "labels": labels}


def build_page(shot_path=None, status_path=None, days_path=None, out_path=None, with_background=True, root=None):
    """Build the page; returns (path, info). Safe to re-run (same inputs -> same file)."""
    shot = load_json(shot_path or SHOT_LIST)
    dp = Path(days_path or DAYS_JSON)
    days = load_json(dp) if dp.exists() else None
    status, updated = load_status(status_path)
    aff = frame_affine()
    xs, ys = [], []
    for e in shot["entries"]:
        for p in e["centerline"]:
            xs.append(p[0])
            ys.append(p[1])
    bounds = [min(xs) - 60, min(ys) - 60, max(xs) + 60, max(ys) + 60] if xs else [-100, -100, 100, 100]
    bg = None
    if with_background:
        bg = background(Path(root) if root else data_root(), bounds)
        bounds = bg["bounds"]
    data = page_data(shot, days, status, updated, aff, bounds)
    text = json.dumps(data, separators=(",", ":"), ensure_ascii=False)
    if len(text.encode("utf-8")) <= PLAIN_LIMIT:
        payload, enc = text.replace("</", "<\\/"), "json"
    else:
        payload = base64.b64encode(gzip.compress(text.encode("utf-8"), 9, mtime=0)).decode("ascii")
        enc = "gzip-base64"
    html = (TEMPLATE.replace("__ENC__", enc).replace("__DATA__", payload)
            .replace("__BG__", bg["uri"] if bg else "")
            .replace("__BGNOTE__", "Background: USDA NAIP 2021 (public domain)." if bg else "No background image."))
    out = Path(out_path or OUT)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(html, encoding="utf-8", newline="\n")
    size = out.stat().st_size
    info = {"bytes": size, "image_bytes": bg["bytes"] if bg else 0, "data_bytes": len(payload), "encoding": enc,
            "entries": len(data["entries"]), "unknown_status_ids": data["meta"]["unknown_status_ids"],
            "frame_max_err_m": aff["max_err_m"]}
    if size > MAX_PAGE:
        fail(f"page is {size} bytes, over {MAX_PAGE}")
    return out, info


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build", help="write capture/coverage/index.html")
    b.add_argument("--no-background", action="store_true", help="skip the NAIP background (no archive needed)")
    a = ap.parse_args(argv)
    if not SHOT_LIST.exists():
        fail(f"{SHOT_LIST} is missing; run shotlist.py build first")
    out, info = build_page(with_background=not a.no_background)
    print(f"wrote {out}: {info['bytes'] / 1e6:.2f} MB (image {info['image_bytes'] / 1e3:.0f} KB, data "
          f"{info['data_bytes'] / 1e3:.0f} KB {info['encoding']}), {info['entries']} entries, frame fit max error "
          f"{info['frame_max_err_m']} m")
    if info["unknown_status_ids"]:
        print(f"note: {len(info['unknown_status_ids'])} status ids are not in the shot list, e.g. "
              f"{info['unknown_status_ids'][:3]}")


TEMPLATE = r"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
<title>Campus coverage</title>
<style>
:root{--bg:#121212;--panel:#1d1d1f;--fg:#f2f2f2;--muted:#a8a8a8;--line:#3a3a3c;--accent:#56B4E9;
--st0:#c8c8c8;--st1:#E69F00;--st2:#0072B2;--st3:#CC79A7;--bld:rgba(255,255,255,.16);--hero:#F0E442}
*{box-sizing:border-box}
html,body{margin:0;height:100%;background:var(--bg);color:var(--fg);
font:14px/1.35 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;overflow:hidden;-webkit-text-size-adjust:100%}
#map{position:absolute;inset:0;touch-action:none;background:#1a1a1a}
#map svg{display:block;width:100%;height:100%;user-select:none;-webkit-user-select:none}
#top{position:absolute;left:0;right:0;top:0;z-index:5;background:rgba(18,18,18,.92);border-bottom:1px solid var(--line);
padding:6px 8px;display:flex;flex-wrap:wrap;gap:6px;align-items:center}
#top select,#top button{font:inherit;color:var(--fg);background:var(--panel);border:1px solid var(--line);
border-radius:8px;padding:5px 8px;min-height:32px}
#top button.on{border-color:var(--accent);box-shadow:inset 0 0 0 1px var(--accent)}
#top .grp{display:flex;gap:4px;flex-wrap:wrap}
#prog{position:absolute;right:8px;z-index:4;background:rgba(18,18,18,.9);border:1px solid var(--line);border-radius:10px;
padding:6px 8px;width:min(260px,calc(100vw - 16px));font-size:12px}
#prog .row{margin:3px 0}
#prog .bar{height:7px;background:#333;border-radius:4px;overflow:hidden;margin-top:2px}
#prog .bar i{display:block;height:100%;background:var(--st1)}
#prog .bar i.al{background:var(--st2)}
#prog{cursor:pointer}
#sheet{position:absolute;left:0;right:0;bottom:0;z-index:6;background:var(--panel);border-top:1px solid var(--line);
border-radius:14px 14px 0 0;max-height:62vh;overflow:auto;padding:10px 14px 18px;transform:translateY(105%);
transition:transform .2s ease}
#sheet.open{transform:none}
#sheet h2{font-size:16px;margin:2px 28px 6px 0}
#sheet .x{position:absolute;right:8px;top:6px;font-size:20px;background:none;border:0;color:var(--fg);min-width:36px;min-height:36px}
.chips span{display:inline-block;border:1px solid var(--line);border-radius:10px;padding:1px 7px;margin:0 4px 4px 0;font-size:12px}
.kv{display:grid;grid-template-columns:auto 1fr;gap:3px 10px;margin:6px 0}
.kv b{color:var(--muted);font-weight:600}
#legend{position:absolute;left:8px;z-index:4;background:rgba(18,18,18,.9);border:1px solid var(--line);border-radius:10px;
padding:6px 8px;font-size:12px;max-width:calc(100vw - 16px)}
#legend svg{vertical-align:middle}
#msg{position:absolute;left:50%;transform:translateX(-50%);bottom:12px;z-index:7;background:#000c;padding:6px 10px;
border-radius:8px;display:none;max-width:90vw}
.fac{fill:none;stroke-linecap:butt;stroke-linejoin:round}
.st-not_started{stroke:var(--st0);stroke-dasharray:2 3}
.st-captured{stroke:var(--st1);stroke-dasharray:8 3}
.st-aligned{stroke:var(--st2)}
.st-needs_reshoot{stroke:var(--st3);stroke-dasharray:9 3 2 3}
.blk{opacity:.45}
.bld{fill:var(--bld);stroke:rgba(255,255,255,.35);stroke-width:.6}
.bld.hero{fill:rgba(240,228,66,.22)}
.area{fill:rgba(255,255,255,.07)}
.route{fill:none;stroke:#fff;stroke-opacity:.85;stroke-width:2.5}
.route.opt{stroke-dasharray:6 4;stroke-opacity:.6}
.route.sel{stroke:#56B4E9;stroke-opacity:1;stroke-width:4}
.lbl{fill:#fff;paint-order:stroke;stroke:#000;stroke-width:3px;stroke-linejoin:round;font-weight:600;pointer-events:none}
.sel-ring{fill:none;stroke:#fff;stroke-width:9;stroke-opacity:.55}
.me{fill:#56B4E9;stroke:#fff;stroke-width:2}
.meacc{fill:rgba(86,180,233,.15);stroke:#56B4E9;stroke-width:1}
.hidden{display:none}
</style>
</head>
<body>
<div id="map"><svg id="svg" xmlns="http://www.w3.org/2000/svg">
<g id="vp"><image id="bg" preserveAspectRatio="none" href="__BG__"/><g id="gAreas"></g><g id="gB"></g><g id="gRoutes"></g>
<g id="gE"></g><g id="gSel"></g><g id="gMe"></g><g id="gL"></g></g></svg></div>
<div id="top">
<select id="fDay" aria-label="Day"></select>
<div class="grp" id="fPrio"></div>
<div class="grp" id="fSt"></div>
<button id="bRoutes">All routes</button>
<button id="bPaths" class="on">Paths</button>
<button id="bLoc">Locate me</button>
<button id="bHelp">?</button>
</div>
<div id="prog"></div>
<div id="legend" class="hidden"></div>
<div id="sheet"><button class="x" id="sx" aria-label="Close">&times;</button><div id="sc"></div></div>
<div id="msg"></div>
<script type="application/octet-stream" id="data" data-enc="__ENC__">__DATA__</script>
<script>
"use strict";
(function(){
var NS="http://www.w3.org/2000/svg";
var ST=["not_started","captured","aligned","needs_reshoot"], STN=["not started","captured","aligned","needs reshoot"];
var PR=["hero","normal","background"], KIND=["facade","courtyard","plaza","path","stairs","detail","swatch"];
var METH=["photo set","360 walk","detail set","swatch"], ACC=["public","restricted","blocked"];
var D=null, view={x:0,y:0,s:1}, W=0, H=0, sel=null;
var flt={day:"all",prio:[1,1,1],st:[1,1,1,1],routes:false,paths:true,prog:false};
function $(id){return document.getElementById(id);}
function el(tag,attrs,parent){var e=document.createElementNS(NS,tag);for(var k in attrs)e.setAttribute(k,attrs[k]);if(parent)parent.appendChild(e);return e;}
function esc(s){return String(s==null?"":s).replace(/[&<>"]/g,function(c){return{"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c];});}
function pts(g){var a=[];for(var i=0;i+1<g.length;i+=2)a.push(g[i].toFixed(1)+","+(-g[i+1]).toFixed(1));return a.join(" ");}
function msg(t,ms){var m=$("msg");m.textContent=t;m.style.display="block";clearTimeout(msg.t);msg.t=setTimeout(function(){m.style.display="none";},ms||3500);}
window.onerror=function(m){document.title="ERROR "+m;msg("Page error: "+m,8000);};
function load(){
  var node=$("data"), enc=node.getAttribute("data-enc"), txt=node.textContent;
  if(enc==="json")return Promise.resolve(JSON.parse(txt));
  if(typeof DecompressionStream==="undefined")return Promise.reject(new Error("this browser cannot unpack the data (needs DecompressionStream)"));
  var bin=atob(txt.trim()), u=new Uint8Array(bin.length);for(var i=0;i<bin.length;i++)u[i]=bin.charCodeAt(i);
  var ds=new Blob([u]).stream().pipeThrough(new DecompressionStream("gzip"));
  return new Response(ds).text().then(JSON.parse);
}
function S(i){return D.strings[i]||"";}
function stOf(e){return e.s||0;}
function visible(e){
  if(!flt.prio[e.p]||!flt.st[stOf(e)])return false;
  if(e.k===3&&!flt.paths)return false;
  if(flt.day==="all")return true;
  if(flt.day==="none")return e.d==null;
  return e.d===+flt.day;
}
function build(){
  var b=D.meta.bounds, bg=$("bg");
  bg.setAttribute("x",b[0]);bg.setAttribute("y",-b[3]);bg.setAttribute("width",b[2]-b[0]);bg.setAttribute("height",b[3]-b[1]);
  var gB=$("gB"), gE=$("gE"), gA=$("gAreas"), gR=$("gRoutes"), gL=$("gL");
  D.buildings.forEach(function(bd){
    var d="";bd.g.forEach(function(g){d+="M"+pts(g).replace(/ /g," L")+" Z ";});
    el("path",{d:d,"class":"bld"+(bd.h?" hero":"")},gB);
  });
  D.entries.forEach(function(e,ix){
    e.ix=ix;var cls="fac st-"+ST[stOf(e)]+(e.a===2?" blk":""), node;
    if(e.k===0){node=el("polyline",{points:pts(e.g),"class":cls,"stroke-width":e.p===0?7:5,"vector-effect":"non-scaling-stroke"},gE);}
    else if(e.k===1||e.k===2){
      el("polygon",{points:pts(e.g),"class":"area"},gA);
      node=el("polyline",{points:pts(e.g.concat(e.g.slice(0,2))),"class":cls,"stroke-width":3,"vector-effect":"non-scaling-stroke"},gE);}
    else if(e.k===3||(e.g.length>2)){node=el("polyline",{points:pts(e.g),"class":cls,"stroke-width":e.k===3?2.5:4,"vector-effect":"non-scaling-stroke"},gE);}
    else{node=el("circle",{cx:e.g[0],cy:-e.g[1],r:2.2,"class":cls,"fill":"rgba(0,0,0,.5)","stroke-width":3,"vector-effect":"non-scaling-stroke"},gE);}
    node.setAttribute("data-id",e.i);e.node=node;
  });
  D.routes.forEach(function(r){r.node=el("polyline",{points:pts(r.r),"class":"route"+(r.o?" opt":""),"vector-effect":"non-scaling-stroke"},gR);});
  D.labels.forEach(function(l){l.node=el("text",{x:l.c[0],y:-l.c[1],"class":"lbl","text-anchor":"middle"},gL);l.node.textContent=l.t;});
  D.buildings.forEach(function(bd){if(bd.h){var t=el("text",{x:bd.c[0],y:-bd.c[1],"class":"lbl","text-anchor":"middle"},gL);t.textContent=S(bd.n);D.labels.push({node:t,c:bd.c});}});
  // Filters.
  var fd=$("fDay"), days={}, o;
  D.entries.forEach(function(e){if(e.d!=null)days[e.d]=1;});
  o=document.createElement("option");o.value="all";o.textContent="All days";fd.appendChild(o);
  Object.keys(days).map(Number).sort(function(a,b){return a-b;}).forEach(function(d){
    var r=D.routes.filter(function(x){return x.d===d;})[0];
    o=document.createElement("option");o.value=d;o.textContent="Day "+d+(r&&r.o?" (opt.)":"")+(r?" - "+r.h.toFixed(1)+" h":"");fd.appendChild(o);});
  o=document.createElement("option");o.value="none";o.textContent="Not scheduled";fd.appendChild(o);
  fd.onchange=function(){flt.day=fd.value;apply();if(flt.day!=="all")fitDay();};
  chips($("fPrio"),PR,flt.prio);chips($("fSt"),STN,flt.st);
  tog("bRoutes","routes");tog("bPaths","paths");$("bRoutes").className="";
  $("bLoc").onclick=locate;$("bHelp").onclick=function(){$("legend").classList.toggle("hidden");};
  $("sx").onclick=closeSheet;
  legend();
}
function chips(box,names,arr){names.forEach(function(n,i){var b=document.createElement("button");b.textContent=n;b.className="on";
  b.onclick=function(){arr[i]=arr[i]?0:1;b.className=arr[i]?"on":"";apply();};box.appendChild(b);});}
function tog(id,key){var b=$(id);b.onclick=function(){flt[key]=!flt[key];b.className=flt[key]?"on":"";apply();};}
function apply(){
  D.entries.forEach(function(e){e.node.style.display=visible(e)?"":"none";});
  D.routes.forEach(function(r){var show=(flt.routes&&flt.day==="all")||+flt.day===r.d;r.node.style.display=show?"":"none";
    r.node.setAttribute("class","route"+(r.o?" opt":"")+(+flt.day===r.d?" sel":""));});
  progress();
}
function progress(){
  // Collapsed: one short line. Tap the box to show bars (entries and photos per priority).
  var h="<div class='row'><b>Progress</b> "+(flt.prog?"(captured + aligned) &#9650;":"&#9660;")+"</div>", short=[];
  PR.forEach(function(p,pi){
    var n=0,done=0,al=0,est=0,ph=0;
    D.entries.forEach(function(e){if(e.p!==pi||e.a===2)return;n++;est+=e.e;var s=stOf(e);
      if(s===1||s===2){done++;ph+=(e.ph!=null?e.ph:e.mn);}if(s===2)al++;});
    if(!n)return;
    short.push(p+" "+done+"/"+n);
    if(flt.prog)h+="<div class='row'>"+p+": "+done+"/"+n+" entries ("+al+" aligned), "+ph.toLocaleString()+"/"+
      est.toLocaleString()+" photos<div class='bar'><i style='width:"+(100*done/n).toFixed(1)+"%'></i></div>"+
      "<div class='bar'><i class='al' style='width:"+(100*Math.min(1,ph/Math.max(1,est))).toFixed(1)+"%'></i></div></div>";
  });
  if(!flt.prog)h+="<div class='row'>"+short.join(" &middot; ")+"</div>";
  var box=$("prog");box.innerHTML=h;box.onclick=function(){flt.prog=!flt.prog;progress();};
}
function legend(){
  var h="<b>Status</b> (color and line style)<br>";
  ST.forEach(function(s,i){h+="<svg width='44' height='12'><line x1='2' y1='6' x2='42' y2='6' class='fac st-"+s+"' stroke-width='5'/></svg> "+STN[i]+"<br>";});
  h+="Thick = hero, faded = blocked (not photographable). Areas = plazas/courtyards, thin = path legs, dots = details, stairs and swatches. "+
     "White lines = capture-day routes (dashed = optional). Tap anything for its shot. Shot list "+esc(D.meta.generated)+
     ", status "+esc(D.meta.status_updated||"none yet")+". __BGNOTE__ Locate me needs https or localhost on most phones; "+
     "campus conversion error "+D.meta.frame.max_err_m+" m plus your GPS error (often 5-15 m near buildings).";
  $("legend").innerHTML=h;
}
function resize(){var r=$("map").getBoundingClientRect();W=r.width;H=r.height;
  $("prog").style.top=($("top").offsetHeight+6)+"px";$("legend").style.top=($("top").offsetHeight+6)+"px";render();}
function render(){
  var vw=W/view.s, vh=H/view.s;
  $("svg").setAttribute("viewBox",(view.x-vw/2)+" "+(view.y-vh/2)+" "+vw+" "+vh);
  var fs=Math.max(4,13/view.s);
  D.labels.forEach(function(l){l.node.setAttribute("font-size",fs.toFixed(2));l.node.style.display=view.s>0.35?"":"none";});
}
function fit(x0,y0,x1,y1){view.x=(x0+x1)/2;view.y=-(y0+y1)/2;view.s=Math.min(W/Math.max(20,x1-x0),(H-60)/Math.max(20,y1-y0))*0.9;render();}
function fitDay(){
  var xs=[],ys=[];D.entries.forEach(function(e){if(visible(e))for(var i=0;i<e.g.length;i+=2){xs.push(e.g[i]);ys.push(e.g[i+1]);}});
  if(xs.length)fit(Math.min.apply(null,xs),Math.min.apply(null,ys),Math.max.apply(null,xs),Math.max.apply(null,ys));
}
function toWorld(sx,sy){return{x:view.x+(sx-W/2)/view.s,y:view.y+(sy-H/2)/view.s};}
function zoomAt(sx,sy,f){var w=toWorld(sx,sy);view.s=Math.max(0.05,Math.min(60,view.s*f));view.x=w.x-(sx-W/2)/view.s;view.y=w.y-(sy-H/2)/view.s;render();}
function pointers(){
  var svg=$("svg"), P={}, moved=0, last=null, startCount=0;
  function rel(e){var r=svg.getBoundingClientRect();return{x:e.clientX-r.left,y:e.clientY-r.top};}
  function pair(){var k=Object.keys(P);var a=P[k[0]],b=P[k[1]];return{d:Math.hypot(a.x-b.x,a.y-b.y),x:(a.x+b.x)/2,y:(a.y+b.y)/2};}
  svg.addEventListener("pointerdown",function(e){svg.setPointerCapture(e.pointerId);P[e.pointerId]=rel(e);
    var n=Object.keys(P).length;if(n===1){moved=0;startCount=1;}else{startCount=n;}last=n===2?pair():null;});
  svg.addEventListener("pointermove",function(e){if(!P[e.pointerId])return;var p=rel(e),q=P[e.pointerId];
    var n=Object.keys(P).length;
    if(n===1){view.x-=(p.x-q.x)/view.s;view.y-=(p.y-q.y)/view.s;moved+=Math.abs(p.x-q.x)+Math.abs(p.y-q.y);P[e.pointerId]=p;render();}
    else if(n===2){P[e.pointerId]=p;var c=pair();if(last&&last.d>0){view.x-=(c.x-last.x)/view.s;view.y-=(c.y-last.y)/view.s;zoomAt(c.x,c.y,c.d/last.d);}last=c;moved+=99;}
  });
  function up(e){if(!P[e.pointerId])return;var p=rel(e);delete P[e.pointerId];var n=Object.keys(P).length;
    if(n===0&&startCount===1&&moved<8)tap(p.x,p.y);last=n===2?pair():null;}
  svg.addEventListener("pointerup",up);svg.addEventListener("pointercancel",function(e){delete P[e.pointerId];last=null;});
  svg.addEventListener("wheel",function(e){e.preventDefault();var p=rel(e);zoomAt(p.x,p.y,Math.exp(-e.deltaY*0.0015));},{passive:false});
}
function segDist(px,py,ax,ay,bx,by){var dx=bx-ax,dy=by-ay,L=dx*dx+dy*dy,t=L?((px-ax)*dx+(py-ay)*dy)/L:0;t=Math.max(0,Math.min(1,t));
  return Math.hypot(px-ax-t*dx,py-ay-t*dy);}
function inside(px,py,g){var c=false;for(var i=0,j=g.length-2;i<g.length;j=i,i+=2){var xi=g[i],yi=g[i+1],xj=g[j],yj=g[j+1];
  if(((yi>py)!==(yj>py))&&(px<(xj-xi)*(py-yi)/(yj-yi)+xi))c=!c;}return c;}
function tap(sx,sy){
  var w=toWorld(sx,sy), X=w.x, Y=-w.y, tol=18/view.s, best=null, bd=1e18;
  D.entries.forEach(function(e){if(!visible(e))return;var g=e.g,d=1e18;
    if(g.length===2)d=Math.hypot(X-g[0],Y-g[1]);
    else for(var i=0;i+3<g.length;i+=2)d=Math.min(d,segDist(X,Y,g[i],g[i+1],g[i+2],g[i+3]));
    if((e.k===1||e.k===2)&&inside(X,Y,g))d=Math.min(d,tol*0.95);
    if(e.k!==0)d+=tol*0.1;
    if(d<bd){bd=d;best=e;}});
  if(best&&bd<=tol)open(best);else closeSheet();
}
function closeSheet(){$("sheet").classList.remove("open");$("gSel").innerHTML="";sel=null;}
function open(e){
  sel=e;var g=$("gSel");g.innerHTML="";
  if(e.g.length>2)el("polyline",{points:pts(e.g),"class":"sel-ring","vector-effect":"non-scaling-stroke"},g);
  else el("circle",{cx:e.g[0],cy:-e.g[1],r:14/view.s,"class":"sel-ring","vector-effect":"non-scaling-stroke"},g);
  var name=S(e.n)||e.i, title=esc(name)+(e.fa?" &middot; "+esc(e.fa):"");
  var h="<h2>"+title+"</h2><div class='chips'><span>"+KIND[e.k]+"</span><span>"+PR[e.p]+"</span><span>"+STN[stOf(e)]+
    "</span><span>"+ACC[e.a]+"</span><span>"+(e.d!=null?"day "+e.d+(e.o?" #"+e.o:""):"not scheduled")+"</span>"+(e.fc?"<span>first capture</span>":"")+(e.ap?"<span>approx. outline</span>":"")+"</div>";
  h+=diagram(e);
  h+="<div class='kv'><b>Method</b><span>"+METH[e.m]+"</span><b>Photos</b><span>"+e.e+" planned, "+e.mn+" to count as captured"+
    (e.ph?"; "+e.ph+" usable taken":"")+"</span><b>Best time</b><span>"+esc(S(e.b))+"</span><b>Access</b><span>"+esc(S(e.an))+"</span>";
  if(e.L!=null)h+="<b>Size</b><span>"+e.L.toFixed(1)+" m"+(e.h!=null?" &times; "+e.h.toFixed(1)+" m high":"")+(e.so!=null?", stand "+e.so+" m out":"")+(e.br!=null?", faces "+e.br.toFixed(0)+"&deg;":"")+"</span>";
  if(e.la)h+="<b>Last</b><span>"+esc(e.la)+"</span>";
  if(e.no)h+="<b>Note</b><span>"+esc(e.no)+"</span>";
  h+="<b>Framing</b><span>"+esc(e.f)+"</span><b>ID</b><span style='word-break:break-all'>"+esc(e.i)+"</span></div>";
  $("sc").innerHTML=h;$("sheet").classList.add("open");$("sheet").scrollTop=0;
}
function diagram(e){
  // Plan-view sketch: wall on top, camera path below, view cones of a 70 degree lens.
  var Wd=320,Hd=170,items=[],P=[];
  function add(a){a.forEach(function(p){P.push(p);});}
  var g=[],c=[],i;for(i=0;i+1<e.g.length;i+=2)g.push([e.g[i],e.g[i+1]]);for(i=0;i+1<e.c.length;i+=2)c.push([e.c[i],e.c[i+1]]);
  var nr=e.nr, cx=0, cy=0;g.forEach(function(p){cx+=p[0];cy+=p[1];});cx/=g.length;cy/=g.length;
  var dU,dV;
  if(nr&&e.k!==6){dV=[nr[0],nr[1]];dU=[-nr[1],nr[0]];}else{dU=[1,0];dV=[0,-1];}
  function loc(p){var x=p[0]-cx,y=p[1]-cy;return[x*dU[0]+y*dU[1],x*dV[0]+y*dV[1]];}
  var G=g.map(loc), C=c.map(loc);add(G);add(C);
  var so=e.so||0, cams=[];
  if(nr&&e.k===0&&C.length>=2){var a=C[0],b=C[C.length-1];cams=[a,[(a[0]+b[0])/2,(a[1]+b[1])/2],b];}
  else if(nr&&C.length>=1){cams=[C[0]];}
  else if(nr&&so){cams=[[0,so]];}
  cams.forEach(function(p){add([[p[0],p[1]],[p[0]-so*0.8,p[1]-so],[p[0]+so*0.8,p[1]-so]]);});
  if(nr)add(G.map(function(p){return[p[0],p[1]-4];}));
  if(P.length===1)add([[P[0][0]-15,P[0][1]-15],[P[0][0]+15,P[0][1]+15]]);
  var x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;P.forEach(function(p){x0=Math.min(x0,p[0]);x1=Math.max(x1,p[0]);y0=Math.min(y0,p[1]);y1=Math.max(y1,p[1]);});
  var k=Math.min((Wd-30)/Math.max(1,x1-x0),(Hd-30)/Math.max(1,y1-y0));
  function T(p){return[(15+(p[0]-x0)*k).toFixed(1),(15+(p[1]-y0)*k).toFixed(1)];}
  function line(a,attrs){return "<polyline points='"+a.map(function(p){return T(p).join(",");}).join(" ")+"' "+attrs+"/>";}
  var s="<svg width='100%' viewBox='0 0 "+Wd+" "+Hd+"' style='max-width:420px;background:#111;border-radius:8px;margin:4px 0'>";
  if(nr&&G.length>=2){var back=G.concat(G.slice().reverse().map(function(p){return[p[0],p[1]-4];}));
    s+=line(back,"fill='rgba(255,255,255,.12)' stroke='none'");}
  if(e.k===1||e.k===2)s+=line(G.concat([G[0]]),"fill='rgba(255,255,255,.08)' stroke='#ddd' stroke-width='2'");
  else if(G.length>=2)s+=line(G,"fill='none' stroke='#E69F00' stroke-width='4'");
  else{var q=T(G[0]);s+="<circle cx='"+q[0]+"' cy='"+q[1]+"' r='5' fill='#E69F00'/><circle cx='"+q[0]+"' cy='"+q[1]+"' r='"+(10*k).toFixed(1)+"' fill='none' stroke='#56B4E9' stroke-dasharray='4 3'/>";}
  if(C.length>=2)s+=line(C,"fill='none' stroke='#56B4E9' stroke-width='2' stroke-dasharray='6 4'");
  cams.forEach(function(p,j){var t=Math.tan(35*Math.PI/180)*so, A=T(p);
    [[0,0.5],[-0.6,0.18],[0.6,0.18]].forEach(function(r,m){if(m>0&&j!==1&&cams.length>1)return;
      var ang=r[0], dx=Math.sin(ang), dy=-Math.cos(ang), px=-dy, py=dx;
      var tip1=[p[0]+dx*so+px*t,p[1]+dy*so+py*t], tip2=[p[0]+dx*so-px*t,p[1]+dy*so-py*t];
      s+=line([p,tip1,tip2,p],"fill='rgba(86,180,233,"+r[1]+")' stroke='none'");});
    s+="<circle cx='"+A[0]+"' cy='"+A[1]+"' r='4' fill='#fff'/>";});
  var nx=dU[1], ny=dV[1], L=Math.hypot(nx,ny)||1;nx/=L;ny/=L;
  s+="<g transform='translate("+(Wd-30)+",26)'><line x1='0' y1='0' x2='"+(nx*12).toFixed(1)+"' y2='"+(ny*12).toFixed(1)+"' stroke='#fff' stroke-width='2'/>"+
     "<text x='"+(nx*19).toFixed(1)+"' y='"+(ny*19+4).toFixed(1)+"' fill='#fff' font-size='11' text-anchor='middle'>N</text></g>";
  if(nr&&so)s+="<text x='8' y='"+(Hd-6)+"' fill='#aaa' font-size='11'>dashed = walk line "+so+" m out; cones = 70&deg; view</text>";
  return s+"</svg>";
}
var meWatch=null;
function locate(){
  if(!navigator.geolocation){msg("No location in this browser.");return;}
  if(meWatch!=null){navigator.geolocation.clearWatch(meWatch);meWatch=null;$("gMe").innerHTML="";$("bLoc").className="";return;}
  $("bLoc").className="on";
  meWatch=navigator.geolocation.watchPosition(function(p){
    var f=D.meta.frame, dl=p.coords.longitude-f.lon0, dt=p.coords.latitude-f.lat0, b=[1,dl,dt,dl*dl,dl*dt,dt*dt], X=0, Y=0;
    for(var i=0;i<6;i++){X+=f.cx[i]*b[i];Y+=f.cy[i]*b[i];}
    var g=$("gMe");g.innerHTML="";el("circle",{cx:X,cy:-Y,r:Math.max(1,p.coords.accuracy||10),"class":"meacc","vector-effect":"non-scaling-stroke"},g);
    el("circle",{cx:X,cy:-Y,r:6/view.s,"class":"me","vector-effect":"non-scaling-stroke"},g);
    msg("You: ("+X.toFixed(0)+", "+Y.toFixed(0)+") campus m, GPS accuracy "+Math.round(p.coords.accuracy||0)+" m",2500);
  },function(err){msg("Location unavailable: "+err.message+" (pages opened as files usually cannot use GPS; use https or localhost).",7000);
    $("bLoc").className="";meWatch=null;},{enableHighAccuracy:true,maximumAge:5000,timeout:20000});
}
load().then(function(d){D=d;build();resize();var b=D.meta.bounds;fit(b[0],b[1],b[2],b[3]);apply();pointers();
  window.addEventListener("resize",resize);document.body.setAttribute("data-ready",D.entries.length);
}).catch(function(err){document.title="ERROR "+err.message;msg("Could not load data: "+err.message,20000);});
})();
</script>
</body>
</html>
"""

if __name__ == "__main__":
    main()

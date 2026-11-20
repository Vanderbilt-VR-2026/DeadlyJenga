"""Stage 3 facade review page: check and fix detected facade features by hand.

    python facade_review.py serve --building B_820346890            # open http://127.0.0.1:8765/
    python facade_review.py build --building B_820346890            # static page (Save downloads JSON)
    python facade_review.py apply --building B_820346890 edits.json # save a downloaded edit file

The page shows each facade's rectified image (from facade_measure.py) with the newest layout's
boxes on top. Click a box to select it; drag to move, drag the corner square to resize; change
its type or arch in the side panel; Delete removes it; "Add box" then drag on the image draws a
new one; "Accept" marks a box as checked without changing it. "Save" sends the edits to the local
server (serve), or downloads an edit file (static page) that "apply" saves.

Saving always writes a NEW layout version (facade_layout.save) with reviewed = true. Changed or
new features get "source": "manual"; features accepted unchanged keep their source and get
reviewed = true; deleted features are left out. The page and server run only on this machine
(127.0.0.1); nothing is uploaded.
"""
import argparse
import base64
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import facade_layout as fl  # noqa: E402
from common import data_root, fail, load_json  # noqa: E402

TOL = 1e-3


def apply_review(base, edits):
    """New layout dict from the base layout and the page's edits (not saved yet).

    edits = {"features": [{"id", "type", "rect", "arch", "depth_m"?, "confidence"?, "accepted"}]}
    A feature counts as unchanged when type, arch and rect (within 1 mm) match the base."""
    old = {f["id"]: f for f in base["features"]}
    out = []
    used = set()
    n_new = 0
    for e in edits["features"]:
        rect = [float(x) for x in e["rect"]]
        if rect[2] < rect[0]:
            rect[0], rect[2] = rect[2], rect[0]
        if rect[3] < rect[1]:
            rect[1], rect[3] = rect[3], rect[1]
        b = old.get(e.get("id"))
        same = (b is not None and b["type"] == e["type"] and b["arch"] == e.get("arch", "none")
                and all(abs(p - q) <= TOL for p, q in zip(b["rect"], rect)))
        if same:
            f = dict(b)
            f["reviewed"] = True
        else:
            fid = e.get("id") if e.get("id") and e.get("id") not in used else None
            if not fid:
                n_new += 1
                fid = f"M{n_new:03d}"
                while fid in old or fid in used:
                    n_new += 1
                    fid = f"M{n_new:03d}"
            depth = e.get("depth_m", b["depth_m"] if b else None)
            f = fl.new_feature(fid, e["type"], rect, arch=e.get("arch", "none"), depth_m=depth,
                               confidence=float(e.get("confidence", 1.0)), source="manual", reviewed=True)
        used.add(f["id"])
        out.append(f)
    lay = dict(base)
    lay["features"] = out
    lay["reviewed"] = True
    return lay


def save_review(building, facade, edits, root=None):
    """Apply edits to the newest layout and save the next version. Returns (path, summary)."""
    base = fl.load_latest(building, facade, root)
    if base is None:
        raise FileNotFoundError(f"no layout for {building}/{facade}")
    if edits.get("base_version") not in (None, base["version"]):
        raise ValueError(f"edits were made on v{edits.get('base_version')} but the newest is v{base['version']}")
    lay = apply_review(base, edits)
    path = fl.save(lay, root, source="pipeline/facade_review.py", license="derived (manual review)", stage=3)
    old_ids = {f["id"] for f in base["features"]}
    new = fl.load(path)
    summary = {"saved": path.name, "features": len(new["features"]),
               "kept": sum(1 for f in new["features"] if f["source"] != "manual"),
               "manual": sum(1 for f in new["features"] if f["source"] == "manual"),
               "deleted": len(old_ids - {f["id"] for f in new["features"]})}
    return path, summary


def page_data(building, root):
    root = Path(root or data_root())
    out = []
    for facade, lay in fl.load_building(building, root).items():
        img = lay["source"].get("rectified_image")
        if not img or not (root / img).exists():
            continue
        b64 = base64.b64encode((root / img).read_bytes()).decode("ascii")
        out.append({"facade": facade, "version": lay["version"], "reviewed": lay["reviewed"],
                    "W": lay["plane"]["width_m"], "H": lay["plane"]["height_m"], "ppm": lay["source"]["px_per_m"],
                    "image": "data:image/jpeg;base64," + b64, "features": lay["features"]})
    return out


PAGE = r"""<!doctype html><html><head><meta charset="utf-8"><title>Facade review</title>
<style>
body{font:14px system-ui,sans-serif;margin:0;background:#1d1f22;color:#e8e8e8}
header{padding:8px 12px;background:#2a2d31}main{display:flex;gap:12px;padding:12px}
#wrap{position:relative;flex:1;overflow:auto;border:1px solid #444}#img{display:block}
.box{position:absolute;border:2px solid #ffd400;box-sizing:border-box;cursor:move}
.box.sel{border-color:#00e5ff}.box.ok{border-style:dashed}.box .h{position:absolute;right:-6px;bottom:-6px;width:10px;height:10px;background:#00e5ff;cursor:nwse-resize}
.box span{position:absolute;left:0;top:-16px;font-size:11px;background:#000a;padding:0 3px;white-space:nowrap}
aside{width:260px}button,select{margin:2px 0;padding:4px 8px}label{display:block;margin-top:6px}
.note{color:#aaa;font-size:12px}
</style></head><body>
<header>Facade review: <b id="bld"></b> <select id="fac"></select> <span id="info" class="note"></span></header>
<main><div id="wrap"><img id="img"></div>
<aside>
<button id="add">Add box</button> <button id="acc">Accept</button> <button id="del">Delete</button>
<button id="accall">Accept all</button>
<label>Type <select id="type"></select></label><label>Arch <select id="arch"></select></label>
<div id="sel" class="note">no box selected</div>
<p><button id="save">Save new version</button></p><div id="msg" class="note"></div>
<p class="note">Rectified image from aligned street photos (Imagery (c) KartaView contributors, CC BY-SA 4.0).
For measuring only.</p></aside></main>
<script>
const DATA=__DATA__, BUILDING="__BUILDING__", TYPES=__TYPES__, ARCHES=__ARCHES__, SERVED=__SERVED__;
let F=0, feats=[], sel=-1, adding=false, drag=null, scale=2;
const img=document.getElementById('img'), wrap=document.getElementById('wrap');
document.getElementById('bld').textContent=BUILDING;
const fac=document.getElementById('fac');
DATA.forEach((d,i)=>{const o=document.createElement('option');o.value=i;o.textContent=d.facade+' (v'+d.version+(d.reviewed?', reviewed':'')+')';fac.appendChild(o)});
for(const [id,list] of [['type',TYPES],['arch',ARCHES]]){const s=document.getElementById(id);list.forEach(t=>{const o=document.createElement('option');o.value=o.textContent=t;s.appendChild(o)})}
function d(){return DATA[F]}
function toPx(r){const p=d().ppm*scale,H=d().H;return {x:r[0]*p,y:(H-r[3])*p,w:(r[2]-r[0])*p,h:(r[3]-r[1])*p}}
function toRect(x,y,w,h){const p=d().ppm*scale,H=d().H;return [x/p,H-(y+h)/p,(x+w)/p,H-y/p].map(v=>Math.round(v*1000)/1000)}
function load(i){F=i;feats=d().features.map(f=>({id:f.id,type:f.type,rect:f.rect.slice(),arch:f.arch,depth_m:f.depth_m,confidence:f.confidence,accepted:f.reviewed}));
 img.onload=()=>{img.width=img.naturalWidth*scale;draw()};img.src=d().image;sel=-1;
 document.getElementById('info').textContent=d().W.toFixed(2)+' x '+d().H.toFixed(2)+' m, '+feats.length+' boxes'}
function draw(){wrap.querySelectorAll('.box').forEach(b=>b.remove());
 feats.forEach((f,i)=>{const p=toPx(f.rect),b=document.createElement('div');b.className='box'+(i==sel?' sel':'')+(f.accepted?' ok':'');
  Object.assign(b.style,{left:p.x+'px',top:p.y+'px',width:p.w+'px',height:p.h+'px'});
  b.innerHTML='<span>'+f.id+' '+f.type+(f.arch!='none'?' '+f.arch:'')+'</span><div class="h"></div>';
  b.onmousedown=e=>{e.stopPropagation();sel=i;drag={i,mode:e.target.className=='h'?'size':'move',x:e.clientX,y:e.clientY,p};showSel();draw()};
  wrap.appendChild(b)})}
function showSel(){const s=document.getElementById('sel');if(sel<0){s.textContent='no box selected';return}
 const f=feats[sel];document.getElementById('type').value=f.type;document.getElementById('arch').value=f.arch;
 s.textContent=f.id+': u '+f.rect[0]+'..'+f.rect[2]+' m, v '+f.rect[1]+'..'+f.rect[3]+' m'+(f.accepted?' (accepted)':'')}
window.onmousemove=e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y,p=drag.p,f=feats[drag.i];
 if(drag.mode=='move')f.rect=toRect(p.x+dx,p.y+dy,p.w,p.h);else if(drag.mode=='size')f.rect=toRect(p.x,p.y,Math.max(4,p.w+dx),Math.max(4,p.h+dy));
 else{f.rect=toRect(Math.min(p.x,p.x+dx),Math.min(p.y,p.y+dy),Math.abs(dx)+1,Math.abs(dy)+1)}
 f.accepted=false;showSel();draw()};
window.onmouseup=()=>{drag=null};
wrap.onmousedown=e=>{if(!adding)return;const r=img.getBoundingClientRect(),x=e.clientX-r.left,y=e.clientY-r.top;
 feats.push({id:null,type:document.getElementById('type').value||'window',rect:toRect(x,y,1,1),arch:'none',depth_m:null,confidence:1,accepted:false});
 sel=feats.length-1;drag={i:sel,mode:'new',x:e.clientX,y:e.clientY,p:{x,y,w:1,h:1}};adding=false;draw()};
document.getElementById('add').onclick=()=>{adding=true;document.getElementById('msg').textContent='drag on the image to draw the box'};
document.getElementById('del').onclick=()=>{if(sel>=0){feats.splice(sel,1);sel=-1;showSel();draw()}};
document.getElementById('acc').onclick=()=>{if(sel>=0){feats[sel].accepted=true;showSel();draw()}};
document.getElementById('accall').onclick=()=>{feats.forEach(f=>f.accepted=true);draw()};
document.getElementById('type').onchange=e=>{if(sel>=0){feats[sel].type=e.target.value;feats[sel].accepted=false;draw()}};
document.getElementById('arch').onchange=e=>{if(sel>=0){feats[sel].arch=e.target.value;feats[sel].accepted=false;draw()}};
fac.onchange=e=>load(+e.target.value);
document.addEventListener('keydown',e=>{if(e.key=='Delete')document.getElementById('del').onclick()});
document.getElementById('save').onclick=async()=>{const body={building:BUILDING,facade:d().facade,base_version:d().version,features:feats};
 const msg=document.getElementById('msg');
 if(SERVED){const r=await fetch('/save',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
  msg.textContent=await r.text();if(r.ok)setTimeout(()=>location.reload(),800)}
 else{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(body,null,1)],{type:'application/json'}));
  a.download=BUILDING+'_'+d().facade+'_edits.json';a.click();msg.textContent='downloaded; run: python facade_review.py apply --building '+BUILDING+' <file>'}};
if(DATA.length)load(0);else document.getElementById('info').textContent='no facade with a rectified image';
</script></body></html>"""


def render_page(building, root=None, served=False):
    data = page_data(building, root)
    return (PAGE.replace("__DATA__", json.dumps(data)).replace("__BUILDING__", building)
            .replace("__TYPES__", json.dumps(list(fl.FEATURE_TYPES))).replace("__ARCHES__", json.dumps(list(fl.ARCH_TYPES)))
            .replace("__SERVED__", "true" if served else "false"))


def serve(building, port, root=None):
    class H(BaseHTTPRequestHandler):
        def do_GET(self):
            body = render_page(building, root, served=True).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.end_headers()
            self.wfile.write(body)

        def do_POST(self):
            if self.path != "/save":
                self.send_error(404)
                return
            n = int(self.headers.get("Content-Length", 0))
            try:
                edits = json.loads(self.rfile.read(n))
                if edits.get("building") != building:
                    raise ValueError("edits are for another building")
                path, summary = save_review(building, edits["facade"], edits, root)
                msg, code = f"saved {path.name}: {summary}", 200
            except Exception as ex:                      # report the problem to the page
                msg, code = f"not saved: {ex}", 400
            self.send_response(code)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.end_headers()
            self.wfile.write(msg.encode("utf-8"))

        def log_message(self, *a):
            pass

    srv = HTTPServer(("127.0.0.1", port), H)
    print(f"review page for {building}: http://127.0.0.1:{port}/  (Ctrl+C to stop)")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("serve")
    s.add_argument("--building", required=True)
    s.add_argument("--port", type=int, default=8765)
    b = sub.add_parser("build")
    b.add_argument("--building", required=True)
    b.add_argument("--out", help="page path (default reports/stage3/review/<building>.html in the archive)")
    a = sub.add_parser("apply")
    a.add_argument("--building", required=True)
    a.add_argument("edits")
    args = ap.parse_args(argv)
    root = data_root()
    if args.cmd == "serve":
        serve(args.building, args.port, root)
    elif args.cmd == "build":
        out = Path(args.out) if args.out else root / "reports" / "stage3" / "review" / f"{args.building}.html"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(render_page(args.building, root), encoding="utf-8")
        print(f"wrote {out}")
    else:
        edits = load_json(args.edits)
        if edits.get("building") != args.building:
            fail("edit file is for another building")
        path, summary = save_review(args.building, edits["facade"], edits, root)
        print(f"saved {path}: {summary}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

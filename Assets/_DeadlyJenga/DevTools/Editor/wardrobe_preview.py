"""Local browser controls for the Unity wardrobe camera."""
import argparse
import json
import math
import secrets
import threading
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

SKINS = {"Default", "Rabbit", "Clownfish", "CandyCorn"}

PAGE = """<!doctype html>
<html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Deadly Jenga Wardrobe</title>
<style>
:root {color-scheme:dark;font:16px "Segoe UI",sans-serif;color:#ecf8f5;background:#101b22}
* {box-sizing:border-box}
body {max-width:790px;margin:auto;padding:24px clamp(11px,4vw,28px)}
header,nav {display:flex;align-items:center;gap:10px;flex-wrap:wrap}
header {justify-content:space-between}
h1 {margin:0;font-size:clamp(22px,5vw,34px);letter-spacing:-.04em}
nav {margin:13px 0}
button,select {background:#253841;color:inherit;border:1px solid #40535a;border-radius:9px;padding:7px 12px;font:inherit}
button {cursor:pointer} select {flex:1;min-width:125px}
:focus-visible {outline:3px solid #78ddc7;outline-offset:2px}
figure {background:#17313e;margin:0;border:1px solid #3b5864;border-radius:18px;overflow:hidden}
#avatar {display:block;width:100%;aspect-ratio:1;touch-action:none;cursor:grab}
#avatar:active {cursor:grabbing}
#avatar:not([src]) {visibility:hidden}
#status {color:#aac2c7;font-size:13px}
#revision {color:#89d5c5;font:12px monospace}
</style>
<header><h1>Wardrobe</h1><span id="revision">Unity · __REVISION__</span></header>
<nav aria-label="Wardrobe controls">
<select id="skin" aria-label="Skin"><option>Rabbit</option><option>Clownfish</option>
<option value="CandyCorn">Candy Corn</option><option>Default</option></select>
<select id="environment" aria-label="Lighting and environment"><option value="studio">Studio</option>
<option value="castle">Castle</option></select>
<button id="reset">Reset view</button><button id="stop">Stop preview</button>
</nav>
<figure><img id="avatar" tabindex="0" draggable="false" alt="Unity wardrobe preview"></figure>
<p id="status" role="status">Starting Unity…</p>
<script>
const skin = document.querySelector('#skin'), avatar = document.querySelector('#avatar');
const environment = document.querySelector('#environment');
const status = document.querySelector('#status'), token = __TOKEN__;
const guidance = 'Drag or use arrow keys to orbit. Scroll or use + and − to zoom.';
let yaw=155, pitch=9, zoom=1, drag, dirty=true, busy=false, stopped=false, imageUrl;
const clamp = (v,min,max) => Math.max(min,Math.min(max,v));
const render = () => { dirty=true; };
const request = (path,body) => fetch(path,{method:'POST',headers:{
  'Content-Type':'application/json','X-Wardrobe-Token':token},body});
setInterval(async () => {
  if (busy || !dirty || stopped) return;
  busy=true; dirty=false;
  try {
    const selection=skin.value;
    const response=await request('/frame',JSON.stringify({skin:selection,environment:environment.value,yaw,pitch,zoom}));
    if (!response.ok) {status.textContent=await response.text(); dirty=response.status===503; return;}
    const next=URL.createObjectURL(await response.blob());
    if (imageUrl) URL.revokeObjectURL(imageUrl);
    imageUrl=next; avatar.src=next; avatar.alt=selection+' rendered by Unity';
    status.textContent=guidance;
  } catch {status.textContent='The Unity preview disconnected.';}
  finally {busy=false;}
},110);
avatar.addEventListener('pointerdown',e => {
  if(e.button!==0)return;
  drag={x:e.clientX,y:e.clientY}; avatar.setPointerCapture(e.pointerId);
});
avatar.addEventListener('pointermove',e => {
  if(!drag)return;
  yaw=(yaw+(e.clientX-drag.x)*.55)%360;
  pitch=clamp(pitch-(e.clientY-drag.y)*.45,-55,70);
  drag={x:e.clientX,y:e.clientY}; render();
});
avatar.addEventListener('lostpointercapture',() => {drag=null;});
avatar.addEventListener('wheel',e => {
  e.preventDefault(); zoom=clamp(zoom*Math.exp(e.deltaY*.0015),.55,2); render();
},{passive:false});
avatar.addEventListener('keydown',e => {
  switch(e.key){
  case 'ArrowLeft':yaw-=10;break; case 'ArrowRight':yaw+=10;break;
  case 'ArrowUp':pitch=clamp(pitch+8,-55,70);break; case 'ArrowDown':pitch=clamp(pitch-8,-55,70);break;
  case '+':case '=':zoom=clamp(zoom/1.12,.55,2);break; case '-':zoom=clamp(zoom*1.12,.55,2);break;
  default:return;
  }
  e.preventDefault(); render();
});
skin.addEventListener('change',render);
environment.addEventListener('change',render);
document.querySelector('#reset').onclick=()=>{yaw=155;pitch=9;zoom=1;render();};
document.querySelector('#stop').onclick=async e=>{
  e.currentTarget.disabled=true; stopped=true;
  try {const response=await request('/stop'); status.textContent=await response.text();}
  catch {status.textContent='The local preview disconnected.';}
};
</script></html>
"""


def write_json(path, data):
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(data), encoding="utf-8")
    temporary.replace(path)


class PreviewServer(ThreadingHTTPServer):
    def __init__(self, folder, revision, port):
        super().__init__(("127.0.0.1", port), Request)
        self.folder = folder
        self.host = f"127.0.0.1:{self.server_port}"
        self.token = secrets.token_urlsafe(24)
        self.page = PAGE.replace("__REVISION__", revision[:10]).replace("__TOKEN__", json.dumps(self.token))
        self.render_lock = threading.Lock()


class Request(BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(8)

    def reply(self, status, body, mime="text/plain; charset=utf-8"):
        if isinstance(body, str):
            body = body.encode()
        self.send_response(status)
        self.send_header("Content-Type", mime)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Security-Policy",
                         "default-src 'none'; img-src blob:; connect-src 'self'; "
                         "script-src 'unsafe-inline'; style-src 'unsafe-inline'; frame-ancestors 'none'")
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_GET(self):
        if self.headers.get("Host") != self.server.host:
            self.reply(403, "Use the local preview URL.")
        elif self.path == "/":
            self.reply(200, self.server.page, "text/html; charset=utf-8")
        else:
            self.reply(404, "Preview path not found.")

    def do_POST(self):
        if (self.headers.get("Host") != self.server.host or
                self.headers.get("X-Wardrobe-Token") != self.server.token):
            self.reply(403, "Open the local preview first.")
        elif self.path == "/stop":
            (self.server.folder / "stop").touch()
            self.reply(200, "Preview stopped.")
            threading.Thread(target=self.server.shutdown, daemon=True).start()
        elif self.path == "/frame":
            self.render_frame()
        else:
            self.reply(404, "Preview path not found.")

    def render_frame(self):
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if not 0 < size <= 1024:
                raise ValueError()
            data = json.loads(self.rfile.read(size))
            orbit = {key: float(data[key]) for key in ("yaw", "pitch", "zoom")}
            skin = data["skin"]
            environment = data.get("environment", "studio")
            if skin not in SKINS or environment not in ("studio", "castle") or not all(math.isfinite(v) for v in orbit.values()):
                raise ValueError()
        except (ValueError, KeyError, TypeError, TimeoutError):
            self.reply(400, "Invalid skin or orbit.")
            return

        folder = self.server.folder
        if (folder / "unity-error.txt").exists():
            self.reply(500, "Unity reported a preview error. See the local preview log.")
            return
        if not (folder / "unity-ready.txt").exists():
            self.reply(503, "Waiting for the Unity preview…")
            return
        if not self.server.render_lock.acquire(blocking=False):
            self.reply(503, "Rendering the previous frame…")
            return
        try:
            request_id = uuid.uuid4().hex
            output = folder / f"frame-{request_id}.png"
            write_json(folder / "orbit.json", {"id": request_id, "skin": skin, "environment": environment, **orbit})
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline and not output.exists() and not (folder / "unity-error.txt").exists():
                time.sleep(.025)
            if output.exists():
                self.reply(200, output.read_bytes(), "image/png")
                output.unlink()
            else:
                self.reply(503, "Waiting for a frame from Unity…")
        finally:
            self.server.render_lock.release()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--port", type=int, default=0)
    args = parser.parse_args()
    if len(args.revision) != 40 or any(c not in "0123456789abcdef" for c in args.revision):
        parser.error("revision must be the loaded Git commit")
    folder = args.folder.resolve()
    folder.mkdir(parents=True, exist_ok=True)
    with PreviewServer(folder, args.revision, args.port) as server:
        url = f"http://{server.host}/"
        write_json(folder / "server.json", {"url": url, "revision": args.revision})
        print(url, flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()

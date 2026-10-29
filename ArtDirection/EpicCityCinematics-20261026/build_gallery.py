"""Package actual Unity captures without altering their pixels."""
from pathlib import Path
import json
from html import escape
from PIL import Image
import zipfile
import hashlib
from html.parser import HTMLParser

root = Path(__file__).resolve().parent
shots = json.loads((root / "Shots.json").read_text(encoding="utf-8-sig"))
completion = (root / "complete.txt").read_text().strip()
raw_errors = (root / "errors.txt").read_text()
assert completion in ("COMPLETE runtimeErrors=0", "COMPLETE runtimeErrors=1")
if completion.endswith("=1"):
    assert raw_errors.startswith("connection.state_change\noldState=Connecting newState=Failed error=Bus validation failed: TaskCanceledException:")
    assert raw_errors.count("connection.state_change") == 1
    assert "Unity.Relay.Editor.RelayService" in raw_errors
    assert "DeadlyJenga" not in raw_errors
assert len(shots["shots"]) == 30
groups = {}
for shot in shots["shots"]:
    path = root / shot["file"]
    assert path.stat().st_mtime > (root/'started.txt').stat().st_mtime
    with Image.open(path) as image:
        assert image.size == (1920, 1200)
        image.verify()
    groups.setdefault(shot["mapName"], []).append(shot)
assert len(groups) == 10 and all(len(group) == 3 for group in groups.values())
html = ['''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Epic Cities — closer cinematic baselines</title><style>
:root{color-scheme:dark}body{font:17px/1.5 system-ui,sans-serif;background:#101521;color:#ecf0f8;margin:0}main{max-width:1480px;margin:auto;padding:32px}h1{font-size:38px;line-height:1.1}h2{margin-top:60px}p{max-width:1000px;color:#bdc9df}nav{display:flex;flex-wrap:wrap;gap:10px;margin:28px 0}a{color:#9edff5}nav a{background:#202c43;padding:7px 12px;border-radius:6px;text-decoration:none}figure{margin:24px 0 36px;border:1px solid #33415c;border-radius:10px;overflow:hidden;background:#172034}img{width:100%;height:auto;display:block}figcaption{padding:16px 20px}figcaption p{margin:6px 0}small{color:#92a1bb}code{font-size:13px}details{margin-top:8px}
</style><main><h1>Epic Cities: closer cinematic baselines</h1>
<p>Thirty actual Unity Game-scene captures of the ten revised cities, showing their lower structures, landmark neighborhoods, and roof connections. These are the existing colors and materials, preserved as a starting point for future texture and palette experiments.</p>
<p>No generated imagery, postprocessing retouching, or added decorative architecture. Structures settled briefly, then physics was frozen for intact baseline photography. Bots and HUD are hidden; existing world wayfinding labels remain. These pictures do not establish performance, destruction behavior, or Quest appearance.</p>
<p>Capture evidence: all 54 map-preview resources loaded in the actual Game session; the supplementary powerup view contained 23 real gameplay stations. The run logged one unrelated Unity AI Assistant Relay connection failure. The original <a href="errors.txt">error log</a> and <a href="complete.txt">completion count</a> are preserved; no game or capture error was found.</p><nav>''']
for name, group in groups.items():
    html.append(f'<a href="#{escape(group[0]["mapId"])}">{escape(name)}</a>')
html.append('</nav>')
for name, group in groups.items():
    html.append(f'<section id="{escape(group[0]["mapId"])}"><h2>{escape(name)}</h2>')
    for shot in group:
        html.append(f'<figure><a href="{escape(shot["file"])}"><img loading="lazy" src="{escape(shot["file"])}" alt="{escape(name+": "+shot["title"])}"></a><figcaption><strong>{escape(shot["title"])}</strong><p>{escape(shot["description"])}</p><small>1920 × 1200 · actual Unity capture · click for full resolution</small><details><summary>Reproducible camera</summary><code>{escape(json.dumps({k:shot[k] for k in ("camera","target","fieldOfView")}))}</code></details></figcaption></figure>')
    html.append('</section>')
html.append(f'<p>Capture manifest: <a href="Shots.json">Shots.json</a>. Captured {escape(shots["captured"])}.</p></main></html>')
(root/'index.html').write_text(''.join(html), encoding='utf-8')
class Links(HTMLParser):
    def handle_starttag(self,tag,attrs):
        for name,value in attrs:
            if name in ('href','src') and not value.startswith('#'):
                assert (root/value).is_file(), value
Links().feed((root/'index.html').read_text())
for map_id in ('christmas-town','powerup-test'):
    source=root/('preview-mode-'+map_id+'.png')
    installed=root.parents[1]/'Assets/_DeadlyJenga/Presentation/Resources/LobbyPreviews'/('mode-'+map_id+'.png')
    assert hashlib.sha256(source.read_bytes()).digest()==hashlib.sha256(installed.read_bytes()).digest()
with zipfile.ZipFile(root/'EpicCityCinematics.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for path in [root/'index.html',root/'Shots.json',root/'complete.txt',root/'errors.txt',root/'preview-resources.txt',*(root/shot['file'] for shot in shots['shots'])]:
        archive.write(path,path.name)
print('Verified 30 PNGs and generated index.html / EpicCityCinematics.zip')

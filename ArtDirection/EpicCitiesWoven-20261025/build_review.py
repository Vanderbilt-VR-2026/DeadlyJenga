from pathlib import Path
from html import escape
import hashlib,json,re,shutil,zipfile

root=Path('ArtDirection/EpicCitiesWoven-20261025')
logs=Path('Logs/Validation')
marker=logs/'EpicCityRuntime-started.txt'
done=logs/'EpicCityRuntime-complete.txt'
assert marker.read_text(encoding='utf-8-sig').startswith('Version=5')
assert done.stat().st_mtime>=marker.stat().st_mtime and 'failures=0 runtimeErrors=0' in done.read_text(encoding='utf-8-sig')
native=(logs/'EpicWovenVerified/native.txt').read_text(encoding='utf-8-sig')
assert 'COMPLETE issues=0' in native
old=json.loads(Path('ArtDirection/EpicCitiesBuilt-20261025/Counts.json').read_text(encoding='utf-8'))
captures=root/'Captures';captures.mkdir(exist_ok=True)
evidence=root/'Evidence';evidence.mkdir(exist_ok=True)
shutil.copy2(logs/'EpicWovenVerified/native.txt',evidence/'NativePhysics.txt')
shutil.copy2(done,evidence/done.name)
shutil.copy2(logs/'EpicCityBuild.txt',evidence/'AssetBuild.txt')
cards=[];rows=[];counts=[]
for c in old:
 n=c['number'];report=logs/f'EpicCity-{n:02}-game.txt'
 assert report.stat().st_mtime>=marker.stat().st_mtime
 text=report.read_text(encoding='utf-8-sig');assert 'REVIEW' not in text and 'validRoundtrip=True' in text and 'Targeted 16-second wave exposure' in text
 match=re.search(r'Bodies=(\d+) shapes=(\d+) routes=(\d+) elevated=(\d+) lowerStructures=(\d+)',text)
 assert match
 bodies,shapes,routes,elevated,lower=map(int,match.groups())
 counts.append(dict(number=n,name=c['name'],bodies=bodies,shapes=shapes,routes=routes,elevated=elevated,lower=lower))
 shutil.copy2(report,evidence/report.name)
 previous=Path(f'ArtDirection/EpicCitiesBuilt-20261025/Captures/EpicCity-{n:02}-intact.png')
 shutil.copy2(previous,captures/f'{n:02}-before.png')
 for state in ('intact','tsunami','impact'):
  source=logs/f'EpicCity-{n:02}-{state}.png';assert source.stat().st_mtime>=marker.stat().st_mtime
  shutil.copy2(source,captures/f'{n:02}-{state}.png')
 concept=next(Path('ArtDirection/EpicCityReview-20261025/Images').glob(f'{n:02}-*.png'))
 shutil.copy2(concept,captures/f'{n:02}-concept.png')
 name=escape(c['name'])
 figures=''.join(f'<figure><a href="Captures/{n:02}-{s}.png"><img loading="lazy" src="Captures/{n:02}-{s}.png" alt="{name}: {label}"></a><figcaption>{label}</figcaption></figure>' for s,label in [('before','Previous sparse build'),('intact','Revised: physical lower structures woven in'),('tsunami','Targeted 16-second tsunami exposure, 12 bots'),('impact','Separate stronger demolition after reset')])
 cards.append(f'<article id="city-{n}"><h2>{n:02} · {name}</h2><p>{lower} added lower buildings · {c["elevated"]} → {elevated} elevated connections · {c["bodies"]} → {bodies} moving bodies</p><div class="shots">{figures}</div><details><summary>Approved concept reference — illustration</summary><img loading="lazy" src="Captures/{n:02}-concept.png" alt="Original concept illustration for {name}"></details><details><summary>Actual runtime checks</summary><pre>{escape(text)}</pre></details></article>')
 rows.append(f'<tr><th><a href="#city-{n}">{name}</a></th><td>{lower}</td><td>{c["elevated"]} → {elevated}</td><td>{c["bodies"]} → {bodies}</td><td>{shapes}</td></tr>')
page='''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Deadly Jenga · Woven cities correction</title><style>body{margin:0;background:#111b2c;color:#ecf2ff;font:17px/1.6 system-ui,sans-serif}main{max-width:1550px;margin:auto;padding:45px 5vw}h1{font-size:46px;line-height:1.15}a{color:#9df3d5}.note,details{padding:18px;background:#213149}.note{border-left:4px solid #ffc77b}.shots{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}img{width:100%;height:auto}figcaption{font-size:14px;color:#c7d4ed}article{padding-top:30px;border-top:1px solid #40516c;margin-top:40px}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:12px;border-bottom:1px solid #40516c}.scroll{overflow:auto}pre{white-space:pre-wrap;font:14px/1.6 monospace}details{margin-top:20px}summary{cursor:pointer}@media(max-width:800px){.shots{grid-template-columns:1fr}h1{font-size:34px}}</style></head><body><main><header><p>DEADLY JENGA · CORRECTED BUILT MAPS</p><h1>A lower city woven through the skyline</h1><p>The previous pass left tower clusters separated by broad bare spaces. These revisions add physical folded terraces, open market arcades, split courtyard pavilions, branching mesa walks and stepped porticos between them. Low roofs connect to neighboring low roofs and tower floors; ground passages remain beneath and between the structures.</p><p>Choose <b>Epic connected cities</b> in the lobby or demo map picker. Existing IDs are retained; these are revised versions of the same ten maps.</p><p><a href="EpicCitiesWovenReview.zip">Download the comparison gallery and evidence</a></p><p class="note">All pictures below are actual Unity game captures. Each before/after pair uses the same camera. The 16-second wave exposure is a targeted geometry and behavior check with 12 bots; it is not the earlier full-wave 32-bot benchmark, and its timings should not be compared with that run. Headset, live multiplayer and competitive mode balance remain unverified.</p></header><div class="scroll"><table><thead><tr><th>Map</th><th>Added lower structures</th><th>Elevated connections</th><th>Moving bodies</th><th>Collision shapes</th></tr></thead><tbody>ROWS</tbody></table></div><p>One compact lower building moves as a rigid section, including its stairs and folded circulation. This limits additional body count; its floors do not fracture individually. The original skyline and selected fall courts remain. Lower buildings occupy collision-checked pockets rather than every square metre of the island.</p><p>Native checks cover initial inter-body overlaps, 15-second settling, full-size stair clearance, replica state and demolition. Production checks cover real player ascent, bot activity, short tsunami exposure and map reload/reset. <a href="Evidence/NativePhysics.txt">Read the native report</a>.</p>CARDS</main></body></html>'''.replace('ROWS',''.join(rows)).replace('CARDS',''.join(cards))
(root/'index.html').write_text(page,encoding='utf-8')
(root/'Counts.json').write_text(json.dumps(counts,indent=2),encoding='utf-8')
(evidence/'SourceSHA256.json').write_text(json.dumps({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('Assets/_DeadlyJenga/World/Maps/Scripts').glob('EpicCity*.cs')},indent=2),encoding='utf-8')
with zipfile.ZipFile(root/'EpicCitiesWovenReview.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in root.rglob('*'):
  if p.is_file() and p.suffix!='.zip':z.write(p,p.relative_to(root))
print(json.dumps(counts,indent=2))

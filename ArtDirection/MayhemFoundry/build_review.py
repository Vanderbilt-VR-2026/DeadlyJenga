from pathlib import Path
import re,json,html,shutil,hashlib,zipfile
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parents[2]
out=Path(__file__).parent
logs=root/'Logs/Validation'
native=logs/'FoundryNative'
captures=out/'Captures';captures.mkdir(exist_ok=True)
def sections(path):
    text=path.read_text(encoding='utf-8-sig')
    return [p for p in re.split(r'(?m)(?=^DISTRICT \d+ )',text) if p.startswith('DISTRICT')]
accepted={}
for file in ['accepted-ten-before-mezzanines.txt','accepted-crown-mezzanines.txt','landing-separation-acceptance.txt']:
    for section in sections(native/file):accepted[int(re.match(r'DISTRICT (\d+)',section)[1])]=section
for section in sections(native/'harbor-solver-comparison.txt'):
    if 'solver=12/8' in section.splitlines()[0]:accepted[9]=section
for section in sections(native/'integrated-landings-acceptance.txt'):
    accepted[int(re.match(r'DISTRICT (\d+)',section)[1])]=section
for section in sections(native/'harbor-entry-acceptance.txt'):
    accepted[int(re.match(r'DISTRICT (\d+)',section)[1])]=section
for section in sections(native/'final-pads-acceptance.txt'):
    accepted[int(re.match(r'DISTRICT (\d+)',section)[1])]=section
for section in sections(native/'final-harbor-entry-acceptance.txt'):
    accepted[int(re.match(r'DISTRICT (\d+)',section)[1])]=section
names=['Interleaved Bookends','Branch Mesa Forest','Broken-Ring Canyon','Folded Ribbon Gorge','Reciprocal Crown Bazaar','Skyrail Shipyard','Domino Portal Quarter','Keystone Vault District','Rolling Borough','Counterweight Sky Harbor']
ideas=[
 'Balanced three-floor combs, with open cantilever bays, flush exterior spine faces, and alternating ramp lanes. A fallen comb becomes a bridge with upright cover.',
 'Three broad branches per crown, supported by separate legs. Multiple stacked crowns form shaded courts and elevated forks. One approach flight per crown avoids neighboring legs.',
 'Rotating C terraces around open atria. Fixed corner supports and exterior flights preserve the void; bridges attach to clear side galleries rather than solid corner piers.',
 'Rigid folded plates form ridges, gullies and covered spaces. Slender loose links cross between crests. Removing a support changes the slope and the routes beneath it.',
 'Eight mutually bearing beams per crown, plus internal mezzanine rings on inward pier landings. The exterior climb faces remain clear. No glued joints or magnetic stabilization.',
 'Two-deck rigid trusses with a central upper-deck opening for the rising internal flight. Separate piers and loose cross-links turn falling corridors into covered ramps.',
 'Tall portal pages with open frames, alternating front/back ramp lanes, and H-shaped street junctions. Shared junction bodies avoid overlapping independent bridge branches.',
 'Five-piece compression arches with flared bearing feet, movable tied thrust cradles and raised keystone seats. The deck loads the keystone so removing it produces a real failure.',
 'Hollow octagonal drums with two internal floors, faceted shells and loose cradles. A moved cradle lets a drum tip; lateral impacts can change which surfaces act as floors.',
 'Eight bascule leaves hinged to dynamic frames, loose heel weights and lower landing blocks. Separated rear stair lanes reach the leaves; removing a weight lets gravity lower the nose.'
]
facts=[]
for i in range(10):
    block=accepted[i]
    counts=re.search(r'bodies=(\d+) (?:colliders|shapes)=(\d+)',block)
    settle=re.search(r'SETTLE 15s maxDrift=([\d.]+)m maxTilt=([\d.]+)deg rmsSpeed=([\d.]+)m/s moved>.6m=(\d+) awake=(\d+)/(\d+)',block)
    assert counts and settle,(i,block)
    overlap=int(re.search(r'overlaps>.04m=(\d+)',block)[1])
    assert overlap==0 and int(settle[4])==0 and int(settle[5])==0,(i,settle.groups())
    facts.append(dict(id=i,name=names[i],bodies=int(counts[1]),colliders=int(counts[2]),maxDrift=float(settle[1]),maxTilt=float(settle[2]),rmsSpeed=float(settle[3]),awake=int(settle[5]),solver='32/8' if i==7 else '12/16' if i==9 else '12/4',idea=ideas[i]))
assert sum(f['bodies'] for f in facts)==669
assert sum(f['colliders'] for f in facts)==2096
summary='Final native acceptance combines the common ten-district run with targeted rechecks after mezzanine and landing changes. Each section is an actual Unity PhysX run with the production layout/member code, ordinary dynamic bodies and a flat test foundation. Materials are stand-ins; live-game captures use the real project. Native step timings are not FPS.\n\n'+'\n'.join(accepted[i] for i in range(10))
(out/'NativeAcceptance.txt').write_text(summary,encoding='utf-8')
physics=root/'Assets/_DeadlyJenga/World/Maps/Scripts'
sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in physics.glob('Mayhem*.cs')}
(out/'Facts.json').write_text(json.dumps({'map':'mayhem-foundry','bodies':669,'colliders':2096,'fixedGroundColliders':1,'hinges':8,'districts':facts,'sourceSHA256':sources},indent=2),encoding='utf-8')
for i in range(10):
    game=logs/f'Foundry-{i:02d}-intact.png'
    if game.exists():shutil.copy2(game,captures/f'{i:02d}-intact.png')
    release=native/f'district-{i:02d}-released.png'
    if release.exists():shutil.copy2(release,captures/f'{i:02d}-release.png')
for state in ['intact','tsunami']:
    p=logs/f'Foundry-island-{state}.png'
    if p.exists():shutil.copy2(p,captures/f'island-{state}.png')
game=(logs/'MayhemFoundry-game.txt').read_text(encoding='utf-8-sig') if (logs/'MayhemFoundry-game.txt').exists() else 'Production checks pending.'
assert 'COMPLETE runtimeErrors=0' in game and 'FAIL ' not in game and 'full route completed=True' in game,game
(out/'GameValidation.txt').write_text(game,encoding='utf-8')
esc=html.escape
rows=''.join(f'<tr><td><a href="#d{f["id"]}">{f["id"]+1:02d} {esc(f["name"])}</a></td><td>{f["bodies"]}</td><td>{f["colliders"]}</td><td>{f["solver"]}</td><td>{f["maxDrift"]:.3f} m</td><td>0</td></tr>' for f in facts)
cards=''.join(f'<article id="d{f["id"]}"><h2>{f["id"]+1:02d} {esc(f["name"])}</h2><p>{esc(f["idea"])}</p><p class="meta">{f["bodies"]} bodies · {f["colliders"]} collision shapes · solver {f["solver"]} · all asleep at 15 s in isolated acceptance</p><a href="Captures/{f["id"]:02d}-intact.png"><img loading="lazy" src="Captures/{f["id"]:02d}-intact.png" alt="Actual Unity game view of {esc(f["name"])}"></a><p><a href="Captures/{f["id"]:02d}-release.png">Open the isolated support-removal test view</a> <small>(isolated physics test with simple materials)</small></p></article>' for f in facts)
page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Mayhem Foundry - playable map review</title><style>body{margin:0;background:#111927;color:#e7eefb;font:17px/1.6 system-ui,sans-serif}main{max-width:1180px;margin:auto;padding:40px 24px}h1{font-size:48px;line-height:1.05}h2{font-size:28px}a{color:#83e9cb}img{width:100%;display:block;border-radius:12px}article,section{padding:24px 0;border-bottom:1px solid #35445a;scroll-margin-top:15px}.meta,small{color:#afc0d8}.notice{border-left:4px solid #f5b86f;background:#302e32;padding:18px}table{width:100%;border-collapse:collapse}td,th{padding:10px;text-align:left;border-bottom:1px solid #35445a;font-size:15px}.table{overflow:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}pre{white-space:pre-wrap;font:13px/1.6 ui-monospace,monospace;background:#1c283b;padding:22px;border-radius:12px}code{color:#83e9cb}@media(max-width:760px){.pair{grid-template-columns:1fr}h1{font-size:36px}}</style><main><p class="meta">DEADLY JENGA / IMPLEMENTED EXPERIMENTAL MAP / 25 OCTOBER 2026</p><h1>Mayhem Foundry<br>Ten districts. One island.</h1><p>Open the lobby map selector and choose <b>Mayhem Foundry · Ten Districts</b>, preferably in Sandbox / Demo. The ten signed neighborhoods share a continuous shore-matched island approximately 688 × 687 metres. The district envelope is 680 × 470 metres; the highest comb reaches approximately 45.6 metres.</p><p><b>669 dynamic structural bodies · 2,096 structural collision shapes · one static island collider · eight hinges.</b> Existing maps were preserved. Gray-box gameplay does not depend on the signs, colors or preview art.</p><div class="notice">These are actual Unity captures of the implemented map, not AI concept renders. The isolated physics tests do not certify Quest performance, tracked-controller comfort, complete bot route coverage, or a live two-player session. The whole island is a comparative experiment rather than a balanced competitive map.</div><section><h2>The complete island</h2><div class="pair"><div><p>Intact / actual Game.unity</p><a href="Captures/island-intact.png"><img src="Captures/island-intact.png" alt="All ten districts on the island"></a></div><div><p>After the production tsunami test</p><a href="Captures/island-tsunami.png"><img src="Captures/island-tsunami.png" alt="Island after the full tsunami route"></a></div></div></section><section><h2>What changed during development</h2><ul><li>Rectangular ramp toes initially intersected supports; tapered toes removed that penetration.</li><li>The combs needed wider load-bearing spines to keep the cantilever mass over their bearings.</li><li>Movement testing exposed switchback headroom failures. Separate ramp lanes and open landings replaced overlapping flights.</li><li>Bridge entrances moved away from solid columns. Portal street junctions became single H-shaped sections.</li><li>Arches required flared feet, tied movable cradles, raised keystone bearing seats and a targeted 32/8 solver. Lower settings kept creeping.</li><li>The harbor needed sixteen velocity iterations to settle its hinges; its position solver remains at 12.</li><li>Shared visual and convex meshes reduce repeated geometry allocation/cooking. The map uses original colliders without a second survivor-collision world.</li></ul><p><a href="NativeAcceptance.txt">Read the native acceptance evidence</a> · <a href="GameValidation.txt">Read production movement / bot / tsunami results</a> · <a href="Facts.json">Counts and source hashes</a></p></section><section><h2>What to play-test first</h2><p>My design picks are the bookends, portal quarter and skyrail: they produce stacked routes and covered space from relatively few moving sections. The moving harbor and keystone vaults have more distinctive failure events, but require more careful bearings and solver settings. The drums are a useful wild card because a fallen wall can become a floor. These are design judgments to test with players, rather than a claim that automated movement proves fun.</p><p>All districts keep normal gravity and contact response. The harbor leaves use real heel bearing stops, with hinges joining them to movable frames rather than the world. Lower and upper stair pads provide space for capsule movement to finish a slope before turning. Loose bridge plates can still form small obstacles that require going around, jumping or climbing.</p></section><section><h2>Measured isolated stability</h2><p>Every final district starts without penetrations deeper than 4 cm, settles with no body moving more than 0.6 m, and has all bodies asleep at the 15-second measurement. Most initial movement is loose links seating onto their bearings. Supports and weights were then removed without forcing a collapse animation.</p><div class="table"><table><thead><tr><th>District</th><th>Bodies</th><th>Shapes</th><th>Solver</th><th>Max seating drift</th><th>Awake</th></tr></thead><tbody>'''+rows+'''</tbody></table></div></section>'''+cards+'''<section><h2>Production test record</h2><p>Frame-time samples describe this desktop Editor run and camera placement, not a Quest prediction. The detailed report distinguishes initial settling, a whole-island overview, and a nearby district view. A full island of active bots remains a separate performance concern from the structural body budget.</p><pre>'''+esc(game)+'''</pre></section><footer><p>World / Maps: layout, compound bodies and shoreline collision. DevTools / Editor: authoring, controlled native experiments and production checks. Presentation: wayfinding prefab and lobby preview. GameSession / Session: one added catalog entry. No Core changes.</p></footer></main></html>'''
(out/'index.html').write_text(page,encoding='utf-8')
sheet=Image.new('RGB',(1500,3*280),'#172235');draw=ImageDraw.Draw(sheet);font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
for i,f in enumerate(facts):
    p=captures/f'{i:02d}-intact.png'
    if not p.exists():continue
    with Image.open(p) as im:im.thumbnail((365,315));x=(i%4)*375+5;y=(i//4)*280+35;sheet.paste(im,(x,y));draw.text((x,y-23),f'{i+1:02d} {f["name"]}',fill='white',font=font)
sheet.save(out/'Districts-contact-sheet.jpg',quality=92)
with zipfile.ZipFile(out/'MayhemFoundryReview.zip','w',zipfile.ZIP_DEFLATED,compresslevel=5) as z:
    for f in list(captures.glob('*.png'))+[out/n for n in ['index.html','Facts.json','GameValidation.txt','NativeAcceptance.txt','Districts-contact-sheet.jpg']]:z.write(f,str(f.relative_to(out)))
print(json.dumps({'bodies':sum(f['bodies'] for f in facts),'colliders':sum(f['colliders'] for f in facts),'gallery':str(out/'index.html'),'gameComplete':'COMPLETE runtimeErrors=0' in game}))










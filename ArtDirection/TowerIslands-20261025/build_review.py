"""Assemble actual Unity captures and reports; never generate or retouch the game content."""
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import html, shutil, zipfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
SOURCE=ROOT/'Logs'/'Validation'
CITY=['Crownline Offices','Lantern Court Apartments','Split Atrium Hotel','Switchback Parking Tower','Central Exchange','Lantern City Hall','Foundry Lofts','Skygarden Residences','Harbor Waterworks','Gateway Headquarters']
TOWERS=['Sky Tree','Zipper Spire','Spiral Lantern','Cross-Block Relay','Cage Cathedral','Drop-Stop Tower','Saddle Beacon','Chalice Stack','Bookfall Keep','Kissing Towers']
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24)
SMALL=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',19)
CAPTURES=HERE/'Captures';CAPTURES.mkdir(exist_ok=True)
for prefix in ['CityExhibition','TowerExhibition']:
    for p in SOURCE.glob(prefix+'-*.png'):
        with Image.open(p) as im: im.verify()
        shutil.copy2(p,CAPTURES/p.name)
    for p in (SOURCE/'TowerNative').glob(prefix+'-*.png'):
        with Image.open(p) as im: im.verify()
        shutil.copy2(p,CAPTURES/p.name)
if (SOURCE/'TowerNative/native.txt').exists():shutil.copy2(SOURCE/'TowerNative/native.txt',HERE/'TowerNative.txt')
for name in ['TowerExhibitionStudy-build.txt','TowerExhibitionStudy-physics.txt','CityExhibition-game.txt','TowerExhibition-game.txt','CityExhibition-routes.txt','TowerExhibition-routes.txt']:
    p=SOURCE/name
    if p.exists():shutil.copy2(p,HERE/name)

def board(prefix,names,start):
    sheet=Image.new('RGB',(1440,5*292+70),'#142238');draw=ImageDraw.Draw(sheet)
    draw.text((16,12),('CITY BUILDINGS 11–20' if start==11 else 'TOWER LABORATORY 1–10')+' · Unity before / after physical demolition',font=FONT,fill='white')
    for i,name in enumerate(names):
        col=i%2;row=i//2;x=col*720;y=70+row*292
        draw.text((x+12,y),f'{start+i:02}  {name}',font=FONT,fill='white')
        for j,state in enumerate(['intact','demolition']):
            p=CAPTURES/f'{prefix}-{start+i:02}-{state}.png'
            assert p.exists(),p
            with Image.open(p) as im: im=ImageOps.contain(im.convert('RGB'),(350,218));sheet.paste(im,(x+10+j*355,y+36))
            draw.text((x+18+j*355,y+258),'Before' if j==0 else 'After physics pulse',font=SMALL,fill='white')
    out=HERE/(prefix+'-BeforeAfter.jpg');sheet.save(out,quality=93)
    return out.name

rows=[]
for prefix,names,start in [('CityExhibition',CITY,11),('TowerExhibition',TOWERS,1)]:
    contact=board(prefix,names,start)
    rows.append(f'<section><h2>{"City Buildings 11–20" if start==11 else "Tower Laboratory 1–10"}</h2><p><a href="{contact}">All ten before/after pairs</a></p>')
    rows.append('<div class="pair">'+''.join(f'<figure><a href="Captures/{prefix}-island-{s}.png"><img src="Captures/{prefix}-island-{s}.png"></a><figcaption>{label}</figcaption></figure>' for s,label in [('intact','Whole island — intact'),('demolition','Whole island — after controlled physics pulse'),('tsunami','Whole island — after actual tsunami')])+'</div>')
    for i,name in enumerate(names):
        rows.append(f'<article><h3>{start+i:02} · {html.escape(name)}</h3><div class="pair">')
        for state,label in [('intact','Production Game scene — before'),('demolition','Production Game scene — after controlled physics pulse'),('tsunami','Production Game scene — after tsunami'),('settled','Isolated native physics — settled'),('impact','Isolated native physics — after release / heavy lower-section impact'),('released','Isolated native physics — mechanism release')]:
            namefile=f'{prefix}-{start+i:02}-{state}.png'
            if (CAPTURES/namefile).exists():rows.append(f'<figure><a href="Captures/{namefile}"><img loading="lazy" src="Captures/{namefile}"></a><figcaption>{label}</figcaption></figure>')
        rows.append('</div></article>')
    report=HERE/f'{prefix}-game.txt'
    if report.exists():rows.append('<details open><summary>Production validation</summary><pre>'+html.escape(report.read_text(encoding='utf-8-sig'))+'</pre></details>')
    report=HERE/f'{prefix}-routes.txt'
    if report.exists():rows.append('<details open><summary>Focused route follow-up</summary><pre>'+html.escape(report.read_text(encoding='utf-8-sig'))+'</pre></details>')
    rows.append('</section>')
physics=HERE/'TowerNative.txt'
if physics.exists():rows.append('<section><h2>Isolated physics results</h2><pre>'+html.escape(physics.read_text(encoding='utf-8-sig'))+'</pre></section>')
page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Twenty built towers · Deadly Jenga</title><style>body{background:#102038;color:#f1f6ff;font:17px/1.55 system-ui;max-width:1500px;margin:auto;padding:30px}h1{font-size:40px}h2{color:#79ebc1}h3{margin-top:40px}a{color:#c7afff}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;background:#1c304b;padding:8px;border-radius:8px}img{width:100%;display:block}figcaption{padding:8px}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#192c44;padding:20px;font-size:14px}section{margin:50px 0}@media(max-width:750px){.pair{grid-template-columns:1fr}}</style><h1>Twenty towers, two real Unity islands</h1><p>Actual Unity screenshots and native PhysX evidence. The production before/after pairs use matching viewpoints. The isolated impact captures use a 22 m/s X + 9 m/s Z off-centre pulse, following any separately logged support release. Production demolition resets the intact map first. Both are separate experiments from the normal tsunami.</p><p>Large fabricated sections move as rigid parts. Their floors, stairs and load-bearing ribs stay together; the joints between separate sections can slide, unseat or topple. This deliberately trades fine fracture for fewer moving bodies. Landscape, props and environment remain presentation art.</p><p><a href="Review.md">Construction notes, final results and limitations</a> · <a href="CityExhibition-BeforeAfter.jpg">City before/after board</a> · <a href="TowerExhibition-BeforeAfter.jpg">Tower before/after board</a></p><p>Desktop Editor evidence only. No Quest device, live multiplayer or exhaustive route certification is implied. See the written review for adaptations and limitations.</p>'''+''.join(rows)+'</html>'
(HERE/'index.html').write_text(page,encoding='utf-8')
with zipfile.ZipFile(HERE/'TowerIslandsReview.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for p in HERE.rglob('*'):
        if p.is_file() and p.suffix!='.zip':archive.write(p,p.relative_to(HERE))
with zipfile.ZipFile(HERE/'TowerIslandsReview.zip') as archive:assert archive.testzip() is None
print('PASS: actual capture integrity, twenty before/after pairs, gallery and archive.')

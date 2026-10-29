from pathlib import Path
import html
import shutil
import zipfile

ROOT=Path(__file__).resolve().parent
REPORTS=ROOT.parent.parent/'Logs'/'Validation'
shots=[
 ('Lobby-Terminals-Updated.png','Terminals','All three terminals','Live lobby render. Host and map/mode controls now sit on flat screen-width walls.'),
 ('Lobby-Host-Flat-Panel.png','Terminals','Host terminal','Flat vertical support, inset D-pad and scroll dial.'),
 ('Lobby-Mode-Flat-Panel.png','Terminals','Mode / map terminal','Flush directional disc and A/B/X/Y details.'),
 ('Lobby-Experimental-Maps.png','Map browsers','Lobby map library','Experimental / unfinished maps retain their own nested categories.'),
 ('Demo-Experimental-Maps.png','Map browsers','Demo map library','The same collections are shared with the desktop and VR demo pickers.'),
 ('Aurora-Beacon-40.png','Beacon','Beacon / emerging','Unity screen capture with controlled transition progress for review.'),
 ('Aurora-Beacon-65.png','Beacon','Beacon / overtaking','White expands across the view with flowing green and violet strips.'),
 ('Aurora-Beacon-92.png','Beacon','Beacon / full cover','White cover hides the scene change; arrival then reveals the game.')
]
for file,*_ in shots:
    assert (REPORTS/file).exists(),file
    shutil.copy2(REPORTS/file,ROOT/file)
shutil.copy2(REPORTS/'LobbyBrowserBeaconChecks.txt',ROOT/'Checks.txt')
cards=[]
for i,(file,category,title,note) in enumerate(shots):
    cards.append(f'<article data-category="{category}"><button class="picture" data-index="{i}" aria-label="Enlarge {html.escape(title)}"><img src="{file}" alt="{html.escape(title)}" loading="lazy"></button><div class="body"><span>{category}</span><h2>{title}</h2><p>{note}</p><a download href="{file}">Download full resolution</a></div></article>')
page='''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Lobby / map library / aurora beacon</title><style>
*{box-sizing:border-box}body{margin:0;background:#110e20;color:#f6f1ff;font:16px/1.6 system-ui,sans-serif}main{max-width:1400px;margin:auto;padding:50px 28px}h1{font-size:clamp(32px,5vw,62px);line-height:1.08;letter-spacing:-.04em;max-width:950px}header p{color:#beb2d1;max-width:850px}.eyebrow{letter-spacing:.2em;color:#82edc1;text-transform:uppercase;font-size:12px}nav{display:flex;gap:12px;flex-wrap:wrap;margin:30px 0}button,a{font:inherit}nav button{border:1px solid #4c3b64;border-radius:30px;padding:9px 20px;background:#241b36;color:#e4d9ef;cursor:pointer}nav button.selected{background:#9df2d1;color:#182d2c}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:25px}article{border:1px solid #3b2d4c;border-radius:16px;overflow:hidden;background:#21182f}.picture{padding:0;border:0;display:block;width:100%;background:#151022;cursor:zoom-in}.picture img{width:100%;display:block;aspect-ratio:1.6;object-fit:contain}.body{padding:20px 24px}h2{font-size:25px;margin:4px 0 8px}.body span{font-size:11px;color:#9df2d1;text-transform:uppercase;letter-spacing:.14em}.body p{color:#c0b4cd}a{color:#ceb2ff;text-underline-offset:4px}.foot{margin-top:35px;color:#aa9cbf;font-size:13px}dialog{background:#100b19;color:white;border:1px solid #56406c;max-width:96vw;padding:12px;border-radius:12px}dialog::backdrop{background:#000d}dialog img{display:block;max-width:92vw;max-height:82vh}dialog button{background:#30213e;border:0;color:white;border-radius:8px;padding:8px 15px;cursor:pointer}dialog footer{display:flex;align-items:center;justify-content:space-between;gap:15px;margin-top:12px}@media(max-width:760px){.grid{grid-template-columns:1fr}main{padding:28px 16px}}
</style></head><body><main><header><div class="eyebrow">Deadly Jenga / Unity Editor captures</div><h1>Flat terminals.<br>A cleaner map library.<br>An aurora departure.</h1><p>Actual lobby renders and menu captures. Beacon images use controlled progress to show its intermediate stages; the validation also exercises the real lobby-to-game departure and cleanup.</p></header><nav aria-label="Filter pictures"><button class="selected" data-filter="All">All</button><button data-filter="Terminals">Terminals</button><button data-filter="Map browsers">Map browsers</button><button data-filter="Beacon">Beacon</button></nav><section class="grid">'''+''.join(cards)+'''</section><p class="foot">Desktop Unity verification; headset appearance still needs an on-device test. <a href="Checks.txt">Checks</a> · <a href="MapClassification.json">Map classification</a> · <a download href="LobbyBrowserBeacon-Pictures.zip">Download pictures</a></p></main><dialog id="viewer"><img alt="Enlarged Unity capture"><footer><button id="previous">Previous</button><span id="caption"></span><button id="next">Next</button><button id="close">Close</button></footer></dialog><script>
const cards=[...document.querySelectorAll('article')],viewer=document.querySelector('#viewer');let index=0;
function show(n){index=(n+cards.length)%cards.length;viewer.querySelector('img').src=cards[index].querySelector('img').src;document.querySelector('#caption').textContent=cards[index].querySelector('h2').textContent;if(!viewer.open)viewer.showModal();}
document.querySelectorAll('.picture').forEach(b=>b.onclick=()=>show(+b.dataset.index));
document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-filter]').forEach(q=>q.classList.toggle('selected',q===b));cards.forEach(c=>c.hidden=b.dataset.filter!=='All'&&c.dataset.category!==b.dataset.filter);});
document.querySelector('#previous').onclick=()=>show(index-1);document.querySelector('#next').onclick=()=>show(index+1);document.querySelector('#close').onclick=()=>viewer.close();viewer.addEventListener('keydown',e=>{if(e.key==='ArrowRight')show(index+1);if(e.key==='ArrowLeft')show(index-1);});
</script></body></html>'''
(ROOT/'index.html').write_text(page,encoding='utf-8')
with zipfile.ZipFile(ROOT/'LobbyBrowserBeacon-Pictures.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for file,*_ in shots:archive.write(ROOT/file,file)
print('Eight Unity pictures and gallery ready.')

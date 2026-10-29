from pathlib import Path
from html import escape

p=Path(__file__).resolve().parent
maps=[
 ('epic-honeycomb-hive','Honeycomb Beehive','honeycomb-hive.png','Amber lighting, cream wax, honey-cell relief, organic wood grain and honey drips.'),
 ('epic-cloud-carnival','Cotton Candy Cloud Carnival','cotton-carnival.png','Peach sunset, blueberry star posts, mint and raspberry candy, gold trim, bulbs and pennants.'),
 ('epic-candy-glass-cloud','Candy Glass Above Clouds','blue-sky-candy-glass.png','Bright daylight, red / green / purple fruit glass, rounded edges, sky reflections and tiny bubbles.')]
def figure(src,label):
 return f'<figure><a href="{src}"><img loading="lazy" src="{src}" alt="{escape(label)}"></a><figcaption>{escape(label)}</figcaption></figure>'
cards=[]
for mid,title,ref,note in maps:
 for suffix in ['overview','close','detail']:
  assert (p/(mid+'-'+suffix+'.png')).exists()
 cards.append(f'<article id="{mid}"><h2>{escape(title)}</h2><p>{escape(note)}</p><p class="where">Lobby / demo map picker &rarr; Epic connected cities &rarr; {escape(title)}</p>')
 cards.append('<div class="compare">'+figure('../CitySurfaceThemes-v3-20261026/'+ref,'Approved AI concept - visual target')+figure(mid+'-overview.png','Actual production Game Play Mode - overview')+'</div>')
 cards.append('<h3>Before / after</h3><div class="compare">'+figure('BeforeLighting/'+mid+'-close.png','Before the material and lighting pass - actual Game capture')+figure(mid+'-close.png','After this pass - actual Game capture')+'</div>')
 cards.append(figure(mid+'-detail.png','After this pass - closer surface inspection in actual Game Play Mode')+'</article>')
report=escape((p/'Runtime.txt').read_text(encoding='utf-8'))
nav=''.join(f'<a href="#{mid}">{escape(title)}</a>' for mid,title,_,_ in maps)
page='''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Deadly Jenga - Fantasy materials and lighting</title><style>
body{margin:0;background:#10141d;color:#f5eee5;font:16px/1.6 system-ui,sans-serif}main{max-width:1750px;margin:auto;padding:32px 3vw}h1{font-size:42px;line-height:1.15;max-width:1000px}h2{font-size:30px}h3{font-size:21px;color:#a9e8da}a{color:#a9e8da}nav{display:flex;gap:24px;flex-wrap:wrap;position:sticky;top:0;background:#10141def;padding:15px 0;z-index:1}.compare{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:0 0 24px}img{display:block;width:100%;height:auto;border-radius:10px}figcaption,.where{font-size:14px;color:#bac9db}article{padding-top:24px;border-top:1px solid #435064;margin-top:32px;scroll-margin-top:65px}.note{background:#1d2a3c;border-left:4px solid #a9e8da;padding:18px}pre{white-space:pre-wrap;font:14px/1.5 monospace}details{padding:18px;background:#1b2332}@media(max-width:850px){.compare{grid-template-columns:1fr}h1{font-size:30px}nav{position:static}}
</style></head><body><main><h1>Fantasy materials &amp; lighting</h1><p>Three themed maps, captured in the actual Unity game.</p><nav>NAV</nav><p class="note">The approved images are AI concepts. Every overview, before/after and detail image below is an actual production Game Play Mode capture, not a paintover. The result is not pixel-identical to the concept. Rounded edges are visual meshes; original colliders, bodies and layouts are unchanged. Reflections are baked rather than rendered live. Desktop bloom and light color grading are disabled in VR. This visual pass adds rendering work; no FPS improvement or Quest headset verification is claimed.</p>CARDS<p><a href="LightingNotes.txt">Implementation and performance notes</a> &middot; <a href="TextureGeneration.json">Saved texture assets and full generation prompts</a></p><details><summary>Final runtime verification - all checks passed</summary><pre>REPORT</pre></details></main></body></html>'''
page=page.replace('NAV',nav).replace('CARDS',''.join(cards)).replace('REPORT',report)
(p/'index.html').write_text(page,encoding='utf-8')
assert all(x in page for x in ['BeforeLighting','-detail.png','TextureGeneration.json','contacts=1090'])
assert (p/'complete.txt').read_text().strip()=='Completed; logged errors=0'
print('PASS: gallery includes all 9 current Game captures, before/after comparisons, saved texture prompts and final runtime report.')

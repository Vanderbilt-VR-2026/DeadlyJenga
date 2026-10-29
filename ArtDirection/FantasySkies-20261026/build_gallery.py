from pathlib import Path
import ast,base64,json

p=Path(__file__).resolve().parent
root=p.parents[1]
source=ast.parse((p.parent/'CarnivalSky-20261026/build_gallery.py').read_text())
template=next(ast.literal_eval(n.value) for n in source.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='page' for t in n.targets))
faces=['front','right','back','left','up','down']
captures=faces+['join-front-right','join-right-back','join-back-left','join-left-front','join-front-up','join-front-down']
themes=[('Glass','GlassSkyCube','Candy Glass Above Clouds','blue-sky-candy-glass.png','epic-candy-glass-cloud'),('Honeycomb','HiveSkyCube','Honeycomb Beehive','honeycomb-hive.png','epic-honeycomb-hive')]
cards=[]
for folder,prefix,title,reference,mid in themes:
 out=p/folder;out.mkdir(exist_ok=True)
 images=['data:image/png;base64,'+base64.b64encode((root/'Assets/_DeadlyJenga/Presentation/Visuals/FantasyMaps/Textures'/(prefix+'-'+f+'-v1.png')).read_bytes()).decode() for f in faces]
 page=template.replace('Carnival cloud sky · Deadly Jenga',title+' sky · Deadly Jenga').replace('Carnival in the cotton-candy clouds',title)
 page=page.replace('the carnival map','the '+title+' map')
 page=page.replace('../CitySurfaceThemes-v3-20261026/cotton-carnival.png','../../CitySurfaceThemes-v3-20261026/'+reference).replace('href="Generation.json"','href="../Generation.json"').replace('../ApprovedFantasyMaps-20261026/index.html','../../ApprovedFantasyMaps-20261026/index.html')
 page=page.replace('IMAGES',json.dumps(images)).replace('CAPTURES',''.join('<figure><a href="unity-sky-'+name+'.png"><img loading="lazy" src="unity-sky-'+name+'.png" alt="Unity '+name+' view"></a><figcaption>'+name.replace('-',' ')+' · Unity Editor render</figcaption></figure>' for name in captures))
 page=page.replace('<canvas id="sky">','<p><a href="../index.html">Both new skies</a> · Map picker: <b>Epic connected cities → '+title+'</b></p><canvas id="sky">')
 page=page.replace('<h2>Actual Unity sky renders</h2>','<h2>Actual Game Play Mode capture</h2><figure><a href="../../ApprovedFantasyMaps-20261026/'+mid+'-close.png"><img loading="lazy" src="../../ApprovedFantasyMaps-20261026/'+mid+'-close.png" alt="Unity map with sky"></a></figure><h2>Actual Unity sky renders</h2>')
 (out/'index.html').write_text(page,encoding='utf-8')
 cards.append('<article><a href="'+folder+'/index.html"><img src="'+folder+'/unity-sky-front.png" alt="'+title+' sky"><h2>'+title+'</h2></a><p>Interactive 360° view, native assets and actual Unity renders.</p></article>')
index='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Glass Cloud & Honeycomb · new skyboxes</title><style>body{background:#171623;color:#fff3de;font:18px/1.6 system-ui;margin:0}main{max-width:1500px;margin:auto;padding:32px}h1{font-size:38px;line-height:1.2}a{color:#ffe0b9;text-decoration:none}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:28px}img{width:100%;border-radius:14px}article{background:#292438;border-radius:14px;overflow:hidden}h2,article p{margin:18px}small{color:#c9b9ce}</style><main><h1>Glass Cloud & Honeycomb</h1><p>Two reference-led skyboxes, installed on the existing maps. Open either to look around.</p><div class="grid">CARDS</div><p><a href="../CarnivalSky-20261026/index.html">Carnival sky</a> · <a href="Generation.json">Generation prompts and asset provenance</a> · <a href="../ApprovedFantasyMaps-20261026/index.html">All three maps in Unity</a></p><small>Six native 1254 × 1254 generated faces per sky. Built-in image tool; no separate paid enhancer or API key. Gameplay physics unchanged. Desktop verification only; no Quest test.</small></main></html>'''
(p/'index.html').write_text(index.replace('CARDS',''.join(cards)),encoding='utf-8')
print('Built two six-face viewers and combined review gallery')

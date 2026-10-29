from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageColor
import math, json, html, textwrap, zipfile

ROOT=Path(__file__).resolve().parent
DATA=json.loads((ROOT/'Study.json').read_text(encoding='utf-8'))
FIG=ROOT/'Diagrams';FIG.mkdir(exist_ok=True)
GREEN='#28d875';RED='#ff4c69';PURPLE='#b48aff';GOLD='#ffd365';INK='#172b48';BLUE='#167aa5';ORANGE='#df6728'
W,H=1500,1200
def font(n,bold=False):return ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf' if bold else 'C:/Windows/Fonts/segoeui.ttf',n)
def vec(a,b):return tuple(x-y for x,y in zip(a,b))
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def norm(v):
 d=math.sqrt(dot(v,v));return tuple(x/d for x in v)
def shade(c,v):return tuple(round(min(255,max(0,x*v))) for x in ImageColor.getrgb(c))
class Canvas:
 def __init__(self):
  self.im=Image.new('RGB',(W,H),'#edf5fa');self.d=ImageDraw.Draw(self.im)
  self.svg=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}"><rect width="100%" height="100%" fill="#edf5fa"/>']
 def poly(self,p,fill,outline=None,width=1):
  fill=('#%02x%02x%02x'%fill) if isinstance(fill,tuple) else fill
  self.d.polygon(p,fill=fill,outline=outline,width=width)
  self.svg.append(f'<polygon points="'+ ' '.join(f'{x:.1f},{y:.1f}' for x,y in p)+f'" fill="{fill}" stroke="{outline or fill}" stroke-width="{width}"/>')
 def line(self,p,fill=INK,width=2,dash=False):
  if dash:
   for a,b in zip(p,p[1:]):
    dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy)
    for t in range(0,int(length),16):
     u=min(t+9,length);self.d.line([(a[0]+dx*t/length,a[1]+dy*t/length),(a[0]+dx*u/length,a[1]+dy*u/length)],fill=fill,width=width)
  else:self.d.line(p,fill=fill,width=width)
  self.svg.append('<polyline points="'+' '.join(f'{x:.1f},{y:.1f}' for x,y in p)+f'" fill="none" stroke="{fill}" stroke-width="{width}"'+(' stroke-dasharray="9 7"' if dash else '')+'/>')
 def text(self,p,t,n=22,fill=INK,bold=False):
  self.d.text(p,t,font=font(n,bold),fill=fill)
  self.svg.append(f'<text x="{p[0]}" y="{p[1]+n}" font-family="Segoe UI,sans-serif" font-size="{n}" fill="{fill}" font-weight="{700 if bold else 400}">{html.escape(t)}</text>')
 def para(self,p,t,width=36,n=20,fill=INK):
  for i,line in enumerate(textwrap.wrap(t,width)):self.text((p[0],p[1]+i*(n+7)),line,n,fill)
 def arrow(self,a,b,color=ORANGE,width=5):
  self.line([a,b],color,width);v=norm((b[0]-a[0],b[1]-a[1],0));p=(-v[1],v[0]);l=15
  self.poly([b,(b[0]-v[0]*l+p[0]*7,b[1]-v[1]*l+p[1]*7),(b[0]-v[0]*l-p[0]*7,b[1]-v[1]*l-p[1]*7)],color)
 def save(self,name):
  self.im.save(FIG/(name+'.png'));(FIG/(name+'.svg')).write_text(''.join(self.svg)+'</svg>',encoding='utf-8')

def geometry(i):
 parts=[];routes=[]
 def box(c,s,col=GREEN,angle=0,kind='wall'):
  a=math.radians(angle);u=(math.cos(a),math.sin(a),0);v=(-math.sin(a),math.cos(a),0);w=(0,0,1)
  pts=[tuple(c[k]+u[k]*x*s[0]/2+v[k]*y*s[1]/2+w[k]*z*s[2]/2 for k in range(3)) for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
  parts.append((pts,col,kind,c))
 def beam(a,b,width=1,col=GREEN,kind='wall'):
  z=norm(vec(b,a));x=norm(cross((0,0,1),z)) if abs(z[2])<.99 else (1,0,0);y=cross(z,x);c=tuple((a[k]+b[k])/2 for k in range(3));length=math.sqrt(dot(vec(b,a),vec(b,a)))
  pts=[tuple(c[k]+x[k]*u*width/2+y[k]*v*.4+z[k]*w*length/2 for k in range(3)) for u,v,w in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
  parts.append((pts,col,kind,c))
 def ring(z,r,width=2,col=RED,n=8):
  for a in range(n):
   t=(a+.5)*360/n;rad=math.radians(t);box((r*math.cos(rad),r*math.sin(rad),z),(2*r*math.tan(math.pi/n)+.2,width,.45),col,t+90,'floor')
 def flight(a,b):beam(a,b,2.2,PURPLE,'ramp');routes.append((a,b))
 if i==1:
  for q in range(6):
   base=q*12
   for angle in (0,120,240):
    a=math.radians(angle);box((7*math.cos(a),7*math.sin(a),base+6),(18,1.8,12),GREEN,angle)
    for f in (3,6,9,12):box((6*math.cos(a+math.pi/6),6*math.sin(a+math.pi/6),base+f),(14,5,.4),RED,angle+30,'floor')
   for f in range(4):flight((-8 if f%2==0 else 2,-4,base+f*3),(2 if f%2==0 else -8,-4,base+(f+1)*3))
   box((0,0,base+.3),(10,10,.6),GOLD,0,'bearing')
 elif i==2:
  for q in range(6):
   base=q*10;sgn=1 if q%2==0 else -1;box((0,0,base+5),(6,10,10))
   for f in (3.3,6.6,10):box((sgn*3,0,base+f),(23,12,.45),RED,0,'floor')
   box((sgn*11,0,base+5),(2,12,10));box((0,0,base+.3),(11,11,.6),GOLD,0,'bearing')
   for f in range(3):flight((-8 if f%2==0 else 8,-6,base+f*3.33),(8 if f%2==0 else -8,-6,base+(f+1)*3.33))
 elif i==3:
  for q in range(8):
   base=q*8.5;ring(base+.3,11,2,GOLD);ring(base+8.5,11)
   for side in (-1,1):box((side*9.5,0,base+8.5),(5,3,.45),RED,0,'floor')
   for t in range(0,360,90):a=math.radians(t);box((11*math.cos(a),11*math.sin(a),base+4.25),(1.2,1.2,8.5))
   for branch in (0,math.pi):
    for k in range(8):
     a=branch+k*math.pi/4;b=branch+(k+1)*math.pi/4
     flight((8*math.cos(a),8*math.sin(a),base+k*8.5/8),(8*math.cos(b),8*math.sin(b),base+(k+1)*8.5/8))
 elif i==4:
  for q in range(6):
   base=q*12;angle=q%2*90
   for y in (-4.5,4.5):box((0,y,base+6) if not angle else (-y,0,base+6),(34,1,12),GREEN,angle)
   for f in (3,6,9,12):box((0,0,base+f),(34,10,.45),RED,angle,'floor')
   box((0,0,base+.25),(9,9,.5),GOLD,0,'bearing')
   for f in range(4):flight((-7 if f%2==0 else 7,-3,base+f*3),(7 if f%2==0 else -7,-3,base+(f+1)*3))
 elif i==5:
  for q in range(3):
   base=q*22;ring(base+.3,12,2,GOLD);ring(base+22,12)
   for k in range(8):
    a=k*math.pi/4;b=(k+1)*math.pi/4
    beam((12*math.cos(a),12*math.sin(a),base),(12*math.cos(b),12*math.sin(b),base+22),1.2)
   for f in range(1,8):ring(base+f*22/8,9,2.5,RED)
   for f in range(8):flight((-7 if f%2==0 else 7,-3,base+f*22/8),(7 if f%2==0 else -7,-3,base+(f+1)*22/8))
 elif i==6:
  for q in range(4):
   base=q*15
   for x in (-10,10):
    for y in (-8,8):box((x,y,base+7.5),(2,2,15))
   box((0,0,base+.4),(22,18,.8),RED,0,'floor')
   for x in (-7,7):
    for y in (-6,6):box((x,y,base+1.5),(4,4,.7),GREEN,0,'bearing');box((x,y,base+2.6),(2.3,2.3,1.5),GOLD,0,'bearing')
   for f in range(1,4):box((0,0,base+3+f*3),(18,14,.4),RED,0,'floor');flight((-7 if f%2 else 7,-4,base+3+(f-1)*3),(7 if f%2 else -7,-4,base+3+f*3))
 elif i==7:
  base=0
  for height,r0,r1 in ((20,15,10),(18,10,6.5),(20,6.5,3.5)):
   for y in (-5,5):
    for s in (-1,1):beam((s*r0,y,base),(s*r1,y,base+height),2)
   for f in range(1,int(height/3)+1):
    z=min(f*3,height);r=r0+(r1-r0)*z/height;box((0,0,base+z),(2*r,10,.4),RED,0,'floor')
    flight((-r*.65 if f%2 else r*.65,0,base+max(0,z-3)),(r*.65 if f%2 else -r*.65,0,base+z))
   box((0,0,base+height),(2*r1+2,12,.8),GOLD,0,'bearing');base+=height
 elif i==8:
  for q in range(7):
   base=q*9
   for a in (0,120,240):t=math.radians(a);box((5*math.cos(t),5*math.sin(t),base+4.5),(2,2,9))
   ring(base+.3,6,2,GOLD);ring(base+8,16,3,RED)
   for k in range(8):
    if k in (1,5):continue
    a=k*math.pi/4;beam((6*math.cos(a),6*math.sin(a),base+4),(16*math.cos(a),16*math.sin(a),base+8),6,GREEN,'floor')
   for k in range(8):a=k*math.pi/4;b=(k+1)*math.pi/4;flight((7*math.cos(a),7*math.sin(a),base+k*9/8),(7*math.cos(b),7*math.sin(b),base+(k+1)*9/8))
 elif i==9:
  for q in range(3):
   base=q*22
   for x in (-3,3):box((x,4,base+11),(2,4,22))
   for side in (-1,1):
    beam((side*6,0,base),(side*9,0,base+22),12)
    for f in range(1,8):
     box((side*(6+3*f/7-2.4),0,base+f*3),(4,10,.4),RED,0,'floor')
     flight((side*(6+3*(f-1)/7-2.4),-4 if f%2 else 4,base+(f-1)*3),(side*(6+3*f/7-2.4),4 if f%2 else -4,base+f*3))
    box((side*6,0,base+.5),(4,13,1),GOLD,0,'bearing')
  box((26,0,31.1),(8,12,.5),'#a7b8c7',0,'floor');box((26,4,15.5),(3,3,31),'#a7b8c7')
 elif i==10:
  box((0,0,.6),(36,22,1.2),GREEN,0,'bearing')
  for side in (-1,1):
   for q in range(3):
    base=q*24;x0=side*(13-base/6);x1=side*(13-(base+24)/6)
    for y in (-5,5):beam((x0,y,base+1.2),(x1,y,base+25.2),2)
    for f in range(1,9):
     z=base+f*3;x=side*(13-z/6);box((x,0,z+1.2),(7,12,.4),RED,0,'floor')
     flight((side*(13-(z-3)/6),-4 if f%2 else 4,z-3+1.2),(x,4 if f%2 else -4,z+1.2))
    box((x0,0,base+1.5),(10,15,.7),GOLD,0,'bearing')
  box((0,0,72),(3,10,2),GOLD,0,'bearing')
 return parts,routes

FACES=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
def render(c,cut):
 i=c['id'];parts,routes=geometry(i);can=Canvas();height=c['height']
 can.text((54,35),f'{i:02d}  {c["name"].upper()}',42,bold=True)
 can.text((56,99),'CUTAWAY + FAILURE HYPOTHESIS' if cut else 'STRUCTURAL MASSING + ROUTE INTENT',22,BLUE,True)
 can.text((56,140),f'{height} m study height   |   {c["floors"]}   |   {c["risk"]} development risk',20)
 az=math.radians(-90 if cut else -58);el=math.radians(8 if cut else 15)
 view=(math.cos(el)*math.cos(az),math.cos(el)*math.sin(az),math.sin(el));right=(-math.sin(az),math.cos(az),0);up=cross(view,right)
 scale=11.6 if height>65 else 12.8
 def p(v):v=(v[0],v[1],v[2]-height*.48);return (530+dot(v,right)*scale,665-dot(v,up)*scale)
 ground=[p(v) for v in [(-25,-20,0),(25,-20,0),(25,20,0),(-25,20,0)]];can.poly(ground,'#dfd2ad')
 polys=[]
 for vertices,col,kind,center in parts:
  if cut and kind=='wall' and center[1]<-1:continue
  for face in FACES:
   vs=[vertices[k] for k in face];n=cross(vec(vs[1],vs[0]),vec(vs[2],vs[0]));n=norm(n)
   if dot(n,view)<.001:continue
   center=tuple(sum(v[k] for v in vs)/4 for k in range(3));light=.68+.28*max(0,dot(n,norm((-.4,-.7,1))))
   polys.append((dot(center,view),[p(v) for v in vs],shade(col,light)))
 for _,points,color in sorted(polys,key=lambda x:x[0]):can.poly(points,color,'#477060',1)
 # Route is a diagrammatic graph, not a claimed capsule-tested path.
 for f,(a,b) in enumerate(routes):
  can.line([p(a),p(b)],BLUE,2,True)
  if f%6==0:can.arrow(p(a),p(b),BLUE,3)
 a,b=p((-24,0,0)),p((-24,0,height));can.line([a,b],INK,2);can.text((a[0]-72,(a[1]+b[1])/2),f'{height} m',20,INK,True)
 for z in (0,height):v=p((-24,0,z));can.line([(v[0]-7,v[1]),(v[0]+7,v[1])],INK,2)
 anchor_points={1:[(0,0,12.3),(7,0,42),(6,6,63)],2:[(0,0,10.3),(10,0,35),(-8,-6,53.3)],3:[(11,0,17.3),(0,8,36),(0,0,59)],4:[(0,0,12.25),(0,13,42),(12,0,57)],5:[(12,0,22.3),(10.24,4.24,33),(0,-12,55)],6:[(7,-6,17.6),(7,-6,16.5),(-10,-8,52.5)],7:[(0,0,20),(3,0,32),(6.5,-5,44)],8:[(6,0,9.3),(16,0,35),(12.7,12.7,62)],9:[(6,0,22.5),(-7.5,0,33),(26,0,31.3)],10:[(0,0,72),(8,0,.6),(-5,0,49)]}[i]
 labels=sorted(enumerate(zip(c['anchors'],anchor_points)),key=lambda x:p(x[1][1])[1])
 for rank,(j,(title,target)) in enumerate(labels):
  yy=245+rank*250;can.text((1030,yy),f'{j+1:02d}',28,ORANGE,True);can.para((1080,yy+3),title,25,21)
  can.arrow((1010,yy+30),p(target),ORANGE,3)
 if cut:
  can.text((70,1080),'Proposed change:',20,ORANGE,True)
  can.para((70,1110),c['failure'].split('. ')[0]+'.',125,18)
 else:
  can.text((70,1085),'Estimated authoring allowance (not measured):',20,INK,True)
  can.text((70,1120),f'{c["bodies"]} bodies   /   {c["colliders"]} contact shapes   /   {c["joints"]} joints',22)
 can.text((1030,925),'Dashed blue: route intent',18,BLUE)
 can.text((1030,955),'Gold: bearings / loose keys',18)
 can.text((1030,985),'Green: structural sections',18)
 can.text((1030,1015),'Orange: feature callouts',18,ORANGE)
 can.text((56,1170),'SCHEMATIC • not a Unity capture, construction model or simulation • Front ribs omitted in cutaway; route holes/pads require detailed authoring.',15)
 can.save(f'{i:02d}-'+('mechanism' if cut else 'structure'))

SOURCES={
 'UnityCompound':('Unity — compound collider construction','https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collider-shapes/compound-colliders/create-compound-collider'),
 'UnityPerformance':('Unity — enhanced physics performance','https://unity.com/how-to/enhanced-physics-performance-smooth-gameplay'),
 'PhysXJoints':('NVIDIA PhysX — joints and convergence','https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/Joints.html'),
 'Safdie':('Safdie Architects — Habitat 67','https://www.safdiearchitects.com/projects/habitat-67'),
 'Interlace':('Büro Ole Scheeren — The Interlace','https://www.buro-os.com/projects/the-interlace/'),
 'SOM':('SOM — Burj Khalifa','https://www.som.com/projects/burj-khalifa/'),
 'CTBUH':('CTBUH — Outrigger Design Guide preview','https://store.ctbuh.org/PDF_Previews/Books/2017_CTBUHOutriggerGuide_Preview.pdf'),
 'Arup':('Arup — CCTV structural system','https://www.arup.com/en-us/projects/china-central-television-headquarters/'),
 'GDC':('GDC 2010 — Luke Schneider, Multiplayer Level Design in Red Faction Guerrilla (session overview)','https://www.gdcvault.com/play/1012881/contactUs'),
 'BuildingCreator':('Embark technical artist Adrian Björkerud / SideFX — Building Creator','https://www.sidefx.com/community/making-the-procedural-buildings-of-the-finals-using-houdini/'),
 'Finals':('Embark — THE FINALS destruction update, July 2025','https://www.reachthefinals.com/patchnotes/730'),
 'LocalFoundry':('Local measured Mayhem Foundry results','Evidence/FoundryGame.txt')}
def link(key):title,url=SOURCES[key];return f'<a href="{html.escape(url,quote=True)}">{html.escape(title)}</a>'

def report():
 esc=html.escape
 rows=''.join(f'<tr><td><a href="#t{c["id"]}">{c["id"]:02d} {esc(c["name"])}</a></td><td>{c["height"]} m</td><td>{esc(c["bodies"])}</td><td>{esc(c["colliders"])}</td><td>{esc(c["joints"])}</td><td>{esc(c["risk"])}</td></tr>' for c in DATA['concepts'])
 sections=[]
 for c in DATA['concepts']:
  paragraphs=''.join(f'<h3>{title}</h3><p>{esc(c[key])}</p>' for key,title in [('idea','The build'),('surprise','What is different'),('bearing','How it could stand'),('routes','How people move through it'),('failure','What damage changes'),('cost','Cost and compromises'),('test','Smallest useful test')])
  sections.append(f'<article id="t{c["id"]}"><p class="eyebrow">{esc(c["priority"])} · {esc(c["risk"])} risk</p><h2>{c["id"]:02d} {esc(c["name"])}</h2><p class="meta">Study height {c["height"]} m · {esc(c["floors"])} · footprint {esc(c["footprint"])}</p><div class="pair">'+''.join(f'<a href="Diagrams/{c["id"]:02d}-{kind}.svg"><img loading="lazy" src="Diagrams/{c["id"]:02d}-{kind}.png" alt="{esc(c["name"])} {kind} schematic"><span>Open full vector {kind} diagram</span></a>' for kind in ['structure','mechanism'])+'</div>'+paragraphs+'<p class="meta">References are precedents and constraints, not proof of this proposal: '+ ' · '.join(link(k) for k in c['sources'])+'</p></article>')
 research=f'''<section><h2>The important translation from architecture to this game</h2>
 <p><b>Use architecture for load paths and spatial ideas, not as evidence that loose blocks will stand.</b> Habitat 67 combines modules into a connected system using post-tensioning, rods, cables and welding. Its attractive offsets do not establish that the same shapes can simply be stacked loose. My proposal instead places broad bearings under balanced rigid cassettes. {link('Safdie')}.</p>
 <p>The Interlace demonstrates stacked inhabited bars and roof-level shared spaces. I borrow the idea that a building block can contain an entire neighborhood, then constrain each game bar to a valid centre crossing and an independently checked upper-stack centre of mass. {link('Interlace')}.</p>
 <p>SOM describes Burj Khalifa around a buttressed core; CTBUH describes outriggers engaging perimeter columns to reduce deformation. PhysX rigid bodies do not simulate a concrete core bending or a loose decorative truss stiffening a tower. In the proposed Sky Tree, the three wings are actually part of the rigid section. A loose belt laid around separate columns is not credited as a tension tie. {link('SOM')} · {link('CTBUH')}.</p>
 <p>Arup's CCTV system is a continuous tube supported through its surface grid. The Kissing Towers proposal is a different mechanism: compression at the crown with thrust resisted at the feet. It is deliberately not a loose replica of CCTV, and it has high risk until a small native prototype works. {link('Arup')}.</p>
 <p>Unity supports several colliders acting together under one body, but explicitly recommends testing compound arrangements for behavior and cost. This motivates four-storey cassettes, not the assumption that they are free: contact shapes, active contacts, queries and body bounds all still matter. {link('UnityCompound')} · {link('UnityPerformance')}.</p>
 <p>NVIDIA notes that difficult joint convergence is associated with heavy/light chains and large mass ratios; its joint guidance recommends avoiding ratios above 10 in those scenarios. This is relevant to Bookfall's hinges, not a universal rule for every loose block contact. Large wings on tiny jointed props are an experiment to reject early. {link('PhysXJoints')}.</p>
 <p>The public Red Faction session overview explicitly treats destruction as something that changes level flow, strategy and exploits, and identifies playtesting and technical constraints. I used that as the design question: does the damaged tower create another playable place? I did not treat its engine or talk overview as a ready-made Unity implementation, and did not claim to have watched unavailable lecture content. {link('GDC')}.</p>
 <p>Embark's July 2025 destruction update explicitly discusses keeping level design playable after substantial destruction, alongside knock-on building damage and clearer damage visuals. My interpretation is to judge each tower in several damaged states, not just approve its intact silhouette. A leaning section should create cover or a route choice, while cues let players understand the danger. This is a design lesson, not evidence that our physics engine has the same destruction features. {link('Finals')}.</p>
 <p>In the published Building Creator breakdown, Embark technical artist Adrian Björkerud describes pre-fractured assets, watertight source geometry, authored collision, shared building parameters and reusable feature nodes. That is a production pipeline beyond ordinary loose Rigidbody stacks. The relevant adaptation here is a reusable cassette recipe: load-bearing shell, floors, circulation openings, landing pads, collision and visual layer must agree. If finer fracture is added later, pre-author a small bounded set of fragments and its collision; do not assume a runtime cut can cheaply manufacture a playable interior. This proposal does not require Houdini or a package change. {link('BuildingCreator')}.</p></section>'''
 equations='''<section><h2>A quick physics screen before opening Unity</h2><p>For a rigid upper assembly on horizontal seats, project its combined centre of mass onto the support polygon formed by real contact patches. For every separable level, use the mass of <em>everything above that level</em>; checking only one cassette misses unstable upper stacks. A point inside the polygon is only a static feasibility screen. It does not guarantee stability under impacts, sliding, contact tolerances or moving players.</p><p><code>x̄ = Σ(mᵢ xᵢ) / Σmᵢ</code>. For a simplified rigid assembly accelerated laterally at its centre of mass, tipping begins near <code>a / g ≈ b / h</code>, where <code>b</code> is the horizontal margin from the COM projection to the active support edge and <code>h</code> is its height. Example: a uniform 60 m tower with COM at 30 m and a 10 m margin gives approximately 0.33 g; narrowing that margin to 5 m gives 0.17 g. Those are illustrative mechanics calculations, not a tsunami prediction. A wave applying force at another height has a different moment arm, and sliding can occur first.</p><p>Practical consequence: use a real wide base, taper or an inhabited buttress to keep height affordable. A bigger decorative island or a painted foundation does not widen the tower's support. Do not silently move the centre of mass below the visible structure to make a failed concept appear successful.</p><p>For Bookfall, a 22 m wing rotated 65° from vertical has outward reach <code>22 sin65° ≈ 19.9 m</code> and endpoint rise <code>22 cos65° ≈ 9.3 m</code>. The receiving roof must be designed for that geometry. A roof elsewhere is not a guaranteed landing. These calculations are checked by the build script.</p></section>'''
 gameplay='''<section><h2>Rules for making height playable</h2><ul><li><b>Many useful levels, not just one faraway rooftop.</b> Aim for 3 m floor intervals in the first gray box, rest courts every three or four levels and views between levels. Do not force a continuous 60 m climb as the only route.</li><li><b>Two routes should differ in cost.</b> One reliable stair/ramp route; one climb, short drop or cross-tower route. After a local failure, try to preserve one escape rather than guarantee every route remains open.</li><li><b>Build the stair opening as part of the section.</b> Start with roughly 1.5–2 m clear route width, generous headroom relative to the current avatar, 20–28° ramp targets and 1.5–2 m flat turns. These are gray-box targets, not verified universal comfort limits.</li><li><b>Flush exterior climb strips.</b> Recess red floors and decorative bands behind designated wall/rib faces. Make a deliberate top-out platform. Repeated proud floor lips are a regression to the festival problem.</li><li><b>No tiny surprise steps at slope ends.</b> In the current source the default player scale is 0.5; CharacterController step offset is 0.05 × scale, only 2.5 cm. The Foundry tests exposed both upper and lower landing failures. A ramp must finish onto a flat pad before a turn or an adjacent vertical collider.</li><li><b>Space must exist after damage too.</b> Reserve fall lanes, side exits from cages, a route outside a Drop-Stop crush zone, and broad footholds on tilted remains. Keep objectives away from the sole part that can vanish.</li><li><b>VR and bots are separate acceptance checks.</b> A diagrammed route is not a capsule sweep, a successful capsule sweep is not tracked-hand comfort, and a few bots moving is not proof they navigate every floor. The game also allows equipment that blocks climbing/jumping; objective-critical routes must account for that.</li></ul></section>'''
 budget=f'''<section><h2>Budgets: fewer bodies, not imaginary free physics</h2><p>{esc(DATA['budgetStatus'])} These ranges are deliberately wide and must be replaced by authored counts. Visual trim is included in neither physics count; it should not receive collision unless gameplay needs it.</p><div class="scroll"><table><thead><tr><th>Concept</th><th>Height</th><th>Moving bodies</th><th>Contact shapes</th><th>Joints</th><th>Risk</th></tr></thead><tbody>{rows}</tbody></table></div><p>Keep large sections rigid within themselves and separate them at meaningful story bands. The compromise is that one four-floor section cannot individually lose a tiny wall. Optional bounded fracture into a few larger pieces could be a later feature, but none of these budgets assumes an existing fracture pipeline or free creation of hundreds of fragments.</p><p>Measured local comparison: the existing Foundry has 669 bodies and 2,096 structural shapes. Its recorded editor run was approximately 38 FPS idle, 15 FPS with 32 bots and 14 FPS with bots plus tsunami. Quiet sampling included about 14.4 ms/frame in Physics.Simulate and 22.6 ms in the two bot threat/destination scopes combined. Nested profiler scopes are not additive frame totals. Different camera views and short samples also vary. This means tower design can reduce costs, but cannot responsibly promise Quest FPS or solve all bot/navigation overhead. {link('LocalFoundry')}.</p></section>'''
 tests='''<section><h2>Ideas I would reject or restrict</h2><ul><li><b>A forest of separate diagrid sticks:</b> many contact bodies and no automatic welded load path. Keep meaningful diagonals integral to a larger rigid section.</li><li><b>Tensegrity or cable-hung skyscrapers as the default:</b> visually striking but dependent on tension constraints, tuning and moving supports. They add a different simulation problem before the basic tower routes work.</li><li><b>Magnetic stacking as a cure for a bad bearing:</b> it hides an invalid structure and adds continuous forces or constraints. Use explicit broad contact seats first.</li><li><b>One enormous indestructible tower body:</b> cheap body count but very coarse interactions. Keep separable bands and a few comprehensible weak points.</li><li><b>Every floor as an independent thin plate:</b> more contacts, seams, tiny steps and unstable debris. Integral multi-floor sections trade some granularity for dependable routes.</li><li><b>Tight mechanical slots everywhere:</b> interlocking detail may jam, tunnel or keep contacts awake. Start with generous clearances, broad stops and large readable keys.</li><li><b>Beautiful completely closed bowls:</b> they catch debris and players. The Chalice needs intentional openings and an escape route before it deserves a full-height test.</li></ul></section><section><h2>The prototype funnel</h2><ol><li><b>Start with four candidates:</b> Sky Tree, Zipper Spire, Cross-Block Relay and Saddle Beacon. One inhabited module each, not ten maximum-height towers at once.</li><li><b>Check authored geometry:</b> valid closed convex pieces, no unintended initial inter-body penetration, real bearing patches, mass/COM and upper-stack support checks. Count bodies, contact shapes, joints and live contact pairs separately.</li><li><b>Check quiet native physics:</b> 30–60 seconds settling, at least several initial orientations/placements, and no forced Sleep(), magnetism or invisible static anchors. Record drift and which assemblies remain awake. Compare with a plain tower at equal height and usable floor area.</li><li><b>Walk the actual routes:</b> production desktop motor, controller/XR simulator, both directions, every switchback, top-out and equipment-restricted route. Include two characters passing and a character on the outer edge during impact. Headset comfort still needs a real headset.</li><li><b>Test damage as level design:</b> remove one shoe, displace a key, hit the base and midsection, then test several plausible fallen poses for cover, escape and new routes. Do not replace physics with an authored collapse and call that validation.</li><li><b>Increase height progressively:</b> one section, two sections, then the proposed tower height. Only raise solver settings on troublesome assemblies after measuring the effect; a higher iteration count is not an excuse for bad seating.</li><li><b>Stress comparably:</b> equal bots, camera, seed, timestep, wave direction and total test duration. Include a deliberate multi-tower collapse. Capture frame-time distributions, active bodies/contacts, bot planning and allocations; test on Quest before accepting a device budget.</li><li><b>Only then try wild cards:</b> one Drop-Stop bay, one small hinged Bookfall wing, and two short Kissing Tower halves. Stop early if the mechanism jams, launches a player, or needs hidden stabilization.</li></ol><p><b>Decision rule:</b> select a tower by useful routes and interesting damaged states per measured CPU cost. Height and low Rigidbody count alone are not a win. Combining two proven tower types will probably produce a stronger city than repeating the most exotic mechanism everywhere.</p></section>'''
 style='''body{margin:0;background:#111d30;color:#e9eff7;font:18px/1.65 system-ui,sans-serif}main{max-width:1260px;margin:auto;padding:48px 26px}h1{font-size:clamp(38px,5vw,68px);line-height:1.06}h2{font-size:32px;line-height:1.2;margin-top:0}h3{font-size:20px;color:#9ce7cb;margin-bottom:4px}p{max-width:1050px}a{color:#93d9ff}img{display:block;width:100%;border-radius:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}.pair a{font-size:15px}.meta,.eyebrow{color:#a7bad2}.eyebrow{text-transform:uppercase;font-size:14px;letter-spacing:1.5px}section,article{padding:38px 0;border-bottom:1px solid #33445c;scroll-margin-top:20px}.notice{background:#353134;border-left:5px solid #ffcc69;padding:20px}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:12px 10px;border-bottom:1px solid #33445c;font-size:15px}.scroll{overflow:auto}li{margin:12px 0}code{color:#ffda92}nav{display:flex;flex-wrap:wrap;gap:9px}nav a{padding:5px 10px;background:#20324a;border-radius:7px;font-size:15px}@media(max-width:800px){.pair{grid-template-columns:1fr}main{padding:30px 18px}}'''
 nav=''.join(f'<a href="#t{c["id"]}">{c["id"]:02d} {esc(c["name"])}</a>' for c in DATA['concepts'])
 page=f'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Deadly Jenga — Tall Tower Research</title><style>{style}</style><main><p class="eyebrow">Deadly Jenga · Tower research / 25 October 2026</p><h1>Height that changes<br>the way the map plays.</h1><p>Ten high-rise families using large inhabited sections, explicit bearings and multiple vertical routes. Four credible first prototypes, three spatial/cost comparisons and three mechanisms to test carefully. These are new combinations proposed for this game; I am not claiming invention of the underlying architectural systems.</p><div class="notice"><b>Research and proposals, not implemented maps.</b> No new tower was simulated in this study. The two candy-style boards are AI art direction, not screenshots or dependable dimensions. The twenty vector diagrams are simplified authored schematics, not collider models or solved load paths. Floor lips, stairs and the number of storeys in the art are illustrative; flush climbing strips and detailed route clearance take precedence. Budgets and failure behavior are hypotheses until tested.</div><section><h2>The ten silhouettes</h2><p>Built-in image generation, using the existing game only as style reference. The numbered names correspond to the detailed proposals below. <a href="ArtPrompts.json">Exact prompts and generation method</a>.</p><a href="Concepts-01-05.png"><img src="Concepts-01-05.png" alt="Art studies of tower concepts one to five"></a><br><a href="Concepts-06-10.png"><img src="Concepts-06-10.png" alt="Art studies of tower concepts six to ten"></a></section><nav>{nav}</nav>{research}{equations}{gameplay}{budget}{''.join(sections)}{tests}<section><h2>Evidence and deliverables</h2><p>The reference research uses primary architecture practices, engineering guidance, engine documentation and the public GDC session overview. Local gameplay numbers come from the saved Foundry report, not a new benchmark. The proposals and sketches are my design synthesis.</p><ul>{''.join('<li>'+link(k)+'</li>' for k in SOURCES)}</ul><p><a href="Study.json">Structured design specifications</a> · <a href="ContactSheet.jpg">All ten structural diagrams</a> · <a href="TowerResearch.zip">Download the complete study</a></p><p>No Unity assets, scenes, Core contracts, project settings, packages or existing maps were changed. Only ArtDirection/TowerLab-20261025 was added. Nothing here certifies tracked-controller behavior, Quest performance or networking.</p></section></main></html>'''
 (ROOT/'index.html').write_text(page,encoding='utf-8')

def main():
 for c in DATA['concepts']:
  render(c,False);render(c,True)
  print(f'Rendered {c["id"]:02d} {c["name"]}',flush=True)
 sheet=Image.new('RGB',(1600,3*430),'#172b48');d=ImageDraw.Draw(sheet)
 for i,c in enumerate(DATA['concepts']):
  with Image.open(FIG/f'{c["id"]:02d}-structure.png') as im:
   im.thumbnail((390,365));x=i%4*400+5;y=i//4*430+38;sheet.paste(im,(x,y));d.text((x,y-30),f'{c["id"]:02d} {c["name"]}',fill='white',font=font(19,True))
 sheet.save(ROOT/'ContactSheet.jpg',quality=94)
 report()
 assert abs(22*math.sin(math.radians(65))-19.94)<.02
 assert abs(22*math.cos(math.radians(65))-9.30)<.02
 assert len(list(FIG.glob('*.png')))==20 and len(list(FIG.glob('*.svg')))==20
 with zipfile.ZipFile(ROOT/'TowerResearch.zip','w',zipfile.ZIP_DEFLATED) as z:
  for p in ROOT.rglob('*'):
   if p.is_file() and p.suffix in ('.html','.png','.svg','.json','.jpg','.txt'):z.write(p,str(p.relative_to(ROOT)))
 print('PASS 10 proposals, 20 diagram pairs of PNG/SVG, two art boards, checked example geometry, HTML and zip generated.')
if __name__=='__main__':main()

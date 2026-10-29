"""Original physical candy/plastic injury cues; no speech or third-party samples."""
from pathlib import Path
import math, random, struct, wave, json
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_DeadlyJenga/Presentation/Audio/CandyAudio/Resources/CandyAudio/Players'
RATE=32000
report=[]
for name,duration in [('PlayerPain',.42),('PlayerDeath',1.18),('PlayerHeal',.64)]:
 for variant in range(1,5):
  rng=random.Random(name+str(variant)); low=0.; values=[]; shift=rng.uniform(.94,1.06)
  for i in range(int(RATE*duration)):
   t=i/RATE; n=rng.uniform(-1,1);low+=.12*(n-low)
   def tap(at,freq,gain,decay):
    u=t-at
    return 0 if u<0 else gain*(math.sin(2*math.pi*freq*shift*u)+.22*math.sin(2*math.pi*freq*2.71*u))*math.exp(-u*decay)*(1-math.exp(-u*1500))
   if name=='PlayerHeal':
    a=tap(0,510,.24,13)+tap(.085,765,.18,13)+tap(.17,1020,.12,12)+low*.08*math.exp(-t*10)
   else:
    death=name=='PlayerDeath'
    a=low*(2.2 if death else 1.5)*math.exp(-t*24)+n*.24*math.exp(-t*180)
    a+=tap(0,92 if death else 145,.58,22)+tap(.006,241,.17,28)+tap(.012,880,.10,85)
    a+=low*.3*math.sin(t*1200)*math.exp(-t*17)
    if death:
     for at,g in [(.075,.40),(.16,.27),(.29,.15)]:a+=tap(at,65+at*110,g,17)
     a+=low*.62*math.exp(-max(0,t-.12)*6)*(1-math.exp(-t*12))
     for at in [.20,.29,.39,.52,.65]:a+=tap(at,330+rng.random()*2,.025,65)
   values.append(a)
  peak=max(abs(x) for x in values);gain=.78/peak
  samples=[int(max(-1,min(1,x*gain))*32767*min(1,(duration-i/RATE)/.025)) for i,x in enumerate(values)]
  path=OUT/f'{name}-{variant:02}.wav'
  with wave.open(str(path),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(struct.pack('<'+'h'*len(samples),*samples))
  report.append({'file':path.name,'seconds':duration,'peak':max(abs(x) for x in samples)/32768,'rms':math.sqrt(sum((x/32768)**2 for x in samples)/len(samples))})
(ROOT/'ArtDirection/CandyAudio/PhysicalPlayerCues.json').write_text(json.dumps(report,indent=2))
print('Generated and peak/RMS checked',len(report),'original mono cues.')

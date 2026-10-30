"""Original candy weapon synthesis. Standard-library Python; no sampled firearm recordings.
Run from any directory. Existing authored files are preserved; --force regenerates these clips.
"""
from pathlib import Path
import math, random, hashlib, json, struct, uuid, wave, sys
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_DeadlyJenga/Presentation/Audio/CandyAudio/Resources/CandyAudio/Weapons'
RATE=32000
DURATIONS={'WeaponRifle':.85,'WeaponShotgun':1.25,'WeaponBlaster':.65,'WeaponTransform':.65,'WeaponDraw':.34,'WeaponHolster':.3,'WeaponPickup':.48,'WeaponSwap':.55,'WeaponDrop':.4,'WeaponSword':.38,'WeaponHammer':.48,'WeaponBoxing':.42,'WeaponPlasmaImpact':.7}
def meta(p,folder=False):
    target=Path(str(p)+'.meta')
    if target.exists():return
    g=uuid.uuid5(uuid.NAMESPACE_URL,'DeadlyJenga/CandyWeapon/'+p.relative_to(ROOT).as_posix()).hex
    extra='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if folder else '''AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 32000
    compressionFormat: 1
    quality: 0.75
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 1
  ambisonic: 0
  3D: 1
'''
    target.write_text('fileFormatVersion: 2\nguid: '+g+'\n'+extra)
def generate(name,take,duration):
    r=random.Random(name+str(take)); shift=r.uniform(.96,1.04); smooth=0.; values=[]
    for i in range(int(RATE*duration)):
        t=i/RATE; n=r.uniform(-1,1);smooth+=.18*(n-smooth)
        def tone(f,decay=12,start=0,amp=1):
            u=t-start
            return 0 if u<0 else amp*math.sin(2*math.pi*f*shift*u)*math.exp(-decay*u)*(1-math.exp(-u*1500))
        if name in ('WeaponRifle','WeaponShotgun'):
            heavy=name=='WeaponShotgun'
            # Pressure wave, sharp report, low chest and a short mechanical pump cycle.
            a=smooth*(4.6 if heavy else 2.9)*math.exp(-t*(13 if heavy else 20))
            a+=n*(1.05 if heavy else .9)*math.exp(-t*150)
            a+=tone(51 if heavy else 83,13 if heavy else 18,amp=1.0)
            a+=tone(108 if heavy else 173,23,amp=.44)+tone(1250,90,amp=.24)
            for delay,gain in ((.035,.40),(.072,.24),(.13,.14)):
                if t>=delay:a+=smooth*gain*math.exp(-(t-delay)*22)
            # Distinct rack-back, sliding grit, then a solid breech lock before the next shot.
            for start,freq,gain in ((.14,310,.36),(.235,145,.60)):
                u=t-start
                if u>=0:a+=smooth*gain*math.exp(-u*48)+tone(freq,50,start,gain)
            if .15<t<.225:a+=n*.13*math.sin((t-.15)/.075*math.pi)
        elif name=='WeaponBlaster':
            phase=2*math.pi*(690*t-460*t*t)
            a=.48*math.sin(phase)*math.exp(-t*7)+tone(94,9,amp=.35)+smooth*.8*math.exp(-t*24)
            a+=tone(1120,18,.035,.15)
        elif name=='WeaponTransform':
            # Motor winds the carriage through a half turn, two pawls catch, heavy lock closes.
            a=tone(74,15,amp=.65)+tone(740,40,.025,.25)
            u=max(0,t-.04)
            a+=math.sin(2*math.pi*(170*u+980*u*u))*.22*math.exp(-u*5)*(t>.04)
            for start,freq,gain in ((.12,420,.35),(.24,610,.38),(.34,82,.95),(.36,1380,.22)):
                a+=tone(freq,32,start,gain)
                if t>=start:a+=smooth*gain*math.exp(-(t-start)*50)
        elif name in ('WeaponSword','WeaponBoxing','WeaponHammer'):
            a=smooth*math.exp(-abs(t-.1)*24)*1.8+tone(105 if name=='WeaponBoxing' else 340,19,.11,.45)
            a+=tone(820 if name=='WeaponSword' else 190,22,.14,.22)
        elif name=='WeaponPlasmaImpact':
            a=smooth*2*math.exp(-t*9)+tone(82,9,amp=.6)+tone(550,12,amp=.25)
        else:
            pitches={'WeaponDraw':(320,680),'WeaponHolster':(600,240),'WeaponPickup':(420,840),'WeaponSwap':(560,1120),'WeaponDrop':(280,165)}[name]
            a=tone(pitches[0],24,amp=.45)+tone(pitches[1],20,.09,.45)+smooth*.75*math.exp(-t*35)
            if name in ('WeaponPickup','WeaponSwap'):a+=tone(pitches[1]*1.5,18,.18,.24)
        fade=min(1,t/.0015,(duration-t)/.035)
        values.append(math.tanh(a*1.3)*fade)
    peak=max(abs(v) for v in values);gain=.88/max(.88,peak)
    return b''.join(struct.pack('<h',int(max(-1,min(1,v*gain))*32767)) for v in values)
OUT.mkdir(parents=True,exist_ok=True)
for p in (OUT.parent.parent,OUT.parent,OUT):meta(p,True)
manifest=[]
for name,duration in DURATIONS.items():
    for take in range(4):
        p=OUT/f'{name}-{take+1:02}.wav'
        if '--force' in sys.argv or not p.exists():
            data=generate(name,take,duration)
            with wave.open(str(p),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(data)
        meta(p)
        manifest.append({'name':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'seconds':duration,'rate':RATE,'channels':1})
(ROOT/'ArtDirection/CandyAudio/WEAPON_CLIP_MANIFEST.json').write_text(json.dumps({'provenance':'Original deterministic synthesis; no external recordings.','clips':manifest},indent=2))
# Verify authored clips too: preserve edits, but fail explicitly if they are invalid.
seen=set()
for entry in manifest:
    p=OUT/entry['name']
    assert entry['sha256'] not in seen, 'Duplicate take: '+p.name
    seen.add(entry['sha256'])
    with wave.open(str(p),'rb') as w:
        assert w.getnchannels()==1 and w.getframerate()==RATE and w.getsampwidth()==2,p.name
        data=struct.unpack('<'+'h'*w.getnframes(),w.readframes(w.getnframes()))
        peak=max(abs(v) for v in data)/32767
        rms=math.sqrt(sum(v*v for v in data)/len(data))/32767
        assert .5<peak<.95 and .015<rms<.6, 'Headroom/energy: '+p.name
        assert abs(data[0])<5 and abs(data[-1])<100, 'Boundary fade: '+p.name
    assert Path(str(p)+'.meta').exists(),p.name
print(f'PASS: {len(manifest)} unique mono weapon clips; provenance, headroom, energy, fades and metadata')

"""Original procedural candy audio. No downloaded or third-party samples.
Run with bundled Python + numpy. Existing clips/bank are preserved unless --force.
"""
from pathlib import Path
import argparse, hashlib, json, uuid, wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ASSETS=ROOT/'Assets/CandyAudio'
RATE=32000
FORCE=argparse.ArgumentParser();FORCE.add_argument('--force',action='store_true');FORCE=FORCE.parse_args().force
manifest=[]
def guid(path):return uuid.uuid5(uuid.NAMESPACE_URL,'DeadlyJenga/CandyAudio/'+str(path).replace('\\','/')).hex
def meta(path,kind='file'):
    target=Path(str(path)+'.meta')
    if target.exists():return target.read_text().split('guid: ')[1].splitlines()[0]
    g=guid(path.relative_to(ROOT));extra=''
    if kind=='script':extra='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif kind=='audio':
        long='Music' in path.stem or path.stem.startswith(('Surf','BeachWind','FallWind','HandFlight','RubberSlide','BlockScrape'))
        extra='AudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: '+('1' if long else '0')+'\n    sampleRateSetting: 0\n    sampleRateOverride: 32000\n    compressionFormat: 1\n    quality: 0.65\n    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 1\n  ambisonic: 0\n  3D: 1\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target.write_text('fileFormatVersion: 2\nguid: '+g+'\n'+extra)
    return g

def noise(rng,n):
    raw=rng.normal(0,1,n)
    # Cheap band-limited noise; smooth enough for rubber and surf, no white hiss wall.
    return np.convolve(raw,np.ones(9)/9,mode='same')
def env(t,decay=8):return (1-np.exp(-t*180))*np.exp(-t*decay)
def tone(t,f,decay=8):return np.sin(2*np.pi*f*t)*env(t,decay)
def sweep(t,f0,f1,duration):
    phase=2*np.pi*(f0*t+(f1-f0)*t*t/(2*duration));return np.sin(phase)
def shape(t):
    length=max(.0001,t[-1]);return np.minimum(1,t/.012)*np.minimum(1,(length-t)/.025)
def generate(name,variant):
    seed=int(hashlib.sha256((name+str(variant)).encode()).hexdigest()[:8],16);r=np.random.default_rng(seed)
    f=r.uniform(.9,1.1);duration=r.uniform(.18,.3)
    if name in ['BlockHit','BlockSmash','LandHard']:duration=r.uniform(.45,.65)
    if name in ['BlockSplash','PlayerSplash']:duration=r.uniform(.85,1.3)
    if name=='StructureLean':duration=r.uniform(1.7,2.4)
    if name in ['RubberSlide','BlockScrape','FallWind','BeachWind','HandFlight','Surf']:duration=6
    if name in EMOTES:duration=r.uniform(.65,1.3)
    t=np.arange(int(RATE*duration))/RATE;n=noise(r,len(t));a=np.zeros_like(t)
    if name.startswith('Block') and 'Splash' not in name and name!='BlockScrape':
        base={'BlockTap':470,'BlockHit':210,'BlockSmash':105}[name]*f
        for ratio,amp,d in [(1,1,10),(1.63,.5,15),(2.7,.22,22),(4.1,.08,30)]:a+=amp*tone(t,base*ratio,d)
        a+=n*.5*env(t,22)
        if name=='BlockSmash':a+=.4*sweep(t,170*f,45*f,duration)*env(t,7)+n*.5*env(t,9)
    elif name in ['FootstepCandy','FootstepSand','LandSoft','LandHard']:
        base=180*f if 'Hard' not in name else 110*f
        a=tone(t,base,18)+.3*tone(t,base*2.4,28)+n*(.25 if name=='FootstepCandy' else .8)*env(t,20)
        if name=='FootstepSand':a+=noise(r,len(t))*.6*env(t,12)
    elif name in ['BlockSplash','PlayerSplash']:
        a=n*(1-np.exp(-t*65))*np.exp(-t*4)*(1+.3*np.sin(2*np.pi*7*t))
        for k in range(11):
            at=r.uniform(.03,duration*.85);local=np.maximum(0,t-at)
            a+=.17*np.sin(2*np.pi*(r.uniform(450,1300)*local+900*local**2))*env(local,35)*(t>=at)
        a+=.4*sweep(t,130*f,45,duration)*env(t,8)
    elif name=='StructureLean':
        u=t/duration;freq=(220*(1-u)**2+45)*f;phase=2*np.pi*np.cumsum(freq)/RATE
        pulse=(.55+.45*np.sin(2*np.pi*(3*t+2*t*t)))
        a=(np.sin(phase)+.32*np.sin(phase*2.01))*np.sin(np.pi*u)**.6*pulse*.6+n*.18*np.sin(np.pi*u)
    elif name in ['RubberSlide','BlockScrape']:
        freq=(190+90*np.sin(2*np.pi*1.0*t)+35*np.sin(2*np.pi*3*t))*f
        phase=np.cumsum(freq)*2*np.pi/RATE
        a=(np.sin(phase)+.22*np.sin(phase*3))*(.3+.2*np.sin(2*np.pi*4*t))*.35+n*(.2 if name=='BlockScrape' else .06)
    elif name in ['FallWind','BeachWind','HandFlight','Surf']:
        a=n*(.65+.2*np.sin(2*np.pi*t/6)+.12*np.sin(2*np.pi*3*t/6))
        if name=='Surf':a*=.5+.4*np.sin(2*np.pi*t/6)**2
        if name=='HandFlight':a+=.035*np.sin(2*np.pi*80*t)*(.4+.2*np.sin(2*np.pi*t/3))
    elif name in ['VaultGrunt','ClimbGrunt']:
        # Nonverbal, rounded cartoon effort syllable, not synthesized speech.
        fundamental=(145 if name=='VaultGrunt' else 170)*f*(1+.15*np.sin(np.pi*t/duration))
        phase=2*np.pi*np.cumsum(fundamental)/RATE
        a=(np.sin(phase)+.35*np.sin(phase*2)+.16*np.sin(phase*3))*np.sin(np.pi*t/duration)**1.4+n*.07*env(t,9)
    elif name in EMOTES:
        patterns={'FriendlyWave':[0,4,7],'TotallyMeantThat':[0,7,12],'Phew':[7,3,-2],'WhatWasThat':[0,1,-3],
          'GolfClap':[0,0,7],'JustChilling':[7,4,0],'NopeNopeNope':[7,5,3,0],'TinyVictoryDance':[0,4,7,12]}[name]
        root=r.uniform(220,290);spacing=duration/(len(patterns)+.8)
        for k,note in enumerate(patterns):
            at=k*spacing;u=np.maximum(0,t-at);active=t>=at;fr=root*2**((note+(variant-1.5)*.4)/12)
            wobble=fr*(1+.035*np.sin(2*np.pi*11*u))
            phase=2*np.pi*np.cumsum(wobble)/RATE
            a+=(np.sin(phase)+.3*np.sin(phase*2)+.1*np.sin(phase*3))*env(u,13)*active*.8
            if name=='GolfClap':a+=n*.7*env(u,55)*active
    else:
        values={'RubberTouch':(430,210),'ClimbPlant':(900,190),'ClimbRelease':(260,700),'Jump':(230,600),
            'Spawn':(420,850),'Respawn':(300,750),'UiClick':(800,1100),'GripWarning':(600,450)}
        f0,f1=values.get(name,(300,600));a=sweep(t,f0*f,f1*f,duration)*env(t,12)+.18*tone(t,f0*2*f,22)
        if name=='ClimbPlant':a+=.3*n*env(t,35)
    # Wrap crossfade for ambience/friction loops, preserving a continuous boundary.
    if duration==6:
        cross=int(RATE*.08);w=np.linspace(0,1,cross);a[-cross:]=a[-cross:]*(1-w)+a[:cross]*w;a=a[cross:]
    else:a*=shape(t)
    return a

EMOTES=['FriendlyWave','TotallyMeantThat','Phew','WhatWasThat','GolfClap','JustChilling','NopeNopeNope','TinyVictoryDance']
WORLD=['BlockTap','BlockHit','BlockSmash','BlockScrape','StructureLean','BlockSplash','PlayerSplash','RubberSlide','RubberTouch']
CHAR=['FootstepCandy','FootstepSand','Jump','LandSoft','LandHard','VaultGrunt','ClimbGrunt','ClimbPlant','ClimbRelease','Spawn','Respawn','GripWarning']+EMOTES
LOOPS=['BlockScrape','RubberSlide','FallWind','BeachWind','HandFlight','Surf','Music']
NAMES=WORLD+CHAR+['FallWind','BeachWind','HandFlight','Surf','Music','UiClick']
# Enum order is intentionally explicit and matches CandySound in CandyAudioBank.cs.
ENUM=['BlockTap','BlockHit','BlockSmash','BlockScrape','StructureLean','BlockSplash','PlayerSplash','RubberSlide','RubberTouch','FootstepCandy','FootstepSand','Jump','LandSoft','LandHard','VaultGrunt','ClimbGrunt','ClimbPlant','ClimbRelease','FallWind','BeachWind','HandFlight','Surf','Music','Spawn','Respawn','UiClick','GripWarning']+EMOTES
def music():
    bpm=112;beat=60/bpm;duration=64*beat;t=np.arange(round(duration*RATE))/RATE;a=np.zeros_like(t);rng=np.random.default_rng(541)
    def note(at,midi,length,amp,kind='mallet'):
        start=round(at*RATE);end=min(len(a),start+round(length*RATE));u=np.arange(end-start)/RATE;f=440*2**((midi-69)/12)
        if kind=='bass':v=(np.sin(2*np.pi*f*u)+.15*np.sin(2*np.pi*2*f*u))*env(u,5)
        elif kind=='hat':v=noise(rng,len(u))*env(u,55)
        else:v=(np.sin(2*np.pi*f*u)+.35*np.sin(2*np.pi*f*2.76*u)+.08*np.sin(2*np.pi*f*5.4*u))*env(u,9)
        a[start:end]+=v*amp
    chords=[(48,[60,64,67]),(45,[57,60,64]),(53,[60,65,69]),(55,[59,62,67])]
    motifs=[[0,2,4,2,1,0,2,4],[4,2,1,0,2,1,0,0],[0,1,2,4,2,1,0,2],[2,4,3,2,1,2,0,0]]
    scale=[72,74,76,79,81]
    for bar in range(16):
        bass,chord=chords[(bar//2)%4]
        for b in range(4):
            at=(bar*4+b)*beat;note(at,bass if b%2==0 else bass+7,beat*.8,.24,'bass');note(at+beat*.5,0,.08,.11,'hat')
            if b%2:note(at,chord[b%3]+12,.3,.1)
        for eighth,degree in enumerate(motifs[bar%4]):
            if (bar+eighth)%7!=0:note((bar*4+eighth*.5)*beat,scale[degree],.52,.2 if eighth%2==0 else .14)
    fade=min(160,len(a)//2);a[:fade]*=np.linspace(0,1,fade);a[-fade:]*=np.linspace(1,0,fade)
    return a
def write_clip(name,variant,signal):
    folder=ASSETS/'Audio'/('Music' if name=='Music' else 'Loops' if name in LOOPS else 'Emotes' if name in EMOTES else 'Effects');folder.mkdir(parents=True,exist_ok=True)
    path=folder/f'{name}_{variant+1:02}.wav'
    if not path.exists() or FORCE:
        # Fixed headroom and smooth saturation. Runtime mix provides further attenuation.
        peak=max(.001,float(np.max(np.abs(signal))));signal=np.tanh(signal/peak*1.15)*.72
        pcm=np.asarray(np.clip(signal,-1,1)*32767,dtype='<i2')
        with wave.open(str(path),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    g=meta(path,'audio');manifest.append({'cue':name,'variant':variant+1,'path':str(path.relative_to(ROOT)).replace('\\','/'),'guid':g,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    return g

ASSETS.mkdir(parents=True,exist_ok=True)
for path in ASSETS.rglob('*.cs'):meta(path,'script')
bank_script=meta(ASSETS/'CandyAudioBank.cs','script')
entries=[]
for name in ENUM:
    count=1 if name in LOOPS else 4
    gs=[write_clip(name,i,music() if name=='Music' else generate(name,i)) for i in range(count)]
    bus=3 if name=='Music' else 4 if name=='UiClick' else 2 if name in ['BeachWind','FallWind','Surf','HandFlight'] else 1 if name in CHAR else 0
    volume=.55 if name in EMOTES else .4 if name.startswith('Footstep') else .65
    if name=='Music':volume=.7
    cooldown=.04 if name=='ClimbPlant' else .1 if name in EMOTES else .08
    priority=45 if name=='GripWarning' else 70 if name in EMOTES or 'Grunt' in name else 105 if name=='RubberSlide' else 190 if name=='BlockScrape' else 160 if name.startswith('Footstep') else 180 if name in LOOPS and name!='Music' else 120
    maximum=3 if name in EMOTES or 'Grunt' in name else 5
    distance=100 if name in ['StructureLean','BlockSmash','BlockSplash'] else 32 if name in EMOTES else 65
    entries.append(f'  - sound: {ENUM.index(name)}\n    variants:\n'+''.join(f'    - {{fileID: 8300000, guid: {g}, type: 3}}\n' for g in gs)+f'    bus: {bus}\n    volume: {volume}\n    pitch: {{x: 0.94, y: 1.06}}\n    cooldown: {cooldown}\n    maxVoices: {maximum}\n    priority: {priority}\n    minDistance: 2\n    maxDistance: {distance}\n    loop: {int(name in LOOPS)}\n    output: {{fileID: 0}}\n')
entries=[entry.replace('    variants:\n','    enabled: 1\n    variants:\n') for entry in entries]
bank=ASSETS/'Resources/CandyAudio/Default.asset';bank.parent.mkdir(parents=True,exist_ok=True)
if not bank.exists() or FORCE:
    bank.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+bank_script+', type: 3}\n  m_Name: Default\n  m_EditorClassIdentifier: \n  cues:\n'+''.join(entries)+'  oneShotVoices: 28\n  loopVoices: 12\n  eventsPerSecond: 22\n  master: 0.8\n  world: 0.8\n  characters: 0.8\n  ambience: 0.32\n  music: 0.22\n  ui: 0.55\n')
meta(bank)
for folder in ASSETS.rglob('*'):
    if folder.is_dir():
        m=Path(str(folder)+'.meta')
        if not m.exists():m.write_text(f'fileFormatVersion: 2\nguid: {guid(folder.relative_to(ROOT))}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
(ROOT/'ArtDirection/CandyAudio/CLIP_MANIFEST.json').write_text(json.dumps({'sample_rate':RATE,'origin':'Original deterministic procedural synthesis; no external samples or melodies.','clips':manifest},indent=2))
print(f'Created/preserved {len(manifest)} clips across {len(ENUM)} cues. Bank: {bank.relative_to(ROOT)}')

"""Original electronic title-screen music revisions, with no sampled recordings."""
from pathlib import Path
import importlib.util
import json
import math
import numpy as np

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('engine', ROOT/'engine.py')
engine = importlib.util.module_from_spec(spec)
spec.loader.exec_module(engine)
engine.ROOT = ROOT
RATE = engine.RATE
TAU = 2*np.pi
PLANS = [
    {'id':'01-critical-mass','title':'Critical Mass','bpm':132,'direction':'Main-menu pick: aggressive electro-breakbeat, distorted bass and a broad synth hook'},
    {'id':'02-collapse-protocol','title':'Collapse Protocol','bpm':140,'direction':'Industrial arena groove: crunchy low riffs, half-time impacts and racing arpeggios'},
    {'id':'03-overdrive-island','title':'Overdrive Island','bpm':174,'direction':'Arcade drum and bass: fast broken drums, rolling bass and wide euphoric chords'},
    {'id':'04-neon-aftershock','title':'Neon Aftershock','bpm':118,'direction':'Electro-rock/synthwave: heavy toms, driving octaves and an anthemic low synth lead'},
    {'id':'05-chaos-engine','title':'Chaos Engine','bpm':128,'direction':'Bass-house launch energy: four-on-the-floor, growling syncopated bass and sharp stabs'}
]

def tone(kind,midi,gate,rng):
    f=440*2**((midi-69)/12)
    tail=.28 if kind=='pad' else .11
    t=np.arange(round((gate+tail)*RATE))/RATE
    phase=TAU*f*t
    release=np.exp(-np.maximum(t-gate,0)/(.065 if kind=='pad' else .025))
    attack=np.minimum(1,t/(.045 if kind=='pad' else .004))
    if kind=='bass':
        # A moving harmonic spectrum gives the bass a growl instead of a toy pluck.
        cutoff=3+10*np.exp(-t*9)+2*np.sin(TAU*2*t)**2
        y=np.zeros_like(t)
        for k in range(1,min(38,int(18000/f))):
            y+=np.sin(phase*k+.12*np.sin(TAU*.6*t)*k)/k*np.exp(-k/cutoff)
        y=np.tanh(y*2)*.7+np.sin(phase*.5)*.32
    elif kind=='lead':
        y=np.zeros_like(t)
        for detune in [.9975,1,1.0025]:
            for k in range(1,min(15,int(17000/f))):
                y+=np.sin(phase*k*detune)/k**1.25*np.exp(-k/(5+8*np.exp(-t*4)))/3
        y=np.tanh(y*1.4)*.70
    elif kind=='pad':
        y=np.zeros_like(t)
        for d in [.998,1.002]:
            for k in range(1,min(9,int(16000/f))):
                y+=np.sin(phase*k*d+.2*k)/k**1.7*.35
    elif kind=='chug':
        y=sum(np.sin(phase*k)/k*np.exp(-k/(6+10*np.exp(-t*35))) for k in range(1,min(25,int(17000/f))))
        y=np.tanh(y*3)*np.exp(-t*7)*.8
    elif kind=='arp':
        y=(np.sin(phase+.9*np.sin(phase*2)*np.exp(-t*5))+.17*np.sin(phase*3))*np.exp(-t*5)*.75
    else:
        y=(np.sin(phase)+.24*np.sin(phase*2))*np.exp(-t*10)
    return (y*release*attack*np.minimum(1,(gate+tail-t)/.012)).astype(np.float32)

def drum(kind,rng):
    dur={'kick':.48,'snare':.30,'hat':.09,'rim':.16,'shaker':.26,'tom':.38,'conga':.65}[kind]
    t=np.arange(round(dur*RATE))/RATE
    n=rng.normal(0,1,len(t))
    smooth=(n+np.roll(n,1)+np.roll(n,2))*.333
    high=n-smooth
    if kind=='kick':
        p=TAU*(46*t+132*.018*(1-np.exp(-t/.018)))
        y=np.tanh(np.sin(p)*1.8)*np.exp(-t*11)+high*.16*np.exp(-t*260)
    elif kind=='snare':
        y=(smooth*.8+high*.24)*np.exp(-t*20)+(np.sin(TAU*185*t)+.23*np.sin(TAU*327*t))*np.exp(-t*27)*.65
        y=np.tanh(y*1.6)
    elif kind=='rim':
        # Tight clap: three short noise pulses above a compact backbeat.
        y=np.zeros_like(t)
        for shift in [0,.011,.022]:
            z=np.maximum(t-shift,0)
            y+=smooth*.6*np.exp(-z*45)*(t>=shift)
    elif kind in ['hat','shaker','conga']:
        decay={'hat':75,'shaker':19,'conga':9}[kind]
        y=high*np.exp(-t*decay)*.45
        y+=sum(np.sin(TAU*f*t) for f in [4210,6137,8131])*.035*np.exp(-t*decay)
    else:
        y=np.sin(TAU*(91*t+85*.025*(1-np.exp(-t/.025))))*np.exp(-t*12)*.9
    return (y*np.minimum(1,t/.001)*np.minimum(1,(dur-t)/.015)).astype(np.float32)

engine.tone=tone
engine.drum=drum
engine.GM.update({'lead':81,'chug':30,'arp':80})

ROOT.mkdir(exist_ok=True)
(ROOT/'Designs.json').write_text(json.dumps({'method':'Original local electronic compositions. Revised after feedback that the initial proposals sounded too juvenile. No external music or samples.','tracks':PLANS},indent=2),encoding='utf-8')

# Relative semitones; the hooks favor rhythmic identity, rests and sustained notes.
RIFFS=[
 [(0,0,.38),(.75,0,.24),(1.5,7,.4),(2.5,3,.68),(3.5,2,.28)],
 [(0,0,.6),(1.5,0,.3),(2.25,1,.3),(3,0,.65)],
 [(0,0,.7),(1.5,7,.5),(2.5,10,.7),(3.5,7,.35)],
 [(0,0,1.3),(1.75,7,.65),(3,3,.8)],
 [(.5,0,.27),(1.25,0,.24),(2,12,.35),(2.75,10,.25),(3.5,7,.28)]
]
TONIC=[38,36,41,40,37]

def arrange(number,plan):
    s=engine.Song(number,plan)
    s.beats=128
    s.samples=round(s.beats*s.beat*RATE)
    s.mix=np.zeros((s.samples,2),np.float32)
    for bar in range(32):
        start=bar*4
        intro=bar<4
        breakdown=16<=bar<20
        build=20<=bar<24
        drop=not intro and not breakdown and not build
        root=TONIC[number]+[0,0,8,10][(bar//2)%4]
        # Minor harmony in the lower middle register, no nursery-style scalar tune.
        chord=[root+24,root+27,root+31,root+38]
        if number==1:chord=[root+24,root+31,root+36]
        if bar%2==0:
            for j,n in enumerate(chord):
                s.note('pad','pad',start,n,7.7,.040 if breakdown else .028,(-.65 if j%2 else .65))
        if number in [0,2,3] and (drop or breakdown):
            hook=RIFFS[number]
            for at,interval,gate in hook:
                if bar%4==3 and at>2.5:continue
                n=root+24+interval+(12 if number==2 and bar>=24 else 0)
                s.note('lead','lead',start+at,n,gate*(1.4 if breakdown else 1),.115 if breakdown else .16,-.06)
        if not breakdown:
            for at,interval,gate in RIFFS[number]:
                s.note('bass','bass',start+at,root+interval-(12 if interval>=10 else 0),gate,.25 if drop else .17)
            if number in [1,3]:
                for j,at in enumerate([0,.5,.75,1.5,2,2.75,3.5]):
                    s.note('chords','chug',start+at,root+12+(7 if j==5 else 0),.21,.12,-.4)
                    s.note('chords','chug',start+at+.009,root+19,.19,.08,.4)
            if number==4:
                for at in [.75,2.5]:
                    for j,n in enumerate(chord[:3]):s.note('chords','lead',start+at,n,.16,.063,(-.6 if j%2 else .6))
        if bar>=8 and not (breakdown and bar<18):
            for j in range(8 if number!=1 else 16):
                if intro or (breakdown and j%2):continue
                n=chord[[0,2,1,3,2,1,3,2][j%8]%len(chord)]+(12 if number==1 else 0)
                s.note('answer','arp',start+j*(.5 if number!=1 else .25),n,.18,.047,(-.45 if j%2 else .45))
        if breakdown:
            if bar%2==0:s.hit('kick',start,.26)
            continue
        if build:
            division=.5 if bar<22 else .25 if bar==22 else .125
            for j in range(round(4/division)):
                s.hit('snare',start+j*division,.08+.12*j/(4/division),(-.15 if j%2 else .15))
            if bar==23:
                # Noise riser, with a deliberate short breath before the drop.
                t=np.arange(round(3.6*s.beat*RATE))/RATE
                n=s.rng.normal(0,1,len(t)).astype(np.float32)
                n=(n-np.roll(n,1))*(t/max(t[-1],.001))**2*.13
                s.mix_note(n,start,.65,.15)
            continue
        if number==2:
            kicks=[0,1.75,2.5] if bar%2==0 else [0,1.5,2.75]
            snares=[1,3]
        elif number==1:
            kicks=[0,1.5,2.75];snares=[2]
        elif number==4:
            kicks=[0,1,2,3];snares=[1,3]
        elif number==3:
            kicks=[0,.75,2,2.5];snares=[1,3]
        else:
            kicks=[0,.75,2.5];snares=[1,3]
        for at in kicks:s.hit('kick',start+at,.38)
        for at in snares:
            s.hit('snare',start+at,.30 if drop else .21)
            if number in [0,4]:s.hit('rim',start+at+.008,.14)
        for j in range(16 if number in [0,2] else 8):
            if intro and j%2:continue
            at=j*(.25 if number in [0,2] else .5)
            s.hit('hat',start+at,.065 if j%2 else .1,(-.28 if j%2 else .28))
        for at in [.5,2.5]:s.hit('shaker',start+at,.09,.25)
        if bar in [4,8,12,24,28]:s.hit('conga',start,.16,.22)
        if bar%8==7:
            for j in range(6):s.hit('tom',start+2.5+j*.25,.16+j*.015,-.5+j*.2)
    return s

if __name__=='__main__':
    report=[]
    for i,plan in enumerate(PLANS):
        song=arrange(i,plan)
        stats=engine.finish(song)
        scorepath=ROOT/(plan['id']+'.score.json')
        score=json.loads(scorepath.read_text(encoding='utf-8'))
        score.update({'bars':32,'beats':128,'sections':['4-bar launch groove','12-bar first full section','4-bar breakdown','4-bar build','8-bar final section and loop return']})
        scorepath.write_text(json.dumps(score,indent=2),encoding='utf-8')
        stats['bars']=32
        report.append(stats)
        print(plan['title'],round(stats['seconds'],1),'seconds;',stats['events'],'events;',round(stats['peakDbFS'],2),'dBFS',flush=True)
        del song
    (ROOT/'Tracks.json').write_text(json.dumps({'method':'Original local electronic compositions and synthesis. No external samples. Revision emphasizes game-launch energy instead of cute mallet melodies.','recommended':PLANS[0]['id'],'tracks':report},indent=2),encoding='utf-8')

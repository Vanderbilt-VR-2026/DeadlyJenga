"""Four tactile coastal arrangements of the original Critical Mass hook."""
from pathlib import Path
import json
import numpy as np
import arrangement_base as base

ROOT=Path(__file__).resolve().parent
engine=base.engine
RATE=engine.RATE
TAU=2*np.pi
VARIANT=0
PLANS=[
 {'id':'01-critical-mass-shorebreak','title':'Critical Mass / Shorebreak','bpm':132,'direction':'Closest to the approved track: punchy breakbeat, warm elastic bass, hollow plastic lead and coastal air'},
 {'id':'02-critical-mass-candy-current','title':'Critical Mass / Candy Current','bpm':128,'direction':'Glossy candy identity: thick glass-resonator hook, rubbery low end and clacking tactile percussion'},
 {'id':'03-critical-mass-tidal-pressure','title':'Critical Mass / Tidal Pressure','bpm':132,'direction':'Ocean mayhem: deeper bass, weighty drums, muted metallic hook and wave-like swells'},
 {'id':'04-critical-mass-sunlit-chaos','title':'Critical Mass / Sunlit Chaos','bpm':124,'direction':'Warmer island energy: plucked string body, wide airy chords and syncopated hand percussion'}
]
original_drum=base.drum

def tone(kind,midi,gate,rng):
    f=440*2**((midi-69)/12)
    tail=.50 if kind=='pad' else .34 if kind=='lead' else .18
    t=np.arange(round((gate+tail)*RATE))/RATE
    p=TAU*f*t
    a=np.minimum(1,t/(.08 if kind=='pad' else .003))
    release=np.exp(-np.maximum(t-gate,0)/(.12 if kind=='pad' else .07))
    if kind=='bass':
        # Rounded fundamental with enough harmonics to retain the approved riff's weight.
        y=np.sin(p)+.31*np.sin(2*p)*np.exp(-t*3)+.14*np.sin(3*p)
        if VARIANT==2:y+=.24*np.sin(p*.5)
        if VARIANT==1:y+=.15*np.sin(p+.8*np.sin(p*2)*np.exp(-t*7))
        y=np.tanh(y*1.25)*.83
    elif kind=='lead':
        if VARIANT==0:
            # Hollow resonant plastic, struck attack followed by a fuller sustained core.
            y=np.sin(p+.65*np.sin(2*p)*np.exp(-t*10))+.18*np.sin(3*p)+.10*np.sin(4.08*p)*np.exp(-t*18)
            y*=.72+.28*np.exp(-t*5)
        elif VARIANT==1:
            # Low glass/candy resonances, not a high-pitched bell melody.
            y=np.sin(p)+.24*np.sin(2.003*p)*np.exp(-t*5)+.12*np.sin(3.97*p)*np.exp(-t*13)
            y+=.17*np.sin(p*.998)*(1-np.exp(-t*14))
        elif VARIANT==2:
            y=np.sin(p+.4*np.sin(3*p)*np.exp(-t*7))+.26*np.sin(2*p)+.08*np.sin(5*p)*np.exp(-t*14)
        else:
            y=sum(np.sin(p*k)*np.exp(-t*(1.5+k*.9))/k**1.5 for k in range(1,10))
            y+=.27*np.sin(p)*(1-np.exp(-t*10))
        y*=.82
    elif kind=='pad':
        y=(np.sin(p*.998)+np.sin(p*1.002)+.18*np.sin(2*p))*.32
        y*=.8+.2*np.sin(TAU*.24*t+.3)
    elif kind=='arp':
        y=(np.sin(p+.30*np.sin(p*2)*np.exp(-t*9))+.10*np.sin(p*3))*np.exp(-t*6)*.8
    else:
        y=(np.sin(p)+.14*np.sin(2.76*p)*np.exp(-t*18))*np.exp(-t*8)
    return (y*a*release*np.minimum(1,(gate+tail-t)/.018)).astype(np.float32)

def drum(kind,rng):
    y=original_drum(kind,rng)
    t=np.arange(len(y))/RATE
    if kind=='snare':
        # Less industrial crack; retain the punch and add a hollow physical body.
        y=.73*y+.26*np.sin(TAU*224*t)*np.exp(-t*30)
    elif kind=='kick':
        y*=.93
    elif kind in ['hat','shaker','conga']:
        # Tame the techno hiss and replace some with clattery object resonances.
        y*=.55 if kind=='hat' else .72
        if kind=='shaker':y+=(np.sin(TAU*1830*t)+.35*np.sin(TAU*2713*t))*np.exp(-t*65)*.10
    elif kind=='tom':
        y=.68*y+.28*np.sin(TAU*186*t+.5*np.sin(TAU*463*t)*np.exp(-t*22))*np.exp(-t*16)
    elif kind=='rim':
        y=.48*y+(np.sin(TAU*610*t)+.3*np.sin(TAU*940*t))*np.exp(-t*75)*.25
    return y.astype(np.float32)

engine.tone=tone
engine.drum=drum

def add_ocean(s,index):
    rng=np.random.default_rng(8192+index)
    # Original colored-noise surf: smooth and periodic so no recording or loop-tail cut is needed.
    n=rng.normal(0,1,s.samples+256).astype(np.float32)
    cumulative=np.concatenate(([0.],np.cumsum(n,dtype=np.float64)))
    low=(cumulative[128:]-cumulative[:-128])/128
    low=low[:s.samples]
    t=np.arange(s.samples)/RATE
    swell=(.5-.5*np.cos(TAU*t/(s.samples/RATE)*8))**2
    sea=low*(.018 if index!=2 else .032)*swell
    s.mix[:,0]+=sea
    s.mix[:,1]+=np.roll(sea,round(.017*RATE))
    # Sparse tuned object details live around the core riff, never replace it.
    for bar in range(32):
        if 16<=bar<20:continue
        root=base.TONIC[0]+[0,0,8,10][(bar//2)%4]
        if index in [0,3]:
            for at in [1.5,3.25]:
                s.hit('tom',bar*4+at,.065 if index==0 else .11,.35)
        if index==1 and bar%2:
            for j,at in enumerate([2.75,3.5]):s.note('plastic','arp',bar*4+at,root+31+j*3,.18,.055,-.35)
        if index==3:
            for at in [.5,2.5]:
                for j,note in enumerate([root+24,root+27,root+31]):
                    s.note('chords','chug',bar*4+at+j*.018,note,.32,.048,(-.5 if j%2 else .5))
    return s

if __name__=='__main__':
    report=[]
    for i,plan in enumerate(PLANS):
        VARIANT=i
        song=add_ocean(base.arrange(0,plan),i)
        stats=engine.finish(song)
        scorepath=ROOT/(plan['id']+'.score.json')
        score=json.loads(scorepath.read_text(encoding='utf-8'))
        score.update({'bars':32,'beats':128,'sections':['4-bar launch groove','12-bar main section','4-bar breakdown','4-bar build','8-bar return'],'additionalSynthesis':'Low-level procedural periodic surf bed; see compose_coastal.py.'})
        scorepath.write_text(json.dumps(score,indent=2),encoding='utf-8')
        stats['bars']=32
        report.append(stats)
        print(plan['title'],round(stats['seconds'],1),'seconds;',round(stats['peakDbFS'],2),'dBFS',flush=True)
        del song
    (ROOT/'Designs.json').write_text(json.dumps({'method':'Four original tactile coastal variations of the approved Critical Mass composition. No external samples.','tracks':PLANS},indent=2),encoding='utf-8')
    (ROOT/'Tracks.json').write_text(json.dumps({'method':'Locally composed and synthesized. Critical Mass hook and breakbeat retained, with redesigned timbres and coastal texture.','recommended':PLANS[0]['id'],'tracks':report},indent=2),encoding='utf-8')

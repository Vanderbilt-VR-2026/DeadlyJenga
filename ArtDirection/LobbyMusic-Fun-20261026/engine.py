"""Five original menu compositions and stereo synthesis. No external music samples.
Uses bundled numpy and FFmpeg; does not modify Unity or its audio bank.
"""
from pathlib import Path
import hashlib, json, math, shutil, struct, subprocess, wave
import numpy as np

ROOT = Path(__file__).resolve().parent
RATE = 48000
TAU = 2 * np.pi
FFMPEG = shutil.which('ffmpeg')
PLANS = json.loads((ROOT / 'Designs.json').read_text(encoding='utf-8'))['tracks']

# Scale degrees are relative to a tonic. Negative degrees extend below it.
SCALES = [[0,2,4,6,7,9,10],[0,2,3,5,7,9,10],[0,2,4,6,7,9,11],[0,2,3,5,7,8,11],[0,2,4,5,7,9,11]]
TONICS = [74,65,69,72,70]
CHORDS = [
 [(38,[62,66,69,73]),(35,[62,66,69,71]),(43,[62,67,69,71]),(45,[61,64,67,71]),(42,[62,66,69,74]),(40,[62,64,67,71]),(43,[62,67,69,74]),(45,[61,64,67,69])],
 [(41,[60,63,65,68]),(46,[62,65,68,72]),(39,[58,62,65,67]),(36,[58,60,63,67]),(41,[60,63,65,67]),(43,[62,65,67,70]),(46,[60,62,65,68]),(36,[58,60,64,67])],
 [(45,[61,64,68,71]),(47,[63,66,70,73]),(42,[61,64,68,69]),(40,[59,64,66,68]),(49,[59,61,64,68]),(47,[61,63,66,73]),(45,[61,64,68,71]),(40,[59,62,64,68])],
 [(36,[60,63,67,70]),(44,[60,63,67,68]),(41,[60,65,68,72]),(43,[59,62,65,67]),(39,[58,63,67,70]),(46,[62,65,70,72]),(41,[60,65,68,72]),(43,[59,62,65,67])],
 [(34,[60,62,65,67]),(43,[58,62,65,69]),(39,[58,62,65,67]),(41,[57,60,63,67]),(38,[60,62,65,69]),(43,[58,62,65,69]),(36,[58,60,63,67]),(41,[57,60,63,65])]
]
# Each row is one bar of a four-bar original hook: beat, scale degree, gate.
MELODIES = [
 [[(0,0,.45),(.75,2,.22),(1,4,.5),(1.75,2,.25),(2.5,1,.4),(3.25,0,.45)],[(.25,1,.35),(1,2,.4),(1.75,4,.25),(2.5,5,.5),(3.25,4,.4)],[(0,2,.55),(1,1,.25),(1.5,0,.5),(2.5,-1,.3),(3.25,1,.35)],[(0,2,.4),(.75,1,.3),(1.5,-1,.4),(2.5,0,.7)]],
 [[(.25,0,.25),(.75,4,.25),(1.5,3,.3),(2.25,2,.2),(3,0,.5)],[(0,-1,.3),(.75,0,.25),(1.5,2,.45),(2.5,1,.25),(3.25,-1,.3)],[(.25,0,.2),(.75,2,.25),(1.5,4,.25),(2,5,.3),(3,4,.5)],[(0,3,.35),(1,2,.3),(1.75,1,.25),(2.5,-1,.25),(3.25,0,.35)]],
 [[(0,0,.9),(1.5,1,.5),(2.5,4,1)],[(.5,3,.6),(1.5,4,.65),(3,1,.75)],[(0,2,.85),(1.5,1,.6),(2.75,-1,.65)],[(0,0,.75),(1.25,4,.5),(2.5,2,.45),(3.25,1,.35)]],
 [[(0,0,.25),(.5,4,.25),(1.25,3,.25),(1.75,2,.35),(2.5,1,.25),(3.25,0,.35)],[(0,2,.25),(.5,3,.25),(1,4,.35),(2,6,.3),(2.75,4,.25),(3.5,3,.25)],[(0,2,.4),(.75,0,.3),(1.5,-1,.3),(2.25,-2,.35),(3,0,.35)],[(0,-1,.25),(.75,1,.3),(1.5,3,.25),(2.25,2,.3),(3,0,.5)]],
 [[(.25,0,.4),(1,2,.35),(1.75,4,.65),(3,2,.5)],[(.5,1,.35),(1.25,0,.4),(2.25,-2,.35),(3,0,.5)],[(.25,2,.4),(1,4,.4),(2,5,.6),(3,4,.5)],[(0,3,.4),(1,2,.35),(1.75,1,.35),(2.75,-1,.3),(3.5,0,.25)]]
]
LEADS = ['marimba','reed','glass','marimba','vibes']
SECOND = ['rhodes','marimba','rhodes','pizz','guitar']
GM = {'marimba':12,'reed':71,'glass':10,'vibes':11,'rhodes':4,'pizz':45,'guitar':28,'bass':33,'pad':89,'horn':60,'plastic':115}
PANS = {'lead':-.06,'answer':.28,'chords':-.25,'bass':0,'pad':0,'horn':.13,'drums':0,'plastic':-.32}

def degree(track, value):
    octave, n = divmod(value, 7)
    return TONICS[track] + SCALES[track][n] + octave * 12

def tone(kind, midi, gate, rng):
    frequency = 440 * 2 ** ((midi-69)/12)
    tail = {'marimba':1.35,'vibes':1.9,'glass':2.2,'rhodes':.65,'pad':.8,'reed':.13,'horn':.18,'pizz':.28,'guitar':.3,'bass':.12}.get(kind,.15)
    duration = gate + tail
    t = np.arange(round(duration * RATE), dtype=np.float64) / RATE
    attack = min(.035, gate*.2) if kind in ['pad','reed','horn'] else .005
    a = np.minimum(1,t/max(attack,.001))
    release = np.exp(-np.maximum(t-gate,0)/max(.015,tail/5))
    phase = TAU*frequency*t
    if kind == 'marimba':
        y = np.sin(phase)*np.exp(-t*4.3) + .28*np.sin(phase*2.76)*np.exp(-t*17) + .06*np.sin(phase*5.4)*np.exp(-t*38)
    elif kind == 'vibes':
        y = (np.sin(phase)+.18*np.sin(phase*3)*np.exp(-t*5))*np.exp(-t*1.9)*(1+.035*np.sin(TAU*4.6*t))
    elif kind == 'glass':
        y = (np.sin(phase + .45*np.sin(phase*2)*np.exp(-t*6)) + .09*np.sin(phase*4.01)*np.exp(-t*11))*np.exp(-t*2)
    elif kind == 'rhodes':
        y = np.sin(phase + .7*np.sin(phase*2)*np.exp(-t*4))*np.exp(-t*2.9) + .13*np.sin(phase*.5)*np.exp(-t*4)
    elif kind in ['pizz','guitar']:
        y = sum((1/k**1.8)*np.sin(phase*k)*np.exp(-t*(4+k*1.6)) for k in range(1,7))
        y *= .9
    elif kind == 'bass':
        y = (np.sin(phase)+.25*np.sin(phase*2)+.06*np.sin(phase*3))*np.exp(-t*1.1)
    elif kind in ['reed','horn']:
        vibrato = .004*np.sin(TAU*4.8*t)*np.minimum(t*3,1)
        y = np.sin(phase+vibrato*frequency) + .20*np.sin(phase*3+vibrato*frequency) + .045*np.sin(phase*5)
        if kind == 'horn': y += .16*np.sin(phase*2)
        y *= .58
    elif kind == 'pad':
        y = (np.sin(phase)+np.sin(phase*1.0018+.3)+.18*np.sin(phase*2))*.32
        a = np.minimum(1,t/.14)
    else:
        y = np.sin(phase + 1.2*np.sin(phase*2.03)*np.exp(-t*25))*np.exp(-t*13)
    end = np.minimum(1,np.maximum(duration-t,0)/.035)
    return (y*a*release*end).astype(np.float32)

def drum(kind, rng):
    duration = {'kick':.38,'snare':.23,'hat':.075,'rim':.105,'shaker':.10,'tom':.24,'conga':.22}[kind]
    t = np.arange(round(duration*RATE))/RATE
    noise = rng.normal(0,1,len(t)).astype(np.float32)
    soft = (noise+np.roll(noise,1)+np.roll(noise,2)+np.roll(noise,3))*.25
    if kind == 'kick':
        phase=TAU*(49*t+72*.025*(1-np.exp(-t/.025)))
        y=np.sin(phase)*np.exp(-t*12)+soft*.06*np.exp(-t*120)
    elif kind == 'snare':
        y=(soft*.7+np.sin(TAU*173*t)*.25)*np.exp(-t*22)
    elif kind == 'hat':
        y=(noise-soft)*np.exp(-t*90)*.28
    elif kind == 'shaker':
        y=(noise-soft)*np.sin(np.pi*t/duration)**.8*.17
    elif kind == 'rim':
        y=(np.sin(TAU*720*t)+.4*np.sin(TAU*1127*t))*np.exp(-t*65)*.42+soft*.14*np.exp(-t*90)
    elif kind == 'tom':
        y=np.sin(TAU*(110*t+30*.035*(1-np.exp(-t/.035))))*np.exp(-t*19)*.75
    else:
        y=(np.sin(TAU*235*t)+.24*np.sin(TAU*370*t))*np.exp(-t*25)*.55
    return (y*np.minimum(1,t/.002)*np.minimum(1,(duration-t)/.009)).astype(np.float32)

class Song:
    def __init__(self, number, plan):
        self.number=number; self.plan=plan; self.beat=60/plan['bpm'];self.beats=160
        self.samples=round(self.beats*self.beat*RATE)
        self.mix=np.zeros((self.samples,2),dtype=np.float32)
        self.events=[]; self.rng=np.random.default_rng(1731+number*319);self.cache={}
    def mix_note(self, signal, beat, amp, pan):
        start=round(beat*self.beat*RATE)%self.samples
        gains=np.array([math.cos((pan+1)*math.pi/4),math.sin((pan+1)*math.pi/4)],dtype=np.float32)*amp
        at=0
        while at<len(signal):
            count=min(self.samples-start,len(signal)-at)
            self.mix[start:start+count] += signal[at:at+count,None]*gains
            at+=count;start=0
    def note(self, part, kind, beat, midi, gate, amp, pan=None):
        if pan is None:pan=PANS.get(part,0)
        key=(kind,midi,round(gate*self.beat,3))
        if key not in self.cache:self.cache[key]=tone(kind,midi,key[2],self.rng)
        self.mix_note(self.cache[key],beat,amp,pan)
        self.events.append({'part':part,'instrument':kind,'beat':round(beat,4),'midi':midi,'gate':round(gate,4),'velocity':max(1,min(120,round(amp*340))),'pan':pan})
    def hit(self, kind, beat, amp, pan=0):
        key=('drum',kind)
        if key not in self.cache:self.cache[key]=drum(kind,self.rng)
        self.mix_note(self.cache[key],beat,amp,pan)
        self.events.append({'part':'drums','instrument':kind,'beat':round(beat,4),'midi':{'kick':36,'snare':38,'hat':42,'rim':37,'shaker':70,'tom':47,'conga':64}[kind],'gate':.12,'velocity':max(1,min(120,round(amp*380))),'pan':pan})

def arrange(number, plan):
    s=Song(number,plan)
    for bar in range(40):
        section=bar//8; local=bar%8; start=bar*4
        root,chord=CHORDS[number][bar%8]
        light=section==2
        # A voiced chord bed and short offset plucks, with real space between hits.
        for j,midi in enumerate(chord):
            if number==2:
                s.note('chords','rhodes',start+j*.035,midi,2.6,.062,(-.38 if j%2 else .38))
            else:
                positions=[.5,2.5] if number!=1 else [.75,2.25,3.5]
                for b in positions:
                    s.note('chords',SECOND[number],start+b+j*.022,midi,.55 if number!=3 else .30,.047 if light else .058,(-.35 if j%2 else .35))
            if number in [0,2,4] and (section in [1,2,3]):
                s.note('pad','pad',start+j*.015,midi,3.8,.017,(-.5 if j%2 else .5))
        bass=[(0,root,.72),(1.5,root+7,.35),(2.5,root,.65),(3.5,root+12,.30)]
        if number==1:bass=[(0,root,.4),(.75,root+12,.23),(1.5,root+7,.35),(2.25,root,.4),(3,root+10,.25),(3.5,root+7,.3)]
        if number==2:bass=[(0,root,1.4),(2.5,root+7,.8)]
        if number==3:bass=[(0,root,.55),(1,root,.3),(1.75,root+7,.3),(2.5,root,.55),(3.5,root+7,.3)]
        if number==4:bass=[(0,root,.75),(1.75,root+7,.3),(2.5,root+12,.45),(3.5,root+7,.3)]
        for at,midi,gate in bass:s.note('bass','bass',start+at,midi,gate,.22)
        # Hooks repeat as recognizable phrases, with a contrasting eight-bar B section.
        phrase=MELODIES[number][bar%4]
        for j,(at,d,gate) in enumerate(phrase):
            if section==2:
                d=2-d if number in [0,2,4] else d-2
                if j%3==2:continue
            if section==4 and local in [1,3,5] and j>2:continue
            midi=degree(number,d)
            if number==1 and section in [1,3] and bar%2:midi+=12
            if at in [0,.25] and j==0 and bar%4!=0:
                # Anchor a phrase opening to its current harmony, retain the hook contour.
                options=[n+12*k for n in chord for k in [-1,0,1]]
                midi=min(options,key=lambda n:abs(n-midi))
            time=start+at+float(s.rng.uniform(-.006,.006))
            if at%.5==.0 and at%1>.1:time+=.035 if number in [0,1,4] else 0
            s.note('lead',LEADS[number],time,midi,gate,.175*float(s.rng.uniform(.92,1.04)))
        if section in [1,3] and bar%2==1:
            response=[chord[1]+12,chord[2]+12,chord[0]+12]
            for j,n in enumerate(response):s.note('answer','marimba' if number!=3 else 'pizz',start+2.65+j*.35,n,.28,.073,.29)
        if number==3 and section in [1,3]:
            for j,n in enumerate([chord[0],chord[2],chord[1],chord[2]]):s.note('answer','pizz',start+j*.75,n,.25,.065,-.38)
            if local in [3,7]:s.note('horn','horn',start+.25,chord[2],1.2,.075)
        if number==0 and section==3 and local in [1,5]:s.note('horn','horn',start+.5,chord[1]+12,.55,.055)
        # Original dry percussion, deliberately softer than the tune.
        kicks=[0,2] if number==4 else [0,2.5] if number==2 else [0,1.5,2.75] if number==3 else [0,1.75,2.5]
        for at in kicks:s.hit('kick',start+at,.19 if light else .23)
        for at in ([2] if number==2 else [1,3]):s.hit('snare' if number==3 else 'rim',start+at,.13 if number==3 else .16)
        for j in range(8):
            if light and j%2==0:continue
            s.hit('hat' if j%2==0 else 'shaker',start+j*.5+(j%2)*(.045 if number!=3 else .01),.095 if j%2 else .055, -.22 if j%2 else .24)
        if number in [0,4]:
            for at in [1.5,3.5]:s.hit('conga',start+at,.09,.25)
        if local==7:
            for j,at in enumerate([2.75,3.25,3.5]):s.hit('tom',start+at,.065+j*.014, -.3+j*.25)
        if bar%4==3:
            s.note('plastic','plastic',start+3.65,chord[2]+12,.13,.045,-.35)
    return s

def variable(value):
    data=[value&127];value>>=7
    while value:data.insert(0,(value&127)|128);value>>=7
    return bytes(data)

def midi_file(song,path):
    ppq=480;tempo=round(60_000_000/song.plan['bpm']);end=round(song.beats*ppq)
    chunks=[]
    def track(events):
        events=sorted(events,key=lambda e:(e[0],e[1][0]>>4!=8))
        output=bytearray();previous=0
        for tick,event in events:
            output+=variable(max(0,tick-previous))+event;previous=tick
        output+=variable(max(0,end-previous))+b'\xff\x2f\x00'
        return b'MTrk'+struct.pack('>I',len(output))+output
    chunks.append(track([(0,b'\xff\x51\x03'+tempo.to_bytes(3,'big')),(0,b'\xff\x58\x04\x04\x02\x18\x08')]))
    for channel,part in enumerate(['lead','answer','chords','bass','pad','horn','plastic','drums']):
        selected=[e for e in song.events if e['part']==part]
        if not selected:continue
        ch=9 if part=='drums' else channel
        events=[(0,b'\xff\x03'+variable(len(part))+part.encode()),(0,bytes([0xb0|ch,10,round((PANS.get(part,0)+1)*63.5)]))]
        if ch!=9:events.append((0,bytes([0xc0|ch,GM[selected[0]['instrument']]])))
        for e in selected:
            tick=round((e['beat']%song.beats)*ppq);stop=min(end,tick+round(e['gate']*ppq))
            events += [(tick,bytes([0x90|ch,e['midi'],e['velocity']])),(stop,bytes([0x80|ch,e['midi'],0]))]
        chunks.append(track(events))
    path.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(chunks),ppq)+b''.join(chunks))

def finish(song):
    dry=song.mix
    # Circular early reflections and diffuse taps retain the musical loop tail.
    wet=np.zeros_like(dry)
    for index,ms in enumerate([23,37,53,71,97,127,163,211,263,317,383,457]):
        gain=.065*math.exp(-ms/210)
        wet += np.roll(dry[:,::-1] if index%2 else dry,round(ms*RATE/1000),axis=0)*gain
    delay=round(song.beat*.75*RATE)
    wet += np.roll(dry[:,::-1],delay,axis=0)*(.075 if song.number==2 else .035)
    mix=dry+wet;mix-=mix.mean(axis=0)
    rms=float(np.sqrt(np.mean(mix**2)));mix*=.12/max(rms,1e-9)
    mix=np.tanh(mix*1.08)/1.08
    peak=float(np.max(np.abs(mix)))
    if peak>.83:mix*=.83/peak
    base=ROOT/song.plan['id'];raw=base.with_suffix('.f32')
    mix.astype('<f4').tofile(raw)
    meta=['-metadata','title='+song.plan['title'],'-metadata','artist=Deadly Jenga - original synthesized menu demo']
    subprocess.run([FFMPEG,'-hide_banner','-loglevel','error','-y','-f','f32le','-ar',str(RATE),'-ac','2','-i',str(raw),*meta,'-c:a','pcm_s24le',str(base.with_suffix('.wav'))],check=True)
    subprocess.run([FFMPEG,'-hide_banner','-loglevel','error','-y','-i',str(base.with_suffix('.wav')),*meta,'-c:a','libmp3lame','-b:a','192k',str(base.with_suffix('.mp3'))],check=True)
    raw.unlink()
    midi_file(song,base.with_suffix('.mid'))
    (base.with_suffix('.score.json')).write_text(json.dumps({'title':song.plan['title'],'bpm':song.plan['bpm'],'bars':40,'beats':160,'sections':['A - opening hook','A2 - playful variation','B - lighter contrast','A3 - full return','A4 - breathing room and loop pickup'],'events':song.events},indent=2),encoding='utf-8')
    delta=float(np.max(np.abs(mix[0]-mix[-1])))
    stats={'id':song.plan['id'],'title':song.plan['title'],'direction':song.plan['direction'],'bpm':song.plan['bpm'],'seconds':song.samples/RATE,'bars':40,'sampleRate':RATE,'channels':2,'peakDbFS':20*math.log10(float(np.max(np.abs(mix)))+1e-12),'rmsDbFS':20*math.log10(float(np.sqrt(np.mean(mix**2)))),'loopBoundaryDelta':delta,'events':len(song.events),'files':{ext:base.with_suffix('.'+ext).name for ext in ['wav','mp3','mid']}}
    assert np.isfinite(mix).all() and stats['peakDbFS']< -1.5
    assert float(np.std(mix[:,0]-mix[:,1]))>.002
    assert delta<.035,('loop discontinuity',delta)
    stats['sha256']=hashlib.sha256(base.with_suffix('.wav').read_bytes()).hexdigest()
    return stats

if __name__=='__main__':
    report=[]
    for i,plan in enumerate(PLANS):
        song=arrange(i,plan);stats=finish(song);report.append(stats)
        print(plan['title'],round(stats['seconds'],2),'seconds;',stats['events'],'events; peak',round(stats['peakDbFS'],2),'dBFS; seam',round(stats['loopBoundaryDelta'],5),flush=True)
        del song
    (ROOT/'Tracks.json').write_text(json.dumps({'method':'Original local composition and deterministic stereo synthesis; no successful cloud music generations or downloaded music/samples. MIDI files approximate synth timbres with General MIDI patch assignments.','recommended':'01-candy-catastrophe','tracks':report},indent=2),encoding='utf-8')

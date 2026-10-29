"""Bright, rhythm-led original lobby music revisions. No sampled music."""
from pathlib import Path
import json
import compose_coastal as coastal

ROOT=Path(__file__).resolve().parent
engine=coastal.engine
PLANS=[
 {'id':'01-bubblegum-boardwalk','title':'Bubblegum Boardwalk','bpm':124,'direction':'Recommended: bright major-key coastal breakbeat, chunky rubber bass and a confident plastic riff'},
 {'id':'02-ready-set-splash','title':'Ready, Set, Splash','bpm':128,'direction':'A playful ready-up groove: punchy dance drums, glassy chord stabs and cheeky rhythmic answers'},
 {'id':'03-plastic-paradise','title':'Plastic Paradise','bpm':120,'direction':'Sunny island party: plucked rhythmic chords, percussive call-and-response and a lively bass line'}
]
HOOKS=[
 [[(0,0,.4),(.75,0,.25),(1.5,7,.45),(2.5,9,.6),(3.5,7,.25)],[(.5,4,.6),(1.5,2,.35),(2.5,0,.8)],[(0,0,.3),(.75,7,.35),(1.5,9,.5),(2.75,12,.65)],[(.25,9,.45),(1.25,7,.4),(2,4,.55),(3,2,.45)]],
 [[(.5,0,.25),(1.25,0,.3),(2,7,.4),(2.75,9,.3),(3.5,7,.3)],[(0,4,.65),(1.75,2,.3),(2.5,4,.7)],[(.5,7,.4),(1.5,9,.5),(2.75,12,.6)],[(0,9,.4),(.75,7,.35),(2,4,.6),(3.25,2,.3)]],
 [[(.25,0,.45),(1,4,.4),(2,7,.7),(3.25,4,.3)],[(.5,2,.4),(1.5,4,.5),(3,0,.7)],[(.25,4,.4),(1.25,7,.5),(2.5,9,.65)],[(0,7,.45),(1,4,.4),(2,2,.45),(3,0,.65)]]
]

def arrange(i,plan):
    coastal.VARIANT=[0,1,3][i]
    s=engine.Song(0,plan)
    for bar in range(40):
        at=bar*4
        # Major and dominant harmony; the bass supplies motion instead of a moody pad bed.
        root=[38,41,36][i]+[0,5,7,0,9,5,7,0][(bar//2)%8]
        minor=(bar//2)%8==4
        chord=[root+24,root+(27 if minor else 28),root+31,root+38]
        light=16<=bar<24
        for j,n in enumerate(chord):
            for beat in ([.5,2.5] if i!=1 else [.75,2.25,3.5]):
                s.note('chords','lead' if i==1 else 'chug',at+beat+j*.015,n,.22 if i==1 else .32,.048 if not light else .035,(-.48 if j%2 else .48))
        bass=[(0,0,.52),(.75,12,.24),(1.5,7,.32),(2.5,0,.48),(3.25,7,.22),(3.75,12,.17)]
        if i==2:bass=[(0,0,.65),(1.5,7,.3),(2.25,12,.35),(3,7,.3),(3.5,4,.3)]
        for beat,n,gate in bass:s.note('bass','bass',at+beat,root+n,gate,.245)
        for j,(beat,n,gate) in enumerate(HOOKS[i][bar%4]):
            if light and j%2:continue
            if bar%8==7 and beat>2.5:continue
            s.note('lead','lead',at+beat,root+24+n,gate,.135 if not light else .105,-.06)
        if bar%2 and not light:
            for j,(beat,n) in enumerate([(2.75,7),(3.5,4)]):s.note('answer','arp',at+beat,root+36+n,.23,.067,.38)
        if bar in [8,10,12,14,24,26,28,30,32,34]:
            for j,n in enumerate(chord[:3]):s.note('pad','pad',at,n,3.5,.018,(-.6 if j%2 else .6))
        kicks=[0,1.75,2.5] if i==0 else [0,1,2,3] if i==1 else [0,.75,2.5]
        for beat in kicks:s.hit('kick',at+beat,.34 if not light else .27)
        for beat in [1,3]:
            s.hit('snare',at+beat,.24 if not light else .18)
            s.hit('rim',at+beat+.012,.11 if i!=2 else .14)
        for j in range(8):
            s.hit('hat',at+j*.5,.095 if j%2 else .055,(-.23 if j%2 else .23))
            if i==0 and not light and j%2:s.hit('hat',at+j*.5+.25,.037,.35)
        for beat in [.75,2.25,3.5]:s.hit('shaker',at+beat,.085,.28)
        if i==2:
            for beat in [1.5,2.75,3.75]:s.hit('tom',at+beat,.11,-.28)
        elif i==0:
            for beat in [1.5,3.5]:s.hit('tom',at+beat,.075,-.3)
        if bar%8==7:
            for j,beat in enumerate([2.75,3.25,3.5,3.75]):s.hit('tom',at+beat,.10+j*.018,-.4+j*.23)
    return s

if __name__=='__main__':
    report=[]
    for i,plan in enumerate(PLANS):
        song=arrange(i,plan);stats=engine.finish(song)
        path=ROOT/(plan['id']+'.score.json');score=json.loads(path.read_text(encoding='utf-8'))
        score['sections']=['8-bar full groove','8-bar answering phrases','8-bar lighter rhythm section','8-bar full return','8-bar loop continuation']
        path.write_text(json.dumps(score,indent=2),encoding='utf-8')
        report.append(stats);print(plan['title'],round(stats['seconds'],1),'seconds;',round(stats['peakDbFS'],2),'dBFS',flush=True)
        del song
    (ROOT/'Tracks.json').write_text(json.dumps({'method':'Original bright lobby revision following feedback that the earlier tracks were too depressing. New major-key compositions and locally synthesized instruments. No external samples. Not installed as lobby music.','recommended':PLANS[0]['id'],'tracks':report},indent=2),encoding='utf-8')
    (ROOT/'Designs.json').write_text(json.dumps({'tracks':PLANS},indent=2),encoding='utf-8')

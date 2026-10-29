"""Original lower-intensity lobby grooves in the Shorebreak sound palette."""
from pathlib import Path
import json
import compose_coastal as coastal

engine=coastal.engine
ROOT=Path(__file__).resolve().parent
PLANS=[
 {'id':'01-dockside-momentum','title':'Dockside Momentum','bpm':116,'direction':'Recommended lobby direction: a warm coastal breakbeat, rubber bass and restrained plastic hook'},
 {'id':'02-plastic-palms','title':'Plastic Palms','bpm':108,'direction':'Social island groove: plucked chords, hollow clacks and relaxed bass syncopation'},
 {'id':'03-ready-when-you-are','title':'Ready When You Are','bpm':124,'direction':'A more competitive lobby pulse: crisp backbeat, candy-glass accents and patient low-end drive'}
]
HOOKS=[
 [[(0,0,.8),(1.5,7,.4),(3,3,.65)],[(.75,2,.6),(2.5,0,1)],[(0,0,.5),(1.25,3,.7),(3,7,.7)],[(.5,5,.6),(2,3,.8),(3.25,2,.45)]],
 [[(.5,0,.6),(2,7,.7)],[(0,3,.8),(2.75,2,.6)],[(.75,0,.75),(2.5,10,.55)],[(.5,7,.75),(2,3,1)]],
 [[(0,0,.45),(.75,0,.25),(2,7,.7)],[(1,3,.8),(3,2,.5)],[(0,0,.65),(1.5,7,.4),(2.75,10,.5)],[(.5,7,.65),(2.5,3,.6)]]
]

def arrange(index,plan):
    coastal.VARIANT=[0,3,1][index]
    s=engine.Song(0,plan)
    # Keep 40 bars for longer, less insistent lobby phrasing.
    for bar in range(40):
        at=bar*4
        quiet=16<=bar<24
        root=[38,41,36][index]+[0,0,8,10,0,0,5,10][(bar//2)%8]
        chord=[root+24,root+27,root+31,root+38]
        if bar%2==0:
            for j,n in enumerate(chord):s.note('pad','pad',at,n,7.7,.025,(-.6 if j%2 else .6))
        for step,n,gate in [(0,0,.70),(1.75,7,.3),(2.5,0,.65),(3.5,10,.22)]:
            if quiet and step==3.5:continue
            s.note('bass','bass',at+step,root+n,gate,.22 if not quiet else .17)
        if bar%8<6 and not (quiet and bar%2):
            for step,n,gate in HOOKS[index][bar%4]:
                s.note('lead','lead',at+step,root+24+n,gate,.105 if not quiet else .073,-.08)
        if index==1 or bar%4 in [1,3]:
            for step in [.5,2.5]:
                for j,n in enumerate(chord[:3]):s.note('chords','chug',at+step+j*.018,n,.4,.044,(-.45 if j%2 else .45))
        if not quiet and 8<=bar<36 and bar%2:
            for j,step in enumerate([2.75,3.5]):s.note('answer','arp',at+step,chord[[1,2][j]],.25,.044,.4)
        kicks=[0,2.5] if index==1 else [0,1.75,2.5] if index==0 else [0,2,2.75]
        for step in kicks:s.hit('kick',at+step,.28 if not quiet else .22)
        for step in [1,3]:s.hit('snare',at+step,.18 if not quiet else .11)
        for j in range(8):
            if quiet and j%2==0:continue
            s.hit('hat',at+j*.5,.075 if j%2 else .045,(-.25 if j%2 else .25))
        for step in [1.5,3.5]:s.hit('shaker',at+step,.07,.28)
        if index in [0,1]:
            for step in [.75,2.25]:s.hit('tom',at+step,.06,-.30)
        if bar%8==7:
            for j,step in enumerate([3,3.5,3.75]):s.hit('rim',at+step,.055+j*.014,-.2+j*.2)
    # A subtle synthesized sea wash belongs to the arrangement, not a loud SFX layer.
    s=coastal.add_ocean(s,2 if index==0 else 0)
    return s

if __name__=='__main__':
    report=[]
    for i,plan in enumerate(PLANS):
        song=arrange(i,plan)
        stats=engine.finish(song)
        path=ROOT/(plan['id']+'.score.json')
        score=json.loads(path.read_text(encoding='utf-8'))
        score['sections']=['8-bar entrance groove','8-bar development','8-bar breathing room','8-bar return','8-bar relaxed loop pickup']
        score['additionalSynthesis']='Procedural periodic surf bed, see compose_coastal.py.'
        path.write_text(json.dumps(score,indent=2),encoding='utf-8')
        report.append(stats)
        print(plan['title'],round(stats['seconds'],1),'seconds;',round(stats['peakDbFS'],2),'dBFS',flush=True)
        del song
    (ROOT/'Tracks.json').write_text(json.dumps({'method':'Three original locally composed and synthesized lobby proposals. No external samples. Not installed as lobby music.','recommended':PLANS[0]['id'],'tracks':report},indent=2),encoding='utf-8')
    (ROOT/'Designs.json').write_text(json.dumps({'tracks':PLANS},indent=2),encoding='utf-8')

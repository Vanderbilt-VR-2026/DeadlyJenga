"""20-second concept animatic: generated artwork + original animated editorial overlays.
Not a Unity gameplay recording. Uses the project's existing sound library.
Run with bundled Python (Pillow/numpy) and the installed File Converter ffmpeg.
"""
from pathlib import Path
import math, json, subprocess, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[2]
FFMPEG=Path(r'C:\Program Files\File Converter\ffmpeg.exe')
W,H,FPS,DURATION=1280,720,30,20
RED=(255,54,77); BLUE=(36,124,255); MINT=(94,255,210); INK=(18,15,43)
BOLD=r'C:\Windows\Fonts\segoeuib.ttf'
REG=r'C:\Windows\Fonts\seguisb.ttf'
fonts={}
def font(size,bold=True):
    key=(size,bold)
    if key not in fonts: fonts[key]=ImageFont.truetype(BOLD if bold else REG,size)
    return fonts[key]
def clamp(v,a=0,b=1):return max(a,min(b,v))
def smooth(v):v=clamp(v);return v*v*(3-2*v)

SHOTS=[
 dict(file='01-capture.png',start=0,end=6.5,z0=1.02,z1=1.13,c0=(.51,.51),c1=(.57,.57)),
 dict(file='02-meteors.png',start=6.5,end=10.5,z0=1.13,z1=1.02,c0=(.44,.52),c1=(.50,.50)),
 dict(file='03-hands.png',start=10.5,end=15.5,z0=1.04,z1=1.12,c0=(.48,.52),c1=(.53,.56)),
 dict(file='04-flood.png',start=15.5,end=20,z0=1.13,z1=1.02,c0=(.54,.58),c1=(.50,.50)),
]
for shot in SHOTS:shot['image']=Image.open(ROOT/shot['file']).convert('RGB')

def camera(shot,t):
    u=smooth((t-shot['start'])/(shot['end']-shot['start']))
    zoom=shot['z0']*(1-u)+shot['z1']*u
    image=shot['image']; iw,ih=image.size
    cw=min(iw,ih*W/H)/zoom;ch=cw*H/W
    cx=(shot['c0'][0]*(1-u)+shot['c1'][0]*u)*iw
    cy=(shot['c0'][1]*(1-u)+shot['c1'][1]*u)*ih
    for impact in (6.5,8.8,12.0,15.5):
        d=t-impact
        if 0<=d<.55:
            amp=9*math.exp(-d*8)
            cx+=amp*math.sin(d*118);cy+=amp*.6*math.cos(d*93)
    left=clamp(cx-cw/2,0,iw-cw);top=clamp(cy-ch/2,0,ih-ch)
    result=image.resize((W,H),Image.Resampling.BICUBIC,box=(left,top,left+cw,top+ch)).convert('RGBA')
    return result,(left,top,cw,ch)

def text(draw,xy,value,size=24,color=(255,255,255,255),anchor='la',bold=True):
    draw.text((xy[0]+1,xy[1]+2),value,font=font(size,bold),fill=(0,0,0,min(130,color[3] if len(color)>3 else 130)),anchor=anchor)
    draw.text(xy,value,font=font(size,bold),fill=color,anchor=anchor)
def pill(draw,rect,color,outline=None,width=1,r=18):
    draw.rounded_rectangle(rect,radius=r,fill=color,outline=outline,width=width)

# Reused vertical gradients; no extra raster asset generation or per-frame allocation.
shade=Image.new('RGBA',(W,H));gd=ImageDraw.Draw(shade)
for y in range(H):
    a=int(115*(1-y/130)) if y<130 else int(145*(y-(H-190))/190) if y>H-190 else 0
    if a:gd.line((0,y,W,y),fill=(*INK,a))

def score(draw,t):
    r=128+sum(t>=n for n in (4.8,8.4,12.4,16.8))
    b=121+sum(t>=n for n in (3.1,3.8,5.4,7.8,9.3,11.3,13.1,14.8,15.8,16.5,17.1,17.7,18.0))
    pill(draw,(32,34,225,92),(*RED,230),(*RED,255),2)
    pill(draw,(W-225,34,W-32,92),(*BLUE,230),(*BLUE,255),2)
    draw.ellipse((47,51,65,69),fill='white');draw.ellipse((W-65,51,W-47,69),fill='white')
    text(draw,(80,59),f'RED  {r}',27,anchor='lm')
    text(draw,(W-80,59),f'{b}  BLUE',27,anchor='rm')
    pill(draw,(W/2-83,36,W/2+83,76),(*INK,205),r=16)
    text(draw,(W/2,56),'BLOCK CONTROL',15,anchor='mm')
    text(draw,(W/2,88),f'01:{60-int(t):02d}' if t>=1 else '02:00',15,anchor='mm')

def warning(draw,t,start,end,label,sub,color):
    if not start<=t<end:return
    remaining=max(1,math.ceil(end-t))
    blink=.65+.35*abs(math.sin((t-start)*math.pi*2))
    rect=(W/2-240,108,W/2+240,192)
    pill(draw,rect,(*INK,235),(*color,int(255*blink)),2,19)
    # Vector warning triangle and progress stroke.
    x=W/2-206;y=143
    draw.polygon(((x,y-14),(x-15,y+12),(x+15,y+12)),fill=(*color,255))
    text(draw,(x,y+1),'!',17,(*INK,255),anchor='mm')
    text(draw,(W/2-175,131),label,23,(*color,255))
    text(draw,(W/2-175,161),sub,15)
    text(draw,(W/2+201,146),str(remaining),42,(*color,255),anchor='mm')
    draw.rounded_rectangle((W/2-226,181,W/2-226+452*clamp((end-t)/(end-start)),185),radius=2,fill=(*color,255))

def lower(draw,t,a,b,top,bottom=None,color=MINT):
    if not a<=t<b:return
    opacity=min(smooth((t-a)/.22),smooth((b-t)/.22))
    if opacity<=0:return
    alpha=int(255*opacity)
    x=40;y=H-135 if bottom else H-115
    draw.rounded_rectangle((x,y,x+5,H-57),radius=2,fill=(*color,alpha))
    text(draw,(x+20,y-2),top,33,(*color,alpha))
    if bottom:text(draw,(x+20,y+44),bottom,20,(255,255,255,alpha),bold=False)

def overlays(frame,t,shot,crop):
    frame=Image.alpha_composite(frame,shade)
    layer=Image.new('RGBA',(W,H));d=ImageDraw.Draw(layer)
    score(d,t)
    # A very brief random-event draw before each countdown locks in.
    for start in (3.05,12.05):
        if start<=t<start+.45:
            choices=('FLOOD','METEORS','TORNADO','ACID RAIN')
            val=choices[int((t-start)*16)%4]
            pill(d,(W/2-175,112,W/2+175,154),(*INK,225),r=15)
            text(d,(W/2,133),'EVENT ROLL  /  '+val,18,anchor='mm')
    warning(d,t,3.5,6.5,'METEOR SHOWER','MOVE AWAY FROM EXPOSED LEDGES',(255,181,75))
    warning(d,t,12.5,15.5,'FLOOD INCOMING','TAKE THE HIGH ROUTE',(94,223,255))
    if .5<=t<3.5:
        progress=clamp((t-.5)/2.6)
        pill(d,(W/2-210,H-122,W/2+210,H-57),(*INK,225),(70,144,255,220),2)
        text(d,(W/2,H-101),'HOLD TO CLAIM' if progress<1 else 'BLUE CAPTURED  +1',19,(*MINT,255) if progress>=1 else (255,255,255,255),anchor='mm')
        d.rounded_rectangle((W/2-186,H-82,W/2+186,H-72),radius=5,fill=(240,245,255,65))
        if progress>0:d.rounded_rectangle((W/2-186,H-82,W/2-186+372*progress,H-72),radius=5,fill=(*BLUE,255))
    lower(d,t,0,.55,'RED vs BLUE','Every neutral block is a point waiting to happen.',RED)
    lower(d,t,3.55,6.35,'EVERY BLOCK COUNTS.','Claim territory. Keep an escape route.',MINT)
    lower(d,t,6.55,10.25,'RANDOM DISASTERS. FAIR WARNING.','The route you planned can disappear.',(255,189,92))
    lower(d,t,10.55,12.45,'AI HANDS ROAM.','Small hands. Unpredictable nudges.',MINT)
    lower(d,t,12.6,15.35,'ONE HUGE SPIRIT.','Limited force. Splash-sized trouble.',(121,227,255))
    lower(d,t,15.6,18.2,'CLIMB. CAPTURE. SURVIVE.','Your team still needs those points.',MINT)
    # Original animated compositing: impact sparks and water droplets.
    if 6.5<=t<10.5:
        for k in range(34):
            phase=((t-6.5)*.7+k*.113)%1
            x=70+k*13+phase*90;y=150+phase*phase*470+(k%5)*20
            d.ellipse((x,y,x+3,y+5),fill=(255,194,95,int((1-phase)*160)))
    if 11.1<t<15.3:
        for k in range(24):
            q=((t-11.1)*.6+k*.137)%1
            x=240+k*24+q*55;y=300-105*math.sin(q*math.pi)+q*220
            d.ellipse((x,y,x+3+(k%3),y+6),fill=(210,252,255,int(160*math.sin(q*math.pi))))
    if 15.5<=t<18.3:
        # Wavelets drift across the lower portion, emphasizing flood motion.
        for k in range(12):
            x=(k*119+t*35)%1400-60;y=H-25-(k%4)*14+5*math.sin(t*2+k)
            d.arc((x,y-7,x+75,y+7),190,345,fill=(215,255,255,75),width=2)
    # Always distinguish this art-driven edit from captured implementation.
    text(d,(W-30,H-25),'CONCEPT FOOTAGE',13,(235,242,255,225),anchor='rd')
    frame=Image.alpha_composite(frame,layer)
    if t>=18.2:
        alpha=smooth((t-18.2)/.4)
        frame=Image.alpha_composite(frame,Image.new('RGBA',(W,H),(*INK,int(218*alpha))))
        title=Image.new('RGBA',(W,H));td=ImageDraw.Draw(title);a=int(255*alpha)
        text(td,(W/2,204),'DEADLY JENGA',83,(255,255,255,a),anchor='mm')
        text(td,(W/2,280),'RED vs BLUE',38,(*MINT,a),anchor='mm')
        text(td,(W/2,350),'CLAIM THE TOWER. SURVIVE THE CHAOS.',24,(255,255,255,a),anchor='mm')
        pill(td,(W/2-197,405,W/2+197,457),(*BLUE,int(190*alpha)),(*MINT,a),2,22)
        text(td,(W/2,431),'20-SECOND CONCEPT TRAILER',18,(255,255,255,a),anchor='mm')
        text(td,(W/2,502),'Art-driven visualization of proposed gameplay',16,(215,222,240,a),anchor='mm',bold=False)
        frame=Image.alpha_composite(frame,title)
    fade=min(smooth(t/.25),smooth((DURATION-t)/.28))
    if fade<1:frame=Image.blend(Image.new('RGBA',(W,H),(8,8,20,255)),frame,fade)
    return frame.convert('RGB')

def audio():
    rate=48000;n=rate*DURATION;mix=np.zeros((n,2),dtype=np.float64)
    soundroot=REPO/'Assets/CandyAudio/Audio'
    def read(path):
        with wave.open(str(path),'rb') as f:
            sr=f.getframerate();channels=f.getnchannels();sw=f.getsampwidth();raw=f.readframes(f.getnframes())
        if sw!=2:raise ValueError(f'Expected PCM16: {path}')
        data=np.frombuffer(raw,dtype='<i2').astype(np.float64).reshape(-1,channels)/32768
        if channels>1:data=data.mean(axis=1)
        else:data=data[:,0]
        if sr!=rate:
            outlen=round(len(data)*rate/sr);data=np.interp(np.arange(outlen)*sr/rate,np.arange(len(data)),data)
        return data
    def add(data,at,gain=1,pan=0):
        start=round(at*rate);end=min(n,start+len(data));data=data[:max(0,end-start)]*gain
        if end<=start:return
        mix[start:end,0]+=data*math.sqrt((1-pan)/2);mix[start:end,1]+=data*math.sqrt((1+pan)/2)
    music=read(soundroot/'Music/Music_01.wav');music=np.tile(music,math.ceil(n/len(music)))[:n]
    add(music,0,.32)
    surf=read(soundroot/'Loops/Surf_01.wav');surf=np.tile(surf,math.ceil(n/len(surf)))[:n]
    add(surf,0,.12)
    def cue(name,at,gain=.6,pan=0):add(read(soundroot/'Effects'/name),at,gain,pan)
    cue('BlockTap_01.wav',.7,.3,-.1);cue('BlockTap_02.wav',1.5,.35,.1);cue('BlockTap_03.wav',2.4,.35)
    # Original warning / capture tones; no external music or recorded voice.
    def tone(at,freq,duration=.12,gain=.17):
        ts=np.arange(round(duration*rate))/rate
        envelope=np.minimum(ts/.012,1)*np.minimum((duration-ts)/.04,1)
        add(np.sin(2*np.pi*freq*ts)*envelope,at,gain)
    for at,freq in ((3.1,660),(3.22,880),(3.38,1100)):tone(at,freq,.16,.15)
    for at in (3.5,4.5,5.5,12.5,13.5,14.5):tone(at,880,.13,.22)
    for at,gain,pan in ((6.5,.8,-.65),(6.8,.65,-.3),(7.4,.5,.2),(8.8,.6,-.4)):
        cue('BlockSmash_01.wav',at,gain,pan)
    for at,pan in ((7.0,.3),(8.0,-.4),(9.3,.55),(11.3,.2),(13.7,-.3),(16.6,.3)):
        cue('BlockHit_02.wav',at,.4,pan)
    flight=read(soundroot/'Loops/HandFlight_01.wav');add(flight[:rate*4],10.5,.3,.25)
    cue('BlockSplash_01.wav',12.0,.7,-.4);cue('BlockSplash_02.wav',12.45,.5,.25)
    cue('BlockSplash_03.wav',15.5,.65);cue('BlockSplash_04.wav',16.6,.5,.4)
    cue('ClimbPlant_01.wav',16.1,.35,-.15);cue('ClimbPlant_02.wav',17.0,.4,.1)
    for i,freq in enumerate((392,494,587,784)):tone(18.32+i*.1,freq,.45,.11)
    times=np.arange(n)/rate;env=np.minimum(times/.3,1)*np.minimum((DURATION-times)/.5,1)
    mix*=env[:,None]
    peak=float(np.max(np.abs(mix)));mix*=.92/max(.92,peak)
    path=ROOT/'trailer-mix.wav'
    with wave.open(str(path),'wb') as f:f.setnchannels(2);f.setsampwidth(2);f.setframerate(rate);f.writeframes((mix*32767).astype('<i2').tobytes())
    return path

def main():
    soundtrack=audio();output=ROOT/'DeadlyJenga_RedBlue_Disaster_Concept_20s.mp4'
    args=[str(FFMPEG),'-hide_banner','-loglevel','warning','-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','pipe:0','-i',str(soundtrack),'-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-t',str(DURATION),'-movflags','+faststart',str(output)]
    process=subprocess.Popen(args,stdin=subprocess.PIPE,stderr=open(ROOT/'render.log','w'))
    try:
        for index in range(FPS*DURATION):
            t=index/FPS;si=max(i for i,s in enumerate(SHOTS) if t>=s['start']);shot=SHOTS[si]
            base,crop=camera(shot,t)
            # 0.22s editorial dissolve on each cut, with physical impact at meteor cut.
            if si and t-shot['start']<.22:
                previous,_=camera(SHOTS[si-1],shot['start'])
                base=Image.blend(previous,base,smooth((t-shot['start'])/.22))
            result=overlays(base,t,shot,crop)
            process.stdin.write(result.tobytes())
            if index%90==0:print(f'Rendered {index}/{FPS*DURATION} frames',flush=True)
        process.stdin.close();code=process.wait()
        if code:raise RuntimeError(f'ffmpeg exit {code}; see render.log')
    finally:
        if process.poll() is None:process.terminate()
    manifest={'durationSeconds':DURATION,'fps':FPS,'resolution':[W,H],'type':'art-driven animated concept trailer, not captured Unity gameplay','video':output.name,'audio':'existing project sound library plus original synthesized UI tones','shots':[{k:v for k,v in s.items() if k!='image'} for s in SHOTS],'warnings':[{'event':'meteors','warningAt':3.5,'eventAt':6.5},{'event':'flood','warningAt':12.5,'eventAt':15.5}]}
    (ROOT/'trailer-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(output,flush=True)
if __name__=='__main__':main()

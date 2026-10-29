import concurrent.futures
import html
import json
from pathlib import Path
import re
import subprocess
import zipfile
import sys

ROOT = Path(__file__).resolve().parent
tracks = json.loads((ROOT / 'Tracks.json').read_text(encoding='utf-8'))['tracks']

def verify(track):
    path = ROOT / track['files']['wav']
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)], text=True))
    stream = probe['streams'][0]
    assert stream['codec_name'] == 'pcm_s24le'
    assert stream['sample_rate'] == '48000' and stream['channels'] == 2
    assert abs(float(probe['format']['duration']) - track['seconds']) < .001
    check = subprocess.run(['ffmpeg', '-hide_banner', '-i', str(path), '-af', 'silencedetect=n=-50dB:d=1,loudnorm=I=-18:TP=-1.5:LRA=11:print_format=json', '-f', 'null', '-'], capture_output=True, text=True, check=True)
    assert 'silence_start' not in check.stderr, track['title']
    loudness = json.loads(re.search(r'\{\s*"input_i".*?\}', check.stderr, re.S).group())
    assert float(loudness['input_tp']) < -1.5
    subprocess.run(['ffmpeg', '-v', 'error', '-i', str(ROOT / track['files']['mp3']), '-f', 'null', '-'], capture_output=True, check=True)
    return {'id': track['id'], 'wav': '48 kHz stereo 24-bit PCM', 'mp3Decode': 'passed', 'silenceLongerThanOneSecond': False, 'integratedLUFS': float(loudness['input_i']), 'truePeakDbTP': float(loudness['input_tp'])}

if '--gallery-only' in sys.argv:
    checks = json.loads((ROOT / 'Verification.json').read_text(encoding='utf-8'))['checks']
else:
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        checks = list(pool.map(verify, tracks))
assert len({t['sha256'] for t in tracks}) == 3
(ROOT / 'Verification.json').write_text(json.dumps({'checks': checks, 'scope': 'Automated file, decode, silence, peak and synthesis-loop-boundary checks. No in-game or headset audition performed. Musical loop suitability remains an audition decision.'}, indent=2), encoding='utf-8')

notes = [
 ('03-flag-run-crossfire.png','The boardwalk','Bright major-key plastic riff, chunky bass, crisp breakbeat and cheeky answering phrases. A brighter breakbeat alternative.','#79ead8'),
 ('05-carnival-flight.png','The ready-up','Playful dance groove with glassy chord stabs, a confident low-end pulse and snappy percussion.','#ffb2e4'),
 ('01-tsunami-scramble.png','The island party','Sunny plucked chords, physical hand-percussion textures and a more buoyant bass line.','#ffd183'),
]
cards = []
for i, (t, n) in enumerate(zip(tracks, notes)):
    image, label, description, color = n
    seconds = round(t['seconds'])
    links = ' '.join(f'<a download href="{html.escape(t["files"][ext])}">{ext.upper()}</a>' for ext in ['mp3', 'wav', 'mid'])
    cards.append(f'''<article style="--accent:{color}"><div class="picture" style="background-image:url('../HomeMenuArtwork-20261026/{image}')"><span>{label}</span></div><div class="body"><div class="meta">0{i+1} / {t['bpm']} BPM / {seconds//60}:{seconds%60:02d}{' / APPROVED LOBBY TRACK' if i==1 else ''}</div><h2>{html.escape(t['title'])}</h2><p>{description}</p><audio controls preload="none" src="{t['files']['mp3']}"></audio><div class="links">{links}<a download href="{t['id']}.score.json">SCORE</a></div></div></article>''')

page = '''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Deadly Jenga / Lobby Music</title><style>
*{box-sizing:border-box}body{margin:0;background:#100c20;color:#f7f2ff;font:16px/1.6 system-ui,sans-serif}main{max-width:1260px;margin:auto;padding:60px 28px}header{max-width:850px;margin-bottom:30px}.eyebrow{color:#b9a4e8;text-transform:uppercase;letter-spacing:.2em;font-size:12px;font-weight:750}h1{font-size:clamp(36px,6vw,70px);line-height:1.06;margin:16px 0 22px;letter-spacing:-.05em}header p{color:#c1b6d5;font-size:18px}.toolbar{display:flex;flex-wrap:wrap;gap:18px;align-items:center;margin:25px 0 38px}a{color:inherit;text-underline-offset:4px}.button{background:#d4b7ff;color:#1c1030;padding:10px 18px;border-radius:10px;font-weight:700;text-decoration:none}label{cursor:pointer;color:#c9bddc}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:26px}article{background:#21182f;border:1px solid #382848;border-radius:19px;overflow:hidden}.picture{height:225px;background-size:cover;background-position:center;position:relative}.picture:after{content:'';position:absolute;inset:0;background:linear-gradient(transparent,#21182f)}.picture span{position:absolute;bottom:15px;left:26px;z-index:1;background:var(--accent);color:#201329;border-radius:30px;padding:4px 14px;font-size:13px;font-weight:750}.body{padding:22px 26px 27px}.meta{font-size:11px;color:var(--accent);font-weight:700;letter-spacing:.1em}h2{font-size:27px;line-height:1.2;margin:10px 0 12px}.body p{color:#c2b8cf;min-height:52px}audio{width:100%;margin:9px 0 16px}.links{display:flex;gap:20px;font-size:12px;font-weight:750;color:var(--accent)}footer{margin-top:40px;color:#ab9bbf;font-size:13px;max-width:900px}input{accent-color:#d4b7ff}@media(max-width:760px){.grid{grid-template-columns:1fr}main{padding:32px 18px}.picture{height:210px}}
</style></head><body><main><header><div class="eyebrow">Deadly Jenga / Lobby music proposals</div><h1>Good company.<br>Bad structural decisions.</h1><p>Three brighter lobby revisions: major-key riffs, punchier grooves and more bounce. Fun, social energy in the plastic-and-candy sound palette. <strong>Ready, Set, Splash</strong> is the approved looping lobby track. These replace the moody direction of the first lobby proposals. Ready, Set, Splash is now configured for lobby playback; the other two remain alternatives.</p></header><div class="toolbar"><a class="button" download href="MenuMusic-Listening.zip">Download all MP3s</a><a download href="MenuMusic-Production.zip">WAV + MIDI + source pack</a><label><input type="checkbox" id="loop" checked> Loop playback</label></div><section class="grid">''' + '\n'.join(cards) + '''</section><footer><p>Three original lobby compositions, 40 bars each. WAV masters: 48 kHz / stereo / 24-bit. MIDI and score files remain editable; General MIDI playback will sound different from these custom synthesized instruments. Browser MP3 playback may introduce a small loop gap; use the WAV masters for game implementation.</p><p>These are music auditions, not installed game assets. Background artwork is game-style concept art. Automated decode, silence and peak checks passed; final musical balance and loop feel need listening review. <a href="Verification.json">Audio checks</a> · <a href="Tracks.json">Track data</a> · <a href="compose_fun.py">Composition source</a></p></footer></main><script>
const players=[...document.querySelectorAll('audio')];const loop=document.querySelector('#loop');function setLoops(){players.forEach(p=>p.loop=loop.checked)}setLoops();loop.addEventListener('change',setLoops);players.forEach(p=>p.addEventListener('play',()=>players.filter(q=>q!==p).forEach(q=>q.pause())));
</script></body></html>'''
(ROOT / 'index.html').write_text(page, encoding='utf-8')
(ROOT / 'README.txt').write_text('Deadly Jenga — original electronic menu music revision\n\nApproved lobby loop: Ready, Set, Splash.\nThree brighter original lobby grooves. No external recordings or downloaded samples.\nOpen index.html for the listening gallery, or play the MP3 files directly.\nWAV files are 48 kHz stereo 24-bit masters. Prefer WAV for looping in Unity.\nMIDI files use approximate General MIDI timbres; score JSON and compose_fun.py preserve the custom arrangement and synthesis.\nNo Unity assets or game settings were changed.\nSee Verification.json for automated checks; this is not a claim of in-game or headset audition.\n', encoding='utf-8')
common = ['Tracks.json', 'README.txt', 'Verification.json']
for name, files in [('MenuMusic-Listening.zip', common + [t['files']['mp3'] for t in tracks]), ('MenuMusic-Production.zip', common + ['Designs.json', 'compose_fun.py', 'compose_coastal.py', 'arrangement_base.py', 'engine.py'] + [t['files'][ext] for t in tracks for ext in ['wav', 'mid']] + [t['id']+'.score.json' for t in tracks])]:
    with zipfile.ZipFile(ROOT / name, 'w', zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
        for file in files:
            archive.write(ROOT / file, file)
print(json.dumps(checks, indent=2))
print('Gallery and both ZIP packages ready.')

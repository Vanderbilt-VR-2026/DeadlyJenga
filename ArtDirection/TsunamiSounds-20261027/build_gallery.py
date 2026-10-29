from pathlib import Path
import csv, wave, struct, html, json, hashlib

root = Path(__file__).parent
rows = list(csv.DictReader((root / 'Export.tsv').read_text().splitlines(), delimiter='\t'))
info = {r['sound']: r for r in rows}
labels = {
    'DisasterHorn': ('Warning horn', 'A low two-tone alarm when the tsunami starts.', 'One-shot'),
    'DisasterTension': ('Countdown tension', 'A pulsing low drone. In-game pitch rises during the warning.', 'Loop source'),
    'WaveImpact': ('First impact', 'A short filtered-noise hit on first contact with blocks.', 'One-shot'),
    'WaveSpray': ('Contact spray', 'A short rushing noise requested repeatedly while the wave touches blocks.', 'Repeated in-game'),
    'PuddleDrain': ('Puddle drain', 'A bubbly, wobbling drain cue after the wave ends.', 'One-shot'),
}

def card(name):
    title, desc, detail = labels[name]
    row = info[name]
    with wave.open(str(root / (name + '.wav'))) as wav:
        assert wav.getframerate() == int(row['frequency'])
        assert wav.getnchannels() == int(row['channels'])
        assert wav.getnframes() == int(row['samples'])
        frames = wav.readframes(wav.getnframes())
        values = struct.unpack('<' + 'h' * (len(frames) // 2), frames)
    peaks = [max(abs(v) for v in values[int(i*len(values)/100):int((i+1)*len(values)/100)])/32768 for i in range(100)]
    bars = ''.join(f'<rect x="{i*6}" y="{24-max(1,v*40):.2f}" width="3" height="{max(2,v*80):.2f}" rx="1.5"/>' for i,v in enumerate(peaks))
    return f'<article><div class="cardtop"><h3>{title}</h3><span>{float(row["seconds"]):.1f}s</span></div><p>{desc}</p><svg viewBox="0 0 600 48" aria-hidden="true">{bars}</svg><audio controls preload="metadata" src="{name}.wav" aria-label="Listen to {title}"></audio><footer>{detail} &middot; <a href="{name}.wav" download>Download WAV</a></footer></article>'

groups = [('warning', '01', 'Warning', ['DisasterHorn','DisasterTension']), ('contact','02','Impact &amp; spray',['WaveImpact','WaveSpray']), ('after','03','Aftermath',['PuddleDrain'])]
sections = ''.join(f'<section id="{key}"><h2><em>{num}</em>{title}</h2><div class="cards">'+''.join(card(n) for n in names)+'</div></section>' for key,num,title,names in groups)
page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Tsunami - Current sound library</title><style>
:root{color-scheme:dark;font:16px/1.6 system-ui,sans-serif;background:#08151e;color:#eaf6fa}*{box-sizing:border-box}body{margin:0}header,main{max-width:1120px;margin:auto;padding:32px 28px}header{padding-top:60px;background:radial-gradient(ellipse at 10% 0%,#12546780,transparent 70%)}.eyebrow{color:#8bd6dc;text-transform:uppercase;letter-spacing:.18em;font-size:12px;font-weight:700}h1{font-size:clamp(34px,5vw,58px);line-height:1.1;margin:16px 0}header p{max-width:750px;color:#b7ced8}.tools{display:flex;align-items:center;gap:20px;flex-wrap:wrap;padding-top:12px}button{background:#bceef0;color:#08232c;border:0;border-radius:9px;padding:10px 18px;cursor:pointer;font-weight:700}label{display:flex;align-items:center;gap:10px;font-size:14px}nav{position:sticky;top:0;z-index:2;display:flex;gap:24px;padding:14px 28px;justify-content:center;background:#08151eee;border-block:1px solid #26414f;backdrop-filter:blur(12px)}a{color:#92e2e7}nav a{text-decoration:none}main{padding-top:0}section{scroll-margin-top:80px;margin:40px 0 56px}h2{font-size:23px;display:flex;align-items:center;gap:16px}h2 em{font-style:normal;color:#719aab;font-size:14px}.cards{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}article{border:1px solid #294553;background:linear-gradient(135deg,#142d3a,#10212d);border-radius:16px;padding:24px;min-width:0}.cardtop{display:flex;justify-content:space-between;gap:20px;align-items:center}h3{margin:0;font-size:21px}.cardtop span{font-size:13px;border:1px solid #3a5c6d;border-radius:30px;padding:3px 10px;color:#b7d2df;white-space:nowrap}article p{color:#b7ced8;font-size:14px;min-height:46px}svg{display:block;width:100%;height:58px;margin:16px 0;fill:#6ac8d2}audio{width:100%;display:block}footer{font-size:12px;color:#9bb5c2;margin-top:16px}.note{border-left:3px solid #6ac8d2;padding-left:20px;color:#a6c1ce;font-size:14px;max-width:840px}.end{color:#7e9aa8;font-size:12px;padding:10px 0 40px}@media(max-width:680px){.cards{grid-template-columns:1fr}header,main{padding-left:18px;padding-right:18px}nav{gap:18px;font-size:14px}article{padding:20px}}html{scroll-behavior:smooth}
</style><header><div class="eyebrow">Deadly Jenga / Audio review</div><h1>Tsunami sound library</h1><p>The five sounds currently assigned to the tsunami, organized in gameplay order. These are the actual source clips exported from Unity, not redesigned sound proposals.</p><div class="tools"><button id="stop" type="button">Stop playback</button><label for="volume">Preview volume <input id="volume" type="range" min="0" max="1" step=".01" value=".65"></label></div></header><nav aria-label="Sound categories"><a href="#warning">Warning</a><a href="#contact">Impact &amp; spray</a><a href="#after">Aftermath</a></nav><main>'''+sections+'''<p class="note">Source-clip audition: playback uses a fixed pitch and excludes in-game distance, bus volume, cooldowns and overlapping events. The tension player plays one loop cycle; the game loops it. All five currently come from synthesized fallback cues. There is no dedicated continuous tsunami roar.</p><div class="end">PCM WAV &middot; 22,050 Hz &middot; mono &middot; exported from the current Unity sound generator.</div></main><script>
const players=[...document.querySelectorAll('audio')], volume=document.getElementById('volume');
for(const player of players){player.volume=Number(volume.value);player.addEventListener('play',()=>{for(const other of players)if(other!==player)other.pause()})}
volume.addEventListener('input',()=>players.forEach(player=>player.volume=Number(volume.value)));
document.getElementById('stop').addEventListener('click',()=>players.forEach(player=>{player.pause();player.currentTime=0}));
</script></html>'''
(root/'index.html').write_text(page,encoding='utf-8')
manifest = {'source':'Actual AudioClip PCM exported by Unity','clips':[]}
for row in rows:
    path=root/(row['sound']+'.wav')
    manifest['clips'].append({**row,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'file':path.name})
(root/'Manifest.json').write_text(json.dumps(manifest,indent=2))
assert page.count('<audio ')==5 and all((root/(n+'.wav')).exists() for n in labels)
print('PASS: 5 WAV headers/sample counts, audio player links and waveform previews validated.')

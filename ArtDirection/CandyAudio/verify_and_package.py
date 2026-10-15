from pathlib import Path
import wave,json,hashlib,zipfile,math
import numpy as np
root=Path(__file__).resolve().parents[2];output=root/'ArtDirection/CandyAudio'
manifest=json.loads((output/'CLIP_MANIFEST.json').read_text());clips=manifest['clips'];lines=[]
def check(ok,label):
    lines.append(('PASS ' if ok else 'FAIL ')+label)
    if not ok:raise ValueError(label)
check(len(clips)==119,'119 generated clips present')
check(len({c['cue'] for c in clips})==35,'35 cue categories present')
signals={};peaks=[]
for clip in clips:
    path=root/clip['path'];check(path.exists(),'Clip exists: '+path.name)
    with wave.open(str(path),'rb') as w:
        check(w.getnchannels()==1 and w.getsampwidth()==2 and w.getframerate()==32000,'Mono 16-bit/32kHz: '+path.name)
        data=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(np.float64)/32768
    peak=float(np.max(np.abs(data)));rms=float(np.sqrt(np.mean(data*data)))
    check(.01<rms<.6 and peak<.95,'Non-silent, unclipped: '+path.name)
    check(hashlib.sha256(path.read_bytes()).hexdigest()==clip['sha256'],'Manifest checksum: '+path.name)
    peaks.append(peak);signals.setdefault(clip['cue'],[]).append((data,path))
for name,variants in signals.items():
    if len(variants)>1:check(len({hashlib.sha256(v[0].tobytes()).hexdigest() for v in variants})==len(variants),'Distinct variants: '+name)
for path in (root/'Assets/CandyAudio').rglob('*.cs'):
    if 'Editor' in path.parts:continue
    source=path.read_text()
    check(not any(word in source for word in ['AddForce(','AddTorque(','Physics.IgnoreCollision(','Physics.IgnoreLayerCollision(']),'No physics force/matrix mutations: '+path.name)
# Short labelled-in-notes audition; silence separates each sound family.
names=['BlockTap','BlockHit','BlockSmash','StructureLean','RubberTouch','ClimbPlant','VaultGrunt','ClimbGrunt','FriendlyWave','Phew','NopeNopeNope','BlockSplash']
segments=[]
for name in names:
    segments.extend([signals[name][0][0]*.7,np.zeros(int(32000*.35))])
audition=np.concatenate(segments);path=output/'Candy-Audio-Audition.wav'
with wave.open(str(path),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(32000);w.writeframes((audition*32767).astype('<i2').tobytes())
(output/'AUDITION_ORDER.txt').write_text('\n'.join(names)+'\n')
(output/'FileValidation.txt').write_text('\n'.join(lines)+f'\n{len(lines)} checks passed. File/source checks only; Unity runtime and listening are separate.\n')
# Unity's standard GUID-based asset package, reproducible without controlling the editor.
import io, re, tarfile
with tarfile.open(output/'CandyAudio.unitypackage','w:gz') as package:
    for meta in sorted((root/'Assets/CandyAudio').rglob('*.meta')):
        guid=re.search(r'^guid: ([0-9a-f]{32})$',meta.read_text(),re.M)
        if not guid:continue
        asset=meta.with_suffix('');prefix=guid.group(1)+'/'
        entries={'asset.meta':meta.read_bytes(),'pathname':asset.relative_to(root).as_posix().encode()}
        if asset.is_file():entries['asset']=asset.read_bytes()
        for name,data in entries.items():
            member=tarfile.TarInfo(prefix+name);member.size=len(data);package.addfile(member,io.BytesIO(data))
target=output/'CandyAudio-Reapply.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
    for path in (root/'Assets/CandyAudio').rglob('*'):
        if path.is_file():z.write(path,path.relative_to(root))
    for path in output.iterdir():
        if path.is_file() and path.suffix not in ['.zip','.unitypackage'] and path!=target:z.write(path,path.relative_to(root))
print(f'{len(lines)} checks passed; {len(clips)} WAVs; {target.stat().st_size/1048576:.2f} MiB source package.')

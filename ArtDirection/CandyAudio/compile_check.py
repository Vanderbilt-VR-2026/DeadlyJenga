from pathlib import Path
import re,subprocess
root=Path(__file__).resolve().parents[2]
folder=root/'Temp/CandyAudioCheck';folder.mkdir(parents=True,exist_ok=True)
source=next((root/'Library/Bee/artifacts').glob('*/Assembly-CSharp.rsp'))
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data')
for kind in ['runtime','editor']:
    src=source if kind=='runtime' else source.with_name('Assembly-CSharp-Editor.rsp')
    text=src.read_text(encoding='utf-8-sig');out='CandyAudioCheck' if kind=='runtime' else 'Assembly-CSharp-Editor'
    text=re.sub(r'^-out:.*$',f'-out:"{folder.as_posix()}/{out}.dll"',text,flags=re.M)
    text=re.sub(r'^-refout:.*$',f'-refout:"{folder.as_posix()}/{out}.ref.dll"',text,flags=re.M)
    if kind=='editor':text=re.sub(r'^-r:"[^"\r\n]*/Assembly-CSharp\.ref\.dll"$',f'-r:"{folder.as_posix()}/CandyAudioCheck.ref.dll"',text,flags=re.M)
    paths=list((root/'Assets/CandyAudio').rglob('*.cs'))
    # Include any freshly written runtime sources not imported into the project's rsp yet.
    if kind=='runtime':paths+=list((root/'Assets/Scripts').glob('*.cs'))+list((root/'Assets/CastleBots').glob('*.cs'))
    else:paths+=list((root/'Assets/Scripts/Editor').glob('*.cs'))+list((root/'Assets/CastleBots/Editor').glob('*.cs'))
    for path in paths:
        is_editor='Editor' in path.relative_to(root/'Assets').parts
        if is_editor!=(kind=='editor'):continue
        rel=path.relative_to(root).as_posix()
        if f'"{rel}"' not in text:text+='\n"'+rel+'"\n'
    rsp=folder/(kind+'.rsp');rsp.write_text(text)
    result=subprocess.run([str(editor/'NetCoreRuntime/dotnet.exe'),'exec',str(editor/'DotNetSdkRoslyn/csc.dll'),'/nostdlib','/noconfig','@'+str(rsp),'@'+str(src.with_suffix('.rsp2'))],cwd=root,capture_output=True,text=True)
    (folder/(kind+'.log')).write_text(result.stdout+'\n'+result.stderr)
    print(kind,result.returncode,result.stdout[-10000:],result.stderr[-2000:])
    if result.returncode:raise SystemExit(result.returncode)

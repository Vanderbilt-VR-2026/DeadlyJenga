"""Build the small native adapter, assemble the model pack, and copy licensed runtimes."""
import hashlib,json,os,pathlib,re,shutil,subprocess
ROOT=pathlib.Path(__file__).resolve().parent;PROJECT=ROOT.parents[1];CACHE=ROOT/'.cache';BUILD=ROOT/'.build';BUILD.mkdir(exist_ok=True)
UNITY=pathlib.Path('C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/PlaybackEngines/AndroidPlayer')
BIN=UNITY/'NDK/toolchains/llvm/prebuilt/windows-x86_64/bin'
BOX=PROJECT/'Assets/_DeadlyJenga/GameSession/Session/CastleSocial'
def find(folder,pattern):return next(folder.rglob(pattern))
def run(args,**kwargs):
    print('Run '+str(args[0]),flush=True);subprocess.run([str(a) for a in args],check=True,**kwargs)
def plugin(path,platform):
    # Native plugins have explicit platforms, including the DLLs used by desktop verification.
    meta=path.with_name(path.name+'.meta')
    if meta.exists():return
    guid=hashlib.md5(('DeadlyJenga.OnDevice.'+platform+'.'+path.name).encode()).hexdigest()
    editor=1 if platform=='Windows' else 0;android=1 if platform=='Android' else 0
    meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: {editor}
      settings:
        CPU: x86_64
        OS: Windows
  - first:
      Android: Android
    second:
      enabled: {android}
      settings:
        CPU: ARM64
  - first:
      Standalone: Win64
    second:
      enabled: {editor}
      settings:
        CPU: x86_64
  userData: 
  assetBundleName: 
  assetBundleVariant: 
''')
def main():
    source=BOX/'NativeSource~/conversation.cpp';headers=CACHE/'headers'
    win=CACHE/'llama-b11714-bin-win-cpu-x64';android=find(CACHE/'llama-b11714-bin-android-arm64','libllama.so').parent
    sherpaWin=find(CACHE/'sherpa-onnx-v1.13.8-win-x64-shared-MD-Release','sherpa-onnx-c-api.dll').parent
    sherpaLib=find(CACHE/'sherpa-onnx-v1.13.8-win-x64-shared-MD-Release-lib','sherpa-onnx-c-api.lib')
    sherpaAndroid=CACHE/'sherpa-onnx-v1.13.8-android/jniLibs/arm64-v8a'
    for library in ['llama','ggml','ggml-base']:
        exports=subprocess.check_output([str(BIN/'llvm-readobj.exe'),'--coff-exports',str(win/(library+'.dll'))],text=True)
        definition=BUILD/(library+'.def');definition.write_text('LIBRARY '+library+'.dll\nEXPORTS\n'+'\n'.join(re.findall(r'^  Name: (.+)$',exports,re.M))+'\n')
        run([BIN/'llvm-dlltool.exe','-m','i386:x86-64','-d',definition,'-l',BUILD/(library+'.lib')])
    zig=find(CACHE/'zig','zig.exe')
    run([zig,'c++','-target','x86_64-windows-gnu','-shared','-std=c++17','-O2','-DSHERPA_ONNX_BUILD_SHARED_LIBS','-I'+str(headers),source,sherpaLib,BUILD/'llama.lib',BUILD/'ggml.lib',BUILD/'ggml-base.lib','-o',BUILD/'deadlyjenga_conversation.dll'],cwd=BUILD)
    run([BIN/'clang++.exe','--target=aarch64-linux-android28','-shared','-fPIC','-std=c++17','-O2','-I'+str(headers),source,'-L'+str(android),'-L'+str(sherpaAndroid),'-lllama','-lggml','-lggml-base','-lsherpa-onnx-c-api','-Wl,-z,max-page-size=16384','-Wl,-rpath,$ORIGIN','-o',BUILD/'libdeadlyjenga_conversation.so'])
    for platform,inputs in [('Windows',[BUILD/'deadlyjenga_conversation.dll',*win.glob('ggml*.dll'),win/'llama.dll',win/'libomp.dll',sherpaWin/'sherpa-onnx-c-api.dll',sherpaWin/'onnxruntime.dll']),('Android',[BUILD/'libdeadlyjenga_conversation.so',*android.glob('libggml*.so'),android/'libllama.so',sherpaAndroid/'libsherpa-onnx-c-api.so',sherpaAndroid/'libonnxruntime.so',UNITY/'NDK/toolchains/llvm/prebuilt/windows-x86_64/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so'])]:
        output=BOX/'Plugins'/platform;output.mkdir(parents=True,exist_ok=True)
        for path in inputs:
            if 'rpc' in path.name:continue
            dest=output/path.name;shutil.copy2(path,dest);plugin(dest,platform)
    assemble_pack()

def assemble_pack():
    win=CACHE/'llama-b11714-bin-win-cpu-x64'
    pack=ROOT/'.models/pack';pack.mkdir(parents=True,exist_ok=True)
    llm=pack/'language.gguf'
    if not llm.exists() or llm.stat().st_size<100000000:
        partial=pack/'language.partial.gguf'
        run([win/'llama-quantize.exe','--allow-requantize',CACHE/'Qwen3-0.6B-Q8_0.gguf',partial,'Q4_K_M','2'],cwd=win)
        partial.replace(llm)
    asr=find(CACHE/'sherpa-onnx-streaming-zipformer-en-20M-2023-02-17','tokens.txt').parent
    recognition=pack/'recognition';recognition.mkdir(exist_ok=True)
    for name in ['encoder-epoch-99-avg-1.int8.onnx','decoder-epoch-99-avg-1.int8.onnx','joiner-epoch-99-avg-1.int8.onnx','tokens.txt']:shutil.copy2(asr/name,recognition/name)
    tts=find(CACHE/'kitten-nano-en-v0_2-fp16','model.fp16.onnx').parent
    shutil.copytree(tts,pack/'voice',dirs_exist_ok=True)
    shutil.copytree(ROOT/'licenses',pack/'licenses',dirs_exist_ok=True)
    records=[]
    for path in sorted(pack.rglob('*')):
        if path.is_file() and path.name!='manifest.json':
            records.append({'path':path.relative_to(pack).as_posix(),'bytes':path.stat().st_size,'sha256':hashlib.file_digest(path.open('rb'),'sha256').hexdigest()})
    manifest={'version':'dj-conversation-1','nativeApi':1,'files':records}
    (pack/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('READY '+str(sum(r['bytes'] for r in records)//1048576)+' MiB model pack, '+str(len(records))+' files',flush=True)
if __name__=='__main__':main()

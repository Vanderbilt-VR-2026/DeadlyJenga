"""Explicit developer setup. Downloads official pinned releases; never runs at Unity startup."""
import argparse, concurrent.futures, hashlib, json, pathlib, shutil, subprocess, tarfile, urllib.request, zipfile
ROOT=pathlib.Path(__file__).resolve().parent
CACHE=ROOT/'.cache'; CACHE.mkdir(exist_ok=True)
HEADERS={'User-Agent':'DeadlyJenga-OnDeviceConversation'}
def get_json(url):
    return json.load(urllib.request.urlopen(urllib.request.Request(url,headers=HEADERS),timeout=60))
def download(url,name,sha=None):
    target=CACHE/name
    if not target.exists():
        partial=target.with_suffix(target.suffix+'.part')
        print('Downloading '+name,flush=True)
        with urllib.request.urlopen(urllib.request.Request(url,headers=HEADERS),timeout=120) as response, partial.open('wb') as output:
            shutil.copyfileobj(response,output,1024*1024)
        partial.replace(target)
    digest=hashlib.file_digest(target.open('rb'),'sha256').hexdigest()
    if sha and digest!=sha.removeprefix('sha256:'):raise RuntimeError('Checksum mismatch: '+name)
    return target,digest
def extract(path,folder):
    folder.mkdir(parents=True,exist_ok=True)
    if (folder/'.complete').exists():return folder
    if path.suffix=='.zip':
        with zipfile.ZipFile(path) as archive:archive.extractall(folder)
    else:
        with tarfile.open(path) as archive:archive.extractall(folder,filter='data')
    (folder/'.complete').write_text('ok')
    return folder
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--downloads-only',action='store_true');args=parser.parse_args()
    jobs=[]
    def release(repo,tag,names):
        data=get_json('https://api.github.com/repos/'+repo+'/releases/tags/'+tag)
        for name in names:
            asset=next(a for a in data['assets'] if a['name']==name)
            jobs.append((asset['browser_download_url'],name,asset.get('digest')))
    release('k2-fsa/sherpa-onnx','v1.13.8',['sherpa-onnx-v1.13.8-android.tar.bz2','sherpa-onnx-v1.13.8-win-x64-shared-MD-Release.tar.bz2','sherpa-onnx-v1.13.8-win-x64-shared-MD-Release-lib.tar.bz2'])
    release('ggml-org/llama.cpp','b11714',['llama-b11714-bin-android-arm64.tar.gz','llama-b11714-bin-win-cpu-x64.zip'])
    release('k2-fsa/sherpa-onnx','asr-models',['sherpa-onnx-streaming-zipformer-en-20M-2023-02-17.tar.bz2'])
    release('k2-fsa/sherpa-onnx','tts-models',['kitten-nano-en-v0_2-fp16.tar.bz2'])
    revision='23749fefcc72300e3a2ad315e1317431b06b590a'
    info=get_json('https://huggingface.co/api/models/Qwen/Qwen3-0.6B-GGUF/revision/'+revision+'?blobs=true')
    weights=next(s for s in info['siblings'] if s['rfilename'].endswith('.gguf'))
    jobs.append(('https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/'+revision+'/'+weights['rfilename'],'Qwen3-0.6B-Q8_0.gguf',weights['lfs']['sha256']))
    oldlock=json.loads((ROOT/'downloads.lock.json').read_text()) if (ROOT/'downloads.lock.json').exists() else []
    known={item['file']:item['sha256'] for item in oldlock}
    jobs=[(url,name,known.get(name,sha)) for url,name,sha in jobs]
    zig='zig-x86_64-windows-0.15.2.zip'
    jobs.append(('https://ziglang.org/download/0.15.2/'+zig,zig,'3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c'))
    locked=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for job,(path,digest) in zip(jobs,pool.map(lambda item:download(*item),jobs)):
            locked.append({'url':job[0],'file':path.name,'sha256':digest})
            if path.suffix!='.gguf':extract(path,CACHE/('zig' if path.name.startswith('zig-') else path.name.split('.tar')[0].removesuffix('.zip')))
    (ROOT/'downloads.lock.json').write_text(json.dumps(locked,indent=2)+'\n')
    headers=CACHE/'headers';headers.mkdir(exist_ok=True)
    sha='c0fd78626405c6d9b934efef0ec7291fcc20e3d6'
    for relative in ['include/llama.h','ggml/include/ggml.h','ggml/include/gguf.h','ggml/include/ggml-cpu.h','ggml/include/ggml-backend.h','ggml/include/ggml-opt.h','ggml/include/ggml-alloc.h']:
        name=pathlib.Path(relative).name
        path,_=download('https://raw.githubusercontent.com/ggml-org/llama.cpp/'+sha+'/'+relative,name)
        shutil.copy2(path,headers/name)
    path,_=download('https://raw.githubusercontent.com/k2-fsa/sherpa-onnx/v1.13.8/sherpa-onnx/c-api/c-api.h','sherpa-c-api.h')
    shutil.copy2(path,headers/'sherpa-c-api.h')
    print('DOWNLOADS COMPLETE',flush=True)
    if not args.downloads_only:
        import build
        build.main()
if __name__=='__main__':main()

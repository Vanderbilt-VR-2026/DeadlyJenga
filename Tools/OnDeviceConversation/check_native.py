"""Exercise the real embedded engines without Unity, microphones, or a speech service."""
import array,ctypes as c,json,math,os,pathlib,threading,time,wave
ROOT=pathlib.Path(__file__).resolve().parent;PROJECT=ROOT.parents[1]
LIB=PROJECT/'Assets/_DeadlyJenga/GameSession/Session/CastleSocial/Plugins/Windows';PACK=ROOT/'.models/pack'
OUTPUT=PROJECT/'Logs/Validation';OUTPUT.mkdir(parents=True,exist_ok=True)
directory=os.add_dll_directory(str(LIB));loaded=[c.CDLL(str(LIB/name)) for name in ['ggml-base.dll','ggml.dll','llama.dll','onnxruntime.dll','sherpa-onnx-c-api.dll']]
api=c.CDLL(str(LIB/'deadlyjenga_conversation.dll'));ptr=c.c_void_p
api.dj_create.argtypes=[c.c_char_p]*10+[c.c_int]*2;api.dj_create.restype=ptr
api.dj_error.restype=c.c_char_p;api.dj_destroy.argtypes=[ptr];api.dj_cancel.argtypes=[ptr]
api.dj_generate.argtypes=[ptr,c.c_char_p,c.c_char_p,c.c_int,c.c_int,c.c_char_p,c.c_int]
api.dj_listen.argtypes=[ptr,c.c_int,c.POINTER(c.c_float),c.c_int,c.c_int,c.c_char_p,c.c_int]
api.dj_synthesize.argtypes=[ptr,c.c_char_p,c.c_int,c.c_float,c.POINTER(c.POINTER(c.c_float)),c.POINTER(c.c_int)]
def encoded(path):return str(path).encode()
def checked(result):
    if result<0:raise RuntimeError(api.dj_error().decode()+' return '+str(result))
    return result
paths=[LIB,PACK/'language.gguf',PACK/'recognition/encoder-epoch-99-avg-1.int8.onnx',PACK/'recognition/decoder-epoch-99-avg-1.int8.onnx',PACK/'recognition/joiner-epoch-99-avg-1.int8.onnx',PACK/'recognition/tokens.txt',PACK/'voice/model.fp16.onnx',PACK/'voice/voices.bin',PACK/'voice/tokens.txt',PACK/'voice/espeak-ng-data']
start=time.perf_counter();ctx=api.dj_create(*map(encoded,paths),1,1024)
if not ctx:raise RuntimeError(api.dj_error().decode())
report={'platform':'Windows desktop native CPU, one thread; not a Quest benchmark','load_seconds':time.perf_counter()-start}
try:
    fixture=next((ROOT/'.cache/sherpa-onnx-streaming-zipformer-en-20M-2023-02-17').rglob('0.wav'))
    with wave.open(str(fixture),'rb') as source:
        assert source.getframerate()==16000 and source.getsampwidth()==2
        audio=array.array('h',source.readframes(source.getnframes()))
    start=time.perf_counter();transcript=''
    for at in range(0,len(audio),4000):
        samples=(c.c_float*min(4000,len(audio)-at))(*[v/32768 for v in audio[at:at+4000]])
        output=c.create_string_buffer(2048);checked(api.dj_listen(ctx,1,samples,len(samples),int(at+4000>=len(audio)),output,len(output)));transcript=output.value.decode()
    assert len(transcript)>10,transcript
    report['recognition']={'text':transcript,'seconds':time.perf_counter()-start,'audio_seconds':len(audio)/16000}
    system=b'You are Pebble, a friendly player climbing a candy tower over an ocean in Deadly Jenga. Reply in one short sentence, under 20 words. Answer the actual question. No narration or reasoning.'
    answers=[]
    for message in [b'What is your name?',b'I am scared of falling into the water. What should I do?']:
        output=c.create_string_buffer(4096);start=time.perf_counter();checked(api.dj_generate(ctx,system,message,40,15000,output,len(output)));text=output.value.decode();assert text.strip();answers.append({'question':message.decode(),'reply':text,'seconds':time.perf_counter()-start})
    report['generation']=answers
    samples=c.POINTER(c.c_float)();rate=c.c_int();start=time.perf_counter();count=checked(api.dj_synthesize(ctx,answers[-1]['reply'].encode(),0,1,c.byref(samples),c.byref(rate)))
    pcm=array.array('h',(int(max(-1,min(1,samples[i]))*32767) for i in range(count)))
    assert count>rate.value/4 and max(abs(x) for x in pcm)>100
    with wave.open(str(OUTPUT/'OnDeviceVoice.wav'),'wb') as out:out.setnchannels(1);out.setsampwidth(2);out.setframerate(rate.value);out.writeframes(pcm.tobytes())
    report['synthesis']={'seconds':time.perf_counter()-start,'audio_seconds':count/rate.value,'sample_rate':rate.value,'peak':max(abs(x) for x in pcm)}
    output=c.create_string_buffer(4096);timer=threading.Timer(.15,lambda:api.dj_cancel(ctx));timer.start();start=time.perf_counter();result=api.dj_generate(ctx,system,b'Tell me a long story about the tower.',64,15000,output,len(output));timer.join();assert result==-2,result
    report['cancel_seconds']=time.perf_counter()-start;report['passed']=True
finally:api.dj_destroy(ctx)
(OUTPUT/'OnDeviceNative.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

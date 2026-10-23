using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DeadlyJenga.CastleSocial
{
    // This worker owns every native model. Unity objects are touched only by its completion callbacks.
    internal sealed class OnDeviceNative : IDisposable
    {
        const string Library="deadlyjenga_conversation";
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int dj_api_version();
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr dj_error();
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr dj_create([MarshalAs(UnmanagedType.LPUTF8Str)]string backend,[MarshalAs(UnmanagedType.LPUTF8Str)]string model,[MarshalAs(UnmanagedType.LPUTF8Str)]string encoder,[MarshalAs(UnmanagedType.LPUTF8Str)]string decoder,[MarshalAs(UnmanagedType.LPUTF8Str)]string joiner,[MarshalAs(UnmanagedType.LPUTF8Str)]string tokens,[MarshalAs(UnmanagedType.LPUTF8Str)]string voice,[MarshalAs(UnmanagedType.LPUTF8Str)]string voices,[MarshalAs(UnmanagedType.LPUTF8Str)]string voiceTokens,[MarshalAs(UnmanagedType.LPUTF8Str)]string espeak,int threads,int context);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void dj_destroy(IntPtr context);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void dj_cancel(IntPtr context);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int dj_generate(IntPtr context,[MarshalAs(UnmanagedType.LPUTF8Str)]string system,[MarshalAs(UnmanagedType.LPUTF8Str)]string message,int tokens,int timeout,[Out]byte[] output,int capacity);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int dj_listen(IntPtr context,int speaker,float[] audio,int count,int final,[Out]byte[] output,int capacity);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void dj_forget(IntPtr context,int speaker);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int dj_synthesize(IntPtr context,[MarshalAs(UnmanagedType.LPUTF8Str)]string text,int speaker,float speed,out IntPtr samples,out int rate);
        [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
        static readonly System.Collections.Generic.HashSet<string> loadedFolders=new System.Collections.Generic.HashSet<string>();
        readonly BlockingCollection<Action> jobs=new BlockingCollection<Action>(12);
        readonly ConcurrentQueue<Action> completed=new ConcurrentQueue<Action>();
        readonly object lifetime=new object();
        IntPtr handle;bool disposed;
        volatile bool stopped;public bool Stopped=>stopped;
        public OnDeviceNative(string pack,string libraries,OnDeviceConversationSettings settings,Action<string> ready)
        {
            var thread=new Thread(()=>
            {
                try
                {
                    if(Environment.OSVersion.Platform==PlatformID.Win32NT)lock(loadedFolders)
                    {
                        if(!loadedFolders.Contains(libraries))foreach(string dll in new[]{"ggml-base.dll","ggml.dll","llama.dll","onnxruntime.dll","sherpa-onnx-c-api.dll","deadlyjenga_conversation.dll"})
                            if(LoadLibraryEx(Path.Combine(libraries,dll),IntPtr.Zero,0x100|0x1000)==IntPtr.Zero)throw new InvalidOperationException("Cannot load "+dll+" (Windows error "+Marshal.GetLastWin32Error()+")");
                        loadedFolders.Add(libraries);
                    }
                    if(dj_api_version()!=1)throw new InvalidOperationException("Native conversation ABI mismatch");
                    string FileAt(string name)=>Path.Combine(pack,name);
                    var created=dj_create(libraries,FileAt("language.gguf"),FileAt("recognition/encoder-epoch-99-avg-1.int8.onnx"),FileAt("recognition/decoder-epoch-99-avg-1.int8.onnx"),FileAt("recognition/joiner-epoch-99-avg-1.int8.onnx"),FileAt("recognition/tokens.txt"),FileAt("voice/model.fp16.onnx"),FileAt("voice/voices.bin"),FileAt("voice/tokens.txt"),FileAt("voice/espeak-ng-data"),settings.cpuThreads,settings.contextTokens);
                    lock(lifetime){handle=created;if(disposed&&handle!=IntPtr.Zero)dj_cancel(handle);}
                    if(created==IntPtr.Zero)throw new InvalidOperationException(Error());
                    completed.Enqueue(()=>ready(null));
                    foreach(var job in jobs.GetConsumingEnumerable())
                    {if(disposed)break;try{job();}catch(Exception e){string failure=e.Message;completed.Enqueue(()=>ready(failure));}}
                }
                catch(Exception e){string failure=e.Message;completed.Enqueue(()=>ready(failure));}
                finally{lock(lifetime){if(handle!=IntPtr.Zero){dj_destroy(handle);handle=IntPtr.Zero;}}stopped=true;}
            }){IsBackground=true,Name="Deadly Jenga conversation",Priority=ThreadPriority.BelowNormal};
            thread.Start();
        }
        static string Error()=>Marshal.PtrToStringAnsi(dj_error())??"Native inference failed";
        public void Pump(){for(int i=0;i<12&&completed.TryDequeue(out var action);i++)action();}
        public bool Submit(Action<OnDeviceNative> action){try{return !disposed&&jobs.TryAdd(()=>action(this));}catch(InvalidOperationException){return false;}}
        public string Generate(string system,string message,int tokens,int timeout)
        {var output=new byte[4096];int n=dj_generate(handle,system,message,tokens,timeout,output,output.Length);return n==-2?null:n<0?throw new InvalidOperationException(Error()):Encoding.UTF8.GetString(output,0,n);}
        public string Listen(int speaker,float[] audio,bool final)
        {var output=new byte[2048];int n=dj_listen(handle,speaker,audio,audio.Length,final?1:0,output,output.Length);if(n<0)throw new InvalidOperationException(Error());return Encoding.UTF8.GetString(output,0,n);}
        public float[] Synthesize(string text,int speaker,float speed)
        {
            int count=dj_synthesize(handle,text,speaker,speed,out var pointer,out int rate);if(count==-2)return null;if(count<0)throw new InvalidOperationException(Error());
            if(rate<8000||rate>96000||count>480000)throw new InvalidOperationException("Invalid generated audio size");
            var input=new float[count];Marshal.Copy(pointer,input,0,count);var result=new float[(int)((long)count*16000/rate)];
            for(int i=0;i<result.Length;i++){float at=(float)i*rate/16000;int a=Math.Min(count-1,(int)at),b=Math.Min(count-1,a+1);result[i]=input[a]+(input[b]-input[a])*(at-a);}
            return result;
        }
        public void Forget(int speaker){dj_forget(handle,speaker);}
        public void Complete(Action completion){completed.Enqueue(completion);}
        public void Cancel(){lock(lifetime){if(handle!=IntPtr.Zero)dj_cancel(handle);}}
        public void Dispose(){lock(lifetime){if(disposed)return;disposed=true;if(handle!=IntPtr.Zero)dj_cancel(handle);jobs.CompleteAdding();}}
    }
}

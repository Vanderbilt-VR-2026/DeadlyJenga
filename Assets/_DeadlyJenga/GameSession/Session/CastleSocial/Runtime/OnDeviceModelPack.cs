using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DeadlyJenga.CastleSocial
{
    internal static class OnDeviceModelPack
    {
        [Serializable] sealed class Entry {public string path,sha256;public long bytes;}
        [Serializable] sealed class Manifest {public string version;public int nativeApi;public Entry[] files;}
        public static string EditorLibraries;
        static Task<bool> editorValidation;static string validationKey;
        public static IEnumerator Prepare(OnDeviceConversationSettings settings,Action<string> progress,Action<string,string> done,Func<bool> cancelled)
        {
#if UNITY_EDITOR
            string local=Path.GetFullPath(Path.Combine(Application.dataPath,"..",settings.editorPackFolder));
            if(!File.Exists(Path.Combine(local,"manifest.json"))){done(null,"Model pack missing. Run Tools/OnDeviceConversation/prepare.py once before using this demo.");yield break;}
            progress("Checking local conversation models");
            string key=local+File.GetLastWriteTimeUtc(Path.Combine(local,"manifest.json")).Ticks;
            if(editorValidation==null||validationKey!=key)
            {validationKey=key;editorValidation=Task.Run(()=>
            {
                try{var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(local,"manifest.json")));return manifest.nativeApi==1&&manifest.files.All(entry=>Matches(Path.Combine(local,entry.path),entry));}
                catch(Exception){return false;}
            });}
            var validation=editorValidation;
            while(!validation.IsCompleted){if(cancelled())yield break;yield return null;}
            done(validation.Result?local:null,validation.Result?null:"Conversation pack is incomplete or corrupted; run prepare.py again.");yield break;
#else
            string source=Application.streamingAssetsPath.TrimEnd('/')+"/CastleConversation";
            string json;
            using(var request=UnityWebRequest.Get(source+"/manifest.json"))
            {
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){done(null,"This build has no conversation model pack. Build with the prepared pack to enable it.");yield break;}
                json=request.downloadHandler.text;
            }
            Manifest manifest=null;string failure=null;
            try{manifest=JsonUtility.FromJson<Manifest>(json);if(manifest.nativeApi!=1||manifest.version!="dj-conversation-1"||manifest.files==null||manifest.files.Length>2000)throw new IOException("Unsupported conversation pack");}
            catch(Exception e){failure=e.Message;}
            if(failure!=null){done(null,failure);yield break;}
            string root=Path.Combine(Application.persistentDataPath,"Conversation",manifest.version);Directory.CreateDirectory(root);
            long total=0;int index=0;
            foreach(var entry in manifest.files)
            {
                if(cancelled())yield break;
                if(string.IsNullOrEmpty(entry.path)||entry.path.Contains("..")||entry.path.Contains(":")||entry.path.StartsWith("/")||entry.path.StartsWith("\\")||entry.bytes<0||entry.sha256==null||entry.sha256.Length!=64||(total+=entry.bytes)>1024L*1024*1024){done(null,"Invalid conversation manifest");yield break;}
                string target=Path.GetFullPath(Path.Combine(root,entry.path));
                if(!target.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.Ordinal)){done(null,"Invalid model path");yield break;}
                progress("Preparing on-device models · "+(++index)+" / "+manifest.files.Length);
                var check=Task.Run(()=>Matches(target,entry));while(!check.IsCompleted){if(cancelled())yield break;yield return null;}
                if(check.Result)continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));string partial=target+".part";
                using(var request=UnityWebRequest.Get(source+"/"+entry.path))
                {
                    request.downloadHandler=new DownloadHandlerFile(partial){removeFileOnAbort=true};var operation=request.SendWebRequest();
                    while(!operation.isDone){if(cancelled()){request.Abort();yield break;}yield return null;}
                    if(request.result!=UnityWebRequest.Result.Success){done(null,"Cannot unpack models: "+request.error);yield break;}
                }
                check=Task.Run(()=>Matches(partial,entry));while(!check.IsCompleted){if(cancelled())yield break;yield return null;}
                if(!check.Result){File.Delete(partial);done(null,"Conversation model checksum mismatch");yield break;}
                if(File.Exists(target))File.Delete(target);File.Move(partial,target);
            }
            done(root,null);
#endif
        }
        static bool Matches(string path,Entry entry)
        {
            try{if(!File.Exists(path)||new FileInfo(path).Length!=entry.bytes)return false;using(var input=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").Equals(entry.sha256,StringComparison.OrdinalIgnoreCase);}
            catch(IOException){return false;}
        }
        public static string Libraries()
        {
#if UNITY_EDITOR
            return EditorLibraries;
#elif UNITY_ANDROID
            using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var info=activity.Call<AndroidJavaObject>("getApplicationInfo"))return info.Get<string>("nativeLibraryDir");
#else
            string folder=Path.Combine(Application.dataPath,"Plugins","x86_64");return Directory.Exists(folder)?folder:Path.Combine(Application.dataPath,"Plugins");
#endif
        }
    }
}

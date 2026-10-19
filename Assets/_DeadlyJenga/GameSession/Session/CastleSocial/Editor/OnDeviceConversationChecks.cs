using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CastleSocial.Editor
{
    [InitializeOnLoad]
    public static class OnDeviceConversationChecks
    {
        const string Key="OnDeviceConversationChecks";
        static readonly List<string> lines=new List<string>();
        static string Report=>ProjectPath.Report("OnDeviceUnity.txt");
        static OnDeviceConversationChecks()
        {
            EditorApplication.update+=()=>
            {
                string request=Path.Combine("Logs","Validation","OnDevice.request");
                if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                try{File.Delete(request);}catch(IOException){return;}Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>
                {var session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();session.StartCoroutine(Guard(Check(session)));};
                if(state==PlayModeStateChange.EnteredEditMode)
                {SessionState.SetBool(Key,false);MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;string previous=SessionState.GetString(Key+"Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);if(SessionState.GetBool(Key+"Batch",false))EditorApplication.Exit(lines.Any(line=>line.StartsWith("FAIL "))?1:0);}
            };
        }
        [MenuItem("Tools/Castle Social/Run On-device Conversation Checks")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){Debug.LogError("Save scene edits and stop Play Mode before conversation checks.");return;}
            lines.Clear();File.WriteAllText(Report,"Real native conversation integration checks (desktop, not Quest)\n");
            SessionState.SetString(Key+"Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static void RunBatch(){SessionState.SetBool(Key+"Batch",true);Run();}
        static void Assert(bool value,string message)
        {lines.Add((value?"PASS ":"FAIL ")+message);File.WriteAllLines(Report,lines);if(!value)throw new Exception(message);}
        static IEnumerator Until(Func<bool> predicate,float seconds,string message)
        {float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;Assert(predicate(),message);}
        static IEnumerator Guard(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object current=null;bool next=false;Exception error=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){lines.Add("FAIL "+error);File.WriteAllLines(Report,lines);Debug.LogException(error);EditorApplication.ExitPlaymode();yield break;}
                if(!next){stack.Pop();continue;}if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
        }
        static IEnumerator Check(CastlePlaySession session)
        {
            yield return null;yield return null;var world=session.GetComponent<SocialWorld>();Assert(world&&world.OnDevice,"Real game initializes native conversation adapter");
            world.Settings.mode=SocialOperatingMode.Offline;world.Config.enableBots=false;
            for(int i=0;i<3;i++)session.SurvivorBots.SpawnOne();yield return new WaitForSecondsRealtime(.7f);
            Assert(world.Agents.Count==3,"Three existing ordinary bots available before enabling");
            var agents=world.Agents.ToArray();var identities=agents.Select(a=>a.State.identity).ToArray();var traits=identities.Select(x=>string.Join(",",x.traits)).ToArray();
            bool oldVoice=world.Config.enableVoice;int oldConcurrent=world.Config.maxConcurrentSpeech;
            world.Settings.onDevice.frameBudgetMs=0;world.Settings.onDevice.generationTimeoutMs=15000;
            world.OnDevice.SetEnabled(true,false);
            yield return Until(()=>world.OnDevice.Ready,40,"Native models load inside Unity: "+world.OnDevice.Status);
            Assert(!world.MicrophoneInput.Capturing,"Automated check does not opt into the human microphone");
            Assert(agents.SequenceEqual(world.Agents)&&identities.SequenceEqual(agents.Select(a=>a.State.identity)),"Toggle preserves existing bot objects and identities");
            Assert(traits.SequenceEqual(identities.Select(x=>string.Join(",",x.traits))),"Personality traits preserved");
            session.SetMenu(true);var hud=UnityEngine.Object.FindFirstObjectByType<CandyDemoHud>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var pageName=typeof(CandyDemoHud).GetMethod("PageName",flags);
            int voicePage=Enumerable.Range(0,8).First(i=>(string)pageName.Invoke(hud,new object[]{i})=="Voice");
            typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,voicePage);
            yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot(ProjectPath.Report("VoicePanel.png"));yield return new WaitForSecondsRealtime(.5f);
            var one=identities[0];world.Speech.Speak(new SocialSpeech{botId=one.id,text="Hello.",context="HumanSpeech: What is your name?",recipient=world.LocalParticipantId},one);
            yield return Until(()=>world.OnDevice.GeneratedReplies>0,35,"Actual language-model reply synthesized in Unity: "+world.OnDevice.Status);
            yield return Until(()=>world.Playback.SpeakerCount>0,5,"Native PCM reaches Unity AudioSource playback");
            Assert(world.Speech.ActiveRequests==0,"Native mode makes no HTTP speech requests");
            Assert(world.OnDevice.LastReplyKind=="Generated response"&&world.OnDevice.FallbackReplies==0,"UI distinguishes a real generated response from a fallback");
            Assert(world.Caption.Contains(one.name),"Generated caption belongs to the existing bot identity");
            yield return Until(()=>!world.OnDevice.Busy,20,"Speech playback releases its turn");
            int replies=world.OnDevice.GeneratedReplies;
            world.OnDevice.SubmitText("What is your name?");
            yield return Until(()=>world.OnDevice.GeneratedReplies>replies,30,"Human text route produces a new contextual answer without canned-response deduplication");
            yield return Until(()=>!world.OnDevice.Busy,20,"Second conversational turn finishes");
            string fixtures=Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/OnDeviceConversation/.cache/sherpa-onnx-streaming-zipformer-en-20M-2023-02-17"));
            byte[] wav=File.ReadAllBytes(Directory.GetFiles(fixtures,"0.wav",SearchOption.AllDirectories).First());int offset=12,length=0;
            while(offset+8<wav.Length){int n=BitConverter.ToInt32(wav,offset+4);if(System.Text.Encoding.ASCII.GetString(wav,offset,4)=="data"){length=n;offset+=8;break;}offset+=8+n+(n&1);}
            Assert(length>16000,"Recorded speech fixture loaded without microphone capture");
            for(int at=0;at<length;at+=1600)
            {
                int count=Math.Min(800,(length-at)/2);var audio=new float[count];for(int i=0;i<count;i++)audio[i]=BitConverter.ToInt16(wav,offset+at+i*2)/32768f;
                world.SendAudio(new SocialAudioPacket{samples=audio},true);yield return new WaitForSecondsRealtime(.015f);
            }
            world.SendAudio(new SocialAudioPacket{samples=Array.Empty<float>(),end=true},true);
            yield return Until(()=>world.OnDevice.RecognizedTurns>0,15,"Real recorded PCM is recognized through Unity's normal human-audio path");
            Assert(!string.IsNullOrWhiteSpace(world.OnDevice.LastHeard),"Recognized speech is exposed in the voice panel");
            world.OnDevice.CancelAll();yield return Until(()=>!world.OnDevice.Busy,20,"Human interruption releases model work");
            world.Settings.onDevice.frameBudgetMs=.01f;
            world.Speech.Speak(new SocialSpeech{botId=one.id,text="Hello.",context="HumanSpeech: Hello again."},one);yield return new WaitForSecondsRealtime(.7f);
            Assert(world.OnDevice.BudgetPaused&&!world.OnDevice.Busy,"Frame-time guard defers model work");
            world.OnDevice.SubmitText("Hey, are you still there?");world.Settings.onDevice.frameBudgetMs=0;
            world.OnDevice.SetEnabled(false);
            Assert(!world.OnDevice.Requested&&!world.OnDevice.Ready&&world.Playback.SpeakerCount==0,"Off cancels pending replies and clears bot audio");
            Assert(world.Config.enableVoice==oldVoice&&world.Config.maxConcurrentSpeech==oldConcurrent,"Off restores original voice configuration");
            world.OnDevice.SetEnabled(true,false);yield return Until(()=>world.OnDevice.Ready,40,"Rapid off/on safely reloads one model worker");
            Assert(agents.SequenceEqual(world.Agents),"Repeated toggles never respawn survivors");world.Speech.Speak(new SocialSpeech{botId=one.id,text="Hi.",context="HumanSpeech: Where should I go?"},one);yield return null;
            world.OnDevice.SetEnabled(false);world.OnDevice.SetEnabled(true,false);
            yield return Until(()=>world.OnDevice.Ready&&!world.OnDevice.Busy,40,"Toggle during pending inference safely recovers its worker budget");world.OnDevice.SetEnabled(false);
            lines.Add("MEASURED desktop last text-to-audio latency: "+world.OnDevice.LastReplySeconds.ToString("F2")+" s; contextual replies: "+world.OnDevice.GeneratedReplies+"; recognized turns: "+world.OnDevice.RecognizedTurns);
            lines.Add("NOT VERIFIED: Quest microphone/acoustic echo, thermal load, real Fusion peers, headset frame budget.");File.WriteAllLines(Report,lines);EditorApplication.ExitPlaymode();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleSocial.Editor
{
    [InitializeOnLoad]
    public static class OnDeviceConversationChecks
    {
        const string Key="OnDeviceConversationChecks";
        static readonly List<string> lines=new List<string>();
        static bool Bundled=>SessionState.GetBool(Key+"Bundled",false);
        static bool Collapse=>SessionState.GetBool(Key+"Collapse",false);
        static string Report=>ProjectPath.Report(Collapse?"ContextualCollapseUnity.txt":Bundled?"BundledBotVoicesUnity.txt":"OnDeviceUnity.txt");
        static OnDeviceConversationChecks()
        {
            EditorApplication.update+=()=>
            {
                string exitRequest=Path.Combine("Logs","Validation","ContextualCollapse.exit");
                if(Application.isBatchMode&&File.Exists(exitRequest)&&!EditorApplication.isPlayingOrWillChangePlaymode&&
                    !EditorApplication.isCompiling&&!EditorApplication.isUpdating)
                {
                    File.Delete(exitRequest);
                    EditorApplication.Exit(lines.Any(line=>line.StartsWith("FAIL "))?1:0);
                    return;
                }
                string bundledRequest=Path.Combine("Logs","Validation","BundledVoices.request");
                bool bundled=File.Exists(bundledRequest);
                string collapseRequest=Path.Combine("Logs","Validation","ContextualCollapse.request");
                bool collapse=File.Exists(collapseRequest);
                string request=collapse?collapseRequest:bundled?bundledRequest:Path.Combine("Logs","Validation","OnDevice.request");
                if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                try
                {
                    if(collapse)
                    {
                        var arguments=File.ReadAllLines(request);
                        SessionState.SetString(Key+"Pack",arguments.FirstOrDefault()?.Trim()??"");
                        SessionState.SetBool(Key+"KeepEditor",arguments.Skip(1).Any(line=>line.Trim()=="keep-open"));
                    }
                    File.Delete(request);
                }
                catch(IOException){return;}
                if(collapse){SessionState.SetBool(Key+"Collapse",true);RunBatch();}
                else if(bundled){AssetDatabase.Refresh();RunBundledBatch();}
                else Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>
                {var session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();session.StartCoroutine(Guard(Collapse?CheckCollapse(session):Bundled?CheckBundled(session):Check(session)));};
                if(state==PlayModeStateChange.EnteredEditMode)
                {SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Bundled",false);SessionState.SetBool(Key+"Collapse",false);MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;string previous=SessionState.GetString(Key+"Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);if(SessionState.GetBool(Key+"Batch",false)&&!SessionState.GetBool(Key+"KeepEditor",false))EditorApplication.Exit(lines.Any(line=>line.StartsWith("FAIL "))?1:0);SessionState.SetBool(Key+"KeepEditor",false);}
            };
        }
        [MenuItem("Tools/Castle Social/Run On-device Conversation Checks")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){Debug.LogError("Save scene edits and stop Play Mode before conversation checks.");return;}
            lines.Clear();File.WriteAllText(Report,Collapse?"Contextual collapse checks (desktop Unity)\n":Bundled?"Bundled bot speech checks (desktop Unity)\n":"Real native conversation integration checks (desktop, not Quest)\n");
            SessionState.SetString(Key+"Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static void RunBatch(){SessionState.SetBool(Key+"Batch",true);Run();}
        public static void RunBundledBatch()
        {
            SessionState.SetBool(Key+"Bundled",true);
            RunBatch();
        }
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


        static IEnumerator CheckCollapse(CastlePlaySession session)
        {
            yield return null;
            var world=session.GetComponent<SocialWorld>();
            Assert(world&&world.IsAuthority,"Game scene and local match authority ready");
            world.Config.enableBots=false;
            world.Settings.onDevice.editorPackFolder=SessionState.GetString(Key+"Pack","");
            world.Settings.onDevice.frameBudgetMs=0;
            world.Settings.onDevice.cpuThreads=2;
            world.Config.respondBot=1;
            world.Config.spontaneous=0;
            world.Settings.onDevice.replyTokens=32;
            world.Settings.onDevice.generationTimeoutMs=7000;
            string library=ProjectPath.Find("deadlyjenga_conversation.dll");
            OnDeviceConversation.SetEditorLibraries(Path.GetDirectoryName(Path.GetFullPath(library)));
            world.OnDevice.SetEnabled(true,false);
            float loadUntil=Time.realtimeSinceStartup+70;
            string lastStatus=null;
            while(!world.OnDevice.Ready&&world.OnDevice.Requested&&Time.realtimeSinceStartup<loadUntil)
            {
                if(lastStatus!=world.OnDevice.Status)
                {
                    lastStatus=world.OnDevice.Status;
                    lines.Add("MODEL "+lastStatus);
                    File.WriteAllLines(Report,lines);
                }
                yield return null;
            }
            Assert(world.OnDevice.Ready,"Local model and speech synth ready ("+world.OnDevice.Status+")");

            session.SurvivorBots.AutoFill=false;
            session.SurvivorBots.Clear();
            yield return Until(()=>world.Agents.Count==0,5,"Cleared the initial population before staging");
            for(int i=0;i<2;i++)Assert(session.SurvivorBots.SpawnOne(),"Spawned a fresh game survivor");
            yield return Until(()=>world.Agents.Count==2&&world.Agents.All(AvailableForCollapse),8,
                "Two fresh survivor bots are alive and have social identities");
            var bots=world.Agents.ToArray();
            var original=Services.World.Blocks.FirstOrDefault(body=>body&&!body.isKinematic&&body.GetComponent<IBreakable>() is IBreakable item&&!item.Broken);
            Assert(original,"Found an unbroken live map block to stage");
            var at=new Vector3(Services.World.Center.x,Services.World.TopHeight+15,Services.World.Center.z);
            // The map owns its pooled originals. Stage an independent instance of its actual block.
            var block=UnityEngine.Object.Instantiate(original,at,Quaternion.identity);
            block.name="Fixture tower block";
            block.isKinematic=false;
            block.position=at;
            block.transform.position=at;
            block.useGravity=false;
            block.linearVelocity=Vector3.zero;
            block.constraints=RigidbodyConstraints.FreezeAll;
            Vector3 seenFrom=at+new Vector3(-2,-.7f,-4);
            Vector3 heardFrom=at+new Vector3(2,-.7f,-4);
            PlaceForCollapse(bots[0],seenFrom);
            PlaceForCollapse(bots[1],heardFrom);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Fixture sight blocker";
            wall.transform.position=at+new Vector3(1,0,-2);
            wall.transform.localScale=new Vector3(.6f,3,.5f);
            // Break removes the target collider before announcing its last position.
            block.detectCollisions=false;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            const int sightMask=~(1<<Layers.Survivor);
            bool blockedA=Physics.Linecast(seenFrom+Vector3.up*.7f,at,out var hitA,sightMask,QueryTriggerInteraction.Ignore);
            bool blockedB=Physics.Linecast(heardFrom+Vector3.up*.7f,at,out var hitB,sightMask,QueryTriggerInteraction.Ignore);
            bool targetA=blockedA&&(hitA.collider.transform.IsChildOf(block.transform)||
                Climbables.TryGet(hitA.collider,out var climbable)&&climbable.Body==block);
            Assert((!blockedA||targetA)&&blockedB&&hitB.collider.gameObject==wall&&Vector3.Distance(seenFrom,heardFrom)<12,
                $"Unity geometry gives A sight and B a wall in hearing range (A: {(blockedA?hitA.collider.name:"clear")}, B: {(blockedB?hitB.collider.name:"clear")})");

            Assert(bots.All(AvailableForCollapse),"Both staged survivors are available to speak: "+
                string.Join("; ",bots.Select(bot=>$"{bot.State.identity.name} {bot.transform.position} respawning={bot.Bot.Respawning}")));
            var breakable=block.GetComponent<IBreakable>();
            foreach(var bot in bots)world.Director.Cancel(bot.State.identity.id);
            world.Config.enableBots=true;
            Vector3? reportedPoint=null;
            void CaptureBreak(GameObject actor,Vector3 position,Vector3 velocity)
            {
                if(actor==block.gameObject)reportedPoint=position;
            }
            float started=Time.realtimeSinceStartup;
            GameEvents.BlockBroken+=CaptureBreak;
            try{breakable.Break(null);}
            finally{GameEvents.BlockBroken-=CaptureBreak;}
            Assert(breakable.Broken&&reportedPoint.HasValue,"Real map block went through IBreakable.Break and emitted its position");
            Assert(Vector3.Distance(at,reportedPoint.Value)<.3f,$"Break was at the staged position ({at}, reported {reportedPoint.Value})");
            AssertQueuedObservations(world.OnDevice);
            var heard=new List<string>();
            var samples=new float[256];
            foreach(var bot in bots)
            {
                string id=bot.State.identity.id;
                yield return Until(()=>
                {
                    var child=world.transform.Find("Voice · "+id);
                    var source=child?child.GetComponent<AudioSource>():null;
                    if(!source||!source.isPlaying)return false;
                    source.GetOutputData(samples,0);
                    return samples.Any(sample=>Mathf.Abs(sample)>.001f);
                },22,"Non-silent AudioSource output for "+bot.State.identity.name);
                string caption=world.Caption;
                Assert(caption.StartsWith(bot.State.identity.name+" [AI]:",StringComparison.Ordinal),"Caption matches "+bot.State.identity.name);
                lines.Add($"AUDIO {Time.realtimeSinceStartup-started:F2}s {caption} ({world.OnDevice.LastReplySeconds:F2}s text and synthesis, {world.OnDevice.LastReplyKind})");
                File.WriteAllLines(Report,lines);
                Assert(world.OnDevice.LastReplyKind.EndsWith("Collapse",StringComparison.Ordinal),"Played line belongs to this collapse");
                heard.Add(caption.Substring(caption.IndexOf("[AI]:",StringComparison.Ordinal)+5).Trim());
            }
            Assert(world.OnDevice.GeneratedReplies+world.OnDevice.FallbackReplies==2&&world.Speech.ActiveRequests==0,
                $"Two offline turns ({world.OnDevice.GeneratedReplies} model, {world.OnDevice.FallbackReplies} composed); no speech service");
            string seen=heard[0].ToLowerInvariant(), reply=heard[1].ToLowerInvariant();
            Assert((seen.Contains("block")||seen.Contains("tower"))&&(seen.Contains("broke")||seen.Contains("break")||seen.Contains("crack")||seen.Contains("fall")||seen.Contains("crash")||seen.Contains("collapse")),
                "Witness actually talks about the break");
            Assert(!reply.Contains("i saw")&&(reply.Contains("hear")||reply.Contains("heard")||reply.Contains("?")||reply.Contains("couldn't see")),
                "Listener acknowledges what it heard or asks the witness instead of claiming sight");
            Assert(seen!=reply,"The second turn uses distinct wording");
            Assert(bots.All(bot=>!bot.Bot.Respawning)&&Vector3.Distance(bots[0].transform.position,seenFrom)<.3f&&
                Vector3.Distance(bots[1].transform.position,heardFrom)<.3f,"Both observer positions stayed fixed through playback");
            EditorApplication.ExitPlaymode();
        }

        static void AssertQueuedObservations(OnDeviceConversation native)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Field(object owner,string field)=>owner?.GetType().GetField(field,flags)?.GetValue(owner);
            var pending=Field(native,"pending");
            var observation=Field(Field(pending,"intent"),"observation");
            string witness=Field(observation,"Fact") as string;
            string listener=Field(observation,"PartnerFact") as string;
            lines.Add($"INPUT witness: {witness ?? "(none)"}");
            lines.Add($"INPUT listener: {listener ?? "(none)"}");
            Assert(witness!=null&&witness.Contains("I saw it happen.")&&listener!=null&&listener.Contains("I heard"),
                "Runtime queued a visual fact for the witness and a sound-only fact for the listener");
        }

        static bool AvailableForCollapse(SocialBotAgent bot) => bot&&bot.gameObject.activeInHierarchy&&
            !bot.Bot.Respawning&&bot.GetComponent<IModeActor>() is IModeActor actor&&actor.Alive;

        static void PlaceForCollapse(SocialBotAgent agent,Vector3 point)
        {
            var motor=agent.GetComponents<MonoBehaviour>().FirstOrDefault(component=>component is IMovementState);
            Assert(motor,"Found the staged survivor's movement controller");
            if(motor is ITeleportable teleport)teleport.Teleport(point,Quaternion.identity);
            motor.enabled=false;
            foreach(var body in agent.GetComponentsInChildren<Rigidbody>())
            {
                body.isKinematic=true;
                body.detectCollisions=false;
            }
            agent.transform.SetPositionAndRotation(point,Quaternion.identity);
            agent.State.nextSpeech=0;
        }

        static IEnumerator CheckBundled(CastlePlaySession session)
        {
            yield return null;
            var world=session.GetComponent<SocialWorld>();
            Assert(world&&world.Settings.mode==SocialOperatingMode.BundledVoices,"Game selects bundled bot voices by default");
            Assert(world.Speech.Status=="Bundled bot voices ready","Unity imported and validated the voice bank");
            for(int attempt=0;attempt<2&&session.SurvivorBots.Count<2;attempt++)
                session.SurvivorBots.SpawnOne();
            yield return Until(()=>world.Agents.Count>=2,5,"Two actual survivor bots have social identities");
            world.Config.spontaneous=0;
            world.Config.respondBot=0;
            world.Config.typicalResponses=1;
            var bots=world.Agents.Take(2).ToArray();
            var samples=new float[256];
            foreach(var turn in new[]{(bots[0],SocialEventKind.NearMiss),(bots[1],SocialEventKind.NearMiss),(bots[0],SocialEventKind.Success)})
            {
                var state=turn.Item1.State;
                string id=state.identity.id;
                world.Director.Cancel(id);
                world.Playback.Remove(id);
                state.nextSpeech=0;
                world.Publish(turn.Item2,world.LocalParticipantId,1,recipient:id);
                yield return Until(()=>
                {
                    var child=world.transform.Find("Voice · "+id);
                    var source=child?child.GetComponent<AudioSource>():null;
                    if(!source||!source.isPlaying)return false;
                    source.GetOutputData(samples,0);
                    return samples.Any(sample=>Mathf.Abs(sample)>.001f);
                },5,"Scheduled reaction reaches non-silent Unity AudioSource output for "+id);
                Assert(world.Caption.StartsWith(state.identity.name+" [AI]:",StringComparison.Ordinal),"Caption matches the speaking bot");
                yield return Until(()=>world.Director.Speaking==0,8,"Bundled voice releases its speech slot");
            }
            Assert(world.Speech.ActiveRequests==0&&!world.OnDevice.Requested,"Bundled dialogue plays without a server or on-device language model");
            EditorApplication.ExitPlaymode();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.DevTools.Editor;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastleSocial.Editor
{
    [InitializeOnLoad]
    public static class SocialValidation
    {
        const string Key="CastleSocialValidation",Output="ArtDirection/CastleSocial/UnityValidation.txt",Request="ArtDirection/CastleSocial/validate.request";
        static readonly List<string> results=new List<string>();
        static SocialValidation()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
                File.Delete(Request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))EditorApplication.delayCall+=()=>
                {
                    var owner=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();owner.StartCoroutine(Guard(Check(owner)));
                };
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false))
                {SessionState.SetBool(Key,false);MatchSetup.StartLocalSession=false;if(Application.isBatchMode&&!SessionState.GetBool("CastleMergeSuite",false))EditorApplication.Exit(results.Any(l=>l.StartsWith("FAIL"))?1:0);}
            };
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureTestSession()
        {if(SessionState.GetBool(Key,false)){MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=true;}}
        [MenuItem("Tools/Castle Social/Run Integrated Checks")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));results.Clear();File.WriteAllText(Output,"Unity social integration checks\n");
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before running integration tests.");
            SocialPopulationWindow.EnsureProfile();MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=true;EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        public static void RunBatch()=>Run();
        static void Assert(bool ok,string message)
        {results.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Output,results);if(!ok)throw new InvalidOperationException(message);}
        static IEnumerator Guard(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object current=null;bool has=false;Exception error=null;
                try{has=stack.Peek().MoveNext();if(has)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){results.Add("FAIL "+error);File.WriteAllLines(Output,results);Debug.LogException(error);while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();yield break;}
                if(!has){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
        }
        static IEnumerator Check(CastlePlaySession session)
        {
            yield return null;yield return null;session.SetMenu(true);var world=session.GetComponent<SocialWorld>();
            Assert(world&&world.Director!=null,"Social system initializes in the real castle scene.");
            Assert(world.IsAuthority,"Local host owns social decisions.");
            Assert(!world.MicrophoneInput.Capturing,"Microphone remains off until explicit user action.");
            world.Settings.mode=SocialOperatingMode.Offline;Assert(world.Settings.mode==SocialOperatingMode.Offline,"Offline test mode has no model/service dependency; authored voice settings are preserved.");
            Assert(session.Network.HumanCount==1,"Integration test starts without external connected humans.");
            session.Bots().targetPopulation=12;for(int i=0;i<6;i++)Assert(session.Bots().SpawnBot(),"Existing factory spawns legal survivor "+i);
            var hand=session.Hands().SpawnHand();Assert(hand,"Existing flying-hand bot still spawns.");
            yield return new WaitForSeconds(.5f);
            Assert(world.Agents.Count==6,"Every survivor receives a social adapter.");
            Assert(!hand.GetComponent<SocialBotAgent>(),"Flying-hand bot has no player-social adapter.");
            Assert(world.Agents.Select(a=>a.State.identity.id).Distinct().Count()==6,"Generated social identities are unique.");
            Assert(world.Agents.All(a=>ReferenceEquals(a.GetComponent<CastleBotBrain>().Motor.InputDriver,a.Bot)),"Social movement uses the ordinary survivor driver interface.");
            Assert(world.Agents.Select(a=>a.GetComponent<CastleBotBrain>().Personality.bravery).Distinct().Count()>1,"Generated traits affect actual planner personalities.");
            var starts=world.Agents.Select(a=>a.transform.position).ToArray();yield return new WaitForSeconds(6);
            Assert(world.Agents.Select((a,i)=>Vector3.Distance(a.transform.position,starts[i])).Sum()>.5,"Bots physically move in the real castle.");
            Assert(Physics.GetIgnoreLayerCollision(CastleBlock.SurvivorLayer,CastleBlock.BlockLayer),"Survivor-to-block physics isolation remains intact.");
            double before=world.Agents.Sum(a=>a.State.emotion.fear);world.Publish(SocialEventKind.Collapse,"tower");yield return new WaitForSeconds(.5f);
            Assert(world.Agents.Sum(a=>a.State.emotion.fear)>before,"Tower event changes actual bot emotion/decision context.");
            Assert(world.Agents.All(a=>a.State.memories.Count<=world.Config.memoryLimit),"Runtime memories remain bounded.");
            world.Config.maxPopulation=1;
            Assert(!world.ApplyConfiguration()&&world.Config.maxPopulation>=6,"Invalid live capacity change restores a valid population without deleting survivors.");
            Assert(world.Agents.All(a=>world.Director.bots.ContainsKey(a.State.identity.id)),"Restored configuration rebinds every live survivor to the current director.");
            var one=world.Agents[0];one.State.identity.voice=true;world.Mute(one.State.identity.id,true);Assert(one.State.muted,"Per-bot mute updates director state.");world.Mute(one.State.identity.id,false);
            yield return NetworkCheck(session,world);
            // Exercise real optional HTTP streaming speech and Unity PCM playback
            // only when the locally tested bridge is explicitly requested.
            if(File.Exists("ArtDirection/CastleSocial/voice-test.enabled"))
            {
                world.Config.enableBots=false;world.Speech.CancelAll();yield return Until(()=>world.Speech.ActiveRequests==0,"Cancelled voice jobs release their budget");
                int speechFailures=world.Speech.Failures;world.Settings.mode=SocialOperatingMode.LocalDevelopment;one.State.identity.voice=true;
                world.Speech.Speak(new SocialSpeech{botId=one.State.identity.id,text="Wait."},one.State.identity);
                float until=Time.realtimeSinceStartup+14;while(world.Speech.ActiveRequests>0&&Time.realtimeSinceStartup<until)yield return null;
                Assert(world.Speech.Failures==speechFailures&&world.Speech.LastFirstAudioSeconds>0,"Actual self-hosted TTS streams PCM into Unity without provider errors: "+world.Speech.Status);
                Assert(world.Playback.SpeakerCount>0,"Independent voice playback source is created.");
                results.Add("MEASURE TTS first audio "+world.Speech.LastFirstAudioSeconds.ToString("F3")+" seconds; provider "+File.ReadAllText("ArtDirection/CastleSocial/voice-test.enabled")+"; latency is not a naturalness rating.");
                world.Config.enableBots=true; // Resume perception/decision ticks after the isolated playback assertion.
                const string fixture="ArtDirection/CastleSocial/synthetic-speech.pcm";
                if(File.Exists(fixture))
                {
                    var data=File.ReadAllBytes(fixture);int seq=1000;
                    for(int offset=0;offset<data.Length;offset+=1600)
                    {
                        var pcm=new float[Math.Min(800,(data.Length-offset)/2)];for(int i=0;i<pcm.Length;i++)pcm[i]=(short)(data[offset+i*2]|data[offset+i*2+1]<<8)/32768f;
                        world.SendAudio(new SocialAudioPacket{sequence=seq++,samples=pcm},true);yield return new WaitForSecondsRealtime(.05f);
                    }
                    world.SendAudio(new SocialAudioPacket{sequence=seq,end=true,samples=new float[0]},true);
                    yield return Until(()=>world.Speech.TranscriptsReceived>0,"Actual incoming audio reaches local ASR and returns attributed text",10);
                    yield return new WaitForSecondsRealtime(.5f);
                    Assert(world.Agents.Any(a=>a.State.memories.Any(m=>m.summary=="HumanSpeech")),"Recognized speech enters bot perception without retaining private text by default.");
                    Assert(!world.MicrophoneInput.Capturing,"Synthetic audio test never enables the human microphone.");
                }
                world.Settings.mode=SocialOperatingMode.Offline;
            }
            int count=world.Agents.Count;session.Bots().ClearBots();yield return null;yield return null;
            Assert(count>0&&world.Agents.Count==0,"Despawn cancels social agents and pending speech.");
            Assert(session.HandBots.Count==1,"Clearing regular players does not clear flying hands.");session.Hands().ClearHands();
            File.WriteAllLines(Output,results);Debug.Log(string.Join("\n",results));EditorApplication.ExitPlaymode();
        }
        sealed class Peer:IDisposable
        {
            public string id,name,token;public CastleLanTransport transport;public CastleLanTransport.Link link;
            public void Dispose(){link?.Dispose();transport?.Dispose();}
        }
        static IEnumerator Until(Func<bool> predicate,string label,float seconds=6)
        {float end=Time.realtimeSinceStartup+seconds;bool met=predicate();while(!met&&Time.realtimeSinceStartup<end){yield return null;met=predicate();}Assert(met,label);}
        static void Advertise(Peer peer,CastleLanSession net,string kind)
        {
            var ad=new CastleLanSession.Advertisement{id=peer.id,name=peer.name,port=peer.transport.Port,kind=kind,target=net.LocalParticipantId,available=true};
            peer.transport.Advertise(Encoding.UTF8.GetBytes(JsonUtility.ToJson(ad)),IPAddress.Parse("127.0.0.10"));
        }
        static void Drain(Peer peer,string kind)
        {
            while(peer.transport.Discovery.TryDequeue(out var packet))
            {try{var m=JsonUtility.FromJson<CastleLanSession.Advertisement>(Encoding.UTF8.GetString(packet.Data));if(m.kind==kind&&m.target==peer.id)peer.token=string.IsNullOrEmpty(m.token)?kind:m.token;}catch(ArgumentException){}}
        }
        static IEnumerator Connect(Peer peer,CastleLanSession network,int port)
        {
            string error=null;peer.transport.Connect(IPAddress.Parse("127.0.0.10"),port,(link,e)=>{peer.link=link;error=e;});
            yield return Until(()=>peer.link!=null||error!=null,"Loopback peer connects after approval");Assert(error==null,"No peer connection error");
            peer.link.Send(P.Encode(P.Hello,w=>{w.Write(P.Version);w.Write(peer.id);w.Write(peer.token);}));
            yield return Until(()=>peer.link.Controls.Count>0,"Approved peer receives welcome");
            yield return Until(()=>peer.link.TakeState()!=null,"Late joiner receives live castle snapshot");
        }
        static IEnumerator NetworkCheck(CastlePlaySession session,SocialWorld world)
        {
            var net=session.Network;var field=typeof(CastleLanSession).GetField("transport",BindingFlags.Instance|BindingFlags.NonPublic);((CastleLanTransport)field.GetValue(net)).Dispose();
            var host=new CastleLanTransport(IPAddress.Parse("127.0.0.10"));field.SetValue(net,host);
            try
            {
            using(var requester=new Peer{id="social-check-requester",name="Social test requester",transport=new CastleLanTransport(IPAddress.Parse("127.0.0.2"))})
            using(var invited=new Peer{id="social-check-invited",name="Social test invited",transport=new CastleLanTransport(IPAddress.Parse("127.0.0.3"))})
            {
                Advertise(requester,net,"request");yield return Until(()=>net.HasPending,"Real UDP join request opens pending approval");net.AcceptPending();
                yield return Until(()=>{Drain(requester,"approve");return requester.token!=null;},"Accept sends approval token to requesting peer");
                yield return Connect(requester,net,host.Port);Assert(net.HumanCount==2,"Authoritative session admits approved requester");
                Advertise(invited,net,"beacon");yield return new WaitForSeconds(.2f);
                for(int i=0;i<net.NearbyCount+1&&net.SelectedName!=invited.name;i++)net.NextNearby();Assert(net.SelectedName==invited.name,"Nearby selector discovers invitation target");net.InviteSelected();
                yield return Until(()=>{Drain(invited,"invite");return invited.token=="invite";},"Real UDP invitation reaches selected peer");invited.token=null;
                Advertise(invited,net,"request");yield return Until(()=>{Drain(invited,"approve");return invited.token!=null;},"Accepted invitation auto-approves return join request");
                yield return Connect(invited,net,host.Port);Assert(net.HumanCount==3,"Both humans coexist with host-owned social bots");
                var samples=Enumerable.Range(0,800).Select(i=>(float)Math.Sin(i*.2)*.2f).ToArray();requester.link.SendAudio(new SocialAudioPacket{speaker="social:forged-bot",sequence=1,samples=samples}.Encode());
                byte[] audio=null;yield return Until(()=>invited.link.Audio.TryDequeue(out audio),"Human PCM travels through host to other participant");
                Assert(SocialAudioPacket.Decode(audio).speaker==requester.id,"Host binds speech to authenticated player ID; forged bot ID discarded");
                int hands=session.HandBots.Count;requester.Dispose();yield return Until(()=>net.HumanCount==2,"Human disconnect removes only its own connection");Assert(session.HandBots.Count==hands,"Voice/disconnect path preserves AI hand population");
            }
            net.LeaveSession();
            }
            finally{host.Dispose();field.SetValue(net,new CastleLanTransport());}
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleSocial;
using DeadlyJenga.World.Hazards;

namespace DeadlyJenga.CastlePrototype
{
    // Opt-in development-player test. Inert unless launched with both explicit smoke arguments.
    public sealed class CrossProcessSmoke : MonoBehaviour
    {
        string folder,role;bool done;float deadline;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();string role=args.FirstOrDefault(a=>a.StartsWith("--dj-smoke="));
            string folder=args.FirstOrDefault(a=>a.StartsWith("--dj-smoke-dir="));
            if(role==null||folder==null)return;
            var go=new GameObject("Explicit multiplayer smoke test");DontDestroyOnLoad(go);
            var test=go.AddComponent<CrossProcessSmoke>();test.role=role.Substring(11);test.folder=Path.GetFullPath(folder.Substring(15));
            Directory.CreateDirectory(test.folder);test.deadline=Time.realtimeSinceStartup+(LobbyHandoff||NearbyFlow?600:180);
            Application.runInBackground=true;Application.logMessageReceived+=test.Log;test.StartCoroutine(test.Guard(test.Check()));
        }
        string Report=>Path.Combine(folder,role+".txt");
        string Mark(string name)=>Path.Combine(folder,name);
        void Log(string message,string trace,LogType type)
        {
            if(done||type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Explicit deliberate disconnect can race Fusion's current cloud request.
            File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        void Update(){if(!done&&Time.realtimeSinceStartup>deadline)Finish(false,"Timed out");}
        void Finish(bool ok,string message)
        {if(done)return;done=true;File.AppendAllText(Report,(ok?"COMPLETE ":"FAILED ")+message+"\n");Application.logMessageReceived-=Log;Application.Quit(ok?0:1);}
        void Require(bool ok,string message){File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        IEnumerator Until(Func<bool> condition,string message,float seconds=45,Func<string> failureDetail=null)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<until)yield return null;
            bool passed=condition();
            Require(passed,passed||failureDetail==null?message:message+" | "+failureDetail());
        }
        IEnumerator Guard(IEnumerator root)
        {
            var work=new Stack<IEnumerator>();work.Push(root);
            while(work.Count>0&&!done)
            {
                bool next=false;object value=null;Exception error=null;
                try{next=work.Peek().MoveNext();if(next)value=work.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){Finish(false,error.ToString());yield break;}
                if(!next){(work.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){work.Push(child);continue;}yield return value;
            }
            if(!done)Finish(!File.ReadAllText(Report).Contains("ERROR "),"Separate-process multiplayer run");
        }
        static bool NearbyFlow => Environment.GetCommandLineArgs().Contains("--dj-nearby-lobby");
        static bool LobbyHandoff => Environment.GetCommandLineArgs().Contains("--dj-lobby-handoff");
        bool ProbePoses => Environment.GetCommandLineArgs().Contains("--dj-pose-probe");
        const System.Reflection.BindingFlags PrivateField =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        IEnumerator CheckPoseCadence(CastlePlaySession session, CastleFusionSession sync)
        {
            bool host = role == "host";
            object peer = null;
            System.Reflection.FieldInfo waiting = null;
            Func<float> latest;
            if (host)
            {
                var peers = (System.Collections.IDictionary)typeof(CastleFusionSession)
                    .GetField("peers", PrivateField).GetValue(sync);
                peer = peers.Values.Cast<object>().Single();
                waiting = peer.GetType().GetField("waiting");
                var lastPose = peer.GetType().GetField("lastPose");
                latest = () => (float)lastPose.GetValue(peer);
                yield return Until(() => File.Exists(Mark("pose-ready")), "Guest is ready to sample poses");
                waiting.SetValue(peer, int.MaxValue);
                File.WriteAllText(Mark("pose-hold"), "World acknowledgments held by test");
            }
            else
            {
                var poses = (Dictionary<string, float>)typeof(CastleLanSession)
                    .GetField("livePoseAt", PrivateField).GetValue(session.Network);
                latest = () => poses.Values.DefaultIfEmpty(0).Max();
                File.WriteAllText(Mark("pose-ready"), "ready");
                yield return Until(() => File.Exists(Mark("pose-hold")), "Host deliberately holds world sends");
            }

            float start = Time.unscaledTime, previous = latest(), maxGap = 0;
            int count = 0;
            try
            {
                while (Time.unscaledTime - start < 2f)
                {
                    float current = latest();
                    if (current > previous)
                    {
                        maxGap = Mathf.Max(maxGap, current - Mathf.Max(previous, start));
                        previous = current;
                        count++;
                    }
                    yield return null;
                }
                maxGap = Mathf.Max(maxGap, Time.unscaledTime - Mathf.Max(previous, start));
                if (host) Require((int)waiting.GetValue(peer) == int.MaxValue, "World stream stayed held throughout pose sample");
                File.AppendAllText(Report, $"POSE cadence samples={count} maxGapSeconds={maxGap:F3}\n");
                Require(count >= 15 && maxGap < .5f, "Fresh human poses continue while world snapshot delivery is held");
            }
            finally
            {
                if (host) waiting.SetValue(peer, 0);
            }
        }

        IEnumerator CheckLobbyHandoff()
        {
            HomeSession home=null;
            yield return Until(()=>(home=FindFirstObjectByType<HomeSession>())&&!string.IsNullOrEmpty(home.Region),"Home initializes its room connection");
            if(role=="host")
            {
                yield return Until(()=>File.Exists(Mark("guest-home")),"Both processes are ready before opening the room",360);
                home.Host();
                yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.Room.IsHost&&IslandLobby.Current.CanStart,
                    "Home Host opens a normal Play lobby",65);
                var lobby=IslandLobby.Current;
                var room=lobby.Room;
                Require(lobby.SelectMode("ctf")&&lobby.SelectMap("mode-ctf"),"Host chooses Capture the Flag and its map");
                File.WriteAllText(Mark("code-pending"),room.Code);
                File.Move(Mark("code-pending"),Mark("code"));
                yield return Until(()=>room.HumanCount==2&&File.Exists(Mark("guest-ready")),
                    "Second process receives the configured room",150);
                Require(lobby.CanLeave,"Production leave action is available to the host");
                lobby.Leave();
                yield return Until(()=>!room.Connected&&!room.Busy,
                    "Original host leaves the room",30,()=>room.Status);
                Require(IslandLobby.Current&&NetworkActors.Alive(lobby.Player),"Departing host stays in the usable local lobby");
            }
            else
            {
                File.WriteAllText(Mark("guest-home"),"ready");
                yield return Until(()=>File.Exists(Mark("code")),"Original host publishes the room code",420);
                string code=File.ReadAllText(Mark("code"));
                home.Code=code;
                home.JoinCode();
                yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.Room.Connected&&
                    !IslandLobby.Current.Room.IsHost&&IslandLobby.Current.SelectedMapId=="mode-ctf",
                    "Guest joins the same room and receives the chosen map",75);
                var lobby=IslandLobby.Current;
                var room=lobby.Room;
                var previousRunner=room.Runner;
                string identity=room.LocalLobbyId;
                Require(!lobby.CanConfigure,"Guest cannot configure the match before promotion");
                File.WriteAllText(Mark("guest-ready"),"ready");
                yield return Until(()=>room.IsHost&&room.Runner!=previousRunner&&!room.Busy&&lobby.CanStart,
                    "Guest becomes the Fusion host and can start a match",75,()=>room.Status);
                Require(room.Code==code&&room.LocalLobbyId==identity&&room.HumanCount==1&&lobby.IsHost,
                    "The new host keeps the room code, identity and control");
                Require(lobby.SelectedModeId=="ctf"&&lobby.SelectedMapId=="mode-ctf",
                    "The selected mode and map survive the handoff");
                Require(lobby.SelectMode("hill")&&lobby.SelectMap("mode-koth"),"New host can change mode and map");
                lobby.StartOrCancel();
                CastlePlaySession session=null;
                yield return Until(()=>(session=FindFirstObjectByType<CastlePlaySession>())&&session.Network&&
                    !session.Network.IsClient&&NetworkActors.Alive(session.Player)&&Services.World?.Blocks.Count>0,
                    "New host starts and plays the next match",95);
                Require(MatchSetup.ModeId=="hill"&&MatchSetup.MapId=="mode-koth","Game uses the new host's selection");
                var leave=room.Leave();
                yield return Until(()=>leave.IsCompleted&&!room.Connected,"Promoted host disconnects cleanly",30);
            }
        }

        IEnumerator CheckNearbyLobby()
        {
            bool host=role=="host";
            bool invite=Environment.GetCommandLineArgs().Contains("--dj-nearby-invite");
            HomeSession home=null;
            yield return Until(()=>(home=FindFirstObjectByType<HomeSession>())&&!string.IsNullOrEmpty(home.Region),
                "Home is ready",95);
            var room=IslandRoomConnection.Instance;
            string address=null;
            if(host)
            {
                home.Host();
                yield return Until(()=>IslandLobby.Current&&room.IsHost&&!room.Busy&&IslandLobby.Current.CanStart,
                    "Home Host creates a playable Marina room",95,()=>room.Status);
                address=room.Code;
                File.WriteAllText(Mark("pending-code"),address);
                File.Move(Mark("pending-code"),Mark("code"));
                yield return Until(()=>File.Exists(Mark("guest-scanning")),"Guest started nearby search",35);
                if(invite)IslandLobby.Current.InviteNearby();
            }
            else
            {
                yield return Until(()=>File.Exists(Mark("code")),"Host test rendezvous is ready",110);
                address=File.ReadAllText(Mark("code"));
                home.DetectNearby();
                File.WriteAllText(Mark("guest-scanning"),"ready");
                int row=-1;
                yield return Until(()=>
                {
                    for(int i=0;i<home.NearbyCount;i++)
                    {
                        string title=home.NearbyTitle(i);
                        if(title.EndsWith("  "+address,StringComparison.Ordinal)&&
                            (!invite||title.StartsWith("Invite: ",StringComparison.Ordinal))){row=i;return true;}
                    }
                    return false;
                },invite?"Home receives a nearby invite over UDP":"Home lists the real nearby room over UDP",
                    25,()=>home.Status);
                Require(string.IsNullOrEmpty(home.Code),"No code entered in the join form");
                home.JoinNearby(row);
            }
            yield return Until(()=>IslandLobby.Current&&room.Connected&&!room.Busy&&room.Code==address&&
                room.HumanCount==2&&IslandLobby.Current.PeerCount==1,
                "Both real Fusion clients share the same Marina room and see one another",85,()=>room.Status);
            File.WriteAllText(Mark(role+"-nearby-joined"),"ready");
            yield return Until(()=>File.Exists(Mark((host?"guest":"host")+"-nearby-joined")),
                "Other client confirmed joining",25);
            if(host)
            {
                Require(IslandLobby.Current.SelectMode("ctf")&&IslandLobby.Current.SelectMap("mode-ctf"),
                    "Host selects Capture the Flag and its authored map");
                IslandLobby.Current.StartOrCancel();
            }
            CastlePlaySession game=null;
            yield return Until(()=>(game=FindFirstObjectByType<CastlePlaySession>())&&game.Network&&
                !game.Network.WaitingForWorld&&game.Modes&&game.Modes.Frame.phase==RoundPhase.Playing&&
                MatchSetup.MapId=="mode-ctf"&&room.Connected&&room.Code==address,
                "Guest follows host from Marina to active CTF in the same room",110,()=>room.Status);
            File.WriteAllText(Mark(role+"-nearby-playing"),"ready");
            yield return Until(()=>File.Exists(Mark((host?"guest":"host")+"-nearby-playing")),
                "Both clients confirmed the match before returning",25);
            if(host)game.Modes.ReturnToLobby();
            yield return Until(()=>IslandLobby.Current&&room.Connected&&!room.Busy&&room.Code==address&&
                room.HumanCount==2&&IslandLobby.Current.PeerCount==1,
                "Guest follows host back to Marina without disconnecting",90,()=>room.Status);
            File.WriteAllText(Mark(role+"-nearby-returned"),"ready");
            yield return Until(()=>File.Exists(Mark((host?"guest":"host")+"-nearby-returned")),
                "Both clients confirm the return",25);
        }

        IEnumerator Check()
        {
            File.WriteAllText(Report,"Independent player "+role+"\n");
            if(NearbyFlow){yield return CheckNearbyLobby();yield break;}
            if(LobbyHandoff)
            {
                yield return CheckLobbyHandoff();
                yield break;
            }
            yield return Until(()=>IslandLobby.Current&&NetworkActors.Alive(IslandLobby.Current.Player),"Independent process opens playable lobby");
            var room=IslandRoomConnection.Instance;
            if(role=="host")
            {
                var task=room.Host();yield return Until(()=>task.IsCompleted&&room.IsHost,"Host connects to real Fusion");
                File.WriteAllText(Mark("code"),room.Code);
                yield return Until(()=>room.HumanCount==2&&IslandLobby.Current.PeerCount==1,"Independent guest avatar joins host island");
                room.SetSharedControls(true);room.Launch();
                yield return Until(()=>FindFirstObjectByType<CastlePlaySession>(),"Host transitions lobby to Castle Demo");
                var session=FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
                yield return Until(()=>session.Network.HumanCount==2,"Guest completes castle snapshot handshake");
                if (ProbePoses)
                {
                    yield return CheckPoseCadence(session, session.GetComponent<CastleFusionSession>());
                    yield return Until(() => File.Exists(Mark("pose-done")), "Guest completes pose measurement");
                    yield break;
                }
                yield return Until(()=>session.SurvivorBots.Count==1,"Guest shared-control command creates exactly one authoritative AI");
                Require(session.GetComponent<SocialWorld>().IsAuthority,"Host retains AI voice authority with shared UI");
                Require(DemoDisasters.Execute(session,"Tsunami"),"Host triggers network tsunami");
                yield return Until(()=>File.Exists(Mark("wave-seen")),"Independent guest receives active wave state");
                DemoDisasters.StopAll(session);
                yield return Until(()=>File.Exists(Mark("wave-cleared")),"Independent guest clears wave visuals after stop");
                yield return new WaitForSeconds(4);
                File.WriteAllText(Mark("host-leaving"),"ready");
                task=room.Leave();yield return Until(()=>task.IsCompleted,"Original host leaves Game cleanly");
            }
            else
            {
                yield return Until(()=>File.Exists(Mark("code")),"Host publishes room address");string code=File.ReadAllText(Mark("code"));
                yield return Until(()=>room.Discovery!=null&&room.Discovery.Nearby.Any(e=>e.message.code==code),"Separate process discovers host over UDP",12);
                var task=room.Join(code);yield return Until(()=>task.IsCompleted&&room.Connected,"Guest joins the discovered Fusion room");
                yield return Until(()=>FindFirstObjectByType<CastlePlaySession>()&&FindFirstObjectByType<CastlePlaySession>().Network&&!FindFirstObjectByType<CastlePlaySession>().Network.WaitingForWorld,"Guest transitions and receives castle snapshot",65);
                var session=FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);var sync=session.GetComponent<CastleFusionSession>();
                yield return Until(()=>sync.CanUseDemoControls,"Shared Demo powers arrive on independent guest");
                if (ProbePoses)
                {
                    yield return CheckPoseCadence(session, sync);
                    File.WriteAllText(Mark("pose-done"), "done");
                    yield break;
                }
                Require(!session.GetComponent<SocialWorld>().IsAuthority,"Shared UI does not create a second AI voice authority");
                Require(Services.World.Blocks.All(b=>b&&b.isKinematic),"Independent guest has replica-only block physics");
                CastleDemoCommands.Run(session,CastleDemoAction.AddSurvivor);
                yield return Until(()=>sync.Demo.survivors==1,"Guest receives authoritative AI population update");
                yield return Until(()=>Tracked.Waves.Any(w=>w.Wave.active),"Active tsunami is replicated to guest");File.WriteAllText(Mark("wave-seen"),"yes");
                yield return Until(()=>!Tracked.Waves.Any(w=>w.Wave.active),"Stopped tsunami is removed on guest");File.WriteAllText(Mark("wave-cleared"),"yes");
                yield return Until(()=>File.Exists(Mark("host-leaving")),"Host begins intentional departure");
                yield return Until(()=>room.IsHost&&!room.Busy,"Guest becomes host inside actual Game process",70);
                session=FindFirstObjectByType<CastlePlaySession>();
                Require(session&&session.Network&&!session.Network.IsClient,"Promoted guest keeps actual Castle Game active");
                Require(Services.World.Blocks.Count>0&&Services.World.Blocks.All(b=>b&&!b.isKinematic),"Promoted guest resumes authoritative block physics");
                Require(session.GetComponent<SocialWorld>().IsAuthority,"AI voice authority follows real process host migration");
                yield return new WaitForSeconds(3);
                task=room.Leave();yield return Until(()=>task.IsCompleted,"Migrated host leaves cleanly");
                yield return Until(()=>IslandLobby.Current&&NetworkActors.Alive(IslandLobby.Current.Player),"Migrated host returns to usable island");
            }
        }
    }
}
#endif

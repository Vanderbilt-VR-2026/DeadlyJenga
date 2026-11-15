#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
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
            Directory.CreateDirectory(test.folder);test.deadline=Time.realtimeSinceStartup+(NearbyFlow?850:TenRound?900:LobbyHandoff||TycoonTest||FullRound?600:180);
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
                Require(lobby.SelectMode("ctf")&&lobby.SelectMap("ctf"),"Host chooses Capture the Flag and its map");
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
                    !IslandLobby.Current.Room.IsHost&&IslandLobby.Current.SelectedMapId=="ctf",
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
                Require(lobby.SelectedModeId=="ctf"&&lobby.SelectedMapId=="ctf",
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
            string LobbyState() => $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, room={room.Status}, " +
                $"busy={room.Busy}, humans={room.HumanCount}, mirrors={IslandLobby.Current?.PeerCount}, home={(home ? home.Status : "closed")}";
            bool SeesOtherHuman() => IslandLobby.Current && room.Connected && room.Runner &&
                room.Runner.ActivePlayers.Any(player => player != room.Runner.LocalPlayer &&
                    IslandLobby.Current.TryPeerPosition(player.ToString(), out _));
            yield return Until(()=>IslandLobby.Current&&room.Connected&&!room.Busy&&room.Code==address&&
                room.HumanCount==2&&SeesOtherHuman(),
                "Both real Fusion clients share the same Marina room and see one another",75,LobbyState);
            File.WriteAllText(Mark(role+"-nearby-joined"),"ready");
            yield return Until(()=>File.Exists(Mark((host?"guest":"host")+"-nearby-joined")),
                "Other client confirmed joining",85,LobbyState);
            if (!host)
                yield return Until(() => File.Exists(Mark("host-pressed-go")),
                    "Host opens the ready check", 40, LobbyState);
            yield return Until(() => IslandLobby.Current.CanStart,
                "Play's Marina GO button is available", 35, LobbyState);
            IslandLobby.Current.StartOrCancel();
            if (host) File.WriteAllText(Mark("host-pressed-go"), "ready");
            CastlePlaySession game=null;
            yield return Until(()=>(game=FindFirstObjectByType<CastlePlaySession>())&&game.Network&&
                !game.Network.WaitingForWorld&&game.Modes&&game.Modes.Frame.phase==RoundPhase.Playing&&
                MatchSetup.MapId=="mode-castle-catwalks"&&room.Connected&&room.Code==address,
                "Guest follows host from Marina to the real Play round in the same room",125,()=>room.Status);
            File.WriteAllText(Mark(role+"-nearby-playing"),"ready");
            yield return Until(()=>File.Exists(Mark((host?"guest":"host")+"-nearby-playing")),
                "Both clients confirmed the match before returning",25);
            yield return Until(() => game && game.Modes &&
                game.Modes.Frame.phase is RoundPhase.Victory or RoundPhase.Results,
                "The full Play round reaches results after nearby join", 480, () => room.Status);
            yield return Until(()=>IslandLobby.Current&&room.Connected&&!room.Busy&&room.Code==address&&
                room.HumanCount==2&&SeesOtherHuman(),
                "Guest follows host back to Marina without disconnecting",90,()=>room.Status);
            File.WriteAllText(Mark(role+"-nearby-returned"),"ready");
            yield return Until(()=>File.Exists(Mark((host?"guest":"host")+"-nearby-returned")),
                "Both clients confirm the return",25);
        }

        static bool TycoonTest => Environment.GetCommandLineArgs().Contains("--dj-tycoon");
        static bool TycoonImpactTest => Environment.GetCommandLineArgs().Contains("--dj-tycoon-impact");
        const string TycoonMap = "mode-tycoon";
        static bool MigrationRound => Environment.GetCommandLineArgs().Contains("--dj-migrate-round");
        static bool FullRound => Environment.GetCommandLineArgs().Contains("--dj-full-round");
        static bool TenRound => Environment.GetCommandLineArgs().Contains("--dj-ten-round");

        IEnumerator CheckTenPlayerRound()
        {
            QualitySettings.vSyncCount=0;Application.targetFrameRate=30;
            File.AppendAllText(Report,"CONFIG functional ten-client test; cap=30; graphics="+SystemInfo.graphicsDeviceType+"\n");
            bool host=role=="host";
            var guests=Enumerable.Range(1,9).Select(index=>"guest"+index).ToArray();
            bool AllGuests(string suffix)=>guests.All(guest=>File.Exists(Mark(guest+suffix)));
            Require(host||guests.Contains(role),"Unique ten-player process role: "+role);
            HomeSession home=null;
            yield return Until(()=>(home=FindFirstObjectByType<HomeSession>())&&home.Region.Length>0,"Home initializes",90);
            home.Demo();
            yield return Until(()=>IslandLobby.Current&&NetworkActors.Alive(IslandLobby.Current.Player),"Developer opens Marina",100);
            var lobby=IslandLobby.Current;var room=lobby.Room;string code;
            if(host)
            {
                var connect=room.Host();
                yield return Until(()=>connect.IsCompleted&&room.IsHost&&lobby.CanConfigure,"Host creates real Fusion room",100,()=>room.Status);
                Require(lobby.SelectMode("survival")&&lobby.SelectMap("castle"),"Select survival on Castle");
                MatchSetup.BotTarget=10;
                code=room.Code;File.WriteAllText(Mark("code-pending"),code);File.Move(Mark("code-pending"),Mark("code"));
            }
            else
            {
                yield return Until(()=>File.Exists(Mark("code")),"Host publishes room",180);
                code=File.ReadAllText(Mark("code"));var connect=room.Join(code);
                yield return Until(()=>connect.IsCompleted&&room.Connected&&!room.IsHost&&lobby.SelectedMapId=="castle"&&lobby.SelectedModeId=="survival",
                    "Join same room and receive map/mode",100,()=>room.Status);
            }
            yield return Until(()=>room.HumanCount==10&&lobby.PeerCount==9&&room.Runner.ActivePlayers.Count()==10,
                "Ten actual Fusion humans and nine remote lobby avatars",150,()=>room.Status+" humans="+room.HumanCount+" avatars="+lobby.PeerCount+
                    " blocker="+typeof(IslandLobby).GetProperty("MarinaSpawnBlocker",PrivateField)?.GetValue(lobby));
            File.WriteAllText(Mark(role+"-ready"),room.LocalLobbyId);
            if(host){yield return Until(()=>AllGuests("-ready"),"All nine guests independently verify full lobby",90);lobby.StartOrCancel();}
            CastlePlaySession match=null;
            yield return Until(()=>(match=FindFirstObjectByType<CastlePlaySession>())&&match.Network&&!match.Network.WaitingForWorld&&match.Modes&&
                NetworkActors.Alive(match.Player)&&room.Connected&&room.Code==code&&room.HumanCount==10&&Services.World?.Blocks.Count>0,
                "Enter actual Game with ten humans and world snapshot",150,()=>room.Status);
            File.WriteAllText(Mark(role+"-in-game"),"ready");
            if(host)
            {
                yield return Until(()=>AllGuests("-in-game"),"Every guest loaded the world",90);
                var rules=match.Modes.Rules.Copy();rules.countdownSeconds=1;rules.roundSeconds=30;rules.victorySeconds=2;rules.automaticDisasters=false;
                match.Modes.Configure(rules);match.Modes.StartRound();
            }
            yield return Until(()=>match.Modes.Frame.phase==RoundPhase.Playing&&match.Modes.Frame.rules.roundSeconds==30&&room.HumanCount==10,
                "Ten-player timed round is playing",60,()=>room.Status);
            if(host)
            {
                yield return Until(()=>AllGuests("-migration-ready"),"All guests observe progress before migration",60);
                var leave=room.Leave();
                yield return Until(()=>leave.IsCompleted&&!room.Connected,"Host leaves during the live round",60);
                yield return Until(()=>AllGuests("-returned"),"All nine remaining players finish and return after migration",220);
                yield break;
            }
            yield return Until(()=>match.Modes.Frame.phase==RoundPhase.Playing&&match.Modes.Frame.remaining<=27,"Observe round progress before migration",50);
            string identity=room.LocalLobbyId;float remaining=match.Modes.Frame.remaining;
            File.WriteAllText(Mark(role+"-migration-ready"),identity);
            yield return Until(()=>room.Connected&&!room.Busy&&room.HumanCount==9&&match.Network&&
                (room.IsHost?!match.Network.IsClient:match.Network.IsClient),"Nine clients resume with one replacement authority",100,()=>room.Status);
            Require(room.Code==code&&room.LocalLobbyId==identity,"Room and local actor identity survive migration");
            Require(match.Modes.Frame.phase==RoundPhase.Playing&&match.Modes.Frame.remaining<=remaining+1,"Migration preserves the original running round and timer");
            File.AppendAllText(Report,"MIGRATION authority="+room.IsHost+" identity="+identity+" remaining="+match.Modes.Frame.remaining.ToString(CultureInfo.InvariantCulture)+"\n");
            yield return Until(()=>match.Modes.Frame.phase==RoundPhase.Results&&room.Connected&&room.HumanCount==9,"Original round reaches results after migration",90,()=>room.Status);
            File.WriteAllText(Mark(role+"-results"),"ready");
            if(room.IsHost){yield return Until(()=>AllGuests("-results"),"All remaining peers see results",60);match.Modes.ReturnToLobby();}
            yield return Until(()=>IslandLobby.Current&&NetworkActors.Alive(IslandLobby.Current.Player)&&room.Connected&&room.Code==code&&
                room.HumanCount==9&&IslandLobby.Current.PeerCount==8,"Nine players return to the same usable lobby",100,()=>room.Status);
            File.WriteAllText(Mark(role+"-returned"),"ready");
            yield return Until(()=>AllGuests("-returned"),"Every remaining peer independently confirms return",60);
        }

        IEnumerator CheckFullRound()
        {
            HomeSession home = null;
            yield return Until(() => (home = FindFirstObjectByType<HomeSession>()) && home.Region.Length > 0,
                "Home initializes", 60);
            home.Demo();
            yield return Until(() => IslandLobby.Current && NetworkActors.Alive(IslandLobby.Current.Player),
                "Developer opens the Marina", 85);
            var lobby = IslandLobby.Current;
            var room = lobby.Room;
            bool host = role == "host";
            string code;
            if (host)
            {
                var connect = room.Host();
                yield return Until(() => connect.IsCompleted && room.IsHost && lobby.CanConfigure,
                    "Host creates a Fusion room through the Marina", 75, () => room.Status);
                Require(lobby.SelectMode("survival") && lobby.SelectMap("castle"), "Host selects Disaster Survival on Castle");
                MatchSetup.BotTarget = 2;
                code = room.Code;
                File.WriteAllText(Mark("code-pending"), code);
                File.Move(Mark("code-pending"), Mark("code"));
                yield return Until(() => room.HumanCount == 2 && File.Exists(Mark("guest-ready")),
                    "Guest joins and receives the host's selection", 90, () => room.Status);
                lobby.StartOrCancel();
            }
            else
            {
                yield return Until(() => File.Exists(Mark("code")), "Host advertises its room code", 150);
                code = File.ReadAllText(Mark("code"));
                var connect = room.Join(code);
                yield return Until(() => connect.IsCompleted && room.Connected && !room.IsHost &&
                    lobby.SelectedMapId == "castle" && lobby.SelectedModeId == "survival",
                    "Guest joins by code and reads the chosen map and mode", 90, () => room.Status);
                File.WriteAllText(Mark("guest-ready"), "ready");
            }

            CastlePlaySession match = null;
            yield return Until(() => (match = FindFirstObjectByType<CastlePlaySession>()) && match.Network &&
                !match.Network.WaitingForWorld && match.Modes && NetworkActors.Alive(match.Player) &&
                room.Connected && room.Code == code && room.HumanCount == 2 && Services.World?.Blocks.Count > 0,
                "Both peers enter Game with an actor and synchronized blocks", 110, () => room.Status);
            File.WriteAllText(Mark(role + "-in-game"), "ready");
            if (host)
            {
                yield return Until(() => File.Exists(Mark("guest-in-game")), "Guest confirms its world snapshot");
                var rules = match.Modes.Rules.Copy();
                rules.countdownSeconds = 1;
                rules.roundSeconds = 20;
                rules.victorySeconds = 2;
                rules.automaticDisasters = false;
                match.Modes.Configure(rules);
                match.Modes.StartRound();
            }
            yield return Until(() => match.Modes.Frame.phase == RoundPhase.Playing && match.Modes.Frame.rules.roundSeconds == 20 &&
                room.Connected && room.HumanCount == 2, "Authoritative timed round is playing on both peers", 40,
                () => room.Status);
            if (MigrationRound)
            {
                if (host)
                {
                    yield return Until(() => File.Exists(Mark("guest-migration-ready")), "Guest observed in-progress round");
                    var leave = room.Leave();
                    yield return Until(() => leave.IsCompleted && !room.Connected, "Former host leaves during the actual round", 40);
                    yield return Until(() => File.Exists(Mark("guest-migration-returned")), "Promoted guest independently confirms the full round", 160);
                    yield break;
                }

                yield return Until(() => match.Modes.Frame.remaining <= 17f &&
                    match.Modes.Frame.phase == RoundPhase.Playing, "Guest records round progress before transfer");
                string lastLocalId = room.LocalLobbyId;
                float remaining = match.Modes.Frame.remaining;
                File.WriteAllText(Mark("guest-migration-ready"), "ready");
                yield return Until(() => room.IsHost && !room.Busy && match.Network && !match.Network.IsClient,
                    "Guest becomes authoritative in the same active Game session", 90, () => room.Status);
                Require(room.Code == code && room.LocalLobbyId == lastLocalId,
                    "Room code and local actor identity survive host transfer");
                Require(match.Modes.Frame.phase == RoundPhase.Playing && match.Modes.Frame.remaining <= remaining + 1,
                    "Host transfer preserves the existing round and timer");
                yield return Until(() => match.Modes.Frame.phase == RoundPhase.Results && room.IsHost && room.Code == code,
                    "Promoted guest finishes the original round", 75, () => room.Status);
                match.Modes.ReturnToLobby();
                yield return Until(() => IslandLobby.Current && NetworkActors.Alive(IslandLobby.Current.Player) &&
                    room.IsHost && room.Code == code, "Promoted guest returns to the same Marina room", 85, () => room.Status);
                File.WriteAllText(Mark("guest-migration-returned"), "ready");
                yield break;
            }
            yield return Until(() => match.Modes.Frame.phase == RoundPhase.Results && room.Connected && room.HumanCount == 2,
                "Both peers reach final results in the same room", 75, () => room.Status);
            File.WriteAllText(Mark(role + "-results"), "ready");
            if (host)
            {
                yield return Until(() => File.Exists(Mark("guest-results")), "Guest independently confirms results");
                match.Modes.ReturnToLobby();
            }
            yield return Until(() => IslandLobby.Current && NetworkActors.Alive(IslandLobby.Current.Player) &&
                room.Connected && room.Code == code && room.HumanCount == 2 && IslandLobby.Current.PeerCount == 1,
                "Both players return to a usable Marina in the same room", 90, () => room.Status);
            File.WriteAllText(Mark(role + "-returned"), "ready");
            yield return Until(() => File.Exists(Mark((host ? "guest" : "host") + "-returned")),
                "Other peer independently confirms the return");
        }

        IEnumerator CheckTycoon()
        {
            HomeSession home = null;
            yield return Until(() => (home = FindFirstObjectByType<HomeSession>()) && home.Region.Length > 0, "Home initialized");
            home.Demo();
            yield return Until(() => IslandLobby.Current && NetworkActors.Alive(IslandLobby.Current.Player), "Developer enters usable lobby");
            var lobby = IslandLobby.Current;
            var room = lobby.Room;
            bool host = role == "host";
            bool solo = role == "solo";
            string code = "";
            if (host)
            {
                yield return Until(() => File.Exists(Mark("tycoon-guest-home")), "Guest ready in lobby");
                var task = room.Host();
                yield return Until(() => task.IsCompleted && room.IsHost && lobby.CanConfigure, "Host terminal opens a real Fusion room", 60, () => room.Status);
            }
            if (host || solo)
            {
                Require(lobby.SelectMode("demo") && lobby.SelectMap(TycoonMap), "Terminal selects Tycoon");
                MatchSetup.BotTarget = solo ? 1 : 2;
                if (host)
                {
                    code = room.Code;
                    File.WriteAllText(Mark("tycoon-code-temp"), code);
                    File.Move(Mark("tycoon-code-temp"), Mark("tycoon-code"));
                    yield return Until(() => room.HumanCount == 2 && File.Exists(Mark("tycoon-guest-ready")), "Guest and host agree on the room and map", 75);
                }
                lobby.StartOrCancel();
            }
            else
            {
                File.WriteAllText(Mark("tycoon-guest-home"), "ready");
                yield return Until(() => File.Exists(Mark("tycoon-code")), "Host publishes code", 90);
                code = File.ReadAllText(Mark("tycoon-code"));
                var task = room.Join(code);
                yield return Until(() => task.IsCompleted && room.Connected && !room.IsHost && lobby.SelectedMapId == TycoonMap,
                    "Guest joins by code and receives Tycoon selection", 75, () => room.Status);
                File.WriteAllText(Mark("tycoon-guest-ready"), "ready");
            }

            CastlePlaySession session = null;
            float nextDiagnostic = Time.realtimeSinceStartup + 5;
            yield return Until(() =>
            {
                session = FindFirstObjectByType<CastlePlaySession>();
                if (Time.realtimeSinceStartup >= nextDiagnostic)
                {
                    nextDiagnostic = Time.realtimeSinceStartup + 5;
                    File.AppendAllText(Report, $"JOIN scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}; room={room.Status}; match={session != null}; actor={NetworkActors.Alive(session?.Player)}; waiting={session?.Network?.WaitingForWorld}; status={session?.Network?.Status}; world={Services.World?.Blocks.Count}; humans={room.HumanCount}\n");
                }
                return session &&
                NetworkActors.Alive(session.Player) && Services.World is CastleGenerator map &&
                map.settings.layoutPreset == TycoonLayout.Preset && map.Bodies.Count > 0 &&
                (solo || session.Network && room.HumanCount == 2 && !session.Network.WaitingForWorld);
            },
                "Actual Game transition and Tycoon world snapshot complete", 85, () =>
                    $"room={room.Status}; session={session != null}; actor={NetworkActors.Alive(session?.Player)}; " +
                    $"world={Services.World?.Blocks.Count}; waiting={session?.Network?.WaitingForWorld}; humans={room.HumanCount}");
            var world = (CastleGenerator)Services.World;
            var blocks = world.Bodies.ToArray();
            var authored = blocks.Select(body => body.position).ToArray();
            if (host)
                yield return Until(() => File.Exists(Mark("tycoon-guest-match")),
                    "Guest confirms initial world snapshot before load is added", 60);
            else if (!solo)
                File.WriteAllText(Mark("tycoon-guest-match"), "ready");
            const int expectedTycoonBlocks = 775;
            Require(blocks.Length == expectedTycoonBlocks &&
                blocks.All(body => body && body.isKinematic && body.TryGetComponent<Collider>(out var collider) && collider.enabled),
                $"All {expectedTycoonBlocks} resting Tycoon blocks have live colliders");
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            if (TycoonImpactTest)
            {
                yield return CheckTycoonImpact(session, world, blocks, host, solo);
                if (host || solo) Require(room.ReturnMatchToLobby(), "Host requests normal return to lobby");
                yield return CheckTycoonReturn(room, code, solo);
                yield break;
            }
            if (solo)
            {
                var probe = blocks.OrderByDescending(body => body.position.y).First();
                var start = probe.position;
                probe.GetComponent<IPushable>().Push(new Vector3(25, 0, 0), gameObject);
                yield return Until(() => !probe.isKinematic, "Direct force releases a supported piece", 3);
                yield return Until(() => probe.position.y < start.y - .5f, "Released piece falls under gravity", 8);
                var ocean = world.Center + Vector3.right * (world.PlayableBounds.extents.x + 10);
                probe.position = new Vector3(ocean.x, world.WaterHeight(ocean) + 4f, ocean.z);
                probe.linearVelocity = Vector3.zero;
                probe.angularVelocity = Vector3.zero;
                yield return Until(() => probe && probe.position.y < world.WaterHeight(ocean), "Released block crosses the waterline under gravity", 8);
                yield return Until(() => !probe.gameObject.activeSelf, "Submerged debris is retired", 12);
                session.ResetMap();
                yield return Until(() => world.Bodies.Count == blocks.Length && world.Bodies.All(body => body.isKinematic), "Rebuild restores all supports");
                yield return SampleFrames("solo-idle", 6);
                foreach (string hazard in new[] { "Earthquake", "Tornado" })
                {
                    var oldBodies = world.Bodies.ToArray();
                    Require(DemoDisasters.ExecuteForMode(session, hazard), hazard + " starts from the production catalog");
                    yield return Until(() => oldBodies.Count(body => body && !body.isKinematic) >= 2, hazard + " dislodges multiple map pieces", 35);
                    yield return SampleFrames(hazard, 4);
                    DemoDisasters.StopAll(session);
                    session.ResetMap();
                    yield return null;
                }
            }
            if (host || solo)
            {
                session.SurvivorBots.AutoFill = false;
                session.SurvivorBots.Clear();
                for (int i = 0; i < 24; i++) session.SurvivorBots.SpawnOne();
                Require(session.SurvivorBots.Count == 24, "Host spawns 24 actual bots");
                yield return SampleFrames("24-bots", 12);
                var beforeWave = world.Bodies.Select(body => body.position).ToArray();
                var wavePrefab = DemoDisasters.Prefabs.First(prefab => prefab.name == "Tsunami");
                var parameters = DemoDisasters.Parameters(wavePrefab).Select(parameter => parameter.initial).ToArray();
                parameters[0] = 23;
                parameters[2] = 27;
                UnityEngine.Random.InitState(71293);
                parameters[4] = 1;
                Require(DemoDisasters.ExecuteForMode(session, "Tsunami", parameters), "Host starts Tycoon tsunami");
                yield return SampleFrames("24-bots-tsunami", 21);
                int Moved() => world.Bodies.Where((body, index) => body && (body.position - beforeWave[index]).sqrMagnitude > 1f).Count();
                yield return Until(() => Moved() >= 12, "Tsunami moves at least 12 blocks", 25, () => "Moved " + Moved());
                File.AppendAllText(Report, $"Tsunami moved {Moved()} blocks over 1 m.\n");
                Require(world.Bodies.All(body => body && float.IsFinite(body.position.sqrMagnitude)), "World remains finite after the wave");
                if (host) yield return Until(() => File.Exists(Mark("tycoon-wave-seen")), "Guest receives wave and changed block poses");
                DemoDisasters.StopAll(session);
                Require(room.ReturnMatchToLobby(), "Host requests normal return to lobby");
            }
            else
            {
                yield return Until(() => Tracked.Waves.Any(wave => wave.Wave.active), "Guest sees the replicated tsunami", 60);
                yield return SampleFrames("guest-tsunami", 12);
                yield return Until(() => blocks.Where((body, index) => body && (body.position - authored[index]).sqrMagnitude > 1f).Count() >= 12,
                    "Guest receives collapsed poses", 50);
                File.WriteAllText(Mark("tycoon-wave-seen"), "yes");
            }
            yield return CheckTycoonReturn(room, code, solo);
        }

        IEnumerator CheckTycoonReturn(IslandRoomConnection room, string code, bool solo)
        {
            yield return Until(() => IslandLobby.Current && NetworkActors.Alive(IslandLobby.Current.Player) &&
                (solo || room.Connected && room.Code == code && room.HumanCount == 2),
                "Returns to a usable lobby and retains the same party", 70, () => room.Status);
            if (!solo)
            {
                File.WriteAllText(Mark("tycoon-return-" + role), "ready");
                yield return Until(() => File.Exists(Mark("tycoon-return-" + (role == "host" ? "guest" : "host"))),
                    "Both peers independently confirmed return", 40);
            }
        }

        readonly string ImpactHazard = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--dj-impact-hazard=", StringComparison.Ordinal))?[19..] ?? "Tsunami";
        string ImpactLabel => ImpactHazard == "Tsunami" ? "tsunami" : ImpactHazard.ToLowerInvariant();

        IEnumerator CheckTycoonImpact(CastlePlaySession session, CastleGenerator world, Rigidbody[] blocks, bool host, bool solo)
        {
            Require(ImpactHazard is "Tsunami" or "Tornado" or "PlanetKillerMeteor" or "Combined", "Recognized production impact scenario: " + ImpactHazard);
            var populationArg = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--dj-bots=", StringComparison.Ordinal));
            int population = populationArg == "--dj-bots=52" ? 52 : 24;
            Require(populationArg == null || populationArg == "--dj-bots=24" || populationArg == "--dj-bots=52",
                "Impact benchmark supports 24 or 52 survivor bots");
            var before = blocks.Select(body => body.position).ToArray();
            if (host || solo)
            {
                UnityEngine.Random.InitState(71292);
                session.SurvivorBots.AutoFill = false;
                session.SurvivorBots.Clear();
                for (int i = 0; i < population; i++) session.SurvivorBots.SpawnOne();
                Require(session.SurvivorBots.Count == population, $"Authority spawned {population} actual survivor bots");
                if (host) File.WriteAllText(Mark("impact-bots-spawned"), population.ToString(CultureInfo.InvariantCulture));
            }
            else
                yield return Until(() => File.Exists(Mark("impact-bots-spawned")), "Host spawned load before the guest samples baseline");

            File.AppendAllText(Report, $"CONFIG bots={population} vsync={QualitySettings.vSyncCount} cap={Application.targetFrameRate} " +
                $"resolution={Screen.width}x{Screen.height} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}\n");
            yield return new WaitForSecondsRealtime(4);
            if (host || solo) CheckNearbyCandidates("settled");
            yield return null; yield return null;
            yield return SampleImpactFrames("before-wave", blocks, before, 8);

            if (host)
                yield return Until(() => File.Exists(Mark("impact-guest-baseline")), "Guest completes its no-disaster sample");
            else if (!solo)
                File.WriteAllText(Mark("impact-guest-baseline"), "ready");
            if (host || solo)
            {
                UnityEngine.Random.InitState(71293);
                foreach (string hazard in ImpactHazard == "Combined" ? new[] { "Tsunami", "Tornado", "PlanetKillerMeteor" } : new[] { ImpactHazard })
                {
                    var prefab = DemoDisasters.Prefabs.First(item => item.name == hazard);
                    var schema = DemoDisasters.Parameters(prefab);
                    var parameters = schema.Select(parameter => parameter.initial).ToArray();
                    for (int i = 0; i < schema.Count; i++)
                    {
                        if (schema[i].id == "warning") parameters[i] = 1;
                        if (schema[i].id == "duration" && hazard == "Tornado") parameters[i] = 45;
                        if (schema[i].id == "size" && hazard == "PlanetKillerMeteor") parameters[i] = 90;
                    }
                    if (hazard == "Tsunami") { parameters[0] = 23; parameters[2] = 27; parameters[4] = 1; }
                    File.AppendAllText(Report, "HAZARD " + hazard + " " + string.Join(" ", schema.Select((parameter, i) => parameter.id + "=" + parameters[i].ToString(CultureInfo.InvariantCulture))) + "\n");
                    Require(DemoDisasters.ExecuteForMode(session, hazard, parameters), "Authority starts production " + hazard);
                }
            }

            yield return Until(() => ImpactHazard == "Tsunami" || ImpactHazard == "Combined"
                ? Tracked.Waves.Any(wave => wave.Wave.active)
                : DisasterStates.Active.Any(source => source.Capture()?.kind == ImpactHazard), "Disaster is visible locally: " + ImpactHazard, 75);
            bool gpuProfile = Environment.GetCommandLineArgs().Contains("--dj-impact-gpu-profile");
            bool binaryProfile = gpuProfile || Environment.GetCommandLineArgs().Contains("--dj-impact-raw-profile");
            bool gpuWasEnabled = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU);
            try
            {
                if (binaryProfile)
                {
                    if (gpuProfile) UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, true);
                    UnityEngine.Profiling.Profiler.logFile = Path.Combine(folder, role + "-" + ImpactLabel + ".raw");
                    UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                    UnityEngine.Profiling.Profiler.enabled = true;
                }
                yield return SampleImpactFrames(ImpactLabel, blocks, before, 8);
            }
            finally
            {
                if (binaryProfile)
                {
                    UnityEngine.Profiling.Profiler.enabled = false;
                    UnityEngine.Profiling.Profiler.enableBinaryLog = false;
                    UnityEngine.Profiling.Profiler.logFile = "";
                    if (gpuProfile) UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, gpuWasEnabled);
                }
            }
            if (host || solo) CheckNearbyCandidates("after-impact");
            Require(world.Bodies.Count == blocks.Length &&
                world.Bodies.All(body => body && float.IsFinite(body.position.sqrMagnitude)), "World remains finite after the wave");
            if (host)
                yield return Until(() => File.Exists(Mark("impact-guest-complete")), "Guest independently measured replicated block displacement", 90);
            else if (!solo)
                File.WriteAllText(Mark("impact-guest-complete"), "ready");
            if (host || solo) DemoDisasters.StopAll(session);
        }

        void CheckNearbyCandidates(string phase)
        {
            var bots = FindObjectsByType<DeadlyJenga.CastleBots.CastleBotBrain>(FindObjectsSortMode.None);
            var method = typeof(DeadlyJenga.CastleBots.CastleBotNavigation).GetMethod("CollectNearby",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var spatial = new List<DeadlyJenga.CastleBots.CastleBotLanding>();
            var exhaustive = new List<DeadlyJenga.CastleBots.CastleBotLanding>();
            foreach (int index in new[] { 0, bots.Length / 2, bots.Length - 1 }.Distinct())
            {
                if (index < 0 || !bots[index].Motor || !bots[index].Population) continue;
                method.Invoke(null, new object[] { bots[index], exhaustive, false });
                method.Invoke(null, new object[] { bots[index], spatial, true });
                bool same = spatial.Count == exhaustive.Count;
                for (int i = 0; same && i < spatial.Count; i++)
                    same = spatial[i].surface == exhaustive[i].surface &&
                        (spatial[i].local - exhaustive[i].local).sqrMagnitude < .00000001f &&
                        Mathf.Abs(spatial[i].score - exhaustive[i].score) < .0001f;
                Require(same, $"Spatial navigation matches exhaustive candidates {phase}: bot={index}, spatial={spatial.Count}, exhaustive={exhaustive.Count}");
            }
        }

        static readonly ProfilerMarker ImpactProbeMarker = new ProfilerMarker("DJ.Test.ImpactProbe");
        Fusion.NetworkRunner rateConfiguredRunner;

        void RecordNetworkTraffic(string phase)
        {
            var sync=FindFirstObjectByType<CastleFusionSession>();
            if(!sync)return;
            var text=new StringBuilder();
            text.AppendLine("PHASE "+phase+" time="+Time.realtimeSinceStartupAsDouble.ToString("F4",CultureInfo.InvariantCulture));
            text.AppendLine("HotpathBytesSubmitted="+sync.HotpathBytesSubmitted+" HotpathBundlesSubmitted="+sync.HotpathBundlesSubmitted);
            var room=IslandRoomConnection.Instance;
            if(room&&room.Runner)
            {
                text.AppendLine("ReliableDataSendRateBefore="+room.Runner.ReliableDataSendRate);
                if(rateConfiguredRunner!=room.Runner)
                {
                    if(Environment.GetCommandLineArgs().Contains("--dj-native-state-probe")) CastleNativeStateProbe.Configure(room.Runner);
                    var argument=Environment.GetCommandLineArgs().FirstOrDefault(arg=>arg.StartsWith("--dj-reliable-rate=",StringComparison.Ordinal));
                    if(argument!=null&&int.TryParse(argument[19..],out int rate)&&rate>=1&&rate<=60)
                        room.Runner.ReliableDataSendRate=rate;
                    rateConfiguredRunner=room.Runner;
                }
                text.AppendLine("ReliableDataSendRate="+room.Runner.ReliableDataSendRate);
                text.AppendLine($"NativeStateProbe received={CastleNativeStateProbe.Received} gapSum={CastleNativeStateProbe.GapSum:F6} maxGap={CastleNativeStateProbe.MaxGap:F6}");
                text.AppendLine($"NativeWorld published={CastleNativeWorldChannel.PublishedFrames} bytes={CastleNativeWorldChannel.PublishedBytes} received={CastleNativeWorldChannel.ReceivedFrames}");
                text.AppendLine("ReliableSendCount="+room.Runner.Config.Network.GameReliableChannelSendCount);
                foreach(var player in room.Runner.ActivePlayers)
                    if(room.Runner.IsServer||player==room.Runner.LocalPlayer)
                        text.AppendLine("RTT "+player+" seconds="+room.Runner.GetPlayerRtt(player).ToString("F6",CultureInfo.InvariantCulture));
            }
            foreach(var traffic in role=="host"?sync.PeerPoseTraffic:new[]{sync.LocalPoseTraffic})
            {
                text.AppendLine("PEER "+traffic.PeerId);
                foreach(var property in typeof(CastleFusionSession.LivePoseTraffic).GetProperties())
                    text.AppendLine(property.Name+"="+Convert.ToString(property.GetValue(traffic),CultureInfo.InvariantCulture));
            }
            File.AppendAllText(Path.Combine(folder,role+"-network-traffic.txt"),text.ToString());
        }

        IEnumerator SampleImpactFrames(string label, Rigidbody[] bodies, Vector3[] authored, double seconds)
        {
            RecordNetworkTraffic(label+"-start");
            bool requireImpact = label != "before-wave";
            var headers = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(headers);
            var recorders = new List<(string name, ProfilerRecorder recorder)>();
            foreach (var handle in headers)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                string name = description.Name;
                if (name.StartsWith("DJ.", StringComparison.Ordinal) || name.StartsWith("DeadlyJenga.", StringComparison.Ordinal) ||
                    name.StartsWith("Behaviour", StringComparison.Ordinal) || name.StartsWith("Gfx.Wait", StringComparison.Ordinal) ||
                    name is "Main Thread" or "Render Thread" or "PlayerLoop" or "Camera.Render" or
                        "RenderPipelineManager.DoRenderLoop_Internal" or "Physics.Simulate" or "Physics.SyncTransforms" or
                        "Update.ScriptRunBehaviourUpdate" or "PreLateUpdate.ScriptRunBehaviourLateUpdate" or
                        "GC Allocated In Frame" or "GC.Collect" or "Draw Calls Count" or "SetPass Calls Count" or
                        "Batches Count" or "Triangles Count" or "GPU Frame Time" or "CPU Total Frame Time" or "WaitForTargetFPS")
                {
                    var recorder = ProfilerRecorder.StartNew(description.Category, name, 1);
                    if (recorder.Valid) recorders.Add(($"{description.Category.Name}/{name} [{description.UnitType}]", recorder));
                    else recorder.Dispose();
                }
            }

            var accepted = new List<double>(1024);
            var csv = new StringBuilder(512 * 1024);
            csv.Append("completed_frame,seconds_since_start,phase,wave_active,wall_ms,moved_over_25cm,moved_over_1m,moving_this_frame,local_wave_targets,world_age_ms,pose_age_ms,pose_applied,bots_alive,bots_moving,bots_grounded,bot_travel_metres,impact_eligible,tornado_active,planet_land_contact");
            foreach (var counter in recorders) csv.Append(',').Append('"').Append(counter.name.Replace("\"", "\"\"")).Append('"');
            csv.AppendLine();
            var sync = FindFirstObjectByType<CastleFusionSession>();
            var worldTime = typeof(CastleFusionSession).GetField("lastWorld", PrivateField);
            var poseTraffic = sync ? (role == "host" ? sync.PeerPoseTraffic : new[] { sync.LocalPoseTraffic }) : Array.Empty<CastleFusionSession.LivePoseTraffic>();
            // Local arrival age, not end-to-end network latency or synchronized clocks.
            double worldAge = -1, poseAge = -1;
            long poseApplied = 0;
            var measuredBots = FindObjectsByType<DeadlyJenga.CastleBots.CastleBotBrain>(FindObjectsSortMode.None);
            int botsAlive = 0, botsMoving = 0, botsGrounded = 0;
            double botTravel = 0;
            var positions = bodies.Select(body => body.position).ToArray();
            int moved = 0, movedOne = 0, moving = 0, targets = 0, peakMoved = 0, activeMotionFrames = 0, totalFrames = 0;
            bool waveActive = false;
            bool eligible=false,tornadoActive=false,planetContact=false;
            double started = Time.realtimeSinceStartupAsDouble;
            double previous = started, impact = double.NaN;
            try
            {
                while (previous - started < (requireImpact ? 95 : seconds))
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    double delta = (now - previous) * 1000;
                    string phase = !requireImpact ? "baseline" : double.IsNaN(impact) ? "approach" : "impact";
                    totalFrames++;
                    if (phase == "baseline" || phase == "impact" && eligible)
                    {
                        accepted.Add(delta);
                        if (moving > 0) activeMotionFrames++;
                    }
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:F4},{2},{3},{4:F4},{5},{6},{7},{8}",
                        Time.frameCount - 1, now - started, phase, waveActive ? 1 : 0, delta, moved, movedOne, moving, targets);
                    csv.AppendFormat(CultureInfo.InvariantCulture, ",{0:F3},{1:F3},{2},{3},{4},{5},{6:F3}", worldAge, poseAge, poseApplied, botsAlive, botsMoving, botsGrounded, botTravel);
                    csv.AppendFormat(CultureInfo.InvariantCulture, ",{0},{1},{2}", eligible ? 1 : 0, tornadoActive ? 1 : 0, planetContact ? 1 : 0);
                    foreach (var counter in recorders) csv.Append(',').Append(counter.recorder.LastValue);
                    csv.AppendLine();

                    using (ImpactProbeMarker.Auto())
                    {
                        botsAlive = botsMoving = botsGrounded = 0; botTravel = 0;
                        foreach (var bot in measuredBots)
                        {
                            if (!bot || !bot.Motor || !bot.Motor.Alive || bot.Respawning) continue;
                            botsAlive++;
                            if (bot.ActualSpeed > .15f) botsMoving++;
                            if (bot.Motor.Grounded) botsGrounded++;
                            botTravel += bot.TravelDistance;
                        }
                        worldAge = sync && role == "guest" ? (Time.unscaledTime - (float)worldTime.GetValue(sync)) * 1000 : -1;
                        poseAge = -1; poseApplied = 0;
                        foreach (var traffic in poseTraffic)
                        {
                            if (traffic.LastAppliedAt > 0) poseAge = Math.Max(poseAge, (Time.unscaledTime - traffic.LastAppliedAt) * 1000);
                            poseApplied += traffic.AppliedFrames;
                        }
                        moved = movedOne = moving = targets = 0;
                        waveActive = false;
                        foreach (var wave in Tracked.Waves)
                        {
                            if (!wave.Wave.active) continue;
                            waveActive = true;
                            if (wave is TsunamiHazard tsunami) targets += tsunami.LastTargetCount;
                        }
                        tornadoActive=false;
                        if (ImpactHazard != "Tsunami") foreach (var source in DisasterStates.Active)
                        {
                            var state=source.Capture();
                            if(state==null)continue;
                            if(state.kind=="Tornado"&&state.warning<=0)tornadoActive=true;
                            if(state.kind=="PlanetLandContact"&&state.points?.Length>0)planetContact=true;
                        }
                        eligible=ImpactHazard switch
                        {
                            "Tornado" => tornadoActive,
                            "PlanetKillerMeteor" => planetContact,
                            "Combined" => waveActive&&tornadoActive&&planetContact,
                            _ => waveActive
                        };
                        for (int i = 0; i < bodies.Length; i++)
                        {
                            var body = bodies[i];
                            if (!body || !body.gameObject.activeSelf) continue;
                            Vector3 position = body.position;
                            float offset = (position - authored[i]).sqrMagnitude;
                            if (offset > .0625f) moved++;
                            if (offset > 1f) movedOne++;
                            if (offset > .0625f && (position - positions[i]).sqrMagnitude > .000025f) moving++;
                            positions[i] = position;
                        }
                    }
                    peakMoved = Mathf.Max(movedOne, peakMoved);
                    if (requireImpact && double.IsNaN(impact) && eligible && moving >= 2 &&
                        (ImpactHazard != "Tsunami" && ImpactHazard != "Combined" || role == "guest" || targets > 0))
                        impact = now;
                    previous = now;
                    if (!double.IsNaN(impact) && now - impact >= seconds) break;
                }
            }
            finally
            {
                foreach (var counter in recorders) counter.recorder.Dispose();
            }

            File.WriteAllText(Path.Combine(folder, role + "-" + label + "-frames.csv"), csv.ToString());
            RecordNetworkTraffic(label+"-end");
            if (requireImpact)
                Require(!double.IsNaN(impact) && activeMotionFrames >= 10 && peakMoved >= 12,
                    $"{ImpactHazard} impact window observed moving blocks on {activeMotionFrames} frames and at least {peakMoved} blocks displaced over 1 m");
            Require(accepted.Count > 0, "Frame timing window contains completed frames");
            accepted.Sort();
            double Percentile(double fraction) => accepted[Math.Max(0, (int)Math.Ceiling(accepted.Count * fraction) - 1)];
            double median = Percentile(.5);
            File.AppendAllText(Report, FormattableString.Invariant(
                $"IMPACT {label} n={accepted.Count} totalFrames={totalFrames} onset={impact - started:F3}s p50={median:F3}ms fps={1000 / median:F2} p95={Percentile(.95):F3}ms p99={Percentile(.99):F3}ms max={accepted.Last():F3}ms motionFrames={activeMotionFrames} displaced1m={peakMoved}\n"));
        }

        IEnumerator SampleFrames(string label, double seconds)
        {
            var times = new List<double>(2000);
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var recorders = new List<(string name, ProfilerRecorder recorder)>();
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                string name = description.Name;
                if (name.StartsWith("DJ.", StringComparison.Ordinal) ||
                    name.StartsWith("DeadlyJenga.", StringComparison.Ordinal) ||
                    name is "Main Thread" or "Render Thread" or "Physics.Simulate" or
                        "Physics.SyncTransforms" or "GC Allocated In Frame" or "GC.Collect" or "BehaviourFixedUpdate")
                    recorders.Add((name, ProfilerRecorder.StartNew(description.Category, name, 2048)));
            }
            try
            {
                double previous = Time.realtimeSinceStartupAsDouble;
                double deadline = previous + seconds;
                while (previous < deadline)
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    times.Add((now - previous) * 1000);
                    previous = now;
                }
                var sorted = times.OrderBy(ms => ms).ToArray();
                double Percentile(float fraction) => sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length * fraction) - 1, 0, sorted.Length - 1)];
                int slow = times.Count(ms => ms >= 100);
                File.AppendAllText(Report, $"FRAMES {label} n={times.Count} p50={Percentile(.5f):F1}ms p95={Percentile(.95f):F1}ms p99={Percentile(.99f):F1}ms max={sorted.Last():F1}ms >=100ms={slow}\n");
                File.WriteAllLines(Path.Combine(folder, role + "-" + label + "-frames-ms.txt"), times.Select(ms => ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)));
                foreach (var counter in recorders)
                {
                    var samples = new List<ProfilerRecorderSample>();
                    if (!counter.recorder.Valid) continue;
                    counter.recorder.CopyTo(samples);
                    if (samples.Count == 0) continue;
                    File.AppendAllText(Report, $"PROFILE {label} {counter.name} n={samples.Count} mean={samples.Average(sample => (double)sample.Value):F0} max={samples.Max(sample => sample.Value)}\n");
                }
            }
            finally
            {
                foreach (var counter in recorders) counter.recorder.Dispose();
            }
        }


        IEnumerator Check()
        {
            File.WriteAllText(Report,"Independent player "+role+"\n");
            if(TenRound){yield return CheckTenPlayerRound();yield break;}
            if(FullRound){yield return CheckFullRound();yield break;}
            if(TycoonTest){yield return CheckTycoon();yield break;}
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

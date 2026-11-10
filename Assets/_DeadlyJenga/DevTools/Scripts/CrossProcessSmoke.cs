#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion.Statistics;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;
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
            Directory.CreateDirectory(test.folder);test.deadline=Time.realtimeSinceStartup+(LobbyHandoff||test.ProbeMotion||FullRoom?600:180);
            Application.runInBackground=true;Application.logMessageReceived+=test.Log;test.StartCoroutine(test.Guard(test.Check()));
        }
        string Report=>Path.Combine(folder,role+".txt");
        string Mark(string name)=>Path.Combine(folder,name);
        void Log(string message,string trace,LogType type)
        {
            if(done||type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            if (FullRoom && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null &&
                (message.StartsWith("RenderTexture.", StringComparison.Ordinal) ||
                 message.StartsWith("RenderingCommandBuffer:", StringComparison.Ordinal) ||
                 message.StartsWith("EndRenderPass:", StringComparison.Ordinal) ||
                 message.Contains("Unable to find surface for attachment"))) return;
            // Explicit deliberate disconnect can race Fusion's current cloud request.
            File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        void Update(){if(!done&&Time.realtimeSinceStartup>deadline)Finish(false,"Timed out");}
        void Finish(bool ok,string message)
        {if(done)return;done=true;if(ProbeMotion)FusionMotionWireTrace.Save(Mark(role + "-wire.csv"));File.AppendAllText(Report,(ok?"COMPLETE ":"FAILED ")+message+"\n");Application.logMessageReceived-=Log;Application.Quit(ok?0:1);}
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
        static bool FullRoom => Environment.GetCommandLineArgs().Contains("--dj-full-room");
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


        bool StressMotion => Environment.GetCommandLineArgs().Contains("--dj-motion-stress");
        bool ProbeMotion => Environment.GetCommandLineArgs().Contains("--dj-motion-probe");

        sealed class MotionProbeDriver : DeadlyJenga.CastleBots.ICastleSurvivorDriver
        {
            readonly Vector3 origin;
            readonly double started;
            public MotionProbeDriver(Vector3 origin)
            { this.origin = origin; started = Time.realtimeSinceStartupAsDouble; }
            public DeadlyJenga.CastleBots.CastleBotCommand ReadCommand(CastleSurvivor motor, float dt)
            {
                float t = (float)(Time.realtimeSinceStartupAsDouble - started) * 1.4f;
                var destination = origin + new Vector3(Mathf.Sin(t), 0, 1 - Mathf.Cos(t)) * 1.25f;
                var move = destination - motor.transform.position;
                move.y = 0;
                return new DeadlyJenga.CastleBots.CastleBotCommand
                { move = Vector3.ClampMagnitude(move * 2, 1), look = move.sqrMagnitude > .01f ? move.normalized : Vector3.forward };
            }
        }

        static System.Reflection.FieldInfo ProbeField(object target, string name)
            => target.GetType().GetField(name, PrivateField | System.Reflection.BindingFlags.Public)
                ?? throw new MissingFieldException(target.GetType().Name, name);

        static Vector3 RenderedFeet(INetworkActorView view)
        {
            if (view == null) return Vector3.zero;
            var field = view.GetType().GetField("frame", PrivateField);
            if (field != null)
            {
                var frame = field.GetValue(view);
                if (frame.GetType().GetField("feet")?.GetValue(frame) is Vector3 feet) return feet;
            }
            return view is Component part ? part.transform.position : view.NetworkPosition;
        }


        IEnumerator StartDirectMotionMatch(bool host, Action<Fusion.NetworkRunner> connected)
        {
            const string marker = "direct-room-code";
            if (!host)
                yield return Until(() => File.Exists(Mark(marker)), "Host publishes direct match room", 180);
            string code = host ? "dj-motion-" + Guid.NewGuid().ToString("N").Substring(0, 16) : File.ReadAllText(Mark(marker));
            var holder = new GameObject("Explicit motion probe runner");
            DontDestroyOnLoad(holder);
            var runner = holder.AddComponent<Fusion.NetworkRunner>();
            var start = CastleMotionStateProbe.ConnectDirect(runner, holder, host, code);
            yield return Until(() => start.IsCompleted, "Direct Fusion StartGame completed", 75);
            Require(!start.IsFaulted && start.Result.Ok, "Direct match uses real Fusion connection");
            if (host)
            {
                File.WriteAllText(Mark(marker + "-pending"), code);
                File.Move(Mark(marker + "-pending"), Mark(marker));
            }
            yield return Until(() => runner.ActivePlayers.Count() == 2, "Direct match has both independent peers", 100);
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.ModeId = StressMotion ? "demo" : "ctf";
            MatchSetup.MapId = StressMotion ? "skyline-island" : "mode-ctf";
            MatchSetup.BotTarget = StressMotion ? 26 : 3;
            connected(runner);
            if (!host)
                yield return Until(() => File.Exists(Mark("host-game-bound")), "Host already bound its Game transport", 100);
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Game");
            if (host)
            {
                yield return Until(() => FindFirstObjectByType<CastlePlaySession>() is { } game && game.Network,
                    "Host binds Game transport before guest enters", 70);
                File.WriteAllText(Mark("host-game-bound"), "bound");
            }
        }

        IEnumerator CheckMotionProbe()
        {
            bool host = role == "host";
            Fusion.NetworkRunner probeRunner = null;
            if (Environment.GetCommandLineArgs().Contains("--dj-motion-direct"))
                yield return StartDirectMotionMatch(host, runner => probeRunner = runner);
            else
            {
            HomeSession home = null;
            yield return Until(() => (home = FindFirstObjectByType<HomeSession>()) && !string.IsNullOrEmpty(home.Region),
                "Home initialized before motion probe", 150);
            if (host)
            {
                yield return Until(() => File.Exists(Mark("guest-home")), "Independent guest is ready", 360);
                if (StressMotion)
                {
                    home.Demo();
                    yield return Until(() => IslandLobby.Current && IslandLobby.Current.CanHost,
                        "Developer lobby is ready to host", 75);
                    IslandLobby.Current.Host();
                }
                else home.Host();
                yield return Until(() => IslandLobby.Current && IslandLobby.Current.Room.IsHost && IslandLobby.Current.CanStart,
                    "Host opens live Fusion room", 75);
                var lobby = IslandLobby.Current;
                Require(lobby.SelectMode(StressMotion ? "demo" : "ctf") && lobby.SelectMap(StressMotion ? "sparse-prototype-skyline-island" : "mode-ctf"),
                    "Host selects the requested fixture through the lobby");
                File.WriteAllText(Mark("pending-code"), lobby.Room.Code);
                File.Move(Mark("pending-code"), Mark("code"));
                yield return Until(() => lobby.Room.HumanCount == 2 && File.Exists(Mark("guest-joined")), "Guest joined the room", 120, () => lobby.Room.Status);
                lobby.StartOrCancel();
            }
            else
            {
                File.WriteAllText(Mark("guest-home"), "ready");
                yield return Until(() => File.Exists(Mark("code")), "Host published room code", 420);
                home.Code = File.ReadAllText(Mark("code"));
                home.JoinCode();
                yield return Until(() => IslandLobby.Current && IslandLobby.Current.Room.Connected &&
                    !IslandLobby.Current.Room.IsHost && IslandLobby.Current.SelectedMapId == (StressMotion ? "sparse-prototype-skyline-island" : "mode-ctf"),
                    "Guest joined the same configured room", 90, () =>
                    $"{IslandRoomConnection.Instance?.Status} lobby={IslandLobby.Current != null} " +
                    $"selected={IslandLobby.Current?.SelectedMapId} peers={IslandRoomConnection.Instance?.HumanCount}");
                File.WriteAllText(Mark("guest-joined"), "joined");
            }

            probeRunner = IslandRoomConnection.Instance.Runner;
            }
            CastlePlaySession session = null;
            yield return Until(() => (session = FindFirstObjectByType<CastlePlaySession>()) && session.Network &&
                probeRunner && probeRunner.ActivePlayers.Count() == 2 && (!host || session.Network.HumanCount == 2) && !session.Network.WaitingForWorld && Services.World?.Blocks.Count > 0 &&
                NetworkActors.Alive(session.Player), "Both humans and world are ready for movement", 125, () =>
                $"runner={probeRunner && probeRunner.IsRunning} players={(probeRunner ? probeRunner.ActivePlayers.Count() : 0)} " +
                $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                $"worldWaiting={session && session.Network && session.Network.WaitingForWorld} hostHumans={(session && session.Network ? session.Network.HumanCount : 0)} " +
                $"blocks={Services.World?.Blocks.Count}");
            var sync = session.GetComponent<CastleFusionSession>();
            var lan = session.Network;
            var player = ((Component)session.Player).GetComponent<CastleSurvivor>();
            session.SetMenu(false);
            var world = Services.World;
            int block;
            if (host)
            {
                block = Enumerable.Range(0, world.Blocks.Count).Where(i => world.Blocks[i] && !world.Blocks[i].isKinematic)
                    .OrderByDescending(i => world.Blocks[i].position.y).First();
                File.WriteAllText(Mark("tracked-block"), block.ToString());
                CastleMotionStateProbe.SelectedBlock = block;
            }
            else
            {
                yield return Until(() => File.Exists(Mark("tracked-block")), "Host selects a dynamic block", 35);
                block = int.Parse(File.ReadAllText(Mark("tracked-block")));
            }
            var viewField = ProbeField(lan, "views");
            var views = (Dictionary<string, INetworkActorView>)viewField.GetValue(lan);
            var receiver = ProbeField(sync, "worldReceiver").GetValue(sync);
            var worldSerial = ProbeField(receiver, "serial");
            var lastPose = ProbeField(sync, "lastPoseBatch");
            object peer = host ? ((IDictionary)ProbeField(sync, "peers").GetValue(sync)).Values.Cast<object>().Single() : null;
            var waiting = host ? ProbeField(peer, "waiting") : null;
            var sentAt = host ? ProbeField(peer, "sentAt") : null;
            var poseTraffic = host ? sync.PeerPoseTraffic.Single() : sync.LocalPoseTraffic;
            string remoteId = probeRunner.ActivePlayers.Single(p => p != probeRunner.LocalPlayer).ToString();
            GameObject bot = null;
            string trackedBot = null;
            var startingTravel = new Dictionary<CastleBotBrain, float>();
            if (host)
            {
                session.SurvivorBots.AutoFill = false;
                session.SurvivorBots.Clear();
                UnityEngine.Random.InitState(1109);
                int bots = StressMotion ? 24 : 1;
                for (int i = 0; i < bots; i++) session.SurvivorBots.SpawnOne();
                Require(session.SurvivorBots.Count == bots, $"Host spawned exactly {bots} bots");
                yield return Until(() => session.SurvivorBots.Members.Any(), "Host has an authoritative bot", 40);
                bot = session.SurvivorBots.Members.First();
                if(StressMotion)
                    foreach(var member in session.SurvivorBots.Members)
                        if(member&&member.TryGetComponent<CastleBotBrain>(out var brain))
                            startingTravel[brain]=brain.TravelDistance;
                var actorId = typeof(CastleLanSession).GetMethod("LocalActorId",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                trackedBot = (string)actorId.Invoke(null, new object[] { bot.transform });
                File.WriteAllText(Mark("tracked-bot"), trackedBot);
            }
            else
            {
                yield return Until(() => File.Exists(Mark("tracked-bot")), "Host identifies the tracked bot", 40);
                trackedBot = File.ReadAllText(Mark("tracked-bot"));
            }
            if (CastleMotionStateProbe.Enabled)
            {
                if (host) Require(CastleMotionStateProbe.StartHost(probeRunner), "Host spawned Fusion state probe");
                yield return Until(() => CastleMotionStateProbe.Local && CastleMotionStateProbe.Local.Sequence != 0,
                    "Independent peer sees the woven Fusion state", 45);
            }
            var source = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--dj-source="));
            File.AppendAllText(Report, $"TRACE {source} map={MatchSetup.MapId} blocks={world.Blocks.Count} role={role} " +
                $"connection={probeRunner.CurrentConnectionType} fixture=scripted-collision-motor,authoritative-bot,dynamic-block-impulse\n");
            File.WriteAllText(Mark(role + "-trace-ready"), "ready");
            yield return Until(() => File.Exists(Mark((host ? "guest" : "host") + "-trace-ready")), "Peer is ready to trace", 60);
            Require(world.Blocks[block].isKinematic != host, "Selected block preserves host physics authority");
            yield return new WaitForSecondsRealtime(1.25f);
            var driver = new MotionProbeDriver(player.transform.position);
            Camera motionCamera = null;
            RenderTexture motionTexture = null;
            Texture2D motionPixels = null;
            int motionFrame = 0;
            long captureStart = 0;
            bool footage = Environment.GetCommandLineArgs().Contains("--dj-motion-footage");
            if (footage)
            {
                var pivot = world.Blocks[block].position;
                var cameraObject = new GameObject("Motion probe fixed capture view");
                motionCamera = cameraObject.AddComponent<Camera>();
                if (session.ActiveCamera) motionCamera.CopyFrom(session.ActiveCamera);
                motionCamera.enabled = false;
                motionCamera.transform.SetPositionAndRotation(pivot + new Vector3(6, 4, -8),
                    Quaternion.LookRotation(new Vector3(-6, -4.4f, 8)));
                motionTexture = new RenderTexture(640, 360, 24);
                motionPixels = new Texture2D(640, 360, TextureFormat.RGB24, false);
                SaveMotionFrame(motionCamera, motionTexture, motionPixels, Mark(role + "-warm.jpg"));
                File.WriteAllText(Mark(role + "-footage-warm"), "ready");
                var otherRole = host ? "guest" : "host";
                yield return Until(() => File.Exists(Mark(otherRole + "-footage-warm")), "Peer camera warm", 60);
                var cue = Mark("footage-start");
                if (host) File.WriteAllText(cue, DateTime.UtcNow.AddSeconds(4).Ticks.ToString());
                else yield return Until(() => File.Exists(cue), "Host publishes the shared footage clock", 12);
                captureStart = long.Parse(File.ReadAllText(cue));
                while (DateTime.UtcNow.Ticks < captureStart) yield return null;
            }
            var profileHandles = new List<ProfilerRecorderHandle>();
            var profiles = new List<(string name, ProfilerRecorder recorder)>();
            if (host && StressMotion && Environment.GetCommandLineArgs().Contains("--dj-motion-profile"))
            {
                ProfilerRecorderHandle.GetAvailable(profileHandles);
                foreach (var handle in profileHandles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name.StartsWith("DJ.", StringComparison.Ordinal) ||
                        description.Name == "Physics.Simulate" || description.Name == "Physics.SyncTransforms" ||
                        description.Name == "BehaviourUpdate" || description.Name == "BehaviourFixedUpdate" ||
                        description.Name == "Main Thread" || description.Name == "Render Thread" ||
                        description.Name == "GC Allocated In Frame")
                        profiles.Add((description.Name, ProfilerRecorder.StartNew(description.Category, description.Name, 2048)));
                }
            }
            try
            {
                double start = Time.realtimeSinceStartupAsDouble;
                var data = new System.Text.StringBuilder(160 * 1024);
                data.AppendLine("elapsed,clock,frameMs,gc0,rttSeconds,worldSerial,worldWaitSeconds,poseSerial,posePending,poseSuppressed,poseSent,poseSentBytes,poseApplied,localX,localY,localZ,remoteTargetX,remoteTargetY,remoteTargetZ,remoteRenderX,remoteRenderY,remoteRenderZ,botX,botY,botZ,blockX,blockY,blockZ,stateSequence,stateTick,stateHumanX,stateHumanY,stateHumanZ,stateBotX,stateBotY,stateBotZ,stateBlockX,stateBlockY,stateBlockZ");
                bool pushed = false, sawWave = false;
                double nextRtt = 0, rtt = 0;
                var wireStats = new System.Text.StringBuilder("elapsed,inBandwidth,outBandwidth,reliableQueueDepth,fragmentGroupsLost\n");
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                while (Time.realtimeSinceStartupAsDouble - start < (StressMotion ? 24 : 12))
                {
                    double now = Time.realtimeSinceStartupAsDouble, elapsed = now - start;
                    if (StressMotion && !sawWave) sawWave = Tracked.Waves.Any(wave => wave.Wave.active);
                    if (host && !pushed && elapsed >= 2)
                    {
                        var body = world.Blocks[block];
                        if (body && !body.isKinematic) body.AddForce(new Vector3(5, 4, 2), ForceMode.VelocityChange);
                        if (StressMotion) Require(DemoDisasters.Execute(session, "Tsunami"), "Host started the live tsunami");
                        pushed = true;
                    }
                    if (elapsed >= nextRtt)
                    {
                        rtt = probeRunner.GetPlayerRtt(probeRunner.ActivePlayers.Single(p => p != probeRunner.LocalPlayer));
                        nextRtt = elapsed + 1;
                        if (probeRunner.TryGetFusionStatistics(out var statistics))
                        {
                            var stats = statistics.SimulationSnapshot.Stats;
                            wireStats.AppendFormat(ci, "{0:F3},{1},{2},{3},{4}\n", elapsed,
                                stats.GetValueOrDefault(FusionStatType.InBandwidth, 0),
                                stats.GetValueOrDefault(FusionStatType.OutBandwidth, 0),
                                stats.GetValueOrDefault(FusionStatType.ReliableQueueDepth, 0),
                                stats.GetValueOrDefault(FusionStatType.StateFragmentGroupsLost, 0));
                        }
                    }
                    if (player.Capsule && player.Capsule.enabled)
                    {
                        var command = driver.ReadCommand(player, Time.deltaTime);
                        player.Capsule.Move(command.move * Mathf.Min(Time.deltaTime, .04f) * 2.4f);
                    }
                    views.TryGetValue(remoteId, out var remote);
                    views.TryGetValue(trackedBot, out var botView);
                    int serial = host ? (int)waiting.GetValue(peer) : (int)worldSerial.GetValue(receiver);
                    float wait = host && serial != 0 ? Time.unscaledTime - (float)sentAt.GetValue(peer) : 0;
                    data.AppendFormat(ci, "{0:F5},{1},{2:F3},{3},{4:F4},{5},{6:F5},{7},{8},{9},{10},{11},{12}",
                        elapsed, System.Diagnostics.Stopwatch.GetTimestamp(), Time.unscaledDeltaTime * 1000,
                        GC.CollectionCount(0), rtt, serial, wait, lastPose.GetValue(sync), poseTraffic.PendingCount,
                        poseTraffic.SuppressedTicks, poseTraffic.SubmittedFrames, poseTraffic.SubmittedBytes, poseTraffic.AppliedFrames);
                    AppendPosition(data, player.transform.position);
                    AppendPosition(data, remote?.NetworkPosition ?? Vector3.zero);
                    AppendPosition(data, RenderedFeet(remote));
                    AppendPosition(data, bot ? bot.transform.position : RenderedFeet(botView));
                    AppendPosition(data, world.Blocks[block].position);
                    var shadow = CastleMotionStateProbe.Local;
                    data.AppendFormat(ci, ",{0},{1}", shadow ? shadow.Sequence : 0, shadow ? shadow.CaptureTick.Raw : 0);
                    AppendPosition(data, shadow ? shadow.Human : Vector3.zero);
                    AppendPosition(data, shadow ? shadow.Bot : Vector3.zero);
                    AppendPosition(data, shadow ? shadow.Block : Vector3.zero);
                    data.AppendLine();
                    if (motionCamera && elapsed >= motionFrame / 8d && motionFrame < 48)
                    {
                        SaveMotionFrame(motionCamera, motionTexture, motionPixels, Mark($"{role}-frame-{motionFrame:D4}.jpg"));
                        File.AppendAllText(Mark(role + "-frame-times.csv"), FormattableString.Invariant($"{motionFrame},{elapsed:F4}") + Environment.NewLine);
                        motionFrame++;
                    }
                    yield return null;
                }
                File.WriteAllText(Mark(role + "-motion.csv"), data.ToString());
                File.WriteAllText(Mark(role + "-fusion-stats.csv"), wireStats.ToString());
                if (StressMotion) Require(sawWave, "Tsunami was active during the sample");
                FusionMotionWireTrace.Save(Mark(role + "-wire.csv"));
            }
            finally
            {
                foreach (var (name, recorder) in profiles)
                {
                    try
                    {
                        var samples = recorder.ToArray();
                        File.AppendAllLines(Mark("host-profiler.txt"), samples.Length == 0
                            ? new[] { name + ": no samples" }
                            : new[] { name + ": " + string.Join(",", samples.Select(sample => sample.Value)) });
                    }
                    finally { recorder.Dispose(); }
                }
                CastleMotionStateProbe.SelectedBlock = -1;
                if (motionCamera) Destroy(motionCamera.gameObject);
                if (motionTexture) Destroy(motionTexture);
                if (motionPixels) Destroy(motionPixels);
            }
            File.WriteAllText(Mark(role + "-trace-done"), "done");
            if(host&&StressMotion)
            {
                int travelled=startingTravel.Count(pair=>pair.Key&&pair.Key.TravelDistance-pair.Value>=.5f);
                File.AppendAllText(Report,$"BOTS movement={travelled}/{startingTravel.Count} at least 0.5 m\n");
                Require(startingTravel.Count==24&&travelled>=12,"At least half of the authoritative bots travelled during the wave");
            }
            yield return Until(() => File.Exists(Mark((host ? "guest" : "host") + "-trace-done")), "Both independent peer traces completed", 40);
            if (!Environment.GetCommandLineArgs().Contains("--dj-motion-lifecycle")) yield break;

            var room = IslandRoomConnection.Instance;
            var synced = (ISyncedWorld)Services.World;
            int before = synced.Revision;
            if (host)
            {
                ((IRebuildable)Services.World).Rebuild();
                Require(synced.Revision != before, "Host rebuild changes the world revision");
                File.WriteAllText(Mark("rebuild-revision"), synced.Revision.ToString());
                yield return Until(() => File.Exists(Mark("guest-rebuild-ready")),
                    "Guest acknowledges the rebuilt world", 55);
                Require(room.ReturnMatchToLobby(), "Host requests normal return to lobby");
            }
            else
            {
                yield return Until(() => File.Exists(Mark("rebuild-revision")), "Host publishes new revision", 55);
                int expected = int.Parse(File.ReadAllText(Mark("rebuild-revision")));
                var revision = ProbeField(lan, "hostRevision");
                yield return Until(() => (int)revision.GetValue(lan) == expected && Services.World.Blocks.Count > 0,
                    "Guest applies the rebuilt world revision", 55);
                File.WriteAllText(Mark("guest-rebuild-ready"), "ready");
            }
            yield return Until(() => IslandLobby.Current && room.Connected && room.HumanCount == 2 &&
                ReferenceEquals(Services.Session, IslandLobby.Current), "Both clients stay connected on return to lobby", 75,
                () => room.Status);
            File.WriteAllText(Mark(role + "-lobby-returned"), "ready");
            yield return Until(() => File.Exists(Mark((host ? "guest" : "host") + "-lobby-returned")),
                "Other client confirms the lobby return", 25);
        }

        static void SaveMotionFrame(Camera camera, RenderTexture output, Texture2D pixels, string path)
        {
            var previous = RenderTexture.active;
            camera.targetTexture = output;
            try
            {
                camera.Render();
                RenderTexture.active = output;
                pixels.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToJPG(78));
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
            }
        }

        static void AppendPosition(System.Text.StringBuilder data, Vector3 position) =>
            data.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, ",{0:F4},{1:F4},{2:F4}",
                position.x, position.y, position.z);

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

        IEnumerator CheckFullRoom()
        {
            const int Seats = 10;
            bool host = role == "host";
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = role == "host" || role == "guest-01" ? 90 : 30;
            if (Environment.GetCommandLineArgs().Contains("--dj-full-room-direct"))
            {
                yield return CheckDirectFullRoom(host, Seats);
                yield break;
            }
            HomeSession home = null;
            yield return Until(() => (home = FindFirstObjectByType<HomeSession>()) && !string.IsNullOrEmpty(home.Region),
                "Home is ready", 95);
            if (host)
            {
                home.Host();
                yield return Until(() => IslandLobby.Current && IslandLobby.Current.Room.IsHost &&
                    IslandLobby.Current.CanStart, "Normal room is open", 70);
                Require(IslandLobby.Current.SelectMode("ctf") && IslandLobby.Current.SelectMap("mode-ctf"),
                    "Host selects CTF");
                File.WriteAllText(Mark("pending-code"), IslandLobby.Current.Room.Code);
                File.Move(Mark("pending-code"), Mark("code"));
                yield return Until(() => IslandLobby.Current.Room.HumanCount == Seats &&
                    Directory.GetFiles(folder, "guest-*-ready").Length == Seats - 1,
                    "Nine other processes confirmed the full lobby", 145, () => IslandLobby.Current.Room.Status);
                IslandLobby.Current.StartOrCancel();
            }
            else
            {
                yield return Until(() => File.Exists(Mark("code")), "Host published code", 120);
                home.Code = File.ReadAllText(Mark("code"));
                home.JoinCode();
                yield return Until(() => IslandLobby.Current && IslandLobby.Current.Room.Connected &&
                    IslandLobby.Current.SelectedMapId == "mode-ctf", "Guest joined the configured room", 100,
                    () => IslandRoomConnection.Instance?.Status);
                File.WriteAllText(Mark(role + "-ready"), "ready");
            }

            var room = IslandRoomConnection.Instance;
            CastlePlaySession game = null;
            yield return Until(() => (game = FindFirstObjectByType<CastlePlaySession>()) && game.Network &&
                !game.Network.WaitingForWorld && room.Runner.ActivePlayers.Count() == Seats &&
                (!host || game.Network.HumanCount == Seats) && game.Modes && game.Modes.Frame?.phase==RoundPhase.Playing,
                "All ten processes are in the active match", 125,
                () => $"{room.Status} | {room.GetComponent<RoomGameExperience>()?.GameStatus} | phase={game?.Modes?.Frame?.phase}");
            string code = room.Code;
            var originalRunner = room.Runner;
            File.WriteAllText(Mark(role + "-playing"), "ready");
            yield return Until(() => Directory.GetFiles(folder, "*-playing").Length == Seats,
                "All peers received their initial match state", 60);
            yield return DriveFullRoom(game, role + "-before");
            File.WriteAllText(Mark(role + "-moving"), "ready");
            if (host)
            {
                yield return Until(() => Directory.GetFiles(folder, "*-moving").Length == Seats,
                    "All peers measured movement before handoff", 50);
                var leave = room.Leave();
                yield return Until(() => leave.IsCompleted && !room.Connected, "Original host left mid-match", 50);
                yield break;
            }
            yield return Until(() => room.Connected && room.Runner != originalRunner && !room.Migrating &&
                room.Runner.ActivePlayers.Count() == Seats - 1 &&
                (game = FindFirstObjectByType<CastlePlaySession>()) && game.Network && !game.Network.WaitingForWorld,
                "Match recovered on the new runner with nine peers", 90, () => room.Status);
            Require(room.Code == code && MatchSetup.ModeId == "ctf", "Room and mode survive the host departure");
            Require(room.IsHost == !game.Network.IsClient, "Simulation authority matches the new runner");
            File.WriteAllText(Mark(role + "-authority"), room.IsHost ? "host" : "guest");
            yield return Until(() => Directory.GetFiles(folder, "guest-*-authority").Length == Seats - 1,
                "All surviving peers reported authority", 50);
            Require(Directory.GetFiles(folder, "guest-*-authority").Count(path => File.ReadAllText(path) == "host") == 1,
                "Exactly one survivor became host");
            yield return DriveFullRoom(game, role + "-after");
            File.WriteAllText(Mark(role + "-finished"), "ready");
            yield return Until(() => Directory.GetFiles(folder, "guest-*-finished").Length == Seats - 1,
                "All surviving peers measured post-migration movement", 50);
        }

        IEnumerator CheckDirectFullRoom(bool host, int seats)
        {
            string codePath = Mark("direct-full-code");
            if (!host) yield return Until(() => File.Exists(codePath), "Host publishes the stress-room code", 120);
            string code = host ? "dj-full-" + Guid.NewGuid().ToString("N") : File.ReadAllText(codePath);
            var holder = new GameObject("Full room load fixture");
            DontDestroyOnLoad(holder);
            var runner = holder.AddComponent<Fusion.NetworkRunner>();
            var connection = CastleMotionStateProbe.ConnectDirect(runner, holder, host, code, seats);
            yield return Until(() => connection.IsCompleted, "Independent Fusion connection completes", 100);
            Require(!connection.IsFaulted && connection.Result.Ok, "Connected to Photon");
            if (host)
            {
                File.WriteAllText(Mark("direct-full-pending"), code);
                File.Move(Mark("direct-full-pending"), codePath);
            }
            yield return Until(() => runner.ActivePlayers.Count() == seats, "All ten clients are connected", 125);
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.ModeId = "ctf";
            MatchSetup.MapId = "mode-ctf";
            MatchSetup.BotTarget = seats;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Game");
            CastlePlaySession game = null;
            yield return Until(() => (game = FindFirstObjectByType<CastlePlaySession>()) && game.Network &&
                !game.Network.WaitingForWorld && (!host || game.Network.HumanCount == seats),
                "Authoritative world reached every client", 125);
            File.WriteAllText(Mark(role + "-playing"), "ready");
            yield return Until(() => Directory.GetFiles(folder, "*-playing").Length == seats,
                "All ten clients are playing CTF", 60);
            yield return DriveFullRoom(game, role + "-before");
            File.WriteAllText(Mark(role + "-moving"), "ready");
            yield return Until(() => Directory.GetFiles(folder, "*-moving").Length == seats,
                "All ten clients measured motion", 60);
        }

        IEnumerator DriveFullRoom(CastlePlaySession game, string phase)
        {
            var survivor = ((Component)game.Player).GetComponent<CastleSurvivor>();
            var views = (Dictionary<string, INetworkActorView>)ProbeField(game.Network, "views").GetValue(game.Network);
            var driver = new MotionProbeDriver(survivor.transform.position);
            var moved = new HashSet<string>();
            var previous = new Dictionary<string, Vector3>();
            var lastMove = new Dictionary<string, float>();
            var data = new System.Text.StringBuilder("elapsed,frameMs,visibleActors,movedActors,worstObservedMoveGapMs\n");
            float worst = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 7)
            {
                float elapsed = Time.realtimeSinceStartup - start;
                if (survivor.Capsule && survivor.Capsule.enabled)
                {
                    var command = driver.ReadCommand(survivor, Time.deltaTime);
                    survivor.Capsule.Move(command.move * Mathf.Min(Time.deltaTime, .04f) * 2.4f);
                }
                foreach (var pair in views)
                {
                    if (!pair.Key.StartsWith("[Player:", StringComparison.Ordinal) ||
                        pair.Value == null || !pair.Value.NetworkActive) continue;
                    var point = RenderedFeet(pair.Value);
                    if (previous.TryGetValue(pair.Key, out var earlier) && (point - earlier).sqrMagnitude > .000009f)
                    {
                        moved.Add(pair.Key);
                        if (lastMove.TryGetValue(pair.Key, out float last)) worst = Mathf.Max(worst, elapsed - last);
                        lastMove[pair.Key] = elapsed;
                    }
                    previous[pair.Key] = point;
                }
                data.AppendLine(FormattableString.Invariant($"{elapsed:F4},{Time.unscaledDeltaTime * 1000:F3},{previous.Count},{moved.Count},{worst * 1000:F1}"));
                yield return null;
            }
            File.WriteAllText(Mark(phase + ".csv"), data.ToString());
            Require(moved.Count > 0, "Observed real remote movement during " + phase);
        }

        IEnumerator Check()
        {
            File.WriteAllText(Report,"Independent player "+role+"\n");
            if(FullRoom)
            {
                yield return CheckFullRoom();
                yield break;
            }
            if(ProbeMotion)
            {
                yield return CheckMotionProbe();
                yield break;
            }
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using DeadlyJenga.Core;
using P = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-150)]
    public sealed class CastleLanSession : MonoBehaviour, IMultiplayerControls
    {
        [Serializable] public sealed class Advertisement
        {
            public string app = P.Version, id, name, target, kind, token;
            public int port;
            public bool available;
        }
        public sealed class Nearby
        {
            public Advertisement info;
            public IPAddress address;
            public float seen;
        }
        sealed class Pending { public Nearby peer; public float expires; public bool invite; }
        sealed class Grant { public string token; public IPAddress address; public float expires; }
        sealed class Connection
        {
            public CastleLanTransport.Link link;
            public string id;
            public float connected, lastPose;
            public P.Actor actor;
        }
        // The map, asked through Core. Networking never names a specific map type.
        static IWorldQuery World => Services.World;
        static ISyncedWorld Synced => Services.World as ISyncedWorld;
        string id = Guid.NewGuid().ToString("N");
        readonly Dictionary<string, Nearby> nearby = new Dictionary<string, Nearby>();
        readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        readonly Dictionary<string, Grant> grants = new Dictionary<string, Grant>();
        readonly Dictionary<string, float> invitations = new Dictionary<string, float>();
        readonly List<Connection> connections = new List<Connection>();
        readonly Dictionary<string, INetworkActorView> views = new Dictionary<string, INetworkActorView>();
        readonly ConcurrentQueue<Action> completions = new ConcurrentQueue<Action>();
        CastleLanTransport transport;
        CastlePlaySession session;
        CastleLanTransport.Link host;
        Nearby requested;
        string requestedToken;
        bool connecting, welcomed, receivedWorld;
        int connectAttempt, selectedIndex, hostRevision = -1;
        float nextBeacon, nextState, nextPose, requestedAt, lastRequest, worldAt, lastWorldAt;
        P.Snapshot targetWorld;
        P.Pose[] previousBlocks;
        INetworkPreview localPreview;
        string displayName;
        bool oldRunInBackground;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject multicastLock;
#endif
        bool fusionMode, fusionClient;
        readonly Dictionary<string, P.Actor> fusionActors = new Dictionary<string, P.Actor>();
        public bool IsFusion => fusionMode;
        public bool IsClient => fusionMode ? fusionClient : host != null;
        public bool CanEditWorld => !IsClient || fusionMode&&GetComponent<CastleFusionSession>()&&GetComponent<CastleFusionSession>().CanUseDemoControls;
        public string LocalParticipantId=>id;
        public event Action<string,byte[]> SocialReceived;
        public event Action<string> ParticipantLeft;
        public void SendSocial(byte[] data,string except=null)
        {
            if(data==null||data.Length<1||data.Length>16000||(data[0]!=5&&data[0]!=6))return;
            if(fusionMode){GetComponent<CastleFusionSession>()?.SendSocial(data,except);return;}
            if(IsClient){if(data[0]==5)host.SendAudio(data);else host.Send(data);}
            else foreach(var c in connections)if(c.id!=null&&c.id!=except){if(data[0]==5)c.link.SendAudio(data);else c.link.Send(data);}
        }
        public Transform ParticipantTransform(string actorId)
        {
            if(actorId==id)return session.Player.transform;
            return views.TryGetValue(actorId,out var view)&&view is Component c&&c?c.transform:null;
        }
        public bool TryParticipantPosition(string actorId,out Vector3 position)
        {
            if(actorId==id&&session&&NetworkActors.Alive(session.Player)){position=session.Player.ViewCamera.transform.position;return true;}
            if(actorId!=null&&views.TryGetValue(actorId,out var view)&&NetworkActors.Alive(view)){position=view.NetworkPosition+(view.NetworkKind==NetworkKinds.Survivor?Vector3.up*1.6f:Vector3.zero);return true;}
            position=default;return false;
        }
        public IEnumerable<KeyValuePair<string,Vector3>> HumanPositions()
        {
            if(session&&NetworkActors.Alive(session.Player))yield return new KeyValuePair<string,Vector3>(id,session.Player.transform.position);
            if(fusionMode)foreach(var a in fusionActors)yield return new KeyValuePair<string,Vector3>(a.Key,a.Value.position);
            foreach(var c in connections)if(c.id!=null&&c.actor!=null)yield return new KeyValuePair<string,Vector3>(c.id,c.actor.position);
        }
        public bool WaitingForWorld => IsClient && !receivedWorld;
        public int HumanCount => 1 + (fusionMode ? fusionActors.Count : connections.Count(c => c.id != null));
        public string Status { get; private set; } = "Starting local discovery…";
        public int NearbyCount => Choices().Count;
        public string SelectedName => Selected()?.info.name ?? "No local demos found";
        public string PendingLabel
        {
            get
            {
                var p = FirstPending();
                return p == null ? "No invites or join requests" : p.peer.info.name + (p.invite ? " invited you" : " wants to join");
            }
        }
        public bool HasPending => pending.Count > 0 || fusionMode&&IslandRoomConnection.Instance&&IslandRoomConnection.Instance.Discovery?.Invitation!=null;

        public void Initialize(CastlePlaySession owner)
        {
            session = owner; localPreview = session.Hand.gameObject.GetComponent<INetworkPreview>();
            displayName = SystemInfo.deviceName;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "Demo";
            displayName = displayName.Substring(0, Math.Min(24, displayName.Length)) + " · " + id.Substring(0, 4);
            oldRunInBackground = Application.runInBackground; Application.runInBackground = true;
            try
            {
                transport = new CastleLanTransport();
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "DeadlyJengaDiscovery");
                    multicastLock.Call("setReferenceCounted", false); multicastLock.Call("acquire");
                }
#endif
                Status = "Local demo ready · same Wi-Fi · " + displayName;
            }
            catch (Exception e) { Status = "Local networking: " + e.Message; Debug.LogWarning(Status, this); }
        }
        // The castle snapshot/actor implementation is shared by both transports.
        // Fusion mode never opens discovery sockets or TCP connections.
        public void InitializeFusion(CastlePlaySession owner, string playerID, bool client)
        {
            session = owner; id = playerID; fusionMode = true; fusionClient = client;
            localPreview = session.Hand.gameObject.GetComponent<INetworkPreview>();
            Status = client ? "Waiting for host castle…" : "Hosting castle through Fusion";
            if (client)
            {
                StopBots();
                Synced?.SetReplica(true);
                if (localPreview != null) localPreview.NetworkPreview = true;
                session.SetMenu(true);
            }
        }
        Vector3[] migrationVelocity,migrationAngular;
        public void FusionFreeze()
        {
            var blocks=World.Blocks;if(migrationVelocity==null||migrationVelocity.Length!=blocks.Count)migrationVelocity=new Vector3[blocks.Count];if(migrationAngular==null||migrationAngular.Length!=blocks.Count)migrationAngular=new Vector3[blocks.Count];
            for(int i=0;i<blocks.Count;i++)if(blocks[i]&&!blocks[i].isKinematic){migrationVelocity[i]=blocks[i].linearVelocity;migrationAngular[i]=blocks[i].angularVelocity;}
            Synced?.SetReplica(true);
        }
        public void FusionRebind(string newId,bool client,int survivors,int hands,int survivorPreset,int handPreset)
        {
            id=newId;fusionClient=client;fusionActors.Clear();foreach(var key in views.Keys.ToArray())RemoveView(key);
            if(!client)
            {
                if(targetWorld!=null)for(int i=0;i<World.Blocks.Count;i++)if(World.Blocks[i]){World.Blocks[i].position=targetWorld.blocks[i].position;World.Blocks[i].rotation=targetWorld.blocks[i].rotation;}
                Synced?.SetReplica(false);
                for(int i=0;i<World.Blocks.Count;i++)if(World.Blocks[i]&&migrationVelocity!=null&&i<migrationVelocity.Length){World.Blocks[i].linearVelocity=migrationVelocity[i];if(migrationAngular!=null)World.Blocks[i].angularVelocity=migrationAngular[i];}
                if(localPreview!=null)localPreview.NetworkPreview=false;
                var bots=session.SurvivorBots;var handBots=session.HandBots;
                if(bots!=null){bots.Running=true;bots.TargetPopulation=Mathf.Max(10,bots.TargetPopulation);bots.SelectPreset(survivorPreset);}
                if(handBots!=null){handBots.Running=true;handBots.SelectPreset(handPreset);}
                var survivorFrames=targetWorld?.actors.Where(a=>a.id.StartsWith("bot:")).ToArray();
                var handFrames=targetWorld?.actors.Where(a=>a.id.StartsWith("bot-hand:")).ToArray();
                for(int i=0;i<survivors&&bots!=null;i++){var bot=bots.SpawnOne();if(bot&&survivorFrames!=null&&i<survivorFrames.Length)bot.GetComponent<IPlayableCharacter>()?.Place(survivorFrames[i].position,Quaternion.identity);}
                for(int i=0;i<hands&&handBots!=null;i++){var bot=handBots.SpawnOne();if(bot&&handFrames!=null&&i<handFrames.Length)bot.GetComponent<IPlayableCharacter>()?.Place(handFrames[i].position,Quaternion.identity);}
                receivedWorld=false;targetWorld=null;
            }
            else {Synced?.SetReplica(true);if(localPreview!=null)localPreview.NetworkPreview=true;}
            Status=client?"Host reassigned · reconnecting castle":"You are the new castle host";
        }
        public byte[] FusionWelcome(string playerID)
        {
            bool survivor = TrySpawn(out var pose);
            fusionActors[playerID] = InitialActor(playerID, survivor, pose);
            return P.Encode(P.Welcome, w => { w.Write(!survivor); P.Write(w, pose); w.Write((session.Player as ISurvivorSettings)?.SaveStaminaRules() ?? "{}"); });
        }
        public void FusionReceiveWelcome(byte[] data)
        {
            if (welcomed) return;
            using var reader = P.Reader(data, P.Welcome);
            reader.ReadBoolean(); P.ReadPose(reader);
            var rules = P.Text(reader, 4096); (session.Player as ISurvivorSettings)?.LoadStaminaRules(rules);
            ReceiveWelcome(data);
        }
        public byte[] FusionCaptureWorld() => P.EncodeWorld(CaptureWorld());
        public byte[] FusionCapturePlayer() => P.Encode(P.Player, w => P.Write(w, LocalActor()));
        public void FusionApplyWorld(byte[] data)
        {
            var snapshot=P.DecodeWorld(data);float dt=Time.unscaledTime-lastWorldAt;
            if(targetWorld!=null&&targetWorld.revision==snapshot.revision&&targetWorld.blocks.Length==snapshot.blocks.Length&&dt>.01f&&dt<1)
            {migrationVelocity=new Vector3[snapshot.blocks.Length];migrationAngular=new Vector3[snapshot.blocks.Length];for(int i=0;i<snapshot.blocks.Length;i++)
                {migrationVelocity[i]=Vector3.ClampMagnitude((snapshot.blocks[i].position-targetWorld.blocks[i].position)/dt,60);
                    var delta=snapshot.blocks[i].rotation*Quaternion.Inverse(targetWorld.blocks[i].rotation);delta.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;
                    if(axis.sqrMagnitude>0&&!float.IsNaN(axis.x))migrationAngular[i]=Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad/dt),30);}}
            ApplyWorld(snapshot);
        }
        public void FusionReceivePlayer(string playerID, byte[] data)
        {
            using var reader = P.Reader(data, P.Player);
            var actor = P.ReadActor(reader); actor.id = playerID;
            fusionActors[playerID] = actor;
            ShowActor(actor, true);
        }
        public void FusionRemovePlayer(string playerID) { fusionActors.Remove(playerID); RemoveView(playerID); ParticipantLeft?.Invoke(playerID); }
        public void FusionReceiveSocial(string source,byte[] data) { if(data!=null&&data.Length>0&&data.Length<=16000&&(data[0]==5||data[0]==6))SocialReceived?.Invoke(source,data); }
        public void FusionSuspendPlayer(string playerID) { if (views.TryGetValue(playerID, out var view)) view.Suspend(); }
        public void FusionRender()
        {
            if (IsClient && receivedWorld) InterpolateWorld();
            foreach (var view in views.Values) if (NetworkActors.Alive(view)) view.Render(Time.unscaledDeltaTime);
        }
        List<Nearby> Choices() => nearby.Values.Where(p => p.info.available && Time.unscaledTime - p.seen < 5).OrderBy(p => p.info.name).ToList();
        Nearby Selected() { var list = Choices(); return list.Count == 0 ? null : list[selectedIndex % list.Count]; }
        Pending FirstPending() => pending.Values.OrderBy(p => p.expires).FirstOrDefault();
        public void NextNearby() { selectedIndex++; }
        public void RequestSelected() { var p = Selected(); if (p != null) Request(p); else Status = "No demos found. Use the same Wi-Fi; guest-network isolation can block discovery."; }
        void Request(Nearby peer)
        {
            if (IsClient || connecting || HumanCount > 1) { Status = "Leave the current session before joining another demo."; return; }
            requested = peer; requestedAt = Time.unscaledTime; lastRequest = -100;
            Status = "Requesting to join " + peer.info.name + "…";
        }
        public void InviteSelected()
        {
            var peer = Selected();
            if (peer == null || IsClient || connecting) return;
            invitations[peer.info.id] = Time.unscaledTime + 30;
            Send("invite", peer); Status = "Invite sent to " + peer.info.name;
        }
        public void AcceptPending()
        {
            var p = FirstPending(); if (p == null) return;
            pending.Remove(p.peer.info.id);
            if (p.invite) Request(p.peer); else Approve(p.peer);
        }
        public void DeclinePending()
        {
            var p = FirstPending(); if (p == null) return;
            Send("decline", p.peer); pending.Remove(p.peer.info.id);
            Status = "Declined " + p.peer.info.name;
        }
        void Approve(Nearby peer)
        {
            if (IsClient || connecting || requested != null) return;
            if (!grants.TryGetValue(peer.info.id, out var grant) || grant.expires < Time.unscaledTime)
            {
                grant = new Grant { token = Guid.NewGuid().ToString("N"), address = peer.address, expires = Time.unscaledTime + 30 };
                grants[peer.info.id] = grant;
            }
            Send("approve", peer, grant.token); Status = "Accepted " + peer.info.name;
        }
        void Send(string kind, Nearby peer = null, string token = null)
        {
            var message = new Advertisement { id = id, name = displayName, port = transport?.Port ?? 0,
                available = !IsClient && !connecting && requested == null, kind = kind, target = peer?.info.id, token = token };
            transport?.Advertise(Encoding.UTF8.GetBytes(JsonUtility.ToJson(message)), peer?.address);
        }
        void Receive(CastleLanTransport.Datagram packet)
        {
            Advertisement m;
            try { m = JsonUtility.FromJson<Advertisement>(Encoding.UTF8.GetString(packet.Data)); }
            catch { return; }
            if (m == null || m.app != P.Version || string.IsNullOrEmpty(m.id) || m.id.Length > 64 || m.id == id || m.name == null || m.name.Length > 64 || m.port < 1 || m.port > 65535) return;
            if (!string.IsNullOrEmpty(m.target) && m.target != id) return;
            if (!nearby.TryGetValue(m.id, out var peer))
            {
                if (nearby.Count >= 256) return;
                nearby[m.id] = peer = new Nearby();
            }
            peer.info = m; peer.address = packet.Endpoint.Address; peer.seen = Time.unscaledTime;
            if (m.kind == "beacon") return;
            if (m.kind == "decline")
            {
                pending.Remove(m.id); invitations.Remove(m.id);
                if (requested?.info.id == m.id && !connecting) { requested = null; Status = peer.info.name + " declined the request."; }
                return;
            }
            if (m.kind == "approve" && requested?.info.id == m.id && !connecting && !IsClient && !string.IsNullOrEmpty(m.token))
            {
                connecting = true; requestedToken = m.token;
                int attempt = ++connectAttempt;
                transport.Connect(peer.address, m.port, (link, error) => completions.Enqueue(() =>
                {
                    if (!this || attempt != connectAttempt) { link?.Dispose(); return; }
                    connecting = false;
                    if (error != null) { Status = "Could not join: " + error; requested = null; return; }
                    host = link; lastWorldAt = Time.unscaledTime;
                    host.Send(P.Encode(P.Hello, w => { w.Write(P.Version); w.Write(id); w.Write(requestedToken); }));
                    Status = "Joining " + peer.info.name + " · receiving current demo…";
                }));
                return;
            }
            if (IsClient || connecting || requested != null) return;
            if (m.kind == "request")
            {
                if (grants.ContainsKey(m.id) || invitations.TryGetValue(m.id, out var until) && until > Time.unscaledTime) { Approve(peer); return; }
                if (!pending.ContainsKey(m.id)) { pending[m.id] = new Pending { peer = peer, expires = Time.unscaledTime + 30 }; Status = PendingLabel; session.SetMenu(true); }
            }
            else if (m.kind == "invite" && HumanCount == 1)
            {
                if (!pending.ContainsKey(m.id)) { pending[m.id] = new Pending { peer = peer, expires = Time.unscaledTime + 30, invite = true }; Status = PendingLabel; session.SetMenu(true); }
            }
        }
        void Update()
        {
            if (session == null || transport == null) return;
            while (completions.TryDequeue(out var action)) action();
            for (int i = 0; i < 64 && transport.Discovery.TryDequeue(out var packet); i++) Receive(packet);
            while (transport.Errors.TryDequeue(out var error)) Status = "LAN: " + error;
            float now = Time.unscaledTime;
            if (now >= nextBeacon)
            {
                nextBeacon = now + 1; Send("beacon");
                foreach (var key in nearby.Where(p => now - p.Value.seen > 6).Select(p => p.Key).ToArray()) nearby.Remove(key);
                foreach (var key in pending.Where(p => p.Value.expires < now).Select(p => p.Key).ToArray()) pending.Remove(key);
                foreach (var key in grants.Where(p => p.Value.expires < now).Select(p => p.Key).ToArray()) grants.Remove(key);
                foreach (var key in invitations.Keys.ToArray())
                {
                    if (invitations[key] < now) invitations.Remove(key);
                    else if (nearby.TryGetValue(key, out var peer)) Send("invite", peer);
                }
            }
            if (requested != null && !connecting && !IsClient)
            {
                if (now - requestedAt > 30) { requested = null; Status = "Join request expired. You can try again."; }
                else if (now - lastRequest > 1) { lastRequest = now; Send("request", requested); }
            }
            while (transport.Accepted.TryDequeue(out var link))
            {
                if (IsClient || connecting || requested != null) link.Dispose();
                else connections.Add(new Connection { link = link, connected = now, lastPose = now });
            }
            ProcessConnections();
            if (host != null)
            {
                try
                {
                    while (host.Controls.TryDequeue(out var control)){if(control[0]==6)SocialReceived?.Invoke("host",control);else ReceiveWelcome(control);}
                    for(int n=0;n<32&&host.Audio.TryDequeue(out var audio);n++)SocialReceived?.Invoke("host",audio);
                    var state = welcomed ? host.TakeState() : null;
                    if (state != null) ApplyWorld(P.DecodeWorld(state));
                    if (host.Closed || now - lastWorldAt > 12) Leave("Host disconnected · continuing locally from the last received state.");
                }
                catch (Exception e) { Leave("Join ended: " + e.Message); }
            }
            if (IsClient && receivedWorld) InterpolateWorld();
            foreach (var view in views.Values) if (NetworkActors.Alive(view)) view.Render(Time.unscaledDeltaTime);
        }
        void ProcessConnections()
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                var c = connections[i];
                try
                {
                    while (c.link.Controls.TryDequeue(out var control))
                    {
                        if(control[0]==6&&c.id!=null){SocialReceived?.Invoke(c.id,control);continue;}
                        if (c.id != null) throw new InvalidDataException("Duplicate handshake");
                        using var r = P.Reader(control, P.Hello);
                        string version = P.Text(r), peerID = P.Text(r), token = P.Text(r);
                        if (version != P.Version || !grants.TryGetValue(peerID, out var grant) || grant.expires < Time.unscaledTime || grant.token != token || grant.address.ToString() != c.link.Address || connections.Any(other => other != c && other.id == peerID))
                            throw new InvalidDataException("Join was not approved");
                        c.id = peerID; grants.Remove(peerID); invitations.Remove(peerID); pending.Remove(peerID);
                        bool survivor = TrySpawn(out var spawn);
                        c.actor = InitialActor(peerID, survivor, spawn);
                        c.link.Send(P.Encode(P.Welcome, w => { w.Write(!survivor); P.Write(w, spawn); }));
                        c.link.Send(P.EncodeWorld(CaptureWorld()), true);
                        Status = "Hosting " + HumanCount + " humans · live local demo";
                        // Humans do not consume the demo's ten-slot bot budget.
                    }
                    for(int n=0;n<32&&c.link.Audio.TryDequeue(out var audio);n++)if(c.id!=null)SocialReceived?.Invoke(c.id,audio);
                    var state = c.link.TakeState();
                    if (state != null && c.id != null)
                    {
                        using var r = P.Reader(state, P.Player);
                        var actor = P.ReadActor(r); actor.id = c.id;
                        c.actor = actor; c.lastPose = Time.unscaledTime;
                        ShowActor(actor, true);
                    }
                }
                catch (Exception e) { Debug.LogWarning("LAN peer rejected: " + e.Message); c.link.Dispose(); }
                if (c.link.Closed || c.id == null && Time.unscaledTime - c.connected > 5 || Time.unscaledTime - c.lastPose > 12)
                {
                    c.link.Dispose(); connections.RemoveAt(i);
                    if (c.id != null) {RemoveView(c.id);ParticipantLeft?.Invoke(c.id);}
                }
                else if (c.id != null && Time.unscaledTime - c.lastPose > 1 && views.TryGetValue(c.id, out var stale)) stale.Suspend();
            }
        }
        bool spawnHand;
        P.Pose spawnPose;
        void ReceiveWelcome(byte[] data)
        {
            if(welcomed)throw new InvalidDataException("Duplicate welcome");
            using var r = P.Reader(data, P.Welcome);
            spawnHand = r.ReadBoolean(); spawnPose = P.ReadPose(r);
            StopBots();
            session.SetMenu(true);
            if (localPreview != null) localPreview.NetworkPreview = true;
            welcomed=true;
        }
        P.Actor InitialActor(string actorID, bool survivor, P.Pose spawn)
        {
            return P.Spawn(actorID, survivor ? NetworkKinds.Survivor : NetworkKinds.Hand, spawn, Context(true));
        }
        public bool TrySpawn(out P.Pose spawn)
        {
            Physics.SyncTransforms();
            var sites = World.Blocks.Where(b => b && b.gameObject.activeInHierarchy && b.position.y > 0 && !b.isKinematic && b.linearVelocity.sqrMagnitude < 1 && Vector3.Dot(b.rotation * Vector3.up, Vector3.up) > .97f).OrderByDescending(b => b.position.y);
            foreach (var rb in sites)
            {
                for (int lane = -1; lane <= 1; lane++)
                {
                    // The local survivor checks whether a body like it could stand there.
                    if (!(session.Player is ISurvivorSettings probe) || !probe.TryStandOn(rb, lane * .28f, out var p)) continue;
                    if (!session.PlayingHand && Vector3.Distance(session.Player.transform.position, p) < 1) continue;
                    if (connections.Any(c => c.actor != null && c.actor.kind == NetworkKinds.Survivor && Vector3.Distance(c.actor.position, p) < 1)) continue;
                    if (fusionActors.Values.Any(a => a.kind == NetworkKinds.Survivor && Vector3.Distance(a.position, p) < 1)) continue;
                    if (NetworkActors.Local.Any(a => NetworkActors.Alive(a) && a.NetworkKind == NetworkKinds.Survivor && a.NetworkActive && !IsOwnCharacter(a) && Vector3.Distance(a.NetworkPosition, p) < 1)) continue;
                    var heading = Vector3.ProjectOnPlane(World.Center - p, Vector3.up);
                    spawn = new P.Pose(p, heading.sqrMagnitude > .01f ? Quaternion.LookRotation(heading) : Quaternion.identity); return true;
                }
            }
            // No room on top: join as a flying hand, floating beside the map.
            spawn = new P.Pose(World.Center + new Vector3(0, Mathf.Max(5, World.TopHeight - World.Center.y + 3), -World.Radius - 10), Quaternion.identity);
            return false;
        }
        void LateUpdate()
        {
            if (!session || !NetworkActors.Alive(session.Player) || transport == null) return;
            float now = Time.unscaledTime;
            if (IsClient && receivedWorld && now >= nextPose)
            {
                nextPose = now + 1f / 30;
                var actor = LocalActor(); host.Send(P.Encode(P.Player, w => P.Write(w, actor)), true);
            }
            else if (!IsClient && connections.Any(c => c.id != null) && now >= nextState)
            {
                nextState = now + 1f / 15;
                var data = P.EncodeWorld(CaptureWorld());
                foreach (var c in connections) if (c.id != null) c.link.Send(data, true);
            }
        }
        // The character this machine's person is playing right now, read through Core.
        P.Actor LocalActor()
        {
            var who = (session.PlayingHand ? session.Hand : session.Player).gameObject.GetComponent<INetworkActor>();
            if (who != null) return P.Capture(id, who);
            var at = session.PlayingHand ? session.Hand.transform : session.Player.transform;
            return InitialActor(id, !session.PlayingHand, new P.Pose(at.position, at.rotation));
        }
        // Joined machines show the host's bots instead of running their own.
        void StopBots()
        {
            if (session.SurvivorBots != null) { session.SurvivorBots.Clear(); session.SurvivorBots.Running = false; }
            var hands = session.HandBots;
            if (hands != null) { hands.Clear(); hands.Running = false; }
        }
        void ResumeBots()
        {
            if (session.SurvivorBots != null) session.SurvivorBots.Running = true;
            if (session.HandBots != null) session.HandBots.Running = true;
        }
        bool IsOwnCharacter(INetworkActor a) => a is Component c && c && (c.gameObject == session.Player.gameObject || c.gameObject == session.Hand.gameObject);
        NetworkContext Context(bool host, string id = null) => new NetworkContext
        {
            host = host, localSurvivor = session.Player.gameObject, localHand = session.Hand.gameObject, actorId = id,
            sendImpact = (actor, push, damage) => GetComponent<CastleFusionSession>()?.QueueDisasterImpact(actor, push, damage)
        };
        // A joined player's character, as the host currently has it (a host-simulated hand sends its real bones).
        P.Actor Rebroadcast(string actorID, P.Actor stored) =>
            views.TryGetValue(actorID, out var view) && NetworkActors.Alive(view) ? P.Capture(actorID, view) : stored;
        P.Snapshot CaptureWorld()
        {
            var w = Synced; var blocks = World.Blocks;
            var s = new P.Snapshot { revision = w.Revision, settings = w.SaveLayout(), origin = new P.Pose(w.Root.position, w.Root.rotation), originScale = w.Root.localScale,
                blocks = new P.Pose[blocks.Count], active = new bool[blocks.Count] };
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i]; s.blocks[i] = b ? new P.Pose(b.position, b.rotation) : new P.Pose(Vector3.zero, Quaternion.identity);
                s.active[i] = b && b.gameObject.activeSelf;
            }
            var actors = new List<P.Actor> { LocalActor() };
            foreach (var c in connections) if (c.actor != null) actors.Add(Rebroadcast(c.id, c.actor));
            foreach (var pair in fusionActors) actors.Add(Rebroadcast(pair.Key, pair.Value));
            // Bots and AI hands simulated here.
            foreach (var local in NetworkActors.Local)
                if (NetworkActors.Alive(local) && !IsOwnCharacter(local) && local is Component part)
                    actors.Add(P.Capture("local:" + part.GetInstanceID(), local));
            s.actors = actors.ToArray(); return s;
        }
        void ApplyWorld(P.Snapshot s)
        {
            var w = Synced;
            bool rebuild = !receivedWorld || hostRevision != s.revision;
            if (rebuild)
            {
                if (!w.LoadLayout(s.settings, s.blocks.Length, new Pose(s.origin.position, s.origin.rotation), s.originScale))
                    throw new InvalidDataException("Demo layouts do not match");
                hostRevision = s.revision;
                w.SetReplica(true);
                previousBlocks = new P.Pose[World.Blocks.Count];
            }
            var blocks = World.Blocks;
            if (blocks.Count != s.blocks.Length) throw new InvalidDataException("Demo block count changed unexpectedly");
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                previousBlocks[i] = rebuild ? s.blocks[i] : new P.Pose(b.position, b.rotation);
                b.gameObject.SetActive(s.active[i]);
            }
            targetWorld = s; worldAt = lastWorldAt = Time.unscaledTime;
            var present = new HashSet<string>();
            foreach (var actor in s.actors) { present.Add(actor.id); if (actor.id != id) ShowActor(actor, false); }
            foreach (var key in views.Keys.Where(key => !present.Contains(key)).ToArray()) RemoveView(key);
            if (!receivedWorld)
            {
                receivedWorld = true; InterpolateWorld(); Physics.SyncTransforms();
                session.SetRole(spawnHand);
                if (spawnHand) session.Hand.Place(spawnPose.position, spawnPose.rotation);
                else session.Player.Place(spawnPose.position, spawnPose.rotation);
                Status = spawnHand ? "Joined as flying hand · no safe survivor spawn remained" : "Joined as survivor · live local demo";
                requested = null; session.SetMenu(false);
            }
        }
        void InterpolateWorld()
        {
            if (targetWorld == null) return;
            float t = Mathf.Clamp01((Time.unscaledTime - worldAt) * 15);
            for (int i = 0; i < targetWorld.blocks.Length; i++)
            {
                var b = World.Blocks[i]; var p = targetWorld.blocks[i];
                b.position = Vector3.Lerp(previousBlocks[i].position, p.position, t);
                b.rotation = Quaternion.Slerp(previousBlocks[i].rotation, p.rotation, t);
            }
        }
        void ShowActor(P.Actor actor, bool authoritative)
        {
            if (views.TryGetValue(actor.id, out var view) && (!NetworkActors.Alive(view) || view.NetworkKind != actor.kind)) { RemoveView(actor.id); view = null; }
            if (view == null)
            {
                var root = new GameObject("LAN player " + actor.id);
                view = NetworkActors.CreateView(actor.kind, root, Context(authoritative, actor.id));
                if (view == null) { Destroy(root); return; }
                views[actor.id] = view;
            }
            using (var reader = P.StateReader(actor)) view.ReadState(reader, actor.active);
            actor.position = view.NetworkPosition;
        }
        void RemoveView(string key) { if (views.TryGetValue(key, out var view) && view is Component c && c) { c.gameObject.SetActive(false); Destroy(c.gameObject); } views.Remove(key); }
        public void LeaveSession()
        {
            if (fusionMode) { GetComponent<CastleFusionSession>().Leave(); return; }
            Leave("Local session ended · continuing this demo locally.");
        }
        void Leave(string reason)
        {
            connectAttempt++; connecting = false; requested = null;
            host?.Dispose(); host = null;
            foreach (var c in connections) c.link.Dispose(); connections.Clear();
            foreach (var key in views.Keys.ToArray()) RemoveView(key);
            if (receivedWorld)
                Synced?.SetReplica(false);
            welcomed=false;receivedWorld = false; hostRevision = -1; targetWorld = null;
            if (localPreview != null) localPreview.NetworkPreview = false;
            ResumeBots();
            pending.Clear(); grants.Clear(); invitations.Clear(); Status = reason;
        }
        void OnDestroy()
        {
            connectAttempt++; transport?.Dispose();
            while (completions.TryDequeue(out var completion)) completion();
            foreach (var view in views.Values) if (view is Component c && c) Destroy(c.gameObject);
            if (!fusionMode) Application.runInBackground = oldRunInBackground;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (multicastLock != null) { multicastLock.Call("release"); multicastLock.Dispose(); }
#endif
        }
        public bool CanLeave => IsClient || HumanCount > 1 || requested != null || connecting;
        IslandRoomConnection Room => fusionMode ? IslandRoomConnection.Instance : null;
        public string RoomInfo => Room ? "Room: " + Room.Code + " \u00b7 " + Room.HumanCount + " players" : "";
        public string DiscoveryStatus => !Room ? "" : Room.DiscoveryStatus.Length > 0 ? Room.DiscoveryStatus : Room.Discovery?.Status ?? "Discovery starting";
        public bool CanToggleSharedControls => Room && Room.IsHost && MatchSetup.ModeId == "demo";
        public bool SharedControls => Room && Room.SharedDemoControls;
        public void ToggleSharedControls() { if (CanToggleSharedControls) Room.SetSharedControls(!Room.SharedDemoControls); }
        public bool CanInviteNearby => Room && Room.IsHost;
        public void InviteNearby() { if (CanInviteNearby) Room.InviteNearby(); }
        public string InvitationFrom => Room && Room.Discovery?.Invitation != null ? Room.Discovery.Invitation.message.name : "";
        public void DismissInvitation() => Room?.Discovery?.Dismiss();
    }
}

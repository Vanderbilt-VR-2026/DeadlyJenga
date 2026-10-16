using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastleBots;
using DeadlyJenga.Core;
using P = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-150)]
    public sealed class CastleLanSession : MonoBehaviour
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
        readonly Dictionary<string, CastleLanActorView> views = new Dictionary<string, CastleLanActorView>();
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
        CandyFlyingHandBridge localBridge;
        string displayName;
        bool oldRunInBackground;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject multicastLock;
#endif
        bool fusionMode, fusionClient;
        readonly Dictionary<string, P.Actor> fusionActors = new Dictionary<string, P.Actor>();
        public bool IsFusion => fusionMode;
        public bool IsClient => fusionMode ? fusionClient : host != null;
        public bool CanEditWorld => !IsClient;
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
        public bool HasPending => pending.Count > 0;

        public void Initialize(CastlePlaySession owner)
        {
            session = owner; localBridge = session.Hand.GetComponent<CandyFlyingHandBridge>();
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
            localBridge = session.Hand.GetComponent<CandyFlyingHandBridge>();
            Status = client ? "Waiting for host castle…" : "Hosting castle through Fusion";
            if (client)
            {
                session.Bots.ClearBots(); session.Bots.enabled = false;
                session.HandBots.ClearHands(); session.HandBots.enabled = false;
                Synced?.SetReplica(true);
                if (localBridge) localBridge.NetworkPreview = true;
                session.SetMenu(true);
            }
        }
        public byte[] FusionWelcome(string playerID)
        {
            bool survivor = TrySpawn(out var pose);
            fusionActors[playerID] = InitialActor(playerID, survivor, pose);
            return P.Encode(P.Welcome, w => { w.Write(!survivor); P.Write(w, pose); w.Write(MatchSetup.CaptureRules(session.Player)); });
        }
        public void FusionReceiveWelcome(byte[] data)
        {
            if (welcomed) return;
            using var reader = P.Reader(data, P.Welcome);
            reader.ReadBoolean(); P.ReadPose(reader);
            MatchSetup.ApplyRules(session.Player, P.Text(reader, 4096));
            ReceiveWelcome(data);
        }
        public byte[] FusionCaptureWorld() => P.EncodeWorld(CaptureWorld());
        public byte[] FusionCapturePlayer() => P.Encode(P.Player, w => P.Write(w, LocalActor()));
        public void FusionApplyWorld(byte[] data) => ApplyWorld(P.DecodeWorld(data));
        public void FusionReceivePlayer(string playerID, byte[] data)
        {
            using var reader = P.Reader(data, P.Player);
            var actor = P.ReadActor(reader); actor.id = playerID;
            if (actor.hand && localBridge) { actor.size = localBridge.settings.size; actor.left = localBridge.useLeftHand; }
            fusionActors[playerID] = actor;
            ShowActor(actor, true);
        }
        public void FusionRemovePlayer(string playerID) { fusionActors.Remove(playerID); RemoveView(playerID); }
        public void FusionSuspendPlayer(string playerID) { if (views.TryGetValue(playerID, out var view)) view.Suspend(); }
        public void FusionRender()
        {
            if (IsClient && receivedWorld) InterpolateWorld();
            foreach (var view in views.Values) if (view) view.Render(Time.unscaledDeltaTime);
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
                    while (host.Controls.TryDequeue(out var control)) ReceiveWelcome(control);
                    var state = welcomed ? host.TakeState() : null;
                    if (state != null) ApplyWorld(P.DecodeWorld(state));
                    if (host.Closed || now - lastWorldAt > 12) Leave("Host disconnected · continuing locally from the last received state.");
                }
                catch (Exception e) { Leave("Join ended: " + e.Message); }
            }
            if (IsClient && receivedWorld) InterpolateWorld();
            foreach (var view in views.Values) if (view) view.Render(Time.unscaledDeltaTime);
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
                    var state = c.link.TakeState();
                    if (state != null && c.id != null)
                    {
                        using var r = P.Reader(state, P.Player);
                        var actor = P.ReadActor(r); actor.id = c.id;
                        if (actor.hand && localBridge) { actor.size = localBridge.settings.size; actor.left = localBridge.useLeftHand; }
                        c.actor = actor; c.lastPose = Time.unscaledTime;
                        ShowActor(actor, true);
                    }
                }
                catch (Exception e) { Debug.LogWarning("LAN peer rejected: " + e.Message); c.link.Dispose(); }
                if (c.link.Closed || c.id == null && Time.unscaledTime - c.connected > 5 || Time.unscaledTime - c.lastPose > 12)
                {
                    c.link.Dispose(); connections.RemoveAt(i);
                    if (c.id != null) RemoveView(c.id);
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
            session.Bots.ClearBots(); session.Bots.enabled = false;
            session.HandBots.ClearHands(); session.HandBots.enabled = false;
            session.SetMenu(true);
            if (localBridge) localBridge.NetworkPreview = true;
            welcomed=true;
        }
        P.Actor InitialActor(string actorID, bool survivor, P.Pose spawn)
        {
            var a = new P.Actor { id = actorID, hand = !survivor, size = localBridge ? localBridge.settings.size : 20, left = localBridge && localBridge.useLeftHand };
            a.target = new CandyHandPose { position = spawn.position, rotation = spawn.rotation, tracked = true, skeleton = CandyHandSkeleton.Rest(a.left) };
            float scale = session.Player.playerScale;
            a.avatar = new CandyAvatarFrame { feet = spawn.position, head = spawn.position + Vector3.up * 1.6f * scale, headRotation = spawn.rotation, scale = scale,
                left = new CandyHandPose { position = spawn.position, rotation = Quaternion.identity }, right = new CandyHandPose { position = spawn.position, rotation = Quaternion.identity } };
            return a;
        }
        public bool TrySpawn(out P.Pose spawn)
        {
            Physics.SyncTransforms();
            var sites = World.Blocks.Where(b => b && b.gameObject.activeInHierarchy && b.position.y > 0 && !b.isKinematic && b.linearVelocity.sqrMagnitude < 1 && Vector3.Dot(b.rotation * Vector3.up, Vector3.up) > .97f).OrderByDescending(b => b.position.y);
            foreach (var rb in sites)
            {
                var box = rb.GetComponent<BoxCollider>(); if (!box) continue;
                for (int lane = -1; lane <= 1; lane++)
                {
                    var p = CastleBotNavigation.TopPoint(rb, box, lane * .28f);
                    if (!CastleBotNavigation.Landing(session.Player, p, out var hit)) continue;
                    p = hit.point + Vector3.up * .06f;
                    if (!session.PlayingHand && Vector3.Distance(session.Player.transform.position, p) < 1) continue;
                    if (connections.Any(c => c.actor != null && !c.actor.hand && Vector3.Distance(c.actor.avatar.feet, p) < 1)) continue;
                    if (fusionActors.Values.Any(a => !a.hand && Vector3.Distance(a.avatar.feet, p) < 1)) continue;
                    if (session.Bots.Bots.Any(b => b && !b.Respawning && Vector3.Distance(b.transform.position, p) < 1)) continue;
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
            if (!session || !session.Player || transport == null) return;
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
        P.Actor LocalActor()
        {
            if (session.PlayingHand && localBridge && localBridge.PhysicalHand)
                return CaptureHand(id, localBridge.PhysicalHand);
            if (session.PlayingHand) return InitialActor(id, false, new P.Pose(session.Hand.motor.transform.position, session.Hand.motor.transform.rotation));
            var avatar = session.Player.GetComponent<CandyPlayerAvatar>();
            return new P.Actor { id = id, avatar = avatar.ReadFrame(), color = avatar.ColorIndex };
        }
        public static P.Actor CaptureHand(string actorID, CandyPhysicalHand hand)
        {
            var a = new P.Actor { id = actorID, hand = true, left = hand.left, size = hand.GeometrySize, target = hand.Target,
                active = hand.gameObject.activeInHierarchy && hand.skin && hand.skin.enabled, bones = new P.Pose[hand.bodies.Length] };
            for (int i = 0; i < a.bones.Length; i++) a.bones[i] = new P.Pose(hand.bodies[i].position, hand.bodies[i].rotation);
            return a;
        }
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
            foreach (var c in connections)
            {
                if (c.actor == null) continue;
                if (c.actor.hand && views.TryGetValue(c.id, out var v) && v.Hand) actors.Add(CaptureHand(c.id, v.Hand));
                else actors.Add(c.actor);
            }
            foreach (var pair in fusionActors)
            {
                if (pair.Value.hand && views.TryGetValue(pair.Key, out var view) && view.Hand)
                    actors.Add(CaptureHand(pair.Key, view.Hand));
                else actors.Add(pair.Value);
            }
            foreach (var bot in session.Bots.Bots)
            {
                if (!bot) continue;
                var a = bot.GetComponent<CandyPlayerAvatar>();
                actors.Add(new P.Actor { id = "bot:" + bot.GetInstanceID(), avatar = a.ReadFrame(), color = a.ColorIndex, active = !bot.Respawning });
            }
            foreach (var bot in session.HandBots.Hands) if (bot && bot.Bridge.PhysicalHand) actors.Add(CaptureHand("bot-hand:" + bot.GetInstanceID(), bot.Bridge.PhysicalHand));
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
                else session.Player.Teleport(spawnPose.position, spawnPose.rotation);
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
            if (!views.TryGetValue(actor.id, out var view))
            {
                view = new GameObject("LAN player " + actor.id).AddComponent<CastleLanActorView>();
                view.Initialize(session, authoritative); views.Add(actor.id, view);
            }
            view.Receive(actor);
        }
        void RemoveView(string key) { if (views.TryGetValue(key, out var view) && view) { view.gameObject.SetActive(false); Destroy(view.gameObject); } views.Remove(key); }
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
            if (localBridge) localBridge.NetworkPreview = false;
            session.Bots.enabled = true; session.HandBots.enabled = true;
            pending.Clear(); grants.Clear(); invitations.Clear(); Status = reason;
        }
        void OnDestroy()
        {
            connectAttempt++; transport?.Dispose();
            while (completions.TryDequeue(out var completion)) completion();
            foreach (var view in views.Values) if (view) Destroy(view.gameObject);
            if (!fusionMode) Application.runInBackground = oldRunInBackground;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (multicastLock != null) { multicastLock.Call("release"); multicastLock.Dispose(); }
#endif
        }
        public void DrawMenu()
        {
            if (fusionMode) { GUILayout.Label(Status); if (GUILayout.Button(IsClient ? "Leave game" : "Close game for everyone", GUILayout.Height(32))) LeaveSession(); return; }
            GUILayout.Space(8); GUILayout.Label("LOCAL MULTIPLAYER · same Wi-Fi"); GUILayout.Label(Status);
            if (!IsClient)
            {
                if (GUILayout.Button("Nearby: " + SelectedName + " (next)", GUILayout.Height(30))) NextNearby();
                if (GUILayout.Button("Request to join selected demo", GUILayout.Height(30))) RequestSelected();
                if (GUILayout.Button("Invite selected demo here", GUILayout.Height(30))) InviteSelected();
                GUILayout.Label(PendingLabel);
                if (HasPending)
                {
                    if (GUILayout.Button("Accept", GUILayout.Height(30))) AcceptPending();
                    if (GUILayout.Button("Decline", GUILayout.Height(30))) DeclinePending();
                }
            }
            if (IsClient || HumanCount > 1 || requested != null || connecting)
                if (GUILayout.Button(IsClient ? "Leave local session" : "End / cancel local session", GUILayout.Height(30))) LeaveSession();
        }
    }
}

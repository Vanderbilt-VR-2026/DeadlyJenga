#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using DeadlyJenga.Core;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A shadow channel for measuring Fusion state delivery without changing gameplay.
    public sealed class CastleMotionStateProbe : NetworkBehaviour
    {
        internal const uint PrefabNumber = 100000;
        public static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--dj-motion-state-probe") >= 0;
        public static CastleMotionStateProbe Local { get; private set; }

        [Networked] public int Sequence { get; private set; }
        [Networked] public Tick CaptureTick { get; private set; }
        [Networked] public int BlockIndex { get; private set; }
        [Networked] public Vector3 Human { get; private set; }
        [Networked] public Vector3 Bot { get; private set; }
        [Networked] public Vector3 Block { get; private set; }
        [Networked] public Quaternion BlockRotation { get; private set; }



        [Networked] public int WorldRevision { get; private set; }
        [Networked] public int ActorBytes { get; private set; }
        [Networked, Capacity(1024)] NetworkArray<int> ActorWords => default;
        readonly byte[] incoming = new byte[4096];
        int lastApplied;
        public static int SelectedBlock { get; set; } = -1;
        public static bool ApplyEnabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--dj-motion-state-apply") >= 0;

        void CaptureActors(CastlePlaySession session)
        {
            var data = session.Network.CaptureProbeMotionActors();
            if (data.Length > incoming.Length) { ActorBytes = 0; return; }
            var words = ActorWords;
            int count = (data.Length + 3) / 4;
            for (int index = 0; index < count; index++)
            {
                int start = index * 4;
                int value = 0;
                for (int i = 0; i < 4 && start + i < data.Length; i++)
                    value |= data[start + i] << (8 * i);
                words.Set(index, value);
            }
            ActorBytes = data.Length;
        }

        public override void Render()
        {
            if (!ApplyEnabled || HasStateAuthority || Sequence == 0 || unchecked(Sequence - lastApplied) <= 0 ||
                Services.Session is not CastlePlaySession session || !session.Network) return;
            int bytes = ActorBytes;
            if (bytes <= 0 || bytes > incoming.Length) return;
            var words = ActorWords;
            for (int i = 0; i < bytes; i++)
                incoming[i] = (byte)(words[i / 4] >> (8 * (i % 4)));
            session.Network.ApplyProbeMotion(WorldRevision, incoming, bytes, BlockIndex, Block, BlockRotation);
            lastApplied = Sequence;
        }

        public static System.Threading.Tasks.Task<StartGameResult> ConnectDirect(
            NetworkRunner runner, GameObject holder, bool host, string code)
        {
            if (Enabled) holder.AddComponent<CastleMotionStateProbeProvider>();
            var app = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
            app.FixedRegion = "us";
            app.AppVersion += "/motion-probe";
            return runner.StartGame(new StartGameArgs
            {
                GameMode = host ? GameMode.Host : GameMode.Client,
                SessionName = code,
                PlayerCount = 2,
                IsVisible = false,
                EnableClientSessionCreation = false,
                CustomPhotonAppSettings = app,
                Config = IslandRoomConnection.MigrationConfig(),
                SceneManager = holder.AddComponent<NetworkSceneManagerDefault>()
            });
        }

        public static CastleMotionStateProbe StartHost(NetworkRunner runner)
        {
            if (!Enabled || !runner || !runner.IsServer) return null;
            var instance = runner.Spawn(NetworkPrefabId.FromRaw(PrefabNumber));
            return instance ? instance.GetComponent<CastleMotionStateProbe>() : null;
        }

        public override void Spawned() => Local = this;

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this) Local = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Services.Session is not CastlePlaySession session ||
                Services.World is not { } world || world.Blocks.Count == 0 ||
                session.Player is not Component player) return;
            var bot = session.SurvivorBots?.Members.FirstOrDefault();
            int index = SelectedBlock >= 0 && SelectedBlock < world.Blocks.Count ? SelectedBlock : world.Blocks.Count / 2;
            var block = world.Blocks[index];
            Human = player.transform.position;
            Bot = bot ? bot.transform.position : Vector3.zero;
            BlockIndex = index;
            Block = block ? block.position : Vector3.zero;
            BlockRotation = block ? block.rotation : Quaternion.identity;
            if (ApplyEnabled && session.Network && world is ISyncedWorld synced)
            {
                WorldRevision = synced.Revision;
                CaptureActors(session);
            }
            CaptureTick = Runner.Tick;
            Sequence++;
        }
    }

    internal sealed class CastleMotionStateProbeProvider : NetworkObjectProviderDefault
    {
        readonly NetworkObjectBaker baker = new NetworkObjectBaker();

        public override NetworkObjectAcquireResult AcquireInstance(NetworkRunner runner,
            in NetworkObjectAcquireContext context, out NetworkObject instance)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleMotionStateProbe.PrefabNumber)
                return base.AcquireInstance(runner, context, out instance);
            var root = new GameObject("Shadow motion measurement");
            instance = root.AddComponent<NetworkObject>();
            root.AddComponent<CastleMotionStateProbe>();
            baker.Bake(root);
            if (context.DontDestroyOnLoad) runner.MakeDontDestroyOnLoad(root);
            else runner.MoveToRunnerScene(root);
            return NetworkObjectAcquireResult.Success;
        }

        public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleMotionStateProbe.PrefabNumber)
            {
                base.ReleaseInstance(runner, context);
                return;
            }
            if (!context.IsBeingDestroyed && context.Object) Destroy(context.Object.gameObject);
        }
    }
}
#endif

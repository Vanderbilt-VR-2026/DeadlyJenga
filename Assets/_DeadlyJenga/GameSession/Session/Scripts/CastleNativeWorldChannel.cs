using System;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Development transport candidate: native atomic snapshots, with the existing world decoder.
    public sealed class CastleNativeWorldChannel : NetworkBehaviour
    {
        public const int CapacityBytes = 24576;
        internal static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--dj-native-world") >= 0;
        internal static readonly NetworkObjectGuid PrefabGuid = NetworkObjectGuid.Parse("3c2e37ad019b4c8d926075abda6049b9");
        static NetworkObject template;
        internal static void PrepareConfig(NetworkProjectConfig config)
        {
            if(!Enabled)return;
            // Native state uses the game channel; reliable streams have a separate budget.
            config.Network.GameChannelSendCount=16;
            if(!template)
            {
                var root=new GameObject("Native world transport template");
                template=root.AddComponent<NetworkObject>();root.AddComponent<CastleNativeWorldChannel>();
                new NetworkObjectBaker().Bake(root);DontDestroyOnLoad(root);
            }
            if(config.PrefabTable.GetSource(PrefabGuid)==null)
                config.PrefabTable.AddSource(new NetworkPrefabSourceStatic{Object=template,AssetGuid=PrefabGuid});
        }
        [Networked] public int Sequence { get; set; }
        [Networked] public int Scope { get; set; }
        [Networked] public int Length { get; set; }
        [Networked, Capacity(CapacityBytes / 8)] public NetworkArray<ulong> Bytes => default;
        byte[] pending;
        int pendingScope, received;
        static readonly Unity.Profiling.ProfilerMarker PublishMarker = new("DJ.Network.NativeWorld.Publish");
        static readonly Unity.Profiling.ProfilerMarker ReceiveMarker = new("DJ.Network.NativeWorld.Receive");
        public static long PublishedBytes { get; private set; }
        public static long PublishedFrames { get; private set; }
        public static long ReceivedFrames { get; private set; }
        public static long RawPublishedBytes { get; internal set; }
        public static long FallbackFrames { get; internal set; }

        internal void Publish(byte[] bytes, int scope) { pending = bytes; pendingScope = scope; }
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || pending == null) return;
            using var measurement = PublishMarker.Auto();
            var data = pending; pending = null;
            int words = (data.Length + 7) / 8;
            for (int i = 0; i < words; i++)
            {
                ulong value = 0;
                for (int b = 0; b < 8 && i * 8 + b < data.Length; b++) value |= (ulong)data[i * 8 + b] << (b * 8);
                if (Bytes[i] != value) Bytes.Set(i, value);
            }
            Length = data.Length; Scope = pendingScope;
            Sequence = unchecked(Sequence + 1); if (Sequence == 0) Sequence = 1;
            PublishedBytes += data.Length; PublishedFrames++;
        }
        public override void Render()
        {
            if (Runner.IsServer || Sequence == 0 || received == Sequence) return;
            var owner = Runner.GetComponent<CastleNativeWorldSetup>();
            if (!owner || owner.Scope != Scope || owner.Receive == null) return;
            using var measurement = ReceiveMarker.Auto();
            if (Length < 0 || Length > CapacityBytes) throw new InvalidOperationException("Native world length invalid");
            var data = new byte[Length];
            for (int i = 0; i < data.Length; i += 8)
            {
                ulong word = Bytes[i / 8];
                for (int b = 0; b < 8 && i + b < data.Length; b++) data[i + b] = (byte)(word >> (b * 8));
            }
            if (!owner.Receive(data.Length==0?data:WorldDeltaCodec.UnpackNative(data))) return;
            received = Sequence; ReceivedFrames++;
        }
        public override void Spawned()
        {
            var owner = Runner.GetComponent<CastleNativeWorldSetup>();
            if (owner) owner.Channel = this;
        }
        internal static CastleNativeWorldSetup Bind(NetworkRunner runner, int scope, Func<byte[], bool> receive)
        {
            var setup = runner.GetComponent<CastleNativeWorldSetup>();
            if (!setup)
            {
                setup = runner.gameObject.AddComponent<CastleNativeWorldSetup>();
                setup.Initialize(runner);
            }
            setup.Scope = scope; setup.Receive = receive;
            return setup;
        }
    }

    internal sealed class CastleNativeWorldSetup : MonoBehaviour
    {
        internal int Scope;
        internal Func<byte[], bool> Receive;
        internal CastleNativeWorldChannel Channel;
        NetworkRunner runner;
        NetworkPrefabId prefab;
        bool spawned;
        internal void Initialize(NetworkRunner value)
        {
            runner = value;
            prefab=runner.Prefabs.GetId(CastleNativeWorldChannel.PrefabGuid);
            if(!prefab.IsValid)throw new InvalidOperationException("Native world prefab was not registered before connection");
        }
        internal bool Publish(byte[] data)
        {
            if(!Channel&&!spawned&&runner&&runner.IsRunning&&runner.IsServer)
            {spawned=true;runner.Spawn(prefab,flags:NetworkSpawnFlags.DontDestroyOnLoad);}
            if (!Channel || !Channel.Object || !Channel.Object.HasStateAuthority) return false;
            var packet=WorldDeltaCodec.PackNative(data);
            if(packet.Length > CastleNativeWorldChannel.CapacityBytes)
            {CastleNativeWorldChannel.FallbackFrames++;Channel.Publish(Array.Empty<byte>(),Scope);return false;}
            CastleNativeWorldChannel.RawPublishedBytes+=data.Length;
            Channel.Publish(packet, Scope); return true;
        }
    }
}

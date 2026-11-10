using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Independent pages keep busy block batches from delaying player samples.
    public sealed class CastleMotionPage : NetworkBehaviour
    {
        internal const uint PrefabNumber = 100001;
        internal const int ByteCapacity = 1792;
        internal const int LobbyBroadcastLane = -1000000, CheckpointLane = -2000000;
        [Networked, Capacity(ByteCapacity / 4)] NetworkArray<int> Words => default;
        [Networked] public int Lane { get; set; }
        [Networked] int SceneEpoch { get; set; }
        [Networked] int WorldRevision { get; set; }
        [Networked] int SampleTick { get; set; }
        [Networked] int Length { get; set; }
        [Networked] int Sequence { get; set; }

        readonly byte[] receiveBytes = new byte[ByteCapacity];
        CastleMotionProvider provider;
        int lastSequence;

        public override void Spawned() => provider = Runner.GetComponent<CastleMotionProvider>();

        internal bool Publish(int scene, int revision, int tick, byte[] source, int length)
        {
            bool changed = SceneEpoch != scene || WorldRevision != revision || Length != length;
            var words = Words;
            for (int start = 0; start < length; start += 4)
            {
                int value = 0;
                for (int i = 0; i < 4 && start + i < length; i++)
                    value |= source[start + i] << (8 * i);
                if (words[start / 4] == value) continue;
                words.Set(start / 4, value);
                changed = true;
            }
            if (!changed && Lane >= 0 && unchecked(tick - SampleTick) < 15) return false;
            SceneEpoch = scene;
            WorldRevision = revision;
            SampleTick = tick;
            Length = length;
            Sequence++;
            return true;
        }

        public override void FixedUpdateNetwork()
        {
            if (Lane == 0 && HasStateAuthority && Runner.IsForward && provider && provider.Session)
                provider.Session.ReadMotionInput(Runner);
        }

        public override void Render()
        {
            if (HasStateAuthority || !provider || Sequence == lastSequence ||
                Length <= 0 || Length > receiveBytes.Length) return;
            var lobby = Lane < 0 ? IslandRoomConnection.Instance : null;
            bool roomBroadcast = Lane <= LobbyBroadcastLane;
            if (Lane < 0)
            {
                if (!lobby) return;
                if (roomBroadcast)
                {
                    if (!lobby.CanReadRoomPage(Runner, Lane)) return;
                }
                else if ((~Lane >> 5) != Runner.LocalPlayer.RawEncoded || !lobby.CanReadLobbyArrival(Runner)) return;
            }
            else if (!provider.Session || !provider.Session.CanReadMotion(Runner, SceneEpoch, WorldRevision)) return;

            int length = Length;
            var words = Words;
            for (int start = 0; start < length; start += 4)
            {
                int value = words[start / 4];
                for (int i = 0; i < 4 && start + i < length; i++)
                    receiveBytes[start + i] = (byte)(value >> (8 * i));
            }
            if (lobby)
            {
                var bytes=receiveBytes.AsSpan(0,length);
                if (roomBroadcast)
                {
                    if (!lobby.ReadRoomPage(Runner,Lane,SampleTick,bytes)) return;
                }
                else if (!lobby.ReadLobbyArrival(Runner, SampleTick, ~Lane & 31, bytes)) return;
            }
            else provider.Session.ReadMotionPage(SampleTick, Runner.DeltaTime, receiveBytes, length);
            lastSequence = Sequence;
        }
    }

    // Shared room records can span several independently delivered Fusion pages.
    internal sealed class RoomControlPages
    {
        const int ChunkBytes=CastleMotionPage.ByteCapacity-1, MaxChunks=19;
        readonly List<CastleMotionPage> pages=new List<CastleMotionPage>(2);
        int serial,received;
        byte[][] pieces;
        internal void Reset(){pieces=null;serial=received=0;}
        internal void Retire(NetworkRunner runner)
        {
            if(runner&&runner.IsRunning&&runner.IsServer)
                foreach(var page in pages)
                    if(page&&page.Object&&page.Object.IsValid)runner.Despawn(page.Object);
            pages.Clear();
        }
        internal bool Publish(NetworkRunner runner,int lane,int id,byte[] payload)
        {
            int count=(payload.Length+ChunkBytes-1)/ChunkBytes;
            if(count<1||count>MaxChunks)return false;
            for(int i=0;i<count;i++)
            {
                if(i==pages.Count)pages.Add(null);
                var page=pages[i];
                if(!page||!page.Object||!page.Object.IsValid)
                {
                    int slot=lane-i;
                    var obj=runner.Spawn(NetworkPrefabId.FromRaw(CastleMotionPage.PrefabNumber),
                        onBeforeSpawned:(_,instance)=>instance.GetComponent<CastleMotionPage>().Lane=slot);
                    if(!obj){Retire(runner);return false;}
                    pages[i]=page=obj.GetComponent<CastleMotionPage>();
                }
                int offset=i*ChunkBytes,length=Math.Min(payload.Length-offset,ChunkBytes);
                var piece=new byte[length+1];piece[0]=(byte)count;
                Buffer.BlockCopy(payload,offset,piece,1,length);
                page.Publish(0,0,id,piece,piece.Length);
            }
            return true;
        }
        internal byte[] Accept(int id,int index,ReadOnlySpan<byte> data,int maximum)
        {
            if(data.Length<2||data[0]<1||data[0]>MaxChunks||index<0||index>=data[0]||
                index<data[0]-1&&data.Length!=CastleMotionPage.ByteCapacity)
                throw new InvalidDataException("Invalid room control page.");
            if(serial!=id)
            {
                if(serial!=0&&unchecked(id-serial)<=0)return null;
                serial=id;pieces=new byte[data[0]][];received=0;
            }
            if(pieces==null)return null;
            if(pieces.Length!=data[0])throw new InvalidDataException("Room page count changed.");
            if(pieces[index]==null){pieces[index]=data.Slice(1).ToArray();received++;}
            if(received!=pieces.Length)return null;
            int length=(pieces.Length-1)*ChunkBytes+pieces[pieces.Length-1].Length;
            if(length>maximum)throw new InvalidDataException("Room control packet exceeds its limit.");
            var packet=new byte[length];
            for(int i=0;i<pieces.Length;i++)pieces[i].CopyTo(packet,i*ChunkBytes);
            pieces=null;
            return packet;
        }
    }

    internal sealed class CastleMotionProvider : NetworkObjectProviderDefault
    {
        internal CastleFusionSession Session;
        readonly NetworkObjectBaker baker = new NetworkObjectBaker();

        public override NetworkObjectAcquireResult AcquireInstance(NetworkRunner runner,
            in NetworkObjectAcquireContext context, out NetworkObject instance)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleMotionPage.PrefabNumber)
                return base.AcquireInstance(runner, context, out instance);
            var root = new GameObject("Castle motion page");
            instance = root.AddComponent<NetworkObject>();
            root.AddComponent<CastleMotionPage>();
            baker.Bake(root);
            if (context.DontDestroyOnLoad) runner.MakeDontDestroyOnLoad(root);
            else runner.MoveToRunnerScene(root);
            return NetworkObjectAcquireResult.Success;
        }

        public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleMotionPage.PrefabNumber)
                base.ReleaseInstance(runner, context);
            else if (!context.IsBeingDestroyed && context.Object)
                Destroy(context.Object.gameObject);
        }
    }
}

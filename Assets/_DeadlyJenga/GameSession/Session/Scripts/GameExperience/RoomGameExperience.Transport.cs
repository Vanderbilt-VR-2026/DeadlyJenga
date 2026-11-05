using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class RoomGameExperience
    {
        const int ExperienceChannel=0x47455850, PacketLimit=16384;
        static readonly UTF8Encoding WireEncoding=new UTF8Encoding(false,true);
        [Serializable] sealed class ExperienceCheckpoint
        {public string body="",signature="",modulus="",exponent="";}
        [Serializable] sealed class ExperiencePacket
        {
            public int version=1,sequence;
            public string kind="",epoch="",roomId="",digest="",challenge="";
            public GameExperienceCommand command;
            public ExperienceCheckpoint checkpoint;
        }
        sealed class CachedCheckpoint
        {public ExperienceCheckpoint wire;public GameFlowState state;public string digest;}
        sealed class PeerReceipt
        {public int sequence;public double lastCommand=-100;public string digest="";}
        readonly Dictionary<string,PeerReceipt> transportPeers=new Dictionary<string,PeerReceipt>(StringComparer.Ordinal);
        readonly Dictionary<string,CachedCheckpoint> transportSent=new Dictionary<string,CachedCheckpoint>(StringComparer.Ordinal);
        readonly Queue<GameExperienceCommand> transportCommands=new Queue<GameExperienceCommand>();
        readonly Dictionary<int,string> recoveryDigests=new Dictionary<int,string>();
        readonly HashSet<string> recoveryResponses=new HashSet<string>(StringComparer.Ordinal);
        NetworkRunner transportRunner,transportFencedRunner;
        RSA transportSigner;
        string transportEpoch="",transportPinnedKey="",transportPriorKey="",transportChallenge="",transportSemantic="";
        CachedCheckpoint transportCache,recoveryBest;
        bool transportRecovering,transportMigration,transportFailed;
        int transportSequence;
        double transportLastPublish=-100,transportLastSend=-100,transportBoundAt;
        internal bool TransportRecovering=>transportRecovering||room&&room.Migrating;
        internal string TransportStatus{get;private set;}="";
        double TransportNow=>Time.realtimeSinceStartupAsDouble;

        internal void TransportBind(IslandRoomConnection owner)
        {transportRecovering=owner.Connected&&!owner.IsHost;if(owner.Connected)TransportAfterMigration(owner.Runner);}
        void TransportFailure(string reason)
        {transportFailed=true;TransportStatus=reason;transportRecovering=room&&room.Connected;}
        bool CreateSigner()
        {
            try{transportSigner?.Dispose();transportSigner=RSA.Create();transportSigner.KeySize=2048;return true;}
            catch(Exception){transportSigner=null;TransportFailure("Room checkpoint signing unavailable; Game is disabled on this platform.");return false;}
        }
        internal void TransportBeforeMigration(NetworkRunner oldRunner)
        {
            if(!ReferenceEquals(oldRunner,transportRunner))return;
            transportFencedRunner=oldRunner;transportMigration=true;transportRecovering=true;transportPriorKey=transportPinnedKey;
            transportCommands.Clear();transportPeers.Clear();transportSent.Clear();transportRunner=null;
            transportSigner?.Dispose();transportSigner=null;
        }
        internal void TransportAfterMigration(NetworkRunner next)
        {
            if(next==null||ReferenceEquals(next,transportRunner)||ReferenceEquals(next,transportFencedRunner))return;
            if(transportRunner!=null)TransportBeforeMigration(transportRunner);
            transportRunner=next;transportEpoch="";transportBoundAt=TransportNow;transportLastSend=-100;
            transportPinnedKey="";transportFailed=false;TransportStatus="";transportSequence=0;
            transportCommands.Clear();transportPeers.Clear();transportSent.Clear();transportSemantic="";
            transportRecovering=!next.IsServer||transportMigration;
            if(next.IsServer)
            {
                transportEpoch=Guid.NewGuid().ToString("N");
                if(!CreateSigner())return;
                if(transportMigration)
                {
                    if(transportCache==null||string.IsNullOrEmpty(transportPriorKey))
                    {TransportFailure("No authenticated pre-migration checkpoint; leave and rejoin the room.");return;}
                    recoveryBest=transportCache;recoveryResponses.Clear();recoveryDigests.Clear();recoveryDigests[transportCache.state.revision]=transportCache.digest;transportChallenge=Guid.NewGuid().ToString("N");
                }
                else TransportPublish();
            }
        }
        internal void TransportDisconnected()
        {
            transportSigner?.Dispose();transportSigner=null;transportRunner=null;
            transportCommands.Clear();transportPeers.Clear();transportSent.Clear();
            if(room&&room.Migrating){transportMigration=true;transportRecovering=true;return;}
            transportFencedRunner=null;transportCache=null;transportPinnedKey="";transportPriorKey="";transportMigration=false;
            transportRecovering=false;transportFailed=false;TransportStatus="";ResetRoomFlow();
        }
        internal void TransportTick()
        {
            if(!room||!room.Connected)return;
            if(!ReferenceEquals(room.Runner,transportRunner))TransportAfterMigration(room.Runner);
            if(transportFailed)return;
            if(room.IsHost)
            {
                var active=new HashSet<string>(StringComparer.Ordinal);
                foreach(var peer in RemotePlayers())if(room.GameExperienceMemberId(transportRunner,peer,out var id))active.Add(id);
                foreach(var id in transportPeers.Keys.Where(id=>!active.Contains(id)).ToArray())transportPeers.Remove(id);
            }
            if(transportRecovering&&TransportNow-transportBoundAt>8)
            {TransportFailure("Room checkpoint recovery timed out; leave and rejoin the room.");return;}
            if(room.IsHost&&transportMigration)
            {
                var peers=RemotePlayers().ToArray();
                if(peers.All(p=>room.GameExperienceMemberId(transportRunner,p,out var id)&&recoveryResponses.Contains(id)))
                {
                    if(recoveryBest==null||!ValidateAndApplySnapshot(recoveryBest.state))
                    {TransportFailure("Recovered profile or checkpoint is unavailable.");return;}
                    transportRecovering=false;transportMigration=false;transportCache=recoveryBest;
                    OnTransportRecoveryComplete();return;
                }
                if(TransportNow-transportLastSend>=1)
                {transportLastSend=TransportNow;foreach(var peer in peers)Send(peer,new ExperiencePacket{kind="recover",epoch=transportEpoch,challenge=transportChallenge});}
            }
            else if(!room.IsHost&&TransportNow-transportLastSend>=1)
            {
                transportLastSend=TransportNow;
                if(transportRecovering)Send(default,new ExperiencePacket{kind="hello"});
                else if(transportCommands.Count>0)Send(default,new ExperiencePacket{kind="command",epoch=transportEpoch,roomId=transportCache.state.roomId,sequence=transportSequence+1,command=transportCommands.Peek()});
            }
        }
        IEnumerable<PlayerRef> RemotePlayers()=>transportRunner.ActivePlayers.Where(p=>p!=transportRunner.LocalPlayer);
        void Send(PlayerRef target,ExperiencePacket packet)
        {
            if(transportRunner==null||!room.Connected||!ReferenceEquals(room.Runner,transportRunner))return;
            try
            {
                var bytes=WireEncoding.GetBytes(JsonUtility.ToJson(packet));if(bytes.Length>PacketLimit){TransportFailure("Room checkpoint exceeds the packet limit.");return;}
                var key=ReliableKey.FromInts(ExperienceChannel,1,0,0);
                if(transportRunner.IsServer)transportRunner.SendReliableDataToPlayer(target,key,bytes);
                else transportRunner.SendReliableDataToServer(key,bytes);
            }
            catch(Exception){TransportFailure("Room checkpoint send failed; Game is paused.");}
        }
        static string CheckpointKey(ExperienceCheckpoint c)=>c.modulus+":"+c.exponent;
        static string Digest(string body)
        {using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(WireEncoding.GetBytes(body)));}
        static CachedCheckpoint Verify(ExperienceCheckpoint checkpoint,string requiredKey=null)
        {
            if(checkpoint==null||string.IsNullOrEmpty(checkpoint.body)||checkpoint.body.Length>10000||
                checkpoint.modulus==null||checkpoint.modulus.Length>344||checkpoint.exponent==null||checkpoint.exponent.Length>12||
                checkpoint.signature==null||checkpoint.signature.Length>344)return null;
            if(requiredKey!=null&&CheckpointKey(checkpoint)!=requiredKey)return null;
            var modulus=Convert.FromBase64String(checkpoint.modulus);var exponent=Convert.FromBase64String(checkpoint.exponent);
            if(modulus.Length!=256||exponent.Length<1||exponent.Length>8)return null;
            using(var rsa=RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters{Modulus=modulus,Exponent=exponent});
                if(!rsa.VerifyData(WireEncoding.GetBytes(checkpoint.body),Convert.FromBase64String(checkpoint.signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))return null;
            }
            var state=JsonUtility.FromJson<GameFlowState>(checkpoint.body);RoomGameFlow.Validate(state);
            return new CachedCheckpoint{wire=checkpoint,state=state,digest=Digest(checkpoint.body)};
        }
        static string Semantic(GameFlowState state)
        {
            var copy=new RoomGameFlow(state).State;copy.remaining=0;copy.revision=0;
            return JsonUtility.ToJson(copy);
        }
        internal void TransportPublish()
        {
            if(!room||!IsAuthority||TransportRecovering||transportFailed)return;
            if(!room.Connected)return;
            if(transportRunner==null||transportSigner==null)return;
            var state=CaptureSnapshot();var semantic=Semantic(state);
            if(semantic==transportSemantic&&TransportNow-transportLastPublish<1)return;
            try
            {
                string body=JsonUtility.ToJson(state);var key=transportSigner.ExportParameters(false);
                var checkpoint=new ExperienceCheckpoint{body=body,modulus=Convert.ToBase64String(key.Modulus),exponent=Convert.ToBase64String(key.Exponent),
                    signature=Convert.ToBase64String(transportSigner.SignData(WireEncoding.GetBytes(body),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))};
                var cached=Verify(checkpoint);if(cached==null)throw new InvalidOperationException();
                transportCache=cached;transportPinnedKey=CheckpointKey(checkpoint);transportSemantic=semantic;transportLastPublish=TransportNow;
                if(transportSent.Count>=32)transportSent.Remove(transportSent.Keys.First());transportSent[cached.digest]=cached;
                foreach(var peer in RemotePlayers())SendSnapshot(peer);
            }
            catch(Exception){TransportFailure("Room checkpoint signing failed; Game is disabled.");}
        }
        void SendSnapshot(PlayerRef peer)
        {if(transportCache!=null)Send(peer,new ExperiencePacket{kind="state",epoch=transportEpoch,checkpoint=transportCache.wire});}
        internal bool TransportCheckpointReady(GameFlowState state)
        {
            if(TransportRecovering||transportFailed)return false;
            if(!room.Connected)return IsAuthority;
            if(!room.IsHost||transportCache==null||Semantic(state)!=Semantic(transportCache.state))return false;
            foreach(var peer in RemotePlayers())
            {
                if(!room.GameExperienceMemberId(transportRunner,peer,out var id)||!transportPeers.TryGetValue(id,out var receipt)||
                    !transportSent.TryGetValue(receipt.digest,out var cached)||Semantic(cached.state)!=Semantic(state))return false;
            }
            return true;
        }
        internal void TransportSubmit(GameExperienceCommand command)
        {
            if(!CommandValid(command)||TransportRecovering||transportFailed)return;
            if(IsAuthority){HandleAuthoritativeCommand(room.GameExperienceLocalId,command);return;}
            if(transportCache==null||transportCommands.Count>=8)return;
            transportCommands.Enqueue(command);transportLastSend=-100;
        }
        static bool CommandValid(GameExperienceCommand c)
        {
            if(c==null||c.value==null||c.value.Length>128||c.authorityTerm<1||c.selectionVersion<0||c.rosterVersion<0)return false;
            switch(c.verb){case "experience":case "automatic":case "ready":case "start":case "cancel":case "retry":case "return":case "rematch":return true;default:return false;}
        }
        internal bool TransportReceive(NetworkRunner runner,PlayerRef sender,ReliableKey key,ReadOnlySpan<byte> bytes)
        {
            key.GetInts(out int channel,out int kind,out int ignored,out int unused);if(channel!=ExperienceChannel)return false;
            if(ReferenceEquals(runner,transportFencedRunner)||kind!=1||bytes.Length<2||bytes.Length>PacketLimit||!room||!room.Connected||!ReferenceEquals(runner,room.Runner))return true;
            if(!ReferenceEquals(runner,transportRunner))TransportAfterMigration(runner);
            if(transportFailed)return true;
            try
            {
                var packet=JsonUtility.FromJson<ExperiencePacket>(WireEncoding.GetString(bytes.ToArray()));
                if(packet==null||packet.version!=1||packet.epoch==null||packet.epoch.Length>32||packet.challenge==null||packet.challenge.Length>32||packet.roomId==null||packet.roomId.Length>128||packet.digest==null||packet.digest.Length>64)return true;
                if(runner.IsServer)ReceivePeer(sender,packet);
                else if(sender.IsNone)ReceiveServer(packet);
            }
            catch(Exception){/* Malformed packets never mutate the authority model. */}
            return true;
        }
        void ReceivePeer(PlayerRef sender,ExperiencePacket packet)
        {
            if(!room.GameExperienceMemberId(transportRunner,sender,out var id))return;
            if(!transportPeers.TryGetValue(id,out var receipt)){receipt=new PeerReceipt();transportPeers[id]=receipt;}
            if(packet.kind=="hello"){if(!transportMigration&&TransportNow-receipt.lastCommand>.25){receipt.lastCommand=TransportNow;SendSnapshot(sender);}return;}
            if(packet.epoch!=transportEpoch)return;
            if(packet.kind=="offer"&&transportMigration&&packet.challenge==transportChallenge)
            {
                if(recoveryResponses.Contains(id))return;
                var offered=Verify(packet.checkpoint,transportPriorKey);
                if(offered==null||offered.state.roomId!=transportCache.state.roomId||offered.state.authorityTerm!=transportCache.state.authorityTerm)return;
                if(recoveryDigests.TryGetValue(offered.state.revision,out var previousDigest)&&previousDigest!=offered.digest)
                {TransportFailure("Conflicting signed room checkpoints; recovery stopped.");return;}
                recoveryDigests[offered.state.revision]=offered.digest;
                if(offered.state.revision>recoveryBest.state.revision)recoveryBest=offered;
                recoveryResponses.Add(id);return;
            }
            if(TransportRecovering)return;
            if(packet.kind=="cached")
            {if(transportSent.ContainsKey(packet.digest))receipt.digest=packet.digest;return;}
            if(packet.kind!="command"||packet.roomId!=CaptureSnapshot().roomId||!CommandValid(packet.command)||packet.sequence<1)return;
            if(packet.sequence==receipt.sequence){Send(sender,new ExperiencePacket{kind="processed",epoch=transportEpoch,sequence=receipt.sequence});return;}
            if(packet.sequence!=receipt.sequence+1||TransportNow-receipt.lastCommand<.1)return;
            receipt.lastCommand=TransportNow;
            if(packet.command.verb=="ready"||room.GameExperienceIsLeader(id))HandleAuthoritativeCommand(id,packet.command);
            receipt.sequence=packet.sequence;
            Send(sender,new ExperiencePacket{kind="processed",epoch=transportEpoch,sequence=receipt.sequence});
        }
        void ReceiveServer(ExperiencePacket packet)
        {
            if(packet.kind=="recover")
            {
                if(!transportMigration||transportCache==null||packet.epoch.Length!=32||packet.challenge.Length!=32)return;
                transportEpoch=packet.epoch;
                Send(default,new ExperiencePacket{kind="offer",epoch=transportEpoch,challenge=packet.challenge,checkpoint=transportCache.wire});return;
            }
            if(packet.kind=="processed")
            {
                if(!TransportRecovering&&packet.epoch==transportEpoch&&transportCommands.Count>0&&packet.sequence==transportSequence+1)
                {transportCommands.Dequeue();transportSequence=packet.sequence;transportLastSend=-100;}return;
            }
            if(packet.kind!="state"||packet.epoch.Length!=32)return;
            if(transportEpoch.Length!=0&&packet.epoch!=transportEpoch)return;
            var accepted=Verify(packet.checkpoint,string.IsNullOrEmpty(transportPinnedKey)?null:transportPinnedKey);if(accepted==null)return;
            if(transportCache!=null)
            {
                if(accepted.state.roomId!=transportCache.state.roomId||transportMigration&&accepted.state.authorityTerm<=transportCache.state.authorityTerm)return;
                if(accepted.state.roomId==transportCache.state.roomId)
                {
                    if(accepted.state.authorityTerm<transportCache.state.authorityTerm||accepted.state.authorityTerm==transportCache.state.authorityTerm&&accepted.state.revision<transportCache.state.revision)return;
                    if(accepted.state.authorityTerm==transportCache.state.authorityTerm&&accepted.state.revision==transportCache.state.revision&&accepted.digest!=transportCache.digest)
                    {TransportFailure("Conflicting room checkpoint; leave and rejoin.");return;}
                }
            }
            if(!ValidateAndApplySnapshot(accepted.state))return;
            transportEpoch=packet.epoch;transportPinnedKey=CheckpointKey(accepted.wire);transportCache=accepted;
            transportRecovering=false;transportMigration=false;TransportStatus="";
            Send(default,new ExperiencePacket{kind="cached",epoch=transportEpoch,digest=accepted.digest});
        }
    }
}

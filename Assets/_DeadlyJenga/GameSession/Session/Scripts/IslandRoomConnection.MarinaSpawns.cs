using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    internal enum MarinaSpawnResult { Wait, Accepted, Rejected }
    public sealed partial class IslandRoomConnection
    {
        readonly Dictionary<PlayerRef,int> marinaSlots=new Dictionary<PlayerRef,int>();
        int marinaReservationGeneration=int.MinValue;
        bool marinaRecoveryPending,marinaHostPlaced;
        IslandLobby marinaPlacedLobby;
        int marinaPlacedFloor=-1;
        bool marinaHasAcceptedPose;
        int marinaAcceptedGeneration=int.MinValue,marinaAcceptedFloor=-1;
        bool marinaRefreshRequested;
        Transform marinaAcceptedRoot;
        P.Pose marinaAcceptedLocal;
        internal int MarinaSpawnGeneration=>generation;
        internal bool MarinaHostRecoveryPending=>marinaRecoveryPending;
        internal bool ConsumeMarinaSpawnRefreshRequest(){bool requested=marinaRefreshRequested;marinaRefreshRequested=false;return requested;}
        IslandLobby MarinaLobby=>IslandLobby.Current;
        internal MarinaSpawnResult TryReserveMarinaSpawn(PlayerRef participant,out P.Pose pose,out string reason)
        {
            pose=default;reason="";
            if(!IsHost||Runner!=ownedRunner||!Runner.ActivePlayers.Contains(participant))
            {reason="Only an authenticated active participant can reserve a lobby slot.";return MarinaSpawnResult.Rejected;}
            var lobby=MarinaLobby;if(!lobby){reason="Waiting for the active lobby.";return MarinaSpawnResult.Wait;}
            if(!lobby.UsesMarinaSpawns){pose=new P.Pose(IslandLobby.SpawnPoint(participant.RawEncoded),Quaternion.identity);return MarinaSpawnResult.Accepted;}
            if(marinaRecoveryPending){reason="Waiting for migrated lobby spawn recovery.";return MarinaSpawnResult.Wait;}
            if(marinaSlots.Count==0)marinaReservationGeneration=generation;
            if(marinaReservationGeneration!=generation){reason="Waiting for the current room spawn generation.";return MarinaSpawnResult.Wait;}
            if(!marinaSlots.ContainsKey(Runner.LocalPlayer))
            {
                if(marinaSlots.Values.Contains(0)){reason="Host slot zero is already reserved.";return MarinaSpawnResult.Rejected;}
                var host=lobby.TryMarinaSpawnPose(0,out var hostPose,out reason,lobby.Player?.gameObject);
                if(host!=MarinaSpawnResult.Accepted)return host;
                if(!lobby.ApplyReservedMarinaSpawn(hostPose)){reason="Waiting for the host lobby body.";return MarinaSpawnResult.Wait;}
                marinaSlots.Add(Runner.LocalPlayer,0);marinaHostPlaced=true;marinaPlacedLobby=lobby;marinaPlacedFloor=lobby.MarinaFloorRevision;
            }
            if(!marinaHostPlaced||marinaPlacedLobby!=lobby||marinaPlacedFloor!=lobby.MarinaFloorRevision)
            {
                var host=lobby.TryMarinaSpawnPose(0,out var hostPose,out reason,lobby.Player?.gameObject);
                if(host!=MarinaSpawnResult.Accepted)return host;
                if(!lobby.ApplyReservedMarinaSpawn(hostPose)){reason="Waiting for the host lobby body.";return MarinaSpawnResult.Wait;}
                marinaHostPlaced=true;marinaPlacedLobby=lobby;marinaPlacedFloor=lobby.MarinaFloorRevision;
            }
            if(marinaSlots.TryGetValue(participant,out int existing))
                return lobby.TryMarinaSpawnPose(existing,out pose,out reason,participant==Runner.LocalPlayer?lobby.Player.gameObject:lobby.MarinaParticipantRoot(participant.ToString()));
            bool free=false;string waiting="";
            for(int slot=1;slot<10;slot++)
            {
                if(marinaSlots.Values.Contains(slot))continue;free=true;
                var result=lobby.TryMarinaSpawnPose(slot,out pose,out reason,lobby.MarinaParticipantRoot(participant.ToString()));
                if(result==MarinaSpawnResult.Accepted){marinaSlots.Add(participant,slot);return result;}
                if(result==MarinaSpawnResult.Rejected)return result;
                waiting=reason;
            }
            reason=free?waiting:"This ten-player lobby has no unreserved slots.";
            return free?MarinaSpawnResult.Wait:MarinaSpawnResult.Rejected;
        }
        internal MarinaSpawnResult TryGetReservedMarinaSpawn(PlayerRef participant,out P.Pose pose,out string reason)
        {
            pose=default;reason="";
            if(!IsHost||Runner!=ownedRunner||!Runner.ActivePlayers.Contains(participant))
            {reason="The lobby participant is no longer active.";return MarinaSpawnResult.Rejected;}
            var lobby=MarinaLobby;if(!lobby){reason="Waiting for the active lobby.";return MarinaSpawnResult.Wait;}
            if(!lobby.UsesMarinaSpawns){pose=new P.Pose(IslandLobby.SpawnPoint(participant.RawEncoded),Quaternion.identity);return MarinaSpawnResult.Accepted;}
            if(marinaRecoveryPending||marinaReservationGeneration!=generation)
            {reason="Waiting for current-generation lobby reservations.";return MarinaSpawnResult.Wait;}
            if(!marinaSlots.TryGetValue(participant,out int slot)){reason="The participant has no lobby reservation.";return MarinaSpawnResult.Rejected;}
            return lobby.TryMarinaSpawnPose(slot,out pose,out reason,participant==Runner.LocalPlayer?lobby.Player.gameObject:lobby.MarinaParticipantRoot(participant.ToString()));
        }
        internal void ReleaseMarinaSpawn(PlayerRef participant){marinaSlots.Remove(participant);if(Runner&&participant==Runner.LocalPlayer)marinaHostPlaced=false;}
        internal void ClearMarinaSpawnReservations()
        {
            marinaSlots.Clear();marinaReservationGeneration=int.MinValue;marinaHostPlaced=false;marinaPlacedLobby=null;
            marinaRecoveryPending=false;marinaHasAcceptedPose=false;marinaAcceptedGeneration=int.MinValue;marinaAcceptedRoot=null;marinaAcceptedFloor=-1;marinaRefreshRequested=false;
        }
        internal void BeginMarinaHostRecovery(){marinaRecoveryPending=true;}
        internal MarinaSpawnResult TryCompleteMarinaHostRecovery(out bool completedNow,out string reason)
        {
            completedNow=false;reason="";
            if(!marinaRecoveryPending)return MarinaSpawnResult.Accepted;
            if(!IsHost||Runner!=ownedRunner){reason="Waiting for the current migrated host runner.";return MarinaSpawnResult.Wait;}
            var lobby=MarinaLobby;if(!lobby){reason="Waiting for the adopted lobby scene.";return MarinaSpawnResult.Wait;}
            if(!lobby.UsesMarinaSpawns){marinaRecoveryPending=false;completedNow=true;return MarinaSpawnResult.Accepted;}
            var roster=Runner.ActivePlayers.ToArray();
            if(roster.Length==0||!roster.Contains(Runner.LocalPlayer)){reason="Waiting for the authenticated migrated roster.";return MarinaSpawnResult.Wait;}
            if(roster.Length>10){reason="Migrated roster exceeds lobby capacity.";return MarinaSpawnResult.Rejected;}
            var replacement=new Dictionary<PlayerRef,int>();
            var host=lobby.TryMarinaSpawnPose(0,out var hostPose,out reason,lobby.Player?.gameObject);
            if(host!=MarinaSpawnResult.Accepted)return host;
            replacement.Add(Runner.LocalPlayer,0);
            foreach(var participant in roster.OrderBy(player=>player.RawEncoded))
            {
                if(participant==Runner.LocalPlayer)continue;
                bool assigned=false;string waiting="";
                for(int slot=1;slot<10;slot++)
                {
                    if(replacement.Values.Contains(slot))continue;
                    var result=lobby.TryMarinaSpawnPose(slot,out _,out reason,lobby.MarinaParticipantRoot(participant.ToString()));
                    if(result==MarinaSpawnResult.Rejected)return result;
                    if(result==MarinaSpawnResult.Accepted){replacement.Add(participant,slot);assigned=true;break;}
                    waiting=reason;
                }
                if(!assigned){reason=waiting;return MarinaSpawnResult.Wait;}
            }
            if(!roster.OrderBy(player=>player.RawEncoded).SequenceEqual(Runner.ActivePlayers.OrderBy(player=>player.RawEncoded)))
            {reason="Migrated roster changed during allocation.";return MarinaSpawnResult.Wait;}
            if(!lobby.ApplyReservedMarinaSpawn(hostPose)){reason="Waiting for the migrated host body.";return MarinaSpawnResult.Wait;}
            marinaSlots.Clear();foreach(var pair in replacement)marinaSlots.Add(pair.Key,pair.Value);
            marinaReservationGeneration=generation;marinaHostPlaced=true;marinaPlacedLobby=lobby;marinaPlacedFloor=lobby.MarinaFloorRevision;
            marinaRecoveryPending=false;completedNow=true;return MarinaSpawnResult.Accepted;
        }
        internal MarinaSpawnResult AcceptLocalMarinaSpawn(NetworkRunner packetRunner,P.Pose pose,out string reason)
        {
            reason="";
            if(!Connected||IsHost||packetRunner!=Runner||!Owns(packetRunner))
            {reason="Lobby spawn came from an inactive runner.";return MarinaSpawnResult.Rejected;}
            var lobby=MarinaLobby;if(!lobby){reason="Waiting for the local lobby.";return MarinaSpawnResult.Wait;}
            if(!lobby.UsesMarinaSpawns)return MarinaSpawnResult.Accepted;
            var validation=lobby.ValidateMarinaAcceptedPose(pose,out reason,false);
            if(validation!=MarinaSpawnResult.Accepted)return validation;
            marinaAcceptedLocal=new P.Pose(lobby.transform.InverseTransformPoint(pose.position),Quaternion.Inverse(lobby.transform.rotation)*pose.rotation);
            marinaAcceptedRoot=lobby.transform;marinaAcceptedGeneration=generation;marinaAcceptedFloor=lobby.MarinaFloorRevision;marinaHasAcceptedPose=true;marinaRefreshRequested=false;
            return lobby.ValidateMarinaAcceptedPose(pose,out reason);
        }
        internal MarinaSpawnResult TryGetLocalMarinaSpawn(out P.Pose pose,out string reason)
        {
            pose=default;reason="";
            if(!Connected){reason="Waiting for the room connection.";return MarinaSpawnResult.Wait;}
            if(IsHost)return TryReserveMarinaSpawn(Runner.LocalPlayer,out pose,out reason);
            var lobby=MarinaLobby;
            if(!lobby||!marinaHasAcceptedPose||marinaAcceptedGeneration!=generation||marinaAcceptedRoot!=lobby.transform)
            {reason="Waiting for your current host-approved lobby arrival.";return MarinaSpawnResult.Wait;}
            if(marinaAcceptedFloor!=lobby.MarinaFloorRevision)
            {marinaHasAcceptedPose=false;marinaRefreshRequested=true;reason="Waiting for a fresh host spawn on the rebuilt lobby floor.";return MarinaSpawnResult.Wait;}
            pose=new P.Pose(lobby.transform.TransformPoint(marinaAcceptedLocal.position),lobby.transform.rotation*marinaAcceptedLocal.rotation);
            return lobby.ValidateMarinaAcceptedPose(pose,out reason);
        }
    }
}

using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class RoomGameExperience
    {
        CastlePlaySession holdOwner;
        bool previousDeferredHold;

        internal bool HoldsLocalParticipant(CastlePlaySession session)
        {
            if(!session||!Manages(session)||session.VisitingLobby)return false;
            var state=State;
            string own=room.GameExperienceLocalId;
            return TransportRecovering||string.IsNullOrEmpty(own)||Array.IndexOf(state.launchIds,own)<0;
        }
        internal bool ShouldSuspendActor(string actorId)
        {
            if(string.IsNullOrEmpty(actorId)||Experience!=GameExperience.Game)return false;
            if(IsAuthority&&room.Connected&&room.Runner)
                foreach(var player in room.Runner.ActivePlayers)
                    if(player.ToString()==actorId)
                    {
                        if(!room.GameExperienceMemberId(room.Runner,player,out var stable))return true;
                        return TransportRecovering||Array.IndexOf(State.launchIds,stable)<0;
                    }
            if(match&&match.Modes&&actorId==match.Modes.LocalActorId)return HoldsLocalParticipant(match);
            var rows=match&&match.Modes?match.Modes.Frame?.players:null;
            var row=rows==null?null:Array.Find(rows,value=>value!=null&&value.id==actorId);
            return row!=null&&!row.bot&&(TransportRecovering||row.role=="spectator");
        }
        internal bool ShouldSuspendVisualActor(string actorId)
        {
            if(IsAuthority||!TransportRecovering||!match||!match.Modes||
                actorId==match.Modes.LocalActorId)return ShouldSuspendActor(actorId);
            // Fusion already authenticated the new host's pose. A room checkpoint
            // can hold input and gameplay without stopping presentation of that pose.
            var rows=match.Modes.Frame?.players;
            var row=rows==null?null:Array.Find(rows,value=>value!=null&&value.id==actorId);
            return row!=null&&!row.bot&&row.role=="spectator";
        }
        void TickDeferredParticipants()
        {
            if(Experience==GameExperience.Demo)
            {
                if(holdOwner&&previousDeferredHold&&NetworkActors.Alive(holdOwner.Player)&&NetworkActors.Alive(holdOwner.Hand))holdOwner.SetMenu(holdOwner.MenuOpen);
                previousDeferredHold=false;holdOwner=null;return;
            }
            var phase=State.phase;
            if(!match&&phase!=GameFlowPhase.LoadingGame&&phase!=GameFlowPhase.Playing&&phase!=GameFlowPhase.Results&&phase!=GameFlowPhase.Failed)return;
            if(!match)FindExistingMatch();
            if(!match)return;
            bool held=HoldsLocalParticipant(match);
            if(!ReferenceEquals(holdOwner,match)||previousDeferredHold!=held)
            {
                holdOwner=match;previousDeferredHold=held;
                // Session combines this reason with its current menu/focus/travel/world holds.
                match.SetMenu(match.MenuOpen);
            }
            if(!Manages(match)||match.Modes?.Frame?.players==null)return;
            foreach(var row in match.Modes.Frame.players)
            {
                if(row==null||row.bot||row.role!="spectator")continue;
                var actor=match.Modes.FindActor(row.id);
                if(!NetworkActors.Alive(actor))continue;
                actor.gameObject.GetComponent<INetworkActorView>()?.Suspend();
            }
            // Character owners project spectator frames into their own body/hand collision state.
            // XR origins and tracking components remain enabled; this class never edits colliders.
        }
    }
}

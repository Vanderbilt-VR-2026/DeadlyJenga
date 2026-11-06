using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum CastleDemoAction { Rebuild, AddSurvivor, FillSurvivors, NextSurvivor, ClearSurvivors, AddHand, NextHand, ClearHands, Stamina, Disaster, StopDisasters, AdvanceScaffold, SwitchMap, ReturnRoom, SharedControls, EndGame }
    [Serializable] public sealed class CastleDemoCommand
    {
        public CastleDemoAction action;
        public string rules;
        public bool tuneWave;
        public float waveSpeed,waveForce;
        public float[] hazardValues;
    }
    public static class CastleDemoCommands
    {
        public static void Run(CastlePlaySession session,CastleDemoAction action,string rules=null)
        {
            if(!SessionDevelopmentPolicy.CanRequestLocal(session))return;
            var network=session.GetComponent<CastleFusionSession>();
            if(network){network.Request(new CastleDemoCommand{action=action,rules=rules});return;}
            Execute(session,new CastleDemoCommand{action=action,rules=rules});
        }
        public static bool Execute(CastlePlaySession session,CastleDemoCommand command)
        {
            if(!SessionDevelopmentPolicy.CanExecuteLocal(session))return false;
            return ExecuteAuthorized(session,command,IslandRoomConnection.Instance?.LocalLobbyId);
        }
        internal static bool ExecuteFromPeer(CastlePlaySession session,CastleDemoCommand command,string sender)
        {
            if(!SessionDevelopmentPolicy.CanExecutePeer(session,sender))return false;
            return ExecuteAuthorized(session,command,sender);
        }
        static bool ExecuteAuthorized(CastlePlaySession session,CastleDemoCommand command,string sender)
        {
            if(!SessionDevelopmentPolicy.CanExecuteServer(session)||command==null||!Enum.IsDefined(typeof(CastleDemoAction),command.action))return false;
            switch(command.action)
            {
                case CastleDemoAction.SwitchMap:
                case CastleDemoAction.ReturnRoom:
                case CastleDemoAction.SharedControls:
                case CastleDemoAction.EndGame:
                    var room=IslandRoomConnection.Instance;
                    if(!room||!room.IsHost||sender!=room.LeaderId)return false;
                    if(command.action==CastleDemoAction.SwitchMap)
                    {
                        if(string.IsNullOrEmpty(command.rules)||command.rules.Length>128)return false;
                        room.SwitchGameMap(command.rules);
                    }
                    else if(command.action==CastleDemoAction.ReturnRoom)return room.ReturnMatchToLobby();
                    else if(command.action==CastleDemoAction.EndGame)return session.Modes&&session.Modes.EndGameForDevelopment();
                    else if(command.rules=="on"||command.rules=="off")room.SetMatchSharedControls(command.rules=="on");
                    else return false;
                    break;
                case CastleDemoAction.AdvanceScaffold:
                    if (!(DeadlyJenga.Core.Services.World is DeadlyJenga.Core.IBuildUpControls buildUp) || !buildUp.CanAdvance) return false;
                    buildUp.AdvanceStage(); break;
                case CastleDemoAction.StopDisasters:DemoDisasters.StopAll(session);break;
                case CastleDemoAction.Disaster:return DemoDisasters.ExecuteDevelopmentAuthorized(session,command.rules,command.tuneWave?(float?)command.waveSpeed:null,command.tuneWave?(float?)command.waveForce:null,command.hazardValues);
                case CastleDemoAction.Rebuild:session.RebuildForDevelopment();break;
                case CastleDemoAction.AddSurvivor:session.SurvivorBots?.SpawnOne();break;
                case CastleDemoAction.FillSurvivors:session.SurvivorBots?.FillToTarget();break;
                case CastleDemoAction.NextSurvivor:session.SurvivorBots?.CyclePersonality();break;
                case CastleDemoAction.ClearSurvivors:session.SurvivorBots?.Clear();break;
                case CastleDemoAction.AddHand:session.HandBots?.SpawnOne();break;
                case CastleDemoAction.NextHand:session.HandBots?.CyclePersonality();break;
                case CastleDemoAction.ClearHands:session.HandBots?.Clear();break;
                case CastleDemoAction.Stamina:if(command.rules==null||command.rules.Length>4096)return false;MatchSetup.ApplyRules(session.Player,command.rules);break;
            }
            return true;
        }
    }
}

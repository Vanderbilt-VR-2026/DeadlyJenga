using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum CastleDemoAction { Rebuild, AddSurvivor, FillSurvivors, NextSurvivor, ClearSurvivors, AddHand, NextHand, ClearHands, Stamina, Disaster, StopDisasters }
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
            var network=session.GetComponent<CastleFusionSession>();
            if(network){network.Request(new CastleDemoCommand{action=action,rules=rules});return;}
            Execute(session,new CastleDemoCommand{action=action,rules=rules});
        }
        public static bool Execute(CastlePlaySession session,CastleDemoCommand command)
        {
            if(command==null||!Enum.IsDefined(typeof(CastleDemoAction),command.action))return false;
            switch(command.action)
            {
                case CastleDemoAction.StopDisasters:if(!DemoDisasters.CanTrigger(session))return false;DemoDisasters.StopAll(session);break;
                case CastleDemoAction.Disaster:return DemoDisasters.Execute(session,command.rules,command.tuneWave?(float?)command.waveSpeed:null,command.tuneWave?(float?)command.waveForce:null,command.hazardValues);
                case CastleDemoAction.Rebuild:session.ResetCastle();break;
                case CastleDemoAction.AddSurvivor:session.Bots.SpawnBot();break;
                case CastleDemoAction.FillSurvivors:session.Bots.FillToTarget();break;
                case CastleDemoAction.NextSurvivor:session.Bots.CyclePersonality();break;
                case CastleDemoAction.ClearSurvivors:session.Bots.ClearBots();break;
                case CastleDemoAction.AddHand:session.HandBots.SpawnHand();break;
                case CastleDemoAction.NextHand:session.HandBots.CyclePersonality();break;
                case CastleDemoAction.ClearHands:session.HandBots.ClearHands();break;
                case CastleDemoAction.Stamina:if(command.rules==null||command.rules.Length>4096)return false;MatchSetup.ApplyRules(session.Player,command.rules);break;
            }
            return true;
        }
    }
}

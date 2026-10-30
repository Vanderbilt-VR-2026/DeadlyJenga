using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public static class ModeCtfRecovery
    {
        public static void FilterRoleGoals(bool defender,List<BotObjective> goals)
        {
            if(defender)return;
            for(int i=goals.Count-1;i>=0;i--)if(goals[i].id=="guard-home")goals.RemoveAt(i);
        }
        public static void PublicSearch(int team,bool ownHeldByEnemy,bool carrierVisible,IEnumerable<FlagFrame> flags,List<Vector3> destinations)
        {
            destinations.Clear();if(!ownHeldByEnemy||carrierVisible)return;
            foreach(var flag in flags)if(flag.team!=team)destinations.Add(flag.home);
        }
    }
}

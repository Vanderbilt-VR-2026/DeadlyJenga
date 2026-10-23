using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class DemoPowerups
    {
        static readonly System.Collections.Generic.List<GameObject> spawned=new System.Collections.Generic.List<GameObject>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){spawned.Clear();status="";statusUntil=0;}
        static string status;
        static float statusUntil;
        public static string Status=>Time.unscaledTime<statusUntil?status:"";
        static bool Message(string value,bool success){status=value;statusUntil=Time.unscaledTime+5;return success;}
        public static bool Spawn(CastlePlaySession session,bool speed)
        {
            bool success=session&&session.Demo?.CanCommand==true&&(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment(speed?EquipmentKind.Speed:EquipmentKind.Jump)==true;
            return Message(success?"Powerup spawned. Walk near it; confirm a swap if your slot is occupied.":"Return to your survivor in Demo and make space ahead.",success);
        }
    }
}

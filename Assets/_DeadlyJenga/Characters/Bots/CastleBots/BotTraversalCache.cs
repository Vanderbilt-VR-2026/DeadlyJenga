using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public static class BotTraversalCache
    {
        struct Entry { public bool valid; public CastleBotRoute route; public float speed, until; }
        static readonly Dictionary<(Vector3Int, Vector3Int, int), Entry> links = new Dictionary<(Vector3Int, Vector3Int, int), Entry>();
        struct LandingEntry { public bool valid; public float until; }
        static readonly Dictionary<(Vector3Int,int,int),LandingEntry> landings=new Dictionary<(Vector3Int,int,int),LandingEntry>();
        static IWorldQuery world;
        public static int Hits { get; private set; }
        public static int Misses { get; private set; }
        static Vector3Int Cell(Vector3 p) => Vector3Int.RoundToInt(p * 50);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { links.Clear(); landings.Clear(); world = null; Hits = Misses = 0; }
        public static bool Landing(CastleSurvivor motor,Vector3 point)
        {
            if(!ReferenceEquals(world,Services.World)){links.Clear();landings.Clear();world=Services.World;}
            var key=(Cell(point),Mathf.RoundToInt(motor.StandingHeight*1000),Mathf.RoundToInt(motor.Capsule.radius*1000));
            if(landings.TryGetValue(key,out var entry)&&Time.time<entry.until)return entry.valid;
            bool valid=CastleBotNavigation.Landing(motor,point,out _);
            if(landings.Count>16000)landings.Clear();
            landings[key]=new LandingEntry{valid=valid,until=Time.time+.4f};
            return valid;
        }
        public static bool TryLink(CastleSurvivor motor, Vector3 from, Vector3 to, out CastleBotRoute route, out float speed)
        {
            if (!ReferenceEquals(world, Services.World)) { links.Clear(); landings.Clear(); world = Services.World; }
            var rules = MatchRuntime.Current?.RulesFor(motor.gameObject);
            int capability;
            unchecked
            {
                capability = Mathf.RoundToInt(motor.EffectiveWalkSpeed * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.StandingHeight * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.Capsule.radius * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.jumpHeight * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.gravity * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.vaultMaxRise * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.vaultReach * 100);
                capability = capability * 31 + Mathf.RoundToInt(motor.GripRemaining * 20);
                capability = capability * 31 + (motor.CarryingFlag ? 1 : 0);
                capability = capability * 31 + (rules == null || rules.canJump ? 1 : 0);
                capability = capability * 31 + (rules == null || rules.canVault ? 1 : 0);
                capability = capability * 31 + (rules == null || rules.canClimb ? 1 : 0);
                capability = capability * 31 + (rules!=null&&rules.enemyClimbBlocked?motor.Team:-1);
                capability = capability * 31 + (motor.climbingEnabled?1:0);
                capability = capability * 31 + Mathf.RoundToInt(motor.climbSpeed*100);
                capability = capability * 31 + Mathf.RoundToInt(motor.climbGripSeconds*100);
                capability = capability * 31 + Mathf.RoundToInt(CastleBotNavigation.MaximumDrop(motor)*100);
            }
            var key = (Cell(from), Cell(to), capability);
            if (links.TryGetValue(key, out var entry) && Time.time < entry.until)
            { Hits++; route = entry.route; speed = entry.speed; return entry.valid; }
            Misses++;
            bool valid = CastleBotNavigation.TryLink(motor, from, to, out route, out speed);
            if (links.Count > 16000) links.Clear();
            links[key] = new Entry { valid = valid, route = route, speed = speed, until = Time.time + .5f };
            return valid;
        }
    }
}

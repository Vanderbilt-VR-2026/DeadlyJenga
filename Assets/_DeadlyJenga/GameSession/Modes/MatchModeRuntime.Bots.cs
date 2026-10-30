using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly List<Vector3> supportGoals=new List<Vector3>();
        readonly List<Vector3> supportCandidates=new List<Vector3>();
        readonly List<Vector3> searchStages=new List<Vector3>(8);
        float nextSupportGoals;
        IWorldQuery supportWorld;
        void RefreshSupportGoals()
        {
            if(!ReferenceEquals(supportWorld,Services.World)){supportWorld=Services.World;nextSupportGoals=0;}
            if(Time.time<nextSupportGoals)return;nextSupportGoals=Time.time+5;supportCandidates.Clear();supportGoals.Clear();
            var world=Services.World;if(world==null)return;
            foreach(var body in world.Blocks)
            {
                if(!body||!body.gameObject.activeInHierarchy||body.linearVelocity.sqrMagnitude>9)continue;
                var collider=body.GetComponent<Collider>();if(!collider)continue;
                var bounds=collider.bounds;var ray=new Ray(new Vector3(bounds.center.x,bounds.max.y+.5f,bounds.center.z),Vector3.down);
                if(!collider.Raycast(ray,out var hit,bounds.size.y+1)||hit.normal.y<=.65f||hit.point.y<=world.WaterHeight(hit.point)+.2f)continue;
                if(Physics.CheckCapsule(hit.point+Vector3.up*.38f,hit.point+Vector3.up*1.55f,.3f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))continue;
                supportCandidates.Add(hit.point+Vector3.up*.05f);
            }
            supportCandidates.Sort((a,b)=>b.y.CompareTo(a.y));
            foreach(var p in supportCandidates)
            {
                bool crowded=false;foreach(var selected in supportGoals)if((selected-p).sqrMagnitude<16){crowded=true;break;}
                if(!crowded)supportGoals.Add(p);if(supportGoals.Count>=12)break;
            }
        }
        static bool VisibleTo(Vector3 from,Vector3 target)
        {
            Vector3 delta=target+Vector3.up*.6f-(from+Vector3.up*.6f);
            return delta.sqrMagnitude<30*30&&!Physics.Raycast(from+Vector3.up*.6f,delta.normalized,delta.magnitude,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore);
        }
        static void AddGoal(List<BotObjective> goals,string id,BotObjectiveKind kind,Vector3 point,GameObject subject,float radius,float priority)
        {goals.Add(new BotObjective{id=id,kind=kind,position=point,subject=subject,radius=radius,priority=priority,expiresAt=Time.time+1});}
    }
}

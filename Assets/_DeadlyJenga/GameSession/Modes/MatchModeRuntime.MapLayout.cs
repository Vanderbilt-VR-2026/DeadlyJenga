using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        MapMatchLayout[] availableLayouts;
        MapMatchLayout authoredLayout;
        IWorldQuery layoutWorld;
        int layoutRevision=-1;
        MapMatchLayout AuthoredLayout()
        {
            var world=Services.World;int revision=(world as ISyncedWorld)?.Revision??0;
            if(ReferenceEquals(world,layoutWorld)&&revision==layoutRevision)return authoredLayout;
            layoutWorld=world;layoutRevision=revision;authoredLayout=null;
            Physics.SyncTransforms();
            availableLayouts??=Resources.LoadAll<MapMatchLayout>("MapMatchLayouts");
            foreach(var candidate in availableLayouts)if(candidate&&candidate.Matches(world)){authoredLayout=candidate;break;}
            return authoredLayout;
        }
        Vector3 LayoutPosition(Vector3 local)=>((ISyncedWorld)Services.World).Root.TransformPoint(local);
        bool TryLayoutSpawn(int team,int ordinal,string role,out Vector3 point)
        {
            point=default;var layout=AuthoredLayout();if(!layout)return false;
            var spawns=layout.freeSpawns;
            if(Rules.spawn==SpawnRule.TeamPoles||role=="infected")
            {
                if(layout.teams==null||layout.teams.Length==0||Rules.teamCount>layout.teams.Length)return false;
                int group=team<0?Mathf.Abs(ordinal)%layout.teams.Length:team;
                if(group>=layout.teams.Length||layout.teams[group]==null)return false;
                spawns=layout.teams[group].spawns;
            }
            if(spawns==null||spawns.Length==0)return false;
            float bestClearance=-1;bool found=false;
            for(int attempt=0;attempt<spawns.Length;attempt++)
            {
                int index=(Mathf.Max(0,ordinal)+attempt)%spawns.Length;
                var candidate=LayoutPosition(spawns[index]);
                if(!ClearSpawn(candidate))continue;
                float clearance=float.PositiveInfinity;
                foreach(var pair in actors)
                {
                    var actor=pair.Value;if(!NetworkActors.Alive(actor)||!actor.gameObject.activeInHierarchy||!actor.Alive)continue;
                    clearance=Mathf.Min(clearance,(actor.transform.position-candidate).sqrMagnitude);
                }
                if(clearance>bestClearance){point=candidate;bestClearance=clearance;found=true;}
                if(clearance>=1.44f)return true;
            }
            return found;
        }
        static bool ClearSpawn(Vector3 point)
        {
            int mask=(1<<Layers.Surface)|(1<<Layers.Block)|1;
            return Physics.Raycast(point+Vector3.up*.25f,Vector3.down,.5f,mask,QueryTriggerInteraction.Ignore)&&
                !Physics.CheckCapsule(point+Vector3.up*.4f,point+Vector3.up*1.65f,.32f,mask,QueryTriggerInteraction.Ignore);
        }
        bool TryLayoutFlagHomes(out Vector3[] homes)
        {
            homes=null;var layout=AuthoredLayout();if(!layout||layout.teams==null||Rules.teamCount>layout.teams.Length)return false;
            homes=new Vector3[Rules.teamCount];
            for(int i=0;i<homes.Length;i++){if(layout.teams[i]==null)return false;homes[i]=LayoutPosition(layout.teams[i].flagHome);}
            return true;
        }
    }
}

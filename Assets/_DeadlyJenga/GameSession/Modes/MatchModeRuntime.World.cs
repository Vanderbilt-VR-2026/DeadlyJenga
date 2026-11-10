using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly RaycastHit[] supportHits=new RaycastHit[16];
        Vector3 HighestSupport()
        {
            var world=Services.World;if(world==null)return Vector3.zero;
            var layout=AuthoredLayout();if(layout)return LayoutPosition(layout.hill);
            return HighestBlockSupport(world);
        }
        Vector3 HighestBlockSupport(IWorldQuery world)
        {
            Vector3 best=world.SafeSpawnPoint();float height=float.NegativeInfinity;
            if(Physics.Raycast(new Vector3(world.Center.x,world.PlayableBounds.max.y+20,world.Center.z),Vector3.down,out var floor,world.PlayableBounds.size.y+100,1,QueryTriggerInteraction.Ignore))best=floor.point+Vector3.up*.05f;
            foreach(var block in world.Blocks)
            {
                if(!block||!block.gameObject.activeInHierarchy||block.linearVelocity.sqrMagnitude>16)continue;
                var collider=block.GetComponent<Collider>();if(!collider)continue;
                var bounds=collider.bounds;
                Vector3 top=new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z);
                if(collider.Raycast(new Ray(top,Vector3.down),out var hit,bounds.size.y+2)&&hit.normal.y>=.65f&&hit.point.y>height&&hit.point.y>world.WaterHeight(hit.point)+.2f&&ClearSpawn(hit.point+Vector3.up*.05f))
                {best=hit.point+Vector3.up*.04f;height=hit.point.y;}
            }
            return best;
        }
        const int SpawnSupportMask=(1<<Layers.Surface)|(1<<Layers.Block)|1;
        readonly Collider[] spawnContacts=new Collider[32];
        readonly Dictionary<string,(Vector3 point,float expires)> spawnReservations=new();
        readonly List<string> expiredSpawnReservations=new();
        internal void ReserveSpawn(string id,Vector3 point)
        {
            if(!string.IsNullOrEmpty(id))spawnReservations[id]=(point,Time.unscaledTime+4);
        }

        public Vector3 SpawnPoint(int team,int ordinal,string role,string actorId=null)
        {
            var world=Services.World;
            if(world==null)return Vector3.up;
            Physics.SyncTransforms();
            expiredSpawnReservations.Clear();
            foreach(var reservation in spawnReservations)
                if(reservation.Value.expires<=Time.unscaledTime)expiredSpawnReservations.Add(reservation.Key);
            foreach(var id in expiredSpawnReservations)spawnReservations.Remove(id);
            if(TryLayoutSpawn(team,ordinal,role,actorId,out var authored))return authored;

            var preferred=PreferredSpawn(world,team,ordinal,role);
            if(SpawnClear(preferred,actorId))return preferred;
            bool requireBlock=Rules.spawn==SpawnRule.Top&&Rules.island!=IslandRule.Walkable&&role!="infected";
            int supportMask=requireBlock?1<<Layers.Block:SpawnSupportMask;
            // Sample nearby supports closest to the intended height first. This preserves
            // room interiors and team territory when their original slot is occupied.
            float seed=ordinal*2.399963f;
            for(int ring=1;ring<=12;ring++)
            for(int step=0;step<8;step++)
            {
                float angle=seed+step*Mathf.PI*.25f;
                var probe=preferred+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.65f);
                if(TrySpawnSupport(probe,supportMask,actorId,out var point))return point;
            }
            var safe=world.SafeSpawnPoint();
            if(TrySpawnSupport(safe,supportMask,actorId,out var fallback))return fallback;
            for(int i=0;i<24;i++)
            {
                float angle=seed+i*2.399963f;
                var probe=safe+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*((1+i/8)*1.35f);
                if(TrySpawnSupport(probe,supportMask,actorId,out fallback))return fallback;
            }

            // A heavily destroyed map may have no standable surface. Use separated
            // air spawns above the safe point instead of placing bodies inside one another.
            var above=new Vector3(safe.x,Mathf.Max(safe.y,world.PlayableBounds.max.y)+2f,safe.z);
            for(int i=0;i<=actors.Count+spawnReservations.Count;i++,above.y+=2.5f)
                if(SpawnClear(above,actorId))return above;
            return above;
        }
        bool TrySpawnSupport(Vector3 probe,int mask,string actorId,out Vector3 point)
        {
            int count=Physics.RaycastNonAlloc(probe+Vector3.up*2.5f,Vector3.down,supportHits,8,mask,QueryTriggerInteraction.Ignore);
            point=default;
            float closest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=supportHits[i];
                var candidate=hit.point+Vector3.up*.045f;
                float distance=Mathf.Abs(probe.y-candidate.y);
                if(hit.normal.y<.65f||distance>=closest||candidate.y<=Services.World.WaterHeight(candidate)+.02f||
                    !SpawnClear(candidate,actorId))continue;
                point=candidate;closest=distance;
            }
            return closest<float.PositiveInfinity;
        }
        bool SpawnClear(Vector3 point,string actorId)
        {
            var actor=actorId!=null?FindActor(actorId):null;
            var own=actor?.transform;
            if(!own&&actorId==LocalActorId)own=session?.Player?.transform;
            var movement=own?own.GetComponent<IMovementState>():null;
            float radius=Mathf.Max(.16f,.24f*(movement?.Scale??1)+.10f);
            float height=Mathf.Max(radius*2,movement?.Height??1.7f);
            int count=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*(radius+.02f),
                point+Vector3.up*(height-radius+.02f),radius,spawnContacts,SpawnSupportMask|(1<<Layers.Survivor),QueryTriggerInteraction.Ignore);
            if(count==spawnContacts.Length)return false;
            for(int i=0;i<count;i++)
                if(!own||!spawnContacts[i].transform.IsChildOf(own))return false;
            foreach(var reservation in spawnReservations)
            {
                if(reservation.Key==actorId)continue;
                Vector3 offset=point-reservation.Value.point;
                if(Mathf.Abs(offset.y)<Mathf.Max(1.7f,height)&&offset.x*offset.x+offset.z*offset.z<.85f*.85f)return false;
            }
            return true;
        }

        Vector3 PreferredSpawn(IWorldQuery world,int team,int ordinal,string role)
        {
            if(role=="infected")return RimPoint(team<0?ordinal:team,12);
            if (Rules.spawn == SpawnRule.Top)
            {
                var safe = world.SafeSpawnPoint();
                if (Rules.island != IslandRule.Walkable &&
                    !Physics.Raycast(safe + Vector3.up * .25f, Vector3.down, .8f, 1 << Layers.Block, QueryTriggerInteraction.Ignore))
                    return HighestBlockSupport(world);
                return safe;
            }
            int participants=0;foreach(var p in simulation.Participants)if(p.present)participants++;
            int count=Rules.spawn==SpawnRule.TeamPoles?Rules.teamCount:Mathf.Max(8,participants);
            return RimPoint(Rules.spawn==SpawnRule.TeamPoles?Mathf.Max(0,team):ordinal,count,Rules.spawn==SpawnRule.TeamPoles?ordinal:0);
        }
        Vector3 RimPoint(int index,int count,int spread=0)
        {
            var world=Services.World;
            float step=Mathf.Min(.045f,Mathf.PI/Mathf.Max(1,count)/8);
            float offset=spread==0?0:((spread+1)/2)*step*(spread%2==0?-1:1);
            float angle=Mathf.PI*2*index/Mathf.Max(1,count)+(world.PlayableBounds.size.x>world.PlayableBounds.size.z?Mathf.PI*.5f:0)+offset;
            Vector3 direction=new Vector3(Mathf.Sin(angle)*world.PlayableBounds.extents.x,0,Mathf.Cos(angle)*world.PlayableBounds.extents.z);
            float radius=Mathf.Max(.55f,.88f-.04f*(spread/8));
            for(int attempt=0;attempt<8;attempt++)
            {
                Vector3 point=world.Center+direction*radius;
                var ray=new Ray(new Vector3(point.x,world.PlayableBounds.max.y+20,point.z),Vector3.down);
                int n=Physics.RaycastNonAlloc(ray,supportHits,world.PlayableBounds.size.y+100,(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
                float top=float.NegativeInfinity;
                for(int i=0;i<n;i++)if(supportHits[i].normal.y>.65f&&supportHits[i].point.y>top)top=supportHits[i].point.y;
                if(top>world.WaterHeight(point)+.02f)return new Vector3(point.x,top+.05f,point.z);
                radius*=.8f;
            }
            return world.SafeSpawnPoint();
        }
        void SetFlagHomes()
        {
            if(!Authority||Rules.mode!=GameModeKind.CaptureFlag)return;
            if(TryLayoutFlagHomes(out var authoredHomes)){simulation.SetFlagHomes(authoredHomes);return;}
            var homes=new Vector3[Rules.teamCount];
            for(int t=0;t<homes.Length;t++)
            {
                var basePoint=RimPoint(t,homes.Length);var world=Services.World;
                var ray=new Ray(new Vector3(basePoint.x,world.PlayableBounds.max.y+10,basePoint.z),Vector3.down);
                if(Physics.Raycast(ray,out var hit,world.PlayableBounds.size.y+100,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))basePoint=hit.point;
                homes[t]=basePoint+Vector3.up*.1f;
                Vector3 teamDirection=Vector3.ProjectOnPlane(basePoint-world.Center,Vector3.up).normalized;
                float highest=homes[t].y;
                foreach(var block in world.Blocks)
                {
                    if(!block||block.linearVelocity.sqrMagnitude>4)continue;
                    var collider=block.GetComponent<Collider>();if(!collider)continue;
                    Vector3 direction=Vector3.ProjectOnPlane(collider.bounds.center-world.Center,Vector3.up);
                    if(direction.magnitude<world.Radius*.25f||Vector3.Dot(direction.normalized,teamDirection)<Mathf.Cos(Mathf.PI/homes.Length))continue;
                    if(collider.bounds.max.y>highest){highest=collider.bounds.max.y;homes[t]=new Vector3(collider.bounds.center.x,highest+.1f,collider.bounds.center.z);}
                }
            }
            simulation.SetFlagHomes(homes);
        }
        void TickFlagGravity()
        {
            var world=Services.World;if(world==null)return;
            foreach(var flag in simulation.Flags)
            {
                // Probe below the home so an overhead floor cannot lift a covered flag.
                var homeRay=new Ray(flag.home,Vector3.down);
                if(Physics.Raycast(homeRay,out var homeHit,world.PlayableBounds.size.y+100,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))
                    simulation.UpdateFlagSupport(flag.team,Mathf.Max(world.WaterHeight(flag.home)+.2f,homeHit.point.y+.1f,flag.home.y-1.8f));
                if(flag.carrier!=null||flag.atHome)continue;
                Vector3 p=flag.position;
                p.x=Mathf.Clamp(p.x,world.PlayableBounds.min.x+.5f,world.PlayableBounds.max.x-.5f);
                p.z=Mathf.Clamp(p.z,world.PlayableBounds.min.z+.5f,world.PlayableBounds.max.z-.5f);
                var ray=new Ray(new Vector3(p.x,world.PlayableBounds.max.y+10,p.z),Vector3.down);
                if(Physics.Raycast(ray,out var hit,world.PlayableBounds.size.y+100,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))
                {float floor=Mathf.Max(world.WaterHeight(p)+.2f,hit.point.y+.1f);p.y=Mathf.Max(floor,p.y-1.8f);}
                else p=flag.home;
                simulation.PlaceDroppedFlag(flag.team,p);
            }
        }
        void TickIsland()
        {
            var world=Services.World;if(world==null)return;
            if(simulation.Phase!=RoundPhase.Playing)return;
            foreach(var pair in actors)
            {
                var actor=pair.Value;var p=simulation.Find(pair.Key);
                if(p==null||!p.frame.alive||p.frame.role=="hand"||p.frame.role=="spectator"||transitionUntil.ContainsKey(pair.Key)||!NetworkActors.Alive(actor))continue;
                p.frame.sinking=false;p.frame.sinkProgress=0;
                var rules=RulesFor(actor.gameObject);
                var view=actor.ViewPose.position;
                if(view.y<=world.WaterHeight(view)+.25f){EliminateActor(actor,MatchSoundCue.DrowningDeath);continue;}
                Vector3 feet=actor.transform.position;
                bool onIsland=actor.Grounded&&!actor.StandingBody&&Physics.Raycast(feet+Vector3.up*.2f,Vector3.down,out var hit,.65f,(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)&&hit.normal.y>.4f;
                if(!onIsland||rules.island==IslandRule.Walkable){sinkStarted.Remove(pair.Key);continue;}
                if(rules.island==IslandRule.Lethal){EliminateActor(actor,MatchSoundCue.IslandDeath);continue;}
                if(!sinkStarted.ContainsKey(pair.Key))sinkStarted[pair.Key]=Time.time;
                p.frame.sinking=true;p.frame.sinkProgress=Mathf.Clamp01((Time.time-sinkStarted[pair.Key])/Mathf.Max(.2f,rules.sinkGraceSeconds));
                if(Time.time-sinkStarted[pair.Key]>=rules.sinkGraceSeconds)EliminateActor(actor,MatchSoundCue.IslandDeath);
            }
        }
        void EliminateActor(IModeActor actor,MatchSoundCue cause)
        {
            if(actorIds.TryGetValue(actor.gameObject,out string id)){var p=simulation.Find(id);if(p!=null){p.frame.health=0;simulation.Kill(id,null,cause);}}
            if(actor.gameObject.TryGetComponent<IDamageable>(out var damageable))damageable.Eliminate(gameObject);
        }
        public void MapRebuilt()
        {
            ResetEquipment();
            LatestReport=null;restoredHistory=false;pendingBotHealth.Clear();lastBlocks=-1;RefreshBlocks();sinkStarted.Clear();transitionUntil.Clear();delayedOutcomes.Clear();nextHill=0;ruleCache.Clear();
            simulation.StartRound();SetFlagHomes();frame=simulation.Snapshot();PublishEnemyActions();PublishEquipment();
            simulation.Hill=HighestSupport();
            foreach(var p in simulation.Participants)Respawn(p.frame.id,p.frame.role);
        }
    }
}

using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime : IBlasterRuntime
    {
        sealed class Shot{public string owner;public int id;public Vector3 position,direction;public float life;}
        readonly List<Shot> plasma=new List<Shot>();
        readonly List<PlasmaImpactFrame> plasmaImpacts=new List<PlasmaImpactFrame>();
        readonly Dictionary<string,float> shotCooldowns=new Dictionary<string,float>();
        readonly RaycastHit[] plasmaHits=new RaycastHit[64];
        readonly Collider[] plasmaOverlaps=new Collider[64];
        readonly HashSet<Rigidbody> plasmaBlocks=new HashSet<Rigidbody>();
        bool blasterTesting;int nextPlasmaId;
        const float PlasmaSpeed=14,PlasmaRadius=.12f,BlastRadius=2.5f;
        [Serializable] public sealed class BlasterCommand{public Vector3 position,forward;}
        public bool BlasterTestingEnabled=>Authority?blasterTesting:frame.blaster?.testingEnabled==true;
        public void SetBlasterTesting(bool enabled)
        {
            if(!Authority||Rules.mode!=GameModeKind.Demo)return;
            blasterTesting=enabled;
            if(enabled&&!equipment.ContainsKey(LocalActorId))
            {
                equipment[LocalActorId]=new EquipmentState{actor=LocalActorId,kind=EquipmentKind.Blaster,remaining=20};
                AnnounceEquipment(LocalActorId,equipment[LocalActorId],EquipmentAction.Pickup,FindActor(LocalActorId)?.ViewPose.position??Vector3.zero);
            }
            if(!enabled&&EquipmentFor(LocalActorId).kind==EquipmentKind.Blaster)
            {
                var state=EquipmentFor(LocalActorId);
                if(state.drawn)AnnounceEquipment(LocalActorId,state,EquipmentAction.Holster,FindActor(LocalActorId)?.ViewPose.position??Vector3.zero);
                equipment.Remove(LocalActorId);
            }
            PublishEquipment();plasma.Clear();plasmaImpacts.Clear();shotCooldowns.Clear();frame.blaster=CaptureBlaster();
        }
        void ResetBlaster(){blasterTesting=false;plasma.Clear();plasmaImpacts.Clear();shotCooldowns.Clear();}
        public bool RequestBlasterShot(GameObject source,Pose muzzle)
        {
            if(!source||!actorIds.TryGetValue(source,out var id)||!BlasterTestingEnabled)return false;
            var command=new BlasterCommand{position=muzzle.position,forward=muzzle.rotation*Vector3.forward};
            if(!Authority){CommandRequested?.Invoke("blaster:"+JsonUtility.ToJson(command));return true;}
            return FireBlaster(id,command);
        }
        bool FireBlaster(string id,BlasterCommand command)
        {
            var actor=FindActor(id);var p=simulation.Find(id);
            if(EquipmentFor(id).kind!=EquipmentKind.Blaster||!EquipmentFor(id).drawn||!blasterTesting||Rules.mode!=GameModeKind.Demo||simulation.Phase!=RoundPhase.Playing||actor==null||p==null||!p.frame.alive||p.frame.dying||p.frame.role=="hand"||p.frame.role=="spectator"||simulation.CarriesFlag(id)||plasma.Count>=64||command==null)return false;
            if(!Finite(command.position)||!Finite(command.forward)||command.forward.sqrMagnitude<.5f||command.forward.sqrMagnitude>2||Vector3.Distance(command.position,actor.ViewPose.position)>2)return false;
            if(shotCooldowns.TryGetValue(id,out var ready)&&Time.time<ready)return false;
            if(Physics.Linecast(actor.ViewPose.position,command.position,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return false;
            shotCooldowns[id]=Time.time+.45f;
            plasma.Add(new Shot{id=++nextPlasmaId,owner=id,position=command.position,direction=command.forward.normalized,life=5});
            AnnounceEquipment(id,EquipmentFor(id),EquipmentAction.Shot,command.position);return true;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
        void TickBlaster(float dt)
        {
            if(!Authority)return;
            if(simulation.Phase!=RoundPhase.Playing)
            {plasma.Clear();plasmaImpacts.Clear();return;}
            for(int i=plasmaImpacts.Count-1;i>=0;i--){plasmaImpacts[i].remaining-=dt;if(plasmaImpacts[i].remaining<=0)plasmaImpacts.RemoveAt(i);}
            int mask=(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1;
            for(int i=plasma.Count-1;i>=0;i--)
            {
                var shot=plasma[i];float distance=PlasmaSpeed*Mathf.Min(dt,shot.life);shot.life-=dt;
                int overlaps=Physics.OverlapSphereNonAlloc(shot.position,PlasmaRadius,plasmaOverlaps,mask,QueryTriggerInteraction.Collide);bool touching=false;
                for(int h=0;h<overlaps;h++)
                {
                    var collider=plasmaOverlaps[h];var actor=collider.GetComponentInParent<IModeActor>();
                    if(actor!=null&&actor.ActorId==shot.owner||collider.isTrigger&&actor==null)continue;
                    touching=true;break;
                }
                if(touching){ExplodePlasma(shot,shot.position);plasma.RemoveAt(i);continue;}
                if(overlaps==plasmaOverlaps.Length){plasma.RemoveAt(i);continue;}
                int count=Physics.SphereCastNonAlloc(shot.position,PlasmaRadius,shot.direction,plasmaHits,distance,mask,QueryTriggerInteraction.Collide);
                if(count==plasmaHits.Length){plasma.RemoveAt(i);continue;}
                bool collided=false;float nearest=float.PositiveInfinity;Vector3 point=default;
                for(int h=0;h<count;h++)
                {
                    var hit=plasmaHits[h];var actor=hit.collider.GetComponentInParent<IModeActor>();
                    if(actor!=null&&actor.ActorId==shot.owner||hit.collider.isTrigger&&actor==null)continue;
                    if(hit.distance<nearest){nearest=hit.distance;point=hit.point+hit.normal*.04f;collided=true;}
                }
                if(collided){ExplodePlasma(shot,point);plasma.RemoveAt(i);}
                else if(shot.life<=0)plasma.RemoveAt(i);
                else shot.position+=shot.direction*distance;
            }
            frame.blaster=CaptureBlaster();
        }
        void ExplodePlasma(Shot shot,Vector3 point)
        {
            if(plasmaImpacts.Count>=64)plasmaImpacts.RemoveAt(0);plasmaImpacts.Add(new PlasmaImpactFrame{id=shot.id,position=point,remaining=.4f});
            AnnounceEquipment(shot.owner,new EquipmentState{kind=EquipmentKind.Blaster},EquipmentAction.Impact,point);
            var source=FindActor(shot.owner);if(source==null)return;
            foreach(var pair in actors)
            {
                var target=pair.Value;var p=simulation.Find(pair.Key);if(!NetworkActors.Alive(target)||p==null||!p.frame.alive||p.frame.dying||p.frame.role=="hand"||p.frame.role=="spectator")continue;
                var contact=target.transform.position+Vector3.up*.7f;var delta=contact-point;float distance=delta.magnitude;if(distance>BlastRadius)continue;
                if(Physics.Raycast(point,delta.normalized,distance,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))continue;
                if(!Rules.friendlyFire&&source.Team>=0&&source.Team==target.Team&&pair.Key!=shot.owner)continue;
                float falloff=Mathf.Lerp(1,.25f,Mathf.Clamp01(distance/BlastRadius));float damage=35*falloff;
                var push=(Vector3.ProjectOnPlane(delta,Vector3.up).normalized*4+Vector3.up)*falloff;
                if(target.IsLocal||target.IsBot){target.gameObject.GetComponent<IPushable>()?.Push(push,source.gameObject);target.gameObject.GetComponent<IDamageable>()?.Damage(damage,source.gameObject);}
                else{RecordRemoteDamage(pair.Key,damage,shot.owner);RemoteImpact?.Invoke(pair.Key,push,damage);}
            }
            plasmaBlocks.Clear();var blocks=Services.World?.Blocks;if(blocks==null)return;
            foreach(var body in blocks)if(body&&(body.worldCenterOfMass-point).sqrMagnitude<BlastRadius*BlastRadius&&plasmaBlocks.Add(body))
            {var delta=body.worldCenterOfMass-point;float distance=delta.magnitude;if(!Physics.Raycast(point,delta.normalized,out var hit,distance,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)||hit.rigidbody==body)body.GetComponent<IPushable>()?.Push((delta.normalized+Vector3.up*.2f)*Mathf.Lerp(4,1,distance/BlastRadius),source.gameObject);}
        }
        BlasterFrame CaptureBlaster()
        {
            var state=new BlasterFrame{enabled=blasterTesting||plasma.Count>0||plasmaImpacts.Count>0,testingEnabled=blasterTesting,shots=new PlasmaShotFrame[plasma.Count],impacts=new PlasmaImpactFrame[plasmaImpacts.Count]};
            for(int i=0;i<plasmaImpacts.Count;i++){var hit=plasmaImpacts[i];state.impacts[i]=new PlasmaImpactFrame{id=hit.id,position=hit.position,remaining=hit.remaining};}
            for(int i=0;i<plasma.Count;i++)state.shots[i]=new PlasmaShotFrame{id=plasma[i].id,position=plasma[i].position,velocity=plasma[i].direction*PlasmaSpeed};return state;
        }
    }
}

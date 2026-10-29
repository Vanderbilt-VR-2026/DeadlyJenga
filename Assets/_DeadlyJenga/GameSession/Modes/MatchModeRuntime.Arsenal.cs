using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime:IHeldEquipmentRuntime
    {
        sealed class HeldWeapon {public Pose pose;public float seen,start;public bool down;public int burst;}
        sealed class ArsenalProjectile {public int id;public string owner;public EquipmentKind kind;public Vector3 position,velocity;public float life,bornAt;}
        readonly Dictionary<string,HeldWeapon> heldWeapons=new Dictionary<string,HeldWeapon>();
        readonly List<ArsenalProjectile> arsenalProjectiles=new List<ArsenalProjectile>();
        readonly List<string> heldIds=new List<string>();int nextArsenalProjectile;
        readonly HashSet<IModeActor> arsenalVictims=new HashSet<IModeActor>();
        public bool RequestEquipmentTrigger(bool held,Pose muzzle)=>SendEquipment(new EquipmentCommand{action="trigger",held=held,position=muzzle.position,forward=muzzle.rotation*Vector3.forward});
        void StopArsenalTrigger(string id){heldWeapons.Remove(id);if(equipment.TryGetValue(id,out var s)){s.triggerHeld=false;s.charge=0;}}
        void ResetArsenal(){heldWeapons.Clear();arsenalProjectiles.Clear();frame.weaponProjectiles=Array.Empty<EquipmentProjectileFrame>();}
        bool HoldArsenalTrigger(string id,EquipmentState state,EquipmentCommand command)
        {
            if(state.kind<=EquipmentKind.PoweredArmor)return false;
            if(!command.held){StopArsenalTrigger(id);PublishEquipment();return true;}
            var actor=FindActor(id);if(!state.drawn||!Finite(command.position)||!Finite(command.forward)||command.forward.sqrMagnitude<.5f||command.forward.sqrMagnitude>2||(command.position-actor.ViewPose.position).sqrMagnitude>4)return false;
            if(!heldWeapons.TryGetValue(id,out var held)){held=new HeldWeapon{start=Time.time};heldWeapons[id]=held;}
            if(!held.down){held.start=Time.time;held.burst=0;}held.down=true;held.seen=Time.time;held.pose=new Pose(command.position,Quaternion.LookRotation(command.forward));return true;
        }
        void TickArsenal(float dt)
        {
            heldIds.Clear();heldIds.AddRange(heldWeapons.Keys);
            foreach(string id in heldIds)
            {
                var held=heldWeapons[id];var state=EquipmentFor(id);var actor=FindActor(id);
                if(actor==null||!actor.Alive||!state.drawn||Time.time-held.seen>.4f||simulation.Phase!=RoundPhase.Playing){StopArsenalTrigger(id);continue;}
                var spec=EquipmentSpecifications.Get(state.kind);float charge=state.kind==EquipmentKind.Chainsaw&&state.shotgun?1:spec.charge;
                state.triggerHeld=true;state.charge=charge>0?Mathf.Clamp01((Time.time-held.start)/charge):1;equipment[id]=state;
                if(state.kind==EquipmentKind.StockPistol&&state.shotgun&&held.burst>=3)continue;
                if(!spec.automatic&&!(state.kind==EquipmentKind.Chainsaw&&state.shotgun)&&held.burst>0)continue;
                if(AttackEquipment(id,new EquipmentCommand{position=held.pose.position,forward=held.pose.rotation*Vector3.forward})&&heldWeapons.TryGetValue(id,out held))held.burst++;
            }
            TickMagazines(dt);
            TickArsenalProjectiles(dt);
        }
        // The ordinary attack validator still checks actor, life, mode, muzzle and cover.
        bool AttackEquipment(string id,EquipmentCommand command)=>AttackEquipment(id,EquipmentFor(id),command);
        bool AttackArsenal(string id,EquipmentState state,EquipmentCommand command)
        {
            var spec=EquipmentSpecifications.Get(state.kind);float required=state.kind==EquipmentKind.Chainsaw&&state.shotgun?1:spec.charge;
            if(required>0)
            {
                if(!heldWeapons.TryGetValue(id,out var held)){heldWeapons[id]=new HeldWeapon{start=Time.time,seen=Time.time,pose=new Pose(command.position,Quaternion.LookRotation(command.forward))};return false;}
                if(Time.time-held.start<required)return false;
            }
            if(state.reloadRemaining>0||state.embedRemaining>0)return false;
            if(spec.magazine>0&&(!spec.melee||state.shotgun)){if(!state.magazineInitialized){state.rounds=spec.magazine;state.magazineInitialized=true;}if(state.rounds<=0){BeginMagazineReload(id,state);return false;}}
            if(weaponCooldown.TryGetValue(id,out float ready)&&Time.time<ready)return false;
            float cooldown=state.kind==EquipmentKind.StockPistol&&state.shotgun?.095f:state.kind==EquipmentKind.LaserAxe&&state.shotgun?1.2f:state.kind==EquipmentKind.Chainsaw&&state.shotgun?1:spec.cooldown;
            bool projectile=spec.projectileSpeed>0||state.kind==EquipmentKind.Chainsaw&&state.shotgun;
            if(projectile&&arsenalProjectiles.Count>=64)return false;
            weaponCooldown[id]=Time.time+cooldown;
            if(spec.magazine>0&&(!spec.melee||state.shotgun)){state.rounds--;equipment[id]=state;if(state.rounds==0)BeginMagazineReload(id,state);}
            var direction=command.forward.normalized;
            if(state.kind==EquipmentKind.DualUzi&&equipmentSequence%2==0&&FindActor(id)?.gameObject.GetComponent<IEnemyActor>()?.ArchetypeId!="mushroom")
            {
                var alternate=command.position+Quaternion.LookRotation(direction)*Vector3.right*(state.leftHanded?.35f:-.35f);
                if(Physics.Linecast(FindActor(id).ViewPose.position,alternate,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return true;command.position=alternate;
            }
            if(projectile)
            {
                arsenalProjectiles.Add(new ArsenalProjectile{id=++nextArsenalProjectile,owner=id,kind=state.kind,bornAt=Time.time,position=command.position,velocity=direction*(state.kind==EquipmentKind.Chainsaw?22:spec.projectileSpeed),life=state.kind==EquipmentKind.DualUzi?.85f:state.kind==EquipmentKind.GrenadeLauncher?3.5f:5});
                if(state.kind==EquipmentKind.Chainsaw&&heldWeapons.TryGetValue(id,out var h))h.start=Time.time;
                PublishArsenalState();AnnounceEquipment(id,state,EquipmentAction.Shot,command.position);return true;
            }
            float range=state.kind==EquipmentKind.LaserAxe&&state.shotgun?35:spec.range;
            float radius=state.kind==EquipmentKind.LaserAxe&&state.shotgun?.5f:spec.radius;
            float spread=state.kind==EquipmentKind.StockPistol?(state.shotgun?.002f:spec.spread*(1+Mathf.Min(3,heldWeapons.TryGetValue(id,out var recoil)?recoil.burst*.15f:0))):spec.spread;
            if(spread>0)direction=(direction+Quaternion.LookRotation(direction)*new Vector3(Mathf.Sin(equipmentSequence*2.4f),Mathf.Cos(equipmentSequence*1.7f),0)*spread).normalized;
            var source=FindActor(id);
            int count=Physics.SphereCastNonAlloc(command.position,radius,direction,plasmaHits,range,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
            if(count==plasmaHits.Length)return true;
            IModeActor target=null;float nearest=range;Vector3 endpoint=command.position+direction*range;
            if(spec.melee)
            {
                int touching=Physics.OverlapSphereNonAlloc(command.position,radius,plasmaOverlaps,(1<<Layers.Survivor)|(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Collide);
                if(touching==plasmaOverlaps.Length)return true;
                for(int i=0;i<touching;i++){var collider=plasmaOverlaps[i];var candidate=collider.GetComponentInParent<IModeActor>();if(candidate==source||collider.isTrigger&&candidate==null)continue;target=candidate;nearest=0;endpoint=command.position;break;}
            }
            for(int i=0;i<count;i++){var hit=plasmaHits[i];var candidate=hit.collider.GetComponentInParent<IModeActor>();if(candidate==source||hit.collider.isTrigger&&candidate==null||hit.distance>=nearest)continue;nearest=hit.distance;endpoint=command.position+direction*nearest;target=candidate;}
            AnnounceEquipment(id,state,EquipmentAction.Shot,command.position,new[]{endpoint});
            if(target!=null)
            {
                float damage=spec.damage;
                if(spec.melee&&!(state.kind==EquipmentKind.LaserAxe&&state.shotgun))
                {
                    if(state.kind==EquipmentKind.Dagger)damage=target.MaxHealth*(target.Health>=target.MaxHealth-.01f?.34f:.5f);
                    else if(state.kind==EquipmentKind.FlamingMarshmallow)damage=target.MaxHealth*.5f;
                    else if(state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw||state.kind==EquipmentKind.LaserAxe)damage=target.MaxHealth;
                }
                bool alive=target.Alive;ArsenalDamage(source,target,damage,direction*spec.push+Vector3.up*.4f);
                if(alive&&!target.Alive&&(state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw||state.kind==EquipmentKind.LaserAxe)&&!state.shotgun)
                {state.embeddedVictim=target.ActorId;state.embeddedPoint=endpoint;state.embedRemaining=.65f;equipment[id]=state;}
            }
            return true;
        }
        void BeginMagazineReload(string id,EquipmentState state)
        {
            if(state.reloadRemaining>0)return;
            state.reloadRemaining=EquipmentSpecifications.Get(state.kind).reloadSeconds;equipment[id]=state;
            AnnounceEquipment(id,state,EquipmentAction.Reload,FindActor(id)?.ViewPose.position??Vector3.zero);
        }
        void TickMagazines(float dt)
        {
            foreach(var pair in equipment)
            {
                var state=pair.Value;var spec=EquipmentSpecifications.Get(state.kind);
                if(spec.magazine>0&&!state.magazineInitialized){state.rounds=spec.magazine;state.magazineInitialized=true;}
                if(state.reloadRemaining>0){state.reloadRemaining=Mathf.Max(0,state.reloadRemaining-dt);if(state.reloadRemaining==0){state.rounds=spec.magazine;if(heldWeapons.TryGetValue(pair.Key,out var held)){held.start=Time.time;held.burst=0;}}}
                if(state.embedRemaining>0){state.embedRemaining=Mathf.Max(0,state.embedRemaining-dt);if(state.embedRemaining==0)state.embeddedVictim=null;}
            }
        }
        void ArsenalDamage(IModeActor source,IModeActor target,float damage,Vector3 push)
        {
            if(source==null||target==null)return;var p=simulation.Find(target.ActorId);
            if(p==null||!p.frame.alive||p.frame.dying||p.frame.role=="hand"||p.frame.role=="spectator"||source!=target&&source.Team>=0&&source.Team==target.Team&&!Rules.friendlyFire)return;
            if(MysteryActive&&source.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true&&target.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true&&MysteryPunch(source.ActorId,target.ActorId,ref damage))return;
            if(target.IsLocal||target.IsBot){target.gameObject.GetComponent<IPushable>()?.Push(push,source.gameObject);target.gameObject.GetComponent<IDamageable>()?.Damage(damage,source.gameObject);}
            else{RecordRemoteDamage(target.ActorId,damage,source.ActorId);RemoteImpact?.Invoke(target.ActorId,push,damage);}
        }
        void TickArsenalProjectiles(float dt)
        {
            if(simulation.Phase!=RoundPhase.Playing){arsenalProjectiles.Clear();frame.weaponProjectiles=Array.Empty<EquipmentProjectileFrame>();return;}
            for(int i=arsenalProjectiles.Count-1;i>=0;i--)
            {
                var shot=arsenalProjectiles[i];var spec=EquipmentSpecifications.Get(shot.kind);bool done=false;
                int overlaps=Physics.OverlapSphereNonAlloc(shot.position,Mathf.Max(.08f,spec.radius),plasmaOverlaps,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
                for(int h=0;h<overlaps;h++){var shape=plasmaOverlaps[h];var actor=shape.GetComponentInParent<IModeActor>();if(actor?.ActorId==shot.owner||shape.isTrigger&&actor==null)continue;ImpactArsenal(shot,actor);done=true;break;}
                if(done||overlaps==plasmaOverlaps.Length){arsenalProjectiles.RemoveAt(i);continue;}
                float elapsed=Mathf.Min(dt,Mathf.Max(0,Time.time-shot.bornAt));if(elapsed<=0)continue;
                int steps=Mathf.Clamp(Mathf.CeilToInt(elapsed/.025f),1,8);float step=elapsed/steps;
                for(int n=0;n<steps;n++)
                {
                    var delta=shot.velocity*step+Vector3.down*(spec.gravity*step*step*.5f);shot.velocity+=Vector3.down*spec.gravity*step;shot.life-=step;
                    int count=Physics.SphereCastNonAlloc(shot.position,Mathf.Max(.08f,spec.radius),delta.normalized,plasmaHits,delta.magnitude,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
                    if(count==plasmaHits.Length){done=true;break;}
                    float nearest=delta.magnitude;bool hit=false;IModeActor direct=null;
                    for(int h=0;h<count;h++){var contact=plasmaHits[h];var actor=contact.collider.GetComponentInParent<IModeActor>();if(actor?.ActorId==shot.owner||contact.collider.isTrigger&&actor==null||contact.distance>nearest)continue;nearest=contact.distance;hit=true;direct=actor;}
                    shot.position+=delta.normalized*nearest;
                    if(hit||shot.life<=0){ImpactArsenal(shot,direct);done=true;break;}
                }
                if(done)arsenalProjectiles.RemoveAt(i);
            }
        }
        void RestoreArsenal(MatchFrame saved)
        {
            foreach(var shot in saved.weaponProjectiles??Array.Empty<EquipmentProjectileFrame>()){arsenalProjectiles.Add(new ArsenalProjectile{id=shot.id,kind=shot.kind,owner=shot.owner,bornAt=Time.time,position=shot.position,velocity=shot.velocity,life=shot.remaining});nextArsenalProjectile=Mathf.Max(nextArsenalProjectile,shot.id);}
        }
        void PublishArsenalState()
        {
            frame.weaponProjectiles=new EquipmentProjectileFrame[arsenalProjectiles.Count];
            for(int i=0;i<arsenalProjectiles.Count;i++){var s=arsenalProjectiles[i];frame.weaponProjectiles[i]=new EquipmentProjectileFrame{id=s.id,kind=s.kind,owner=s.owner,position=s.position,velocity=s.velocity,remaining=s.life};}
        }
        void ImpactArsenal(ArsenalProjectile shot,IModeActor direct)
        {
            var source=FindActor(shot.owner);var spec=EquipmentSpecifications.Get(shot.kind);
            AnnounceEquipment(shot.owner,new EquipmentState{kind=shot.kind},EquipmentAction.Impact,shot.position);
            if(source==null)return;
            if(shot.kind==EquipmentKind.DualUzi){if(direct!=null)ArsenalDamage(source,direct,spec.damage,shot.velocity.normalized*spec.push);return;}
            if(shot.kind==EquipmentKind.Chainsaw){if(direct!=null)ArsenalDamage(source,direct,55,shot.velocity.normalized*8);return;}
            int count=Physics.OverlapSphereNonAlloc(shot.position,spec.blastRadius,plasmaOverlaps,(1<<Layers.Survivor)|(1<<Layers.Block),QueryTriggerInteraction.Collide);
            arsenalVictims.Clear();plasmaBlocks.Clear();
            for(int i=0;i<count;i++)
            {
                var shape=plasmaOverlaps[i];var target=shape.GetComponentInParent<IModeActor>();var point=shape.ClosestPoint(shot.position);float distance=Vector3.Distance(point,shot.position);
                if(distance>.05f&&Physics.Raycast(shot.position,(point-shot.position).normalized,distance-.03f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))continue;
                float falloff=Mathf.Lerp(1,.2f,Mathf.Clamp01(distance/spec.blastRadius));var push=((point-shot.position).normalized+Vector3.up*.35f)*spec.push*falloff;
                if(target!=null){if(arsenalVictims.Add(target))ArsenalDamage(source,target,spec.damage*falloff,push);}
                else{var body=HitAbilities.Get<Rigidbody>(shape);if(body&&plasmaBlocks.Add(body))HitAbilities.Get<IPushable>(shape)?.Push(push,source.gameObject);}
            }
        }
    }
}

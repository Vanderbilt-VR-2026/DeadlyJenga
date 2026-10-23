using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime : IEquipmentRuntime, IPoweredEquipmentRuntime
    {
        readonly Dictionary<string,EquipmentState> equipment=new Dictionary<string,EquipmentState>();
        readonly List<EquipmentPickup> pickups=new List<EquipmentPickup>();
        readonly Dictionary<IEquipmentPickupSource,int> authoredPickups=new Dictionary<IEquipmentPickupSource,int>();
        readonly Dictionary<string,float> weaponCooldown=new Dictionary<string,float>();
        readonly Dictionary<string,float> thrustSeen=new Dictionary<string,float>();
        int nextPickup;bool stationsSeeded;
        [Serializable] public sealed class EquipmentCommand
        {public string action;public int pickup;public bool swap,drawn,left,shotgun,thrust;public Vector3 position,forward;}

        public EquipmentState EquipmentFor(string id)
        {
            if(string.IsNullOrEmpty(id))return new EquipmentState();
            EquipmentState state=null;
            if(Authority)equipment.TryGetValue(id,out state);
            else foreach(var value in frame.equipment)if(value.actor==id){state=value;break;}
            return state?.Copy()??new EquipmentState{actor=id};
        }
        bool SendEquipment(EquipmentCommand command)
        {
            if(Authority)return EquipmentCommandFor(LocalActorId,command);
            CommandRequested?.Invoke("equipment:"+JsonUtility.ToJson(command));return true;
        }
        public bool RequestThrust(bool active)=>SendEquipment(new EquipmentCommand{action="thrust",thrust=active});
        public bool RequestPickup(int id,bool swap)=>SendEquipment(new EquipmentCommand{action="pickup",pickup=id,swap=swap});
        public bool RequestEquipment(bool drawn,bool leftHanded,bool shotgun)=>SendEquipment(new EquipmentCommand{action="state",drawn=drawn,left=leftHanded,shotgun=shotgun});
        public bool RequestEquipmentAttack(Pose muzzle)=>SendEquipment(new EquipmentCommand{action="attack",position=muzzle.position,forward=muzzle.rotation*Vector3.forward});
        bool EquipmentCommandFor(string id,EquipmentCommand command)
        {
            var actor=FindActor(id);var p=simulation.Find(id);
            if(command==null||actor==null||p==null||!p.frame.alive||p.frame.dying||p.frame.role=="hand"||p.frame.role=="spectator"||simulation.Phase!=RoundPhase.Playing||MysteryActive&&mystery.IsDowned(id))return false;
            if(command.action=="pickup")return TakeEquipment(id,command.pickup,command.swap);
            var state=EquipmentFor(id);if(state.kind==EquipmentKind.None)return false;
            if(command.action=="thrust")
            {
                if(state.kind!=EquipmentKind.PoweredArmor||command.thrust&&!state.drawn)return false;
                state.thrust=command.thrust;equipment[id]=state;thrustSeen[id]=Time.time;PublishEquipment();return true;
            }
            if(command.action=="state")
            {
                if(command.drawn&&(state.kind<EquipmentKind.Blaster||simulation.CarriesFlag(id)))return false;
                var mover=actor.gameObject.GetComponent<IMovementState>();
                if(command.drawn&&mover!=null&&(mover.Climbing||mover.Vaulting))return false;
                bool drawChanged=state.drawn!=command.drawn,modeChanged=state.kind==EquipmentKind.DualRifle&&state.shotgun!=command.shotgun;
                state.drawn=command.drawn;if(!state.drawn)state.thrust=false;state.leftHanded=command.left;state.shotgun=state.kind==EquipmentKind.DualRifle&&command.shotgun;equipment[id]=state;
                if(drawChanged)AnnounceEquipment(id,state,state.drawn?EquipmentAction.Draw:EquipmentAction.Holster,actor.ViewPose.position);
                if(modeChanged)AnnounceEquipment(id,state,EquipmentAction.ModeSwitch,actor.ViewPose.position);
                if(state.roleIssued&&MysteryActive)mystery.SetSpecial(id,state.drawn);
                PublishEquipment();return true;
            }
            return command.action=="attack"&&AttackEquipment(id,state,command);
        }
        public int RegisterPickup(IEquipmentPickupSource source)
        {
            if(!Authority||source==null||source.Kind<EquipmentKind.Speed||source.Kind>EquipmentKind.PoweredArmor||!Finite(source.Position)||(float.IsNaN(source.Duration)||float.IsInfinity(source.Duration)))return 0;
            if(authoredPickups.TryGetValue(source,out int id))return id;
            var pickup=AddPickup(source.Kind,source.Position,Mathf.Clamp(source.Duration,.1f,20));if(pickup==null)return 0;
            authoredPickups[source]=pickup.id;PublishEquipment();return pickup.id;
        }
        EquipmentPickup AddPickup(EquipmentKind kind,Vector3 point,float remaining=20,bool station=false)
        {
            if(pickups.Count>=128||!Finite(point))return null;
            var pickup=new EquipmentPickup{id=++nextPickup,kind=kind,position=point,remaining=remaining,station=station};pickups.Add(pickup);return pickup;
        }
        bool TakeEquipment(string id,int pickupId,bool swap)
        {
            var actor=FindActor(id);var pickup=pickups.Find(x=>x.id==pickupId);
            if(actor==null||pickup==null||pickup.availableIn>0||Vector3.Distance(actor.transform.position,pickup.position)>1.65f)return false;
            var delta=pickup.position-actor.ViewPose.position;
            if(Physics.Raycast(actor.ViewPose.position,delta.normalized,delta.magnitude*.92f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return false;
            var previous=EquipmentFor(id);
            if(previous.kind!=EquipmentKind.None&&!swap)return false;
            if(previous.kind!=EquipmentKind.None)
            {
                // Keep the old pickup id for a station and create exactly one dropped item.
                if(pickups.Count>=128)return false;
                var dropped=AddPickup(previous.kind,pickup.position,previous.remaining);dropped.availableIn=1;
            }
            thrustSeen.Remove(id);
            equipment[id]=new EquipmentState{actor=id,kind=pickup.kind,remaining=pickup.remaining,leftHanded=previous.leftHanded};
            AnnounceEquipment(id,equipment[id],previous.kind==EquipmentKind.None?EquipmentAction.Pickup:EquipmentAction.Swap,pickup.position);
            if(pickup.station)pickup.availableIn=15;else pickups.Remove(pickup);
            if(MysteryActive)mystery.SetSpecial(id,false);
            PublishEquipment();return true;
        }
        void DropEquipment(string id,Vector3 point)
        {
            if(!equipment.TryGetValue(id,out var state))return;
            if(pickups.Count>=128){var oldest=pickups.Find(x=>!x.station);if(oldest!=null)pickups.Remove(oldest);}
            if(state.kind!=EquipmentKind.None){var drop=AddPickup(state.kind,point+Vector3.up*.2f,state.remaining);if(drop!=null)drop.availableIn=1;}
            if(state.kind!=EquipmentKind.None)AnnounceEquipment(id,state,EquipmentAction.Drop,point);
            equipment.Remove(id);weaponCooldown.Remove(id);thrustSeen.Remove(id);PublishEquipment();
        }
        public bool SpawnEquipment(EquipmentKind kind)
        {
            if(!Authority||Rules.mode!=GameModeKind.Demo||kind<EquipmentKind.Speed||kind>EquipmentKind.PoweredArmor||session.PlayingHand||DemoSpectating)return false;
            var actor=FindActor(LocalActorId);if(actor==null)return false;
            var direction=Vector3.ProjectOnPlane(actor.ViewPose.rotation*Vector3.forward,Vector3.up).normalized;
            var point=actor.transform.position+direction*1.8f+Vector3.up*.35f;
            if(Physics.CheckSphere(point,.2f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return false;
            bool added=AddPickup(kind,point)!=null;PublishEquipment();return added;
        }
        void TickEquipment(float dt)
        {
            if(!Authority)return;
            if(!stationsSeeded&&Services.World!=null&&(MatchSetup.MapId=="mode-powerup-test"||MatchSetup.MapId=="powerup-test"))
            {
                stationsSeeded=true;
                for(int i=1;i<=8;i++)
                {
                    var origin=Services.World.Center+new Vector3((i-4.5f)*2.5f,20,-7);
                    if(Physics.Raycast(origin,Vector3.down,out var hit,40,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore))AddPickup((EquipmentKind)i,hit.point+Vector3.up*.35f,20,true);
                }
            }
            foreach(var pickup in pickups)pickup.availableIn=Mathf.Max(0,pickup.availableIn-dt);
            foreach(var pair in equipment.ToArray())
            {
                var actor=FindActor(pair.Key);var p=simulation.Find(pair.Key);
                if(actor==null){equipment.Remove(pair.Key);thrustSeen.Remove(pair.Key);continue;}
                if(p==null||!p.frame.alive||p.frame.dying||!actor.Alive){DropEquipment(pair.Key,actor.transform.position);continue;}
                var state=pair.Value;
                if(state.thrust&&(simulation.Phase!=RoundPhase.Playing||!state.drawn||!thrustSeen.TryGetValue(pair.Key,out float seen)||Time.time-seen>1.2f))state.thrust=false;
                if(state.kind==EquipmentKind.Speed||state.kind==EquipmentKind.Jump){state.remaining-=dt;if(state.remaining<=0)equipment.Remove(pair.Key);}
                if(simulation.CarriesFlag(pair.Key)&&state.drawn){state.drawn=false;state.thrust=false;AnnounceEquipment(pair.Key,state,EquipmentAction.Holster,actor.ViewPose.position);}
            }
            // Role weapons share the slot, but concealed roles never enter the public snapshot.
            if(MysteryActive)foreach(var p in simulation.Participants)
            {
                if(!p.frame.alive||p.frame.dying)continue;
                var personal=mystery.Personal(p.frame.id);var kind=personal.role==MysteryRole.Murderer?EquipmentKind.MurderSword:personal.role==MysteryRole.Sheriff?EquipmentKind.SheriffHammer:EquipmentKind.None;
                if(kind==EquipmentKind.None){if(equipment.TryGetValue(p.frame.id,out var old)&&old.roleIssued)equipment.Remove(p.frame.id);continue;}
                if(!equipment.ContainsKey(p.frame.id))equipment[p.frame.id]=new EquipmentState{actor=p.frame.id,kind=kind,roleIssued=true,drawn=personal.specialAttack,remaining=20};
                else if(equipment[p.frame.id].roleIssued)
                {
                    var state=equipment[p.frame.id];bool changed=state.drawn!=personal.specialAttack;
                    state.kind=kind;state.drawn=personal.specialAttack;
                    if(changed)AnnounceEquipment(p.frame.id,state,state.drawn?EquipmentAction.Draw:EquipmentAction.Holster,FindActor(p.frame.id)?.ViewPose.position??p.frame.position);
                }
            }
            PublishEquipment();
        }
        void PublishEquipment()
        {
            frame.equipment=equipment.Values.Where(x=>!x.roleIssued||x.drawn).Select(x=>x.Copy()).ToArray();
            frame.pickups=pickups.Select(x=>new EquipmentPickup{id=x.id,kind=x.kind,position=x.position,remaining=x.remaining,availableIn=x.availableIn,station=x.station}).ToArray();
            PublishEquipmentAnnouncements();ruleCache.Clear();
        }
        void ResetEquipment(){ResetEquipmentAnnouncements();equipment.Clear();pickups.Clear();authoredPickups.Clear();weaponCooldown.Clear();thrustSeen.Clear();stationsSeeded=false;PublishEquipment();}
        void RestoreEquipment(MatchFrame saved)
        {
            equipmentAnnouncements.Clear();equipmentEpoch=string.IsNullOrEmpty(saved.equipmentEpoch)?Guid.NewGuid().ToString("N"):saved.equipmentEpoch;
            equipmentSequence=Mathf.Max(0,saved.equipmentSequence);
            equipment.Clear();pickups.Clear();authoredPickups.Clear();weaponCooldown.Clear();thrustSeen.Clear();nextPickup=0;
            foreach(var state in saved.equipment??Array.Empty<EquipmentState>()){var copy=state.Copy();copy.thrust=false;equipment[state.actor]=copy;}
            foreach(var pickup in saved.pickups??Array.Empty<EquipmentPickup>()){pickups.Add(pickup);nextPickup=Mathf.Max(nextPickup,pickup.id);}
            stationsSeeded=pickups.Exists(x=>x.station);
        }
        bool AttackEquipment(string id,EquipmentState state,EquipmentCommand command)
        {
            var source=FindActor(id);
            if(!state.drawn||state.kind<EquipmentKind.Blaster||simulation.CarriesFlag(id)||!Finite(command.position)||!Finite(command.forward)||command.forward.sqrMagnitude<.5f||command.forward.sqrMagnitude>2||Vector3.Distance(command.position,source.ViewPose.position)>2)return false;
            var mover=source.gameObject.GetComponent<IMovementState>();if(mover!=null&&(mover.Climbing||mover.Vaulting))return false;
            if(weaponCooldown.TryGetValue(id,out float ready)&&Time.time<ready)return false;
            if(Physics.Linecast(source.ViewPose.position,command.position,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore))return false;
            if(state.roleIssued)return Punch(id,command.position,command.forward,4);
            float cooldown=state.kind==EquipmentKind.DualRifle?(state.shotgun?.85f:.65f):(state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)?.45f:.9f;
            weaponCooldown[id]=Time.time+cooldown;
            if(state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)
            {
                if(plasma.Count>=64)return false;
                plasma.Add(new Shot{id=++nextPlasmaId,owner=id,position=command.position,direction=command.forward.normalized,life=5});
                AnnounceEquipment(id,state,EquipmentAction.Shot,command.position);return true;
            }
            bool melee=state.kind!=EquipmentKind.DualRifle;float range=melee?1.4f:state.shotgun?14:65;
            var direction=command.forward.normalized;
            int pellets=state.kind==EquipmentKind.DualRifle&&state.shotgun?7:1;
            var hits=new Dictionary<IModeActor,float>();var endpoints=new Vector3[pellets];
            for(int pellet=0;pellet<pellets;pellet++)
            {
                var aim=direction;
                if(pellets>1){var q=Quaternion.LookRotation(direction);float angle=pellet*Mathf.PI*2/6;aim=(direction+q*(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(pellet==6?0:.09f))).normalized;}
                int count=Physics.SphereCastNonAlloc(command.position,melee?.16f:.025f,aim,plasmaHits,range,(1<<Layers.Surface)|(1<<Layers.Block)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
                if(count==plasmaHits.Length){endpoints[pellet]=command.position;continue;}
                IModeActor target=null;float nearest=range;Vector3 endpoint=command.position+aim*range;
                for(int i=0;i<count;i++)
                {
                    var hit=plasmaHits[i];var candidate=hit.collider.GetComponentInParent<IModeActor>();
                    if(candidate==source||hit.collider.isTrigger&&candidate==null||hit.distance>=nearest)continue;
                    nearest=hit.distance;target=candidate;endpoint=hit.point;
                    if(!Finite(endpoint)||(endpoint-command.position).sqrMagnitude>(range+.05f)*(range+.05f))endpoint=command.position+aim*nearest;
                }
                endpoints[pellet]=endpoint;
                if(target!=null)hits[target]=(hits.TryGetValue(target,out float damage)?damage:0)+(state.kind==EquipmentKind.BoxingGlove?80:state.kind==EquipmentKind.MurderSword?60:state.kind==EquipmentKind.SheriffHammer?50:state.shotgun?13:55);
            }
            AnnounceEquipment(id,state,EquipmentAction.Shot,command.position,endpoints);
            foreach(var hit in hits)
            {
                var target=hit.Key;var p=simulation.Find(target.ActorId);if(p==null||!p.frame.alive||p.frame.dying||target.Team>=0&&target.Team==source.Team&&!Rules.friendlyFire)continue;
                var push=direction*(state.kind==EquipmentKind.BoxingGlove?22:4)+Vector3.up*(state.kind==EquipmentKind.BoxingGlove?6:.6f);
                if(target.IsLocal||target.IsBot){target.gameObject.GetComponent<IPushable>()?.Push(push,source.gameObject);target.gameObject.GetComponent<IDamageable>()?.Damage(hit.Value,source.gameObject);}
                else{RecordRemoteDamage(target.ActorId,hit.Value,id);RemoteImpact?.Invoke(target.ActorId,push,hit.Value);}
            }
            return true;
        }
    }
}

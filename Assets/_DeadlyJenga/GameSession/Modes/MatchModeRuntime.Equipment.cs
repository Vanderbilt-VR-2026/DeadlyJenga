using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime : IEquipmentRuntime, IPoweredEquipmentRuntime, IDemoEquipmentControls
    {
        readonly Dictionary<string,bool> equipmentHands=new Dictionary<string,bool>();
        readonly Dictionary<string,EquipmentState> equipment=new Dictionary<string,EquipmentState>();
        readonly List<EquipmentPickup> pickups=new List<EquipmentPickup>();
        readonly Dictionary<IEquipmentPickupSource,int> authoredPickups=new Dictionary<IEquipmentPickupSource,int>();
        readonly Dictionary<string,float> weaponCooldown=new Dictionary<string,float>();
        readonly Dictionary<string,float> thrustSeen=new Dictionary<string,float>();
        readonly HashSet<string> sheriffDrops=new HashSet<string>();
        readonly List<KeyValuePair<string,EquipmentState>> equipmentTick=new List<KeyValuePair<string,EquipmentState>>();
        int nextPickup;bool stationsSeeded;
        [Serializable] public sealed class EquipmentCommand
        {public string action;public int pickup;public bool swap,drawn,left,shotgun,thrust,held;public Vector3 position,forward;}

        public EquipmentState EquipmentFor(string id)
        {
            if(string.IsNullOrEmpty(id))return new EquipmentState();
            EquipmentState state=null;
            if(Authority)equipment.TryGetValue(id,out state);
            else foreach(var value in frame.equipment)if(value.actor==id){state=value;break;}
            if((state==null||state.kind==EquipmentKind.Speed||state.kind==EquipmentKind.Jump)&&Rules.defaultDagger&&Rules.punchEnabled&&simulation?.Phase==RoundPhase.Playing)
            {
                var p=simulation.Find(id);if(p!=null&&p.frame.alive&&!p.frame.dying&&p.frame.role!="hand"&&p.frame.role!="spectator"&&FindActor(id)?.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true)
                    return new EquipmentState{actor=id,kind=EquipmentKind.Dagger,activePower=state?.kind??EquipmentKind.None,remaining=state?.remaining??20,drawn=true,builtIn=true,leftHanded=equipmentHands.TryGetValue(id,out bool left)?left:FindActor(id)?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded??state?.leftHanded??false};
            }
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
            if(command.action=="trigger")return HoldArsenalTrigger(id,state,command);
            if(command.action=="thrust")
            {
                if(state.kind!=EquipmentKind.PoweredArmor||command.thrust&&!state.drawn)return false;
                state.thrust=command.thrust;equipment[id]=state;thrustSeen[id]=Time.time;PublishEquipment();return true;
            }
            if(command.action=="state")
            {
                equipmentHands[id]=command.left;
                if(state.builtIn){if(!command.drawn)return false;if(equipment.TryGetValue(id,out var power))power.leftHanded=command.left;PublishEquipment();return true;}
                if(command.drawn&&(state.kind<EquipmentKind.Blaster||simulation.CarriesFlag(id)))return false;
                var mover=actor.gameObject.GetComponent<IMovementState>();
                if(command.drawn&&mover!=null&&(mover.Climbing||mover.Vaulting))return false;
                bool dual=EquipmentSpecifications.Get(state.kind).modeSwitch;
                bool drawChanged=state.drawn!=command.drawn,modeChanged=dual&&state.shotgun!=command.shotgun;
                if(state.kind==EquipmentKind.Dagger&&!command.drawn)return false;
                StopArsenalTrigger(id);state.triggerHeld=false;state.charge=0;
                state.drawn=command.drawn;if(!state.drawn)state.thrust=false;state.leftHanded=command.left;state.shotgun=dual&&command.shotgun;equipment[id]=state;
                if(modeChanged)weaponCooldown[id]=Mathf.Max(Time.time+.4f,weaponCooldown.TryGetValue(id,out float ready)?ready:0);
                if(drawChanged)AnnounceEquipment(id,state,state.drawn?EquipmentAction.Draw:EquipmentAction.Holster,actor.ViewPose.position);
                if(modeChanged)AnnounceEquipment(id,state,EquipmentAction.ModeSwitch,actor.ViewPose.position);
                if(state.roleIssued&&MysteryActive)mystery.SetSpecial(id,state.drawn);
                PublishEquipment();return true;
            }
            return command.action=="attack"&&AttackEquipment(id,state,command);
        }
        public int RegisterPickup(IEquipmentPickupSource source)
        {
            if(!Authority||source==null||source.Kind<EquipmentKind.Speed||source.Kind>EquipmentKind.Dagger||!Finite(source.Position)||(float.IsNaN(source.Duration)||float.IsInfinity(source.Duration)))return 0;
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
            if(actor==null||pickup==null||pickup.kind==EquipmentKind.FlyingHand&&actor.IsBot||pickup.availableIn>0||Vector3.Distance(actor.transform.position,pickup.position)>1.65f)return false;
            var delta=pickup.position-actor.ViewPose.position;
            if(Physics.Raycast(actor.ViewPose.position,delta.normalized,delta.magnitude*.92f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return false;
            var previous=equipment.TryGetValue(id,out var heldItem)?heldItem.Copy():EquipmentFor(id);
            if(previous.kind!=EquipmentKind.None&&!previous.builtIn&&!swap)return false;
            if(MysteryActive&&pickup.kind==EquipmentKind.SheriffHammer&&mystery.Role(id)!=MysteryRole.Sheriff&&!mystery.CanRecoverDroppedSheriff(id))return false;
            if(previous.kind!=EquipmentKind.None&&!previous.builtIn)
            {
                // Keep the old pickup id for a station and create exactly one dropped item.
                if(pickups.Count>=128)return false;
                var dropped=AddPickup(previous.kind,pickup.position,previous.remaining);dropped.availableIn=1;dropped.rounds=previous.magazineInitialized?previous.rounds:-1;
            }
            StopArsenalTrigger(id);thrustSeen.Remove(id);
            sheriffDrops.Remove(id);
            if(FindActor(id)?.IsBot==true)botPickupReceipts[(id,pickup.id)]=Time.time;
            equipment[id]=new EquipmentState{actor=id,kind=pickup.kind,remaining=pickup.remaining,rounds=Mathf.Max(0,pickup.rounds),magazineInitialized=pickup.rounds>=0,leftHanded=previous.leftHanded};
            if(MysteryActive&&pickup.kind==EquipmentKind.SheriffHammer){mystery.RecoverDroppedSheriff(id);equipment[id].roleIssued=true;}
            AnnounceEquipment(id,equipment[id],previous.kind==EquipmentKind.None?EquipmentAction.Pickup:EquipmentAction.Swap,pickup.position);
            if(pickup.station)pickup.availableIn=15;else pickups.Remove(pickup);
            randomPickups.Remove(pickup.id);
            if(pickup.kind==EquipmentKind.FlyingHand){equipment.Remove(id);simulation.HandPower(id,30);}
            if(MysteryActive)mystery.SetSpecial(id,false);
            PublishEquipment();return true;
        }
        void DropEquipment(string id,Vector3 point)
        {
            if(sheriffDrops.Contains(id))return;
            StopArsenalTrigger(id);
            if(!equipment.TryGetValue(id,out var state))
            {
                if(!MysteryActive||mystery.Role(id)!=MysteryRole.Sheriff)return;
                state=new EquipmentState{actor=id,kind=EquipmentKind.SheriffHammer,remaining=20,roleIssued=true};
            }
            if(state.kind==EquipmentKind.SheriffHammer)sheriffDrops.Add(id);
            if(pickups.Count>=128){var oldest=pickups.Find(x=>!x.station);if(oldest!=null)pickups.Remove(oldest);}
            if(state.kind!=EquipmentKind.None){var drop=AddPickup(state.kind,point+Vector3.up*.2f,state.remaining);if(drop!=null){drop.availableIn=1;drop.rounds=state.magazineInitialized?state.rounds:-1;}}
            if(state.kind!=EquipmentKind.None)AnnounceEquipment(id,state,EquipmentAction.Drop,point);
            equipment.Remove(id);weaponCooldown.Remove(id);thrustSeen.Remove(id);PublishEquipment();
        }
        public bool SpawnEquipment(EquipmentKind kind)
        {
            if(!Authority||kind<EquipmentKind.Speed||kind>EquipmentKind.FlyingHand||session.PlayingHand||DemoSpectating)return false;
            var actor=FindActor(LocalActorId);if(actor==null)return false;
            var direction=Vector3.ProjectOnPlane(actor.ViewPose.rotation*Vector3.forward,Vector3.up).normalized;
            var point=actor.transform.position+direction*1.8f+Vector3.up*.35f;
            if(Physics.CheckSphere(point,.2f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return false;
            bool added=AddPickup(kind,point)!=null;PublishEquipment();return added;
        }
        public bool GiveEquipment(EquipmentKind kind)
        {
            if(!Authority||kind<EquipmentKind.Speed||kind>EquipmentKind.FlyingHand||session.PlayingHand||DemoSpectating||simulation.Phase!=RoundPhase.Playing)return false;
            string id=LocalActorId;var actor=FindActor(id);var participant=simulation.Find(id);
            if(actor==null||participant==null||!participant.frame.alive||participant.frame.dying)return false;
            return GiveEquipmentTo(id,kind,true,true);
        }
        bool GiveEquipmentTo(string id,EquipmentKind kind,bool dropPrevious,bool publish)
        {
            var actor=FindActor(id);if(actor==null)return false;
            if(kind==EquipmentKind.FlyingHand){if(actor.IsBot)return false;DropEquipment(id,actor.transform.position);AnnounceEquipment(id,new EquipmentState{kind=kind},EquipmentAction.Pickup,actor.transform.position);simulation.HandPower(id,30);return true;}
            var previous=equipment.TryGetValue(id,out var heldItem)?heldItem.Copy():EquipmentFor(id);
            if(dropPrevious&&previous.kind!=EquipmentKind.None&&!previous.builtIn&&previous.kind!=kind)
            {
                if(pickups.Count>=128)return false;
                DropEquipment(id,actor.transform.position);
            }
            var movement=actor.gameObject.GetComponent<IMovementState>();
            var state=new EquipmentState{actor=id,kind=kind,remaining=20,leftHanded=actor.gameObject.GetComponent<IBlasterControls>()?.LeftHanded??previous.leftHanded,shotgun=previous.kind==kind&&previous.shotgun,
                drawn=kind>=EquipmentKind.Blaster&&!simulation.CarriesFlag(id)&&(movement==null||!movement.Climbing&&!movement.Vaulting)};
            StopArsenalTrigger(id);equipment[id]=state;weaponCooldown.Remove(id);thrustSeen.Remove(id);sheriffDrops.Remove(id);
            AnnounceEquipment(id,state,previous.kind==EquipmentKind.None?EquipmentAction.Pickup:EquipmentAction.Swap,actor.ViewPose.position);
            if(publish)PublishEquipment();return true;
        }
        void TickEquipment(float dt)
        {
            if(!Authority)return;
            if(!stationsSeeded&&Services.World!=null&&(MatchSetup.MapId=="mode-powerup-test"||MatchSetup.MapId=="powerup-test"))
            {
                stationsSeeded=true;
                for(int i=1;i<EquipmentSpecifications.All.Length;i++)
                {
                    var origin=Services.World.Center+new Vector3(((i-1)%8-3.5f)*2.5f,20,-7+(i-1)/8*3);
                    if(Physics.Raycast(origin,Vector3.down,out var hit,40,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore))AddPickup((EquipmentKind)i,hit.point+Vector3.up*.35f,20,true);
                }
            }
            foreach(var enemy in simulation.Enemies){var actor=FindActor(enemy.frame.id);if(actor?.gameObject.GetComponent<IEnemyActor>()?.ArchetypeId=="mushroom"&&!equipment.ContainsKey(enemy.frame.id))GiveEquipmentTo(enemy.frame.id,EquipmentKind.DualUzi,false,false);}
            foreach(var pickup in pickups)pickup.availableIn=Mathf.Max(0,pickup.availableIn-dt);
            equipmentTick.Clear();equipmentTick.AddRange(equipment);
            foreach(var pair in equipmentTick)
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
            TickDemoLoadout();TickArsenal(dt);PublishEquipment();
        }
        void PublishEquipment()
        {
            PublishArsenalState();frame.demoRespawnEquipment=respawnEquipment;
            var published=new List<EquipmentState>();foreach(var item in equipment.Values)if(!item.roleIssued||item.drawn)published.Add(EquipmentFor(item.actor));
            if(Authority&&Rules.defaultDagger)foreach(var p in simulation.Participants)if(!equipment.ContainsKey(p.frame.id)){var fallback=EquipmentFor(p.frame.id);if(fallback.builtIn)published.Add(fallback);}
            frame.equipment=published.ToArray();
            frame.pickups=pickups.Select(x=>new EquipmentPickup{id=x.id,rounds=x.rounds,kind=x.kind,position=x.position,remaining=x.remaining,availableIn=x.availableIn,station=x.station,randomSpawn=x.randomSpawn,supportBlock=x.supportBlock,supportLocal=x.supportLocal,expiresIn=x.expiresIn}).ToArray();
            PublishEquipmentAnnouncements();WritePowerupFrame();ruleCache.Clear();
        }
        void ResetEquipment(){ResetArsenal();loadoutAlive.Clear();ResetRandomPickups();ResetEquipmentAnnouncements();equipment.Clear();pickups.Clear();authoredPickups.Clear();weaponCooldown.Clear();thrustSeen.Clear();sheriffDrops.Clear();stationsSeeded=false;PublishEquipment();}
        void RestoreEquipment(MatchFrame saved)
        {
            ResetArsenal();respawnEquipment=saved.demoRespawnEquipment;loadoutAlive.Clear();foreach(var p in saved.players)loadoutAlive[p.id]=p.alive&&!p.dying&&p.role!="hand"&&p.role!="spectator";
            ResetRandomPickups();sheriffDrops.Clear();
            if(ValidPowerupSpawns(saved.powerupSpawns)){spawnSettings=CopySpawnSettings(saved.powerupSpawns);spawnMode=Rules.mode;nextRandomPickup=Time.time+saved.nextPowerupIn;}
            equipmentAnnouncements.Clear();equipmentEpoch=string.IsNullOrEmpty(saved.equipmentEpoch)?Guid.NewGuid().ToString("N"):saved.equipmentEpoch;
            equipmentSequence=Mathf.Max(0,saved.equipmentSequence);
            equipment.Clear();pickups.Clear();authoredPickups.Clear();weaponCooldown.Clear();thrustSeen.Clear();nextPickup=0;
            foreach(var state in saved.equipment??Array.Empty<EquipmentState>()){var copy=state.Copy();copy.thrust=false;copy.triggerHeld=false;copy.charge=0;equipmentHands[state.actor]=copy.leftHanded;
                if(copy.builtIn&&copy.activePower!=EquipmentKind.None){copy.kind=copy.activePower;copy.activePower=EquipmentKind.None;copy.builtIn=false;copy.drawn=false;}
                if(!copy.builtIn)equipment[state.actor]=copy;}
            foreach(var pickup in saved.pickups??Array.Empty<EquipmentPickup>()){pickups.Add(pickup);nextPickup=Mathf.Max(nextPickup,pickup.id);}
            RestoreArsenal(saved);RestoreRandomPickups();stationsSeeded=pickups.Exists(x=>x.station);
        }
        bool AttackEquipment(string id,EquipmentState state,EquipmentCommand command)
        {
            var source=FindActor(id);
            var participant=simulation.Find(id);
            if(participant==null||!participant.frame.alive||participant.frame.dying||simulation.Phase!=RoundPhase.Playing||MysteryActive&&mystery.IsDowned(id))return false;
            if(source==null||!state.drawn||state.kind<EquipmentKind.Blaster||simulation.CarriesFlag(id)||!Finite(command.position)||!Finite(command.forward)||command.forward.sqrMagnitude<.5f||command.forward.sqrMagnitude>2||Vector3.Distance(command.position,source.ViewPose.position)>2)return false;
            var mover=source.gameObject.GetComponent<IMovementState>();if(mover!=null&&(mover.Climbing||mover.Vaulting))return false;
            if(Physics.Linecast(source.ViewPose.position,command.position,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore))return false;
            if(state.kind>EquipmentKind.PoweredArmor)return AttackArsenal(id,state,command);
            var specification=EquipmentSpecifications.Get(state.kind);
            if(specification.magazine>0){if(!state.magazineInitialized){state.magazineInitialized=true;state.rounds=specification.magazine;}if(state.reloadRemaining>0)return false;if(state.rounds==0){BeginMagazineReload(id,state);return false;}}
            if(weaponCooldown.TryGetValue(id,out float ready)&&Time.time<ready)return false;
            if(state.roleIssued&&state.kind!=EquipmentKind.SheriffHammer)return Punch(id,command.position,command.forward,4);
            if(MysteryActive&&mystery.Personal(id).attackReadyIn>0)return false;
            float cooldown=state.kind==EquipmentKind.DualRifle?(state.shotgun?.20f:.65f):state.kind==EquipmentKind.SheriffHammer?(state.shotgun?.28f:.75f):(state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)?.45f:.9f;
            weaponCooldown[id]=Time.time+cooldown;
            if(specification.magazine>0){state.rounds--;equipment[id]=state;if(state.rounds==0)BeginMagazineReload(id,state);}
            if(state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)
            {
                if(plasma.Count>=64)return false;
                plasma.Add(new Shot{id=++nextPlasmaId,owner=id,position=command.position,direction=command.forward.normalized,life=5});
                AnnounceEquipment(id,state,EquipmentAction.Shot,command.position);return true;
            }
            bool revolver=state.kind==EquipmentKind.SheriffHammer;
            bool melee=state.kind!=EquipmentKind.DualRifle&&!revolver;float range=melee?1.4f:state.shotgun?(revolver?18:14):(revolver?90:65);
            var direction=command.forward.normalized;
            int pellets=!melee&&state.shotgun?7:1;
            var hits=new Dictionary<IModeActor,float>();var endpoints=new Vector3[pellets];
            for(int pellet=0;pellet<pellets;pellet++)
            {
                var aim=direction;
                if(pellets>1){var q=Quaternion.LookRotation(direction);float angle=pellet*Mathf.PI*2/6;aim=(direction+q*(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(pellet==6?0:state.kind==EquipmentKind.DualRifle?.15f:.09f))).normalized;}
                float shotRadius=melee?.16f:state.kind==EquipmentKind.DualRifle?(state.shotgun?.10f:.075f):.025f;
                int count=Physics.SphereCastNonAlloc(command.position,shotRadius,aim,plasmaHits,range,(1<<Layers.Surface)|(1<<Layers.Block)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
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
                if(target!=null)hits[target]=(hits.TryGetValue(target,out float damage)?damage:0)+(state.kind==EquipmentKind.BoxingGlove?80:state.kind==EquipmentKind.MurderSword?60:revolver?(state.shotgun?10:85):state.shotgun?13:55);
            }
            AnnounceEquipment(id,state,EquipmentAction.Shot,command.position,endpoints);
            foreach(var hit in hits)
            {
                var target=hit.Key;var p=simulation.Find(target.ActorId);if(p==null||!p.frame.alive||p.frame.dying||target.Team>=0&&target.Team==source.Team&&!Rules.friendlyFire)continue;
                float damage=hit.Value;
                if(MysteryActive&&target.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true&&MysteryPunch(id,target.ActorId,ref damage))continue;
                var push=direction*(state.kind==EquipmentKind.BoxingGlove?22:4)+Vector3.up*(state.kind==EquipmentKind.BoxingGlove?6:.6f);
                if(target.IsLocal||target.IsBot){target.gameObject.GetComponent<IPushable>()?.Push(push,source.gameObject);target.gameObject.GetComponent<IDamageable>()?.Damage(damage,source.gameObject);}
                else{RecordRemoteDamage(target.ActorId,damage,id);RemoteImpact?.Invoke(target.ActorId,push,damage);}
            }
            return true;
        }
    }
}

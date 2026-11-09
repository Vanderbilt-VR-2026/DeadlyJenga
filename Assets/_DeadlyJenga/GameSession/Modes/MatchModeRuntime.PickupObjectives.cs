using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public static class BotEquipmentValue
    {
        public static float Value(EquipmentKind kind)=>kind switch
        {EquipmentKind.None=>0,EquipmentKind.Speed=>1.2f,EquipmentKind.Jump=>1.25f,EquipmentKind.MurderSword=>1.6f,EquipmentKind.BoxingGlove=>2.2f,EquipmentKind.Blaster=>3,EquipmentKind.SheriffHammer=>3.6f,EquipmentKind.DualRifle=>4,EquipmentKind.PoweredArmor=>2.5f,_=>kind>EquipmentKind.PoweredArmor?(EquipmentSpecifications.Get(kind).melee?1.8f:3.4f):0};
        public static float NoticeRoll(string actor,int pickup)
        {unchecked{uint hash=2166136261;foreach(char c in actor??"")hash=(hash^c)*16777619;hash=(hash^(uint)pickup)*16777619;hash^=hash>>16;hash*=0x7feb352d;hash^=hash>>15;hash*=0x846ca68b;hash^=hash>>16;return (hash&0xffffff)/16777216f;}}
        public static float Desire(EquipmentKind current,EquipmentKind next,float distance,float height)
            =>Value(next)-Value(current)-distance*.025f-Mathf.Abs(height)*.04f;
        public const float SwapMargin = .32f;

        public static float ContextualDesire(EquipmentState current,EquipmentKind next,float route,
            float height,float engagementRange,bool tightLane)
        {
            var inventory=current.builtIn?current.activePower:current.kind;
            float desire=Desire(inventory,next,route,height)+
                FightFit(next,engagementRange,tightLane)-FightFit(inventory,engagementRange,tightLane);
            int capacity=EquipmentSpecifications.Get(inventory).magazine;
            if(capacity>0&&current.magazineInitialized)
            {
                float ammo=Mathf.Clamp01((float)current.rounds/capacity);
                desire+=.85f*(1-Mathf.InverseLerp(0,.30f,ammo));
            }
            if(current.reloadRemaining>0)desire+=Mathf.Min(.65f,current.reloadRemaining*.30f);
            return desire;
        }

        static float FightFit(EquipmentKind kind,float range,bool tightLane)
        {
            var spec=EquipmentSpecifications.Get(kind);
            if(kind==EquipmentKind.None||spec.power)return 0;
            if(tightLane&&spec.blastRadius>0)return -2.1f;
            if(range<=0)return tightLane&&spec.charge>0?-.35f:0;
            if(spec.blastRadius>0)
            {
                if(range<spec.blastRadius+1)return -3.2f;
                if(kind==EquipmentKind.GrenadeLauncher&&range>28)return -2;
                return range<34?1.2f:.2f;
            }
            if(kind==EquipmentKind.DualRifle||kind==EquipmentKind.SheriffHammer)
                return range<9?.9f:Mathf.InverseLerp(10,38,range)*1.7f;
            if(kind==EquipmentKind.Chainsaw||kind==EquipmentKind.LaserAxe)
                return range<=spec.range?1.55f:range<=28?.48f:-1.5f;
            if(spec.melee)return range<=3?2.1f:range<=7?.5f:-2.3f;
            if((kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor)&&range<3.8f)return -1.8f;
            if(range>spec.range)return -2.4f;
            if(kind==EquipmentKind.DualUzi||kind==EquipmentKind.WristLaser)
                return Mathf.Lerp(1.45f,-.65f,Mathf.InverseLerp(7,32,range));
            if(kind==EquipmentKind.StockPistol)return Mathf.Lerp(1.1f,.30f,Mathf.InverseLerp(8,40,range));
            return range<7&&spec.charge>0?-.55f:.4f;
        }
        public static bool Interested(string actor,int pickup,float desire,float distance)
            =>desire>.2f&&NoticeRoll(actor,pickup)<Mathf.Clamp(.38f+desire*.12f-distance*.004f,.18f,.88f);
    }
    public sealed partial class MatchModeRuntime
    {
        readonly Dictionary<int,PowerupObjectiveTarget> pickupTargets=new Dictionary<int,PowerupObjectiveTarget>();
        readonly Dictionary<(string,int),float> botPickupReceipts=new Dictionary<(string,int),float>();
        readonly Dictionary<string,(float range,bool tight,float seen)> botPickupContext=
            new Dictionary<string,(float range,bool tight,float seen)>();
        PowerupObjectiveTarget PickupTarget(EquipmentPickup pickup)
        {
            if(!pickupTargets.TryGetValue(pickup.id,out var target)||!target)
            {var go=new GameObject("Pickup objective "+pickup.id);go.transform.SetParent(transform,false);target=go.AddComponent<PowerupObjectiveTarget>();target.Initialize(this,pickup.id);pickupTargets[pickup.id]=target;}
            target.transform.position=pickup.position-Vector3.up*.25f;return target;
        }
        void PrunePickupTargets()
        {
            foreach(var pair in pickupTargets.ToArray())
            {var pickup=pickups.Find(p=>p.id==pair.Key);if(pickup==null){if(pair.Value)Destroy(pair.Value.gameObject);pickupTargets.Remove(pair.Key);}else if(pair.Value)pair.Value.transform.position=pickup.position-Vector3.up*.25f;}
            if(botPickupContext.Count>128)
                foreach(var pair in botPickupContext.Where(p=>Time.time-p.Value.seen>2).ToArray())botPickupContext.Remove(pair.Key);
            if(botPickupReceipts.Count>512)foreach(var pair in botPickupReceipts.Where(p=>Time.time-p.Value>30).ToArray())botPickupReceipts.Remove(pair.Key);
        }
        void ClearPickupTargets(){foreach(var target in pickupTargets.Values)if(target)Destroy(target.gameObject);pickupTargets.Clear();botPickupContext.Clear();}
        internal bool CanBotTake(GameObject bot,int pickup)
        {
            if(!ValidEquipmentBot(bot,out var id)||simulation.Phase!=RoundPhase.Playing)return false;
            var item=pickups.Find(p=>p.id==pickup);var actor=FindActor(id);var person=simulation.Find(id);
            if(item==null||person==null||person.frame.role=="infected")return false;
            var current=EquipmentFor(id);var inventory=current.builtIn?current.activePower:current.kind;
            if(!botPickupContext.TryGetValue(id,out var context)||Time.time-context.seen>1.5f)context=default;
            var delta=item.position-actor.transform.position;
            if(current.roleIssued||inventory==item.kind||
                BotEquipmentValue.ContextualDesire(current,item.kind,delta.magnitude,delta.y,context.range,context.tight)<=BotEquipmentValue.SwapMargin)return false;
            return item.availableIn<=0&&actor.Alive&&person.frame.alive&&!person.frame.dying&&!simulation.CarriesFlag(id)&&
                (!MysteryActive||!mystery.IsDowned(id))&&(actor.transform.position-item.position).sqrMagnitude<1.65f*1.65f;
        }
        internal bool BotTake(GameObject bot,int pickup)=>CanBotTake(bot,pickup)&&actorIds.TryGetValue(bot,out var id)&&TakeEquipment(id,pickup,true);
        sealed partial class MatchBotFacts
        {
            readonly List<BotObjective> pickupHostiles=new List<BotObjective>(24);

            (float range,bool tight,float seen) PickupContext(BotDirectorActor actor,MatchPlayerFrame person)
            {
                float range=0;
                if(!owner.MysteryActive)
                {
                    pickupHostiles.Clear();
                    Hostiles(actor,pickupHostiles);
                    foreach(var threat in pickupHostiles)
                    {
                        if(!threat.subject||!owner.botSightings.TryGetValue(actor.id+"/"+threat.id,out var sight)||
                            Time.time-sight.seen>.55f||Time.time<sight.recognizedAt)continue;
                        float distance=Vector3.Distance(actor.position,sight.position);
                        if(distance>0&&(range<=0||distance<range))range=distance;
                    }
                }
                bool tight=Physics.Raycast(person.viewPosition,person.viewRotation*Vector3.forward,7,
                    (1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
                return (range,tight,Time.time);
            }

            void PickupQuery(BotDirectorActor actor,List<BotDirectorTarget> results)
            {
                var person=Sim.Find(actor.id);if(person==null||person.frame.role=="infected"||owner.simulation.CarriesFlag(actor.id)||actor.body.GetComponent<IEnemyActor>()?.IsEnemy==true)return;
                var current=owner.EquipmentFor(actor.id);var inventory=current.builtIn?current.activePower:current.kind;if(current.roleIssued)return;
                if(owner.MysteryActive&&owner.mystery.Role(actor.id)==MysteryRole.Murderer)return;
                if(owner.pickups.Count==0)return;
                var context=PickupContext(actor,person.frame);
                owner.botPickupContext[actor.id]=context;
                var candidates=new List<(EquipmentPickup pickup,float value)>(4);
                foreach(var pickup in owner.pickups)
                {
                    if(pickup.kind==EquipmentKind.FlyingHand||pickup.availableIn>0||pickup.kind==inventory)continue;
                    var delta=pickup.position-actor.position;float distance=delta.magnitude;if(distance>22)continue;
                    if(distance>3&&Vector3.Dot(person.frame.viewRotation*Vector3.forward,delta.normalized)<-.15f)continue;
                    if(owner.MysteryActive&&pickup.kind==EquipmentKind.SheriffHammer&&!owner.mystery.CanRecoverDroppedSheriff(actor.id))continue;
                    float desire=BotEquipmentValue.ContextualDesire(current,pickup.kind,distance,delta.y,context.range,context.tight);
                    if(desire<=BotEquipmentValue.SwapMargin||!BotEquipmentValue.Interested(actor.id,pickup.id,desire,distance))continue;
                    if(!VisibleTo(actor.position,pickup.position-Vector3.up*.6f))continue;
                    float utility=desire*.3f+.15f;
                    int at=0;while(at<candidates.Count&&candidates[at].value>utility)at++;
                    if(at<4){candidates.Insert(at,(pickup,utility));if(candidates.Count>4)candidates.RemoveAt(4);}
                }
                foreach(var candidate in candidates)
                {var target=owner.PickupTarget(candidate.pickup);Add(results,candidate.pickup.id.ToString(),target.transform.position,target.gameObject,(int)candidate.pickup.kind,1);var value=results[results.Count-1];value.utility=candidate.value;results[results.Count-1]=value;}
            }
        }
    }
    public sealed class PowerupObjectiveTarget:MonoBehaviour,IBotObjectiveInteraction
    {
        MatchModeRuntime owner;int id;
        internal void Initialize(MatchModeRuntime runtime,int pickup){owner=runtime;id=pickup;}
        public string InteractionId=>"collect-pickup";
        public bool CanInteract(GameObject actor)=>owner&&owner.CanBotTake(actor,id);
        public bool TryInteract(GameObject actor)=>owner&&owner.BotTake(actor,id);
    }
}

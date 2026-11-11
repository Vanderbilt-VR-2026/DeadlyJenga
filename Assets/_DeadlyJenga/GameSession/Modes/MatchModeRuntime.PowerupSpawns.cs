using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime:IPowerupSpawnControls,IBotEquipmentRuntime
    {
        sealed class RandomPickup{public Rigidbody support;public Collider surface;public Vector3 local;public float until;}
        readonly Dictionary<int,RandomPickup> randomPickups=new Dictionary<int,RandomPickup>();
        readonly System.Random pickupRandom=new System.Random();
        PowerupSpawnSettings spawnSettings;
        GameModeKind spawnMode;
        const int LootSurfaceMask=(1<<Layers.Block)|(1<<Layers.Surface)|1;
        readonly List<Vector3> lootOrigins=new List<Vector3>();
        float nextRandomPickup,nextPickupSupport;
        int openingWeapons;
        bool defaultSpawnSettings=true;
        public PowerupSpawnSettings SpawnSettings=>CopySpawnSettings(Authority?EnsureSpawnSettings():frame.powerupSpawns);
        public int RandomPickupCount=>Authority?randomPickups.Count:frame.randomPickupCount;
        public float NextSpawnIn=>Authority?Mathf.Max(0,nextRandomPickup-Time.time):frame.nextPowerupIn;
        public static PowerupSpawnSettings DefaultPowerupSpawns(GameModeKind mode)
        {
            bool mystery=mode is GameModeKind.MurderMystery or GameModeKind.Survival;
            return new PowerupSpawnSettings
            {
                enabled=true,interval=mystery?15:4,maximum=mystery?8:24,lifetime=90,jitter=mystery?.25f:.15f,
                types=Enumerable.Range(1,EquipmentSpecifications.All.Length-1).Select(i=>new PowerupSpawnWeight
                {
                    kind=(EquipmentKind)i,
                    weight=mystery?(i<=2?50:0):i<=2?8:i==(int)EquipmentKind.FlyingHand?4:
                        i==(int)EquipmentKind.PoweredArmor?5:10
                }).ToArray()
            };
        }
        static PowerupSpawnSettings CopySpawnSettings(PowerupSpawnSettings value)=>value==null?new PowerupSpawnSettings():new PowerupSpawnSettings{enabled=value.enabled,interval=value.interval,maximum=value.maximum,lifetime=value.lifetime,jitter=value.jitter,types=Enumerable.Range(1,EquipmentSpecifications.All.Length-1).Select(i=>new PowerupSpawnWeight{kind=(EquipmentKind)i,weight=value.types.FirstOrDefault(w=>(int)w.kind==i)?.weight??0}).ToArray()};
        public static bool ValidPowerupSpawns(PowerupSpawnSettings value)
        {
            if(value==null||!float.IsFinite(value.interval)||value.interval<1||value.interval>300||!float.IsFinite(value.jitter)||value.jitter<0||value.jitter>.9f||!float.IsFinite(value.lifetime)||value.lifetime<5||value.lifetime>600||value.maximum<0||value.maximum>32||value.types==null||value.types.Length>EquipmentSpecifications.All.Length-1)return false;
            var kinds=new HashSet<EquipmentKind>();foreach(var w in value.types)if(w==null||w.kind<EquipmentKind.Speed||w.kind>EquipmentKind.FlyingHand||!kinds.Add(w.kind)||!float.IsFinite(w.weight)||w.weight<0||w.weight>1000)return false;return true;
        }
        PowerupSpawnSettings EnsureSpawnSettings()
        {
            if(spawnSettings==null||spawnMode!=Rules.mode){spawnMode=Rules.mode;spawnSettings=DefaultPowerupSpawns(Rules.mode);defaultSpawnSettings=true;openingWeapons=0;nextRandomPickup=Time.time+spawnSettings.interval;}
            return spawnSettings;
        }
        public bool ConfigurePowerupSpawns(PowerupSpawnSettings value)
        {
            if(!Authority||!DeadlyJenga.CastlePrototype.SessionDevelopmentPolicy.CanExecuteLocal(session)||!ValidPowerupSpawns(value))return false;
            spawnSettings=CopySpawnSettings(value);spawnMode=Rules.mode;defaultSpawnSettings=false;openingWeapons=0;SchedulePickup();WritePowerupFrame();return true;
        }
        void SchedulePickup(){var s=EnsureSpawnSettings();nextRandomPickup=Time.time+s.interval*(1+(float)(pickupRandom.NextDouble()*2-1)*s.jitter);}
        void WritePowerupFrame(){frame.powerupSpawns=EnsureSpawnSettings();frame.randomPickupCount=randomPickups.Count;frame.nextPowerupIn=NextSpawnIn;}
        void ResetRandomPickups()
        {
            randomPickups.Clear();ClearPickupTargets();botPickupReceipts.Clear();nextPickupSupport=0;
            openingWeapons=0;
            nextRandomPickup=Time.time+EnsureSpawnSettings().interval;
        }
        void StartOpeningWeapons()
        {
            var settings=EnsureSpawnSettings();
            if(!defaultSpawnSettings||(Rules.mode is GameModeKind.MurderMystery or GameModeKind.Survival)||!settings.enabled)return;
            openingWeapons=Mathf.Min(8,settings.maximum);
            if(openingWeapons>0)nextRandomPickup=Time.time;
        }

        void TickRandomPickups()
        {
            if(!Authority)return;var settings=EnsureSpawnSettings();
            if(Time.time>=nextPickupSupport)
            {
                nextPickupSupport=Time.time+.2f;
                foreach(var pair in randomPickups.ToArray())
                {
                    var pickup=pickups.Find(p=>p.id==pair.Key);var anchor=pair.Value;
                    if(pickup==null){randomPickups.Remove(pair.Key);continue;}
                    if(!anchor.surface||!anchor.surface.enabled||!anchor.surface.gameObject.activeInHierarchy||Time.time>=anchor.until||
                        pickup.supportBlock>=0&&(!anchor.support||!anchor.support.gameObject.activeInHierarchy||
                        Vector3.Dot(anchor.support.transform.up,Vector3.up)<.5f))
                    {pickups.Remove(pickup);randomPickups.Remove(pair.Key);continue;}
                    if(anchor.support)pickup.position=anchor.support.transform.TransformPoint(anchor.local);
                    pickup.expiresIn=Mathf.Max(0,anchor.until-Time.time);
                    if(Services.World!=null&&pickup.position.y<Services.World.WaterHeight(pickup.position)){pickups.Remove(pickup);randomPickups.Remove(pair.Key);}
                }
                PrunePickupTargets();
            }
            if(simulation.Phase!=RoundPhase.Playing||!settings.enabled||settings.maximum==0||Time.time<nextRandomPickup)return;
            SchedulePickup();
            if(randomPickups.Count>=settings.maximum||pickups.Count>=128)return;
            bool opening=openingWeapons>0;
            var kind=RollLoot(settings,opening);
            if(kind==EquipmentKind.None)return;
            if(!TrySpawnLoot(kind,settings.lifetime))
            {
                nextRandomPickup=Time.time+Mathf.Min(1,settings.interval);
                return;
            }
            if(opening&&--openingWeapons>0)nextRandomPickup=Time.time+.25f;
        }

        EquipmentKind RollLoot(PowerupSpawnSettings settings,bool weaponsOnly)
        {
            bool Eligible(EquipmentKind kind)=>AllowsSurvivalPickup(kind) &&
                (!weaponsOnly||kind>=EquipmentKind.Blaster&&kind<=EquipmentKind.Dagger);
            float total=0;
            foreach(var entry in settings.types)if(Eligible(entry.kind))total+=entry.weight;
            if(total<=0)return EquipmentKind.None;
            float roll=(float)pickupRandom.NextDouble()*total;
            foreach(var entry in settings.types)
            {
                if(!Eligible(entry.kind))continue;
                roll-=entry.weight;
                if(roll<0)return entry.kind;
            }
            return EquipmentKind.None;
        }

        bool TrySpawnLoot(EquipmentKind kind,float lifetime)
        {
            var world=Services.World;
            if(world==null)return false;
            lootOrigins.Clear();
            foreach(var player in simulation.Participants)
            {
                var row=player.frame;
                if(player.present&&player.grounded&&!player.deferredParticipation&&row.alive&&!row.dying&&!row.sinking&&
                    row.role!="hand"&&row.role!="spectator"&&Finite(row.position))lootOrigins.Add(row.position);
            }
            var safe=world.SafeSpawnPoint();
            if(Finite(safe))lootOrigins.Add(safe);
            float step=Mathf.Clamp(world.StepHeight*.50f,.28f,.65f);

            // Prefer a continuous route from a survivor's current level. Rotate origins to spread loot.
            if(lootOrigins.Count>0)
            {
                int first=pickupRandom.Next(lootOrigins.Count);
                for(int attempt=0;attempt<18;attempt++)
                {
                    var from=lootOrigins[(first+attempt)%lootOrigins.Count];
                    float angle=(float)pickupRandom.NextDouble()*Mathf.PI*2;
                    float radius=1.7f+(float)pickupRandom.NextDouble()*7;
                    var at=from+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                    if(!LootFloor(world,at,step+.15f,2.2f,out var hit))continue;
                    if(!OpenLootRoute(world,from,hit.point,step))continue;
                    if(StoreLoot(world,kind,lifetime,hit))return true;
                }
            }

            // Search exposed structures too, including logical blocks with grouped colliders.
            var blocks=world.Blocks;
            if(blocks!=null)
                for(int attempt=0;attempt<30&&blocks.Count>0;attempt++)
                {
                    var body=blocks[pickupRandom.Next(blocks.Count)];
                    if(!StableLootSupport(body))continue;
                    var collider=body.GetComponent<Collider>();
                    if(!collider||!collider.enabled||collider.isTrigger)continue;
                    var bounds=collider.bounds;
                    if(bounds.size.sqrMagnitude<.01f)continue;
                    var at=bounds.center;
                    at.y=bounds.max.y;
                    at.x+=((float)pickupRandom.NextDouble()-.5f)*Mathf.Max(0,bounds.size.x-.48f);
                    at.z+=((float)pickupRandom.NextDouble()-.5f)*Mathf.Max(0,bounds.size.z-.48f);
                    if(!LootFloor(world,at,.45f,1.1f,out var hit)||LootBody(hit.collider)!=body)continue;
                    if(defaultSpawnSettings&&!LootDeckHasApproach(world,hit,step))continue;
                    if(StoreLoot(world,kind,lifetime,hit))return true;
                }

            // Wide static floors may have no block registry at all.
            if(Rules.island==IslandRule.Walkable&&Finite(safe))
            {
                for(int attempt=0;attempt<12;attempt++)
                {
                    float angle=(float)pickupRandom.NextDouble()*Mathf.PI*2;
                    float radius=6+(float)pickupRandom.NextDouble()*10;
                    var at=safe+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                    if(LootFloor(world,at,.8f,3.2f,out var hit)&&!LootBody(hit.collider)&&
                        OpenLootRoute(world,safe,hit.point,step)&&StoreLoot(world,kind,lifetime,hit))return true;
                }
            }
            return false;
        }

        bool LootFloor(IWorldQuery world,Vector3 at,float raise,float reach,out RaycastHit hit)
        {
            if(!Physics.Raycast(at+Vector3.up*raise,Vector3.down,out hit,reach,LootSurfaceMask,QueryTriggerInteraction.Ignore)||
                hit.normal.y<.70f||hit.point.y<world.WaterHeight(hit.point)+.20f)return false;
            return Rules.island==IslandRule.Walkable||LootBody(hit.collider);
        }

        bool OpenLootRoute(IWorldQuery world,Vector3 from,Vector3 to,float step)
        {
            float distance=Vector3.ProjectOnPlane(to-from,Vector3.up).magnitude;
            int pieces=Mathf.Max(1,Mathf.CeilToInt(distance/1.15f));
            var previous=from;
            for(int i=1;i<=pieces;i++)
            {
                var sample=Vector3.Lerp(from,to,i/(float)pieces);
                sample.y=previous.y;
                if(!LootFloor(world,sample,step+.12f,step*2+.24f,out var floor)||
                    Mathf.Abs(floor.point.y-previous.y)>step+.10f||!ClearLootHeadroom(floor.point))return false;
                var start=previous+Vector3.up*.52f;
                var end=floor.point+Vector3.up*.52f;
                if(Physics.Linecast(start,end,LootSurfaceMask,QueryTriggerInteraction.Ignore))return false;
                previous=floor.point;
            }
            return (previous-to).sqrMagnitude<.15f*.15f;
        }

        bool LootDeckHasApproach(IWorldQuery world,RaycastHit deck,float step)
        {
            foreach(var origin in lootOrigins)
                if((origin-deck.point).sqrMagnitude<3*3&&OpenLootRoute(world,origin,deck.point,step))return true;
            bool climbable=Rules.canClimb&&Climbables.TryGet(deck.collider,out var grip)&&grip.CanClimb;
            var bounds=deck.collider.bounds;
            for(int i=0;i<4;i++)
            {
                var edge=deck.point;
                if(i==0)edge.x=bounds.min.x-.42f;
                if(i==1)edge.x=bounds.max.x+.42f;
                if(i==2)edge.z=bounds.min.z-.42f;
                if(i==3)edge.z=bounds.max.z+.42f;
                float climbHeight=climbable?Mathf.Clamp(world.StepHeight*1.6f,1,2.4f):step;
                if(!LootFloor(world,edge,step+.12f,climbHeight+step+.12f,out var landing)||
                    !ClearLootHeadroom(landing.point))continue;
                if(Mathf.Abs(landing.point.y-deck.point.y)<=step&&OpenLootRoute(world,landing.point,deck.point,step))
                    return true;
                if(climbable&&deck.point.y-landing.point.y>step)return true;
            }
            return false;
        }

        static bool ClearLootHeadroom(Vector3 floor)=>!Physics.CheckCapsule(
            floor+Vector3.up*.30f,floor+Vector3.up*.91f,.22f,LootSurfaceMask,QueryTriggerInteraction.Ignore);

        static Rigidbody LootBody(Collider surface)
        {
            if(Climbables.TryGet(surface,out var climbable)&&climbable.Body)return climbable.Body;
            return surface.attachedRigidbody;
        }

        static bool StableLootSupport(Rigidbody body)=>body&&body.gameObject.activeInHierarchy&&
            body.linearVelocity.sqrMagnitude<4&&body.angularVelocity.sqrMagnitude<4&&Vector3.Dot(body.transform.up,Vector3.up)>=.5f;

        bool StoreLoot(IWorldQuery world,EquipmentKind kind,float lifetime,RaycastHit floor)
        {
            if(!ClearLootHeadroom(floor.point))return false;
            var point=floor.point+Vector3.up*.30f;
            var support=LootBody(floor.collider);
            int index=support?IndexOfBlock(world.Blocks,support):-1;
            if(support&&(index<0||!StableLootSupport(support)))return false;
            foreach(var item in pickups)
            {
                var delta=item.position-point;
                if(Mathf.Abs(delta.y)<1.15f&&delta.x*delta.x+delta.z*delta.z<2.1f*2.1f)return false;
            }
            var pickup=AddPickup(kind,point);
            if(pickup==null)return false;
            pickup.randomSpawn=true;
            pickup.supportBlock=index;
            pickup.supportLocal=support?support.transform.InverseTransformPoint(point):Vector3.zero;
            pickup.expiresIn=lifetime;
            randomPickups[pickup.id]=new RandomPickup
            {
                support=support,surface=floor.collider,local=pickup.supportLocal,until=Time.time+lifetime
            };
            return true;
        }

        int IndexOfBlock(IReadOnlyList<Rigidbody> blocks,Rigidbody body)
        {
            if(blocks==null)return -1;
            if(blockIds.TryGetValue(body,out var index)&&index>=0&&index<blocks.Count&&blocks[index]==body)return index;
            for(int i=0;i<blocks.Count;i++)if(blocks[i]==body)return i;
            return -1;
        }

        void RestoreRandomPickups()
        {
            var blocks=Services.World?.Blocks;
            foreach(var pickup in pickups.ToArray())
            {
                if(!pickup.randomSpawn)continue;
                Rigidbody support=null;
                if(pickup.supportBlock>=0)
                {
                    if(blocks==null||pickup.supportBlock>=blocks.Count||!blocks[pickup.supportBlock])
                    {
                        pickups.Remove(pickup);
                        continue;
                    }
                    support=blocks[pickup.supportBlock];
                }
                Collider surface=null;
                if(Physics.Raycast(pickup.position+Vector3.up*.12f,Vector3.down,out var hit,.65f,
                    LootSurfaceMask,QueryTriggerInteraction.Ignore)&&LootBody(hit.collider)==support)surface=hit.collider;
                if(!surface&&support)surface=support.GetComponent<Collider>();
                if(!surface)
                {
                    pickups.Remove(pickup);
                    continue;
                }
                randomPickups[pickup.id]=new RandomPickup
                {
                    support=support,surface=surface,local=pickup.supportLocal,until=Time.time+pickup.expiresIn
                };
            }
        }
        bool ValidEquipmentBot(GameObject bot,out string id)
        {id=null;return Authority&&bot&&bot.GetComponent<IEnemyActor>()?.IsEnemy!=true&&actorIds.TryGetValue(bot,out id)&&FindActor(id)?.IsBot==true;}
        public bool SetBotWeapon(GameObject bot,bool drawn,bool shotgun)
        {
            if(!ValidEquipmentBot(bot,out string id))return false;
            var state=EquipmentFor(id);
            if(drawn&&state.drawn&&state.shotgun==shotgun)
            {
                if(heldWeapons.ContainsKey(id)){StopArsenalTrigger(id);PublishEquipment();}
                return true;
            }
            return EquipmentCommandFor(id,new EquipmentCommand{action="state",drawn=drawn,shotgun=shotgun,left=state.leftHanded});
        }
        public bool FireBotWeapon(GameObject bot,Pose muzzle)
        {
            if(!ValidEquipmentBot(bot,out string id))return false;
            var state=EquipmentFor(id);
            var spec=EquipmentSpecifications.Get(state.kind);
            var forward=muzzle.rotation*Vector3.forward;
            if(state.kind>EquipmentKind.PoweredArmor&&(spec.automatic||spec.charge>0)&&
                !EquipmentCommandFor(id,new EquipmentCommand{action="trigger",held=true,position=muzzle.position,forward=forward}))
                return false;
            return EquipmentCommandFor(id,new EquipmentCommand{action="attack",position=muzzle.position,forward=forward});
        }
    }
}

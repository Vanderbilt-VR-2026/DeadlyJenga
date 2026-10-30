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
        float nextRandomPickup,nextPickupSupport;
        public PowerupSpawnSettings SpawnSettings=>CopySpawnSettings(Authority?EnsureSpawnSettings():frame.powerupSpawns);
        public int RandomPickupCount=>Authority?randomPickups.Count:frame.randomPickupCount;
        public float NextSpawnIn=>Authority?Mathf.Max(0,nextRandomPickup-Time.time):frame.nextPowerupIn;
        public static PowerupSpawnSettings DefaultPowerupSpawns(GameModeKind mode)
        {
            bool mystery=mode==GameModeKind.MurderMystery;
            return new PowerupSpawnSettings{enabled=true,interval=15,maximum=8,lifetime=90,jitter=.25f,
                types=Enumerable.Range(1,EquipmentSpecifications.All.Length-1).Select(i=>new PowerupSpawnWeight{kind=(EquipmentKind)i,weight=mystery?(i<=2?50:0):i>8?5:i<=2?20:i==3?15:i==6?15:i==7?10:i==8?5:i==4?7:8}).ToArray()};
        }
        static PowerupSpawnSettings CopySpawnSettings(PowerupSpawnSettings value)=>value==null?new PowerupSpawnSettings():new PowerupSpawnSettings{enabled=value.enabled,interval=value.interval,maximum=value.maximum,lifetime=value.lifetime,jitter=value.jitter,types=Enumerable.Range(1,EquipmentSpecifications.All.Length-1).Select(i=>new PowerupSpawnWeight{kind=(EquipmentKind)i,weight=value.types.FirstOrDefault(w=>(int)w.kind==i)?.weight??0}).ToArray()};
        public static bool ValidPowerupSpawns(PowerupSpawnSettings value)
        {
            if(value==null||!float.IsFinite(value.interval)||value.interval<1||value.interval>300||!float.IsFinite(value.jitter)||value.jitter<0||value.jitter>.9f||!float.IsFinite(value.lifetime)||value.lifetime<5||value.lifetime>600||value.maximum<0||value.maximum>32||value.types==null||value.types.Length>EquipmentSpecifications.All.Length-1)return false;
            var kinds=new HashSet<EquipmentKind>();foreach(var w in value.types)if(w==null||w.kind<EquipmentKind.Speed||w.kind>EquipmentKind.FlyingHand||!kinds.Add(w.kind)||!float.IsFinite(w.weight)||w.weight<0||w.weight>1000)return false;return true;
        }
        PowerupSpawnSettings EnsureSpawnSettings()
        {
            if(spawnSettings==null||spawnMode!=Rules.mode){spawnMode=Rules.mode;spawnSettings=DefaultPowerupSpawns(Rules.mode);nextRandomPickup=Time.time+spawnSettings.interval;}
            return spawnSettings;
        }
        public bool ConfigurePowerupSpawns(PowerupSpawnSettings value)
        {
            if(!Authority||!ValidPowerupSpawns(value))return false;
            spawnSettings=CopySpawnSettings(value);spawnMode=Rules.mode;SchedulePickup();WritePowerupFrame();return true;
        }
        void SchedulePickup(){var s=EnsureSpawnSettings();nextRandomPickup=Time.time+s.interval*(1+(float)(pickupRandom.NextDouble()*2-1)*s.jitter);}
        void WritePowerupFrame(){frame.powerupSpawns=EnsureSpawnSettings();frame.randomPickupCount=randomPickups.Count;frame.nextPowerupIn=NextSpawnIn;}
        void ResetRandomPickups()
        {
            randomPickups.Clear();ClearPickupTargets();botPickupReceipts.Clear();nextPickupSupport=0;
            nextRandomPickup=Time.time+(spawnSettings?.interval??15);
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
                    if(!anchor.support||!anchor.surface||!anchor.surface.enabled||!anchor.support.gameObject.activeInHierarchy||Time.time>=anchor.until||Vector3.Dot(anchor.support.transform.up,Vector3.up)<.5f)
                    {pickups.Remove(pickup);randomPickups.Remove(pair.Key);continue;}
                    pickup.position=anchor.support.transform.TransformPoint(anchor.local);
                    pickup.expiresIn=Mathf.Max(0,anchor.until-Time.time);
                    if(Services.World!=null&&pickup.position.y<Services.World.WaterHeight(pickup.position)){pickups.Remove(pickup);randomPickups.Remove(pair.Key);}
                }
                PrunePickupTargets();
            }
            if(simulation.Phase!=RoundPhase.Playing||!settings.enabled||settings.maximum==0||Time.time<nextRandomPickup)return;
            SchedulePickup();
            if(randomPickups.Count>=settings.maximum||pickups.Count>=128)return;
            float total=0;foreach(var w in settings.types)total+=w.weight;if(total<=0)return;
            float roll=(float)pickupRandom.NextDouble()*total;EquipmentKind kind=EquipmentKind.None;
            foreach(var w in settings.types){roll-=w.weight;if(roll<0){kind=w.kind;break;}}
            var blocks=Services.World?.Blocks;if(kind==EquipmentKind.None||blocks==null||blocks.Count==0)return;
            // Uniform block sampling includes all heights. Work stays bounded even on dense maps.
            for(int attempt=0;attempt<32;attempt++)
            {
                var body=blocks[pickupRandom.Next(blocks.Count)];if(!body||!body.gameObject.activeInHierarchy||body.linearVelocity.sqrMagnitude>4||body.angularVelocity.sqrMagnitude>4)continue;
                var collider=body.GetComponent<Collider>();if(!collider||!collider.enabled||collider.isTrigger)continue;var b=collider.bounds;
                float x=b.center.x+(float)(pickupRandom.NextDouble()-.5)*Mathf.Max(0,b.size.x-.6f),z=b.center.z+(float)(pickupRandom.NextDouble()-.5)*Mathf.Max(0,b.size.z-.6f);
                int mask=(1<<Layers.Block)|(1<<Layers.Surface)|1;
                if(!Physics.Raycast(new Vector3(x,b.max.y+.4f,z),Vector3.down,out var hit,b.size.y+.8f,mask,QueryTriggerInteraction.Ignore)||HitAbilities.Get<Rigidbody>(hit.collider)!=body||hit.normal.y<.7f||hit.point.y<Services.World.WaterHeight(hit.point)+.25f)continue;
                if(Physics.CheckCapsule(hit.point+Vector3.up*.4f,hit.point+Vector3.up*1.3f,.23f,mask,QueryTriggerInteraction.Ignore))continue;
                Vector3 point=hit.point+Vector3.up*.3f;if(pickups.Any(p=>(p.position-point).sqrMagnitude<1))continue;
                var pickup=AddPickup(kind,point);if(pickup==null)return;
                pickup.randomSpawn=true;pickup.supportBlock=blockIds.TryGetValue(body,out int index)?index:IndexOfBlock(body);pickup.supportLocal=body.transform.InverseTransformPoint(point);pickup.expiresIn=settings.lifetime;
                if(pickup.supportBlock<0){pickups.Remove(pickup);continue;}
                randomPickups[pickup.id]=new RandomPickup{support=body,surface=collider,local=pickup.supportLocal,until=Time.time+settings.lifetime};return;
            }
            nextRandomPickup=Time.time+Mathf.Min(2,settings.interval);
        }
        int IndexOfBlock(Rigidbody body){var blocks=Services.World.Blocks;for(int i=0;i<blocks.Count;i++)if(blocks[i]==body)return i;return -1;}
        void RestoreRandomPickups()
        {
            foreach(var pickup in pickups.ToArray())if(pickup.randomSpawn)
            {
                var blocks=Services.World?.Blocks;
                if(blocks==null||pickup.supportBlock<0||pickup.supportBlock>=blocks.Count||!blocks[pickup.supportBlock]){pickups.Remove(pickup);continue;}
                randomPickups[pickup.id]=new RandomPickup{support=blocks[pickup.supportBlock],surface=blocks[pickup.supportBlock].GetComponent<Collider>(),local=pickup.supportLocal,until=Time.time+pickup.expiresIn};
            }
        }
        bool ValidEquipmentBot(GameObject bot,out string id)
        {id=null;return Authority&&bot&&bot.GetComponent<IEnemyActor>()?.IsEnemy!=true&&actorIds.TryGetValue(bot,out id)&&FindActor(id)?.IsBot==true;}
        public bool SetBotWeapon(GameObject bot,bool drawn,bool shotgun)
        {
            if(!ValidEquipmentBot(bot,out string id))return false;
            return EquipmentCommandFor(id,new EquipmentCommand{action="state",drawn=drawn,shotgun=shotgun,left=EquipmentFor(id).leftHanded});
        }
        public bool FireBotWeapon(GameObject bot,Pose muzzle)
        {if(!ValidEquipmentBot(bot,out string id))return false;var state=EquipmentFor(id);
            if(state.kind>EquipmentKind.PoweredArmor&&(EquipmentSpecifications.Get(state.kind).automatic||EquipmentSpecifications.Get(state.kind).charge>0))
                HoldArsenalTrigger(id,state,new EquipmentCommand{held=true,position=muzzle.position,forward=muzzle.rotation*Vector3.forward});
            return EquipmentCommandFor(id,new EquipmentCommand{action="attack",position=muzzle.position,forward=muzzle.rotation*Vector3.forward});}
    }
}

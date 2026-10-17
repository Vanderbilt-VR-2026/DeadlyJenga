using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public sealed class CastleBotPopulation : MonoBehaviour, ISurvivorBotPopulation
    {
        public BotWorld Session {get;private set;}
        public int targetPopulation=10;
        [Tooltip("Supply the host's connected human count here when integrating multiplayer.")]
        public int humanPlayers=1;
        public bool autoFill;
        public float deathHeight=-6,respawnDelay=2;
        public int SelectedPreset {get;private set;}
        public string SelectedName=>profiles[SelectedPreset].displayName;
        public int Count=>bots.Count;
        public int Capacity=>int.MaxValue;
        public int FillCapacity=>Mathf.Max(0,targetPopulation-humanPlayers);
        public IReadOnlyList<CastleBotBrain> Bots=>bots;
        public string Status {get;private set;}="";
        readonly List<CastleBotBrain> bots=new List<CastleBotBrain>();
        readonly List<CastleBotLanding> spawnSites=new List<CastleBotLanding>();
        readonly List<Vector3> hazards=new List<Vector3>();
        readonly Collider[] nearbySurvivors=new Collider[16];
        CastleBotPersonality[] profiles;
        bool ownsProfiles;
        DeadlyJenga.CandyPhysicsHands.CandyFlyingHandBridge handBridge;
        Transform staging;
        float nextPopulation,nextHazards;
        int serial;
        public void Initialize(BotWorld session)
        {
            Session=session;handBridge=session.Hand.GetComponent<DeadlyJenga.CandyPhysicsHands.CandyFlyingHandBridge>();profiles=Resources.LoadAll<CastleBotPersonality>("CastleBots").OrderBy(p=>p.displayName).ToArray();
            if(profiles.Length==0){ownsProfiles=true;profiles=new CastleBotPersonality[4];for(int i=0;i<4;i++)profiles[i]=CastleBotPersonality.Create(i);}
            staging=new GameObject("Inactive NPC assembly").transform;staging.SetParent(transform,false);staging.gameObject.SetActive(false);
        }
        public void CyclePersonality(){SelectedPreset=(SelectedPreset+1)%profiles.Length;}
        public void SelectPreset(int index){if(profiles==null||profiles.Length==0)return;SelectedPreset=Mathf.Clamp(index,0,profiles.Length-1);}
        public CastleBotBrain SpawnBot()
        {
            var motor=Instantiate(Session.survivorPrefab,staging);motor.name="Candy NPC "+(++serial)+" — "+SelectedName;
            motor.externallyDriven=true;motor.controlMode=FlyingHand.ControlMode.Desktop;
            var avatar=motor.GetComponent<CandyPlayerAvatar>();if(avatar)avatar.remoteDriven=true;
            foreach(var camera in motor.GetComponentsInChildren<Camera>(true)){camera.enabled=false;camera.tag="Untagged";}
            foreach(var listener in motor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            var brain=motor.gameObject.AddComponent<CastleBotBrain>();
            motor.transform.SetParent(null,true);motor.RefreshMode();
            // Disabled cameras still supply the shared controller's facing and avatar head pose.
            brain.Initialize(this,motor,profiles[SelectedPreset],serial);
            motor.jumpHeight=Session.Player.jumpHeight;
            if(!Place(brain)){motor.gameObject.SetActive(false);Destroy(motor.gameObject);Status="No clear, stable spawn surface — try rebuilding the castle";return null;}
            bots.Add(brain);brain.Spawned(false);Status="Spawned "+motor.name;return brain;
        }
        public bool Place(CastleBotBrain bot)
        {
            spawnSites.Clear();Physics.SyncTransforms();
            foreach(var rb in Session.world.Blocks)
            {
                if(!rb||!rb.gameObject.activeInHierarchy||Vector3.Dot(rb.rotation*Vector3.up,Vector3.up)<.97f||rb.linearVelocity.magnitude>1)continue;
                var box=rb.GetComponent<BoxCollider>();if(!box)continue;
                for(int i=-1;i<=1;i++)
                {
                    var p=CastleBotNavigation.TopPoint(rb,box,i*.28f);
                    spawnSites.Add(new CastleBotLanding{surface=rb.transform,local=rb.transform.InverseTransformPoint(p),score=p.y});
                }
            }
            spawnSites.Sort((a,b)=>b.score.CompareTo(a.score));
            foreach(var site in spawnSites)
            {
                var p=site.Point;
                if(!CastleBotNavigation.Landing(bot.Motor,p,out var hit))continue;
                p=hit.point+Vector3.up*.06f;
                float r=bot.Motor.Capsule.radius+.18f;
                int count=Physics.OverlapCapsuleNonAlloc(p+Vector3.up*r,p+Vector3.up*bot.Motor.StandingHeight,r,nearbySurvivors,1<<Layers.Survivor,QueryTriggerInteraction.Ignore);
                bool occupied=count>=nearbySurvivors.Length;
                for(int i=0;i<count;i++)if(nearbySurvivors[i]!=bot.Motor.Capsule)occupied=true;
                if(occupied||bots.Any(b=>b&&b!=bot&&!b.Respawning&&Vector3.Distance(b.transform.position,p)<.7f))continue;
                var direction=Vector3.ProjectOnPlane(Session.world.Center-p,Vector3.up);
                bot.Motor.Teleport(p,direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):Quaternion.identity);return true;
            }
            return false;
        }
        // ISurvivorBotPopulation (Count, Capacity, Status, SelectedName, CyclePersonality, SelectPreset,
        // FillToTarget and SetHumanPlayerCount are public members of this class)
        public bool Running { get => enabled; set => enabled = value; }
        void IBotPopulation.SpawnOne() => SpawnBot();
        void IBotPopulation.Clear() => ClearBots();
        void IBotPopulation.MapRebuilt() => CastleRebuilt();
        int ISurvivorBotPopulation.TargetPopulation { get => targetPopulation; set => targetPopulation = value; }
        public void FillToTarget(){autoFill=true;Reconcile();}
        public void SetHumanPlayerCount(int count){humanPlayers=Mathf.Max(0,count);if(Session.HandBots)Session.HandBots.TrimHumanCapacity();if(autoFill)Reconcile();}
        public void ReconcileHandSlots(){if(autoFill)Reconcile();}
        void Reconcile()
        {
            int remaining=Mathf.Max(0,FillCapacity-bots.Count);
            for(int i=0;i<Mathf.Min(remaining,4);i++)if(!SpawnBot())break;
        }
        void RemoveLast(){var bot=bots[bots.Count-1];bots.RemoveAt(bots.Count-1);if(bot){bot.gameObject.SetActive(false);Destroy(bot.gameObject);}}
        public void ClearBots(){autoFill=false;while(bots.Count>0)RemoveLast();Status="NPCs cleared";}
        public void CastleRebuilt(){foreach(var bot in bots)if(bot){bot.Motor.jumpHeight=Session.Player.jumpHeight;if(Place(bot))bot.Spawned(false);}}
        void Update()
        {
            bots.RemoveAll(b=>!b);
            if(Time.time>=nextPopulation){nextPopulation=Time.time+1;if(autoFill)Reconcile();}
            if(Time.time>=nextHazards)
            {
                nextHazards=Time.time+.2f;hazards.Clear();
                foreach(var rb in Session.world.Blocks)if(rb&&rb.linearVelocity.y<-.8f)hazards.Add(rb.position+rb.linearVelocity*.35f);
            }
        }
        public float ThreatCost(Vector3 point)
        {
            float cost=Session.HandBots?Session.HandBots.ThreatCost(point):0;
            if(Session.PlayingHand&&Session.Hand.gameObject.activeInHierarchy)
            {
                Vector3 position=handBridge&&handBridge.PhysicalHand&&handBridge.PhysicalHand.IsInteractive?handBridge.PhysicalHand.bodies[0].position:Session.Hand.motor.transform.position;
                cost+=Mathf.Max(0,7-Vector3.Distance(point,position))*.5f;
            }
            foreach(var predicted in hazards)
            {
                if(predicted.y<point.y-.5f||predicted.y>point.y+8)continue;
                float dx=predicted.x-point.x,dz=predicted.z-point.z,d2=dx*dx+dz*dz;
                if(d2<9)cost+=(3-Mathf.Sqrt(d2))*.8f;
            }
            return cost;
        }
        public float Crowding(Vector3 point,CastleBotBrain self)
        {float cost=0;foreach(var b in bots)if(b&&b!=self&&!b.Respawning)cost+=Mathf.Max(0,1.2f-Vector3.Distance(b.transform.position,point));return cost;}
        void OnDestroy()
        {
            foreach(var bot in bots)if(bot)Destroy(bot.gameObject);
            if(ownsProfiles&&profiles!=null)foreach(var p in profiles)if(p)Destroy(p);
        }
    }
}

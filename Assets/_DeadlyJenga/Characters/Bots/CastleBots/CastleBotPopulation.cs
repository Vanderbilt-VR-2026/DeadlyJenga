using Unity.Profiling;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public sealed partial class CastleBotPopulation : MonoBehaviour, ISurvivorBotPopulation, IEnemyPopulation
    {
        static readonly ProfilerMarker DJBotThreatMarker = new ProfilerMarker("DJ.BotThreat");
        public BotWorld Session {get;private set;}
        public int targetPopulation=10;
        [Tooltip("Supply the host's connected human count here when integrating multiplayer.")]
        public int humanPlayers=1;
        public bool autoFill;
        public float deathHeight=-6,respawnDelay=2;
        public int SelectedPreset {get;private set;}
        public string SelectedName=>profiles[SelectedPreset].displayName;
        public int Count
        {
            get
            {
                if(countFrame!=Time.frameCount||countSize!=bots.Count||countSerial!=serial)
                {
                    countFrame=Time.frameCount;countSize=bots.Count;countSerial=serial;
                    cachedCount=bots.Count(b=>b&&!b.GetComponent<CastleEnemyActor>());
                }
                return cachedCount;
            }
        }
        int countFrame=-1,countSize=-1,countSerial=-1,cachedCount;
        int spawnFrame=-1,spawnBlockCount;
        IWorldQuery spawnWorld;
        Rigidbody spawnFirst;
        readonly List<Vector3> spawnSamples=new List<Vector3>(18);
        public int Capacity=>int.MaxValue;
        public int FillCapacity=>Mathf.Max(0,targetPopulation-humanPlayers);
        public IReadOnlyList<CastleBotBrain> Bots=>bots;
        public string Status {get;private set;}="";
        readonly List<CastleBotBrain> bots=new List<CastleBotBrain>();
        readonly List<CastleBotLanding> spawnSites=new List<CastleBotLanding>();
        readonly FallingBlockThreats hazards=new FallingBlockThreats();
        readonly Collider[] nearbySurvivors=new Collider[16];
        CastleBotPersonality[] profiles;
        bool ownsProfiles;
        DeadlyJenga.CandyPhysicsHands.CandyFlyingHandBridge handBridge;
        Transform staging;
        float nextPopulation,nextHazards;
        readonly List<CastleBotBrain> planningQueue=new List<CastleBotBrain>();
        public int PlanningCount => Mathf.Max(1,planningQueue.Count);
        int serial;
        public BotWalkingRoutes WalkingRoutes { get; } = new BotWalkingRoutes();
        readonly Dictionary<Vector3Int,List<CastleBotBrain>> crowdCells=new Dictionary<Vector3Int,List<CastleBotBrain>>();
        float crowdAt;
        static Vector3Int CrowdCell(Vector3 point)=>new Vector3Int(Mathf.FloorToInt(point.x/2),Mathf.FloorToInt(point.y/2),Mathf.FloorToInt(point.z/2));
        public void Initialize(BotWorld session)
        {
            Session=session;handBridge=session.Hand?session.Hand.GetComponent<DeadlyJenga.CandyPhysicsHands.CandyFlyingHandBridge>():null;profiles=Resources.LoadAll<CastleBotPersonality>("CastleBots").OrderBy(p=>p.displayName).ToArray();
            if(profiles.Length==0){ownsProfiles=true;profiles=new CastleBotPersonality[4];for(int i=0;i<4;i++)profiles[i]=CastleBotPersonality.Create(i);}
            staging=new GameObject("Inactive NPC assembly").transform;staging.SetParent(transform,false);staging.gameObject.SetActive(false);
        }
        public void CyclePersonality(){SelectedPreset=(SelectedPreset+1)%profiles.Length;}
        public void SelectPreset(int index){if(profiles==null||profiles.Length==0)return;SelectedPreset=Mathf.Clamp(index,0,profiles.Length-1);}
        public CastleBotBrain SpawnBot()
        {return SpawnBot(null);}
        CastleBotBrain SpawnBot(EnemySpawnSettings enemy)
        {
            var motor=Instantiate(Session.survivorPrefab,staging);motor.name="Candy NPC "+(++serial)+" — "+SelectedName;
            motor.externallyDriven=true;motor.controlMode=FlyingHand.ControlMode.Desktop;
            var avatar=motor.GetComponent<CandyPlayerAvatar>();if(avatar)avatar.remoteDriven=true;
            foreach(var camera in motor.GetComponentsInChildren<Camera>(true)){camera.enabled=false;camera.tag="Untagged";}
            foreach(var listener in motor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            var brain=motor.gameObject.AddComponent<CastleBotBrain>();
            if(enemy!=null){var npc=motor.gameObject.AddComponent<CastleEnemyActor>();npc.Configure(enemy);motor.name="Jaguar enemy "+serial;}
            motor.transform.SetParent(null,true);motor.RefreshMode();
            // Disabled cameras still supply the shared controller's facing and avatar head pose.
            brain.Initialize(this,motor,profiles[SelectedPreset],serial);
            if(enemy==null&&avatar)avatar.ApplyAppearance(BotSpawnAppearance.Pick());
            if(enemy==null&&motor.GetComponent<ISocialBot>()==null)motor.gameObject.AddComponent<SocialBotDriver>();
            motor.jumpHeight=Session.Player.jumpHeight;
            if(!(enemy!=null?PlaceEnemy(brain):Place(brain))){motor.gameObject.SetActive(false);Destroy(motor.gameObject);Status="No clear, stable spawn surface — try rebuilding the castle";return null;}
            bots.Add(brain);brain.Spawned(false);Status="Spawned "+motor.name;return brain;
        }
        public bool Place(CastleBotBrain bot)
        {
            if(bot.GetComponent<CastleEnemyActor>())return PlaceEnemy(bot);
            Physics.SyncTransforms();
            var map=Session.world;var first=map.Blocks.Count>0?map.Blocks[0]:null;
            if(spawnFrame!=Time.frameCount||!ReferenceEquals(spawnWorld,map)||spawnFirst!=first||spawnBlockCount!=map.Blocks.Count)
            {
                spawnFrame=Time.frameCount;spawnWorld=map;spawnFirst=first;spawnBlockCount=map.Blocks.Count;
                spawnSites.Clear();
                foreach(var rb in map.Blocks)
                {
                    if(!rb||!rb.gameObject.activeInHierarchy||!rb.isKinematic&&rb.linearVelocity.magnitude>1)continue;
                    BotGeometrySamples.Fill(rb,spawnSamples);
                    var anchor=rb.transform;var inverse=anchor.worldToLocalMatrix;
                    foreach(var p in spawnSamples)
                        spawnSites.Add(new CastleBotLanding{surface=anchor,body=rb,local=inverse.MultiplyPoint3x4(p),score=p.y});
                }
                spawnSites.Sort((a,b)=>b.score.CompareTo(a.score));
            }
            if(spawnSites.Count==0)
            {
                for(int sample=0;sample<32;sample++)
                {
                    float angle=sample*2.399963f;float radius=1.5f+sample%5;
                    var candidate=map.SafeSpawnPoint()+new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);
                    if(!CastleBotNavigation.Landing(bot.Motor,candidate,out var ground))continue;
                    var point=ground.point+Vector3.up*.06f;
                    if(bots.Any(other=>other&&other!=bot&&!other.Respawning&&(other.transform.position-point).sqrMagnitude<1))continue;
                    bot.Motor.Teleport(point,Quaternion.Euler(0,sample*137.5f,0));return true;
                }
            }
            foreach(var site in spawnSites)
            {
                if(!site.surface||!site.surface.gameObject.activeInHierarchy||site.body&&!site.body.isKinematic&&site.body.linearVelocity.magnitude>1)continue;
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
        GameObject IBotPopulation.SpawnOne() => SpawnBot()?.gameObject;
        IEnumerable<GameObject> ISurvivorBotPopulation.Members => bots.Where(b => b).Select(b => b.gameObject);
        bool ISurvivorBotPopulation.AutoFill { get => autoFill; set => autoFill = value; }
        void IBotPopulation.Clear() => ClearBots();
        void IBotPopulation.MapRebuilt() => CastleRebuilt();
        int ISurvivorBotPopulation.TargetPopulation { get => targetPopulation; set => targetPopulation = value; }
        public void FillToTarget(){autoFill=true;Reconcile();}
        public void SetHumanPlayerCount(int count){humanPlayers=Mathf.Max(0,count);if(Session.HandBots)Session.HandBots.TrimHumanCapacity();if(autoFill)Reconcile();}
        public void ReconcileHandSlots(){if(autoFill)Reconcile();}
        void Reconcile()
        {
            while(Count>FillCapacity)RemoveLastSurvivor();
            int remaining=Mathf.Max(0,FillCapacity-Count);
            for(int i=0;i<Mathf.Min(remaining,4);i++)if(!SpawnBot())break;
        }
        void RemoveLast(){var bot=bots[bots.Count-1];bots.RemoveAt(bots.Count-1);if(bot){bot.gameObject.SetActive(false);Destroy(bot.gameObject);}}
        public void ClearBots(){autoFill=false;while(bots.Count>0)RemoveLast();Status="NPCs cleared";}
        public void CastleRebuilt(){foreach(var bot in bots)if(bot){bot.Motor.jumpHeight=Session.Player.jumpHeight;if(Place(bot))bot.Spawned(false);}}
        void Update()
        {
            if(Session?.world==null)return;
            bots.RemoveAll(b=>!b);
            if(bots.Count>0)WalkingRoutes.Refresh(bots[0]);
            planningQueue.Clear();
            foreach(var bot in bots)if(bot.PlanningPending)planningQueue.Add(bot);
            planningQueue.Sort((a,b)=>b.PlanningPriority.CompareTo(a.PlanningPriority));
            foreach(var bot in planningQueue)bot.AdvancePlanning();
            if(Time.time>=nextPopulation){nextPopulation=Time.time+1;if(autoFill)Reconcile();}
            if(Time.time>=nextHazards)
            {
                nextHazards=Time.time+.2f;hazards.Clear();
                foreach(var rb in Session.world.Blocks)if(rb&&rb.linearVelocity.y<-.8f)hazards.Add(rb.position+rb.linearVelocity*.35f);
            }
        }
        public float ThreatCost(Vector3 point)
        {
            using var measurement = DJBotThreatMarker.Auto();
            float cost=BotHazardRisk.Cost(point)+(Session.HandBots?Session.HandBots.ThreatCost(point):0);
            if(Session.PlayingHand&&Session.Hand.gameObject.activeInHierarchy)
            {
                Vector3 position=handBridge&&handBridge.PhysicalHand&&handBridge.PhysicalHand.IsInteractive?handBridge.PhysicalHand.bodies[0].position:Session.Hand.motor.transform.position;
                cost+=Mathf.Max(0,7-Vector3.Distance(point,position))*.5f;
            }
            cost+=hazards.Cost(point);
            return cost;
        }
        public float Crowding(Vector3 point,CastleBotBrain self)
        {
            if(Time.time>=crowdAt)
            {
                crowdAt=Time.time+.2f;
                if(crowdCells.Count>256)crowdCells.Clear();
                foreach(var list in crowdCells.Values)list.Clear();
                foreach(var bot in bots)
                {
                    if(!bot||bot.Respawning)continue;var key=CrowdCell(bot.transform.position);
                    if(!crowdCells.TryGetValue(key,out var list))crowdCells[key]=list=new List<CastleBotBrain>();
                    list.Add(bot);
                }
            }
            float cost=0;var cell=CrowdCell(point);
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                if(crowdCells.TryGetValue(cell+new Vector3Int(x,y,z),out var list))
                    foreach(var bot in list)if(bot&&bot!=self&&!bot.Respawning)cost+=Mathf.Max(0,1.2f-Vector3.Distance(bot.transform.position,point));
            return cost;
        }
        public float ObjectivePressure(string id,CastleBotBrain self)
        {
            int count=0;
            foreach(var bot in bots)if(bot&&bot!=self&&!bot.Respawning&&bot.Motor.Team==self.Motor.Team&&bot.ObjectiveId==id)count++;
            return count;
        }
        public Vector3 AssignedPosition(BotObjective objective,CastleBotBrain self)
        {
            if(objective.kind==BotObjectiveKind.Attack&&objective.subject&&MatchRuntime.Current?.RulesFor(objective.subject)?.carryingFlag==true&&Vector3.Distance(self.transform.position,objective.position)<7)
            {
                var target=objective.subject.GetComponent<IModeActor>();
                var forward=target==null?objective.subject.transform.forward:target.ViewPose.rotation*Vector3.forward;
                forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
                var flank=objective.position-forward*.9f+Vector3.Cross(Vector3.up,forward)*(Mathf.Repeat(self.Seed,2)<1?-.5f:.5f);
                if(CastleBotNavigation.Ground(flank+Vector3.up*.4f,.8f,out var behind)&&Mathf.Abs(behind.point.y-objective.position.y)<.4f&&CastleBotNavigation.Room(self.Motor,behind.point+Vector3.up*.04f))return behind.point;
            }
            if(objective.kind!=BotObjectiveKind.Guard&&objective.kind!=BotObjectiveKind.Escort&&objective.kind!=BotObjectiveKind.Contest)return objective.position;
            int ordinal=0;
            foreach(var bot in bots){if(bot==self)break;if(bot&&!bot.Respawning&&bot.Motor.Team==self.Motor.Team)ordinal++;}
            float radius=Mathf.Min(objective.radius*.65f,1.8f);
            if(radius<.4f)return objective.position;
            var direction=Quaternion.Euler(0,ordinal*137.508f,0)*Vector3.forward;
            var probe=objective.position+direction*radius;
            if(CastleBotNavigation.Ground(probe+Vector3.up*.4f,.8f,out var top)&&Mathf.Abs(top.point.y-objective.position.y)<.4f&&CastleBotNavigation.Room(self.Motor,top.point+Vector3.up*.04f))return top.point;
            return objective.position;
        }
        public static BotObjective ApplyAssignedPosition(BotObjective objective,Vector3 position)
        {
            if(objective.kind==BotObjectiveKind.Guard||objective.kind==BotObjectiveKind.Escort||objective.kind==BotObjectiveKind.Contest)
            {
                float remaining=objective.radius-Vector3.Distance(objective.position,position);
                if(remaining<.35f)return objective;
                objective.radius=remaining;
            }
            objective.position=position;return objective;
        }
        void OnDestroy()
        {
            WalkingRoutes.Dispose();
            foreach(var bot in bots)if(bot)Destroy(bot.gameObject);
            if(ownsProfiles&&profiles!=null)foreach(var p in profiles)if(p)Destroy(p);
        }
    }
}

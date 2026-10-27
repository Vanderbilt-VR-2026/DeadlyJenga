using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    [DefaultExecutionOrder(150)]
    public sealed partial class CastleBotBrain : MonoBehaviour,ICastleSurvivorDriver
    {
        public CastleBotPersonality Personality;
        public CastleSurvivor Motor {get;private set;}
        public CastleBotPopulation Population {get;private set;}
        public string Intent {get;private set;}="Finding footing";
        public int Jumps {get;private set;}
        public int Respawns {get;private set;}
        public float Seed {get;private set;}
        public bool Respawning {get;private set;}
        public Vector3 WanderDirection {get;private set;}
        public Vector3 Goal=>hasGoal?goal.Point:transform.position;
        public ICastleBotTraversalAbility[] Abilities {get;private set;}
        public bool diagnostics;
        public string DebugState {get;private set;}
        readonly List<CastleBotLanding> candidates=new List<CastleBotLanding>(96);
        CastleBotLanding goal;
        CandyPlayerAvatar avatar;
        CastleBotEmotes poseEmotes;
        bool hasGoal;
        float nextPlan,nextSense,waitUntil,nextJump,respawnAt,progressAt,goalSince;
        Vector3 progressPosition;
        bool safeAhead,obstacle;
        bool playfulHop;
        float hopStarted,armPhase;
        Vector3 previousVisualFeet;
        float variation;
        CastleBotJourneyPlanner planner;
        readonly Queue<CastleBotLanding> journey=new Queue<CastleBotLanding>();
        readonly Dictionary<int,float> visits=new Dictionary<int,float>();
        readonly List<Vector3> recentStops=new List<Vector3>();
        readonly List<float> stopTimes=new List<float>();
        Vector3 tripDestination;
        float tripUntil,nextVaultProbe,dropSpeed;
        int tripNumber;
        public string Activity {get;private set;}="Exploring";
        public Vector3 TripDestination=>tripDestination;
        public bool PrecisionTraversal => hasGoal&&(goal.route!=CastleBotRoute.Walk||journey.Count>0&&journey.Peek().route!=CastleBotRoute.Walk);
        public float TravelDistance {get;private set;}
        public string LastRouteSearch {get;private set;}
        public string LastRouteChange {get;private set;}
        public string RecoveryDiagnostics {get;internal set;}
        public int RemainingWaypoints=>journey.Count+(hasGoal?1:0);
        public float RevisitCost(Rigidbody body)
        {return body&&visits.TryGetValue(body.GetInstanceID(),out float at)?Mathf.Max(0,1-(Time.time-at)/25):0;}
        public float RecentStopCost(Vector3 point)
        {
            float cost=0;
            for(int i=0;i<recentStops.Count;i++)
                cost=Mathf.Max(cost,Mathf.Clamp01(1-Vector3.Distance(point,recentStops[i])/1.1f)*Mathf.Clamp01(1-(Time.time-stopTimes[i])/18));
            return cost;
        }
        public float JourneyProgress(Vector3 point)
        {
            float Remaining(Vector3 p){var d=p-tripDestination;return Vector3.ProjectOnPlane(d,Vector3.up).magnitude*.3f+Mathf.Abs(d.y)*.5f;}
            return Mathf.Clamp(Remaining(transform.position)-Remaining(point),-4,4);
        }
        public void SetJourneyDestination(Vector3 destination,float seconds=30)
        {transitReturns.Clear();transitAscending=false;ascentCommittedUntil=0;SetRouteDestination(destination,seconds);}
        void SetRouteDestination(Vector3 destination,float seconds)
        {
            progressSampleAt=0;progressDistance=float.PositiveInfinity;tripDestination=destination;tripUntil=Time.time+Mathf.Max(5,seconds);planner=null;journey.Clear();hasGoal=false;waitUntil=0;nextPlan=0;Activity="Travelling to selected area";
        }
        public void Initialize(CastleBotPopulation population,CastleSurvivor motor,CastleBotPersonality personality,int number)
        {
            Population=population;Motor=motor;Personality=personality;Motor.InputDriver=this;
            Seed=number*7.193f;variation=Mathf.Lerp(.9f,1.1f,Mathf.PerlinNoise(Seed,0));
            WanderDirection=Quaternion.Euler(0,number*137.508f,0)*Vector3.forward;
            avatar=GetComponent<CandyPlayerAvatar>();
            Abilities=GetComponents<MonoBehaviour>().OfType<ICastleBotTraversalAbility>().ToArray();
            nextPlan=Time.time+number*.035f;progressAt=Time.time;progressPosition=transform.position;
        }
        public void Spawned(bool respawn)
        {
            ResetCombat();
            ReleaseExecution("Spawned");
            Motor.Revive();traversalProgress.Reset();transitReturns.Clear();transitAscending=false;ascentCommittedUntil=0;observedFeet=false;idleFor=0;progressSampleAt=0;steeringVelocity=Vector3.zero;hasObjective=false;nextObjective=0;recovering=false;hasGoal=false;planner=null;journey.Clear();tripUntil=0;waitUntil=0;Respawning=false;Motor.InputSuspended=false;
            if(avatar&&avatar.Rig)avatar.Rig.gameObject.SetActive(true);
            if(respawn)Respawns++;nextPlan=Time.time+.2f+(Seed%1)*.2f;progressAt=Time.time;progressPosition=transform.position;Intent="Finding footing";
        }
        void Update()
        {
            if(!Motor||!Population)return;
            bool matchOwnsLife=MatchRuntime.Current!=null;
            if(matchOwnsLife)
            {
                if(!Motor.Alive){Respawning=true;Motor.InputSuspended=true;Intent="Eliminated — watching survivors";return;}
                if(Respawning){Respawning=false;Motor.InputSuspended=false;Motor.Capsule.enabled=true;}
                if(transform.position.y<Population.deathHeight){Motor.Eliminate(null);return;}
            }
            if(Respawning)
            {
                if(Time.time>=respawnAt)
                {if(Population.Place(this))Spawned(true);else respawnAt=Time.time+1;}
                return;
            }
            if(!matchOwnsLife&&(transform.position.y<Population.deathHeight||Vector3.Distance(transform.position,Population.Session.world.Center)>Mathf.Max(200,Population.Session.world.PlayableBounds.size.magnitude+30)))
            {
                Respawning=true;Intent="Knocked out — respawning";Motor.InputSuspended=true;Motor.Capsule.enabled=false;
                if(avatar&&avatar.Rig)avatar.Rig.gameObject.SetActive(false);
                respawnAt=Time.time+Population.respawnDelay*variation;return;
            }

        }
        void LateUpdate()
        {
            if(!avatar||!avatar.Rig||Respawning)return;
            var frame=avatar.ReadFrame();
            float travelled=Vector3.ProjectOnPlane(frame.feet-previousVisualFeet,Vector3.up).magnitude;
            if(travelled<=Mathf.Max(.15f,Motor.EffectiveWalkSpeed*Time.deltaTime*2))TravelDistance+=travelled;
            float speed=travelled/Mathf.Max(.001f,Time.deltaTime);previousVisualFeet=frame.feet;
            armPhase+=Mathf.Min(speed,6)*Time.deltaTime*5;
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;var hand=side==0?frame.left:frame.right;
                hand.position=frame.feet+transform.rotation*(new Vector3(sign*.27f,.83f,.06f+Mathf.Sin(armPhase+side*Mathf.PI)*Mathf.Clamp01(speed)*.13f)*frame.scale);
                hand.rotation=transform.rotation*Quaternion.Euler(80,0,sign*-10);
                if(side==0)frame.left=hand;else frame.right=hand;
            }
            // Cosmetic social gesture; the shared rig still drives vault contact hands.
            if(Time.time<waitUntil&&Personality.goofiness>.5f&&!Motor.Vaulting)
            {frame.left.position=frame.head+transform.right*(-.2f*frame.scale)+Vector3.up*.08f*frame.scale;frame.left.rotation=transform.rotation*Quaternion.Euler(-65,0,0);}
            if(!poseEmotes)poseEmotes=GetComponent<CastleBotEmotes>();
            avatar.Rig.BeginPoseComposition();
            try
            {
                avatar.OverridePose(frame);
                try{avatar.RefreshExternal(Time.deltaTime);}finally{avatar.ClearPoseOverride();}
                if(poseEmotes&&poseEmotes.isActiveAndEnabled)poseEmotes.ComposePose();
            }
            finally{avatar.Rig.EndPoseComposition();}
        }
        bool GoalSafe()
        {
            if(!hasGoal||!goal.surface||!goal.surface.gameObject.activeInHierarchy)return false;
            if(goal.body&&!goal.body.isKinematic&&goal.body.linearVelocity.magnitude>2)return false;
            return CastleBotNavigation.Landing(Motor,goal.Point,out _);
        }
        void Plan()
        {
            if(!BotPlanningBudget.TrySense(Mathf.RoundToInt(Seed/7.193f)-1,Population.Count)){nextPlan=Time.time+.12f;return;}
            BotPlanningBudget.BeginWork();
            try { PlanWork(); }
            finally { BotPlanningBudget.EndWork(); }
        }
        void PlanWork()
        {
            hasGoal=false;
            if(journey.Count>0&&TakeWaypoint())return;
            if(planner!=null)return;
            if(Time.time>=tripUntil||Vector3.Distance(transform.position,tripDestination)<2)
            {
                if(!ObjectiveDestination(out tripDestination))tripDestination=CastleBotJourneyPlanner.ChooseDestination(this,++tripNumber);
                tripUntil=Time.time+Mathf.Max(5,Personality.travelSeconds)*variation;
                Activity="Exploring another part of the castle";
            }
            if(Vector3.Distance(transform.position,tripDestination)<7&&CastleBotNavigation.Landing(Motor,tripDestination,out var directTop)&&CastleBotNavigation.TryLink(Motor,transform.position,directTop.point,out var directRoute,out dropSpeed))
            {
                goal=new CastleBotLanding{surface=directTop.transform,body=directTop.rigidbody,local=directTop.transform.InverseTransformPoint(directTop.point),route=directRoute};
                hasGoal=true;goalSince=Time.time;Intent="Taking a direct "+directRoute+" route";return;
            }
            if(Population.WalkingRoutes.TryWaypoint(this,tripDestination,out goal))
            {hasGoal=true;goalSince=Time.time;Intent="Following a walking corridor";return;}
            if(hasObjective&&Motor.HasWalkableIsland&&!CastleBotNavigation.ShouldStageAscent(transform.position,tripDestination,false)&&Time.time>=ascentCommittedUntil&&Time.time>=nextDeparture&&Vector3.ProjectOnPlane(tripDestination-transform.position,Vector3.up).magnitude>20)
            {
                nextDeparture=Time.time+3;
                if(CastleBotJourneyPlanner.DepartureStage(this,tripDestination,out var departure,out var approach,out _))
                {
                    PushTransit(departure,25);Activity="Taking a descending exit route";
                    if(CastleBotNavigation.Landing(Motor,departure,out var exitTop)&&CastleBotNavigation.Landing(Motor,approach,out var launchTop)&&CastleBotNavigation.Walkable(Motor,transform.position,launchTop.point)&&CastleBotNavigation.TryLink(Motor,launchTop.point,exitTop.point,out var exitRoute,out dropSpeed))
                    {
                        var exit=new CastleBotLanding{surface=exitTop.transform,body=exitTop.rigidbody,local=exitTop.transform.InverseTransformPoint(exitTop.point),route=exitRoute};
                        journey.Clear();
                        if(Vector3.Distance(transform.position,launchTop.point)<.08f)goal=exit;
                        else {goal=new CastleBotLanding{surface=launchTop.transform,body=launchTop.rigidbody,local=launchTop.transform.InverseTransformPoint(launchTop.point),route=CastleBotRoute.Walk};journey.Enqueue(exit);}
                        hasGoal=true;goalSince=Time.time;return;
                    }
                }
            }
            planner=new CastleBotJourneyPlanner(this,tripDestination);Intent="Planning stepping stones";
            // Get moving on a safe local step while the longer search is spread over frames.
            LocalFallbackWork();
        }
        bool TakeWaypoint()
        {
            while(journey.Count>0)
            {
                var candidate=journey.Dequeue();
                if(!candidate.surface||!CastleBotNavigation.Landing(Motor,candidate.Point,out var top)){if(diagnostics)LastRouteChange="discard missing/covered waypoint "+candidate.Point;continue;}
                float stagingTolerance=candidate.route==CastleBotRoute.Walk&&journey.Count>0&&journey.Peek().route!=CastleBotRoute.Walk?.025f:.3f;
                if(Vector3.Distance(candidate.Point,transform.position)<stagingTolerance)continue;
                if(!CastleBotNavigation.TryLink(Motor,transform.position,top.point,out var route,out dropSpeed))
                {
                    if(!CastleBotNavigation.Route(Motor,top.point,Abilities,out route)){if(diagnostics)LastRouteChange="cannot adopt "+candidate.route+" from "+transform.position+" to "+top.point;journey.Clear();return false;}
                }
                if(diagnostics)LastRouteChange="adopt "+route+" from "+transform.position+" to "+top.point;
                goal=candidate;goal.local=goal.surface.InverseTransformPoint(top.point);goal.route=route;hasGoal=true;goalSince=Time.time;
                Intent=route==CastleBotRoute.Walk?"Following a route":route==CastleBotRoute.Drop?"Dropping to a lower ledge":route==CastleBotRoute.Vault?"Jumping for an edge":"Crossing a gap";return true;
            }
            return false;
        }
        void LocalFallback()
        {
            if(!BotPlanningBudget.TrySense(Mathf.RoundToInt(Seed/7.193f)-1,Population.Count)){nextPlan=Time.time+.12f;return;}
            BotPlanningBudget.BeginWork();
            try { LocalFallbackWork(); }
            finally { BotPlanningBudget.EndWork(); }
        }
        void LocalFallbackWork()
        {
            if(Population.WalkingRoutes.TryWaypoint(this,tripDestination,out goal))
            {hasGoal=true;goalSince=Time.time;Intent="Following a walking corridor";return;}
            CastleBotNavigation.Nearby(this,candidates);
            int checkedRoutes=0;
            foreach(var candidate in candidates)
            {
                if(checkedRoutes++>=32)break;
                if(!CastleBotNavigation.Landing(Motor,candidate.Point,out var top))continue;
                if(!CastleBotNavigation.Route(Motor,top.point,Abilities,out var route))continue;
                if(diagnostics)LastRouteChange="adopt "+route+" from "+transform.position+" to "+top.point;
                goal=candidate;goal.local=goal.surface.InverseTransformPoint(top.point);goal.route=route;hasGoal=true;goalSince=Time.time;
                CastleBotNavigation.TryLink(Motor,transform.position,top.point,out _,out dropSpeed);
                Intent=route==CastleBotRoute.Walk?"Finding safer footing":route==CastleBotRoute.Vault?"Climbing a crate":"Crossing a gap";return;
            }
            Intent="Holding safe ground";nextPlan=Time.time+.25f*variation;
        }
        void Arrived()
        {
            if(goal.body){if(visits.Count>64)visits.Clear();visits[goal.body.GetInstanceID()]=Time.time;}
            if(recentStops.Count>=16){recentStops.RemoveAt(0);stopTimes.RemoveAt(0);}
            recentStops.Add(transform.position);stopTimes.Add(Time.time);
            hasGoal=false;
            if(journey.Count>0&&TakeWaypoint())return;
            bool completed=Time.time>=tripUntil||Vector3.Distance(transform.position,tripDestination)<2;
            if(completed&&!hasObjective&&Population.ThreatCost(transform.position)<.3f)
            {
                float choose=Mathf.PerlinNoise(Seed+tripNumber,tripNumber*.731f);
                float restChance=Mathf.Lerp(.7f,.15f,Personality.journeyDrive);
                if(choose<restChance+Personality.hidePreference*.2f)
                {
                    Activity=choose<Personality.hidePreference?"Laying low for a while":"Chilling between trips";
                    waitUntil=Time.time+Personality.restSeconds*variation*(.6f+choose);Intent=Activity;nextPlan=waitUntil;
                }
                tripUntil=0;
            }
            WanderDirection=Quaternion.Euler(0,47+Seed%90,0)*WanderDirection;
        }
        CastleBotCommand ReadMovementCommand(CastleSurvivor motor,float dt)
        {
            if(!isActiveAndEnabled||Respawning||!Population)return default;
            UpdateObjectives();
            if(motor.Climbing&&!wasObjectiveClimbing)Climbs++;
            wasObjectiveClimbing=motor.Climbing;
            if(diagnostics)DebugState="grounded="+motor.Grounded+" goal="+hasGoal+" planner="+(planner!=null)+" planIn="+(nextPlan-Time.time)+" budget="+BotPlanningBudget.EdgesUsed+" vault="+motor.Vaulting+" wait="+(waitUntil-Time.time);
            var command=new CastleBotCommand();
            if(motor.Vaulting){Intent="Vaulting";return command;}
            if(TacticalHold&&motor.Grounded&&Population.ThreatCost(transform.position)<1)
            {
                planner=null;journey.Clear();hasGoal=false;
                if(!objectiveReached){ObjectivesReached++;objectiveReached=true;}
                command.look=Population.Session.world.Center-transform.position;return command;
            }
            if(hasGoal&&goal.route==CastleBotRoute.Climb)
            {
                var end=goal.Point;
                if(motor.Grounded&&Mathf.Abs(end.y-transform.position.y)<.3f&&Vector3.Distance(end,transform.position)<.5f){Arrived();return command;}
                var flat=Vector3.ProjectOnPlane(end-transform.position,Vector3.up);
                if(CastleBotNavigation.ClimbLink(motor,transform.position,end,out var face))
                {
                    var normal=face.normal;command.look=-normal;command.climbHeld=true;
                    var stage=face.point+normal*(motor.Capsule.radius+.025f);stage.y=transform.position.y;
                    if(!motor.Climbing&&motor.Grounded&&Vector3.Distance(stage,transform.position)>.12f)
                        command.move=Vector3.ClampMagnitude(stage-transform.position,1)*.5f;
                    else
                    {
                        command.climbHeld=true;
                        var right=Vector3.Cross(Vector3.up,-normal).normalized;
                        command.move=-normal+right*Mathf.Clamp(Vector3.Dot(flat,right)*2,-.7f,.7f);
                        Intent="Climbing a real surface";
                    }
                }
                else if(motor.Climbing){command.look=-motor.ClimbNormal;command.climbHeld=true;command.move=-motor.ClimbNormal;}
                else {hasGoal=false;journey.Clear();planner=null;RouteFailures++;nextPlan=Time.time+.2f;}
                if(Time.time-goalSince>Mathf.Max(4,motor.climbGripSeconds+2)){hasGoal=false;journey.Clear();planner=null;RouteFailures++;}
                return command;
            }
            if(playfulHop)
            {if(!motor.Grounded||Time.time-hopStarted<.2f)return command;playfulHop=false;}
            bool danger=Population.ThreatCost(transform.position)>1;
            if(danger){if(Time.time<waitUntil)nextPlan=0;waitUntil=0;Activity="Escaping mayhem";}
            if(Time.time<waitUntil&&motor.Grounded&&!danger){command.look=Population.Session.Player.transform.position-transform.position;return command;}
            if(planner!=null&&motor.Grounded)
            {
                if(planner.Complete)
                {
                    if(diagnostics){LastRouteSearch=planner.Diagnostics;for(int i=0;i<Mathf.Min(4,planner.Result.Count);i++)LastRouteSearch+=" ["+planner.Result[i].route+" "+planner.Result[i].Point+"]";}
                    foreach(var waypoint in planner.Result)journey.Enqueue(waypoint);planner=null;
                    if(!TakeWaypoint()){LocalFallback();if(!hasGoal)tripUntil=0;}
                }
            }
            if(planner!=null&&!hasGoal&&motor.Grounded&&Time.time>=nextFallback)
            {nextFallback=Time.time+.25f;LocalFallback();}
            if(Time.time>=nextPlan&&motor.Grounded)
            {
                nextPlan=Time.time+Personality.decisionInterval*variation;
                if(hasGoal&&(!GoalSafe()||planner==null&&Time.time-goalSince>7)){hasGoal=false;journey.Clear();planner=null;}
                if(danger&&hasGoal&&Population.ThreatCost(goal.Point)>Population.ThreatCost(transform.position)){hasGoal=false;journey.Clear();planner=null;tripUntil=0;}
                if(!hasGoal&&planner==null)Plan();
            }
            if(hasGoal&&(!goal.surface||!goal.surface.gameObject.activeInHierarchy))
            {
                // A removed landing must not resolve to world origin mid-flight.
                hasGoal=false;journey.Clear();planner=null;tripUntil=0;nextPlan=0;
            }
            if(!hasGoal)
            {
                command.look=Population.Session.Player.transform.position-transform.position;
                return command;
            }
            if(goal.route==CastleBotRoute.Climb)
            {
                command.look=Vector3.ProjectOnPlane(goal.Point-transform.position,Vector3.up).normalized;
                return command;
            }
            if(goal.route==CastleBotRoute.Walk&&journey.Count>0&&!PrecisionTraversal)SmoothWalkingRoute();
            var destination=goal.Point;var delta=Vector3.ProjectOnPlane(destination-transform.position,Vector3.up);float distance=delta.magnitude;
            float arrival=goal.route==CastleBotRoute.Walk&&journey.Count>0&&journey.Peek().route!=CastleBotRoute.Walk?.025f:.12f;
            if(distance<arrival&&Mathf.Abs(destination.y-transform.position.y)<.3f&&motor.Grounded)
            {
                Arrived();return command;
            }
            if(goal.route==CastleBotRoute.Ability)
                foreach(var ability in Abilities)if(ability.TryTraverse(motor,destination,out command))return command;
            Vector3 direction=delta.normalized;command.look=direction;
            if(Time.time>=nextSense)
            {
                nextSense=Time.time+.09f;
                // A safe staging point can be close to a lip. Never probe beyond
                // that point and mistake the intended gap for an unsafe approach.
                float lookAhead=Mathf.Min(distance,Mathf.Max(.15f,motor.EffectiveWalkSpeed*.15f));
                safeAhead=CastleBotNavigation.Ground(transform.position+direction*lookAhead+Vector3.up*.25f,.65f,out _);
                obstacle=Physics.Raycast(transform.position+Vector3.up*.3f,direction,Mathf.Min(distance,Mathf.Max(.4f,motor.vaultReach)),CastleBotNavigation.QueryMask,QueryTriggerInteraction.Ignore);
            }
            float pace=Mathf.Min(1,Personality.pace*variation);
            if(diagnostics)DebugState+=" route="+goal.route+" distance="+distance+" rise="+(destination.y-transform.position.y)+" ahead="+safeAhead+" obstacle="+obstacle+" angle="+Vector3.Angle(transform.forward,direction);
            if(motor.Grounded)
            {
                if(Time.time<waitUntil)return command;
                // A finished vault may leave us a short walk from the sampled top point.
                if(goal.route!=CastleBotRoute.Walk&&Mathf.Abs(destination.y-transform.position.y)<.18f&&CastleBotNavigation.Walkable(motor,transform.position,destination))goal.route=CastleBotRoute.Walk;
                bool needsJump=goal.route==CastleBotRoute.Jump||goal.route==CastleBotRoute.Vault;
                if(needsJump)
                {
                    // Keep the climb goal while turning. Discarding it here makes the
                    // idle/social gaze turn back toward the human before alignment finishes.
                    if(Time.time<nextJump||Vector3.Angle(transform.forward,direction)>=25){Intent="Lining up jump";return command;}
                    command.jump=true;pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.EffectiveWalkSpeed),.08f,1);nextJump=Time.time+.1f;Jumps++;Intent="Jumping";
                }
                else if(goal.route==CastleBotRoute.Drop)
                {
                    if(Vector3.Angle(transform.forward,direction)>20)return command;
                    pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.EffectiveWalkSpeed),.08f,1);
                }
                else if(!safeAhead||obstacle){if(diagnostics)LastRouteChange="cancelWalk ahead="+safeAhead+" obstacle="+obstacle+" to "+goal.Point;hasGoal=false;journey.Clear();nextPlan=Time.time+.1f;return command;}
                else pace=Mathf.Min(pace,distance/Mathf.Max(.001f,motor.EffectiveWalkSpeed*dt));
            }
            else
            {
                pace=1;
                // Use the ordinary second jump press only when its real ledge query succeeds.
                if(Time.time>=nextJump&&Time.time>=nextVaultProbe)
                {
                    nextVaultProbe=Time.time+.06f;
                    if(motor.TryFindLedge(out _,out var ledge)&&Mathf.Abs(ledge.y-destination.y)<.4f){command.jump=true;nextJump=Time.time+.12f;Intent="Catching a ledge";}
                }
                if(goal.route==CastleBotRoute.Drop)pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.EffectiveWalkSpeed),.08f,1);
                if(goal.route==CastleBotRoute.Vault)pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.EffectiveWalkSpeed),.08f,1);
                if(goal.route==CastleBotRoute.Jump)
                {
                    float remaining=(motor.VerticalSpeed+Mathf.Sqrt(Mathf.Max(0,motor.VerticalSpeed*motor.VerticalSpeed+2*motor.gravity*(transform.position.y-destination.y))))/motor.gravity;
                    if(remaining>.03f)pace=Mathf.Clamp01(distance/(motor.EffectiveWalkSpeed*remaining));
                }
            }
            command.move=direction*pace;
            // Never shuffle off a safe ledge to make a purely cosmetic joke.
            if(motor.Grounded&&goal.route==CastleBotRoute.Walk&&!command.jump&&safeAhead&&!obstacle&&Time.time>=nextJump&&!hasObjective&&Personality.goofiness>.5f&&Population.ThreatCost(transform.position)<.1f&&Mathf.PerlinNoise(Seed,Time.time*.1f)>.86f)
            {command.jump=true;command.move=Vector3.zero;playfulHop=true;hopStarted=Time.time;nextJump=Time.time+3;Jumps++;Intent="Playful hop";}
            return command;
        }
        void OnDestroy(){ReleaseExecution("Actor destroyed");if(Motor&&ReferenceEquals(Motor.InputDriver,this))Motor.InputDriver=null;}
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastleBots
{
    [DefaultExecutionOrder(150)]
    public sealed class CastleBotBrain : MonoBehaviour,ICastleSurvivorDriver
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
        {
            tripDestination=destination;tripUntil=Time.time+Mathf.Max(5,seconds);planner=null;journey.Clear();hasGoal=false;waitUntil=0;nextPlan=0;Activity="Travelling to selected area";
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
            hasGoal=false;planner=null;journey.Clear();tripUntil=0;waitUntil=0;Respawning=false;Motor.InputSuspended=false;
            if(avatar&&avatar.Rig)avatar.Rig.gameObject.SetActive(true);
            if(respawn)Respawns++;nextPlan=Time.time+.2f+(Seed%1)*.2f;progressAt=Time.time;progressPosition=transform.position;Intent="Finding footing";
        }
        void Update()
        {
            if(!Motor||!Population)return;
            if(Respawning)
            {
                if(Time.time>=respawnAt)
                {if(Population.Place(this))Spawned(true);else respawnAt=Time.time+1;}
                return;
            }
            if(transform.position.y<Population.deathHeight||Vector3.Distance(transform.position,Population.Session.generator.transform.position)>200)
            {
                Respawning=true;Intent="Knocked out — respawning";Motor.InputSuspended=true;Motor.Capsule.enabled=false;
                if(avatar&&avatar.Rig)avatar.Rig.gameObject.SetActive(false);
                respawnAt=Time.time+Population.respawnDelay*variation;return;
            }
            if(Time.time-progressAt>2.5f)
            {
                if(hasGoal&&planner==null&&!Motor.Vaulting&&Vector3.Distance(progressPosition,transform.position)<.18f){hasGoal=false;journey.Clear();tripUntil=0;WanderDirection=Quaternion.Euler(0,107,0)*WanderDirection;}
                progressPosition=transform.position;progressAt=Time.time;
            }
        }
        void LateUpdate()
        {
            if(!avatar||!avatar.Rig||Respawning)return;
            var frame=avatar.ReadFrame();
            float speed=Vector3.ProjectOnPlane(frame.feet-previousVisualFeet,Vector3.up).magnitude/Mathf.Max(.001f,Time.deltaTime);previousVisualFeet=frame.feet;
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
            avatar.OverridePose(frame);avatar.RefreshExternal(Time.deltaTime);avatar.ClearPoseOverride();
        }
        bool GoalSafe()
        {
            if(!hasGoal||!goal.surface||!goal.surface.gameObject.activeInHierarchy)return false;
            if(goal.body&&(Vector3.Dot(goal.body.rotation*Vector3.up,Vector3.up)<.9f||goal.body.linearVelocity.magnitude>2))return false;
            return CastleBotNavigation.Landing(Motor,goal.Point,out _);
        }
        void Plan()
        {
            hasGoal=false;
            if(journey.Count>0&&TakeWaypoint())return;
            if(planner!=null)return;
            if(Time.time>=tripUntil||Vector3.Distance(transform.position,tripDestination)<2)
            {
                tripDestination=CastleBotJourneyPlanner.ChooseDestination(this,++tripNumber);
                tripUntil=Time.time+Mathf.Max(5,Personality.travelSeconds)*variation;
                Activity="Exploring another part of the castle";
            }
            planner=new CastleBotJourneyPlanner(this,tripDestination);Intent="Planning stepping stones";
            // Get moving on a safe local step while the longer search is spread over frames.
            LocalFallback();
        }
        bool TakeWaypoint()
        {
            while(journey.Count>0)
            {
                var candidate=journey.Dequeue();
                if(!candidate.surface||!CastleBotNavigation.Landing(Motor,candidate.Point,out var top))continue;
                if(Vector3.Distance(candidate.Point,transform.position)<.3f)continue;
                if(!CastleBotNavigation.TryLink(Motor,transform.position,top.point,out var route,out dropSpeed))
                {
                    if(!CastleBotNavigation.Route(Motor,top.point,Abilities,out route)){journey.Clear();return false;}
                }
                goal=candidate;goal.local=goal.surface.InverseTransformPoint(top.point);goal.route=route;hasGoal=true;goalSince=Time.time;
                Intent=route==CastleBotRoute.Walk?"Following a route":route==CastleBotRoute.Drop?"Dropping to a lower ledge":route==CastleBotRoute.Vault?"Jumping for an edge":"Crossing a gap";return true;
            }
            return false;
        }
        void LocalFallback()
        {
            CastleBotNavigation.Nearby(this,candidates);
            int checkedRoutes=0;
            foreach(var candidate in candidates)
            {
                if(checkedRoutes++>=18)break;
                if(!CastleBotNavigation.Landing(Motor,candidate.Point,out var top))continue;
                if(!CastleBotNavigation.Route(Motor,top.point,Abilities,out var route))continue;
                goal=candidate;goal.local=goal.surface.InverseTransformPoint(top.point);goal.route=route;hasGoal=true;goalSince=Time.time;
                CastleBotNavigation.TryLink(Motor,transform.position,top.point,out _,out dropSpeed);
                Intent=route==CastleBotRoute.Walk?"Finding safer footing":route==CastleBotRoute.Vault?"Climbing a crate":"Crossing a gap";return;
            }
            Intent="Holding safe ground";nextPlan=Time.time+.6f*variation;
        }
        void Arrived()
        {
            if(goal.body){if(visits.Count>64)visits.Clear();visits[goal.body.GetInstanceID()]=Time.time;}
            if(recentStops.Count>=16){recentStops.RemoveAt(0);stopTimes.RemoveAt(0);}
            recentStops.Add(transform.position);stopTimes.Add(Time.time);
            hasGoal=false;
            if(journey.Count>0&&TakeWaypoint())return;
            bool completed=Time.time>=tripUntil||Vector3.Distance(transform.position,tripDestination)<2;
            if(completed&&Population.ThreatCost(transform.position)<.3f)
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
        public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
        {
            if(!isActiveAndEnabled||Respawning||!Population)return default;
            if(diagnostics)DebugState="grounded="+motor.Grounded+" goal="+hasGoal+" vault="+motor.Vaulting+" wait="+(waitUntil-Time.time);
            var command=new CastleBotCommand();
            if(motor.Vaulting){Intent="Vaulting";return command;}
            if(playfulHop)
            {if(!motor.Grounded||Time.time-hopStarted<.2f)return command;playfulHop=false;}
            bool danger=Population.ThreatCost(transform.position)>1;
            if(danger){if(Time.time<waitUntil)nextPlan=0;waitUntil=0;Activity="Escaping mayhem";}
            if(Time.time<waitUntil&&motor.Grounded&&!danger){command.look=Population.Session.Player.transform.position-transform.position;return command;}
            if(planner!=null&&motor.Grounded)
            {
                planner.Advance(2);
                if(planner.Complete)
                {
                    foreach(var waypoint in planner.Result)journey.Enqueue(waypoint);planner=null;
                    if(!TakeWaypoint()){LocalFallback();if(!hasGoal)tripUntil=0;}
                }
            }
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
            var destination=goal.Point;var delta=Vector3.ProjectOnPlane(destination-transform.position,Vector3.up);float distance=delta.magnitude;
            if(distance<.12f&&Mathf.Abs(destination.y-transform.position.y)<.3f&&motor.Grounded)
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
                float lookAhead=Mathf.Min(distance,Mathf.Max(.15f,motor.walkSpeed*.15f));
                safeAhead=CastleBotNavigation.Ground(transform.position+direction*lookAhead+Vector3.up*.25f,.65f,out _);
                obstacle=Physics.Raycast(transform.position+Vector3.up*.3f,direction,Mathf.Max(.4f,motor.vaultReach),CastleBotNavigation.Mask,QueryTriggerInteraction.Ignore);
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
                    command.jump=true;pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.walkSpeed),.08f,1);nextJump=Time.time+.1f;Jumps++;Intent="Jumping";
                }
                else if(goal.route==CastleBotRoute.Drop)
                {
                    if(Vector3.Angle(transform.forward,direction)>20)return command;
                    pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.walkSpeed),.08f,1);
                }
                else if(!safeAhead||obstacle){hasGoal=false;journey.Clear();nextPlan=Time.time+.1f;return command;}
                else pace=Mathf.Min(pace,distance/Mathf.Max(.001f,motor.walkSpeed*dt));
            }
            else
            {
                pace=1;
                // Use the ordinary second jump press only when its real ledge query succeeds.
                if(Time.time>=nextJump&&Time.time>=nextVaultProbe)
                {
                    nextVaultProbe=Time.time+.06f;
                    if(motor.TryFindLedge(out _,out _)){command.jump=true;nextJump=Time.time+.12f;Intent="Catching a ledge";}
                }
                if(goal.route==CastleBotRoute.Drop)pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.walkSpeed),.08f,1);
                if(goal.route==CastleBotRoute.Vault)pace=Mathf.Clamp(dropSpeed/Mathf.Max(.1f,motor.walkSpeed),.08f,1);
                if(goal.route==CastleBotRoute.Jump)
                {
                    float remaining=(motor.VerticalSpeed+Mathf.Sqrt(Mathf.Max(0,motor.VerticalSpeed*motor.VerticalSpeed+2*motor.gravity*(transform.position.y-destination.y))))/motor.gravity;
                    if(remaining>.03f)pace=Mathf.Clamp01(distance/(motor.walkSpeed*remaining));
                }
            }
            command.move=direction*pace;
            // Never shuffle off a safe ledge to make a purely cosmetic joke.
            if(motor.Grounded&&goal.route==CastleBotRoute.Walk&&!command.jump&&safeAhead&&!obstacle&&Time.time>=nextJump&&Personality.goofiness>.5f&&Population.ThreatCost(transform.position)<.1f&&Mathf.PerlinNoise(Seed,Time.time*.1f)>.86f)
            {command.jump=true;command.move=Vector3.zero;playfulHop=true;hopStarted=Time.time;nextJump=Time.time+3;Jumps++;Intent="Playful hop";}
            return command;
        }
        void OnDestroy(){if(Motor&&ReferenceEquals(Motor.InputDriver,this))Motor.InputDriver=null;}
    }
}

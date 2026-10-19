using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using FlyingHand;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public sealed class CastleHandBotBrain:MonoBehaviour,IFlyingHandInputSource,ICandyHandPoseSource
    {
        public CastleHandBotPersonality Personality;
        public CastleHandActor Actor {get;private set;}
        public CandyFlyingHandBridge Bridge {get;private set;}
        public CastleHandBotPopulation Population {get;private set;}
        public Rigidbody Target {get;private set;}
        public string Intent {get;private set;}="Approaching castle";
        public int Attacks {get;private set;}
        public Vector3 Destination {get;private set;}
        public bool Paused;
        public Vector3 ContactPosition=>Bridge&&Bridge.PhysicalHand?Bridge.PhysicalHand.bodies[0].position:Actor.motor.transform.position;
        enum Phase{Approach,Windup,Strike,Recover}
        enum AttackStyle{Poke,Slap,Punch,Scrape,Grip}
        AttackStyle style;
        Vector3 tangent;
        float wristRoll;
        public string AttackStyleName=>style.ToString();
        Phase phase;
        public bool Preparing => phase==Phase.Windup;
        public bool Striking => phase==Phase.Strike;
        public bool Withdrawing => phase==Phase.Recover;
        float phaseAt,nextChoice,angle,seed,curl=.1f,blockedTime;
        int commandFrame=-1;
        Vector3 outward=Vector3.back,strikeTo,approach,targetStart;
        readonly RaycastHit[] approachHits=new RaycastHit[64];
        readonly List<Rigidbody> fallbackBlocks=new List<Rigidbody>();
        float fallbackScanAt;
        Rigidbody failedTarget;
        float retryAfter;
        readonly HashSet<CandyPhysicalHand> separatedHands=new HashSet<CandyPhysicalHand>();
        public bool ClaimingTarget=>Target&&phase!=Phase.Recover&&!Paused;
        public Vector3 AttackNormal=>outward;
        public int CompletedPushes {get;private set;}
        FlyingHandCommand flight;
        CandyHandPose pose;
        ICastleHandBotAbility[] abilities;
        public void Initialize(CastleHandBotPopulation population,CastleHandActor actor,CastleHandBotPersonality personality,int serial)
        {
            Population=population;Actor=actor;Bridge=actor.GetComponent<CandyFlyingHandBridge>();Personality=personality;
            seed=serial*.371f;angle=serial*137.508f;abilities=GetComponents<MonoBehaviour>().OfType<ICastleHandBotAbility>().ToArray();
            Actor.motor.InputDriver=this;Bridge.PoseSource=this;Restart();
        }
        public void Restart()
        {Target=null;phase=Phase.Approach;phaseAt=Time.time;nextChoice=0;commandFrame=-1;blockedTime=0;curl=.1f;separatedHands.Clear();if(Bridge)Bridge.ResetExternalRig();}
        public void SetTarget(Rigidbody target)
        {
            Target=target;commandFrame=-1;
            if(!target)return;
            var direction=Vector3.ProjectOnPlane(ContactPosition-target.position,Vector3.up);
            BeginTarget(target,direction.sqrMagnitude>.01f?direction.normalized:Vector3.back);
        }
        void BeginTarget(Rigidbody target,Vector3 normal)
        {
            Target=target;outward=normal;phase=Phase.Approach;phaseAt=Time.time;blockedTime=0;
            style=(AttackStyle)((Attacks+Mathf.FloorToInt(seed*7))%5);
            tangent=Vector3.Cross(Vector3.up,outward).normalized*(Mathf.PerlinNoise(seed,Attacks*.71f)>.5f?1:-1);
            wristRoll=Mathf.Lerp(-18,18,Mathf.PerlinNoise(seed+3,Attacks*.31f));
            var box=target.GetComponent<Collider>();
            var surface=box?box.ClosestPoint(target.worldCenterOfMass+outward*(box.bounds.size.magnitude+10)):target.position;
            // Local -Y is the palm face; local +Z becomes upright fingers.
            float size=Bridge.settings.size;
            float clearance=size*(style==AttackStyle.Punch?.085f:.135f)+.6f;
            approach=surface+outward*clearance;
            if(style==AttackStyle.Slap||style==AttackStyle.Scrape)approach+=tangent*size*.045f;
            strikeTo=approach-outward*Mathf.Max(size*.12f,Personality.pushDistance);
            if(style==AttackStyle.Slap||style==AttackStyle.Scrape)strikeTo-=tangent*size*.09f;
            targetStart=target.position;
        }
        bool ClearApproach(Rigidbody target,Vector3 normal)
        {
            var box=target.GetComponent<Collider>();if(!box)return false;
            Vector3 surface=box.ClosestPoint(target.worldCenterOfMass+normal*(box.bounds.size.magnitude+10));
            float distance=Bridge.settings.size*.12f+2;
            int count=Physics.SphereCastNonAlloc(surface+normal*distance,Bridge.settings.size*.035f,-normal,approachHits,distance+.1f,~0,QueryTriggerInteraction.Ignore);
            if(count==approachHits.Length)return false;
            float nearest=float.PositiveInfinity;Rigidbody first=null;
            for(int i=0;i<count;i++)
            {
                var hit=approachHits[i];
                if(hit.transform.IsChildOf(Actor.transform)||(Bridge.PhysicalHand&&hit.transform.IsChildOf(Bridge.PhysicalHand.transform)))continue;
                if(hit.distance<nearest){nearest=hit.distance;first=hit.rigidbody;}
            }
            return first==target;
        }
        void PickTarget()
        {
            if(!Population||Population.Session==null){Target=null;return;}
            float best=float.NegativeInfinity;Rigidbody choice=null;Vector3 normal=Vector3.back;var session=Population.Session;
            if(session.world==null)return;
            float survivorHeight=session.Player&&session.Player.gameObject.activeInHierarchy?session.Player.transform.position.y:float.NegativeInfinity;
            if(session.Bots)foreach(var bot in session.Bots.Bots)if(bot&&!bot.Respawning)survivorHeight=Mathf.Max(survivorHeight,bot.transform.position.y);
            if(float.IsNegativeInfinity(survivorHeight))survivorHeight=session.world.TopHeight;
            var blocks=session.world.Blocks;
            if(blocks==null||blocks.Count==0)
            {
                if(Time.time>=fallbackScanAt)
                {
                    fallbackBlocks.Clear();fallbackScanAt=Time.time+3;
                    foreach(var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    {
                        foreach(var collider in body.GetComponentsInChildren<Collider>())
                            if(Climbables.TryGet(collider,out var climbable)&&climbable.Body==body){fallbackBlocks.Add(body);break;}
                    }
                }
                blocks=fallbackBlocks;
            }
            foreach(var rb in blocks)
            {
                if(!rb||!rb.gameObject.activeInHierarchy||(!rb.isKinematic&&rb.linearVelocity.magnitude>5))continue;
                if(rb.isKinematic && rb.GetComponent<IDamageable>()==null)continue;
                if(Reserved(rb))continue;
                if(rb==failedTarget&&Time.time<retryAfter)continue;
                var radial=Vector3.ProjectOnPlane(rb.position-session.world.Center,Vector3.up);
                var side=Vector3.ProjectOnPlane(ContactPosition-session.world.Center,Vector3.up);
                if(side.sqrMagnitude>1&&radial.sqrMagnitude>1&&Vector3.Dot(side.normalized,radial.normalized)<.25f)continue;
                float attackHeight=Mathf.Lerp(session.world.Center.y+2,survivorHeight-1.2f,Mathf.Lerp(.25f,1,Mathf.PerlinNoise(seed,Attacks*.71f)));
                float score=-Mathf.Abs(rb.position.y-attackHeight)*(.15f+Personality.targetSurvivors*.3f)-Vector3.Distance(rb.position,Actor.motor.transform.position)*.025f;
                score+=radial.magnitude*.08f+Mathf.PerlinNoise(rb.position.x*.2f+seed,rb.position.y*.2f+Time.time*.02f)*Personality.goofiness;
                if(radial.sqrMagnitude>.01f)score+=Vector3.Dot(radial.normalized,Quaternion.Euler(0,angle,0)*Vector3.forward)*1.5f;
                if(score<=best)continue;
                // Face normals give the broad palm a real contact patch, including rotated debris.
                for(int face=0;face<6;face++)
                {
                    var direction=rb.rotation*(face==0?Vector3.forward:face==1?Vector3.back:face==2?Vector3.right:face==3?Vector3.left:face==4?Vector3.up:Vector3.down);
                    if(Mathf.Abs(direction.y)>.55f||Vector3.Dot(direction,radial.normalized)<.3f)continue;
                    if(Vector3.Dot(direction,side.normalized)<.1f)continue;
                    if(ClearApproach(rb,direction)){best=score;choice=rb;normal=direction;break;}
                }
            }
            Target=choice;nextChoice=Time.time+.7f;
            if(choice)BeginTarget(choice,normal);
        }
        bool Reserved(Rigidbody candidate)
        {
            if(!Population)return false;
            foreach(var other in Population.Hands)
                if(other&&other!=this&&other.ClaimingTarget&&Vector3.Distance(other.Target.position,candidate.position)<Bridge.settings.size*.17f)return true;
            return false;
        }
        Vector3 AvoidHands(Vector3 velocity)
        {
            if(!Population)return velocity;
            foreach(var other in Population.Hands)
            {
                if(!other||other==this)continue;
                var otherHand=other.Bridge.PhysicalHand;
                if(Bridge.PhysicalHand&&otherHand&&separatedHands.Add(otherHand))Bridge.PhysicalHand.IgnoreContactsWith(otherHand);
                Vector3 delta=ContactPosition-other.ContactPosition;
                float distance=delta.magnitude,clearance=Bridge.settings.size*.15f;
                if(distance<clearance)
                {
                    Vector3 away=distance>.05f?delta/distance:Quaternion.Euler(0,angle,0)*Vector3.right;
                    velocity+=away*(clearance-distance)*2.5f;
                }
            }
            return velocity;
        }
        void Compute()
        {
            if(commandFrame==Time.frameCount)return;commandFrame=Time.frameCount;flight=default;
            var motor=Actor.motor;float size=Bridge.settings.size;
            pose=new CandyHandPose{tracked=!Paused,skeletonTracked=true,position=motor.transform.position,rotation=AttackRotation(),skeleton=AttackSkeleton()};
            if(Paused){Intent="Paused";return;}
            if(Target&&!Target.gameObject.activeInHierarchy)Target=null;
            if(!Target&&Time.time>=nextChoice)PickTarget();
            if(!Target){Intent="Finding an exposed block";return;}
            foreach(var ability in abilities)if(ability.TryAct(this,Target,out flight,out pose)){Intent="Using hand ability";return;}
            float elapsed=Time.time-phaseAt,desiredCurl=.1f;
            var physical=Bridge.PhysicalHand;
            float separation=Vector3.Distance(ContactPosition,motor.transform.position);
            blockedTime=separation>size*.04f?blockedTime+Time.deltaTime:0;
            if(phase!=Phase.Recover&&((phase==Phase.Approach&&blockedTime>3)||elapsed>12||Vector3.Distance(Target.position,targetStart)>size*.35f))
            {failedTarget=Target;retryAfter=Time.time+12;phase=Phase.Recover;phaseAt=Time.time;}
            switch(phase)
            {
                case Phase.Approach:
                    Intent="Reaching fingers toward a block";Destination=approach;
                    if(physical&&physical.IsInteractive&&Vector3.Distance(ContactPosition,approach)<.9f&&Quaternion.Angle(physical.bodies[0].rotation,pose.rotation)<35)
                    {phase=Phase.Windup;phaseAt=Time.time;}
                    break;
                case Phase.Windup:
                    Intent="Feeling for contact";Destination=approach;desiredCurl=.14f;
                    if(elapsed>Personality.settleSeconds&&physical&&physical.IsInteractive)
                    {
                        phase=Phase.Strike;phaseAt=Time.time;
                        targetStart=Target.position;Attacks++;
                    }
                    break;
                case Phase.Strike:
                    Intent=style+" — following through";Destination=strikeTo;desiredCurl=style==AttackStyle.Grip?.65f:style==AttackStyle.Punch?.8f:.14f;
                    if(style==AttackStyle.Grip&&elapsed>Personality.attackSeconds*.55f)
                        Destination=strikeTo+outward*(elapsed-Personality.attackSeconds*.55f)*Mathf.Min(3,Personality.pushSpeed);
                    bool moved=Vector3.Distance(Target.position,targetStart)>.45f;
                    if((moved&&elapsed>Personality.attackSeconds)||elapsed>Personality.attackSeconds+1.8f)
                    {if(moved)CompletedPushes++;else{failedTarget=Target;retryAfter=Time.time+12;}phase=Phase.Recover;phaseAt=Time.time;}
                    break;
                default:
                    Intent="Releasing and backing away";Destination=approach+outward*Mathf.Max(2,size*.12f);
                    if(elapsed>Personality.recoverySeconds&&(Vector3.Distance(ContactPosition,Destination)<.5f||elapsed>8))
                    {Target=null;nextChoice=Time.time+.5f;}
                    break;
            }
            Vector3 delta=Destination-motor.transform.position;
            float speed=phase==Phase.Strike?Personality.pushSpeed*Mathf.Lerp(.75f,1.25f,Personality.aggression):Personality.flightSpeed;
            var desiredVelocity=Vector3.ClampMagnitude(delta*(phase==Phase.Strike?3:2),Mathf.Min(speed,motor.maximumSpeed));
            desiredVelocity=Vector3.ClampMagnitude(AvoidHands(desiredVelocity),motor.maximumSpeed);
            var acceleration=(desiredVelocity-motor.Body.linearVelocity)*2+desiredVelocity*motor.drag;
            flight.moveWorld=new Vector3(acceleration.x/Mathf.Max(.1f,motor.thrust),acceleration.y/Mathf.Max(.1f,motor.verticalThrust),acceleration.z/Mathf.Max(.1f,motor.thrust));
            float yaw=Vector3.SignedAngle(motor.transform.forward,-outward,Vector3.up);
            flight.yaw=Mathf.Clamp(yaw/35f,-1,1);
            curl=Mathf.MoveTowards(curl,desiredCurl,Time.deltaTime*.4f);
            pose.rotation=AttackRotation();
            pose.skeleton=AttackSkeleton();
        }
        Quaternion AttackRotation()
        {
            var forward=-outward;
            float drift=Mathf.Sin(Time.time*1.3f+seed)*Mathf.Lerp(2,9,Personality?Personality.goofiness:.3f);
            return Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,drift,wristRoll);
        }
        CandyHandSkeleton AttackSkeleton()
        {
            var kind=style==AttackStyle.Poke?DesktopHandPose.Pointer:style==AttackStyle.Punch?DesktopHandPose.Fist:style==AttackStyle.Grip?DesktopHandPose.Cup:DesktopHandPose.Flat;
            return DesktopHandPoses.Build(Bridge.useLeftHand,kind,style==AttackStyle.Grip?curl*.5f:0);
        }
        public static CandyHandSkeleton Articulate(bool left,float curl)
        {
            var rest=CandyHandSkeleton.Rest(left);var result=rest;
            // Forward kinematics preserve each phalanx length as the fingers bend.
            for(int f=0;f<5;f++)
            {
                Vector3 p=rest.Joint(f*4);
                for(int j=0;j<3;j++)
                {
                    var delta=rest.Joint(f*4+j+1)-rest.Joint(f*4+j);
                    var rotation=f==0?Quaternion.AngleAxis((left?-1:1)*curl*(j+1)*15,Vector3.up):Quaternion.AngleAxis(curl*(j+1)*30,Vector3.right);
                    p+=rotation*delta;result.SetJoint(f*4+j+1,p);
                }
            }
            return result;
        }
        public FlyingHandCommand ReadFlightInput(FlyingHandController motor,float dt){Compute();return flight;}
        public bool TryGetPose(out CandyHandPose value){Compute();value=pose;return pose.tracked;}
    }
}

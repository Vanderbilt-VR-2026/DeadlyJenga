using System.Linq;
using UnityEngine;
using FlyingHand;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastlePrototype;

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
        Phase phase;
        float phaseAt,nextChoice,angle,seed,curl=.18f,blockedTime;
        int commandFrame=-1;
        Vector3 outward=Vector3.back,strikeTo,approach,targetStart;
        readonly RaycastHit[] approachHits=new RaycastHit[64];
        Rigidbody failedTarget;
        float retryAfter;
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
        {Target=null;phase=Phase.Approach;phaseAt=Time.time;nextChoice=0;commandFrame=-1;blockedTime=0;curl=.18f;if(Bridge)Bridge.ResetExternalRig();}
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
            var box=target.GetComponent<Collider>();
            var surface=box?box.ClosestPoint(target.worldCenterOfMass+outward*(box.bounds.size.magnitude+10)):target.position;
            // Local -Y is the palm face; local +Z becomes upright fingers.
            approach=surface+outward*(Bridge.settings.size*.02f+1.2f)-Vector3.up*(Bridge.settings.size*.02f);
            strikeTo=approach-outward*1.65f;targetStart=target.position;
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
            float survivorHeight=session.Player.gameObject.activeInHierarchy?session.Player.transform.position.y:float.NegativeInfinity;
            foreach(var bot in session.Bots.Bots)if(bot&&!bot.Respawning)survivorHeight=Mathf.Max(survivorHeight,bot.transform.position.y);
            if(float.IsNegativeInfinity(survivorHeight))survivorHeight=session.world.TopHeight;
            foreach(var rb in session.world.Blocks)
            {
                if(!rb||!rb.gameObject.activeInHierarchy||rb.linearVelocity.magnitude>3||Vector3.Dot(rb.rotation*Vector3.up,Vector3.up)<.8f)continue;
                if(rb==failedTarget&&Time.time<retryAfter)continue;
                var radial=Vector3.ProjectOnPlane(rb.position-session.world.Center,Vector3.up);
                var side=Vector3.ProjectOnPlane(ContactPosition-session.world.Center,Vector3.up);
                if(side.sqrMagnitude>1&&radial.sqrMagnitude>1&&Vector3.Dot(side.normalized,radial.normalized)<.8f)continue;
                float score=-Mathf.Abs(rb.position.y-(survivorHeight-1.2f))*(.3f+Personality.targetSurvivors)-Vector3.Distance(rb.position,Actor.motor.transform.position)*.025f;
                score+=radial.magnitude*.08f+Mathf.PerlinNoise(rb.position.x*.2f+seed,rb.position.y*.2f+Time.time*.02f)*Personality.goofiness;
                if(radial.sqrMagnitude>.01f)score+=Vector3.Dot(radial.normalized,Quaternion.Euler(0,angle,0)*Vector3.forward)*1.5f;
                var direction=radial.sqrMagnitude>.01f?radial.normalized:Vector3.back;
                if(score>best&&ClearApproach(rb,direction)){best=score;choice=rb;normal=direction;}
            }
            Target=choice;nextChoice=Time.time+2;
            if(choice)BeginTarget(choice,normal);
        }
        void Compute()
        {
            if(commandFrame==Time.frameCount)return;commandFrame=Time.frameCount;flight=default;
            var motor=Actor.motor;float size=Bridge.settings.size;
            pose=new CandyHandPose{tracked=!Paused,skeletonTracked=true,position=motor.transform.position,rotation=Quaternion.LookRotation(Vector3.up,outward),skeleton=Articulate(Bridge.useLeftHand,curl)};
            if(Paused){Intent="Paused";return;}
            if(Target&&!Target.gameObject.activeInHierarchy)Target=null;
            if(!Target&&Time.time>=nextChoice)PickTarget();
            if(!Target){Intent="Waiting for a surviving castle";return;}
            foreach(var ability in abilities)if(ability.TryAct(this,Target,out flight,out pose)){Intent="Using hand ability";return;}
            float elapsed=Time.time-phaseAt,desiredCurl=.18f;
            var physical=Bridge.PhysicalHand;
            float separation=Vector3.Distance(ContactPosition,motor.transform.position);
            blockedTime=separation>size*.04f?blockedTime+Time.deltaTime:0;
            if(phase!=Phase.Recover&&(blockedTime>1||elapsed>15||Vector3.Distance(Target.position,targetStart)>size*.15f))
            {failedTarget=Target;retryAfter=Time.time+12;phase=Phase.Recover;phaseAt=Time.time;}
            switch(phase)
            {
                case Phase.Approach:
                    Intent="Placing palm against a block";Destination=approach;
                    if(physical&&physical.IsInteractive&&Vector3.Distance(ContactPosition,approach)<.3f&&physical.bodies[0].linearVelocity.magnitude<.5f&&Quaternion.Angle(physical.bodies[0].rotation,pose.rotation)<12)
                    {phase=Phase.Windup;phaseAt=Time.time;}
                    break;
                case Phase.Windup:
                    Intent="Settling fingers";Destination=approach;desiredCurl=.3f;
                    if(elapsed>.6f&&physical&&physical.IsInteractive&&physical.bodies[0].linearVelocity.magnitude<.35f)
                    {
                        phase=Phase.Strike;phaseAt=Time.time;
                        targetStart=Target.position;Attacks++;
                    }
                    break;
                case Phase.Strike:
                    Intent="Pushing with palm";Destination=strikeTo;desiredCurl=.3f;
                    bool moved=Vector3.Dot(Target.position-targetStart,-outward)>.25f;
                    if(moved||elapsed>Personality.attackSeconds+2)
                    {if(moved)CompletedPushes++;else{failedTarget=Target;retryAfter=Time.time+12;}phase=Phase.Recover;phaseAt=Time.time;}
                    break;
                default:
                    Intent="Releasing and backing away";Destination=approach+outward*.8f;
                    if(elapsed>Personality.recoverySeconds&&(Vector3.Distance(ContactPosition,Destination)<.5f||elapsed>8))
                    {Target=null;nextChoice=Time.time+.5f;}
                    break;
            }
            Vector3 delta=Destination-motor.transform.position;
            float speed=phase==Phase.Strike?Mathf.Lerp(.8f,1.5f,Personality.aggression):Mathf.Min(Personality.flightSpeed,2.5f);
            if(phase==Phase.Approach&&delta.magnitude<2)speed=Mathf.Min(speed,1);
            var desiredVelocity=Vector3.ClampMagnitude(delta*1.5f,Mathf.Min(speed,motor.maximumSpeed));
            var acceleration=(desiredVelocity-motor.Body.linearVelocity)*2+desiredVelocity*motor.drag;
            flight.moveWorld=new Vector3(acceleration.x/Mathf.Max(.1f,motor.thrust),acceleration.y/Mathf.Max(.1f,motor.verticalThrust),acceleration.z/Mathf.Max(.1f,motor.thrust));
            float yaw=Vector3.SignedAngle(motor.transform.forward,-outward,Vector3.up);
            flight.yaw=Mathf.Clamp(yaw/35f,-1,1);
            curl=Mathf.MoveTowards(curl,desiredCurl,Time.deltaTime*.4f);
            pose.rotation=Quaternion.LookRotation(Vector3.up,outward);
            pose.skeleton=Articulate(Bridge.useLeftHand,curl);
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

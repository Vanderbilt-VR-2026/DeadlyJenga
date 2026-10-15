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
        float phaseAt,nextChoice,angle,seed;
        int commandFrame=-1;
        Vector3 outward=Vector3.back,strikeTo;
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
        {Target=null;phase=Phase.Approach;phaseAt=Time.time;nextChoice=0;commandFrame=-1;if(Bridge)Bridge.ResetExternalRig();}
        public void SetTarget(Rigidbody target)
        {Target=target;phase=Phase.Approach;phaseAt=Time.time;nextChoice=Time.time+15;commandFrame=-1;}
        void PickTarget()
        {
            float best=float.NegativeInfinity;Rigidbody choice=null;var session=Population.Session;
            float survivorHeight=session.Player.gameObject.activeInHierarchy?session.Player.transform.position.y:float.NegativeInfinity;
            foreach(var bot in session.Bots.Bots)if(bot&&!bot.Respawning)survivorHeight=Mathf.Max(survivorHeight,bot.transform.position.y);
            if(float.IsNegativeInfinity(survivorHeight))survivorHeight=session.generator.transform.position.y+session.generator.settings.keepCourses*session.generator.settings.blockSize.y;
            foreach(var rb in session.generator.Bodies)
            {
                if(!rb||!rb.gameObject.activeInHierarchy||rb.linearVelocity.magnitude>3||Vector3.Dot(rb.rotation*Vector3.up,Vector3.up)<.8f)continue;
                var radial=Vector3.ProjectOnPlane(rb.position-session.generator.transform.position,Vector3.up);
                float score=-Mathf.Abs(rb.position.y-(survivorHeight-1.2f))*(.3f+Personality.targetSurvivors)-Vector3.Distance(rb.position,Actor.motor.transform.position)*.025f;
                score+=radial.magnitude*.08f+Mathf.PerlinNoise(rb.position.x*.2f+seed,rb.position.y*.2f+Time.time*.02f)*Personality.goofiness;
                if(radial.sqrMagnitude>.01f)score+=Vector3.Dot(radial.normalized,Quaternion.Euler(0,angle,0)*Vector3.forward)*1.5f;
                if(score>best){best=score;choice=rb;}
            }
            Target=choice;nextChoice=Time.time+3;
        }
        void Compute()
        {
            if(commandFrame==Time.frameCount)return;commandFrame=Time.frameCount;flight=default;
            var motor=Actor.motor;float size=Bridge.settings.size;
            pose=new CandyHandPose{tracked=!Paused,skeletonTracked=true,position=motor.transform.position,rotation=motor.transform.rotation,skeleton=Articulate(Bridge.useLeftHand,.12f)};
            if(Paused){Intent="Paused";return;}
            if(!Target||!Target.gameObject.activeInHierarchy||phase==Phase.Approach&&Time.time>=nextChoice)PickTarget();
            if(!Target){Intent="Waiting for a surviving castle";return;}
            foreach(var ability in abilities)if(ability.TryAct(this,Target,out flight,out pose)){Intent="Using hand ability";return;}
            var center=Population.Session.generator.transform.position;
            var radial=Vector3.ProjectOnPlane(Target.position-center,Vector3.up);
            if(radial.sqrMagnitude<1)radial=Quaternion.Euler(0,angle,0)*Vector3.forward;
            outward=radial.normalized;
            var box=Target.GetComponent<Collider>();var surface=box?box.ClosestPoint(Target.position+outward*20):Target.position;
            float reach=size*.12f;
            Vector3 approach=surface+outward*(reach+1.5f)+Vector3.up*.15f;
            float elapsed=Time.time-phaseAt,curl=.12f;
            switch(phase)
            {
                case Phase.Approach:
                    Intent="Lining up a crate";Destination=approach;
                    if(Vector3.Distance(motor.transform.position,approach)<.55f&&Mathf.Abs(Vector3.SignedAngle(motor.transform.forward,-outward,Vector3.up))<20)
                    {phase=Phase.Windup;phaseAt=Time.time;}
                    break;
                case Phase.Windup:
                    Intent="Winding up";Destination=approach+outward*.5f;curl=.35f;
                    if(elapsed>.65f+Personality.goofiness*.5f)
                    {
                        phase=Phase.Strike;phaseAt=Time.time;
                        // Wrist stays outside the crate; the current physical fingers deliver contact.
                        var tangent=Vector3.Cross(Vector3.up,outward);
                        strikeTo=surface+outward*(reach*.35f)+tangent*(Personality.goofiness-.25f)*size*.025f;
                        Attacks++;
                    }
                    break;
                case Phase.Strike:
                    Intent="Swiping blocks";Destination=strikeTo;curl=.08f;
                    if(elapsed>Personality.attackSeconds+1.1f){phase=Phase.Recover;phaseAt=Time.time;}
                    break;
                default:
                    Intent="Backing off";Destination=approach+outward*(1+Personality.goofiness);curl=.12f;
                    if(elapsed>Personality.recoverySeconds){phase=Phase.Approach;phaseAt=Time.time;nextChoice=0;}
                    break;
            }
            Vector3 delta=Destination-motor.transform.position;
            float speed=Personality.flightSpeed*(phase==Phase.Strike?1+Personality.aggression*.5f:1);
            var desiredVelocity=Vector3.ClampMagnitude(delta*1.5f,Mathf.Min(speed,motor.maximumSpeed));
            var acceleration=(desiredVelocity-motor.Body.linearVelocity)*2+desiredVelocity*motor.drag;
            flight.moveWorld=new Vector3(acceleration.x/Mathf.Max(.1f,motor.thrust),acceleration.y/Mathf.Max(.1f,motor.verticalThrust),acceleration.z/Mathf.Max(.1f,motor.thrust));
            float yaw=Vector3.SignedAngle(motor.transform.forward,-outward,Vector3.up);
            flight.yaw=Mathf.Clamp(yaw/35f,-1,1);
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

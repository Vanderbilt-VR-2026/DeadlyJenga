using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Wildlife
{
    [DisallowMultipleComponent]
    public sealed class IslandAnimal : MonoBehaviour,IWildlifeActor
    {
        IslandWildlife population;
        IslandAnimalModel model;
        CharacterController controller;
        Transform visual;
        Vector3 home,goal;
        float nextThink,nextStep,nextPose,vertical,phase,travel,stuckAt;
        Vector3 lastPosition;
        uint random;
        public string Species {get;private set;}
        public bool Tame => FollowTarget;
        public Transform FollowTarget {get;private set;}
        public float DistanceTravelled {get;private set;}
        public int Decisions {get;private set;}
        public bool Hovering => Species=="ray";
        public void Initialize(IslandWildlife owner,string species,int seed)
        {
            population=owner;Species=species;random=(uint)(seed*7919+1);phase=Next()*20;
            home=goal=lastPosition=transform.position;
            var child=new GameObject("Visual");visual=child.transform;visual.SetParent(transform,false);
            model=child.AddComponent<IslandAnimalModel>();model.Build(species);
            gameObject.layer=2;
            controller=gameObject.AddComponent<CharacterController>();controller.radius=species=="crab"?.38f:.27f;
            controller.height=species=="fox"?1.14f:species=="otter"||species=="monkey"?1.0f:species=="jaguar"||species=="penguin"?.92f:.72f;controller.center=Vector3.up*(controller.height*.5f);
            controller.includeLayers=IslandWildlife.GroundMask;controller.layerOverridePriority=100;
            controller.skinWidth=.025f;controller.stepOffset=.15f;controller.slopeLimit=48;controller.minMoveDistance=0;
            // Wildlife is non-blocking to players. Its controller only sweeps against the map.
            int survivor=Layers.Survivor;controller.excludeLayers=(1<<2)|(survivor>=0?1<<survivor:0);
            nextThink=Time.unscaledTime+Next()*.3f;nextStep=Time.unscaledTime;
        }
        public void SetCompanion(Transform target){FollowTarget=target;home=transform.position;nextThink=0;}
        float Next(){random^=random<<13;random^=random>>17;random^=random<<5;return (random&0xffffff)/16777216f;}
        float Speed => Species=="tortoise"?.32f:Species=="seal"?.65f:Species=="boar"?1.05f:Species=="jaguar"?1.75f:1.25f;
        void Update()
        {
            if(!population||!controller)return;
            float now=Time.unscaledTime;
            var camera=population.Session?.ActiveCamera;
            float distance=camera?Vector3.Distance(camera.transform.position,transform.position):0;
            if(now>=nextThink){nextThink=now+.55f+Next()*.35f;Think();}
            if(now>=nextStep)
            {
                float step=distance>45?.18f:.05f;nextStep=now+step;Move(step,now);
            }
            if(now>=nextPose&&distance<65)
            {
                nextPose=now+(distance>30?.12f:.033f);model.Animate(now+phase,travel);
                float bob=Hovering?.5f+Mathf.Sin(now*2+phase)*.09f:Species=="frog"?Mathf.Max(0,Mathf.Sin(now*7+phase))*.10f*travel:Mathf.Sin(now*5+phase)*.012f*travel;
                visual.localPosition=Vector3.up*bob;
                visual.localRotation=Species=="penguin"?Quaternion.Euler(0,0,Mathf.Sin(now*7+phase)*travel*7):Quaternion.identity;
            }
            if(transform.position.y<(population.World?.Center.y??0)-20)Destroy(gameObject);
        }
        void Think()
        {
            Decisions++;
            if(FollowTarget)
            {
                var delta=FollowTarget.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude>9)
                {
                    Vector3 desired=FollowTarget.position-delta.normalized*1.8f;
                    if(population.TryGround(desired,FollowTarget.position.y+1,5,out var floor))goal=floor;
                }
                else goal=transform.position;
                return;
            }
            if((goal-transform.position).sqrMagnitude>.7f&&Time.unscaledTime-stuckAt<3)return;
            if(Next()<.25f){goal=transform.position;return;}
            for(int i=0;i<6;i++)
            {
                float angle=Next()*Mathf.PI*2;float radius=i<3?1.0f+Next()*3.5f:.45f+Next()*.45f;
                var origin=i<3?home:transform.position;
                var candidate=origin+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(population.TryGround(candidate,transform.position.y+1,3,out var p))
                {
                    var delta=p-transform.position;delta.y=0;
                    if(delta.sqrMagnitude>.1f&&!Physics.SphereCast(transform.position+Vector3.up*.4f,.20f,delta.normalized,out _,delta.magnitude,IslandWildlife.GroundMask,QueryTriggerInteraction.Ignore)){goal=p;break;}
                }
            }
            stuckAt=Time.unscaledTime;
        }
        void Move(float dt,float now)
        {
            var direction=goal-transform.position;direction.y=0;
            bool go=direction.sqrMagnitude>.10f;
            if(go)
            {
                direction.Normalize();
                var probe=transform.position+direction*(controller.radius+.30f);
                // Stop at unsupported edges instead of walking off platforms or teleporting.
                if(!population.TryGround(probe,transform.position.y+.8f,1.6f,out _)){go=false;goal=transform.position;nextThink=now+.4f;}
                else transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*170);
            }
            float speed=Speed*(Tame?1.25f:1);
            vertical=controller.isGrounded?-1:Mathf.Max(-12,vertical-20*dt);
            if(Species=="frog"&&go&&controller.isGrounded&&Mathf.Sin(now*3+phase)>.92f)vertical=3.5f;
            var motion=(go?direction*speed:Vector3.zero)+Vector3.up*vertical;
            var flags=controller.Move(motion*dt);
            if((flags&CollisionFlags.Sides)!=0){goal=transform.position;nextThink=now+.15f;}
            var delta=transform.position-lastPosition;delta.y=0;DistanceTravelled+=delta.magnitude;lastPosition=transform.position;
            travel=Mathf.Lerp(travel,go?1:0,.35f);
        }
    }
}

using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor
    {
        [Header("Short scramble / cling")]
        public bool climbingEnabled=true;
        public float climbSpeed=1.8f;
        [Tooltip("Stamina capacity in effort units. At default drain, one unit is one second of moving climb.")]
        [Min(.2f)] public float climbGripSeconds=4;
        [Tooltip("Normalized recovery speed: .65 restores 65% per second with the linear curve. Zero disables recovery.")]
        [Min(0)] public float climbRecoveryPerSecond=.65f;
        [Tooltip("Maximum nearby seam search in human-scale metres; scaled with the player. Never bridges empty space without finding a real face.")]
        public float climbSeamReach=.35f;
        [Tooltip("Fast falling, spinning and violently accelerated blocks break grip.")]
        public float climbBreakSpeed=5.5f,climbBreakFallSpeed=2.5f,climbBreakSpin=1.8f,climbBreakAcceleration=22;
        public bool Climbing { get; private set; }
        public float GripRemaining { get; private set; }=1;
        public float ClimbProgress=>Mathf.Clamp01(climbElapsed/.14f);
        public Vector3 ClimbNormal=>climbNormal;
        IClimbable climbBlock;
        Rigidbody climbBody;
        Collider climbCollider;
        Vector3 climbNormal,climbLocalFeet,climbWorldFeet,climbPointVelocity,climbLastVelocity;
        Quaternion climbLastRotation;
        float climbHold,climbCooldown,climbElapsed,climbGroundRest,climbGait,climbStun;
        bool climbMustRelease;
        readonly Vector3[] climbHandLocal=new Vector3[2];
        readonly Vector3[] climbHandFrom=new Vector3[2];
        readonly float[] climbHandReach=new float[2];
        int climbMovingHand=-1;
        Vector3 vrClimbVelocity;
        [Min(.1f)] public float vrClimbAcceleration=10f;
        void ResetClimb(bool refill)
        {
            Climbing=false;climbBlock=null;climbBody=null;climbCollider=null;
            vrClimbVelocity=Vector3.zero;
            climbHold=climbCooldown=climbElapsed=climbGroundRest=climbStun=0;climbMustRelease=false;
            if(refill)GripRemaining=1;
        }
        int ClimbMask=>TraversalCollision.ClimbMask;
        bool ClimbFace(Vector3 origin,Vector3 direction,float distance,out RaycastHit hit)
        {
            if(!Physics.Raycast(origin,direction,out hit,distance,ClimbMask,QueryTriggerInteraction.Ignore))return false;
            float angle=Vector3.Angle(hit.normal,Vector3.up);
            return angle>=55&&angle<=100&&Climbables.TryGet(hit.collider,out var block)&&block.CanClimb&&block.Grip>0&&(ClimbPermission==null||ClimbPermission(block))&&(MatchRuntime.Current==null||MatchRuntime.Current.CanClimb(gameObject,block));
        }
        bool FindClimbFace(Vector3 feet,Vector3 direction,out RaycastHit hit)
        {
            float reach=Capsule.radius+.48f*playerScale;
            bool WithinSeam(RaycastHit candidate)
            {
                if(!Climbing)return true;
                Vector3 lateral=Vector3.Cross(Vector3.up,climbNormal).normalized;
                return Mathf.Abs(Vector3.Dot(candidate.point-Feet,lateral))<=Mathf.Clamp(climbSeamReach,0,.45f)*playerScale+.01f;
            }
            // Probe above the head first so an overhanging next course is found before
            // the capsule hits its underside. Retreat the ray outside a small outset;
            // raycasts beginning inside that next crate otherwise miss its near face.
            for(int i=0;i<3;i++)
            {
                Vector3 origin=feet+Vector3.up*(StandingHeight*(1.08f-i*.23f));
                if(ClimbFace(origin,direction,reach,out hit)&&WithinSeam(hit))return true;
                float retreat=Mathf.Clamp(climbSeamReach,0,.45f)*playerScale;
                if(ClimbFace(origin-direction*retreat,direction,reach+retreat,out hit)&&Vector3.Distance(origin,hit.point)<=reach&&WithinSeam(hit))return true;
            }
            hit=default;return false;
        }
        Vector3 PointVelocity()=>climbBody&&Climbables.Alive(climbBlock)?climbBody.GetPointVelocity(climbBlock.Anchor.TransformPoint(climbLocalFeet)):Vector3.zero;
        bool UnsafeClimb(Vector3 velocity,float dt,bool initial)
        {
            if(!Climbables.Alive(climbBlock)||!climbCollider||!climbCollider.enabled||!climbBlock.CanClimb)return true;
            dt=Mathf.Max(dt,Time.fixedDeltaTime); // Rigidbody samples advance at the physics rate.
            float spin=climbBody?climbBody.angularVelocity.magnitude:0;
            if(!initial)spin=Mathf.Max(spin,Quaternion.Angle(climbLastRotation,climbBlock.Anchor.rotation)*Mathf.Deg2Rad/Mathf.Max(.001f,dt));
            return velocity.magnitude>climbBreakSpeed||velocity.y < -climbBreakFallSpeed||spin>climbBreakSpin||
                (!initial&&(velocity-climbLastVelocity).magnitude/Mathf.Max(.001f,dt)>climbBreakAcceleration);
        }
        void BindClimb(RaycastHit hit)
        {
            Climbables.TryGet(hit.collider,out climbBlock);climbBody=climbBlock.Body;climbCollider=hit.collider;
            climbNormal=hit.normal;climbLocalFeet=climbBlock.Anchor.InverseTransformPoint(Feet);climbWorldFeet=Feet;
            climbLastVelocity=PointVelocity();climbLastRotation=climbBlock.Anchor.rotation;
        }
        void ReleaseClimb(string reason,bool forced)
        {
            Vector3 inherited=Vector3.ClampMagnitude(climbPointVelocity,12);
            Climbing=false;climbBlock=null;climbBody=null;climbCollider=null;Grounded=false;support=null;
            verticalSpeed=Mathf.Clamp(inherited.y,-10,8);slopeVelocity=Vector3.ProjectOnPlane(inherited,Vector3.up)+climbNormal*(forced?1.8f:.65f);
            jumped=true;lastGrounded=-100;airborneSince=Time.time;climbCooldown=forced?.85f:.3f;climbMustRelease=forced;
            climbHold=0;ActionStatus=reason;
            vrClimbVelocity=Vector3.zero;
        }
        // Future hit/ragdoll systems call this before taking over. Climbing cannot mask knockback.
        public void ApplyTraversalImpulse(Vector3 velocityChange)
        {
            if(Climbing)ReleaseClimb("Grip broken by impact",true);
            slopeVelocity+=Vector3.ProjectOnPlane(velocityChange,Vector3.up);verticalSpeed+=velocityChange.y;
            climbStun=.4f;climbCooldown=Mathf.Max(climbCooldown,.85f);climbMustRelease=true;Grounded=false;support=null;jumped=true;lastGrounded=-100;
        }
        bool UpdateClimb(Vector2 input,bool held,float dt)
        {
            if(PowerupUnlimitedStamina)GripRemaining=1;
            climbCooldown=Mathf.Max(0,climbCooldown-dt);climbStun=Mathf.Max(0,climbStun-dt);
            if(!held){climbMustRelease=false;climbHold=0;}else climbHold+=dt;
            if(!Climbing)
            {
                // Rest on secure ground only. Dropping/regrabbing or changing blocks never refills grip.
                if(Grounded&&GroundAngle<=slopeGripAngle&&SupportAtRest())climbGroundRest+=dt;else climbGroundRest=0;
                if(climbGroundRest>Mathf.Max(0,climbRecoveryDelay))GripRemaining=CastleStaminaCurve.Advance(GripRemaining,Mathf.Max(0,climbRecoveryPerSecond)*dt,climbRecoveryCurve,climbRecoveryDegree);
                if(!climbingEnabled||((modeRules!=null&&!modeRules.canClimb)||GetComponent<DeadlyJenga.CandyPlayers.CandyEquipment>()?.BlocksTraversal==true)||!held||climbMustRelease||climbHold<.18f||climbCooldown>0||climbStun>0||Vaulting||GripRemaining<.12f)return false;
                var facing=Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized;
                if(!FindClimbFace(Feet,facing,out var initial))return false;
                BindClimb(initial);climbPointVelocity=PointVelocity();
                if(UnsafeClimb(climbPointVelocity,dt,true)){climbBlock=null;climbBody=null;climbCollider=null;return false;}
                Climbing=true;Grounded=false;support=null;verticalSpeed=0;slopeVelocity=Vector3.zero;vrClimbVelocity=Vector3.zero;climbElapsed=climbGait=0;climbMovingHand=-1;
                PlantClimbHand(0);PlantClimbHand(1);climbHandFrom[0]=climbHandLocal[0];climbHandFrom[1]=climbHandLocal[1];climbHandReach[0]=climbHandReach[1]=1;ActionStatus="Scramble";
            }
            if(!held||InputSuspended){ReleaseClimb("Released grip",false);return false;}
            climbPointVelocity=PointVelocity();
            if(UnsafeClimb(climbPointVelocity,dt,false)){ReleaseClimb("Block thrown / falling — grip broken",true);return false;}
            Vector3 carried=climbBlock.Anchor.TransformPoint(climbLocalFeet);
            Vector3 carry=carried-climbWorldFeet;
            if(carry.magnitude>climbBreakSpeed*dt+.06f){ReleaseClimb("Block moved abruptly — grip broken",true);return false;}
            Capsule.Move(carry);
            if(!Climbing)return false;
            var up=Vector3.ProjectOnPlane(Vector3.up,climbNormal).normalized;
            var right=Vector3.Cross(climbNormal,up).normalized;
            Vector3 along=(up*input.y+right*input.x)*climbSpeed;
            if(IsVR&&!externallyDriven)
            {vrClimbVelocity=Vector3.MoveTowards(vrClimbVelocity,along,vrClimbAcceleration*dt);along=vrClimbVelocity;}
            // Try the prospective position first, then the existing patch and nearby face angles.
            Vector3 prospective=Feet+along*dt;
            bool found=FindClimbFace(prospective,-climbNormal,out var face);
            if(!found)found=FindClimbFace(Feet,-climbNormal,out face);
            // Search a bounded hand-span ahead/behind through small course gaps.
            // Every continuation still needs a real, safely moving crate face.
            float seam=Mathf.Clamp(climbSeamReach,0,.45f)*playerScale;
            Vector3 search=along.sqrMagnitude>.001f?along.normalized*seam:up*seam;
            for(int i=0;!found&&i<6;i++)
            {
                float angle=(i%2==0?-1:1)*(i/2+1)*22.5f;
                found=FindClimbFace(prospective,Quaternion.AngleAxis(angle,Vector3.up)*-climbNormal,out face);
            }
            for(int i=0;!found&&i<2;i++)
            {
                Vector3 origin=prospective+search*(i==0?1:-1);
                found=FindClimbFace(origin,-climbNormal,out face);
                for(int j=0;!found&&j<2;j++)found=FindClimbFace(origin,Quaternion.AngleAxis(j==0?-45:45,Vector3.up)*-climbNormal,out face);
            }
            if(!found)
            {
                if(input.y>.1f&&ClimbMantle())return true;
                ReleaseClimb("No grip — falling",false);return false;
            }
            if(face.collider!=climbCollider)
            {
                BindClimb(face);climbPointVelocity=PointVelocity();
                if(UnsafeClimb(climbPointVelocity,dt,true)){ReleaseClimb("Unstable block — grip broken",true);return false;}
                PlantClimbHand(0);PlantClimbHand(1);
                climbHandFrom[0]=climbHandLocal[0];climbHandFrom[1]=climbHandLocal[1];climbHandReach[0]=climbHandReach[1]=1;
            }
            climbNormal=face.normal;
            float gap=Vector3.Dot(Feet+Vector3.up*(StandingHeight*.65f)-face.point,climbNormal);
            Vector3 adhesion=-climbNormal*Mathf.Clamp((gap-(Capsule.radius+.035f*playerScale))*12,-.6f,1.2f);
            var before=Feet;Capsule.Move((along+adhesion)*dt);
            if(!Climbing)return false; // Another moving block can break grip during the sweep.
            climbGait+=(Feet-before).magnitude/Mathf.Max(.1f,StandingHeight*.45f);
            int moving=(Mathf.FloorToInt(climbGait)%2);
            if(moving!=climbMovingHand){climbMovingHand=moving;PlantClimbHand(moving);}
            for(int i=0;i<2;i++)
            {
                Vector3 shoulder=Feet+Vector3.up*(1.255f*playerScale)+right*((i==0?-.21f:.21f)*playerScale)+climbNormal*(.05f*playerScale);
                Vector3 planted=climbBlock.Anchor.TransformPoint(climbHandLocal[i]);
                if(Vector3.Distance(shoulder,planted)>.48f*playerScale)PlantClimbHand(i);
                climbHandReach[i]=Mathf.Min(1,climbHandReach[i]+dt/.1f);
            }
            if(!PowerupUnlimitedStamina)GripRemaining=1-CastleStaminaCurve.Advance(1-GripRemaining,dt*Mathf.Max(0,climbDrainPerSecond)/Mathf.Max(.2f,climbGripSeconds)*(input.sqrMagnitude>.01f?1f:.85f),climbDrainCurve,climbDrainDegree);climbElapsed+=dt;
            climbLocalFeet=climbBlock.Anchor.InverseTransformPoint(Feet);climbWorldFeet=climbBlock.Anchor.TransformPoint(climbLocalFeet);
            climbLastVelocity=climbPointVelocity;climbLastRotation=climbBlock.Anchor.rotation;
            verticalSpeed=0;Grounded=false;ActionStatus="Scramble · grip "+Mathf.CeilToInt(GripRemaining*100)+"% · release to drop";
            if(GripRemaining<=0){ReleaseClimb("Grip exhausted — find secure footing",true);return false;}
            if(input.y>.1f&&ClimbMantle())return true;
            return true;
        }
        bool SupportAtRest()
        {
            if(!support)return true;
            var collider=support.GetComponent<Collider>();
            return !collider||!Climbables.TryGet(collider,out var block)||!block.Body||
                (block.Body.linearVelocity.magnitude<.7f&&block.Body.angularVelocity.magnitude<.4f);
        }
        bool ClimbMantle()
        {
            // Use wall-facing probes without turning the player's view (especially the HMD).
            if(!TryFindLedge(out var top,out var destination,-climbNormal))return false;
            Climbing=false;climbBlock=null;climbBody=null;climbCollider=null;climbCooldown=.3f;vrClimbVelocity=Vector3.zero;
            BeginVault(top,destination);return true;
        }
        void PlantClimbHand(int side)
        {
            if(!Climbables.Alive(climbBlock))return;
            Vector3 up=Vector3.ProjectOnPlane(Vector3.up,climbNormal).normalized,right=Vector3.Cross(climbNormal,up).normalized;
            climbHandFrom[side]=Vector3.Lerp(climbHandFrom[side],climbHandLocal[side],Mathf.SmoothStep(0,1,climbHandReach[side]));climbHandReach[side]=0;
            Vector3 origin=Feet+Vector3.up*(StandingHeight*.83f)+right*((side==0?-.18f:.18f)*playerScale)+up*(.06f*playerScale);
            if(ClimbFace(origin,-climbNormal,Capsule.radius+.6f*playerScale,out var hit)&&hit.collider==climbCollider)
                climbHandLocal[side]=climbBlock.Anchor.InverseTransformPoint(hit.point+hit.normal*(.024f*playerScale));
            else climbHandLocal[side]=climbBlock.Anchor.InverseTransformPoint(origin-climbNormal*(Capsule.radius+.02f));
        }
        public bool TryGetClimbGrip(bool left,out Pose grip)
        {
            grip=default;if(!Climbing||!Climbables.Alive(climbBlock))return false;
            Vector3 up=Vector3.ProjectOnPlane(Vector3.up,climbNormal).normalized;
            // Skeleton local +Z runs along fingers, local -Y is the palm side.
            int side=left?0:1;float t=climbHandReach[side];
            Vector3 point=Vector3.Lerp(climbHandFrom[side],climbHandLocal[side],Mathf.SmoothStep(0,1,t));
            grip=new Pose(climbBlock.Anchor.TransformPoint(point)+climbNormal*(Mathf.Sin(t*Mathf.PI)*.035f*playerScale),Quaternion.LookRotation(up,climbNormal));return true;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(!Climbing||!Climbables.TryGet(hit.collider,out var block)||block==climbBlock||!block.Body)return;
            var body=block.Body;var velocity=body.GetPointVelocity(hit.point);
            if(velocity.magnitude>3&&Vector3.Dot(velocity,hit.normal)>2)ApplyTraversalImpulse(Vector3.ClampMagnitude(velocity,10));
        }
    }
}

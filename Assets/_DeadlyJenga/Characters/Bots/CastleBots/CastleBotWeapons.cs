using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public static class BotGunPolicy
    {
        public static bool IsGun(EquipmentKind kind)
        {
            if(EquipmentSpecifications.Get(kind).power)return false;
            if(kind>EquipmentKind.PoweredArmor)return !EquipmentSpecifications.Get(kind).melee||kind==EquipmentKind.Chainsaw||kind==EquipmentKind.LaserAxe;
            return kind==EquipmentKind.Blaster||kind==EquipmentKind.DualRifle||kind==EquipmentKind.SheriffHammer||kind==EquipmentKind.PoweredArmor;
        }
        public static float Range(EquipmentKind kind,bool shotgun)=>kind==EquipmentKind.Chainsaw?(shotgun?28:1.6f):kind==EquipmentKind.LaserAxe?(shotgun?32:2.2f):kind==EquipmentKind.GrenadeLauncher?28:kind>EquipmentKind.PoweredArmor?EquipmentSpecifications.Get(kind).range:kind==EquipmentKind.DualRifle?(shotgun?12:60):kind==EquipmentKind.SheriffHammer?(shotgun?15:80):kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor?28:1.8f;
        public static bool Shotgun(EquipmentKind kind,bool current,float distance)=>kind==EquipmentKind.Chainsaw||kind==EquipmentKind.LaserAxe?(current?distance>3:distance>5):kind==EquipmentKind.DualRifle||kind==EquipmentKind.SheriffHammer?(current?distance<13:distance<9):false;
        public static float LeadTime(EquipmentKind kind,float distance)=>kind>EquipmentKind.PoweredArmor&&EquipmentSpecifications.Get(kind).projectileSpeed>0?Mathf.Min(1.5f,distance/EquipmentSpecifications.Get(kind).projectileSpeed):kind==EquipmentKind.Chainsaw&&distance>5?distance/22:kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor?Mathf.Min(1,distance/14):.06f;
        public static Vector3 AimDirection(EquipmentKind kind,Vector3 origin,Vector3 target)
        {
            var delta=target-origin;var spec=EquipmentSpecifications.Get(kind);if(spec.gravity<=0||spec.projectileSpeed<=0)return delta.normalized;
            var flat=Vector3.ProjectOnPlane(delta,Vector3.up);float distance=flat.magnitude;if(distance<.1f)return delta.normalized;
            float speed2=spec.projectileSpeed*spec.projectileSpeed;float discriminant=speed2*speed2-spec.gravity*(spec.gravity*distance*distance+2*delta.y*speed2);if(discriminant<=0)return delta.normalized;
            float angle=Mathf.Atan((speed2-Mathf.Sqrt(discriminant))/(spec.gravity*distance));return flat.normalized*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle);
        }
    }
    public sealed partial class CastleBotBrain
    {
        CandyEquipment botEquipment;
        GameObject gunTarget;long gunToken;float gunReady,nextGunSample,nextGunRequest,gunSeenAt,burstPause;int burstShots;
        Vector3 gunSeen,gunVelocity;bool gunVisible;
        readonly RaycastHit[] gunHits=new RaycastHit[32];
        readonly Collider[] shotgunBodies=new Collider[48];
        public int WeaponShotsAccepted{get;private set;}
        public int WeaponShotsAttempted{get;private set;}
        public int WeaponHolsters{get;private set;}
        public string WeaponReason{get;private set;}="No weapon";
        void PrepareBotWeapon()
        {
            botEquipment??=GetComponent<CandyEquipment>();if(!botEquipment)return;
            var state=botEquipment.Equipment;
            bool firingOpportunity=hasObjective&&selectedObjective.kind==BotObjectiveKind.Attack&&selectedObjective.subject==gunTarget&&gunVisible&&BotGunPolicy.IsGun(state.kind)&&Vector3.Distance(transform.position,selectedObjective.position)<BotGunPolicy.Range(state.kind,state.shotgun);
            firingOpportunity|=state.roleIssued&&state.kind==EquipmentKind.MurderSword&&hasObjective&&selectedObjective.kind==BotObjectiveKind.Attack&&selectedObjective.subject&&Vector3.Distance(transform.position,selectedObjective.position)<2.05f;
            bool travel=!hasObjective||selectedObjective.kind!=BotObjectiveKind.Attack||!selectedObjective.subject||Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting||!Motor.Grounded||PrecisionTraversal&&!firingOpportunity;
            if(hasObjective&&Mathf.Abs(selectedObjective.position.y-transform.position.y)>1&&Vector3.Distance(transform.position,selectedObjective.position)>BotGunPolicy.Range(state.kind,false))travel=true;
            if(travel&&state.drawn)HolsterBotWeapon(state,"Holster for travel, traversal or safety");
        }
        void HolsterBotWeapon(EquipmentState state,string reason)
        {
            WeaponReason=reason;if(state.kind==EquipmentKind.Dagger)return;
            if(state.drawn&&(MatchRuntime.Current as IBotEquipmentRuntime)?.SetBotWeapon(gameObject,false,state.shotgun)==true)WeaponHolsters++;
        }
        bool HandleWeaponCombat(ref CastleBotCommand command,out bool fighting)
        {
            fighting=false;botEquipment??=GetComponent<CandyEquipment>();var runtime=MatchRuntime.Current as IBotEquipmentRuntime;
            var state=botEquipment?botEquipment.Equipment:null;
            if(runtime==null||state==null||state.kind<EquipmentKind.Blaster)return false;
            if(!hasObjective||selectedObjective.kind!=BotObjectiveKind.Attack||!selectedObjective.subject){gunTarget=null;return false;}
            var target=selectedObjective.subject.GetComponent<IModeActor>();
            if(target==null||!target.Alive||MatchRuntime.Current.RulesFor(target.gameObject).downed||Motor.Team>=0&&target.Team==Motor.Team){HolsterBotWeapon(state,"Target unavailable");return true;}
            if(state.roleIssued&&state.kind==EquipmentKind.MurderSword)
            {
                var delta=target.transform.position+Vector3.up*.6f-Motor.ViewPose.position;
                bool striking=!Motor.CarryingFlag&&!Motor.Climbing&&!Motor.Vaulting&&Motor.Grounded&&BotCombatPolicy.InReach(Motor.ViewPose.position,target.transform.position+Vector3.up*.6f)&&!Physics.Raycast(Motor.ViewPose.position,delta.normalized,delta.magnitude,CombatMask|1,QueryTriggerInteraction.Ignore);
                if(striking&&!state.drawn)runtime.SetBotWeapon(gameObject,true,false);
                else if(!striking)HolsterBotWeapon(state,"Conceal sword until striking distance");
                return false;
            }
            if(Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting||!Motor.Grounded){HolsterBotWeapon(state,"Traversal takes priority");return true;}
            Vector3 contact=target.transform.position+Vector3.up*.6f;float distance=Vector3.Distance(Motor.ViewPose.position,contact);
            if(((state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)&&distance<3.8f)||EquipmentSpecifications.Get(state.kind).blastRadius>0&&distance<EquipmentSpecifications.Get(state.kind).blastRadius+1){HolsterBotWeapon(state,"Plasma splash too close; use melee");return false;}
            bool shotgun=BotGunPolicy.Shotgun(state.kind,state.shotgun,distance);
            if(distance>BotGunPolicy.Range(state.kind,shotgun)){HolsterBotWeapon(state,"Outside effective range; close distance");return true;}
            if(gunTarget!=target.gameObject||gunToken!=ObjectiveAssignment)
            {gunTarget=target.gameObject;gunToken=ObjectiveAssignment;gunSeen=contact;gunSeenAt=Time.time;gunVelocity=Vector3.zero;nextGunSample=0;gunReady=Time.time+Mathf.Lerp(.2f,.65f,Mathf.Repeat(Seed,.91f));burstShots=0;}
            if(Time.time>=nextGunSample)
            {
                float dt=Time.time-gunSeenAt;if(dt>.01f)gunVelocity=Vector3.ClampMagnitude((contact-gunSeen)/dt,8);
                gunSeen=contact;gunSeenAt=Time.time;nextGunSample=Time.time+.15f;
                var delta=contact-Motor.ViewPose.position;
                gunVisible=!Physics.Raycast(Motor.ViewPose.position,delta.normalized,delta.magnitude,CombatMask|1,QueryTriggerInteraction.Ignore);
            }
            if(!gunVisible){HolsterBotWeapon(state,"Cover blocks firing; seek a route");return true;}
            Vector3 predicted=contact+Vector3.ClampMagnitude(gunVelocity*BotGunPolicy.LeadTime(state.kind,distance),3);
            Vector3 aim=predicted-Motor.ViewPose.position;command.look=aim;
            // Stay on current safe footing to aim. The normal route resumes when the shot is unavailable.
            command.move=Vector3.zero;command.jump=false;command.climbHeld=false;fighting=true;
            if(!state.drawn||state.shotgun!=shotgun)
            {
                if(Time.time>=nextGunRequest){runtime.SetBotWeapon(gameObject,true,shotgun);nextGunRequest=Time.time+.25f;gunReady=Mathf.Max(gunReady,Time.time+.45f);}
                WeaponReason="Drawing or switching to suitable range";return true;
            }
            float error=(1-Personality.bravery*.35f)*.009f;
            var rotation=Quaternion.LookRotation(aim.normalized);
            rotation*=Quaternion.Euler(Mathf.Sin(Time.time*3.1f+Seed)*error*40,Mathf.Sin(Time.time*2.7f+Seed)*error*40,0);
            var grip=new Pose(Motor.ViewPose.position+rotation*new Vector3(state.leftHanded?-.22f:.22f,-.18f,.35f)*Motor.playerScale,rotation);
            grip.rotation=Quaternion.LookRotation(BotGunPolicy.AimDirection(state.kind,grip.position,predicted))*Quaternion.Euler(Mathf.Sin(Time.time*3.1f+Seed)*error*40,Mathf.Sin(Time.time*2.7f+Seed)*error*40,0);
            botEquipment.SetBotAim(grip);
            if(EquipmentSpecifications.Get(state.kind).gravity>0)for(int iteration=0;iteration<2;iteration++)
            {grip.rotation=Quaternion.LookRotation(BotGunPolicy.AimDirection(state.kind,botEquipment.MuzzlePose.position,predicted));botEquipment.SetBotAim(grip);}
            if(Time.time<gunReady||Time.time<burstPause||!BotCombatPolicy.Facing(Motor.ViewPose.rotation*Vector3.forward,aim)){WeaponReason="Reacting and settling aim";return true;}
            if(!ClearGunShot(target,botEquipment.MuzzlePose,shotgun&&(state.kind==EquipmentKind.DualRifle||state.kind==EquipmentKind.SheriffHammer),distance)){WeaponReason="Withhold fire: cover or another actor";gunReady=Time.time+.2f;return true;}
            WeaponShotsAttempted++;
            if(runtime.FireBotWeapon(gameObject,botEquipment.MuzzlePose))
            {
                WeaponShotsAccepted++;WeaponReason="Authoritative weapon shot accepted";burstShots++;
                float delay=state.kind>EquipmentKind.PoweredArmor?Mathf.Max(.08f,EquipmentSpecifications.Get(state.kind).cooldown):state.kind==EquipmentKind.DualRifle?(shotgun?.36f:.72f):state.kind==EquipmentKind.SheriffHammer?(shotgun?.34f:.82f):BotGunPolicy.IsGun(state.kind)?.52f:.98f;
                gunReady=Time.time+delay;
                if(burstShots>=2+(Mathf.FloorToInt(Seed)%3)){burstShots=0;burstPause=gunReady+Mathf.Lerp(.2f,.6f,Mathf.Repeat(Seed,.77f));}
            }
            else {WeaponReason="Authority withheld shot";gunReady=Time.time+.2f;}
            return true;
        }
        bool ClearGunShot(IModeActor target,Pose muzzle,bool shotgun,float distance)
        {
            var delta=target.transform.position+Vector3.up*.6f-muzzle.position;
            int count=Physics.RaycastNonAlloc(muzzle.position,delta.normalized,gunHits,delta.magnitude+.3f,CombatMask|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
            if(count==gunHits.Length)return false;
            float nearest=float.PositiveInfinity;IModeActor first=null;
            for(int i=0;i<count;i++){var actor=gunHits[i].collider.GetComponentInParent<IModeActor>();if(actor?.gameObject==gameObject||gunHits[i].collider.isTrigger&&actor==null)continue;if(gunHits[i].distance<nearest){nearest=gunHits[i].distance;first=actor;}}
            if(first==null||first.gameObject!=target.gameObject)return false;
            if(!shotgun)return true;
            int overlaps=Physics.OverlapCapsuleNonAlloc(muzzle.position,target.transform.position+Vector3.up*.6f,Mathf.Clamp(distance*.09f,.2f,1.3f),shotgunBodies,1<<Layers.Survivor,QueryTriggerInteraction.Collide);
            if(overlaps==shotgunBodies.Length)return false;
            for(int i=0;i<overlaps;i++){var actor=shotgunBodies[i].GetComponentInParent<IModeActor>();if(actor!=null&&actor.gameObject!=gameObject&&actor.gameObject!=target.gameObject&&actor.Alive)return false;}
            return true;
        }
    }
}

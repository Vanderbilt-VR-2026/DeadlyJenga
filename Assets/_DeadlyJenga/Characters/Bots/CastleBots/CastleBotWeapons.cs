using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
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
        public static float Range(EquipmentKind kind,bool shotgun)=>kind==EquipmentKind.ChainGun?11:
            kind==EquipmentKind.Chainsaw?(shotgun?28:1.6f):kind==EquipmentKind.LaserAxe?(shotgun?32:2.2f):kind==EquipmentKind.GrenadeLauncher?28:kind>EquipmentKind.PoweredArmor?EquipmentSpecifications.Get(kind).range:kind==EquipmentKind.DualRifle?(shotgun?12:60):kind==EquipmentKind.SheriffHammer?(shotgun?15:80):kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor?28:1.8f;
        public static bool Shotgun(EquipmentKind kind,bool current,float distance)
        {
            if(kind is EquipmentKind.Chainsaw or EquipmentKind.LaserAxe)
            {
                float reach=Range(kind,false);
                return distance>(current?reach-.25f:reach);
            }
            if(kind is EquipmentKind.DualRifle or EquipmentKind.SheriffHammer)
                return current?distance<=Mathf.Min(13,Range(kind,true)):distance<9;
            return kind==EquipmentKind.StockPistol&&(current?distance>11:distance>15);
        }
        public static bool CanUseAt(EquipmentKind kind,bool alternate,float distance)
        {
            var spec=EquipmentSpecifications.Get(kind);
            return distance<=Range(kind,alternate)&&
                (spec.blastRadius<=0||distance>=spec.blastRadius+1)&&
                (kind!=EquipmentKind.Blaster&&kind!=EquipmentKind.PoweredArmor||distance>=3.8f);
        }
        public static Vector2 AimOffset(float seed,int cycle,float settled,float skill,float lateralSpeed,int shots)
        {
            float phase=seed*.73f+cycle*2.73f;
            float radius=Mathf.Lerp(Mathf.Lerp(4.8f,2.0f,skill),Mathf.Lerp(1.14f,.45f,skill),settled);
            radius+=Mathf.Min(1.6f,lateralSpeed*.20f);
            return new Vector2(Mathf.Sin(phase*1.37f)*radius*.72f+Mathf.Min(2,shots)*.45f,
                Mathf.Cos(phase*1.61f)*radius);
        }
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
        Vector3 gunSeen,gunVelocity;
        Quaternion gunAim;
        float gunAimAt,gunAcquiredAt,lastWeaponGrounded = float.NegativeInfinity;
        int gunAimCycle;
        EquipmentKind gunKind;
        bool gunVisible,botTriggerActive;
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
            if (Motor.Grounded) lastWeaponGrounded = Time.time;
            if(state.kind==EquipmentKind.PoweredArmor)return;
            bool airborne = Time.time - lastWeaponGrounded > .25f;
            bool firingOpportunity=hasObjective&&selectedObjective.kind==BotObjectiveKind.Attack&&selectedObjective.subject==gunTarget&&gunVisible&&BotGunPolicy.IsGun(state.kind)&&BotGunPolicy.CanUseAt(state.kind,state.shotgun,Vector3.Distance(transform.position,gunSeen));
            firingOpportunity|=state.roleIssued&&state.kind==EquipmentKind.MurderSword&&hasObjective&&selectedObjective.kind==BotObjectiveKind.Attack&&selectedObjective.subject&&Vector3.Distance(transform.position,selectedObjective.position)<2.05f;
            bool travel=!hasObjective||selectedObjective.kind!=BotObjectiveKind.Attack||!selectedObjective.subject||Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting||airborne||PrecisionTraversal&&!firingOpportunity;
            if(hasObjective&&Mathf.Abs(selectedObjective.position.y-transform.position.y)>1&&Vector3.Distance(transform.position,selectedObjective.position)>BotGunPolicy.Range(state.kind,false))travel=true;
            if(travel)
            {
                if(!hasObjective||selectedObjective.kind!=BotObjectiveKind.Attack||!selectedObjective.subject||
                    Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting||airborne)
                {gunVisible=false;nextGunSample=0;}
                ReleaseBotTrigger(state);
                if(state.drawn)HolsterBotWeapon(state,"Holster for travel, traversal or safety");
            }
        }
        float suitStarted,suitRetry,suitProbeAt;
        bool suitFlying,suitLanding;
        Vector3 suitVelocity;

        bool TryBotSuitFlight(ref CastleBotCommand command)
        {
            var state=botEquipment?botEquipment.Equipment:null;
            var runtime=MatchRuntime.Current as IBotEquipmentRuntime;
            if(state?.kind!=EquipmentKind.PoweredArmor||runtime==null)
            {suitFlying=suitLanding=false;return false;}
            if(Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting)
            {suitFlying=suitLanding=false;HolsterBotWeapon(state,"Keep hands available for traversal");return false;}

            float now=Time.time;
            Vector3 feet=Motor.transform.position;
            Vector3 target=hasObjective?selectedObjective.position:tripDestination;
            if(hasObjective&&selectedObjective.subject&&selectedObjective.kind==BotObjectiveKind.Attack)
                target=selectedObjective.subject.transform.position;
            Vector3 flat=Vector3.ProjectOnPlane(target-feet,Vector3.up);
            bool attacking=hasObjective&&selectedObjective.kind==BotObjectiveKind.Attack&&selectedObjective.subject;
            bool trip=hasObjective||tripUntil>now&&hasGoal;
            if(!suitFlying)
            {
                if(now<suitRetry||!trip||(flat.sqrMagnitude<25&&target.y-feet.y<2))return false;
                if(!state.drawn&&!runtime.SetBotWeapon(gameObject,true,false)){suitRetry=now+.5f;return false;}
                suitStarted=now;suitProbeAt=0;suitFlying=true;suitLanding=false;
            }
            if(!state.drawn&&!runtime.SetBotWeapon(gameObject,true,false))
            {suitFlying=suitLanding=false;suitRetry=now+2;return false;}

            if(now>=suitProbeAt)
            {
                suitProbeAt=now+.2f;
                const int solids=(1<<Layers.Block)|(1<<Layers.Surface)|1;
                var center=feet+Vector3.up*Motor.Height*.5f;
                bool aboveClear=!Physics.SphereCast(center,Motor.Capsule.radius*.82f,Vector3.up,out _,1.35f,solids,QueryTriggerInteraction.Ignore);
                bool floor=Physics.Raycast(feet+Vector3.up*.2f,Vector3.down,out var landing,9,solids,QueryTriggerInteraction.Ignore)&&
                    landing.normal.y>.65f&&landing.point.y>(Services.World?.WaterHeight(landing.point)??float.NegativeInfinity)+.05f;
                if(!trip||now-suitStarted>12||!attacking&&flat.sqrMagnitude<2.25f&&Mathf.Abs(target.y-feet.y)<4)
                    suitLanding=true;
                if(suitLanding&&floor&&feet.y-landing.point.y<.8f)
                {
                    suitFlying=suitLanding=false;suitRetry=now+2;
                    HolsterBotWeapon(state,"Landing on supported ground");return false;
                }
                if(suitLanding&&now-suitStarted>17)
                {
                    suitFlying=suitLanding=false;suitRetry=now+3;
                    HolsterBotWeapon(state,"Return to the ground route");return false;
                }
                float range=flat.magnitude;
                Vector3 direction=flat.normalized;
                if(attacking&&!suitLanding&&range<5)direction=-direction;
                float horizontal=attacking&&range>=5&&range<13?1.15f:Mathf.Min(5.5f,range*1.7f);
                if(suitLanding&&floor)horizontal=0;
                bool blocked=horizontal>.1f&&Physics.SphereCast(center,Motor.Capsule.radius*.82f,direction,out _,1.9f,solids,QueryTriggerInteraction.Ignore);
                if(blocked)
                {
                    Vector3 side=Vector3.Cross(Vector3.up,direction);
                    if(Physics.SphereCast(center,Motor.Capsule.radius*.82f,side,out _,2,solids,QueryTriggerInteraction.Ignore))side=-side;
                    direction=Physics.SphereCast(center,Motor.Capsule.radius*.82f,side,out _,2,solids,QueryTriggerInteraction.Ignore)?Vector3.zero:side;
                    horizontal=2.7f;
                }
                float height=Mathf.Clamp((target.y+(attacking?3:2.2f)-feet.y)*1.7f,-2.25f,3);
                if(blocked&&aboveClear&&!suitLanding)height=Mathf.Max(height,2);
                if(suitLanding&&floor)height=-2.2f;
                if(height>0&&!aboveClear)height=0;
                suitVelocity=direction*horizontal+Vector3.up*height;
            }
            command.suitFlight=true;command.suitVelocity=suitVelocity;command.jump=command.climbHeld=false;
            return true;
        }

        void HolsterBotWeapon(EquipmentState state,string reason)
        {
            WeaponReason=reason;if(state.kind==EquipmentKind.Dagger)return;
            if(state.drawn&&(MatchRuntime.Current as IBotEquipmentRuntime)?.SetBotWeapon(gameObject,false,state.shotgun)==true)WeaponHolsters++;
        }
        void ReleaseBotTrigger(EquipmentState state)
        {
            if(!botTriggerActive)return;
            botTriggerActive=false;
            // A same-state bot request releases authority-held fire without a holster event.
            if(state.drawn)(MatchRuntime.Current as IBotEquipmentRuntime)?.SetBotWeapon(gameObject,true,state.shotgun);
        }
        bool HandleWeaponCombat(ref CastleBotCommand command,out bool fighting)
        {
            fighting=false;botEquipment??=GetComponent<CandyEquipment>();var runtime=MatchRuntime.Current as IBotEquipmentRuntime;
            var state=botEquipment?botEquipment.Equipment:null;
            if(runtime==null||state==null||state.kind<EquipmentKind.Blaster)return false;
            if(!hasObjective||selectedObjective.kind!=BotObjectiveKind.Attack||!selectedObjective.subject)
            {gunTarget=null;gunVisible=false;ReleaseBotTrigger(state);return false;}
            var target=selectedObjective.subject.GetComponent<IModeActor>();
            if(target==null||!target.Alive||MatchRuntime.Current.RulesFor(target.gameObject).downed||Motor.Team>=0&&target.Team==Motor.Team){gunVisible=false;ReleaseBotTrigger(state);HolsterBotWeapon(state,"Target unavailable");return true;}
            if(state.roleIssued&&state.kind==EquipmentKind.MurderSword)
            {
                var delta=target.transform.position+Vector3.up*.6f-Motor.ViewPose.position;
                bool striking=!Motor.CarryingFlag&&!Motor.Climbing&&!Motor.Vaulting&&Motor.Grounded&&BotCombatPolicy.InReach(Motor.ViewPose.position,target.transform.position+Vector3.up*.6f)&&!Physics.Raycast(Motor.ViewPose.position,delta.normalized,delta.magnitude,CombatMask|1,QueryTriggerInteraction.Ignore);
                if(striking&&!state.drawn)runtime.SetBotWeapon(gameObject,true,false);
                else if(!striking)HolsterBotWeapon(state,"Conceal sword until striking distance");
                return false;
            }
            if(Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting||!command.suitFlight&&Time.time-lastWeaponGrounded>.25f)
            {gunVisible=false;ReleaseBotTrigger(state);HolsterBotWeapon(state,"Traversal takes priority");return true;}

            float now=Time.time;
            var eye=Motor.ViewPose;
            float skill=Mathf.Lerp(.20f,.83f,Mathf.InverseLerp(.9f,1.1f,variation));
            if(gunTarget!=target.gameObject||gunToken!=ObjectiveAssignment||gunKind!=state.kind)
            {
                ReleaseBotTrigger(state);
                gunTarget=target.gameObject;gunToken=ObjectiveAssignment;gunKind=state.kind;
                gunVisible=false;gunVelocity=Vector3.zero;nextGunSample=0;
                gunAim=eye.rotation;gunAimAt=now;burstShots=0;burstPause=0;
            }
            if(now>=nextGunSample)
            {
                var feet=target.transform.position;
                float torsoHeight=Mathf.Clamp((target.ViewPose.position.y-feet.y)*.58f,.25f,1.15f);
                var sample = feet + Vector3.up * torsoHeight;
                if (target is CastleSurvivor survivor && survivor.Capsule)
                {
                    var body = survivor.Capsule.bounds;
                    sample = body.center;
                    // The wide axe beam otherwise clips the floor before reaching a short target.
                    if (state.kind == EquipmentKind.LaserAxe) sample.y += body.extents.y * .55f;
                }
                var delta=sample-eye.position;
                bool inView=delta.sqrMagnitude<16||Vector3.Dot(eye.rotation*Vector3.forward,delta.normalized)>-.15f;
                bool visible=inView&&!Physics.Raycast(eye.position,delta.normalized,delta.magnitude,CombatMask|1,QueryTriggerInteraction.Ignore);
                if(visible)
                {
                    float dt=now-gunSeenAt;
                    if(!gunVisible||dt>.3f)
                    {
                        gunVelocity=Vector3.zero;gunAcquiredAt=now;gunAimCycle++;burstShots=0;
                        gunReady=Mathf.Max(gunReady,now+Mathf.Lerp(.60f,.24f,skill));
                    }
                    else if(dt>.01f)
                        gunVelocity=Vector3.Lerp(gunVelocity,Vector3.ClampMagnitude((sample-gunSeen)/dt,8),.55f);
                    gunSeen=sample;gunSeenAt=now;
                }
                gunVisible=visible;
                nextGunSample=now+Mathf.Lerp(.13f,.19f,skill);
            }
            if(!gunVisible||now-gunSeenAt>.30f)
            {WeaponReason="Lost sight; seek a route";ReleaseBotTrigger(state);return true;}

            float distance=Vector3.Distance(eye.position,gunSeen);
            bool alternate=BotGunPolicy.Shotgun(state.kind,state.shotgun,distance);
            if(!BotGunPolicy.CanUseAt(state.kind,alternate,distance))
            {
                ReleaseBotTrigger(state);
                bool tooClose=distance<BotGunPolicy.Range(state.kind,alternate);
                if (tooClose && state.drawn && !command.suitFlight) HolsterBotWeapon(state, "Unsafe splash distance; switch to melee");
                WeaponReason=tooClose?"Unsafe splash distance; use melee":"Outside effective range; close distance";
                return !tooClose;
            }

            var spec=EquipmentSpecifications.Get(state.kind);
            float lead=BotGunPolicy.LeadTime(state.kind,distance)*Mathf.Lerp(.55f,.94f,skill);
            if(state.kind==EquipmentKind.Chainsaw&&alternate)lead=distance/22*Mathf.Lerp(.55f,.94f,skill);
            var predicted=gunSeen+Vector3.ClampMagnitude(gunVelocity*(lead+Mathf.Min(.09f,now-gunSeenAt)),3);
            var aim=predicted-eye.position;
            if(aim.sqrMagnitude<.0001f){ReleaseBotTrigger(state);return false;}
            command.look=aim;
            if(state.reloadRemaining>0)
            {WeaponReason="Reloading; keep moving";ReleaseBotTrigger(state);return true;}

            command.move=Vector3.zero;command.jump=false;command.climbHeld=false;fighting=true;
            if(!state.drawn||state.shotgun!=alternate)
            {
                ReleaseBotTrigger(state);
                if(now>=nextGunRequest)
                {
                    runtime.SetBotWeapon(gameObject,true,alternate);nextGunRequest=now+.25f;
                    gunReady=Mathf.Max(gunReady,now+.45f);
                }
                WeaponReason="Drawing or switching to suitable range";return true;
            }

            float settled=Mathf.Clamp01((now-gunAcquiredAt)/.85f);
            float lateral=Vector3.ProjectOnPlane(gunVelocity,aim.normalized).magnitude;
            var error=BotGunPolicy.AimOffset(Seed,gunAimCycle,settled,skill,lateral,burstShots);
            bool precision = !alternate && (state.kind == EquipmentKind.SheriffHammer || state.kind == EquipmentKind.DualRifle);
            bool directFire = BotGunPolicy.IsGun(state.kind) && spec.gravity <= 0 && spec.blastRadius <= 0 &&
                state.kind != EquipmentKind.Blaster && state.kind != EquipmentKind.PoweredArmor;
            if (directFire)
            {
                float radius = target is CastleSurvivor survivor && survivor.Capsule
                    ? survivor.Capsule.bounds.extents.x : .2f;
                float angularRadius = Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
                float maxError = angularRadius * Mathf.Lerp(.65f, 1.45f, Mathf.Clamp01(lateral / 4));
                error = Vector2.ClampMagnitude(error, maxError);
            }
            var gripPosition=eye.position+gunAim*new Vector3(state.leftHanded?-.22f:.22f,-.18f,.35f)*Motor.playerScale;
            botEquipment.SetBotAim(new Pose(gripPosition,gunAim));
            var desired=Quaternion.LookRotation(BotGunPolicy.AimDirection(state.kind,botEquipment.MuzzlePose.position,predicted))*
                Quaternion.Euler(-error.x,error.y,0);
            float dtAim=Mathf.Clamp(now-gunAimAt,0,.075f);
            gunAim=Quaternion.RotateTowards(gunAim,desired,Mathf.Lerp(105,225,skill)*dtAim);
            gunAimAt=now;
            botEquipment.SetBotAim(new Pose(gripPosition,gunAim));

            if(now<burstPause||Quaternion.Angle(gunAim,desired)>(precision?.45f:directFire?.8f:5f)||!BotCombatPolicy.Facing(eye.rotation*Vector3.forward,aim))
            {WeaponReason="Reacting and settling aim";ReleaseBotTrigger(state);return true;}
            if(now<gunReady){WeaponReason="Reaction or weapon recovery";return true;}
            var muzzle=botEquipment.MuzzlePose;
            var checkedMuzzle=muzzle;
            if(state.kind==EquipmentKind.DualUzi&&((spec.magazine-state.rounds)&1)!=0)
            {
                // The authority transfers every second spiker shot to the other grip.
                var primary=Motor.HandPose(state.leftHanded);
                var secondary=Motor.HandPose(!state.leftHanded);
                var local=Quaternion.Inverse(primary.rotation)*(muzzle.position-primary.position);
                checkedMuzzle=new Pose(secondary.position+secondary.rotation*local,secondary.rotation);
            }
            if(!ClearGunShot(target,checkedMuzzle,gunSeen,predicted,state,alternate,distance))
            {WeaponReason="Withhold fire: cover or another actor";ReleaseBotTrigger(state);gunReady=now+.2f;return true;}

            WeaponShotsAttempted++;
            botTriggerActive=state.kind>EquipmentKind.PoweredArmor&&(spec.automatic||spec.charge>0);
            if(runtime.FireBotWeapon(gameObject,muzzle))
            {
                WeaponShotsAccepted++;WeaponReason="Authoritative weapon shot accepted";burstShots++;
                float delay=state.kind>EquipmentKind.PoweredArmor?Mathf.Max(.08f,spec.cooldown):
                    state.kind==EquipmentKind.DualRifle?(alternate?.36f:.72f):
                    state.kind==EquipmentKind.SheriffHammer?(alternate?.34f:.82f):BotGunPolicy.IsGun(state.kind)?.52f:.98f;
                gunReady=now+delay;
                int burstLimit=state.kind==EquipmentKind.StockPistol&&alternate?3:2+(Mathf.FloorToInt(Seed)%3);
                if(burstShots>=burstLimit)
                {
                    burstShots=0;gunAimCycle++;burstPause=gunReady+Mathf.Lerp(.6f,.22f,skill);
                    ReleaseBotTrigger(state);
                }
            }
            else {WeaponReason="Charging or authority withheld shot";gunReady=now+.12f;}
            return true;
        }
        bool ClearGunShot(IModeActor target,Pose muzzle,Vector3 observed,Vector3 predicted,
            EquipmentState state,bool alternate,float distance)
        {
            if(!ShotLaneClear(Motor.ViewPose.position,observed,target,out _))return false;
            var spec=EquipmentSpecifications.Get(state.kind);
            var forward=muzzle.rotation*Vector3.forward;
            var end=muzzle.position+forward*Vector3.Distance(muzzle.position,predicted);
            if(spec.gravity>0)
            {
                float flatSpeed=Vector3.ProjectOnPlane(forward,Vector3.up).magnitude*spec.projectileSpeed;
                float flight=Mathf.Clamp(Vector3.ProjectOnPlane(predicted-muzzle.position,Vector3.up).magnitude/
                    Mathf.Max(1,flatSpeed),.05f,2.4f);
                var start=muzzle.position;
                for(int i=1;i<=4;i++)
                {
                    float t=flight*i/4;
                    end=muzzle.position+forward*(spec.projectileSpeed*t)+Vector3.down*(.5f*spec.gravity*t*t);
                    if(!ShotLaneClear(start,end,target,out bool hit))return false;
                    if(hit){end=observed;break;}
                    start=end;
                }
            }
            else
            {
                if(!ShotLaneClear(muzzle.position,end,target,out bool hit))return false;
                if(hit)end=observed;
            }

            bool shotgun=alternate&&(state.kind==EquipmentKind.DualRifle||state.kind==EquipmentKind.SheriffHammer);
            if(!shotgun&&spec.blastRadius<=0)return true;
            if(spec.blastRadius>0&&(end-transform.position).sqrMagnitude<spec.blastRadius*spec.blastRadius)return false;
            int overlaps=shotgun?
                Physics.OverlapCapsuleNonAlloc(muzzle.position,end,Mathf.Clamp(distance*.09f,.2f,1.3f),shotgunBodies,1<<Layers.Survivor,QueryTriggerInteraction.Collide):
                Physics.OverlapSphereNonAlloc(end,spec.blastRadius,shotgunBodies,1<<Layers.Survivor,QueryTriggerInteraction.Collide);
            if(overlaps==shotgunBodies.Length)return false;
            for(int i=0;i<overlaps;i++)
            {
                var actor=shotgunBodies[i].GetComponentInParent<IModeActor>();
                if(actor!=null&&actor.gameObject!=gameObject&&actor.gameObject!=target.gameObject&&actor.Alive&&
                    (shotgun||Motor.Team>=0&&actor.Team==Motor.Team))return false;
            }
            return true;
        }

        bool ShotLaneClear(Vector3 start,Vector3 end,IModeActor target,out bool hitTarget)
        {
            hitTarget=false;
            var delta=end-start;
            int count=Physics.RaycastNonAlloc(start,delta.normalized,gunHits,delta.magnitude,
                CombatMask|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);
            if(count==gunHits.Length)return false;
            float nearest=float.PositiveInfinity;IModeActor first=null;
            for(int i=0;i<count;i++)
            {
                var hit=gunHits[i];
                var actor=hit.collider.GetComponentInParent<IModeActor>();
                if(actor?.gameObject==gameObject||hit.collider.isTrigger&&actor==null||hit.distance>=nearest)continue;
                nearest=hit.distance;first=actor;
            }
            hitTarget=first?.gameObject==target.gameObject;
            // An empty lane is a legal shot, including a genuine miss.
            return hitTarget||float.IsPositiveInfinity(nearest);
        }
    }
}

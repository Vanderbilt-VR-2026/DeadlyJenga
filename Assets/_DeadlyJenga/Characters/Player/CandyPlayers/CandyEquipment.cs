using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyPlayers
{
    [DefaultExecutionOrder(190)]
    public sealed partial class CandyEquipment:MonoBehaviour,IEquipmentControls,IWeaponAim
    {
        CastleSurvivor motor;CandyPlayerAvatar avatar;CandyWeaponVisual visual;
        InputAction trigger,offTrigger,grip;
        bool left,armed,sheriffShotgun,primaryJumpReserved;float nextHandSync;float nextFire,lastAttackTime=float.NegativeInfinity;EquipmentKind lastPower;
        EquipmentKind lastAttackKind;
        Vector3 lastHand;bool handReady;float nextSwing;
        Vector3 desktopAimTarget;
        float aimBlend;int aimBlendFrame=-1;EquipmentKind aimBlendKind;bool aimBlendLeft;
        readonly RaycastHit[] aimHits=new RaycastHit[32];
        EffectType speedEffect,jumpEffect;
        Pose botAim;float botAimAt=float.NegativeInfinity;
        public void SetBotAim(Pose pose){if(motor&&motor.externallyDriven){botAim=pose;botAimAt=Time.time;}}
        internal bool ReservesPrimaryJump(bool jumpLeft)
        {
            if(!HybridXRInput.PrimaryHeld(jumpLeft)){primaryJumpReserved=false;return false;}
            if(HybridXRInput.SingleController&&HybridXRInput.SingleControllerLeft==jumpLeft&&HybridXRInput.GripHeld(jumpLeft))
            {
                var state=Equipment;
                if(state.drawn&&EquipmentSpecifications.Get(state.kind).modeSwitch)primaryJumpReserved=true;
            }
            return primaryJumpReserved;
        }
        IEquipmentRuntime Runtime=>MatchRuntime.Current as IEquipmentRuntime;
        public EquipmentState Equipment
        {
            get
            {
                var state=Runtime?.EquipmentFor(motor?motor.ActorId:null)??new EquipmentState();
                if(state.kind==EquipmentKind.None&&motor&&!motor.externallyDriven&&MatchRuntime.Current is IMysteryControls mystery&&mystery.Active)
                {
                    var own=mystery.Personal;
                    state=new EquipmentState{actor=motor.ActorId,kind=own.role==MysteryRole.Murderer?EquipmentKind.MurderSword:own.role==MysteryRole.Sheriff?EquipmentKind.SheriffHammer:EquipmentKind.None,roleIssued=true,drawn=own.specialAttack,leftHanded=left,shotgun=own.role==MysteryRole.Sheriff&&sheriffShotgun};
                }
                if(Local)state.leftHanded=left;
                if(state.kind==EquipmentKind.SheriffHammer)sheriffShotgun=state.shotgun;
                return state;
            }
        }
        public bool BlocksTraversal=>false;
        bool UsesSupportGrip=>EquipmentSpecifications.Get(Equipment.kind).twoHanded&&Equipment.drawn;
        bool SlingForEmote(EquipmentState state) =>
            state.drawn && motor && motor.EmoteActive && EquipmentSpecifications.Get(state.kind).twoHanded;
        public bool UsesTriggers=>Equipment.kind>=EquipmentKind.Blaster;
        float Scale=>motor?Mathf.Max(.1f,motor.playerScale):1;
        bool Local=>motor&&!motor.externallyDriven&&!avatar.remoteDriven;
        void Awake(){motor=GetComponent<CastleSurvivor>();avatar=GetComponent<CandyPlayerAvatar>();left=PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0)!=0;if(Local)Bind();}
        void Bind()
        {
            trigger?.Dispose();offTrigger?.Dispose();grip?.Dispose();string dominant=left?"LeftHand":"RightHand",other=left?"RightHand":"LeftHand";
            trigger=new InputAction("Weapon fire / draw",InputActionType.Button,"<XRController>{"+dominant+"}/trigger");
            offTrigger=new InputAction("Weapon mode / jump",InputActionType.Button,"<XRController>{"+other+"}/trigger");
            grip=new InputAction("Rifle support",InputActionType.Button,"<XRController>{"+other+"}/grip");
            if(isActiveAndEnabled){trigger.Enable();offTrigger.Enable();grip.Enable();}armed=false;
        }
        void OnEnable(){trigger?.Enable();offTrigger?.Enable();grip?.Enable();}
        void OnDisable(){aimBlend=0;aimBlendFrame=-1;ResetBashTracking();CancelBashPresentation();ClearPickupFocus();nextPickupRequest=0;primaryJumpReserved=false;if(Local){ReleaseHeldArsenal();SetSuitThrust(false);motor.ClearHoverFlight();suitWasFlying=false;}trigger?.Disable();offTrigger?.Disable();grip?.Disable();visual?.Hide();}
        void OnDestroy(){trigger?.Dispose();offTrigger?.Dispose();grip?.Dispose();}
        public void SetEquipped(bool value)
        {
            var state=Equipment;CancelBashPresentation();if(!value){motor?.ClearHoverFlight();suitWasFlying=false;}
            if(state.roleIssued&&MatchRuntime.Current is IMysteryControls mystery)mystery.SetSpecialAttack(value);
            Runtime?.RequestEquipment(value,left,state.shotgun);offhandReady=false;armed=false;motor?.ResetVRJump();
        }
        public void SetWeaponHand(bool value)
        {
            PlayerPrefs.SetInt("CandyBlaster.LeftHanded",value?1:0);PlayerPrefs.Save();ApplyWeaponHand(motor&&motor.IsVR&&HybridXRInput.SingleController?HybridXRInput.SingleControllerLeft:value);
        }
        void ApplyWeaponHand(bool value)
        {
            if(left!=value)
            {
                ReleaseHeldArsenal();ResetBashTracking();CancelBashPresentation();ClearPickupFocus();
                handReady=offhandReady=false;primaryJumpReserved=false;
                left=value;Bind();motor?.ResetVRJump();nextHandSync=0;
            }
            var state=Runtime?.EquipmentFor(motor?motor.ActorId:null);
            if(state==null||state.kind==EquipmentKind.None||state.leftHanded==left||Time.unscaledTime<nextHandSync)return;
            nextHandSync=Time.unscaledTime+.25f;
            Runtime.RequestEquipment(state.drawn,left,state.shotgun);
        }
        public void ToggleRifleMode(){var state=Equipment;if(EquipmentSpecifications.Get(state.kind).modeSwitch){if(Runtime?.RequestEquipment(state.drawn,left,!state.shotgun)==true&&state.kind==EquipmentKind.SheriffHammer)sheriffShotgun=!state.shotgun;}}
        public bool JumpHeld=>UsesTriggers&&Equipment.kind!=EquipmentKind.PoweredArmor&&!(EquipmentSpecifications.Get(Equipment.kind).modeSwitch&&Equipment.drawn)&&!BlocksTraversal&&offTrigger!=null&&offTrigger.IsPressed();
        // Model-space sight lines. Large weapons stay shouldered beside the eye.
        public static bool TryAimGripOffset(EquipmentState state,bool leftHanded,bool vr,out Vector3 offset)
        {
            offset=Vector3.zero;
            if(state==null||state.kind==EquipmentKind.None)return false;
            var spec=EquipmentSpecifications.Get(state.kind);
            if(spec.power||spec.melee&&!(state.shotgun&&
                (state.kind==EquipmentKind.Chainsaw||state.kind==EquipmentKind.LaserAxe)))return false;
            offset=state.kind switch
            {
                EquipmentKind.DualRifle=>new Vector3(0,-.28f,vr?-.20f:-.07f),
                EquipmentKind.Blaster=>new Vector3(0,-.24f,vr?.03f:.12f),
                EquipmentKind.SheriffHammer=>new Vector3(0,-.23f,vr?-.02f:.12f),
                EquipmentKind.RocketLauncher=>new Vector3(.33f,-.51f,.66f),
                EquipmentKind.ChainGun=>new Vector3(.26f,-.48f,.36f),
                EquipmentKind.GrenadeLauncher=>new Vector3(.29f,-.52f,.61f),
                EquipmentKind.NeedleDrill=>new Vector3(0,-.31f,.12f),
                EquipmentKind.StockPistol=>new Vector3(0,-.218f,.12f),
                EquipmentKind.WristLaser=>new Vector3(0,-.17f,.35f),
                EquipmentKind.Chainsaw=>new Vector3(0,-.25f,.10f),
                EquipmentKind.LaserAxe=>new Vector3(0,-.32f,.10f),
                EquipmentKind.PoweredArmor=>new Vector3(0,-.12f,.40f),
                _=>new Vector3(0,-.24f,.12f)
            };
            if(leftHanded)offset.x=-offset.x;
            return true;
        }
        Pose RaiseForAim(Pose tracked,Transform head)
        {
            var state=Equipment;
            bool supported=TryAimGripOffset(state,left,true,out var offset);
            bool available=Local&&state.drawn&&supported&&motor.Alive&&motor.HasInputFocus&&!motor.InputSuspended&&
                !motor.Climbing&&!motor.Vaulting&&Services.Session?.MenuOpen!=true&&!SlingForEmote(state);
            if(!available||aimBlendKind!=state.kind||aimBlendLeft!=left)aimBlend=0;
            aimBlendKind=state.kind;aimBlendLeft=left;
            bool requested=available&&HybridXRInput.GripHeld(left);
            if(Time.frameCount!=aimBlendFrame)
            {
                aimBlendFrame=Time.frameCount;
                aimBlend=Mathf.MoveTowards(aimBlend,requested?1:0,Time.unscaledDeltaTime/.16f);
            }
            if(aimBlend<=0)return tracked;
            var raised=new Pose(head.position+head.rotation*offset*Scale,head.rotation);
            if(visual!=null&&visual.TryOpticAnchor(state,out var anchor))
            {
                raised.rotation=head.rotation*Quaternion.Inverse(anchor.rotation);
                raised.position=head.position+head.forward*.18f-raised.rotation*anchor.position*Scale;
            }
            float blend=Mathf.SmoothStep(0,1,aimBlend);
            return new Pose(Vector3.Lerp(tracked.position,raised.position,blend),Quaternion.Slerp(tracked.rotation,raised.rotation,blend));
        }
        public Pose GripPose
        {
            get
            {
                var head=avatar&&avatar.viewCamera?avatar.viewCamera.transform:transform;
                if(motor&&motor.externallyDriven&&Time.time-botAimAt<.5f)return botAim;
                Pose aim;
                if(motor&&motor.IsVR&&avatar.tracking&&avatar.tracking.WorldHand(left).tracked)
                {
                    var primary=avatar.tracking.WorldHand(left);aim=new Pose(primary.position,primary.rotation);
                    if(avatar.tracking.TryControllerAim(left,out var controllerAim))aim=controllerAim;
                    if(UsesSupportGrip&&(grip.IsPressed()||avatar.tracking.IsHybrid&&(left?avatar.tracking.RightPinching:avatar.tracking.LeftPinching)))
                    {
                        var secondary=avatar.tracking.WorldHand(!left);var delta=secondary.position-primary.position;
                        if(secondary.tracked&&delta.magnitude>.12f*Scale&&delta.magnitude<1.1f*Scale)aim.rotation=Quaternion.LookRotation(delta,primary.rotation*Vector3.up);
                    }
                    // Dominant grip is explicit aim; releasing it restores the tracked hip pose.
                    if(Equipment.kind!=EquipmentKind.LaserAxe)aim=RaiseForAim(aim,head);
                }
                else
                {
                    if(motor&&motor.IsVR){aimBlend=0;aimBlendFrame=-1;}
                    var carryOffset=new Vector3(left?-.25f:.25f,-.22f,.36f);
                    var carried=Equipment;
                    if(carried.drawn&&(carried.kind==EquipmentKind.ChainGun||IsShoulderLauncher(carried.kind)))
                        TryAimGripOffset(carried,left,false,out carryOffset);
                    var position=head.position+head.rotation*carryOffset*Scale;
                    if(Local&&Equipment.drawn&&Mouse.current?.rightButton.isPressed==true&&
                        TryAimGripOffset(Equipment,left,false,out var offset))
                        position=head.position+head.rotation*offset*Scale;
                    Vector3 target=head.position+head.forward*120;
                    int count=Physics.RaycastNonAlloc(head.position,head.forward,aimHits,120,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);float nearest=120;
                    for(int i=0;i<count;i++){var hit=aimHits[i];if(!hit.collider.transform.IsChildOf(transform)&&(!hit.collider.isTrigger||hit.collider.GetComponentInParent<IModeActor>()!=null)&&hit.distance<nearest){nearest=hit.distance;target=hit.point;}}
                    desktopAimTarget=target;
                    aim=new Pose(position,Quaternion.LookRotation(target-position));
                }
                if(motor&&!motor.IsVR&&Equipment.drawn)
                {
                    float age=Time.time-lastAttackTime;
                    bool melee=EquipmentSpecifications.Get(Equipment.kind).melee;
                    if(!melee&&age>=0&&age<.3f)aim.position-=aim.rotation*Vector3.forward*(Mathf.Sin(age/.3f*Mathf.PI)*.035f*Scale);
                }
                var state=Equipment;
                if(state.kind>EquipmentKind.PoweredArmor&&state.triggerHeld)
                    aim.position-=aim.rotation*Vector3.forward*(.012f*Mathf.Abs(Mathf.Sin(Time.time*55))*Scale);
                if(Local&&state.drawn&&IsShoulderLauncher(state.kind))
                    aim=ClearLauncherView(aim,head.position,head.rotation,Scale,
                        avatar.viewCamera?avatar.viewCamera.nearClipPlane:.035f,state.kind);
                return aim;
            }
        }
        static bool IsShoulderLauncher(EquipmentKind kind)=>
            kind==EquipmentKind.RocketLauncher||kind==EquipmentKind.GrenadeLauncher;

        // Y and Z locate each bulky section. W is its conservative profile radius.
        static readonly Vector4[] rocketClearance=
        {
            new Vector4(0,.205f,-.345f,.132f),new Vector4(0,.16f,.11f,.155f),
            new Vector4(0,.205f,.56f,.113f),new Vector4(0,.205f,.96f,.096f)
        };
        static readonly Vector4[] grenadeClearance=
        {
            new Vector4(0,.092f,-.325f,.085f),new Vector4(0,.12f,.11f+CandyWeaponVisual.GrenadeDrumOffset,.169f),
            new Vector4(0,.235f,.48f,.092f),new Vector4(0,.235f,.74f,.088f)
        };

        internal static Pose ClearLauncherView(Pose grip,Vector3 eye,Quaternion view,float scale,float nearClip,EquipmentKind kind)
        {
            var toView=new Quaternion(-view.x,-view.y,-view.z,view.w);
            float shift=0;
            foreach(var section in kind==EquipmentKind.RocketLauncher?rocketClearance:grenadeClearance)
            {
                var point=grip.position+grip.rotation*(new Vector3(section.x,section.y,section.z)*scale);
                var local=toView*(point-eye);
                float radius=section.w*scale;
                if(Mathf.Abs(local.x)>.29f*scale+radius||local.y>.24f*scale+radius||
                    local.y<-.72f*scale-radius||local.z<-.39f*scale-radius)continue;
                float safeDepth=Mathf.Max(nearClip+.055f,.17f*scale)+radius;
                shift=Mathf.Max(shift,safeDepth-local.z);
            }
            grip.position+=view*Vector3.forward*Mathf.Min(shift,.68f*scale);
            return grip;
        }

        public Pose MuzzlePose
        {
            get
            {
                var state=Equipment;if(GetComponent<IEnemyActor>()?.ArchetypeId=="mushroom"){var aim=motor.externallyDriven&&Time.time-botAimAt<.5f?botAim.rotation:avatar.viewCamera.transform.rotation;return new Pose(transform.position+Vector3.up*motor.Height*.85f,aim);}if(state.kind==EquipmentKind.PoweredArmor&&!motor.externallyDriven)return SuitMuzzle;
                var pose=GripPose;
                if(state.kind==EquipmentKind.LaserAxe&&state.shotgun)
                {
                    var carry=AxeCarry(pose,state.leftHanded);
                    pose.position=carry.position+carry.rotation*Vector3.forward*(.986f*Scale);
                    if(!motor.externallyDriven&&!motor.IsVR&&(desktopAimTarget-pose.position).sqrMagnitude>.001f)
                        pose.rotation=Quaternion.LookRotation(desktopAimTarget-pose.position);
                    else if(motor.IsVR&&avatar.tracking&&avatar.tracking.TryControllerAim(state.leftHanded,out var aim))
                        pose.rotation=aim.rotation;
                    return pose;
                }
                if(state.kind>EquipmentKind.PoweredArmor)
                {
                    var localMuzzle=state.kind switch
                    {
                        EquipmentKind.RocketLauncher=>CandyWeaponVisual.RocketMuzzle,
                        EquipmentKind.GrenadeLauncher=>CandyWeaponVisual.GrenadeMuzzle,
                        EquipmentKind.ChainGun=>new Vector3(0,CandyWeaponVisual.ChainGunBarrelHeight,CandyWeaponVisual.ChainGunMuzzleDepth),
                        EquipmentKind.Chainsaw when state.shotgun=>new Vector3(0,0,.78f),
                        EquipmentKind.NeedleDrill=>new Vector3(0,.12f,.72f),
                        EquipmentKind.StockPistol or EquipmentKind.DualUzi=>new Vector3(0,.12f,.51f),
                        EquipmentKind.WristLaser=>new Vector3(0,.12f,.17f),
                        _=>new Vector3(0,EquipmentSpecifications.Get(state.kind).melee?0:.12f,.10f)
                    };
                    pose.position+=pose.rotation*localMuzzle*Scale;
                    if(!motor.externallyDriven&&!motor.IsVR&&(desktopAimTarget-pose.position).sqrMagnitude>.001f)pose.rotation=Quaternion.LookRotation(desktopAimTarget-pose.position);
                    return pose;
                }
                pose.position+=pose.rotation*new Vector3(0,state.kind==EquipmentKind.Blaster?.125f:state.kind==EquipmentKind.DualRifle?.15f:state.kind==EquipmentKind.SheriffHammer?.12f:.035f,state.kind==EquipmentKind.DualRifle?(state.shotgun?.73f:1.0f):state.kind==EquipmentKind.SheriffHammer?.53f:state.kind==EquipmentKind.Blaster?.57f:.1f)*Scale;
                if(motor&&!motor.externallyDriven&&!motor.IsVR&&(state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.DualRifle||state.kind==EquipmentKind.SheriffHammer)&&(desktopAimTarget-pose.position).sqrMagnitude>.001f)
                    pose.rotation=Quaternion.LookRotation(desktopAimTarget-pose.position);
                return pose;
            }
        }
        public Pose WeaponAim=>MuzzlePose;
        public bool FireOnce()=>FireAt(MuzzlePose);
        bool FireAt(Pose muzzle,bool offhand=false)
        {
            var state=Equipment;
            if(!Local||!state.drawn||SlingForEmote(state)||Time.time<nextFire||!motor.Alive||!motor.HasInputFocus||motor.InputSuspended||motor.Climbing||motor.Vaulting||Services.Session?.MenuOpen==true)return false;
            if(motor.IsVR&&(!avatar.tracking||!avatar.tracking.WorldHand(state.kind==EquipmentKind.PoweredArmor?SuitShootingLeft:offhand?!left:left).tracked))return false;
            muzzle=AssistAim(muzzle);
            if(Runtime?.RequestEquipmentAttack(muzzle)!=true)return false;
            if(!EquipmentSpecifications.Get(state.kind).twoHanded)
                GetComponent<SurvivorMoments>()?.Cancel();
            lastAttackKind=state.kind;lastAttackTime=Time.time;nextFire=Time.time+(state.kind>EquipmentKind.PoweredArmor?EquipmentSpecifications.Get(state.kind).cooldown:((state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)?.45f:state.kind==EquipmentKind.DualRifle?(state.shotgun?.20f:.65f):state.kind==EquipmentKind.SheriffHammer?(state.shotgun?.28f:.75f):.9f));return true;
        }
        Pose AxeCarry(Pose gripPose,bool handLeft)
        {
            CandyWeaponVisual.MeleeMount(EquipmentKind.LaserAxe,handLeft,out var palm,out var mount);
            if(motor.IsVR&&avatar.tracking&&avatar.tracking.WorldHand(handLeft).tracked)
            {
                var primary=avatar.tracking.WorldHand(handLeft);
                var support=avatar.tracking.WorldHand(!handLeft);
                var separation=support.position-primary.position;
                bool latched=grip?.IsPressed()==true||avatar.tracking.IsHybrid&&
                    (handLeft?avatar.tracking.RightPinching:avatar.tracking.LeftPinching);
                if(latched&&support.tracked&&separation.magnitude>.12f*Scale&&separation.magnitude<1.1f*Scale)
                {
                    var shaft=Quaternion.LookRotation(separation,primary.rotation*Vector3.up);
                    return new Pose(primary.position+shaft*Quaternion.Inverse(mount)*palm*Scale,shaft);
                }
                return CandyWeaponVisual.DrawnPose(EquipmentKind.LaserAxe,primary,handLeft,Scale);
            }
            float side=handLeft?1:-1;
            var yaw=Quaternion.Euler(0,gripPose.rotation.eulerAngles.y,0);
            var rotation=yaw*Quaternion.LookRotation(new Vector3(side*.68f,.71f,.32f),Vector3.up);
            return new Pose(gripPose.position+rotation*Quaternion.Inverse(mount)*palm*Scale,rotation);
        }

        Pose DesktopMeleePose(Pose gripPose, EquipmentState state)
        {
            if (motor.IsVR || lastAttackKind != state.kind || !EquipmentSpecifications.Get(state.kind).melee ||
                state.shotgun && (state.kind == EquipmentKind.LaserAxe || state.kind == EquipmentKind.Chainsaw))
                return gripPose;

            float side = state.leftHanded ? -1f : 1f;
            float duration = .43f;
            Vector3 windup, strike, follow, windAngle, hitAngle, followAngle;
            switch (state.kind)
            {
                case EquipmentKind.Dagger:
                    duration = .29f;
                    windup = new(0, -.035f, -.13f); strike = new(-.03f, .045f, .27f); follow = new(-.04f, -.025f, .14f);
                    windAngle = new(-18, 12, -18); hitAngle = new(3, 0, -8); followAngle = new(19, -12, -18);
                    break;
                case EquipmentKind.FlamingMarshmallow:
                    duration = .34f;
                    windup = new(.08f, -.04f, -.10f); strike = new(-.02f, .06f, .22f); follow = new(-.09f, .13f, .08f);
                    windAngle = new(28, 20, 13); hitAngle = new(-12, -10, 0); followAngle = new(-40, -28, -12);
                    break;
                case EquipmentKind.MurderSword:
                    duration = .40f;
                    windup = new(.17f, .26f, -.13f); strike = new(-.04f, .10f, .20f); follow = new(-.20f, -.13f, .12f);
                    windAngle = new(-45, 58, 34); hitAngle = new(10, -16, -8); followAngle = new(57, -62, -38);
                    break;
                case EquipmentKind.BarbedBat:
                    duration = .51f;
                    windup = new(.22f, .20f, -.16f); strike = new(-.04f, .08f, .19f); follow = new(-.24f, -.07f, .05f);
                    windAngle = new(-24, 82, 33); hitAngle = new(-7, -17, 9); followAngle = new(21, -100, -28);
                    break;
                case EquipmentKind.SawStaff:
                    duration = .57f;
                    windup = new(.24f, .05f, -.19f); strike = new(.01f, .06f, .20f); follow = new(-.23f, .09f, .08f);
                    windAngle = new(13, 67, 26); hitAngle = new(-11, -8, 4); followAngle = new(-22, -74, -17);
                    break;
                case EquipmentKind.LaserAxe:
                    duration = .65f;
                    windup = new(.14f, .30f, -.12f); strike = new(-.06f, .11f, .16f); follow = new(-.12f, -.16f, .12f);
                    windAngle = new(-64, 33, 19); hitAngle = new(8, -20, -12); followAngle = new(67, -44, -25);
                    break;
                case EquipmentKind.Chainsaw:
                    duration = .54f;
                    windup = new(.16f, -.05f, -.08f); strike = new(.03f, .05f, .18f); follow = new(-.22f, -.06f, .12f);
                    windAngle = new(-8, 34, 12); hitAngle = new(-6, 5, 0); followAngle = new(12, -46, -16);
                    break;
                case EquipmentKind.LaserClaws:
                    duration = .37f;
                    windup = new(.19f, .15f, -.10f); strike = new(-.04f, .08f, .19f); follow = new(-.21f, -.15f, .08f);
                    windAngle = new(-23, 52, 18); hitAngle = new(11, -16, 3); followAngle = new(51, -72, -31);
                    break;
                default:
                    duration = .32f;
                    windup = new(.12f, .05f, -.12f); strike = new(-.03f, .07f, .22f); follow = new(-.15f, -.04f, .12f);
                    windAngle = new(-22, 34, 14); hitAngle = new(1, -9, 0); followAngle = new(22, -34, -19);
                    break;
            }
            float progress = (Time.time - lastAttackTime) / duration;
            if (progress < 0f || progress >= 1f) return gripPose;
            Vector3 offset, euler;
            if (progress < .16f)
            {
                float t = Mathf.SmoothStep(0, 1, progress / .16f);
                offset = windup * t; euler = windAngle * t;
            }
            else if (progress < .40f)
            {
                float t = Mathf.SmoothStep(0, 1, (progress - .16f) / .24f);
                offset = Vector3.Lerp(windup, strike, t); euler = Vector3.Lerp(windAngle, hitAngle, t);
            }
            else
            {
                float recovery = (progress - .40f) / .60f;
                float t = Mathf.SmoothStep(0, 1, recovery * 3f);
                float settle = 1f - Mathf.SmoothStep(0, 1, (recovery - .32f) / .68f);
                offset = Vector3.Lerp(strike, follow, t) * settle;
                euler = Vector3.Lerp(hitAngle, followAngle, t) * settle;
            }
            offset.x *= side; euler.y *= side; euler.z *= side;
            return new Pose(gripPose.position + gripPose.rotation * offset * Scale,
                gripPose.rotation * Quaternion.Euler(euler));
        }

        public void PoseHands(ref CandyAvatarFrame frame)
        {
            var state=Equipment;if(!state.drawn||state.kind<EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor||motor&&(motor.Climbing||motor.Vaulting))return;
            ReadAcceptedBashes(state);
            var hand=state.leftHanded?frame.left:frame.right;
            var gripPose=GripPose;
            var pose=DesktopMeleePose(PresentedBashGrip(PresentedReloadGrip(gripPose,state,state.leftHanded),state),state);
            bool cosmeticMove=(pose.position-gripPose.position).sqrMagnitude>.000001f||
                Mathf.Abs(Quaternion.Dot(pose.rotation,gripPose.rotation))<.99999f;
            if(cosmeticMove&&Local&&IsShoulderLauncher(state.kind)&&avatar.viewCamera)
            {
                var eye=avatar.viewCamera;
                pose=ClearLauncherView(pose,eye.transform.position,eye.transform.rotation,Scale,
                    eye.nearClipPlane,state.kind);
            }
            bool pressed=state.triggerHeld || motor.IsVR && Local && trigger?.IsPressed()==true ||
                Local && !motor.IsVR && Mouse.current?.leftButton.isPressed==true && state.reloadRemaining<=0;
            hand.position=pose.position;
            hand.rotation=pose.rotation;
            hand.skeleton=CandyHandSkeleton.ControllerGrip(state.leftHanded,1,pressed?1:0);
            hand.skeletonTracked=true;
            if(state.kind==EquipmentKind.LaserAxe)
            {
                var carry=AxeCarry(gripPose,state.leftHanded);
                if (!motor.IsVR)
                {
                    var delta = pose.rotation * Quaternion.Inverse(gripPose.rotation);
                    carry = new Pose(pose.position + delta * (carry.position - gripPose.position), delta * carry.rotation);
                }
                CandyWeaponVisual.MeleeMount(state.kind,state.leftHanded,out var palm,out var axeMount);
                hand.rotation=carry.rotation*Quaternion.Inverse(axeMount);
                hand.position=carry.position-hand.rotation*palm*Scale;
                hand.skeleton=CandyWeaponVisual.ShaftGrip(state.kind,state.leftHanded);
            }
            else if(CandyWeaponVisual.MeleeMount(state.kind,state.leftHanded,out _,out var mount))
            {
                hand.rotation=pose.rotation*Quaternion.Inverse(mount);
                hand.skeleton=CandyWeaponVisual.ShaftGrip(state.kind,state.leftHanded);
            }
            else if(state.kind==EquipmentKind.BoxingGlove||state.kind==EquipmentKind.LaserClaws)
                hand.skeleton=CandyHandSkeleton.Rest(state.leftHanded,.96f);
            else if(state.kind==EquipmentKind.WristLaser)
                hand.skeleton=CandyHandSkeleton.Rest(state.leftHanded,pressed?.38f:.18f);
            else
                CandyWeaponVisual.TryPrimaryContact(ref hand,pose,state.kind,state.leftHanded,pressed,Scale);
            if(state.leftHanded)frame.left=hand;else frame.right=hand;

            bool supportLeft=!state.leftHanded;
            var support=supportLeft?frame.left:frame.right;
            if(state.kind==EquipmentKind.DualUzi)
            {
                if(motor.IsVR)
                {
                    var offhandAim=new Pose(support.position,support.rotation);
                    if(avatar.tracking&&avatar.tracking.TryControllerAim(supportLeft,out var aim))offhandAim=aim;
                    CandyWeaponVisual.TryPrimaryContact(ref support,offhandAim,state.kind,supportLeft,
                        offTrigger?.IsPressed()==true,Scale);
                }
                else
                {
                    var offhand=new Pose(gripPose.position+gripPose.rotation*new Vector3(state.leftHanded?.42f:-.42f,0,0)*Scale,gripPose.rotation);
                    offhand=PresentedReloadGrip(offhand,state,supportLeft);
                    CandyWeaponVisual.TryPrimaryContact(ref support,offhand,state.kind,supportLeft,pressed,Scale);
                }
                if(supportLeft)frame.left=support;else frame.right=support;
                return;
            }
            if(state.kind==EquipmentKind.LaserClaws&&!motor.IsVR)
            {
                support.position=gripPose.position+gripPose.rotation*new Vector3(state.leftHanded?.42f:-.42f,0,0)*Scale;
                support.rotation=gripPose.rotation;
                support.skeleton=CandyHandSkeleton.Rest(supportLeft,.96f);
                support.skeletonTracked=true;
                if(supportLeft)frame.left=support;else frame.right=support;
            }
            if(state.kind==EquipmentKind.LaserClaws&&motor.IsVR)
            {
                var other=state.leftHanded?frame.right:frame.left;
                if(other.tracked&&!other.skeletonTracked)
                {
                    other.skeleton=CandyHandSkeleton.Rest(!state.leftHanded,.96f);
                    other.skeletonTracked=true;
                    if(state.leftHanded)frame.right=other;else frame.left=other;
                }
            }
            if(EquipmentSpecifications.Get(state.kind).twoHanded||state.kind==EquipmentKind.Chainsaw)
            {
                bool holding=!motor.IsVR||support.tracked&&(grip?.IsPressed()==true||
                    avatar.tracking&&avatar.tracking.IsHybrid&&(state.leftHanded?avatar.tracking.RightPinching:avatar.tracking.LeftPinching));
                if(!holding||motor.IsVR&&state.reloadRemaining>0&&state.kind!=EquipmentKind.LaserAxe)return;
                var weapon=CandyWeaponVisual.DrawnPose(state.kind,hand,state.leftHanded,Scale);
                var freePosition=support.position;
                if(!CandyWeaponVisual.TrySupportContact(ref support,weapon,state.kind,supportLeft,Scale))return;
                if(motor.IsVR&&(support.position-freePosition).sqrMagnitude>.30f*.30f*Scale*Scale)return;
                if(supportLeft)frame.left=support;else frame.right=support;
            }
        }
        void Update()
        {
            if(motor&&motor.externallyDriven){UpdatePower(Equipment);return;}
            if(!Local)return;
            bool preferred=PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0)!=0;
            bool effective=motor.IsVR&&HybridXRInput.SingleController?HybridXRInput.SingleControllerLeft:preferred;
            ApplyWeaponHand(effective);
            var state=Equipment;if(state.kind!=EquipmentKind.LaserClaws||!state.drawn)offhandReady=false;UpdatePower(state);if(state.kind!=EquipmentKind.PoweredArmor)SetSuitThrust(false);if(state.kind!=EquipmentKind.PoweredArmor||!state.drawn){motor.ClearHoverFlight();suitWasFlying=false;}
            if(motor.InputSuspended||!motor.HasInputFocus||!motor.Alive||Services.Session?.MenuOpen==true){ResetBashTracking();CancelBashPresentation();ClearPickupFocus();SetSuitThrust(MaintainPausedSuit(state));ReleaseHeldArsenal();armed=false;handReady=false;return;}
            if (SlingForEmote(state))
            {
                ResetBashTracking();CancelBashPresentation();
                ClearPickupFocus();
                ReleaseHeldArsenal();
                SetSuitThrust(false);
                armed = handReady = offhandReady = false;
                return;
            }
            if(state.kind==EquipmentKind.PoweredArmor&&state.drawn){ResetBashTracking();CancelBashPresentation();ClearPickupFocus();UpdateSuit(state);return;}
            if(motor.Climbing||motor.Vaulting){ResetBashTracking();CancelBashPresentation();ClearPickupFocus();ReleaseHeldArsenal();armed=false;handReady=false;return;}
            if(!motor.IsVR&&Keyboard.current?.deleteKey.wasPressedThisFrame==true)
            {
                ResetBashTracking();CancelBashPresentation();ClearPickupFocus();
                if((Runtime as IEquipmentDropRuntime)?.RequestDropEquipment()==true)
                {ReleaseHeldArsenal();armed=false;handReady=false;offhandReady=false;}
                return;
            }
            if(UpdatePickupFocus(state))
            {
                ResetBashTracking();CancelBashPresentation();ReleaseHeldArsenal();
                armed=handReady=offhandReady=false;
                return;
            }
            if(state.kind==EquipmentKind.PoweredArmor){ResetBashTracking();CancelBashPresentation();UpdateSuit(state);return;}
            UpdateWeaponBash(state);
            bool vr=motor.IsVR,pressed=vr?trigger.WasPressedThisFrame():Mouse.current?.leftButton.wasPressedThisFrame==true;
            bool held=vr?trigger.IsPressed():Cursor.lockState==CursorLockMode.Locked&&Mouse.current?.leftButton.isPressed==true;
            if(!held)armed=true;
            if((vr?HybridXRInput.SecondaryPressed(left):Keyboard.current?.vKey.wasPressedThisFrame==true)&&UsesTriggers)
            {
                SetEquipped(!state.drawn);
                armed=false;
                return;
            }
            if(state.drawn&&EquipmentSpecifications.Get(state.kind).modeSwitch&&(vr?(HybridXRInput.SingleController?HybridXRInput.SingleModePressed:offTrigger.WasPressedThisFrame()):Keyboard.current?.rKey.wasPressedThisFrame==true))ToggleRifleMode();
            if(state.kind>EquipmentKind.PoweredArmor&&(!EquipmentSpecifications.Get(state.kind).melee||state.kind==EquipmentKind.Chainsaw||state.kind==EquipmentKind.LaserAxe&&state.shotgun))
            {UpdateHeldArsenal(held&&armed&&state.drawn,state);return;}
            bool melee=EquipmentSpecifications.Get(state.kind).melee&&!(state.kind==EquipmentKind.Chainsaw&&state.shotgun)&&!(state.kind==EquipmentKind.LaserAxe&&state.shotgun);
            if(melee&&vr&&state.drawn&&avatar.tracking)
            {
                if(state.kind==EquipmentKind.LaserClaws)CheckOffhandClaws();
                var hand=avatar.tracking.WorldHand(left);var relative=hand.position-avatar.viewCamera.transform.position;
                if(hand.tracked&&handReady&&Time.time>=nextSwing&&(relative-lastHand).magnitude/Mathf.Max(.001f,Time.deltaTime)/Scale>1.4f){if(FireOnce())nextSwing=Time.time+EquipmentSpecifications.Get(state.kind).cooldown;}
                lastHand=relative;handReady=hand.tracked;
            }
            else if(held&&armed&&state.drawn&&(!melee||pressed))FireOnce();
        }
        void UpdatePower(EquipmentState state)
        {
            var kind=state.activePower!=EquipmentKind.None?state.activePower:state.kind;
            var speed=speedEffect?speedEffect:speedEffect=Resources.Load<EffectType>("Powerups/Speed2x");var jump=jumpEffect?jumpEffect:jumpEffect=Resources.Load<EffectType>("Powerups/Jump3x");
            if(lastPower!=kind){if(lastPower==EquipmentKind.Speed)motor.Remove(speed);if(lastPower==EquipmentKind.Jump)motor.Remove(jump);lastPower=kind;}
            if((kind==EquipmentKind.Speed||kind==EquipmentKind.Jump)&&state.remaining>0)motor.Apply(new Effect{type=kind==EquipmentKind.Speed?speed:jump,duration=state.remaining,strength=1,source=gameObject});
        }
        internal void PoseWeaponPresentationHands(ref CandyAvatarFrame frame,float deltaTime)
        {
            if(visual==null||!motor||motor.IsVR||!motor.Alive||motor.Climbing||motor.Vaulting)return;
            var state=Equipment;
            if(!SlingForEmote(state))visual.PoseDesktopCarryHand(ref frame,state,deltaTime);
        }
        void LateUpdate()
        {
            if(!avatar||!avatar.Rig)return;visual??=new CandyWeaponVisual(transform,avatar.Rig.style.material);
            avatar.Rig.SetAppearance(avatar.EquippedAppearance);
            var state = Equipment;
            visual.Present(state, avatar.RenderedFrame, motor && motor.Alive && !motor.Climbing && !motor.Vaulting,
                SlingForEmote(state));
        }
    }
}

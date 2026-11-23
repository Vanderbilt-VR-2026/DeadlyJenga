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
        void OnDisable(){ResetBashTracking();CancelBashPresentation();ClearPickupFocus();nextPickupRequest=0;primaryJumpReserved=false;if(Local){ReleaseHeldArsenal();SetSuitThrust(false);motor.ClearHoverFlight();suitWasFlying=false;}trigger?.Disable();offTrigger?.Disable();grip?.Disable();visual?.Hide();}
        void OnDestroy(){trigger?.Dispose();offTrigger?.Dispose();grip?.Dispose();}
        public void SetEquipped(bool value)
        {
            var state=Equipment;if(value && motor && !motor.WeaponAvailableDuringTraversal(state,left))return;
            handFireArmed[0] = handFireArmed[1] = false;
            CancelBashPresentation();if(!value){motor?.ClearHoverFlight();suitWasFlying=false;}
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
                if (motor.Climbing || motor.Vaulting) return;
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
                EquipmentKind.LaserAxe=>new Vector3(.32f,-.58f,.36f),
                EquipmentKind.PoweredArmor=>new Vector3(0,-.12f,.40f),
                _=>new Vector3(0,-.24f,.12f)
            };
            if(leftHanded)offset.x=-offset.x;
            return true;
        }
        Pose TrackedCarry(CandyHandPose tracked, EquipmentState state, bool handLeft)
        {
            var kind = state.kind;
            var controllerRight = tracked.rotation * Vector3.right;
            tracked.rotation = avatar.WristRotation(tracked, handLeft);
            var weapon = CandyWeaponVisual.DrawnPose(kind, tracked, handLeft, Scale);
            // Shaft weapons add their palm offset when the visual is attached.
            if (CandyWeaponVisual.MeleeMount(kind, handLeft, out _, out _))
            {
                weapon.position = tracked.position;
                weapon.rotation = Quaternion.AngleAxis(45f, controllerRight) * weapon.rotation;
            }
            if (kind == EquipmentKind.LaserAxe || kind == EquipmentKind.PoweredArmor)
                return weapon;
            bool melee = EquipmentSpecifications.Get(kind).melee && !(kind == EquipmentKind.Chainsaw && state.shotgun);
            Quaternion adjusted = weapon.rotation;
            if (melee && kind != EquipmentKind.Chainsaw && kind != EquipmentKind.MurderSword && kind != EquipmentKind.BoxingGlove && kind != EquipmentKind.LaserClaws)
                adjusted *= Quaternion.Euler(0, 0, 90f);
            var angles = avatar.vrGunRotation;
            if (melee && kind != EquipmentKind.MurderSword) angles.y = 0;
            adjusted *= Quaternion.Euler(angles);
            // Turn about the wrist so adjusting the sights keeps the grip in the hand.
            weapon.position = tracked.position + adjusted * Quaternion.Inverse(weapon.rotation) * (weapon.position - tracked.position);
            if (melee && kind != EquipmentKind.MurderSword &&
                CandyWeaponVisual.MeleeMount(kind, handLeft, out var palm, out var mount))
            {
                palm *= CandyWeaponVisual.ModelScale(kind, Scale);
                weapon.position += tracked.rotation * palm - adjusted * Quaternion.Inverse(mount) * palm;
            }
            weapon.rotation = adjusted;
            return weapon;
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
                    var primary = avatar.tracking.WorldHand(left);
                    var carried = Equipment;
                    var kind = carried.kind;
                    aim = TrackedCarry(primary, carried, left);
                    if (kind == EquipmentKind.LaserAxe)
                    {
                        // Stormbreaker has its own shaft, support and lightning aim solver.
                        aim = avatar.tracking.TryControllerAim(left, out var pointer)
                            ? pointer : new Pose(primary.position, primary.rotation);
                        var angles = avatar.vrGunRotation;
                        if (!carried.shotgun) angles.y = 0;
                        aim.rotation *= Quaternion.Euler(angles);
                    }
                    else if ((UsesSupportGrip || kind == EquipmentKind.Chainsaw) &&
                        (grip?.IsPressed() == true || avatar.tracking.IsHybrid &&
                        (left ? avatar.tracking.RightPinching : avatar.tracking.LeftPinching)))
                    {
                        var secondary = avatar.tracking.WorldHand(!left);
                        var contact = secondary;
                        if (secondary.tracked && CandyWeaponVisual.TrySupportContact(ref contact, aim, kind, !left, Scale))
                        {
                            var from = contact.position - primary.position;
                            var to = secondary.position - primary.position;
                            if (to.magnitude > .12f * Scale && (contact.position - secondary.position).magnitude <= .30f * Scale)
                            {
                                var turn = Quaternion.FromToRotation(from, to);
                                aim.position = primary.position + turn * (aim.position - primary.position);
                                aim.rotation = turn * aim.rotation;
                            }
                        }
                    }

                }
                else
                {
                    var carryOffset=new Vector3(left?-.25f:.25f,-.22f,.36f);
                    var carried=Equipment;
                    if(carried.drawn&&carried.kind==EquipmentKind.LaserAxe)
                        carryOffset=new Vector3(left?-.32f:.32f,carried.shotgun?-.58f:-.68f,.36f);
                    else if(carried.drawn&&(carried.kind==EquipmentKind.ChainGun||IsShoulderLauncher(carried.kind)))
                        TryAimGripOffset(carried,left,false,out carryOffset);
                    var position=head.position+head.rotation*carryOffset*Scale;
                    if(Local&&!motor.IsVR&&Equipment.drawn&&(Mouse.current?.rightButton.isPressed==true||DesktopGamepad.AimHeld)&&
                        TryAimGripOffset(Equipment,left,false,out var offset))
                        position=head.position+head.rotation*offset*Scale;
                    Vector3 target=EquipmentAimPoint(head.position,head.forward,120);
                    desktopAimTarget=target;
                    aim=new Pose(position,Quaternion.LookRotation(target-position));
                }
                var state=Equipment;
                if(Local&&state.drawn&&IsShoulderLauncher(state.kind))
                    aim=ClearLauncherView(aim,head.position,head.rotation,Scale,
                        avatar.viewCamera?avatar.viewCamera.nearClipPlane:.035f,state.kind);
                return aim;
            }
        }
        Vector3 EquipmentAimPoint(Vector3 origin, Vector3 direction, float range)
        {
            var target = origin + direction * range;
            int count = Physics.RaycastNonAlloc(origin, direction, aimHits, range,
                (1 << Layers.Block) | (1 << Layers.Surface) | (1 << Layers.Survivor) | 1, QueryTriggerInteraction.Collide);
            float nearest = range;
            for (int i = 0; i < count; i++)
            {
                var hit = aimHits[i];
                if (hit.distance >= nearest || hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.isTrigger && hit.collider.GetComponentInParent<IModeActor>() == null) continue;
                nearest = hit.distance;
                target = hit.point;
            }
            return target;
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
                    var aim = pose;
                    Vector3 forward = aim.rotation * Vector3.forward;
                    Vector3 target = motor.externallyDriven ? aim.position + forward * 35 :
                        motor.IsVR ? EquipmentAimPoint(aim.position, forward, 35) : desktopAimTarget;
                    var carry = AxeCarry(pose, state, aim.rotation, out _);
                    return CandyWeaponVisual.StormbreakerShot(carry, target, forward, Scale * CandyWeaponVisual.HandheldScale);
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
                        EquipmentKind.CompactSidearm=>new Vector3(0,.145f,.517f),
                        EquipmentKind.StockPistol=>CandyWeaponVisual.StockPistolMuzzle,
                        EquipmentKind.DualUzi=>new Vector3(0,.12f,.51f),
                        EquipmentKind.WristLaser=>new Vector3(0,.12f,.17f),
                        _=>new Vector3(0,EquipmentSpecifications.Get(state.kind).melee?0:.12f,.10f)
                    };
                    pose.position+=pose.rotation*localMuzzle*CandyWeaponVisual.ModelScale(state.kind, Scale);
                    if(!motor.externallyDriven&&!motor.IsVR&&(desktopAimTarget-pose.position).sqrMagnitude>.001f)pose.rotation=Quaternion.LookRotation(desktopAimTarget-pose.position);
                    return pose;
                }
                pose.position+=pose.rotation*new Vector3(0,state.kind==EquipmentKind.Blaster?.125f:state.kind==EquipmentKind.DualRifle?.15f:state.kind==EquipmentKind.SheriffHammer?.12f:.035f,state.kind==EquipmentKind.DualRifle?(state.shotgun?.73f:1.0f):state.kind==EquipmentKind.SheriffHammer?.53f:state.kind==EquipmentKind.Blaster?.57f:.1f)*CandyWeaponVisual.ModelScale(state.kind, Scale);
                if(motor&&!motor.externallyDriven&&!motor.IsVR&&(state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.DualRifle||state.kind==EquipmentKind.SheriffHammer)&&(desktopAimTarget-pose.position).sqrMagnitude>.001f)
                    pose.rotation=Quaternion.LookRotation(desktopAimTarget-pose.position);
                return pose;
            }
        }
        public Pose WeaponAim=>MuzzlePose;
        public bool FireOnce()=>FireAt(MuzzlePose);
        readonly bool[] handFireArmed = new bool[2];
        readonly float[] nextHandFire = new float[2];
        EquipmentKind handFireKind;

        void UpdateHandFire(EquipmentState state, bool handLeft)
        {
            if (handFireKind != state.kind)
            {
                handFireKind = state.kind;
                handFireArmed[0] = handFireArmed[1] = false;
                nextHandFire[0] = nextHandFire[1] = 0f;
            }

            int handIndex = handLeft ? 0 : 1;
            var hand = avatar.tracking ? avatar.tracking.WorldHand(handLeft) : default;
            bool suit = state.kind == EquipmentKind.PoweredArmor;
            bool controllerTracked = HybridXRInput.ControllerTracked(handLeft);
            if (!state.drawn || !hand.tracked || (!controllerTracked && !(suit && hand.skeletonTracked)))
            {
                handFireArmed[handIndex] = false;
                return;
            }

            bool held = controllerTracked
                ? (handLeft == left ? trigger : offTrigger)?.IsPressed() == true
                : (handLeft ? avatar.tracking.LeftPinching : avatar.tracking.RightPinching);
            if (!held)
            {
                handFireArmed[handIndex] = true;
                return;
            }
            if (!handFireArmed[handIndex] || Time.time < nextHandFire[handIndex]) return;

            Pose muzzle = suit ? SuitPalmMuzzle(handLeft) : TrackedCarry(hand, state, handLeft);
            if (!suit) muzzle.position += muzzle.rotation * new Vector3(0, .12f, .51f) * CandyWeaponVisual.ModelScale(state.kind, Scale);
            // Pace commands per hand even when a shot fails its authority checks.
            nextHandFire[handIndex] = Time.time + EquipmentSpecifications.Get(state.kind).cooldown;
            FireAt(muzzle, handLeft != left, handSpecific: true);
        }

        bool FireAt(Pose muzzle, bool offhand = false, bool handSpecific = false)
        {
            var state=Equipment;
            if(!Local||!state.drawn||SlingForEmote(state)||(!handSpecific && Time.time<nextFire)||!motor.Alive||!motor.HasInputFocus||motor.InputSuspended||!motor.WeaponAvailableDuringTraversal(state,offhand ? !left : left)||Services.Session?.MenuOpen==true)return false;
            if(motor.IsVR&&(!avatar.tracking||!avatar.tracking.WorldHand(offhand ? !left : left).tracked))return false;
            muzzle=AssistAim(muzzle);
            if (handSpecific)
            {
                if ((Runtime as IPairedEquipmentRuntime)?.RequestHandAttack(muzzle, offhand ? !left : left) != true) return false;
            }
            else if (Runtime?.RequestEquipmentAttack(muzzle) != true) return false;
            if(!EquipmentSpecifications.Get(state.kind).twoHanded)
                GetComponent<SurvivorMoments>()?.Cancel();
            lastAttackKind=state.kind;lastAttackTime=Time.time;if(!handSpecific)nextFire=Time.time+(state.kind>EquipmentKind.PoweredArmor?EquipmentSpecifications.Get(state.kind).cooldown:((state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)?.45f:state.kind==EquipmentKind.DualRifle?(state.shotgun?.20f:.65f):state.kind==EquipmentKind.SheriffHammer?(state.shotgun?.28f:.75f):.9f));return true;
        }
        Pose AxeCarry(Pose gripPose, EquipmentState state, Quaternion aimRotation, out bool holdingSupport)
        {
            Vector3? supportPosition = null;
            if (motor.IsVR && avatar.tracking)
            {
                bool handLeft = state.leftHanded;
                var support = avatar.tracking.WorldHand(!handLeft);
                bool latched = grip?.IsPressed() == true || avatar.tracking.IsHybrid &&
                    (handLeft ? avatar.tracking.RightPinching : avatar.tracking.LeftPinching);
                if (latched && support.tracked) supportPosition = support.position;
            }
            return CandyWeaponVisual.StormbreakerCarry(new Pose(gripPose.position, aimRotation), state.leftHanded,
                state.shotgun, Scale * CandyWeaponVisual.HandheldScale, supportPosition, out holdingSupport, motor.IsVR && !state.shotgun);
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
            var state=Equipment;if(!state.drawn||state.kind<EquipmentKind.Blaster||motor&&!motor.WeaponAvailableDuringTraversal(state,state.leftHanded))return;
            ReadAcceptedWeaponMotion(state);
            if(state.kind==EquipmentKind.PoweredArmor)
            {
                void Recoil(ref CandyHandPose hand, bool handLeft)
                {
                    var recoil = PresentedRecoil(new Pose(hand.position, hand.rotation), state, handLeft, handLeft != state.leftHanded);
                    hand.position = recoil.position;
                    hand.rotation = recoil.rotation;
                }
                if (motor.IsVR || state.leftHanded) Recoil(ref frame.left, true);
                if (motor.IsVR || !state.leftHanded) Recoil(ref frame.right, false);
                return;
            }
            var hand=state.leftHanded?frame.left:frame.right;
            var gripPose=GripPose;
            var carryPose=PresentedSprintGrip(gripPose,state,frame,state.leftHanded);
            var pose=DesktopMeleePose(PresentedBashGrip(PresentedReloadGrip(carryPose,state,state.leftHanded),state),state);
            pose=PresentedRecoil(pose,state,state.leftHanded);
            bool cosmeticMove=(pose.position-gripPose.position).sqrMagnitude>.000001f||
                Mathf.Abs(Quaternion.Dot(pose.rotation,gripPose.rotation))<.99999f;
            if(cosmeticMove&&Local&&IsShoulderLauncher(state.kind)&&avatar.viewCamera)
            {
                var eye=avatar.viewCamera;
                pose=ClearLauncherView(pose,eye.transform.position,eye.transform.rotation,Scale,
                    eye.nearClipPlane,state.kind);
            }
            bool pressed=state.triggerHeld || motor.IsVR && Local && trigger?.IsPressed()==true ||
                Local && !motor.IsVR && (Mouse.current?.leftButton.isPressed==true||DesktopGamepad.FireHeld) && state.reloadRemaining<=0;
            hand.position=pose.position;
            hand.rotation=pose.rotation;
            hand.skeleton=CandyHandSkeleton.ControllerGrip(state.leftHanded,1,pressed?1:0);
            hand.skeletonTracked=true;
            bool axeSupport = false;
            if(state.kind==EquipmentKind.LaserAxe)
            {
                var aim = gripPose;
                var carry = AxeCarry(gripPose, state, aim.rotation, out axeSupport);
                var delta=pose.rotation*Quaternion.Inverse(gripPose.rotation);
                carry=new Pose(pose.position+delta*(carry.position-gripPose.position),delta*carry.rotation);
                CandyWeaponVisual.MeleeMount(state.kind,state.leftHanded,out var palm,out var axeMount);
                hand.rotation=carry.rotation*Quaternion.Inverse(axeMount);
                hand.position=carry.position-hand.rotation*palm*(Scale * CandyWeaponVisual.HandheldScale);
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
                    if (avatar.tracking)
                    {
                        var tracked = avatar.tracking.WorldHand(supportLeft);
                        if (tracked.tracked) offhandAim = TrackedCarry(tracked, state, supportLeft);
                    }
                    offhandAim=PresentedSprintGrip(offhandAim,state,frame,supportLeft);
                    offhandAim=PresentedRecoil(offhandAim,state,supportLeft,true);
                    CandyWeaponVisual.TryPrimaryContact(ref support,offhandAim,state.kind,supportLeft,
                        offTrigger?.IsPressed()==true,Scale);
                }
                else
                {
                    var offhand=new Pose(gripPose.position+gripPose.rotation*new Vector3(state.leftHanded?.42f:-.42f,0,0)*Scale,gripPose.rotation);
                    offhand=PresentedSprintGrip(offhand,state,frame,supportLeft);
                    offhand=PresentedRecoil(PresentedReloadGrip(offhand,state,supportLeft),state,supportLeft,true);
                    CandyWeaponVisual.TryPrimaryContact(ref support,offhand,state.kind,supportLeft,pressed,Scale);
                }
                if(supportLeft)frame.left=support;else frame.right=support;
                return;
            }
            if(state.kind==EquipmentKind.LaserClaws&&!motor.IsVR)
            {
                var clawGrip=new Pose(gripPose.position+gripPose.rotation*new Vector3(state.leftHanded?.42f:-.42f,0,0)*Scale,gripPose.rotation);
                clawGrip=PresentedSprintGrip(clawGrip,state,frame,supportLeft);
                support.position=clawGrip.position;
                support.rotation=clawGrip.rotation;
                support.skeleton=CandyHandSkeleton.Rest(supportLeft,.96f);
                support.skeletonTracked=true;
                if(supportLeft)frame.left=support;else frame.right=support;
            }
            if(state.kind==EquipmentKind.LaserClaws&&motor.IsVR)
            {
                var other=state.leftHanded?frame.right:frame.left;
                if(other.tracked)
                {
                    other.rotation *= Quaternion.Euler(avatar.vrGunRotation.x, 0, avatar.vrGunRotation.z);
                    other.skeleton=CandyHandSkeleton.Rest(!state.leftHanded,.96f);
                    other.skeletonTracked=true;
                    if(state.leftHanded)frame.right=other;else frame.left=other;
                }
            }
            if(EquipmentSpecifications.Get(state.kind).twoHanded||state.kind==EquipmentKind.Chainsaw)
            {
                bool holding = !motor.IsVR || (state.kind == EquipmentKind.LaserAxe ? axeSupport :
                    support.tracked && (grip?.IsPressed() == true || avatar.tracking && avatar.tracking.IsHybrid &&
                        (state.leftHanded ? avatar.tracking.RightPinching : avatar.tracking.LeftPinching)));
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
            if(motor.InputSuspended||!motor.HasInputFocus||!motor.Alive||Services.Session?.MenuOpen==true){ResetBashTracking();CancelBashPresentation();ClearPickupFocus();SetSuitThrust(MaintainPausedSuit(state));ReleaseHeldArsenal();handFireArmed[0]=handFireArmed[1]=false;armed=false;handReady=false;return;}
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
            bool holsterPressed = motor.IsVR ? HybridXRInput.SecondaryPressed(left) :
                Keyboard.current?.vKey.wasPressedThisFrame == true || DesktopGamepad.HolsterPressed;
            if(!motor.WeaponAvailableDuringTraversal(state,left))
            {
                if (state.drawn && holsterPressed) SetEquipped(false);
                ResetBashTracking();CancelBashPresentation();ClearPickupFocus();ReleaseHeldArsenal();armed=false;handReady=false;return;
            }
            if(!motor.IsVR&&(Keyboard.current?.deleteKey.wasPressedThisFrame==true||DesktopGamepad.DropPressed))
            {
                ResetBashTracking();CancelBashPresentation();ClearPickupFocus();
                if((Runtime as IEquipmentDropRuntime)?.RequestDropEquipment()==true)
                {ReleaseHeldArsenal();armed=false;handReady=false;offhandReady=false;}
                return;
            }
            if (motor.Climbing) ClearPickupFocus();
            else if(UpdatePickupFocus(state))
            {
                ResetBashTracking();CancelBashPresentation();ReleaseHeldArsenal();
                armed=handReady=offhandReady=false;
                return;
            }
            if(state.kind==EquipmentKind.PoweredArmor){ResetBashTracking();CancelBashPresentation();UpdateSuit(state);return;}
            UpdateWeaponBash(state);
            bool vr=motor.IsVR,pressed=vr?trigger.WasPressedThisFrame():(Mouse.current?.leftButton.wasPressedThisFrame==true||DesktopGamepad.FirePressed);
            bool held=vr?trigger.IsPressed():(Cursor.lockState==CursorLockMode.Locked||DesktopGamepad.Current!=null)&&(Mouse.current?.leftButton.isPressed==true||DesktopGamepad.FireHeld);
            if(!held)armed=true;
            if(holsterPressed&&UsesTriggers)
            {
                SetEquipped(!state.drawn);
                armed=false;
                return;
            }
            if(state.drawn&&EquipmentSpecifications.Get(state.kind).modeSwitch&&(vr?(HybridXRInput.SingleController?HybridXRInput.SingleModePressed:offTrigger.WasPressedThisFrame()):(Keyboard.current?.rKey.wasPressedThisFrame==true||DesktopGamepad.ModePressed)))ToggleRifleMode();
            if (vr && state.kind == EquipmentKind.DualUzi)
            {
                UpdateHandFire(state, true);
                UpdateHandFire(state, false);
                return;
            }
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
            if(!motor||!motor.Alive||motor.Climbing||motor.Vaulting)return;
            var state=Equipment;
            if (motor.IsVR)
            {
                if (!Local || !avatar.tracking || !state.drawn || motor.EmoteActive ||
                    !EquipmentSpecifications.Get(state.kind).melee) return;
                // The weapon uses the carry frame. Rotate only the rendered hand about its grip.
                float frameScale = frame.scale;
                void AlignWrist(ref CandyHandPose hand, bool left)
                {
                    var tracked = avatar.tracking.WorldHand(left);
                    if (!tracked.tracked) return;
                    var rotation = avatar.WristRotation(tracked, left);
                    if (CandyWeaponVisual.MeleeMount(state.kind, left, out var palm, out _))
                    {
                        palm *= CandyWeaponVisual.ModelScale(state.kind, frameScale);
                        hand.position += hand.rotation * palm - rotation * palm;
                    }
                    if (state.kind != EquipmentKind.MurderSword && left == state.leftHanded)
                        hand.position = tracked.position;
                    hand.rotation = rotation;
                }
                bool usesBothHands = state.kind is EquipmentKind.LaserAxe or EquipmentKind.Chainsaw or EquipmentKind.LaserClaws;
                if (state.leftHanded || usesBothHands) AlignWrist(ref frame.left, true);
                if (!state.leftHanded || usesBothHands) AlignWrist(ref frame.right, false);
                return;
            }
            if(visual!=null&&!SlingForEmote(state))visual.PoseDesktopCarryHand(ref frame,state,deltaTime);
        }
        void LateUpdate()
        {
            if(!avatar||!avatar.Rig)return;visual??=new CandyWeaponVisual(transform,avatar.Rig.style.material);
            avatar.Rig.SetAppearance(avatar.EquippedAppearance);
            var state = Equipment;
            visual.SetCosmeticColor(avatar.Rig.BaseAppearanceColor);
            visual.Present(state, avatar.RenderedFrame, motor && motor.Alive && motor.WeaponAvailableDuringTraversal(state,state.leftHanded),
                SlingForEmote(state));
        }
    }
}

using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyPlayers
{
    [DefaultExecutionOrder(190)]
    public sealed partial class CandyEquipment:MonoBehaviour,IEquipmentControls
    {
        CastleSurvivor motor;CandyPlayerAvatar avatar;CandyWeaponVisual visual;
        InputAction trigger,offTrigger,grip;
        bool left,armed;float nextFire,lastAttackTime=float.NegativeInfinity;EquipmentKind lastPower;
        Vector3 lastHand;bool handReady;float nextSwing;
        readonly RaycastHit[] aimHits=new RaycastHit[32];
        EffectType speedEffect,jumpEffect;
        int pendingPickup,dismissedPickup;float nextPickupRequest;
        public int PendingPickup=>pendingPickup;
        public void ConfirmSwap(bool accept)
        {
            if(pendingPickup==0)return;
            if(accept)Runtime?.RequestPickup(pendingPickup,true);
            dismissedPickup=pendingPickup;pendingPickup=0;nextPickupRequest=Time.unscaledTime+.6f;
        }
        void CheckPickups()
        {
            var match=MatchRuntime.Current;if(match==null||match.Frame.phase!=RoundPhase.Playing)return;
            EquipmentPickup nearby=null;float nearest=1.25f;
            foreach(var pickup in match.Frame.pickups)
            {
                float distance=Vector3.Distance(transform.position,pickup.position);
                if(pickup.availableIn<=0&&distance<nearest){nearest=distance;nearby=pickup;}
            }
            if(nearby==null){pendingPickup=dismissedPickup=0;return;}
            if(nearby.id==dismissedPickup)return;
            if(Equipment.kind!=EquipmentKind.None){pendingPickup=nearby.id;return;}
            if(Time.unscaledTime>=nextPickupRequest){Runtime?.RequestPickup(nearby.id,false);nextPickupRequest=Time.unscaledTime+.5f;}
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
                    state=new EquipmentState{actor=motor.ActorId,kind=own.role==MysteryRole.Murderer?EquipmentKind.MurderSword:own.role==MysteryRole.Sheriff?EquipmentKind.SheriffHammer:EquipmentKind.None,roleIssued=true,drawn=own.specialAttack,leftHanded=left};
                }
                return state;
            }
        }
        public bool BlocksTraversal=>Equipment.kind==EquipmentKind.DualRifle&&Equipment.drawn;
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
        void OnDisable(){if(Local)SetSuitThrust(false);trigger?.Disable();offTrigger?.Disable();grip?.Disable();visual?.Hide();}
        void OnDestroy(){trigger?.Dispose();offTrigger?.Dispose();grip?.Dispose();}
        public void SetEquipped(bool value)
        {
            var state=Equipment;
            if(state.roleIssued&&MatchRuntime.Current is IMysteryControls mystery)mystery.SetSpecialAttack(value);
            Runtime?.RequestEquipment(value,left,state.shotgun);armed=false;motor?.ResetVRJump();
        }
        public void SetWeaponHand(bool value){left=value;PlayerPrefs.SetInt("CandyBlaster.LeftHanded",left?1:0);PlayerPrefs.Save();Bind();var state=Equipment;Runtime?.RequestEquipment(state.drawn,left,state.shotgun);}
        public void ToggleRifleMode(){var state=Equipment;if(state.kind==EquipmentKind.DualRifle)Runtime?.RequestEquipment(state.drawn,left,!state.shotgun);}
        public bool JumpHeld=>UsesTriggers&&Equipment.kind!=EquipmentKind.PoweredArmor&&!BlocksTraversal&&offTrigger!=null&&offTrigger.IsPressed();
        public Pose GripPose
        {
            get
            {
                var head=avatar&&avatar.viewCamera?avatar.viewCamera.transform:transform;
                Pose aim;
                if(motor&&motor.IsVR&&avatar.tracking&&avatar.tracking.WorldHand(left).tracked)
                {
                    var primary=avatar.tracking.WorldHand(left);aim=new Pose(primary.position,primary.rotation);
                    if(BlocksTraversal&&grip.IsPressed())
                    {
                        var secondary=avatar.tracking.WorldHand(!left);var delta=secondary.position-primary.position;
                        if(secondary.tracked&&delta.magnitude>.12f*Scale&&delta.magnitude<1.1f*Scale)aim.rotation=Quaternion.LookRotation(delta,primary.rotation*Vector3.up);
                    }
                }
                else
                {
                    var position=head.position+head.rotation*new Vector3(left?-.25f:.25f,-.22f,.36f)*Scale;
                    Vector3 target=head.position+head.forward*70;
                    int count=Physics.RaycastNonAlloc(head.position,head.forward,aimHits,70,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);float nearest=70;
                    for(int i=0;i<count;i++){var hit=aimHits[i];if(!hit.collider.transform.IsChildOf(transform)&&(!hit.collider.isTrigger||hit.collider.GetComponentInParent<IModeActor>()!=null)&&hit.distance<nearest){nearest=hit.distance;target=hit.point;}}
                    aim=new Pose(position,Quaternion.LookRotation(target-position));
                }
                if(motor&&!motor.IsVR&&Equipment.drawn)
                {
                    float age=Time.time-lastAttackTime;
                    bool melee=Equipment.kind==EquipmentKind.MurderSword||Equipment.kind==EquipmentKind.SheriffHammer||Equipment.kind==EquipmentKind.BoxingGlove;
                    if(age>=0&&age<.3f)aim.position+=aim.rotation*Vector3.forward*(Mathf.Sin(age/.3f*Mathf.PI)*(melee?.30f:-.035f)*Scale);
                }
                return aim;
            }
        }
        public Pose MuzzlePose
        {get{var state=Equipment;if(state.kind==EquipmentKind.PoweredArmor)return SuitMuzzle;var pose=GripPose;pose.position+=pose.rotation*new Vector3(0,state.kind==EquipmentKind.Blaster?.125f:state.kind==EquipmentKind.DualRifle?.09f:.035f,state.kind==EquipmentKind.DualRifle?(state.shotgun?.68f:.98f):state.kind==EquipmentKind.Blaster?.57f:.1f)*Scale;return pose;}}
        public bool FireOnce()
        {
            var state=Equipment;
            if(!Local||!state.drawn||Time.time<nextFire||!motor.Alive||!motor.HasInputFocus||motor.InputSuspended||motor.Climbing||motor.Vaulting||Services.Session?.MenuOpen==true)return false;
            if(motor.IsVR&&(!avatar.tracking||!avatar.tracking.WorldHand(state.kind==EquipmentKind.PoweredArmor?SuitShootingLeft:left).tracked))return false;
            if(Runtime?.RequestEquipmentAttack(MuzzlePose)!=true)return false;
            lastAttackTime=Time.time;nextFire=Time.time+((state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor)?.45f:state.kind==EquipmentKind.DualRifle?(state.shotgun?.85f:.65f):.9f);return true;
        }
        public void PoseHands(ref CandyAvatarFrame frame)
        {
            var state=Equipment;if(!state.drawn||state.kind<EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor||motor&&(motor.Climbing||motor.Vaulting))return;
            var hand=state.leftHanded?frame.left:frame.right;var pose=motor&&motor.externallyDriven?new Pose(hand.position,hand.rotation):GripPose;hand.position=pose.position;hand.rotation=pose.rotation;hand.skeleton=CandyHandSkeleton.Rest(state.leftHanded,.88f);hand.skeletonTracked=true;
            if(state.leftHanded)frame.left=hand;else frame.right=hand;
            if(state.kind==EquipmentKind.DualRifle&&(!motor.IsVR||grip.IsPressed()))
            {
                var support=state.leftHanded?frame.right:frame.left;support.position=pose.position+pose.rotation*new Vector3(0,-.015f,.43f)*Scale;support.rotation=pose.rotation;support.skeleton=CandyHandSkeleton.Rest(!state.leftHanded,.85f);support.skeletonTracked=true;
                if(state.leftHanded)frame.right=support;else frame.left=support;
            }
        }
        void Update()
        {
            if(!Local)return;
            var state=Equipment;UpdatePower(state);if(state.kind!=EquipmentKind.PoweredArmor)SetSuitThrust(false);
            if(motor.InputSuspended||!motor.HasInputFocus||!motor.Alive||Services.Session?.MenuOpen==true){SetSuitThrust(false);armed=false;handReady=false;return;}
            CheckPickups();
            if(pendingPickup!=0)
            {
                SetSuitThrust(false);
                bool accept=motor.IsVR?trigger.WasPressedThisFrame():Keyboard.current?.eKey.wasPressedThisFrame==true;
                bool decline=motor.IsVR?offTrigger.WasPressedThisFrame():Keyboard.current?.nKey.wasPressedThisFrame==true;
                if(accept||decline)ConfirmSwap(accept);armed=false;return;
            }
            if(state.kind==EquipmentKind.PoweredArmor){UpdateSuit(state);return;}
            if(state.drawn&&(motor.Climbing||motor.Vaulting)){SetEquipped(false);return;}
            bool vr=motor.IsVR,pressed=vr?trigger.WasPressedThisFrame():Mouse.current?.leftButton.wasPressedThisFrame==true;
            bool held=vr?trigger.IsPressed():Cursor.lockState==CursorLockMode.Locked&&Mouse.current?.leftButton.isPressed==true;
            if(!held)armed=true;
            if(Keyboard.current?.vKey.wasPressedThisFrame==true)SetEquipped(!state.drawn);
            if(state.kind==EquipmentKind.DualRifle&&(vr?offTrigger.WasPressedThisFrame():Keyboard.current?.rKey.wasPressedThisFrame==true))ToggleRifleMode();
            // Trigger near the physical holster draws or stows; elsewhere it fires.
            if(vr&&pressed&&UsesTriggers)
            {
                var hand=avatar.tracking?avatar.tracking.WorldHand(left):default;
                var head=avatar.viewCamera.transform;var yaw=Quaternion.Euler(0,head.eulerAngles.y,0);
                var holster=CandyWeaponVisual.Holster(state.kind,transform.position,head.position,yaw,Scale,left);
                bool near=hand.tracked&&Vector3.Distance(hand.position,holster.position)<.35f*Scale;
                if(!state.drawn||near){SetEquipped(!state.drawn);return;}
            }
            bool melee=state.kind==EquipmentKind.MurderSword||state.kind==EquipmentKind.SheriffHammer||state.kind==EquipmentKind.BoxingGlove;
            if(melee&&vr&&state.drawn&&avatar.tracking)
            {
                var hand=avatar.tracking.WorldHand(left);var relative=hand.position-avatar.viewCamera.transform.position;
                if(hand.tracked&&handReady&&Time.time>=nextSwing&&(relative-lastHand).magnitude/Mathf.Max(.001f,Time.deltaTime)/Scale>1.4f){if(FireOnce())nextSwing=Time.time+.9f;}
                lastHand=relative;handReady=hand.tracked;
            }
            else if(held&&armed&&state.drawn&&(!melee||pressed))FireOnce();
        }
        void UpdatePower(EquipmentState state)
        {
            var speed=speedEffect?speedEffect:speedEffect=Resources.Load<EffectType>("Powerups/Speed2x");var jump=jumpEffect?jumpEffect:jumpEffect=Resources.Load<EffectType>("Powerups/Jump3x");
            if(lastPower!=state.kind){if(lastPower==EquipmentKind.Speed)motor.Remove(speed);if(lastPower==EquipmentKind.Jump)motor.Remove(jump);lastPower=state.kind;}
            if((state.kind==EquipmentKind.Speed||state.kind==EquipmentKind.Jump)&&state.remaining>0)motor.Apply(new Effect{type=state.kind==EquipmentKind.Speed?speed:jump,duration=state.remaining,strength=1,source=gameObject});
        }
        void LateUpdate()
        {
            if(!avatar||!avatar.Rig)return;visual??=new CandyWeaponVisual(transform,avatar.Rig.style.material);
            avatar.Rig.SetAppearance(avatar.EquippedAppearance);
            visual.Present(Equipment,avatar.LastFrame,motor&&motor.Alive);
        }
    }
}

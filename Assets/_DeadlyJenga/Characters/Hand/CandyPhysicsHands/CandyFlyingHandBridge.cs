using UnityEngine;
using FlyingHand;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.CharacterNetwork;
using System.IO;
using UnityEngine.InputSystem;
namespace DeadlyJenga.CandyPhysicsHands
{
    [DefaultExecutionOrder(50)]
    public sealed class CandyFlyingHandBridge : MonoBehaviour, INetworkActorView, INetworkPreview, ITrackedFlightInputSource
    {
        public CastleHandActor actor;
        public CandyPhysicalHand prefab;
        public CandyHandSettings settings;
        public CandyPlayerStyle trackingStyle;
        public bool useLeftHand;
        public bool ExternalUsesJointDrives {get;set;}=true;
        public bool SuppressNetworkRegistration {get;set;}
        SeaSpiritController Spirit=>GetComponent<SeaSpiritController>();
        public CandyHandMode Mode=>transition.Mode;
        public CandyPhysicalHand PhysicalHand=>physical;
        public HandDriveProfile PhysicsProfile { get; set; }
        Pose[] pendingAuthoritativeBones;
        float authoritativeAt=-100;
        public void ReadState(BinaryReader reader,bool isActive)
        {
            if(!NetworkPreview)return;
            CharacterWire.ReadHand(reader,out bool left,out float size,out _,out var bones);
            var sea=CharacterWire.ReadSeaSpirit(reader);
            if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing own-hand feedback");
            if(sea!=null)
            {
                var spirit=Spirit;
                if(spirit&&spirit.Active)
                {
                    using(var r=new BinaryReader(new MemoryStream(sea.left,false)))spirit.LeftHand.GetComponent<CandyFlyingHandBridge>().ReadState(r,isActive);
                    using(var r=new BinaryReader(new MemoryStream(sea.right,false)))spirit.RightHand.GetComponent<CandyFlyingHandBridge>().ReadState(r,isActive);
                }
                return;
            }
            if(!isActive||left!=useLeftHand||!settings||Mathf.Abs(size-settings.size)>.01f||bones.Length!=16)
            {
                pendingAuthoritativeBones=null;authoritativeAt=-100;
                if(physical)physical.ClearAuthoritativePreview();
                return;
            }
            pendingAuthoritativeBones=bones;authoritativeAt=Time.unscaledTime;
            if(physical)physical.ReceiveAuthoritativePreview(bones,authoritativeAt);
        }
        public void Render(float dt)
        {
            if(NetworkPreview&&physical)physical.RenderAuthoritativePreview(dt);
            var spirit=Spirit;
            if(NetworkPreview&&spirit&&spirit.Active)
            {
                spirit.LeftHand?.GetComponent<CandyFlyingHandBridge>()?.Render(dt);
                spirit.RightHand?.GetComponent<CandyFlyingHandBridge>()?.Render(dt);
            }
        }
        public void Suspend()
        {
            pendingAuthoritativeBones=null;authoritativeAt=-100;
            if(physical)physical.ClearAuthoritativePreview();
        }
        // A joining headset predicts its own hand visuals; only the host makes
        // contact with the shared castle. Set every frame to cover rig rebuilds.
        bool spectatorInputHeld;
        bool EffectiveInputBlocked
        {
            get
            {
                if(LocalSpectatorParticipation.TryRead(gameObject,out bool spectator))spectatorInputHeld=spectator;
                return actor.motor.InputSuspended||spectatorInputHeld;
            }
        }
        public bool NetworkPreview {get;set;}
        CandyHandTransition transition=new CandyHandTransition();
        public ICandyHandPoseSource PoseSource {get;set;}
        CandyTrackingSource source;
        CandyPhysicalHand physical;
        Collider[] pointerColliders;
        Renderer[] pointerRenderers;
        bool[] colliderEnabled,rendererEnabled;
        Vector3 trackingAnchor;
        bool anchored;
        float selectedMissing,alternateStable;
        bool preferenceInitialized,lastPreference;
        float preferredClearSince=-1,nextPreferredClearCheck;
        public bool PreferredHandReturnPending {get;private set;}
        public string TrackingStatus=>PreferredHandReturnPending?"Preferred hand ready; release current contact to switch back":"";
        bool trackedGeometry;
        public DesktopHandPose DesktopPose { get; private set; }
        public float DesktopClench { get; private set; }
        CandyHandSkeleton desktopSkeleton;
        bool desktopPoseReady;
        readonly CandyHandFlightGesture flightGesture=new CandyHandFlightGesture();
        void Awake()
        {
            if(!actor.motor.externallyDriven){source=gameObject.AddComponent<CandyTrackingSource>();source.style=trackingStyle;actor.motor.TrackedInputDriver=this;}
            var visuals=actor.motor.transform.Find("HandVisual");pointerRenderers=visuals?visuals.GetComponentsInChildren<Renderer>(true):new Renderer[0];
            pointerColliders=actor.motor.GetComponentsInChildren<Collider>(true);
            colliderEnabled=new bool[pointerColliders.Length];rendererEnabled=new bool[pointerRenderers.Length];
            for(int i=0;i<colliderEnabled.Length;i++)colliderEnabled[i]=pointerColliders[i].enabled;
            for(int i=0;i<rendererEnabled.Length;i++)rendererEnabled[i]=pointerRenderers[i].enabled;
        }
        void Pointer(bool value)
        {
            for(int i=0;i<pointerColliders.Length;i++)pointerColliders[i].enabled=value&&colliderEnabled[i];
            for(int i=0;i<pointerRenderers.Length;i++)pointerRenderers[i].enabled=value&&rendererEnabled[i];
        }
        public bool TryReadFlightInput(out FlyingHandCommand command)
        {
            command=default;
            if(!source||!source.HasHandTracking){flightGesture.Reset();return false;}
            var steering=source.WorldHand(!useLeftHand);
            if(!steering.tracked||!steering.skeletonTracked){flightGesture.Reset();return false;}
            command=flightGesture.Read(steering,actor.follow.transform.rotation,EffectiveInputBlocked);
            return true;
        }
        public void ResetFlightInput()=>flightGesture.Reset();
        void Update()
        {
            bool desktop=actor.motor.ActiveControlMode==ControlMode.Desktop;
            Pointer(false);
            bool external=actor.motor.externallyDriven;
            if(!desktop&&!external&&PoseSource==null&&source)
            {
                bool preferred=PlayerPrefs.GetInt("CandyBlaster.LeftHanded",useLeftHand?1:0)!=0;
                if(!preferenceInitialized){lastPreference=preferred;preferenceInitialized=true;if(!physical&&!transition.Initialized)useLeftHand=preferred;}
                if(preferred!=lastPreference)
                {ResetTrackingRig();useLeftHand=preferred;lastPreference=preferred;selectedMissing=alternateStable=0;}
                var selected=source.WorldHand(useLeftHand);var alternate=source.WorldHand(!useLeftHand);
                bool selectedValid=selected.tracked&&CandyHandTransition.IsValidPose(selected);
                bool alternateValid=alternate.tracked&&CandyHandTransition.IsValidPose(alternate);
                float elapsed=Mathf.Clamp(Time.unscaledDeltaTime,0,.05f);
                selectedMissing=selectedValid?0:selectedMissing+elapsed;
                alternateStable=alternateValid?alternateStable+elapsed:0;
                PreferredHandReturnPending=useLeftHand!=preferred&&alternateValid&&alternateStable>=settings.stableTime;
                if(!PreferredHandReturnPending)preferredClearSince=-1;
                bool fallback=!selectedValid&&alternateValid&&selectedMissing>=settings.dropoutHold+settings.releaseTime&&
                    alternateStable>=settings.stableTime&&transition.Strength<=0;
                bool returnPreferred=PreferredHandReturnPending&&PreferredReturnIsClear();
                if(fallback||returnPreferred)
                {
                    useLeftHand=!useLeftHand;anchored=false;flightGesture.Reset();
                    if(physical)
                    {
                        var rest=physical.Target;rest.position=physical.bodies[0].position;rest.rotation=physical.bodies[0].rotation;
                        rest.skeleton=alternate.skeleton;rest.skeletonTracked=alternate.skeletonTracked;
                        physical.SetInteractive(false);physical.left=useLeftHand;physical.RebuildInactive(rest);
                        trackedGeometry=alternate.skeletonTracked;
                    }
                    pendingAuthoritativeBones=null;authoritativeAt=-100;selectedMissing=alternateStable=0;
                    PreferredHandReturnPending=false;preferredClearSince=-1;
                }
            }
            else {PreferredHandReturnPending=false;preferredClearSince=-1;}
            var raw=source?source.WorldHand(useLeftHand):default;
            if(PoseSource!=null){if(!PoseSource.TryGetPose(out raw))raw=default;}
            else if(external)raw=default;
            if(!CandyHandTransition.IsValidPose(raw))raw.tracked=false;
            if(!raw.tracked)anchored=false;
            CandyHandMode mode=raw.tracked?(raw.skeletonTracked?CandyHandMode.HandTracking:CandyHandMode.Controller):CandyHandMode.Inactive;
            if(desktop&&!external)
            {
                ReadDesktopPose();
                raw=new CandyHandPose{tracked=true,skeletonTracked=true,position=actor.motor.transform.position,rotation=actor.motor.transform.rotation,skeleton=desktopSkeleton};
                mode=CandyHandMode.Desktop;anchored=false;
            }
            if(raw.tracked)
            {
                if(!desktop&&!external)
                {
                    if(PoseSource==null&&source&&source.trackingOrigin)
                    {
                        var origin=source.trackingOrigin;
                        raw.position=origin.InverseTransformPoint(raw.position);
                        raw.rotation=Quaternion.Inverse(origin.rotation)*raw.rotation;
                    }
                    if(!anchored){trackingAnchor=raw.position;anchored=true;}
                    raw.position=actor.motor.transform.position+actor.motor.transform.rotation*((raw.position-trackingAnchor)*settings.size);
                    raw.rotation=actor.motor.transform.rotation*raw.rotation;
                }
            }
            transition.Step(raw,mode,useLeftHand,Time.unscaledDeltaTime,settings);
            if(!transition.Initialized)return;
            if(transition.Mode==CandyHandMode.Inactive&&transition.Strength<=0)
            {if(physical&&physical.gameObject.activeSelf){physical.SetInteractive(false);physical.gameObject.SetActive(false);}return;}
            bool needsTrackedGeometry=transition.Mode==CandyHandMode.HandTracking;
            if(!physical){physical=Instantiate(prefab);physical.name="Contact driven candy hand";physical.settings=settings;physical.left=useLeftHand;physical.JointDrivenFingers=external&&ExternalUsesJointDrives;physical.Build(transition.Pose);trackedGeometry=needsTrackedGeometry;}
            // Wrist movement must stay visible relative to the observer.
            actor.follow.target=actor.motor.transform;
            actor.follow.positionTarget=physical.bodies[0].transform;actor.follow.compactHandView=true;
            if(trackedGeometry!=needsTrackedGeometry&&raw.tracked&&transition.Mode==mode)
            {
                physical.SetInteractive(false);
                var calibration=transition.Pose;calibration.skeleton=raw.skeleton;
                physical.RebuildInactive(calibration);trackedGeometry=needsTrackedGeometry;
            }
            physical.Profile=PhysicsProfile;physical.RequiresProfile=true;
            actor.motor.ContactRulesConfirmed=PhysicsProfile&&PhysicsProfile.Confirmed;
            actor.motor.ContactForceNewtons=PhysicsProfile?PhysicsProfile.ForceNewtons:0;
            actor.motor.RequiresContactProfile=true;
            actor.motor.ContactTargetOffset=transition.Pose.position-actor.motor.Body.position;
            actor.motor.MaximumContactLead=Mathf.Clamp(settings.size*.01f,.02f,.5f);
            physical.gameObject.SetActive(true);physical.SetTarget(transition.Pose,transition.Strength);
            physical.NetworkPreview=NetworkPreview;
            if(NetworkPreview&&pendingAuthoritativeBones!=null&&Time.unscaledTime-authoritativeAt<.5f&&!physical.HasFreshAuthoritativePreview)
                physical.ReceiveAuthoritativePreview(pendingAuthoritativeBones,authoritativeAt);
            if(!physical.IsInteractive&&Mathf.Abs(physical.GeometrySize-settings.size)>.001f)physical.RebuildInactive(transition.Pose);
            // No reset on reacquisition: the dynamic hand returns through bounded drives.
            physical.SetInteractive(transition.Strength>0&&!EffectiveInputBlocked);
            actor.motor.ContactPalm=physical.IsInteractive?physical.bodies[0]:null;
            physical.DesktopRecoveryOnly=desktop&&!external&&!NetworkPreview&&!EffectiveInputBlocked&&physical.ActivationBlocked;
            physical.skin.enabled=physical.IsInteractive||(EffectiveInputBlocked&&transition.Strength>0);
            CandyHandLook.For(physical).Apply(Look);
            if(transition.Mode==CandyHandMode.Inactive){physical.gameObject.SetActive(false);}
        }
        public void SelectDesktopPose(DesktopHandPose value) => DesktopPose = value;
        void ReadDesktopPose()
        {
            bool input=actor.motor.DesktopInputAllowed;
            if(input)
            {
                var keyboard=Keyboard.current;
                if(keyboard!=null)
                {
                    if(keyboard.digit1Key.wasPressedThisFrame||keyboard.numpad1Key.wasPressedThisFrame)DesktopPose=DesktopHandPose.Pointer;
                    if(keyboard.digit2Key.wasPressedThisFrame||keyboard.numpad2Key.wasPressedThisFrame)DesktopPose=DesktopHandPose.Cup;
                    if(keyboard.digit3Key.wasPressedThisFrame||keyboard.numpad3Key.wasPressedThisFrame)DesktopPose=DesktopHandPose.Fist;
                    if(keyboard.digit4Key.wasPressedThisFrame||keyboard.numpad4Key.wasPressedThisFrame)DesktopPose=DesktopHandPose.Flat;
                    if(keyboard.digit5Key.wasPressedThisFrame||keyboard.numpad5Key.wasPressedThisFrame)DesktopPose=DesktopHandPose.Peace;
                    if(keyboard.digit6Key.wasPressedThisFrame||keyboard.numpad6Key.wasPressedThisFrame)DesktopPose=DesktopHandPose.Spread;
                }
            }
            bool held=input && Mouse.current!=null && Mouse.current.leftButton.isPressed;
            DesktopClench=Mathf.MoveTowards(DesktopClench,held?1:0,Time.deltaTime*5);
            var goal=DesktopHandPoses.Build(useLeftHand,DesktopPose,DesktopClench);
            desktopSkeleton=desktopPoseReady?DesktopHandPoses.Blend(desktopSkeleton,goal,1-Mathf.Exp(-Time.deltaTime*12)):goal;
            desktopPoseReady=true;
        }
        // Respawn/rebuild only: release contacts before discarding the old rig. Never moves a held block.
        public void ResetExternalRig()
        {
            if(!actor.motor.externallyDriven)return;
            ResetTrackingRig();
        }
        public void ResetTrackingRig()
        {
            flightGesture.Reset();
            pendingAuthoritativeBones=null;authoritativeAt=-100;
            actor.motor.ContactPalm=null;
            if(physical){physical.SetInteractive(false);physical.gameObject.SetActive(false);Destroy(physical.gameObject);physical=null;}
            transition=new CandyHandTransition();trackedGeometry=false;anchored=false;
        }
        // Hand design: the player's pick, or a random one for an AI hand.
        int botLook=-1;
        HandTaunts taunts;
        // Network byte: low 4 bits = design, high 4 bits = taunt showing (index + 1).
        int NetworkLook{get{if(!taunts){taunts=GetComponent<HandTaunts>();if(!taunts)taunts=gameObject.AddComponent<HandTaunts>();}return (Look&0x0F)|(taunts.NetworkBits<<4);}}
        public int Look{get{if(!actor||!actor.motor.externallyDriven)return HandLooks.Chosen;if(botLook<0)botLook=Random.Range(1,CandyHandDesigns.Count);return botLook;}}
        // INetworkActor: what other players see of this hand (the player's or a bot's).
        public string NetworkKind => NetworkKinds.Hand;
        public bool NetworkActive => Spirit&&Spirit.Active || (physical ? physical.gameObject.activeInHierarchy && physical.skin && physical.skin.enabled : actor && !actor.motor.externallyDriven);
        public Vector3 NetworkPosition => Spirit&&Spirit.Active?Spirit.BustAnchor.position:physical ? physical.bodies[0].position : actor ? actor.motor.transform.position : transform.position;
        public void WriteState(BinaryWriter w)
        {
            var spirit=Spirit;
            if(spirit&&spirit.Active)
            {
                var restSea=new CandyHandPose{position=spirit.BustAnchor.position,rotation=spirit.BustAnchor.rotation,tracked=false,skeleton=CandyHandSkeleton.Rest(useLeftHand)};
                CharacterWire.WriteHand(w,useLeftHand,settings.size,restSea,(Pose[])null, NetworkLook);
                CharacterWire.WriteSeaSpirit(w,spirit);return;
            }
            if (physical) { CharacterWire.WriteHand(w, physical.left, physical.GeometrySize, physical.Target, physical.bodies, NetworkLook); return; }
            var at = actor ? actor.motor.transform : transform;
            var rest = new CandyHandPose { position = at.position, rotation = at.rotation, tracked = true, skeleton = CandyHandSkeleton.Rest(useLeftHand) };
            CharacterWire.WriteHand(w, useLeftHand, settings ? settings.size : 20, rest, (Pose[])null, NetworkLook);
        }
        bool PreferredReturnIsClear()
        {
            if(!physical||!physical.IsInteractive)return true;
            float now=Time.unscaledTime;
            if(now>=nextPreferredClearCheck||preferredClearSince>=0&&now-preferredClearSince>=settings.stableTime)
            {
                nextPreferredClearCheck=now+.1f;
                if(!physical.ClearForAutomaticSideSwitch()){preferredClearSince=-1;return false;}
                if(preferredClearSince<0)preferredClearSince=now;
            }
            return preferredClearSince>=0&&now-preferredClearSince>=settings.stableTime;
        }
        bool QueueDesktopRecovery(Vector3 delta)
        {
            return actor&&actor.motor.DesktopInputAllowed&&physical&&physical.DesktopRecoveryOnly&&
                !EffectiveInputBlocked&&physical.QueueInactiveRecovery(delta);
        }
        void OnEnable(){actor.motor.ContactRecoveryMove=QueueDesktopRecovery;if(!SuppressNetworkRegistration&&!NetworkActors.Local.Contains(this))NetworkActors.Local.Add(this);if(!GetComponent<HandTaunts>())gameObject.AddComponent<HandTaunts>();}
        void OnDisable(){PreferredHandReturnPending=false;preferredClearSince=-1;selectedMissing=alternateStable=0;preferenceInitialized=false;actor.motor.ContactRecoveryMove=null;spectatorInputHeld=false;if(actor&&actor.motor)actor.motor.ContactPalm=null;NetworkActors.Local.Remove(this);if(physical){physical.SetInteractive(false);physical.gameObject.SetActive(false);}if(pointerColliders!=null)Pointer(true);if(actor&&actor.follow){actor.follow.target=actor.motor.transform.Find("CameraFollowTarget");actor.follow.positionTarget=null;}anchored=false;}
        void OnDestroy(){if(physical)Destroy(physical.gameObject);}
    }
}

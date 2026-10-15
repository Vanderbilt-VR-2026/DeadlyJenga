using System.Collections.Generic;
using FlyingHand;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class CastleSurvivor : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform cameraOffset;
        public ControlMode controlMode = ControlMode.Auto;
        public float walkSpeed = 4.6f, gravity = 18, jumpHeight = 1.82f;
        [Header("Slope traversal")]
        [Tooltip("Full traction below this angle; steeper surfaces progressively slide.")]
        [Range(10,55)] public float slopeGripAngle = 38;
        [Tooltip("Maximum foot-support angle. Near-vertical faces never grant a jump.")]
        [Range(55,80)] public float slopeSupportAngle = 75;
        public float slopeSlideSpeed = 7, slopeSlideAcceleration = 14;
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float GroundAngle => Vector3.Angle(GroundNormal,Vector3.up);
        public bool Sliding => Grounded && slopeVelocity.sqrMagnitude > .04f;
        public float eyeHeight = 1.6f, mouseSensitivity = .12f;
        [Tooltip("Body and tracked-head size relative to a 1.7 metre survivor. Does not scale jump height or movement speed.")]
        [Range(.1f,2f)] public float playerScale = .5f;
        public float StandingHeight => 1.7f * playerScale;
        public float vaultReach = .65f, vaultMaxRise = 1.25f, vaultDuration = .65f;
        public float VaultProgress=>Vaulting?Mathf.Clamp01(vaultTime/vaultDuration):0;
        public bool InputSuspended { get; set; }
        [HideInInspector] public bool externallyDriven;
        public DeadlyJenga.CastleBots.ICastleSurvivorDriver InputDriver { get; set; }
        public bool IsVR { get; private set; }
        public bool Grounded { get; private set; }
        public bool Vaulting { get; private set; }
        public float VerticalSpeed => verticalSpeed;
        public int VaultCount { get; private set; }
        public string ActionStatus { get; private set; } = "Ready";
        public CharacterController Capsule { get; private set; }
        readonly List<UnityEngine.XR.XRDisplaySubsystem> displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        InputAction move, turn, jump;
        TrackedPoseDriver tracker;
        float verticalSpeed, pitch, lastGrounded = -100, airborneSince;
        bool jumped, modeReady, turnReady = true;
        float appliedScale;
        Transform support, vaultSurface;
        Vector3 supportLocal, supportWorld, vaultLocal, vaultStart;
        Vector3 slopeVelocity;
        Vector3 candidateGrip,candidateNormal,candidateForward,vaultGripLocal,vaultNormalLocal,vaultForwardLocal;
        DeadlyJenga.CandyPlayers.CandyTrackingSource candyTracking;
        float vaultTime;

        void Awake()
        {
            Capsule = GetComponent<CharacterController>();
            gameObject.layer = CastleBlock.SurvivorLayer;
            Capsule.excludeLayers = 1 << CastleBlock.BlockLayer;
            tracker = viewCamera.GetComponent<TrackedPoseDriver>();
            headActions=ConfigureHeadTracking(tracker);
            move = new InputAction("Walk", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            turn = new InputAction("Snap turn", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
            jump = new InputAction("Jump or vault", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        }
        void OnEnable() { if(!externallyDriven){move.Enable(); turn.Enable(); jump.Enable();} modeReady = false; }
        void OnDisable() { move.Disable(); turn.Disable(); jump.Disable(); support = null; Vaulting = false; ResetClimb(false); }
        void OnDestroy() { if(tracker)tracker.enabled=false;foreach(var action in headActions)action.Dispose();move.Dispose(); turn.Dispose(); jump.Dispose(); }

        public void RefreshMode()
        {
            UnityEngine.SubsystemManager.GetSubsystems(displays);
            bool running = displays.Exists(d => d.running);
            bool vr = !externallyDriven && (controlMode == ControlMode.VR || (controlMode == ControlMode.Auto && running));
            playerScale = Mathf.Clamp(playerScale,.1f,2f);
            if (modeReady && vr == IsVR && Mathf.Approximately(appliedScale,playerScale)) return;
            bool wasVR=IsVR;modeReady = true; IsVR = vr;
            appliedScale = playerScale;
            Capsule.radius = .24f * playerScale;
            Capsule.height = StandingHeight; Capsule.center = Vector3.up * StandingHeight * .5f;
            Capsule.stepOffset = .05f * playerScale; Capsule.skinWidth = .015f * playerScale;
            Capsule.slopeLimit = slopeSupportAngle;
            tracker.enabled = vr;
            float scaledEye = eyeHeight * playerScale;
            cameraOffset.localScale = Vector3.one * (vr ? playerScale : 1f);
            cameraOffset.localPosition = vr ? new Vector3(0, scaledEye, 0) : Vector3.zero;
            // XROrigin handles floor/device origin offsets in VR. Desktop owns its eye pose.
            var origin = GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if (origin) { origin.enabled = vr; origin.CameraYOffset = scaledEye; }
            if(!vr){viewCamera.transform.localPosition=Vector3.up*scaledEye;viewCamera.transform.localRotation=Quaternion.identity;pitch=0;}
            else if(!wasVR){viewCamera.transform.localPosition=Vector3.zero;viewCamera.transform.localRotation=Quaternion.identity;}
        }
        void Update()
        {
            RefreshMode();
            if (IsVR)
            {
                Vector3 head = transform.InverseTransformPoint(viewCamera.transform.position);
                Capsule.height = Mathf.Clamp(head.y + .1f * playerScale, .7f * playerScale, 2.4f * playerScale);
                Capsule.center = new Vector3(head.x, Capsule.height / 2, head.z);
            }
            else
            {
                if (Mathf.Abs(Capsule.height - StandingHeight) > .001f) Capsule.height = StandingHeight;
                if ((Capsule.center - Vector3.up * StandingHeight * .5f).sqrMagnitude > .000001f) Capsule.center = Vector3.up * StandingHeight * .5f;
            }
            Vector2 walking = Vector2.zero;
            bool pressed = false,held=false;
            if(externallyDriven)
            {
                var command=!InputSuspended&&InputDriver!=null?InputDriver.ReadCommand(this,Time.deltaTime):default;
                Vector3 facing=Vector3.ProjectOnPlane(command.look.sqrMagnitude>.001f?command.look:command.move,Vector3.up);
                if(facing.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),360*Time.deltaTime);
                Vector3 f=Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized;
                walking=new Vector2(Vector3.Dot(command.move,Vector3.Cross(Vector3.up,f)),Vector3.Dot(command.move,f));pressed=command.jump;held=command.climbHeld;
            }
            else if (!InputSuspended)
            {
                if (IsVR)
                {
                    walking = move.ReadValue<Vector2>();
                    if (walking.magnitude < .18f) walking = Vector2.zero;
                    ApplyVRTurn(turn.ReadValue<Vector2>().x,Time.deltaTime);
                    pressed = jump.WasPressedThisFrame();
                    held=jump.IsPressed();
                    if(!candyTracking)candyTracking=GetComponent<DeadlyJenga.CandyPlayers.CandyTrackingSource>();
                    if(candyTracking&&candyTracking.HasHandTracking)
                    {if(candyTracking.LeftPinching)walking=Vector2.up;pressed|=candyTracking.RightPinchPressed;held|=candyTracking.RightPinching;}
                }
                else if (Cursor.lockState == CursorLockMode.Locked)
                {
                    var k = Keyboard.current; var m = Mouse.current;
                    if (k != null)
                    {
                        walking = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                            (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                        pressed = k.spaceKey.wasPressedThisFrame;
                        held=k.spaceKey.isPressed;
                    }
                    if (m != null)
                    {
                        Vector2 delta = m.delta.ReadValue() * mouseSensitivity;
                        transform.Rotate(0, delta.x, 0);
                        pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
                        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                    }
                }
            }
            Simulate(walking, pressed, Time.deltaTime,held);
            UpdateClimbHud();
        }

        public void Teleport(Vector3 feet, Quaternion rotation)
        {
            Capsule.enabled = false; transform.SetPositionAndRotation(feet, rotation);
            Physics.SyncTransforms(); Capsule.enabled = true; Physics.SyncTransforms();
            verticalSpeed = 0; Grounded = false; Vaulting = false; jumped = false; support = null; slopeVelocity = Vector3.zero; GroundNormal = Vector3.up;
            lastGrounded = -100; ActionStatus = "Spawned on top";
            ResetClimb(true);
        }
        public void Simulate(Vector2 input, bool jumpPressed, float dt,bool climbHeld=false)
        {
            if (dt <= 0 || !Capsule.enabled) return;
            Physics.SyncTransforms();
            if(UpdateClimb(Vector2.ClampMagnitude(input,1),climbHeld&&!InputSuspended,dt))return;
            if (Vaulting) { AdvanceVault(dt); return; }
            if (support && Grounded)
            {
                Vector3 next = support.TransformPoint(supportLocal);
                Vector3 carry = next - supportWorld;
                if (carry.magnitude < 2) Capsule.Move(carry);
            }
            ProbeGround();
            if (Grounded)
            {
                lastGrounded = Time.time; jumped = false;
                if (verticalSpeed < 0) verticalSpeed = -1;
            }
            if (jumpPressed)
            {
                if (!jumped && (Grounded || Time.time - lastGrounded < .07f))
                {
                    verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight); Grounded = false;
                    jumped = true; support = null; airborneSince = Time.time; ActionStatus = "Jump";
                }
                else if (!Grounded && Time.time - airborneSince > .06f && TryStartVault()) return;
                else ActionStatus = "No reachable ledge — no double jump";
            }
            Vector3 forward = MovementForward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 movement=(forward*input.y+right*input.x)*walkSpeed;
            if(Grounded)
            {
                float steepness=Mathf.SmoothStep(0,1,Mathf.InverseLerp(slopeGripAngle,slopeSupportAngle,GroundAngle));
                Vector3 downhill=Vector3.ProjectOnPlane(Vector3.down,GroundNormal).normalized;
                movement=Vector3.ProjectOnPlane(movement,GroundNormal);
                // Preserve control across the slope, gradually reduce uphill traction.
                float uphill=Vector3.Dot(movement,-downhill);
                if(uphill>0)movement+=downhill*(uphill*steepness*.85f);
                slopeVelocity=Vector3.MoveTowards(Vector3.ProjectOnPlane(slopeVelocity,GroundNormal),downhill*(slopeSlideSpeed*steepness),
                    (steepness>.001f?slopeSlideAcceleration:24)*dt);
                if(steepness>.05f)ActionStatus="Sliding — jump to escape";
            }
            else
            {
                slopeVelocity=Vector3.MoveTowards(Vector3.ProjectOnPlane(slopeVelocity,Vector3.up),Vector3.zero,1.5f*dt);
            }
            float fall = verticalSpeed * dt - .5f * gravity * dt * dt;
            var flags = Capsule.Move((movement+slopeVelocity)*dt + Vector3.up * fall);
            verticalSpeed -= gravity * dt;
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0) verticalSpeed = -1;
            ProbeGround();
        }
        void ProbeGround()
        {
            Vector3 feet = Feet;
            bool wasGrounded = Grounded;
            int mask=~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer));
            bool contact=Physics.SphereCast(feet+Vector3.up*(Capsule.radius+.14f*playerScale),Capsule.radius*.9f,
                Vector3.down,out var hit,.24f*playerScale,mask,QueryTriggerInteraction.Ignore);
            // Sphere normals can interpolate across box edges. Recover the actual face
            // at the contact point, rather than classifying a wall edge as a floor.
            if(contact&&Physics.Raycast(hit.point+Vector3.up*(.06f*playerScale),Vector3.down,out var face,.12f*playerScale,mask,QueryTriggerInteraction.Ignore)&&face.collider==hit.collider)hit=face;
            Grounded=verticalSpeed<=0&&contact&&hit.normal.y>=Mathf.Cos(slopeSupportAngle*Mathf.Deg2Rad);
            if (Grounded)
            {
                GroundNormal=hit.normal;
                support = hit.transform; supportLocal = support.InverseTransformPoint(feet); supportWorld = support.TransformPoint(supportLocal);
            }
            else { support = null; if (wasGrounded) airborneSince = Time.time; }
        }
        Vector3 Feet => transform.TransformPoint(Capsule.center) - Vector3.up * Capsule.height / 2;

        public bool TryFindLedge(out RaycastHit top, out Vector3 target,Vector3? facingOverride=null)
        {
            top = default; target = default;
            Vector3 feet = Feet;
            Vector3 forward = Vector3.ProjectOnPlane(facingOverride??viewCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) return false;
            // Look for a solid vertical face first: empty air is never a vault target.
            for (float probeHeight = .12f; probeHeight <= 1.15f; probeHeight += .25f)
            {
                if (!Physics.Raycast(feet + Vector3.up * probeHeight, forward, out var face,
                    vaultReach, ~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer)), QueryTriggerInteraction.Ignore) || Mathf.Abs(face.normal.y) > .35f) continue;
                Vector3 inside = face.point + forward * (Capsule.radius + .12f);
                Vector3 above = new Vector3(inside.x, feet.y + vaultMaxRise + .15f, inside.z);
                if (!Physics.Raycast(above, Vector3.down, out top, vaultMaxRise + .3f, ~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer)), QueryTriggerInteraction.Ignore)
                    || top.collider != face.collider || top.normal.y < .85f) continue;
                float rise = top.point.y - feet.y;
                if (rise < .08f || rise > vaultMaxRise) continue;
                Vector3 destinationFeet = top.point + Vector3.up * .04f;
                if (!ClearCapsule(destinationFeet)) continue;
                // Check both lift and traverse segments, including low ceilings.
                Vector3 lifted = new Vector3(feet.x, destinationFeet.y, feet.z);
                if (!ClearPath(feet + Vector3.up * .04f, lifted) || !ClearPath(lifted, destinationFeet)) continue;
                target = destinationFeet - (Feet - transform.position);
                candidateGrip=new Vector3(face.point.x,top.point.y,face.point.z);candidateNormal=top.normal;candidateForward=forward;
                if(CastleBlock.TryGetSurfaceOwner(top.collider,out var owner)&&owner.visualRoot)
                {
                    float maxY=float.NegativeInfinity;
                    foreach(var mesh in owner.visualRoot.GetComponentsInChildren<MeshFilter>())
                    {
                        if(!mesh.sharedMesh)continue;
                        var bounds=mesh.sharedMesh.bounds;var matrix=owner.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                        for(int i=0;i<8;i++)maxY=Mathf.Max(maxY,matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))).y);
                    }
                    if(!float.IsNegativeInfinity(maxY))
                    {var box=owner.GetComponent<BoxCollider>();candidateGrip+=top.normal*((maxY-box.center.y-box.size.y*.5f)*owner.transform.lossyScale.y);}
                }
                return true;
            }
            return false;
        }
        bool ClearCapsule(Vector3 feet)
        {
            float r = Capsule.radius * .95f;
            return !Physics.CheckCapsule(feet + Vector3.up * (r + .03f), feet + Vector3.up * (Capsule.height - r),
                r, ~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer)), QueryTriggerInteraction.Ignore);
        }
        bool ClearPath(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start; float r = Capsule.radius * .95f;
            return delta.magnitude < .001f || !Physics.CapsuleCast(start + Vector3.up * (r + .03f),
                start + Vector3.up * (Capsule.height - r), r, delta.normalized, delta.magnitude,
                ~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer)), QueryTriggerInteraction.Ignore);
        }
        public bool TryStartVault()
        {
            if (Grounded || Vaulting || !TryFindLedge(out var hit, out var destination)) return false;
            BeginVault(hit,destination);return true;
        }
        void BeginVault(RaycastHit hit,Vector3 destination)
        {
            vaultSurface = hit.transform; vaultLocal = vaultSurface.InverseTransformPoint(destination);
            vaultGripLocal=vaultSurface.InverseTransformPoint(candidateGrip);
            vaultNormalLocal=vaultSurface.InverseTransformDirection(candidateNormal);vaultForwardLocal=vaultSurface.InverseTransformDirection(candidateForward);
            vaultStart = transform.position; vaultTime = 0; Vaulting = true; verticalSpeed = 0;
            support = null; ActionStatus = "Vault"; VaultCount++;
        }
        public bool TryGetVaultGrip(bool left,out Pose grip)
        {
            grip=default;if(!Vaulting||!vaultSurface)return false;
            Vector3 normal=vaultSurface.TransformDirection(vaultNormalLocal).normalized,forward=vaultSurface.TransformDirection(vaultForwardLocal).normalized;
            Vector3 right=Vector3.Cross(normal,forward).normalized;
            Vector3 point=vaultSurface.TransformPoint(vaultGripLocal)+right*(left?-.14f:.14f)*playerScale+forward*Mathf.Max(.14f,Capsule.radius+.04f)+normal*(.025f*playerScale);
            grip=new Pose(point,Quaternion.LookRotation(forward,normal));return true;
        }
        void AdvanceVault(float dt)
        {
            if (!vaultSurface || Vector3.Angle(vaultSurface.up, Vector3.up) > 25)
            { Vaulting = false; ActionStatus = "Ledge moved — vault cancelled"; return; }
            Vector3 end = vaultSurface.TransformPoint(vaultLocal);
            if (Vector3.Distance(end, vaultStart) > 3 || !ClearCapsule(end + (Feet - transform.position)))
            { Vaulting = false; return; }
            vaultTime += dt;
            float t = Mathf.Clamp01(vaultTime / vaultDuration);
            // Lift above the lip first, then move across it. CharacterController sweeps every step.
            Vector3 lifted = new Vector3(vaultStart.x, Mathf.Max(vaultStart.y, end.y) + .1f, vaultStart.z);
            Vector3 desired=t<.18f?vaultStart:t<.65f?Vector3.Lerp(vaultStart,lifted,Mathf.SmoothStep(0,1,(t-.18f)/.47f)):
                Vector3.Lerp(lifted,end,Mathf.SmoothStep(0,1,(t-.65f)/.35f));
            Capsule.Move(desired - transform.position);
            if (t >= 1) { Vaulting = false; verticalSpeed = -1; ActionStatus = "Vault complete"; }
        }
    }
}



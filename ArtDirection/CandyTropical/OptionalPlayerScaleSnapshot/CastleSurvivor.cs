using System.Collections.Generic;
using FlyingHand;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CastleSurvivor : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform cameraOffset;
        public ControlMode controlMode = ControlMode.Auto;
        public float walkSpeed = 4.6f, gravity = 18, jumpHeight = 2.03f;
        public float eyeHeight = 1.6f, mouseSensitivity = .12f;
        [Tooltip("Body and tracked-head size relative to a 1.7 metre survivor. Does not scale jump height or movement speed.")]
        [Range(.1f,2f)] public float playerScale = .5f;
        public float StandingHeight => 1.7f * playerScale;
        public float vaultReach = 1.05f, vaultMaxRise = 1.25f, vaultDuration = .28f;
        public bool InputSuspended { get; set; }
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
        float vaultTime;

        void Awake()
        {
            Capsule = GetComponent<CharacterController>();
            gameObject.layer = CastleBlock.SurvivorLayer;
            Capsule.excludeLayers = 1 << CastleBlock.BlockLayer;
            tracker = viewCamera.GetComponent<TrackedPoseDriver>();
            move = new InputAction("Walk", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            turn = new InputAction("Snap turn", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
            jump = new InputAction("Jump or vault", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        }
        void OnEnable() { move.Enable(); turn.Enable(); jump.Enable(); modeReady = false; }
        void OnDisable() { move.Disable(); turn.Disable(); jump.Disable(); support = null; Vaulting = false; }
        void OnDestroy() { move.Dispose(); turn.Dispose(); jump.Dispose(); }

        public void RefreshMode()
        {
            UnityEngine.SubsystemManager.GetSubsystems(displays);
            bool running = displays.Exists(d => d.running);
            bool vr = controlMode == ControlMode.VR || (controlMode == ControlMode.Auto && running);
            playerScale = Mathf.Clamp(playerScale,.1f,2f);
            if (modeReady && vr == IsVR && Mathf.Approximately(appliedScale,playerScale)) return;
            modeReady = true; IsVR = vr;
            appliedScale = playerScale;
            Capsule.radius = .24f * playerScale;
            Capsule.height = StandingHeight; Capsule.center = Vector3.up * StandingHeight * .5f;
            Capsule.stepOffset = .05f * playerScale; Capsule.skinWidth = .015f * playerScale;
            tracker.enabled = vr;
            float scaledEye = eyeHeight * playerScale;
            cameraOffset.localScale = Vector3.one * (vr ? playerScale : 1f);
            cameraOffset.localPosition = vr ? new Vector3(0, scaledEye, 0) : Vector3.zero;
            // XROrigin handles floor/device origin offsets in VR. Desktop owns its eye pose.
            var origin = GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if (origin) { origin.enabled = vr; origin.CameraYOffset = scaledEye; }
            viewCamera.transform.localPosition = vr ? Vector3.zero : Vector3.up * scaledEye;
            viewCamera.transform.localRotation = Quaternion.identity; pitch = 0;
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
            bool pressed = false;
            if (!InputSuspended)
            {
                if (IsVR)
                {
                    walking = move.ReadValue<Vector2>();
                    if (walking.magnitude < .18f) walking = Vector2.zero;
                    float x = turn.ReadValue<Vector2>().x;
                    if (Mathf.Abs(x) < .3f) turnReady = true;
                    if (Mathf.Abs(x) > .7f && turnReady)
                    { transform.RotateAround(viewCamera.transform.position, Vector3.up, Mathf.Sign(x) * 30); turnReady = false; }
                    pressed = jump.WasPressedThisFrame();
                }
                else if (Cursor.lockState == CursorLockMode.Locked)
                {
                    var k = Keyboard.current; var m = Mouse.current;
                    if (k != null)
                    {
                        walking = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                            (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                        pressed = k.spaceKey.wasPressedThisFrame;
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
            Simulate(walking, pressed, Time.deltaTime);
        }

        public void Teleport(Vector3 feet, Quaternion rotation)
        {
            Capsule.enabled = false; transform.SetPositionAndRotation(feet, rotation);
            Physics.SyncTransforms(); Capsule.enabled = true; Physics.SyncTransforms();
            verticalSpeed = 0; Grounded = false; Vaulting = false; jumped = false; support = null;
            lastGrounded = -100; ActionStatus = "Spawned on top";
        }
        public void Simulate(Vector2 input, bool jumpPressed, float dt)
        {
            if (dt <= 0 || !Capsule.enabled) return;
            Physics.SyncTransforms();
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
            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            input = Vector2.ClampMagnitude(input, 1);
            float fall = verticalSpeed * dt - .5f * gravity * dt * dt;
            var flags = Capsule.Move((forward * input.y + right * input.x) * (walkSpeed * dt) + Vector3.up * fall);
            verticalSpeed -= gravity * dt;
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0) verticalSpeed = -1;
            ProbeGround();
        }
        void ProbeGround()
        {
            Vector3 feet = Feet;
            bool wasGrounded = Grounded;
            Grounded = verticalSpeed <= 0 && Physics.SphereCast(feet + Vector3.up * (.3f * playerScale), Capsule.radius * .85f,
                Vector3.down, out var hit, .18f * playerScale, ~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer)), QueryTriggerInteraction.Ignore) && hit.normal.y > .7f;
            if (Grounded)
            {
                // A second ray avoids retaining a stale moving platform when stepping away.
                if (Physics.Raycast(feet + Vector3.up * (.15f * playerScale), Vector3.down, out hit, .3f * playerScale, ~((1 << gameObject.layer) | (1 << CastleBlock.BlockLayer)), QueryTriggerInteraction.Ignore))
                { support = hit.transform; supportLocal = support.InverseTransformPoint(feet); supportWorld = support.TransformPoint(supportLocal); }
            }
            else { support = null; if (wasGrounded) airborneSince = Time.time; }
        }
        Vector3 Feet => transform.TransformPoint(Capsule.center) - Vector3.up * Capsule.height / 2;

        public bool TryFindLedge(out RaycastHit top, out Vector3 target)
        {
            top = default; target = default;
            Vector3 feet = Feet;
            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
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
            vaultSurface = hit.transform; vaultLocal = vaultSurface.InverseTransformPoint(destination);
            vaultStart = transform.position; vaultTime = 0; Vaulting = true; verticalSpeed = 0;
            support = null; ActionStatus = "Vault"; VaultCount++; return true;
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
            Vector3 desired = t < .5f ? Vector3.Lerp(vaultStart, lifted, t * 2) : Vector3.Lerp(lifted, end, (t - .5f) * 2);
            Capsule.Move(desired - transform.position);
            if (t >= 1) { Vaulting = false; verticalSpeed = -1; ActionStatus = "Vault complete"; }
        }
    }
}



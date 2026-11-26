using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // PC-only camera feel: a dip when landing, a short shake on hard landings, a gentle head bob
    // while walking and a small sway as each hand plants while climbing.
    // Only the rendered view is offset (the camera and the player never move), and it is off in VR,
    // where moving the view without the head causes motion sickness.
    // Players can turn it off with PlayerPrefs "CandyCamera.Motion" = 0.
    [DisallowMultipleComponent]
    public sealed class CandyDesktopCameraMotion : MonoBehaviour
    {
        public const string PrefKey = "CandyCamera.Motion";
        IMovementState motor;
        IPlayerView view;
        bool wasGrounded = true, wasClimbing;
        float fallSpeed, dip, dipVelocity, shake, bobPhase, bobWeight, sway, swayVelocity;
        readonly Vector3[] gripLast = new Vector3[2];
        readonly bool[] gripMoving = new bool[2];
        Camera shifted;
        Vector3 offset, lastPosition;
        Quaternion tilt = Quaternion.identity;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            if (shifted) shifted.ResetWorldToCameraMatrix();
            shifted = null;
        }

        void LateUpdate()
        {
            motor ??= GetComponent<IMovementState>();
            view ??= GetComponent<IPlayerView>();
            float dt = Mathf.Min(Time.deltaTime, .05f);
            bool active = motor != null && view != null && view.ControlledHere && !view.IsVR &&
                !motor.InputPaused && PlayerPrefs.GetInt(PrefKey, 1) != 0;
            if (!active)
            {
                dip = dipVelocity = shake = bobWeight = sway = swayVelocity = 0;
                offset = Vector3.zero; tilt = Quaternion.identity;
                return;
            }

            float scale = Mathf.Max(.2f, motor.Scale);
            // Landing: remember the fastest fall, then kick the view down by how hard we hit.
            if (!motor.Grounded && !motor.Climbing) fallSpeed = Mathf.Max(fallSpeed, -motor.VerticalSpeed);
            if (motor.Grounded && !wasGrounded)
            {
                float hit = Mathf.Clamp01((fallSpeed - 2.5f) / 14f);
                dipVelocity -= hit * 1.1f;
                if (fallSpeed > 9f) shake = Mathf.Max(shake, Mathf.Clamp01((fallSpeed - 9f) / 12f));
            }
            if (motor.Grounded || motor.Climbing) fallSpeed = 0;
            wasGrounded = motor.Grounded;

            // A soft spring brings the dip back to rest.
            dipVelocity += (-dip * 90f - dipVelocity * 13f) * dt;
            dip += dipVelocity * dt;
            shake = Mathf.MoveTowards(shake, 0, dt * 3.2f);

            // Head bob only while walking or running on the ground.
            var moved = Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up);
            lastPosition = transform.position;
            float speed = moved.magnitude > 2f ? 0 : moved.magnitude / Mathf.Max(dt, .001f) / scale;
            bool walking = motor.Grounded && !motor.Climbing && !motor.Vaulting && speed > 1.2f;
            bobWeight = Mathf.MoveTowards(bobWeight, walking ? Mathf.Clamp01(speed / 7f) : 0, dt * 4f);
            bobPhase += speed * dt * 2.1f;

            // Climbing: each hand that plants nudges the view toward it.
            if (motor.Climbing)
            {
                for (int side = 0; side < 2; side++)
                {
                    if (!motor.TryGetGrip(side == 0, out var grip)) continue;
                    float handSpeed = (grip.position - gripLast[side]).magnitude / Mathf.Max(dt, .001f);
                    if (handSpeed > .1f) gripMoving[side] = true;
                    if (gripMoving[side] && handSpeed < .035f && wasClimbing)
                    {
                        gripMoving[side] = false;
                        swayVelocity += side == 0 ? -9f : 9f;
                        dipVelocity -= .25f;
                    }
                    gripLast[side] = grip.position;
                }
            }
            wasClimbing = motor.Climbing;
            swayVelocity += (-sway * 70f - swayVelocity * 11f) * dt;
            sway += swayVelocity * dt;

            float bobUp = Mathf.Abs(Mathf.Sin(bobPhase)) * .016f * bobWeight;
            float bobSide = Mathf.Sin(bobPhase) * .008f * bobWeight;
            float jitter = shake * shake;
            float t = Time.time * 38f;
            offset = new Vector3(bobSide, dip * .055f + bobUp - .008f * bobWeight, 0) * scale;
            tilt = Quaternion.Euler(-dip * 3.2f + (Mathf.PerlinNoise(t, 1.3f) - .5f) * 2.4f * jitter,
                (Mathf.PerlinNoise(2.1f, t) - .5f) * 1.6f * jitter,
                Mathf.Sin(bobPhase) * .35f * bobWeight + sway + (Mathf.PerlinNoise(t, 7.7f) - .5f) * 1.8f * jitter);
        }

        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (view == null || camera != view.ViewCamera || camera.stereoEnabled) return;
            if (offset.sqrMagnitude < 1e-8f && Quaternion.Angle(tilt, Quaternion.identity) < .01f) return;
            var eye = camera.transform.localToWorldMatrix * Matrix4x4.TRS(offset, tilt, Vector3.one);
            camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1)) * eye.inverse;
            shifted = camera;
        }

        void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != shifted) return;
            camera.ResetWorldToCameraMatrix();
            shifted = null;
        }
    }
}

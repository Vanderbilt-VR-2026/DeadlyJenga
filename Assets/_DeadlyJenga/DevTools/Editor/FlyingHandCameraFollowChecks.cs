using System;
using System.Collections.Generic;
using System.IO;
using FlyingHand;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit capture lifetime inside an existing QA scenario. Never drives input, poses or rendering.
    public sealed class FlyingHandCameraFollowChecks : IDisposable
    {
        [Serializable] public sealed class Sample
        {
            public int frame, cameraId;
            public float time, motorYaw, observerYaw, yawInput, pitchInput;
            public bool dual, inputSuspended, contactConfirmed;
            public float contactForce;
            public Vector3 motor, palm, observer, camera, cameraLocal;
            public Quaternion observerRotation, cameraLocalRotation;
        }
        [Serializable] sealed class Report
        {
            public string kind = "Actual active-camera render callbacks if executed; no headset proof";
            public string segment;
            public List<Sample> samples = new List<Sample>();
        }
        readonly ThirdPersonVRFollow follow;
        readonly Report report = new Report();
        bool disposed;

        public FlyingHandCameraFollowChecks(ThirdPersonVRFollow follow, string segment)
        {
            if (!follow) throw new ArgumentNullException(nameof(follow));
            this.follow = follow;
            report.segment = segment;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (disposed || !follow || !follow.isActiveAndEnabled || camera != follow.viewCamera ||
                !camera.isActiveAndEnabled || !follow.target || !follow.controller) return;
            if (report.samples.Count >= 12000) throw new InvalidOperationException("Flying camera capture exceeded its finite sample budget.");
            var motor = follow.controller;
            report.samples.Add(new Sample {
                frame = Time.frameCount, time = Time.unscaledTime, cameraId = camera.GetInstanceID(),
                motorYaw = follow.target.eulerAngles.y, observerYaw = follow.transform.eulerAngles.y,
                yawInput = motor.YawInput, pitchInput = motor.CameraPitchInput,
                dual = motor.DualControllerFlight, inputSuspended = motor.InputSuspended,
                contactConfirmed = motor.ContactRulesConfirmed, contactForce = motor.ContactForceNewtons,
                motor = follow.target.position, palm = follow.FollowPosition,
                observer = follow.transform.position, camera = camera.transform.position,
                cameraLocal = camera.transform.localPosition, observerRotation = follow.transform.rotation,
                cameraLocalRotation = camera.transform.localRotation
            });
        }
        public void CheckDualTurn(Action<bool,string> check, float minimumDegrees, int direction)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException(nameof(direction));
            float motorTravel = 0, observerTravel = 0, largestError = 0;
            Sample previous = null;
            int count = 0;
            foreach (var sample in report.samples)
            {
                if (!sample.dual) { previous = null; continue; }
                count++;
                largestError = Mathf.Max(largestError, Mathf.Abs(Mathf.DeltaAngle(sample.motorYaw, sample.observerYaw)));
                if (previous != null)
                {
                    motorTravel += Mathf.DeltaAngle(previous.motorYaw, sample.motorYaw);
                    observerTravel += Mathf.DeltaAngle(previous.observerYaw, sample.observerYaw);
                }
                previous = sample;
            }
            check(count >= 3, "Actual active-camera renders sampled dual-controller flight");
            check(direction * motorTravel >= minimumDegrees,
                "Production motor completed requested turn; inspect input/contact gates first if missing: " + motorTravel);
            check(direction * observerTravel >= minimumDegrees,
                "Rendered observer completed the same full turn: " + observerTravel);
            check(largestError <= .5f, "Dual-flight observer follows actual motor yaw without a second spring: " + largestError);
        }
        // Use a separate straight-movement segment with neutral look and a stationary tracked head.
        public void CheckTranslation(Action<bool,string> check, float minimumDistance)
        {
            check(report.samples.Count >= 3, "Actual render samples exist for translation");
            if (report.samples.Count < 3) return;
            var first = report.samples[0];
            var last = report.samples[report.samples.Count - 1];
            float palmTravel = Vector3.Distance(first.palm, last.palm);
            check(palmTravel >= minimumDistance, "Physical hand actually moved through the scene: " + palmTravel);
            check(Quaternion.Angle(first.observerRotation, last.observerRotation) < .5f &&
                Vector3.Distance(first.cameraLocal, last.cameraLocal) < .002f,
                "Translation oracle used neutral look and stationary tracked head");
            var palmDelta = last.palm - first.palm;
            var cameraDelta = last.camera - first.camera;
            check(Vector3.Distance(palmDelta, cameraDelta) <= .02f,
                "Rendered camera follows actual physical-hand translation within two centimetres");
        }
        public void Save(string reportName)
        {
            File.WriteAllText(ProjectPath.Report(reportName), JsonUtility.ToJson(report, true));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        }
    }
}

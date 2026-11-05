using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentPlaytest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class UprightPostureScenario : IPlaytestScenario
    {
        [Serializable] public sealed class Row
        {
            public string label, inputUpdate;
            public int frame, footVertices;
            public bool seatedFrame, grounded, floorFound;
            public Vector3 rawHead, consumedHead, feet, head, renderedHead, rootPosition, rootUp, leftAnkle, rightAnkle, leftHip, rightHip;
            public Quaternion rawRotation, consumedRotation;
            public float footMin, footMax, floorY, rootUpDot;
        }
        [Serializable] public sealed class Evidence
        {
            public string kind = "Stock XRI keyboard-driven synthetic HMD; actual TPD, avatar skin and Game runtime if executed. No physical Quest posture/comfort claim.";
            public string mvid = typeof(UprightPostureScenario).Assembly.ManifestModule.ModuleVersionId.ToString();
            public string meshMethod = "BakeMesh(useScale:false), renderer.TransformPoint once; foot vertices weighted >=0.5 to actual foot bones 10/13. Hip origins bones 8/11. No pose override.";
            public List<Row> rows = new List<Row>();
        }
        readonly Evidence evidence = new Evidence();
        SimulatorRig rig;
        Keyboard keyboard, previousKeyboard;
        XRSimulatedHMD hmd;
        CandyPlayerAvatar avatar;
        Camera exterior;
        PlaytestContext c;
        Row latest;
        int failures;
        void Check(bool value, string label)
        {
            c.Report.assertions.Add(new Assertion { passed = value, message = label, frame = Time.frameCount });
            if (!value) failures++;
        }
        IEnumerator Keys(int frames, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return c.WaitFrames(frames);
        }
        IEnumerator Sample(string label, bool assertions)
        {
            yield return ManagedFrameObserver.Observe(() =>
            {
                var model = avatar.Rig;
                c.Check(model && model.bodyRenderer && model.BoneCount >= 14, "Actual avatar rig and body skin exist");
                var frame = avatar.LastFrame;
                var row = new Row { label = label, frame = Time.frameCount,
                    inputUpdate = InputState.currentUpdateType.ToString(), rawHead = hmd.centerEyePosition.ReadValue(),
                    rawRotation = hmd.centerEyeRotation.ReadValue(), consumedHead = rig.Player.viewCamera.transform.localPosition,
                    consumedRotation = rig.Player.viewCamera.transform.localRotation, seatedFrame = frame.seated,
                    grounded = rig.Player.Grounded, feet = frame.feet, head = frame.head, renderedHead = model.HeadAnchor.position, rootPosition = model.transform.position, rootUp = model.transform.up,
                    leftAnkle = model.AppearanceBone(10).position, rightAnkle = model.AppearanceBone(13).position,
                    leftHip = model.AppearanceBone(8).position, rightHip = model.AppearanceBone(11).position,
                    footMin = float.PositiveInfinity, footMax = float.NegativeInfinity };
                row.rootUpDot = Vector3.Dot(row.rootUp, Vector3.up);
                if (assertions)
                {
                var baked = new Mesh();
                try
                {
                    model.bodyRenderer.BakeMesh(baked, false);
                    var vertices = baked.vertices;
                    var weights = model.bodyRenderer.sharedMesh.boneWeights;
                    c.Check(vertices.Length == weights.Length && vertices.Length > 0, "Baked full body vertices map to authored skin weights");
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i];
                        float footWeight = Foot(w.boneIndex0) * w.weight0 + Foot(w.boneIndex1) * w.weight1 +
                            Foot(w.boneIndex2) * w.weight2 + Foot(w.boneIndex3) * w.weight3;
                        if (footWeight < .5f) continue;
                        float y = model.bodyRenderer.transform.TransformPoint(vertices[i]).y;
                        row.footMin = Mathf.Min(row.footMin, y); row.footMax = Mathf.Max(row.footMax, y); row.footVertices++;
                    }
                }
                finally { Object.DestroyImmediate(baked); }
                var hits = Physics.RaycastAll(row.feet + Vector3.up * .2f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(x => !x.collider.transform.IsChildOf(rig.Player.transform) && x.normal.y > .6f)
                    .OrderBy(x => Mathf.Abs(x.point.y - row.feet.y)).ToArray();
                if (hits.Length > 0) { row.floorFound = true; row.floorY = hits[0].point.y; }
                }
                latest = row; evidence.rows.Add(row);
                if (!assertions) return;
                Check(Vector3.Distance(row.rawHead, row.consumedHead) < .001f && Quaternion.Angle(row.rawRotation, row.consumedRotation) < .2f,
                    label + ": actual TPD consumed stock HMD pose without direct camera write");
                Check(!row.seatedFrame, label + ": comfort profile leaves visual stance standing");
                Check(row.rootUpDot > .999f && Vector3.Distance(row.rootPosition, row.feet) < .001f, label + ": avatar root remains world upright under HMD tilt");
                float hipMin = Mathf.Min(row.leftHip.y, row.rightHip.y), hipMax = Mathf.Max(row.leftHip.y, row.rightHip.y);
                Check(row.footVertices > 20 && float.IsFinite(row.footMin) && float.IsFinite(row.footMax), label + ": actual deformed foot geometry measured");
                Check(Mathf.Max(row.leftAnkle.y, row.rightAnkle.y) < hipMin && row.footMax < hipMin && hipMax < Mathf.Min(row.head.y, row.renderedHead.y),
                    label + ": ankles and maximum rendered foot geometry below hips below actual tracked head");
                Check(row.grounded && row.floorFound && Mathf.Abs(row.feet.y - row.floorY) < .08f && Mathf.Abs(row.footMin - row.floorY) < .08f,
                    label + ": actual support surface agrees with actor feet and rendered soles within 0.08 world units");
            });
        }
        static float Foot(int bone) => bone == 10 || bone == 13 ? 1 : 0;
        static float Signed(float angle) => Mathf.DeltaAngle(0, angle);
        IEnumerator Angle(bool roll, float target)
        {
            yield return Sample("angle-start", false);
            int start = Time.frameCount;
            float wall = Time.realtimeSinceStartup;
            for (int i = 0; i < 100; i++)
            {
                float current = Signed(roll ? latest.rawRotation.eulerAngles.z : latest.rawRotation.eulerAngles.x);
                if (Mathf.Abs(Mathf.DeltaAngle(current, target)) <= 4) break;
                c.Check(Time.frameCount - start < 240 && Time.realtimeSinceStartup - wall < 20, "Bounded stock keyboard head rotation progresses");
                Key key = roll ? (current < target ? Key.LeftArrow : Key.RightArrow) : (current < target ? Key.DownArrow : Key.UpArrow);
                if (roll) yield return Keys(1, Key.Z, key); else yield return Keys(1, Key.V, key);
                yield return Keys(1);
                yield return Sample("keyboard-head-rotation", false);
            }
            yield return Keys(3);
            yield return Sample("angle-released", false);
            float actual = Signed(roll ? latest.rawRotation.eulerAngles.z : latest.rawRotation.eulerAngles.x);
            c.Check(Mathf.Abs(Mathf.DeltaAngle(actual, target)) <= 5, "Stock keyboard reached requested " + (roll ? "roll" : "pitch") + "=" + target + ", actual=" + actual);
        }
        IEnumerator Height(float target)
        {
            yield return Sample("height-start", false);
            float wall = Time.realtimeSinceStartup;
            for (int i = 0; i < 100 && Mathf.Abs(latest.rawHead.y - target) > .035f; i++)
            {
                c.Check(Time.realtimeSinceStartup - wall < 20, "Bounded stock keyboard head height progresses");
                yield return Keys(1, latest.rawHead.y < target ? Key.E : Key.Q);
                yield return Keys(1);
                yield return Sample("keyboard-head-height", false);
            }
            yield return Keys(3);
            yield return Sample("height-released", false);
            c.Check(Mathf.Abs(latest.rawHead.y - target) <= .05f, "Stock keyboard reached synthetic physical head height=" + target);
        }
        void Capture(string label, bool lookdown)
        {
            var model = avatar.Rig;
            var center = (latest.feet + latest.head) * .5f;
            float distance = Mathf.Max(.9f, Vector3.Distance(latest.feet, latest.head) * 2);
            exterior.transform.position = center - model.transform.forward * distance + model.transform.right * distance * .55f + Vector3.up * distance * .2f;
            exterior.transform.LookAt(center);
            c.Screenshot(exterior, label + "-exterior-full-body");
            if (lookdown) c.Screenshot(rig.Player.viewCamera, label + "-actual-head-lookdown");
        }
        public IEnumerator Run(PlaytestContext context)
        {
            c = context;
            yield return c.WaitUntil(() => Object.FindFirstObjectByType<CastlePlaySession>()?.Player != null && Services.World != null);
            previousKeyboard = Keyboard.current;
            try
            {
                rig = new SimulatorRig(false);
                keyboard = InputSystem.AddDevice<Keyboard>("Upright stock simulator keyboard");
                avatar = rig.Player.GetComponent<CandyPlayerAvatar>();
                c.Check(avatar, "Real survivor avatar exists; no avatar pose override is installed by fixture");
                yield return c.WaitFrames(6);
                hmd = InputSystem.devices.OfType<XRSimulatedHMD>().Single();
                rig.Simulator.targetedDeviceInput = TargetedDevices.HMD;
                // Public sensitivity configuration on this owned simulator; all poses still come from stock keyboard processing.
                rig.Simulator.rotateXSensitivity = 2; rig.Simulator.rotateYSensitivity = 2;
                exterior = new GameObject("Temporary upright evidence camera").AddComponent<Camera>();
                exterior.enabled = false; exterior.fieldOfView = 50; exterior.nearClipPlane = .02f; exterior.farClipPlane = 300;
                rig.Session.SetMenu(false);
                yield return c.WaitUntil(() => !rig.Player.InputSuspended && rig.Player.Grounded);
                rig.Session.SetMenu(true);
                foreach (bool seatedComfort in new[] { false, true })
                foreach (bool left in new[] { false, true })
                {
                    string label = (seatedComfort ? "seated-comfort" : "standing-comfort") + (left ? "-left" : "-right");
                    yield return Angle(true, 0); yield return Angle(false, 0);
                    yield return Height(seatedComfort ? 1.15f : 1.7f);
                    rig.Player.SetVRPlayStyle(seatedComfort ? SurvivorVRPlayStyle.Seated : SurvivorVRPlayStyle.Standing, false);
                    if (seatedComfort) rig.Player.CalibrateSeatedHeight();
                    PlayerPrefs.SetInt("CandyBlaster.LeftHanded", left ? 1 : 0);
                    yield return c.WaitFrames(4);
                    yield return Sample(label + "-neutral", true);
                    yield return Angle(false, 75); yield return Angle(true, 60);
                    yield return Sample(label + "-pitch75-roll60", true); Capture(label + "-pitch75-roll60", true);
                    yield return Angle(true, -60); yield return Angle(false, -75);
                    yield return Sample(label + "-pitchMinus75-rollMinus60", true);
                }
                rig.Session.SetRole(true); rig.SelectCamera(); yield return c.WaitFrames(4);
                c.Check(rig.Session.PlayingHand, "Normal role command entered flying hand");
                rig.Session.SetRole(false); rig.SelectCamera(); yield return c.WaitFrames(5);
                c.Check(!rig.Session.PlayingHand && rig.Session.ActiveCamera == rig.Player.viewCamera, "Normal hand-to-survivor command restores actual camera");
                yield return Sample("hand-to-survivor-restored", true); Capture("hand-to-survivor-restored", false);
            }
            finally
            {
                try { File.WriteAllText(Path.Combine(c.Plan.output, "upright-posture.json"), JsonUtility.ToJson(evidence, true)); }
                finally
                {
                    try { if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent(); }
                    finally
                    {
                        try { if (exterior) Object.DestroyImmediate(exterior.gameObject); }
                        finally
                        {
                            try { rig?.Dispose(); }
                            finally { c.Check(ManagedFrameObserver.SaveCleanup(Path.Combine(c.Plan.output, "upright-observer-cleanup.json")), "Owned posture observers/hooks removed while Play Mode remains active"); }
                        }
                    }
                }
            }
            c.Check(failures == 0, "All actual upright posture invariants passed; physical headset verification remains separate");
        }
    }
}

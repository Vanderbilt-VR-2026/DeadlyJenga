using System;
using System.IO;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class HandVisualRegression
    {
        static int assertions;
        static string Report => ProjectPath.Report("HandVisualRegression.txt");
        static readonly float[] TorsoHeights = { .53f, .58f, .66f, .79f, .94f, 1.08f, 1.2f, 1.29f, 1.34f };
        static readonly float[] TorsoRadii = { 0, .12f, .18f, .17f, .155f, .2f, .21f, .16f, 0 };

        static void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException(label);
            assertions++;
            File.AppendAllText(Report, "PASS " + label + "\n");
        }
        static bool InsideTorso(Vector3 point)
        {
            if (point.y <= TorsoHeights[0] || point.y >= TorsoHeights[TorsoHeights.Length - 1]) return false;
            int i = 0;
            while (i < TorsoHeights.Length - 2 && point.y > TorsoHeights[i + 1]) i++;
            float t = Mathf.InverseLerp(TorsoHeights[i], TorsoHeights[i + 1], point.y);
            float p0 = TorsoRadii[Mathf.Max(0, i - 1)], p1 = TorsoRadii[i];
            float p2 = TorsoRadii[i + 1], p3 = TorsoRadii[Mathf.Min(TorsoRadii.Length - 1, i + 2)];
            float radius = Mathf.Max(0, .5f * (2 * p1 + (-p0 + p2) * t +
                (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t));
            return radius > .01f && point.x * point.x / (radius * radius) +
                point.z * point.z / (radius * radius * .72f * .72f) < 1;
        }
        static int TorsoCrossings(CandyPlayerRig rig, Vector3 start, Vector3 end)
        {
            int count = 0;
            var torso = rig.BodyAnchor;
            for (int i = 1; i < 20; i++)
                if (InsideTorso(torso.InverseTransformPoint(Vector3.Lerp(start, end, i / 20f)))) count++;
            return count;
        }

        public static void ApplyPreviewPose(CandyPlayerRig rig, Vector3 leftWrist, float scale, bool seated, float curl, Quaternion? wristRotation = null)
        {
            Vector3 origin = rig.transform.position;
            var rotation = wristRotation ?? Quaternion.identity;
            var rightWrist = new Vector3(-leftWrist.x, leftWrist.y, leftWrist.z);
            rig.SeatedVisual = seated;
            var frame = new CandyAvatarFrame
            {
                feet = origin, head = origin + Vector3.up * 1.6f * scale,
                headRotation = Quaternion.identity, scale = scale, seated = seated,
                left = new CandyHandPose { tracked = true, skeletonTracked = true,
                    position = origin + leftWrist * scale, rotation = rotation,
                    skeleton = CandyHandSkeleton.Rest(true, curl) },
                right = new CandyHandPose { tracked = true, skeletonTracked = true,
                    position = origin + rightWrist * scale, rotation = rotation,
                    skeleton = CandyHandSkeleton.Rest(false, curl) }
            };
            rig.ApplyFrame(frame, true, 0);
            Check(Vector3.Distance(rig.LeftPalm.position, frame.left.position) < .0001f * scale &&
                Vector3.Distance(rig.RightPalm.position, frame.right.position) < .0001f * scale,
                "Native rig preserves both wrist targets at scale=" + scale + " seated=" + seated);
            Check(Quaternion.Angle(rig.LeftPalm.rotation, frame.left.rotation) < .01f &&
                Quaternion.Angle(rig.RightPalm.rotation, frame.right.rotation) < .01f,
                "Native rig preserves both wrist rotations");
        }

        [MenuItem("Tools/Deadly Jenga/Validation/Check hand visual scratch")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run hand visual scratch in Edit Mode");
            assertions = 0;
            File.WriteAllText(Report, "Explicit native Edit Mode scratch, actual generated production mesh and visual rig with synthetic poses. No gameplay input, network or headset.\n");
            HandTopologyRebake.GenerateScratch(true);
            File.AppendAllText(Report, HandTopologyRebake.LastAudit + "\n");
            var rig = HandTopologyRebake.LastPreviewRig;
            var before = HandTopologyRebake.LastUnchangedRig;
            Check(rig && before && HandTopologyRebake.LastCandidate && HandTopologyRebake.LastFlyingCandidate,
                "Body and flying-hand source/candidate scratch objects exist");
            var originalBindings = before.bodyRenderer.sharedMesh.bindposes;
            Check(HandTopologyRebake.DistalCrossDigitTriangles(rig.bodyRenderer.sharedMesh, 0, originalBindings[4]) == 0 &&
                HandTopologyRebake.DistalCrossDigitTriangles(rig.bodyRenderer.sharedMesh, 1, originalBindings[7]) == 0,
                "Both native regenerated body hands have no distal cross-digit triangles");
            var flyer = HandTopologyRebake.LastFlyingPreview;
            Check(HandTopologyRebake.FlyingDistalCrossDigitTriangles(flyer.sharedMesh, flyer.sharedMesh.bindposes[0]) == 0,
                "Native regenerated 16-bone flying hand has no distal cross-digit triangles");
            var poses = new[]
            {
                new Vector3(-.30f, .75f, .09f), new Vector3(-.05f, 1.1f, .45f),
                new Vector3(0, 1.32f, -.07f), new Vector3(.28f, 1.0f, .30f),
                new Vector3(.2f, 1.15f, .23f), new Vector3(-.25f, 1.8f, .4f)
            };
            var baked = new Mesh { name = "Temporary native hand skin check" };
            try
            {
                foreach (var fit in new[] { null, CandyAvatarCalibration.Current })
                foreach (float scale in new[] { .5f, 1f })
                    foreach (bool seated in new[] { false, true })
                        foreach (var wrist in poses)
                        {
                            // Production local VR uses Current fit with the seated visual posture.
                            if (fit != null && !seated && wrist != poses[poses.Length - 1]) continue;
                            rig.calibration = fit;
                            ApplyPreviewPose(rig, wrist, scale, seated, .1f);
                            int crossings = 0;
                            foreach (int upper in new[] { 2, 5 })
                            {
                                var shoulder = rig.AppearanceBone(upper).position;
                                var elbow = rig.AppearanceBone(upper + 1).position;
                                var hand = rig.AppearanceBone(upper + 2).position;
                                crossings += TorsoCrossings(rig, shoulder, elbow) + TorsoCrossings(rig, elbow, hand);
                            }
                            Check(crossings == 0, "Native arm centerlines avoid torso for wrist=" + wrist.ToString("F3") +
                                " scale=" + scale + " seated=" + seated);
                        }
                rig.calibration = CandyAvatarCalibration.Current;
                ApplyPreviewPose(rig, poses[0], .5f, true, .2f, Quaternion.Euler(12, 23, 34));
                rig.calibration = null;
                foreach (float curl in new[] { 0f, .5f, 1f })
                {
                    ApplyPreviewPose(rig, poses[0], 1, false, curl);
                    rig.bodyRenderer.BakeMesh(baked);
                    Check(baked.vertexCount == rig.bodyRenderer.sharedMesh.vertexCount, "Native BakeMesh preserves count at curl=" + curl);
                    foreach (var point in baked.vertices)
                        if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                            throw new InvalidOperationException("Native hand deformation has a non-finite vertex");
                    Check(true, "Native body hand deformation is finite at curl=" + curl);
                }
                ApplyPreviewPose(before, poses[0], 1, false, 0);
                ApplyPreviewPose(rig, poses[0], 1, false, 0);
                File.AppendAllText(Report, "COMPLETE assertions=" + assertions +
                    ". Scratch scene remains for inspection; source assets unchanged. Centerline checks exclude skin thickness and do not establish HMD comfort or actual controller input. KEEP/captures and separate asset application remain pending.\n");
            }
            finally { Object.DestroyImmediate(baked); }
        }
    }
}

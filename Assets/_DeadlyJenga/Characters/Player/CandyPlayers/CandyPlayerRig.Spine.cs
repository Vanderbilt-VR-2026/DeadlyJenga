using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // The torso has three bones: pelvis (the old Torso bone), spine and chest.
    // The two extra bones are added when the rig wakes, so the saved prefab and the
    // 44-bone list other scripts use do not change. The torso mesh is re-weighted by height.
    public sealed partial class CandyPlayerRig
    {
        const int SpineBoneIndex = 44, ChestBoneIndex = 45, SkinBoneCount = 46;
        const string SpineName = "Candy spine", ChestName = "Candy chest";
        static readonly Dictionary<Mesh, Mesh> spineMeshes = new();
        Transform spine, chest;
        // Local pose of the pelvis and chest, used to place the legs and the shoulders.
        Vector3 pelvisPosition, chestPosition;
        Quaternion pelvisRotation = Quaternion.identity, chestRotation = Quaternion.identity;
        float breathSeed;
        public Transform SpineBone => spine;
        public Transform ChestBone => chest;

        void EnsureSpine()
        {
            if (!bodyRenderer || bones == null || bones.Length < 44) return;
            breathSeed = (GetInstanceID() & 1023) * .037f;
            spine = FindSpineBone(SpineName);
            chest = FindSpineBone(ChestName);
            if (!spine) spine = NewSpineBone(SpineName);
            if (!chest) chest = NewSpineBone(ChestName);
            var current = bodyRenderer.bones;
            if (current.Length < SkinBoneCount)
            {
                var all = new Transform[SkinBoneCount];
                for (int i = 0; i < current.Length && i < SpineBoneIndex; i++) all[i] = current[i];
                all[SpineBoneIndex] = spine; all[ChestBoneIndex] = chest;
                bodyRenderer.bones = all;
            }
            else
            {
                var all = (Transform[])current.Clone();
                all[SpineBoneIndex] = spine; all[ChestBoneIndex] = chest;
                bodyRenderer.bones = all;
            }
            bodyRenderer.sharedMesh = SpineMesh(bodyRenderer.sharedMesh);
        }

        Transform FindSpineBone(string boneName)
        {
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name == boneName) return transform.GetChild(i);
            return null;
        }

        Transform NewSpineBone(string boneName)
        {
            var bone = new GameObject(boneName).transform;
            bone.SetParent(transform, false);
            return bone;
        }

        // Pelvis keeps the lower torso, the spine takes the waist and the chest takes the ribs and shoulders.
        static Mesh SpineMesh(Mesh source)
        {
            if (!source || !source.isReadable || source.bindposes.Length < 44) return source;
            if (source.bindposes.Length >= SkinBoneCount) return source;
            if (spineMeshes.TryGetValue(source, out var cached) && cached) return cached;
            var vertices = source.vertices;
            var weights = source.boneWeights;
            if (weights.Length != vertices.Length) return source;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                if (w.boneIndex0 != 0 || w.weight0 < .99f) continue;
                float y = vertices[i].y;
                float toSpine = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.70f, .92f, y));
                float toChest = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.96f, 1.16f, y));
                weights[i] = new BoneWeight
                {
                    boneIndex0 = 0, weight0 = 1 - toSpine,
                    boneIndex1 = SpineBoneIndex, weight1 = toSpine * (1 - toChest),
                    boneIndex2 = ChestBoneIndex, weight2 = toSpine * toChest
                };
            }
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " (spine)";
            mesh.boneWeights = weights;
            var binds = new Matrix4x4[SkinBoneCount];
            var old = source.bindposes;
            for (int i = 0; i < old.Length && i < SpineBoneIndex; i++) binds[i] = old[i];
            // The new bones rest at the rig origin, exactly like the pelvis bone.
            binds[SpineBoneIndex] = binds[ChestBoneIndex] = old[0];
            mesh.bindposes = binds;
            mesh.RecalculateBounds();
            spineMeshes[source] = mesh; spineMeshes[mesh] = mesh;
            return mesh;
        }

        // A dead-body copy moves only the pelvis, so the spine bones ride along with it.
        void SnapSpineToTorso()
        {
            if (!spine || !chest || bones == null) return;
            spine.SetParent(bones[0], false); chest.SetParent(bones[0], false);
            spine.localPosition = chest.localPosition = Vector3.zero;
            spine.localRotation = chest.localRotation = Quaternion.identity;
            spine.localScale = chest.localScale = Vector3.one;
        }

        // Every spine bone turns about its own pivot on top of the bone below it.
        // Pivots sit in the scaled torso (crouch and width already applied), like the old waist pivot.
        void PoseSpine(Vector3 offset, Quaternion pelvis, Quaternion spineRelative, Quaternion chestRelative, float crouch, float width)
        {
            var size = new Vector3(width, crouch, 1);
            Vector3 p0 = Vector3.up * (.67f * crouch), p1 = Vector3.up * (.80f * crouch), p2 = Vector3.up * (1.0f * crouch);
            pelvisRotation = pelvis;
            pelvisPosition = offset + p0 - pelvis * p0;
            Quaternion r1 = pelvis * spineRelative;
            Vector3 o1 = pelvisPosition + pelvis * (p1 - spineRelative * p1);
            chestRotation = r1 * chestRelative;
            chestPosition = o1 + r1 * (p2 - chestRelative * p2);
            bones[0].localPosition = pelvisPosition; bones[0].localRotation = pelvisRotation; bones[0].localScale = size;
            if (!spine || !chest) { chestPosition = pelvisPosition; chestRotation = pelvisRotation; return; }
            if (spine.parent != transform) { spine.SetParent(transform, false); chest.SetParent(transform, false); }
            spine.localPosition = o1; spine.localRotation = r1; spine.localScale = size;
            chest.localPosition = chestPosition; chest.localRotation = chestRotation; chest.localScale = size;
        }

        // Lean, hip and shoulder counter-rotation and breathing, shared between the three torso bones.
        void PoseTorso(CandyAvatarFrame frame, Vector3 offset, float crouch, float width, float run, float dance, bool moving)
        {
            float gait = moving ? Mathf.InverseLerp(.12f, 4f, locomotionSpeed) : 0f;
            float counter = Mathf.Cos(walkPhase);
            // Pitch and roll follow the stride direction. The body leans a little when walking and more when running.
            float lean = run * 14f + gait * (1f - run) * 3f;
            float pitch = lean * strideDirection.z, roll = -(run * 13f + gait * (1f - run) * 2f) * strideDirection.x;
            // Hips and shoulders twist against each other, in time with the arm swing.
            float twist = Mathf.Lerp(3.5f, 9f, run) * gait * counter;
            float breath = Mathf.Sin(Time.time * 1.7f + breathSeed) * (1f - gait * .7f);
            Quaternion pelvis = Quaternion.Euler(frame.danceTorsoEuler * dance) * Quaternion.Euler(pitch * .25f, twist * .6f, roll * .2f);
            Quaternion spineStep = Quaternion.Euler(pitch * .35f + breath * .4f, -twist * .7f, roll * .35f);
            Quaternion chestStep = Quaternion.Euler(pitch * .4f + breath * .8f, -twist * .9f, roll * .45f);
            PoseSpine(offset, pelvis, spineStep, chestStep, crouch, width);
        }

        // The three torso bones as one block (a downed body is stiff).
        void PoseSpineRigid()
        {
            if (!spine || !chest) return;
            spine.localPosition = chest.localPosition = bones[0].localPosition;
            spine.localRotation = chest.localRotation = bones[0].localRotation;
            spine.localScale = chest.localScale = bones[0].localScale;
            pelvisPosition = chestPosition = bones[0].localPosition;
            pelvisRotation = chestRotation = bones[0].localRotation;
        }
    }
}

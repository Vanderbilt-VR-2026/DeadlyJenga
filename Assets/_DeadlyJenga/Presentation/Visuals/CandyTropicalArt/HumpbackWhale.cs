using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The giant humpback: a Blender-authored mesh and painted skin (Resources/SeaLife/HumpbackWhale.bytes),
    // skinned to a small skeleton so the tail, body and flippers can flex while it breaches.
    // The data is built at length 1 (nose at z = 1, fluke root at z = 0); Create scales it.
    internal sealed class HumpbackWhale
    {
        public Transform Root { get; private set; }
        Transform tail2, tail1, mid, chest, head, leftFlipper, rightFlipper;
        Quaternion leftRest, rightRest;
        Mesh mesh;
        Texture2D skin;
        Material material;

        // Spine bones along the body (fractions of length from the fluke root).
        static readonly float[] SpineAt = { .04f, .20f, .45f, .70f, .90f };

        public static HumpbackWhale Create(Transform parent, float length)
        {
            var data = Resources.Load<TextAsset>("SeaLife/HumpbackWhale");
            if (!data) return null;
            var whale = new HumpbackWhale();
            whale.Build(parent, length, data.bytes);
            Resources.UnloadAsset(data);
            return whale;
        }

        void Build(Transform parent, float length, byte[] bytes)
        {
            using var stream = new System.IO.MemoryStream(bytes, false);
            using var read = new System.IO.BinaryReader(stream);
            if (read.ReadInt32() != 0x504D5548 || read.ReadInt32() != 1) throw new System.IO.InvalidDataException("Not humpback whale data.");
            int vertexCount = read.ReadInt32(), indexCount = read.ReadInt32();
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++) positions[i] = new Vector3(read.ReadSingle(), read.ReadSingle(), read.ReadSingle()) * length;
            for (int i = 0; i < vertexCount; i++) normals[i] = new Vector3(read.ReadSingle(), read.ReadSingle(), read.ReadSingle());
            for (int i = 0; i < vertexCount; i++) uvs[i] = new Vector2(read.ReadSingle(), read.ReadSingle());
            var parts = read.ReadBytes(vertexCount);
            var triangles = new int[indexCount];
            for (int i = 0; i < indexCount; i++) triangles[i] = read.ReadInt32();
            var png = read.ReadBytes(read.ReadInt32());

            skin = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "Humpback skin", wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            skin.LoadImage(png, true);

            // Skeleton: root at the middle of the body, a chain toward each end, flippers on the chest.
            Root = new GameObject("Humpback whale").transform;
            Root.SetParent(parent, false);
            var bodyRoot = Root;
            mid = Bone("Spine middle", bodyRoot, new Vector3(0, 0, (SpineAt[2] - .5f) * length));
            tail1 = Bone("Tail stock", mid, new Vector3(0, 0, (SpineAt[1] - SpineAt[2]) * length));
            tail2 = Bone("Fluke root", tail1, new Vector3(0, 0, (SpineAt[0] - SpineAt[1]) * length));
            chest = Bone("Chest", mid, new Vector3(0, 0, (SpineAt[3] - SpineAt[2]) * length));
            head = Bone("Head", chest, new Vector3(0, 0, (SpineAt[4] - SpineAt[3]) * length));
            var flipperRoot = new Vector3(.09f, -.06f, .70f - SpineAt[3]) * length;
            leftFlipper = Bone("Left flipper", chest, new Vector3(-flipperRoot.x, flipperRoot.y, flipperRoot.z));
            rightFlipper = Bone("Right flipper", chest, flipperRoot);
            leftRest = leftFlipper.localRotation; rightRest = rightFlipper.localRotation;
            var bones = new[] { tail2, tail1, mid, chest, head, leftFlipper, rightFlipper };

            // Positions are relative to the whale's middle so the root turns it about its centre.
            var shift = new Vector3(0, 0, -.5f * length);
            for (int i = 0; i < vertexCount; i++) positions[i] += shift;
            var weights = new BoneWeight[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                if (parts[i] == 2 || parts[i] == 3) { weights[i] = new BoneWeight { boneIndex0 = parts[i] == 2 ? 5 : 6, weight0 = 1 }; continue; }
                float t = positions[i].z / length + .5f;
                int k = 0;
                while (k < SpineAt.Length - 2 && t > SpineAt[k + 1]) k++;
                float f = Mathf.Clamp01(Mathf.InverseLerp(SpineAt[k], SpineAt[k + 1], t));
                f = f * f * (3 - 2 * f);
                weights[i] = new BoneWeight { boneIndex0 = k, weight0 = 1 - f, boneIndex1 = k + 1, weight1 = f };
            }

            mesh = new Mesh { name = "Humpback whale", indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights;
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * Root.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Humpback skin" };
            material.SetTexture("_BaseMap", skin);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .62f);
            material.SetFloat("_Metallic", 0);

            var skinGo = new GameObject("Humpback skin");
            skinGo.transform.SetParent(Root, false);
            var renderer = skinGo.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = mid;
            renderer.sharedMaterial = material;
            renderer.updateWhenOffscreen = false;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * length * 1.4f);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            Root.gameObject.SetActive(false);
        }

        static Transform Bone(string name, Transform parent, Vector3 at)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = at;
            return bone;
        }

        // tail: up/down bend of the tail stock in degrees (+ = flukes up). arch: the whole body bending its
        // back (+ = head and tail lift). flippers: 0 = at rest along the body, 1 = flung wide like wings.
        public void Pose(float tail, float arch, float flippers)
        {
            tail1.localRotation = Quaternion.Euler(-tail * .45f - arch * .5f, 0, 0);
            tail2.localRotation = Quaternion.Euler(-tail * .55f - arch * .3f, 0, 0);
            chest.localRotation = Quaternion.Euler(arch * .35f, 0, 0);
            head.localRotation = Quaternion.Euler(arch * .25f, 0, 0);
            leftFlipper.localRotation = leftRest * Quaternion.Euler(-12 * flippers, 40 * flippers, -38 * flippers);
            rightFlipper.localRotation = rightRest * Quaternion.Euler(-12 * flippers, -40 * flippers, 38 * flippers);
        }

        public void Destroy()
        {
            if (Root) Object.Destroy(Root.gameObject);
            if (mesh) Object.Destroy(mesh);
            if (skin) Object.Destroy(skin);
            if (material) Object.Destroy(material);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    public sealed class WardrobeSkinAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            public string label;
            public int anchor;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Mesh mesh;
            public Material[] materials;
            public ShadowCastingMode shadows;
            public int[] bones;
            public int rootBone;
            public Bounds bounds;
        }

        public Vector3 headScale;
        public Mesh bodyMesh;
        public Mesh headMesh;
        public Material[] bodyMaterials;
        public Material[] headMaterials;
        public bool bodyVisible;
        public bool headVisible;
        public Part[] parts;

        public static Transform FindBone(CandyPlayerRig rig, int index)
        {
            if (index == rig.BoneCount) return rig.SpineBone;
            if (index == rig.BoneCount + 1) return rig.ChestBone;
            return index < 0 ? rig.transform : rig.AppearanceBone(index);
        }
    }
}

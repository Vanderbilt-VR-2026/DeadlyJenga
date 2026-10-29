using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.Core
{
    public sealed class EquipmentModelPart
    {
        public Mesh mesh;public Material material;public Matrix4x4 local;public Color color;
    }
    public static class EquipmentModelData
    {
        public static readonly Dictionary<EquipmentKind,EquipmentModelPart[]> Parts=new Dictionary<EquipmentKind,EquipmentModelPart[]>();
    }
}

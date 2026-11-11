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
        // Set by the weapon builder. Models are built on first use (or trickled in the background) instead of all at spawn.
        public static System.Func<EquipmentKind,bool> Build;
        public static bool TryGet(EquipmentKind kind,out EquipmentModelPart[] parts)
        {
            if(Parts.TryGetValue(kind,out parts))return true;
            return Build!=null&&Build(kind)&&Parts.TryGetValue(kind,out parts);
        }
    }
}

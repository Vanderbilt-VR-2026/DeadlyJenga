using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public static class CandyWeaponIcons
    {
        static readonly Dictionary<(EquipmentKind kind, bool alternate), Sprite> Cache = new();

        public static Sprite Get(EquipmentKind kind, bool alternate = false)
        {
            if (kind < EquipmentKind.Blaster || kind > EquipmentKind.Dagger)
                return null;

            alternate &= kind == EquipmentKind.DualRifle;
            var key = (kind, alternate);
            if (Cache.TryGetValue(key, out var icon))
                return icon;

            string suffix = alternate ? "-Alternate" : "";
            icon = Resources.Load<Sprite>("WeaponIcons/" + kind + suffix);
            Cache.Add(key, icon);
            return icon;
        }
    }
}

using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // The giant-hand design the local player picked. Characters fills Names and owns the designs;
    // menus show Names and set Chosen. Hands send their own design over the network.
    public static class HandLooks
    {
        public static string[] Names = new string[0];
        public static event Action Changed;
        static int chosen;

        public static int Chosen
        {
            get => chosen;
            set
            {
                int n = Names.Length;
                int v = n > 0 ? (value % n + n) % n : 0;
                if (v == chosen) return;
                chosen = v;
                Changed?.Invoke();
            }
        }

        public static string ChosenName => chosen < Names.Length ? Names[chosen] : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Changed = null; }
    }

    // A giant hand wearing a design. Read-only, for sounds and effects.
    public interface IHandLook
    {
        int Look { get; }
        bool Visible { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
    }
}

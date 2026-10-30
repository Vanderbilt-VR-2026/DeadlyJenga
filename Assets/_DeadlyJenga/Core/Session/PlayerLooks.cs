using System;

namespace DeadlyJenga.Core
{
    // The character design the local player picked. Characters fills Names and owns the designs;
    // menus show Names and set Chosen. Survivors send their own design over the network.
    public static class PlayerLooks
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

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Changed = null; }
    }
}

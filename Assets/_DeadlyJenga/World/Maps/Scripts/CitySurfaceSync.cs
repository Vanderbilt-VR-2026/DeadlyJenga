using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-85)]
    public sealed class CitySurfaceSync : MonoBehaviour
    {
        static bool dirty;
        internal static void Changed()=>dirty=true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>dirty=false;
        void Update(){if(!dirty)return;dirty=false;Physics.SyncTransforms();}
    }
}

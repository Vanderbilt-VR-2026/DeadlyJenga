using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public enum EnemyActionKind { Alert, Attack, Hurt, Death }
    [Serializable]
    public sealed class EnemyActionFrame
    {
        public int id;
        public string actor, archetype;
        public EnemyActionKind action;
        public Vector3 position;
        public float strength, remaining;
    }
    // Local gameplay announcements. The session publishes accepted authority events to peers.
    public static class EnemyActions
    {
        public static event Action<GameObject,EnemyActionKind,float> Occurred;
        public static void Raise(GameObject actor,EnemyActionKind action,float strength=1)=>Occurred?.Invoke(actor,action,strength);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>Occurred=null;
    }
}

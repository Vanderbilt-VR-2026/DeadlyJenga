using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    [CreateAssetMenu(menuName="Deadly Jenga/Map Match Layout")]
    public sealed class MapMatchLayout : ScriptableObject
    {
        [Serializable] public sealed class TeamArea
        {
            public string name;
            public Vector3 flagHome;
            public Vector3[] spawns=Array.Empty<Vector3>();
        }
        [Tooltip("Exact ISyncedWorld layout token. Geometry changes must update both assets.")]
        public string layoutSignature;
        public Vector3[] freeSpawns=Array.Empty<Vector3>();
        public TeamArea[] teams=Array.Empty<TeamArea>();
        public Vector3 hill;
        public bool Matches(IWorldQuery world)=>world is ISyncedWorld synced&&!string.IsNullOrEmpty(layoutSignature)&&synced.SaveLayout()==layoutSignature;
    }
}

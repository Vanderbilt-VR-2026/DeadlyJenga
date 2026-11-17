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
        public bool playOpeningOnly;
        [Tooltip("Resolve authored room-floor starts onto nearby static ground; never onto loose blocks.")]
        public bool groundSpawns;
        public Vector3[] freeSpawns=Array.Empty<Vector3>();
        public TeamArea[] teams=Array.Empty<TeamArea>();
        public Vector3 hill;
        public bool TrySpawnPosition(IWorldQuery world, Vector3 local, out Vector3 point)
        {
            point=((ISyncedWorld)world).Root.TransformPoint(local);
            if(!groundSpawns)return true;
            if(!Physics.Raycast(point+Vector3.up*.25f,Vector3.down,out var hit,2f,
                (1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)||hit.normal.y<.9f)return false;
            point=hit.point+Vector3.up*.08f;
            return true;
        }
        public bool Matches(IWorldQuery world)=>world is ISyncedWorld synced&&!string.IsNullOrEmpty(layoutSignature)&&synced.SaveLayout()==layoutSignature;
    }
}

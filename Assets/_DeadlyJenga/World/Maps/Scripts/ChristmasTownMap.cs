using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class ChristmasTownMap : MonoBehaviour, IWorldQuery, IRebuildable, ISyncedWorld, IMapFootprint
    {
        public Rigidbody[] claimSurfaces = Array.Empty<Rigidbody>();
        public Transform[] interiorCenters = Array.Empty<Transform>();
        public int Revision { get; private set; }
        public Transform Root => transform;
        public IReadOnlyList<Rigidbody> Blocks => claimSurfaces;
        public Vector3 Center => transform.position;
        public float TopHeight => transform.TransformPoint(new Vector3(0,.18f,7)).y;
        public float Radius => ChristmasTownLayout.Width * .5f * Mathf.Abs(transform.lossyScale.x);
        public float StepHeight => .2f;
        public Bounds PlayableBounds
        {
            get
            {
                var b = new Bounds(transform.position, Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)for(int y=0;y<=1;y++)
                    b.Encapsulate(transform.TransformPoint(new Vector3(x*48,y*18,z*38)));
                return b;
            }
        }
        void OnEnable() { Services.World = this; }
        void OnDisable() { if(ReferenceEquals(Services.World,this)) Services.World = null; }
        public float WaterHeight(Vector3 position) => transform.position.y - 4;
        public bool IsSheltered(Vector3 position) => Physics.Raycast(position+Vector3.up*.1f,Vector3.up,20,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore);
        public Vector3 SafeSpawnPoint() => transform.TransformPoint(ChristmasTownLayout.SpawnPoints[2]);
        public void Rebuild()
        {
            foreach(var body in claimSurfaces) if(body) body.GetComponent<IBlockOwnership>()?.SetOwner(-1,Color.white);
            Revision++;
        }
        public string SaveLayout() => "christmas-candy-town:4";
        public bool LoadLayout(string layout,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(layout!=SaveLayout()||expectedBlocks!=claimSurfaces.Length||!Finite(origin.position)||!Finite(scale)||scale.x<=0||scale.y<=0||scale.z<=0||
                !Finite(new Vector3(origin.rotation.x,origin.rotation.y,origin.rotation.z))||!Finite(origin.rotation.w)||Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)>.01f)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public void SetReplica(bool replica) { /* All authored surfaces remain kinematic on every peer. */ }
        public int FootprintVersion => 1;
        public float FootprintHalfSize => 48;
        public void GetGroundFootprints(List<Footprint> into) => into.Add(new Footprint { center=Vector2.zero,halfSize=new Vector2(48,38),rotation=Quaternion.identity });
    }
}

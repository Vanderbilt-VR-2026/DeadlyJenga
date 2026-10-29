using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class PowerupTestMap : MonoBehaviour,IWorldQuery,ISyncedWorld,IMapFootprint,IRebuildable
    {
        public Rigidbody[] towerSurfaces=Array.Empty<Rigidbody>();
        public int Revision {get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>towerSurfaces;
        public Vector3 Center=>transform.position;
        public float TopHeight=>transform.TransformPoint(new Vector3(0,15.2f,0)).y;
        public float Radius=>34*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
        public float StepHeight=>1.5f;
        public Bounds PlayableBounds
        {
            get
            {
                var bounds=new Bounds(transform.position,Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)for(int y=0;y<=1;y++)bounds.Encapsulate(transform.TransformPoint(new Vector3(x*34,y*22,z*34)));
                return bounds;
            }
        }
        void OnEnable(){Services.World=this;}
        void OnDisable(){if(ReferenceEquals(Services.World,this))Services.World=null;}
        public float WaterHeight(Vector3 p)=>transform.position.y-3;
        public bool IsSheltered(Vector3 p)=>Physics.Raycast(p+Vector3.up*.1f,Vector3.up,30,(1<<Layers.Surface)|(1<<Layers.Block),QueryTriggerInteraction.Ignore);
        public Vector3 SafeSpawnPoint()=>transform.TransformPoint(new Vector3(0,.06f,-12));
        public void Rebuild(){foreach(var pickup in GetComponentsInChildren<MovementPowerupPickup>()){pickup.gameObject.SetActive(false);Destroy(pickup.gameObject);}Revision++;}
        public string SaveLayout()=>"powerup-test-island:1";
        public bool LoadLayout(string layout,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(layout!=SaveLayout()||expectedBlocks!=towerSurfaces.Length||!Finite(origin.position)||!Finite(scale)||scale.x<=0||scale.y<=0||scale.z<=0||
                !Finite(new Vector3(origin.rotation.x,origin.rotation.y,origin.rotation.z))||!Finite(origin.rotation.w)||Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)>.01f)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public void SetReplica(bool replica){}
        public int FootprintVersion=>1;
        public float FootprintHalfSize=>34;
        public void GetGroundFootprints(List<Footprint> into)=>into.Add(new Footprint{center=Vector2.zero,halfSize=Vector2.one*34,rotation=Quaternion.identity});
    }
}

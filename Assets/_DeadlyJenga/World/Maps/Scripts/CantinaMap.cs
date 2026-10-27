using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class CantinaMap : MonoBehaviour,IWorldQuery,ISyncedWorld,IMapFootprint,IRebuildable
    {
        static readonly Rigidbody[] Empty=Array.Empty<Rigidbody>();
        public int Revision {get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>Empty;
        public Vector3 Center=>transform.position;
        public float TopHeight=>transform.TransformPoint(Vector3.up*1.12f).y;
        public float Radius=>17.5f*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
        public float StepHeight=>.2f;
        public Bounds PlayableBounds
        {
            get
            {
                var b=new Bounds(transform.position,Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)for(int y=0;y<=1;y++)
                    b.Encapsulate(transform.TransformPoint(new Vector3(x*14,y*5.2f,z*10.5f)));
                return b;
            }
        }
        void OnEnable(){Services.World=this;}
        void OnDisable(){if(ReferenceEquals(Services.World,this))Services.World=null;}
        public float WaterHeight(Vector3 p)=>transform.position.y-4;
        public bool IsSheltered(Vector3 p)=>Physics.Raycast(p+Vector3.up*.1f,Vector3.up,12,1<<Layers.Surface,QueryTriggerInteraction.Ignore);
        public Vector3 SafeSpawnPoint()=>transform.TransformPoint(new Vector3(2.8f,.08f,-6.5f));
        public void Rebuild(){Revision++;}
        public string SaveLayout()=>"cantina-v2:1";
        public bool LoadLayout(string layout,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(layout!=SaveLayout()||expectedBlocks!=0||!Finite(origin.position)||!Finite(scale)||
                scale.x<=0||scale.y<=0||scale.z<=0||!Finite(origin.rotation.x)||!Finite(origin.rotation.y)||
                !Finite(origin.rotation.z)||!Finite(origin.rotation.w)||Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)>.01f)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public void SetReplica(bool replica){}
        public int FootprintVersion=>1;
        public float FootprintHalfSize=>14;
        public void GetGroundFootprints(List<Footprint> into)=>into.Add(new Footprint{center=Vector2.zero,halfSize=new Vector2(14,10.5f),rotation=Quaternion.identity});
    }
}

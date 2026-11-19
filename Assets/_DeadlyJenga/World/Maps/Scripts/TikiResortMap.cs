using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class TikiResortMap : MonoBehaviour,IWorldQuery,ISyncedWorld,IMapFootprint,IRebuildable
    {
        static readonly Rigidbody[] Empty=Array.Empty<Rigidbody>();
        const float Compression=.88f;
        const float SandTop=-.45f;
        const float ShoreX=23.4f, ShoreZ=16.2f;
        const float TowerX=16.75f, TowerZ=12.75f, DeckY=3.42f, BridgeZ=13.2f;
        const float TowerHalf=2.3f, RampWidth=2.75f, BridgeWidth=2.7f;
        readonly List<Mesh> generatedMeshes=new List<Mesh>(8);
        public int Revision {get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>Empty;
        public Vector3 Center=>transform.position;
        public float TopHeight=>transform.TransformPoint(Vector3.up*6.68f).y;
        public float Radius=>45f*Compression*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
        public float StepHeight=>.2f;
        public Bounds PlayableBounds
        {
            get
            {
                var b=new Bounds(transform.position,Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int z=0;z<2;z++)for(int y=0;y<2;y++)
                    b.Encapsulate(LayoutPoint(new Vector3(x*28,y==0?-2:7,z==0?-36:22)));
                return b;
            }
        }
        void Awake()
        {
            var collision=transform.Find("Collision");
            if(!collision)return;
            collision.localScale=new Vector3(Compression,1,Compression);
            var beach=collision.Find("Contoured walkable beach")?.GetComponent<MeshCollider>();
            if(beach)beach.sharedMesh=ShoreMesh("Compact resort beach",new Vector2(ShoreX,ShoreZ),
                new Vector2(26.1f,19.3f),4,64);

            var routes=Group(collision,"Tiki tower routes",Vector3.zero);
            AddLookout(routes,-1);
            AddLookout(routes,1);
            var start=new Vector3(-TowerX,DeckY,BridgeZ);
            var end=new Vector3(TowerX,DeckY,BridgeZ);
            Walkway(routes,"Skybridge",start,end,BridgeWidth,false);
            Walkway(routes,"Lower promenade",new Vector3(-TowerX,-.425f,BridgeZ),
                new Vector3(TowerX,-.425f,BridgeZ),3.12f,false);
            foreach(int side in new[]{-1,1})
            {
                float z=BridgeZ+side*BridgeWidth*.5f;
                Rail(routes,new Vector3(-TowerX+TowerHalf,DeckY,z),new Vector3(TowerX-TowerHalf,DeckY,z));
                foreach(float x in new[]{-9.7f,9.7f})
                {
                    Box(routes,"Support · bridge trestle",new Vector3(x,1.42f,z+side*.13f),new Vector3(.33f,4,.33f));
                    Box(routes,"Support · pier footing",new Vector3(x,-.28f,z+side*.13f),new Vector3(.74f,.35f,.69f));
                }
            }

            // The outer palms sit on small cays where the old beach has receded.
            foreach(Transform part in collision)
            {
                if(part.name!="Palm trunk")continue;
                var at=part.localPosition;
                float x=at.x/ShoreX,z=at.z/ShoreZ;
                if(x*x*x*x+z*z*z*z<.96f)continue;
                var cay=Group(routes,"Palm cay",new Vector3(at.x,0,at.z));
                cay.gameObject.layer=Layers.Surface;
                cay.gameObject.AddComponent<MeshCollider>().sharedMesh=ShoreMesh("Palm cay",
                    new Vector2(1.1f,1.32f),new Vector2(1.74f,1.92f),2,20);
            }
        }

        void AddLookout(Transform routes,int side)
        {
            float x=TowerX*side;
            var deck=new Vector3(x,DeckY,TowerZ);
            var tower=Group(routes,side<0?"Signal lookout":"Surf watch",deck);
            Box(tower,"Deck · lookout floor",new Vector3(0,-.14f,0),new Vector3(TowerHalf*2,.28f,TowerHalf*2));
            Box(tower,"Roof · weather shelter",new Vector3(0,3.07f,0),new Vector3(5.12f,.32f,4.99f));
            foreach(int east in new[]{-1,1})foreach(int north in new[]{-1,1})
            {
                var corner=new Vector3(east*2.02f,0,north*2.01f);
                Box(tower,"Support · bound timber post",corner-Vector3.up*.82f,new Vector3(.28f,7.8f,.28f));
                Box(tower,"Stone · tide footing",corner-Vector3.up*3.79f,new Vector3(.72f,.55f,.72f));
            }

            float edge=TowerHalf;
            Rail(tower,new Vector3(-edge,0,edge),new Vector3(edge,0,edge));
            Rail(tower,new Vector3(side*edge,0,-edge),new Vector3(side*edge,0,edge));
            Rail(tower,new Vector3(-edge,0,-edge),new Vector3(-RampWidth*.5f,0,-edge));
            Rail(tower,new Vector3(RampWidth*.5f,0,-edge),new Vector3(edge,0,-edge));
            float inner=-side*edge,bridge=BridgeZ-TowerZ;
            Rail(tower,new Vector3(inner,0,-edge),new Vector3(inner,0,bridge-BridgeWidth*.5f));
            Rail(tower,new Vector3(inner,0,bridge+BridgeWidth*.5f),new Vector3(inner,0,edge));

            Walkway(routes,"Lookout ramp",new Vector3(x,SandTop,.58f),
                new Vector3(x,DeckY,TowerZ-edge+.10f),RampWidth,true);
        }

        static void Walkway(Transform parent,string label,Vector3 start,Vector3 end,float width,bool rail)
        {
            var direction=end-start;
            var rotation=Quaternion.LookRotation(direction);
            Box(parent,"Deck · "+label,(start+end)*.5f-rotation*Vector3.up*.12f,
                new Vector3(width,.24f,direction.magnitude+.12f),rotation);
            if(!rail)return;
            var edge=rotation*Vector3.right*(width*.5f);
            Rail(parent,start+edge,end+edge);
            Rail(parent,start-edge,end-edge);
        }

        static void Rail(Transform parent,Vector3 start,Vector3 end)
        {
            var direction=end-start;
            var rotation=Quaternion.LookRotation(direction);
            Box(parent,"Guard · continuous handrail",(start+end)*.5f+Vector3.up*.51f,
                new Vector3(.12f,1.02f,direction.magnitude),rotation);
        }

        static Transform Group(Transform parent,string label,Vector3 point)
        {
            var child=new GameObject(label).transform;
            child.SetParent(parent,false);
            child.localPosition=point;
            return child;
        }

        static void Box(Transform parent,string label,Vector3 point,Vector3 size,Quaternion? rotation=null)
        {
            var piece=Group(parent,label,point);
            piece.gameObject.layer=Layers.Surface;
            piece.localRotation=rotation??Quaternion.identity;
            piece.gameObject.AddComponent<BoxCollider>().size=size;
        }

        Mesh ShoreMesh(string label,Vector2 dry,Vector2 wet,float shape,int segments)
        {
            var vertices=new Vector3[1+segments*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*9];
            vertices[0]=Vector3.up*SandTop;
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                float c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                float x=Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),2/shape),z=Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),2/shape);
                vertices[1+i]=new Vector3(x*dry.x,SandTop,z*dry.y);
                vertices[1+segments+i]=new Vector3(x*wet.x,-1.85f,z*wet.y);
                uv[1+i]=new Vector2(vertices[1+i].x,vertices[1+i].z)*.2f;
                uv[1+segments+i]=new Vector2(vertices[1+segments+i].x,vertices[1+segments+i].z)*.2f;
                int next=(i+1)%segments,a=1+i,b=1+next,outer=1+segments;
                int t=i*9;
                triangles[t]=0;triangles[t+1]=b;triangles[t+2]=a;
                triangles[t+3]=a;triangles[t+4]=b;triangles[t+5]=outer+i;
                triangles[t+6]=b;triangles[t+7]=outer+next;triangles[t+8]=outer+i;
            }
            var mesh=new Mesh{name=label,vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            return mesh;
        }

        Vector3 LayoutPoint(Vector3 local)=>transform.TransformPoint(new Vector3(local.x*Compression,local.y,local.z*Compression));
        void OnDestroy(){foreach(var mesh in generatedMeshes)if(mesh)Destroy(mesh);}
        void OnEnable(){Services.World=this;}
        void OnDisable(){if(ReferenceEquals(Services.World,this))Services.World=null;}
        public float WaterHeight(Vector3 p)=>transform.TransformPoint(Vector3.down*1.4f).y;
        public bool IsSheltered(Vector3 p)=>Physics.Raycast(p+Vector3.up*.1f,Vector3.up,12,1<<Layers.Surface,QueryTriggerInteraction.Ignore);
        public Vector3 SafeSpawnPoint()=>LayoutPoint(new Vector3(2.8f,.08f,-6.5f));
        public void Rebuild(){Revision++;}
        public string SaveLayout()=>"tiki-resort:2";
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
        public int FootprintVersion=>2;
        public float FootprintHalfSize=>36*Compression;
        public void GetGroundFootprints(List<Footprint> into)
        {
            into.Add(new Footprint{center=Vector2.zero,halfSize=new Vector2(26.1f,19.3f)*Compression,rotation=Quaternion.identity});
            into.Add(new Footprint{center=new Vector2(0,-25.5f)*Compression,halfSize=new Vector2(3.5f,10.5f)*Compression,rotation=Quaternion.identity});
        }
    }
}

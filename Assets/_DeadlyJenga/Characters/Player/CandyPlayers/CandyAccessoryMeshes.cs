using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // One immutable mesh per attachment, shared by every player and bot.
    public static class CandyAccessoryMeshes
    {
        static readonly Dictionary<AvatarAccessory,Mesh> cache=new Dictionary<AvatarAccessory,Mesh>();
        public static Mesh[] Blaster()
        {
            var body=new Builder();var rings=new Builder();var details=new Builder();var trim=new Builder();
            body.Sphere(new Vector3(0,.035f,.035f),new Vector3(.085f,.09f,.15f));body.Cone(new Vector3(0,.035f,.08f),new Vector3(0,.035f,.20f),.09f,.07f,20);
            body.Box(new Vector3(0,-.09f,-.025f),new Vector3(.065f,.18f,.065f));body.Beam(new Vector3(0,.035f,.18f),new Vector3(0,.035f,.43f),.022f);body.Sphere(new Vector3(0,.035f,.44f),Vector3.one*.035f);
            for(int i=0;i<3;i++)rings.Ring(new Vector3(0,.035f,.24f+i*.065f),.09f-i*.012f,.009f,Quaternion.identity);
            for(int i=0;i<4;i++)details.Box(new Vector3(.081f,-.015f,-.025f+i*.025f),new Vector3(.008f,.04f,.012f));
            for(int side=-1;side<=1;side+=2)
            {
                trim.Beam(new Vector3(side*.07f,.07f,-.055f),new Vector3(side*.065f,.075f,.16f),.006f);
                body.Cone(new Vector3(side*.055f,.08f,-.04f),new Vector3(side*.10f,.15f,-.095f),.025f,.006f,12);
                details.Sphere(new Vector3(side*.087f,.025f,.035f),new Vector3(.009f,.018f,.018f));
            }
            for(int i=0;i<5;i++)trim.Box(new Vector3(0,-.04f-i*.027f,-.06f),new Vector3(.068f,.008f,.009f));
            trim.Ring(new Vector3(0,.035f,.425f),.028f,.006f,Quaternion.identity);
            return new[]{body.Mesh("Blaster body"),rings.Mesh("Three blaster rings"),details.Mesh("Blaster vents"),trim.Mesh("Blaster chrome seams and grip ribs")};
        }
        public static Mesh Get(AvatarAccessory option)
        {
            if(cache.TryGetValue(option,out var mesh)&&mesh)return mesh;
            var b=new Builder();
            switch(option)
            {
                case AvatarAccessory.Mohawk:
                    for(int i=0;i<7;i++){float z=-.17f+i*.052f;b.Cone(new Vector3(0,.16f,z),new Vector3(0,.37f-Mathf.Abs(z)*.5f,z),.047f,0,8);}break;
                case AvatarAccessory.Glasses:
                    for(int s=-1;s<=1;s+=2){b.Ring(new Vector3(s*.105f,.025f,.205f),.082f,.014f,Quaternion.identity);b.Beam(new Vector3(s*.18f,.03f,.19f),new Vector3(s*.218f,.03f,-.07f),.012f);}
                    b.Beam(new Vector3(-.03f,.03f,.232f),new Vector3(.03f,.03f,.232f),.012f);break;
                case AvatarAccessory.RabbitEars:
                    for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.115f,.37f,0),new Vector3(.07f,.25f,.045f),Quaternion.Euler(0,0,s*-12));break;
                case AvatarAccessory.SkeletonMask:
                    for(int s=-1;s<=1;s+=2){b.Ring(new Vector3(s*.10f,.035f,.217f),.078f,.025f,Quaternion.identity);b.Sphere(new Vector3(s*.15f,-.08f,.17f),new Vector3(.075f,.045f,.065f));}
                    b.Sphere(new Vector3(0,.15f,.12f),new Vector3(.19f,.08f,.13f));
                    for(int i=0;i<7;i++)b.Box(new Vector3((i-3)*.037f,-.135f,.20f),new Vector3(.027f,.075f,.055f));break;
                case AvatarAccessory.TopHat:
                    b.Cone(new Vector3(0,.175f,0),new Vector3(0,.20f,0),.31f,.31f,32);
                    b.Cone(new Vector3(0,.20f,0),new Vector3(0,.49f,0),.175f,.19f,24);
                    b.Cone(new Vector3(0,.49f,0),new Vector3(0,.515f,0),.20f,.20f,24);break;
                case AvatarAccessory.Cape:
                    b.Wedge(new Vector3(-.23f,1.22f,-.11f),new Vector3(.23f,1.22f,-.11f),new Vector3(-.39f,.38f,-.32f),new Vector3(.39f,.38f,-.32f),.025f);break;
                case AvatarAccessory.AntAntennae:
                    for(int s=-1;s<=1;s+=2){b.Beam(new Vector3(s*.10f,.17f,.015f),new Vector3(s*.19f,.40f,.10f),.014f);b.Beam(new Vector3(s*.19f,.40f,.10f),new Vector3(s*.22f,.43f,.19f),.014f);b.Sphere(new Vector3(s*.22f,.43f,.19f),Vector3.one*.038f);}break;
                case AvatarAccessory.UnicornHorn:b.Cone(new Vector3(0,.13f,.14f),new Vector3(0,.49f,.28f),.065f,0,16);break;
                case AvatarAccessory.DevilHorns:
                    for(int s=-1;s<=1;s+=2){b.Cone(new Vector3(s*.15f,.13f,0),new Vector3(s*.24f,.30f,.02f),.073f,.042f,12);b.Cone(new Vector3(s*.24f,.30f,.02f),new Vector3(s*.20f,.43f,.065f),.042f,0,12);}break;
                case AvatarAccessory.Broccoli:
                    b.Cone(new Vector3(0,.14f,0),new Vector3(0,.31f,0),.095f,.11f,12);
                    for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;b.Sphere(new Vector3(Mathf.Cos(a)*.18f,.31f+(i%2)*.045f,Mathf.Sin(a)*.16f),new Vector3(.12f,.10f,.12f));}b.Sphere(new Vector3(0,.41f,0),Vector3.one*.14f);break;
                case AvatarAccessory.LongNose:b.Cone(new Vector3(0,-.015f,.17f),new Vector3(0,-.015f,.64f),.047f,.009f,16);break;
                case AvatarAccessory.StrongJaw:
                    b.Wedge(new Vector3(-.215f,-.04f,.15f),new Vector3(.215f,-.04f,.15f),new Vector3(-.14f,-.235f,.13f),new Vector3(.14f,-.235f,.13f),.10f);break;
                case AvatarAccessory.PantherHead:
                    for(int s=-1;s<=1;s+=2){b.Sphere(new Vector3(s*.16f,.19f,-.015f),new Vector3(.082f,.105f,.05f));b.Sphere(new Vector3(s*.072f,-.06f,.20f),new Vector3(.09f,.065f,.09f));b.Beam(new Vector3(s*.04f,.055f,.21f),new Vector3(s*.16f,.08f,.17f),.027f);}
                    b.Sphere(new Vector3(0,-.035f,.28f),new Vector3(.045f,.032f,.033f));break;
                case AvatarAccessory.PantherClaws:
                    for(int i=0;i<4;i++)b.Cone(new Vector3((i-1.5f)*.027f,-.03f,.07f),new Vector3((i-1.5f)*.032f,-.065f,.23f-Mathf.Abs(i-1.5f)*.02f),.014f,0,8);break;
                case AvatarAccessory.PantherTail:
                    b.Beam(new Vector3(0,.65f,-.10f),new Vector3(0,.40f,-.45f),.045f);b.Beam(new Vector3(0,.40f,-.45f),new Vector3(0,.54f,-.72f),.035f);b.Sphere(new Vector3(0,.54f,-.72f),Vector3.one*.044f);break;
            }
            mesh=b.Mesh(option.ToString());cache[option]=mesh;return mesh;
        }

        internal sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> triangles=new List<int>();
            void Quad(int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
            public void Sphere(Vector3 center,Vector3 radii,Quaternion? rotation=null)
            {
                int start=vertices.Count;var q=rotation??Quaternion.identity;const int rings=10,sides=16;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++){float t=Mathf.PI*y/rings,a=Mathf.PI*2*x/sides;vertices.Add(center+q*Vector3.Scale(radii,new Vector3(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a))));}
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=start+y*(sides+1)+x;Quad(a,a+1,a+sides+2,a+sides+1);}
            }
            public void Cone(Vector3 from,Vector3 to,float r0,float r1,int sides)
            {
                int start=vertices.Count;var q=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
                for(int y=0;y<2;y++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add((y==0?from:to)+q*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(y==0?r0:r1));}
                int cap=vertices.Count;vertices.Add(from);vertices.Add(to);
                for(int i=0;i<sides;i++){int j=(i+1)%sides;Quad(start+i,start+sides+i,start+sides+j,start+j);triangles.Add(cap);triangles.Add(start+i);triangles.Add(start+j);triangles.Add(cap+1);triangles.Add(start+sides+j);triangles.Add(start+sides+i);}
            }
            public void Beam(Vector3 a,Vector3 b,float radius)=>Cone(a,b,radius,radius,8);
            public void Ring(Vector3 center,float radius,float tube,Quaternion rotation)
            {
                int start=vertices.Count;const int around=24,sides=8;
                for(int i=0;i<around;i++)for(int j=0;j<sides;j++){float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/sides,r=radius+Mathf.Cos(b)*tube;vertices.Add(center+rotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*tube));}
                for(int i=0;i<around;i++)for(int j=0;j<sides;j++)Quad(start+i*sides+j,start+((i+1)%around)*sides+j,start+((i+1)%around)*sides+(j+1)%sides,start+i*sides+(j+1)%sides);
            }
            public void Box(Vector3 center,Vector3 size)=>Wedge(center+new Vector3(-size.x,size.y,0)*.5f,center+new Vector3(size.x,size.y,0)*.5f,center+new Vector3(-size.x,-size.y,0)*.5f,center+new Vector3(size.x,-size.y,0)*.5f,size.z);
            public void Wedge(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float depth)
            {
                int s=vertices.Count;foreach(var p in new[]{a,b,d,c})vertices.Add(p+Vector3.forward*depth*.5f);foreach(var p in new[]{a,b,d,c})vertices.Add(p-Vector3.forward*depth*.5f);
                Quad(s,s+3,s+2,s+1);Quad(s+4,s+5,s+6,s+7);for(int i=0;i<4;i++)Quad(s+i,s+(i+1)%4,s+4+(i+1)%4,s+4+i);
            }
            public Mesh Mesh(string name){var mesh=new Mesh{name="Candy attachment - "+name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}

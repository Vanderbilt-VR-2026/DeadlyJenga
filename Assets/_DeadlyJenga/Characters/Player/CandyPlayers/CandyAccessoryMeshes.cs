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
                    b.Sphere(new Vector3(0,.055f,-.065f),new Vector3(.185f,.175f,.125f));
                    for(int side=-1;side<=1;side+=2){b.Ring(new Vector3(side*.085f,.015f,.12f),.061f,.025f,Quaternion.identity);b.Sphere(new Vector3(side*.145f,-.065f,.09f),new Vector3(.045f,.035f,.06f));}
                    b.Box(new Vector3(0,.10f,.10f),new Vector3(.29f,.055f,.06f));b.Box(new Vector3(0,-.145f,.065f),new Vector3(.23f,.025f,.09f));
                    for(int tooth=0;tooth<7;tooth++)b.Box(new Vector3((tooth-3)*.027f,-.11f,.12f),new Vector3(.020f,.050f,.025f));break;
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
                case AvatarAccessory.Crown:
                    b.Ring(new Vector3(0,.22f,0),.20f,.025f,Quaternion.Euler(90,0,0));for(int i=0;i<8;i++){float a=i*Mathf.PI/4;b.Cone(new Vector3(Mathf.Cos(a)*.20f,.22f,Mathf.Sin(a)*.20f),new Vector3(Mathf.Cos(a)*.20f,.38f,Mathf.Sin(a)*.20f),.04f,0,4);}break;
                case AvatarAccessory.Halo:b.Ring(new Vector3(0,.36f,0),.22f,.018f,Quaternion.Euler(90,0,0));break;
                case AvatarAccessory.CatEars:for(int side=-1;side<=1;side+=2)b.Cone(new Vector3(side*.15f,.14f,0),new Vector3(side*.22f,.39f,0),.09f,0,4);break;
                case AvatarAccessory.Headphones:for(int side=-1;side<=1;side+=2)b.Sphere(new Vector3(side*.22f,0,0),new Vector3(.055f,.11f,.08f));b.Ring(new Vector3(0,.08f,0),.23f,.018f,Quaternion.identity);break;
                case AvatarAccessory.CowboyHat:b.Sphere(new Vector3(0,.20f,0),new Vector3(.37f,.025f,.29f));b.Sphere(new Vector3(0,.27f,0),new Vector3(.18f,.14f,.16f));break;
                case AvatarAccessory.PirateHat:b.Wedge(new Vector3(-.32f,.20f,.05f),new Vector3(.32f,.20f,.05f),new Vector3(-.13f,.45f,-.02f),new Vector3(.13f,.45f,-.02f),.12f);break;
                case AvatarAccessory.Beanie:b.Sphere(new Vector3(0,.18f,-.01f),new Vector3(.23f,.13f,.22f));b.Sphere(new Vector3(0,.34f,0),Vector3.one*.06f);break;
                case AvatarAccessory.ChefHat:b.Cone(new Vector3(0,.16f,0),new Vector3(0,.37f,0),.18f,.19f,16);for(int i=0;i<5;i++){float a=i*Mathf.PI*.4f;b.Sphere(new Vector3(Mathf.Cos(a)*.12f,.40f,Mathf.Sin(a)*.12f),Vector3.one*.12f);}break;
                case AvatarAccessory.VikingHelmet:b.Sphere(new Vector3(0,.15f,0),new Vector3(.23f,.13f,.21f));for(int side=-1;side<=1;side+=2)b.Cone(new Vector3(side*.17f,.17f,0),new Vector3(side*.36f,.36f,0),.065f,0,10);break;
                case AvatarAccessory.PartyHat:b.Cone(new Vector3(0,.16f,0),new Vector3(.03f,.57f,0),.17f,0,16);break;
                case AvatarAccessory.PropellerCap:b.Sphere(new Vector3(0,.18f,0),new Vector3(.22f,.08f,.21f));b.Beam(new Vector3(0,.22f,0),new Vector3(0,.36f,0),.015f);b.Box(new Vector3(0,.37f,0),new Vector3(.48f,.015f,.07f));break;
                case AvatarAccessory.FlowerCrown:for(int i=0;i<12;i++){float a=i*Mathf.PI/6;for(int petal=0;petal<5;petal++){float t=petal*Mathf.PI*.4f;b.Sphere(new Vector3(Mathf.Cos(a)*.21f+Mathf.Cos(t)*.025f,.17f+Mathf.Sin(t)*.025f,Mathf.Sin(a)*.21f),Vector3.one*.018f);}}break;
                case AvatarAccessory.RobotAntenna:b.Beam(new Vector3(0,.18f,0),new Vector3(0,.46f,0),.016f);b.Sphere(new Vector3(0,.46f,0),Vector3.one*.055f);break;
                case AvatarAccessory.ButterflyWings:case AvatarAccessory.AngelWings:case AvatarAccessory.DragonWings:
                    for(int side=-1;side<=1;side+=2){float reach=option==AvatarAccessory.DragonWings?.90f:.65f;b.Wedge(new Vector3(side*.10f,1.2f,-.17f),new Vector3(side*reach,1.52f,-.30f),new Vector3(side*.11f,.72f,-.18f),new Vector3(side*reach,.72f,-.34f),.04f);if(option==AvatarAccessory.AngelWings)for(int feather=0;feather<6;feather++)b.Sphere(new Vector3(side*(.25f+feather*.07f),1.15f-feather*.04f,-.32f),new Vector3(.07f,.26f,.025f));}break;
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
            List<Vector3> pumpkinNormals;
            public void CarvedPumpkin()
            {
                pumpkinNormals=new List<Vector3>();
                var holes=new List<Vector2[]> {
                    new[]{new Vector2(.04f,.075f),new Vector2(.20f,.13f),new Vector2(.165f,.015f)},
                    new[]{new Vector2(-.04f,.075f),new Vector2(-.20f,.13f),new Vector2(-.165f,.015f)}
                };
                float[] x={-.21f,-.16f,-.11f,-.055f,0,.055f,.11f,.16f,.21f};
                float[] top={-.065f,-.095f,-.065f,-.10f,-.07f,-.10f,-.065f,-.095f,-.065f};
                float[] bottom={-.065f,-.17f,-.14f,-.19f,-.15f,-.19f,-.14f,-.17f,-.065f};
                for(int i=0;i<x.Length-1;i++)holes.Add(new[]{new Vector2(x[i],top[i]),new Vector2(x[i+1],top[i+1]),new Vector2(x[i+1],bottom[i+1]),new Vector2(x[i],bottom[i])});
                foreach(var hole in holes)
                {float area=0;for(int i=0;i<hole.Length;i++)area+=Cross(hole[i],hole[(i+1)%hole.Length]);if(area<0)System.Array.Reverse(hole);}
                const int rows=24,cols=64;
                Vector3 Point(int y,int column)
                {
                    float t=Mathf.PI*y/rows,a=Mathf.PI*2*column/cols,r=1+.045f*Mathf.Cos(a*10);
                    return new Vector3(Mathf.Sin(t)*Mathf.Cos(a)*.31f*r,Mathf.Cos(t)*.29f,Mathf.Sin(t)*Mathf.Sin(a)*.285f*r);
                }
                for(int y=0;y<rows;y++)for(int column=0;column<cols;column++)
                {
                    var a=Point(y,column);var b=Point(y,column+1);var c=Point(y+1,column+1);var d=Point(y+1,column);
                    PumpkinPatch(new List<Vector3>{a,b,c},holes);PumpkinPatch(new List<Vector3>{a,c,d},holes);
                }
            }
            static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            static List<Vector3> ClipEdge(List<Vector3> polygon,Vector2 a,Vector2 b,bool inside)
            {
                var result=new List<Vector3>();if(polygon.Count==0)return result;
                var previous=polygon[polygon.Count-1];float oldDistance=Cross(b-a,(Vector2)previous-a);bool oldInside=inside?oldDistance>=0:oldDistance<=0;
                foreach(var point in polygon)
                {
                    float distance=Cross(b-a,(Vector2)point-a);bool current=inside?distance>=0:distance<=0;
                    if(current!=oldInside)result.Add(Vector3.Lerp(previous,point,oldDistance/(oldDistance-distance)));
                    if(current)result.Add(point);previous=point;oldDistance=distance;oldInside=current;
                }
                return result;
            }
            void PumpkinPatch(List<Vector3> triangle,List<Vector2[]> holes)
            {
                var pieces=new List<List<Vector3>>{triangle};
                if((triangle[0].z+triangle[1].z+triangle[2].z)/3>.12f)foreach(var hole in holes)
                {
                    var outside=new List<List<Vector3>>();
                    foreach(var piece in pieces)
                    {
                        var remaining=piece;
                        for(int i=0;i<hole.Length&&remaining.Count>=3;i++)
                        {
                            if((hole[i]-hole[(i+1)%hole.Length]).sqrMagnitude<1e-12f)continue;
                            var cut=ClipEdge(remaining,hole[i],hole[(i+1)%hole.Length],false);if(cut.Count>=3)outside.Add(cut);
                            remaining=ClipEdge(remaining,hole[i],hole[(i+1)%hole.Length],true);
                        }
                    }
                    pieces=outside;
                }
                foreach(var polygon in pieces)
                {
                    int start=vertices.Count;
                    foreach(var p in polygon){vertices.Add(p);pumpkinNormals.Add(new Vector3(p.x/(.31f*.31f),p.y/(.29f*.29f),p.z/(.285f*.285f)).normalized);}
                    foreach(var p in polygon){vertices.Add(p*.96f);pumpkinNormals.Add(-new Vector3(p.x/(.31f*.31f),p.y/(.29f*.29f),p.z/(.285f*.285f)).normalized);}
                    for(int i=1;i<polygon.Count-1;i++)
                    {
                        if(Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).sqrMagnitude<1e-13f)continue;
                        triangles.AddRange(new[]{start,start+i,start+i+1,start+polygon.Count,start+polygon.Count+i+1,start+polygon.Count+i});
                    }
                }
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
            public Mesh Mesh(string name){var mesh=new Mesh{name="Candy attachment - "+name};mesh.indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(pumpkinNormals!=null)mesh.SetNormals(pumpkinNormals);else mesh.RecalculateNormals();var uv=new List<Vector2>(vertices.Count);var normals=mesh.normals;for(int i=0;i<vertices.Count;i++){var v=vertices[i];var n=normals[i];uv.Add(Mathf.Abs(n.x)>Mathf.Abs(n.y)&&Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(v.z,v.y)*4:Mathf.Abs(n.y)>Mathf.Abs(n.z)?new Vector2(v.x,v.z)*4:new Vector2(v.x,v.y)*4);}mesh.SetUVs(0,uv);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
        }
    }
}

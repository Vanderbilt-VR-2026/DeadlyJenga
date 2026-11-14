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
            var b=new Builder{Soft=true};
            switch(option)
            {
                case AvatarAccessory.Mohawk:
                    for(int i=0;i<7;i++){float z=-.17f+i*.052f;b.Cone(new Vector3(0,.15f,z),new Vector3(0,.37f-Mathf.Abs(z)*.5f,z),.05f,0,12);}break;
                case AvatarAccessory.Glasses:
                    for(int s=-1;s<=1;s+=2){b.Ring(new Vector3(s*.105f,.025f,.205f),.082f,.016f,Quaternion.identity);b.Beam(new Vector3(s*.18f,.03f,.19f),new Vector3(s*.218f,.03f,-.07f),.013f);}
                    b.Beam(new Vector3(-.03f,.03f,.232f),new Vector3(.03f,.03f,.232f),.013f);break;
                case AvatarAccessory.RabbitEars:
                    for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.115f,.37f,0),new Vector3(.07f,.25f,.045f),Quaternion.Euler(0,0,s*-12));break;
                case AvatarAccessory.SkeletonMask:
                    b.Sphere(new Vector3(0,.055f,-.065f),new Vector3(.185f,.175f,.125f));
                    for(int side=-1;side<=1;side+=2){b.Ring(new Vector3(side*.085f,.015f,.12f),.061f,.025f,Quaternion.identity);b.Sphere(new Vector3(side*.145f,-.065f,.09f),new Vector3(.045f,.035f,.06f));}
                    b.Box(new Vector3(0,.10f,.10f),new Vector3(.29f,.055f,.06f));b.Box(new Vector3(0,-.145f,.065f),new Vector3(.23f,.025f,.09f));
                    for(int tooth=0;tooth<7;tooth++)b.Box(new Vector3((tooth-3)*.027f,-.11f,.12f),new Vector3(.020f,.050f,.025f));break;
                case AvatarAccessory.TopHat:
                    b.Sphere(new Vector3(0,.19f,0),new Vector3(.32f,.028f,.32f));
                    b.Cone(new Vector3(0,.19f,0),new Vector3(0,.49f,0),.18f,.19f,24);
                    b.Ring(new Vector3(0,.255f,0),.182f,.022f,Quaternion.Euler(90,0,0));break;
                case AvatarAccessory.Cape:
                    b.Pillow(new Vector3(-.23f,1.24f,-.12f),new Vector3(.23f,1.24f,-.12f),new Vector3(-.40f,.38f,-.34f),new Vector3(.40f,.38f,-.34f),.05f,.13f,.025f);
                    b.Sphere(new Vector3(0,1.27f,-.09f),new Vector3(.19f,.06f,.11f));break;
                case AvatarAccessory.AntAntennae:
                    for(int s=-1;s<=1;s+=2){b.Beam(new Vector3(s*.10f,.17f,.015f),new Vector3(s*.19f,.40f,.10f),.016f);b.Beam(new Vector3(s*.19f,.40f,.10f),new Vector3(s*.22f,.43f,.19f),.016f);b.Sphere(new Vector3(s*.22f,.43f,.19f),Vector3.one*.045f);}break;
                case AvatarAccessory.UnicornHorn:
                    b.Cone(new Vector3(0,.13f,.14f),new Vector3(0,.49f,.28f),.068f,0,20);
                    for(int i=1;i<=3;i++){float t=i*.22f;var dir=new Vector3(0,.36f,.14f).normalized;b.Ring(new Vector3(0,.13f,.14f)+new Vector3(0,.36f,.14f)*t,.068f*(1-t)+.006f,.011f,Quaternion.FromToRotation(Vector3.forward,dir));}
                    b.Sphere(new Vector3(0,.135f,.135f),new Vector3(.085f,.05f,.085f));break;
                case AvatarAccessory.DevilHorns:
                    for(int s=-1;s<=1;s+=2){b.Cone(new Vector3(s*.15f,.13f,0),new Vector3(s*.24f,.30f,.02f),.075f,.045f,12);b.Cone(new Vector3(s*.24f,.30f,.02f),new Vector3(s*.20f,.43f,.065f),.045f,0,12);}break;
                case AvatarAccessory.Broccoli:
                    b.Cone(new Vector3(0,.14f,0),new Vector3(0,.31f,0),.095f,.11f,16);
                    for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;b.Sphere(new Vector3(Mathf.Cos(a)*.18f,.31f+(i%2)*.045f,Mathf.Sin(a)*.16f),new Vector3(.12f,.10f,.12f));}b.Sphere(new Vector3(0,.41f,0),Vector3.one*.14f);break;
                case AvatarAccessory.LongNose:b.Cone(new Vector3(0,-.015f,.17f),new Vector3(0,-.015f,.64f),.052f,.012f,16);break;
                case AvatarAccessory.StrongJaw:
                    b.Sphere(new Vector3(0,-.17f,.12f),new Vector3(.17f,.075f,.10f));
                    for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.15f,-.09f,.115f),new Vector3(.075f,.095f,.085f));break;
                case AvatarAccessory.Crown:
                    b.Ring(new Vector3(0,.22f,0),.205f,.035f,Quaternion.Euler(90,0,0));
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var baseP=new Vector3(Mathf.Cos(a)*.205f,.23f,Mathf.Sin(a)*.205f);b.Cone(baseP,baseP+Vector3.up*.15f,.05f,.018f,10);b.Sphere(baseP+Vector3.up*.17f,Vector3.one*.03f);}
                    for(int i=0;i<4;i++){float a=i*Mathf.PI/2+Mathf.PI/4;b.Sphere(new Vector3(Mathf.Cos(a)*.236f,.22f,Mathf.Sin(a)*.236f),Vector3.one*.026f);}break;
                case AvatarAccessory.Halo:b.Ring(new Vector3(0,.37f,0),.22f,.03f,Quaternion.Euler(90,0,0));break;
                case AvatarAccessory.CatEars:for(int side=-1;side<=1;side+=2){b.Cone(new Vector3(side*.15f,.14f,0),new Vector3(side*.22f,.39f,0),.10f,0,12);}break;
                case AvatarAccessory.Headphones:
                    for(int side=-1;side<=1;side+=2){b.Sphere(new Vector3(side*.225f,0,0),new Vector3(.065f,.12f,.09f));b.Sphere(new Vector3(side*.255f,0,0),new Vector3(.03f,.085f,.07f));}
                    b.Ring(new Vector3(0,.07f,0),.235f,.022f,Quaternion.identity);break;
                case AvatarAccessory.CowboyHat:
                    b.Sphere(new Vector3(0,.20f,0),new Vector3(.34f,.026f,.27f));
                    for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.29f,.235f,0),new Vector3(.09f,.026f,.22f),Quaternion.Euler(0,0,s*-24));
                    b.Sphere(new Vector3(0,.27f,0),new Vector3(.18f,.14f,.16f));b.Ring(new Vector3(0,.225f,0),.172f,.02f,Quaternion.Euler(90,0,0),new Vector2(1,.9f));break;
                case AvatarAccessory.PirateHat:
                    b.Sphere(new Vector3(0,.30f,0),new Vector3(.36f,.13f,.17f));
                    for(int s=-1;s<=1;s+=2)b.Cone(new Vector3(s*.30f,.29f,0),new Vector3(s*.45f,.24f,0),.12f,0,12);
                    b.Sphere(new Vector3(0,.22f,0),new Vector3(.24f,.07f,.22f));
                    b.Ring(new Vector3(0,.26f,.0f),.20f,.02f,Quaternion.Euler(90,0,0),new Vector2(1,.9f));break;
                case AvatarAccessory.Beanie:
                    b.Sphere(new Vector3(0,.16f,-.01f),new Vector3(.235f,.15f,.23f));b.Ring(new Vector3(0,.12f,-.01f),.228f,.032f,Quaternion.Euler(90,0,0),new Vector2(1,.97f));b.Sphere(new Vector3(0,.33f,0),Vector3.one*.07f);break;
                case AvatarAccessory.ChefHat:
                    b.Cone(new Vector3(0,.16f,0),new Vector3(0,.37f,0),.18f,.19f,16);
                    for(int i=0;i<5;i++){float a=i*Mathf.PI*.4f;b.Sphere(new Vector3(Mathf.Cos(a)*.12f,.40f,Mathf.Sin(a)*.12f),Vector3.one*.12f);}b.Sphere(new Vector3(0,.44f,0),Vector3.one*.13f);break;
                case AvatarAccessory.VikingHelmet:
                    b.Sphere(new Vector3(0,.13f,0),new Vector3(.235f,.16f,.225f));b.Ring(new Vector3(0,.10f,0),.228f,.022f,Quaternion.Euler(90,0,0));
                    for(int side=-1;side<=1;side+=2){b.Cone(new Vector3(side*.19f,.14f,0),new Vector3(side*.31f,.22f,0),.07f,.055f,12);b.Cone(new Vector3(side*.31f,.22f,0),new Vector3(side*.35f,.40f,0),.055f,0,12);}break;
                case AvatarAccessory.PartyHat:
                    b.Cone(new Vector3(0,.16f,0),new Vector3(.03f,.55f,0),.17f,0,24);b.Ring(new Vector3(0,.17f,0),.17f,.018f,Quaternion.Euler(90,0,0));b.Sphere(new Vector3(.03f,.57f,0),Vector3.one*.045f);break;
                case AvatarAccessory.PropellerCap:
                    b.Sphere(new Vector3(0,.17f,0),new Vector3(.23f,.11f,.22f));b.Beam(new Vector3(0,.22f,0),new Vector3(0,.33f,0),.02f);b.Sphere(new Vector3(0,.345f,0),Vector3.one*.03f);
                    for(int s=0;s<2;s++)b.Sphere(new Vector3(0,.36f,0),new Vector3(.25f,.012f,.045f),Quaternion.Euler(0,s*90+22,0));break;
                case AvatarAccessory.FlowerCrown:
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var c=new Vector3(Mathf.Cos(a)*.215f,.15f,Mathf.Sin(a)*.215f);var n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));b.Sphere(c,Vector3.one*.026f);for(int petal=0;petal<5;petal++){float t=petal*Mathf.PI*.4f;b.Sphere(c+n*.012f+Vector3.up*Mathf.Cos(t)*.04f+Vector3.Cross(n,Vector3.up)*Mathf.Sin(t)*.04f,Vector3.one*.026f);}}break;
                case AvatarAccessory.RobotAntenna:b.Beam(new Vector3(0,.18f,0),new Vector3(0,.46f,0),.018f);b.Sphere(new Vector3(0,.46f,0),Vector3.one*.058f);b.Sphere(new Vector3(0,.19f,0),new Vector3(.06f,.035f,.06f));break;
                case AvatarAccessory.ButterflyWings:case AvatarAccessory.AngelWings:case AvatarAccessory.DragonWings:Wings(b,option);break;
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

        // Wings are sculpted from rounded panels and ellipsoids, never flat slabs. Shared by the accessory and by dragon, gargoyle and similar skins.
        internal static void Wings(Builder b,AvatarAccessory style)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;var shoulder=new Vector3(s*.11f,1.22f,-.16f);
                if(style==AvatarAccessory.ButterflyWings)
                {
                    b.Sphere(new Vector3(s*.40f,1.31f,-.27f),new Vector3(.30f,.22f,.022f),Quaternion.Euler(0,s*-18,s*-20));
                    b.Sphere(new Vector3(s*.30f,.90f,-.25f),new Vector3(.20f,.17f,.022f),Quaternion.Euler(0,s*-14,s*28));
                    b.Sphere(new Vector3(s*.43f,1.33f,-.285f),new Vector3(.07f,.07f,.026f),Quaternion.Euler(0,s*-18,0));
                }
                else if(style==AvatarAccessory.AngelWings)
                {
                    for(int row=0;row<3;row++)for(int f=0;f<5;f++)
                    {
                        float t=f/4f,angle=Mathf.Lerp(-8f,-62f,t)*s;float length=Mathf.Lerp(.20f,.34f,t)*(1+row*.22f);
                        var root=shoulder+new Vector3(s*(.06f+t*.28f+row*.02f),-.05f*row-.04f*t,-.03f-row*.015f);
                        b.Sphere(root+new Vector3(0,-length*.55f,0)*Mathf.Cos(angle*Mathf.Deg2Rad)+new Vector3(s*length*.55f*Mathf.Abs(Mathf.Sin(angle*Mathf.Deg2Rad)),0,-.015f*row),new Vector3(.055f+row*.008f,length*.5f,.018f),Quaternion.Euler(0,0,angle));
                    }
                    b.Sphere(shoulder+new Vector3(s*.05f,.02f,-.02f),new Vector3(.09f,.10f,.05f));
                }
                else
                {
                    var wrist=new Vector3(s*.30f,1.52f,-.22f);var tip=new Vector3(s*.88f,1.58f,-.40f);
                    b.Beam(shoulder,wrist,.026f);b.Beam(wrist,tip,.020f);b.Sphere(wrist,Vector3.one*.04f);
                    var f0=new Vector3(s*.82f,.98f,-.40f);var f1=new Vector3(s*.62f,.80f,-.36f);var f2=new Vector3(s*.40f,.78f,-.30f);var body=new Vector3(s*.12f,.80f,-.18f);
                    b.Beam(wrist,f0,.016f);b.Beam(wrist,f1,.016f);b.Beam(wrist,f2,.016f);
                    b.Pillow(wrist,wrist+Vector3.up*.002f,f0,tip,.022f,0,0,5,6);b.Pillow(wrist,wrist+Vector3.up*.002f,f1,f0,.022f,0,0,5,6);b.Pillow(wrist,wrist+Vector3.up*.002f,f2,f1,.022f,0,0,5,6);b.Pillow(shoulder,wrist,body,f2,.022f,0,0,5,6);
                }
            }
        }

        internal sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> triangles=new List<int>();
            void Quad(int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
            // Soft builders make rounded, welded-normal candy shapes. Weapons, jaguar and mushroom keep the original sharp builder.
            public bool Soft;
            public void Sphere(Vector3 center,Vector3 radii,Quaternion? rotation=null)
            {
                int rings=10,sides=16;
                if(Soft){float big=Mathf.Max(radii.x,Mathf.Max(radii.y,radii.z));sides=Mathf.Clamp(Mathf.RoundToInt(big*150),8,22);rings=Mathf.Max(6,sides*2/3);}
                int start=vertices.Count;var q=rotation??Quaternion.identity;
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
                if(Soft){RoundCone(from,to,r0,r1,sides);return;}
                int start=vertices.Count;var q=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
                for(int y=0;y<2;y++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add((y==0?from:to)+q*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(y==0?r0:r1));}
                int cap=vertices.Count;vertices.Add(from);vertices.Add(to);
                for(int i=0;i<sides;i++){int j=(i+1)%sides;Quad(start+i,start+sides+i,start+sides+j,start+j);triangles.Add(cap);triangles.Add(start+i);triangles.Add(start+j);triangles.Add(cap+1);triangles.Add(start+sides+j);triangles.Add(start+sides+i);}
            }
            public void Beam(Vector3 a,Vector3 b,float radius)=>Cone(a,b,radius,radius,Soft?Mathf.Clamp(Mathf.RoundToInt(radius*300)+6,8,14):14);
            // A tapered tube with rounded ends. A zero radius end stays a smooth point.
            void RoundCone(Vector3 from,Vector3 to,float r0,float r1,int sides)
            {
                sides=Mathf.Clamp(sides,8,24);var axis=to-from;float length=axis.magnitude;if(length<1e-5f)return;
                var q=Quaternion.FromToRotation(Vector3.up,axis/length);
                var profile=new List<Vector2>();// x = distance along the axis, y = radius
                float b0=r0>0?Mathf.Min(r0*.6f,length*.3f):0,b1=r1>0?Mathf.Min(r1*.6f,length*.3f):0;
                if(r0>0)for(int k=0;k<=3;k++){float a=k*Mathf.PI*.5f/3;profile.Add(new Vector2(b0*(1-Mathf.Cos(a)),r0*Mathf.Sin(a)));}else profile.Add(new Vector2(0,0));
                if(r0>0&&r1>0&&length>b0+b1)profile.Add(new Vector2(length*.5f,(r0+r1)*.5f));
                if(r1>0)for(int k=3;k>=0;k--){float a=k*Mathf.PI*.5f/3;profile.Add(new Vector2(length-b1*(1-Mathf.Cos(a)),r1*Mathf.Sin(a)));}else profile.Add(new Vector2(length,0));
                int start=vertices.Count;
                foreach(var row in profile)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add(from+q*new Vector3(Mathf.Cos(a)*row.y,row.x,Mathf.Sin(a)*row.y));}
                for(int r=0;r<profile.Count-1;r++)for(int i=0;i<sides;i++){int j=(i+1)%sides;Quad(start+r*sides+i,start+(r+1)*sides+i,start+(r+1)*sides+j,start+r*sides+j);}
            }
            public void Ring(Vector3 center,float radius,float tube,Quaternion rotation)=>Ring(center,radius,tube,rotation,Vector2.one);
            // squash scales the ring's two flat axes, so a belt or stripe can follow an oval body.
            public void Ring(Vector3 center,float radius,float tube,Quaternion rotation,Vector2 squash)
            {
                int around=36,sides=12;if(Soft){around=Mathf.Clamp(Mathf.RoundToInt(radius*130),16,40);sides=tube>.02f?10:7;}
                int start=vertices.Count;
                for(int i=0;i<around;i++)for(int j=0;j<sides;j++){float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/sides,r=radius+Mathf.Cos(b)*tube;vertices.Add(center+rotation*new Vector3(Mathf.Cos(a)*r*squash.x,Mathf.Sin(a)*r*squash.y,Mathf.Sin(b)*tube));}
                for(int i=0;i<around;i++)for(int j=0;j<sides;j++)Quad(start+i*sides+j,start+((i+1)%around)*sides+j,start+((i+1)%around)*sides+(j+1)%sides,start+i*sides+(j+1)%sides);
            }
            public void Box(Vector3 center,Vector3 size){if(Soft)RoundBox(center,size);else BoxPlain(center,size);}
            void BoxPlain(Vector3 center,Vector3 size)=>Wedge(center+new Vector3(-size.x,size.y,0)*.5f,center+new Vector3(size.x,size.y,0)*.5f,center+new Vector3(-size.x,-size.y,0)*.5f,center+new Vector3(size.x,-size.y,0)*.5f,size.z);
            // A box with rounded edges and corners.
            void RoundBox(Vector3 center,Vector3 size)
            {
                var half=size*.5f;float r=Mathf.Min(Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.7f,.03f);var inner=half-Vector3.one*r;
                float[] u={-1,-.93f,-.75f,-.45f,0,.45f,.75f,.93f,1};
                // Each face is a grid on the cube; every point is pushed onto the rounded surface.
                for(int face=0;face<6;face++)
                {
                    int axis=face/2;float sign=face%2==0?1:-1;int start=vertices.Count;
                    for(int i=0;i<u.Length;i++)for(int j=0;j<u.Length;j++)
                    {
                        var c=Vector3.zero;c[axis]=sign;c[(axis+1)%3]=u[i];c[(axis+2)%3]=u[j];
                        var p=Vector3.Scale(c,half);var core=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                        var n=p-core;n=n.sqrMagnitude<1e-10f?c:n.normalized;vertices.Add(center+core+n*r);
                    }
                    for(int i=0;i<u.Length-1;i++)for(int j=0;j<u.Length-1;j++)
                    {
                        int a0=start+i*u.Length+j,b0=a0+1,c0=a0+u.Length+1,d0=a0+u.Length;
                        if(sign>0)Quad(a0,d0,c0,b0);else Quad(a0,b0,c0,d0);
                    }
                }
            }
            public void Wedge(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float depth)
            {
                if(Soft){Pillow(a,b,c,d,depth,0,0);return;}
                int s=vertices.Count;foreach(var p in new[]{a,b,d,c})vertices.Add(p+Vector3.forward*depth*.5f);foreach(var p in new[]{a,b,d,c})vertices.Add(p-Vector3.forward*depth*.5f);
                Quad(s,s+3,s+2,s+1);Quad(s+4,s+5,s+6,s+7);for(int i=0;i<4;i++)Quad(s+i,s+(i+1)%4,s+4+(i+1)%4,s+4+i);
            }
            // A puffy cloth panel between four corners (a top-left, b top-right, c bottom-left, d bottom-right).
            // bend curves the side edges toward +Z so a cape wraps around the body; ripple adds soft folds down the length.
            public void Pillow(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float depth,float bend,float ripple,int across=8,int down=10)
            {
                int start=vertices.Count;
                for(int face=0;face<2;face++)
                {
                    float side=face==0?1:-1;
                    for(int j=0;j<=down;j++)for(int i=0;i<=across;i++)
                    {
                        float u=(float)i/across,v=(float)j/down;var top=Vector3.Lerp(a,b,u);var bottom=Vector3.Lerp(c,d,u);var p=Vector3.Lerp(top,bottom,v);
                        float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));float round=Mathf.Sin(Mathf.Clamp01(edge/.16f)*Mathf.PI*.5f);
                        float curve=bend*Mathf.Pow(2*u-1,2)*(.35f+.65f*v)+ripple*Mathf.Sin(u*Mathf.PI*3+v*1.5f)*v;
                        p.z+=curve+side*depth*.5f*round;vertices.Add(p);
                    }
                }
                int row=across+1,face1=(across+1)*(down+1);
                for(int j=0;j<down;j++)for(int i=0;i<across;i++)
                {
                    int q0=start+j*row+i;Quad(q0,q0+row,q0+row+1,q0+1);
                    int r0=q0+face1;Quad(r0,r0+1,r0+row+1,r0+row);
                }
            }
            // Soft meshes share normals at matching positions, so there are no seams or hard corners.
            void WeldNormals(Mesh mesh)
            {
                var normals=mesh.normals;var sums=new Dictionary<Vector3Int,Vector3>();const float grid=20000f;
                Vector3Int Key(Vector3 v)=>new Vector3Int(Mathf.RoundToInt(v.x*grid),Mathf.RoundToInt(v.y*grid),Mathf.RoundToInt(v.z*grid));
                for(int i=0;i<vertices.Count;i++){var k=Key(vertices[i]);sums[k]=sums.TryGetValue(k,out var sum)?sum+normals[i]:normals[i];}
                for(int i=0;i<vertices.Count;i++)normals[i]=sums[Key(vertices[i])].normalized;
                mesh.normals=normals;
            }
            public Mesh Mesh(string name){var mesh=new Mesh{name="Candy attachment - "+name};mesh.indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(pumpkinNormals!=null)mesh.SetNormals(pumpkinNormals);else{mesh.RecalculateNormals();if(Soft)WeldNormals(mesh);}var uv=new List<Vector2>(vertices.Count);var normals=mesh.normals;for(int i=0;i<vertices.Count;i++){var v=vertices[i];var n=normals[i];uv.Add(Mathf.Abs(n.x)>Mathf.Abs(n.y)&&Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(v.z,v.y)*4:Mathf.Abs(n.y)>Mathf.Abs(n.z)?new Vector2(v.x,v.z)*4:new Vector2(v.x,v.y)*4);}mesh.SetUVs(0,uv);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
        }
    }
}

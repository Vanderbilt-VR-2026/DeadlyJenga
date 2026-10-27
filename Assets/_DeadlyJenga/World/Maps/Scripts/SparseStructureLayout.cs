using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum SparseStructureKind { Crown, Terraces, Viaduct, Keeps, CrownGrand, TerracesGrand, ViaductGrand, KeepsGrand, Festival, FestivalCity, FestivalCityPacked, FestivalCitySurfaceMesh, FestivalCityOptimized }

    public readonly struct SparseMember
    {
        public readonly Vector3 Position, Size;
        public readonly Quaternion Rotation;
        public readonly CityPart[] Parts;
        public readonly CastleSection Section;
        public readonly string Role;
        public SparseMember(Vector3 position, Vector3 size, CastleSection section, string role)
        { Position=position; Size=size; Section=section; Role=role; Rotation=Quaternion.identity; Parts=null; }
        public SparseMember(Vector3 position,Vector3 size,Quaternion rotation,CastleSection section,string role)
        { Position=position; Size=size; Rotation=rotation; Section=section; Role=role; Parts=null; }
        public SparseMember(Vector3 position,Quaternion rotation,CityPart[] parts,string role)
        {Position=position;Rotation=rotation;Parts=parts;Section=CastleSection.Corner;Role=role;Size=Vector3.one;}
        public Bounds Bounds
        {
            get
            {
                if(Parts!=null)
                {
                    var b=new Bounds(Position+Rotation*Parts[0].Bounds.center,Vector3.zero);
                    foreach(var part in Parts){var pb=part.Bounds;for(int x0=0;x0<2;x0++)for(int y0=0;y0<2;y0++)for(int z0=0;z0<2;z0++)b.Encapsulate(Position+Rotation*new Vector3(x0==0?pb.min.x:pb.max.x,y0==0?pb.min.y:pb.max.y,z0==0?pb.min.z:pb.max.z));}
                    return b;
                }
                var x=Rotation*Vector3.right*Size.x;var y=Rotation*Vector3.up*Size.y;var z=Rotation*Vector3.forward*Size.z;
                return new Bounds(Position,new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
            }
        }
    }

    // All members are separate loose boxes. Bearings, not anchors or joints, support them.
    public static class SparseStructureLayout
    {
        public static readonly string[] Ids={"crown-study","terrace-study","viaduct-study","keep-study","crown-grand","terrace-grand","viaduct-grand","keep-grand","festival-megastructure","festival-city","festival-city-packed","festival-city-surface-mesh","festival-city-optimized"};
        public static readonly string[] Titles={"Crown of Giants · Study","Terrace City · Study","Falling Viaduct · Study","Hollow Megakeeps · Study","Crown Archipelago · Grand","Terrace Mountain · Grand","Ribbon Skyways · Grand","Megakeep District · Grand","Festival of Giants · Hybrid","Festival of Giants - City","Festival City - Packed Shapes","Festival City - Surface Mesh","Festival City - Optimized"};
        public static bool IsCity(SparseStructureKind kind)=>kind==SparseStructureKind.FestivalCity||kind==SparseStructureKind.FestivalCityPacked||kind==SparseStructureKind.FestivalCitySurfaceMesh||kind==SparseStructureKind.FestivalCityOptimized;
        public static string Id(SparseStructureKind kind)=>Ids[(int)kind];
        public static string Title(SparseStructureKind kind)=>Titles[(int)kind];
        public static List<SparseMember> Build(SparseStructureKind kind)
        {
            if(!Enum.IsDefined(typeof(SparseStructureKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            var b=new Builder();
            switch(kind)
            {
                case SparseStructureKind.Crown: Crown(b,Vector2.zero,3);break;
                case SparseStructureKind.Terraces: Terraces(b,Vector2.zero,5,6);break;
                case SparseStructureKind.Viaduct: Viaduct(b,5,3,false);break;
                case SparseStructureKind.Keeps: Keeps(b,2,2,3);break;
                case SparseStructureKind.CrownGrand: CrownChain(b);break;
                case SparseStructureKind.TerracesGrand: Terraces(b,Vector2.zero,13,6);break;
                case SparseStructureKind.ViaductGrand: Viaduct(b,9,4,true);break;
                case SparseStructureKind.KeepsGrand: Keeps(b,3,3,5);break;
                default: Festival(b);break;
            }
            if(kind==SparseStructureKind.Festival||IsCity(kind))
                for(int i=0;i<b.Members.Count;i++){var p=b.Members[i];b.Members[i]=new SparseMember(p.Position*1.25f,p.Size*1.25f,p.Rotation,p.Section,p.Role);}
            if(IsCity(kind))FestivalCityLayout.AddTo(b.Members);
            return b.Members;
        }
        public static Bounds Bounds(IReadOnlyList<SparseMember> members)
        {
            if(members.Count==0)return new Bounds(Vector3.zero,Vector3.one);
            var bounds=members[0].Bounds;for(int i=1;i<members.Count;i++)bounds.Encapsulate(members[i].Bounds);return bounds;
        }
        sealed class Builder
        {
            public readonly List<SparseMember> Members=new List<SparseMember>();
            public void Box(float x,float bottom,float z,float sx,float sy,float sz,CastleSection section,string role)
            { Members.Add(new SparseMember(new Vector3(x,bottom+sy*.5f,z),new Vector3(sx,sy,sz),section,role)); }
            public void Pier(Vector2 p,int courses,float width=8)
            {
                for(int level=0;level<courses;level++)
                {
                    float y=level*6;
                    Box(p.x,y,p.y,width,5,width,CastleSection.Corner,"pier");
                    Box(p.x,y+5,p.y,width+2,1,width+2,CastleSection.Wall,"rest cap");
                }
            }
            public void Deck(Vector2 a,Vector2 c,float top,float halfA,float halfC,float width=3)
            {
                var d=c-a;bool alongX=Mathf.Abs(d.x)>.01f;
                if(alongX&&Mathf.Abs(d.y)>.01f)throw new ArgumentException("Deck endpoints must align with a bearing axis.");
                float distance=d.magnitude;if(distance<=halfA+halfC)return;
                Vector2 direction=d/distance;
                float start=halfA-1,end=distance-halfC+1;
                var center=a+direction*((start+end)*.5f);
                Box(center.x,top,center.y,alongX?end-start:width,1,alongX?width:end-start,CastleSection.Keep,"bearing deck");
            }
            public void Cover(Vector2 p,float y,bool split=false)
            {
                Box(p.x,y,p.y,split?1.2f:2,1,1.2f,CastleSection.Wall,"loose cover");
            }
            // One sloped physical box rests on the ground and a cap edge. Its collider follows the slope.
            public void Ramp(Vector2 face,Vector2 outward,float height,float width=2)
            {
                float run=height*2,thickness=.6f;
                var axis=new Vector3(-outward.x*run,height,-outward.y*run).normalized;
                var forward=Vector3.Cross(axis,Vector3.up).normalized;
                var normal=Vector3.Cross(forward,axis).normalized;
                float length=Mathf.Sqrt(run*run+height*height)+.5f/Mathf.Sqrt(.8f);
                var low=new Vector3(face.x+outward.x*run,0,face.y+outward.y*run);
                Members.Add(new SparseMember(low+axis*(length*.5f)+normal*(thickness*.5f),new Vector3(length,thickness,width),Quaternion.LookRotation(forward,normal),CastleSection.Keep,"grounded ramp"));
            }
            // Each tread is one grounded column, not a staircase made from a pyramid of tiny cubes.
            public void Stairs(Vector2 face,Vector2 outward,int height,float width=3)
            {
                for(int step=1;step<=height;step++)
                {
                    var p=face+outward*((height-step+.5f)*1.2f);
                    Box(p.x,0,p.y,outward.x!=0?1.2f:width,step,outward.y!=0?1.2f:width,CastleSection.Keep,"grounded stair");
                }
            }
        }
        static void Crown(Builder b,Vector2 center,int courses,bool stairs=true)
        {
            float r=14;var points=new[]{center+new Vector2(-r,-r),center+new Vector2(r,-r),center+new Vector2(r,r),center+new Vector2(-r,r)};
            foreach(var p in points)b.Pier(p,courses);
            for(int level=1;level<=courses;level++)
            {
                // Alternating pairs at intermediate tiers; complete loops at the first and last tiers.
                for(int side=0;side<4;side++)
                    if(level==1||level==courses||side%2==level%2)b.Deck(points[side],points[(side+1)%4],level*6,5,5);
                foreach(var p in points)b.Cover(p+new Vector2(4.6f,4.6f),level*6,true);
            }
            if(stairs)
            {
                b.Stairs(points[0]+Vector2.left*5,Vector2.left,6);
                b.Stairs(points[2]+Vector2.right*5,Vector2.right,6);
            }
        }
        static void CrownChain(Builder b)
        {
            for(int x=0;x<3;x++)for(int z=0;z<2;z++)
            {
                var c=new Vector2((x-1)*56,(z-.5f)*56);int courses=4+x+z;
                Crown(b,c,courses,x==0||x==2);
                if(x<2)for(int side=-1;side<=1;side+=2)for(int tier=1;tier<=3;tier+=2)
                    b.Deck(c+new Vector2(14,side*14),c+new Vector2(42,side*14),tier*6,5,5);
                if(z==0)for(int side=-1;side<=1;side+=2)for(int tier=2;tier<=4;tier+=2)
                    b.Deck(c+new Vector2(side*14,14),c+new Vector2(side*14,42),tier*6,5,5);
            }
        }
        static void Terraces(Builder b,Vector2 center,int count,int courseHeight,bool stairs=true)
        {
            int r=count/2;
            for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)
            {
                if(x==0&&z==0)continue;
                // One court, four rising ridges and small notches keep long roofs from becoming a safe solid pyramid.
                if(count>5&&Mathf.Abs(x)==r&&Mathf.Abs(z)%3==1)continue;
                int courses=r+1-Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
                var p=center+new Vector2(x*14,z*14);
                for(int level=0;level<courses;level++)
                    b.Box(p.x,level*courseHeight,p.y,12,courseHeight,12,level%2==0?CastleSection.Corner:CastleSection.Wall,"terrace mass");
                // Raised cover only on roofs; the central roof lane remains unobstructed.
                if((x+z)%2==0)b.Cover(p+new Vector2(3.5f,3.5f),courses*courseHeight);
            }
            if(stairs)foreach(var d in new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down})
                b.Stairs(center+d*(r*14+6),d,courseHeight);
            // Short bridges join the inner corners over the 2 m seams, with seats on both roof edges.
            for(int side=-1;side<=1;side+=2)
            {
                b.Deck(center+new Vector2(-14,side*14),center+new Vector2(0,side*14),r*courseHeight,6,6);
                b.Deck(center+new Vector2(0,side*14),center+new Vector2(14,side*14),r*courseHeight,6,6);
            }
        }
        static void Viaduct(Builder b,int columns,int rows,bool grand)
        {
            var tiers=new int[columns,rows];
            for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
            {
                var p=new Vector2((x-(columns-1)*.5f)*22,(z-(rows-1)*.5f)*20);
                tiers[x,z]=grand?4+(x+2*z)%4:2+(x+z)%2;
                b.Pier(p,tiers[x,z],6);
                b.Cover(p+new Vector2(3.6f,3.6f),tiers[x,z]*6,true);
                if(x==0&&z%2==0)b.Stairs(p+Vector2.left*4,Vector2.left,6);
            }
            for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
            {
                var p=new Vector2((x-(columns-1)*.5f)*22,(z-(rows-1)*.5f)*20);
                if(x+1<columns)
                {
                    int shared=Mathf.Min(tiers[x,z],tiers[x+1,z]);
                    for(int t=1;t<=shared;t++)if(t==1||t==shared||t%2==z%2)b.Deck(p,p+Vector2.right*22,t*6,4,4);
                }
                if(z+1<rows)
                {
                    int shared=Mathf.Min(tiers[x,z],tiers[x,z+1]);
                    for(int t=1;t<=shared;t++)if(t==1||t==shared)b.Deck(p,p+Vector2.up*20,t*6,4,4);
                }
            }
        }
        static void Keep(Builder b,Vector2 center,int levels)
        {
            for(int level=0;level<levels;level++)
            {
                float y=level*8;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    b.Box(center.x+x*6,y,center.y+z*6,3,5,3,CastleSection.Corner,"keep pier");
                for(int side=-1;side<=1;side+=2)
                {
                    b.Box(center.x,y+5,center.y+side*6,15,1,3,CastleSection.Wall,"lower lintel");
                    b.Box(center.x+side*6,y+6,center.y,3,1,15,CastleSection.Wall,"upper lintel");
                    b.Box(center.x,y+7,center.y+side*6,15,1,3,CastleSection.Keep,"floor sector");
                    b.Box(center.x+side*6,y+7,center.y,3,1,9,CastleSection.Keep,"floor sector");
                }
            }
            // Covers stay clear of the next tier's corner piers.
            b.Cover(center+new Vector2(3.2f,6),levels*8);
        }
        static void Keeps(Builder b,int columns,int rows,int levels)
        {
            for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
            {
                var p=new Vector2((x-(columns-1)*.5f)*36,(z-(rows-1)*.5f)*36);
                int height=levels+(columns>2?(x+z)%2:0);Keep(b,p,height);
                if(x==0)b.Stairs(p+Vector2.left*7.5f,Vector2.left,8);
                for(int t=1;t<=levels;t++)
                {
                    if(x+1<columns)b.Deck(p,p+Vector2.right*36,t*8,7.5f,7.5f);
                    if(z+1<rows&&(t==1||t==levels||x%2==0))b.Deck(p,p+Vector2.up*36,t*8,7.5f,7.5f);
                }
            }
        }
        static void Festival(Builder b)
        {
            // Outer keep landmarks and a perimeter viaduct leave the central terraces readable.
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Keep(b,new Vector2(x*72,z*48),4+(x==z?1:0));
            var xs=new[]{-72f,-48f,-32f,0f,32f,48f,72f};
            foreach(int side in new[]{-1,1})
            {
                for(int i=1;i<xs.Length-1;i++)b.Pier(new Vector2(xs[i],side*48),4,8);
                for(int i=0;i<xs.Length-1;i++)foreach(int tier in new[]{2,4})
                    if(tier==4||(i>0&&i<xs.Length-2))b.Deck(new Vector2(xs[i],side*48),new Vector2(xs[i+1],side*48),tier*6,i==0?7.5f:5,i+1==xs.Length-1?7.5f:5);
                foreach(float z in new[]{-24f,0f,24f})b.Pier(new Vector2(side*72,z),4,8);
                var zs=new[]{-48f,-24f,0f,24f,48f};
                for(int i=0;i<zs.Length-1;i++)b.Deck(new Vector2(side*72,zs[i]),new Vector2(side*72,zs[i+1]),24,i==0?7.5f:5,i==3?7.5f:5);
            }
            // Inner crowns have taller north spires, lower rescue caps and actual bearing at each connection.
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var p=new Vector2(x*32,z*24);b.Pier(p,z>0?6:4);
                b.Deck(p,new Vector2(x*32,z*48),24,5,5);
            }
            for(int side=-1;side<=1;side+=2)
            {
                b.Pier(new Vector2(side*32,0),3);
                b.Deck(new Vector2(-32,side*24),new Vector2(0,side*24),24,5,5,4);
                b.Deck(new Vector2(0,side*24),new Vector2(32,side*24),24,5,5,4);
                b.Deck(new Vector2(side*32,-24),new Vector2(side*32,0),18,5,5,4);
                b.Deck(new Vector2(side*32,0),new Vector2(side*32,24),18,5,5,4);
            }
            Terraces(b,Vector2.zero,3,6,false);
            // Shorten the two centerline terraces to leave the adjacent crown caps clear.
            for(int i=0;i<b.Members.Count;i++)
            {
                var p=b.Members[i];
                if(p.Role=="terrace mass"&&Mathf.Abs(p.Position.x)<.01f&&Mathf.Abs(Mathf.Abs(p.Position.z)-14)<.01f)
                    b.Members[i]=new SparseMember(p.Position,new Vector3(p.Size.x,p.Size.y,10),p.Section,p.Role);
            }
            // Four central masses and their grounded approach steps provide a low, sheltered recovery district.
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)b.Stairs(new Vector2(x*14,z*20),Vector2.up*z,6);
            foreach(int side in new[]{-1,1})
            {
                b.Stairs(new Vector2(side*77,0),Vector2.right*side,6);
                b.Ramp(new Vector2(side*77,2.8f),Vector2.right*side,6);
            }
            // Intermediate seats split the long crown cross-spans, reducing unsupported span lengths.
            // These piers occupy the open centerline outside the terrace district.
            foreach(int side in new[]{-1,1})
            {
                b.Pier(new Vector2(0,side*24),4);
                b.Cover(new Vector2(0,side*24),24);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public readonly struct MayhemShape
    {
        public readonly Vector3 center, size;
        public readonly Quaternion rotation;
        public readonly int finish;
        public readonly Vector3[] vertices;
        public MayhemShape(Vector3 center, Vector3 size, int finish, Quaternion rotation, Vector3[] vertices=null)
        {this.center=center;this.size=size;this.finish=finish;this.rotation=rotation;this.vertices=vertices;}
    }

    public sealed class MayhemModule
    {
        public int district;
        public string purpose;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public readonly List<MayhemShape> shapes=new();
        public float mass;
        public MayhemModule hingeSupport;
        public Vector3 pivot;
    }

    // Ten independent neighborhoods on one ground plane. Coordinates are in metres.
    public static partial class MayhemStructureLayout
    {
        public static readonly string[] Names={"Interleaved Bookends","Branch Mesa Forest","Broken-Ring Canyon","Folded Ribbon Gorge","Reciprocal Crown Bazaar","Skyrail Shipyard","Domino Portal Quarter","Keystone Vault District","Rolling Borough","Counterweight Sky Harbor"};
        public static readonly Vector3[] Centers={new(-255,0,-150),new(-85,0,-150),new(85,0,-150),new(255,0,-150),new(-255,0,0),new(-85,0,0),new(85,0,0),new(255,0,0),new(-85,0,150),new(85,0,150)};
        public static readonly Vector3 IslandSize=new(680,2,470);
        public static List<MayhemModule> Build(int district=-1)
        {
            if(district < -1 || district>9)throw new ArgumentOutOfRangeException(nameof(district));
            var b=new Builder();
            for(int d=0;d<10;d++)
            {
                if(district>=0&&d!=district)continue;
                b.district=d;b.origin=Centers[d];
                switch(d){case 0:Bookends(b);break;case 1:Mesas(b);break;case 2:Rings(b);break;case 3:Ribbons(b);break;case 4:Crowns(b);break;case 5:Skyrail(b);break;case 6:Portals(b);break;case 7:Vaults(b);break;case 8:Drums(b);break;case 9:Harbor(b);break;}
            }
            return b.modules;
        }
        public static Bounds Bounds(IReadOnlyList<MayhemModule> modules)
        {
            bool first=true;var bounds=new Bounds();
            foreach(var m in modules)foreach(var s in m.shapes)
            {
                var vs=s.vertices??Cube(s.size);
                foreach(var v in vs){var p=m.position+m.rotation*(s.center+s.rotation*v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            return bounds;
        }
        public static Vector3[] Cube(Vector3 size)
        {
            var h=size*.5f;
            return new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)};
        }
        public static readonly int[] CubeTriangles={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};

        sealed class Builder
        {
            public int district;public Vector3 origin;
            public readonly List<MayhemModule> modules=new();
            public MayhemModule Module(Vector3 p,string purpose,float heading=0)
            {var m=new MayhemModule{district=district,position=origin+p,purpose=purpose,rotation=Quaternion.Euler(0,heading,0)};modules.Add(m);return m;}
            public MayhemModule Box(Vector3 p,Vector3 size,int finish,string purpose,float mass=0)
            {var m=Module(p,purpose);Part(m,Vector3.zero,size,finish);m.mass=mass;return m;}
            public void Part(MayhemModule m,Vector3 p,Vector3 size,int finish,Quaternion? rotation=null)
            {m.shapes.Add(new MayhemShape(p,size,finish,rotation??Quaternion.identity));}
            public void Beam(MayhemModule m,Vector3 a,Vector3 c,float width,float thickness,int finish)
            {var delta=c-a;Part(m,(a+c)*.5f,new Vector3(width,thickness,delta.magnitude),finish,Quaternion.LookRotation(delta,Vector3.up));}
            public void Ramp(MayhemModule m,Vector3 a,Vector3 c,float width=2.5f,int finish=2)
            {
                if(a.y>c.y){var swap=a;a=c;c=swap;}
                var q=Quaternion.LookRotation(c-a,Vector3.up);float length=(c-a).magnitude;
                if(Mathf.Abs(a.y-c.y)<.001f){Part(m,(a+c)*.5f-Vector3.up*.16f,new Vector3(width,.32f,length),finish,q);return;}
                float x=width*.5f,z=length*.5f;
                var vertices=new[]{new Vector3(-x,0,-z),new Vector3(x,0,-z),new Vector3(x,0,-z),new Vector3(-x,0,-z),new Vector3(-x,-.32f,z),new Vector3(x,-.32f,z),new Vector3(x,0,z),new Vector3(-x,0,z)};
                m.shapes.Add(new MayhemShape((a+c)*.5f,new Vector3(width,.32f,length),finish,q,vertices));
            }
            public MayhemModule Link(Vector3 a,Vector3 c,float width=3)
            {var shorten=(c-a).normalized*.12f;a+=shorten;c-=shorten;var mid=(a+c)*.5f;var m=Module(mid,"Loose route bridge");Ramp(m,a-mid+Vector3.up*.65f,c-mid+Vector3.up*.65f,width);return m;}
            public void Flight(MayhemModule m,Vector3 a,Vector3 c,float landing,float width,bool lowerLanding=true)
            {
                var delta=Vector3.ProjectOnPlane(c-a,Vector3.up);float run=delta.magnitude,rise=c.y-a.y,x=width*.5f;
                var q=Quaternion.LookRotation(delta,Vector3.up);
                if(lowerLanding)Part(m,a-q*new Vector3(0,.16f,landing*.5f),new Vector3(width,.32f,landing),2,q);
                var vertices=new[]{new Vector3(-x,0,0),new Vector3(x,0,0),new Vector3(x,rise,run),new Vector3(-x,rise,run),new Vector3(-x,rise-.32f,run+landing),new Vector3(x,rise-.32f,run+landing),new Vector3(x,rise,run+landing),new Vector3(-x,rise,run+landing)};
                m.shapes.Add(new MayhemShape(a,new Vector3(width,rise,run+landing),2,q,vertices));
            }
            public void Pier(Vector3 p,float height,float width=4)
            {Box(p+Vector3.up*(height*.5f),new Vector3(width,height,width),0,"Bearing pier");}
        }

        static void Bookends(Builder b)
        {
            int[] tiers={2,3,4,3,3,4,3,2};
            for(int i=0;i<8;i++)
            {
                float x=(i%4-1.5f)*34,z=(i/4-.5f)*35;float heading=i%2==0?0:180;
                b.Box(new Vector3(x+((i%2==0)?3:-3),.5f,z),new Vector3(13,1,14),1,"Bookend bearing plinth");
                for(int t=0;t<tiers[i];t++)
                {
                    var m=b.Module(new Vector3(x,1+t*11.15f,z),"Three-floor comb",heading);
                    b.Part(m,new Vector3(0,5.575f,0),new Vector3(6,11.15f,14),0);
                    for(int f=0;f<3;f++)
                    {
                        float y=3.5f*(f+1);
                        b.Part(m,new Vector3(6,y+.325f,0),new Vector3(18,.65f,14),f%2==0?1:2);
                        float low=f==0?0:3.5f*f+.65f;
                        // Flights meet floor edges; the opposite spine face remains entirely flush.
                        float lane=f%2==0?7.9f:-7.9f;
                        var a=new Vector3(f%2==0?5.3f:13,low,lane);var c=new Vector3(f%2==0?13:5.3f,y+.65f,lane);
                        b.Flight(m,a,c,2,2.6f,false);
                        b.Part(m,new Vector3(f%2==0?4.3f:14,low-.16f,f%2==0?8.1f:-8.1f),new Vector3(2,.32f,2.2f),2);
                    }
                }
            }
            for(int row=0;row<2;row++)for(int col=0;col<3;col++)for(int level=0;level<Mathf.Min(tiers[row*4+col],tiers[row*4+col+1]);level++)
            {
                float x=(col-1.5f)*34,z=(row-.5f)*35;float y=5.15f+level*11.15f;
                if(col%2==0)b.Link(new Vector3(x+13,y,z-3),new Vector3(x+21,y,z-3),3);
            }
        }
        static void Mesas(Builder b)
        {
            for(int site=0;site<6;site++)
            {
                float x=(site%3-1)*33,z=(site/3-.5f)*38;int tiers=site==1||site==4?3:2;
                for(int t=0;t<tiers;t++)
                {
                    float baseY=t*9.7f;var crown=b.Module(new Vector3(x,baseY+9,z),"Three-branch mesa");
                    for(int arm=0;arm<3;arm++)
                    {
                        var q=Quaternion.Euler(0,arm*120,0);var end=q*new Vector3(0,0,11);
                        b.Box(new Vector3(x,baseY+4.5f,z)+end,new Vector3(3.5f,9,3.5f),0,"Mesa leg");
                        b.Part(crown,q*new Vector3(0,.35f,5),new Vector3(5,.7f,16),arm%2+1,q);
                        if(arm==0)b.Ramp(crown,q*new Vector3(-3.5f,-9,-3),q*new Vector3(-3.5f,.7f,12.3f),2.6f);
                    }
                }
            }
            for(int i=0;i<3;i++){float top=(i==1?3:2)*9.7f;b.Link(new Vector3((i-1)*33,top,-8),new Vector3((i-1)*33+8.66f,top,14),3);}
        }
        static void Rings(Builder b)
        {
            for(int site=0;site<3;site++)
            {
                float x=(site-1)*38;int levels=site==1?11:8;
                for(int level=0;level<levels;level++)
                {
                    float y=level*3.7f;var m=b.Module(new Vector3(x,y,0),"Open C terrace");
                    // Fixed corner supports avoid rotating solid piers into the floor below.
                    for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)b.Part(m,new Vector3(sx*9,1.5f,sz*9),new Vector3(2,3,2),0);
                    var q=Quaternion.Euler(0,level*90,0);
                    foreach(var p in new[]{new Vector3(0,3.35f,-9),new Vector3(-9,3.35f,0),new Vector3(9,3.35f,0)})
                        b.Part(m,q*p,new Vector3(p.x==0?20:2,.7f,p.x==0?2:20),level%2+1,q);
                    // A shallow exterior flight crosses one side; the missing quarter stays open.
                    b.Ramp(m,q*new Vector3(-9,0,11),q*new Vector3(9,3.7f,11),3);
                    b.Part(m,q*new Vector3(9,3.35f,11),new Vector3(4,.7f,4),2,q);
                }
            }
            for(int side=-1;side<=1;side+=2)for(int level=1;level<=3;level+=2)b.Link(new Vector3(side*9,level*3.7f,0),new Vector3(side*29,level*3.7f,0),2.5f);
        }
        static void Ribbons(Builder b)
        {
            for(int row=0;row<6;row++)for(int cell=0;cell<6;cell++)
            {
                float x=(cell-2.5f)*19,z=(row-2.5f)*17;
                float baseY=(Mathf.Min(cell,5-cell)+Mathf.Min(row,5-row))*3;
                var m=b.Module(new Vector3(x,baseY,z),"Rigid folded ribbon");
                b.Ramp(m,new Vector3(-9.5f,0,0),new Vector3(0,3,0),8,1);
                b.Ramp(m,new Vector3(0,3,0),new Vector3(9.5f,0,0),8,2);
                if(baseY>0)
                {
                    b.Pier(new Vector3(x-8,0,z),baseY,3);b.Pier(new Vector3(x+8,0,z),baseY,3);
                }
                if(row<5&&cell%2==row%2)
                {float next=(Mathf.Min(cell,5-cell)+Mathf.Min(row+1,4-row))*3;b.Link(new Vector3(x,baseY+3,z+3.5f),new Vector3(x,next+3,z+13.5f),3);}
                if(cell<5)
                {float next=(Mathf.Min(cell+1,4-cell)+Mathf.Min(row,5-row))*3;b.Link(new Vector3(x,baseY+3,z),new Vector3(x+19,next+3,z),2.5f);}
            }
        }
    }
}

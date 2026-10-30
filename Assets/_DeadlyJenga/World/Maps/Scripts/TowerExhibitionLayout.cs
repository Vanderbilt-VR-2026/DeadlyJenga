using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Each section is a fabricated rigid part. Sections bear on other sections, never on invisible anchors.
    public static partial class TowerExhibitionLayout
    {
        public static readonly string[] CityNames={"Crownline Offices","Lantern Court Apartments","Split Atrium Hotel","Switchback Parking Tower","Central Exchange","Lantern City Hall","Foundry Lofts","Skygarden Residences","Harbor Waterworks","Gateway Headquarters"};
        public static readonly string[] TowerNames={"Sky Tree","Zipper Spire","Spiral Lantern","Cross-Block Relay","Cage Cathedral","Drop-Stop Tower","Saddle Beacon","Chalice Stack","Bookfall Keep","Kissing Towers"};
        public static readonly Vector3[] Centers={new(-256,0,-110),new(-128,0,-110),new(0,0,-110),new(128,0,-110),new(256,0,-110),new(-256,0,110),new(-128,0,110),new(0,0,110),new(128,0,110),new(256,0,110)};
        public static string[] Names(bool city)=>city?CityNames:TowerNames;
        public static List<MayhemModule> Build(bool city,int district=-1)
        {
            if(district< -1||district>9)throw new ArgumentOutOfRangeException(nameof(district));
            var b=new Builder();
            for(int d=0;d<10;d++)
            {
                if(district>=0&&district!=d)continue;b.d=d;b.origin=Centers[d];
                if(city)City(b,d);else Tower(b,d);
            }
            return b.modules;
        }
        sealed class Builder
        {
            public int d;public Vector3 origin;
            public readonly List<MayhemModule> modules=new();
            public MayhemModule Module(Vector3 p,string purpose,float heading=0)
            {var m=new MayhemModule{district=d,position=origin+p,purpose=purpose,rotation=Quaternion.Euler(0,heading,0)};modules.Add(m);return m;}
            public void Part(MayhemModule m,Vector3 p,Vector3 size,int finish=0,Quaternion? rotation=null)
            {if(size.x>.001f&&size.y>.001f&&size.z>.001f)m.shapes.Add(new MayhemShape(p,size,finish,rotation??Quaternion.identity));}
            public MayhemModule Box(Vector3 p,Vector3 size,string purpose,int finish=0)
            {var m=Module(p,purpose);Part(m,Vector3.zero,size,finish);return m;}
            public void Beam(MayhemModule m,Vector3 a,Vector3 c,float w,float thickness,int finish=0)
            {var v=c-a;Part(m,(a+c)*.5f,new Vector3(w,thickness,v.magnitude),finish,Quaternion.LookRotation(v,Vector3.up));}
            public void Flight(MayhemModule m,Vector3 a,Vector3 c,float width=3)
            {
                var run=Vector3.ProjectOnPlane(c-a,Vector3.up);var q=Quaternion.LookRotation(run,Vector3.up);float x=width*.5f,z=run.magnitude,rise=c.y-a.y;
                var v=new[]{new Vector3(-x,0,0),new Vector3(x,0,0),new Vector3(x,rise-.28f,z),new Vector3(-x,rise-.28f,z),new Vector3(-x,0,0),new Vector3(x,0,0),new Vector3(x,rise,z),new Vector3(-x,rise,z)};
                // Flip winding to face outwards; the convex points and absolute volume stay identical.
                for(int i=0;i<v.Length;i+=2)(v[i],v[i+1])=(v[i+1],v[i]);
                m.shapes.Add(new MayhemShape(a,new Vector3(width,rise+.28f,z),2,q,v));
            }
            // Four strips leave an actual stair opening, with 2.5 m flat pads at either end.
            public void Deck(MayhemModule m,float y,float w,float depth,float lane,int finish=1)
            {
                float hole=5.5f,near=lane-3.65f,far=lane+3.65f;
                Part(m,new Vector3(0,y-.35f,(-depth*.5f+near)*.5f),new Vector3(w,.7f,near+depth*.5f),finish);
                Part(m,new Vector3(0,y-.35f,(far+depth*.5f)*.5f),new Vector3(w,.7f,depth*.5f-far),finish);
                foreach(int s in new[]{-1,1})Part(m,new Vector3(s*(w*.5f+hole)*.5f,y-.35f,lane),new Vector3(w*.5f-hole,.7f,7.3f),finish);
            }
            public MayhemModule Cassette(Vector3 p,float w,float depth,int floors=3,float heading=0,string purpose="Inhabited cassette",bool cover=true)
            {
                var m=Module(p,purpose,heading);float h=4*floors,lane=-depth*.5f+7;
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Part(m,new Vector3(x*(w*.5f-1.25f),h*.5f,z*(depth*.5f-1.25f)),new Vector3(2.5f,h,2.5f));
                if(cover)Part(m,new Vector3(0,h*.5f,depth*.5f-1.25f),new Vector3(Mathf.Min(7,w-6),h,2.5f));
                for(int f=0;f<floors;f++)
                {
                    float y=f*4;Deck(m,y+4,w-1,depth-1,lane,f%2==0?1:3);
                    float a=f%2==0?-5.5f:5.5f,z=lane+(f%2==0?-2:2);Flight(m,new Vector3(a,y,z),new Vector3(-a,y+4,z));
                }
                return m;
            }
            public float Stack(Vector3 p,int sections,float w=24,float depth=24,float heading=0,string purpose="Three-floor city section")
            {
                Box(p+new Vector3(0,.4f,0),new Vector3(w+1,.8f,depth+1),"Loose bearing raft",3);
                for(int t=0;t<sections;t++)Cassette(p+Vector3.up*(.8f+t*12),w,depth,3,heading,purpose);
                return .8f+sections*12;
            }
            public void Bridge(Vector3 a,Vector3 c,float width=4,string purpose="Loose bridge on end bearings")
            {
                var m=Module((a+c)*.5f+Vector3.up*.35f,purpose);Beam(m,a-m.position+origin+Vector3.up*.35f,c-m.position+origin+Vector3.up*.35f,width,.7f,2);
                var direction=Vector3.ProjectOnPlane(c-a,Vector3.up).normalized;
                Flight(m,a-direction*2-m.position+origin,a+Vector3.up*.7f-m.position+origin,width);
                Flight(m,c+direction*2-m.position+origin,c+Vector3.up*.7f-m.position+origin,width);
            }
            public void Ring(MayhemModule m,float y,float outer,float inner,int finish=1,int sides=8,float gap=-1)
            {
                float r=(outer+inner)*.5f,length=2*r*Mathf.Tan(Mathf.PI/sides);
                for(int i=0;i<sides;i++)
                {
                    if(i==gap)continue;float angle=360f*i/sides;var q=Quaternion.Euler(0,angle,0);
                    Part(m,q*new Vector3(0,y-.35f,r),new Vector3(length,.7f,outer-inner),finish,q);
                }
            }
        }
        static void City(Builder b,int d)
        {
            switch(d)
            {
                case 0:
                    b.Box(new(0,.4f,0),new(35,.8f,32),"Street podium",3);
                    for(int t=0;t<5;t++)b.Cassette(new(0,.8f+12*t,0),34-t*3,30-t*2,3,0,"Setback office floors");
                    var crown=b.Module(new(0,60.8f,0),"Lavender office crown");
                    foreach(int s in new[]{-1,1})b.Part(crown,new(s*5,2,0),new(2,4,8),2);b.Part(crown,new(0,4.35f,0),new(12,.7f,8),2);break;
                case 1:
                    for(int side=-1;side<=1;side+=2)b.Stack(new(side*25,0,0),3,22,48,0,"Courtyard apartment wing");
                    b.Stack(new(0,0,26),3,26,20,0,"Rear apartment gallery");
                    foreach(int side in new[]{-1,1})for(int t=0;t<3;t++)b.Bridge(new(side*16,12.8f+t*12,20),new(side*11,12.8f+t*12,20),2.5f);break;
                case 2:
                    foreach(int s in new[]{-1,1})b.Stack(new(s*20,0,0),4,24,32,0,"Independent hotel wing");
                    for(int t=1;t<=3;t++)b.Bridge(new(-10,.8f+12*t,7),new(10,.8f+12*t,7),5,"Atrium crossover");break;
                case 3:
                    b.Stack(Vector3.zero,3,38,32,0,"Open parking switchback deck");break;
                case 4:
                    b.Stack(new(-23,0,0),4,24,30,0,"Exchange west tower");b.Stack(new(23,0,0),3,24,30,0,"Exchange east tower");
                    for(int t=0;t<2;t++)b.Bridge(new(-13,8.8f+t*12,0),new(13,8.8f+t*12,0),20,"Roofed concourse mezzanine");break;
                case 5:
                    b.Stack(new(-16,0,0),2,22,30,0,"Civic arcade");b.Stack(new(16,0,0),3,22,30,0,"Clock tower stack");
                    b.Bridge(new(-7,12.8f,7),new(7,12.8f,7),8,"Roof over public square");
                    var lantern=b.Cassette(new(16,36.8f,0),18,20,1,0,"Open lantern crown",false);
                    foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})b.Part(lantern,new(x*7.75f,6,z*8.75f),new(2.5f,4,2.5f));
                    b.Part(lantern,new(0,8.35f,0),new(20,.7f,22),2);break;
                case 6:
                    b.Stack(Vector3.zero,3,40,30,0,"Tall factory hall");
                    var roof=b.Module(new(0,37.1f,0),"Three large sawtooth roof bays");
                    for(int i=-1;i<=1;i++)b.Beam(roof,new(i*12-5,0,-13),new(i*12+5,4,-13),26,.6f,3);break;
                case 7:
                    b.Stack(new(0,0,0),5,26,26,0,"Recessed residential galleries");
                    foreach(int s in new[]{-1,1})b.Stack(new(s*25,0,0),s<0?2:3,22,26,0,"Lower sky garden shoulder");
                    b.Bridge(new(-15,24.8f,5),new(-10,24.8f,5),6);b.Bridge(new(10,36.8f,5),new(15,36.8f,5),6);break;
                case 8:
                    b.Stack(Vector3.zero,3,20,22,0,"Waterworks service tower");
                    var carrier=b.modules.FindLast(m=>m.purpose=="Waterworks service tower");b.Deck(carrier,12,34,34,-4,3);
                    var tank=b.Cassette(new(0,36.8f,0),32,32,2,0,"Hollow occupied water crown");
                    foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})b.Part(tank,new(x*14.75f,10,z*14.75f),new(2.5f,4,2.5f));
                    b.Part(tank,new(0,12.4f,0),new(34,.8f,34),3);break;
                case 9:
                    foreach(int s in new[]{-1,1})b.Stack(new(s*25,0,0),5,26,30,0,"Independent headquarters stack");
                    var bridge=b.Cassette(new(0,37.5f,0),24,24,2,0,"Inhabited loose skybridge");
                    // The bridge seats on the adjacent towers' broad floor decks.
                    b.Part(bridge,new(0,-.35f,0),new(28,.7f,24),3);break;
            }
        }
    }
}

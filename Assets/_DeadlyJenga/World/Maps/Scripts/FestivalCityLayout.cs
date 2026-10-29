using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public readonly struct CityPart
    {
        public readonly Vector3 Position,Size;
        public readonly int Finish;
        public readonly bool Solid,Ramp;
        public CityPart(Vector3 position,Vector3 size,int finish,bool solid=true,bool ramp=false)
        {Position=position;Size=size;Finish=finish;Solid=solid;Ramp=ramp;}
        public Bounds Bounds=>Ramp?new Bounds(Position+Vector3.up*(Size.y-.3f)*.5f,new Vector3(Size.x,Size.y+.3f,Size.z)):new Bounds(Position,Size);
    }

    // Buildings use solid room sections, with independent seated flights and bridges.
    public static class FestivalCityLayout
    {
        public static readonly Vector2 Hall=new Vector2(65,-4),House=new Vector2(-65,2);
        static CityPart Box(float x,float y,float z,float sx,float sy,float sz,int finish=0,bool solid=true)
            =>new CityPart(new Vector3(x,y,z),new Vector3(sx,sy,sz),finish,solid);
        static void Member(List<SparseMember> into,Vector2 p,float y,float heading,List<CityPart> parts,string role)
            =>into.Add(new SparseMember(new Vector3(p.x,y,p.y),Quaternion.Euler(0,heading,0),parts.ToArray(),role));
        public static void AddTo(List<SparseMember> into)
        {
            Tower(into,Hall,12,22,6,false,"Lantern Hall");
            Tower(into,House,8,22,6,true,"Setback House");
            Wing(into,House+Vector2.up*15,14,8,3,"Setback terrace wing");
            Bridge(into,new Vector2(45,0),new Vector2(54,0),22.5f,2,"Hall west street");
            Bridge(into,new Vector2(61,-55),new Vector2(61,-15),30,2,"Hall south street");
            Bridge(into,new Vector2(-58,0),new Vector2(-45,0),22.5f,2,"House east street");
            // The promenade joins both towers to the existing sand footprint, not another island.
            into.Add(new SparseMember(new Vector3(0,.1f,77.55f),new Vector3(12,.2f,24.9f),CastleSection.Keep,"City grounded promenade"));
            Gate(into,new Vector2(-12,82),7,-90,"West gate");Gate(into,new Vector2(12,82),6,90,"East gate");
            Member(into,new Vector2(0,82),30,0,new List<CityPart>{Box(0,.625f,0,12,1.25f,4,2),Box(0,-.4f,-2.6f,2.5f,.8f,1.2f)},"Gate cross street");
            Bridge(into,new Vector2(0,65),new Vector2(0,80),30,2,"Gate approach street");
            for(int tier=0;tier<6;tier++)
            {
                Ramp(into,new Vector2(-6,77.5f),new Vector2(6,77.5f),tier*5+1.25f,tier*5+3.75f,2,"Gate rising flight");
                Ramp(into,new Vector2(6,86.5f),new Vector2(-6,86.5f),tier*5+3.75f,tier*5+6.25f,2,"Gate return flight");
            }
            for(int i=0;i<3;i++)into.Add(new SparseMember(new Vector3(-3.5f-i,.2f+(i+1)*.35f*.5f,82),new Vector3(1,(i+1)*.35f,2.5f),CastleSection.Keep,"Gate entry step"));
            StackRoomSections(into);
        }
        // Four-gallery manufactured sections reduce the number of load-bearing seams.
        static void StackRoomSections(List<SparseMember> into)
        {
            var output=new List<SparseMember>();var used=new bool[into.Count];
            for(int i=0;i<into.Count;i++)
            {
                if(used[i])continue;var a=into[i];
                if(i<512||a.Parts==null||!a.Role.EndsWith(" room")){output.Add(a);continue;}
                int partner=-1;
                for(int j=i+1;j<into.Count;j++)
                {
                    var b=into[j];
                    if(!used[j]&&b.Parts!=null&&b.Role==a.Role&&b.Rotation==a.Rotation&&Mathf.Abs(b.Position.x-a.Position.x)<.001f&&Mathf.Abs(b.Position.z-a.Position.z)<.001f&&Mathf.Abs(b.Position.y-a.Position.y-5)<.001f&&Mathf.FloorToInt(b.Position.y/10)==Mathf.FloorToInt(a.Position.y/10)){partner=j;break;}
                }
                if(partner<0){output.Add(a);continue;}
                used[partner]=true;var parts=new List<CityPart>(a.Parts);
                foreach(var p in into[partner].Parts)
                {
                    var shifted=new CityPart(p.Position+Vector3.up*5,p.Size,p.Finish,p.Solid,p.Ramp);bool merged=false;
                    if(p.Solid&&!p.Ramp&&p.Finish==0)
                    for(int k=0;k<parts.Count;k++)
                    {
                        var q=parts[k];
                        if(q.Solid&&!q.Ramp&&q.Finish==0&&Mathf.Abs(q.Position.x-shifted.Position.x)<.001f&&Mathf.Abs(q.Position.z-shifted.Position.z)<.001f&&q.Size.x==shifted.Size.x&&q.Size.z==shifted.Size.z&&Mathf.Abs(q.Position.y+q.Size.y/2-shifted.Position.y+shifted.Size.y/2)<.001f)
                        {parts[k]=new CityPart(new Vector3(q.Position.x,(q.Position.y-q.Size.y/2+shifted.Position.y+shifted.Size.y/2)/2,q.Position.z),new Vector3(q.Size.x,q.Size.y+shifted.Size.y,q.Size.z),0);merged=true;break;}
                    }
                    if(!merged)parts.Add(shifted);
                }
                output.Add(new SparseMember(a.Position,a.Rotation,parts.ToArray(),a.Role));
            }
            into.Clear();into.AddRange(output);
        }
        static void Tower(List<SparseMember> into,Vector2 p,int tiers,float width,float ring,bool setback,string name)
        {
            for(int tier=0;tier<tiers;tier++)
            {
                float w=setback&&tier>=4?14:width,r=setback&&tier>=4?4:ring,inner=w*.5f-r;
                float nextInner=setback&&tier==3?3:inner,previousInner=setback&&tier==4?5:inner;
                for(int side=0;side<4;side++)
                {
                    var parts=Room(w,r);
                    if(name=="Lantern Hall"&&(tier==3||tier==7||tier==10))Balcony(parts,w,r);
                    if(setback&&tier==3)parts.Add(Box(-r*.5f,4.9875f,w*.5f-r*.5f,w-r,.025f,r,1));
                    // These ledges sit below the walking surface; the flights have flush tops.
                    if(side==0||side==2)Seats(parts,inner,side==0?3.75f:1.25f,side==0?nextInner*.5f:inner*.5f,side==0?2.5f/(inner*2):2.5f/(previousInner+inner));
                    if(name=="Lantern Hall"&&side==3&&tier==4)Corbel(parts,w,r,2.5f);
                    if(name=="Lantern Hall"&&side==2&&tier==6)Corbel(parts,w,r,0);
                    if(name=="Setback House"&&side==1&&tier==4)Corbel(parts,w,r,2.5f);
                    Member(into,p,tier*5,side*90,parts,name+" room");
                }
                float lane=inner*.5f,flightWidth=Mathf.Min(2,inner*.6f);
                Ramp(into,p+new Vector2(-lane,-inner),p+new Vector2(-lane,inner),tier*5+1.25f,tier*5+3.75f,flightWidth,name+" lower flight");
                if(tier+1<tiers)
                    Ramp(into,p+new Vector2(nextInner*.5f,inner),p+new Vector2(nextInner*.5f,-nextInner),tier*5+3.75f,tier*5+6.25f,flightWidth,name+" upper flight");
            }
            Crown(into,p,tiers*5,setback?14:width,name);
            // Ground entry is separate from the building's bearing walls.
            float outer=width*.5f;
            for(int step=0;step<2;step++)
                into.Add(new SparseMember(new Vector3(p.x+(setback?-1:1)*(outer-ring-1),(step==0?.6f:1.25f)*.5f,p.y+(setback?-1:1)*(outer+1.5f-step)),new Vector3(1.8f,step==0?.6f:1.25f,1),CastleSection.Keep,name+" entrance step"));
        }
        static List<CityPart> Room(float width,float ring)
        {
            float r=width*.5f,length=width-ring,end=r-ring;
            var parts=new List<CityPart>{
                Box(-ring*.5f,1.05f,r-ring*.5f,length,.4f,ring,1),
                Box(-ring*.5f,3.55f,r-ring*.5f,length,.4f,ring,1),
                Box((-r+end-2)*.5f,2.5f,r-.4f,length-2,5,.8f),
                Box(-r+.4f,2.5f,r-ring*.5f,.8f,5,ring-.8f)
            };
            // Closed glazed panels are artwork on solid walls, not fake traversable openings.
            foreach(float y in new[]{2.2f,4.35f})foreach(float side in new[]{-1f,1f})
                parts.Add(Box(-ring*.5f-1+side*(length-2)*.23f,y,r+.09f,Mathf.Min(1.8f,(length-2)*.17f),1.45f,.04f,3,false));
            return parts;
        }
        static void Balcony(List<CityPart> parts,float width,float ring)
        {
            float r=width*.5f,length=width-ring;
            parts.Add(Box(-ring*.5f,1.05f,r+1,length,.4f,2,1));
            parts.Add(Box(-r-1,1.05f,r-ring*.5f+1,2,.4f,ring+2,1));
            parts.Add(Box(-ring*.5f,1.6f,r+1.8f,length,.7f,.3f,2));
            parts.Add(Box(-r-1.8f,1.6f,r-ring*.5f+1,.3f,.7f,ring+2,2));
        }
        static void Seats(List<CityPart> parts,float inner,float top,float startLane,float endSlope)
        {
            float lane=inner*.5f,width=Mathf.Min(2,inner*.6f),slope=2.5f/(inner*2);
            parts.Add(Box(-lane,top-.3f-endSlope*.5f-.15f,inner-.25f,width+.2f,.3f,.5f,2));
            parts.Add(Box(startLane,top-.3f-.15f,inner-.25f,width+.2f,.3f,.5f,2));
        }
        static void Corbel(List<CityPart> parts,float width,float ring,float level)
        {
            float r=width*.5f,door=r-ring-1;
            parts.Add(Box(door,level-.4f,r+.6f,2.2f,.8f,1.2f,0));
        }
        static void Wing(List<SparseMember> into,Vector2 p,float width,float depth,int tiers,string name)
        {
            for(int tier=0;tier<tiers;tier++)
            {
                var parts=new List<CityPart>{Box(0,1.05f,0,width,.4f,depth,1),Box(0,3.55f,0,width,.4f,depth,1),Box(0,2.5f,depth*.5f-.4f,width,5,.8f),Box(-width*.5f+.4f,2.5f,0,.8f,5,depth-.8f),Box(width*.5f-.4f,2.5f,0,.8f,5,depth-.8f)};
                parts.Add(Box(0,2.2f,depth*.5f+.025f,width*.55f,1.2f,.04f,3,false));
                Member(into,p,tier*5,0,parts,name+" room");
            }
            into.Add(new SparseMember(new Vector3(p.x,tiers*5+.625f,p.y),new Vector3(width,1.25f,depth),CastleSection.Wall,name+" roof"));
        }
        static void Gate(List<SparseMember> into,Vector2 p,int tiers,float rotation,string name)
        {
            for(int tier=0;tier<tiers;tier++)
            {
                var parts=new List<CityPart>{Box(0,1.05f,0,12,.4f,12,1),Box(0,3.55f,0,12,.4f,12,1),Box(0,2.5f,5.6f,12,5,.8f),Box(-5.6f,2.5f,0,.8f,5,11.2f),Box(5.6f,2.5f,0,.8f,5,11.2f)};
                parts.Add(Box(0,2.1f,6.025f,5,1.3f,.04f,3,false));
                if(rotation<0)
                {
                    parts.Add(Box(-4.5f,.8f,-6.25f,2.2f,.3f,.5f,2));
                    parts.Add(Box(4.5f,1.25f-.3f-2.5f/12*.5f-.15f,-6.25f,2.2f,.3f,.5f,2));
                }
                else
                {
                    parts.Add(Box(-4.5f,3.75f-.3f-.15f,-6.25f,2.2f,.3f,.5f,2));
                    parts.Add(Box(4.5f,3.75f-.3f-2.5f/12*.5f-.15f,-6.25f,2.2f,.3f,.5f,2));
                }
                if(tier==tiers-1)parts.Add(Box(0,(rotation<0?0:5)-.4f,-6.6f,5,.8f,1.2f,0));
                Member(into,p,tier*5,rotation,parts,name+" room");
            }
            Crown(into,p,tiers*5,12,name);
        }
        static void Crown(List<SparseMember> into,Vector2 p,float y,float width,string name)
        {
            into.Add(new SparseMember(new Vector3(p.x,y+.625f,p.y),new Vector3(width,1.25f,width),CastleSection.Wall,name+" roof"));
            if(name=="Lantern Hall")
            {
                Member(into,p,y+1.25f,0,new List<CityPart>{Box(0,.5f,0,9,1,9,2),Box(0,2.3f,0,4,3.6f,4,4),Box(0,4.2f,0,7,.4f,7,1)},name+" lantern");
            }
            else into.Add(new SparseMember(new Vector3(p.x,y+2,p.y),new Vector3(width*.6f,1.5f,width*.6f),CastleSection.Keep,name+" crown"));
        }
        public static void Bridge(List<SparseMember> into,Vector2 a,Vector2 b,float bottom,float width,string name)
        {
            var d=b-a;float angle=Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg;
            into.Add(new SparseMember(new Vector3((a.x+b.x)*.5f,bottom+.625f,(a.y+b.y)*.5f),new Vector3(width,1.25f,d.magnitude),Quaternion.Euler(0,angle,0),CastleSection.Keep,name));
        }
        static void Ramp(List<SparseMember> into,Vector2 a,Vector2 b,float low,float high,float width,string name)
        {
            if(high<low){var t=a;a=b;b=t;float f=low;low=high;high=f;}
            var d=b-a;
            Member(into,(a+b)*.5f,low,Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg,new List<CityPart>{new CityPart(Vector3.zero,new Vector3(width,high-low,d.magnitude),2,true,true)},name);
        }
    }
}


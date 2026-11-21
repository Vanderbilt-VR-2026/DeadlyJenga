using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy Sky Ring City personality, one landmark idea per district:
    //   Downtown: candy-cane catch frames, a gold onion spire and a glass elevator that rides up and down.
    //   Sugar Market: candy-cane corner posts, glowing shop signs, café tables and a giant rooftop lollipop.
    //   Cloud Harbor: a gold anchor facing the plaza, framed warehouse doors, cargo crates and a rooftop crane.
    //   Grape Heights keep: corner turrets with cone roofs and long heart banners.
    //   Canyon towers: gumdrop domes on their climb-only crowns.
    // Every open stair hole on a roof gets a striped pavilion, so roofs read finished.
    // Art only, no colliders; everything sits on its own body's visual, so it falls with the block.
    public sealed partial class FantasyMapArt
    {
        static readonly Color CaneRed=new(.92f,.12f,.2f),GrapeDark=new(.42f,.2f,.62f),Crate=new(.78f,.55f,.32f),CrateDark=new(.55f,.36f,.2f);
        readonly List<(Transform cabin,Vector3 bottom,Vector3 top,float phase)> elevators=new();

        // A body's boxes in its own frame.
        static List<(Vector3 c,Vector3 s)> Pieces(Rigidbody body)
        {
            var list=new List<(Vector3,Vector3)>();var frame=body.transform;
            foreach(var shape in body.GetComponentsInChildren<BoxCollider>())
            {
                var c=frame.InverseTransformPoint(shape.transform.TransformPoint(shape.center));
                var s=(Quaternion.Inverse(frame.rotation)*shape.transform.rotation)*shape.size;
                list.Add((c,new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z))));
            }
            return list;
        }

        // The top deck of a cassette and the stair opening left in it, in the body's frame.
        // Decks are four strips; the two beside the opening are 7.3 m long.
        static bool RoofHole(List<(Vector3 c,Vector3 s)> pieces,out Vector3 center,out Vector2 size)
        {
            center=default;size=default;float top=float.MinValue;
            foreach(var (c,s) in pieces)if(s.y>.6f&&s.y<.95f&&Mathf.Max(s.x,s.z)>6)top=Mathf.Max(top,c.y+s.y*.5f);
            if(top==float.MinValue)return false;
            for(int i=0;i<pieces.Count;i++)for(int j=i+1;j<pieces.Count;j++)
            {
                var (a,sa)=pieces[i];var (b,sb)=pieces[j];
                if(sa.y>.95f||sb.y>.95f||Mathf.Abs(a.y+sa.y*.5f-top)>.05f||Mathf.Abs(b.y+sb.y*.5f-top)>.05f)continue;
                if(Mathf.Abs(sa.z-7.3f)<.05f&&Mathf.Abs(sb.z-7.3f)<.05f&&Mathf.Abs(a.z-b.z)<.1f)
                {
                    float gap=Mathf.Abs(a.x-b.x)-(sa.x+sb.x)*.5f;if(gap<2)continue;
                    center=new Vector3((a.x+b.x)*.5f,top,a.z);size=new Vector2(gap,7.3f);return true;
                }
            }
            return false;
        }

        static Bounds PieceBounds(List<(Vector3 c,Vector3 s)> pieces)
        {
            var b=new Bounds(pieces[0].c,pieces[0].s);foreach(var (c,s) in pieces)b.Encapsulate(new Bounds(c,s));return b;
        }

        // The horizontal frame axis (±x or ±z) that points most toward (or away from) the city center.
        static Vector3 FaceToward(Transform frame,Vector3 world,bool away)
        {
            var d=frame.InverseTransformDirection(Services.World.Center-world);d.y=0;if(away)d=-d;
            return Mathf.Abs(d.x)>Mathf.Abs(d.z)?new Vector3(Mathf.Sign(d.x),0,0):new Vector3(0,0,Mathf.Sign(d.z));
        }

        bool Landmark(Rigidbody body,Transform visual,string site,string purpose,ArtMesh m)
        {
            var pieces=Pieces(body);if(pieces.Count==0)return false;
            var frame=body.transform;
            Vector3 V(Vector3 local)=>visual.InverseTransformPoint(frame.TransformPoint(local));
            var vq=Quaternion.Inverse(visual.rotation)*frame.rotation;
            var b=PieceBounds(pieces);bool any=false;
            bool roofOpen=RoofHole(pieces,out var hole,out var holeSize)&&!Covered(frame.TransformPoint(hole),body);
            if(roofOpen){Pavilion(m,V,vq,hole,holeSize,site.StartsWith("Plaza")?Gold:DistrictColor(site));any=true;}
            bool streetLevel=frame.TransformPoint(b.min).y<groundY+2;

            if(purpose=="Broad catch frame")
            {
                foreach(var (c,s) in pieces)if(s.y>10&&Mathf.Max(s.x,s.z)<4)CandyCane(m,V,vq,c,s);
                return true;
            }
            if(purpose=="Escape spine")
            {
                if(streetLevel){Elevator(body,visual,V,vq,b);any=true;}
                if(roofOpen){Spire(m,V,vq,frame,b);any=true;}
                return any;
            }
            if(purpose=="Climb-only candy crown"){CrownDome(m,V,vq,b);return true;}
            if(purpose=="Independent bookfall support spine")
            {
                if(roofOpen){foreach(var (c,s) in pieces)if(s.y>3&&s.x<3&&s.z<3)Turret(m,V(new Vector3(c.x,b.max.y,c.z)));}
                else if(!streetLevel)Banners(m,V,vq,b);
                return true;
            }
            if(!purpose.Contains(" floor "))return any;

            bool ground=purpose.EndsWith(" floor 1");
            float hx=b.extents.x,hz=b.extents.z,floorY=b.min.y;var mid=b.center;
            if(site.StartsWith("Sugar"))
            {
                if(ground)
                {
                    foreach(var (c,s) in pieces)if(s.y>3&&s.x<3&&s.z<3)CandyCane(m,V,vq,c,s);
                    foreach(int side in new[]{-1,1})
                    {
                        ShopSign(m,V,vq,new Vector3(mid.x,floorY+3.45f,mid.z+side*(hz+.12f)),side,site.GetHashCode()+side);
                        Cafe(m,V,frame,new Vector3(mid.x+5.5f,floorY,mid.z+side*(hz+3.2f)),body);
                        Cafe(m,V,frame,new Vector3(mid.x-5.5f,floorY,mid.z+side*(hz+3.2f)),body);
                    }
                }
                if(roofOpen&&RoofCorner(frame,body,b,hole,out var corner))Lollipop(m,V(corner));
                return true;
            }
            if(site.StartsWith("Cloud Harbor"))
            {
                if(ground)
                {
                    foreach(int side in new[]{-1,1})DoorFrame(m,V,vq,new Vector3(mid.x,floorY,mid.z+side*(hz-.55f)),side);
                    foreach(int side in new[]{-1,1})Cargo(m,V,frame,new Vector3(mid.x+side*(hx+2.6f),floorY,mid.z),body,side);
                }
                if(purpose.EndsWith(" floor 2"))
                {
                    var face=FaceToward(frame,frame.TransformPoint(mid),false);
                    var at=mid+Vector3.Scale(face,b.extents)+face*.2f;at.y=floorY+2.1f;
                    Anchor(m,V,vq,at,face);
                }
                if(roofOpen&&RoofCorner(frame,body,b,hole,out var corner))Crane(m,V,vq,corner,FaceToward(frame,frame.TransformPoint(mid),true));
                return true;
            }
            return any;
        }

        // Roof corner for a rooftop landmark: away from the stair hole, the flag corner and arriving bridges.
        bool RoofCorner(Transform frame,Rigidbody body,Bounds b,Vector3 hole,out Vector3 corner)
        {
            corner=default;float best=-1;
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                if(sx<0&&sz<0)continue; // the flag
                var p=new Vector3(b.center.x+sx*(b.extents.x-2),hole.y,b.center.z+sz*(b.extents.z-2));
                if(Blocked(frame.TransformPoint(p),body))continue;
                float d=new Vector2(p.x-hole.x,p.z-hole.z).magnitude;if(d>best){best=d;corner=p;}
            }
            return best>0;
        }

        void Pavilion(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Vector3 c,Vector2 size,Color tint)
        {
            float hx=size.x*.5f+.3f,hz=size.y*.5f+.3f,h=3.2f,slope=24;
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                var foot=c+new Vector3(sx*hx,0,sz*hz);
                m.Box(V(foot+Vector3.up*h*.5f),new Vector3(.22f,h,.22f),vq,0,Sugar);
                m.Sphere(V(foot+Vector3.up*h),Vector3.one*.22f,0,Gold,3,6);
            }
            // Peaked candy-striped canopy, ridge along the long side.
            float rise=Mathf.Tan(slope*Mathf.Deg2Rad)*hz,run=hz/Mathf.Cos(slope*Mathf.Deg2Rad)+.5f;
            int stripes=Mathf.Max(4,Mathf.RoundToInt(hx*2/1.1f));float w=(hx*2+.8f)/stripes;
            for(int i=0;i<stripes;i++)foreach(int sz in new[]{-1,1})
            {
                var p=c+new Vector3(-hx-.4f+(i+.5f)*w,h+rise*.5f,sz*hz*.5f);
                m.Box(V(p),new Vector3(w*1.01f,.1f,run),vq*Quaternion.Euler(sz*slope,0,0),0,i%2==0?tint:Sugar);
            }
            m.Box(V(c+Vector3.up*(h+rise+.05f)),new Vector3(hx*2+.9f,.16f,.16f),vq,0,Gold);
            foreach(int sx in new[]{-1,1})m.Sphere(V(c+new Vector3(sx*(hx+.45f),h+rise+.2f,0)),Vector3.one*.3f,0,Gold,4,8);
        }

        void CandyCane(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Vector3 c,Vector3 s)
        {
            int bands=Mathf.Max(2,Mathf.RoundToInt(s.y/.8f));float h=s.y/bands;
            for(int i=0;i<bands;i++)
                m.Box(V(c+Vector3.up*(-s.y*.5f+(i+.5f)*h)),new Vector3(s.x+.06f,h*1.01f,s.z+.06f),vq,0,i%2==0?CaneRed:Sugar);
        }

        void ShopSign(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Vector3 at,int side,int seed)
        {
            var n=new Vector3(0,0,side);
            m.Box(V(at),new Vector3(5.2f,1.1f,.16f),vq,0,Sugar);
            m.Box(V(at+n*.05f),new Vector3(5.4f,1.3f,.08f),vq,0,Gold);
            // A row of glowing "letters" of different widths.
            float x=-2.1f;var colors=new[]{new Color(1,.3f,.55f),new Color(.4f,.75f,1),new Color(1,.55f,.15f)};
            for(int i=0;x<2.1f;i++)
            {
                float w=.3f+Hash(new Vector3(seed,i,3))*.35f;if(x+w>2.2f)break;
                m.Box(V(at+n*.12f+new Vector3(x+w*.5f,0,0)),new Vector3(w,.55f,.06f),vq,1,colors[Mathf.Abs(seed)%3]);
                x+=w+.14f;
            }
        }

        // Café table with a striped umbrella, only where the street is clear.
        void Cafe(ArtMesh m,System.Func<Vector3,Vector3> V,Transform frame,Vector3 local,Rigidbody body)
        {
            var world=frame.TransformPoint(local);world.y=groundY;
            if(Physics.CheckBox(world+Vector3.up*1.6f,new Vector3(1.4f,1.4f,1.4f),Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore))return;
            var up=Quaternion.identity;local.y=frame.InverseTransformPoint(world).y;
            m.Cylinder(V(local),up,.08f,2.4f,0,Sugar,6);
            m.Cylinder(V(local+Vector3.up*.75f),up,.6f,.08f,0,Sugar,12);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;var tip=local+Vector3.up*2.75f;var rim=local+new Vector3(Mathf.Cos(a)*1.4f,2.2f,Mathf.Sin(a)*1.4f);
                var rim2=local+new Vector3(Mathf.Cos(a+Mathf.PI/4)*1.4f,2.2f,Mathf.Sin(a+Mathf.PI/4)*1.4f);
                m.Panel(V(tip),V(rim),V(rim2),V(tip),Vector3.up,Vector3.up,Vector3.up,Vector3.up,i%2==0?new Color(1,.36f,.62f):Sugar);
            }
            foreach(int s in new[]{-1,1})m.Cylinder(V(local+new Vector3(s*.95f,0,0)),up,.25f,.45f,0,new Color(.4f,.75f,1),10);
        }

        void Lollipop(ArtMesh m,Vector3 at)
        {
            var up=Quaternion.identity;
            m.Cylinder(at,up,.14f,6,0,Sugar,8);
            var head=at+Vector3.up*7.6f;
            m.Sphere(head,new Vector3(2.1f,2.1f,.5f),0,new Color(1,.36f,.62f),10,16);
            m.Sphere(head,new Vector3(1.45f,1.45f,.56f),0,Sugar,10,16);
            m.Sphere(head,new Vector3(.8f,.8f,.6f),0,new Color(.62f,.36f,.96f),8,12);
            m.Sphere(head,new Vector3(.3f,.3f,.64f),0,Sugar,6,8);
        }

        void DoorFrame(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Vector3 at,int side)
        {
            var n=new Vector3(0,0,side);
            foreach(int s in new[]{-1,1})m.Box(V(at+n*.05f+new Vector3(s*2.75f,1.2f,0)),new Vector3(.32f,2.4f,1.3f),vq,0,Gold);
            m.Box(V(at+n*.05f+Vector3.up*2.42f),new Vector3(5.8f,.36f,1.3f),vq,0,Gold);
            m.Box(V(at+n*.75f+Vector3.up*2.95f),new Vector3(4.6f,.5f,.1f),vq,1,new Color(1,.82f,.48f));
        }

        // A little pile of cargo crates and barrels beside a harbor warehouse.
        void Cargo(ArtMesh m,System.Func<Vector3,Vector3> V,Transform frame,Vector3 local,Rigidbody body,int seed)
        {
            var world=frame.TransformPoint(local);world.y=groundY;
            if(Physics.CheckBox(world+Vector3.up*1.6f,new Vector3(1.8f,1.4f,2.6f),frame.rotation,1<<Layers.Block,QueryTriggerInteraction.Ignore))return;
            local.y=frame.InverseTransformPoint(world).y;
            void CrateAt(Vector3 p,float s)
            {
                m.Box(V(p+Vector3.up*s*.5f),Vector3.one*s,Quaternion.identity,0,Crate);
                m.Box(V(p+Vector3.up*s*.5f),new Vector3(s*1.02f,s*.16f,s*1.02f),Quaternion.identity,0,CrateDark);
            }
            CrateAt(local+new Vector3(0,0,-1.2f),1.3f);CrateAt(local+new Vector3(0,0,.2f),1.1f);CrateAt(local+new Vector3(0,1.3f,-1.1f),.9f);
            foreach(float z in new[]{1.5f,2.4f})
            {
                m.Cylinder(V(local+new Vector3(.1f,0,z)),Quaternion.identity,.42f,1.1f,0,seed>0?new Color(.4f,.75f,1):new Color(1,.36f,.62f),12);
                m.Cylinder(V(local+new Vector3(.1f,.5f,z)),Quaternion.identity,.44f,.1f,0,Gold,12);
            }
        }

        // Gold anchor on the wall facing the plaza. at = point on the wall face, face = outward axis.
        void Anchor(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Vector3 at,Vector3 face)
        {
            var right=Vector3.Cross(Vector3.up,face);
            void Bar(Vector3 a,Vector3 c,float width)
            {
                var d=c-a;m.Box(V((a+c)*.5f),new Vector3(width,.22f,d.magnitude+width*.5f),vq*Quaternion.LookRotation(d,face),0,Gold);
            }
            Bar(at+Vector3.down*1.6f,at+Vector3.up*1.7f,.42f);                       // shank
            Bar(at+Vector3.up*1.25f-right*1.2f,at+Vector3.up*1.25f+right*1.2f,.36f); // stock
            m.Sphere(V(at+Vector3.up*2.15f),new Vector3(.45f,.45f,.2f),0,Gold,6,10); // ring
            Vector3 Arc(float deg)=>at+Vector3.up*-.3f+(right*Mathf.Cos(deg*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(deg*Mathf.Deg2Rad))*1.5f;
            for(float a=200;a<340;a+=20)Bar(Arc(a),Arc(a+20),.34f);
            foreach(float a in new[]{200f,340f})
            {
                var tip=Arc(a);var dir=(tip-(at+Vector3.up*-.3f)).normalized;
                Bar(tip,tip+Vector3.up*.6f+dir*.15f,.5f);
            }
        }

        void Crane(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Vector3 at,Vector3 outward)
        {
            var yellow=new Color(1,.82f,.3f);
            m.Box(V(at+Vector3.up*3),new Vector3(.5f,6,.5f),vq,0,yellow);
            var top=at+Vector3.up*6;var tip=top+outward*7+Vector3.up*.8f;var back=top-outward*2;
            var d=tip-back;m.Box(V((tip+back)*.5f),new Vector3(.4f,.4f,d.magnitude),vq*Quaternion.LookRotation(d),0,yellow);
            m.Box(V(back+Vector3.down*.4f),new Vector3(1.2f,.9f,1.2f),vq,0,new Color(.62f,.36f,.96f));
            m.Box(V(tip+Vector3.down*1.6f),new Vector3(.06f,3.2f,.06f),vq,0,new Color(.3f,.25f,.3f));
            m.Box(V(tip+Vector3.down*3.4f),new Vector3(1.1f,.9f,1.1f),vq,0,Crate);
        }

        void Turret(ArtMesh m,Vector3 at)
        {
            var up=Quaternion.identity;var green=new Color(.5f,.84f,.16f);
            m.Cylinder(at,up,1.75f,4.4f,0,green,14);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;m.Box(at+new Vector3(Mathf.Cos(a)*1.65f,4.75f,Mathf.Sin(a)*1.65f),new Vector3(.7f,.7f,.7f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,0),0,green);}
            m.Cone(at+Vector3.up*4.4f,up,2.15f,3.8f,0,GrapeDark,14);
            m.Sphere(at+Vector3.up*8.3f,Vector3.one*.3f,0,Gold,4,8);
            m.Box(at+new Vector3(0,9.2f,0),new Vector3(.08f,1.8f,.08f),up,0,Sugar);
            m.Box(at+new Vector3(.5f,9.7f,0),new Vector3(1,.6f,.05f),up,0,new Color(1,.36f,.62f));
        }

        // Long heart banners down the keep's two long faces.
        void Banners(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Bounds b)
        {
            bool alongZ=b.size.z>b.size.x;
            foreach(int side in new[]{-1,1})foreach(float t in new[]{-.25f,.25f})
            {
                var n=alongZ?new Vector3(side,0,0):new Vector3(0,0,side);
                var along=alongZ?Vector3.forward:Vector3.right;
                var at=b.center+Vector3.Scale(n,b.extents)+n*.2f+along*t*(alongZ?b.size.z:b.size.x);at.y=b.max.y-1;
                var q=vq*Quaternion.LookRotation(n);
                m.Box(V(at),new Vector3(3.6f,.2f,.2f),q,0,Gold);
                m.Box(V(at+Vector3.down*4),new Vector3(3,7.6f,.12f),q,0,GrapeDark);
                m.Box(V(at+Vector3.down*4-n*.01f),new Vector3(3.3f,7.9f,.08f),q,0,Gold);
                var heart=at+Vector3.down*3.6f+n*.1f;var right=Vector3.Cross(Vector3.up,n);
                foreach(int s in new[]{-1,1})m.Sphere(V(heart+right*s*.42f+Vector3.up*.25f),new Vector3(.6f,.6f,.12f),0,new Color(1,.36f,.62f),6,10);
                m.Box(V(heart-Vector3.up*.35f),new Vector3(.95f,.95f,.1f),q*Quaternion.Euler(0,0,45),0,new Color(1,.36f,.62f));
            }
        }

        void CrownDome(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Bounds b)
        {
            var top=new Vector3(b.center.x,b.max.y,b.center.z);float r=Mathf.Min(b.extents.x,b.extents.z)*.85f;
            m.Sphere(V(top),new Vector3(r,r*.8f,r),0,new Color(.3f,.88f,.82f),10,18);
            for(int i=0;i<10;i++){float a=i*Mathf.PI/5;m.Sphere(V(top+new Vector3(Mathf.Cos(a)*r*.62f,r*.5f,Mathf.Sin(a)*r*.62f)),Vector3.one*r*.1f,0,Sugar,3,6);}
            m.Cylinder(V(top+Vector3.up*r*.75f),vq,.12f,3,0,Gold,6);
            m.Sphere(V(top+Vector3.up*(r*.75f+3.2f)),Vector3.one*.45f,1,new Color(1,.85f,.5f),4,8);
        }

        // Downtown: a gold onion spire on the outer roof corner of the top escape spine.
        void Spire(ArtMesh m,System.Func<Vector3,Vector3> V,Quaternion vq,Transform frame,Bounds b)
        {
            Vector3 best=b.center;float far=-1;
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                var p=new Vector3(b.center.x+sx*(b.extents.x-1.25f),b.max.y,b.center.z+sz*(b.extents.z-1.25f));
                var w=frame.TransformPoint(p)-Services.World.Center;w.y=0;if(w.magnitude>far){far=w.magnitude;best=p;}
            }
            var up=vq;var orange=new Color(1,.54f,.08f);
            m.Cylinder(V(best),up,1.6f,2.6f,0,orange,16);
            m.Cylinder(V(best+Vector3.up*2.6f),up,1.8f,.35f,0,Gold,16);
            m.Cylinder(V(best+Vector3.up*2.95f),up,1.15f,2.4f,0,orange,14);
            m.Cylinder(V(best+Vector3.up*3.6f),up,1.18f,.6f,1,new Color(1,.85f,.5f),14);
            m.Sphere(V(best+Vector3.up*6.4f),new Vector3(1.5f,1.9f,1.5f),0,Gold,10,16);
            m.Cone(V(best+Vector3.up*7.9f),up,.5f,6.5f,0,Gold,10);
            m.Sphere(V(best+Vector3.up*14.6f),Vector3.one*.55f,1,new Color(1,.92f,.6f),5,8);
        }

        // Downtown: a glass elevator up the outer face; the cabin rides between street and roof.
        void Elevator(Rigidbody body,Transform visual,System.Func<Vector3,Vector3> V,Quaternion vq,Bounds b)
        {
            var frame=body.transform;var face=FaceToward(frame,frame.TransformPoint(b.center),true);
            var along=Vector3.Cross(Vector3.up,face);
            var foot=b.center+Vector3.Scale(face,b.extents)+face*1.45f+along*(Vector3.Scale(along,b.extents).magnitude*.35f);foot.y=b.min.y;
            float height=29;var m=new ArtMesh(2);
            // A thin bar lying along a frame axis.
            Vector3 Size(Vector3 axis,float length)=>new Vector3(Mathf.Abs(axis.x)*(length-.12f)+.12f,.12f,Mathf.Abs(axis.z)*(length-.12f)+.12f);
            foreach(int s in new[]{-1,1})foreach(int k in new[]{-1,1})
                m.Box(V(foot+along*s*1.3f+face*k*1.2f+Vector3.up*height*.5f),new Vector3(.18f,height,.18f),vq,0,Gold);
            for(float y=0;y<=height;y+=2.5f)
            {
                m.Box(V(foot+face*1.2f+Vector3.up*y),Size(along,2.8f),vq,0,Gold);
                m.Box(V(foot+along*1.3f+Vector3.up*y),Size(face,2.4f),vq,0,Gold);
                m.Box(V(foot-along*1.3f+Vector3.up*y),Size(face,2.4f),vq,0,Gold);
            }
            m.Cylinder(V(foot+Vector3.up*height),vq,1.9f,.5f,0,new Color(1,.54f,.08f),12);
            Model("Fantasy elevator shaft",m,visual,new[]{cityCandyMaterial,cityGlowMaterial},true);
            var c=new ArtMesh(2);
            c.Box(Vector3.up*1.3f,new Vector3(2.3f,2.6f,2.1f),Quaternion.identity,1,new Color(.75f,.92f,1));
            c.Box(Vector3.up*.08f,new Vector3(2.45f,.16f,2.25f),Quaternion.identity,0,Gold);
            c.Box(Vector3.up*2.62f,new Vector3(2.45f,.2f,2.25f),Quaternion.identity,0,Gold);
            foreach(int s in new[]{-1,1})foreach(int k in new[]{-1,1})c.Box(new Vector3(s*1.17f,1.3f,k*1.07f),new Vector3(.12f,2.6f,.12f),Quaternion.identity,0,Gold);
            var cabin=Model("Fantasy elevator cabin",c,visual,new[]{cityCandyMaterial,cityGlowMaterial},true).transform;
            cabin.localRotation=vq;
            elevators.Add((cabin,V(foot+Vector3.up*.3f),V(foot+Vector3.up*(height-3)),Hash(foot)*10));
        }

        void AnimateElevators(float time)
        {
            foreach(var (cabin,bottom,top,phase) in elevators)
            {
                if(!cabin)continue;
                // Ride up, wait, ride down, wait: 24 seconds a round trip.
                float t=Mathf.Repeat(time+phase,24)/24;
                float k=t<.1f?0:t<.45f?Mathf.SmoothStep(0,1,(t-.1f)/.35f):t<.55f?1:t<.9f?1-Mathf.SmoothStep(0,1,(t-.55f)/.35f):0;
                cabin.localPosition=Vector3.Lerp(bottom,top,k);
            }
        }
    }
}

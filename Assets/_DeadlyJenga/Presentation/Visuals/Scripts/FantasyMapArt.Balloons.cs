using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy Sky Ring City hot-air balloons: each one is its own model (striped teardrop envelope, crown cap,
    // skirt, ropes, wicker basket with sandbags, sandbags, burner) and flies on its own.
    // Cruisers circle the island at different heights and speeds; voyagers cross the whole sky from one
    // distant island to another and come round again. All of them bob, sway, slowly turn and fire their
    // burners now and then (the flame flares and the balloon lifts a little). Art only, high above play.
    public sealed partial class FantasyMapArt
    {
        sealed class Balloon
        {
            public Transform root, flame;
            public bool voyager;
            public float radius, height, speed, phase, bob, sway, spin;
            public Vector3 from, to;
            public float lift;
        }
        readonly List<Balloon> fleet=new();

        static readonly Color[][] BalloonPalettes=
        {
            new[]{new Color(1,.36f,.62f),new Color(1,.97f,.93f),new Color(1,.82f,.3f)},
            new[]{new Color(.62f,.36f,.96f),new Color(1,.82f,.3f),new Color(1,.97f,.93f)},
            new[]{new Color(.08f,.72f,.66f),new Color(1,.97f,.93f),new Color(1,.36f,.62f)},
            new[]{new Color(1,.54f,.08f),new Color(1,.92f,.4f),new Color(.92f,.22f,.26f)},
            new[]{new Color(.5f,.84f,.16f),new Color(1,.97f,.93f),new Color(.62f,.36f,.96f)},
            new[]{new Color(.92f,.22f,.26f),new Color(1,.97f,.93f),new Color(.4f,.75f,1)},
            new[]{new Color(.4f,.75f,1),new Color(1,.92f,.4f),new Color(1,.36f,.62f)},
        };

        void BuildBalloonFleet()
        {
            fleet.Clear();
            float R=Mathf.Max(halfWidth,halfDepth);
            // Cruisers: radius (relative to the island), height above the island, degrees per second.
            var cruisers=new[]{(.55f,62f,2.2f),(.8f,74f,-1.6f),(1.05f,58f,1.3f),(1.25f,88f,-1.1f),(.35f,96f,1.8f),(1.45f,70f,.9f),(.95f,110f,-1.4f)};
            for(int i=0;i<cruisers.Length;i++)
            {
                var (r,h,speed)=cruisers[i];
                var b=MakeBalloon("Visual · hot-air balloon "+(i+1),BalloonPalettes[i%BalloonPalettes.Length],i,1+(i%3)*.12f);
                b.radius=R*r;b.height=h;b.speed=speed;b.phase=i*51.4f;b.bob=2+(i%3);b.sway=2.5f+(i%2);b.spin=(i%2==0?1:-1)*(4+i);
                fleet.Add(b);
            }
            // Voyagers: cross the sky between far points and loop back, each at its own time and height.
            for(int i=0;i<4;i++)
            {
                float a=i*1.7f+.4f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir);
                var b=MakeBalloon("Visual · voyaging hot-air balloon "+(i+1),BalloonPalettes[(i+3)%BalloonPalettes.Length],20+i,1.25f);
                b.voyager=true;b.from=-dir*(R+420)+side*(i-1.5f)*45;b.to=dir*(R+420)+side*(i-1.5f)*45;
                b.height=80+i*14;b.speed=1f/(260+i*40);b.phase=i*.27f;b.bob=3;b.sway=2;b.spin=3;
                fleet.Add(b);
            }
        }

        Balloon MakeBalloon(string name,Color[] palette,int seed,float scale)
        {
            var m=new ArtMesh(2);
            const float R=5.2f,H=12.5f;int gores=18;
            // Teardrop profile (radius, height) from the mouth up to the crown.
            var profile=new[]{new Vector2(.2f,0),new Vector2(.34f,.1f),new Vector2(.55f,.24f),new Vector2(.8f,.4f),new Vector2(.96f,.55f),
                new Vector2(1,.68f),new Vector2(.93f,.81f),new Vector2(.74f,.91f),new Vector2(.44f,.975f),new Vector2(0,1)};
            int style=seed%3;
            for(int g=0;g<gores;g++)
            {
                float a0=g*Mathf.PI*2/gores,a1=(g+1)*Mathf.PI*2/gores;
                for(int k=0;k<profile.Length-1;k++)
                {
                    var p0=profile[k];var p1=profile[k+1];
                    Color c=g%2==0?palette[0]:palette[1];
                    if(style==1&&k>=4&&k<=5)c=palette[2];                 // equator band
                    if(style==2&&(k==2||k==6))c=palette[2];               // two rings
                    if(k>=profile.Length-3)c=palette[0];                  // crown cap
                    Vector3 P(float r,float y,float a)=>new Vector3(Mathf.Cos(a)*r*R,y*H,Mathf.Sin(a)*r*R);
                    // Smooth normals across the profile, so the fabric reads round instead of faceted.
                    Vector3 N(int i,float a)
                    {
                        var lo=profile[Mathf.Max(0,i-1)];var hi=profile[Mathf.Min(profile.Length-1,i+1)];
                        var n=new Vector2((hi.y-lo.y)*H,-(hi.x-lo.x)*R).normalized;
                        return new Vector3(Mathf.Cos(a)*n.x,n.y,Mathf.Sin(a)*n.x).normalized;
                    }
                    m.Panel(P(p0.x,p0.y,a0),P(p0.x,p0.y,a1),P(p1.x,p1.y,a1),P(p1.x,p1.y,a0),N(k,a0),N(k,a1),N(k+1,a1),N(k+1,a0),c);
                }
            }
            // Gold crown ring and a little pennant on top.
            m.Cylinder(new Vector3(0,H*.985f,0),Quaternion.identity,.9f,.25f,0,Gold,12);
            m.Box(new Vector3(0,H+.9f,0),new Vector3(.1f,1.8f,.1f),Quaternion.identity,0,Gold);
            m.Box(new Vector3(.5f,H+1.5f,0),new Vector3(1,.55f,.05f),Quaternion.identity,0,palette[2]);
            // Skirt under the mouth.
            m.Cylinder(new Vector3(0,-1.1f,0),Quaternion.identity,R*.2f,1.1f,0,Color.Lerp(palette[0],Color.black,.25f),14);
            // Basket, rim, ropes and sandbags.
            float drop=4.6f;var basket=new Vector3(0,-drop,0);var wicker=new Color(.66f,.43f,.22f);
            m.Box(basket,new Vector3(1.9f,1.3f,1.9f),Quaternion.identity,0,wicker);
            m.Box(basket+Vector3.up*.7f,new Vector3(2.05f,.18f,2.05f),Quaternion.identity,0,new Color(.45f,.27f,.13f));
            for(int i=0;i<4;i++){float y=-.45f+i*.3f;m.Box(basket+Vector3.up*y,new Vector3(1.93f,.05f,1.93f),Quaternion.identity,0,new Color(.55f,.34f,.17f));}
            for(int i=0;i<4;i++)
            {
                var corner=new Vector3((i%2*2-1)*.9f,.7f,(i/2*2-1)*.9f);
                var a=basket+corner;var e=new Vector3(corner.x*.82f,-1.1f,corner.z*.82f);var dirn=e-a;
                m.Box((a+e)*.5f,new Vector3(.06f,.06f,dirn.magnitude),Quaternion.LookRotation(dirn),0,new Color(.35f,.24f,.16f));
                m.Sphere(basket+new Vector3(corner.x*1.12f,-.1f,corner.z*1.12f),new Vector3(.22f,.3f,.22f),0,new Color(.85f,.74f,.55f),4,6);
            }
            // Burner frame between basket and mouth.
            m.Box(new Vector3(0,-2.4f,0),new Vector3(.55f,.4f,.55f),Quaternion.identity,0,new Color(.6f,.62f,.68f));
            var go=Model(name,m,transform,new[]{cityCandyMaterial,cityGlowMaterial});
            go.transform.localScale=Vector3.one*scale;
            // Flame as its own child so it can flicker.
            var f=new ArtMesh(1);
            f.Cone(Vector3.zero,Quaternion.identity,.35f,1.5f,0,new Color(1,.62f,.15f),8);
            f.Cone(Vector3.up*.1f,Quaternion.identity,.2f,1.1f,0,new Color(1,.95f,.55f),8);
            var flame=Model(name+" flame",f,go.transform,new[]{cityGlowMaterial});
            flame.transform.localPosition=new Vector3(0,-2.15f,0);
            return new Balloon{root=go.transform,flame=flame.transform};
        }

        void AnimateBalloons(float time)
        {
            var center=transform.InverseTransformPoint(Services.World!=null?Services.World.Center:transform.position);
            float top=transform.InverseTransformPoint(new Vector3(0,groundY,0)).y;
            foreach(var b in fleet)
            {
                if(!b.root)continue;
                // Burner: short flares every few seconds; while it burns the balloon gains a little height.
                float burn=Mathf.PerlinNoise(b.phase*.37f,time*.25f);bool firing=burn>.62f;
                b.lift=Mathf.MoveTowards(b.lift,firing?4:0,Time.deltaTime*(firing?.8f:.25f));
                if(b.flame)b.flame.localScale=firing?new Vector3(1,1.4f+Mathf.PerlinNoise(time*9,b.phase)*.9f,1):Vector3.one*.35f;
                Vector3 p,heading;
                if(b.voyager)
                {
                    float t=Mathf.Repeat(time*b.speed+b.phase,1);
                    p=Vector3.Lerp(b.from,b.to,t);heading=b.to-b.from;
                    p+=Vector3.Cross(Vector3.up,heading.normalized)*Mathf.Sin(t*Mathf.PI*2)*30;
                }
                else
                {
                    float a=(b.phase+time*b.speed)*Mathf.Deg2Rad;
                    p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*b.radius*(1+.08f*Mathf.Sin(time*.05f+b.phase));
                    heading=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*Mathf.Sign(b.speed);
                }
                p+=center;p.y=top+b.height+b.lift+Mathf.Sin(time*(.18f+b.bob*.02f)+b.phase)*b.bob;
                b.root.localPosition=p;
                // Lean gently into the drift, sway, and turn slowly as balloons do.
                float yaw=time*b.spin+b.phase*10;
                var lean=Quaternion.AngleAxis(Mathf.Sin(time*.4f+b.phase)*b.sway,Vector3.Cross(Vector3.up,heading.normalized));
                b.root.localRotation=lean*Quaternion.Euler(0,yaw,Mathf.Sin(time*.33f+b.phase*2)*b.sway*.5f);
            }
        }

        sealed partial class ArtMesh
        {
            // One envelope panel with its own smooth normals, drawn from both sides.
            public void Panel(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 na,Vector3 nb,Vector3 nc,Vector3 nd,Color color)
            {
                foreach(bool outside in new[]{true,false})
                {
                    int start=vertices.Count;float s=outside?1:-1;
                    vertices.AddRange(new[]{a,b,c,d});normals.AddRange(new[]{na*s,nb*s,nc*s,nd*s});for(int i=0;i<4;i++)colors.Add(color);
                    var face=Vector3.Cross(b-a,c-a);bool facesOut=Vector3.Dot(face,na)>0;
                    if(facesOut==outside)triangles[0].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                    else triangles[0].AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
                }
            }
        }
    }
}

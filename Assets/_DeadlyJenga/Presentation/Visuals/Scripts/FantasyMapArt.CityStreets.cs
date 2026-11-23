using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy city streets: paved ground, a ring boulevard, avenues, a plaza ring, parks, lamps and cars.
    // Art only (no colliders). Roads are laid where the ground is free of blocks, so any layout works.
    public sealed partial class FantasyMapArt
    {
        public bool cityStreets;

        static readonly Color Pavement=new(.98f,.7f,.82f),PavementLine=new(.84f,.48f,.66f),Asphalt=new(.24f,.22f,.3f),Sidewalk=new(1,.93f,.96f),
            LaneLine=new(1,.95f,.62f),Grass=new(.55f,.93f,.5f);
        const int RoadBins=96;
        float roadRadius;Vector3 roadCenter;readonly bool[] roadOpen=new bool[RoadBins];
        readonly List<Transform> cars=new();readonly List<float> carPhase=new();

        float streetY;
        bool Free(Vector3 p,float half)=>!Physics.CheckBox(new Vector3(p.x,streetY+1.6f,p.z),new Vector3(half,1.4f,half),Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        Vector3 L(Vector3 world)=>transform.InverseTransformPoint(world);
        Quaternion LR(Quaternion world)=>Quaternion.Inverse(transform.rotation)*world;

        void BuildCityStreets()
        {
            var center=Services.World.Center;streetY=groundY;
            if(Physics.Raycast(new Vector3(center.x,groundY+60,center.z),Vector3.down,out var hit,120,1<<Layers.Surface,QueryTriggerInteraction.Ignore))streetY=hit.point.y;
            center.y=streetY;roadCenter=center;
            float R=Mathf.Min(halfWidth,halfDepth)*.9f;
            var m=new ArtMesh(2);
            // Ring boulevard at the freest radius between the inner and outer districts.
            float bestScore=-1;roadRadius=R*.6f;
            for(float r=R*.4f;r<=R*.9f;r+=1)
            {
                int free=0;for(int i=0;i<72;i++){float a=i*Mathf.PI*2/72;if(Free(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r,3.5f))free++;}
                float score=free+r/R*.5f;if(score>bestScore){bestScore=score;roadRadius=r;}
            }
            int open=0;
            for(int i=0;i<RoadBins;i++)
            {
                float a=(i+.5f)*Mathf.PI*2/RoadBins;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var p=center+dir*roadRadius;
                roadOpen[i]=Free(p,3.2f);if(!roadOpen[i])continue;open++;
                var q=Quaternion.LookRotation(Vector3.Cross(Vector3.up,dir));float len=roadRadius*Mathf.PI*2/RoadBins+.4f;
                if(solidCandy)
                {
                    var start=Radial(i);var end=Radial(i+1);
                    m.RoadBand(L(center),start,end,roadRadius-6.5f,roadRadius+6.5f,.116f,Sidewalk);
                    m.RoadBand(L(center),start,end,roadRadius-4.5f,roadRadius+4.5f,.15f,Asphalt);
                    // Drop the curb at the eight avenue crossings.
                    float junction=Mathf.Repeat(a,Mathf.PI/4)-Mathf.PI/8;
                    if(Mathf.Abs(Mathf.Sin(junction))*roadRadius>3.8f)
                        foreach(int side in new[]{-1,1})
                        {
                            float curb=roadRadius+side*4.7f;
                            m.RoadBand(L(center),start,end,curb-.21f,curb+.21f,.21f,Sugar,.116f);
                        }
                }
                else
                {
                    m.Box(L(p+Vector3.up*.08f),new Vector3(13,.04f,len),LR(q),0,Sidewalk);
                    m.Box(L(p+Vector3.up*.12f),new Vector3(9,.06f,len),LR(q),0,Asphalt);
                    foreach(int side in new[]{-1,1})m.Box(L(p+dir*side*4.7f+Vector3.up*.2f),new Vector3(.5f,.22f,len),LR(q),0,Sugar);
                }
                if(i%2==0)m.Box(L(p+Vector3.up*.16f),new Vector3(.3f,.02f,len*.55f),LR(q),1,LaneLine);
                if(i%4==0&&Free(p+dir*6.2f,.8f))Lamp(m,p+dir*6.2f);
                if(i%4==2&&Free(p-dir*6.2f,.8f))Lamp(m,p-dir*6.2f);
                if(!solidCandy && i%7==3&&Free(p+dir*3,1.4f))Car(m,p+dir*2.6f,q,i);
            }

            // Eight avenues from the plaza to the island edge, crossing the boulevard on zebra crossings.
            for(int k=0;k<8;k++)
            {
                float a=k*Mathf.PI/4+Mathf.PI/8;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var q=Quaternion.LookRotation(dir);
                for(float r=18;r<R-2;r+=2)
                {
                    var p=center+dir*r;if(Mathf.Abs(r-roadRadius)<5.5f){if(Free(p,2.5f))Zebra(m,p,q);continue;}
                    if(!Free(p,2.6f))continue;
                    m.Box(L(p+Vector3.up*.08f),new Vector3(9.5f,.04f,2.2f),LR(q),0,Sidewalk);
                    m.Box(L(p+Vector3.up*.11f),new Vector3(6,.05f,2.2f),LR(q),0,Asphalt);
                    if(Mathf.RoundToInt(r)%4==0)m.Box(L(p+Vector3.up*.15f),new Vector3(.25f,.02f,1),LR(q),1,LaneLine);
                    var side=Vector3.Cross(Vector3.up,dir);
                    if(Mathf.RoundToInt(r)%12==0)foreach(int s in new[]{-1,1})if(Free(p+side*s*4.5f,1))Tree(m,p+side*s*4.5f,k+s);
                }
            }

            if(!solidCandy) BuildOutdoorPaving(m,center,R);
            Model("Visual · candy city streets",m,transform,new[]{cityCandyMaterial,cityGlowMaterial});

            // Traffic on the boulevard when most of it is open road.
            if(!solidCandy && open>RoadBins*.35f)for(int i=0;i<9;i++)
            {
                var c=new ArtMesh(2);Car(c,Vector3.zero,Quaternion.identity,i+11,true);
                var go=Model("Visual · candy car",c,transform,new[]{cityCandyMaterial,cityGlowMaterial});
                cars.Add(go.transform);carPhase.Add(i/9f*Mathf.PI*2);
            }
        }

        void BuildOutdoorPaving(ArtMesh m,Vector3 center,float R)
        {
            // Paved city floor with a tile grid, so the island reads as streets instead of loose cloud.
            m.Cylinder(L(center+Vector3.up*.03f),LR(Quaternion.identity),R,.05f,0,Pavement,64);
            for(float x=-R+6;x<R;x+=6)
            {
                float chord=Mathf.Sqrt(Mathf.Max(0,R*R-x*x))*2-2;if(chord<2)continue;
                m.Box(L(center+new Vector3(x,.09f,0)),new Vector3(.18f,.02f,chord),LR(Quaternion.identity),0,PavementLine);
                m.Box(L(center+new Vector3(0,.09f,x)),new Vector3(chord,.02f,.18f),LR(Quaternion.identity),0,PavementLine);
            }

            // Patterned plaza ring around whatever stands in the middle.
            for(int i=0;i<48;i++)
            {
                float a=(i+.5f)*Mathf.PI*2/48;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                for(int band=0;band<3;band++)
                {
                    float r=17+band*2.2f;var p=center+dir*r;if(!Free(p,1.2f))continue;
                    m.Box(L(p+Vector3.up*.1f),new Vector3(2.1f,.04f,r*Mathf.PI*2/48),LR(Quaternion.LookRotation(Vector3.Cross(Vector3.up,dir))),0,(i+band)%2==0?new Color(1,.55f,.75f):Sugar);
                }
            }

            // Pocket parks wherever a lot is empty: lawn, a lollipop tree or two and a bench.
            int parks=0;
            for(float x=-R;x<R;x+=10)for(float z=-R;z<R;z+=10)
            {
                var p=center+new Vector3(x+((int)(z/10)%2)*5,0,z);var flat=p-center;float r=flat.magnitude;
                if(r<26||r>R-6||Mathf.Abs(r-roadRadius)<9)continue;
                float ang=(Mathf.Atan2(flat.z,flat.x)-Mathf.PI/8)/(Mathf.PI/4);if(Mathf.Abs(ang-Mathf.Round(ang))*r*Mathf.PI/4<6)continue;
                if(Hash(p)>.6f||!Free(p,4.6f))continue;
                m.Cylinder(L(p+Vector3.up*.05f),LR(Quaternion.identity),4.2f,.16f,0,Grass,18);
                m.Cylinder(L(p+Vector3.up*.05f),LR(Quaternion.identity),4.5f,.12f,0,Sugar,18);
                Tree(m,p+new Vector3(-1.2f,0,.8f),parks);
                if(Hash(p+Vector3.one)>.5f)Tree(m,p+new Vector3(1.6f,0,-1.1f),parks+1);
                m.Box(L(p+new Vector3(0,.5f,-2.8f)),new Vector3(2.2f,.18f,.6f),LR(Quaternion.identity),0,new Color(.95f,.4f,.55f));
                parks++;
            }
        }

        Vector3 Radial(int index)
        {
            float a=(index%RoadBins)*2*Mathf.PI/RoadBins;
            return Quaternion.Inverse(transform.rotation)*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
        }

        sealed partial class ArtMesh
        {
            // Adjacent sectors use identical endpoints, including the wrap at the first sector.
            public void RoadBand(Vector3 center,Vector3 start,Vector3 end,float inner,float outer,float y,Color color,float baseY=-1)
            {
                var lift=Vector3.up*y;
                var a=center+start*inner+lift;var b=center+end*inner+lift;
                var c=center+end*outer+lift;var d=center+start*outer+lift;
                Quad(a,b,c,d,0,color);
                if(baseY<0)return;
                var drop=Vector3.down*(y-baseY);
                Quad(b,a,a+drop,b+drop,0,color);
                Quad(d,c,c+drop,d+drop,0,color);
                Quad(a,d,d+drop,a+drop,0,color);
                Quad(c,b,b+drop,c+drop,0,color);
            }
        }

        void Lamp(ArtMesh m,Vector3 p)
        {
            if(solidCandy)
            {
                var lamp=new Room(m,L(p),LR(Quaternion.identity));
                lamp.Tube(0,.10f,0,.36f,.20f,Ink);
                lamp.Tube(0,.26f,0,.24f,.54f,Jade);
                lamp.Tube(0,.76f,0,.15f,3.05f,Jade);
                lamp.Tube(0,3.71f,0,.29f,.18f,Brass);
                m.Sphere(lamp.At(0,4.23f,0),new Vector3(.39f,.49f,.39f),1,new Color(1,.83f,.58f),8,14);
                m.Cone(lamp.At(0,4.54f,0),lamp.q,.37f,.22f,0,Jade,14);
                lamp.Tube(0,4.74f,0,.065f,.12f,Brass);
                return;
            }
            m.Cylinder(L(p),LR(Quaternion.identity),.12f,4.2f,0,Sugar,8);
            m.Box(L(p+Vector3.up*4.25f),new Vector3(.9f,.12f,.12f),LR(Quaternion.identity),0,Sugar);
            m.Sphere(L(p+Vector3.up*4.55f),Vector3.one*.55f,1,WindowTints[0],4,8);
        }

        void Tree(ArtMesh m,Vector3 p,int seed)
        {
            m.Cylinder(L(p),LR(Quaternion.identity),.14f,2.6f,0,Sugar,8);
            var tint=RoofTints[Mathf.Abs(seed)%4];
            m.Sphere(L(p+Vector3.up*3.3f),new Vector3(1.6f,1.6f,1.6f),0,tint,6,10);
            m.Sphere(L(p+Vector3.up*3.3f),new Vector3(1.1f,1.7f,1.1f),0,Color.Lerp(tint,Color.white,.55f),6,10);
        }

        void Zebra(ArtMesh m,Vector3 p,Quaternion q)
        {
            for(int s=-2;s<=2;s++)m.Box(L(p+q*new Vector3(s*1.2f,.17f,0)),new Vector3(.6f,.02f,3.4f),LR(q),0,Sugar);
        }

        // local=true builds a car around the origin (for moving cars); otherwise in world space.
        void Car(ArtMesh m,Vector3 p,Quaternion q,int seed,bool local=false)
        {
            Vector3 P(Vector3 o)=>local?o:L(p+q*o);Quaternion Q(Quaternion r)=>local?r:LR(q*r);
            var tint=new[]{new Color(1,.3f,.45f),new Color(.4f,.8f,1),new Color(1,.85f,.3f),new Color(.6f,1,.55f),new Color(.75f,.5f,1)}[seed%5];
            m.Box(P(new Vector3(0,.75f,0)),new Vector3(1.9f,.8f,4.2f),Q(Quaternion.identity),0,tint);
            m.Box(P(new Vector3(0,1.5f,-.2f)),new Vector3(1.7f,.75f,2.3f),Q(Quaternion.identity),1,new Color(.75f,.92f,1));
            m.Box(P(new Vector3(0,1.92f,-.2f)),new Vector3(1.75f,.12f,2.4f),Q(Quaternion.identity),0,Sugar);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                m.Cylinder(P(new Vector3(x>0?1.2f:-.9f,.42f,z*1.35f)),Q(Quaternion.Euler(0,0,90)),.42f,.3f,0,new Color(.25f,.15f,.25f),10);
            m.Sphere(P(new Vector3(-.6f,.85f,2.1f)),Vector3.one*.3f,1,WindowTints[0],3,6);
            m.Sphere(P(new Vector3(.6f,.85f,2.1f)),Vector3.one*.3f,1,WindowTints[0],3,6);
        }

        void AnimateCars(float time)
        {
            if(cars.Count==0||roadRadius<=0)return;
            float lane=roadRadius-2.2f;
            for(int i=0;i<cars.Count;i++)
            {
                if(!cars[i])continue;
                float a=carPhase[i]+time*9/lane;int bin=((int)(a/(Mathf.PI*2)*RoadBins)%RoadBins+RoadBins)%RoadBins;
                bool show=roadOpen[bin]&&roadOpen[(bin+1)%RoadBins]&&roadOpen[(bin+RoadBins-1)%RoadBins];
                if(cars[i].gameObject.activeSelf!=show)cars[i].gameObject.SetActive(show);
                var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                cars[i].localPosition=L(roadCenter+dir*lane+Vector3.up*.1f);
                cars[i].localRotation=LR(Quaternion.LookRotation(Vector3.Cross(dir,Vector3.up)));
            }
        }
    }
}

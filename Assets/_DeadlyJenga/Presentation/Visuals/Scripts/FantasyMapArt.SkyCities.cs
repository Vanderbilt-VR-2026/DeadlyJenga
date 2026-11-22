using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy Sky Ring City's horizon: six themed cloud cities floating around the island, and a rainbow bridge
    // between two of them with a little candy train riding over it. Art only, no colliders.
    // Everything stays within about 560 m of the map center, so the survivor camera (700 m far plane)
    // sees all of it even from the far edge of the island.
    public sealed partial class FantasyMapArt
    {
        readonly List<(Transform city,Vector3 home,float phase)> floatingCities=new();
        readonly List<Transform> wheelCars=new();
        readonly List<(Transform car,float offset)> trainCars=new();
        Transform ferrisWheel,lighthouseBeam;Vector3 wheelHub;float wheelRadius;
        Vector3[] rainbowPath,rainbowUp;

        static readonly Color Haze=new(.82f,.86f,1);
        static Color H(Color c,float amount=.06f)=>Color.Lerp(c,Haze,amount);
        static readonly Color[] Pastels={new(1,.45f,.68f),new(.42f,.78f,1),new(.68f,.52f,1),new(.5f,.95f,.62f),new(1,.8f,.4f),new(1,.6f,.45f)};
        static readonly Color[] Crystals={new(.85f,.7f,1),new(1,.7f,.86f),new(.6f,.92f,1),new(.75f,1,.9f)};

        // Mesh slots used by every sky city: 0 cloud, 1 candy, 2 glow.
        Material[] SkyMaterials=>new[]{cloudMaterial,cityCandyMaterial,cityGlowMaterial};

        void BuildSkyCities()
        {
            floatingCities.Clear();wheelCars.Clear();trainCars.Clear();ferrisWheel=lighthouseBeam=null;
            var center=Services.World!=null?Services.World.Center:transform.position;center.y=groundY;
            // angle (degrees), distance from the map center, height above the street, island radius, kind
            var cities=new[]{(70f,400f,25f,55f,0),(22f,340f,40f,34f,5),(140f,370f,8f,42f,1),(205f,420f,32f,48f,2),(262f,385f,14f,45f,3),(318f,440f,-4f,44f,4)};
            var tops=new Vector3[cities.Length];var radii=new float[cities.Length];var transforms=new Transform[cities.Length];
            for(int i=0;i<cities.Length;i++)
            {
                var (angle,distance,height,radius,kind)=cities[i];
                float a=angle*Mathf.Deg2Rad;var world=center+new Vector3(Mathf.Cos(a)*distance,height,Mathf.Sin(a)*distance);
                var m=new ArtMesh(3);
                CloudIsland(m,radius,Pastels[i%Pastels.Length],i);
                switch(kind)
                {
                    case 0:Citadel(m,radius);break;
                    case 1:LollipopGrove(m,radius);break;
                    case 2:CarnivalIsle(m,radius);break;
                    case 3:CrystalSpires(m,radius);break;
                    case 4:LighthouseHarbor(m,radius);break;
                    default:CupcakeHamlet(m,radius);break;
                }
                var go=Model("Visual · sky city "+(i+1),m,transform,SkyMaterials);
                // Each city turns its best side (+z) toward the island.
                var face=Quaternion.LookRotation(Vector3.ProjectOnPlane(center-world,Vector3.up).normalized);
                go.transform.localPosition=L(world);go.transform.localRotation=LR(face);
                tops[i]=world;radii[i]=radius;transforms[i]=go.transform;
                if(kind==2)AddFerrisWheel(go.transform,radius);
                if(kind==4)AddLighthouseBeam(go.transform);
                // The two rainbow cities stay put so the bridge stays attached; the others drift gently.
                if(i>1)floatingCities.Add((go.transform,go.transform.localPosition,i*1.7f));
            }
            BuildRainbowBridge(tops[0],radii[0],tops[1],radii[1]);
            // The cupcake hamlet (one end of the bridge) pours a rainbow waterfall toward the island.
            RainbowFall(transforms[1],new Vector3(0,-1,radii[1]+1),16,10,150,"hamlet");
            BuildIslandAbove(center);
            // Place moving parts now: pictures and the first frame must not show them at the map origin.
            AnimateSkyCities(Time.time);
        }

        // Frosted cake platform on a cotton-candy cloud, with rock-candy crystals hanging underneath.
        void CloudIsland(ArtMesh m,float r,Color frosting,int seed)
        {
            var cream=H(Color.Lerp(frosting,Color.white,.4f));frosting=H(frosting);
            m.Cylinder(Vector3.down*7,Quaternion.identity,r,7,1,frosting,32);
            m.Cylinder(Vector3.down*.6f,Quaternion.identity,r*.97f,.8f,1,cream,32);
            for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI*2/32;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                m.Sphere(dir*r+Vector3.down*.4f,new Vector3(r*.1f,1.6f,r*.1f),1,cream,4,8);
                if(i%2==0){float drip=1.5f+Hash(new Vector3(i,seed,4))*3.5f;m.Sphere(dir*(r+.3f)+Vector3.down*(2.4f+drip*.5f),new Vector3(1.5f,1.6f+drip*.6f,1.5f),1,cream,4,8);}
            }
            // Cloud collar hiding where the cake meets the cloud, then the cloud body.
            for(int i=0;i<18;i++)
            {
                float a=i*Mathf.PI*2/18+seed;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                m.Sphere(dir*r*1.02f+Vector3.down*(7.5f+i%3),new Vector3(r*.34f,r*.15f+2,r*.3f),0,H(i%4==0?new Color(1,.86f,.94f):Color.white,.03f),8,14);
            }
            for(int i=0;i<5;i++)
            {
                float a=i*1.256f+seed;var off=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*.45f*(i==0?0:1);
                m.Sphere(off+Vector3.down*(11+i%2*3),new Vector3(r*.62f,r*.22f+3,r*.62f),0,H(i%3==0?new Color(.94f,.9f,1):Color.white,.03f),8,16);
            }
            // Rock-candy crystals pointing down.
            var down=Quaternion.Euler(180,0,0);
            m.Cone(Vector3.down*12,down,r*.32f,r*1.05f,1,H(Crystals[seed%4]),7);
            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2/7+seed*.7f;var off=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*(.42f+i%2*.12f);
                m.Cone(off+Vector3.down*11,down*Quaternion.Euler(Mathf.Cos(a)*12,0,Mathf.Sin(a)*12),r*(.12f+i%3*.04f),r*(.35f+Hash(new Vector3(seed,i,1))*.35f),1,H(Crystals[(seed+i)%4]),6);
            }
        }

        // A building with glowing windows on the three sides the island can see, and a candy roof.
        void SkyTower(ArtMesh m,Vector3 at,float w,float h,Color body,int roof,int seed)
        {
            body=H(body);var accent=H(Pastels[(seed+2)%Pastels.Length]);var window=WindowTints[seed%WindowTints.Length];
            m.Box(at+Vector3.up*h*.5f,new Vector3(w,h,w),Quaternion.identity,1,body);
            m.Box(at+Vector3.up*(h-.6f),new Vector3(w+1,1.2f,w+1),Quaternion.identity,1,accent);
            int cols=Mathf.Max(1,Mathf.RoundToInt(w/3.4f));float pitch=w/cols;
            for(float y=3;y<h-3;y+=4)
                for(int c=0;c<cols;c++)
                {
                    float x=-w*.5f+pitch*(c+.5f);
                    m.Box(at+new Vector3(x,y,w*.5f+.05f),new Vector3(pitch*.55f,1.7f,.3f),Quaternion.identity,2,window);
                    m.Box(at+new Vector3(w*.5f+.05f,y,x),new Vector3(.3f,1.7f,pitch*.55f),Quaternion.identity,2,window);
                    m.Box(at+new Vector3(-w*.5f-.05f,y,x),new Vector3(.3f,1.7f,pitch*.55f),Quaternion.identity,2,window);
                }
            var top=at+Vector3.up*h;
            switch(roof)
            {
                case 0:m.Cone(top,Quaternion.identity,w*.72f,w*1.1f,1,accent,10);break;
                case 1:m.Sphere(top,new Vector3(w*.55f,w*.5f,w*.55f),1,accent,6,12);break;
                case 2:m.Sphere(top+Vector3.up*w*.35f,new Vector3(w*.5f,w*.55f,w*.5f),1,accent,6,12);m.Cone(top+Vector3.up*w*.8f,Quaternion.identity,w*.18f,w*.7f,1,H(Gold),8);break;
                case 3:m.Cone(top,Quaternion.identity,w*.3f,w*2.2f,1,H(Gold),8);m.Sphere(top+Vector3.up*w*2.25f,Vector3.one*1.2f,2,new Color(1,.9f,.6f),4,8);break;
                default:m.Box(top+Vector3.up*.5f,new Vector3(w*.8f,1,w*.8f),Quaternion.identity,1,accent);break;
            }
        }

        void SkyFlag(ArtMesh m,Vector3 top,Color color)
        {
            m.Box(top+Vector3.up*3,new Vector3(.35f,6,.35f),Quaternion.identity,1,H(Sugar));
            m.Box(top+new Vector3(2,5,0),new Vector3(4,2.4f,.2f),Quaternion.identity,2,color);
        }

        void Citadel(ArtMesh m,float r)
        {
            var wall=H(new Color(1,.8f,.88f));var roofPink=H(new Color(1,.42f,.66f));
            // Curtain wall with towers at the corners.
            float s=r*.55f;var corners=new[]{new Vector3(-s,0,-s),new Vector3(s,0,-s),new Vector3(s,0,s),new Vector3(-s,0,s)};
            for(int i=0;i<4;i++)
            {
                var a=corners[i];var b=corners[(i+1)%4];var mid=(a+b)*.5f;var along=(b-a);
                m.Box(mid+Vector3.up*8,new Vector3(Mathf.Abs(along.x)+3,16,Mathf.Abs(along.z)+3),Quaternion.identity,1,wall);
                for(int k=0;k<9;k++){var p=Vector3.Lerp(a,b,(k+.5f)/9)+Vector3.up*17;m.Box(p,new Vector3(2.4f,2,2.4f),Quaternion.identity,1,wall);}
                m.Cylinder(a,Quaternion.identity,6,42,1,wall,14);
                m.Cylinder(a+Vector3.up*40,Quaternion.identity,7,2.5f,1,H(Pastels[i]),14);
                m.Cone(a+Vector3.up*42.5f,Quaternion.identity,7.5f,16,1,roofPink,14);
                SkyFlag(m,a+Vector3.up*58,Pastels[(i+1)%Pastels.Length]);
                for(float y=12;y<38;y+=8)m.Box(a+new Vector3(0,y,6.05f),new Vector3(1.6f,2.6f,.3f),Quaternion.identity,2,WindowTints[0]);
            }
            // Glowing gate facing the island.
            m.Box(new Vector3(0,6,s+1.6f),new Vector3(9,12,.4f),Quaternion.identity,2,new Color(1,.8f,.5f));
            m.Sphere(new Vector3(0,12,s+1.6f),new Vector3(4.5f,4,.6f),2,new Color(1,.8f,.5f),4,10);
            // Central keep, tall and tiered.
            SkyTower(m,Vector3.zero,20,46,new Color(1,.88f,.93f),4,1);
            SkyTower(m,Vector3.up*47,13,18,new Color(1,.75f,.86f),4,2);
            m.Cylinder(Vector3.up*66,Quaternion.identity,5,10,1,wall,14);
            m.Cone(Vector3.up*76,Quaternion.identity,6.5f,22,1,roofPink,14);
            m.Sphere(Vector3.up*99,Vector3.one*2.2f,1,H(Gold),6,10);
            SkyFlag(m,Vector3.up*101,new Color(1,.3f,.5f));
            for(int i=0;i<4;i++){var c=corners[i]*.55f;m.Cylinder(c+Vector3.up*46,Quaternion.identity,2.6f,14,1,wall,10);m.Cone(c+Vector3.up*60,Quaternion.identity,3.4f,9,1,H(Pastels[i+1]),10);}
            // Little houses outside the walls.
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2/10+.3f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*.84f;
                SkyTower(m,p,5,6+i%3*3,Pastels[i%Pastels.Length],0,i);
            }
        }

        void LollipopGrove(ArtMesh m,float r)
        {
            var swirl=new[]{new Color(1,.3f,.5f),Sugar,new Color(.5f,.8f,1),Sugar,new Color(1,.85f,.35f),Sugar};
            for(int i=0;i<9;i++)
            {
                float a=i*2.399963f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*(i==0?0:.35f+(i%3)*.17f);
                float h=26+Hash(new Vector3(i,4,4))*22;float size=6+(i%3)*2.2f;
                m.Cylinder(p,Quaternion.identity,.9f,h,1,H(Sugar),8);
                var face=Quaternion.Euler(90,0,0);
                for(int k=0;k<6;k++){float rad=size*(1-k*.15f);m.Cylinder(p+Vector3.up*h+Vector3.back*(.9f+k*.12f),face,rad,1.8f+k*.24f,1,H(Color.Lerp(swirl[(k+i)%6],Pastels[i%Pastels.Length],.15f)),24);}
            }
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2/10+.2f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*.82f;
                SkyTower(m,p,6,7+(i%2)*4,Pastels[(i+3)%Pastels.Length],i%2==0?0:1,i);
                m.Sphere(p*.8f+new Vector3(3,4,0),new Vector3(3.2f,3.6f,3.2f),1,H(new Color(.5f,.9f,.4f)),5,10);
            }
        }

        void CarnivalIsle(ArtMesh m,float r)
        {
            // Big striped circus tent.
            var tent=new Vector3(-r*.35f,0,-r*.1f);
            m.Cylinder(tent,Quaternion.identity,16,10,1,H(new Color(1,.92f,.8f)),20);
            m.StripedCone(tent+Vector3.up*10,18,20,20,1,H(new Color(1,.3f,.4f)),H(Sugar));
            SkyFlag(m,tent+Vector3.up*30,new Color(1,.8f,.3f));
            m.Box(tent+new Vector3(0,4,16.1f),new Vector3(6,8,.3f),Quaternion.identity,2,new Color(1,.75f,.45f));
            // Ferris wheel supports (the wheel itself turns; see AddFerrisWheel).
            var hub=new Vector3(r*.32f,34,0);
            foreach(int side in new[]{-1,1})
            {
                foreach(int leg in new[]{-1,1})
                {
                    var foot=new Vector3(hub.x+leg*14,0,side*4.5f);var top=hub+new Vector3(0,0,side*4.5f);
                    m.Box((foot+top)*.5f,new Vector3(1.4f,(top-foot).magnitude,1.4f),Quaternion.LookRotation(Vector3.forward,top-foot),1,H(Gold));
                }
            }
            // Coaster loop and booths.
            m.Ring(new Vector3(-r*.1f,14,r*.55f),Quaternion.Euler(0,0,90)*Quaternion.Euler(0,90,0),12,1.1f,40,8,1,H(new Color(.5f,.8f,1)));
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*2/8;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*.8f;
                SkyTower(m,p,5,5+(i%3)*2,Pastels[i%Pastels.Length],i%2==0?0:2,i+3);
            }
        }

        void AddFerrisWheel(Transform city,float r)
        {
            wheelRadius=24;wheelHub=new Vector3(r*.32f,34,0);
            var m=new ArtMesh(3);
            m.Ring(Vector3.zero,Quaternion.Euler(90,0,0),wheelRadius,.9f,48,8,1,H(new Color(1,.4f,.65f)));
            m.Ring(Vector3.zero,Quaternion.Euler(90,0,0),wheelRadius*.62f,.5f,40,6,1,H(Sugar));
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2/12;var tip=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*wheelRadius;
                m.Box(tip*.5f,new Vector3(.5f,wheelRadius,.5f),Quaternion.LookRotation(Vector3.forward,tip),1,H(Sugar));
                m.Sphere(tip,Vector3.one*1.1f,2,i%2==0?new Color(1,.9f,.6f):new Color(1,.6f,.8f),4,8);
            }
            m.Cylinder(Vector3.back*5,Quaternion.Euler(90,0,0),2.2f,10,1,H(Gold),12);
            var wheel=Model("Visual · sky city ferris wheel",m,city,SkyMaterials);
            wheel.transform.localPosition=wheelHub;ferrisWheel=wheel.transform;
            for(int i=0;i<12;i++)
            {
                var c=new ArtMesh(3);var tint=H(Pastels[i%Pastels.Length]);
                c.Box(Vector3.down*1.2f,new Vector3(.25f,2.4f,.25f),Quaternion.identity,1,H(Sugar));
                c.Box(Vector3.down*3.6f,new Vector3(3.4f,2.8f,2.6f),Quaternion.identity,1,tint);
                c.Box(Vector3.down*3.4f,new Vector3(3.5f,1.1f,2),Quaternion.identity,2,WindowTints[0]);
                var car=Model("Visual · sky city wheel car",c,city,SkyMaterials);
                wheelCars.Add(car.transform);
            }
        }

        void CrystalSpires(ArtMesh m,float r)
        {
            for(int i=0;i<9;i++)
            {
                float a=i*2.399963f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*(i==0?0:.3f+(i%3)*.18f);
                float h=(i==0?90:40+Hash(new Vector3(i,8,2))*35);float w=i==0?9:4+(i%3)*1.5f;
                var tint=H(Crystals[i%4],.1f);
                m.Cylinder(p,Quaternion.identity,w,h*.7f,1,tint,6);
                m.Cone(p+Vector3.up*h*.7f,Quaternion.identity,w,h*.3f,1,tint,6);
                for(float y=6;y<h*.65f;y+=7)m.Cylinder(p+Vector3.up*y,Quaternion.identity,w+.15f,.7f,2,new Color(.8f,.95f,1),6);
                m.Sphere(p+Vector3.up*(h+1.5f),Vector3.one*(i==0?3:1.6f),2,new Color(1,.85f,1),5,10);
            }
            // Arches linking the spires.
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI/2+.4f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*.62f;
                m.Ring(p+Vector3.up*6,Quaternion.LookRotation(p.normalized)*Quaternion.Euler(90,0,0),10,.8f,32,6,1,H(Crystals[(i+1)%4]));
            }
        }

        void LighthouseHarbor(ArtMesh m,float r)
        {
            // Striped lighthouse at the back, terraced houses stepping down toward the island.
            var lh=new Vector3(0,0,-r*.45f);
            for(int k=0;k<8;k++)m.Cylinder(lh+Vector3.up*k*7,Quaternion.identity,6.2f-k*.35f,7,1,H(k%2==0?CaneRed:Sugar),16);
            m.Cylinder(lh+Vector3.up*56,Quaternion.identity,4.6f,1.2f,1,H(Gold),16);
            m.Cylinder(lh+Vector3.up*57.2f,Quaternion.identity,3.6f,6,2,new Color(1,.95f,.7f),16);
            m.Cone(lh+Vector3.up*63.2f,Quaternion.identity,4.6f,6,1,H(CaneRed),16);
            for(int row=0;row<3;row++)
                for(int i=0;i<6;i++)
                {
                    float x=(i-2.5f)*r*.28f;var p=new Vector3(x,row*5,r*(.55f-row*.25f));
                    SkyTower(m,p,6,6+((i+row)%3)*3,Pastels[(i+row)%Pastels.Length],(i+row)%3==0?0:4,i+row*6);
                }
            for(int row=1;row<3;row++)m.Box(new Vector3(0,row*5-2.5f,r*(.55f-row*.25f)),new Vector3(r*1.6f,5,r*.25f),Quaternion.identity,1,H(new Color(1,.85f,.9f)));
            // Pink cotton-candy waterfall pouring off the side edge.
            var lip=new Vector3(r*.98f,-1,0);
            for(int i=0;i<34;i++)
            {
                float t=i/33f;var p=lip+new Vector3(4+t*t*14,-t*110,(Hash(new Vector3(i,1,3))-.5f)*(4+t*16));
                float puff=4+t*12;
                m.Sphere(p,new Vector3(puff*.8f,puff*1.4f,puff),0,H(Color.Lerp(new Color(1,.7f,.86f),Color.white,t),.08f),5,9);
            }
        }

        void AddLighthouseBeam(Transform city)
        {
            var m=new ArtMesh(3);
            foreach(int s in new[]{-1,1})m.Cone(Vector3.zero,Quaternion.Euler(0,0,s*90),3.2f,46,2,new Color(1,.95f,.75f),10);
            var beam=Model("Visual · sky city lighthouse beam",m,city,SkyMaterials);
            float r=44;beam.transform.localPosition=new Vector3(0,60,-r*.45f);lighthouseBeam=beam.transform;
        }

        void CupcakeHamlet(ArtMesh m,float r)
        {
            Cupcake(m,Vector3.zero,1.6f,0);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI*2/6+.5f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r*.66f;
                Cupcake(m,p,.6f+(i%2)*.15f,i+1);
            }
        }

        void Cupcake(ArtMesh m,Vector3 at,float scale,int seed)
        {
            var wrapper=H(Pastels[seed%Pastels.Length]);var icing=H(Color.Lerp(Pastels[(seed+2)%Pastels.Length],Color.white,.4f));
            m.Cylinder(at,Quaternion.identity,9.5f*scale,16*scale,1,wrapper,16);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                m.Box(at+dir*9.6f*scale+Vector3.up*8*scale,new Vector3(1.2f,16,.8f)*scale,Quaternion.LookRotation(dir),1,H(Color.Lerp(wrapper,Color.white,.3f)));
            }
            for(int k=0;k<4;k++)m.Sphere(at+Vector3.up*(18+k*5.5f)*scale,new Vector3(13-k*3,5,13-k*3)*scale,1,icing,6,14);
            m.Sphere(at+Vector3.up*41*scale,Vector3.one*3*scale,1,H(CaneRed),6,10);
            for(int y=0;y<2;y++)for(int i=0;i<5;i++){float a=i*.5f-1;var dir=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));m.Box(at+dir*9.75f*scale+Vector3.up*(5+y*5)*scale,new Vector3(1.6f,2,.3f)*scale,Quaternion.LookRotation(dir),2,WindowTints[(i+y)%3]);}
        }

        // A rainbow arching from one cloud city to the other, with bulbs along its edges and a candy train on top.
        void BuildRainbowBridge(Vector3 cityA,float radiusA,Vector3 cityB,float radiusB)
        {
            var across=Vector3.ProjectOnPlane(cityB-cityA,Vector3.up).normalized;
            var a=cityA+across*radiusA*.75f;var b=cityB-across*radiusB*.75f;
            float span=Vector3.Distance(a,b),rise=span*.55f;
            const int Steps=72;const float Band=4.2f;
            var bands=Spectrum;
            rainbowPath=new Vector3[Steps+1];rainbowUp=new Vector3[Steps+1];
            for(int i=0;i<=Steps;i++)
            {
                float t=i/(float)Steps;
                rainbowPath[i]=L(Vector3.Lerp(a,b,.5f-.5f*Mathf.Cos(Mathf.PI*t))+Vector3.up*rise*Mathf.Sin(Mathf.PI*t));
            }
            // "Up" across the bands: in the rainbow's plane, at right angles to the arc, pointing away from its inside.
            var normal=LR(Quaternion.identity)*Vector3.Cross(across,Vector3.up);
            for(int i=0;i<=Steps;i++)
            {
                var tangent=(rainbowPath[Mathf.Min(i+1,Steps)]-rainbowPath[Mathf.Max(i-1,0)]).normalized;
                rainbowUp[i]=Vector3.Cross(normal,tangent).normalized;
            }
            if(Vector3.Dot(rainbowUp[Steps/2],LR(Quaternion.identity)*Vector3.up)<0)for(int i=0;i<=Steps;i++)rainbowUp[i]=-rainbowUp[i];
            // The glow material adds the color on top of lighting, so bands are drawn at half strength
            // to stay saturated instead of bleaching to white (the old rainbow's problem).
            var m=new ArtMesh(3);var water=new ArtMesh();float total=Band*(bands.Length+.6f);
            for(int i=0;i<Steps;i++)
            {
                var p=rainbowPath[i];var q=rainbowPath[i+1];var up=rainbowUp[i];var up2=rainbowUp[i+1];
                // Soft white edges outside the colors.
                m.TwoSidedQuad(p,q,q+up2*Band*.3f,p+up*Band*.3f,2,new Color(.6f,.58f,.62f));
                for(int k=0;k<bands.Length;k++)
                {
                    float o0=Band*(.3f+k),o1=Band*(.3f+k+1);
                    water.WaterStrip(p+up*o0,p+up*o1,q+up2*o1,q+up2*o0,
                        Mathf.Abs(i-Steps*.5f)*.31f,Mathf.Abs(i+1-Steps*.5f)*.31f,k/7f,(k+1)/7f,bands[bands.Length-1-k]);
                }
                m.TwoSidedQuad(p+up*(total-Band*.3f),q+up2*(total-Band*.3f),q+up2*total,p+up*total,2,new Color(.6f,.58f,.62f));
                if(i%3==0){m.Sphere(p+up*(total+.6f),Vector3.one*.9f,2,new Color(1,.92f,.6f),3,6);m.Sphere(p-up*.6f,Vector3.one*.9f,2,new Color(1,.7f,.9f),3,6);}
            }
            // Cloud puffs where the rainbow lands.
            foreach(var end in new[]{0,Steps})
            {
                var foot=rainbowPath[end]+rainbowUp[end]*total*.5f;
                Splash(transform,foot+Vector3.up*4,total,end==0?"citadel":"bridge hamlet");
                for(int k=0;k<6;k++){float ang=k*1.05f;m.Sphere(foot+new Vector3(Mathf.Cos(ang)*9,-2+k%2*3,Mathf.Sin(ang)*9),new Vector3(12,8,12),0,Color.white,6,10);}
            }
            Model("Visual · rainbow bridge",m,transform,SkyMaterials);
            // Candy train riding along the top of the rainbow.
            for(int i=0;i<5;i++)
            {
                var c=new ArtMesh(3);var tint=H(Pastels[i%Pastels.Length],.1f);
                if(i==0)
                {
                    c.Box(Vector3.up*2.4f,new Vector3(4,3.4f,9),Quaternion.identity,1,H(CaneRed,.1f));
                    c.Cylinder(new Vector3(0,4.2f,4.5f),Quaternion.Euler(90,0,0),1.7f,4,1,H(Gold,.1f),12);
                    c.Cone(new Vector3(0,5.5f,6.5f),Quaternion.identity,1,3,1,H(Sugar,.1f),8);
                    c.Sphere(new Vector3(0,3.8f,9),Vector3.one*.9f,2,new Color(1,.95f,.7f),4,8);
                }
                else
                {
                    c.Box(Vector3.up*2.4f,new Vector3(4,3.4f,8),Quaternion.identity,1,tint);
                    c.Box(Vector3.up*3,new Vector3(4.1f,1.3f,6.4f),Quaternion.identity,2,WindowTints[i%3]);
                    c.Box(Vector3.up*4.3f,new Vector3(4.4f,.5f,8.4f),Quaternion.identity,1,H(Sugar,.1f));
                }
                foreach(int s in new[]{-1,1})foreach(int e in new[]{-1,1})c.Cylinder(new Vector3(s*2.1f,.8f,e*2.6f),Quaternion.Euler(0,0,90),.8f,.5f,1,H(new Color(.4f,.2f,.3f),.1f),10);
                var car=Model("Visual · rainbow train car",c,transform,SkyMaterials);car.transform.localScale=Vector3.one*2;
                trainCars.Add((car.transform,i*.042f));
            }
        }

        // A small spring island high over the edge of our island; its rainbow waterfall lands on our cloud,
        // just outside the play area so it never hides the streets.
        void BuildIslandAbove(Vector3 center)
        {
            float R=Mathf.Max(halfWidth,halfDepth),a=150*Mathf.Deg2Rad;const float r=30,height=140,push=12;
            var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            var world=center+dir*(R*1.08f+r+push)+Vector3.up*height;
            var m=new ArtMesh(3);
            CloudIsland(m,r,new Color(.7f,.6f,1),7);
            // Rainbow spring: a gold-pillared gazebo over a glowing pool, with little cottages around it.
            m.Cylinder(Vector3.zero,Quaternion.identity,9,1.4f,1,H(Sugar),24);
            m.Cylinder(Vector3.up*1.2f,Quaternion.identity,7.6f,.4f,2,new Color(.45f,.7f,.9f),24);
            for(int i=0;i<8;i++){float b=i*Mathf.PI/4;m.Cylinder(new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*8.4f,Quaternion.identity,.5f,10,1,H(Gold),8);}
            m.Cylinder(Vector3.up*10,Quaternion.identity,9.4f,1,1,H(Gold),24);
            m.StripedCone(Vector3.up*11,10,7,16,1,H(new Color(.68f,.5f,1)),H(Sugar));
            m.Sphere(Vector3.up*18.4f,Vector3.one*1.4f,2,new Color(1,.85f,.55f),5,10);
            for(int i=0;i<7;i++){float b=i*Mathf.PI*2/7+.4f;var p=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*r*.72f;if(p.z>r*.4f)continue;SkyTower(m,p,5,5+i%3*2,Pastels[i%Pastels.Length],i%2==0?0:1,i);}
            var go=Model("Visual · spring island above",m,transform,SkyMaterials);
            go.transform.localPosition=L(world);go.transform.localRotation=LR(Quaternion.LookRotation(-dir));
            // Lands on the cloud skirt around our island (about 11 m below the streets).
            RainbowFall(go.transform,new Vector3(0,-1,r+1),22,push,height+10,"above");
        }

        // A seven-band rainbow sheet pouring forward (+z of its parent) off a lip, curving down,
        // with bright streaks sliding down it and rainbow mist where it lands.
        void RainbowFall(Transform parent,Vector3 lip,float width,float push,float drop,string label)
        {
            var water=new ArtMesh();
            Vector3 At(float t)=>lip+new Vector3(0,-drop*t*t,push*t);
            const int steps=40;
            for(int i=0;i<steps;i++)
            {
                float t=i/(float)steps,next=(i+1)/(float)steps;
                Vector3 Bank(float time,float across)=>At(time)+Vector3.right*(across-.5f)*width*(1+time*.45f);
                for(int k=0;k<Spectrum.Length;k++)
                {
                    float u=k/7f,v=(k+1)/7f;
                    water.WaterStrip(Bank(t,u),Bank(t,v),Bank(next,v),Bank(next,u),t*8,next*8,u,v,Spectrum[k]);
                }
            }
            CurrentModel("Visual � rainbow waterfall "+label,water,parent);
            Splash(parent,At(1),width*1.45f,label);
        }

        void AnimateSkyCities(float time)
        {
            foreach(var (city,home,phase) in floatingCities)if(city)city.localPosition=home+Vector3.up*Mathf.Sin(time*.18f+phase)*2.2f;
            if(ferrisWheel)
            {
                float spin=time*4;ferrisWheel.localRotation=Quaternion.Euler(0,0,spin);
                for(int i=0;i<wheelCars.Count;i++)
                {
                    if(!wheelCars[i])continue;float a=(spin+i*30)*Mathf.Deg2Rad;
                    wheelCars[i].localPosition=wheelHub+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*wheelRadius;wheelCars[i].localRotation=Quaternion.identity;
                }
            }
            if(lighthouseBeam)lighthouseBeam.localRotation=Quaternion.Euler(0,time*28,0);
            if(rainbowPath!=null&&rainbowPath.Length>1)
            {
                int last=rainbowPath.Length-1;float top=4.2f*7.6f;
                foreach(var (car,offset) in trainCars)
                {
                    if(!car)continue;
                    // One way across, then round again; the cars disappear into the landing clouds at each end.
                    float t=Mathf.Repeat(time*.02f+.4f,1.25f)-offset;
                    bool show=t>.03f&&t<.97f;if(car.gameObject.activeSelf!=show)car.gameObject.SetActive(show);
                    t=Mathf.Clamp01(t);
                    float f=t*last;int i=Mathf.Min((int)f,last-1);float u=f-i;
                    var up=Vector3.Lerp(rainbowUp[i],rainbowUp[i+1],u).normalized;
                    car.localPosition=Vector3.Lerp(rainbowPath[i],rainbowPath[i+1],u)+up*top;
                    car.localRotation=Quaternion.LookRotation(rainbowPath[i+1]-rainbowPath[i],up);
                }
            }
        }

        void ClearSkyCities(){if(rainbowCurrent)Release(rainbowCurrent);rainbowCurrent=null;floatingCities.Clear();wheelCars.Clear();trainCars.Clear();ferrisWheel=lighthouseBeam=null;rainbowPath=rainbowUp=null;}

        sealed partial class ArtMesh
        {
            public void TwoSidedQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int mat,Color color){Quad(a,b,c,d,mat,color);Quad(d,c,b,a,mat,color);}

            // A cone whose wedges alternate between two colors (circus tents).
            public void StripedCone(Vector3 bottom,float radius,float height,int sides,int mat,Color one,Color two)
            {
                var tip=bottom+Vector3.up*height;
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var pa=bottom+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;var pb=bottom+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius;
                    Tri(pb,pa,tip,Vector3.Cross(pa-pb,tip-pb).normalized,mat,i%2==0?one:two);
                }
            }

            // A ring (torus) in any orientation; its hole points along rotation * up.
            public void Ring(Vector3 center,Quaternion rotation,float radius,float tube,int segments,int sides,int mat,Color color)
            {
                int start=vertices.Count;
                for(int i=0;i<=segments;i++)
                {
                    float a=i*Mathf.PI*2/segments;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    for(int j=0;j<=sides;j++)
                    {
                        float b=j*Mathf.PI*2/sides;var n=dir*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b);
                        vertices.Add(center+rotation*(dir*radius+n*tube));normals.Add(rotation*n);colors.Add(color);
                    }
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    int a=start+i*(sides+1)+j,b=a+sides+1;
                    triangles[mat].AddRange(new[]{a,a+1,b+1,a,b+1,b});
                }
            }
        }
    }
}

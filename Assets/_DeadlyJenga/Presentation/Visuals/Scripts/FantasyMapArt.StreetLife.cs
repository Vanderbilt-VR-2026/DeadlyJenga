using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy Sky Ring City street life and its centerpiece. Art only, no colliders.
    //   planters, candy hydrants and signposts dress the boulevard.
    //   A gold "sky ring" floats and slowly turns above the fountain plaza, visible from every street.
    public sealed partial class FantasyMapArt
    {
        Transform skyRing;Vector3 skyRingHome;

        void BuildStreetLife()
        {
            var center=roadCenter;var m=new ArtMesh(2);
            // A signpost where each avenue meets the boulevard.
            for(int k=0;k<8;k++)
            {
                float a=k*Mathf.PI/4+Mathf.PI/8;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir);var q=Quaternion.LookRotation(dir);
                var post=center+dir*(roadRadius+7.4f)+side*3.6f;if(Free(post,.8f))Signpost(m,post,q,k);
            }
            // Planters and candy hydrants on the outer sidewalk.
            for(int i=1;i<RoadBins;i+=4)
            {
                if(!roadOpen[i])continue;
                float a=(i+.5f)*Mathf.PI*2/RoadBins;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var p=center+dir*(roadRadius+5.8f);
                if(!Free(p,.9f))continue;
                if(i%8==1)Planter(m,p,i);else Hydrant(m,p);
            }
            Model("Visual · candy street life",m,transform,new[]{cityCandyMaterial,cityGlowMaterial});

            BuildCloudResidents();
            AnimateLandmarks(Time.time);
        }

        void Signpost(ArtMesh m,Vector3 p,Quaternion q,int seed)
        {
            m.Cylinder(L(p),LR(Quaternion.identity),.08f,3.2f,0,Sugar,8);
            var colors=new[]{new Color(1,.36f,.62f),new Color(.4f,.75f,1),new Color(1,.82f,.3f),new Color(.5f,.84f,.16f)};
            for(int i=0;i<2;i++)
            {
                var r=q*Quaternion.Euler(0,i==0?90:-60,0);
                m.Box(L(p+Vector3.up*(2.6f-i*.55f)+r*Vector3.forward*.6f),new Vector3(.08f,.4f,1.3f),LR(r),0,colors[(seed+i)%4]);
            }
            m.Sphere(L(p+Vector3.up*3.25f),Vector3.one*.14f,0,Gold,3,6);
        }

        void Planter(ArtMesh m,Vector3 p,int seed)
        {
            m.Cylinder(L(p),LR(Quaternion.identity),.75f,.6f,0,new Color(1,.55f,.75f),12);
            m.Sphere(L(p+Vector3.up*.75f),new Vector3(.7f,.35f,.7f),0,Grass,5,10);
            for(int i=0;i<5;i++){float a=i*1.26f+seed;m.Sphere(L(p+new Vector3(Mathf.Cos(a)*.4f,.98f,Mathf.Sin(a)*.4f)),Vector3.one*.14f,0,RoofTints[(i+seed)%4],3,6);}
        }

        void Hydrant(ArtMesh m,Vector3 p)
        {
            m.Cylinder(L(p),LR(Quaternion.identity),.2f,.65f,0,CaneRed,10);
            m.Sphere(L(p+Vector3.up*.68f),new Vector3(.22f,.16f,.22f),0,Sugar,4,8);
            m.Box(L(p+Vector3.up*.42f),new Vector3(.6f,.12f,.12f),LR(Quaternion.identity),0,Sugar);
        }

        void BuildSkyRing()
        {
            var m=new ArtMesh(2);const float R=10;
            m.Torus(Vector3.zero,R,.75f,64,12,0,Gold);
            m.Torus(Vector3.zero,R-1.5f,.28f,56,8,0,new Color(1,.36f,.62f));
            for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI*2/32;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                m.Sphere(dir*(R+.75f),Vector3.one*.32f,1,i%2==0?new Color(1,.9f,.6f):new Color(1,.6f,.8f),4,8);
                if(i%4==0)m.Box(dir*(R-.75f),new Vector3(.12f,.12f,1.5f),Quaternion.LookRotation(dir),0,Gold);
            }
            var go=Model("Visual · sky ring",m,transform,new[]{cityCandyMaterial,cityGlowMaterial});
            var home=Services.World.Center;home.y=groundY+38;skyRingHome=L(home);
            skyRing=go.transform;skyRing.localPosition=skyRingHome;
        }

        void AnimateLandmarks(float time)
        {
            AnimateElevators(time);
            if(skyRing)
            {
                skyRing.localPosition=skyRingHome+Vector3.up*Mathf.Sin(time*.35f)*1.2f;
                // Tilted enough that street-level players see the ring's face, not just its edge.
                skyRing.localRotation=Quaternion.Euler(0,time*5,0)*Quaternion.Euler(22+4*Mathf.Sin(time*.21f),0,0);
            }
        }

        void ClearLandmarks(){elevators.Clear();skyRing=null;ClearSkyCities();}

        sealed partial class ArtMesh
        {
            // A smooth ring lying flat (around the Y axis).
            public void Torus(Vector3 center,float radius,float tube,int segments,int sides,int mat,Color color)
            {
                int start=vertices.Count;
                for(int i=0;i<=segments;i++)
                {
                    float a=i*Mathf.PI*2/segments;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    for(int j=0;j<=sides;j++)
                    {
                        float b=j*Mathf.PI*2/sides;var n=dir*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b);
                        vertices.Add(center+dir*radius+n*tube);normals.Add(n);colors.Add(color);
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

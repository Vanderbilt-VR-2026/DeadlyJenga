using System.Collections.Generic;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    public sealed partial class MayhemMarina
    {
        void Planting()
        {
            for(int i=0;i<28;i++)
            {
                float a=Mathf.Lerp(-106,117,i/27f)*Mathf.Deg2Rad;
                Rock(new Vector3(Mathf.Sin(a)*24.0f,-.15f,4+Mathf.Cos(a)*21),new Vector3(1.4f+i%3*.38f,1.1f+i%2*.32f,1.6f),i+50,true);
            }
            Palm(new Vector3(-20,1.3f,10),6.8f,.65f,0);Palm(new Vector3(-16,4.15f,19),5.2f,-.4f,1);
            Palm(new Vector3(-20,4.15f,18.6f),6.1f,.45f,2);Palm(new Vector3(-10.5f,4.15f,21),4.7f,.5f,3);
            Palm(new Vector3(6,4.15f,20),5.8f,-.5f,4);Palm(new Vector3(19.5f,1.3f,10),5.9f,-.5f,5);
            Palm(new Vector3(22,1.3f,3),5.3f,.6f,6);Palm(new Vector3(-22,1.3f,-1.5f),4.6f,-.4f,7);
            Palm(new Vector3(-23,1.3f,16),5.7f,.45f,8);
            Vector3[] pots={new(-16,1.3f,2),new(-18.5f,1.3f,12.8f),new(-4.7f,1.86f,5.2f),new(4.7f,1.86f,5.2f),new(6.4f,1.3f,2),new(9.5f,1.3f,2),new(18.8f,1.3f,1.8f),new(-6.9f,4.15f,15),new(-18.3f,4.15f,14),new(2.5f,4.15f,20),new(-6,4.15f,20.5f)};
            for(int i=0;i<pots.Length;i++)Pot(pots[i],.45f+(i%3)*.07f,.72f+(i%2)*.12f,i+20);
            for(int i=0;i<18;i++)
            {
                float a=Mathf.Lerp(-125,110,i/17f)*Mathf.Deg2Rad;
                var p=new Vector3(Mathf.Sin(a)*21.2f,1.25f,4+Mathf.Cos(a)*18.7f);
                Shrub(p,.6f+(i%3)*.15f,i);
            }
            for(int i=0;i<5;i++)
            {
                var p=new Vector3(8.1f+i*1.7f,-.03f,-7.7f-i*.9f);
                Buoy(p,i%2==0?F.Coral:F.Teal);
            }
            LifeRing(new Vector3(-16.2f,1.7f,-20.5f),Quaternion.Euler(0,25,0));
        }
        void Palm(Vector3 p,float height,float lean,int seed)
        {
            Vector3 offset=new(Mathf.Sin(seed*1.7f)*lean,0,Mathf.Cos(seed*2.2f)*lean);var path=new List<Vector3>();
            for(int i=0;i<=16;i++){float t=i/16f;path.Add(p+Vector3.up*(height*t)+offset*t*t);}
            mesh.Tube(path,.21f,F.Wood,12,false,.13f);Vector3 crown=path[path.Count-1];
            for(int i=0;i<14;i++)
            {
                float t=(i+.5f)/15f;var center=p+Vector3.up*(height*t)+offset*t*t;
                mesh.Ellipsoid(center,new Vector3(Mathf.Lerp(.215f,.14f,t),.027f,Mathf.Lerp(.215f,.14f,t)),F.DarkWood,rings:4,sides:12);
            }
            for(int leaf=0;leaf<8;leaf++)
            {
                float angle=(leaf/8f*Mathf.PI*2)+seed*.37f;Vector3 dir=new(Mathf.Cos(angle),0,Mathf.Sin(angle)),side=Vector3.Cross(Vector3.up,dir);
                float length=height*.52f;var stem=new List<Vector3>();
                for(int i=0;i<=16;i++){float t=i/16f;stem.Add(crown+dir*(length*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.8f-.95f*t*t));}
                mesh.Tube(stem,.032f,F.LeafLight,5,false,.009f);
                for(int i=1;i<15;i++)
                {
                    float t=i/16f;var start=crown+dir*(length*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.8f-.95f*t*t);
                    float blade=Mathf.Sin(t*Mathf.PI)*1.05f;
                    mesh.Leaf(start,(side*.94f+dir*.35f).normalized,blade,.12f,.10f,leaf%2==0?F.Leaf:F.LeafLight);
                    mesh.Leaf(start,(-side*.94f+dir*.35f).normalized,blade,.12f,.10f,leaf%2==0?F.LeafLight:F.Leaf);
                }
            }
            for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;mesh.Ellipsoid(crown+new Vector3(Mathf.Cos(a)*.2f,-.14f,Mathf.Sin(a)*.2f),new Vector3(.16f,.2f,.16f),F.DarkWood,rings:7,sides:10);}
            Shrub(p,.8f,seed);
        }
        void Pot(Vector3 p,float radius,float height,int seed)
        {
            mesh.Disk(p,radius*.82f,height*.12f,F.Ivory,false,24);
            mesh.Ellipsoid(p+Vector3.up*height*.49f,new Vector3(radius,height*.51f,radius),F.Ivory,rings:10,sides:20);
            mesh.Disk(p+Vector3.up*(height-.04f),radius*.89f,.06f,F.DarkWood,false,24);
            var rim=new List<Vector3>();for(int i=0;i<=32;i++){float a=i*Mathf.PI/16;rim.Add(p+new Vector3(Mathf.Cos(a)*radius*.89f,height,Mathf.Sin(a)*radius*.89f));}mesh.Tube(rim,.045f,F.Ivory,8);
            Shrub(p+Vector3.up*height,radius*1.5f,seed);
        }
        void Shrub(Vector3 p,float scale,int seed)
        {
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.764f+seed;Vector3 d=new(Mathf.Cos(a),0,Mathf.Sin(a));
                mesh.Leaf(p,d,scale*(.8f+(i%3)*.16f),scale*.18f,scale*.58f,i%2==0?F.LeafLight:F.Leaf);
            }
            if(seed%3!=0)return;
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;var center=p+new Vector3(Mathf.Cos(a)*scale*.22f,.48f*scale,Mathf.Sin(a)*scale*.22f);
                mesh.Beam(p,center,.018f,F.Leaf, false);
                for(int petal=0;petal<5;petal++){float b=petal*Mathf.PI*2/5;mesh.Ellipsoid(center+new Vector3(Mathf.Cos(b)*.09f,.01f,Mathf.Sin(b)*.09f),new Vector3(.085f,.04f,.085f),seed%2==0?F.Coral:F.Purple,rings:5,sides:8);}
                mesh.Ellipsoid(center+Vector3.up*.03f,Vector3.one*.045f,F.Brass,rings:4,sides:8);
            }
        }
        void Lantern(Vector3 p,float radius)
        {
            mesh.Ellipsoid(p,new Vector3(radius,radius*1.2f,radius),F.Glow,rings:10,sides:16);
            mesh.Disk(p-Vector3.up*radius*1.17f,radius*.6f,.05f,F.Brass,false,16);
            mesh.Disk(p+Vector3.up*radius*1.1f,radius*.6f,.05f,F.Brass,false,16);
            mesh.Beam(p+Vector3.up*radius,p+Vector3.up*(radius+.3f),.025f,F.Brass);
        }
        void Rock(Vector3 p,Vector3 radius,int seed,bool solid=false)
        {
            mesh.Ellipsoid(p,radius,F.Rock,Quaternion.Euler(seed*17,seed*63,seed*11),rings:6,sides:10,solid:solid);
            mesh.Ellipsoid(p+new Vector3(radius.x*.43f,radius.y*.21f,0),radius*.64f,F.Ivory,Quaternion.Euler(14,seed*41,7),rings:5,sides:8);
        }
        void Buoy(Vector3 p,F color)
        {
            mesh.Disk(p,.23f,.16f,F.Ivory,false,20);mesh.Disk(p+Vector3.up*.15f,.16f,.19f,color,false,20);
            mesh.Disk(p+Vector3.up*.34f,.13f,.12f,F.Ivory,false,20);mesh.Ellipsoid(p+Vector3.up*.48f,new Vector3(.13f,.18f,.13f),color,rings:7,sides:12);
        }
        void LifeRing(Vector3 p,Quaternion q)
        {
            for(int section=0;section<8;section++)
            {
                var path=new List<Vector3>();for(int i=0;i<=8;i++){float a=(section+i/8f)*Mathf.PI/4;path.Add(p+q*new Vector3(Mathf.Cos(a)*.48f,Mathf.Sin(a)*.48f,0));}
                mesh.Tube(path,.105f,section%2==0?F.Coral:F.Ivory,10);
            }
        }
        void DistantIslands()
        {
            for(int i=0;i<7;i++)
            {
                float a=Mathf.Lerp(-76,86,i/6f)*Mathf.Deg2Rad;float distance=145+(i%3)*37;var p=new Vector3(Mathf.Sin(a)*distance,-1.4f,Mathf.Cos(a)*distance+22);
                for(int cluster=0;cluster<5;cluster++)
                {
                    float r=cluster*1.7f;var center=p+new Vector3(Mathf.Cos(r)*6,cluster%2*1.3f,Mathf.Sin(r)*4);
                    Rock(center,new Vector3(4.5f+cluster%2,4+cluster%3*1.4f,3.8f),i*11+cluster);
                }
                if(i%2==0)Palm(p+Vector3.up*(6+(i%3)*2),4.5f,.4f,i+30);
                if(i==5)
                {
                    mesh.Disk(p+Vector3.up*8,.85f,6,F.Ivory,false,16);mesh.Disk(p+Vector3.up*13.7f,1.08f,.22f,F.Wood,false,16);mesh.Disk(p+Vector3.up*13.9f,.75f,.9f,F.Teal,false,16);mesh.Disk(p+Vector3.up*14.8f,1.05f,.18f,F.Coral,false,16);
                }
            }
        }
    }
}

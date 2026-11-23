using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class GingerbreadKingdom
    {
        void Steps(string name,Vector3 start,int count,float width,float rise,float run,float direction=1)
        {
            for(int i=0;i<count;i++)
            {
                float height=(i+1)*rise;
                Part(name,V(start.x,start.y+height*.5f,start.z+direction*(i+.5f)*run),V(width,height,run),Biscuit,false,
                    m=>m.Box(Ice,V(0,height*.5f+.015f,0),V(width+.03f,.03f,run)));
            }
        }
        void Palace()
        {
            plot=Group("Princess palace",town,V(3,0,18));
            Steps("Grand stair",V(0,0,-8.82f),18,5.8f,.16f,.29f);
            Part("Royal terrace",V(0,1.44f,0),V(17,2.88f,7.2f),Caramel,false);
            for(int s=-1;s<=1;s+=2)
            {
                Steps("Garden stair",V(s*7.25f,0,-9),18,2.25f,.16f,.30f);
                Part("Gallery colonnade",V(s*5.1f,4.32f,0),V(.76f,2.88f,4.8f),Caramel);
                Part("Gallery outer pier",V(s*7.35f,4.32f,0),V(.8f,2.88f,4.8f),Biscuit);
                Part("Gallery balcony",V(s*6.2f,5.96f,0),V(3.4f,.4f,5.4f),Caramel,true,m=>Rail(m,3.25f,-2.65f,.2f));
                Tower(s*6.25f,6.16f,1.0f,2.25f,3.15f,s<0?Violet:Pink);
                Tower(s*3.95f,2.88f,-1.25f,2,4.65f,s<0?Pink:Violet);
                Tower(s*2.75f,2.88f,2.15f,2.35f,6.7f,s<0?Violet:Pink);
                Part("Entrance pillar",V(s*1.52f,4.86f,-1.45f),V(.82f,3.96f,2.65f),Biscuit,true,
                    m=>Window(m,V(0,-1.3f,-1.35f),.55f,2.4f));
            }
            Part("Audience hall rear",V(0,4.86f,1.45f),V(4,3.96f,1.3f),Cocoa,true,
                m=>Window(m,V(0,-1.9f,-.68f),2.45f,3.65f));
            Part("Royal loggia",V(0,7.05f,0),V(4.5f,.42f,5.6f),Biscuit,true,m=>{
                Rail(m,4.3f,-2.68f,.21f);Arch(m,V(0,-4.12f,-2.85f),1.38f,2.46f,Ice);
                Arch(m,V(0,-4.12f,-2.89f),1.54f,2.47f,Gold);
            });
            Tower(0,7.26f,.32f,3.1f,4.45f,Pink);
        }
        void Tower(float x,float y,float z,float w,float h,int color)
        {
            Part("Turret drum",V(x,y+h*.5f,z),V(w,h,w),Biscuit,true,m=>{
                Window(m,V(0,-h*.34f,-w*.5f-.05f),w*.59f,h*.68f);
                for(int s=-1;s<=1;s+=2)
                    m.Box(Ice,V(s*(w*.5f-.06f),0,-w*.5f-.05f),V(.13f,h,.15f));
            });
            Part("Spire and cornice",V(x,y+h+.14f,z),V(w+.24f,.28f,w+.24f),Ice,true,m=>{
                float r=w*.78f,peak=w*1.63f;
                m.Ring(Caramel,V(0,.14f,0),r,r,.24f);
                m.Ring(color,V(0,.38f,0),r,.065f,peak,12);
                for(int i=0;i<12;i+=2)
                {
                    float a=i*Mathf.PI/6;
                    m.Line(Ice,V(r*Mathf.Cos(a),.39f,r*Mathf.Sin(a)),V(0,peak+.4f,0),.09f);
                }
                m.Ring(Gold,V(0,peak+.34f,0),.15f,.04f,.49f);
            });
        }
        void Market()
        {
            plot=Group("Fountain market",town,V(0,0,3));
            Part("Fountain plinth",V(0,.14f,0),V(2.1f,.28f,2.1f),Stone,false,m=>{
                m.Ring(Caramel,V(0,.14f,0),1.5f,1.55f,.24f,24);
                m.Ring(Ice,V(0,.38f,0),1.57f,1.53f,.11f,24);
                m.Ring(Violet,V(0,.5f,0),1.32f,1.3f,.025f,24);
                m.Ring(Biscuit,V(0,.5f,0),.32f,.18f,.75f);
                m.Ring(Gold,V(0,1.25f,0),.24f,.67f,.23f);
                m.Ring(Ice,V(0,1.48f,0),.16f,.05f,.48f);
            });
            for(int s=-1;s<=1;s+=2)
            {
                Part("Market counter",V(s*3,.45f,3.7f),V(2.2f,.9f,1.1f),Caramel,false,m=>{
                    for(int k=-1;k<=1;k+=2)m.Box(Cocoa,V(k*.95f,.86f,0),V(.11f,1.55f,.11f));
                    for(int i=0;i<7;i++)m.Box(i%2==0?Ice:s<0?Red:Green,V((i-3)*.35f,1.72f,0),V(.35f,.10f,1.75f),Quaternion.Euler(-12,0,0));
                    for(int i=0;i<7;i++)m.Ring(i%2==0?Red:Gold,V((i-3)*.24f,.45f,.06f),.14f,.1f,.19f);
                });
            }
        }
        void Bridge(float x,float z)
        {
            plot=Group("Icing footbridge",town,V(x,0,z));
            Steps("South bridge stair",V(0,0,-5.45f),10,1.65f,.16f,.34f);
            Steps("North bridge stair",V(0,0,5.45f),10,1.65f,.16f,.34f,-1);
            Part("Pullable bridge bearing",V(0,.8f,0),V(1.35f,1.6f,1.68f),Caramel);
            Part("Bridge span",V(0,1.73f,0),V(1.72f,.26f,4.02f),Biscuit,true,m=>{
                for(int s=-1;s<=1;s+=2)
                {
                    m.Box(Ice,V(s*.79f,.63f,0),V(.12f,.11f,4));
                    for(int j=-3;j<=3;j++)m.Box(Caramel,V(s*.79f,.34f,j*.56f),V(.10f,.45f,.10f));
                }
            });
        }
    }
}

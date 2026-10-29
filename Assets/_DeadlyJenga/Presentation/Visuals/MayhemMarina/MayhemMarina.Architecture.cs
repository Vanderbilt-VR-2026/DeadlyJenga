using System.Collections.Generic;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    public sealed partial class MayhemMarina
    {
        void ShoreAndCourts()
        {
            mesh.ArcSlab(new Vector3(0,-1.6f,4),new Vector2(9,11),new Vector2(24,21),2.8f,-110,120,F.Rock,true,72);
            mesh.ArcSlab(new Vector3(0,1.12f,4),new Vector2(9,11),new Vector2(23.8f,20.8f),.13f,-110,120,F.Stone,true,72);
            mesh.Box(new Vector3(0,.6f,10),new Vector3(16,1.2f,15),F.Ivory,.28f,true);
            mesh.Box(new Vector3(0,1.22f,10),new Vector3(16,.13f,15),F.Stone,.045f,true);
            mesh.Box(new Vector3(-14,.6f,8),new Vector3(11,1.2f,12),F.Ivory,.4f,true);
            mesh.Box(new Vector3(-14,1.22f,8),new Vector3(11,.13f,12),F.Stone,.04f,true);
            mesh.Box(new Vector3(14,.6f,7),new Vector3(12,1.2f,11),F.Ivory,.4f,true);
            mesh.Box(new Vector3(14,1.22f,7),new Vector3(12,.13f,11),F.Stone,.04f,true);
            mesh.Disk(new Vector3(-10,-.8f,-8),5.55f,2,F.Rock,true);
            mesh.Disk(new Vector3(-10,1.2f,-8),5.35f,.13f,F.Stone,true);
            mesh.ArcSlab(new Vector3(-10,1.34f,-8),Vector2.one*4.9f,Vector2.one*5.3f,.10f,15,250,F.Wood,false,48);
            // Individual copings give the water's edge a rounded masonry rhythm.
            for(int i=0;i<30;i++)
            {
                float a=Mathf.Lerp(-128,123,i/29f)*Mathf.Deg2Rad;
                var p=new Vector3(Mathf.Sin(a)*8.85f,1.23f,4+Mathf.Cos(a)*10.85f);
                mesh.Box(p,new Vector3(1.05f,.24f,.48f),F.Ivory,.07f,false,Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0));
            }
            Bridge(new Vector3(-18,1.17f,-25),new Vector3(-10,1.34f,-12.3f),3.6f,0);
            Bridge(new Vector3(-9.5f,1.34f,-2.8f),new Vector3(-7.5f,1.35f,3.3f),2.7f,.13f);
            Bridge(new Vector3(-5.45f,1.34f,-5.1f),new Vector3(9.0f,1.3f,2),2.45f,.48f);
            mesh.Disk(new Vector3(17,-.7f,-8),5.5f,1,F.Rock,true);
            mesh.Disk(new Vector3(17,.27f,-8),4.82f,.11f,F.Stone,true);
            mesh.ArcSlab(new Vector3(17,.3f,-8),Vector2.one*4.8f,Vector2.one*5.45f,.78f,0,360,F.Ivory,true,64);
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;var p=new Vector3(17+Mathf.Sin(a)*5.12f,1.08f,-8+Mathf.Cos(a)*5.12f);
                mesh.Box(p,new Vector3(1.22f,.15f,.68f),F.Stone,.05f,false,Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0));
            }
            Bridge(new Vector3(14.5f,1.3f,1.4f),new Vector3(17,1.13f,-2.7f),2.3f,.12f);
            // A few blocks already in the basin make its purpose legible before anyone interacts.
            for(int i=0;i<6;i++)
            {
                float a=i*2.39f;var p=new Vector3(17+Mathf.Cos(a)*3.3f,.59f,-8+Mathf.Sin(a)*3.3f);
                mesh.Box(p,new Vector3(1.2f,.48f,.68f),(F)((int)F.Red+i%3),.09f,false,Quaternion.Euler(i*9,i*43,8));
            }
            TileLines(new Vector3(0,1.292f,8),new Vector2(15,10));
            TileLines(new Vector3(-14,1.292f,7),new Vector2(10,10));
            for(int i=0;i<5;i++)
            {
                float a=i*2.21f;Rock(new Vector3(-10+Mathf.Cos(a)*5.35f,-.05f,-8+Mathf.Sin(a)*5.35f),new Vector3(1.7f,.9f,1.3f),i);
                Rock(new Vector3(17+Mathf.Cos(a)*5.6f,-.2f,-8+Mathf.Sin(a)*5.6f),new Vector3(1.2f,.8f,1.5f),i+9);
            }
        }
        void TileLines(Vector3 center,Vector2 area)
        {
            for(float x=-area.x*.5f+.8f;x<area.x*.5f;x+=1.55f)mesh.Box(center+Vector3.right*x,new Vector3(.013f,.006f,area.y),F.Ivory,.001f);
            for(float z=-area.y*.5f+.7f;z<area.y*.5f;z+=1.55f)mesh.Box(center+Vector3.forward*z,new Vector3(area.x,.006f,.013f),F.Ivory,.001f);
        }
        void Bridge(Vector3 from,Vector3 to,float width,float arch)
        {
            Vector3 direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized,side=Vector3.Cross(Vector3.up,direction);float length=Vector3.Distance(from,to);var q=Quaternion.LookRotation(direction);
            Vector3 P(float t)=>Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*arch;
            int planks=Mathf.CeilToInt(length/.29f);
            for(int i=0;i<planks;i++)
            {
                float t=(i+.5f)/planks;var slope=(P(Mathf.Min(1,t+.01f))-P(Mathf.Max(0,t-.01f))).normalized;
                mesh.Box(P(t)-Vector3.up*.07f,new Vector3(width,.14f,length/planks+.009f),F.Wood,.025f,true,Quaternion.LookRotation(slope));
                foreach(int sign in new[]{-1,1})mesh.Ellipsoid(P(t)+side*(sign*(width*.5f-.12f))+Vector3.up*.007f,new Vector3(.025f,.008f,.025f),F.Brass,rings:4,sides:8);
            }
            int posts=Mathf.CeilToInt(length/2.1f);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var ropeLow=new List<Vector3>();var ropeHigh=new List<Vector3>();
                for(int i=0;i<=posts;i++)
                {
                    float t=i/(float)posts;var p=P(t)+side*(sign*(width*.5f+.08f));Post(p);
                    if(i%2==0)mesh.Beam(p-Vector3.up*.15f,p-Vector3.up*2.1f,.14f,F.DarkWood);
                    for(int s=0;s<10;s++)
                    {
                        float u=s/10f;if(i==posts&&s>0)break;
                        float at=(i+u)/posts;var r=P(Mathf.Min(1,at))+side*(sign*(width*.5f+.08f));float sag=Mathf.Sin(u*Mathf.PI)*.13f;
                        ropeLow.Add(r+Vector3.up*(.55f-sag));ropeHigh.Add(r+Vector3.up*(1.0f-sag));
                    }
                }
                mesh.Tube(ropeLow,.035f,F.Wood,7);mesh.Tube(ropeHigh,.045f,F.Wood,7,true);
            }
        }
        void Post(Vector3 p)
        {
            mesh.Box(p+Vector3.up*.48f,new Vector3(.19f,1.03f,.19f),F.Wood,.025f,true);
            mesh.Box(p+Vector3.up*.91f,new Vector3(.27f,.35f,.27f),F.Ivory,.06f);
            mesh.Box(p+Vector3.up*1.1f,new Vector3(.28f,.13f,.28f),F.Purple,.05f);
        }
        void Pavilion()
        {
            for(int step=0;step<3;step++)mesh.Box(new Vector3(0,1.35f+step*.14f,4+step*.44f),new Vector3(13-step*.4f,.24f,1.15f),F.Stone,.09f,true);
            mesh.Box(new Vector3(0,1.56f,8.45f),new Vector3(13,.6f,6.9f),F.Stone,.22f,true);
            mesh.Box(new Vector3(0,3.7f,11.65f),new Vector3(13.3f,4.0f,.5f),F.Teal,.2f,true);
            for(int side=-1;side<=1;side+=2)
            {
                mesh.Box(new Vector3(side*6.55f,3.1f,9.0f),new Vector3(1.5f,3.9f,5.1f),F.Ivory,.4f,true);
                for(int n=0;n<4;n++)mesh.Box(new Vector3(side*5.7f,1.64f+n*.61f,5.05f),new Vector3(1.0f,.58f,1),n==0?F.Red:n==1?F.Green:F.Purple,.11f,true,Quaternion.Euler(0,n%2*90,0));
                mesh.Beam(new Vector3(side*5.7f,3.95f,5.1f),new Vector3(side*5.7f,5.0f,5.1f),.14f,F.Wood,true);
                mesh.Beam(new Vector3(side*5.7f,4.65f,5.1f),new Vector3(side*4.4f,5.7f,5.1f),.12f,F.Wood);
                Pot(new Vector3(side*5.15f,1.86f,9.65f),.43f,.74f,side+3);
                Lantern(new Vector3(side*4.2f,4.75f,5.1f),.2f);
            }
            mesh.CurvedRoof(new Vector3(0,4.8f,8.3f),15.1f,8.6f,2.15f,F.Coral);
            // The screen frame is real architecture; the live controls are supplied by the lobby view.
            mesh.Box(new Vector3(0,3.9f,11.32f),new Vector3(7.75f,3.9f,.23f),F.Wood,.17f);
            mesh.Box(new Vector3(0,3.9f,11.17f),new Vector3(7.38f,3.57f,.10f),F.Ivory,.18f);
            mesh.Box(new Vector3(0,3.9f,11.10f),new Vector3(7.12f,3.31f,.05f),F.Teal,.13f);
            mesh.Disk(new Vector3(0,1.82f,6.65f),2.3f,.87f,F.Wood,true,40);
            mesh.Disk(new Vector3(0,2.69f,6.65f),2.43f,.16f,F.Stone,false,40);
            mesh.Disk(new Vector3(-.9f,2.84f,6.50f),.3f,.09f,F.Brass,false,24);
            mesh.Disk(new Vector3(-.9f,2.94f,6.50f),.22f,.11f,F.Glow,false,24);
            mesh.Disk(new Vector3(.75f,2.85f,6.5f),.31f,.09f,F.Teal,false,24);
            Lantern(new Vector3(0,5.15f,7),.24f);
        }
        void StyleShop()
        {
            Vector3 c=new(-13,1.3f,7.7f);
            Portal(c,9.2f,5.6f,5.2f);
            mesh.Box(c+new Vector3(0,2.0f,2.8f),new Vector3(8.6f,4.1f,.3f),F.Wood,.18f,true);
            mesh.Box(c+new Vector3(0,3.88f,-2.7f),new Vector3(3.7f,.75f,.27f),F.Wood,.12f);
            // The illuminated oval mirror is centered in the open wardrobe, as in the approved render.
            var ring=new List<Vector3>();for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;ring.Add(c+new Vector3(Mathf.Cos(a)*1.38f,2.1f+Mathf.Sin(a)*1.9f,2.42f));}
            mesh.Tube(ring,.14f,F.Wood,10);mesh.Tube(ring,.065f,F.Glow,10);
            for(int i=0;i<20;i++){float a=i*Mathf.PI/10;mesh.Ellipsoid(c+new Vector3(Mathf.Cos(a)*1.4f,2.1f+Mathf.Sin(a)*1.91f,2.28f),Vector3.one*.06f,F.Glow,rings:5,sides:8);}
            mesh.Disk(new Vector3(-13,1.35f,8.2f),1.32f,.18f,F.Stone,true,40);
            for(int side=-1;side<=1;side+=2)
            {
                for(int shelf=0;shelf<3;shelf++)mesh.Box(c+new Vector3(side*3.12f,1.05f+shelf*1.02f,2.12f),new Vector3(1.75f,.12f,.68f),F.Wood,.04f);
                for(int j=0;j<3;j++)
                {
                    var p=c+new Vector3(side*3.1f+(j-1)*.53f,2.93f,1.94f);
                    mesh.Disk(p,.24f,.05f,F.Metal,false,20);mesh.Disk(p+Vector3.up*.05f,.15f,.28f,(F)((int)F.Red+(j+1)%3),false,20);
                }
                var railA=c+new Vector3(side*3.2f-.7f,2.0f,1.42f);var railB=c+new Vector3(side*3.2f+.7f,2.0f,1.42f);mesh.Beam(railA,railB,.027f,F.Brass);
                for(int j=0;j<4;j++)
                {
                    var p=c+new Vector3(side*3.2f+(j-1.5f)*.34f,1.4f,1.4f);var color=j%2==0?F.MintFabric:F.CoralFabric;
                    mesh.Box(p,new Vector3(.3f,.85f,.12f),color,.06f);mesh.Box(p+new Vector3(-.18f,.24f,0),new Vector3(.16f,.5f,.12f),color,.05f,false,Quaternion.Euler(0,0,-14));mesh.Box(p+new Vector3(.18f,.24f,0),new Vector3(.16f,.5f,.12f),color,.05f,false,Quaternion.Euler(0,0,14));
                }
            }
            Pot(new Vector3(-18.1f,1.29f,3.2f),.56f,.9f,4);Pot(new Vector3(-7.8f,1.29f,3.8f),.45f,.8f,5);
        }
        void Portal(Vector3 center,float width,float height,float depth)
        {
            float half=width*.5f;
            for(int side=-1;side<=1;side+=2)mesh.Box(center+new Vector3(side*(half-.5f),height*.42f,0),new Vector3(1.05f,height*.85f,depth),F.Ivory,.4f,true);
            var arch=new List<Vector3>();
            for(int i=0;i<=40;i++){float a=Mathf.PI-i*Mathf.PI/40;arch.Add(center+new Vector3(Mathf.Cos(a)*(half-.45f),height*.59f+Mathf.Sin(a)*height*.35f,-depth*.5f));}
            mesh.Tube(arch,.53f,F.Ivory,16,true);
            mesh.CurvedRoof(center+new Vector3(0,height*.57f,0),width,depth,height*.35f,F.Ivory);
        }
        void Lounge()
        {
            Vector3 c=new(13.7f,1.3f,6.0f);
            mesh.Box(c+new Vector3(0,.48f,3.0f),new Vector3(10.4f,.96f,.45f),F.Wood,.14f,true);
            for(int pane=0;pane<3;pane++)
            {
                float x=(pane-1)*3.25f;
                mesh.Box(c+new Vector3(x,2.05f,3.0f),new Vector3(2.95f,2.14f,.08f),F.Glass,.025f,true);
                mesh.Box(c+new Vector3(x-1.56f,2.08f,3.0f),new Vector3(.18f,2.4f,.24f),F.Wood,.04f,true);
                mesh.Box(c+new Vector3(x,3.25f,3.0f),new Vector3(3.25f,.15f,.24f),F.Wood,.035f);
                mesh.Beam(c+new Vector3(x,1.02f,2.94f),c+new Vector3(x,3.2f,2.94f),.025f,F.Brass);
            }
            mesh.Box(c+new Vector3(5,1.65f,1),new Vector3(.65f,3.3f,4.3f),F.Ivory,.2f,true);
            mesh.CurvedRoof(c+new Vector3(0,3.5f,.2f),12.2f,7.5f,.68f,F.Coral);
            for(int side=-1;side<=1;side+=2)
            {
                mesh.Beam(c+new Vector3(side*4.8f,0,-2),c+new Vector3(side*4.8f,3.7f,-2),.15f,F.Wood,true);
                Lantern(c+new Vector3(side*3.7f,2.8f,-1.8f),.22f);
            }
            Sofa(c+new Vector3(0,0,1.7f),5.7f,Quaternion.identity);
            Sofa(c+new Vector3(3.9f,0,.6f),2.3f,Quaternion.Euler(0,-90,0));
            Table(c+new Vector3(.2f,0,-.6f),1.35f);
            for(int i=0;i<3;i++)mesh.Box(c+new Vector3(-3.8f,1.1f+i*.55f,2.7f),new Vector3(2,.09f,.55f),F.Wood,.03f);
            Pot(c+new Vector3(-4.6f,0,1.65f),.42f,.7f,10);
            Pot(c+new Vector3(4.5f,0,-2.25f),.5f,.78f,11);
            // The front social island has another curved bank of seating.
            for(int i=0;i<4;i++)
            {
                float angle=Mathf.Lerp(15,105,i/3f)*Mathf.Deg2Rad;var p=new Vector3(-10+Mathf.Cos(angle)*4.0f,1.35f,-8+Mathf.Sin(angle)*4.0f);
                Sofa(p,1.35f,Quaternion.Euler(0,90-angle*Mathf.Rad2Deg,0));
            }
        }
        void Sofa(Vector3 p,float width,Quaternion q)
        {
            void B(Vector3 d,Vector3 s,F color,float bevel=.10f,bool solid=false)=>mesh.Box(p+q*d,s,color,bevel,solid,q);
            B(new Vector3(0,.25f,0),new Vector3(width,.5f,1.25f),F.Wood,.15f,true);
            B(new Vector3(0,.60f,0),new Vector3(width-.24f,.27f,1.0f),F.CreamFabric);
            B(new Vector3(0,1.05f,.48f),new Vector3(width-.12f,.78f,.27f),F.CreamFabric,.11f,true);
            for(int side=-1;side<=1;side+=2)B(new Vector3(side*(width*.5f-.10f),.65f,0),new Vector3(.22f,.58f,1.25f),F.Wood,.08f,true);
            int cushions=Mathf.Max(1,Mathf.RoundToInt(width/1.4f));
            for(int i=0;i<cushions;i++)
            {
                float x=(i-(cushions-1)*.5f)*1.05f;
                mesh.Box(p+q*new Vector3(x,.95f,.25f),new Vector3(.62f,.6f,.20f),i%2==0?F.MintFabric:F.CoralFabric,.12f,false,q*Quaternion.Euler(-12,0,6));
            }
        }
        void Table(Vector3 p,float radius)
        {
            mesh.Disk(p+Vector3.up*.08f,radius*.63f,.45f,F.Wood,true,28);mesh.Disk(p+Vector3.up*.53f,radius,.15f,F.Wood,false,40);
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;var c=p+new Vector3(Mathf.Cos(a)*radius*.5f,.73f,Mathf.Sin(a)*radius*.5f);
                mesh.Disk(c,.1f,.16f,F.Ivory,false,12);
            }
        }
        void UpperTerraces()
        {
            mesh.Box(new Vector3(-12,2.7f,17),new Vector3(13,2.6f,8),F.Ivory,.5f,true);
            mesh.Box(new Vector3(-12,4.05f,17),new Vector3(13,.18f,8),F.Stone,.06f,true);
            mesh.Box(new Vector3(-1,3.4f,18),new Vector3(10,1.1f,6),F.Ivory,.4f,true);
            mesh.Box(new Vector3(-1,4.05f,18),new Vector3(10,.18f,6),F.Stone,.06f,true);
            Stairs(new Vector3(-8.0f,1.3f,9.5f),new Vector3(-8.0f,4.14f,16.5f),2.3f);
            Stairs(new Vector3(8.2f,1.3f,10),new Vector3(8.2f,4.12f,16.8f),2.25f);
            // A broad side ramp gives the upper gathering terrace a route without jumps.
            Ramp(new Vector3(-21.0f,1.28f,2.4f),new Vector3(-21.0f,4.14f,17.4f),2.5f);
            mesh.Box(new Vector3(-19.7f,4.05f,19.1f),new Vector3(5,.18f,4.0f),F.Stone,.06f,true);
            for(int i=0;i<4;i++)
            {
                float x=-17+i*3.2f;mesh.Beam(new Vector3(x,4.15f,17.9f),new Vector3(x,7.2f,17.9f),.15f,F.Wood,true);
                mesh.Beam(new Vector3(x,7.2f,14),new Vector3(x,7.2f,20),.17f,F.Wood);
            }
            mesh.Beam(new Vector3(-17,7.2f,17.9f),new Vector3(-7.4f,7.2f,17.9f),.18f,F.Wood);
            for(int i=0;i<8;i++)mesh.Box(new Vector3(-12.1f,7.24f,14+i*.82f),new Vector3(11,.1f,.25f),F.Wood,.025f);
            Sofa(new Vector3(-14,4.15f,18.4f),4.3f,Quaternion.identity);Table(new Vector3(-14,4.15f,16.6f),1.1f);
            Rail(new Vector3(-18,4.15f,13.1f),new Vector3(-9.5f,4.15f,13.1f));
            Rail(new Vector3(-6.4f,4.15f,14),new Vector3(4.0f,4.15f,14));
            Rail(new Vector3(4.0f,4.15f,14),new Vector3(4.0f,4.15f,21));
            mesh.Box(new Vector3(14,2.6f,15.8f),new Vector3(10,2.5f,6),F.Ivory,.3f,true);
            mesh.Box(new Vector3(14,3.94f,15.8f),new Vector3(10,.18f,6),F.Wood,.04f,true);
            Rail(new Vector3(10.1f,4.04f,13.2f),new Vector3(18.8f,4.04f,13.2f));
            for(int i=0;i<6;i++)
            {
                float x=10.5f+i*1.4f;mesh.Box(new Vector3(x,5.0f,18.3f),new Vector3(1.15f,1.8f,.22f),i%2==0?F.Teal:F.Wood,.10f,true);
                Target(new Vector3(x,5.2f,18.14f),.41f);
            }
            mesh.Box(new Vector3(14,4.8f,16.9f),new Vector3(8.4f,.18f,.8f),F.Wood,.08f,true);
        }
        void Stairs(Vector3 from,Vector3 to,float width)
        {
            int count=Mathf.CeilToInt((to.y-from.y)/.17f);Vector3 direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;float run=Vector3.ProjectOnPlane(to-from,Vector3.up).magnitude;var q=Quaternion.LookRotation(direction);
            for(int i=0;i<count;i++){float top=Mathf.Lerp(from.y,to.y,(i+1f)/count);var p=Vector3.Lerp(from,to,(i+.5f)/count);p.y=(top+from.y-.15f)*.5f;mesh.Box(p,new Vector3(width,top-from.y+.15f,run/count+.02f),F.Stone,.026f,true,q);}
            for(int side=-1;side<=1;side+=2){var offset=q*Vector3.right*(side*(width*.5f+.14f));Rail(from+offset,to+offset);}
        }
        void Ramp(Vector3 from,Vector3 to,float width)
        {
            var slope=(to-from).normalized;mesh.Box((from+to)*.5f-Vector3.up*.11f,new Vector3(width,.22f,Vector3.Distance(from,to)),F.Stone,.025f,true,Quaternion.LookRotation(slope));
            Vector3 side=Vector3.Cross(Vector3.up,slope).normalized*(width*.5f+.12f);Rail(from-side,to-side);Rail(from+side,to+side);
        }
        void Rail(Vector3 from,Vector3 to)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(from,to)/1.75f);
            for(int i=0;i<=count;i++)Post(Vector3.Lerp(from,to,i/(float)count));
            mesh.Beam(from+Vector3.up*.58f,to+Vector3.up*.58f,.035f,F.Wood);
            mesh.Beam(from+Vector3.up*1.03f,to+Vector3.up*1.03f,.065f,F.Wood,true);
        }
        void Target(Vector3 p,float radius)
        {
            for(int i=0;i<4;i++)mesh.Ellipsoid(p+Vector3.back*i*.008f,new Vector3(radius*(1-i*.23f),radius*(1-i*.23f),.025f),i%2==0?F.Ivory:F.Coral,rings:10,sides:20);
        }
        void Landmark()
        {
            for(int i=0;i<9;i++)mesh.Box(new Vector3(3.1f,4.5f+i*.78f,17.7f),new Vector3(2.5f,.76f,1.25f),(F)((int)F.Red+i%3),.11f,true,Quaternion.Euler(0,i%2*90+i%3*3,0));
        }
    }
}

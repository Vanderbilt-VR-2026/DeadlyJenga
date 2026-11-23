using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class GingerbreadKingdom
    {
        void Landscape()
        {
            // Two open cuts flank the east bridge pedestal. Pavement elsewhere remains continuous.
            void Ground(float left,float right,float south,float north)
            {
                Part("Packed snow",V((left+right)*.5f,-.52f,(south+north)*.5f),V(right-left,1.04f,north-south),Snow,false);
            }
            Ground(-85,85,-85,-13.04f);Ground(-85,85,-8.96f,85);
            Ground(-85,12.90f,-13.04f,-8.96f);Ground(17.10f,85,-13.04f,-8.96f);
            Ground(14.31f,15.69f,-11.86f,-10.14f);
            var kill=Group("Below the snow cut",town,V(0,-8.5f,0));
            kill.gameObject.AddComponent<BoxCollider>().size=V(180,.6f,180);
            kill.gameObject.AddComponent<UnderneathTreeLethalFloor>();
            var m=new GingerbreadMesh();
            Street(m,new[]{V(0,0,-24),V(-5,0,-19),V(-5,0,-10),V(-4,0,-3),V(-2.7f,0,3),V(-4,0,9),V(1,0,10)});
            Street(m,new[]{V(0,0,-24),V(5,0,-20),V(5,0,-13),V(5,0,-5),V(4,0,1),V(5,0,7),V(3,0,10)});
            Street(m,new[]{V(-5,0,-19),V(-10,0,-19),V(-15,0,-12),V(-16,0,-4),V(-17,0,6),V(-17,0,10),V(-10,0,12),V(-4,0,9)});
            Street(m,new[]{V(5,0,-20),V(13,0,-20),V(17.8f,0,-16),V(18.4f,0,-8),V(18,0,1),V(18,0,9),V(14,0,13),V(10,0,11),V(4,0,8)});
            Street(m,new[]{V(-4,0,-1),V(0,0,-1),V(4,0,1)});
            Street(m,new[]{V(-3,0,5),V(0,0,6),V(3,0,9.2f)});
            foreach(var site in new[]{V(-5,0,-18),V(5,0,-17),V(-7,0,-8),V(6,0,-7),V(-4.7f,0,4.4f),V(4.7f,0,4.4f),V(-.5f,0,9.2f),V(6.5f,0,9.2f),V(18,0,-13),V(-17,0,8)})
            {
                m.Ring(Cocoa,site,.18f,.12f,.22f);
                m.Ring(Cocoa,site+Vector3.up*.22f,.085f,.065f,1.53f);
                m.Ring(Gold,site+Vector3.up*1.75f,.16f,.24f,.31f,6);
                m.Ring(Cocoa,site+Vector3.up*2.06f,.28f,.03f,.19f,6);
            }
            var random=new System.Random(614);
            float Noise()=> (float)random.NextDouble();
            for(int i=0;i<230;i++)
            {
                float a=Noise()*Mathf.PI*2, distance=32+Noise()*44;
                float x=Mathf.Cos(a)*distance,z=Mathf.Sin(a)*distance;
                if(z<-20 && Mathf.Abs(x)<25)continue;
                float hill=Mathf.Max(0,(distance-31)*.22f)*(1+.35f*Mathf.Sin(a*7));
                float h=2.5f+Noise()*5;
                m.Ring(Snow,V(x,-.15f,z),5+Noise()*4,.85f,hill+.25f,12);
                m.Ring(Cocoa,V(x,hill,z),.25f,.18f,h*.36f,7);
                for(int tier=0;tier<4;tier++)
                {
                    float rad=h*(.30f-tier*.048f),y=hill+h*(.13f+tier*.165f);
                    m.Ring(Green,V(x,y,z),rad,.03f,h*.42f,9);
                    m.Ring(Snow,V(x,y+h*.065f,z),rad*.91f,.012f,h*.38f,9);
                }
            }
            Mount(Group("Cobble streets and snowy pine horizon",town,Vector3.zero),m);
        }
        static void Street(GingerbreadMesh m,Vector3[] p)
        {
            for(int i=0;i<p.Length-1;i++)
            {
                Vector3 a=p[Mathf.Max(0,i-1)],b=p[i],c=p[i+1],d=p[Mathf.Min(i+2,p.Length-1)];
                int n=Mathf.CeilToInt(Vector3.Distance(b,c)/.55f);
                Vector3 last=b;
                for(int j=1;j<=n;j++)
                {
                    float t=(float)j/n;
                    Vector3 next=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
                    Vector3 mid=(last+next)*.5f,delta=next-last;
                    var rot=Quaternion.LookRotation(delta);
                    for(int k=-2;k<=2;k++)
                    {
                        Vector3 offset=rot*V(k*.60f,.018f,0);
                        m.Box(Stone,mid+offset,V(.588f,.032f,delta.magnitude+.015f),rot);
                    }
                    if(j%3==0)for(int s=-1;s<=1;s+=2)
                        m.Box(Snow,mid+rot*V(s*1.55f,.065f,0),V(.16f,.13f,.55f),rot);
                    last=next;
                }
            }
        }
    }
}

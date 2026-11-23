using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class GingerbreadKingdom
    {
        static void Arch(GingerbreadMesh m,Vector3 basePoint,float radius,float jamb,int trim)
        {
            Vector3 crown=basePoint+Vector3.up*jamb;
            for(int side=-1;side<=1;side+=2)
                m.Box(trim,basePoint+V(side*radius,jamb*.5f,0),V(.105f,jamb,.14f));
            Vector3 previous=crown+Vector3.right*radius;
            for(int i=1;i<=14;i++)
            {
                float a=i*Mathf.PI/14;Vector3 next=crown+V(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
                m.Line(trim,previous,next,.115f);previous=next;
            }
        }
        static void Window(GingerbreadMesh m,Vector3 foot,float width,float height)
        {
            float r=width*.5f,jamb=height-r;
            m.Box(Glow,foot+V(0,jamb*.5f,0),V(width,jamb,.07f));
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;
                m.Face(Glow,foot+V(0,jamb,-.045f),foot+V(r*Mathf.Cos(b),jamb+r*Mathf.Sin(b),-.045f),foot+V(r*Mathf.Cos(a),jamb+r*Mathf.Sin(a),-.045f));
            }
            Arch(m,foot+Vector3.back*.065f,r,jamb,Ice);
            m.Box(Cocoa,foot+V(0,height*.50f,-.09f),V(.055f,height,.05f));
            m.Box(Cocoa,foot+V(0,jamb*.76f,-.09f),V(width,.055f,.05f));
            m.Box(Ice,foot+V(0,0,-.10f),V(width+.22f,.12f,.22f));
        }
        static void Rail(GingerbreadMesh m,float width,float z,float y)
        {
            m.Box(Ice,V(0,y+.54f,z),V(width,.1f,.13f));
            int posts=Mathf.CeilToInt(width/.42f);
            for(int i=0;i<=posts;i++)
                m.Box(Caramel,V(Mathf.Lerp(-width*.5f,width*.5f,(float)i/posts),y+.25f,z),V(.09f,.5f,.09f));
        }
        void House(float x,float z,float yaw,float w,float d,int floors,int style)
        {
            plot=Group("Bakeshop "+style,town,V(x,0,z),yaw);
            float h=1.9f, slab=.24f, wall=.38f, doorway=1.32f;
            int baseColor=style%3==0?Caramel:Biscuit;
            for(int floor=0;floor<floors;floor++)
            {
                float y=floor*(h+slab);
                for(int side=-1;side<=1;side+=2)
                {
                    Part("Baked flank",V(side*(w-wall)*.5f,y+h*.5f,0),V(wall,h,d),baseColor,true,m=>{
                        for(float along=-d*.5f+.85f;along<d*.5f;along+=1.45f)
                        {
                            m.Box(Glow,V(side*.21f,.06f,along),V(.09f,.86f,.69f));
                            m.Box(Cocoa,V(side*.27f,.06f,along),V(.07f,.91f,.06f));
                            m.Box(Ice,V(side*.24f,-.4f,along),V(.19f,.12f,.86f));
                        }
                    });
                    float wing=(w-wall*2-doorway)*.5f;
                    Part("Shopfront pier",V(side*(doorway+wing)*.5f,y+h*.5f,-(d-wall)*.5f),V(wing,h,wall),baseColor,true,m=>
                        Window(m,V(0,-.56f,-.22f),Mathf.Min(.68f,wing*.77f),1.08f));
                }
                // Split rear piers leave a passage through each deep lot.
                float rear=(w-2*wall-doorway)*.5f;
                for(int side=-1;side<=1;side+=2)
                    Part("Rear jamb",V(side*(doorway+rear)*.5f,y+h*.5f,(d-wall)*.5f),V(rear,h,wall),baseColor);
                bool attic=floor==floors-1;
                Part(attic?"Pitched biscuit roof":"Balcony floor",V(0,y+h+slab*.5f,0),V(w+.3f,slab,d+(attic ? .55f : 1)),Cocoa,true,m=>{
                    if(attic)Roof(m,w+.34f,d+.6f,style);
                    else Rail(m,w+.15f,-d*.5f-.42f,.12f);
                    Arch(m,V(0,-h+.01f,-d*.5f-.08f),doorway*.49f,h-doorway*.49f,Ice);
                });
            }
        }
        static void Roof(GingerbreadMesh m,float w,float d,int style)
        {
            float half=w*.5f, rise=w*(.32f+.085f*(style%4)),eave=.15f;
            float front=-d*.5f;
            m.Face(Biscuit,V(-half,eave,front),V(0,rise+eave,front),V(half,eave,front));
            m.Face(Biscuit,V(half,eave,-front),V(0,rise+eave,-front),V(-half,eave,-front));
            int topping=style==3?Caramel:style==5?Pink:style==2?Violet:Cocoa;
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 slope=V(side*half,-rise,0);
                var rotation=Quaternion.Euler(0,0,-side*Mathf.Atan2(rise,half)*Mathf.Rad2Deg);
                m.Box(topping,V(side*half*.5f,eave+rise*.5f,0),V(slope.magnitude,.22f,d),rotation);
                for(int row=0;row<=4;row++)
                {
                    float t=row*.23f;Vector3 pos=V(side*half*t,eave+rise*(1-t)+.12f,0);
                    m.Box(style==3?Biscuit:Ice,pos,V(style==3?.14f:.105f,.09f,d+.08f),rotation);
                    for(int j=0;j<7;j++)
                    {
                        float az=(j-3)*d/7+((row%2)-.5f)*.12f;
                        if(style==0)continue;
                        int tint=style==6?(row+j)%3==0?Red:Green:style==4?Red:style==1?Ice:topping;
                        if(style==3) m.Box(Biscuit,pos+V(0,.04f,az),V(slope.magnitude*.44f,.1f,.12f),rotation);
                        else if(style==1 || style==2) m.Line(Ice,pos+V(0,.05f,az-.23f),pos+V(side*.22f,-.12f,az),.08f);
                        else m.Ring(tint,pos+V(0,0,az),.16f,.12f,.14f,8);
                    }
                }
                for(int end=-1;end<=1;end+=2)
                    m.Line(Ice,V(0,rise+eave+.18f,end*d*.5f),V(side*half,eave+.16f,end*d*.5f),.18f);
            }
            Window(m,V(0,.22f,front-.05f),.70f,Mathf.Min(1.12f,rise-.1f));
            m.Box(Caramel,V(.45f,rise+.22f,d*.24f),V(.57f,1.05f,.63f));
            m.Box(Ice,V(.45f,rise+.77f,d*.24f),V(.73f,.17f,.78f));
            if(style==7)
            {
                Vector3 cherry=V(0,rise+.24f,-d*.30f);
                m.Ring(Red,cherry,.22f,.56f,.32f);m.Ring(Red,cherry+Vector3.up*.32f,.56f,.47f,.35f);
                m.Ring(Red,cherry+Vector3.up*.67f,.47f,.12f,.22f);
                m.Line(Cocoa,cherry+V(0,.86f,0),cherry+V(.23f,1.24f,.09f),.08f);
            }
        }
    }
}

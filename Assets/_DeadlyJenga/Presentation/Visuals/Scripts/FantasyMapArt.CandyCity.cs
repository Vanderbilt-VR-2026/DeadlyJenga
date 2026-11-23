using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy Glass City: art-only dressing for the glass blocks (no colliders, no gameplay).
    // Buildings get candy frames and lit windows, top blocks get fun roofs, flat pieces become streets,
    // and the city gets a plaza, a gondola, balloons and a skyline backdrop.
    public sealed partial class FantasyMapArt
    {
        // District look: 0 Downtown, 1 Grape Heights, 2 Sugar Market, 3 Cloud Harbor.
        static readonly Color[] FrameTints={new(1,.36f,.66f),new(1,.97f,.9f),new(.45f,1,.8f),new(1,.84f,.3f)};
        static readonly Color[] RoofTints={new(.55f,1,.35f),new(.7f,.35f,1),new(1,.3f,.45f),new(.35f,.85f,1)};
        static readonly Color[] WindowTints={new(1,.82f,.48f),new(1,.7f,.82f),new(.85f,.92f,1)};
        static readonly Color Licorice=new(.82f,.07f,.12f),LicoriceDark=new(.5f,.02f,.06f),Sugar=new(1,.98f,.96f);

        bool City=>candyCity&&cityCandyMaterial&&cityGlowMaterial;
        float groundY;
        bool cityBuilt;
        readonly List<Vector3> roofPoints=new();
        Transform balloonRing,balloonRingOuter;
        readonly List<Transform> cabins=new();
        Vector3 cableA,cableB;

        void BeginCityPass()
        {
            roofPoints.Clear();groundY=float.MaxValue;elevators.Clear();fountainBuilt=false;landmarkCount=0;
            foreach(var body in Services.World.Blocks)
                if(body)foreach(var shape in body.GetComponentsInChildren<Collider>())groundY=Mathf.Min(groundY,shape.bounds.min.y);
            if(groundY==float.MaxValue)groundY=transform.position.y;
        }

        int District(Vector3 world)
        {
            var d=world-Services.World.Center;d.y=0;
            float r=d.magnitude/Mathf.Max(1,Mathf.Max(halfWidth,halfDepth));
            if(r<.3f)return 0;
            float angle=(Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg+360)%360;
            return angle<120?1:angle<240?2:3;
        }

        static float Hash(Vector3 p)
        {
            var q=new Vector3(Mathf.Round(p.x),Mathf.Round(p.y),Mathf.Round(p.z));
            float h=Mathf.Sin(Vector3.Dot(q,new Vector3(12.9898f,78.233f,37.719f)))*43758.5453f;
            return h-Mathf.Floor(h);
        }

        Material CityGlass(Rigidbody body)
        {
            var p=body.worldCenterOfMass;
            var glass=cityGlassMaterials!=null&&cityGlassMaterials.Length>=4?cityGlassMaterials:blockMaterials;
            switch(District(p))
            {
                case 0:return glass[Hash(p)<.5f?0:1];
                case 1:return glass[2];
                case 2:return glass[Hash(p)<.6f?3:1];
                default:return glass[Hash(p)<.6f?0:2];
            }
        }

        void DecorateCity(Rigidbody body,Transform visual)
        {
            if(solidCandy){SolidDress(body,visual);return;}
            // Material 0 = candy (vertex tinted), 1 = glow.
            var mesh=new ArtMesh(2);bool any=false;
            foreach(var shape in body.GetComponentsInChildren<BoxCollider>())
            {
                var size=shape.size;if(size.x<.4f||size.y<.2f||size.z<.4f)continue;
                var world=shape.transform.TransformPoint(shape.center);
                int district=District(world);
                Vector3 P(Vector3 p)=>visual.InverseTransformPoint(shape.transform.TransformPoint(shape.center+p));
                var q=Quaternion.Inverse(visual.rotation)*shape.transform.rotation;
                bool flat=size.y<1.2f;
                float shortSide=Mathf.Min(size.x,size.z),longSide=Mathf.Max(size.x,size.z);
                if(flat&&longSide>=4&&longSide>=shortSide*2.2f&&shortSide<=6)Street(mesh,size,P,q);
                else if(flat)
                {
                    Slab(mesh,size,P,q,district,Hash(world));
                    var top=shape.transform.TransformPoint(shape.center+Vector3.up*size.y*.5f);
                    if(shortSide>=3&&top.y>groundY+4&&!Covered(top,body))
                    {
                        Roof(mesh,size,P,q,district,Hash(world+Vector3.one*7));
                        roofPoints.Add(top);
                    }
                }
                else
                {
                    Building(mesh,size,P,q,district,Hash(world));
                    var top=shape.transform.TransformPoint(shape.center+Vector3.up*size.y*.5f);
                    if(Mathf.Min(size.x,size.z)>=1.8f&&top.y>groundY+2&&!Covered(top,body))
                    {
                        Roof(mesh,size,P,q,district,Hash(world+Vector3.one*7));
                        roofPoints.Add(top);
                    }
                    if(district==2&&size.y>1.6f&&shape.bounds.min.y<groundY+1.5f)Awnings(mesh,size,P,q);
                }
                any=true;
            }
            if(any)Model("Fantasy decorations",mesh,visual,new[]{cityCandyMaterial,cityGlowMaterial},true);
        }

        static bool Covered(Vector3 top,Rigidbody body)
        {
            foreach(var hit in Physics.RaycastAll(top+Vector3.up*.05f,Vector3.up,3,1<<Layers.Block,QueryTriggerInteraction.Ignore))
                if(hit.collider.attachedRigidbody!=body)return true;
            return false;
        }

        void Building(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q,int district,float seed)
        {
            var h=size*.5f;var frame=FrameTints[district];
            float t=Mathf.Clamp(Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.07f,.1f,.24f);
            // Twelve candy edges.
            for(int axis=0;axis<3;axis++)
            {
                int u=(axis+1)%3,v=(axis+2)%3;
                for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)
                {
                    var c=Vector3.zero;c[u]=h[u]*a;c[v]=h[v]*b;
                    var s=Vector3.one*t;s[axis]=size[axis]+t;
                    mesh.Box(P(c),s,q,0,frame);
                }
            }
            // Each wall: mullions, a floor band and glowing windows behind the glass.
            for(int face=0;face<4;face++)
            {
                bool alongX=face<2;float sign=face%2==0?-1:1;
                float width=alongX?size.x:size.z,depth=alongX?h.z:h.x;
                var normal=alongX?new Vector3(0,0,sign):new Vector3(sign,0,0);
                var across=alongX?Vector3.right:Vector3.forward;
                int bars=Mathf.FloorToInt(width/2.6f);
                for(int k=1;k<=bars;k++)
                {
                    float x=-width*.5f+k*width/(bars+1);
                    var s=alongX?new Vector3(t*.6f,size.y,t*.6f):new Vector3(t*.6f,size.y,t*.6f);
                    mesh.Box(P(across*x+normal*depth),s,q,0,frame);
                }
                if(size.y>3.5f)
                {
                    var s=alongX?new Vector3(size.x,t*.6f,t*.6f):new Vector3(t*.6f,t*.6f,size.z);
                    mesh.Box(P(normal*depth),s,q,0,frame);
                }
                if(size.y<1.6f||width<1.6f)continue;
                int panes=bars+1;float pane=width/panes;
                for(int k=0;k<panes;k++)
                {
                    if(Hash(new Vector3(seed*97,face,k))<.18f)continue; // a few dark rooms read more natural
                    float x=-width*.5f+(k+.5f)*pane;
                    var s=alongX?new Vector3(pane*.62f,size.y*.45f,.06f):new Vector3(.06f,size.y*.45f,pane*.62f);
                    var tint=WindowTints[(int)(Hash(new Vector3(seed*31,face,k))*WindowTints.Length)%WindowTints.Length];
                    mesh.Box(P(across*x+normal*(depth-.18f)+Vector3.up*size.y*.04f),s,q,1,tint);
                }
            }
        }

        // Floor plates: candy trim around the edge and a glowing strip underneath (lit floors).
        void Slab(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q,int district,float seed)
        {
            var h=size*.5f;float t=.24f;var frame=FrameTints[district];
            var glow=WindowTints[(int)(seed*WindowTints.Length)%WindowTints.Length];
            for(int edge=0;edge<4;edge++)
            {
                bool alongX=edge<2;float sign=edge%2==0?-1:1;
                var c=alongX?new Vector3(0,0,sign*h.z):new Vector3(sign*h.x,0,0);
                var s=alongX?new Vector3(size.x+t,size.y+.04f,t):new Vector3(t,size.y+.04f,size.z+t);
                mesh.Box(P(c),s,q,0,frame);
                var under=alongX?new Vector3(size.x-.6f,.1f,.16f):new Vector3(.16f,.1f,size.z-.6f);
                mesh.Box(P(c*.97f+Vector3.down*(h.y+.07f)),under,q,1,glow);
            }
        }

        void Roof(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q,int district,float seed)
        {
            float r=Mathf.Min(Mathf.Min(size.x,size.z)*.5f,5);var top=Vector3.up*size.y*.5f;var tint=RoofTints[district];
            int kind=(int)(seed*4)%4;
            if(kind==0) // gumdrop dome with sugar dots
            {
                mesh.Sphere(P(top),new Vector3(r*.78f,r*.72f,r*.78f),0,tint,8,14);
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7;var d=new Vector3(Mathf.Cos(a)*r*.55f,r*.42f,Mathf.Sin(a)*r*.55f);
                    mesh.Sphere(P(top+d),Vector3.one*r*.09f,0,Sugar,3,6);
                }
            }
            else if(kind==1) // lollipop spire
            {
                float stick=2.2f+r;
                mesh.Box(P(top+Vector3.up*stick*.5f),new Vector3(.2f,stick,.2f),q,0,Sugar);
                var head=P(top+Vector3.up*(stick+r*.55f));
                mesh.Sphere(head,new Vector3(r*.7f,r*.7f,.28f),0,tint,8,14);
                mesh.Sphere(head+q*Vector3.forward*.06f,new Vector3(r*.45f,r*.45f,.28f),0,Sugar,8,14);
                mesh.Sphere(head+q*Vector3.forward*.1f,new Vector3(r*.22f,r*.22f,.28f),0,tint,8,14);
                mesh.Sphere(head-q*Vector3.forward*.06f,new Vector3(r*.45f,r*.45f,.28f),0,Sugar,8,14);
                mesh.Sphere(head-q*Vector3.forward*.1f,new Vector3(r*.22f,r*.22f,.28f),0,tint,8,14);
            }
            else if(kind==2) // wafer-cone tower with a cherry
            {
                float height=r*2.4f+1;
                mesh.Cone(P(top),q,r*.8f,height,0,new Color(.95f,.7f,.4f),12);
                mesh.Sphere(P(top+Vector3.up*(height+.25f)),Vector3.one*Mathf.Max(.3f,r*.16f),0,new Color(.95f,.08f,.15f),6,10);
            }
            else // rooftop soda pool with a straw
            {
                float wall=.18f,y=size.y*.5f+.22f;
                mesh.Box(P(new Vector3(0,y,size.z*.5f-wall)),new Vector3(size.x,.44f,wall*2),q,0,Sugar);
                mesh.Box(P(new Vector3(0,y,-size.z*.5f+wall)),new Vector3(size.x,.44f,wall*2),q,0,Sugar);
                mesh.Box(P(new Vector3(size.x*.5f-wall,y,0)),new Vector3(wall*2,.44f,size.z),q,0,Sugar);
                mesh.Box(P(new Vector3(-size.x*.5f+wall,y,0)),new Vector3(wall*2,.44f,size.z),q,0,Sugar);
                mesh.Box(P(new Vector3(0,y-.02f,0)),new Vector3(size.x-wall*3,.3f,size.z-wall*3),q,1,new Color(.45f,.92f,1));
                for(int i=0;i<5;i++)mesh.Sphere(P(new Vector3((Hash(new Vector3(seed,i,1))-.5f)*size.x*.6f,y+.2f,(Hash(new Vector3(seed,i,2))-.5f)*size.z*.6f)),Vector3.one*.14f,0,Sugar,3,6);
                var a=P(new Vector3(size.x*.2f,y,0));var b=P(new Vector3(size.x*.32f,y+2.6f,size.z*.12f));
                mesh.Box((a+b)*.5f,new Vector3(.18f,.18f,(b-a).magnitude),Quaternion.LookRotation(b-a),0,new Color(1,.25f,.4f));
            }
        }

        void Street(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q)
        {
            bool alongX=size.x>=size.z;float length=alongX?size.x:size.z,width=alongX?size.z:size.x;
            var along=alongX?Vector3.right:Vector3.forward;var side=alongX?Vector3.forward:Vector3.right;
            float top=size.y*.5f;
            // Twisted licorice rope along both edges.
            int links=Mathf.Clamp(Mathf.RoundToInt(length/.7f),3,60);
            for(int s=-1;s<=1;s+=2)for(int i=0;i<links;i++)
            {
                float x=-length*.5f+(i+.5f)*length/links;
                var rot=q*Quaternion.LookRotation(along)*Quaternion.Euler(0,0,i*45);
                mesh.Box(P(along*x+side*(width*.5f+.08f)+Vector3.up*(top+.02f)),new Vector3(.24f,.24f,length/links*1.15f),rot,0,i%2==0?Licorice:LicoriceDark);
            }
            if(length<6)return;
            // Striped crosswalks at both ends.
            for(int end=-1;end<=1;end+=2)for(int i=0;i<4;i++)
            {
                float x=end*(length*.5f-.7f-i*.65f);
                var s=alongX?new Vector3(.35f,.04f,width*.75f):new Vector3(width*.75f,.04f,.35f);
                mesh.Box(P(along*x+Vector3.up*(top+.02f)),s,q,0,Sugar);
            }
            if(length<8)return;
            // Rock-candy street lamps on opposite corners.
            for(int end=-1;end<=1;end+=2)
            {
                var foot=along*end*(length*.5f-.4f)+side*end*(width*.5f+.35f)+Vector3.up*top;
                mesh.Box(P(foot+Vector3.up*1.3f),new Vector3(.13f,2.6f,.13f),q,0,new Color(.95f,.42f,.72f));
                mesh.Sphere(P(foot+Vector3.up*2.95f),new Vector3(.3f,.48f,.3f),1,new Color(1,.9f,.62f),2,4);
            }
        }

        void Awnings(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q)
        {
            var h=size*.5f;float y=-h.y+Mathf.Min(2.3f,size.y*.6f);
            for(int face=0;face<2;face++)
            {
                bool alongX=size.x>=size.z;float sign=face==0?-1:1;
                float width=alongX?size.x:size.z;var normal=alongX?new Vector3(0,0,sign):new Vector3(sign,0,0);
                var across=alongX?Vector3.right:Vector3.forward;float depth=alongX?h.z:h.x;
                int stripes=Mathf.Clamp(Mathf.RoundToInt(width/.6f),3,30);
                var rot=q*Quaternion.LookRotation(normal)*Quaternion.Euler(22,0,0);
                for(int i=0;i<stripes;i++)
                {
                    float x=-width*.45f+(i+.5f)*width*.9f/stripes;
                    mesh.Box(P(across*x+normal*(depth+.55f)+Vector3.up*(y-.2f)),new Vector3(width*.9f/stripes*1.02f,.06f,1.2f),rot,0,i%2==0?new Color(1,.35f,.6f):Sugar);
                }
            }
        }

        void FinishCityPass()
        {
            if(cityBuilt)return;cityBuilt=true;
            if(!solidCandy){BuildPlaza();BuildGondola();}if(cityStreets)BuildCityStreets();
            if(solidCandy){BuildSkyRing();BuildSkyCities();if(cityStreets)BuildStreetLife();}
        }

        void BuildPlaza()
        {
            var center=Services.World.Center;Vector3 spot=center;bool found=false;
            for(int ring=0;ring<16&&!found;ring++)
            {
                int steps=Mathf.Max(1,ring*6);
                for(int i=0;i<steps&&!found;i++)
                {
                    float a=i*Mathf.PI*2/steps;var p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*ring*6;
                    if(!Physics.CheckBox(new Vector3(p.x,groundY+3.5f,p.z),new Vector3(5,3,5),Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore)){spot=p;found=true;}
                }
            }
            if(!found)return;
            float y=groundY;
            if(Physics.Raycast(new Vector3(spot.x,groundY+40,spot.z),Vector3.down,out var hit,80,1<<Layers.Surface,QueryTriggerInteraction.Ignore))y=hit.point.y;
            var place=transform.InverseTransformPoint(new Vector3(spot.x,y,spot.z));var o=Vector3.zero;var up=Quaternion.identity;
            var m=new ArtMesh(2);
            m.Cylinder(o,up,6,.08f,0,new Color(1,.86f,.92f),28);
            m.Cylinder(o,up,3.2f,.55f,0,new Color(1,.4f,.7f),24);
            m.Cylinder(o+Vector3.up*.05f,up,2.9f,.55f,1,new Color(.5f,.85f,1),24);
            m.Cylinder(o,up,.6f,2.2f,0,Sugar,12);
            m.Cylinder(o+Vector3.up*2.2f,up,1.7f,.35f,0,new Color(1,.4f,.7f),20);
            m.Cylinder(o+Vector3.up*2.25f,up,1.5f,.35f,1,new Color(.5f,.85f,1),20);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;var rim=o+new Vector3(Mathf.Cos(a)*1.6f,2.5f,Mathf.Sin(a)*1.6f);
                var pool=o+new Vector3(Mathf.Cos(a)*2.6f,.6f,Mathf.Sin(a)*2.6f);
                m.Box((rim+pool)*.5f,new Vector3(.18f,.05f,(pool-rim).magnitude),Quaternion.LookRotation(pool-rim),1,new Color(.6f,.9f,1));
            }
            // Gumball-machine statue.
            m.Box(o+Vector3.up*3.2f,new Vector3(1.3f,1.1f,1.3f),up,0,new Color(.95f,.12f,.2f));
            var globe=o+Vector3.up*4.9f;m.Sphere(globe,Vector3.one*1.25f,0,new Color(.8f,.95f,1),10,16);
            for(int i=0;i<18;i++)
            {
                float a=i*2.399963f,b=Mathf.Acos(1-2*(i+.5f)/18);
                var d=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));
                m.Sphere(globe+d*1.18f,Vector3.one*.28f,0,RoofTints[i%4],4,8);
            }
            m.Sphere(globe+Vector3.up*1.3f,new Vector3(.5f,.25f,.5f),0,new Color(.95f,.12f,.2f),4,8);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI/2+Mathf.PI/4;var p=o+new Vector3(Mathf.Cos(a)*4.6f,.45f,Mathf.Sin(a)*4.6f);
                m.Box(p,new Vector3(1.8f,.18f,.6f),Quaternion.Euler(0,-a*Mathf.Rad2Deg+90,0),0,new Color(.45f,1,.8f));
            }
            var plaza=Model("Visual · candy city plaza",m,transform,new[]{cityCandyMaterial,cityGlowMaterial});
            plaza.transform.localPosition=place;plaza.transform.localScale=Vector3.one*1.6f;
        }

        void BuildGondola()
        {
            if(roofPoints.Count<2)return;
            roofPoints.Sort((a,b)=>b.y.CompareTo(a.y));
            Vector3 a=roofPoints[0],b=a;float best=0;
            foreach(var p in roofPoints)
            {
                float d=new Vector2(p.x-a.x,p.z-a.z).magnitude;
                if(d>45&&p.y>best){best=p.y;b=p;}
            }
            if(best<=0)return;
            float cable=Mathf.Max(a.y,b.y)+8;
            cableA=transform.InverseTransformPoint(new Vector3(a.x,cable,a.z));cableB=transform.InverseTransformPoint(new Vector3(b.x,cable,b.z));
            var m=new ArtMesh(1);var pink=new Color(1,.4f,.7f);
            foreach(var (foot,head) in new[]{(transform.InverseTransformPoint(a),cableA),(transform.InverseTransformPoint(b),cableB)})
            {
                m.Box((foot+head)*.5f+Vector3.up*.3f,new Vector3(.45f,(head-foot).magnitude+.6f,.45f),Quaternion.identity,0,Sugar);
                m.Box(head+Vector3.up*.4f,new Vector3(1.6f,.4f,1.6f),Quaternion.identity,0,pink);
            }
            m.Box((cableA+cableB)*.5f,new Vector3(.25f,.25f,(cableB-cableA).magnitude),Quaternion.LookRotation(cableB-cableA),0,new Color(.35f,.18f,.3f));
            Model("Visual · candy gondola line",m,transform,new[]{cityCandyMaterial});
            for(int i=0;i<4;i++)
            {
                var c=new ArtMesh(2);var tint=RoofTints[i%4];
                c.Box(Vector3.down*.9f,new Vector3(.1f,1.8f,.1f),Quaternion.identity,0,Sugar);
                c.Box(Vector3.down*2.6f,new Vector3(2,1.6f,1.5f),Quaternion.identity,0,tint);
                c.Sphere(Vector3.down*1.8f,new Vector3(1.05f,.35f,.8f),0,Sugar,4,10);
                c.Box(Vector3.down*2.5f,new Vector3(2.04f,.6f,1.2f),Quaternion.identity,1,WindowTints[0]);
                var go=Model("Visual · gondola cabin",c,transform,new[]{cityCandyMaterial,cityGlowMaterial});
                go.transform.localRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(cableB-cableA,Vector3.up));go.transform.localScale=Vector3.one*1.8f;
                cabins.Add(go.transform);
            }
        }

        void BuildCityBackdrop()
        {
            float R=Mathf.Max(halfWidth,halfDepth);
            // Hot-air balloons on two slowly turning rings (Candy Sky Ring City has its own flying fleet).
            if(solidCandy)BuildBalloonFleet();
            else for(int ring=0;ring<2;ring++)
            {
                var m=new ArtMesh(1);int count=ring==0?5:6;
                for(int i=0;i<count;i++)
                {
                    float a=i*Mathf.PI*2/count+ring*.4f,r=R*(ring==0?.55f:1.05f);
                    var p=new Vector3(Mathf.Cos(a)*r,38+((i*7+ring*3)%5)*9,Mathf.Sin(a)*r);
                    var tint=RoofTints[(i+ring)%4];
                    m.Sphere(p,new Vector3(5,6.2f,5),0,tint,10,16);
                    m.Sphere(p+Vector3.up*2.4f,new Vector3(3.6f,3.4f,3.6f),0,Sugar,6,12);
                    m.Box(p+Vector3.down*8.4f,new Vector3(1.8f,1.3f,1.8f),Quaternion.identity,0,new Color(.85f,.55f,.3f));
                    for(int k=0;k<4;k++)
                    {
                        var s=p+new Vector3((k%2*2-1)*2.6f,-4.5f,(k/2*2-1)*2.6f);var e=p+new Vector3((k%2*2-1)*.8f,-7.8f,(k/2*2-1)*.8f);
                        m.Box((s+e)*.5f,new Vector3(.08f,.08f,(e-s).magnitude),Quaternion.LookRotation(e-s),0,new Color(.4f,.25f,.2f));
                    }
                }
                var go=Model(ring==0?"Visual · city balloons":"Visual · city balloons outer",m,transform,new[]{cityCandyMaterial});
                if(ring==0)balloonRing=go.transform;else balloonRingOuter=go.transform;
            }
            // Distant floating islands with mini skylines (Candy Sky Ring City has its own sky cities and rainbow bridge).
            if(!solidCandy)
            {
            var islands=new ArtMesh(2);
            for(int i=0;i<7;i++)
            {
                float a=i*2.399963f+.6f,r=R+260+(i%3)*90;var c=new Vector3(Mathf.Cos(a)*r,-5+(i%4)*22,Mathf.Sin(a)*r);
                for(int n=0;n<6;n++)islands.Sphere(c+new Vector3((n-2.5f)*9,-6-(n%2)*3,(n%3-1)*7),new Vector3(16,7,13),0,Color.white,6,10);
                int towers=6+i%5;
                for(int n=0;n<towers;n++)
                {
                    float x=(n-towers*.5f)*5.2f;float height=8+Hash(new Vector3(i,n,3))*22;
                    var tint=Color.Lerp(RoofTints[(i+n)%4],Color.white,.35f);
                    islands.Box(c+new Vector3(x,height*.5f-2,(n%3-1)*4),new Vector3(4.2f,height,4.2f),Quaternion.identity,1,tint);
                    if(n%3==0)islands.Cone(c+new Vector3(x,height-2,(n%3-1)*4),Quaternion.identity,2.4f,6,1,Sugar,8);
                }
            }
            Model("Visual · distant candy islands",islands,transform,new[]{cloudMaterial,cityCandyMaterial});
            // Rainbow arc behind the skyline.
            var rainbow=new ArtMesh(1);
            var bands=new[]{new Color(1,.3f,.4f),new Color(1,.6f,.3f),new Color(1,.92f,.4f),new Color(.5f,1,.5f),new Color(.4f,.75f,1),new Color(.7f,.5f,1)};
            var center=new Vector3(0,-60,R+430);
            for(int band=0;band<bands.Length;band++)
            {
                float radius=330-band*10;
                for(int i=0;i<40;i++)
                {
                    float a=Mathf.PI*i/40,b=Mathf.PI*(i+1)/40;
                    var p=center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);var e=center+new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0);
                    var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*5;
                    var nextRadial=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*5;
                    rainbow.WaterStrip(p-radial,p+radial,e+nextRadial,e-nextRadial,Mathf.Abs(i-20)*.65f,Mathf.Abs(i+1-20)*.65f,band/6f,(band+1)/6f,bands[band]);
                }
            }
            CurrentModel("Visual · candy rainbow",rainbow,transform);
            float middleRadius=330-(bands.Length-1)*5;
            Splash(transform,center+Vector3.right*middleRadius,60,"skyline east");
            Splash(transform,center+Vector3.left*middleRadius,60,"skyline west");
            }
            // Cotton-candy waterfall pouring off the island rim.
            if(solidCandy)return; // Sky Ring builds both ends together with its river.
            float edge=Mathf.PI*1.1f;
            var rim=new Vector3(Mathf.Cos(edge)*halfWidth*.98f,-8,Mathf.Sin(edge)*halfDepth*.98f);
            var outward=new Vector3(Mathf.Cos(edge),0,Mathf.Sin(edge));
            var lip=new GameObject("Visual · rim spillway");lip.transform.SetParent(transform,false);
            lip.transform.localPosition=rim;lip.transform.localRotation=Quaternion.LookRotation(outward);
            ownedObjects.Add(lip);
            RainbowFall(lip.transform,Vector3.zero,17,30,150,"rim");
        }

        void AnimateCity()
        {
            float time=Time.time;
            if(rainbowCurrent)rainbowCurrent.SetFloat(FlowTime,time);
            if(balloonRing){balloonRing.localRotation=Quaternion.Euler(0,time*1.2f,0);balloonRing.localPosition=Vector3.up*Mathf.Sin(time*.3f)*2;}
            if(balloonRingOuter){balloonRingOuter.localRotation=Quaternion.Euler(0,-time*.7f,0);balloonRingOuter.localPosition=Vector3.up*Mathf.Sin(time*.25f+1)*3;}
            AnimateResidents(time);
            AnimateCars(time);
            if(solidCandy){AnimateBalloons(time);AnimateLandmarks(time);AnimateSkyCities(time);}
            float length=Mathf.Max(1,(cableB-cableA).magnitude);
            for(int i=0;i<cabins.Count;i++)
            {
                if(!cabins[i])continue;
                float t=Mathf.PingPong(time*3/length+i*.5f,1);
                cabins[i].localPosition=Vector3.Lerp(cableA,cableB,t);
            }
        }

        sealed partial class ArtMesh
        {
            void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 normal,int mat,Color color)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c});
                for(int i=0;i<3;i++){normals.Add(normal);colors.Add(color);}
                triangles[mat].AddRange(new[]{n,n+1,n+2});
            }
            public void Cylinder(Vector3 bottom,Quaternion rotation,float radius,float height,int mat=0,Color? tint=null,int sides=14)
            {
                var color=tint??Color.white;var up=rotation*Vector3.up;var top=bottom+up*height;
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var da=rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var db=rotation*new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    var n=(da+db).normalized;
                    Quad(bottom+db*radius,bottom+da*radius,top+da*radius,top+db*radius,mat,color);
                    Tri(top,top+db*radius,top+da*radius,up,mat,color);
                    Tri(bottom,bottom+da*radius,bottom+db*radius,-up,mat,color);
                }
            }
            public void Cone(Vector3 bottom,Quaternion rotation,float radius,float height,int mat=0,Color? tint=null,int sides=12)
            {
                var color=tint??Color.white;var up=rotation*Vector3.up;var tip=bottom+up*height;
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var pa=bottom+rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;var pb=bottom+rotation*new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius;
                    var normal=Vector3.Cross(pa-pb,tip-pb).normalized;
                    Tri(pb,pa,tip,normal,mat,color);
                    Tri(bottom,pa,pb,-up,mat,color);
                }
            }
        }
    }
}

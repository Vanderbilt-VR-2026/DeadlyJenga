using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class FossilBasinMap
    {
        enum Dinosaur { Stegosaurus, Rex, Triceratops }
        void Skeleton(Dinosaur kind,Vector3 rump,Vector3 shoulder,Site rear,Site front)
        {
            var f=Vector3.ProjectOnPlane(shoulder-rump,Vector3.up).normalized;
            var side=Vector3.Cross(f,Vector3.up);
            const int sections=8;
            FossilMember first=null,last=null;
            for(int i=0;i<sections;i++)
            {
                float t=(i+.5f)/sections;
                Vector3 Spine(float u)=>Vector3.Lerp(rump,shoulder,u)+Vector3.up*(.46f*Mathf.Sin(Mathf.PI*u));
                var at=Spine(t);float girth=.72f+.67f*Mathf.Sin(Mathf.PI*t);
                var rib=Piece(kind+" vertebra and ribs "+i,at,8,c=>{
                    c.Shaft(Spine((float)i/sections),Spine((i+1f)/sections),.44f,.49f,Ivory);
                    for(int s=-1;s<=1;s+=2)
                    {
                        var root=at+side*s*.22f;
                        var upper=at+side*s*girth+Vector3.down*.32f;
                        var flank=at+side*s*(girth+.31f)+Vector3.down*1.18f-f*.08f;
                        var belly=at+side*s*.58f+Vector3.down*(1.32f+girth*.53f)-f*.30f;
                        c.Shaft(root,upper,.24f,.24f,Ivory);
                        c.Shaft(upper,flank,.22f,.23f,Ivory);
                        c.Shaft(flank,belly,.18f,.19f,Ochre,true);
                    }
                    if(kind==Dinosaur.Stegosaurus)
                    {
                        var tip=at+Vector3.up*(1.05f+1.05f*Mathf.Sin(Mathf.PI*t))-f*.46f+side*((i%2==0?1:-1)*.32f);
                        c.Shaft(at+Vector3.up*.15f,tip,.65f,.19f,Ivory,false,true,true);
                    }
                    else c.Shaft(at,at+Vector3.up*.72f-f*.16f,.18f,.13f,Ochre,true);
                });
                if(last)Socket(rib,last,Spine((float)i/sections));
                if(!first)first=rib;
                last=rib;
            }
            Tail(kind,first,rump,f,side);
            for(int s=-1;s<=1;s+=2)
            {
                if(kind==Dinosaur.Rex)
                {
                    var site=s<0?rear:front;
                    Leg(kind,first,rump+side*s*.65f,site.top+side*s*.45f,f,site,s,true);
                    var elbow=shoulder+side*s*1.05f+Vector3.down*1.05f;
                    var hand=elbow+f*.69f+Vector3.down*.18f;
                    var arm=Piece("Rex small two-fingered arm",elbow,2,c=>{
                        c.Shaft(shoulder+side*s*.35f,elbow,.16f,.14f,Ivory);
                        c.Shaft(elbow,hand,.13f,.13f,Ivory);
                        for(int k=0;k<2;k++)c.Shaft(hand+side*k*.14f,hand+f*.36f+side*k*.22f+Vector3.down*.29f,.09f,.08f,Ivory,true);
                    });
                    Socket(arm,last,shoulder);
                }
                else
                {
                    Leg(kind,first,rump+side*s*.75f,rear.top+side*s*.92f+f*(.28f*s),f,rear,s,true);
                    Leg(kind,last,shoulder+side*s*.7f,front.top+side*s*.94f+f*(.27f*s),f,front,s,false);
                }
            }
            Skull(kind,last,shoulder,f,side);
        }
        void Leg(Dinosaur kind,FossilMember torso,Vector3 hip,Vector3 foot,Vector3 f,Site site,int sign,bool hind)
        {
            var knee=Vector3.Lerp(hip,foot,.43f)+f*(hind?1.0f:-.65f);
            var ankle=Vector3.Lerp(knee,foot,.75f)-f*.42f;
            var thigh=Piece(kind+" upper leg "+sign, (hip+knee)*.5f,10,c=>c.Shaft(hip,knee,.42f,.45f,Ivory));
            var shin=Piece(kind+" lower leg "+sign,(knee+ankle)*.5f,8,c=>c.Shaft(knee,ankle,.32f,.32f,Ivory));
            var instep=foot+Vector3.up*.25f;
            var toes=Piece(kind+" tarsals and digits "+sign,instep,7,c=>{
                c.Shaft(ankle,instep,.31f,.34f,Ivory);
                var side=Vector3.Cross(f,Vector3.up);
                for(int i=-1;i<=1;i++)
                {
                    var knuckle=instep+f*.46f+side*i*.36f;
                    c.Shaft(instep+side*i*.12f,knuckle,.19f,.2f,Ivory);
                    c.Shaft(knuckle,foot+f*(i==0?1.07f:.89f)+side*i*.55f+Vector3.up*.045f,.18f,.19f,Ochre,true);
                }
            });
            Socket(thigh,torso,hip);Socket(shin,thigh,knee);Socket(toes,shin,ankle);
            // The feet bear on the decks. No tie from a foot to a deck or to the world.
        }
        void Tail(Dinosaur kind,FossilMember pelvis,Vector3 rump,Vector3 f,Vector3 side)
        {
            int count=kind==Dinosaur.Triceratops?6:11;
            float length=kind==Dinosaur.Triceratops?4.3f:7.5f;
            Vector3 Path(float t)=>rump-f*(length*t)+side*(Mathf.Sin(t*Mathf.PI*.8f)*1.2f)
                +Vector3.down*((kind==Dinosaur.Stegosaurus?3.6f:2.4f)*t*t);
            for(int i=0;i<count;i++)
            {
                float t=(float)i/count;var a=Path(t);var b=Path((i+1f)/count);
                float r=Mathf.Lerp(.52f,.15f,t);
                var segment=Piece(kind+" tail centrum "+i,(a+b)*.5f,Mathf.Lerp(4,.8f,t),c=>{
                    c.Shaft(a,b,r,r,Ivory);
                    if(kind==Dinosaur.Stegosaurus && i<count-3)
                        c.Shaft(a,a+Vector3.up*(1.06f-t)-f*.18f,.31f,.13f,Ivory,true,true,true);
                    if(kind==Dinosaur.Stegosaurus && (i==count-1||i==count-3))
                        for(int s=-1;s<=1;s+=2)c.Shaft(a,a+side*s*.76f-f*.8f+Vector3.up*.72f,.2f,.2f,Ivory,true);
                });
                Socket(segment,pelvis,a);pelvis=segment;
            }
        }
        void Skull(Dinosaur kind,FossilMember torso,Vector3 shoulder,Vector3 f,Vector3 side)
        {
            var up=Vector3.up;
            var neck=shoulder+f*(kind==Dinosaur.Stegosaurus?2.5f:1.5f)+up*(kind==Dinosaur.Stegosaurus?1.15f:.68f);
            var cervical=Piece(kind+" cervical vertebrae",(shoulder+neck)*.5f,6,c=>{
                for(int i=0;i<4;i++)c.Shaft(Vector3.Lerp(shoulder,neck,i/4f),Vector3.Lerp(shoulder,neck,(i+1)/4f),.39f,.43f,Ivory);
            });
            Socket(cervical,torso,shoulder);
            float length=kind==Dinosaur.Rex?3.3f:kind==Dinosaur.Triceratops?2.55f:1.1f;
            float width=kind==Dinosaur.Stegosaurus?.35f:.72f;
            var snout=neck+f*length+up*(kind==Dinosaur.Rex?.36f:.08f);
            var lower=snout-up*(kind==Dinosaur.Rex?1.72f:kind==Dinosaur.Triceratops?.96f:.34f)-f*.26f;
            var skull=Piece(kind+" perforated skull",neck,11,c=>{
                for(int s=-1;s<=1;s+=2)
                {
                    var cheek=neck+side*s*width;
                    var brow=cheek+up*.72f+f*.36f;
                    var eye=cheek+f*.96f+up*.37f;
                    var nose=snout+side*(s*width*.65f);
                    c.Shaft(cheek,brow,.31f,.25f,Ivory);
                    c.Shaft(brow,eye,.28f,.28f,Ivory);
                    c.Shaft(eye,cheek+f*.51f-up*.19f,.19f,.2f,Ochre);
                    c.Shaft(cheek,cheek+f*.51f-up*.19f,.23f,.24f,Ivory);
                    c.Shaft(eye,nose+up*.15f,.32f,.28f,Ivory);
                    c.Shaft(cheek+f*.51f-up*.19f,nose,.22f,.22f,Ivory);
                    for(int i=0;i<7;i++)
                    {
                        var tooth=Vector3.Lerp(eye,nose,(i+1)/8f)-up*.15f;
                        c.Shaft(tooth,tooth-up*(kind==Dinosaur.Rex?.32f:.13f),.09f,.09f,Ivory,true,false);
                    }
                    if(kind==Dinosaur.Triceratops)
                    {
                        var tip=s>0?P(2.3f,14.0f,4.15f):brow+f*1.95f+up*1.62f;
                        var bend=Vector3.Lerp(brow,tip,.55f)-up*.19f;
                        c.Shaft(brow,bend,.28f,.29f,Ivory);
                        c.Shaft(bend,tip,.20f,.2f,Ivory,true);
                    }
                }
                c.Shaft(snout-side*width*.7f,snout+side*width*.7f,.25f,.29f,Ivory);
                if(kind!=Dinosaur.Triceratops)return;
                c.Shaft(snout,snout+up*.76f+f*.29f,.2f,.20f,Ivory,true);
                // Radial plates spread from the occiput across the back of the skull.
                for(int k=-5;k<=5;k++)
                {
                    float angle=k*Mathf.PI/12;
                    var root=neck+side*(k*.11f)+up*.25f;
                    var edge=neck-f*(1.12f+.48f*Mathf.Cos(angle))+side*(1.96f*Mathf.Sin(angle))+up*(.58f+2.2f*Mathf.Cos(angle));
                    c.Shaft(root,edge,.42f,.44f,Ivory,false,true,true);
                }
            });
            Socket(skull,cervical,neck);
            var jaw=Piece(kind+" mandible",(neck+lower)*.5f,6,c=>{
                for(int s=-1;s<=1;s+=2)
                {
                    var hinge=neck+side*s*width-up*.12f;
                    var chin=lower+side*s*width*.47f;
                    c.Shaft(hinge,chin,.22f,.2f,Ivory);
                    for(int i=1;i<7;i++)
                    {
                        var tooth=Vector3.Lerp(hinge,chin,i/7f);
                        c.Shaft(tooth,tooth+up*.24f,.08f,.07f,Ivory,true,false);
                    }
                }
                c.Shaft(lower-side*width*.47f,lower+side*width*.47f,.17f,.18f,Ochre);
            });
            Socket(jaw,skull,neck-up*.12f);
        }
    }
}

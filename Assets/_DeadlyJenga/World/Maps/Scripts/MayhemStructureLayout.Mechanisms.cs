using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static partial class MayhemStructureLayout
    {
        static void Crowns(Builder b)
        {
            const float radius=8, width=3, thick=.65f;
            float halfAngle=Mathf.PI/8;
            float tilt=Mathf.Asin(thick/((2*radius-width)*Mathf.Tan(halfAngle)))*Mathf.Rad2Deg;
            for(int site=0;site<6;site++)
            {
                var p=new Vector3((site%3-1)*36,0,(site/3-.5f)*40);
                float height=7+(site%3)*5;
                var landings=new Vector3[8];
                for(int beam=0;beam<8;beam++)
                {
                    float angle=beam*45*Mathf.Deg2Rad;
                    var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var along=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle));
                    var center=p+radial*radius+Vector3.up*height;
                    var m=b.Module(center,"Reciprocal bearing beam");
                    var q=Quaternion.LookRotation(along,Vector3.up)*Quaternion.Euler(-tilt,0,0);
                    b.Part(m,Vector3.zero,new Vector3(width,thick,14),beam%3,q);
                    // The low outer end bears on a pier; the high end bears on its neighbor.
                    float foot=-5.5f;
                    var bearing=center+along*foot;
                    float y=height+foot*Mathf.Tan(tilt*Mathf.Deg2Rad)-thick*.5f/Mathf.Cos(tilt*Mathf.Deg2Rad)-.24f;
                    var pier=b.Box(new Vector3(bearing.x,y*.5f,bearing.z),new Vector3(3,y,3),0,"Reciprocal outer pier");
                    var inward=Vector3.ProjectOnPlane(bearing-p,Vector3.up).normalized;
                    b.Part(pier,-inward*2.5f+Vector3.up*(height*.5f-y*.5f),new Vector3(3,.5f,4),2,Quaternion.LookRotation(inward,Vector3.up));
                    landings[beam]=new Vector3(bearing.x,height*.5f+.25f,bearing.z)-inward*3.5f;
                }
                for(int i=0;i<8;i++){var a=landings[i];var c=landings[(i+1)%8];var trim=(c-a).normalized;b.Link(a+trim,c-trim,1.8f);}
                var ramp=b.Module(p,"Crown approach ramp");
                float approach=site<3?-1:1;
                b.Ramp(ramp,new Vector3(-approach*8,0,approach*38),new Vector3(-approach*8,height+.35f,approach*6.7f),1.2f);
            }
        }
        static void Skyrail(Builder b)
        {
            for(int row=0;row<3;row++)for(int span=0;span<4;span++)
            {
                float x=(span-1.5f)*26,z=(row-1)*30;
                float y=5+row*7;
                var m=b.Module(new Vector3(x,y,z),"Two-deck skyrail girder");
                b.Part(m,new Vector3(0,.4f,0),new Vector3(26,.8f,10),1);
                for(int side=-1;side<=1;side+=2)b.Part(m,new Vector3(0,5.4f,side*4),new Vector3(26,.8f,2),2);
                b.Part(m,new Vector3(12,5.4f,0),new Vector3(2,.8f,6),2);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Part(m,new Vector3(-11,2.9f,side*4.3f),new Vector3(1,4.2f,1.4f),0);
                    b.Part(m,new Vector3(11,2.9f,side*4.3f),new Vector3(1,4.2f,1.4f),0);
                    b.Beam(m,new Vector3(-10.5f,.8f,side*4.3f),new Vector3(10.5f,5,side*4.3f),1.4f,.65f,0);
                }
                b.Ramp(m,new Vector3(-11,.8f,0),new Vector3(11,5.8f,0),3);
                for(int end=-1;end<=1;end+=2)b.Pier(new Vector3(x+end*10,0,z),y,5);
                if(span==0)
                {var entry=b.Module(new Vector3(x,y,z),"Ground-to-skyrail approach");b.Ramp(entry,new Vector3(-13-y*2,-y,0),new Vector3(-12.6f,1.6f,0),4);}
                if(row<2&&span%2==0)b.Link(new Vector3(x,y+5.8f,z+4),new Vector3(x,y+12.8f,z+26),4);
            }
        }
        static void Portals(Builder b)
        {
            for(int row=0;row<3;row++)for(int col=0;col<6;col++)
            {
                float x=(col-2.5f)*18,z=(row-1)*30;int levels=4+(col+row)%4;
                var m=b.Module(new Vector3(x,0,z),"Domino portal page");
                b.Part(m,new Vector3(0,.5f,0),new Vector3(14,1,7),0);
                for(int side=-1;side<=1;side+=2)b.Part(m,new Vector3(side*6,(1+levels*4)*.5f,0),new Vector3(2,1+levels*4,3),0);
                for(int level=1;level<=levels;level++)b.Part(m,new Vector3(0,level*4+.65f,0),new Vector3(14,.7f,3),level%2+1);
                // A real diagonal route through the openings, integral to this rigid page.
                for(int level=0;level<levels;level++){float lane=level%2==0?-2.2f:2.2f;b.Flight(m,new Vector3(level%2==0?-3:3,1+level*4,lane),new Vector3(level%2==0?3:-3,1+(level+1)*4,lane),1.5f,2.5f);}
                if(row<2)for(int level=1;level<=2;level++)b.Link(new Vector3(x,level*4+1,z+1),new Vector3(x,level*4+1,z+29),3);
            }
            for(int row=0;row<2;row++)
            {
                for(int col=row==0?0:1;col<5;col+=2)
                {
                    float x=(col-2)*18,z=(row-1)*30+15;
                    var h=b.Module(new Vector3(x,13,z),"Three-way portal street junction");
                    for(int side=-1;side<=1;side+=2)b.Part(h,new Vector3(side*9,.24f,0),new Vector3(3,.32f,28),2);
                    b.Part(h,new Vector3(0,.24f,0),new Vector3(18,.32f,3),1);
                }
                if(row==1)foreach(int col in new[]{0,5}){float x=(col-2.5f)*18;b.Link(new Vector3(x,13,1),new Vector3(x,13,29),3);}
            }
        }
        static void Vaults(Builder b)
        {
            for(int site=0;site<3;site++)for(int tier=0;tier<3-site;tier++)
            {
                float x=(site-1)*34,baseY=tier*12.66f;
                for(int end=-1;end<=1;end+=2)
                {
                    var cradle=b.Module(new Vector3(x,baseY,end*9),"Tied arch thrust cradle");cradle.mass=600;
                    b.Part(cradle,new Vector3(0,.25f,0),new Vector3(28,.5f,6),0);
                    for(int side=-1;side<=1;side+=2)
                    {
                        b.Part(cradle,new Vector3(side*8,1.5f,0),new Vector3(7,3,6),0);
                        b.Part(cradle,new Vector3(side*12.75f,5,0),new Vector3(2.5f,4,6),0);
                    }
                    for(int stone=0;stone<5;stone++)
                    {
                        float a=stone*Mathf.PI/5,c=(stone+1)*Mathf.PI/5;var vs=new Vector3[8];
                        for(int back=0;back<2;back++)
                        {
                            float z=(back==0?-1:1)*2.8f;
                            vs[back*4]=new Vector3(Mathf.Cos(c)*7,Mathf.Sin(c)*7,z);
                            vs[back*4+1]=new Vector3(Mathf.Cos(a)*7,Mathf.Sin(a)*7,z);
                            vs[back*4+2]=new Vector3(Mathf.Cos(a)*9,Mathf.Sin(a)*9,z);
                            vs[back*4+3]=new Vector3(Mathf.Cos(c)*9,Mathf.Sin(c)*9,z);
                            if(stone==0){vs[back*4+1].x=4.5f;vs[back*4+2].x=11.5f;}
                            if(stone==4){vs[back*4].x=-4.5f;vs[back*4+3].x=-11.5f;}
                        }
                        var centroid=Vector3.zero;foreach(var v in vs)centroid+=v;centroid/=vs.Length;
                        for(int n=0;n<vs.Length;n++)vs[n]-=centroid;
                        var m=b.Module(new Vector3(x,baseY+3,end*9)+centroid,stone==2?"Loose arch keystone":"Compression arch wedge");
                        m.mass=90;m.shapes.Add(new MayhemShape(Vector3.zero,new Vector3(18,9,5.6f),stone%3,Quaternion.identity,vs));
                        if(stone==2)b.Part(m,new Vector3(0,9*Mathf.Cos(Mathf.PI/10)+.2f,0)-centroid,new Vector3(4,.4f,5.6f),1);
                    }
                }
                float top=baseY+3+9*Mathf.Cos(Mathf.PI/10)+.4f;
                var deck=b.Module(new Vector3(x,top,0),"Vault terrace deck");
                b.Part(deck,new Vector3(0,.35f,0),new Vector3(28,.7f,26),2);
                b.Ramp(deck,new Vector3(-13,baseY-top,14.5f),new Vector3(13,.7f,14.5f),4);
            }
            for(int site=0;site<2;site++)b.Link(new Vector3((site-1)*34+12,12.66f,0),new Vector3(site*34-12,12.66f,0),3);
        }
        static void Drums(Builder b)
        {
            const float radius=7,wall=.75f;
            float facet=2*radius*Mathf.Tan(Mathf.PI/8);
            for(int site=0;site<8;site++)
            {
                float x=(site%4-1.5f)*29,z=(site/4-.5f)*44;float height=site%4*3;
                var m=b.Module(new Vector3(x,height+radius+1,z),"Rolling octagonal borough");
                // Flat top and bottom make both the collider and the settled pose readable.
                for(int side=0;side<8;side++)
                {
                    var q=Quaternion.Euler(0,0,side*45);b.Part(m,q*new Vector3(0,-radius+wall*.5f,0),new Vector3(facet,wall,25),side%3,q);
                }
                b.Part(m,new Vector3(0,-2.5f,0),new Vector3(12.5f,.65f,25),2);
                b.Part(m,new Vector3(0,2.5f,0),new Vector3(12.5f,.65f,25),1);
                b.Ramp(m,new Vector3(-4.5f,-2.175f,-11),new Vector3(-4.5f,2.825f,11),2.5f);
                m.mass=150;
                for(int end=-1;end<=1;end+=2)
                {
                    var cradle=b.Module(new Vector3(x,0,z+end*8),"Loose drum cradle");
                    b.Part(cradle,new Vector3(0,(height+1)*.5f,0),new Vector3(15,height+1,4),0);
                    for(int side=-1;side<=1;side+=2)b.Part(cradle,new Vector3(side*6,height+1.8f,0),new Vector3(2,1.6f,4),1);
                    cradle.mass=300;
                }
                var ramp=b.Module(new Vector3(x,0,z),"Drum entry ramp");
                float direction=site<4?-1:1;
                b.Ramp(ramp,new Vector3(0,.1f,direction*(13+(height+6)*2)),new Vector3(0,height+6.625f,direction*11.9f),3.5f);
            }
        }
        static void Harbor(Builder b)
        {
            for(int site=0;site<8;site++)
            {
                float x=(site%4-1.5f)*29,z=(site/4-.5f)*52;float height=12+(site%4)*4;
                var frame=b.Module(new Vector3(x,0,z),"Bascule dynamic pier frame");frame.mass=600;
                b.Part(frame,new Vector3(0,1,0),new Vector3(15,2,14),0);
                for(int side=-1;side<=1;side+=2)b.Part(frame,new Vector3(side*5,(height+2)*.5f,0),new Vector3(3,height+2,5),0);
                for(int level=1;level*4<=height;level++)
                {
                    bool top=level*4==height;
                    if(!top)b.Part(frame,new Vector3(0,level*4,0),new Vector3(13,.65f,5),2);
                    for(int side=-1;side<=1;side+=2)b.Part(frame,new Vector3(side*4,level*4+(top?.01f:0),top?-8.61f:-6),new Vector3(2,top?.7f:.65f,top?6.98f:12),2);
                }
                for(int level=0;level*4<height;level++)
                {float lane=level%2==0?-6.5f:-10;b.Flight(frame,new Vector3(level%2==0?-3:3,level==0?2:level*4+.35f,lane),new Vector3(level%2==0?2.4f:-2.4f,(level+1)*4+.35f,lane),1.6f,2.6f);}
                b.Part(frame,new Vector3(0,height-.55f,-4),new Vector3(6,.4f,2),0);
                b.Ramp(frame,new Vector3(-3,.03f,-18),new Vector3(-3,2,-6.9f),2.6f);
                var leaf=b.Module(new Vector3(x,height,z),"Counterweighted bascule leaf");leaf.mass=80;leaf.hingeSupport=frame;leaf.pivot=new Vector3(x,height,z)+b.origin;
                b.Part(leaf,new Vector3(0,0,5.5f),new Vector3(7,.7f,21),2);
                for(int side=-1;side<=1;side+=2)b.Part(leaf,new Vector3(side*2.9f,.75f,-2),new Vector3(.5f,1.5f,2),1);
                b.Box(new Vector3(x,height+1.35f,z-4),new Vector3(3.8f,2,1.8f),3,"Removable heel counterweight",180);
                var landing=b.Module(new Vector3(x,0,z+18),"Lower bascule landing");
                b.Part(landing,new Vector3(0,(height-8)*.5f,0),new Vector3(10,height-8,8),0);
                b.Part(landing,new Vector3(0,height-7.65f,0),new Vector3(13,.7f,10),1);
                if(site%4<3)b.Link(new Vector3(x+4,height+.325f,z-8.6f),new Vector3(x+25,height+4.325f,z-8.6f),2);
            }
        }
    }
}





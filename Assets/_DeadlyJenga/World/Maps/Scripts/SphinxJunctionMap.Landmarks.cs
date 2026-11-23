using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class SphinxJunctionMap
    {
        void Sphinx()
        {
            foreach(int side in new[]{-1,1})
            {
                Begin("Sphinx shoulder bearing "+side,side*4,2.5f,1);
                Box(0,2.5f,0,3.5f,5,8.3f,Shade);
                for(int j=0;j<4;j++) Round(0,.62f+1.25f*j,0,4.6f-.15f*j,1.24f,9,Sand,1,10);
                End();
                for(int j=0;j<3;j++)
                {
                    Begin("Sphinx foreleg "+side+"/"+j,side*4,2.5f,-4.55f-j*2.45f);
                    float h=3.75f-j*.85f;
                    Round(0,h*.5f,0,3.48f,h,2.44f,Sand,.78f,10,true);
                    for(int toe=-1;toe<=1;toe++)
                    {
                        Round(toe*1.06f,h*.51f,-.08f,1.44f,h*.92f,2.75f,Honey,.64f,10);
                        if(j==2) Round(toe*1.06f,.37f,-1.2f,.64f,.72f,.49f,Teal,.65f);
                    }
                    End();
                }
                Stairs("Shoulder processional ascent",side*7.2f,2.5f,-1.2f,2.2f,5,8.5f);
            }
            Begin("Sphinx chest lintel",0,8.16f,1);
            Box(0,0,0,11.4f,1.32f,8.1f,Honey);
            Box(0,-.27f,-4.06f,11.4f,.34f,.14f,Teal,false);
            for(int i=-4;i<=4;i+=2) Lamp(i,-.2f,-4.24f);
            End();
            Begin("Sphinx striped nemes and carved face",0,8.82f,.25f,-12);
            Box(0,.9f,0,5.5f,1.8f,4.6f,Sand);
            // A limestone neck, jaw, cheekbones and brow, sculpted as successive polygon rings.
            Round(0,2.65f,-.2f,4.7f,1.7f,4.3f,Honey,1.06f,14,true);
            Round(0,4.2f,-.5f,4.3f,1.4f,4.3f,Honey,1.28f,14,true);
            Round(0,5.9f,-.35f,5.5f,2.05f,4.7f,Honey,1.04f,14,true);
            Round(0,7.38f,-.12f,5.75f,.95f,4.85f,Sand,.87f,14,true);
            for(int s=-1;s<=1;s+=2)
            {
                for(int j=0;j<9;j++)
                {
                    float t=j/8f;
                    Round(s*(3.85f-.93f*t),.45f+j*.83f,.25f+t*.64f,
                        2.65f+1.24f*Mathf.Sin(t*3.14f),.84f,4.5f-.5f*t,j%2==0?Gold:Teal,1,8,true);
                }
                Beam(new(s*.4f,6.5f,-2.7f),new(s*2.12f,6.77f,-2.12f),.27f,.42f,Dark);
                Round(s*1.25f,6.19f,-2.52f,1.43f,.39f,.48f,Teal,.82f);
                Round(s*1.17f,6.2f,-2.74f,.26f,.27f,.14f,Iron);
                Round(s*2.77f,5.6f,-.75f,.89f,1.25f,1.2f,Honey);
                Round(s*2.9f,4.68f,-1.05f,.86f,1.12f,.48f,Gold,.36f);
            }
            Round(0,5.56f,-2.62f,.93f,1.47f,1.24f,Sand,.58f,8);
            Round(0,4.65f,-2.69f,1.85f,.32f,.64f,Shade);
            Round(0,4.40f,-2.7f,1.62f,.28f,.57f,Honey);
            for(int j=0;j<7;j++) Round(0,1.77f+.32f*j,-2.83f+.045f*j,1.16f,.34f,.93f,j%2==0?Gold:Teal);
            Round(0,8.04f,-.03f,5.8f,.4f,5,Gold, .92f,14);
            foreach(int sign in new[]{-1,1})
                Beam(new(sign*.3f,8.05f,-1.96f),new(sign*2.35f,8.93f,-.92f),.32f,.65f,Gold);
            Round(0,8.59f,-2.34f,1.18f,1.5f,.8f,Gold,.12f,6);
            Round(0,8.38f,-2.78f,.55f,.72f,.25f,Teal,.55f,8);
            End();
        }

        void Saloon()
        {
            const float x=-23,z=-2;
            for(int level=0;level<2;level++)
            {
                Begin("Saloon timber storey "+level,x,2.5f+3.4f*level,z);
                Box(0,.13f,0,10,.26f,6,Wood);
                Box(0,1.69f,2.84f,9.9f,3.38f,.28f,Red);
                foreach(int side in new[]{-1,1})
                {
                    Box(side*4.83f,1.83f,0,.34f,3.14f,5.6f,Red);
                    Box(side*3.25f,1.75f,-2.84f,3.4f,3.1f,.28f,Wood);
                    Box(side*3.25f,1.93f,-3.01f,1.65f,1.53f,.10f,Teal,false);
                    Box(side*3.25f,1.93f,-3.11f,.12f,1.57f,.16f,Gold,false);
                    Box(side*3.25f,1.93f,-3.11f,1.67f,.12f,.16f,Gold,false);
                    Box(side*.96f,.95f,-2.9f,.89f,1.38f,.21f,Honey,false);
                }
                Box(0,3.27f,-2.84f,10,.26f,.34f,Wood);
                for(int i=-4;i<=4;i+=2) Lamp(i,2.56f,-3.04f);
                End();
                Begin("Saloon wraparound veranda "+level,x,2.5f+level*3.4f,z);
                Box(0,.14f,-4.01f,13,.28f,1.99f,Wood);
                foreach(int s in new[]{-1,1})
                {
                    Box(s*5.75f,.14f,.1f,1.5f,.28f,6.2f,Wood);
                    for(int j=-1;j<=1;j++) Box(s*6.25f,1.84f,j*2.7f,.38f,3.12f,.38f,Wood);
                }
                for(int i=-3;i<=3;i++)
                {
                    Box(i*2,1.84f,-4.77f,.34f,3.12f,.34f,Red);
                    if(level==1 && i<3) Box(i*2+1,1.12f,-4.77f,2,.16f,.18f,Teal);
                }
                End();
            }
            Begin("Saloon false front and cornice",x,9.3f,z);
            Box(0,.25f,0,10.75f,.5f,6.4f,Teal);
            Box(0,1.05f,-2.85f,9.8f,1.6f,.45f,Red);
            Box(0,1.95f,-2.85f,6.4f,.59f,.49f,Wood);
            Round(0,2.29f,-2.85f,3.75f,1.24f,.53f,Teal,.77f);
            Box(0,1.15f,-3.12f,6.95f,.72f,.13f,Gold,false);
            End();
            Stairs("Saloon balcony steps",x-5.6f,2.5f,-8.5f,1.65f,3.67f,6.8f);
            Begin("Depot water tank timber frame",-23,2.5f,7.5f);
            foreach(int a in new[]{-1,1}) foreach(int b in new[]{-1,1}) Box(a*1.63f,3.87f,b*1.63f,.53f,7.74f,.53f,Wood);
            for(int s=-1;s<=1;s+=2)
            {
                Beam(new(-1.65f,.4f,s*1.64f),new(1.65f,7.3f,s*1.64f),.26f,.29f,Dark);
                Beam(new(1.65f,.4f,s*1.64f),new(-1.65f,7.3f,s*1.64f),.26f,.29f,Dark);
            }
            Box(0,7.95f,0,4.5f,.42f,4.5f,Wood);
            End();
            Begin("Banded depot cistern",-23,10.66f,7.5f);
            Round(0,1.95f,0,4.35f,3.9f,4.35f,Teal,1,18,true);
            for(int j=0;j<4;j++) Round(0,.32f+j*1.05f,0,4.48f,.19f,4.48f,Wood,1,18);
            Round(0,4.53f,0,4.9f,1.26f,4.9f,Red,.06f,18);
            End();
            Begin("Stock corral",-22,2.5f,-10);
            for(int s=-1;s<=1;s+=2) {
                for(int i=-2;i<=2;i++) Box(i*1.9f,.65f,s*1.55f,.22f,1.3f,.22f,Wood);
                for(int j=0;j<2;j++) Box(0,.53f+j*.52f,s*1.55f,7.8f,.18f,.15f,Honey);
            }
            End();
        }

        static Vector3 PyramidPoint(float localX,float localZ)
            =>new Vector3(20,0,10)+Quaternion.Euler(0,-40,0)*new Vector3(localX,0,localZ);

        void Pyramid()
        {
            const float x=20,z=10;
            // Galleries flank a full-width east-west train bore on the middle tier.
            for(int tier=0;tier<4;tier++)
            {
                float y=2.5f+tier*3.18f;
                float w=18.6f-tier*2.65f, depth=17-tier*2.5f;
                foreach(int s in new[]{-1,1})
                {
                    float inner=2.55f, strip=depth*.5f-inner;
                    var center=PyramidPoint(0,s*(inner+strip*.5f));
                    Begin("Pyramid gallery "+tier+"/"+s,center.x,y,center.z,-40);
                    for(int col=-2;col<=2;col++)
                    {
                        float px=col*(w-2.3f)/4;
                        float pierWidth=col==0 ? 1.35f : 1.93f;
                        model.Shape(new(px,1.3f,0),new(pierWidth,2.6f,strip),Sand,top:.83f);
                        for(int course=0;course<4;course++)
                        {
                            float width=pierWidth*(1-course*.045f);
                            Box(px,.32f+course*.64f,s*strip*.37f,width,.60f,strip*.25f,course%3==0?Honey:Sand,false);
                        }
                        Box(px,.45f,0,1.43f,.31f,Mathf.Min(strip,2.3f),Teal,false);
                        Box(px,2.32f,0,1.62f,.38f,Mathf.Min(strip,2.36f),Gold,false);
                        Lamp(px,1.6f,-s*.78f);
                    }
                    Box(0,2.89f,0,w,.58f,strip,Honey);
                    for(int k=0;k<8;k++) Box((k-3.5f)*w/8,2.9f,s*strip*.5f,w/8-.045f,.51f,.18f,k%3==0?Shade:Sand,false);
                    End();
                }
            }
            Begin("Pyramid portal vault",x,15.22f,z,-40);
            Box(0,.48f,0,10.4f,.96f,9.35f,Honey);
            foreach(int s in new[]{-1,1})
            {
                Beam(new(-5.19f,-2.55f,s*2.62f),new(-5.19f,.27f,s*.91f),.44f,.55f,Teal);
                Beam(new(5.19f,-2.55f,s*2.62f),new(5.19f,.27f,s*.91f),.44f,.55f,Teal);
            }
            End();
            Begin("Open summit pavilion",x,16.18f,z,-40);
            for(int i=-1;i<=1;i++) for(int k=-1;k<=1;k+=2)
            {
                Round(i*2.55f,1.56f,k*2.28f,.84f,3.12f,.84f,Honey,.86f,10,true);
                Round(i*2.55f,2.88f,k*2.28f,1.14f,.46f,1.14f,Teal,1.22f);
            }
            Box(0,3.42f,0,7.5f,.6f,6.15f,Gold);
            Box(0,3.78f,0,7.1f,.16f,5.9f,Teal,false);
            End();
            var entry=PyramidPoint(0,-11.8f);
            Stairs("Pyramid broad forecourt flight",entry.x,2.5f,entry.z,4.8f,3.18f,7.1f,-40);
            // Switchbacks rest along the exposed ledge at each gallery setback.
            for(int tier=1;tier<4;tier++)
            {
                float outer=-(17-(tier-1)*2.5f)*.5f;
                var landing=PyramidPoint(0,outer+.54f);
                Stairs("Pyramid terrace flight "+tier,landing.x,2.5f+3.18f*tier,landing.z,
                    1.1f,3.18f,6.2f,tier%2==1?50:-130);
            }
        }
    }
}

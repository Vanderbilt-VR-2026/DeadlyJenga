using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class SphinxJunctionMap
    {
        static Vector3[] NemesSection(int side,float t)
        {
            float y=t*7.96f;
            float inner=1.85f+.58f*Mathf.Sin(t*Mathf.PI);
            float outer=3.92f+1.04f*Mathf.Sin(t*Mathf.PI)-.91f*t;
            float front=-3.20f+2.52f*t;
            float rear=1.04f+1.27f*t;
            var row=new[]{new Vector3(outer*.95f,y,rear),new Vector3(outer,y,.68f*rear+.32f*front),
                new Vector3(outer-.33f,y,front+.34f),new Vector3(inner+.20f,y,front),
                new Vector3(inner,y,front+.51f),new Vector3(inner+.10f,y,rear-.22f)};
            if(side<0)
            {
                for(int i=0;i<row.Length;i++)row[i].x=-row[i].x;
                System.Array.Reverse(row);
            }
            return row;
        }
        void Sphinx()
        {
            foreach(int side in new[]{-1,1})
            {
                Begin("Sphinx shoulder bearing "+side,side*5.5f,2.5f,1);
                Box(0,2.5f,0,4.6f,5,8.3f,Shade);
                for(int j=0;j<4;j++) Round(0,.62f+1.25f*j,0,5.6f-.24f*j,1.24f,9,Sand,1,10);
                End();
                for(int j=0;j<3;j++)
                {
                    Begin("Sphinx foreleg "+side+"/"+j,side*5.5f,2.5f,-4.55f-j*2.45f);
                    float h=3.75f-j*.85f;
                    for(int toe=-1;toe<=1;toe++)
                    {
                        float x=toe*1.62f, crown=h-.15f*Mathf.Abs(toe);
                        var front=ToeSection(x,-1.21f,crown-(j==2?1.35f:.56f));
                        var back=ToeSection(x,1.21f,crown+.30f);
                        model.DrapedBand(front,back,toe==0?Honey:Sand);
                        if(j==2)
                            model.Shape(new(x,.28f,-1.225f),new(.83f,.54f,.10f),Teal,6,.55f,solid:false);
                    }
                    End();
                }
                Stairs("Shoulder processional ascent",side*9.05f,2.5f,-1.2f,2.2f,5,8.5f);
            }
            Begin("Sphinx chest lintel",0,8.16f,1);
            Box(0,0,0,15.6f,1.32f,8.1f,Honey);
            Box(0,-.27f,-4.06f,15.6f,.34f,.14f,Teal,false);
            for(int i=-4;i<=4;i+=2) Lamp(i,-.2f,-4.24f);
            End();
            Begin("Sphinx striped nemes and carved face",0,8.82f,.25f,-12);
            Box(0,.9f,0,5.5f,1.8f,4.6f,Sand);
            unit.transform.localScale=new Vector3(1.32f,1.18f,1.18f);
            model.Carving(Vector3.zero,Sand,new(2.3f,1.75f,1.77f,0),new(1.91f,1.62f,2.6f,0),
                new(2.06f,1.72f,4.32f,-.28f));
            model.Carving(Vector3.zero,Honey,new(1.12f,.98f,3.59f,-1.22f),new(1.75f,1.63f,3.98f,-.89f),
                new(2.28f,1.99f,4.77f,-.54f),new(2.67f,2.33f,5.65f,-.33f),
                new(2.72f,2.21f,6.29f,-.24f),new(2.85f,2.39f,6.67f,-.22f),
                new(2.79f,2.36f,7.43f,-.1f),new(2.40f,2.15f,7.92f,-.03f));
            for(int s=-1;s<=1;s+=2)
            {
                for(int j=0;j<9;j++)
                {
                    model.DrapedBand(NemesSection(s,j/9f),NemesSection(s,(j+1)/9f),j%2==0?Gold:Teal);
                }
                Beam(new(s*.4f,6.5f,-2.7f),new(s*2.12f,6.77f,-2.12f),.27f,.42f,Dark);
                Beam(new(s*.41f,6.14f,-2.53f),new(s*1.39f,6.42f,-2.47f),.16f,.19f,Dark);
                Beam(new(s*1.39f,6.42f,-2.47f),new(s*2.09f,6.35f,-2.04f),.14f,.2f,Dark);
                Round(s*1.19f,6.20f,-2.49f,1.41f,.32f,.47f,Teal,.62f);
                Round(s*1.17f,6.2f,-2.74f,.26f,.27f,.14f,Iron);
                Round(s*2.77f,5.6f,-.75f,.89f,1.25f,1.2f,Honey);
                Round(s*2.9f,4.68f,-1.05f,.86f,1.12f,.48f,Gold,.36f);
            }
            model.Carving(Vector3.zero,Sand,new(.46f,.29f,5.08f,-2.62f),new(.55f,.59f,5.28f,-2.82f),
                new(.34f,.47f,5.67f,-2.65f),new(.29f,.22f,6.52f,-2.48f));
            Round(0,4.70f,-2.52f,1.65f,.22f,.69f,Shade,.72f);
            Round(0,4.48f,-2.53f,1.54f,.25f,.66f,Honey,.89f);
            for(int j=0;j<7;j++) Round(0,1.77f+.32f*j,-2.83f+.045f*j,1.16f,.34f,.93f,j%2==0?Gold:Teal);
            Round(0,8.04f,-.03f,5.8f,.4f,5,Gold, .92f,14);
            foreach(int sign in new[]{-1,1})
                Beam(new(sign*.3f,8.05f,-1.96f),new(sign*2.35f,8.93f,-.92f),.32f,.65f,Gold);
            Round(0,8.59f,-2.34f,1.18f,1.5f,.8f,Gold,.12f,6);
            Round(0,8.38f,-2.78f,.55f,.72f,.25f,Teal,.55f,8);
            End();
        }

        static Vector3[] ToeSection(float x,float z,float height)
        {
            return new[]{new Vector3(x-.79f,0,z),new Vector3(x+.79f,0,z),
                new Vector3(x+.79f,height*.54f,z),new Vector3(x+.66f,height*.81f,z),
                new Vector3(x+.36f,height*.96f,z),new Vector3(x,height,z),
                new Vector3(x-.36f,height*.96f,z),new Vector3(x-.66f,height*.81f,z),
                new Vector3(x-.79f,height*.54f,z)};
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
                    // Open sash above the sill; both shutters are folded back against the posts.
                    Box(side*3.25f,.70f,-2.84f,3.4f,1.20f,.28f,Wood);
                    Box(side*3.25f,2.99f,-2.84f,3.4f,.57f,.28f,Wood);
                    for(int shutter=-1;shutter<=1;shutter+=2)
                    {
                        float post=side*3.25f+shutter*1.30f;
                        Box(post,2.01f,-2.84f,.8f,1.42f,.28f,Red);
                        for(int slat=0;slat<5;slat++)
                            Box(post,1.48f+slat*.27f,-3.0f,.67f,.18f,.13f,Teal,false);
                    }
                    Box(side*3.25f,1.93f,-3.11f,.12f,1.57f,.16f,Gold,false);
                    Box(side*3.25f,1.93f,-3.11f,1.67f,.12f,.16f,Gold,false);
                    Box(side*.96f,.95f,-2.9f,.89f,1.38f,.21f,Honey,false);
                    for(int plank=0;plank<4;plank++)
                        Box(side*(.64f+.21f*plank),1.13f,-3.03f,.17f,1.12f+.16f*plank,.08f,Wood,false);
                }
                Box(-3.5f,.87f,1.3f,2.64f,1.22f,.67f,Wood);
                Box(-3.5f,1.54f,1.3f,2.81f,.12f,.79f,Honey);
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
                    Box(i==0?1.05f:i*2,1.84f,-4.77f,.34f,3.12f,.34f,Red);
                    if(level==1 && i<3) Box(i*2+1,1.12f,-4.77f,2,.16f,.18f,Teal);
                    if(level==1 && i<3)
                    {
                        for(int spindle=1;spindle<4;spindle++)
                            Box(i*2+spindle*.5f,.71f,-4.77f,.085f,.79f,.085f,Wood,false);
                        Beam(new(i*2,2.60f,-4.77f),new(i*2+.68f,3.26f,-4.77f),.18f,.24f,Honey);
                    }
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
            Stairs("Saloon balcony steps",x-5,2.5f,-10.43f,1.65f,3.67f,6.8f);
            Begin("Depot water tank timber frame",-23.8f,2.5f,7.5f);
            foreach(int a in new[]{-1,1}) foreach(int b in new[]{-1,1}) Box(a*1.63f,3.87f,b*1.63f,.53f,7.74f,.53f,Wood);
            for(int s=-1;s<=1;s+=2)
            {
                Beam(new(-1.65f,.4f,s*1.64f),new(1.65f,7.3f,s*1.64f),.26f,.29f,Dark);
                Beam(new(1.65f,.4f,s*1.64f),new(-1.65f,7.3f,s*1.64f),.26f,.29f,Dark);
            }
            Box(0,7.95f,0,4.5f,.42f,4.5f,Wood);
            End();
            Begin("Banded depot cistern",-23.8f,10.66f,7.5f);
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
                        if(Mathf.Abs(col)==2)
                        {
                            float heel=px+Mathf.Sign(col)*.25f;
                            model.Shape(new(heel,1.3f,s*.18f),new(2.59f,2.6f,strip+.32f),Honey,top:.61f);
                        }
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
                    Box(0,2.73f,s*strip*.5f,w,.14f,.10f,Teal,false);
                    for(int k=0;k<8;k++) Box((k-3.5f)*w/8,2.9f,s*strip*.5f,w/8-.045f,.51f,.18f,k%3==0?Shade:Sand,false);
                    End();
                }
            }
            Begin("Pyramid portal vault",x,15.22f,z,-40);
            Box(0,.48f,0,10.4f,.96f,9.35f,Honey);
            foreach(int s in new[]{-1,1})
            {
                Beam(new(-5.32f,-2.70f,s*2.73f),new(-5.32f,.32f,s*.92f),.79f,.82f,Gold);
                Beam(new(-5.64f,-2.66f,s*2.68f),new(-5.64f,.27f,s*.91f),.45f,.54f,Teal);
                Beam(new(5.19f,-2.55f,s*2.62f),new(5.19f,.27f,s*.91f),.44f,.55f,Teal);
                Lamp(-5.15f,-2.75f,s*2.8f);
            }
            Box(-5.43f,.36f,0,1.05f,.48f,2.45f,Gold,false);
            Box(-5.98f,.36f,0,.10f,.23f,2.12f,Teal,false);
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
            var entry=PyramidPoint(0,-12.09f);
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

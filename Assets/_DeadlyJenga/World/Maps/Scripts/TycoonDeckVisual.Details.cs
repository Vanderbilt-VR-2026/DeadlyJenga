using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class TycoonDeckVisual
    {
        static Material cream, peach, mint, coral;
        static Material Cream => cream ? cream : cream = Resources.Load<Material>("Tycoon Cream");
        static Material Peach => peach ? peach : peach = Resources.Load<Material>("Tycoon Peach");
        static Material Mint => mint ? mint : mint = Resources.Load<Material>("Tycoon Mint");
        static Material Coral => coral ? coral : coral = Resources.Load<Material>("Tycoon Coral");
        Vector3 dimensions;

        // Author details in metres on the top face of their movable parent module.
        Vector3 LocalPoint(float x, float y, float z) =>
            new Vector3(x / dimensions.x, .5f + y / dimensions.y, z / dimensions.z);

        void Slab(float x,float y,float z,float w,float h,float d,int material)
        {
            Prism(LocalPoint(x-w/2,y+h,z-d/2), LocalPoint(x+w/2,y+h,z-d/2),
                LocalPoint(x-w/2,y+h,z+d/2), LocalPoint(x+w/2,y+h,z+d/2),h/dimensions.y,material);
        }

        void AddParkDetails(TycoonDeckProfile profile,Vector3 size)
        {
            dimensions=size;
            float w=size.x, d=size.z;
            switch(profile.Finish)
            {
                case TycoonDeckFinish.Peach:
                case TycoonDeckFinish.LoosePeach:
                case TycoonDeckFinish.Mint:
                    // Flush end bands and paired fasteners follow each removable plank.
                    foreach (int end in new[]{-1,1})
                    {
                        float z=end*(d*.5f-.32f);
                        Slab(0,0,z,w-.22f,.018f,.10f,2);
                        foreach (int side in new[]{-1,1})
                            Slab(side*(w*.5f-.25f),0,z-end*.18f,.10f,.018f,.10f,3);
                    }
                    break;
                case TycoonDeckFinish.Tube:
                    Tube(profile, true);
                    break;
                case TycoonDeckFinish.Gate:
                    Tube(profile, false);
                    // Cream portal collars frame each entrance from the beach and from the queue.
                    foreach(float z in new[]{-d*.5f,d*.5f})
                    for(int i=0;i<16;i++)
                    {
                        Vector3 Rim(int k,float r) => LocalPoint(Mathf.Cos(k*Mathf.PI/16)*r,Mathf.Sin(k*Mathf.PI/16)*r,z);
                        var a=Rim(i,w*.50f); var b=Rim(i+1,w*.50f);
                        var c=Rim(i+1,w*.56f); var e=Rim(i,w*.56f);
                        Quad(a,b,c,e,2); Quad(e,c,b,a,2);
                    }
                    break;
                case TycoonDeckFinish.Basin:
                    Slab(0,0,0,w-.52f,.09f,d-.52f,1);
                    // Inlaid pool tiles and drain slots at the ends of the runout.
                    foreach (int end in new[]{-1,1})
                    {
                        float z=end*(d*.5f-.66f);
                        Slab(0,.09f,z,w-.94f,.012f,.18f,2);
                        for(int slot=-3;slot<=3;slot++)
                            Slab(slot*.29f,.102f,z,.14f,.012f,.11f,0);
                    }
                    // A rescue ring hangs on the outer face of each catch basin.
                    Slab(0,-.25f,-d/2-.05f,.26f,.75f,.12f,2);
                    for(int i=0;i<24;i++)
                    {
                        Vector3 Ring(int k,float r) => LocalPoint(Mathf.Cos(k*Mathf.PI/12)*r,.12f+Mathf.Sin(k*Mathf.PI/12)*r,-d/2-.09f);
                        var a=Ring(i,.42f); var b=Ring(i+1,.42f);
                        var c=Ring(i+1,.74f); var e=Ring(i,.74f);
                        Quad(a,b,c,e,(i/3)%2==0?3:2);
                        Quad(e,c,b,a,(i/3)%2==0?3:2);
                    }
                    for(int i=0;i<10;i++)
                        foreach(int side in new[]{-1,1})
                            Slab((i-4.5f)*w/10,0,side*(d-.35f)/2,w/10,.35f,.35f, i%2==0?1:2);
                    float sideLength=d-.70f;
                    for(int i=0;i<6;i++)
                        foreach(int side in new[]{-1,1})
                            Slab(side*(w-.35f)/2,0,(i-2.5f)*sideLength/6,.35f,.35f,sideLength/6,i%2==0?2:1);
                    break;
                case TycoonDeckFinish.Walk:
                    WalkRails(profile,size);
                    break;
                case TycoonDeckFinish.Railed:
                    ArchSpandrels(w,d);
                    // The seaward rail carries a short run of alternating park pennants.
                    for(int i=0;i<7;i++)
                    {
                        float x=(i-3)*w/7;
                        if((profile.Openings & TycoonRailOpening.Front)!=0 && Mathf.Abs(x)<2.7f) continue;
                        var a=LocalPoint(x-.44f,1.12f,-d/2-.07f);
                        var b=LocalPoint(x+.44f,1.12f,-d/2-.07f);
                        var tip=LocalPoint(x,.32f,-d/2-.12f);
                        Quad(a,b,tip,tip,i%2==0?2:1); Quad(b,a,tip,tip,i%2==0?2:1);
                    }
                    foreach(int side in new[]{-1,1})
                    {
                        float z=side*(d-.2f)/2;
                        var opening=side<0 ? TycoonRailOpening.Front : TycoonRailOpening.Rear;
                        if((profile.Openings & opening)==0)
                        {
                            Slab(0,1.15f,z,w,.16f,.15f,2);
                            Slab(0,.55f,z,w,.12f,.12f,2);
                            Slab(0,0,z,.16f,1.32f,.16f,2);
                        }
                        else foreach(int end in new[]{-1,1})
                        {
                            float length=(w-5.4f)*.5f;
                            float mid=end*(w-length)*.5f;
                            Slab(mid,1.15f,z,length,.16f,.15f,2);
                            Slab(mid,.55f,z,length,.12f,.12f,2);
                            Slab(end*2.75f,0,z,.16f,1.32f,.16f,2);
                        }
                        foreach(int end in new[]{-1,1}) Slab(end*(w-.24f)/2,0,z,.24f,1.4f,.24f,2);
                    }
                    break;
                case TycoonDeckFinish.Awning:
                    foreach(int side in new[]{-1,1})
                        foreach(int end in new[]{-1,1})
                            Slab(side*w*.44f,0,end*d*.41f,.16f,3.30f,.16f,2);
                    Slab(0,0,.55f,w*.64f,.16f,1.05f,3);
                    Slab(0,.16f,.55f,w*.56f,.92f,.74f,2);
                    Slab(0,1.08f,.55f,w*.67f,.16f,1.14f,3);
                    // Three flavors in a small counter-top ice pop display.
                    Slab(0,1.24f,.58f,1.65f,.18f,.42f,2);
                    for(int flavor=0;flavor<3;flavor++)
                    {
                        float x=(flavor-1)*.49f;
                        Slab(x,1.42f,.58f,.09f,.18f,.09f,2);
                        Slab(x,1.60f,.58f,.34f,.43f,.19f,flavor+1);
                    }
                    for(int i=0;i<9;i++)
                    {
                        float x=(i-4)*w/9;
                        float h=3.1f;
                        // A scalloped valance gives the kiosk a recognizable silhouette at eye height.
                        for(int j=0;j<5;j++)
                        {
                            float u=j/5f,v=(j+1)/5f;
                            float z=-d*.56f-.02f;
                            Vector3 Hem(float t,float drop) => LocalPoint(x+(t-.5f)*w/9,h-drop,z);
                            var a=Hem(u,0); var b=Hem(v,0);
                            var c=Hem(v,.22f+.24f*Mathf.Sin(Mathf.PI*v));
                            var e=Hem(u,.22f+.24f*Mathf.Sin(Mathf.PI*u));
                            Quad(a,b,c,e,i%2==0?3:2); Quad(e,c,b,a,i%2==0?3:2);
                        }
                        foreach(int end in new[]{-1,1})
                        {
                            var a=LocalPoint(x-w/18,h,end*d*.56f);
                            var b=LocalPoint(x+w/18,h,end*d*.56f);
                            var c=LocalPoint(x-w/18,h+.8f,0);
                            var e=LocalPoint(x+w/18,h+.8f,0);
                            if(end<0) Prism(a,b,c,e,.14f/size.y,i%2==0?3:2);
                            else Prism(c,e,a,b,.14f/size.y,i%2==0?3:2);
                        }
                    }
                    break;
                case TycoonDeckFinish.Parasol:
                    Slab(0,0,0,.18f,3.8f,.18f,2);
                    for(int i=0;i<16;i++)
                        Wedge(i,16,0,3.8f,2.25f,3.1f,i%2==0?3:2);
                    break;
                case TycoonDeckFinish.Launch:
                    // Color lanes share one queue apron. Arrows face the slide entrances.
                    for(int lane=0;lane<4;lane++)
                    {
                        float x=-8.4f+lane*5.6f;
                        for(int j=0;j<2;j++)
                        {
                            float z=-2+j*2.4f;
                            var tip=LocalPoint(x,.03f,z-.65f);
                            var left=LocalPoint(x-.85f,.03f,z+.24f);
                            var notch=LocalPoint(x,.03f,z-.18f);
                            var right=LocalPoint(x+.85f,.03f,z+.24f);
                            Quad(tip,left,notch,right,2);
                            Quad(right,notch,left,tip,2);
                        }
                    }
                    break;
                case TycoonDeckFinish.Bucket:
                    SummitSails();
                    foreach (int side in new[]{-1,1})
                    {
                        Slab(side*4.3f,0,0,.65f,6.3f,.85f,0);
                        Slab(side*3.95f,5.8f,0,1.2f,.65f,.8f,2);
                        Slab(side*4.3f,0,0,1.4f,.45f,2.7f,2);
                    }
                    break;
            }
        }

        TycoonDeckVisual Detail(Transform parent,string label)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(parent,false);
            var visual = part.AddComponent<TycoonDeckVisual>();
            visual.dimensions = Vector3.one;
            return visual;
        }

        void BuildTippingBucket(Vector3 size,Material[] palette)
        {
            var frame = new GameObject("Summit bucket axle").transform;
            frame.SetParent(transform,false);
            frame.localPosition = LocalPoint(0,6.1f,0);
            frame.localScale = new Vector3(1/size.x,1/size.y,1/size.z);
            var bowl = Detail(frame,"Striped tipping bucket");
            bowl.MakeBowl();
            // Place the trunnions near the upper half of the vessel.
            for(int i=0;i<bowl.vertices.Count;i++) bowl.vertices[i] -= Vector3.up*3.8f;
            bowl.FinishMesh(palette);
            var spill = Detail(frame,"Pouring water");
            // Continuous blue sheet, broken into tapering foam fingers at its lower edge.
            for(int i=0;i<9;i++)
            {
                float x=(i-4)*.33f;
                Vector3 At(float t,float u) => new Vector3(x+u*(1+.35f*t),-t*(6.1f+.5f*Mathf.Cos(i*2)),-2.4f*t);
                for(int j=0;j<4;j++)
                {
                    float t=j/4f, end=(j+1)/4f;
                    var a=At(t,-.17f); var b=At(t,.17f);
                    var c=At(end,.17f); var d=At(end,-.17f);
                    int color=j==3 && i%3==0 ? 2 : 1;
                    spill.Quad(a,b,c,d,color); spill.Quad(d,c,b,a,color);
                }
            }
            spill.FinishMesh(palette);
            frame.gameObject.AddComponent<TycoonBucketMotion>().Initialize(bowl.transform,spill.transform);
        }

        void SummitSails()
        {
            // Twin striped fabric wings leave the tipping bucket visible between them.
            for(int side=-1;side<=1;side+=2)
            {
                Slab(side*11,0,1.3f,.32f,7.7f,.32f,2);
                Slab(side*5,0,1.3f,.25f,4.75f,.25f,2);
                Slab(side*10,0,-1.65f,.25f,4.7f,.25f,2);
                Slab(side*7.2f,0,-1.65f,.25f,3.75f,.25f,2);
                Vector3 Cloth(float u,float v)
                {
                    float outer=11-v, inner=5+v*2.2f;
                    float height=Mathf.Lerp(4.75f-v,7.7f-3*v,u)-.65f*Mathf.Sin(Mathf.PI*u);
                    return LocalPoint(side*Mathf.Lerp(inner,outer,u),height,1.3f-2.95f*v);
                }
                for(int i=0;i<9;i++)
                {
                    float a=i/9f,b=(i+1)/9f;
                    var n=Cloth(a,0);var f=Cloth(b,0);var e=Cloth(b,1);var t=Cloth(a,1);
                    int mat=i%2==0?2:3;
                    Quad(n,f,e,t,mat);Quad(t,e,f,n,mat);
                }
                Slab(side*11,7.7f,1.3f,.14f,1.65f,.14f,2);
                var hi=LocalPoint(side*11,9.3f,1.3f);
                var low=LocalPoint(side*11,8.2f,1.3f);
                var flag=LocalPoint(side*13,8.85f,1.1f);
                Quad(hi,low,flag,flag,1);Quad(low,hi,flag,flag,1);
            }
        }

        void WalkRails(TycoonDeckProfile p, Vector3 size)
        {
            Vector3 Surface(float across,float along) => Vector3.Lerp(
                Vector3.Lerp(p.StartLeft,p.StartRight,across), Vector3.Lerp(p.EndLeft,p.EndRight,across),along);
            int count=Mathf.Max(1,Mathf.CeilToInt(size.z/1.6f));
            for(int j=0;j<count;j++)
            {
                float t=(j+.45f)/count, half=Mathf.Min(.04f,.05f/size.z);
                Prism(Surface(0,t-half)+vertical*.025f,Surface(1,t-half)+vertical*.025f,
                    Surface(0,t+half)+vertical*.025f,Surface(1,t+half)+vertical*.025f,.022f/size.y,2);
            }
            foreach(int side in new[]{0,1})
            {
                float mid=side==0?.13f/size.x:1-.13f/size.x, half=.075f/size.x;
                foreach(float h in new[]{.66f,1.28f})
                    Prism(Surface(mid-half,0)+vertical*h,Surface(mid+half,0)+vertical*h,
                        Surface(mid-half,1)+vertical*h,Surface(mid+half,1)+vertical*h,.13f/size.y,2);
                for(int j=0;j<=4;j++)
                {
                    float t=.015f+.97f*j/4;
                    var q=Surface(mid,t);
                    var dx=Vector3.right*.085f/size.x; var dz=Vector3.forward*.085f/size.z;
                    var top=q+vertical*1.28f;
                    Quad(q-dx-dz,top-dx-dz,top+dx-dz,q+dx-dz,2);
                    Quad(q+dx+dz,top+dx+dz,top-dx+dz,q-dx+dz,2);
                    Quad(q-dx+dz,top-dx+dz,top-dx-dz,q-dx-dz,2);
                    Quad(q+dx-dz,top+dx-dz,top+dx+dz,q+dx+dz,2);
                }
            }
        }

        void MakeBowl()
        {
            // Flared enamel bucket with alternating stripes and a curved bail.
            for(int i=0;i<32;i++)
            {
                Wedge(i,32,2.2f,0,3.6f,5.2f,(i/2)%2==0?3:2);
                Wedge(i,32,0,0,2.2f,0,2);
                Wedge(i,32,2.22f,.08f,2.34f,.46f,2);
                Wedge(i,32,3.62f,5.22f,3.38f,5.22f,2);
                Wedge(i,32,3.38f,5.22f,0,4.85f,1);
            }
            for(int i=0;i<20;i++)
            {
                float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;
                Vector3 Handle(float t,float r) => LocalPoint(Mathf.Cos(t)*r,4+Mathf.Sin(t)*r,0);
                var v=Handle(a,3.72f); var q=Handle(b,3.72f);
                var r=Handle(b,3.4f); var t=Handle(a,3.4f);
                Quad(v,q,r,t,2); Quad(t,r,q,v,2);
            }
        }

        void ArchSpandrels(float w,float d)
        {
            // Each module carries two half arches above its pier.
            for(int side=-1;side<=1;side+=2)
            for(int j=0;j<7;j++)
            {
                float x0=side*w*.5f*j/7, x1=side*w*.5f*(j+1)/7;
                float Drop(float x)
                {
                    float u=Mathf.Clamp01((Mathf.Abs(x)-1.5f)/(w*.5f-1.5f));
                    return -2.62f+1.86f*Mathf.Sqrt(u*(2-u));
                }
                var a=LocalPoint(x0,Drop(x0),-d*.505f);var b=LocalPoint(x1,Drop(x1),-d*.505f);
                var c=LocalPoint(x1,-.5f,-d*.505f);var e=LocalPoint(x0,-.5f,-d*.505f);
                Vector3 back=new Vector3(0,0,1.01f);
                Quad(a,b,c,e,0);Quad(e,c,b,a,0);
                Quad(e+back,c+back,b+back,a+back,0);Quad(a+back,b+back,c+back,e+back,0);
                Quad(a,a+back,b+back,b,0);Quad(b,b+back,a+back,a,0);
            }
        }

        void Wedge(int i,int count,float low,float y0,float high,float y1,int material)
        {
            Vector3 At(int k,float r,float y)
            {
                float angle=2*Mathf.PI*k/count;
                return LocalPoint(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
            }
            var a=At(i,low,y0);var b=At(i,high,y1);
            var c=At(i+1,high,y1);var d=At(i+1,low,y0);
            Quad(a,b,c,d,material);
            Quad(d,c,b,a,material);
        }

        void Tube(TycoonDeckProfile profile,bool enclosed)
        {
            Vector3 Arch(Vector3 left,Vector3 right,int i)
            {
                float angle=i*Mathf.PI/12;
                return Vector3.Lerp(left,right,(1-Mathf.Cos(angle))/2)
                    +vertical*(Mathf.Sin(angle)*dimensions.x*.50f);
            }
            for(int i=0;i<12;i++)
            {
                var a=Arch(profile.StartLeft,profile.StartRight,i);
                var b=Arch(profile.StartLeft,profile.StartRight,i+1);
                var c=Arch(profile.EndLeft,profile.EndRight,i+1);
                var d=Arch(profile.EndLeft,profile.EndRight,i);
                int material=0;
                Quad(a,b,c,d,material);Quad(d,c,b,a,material);
            }
        }
    }
}

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
                case TycoonDeckFinish.Tube:
                    Tube(profile, true);
                    break;
                case TycoonDeckFinish.Gate:
                    Tube(profile, false);
                    break;
                case TycoonDeckFinish.Basin:
                    Slab(0,0,0,w,.09f,d,1);
                    for(int i=0;i<10;i++)
                        foreach(int side in new[]{-1,1})
                            Slab((i-4.5f)*w/10,0,side*(d-.35f)/2,w/10,.35f,.35f, i%2==0?1:2);
                    break;
                case TycoonDeckFinish.Railed:
                    foreach(int side in new[]{-1,1})
                    {
                        float z=side*(d-.2f)/2;
                        Slab(0,1.15f,z,w,.16f,.15f,2);
                        Slab(0,.55f,z,w,.12f,.12f,2);
                        foreach(int end in new[]{-1,1}) Slab(end*(w-.24f)/2,0,z,.24f,1.4f,.24f,2);
                    }
                    break;
                case TycoonDeckFinish.Awning:
                    foreach(int side in new[]{-1,1})
                        foreach(int end in new[]{-1,1})
                            Slab(side*w*.44f,0,end*d*.41f,.16f,3.1f,.16f,2);
                    for(int i=0;i<9;i++)
                    {
                        float x=(i-4)*w/9;
                        float h=3.1f;
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
                    Slab(0,0,0,.18f,3.6f,.18f,2);
                    for(int i=0;i<16;i++)
                        Wedge(i,16,0,3.8f,2.25f,3.1f,i%2==0?3:2);
                    break;
                case TycoonDeckFinish.Bucket:
                    // Flared enamel bucket with alternating stripes and a curved bail.
                    for(int i=0;i<32;i++)
                    {
                        Wedge(i,32,2.2f,0,3.6f,5.2f,(i/2)%2==0?3:2);
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
                    break;
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
                    +Vector3.up*(Mathf.Sin(angle)*dimensions.x*.50f/dimensions.y);
            }
            for(int i=0;i<12;i++)
            {
                var a=Arch(profile.StartLeft,profile.StartRight,i);
                var b=Arch(profile.StartLeft,profile.StartRight,i+1);
                var c=Arch(profile.EndLeft,profile.EndRight,i+1);
                var d=Arch(profile.EndLeft,profile.EndRight,i);
                int material=enclosed?0:2;
                Quad(a,b,c,d,material);Quad(d,c,b,a,material);
            }
        }
    }
}

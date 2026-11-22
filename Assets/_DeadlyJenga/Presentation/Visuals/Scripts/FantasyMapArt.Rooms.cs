using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        static readonly Color Walnut=new(.30f,.15f,.085f), Oak=new(.64f,.42f,.22f),
            Linen=new(.95f,.84f,.62f), Ink=new(.075f,.13f,.16f), Jade=new(.12f,.43f,.35f),
            Terracotta=new(.78f,.27f,.14f), Denim=new(.15f,.33f,.50f), Brass=new(.83f,.58f,.23f);

        // Local-coordinate furniture groups are baked into the moving floor's art mesh.
        readonly struct Room
        {
            public readonly ArtMesh mesh;
            public readonly Quaternion q;
            readonly Vector3 origin;
            public Room(ArtMesh mesh,Vector3 origin,Quaternion q){this.mesh=mesh;this.origin=origin;this.q=q;}
            public Vector3 At(float x,float y,float z)=>origin+q*new Vector3(x,y,z);
            public Room Place(float x,float y,float z,float yaw=0)=>new(mesh,At(x,y,z),q*Quaternion.Euler(0,yaw,0));
            public void Box(float x,float y,float z,float w,float h,float d,Color color)=>
                mesh.Box(At(x,y,z),new Vector3(w,h,d),q,0,color);
            public void Tube(float x,float y,float z,float r,float h,Color color)=>
                mesh.Cylinder(At(x,y,z),q,r,h,0,color,12);
            public void Round(float x,float y,float z,float w,float h,float d,Color color)=>
                mesh.Sphere(At(x,y,z),new Vector3(w,h,d),0,color,5,10);
            public void Pipe(Vector3 a,Vector3 b,float radius,Color color)
            {
                Vector3 delta=q*(b-a);
                mesh.Cylinder(origin+q*a,Quaternion.FromToRotation(Vector3.up,delta),radius,delta.magnitude,0,color,12);
            }
        }

        static void WindowOpening(ArtMesh mesh,Vector3 center,Quaternion rotation,float width,float height,Color trim)
        {
            var r=new Room(mesh,center,rotation);
            for(int side=-1;side<=1;side+=2)
            {
                r.Box(side*width*.5f,0,0,.19f,height+.28f,.32f,trim);
                r.Box(0,side*height*.5f,0,width,.18f,.34f,trim);
            }
            r.Box(0,-height*.5f-.12f,.12f,width+.56f,.22f,.77f,Oak);
        }

        static void Switchgear(ArtMesh mesh,Vector3 face,Quaternion q,float scale)
        {
            for(int i=-1;i<=1;i++)
            {
                var r=new Room(mesh,face+q*new Vector3(i*scale,0,0),q);
                r.Box(0,0,.08f*scale,.91f*scale,1.63f*scale,.24f*scale,Ink);
                r.Box(0,0,.21f*scale,.79f*scale,1.48f*scale,.12f*scale,new Color(.32f,.46f,.47f));
                r.mesh.Cylinder(r.At(0,.38f*scale,.27f*scale),q*Quaternion.Euler(90,0,0),.26f*scale,.09f*scale,0,Linen,18);
                r.Pipe(new Vector3(0,.38f,.38f)*scale,new Vector3(.14f,.51f,.38f)*scale,.022f*scale,Ink);
                r.Box(0,-.19f*scale,.31f*scale,.16f*scale,.43f*scale,.16f*scale,Terracotta);
                for(int slot=0;slot<3;slot++)r.Box(0,(-.49f-slot*.075f)*scale,.28f*scale,.45f*scale,.03f*scale,.035f*scale,Ink);
            }
        }

        static void FurnishNeighborhood(ArtMesh mesh,Transform visual,Rigidbody body,string site,string purpose)
        {
            var t=body.transform;
            var r=new Room(mesh,visual.InverseTransformPoint(t.position),Quaternion.Inverse(visual.rotation)*t.rotation);
            if(site.StartsWith("Sugar Market") && purpose.Contains(" floor "))
            {
                float w=site=="Sugar Market hall"?20:24;
                float depth=site=="Sugar Market hall"?22:24;
                bool library=site=="Sugar Market west annex" || purpose.EndsWith("floor 2");
                foreach(int side in new[]{-1,1})
                {
                    foreach(float z in new[]{1.4f,depth*.27f})
                    {
                        var desk=r.Place(side*w*.26f,0,z);
                        desk.Box(0,.99f,0,2.9f,.12f,1.25f,Oak);
                        desk.Box(-.51f,1.10f,.09f,.68f,.10f,.49f,Denim);
                        desk.Box(-.51f,1.16f,.09f,.63f,.035f,.45f,Linen);
                        desk.Box(.35f,.59f,0,.97f,.21f,.82f,Walnut);
                        Chair(desk.Place(.24f,0,-1.13f),side<0?Jade:Terracotta);
                    }
                    var wall=r.Place(side*(w*.5f-1.65f),0,2.4f,-side*90);
                    if(library || side<0) Books(wall);
                    else
                    {
                        wall.Box(0,1.92f,0,4.3f,2.02f,.20f,Oak);
                        wall.Box(0,1.92f,.12f,4.06f,1.78f,.08f,new Color(.06f,.20f,.16f));
                        wall.Box(0,.93f,.23f,4.45f,.12f,.42f,Walnut);
                        wall.Box(-.7f,1.03f,.27f,.32f,.08f,.09f,Linen);
                    }
                }
                Pendant(r.Place(0,0,4.1f),3.16f,Jade);
            }
            else if(site.StartsWith("Cloud Harbor") && purpose.Contains(" floor "))
            {
                float w=site=="Cloud Harbor block"?28:24;
                bool grocery=site=="Cloud Harbor block", cafe=site=="Cloud Harbor west annex";
                foreach(int side in new[]{-1,1})
                {
                    var island=r.Place(side*w*.26f,0,1.4f);
                    island.Box(0,.65f,0,3.46f,1.13f,1.61f,cafe?Jade:Walnut);
                    island.Box(0,1.26f,0,3.62f,.14f,1.73f,cafe?Linen:Oak);
                    if(grocery)
                    {
                        Produce(island.Place(0,1.35f,0),side);
                        Produce(r.Place(side*4,1.10f,8.15f),-side);
                    }
                    else if(cafe)
                    {
                        island.Box(-.62f,1.67f,0,1.09f,.70f,.69f,Ink);
                        island.Box(-.62f,1.9f,.34f,.85f,.21f,.18f,Brass);
                        for(int cup=0;cup<3;cup++)
                        {
                            island.Tube(.2f+cup*.33f,1.34f,.16f,.125f,.21f,Linen);
                            island.Tube(.2f+cup*.33f,1.54f,.16f,.095f,.015f,Walnut);
                        }
                    }
                    else island.Box(0,1.62f,0,2.2f,.58f,.8f,Brass);
                    var wall=r.Place(side*(w*.5f-1.55f),0,-.4f,-side*90);
                    if(grocery) StoreShelf(wall);
                    if(cafe){Booth(wall);Pendant(wall.Place(0,0,1.7f),3.17f,Terracotta);}
                }
            }
            else if(site.StartsWith("Grape") && purpose=="Independent bookfall support spine")
            {
                for(int level=0;level<3;level++)
                {
                    float y=level*4;
                    var sitting=r.Place(0,y,9);
                    sitting.Box(0,.035f,0,7.4f,.06f,6.1f,Denim);
                    sitting.Box(0,.071f,0,6.98f,.025f,5.68f,new Color(.47f,.61f,.58f));
                    Booth(sitting.Place(0,0,-1.9f));
                    var table=sitting.Place(0,0,1);
                    table.Tube(0,0,0,.28f,.67f,Walnut);
                    table.Tube(0,.67f,0,1.08f,.14f,Oak);
                    table.Tube(0,.81f,0,.19f,.39f,Terracotta);
                    table.Round(0,1.3f,0,.33f,.34f,.31f,Jade);
                    Books(r.Place(5.4f,y,14,-90));
                    var bed=r.Place(-4.4f,y,21);
                    bed.Box(0,.39f,0,3.14f,.78f,4.88f,Walnut);
                    bed.Box(0,.86f,0,3.10f,.23f,4.77f,Linen);
                    bed.Box(0,1.04f,-.58f,3.14f,.18f,3.55f,level%2==0?Denim:Jade);
                    bed.Box(0,.95f,2.4f,3.45f,1.9f,.22f,Oak);
                    bed.Round(0,1.12f,1.52f,1.13f,.21f,.56f,Linen);
                    Pendant(sitting,3.18f,Brass);
                }
            }
            else if(site.StartsWith("Canyon") && purpose.EndsWith(" floor 1"))
            {
                var pump=r.Place(0,0,site.Contains("south")?6.1f:4.3f);
                pump.Box(0,.22f,0,6,.44f,2.1f,Ink);
                pump.Pipe(new Vector3(-2,1.22f,0),new Vector3(1.4f,1.22f,0),.75f,Jade);
                for(int rib=0;rib<5;rib++)
                    pump.Pipe(new Vector3(-1.88f+rib*.4f,1.22f,0),new Vector3(-1.78f+rib*.4f,1.22f,0),.82f,Brass);
                pump.Round(1.55f,1.22f,0,.95f,1.05f,.86f,new Color(.15f,.56f,.64f));
                pump.Pipe(new Vector3(1.55f,1.68f,0),new Vector3(1.55f,2.95f,0),.34f,Brass);
                pump.Pipe(new Vector3(1.55f,2.95f,0),new Vector3(3.7f,2.95f,0),.34f,Brass);
                pump.Round(1.55f,2.95f,0,.37f,.37f,.37f,Brass);
            }
        }

        static void Chair(Room r,Color fabric)
        {
            r.Box(0,.50f,0,.94f,.17f,.91f,fabric);
            r.Box(0,.90f,-.40f,.94f,.70f,.13f,fabric);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                r.Box(x*.34f,.22f,z*.31f,.09f,.44f,.09f,Walnut);
        }
        static void Booth(Room r)
        {
            r.Box(0,.28f,0,3.9f,.56f,1.45f,Walnut);
            r.Box(0,.91f,-.56f,3.84f,1.24f,.33f,Jade);
            for(int cushion=-1;cushion<=1;cushion++)
                r.Box(cushion*1.17f,.67f,.08f,1.12f,.29f,1.13f,new Color(.30f,.53f,.43f));
            foreach(int end in new[]{-1,1}) r.Box(end*1.88f,.71f,0,.22f,.55f,1.48f,Oak);
        }
        static void Books(Room r)
        {
            r.Box(0,1.26f,-.32f,3.7f,2.52f,.18f,Walnut);
            foreach(int end in new[]{-1,1})r.Box(end*1.86f,1.26f,0,.13f,2.52f,.77f,Oak);
            var jackets=new[]{Jade,Denim,Terracotta,Linen};
            for(int shelf=0;shelf<4;shelf++)
            {
                float y=.14f+shelf*.68f;
                r.Box(0,y,0,3.75f,.11f,.77f,Oak);
                for(int book=0;book<7;book++)
                {
                    float h=.34f+(book*3+shelf)%4*.068f;
                    float x=-1.55f+book*.42f;
                    r.Box(x,y+.055f+h*.5f,.075f,.25f,h,.49f,jackets[(shelf+book)%4]);
                    r.Box(x,y+.16f,.33f,.22f,.034f,.025f,Linen);
                }
            }
        }
        static void StoreShelf(Room r)
        {
            r.Box(0,1.35f,-.40f,4.6f,2.7f,.22f,Jade);
            foreach(int end in new[]{-1,1})r.Box(end*2.25f,1.35f,0,.14f,2.7f,.93f,Oak);
            for(int shelf=0;shelf<3;shelf++)
            {
                float y=.36f+shelf*.79f;
                r.Box(0,y,0,4.6f,.14f,.94f,Oak);
                for(int jar=0;jar<7;jar++)
                {
                    var color=shelf==0?Terracotta:shelf==1?Brass:Jade;
                    float x=-1.82f+jar*.60f;
                    r.Tube(x,y+.08f,.10f,.20f,.44f,color);
                    r.Tube(x,y+.48f,.10f,.205f,.065f,Linen);
                }
            }
        }
        static void Produce(Room r,int side)
        {
            foreach(int edge in new[]{-1,1})
            {
                r.Box(edge*1.48f,.19f,0,.10f,.38f,1.37f,Oak);
                r.Box(0,.13f,edge*.65f,2.95f,.26f,.10f,Oak);
            }
            for(int row=0;row<2;row++)for(int col=0;col<6;col++)
            {
                float x=-1.18f+col*.47f,z=-.27f+row*.52f;
                r.Round(x,.26f,z,.245f,.24f,.24f,side<0?Terracotta:new Color(.58f,.69f,.19f));
                r.Box(x,.5f,z,.045f,.09f,.045f,Walnut);
            }
        }
        static void Pendant(Room r,float ceiling,Color enamel)
        {
            r.Tube(0,ceiling-.65f,0,.04f,.65f,Ink);
            r.mesh.Cone(r.At(0,ceiling-.99f,0),r.q,.55f,.37f,0,enamel,14);
            r.Round(0,ceiling-1.02f,0,.27f,.15f,.27f,Linen);
        }
    }
}

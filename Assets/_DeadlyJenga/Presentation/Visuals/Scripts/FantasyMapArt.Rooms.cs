using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        static readonly Color Walnut=new(.30f,.15f,.085f), Oak=new(.64f,.42f,.22f),
            Linen=new(.95f,.84f,.62f), Ink=new(.075f,.13f,.16f), Jade=new(.12f,.43f,.35f),
            Terracotta=new(.78f,.27f,.14f), Denim=new(.15f,.33f,.50f), Brass=new(.83f,.58f,.23f);

        // The furnishing shader reads the otherwise unused vertex alpha as a finish tag.
        // Upholstery and stained boards can share the floor's single furniture draw.
        static Color Fabric(Color tint){tint.a=.50f;return tint;}
        static Color Veneer(Color tint){tint.a=.75f;return tint;}

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
            public void EdgedTop(float x,float y,float z,float w,float h,float d,Color color)
            {
                float bevel=Mathf.Min(.055f,h*.26f);
                var c=At(x,y,z);
                mesh.Tabletop(c,q,new Vector3(w,h,d),bevel,color);
            }
            public void Tube(float x,float y,float z,float r,float h,Color color)=>
                mesh.Cylinder(At(x,y,z),q,r,h,0,color,12);
            public void Round(float x,float y,float z,float w,float h,float d,Color color)=>
                mesh.Sphere(At(x,y,z),new Vector3(w,h,d),0,color,5,10,q);
            public void Pipe(Vector3 a,Vector3 b,float radius,Color color)
            {
                Vector3 delta=q*(b-a);
                mesh.Cylinder(origin+q*a,Quaternion.FromToRotation(Vector3.up,delta),radius,delta.magnitude,0,color,12);
            }
        }

        sealed partial class ArtMesh
        {
            // Inset top and bottom faces leave a narrow chamfer that catches window light.
            public void Tabletop(Vector3 center,Quaternion q,Vector3 size,float bevel,Color color)
            {
                Vector3 Corner(int index,float height,float inset)
                {
                    float x=index==0||index==3?-1:1;
                    float z=index<2?-1:1;
                    return center+q*new Vector3(x*(size.x*.5f-inset),height,z*(size.z*.5f-inset));
                }
                float y=size.y*.5f;
                Quad(Corner(3,y,bevel),Corner(2,y,bevel),Corner(1,y,bevel),Corner(0,y,bevel),0,color);
                Quad(Corner(0,-y,bevel),Corner(1,-y,bevel),Corner(2,-y,bevel),Corner(3,-y,bevel),0,color);
                for(int i=0;i<4;i++)
                {
                    int j=(i+1)%4;
                    Quad(Corner(i,-y,bevel),Corner(i,-y+bevel,0),Corner(j,-y+bevel,0),Corner(j,-y,bevel),0,color);
                    Quad(Corner(i,-y+bevel,0),Corner(i,y-bevel,0),Corner(j,y-bevel,0),Corner(j,-y+bevel,0),0,color);
                    Quad(Corner(i,y-bevel,0),Corner(i,y,bevel),Corner(j,y,bevel),Corner(j,y-bevel,0),0,color);
                }
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
            DressFixedStations(r,site,purpose);
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
                        desk.EdgedTop(0,.99f,0,2.9f,.12f,1.25f,Veneer(Oak));
                        desk.Box(-.51f,1.10f,.09f,.68f,.10f,.49f,Denim);
                        desk.Box(-.51f,1.16f,.09f,.63f,.035f,.45f,Linen);
                        desk.Box(.35f,.80f,0,.97f,.26f,.82f,Walnut);
                        desk.Box(.35f,.80f,-.43f,.27f,.045f,.07f,Brass);
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
                Pendant(r.Place(0,0,4.1f),3.30f,Jade);
            }
            else if(site.StartsWith("Cloud Harbor") && purpose.Contains(" floor "))
            {
                float w=site=="Cloud Harbor block"?28:24;
                bool grocery=site=="Cloud Harbor block", cafe=site=="Cloud Harbor west annex";
                foreach(int side in new[]{-1,1})
                {
                    var island=r.Place(side*w*.26f,0,1.4f);
                    island.Box(0,.65f,0,3.46f,1.13f,1.61f,cafe?Jade:Walnut);
                    island.EdgedTop(0,1.26f,0,3.62f,.14f,1.73f,cafe?Linen:Oak);
                    if(grocery)
                    {
                        Produce(island.Place(0,1.35f,0),side);
                        if(side>0)
                        {
                            var till=island.Place(1.15f,1.33f,-.42f);
                            till.Box(0,.12f,0,.53f,.24f,.44f,Ink);
                            till.Box(0,.31f,-.12f,.48f,.27f,.13f,Denim);
                            for(int key=0;key<3;key++) till.Box(key*.11f-.11f,.25f,.09f,.073f,.03f,.07f,Linen);
                        }
                    }
                    else if(cafe)
                    {
                        island.Box(-.62f,1.67f,0,1.09f,.70f,.69f,Ink);
                        island.Box(-.62f,1.9f,.34f,.85f,.21f,.18f,Brass);
                        for(int cup=0;cup<3;cup++)
                        {
                            CoffeeCup(island.Place(.19f+cup*.39f,1.33f,.16f));
                        }
                    }
                    else
                    {
                        island.Box(0,1.38f,0,3.13f,.10f,1.24f,Oak);
                        for(int loaf=-2;loaf<=2;loaf++)
                        {
                            island.Round(loaf*.56f,1.58f,.04f,.23f,.19f,.48f,new Color(.79f,.48f,.19f));
                            for(int score=-1;score<=1;score++)
                                island.Box(loaf*.56f,1.754f,.04f+score*.17f,.19f,.025f,.04f,Linen);
                        }
                    }
                    var wall=r.Place(side*(w*.5f-1.55f),0,-.4f,-side*90);
                    if(grocery) StoreShelf(wall);
                    else if(!cafe)
                    {
                        wall.Box(0,.70f,0,3.7f,1.4f,1.40f,Terracotta);
                        wall.Box(0,.72f,.72f,2.66f,.87f,.14f,Ink);
                        wall.Box(0,.24f,.84f,3.04f,.12f,.54f,Brass);
                        wall.Box(0,1.44f,0,3.85f,.15f,1.48f,Linen);
                        wall.Tube(-1.23f,1.5f,-.3f,.26f,1.66f,Ink);
                    }
                    if(cafe)
                    {
                        Booth(wall);
                        var table=wall.Place(0,0,1.75f);
                        table.Tube(0,0,0,.43f,.10f,Ink);
                        table.Tube(0,.1f,0,.14f,.70f,Brass);
                        table.Tube(0,.8f,0,.88f,.13f,Oak);
                        CoffeeCup(table.Place(-.28f,.93f,0,-25));
                        CoffeeCup(table.Place(.27f,.93f,.18f,120));
                        table.Box(.19f,.949f,-.37f,.44f,.035f,.28f,Terracotta);
                        Pendant(table,3.30f,Terracotta);
                    }
                }
            }
            else if(site.StartsWith("Grape") && purpose=="Independent bookfall support spine")
            {
                for(int level=0;level<3;level++)
                {
                    float y=level*4;
                    var sitting=r.Place(0,y,9);
                    sitting.Box(0,.035f,0,7.4f,.06f,6.1f,Denim);
                    sitting.Box(0,.071f,0,6.98f,.025f,5.68f,Fabric(new Color(.47f,.61f,.58f)));
                    Booth(sitting.Place(0,0,-1.9f));
                    var table=sitting.Place(0,0,1);
                    table.Tube(0,0,0,.28f,.67f,Walnut);
                    table.Tube(0,.67f,0,1.08f,.14f,Oak);
                    table.Tube(0,.81f,0,.19f,.39f,Terracotta);
                    table.Round(0,1.3f,0,.33f,.34f,.31f,Jade);
                    Books(r.Place(5.4f,y,14,-90));
                    var bed=r.Place(-4.4f,y,21);
                    bed.Box(0,.39f,0,3.14f,.78f,4.88f,Walnut);
                    bed.EdgedTop(0,.86f,0,3.10f,.23f,4.77f,Linen);
                    bed.EdgedTop(0,1.04f,-.58f,3.14f,.18f,3.55f,Fabric(level%2==0?Denim:Jade));
                    bed.Box(0,.95f,2.4f,3.45f,1.9f,.22f,Oak);
                    bed.Round(0,1.12f,1.52f,1.13f,.21f,.56f,Fabric(Linen));
                    var kitchen=r.Place(-5.5f,y,-8,90);
                    Kitchen(kitchen);
                    var entry=Cabinet(r.Place(-4.6f,y,-18.2f),level==1?Denim:Jade);
                    ReadingLamp(entry.Place(-1.03f,0,-.12f));
                    entry.Round(.85f,.08f,.11f,.28f,.08f,.20f,Brass);
                    entry.Box(.62f,.17f,.12f,.18f,.035f,.07f,Ink);
                    for(int hook=0;hook<3;hook++)
                    {
                        float x=-.80f+hook*.74f;
                        entry.Box(x,.71f,-.56f,.69f,1.42f,.08f,Oak);
                        entry.Box(x,1.20f,-.42f,.09f,.12f,.23f,Brass);
                        if(hook!=1)entry.Box(x,.83f,-.34f,.36f,.63f,.18f,hook==0?Denim:Terracotta);
                    }
                    Pendant(sitting,3.30f,Brass);
                }
            }
            else if(site.StartsWith("Canyon north") && purpose.Contains(" floor ") && !purpose.EndsWith(" floor 1"))
            {
                FilterBank(r.Place(5.8f,0,3.2f,-90));
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

        // The cabinet cores are box shapes in EpicCityLayout.PlankWalls.
        // Dress their surfaces in floor-local space so everything folds or falls together.
        static Room Cabinet(Room r,Color enamel)
        {
            r.EdgedTop(0,1.12f,0,3.82f,.12f,1.39f,Veneer(Oak));
            for(int door=-1;door<=1;door++)
            {
                r.Box(door*1.18f,.56f,.65f,1.13f,.91f,.08f,enamel);
                r.Box(door*1.18f,.88f,.71f,.39f,.06f,.08f,Brass);
            }
            return r.Place(0,1.18f,0);
        }

        static void DressFixedStations(Room r,string site,string purpose)
        {
            if(purpose=="Hinged inhabited book wing")
            {
                for(int level=0;level<=2;level++)
                {
                    float deck=level==0?.20f:level*4;
                    var top=Cabinet(r.Place(-4.8f,deck,4.5f),level==1?Denim:Jade);
                    if(level==0) ReadingLamp(top); else if(level==1) RecordPlayer(top); else SewingTable(top);
                    Booth(r.Place(4.8f,deck,4.5f));
                    Pendant(r.Place(4.8f,deck,4.5f),level==0?3.1f:3.3f,Brass);
                }
                return;
            }
            if(purpose=="Two-floor dropping cassette" || purpose=="Escape spine")
            {
                if(purpose=="Escape spine")
                    InstrumentBench(Cabinet(r.Place(-4.5f,10,1.8f,90),Denim),false);
                else for(int level=0;level<2;level++)
                    InstrumentBench(Cabinet(r.Place(-4.8f,level*4,4.4f),Ink),level==0);
                return;
            }
            int marker=purpose.LastIndexOf(" floor ",System.StringComparison.Ordinal);
            if(marker<0 || !int.TryParse(purpose.Substring(marker+7),out int floor)) return;
            if(site.StartsWith("Canyon"))
            {
                bool gallery=site.StartsWith("Canyon south");
                var station=gallery?r.Place(-6.65f,4,9.26f,180):r.Place(-6.5f,0,5.4f,90);
                var top=Cabinet(station,Jade);
                if((floor%2)==0) InstrumentBench(top,false); else SampleRack(top);
                return;
            }
            bool school=site.StartsWith("Sugar Market");
            if(!school && !site.StartsWith("Cloud Harbor")) return;
            float w=site=="Sugar Market hall"?20:site=="Cloud Harbor block"?28:24;
            var counter=Cabinet(r.Place(-w*.5f+2.1f,0,7.2f,90),school?Denim:Terracotta);
            if(school)
            {
                if(site.Contains("west") || floor%3==0)
                {
                    for(int book=0;book<5;book++)
                        counter.Box(-.8f+book*.045f,.055f+book*.11f,.10f,.85f,.10f,.58f,book%2==0?Denim:Linen);
                    ReadingLamp(counter.Place(.98f,0,-.12f));
                }
                else if(floor%3==1) ArtBench(counter);
                else SampleRack(counter);
            }
            else if(site=="Cloud Harbor block")
            {
                counter.Box(-.7f,.12f,0,1.05f,.24f,.78f,Jade);
                counter.Tube(-.7f,.25f,0,.48f,.07f,Brass);
                counter.Round(-.76f,.49f,0,.19f,.20f,.19f,Terracotta);
                for(int pack=0;pack<3;pack++)
                    counter.Box(.45f+pack*.34f,.32f,-.04f,.29f,.64f,.39f,Linen);
            }
            else if(site.Contains("east"))
            {
                counter.Box(-.48f,.04f,0,1.64f,.08f,.82f,Ink);
                for(int i=0;i<4;i++)counter.Round(-1.04f+i*.37f,.18f,0,.17f,.13f,.23f,Oak);
                counter.Pipe(new Vector3(.63f,.18f,-.12f),new Vector3(1.32f,.18f,.12f),.13f,Walnut);
                counter.Tube(1.53f,0,-.23f,.17f,.48f,Linen);
            }
            else
            {
                for(int pile=0;pile<2;pile++)
                    for(int plate=0;plate<4-pile;plate++)
                        counter.Tube(-1.08f+pile*.94f,plate*.064f,0,.36f,.055f,Linen);
                counter.Tube(1.1f,0,-.16f,.27f,.48f,Jade);
                counter.Pipe(new Vector3(1.30f,.38f,-.16f),new Vector3(1.54f,.62f,-.16f),.09f,Brass);
            }
        }

        static void CoffeeCup(Room r)
        {
            r.Tube(0,0,0,.195f,.035f,Linen);
            r.Tube(0,.034f,0,.122f,.18f,Linen);
            r.Tube(0,.211f,0,.10f,.008f,Walnut);
            r.mesh.Ring(r.At(.128f,.13f,0),r.q*Quaternion.Euler(90,0,0),.067f,.020f,10,5,0,Linen);
        }

        static void WindowHerbs(Room r)
        {
            r.EdgedTop(0,.13f,0,2.14f,.26f,.54f,Terracotta);
            r.Box(0,.264f,0,1.97f,.023f,.40f,Walnut);
            for(int plant=-1;plant<=1;plant++)
            {
                float x=plant*.62f;
                r.Pipe(new Vector3(x,.26f,0),new Vector3(x+.035f,.71f,0),.029f,Jade);
                r.Round(x-.085f,.45f,-.025f,.135f,.079f,.098f,Jade);
                r.Round(x+.12f,.58f,.025f,.14f,.09f,.088f,new Color(.40f,.61f,.29f));
                r.Round(x+.034f,.72f,0,.07f,.09f,.06f,Jade);
            }
        }

        static void ReadingLamp(Room r)
        {
            r.Tube(0,0,0,.26f,.085f,Ink);
            r.Pipe(new Vector3(0,.08f,0),new Vector3(.16f,.82f,0),.045f,Brass);
            r.mesh.Cone(r.At(.16f,.64f,0),r.q,.32f,.27f,0,Jade,14);
        }

        static void RecordPlayer(Room r)
        {
            r.Box(-.48f,.12f,0,1.34f,.24f,.9f,Walnut);
            r.Tube(-.66f,.24f,0,.37f,.046f,Ink);
            r.Tube(-.66f,.287f,0,.12f,.02f,Terracotta);
            r.Tube(.03f,.24f,-.30f,.075f,.10f,Brass);
            r.Pipe(new Vector3(.03f,.34f,-.30f),new Vector3(-.35f,.33f,.12f),.032f,Brass);
            r.Box(1.1f,.42f,-.06f,.60f,.84f,.54f,Walnut);
            r.mesh.Cylinder(r.At(1.1f,.39f,.22f),r.q*Quaternion.Euler(90,0,0),.21f,.05f,0,Ink,14);
        }

        static void SewingTable(Room r)
        {
            r.Box(-.4f,.07f,0,1.4f,.14f,.78f,Jade);
            r.Box(-.91f,.38f,0,.35f,.64f,.43f,Jade);
            r.Box(-.43f,.68f,0,1.19f,.23f,.40f,Jade);
            r.Box(.08f,.46f,0,.036f,.25f,.036f,Brass);
            r.Box(.38f,.032f,.15f,.75f,.06f,.61f,Denim);
            for(int i=0;i<3;i++)r.Tube(.79f+i*.26f,0,-.26f,.10f,.24f,i%2==0?Terracotta:Linen);
        }

        static void ArtBench(Room r)
        {
            r.Box(-.55f,.035f,0,1.37f,.07f,.84f,Linen);
            for(int i=0;i<4;i++)r.Tube(-1.02f+i*.28f,.08f,.12f,.09f,.025f,Spectrum[i+1]);
            r.Tube(.85f,0,-.13f,.19f,.37f,Jade);
            for(int i=0;i<4;i++)r.Pipe(new Vector3(.8f+i*.04f,.20f,-.13f),new Vector3(.65f+i*.14f,.77f,-.1f+i*.025f),.025f,Oak);
        }

        static void SampleRack(Room r)
        {
            r.Box(-.62f,.08f,-.03f,1.67f,.16f,.57f,Walnut);
            for(int i=0;i<5;i++)
            {
                float x=-1.23f+i*.31f;
                r.Tube(x,.16f,0,.105f,.38f,i%2==0?Denim:Jade);
                r.Tube(x,.54f,0,.12f,.07f,Brass);
            }
            r.Tube(.95f,0,0,.34f,.12f,Ink);
            r.Pipe(new Vector3(.95f,.10f,-.21f),new Vector3(.95f,.68f,-.21f),.09f,Brass);
            r.Pipe(new Vector3(.95f,.72f,-.27f),new Vector3(.95f,.49f,.16f),.11f,Ink);
            r.Box(.95f,.29f,-.02f,.48f,.05f,.46f,Ink);
        }

        static void InstrumentBench(Room r,bool windings)
        {
            if(windings)
            {
                for(int coil=0;coil<2;coil++)
                {
                    float x=-.84f+coil*1.18f;
                    r.Tube(x,0,0,.35f,.12f,Ink);
                    for(int turn=0;turn<5;turn++)r.Tube(x,.12f+turn*.10f,0,.28f,.075f,Brass);
                    r.Tube(x,.61f,0,.16f,.18f,Ink);
                }
            }
            else
            {
                r.Box(-.55f,.32f,-.03f,1.55f,.64f,.65f,Denim);
                r.Box(-.72f,.36f,.31f,.80f,.37f,.06f,Linen);
                r.Pipe(new Vector3(-.75f,.22f,.35f),new Vector3(-.55f,.46f,.35f),.022f,Ink);
                r.mesh.Cylinder(r.At(.04f,.30f,.31f),r.q*Quaternion.Euler(90,0,0),.11f,.08f,0,Brass,12);
            }
            r.Box(1.09f,.035f,.11f,.82f,.07f,.61f,Linen);
            r.Box(1.26f,.096f,.11f,.065f,.05f,.48f,Walnut);
        }

        static void FilterBank(Room wall)
        {
            // Upright cartridge housings share a feed line, with removable bolted caps.
            for(int tank=-1;tank<=1;tank++)
            {
                float x=tank*1.27f;
                wall.Tube(x,.06f,0,.54f,.19f,Ink);
                wall.Tube(x,.25f,0,.44f,1.80f,new Color(.32f,.62f,.62f));
                wall.Tube(x,2.04f,0,.54f,.20f,Brass);
                wall.Tube(x,2.23f,0,.17f,.37f,Ink);
                wall.Tube(x,1.02f,0,.47f,.11f,Brass);
            }
            wall.Pipe(new Vector3(-1.48f,2.61f,0),new Vector3(1.48f,2.61f,0),.15f,Brass);
        }

        static void Kitchen(Room r)
        {
            r.Box(0,.54f,0,4.42f,1.08f,1.28f,Jade);
            r.EdgedTop(0,1.13f,0,4.60f,.15f,1.40f,Linen);
            r.Box(0,1.50f,-.61f,4.56f,.73f,.12f,Oak);
            for(int door=-1;door<=1;door++)
            {
                float x=door*1.42f;
                r.Box(x,.56f,.66f,1.34f,.89f,.08f,new Color(.25f,.46f,.37f));
                r.Box(x,.85f,.74f,.40f,.055f,.09f,Brass);
            }
            r.Box(-.91f,1.22f,0,1.40f,.05f,.80f,Ink);
            r.Box(-.91f,1.25f,0,1.18f,.04f,.63f,new Color(.35f,.49f,.49f));
            r.Pipe(new Vector3(-.91f,1.22f,-.49f),new Vector3(-.91f,1.71f,-.49f),.052f,Brass);
            r.Pipe(new Vector3(-.91f,1.71f,-.49f),new Vector3(-.91f,1.71f,-.14f),.052f,Brass);
            r.Tube(1.18f,1.21f,.02f,.40f,.06f,Ink);
            r.Tube(1.18f,1.27f,.02f,.31f,.30f,Terracotta);
            r.Box(1.58f,1.45f,.02f,.35f,.065f,.10f,Walnut);
            for(int bracket=-1;bracket<=1;bracket+=2)
            {
                float x=bracket*1.52f;
                r.Box(x,1.71f,-.58f,.12f,.52f,.14f,Brass);
                r.Pipe(new Vector3(x,1.51f,-.58f),new Vector3(x,1.98f,-.12f),.043f,Brass);
            }
            r.EdgedTop(0,2.03f,-.32f,4.47f,.14f,.63f,Oak);
            for(int jar=0;jar<4;jar++)
            {
                float x=-1.48f+jar*.72f;
                r.Tube(x,2.11f,-.28f,.19f,.34f,jar%2==0?Linen:Terracotta);
                r.Tube(x,2.45f,-.28f,.20f,.065f,Walnut);
            }
        }

        static void Chair(Room r,Color fabric)
        {
            r.EdgedTop(0,.50f,0,.94f,.17f,.91f,Fabric(fabric));
            r.EdgedTop(0,.90f,-.40f,.94f,.70f,.13f,Fabric(fabric));
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                r.Box(x*.34f,.22f,z*.31f,.09f,.44f,.09f,Walnut);
        }
        static void Booth(Room r)
        {
            r.Box(0,.28f,0,3.9f,.56f,1.45f,Walnut);
            r.Box(0,.91f,-.56f,3.84f,1.24f,.33f,Fabric(Jade));
            for(int cushion=-1;cushion<=1;cushion++)
            {
                r.Box(cushion*1.17f,.64f,.08f,1.12f,.22f,1.13f,Fabric(new Color(.30f,.53f,.43f)));
                r.Round(cushion*1.17f,.74f,.08f,.56f,.10f,.555f,Fabric(new Color(.30f,.53f,.43f)));
            }
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
            r.Tube(0,ceiling-.09f,0,.22f,.105f,Brass);
            r.Tube(0,ceiling-.65f,0,.04f,.65f,Ink);
            r.mesh.Cone(r.At(0,ceiling-.99f,0),r.q,.55f,.37f,0,enamel,14);
            r.Round(0,ceiling-1.02f,0,.27f,.15f,.27f,Linen);
        }
    }
}

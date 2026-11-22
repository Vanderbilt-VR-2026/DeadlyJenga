using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        // Each facade uses its own floor as its frame, including after a tower collapses.
        void Neighborhood(ArtMesh mesh, Transform visual, Rigidbody body, Bounds bounds, string site, string purpose)
        {
            FurnishNeighborhood(mesh,visual,body,site,purpose);
            var frame = body.transform;
            var front = FaceToward(frame, frame.TransformPoint(bounds.center), false);
            var yaw = Quaternion.LookRotation(front);
            var rotation = Quaternion.Inverse(visual.rotation) * frame.rotation * yaw;
            var extent = bounds.extents;
            float depth = Mathf.Abs(front.x) > .5f ? extent.x : extent.z;
            float width = Mathf.Abs(front.x) > .5f ? extent.z : extent.x;
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + front * depth;
            Vector3 P(float x, float y, float z) => visual.InverseTransformPoint(frame.TransformPoint(origin + yaw * new Vector3(x,y,z)));
            void Box(float x,float y,float z,float w,float h,float d, Color color) =>
                mesh.Box(P(x,y,z),new Vector3(w,h,d),rotation,0,color);
            void Ball(float x,float y,float z,float radius, Color color) =>
                mesh.Sphere(P(x,y,z),Vector3.one*radius,0,color,6,10);
            var cream = new Color(1,.89f,.68f);
            var wood = new Color(.59f,.32f,.14f);
            var lime = new Color(.48f,.82f,.16f);
            void Bar(Vector3 a, Vector3 b, float thickness, Color color, int material = 0)
            {
                mesh.Box((a+b)*.5f, new Vector3(thickness, thickness, (b-a).magnitude),
                    Quaternion.LookRotation(b-a), material, color);
            }
            bool ground = purpose.EndsWith(" floor 1");
            float roof = bounds.size.y;

            // The counter opens onto the plaza. Display windows, the cornice and the hanging
            // fruit sign also address the boardwalk on the opposite side of the shop.
            if (site.StartsWith("Cloud Harbor") && site!="Cloud Harbor east annex" &&
                (ground || purpose.EndsWith(" floor 2")))
            {
                bool grocer=site=="Cloud Harbor block";
                var outside=rotation*Quaternion.Euler(0,180,0);
                var facade=P(0,0,-2*depth);
                Vector3 Shop(float x,float y,float z)=>facade+outside*new Vector3(x,y,z);
                var trim=grocer?new Color(.46f,.15f,.63f):new Color(1,.65f,.06f);
                if(ground)
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        var center=Shop(side*width*.45f,2.05f,.42f);
                        float span=width*.77f;
                        WindowOpening(mesh,center,outside,span,3.06f,cream);
                        mesh.Box(center+outside*Vector3.forward*.32f,new Vector3(.13f,3.1f,.15f),outside,0,wood);
                    }
                    for(int stripe=-6;stripe<=6;stripe++)
                    {
                        float pitch=width*1.88f/13;
                        var color=grocer && stripe%2==0?cream:trim;
                        mesh.Box(Shop(stripe*pitch,3.65f,1.48f),new Vector3(pitch+.015f,.25f,3.05f),
                            outside*Quaternion.Euler(13,0,0),0,color);
                        mesh.Box(Shop(stripe*pitch,3.06f,2.94f),new Vector3(pitch+.015f,.54f,.18f),outside,0,color);
                    }
                }
                else
                {
                    mesh.Box(Shop(0,roof-.12f,0),new Vector3(width*2+.32f,.54f,.73f),outside,0,trim);
                    if(grocer)
                    {
                        for(int fruit=-1;fruit<=1;fruit+=2)
                        {
                            mesh.Sphere(Shop(fruit*1.8f,roof+1.22f,.44f),new Vector3(1.65f,1.72f,.86f),0,fruit<0?CaneRed:Gold,9,14);
                            mesh.Sphere(Shop(fruit*1.8f+.42f,roof+2.88f,.44f),new Vector3(.73f,.32f,.45f),0,lime);
                        }
                    }
                }
            }

            if (site.StartsWith("Cloud Harbor") && ground)
            {
                bool grocery = site == "Cloud Harbor block";
                bool shop = site == "Cloud Harbor west annex";
                if (!grocery && !shop)
                {
                    for (int i=-1;i<=1;i+=2)
                    {
                        Box(i*5,1.28f,-2,3.5f,2.56f,2.5f,GrapeDark);
                        for(int j=0;j<3;j++) mesh.Cylinder(P(i*5+j*.55f-.55f,2,-2),rotation,.2f,.65f,0,Gold,8);
                    }
                    return;
                }
                var stripe = grocery ? new Color(.50f,.12f,.72f) : new Color(1,.70f,.08f);
                for(int i=-5;i<=5;i++)
                    {
                    mesh.Box(P(i*1.17f,3.51f,1.32f),new Vector3(1.19f,.26f,3.45f),
                        rotation*Quaternion.Euler(14,0,0),0,grocery && i%2!=0?cream:stripe);
                    Box(i*1.17f,2.97f,2.92f,1.19f,.55f,.24f,grocery && i%2!=0?cream:stripe);
                }
                foreach(int side in new[]{-1,1})
                {
                    Box(side*4,.55f,-2.8f,2.5f,1.05f,1.4f,wood);
                    if(grocery)
                        for(int row=0;row<2;row++) for(int col=0;col<5;col++)
                            Ball(side*4+(col-2)*.44f,1.25f,-3.08f+row*.57f,.27f,side<0?lime:Gold);
                }
                // Hang the sign from the projecting fascia so it reads along the street.
                Box(0,3.65f,-.25f,13.2f,.35f,.75f,cream);
                for(int side=-1;side<=1;side+=2)
                    Box(side*6.22f,1.6f,2.72f,.22f,3.2f,.22f,cream);
                for(int side=-1;side<=1;side+=2)
                {
                    // A display window returns around each corner. The tills and open counter still face the street.
                    var sideRotation=rotation*Quaternion.Euler(0,90*side,0);
                    var panel=P(side*width,2.1f,-depth*.72f);
                    WindowOpening(mesh,panel,sideRotation,6.12f,2.76f,cream);
                    mesh.Box(panel+sideRotation*new Vector3(0,1.77f,.76f),new Vector3(7.4f,.32f,2.15f),
                        sideRotation*Quaternion.Euler(13,0,0),0,stripe);
                }
                if(grocery)
                {
                    Ball(-1,4.35f,1.4f,1.05f,CaneRed);
                    Ball(1,4.35f,1.4f,.98f,Gold);
                    foreach(int side in new[]{-1,1})
                        mesh.Sphere(P(side+.32f,5.32f,1.4f),new Vector3(.56f,.24f,.32f),0,lime);
                }
            }
            else if(site.StartsWith("Cloud Harbor") && purpose.EndsWith(" floor 2") && site!="Cloud Harbor east annex")
            {
                bool grocer=site=="Cloud Harbor block";
                var plum=new Color(.29f,.10f,.44f);
                Box(0,roof+.85f,-depth*.76f,11.5f,1.7f,.65f,plum);
                Box(0,roof+1.73f,-depth*.76f,11.8f,.24f,.82f,cream);
                if(grocer)
                {
                    foreach(int side in new[]{-1,1})
                    {
                        float x=side*1.32f;
                        mesh.Sphere(P(x,roof+1.8f,-depth*.76f+.4f),new Vector3(1.35f,1.55f,.75f),0,side<0?CaneRed:Gold,10,16);
                        mesh.Sphere(P(x+.42f,roof+3.22f,-depth*.76f+.4f),new Vector3(.72f,.33f,.34f),0,lime,6,10);
                    }
                }
                else
                {
                    for(int slat=-3;slat<=3;slat++)
                        Box(slat*1.42f,roof+.9f,-depth*.76f+.4f,1.13f,1.14f,.2f,Gold);
                }
            }
            else if(site.StartsWith("Sugar") && purpose.Contains(" floor "))
            {
                if(site=="Sugar Market east annex" && purpose.EndsWith(" floor 7"))
                {
                    var dial=P(0,roof+2.5f,.6f);
                    for(int course=0;course<8;course++)
                        Box(0,roof+2.8f+course*.34f,-.18f,16.8f-course*1.85f,.36f,1.36f,new Color(.95f,.28f,.47f));
                    Box(0,roof+1.1f,0,11,3.2f,1.1f,new Color(.86f,.17f,.39f));
                    mesh.Cylinder(dial,rotation*Quaternion.Euler(90,0,0),4.35f,.38f,0,wood,40);
                    mesh.Cylinder(dial+rotation*Vector3.forward*.4f,rotation*Quaternion.Euler(90,0,0),4.05f,.12f,0,cream,40);
                    foreach(int side in new[]{-1,1})
                        mesh.Box(P(side*4.15f,roof+4.84f,.18f),new Vector3(9.3f,.56f,2.66f),
                            rotation*Quaternion.Euler(0,0,-side*22),0,new Color(.91f,.23f,.4f));
                    float y=roof+2.5f;
                    Bar(P(0,y,1.22f),P(-1.1f,y+1.2f,1.22f),.28f,wood);
                    Bar(P(0,y,1.24f),P(1.95f,y+.38f,1.24f),.24f,wood);
                    for(int hour=0;hour<12;hour++)
                    {
                        float angle=hour*Mathf.PI/6;
                        Ball(3.55f*Mathf.Sin(angle),y+3.55f*Mathf.Cos(angle),1.18f,.15f,wood);
                    }
                }
                if(ground && (site=="Sugar Market hall" || site=="Sugar Market east annex"))
                {
                    // A projecting school portico with a readable roofline from both upper bridges and the street.
                    foreach(int side in new[]{-1,1})
                    {
                        Box(side*3.65f,1.65f,2.3f,.46f,3.3f,.46f,cream);
                        Box(side*3.65f,.23f,2.3f,.82f,.46f,.82f,wood);
                        Box(side*3.65f,3.24f,2.3f,.8f,.4f,.8f,cream);
                        mesh.Box(P(side*2,3.95f,1.2f),new Vector3(4.35f,.28f,3.6f),
                            rotation*Quaternion.Euler(0,0,-side*14),0,new Color(.76f,.2f,.37f));
                        Bar(P(0,4.45f,3),P(side*4.15f,3.42f,3),.25f,cream);
                    }
                    Box(0,3.45f,2.92f,8.6f,.36f,.36f,cream);
                    Box(0,3.55f,1.05f,1.65f,.15f,.55f,Gold);
                }
                if(site=="Sugar Market hall" && purpose.EndsWith(" floor 3"))
                {
                    WindowOpening(mesh,P(0,1.93f,.25f),rotation,width*1.79f,2.8f,cream);
                    foreach(int mullion in new[]{-1,0,1})
                        Box(mullion*width*.46f,1.93f,.75f,.25f,2.8f,.18f,wood);
                    Box(0,-.24f,.88f,width*1.92f,.28f,1.64f,cream);
                }
                if(ground && site=="Sugar Market hall")
                {
                    Box(0,2.1f,-depth*1.75f,7,2.45f,.32f,wood);
                    Box(0,2.1f,-depth*1.75f+.21f,6.65f,2.12f,.12f,new Color(.075f,.27f,.23f));
                    for(int line=0;line<3;line++)
                        Box(-1.6f,2.64f-line*.39f,-depth*1.75f+.29f,1.9f-line*.27f,.065f,.06f,cream);
                    for(int bike=0;bike<3;bike++)
                    {
                        float x=3+bike*2.1f;
                        for(int wheel=0;wheel<2;wheel++)
                        {
                            float wx=x+wheel*.98f;
                            for(int spoke=0;spoke<10;spoke++)
                            {
                                float a=spoke*Mathf.PI/5, b=(spoke+1)*Mathf.PI/5;
                                Bar(P(wx+.39f*Mathf.Cos(a),.40f+.39f*Mathf.Sin(a),1.1f),
                                    P(wx+.39f*Mathf.Cos(b),.40f+.39f*Mathf.Sin(b),1.1f),.075f,wood);
                            }
                            Bar(P(wx,.4f,1.1f),P(x+.53f,1,1.1f),.09f,bike==1?Gold:CaneRed);
                        }
                        Bar(P(x+.98f,.40f,1.1f),P(x+.82f,1.24f,1.1f),.075f,wood);
                        Box(x+.82f,1.24f,1.1f,.36f,.1f,.1f,wood);
                        Box(x+.44f,1,1.1f,.32f,.09f,.18f,wood);
                    }
                    // Painted hopscotch on the school approach; the entry stays clear.
                    for(int row=0;row<6;row++)
                    {
                        int cells=row%2+1;
                        for(int cell=0;cell<cells;cell++)
                        {
                            float x=-4+(cell-(cells-1)*.5f)*1.05f;
                            float z=1.8f+row*1.05f;
                            Box(x,.025f,z,1,.04f,1,cream);
                            Box(x,.053f,z,.78f,.025f,.78f,row%2==0?Gold:lime);
                        }
                    }
                }
                foreach(int side in new[]{-1,1})
                {
                    // Large classroom panes in the broad low wing; the tall bell tower carries
                    // fewer lights so the clock and peaked entry are its focal points.
                    bool tower = site=="Sugar Market east annex";
                    if((!tower && site!="Sugar Market hall") || ground || purpose.EndsWith(" floor 4") || purpose.EndsWith(" floor 6"))
                    {
                        float span=tower?4.3f:7.6f;
                        float x=side*width*.55f;
                        WindowOpening(mesh,P(x,2.02f,-.08f),rotation,span,2.84f,cream);
                        Box(x,2.02f,.32f,.12f,2.94f,.18f,wood);
                        Box(x,.5f,.4f,span+.8f,.24f,.75f,cream);
                    }
                    if(ground) Box(side*6,1.11f,-2.8f,2.6f,.16f,1.48f,wood);
                }
            }
            else if(site.StartsWith("Grape") && (purpose.Contains("bookfall support") || purpose.Contains("book wing")))
            {
                foreach (var collider in body.GetComponentsInChildren<BoxCollider>())
                {
                    if (Mathf.Abs(collider.size.y-1.04f)>.005f || Mathf.Abs(collider.size.z-3.6f)>.005f) continue;
                    var seat=visual.InverseTransformPoint(collider.transform.TransformPoint(collider.center));
                    var facing=Quaternion.Inverse(visual.rotation)*collider.transform.rotation;
                    mesh.Box(seat+facing*Vector3.up*.52f,new Vector3(2.12f,.22f,3.64f),facing,0,cream);
                    for(int pillow=-1;pillow<=1;pillow+=2)
                        mesh.Box(seat+facing*new Vector3(0,.7f,pillow*1.32f),new Vector3(1.65f,.38f,.64f),facing,0,Gold);
                }
                if(purpose=="Independent bookfall support spine" && body.worldCenterOfMass.y-groundY<12)
                {
                    // The front of the long apartment spine opens toward the original overview.
                    Vector3 Entry(float x,float y,float z)=>visual.InverseTransformPoint(frame.TransformPoint(new Vector3(x,y,37+z)));
                    var doorRotation=Quaternion.Inverse(visual.rotation)*frame.rotation;
                    mesh.Box(Entry(0,1.64f,-.65f),new Vector3(3.8f,3.3f,.3f),doorRotation,0,wood);
                    for(int stripe=-3;stripe<=3;stripe++)
                        mesh.Box(Entry(stripe*.72f,3.4f,.85f),new Vector3(.75f,.23f,2.2f),
                            doorRotation*Quaternion.Euler(15,0,0),0,stripe%2==0?CaneRed:cream);
                    for(int side=-1;side<=1;side+=2)
                    {
                        mesh.Cylinder(Entry(side*3.2f,0,.58f),doorRotation,.78f,1,0,cream,12);
                        mesh.Sphere(Entry(side*3.2f,1.4f,.58f),Vector3.one*.94f,0,lime,7,12);
                    }
                }
                var glass=new Color(.28f,.48f,.54f);
                // The long spine and both wing ends have their own elevations. Dress all four faces
                // because the street curves around the building and wings can turn on their hinges.
                for(int face=0;face<4;face++)
                {
                    var turn=Quaternion.Euler(0,90*face,0);
                    var q=rotation*turn;
                    float halfSpan=face%2==0?width:depth;
                    float outwards=face%2==0?depth:width;
                    Vector3 F(float x,float y,float z)=>P(0,y,-depth)+q*new Vector3(x,0,outwards+z);
                    void Trim(float x,float y,float z,float w,float h,float d,Color c)=>
                        mesh.Box(F(x,y,z),new Vector3(w,h,d),q,0,c);
                    int bays=Mathf.Max(2,Mathf.FloorToInt(halfSpan/5.5f));
                    float facadeHeight=purpose.Contains("support spine") || purpose.Contains("book wing")?12:roof;
                    for(float y=facadeHeight*.53f;y+1<facadeHeight;y+=8)
                    for(int bay=0;bay<bays;bay++)
                    {
                        float x=(2*(bay+.5f)/bays-1)*(halfSpan-1);
                        if(bay==bays/2 && bays>2) continue;
                        if(purpose.Contains("support spine") && halfSpan>25 && bay%3==1)
                        {
                            // Inset the door behind the deck edge. Players can occupy the gallery.
                            Trim(x,y,-2.5f,5.6f,3.2f,.24f,wood);
                            Trim(x,y,-2.34f,4.95f,2.87f,.12f,new Color(.85f,.51f,.25f));
                            Trim(x,y+.55f,-2.23f,3.9f,1.06f,.12f,glass);
                            Trim(x,y,-2.13f,.16f,2.88f,.12f,cream);
                            Trim(x,y-1.36f,.08f,5.9f,.15f,.2f,cream);
                            Trim(x,y-.79f,.08f,6,.19f,.23f,cream);
                            for(int post=-3;post<=3;post++)
                                Trim(x+post*.94f,y-1.12f,.08f,.12f,.84f,.12f,cream);
                            Trim(x+2.44f,y+.6f,-2.04f,.52f,.7f,.48f,Gold);
                            continue;
                        }
                        float w=Mathf.Min(6.2f,(halfSpan-1)*1.7f/bays);
                        float h=Mathf.Min(4.9f,roof-1.1f);
                        WindowOpening(mesh,F(x,y,.07f),q,w,h,cream);
                        Trim(x,y,.38f,.135f,h,.13f,wood);
                        Trim(x,y+h*.28f,.39f,w,.115f,.13f,cream);
                        Trim(x,y-h*.5f-.15f,.42f,w+.85f,.29f,.92f,cream);
                        if((bay+Mathf.RoundToInt(y/4))%2==0)
                        {
                            Trim(x,y-h*.5f-.28f,.53f,4.6f,.5f,1,wood);
                            for(int leaf=-1;leaf<=1;leaf++)
                                mesh.Sphere(F(x+1.45f*leaf,y-h*.5f,.65f),new Vector3(.84f,.43f,.45f),0,lime,4,8);
                        }
                    }
                }
            }
            else if(site.StartsWith("Downtown") && purpose=="Escape spine")
            {
                // Dress the existing walkable inclined colliders with treads that follow their actual rise.
                foreach(var flight in body.GetComponentsInChildren<MeshCollider>())
                {
                    if(!flight.sharedMesh) continue;
                    var ramp=flight.sharedMesh.bounds;
                    if(ramp.size.z<5 || ramp.size.y<2) continue;
                    int steps=Mathf.CeilToInt(ramp.size.y/.65f);
                    var q=Quaternion.Inverse(visual.rotation)*flight.transform.rotation;
                    Vector3 Edge(float x,float t,float height)
                    {
                        var point=new Vector3(x,Mathf.Lerp(ramp.min.y,ramp.max.y,t)+height,
                            Mathf.Lerp(ramp.min.z,ramp.max.z,t));
                        return visual.InverseTransformPoint(flight.transform.TransformPoint(point));
                    }
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=ramp.center.x+side*ramp.extents.x;
                        Bar(Edge(x,0,.03f),Edge(x,1,.03f),.46f,Gold);
                        Bar(Edge(x,0,1.02f),Edge(x,1,1.02f),.17f,Gold);
                        for(int post=0;post<=4;post++)
                            Bar(Edge(x,post*.25f,0),Edge(x,post*.25f,1.03f),.13f,wood);
                    }
                    for(int step=0;step<steps;step++)
                    {
                        float t=(step+.5f)/steps;
                        var at=new Vector3(ramp.center.x,.08f+Mathf.Lerp(ramp.min.y,ramp.max.y,t),Mathf.Lerp(ramp.min.z,ramp.max.z,t));
                        mesh.Box(visual.InverseTransformPoint(flight.transform.TransformPoint(at)),new Vector3(ramp.size.x+.24f,.7f,ramp.size.z/steps+.03f),q,0,new Color(1,.82f,.025f));
                    }
                }
                // Cabinet fronts and recessed ceiling lighting are modelled on the service level.
                var towerRotation=Quaternion.Inverse(visual.rotation)*frame.rotation;
                Vector3 T(float x,float y,float z)=>visual.InverseTransformPoint(frame.TransformPoint(new Vector3(x,y,z)));
                // Fascias wrap the three landings into one orange building. The wall behind
                // the stairs carries the controls and leaves walk-through doors at both ends.
                var burntOrange=new Color(.53f,.20f,.07f);
                var copper=new Color(.88f,.40f,.12f);
                mesh.Box(T(-6.9f,4.28f,0),new Vector3(.64f,8.56f,9.4f),towerRotation,0,burntOrange);
                mesh.Box(T(-6.9f,12.18f,0),new Vector3(.64f,4.32f,9.4f),towerRotation,0,burntOrange);
                var cabinet=towerRotation*Quaternion.Euler(0,90,0);
                Switchgear(mesh,T(-6.54f,3.1f,0),cabinet,2.1f);
                Switchgear(mesh,T(-6.54f,11.9f,0),cabinet,1.55f);
                for(int end=-1;end<=1;end+=2)
                {
                    mesh.Box(T(0,14.73f,end*12.42f),new Vector3(17.5f,1.04f,.6f),towerRotation,0,copper);
                    mesh.Box(T(0,14.14f,end*12.25f),new Vector3(15.8f,.17f,.45f),towerRotation,1,Gold);
                }
                mesh.Box(T(8.28f,14.85f,0),new Vector3(.7f,.80f,25.3f),towerRotation,0,copper);
                foreach(int end in new[]{-1,1})
                {
                    var outward=towerRotation*Quaternion.Euler(0,90,0);
                    Switchgear(mesh,T(-2.66f,11.18f,end*7),outward,.85f);
                    mesh.Box(T(-4,14.49f,end*6),new Vector3(1.2f,.15f,3.8f),towerRotation,1,new Color(1,.69f,.28f));
                }
                // Each escape tier has three staggered flights. Only the highest tier gets the terminal.
                if(body.worldCenterOfMass.y-groundY<30) return;
                Vector3 Mast(float x,float y,float z) =>
                    visual.InverseTransformPoint(frame.TransformPoint(new Vector3(-4+x,15+y,z)));
                var mastRotation=Quaternion.Inverse(visual.rotation)*frame.rotation;
                mesh.Box(Mast(0,.55f,0),new Vector3(4.84f,1.12f,4.84f),mastRotation,0,cream);
                mesh.Box(Mast(0,1.5f,0),new Vector3(3,1,3),mastRotation,0,wood);
                mesh.Cylinder(Mast(0,2,0),mastRotation,.39f,5,0,wood,12);
                mesh.Sphere(Mast(0,7,0),Vector3.one*.85f,0,Gold,8,12);
                var bend=Mast(1,10,-.3f);
                var elbow=Mast(-.5f,12.2f,.1f);
                Bar(Mast(0,7.6f,0),bend,.34f,Sugar,1);
                Bar(bend,elbow,.32f,Sugar,1);
                Bar(elbow,Mast(2.7f,20,-.6f),.26f,Sugar,1);
                Bar(elbow,Mast(-4.1f,16,-.4f),.18f,Sugar,1);
            }
            else if(site=="Canyon south-east tower" && purpose.Contains(" floor "))
            {
                var water=new Color(.12f,.72f,.92f);
                var enamel=new Color(.98f,.88f,.65f);
                for(int side=-1;side<=1;side+=2)
                {
                    float at=side*(width-1.5f);
                    mesh.Cylinder(P(at,0,.45f),rotation,.68f,roof,0,water,12);
                    mesh.Cylinder(P(at,0,.45f),rotation,.83f,.36f,0,enamel,12);
                    mesh.Cylinder(P(at,roof-.35f,.45f),rotation,.83f,.35f,0,enamel,12);
                    // Repeat the gauge on the opposite face to serve both the basin and the exterior walk.
                    mesh.Cylinder(P(at,0,-depth*2-.45f),rotation,.8f,roof,0,water,14);
                    mesh.Cylinder(P(at,roof-.44f,-depth*2-.45f),rotation,.97f,.44f,0,enamel,14);
                    for(int tick=1;tick<=3;tick++)
                        Box(at+.38f,tick*.92f,-depth*2-1.18f,.53f,.11f,.12f,cream);
                }
                if(ground)
                {
                    var basin=visual.InverseTransformPoint(frame.TransformPoint(new Vector3(2.1f,.64f,-2.5f)));
                    var q=Quaternion.Inverse(visual.rotation)*frame.rotation;
                    mesh.Box(basin,new Vector3(13.65f,.16f,9.65f),q,0,enamel);
                    mesh.Box(basin+q*Vector3.up*.11f,new Vector3(13.1f,.12f,9.1f),q,0,water);
                    for(int ring=0;ring<3;ring++)
                        mesh.Cylinder(basin+q*Vector3.up*(.18f+ring*.025f),q,2.1f-ring*.61f,.045f,0,ring%2==0?Sugar:water,30);
                }
                if(purpose.EndsWith(" floor 7"))
                {
                    // Sit the collector on the solid half of the deck, clear of the stair opening.
                    var qRoof=Quaternion.Inverse(visual.rotation)*frame.rotation;
                    Vector3 Collector(float x,float y,float z)=>visual.InverseTransformPoint(
                        frame.TransformPoint(new Vector3(2.7f+x,4.12f+y,z)));
                    var center=Collector(0,.03f,0);
                    for(int i=0;i<4;i++)
                    {
                        var q=qRoof*Quaternion.Euler(0,90*i,0);
                        float halfWidth=i%2==0?7.3f:9.6f;
                        float reach=i%2==0?9.6f:7.3f;
                        Vector3 Rim(float x,float y,float z)=>center+q*new Vector3(x,y,z);
                        var a=Rim(-1.65f,0,1.65f); var b=Rim(1.65f,0,1.65f);
                        var c=Rim(halfWidth,2.85f,reach); var d=Rim(-halfWidth,2.85f,reach);
                        var normal=Vector3.Cross(d-a,b-a).normalized;
                        mesh.Panel(a,d,c,b,normal,normal,normal,normal,water);
                        Bar(c+Vector3.up*.24f,d+Vector3.up*.24f,.91f,enamel);
                        Bar(a+Vector3.up*.28f,d+Vector3.up*.28f,.82f,enamel);
                    }
                    mesh.Cylinder(center+Vector3.up*.24f,rotation,2.1f,.22f,0,new Color(.04f,.38f,.53f),20);
                    mesh.Sphere(Collector(-.8f,11.4f,1.3f),new Vector3(4.5f,2.6f,3.35f),0,Sugar,10,16);
                    mesh.Sphere(Collector(2.2f,10,-1.85f),new Vector3(3.3f,2.35f,3.15f),0,Sugar,10,16);
                    for(int puff=-2;puff<=2;puff++)
                    {
                        float y=9.5f+.9f*(2-Mathf.Abs(puff));
                        mesh.Sphere(Collector(puff*2.2f,y,0),new Vector3(3.1f,2.55f,3.1f),0,Sugar,10,16);
                    }
                    for(int drop=0;drop<15;drop++)
                    {
                        float angle=drop*2.4f, radius=1.3f+drop%3;
                        var start=Collector(Mathf.Cos(angle)*radius,3.4f+drop%3,Mathf.Sin(angle)*radius);
                        Bar(start,start+rotation*new Vector3(.26f,2.6f,0),.12f,water,1);
                    }
                }
            }
        }
    }
}

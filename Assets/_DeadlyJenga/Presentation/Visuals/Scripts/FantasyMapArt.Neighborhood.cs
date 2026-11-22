using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        // Each facade uses its own floor as its frame, including after a tower collapses.
        void Neighborhood(ArtMesh mesh, Transform visual, Rigidbody body, Bounds bounds, string site, string purpose, bool top)
        {
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

            if (site.StartsWith("Cloud Harbor") && ground)
            {
                bool grocery = site == "Cloud Harbor block";
                bool shop = site == "Cloud Harbor west annex";
                if (!grocery && !shop)
                {
                    for (int i=-1;i<=1;i+=2)
                    {
                        Box(i*5,1,-2,2.6f,2,2,wood);
                        for(int j=0;j<3;j++) mesh.Cylinder(P(i*5+j*.55f-.55f,2,-2),rotation,.2f,.65f,0,Gold,8);
                    }
                    return;
                }
                var stripe = grocery ? new Color(.58f,.22f,.78f) : Gold;
                for(int i=-5;i<=5;i++)
                    Box(i*.8f,3.2f,1.05f,.81f,.28f,2.3f,i%2==0?stripe:cream);
                foreach(int side in new[]{-1,1})
                {
                    Box(side*4,.55f,-2.8f,2.5f,1.05f,1.4f,wood);
                    if(grocery)
                        for(int row=0;row<2;row++) for(int col=0;col<5;col++)
                            Ball(side*4+(col-2)*.44f,1.25f,-3.08f+row*.57f,.27f,side<0?lime:Gold);
                }
                // Hang the sign from the projecting fascia so it reads along the street.
                Box(0,3.04f,2.18f,9,.55f,.22f,stripe);
                for(int side=-1;side<=1;side+=2)
                    Box(side*4.2f,1.55f,2,.18f,3.1f,.18f,cream);
                Box(0,2,-4.8f,6,2.7f,.3f,wood);
                for(int shelf=0;shelf<3;shelf++)
                {
                    Box(0,.7f+shelf*.82f,-4.4f,6.1f,.15f,.6f,cream);
                    for(int jar=0;jar<7;jar++)
                        Box((jar-3)*.78f,1+shelf*.82f,-4.4f,.43f,.48f,.43f,(jar+shelf)%2==0?lime:Gold);
                }
                if(grocery)
                {
                    Ball(-1,4.35f,1.4f,1.05f,CaneRed);
                    Ball(1,4.35f,1.4f,.98f,Gold);
                    foreach(int side in new[]{-1,1})
                        mesh.Sphere(P(side+.32f,5.32f,1.4f),new Vector3(.56f,.24f,.32f),0,lime);
                }
            }
            else if(site.StartsWith("Sugar") && purpose.Contains(" floor "))
            {
                if(site=="Sugar Market east annex" && purpose.EndsWith(" floor 7"))
                {
                    var dial=P(0,roof+2.5f,.6f);
                    Box(0,roof+1.1f,0,11,3.2f,1.1f,new Color(.86f,.17f,.39f));
                    mesh.Cylinder(dial,rotation*Quaternion.Euler(90,0,0),3.65f,.38f,0,wood,40);
                    mesh.Cylinder(dial+rotation*Vector3.forward*.4f,rotation*Quaternion.Euler(90,0,0),3.28f,.12f,0,cream,40);
                    float y=roof+2.5f;
                    Bar(P(0,y,1.22f),P(-1.1f,y+1.2f,1.22f),.28f,wood);
                    Bar(P(0,y,1.24f),P(1.95f,y+.38f,1.24f),.24f,wood);
                    for(int hour=0;hour<12;hour++)
                    {
                        float angle=hour*Mathf.PI/6;
                        Ball(2.87f*Mathf.Sin(angle),y+2.87f*Mathf.Cos(angle),1.18f,.15f,wood);
                    }
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
                    Box(side*width*.58f,2,0,3.8f,1.8f,.13f,cream);
                    Box(side*width*.58f,2,.09f,3.48f,1.5f,.12f,new Color(.52f,.81f,.86f));
                    Box(side*width*.58f,2,.18f,.16f,1.6f,.12f,cream);
                    Box(side*6,1.11f,-2.8f,2.6f,.16f,1.48f,wood);
                }
            }
            else if(site.StartsWith("Grape") && (purpose.Contains("bookfall support") || purpose.Contains("book wing") ||
                purpose.Contains("receiving foot") || purpose.Contains("landing pedestal")))
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
                var glass=new Color(.28f,.48f,.54f);
                var curtain=new Color(1,.58f,.22f);
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
                    int bays=Mathf.Max(2,Mathf.FloorToInt(halfSpan/3.8f));
                    for(float y=2;y+1<roof;y+=4)
                    for(int bay=0;bay<bays;bay++)
                    {
                        float x=(2*(bay+.5f)/bays-1)*(halfSpan-1);
                        if(bay==bays/2 && bays>2) continue;
                        Trim(x,y,.06f,4.6f,2.85f,.22f,cream);
                        Trim(x,y,.21f,4.16f,2.48f,.14f,glass);
                        Trim(x,y,.31f,.15f,2.5f,.1f,cream);
                        foreach(int side in new[]{-1,1})
                        {
                            Trim(x+side*1.68f,y+.25f,.33f,.63f,1.95f,.12f,curtain);
                            Trim(x+side*1.9f,y-.75f,.33f,.35f,.66f,.12f,curtain);
                        }
                        if((bay+Mathf.RoundToInt(y/4))%2==0)
                        {
                            Trim(x,y-1.2f,.53f,4.6f,.5f,1,wood);
                            for(int leaf=-1;leaf<=1;leaf++)
                                mesh.Sphere(F(x+1.45f*leaf,y-.87f,.65f),new Vector3(.84f,.43f,.45f),0,lime,4,8);
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
                    int steps=Mathf.CeilToInt(ramp.size.y/.35f);
                    var q=Quaternion.Inverse(visual.rotation)*flight.transform.rotation;
                    for(int step=0;step<steps;step++)
                    {
                        float t=(step+.5f)/steps;
                        var at=new Vector3(ramp.center.x,.08f+Mathf.Lerp(ramp.min.y,ramp.max.y,t),Mathf.Lerp(ramp.min.z,ramp.max.z,t));
                        mesh.Box(visual.InverseTransformPoint(flight.transform.TransformPoint(at)),new Vector3(ramp.size.x+.15f,.38f,ramp.size.z/steps+.03f),q,0,new Color(1,.72f,.045f));
                    }
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
                Bar(Mast(0,7.6f,0),bend,.18f,Sugar,1);
                Bar(bend,elbow,.21f,Sugar,1);
                Bar(elbow,Mast(2.1f,17,-.6f),.16f,Sugar,1);
                Bar(elbow,Mast(-2.7f,14,-.4f),.1f,Sugar,1);
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
                }
                if(ground)
                {
                    var basin=visual.InverseTransformPoint(frame.TransformPoint(new Vector3(0,.64f,2)));
                    var q=Quaternion.Inverse(visual.rotation)*frame.rotation;
                    mesh.Box(basin,new Vector3(10.05f,.16f,5.05f),q,0,enamel);
                    mesh.Box(basin+q*Vector3.up*.11f,new Vector3(9.5f,.12f,4.55f),q,0,water);
                    for(int ring=0;ring<3;ring++)
                        mesh.Cylinder(basin+q*Vector3.up*(.18f+ring*.025f),q,2.1f-ring*.61f,.045f,0,ring%2==0?Sugar:water,30);
                }
                if(purpose.EndsWith(" floor 7"))
                {
                    // Sit the collector on the solid half of the deck, clear of the stair opening.
                    float centerZ=-depth;
                    var offset=visual.InverseTransformVector(frame.TransformVector(Vector3.forward*4));
                    var center=P(0,roof+.12f,centerZ)+offset;
                    for(int i=0;i<4;i++)
                    {
                        var q=rotation*Quaternion.Euler(0,90*i,0);
                        Vector3 Rim(float x,float y,float z)=>center+q*new Vector3(x,y,z);
                        var a=Rim(-2,0,2); var b=Rim(2,0,2);
                        var c=Rim(5.5f,2.8f,5.5f); var d=Rim(-5.5f,2.8f,5.5f);
                        var normal=Vector3.Cross(d-a,b-a).normalized;
                        mesh.Panel(a,d,c,b,normal,normal,normal,normal,water);
                        Bar(c,d,.52f,enamel);
                        Bar(a,d,.38f,enamel);
                    }
                    mesh.Cylinder(center,rotation,2.8f,.18f,0,water,20);
                    for(int puff=-2;puff<=2;puff++)
                    {
                        float y=roof+9.5f+.9f*(2-Mathf.Abs(puff));
                        mesh.Sphere(P(puff*2.2f,y,centerZ)+offset,new Vector3(3.1f,2.55f,3.1f),0,Sugar,10,16);
                    }
                    for(int drop=0;drop<15;drop++)
                    {
                        float angle=drop*2.4f, radius=1.3f+drop%3;
                        var start=P(Mathf.Cos(angle)*radius,roof+3.4f+drop%3,centerZ+Mathf.Sin(angle)*radius)+offset;
                        Bar(start,start+rotation*new Vector3(.26f,2.6f,0),.12f,water,1);
                    }
                }
            }
        }
    }
}

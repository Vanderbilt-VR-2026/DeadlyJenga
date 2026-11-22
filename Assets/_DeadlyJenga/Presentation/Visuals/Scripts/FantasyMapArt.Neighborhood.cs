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
                    Box(side*4,.55f,1.15f,2.5f,1.05f,1.4f,wood);
                    if(grocery)
                        for(int row=0;row<2;row++) for(int col=0;col<5;col++)
                            Ball(side*4+(col-2)*.44f,1.25f,.85f+row*.57f,.27f,side<0?lime:Gold);
                }
                if(grocery){Ball(-.72f,4.3f,.24f,.82f,CaneRed);Ball(.72f,4.3f,.24f,.78f,Gold);}
            }
            else if(site.StartsWith("Sugar") && purpose.Contains(" floor "))
            {
                if(site=="Sugar Market east annex" && purpose.EndsWith(" floor 7"))
                {
                    float y=roof+1.55f;
                    mesh.Cylinder(P(0,y,0),rotation*Quaternion.Euler(90,0,0),2.15f,.28f,0,cream,32);
                    Box(0,y+.55f,.34f,.18f,1.1f,.09f,wood);
                    Box(.42f,y,.36f,.84f,.18f,.09f,wood);
                    for(int hour=0;hour<12;hour++)
                    {
                        float angle=hour*Mathf.PI/6;
                        Ball(1.82f*Mathf.Sin(angle),y+1.82f*Mathf.Cos(angle),.32f,.1f,wood);
                    }
                }
                foreach(int side in new[]{-1,1})
                {
                    Box(side*width*.58f,2,0,3.8f,1.8f,.13f,cream);
                    Box(side*width*.58f,2,.09f,3.48f,1.5f,.12f,new Color(.52f,.81f,.86f));
                    Box(side*width*.58f,2,.18f,.16f,1.6f,.12f,cream);
                    Box(side*width*.5f,1.1f,-3,2.1f,.2f,1.2f,wood);
                }
            }
            else if(site.StartsWith("Grape") && purpose=="Independent bookfall support spine")
            {
                for(float y=2.1f;y<roof;y+=4)
                    foreach(int side in new[]{-1,1})
                    {
                        float x=side*width*.57f;
                        Box(x,y,0,2.9f,2.3f,.16f,cream);
                        Box(x,y,.12f,2.52f,1.97f,.12f,new Color(.97f,.71f,.39f));
                        Box(x,y,.21f,.16f,2,.12f,cream);
                        Box(x,y-1.05f,.33f,2.9f,.4f,.75f,wood);
                        for(int i=-1;i<=1;i++) Ball(x+i*.8f,y-.76f,.42f,.35f,lime);
                    }
            }
            else if(site.StartsWith("Downtown") && purpose=="Escape spine" && top)
            {
                mesh.Cylinder(P(width*.68f,roof,-1.6f),rotation,.85f,.65f,0,cream,12);
                mesh.Cylinder(P(width*.68f,roof+.65f,-1.6f),rotation,.14f,3.9f,0,wood,8);
                Ball(width*.68f,roof+4.5f,-1.6f,.35f,Gold);
            }
            else if(site=="Canyon south-east tower" && purpose.Contains(" floor "))
            {
                if(ground)
                {
                    Box(width*.48f,.35f,-2.8f,4.5f,.55f,3.4f,cream);
                    Box(width*.48f,.65f,-2.8f,4.1f,.12f,3,new Color(.1f,.82f,.94f));
                    foreach(int side in new[]{-1,1}) mesh.Cylinder(P(side*(width-1),0,.4f),rotation,.32f,3.8f,0,new Color(.35f,.86f,.99f),10);
                }
                if(purpose.EndsWith(" floor 7"))
                {
                    float x=width*.55f;
                    for(int i=0;i<4;i++)
                    {
                        var q=rotation*Quaternion.Euler(0,90*i,0);
                        var center=P(x,roof+.8f,-depth*.65f)+q*Vector3.forward*1.8f;
                        mesh.Box(center,new Vector3(4.6f,.22f,1.9f),q*Quaternion.Euler(-24,0,0),0,cream);
                    }
                    for(int i=-2;i<=2;i++)
                    {
                        mesh.Sphere(P(x+i*.96f,roof+5.3f+.35f*(2-Mathf.Abs(i)),-depth*.65f),new Vector3(1.45f,1.1f,1.4f),0,Sugar,8,14);
                        mesh.Cylinder(P(x+i*.73f,roof+1.6f,-depth*.65f),rotation,.045f,2.7f,1,new Color(.4f,.8f,1),6);
                    }
                }
            }
        }
    }
}

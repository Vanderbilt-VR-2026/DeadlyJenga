using System.Collections.Generic;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    public sealed partial class MayhemMarina
    {
        void ShoreAndCourts()
        {
            mesh.ArcSlab(new Vector3(0,-1.6f,4),new Vector2(9,11),new Vector2(24,21),2.8f,-110,120,F.Rock,true,72);
            mesh.ArcSlab(new Vector3(0,1.12f,4),new Vector2(9,11),new Vector2(23.8f,20.8f),.13f,-110,120,F.Stone,true,72);
            mesh.Box(new Vector3(0,.6f,10),new Vector3(16,1.2f,15),F.Ivory,.28f,true);
            mesh.Box(new Vector3(0,1.22f,10),new Vector3(16,.13f,15),F.Stone,.045f,true);
            mesh.Box(new Vector3(-14,.6f,8),new Vector3(11,1.2f,12),F.Ivory,.4f,true);
            mesh.Box(new Vector3(-14,1.22f,8),new Vector3(11,.13f,12),F.Stone,.04f,true);
            mesh.Box(new Vector3(14,.6f,7),new Vector3(12,1.2f,11),F.Ivory,.4f,true);
            mesh.Box(new Vector3(14,1.22f,7),new Vector3(12,.13f,11),F.Stone,.04f,true);
            mesh.Disk(new Vector3(-10,-.8f,-8),5.55f,2,F.Rock,true);
            mesh.Disk(new Vector3(-10,1.2f,-8),5.35f,.13f,F.Stone,true);
            mesh.ArcSlab(new Vector3(-10,1.34f,-8),Vector2.one*4.9f,Vector2.one*5.3f,.10f,15,250,F.Wood,false,48);
            // Individual copings give the water's edge a rounded masonry rhythm.
            for(int i=0;i<30;i++)
            {
                float a=Mathf.Lerp(-128,123,i/29f)*Mathf.Deg2Rad;
                var p=new Vector3(Mathf.Sin(a)*8.85f,1.23f,4+Mathf.Cos(a)*10.85f);
                mesh.Box(p,new Vector3(1.05f,.24f,.48f),F.Ivory,.07f,false,Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0));
            }
            Bridge(new Vector3(-18,1.24f,-25),new Vector3(-10,1.33f,-12.3f),3.6f,0);
            Bridge(new Vector3(-6.9f,1.33f,-5.1f),new Vector3(-3,1.285f,3.15f),4.4f,.17f);
            mesh.Box(new Vector3(-8.27f,1.225f,3.15f),new Vector3(1.12f,.12f,2.65f),F.Stone,0,true);
            mesh.Box(new Vector3(0,1.294f,3.1f),new Vector3(15.4f,.007f,1.1f),F.Ivory,.002f);
            mesh.Disk(new Vector3(17,-.7f,-8),5.5f,1,F.Rock,true);
            mesh.Disk(new Vector3(17,.27f,-8),4.82f,.11f,F.Stone,true);
            mesh.ArcSlab(new Vector3(17,.3f,-8),Vector2.one*4.8f,Vector2.one*5.45f,.78f,0,360,F.Ivory,true,64);
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;var p=new Vector3(17+Mathf.Sin(a)*5.12f,1.08f,-8+Mathf.Cos(a)*5.12f);
                mesh.Box(p,new Vector3(1.22f,.15f,.68f),F.Stone,.05f,false,Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0));
            }
            Bridge(new Vector3(15,1.285f,2.6f),new Vector3(17,1.155f,-2.86f),2.8f,0);
            Ramp(new Vector3(17,1.155f,-3.18f),new Vector3(17,.38f,-6.05f),2.1f,false);
            // A few blocks already in the basin make its purpose legible before anyone interacts.
            for(int i=0;i<6;i++)
            {
                float a=i*2.39f;var p=new Vector3(17+Mathf.Cos(a)*3.3f,.59f,-8+Mathf.Sin(a)*3.3f);
                mesh.Box(p,new Vector3(1.2f,.48f,.68f),(F)((int)F.Red+i%3),.09f,false,Quaternion.Euler(i*9,i*43,8));
            }
            TileLines(new Vector3(0,1.292f,8),new Vector2(15,10));
            TileLines(new Vector3(-14,1.292f,7),new Vector2(10,10));
            for(int i=0;i<5;i++)
            {
                float a=i*2.21f;Rock(new Vector3(-10+Mathf.Cos(a)*5.35f,-.05f,-8+Mathf.Sin(a)*5.35f),new Vector3(1.7f,.9f,1.3f),i);
                Rock(new Vector3(17+Mathf.Cos(a)*5.6f,-.2f,-8+Mathf.Sin(a)*5.6f),new Vector3(1.2f,.8f,1.5f),i+9);
            }
        }
        void MarshAndBeach()
        {
            var center = new Vector3(0, .02f, 4);
            mesh.ArcSlab(center, new Vector2(20, 17), new Vector2(27.8f, 24.7f), .28f, 67, 111, F.Sand, true, 28);
            mesh.ArcSlab(center, new Vector2(20, 17), new Vector2(27.2f, 24.0f), .34f, -105, -62, F.Sand, true, 28);
            mesh.ArcSlab(center, new Vector2(20, 18), new Vector2(27.5f, 25.7f), .37f, -34, 32, F.Sand, true, 38);
            Ramp(new Vector3(23.12f, 1.25f, 8.8f), new Vector3(26.1f, .30f, 9.2f), 2.7f, false);
            Ramp(new Vector3(-23.12f, 1.25f, 8.8f), new Vector3(-25.8f, .36f, 9.25f), 2.7f, false);
            Ramp(new Vector3(1.1f, 1.25f, 24.62f), new Vector3(1.1f, .39f, 28.4f), 2.7f, false);
            for (int i = 0; i < 22; i++)
            {
                float angle = Mathf.Lerp(-32, 30, i / 21f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(angle) * 26, .39f, 4 + Mathf.Cos(angle) * 24.35f);
                if (Mathf.Abs(p.x - 1.1f) < 1.6f) continue;
                for (int stalk = 0; stalk < 5; stalk++)
                {
                    float turn = i * 2.4f + stalk * 1.26f;
                    var lean = new Vector3(Mathf.Cos(turn), 0, Mathf.Sin(turn));
                    var foot = p + lean * .17f;
                    mesh.Beam(foot, foot + Vector3.up * (.38f + stalk * .10f) + lean * .21f, .024f, stalk % 2 == 0 ? F.Leaf : F.LeafLight);
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float x = 25.5f * (i < 4 ? -1 : 1);
                float z = -1.4f + i % 4 * 2.8f;
                Rock(new Vector3(x, .31f, z), new Vector3(.72f, .33f, .57f), i);
            }
        }
        void JetSkiDock()
        {
            mesh.Box(new Vector3(-20.8f, 1.12f, -25.0f), new Vector3(6.8f, .24f, 2.8f), F.Wood, .045f, true);
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3(-23.9f + i * 1.9f, 1.24f, -26.36f);
                mesh.Beam(point - Vector3.up * 1.65f, point + Vector3.up * .32f, .12f, F.DarkWood);
                mesh.Disk(point + Vector3.up * .28f, .15f, .065f, F.Brass, false, 14);
            }
            DirectionBoard(new Vector3(-22.7f, 1.24f, -24.2f), Quaternion.Euler(0, 180, 0), 2.8f);
        }

        static readonly Vector3 BeaconWalkStart = new(-5.65f, 1.33f, -8.45f);
        static readonly Vector3 BeaconWalkEnd = new(11.5f, 1.285f, 1.95f);
        static readonly Quaternion BeaconWalkFacing = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(BeaconWalkEnd - BeaconWalkStart, Vector3.up));
        Vector3 BeaconWalkGate => BeaconWalkStart + BeaconWalkFacing * new Vector3(0, 3.16f, .65f);

        void BeaconWalk()
        {
            // A single planar walking surface avoids small joints under the survivor capsule.
            Bridge(BeaconWalkStart, BeaconWalkEnd, 3.65f, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                var edge = BeaconWalkFacing * Vector3.right * (side * 1.93f);
                mesh.Box(BeaconWalkGate + edge - Vector3.up * 1.50f,
                    new Vector3(.24f, 3.45f, .24f), F.DarkWood, .04f, true, BeaconWalkFacing);
                mesh.Box(BeaconWalkGate + edge - Vector3.up * .44f,
                    new Vector3(.36f, .32f, .36f), F.Coral, .05f, false, BeaconWalkFacing);
            }
            mesh.Box(BeaconWalkGate, new Vector3(4.12f, .62f, .23f), F.DarkWood, .07f, false, BeaconWalkFacing);
            mesh.Box(BeaconWalkGate + Vector3.up * .34f, new Vector3(4.28f, .08f, .33f), F.Brass, .025f, false, BeaconWalkFacing);
        }

        void DirectionBoard(Vector3 basePosition, Quaternion facing, float width)
        {
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(basePosition + facing * Vector3.right * (side * (width * .5f - .25f)) + Vector3.up * .64f,
                    new Vector3(.14f, 1.28f, .19f), F.DarkWood, .035f, false, facing);
            mesh.Box(basePosition + Vector3.up * 1.35f, new Vector3(width, .64f, .14f), F.Teal, .065f, false, facing);
        }
        void SignMounts()
        {
            mesh.Box(new Vector3(13.7f, 4.42f, 2.60f), new Vector3(6.5f, .53f, .2f), F.DarkWood, .065f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 13.7f + side * 2.55f;
                mesh.Beam(new Vector3(x, 4.57f, 2.63f), new Vector3(x, 4.98f, 2.83f), .047f, F.Brass);
            }
            mesh.Box(new Vector3(19, 1.45f, -3.34f), new Vector3(.14f, .64f, .14f), F.DarkWood, .024f);
            mesh.Box(new Vector3(19, 1.76f, -3.34f), new Vector3(2.28f, .42f, .18f), F.DarkWood, .05f);
            mesh.Box(new Vector3(-10, 1.340f, -10.1f), new Vector3(5.1f, .018f, .8f), F.Ivory, .008f);
            DirectionBoard(new Vector3(-9.2f, 1.33f, -5.0f), Quaternion.Euler(0, 12, 0), 2.5f);
            DirectionBoard(new Vector3(6.65f, 1.285f, 3.64f), Quaternion.identity, 2.55f);
        }
        void LightingFixtures()
        {
            Lantern(new Vector3(-13.8f, 6.65f, 17.9f), .24f);
            DeckLight(new Vector3(-12.5f, 1.33f, -10.25f));
            DeckLight(new Vector3(-7.5f, 1.33f, -10.25f));
            DeckLight(new Vector3(15, 1.14f, -3.3f));
            DeckLight(new Vector3(16.5f, 4.03f, 17.75f));
        }
        void DeckLight(Vector3 foot)
        {
            mesh.Box(foot + Vector3.up * .25f, new Vector3(.17f, .5f, .17f), F.DarkWood, .025f);
            mesh.Box(foot + Vector3.up * .54f, new Vector3(.20f, .12f, .20f), F.Glow, .018f);
            mesh.Box(foot + Vector3.up * .625f, new Vector3(.24f, .05f, .24f), F.Brass, .015f);
        }
        void TileLines(Vector3 center,Vector2 area)
        {
            for(float x=-area.x*.5f+.8f;x<area.x*.5f;x+=1.55f)mesh.Box(center+Vector3.right*x,new Vector3(.013f,.006f,area.y),F.Ivory,.001f);
            for(float z=-area.y*.5f+.7f;z<area.y*.5f;z+=1.55f)mesh.Box(center+Vector3.forward*z,new Vector3(area.x,.006f,.013f),F.Ivory,.001f);
        }
        void Bridge(Vector3 from,Vector3 to,float width,float arch)
        {
            Vector3 direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized,side=Vector3.Cross(Vector3.up,direction);float length=Vector3.Distance(from,to);var q=Quaternion.LookRotation(direction);
            Vector3 P(float t)=>Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*arch;
            int planks=Mathf.CeilToInt(length/.29f);
            for(int i=0;i<planks;i++)
            {
                float t=(i+.5f)/planks;var slope=(P(Mathf.Min(1,t+.01f))-P(Mathf.Max(0,t-.01f))).normalized;
                var pose=Quaternion.LookRotation(slope);
                mesh.Box(P(t)-pose*Vector3.up*.07f,new Vector3(width,.14f,length/planks+.009f),F.Wood,.018f,false,pose);
                foreach(int sign in new[]{-1,1})mesh.Ellipsoid(P(t)+side*(sign*(width*.5f-.12f))+Vector3.up*.007f,new Vector3(.025f,.008f,.025f),F.Brass,rings:4,sides:8);
            }
            int spans=arch>0?12:1;
            for(int i=0;i<spans;i++)
                mesh.WalkStrip(P(i/(float)spans),P((i+1f)/spans),side*(width*.5f));
            int posts=Mathf.CeilToInt(length/2.1f);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var ropeLow=new List<Vector3>();var ropeHigh=new List<Vector3>();
                for(int i=0;i<=posts;i++)
                {
                    float t=i/(float)posts;var p=P(t)+side*(sign*(width*.5f+.08f));Post(p);
                    if(i%2==0)
                    {
                        mesh.Beam(p-Vector3.up*.15f,p-Vector3.up*2.1f,.14f,F.DarkWood);
                        mesh.Box(p+Vector3.up*1.19f,new Vector3(.16f,.07f,.16f),F.Glow,.015f);
                        mesh.Box(p+Vector3.up*1.24f,new Vector3(.21f,.035f,.21f),F.Brass,.014f);
                    }
                    for(int s=0;s<10;s++)
                    {
                        float u=s/10f;if(i==posts&&s>0)break;
                        float at=(i+u)/posts;var r=P(Mathf.Min(1,at))+side*(sign*(width*.5f+.08f));float sag=Mathf.Sin(u*Mathf.PI)*.13f;
                        ropeLow.Add(r+Vector3.up*(.55f-sag));ropeHigh.Add(r+Vector3.up*(1.0f-sag));
                    }
                }
                mesh.Tube(ropeLow,.035f,F.Wood,7);mesh.Tube(ropeHigh,.045f,F.Wood,7,true);
            }
        }
        void Post(Vector3 p)
        {
            mesh.Box(p+Vector3.up*.48f,new Vector3(.19f,1.03f,.19f),F.Wood,.025f,true);
            mesh.Box(p+Vector3.up*.91f,new Vector3(.27f,.35f,.27f),F.Ivory,.06f);
            mesh.Box(p+Vector3.up*1.1f,new Vector3(.28f,.13f,.28f),F.Purple,.05f);
        }
        void Pavilion()
        {
            Ramp(new Vector3(0,1.285f,3.85f),new Vector3(0,1.86f,5.05f),9.2f,false);
            // Keep the raised platform outside the west passage, where the ramp meets it flush.
            mesh.Box(new Vector3(.6f,1.56f,8.45f),new Vector3(11.8f,.6f,6.9f),F.Stone,0,true);
            mesh.Box(new Vector3(-5.9f,1.56f,5.5f),new Vector3(1.2f,.6f,1.0f),F.Stone,0,true);
            mesh.Box(new Vector3(-5.9f,1.56f,10.55f),new Vector3(1.2f,.6f,2.7f),F.Stone,0,true);
            WardrobeConnector();
            mesh.Box(new Vector3(0,3.7f,11.65f),new Vector3(13.3f,4.0f,.5f),F.Teal,.2f,true);
            for(int side=-1;side<=1;side+=2)
            {
                if(side>0)mesh.Box(new Vector3(side*6.55f,3.1f,9.0f),new Vector3(1.5f,3.9f,5.1f),F.Ivory,.4f,true);
                else mesh.Box(new Vector3(-6.55f,3.1f,10.375f),new Vector3(1.5f,3.9f,2.35f),F.Ivory,.4f,true);
                for(int n=0;n<4;n++)mesh.Box(new Vector3(side*5.7f,1.64f+n*.61f,5.05f),new Vector3(1.0f,.58f,1),n==0?F.Red:n==1?F.Green:F.Purple,.11f,true,Quaternion.Euler(0,n%2*90,0));
                mesh.Beam(new Vector3(side*5.7f,3.95f,5.1f),new Vector3(side*5.7f,5.0f,5.1f),.14f,F.Wood,true);
                mesh.Beam(new Vector3(side*5.7f,4.65f,5.1f),new Vector3(side*4.4f,5.7f,5.1f),.12f,F.Wood);
                Pot(new Vector3(side*5.15f,1.86f,side<0?10.05f:9.65f),.43f,.74f,side+3);
                Lantern(new Vector3(side*4.2f,4.75f,5.1f),.2f);
            }
            mesh.CurvedRoof(new Vector3(0,4.8f,8.3f),15.6f,9.1f,2.15f,F.Teal);
            mesh.Box(new Vector3(0,5.58f,3.71f),new Vector3(5.5f,.95f,.24f),F.DarkWood,.085f);
            for(int side=-1;side<=1;side+=2)
                mesh.Beam(new Vector3(side*2,5.98f,3.77f),new Vector3(side*2,6.73f,3.77f),.055f,F.Brass);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*6.75f;
                mesh.Box(new Vector3(x,1.46f,4.65f),new Vector3(.94f,.34f,.94f),F.Stone,.08f,true);
                mesh.Beam(new Vector3(x,1.55f,4.65f),new Vector3(x,5.55f,4.65f),.18f,F.DarkWood,true);
                mesh.Beam(new Vector3(x,4.72f,4.65f),new Vector3(side*5.92f,5.65f,4.65f),.13f,F.DarkWood);
                Lantern(new Vector3(x,3.42f,4.23f),.16f);
            }
            // The screen frame is real architecture; the live controls are supplied by the lobby view.
            mesh.Box(new Vector3(0,3.9f,11.32f),new Vector3(7.75f,3.9f,.23f),F.Wood,.17f);
            mesh.Box(new Vector3(0,3.9f,11.17f),new Vector3(7.38f,3.57f,.10f),F.Ivory,.18f);
            mesh.Box(new Vector3(0,3.9f,11.10f),new Vector3(7.12f,3.31f,.05f),F.Teal,.13f);
            var keyboardStand = new Vector3(CodeKeyboard.x, 2.28f, CodeKeyboard.z + .08f);
            mesh.Box(keyboardStand, new Vector3(.47f, .84f, .48f), F.DarkWood, .07f, true);
            mesh.Box(CodeKeyboard + CodeKeyboardFacing * Vector3.forward * .178f,
                new Vector3(1.96f, .88f, .13f), F.DarkWood, .065f, false, CodeKeyboardFacing);
            Lantern(new Vector3(0,5.15f,7),.24f);
        }
        void WardrobeConnector()
        {
            var from=new Vector3(-10,1.285f,7.6f);
            var to=new Vector3(-5.3f,1.86f,7.6f);
            var rotation=Quaternion.LookRotation((to-from).normalized);
            // Offset along the slab normal, so its top meets both existing flat landings exactly.
            mesh.Box((from+to)*.5f-rotation*Vector3.up*.11f,new Vector3(3.2f,.22f,Vector3.Distance(from,to)),F.Stone,0,true,rotation);
        }
        void StyleShop()
        {
            Vector3 c=new(-13,1.3f,7.7f);
            Portal(c,9.2f,5.6f,5.2f,true);
            mesh.Box(c+new Vector3(0,2.0f,2.8f),new Vector3(8.6f,4.1f,.3f),F.Wood,.18f,true);
            mesh.Box(c+new Vector3(0,3.88f,-2.7f),new Vector3(3.7f,.75f,.27f),F.Wood,.12f);
            var styleFrame = StyleScreen + StyleScreenFacing * Vector3.forward * .12f;
            mesh.Box(styleFrame, new Vector3(2.48f, 2.91f, .16f), F.DarkWood, .09f, true, StyleScreenFacing);
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = styleFrame + StyleScreenFacing * Vector3.right * (side * .95f);
                foot.y = 1.58f;
                mesh.Box(foot, new Vector3(.16f, .59f, .20f), F.Brass, .035f, true, StyleScreenFacing);
            }
            // The illuminated oval mirror is centered in the open wardrobe, as in the approved render.
            var ring=new List<Vector3>();for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;ring.Add(c+new Vector3(Mathf.Cos(a)*1.38f,2.1f+Mathf.Sin(a)*1.9f,2.42f));}
            mesh.Tube(ring,.14f,F.Wood,10);mesh.Tube(ring,.065f,F.Glow,10);
            for(int i=0;i<20;i++){float a=i*Mathf.PI/10;mesh.Ellipsoid(c+new Vector3(Mathf.Cos(a)*1.4f,2.1f+Mathf.Sin(a)*1.91f,2.28f),Vector3.one*.06f,F.Glow,rings:5,sides:8);}
            mesh.Disk(new Vector3(-13,1.35f,8.2f),1.32f,.18f,F.Stone,true,40);
            for(int side=-1;side<=1;side+=2)
            {
                for(int shelf=0;shelf<3;shelf++)mesh.Box(c+new Vector3(side*3.12f,1.05f+shelf*1.02f,2.12f),new Vector3(1.75f,.12f,.68f),F.Wood,.04f);
                for(int j=0;j<3;j++)
                {
                    var p=c+new Vector3(side*3.1f+(j-1)*.53f,2.93f,1.94f);
                    mesh.Disk(p,.24f,.05f,F.Metal,false,20);mesh.Disk(p+Vector3.up*.05f,.15f,.28f,(F)((int)F.Red+(j+1)%3),false,20);
                }
                float railZ=side>0?1.75f:1.42f;
                var railA=c+new Vector3(side*3.2f-.7f,2.0f,railZ);var railB=c+new Vector3(side*3.2f+.7f,2.0f,railZ);mesh.Beam(railA,railB,.027f,F.Brass);
                for(int j=0;j<4;j++)
                {
                    var p=c+new Vector3(side*3.2f+(j-1.5f)*.34f,1.4f,railZ-.02f);var color=j%2==0?F.MintFabric:F.CoralFabric;
                    mesh.Box(p,new Vector3(.3f,.85f,.12f),color,.06f);mesh.Box(p+new Vector3(-.18f,.24f,0),new Vector3(.16f,.5f,.12f),color,.05f,false,Quaternion.Euler(0,0,-14));mesh.Box(p+new Vector3(.18f,.24f,0),new Vector3(.16f,.5f,.12f),color,.05f,false,Quaternion.Euler(0,0,14));
                }
            }
            Pot(new Vector3(-18.1f,1.29f,3.2f),.56f,.9f,4);Pot(new Vector3(-7.8f,1.29f,3.8f),.45f,.8f,5);
        }
        void Portal(Vector3 center,float width,float height,float depth,bool wardrobePassage=false)
        {
            float half=width*.5f;
            for(int side=-1;side<=1;side+=2)
            {
                if(side<0||!wardrobePassage)
                    mesh.Box(center+new Vector3(side*(half-.5f),height*.42f,0),new Vector3(1.05f,height*.85f,depth),F.Ivory,.4f,true);
                else
                {
                    float near=center.z-depth*.5f,far=center.z+depth*.5f;
                    mesh.Box(new Vector3(center.x+half-.5f,center.y+height*.42f,(near+6f)*.5f),new Vector3(1.05f,height*.85f,6f-near),F.Ivory,.4f,true);
                    mesh.Box(new Vector3(center.x+half-.5f,center.y+height*.42f,(9.2f+far)*.5f),new Vector3(1.05f,height*.85f,far-9.2f),F.Ivory,.4f,true);
                }
            }
            var arch=new List<Vector3>();
            for(int i=0;i<=40;i++){float a=Mathf.PI-i*Mathf.PI/40;arch.Add(center+new Vector3(Mathf.Cos(a)*(half-.45f),height*.59f+Mathf.Sin(a)*height*.35f,-depth*.5f));}
            mesh.Tube(arch,.53f,F.Ivory,16,true);
            mesh.CurvedRoof(center+new Vector3(0,height*.57f,0),width,depth,height*.35f,F.Ivory);
        }
        void Lounge()
        {
            Vector3 c=new(13.7f,1.3f,6.0f);
            mesh.Box(c+new Vector3(0,.48f,3.0f),new Vector3(10.4f,.96f,.45f),F.Wood,.14f,true);
            for(int pane=0;pane<3;pane++)
            {
                float x=(pane-1)*3.25f;
                mesh.Box(c+new Vector3(x,2.05f,3.0f),new Vector3(2.95f,2.14f,.08f),F.Glass,.025f,true);
                mesh.Box(c+new Vector3(x-1.56f,2.08f,3.0f),new Vector3(.18f,2.4f,.24f),F.Wood,.04f,true);
                mesh.Box(c+new Vector3(x,3.25f,3.0f),new Vector3(3.25f,.15f,.24f),F.Wood,.035f);
                mesh.Beam(c+new Vector3(x,1.02f,2.94f),c+new Vector3(x,3.2f,2.94f),.025f,F.Brass);
            }
            mesh.Box(c+new Vector3(5,1.65f,1),new Vector3(.65f,3.3f,4.3f),F.Ivory,.2f,true);
            mesh.CurvedRoof(c+new Vector3(0,3.5f,.2f),12.2f,7.5f,.68f,F.DarkWood);
            for(int side=-1;side<=1;side+=2)
            {
                mesh.Beam(c+new Vector3(side*4.8f,0,-2),c+new Vector3(side*4.8f,3.7f,-2),.15f,F.Wood,true);
                Lantern(c+new Vector3(side*3.7f,2.8f,-1.8f),.22f);
            }
            Sofa(c+new Vector3(0,0,1.7f),5.7f,Quaternion.identity);
            Sofa(c + new Vector3(3.6f, 0, .05f), 2.5f, Quaternion.Euler(0, 90, 0));
            Sofa(c + new Vector3(-3.45f, 0, .65f), 2.5f, Quaternion.Euler(0, -90, 0));
            Table(c + new Vector3(.45f, 0, .05f), 1.18f);
            for(int i=0;i<3;i++)mesh.Box(c+new Vector3(-3.8f,1.1f+i*.55f,2.7f),new Vector3(2,.09f,.55f),F.Wood,.03f);
            Pot(c+new Vector3(-4.6f,0,1.65f),.42f,.7f,10);
            Pot(c+new Vector3(4.5f,0,-2.25f),.5f,.78f,11);
            // The front social island has another curved bank of seating.
            for(int i=0;i<4;i++)
            {
                float angle=Mathf.Lerp(130,205,i/3f)*Mathf.Deg2Rad;var p=new Vector3(-10+Mathf.Cos(angle)*4.0f,1.33f,-8+Mathf.Sin(angle)*4.0f);
                Sofa(p,1.35f,Quaternion.Euler(0,90-angle*Mathf.Rad2Deg,0));
            }
        }
        void Sofa(Vector3 p,float width,Quaternion q)
        {
            void B(Vector3 d,Vector3 s,F color,float bevel=.10f,bool solid=false)=>mesh.Box(p+q*d,s,color,bevel,solid,q);
            B(new Vector3(0,.25f,0),new Vector3(width,.5f,1.25f),F.Wood,.15f,true);
            B(new Vector3(0,.60f,0),new Vector3(width-.24f,.27f,1.0f),F.CreamFabric,.10f,true);
            B(new Vector3(0,1.05f,.48f),new Vector3(width-.12f,.78f,.27f),F.CreamFabric,.11f,true);
            for(int side=-1;side<=1;side+=2)B(new Vector3(side*(width*.5f-.10f),.65f,0),new Vector3(.22f,.58f,1.25f),F.Wood,.08f,true);
            int cushions=Mathf.Max(1,Mathf.RoundToInt(width/1.4f));
            for(int i=0;i<cushions;i++)
            {
                float x=(i-(cushions-1)*.5f)*1.05f;
                mesh.Box(p+q*new Vector3(x,.95f,.25f),new Vector3(.62f,.6f,.20f),i%2==0?F.MintFabric:F.CoralFabric,.12f,false,q*Quaternion.Euler(-12,0,6));
            }
        }
        void Table(Vector3 p,float radius)
        {
            mesh.Disk(p+Vector3.up*.08f,radius*.63f,.45f,F.Wood,true,28);mesh.Disk(p+Vector3.up*.53f,radius,.15f,F.Wood,true,40);
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;var c=p+new Vector3(Mathf.Cos(a)*radius*.5f,.73f,Mathf.Sin(a)*radius*.5f);
                mesh.Disk(c,.1f,.16f,F.Ivory,false,12);
            }
        }
        void UpperTerraces()
        {
            mesh.Box(new Vector3(-12,2.7f,17),new Vector3(13,2.6f,8),F.Ivory,.5f,true);
            mesh.Box(new Vector3(-12,4.05f,17),new Vector3(13,.18f,8),F.Stone,.06f,true);
            mesh.Box(new Vector3(-1,3.4f,18),new Vector3(10,1.1f,6),F.Ivory,.4f,true);
            mesh.Box(new Vector3(-1,4.05f,18),new Vector3(10,.18f,6),F.Stone,.06f,true);
            Stairs(new Vector3(-8.0f,1.3f,9.5f),new Vector3(-8.0f,4.14f,16.5f),2.3f);
            Ramp(new Vector3(8.2f,1.285f,9.55f),new Vector3(8.2f,4.03f,16.85f),2.4f,false);
            mesh.Box(new Vector3(8.3f,3.94f,17.75f),new Vector3(2.85f,.18f,1.85f),F.Wood,.025f,true);
            // A broad side ramp gives the upper gathering terrace a route without jumps.
            Ramp(new Vector3(-21.0f,1.28f,2.4f),new Vector3(-21.0f,4.14f,17.4f),2.5f);
            mesh.Box(new Vector3(-19.7f,4.05f,19.24f),new Vector3(5,.18f,3.72f),F.Stone,0,true);
            for(int i=0;i<4;i++)
            {
                float x=-17+i*3.2f;mesh.Beam(new Vector3(x,4.15f,17.9f),new Vector3(x,7.2f,17.9f),.15f,F.Wood,true);
                mesh.Beam(new Vector3(x,7.2f,14),new Vector3(x,7.2f,20),.17f,F.Wood);
            }
            mesh.Beam(new Vector3(-17,7.2f,17.9f),new Vector3(-7.4f,7.2f,17.9f),.18f,F.Wood);
            for(int i=0;i<8;i++)mesh.Box(new Vector3(-12.1f,7.24f,14+i*.82f),new Vector3(11,.1f,.25f),F.Wood,.025f);
            Sofa(new Vector3(-14,4.15f,18.4f),4.3f,Quaternion.identity);Table(new Vector3(-14,4.15f,16.6f),1.1f);
            Rail(new Vector3(-18,4.15f,13.1f),new Vector3(-9.5f,4.15f,13.1f));
            Rail(new Vector3(-6.4f,4.15f,14),new Vector3(4.0f,4.15f,14));
            Rail(new Vector3(4.0f,4.15f,14),new Vector3(4.0f,4.15f,16.25f));
            Rail(new Vector3(4.0f,4.15f,19.65f),new Vector3(4.0f,4.15f,20.85f));
            mesh.Box(new Vector3(14,2.6f,15.8f),new Vector3(10,2.5f,6),F.Ivory,.3f,true);
            mesh.Box(new Vector3(14,3.94f,15.8f),new Vector3(10,.18f,6),F.Wood,.04f,true);
            Bridge(new Vector3(3.72f,4.14f,17.95f),new Vector3(9.4f,4.03f,17.95f),2.65f,.04f);
            Rail(new Vector3(10.1f,4.04f,13.2f),new Vector3(18.8f,4.04f,13.2f));
            for(int i=0;i<6;i++)
            {
                float x=10.5f+i*1.4f;mesh.Box(new Vector3(x,5.0f,18.3f),new Vector3(1.15f,1.8f,.22f),i%2==0?F.Teal:F.Wood,.10f,true);
                Target(new Vector3(x,5.2f,18.14f),.41f);
            }
            mesh.Box(new Vector3(14,4.8f,16.9f),new Vector3(8.4f,.18f,.8f),F.Wood,.08f,true);
        }
        void Stairs(Vector3 from,Vector3 to,float width)
        {
            int count=Mathf.CeilToInt((to.y-from.y)/.17f);Vector3 direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;float run=Vector3.ProjectOnPlane(to-from,Vector3.up).magnitude;var q=Quaternion.LookRotation(direction);
            for(int i=0;i<count;i++){float top=Mathf.Lerp(from.y,to.y,(i+1f)/count);var p=Vector3.Lerp(from,to,(i+.5f)/count);p.y=(top+from.y-.15f)*.5f;mesh.Box(p,new Vector3(width,top-from.y+.15f,run/count+.02f),F.Stone,.026f,true,q);}
            for(int side=-1;side<=1;side+=2){var offset=q*Vector3.right*(side*(width*.5f+.14f));Rail(from+offset,to+offset);}
        }
        void Ramp(Vector3 from,Vector3 to,float width,bool guarded=true)
        {
            var slope=(to-from).normalized;var rotation=Quaternion.LookRotation(slope);
            mesh.Box((from+to)*.5f-rotation*Vector3.up*.11f,new Vector3(width,.22f,Vector3.Distance(from,to)),F.Stone,0,true,rotation);
            if(!guarded)return;
            Vector3 side=Vector3.Cross(Vector3.up,slope).normalized*(width*.5f+.12f);Rail(from-side,to-side);Rail(from+side,to+side);
        }
        void Rail(Vector3 from,Vector3 to)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(from,to)/1.75f);
            for(int i=0;i<=count;i++)Post(Vector3.Lerp(from,to,i/(float)count));
            mesh.Beam(from+Vector3.up*.58f,to+Vector3.up*.58f,.035f,F.Wood);
            mesh.Beam(from+Vector3.up*1.03f,to+Vector3.up*1.03f,.065f,F.Wood,true);
        }
        void Target(Vector3 p,float radius)
        {
            for(int i=0;i<4;i++)mesh.Ellipsoid(p+Vector3.back*i*.008f,new Vector3(radius*(1-i*.23f),radius*(1-i*.23f),.025f),i%2==0?F.Ivory:F.Coral,rings:10,sides:20);
        }
        void Landmark()
        {
            var plinth = new Vector3(1.8f, 4.14f, 19.9f);
            mesh.Box(plinth + Vector3.up * .27f, new Vector3(2.65f, .54f, 1.85f), F.DarkWood, .13f, true);
            for (int i = 0; i < 10; i++)
                mesh.Box(plinth + Vector3.up * (.90f + i * .78f), new Vector3(2.5f, .76f, 1.25f),
                    (F)((int)F.Red + i % 3), .11f, true, Quaternion.Euler(0, i % 2 * 90 + i % 3 * 3, 0));
            var lanternBase = plinth + Vector3.up * 8.42f;
            mesh.Disk(lanternBase, 1.32f, .24f, F.Brass, false, 32);
            mesh.Ellipsoid(lanternBase + Vector3.up * .87f, new Vector3(.62f, .74f, .62f), F.Glow, rings: 10, sides: 20);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                var edge = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .89f;
                mesh.Beam(lanternBase + edge + Vector3.up * .20f, lanternBase + edge + Vector3.up * 1.56f, .07f, F.DarkWood);
            }
            mesh.Disk(lanternBase + Vector3.up * 1.55f, 1.16f, .22f, F.Brass, false, 32);
            mesh.Ellipsoid(lanternBase + Vector3.up * 1.78f, new Vector3(1.13f, .64f, 1.13f), F.Teal, rings: 10, sides: 24);
            mesh.Beam(lanternBase + Vector3.up * 2.30f, lanternBase + Vector3.up * 2.76f, .065f, F.Brass);
        }
    }
}

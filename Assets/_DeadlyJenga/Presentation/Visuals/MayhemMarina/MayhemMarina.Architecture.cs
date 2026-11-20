using System.Collections.Generic;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    public sealed partial class MayhemMarina
    {
        // The stone island stops at this angle (degrees around the island, 0 = toward the cinema,
        // 90 = east, -90 = west). Everything from here round through the back and the east is open beach.
        const float EastCut = -55;

        void ShoreAndCourts()
        {
            mesh.ArcSlab(new Vector3(0,-1.6f,4),new Vector2(9,11),new Vector2(24,21),2.8f,-110,EastCut,F.Rock,true,72);
            mesh.ArcSlab(new Vector3(0,1.12f,4),new Vector2(9,11),new Vector2(23.8f,20.8f),.13f,-110,EastCut,F.Stone,true,72);
            // The main deck ends at the back of the cinema; behind it is beach.
            mesh.Box(new Vector3(0,.6f,7.2f),new Vector3(16,1.2f,9.4f),F.Ivory,.28f,true);
            mesh.Box(new Vector3(0,1.22f,7.2f),new Vector3(16,.13f,9.4f),F.Stone,.045f,true);
            mesh.Box(new Vector3(-14,.6f,8),new Vector3(11,1.2f,12),F.Ivory,.4f,true);
            mesh.Box(new Vector3(-14,1.22f,8),new Vector3(11,.13f,12),F.Stone,.04f,true);
            mesh.Disk(new Vector3(-10,-.8f,-8),5.55f,2,F.Rock,true);
            mesh.Disk(new Vector3(-10,1.2f,-8),5.35f,.13f,F.Stone,true);
            mesh.ArcSlab(new Vector3(-10,1.34f,-8),Vector2.one*4.9f,Vector2.one*5.3f,.10f,15,250,F.Wood,false,48);
            // Individual copings give the water's edge a rounded masonry rhythm.
            for(int i=0;i<30;i++)
            {
                float degrees=Mathf.Lerp(-128,123,i/29f);
                if(degrees>EastCut)continue;
                float a=degrees*Mathf.Deg2Rad;
                var p=new Vector3(Mathf.Sin(a)*8.85f,1.23f,4+Mathf.Cos(a)*10.85f);
                mesh.Box(p,new Vector3(1.05f,.24f,.48f),F.Ivory,.07f,false,Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0));
            }
            Bridge(new Vector3(-18,1.24f,-25),new Vector3(-10,1.33f,-12.3f),3.6f,0);
            mesh.Box(new Vector3(-8.27f,1.225f,3.15f),new Vector3(1.12f,.12f,2.65f),F.Stone,0,true);
            mesh.Box(new Vector3(0,1.294f,3.1f),new Vector3(15.4f,.007f,1.1f),F.Ivory,.002f);
            TileLines(new Vector3(0,1.292f,7.2f),new Vector2(15,9));
            TileLines(new Vector3(-14,1.292f,7),new Vector2(10,10));
            for(int i=0;i<5;i++)
            {
                float a=i*2.21f;Rock(new Vector3(-10+Mathf.Cos(a)*5.35f,-.05f,-8+Mathf.Sin(a)*5.35f),new Vector3(1.7f,.9f,1.3f),i);
            }
        }
        void MarshAndBeach()
        {
            var center = new Vector3(0, .02f, 4);
            // The whole east side and the back are open beach, low by the water. The plaza, main deck and
            // the west deck step down onto it.
            mesh.ArcSlab(center, new Vector2(6, 6), new Vector2(27.6f, 24.6f), .36f, EastCut, 140, F.Sand, true, 56);
            Ramp(new Vector3(7.0f, PlazaTop, -4.4f), new Vector3(10.6f, .40f, -4.4f), 3f, false);
            Ramp(new Vector3(7.9f, 1.285f, 6.5f), new Vector3(11.4f, .40f, 6.5f), 2.7f, false);
            mesh.ArcSlab(center, new Vector2(20, 17), new Vector2(27.2f, 24.0f), .34f, -105, -62, F.Sand, true, 28);
            Ramp(new Vector3(-23.0f,1.25f,8.78f),new Vector3(-24.1f,1.25f,8.93f),2.7f,false);
            Ramp(new Vector3(-24.1f,1.25f,8.93f),new Vector3(-25.8f,.36f,9.25f),2.7f,false);
            Ramp(new Vector3(-12f,1.285f,14f),new Vector3(-12f,.40f,17.3f),2.7f,false);
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
            // Mark the open water edge with a flush contrasting trim.
            mesh.Box(new Vector3(-20.8f,1.244f,-26.27f),new Vector3(6.32f,.014f,.16f),F.DarkWood,.006f);
            for (int i=0;i<9;i++)
                mesh.Box(new Vector3(-23.6f+i*.70f,1.254f,-26.27f),new Vector3(.27f,.012f,.13f),F.Brass,.005f);
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3(-23.9f + i * 1.9f, 1.24f, -26.36f);
                mesh.Beam(point - Vector3.up * 1.65f, point + Vector3.up * .32f, .12f, F.DarkWood);
                mesh.Disk(point + Vector3.up * .28f, .15f, .065f, F.Brass, false, 14);
            }
            DirectionBoard(new Vector3(-22.7f, 1.24f, -24.2f), Quaternion.Euler(0, 180, 0), 2.8f);
        }

        // Kept only as named spots (the review camera and the lobby checks use them).
        static readonly Vector3 BeaconWalkStart = new(9.25f, 1.31f, -4.55f);
        static readonly Vector3 BeaconWalkEnd = new(11.5f, 1.285f, 2.6f);

        void DirectionBoard(Vector3 basePosition, Quaternion facing, float width)
        {
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(basePosition + facing * Vector3.right * (side * (width * .5f - .25f)) + Vector3.up * .64f,
                    new Vector3(.14f, 1.28f, .19f), F.DarkWood, .035f, false, facing);
            mesh.Box(basePosition + Vector3.up * 1.35f, new Vector3(width, .64f, .14f), F.Teal, .065f, false, facing);
        }
        void SignMounts()
        {
            mesh.Box(new Vector3(-10, 1.340f, -10.1f), new Vector3(5.1f, .018f, .8f), F.Ivory, .008f);
        }
        void LightingFixtures()
        {
            DeckLight(new Vector3(-12.5f, 1.33f, -10.25f));
            DeckLight(new Vector3(-7.5f, 1.33f, -10.25f));
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
            for(float z=-area.y*.5f+.7f;z<area.y*.5f;z+=1.55f)mesh.Box(center+Vector3.forward*z+Vector3.up*.002f,new Vector3(area.x,.006f,.013f),F.Ivory,.001f);
        }
        void Bridge(Vector3 from,Vector3 to,float width,float arch,bool rightSideEntry=false)
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
                if(rightSideEntry && sign==1)
                {
                    // Keep the terrace ramp's entrance open through the bridge railing.
                    var offset=side*(width*.5f+.08f);
                    Rail(P(0)+offset,P(.46f)+offset);
                    Rail(P(.93f)+offset,P(1)+offset);
                    continue;
                }
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
            Ramp(new Vector3(0,PlazaTop,CinemaFront-2),new Vector3(0,CinemaFloor,CinemaFront),9.6f,false);
            Ramp(new Vector3(5.98f,PlazaTop,CinemaFront-1.9f),new Vector3(5.98f,CinemaFloor,CinemaFront),2.15f,false);
            float depth=CinemaBack-CinemaFront;
            float center=(CinemaFront+CinemaBack)*.5f;
            mesh.Box(new Vector3(0,CinemaFloor-.3f,center),new Vector3(15.8f,.6f,depth),F.Stone,0,true);
            TileLines(new Vector3(0,CinemaFloor+.003f,center),new Vector2(15.22f,depth-.45f));
            for(int side=-1;side<=1;side+=2)
            {
                var trim=new Vector3(side*7.64f,CinemaFloor+.004f,center);
                mesh.Box(trim,new Vector3(.16f,.007f,depth-.36f),F.CandyMint,.001f);
                mesh.Box(trim-Vector3.right*(side*.15f),new Vector3(.065f,.007f,depth-.36f),F.Ivory,.001f);
            }
            foreach (float side in new[]{-1f,1f})
                Rail(new Vector3(side*7.78f,CinemaFloor,CinemaFront+.5f),new Vector3(side*7.78f,CinemaFloor,2.0f));
            WardrobeConnector();
            mesh.Box(new Vector3(0,3.7f,11.65f),new Vector3(15.8f,4.0f,.5f),F.Teal,.2f,true);
            mesh.CurvedRoof(new Vector3(0,5.95f,center),17.1f,depth+2.0f,2.08f,F.CandyCoral,striped:true);
            mesh.Box(CinemaSign,new Vector3(6.1f,.95f,.24f),F.Teal,.085f);
            for(int side=-1;side<=1;side+=2)
                mesh.Beam(CinemaSign+new Vector3(side*2,.4f,.06f),CinemaSign+new Vector3(side*2,2.19f,.06f),.055f,F.Brass);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*7.4f;
                float z=CinemaFront+.45f;
                mesh.Box(new Vector3(x,CinemaFloor+.17f,z),new Vector3(.78f,.34f,.78f),F.Stone,.08f,true);
                mesh.Beam(new Vector3(x,CinemaFloor+.22f,z),new Vector3(x,6.72f,z),.18f,F.Wood,true);
                mesh.Beam(new Vector3(x,5.08f,z),new Vector3(side*6.55f,7.0f,z),.13f,F.Wood);
                Lantern(new Vector3(x,4.4f,z-.35f),.16f);
            }
            BuildBeachCinema();
            Lantern(new Vector3(-3.4f,5.46f,2.6f),.21f);
            Lantern(new Vector3(3.4f,5.46f,2.6f),.21f);
            Lantern(new Vector3(-3.8f,5.35f,8.3f),.22f);
            Lantern(new Vector3(3.8f,5.35f,8.3f),.22f);
        }
        static readonly Vector3 ProjectorLens = new(-1.35f,5.49f,5.75f);
        static readonly Vector3 ProjectionSurface = new(0,3.85f,10.91f);

        void BuildBeachCinema()
        {
            mesh.Box(new Vector3(0,3.85f,11.16f),new Vector3(7.48f,3.50f,.26f),F.CandyGrape,.13f);
            mesh.Box(new Vector3(0,3.85f,10.99f),new Vector3(7.25f,3.28f,.09f),F.CreamFabric,.026f);
            mesh.Beam(new Vector3(-3.80f,5.55f,10.99f),new Vector3(3.80f,5.55f,10.99f),.13f,F.Metal);
            mesh.Beam(new Vector3(-3.69f,2.18f,10.99f),new Vector3(3.69f,2.18f,10.99f),.075f,F.DarkWood);
            for (int side=-1;side<=1;side+=2)
            {
                float x=side*3.61f;
                mesh.Beam(new Vector3(x,5.57f,11.00f),new Vector3(x,6.12f,11.00f),.024f,F.Brass);
                mesh.Ellipsoid(new Vector3(side*3.82f,5.55f,10.99f),new Vector3(.13f,.145f,.145f),F.Brass);
            }

            var direction=(ProjectionSurface-ProjectorLens).normalized;
            var facing=Quaternion.LookRotation(direction);
            var housing=ProjectorLens-direction*.31f;
            mesh.Beam(housing+Vector3.up*.14f,new Vector3(housing.x,6.25f,housing.z),.049f,F.DarkWood);
            mesh.Box(housing,new Vector3(.68f,.35f,.59f),F.CandyCream,.082f,false,facing);
            mesh.Box(housing+facing*new Vector3(0,-.125f,0),new Vector3(.67f,.11f,.58f),F.Teal,.045f,false,facing);
            mesh.Box(housing+direction*.28f,new Vector3(.47f,.28f,.09f),F.DarkWood,.045f,false,facing);
            mesh.Ellipsoid(ProjectorLens-direction*.047f,new Vector3(.17f,.135f,.094f),F.Brass,facing);
            mesh.Ellipsoid(ProjectorLens,new Vector3(.112f,.088f,.028f),F.Glow,facing);
            for (int vent=0;vent<3;vent++)
            {
                var offset=facing*new Vector3(.34f,.06f-vent*.057f,-.05f);
                mesh.Box(housing+offset,new Vector3(.018f,.022f,.25f),F.DarkWood,.006f,false,facing);
            }

            CandyRainbow(new Vector3(0,5.46f,10.82f),.41f);
            BuildCandyCabinet(false);
            BuildCandyCabinet(true);
        }
        void CandyRainbow(Vector3 origin,float width)
        {
            for(int band=0;band<3;band++)
            {
                var path=new List<Vector3>();
                float scale=1-band*.19f;
                for(int i=0;i<=18;i++)
                {
                    float angle=Mathf.PI*i/18;
                    path.Add(origin+new Vector3(Mathf.Cos(angle)*width,Mathf.Sin(angle)*width*.49f,0)*scale);
                }
                mesh.Tube(path,width*.055f,band==0?F.CandyCoral:band==1?F.Glow:F.CandyMint,8);
            }
        }
        void BuildCandyCabinet(bool host)
        {
            float x=host?4.87f:-4.87f;
            var color=host?F.CandyMint:F.CandyLilac;
            // The display plane and lobby-owned collision envelope stay fixed.
            mesh.Box(new Vector3(x,3.13f,11.20f),new Vector3(2.08f,2.52f,.88f),F.CandyCream,.24f);
            mesh.Box(new Vector3(x,1.98f,11.13f),new Vector3(2.18f,.24f,1.02f),F.CandyGrape,.09f);
            mesh.Box(new Vector3(x,2.10f,11.10f),new Vector3(2.12f,.11f,.98f),color,.045f);
            mesh.Box(new Vector3(x,2.13f,10.61f),new Vector3(1.78f,.035f,.035f),F.Glow,.012f);

            // A deep hood, layered bezel and sculpted side cheeks read as one console.
            mesh.Box(new Vector3(x,3.30f,10.94f),new Vector3(2.09f,1.99f,.61f),color,.22f);
            mesh.Box(new Vector3(x,3.28f,10.64f),new Vector3(1.91f,1.79f,.17f),F.CandyCream,.085f);
            mesh.Box(new Vector3(x,3.28f,10.60f),new Vector3(1.77f,1.65f,.12f),F.CandyGrape,.065f);
            for(int side=-1;side<=1;side+=2)
            {
                mesh.Box(new Vector3(x+side*.99f,3.22f,11.03f),new Vector3(.18f,2.12f,.86f),color,.08f);
                mesh.Box(new Vector3(x+side*1.035f,3.21f,11.18f),new Vector3(.075f,1.62f,.45f),F.CandyCream,.035f);
                mesh.Box(new Vector3(x+side*.973f,3.27f,10.565f),new Vector3(.035f,1.45f,.03f),F.Glow,.012f);
                for(int vent=0;vent<4;vent++)
                    mesh.Box(new Vector3(x+side*1.078f,2.62f+vent*.12f,11.19f),new Vector3(.014f,.045f,.26f),F.CandyGrape,.006f);
            }
            mesh.Box(new Vector3(x,4.30f,11.00f),new Vector3(2.16f,.42f,.91f),color,.18f);
            mesh.Box(new Vector3(x,4.32f,10.56f),new Vector3(1.78f,.20f,.08f),F.CandyGrape,.075f);
            mesh.Box(new Vector3(x,4.32f,10.513f),new Vector3(1.43f,.046f,.022f),F.Glow,.018f);
            CandyRainbow(new Vector3(x,4.53f,10.77f),.40f);

            // A quiet recessed apron replaces the old fake keyboard/button clutter.
            mesh.Box(new Vector3(x,2.28f,10.87f),new Vector3(1.89f,.32f,.36f),F.CandyGrape,.075f);
            mesh.Box(new Vector3(x,2.36f,10.76f),new Vector3(2.10f,.15f,.64f),F.CandyCream,.065f);
            mesh.Box(new Vector3(x,2.30f,10.444f),new Vector3(1.84f,.06f,.035f),color,.018f);
            for(int side=-1;side<=1;side+=2)
                for(int vent=0;vent<5;vent++)
                    mesh.Box(new Vector3(x+side*.60f+(vent-2)*.065f,2.26f,10.681f),new Vector3(.022f,.105f,.018f),color,.008f);
            mesh.Box(new Vector3(x,2.26f,10.676f),new Vector3(.40f,.10f,.025f),F.CandyCream,.038f);
        }
        void WardrobeConnector()
        {
            var from=new Vector3(-10,1.285f,7.6f);
            var to=new Vector3(-7.9f,CinemaFloor,7.6f);
            var rotation=Quaternion.LookRotation((to-from).normalized);
            // Offset along the slab normal, so its top meets both existing flat landings exactly.
            mesh.Box((from+to)*.5f-rotation*Vector3.up*.11f,new Vector3(3.2f,.22f,Vector3.Distance(from,to)),F.Stone,0,true,rotation);
        }
        // The deck beside the plaza (behind the wardrobe) is a small crate course for trying out
        // climbing and jumping. Crates are fixed in place: solid art, no physics.
        // Routes stay clear: the cinema connector (z 7.6) and the ramp down to the back beach (x -12).
        void GardenLounge()
        {
            const float floor = 1.285f;
            // Steps up: one, two, then three crates high.
            CrateStack(new Vector3(-12.7f, floor, 3.5f), 0, 1, 0);
            CrateStack(new Vector3(-13.95f, floor, 3.5f), 0, 2, 1);
            CrateStack(new Vector3(-15.2f, floor, 3.5f), 0, 3, 2);
            // Jump across and down from the tallest stack, then step back down to the deck.
            CrateStack(new Vector3(-16.9f, floor, 4.4f), 90, 2, 0);
            CrateStack(new Vector3(-16.9f, floor, 5.75f), 90, 1, 1);
            // A couple of loose-looking crates to hop between.
            CrateStack(new Vector3(-13.6f, floor, 5.4f), 18, 1, 2);
            CrateStack(new Vector3(-12.2f, floor, 5.3f), 90, 2, 1);
            foreach (var p in new[] { new Vector3(-18.6f, floor, 3.0f), new Vector3(-11.6f, floor, 3.0f), new Vector3(-18.6f, floor, 12.2f) })
                Pot(p, .5f, .85f, (int)(p.x * 7 + p.z));
        }

        const float CrateLength = 1.15f, CrateSide = .9f;
        static readonly F[] CrateColors = { F.Red, F.Green, F.Purple };

        // Stacks crates on a point of the floor. Upper crates get a small twist so stacks look hand-placed.
        void CrateStack(Vector3 floorPoint, float yaw, int height, int firstColor)
        {
            for (int i = 0; i < height; i++)
            {
                float twist = i == 0 ? 0 : (i % 2 == 0 ? -4 : 5);
                Crate(floorPoint + Vector3.up * (i * CrateSide), yaw + twist, CrateColors[(firstColor + i) % 3]);
            }
        }

        // A classic wooden crate in candy colors: a solid core, a raised frame on every edge,
        // an X brace on each side and slats on top. Only the core has collision.
        void Crate(Vector3 bottom, float yaw, F color)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            const float strip = .1f, relief = .035f;
            float hx = CrateLength * .5f, hz = CrateSide * .5f, h = CrateSide;
            Vector3 P(float x, float y, float z) => bottom + q * new Vector3(x, y, z);
            void Part(Vector3 local, Vector3 size, Quaternion turn) => mesh.Box(bottom + q * local, size, color, .015f, false, q * turn);

            mesh.Box(P(0, h * .5f, 0), new Vector3(CrateLength, h, CrateSide), color, .03f, true, q);

            // Frame: the twelve edges stand out from the core by the relief depth.
            float fx = hx - strip * .5f + relief * .5f, fz = hz - strip * .5f + relief * .5f;
            float lowY = strip * .5f - relief * .5f, highY = h - strip * .5f + relief * .5f;
            float thick = strip + relief;
            foreach (float y in new[] { lowY, highY })
            {
                foreach (float z in new[] { -fz, fz }) Part(new Vector3(0, y, z), new Vector3(CrateLength + relief * 2, thick, thick), Quaternion.identity);
                foreach (float x in new[] { -fx, fx }) Part(new Vector3(x, y, 0), new Vector3(thick, thick, CrateSide + relief * 2), Quaternion.identity);
            }
            foreach (float x in new[] { -fx, fx })
                foreach (float z in new[] { -fz, fz })
                    Part(new Vector3(x, h * .5f, z), new Vector3(thick, h + relief * 2, thick), Quaternion.identity);

            // X braces across the inside of each side panel.
            float innerH = h - strip * 2;
            float longW = CrateLength - strip * 2, longLen = Mathf.Sqrt(longW * longW + innerH * innerH) - strip * .4f;
            float longAngle = Mathf.Atan2(innerH, longW) * Mathf.Rad2Deg;
            foreach (float z in new[] { -(hz + relief * .4f), hz + relief * .4f })
                foreach (float sign in new[] { -1f, 1f })
                    Part(new Vector3(0, h * .5f, z), new Vector3(longLen, strip * .85f, relief), Quaternion.Euler(0, 0, sign * longAngle));
            float shortW = CrateSide - strip * 2, shortLen = Mathf.Sqrt(shortW * shortW + innerH * innerH) - strip * .4f;
            float shortAngle = Mathf.Atan2(innerH, shortW) * Mathf.Rad2Deg;
            foreach (float x in new[] { -(hx + relief * .4f), hx + relief * .4f })
                foreach (float sign in new[] { -1f, 1f })
                    Part(new Vector3(x, h * .5f, 0), new Vector3(relief, strip * .85f, shortLen), Quaternion.Euler(sign * shortAngle, 0, 0));

            // Top slats inside the frame, with small gaps between them.
            const int slats = 4;
            float slatSpan = (CrateSide - strip * 2) / slats;
            for (int i = 0; i < slats; i++)
            {
                float z = -hz + strip + slatSpan * (i + .5f);
                Part(new Vector3(0, h + relief * .25f, z), new Vector3(CrateLength - strip * 2, relief * .5f, slatSpan - .03f), Quaternion.identity);
            }
        }
        void Portal(Vector3 center,float width,float height,float depth,bool wardrobePassage=false)
        {
            float half=width*.5f;
            for(int side=-1;side<=1;side+=2)
            {
                if(side<0||!wardrobePassage)
                    mesh.Box(center+new Vector3(side*(half-.5f),height*.42f,0),new Vector3(1.05f,height*.85f,depth),F.Ivory,.4f,true);
            }
            var arch=new List<Vector3>();
            for(int i=0;i<=40;i++){float a=Mathf.PI-i*Mathf.PI/40;arch.Add(center+new Vector3(Mathf.Cos(a)*(half-.45f),height*.59f+Mathf.Sin(a)*height*.35f,-depth*.5f));}
            mesh.Tube(arch,.53f,F.Ivory,16,true);
            mesh.CurvedRoof(center+new Vector3(0,height*.57f,0),width,depth,height*.35f,F.Ivory);
        }
        // A curved bank of seating on the arrival island.
        void ArrivalSeating()
        {
            for(int i=0;i<3;i++)
            {
                float angle=Mathf.Lerp(130,205,i/2f)*Mathf.Deg2Rad;var p=new Vector3(-10+Mathf.Cos(angle)*4.0f,1.33f,-8+Mathf.Sin(angle)*4.0f);
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
                float a=i*Mathf.PI*2/3;var c=p+new Vector3(Mathf.Cos(a)*radius*.5f,.68f,Mathf.Sin(a)*radius*.5f);
                mesh.Disk(c,.1f,.16f,F.Ivory,false,12);
            }
        }
        void Ramp(Vector3 from,Vector3 to,float width,bool guarded=true)
        {
            var slope=(to-from).normalized;var rotation=Quaternion.LookRotation(slope);
            mesh.Box((from+to)*.5f-rotation*Vector3.up*.11f,new Vector3(width,.22f,Vector3.Distance(from,to)),F.Stone,0,false,rotation);
            mesh.WalkStrip(from,to,Vector3.Cross(Vector3.up,slope).normalized*(width*.5f));
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
    }
}

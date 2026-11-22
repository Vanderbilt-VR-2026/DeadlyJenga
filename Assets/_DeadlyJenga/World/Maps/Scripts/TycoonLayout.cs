using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class TycoonLayout
    {
        public const string Preset = "tycoon";
        // The core climbs CoreFlights flights of 6 m; the crown rings start on the summit deck.
        const int CoreFlights = 10;
        public const int HeightCourses = CoreFlights * 3 + 6;
        public static readonly Vector2 Size = new Vector2(96, 90);

        public static List<CastlePlacement> Build(CastleSettings settings)
        {
            var plan = new List<CastlePlacement>(650);
            var red = CastleSection.Wall;
            var green = CastleSection.Corner;
            var purple = CastleSection.Keep;
            Core(plan, settings);

            // The original rear stacks leave open sight lines through the switchbacks.
            foreach (int side in new[] { -1, 1 })
            {
                Tower(plan, settings, side * 24, 24, 18, green);
                Tower(plan, settings, side * 12, 24, 14, red);
            }

            // Each run splits into its own set of bodies immediately outside the launch deck.
            for (int lane = 0; lane < 4; lane++)
            {
                float x = -8.4f + lane * 5.6f;
                Deck(plan, purple, x, 60, -10.7f, 5.55f, 3, TycoonDeckFinish.Gate);
            }
            Flume(plan, CastleSection.Aqua, TycoonDeckFinish.Flume,
                P(-8.4f,60,-12.2f), P(-17,53,-17), P(-33,40,4), P(-39,34,8),
                P(-44,27,-5), P(-32,19,-18), P(-41,7,-32));
            Flume(plan, red, TycoonDeckFinish.Tube,
                P(-2.8f,60,-12.2f), P(-3,54,-19), P(-15,45,-12), P(-17,40,3),
                P(-11,35,16), P(11,30,16), P(19,26,6), P(24,24,-5));
            Flume(plan, green, TycoonDeckFinish.Flume,
                P(2.8f,60,-12.2f), P(5,52,-19), P(17,44,-12), P(22,37,0),
                P(29,31,-3), P(29,24,-5));
            Flume(plan, CastleSection.Gold, TycoonDeckFinish.DryChute,
                P(8.4f,60,-12.2f), P(20,52,-15), P(39,32,-16), P(36,24,-18));

            // A cross beam and separate planks carry each bay above the lethal sand.
            for (int bay = -2; bay <= 2; bay++)
                PlankBay(plan, bay * 13, 7, -37, 13, 15, CastleSection.Wall, TycoonDeckFinish.Peach);
            Deck(plan, green, -40, 7, -35, 12, 7, TycoonDeckFinish.Basin);
            Pier(plan,green,-40,6.4f,-35,5);
            Segment(plan, green, 5, P(-34,7,-35), P(-32.5f,7,-35));

            // Three open arch bays carry the red promenade.
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 9;
                Pier(plan, red, x, 16, -24, 3);
                Box(plan, red, P(x,16.5f,-24),P(5,1,7));
                Box(plan, red, P(x,17.5f,-24),P(7.5f,1,7));
                Deck(plan, red, x,18.6f,-24,9,7,TycoonDeckFinish.Railed);
            }
            Route(plan, red, 5, P(-22,7,-38), P(-28,12,-30), P(-22,18.6f,-24));
            Route(plan, purple, 5, P(0,18.6f,-20.5f), P(0,18,-12), P(8,18,-6));
            Deck(plan,red,23,18.6f,-24,2,7);
            // Seat courses double as ledges; the side ramp bypasses the steeper climb.
            for (int row = 0; row < 6; row++)
            {
                float z = -19 + row * 2.8f;
                float height = 19 + row * 1.4f;
                Deck(plan,purple,-21,height,z,16,2.8f);
                foreach (int side in new[] {-1,1})
                    Pier(plan,purple,-21+side*6,height-.6f,z,2.4f);
            }
            Segment(plan,purple,3,P(-31,18.6f,-24),P(-31,26,-5));
            Deck(plan,purple,-27,18.6f,-24,9,5);
            Deck(plan,purple,-26,26,-2,13,3);
            Segment(plan,purple,4,P(-21,26,0),P(-26,36,19.5f));
            Deck(plan,green,-24,36.6f,24,12,9,TycoonDeckFinish.Awning);

            // Shaded lower route and a broad upper sundeck connect to the east terrace.
            PlankBay(plan,32,12,-13,18,18,green,TycoonDeckFinish.Mint);
            PlankBay(plan,32,24,-13,18,18,green,TycoonDeckFinish.Mint,12);
            Deck(plan,green,27,24.6f,-7,9,4,TycoonDeckFinish.Basin);
            Deck(plan,green,36,24.6f,-20,7,4,TycoonDeckFinish.Basin);
            Deck(plan,green,32,24.6f,-13,4,4,TycoonDeckFinish.Parasol);
            Terrace(plan);
            Route(plan,green,5,P(33,18,-28),P(43,20,-27),P(39,24,-21));
            Route(plan,red,5,P(24,18.6f,-24),P(28,18,-29));
            Route(plan,green,4,P(25,12,-22),P(24,9,-27),P(24,7,-30));
            Route(plan,purple,4,P(23,24,-8),P(16,24,-8),P(-8,24,-12),P(-9,24,-6));
            Deck(plan,red,11,7.6f,-39,6,4,TycoonDeckFinish.Awning);
            Deck(plan,purple,0,60.6f,4,13,7,TycoonDeckFinish.Bucket);
            return plan;
        }

        static void Flume(List<CastlePlacement> plan, CastleSection color, TycoonDeckFinish finish,
            params Vector3[] guide)
        {
            MoldedRoute(plan,color,4.6f,finish,5,guide);
            // Compression piers support the longer unsupported spans.
            for (int i = 2; i < guide.Length - 1; i += 2)
            {
                var point = guide[i];
                Pier(plan,color,point.x,point.y-.65f,point.z,2.4f);
            }
        }

        static void PlankBay(List<CastlePlacement> plan,float x,float top,float z,float width,float depth,
            CastleSection color,TycoonDeckFinish finish,float baseHeight = 0)
        {
            foreach (int side in new[] {-1,1})
            {
                float beamZ = z + side * depth * .32f;
                foreach (int end in new[] {-1,1})
                {
                    float h = top - 1.8f - baseHeight;
                    Box(plan,color,P(x+end*width*.34f,baseHeight+h*.5f,beamZ),P(2.6f,h,2.6f));
                }
                Box(plan,CastleSection.Keep,P(x,top-1.2f,beamZ),P(width+.8f,1.2f,2));
            }
            for (int i=0;i<5;i++)
                Deck(plan,color,x+width*((i+.5f)/5-.5f),top,z,width/5-.045f,depth,finish == TycoonDeckFinish.Peach && x == 0 && i < 2 ? TycoonDeckFinish.LoosePeach : finish);
        }

        static void Core(List<CastlePlacement> plan, CastleSettings settings)
        {
            var purple = CastleSection.Keep;
            for (int flight = 0; flight < CoreFlights; flight++)
            {
                float bottom = flight * 6;
                float top = bottom + 6;
                float direction = flight % 2 == 0 ? 1 : -1;
                bool summit = flight == CoreFlights - 1;
                foreach (int side in new[] { -1, 1 })
                {
                    float capTop = side == direction || summit ? top - .6f : top;
                    float postHeight = capTop - bottom - 1;
                    foreach (int back in new[] { -1, 1 })
                        Box(plan,purple,P(side*10.75f,bottom+postHeight*.5f,back*8),P(2.5f,postHeight,2.5f));
                    Box(plan,purple,P(side*10.75f,capTop-.5f,0),P(2.5f,1,18.5f));
                    if (side == direction && !summit)
                    {
                        foreach (int back in new[] { -1, 1 })
                            Box(plan,purple,P(side*10.75f,top-.3f,back*8),P(2.5f,.6f,2.5f));
                    }
                }
                if (!summit) Deck(plan,purple,direction*8.75f,top,0,5,12);
                float z = -direction * 3;
                Segment(plan,purple,4,P(-direction*6.25f,bottom,z),P(direction*6.25f,top,z));
            }

            Deck(plan,purple,0,CoreFlights * 6,0,24,18.5f);
        }

        static void Terrace(List<CastlePlacement> plan)
        {
            // Three wide plates leave a clear turning and runoff zone.
            Deck(plan,CastleSection.Wall,32,18,-32,16,8,TycoonDeckFinish.Splash);
            // Rear lip and colored blocks make the runout read as a splash platform.
            plan.Add(new CastlePlacement(P(36.5f,18.6f,-28.65f),Quaternion.Euler(-90,0,0),
                CastleSection.Wall,9,P(5,1.1f,1.2f),TycoonDeckProfile.Rectangle(TycoonDeckFinish.Splash)));
            Box(plan,CastleSection.Wall,P(39.4f,18.6f,-35),P(1.2f,1.2f,8));
            Box(plan,CastleSection.Wall,P(31.5f,18.6f,-41.4f),P(9,1.2f,1.2f));
            Box(plan,CastleSection.Gold,P(37.5f,18.45f,-39.8f),P(.9f,.9f,.9f));
            Box(plan,CastleSection.Keep,P(36.3f,18.55f,-39.8f),P(1.1f,1.1f,1.1f));
            Box(plan,CastleSection.Corner,P(26,18.65f,-39.8f),P(1.3f,1.3f,1.3f));
            Deck(plan,CastleSection.Wall,32,18,-39,14,6);
            MoldedRoute(plan,CastleSection.Wall,3.5f,TycoonDeckFinish.OpenDeck,2.1f,
                P(23,17.4f,-32),P(23,17.4f,-37),P(27,17.4f,-43),P(32,17.4f,-44),
                P(37,17.4f,-43),P(41,17.4f,-37),P(41,17.4f,-32));
            MoldedRoute(plan,CastleSection.Wall,3.3f,TycoonDeckFinish.OpenDeck,2.1f,
                P(21,16.8f,-32),P(21,16.8f,-37),P(25,16.8f,-43),P(32,16.8f,-45),
                P(39,16.8f,-43),P(43,16.8f,-37),P(43,16.8f,-32));


            Pier(plan,CastleSection.Wall,23.5f,16.8f,-36);
            Pier(plan,CastleSection.Wall,40.5f,16.8f,-36);
            Pier(plan,CastleSection.Wall,32,16.2f,-45);
            foreach(int side in new[]{-1,1})
            {
                Pier(plan,CastleSection.Wall,32+side*10,16.2f,-36);
                Pier(plan,CastleSection.Wall,32+side*6,16.2f,-42);
            }
            foreach (var support in new[] { P(28,17.4f,-31), P(36,17.4f,-31), P(32,17.4f,-39), P(32,16.8f,-43) })
                Pier(plan,CastleSection.Wall,support.x,support.y,support.z,2.5f);
        }

        static void Tower(List<CastlePlacement> plan, CastleSettings settings, float x, float z, int courses, CastleSection section)
        {
            float offset = (settings.blockSize.x-settings.blockSize.z)*.5f;
            for (int course = 0; course < courses; course++)
            {
                var rotation = Quaternion.Euler(0,(course%2)*90,0);
                foreach (int side in new[] { -1,1 })
                    plan.Add(new CastlePlacement(P(x,course*2+1,z)+rotation*P(0,0,side*offset),
                        rotation,section,course,settings.blockSize));
            }
        }

        static Vector3 P(float x,float y,float z) => new Vector3(x,y,z);

        static void Pier(List<CastlePlacement> plan, CastleSection section, float x, float top, float z, float width = 2.6f)
        {
            int pieces = Mathf.CeilToInt(top/6);
            float height = top/pieces;
            for(int i=0;i<pieces;i++)
                Box(plan,section,P(x,(i+.5f)*height,z),P(width,height,width));
        }

        static void Deck(List<CastlePlacement> plan, CastleSection section, float x, float top, float z, float width, float depth,
            TycoonDeckFinish finish = TycoonDeckFinish.Plain)
        {
            var position=P(x,top-.3f,z);
            var size=P(width,.6f,depth);
            if(finish==TycoonDeckFinish.Plain) Box(plan,section,position,size);
            else plan.Add(new CastlePlacement(position,Quaternion.identity,section,Mathf.FloorToInt(top/2),size,
                TycoonDeckProfile.Rectangle(finish)));
        }

        static void Box(List<CastlePlacement> plan, CastleSection section, Vector3 position, Vector3 size)
            => plan.Add(new CastlePlacement(position,Quaternion.identity,section,Mathf.FloorToInt(position.y/2),size));

        static void Route(List<CastlePlacement> plan, CastleSection section, float width, params Vector3[] points)
        {
            for (int i=1;i<points.Length;i++)
            {
                var a=points[i-1]; var b=points[i];
                // Butt long spans to small flat joint caps instead of overlapping angled colliders.
                Vector3 Trim(Vector3 p, Vector3 q) => Vector3.Lerp(p,q,Mathf.Min(.18f/(p-q).magnitude,.2f));
                Segment(plan,section,width,i>1?Trim(a,b):a,i<points.Length-1?Trim(b,a):b);
                if(i<points.Length-1) Deck(plan,section,b.x,b.y,b.z,width,.42f);
            }
        }

        static void MoldedRoute(List<CastlePlacement> plan, CastleSection section, float width,
            TycoonDeckFinish finish, float cornerRadius, params Vector3[] guide)
        {
            var points=new List<Vector3>{guide[0]};
            for(int i=1;i<guide.Length-1;i++)
            {
                var pivot=guide[i];
                var back=guide[i-1]-pivot;
                var forward=guide[i+1]-pivot;
                float trim=Mathf.Min(cornerRadius,Mathf.Min(back.magnitude,forward.magnitude)*.36f);
                var a=pivot+back.normalized*trim;
                var b=pivot+forward.normalized*trim;
                if (finish==TycoonDeckFinish.DryChute)
                {
                    var top=(a+2*pivot+b)*.25f;
                    var rotation=Quaternion.LookRotation(b-a,Vector3.up);
                    var pad=top-rotation*Vector3.up*1.1f;
                    Pier(plan,CastleSection.Keep,pad.x,pad.y-.36f,pad.z,2.6f);
                    plan.Add(new CastlePlacement(pad,rotation,CastleSection.Keep,
                        Mathf.FloorToInt(pad.y/2),P(2.6f,1,2.3f)));
                }
                points.Add(a);
                for(int step=1;step<=3;step++)
                {
                    float t=step/3f;
                    points.Add(Vector3.Lerp(Vector3.Lerp(a,pivot,t),Vector3.Lerp(pivot,b,t),t));
                }
            }
            points.Add(guide[guide.Length-1]);
            var edges=new Vector3[points.Count];
            for(int i=0;i<points.Count;i++)
            {
                var before=points[i]-points[Mathf.Max(0,i-1)];
                var after=points[Mathf.Min(points.Count-1,i+1)]-points[i];
                var a=Vector3.Cross(Vector3.up,before).normalized;
                var b=Vector3.Cross(Vector3.up,after).normalized;
                var miter=(a+b).normalized;
                edges[i]=miter*(width*.5f/Mathf.Max(.75f,Vector3.Dot(miter,i==0?b:a)));
            }
            for(int i=1;i<points.Count;i++)
            {
                var a=points[i-1];
                var b=points[i];
                var rotation=Quaternion.LookRotation(b-a,Vector3.up);
                var center=(a+b)*.5f-rotation*Vector3.up*.3f;
                var size=P(width,.6f,Vector3.Distance(a,b));
                var inverse=Quaternion.Inverse(rotation);
                Vector3 Local(Vector3 world)
                {
                    var point=inverse*(world-center);
                    return P(point.x/size.x,point.y/size.y,point.z/size.z);
                }
                var finishProfile=new TycoonDeckProfile(finish,Local(a-edges[i-1]),Local(a+edges[i-1]),
                    Local(b-edges[i]),Local(b+edges[i]));
                plan.Add(new CastlePlacement(center,rotation,section,Mathf.FloorToInt(center.y/2),size,finishProfile));
            }
        }

        static void Segment(List<CastlePlacement> plan, CastleSection section, float width, Vector3 from, Vector3 to)
        {
            var rotation=Quaternion.LookRotation(to-from,Vector3.up);
            var center=(from+to)*.5f-rotation*Vector3.up*.3f;
            plan.Add(new CastlePlacement(center,rotation,section,Mathf.FloorToInt(center.y/2),P(width,.6f,Vector3.Distance(from,to))));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class TycoonLayout
    {
        public const string Preset = "tycoon";
        public const int HeightCourses = 42;
        public static readonly Vector2 Size = new Vector2(96, 90);

        public static List<CastlePlacement> Build(CastleSettings settings)
        {
            var plan = new List<CastlePlacement>(320);
            var purple = CastleSection.Keep;
            var red = CastleSection.Wall;
            var green = CastleSection.Corner;
            var yellow = CastleSection.Gold;
            var aqua = CastleSection.Aqua;

            // Retain the rear Catwalks stacks at the original 12 m pitch.
            foreach (int side in new[] { -1, 1 })
            {
                Tower(plan, settings, side * 24, 24, 18, green);
                Tower(plan, settings, side * 12, 24, 14, red);
                Tower(plan, settings, side * 24, 12, 14, red);
                Tower(plan, settings, side * 24, -24, 8, side < 0 ? green : red);
            }
            Tower(plan, settings, 0, 24, 16, red);

            Core(plan, settings);

            // Banked crossing through the open front of the core.
            Route(plan, red, 6,
                P(-24,18,-24), P(-15,19,-23), P(-9,18,-17), P(0,18,-12),
                P(9,18,-17), P(15,19,-23), P(24,18,-24), P(32,18,-30));
            Deck(plan, purple, 0,18,-10,15,4);
            Deck(plan, green, -24,18,-24,9,8);
            Deck(plan, red, 24,18,-24,9,8);
            foreach (int offset in new[] { -1, 1 })
            {
                Box(plan,green,P(-24+offset*3.5f,16.7f,-24),P(2,1.4f,8));
                Box(plan,red,P(24+offset*3.5f,16.7f,-24),P(2,1.4f,8));
            }
            Pier(plan, red, -15,18.4f,-23);
            Pier(plan, red, 15,18.4f,-23);
            Pier(plan, red, 0,17.4f,-12);
            Route(plan,purple,4,P(0,18,-10),P(6,18,-10),P(9.5f,18,-6));

            // Two wraparound decks open back into the climbable core.
            Route(plan,purple,4,P(-9.5f,24,6),P(-15,24,7),P(-15,24,16),P(0,24,19),P(15,24,16),P(15,24,9));
            Route(plan,purple,4,P(-9.5f,36,6),P(-15,36,11),P(-19,36,14),P(-26,36,14));
            foreach (var support in new[] { P(-15,24,7),P(-15,24,16),P(0,24,19),P(15,24,16),
                         P(15,24,9),P(-15,36,11),P(-19,36,14),P(-26,36,14) })
                Pier(plan,purple,support.x,support.y-.6f,support.z);

            // West flume, with flat transfer and shoreline runout.
            Route(plan,green,6,P(-26,36,14),P(-41,30,10),P(-42,24,-5),P(-28,18,-9),
                P(-31,12,-24),P(-43,6,-24));
            Route(plan,green,4,P(-28,18,-9),P(-25,18,-14),P(-24,18,-24));
            Route(plan,green,4,P(-43,6,-31),P(-33,.9f,-20));
            foreach (var support in new[] { P(-41,30,10),P(-42,24,-5),P(-28,18,-9),P(-31,12,-24),P(-43,6,-24),P(-43,6,-31) })
                Pier(plan,green,support.x,support.y-.6f,support.z);
            Deck(plan,green,-43,6,-27.5f,6,7);

            Route(plan,aqua,4,P(-9.5f,24,-6),P(-13,24,-9),P(-23,18,-24),P(-28,6,-41));
            Pier(plan,purple,-13,23.4f,-9);
            Deck(plan,red,-28,6,-41,8,6);
            Pier(plan,red,-28,5.4f,-41);
            Route(plan,red,4,P(-28,6,-41),P(-12,.9f,-41));

            // The yellow chute occupies the rear side of the east wing before turning to the terrace.
            Route(plan,purple,4,P(-9.5f,60,6),P(-9.5f,60,15),P(10,60,14));
            foreach (var support in new[] { P(-9.5f,60,15),P(0,60,15),P(10,60,14),P(26,46,0),P(43,32,-12) })
                Pier(plan,purple,support.x,support.y-.6f,support.z,3);
            Route(plan,yellow,5.5f,P(10,60,14),P(26,46,0),P(43,32,-12),P(32,18,-28));
            Terrace(plan);
            Route(plan,green,4.5f,P(40,18,-30),P(45,12,-16),P(46,6,-31),P(39,6,-37),P(33,.9f,-22));
            foreach (var support in new[] { P(45,12,-16),P(46,6,-31),P(39,6,-37) })
                Pier(plan,green,support.x,support.y-.6f,support.z);
            Route(plan,aqua,4,P(24,18,-24),P(22,12,-36),P(39,6,-37));
            Pier(plan,green,22,11.4f,-36);

            // Ground-level route stays available when a bridge loses its support.
            Route(plan,purple,4,P(-33,.9f,-20),P(-38,.9f,-35),P(-28,.9f,-41),
                P(-12,.9f,-41),P(0,.9f,-38),P(20,.9f,-41),P(33,.9f,-22));
            foreach (var foot in new[] { P(-33,0,-20),P(-38,0,-35),P(-28,0,-41),P(-12,0,-41),
                         P(0,0,-38),P(20,0,-41),P(33,0,-22),P(26.5f,0,-31.5f),P(-35.5f,0,-27.5f) })
                Pier(plan,purple,foot.x,.3f,foot.z,2.4f);
            Route(plan,purple,5,P(0,.9f,-38),P(0,.9f,-23),P(0,0,-12),P(-6.25f,0,-3));
            Route(plan,purple,4,P(-9.5f,24,6),P(-18,24,6),P(-19,24,0),P(-18,24,-6),P(-9.5f,24,-6));
            foreach (var foot in new[] { P(-18,0,6),P(-19,0,0),P(-18,0,-6) })
                Pier(plan,purple,foot.x,23.4f,foot.z);
            return plan;
        }

        static void Core(List<CastlePlacement> plan, CastleSettings settings)
        {
            var purple = CastleSection.Keep;
            for (int flight = 0; flight < 12; flight++)
            {
                float bottom = flight * 6;
                float top = bottom + 6;
                float direction = flight % 2 == 0 ? 1 : -1;
                bool summit = flight == 11;
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

            float radius = CastleVariants.KeepRadius(settings.blockSize);
            Deck(plan,purple,0,72,0,24,18.5f);
            for (int course = 36; course < HeightCourses; course++)
            for (int side = 0; side < 4; side++)
            {
                var rotation = Quaternion.Euler(0, side*90+(course%2)*45, 0);
                plan.Add(new CastlePlacement(P(0,(course+.5f)*2,0)+rotation*P(0,0,radius),
                    rotation,purple,course,settings.blockSize));
            }
        }

        static void Terrace(List<CastlePlacement> plan)
        {
            // Three wide plates leave a clear turning and runoff zone.
            Deck(plan,CastleSection.Wall,32,18,-32,16,8);
            Deck(plan,CastleSection.Wall,32,18,-39,14,6);
            Deck(plan,CastleSection.Wall,32,17.4f,-43,12,2);
            Deck(plan,CastleSection.Wall,32,16.8f,-45,10,2);
            Deck(plan,CastleSection.Wall,23.5f,17.4f,-36,3,8);
            Deck(plan,CastleSection.Wall,40.5f,17.4f,-36,3,8);
            Pier(plan,CastleSection.Wall,23.5f,16.8f,-36);
            Pier(plan,CastleSection.Wall,40.5f,16.8f,-36);
            Pier(plan,CastleSection.Wall,32,16.2f,-45);
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

        static void Deck(List<CastlePlacement> plan, CastleSection section, float x, float top, float z, float width, float depth)
            => Box(plan,section,P(x,top-.3f,z),P(width,.6f,depth));

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

        static void Segment(List<CastlePlacement> plan, CastleSection section, float width, Vector3 from, Vector3 to)
        {
            var rotation=Quaternion.LookRotation(to-from,Vector3.up);
            var center=(from+to)*.5f-rotation*Vector3.up*.3f;
            plan.Add(new CastlePlacement(center,rotation,section,Mathf.FloorToInt(center.y/2),P(width,.6f,Vector3.Distance(from,to))));
        }
    }
}

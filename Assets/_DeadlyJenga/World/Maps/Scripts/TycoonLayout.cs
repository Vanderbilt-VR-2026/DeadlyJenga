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
                Tower(plan, settings, side * 24, 12, 10, red);
                Tower(plan, settings, side * 24, -24, 8, side < 0 ? green : red);
            }
            // Larger rear piers carry the promenade without another full stack of loose courses.
            Pier(plan,red,-3,23.4f,19,3.5f);
            Pier(plan,red,3,23.4f,19,3.5f);

            Core(plan, settings);

            // Banked crossing through the open front of the core.
            MoldedRoute(plan, red, 7.5f, TycoonDeckFinish.OpenDeck, 3.5f,
                P(-19.5f,18,-24), P(-15,19,-23), P(-9,18,-17), P(0,18,-15.75f),
                P(9,18,-17), P(15,19,-23), P(19.5f,18,-24));
            Route(plan, red, 6, P(28.5f,18,-24), P(31,18,-28));
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
            Pier(plan, red, 0,17.4f,-15.75f,4);
            Route(plan,purple,4,P(6,18,-10),P(9.5f,18,-6));
            // Wide red bearing towers frame the beach entrance and leave its middle bay open.
            Tower(plan,settings,-11,-24,7,red);
            Tower(plan,settings,11,-24,7,red);
            Deck(plan,red,-11,14.6f,-24,9,8);
            Deck(plan,red,11,14.6f,-24,9,8);

            // Two wraparound decks open back into the climbable core.
            MoldedRoute(plan,purple,7,TycoonDeckFinish.OpenDeck,4,
                P(-9.5f,24,6),P(-15,24,8),P(-15,24,16),P(0,24,19),P(15,24,16),P(15,24,9));
            MoldedRoute(plan,purple,8,TycoonDeckFinish.OpenDeck,4,
                P(-9.5f,36,6),P(-15,36,11),P(-19,36,14),P(-24,36,14));
            Deck(plan,purple,-20,36,22,8,8);
            Pier(plan,purple,-20,35.4f,20,3);
            foreach (var support in new[] { P(-15,24,7),P(-15,24,16),P(0,24,19),P(15,24,16),
                         P(15,24,9),P(-15,36,11),P(-19,36,14),P(-26,36,14) })
                Pier(plan,purple,support.x,support.y-.6f,support.z);

            // West flume, with flat transfer and shoreline runout.
            MoldedRoute(plan,green,6,TycoonDeckFinish.Flume,5,
                P(-29,36,14),P(-41,31,10),P(-42,25,-5),P(-29,19,-10),
                P(-30,13,-22),P(-43,6,-24));
            Deck(plan,green,-26.5f,36,14,5,8);
            foreach(int side in new[] {-1,1})
            {
                Box(plan,green,P(-26.5f,38.25f,14+side*3.25f),P(1.6f,4.5f,1.6f));
            }
            Deck(plan,green,-26.5f,41.1f,14,7.5f,8);
            Route(plan,green,4,P(-29,19,-10),P(-24,18,-15),P(-24,18,-20));
            Route(plan,green,4,P(-43,6,-31),P(-33,.9f,-20));
            foreach (var support in new[] { P(-40,30.7f,10),P(-41,24.6f,-4),P(-29,18.5f,-10),P(-31,12.1f,-22),P(-43,6,-24),P(-43,6,-31) })
                Pier(plan,green,support.x,support.y-.6f,support.z);
            Deck(plan,green,-43,6,-27.5f,6,7);

            MoldedRoute(plan,yellow,5,TycoonDeckFinish.Flume,2.4f,
                P(-9.5f,24,-6),P(-13,24,-9),P(-22,18,-21));
            MoldedRoute(plan,yellow,5,TycoonDeckFinish.Flume,1.8f,
                P(-25,18,-28),P(-37,6,-41));
            Pier(plan,purple,-13,23.4f,-9);

            Deck(plan,red,-37,6,-44,8,6);
            Pier(plan,red,-37,5.4f,-44);
            Route(plan,red,5,P(-33,6,-44),P(-17,.9f,-42));

            // The yellow chute occupies the rear side of the east wing before turning to the terrace.
            Route(plan,purple,4,P(-9.5f,60,6),P(-9.5f,60,15),P(10,60,20));
            foreach (var support in new[] { P(-9.5f,60,15),P(0,60,15),P(10,60,20) })
                Pier(plan,purple,support.x,support.y-.6f,support.z,3);
            MoldedRoute(plan,yellow,6.5f,TycoonDeckFinish.DryChute,5,
                P(10,60,20),P(21,52,15),P(37,37,0),P(43,28,-14),P(32,18,-28));
            Terrace(plan);
            Route(plan,green,4.5f,P(40,18,-30),P(45,12,-16),P(46,6,-31),P(39,6,-37),P(33,.9f,-22));
            foreach (var support in new[] { P(45,12,-16),P(46,6,-31),P(39,6,-37) })
                Pier(plan,green,support.x,support.y-.6f,support.z);
            Route(plan,aqua,4,P(24,18,-24),P(22,12,-36),P(39,6,-37));
            Pier(plan,green,22,11.4f,-36);

            // Ground-level route stays available when a bridge loses its support.
            MoldedRoute(plan,purple,5.5f,TycoonDeckFinish.OpenDeck,4.5f,
                P(-36,.9f,5),P(-40,.9f,-8),P(-35,.9f,-19),P(-38,.9f,-34),P(-32,.9f,-41),
                P(-13,.9f,-43),P(0,.9f,-38),P(19,.9f,-41),P(31,.9f,-35),P(33,.9f,-22));
            foreach (var foot in new[] { P(-36,0,5),P(-40,0,-8),P(-35,0,-19),P(-38,0,-34),P(-32,0,-41),P(-13,0,-43),P(31,0,-35) })
                Pier(plan,purple,foot.x,.3f,foot.z,2.8f);
            foreach (var foot in new[] { P(-33,0,-20),P(-38,0,-35),P(-28,0,-41),P(-12,0,-41),
                         P(0,0,-38),P(20,0,-41),P(33,0,-22),P(26.5f,0,-31.5f),P(-35.5f,0,-27.5f) })
                Pier(plan,purple,foot.x,.3f,foot.z,2.4f);
            Route(plan,purple,5,P(0,.9f,-38),P(0,.9f,-23),P(0,0,-12),P(-6.25f,0,-3));
            MoldedRoute(plan,purple,7,TycoonDeckFinish.OpenDeck,3.2f,
                P(-9.5f,24,6),P(-18,24,6),P(-19,24,0),P(-18,24,-6),P(-9.5f,24,-6));
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

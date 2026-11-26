using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class TidalCircuitMap
    {
        const int Red = 0, Green = 1, Purple = 2, Yellow = 3;
        const float Slab = .4f;
        static Vector3 V(float x, float y, float z) => new Vector3(x,y,z);
        static MayhemShape Box(Vector3 center, Vector3 size, int color, Quaternion rotation = default)
            => new MayhemShape(center,size,color,rotation == default ? Quaternion.identity : rotation);

        TidalCircuitPiece Unit(string name, Vector3 position, params MayhemShape[] shapes)
        {
            var module = new MayhemModule { purpose = name, position = position };
            module.shapes.AddRange(shapes);
            var obj = new GameObject(name) { layer = DeadlyJenga.Core.Layers.Block };
            obj.transform.SetParent(generated,false);
            obj.transform.localPosition = position;
            obj.transform.localRotation = shapes[0].rotation;
            var body = obj.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var member = obj.AddComponent<MayhemStructuralMember>();
            member.Build(module, palette, friction, true);
            var piece = obj.AddComponent<TidalCircuitPiece>();
            piece.Map = this;
            piece.Body = body;
            piece.Rest = position;
            member.StructuralLoad = force => { if (force.magnitude >= 4) piece.Unseat(); };
            pieces.Add(piece);
            bodies.Add(body);
            return piece;
        }

        void Land(float x,float z,float rx,float rz) => shores.Add(new Vector4(x,z,rx,rz));
        TidalCircuitPiece Pad(string name,float x,float z,float top,float width,float depth,
            TidalCircuitPiece lower = null, float bottom = 0)
        {
            var surface = Unit(name, V(x,top-Slab/2,z),Box(Vector3.zero,V(width,Slab,depth),Red));
            float height = top-Slab-bottom;
            foreach (int side in new[]{-1,1})
            foreach (int end in new[]{-1,1})
            {
                var post = Unit(name+" post "+side+" "+end,
                    V(x+side*(width/2-.42f),bottom+height/2,z+end*(depth/2-.42f)),
                    Box(Vector3.zero,V(.62f,height,.62f),Green));
                post.Supported.Add(surface);
                if (lower) lower.Supported.Add(post);
            }
            if (!lower) Land(x,z,width/2+.85f,depth/2+.85f);
            return surface;
        }

        // Route endpoints are the edges of the two seats, at walking-surface height.
        TidalCircuitPiece Span(string name,Vector3 a,Vector3 b,float width,TidalCircuitPiece first,TidalCircuitPiece last,int finish=Purple,bool stairs=false)
        {
            var delta=b-a;
            var rot=Quaternion.LookRotation(delta);
            var center=(a+b)/2-rot*Vector3.up*.16f;
            var shapes=new List<MayhemShape>{Box(Vector3.zero,V(width,.32f,delta.magnitude+.12f),finish,rot)};
            if(stairs)
            {
                // Treads flank a smooth central lane that also admits small survivor capsules.
                var yaw=Quaternion.LookRotation(Vector3.ProjectOnPlane(delta,Vector3.up));
                var side=yaw*Vector3.right*(width/2+.14f);
                int count=Mathf.CeilToInt(Mathf.Abs(delta.y)/.21f);
                float depth=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude/count;
                for(int i=0;i<count;i++)
                {
                    var pos=Vector3.Lerp(a,b,(i+.5f)/count)-center-Vector3.up*.15f;
                    var tread=V(.32f,.3f,depth+.02f);
                    shapes.Add(Box(pos+side,tread,finish,yaw));
                    shapes.Add(Box(pos-side,tread,finish,yaw));
                }
            }
            var seat=Unit(name,center,shapes.ToArray());
            first.Supported.Add(seat);
            if (last != first) last.Supported.Add(seat);
            return seat;
        }

        // Seat coordinates are taken from Concept Match in meters (Blender XY becomes Unity XZ).
        readonly Dictionary<string, RouteSeat> routeSeats = new();

        readonly struct RouteSeat
        {
            public readonly Vector3 Top;
            public readonly Vector2 Size;
            public readonly TidalCircuitPiece Deck;

            public RouteSeat(Vector3 top, Vector2 size, TidalCircuitPiece deck)
            {
                Top = top;
                Size = size;
                Deck = deck;
            }

            public Vector3 Edge(Vector3 direction)
            {
                float x = Size.x * .5f / Mathf.Max(.001f, Mathf.Abs(direction.x));
                float z = Size.y * .5f / Mathf.Max(.001f, Mathf.Abs(direction.z));
                return Top + direction * Mathf.Min(x, z);
            }
        }

        void Add(string name, float x, float z, float height, float width, float depth, string lower = null)
        {
            var seat = lower == null ? default : routeSeats[lower];
            var deck = Pad(name, x, z, height, width, depth, seat.Deck, seat.Top.y);
            routeSeats.Add(name, new RouteSeat(V(x, height, z), new Vector2(width, depth), deck));
        }

        void Path(params string[] seats)
        {
            for (int i = 1; i < seats.Length; i++)
            {
                var a = routeSeats[seats[i - 1]];
                var b = routeSeats[seats[i]];
                int color = i % 3 == 2 && Mathf.Abs(a.Top.y - b.Top.y) < .3f ? Red : Purple;
                Join(seats[i - 1], seats[i], color: color);
            }
        }

        void Join(string from, string to, float width = 2.1f, bool graded = true, int color = Purple)
        {
            var a = routeSeats[from];
            var b = routeSeats[to];
            Vector3 heading = Vector3.ProjectOnPlane(b.Top - a.Top, Vector3.up).normalized;
            Vector3 start = a.Edge(heading);
            Vector3 end = b.Edge(-heading);
            if (Vector3.Dot(end - start, heading) < .12f)
            {
                // Adjacent decks at the same level already meet.
                if (Mathf.Abs(start.y - end.y) < .14f) return;
                var seam = (start + end) * .5f;
                start = V(seam.x, start.y, seam.z) - heading * .65f;
                end = V(seam.x, end.y, seam.z) + heading * .65f;
            }
            Span(from + " to " + to, start, end, width, a.Deck, b.Deck, color,
                graded && Mathf.Abs(start.y - end.y) > .61f);
        }

        void BuildCircuit()
        {
            routeSeats.Clear();
            Add("Hub lower", 0, -2, 2.7f, 21, 12);
            Add("Hub middle", 0, -0.6f, 6.25f, 17, 9, "Hub lower");
            Add("Hub crown", -0.5f, 0.25f, 9.65f, 10.5f, 5.3f, "Hub middle");
            Add("Near west elbow", -13.2f, -9, 2.5f, 4.7f, 4);
            Add("Near east elbow", 12.0f, -10.4f, 2.7f, 5.3f, 4.2f);
            Add("Inner west", -19.2f, -5.5f, 2.85f, 5.3f, 3.2f);
            Add("Inner east", 16, -4, 4.4f, 4.5f, 3.3f);
            Add("Hub rear entry", -4.7f, 7.2f, 5.3f, 5.4f, 3.1f);
            Add("Hub rear crown access", 4.3f, 7.6f, 7.8f, 4.8f, 3.2f);
            Add("Under hub landing", 5.5f, -15.0f, 1.24f, 5, 3.2f);
            Path("Near west elbow", "Inner west", "Hub lower", "Inner east");
            Path("Hub rear entry", "Hub rear crown access", "Hub crown");

            Add("West drum deck", -40.5f, -28.5f, 1.35f, 6.6f, 3.6f);
            Add("West water bend", -40.5f, -12.8f, 1.2f, 4.0f, 3.6f);
            Add("West lower tower", -42, -6.3f, 2.48f, 4, 3);
            Add("West middle landing", -40, -0.8f, 3.45f, 5, 3.1f);
            Add("West teeter foot", -41.5f, 4.6f, 3.6f, 4.6f, 3.15f);
            Add("West back tower", -40.5f, 16.5f, 5.0f, 5.3f, 3.1f);
            Add("Near west island", -23.7f, -22, 1.25f, 6.4f, 3.5f);
            Add("Near west turn", -15.9f, -22.0f, 1.4f, 7, 3);
            Add("Near center west", -7.3f, -20, 1.65f, 6.7f, 3.3f);
            Add("Near center", 0.5f, -18.4f, 1.46f, 4.8f, 3.4f);
            Add("Near center east", 8, -19.5f, 1.3f, 6.2f, 3.7f);
            Add("East foreground deck", 17.5f, -21.8f, 1.25f, 6.9f, 3.45f);
            Add("East shoreline link", 26.2f, -21, 1.43f, 6.2f, 3.2f);
            Add("East low corner", 40.8f, -23.8f, 1.35f, 5.2f, 3.1f);
            Add("East low bend", 41.7f, -17.9f, 2.9f, 4.0f, 3.4f);
            Add("East rim", 42, -9, 3.7f, 4.3f, 4.6f);
            Add("East mid tower", 40.6f, -1.0f, 4.22f, 4.4f, 3.1f);
            Add("East water tower", 39.3f, 6.3f, 4.4f, 4.8f, 3.3f);
            Add("East high turn", 37.8f, 13.0f, 6.14f, 4.5f, 3.2f);
            Path("West back tower", "West teeter foot", "West middle landing", "West lower tower", "West water bend", "West drum deck", "Near west island", "Near west turn", "Near center west", "Near center", "Near center east", "East foreground deck", "East shoreline link", "East low corner", "East low bend", "East rim", "East mid tower", "East water tower", "East high turn");

            Add("West gallery", -17.0f, 11.6f, 5.6f, 5.3f, 4.5f);
            Add("West stair tower", -14.0f, 17.0f, 6.4f, 4.1f, 3.2f);
            Add("Back west low", -14, 22.5f, 6.4f, 4.5f, 3.1f);
            Add("Back west crown", -15.0f, 29.8f, 9.2f, 4.75f, 3.2f);
            Add("Back center low", -5.5f, 24.6f, 7.15f, 4.4f, 3.1f);
            Add("Back center crown", -5, 31.8f, 9.8f, 5.6f, 3.2f);
            Add("Back east link", 3.8f, 26, 7.8f, 4.2f, 3.2f);
            Add("Back east crown", 12.0f, 29.5f, 9.6f, 5.05f, 3.6f);
            Add("Back right crossover", 16.0f, 22.6f, 7.3f, 4.8f, 3.2f);
            Add("Back right tower", 21.4f, 22.0f, 7.3f, 4.8f, 3.25f);
            Add("East gallery", 17.4f, 10.2f, 6.14f, 4.9f, 3.7f);
            Add("West inner landing", -19.6f, 0.8f, 4.4f, 4.3f, 3.4f);
            Add("West low link", -29.6f, 1.6f, 2.05f, 5.3f, 3);
            Add("West middle island", -25.3f, -5.0f, 2.5f, 4.8f, 3.5f);
            Add("West return stair", -16.8f, -17.7f, 1.75f, 5.5f, 3.5f);
            Add("East inner island", 18.2f, -10.6f, 2.2f, 5.25f, 3.4f);
            Add("East lower island", 14.5f, -16.1f, 1.59f, 4.8f, 3.3f);
            Add("East route seat", 22.0f, -3.6f, 4.17f, 3.6f, 3.1f);
            Add("Near hub path", -4.5f, -13.4f, 1.45f, 4.1f, 3.5f);
            Add("West upper elbow", -12.7f, 3, 6.25f, 4.2f, 3.1f);
            Add("East upper elbow", 12.75f, 2.1f, 6.25f, 4.3f, 3.1f);
            Path("West gallery", "West stair tower", "Back west low", "Back west crown", "Back center low", "Back center crown", "Back east link", "Back east crown", "Back right crossover", "Back right tower");
            Path("Near west turn", "West return stair", "Near west elbow");
            Path("Inner west", "West middle island", "West low link", "West middle landing");
            Path("West low link", "West inner landing", "West gallery");
            Path("Near center west", "Near hub path", "Under hub landing", "East lower island", "East inner island", "East route seat");
            Path("East lower island", "Near east elbow", "Hub lower");
            Join("East upper elbow", "East gallery", 3.35f, graded: false);
            Join("West inner landing", "West upper elbow");
            Join("West upper elbow", "Hub middle", 2.8f, graded: false);
            Join("East upper elbow", "Hub middle", 2.8f, graded: false);
            Join("Hub middle", "Hub rear entry");
            Join("Hub rear crown access", "Back center low", 2.1f);

            // The elbows share the middle deck edge. The spine reaches both upper tiers.
            Path("Back west low", "Back center low", "Back east link", "Back right crossover");
            Join("Near west elbow", "Hub lower");
            Join("Near west turn", "Near hub path");
            Join("East foreground deck", "East lower island");

            BuildMachines();
            BuildRightCrossover();
        }

        void BuildRightCrossover()
        {
            var west = routeSeats["Inner east"];
            var east = routeSeats["East route seat"];
            var heading = Vector3.ProjectOnPlane(east.Top - west.Top, Vector3.up).normalized;
            var start = west.Edge(heading) - heading * .02f;
            var end = east.Edge(-heading) + heading * .02f;
            var center = (start + end) * .5f;
            float height = center.y - .32f;
            var post = Unit("Right crossover pier post", V(center.x, height / 2, center.z),
                Box(Vector3.zero, V(.72f, height, .72f), Green));
            Land(center.x, center.z, .95f, .95f);
            // Separate rigid bodies release together when this pier is removed.
            var left = Vector3.Lerp(start, end, .32f);
            var right = Vector3.Lerp(start, end, .68f);
            Span("Right crossover west section", start, left, 2.8f, west.Deck, post, Red);
            Span("Midlevel right crossover", left, right, 2.8f, post, post, Red);
            Span("Right crossover east section", right, end, 2.8f, post, east.Deck, Red);
        }

        void Hinge(TidalCircuitPiece rotor,TidalCircuitPiece mount,Vector3 pivot,Vector3 axis,float limit,float speed)
        {
            rotor.Rotor=true;
            rotor.Body.isKinematic=replica;
            var joint=rotor.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody=mount.Body;
            joint.anchor=rotor.transform.InverseTransformPoint(transform.TransformPoint(pivot));
            joint.axis=rotor.transform.InverseTransformDirection(transform.TransformDirection(axis));
            joint.breakForce=4000; joint.breakTorque=4000;
            if(limit>0)
            {
                joint.useLimits=true;
                joint.limits=new JointLimits{min=-limit,max=limit,contactDistance=.5f};
                joint.spring=new JointSpring{spring=80,damper=28,targetPosition=0};
                joint.useSpring=true;
                tilters.Add(rotor.Body);
            }
            else
            {
                joint.motor=new JointMotor{targetVelocity=speed,force=180};
                joint.useMotor=!replica;
            }
            rotor.Hinge=joint;
            mount.Supported.Add(rotor);
        }


        void BuildMachines()
        {
            Drum();
            Bank("Teeter", -29.4f, 10, 10.3f, 2, 6.8f, 1.18f, 4.35f,
                "West teeter foot", "West back tower", "West gallery");
            Bank("Tipping ramp", 31.1f, -10.5f, 9.7f, 3, 5, 1.34f, 4.35f,
                "East low bend", "East route seat", "East mid tower");
            Sweeper();
        }

        static MayhemShape Brace(Vector3 foot, Vector3 head, float width, int color)
        {
            var delta = head - foot;
            return Box((head + foot) / 2, V(width, width, delta.magnitude),
                color, Quaternion.LookRotation(delta));
        }

        void Drum()
        {
            var center = V(-28.3f, 3.53f, -15.5f);
            var yaw = Quaternion.Euler(0, 37.2f, 0);
            var axis = yaw * Vector3.right;
            var heading = yaw * Vector3.forward;
            var shell = new List<MayhemShape>();
            for (int i = 0; i < 4; i++)
            {
                var roll = yaw * Quaternion.AngleAxis(45 * i, Vector3.right);
                shell.Add(Box(Vector3.zero, V(6.12f, 4.95f, 2.05f), Red, roll));
            }
            for (int i = 0; i < 8; i++)
            {
                var roll = yaw * Quaternion.AngleAxis(22.5f + 45 * i, Vector3.right);
                shell.Add(Box(roll * V(0, 2.62f, 0), V(5.6f, .22f, .59f), Yellow, roll));
            }
            shell.Add(Box(Vector3.zero, V(9.66f, .52f, .52f), Purple, yaw));
            var rotor = Unit("Tread drum", center, shell.ToArray());

            foreach (int side in new[] {-1, 1})
            {
                var basePoint = center + axis * (4.27f * side);
                basePoint.y = 0;
                var fork = Unit("Drum bearing " + side, basePoint,
                    Box(V(0, .18f, 0), V(1.4f, .36f, 3.45f), Green, yaw),
                    Brace(heading * -1.65f + V(0, .3f, 0), V(0, 3.4f, 0), .9f, Green),
                    Brace(heading * 1.65f + V(0, .3f, 0), V(0, 3.4f, 0), .9f, Green),
                    Box(V(0, 3.53f, 0), V(1.05f, .84f, .81f), Yellow, yaw));
                Land(basePoint.x, basePoint.z, 1.6f, 2.4f);
                if (side < 0) Hinge(rotor, fork, center, axis, 0, 11);
                else
                {
                    fork.Supported.Add(rotor);
                    var contacts = rotor.GetComponent<MayhemStructuralMember>().Shapes;
                    foreach (var cheek in fork.GetComponent<MayhemStructuralMember>().Shapes)
                        Physics.IgnoreCollision(contacts[contacts.Count - 1], cheek);
                }

                var arrival = center + heading * (5.65f * side);
                string name = "Drum boarding " + side;
                Add(name, arrival.x, arrival.z, 4.99f, 3.95f, 3.8f);
                Join(name, side < 0 ? "West water bend" : "West return stair");
            }
        }

        void Bank(string name, float x, float z, float angle, int count, float spacing,
            float depthScale, float top, string approach, string exit, string secondExit)
        {
            var yaw = Quaternion.Euler(0, angle, 0);
            var axis = yaw * Vector3.right;
            var heading = yaw * Vector3.forward;
            var center = V(x, 0, z);
            float length = 5.62f * depthScale;
            float offset = length / 2 + 1.95f;
            string south = name + " south landing", north = name + " north landing";
            var near = center - heading * offset;
            var far = center + heading * offset;
            Add(south, near.x, near.z, top, count * spacing, 2.8f);
            Add(north, far.x, far.z, top, count * spacing, 2.8f);
            Join(approach, south);
            Join(north, exit);
            Join(north, secondExit);
            for (int i = 0; i < count; i++)
            {
                var pos = center + axis * ((i - (count - 1) * .5f) * spacing);
                float width = spacing - .65f;
                float pivot = top - .28f;
                int finish = count == 2 ? Purple : Green;
                var hingePoint = pos + V(0, pivot, 0);
                var shapes = new List<MayhemShape> {
                    Box(V(0, pivot, 0), V(width+.38f, .38f, .38f), Purple, yaw),
                    Box(heading * (-length * .43f) + V(0, 1.4f, 0), V(.9f, 2.8f, .6f), Yellow, yaw),
                    Box(heading * (length * .43f) + V(0, 1.4f, 0), V(.9f, 2.8f, .6f), Yellow, yaw)
                };
                foreach (int side in new[] {-1, 1})
                {
                    var cheek = axis * ((width / 2 + .16f) * side);
                    shapes.Add(Brace(cheek - heading + V(0, .16f, 0), cheek + V(0, pivot, 0), .48f, finish));
                    shapes.Add(Brace(cheek + heading + V(0, .16f, 0), cheek + V(0, pivot, 0), .48f, finish));
                }
                var mount = Unit(name + " cradle " + i, pos, shapes.ToArray());
                Land(pos.x, pos.z, spacing * .57f, 2.15f);
                var panel = Unit(name + " panel " + i, hingePoint + Vector3.up * .08f,
                    Box(Vector3.zero, V(width, .4f, length), Red, yaw),
                    Box(heading * (-length * .42f) + V(0, .18f, 0), V(width * .87f, .12f, .35f), Yellow, yaw),
                    Box(heading * (length * .42f) + V(0, .18f, 0), V(width * .87f, .12f, .35f), Yellow, yaw));
                Hinge(panel, mount, hingePoint, axis, 15, 0);
            }
        }

        void Sweeper()
        {
            const float x = 28.1f, z = 13.5f, top = 6.14f, radius = 6.88f;
            TidalCircuitPiece core = null;
            for (int i = -4; i <= 4; i++)
            {
                float depth = radius * 2 / 9;
                float edgeZ = (Mathf.Abs(i) + .35f) * depth;
                float width = 2 * Mathf.Sqrt(radius * radius - edgeZ * edgeZ);
                string name = "Sweeper deck " + i;
                Add(name, x, z + i * depth, top, width, depth);
                if (i == 0) core = routeSeats[name].Deck;
            }
            Add("Sweeper west landing", 19.0f, 14.6f, top, 4.1f, 2.9f);
            Add("Sweeper low landing", 23.5f, 2.6f, 4.05f, 4.5f, 3.3f);
            Join("Back right tower", "Sweeper west landing");
            Join("East gallery", "Sweeper west landing");
            Join("Sweeper low landing", "East water tower");
            Join("Sweeper west landing", "Sweeper deck 0", 2.5f);
            Join("Sweeper deck 0", "East high turn", 2.5f);
            Join("Sweeper low landing", "Sweeper deck -4");
            var axle = Unit("Sweeper axle", V(x, top + .38f, z), Box(Vector3.zero, V(1.28f, .76f, 1.28f), Purple));
            core.Supported.Add(axle);
            var arms = new List<MayhemShape>();
            for (int i = 0; i < 4; i++)
            {
                var angle = Quaternion.Euler(0, i * 90 + 13, 0);
                arms.Add(Box(angle * V(0, 0, 3.51f), V(.82f, .4f, 5.55f), Yellow, angle));
                arms.Add(Box(angle * V(0, 0, 6.35f), V(.88f, .48f, .36f), Purple, angle));
            }
            var rotor = Unit("Four-arm sweeper", V(x, top + 1.05f, z), arms.ToArray());
            Hinge(rotor, axle, V(x, top + .76f, z), Vector3.up, 0, 10);
        }
    }
}

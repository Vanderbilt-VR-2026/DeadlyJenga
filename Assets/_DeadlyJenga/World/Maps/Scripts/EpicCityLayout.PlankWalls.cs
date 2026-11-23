using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Candy Sky Ring City: Jenga-style plank walls on every Jenga tower floor.
    // Each floor keeps one body (the Hand still pulls whole floors); the walls are extra shapes of that body.
    // Rows alternate like a real Jenga tower: one row of long planks, then a row of three plank ends with
    // window gaps between them, turned 90 degrees on the neighbouring walls.
    // Walls are added after every bridge exists, and any plank piece that would block a bridge, a stair
    // landing or a street door is left out, so the routes stay exactly as planned.
    public static partial class EpicCityLayout
    {
        public const float PlankRow = .55f, PlankThickness = 1.2f;

        sealed partial class Builder
        {
            // One wall band per floor: the module, its footprint, the floor's base height and height, and street doors.
            readonly List<(MayhemModule floor, float w, float depth, float baseY, float height, bool doors)> jengaFloors = new();

            void RememberFloor(MayhemModule floor, float w, float depth, int level) => jengaFloors.Add((floor, w, depth, 0, 4, level == 0));

            // Transplanted cassettes (the Downtown drop-stop tower, the lower Grape Heights keep) get the same walls,
            // so they read as buildings instead of bare frames. The keep's winged tier stays open: its wings swing
            // down from the spine's sides and players cross between them there.
            void RememberCassettes()
            {
                foreach (var m in plan.modules)
                {
                    int floors; float height;
                    switch (m.purpose)
                    {
                        case "Escape spine": floors = 3; height = 5; break; // built 1.25 times taller
                        case "Two-floor dropping cassette": floors = 2; height = 4; break;
                        case "Independent bookfall support spine": floors = 3; height = 4; break;
                        default: continue;
                    }
                    // The first four shapes are the corner posts, 2.5 m square, set in 1.25 m from each edge.
                    var post = m.shapes[0].center; float w = Mathf.Abs(post.x) * 2 + 2.5f, depth = Mathf.Abs(post.z) * 2 + 2.5f;
                    bool street = m.position.y < 2;
                    for (int f = 0; f < floors; f++) jengaFloors.Add((m, w, depth, f * height, height, street && f == 0));
                }
            }

            public void PlankWalls()
            {
                RememberCassettes();
                var keepClear = new List<(Vector3 c, Vector3 h, Quaternion q)>();
                foreach (var m in plan.modules)
                {
                    if (!m.purpose.Contains("crossing") && !m.purpose.Contains("trestle")) continue;
                    foreach (var s in m.shapes)
                    {
                        var q = m.rotation * s.rotation; var center = m.position + m.rotation * s.center; var size = s.size;
                        if (s.vertices != null)
                        {
                            var b = new Bounds(s.vertices[0], Vector3.zero); foreach (var v in s.vertices) b.Encapsulate(v);
                            center += q * b.center; size = b.size;
                        }
                        // Walking room: the deck itself plus 2.3 m of head room and a little to each side.
                        keepClear.Add((center + Vector3.up * 1.1f, size * .5f + new Vector3(.6f, 1.25f, .6f), q));
                    }
                }
                foreach (var wing in plan.modules)
                {
                    if (wing.purpose!="Hinged inhabited book wing") continue;
                    // An inset floor over the receiving foot carries the ground-level lounge.
                    // Stop it on the lounge side of the stair and above the bearing face.
                    Part(wing,new Vector3(0,.10f,4.6f),new Vector3(14,.20f,5.4f),1);
                    for(int level=0;level<=2;level++)
                    {
                        float y=level==0?.20f:level*4;
                        Part(wing,new Vector3(-4.8f,y+.55f,4.5f),new Vector3(3.65f,1.1f,1.26f),3);
                        Part(wing,new Vector3(4.8f,y+.29f,4.5f),new Vector3(3.76f,.58f,1.34f),3);
                    }
                }
                foreach (var f in jengaFloors)
                    if (plan.sites[f.floor.district].name != "Canyon south-east tower")
                        WallFloor(f.floor, f.w, f.depth, f.baseY, f.height, f.doors, keepClear);
            }

            void WallFloor(MayhemModule floor, float w, float depth, float baseY, float height, bool doors, List<(Vector3 c, Vector3 h, Quaternion q)> keepClear)
            {
                string districtName = plan.sites[floor.district].name;
                if (doors && (districtName.StartsWith("Cloud Harbor") || districtName.StartsWith("Sugar Market")))
                {
                    // These shop counters and classroom desks are solid cover on the same pullable floor.
                    var inward = Quaternion.Inverse(floor.rotation) * -floor.position;
                    var forward = Mathf.Abs(inward.x) > Mathf.Abs(inward.z)
                        ? new Vector3(Mathf.Sign(inward.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(inward.z));
                    var facing = Quaternion.LookRotation(forward);
                    float halfDepth = (forward.x == 0 ? depth : w) * .5f;
                    bool shop = districtName.StartsWith("Cloud Harbor");
                    foreach (int side in new[]{-1, 1})
                    {
                        var center = new Vector3(side * (shop ? 4 : 6), .54f, halfDepth - 2.8f);
                        Part(floor, facing * center, new Vector3(2.5f, 1.08f, 1.4f), 3, facing);
                    }
                }
                // Fixed installations ride the existing module. Keep the shop entrances,
                // center aisle and alternating stairs outside their collision footprints.
                if (districtName.StartsWith("Sugar Market") || districtName.StartsWith("Cloud Harbor"))
                    Part(floor,new Vector3(-w*.5f+2.1f,.55f,7.2f),new Vector3(1.26f,1.1f,3.65f),3);
                else if (districtName.StartsWith("Canyon south"))
                    // The south towers are open through alternate storeys. Fix the station to
                    // this module's outer landing, clear of the flight and its stair head.
                    Part(floor,new Vector3(-6.65f,4.55f,9.26f),new Vector3(3.65f,1.1f,1.26f),3);
                else if (districtName.StartsWith("Canyon"))
                    Part(floor,new Vector3(-6.5f,.55f,5.4f),new Vector3(1.26f,1.1f,3.65f),3);
                else if (floor.purpose=="Two-floor dropping cassette")
                    Part(floor,new Vector3(-4.8f,baseY+.55f,4.4f),new Vector3(3.65f,1.1f,1.26f),3);
                else if (floor.purpose=="Escape spine" && baseY==10)
                    Part(floor,new Vector3(-4.5f,10.55f,1.8f),new Vector3(1.26f,1.1f,3.65f),3);
                if (floor.purpose == "Independent bookfall support spine")
                {
                    Part(floor,new Vector3(-4.6f,baseY+.55f,-18.2f),new Vector3(3.65f,1.1f,1.26f),3);
                    Part(floor,new Vector3(-5.5f,baseY+.54f,-8),new Vector3(1.24f,1.08f,4.38f),3);
                    Part(floor,new Vector3(-4.4f,baseY+.39f,21),new Vector3(3.10f,.78f,4.84f),3);
                    Part(floor,new Vector3(0,baseY+.28f,7.1f),new Vector3(3.86f,.56f,1.42f),3);
                    Part(floor,new Vector3(5.70f,baseY+1.26f,14),new Vector3(.15f,2.48f,3.66f),3);
                    // A bedroom wall stops short of the stair and leaves a passage on either side.
                    Part(floor,new Vector3(-4.4f,baseY+1.62f,23.55f),new Vector3(8.8f,3.24f,.34f),0);
                    // Short room dividers and window seats leave the central passage and stairwell clear.
                    foreach (int side in new[]{-1, 1})
                    foreach (int end in new[]{-1, 1})
                    {
                        float z = end * depth * .23f;
                        Part(floor,new Vector3(side*(w*.5f-2.25f),baseY+1.45f,z),
                            new Vector3(4.5f,2.9f,.48f),1);
                        Part(floor,new Vector3(side*(w*.5f-1.4f),baseY+.52f,z+2.3f),
                            new Vector3(2.1f,1.04f,3.6f),3);
                    }
                }
                if (doors && districtName == "Canyon south-east tower")
                    Part(floor, new Vector3(2.1f,.3f,-2.5f), new Vector3(13.6f,.6f,9.6f),1);
                bool classrooms=districtName.StartsWith("Sugar Market");
                bool shops=districtName.StartsWith("Cloud Harbor");
                if(classrooms || shops)
                {
                    // The cassette stair switches sides in the rear half. Keep a central,
                    // furniture-free passage from the street entrance to that stair.
                    foreach(int side in new[]{-1,1})
                    {
                        var x=side*w*.26f;
                        float wallX=side*(w*.5f-1.65f);
                        if(classrooms)
                        {
                            bool bookshelf=side<0 || districtName=="Sugar Market west annex" || floor.purpose.EndsWith(" floor 2");
                            if(bookshelf)
                                Part(floor,new Vector3(wallX+side*.32f,1.26f,2.4f),new Vector3(.14f,2.48f,3.65f),3);
                            else
                                Part(floor,new Vector3(wallX,1.92f,2.4f),new Vector3(.16f,1.98f,4.26f),3);
                        }
                        else if(districtName=="Cloud Harbor block")
                            Part(floor,new Vector3(side*(w*.5f-1.15f),1.35f,-.4f),new Vector3(.18f,2.65f,4.54f),3);
                        else if(districtName=="Cloud Harbor west annex")
                            Part(floor,new Vector3(side*(w*.5f-1.55f),.28f,-.4f),new Vector3(1.40f,.56f,3.86f),3);
                        else
                            Part(floor,new Vector3(side*(w*.5f-1.55f),.70f,-.4f),new Vector3(1.34f,1.38f,3.64f),3);
                        if(classrooms)
                        {
                            foreach(float z in new[]{1.4f,depth*.27f})
                            {
                                Part(floor,new Vector3(x,.87f,z),new Vector3(2.8f,.18f,1.18f),3);
                                Part(floor,new Vector3(x+.24f,.50f,z-1.13f),new Vector3(.90f,.16f,.87f),3);
                                foreach(int leg in new[]{-1,1})
                                    Part(floor,new Vector3(x+leg*1.1f,.39f,z),new Vector3(.17f,.78f,.92f),3);
                            }
                        }
                        else
                        {
                            // A low shop island gives cover without closing the view down the aisle.
                            Part(floor,new Vector3(x,.61f,1.4f),new Vector3(3.4f,1.22f,1.54f),3);
                        }
                    }
                    for(int wall=0;wall<4;wall++)
                    {
                        bool alongX=wall<2;
                        float sign=wall%2==0?-1:1;
                        float length=(alongX?w:depth)-4.8f;
                        float offset=(alongX?depth:w)*.5f-.68f;
                        // Two sash openings flank a full-height entrance. Width and head
                        // height are cut out of the collider as well as the visible wall.
                        for(int side=-1;side<=1;side+=2)
                        {
                            float start=2.35f, end=length*.5f;
                            float middle=side*(start+end)*.5f;
                            void Panel(float x,float y,float width,float h)
                            {
                                var pos=Local(alongX,sign,offset,baseY+y,x);
                                var size=alongX?new Vector3(width,h,.46f):new Vector3(.46f,h,width);
                                var world=floor.position+floor.rotation*pos;
                                foreach(var route in keepClear)
                                    if(Overlaps(world,size*.5f,floor.rotation,route.c,route.h,route.q)) return;
                                Part(floor,pos,size,0);
                            }
                            Panel(middle,.46f,end-start,.92f);
                            Panel(middle,3.07f,end-start,.42f);
                            // The existing full-height corner post closes the outside jamb.
                            Panel(side*(start+.29f),1.89f,.58f,1.94f);
                        }
                    }
                    return;
                }
                if (floor.purpose=="Independent bookfall support spine" && floor.position.y>20) return;
                if (floor.purpose=="Independent bookfall support spine")
                {
                    // Window piers enclose the apartment suites along the spine's long
                    // elevations. The bridge thresholds and the central stair doors stay open.
                    for(int sign=-1;sign<=1;sign+=2)
                    for(int bay=-3;bay<=3;bay++)
                    {
                        if(bay==0) continue;
                        float z=bay*depth/8;
                        float x=sign*(w*.5f-.86f);
                        var pos=new Vector3(x,baseY+1.66f,z);
                        var size=new Vector3(.46f,3.30f,2.65f);
                        bool obstructs=keepClear.Exists(k=>Overlaps(floor.position+floor.rotation*pos,size*.5f,floor.rotation,k.c,k.h,k.q));
                        if(!obstructs) Part(floor,pos,size,0);
                    }
                }
                int rows = Mathf.FloorToInt((height - .7f) / PlankRow); // up to the underside of the deck above
                for (int wall = 0; wall < 4; wall++)
                {
                    bool alongX = wall < 2; float sign = wall % 2 == 0 ? -1 : 1;
                    float length = (alongX ? w : depth) - 4.8f; // between the corner posts
                    float offset = ((alongX ? depth : w) * .5f) - .6f - PlankThickness * .5f;
                    // The neighborhood's shops and plant galleries open onto the ring road.
                    var siteName = plan.sites[floor.district].name;
                    // Keep the two substations open around their existing posts and landings.
                    if (floor.purpose == "Escape spine" || floor.purpose == "Two-floor dropping cassette" ||
                        siteName == "Canyon south-east tower" || siteName == "Canyon south-west tower")
                        continue;
                    // Low sills define rooms while leaving the long apartment galleries and classrooms open.
                    bool apartment = floor.purpose == "Independent bookfall support spine";
                    for (int row = 0; row < rows; row++)
                    {
                        float y = baseY + .03f + PlankRow * (row + .5f);
                        if (apartment && row > 0 && row < rows - 1) continue;
                        bool planks = (row % 2 == 0) == alongX;
                        // Street doors on the ground floor, front and back.
                        bool door = doors && alongX && row < 4;
                        if (planks) Run(floor, alongX, sign, offset, y, -length * .5f, length * .5f, door, keepClear);
                        else foreach (float at in new[] { -length * .25f, 0, length * .25f })
                                Run(floor, alongX, sign, offset, y, at - 1.2f, at + 1.2f, door, keepClear);
                    }
                }
            }

            // One plank (or plank end) along a wall from a to b, split around anything that must stay open.
            void Run(MayhemModule floor, bool alongX, float sign, float offset, float y, float a, float b, bool door,
                List<(Vector3 c, Vector3 h, Quaternion q)> keepClear)
            {
                const float piece = .6f;
                int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / piece)); float step = (b - a) / count;
                var open = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    float x = a + (i + .5f) * step;
                    if (door && Mathf.Abs(x) < 2.6f) { open[i] = true; continue; }
                    var local = Local(alongX, sign, offset, y, x); var half = Half(alongX, step * .5f);
                    var c = floor.position + floor.rotation * local;
                    foreach (var k in keepClear) if (Overlaps(c, half, floor.rotation, k.c, k.h, k.q)) { open[i] = true; break; }
                }
                int start = -1;
                for (int i = 0; i <= count; i++)
                {
                    bool solid = i < count && !open[i];
                    if (solid && start < 0) start = i;
                    if (!solid && start >= 0)
                    {
                        float from = a + start * step, to = a + i * step;
                        if (to - from > .5f) Part(floor, Local(alongX, sign, offset, y, (from + to) * .5f), Half(alongX, (to - from) * .5f) * 2, 0);
                        start = -1;
                    }
                }
            }

            static Vector3 Local(bool alongX, float sign, float offset, float y, float x) => alongX ? new Vector3(x, y, sign * offset) : new Vector3(sign * offset, y, x);
            static Vector3 Half(bool alongX, float halfLength) => alongX ? new Vector3(halfLength, PlankRow * .5f, PlankThickness * .5f) : new Vector3(PlankThickness * .5f, PlankRow * .5f, halfLength);

            // Separating-axis test for two oriented boxes.
            static bool Overlaps(Vector3 ca, Vector3 ha, Quaternion qa, Vector3 cb, Vector3 hb, Quaternion qb)
            {
                var A = new[] { qa * Vector3.right, qa * Vector3.up, qa * Vector3.forward };
                var B = new[] { qb * Vector3.right, qb * Vector3.up, qb * Vector3.forward };
                var d = cb - ca;
                bool Separated(Vector3 axis)
                {
                    if (axis.sqrMagnitude < 1e-6f) return false;
                    axis.Normalize();
                    float ra = ha.x * Mathf.Abs(Vector3.Dot(A[0], axis)) + ha.y * Mathf.Abs(Vector3.Dot(A[1], axis)) + ha.z * Mathf.Abs(Vector3.Dot(A[2], axis));
                    float rb = hb.x * Mathf.Abs(Vector3.Dot(B[0], axis)) + hb.y * Mathf.Abs(Vector3.Dot(B[1], axis)) + hb.z * Mathf.Abs(Vector3.Dot(B[2], axis));
                    return Mathf.Abs(Vector3.Dot(d, axis)) > ra + rb;
                }
                for (int i = 0; i < 3; i++) { if (Separated(A[i]) || Separated(B[i])) return false; }
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) if (Separated(Vector3.Cross(A[i], B[j]))) return false;
                return true;
            }
        }
    }
}

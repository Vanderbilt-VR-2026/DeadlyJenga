using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class ShrineTownLayout
    {
        public const string Preset = "shrine-town";
        readonly List<CastlePlacement> pieces = new List<CastlePlacement>(360);

        readonly Transform marshSupports;


        ShrineTownLayout(Transform supports) { marshSupports = supports; }

        public static List<CastlePlacement> Build(Transform supports = null)
        {
            var town = new ShrineTownLayout(supports);
            ShrineReviewedStructure.AddHall(town.Box);
            town.Hall(-32, 27, 10, 10, 0, 5, 3, 2, true);
            town.Hall(34, 16, 10, 10, 0, 3.1f, 4, 2);
            town.Hall(-30, -2, 19, 13, 0, 5.25f, 2, 3);
            town.Hall(-10, -2, 9, 9, 0, 5.25f, 2, 2);
            // Bridge ends bear on both first-storey decks.
            town.Box(-18.5f, 6.85f, -2, 8, .5f, 1.6f, CastleSection.Keep);
            for (int side = -1; side <= 1; side += 2)
            {
                town.Hall(side * 10, -29, 9, 8, 0, 4.4f, 1, 2);
                town.Hall(side * 11, -15, 10, 8, 0, 3, 2, 2, yaw: side * 20);
                town.Hall(side * 29, -32, 12, 9, 0, 4.8f, 1, 3, yaw: side * 42);
                town.Hall(side * 32, -17, 9, 8, 0, 4.6f, 1, 2, yaw: side * 65);
            }
            town.Hall(29, 0, 14, 10, 0, 4, 2, 3);
            town.Hall(10, -2, 8, 8, 0, 3, 1, 2);
            town.IslandTower(-45, 6, 12, 5, 4.65f, false);
            town.Hall(17, -40, 8, 6, 0, 3.8f, 1, 2);
            town.Hall(-19, -38, 7, 6, 0, 3.4f, 1, 2);
            town.IslandTower(46, 9, 14, 3, 7.2f, true);
            return town.pieces;
        }

        void Mud(float x,float z,float width,float depth,float yaw)
        {
            var bank = Shape(marshSupports,"Marsh mud",new Vector3(x,0,z),Quaternion.Euler(0,yaw,0));
            bank.localScale = new Vector3(width,1,depth);
        }

        public static void JoinTimberFrames(List<CastlePlacement> layout, List<Rigidbody> bodies)
        {
            // Each lintel follows the uprights in its row. Peg only the two outside posts.
            // The frame still bears freely on its floor and the roof remains a loose body.
            for (int i = 0; i < layout.Count; i++)
            {
                var beam = layout[i];
                if (beam.Section != CastleSection.Wall || Mathf.Abs(beam.Size.y - .6f) > .01f) continue;
                int last = i - 1, first = last;
                while (first > 0 && layout[first-1].Section == CastleSection.Wall && layout[first-1].Size.x == 1) first--;
                Join(bodies[i], bodies[first]);
                if (last != first) Join(bodies[i], bodies[last]);
            }
        }

        internal static void Join(Rigidbody beam, Rigidbody post)
        {
            var joint = beam.gameObject.AddComponent<FixedJoint>();
            joint.anchor = beam.transform.InverseTransformPoint(post.transform.TransformPoint(Vector3.up * .5f));
            joint.connectedBody = post;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = Vector3.up * .5f;
            joint.breakForce = (beam.mass + post.mass) * 9.81f * 8;
            joint.breakTorque = joint.breakForce;
            joint.enablePreprocessing = false;
        }

        public static void BuildStreetColliders(Transform parent,float waterY,Material wood)
        {
            // Bolted fixtures share a kinematic body. Openings between the gate posts stay clear.
            var fixtures = Shape(parent,"Fixed shrine fixtures",Vector3.zero,Quaternion.identity);
            var body = fixtures.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            // Mud has static collision so castle sand rules recognize an unsupported survivor.
            var banks = Shape(parent,"Shrine mud islands",Vector3.zero,Quaternion.identity);
            Build(banks);
            var town = new ShrineTownLayout(banks);
            town.Mud(0,20,41.8f,22.6f,0);
            town.Mud(0,5,11.5f,8.2f,0);
            ShrineMudBanks.Build(banks,wood);
            for (int side = -1; side <= 1; side += 2)
            {
                ColliderBox(fixtures,"Torii post",new Vector3(side*3.5f,4.65f,-43),new Vector3(1.1f,9.3f,1.1f),Quaternion.identity);
                for (int row = 0; row < 3; row++)
                    ColliderBox(fixtures,"Lantern pole",new Vector3(side*4.6f,2.7f,-32+row*12),new Vector3(.26f,5.4f,.26f),Quaternion.identity);
                for (int i = 0; i < 5; i++)
                    StoneLantern(fixtures,new Vector3(side*4,0,i == 4 ? -1 : -39+i*10),1);
            }
            foreach (Transform island in parent.parent.Find("Lake islands"))
                IslandRock(fixtures,island.localPosition + Vector3.up*waterY,island.localScale);
            ColliderBox(fixtures,"Torii crossbeam",new Vector3(0,7.4f,-43),new Vector3(8.65f,.61f,.77f),Quaternion.identity);
            ColliderBox(fixtures,"Torii lintel",new Vector3(0,9.32f,-43),new Vector3(12.5f,1.1f,1.05f),Quaternion.identity);
        }

        static Mesh isletHull;
        static void IslandRock(Transform fixtures,Vector3 at,Vector3 radii)
        {
            if (!isletHull)
            {
                const int sides = 11;
                var points = new List<Vector3>();
                var triangles = new List<int>();
                // A broad waterline and a narrower landing match the two stone tiers.
                Vector2[] profile = {new(.65f,-.65f),new(1,-.13f),new(.81f,.35f),new(.58f,.53f)};
                for (int ring = 0; ring < profile.Length; ring++)
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = 2*Mathf.PI*i/sides;
                        points.Add(new Vector3(Mathf.Cos(angle)*profile[ring].x,profile[ring].y,Mathf.Sin(angle)*profile[ring].x));
                        int a = ring*sides+i, b = ring*sides+(i+1)%sides;
                        if (ring > 0) triangles.AddRange(new[] {a,b,b-sides,a,b-sides,a-sides});
                        if (ring == profile.Length-1 && i>0 && i<sides-1) triangles.AddRange(new[] {ring*sides,b,a});
                    }
                isletHull = new Mesh {name = "Low-poly shrine islet"};
                isletHull.SetVertices(points);
                isletHull.SetTriangles(triangles,0);
            }
            var rock = Shape(fixtures,"Stone islet",at,Quaternion.identity);
            rock.localScale = radii;
            var collider = rock.gameObject.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = isletHull;
        }

        static void StoneLantern(Transform fixtures,Vector3 foot,float scale)
        {
            ColliderBox(fixtures,"Stone lantern base",foot+Vector3.up*.15f*scale,
                new Vector3(.9f,.3f,.9f)*scale,Quaternion.Euler(0,45,0));
            ColliderBox(fixtures,"Stone lantern stem",foot+Vector3.up*.78f*scale,
                new Vector3(.34f,.96f,.34f)*scale,Quaternion.identity);
            ColliderBox(fixtures,"Stone lantern crown",foot+Vector3.up*1.65f*scale,
                new Vector3(.85f,1f,.85f)*scale,Quaternion.identity);
        }

        // Each pitched roof is one moving body. Both slopes travel with the deck beneath them.
        public static void AddRoofColliders(Rigidbody body, Vector3 size)
        {
            bool skirt = Mathf.Abs(size.y - 1) < .01f && size.x >= 30 && size.z > 4;
            if (!skirt && Mathf.Abs(size.y - 1.25f) > .01f && Mathf.Abs(size.y - 1.35f) > .01f && Mathf.Abs(size.y - 1.4f) > .01f) return;
            var frame = new GameObject("Roof collision").transform;
            frame.SetParent(body.transform, false);
            frame.gameObject.layer = body.gameObject.layer;
            frame.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
            if (skirt)
            {
                for (int edge=0; edge<4; edge++)
                {
                    var turn = Quaternion.Euler(0,edge*90,0);
                    float across = edge%2==0 ? size.x : size.z;
                    float depth = (edge%2==0 ? size.z : size.x)/2;
                    float width = depth * .19f;
                    var eave = new GameObject("Gallery eave");
                    eave.layer = frame.gameObject.layer;
                    eave.transform.SetParent(frame,false);
                    eave.transform.localPosition = turn * new Vector3(0,.55f+width*.36f,depth-width/2);
                    eave.transform.localRotation = turn * Quaternion.Euler(Mathf.Atan(.72f)*Mathf.Rad2Deg,0,0);
                    eave.AddComponent<BoxCollider>().size = new Vector3(across*.81f,.1f,width*Mathf.Sqrt(1+.72f*.72f));
                }
                return;
            }
            float run = size.z / 2, rise = run * .44f;
            for (int side = -1; side <= 1; side += 2)
            {
                var slope = new GameObject("Roof slope");
                slope.layer = body.gameObject.layer;
                slope.transform.SetParent(frame, false);
                slope.transform.localPosition = new Vector3(0, (size.y + rise) / 2, side * run / 2);
                slope.transform.localRotation = Quaternion.Euler(side * Mathf.Atan(.44f) * Mathf.Rad2Deg, 0, 0);
                slope.AddComponent<BoxCollider>().size = new Vector3(size.x, .22f, Mathf.Sqrt(run * run + rise * rise));
                if (Mathf.Abs(size.y - 1.25f) < .01f && size.x >= 15)
                {
                    float span = size.x * .38f, peak = rise + span * .32f;
                    var gable = new GameObject("Front gable slope");
                    gable.layer = body.gameObject.layer;
                    gable.transform.SetParent(frame, false);
                    gable.transform.localPosition = new Vector3(side * span / 2,(size.y + peak) / 2,-(run + .75f) / 2);
                    gable.transform.localRotation = Quaternion.Euler(0,0,-side * Mathf.Atan2(peak,span) * Mathf.Rad2Deg);
                    gable.AddComponent<BoxCollider>().size = new Vector3(Mathf.Sqrt(span * span + peak * peak),.15f,run + .75f);
                }
            }
        }

        // Ornamental edges can overlap in the dense streets. Keep the full silhouette on
        // the survivor proxy and retain the load-bearing slopes on the loose roof body.
        public static void AddRoofTraversal(CastleBlock block, Vector3 size)
        {
            bool skirt = Mathf.Abs(size.y - 1) < .01f && size.x >= 30 && size.z > 4;
            bool deck = Mathf.Abs(size.y - 1) < .01f && size.z > 4;
            if (!deck && Mathf.Abs(size.y - 1.25f) > .01f && Mathf.Abs(size.y - 1.35f) > .01f && Mathf.Abs(size.y - 1.4f) > .01f) return;
            var frame = block.CreateTraversalFrame();
            if (!frame) return;
            float half = size.z / 2;
            float overhang = Mathf.Clamp(half * .24f,.65f,1.7f);
            for (int edge = 0; edge < 4; edge++)
            {
                bool front = edge % 2 == 0;
                var turn = Quaternion.Euler(0,90 * edge,0);
                float depth = front ? half : size.x / 2;
                float width = overhang * depth / half;
                float across = (front ? size.x : size.z) * (1 + overhang / half);
                float drop = overhang * .38f;
                ColliderBox(frame, "Overhanging eave", turn * new Vector3(0,(size.y-drop)/2,depth+width/2),
                    new Vector3(across,.16f,Mathf.Sqrt(width*width+drop*drop)),
                    turn * Quaternion.Euler(Mathf.Atan2(drop,width)*Mathf.Rad2Deg,0,0));
            }
            if (skirt)
            {
                for (int edge=0; edge<4; edge++)
                {
                    var turn = Quaternion.Euler(0,edge*90,0);
                    float across = edge%2==0 ? size.x : size.z;
                    float depth = (edge%2==0 ? size.z : size.x)/2;
                    float width = depth * .19f;
                    var eave = new GameObject("Gallery eave");
                    eave.layer = frame.gameObject.layer;
                    eave.transform.SetParent(frame,false);
                    eave.transform.localPosition = turn * new Vector3(0,.55f+width*.36f,depth-width/2);
                    eave.transform.localRotation = turn * Quaternion.Euler(Mathf.Atan(.72f)*Mathf.Rad2Deg,0,0);
                    eave.AddComponent<BoxCollider>().size = new Vector3(across*.81f,.1f,width*Mathf.Sqrt(1+.72f*.72f));
                }
                block.RegisterTraversalShapes(frame);
                return;
            }
            if (deck)
            {
                for (int side = -1; side <= 1; side += 2)
                    ColliderBox(frame,"Gallery rail",new Vector3(0,1.2f,side*(half-.18f)),new Vector3(size.x+.3f,.16f,.18f),Quaternion.identity);
                block.RegisterTraversalShapes(frame);
                return;
            }
            float top = size.y / 2, rise = half * .44f;
            RoofPrism(frame,"Tiled roof",Vector3.up * top,new Vector3(size.x,rise,size.z),Quaternion.identity);
            if (Mathf.Abs(size.y - 1.25f) < .01f && size.x >= 15)
            {
                float span = size.x * .38f, length = half + .99f;
                RoofPrism(frame,"Front gable",new Vector3(0,top,-length/2),
                    new Vector3(length,rise+span*.32f,2*span),Quaternion.Euler(0,90,0));
            }
            if (Mathf.Abs(size.y - 1.4f) < .01f)
                ColliderBox(frame,"Suspended bell",Vector3.down*3.2f,new Vector3(2.6f,3f,2.6f),Quaternion.identity);
            if (Mathf.Abs(size.y - 1.35f) < .01f)
                ColliderBox(frame,"Pagoda spire", Vector3.up*(top+rise+2.8f),new Vector3(.54f,5.6f,.54f),Quaternion.identity);
            block.RegisterTraversalShapes(frame);
        }

        static Mesh roofPrism;
        static void RoofPrism(Transform parent,string name,Vector3 at,Vector3 size,Quaternion rotation)
        {
            if (!roofPrism)
            {
                roofPrism = new Mesh { name = "Timber roof prism",
                    vertices = new Vector3[] {new(-.5f,0,-.5f),new(-.5f,0,.5f),new(-.5f,1,0),
                        new(.5f,0,-.5f),new(.5f,0,.5f),new(.5f,1,0)},
                    triangles = new[] {0,1,2,3,5,4,0,3,4,0,4,1,1,4,5,1,5,2,2,5,3,2,3,0} };
            }
            var shape = Shape(parent,name,at,rotation);
            shape.localScale = size;
            var collider = shape.gameObject.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = roofPrism;
        }

        static Transform Shape(Transform parent,string name,Vector3 at,Quaternion rotation)
        {
            var shape = new GameObject(name).transform;
            shape.gameObject.layer = parent.gameObject.layer;
            shape.SetParent(parent,false);
            shape.SetLocalPositionAndRotation(at,rotation);
            return shape;
        }

        internal static void ColliderBox(Transform parent,string name,Vector3 at,Vector3 size,Quaternion rotation)
        {
            Shape(parent,name,at,rotation).gameObject.AddComponent<BoxCollider>().size = size;
        }

        void Box(float x, float bottom, float z, float width, float height, float depth, CastleSection section, float yaw = 0)
        {
            if (marshSupports) return;
            pieces.Add(new CastlePlacement(new Vector3(x, bottom + height / 2, z),
                Quaternion.Euler(0, yaw, 0), section, Mathf.FloorToInt(bottom / 2), new Vector3(width, height, depth)));
        }

        void IslandTower(float x, float z, float width, int tiers, float storeyHeight, bool bell)
        {
            if (marshSupports)
            {
                Mud(x,z,width + 1,width + 1,0);
                return;
            }

            const float pierDepth = 1.64f;
            float bottom = .5f;
            Box(x,0,z,width,bottom,width,CastleSection.Keep);
            for (int tier = 0; tier < tiers; tier++)
            {
                bool crown = tier == tiers - 1;
                if (crown && !bell)
                {
                    // A compact central lantern room carries the pagoda spire.
                    Box(x,bottom,z,width * .55f,storeyHeight,width * .55f,CastleSection.Keep);
                }
                else
                {
                    // Parallel bearing walls leave the tower open toward the market.
                    // Each balcony seats across both walls and carries the next pair.
                    float offset = width * .34f;
                    for (int side = -1; side <= 1; side += 2)
                        Box(x,bottom,z + side * offset,width * .80f,storeyHeight,pierDepth,CastleSection.Keep);
                }
                bottom += storeyHeight;
                float deck = crown ? (bell ? 1.4f : 1.35f) : 1;
                Box(x,bottom,z,width,deck,width,CastleSection.Corner);
                bottom += deck;
                width *= .91f;
            }
        }

        void Hall(float x, float z, float width, float depth, float y, float height, int levels, int columns, bool bell = false, float yaw = 0)
        {
            bool porch = yaw==0 && y==0 && (z==-29 || x==-30 && z==-2 || x==29 || bell || levels==4);
            if (marshSupports)
            {
                if (y==0)
                {
                    float extra = porch ? 2.8f : 0;
                    Mud(x,z-extra/2,width+1,depth+1+extra,yaw);
                }
                return;
            }
            int first = pieces.Count;
            if (porch) Box(x,0,z-depth/2-.95f,6,.4f,2.1f,CastleSection.Keep);
            for (int level = 0; level < levels; level++)
            {
                if (x == 0 && level == 1) height *= .6f;
                float span = width - 2, reach = depth - 2;
                for (int side = -1; side <= 1; side += 2)
                {
                    float row = z + side * reach / 2;
                    for (int bay = 0; bay < columns; bay++)
                        Box(x + span * (bay / (columns - 1f) - .5f), y, row, 1, height, 1, CastleSection.Wall);
                    Box(x, y + height, row, width, .6f, 1, CastleSection.Wall);
                }
                if (!bell || level + 1 < levels)
                {
                    float bayWidth = span / (columns - 1);
                    bool main = x == 0;
                    float panel = main ? (level == 0 ? .46f : .47f) : bell || levels == 4 ? .44f : level == 0 ? .41f : .42f;
                    for (int bay = 0; bay < columns - 1; bay++)
                    {
                        float center = x - span / 2 + (bay + .5f) * bayWidth;
                        bool sanctuary = main && bay == (columns - 2) / 2 && level == 0;
                        Box(center, y, sanctuary ? z - reach * .24f : z + reach / 2, bayWidth - 1.05f, height,
                            sanctuary ? .48f : .42f, CastleSection.Keep);
                        if (sanctuary) continue;
                        // A recessed shop front leaves a doorway beside the end post.
                        Box(center - (columns == 2 ? .55f : 0), y, z - reach / 2 + (main && level == 0 ? 2.1f : .75f),
                            bayWidth - (columns == 2 ? 2.2f : 1.05f), height, panel, CastleSection.Keep);
                    }
                    for (int side = -1; side <= 1; side += 2)
                        Box(x + side * span / 2, y, z, reach - 1.1f, height, .42f, CastleSection.Keep, 90);
                }
                y += height + .6f;
                bool last = level + 1 == levels;
                float cap = last ? (bell ? 1.4f : levels == 4 ? 1.35f : 1.25f) : 1;
                Box(x, y, z, width + 1, cap, depth + 1, CastleSection.Corner);
                y += cap;
                width *= .80f;
                depth *= .80f;
            }
            if (yaw == 0) return;
            var turn = Quaternion.Euler(0,yaw,0);
            var pivot = new Vector3(x,0,z);
            for (int i = first; i < pieces.Count; i++)
            {
                var part = pieces[i];
                pieces[i] = new CastlePlacement(pivot + turn * (part.Position - pivot),turn * part.Rotation,
                    part.Section,part.Course,part.Size);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The solid ship. Either your own hull prefab (hullPrefab, with a DeckShipHull on its root) or the
    // built-in box ship below. Both go through PrepareHull, so ship events rock whichever one is used.
    // Ship coordinates: origin at the waterline in the middle, x = starboard, y = up, z = toward the bow.
    public sealed partial class DeckShipMap
    {
        // Measurements of the built-in box ship only. Your own hull describes itself with DeckShipHull.
        public const float DeckHeight = 3f;     // main deck above the water
        public const float RaisedDeck = 4f;     // bow and stern decks above the main deck
        public const float HalfBeam = 10f;

        [Header("Ship build")]
        [Tooltip("Your own ship: a prefab with DeckShipHull on its root. Empty = the built-in box ship.")]
        public DeckShipHull hullPrefab;
        [Tooltip("Which built-in box ship to build when there is no hull prefab.")]
        public BoxShipSize boxShip = BoxShipSize.Standard;

        Rigidbody hullBody;
        Transform hull, collisionRoot, visualRoot;
        DeckShipHull ship;
        readonly List<Vector3> bayLocal = new List<Vector3>();
        readonly List<Quaternion> bayLocalRotation = new List<Quaternion>();
        Bounds hullLocalBounds;
        PhysicsMaterial deckMaterial;
        readonly List<Material> hullMaterials = new List<Material>();
        Material navy, red, cream, wood, white, yellow, glass, ice, iceShade;
        DeckShipHullClimb climb;

        public Transform Hull => hull;
        public DeckShipHull Ship => ship;
        // Cargo bays: centers and orientations right now (they move with the ship). Bay 0 is the main cargo area.
        public int BayCount => bayLocal.Count;
        public Vector3 BayCenter(int bay) => hullBody.position + hullBody.rotation * bayLocal[bay];
        public Quaternion BayRotation(int bay) => hullBody.rotation * bayLocalRotation[bay];
        public Vector3 CargoCenter => BayCenter(0);
        public Quaternion CargoRotation => BayRotation(0);
        // Each bay carries a different layout: bay i gets the map's cargo plus i (wrapping around).
        public DeckShipCargoKind BayCargo(int bay) => (DeckShipCargoKind)(((int)cargo + bay) % DeckShipCargo.Count);
        int BaySeed(int bay) => seed + bay * 101;
        // The hull's size in its own space, for camera framing and play area.
        public Bounds HullBounds => hullLocalBounds;

        void BuildHull()
        {
            deckMaterial = new PhysicsMaterial("Ship deck")
            {
                staticFriction = deckFriction + .08f, dynamicFriction = deckFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            MakeMaterials();
            if (hullPrefab)
            {
                ship = Instantiate(hullPrefab, transform, false);
                ship.name = hullPrefab.name;
                hull = ship.transform;
                hull.localPosition = Vector3.zero; hull.localRotation = Quaternion.identity;
            }
            else BuildBoxHull();
            PrepareHull();
            BuildIceberg();
        }

        // Makes any hull ready to be rocked: one kinematic body on the root, climbable Surface colliders,
        // slippery deck friction on solid colliders that have none, and the cargo deck's local pose.
        void PrepareHull()
        {
            foreach (var extra in hull.GetComponentsInChildren<Rigidbody>(true))
                if (extra.transform != hull) { Debug.LogWarning("Deck Ship: removed a Rigidbody inside the hull (" + extra.name + "); the whole hull moves as one body.", extra); if (Application.isPlaying) Destroy(extra); else DestroyImmediate(extra); }
            hullBody = hull.GetComponent<Rigidbody>();
            if (!hullBody) hullBody = hull.gameObject.AddComponent<Rigidbody>();
            hullBody.isKinematic = true; hullBody.useGravity = false;
            hullBody.interpolation = RigidbodyInterpolation.Interpolate;
            hullBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            climb = hull.GetComponent<DeckShipHullClimb>();
            if (!climb) climb = hull.gameObject.AddComponent<DeckShipHullClimb>();
            climb.Body = hullBody; climb.Faces.Clear();
            hullLocalBounds = new Bounds();
            bool first = true;
            foreach (var col in hull.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue;
                if (col.gameObject.layer == Layers.Surface) { Climbables.Register(col, climb); climb.Faces.Add(col); continue; }
                if (!col.sharedMaterial) col.sharedMaterial = deckMaterial;
                var b = col.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = hull.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (first) { hullLocalBounds = new Bounds(corner, Vector3.zero); first = false; } else hullLocalBounds.Encapsulate(corner);
                }
            }
            if (!ship.cargoArea)
            {
                Debug.LogWarning("Deck Ship: the hull has no cargo area marker; using its origin.", ship);
                ship.cargoArea = hull;
            }
            bayLocal.Clear(); bayLocalRotation.Clear();
            foreach (var bay in ship.Bays)
            {
                bayLocal.Add(hull.InverseTransformPoint(bay.position));
                bayLocalRotation.Add(Quaternion.Inverse(hull.rotation) * bay.rotation);
            }
            bodies.Insert(0, hullBody);
            hullBody.position = hull.position; hullBody.rotation = hull.rotation;
            Physics.SyncTransforms();
        }

        // Sizes of the built-in box ships. Standard is the original ship; Large has four times its deck area
        // (twice as long and wide), a higher deck, and four cargo bays so four submaps sail at once.
        public enum BoxShipSize { Standard, Large }

        struct BoxPlan
        {
            public float halfLength, halfBeam, deck;
            public Vector2[] bays;   // bay centers on the main deck (x across, z along)
        }

        static BoxPlan PlanFor(BoxShipSize size) => size == BoxShipSize.Large
            ? new BoxPlan { halfLength = 72, halfBeam = 22, deck = 6, bays = new[] { new Vector2(-11.6f, -17), new Vector2(11.6f, -17), new Vector2(-11.6f, 17), new Vector2(11.6f, 17) } }
            : new BoxPlan { halfLength = 36, halfBeam = HalfBeam, deck = DeckHeight, bays = new[] { Vector2.zero } };

        void BuildBoxHull()
        {
            hull = new GameObject("Ship (solid hull)").transform;
            hull.SetParent(transform, false);
            ship = hull.gameObject.AddComponent<DeckShipHull>();
            collisionRoot = new GameObject("Collision").transform; collisionRoot.SetParent(hull, false);
            visualRoot = new GameObject("Visual").transform; visualRoot.SetParent(hull, false);

            var plan = PlanFor(boxShip);
            float H = plan.halfLength, B = plan.halfBeam, d = plan.deck, top = d + RaisedDeck;
            float bottom = -Mathf.Max(2.5f, d * .7f);
            float midFrom = -(H - 6), midTo = H - 10, bowStart = H - 12, sternEnd = -(H - 12);
            float w1 = B * .8f, w2 = B * .55f, w3 = B * .3f, ws = B * .9f;   // half widths: bow steps and stern

            // Hull: a long midship box, a stepped bow and a squared stern. Their tops are the main deck.
            Slab("Hull midship", 0, B, midFrom, midTo, bottom, d, navy);
            Slab("Hull bow 1", 0, w1, midTo, H - 6, bottom + .5f, d, navy);
            Slab("Hull bow 2", 0, w2, H - 6, H - 3, bottom + 1, d, navy);
            Slab("Hull bow 3", 0, w3, H - 3, H - .5f, bottom + 1.5f, d, navy);
            Slab("Hull stern", 0, ws, -H, midFrom, bottom, d, navy);
            float stripe = d - 1.1f;
            Paint("Red stripe midship", new Vector3(0, stripe, (midFrom + midTo) * .5f), new Vector3(B * 2 + .08f, .7f, midTo - midFrom + .04f), red);
            Paint("Red stripe stern", new Vector3(0, stripe, (-H + midFrom) * .5f), new Vector3(ws * 2 + .08f, .7f, 6.04f), red);
            Paint("Red stripe bow", new Vector3(0, stripe, (midTo + H - 6) * .5f), new Vector3(w1 * 2 + .08f, .7f, 4.04f), red);
            Paint("Main deck boards", new Vector3(0, d - .04f, (midFrom + midTo) * .5f), new Vector3(B * 2 - .4f, .1f, midTo - midFrom - .4f), cream);
            Paint("Waterline", new Vector3(0, -.05f, (midFrom + midTo) * .5f), new Vector3(B * 2 + .1f, .3f, midTo - midFrom + .1f), red);

            // Raised bow deck (team end A) with two ramps up from the main deck.
            Slab("Bow deck 1", 0, w1, bowStart, H - 6, d, top, cream);
            Slab("Bow deck 2", 0, w2, H - 6, H - 3, d, top, cream);
            Slab("Bow deck 3", 0, w3, H - 3, H - .5f, d, top, cream);
            Paint("Bow stripe 1", new Vector3(0, d + 3.2f, (bowStart + H - 6) * .5f), new Vector3(w1 * 2 + .06f, .5f, H - 6 - bowStart + .06f), red);
            Paint("Bow stripe 2", new Vector3(0, d + 3.2f, H - 4.5f), new Vector3(w2 * 2 + .06f, .5f, 3.06f), red);
            Paint("Bow stripe 3", new Vector3(0, d + 3.2f, H - 1.75f), new Vector3(w3 * 2 + .06f, .5f, 2.56f), red);
            Rail(top, new[] { new Vector2(-w1, bowStart), new Vector2(-w1, H - 6), new Vector2(-w2, H - 6), new Vector2(-w2, H - 3), new Vector2(-w3, H - 3), new Vector2(-w3, H - .5f),
                              new Vector2(w3, H - .5f), new Vector2(w3, H - 3), new Vector2(w2, H - 3), new Vector2(w2, H - 6), new Vector2(w1, H - 6), new Vector2(w1, bowStart) }, .8f);
            float rampX = B * .55f;
            Ramp("Bow ramp port", new Vector3(-rampX, d, bowStart - 9), new Vector3(-rampX, top, bowStart), 3.5f);
            Ramp("Bow ramp starboard", new Vector3(rampX, d, bowStart - 9), new Vector3(rampX, top, bowStart), 3.5f);
            // Climb boards only between the ramps, so the ramp tops stay a smooth walk.
            float between = 2 * (rampX - 1.75f) - .2f;
            ClimbFace("Bow wall climb", new Vector3(0, d + 2, bowStart - .12f), new Vector3(between, 4, .2f));

            // Raised stern deck (team end B) with the wheelhouse and funnel(s), and two ramps.
            Slab("Stern deck", 0, ws, -H, sternEnd, d, top, cream);
            Paint("Stern stripe", new Vector3(0, d + 3.2f, (-H + sternEnd) * .5f), new Vector3(ws * 2 + .06f, .5f, sternEnd + H + .06f), red);
            Rail(top, new[] { new Vector2(-ws, sternEnd), new Vector2(-ws, -H), new Vector2(ws, -H), new Vector2(ws, sternEnd) }, .8f);
            Ramp("Stern ramp port", new Vector3(-rampX, d, sternEnd + 9), new Vector3(-rampX, top, sternEnd), 3.5f);
            Ramp("Stern ramp starboard", new Vector3(rampX, d, sternEnd + 9), new Vector3(rampX, top, sternEnd), 3.5f);
            ClimbFace("Stern wall climb", new Vector3(0, d + 2, sternEnd + .12f), new Vector3(between, 4, .2f));
            float wheelZ = -(H - 3.5f);
            Box("Wheelhouse", new Vector3(0, top + 2, wheelZ), new Vector3(B, 4, 5), white);
            Paint("Wheelhouse windows", new Vector3(0, top + 2.6f, wheelZ), new Vector3(B + .06f, 1.1f, 5.06f), glass);
            Paint("Wheelhouse roof", new Vector3(0, top + 4.05f, wheelZ), new Vector3(B + .4f, .14f, 5.4f), red);
            ClimbFace("Wheelhouse climb", new Vector3(0, top + 2, wheelZ + 2.62f), new Vector3(B, 4, .2f));
            int funnels = B > 12 ? 2 : 1;
            for (int f = 0; f < funnels; f++)
            {
                float fx = funnels == 1 ? 0 : (f == 0 ? -B * .22f : B * .22f);
                Cylinder("Funnel", new Vector3(fx, top + 5.75f, -(H - 2.8f)), new Vector3(2.6f, 1.75f, 2.6f), yellow, new Vector3(2.4f, 3.5f, 2.4f));
                Paint("Funnel band", new Vector3(fx, top + 7.1f, -(H - 2.8f)), new Vector3(2.7f, .25f, 2.7f), red, PrimitiveType.Cylinder);
            }

            // Main deck rails, with 3 m gaps every 15 m where cargo can slide overboard.
            var gaps = new List<float>();
            for (float g = sternEnd + 11; g + 3 <= midTo - 3; g += 15) { gaps.Add(g); gaps.Add(g + 3); }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (B - .2f);
                float from = sternEnd;
                for (int g = 0; g <= gaps.Count; g += 2)
                {
                    float to = g < gaps.Count ? gaps[g] : midTo;
                    RailSegment(new Vector2(x, from), new Vector2(x, to), d, 1.1f);
                    if (g + 1 < gaps.Count) from = gaps[g + 1];
                }
                RailSegment(new Vector2(x, midTo), new Vector2(side * w1, midTo), d, 1.1f);
            }

            // A fixed cargo hatch in the middle of every bay: a low wooden plateau the cargo builds on.
            foreach (var bay in plan.bays)
            {
                Box("Cargo hatch", new Vector3(bay.x, d + DeckShipCargo.HatchHeight * .5f, bay.y), new Vector3(DeckShipCargo.HatchHalf * 2, DeckShipCargo.HatchHeight, DeckShipCargo.HatchHalf * 2), wood);
                Paint("Hatch trim", new Vector3(bay.x, d + DeckShipCargo.HatchHeight - .05f, bay.y), new Vector3(DeckShipCargo.HatchHalf * 2 + .1f, .12f, DeckShipCargo.HatchHalf * 2 + .1f), red);
            }

            // Mast with a climbable ladder on its aft side and a crow's nest reached from the ladder's top.
            float mastZ = H - 17, nest = d + 10.5f;
            // The pole stops under the nest so a climber can step off the ladder top onto it.
            float pole = nest - d;
            Cylinder("Mast", new Vector3(0, d + pole * .5f, mastZ), new Vector3(.8f, pole * .5f, .8f), white, new Vector3(.7f, pole, .7f));
            ClimbFace("Mast ladder", new Vector3(0, d + pole * .5f + .05f, mastZ - .5f), new Vector3(1.2f, pole, .2f));
            Paint("Mast ladder rungs", new Vector3(0, d + pole * .5f, mastZ - .48f), new Vector3(1.1f, pole, .1f), yellow);
            Box("Crow's nest", new Vector3(0, nest + .15f, mastZ + 1.3f), new Vector3(3, .3f, 2.6f), wood);
            Rail(nest + .3f, new[] { new Vector2(-1.5f, mastZ), new Vector2(-1.5f, mastZ + 2.6f), new Vector2(1.5f, mastZ + 2.6f), new Vector2(1.5f, mastZ) }, .7f);
            Paint("Top mast", new Vector3(0, nest + 1.8f, mastZ + 2.4f), new Vector3(.14f, 3.2f, .14f), white);
            Paint("Mast flag", new Vector3(.8f, nest + 2.9f, mastZ + 2.4f), new Vector3(1.6f, 1, .05f), red);
            Paint("Bow pennant", new Vector3(0, top + 2.2f, H - 1.4f), new Vector3(.12f, 4.4f, .12f), white);
            Paint("Bow pennant flag", new Vector3(.6f, top + 3.9f, H - 1.4f), new Vector3(1.2f, .8f, .05f), red);
            Paint("Stern pennant", new Vector3(-ws * .83f, top + 2.2f, -(H - .7f)), new Vector3(.12f, 4.4f, .12f), white);
            Paint("Stern pennant flag", new Vector3(-ws * .83f + .6f, top + 3.9f, -(H - .7f)), new Vector3(1.2f, .8f, .05f), MakeColor("Mint", new Color(.31f, .89f, .76f)));
            for (int side = -1; side <= 1; side += 2)
                for (float z = sternEnd; z <= midTo - 2; z += 5.5f)
                {
                    var go = Visual("Porthole", PrimitiveType.Cylinder, new Vector3(side * (B + .02f), d * .3f, z), new Vector3(.9f, .04f, .9f), yellow);
                    go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }

            // Markers: the cargo bays and the spawn spots, exactly as a hull prefab would carry them.
            var bays = new Transform[plan.bays.Length];
            for (int i = 0; i < bays.Length; i++)
            {
                bays[i] = new GameObject(bays.Length == 1 ? "Cargo area" : "Cargo bay " + (i + 1)).transform;
                bays[i].SetParent(hull, false); bays[i].localPosition = new Vector3(plan.bays[i].x, d, plan.bays[i].y);
            }
            ship.cargoArea = bays[0];
            ship.extraCargoAreas = bays.Skip(1).ToArray();
            ship.cargoHalfSize = new Vector2(DeckShipCargo.FieldHalfWidth, DeckShipCargo.FieldMaxZ);
            ship.hatchHeight = DeckShipCargo.HatchHeight; ship.hatchHalf = DeckShipCargo.HatchHalf;
            var spots = new[]
            {
                new Vector3(0, 4, -(H - 10.5f)), new Vector3(-B * .35f, 4, -(H - 10)), new Vector3(B * .35f, 4, -(H - 10)), new Vector3(-B * .75f, 4, -(H - 8.5f)), new Vector3(B * .75f, 4, -(H - 8.5f)),
                new Vector3(0, 4, H - 10), new Vector3(-B * .5f, 4, H - 9), new Vector3(B * .5f, 4, H - 9),
                new Vector3(-(B - 1.5f), 0, -(H - 17)), new Vector3(B - 1.5f, 0, -(H - 17)), new Vector3(-(B - 1.5f), 0, H - 16), new Vector3(B - 1.5f, 0, H - 16),
            };
            var spawns = new Transform[spots.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                spawns[i] = new GameObject("Spawn " + (i + 1)).transform;
                spawns[i].SetParent(hull, false); spawns[i].localPosition = spots[i] + Vector3.up * d;
            }
            ship.spawnPoints = spawns;
        }

        // A solid box given by its x half width, z range and y range.
        void Slab(string name, float x, float halfWidth, float zFrom, float zTo, float yFrom, float yTo, Material material) =>
            Box(name, new Vector3(x, (yFrom + yTo) * .5f, (zFrom + zTo) * .5f), new Vector3(halfWidth * 2, yTo - yFrom, zTo - zFrom), material);

        // A solid box: collision plus matching gray-box art.
        void Box(string name, Vector3 center, Vector3 size, Material material)
        {
            Collide(name, center, size, Quaternion.identity);
            Visual(name, PrimitiveType.Cube, center, size, material);
        }

        // Art only.
        void Paint(string name, Vector3 center, Vector3 size, Material material, PrimitiveType shape = PrimitiveType.Cube) =>
            Visual(name, shape, center, size, material);

        void Cylinder(string name, Vector3 center, Vector3 scale, Material material, Vector3 collider)
        {
            Collide(name, center, collider, Quaternion.identity);
            Visual(name, PrimitiveType.Cylinder, center, scale, material);
        }

        BoxCollider Collide(string name, Vector3 center, Vector3 size, Quaternion rotation, int layer = 0)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(collisionRoot, false);
            go.transform.localPosition = center; go.transform.localRotation = rotation;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            if (layer == 0) box.sharedMaterial = deckMaterial;
            return box;
        }

        GameObject Visual(string name, PrimitiveType shape, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col) { col.enabled = false; if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(visualRoot, false);
            go.transform.localPosition = center; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // A sloped walkway from one point to another (both on its top surface).
        void Ramp(string name, Vector3 from, Vector3 to, float width)
        {
            const float thickness = .4f;
            var along = to - from;
            var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            var center = (from + to) * .5f - rotation * Vector3.up * (thickness * .5f);
            var size = new Vector3(width, thickness, along.magnitude + .3f);
            Collide(name, center, size, rotation);
            var go = Visual(name, PrimitiveType.Cube, center, size, wood);
            go.transform.localRotation = rotation;
            // Low side curbs, art only.
            for (int side = -1; side <= 1; side += 2)
            {
                var curb = Visual(name + " curb", PrimitiveType.Cube, center + rotation * new Vector3(side * (width * .5f - .08f), .25f, 0), new Vector3(.16f, .3f, along.magnitude), red);
                curb.transform.localRotation = rotation;
            }
        }

        // A climbable wall face: a thin Surface-layer board that players can grip (blocks pass through it).
        void ClimbFace(string name, Vector3 center, Vector3 size)
        {
            Collide(name, center, size, Quaternion.identity, Layers.Surface);
        }

        void Rail(float y, Vector2[] points, float height)
        {
            for (int i = 0; i + 1 < points.Length; i++) RailSegment(points[i], points[i + 1], y, height);
        }

        void RailSegment(Vector2 a, Vector2 b, float y, float height)
        {
            var along = b - a;
            if (along.magnitude < .05f) return;
            var center = new Vector3((a.x + b.x) * .5f, y + height * .5f, (a.y + b.y) * .5f);
            var rotation = Quaternion.LookRotation(new Vector3(along.x, 0, along.y), Vector3.up);
            var size = new Vector3(.4f, height, along.magnitude + .4f);
            Collide("Rail", center, size, rotation);
            var go = Visual("Rail", PrimitiveType.Cube, center, size, white);
            go.transform.localRotation = rotation;
            var cap = Visual("Rail cap", PrimitiveType.Cube, center + Vector3.up * (height * .5f + .04f), new Vector3(.5f, .08f, along.magnitude + .5f), red);
            cap.transform.localRotation = rotation;
        }

        void MakeMaterials()
        {
            navy = MakeColor("Navy hull", new Color(.09f, .16f, .42f));
            red = MakeColor("Cherry trim", new Color(.91f, .15f, .23f));
            cream = MakeColor("Cream deck", new Color(.98f, .91f, .76f));
            wood = MakeColor("Warm wood", new Color(.86f, .62f, .36f));
            white = MakeColor("Toy white", new Color(.97f, .97f, .95f));
            yellow = MakeColor("Lemon", new Color(1f, .82f, .12f));
            glass = MakeColor("Window blue", new Color(.25f, .62f, .9f));
            ice = MakeColor("Iceberg", new Color(.93f, .97f, 1f));
            iceShade = MakeColor("Iceberg shade", new Color(.62f, .84f, .97f));
        }

        Material MakeColor(string name, Color color)
        {
            var shader = hullSource ? hullSource.shader : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = hullSource ? new Material(hullSource) : new Material(shader);
            material.name = name;
            material.mainTexture = null;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .7f);
            hullMaterials.Add(material);
            return material;
        }

        void DestroyHullAssets()
        {
            foreach (var m in hullMaterials) if (m) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
            hullMaterials.Clear();
            if (deckMaterial) { if (Application.isPlaying) Destroy(deckMaterial); else DestroyImmediate(deckMaterial); }
            if (climb) foreach (var face in climb.Faces) if (face) Climbables.Unregister(face);
        }
    }
}

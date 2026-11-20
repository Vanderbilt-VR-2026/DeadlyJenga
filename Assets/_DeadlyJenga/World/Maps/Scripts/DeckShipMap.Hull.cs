using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The solid ship. Built from boxes at runtime (gray-box art in "Visual", collision in "Collision").
    // Ship coordinates: origin at the waterline in the middle, x = starboard, y = up, z = toward the bow.
    public sealed partial class DeckShipMap
    {
        public const float DeckHeight = 3f;     // main deck above the water
        public const float RaisedDeck = 4f;     // bow and stern decks above the main deck
        public const float HalfBeam = 10f;

        Rigidbody hullBody;
        Transform hull, collisionRoot, visualRoot;
        PhysicsMaterial deckMaterial;
        readonly List<Material> hullMaterials = new List<Material>();
        Material navy, red, cream, wood, white, yellow, glass, ice, iceShade;
        DeckShipHullClimb climb;

        public Transform Hull => hull;

        void BuildHull()
        {
            hull = new GameObject("Ship (solid hull)").transform;
            hull.SetParent(transform, false);
            hullBody = hull.gameObject.AddComponent<Rigidbody>();
            hullBody.isKinematic = true; hullBody.useGravity = false;
            hullBody.interpolation = RigidbodyInterpolation.Interpolate;
            hullBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            climb = hull.gameObject.AddComponent<DeckShipHullClimb>();
            climb.Body = hullBody;
            collisionRoot = new GameObject("Collision").transform; collisionRoot.SetParent(hull, false);
            visualRoot = new GameObject("Visual").transform; visualRoot.SetParent(hull, false);
            deckMaterial = new PhysicsMaterial("Ship deck")
            {
                staticFriction = deckFriction + .08f, dynamicFriction = deckFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            MakeMaterials();
            bodies.Insert(0, hullBody);

            float d = DeckHeight, top = DeckHeight + RaisedDeck;
            // Hull: a long midship box, a stepped bow and a squared stern. Their tops are the main deck.
            Box("Hull midship", new Vector3(0, .25f, -2), new Vector3(20, 5.5f, 56), navy);
            Box("Hull bow 1", new Vector3(0, .5f, 28), new Vector3(16, 5, 4), navy);
            Box("Hull bow 2", new Vector3(0, .75f, 31.5f), new Vector3(11, 4.5f, 3), navy);
            Box("Hull bow 3", new Vector3(0, 1, 34.25f), new Vector3(6, 4, 2.5f), navy);
            Box("Hull stern", new Vector3(0, .25f, -33), new Vector3(18, 5.5f, 6), navy);
            Paint("Red stripe midship", new Vector3(0, 1.9f, -2), new Vector3(20.08f, .7f, 56.04f), red);
            Paint("Red stripe stern", new Vector3(0, 1.9f, -33), new Vector3(18.08f, .7f, 6.04f), red);
            Paint("Red stripe bow", new Vector3(0, 1.9f, 28), new Vector3(16.08f, .7f, 4.04f), red);
            Paint("Main deck boards", new Vector3(0, d - .04f, -2), new Vector3(19.6f, .1f, 55.6f), cream);
            Paint("Waterline", new Vector3(0, -.05f, -2), new Vector3(20.1f, .3f, 56.1f), red);

            // Raised bow deck (team end A) with two ramps up from the main deck.
            Box("Bow deck 1", new Vector3(0, d + 2, 27), new Vector3(16, 4, 6), cream);
            Box("Bow deck 2", new Vector3(0, d + 2, 31.5f), new Vector3(11, 4, 3), cream);
            Box("Bow deck 3", new Vector3(0, d + 2, 34.25f), new Vector3(6, 4, 2.5f), cream);
            Paint("Bow stripe 1", new Vector3(0, d + 3.2f, 27), new Vector3(16.06f, .5f, 6.06f), red);
            Paint("Bow stripe 2", new Vector3(0, d + 3.2f, 31.5f), new Vector3(11.06f, .5f, 3.06f), red);
            Paint("Bow stripe 3", new Vector3(0, d + 3.2f, 34.25f), new Vector3(6.06f, .5f, 2.56f), red);
            Rail(top, new[] { new Vector2(-8, 24), new Vector2(-8, 30), new Vector2(-5.5f, 30), new Vector2(-5.5f, 33), new Vector2(-3, 33), new Vector2(-3, 35.5f),
                              new Vector2(3, 35.5f), new Vector2(3, 33), new Vector2(5.5f, 33), new Vector2(5.5f, 30), new Vector2(8, 30), new Vector2(8, 24) }, .8f);
            Ramp("Bow ramp port", new Vector3(-5.5f, d, 15), new Vector3(-5.5f, top, 24), 3.5f);
            Ramp("Bow ramp starboard", new Vector3(5.5f, d, 15), new Vector3(5.5f, top, 24), 3.5f);
            // Climb boards only between the ramps, so the ramp tops stay a smooth walk.
            ClimbFace("Bow wall climb", new Vector3(0, d + 2, 23.88f), new Vector3(7.3f, 4, .2f));

            // Raised stern deck (team end B) with the wheelhouse and funnel, and two ramps.
            Box("Stern deck", new Vector3(0, d + 2, -30), new Vector3(18, 4, 12), cream);
            Paint("Stern stripe", new Vector3(0, d + 3.2f, -30), new Vector3(18.06f, .5f, 12.06f), red);
            Rail(top, new[] { new Vector2(-9, -24), new Vector2(-9, -36), new Vector2(9, -36), new Vector2(9, -24) }, .8f);
            Ramp("Stern ramp port", new Vector3(-5.5f, d, -15), new Vector3(-5.5f, top, -24), 3.5f);
            Ramp("Stern ramp starboard", new Vector3(5.5f, d, -15), new Vector3(5.5f, top, -24), 3.5f);
            ClimbFace("Stern wall climb", new Vector3(0, d + 2, -23.88f), new Vector3(7.3f, 4, .2f));
            Box("Wheelhouse", new Vector3(0, top + 2, -32.5f), new Vector3(10, 4, 5), white);
            Paint("Wheelhouse windows", new Vector3(0, top + 2.6f, -32.5f), new Vector3(10.06f, 1.1f, 5.06f), glass);
            Paint("Wheelhouse roof", new Vector3(0, top + 4.05f, -32.5f), new Vector3(10.4f, .14f, 5.4f), red);
            ClimbFace("Wheelhouse climb", new Vector3(0, top + 2, -29.88f), new Vector3(10, 4, .2f));
            Cylinder("Funnel", new Vector3(0, top + 5.75f, -33.2f), new Vector3(2.6f, 1.75f, 2.6f), yellow, new Vector3(2.4f, 3.5f, 2.4f));
            Paint("Funnel band", new Vector3(0, top + 7.1f, -33.2f), new Vector3(2.7f, .25f, 2.7f), red, PrimitiveType.Cylinder);

            // Main deck rails, with gaps where cargo can slide overboard.
            float[] gaps = { -13, -10, 2, 5, 16, 19 };
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (HalfBeam - .2f);
                float from = -24;
                for (int g = 0; g <= gaps.Length; g += 2)
                {
                    float to = g < gaps.Length ? gaps[g] : 26;
                    RailSegment(new Vector2(x, from), new Vector2(x, to), d, 1.1f);
                    if (g + 1 < gaps.Length) from = gaps[g + 1];
                }
                RailSegment(new Vector2(x, 26), new Vector2(side * 8, 26), d, 1.1f);
            }

            // The fixed cargo hatch in the middle: a low wooden plateau the cargo builds on.
            Box("Cargo hatch", new Vector3(0, d + DeckShipCargo.HatchHeight * .5f, 0), new Vector3(DeckShipCargo.HatchHalf * 2, DeckShipCargo.HatchHeight, DeckShipCargo.HatchHalf * 2), wood);
            Paint("Hatch trim", new Vector3(0, d + DeckShipCargo.HatchHeight - .05f, 0), new Vector3(DeckShipCargo.HatchHalf * 2 + .1f, .12f, DeckShipCargo.HatchHalf * 2 + .1f), red);

            // Mast with a climbable ladder on its aft side and a crow's nest reached from the ladder's top.
            const float mastZ = 19, nest = 13.5f;
            // The pole stops under the nest so a climber can step off the ladder top onto it.
            float pole = nest - d;
            Cylinder("Mast", new Vector3(0, d + pole * .5f, mastZ), new Vector3(.8f, pole * .5f, .8f), white, new Vector3(.7f, pole, .7f));
            ClimbFace("Mast ladder", new Vector3(0, d + pole * .5f + .05f, mastZ - .5f), new Vector3(1.2f, pole, .2f));
            Paint("Mast ladder rungs", new Vector3(0, d + pole * .5f, mastZ - .48f), new Vector3(1.1f, pole, .1f), yellow);
            Box("Crow's nest", new Vector3(0, nest + .15f, mastZ + 1.3f), new Vector3(3, .3f, 2.6f), wood);
            Rail(nest + .3f, new[] { new Vector2(-1.5f, mastZ), new Vector2(-1.5f, mastZ + 2.6f), new Vector2(1.5f, mastZ + 2.6f), new Vector2(1.5f, mastZ) }, .7f);
            Paint("Top mast", new Vector3(0, nest + 1.8f, mastZ + 2.4f), new Vector3(.14f, 3.2f, .14f), white);
            Paint("Mast flag", new Vector3(.8f, nest + 2.9f, mastZ + 2.4f), new Vector3(1.6f, 1, .05f), red);
            Paint("Bow pennant", new Vector3(0, top + 2.2f, 34.6f), new Vector3(.12f, 4.4f, .12f), white);
            Paint("Bow pennant flag", new Vector3(.6f, top + 3.9f, 34.6f), new Vector3(1.2f, .8f, .05f), red);
            Paint("Stern pennant", new Vector3(-7.5f, top + 2.2f, -35.3f), new Vector3(.12f, 4.4f, .12f), white);
            Paint("Stern pennant flag", new Vector3(-6.9f, top + 3.9f, -35.3f), new Vector3(1.2f, .8f, .05f), MakeColor("Mint", new Color(.31f, .89f, .76f)));
            Portholes();

            BuildIceberg();
            hullBody.position = transform.position; hullBody.rotation = transform.rotation;
            Physics.SyncTransforms();
        }

        void Portholes()
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 9; i++)
                {
                    var p = new Vector3(side * (HalfBeam + .02f), .9f, -24 + i * 5.5f);
                    var go = Visual("Porthole", PrimitiveType.Cylinder, p, new Vector3(.9f, .04f, .9f), yellow);
                    go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
        }

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
            var box = Collide(name, center, size, Quaternion.identity, Layers.Surface);
            Climbables.Register(box, climb);
            climb.Faces.Add(box);
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

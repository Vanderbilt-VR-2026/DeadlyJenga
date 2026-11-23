using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The snowy night outside the tall window, built in 3-D so it shifts as you move:
    // drifts, snow-capped pines near and far, a few houses with lit windows, hills, a moon,
    // stars and falling snow. Everything out there glows faintly blue (moonlight) on its own,
    // so it needs no extra lamps. Pieces of a kind are combined to keep draw calls low.
    public sealed partial class UnderneathTreeFinish
    {
        const float GroundOutside = -12f;

        void PlaceWindowView(GameObject prefab, float middle, float wall, float yaw)
        {
            var anchor = Pivot(prefab.name + " (window view)", new Vector3(middle, WindowSill, wall), yaw);
            Instantiate(prefab, anchor, false);
        }

        // Built as if outside the north wall; the south window's view is the same kind of scene
        // turned round (see BuildRoom). west/east = the window's edges.
        void BuildOutdoorView(float west, float east, int seed, bool useDefaultScenery)
        {
            var rand = new System.Random(seed);
            var snow = Surface("Moonlit snow", "Paper", new Color(.80f, .86f, .96f), .35f, new Vector2(30, 30), 0f, .2f);
            snow.SetColor("_EmissionColor", new Color(.16f, .21f, .34f));
            snow.EnableKeyword("_EMISSION");
            float midX = (west + east) * .5f;
            // Window reveal: the wall's thickness, so the opening reads as a real hole.
            var casing = Surface("Cream window casing", "Paper", new Color(.79f, .68f, .54f), .48f, new Vector2(2, 2));
            foreach (float x in new[] { west - 3f, east + 3f })
                Box("Window reveal", new Vector3(x, (WindowSill + WindowHead) * .5f, RoomNorth + 7f), new Vector3(3f, WindowHead - WindowSill, 14f), casing);
            Box("Window reveal top", new Vector3(midX, WindowHead + 1.5f, RoomNorth + 7f), new Vector3(east - west + 6f, 3f, 14f), casing);
            Box("Window outer sill", new Vector3(midX, WindowSill - 2f, RoomNorth + 9f), new Vector3(east - west + 10f, 3f, 18f), casing);
            Box("Snow on the sill", new Vector3(midX, WindowSill + 1f, RoomNorth + 13f), new Vector3(east - west, 3f, 9f), snow);

            if (!useDefaultScenery)
                return;

            var pine = Surface("Night pine", null, new Color(.03f, .09f, .06f), .1f, Vector2.one);
            pine.SetColor("_EmissionColor", new Color(.008f, .025f, .03f));
            pine.EnableKeyword("_EMISSION");
            var bark = Surface("Night bark", null, new Color(.06f, .04f, .03f), .1f, Vector2.one);
            var siding = Surface("Night house siding", null, new Color(.22f, .10f, .08f), .2f, Vector2.one);
            siding.SetColor("_EmissionColor", new Color(.03f, .03f, .05f));
            siding.EnableKeyword("_EMISSION");
            var lit = Glow("Lit house window", new Color(1f, .68f, .32f), 2.6f);
            var sky = Glow("Night sky", new Color(.012f, .025f, .07f), 1f);
            var horizon = Glow("Night horizon glow", new Color(.05f, .08f, .18f), 1f);

            // Ground and soft drifts; low hills further off.
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Box("Snow ground", new Vector3(-200f, GroundOutside - 1f, 360f), new Vector3(2000f, 2f, 620f), snow);
            var drifts = new Batch();
            for (int i = 0; i < 26; i++)
            {
                var at = new Vector3(Range(rand, -800f, 400f), GroundOutside, Range(rand, 80f, 520f));
                float w = Range(rand, 50f, 140f);
                // Keep every drift clear of the house wall so none pokes up through the room's floor.
                at.z = Mathf.Max(at.z, RoomNorth + 8f + w * .5f);
                drifts.Add(sphere, at, Quaternion.Euler(0f, Range(rand, 0f, 180f), 0f), new Vector3(w, Range(rand, 10f, 28f), w * Range(rand, .5f, 1f)));
            }
            for (int i = 0; i < 6; i++)
            {
                var at = new Vector3(-900f + i * 300f + Range(rand, -50f, 50f), GroundOutside - 30f, Range(rand, 570f, 610f));
                float w = Range(rand, 380f, 560f);
                drifts.Add(sphere, at, Quaternion.identity, new Vector3(w, Range(rand, 120f, 220f), 120f));
            }
            Draw("Snow drifts and hills", drifts, snow, ShadowCastingMode.Off);

            // Pines: three stacked cones on a trunk, each with a snow cap. Near ones are few and big.
            var cone = ConeMesh();
            meshes.Add(cone);
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var needles = new Batch(); var caps = new Batch(); var trunks = new Batch();
            for (int i = 0; i < 70; i++)
            {
                float z = Mathf.Lerp(110f, 540f, Mathf.Pow((float)rand.NextDouble(), .7f));
                float x = Range(rand, -850f, 500f);
                // Keep a clear view of the houses straight out of the window.
                if (Mathf.Abs(x - midX) < 150f && z > 300f && z < 500f) continue;
                // Big near trees stand to the sides, framing the view instead of filling it.
                if (Mathf.Abs(x - midX) < 120f && z < 300f) continue;
                float height = Range(rand, 200f, 380f) * Mathf.Lerp(1.15f, .8f, (z - 110f) / 430f);
                var foot = new Vector3(x, GroundOutside, z);
                trunks.Add(cylinder, foot + Vector3.up * height * .08f, Quaternion.identity, new Vector3(height * .05f, height * .08f, height * .05f));
                for (int t = 0; t < 3; t++)
                {
                    float r = height * (.30f - t * .07f), h = height * (.42f - t * .06f);
                    var baseAt = foot + Vector3.up * height * (.12f + t * .26f);
                    var turn = Quaternion.Euler(0f, Range(rand, 0f, 360f), 0f);
                    needles.Add(cone, baseAt, turn, new Vector3(r, h, r));
                    caps.Add(cone, baseAt + Vector3.up * h * .38f, turn, new Vector3(r * .66f, h * .62f, r * .66f));
                }
            }
            Draw("Outdoor pines", needles, pine, ShadowCastingMode.Off);
            Draw("Snow on the pines", caps, snow, ShadowCastingMode.Off);
            Draw("Outdoor pine trunks", trunks, bark, ShadowCastingMode.Off);

            // A few houses out across the snow, windows lit for Christmas Eve.
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var walls = new Batch(); var roofs = new Batch(); var windows = new Batch();
            var houses = new[] { new Vector3(midX - 50f, 0f, 380f), new Vector3(midX + 160f, 0f, 460f), new Vector3(midX - 260f, 0f, 490f) };
            foreach (var spot in houses)
            {
                float w = Range(rand, 85f, 115f), d = 70f, h = Range(rand, 55f, 72f);
                var turn = Quaternion.Euler(0f, Range(rand, -15f, 15f), 0f);
                var centre = new Vector3(spot.x, GroundOutside + h * .5f, spot.z);
                walls.Add(cube, centre, turn, new Vector3(w, h, d));
                // Pitched roof: two slabs meeting at the ridge, thick with snow.
                foreach (int side in new[] { -1, 1 })
                {
                    var slab = turn * Quaternion.Euler(side * 38f, 0f, 0f);
                    roofs.Add(cube, centre + turn * new Vector3(0f, h * .5f + 18f, side * d * .25f), slab, new Vector3(w + 12f, 6f, d * .68f));
                }
                walls.Add(cube, centre + turn * new Vector3(w * .3f, h * .5f + 40f, 10f), turn, new Vector3(12f, 40f, 12f));
                for (int k = 0; k < 3; k++)
                    windows.Add(cube, centre + turn * new Vector3((k - 1) * w * .3f, Range(rand, -6f, 8f), -d * .5f - .6f), turn, new Vector3(16f, 20f, 1f));
            }
            Draw("Outdoor houses", walls, siding, ShadowCastingMode.Off);
            Draw("Snowy roofs", roofs, snow, ShadowCastingMode.Off);
            Draw("Lit house windows", windows, lit, ShadowCastingMode.Off);

            // Sky: a dark dome-like backdrop, a paler band near the horizon, stars and the moon.
            Box("Night sky backdrop", new Vector3(-200f, 450f, 640f), new Vector3(2400f, 1000f, 4f), sky);
            foreach (float x in new[] { -1400f, 1000f })
                Box("Night sky side", new Vector3(x, 450f, 340f), new Vector3(4f, 1000f, 600f), sky);
            Box("Night sky top", new Vector3(-200f, 950f, 340f), new Vector3(2400f, 4f, 600f), sky);
            Box("Horizon glow", new Vector3(-200f, 50f, 636f), new Vector3(2400f, 130f, 2f), horizon);
            var stars = new Batch();
            for (int i = 0; i < 260; i++)
            {
                var at = new Vector3(Range(rand, -1300f, 900f), Range(rand, 160f, 930f), 636f);
                stars.Add(sphere, at, Quaternion.identity, Vector3.one * Range(rand, 1.2f, 3.2f));
            }
            Draw("Stars", stars, Glow("Starlight", new Color(.85f, .9f, 1f), 3f), ShadowCastingMode.Off);
            Primitive("Moon", PrimitiveType.Sphere, new Vector3(midX - 100f, 470f, 620f), Vector3.one * 46f,
                Glow("Moon", new Color(1f, .97f, .86f), 2.4f)).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            BuildSnowfall(new Vector3(midX, 320f, 250f), new Vector3(800f, 10f, 320f));
        }

        // A plain cone, one unit high and one unit in radius, standing on its base.
        static Mesh ConeMesh()
        {
            const int sides = 12;
            var vertices = new List<Vector3> { Vector3.up };
            var faces = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % sides;
                faces.Add(0); faces.Add(b); faces.Add(a);
            }
            var mesh = new Mesh { name = "Pine cone shape" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        void BuildSnowfall(Vector3 at, Vector3 area)
        {
            var obj = new GameObject("Falling snow outside");
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = at;
            var snowfall = obj.AddComponent<ParticleSystem>();
            snowfall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = snowfall.main;
            main.startLifetime = 14f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.gravityModifier = .0f;
            main.maxParticles = 900;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Follow the parent's squash too (the south view is pressed shallower), so the snow stays outside.
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var velocity = snowfall.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
            velocity.y = new ParticleSystem.MinMaxCurve(-26f, -18f);
            velocity.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var emission = snowfall.emission;
            emission.rateOverTime = 60f;
            var shape = snowfall.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            var noise = snowfall.noise;
            noise.enabled = true;
            noise.strength = 4f;
            noise.frequency = .3f;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Glow("Snowflake", new Color(.85f, .9f, 1f), 1.4f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            snowfall.Play();
        }
    }
}

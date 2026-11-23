using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The snowy night outside the tall window, built in 3-D so it shifts as you move:
    // a snowy verge, street, houses, trees and a dark background.
    // Everything out there glows faintly on its own,
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
        void BuildOutdoorView(float west, float east, int seed, bool useDefaultScenery, bool south)
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

            float grade = south ? .145f : 0f;
            float Grade(float z) => GroundOutside + grade * (z - RoomNorth);
            const float roadZ = 495f, backgroundZ = 855f;
            var asphalt = Glow("Blue asphalt at night", new Color(.022f, .045f, .086f), 1f);
            var sky = Glow("Night sky", new Color(.012f, .025f, .07f), 1f);
            var horizon = Glow("Night horizon glow", new Color(.033f, .056f, .119f), 1f);
            var pine = Glow("Distant blue spruce", new Color(.024f, .058f, .10f), 1f);
            var lit = Glow("Warm curtains across the street", new Color(1f, .66f, .29f), 1.75f);
            var dark = Glow("Dark exterior trim", new Color(.027f, .039f, .07f), 1f);
            var cedar = new[]
            {
                Glow("Blue spruce siding", new Color(.058f, .104f, .151f), 1f),
                Glow("Cranberry siding", new Color(.128f, .058f, .072f), 1f),
                Glow("Weathered cream siding", new Color(.147f, .147f, .144f), 1f)
            };

            // The road and sidewalks sit on the same slope as the snow. A slight uphill grade
            // on the south view exposes the fronts of the houses above the high interior sill.
            var slope = Quaternion.Euler(-Mathf.Atan(grade) * Mathf.Rad2Deg, 0f, 0f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var ground = new Batch();
            const float snowStart = RoomNorth + 12f;
            float snowCenter = (snowStart + backgroundZ) * .5f;
            ground.Add(cube, new Vector3(midX, Grade(snowCenter) - 2f, snowCenter), slope,
                new Vector3(2100f, 4f, backgroundZ - snowStart));
            foreach (float offset in new[] { -52f, 52f })
            {
                float z = roadZ + offset;
                ground.Add(cube, new Vector3(midX, Grade(z) + 1f, z), slope, new Vector3(1450f, 3f, 11f));
                for (int i = -13; i <= 13; i++)
                    ground.Add(sphere, new Vector3(midX + i * 51f, Grade(z) + 1.5f, z - Mathf.Sign(offset) * 8f),
                        Quaternion.identity, new Vector3(54f, Range(rand, 4f, 10f), Range(rand, 10f, 16f)));
            }
            Draw("Snow-covered yards and sidewalks", ground, snow, ShadowCastingMode.Off);
            var roads = new Batch();
            roads.Add(cube, new Vector3(midX, Grade(roadZ) + .7f, roadZ), slope, new Vector3(1450f, 1f, 80f));
            Draw("Plowed residential street", roads, asphalt, ShadowCastingMode.Off);

            var walls = new[] { new Batch(), new Batch(), new Batch() };
            var roofs = new Batch();
            var frames = new Batch();
            var windows = new Batch();
            var bulbs = new[] { new Batch(), new Batch(), new Batch(), new Batch() };
            var colors = new[] { new Color(1f, .10f, .07f), new Color(1f, .65f, .24f),
                new Color(.15f, .58f, 1f), new Color(.23f, .85f, .37f) };
            for (int index = -3; index <= 3; index++)
            {
                float x = midX + index * 156f + (south ? 37f : -18f) + Range(rand, -13f, 13f);
                float z = roadZ + Range(rand, 148f, 205f);
                float w = Range(rand, 85f, 118f), d = Range(rand, 55f, 68f);
                float h = index % 3 == 0 ? 61f : Range(rand, 73f, 96f);
                float rise = Range(rand, 20f, 33f);
                var front = new Vector3(x, Grade(z) - 2f, z);
                void Part(Batch target, Vector3 offset, Vector3 size, Quaternion rotation)
                    => target.Add(cube, front + offset, rotation, size);

                var siding = walls[(index + 3) % walls.Length];
                Part(siding, new Vector3(0f, h * .5f, d * .5f), new Vector3(w, h, d), Quaternion.identity);
                Part(siding, new Vector3(w * .26f, h + 13f, d * .74f), new Vector3(10f, 46f, 12f), Quaternion.identity);
                Part(frames, new Vector3(0f, 12f, -.8f), new Vector3(14f, 24f, 2f), Quaternion.identity);
                Part(roofs, new Vector3(0f, 1f, -12f), new Vector3(29f, 3f, 25f), Quaternion.identity);

                float run = w * .5f + 4f;
                float onRoof = Mathf.Sqrt(run * run + rise * rise);
                float pitch = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
                var roofMesh = new Mesh { name = "Neighborhood gable" };
                roofMesh.vertices = new[] { new Vector3(-run, 0f, 0f), new Vector3(run, 0f, 0f), new Vector3(0f, rise, 0f),
                    new Vector3(-run, 0f, d), new Vector3(run, 0f, d), new Vector3(0f, rise, d) };
                roofMesh.triangles = new[] { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4 };
                roofMesh.RecalculateNormals();
                meshes.Add(roofMesh);
                siding.Add(roofMesh, front + Vector3.up * h, Quaternion.identity, Vector3.one);
                foreach (int side in new[] { -1, 1 })
                {
                    Part(roofs, new Vector3(side * run * .5f, h + rise * .5f + 2f, d * .5f),
                        new Vector3(onRoof + 1f, 4f, d + 10f), Quaternion.Euler(0f, 0f, -side * pitch));
                    for (int row = 0; row < (h > 68f ? 2 : 1); row++)
                    {
                        var at = new Vector3(side * w * .27f, 27f + row * 34f, -1.2f);
                        Part(windows, at, new Vector3(17f, 21f, 1.4f), Quaternion.identity);
                        Part(frames, at + new Vector3(0f, 0f, -1f), new Vector3(1.2f, 21f, .6f), Quaternion.identity);
                        Part(frames, at + new Vector3(0f, 0f, -1f), new Vector3(17f, 1.1f, .6f), Quaternion.identity);
                    }
                }
                for (int bulb = 0; bulb < 17; bulb++)
                {
                    float edge = Mathf.Lerp(-run, run, bulb / 16f);
                    bulbs[(bulb + index + 4) % colors.Length].Add(cube,
                        front + new Vector3(edge, h + rise * (1f - Mathf.Abs(edge) / run) - .6f, -4.4f),
                        Quaternion.identity, Vector3.one * 2.15f);
                }
            }
            for (int i = 0; i < walls.Length; i++)
                Draw("Houses across the street", walls[i], cedar[i], ShadowCastingMode.Off);
            Draw("Snowy roofs and porch steps", roofs, snow, ShadowCastingMode.Off);
            Draw("House doors and window sashes", frames, dark, ShadowCastingMode.Off);
            Draw("Lit house windows", windows, lit, ShadowCastingMode.Off);
            for (int i = 0; i < bulbs.Length; i++)
                Draw("Christmas lights on distant eaves", bulbs[i], Glow("Distant eave lights " + i, colors[i], 1.8f), ShadowCastingMode.Off);

            var cone = ConeMesh();
            meshes.Add(cone);
            var trees = new Batch();
            for (int i = -12; i <= 12; i++)
            {
                float z = Range(rand, 755f, 825f);
                var at = new Vector3(midX + i * 55f + Range(rand, -20f, 20f), Grade(z), z);
                float height = Range(rand, 92f, 158f);
                trees.Add(cone, at, Quaternion.identity, new Vector3(height * .24f, height, height * .24f));
            }
            Draw("Tree line behind the houses", trees, pine, ShadowCastingMode.Off);
            Box("Night sky backdrop", new Vector3(midX, 445f, backgroundZ), new Vector3(2400f, 1060f, 4f), sky);
            Box("Distant winter haze", new Vector3(midX, 105f, backgroundZ - 4f), new Vector3(2390f, 180f, 2f), horizon);

            var stars = new Batch();
            for (int i = 0; i < 90; i++)
            {
                var at = new Vector3(midX + Range(rand, -1100f, 1100f), Range(rand, 198f, 930f), backgroundZ - 5f);
                stars.Add(cube, at, Quaternion.identity, Vector3.one * Range(rand, 1f, 2.35f));
            }
            Draw("Distant stars", stars, Glow("Soft starlight", new Color(.66f, .77f, 1f), 1.65f), ShadowCastingMode.Off);
            BuildSnowfall(new Vector3(midX, 240f, 265f), new Vector3(720f, 10f, 295f));
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

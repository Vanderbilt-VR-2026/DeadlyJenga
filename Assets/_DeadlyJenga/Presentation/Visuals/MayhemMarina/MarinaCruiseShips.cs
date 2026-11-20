using System;
using System.Collections.Generic;
using UnityEngine;
using F = DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // Three storybook resort cruise ships sailing past the Marina.
    // Each crossing picks a new lane and direction across the open southern sea, far from the island.
    // Pure scenery: no colliders, no gameplay. They hide whenever the Marina ocean is hidden.
    public sealed class MarinaCruiseShips : MonoBehaviour
    {
        enum Design { Resort, Waterpark, Regal }

        sealed class Ship
        {
            public Transform root;
            public Vector3 laneStart, laneEnd;
            public float speed, travelled, length, bobSeed, wait;
        }

        // Finish slot -> paint, per design. Unlisted finishes keep the shared Marina material.
        static readonly Dictionary<Design, Dictionary<F, Color>> Liveries = new()
        {
            [Design.Resort] = new()
            {
                { F.Purple, new Color(.05f, .09f, .22f) },   // navy hull
                { F.Coral, new Color(.78f, .06f, .08f) },    // red boot stripe
                { F.Ivory, new Color(.97f, .97f, .95f) },    // white decks
                { F.Metal, new Color(.07f, .11f, .19f) },    // window bands
                { F.Green, new Color(1f, .78f, .12f) },      // lifeboats
                { F.Red, new Color(.84f, .07f, .08f) },      // funnels
                { F.DarkWood, new Color(.04f, .04f, .05f) }, // funnel caps
                { F.Glass, new Color(.35f, .75f, .95f) },    // pool water
            },
            [Design.Waterpark] = new()
            {
                { F.Purple, new Color(.02f, .45f, .62f) },   // teal hull
                { F.Coral, new Color(1f, .82f, .1f) },       // yellow stripe
                { F.Ivory, new Color(.98f, .98f, .97f) },
                { F.Metal, new Color(.06f, .2f, .3f) },
                { F.Green, new Color(1f, .45f, .1f) },       // orange lifeboats
                { F.Red, new Color(1f, .25f, .55f) },        // pink funnel
                { F.DarkWood, new Color(.1f, .1f, .12f) },
                { F.Glass, new Color(.3f, .85f, 1f) },
            },
            [Design.Regal] = new()
            {
                { F.Purple, new Color(.02f, .02f, .05f) },   // gloss black hull
                { F.Coral, new Color(.55f, .03f, .06f) },    // deep crimson stripe
                { F.Ivory, new Color(.95f, .93f, .88f) },    // cream decks
                { F.Metal, new Color(.05f, .06f, .1f) },
                { F.Green, new Color(.95f, .93f, .88f) },    // cream tenders
                { F.Red, new Color(.55f, .03f, .06f) },      // crimson funnels
                { F.DarkWood, new Color(.02f, .02f, .03f) },
                { F.Glass, new Color(.25f, .55f, .8f) },
            },
        };

        // Raise the model so more hull shows; the boot stripe then sits at the waterline.
        const float FreeboardLift = 1.5f;
        // Lanes stay this far from the island centre and only cross the southern half of the sea.
        const float LaneClearance = 230, LaneRadius = 640;

        readonly List<Ship> ships = new();
        readonly List<MarinaMesh> meshes = new();
        readonly List<Material> liveryMaterials = new();
        readonly System.Random random = new(4117);
        Transform ocean;
        float waterY;

        public void Build(Transform oceanSurface, float oceanLevel)
        {
            ocean = oceanSurface;
            waterY = oceanLevel;
            AddShip("Cruise ship · Coral Dream", Design.Resort, 150, 3.8f, .3f);
            AddShip("Cruise ship · Splash Coaster", Design.Waterpark, 130, 4.4f, .65f);
            AddShip("Cruise ship · Royal Sovereign", Design.Regal, 185, 3.0f, .1f);
        }

        void AddShip(string name, Design design, float length, float speed, float startFraction)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var mesh = new MarinaMesh();
            float s = length / 100f;
            BuildHull(mesh, s, design);
            if (design == Design.Resort) BuildResort(mesh, s);
            else if (design == Design.Waterpark) BuildWaterpark(mesh, s);
            else BuildRegal(mesh, s);
            mesh.Flush(root, waterY);
            meshes.Add(mesh);
            var livery = Liveries[design];
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                string finishName = renderer.name.Substring(renderer.name.LastIndexOf(' ') + 1);
                if (Enum.TryParse(finishName, out F finish) && livery.TryGetValue(finish, out var color))
                {
                    var paint = new Material(renderer.sharedMaterial) { name = name + " livery " + finish };
                    paint.SetTexture("_BaseMap", null);
                    paint.SetColor("_BaseColor", color);
                    paint.SetFloat("_Smoothness", finish == F.Metal || finish == F.Glass || finish == F.Purple ? .9f : .62f);
                    renderer.sharedMaterial = paint;
                    liveryMaterials.Add(paint);
                }
            }
            var ship = new Ship { root = root, speed = speed, length = length, bobSeed = length };
            PickLane(ship);
            ship.travelled = Vector3.Distance(ship.laneStart, ship.laneEnd) * startFraction;
            ships.Add(ship);
        }

        // A straight chord across a big circle, always south of the island and never closer than LaneClearance.
        void PickLane(Ship ship)
        {
            float side = Mathf.Lerp(-70, 70, (float)random.NextDouble()) * Mathf.Deg2Rad;
            var normal = new Vector3(Mathf.Sin(side), 0, -Mathf.Cos(side));
            float offset = Mathf.Lerp(LaneClearance, LaneRadius - 120, (float)random.NextDouble());
            float half = Mathf.Sqrt(LaneRadius * LaneRadius - offset * offset);
            var along = new Vector3(-normal.z, 0, normal.x) * (random.Next(2) == 0 ? 1 : -1);
            // Keep ships apart: nudge this lane outward if another ship already uses a similar offset.
            foreach (var other in ships)
                if (other != ship && Mathf.Abs(Vector3.Dot(other.laneStart, normal) - offset) < 40) offset = Mathf.Min(LaneRadius - 60, offset + 60);
            ship.laneStart = normal * offset - along * half;
            ship.laneEnd = normal * offset + along * half;
            ship.travelled = 0;
        }

        // Shared hull: rounded bow and stern, boot stripe, trim, stacked decks with windows and balconies.
        static void BuildHull(MarinaMesh m, float s, Design design)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * s;
            m.Box(P(0, -1, -2), P(13, 9, 84), F.Purple, 2.2f * s);
            m.Ellipsoid(P(0, -1, 38), P(6.5f, 4.5f, 14), F.Purple, rings: 8, sides: 14);
            m.Ellipsoid(P(0, -.5f, -44), P(6.5f, 4, 4), F.Purple, rings: 6, sides: 12);
            m.Box(P(0, -FreeboardLift, -2), P(13.25f, .8f, 84), F.Coral, .2f * s);
            m.Box(P(0, 3.4f, -2), P(13.3f, .35f, 82), F.Brass, .1f * s);
            // Hull portholes.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 18; i++)
                    m.Ellipsoid(P(side * 6.55f, 1.2f, -36 + i * 4.2f), P(.12f, .45f, .45f), F.Metal, rings: 4, sides: 6);

            (float y, float h, float length, float width, float z)[] decks =
            {
                (3.6f, 4.0f, 82, 12.4f, -3), (7.6f, 3.4f, 72, 11.6f, -6), (11.0f, 3.0f, 56, 10.8f, -8), (14.0f, 2.4f, 30, 9.6f, 4)
            };
            foreach (var d in decks)
            {
                m.Box(P(0, d.y + d.h * .5f, d.z), P(d.width, d.h, d.length), F.Ivory, .5f * s);
                for (int side = -1; side <= 1; side += 2)
                {
                    m.Box(P(side * (d.width * .5f + .02f), d.y + d.h * .55f, d.z), P(.12f, d.h * .35f, d.length - 3), F.Metal, .02f * s);
                    // Balcony dividers and a thin rail give the decks scale.
                    m.Box(P(side * (d.width * .5f + .25f), d.y + .55f, d.z), P(.08f, .08f, d.length - 2), F.Brass, .01f * s);
                    if (design != Design.Waterpark)
                        for (float z = d.z - d.length * .5f + 3; z < d.z + d.length * .5f - 2; z += 3.2f)
                            m.Box(P(side * (d.width * .5f + .2f), d.y + d.h * .5f, z), P(.4f, d.h * .9f, .1f), F.Ivory, .01f * s);
                }
            }
            m.Ellipsoid(P(0, 15.2f, 19), P(4.8f, 1.2f, 2.2f), F.Ivory, rings: 6, sides: 12);
            m.Box(P(0, 15.3f, 18.9f), P(9.8f, .7f, .3f), F.Metal, .05f * s);
            // Bridge wings.
            m.Box(P(0, 14.9f, 16.5f), P(15f, .5f, 2.4f), F.Ivory, .2f * s);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                    m.Ellipsoid(P(side * 6.6f, 6.4f, -26 + i * 8.5f), P(.9f, .75f, 2.8f), F.Green, rings: 5, sides: 8);
            m.Beam(P(0, 16.4f, 10), P(0, 24.5f, 10), .25f * s, F.Ivory);
            m.Beam(P(-2.5f, 22.5f, 10), P(2.5f, 22.5f, 10), .12f * s, F.Ivory);
        }

        static void Funnel(MarinaMesh m, float s, float z, float scale)
        {
            Vector3 P(float x, float y, float zz) => new Vector3(x, y, zz) * s;
            m.Ellipsoid(P(0, 17.5f * scale + (1 - scale) * 14, z), P(2.6f, 4.6f, 3.6f) * scale, F.Red, rings: 8, sides: 14);
            m.Ellipsoid(P(0, 21.6f * scale + (1 - scale) * 14, z), P(2.2f, .9f, 3.1f) * scale, F.DarkWood, rings: 6, sides: 12);
        }

        // Family resort liner: two funnels, sun deck pool with a twisting slide, big screen and lido canopy.
        static void BuildResort(MarinaMesh m, float s)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * s;
            Funnel(m, s, -16, 1); Funnel(m, s, -29, 1);
            m.Box(P(0, 14.15f, -4), P(7f, .3f, 9f), F.Glass, .3f * s);
            m.Box(P(0, 14.05f, -4), P(8f, .25f, 10f), F.Teal, .3f * s);
            m.CurvedRoof(P(0, 16.8f, 8), 8 * s, 6 * s, 1.2f * s, F.CoralFabric, true);
            m.Box(P(0, 17.6f, -9.5f), P(5.5f, 3.2f, .3f), F.Metal, .1f * s);
            var slide = new List<Vector3>();
            for (int i = 0; i <= 28; i++)
            {
                float t = i / 28f, a = t * Mathf.PI * 3.2f;
                slide.Add(P(Mathf.Cos(a) * 3.2f, 20.5f - t * 6.2f, -38 + Mathf.Sin(a) * 2.6f + t * 4));
            }
            m.Tube(slide, .55f * s, F.CandyMint, 8);
            // Stern lounge windows.
            m.Box(P(0, 8f, -40.6f), P(10f, 5f, .2f), F.Metal, .05f * s);
        }

        // Waterpark liner: a looping rollercoaster on supports, rainbow slides and a splash tower.
        static void BuildWaterpark(MarinaMesh m, float s)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * s;
            Funnel(m, s, -22, 1.15f);
            var coaster = new List<Vector3>();
            for (int i = 0; i <= 80; i++)
            {
                float t = i / 80f, a = t * Mathf.PI * 2;
                float x = Mathf.Sin(a) * 5.6f, z = -6 + Mathf.Cos(a) * 30f;
                float y = 17.5f + Mathf.Sin(a * 3) * 2.2f + (t > .4f && t < .6f ? Mathf.Sin((t - .4f) / .2f * Mathf.PI) * 5f : 0);
                coaster.Add(P(x, y, z));
            }
            m.Tube(coaster, .3f * s, F.CandyCoral, 8);
            var rail = new List<Vector3>();
            foreach (var point in coaster) rail.Add(point + Vector3.up * .55f * s);
            m.Tube(rail, .14f * s, F.Brass, 6);
            for (int i = 0; i < 80; i += 5)
                m.Beam(new Vector3(coaster[i].x, 14.2f * s, coaster[i].z), coaster[i], .16f * s, F.Ivory);
            F[] rainbow = { F.CandyCoral, F.CandyMint, F.CandyLilac, F.Coral };
            for (int k = 0; k < 4; k++)
            {
                var slide = new List<Vector3>();
                for (int i = 0; i <= 24; i++)
                {
                    float t = i / 24f, a = t * Mathf.PI * (2.2f + k * .4f);
                    slide.Add(P(Mathf.Cos(a + k) * (2 + k * .6f), 24f - t * 10f, -34 + k * 1.5f + Mathf.Sin(a + k) * 2));
                }
                m.Tube(slide, .5f * s, rainbow[k], 8);
            }
            m.Box(P(0, 19f, -34), P(3f, 10f, 3f), F.Ivory, .3f * s);
            m.Ellipsoid(P(0, 25f, -34), P(3.2f, 1.6f, 3.2f), F.Coral, rings: 6, sides: 12);
            m.Box(P(0, 14.15f, 10), P(7f, .3f, 7f), F.Glass, .3f * s);
            m.Disk(P(0, 14.3f, 24), 2.2f * s, .4f * s, F.CandyLilac, false, 24);
        }

        // Regal luxury liner: three raked funnels, gold trim, crown observation lounge, long promenade.
        static void BuildRegal(MarinaMesh m, float s)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * s;
            Funnel(m, s, -10, .9f); Funnel(m, s, -21, .9f); Funnel(m, s, -32, .9f);
            for (float y = 7.4f; y < 15; y += 3.6f)
                m.Box(P(0, y, -5), P(12.6f, .18f, 76), F.Brass, .03f * s);
            m.Ellipsoid(P(0, 17.6f, 15), P(4.2f, 2.2f, 3.6f), F.Glass, rings: 6, sides: 14);
            m.Disk(P(0, 19.7f, 15), 1.6f * s, .5f * s, F.Brass, false, 16);
            m.Box(P(0, 14.15f, -2), P(6f, .25f, 12f), F.Glass, .2f * s);
            m.Beam(P(0, 2f, 46), P(0, 7f, 52), .16f * s, F.Brass);
            m.Ellipsoid(P(0, 5.6f, 50), P(.9f, .9f, .9f), F.Brass, rings: 5, sides: 8);
            for (int side = -1; side <= 1; side += 2)
                m.Box(P(side * 6.6f, 4.2f, 30), P(.1f, 1.2f, 10f), F.Brass, .02f * s);
        }

        void Update()
        {
            bool visible = !ocean || ocean.gameObject.activeInHierarchy;
            foreach (var ship in ships)
            {
                if (ship.root.gameObject.activeSelf != visible) ship.root.gameObject.SetActive(visible);
                if (!visible) continue;
                float lane = Vector3.Distance(ship.laneStart, ship.laneEnd);
                ship.travelled += ship.speed * Time.deltaTime;
                if (ship.travelled >= lane) { PickLane(ship); lane = Vector3.Distance(ship.laneStart, ship.laneEnd); }
                var direction = (ship.laneEnd - ship.laneStart) / lane;
                var position = ship.laneStart + direction * ship.travelled;
                float t = Time.time * .35f + ship.bobSeed;
                position.y = waterY + FreeboardLift * ship.length / 100f + Mathf.Sin(t) * .12f;
                ship.root.localPosition = position;
                ship.root.localRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(Mathf.Sin(t * .8f) * .25f, 0, Mathf.Sin(t) * .35f);
            }
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) mesh.Dispose();
            foreach (var material in liveryMaterials) if (material) Destroy(material);
        }
    }
}

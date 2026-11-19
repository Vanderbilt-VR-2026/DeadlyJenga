using System;
using System.Collections.Generic;
using UnityEngine;
using F = DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // Big storybook cruise ships gliding past the Marina at medium and far distance.
    // Pure scenery: no colliders, no gameplay. They hide whenever the Marina ocean is hidden.
    public sealed class MarinaCruiseShips : MonoBehaviour
    {
        sealed class Ship
        {
            public Transform root;
            public Vector3 laneStart, laneEnd;
            public float speed, travelled, length, bobSeed;
        }

        // Navy hull, red boot stripe, white decks, gold trim, red funnels, yellow lifeboats.
        static readonly Dictionary<F, Color> Livery = new()
        {
            { F.Purple, new Color(.05f, .09f, .22f) },    // hull
            { F.Coral, new Color(.78f, .06f, .08f) },     // boot stripe
            { F.Ivory, new Color(.97f, .97f, .95f) },     // superstructure
            { F.Metal, new Color(.07f, .11f, .19f) },     // window bands
            { F.Green, new Color(1f, .78f, .12f) },       // lifeboats
            { F.Red, new Color(.84f, .07f, .08f) },       // funnels
            { F.DarkWood, new Color(.04f, .04f, .05f) },  // funnel caps
        };

        // Raise the model so more navy hull shows; the red boot stripe then sits at the waterline.
        const float FreeboardLift = 1.5f;

        readonly List<Ship> ships = new();
        readonly List<MarinaMesh> meshes = new();
        readonly List<Material> liveryMaterials = new();
        Transform ocean;
        float waterY;

        public void Build(Transform oceanSurface, float oceanLevel)
        {
            ocean = oceanSurface;
            waterY = oceanLevel;
            // Medium ship heading east, larger far ship heading west, both across the open southern sea.
            AddShip("Cruise ship · Coral Dream", 120, new Vector3(-420, 0, -175), new Vector3(420, 0, -175), 4.2f, .35f);
            AddShip("Cruise ship · Candy Wonder", 175, new Vector3(560, 0, -335), new Vector3(-560, 0, -335), 3.1f, .7f);
        }

        void AddShip(string name, float length, Vector3 from, Vector3 to, float speed, float startFraction)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var mesh = new MarinaMesh();
            BuildHull(mesh, length / 100f);
            mesh.Flush(root, waterY);
            meshes.Add(mesh);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                string finishName = renderer.name.Substring(renderer.name.LastIndexOf(' ') + 1);
                if (Enum.TryParse(finishName, out F finish) && Livery.TryGetValue(finish, out var color))
                {
                    var paint = new Material(renderer.sharedMaterial) { name = "Cruise livery " + finish };
                    paint.SetTexture("_BaseMap", null);
                    paint.SetColor("_BaseColor", color);
                    paint.SetFloat("_Smoothness", finish == F.Metal ? .9f : .6f);
                    renderer.sharedMaterial = paint;
                    liveryMaterials.Add(paint);
                }
            }
            float lane = Vector3.Distance(from, to);
            ships.Add(new Ship { root = root, laneStart = from, laneEnd = to, speed = speed, length = length, travelled = lane * startFraction, bobSeed = length });
        }

        // A stylized liner in a 100-unit frame, scaled by s. +Z is the bow; y = 0 is the waterline.
        static void BuildHull(MarinaMesh m, float s)
        {
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * s;
            Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * s;

            // Hull with a rounded bow and stern.
            m.Box(P(0, -1, -2), S(13, 9, 84), F.Purple, 2.2f * s);
            m.Ellipsoid(P(0, -1, 38), S(6.5f, 4.5f, 14), F.Purple, rings: 8, sides: 14);
            m.Ellipsoid(P(0, -.5f, -44), S(6.5f, 4, 4), F.Purple, rings: 6, sides: 12);
            m.Box(P(0, -FreeboardLift, -2), S(13.25f, .8f, 84), F.Coral, .2f * s);
            m.Box(P(0, 3.4f, -2), S(13.3f, .35f, 82), F.Brass, .1f * s);

            // Stacked white decks, each a little shorter, with dark window bands on both sides.
            (float y, float h, float length, float width, float z)[] decks =
            {
                (3.6f, 4.0f, 82, 12.4f, -3), (7.6f, 3.4f, 72, 11.6f, -6), (11.0f, 3.0f, 56, 10.8f, -8), (14.0f, 2.4f, 30, 9.6f, 4)
            };
            foreach (var d in decks)
            {
                m.Box(P(0, d.y + d.h * .5f, d.z), S(d.width, d.h, d.length), F.Ivory, .5f * s);
                for (int side = -1; side <= 1; side += 2)
                    m.Box(P(side * (d.width * .5f + .02f), d.y + d.h * .55f, d.z), S(.12f, d.h * .35f, d.length - 3), F.Metal, .02f * s);
            }
            // Rounded bridge front and a bright wraparound wheelhouse window.
            m.Ellipsoid(P(0, 15.2f, 19), S(4.8f, 1.2f, 2.2f), F.Ivory, rings: 6, sides: 12);
            m.Box(P(0, 15.3f, 18.9f), S(9.8f, .7f, .3f), F.Metal, .05f * s);

            // Yellow lifeboats along the promenade deck.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                    m.Ellipsoid(P(side * 6.6f, 6.4f, -26 + i * 8.5f), S(.9f, .75f, 2.8f), F.Green, rings: 5, sides: 8);

            // Two classic red funnels with black caps.
            foreach (float z in new[] { -16f, -29f })
            {
                m.Ellipsoid(P(0, 17.5f, z), S(2.6f, 4.6f, 3.6f), F.Red, rings: 8, sides: 14);
                m.Ellipsoid(P(0, 21.6f, z), S(2.2f, .9f, 3.1f), F.DarkWood, rings: 6, sides: 12);
            }

            // A twisting water slide on the top deck, plus a mast.
            var slide = new List<Vector3>();
            for (int i = 0; i <= 28; i++)
            {
                float t = i / 28f, a = t * Mathf.PI * 3.2f;
                slide.Add(P(Mathf.Cos(a) * 3.2f, 20.5f - t * 6.2f, -38 + Mathf.Sin(a) * 2.6f + t * 4));
            }
            m.Tube(slide, .55f * s, F.CandyMint, 8);
            m.Beam(P(0, 16.4f, 10), P(0, 23.5f, 10), .25f * s, F.Ivory);
        }

        void Update()
        {
            bool visible = !ocean || ocean.gameObject.activeInHierarchy;
            foreach (var ship in ships)
            {
                if (ship.root.gameObject.activeSelf != visible) ship.root.gameObject.SetActive(visible);
                if (!visible) continue;
                float lane = Vector3.Distance(ship.laneStart, ship.laneEnd);
                ship.travelled = (ship.travelled + ship.speed * Time.deltaTime) % lane;
                var direction = (ship.laneEnd - ship.laneStart) / lane;
                var position = ship.laneStart + direction * ship.travelled;
                // A slow, gentle sway so the ships feel afloat without drawing attention.
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Ambient sea life around an island: seagulls circling wide around the map, pods of huge dolphins
    // leaping, and a giant humpback whale that now and then breaches and belly-flops in a huge splash.
    // Visual only: no colliders, no gameplay. Local y = 0 is the waterline.
    public sealed class CandySeaLife : MonoBehaviour
    {
        sealed class Gull { public Transform root, leftArm, rightArm, leftHand, rightHand; public float radius, height, angle, speed, phase; }

        const float WhaleLength = 34f, DolphinLength = 7f;

        readonly List<Gull> gulls = new();
        readonly HashSet<Transform> gullRoots = new();
        readonly List<Material> materials = new();
        readonly List<Mesh> meshes = new();
        Transform[] dolphins;
        HumpbackWhale whale;
        SeaLifeSplash splash;
        float near, far, nextDolphins, nextWhale, dolphinStart = -1, whaleStart = -1, lastWhaleT;
        Vector3 dolphinFrom, dolphinDir, whalePoint;
        Quaternion whaleYaw;
        bool southOnly;
        Transform hideWith;
        bool shown = true;

        // near: gulls circle out beyond this ring; far: dolphins and the whale play beyond it.
        // southOnly keeps swimmers in the southern half (for maps with land to the north).
        // hideWith: optional object (such as the ocean surface); sea life hides while it is inactive.
        public void Build(float nearRadius, float farRadius, bool keepSouth = false, Transform hideWhenInactive = null)
        {
            near = nearRadius; far = farRadius; southOnly = keepSouth; hideWith = hideWhenInactive;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            splash = new SeaLifeSplash(transform);

            var gullBody = Keep(SeaLifeShapes.GullBody());
            var gullArm = Keep(SeaLifeShapes.GullWing(true));
            var gullHand = Keep(SeaLifeShapes.GullWing(false));
            var gullPaint = new[]
            {
                Paint(lit, new Color(.97f, .97f, .96f), .55f), Paint(lit, new Color(.62f, .67f, .74f), .5f),
                Paint(lit, new Color(.08f, .08f, .10f), .5f), Paint(lit, new Color(1f, .78f, .15f), .65f), Paint(lit, new Color(.03f, .03f, .05f), .9f),
            };
            for (int i = 0; i < 8; i++) gulls.Add(MakeGull(i, gullBody, gullArm, gullHand, gullPaint));

            var dolphinMesh = Keep(SeaLifeShapes.Dolphin(DolphinLength));
            var dolphinPaint = new[] { Paint(lit, new Color(.40f, .47f, .55f), .85f), Paint(lit, new Color(.86f, .89f, .92f), .8f), Paint(lit, new Color(.03f, .03f, .05f), .9f) };
            dolphins = new Transform[3];
            for (int i = 0; i < dolphins.Length; i++) dolphins[i] = Shape("Dolphin", transform, dolphinMesh, dolphinPaint, false);

            whale = HumpbackWhale.Create(transform, WhaleLength);

            nextDolphins = Time.time + Random.Range(1f, 4f);
            nextWhale = Time.time + Random.Range(10f, 20f);
        }

        Mesh Keep(Mesh mesh) { meshes.Add(mesh); return mesh; }

        Material Paint(Shader shader, Color color, float smoothness)
        {
            var m = new Material(shader) { name = "Sea life", enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            materials.Add(m);
            return m;
        }

        Transform Shape(string name, Transform parent, Mesh mesh, Material[] paint, bool active, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = paint;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(active);
            return go.transform;
        }

        Gull MakeGull(int index, Mesh body, Mesh arm, Mesh hand, Material[] paint)
        {
            var root = Shape("Seagull " + (index + 1), transform, body, paint, true, false);
            root.localScale = Vector3.one * 1.25f;
            Transform Segment(Transform parent, Mesh mesh, Vector3 at, float side, string name)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = at;
                var part = Shape(name + " feathers", pivot, mesh, paint, true, false);
                part.localScale = new Vector3(side, 1, 1);
                return pivot;
            }
            var leftArm = Segment(root, arm, new Vector3(-.07f, .045f, .07f), -1, "Left wing");
            var rightArm = Segment(root, arm, new Vector3(.07f, .045f, .07f), 1, "Right wing");
            float outer = Mathf.Min(near * 1.9f, far * .92f);
            gullRoots.Add(root);
            return new Gull
            {
                root = root, leftArm = leftArm, rightArm = rightArm,
                leftHand = Segment(leftArm, hand, new Vector3(-.55f, 0, -.02f), -1, "Left wingtip"),
                rightHand = Segment(rightArm, hand, new Vector3(.55f, 0, -.02f), 1, "Right wingtip"),
                radius = Random.Range(Mathf.Min(near * 1.15f, outer), outer), height = Random.Range(16f, 32f),
                angle = Random.Range(0, Mathf.PI * 2), speed = Random.Range(7f, 11f) * (index % 2 == 0 ? 1 : -1),
                phase = Random.Range(0, 10f)
            };
        }

        Vector3 SwimPoint(float radius)
        {
            float angle = southOnly ? Random.Range(200f, 340f) * Mathf.Deg2Rad : Random.Range(0, Mathf.PI * 2);
            return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        // Swim paths are in this object's space; the splash particles live in world space.
        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        void Update()
        {
            bool visible = !hideWith || hideWith.gameObject.activeInHierarchy && (!hideWith.TryGetComponent<Renderer>(out var water) || water.enabled);
            if (visible != shown)
            {
                shown = visible;
                splash.Clear();
                foreach (var g in gulls) g.root.gameObject.SetActive(visible);
                foreach (var d in dolphins) d.gameObject.SetActive(false);
                if (whale != null) whale.Root.gameObject.SetActive(false);
                dolphinStart = whaleStart = -1;
            }
            if (!visible) return;
            float time = Time.time, dt = Time.deltaTime;
            foreach (var g in gulls) FlyGull(g, time, dt);
            UpdateDolphins(time);
            if (whale != null) UpdateWhale(time);
        }

        void FlyGull(Gull g, float time, float dt)
        {
            g.angle += g.speed / g.radius * dt;
            float drift = Mathf.Sin(time * .13f + g.phase) * near * .12f;
            float r = g.radius + drift;
            var position = new Vector3(Mathf.Cos(g.angle) * r, g.height + Mathf.Sin(time * .4f + g.phase) * 2f, Mathf.Sin(g.angle) * r);
            float sign = Mathf.Sign(g.speed);
            var forward = new Vector3(-Mathf.Sin(g.angle), 0, Mathf.Cos(g.angle)) * sign;
            g.root.localPosition = position;
            g.root.localRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0, 0, sign * 18f);
            // Flap in bursts (the wingtips trail the arm), then glide with wings held slightly up.
            float burst = Mathf.Sin(time * .5f + g.phase) > .1f ? 1 : 0;
            float flap = burst * Mathf.Sin(time * 7f + g.phase) * 32f + (1 - burst) * 7f;
            float tip = burst * Mathf.Sin(time * 7f + g.phase - .6f) * 18f + (1 - burst) * -4f;
            g.leftArm.localRotation = Quaternion.Euler(0, 0, -flap);
            g.rightArm.localRotation = Quaternion.Euler(0, 0, flap);
            g.leftHand.localRotation = Quaternion.Euler(0, 0, -tip);
            g.rightHand.localRotation = Quaternion.Euler(0, 0, tip);
        }

        void UpdateDolphins(float time)
        {
            const float Duration = 2.1f, Length = 20f, Height = 7f, Spacing = 3.6f;
            if (dolphinStart < 0 && time >= nextDolphins)
            {
                dolphinStart = time;
                float reach = new Vector2(dolphinFrom.x, dolphinFrom.z).magnitude;
                if (dolphinFrom == Vector3.zero || reach < far * .7f || reach > far * 1.5f || Random.value < .5f)
                {
                    dolphinFrom = SwimPoint(Random.Range(far * .8f, far * 1.25f));
                    dolphinDir = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward;
                }
            }
            if (dolphinStart < 0) return;
            bool anyAirborne = false;
            var side = Vector3.Cross(Vector3.up, dolphinDir);
            for (int i = 0; i < dolphins.Length; i++)
            {
                float t = (time - dolphinStart - i * .28f) / Duration;
                var d = dolphins[i];
                var offset = side * ((i - 1) * Spacing) + dolphinDir * (i == 1 ? 2.5f : 0);
                bool airborne = t >= 0 && t <= 1;
                if (airborne != d.gameObject.activeSelf)
                {
                    var at = World(dolphinFrom + offset + dolphinDir * (t < .5f ? 0 : Length));
                    splash.Crown(at, transform.TransformDirection(dolphinDir), 1.8f, .9f, 26, 7f, .9f);
                    splash.Foam(at, 1.5f, 3, 4f, 4f);
                    d.gameObject.SetActive(airborne);
                }
                if (!airborne) { if (t < 0) anyAirborne = true; continue; }
                anyAirborne = true;
                var point = dolphinFrom + offset + dolphinDir * (Length * t) + Vector3.up * (Height * 4 * t * (1 - t) - 1f);
                var velocity = dolphinDir * Length + Vector3.up * (Height * 4 * (1 - 2 * t));
                d.localPosition = point;
                d.localRotation = Quaternion.LookRotation(velocity.normalized);
            }
            if (!anyAirborne)
            {
                dolphinStart = -1;
                // Pods jump again and again as they travel, with short rests.
                dolphinFrom += dolphinDir * (Length + 6f);
                nextDolphins = time + (Random.value < .6f ? Random.Range(.6f, 1.6f) : Random.Range(2.5f, 5.5f));
            }
        }

        // The breach in "flight" time f: 0 = the nose breaks the surface, 1 = the back slaps down.
        // The body's middle follows y(f) = -.285L + 1.35L f - 1.125L f^2 (peaks a little after half-way),
        // climbing nearly vertical, rolling onto its side over the top and landing on its back.
        void PlaceWhale(float f)
        {
            float L = WhaleLength;
            float y = -.285f * L + 1.35f * L * f - 1.125f * L * f * f;
            float pitch = Mathf.Lerp(72f, -8f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, 1f, f)));
            float roll = Mathf.Lerp(0, 105f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, .95f, f)));
            whale.Root.localPosition = whalePoint + whaleYaw * Vector3.forward * (.25f * L * f) + Vector3.up * y;
            whale.Root.localRotation = whaleYaw * Quaternion.Euler(-pitch, 0, 0) * Quaternion.Euler(0, 0, roll);
            // Powerful tail strokes on the way up, back arched and flippers flung wide over the top.
            float stroke = f < .35f ? Mathf.Sin(f / .35f * Mathf.PI * 2.5f) * 22f : Mathf.Lerp(0, 14f, Mathf.InverseLerp(.75f, 1f, f));
            float arch = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(.2f, .95f, f)) * Mathf.PI) * 10f;
            float wide = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .5f, f)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.9f, 1.1f, f)) * .5f);
            whale.Pose(stroke, arch, wide);
        }

        void UpdateWhale(float time)
        {
            const float Swell = 1.4f, Air = 3.8f;
            float L = WhaleLength;
            if (whaleStart < 0 && time >= nextWhale)
            {
                whaleStart = time;
                lastWhaleT = -1;
                whalePoint = SwimPoint(Random.Range(far * 1.05f, far * 1.45f));
                whaleYaw = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            }
            if (whaleStart < 0) return;
            float t = time - whaleStart, before = lastWhaleT;
            lastWhaleT = t;
            bool Crossed(float at) => before < at && t >= at;
            float f = (t - Swell) / Air;
            var dir = transform.TransformDirection(whaleYaw * Vector3.forward);
            var launch = World(whalePoint);
            var hit = World(whalePoint + whaleYaw * Vector3.forward * (.25f * L));

            // The swell: the sea boils white just before the launch.
            if (Crossed(0)) splash.Foam(launch, .14f * L, 6, .22f * L, 9f);
            if (t < Swell && Random.value < .35f) splash.Column(launch, .1f * L, 2, 4f, .8f);

            bool inView = f > -.25f && f < 1.4f;
            if (inView != whale.Root.gameObject.activeSelf) whale.Root.gameObject.SetActive(inView);
            if (inView)
            {
                PlaceWhale(Mathf.Min(f, 1));
                if (f > 1) whale.Root.localPosition += Vector3.down * ((f - 1) * .9f * L);
            }

            // Launch: a wall of white water bursts up around the rising body and sheets off it.
            if (Crossed(Swell))
            {
                splash.Crown(launch, dir, .12f * L, .1f * L, 160, 18f, 1.4f);
                splash.Column(launch, .07f * L, 70, 22f, 1.6f);
                splash.Mist(launch, .12f * L, 10, .3f * L, 2f);
                splash.Foam(launch, .2f * L, 8, .3f * L, 10f);
            }
            if (f > 0 && f < .45f && Random.value < .8f)
                splash.Drip(World(whalePoint + Vector3.up * Random.Range(0, .35f * L)), .07f * L, 4);

            // The belly-flop: two great curtains of spray along the body, a white wall in the middle,
            // a cloud of mist that hangs in the air, and a wide foam slick that spreads and fades.
            if (Crossed(Swell + Air * .97f))
            {
                splash.Crown(hit, dir, .5f * L, .16f * L, 420, 26f, 1.8f);
                splash.Column(hit, .2f * L, 90, 20f, 2.2f);
                splash.Mist(hit, .4f * L, 26, .45f * L, 2.5f);
                splash.Foam(hit, .5f * L, 22, .45f * L, 14f);
            }
            if (t > Swell + Air && t < Swell + Air + 1.4f && Random.value < .5f)
                splash.Crown(hit, dir, .45f * L, .2f * L, 8, 9f, 1.2f);

            if (t > Swell + Air * 1.45f) { whaleStart = -1; whale.Root.gameObject.SetActive(false); nextWhale = time + Random.Range(30f, 55f); }
        }

        void OnDestroy()
        {
            whale?.Destroy();
            foreach (var m in materials) if (m) Destroy(m);
            foreach (var m in meshes) if (m) Destroy(m);
        }
    }
}

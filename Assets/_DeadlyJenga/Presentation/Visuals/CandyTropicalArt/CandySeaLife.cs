using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Ambient sea life around an island: seagulls circling overhead, dolphins leaping in pairs,
    // and a whale that now and then lifts its tail and slaps the water.
    // Visual only: built from a few primitives, no colliders, no gameplay. Local y = 0 is the waterline.
    public sealed class CandySeaLife : MonoBehaviour
    {
        sealed class Gull { public Transform root, leftWing, rightWing; public float radius, height, angle, speed, phase; }
        sealed class Splash { public Transform t; public float age, life, size; }

        readonly List<Gull> gulls = new();
        readonly List<Splash> splashes = new();
        readonly List<Material> materials = new();
        Transform[] dolphins;
        Transform whaleTail, whaleBack;
        Material white, grey, beak, dolphinSkin, whaleSkin, foam;
        float near, far, nextDolphins, nextWhale, dolphinStart = -1, whaleStart = -1;
        Vector3 dolphinFrom, dolphinDir, whalePoint;
        Quaternion whaleYaw;
        bool southOnly;
        Transform hideWith;
        bool shown = true;

        // near: gulls circle inside this ring; far: dolphins and the whale stay beyond it.
        // southOnly keeps swimmers in the southern half (for maps with land to the north).
        // hideWith: optional object (such as the ocean surface); sea life hides while it is inactive.
        public void Build(float nearRadius, float farRadius, bool keepSouth = false, Transform hideWhenInactive = null)
        {
            near = nearRadius; far = farRadius; southOnly = keepSouth; hideWith = hideWhenInactive;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            white = Paint(lit, new Color(.97f, .97f, .96f), .55f);
            grey = Paint(lit, new Color(.55f, .6f, .66f), .5f);
            beak = Paint(lit, new Color(1f, .62f, .12f), .6f);
            dolphinSkin = Paint(lit, new Color(.42f, .52f, .62f), .9f);
            whaleSkin = Paint(lit, new Color(.09f, .12f, .18f), .85f);
            foam = Paint(lit, new Color(.96f, .99f, 1f), .3f);

            for (int i = 0; i < 8; i++) gulls.Add(MakeGull(i));
            dolphins = new[] { MakeDolphin(), MakeDolphin() };
            MakeWhale();
            nextDolphins = Time.time + Random.Range(3f, 8f);
            nextWhale = Time.time + Random.Range(15f, 30f);
        }

        Material Paint(Shader shader, Color color, float smoothness)
        {
            var m = new Material(shader) { name = "Sea life", enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            materials.Add(m);
            return m;
        }

        Transform Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        Gull MakeGull(int index)
        {
            var root = new GameObject("Seagull " + (index + 1)).transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * 1.6f;
            Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(.22f, .2f, .6f), white);
            Part(root, PrimitiveType.Sphere, new Vector3(0, .07f, .3f), Vector3.one * .17f, white);
            Part(root, PrimitiveType.Cube, new Vector3(0, .05f, .42f), new Vector3(.04f, .04f, .12f), beak);
            Part(root, PrimitiveType.Cube, new Vector3(0, .02f, -.36f), new Vector3(.16f, .02f, .16f), grey);
            Transform Wing(float side)
            {
                var pivot = new GameObject(side < 0 ? "Left wing" : "Right wing").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(side * .09f, .04f, .02f);
                Part(pivot, PrimitiveType.Cube, new Vector3(side * .38f, 0, 0), new Vector3(.75f, .025f, .24f), white);
                Part(pivot, PrimitiveType.Cube, new Vector3(side * .83f, 0, -.02f), new Vector3(.2f, .026f, .18f), grey);
                return pivot;
            }
            return new Gull
            {
                root = root, leftWing = Wing(-1), rightWing = Wing(1),
                radius = Random.Range(near * .45f, near), height = Random.Range(12f, 26f),
                angle = Random.Range(0, Mathf.PI * 2), speed = Random.Range(6f, 9f) * (index % 2 == 0 ? 1 : -1),
                phase = Random.Range(0, 10f)
            };
        }

        Transform MakeDolphin()
        {
            var root = new GameObject("Dolphin").transform;
            root.SetParent(transform, false);
            Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(.5f, .5f, 2.2f), dolphinSkin);
            Part(root, PrimitiveType.Sphere, new Vector3(0, -.02f, 1.15f), new Vector3(.14f, .14f, .45f), dolphinSkin);
            Part(root, PrimitiveType.Cube, new Vector3(0, .32f, -.05f), new Vector3(.05f, .4f, .32f), dolphinSkin, new Vector3(-30, 0, 0));
            Part(root, PrimitiveType.Cube, new Vector3(0, 0, -1.15f), new Vector3(.8f, .04f, .25f), dolphinSkin);
            root.gameObject.SetActive(false);
            return root;
        }

        void MakeWhale()
        {
            whaleTail = new GameObject("Whale tail").transform;
            whaleTail.SetParent(transform, false);
            Part(whaleTail, PrimitiveType.Capsule, new Vector3(0, 1.5f, 0), new Vector3(.9f, 1.6f, .7f), whaleSkin);
            for (int side = -1; side <= 1; side += 2)
                Part(whaleTail, PrimitiveType.Sphere, new Vector3(side * 1.4f, 3.2f, 0), new Vector3(2.8f, .9f, .35f), whaleSkin, new Vector3(0, 0, side * -18));
            whaleTail.gameObject.SetActive(false);
            whaleBack = Part(transform, PrimitiveType.Sphere, Vector3.zero, new Vector3(4f, 1.6f, 11f), whaleSkin);
            whaleBack.name = "Whale back";
            whaleBack.gameObject.SetActive(false);
        }

        Vector3 SwimPoint(float radius)
        {
            float angle = southOnly ? Random.Range(200f, 340f) * Mathf.Deg2Rad : Random.Range(0, Mathf.PI * 2);
            return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        void SplashAt(Vector3 point, float size)
        {
            var t = Part(transform, PrimitiveType.Sphere, point, Vector3.zero, foam);
            t.name = "Splash";
            splashes.Add(new Splash { t = t, life = .6f + size * .12f, size = size });
        }

        void Update()
        {
            bool visible = !hideWith || hideWith.gameObject.activeInHierarchy && (!hideWith.TryGetComponent<Renderer>(out var water) || water.enabled);
            if (visible != shown)
            {
                shown = visible;
                foreach (Transform child in transform) child.gameObject.SetActive(visible && (child != whaleTail && child != whaleBack && !System.Array.Exists(dolphins, d => d == child)));
                dolphinStart = whaleStart = -1;
            }
            if (!visible) return;
            float time = Time.time, dt = Time.deltaTime;
            foreach (var g in gulls) FlyGull(g, time, dt);
            UpdateDolphins(time);
            UpdateWhale(time);
            for (int i = splashes.Count - 1; i >= 0; i--)
            {
                var s = splashes[i];
                s.age += dt;
                float k = s.age / s.life;
                if (k >= 1) { Destroy(s.t.gameObject); splashes.RemoveAt(i); continue; }
                float rise = Mathf.Sin(k * Mathf.PI);
                s.t.localScale = new Vector3(s.size * (.6f + k), s.size * rise * 1.4f, s.size * (.6f + k));
            }
        }

        void FlyGull(Gull g, float time, float dt)
        {
            g.angle += g.speed / g.radius * dt;
            float drift = Mathf.Sin(time * .13f + g.phase) * near * .15f;
            float r = g.radius + drift;
            var position = new Vector3(Mathf.Cos(g.angle) * r, g.height + Mathf.Sin(time * .4f + g.phase) * 2f, Mathf.Sin(g.angle) * r);
            float sign = Mathf.Sign(g.speed);
            var forward = new Vector3(-Mathf.Sin(g.angle), 0, Mathf.Cos(g.angle)) * sign;
            g.root.localPosition = position;
            g.root.localRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0, 0, sign * 18f);
            // Flap in bursts, then glide with wings held slightly up.
            float burst = Mathf.Sin(time * .5f + g.phase) > .1f ? 1 : 0;
            float flap = burst * Mathf.Sin(time * 9f + g.phase) * 35f + (1 - burst) * 8f;
            g.leftWing.localRotation = Quaternion.Euler(0, 0, -flap);
            g.rightWing.localRotation = Quaternion.Euler(0, 0, flap);
        }

        void UpdateDolphins(float time)
        {
            const float Duration = 1.5f, Length = 8f, Height = 3f;
            if (dolphinStart < 0 && time >= nextDolphins)
            {
                dolphinStart = time;
                dolphinFrom = SwimPoint(Random.Range(far, far * 1.6f));
                float heading = Random.Range(0, 360f);
                dolphinDir = Quaternion.Euler(0, heading, 0) * Vector3.forward;
            }
            if (dolphinStart < 0) return;
            bool anyAirborne = false;
            for (int i = 0; i < dolphins.Length; i++)
            {
                float t = (time - dolphinStart - i * .3f) / Duration;
                var d = dolphins[i];
                bool airborne = t >= 0 && t <= 1;
                if (airborne != d.gameObject.activeSelf)
                {
                    var side = Vector3.Cross(Vector3.up, dolphinDir) * (i * 2.2f);
                    SplashAt(dolphinFrom + side + dolphinDir * (t < .5f ? 0 : Length), 1.6f);
                    d.gameObject.SetActive(airborne);
                }
                if (!airborne) { if (t < 0) anyAirborne = true; continue; }
                anyAirborne = true;
                var offset = Vector3.Cross(Vector3.up, dolphinDir) * (i * 2.2f);
                var point = dolphinFrom + offset + dolphinDir * (Length * t) + Vector3.up * (Height * 4 * t * (1 - t) - .4f);
                var velocity = dolphinDir * Length + Vector3.up * (Height * 4 * (1 - 2 * t));
                d.localPosition = point;
                d.localRotation = Quaternion.LookRotation(velocity.normalized);
            }
            if (!anyAirborne)
            {
                dolphinStart = -1;
                // Pods jump two or three times in a row, then rest.
                nextDolphins = time + (Random.value < .55f ? Random.Range(1.2f, 2.5f) : Random.Range(8f, 18f));
                if (Random.value < .55f) dolphinFrom += dolphinDir * 12f;
            }
        }

        void UpdateWhale(float time)
        {
            if (whaleStart < 0 && time >= nextWhale)
            {
                whaleStart = time;
                whalePoint = SwimPoint(Random.Range(far * 1.3f, far * 1.8f));
                whaleYaw = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            }
            if (whaleStart < 0) return;
            float t = time - whaleStart;
            // 0-2 s: the back rolls over; 2-4.5 s: the tail lifts and hangs; 4.5-5.1 s: the slap; then gone.
            bool back = t < 2.4f;
            whaleBack.gameObject.SetActive(back);
            if (back)
            {
                float rise = Mathf.Sin(Mathf.Clamp01(t / 2.4f) * Mathf.PI);
                whaleBack.localPosition = whalePoint + Vector3.up * (rise * 1.1f - .9f);
                whaleBack.localRotation = whaleYaw;
            }
            bool tail = t >= 1.8f && t < 5.1f;
            if (tail != whaleTail.gameObject.activeSelf) whaleTail.gameObject.SetActive(tail);
            if (tail)
            {
                float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 1.8f) / 1.2f));
                float slap = Mathf.Clamp01((t - 4.5f) / .6f);
                float tilt = Mathf.Lerp(0, 95f, slap * slap);
                whaleTail.localPosition = whalePoint + whaleYaw * Vector3.back * 6f + Vector3.up * (lift * 1.6f - 2.2f - slap * 1.5f);
                whaleTail.localRotation = whaleYaw * Quaternion.Euler(-tilt, 0, Mathf.Sin(t * 1.3f) * 4f);
            }
            if (t >= 5.0f && t - Time.deltaTime < 5.0f)
            {
                var hit = whalePoint + whaleYaw * Vector3.back * 10f;
                SplashAt(hit, 5f);
                SplashAt(hit + whaleYaw * Vector3.right * 2.5f, 3.5f);
                SplashAt(hit - whaleYaw * Vector3.right * 2.5f, 3.5f);
            }
            if (t > 5.4f) { whaleStart = -1; nextWhale = time + Random.Range(25f, 50f); }
        }

        void OnDestroy()
        {
            foreach (var m in materials) if (m) Destroy(m);
        }
    }
}

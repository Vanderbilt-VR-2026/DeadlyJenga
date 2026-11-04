using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Destruction;

namespace DeadlyJenga.CastlePrototype
{
    // When a survivor is eliminated: they burst into candy pieces, a shockwave ring and flash go off,
    // a glowing wisp rises into the sky (they come back as a giant hand), and "OUT!" floats up.
    // The eliminated player also gets a banner in front of their eyes.
    public sealed class EliminationEffects : MonoBehaviour
    {
        static EliminationEffects instance;

        sealed class Fx { public GameObject go; public float born, life; public Action<Fx, float> tick; public Vector3 a, b; }
        readonly List<Fx> effects = new List<Fx>();
        readonly Dictionary<Color, Material> pieceMaterials = new Dictionary<Color, Material>();
        Mesh cube, sphere, disc;
        Material ring, flash, wisp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Elimination effects");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EliminationEffects>();
        }

        void Awake()
        {
            cube = DestructionPresentation.PrimitiveMesh(PrimitiveType.Cube);
            sphere = DestructionPresentation.PrimitiveMesh(PrimitiveType.Sphere);
            disc = DestructionPresentation.PrimitiveMesh(PrimitiveType.Cylinder);
            ring = DestructionPresentation.MakeMaterial(new Color(1, .95f, .7f, .6f), true);
            flash = DestructionPresentation.MakeMaterial(new Color(1, 1, 1, .8f), true);
            wisp = DestructionPresentation.MakeMaterial(new Color(.75f, 1, .95f, 1), false);
        }

        void OnEnable() => GameEvents.BodyBurst += OnBurst;
        void OnDisable() => GameEvents.BodyBurst -= OnBurst;

        void OnBurst(GameObject who, Vector3 feet, Color color, string name, bool local)
        {
            var center = feet + Vector3.up * .9f;
            Color[] palette = { color, Color.Lerp(color, Color.white, .5f), Color.white, new Color(1, .5f, .75f), new Color(.35f, 1, .8f) };
            for (int i = 0; i < 26; i++) Piece(center + UnityEngine.Random.insideUnitSphere * .35f, palette[i % palette.Length]);
            Shockwave(feet + Vector3.up * .1f);
            Flash(center);
            Wisp(center, color);
            Label(center + Vector3.up * 1.4f, local ? "YOU'RE OUT!" : name + " is OUT!", color);
            if (local) Banner();
            var engine = CandyAudioEngine.Instance;
            if (engine)
            {
                engine.Play(CandySound.BlockSmash, center, 1, GetInstanceID(), 1.4f);
                engine.Play(CandySound.Respawn, center, 1, GetInstanceID() + 1, .8f);
            }
        }

        Fx Add(GameObject go, float life, Action<Fx, float> tick)
        {
            go.transform.SetParent(transform, true);
            var fx = new Fx { go = go, born = Time.time, life = life, tick = tick };
            effects.Add(fx);
            return fx;
        }

        GameObject Shape(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void Piece(Vector3 at, Color color)
        {
            if (!pieceMaterials.TryGetValue(color, out var m)) pieceMaterials[color] = m = DestructionPresentation.MakeMaterial(color, false, true);
            var go = Shape("Candy piece", UnityEngine.Random.value < .5f ? cube : sphere, m);
            go.transform.position = at; go.transform.rotation = UnityEngine.Random.rotation;
            float size = UnityEngine.Random.Range(.08f, .2f);
            go.transform.localScale = Vector3.one * size;
            var col = go.AddComponent<BoxCollider>();
            col.excludeLayers = (1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface);
            var body = go.AddComponent<Rigidbody>(); body.mass = .05f;
            var dir = (UnityEngine.Random.insideUnitSphere + Vector3.up * .8f).normalized;
            body.linearVelocity = dir * UnityEngine.Random.Range(3f, 7f);
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * 10;
            Add(go, UnityEngine.Random.Range(2.5f, 4f), (fx, t) => { if (t > .8f) fx.go.transform.localScale = Vector3.one * size * (1 - (t - .8f) / .2f); });
        }

        void Shockwave(Vector3 at)
        {
            var go = Shape("Shockwave", disc, ring);
            go.transform.position = at;
            Add(go, .6f, (fx, t) =>
            {
                float s = Mathf.Lerp(.5f, 9, 1 - (1 - t) * (1 - t));
                fx.go.transform.localScale = new Vector3(s, .02f, s);
                DestructionPresentation.SetColor(ring, new Color(1, .95f, .7f, .6f * (1 - t)));
            });
        }

        void Flash(Vector3 at)
        {
            var go = Shape("Flash", sphere, flash);
            go.transform.position = at;
            Add(go, .3f, (fx, t) =>
            {
                fx.go.transform.localScale = Vector3.one * Mathf.Lerp(.3f, 2.5f, t);
                DestructionPresentation.SetColor(flash, new Color(1, 1, 1, .8f * (1 - t)));
            });
        }

        void Wisp(Vector3 at, Color color)
        {
            var go = Shape("Wisp", sphere, wisp);
            go.transform.position = at; go.transform.localScale = Vector3.one * .35f;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = .9f; trail.widthMultiplier = .3f; trail.sharedMaterial = flash;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, .5f), 0), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            var fx = Add(go, 2.8f, (f, t) =>
            {
                float rise = t * t * 45;
                var side = new Vector3(Mathf.Sin(t * 14) * 1.2f * (1 - t), 0, Mathf.Cos(t * 11) * 1.2f * (1 - t));
                f.go.transform.position = f.a + Vector3.up * (rise + t * 2) + side;
                f.go.transform.localScale = Vector3.one * Mathf.Lerp(.35f, .12f, t);
            });
            fx.a = at;
        }

        void Label(Vector3 at, string text, Color color)
        {
            var go = new GameObject("Out label", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(600, 100);
            var group = go.AddComponent<CanvasGroup>();
            var t = Text(go.transform, text, 64, Color.Lerp(color, Color.white, .3f));
            t.rectTransform.sizeDelta = new Vector2(600, 100);
            var outline = t.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .8f); outline.effectDistance = new Vector2(3, -3);
            go.transform.position = at;
            var fx = Add(go, 2.6f, (f, k) =>
            {
                var cam = Services.Session?.ActiveCamera; if (!cam) cam = Camera.main;
                f.go.transform.position = f.a + Vector3.up * k * 1.5f;
                if (cam) f.go.transform.rotation = Quaternion.LookRotation(f.go.transform.position - cam.transform.position, Vector3.up);
                float pop = k < .08f ? Mathf.SmoothStep(0, 1.2f, k / .08f) : 1;
                f.go.transform.localScale = Vector3.one * .006f * pop;
                group.alpha = Mathf.Clamp01((1 - k) / .25f);
            });
            fx.a = at;
        }

        void Banner()
        {
            var go = new GameObject("Eliminated banner", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(900, 260);
            var bg = go.AddComponent<Image>(); bg.sprite = CandyHudVisuals.Pill; bg.type = Image.Type.Sliced; bg.color = new Color(.35f, .03f, .12f, .9f);
            var group = go.AddComponent<CanvasGroup>();
            var big = Text(go.transform, "ELIMINATED", 96, Color.white); big.rectTransform.anchoredPosition = new Vector2(0, 35); big.rectTransform.sizeDelta = new Vector2(900, 130);
            var small = Text(go.transform, "you're coming back as a giant hand. get revenge.", 30, CandyHudVisuals.Peach); small.rectTransform.anchoredPosition = new Vector2(0, -70); small.rectTransform.sizeDelta = new Vector2(900, 60);
            Add(go, 3.2f, (f, k) =>
            {
                var cam = Services.Session?.ActiveCamera; if (!cam) cam = Camera.main;
                if (!cam) return;
                var forward = cam.transform.forward;
                var target = cam.transform.position + forward * 1.6f;
                f.go.transform.position = k < .01f ? target : Vector3.Lerp(f.go.transform.position, target, .15f);
                f.go.transform.rotation = Quaternion.LookRotation(f.go.transform.position - cam.transform.position, Vector3.up);
                float pop = k < .06f ? Mathf.SmoothStep(0, 1.1f, k / .06f) : Mathf.Lerp(1.1f, 1, Mathf.Clamp01((k - .06f) / .05f));
                f.go.transform.localScale = Vector3.one * .0011f * pop;
                group.alpha = Mathf.Clamp01((1 - k) / .2f);
            });
        }

        static Text Text(Transform parent, string value, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = value; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void Update()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var fx = effects[i];
                float t = (Time.time - fx.born) / fx.life;
                if (!fx.go || t >= 1) { if (fx.go) Destroy(fx.go); effects.RemoveAt(i); continue; }
                fx.tick?.Invoke(fx, t);
            }
        }
    }
}

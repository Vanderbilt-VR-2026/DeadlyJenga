using UnityEngine;

namespace DeadlyJenga.NinjaArt
{
    // Shared procedural textures, materials and particle systems. No imported art required.
    public static class NinjaFx
    {
        static Texture2D soft, streak, leaf, puff;
        static Material glow, mist;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { soft = streak = leaf = puff = null; glow = mist = null; }
        public static Shader Find(string name) => Resources.Load<Shader>("NinjaScenery/" + name);
        public static Texture2D Soft => soft ? soft : soft = Make(64, 64, (x, y) => { float r = new Vector2(x - .5f, y - .5f).magnitude * 2; float a = Mathf.Clamp01(1 - r); return new Color(1, 1, 1, a * a); });
        public static Texture2D Streak => streak ? streak : streak = Make(8, 64, (x, y) => { float a = Mathf.Clamp01(1 - Mathf.Abs(x - .5f) * 2) * Mathf.Sin(y * Mathf.PI); return new Color(1, 1, 1, a); });
        public static Texture2D Leaf => leaf ? leaf : leaf = Make(32, 32, (x, y) =>
        {
            var p = new Vector2(x - .5f, y - .5f) * 2; float ang = Mathf.Atan2(p.y, p.x); float lobes = .55f + .45f * Mathf.Abs(Mathf.Cos(ang * 2.5f));
            float a = Mathf.Clamp01((lobes - p.magnitude) * 8); if (Mathf.Abs(p.x) < .06f && p.y < 0) a = 1; return new Color(1, 1, 1, a);
        });
        public static Texture2D Puff => puff ? puff : puff = Make(64, 64, (x, y) =>
        {
            var p = new Vector2(x - .5f, y - .5f) * 2; float n = Mathf.PerlinNoise(x * 6, y * 6) * .5f + Mathf.PerlinNoise(x * 13 + 4, y * 13) * .25f;
            float a = Mathf.Clamp01((1 - p.magnitude) * 1.4f) * (.55f + n); return new Color(1, 1, 1, Mathf.Clamp01(a * a * 1.4f));
        });
        static Texture2D Make(int w, int h, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Ninja procedural" };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, f((x + .5f) / w, (y + .5f) / h));
            t.Apply(true, true); return t;
        }
        public static Material Glow => glow ? glow : glow = new Material(Find("NinjaGlow")) { name = "Ninja glow", enableInstancing = true };
        public static Material Mist
        {
            get
            {
                if (mist) return mist;
                mist = new Material(Find("NinjaGlow")) { name = "Ninja mist", enableInstancing = true };
                mist.SetFloat("_Intensity", .32f); mist.SetFloat("_NearFade", 9); mist.SetFloat("_FarFade", 700); mist.SetFloat("_FogDim", .15f); return mist;
            }
        }
        public static Material Particle(Texture texture, bool additive, float nearFade = .3f)
        {
            var m = new Material(Find("NinjaParticle")) { name = additive ? "Ninja additive particle" : "Ninja alpha particle", enableInstancing = true };
            m.SetTexture("_MainTex", texture); m.SetFloat("_SrcBlend", additive ? 1 : 5); m.SetFloat("_DstBlend", additive ? 1 : 10); m.SetFloat("_NearFade", nearFade);
            return m;
        }
        public static ParticleSystem System(string name, Transform parent, Material material, int max)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.loop = true;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingFudge = 10;
            var e = ps.emission; e.rateOverTime = 0; return ps;
        }
        public static ParticleSystem Rain(Transform parent)
        {
            var ps = System("Visual - rain", parent, Particle(Streak, true, .6f), 1400);
            var main = ps.main; main.startLifetime = 1.3f; main.startSpeed = 0; main.startSize = .035f; main.startColor = new Color(.55f, .65f, .9f, .32f);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(34, 1, 34); shape.position = Vector3.up * 14;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(2.2f, 2.8f); v.y = new ParticleSystem.MinMaxCurve(-24, -20); v.z = new ParticleSystem.MinMaxCurve(1.4f, 1.8f);
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .055f; r.lengthScale = 1;
            var e = ps.emission; e.rateOverTime = 720; ps.Play(); return ps;
        }
        public static ParticleSystem Leaves(Transform parent)
        {
            var ps = System("Visual - falling maple leaves", parent, Particle(Leaf, false), 90);
            var main = ps.main; main.startLifetime = 10; main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.13f, .24f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.8f, .1f, .06f, .95f), new Color(1, .35f, .08f, .95f)); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(40, 1, 40); shape.position = Vector3.up * 10;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(.4f, 1.4f); v.y = new ParticleSystem.MinMaxCurve(-1.3f, -.8f); v.z = new ParticleSystem.MinMaxCurve(.2f, .9f);
            var n = ps.noise; n.enabled = true; n.strength = .9f; n.frequency = .35f; n.scrollSpeed = .3f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(.08f, .85f);
            var e = ps.emission; e.rateOverTime = 7; ps.Play(); return ps;
        }
        public static ParticleSystem Fireflies(Transform parent)
        {
            var ps = System("Visual - fireflies", parent, Particle(Soft, true), 60);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(4, 7); main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.07f, .12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.6f, 1, .35f, 1), new Color(1, .9f, .4f, 1));
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(44, 4, 44); shape.position = Vector3.up * 2;
            var n = ps.noise; n.enabled = true; n.strength = .6f; n.frequency = .5f; n.scrollSpeed = .2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(.25f, .7f);
            var e = ps.emission; e.rateOverTime = 7; ps.Play(); return ps;
        }
        public static ParticleSystem Smoke(Transform parent)
        {
            var ps = System("Visual - smoke bombs", parent, Particle(Puff, false, .4f), 240);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f); main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.8f, 1.7f); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.gravityModifier = -.05f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.2f, .17f, .26f, .8f), new Color(.32f, .28f, .4f, .7f));
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .4f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(.3f, 1.4f), new Keyframe(1, 2.2f)));
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = .18f; limit.limit = .3f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.8f, .8f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(.05f, .55f);
            ps.Play(); return ps;
        }
        public static ParticleSystem Sparks(Transform parent)
        {
            var ps = System("Visual - lantern sparks", parent, Particle(Soft, true), 200);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.3f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .14f); main.gravityModifier = .35f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .55f, .2f, 1), new Color(1, .85f, .5f, 1));
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(.02f, .4f);
            ps.Play(); return ps;
        }
        static Gradient Fade(float inEnd, float outStart)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, inEnd), new GradientAlphaKey(1, outStart), new GradientAlphaKey(0, 1) });
            return g;
        }
        public static void Burst(ParticleSystem ps, Vector3 at, int count, Color? color = null)
        {
            if (!ps) return; var p = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            if (color.HasValue) p.startColor = color.Value; ps.Emit(p, count);
        }
    }
}

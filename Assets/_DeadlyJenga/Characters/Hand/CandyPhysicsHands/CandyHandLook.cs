using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CandyPhysicsHands
{
    // Dresses one physical candy hand in a design. Rebuilds itself when the hand rebuilds its bones,
    // and shows or hides everything along with the hand's skin.
    public sealed class CandyHandLook : MonoBehaviour, IHandLook
    {
        CandyPhysicalHand hand;
        int current = int.MinValue;
        Rigidbody[] builtFor;
        float builtSize;
        internal readonly List<GameObject> made = new List<GameObject>();
        internal readonly List<Renderer> renderers = new List<Renderer>();
        internal readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        internal readonly List<TrailRenderer> trails = new List<TrailRenderer>();
        internal Material skinOverride;
        internal bool tint;
        internal Color tintColor;
        internal float metallic, smoothness;
        static MaterialPropertyBlock block;
        bool shown = true;
        Vector3 lastPosition, velocity;

        public int Look => Mathf.Max(0, current);
        public bool Visible => hand && hand.skin && hand.skin.enabled && hand.gameObject.activeInHierarchy;
        public Vector3 Position => hand && hand.bodies != null && hand.bodies.Length > 0 && hand.bodies[0] ? hand.bodies[0].position : transform.position;
        public Vector3 Velocity => velocity;

        public static CandyHandLook For(CandyPhysicalHand hand)
        {
            var look = hand.GetComponent<CandyHandLook>();
            if (!look) look = hand.gameObject.AddComponent<CandyHandLook>();
            look.hand = hand;
            return look;
        }

        void OnEnable() => Tracked.HandLooks.Add(this);
        void OnDisable() => Tracked.HandLooks.Remove(this);

        public void Apply(int design)
        {
            if (design < 0 || design >= CandyHandDesigns.Count) design = 0;
            if (design == current) return;
            current = design;
            Rebuild();
        }

        void Clear()
        {
            foreach (var go in made) if (go) Destroy(go);
            made.Clear(); renderers.Clear(); particles.Clear(); trails.Clear();
            skinOverride = null; tint = false;
        }

        void Rebuild()
        {
            Clear();
            builtFor = null;
            if (!hand || hand.bodies == null || hand.bodies.Length != 16 || !hand.bodies[0]) { ApplySkin(); return; }
            builtFor = hand.bodies; builtSize = hand.GeometrySize;
            CandyHandDesigns.Build(current, new CandyHandParts(this, hand.bodies, builtSize));
            shown = true;
            ApplySkin();
        }

        void ApplySkin()
        {
            if (!hand || !hand.skin) return;
            var wanted = skinOverride ? skinOverride : hand.settings ? hand.settings.skin : null;
            if (wanted && hand.skin.sharedMaterial != wanted) hand.skin.sharedMaterial = wanted;
            if (tint && !skinOverride)
            {
                block ??= new MaterialPropertyBlock();
                block.Clear(); block.SetColor("_BaseColor", tintColor); block.SetFloat("_Metallic", metallic); block.SetFloat("_Smoothness", smoothness);
                hand.skin.SetPropertyBlock(block);
            }
            else hand.skin.SetPropertyBlock(null);
        }

        void LateUpdate()
        {
            if (!hand) return;
            if (current != int.MinValue && (hand.bodies != builtFor || Mathf.Abs(hand.GeometrySize - builtSize) > .001f)) Rebuild();
            ApplySkin();
            bool show = Visible;
            if (show != shown)
            {
                shown = show;
                foreach (var r in renderers) if (r) r.enabled = show;
                foreach (var ps in particles) if (ps) { var e = ps.emission; e.enabled = show; }
                foreach (var t in trails) if (t) { t.emitting = show; if (!show) t.Clear(); }
            }
            var p = Position;
            if (Time.deltaTime > 0) velocity = Vector3.Lerp(velocity, (p - lastPosition) / Time.deltaTime, .3f);
            lastPosition = p;
        }

        void OnDestroy() => Clear();
    }

    // What a hand design builds with.
    public sealed class CandyHandParts
    {
        readonly CandyHandLook look;
        readonly Rigidbody[] bones;
        public readonly float size;
        static MaterialPropertyBlock block;

        public CandyHandParts(CandyHandLook look, Rigidbody[] bones, float size) { this.look = look; this.bones = bones; this.size = size; }

        public Transform Palm => bones[0].transform;
        public Transform Bone(int i) => bones[i].transform;
        public int Finger(int finger, int joint) => 1 + finger * 3 + joint;
        public float Radius(int i) { int f = (i - 1) / 3, j = (i - 1) % 3; return ((f == 0 ? .011f : .0078f) - j * .001f) * size; }
        public float Length(int i)
        {
            int j = (i - 1) % 3;
            if (j < 2) return Mathf.Max(.002f * size, Vector3.Distance(bones[i].position, bones[i + 1].position));
            return Length(i - 1) * .8f;
        }

        public void Skin(Color color, float metallic, float smoothness)
        { look.tint = true; look.tintColor = color; look.metallic = metallic; look.smoothness = smoothness; }
        public void SkinMaterial(Color color) => look.skinOverride = CandyDesignParts.Material(color, CandyLook.Glass);

        public GameObject Add(Transform bone, PrimitiveType shape, Vector3 position, Vector3 euler, Vector3 scale, Color color, CandyLook kind = CandyLook.Solid)
        {
            var go = new GameObject(shape.ToString());
            go.transform.SetParent(bone, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = CandyDesignParts.Mesh(shape);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = CandyDesignParts.Material(color, kind);
            look.made.Add(go); look.renderers.Add(r);
            return go;
        }

        public void Particles(Color color, float rate, float lifetime, float speed, float particleSize, float gravity)
        {
            var go = new GameObject("Hand particles");
            go.transform.SetParent(Palm, false);
            go.transform.localPosition = new Vector3(0, 0, .02f) * size;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = particleSize; main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = gravity; main.maxParticles = 150;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .045f * size;
            var col = ps.colorOverLifetime; col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial(color);
            ps.Play();
            look.made.Add(go); look.particles.Add(ps);
        }

        public void Trail(Color color, float time, float width)
        {
            var go = new GameObject("Hand trail");
            go.transform.SetParent(Palm, false);
            go.transform.localPosition = new Vector3(0, 0, .02f) * size;
            var t = go.AddComponent<TrailRenderer>();
            t.time = time; t.widthMultiplier = width; t.minVertexDistance = .02f * size;
            t.sharedMaterial = CandyDesignParts.Material(new Color(color.r, color.g, color.b, 1), CandyLook.Glass);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(color.a, 0), new GradientAlphaKey(0, 1) });
            t.colorGradient = g;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.made.Add(go); look.trails.Add(t);
        }

        static Material particleMaterial;
        static Material ParticleMaterial(Color color)
        {
            if (!particleMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                particleMaterial = new Material(shader) { name = "Candy hand particles" };
                if (particleMaterial.HasProperty("_Surface")) { particleMaterial.SetFloat("_Surface", 1); particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); }
                particleMaterial.SetFloat("_ZWrite", 0);
                particleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return particleMaterial;
        }
    }
}

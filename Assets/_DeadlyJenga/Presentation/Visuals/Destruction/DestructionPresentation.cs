using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.Destruction
{
    // Shows the destruction systems: a glow on blocks under strain, chunks when one breaks,
    // and sounds for cracking, breaking and rewinding. Reads Core only.
    public sealed class DestructionPresentation : MonoBehaviour
    {
        public static DestructionPresentation Instance { get; private set; }
        public float glowFrom = .5f;
        public int maxChunks = 240;
        public float chunkLife = 4;

        sealed class Chunk { public GameObject go; public Rigidbody body; public float until; }

        Mesh cube;
        Material[] glow;
        Material chunkMaterial;
        readonly List<Matrix4x4>[] bands = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        readonly List<Chunk> pool = new List<Chunk>();
        readonly List<IBreakable> breakables = new List<IBreakable>();
        readonly List<BoxCollider> boxes = new List<BoxCollider>();
        IReadOnlyList<Rigidbody> source;
        Rigidbody first;
        int sourceCount;
        float crackSoundAt, breakSoundAt, nextScan;
        readonly List<int> strained = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("Destruction — presentation only");
            DontDestroyOnLoad(go);
            go.AddComponent<DestructionPresentation>();
            go.AddComponent<CollapseReplay>();
        }

        void Awake()
        {
            Instance = this;
            cube = PrimitiveMesh(PrimitiveType.Cube);
            glow = new[]
            {
                MakeMaterial(new Color(1, .85f, .2f, .25f), true),
                MakeMaterial(new Color(1, .45f, .1f, .4f), true),
                MakeMaterial(new Color(1, .1f, .1f, .55f), true),
            };
            chunkMaterial = MakeMaterial(new Color(.95f, .55f, .7f, 1), false, true);
        }

        void OnEnable()
        {
            GameEvents.BlockCracked += OnCracked;
            GameEvents.BlockBroken += OnBroken;
            GameEvents.RewindStarted += OnRewindStarted;
            GameEvents.RewindEnded += OnRewindEnded;
        }
        void OnDisable()
        {
            GameEvents.BlockCracked -= OnCracked;
            GameEvents.BlockBroken -= OnBroken;
            GameEvents.RewindStarted -= OnRewindStarted;
            GameEvents.RewindEnded -= OnRewindEnded;
        }

        void Refresh()
        {
            var list = Services.World?.Blocks;
            int n = list?.Count ?? 0;
            if (list == source && n == sourceCount && (n == 0 || list[0] == first)) return;
            source = list; sourceCount = n; first = n > 0 ? list[0] : null;
            breakables.Clear(); boxes.Clear();
            for (int i = 0; i < n; i++)
            {
                var body = list[i];
                breakables.Add(body ? body.GetComponent<IBreakable>() : null);
                boxes.Add(body ? body.GetComponent<BoxCollider>() : null);
            }
        }

        void Update()
        {
            foreach (var c in pool)
                if (c.go.activeSelf && Time.time > c.until) c.go.SetActive(false);
        }

void LateUpdate()
        {
            Refresh();
            // Finding strained blocks scans the whole tower, so do it 10x a second; drawing them stays every frame.
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + .1f;
                strained.Clear();
                for (int i = 0; i < breakables.Count; i++)
                {
                    var b = breakables[i];
                    if (b != null && boxes[i] && !b.Broken && b.Stress >= glowFrom) strained.Add(i);
                }
            }
            foreach (var band in bands) band.Clear();
            foreach (int i in strained)
            {
                if (i >= breakables.Count) continue;
                var b = breakables[i]; var box = boxes[i];
                if (b == null || !box || b.Broken) continue;
                float s = b.Stress;
                if (s < glowFrom) continue;
                int band = s < .75f ? 0 : s < 1 ? 1 : 2;
                var t = box.transform;
                var size = Vector3.Scale(box.size, t.lossyScale) * 1.03f;
                bands[band].Add(Matrix4x4.TRS(t.TransformPoint(box.center), t.rotation, size));
            }
            var red = glow[2].color;
            red.a = .35f + .3f * Mathf.PingPong(Time.time * 4, 1);
            SetColor(glow[2], red);
            for (int b = 0; b < 3; b++) if (bands[b].Count > 0) Draw(cube, glow[b], bands[b]);
        }

        void OnCracked(GameObject block, float stress)
        {
            if (block) Play(CandySound.StructureLean, block.transform.position, Mathf.Clamp01(stress), ref crackSoundAt, .15f);
        }

        void OnBroken(GameObject block, Vector3 position, Vector3 velocity)
        {
            var box = block ? block.GetComponent<BoxCollider>() : null;
            var size = box ? Vector3.Scale(box.size, block.transform.lossyScale) : Vector3.one;
            var rotation = block ? block.transform.rotation : Quaternion.identity;
            var half = size * .25f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var offset = rotation * Vector3.Scale(new Vector3(x, y, z), half);
                Spawn(position + offset, rotation, size * .48f, velocity + offset.normalized * Random.Range(1.5f, 4f));
            }
            Play(CandySound.BlockSmash, position, 1, ref breakSoundAt, .05f);
        }

        void OnRewindStarted(float seconds)
        {
            var e = CandyAudioEngine.Instance;
            if (e && e.Listener) e.Play(CandySound.Respawn, e.Listener.position, 1, GetInstanceID(), .55f, true);
            foreach (var c in pool) c.go.SetActive(false);
        }

        void OnRewindEnded()
        {
            var e = CandyAudioEngine.Instance;
            if (e && e.Listener) e.Play(CandySound.Spawn, e.Listener.position, 1, GetInstanceID(), 1.2f, true);
        }

        void Spawn(Vector3 position, Quaternion rotation, Vector3 size, Vector3 velocity)
        {
            var c = Take();
            c.go.transform.SetPositionAndRotation(position, rotation);
            c.go.transform.localScale = size;
            c.go.SetActive(true);
            c.body.position = position; c.body.rotation = rotation;
            c.body.linearVelocity = velocity;
            c.body.angularVelocity = Random.insideUnitSphere * 6;
            c.until = Time.time + chunkLife * Random.Range(.8f, 1.2f);
        }

        Chunk Take()
        {
            foreach (var c in pool) if (!c.go.activeSelf) return c;
            if (pool.Count < maxChunks)
            {
                var go = new GameObject("Debris");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                go.AddComponent<MeshRenderer>().sharedMaterial = chunkMaterial;
                var col = go.AddComponent<BoxCollider>();
                // Debris is only for looks: it never touches blocks or players.
                col.excludeLayers = (1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface);
                var body = go.AddComponent<Rigidbody>();
                body.mass = .2f;
                var chunk = new Chunk { go = go, body = body };
                pool.Add(chunk);
                return chunk;
            }
            Chunk oldest = pool[0];
            foreach (var c in pool) if (c.until < oldest.until) oldest = c;
            return oldest;
        }

        void Play(CandySound sound, Vector3 at, float intensity, ref float nextAt, float gap)
        {
            var e = CandyAudioEngine.Instance;
            if (!e || Time.time < nextAt) return;
            nextAt = Time.time + gap;
            e.Play(sound, at, intensity, GetInstanceID());
        }

        // Shared helpers for the destruction visuals.
        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return mesh;
        }

        public static Material MakeMaterial(Color color, bool transparent, bool lit = false)
        {
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { enableInstancing = true };
            if (transparent)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            SetColor(m, color);
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        }

        public static void Draw(Mesh mesh, Material material, List<Matrix4x4> list)
        {
            if (!mesh || !material) return;
            var rp = new RenderParams(material);
            for (int start = 0; start < list.Count; start += 1023)
                Graphics.RenderMeshInstanced(rp, mesh, 0, list, Mathf.Min(1023, list.Count - start), start);
        }
    }
}

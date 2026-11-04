using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.Destruction
{
    // Makes the destruction systems look physical:
    //  - strained blocks grow real cracks across their own surface (4 stages as stress rises),
    //  - blocks about to fail sift dust from underneath,
    //  - a broken block splits into irregular shards and splinters of its own material inside a dust cloud.
    // Reads Core only (IBreakable, block events). Sounds for creaks, smashes and rewinds.
    public sealed class DestructionPresentation : MonoBehaviour
    {
        public static DestructionPresentation Instance { get; private set; }
        public int maxChunks = 180;
        public float chunkLife = 5;

        // Stress at which each crack stage appears (stage art comes from CrackArt.StageReveal).
        static readonly float[] StageAt = { .55f, .7f, .85f, 1f };
        const int Patterns = 2;   // two crack patterns so neighbours don't match

        sealed class Chunk { public GameObject go; public Rigidbody body; public MeshFilter filter; public MeshRenderer renderer; public float until; public Vector3 size; }

        Mesh cube;
        Mesh[] shards, splinters;
        Material[] crackMaterials;   // [pattern * 4 + stage]
        readonly Dictionary<Mesh, List<Matrix4x4>[]> crackDraws = new Dictionary<Mesh, List<Matrix4x4>[]>();
        ParticleSystem dust, sift;
        Material fallbackChunk;
        MaterialPropertyBlock copied;

        readonly List<Chunk> pool = new List<Chunk>();
        readonly List<IBreakable> breakables = new List<IBreakable>();
        readonly List<BoxCollider> boxes = new List<BoxCollider>();
        readonly List<MeshRenderer> visuals = new List<MeshRenderer>();
        readonly List<MeshFilter> filters = new List<MeshFilter>();
        readonly List<int> strained = new List<int>();
        IReadOnlyList<Rigidbody> source;
        Rigidbody first;
        int sourceCount;
        float crackSoundAt, breakSoundAt, nextScan;

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
            var rng = new System.Random(1234);
            shards = new Mesh[8]; splinters = new Mesh[4];
            for (int i = 0; i < shards.Length; i++) shards[i] = ShardMesh(rng, false);
            for (int i = 0; i < splinters.Length; i++) splinters[i] = ShardMesh(rng, true);

            crackMaterials = new Material[Patterns * StageAt.Length];
            for (int p = 0; p < Patterns; p++)
            {
                var stages = CrackArt.Stages(23 + p * 17);
                for (int s = 0; s < StageAt.Length; s++) crackMaterials[p * StageAt.Length + s] = CrackMaterial(stages[s]);
            }

            var soft = SoftTexture(64);
            dust = Particles("Break dust", soft, 1500, true);
            sift = Particles("Sifting dust", soft, 600, false);
            fallbackChunk = MakeMaterial(new Color(.8f, .55f, .35f), false, true);
            copied = new MaterialPropertyBlock();
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

        static MeshRenderer Art(GameObject block)
        {
            if (!block) return null;
            var crate = block.GetComponentInChildren<CandyCrateVisual>(true);
            return crate ? crate.Renderer : block.GetComponentInChildren<MeshRenderer>(true);
        }

        void Refresh()
        {
            var list = Services.World?.Blocks;
            int n = list?.Count ?? 0;
            if (list == source && n == sourceCount && (n == 0 || list[0] == first)) return;
            source = list; sourceCount = n; first = n > 0 ? list[0] : null;
            breakables.Clear(); boxes.Clear(); visuals.Clear(); filters.Clear(); strained.Clear();
            for (int i = 0; i < n; i++)
            {
                var body = list[i];
                breakables.Add(body ? body.GetComponent<IBreakable>() : null);
                boxes.Add(body ? body.GetComponent<BoxCollider>() : null);
                var art = body ? Art(body.gameObject) : null;
                visuals.Add(art);
                filters.Add(art ? art.GetComponent<MeshFilter>() : null);
            }
        }

        // The crack shell and where to draw it for block i: wrapped on its artwork, or on its collider box.
        bool CrackShell(int i, out Mesh shell, out Matrix4x4 matrix)
        {
            var art = visuals[i]; var filter = filters[i];
            if (art && filter && filter.sharedMesh)
            {
                shell = CrackArt.Overlay(filter.sharedMesh, art.transform.lossyScale);
                matrix = art.localToWorldMatrix;
                return true;
            }
            var box = boxes[i];
            if (!box) { shell = null; matrix = default; return false; }
            var t = box.transform;
            var size = Vector3.Scale(box.size, t.lossyScale);
            shell = CrackArt.Overlay(cube, size);
            matrix = Matrix4x4.TRS(t.TransformPoint(box.center), t.rotation, size);
            return true;
        }

        // Where block i's bottom face is, for dust.
        bool Body(int i, out Matrix4x4 matrix)
        {
            var art = visuals[i]; var filter = filters[i];
            if (art && filter && filter.sharedMesh)
            {
                var b = filter.sharedMesh.bounds;
                matrix = art.localToWorldMatrix * Matrix4x4.TRS(b.center, Quaternion.identity, b.size);
                return true;
            }
            var box = boxes[i];
            if (!box) { matrix = default; return false; }
            var t = box.transform;
            matrix = Matrix4x4.TRS(t.TransformPoint(box.center), t.rotation, Vector3.Scale(box.size, t.lossyScale));
            return true;
        }

        void Update()
        {
            foreach (var c in pool)
            {
                if (!c.go.activeSelf) continue;
                float left = c.until - Time.time;
                if (left <= 0) { c.go.SetActive(false); continue; }
                if (left < .6f) c.go.transform.localScale = c.size * Mathf.SmoothStep(0, 1, left / .6f);
            }
        }

        void LateUpdate()
        {
            Refresh();
            // Finding strained blocks scans the whole tower, so do it 10x a second; drawing stays every frame.
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + .1f;
                strained.Clear();
                for (int i = 0; i < breakables.Count; i++)
                {
                    var b = breakables[i];
                    if (b == null || b.Broken || b.Stress < StageAt[0]) continue;
                    strained.Add(i);
                    if (b.Stress >= StageAt[2]) Sift(i, b.Stress);
                }
            }
            foreach (var lists in crackDraws.Values) foreach (var l in lists) l.Clear();
            foreach (int i in strained)
            {
                if (i >= breakables.Count) continue;
                var b = breakables[i];
                if (b == null || b.Broken) continue;
                int stage = -1;
                for (int s = 0; s < StageAt.Length; s++) if (b.Stress >= StageAt[s]) stage = s;
                if (stage < 0 || !CrackShell(i, out var shell, out var m)) continue;
                if (!crackDraws.TryGetValue(shell, out var lists))
                {
                    lists = new List<Matrix4x4>[crackMaterials.Length];
                    for (int k = 0; k < lists.Length; k++) lists[k] = new List<Matrix4x4>();
                    crackDraws[shell] = lists;
                }
                lists[(i % Patterns) * StageAt.Length + stage].Add(m);
            }
            foreach (var pair in crackDraws)
                for (int k = 0; k < pair.Value.Length; k++)
                    if (pair.Value[k].Count > 0) Draw(pair.Key, crackMaterials[k], pair.Value[k]);
        }

        // Fine dust trickling out from under a block that is about to fail.
        void Sift(int i, float stress)
        {
            if (!Body(i, out var m)) return;
            int count = stress >= 1 ? 3 : 1;
            var size = m.lossyScale;
            float grain = Mathf.Clamp(Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .08f, .015f, .08f);
            for (int k = 0; k < count; k++)
            {
                var local = new Vector3(Random.Range(-.45f, .45f), -.5f, Random.Range(-.45f, .45f));
                var p = new ParticleSystem.EmitParams
                {
                    position = m.MultiplyPoint3x4(local),
                    velocity = new Vector3(Random.Range(-.05f, .05f), -Random.Range(.1f, .4f), Random.Range(-.05f, .05f)),
                    startSize = grain * Random.Range(.6f, 1.4f),
                    startLifetime = Random.Range(1.2f, 2.2f),
                    startColor = new Color(.92f, .82f, .68f, .55f),
                    applyShapeToPosition = false
                };
                sift.Emit(p, 1);
            }
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
            var art = Art(block);
            float scale = (size.x + size.y + size.z) / 3;

            // Big irregular shards, then thin splinters, all flying apart from the middle.
            for (int k = 0; k < 7; k++)
            {
                var offset = rotation * Vector3.Scale(new Vector3(Random.Range(-.35f, .35f), Random.Range(-.35f, .35f), Random.Range(-.35f, .35f)), size);
                var piece = Vector3.Scale(size, new Vector3(Random.Range(.25f, .45f), Random.Range(.25f, .45f), Random.Range(.25f, .45f)));
                Spawn(shards[Random.Range(0, shards.Length)], art, position + offset, rotation * Random.rotation, piece,
                    velocity * .7f + offset.normalized * Random.Range(1f, 3.2f) + Vector3.up * Random.Range(.3f, 1.2f));
            }
            for (int k = 0; k < 6; k++)
            {
                var offset = rotation * Vector3.Scale(Random.insideUnitSphere * .35f, size);
                float len = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * Random.Range(.25f, .5f);
                var piece = new Vector3(scale * Random.Range(.05f, .09f), scale * Random.Range(.05f, .09f), len);
                Spawn(splinters[Random.Range(0, splinters.Length)], art, position + offset, Random.rotation, piece,
                    velocity * .7f + offset.normalized * Random.Range(2f, 4.5f) + Vector3.up * Random.Range(.5f, 1.8f));
            }

            // A dust cloud that billows out and settles.
            for (int k = 0; k < 28; k++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = position + Vector3.Scale(Random.insideUnitSphere * .45f, size),
                    velocity = Random.insideUnitSphere * Random.Range(.4f, 1.8f) + Vector3.up * .3f + velocity * .2f,
                    startSize = scale * Random.Range(.5f, 1.1f),
                    startLifetime = Random.Range(1.4f, 2.8f),
                    startColor = new Color(.9f, .8f, .66f, Random.Range(.35f, .6f)),
                    rotation = Random.Range(0, 360),
                    applyShapeToPosition = false
                };
                dust.Emit(p, 1);
            }
            Play(CandySound.BlockSmash, position, 1, ref breakSoundAt, .05f);
        }

        void OnRewindStarted(float seconds)
        {
            var e = CandyAudioEngine.Instance;
            if (e && e.Listener) e.Play(CandySound.Respawn, e.Listener.position, 1, GetInstanceID(), .55f, true);
            foreach (var c in pool) c.go.SetActive(false);
            dust.Clear(); sift.Clear();
        }

        void OnRewindEnded()
        {
            var e = CandyAudioEngine.Instance;
            if (e && e.Listener) e.Play(CandySound.Spawn, e.Listener.position, 1, GetInstanceID(), 1.2f, true);
        }

        void Spawn(Mesh mesh, Renderer art, Vector3 position, Quaternion rotation, Vector3 size, Vector3 velocity)
        {
            var c = Take();
            c.filter.sharedMesh = mesh;
            if (art && art.sharedMaterial)
            {
                c.renderer.sharedMaterial = art.sharedMaterial;
                if (art.HasPropertyBlock()) { art.GetPropertyBlock(copied); c.renderer.SetPropertyBlock(copied); }
                else c.renderer.SetPropertyBlock(null);
            }
            else { c.renderer.sharedMaterial = fallbackChunk; c.renderer.SetPropertyBlock(null); }
            c.size = size;
            c.go.transform.SetPositionAndRotation(position, rotation);
            c.go.transform.localScale = size;
            c.go.SetActive(true);
            c.body.position = position; c.body.rotation = rotation;
            c.body.linearVelocity = velocity;
            c.body.angularVelocity = Random.insideUnitSphere * 9;
            c.until = Time.time + chunkLife * Random.Range(.75f, 1.25f);
        }

        Chunk Take()
        {
            foreach (var c in pool) if (!c.go.activeSelf) return c;
            if (pool.Count < maxChunks)
            {
                var go = new GameObject("Debris");
                go.transform.SetParent(transform, false);
                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                var col = go.AddComponent<BoxCollider>();
                // Debris is only for looks: it never touches blocks or players.
                col.excludeLayers = (1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface);
                var body = go.AddComponent<Rigidbody>();
                body.mass = .15f; body.linearDamping = .15f; body.angularDamping = .3f;
                var chunk = new Chunk { go = go, body = body, filter = filter, renderer = renderer };
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

        // ---------- Procedural art ----------

        // An irregular chunk: a cube with every corner pushed around, flat-shaded. Splinters taper to a point.
        static Mesh ShardMesh(System.Random rng, bool splinter)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f);
                c += new Vector3(R(-.2f, .2f), R(-.2f, .2f), R(-.2f, .2f));
                if (splinter && c.z > 0) { c.x *= R(.1f, .35f); c.y *= R(.1f, .35f); }
                corners[i] = c;
            }
            int[][] faces = { new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 } };
            var verts = new List<Vector3>(24); var uvs = new List<Vector2>(24); var tris = new List<int>(36);
            foreach (var f in faces)
            {
                int b = verts.Count;
                for (int k = 0; k < 4; k++) { verts.Add(corners[f[k]]); uvs.Add(new Vector2(k == 1 || k == 2 ? 1 : 0, k >= 2 ? 1 : 0)); }
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            var mesh = new Mesh { name = splinter ? "Splinter" : "Shard" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        static Texture2D SoftTexture(int size)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size, size) * .5f) / (size * .5f);
                float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Soft puff", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(false, true);
            return tex;
        }

        static Material CrackMaterial(Texture2D cracks)
        {
            var m = MakeMaterial(Color.white, true);
            m.name = "Cracks";
            m.SetTexture("_BaseMap", cracks); m.mainTexture = cracks;
            m.renderQueue = (int)RenderQueue.Transparent - 10;
            return m;
        }

        ParticleSystem Particles(string name, Texture2D texture, int max, bool billow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = billow ? -.03f : .15f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.drag = billow ? 1.6f : .4f; limit.multiplyDragByParticleSize = false;
            var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1, billow ? AnimationCurve.EaseInOut(0, .6f, 1, 2.2f) : AnimationCurve.Linear(0, 1, 1, .6f));
            var colorLife = ps.colorOverLifetime; colorLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
            colorLife.color = g;
            var rot = ps.rotationOverLifetime; rot.enabled = billow; rot.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name };
            m.SetTexture("_BaseMap", texture); m.mainTexture = texture;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        // ---------- Shared helpers (also used by the collapse replay and elimination effects) ----------

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
            if (!mesh || !material || list.Count == 0) return;
            var rp = new RenderParams(material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            for (int start = 0; start < list.Count; start += 1023)
                Graphics.RenderMeshInstanced(rp, mesh, 0, list, Mathf.Min(1023, list.Count - start), start);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.Destruction
{
    // Records the tower and everyone on it. After a big collapse (or when a round ends)
    // it plays the last few seconds back in slow motion as a small hologram in front of the player.
    public sealed class CollapseReplay : MonoBehaviour
    {
        public static CollapseReplay Instance { get; private set; }

        [Header("Recording")]
        public float recordSeconds = 8;
        public float sampleRate = 15;
        [Header("Playback")]
        public float playbackSpeed = .4f;
        public float hologramSize = .9f;   // meters
        public float distance = 1.3f;
        public float spinSpeed = 12;       // degrees per second
        [Header("What counts as a collapse")]
        public int collapseBreaks = 8;
        public float collapseWindow = 3;
        public float movingFraction = .15f;
        public float settleDelay = 2.5f;
        public float cooldown = 30;

        const int MaxActors = 64;

        sealed class Frame
        {
            public Vector3[] position; public Quaternion[] rotation;
            public Vector3[] movers = new Vector3[MaxActors], flyers = new Vector3[MaxActors];
            public int moverCount, flyerCount;
            public Frame(int n) { position = new Vector3[n]; rotation = new Quaternion[n]; }
        }

        Frame[] frames = new Frame[0];
        Vector3[] sizes = new Vector3[0];
        int head, count;
        float nextSample, pendingAt = -1, readyAt;
        readonly Queue<float> breaks = new Queue<float>();
        IReadOnlyList<Rigidbody> source;
        Rigidbody first;
        int sourceCount;

        bool playing;
        Frame[] clip;
        float clipTime, clipLength, yaw, scale;
        Vector3 anchor, pivot;

        Mesh cube, capsule, sphere, disc;
        Material blockMaterial, moverMaterial, flyerMaterial, baseMaterial;
        readonly List<Matrix4x4> blockList = new List<Matrix4x4>(), moverList = new List<Matrix4x4>(), flyerList = new List<Matrix4x4>();

        public bool Playing => playing;

        void Awake()
        {
            Instance = this;
            cube = DestructionPresentation.PrimitiveMesh(PrimitiveType.Cube);
            capsule = DestructionPresentation.PrimitiveMesh(PrimitiveType.Capsule);
            sphere = DestructionPresentation.PrimitiveMesh(PrimitiveType.Sphere);
            disc = DestructionPresentation.PrimitiveMesh(PrimitiveType.Cylinder);
            blockMaterial = DestructionPresentation.MakeMaterial(new Color(.3f, .9f, 1, .6f), true);
            moverMaterial = DestructionPresentation.MakeMaterial(new Color(1, .9f, .2f, 1), false);
            flyerMaterial = DestructionPresentation.MakeMaterial(new Color(1, .3f, .9f, .8f), true);
            baseMaterial = DestructionPresentation.MakeMaterial(new Color(.3f, .9f, 1, .2f), true);
        }

        void OnEnable() { GameEvents.BlockBroken += OnBroken; GameEvents.RoundEnded += OnRoundEnded; }
        void OnDisable() { GameEvents.BlockBroken -= OnBroken; GameEvents.RoundEnded -= OnRoundEnded; }

        void OnBroken(GameObject block, Vector3 position, Vector3 velocity)
        {
            breaks.Enqueue(Time.time);
            while (breaks.Count > 0 && Time.time - breaks.Peek() > collapseWindow) breaks.Dequeue();
            if (breaks.Count >= collapseBreaks) Pending(settleDelay);
        }

        void OnRoundEnded() { readyAt = 0; Pending(1.5f); }

        void Pending(float delay) => pendingAt = Time.time + delay;

        // For dev keys and menus.
        public void PlayNow() { readyAt = 0; pendingAt = Time.time; }

        void Refresh()
        {
            var list = Services.World?.Blocks;
            int n = list?.Count ?? 0;
            if (list == source && n == sourceCount && (n == 0 || list[0] == first)) return;
            source = list; sourceCount = n; first = n > 0 ? list[0] : null;
            sizes = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var box = list[i] ? list[i].GetComponent<BoxCollider>() : null;
                sizes[i] = box ? Vector3.Scale(box.size, box.transform.lossyScale) : Vector3.one;
            }
            frames = new Frame[Mathf.Max(2, Mathf.CeilToInt(recordSeconds * sampleRate))];
            for (int i = 0; i < frames.Length; i++) frames[i] = new Frame(n);
            head = 0; count = 0;
        }

        void Update()
        {
            if (playing) { Advance(); return; }
            Refresh();
            if (sourceCount == 0) return;
            if (Time.time >= nextSample) Record();
            if (pendingAt >= 0 && Time.time >= pendingAt && Time.time >= readyAt) Begin();
        }

        void Record()
        {
            nextSample = Time.time + 1 / sampleRate;
            var f = frames[head];
            var previous = count > 0 ? frames[(head - 1 + frames.Length) % frames.Length] : null;
            int moving = 0, alive = 0;
            float step = 4.5f / sampleRate;
            for (int i = 0; i < sourceCount; i++)
            {
                var b = source[i];
                if (!b) continue;
                f.position[i] = b.position; f.rotation[i] = b.rotation;
                if (b.position.y < -4000) continue;
                alive++;
                if (previous != null && (previous.position[i] - b.position).sqrMagnitude > step * step) moving++;
            }
            f.moverCount = 0;
            foreach (var m in Tracked.Movers)
                if (m is Component c && c && f.moverCount < MaxActors) f.movers[f.moverCount++] = c.transform.position;
            f.flyerCount = 0;
            foreach (var m in Tracked.Flyers)
                if (m is Component c && c && f.flyerCount < MaxActors) f.flyers[f.flyerCount++] = c.transform.position;
            head = (head + 1) % frames.Length;
            count = Mathf.Min(count + 1, frames.Length);
            if (alive > 0 && moving > alive * movingFraction) Pending(settleDelay);
        }

        void Begin()
        {
            pendingAt = -1;
            var cam = Services.Session?.ActiveCamera;
            if (!cam) cam = Camera.main;
            if (!cam || count < 4) return;

            clip = new Frame[count];
            for (int k = 0; k < count; k++) clip[k] = frames[(head - count + k + frames.Length * 2) % frames.Length];
            clipLength = (count - 1) / sampleRate;
            clipTime = 0;

            var bounds = new Bounds();
            bool any = false;
            foreach (var f in clip)
                for (int i = 0; i < sourceCount; i++)
                {
                    var p = f.position[i];
                    if (p.y < -4000) continue;
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            if (!any) return;
            pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            scale = hologramSize / Mathf.Max(1, bounds.size.x, bounds.size.y, bounds.size.z);

            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            forward.Normalize();
            anchor = cam.transform.position + forward * distance + Vector3.down * .45f;
            yaw = Quaternion.LookRotation(-forward).eulerAngles.y;
            playing = true;
        }

        void Advance()
        {
            clipTime += Time.unscaledDeltaTime * playbackSpeed;
            yaw += spinSpeed * Time.unscaledDeltaTime;
            if (clipTime >= clipLength)
            {
                playing = false;
                readyAt = Time.time + cooldown;
                count = 0; head = 0;
                return;
            }
            float k = clipTime * sampleRate;
            int k0 = Mathf.Min(Mathf.FloorToInt(k), clip.Length - 1), k1 = Mathf.Min(k0 + 1, clip.Length - 1);
            float u = k - k0;
            Frame a = clip[k0], b = clip[k1];
            var world = Matrix4x4.TRS(anchor, Quaternion.Euler(0, yaw, 0), Vector3.one * scale) * Matrix4x4.Translate(-pivot);

            blockList.Clear(); moverList.Clear(); flyerList.Clear();
            int n = Mathf.Min(sourceCount, a.position.Length);
            for (int i = 0; i < n; i++)
            {
                if (a.position[i].y < -4000 || b.position[i].y < -4000) continue;
                var p = Vector3.Lerp(a.position[i], b.position[i], u);
                var r = Quaternion.Slerp(a.rotation[i], b.rotation[i], u);
                blockList.Add(world * Matrix4x4.TRS(p, r, sizes[i]));
            }
            for (int i = 0; i < a.moverCount; i++)
            {
                var p = i < b.moverCount ? Vector3.Lerp(a.movers[i], b.movers[i], u) : a.movers[i];
                moverList.Add(world * Matrix4x4.TRS(p + Vector3.up * .9f, Quaternion.identity, new Vector3(.6f, .9f, .6f)));
            }
            for (int i = 0; i < a.flyerCount; i++)
            {
                var p = i < b.flyerCount ? Vector3.Lerp(a.flyers[i], b.flyers[i], u) : a.flyers[i];
                flyerList.Add(world * Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * 3));
            }

            DestructionPresentation.Draw(cube, blockMaterial, blockList);
            DestructionPresentation.Draw(capsule, moverMaterial, moverList);
            DestructionPresentation.Draw(sphere, flyerMaterial, flyerList);
            Graphics.RenderMesh(new RenderParams(baseMaterial), disc,
                0, Matrix4x4.TRS(anchor, Quaternion.identity, new Vector3(hologramSize * 1.2f, .005f, hologramSize * 1.2f)));
        }
    }
}

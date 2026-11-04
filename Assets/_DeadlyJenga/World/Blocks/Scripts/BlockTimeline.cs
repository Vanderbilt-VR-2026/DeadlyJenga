using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Remembers where every block was for the last few seconds so the world can be rewound.
    // Only the host records and rewinds; other players see it through the normal block sync.
    public sealed class BlockTimeline : MonoBehaviour, IRewindable
    {
        public float historySeconds = 10;
        public float sampleEvery = .1f;
        public float playbackSpeed = 2;   // rewinding 4 seconds takes 2 seconds

        sealed class Frame
        {
            public Vector3[] position, velocity, spin;
            public Quaternion[] rotation;
            public Frame(int n) { position = new Vector3[n]; velocity = new Vector3[n]; spin = new Vector3[n]; rotation = new Quaternion[n]; }
        }

        Frame[] frames = new Frame[0];
        int head, count;
        float nextSample;
        IReadOnlyList<Rigidbody> source;
        Rigidbody first;
        int sourceCount;
        bool rewinding;

        public bool Rewinding => rewinding;
        public float Recorded => count * sampleEvery;

        void OnEnable() => Services.Rewind = this;
        void OnDisable() { if (ReferenceEquals(Services.Rewind, this)) Services.Rewind = null; }

        void FixedUpdate()
        {
            if (rewinding) return;
            var list = Services.World?.Blocks;
            if (list == null || list.Count == 0) { count = 0; return; }
            if (list != source || list.Count != sourceCount || list[0] != first) Reset(list);
            if (!Owner(list)) { count = 0; return; }
            if (Time.time < nextSample) return;
            nextSample = Time.time + sampleEvery;
            var f = frames[head];
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (!b) continue;
                f.position[i] = b.position; f.rotation[i] = b.rotation;
                f.velocity[i] = b.linearVelocity; f.spin[i] = b.angularVelocity;
            }
            head = (head + 1) % frames.Length;
            count = Mathf.Min(count + 1, frames.Length);
        }

        void Reset(IReadOnlyList<Rigidbody> list)
        {
            source = list; sourceCount = list.Count; first = list[0];
            frames = new Frame[Mathf.Max(2, Mathf.CeilToInt(historySeconds / sampleEvery))];
            for (int i = 0; i < frames.Length; i++) frames[i] = new Frame(sourceCount);
            head = 0; count = 0;
        }

        static bool Owner(IReadOnlyList<Rigidbody> list)
        {
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) return false;
            foreach (var b in list) if (b) return !b.isKinematic;
            return false;
        }

        public bool Rewind(float seconds)
        {
            if (rewinding || count < 2 || source == null || !Owner(source)) return false;
            StartCoroutine(Play(Mathf.Clamp(seconds, sampleEvery, Recorded)));
            return true;
        }

        IEnumerator Play(float seconds)
        {
            rewinding = true;
            GameEvents.RaiseRewindStarted(seconds);
            var bodies = new List<Rigidbody>(source);
            int len = frames.Length, newest = (head - 1 + len) % len;
            int steps = Mathf.Clamp(Mathf.RoundToInt(seconds / sampleEvery), 1, count - 1);
            foreach (var b in bodies) if (b) b.isKinematic = true;

            float duration = seconds / playbackSpeed, t = 0;
            while (t < duration)
            {
                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / duration) * steps;
                int k0 = Mathf.Min(Mathf.FloorToInt(k), steps), k1 = Mathf.Min(k0 + 1, steps);
                var later = frames[(newest - k0 + len * 4) % len];
                var earlier = frames[(newest - k1 + len * 4) % len];
                float u = k - k0;
                for (int i = 0; i < bodies.Count; i++)
                {
                    var b = bodies[i];
                    if (!b) continue;
                    var p = Vector3.Lerp(later.position[i], earlier.position[i], u);
                    // Never slide a block through the map on its way back from the graveyard.
                    if (CastleBlock.InGraveyard(later.position[i]) != CastleBlock.InGraveyard(earlier.position[i])) p = earlier.position[i];
                    b.MovePosition(p);
                    b.MoveRotation(Quaternion.Slerp(later.rotation[i], earlier.rotation[i], u));
                }
                yield return new WaitForFixedUpdate();
            }

            var target = frames[(newest - steps + len * 4) % len];
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                if (!b) continue;
                b.position = target.position[i]; b.rotation = target.rotation[i];
                b.isKinematic = false;
                b.linearVelocity = target.velocity[i]; b.angularVelocity = target.spin[i];
            }
            count = Mathf.Max(1, count - steps);
            head = (newest - steps + 1 + len * 4) % len;
            rewinding = false;
            GameEvents.RaiseRewindEnded();
        }
    }
}

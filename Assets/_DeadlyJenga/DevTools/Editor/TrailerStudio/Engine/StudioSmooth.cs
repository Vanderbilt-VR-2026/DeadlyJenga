using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Takes the twitch out of a machine-driven actor before anything else is layered on it.
    // A driven head moves in short machine-timed steps, and a hand recorded a physics tick apart from its head
    // jitters against it about twelve times a second. Both read as twitching. This filter replaces the recorded
    // head rotation and the two hands (kept in the head's own frame) with a short weighted average of the frames
    // around them, so quick moves keep their place and timing but lose their hard starts, stops and jitter.
    // It never moves the feet or the head's position (those come from the game's physics), never averages across
    // a hidden frame or a teleport, and never changes the take itself.
    // Switch: Set.alive.smooth (seconds; 0 = off) for driven actors, Set.alive.actors[].smooth for one actor.
    public sealed class StudioSmooth : IActorFrameFilter
    {
        public int Order => 2;   // first: the alive layer (5) and the overrides (10) build on the smoothed pose

        public bool Wants(StudioStage stage, StudioTrack track) => track.type == SurvivorTrack.Id && Seconds(stage.set, track.id) > 0;

        // How much smoothing this actor gets, in seconds (the width of the average; 0 = none).
        public static float Seconds(StudioSet set, string markId)
        {
            var alive = set?.alive;
            if (alive == null) return 0;
            foreach (var a in alive.actors) if (a.actor == markId && a.smooth >= 0) return Mathf.Min(a.smooth, .5f);
            bool driven = set.drives.Any(d => d.actor == markId && !d.muted && d.steps.Count > 0);
            return driven ? Mathf.Clamp(alive.smooth, 0, .5f) : 0;
        }

        sealed class Smoothed
        {
            public float seconds; public int count;
            public bool[] ok;
            public Quaternion[] head, leftRotation, rightRotation;   // hand rotations are in the head's frame
            public Vector3[] left, right;                            // hand positions in the head's frame
        }

        static readonly ConditionalWeakTable<StudioTake, Smoothed> cache = new ConditionalWeakTable<StudioTake, Smoothed>();

        public void Apply(StudioStage stage, StudioTrack track, float time, ActorFrame frame)
        {
            if (!frame.visible || frame.isHand) return;
            var take = track.ActiveTake;
            if (take == null || take.frames.Count == 0) return;
            int fps = stage.set.fps;
            var s = For(track, take, Seconds(stage.set, track.id), fps);
            // Between two frames (slow motion, sub-frames) the two smoothed frames are blended.
            float exact = Mathf.Clamp(time * fps - take.start, 0, take.frames.Count - 1);
            int a = Mathf.FloorToInt(exact), b = Mathf.Min(a + 1, take.frames.Count - 1);
            if (!s.ok[a]) return;
            float part = s.ok[b] ? exact - a : 0;
            var body = frame.body;
            Quaternion head = Quaternion.Slerp(s.head[a], s.head[b], part);
            body.headRotation = head;
            body.left.position = body.head + head * Vector3.Lerp(s.left[a], s.left[b], part);
            body.right.position = body.head + head * Vector3.Lerp(s.right[a], s.right[b], part);
            body.left.rotation = head * Quaternion.Slerp(s.leftRotation[a], s.leftRotation[b], part);
            body.right.rotation = head * Quaternion.Slerp(s.rightRotation[a], s.rightRotation[b], part);
            frame.body = body;
        }

        static Smoothed For(StudioTrack track, StudioTake take, float seconds, int fps)
        {
            int n = take.frames.Count;
            if (cache.TryGetValue(take, out var s) && s.count == n && Mathf.Approximately(s.seconds, seconds)) return s;
            var ok = new bool[n];
            var feet = new Vector3[n];
            var head = new Quaternion[n]; var lr = new Quaternion[n]; var rr = new Quaternion[n];
            var lp = new Vector3[n]; var rp = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var pose = ActorFrame.Read(track.type, take.frames[i]);
                ok[i] = pose.visible && !pose.isHand;
                if (!ok[i]) continue;
                var body = pose.body;
                Quaternion inverse = Quaternion.Inverse(body.headRotation);
                feet[i] = body.feet; head[i] = body.headRotation;
                lp[i] = inverse * (body.left.position - body.head); rp[i] = inverse * (body.right.position - body.head);
                lr[i] = inverse * body.left.rotation; rr[i] = inverse * body.right.rotation;
            }
            s = new Smoothed { seconds = seconds, count = n, ok = ok, head = new Quaternion[n], leftRotation = new Quaternion[n], rightRotation = new Quaternion[n], left = new Vector3[n], right = new Vector3[n] };
            float sigma = Mathf.Max(.01f, seconds * fps);
            int reach = Mathf.CeilToInt(sigma * 2.5f);
            for (int i = 0; i < n; i++)
            {
                if (!ok[i]) continue;
                // The frames either side that belong to the same stretch of movement: not past a hidden frame or a
                // teleport (a respawn moves the feet many metres in one frame).
                int from = i, to = i;
                while (from > 0 && i - from < reach && ok[from - 1] && Vector3.Distance(feet[from - 1], feet[from]) < 1.2f) from--;
                while (to < n - 1 && to - i < reach && ok[to + 1] && Vector3.Distance(feet[to + 1], feet[to]) < 1.2f) to++;
                // The same reach both ways, so a move is never dragged earlier or later near a cut in the take.
                int side = Mathf.Min(i - from, to - i);
                Vector4 h = default, l = default, r = default; Vector3 left = default, right = default; float total = 0;
                for (int j = i - side; j <= i + side; j++)
                {
                    float w = Mathf.Exp(-.5f * (j - i) * (j - i) / (sigma * sigma));
                    Add(ref h, head[j], head[i], w); Add(ref l, lr[j], lr[i], w); Add(ref r, rr[j], rr[i], w);
                    left += lp[j] * w; right += rp[j] * w; total += w;
                }
                s.head[i] = Unit(h, head[i]); s.leftRotation[i] = Unit(l, lr[i]); s.rightRotation[i] = Unit(r, rr[i]);
                s.left[i] = left / total; s.right[i] = right / total;
            }
            cache.Remove(take); cache.Add(take, s);
            return s;
        }

        // A weighted sum of rotations that are near each other (kept on the same side as 'like').
        static void Add(ref Vector4 sum, Quaternion q, Quaternion like, float weight)
        {
            if (Quaternion.Dot(q, like) < 0) weight = -weight;
            sum += new Vector4(q.x, q.y, q.z, q.w) * weight;
        }

        static Quaternion Unit(Vector4 sum, Quaternion fallback) =>
            sum.sqrMagnitude < 1e-8f ? fallback : new Quaternion(sum.x, sum.y, sum.z, sum.w).normalized;
    }
}

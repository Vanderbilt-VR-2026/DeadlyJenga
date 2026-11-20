using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Anything a camera can aim at or ride on, written as text in the Set:
    //   actor:<mark id>   the actor's feet (the wrist for a hand), read from the tape, so it works at any time
    //   head:<mark id>    a survivor's head
    //   body:<mark id>    the middle of what is drawn for that actor right now (good for the hand)
    //   block:<index>     a block, where it is shown right now
    //   point:x,y,z       a fixed point, metres from the stage spot
    public static class StudioTargets
    {
        static (string kind, string arg) Split(string spec)
        {
            int colon = spec.IndexOf(':');
            return colon < 0 ? ("actor", spec) : (spec.Substring(0, colon), spec.Substring(colon + 1));
        }

        // True when the target can be read at any tape time (not only the one on screen).
        public static bool Timeless(string spec)
        {
            var (kind, _) = Split(spec ?? "");
            return kind == "actor" || kind == "head" || kind == "point";
        }

        public static bool TryPoint(StudioStage stage, string spec, float tapeTime, out Vector3 point)
        {
            point = default;
            if (stage == null || string.IsNullOrEmpty(spec)) return false;
            var (kind, arg) = Split(spec);
            switch (kind)
            {
                case "actor":
                case "head":
                    var pose = StudioActorPoses.At(stage.tape, arg, tapeTime, stage.Fps);
                    if (!pose.found) return false;
                    point = kind == "head" ? pose.head : pose.feet;
                    return true;
                case "body":
                    var root = Object(stage, spec);
                    if (root && TryBounds(root, out var bounds)) { point = bounds.center; return true; }
                    return TryPoint(stage, "actor:" + arg, tapeTime, out point);
                case "block":
                    if (!int.TryParse(arg, out int index) || index < 0 || index >= stage.blocks.Length || !stage.blocks[index]) return false;
                    point = stage.blocks[index].transform.position;
                    return true;
                case "point":
                    var parts = arg.Split(',');
                    if (parts.Length != 3) return false;
                    float P(int i) => float.Parse(parts[i], CultureInfo.InvariantCulture);
                    point = stage.ToWorld(new Vector3(P(0), P(1), P(2)));
                    return true;
            }
            return false;
        }

        // The object behind a target (for framing metrics). Null for points.
        public static GameObject Object(StudioStage stage, string spec)
        {
            if (stage == null || string.IsNullOrEmpty(spec)) return null;
            var (kind, arg) = Split(spec);
            if (kind == "block") return int.TryParse(arg, out int i) && i >= 0 && i < stage.blocks.Length && stage.blocks[i] ? stage.blocks[i].gameObject : null;
            if (kind == "point") return null;
            return stage.playback.TryGetValue(arg, out var shown) && shown ? shown : stage.Live(arg);
        }

        // The box around everything drawn under an object (switched-off renderers left out).
        public static bool TryBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var shown in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)))
            {
                if (!any) { bounds = shown.bounds; any = true; }
                else bounds.Encapsulate(shown.bounds);
            }
            return any;
        }

        public static string Point(Vector3 stagePoint) =>
            string.Format(CultureInfo.InvariantCulture, "point:{0:0.###},{1:0.###},{2:0.###}", stagePoint.x, stagePoint.y, stagePoint.z);
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Soft lines on the floor, in the game world, so a performer can see where everyone else goes
    // and where their own previous take went.
    public static class StudioPaths
    {
        static readonly List<GameObject> lines = new List<GameObject>();
        static Material material;

        public static void Show(StudioStage stage, StudioPass pass)
        {
            Clear();
            if (!pass.paths || string.IsNullOrEmpty(pass.performer)) return;
            foreach (var mark in stage.set.marks)
            {
                var track = stage.tape.Find(mark.id);
                var take = track?.ActiveTake;
                if (take == null || track.mode == TrackMode.Muted) continue;
                var points = new List<Vector3>();
                for (int f = 0; f < take.frames.Count; f += 3)
                    if (ActorTrack.TryReadPosition(track.type, take.frames[f], out var p)) points.Add(p + Vector3.up * .04f);
                if (points.Count < 2) continue;
                bool own = mark.id == pass.performer;
                var color = StudioColors.For(mark.color);
                lines.Add(Line(mark.actor, points, new Color(color.r, color.g, color.b, own ? .9f : .45f), own ? .07f : .04f));
            }
        }

        static GameObject Line(string name, List<Vector3> points, Color color, float width)
        {
            if (!material) material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            var host = new GameObject("Studio path: " + name);
            var line = host.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return host;
        }

        public static void Clear()
        {
            foreach (var line in lines) if (line) Object.Destroy(line);
            lines.Clear();
        }
    }
}

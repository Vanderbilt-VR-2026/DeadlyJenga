using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt.Marina
{
    // Slides display pods around a rounded-rectangle loop at a steady speed, each pod facing outward.
    // The loop lies flat in this object's space: "deep" = half-length along local x, "along" = half-width
    // along local z, "corner" = the radius of its rounded corners.
    public sealed class MarinaPodTrack : MonoBehaviour
    {
        float speed;
        float length;
        float showFrom, showTo;
        readonly List<Transform[]> lamps = new();
        readonly List<Light[]> lights = new();
        readonly List<Transform> pods = new();
        readonly List<MarinaMesh> meshes = new();
        readonly List<Vector3> points = new();
        readonly List<float> distance = new();

        // lampsFrom/lampsTo: how far along local x a pod must be before its lamps start / finish popping up
        // (the front of the loop is the part inside the shop).
        public void Setup(float deep, float along, float corner, float metresPerSecond, float lampsFrom, float lampsTo)
        {
            speed = metresPerSecond;
            showFrom = lampsFrom;
            showTo = lampsTo;
            corner = Mathf.Clamp(corner, .05f, Mathf.Min(deep, along));
            float fx = deep - corner, fz = along - corner;
            // Walk the outline: each corner is a quarter circle; the straight parts join them.
            var centres = new[] { new Vector2(fx, fz), new Vector2(-fx, fz), new Vector2(-fx, -fz), new Vector2(fx, -fz) };
            points.Add(new Vector3(deep, 0, 0));
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= 16; i++)
                {
                    float t = (c * 90f + i * 90f / 16f) * Mathf.Deg2Rad;
                    points.Add(new Vector3(centres[c].x + Mathf.Cos(t) * corner, 0, centres[c].y + Mathf.Sin(t) * corner));
                }
            points.Add(new Vector3(deep, 0, 0));
            distance.Add(0);
            for (int i = 1; i < points.Count; i++) distance.Add(distance[i - 1] + Vector3.Distance(points[i - 1], points[i]));
            length = distance[distance.Count - 1];
        }

        public void Add(Transform pod, MarinaMesh mesh)
        {
            pods.Add(pod);
            meshes.Add(mesh);
            var podLamps = new Transform[3];
            var podLights = new Light[3];
            for (int i = 0; i < 3; i++)
            {
                float a = (i - 1) * 55f * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * .3f + Vector3.up * .09f;
                podLights[i] = MarinaFamePiston.PopLamp(pod, at, new Vector3(0, 1.25f, 0), .8f, 2.6f, meshes, out podLamps[i]);
            }
            lamps.Add(podLamps);
            lights.Add(podLights);
        }

        // Puts every pod at its spot on the loop for the given time.
        public void Place(float time)
        {
            for (int i = 0; i < pods.Count; i++)
            {
                float d = Mathf.Repeat(time * speed + i * length / pods.Count, length);
                int k = 1;
                while (k < points.Count - 1 && distance[k] < d) k++;
                float f = Mathf.InverseLerp(distance[k - 1], distance[k], d);
                Vector3 at = Vector3.Lerp(points[k - 1], points[k], f);
                Vector3 tangent = points[k] - points[k - 1];
                Vector3 outward = new Vector3(tangent.z, 0, -tangent.x);
                if (Vector3.Dot(outward, at) < 0) outward = -outward;
                pods[i].localPosition = at;
                pods[i].localRotation = Quaternion.LookRotation(outward);
                // Lamps rise and light up only while the pod is in the shop; out of sight they sink and switch off.
                float pop = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(showFrom, showTo, at.x));
                for (int j = 0; j < 3; j++)
                {
                    var p = lamps[i][j].localPosition;
                    lamps[i][j].localPosition = new Vector3(p.x, .09f - .1f * (1 - pop), p.z);
                    lights[i][j].enabled = pop > .01f;
                    lights[i][j].intensity = 1.6f * pop;
                }
            }
        }

        void Update() => Place(Time.time);

        void OnDestroy()
        {
            foreach (var mesh in meshes) mesh.Dispose();
        }
    }
}

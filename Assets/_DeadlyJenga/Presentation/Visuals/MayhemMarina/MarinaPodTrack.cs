using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt.Marina
{
    // Slides display pods around an oval loop at a steady speed, each pod facing outward.
    // The oval lies flat in this object's space: "deep" along local x, "along" along local z.
    public sealed class MarinaPodTrack : MonoBehaviour
    {
        const int Samples = 256;
        float speed;
        float length;
        readonly List<Transform> pods = new();
        readonly List<MarinaMesh> meshes = new();
        readonly Vector3[] points = new Vector3[Samples + 1];
        readonly float[] distance = new float[Samples + 1];

        public void Setup(float deep, float along, float metresPerSecond)
        {
            speed = metresPerSecond;
            // Sample the oval and measure how far along it each sample is, so pods move evenly.
            for (int i = 0; i <= Samples; i++)
            {
                float t = i * Mathf.PI * 2f / Samples;
                points[i] = new Vector3(Mathf.Cos(t) * deep, 0, Mathf.Sin(t) * along);
                distance[i] = i == 0 ? 0 : distance[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            length = distance[Samples];
        }

        public void Add(Transform pod, MarinaMesh mesh)
        {
            pods.Add(pod);
            meshes.Add(mesh);
        }

        // Puts every pod at its spot on the loop for the given time.
        public void Place(float time)
        {
            for (int i = 0; i < pods.Count; i++)
            {
                float d = Mathf.Repeat(time * speed + i * length / pods.Count, length);
                int k = 1;
                while (k < Samples && distance[k] < d) k++;
                float f = Mathf.InverseLerp(distance[k - 1], distance[k], d);
                Vector3 at = Vector3.Lerp(points[k - 1], points[k], f);
                Vector3 tangent = points[k] - points[k - 1];
                Vector3 outward = new Vector3(tangent.z, 0, -tangent.x);
                if (Vector3.Dot(outward, at) < 0) outward = -outward;
                pods[i].localPosition = at;
                pods[i].localRotation = Quaternion.LookRotation(outward);
            }
        }

        void Update() => Place(Time.time);

        void OnDestroy()
        {
            foreach (var mesh in meshes) mesh.Dispose();
        }
    }
}

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
        float showFrom, showTo;
        readonly List<Transform[]> lamps = new();
        readonly List<Light[]> lights = new();
        readonly List<Transform> pods = new();
        readonly List<MarinaMesh> meshes = new();
        readonly Vector3[] points = new Vector3[Samples + 1];
        readonly float[] distance = new float[Samples + 1];

        // showFrom/showTo: how far along local x a pod must be before its lamps start / finish popping up
        // (the near end of the oval is the part inside the shop).
        public void Setup(float deep, float along, float metresPerSecond, float lampsFrom, float lampsTo)
        {
            speed = metresPerSecond;
            showFrom = lampsFrom;
            showTo = lampsTo;
            // Sample the oval and measure how far along it each sample is, so pods move evenly.
            for (int i = 0; i <= Samples; i++)
            {
                float t = i * Mathf.PI * 2f / Samples;
                points[i] = new Vector3(Mathf.Cos(t) * deep, 0, Mathf.Sin(t) * along);
                distance[i] = i == 0 ? 0 : distance[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            length = distance[Samples];
        }

        // A pod plus three pop-up spotlights inside its glass, aimed up at the suit's chest.
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
                while (k < Samples && distance[k] < d) k++;
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

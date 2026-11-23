using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        // The same path distance drives every member of a party. Each body bobs on its own clock.
        sealed class ResidentCircuit
        {
            public Vector3[] points;
            public float[] distance;
            public float Length => distance[distance.Length - 1];

            public Vector3 At(float meters)
            {
                meters = Mathf.Repeat(meters, Length);
                int end = Array.BinarySearch(distance, meters);
                if (end >= 0) return points[end];
                end = ~end;
                return Vector3.Lerp(points[end - 1], points[end],
                    (meters - distance[end - 1]) / (distance[end] - distance[end - 1]));
            }
        }

        sealed class Resident
        {
            public Transform body;
            public CloudDepth shape;
            public Color mistTint;
            public ResidentCircuit circuit;
            public Quaternion alignment;
            public float start, side, phase, frequency, clearance;
        }

        readonly List<Resident> residents = new();
        readonly List<Material> residentPaints = new();

        ResidentCircuit MakeCircuit(int first, int count)
        {
            // Where the boulevard is interrupted, turn across the road and return in the opposite lane.
            int steps = Mathf.Max(32, count * 4);
            var path = new ResidentCircuit { points = new Vector3[steps + 1], distance = new float[steps + 1] };
            float sector = Mathf.PI * 2 / RoadBins;
            for (int i = 0; i < steps; i++)
            {
                float t = Mathf.PI * 2 * i / steps;
                float a = t, radius = roadRadius;
                if (count < RoadBins)
                {
                    float middle = (first + count * .5f) * sector;
                    float halfArc = count * .5f * sector - 2f / radius;
                    a = middle + Mathf.Cos(t) * halfArc;
                    radius += Mathf.Sin(t) * 2.45f;
                }
                path.points[i] = roadCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            }
            path.points[steps] = path.points[0];
            for (int i = 1; i <= steps; i++)
                path.distance[i] = path.distance[i - 1] + Vector3.Distance(path.points[i], path.points[i - 1]);
            return path;
        }

        List<ResidentCircuit> ResidentCircuits()
        {
            var paths = new List<ResidentCircuit>();
            int seam = Array.IndexOf(roadOpen, false);
            if (seam < 0) paths.Add(MakeCircuit(0, RoadBins));
            else
            {
                int run = 0;
                for (int i = 1; i <= RoadBins; i++)
                {
                    int sector = (seam + i) % RoadBins;
                    if (roadOpen[sector]) { run++; continue; }
                    if (run * roadRadius * 2 * Mathf.PI / RoadBins >= 18)
                        paths.Add(MakeCircuit(seam + i - run, run));
                    run = 0;
                }
            }
            return paths;
        }

        static int[] ResidentBag(int size, int varieties, System.Random random)
        {
            var bag = new int[size];
            for (int i = 0; i < size; i++)
            {
                int j = random.Next(i + 1);
                bag[i] = bag[j];
                bag[j] = i % varieties;
            }
            return bag;
        }

        void BuildCloudResidents()
        {
            var model = Resources.Load<GameObject>("FantasyMaps/CloudResidents");
            var shader = Resources.Load<Shader>("FantasyMaps/CloudResident");
            var paths = ResidentCircuits();
            if (!model || !shader || paths.Count == 0)
            {
                Debug.LogError("Cloud City could not build its resident circuits or load the resident assets.");
                return;
            }
            var meshes = model.GetComponentsInChildren<MeshFilter>(true);
            var origin = model.transform.Find("Origin");
            var front = model.transform.Find("Forward");
            var top = model.transform.Find("Up");
            if (meshes.Length != 6 || !origin || !front || !top)
            {
                Debug.LogError("Cloud residents require six body meshes and their three orientation markers.");
                return;
            }
            var shapes = new CloudDepth[meshes.Length];
            for (int i = 0; i < meshes.Length; i++) shapes[i] = new CloudDepth(meshes[i].sharedMesh, 36);
            var up = top.position - origin.position;
            var align = Quaternion.Inverse(Quaternion.LookRotation(front.position - origin.position, up));
            var palette = new[] { Color.white, new Color(1,.48f,.67f), new Color(.43f,.72f,1), new Color(1,.83f,.4f) };
            foreach (var tint in palette)
            {
                var paint = new Material(shader) { enableInstancing = true };
                paint.SetVector("_Tint", (Vector4)tint);
                residentPaints.Add(paint);
            }
            var root = new GameObject("Cloud residents");
            root.transform.SetParent(transform, false);
            ownedObjects.Add(root);
            var random = new System.Random();
            var builds = ResidentBag(25, meshes.Length, random);
            var colors = ResidentBag(25, palette.Length, random);
            int[] parties = { 5,1,1,2,1,4,1,1,2,1,1,2,1,1,1 };
            // Allocate whole parties by available road length, with regular headway on each circuit.
            var routes = new int[parties.Length];
            var loads = new int[paths.Count];
            for (int g = 0; g < parties.Length; g++)
            {
                int best = 0;
                for (int p = 1; p < paths.Count; p++)
                    if ((loads[p]+1) / paths[p].Length < (loads[best]+1) / paths[best].Length) best = p;
                routes[g] = best;
                loads[best]++;
            }
            var slots = new int[paths.Count];
            for (int g = 0; g < parties.Length; g++)
            {
                int route = routes[g];
                var path = paths[route];
                float start = path.Length * (slots[route]++ + .5f) / loads[route];
                var party = new GameObject("Party " + (g + 1) + " (" + parties[g] + ")");
                party.transform.SetParent(root.transform, false);
                for (int seat = 0; seat < parties[g]; seat++)
                {
                    int i = residents.Count;
                    var source = meshes[builds[i]];
                    var go = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(party.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = residentPaints[colors[i]];
                    var rotation = align * source.transform.rotation;
                    go.transform.localScale = source.transform.lossyScale * (.30f / up.magnitude);
                    go.transform.rotation = rotation;
                    float bottom = go.transform.position.y - renderer.bounds.min.y;
                    bool aloneInRow = seat % 2 == 0 && seat == parties[g] - 1;
                    residents.Add(new Resident { body = go.transform, circuit = path, alignment = rotation,
                        shape = shapes[builds[i]], mistTint = Color.Lerp(Color.white, palette[colors[i]], .32f),
                        start = start - (seat / 2) * 1.85f, side = aloneInRow ? 0 : (seat % 2 == 0 ? -.84f : .84f),
                        phase = (float)random.NextDouble() * 6.28f, frequency = 2.6f + (float)random.NextDouble(),
                        clearance = .55f + bottom });
                }
            }
            AnimateResidents(Time.time);
        }

        void AnimateResidents(float time)
        {
            foreach (var r in residents)
            {
                float distance = r.start + time * 1.55f;
                Vector3 pos = r.circuit.At(distance);
                Vector3 forward = (r.circuit.At(distance + .45f) - r.circuit.At(distance - .45f)).normalized;
                float wave = time * r.frequency + r.phase;
                pos += Vector3.Cross(Vector3.up, forward) * r.side + Vector3.up * (r.clearance + .095f * Mathf.Sin(wave));
                var rocking = Quaternion.Euler(2f * Mathf.Cos(wave), 0, 2.5f * Mathf.Sin(wave * .5f));
                r.body.SetPositionAndRotation(pos, Quaternion.LookRotation(forward) * rocking * r.alignment);
            }
        }

        void ClearResidents()
        {
            residents.Clear();
            foreach (var paint in residentPaints) Release(paint);
            residentPaints.Clear();
        }
    }
}

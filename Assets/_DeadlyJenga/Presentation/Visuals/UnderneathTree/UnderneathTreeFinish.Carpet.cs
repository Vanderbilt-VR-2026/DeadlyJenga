using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The tree-skirt carpet is the deadly floor. It should look cozy but hungry:
    // deep yarn loops, slow sinking swirls in the open floor, a swirl opening under anyone
    // who is caught sinking, and loose yarn loops sticking up around gifts, toys and the track.
    public sealed partial class UnderneathTreeFinish
    {
        const int MaxSwirls = 8, MaxTufts = 12000;
        const float TuftChunk = 16f;
        static readonly int SwirlsId = Shader.PropertyToID("_CarpetSwirls");
        static readonly int SwirlCountId = Shader.PropertyToID("_CarpetSwirlCount");
        readonly Vector4[] swirls = new Vector4[MaxSwirls];
        readonly List<Vector4> restingSwirls = new List<Vector4>();
        readonly Dictionary<IMovementState, float> sinkSwirls = new Dictionary<IMovementState, float>();
        GameObject rugBody;
        bool carpetBuilt;

        Material Carpet()
        {
            const string label = "Cranberry tufted tree skirt";
            if (finishes.TryGetValue(label, out var cached))
                return cached;
            var tint = new Color(1.5f, .58f, .7f);
            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Carpet");
            if (!shader || !shader.isSupported)
                return Fabric(label + " (plain)", "Cranberry Rug Weave", tint, new Vector2(12f, 12f), .12f, .65f);

            var weave = Resources.Load<Texture2D>(ResourcePath + "Cranberry Rug Weave");
            var material = new Material(shader) { name = label, color = tint };
            material.SetTexture("_BaseMap", weave);
            material.SetTextureScale("_BaseMap", new Vector2(12f, 12f));
            material.SetTexture("_DetailMap", weave);
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(ResourcePath + "Cranberry Rug Weave Normal"));
            var centre = art.TransformPoint(new Vector3(RugCentreX, 0f, RugCentreZ));
            material.SetColor("_Ember", new Color(1.1f, .28f, .07f));
            material.SetVector("_RugCircle", new Vector4(centre.x, centre.z, RugEdge, 0f));
            materials.Add(material);
            finishes.Add(label, material);
            return material;
        }

        void BuildCarpet()
        {
            if (carpetBuilt)
                return;
            foreach (var renderer in world.Root.GetComponentsInChildren<MeshRenderer>())
                if (renderer.name.Contains("kill floor") && renderer.GetComponent<Collider>())
                {
                    rugBody = renderer.gameObject;
                    break;
                }
            if (!rugBody)
                return;
            carpetBuilt = true;
            Physics.SyncTransforms();

            // Look at the carpet in a 3 m grid: open spots have bare carpet all around,
            // edge spots have a gift, toy or track right next to them.
            var centre = art.TransformPoint(new Vector3(RugCentreX, 0f, RugCentreZ));
            var open = new List<Vector3>();
            var edges = new List<Vector3>();
            const float cell = 3f;
            for (float x = -RugEdge; x <= RugEdge; x += cell)
                for (float z = -RugEdge; z <= RugEdge; z += cell)
                {
                    float ring = Mathf.Sqrt(x * x + z * z) / RugEdge;
                    var spot = new Vector3(centre.x + x, 0f, centre.z + z);
                    if (ring > .96f || !OnCarpet(spot, out _))
                        continue;
                    if (Clear(spot, 3f))
                    {
                        if (ring < .8f)
                            open.Add(spot);
                    }
                    else
                        edges.Add(spot);
                }
            PickRestingSwirls(open);
            BuildTufts(edges);
        }

        // True when the first thing straight below is the carpet itself.
        bool OnCarpet(Vector3 spot, out Vector3 ground)
        {
            ground = spot;
            var top = rugBody.GetComponent<Renderer>().bounds.max.y;
            if (!Physics.Raycast(new Vector3(spot.x, top + 60f, spot.z), Vector3.down, out var hit, 70f, ~0, QueryTriggerInteraction.Ignore) ||
                hit.collider.gameObject != rugBody)
                return false;
            ground = hit.point;
            return true;
        }

        bool Clear(Vector3 spot, float reach)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                if (!OnCarpet(spot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach, out _))
                    return false;
            }
            return true;
        }

        // A few hungry swirls in the open floor, spread apart, nearest the course first.
        void PickRestingSwirls(List<Vector3> open)
        {
            var random = new System.Random(1225);
            var course = new Vector3(-76f, 0f, -102f);
            open.Sort((a, b) => (a - course).sqrMagnitude.CompareTo((b - course).sqrMagnitude));
            var chosen = new List<Vector3>();
            foreach (var spot in open)
            {
                if (chosen.Count >= 5)
                    break;
                bool crowded = false;
                foreach (var other in chosen)
                    crowded |= (other - spot).sqrMagnitude < 22f * 22f;
                if (crowded || !Clear(spot, 6f) || !OnCarpet(spot, out var ground))
                    continue;
                chosen.Add(spot);
                float radius = 2.8f + (float)random.NextDouble() * 1.6f;
                restingSwirls.Add(new Vector4(ground.x, ground.z, radius, .9f));
                // Named marker so the look-dev camera can find it.
                var marker = new GameObject("Carpet swirl " + chosen.Count);
                marker.transform.SetParent(art, false);
                marker.transform.position = ground;
            }
        }

        void AnimateCarpet()
        {
            if (!carpetBuilt)
                return;
            int count = 0;
            // Anyone caught sinking gets a swirl opening under their feet. It closes slowly after.
            float top = rugBody ? rugBody.GetComponent<Renderer>().bounds.max.y : 0f;
            if (sinkSwirls.Count > 64)
                sinkSwirls.Clear();
            foreach (var mover in Tracked.Movers)
            {
                if (!(mover is Component body) || !body)
                    continue;
                float target = mover is ISandState sand && sand.InSand ? .55f + .45f * sand.SandDepth : 0f;
                sinkSwirls.TryGetValue(mover, out float now);
                now = Mathf.MoveTowards(now, target, Time.deltaTime * (target > now ? 1.5f : .5f));
                if (now <= 0f)
                {
                    sinkSwirls.Remove(mover);
                    continue;
                }
                sinkSwirls[mover] = now;
                var feet = body.transform.position;
                if (count < MaxSwirls && Mathf.Abs(feet.y - top) < 3f)
                    swirls[count++] = new Vector4(feet.x, feet.z, 1.2f + 2.2f * now, now);
            }
            // The resting swirls breathe slowly.
            for (int i = 0; i < restingSwirls.Count && count < MaxSwirls; i++)
            {
                var swirl = restingSwirls[i];
                swirl.w *= .8f + .2f * Mathf.Sin(Time.time * .5f + i * 1.9f);
                swirls[count++] = swirl;
            }
            Shader.SetGlobalVectorArray(SwirlsId, swirls);
            Shader.SetGlobalFloat(SwirlCountId, count);
        }

        void ClearCarpet()
        {
            Shader.SetGlobalFloat(SwirlCountId, 0f);
            sinkSwirls.Clear();
            restingSwirls.Clear();
        }

        // Loose yarn loops sticking up from the carpet next to gifts, toys and the track.
        // Decoration only: no colliders. Grouped in 16 m patches that hide when far away.
        void BuildTufts(List<Vector3> edges)
        {
            var yarn = Fabric("Cranberry carpet yarn loops", "Cranberry Rug Weave", new Color(1.6f, .62f, .74f),
                Vector2.one, .3f, .7f);
            var random = new System.Random(2512);
            var patches = new Dictionary<Vector2Int, TuftPatch>();
            int count = 0;
            foreach (var cellCentre in edges)
                for (int i = 0; i < 10 && count < MaxTufts; i++)
                {
                    var spot = cellCentre + new Vector3((float)random.NextDouble() * 3f - 1.5f, 0f,
                        (float)random.NextDouble() * 3f - 1.5f);
                    if (!OnCarpet(spot, out var ground))
                        continue;
                    var key = new Vector2Int(Mathf.FloorToInt(ground.x / TuftChunk), Mathf.FloorToInt(ground.z / TuftChunk));
                    if (!patches.TryGetValue(key, out var patch))
                        patches.Add(key, patch = new TuftPatch());
                    patch.Add(ground, random);
                    count++;
                }

            foreach (var patch in patches.Values)
            {
                var mesh = new Mesh { name = "Carpet yarn loops" };
                mesh.SetVertices(patch.vertices);
                mesh.SetNormals(patch.normals);
                mesh.SetUVs(0, patch.uv);
                mesh.SetTriangles(patch.triangles, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                var go = new GameObject("Carpet yarn loops");
                go.transform.SetParent(art, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = yarn;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.35f, new Renderer[] { renderer }) });
                lod.RecalculateBounds();
            }
        }

        // One patch of yarn loops. Each loop is a little arch of 3-sided tube (24 triangles).
        sealed class TuftPatch
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            const int Segments = 4, Sides = 3;

            public void Add(Vector3 ground, System.Random random)
            {
                float R() => (float)random.NextDouble();
                float half = .1f + R() * .08f;
                float height = .18f + R() * R() * .32f;
                float tube = .04f + R() * .02f;
                float yaw = R() * Mathf.PI * 2f;
                var across = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var outward = Vector3.Cross(across, Vector3.up);
                float lean = (R() - .5f) * .7f;
                float u0 = R(), v0 = R();
                int first = vertices.Count;
                for (int s = 0; s <= Segments; s++)
                {
                    float angle = Mathf.PI * s / Segments;
                    var line = ground + across * (half * Mathf.Cos(angle)) +
                        Vector3.up * (height * Mathf.Sin(angle) - .03f) + outward * (height * Mathf.Sin(angle) * lean);
                    var along = (across * (-half * Mathf.Sin(angle)) + Vector3.up * (height * Mathf.Cos(angle)) +
                        outward * (height * Mathf.Cos(angle) * lean)).normalized;
                    var side = Vector3.Cross(along, outward).normalized;
                    for (int k = 0; k <= Sides; k++)
                    {
                        float around = Mathf.PI * 2f * k / Sides;
                        var offset = outward * Mathf.Cos(around) + side * Mathf.Sin(around);
                        vertices.Add(line + offset * tube);
                        normals.Add(offset);
                        uv.Add(new Vector2(u0 + .35f * s / Segments, v0 + .06f * k / Sides));
                    }
                }
                for (int s = 0; s < Segments; s++)
                    for (int k = 0; k < Sides; k++)
                    {
                        int a = first + s * (Sides + 1) + k, b = a + 1, c = a + Sides + 1, d = c + 1;
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(c); triangles.Add(b); triangles.Add(d);
                    }
            }
        }
    }
}

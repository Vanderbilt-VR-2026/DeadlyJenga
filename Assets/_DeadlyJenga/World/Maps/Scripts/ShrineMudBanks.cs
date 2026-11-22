using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class ShrineMudBanks : MonoBehaviour
    {
        const int Samples = 64;
        readonly List<Mesh> owned = new List<Mesh>();

        public static void Build(Transform group, Material material)
        {
            var owner = group.gameObject.AddComponent<ShrineMudBanks>();
            var footprints = new Bounds[group.childCount];
            for (int i = 0; i < footprints.Length; i++)
            {
                var patch = group.GetChild(i);
                var size = patch.localScale;
                var right = patch.localRotation * Vector3.right * size.x;
                var forward = patch.localRotation * Vector3.forward * size.z;
                footprints[i] = new Bounds(patch.localPosition, new Vector3(
                    Mathf.Abs(right.x) + Mathf.Abs(forward.x), 4, Mathf.Abs(right.z) + Mathf.Abs(forward.z)));
            }
            for (int i = 0; i < footprints.Length; i++)
                owner.BuildBank(group.GetChild(i), material, footprints, i);
        }

        void BuildBank(Transform patch, Material material, Bounds[] footprints, int index)
        {
            Vector3 size = patch.localScale;
            patch.localScale = Vector3.one;
            int seed = Mathf.RoundToInt(patch.localPosition.x * 47 + patch.localPosition.z * 173);
            var rng = new System.Random(seed);
            float phase = (float)rng.NextDouble() * Mathf.PI * 2;
            var vertices = new List<Vector3> { Vector3.zero };
            var colors = new List<Color> { new Color(.28f,.22f,.16f).linear };
            var indices = new List<int>();
            var ridges = new Vector3[Samples];
            var heights = new float[Samples];
            var wet = new Color(.30f,.23f,.18f);
            var moss = new Color(.40f,.42f,.24f);
            for (int i = 0; i < Samples; i++)
            {
                // Clockwise rectangle in plan. Sampling every corner keeps the whole bearing area level.
                int side = i / (Samples / 4);
                float t = i % (Samples / 4) / (Samples / 4f);
                Vector3 edge = side switch
                {
                    0 => new Vector3(t-.5f,0,.5f),
                    1 => new Vector3(.5f,0,.5f-t),
                    2 => new Vector3(.5f-t,0,-.5f),
                    _ => new Vector3(-.5f,0,t-.5f)
                };
                var outward = edge.normalized;
                var foot = Vector3.Scale(edge,size);
                float a = i * (Mathf.PI * 2 / Samples);
                float lobe = .5f + .27f*Mathf.Sin(3*a+phase) + .16f*Mathf.Sin(5*a-phase) + .07f*Mathf.Cos(9*a+phase);
                float muddiness = Mathf.PerlinNoise(foot.x*.35f+phase+88,foot.z*.35f+phase+34);
                var ridge = foot + outward * (.36f + 1.9f*lobe);
                ridges[i] = ridge;
                heights[i] = -.07f + .43f*lobe + .10f*(muddiness-.5f);
                var toe = ridge + outward * (.6f + lobe);
                vertices.Add(foot);
                vertices.Add(ridge);
                vertices.Add(Vector3.Lerp(ridge,toe,.42f) + Vector3.down*.24f);
                vertices.Add(toe + Vector3.down*.88f);
                colors.Add(Color.Lerp(wet,moss,muddiness*.35f).linear);
                colors.Add(Color.Lerp(wet,moss,Mathf.Clamp01(lobe+muddiness-.55f)).linear);
                colors.Add(Color.Lerp(wet,new Color(.25f,.19f,.14f),muddiness).linear);
                colors.Add(new Color(.19f,.16f,.13f).linear);
            }

            // Keep the whole bank sector low where a neighbouring support could bear on it.
            for (int i = 0; i < Samples; i++)
            {
                int next = (i+1)%Samples, a = 1+i*4, b = 1+next*4;
                var sector = new Bounds(patch.localPosition + patch.localRotation*vertices[a],Vector3.zero);
                foreach (var vertex in new[] {vertices[a+3],vertices[b],vertices[b+3]})
                    sector.Encapsulate(patch.localPosition + patch.localRotation*vertex);
                for (int j = 0; j < footprints.Length; j++)
                    if (j != index && sector.Intersects(footprints[j]))
                        heights[i] = heights[next] = 0;
                indices.AddRange(new[] {0,a,b});
                for (int ring = 0; ring < 3; ring++)
                    indices.AddRange(new[] {a+ring,a+ring+1,b+ring+1,a+ring,b+ring+1,b+ring});
            }
            for (int i = 0; i < Samples; i++)
                vertices[2+i*4] = ridges[i] + Vector3.up*heights[i];

            Mesh Bake(string label)
            {
                var mesh = new Mesh {name = label};
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices,0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                owned.Add(mesh);
                return mesh;
            }
            patch.gameObject.AddComponent<MeshCollider>().sharedMesh = Bake("Mud bearing surface and shore");
            for (int i = 0; i < Samples; i++)
            {
                if (heights[i] < .10f || rng.NextDouble() > .55) continue;
                var root = vertices[2+i*4];
                for (int blade = 0; blade < 7; blade++)
                {
                    float angle = (float)rng.NextDouble()*2*Mathf.PI;
                    var lean = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var width = Vector3.Cross(lean,Vector3.up)*.026f;
                    var basePoint = root + lean*.05f - Vector3.up*.025f;
                    int k = vertices.Count;
                    vertices.Add(basePoint-width); vertices.Add(basePoint+width);
                    vertices.Add(basePoint+lean*.16f+Vector3.up*(.17f+.31f*(float)rng.NextDouble()));
                    colors.Add(new Color(.23f,.30f,.15f).linear); colors.Add(new Color(.30f,.36f,.18f).linear);
                    colors.Add(new Color(.61f,.59f,.33f).linear);
                    for (int back = 2; back >= 0; back--)
                    {
                        vertices.Add(vertices[k+back]);
                        colors.Add(colors[k+back]);
                    }
                    indices.AddRange(new[] {k,k+1,k+2,k+3,k+4,k+5});
                }
            }
            patch.gameObject.AddComponent<MeshFilter>().sharedMesh = Bake("Tidal mud and sedge");
            var renderer = patch.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor",Color.white);
            tint.SetFloat("_Gloss",.58f);
            tint.SetFloat("_Spec",.12f);
            tint.SetFloat("_Rim",.06f);
            renderer.SetPropertyBlock(tint);
        }

        void OnDestroy()
        {
            foreach (var mesh in owned)
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}

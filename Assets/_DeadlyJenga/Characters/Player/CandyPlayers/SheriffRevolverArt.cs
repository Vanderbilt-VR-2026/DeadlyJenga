using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.CandyPlayers
{
    public sealed class SheriffRevolverArt : MonoBehaviour
    {
        const string SurfacePrefix = "Detailed sheriff revolver surface ";
        static Material[] surfaces;
        static Mesh[] trimMeshes;
        static readonly Dictionary<Mesh, Mesh> forgedMeshes = new();
        Finish activeFinish;

        public sealed class Finish
        {
            internal readonly List<(MeshRenderer renderer, Material original, Material finish)> swaps = new();
            internal readonly List<(MeshFilter filter, Mesh original, Mesh finish)> meshes = new();
            internal GameObject trim;

            public void SetActive(bool active)
            {
                foreach (var (renderer, original, finish) in swaps)
                {
                    if (!renderer) continue;
                    if (finish) renderer.sharedMaterial = active ? finish : original;
                    else renderer.forceRenderingOff = active;
                }
                foreach (var (filter, original, finish) in meshes)
                    if (filter) filter.sharedMesh = active ? finish : original;
                if (trim) trim.SetActive(active);
            }
        }

        public static Finish Create(Transform root)
        {
            var holder = root.GetComponent<SheriffRevolverArt>();
            if (holder && holder.activeFinish != null) return holder.activeFinish;
            var finish = new Finish();
            var parts = root.GetComponentsInChildren<MeshRenderer>(true);
            var original = Array.Find(parts, renderer => renderer.name == SurfacePrefix + "0");
            if (!original || !original.sharedMaterial)
                throw new InvalidOperationException("The Sheriff Revolver exterior is missing.");

            EnsureAssets(original.sharedMaterial);
            foreach (var renderer in parts)
            {
                if (!renderer.name.StartsWith(SurfacePrefix, StringComparison.Ordinal) ||
                    !int.TryParse(renderer.name.Substring(SurfacePrefix.Length), out int index)) continue;
                var replacement = index switch
                {
                    0 => surfaces[0],
                    1 or 7 => surfaces[1],
                    2 or 8 => surfaces[2],
                    4 => surfaces[4],
                    6 => surfaces[1],
                    _ => renderer.sharedMaterial,
                };
                if (index == 3) replacement = null;
                finish.swaps.Add((renderer, renderer.sharedMaterial, replacement));
                if (index is 0 or 1 or 2 or 5)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh)
                        finish.meshes.Add((filter, filter.sharedMesh, Forge(filter.sharedMesh, index)));
                }
            }

            finish.trim = new GameObject("Sheriff revolver exterior detail");
            finish.trim.transform.SetParent(root, false);
            for (int i = 0; i < trimMeshes.Length; i++)
            {
                var part = new GameObject(trimMeshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(finish.trim.transform, false);
                part.GetComponent<MeshFilter>().sharedMesh = trimMeshes[i];
                part.GetComponent<MeshRenderer>().sharedMaterial = surfaces[i];
            }
            if (!holder) holder = root.gameObject.AddComponent<SheriffRevolverArt>();
            holder.activeFinish = finish;
            return finish;
        }

        static Mesh Forge(Mesh source, int surface)
        {
            if (forgedMeshes.TryGetValue(source, out var cached) && cached) return cached;
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " forged exterior";
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = vertices[i];
                if (surface is 0 or 1 && point.z > .025f && point.z < .239f &&
                    point.y < .198f && point.y > -.048f)
                {
                    float frame = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.025f, .087f, point.z));
                    float edge = Mathf.InverseLerp(.025f, .05f, Mathf.Abs(point.x));
                    point.x *= 1 - frame * edge * .17f;
                    if (point.y < -.017f) point.y += frame * edge * .008f;
                }
                vertices[i] = point;
            }
            mesh.vertices = vertices;
            var keep = new List<int>();
            var triangles = mesh.triangles;
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
                if (IsReplacedDetail(surface, vertices[a]) && IsReplacedDetail(surface, vertices[b]) &&
                    IsReplacedDetail(surface, vertices[c])) continue;
                keep.Add(a);
                keep.Add(b);
                keep.Add(c);
            }
            mesh.SetTriangles(keep, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            forgedMeshes[source] = mesh;
            return mesh;
        }

        static bool IsReplacedDetail(int surface, Vector3 point)
        {
            // The aim reference lives on its own Transform. Strip only the old rendered trim.
            bool overRail = point.z > .144f && point.z < .242f &&
                point.y > (surface == 0 ? .183f : surface == 5 ? .213f : .188f);
            if (overRail && surface is 0 or 1 or 5) return true;

            bool underFrame = point.y < .008f && point.y > -.112f &&
                point.z > .025f && point.z < .184f;
            return underFrame && (surface == 1 && Mathf.Abs(point.x) < .014f ||
                surface == 2 && Mathf.Abs(point.x) < .012f);
        }

        static void EnsureAssets(Material source)
        {
            if (surfaces != null && surfaces[0]) return;
            surfaces = new[] {
                FinishMaterial(source, "Draw-filed blued steel", .74f, .53f, Texture(false), Color.white),
                FinishMaterial(source, "Burnished satin steel", .67f, .56f, null, new Color(.50f,.49f,.46f)),
                FinishMaterial(source, "Matte black oxide", .62f, .36f, null, new Color(.055f,.069f,.081f)),
                FinishMaterial(source, "Oiled checkered walnut", 0, .44f, Texture(true), Color.white),
                FinishMaterial(source, "Aged brass inlay", .72f, .57f, null, new Color(.63f,.39f,.14f))
            };

            var pieces = new List<CombineInstance>[surfaces.Length];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = new List<CombineInstance>();

            void Add(int material, Mesh mesh) =>
                pieces[material].Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity });

            foreach (int side in new[] { -1, 1 })
            {
                Add(1, Panel("Sculpted trigger", side, 0, .014f, .002f, new[]
                {
                    new Vector2(-.029f,.076f), new Vector2(-.029f,.108f),
                    new Vector2(-.049f,.103f), new Vector2(-.068f,.109f),
                    new Vector2(-.083f,.123f), new Vector2(-.092f,.113f),
                    new Vector2(-.082f,.093f), new Vector2(-.059f,.082f)
                }));
                Add(3, Panel("Sculpted walnut", side, .033f, .055f, .006f, new[]
                {
                    new Vector2(-.162f,-.092f), new Vector2(-.174f,-.065f), new Vector2(-.169f,-.027f),
                    new Vector2(-.144f,-.008f), new Vector2(.010f,.030f), new Vector2(.036f,.012f),
                    new Vector2(.033f,-.014f), new Vector2(-.040f,-.052f)
                }, -.175f, .21f, -.104f, .147f));
                Add(0, Panel("Forged frame shoulder", side, .030f, .050f, .004f, new[]
                {
                    new Vector2(.090f,-.055f), new Vector2(.025f,-.032f), new Vector2(.002f,.009f),
                    new Vector2(.055f,.050f), new Vector2(.133f,.047f), new Vector2(.162f,.013f),
                    new Vector2(.155f,-.029f)
                }));
                Add(0, Panel("Faceted barrel jacket", side, .018f, .043f, .004f, new[]
                {
                    new Vector2(.102f,.246f), new Vector2(.089f,.276f), new Vector2(.090f,.435f),
                    new Vector2(.108f,.485f), new Vector2(.131f,.482f), new Vector2(.150f,.439f),
                    new Vector2(.155f,.282f), new Vector2(.143f,.247f)
                }));
                Add(2, Panel("Recessed flank", side, .040f, .0436f, .0007f, new[]
                {
                    new Vector2(.111f,.274f), new Vector2(.103f,.310f), new Vector2(.104f,.423f),
                    new Vector2(.116f,.463f), new Vector2(.124f,.451f), new Vector2(.133f,.300f),
                    new Vector2(.129f,.276f)
                }));
                Add(1, Strip(side,.0439f,.127f,.282f,.124f,.451f,.0024f));
                Add(4, Strip(side,.0441f,.107f,.320f,.109f,.391f,.0013f));
                Add(4, Strip(side,.0441f,.109f,.391f,.115f,.420f,.0013f));
                Add(4, GripBadge(side));
                Add(4, Strip(side,.051f,.074f,-.037f,.143f,-.024f,.0023f));
                Add(4, Strip(side,.051f,.143f,-.024f,.151f,.013f,.0023f));
            }

            trimMeshes = new Mesh[pieces.Length];
            string[] names = { "Blued forged contours", "Bright edge flats", "Recessed barrel flats", "Oiled walnut stocks", "Brass detail lines" };
            for (int i = 0; i < trimMeshes.Length; i++)
            {
                var mesh = new Mesh { name = names[i] };
                mesh.CombineMeshes(pieces[i].ToArray(), true, true);
                mesh.RecalculateBounds();
                foreach (var piece in pieces[i]) Object.Destroy(piece.mesh);
                trimMeshes[i] = mesh;
            }
        }

        static Material FinishMaterial(Material source, string name, float metal, float smoothness,
            Texture2D texture, Color color)
        {
            var material = new Material(source) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_BumpScale", 0);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_EMISSION");
            return material;
        }

        static Texture2D Texture(bool walnut)
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = walnut ? "Walnut grain and diamond checkering" : "Directional bluing and fine handling wear",
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size;
                float v = (y + .5f) / size;
                float haze = Mathf.PerlinNoise(u * 11 + 5, v * 13 + 8);
                float strands = Mathf.PerlinNoise(u * (walnut ? 5 : 14) + 9, v * (walnut ? 83 : 205) + 4);
                Color color;
                if (walnut)
                {
                    float rings = Mathf.Sin((v + .045f * haze + .013f * Mathf.Sin(u * 10)) * 120) * .5f + .5f;
                    color = Color.Lerp(new Color(.19f,.071f,.028f), new Color(.62f,.321f,.128f),
                        .15f + .38f * haze + .30f * rings + .14f * strands);
                    float edge = Mathf.Pow(Mathf.Abs((u - .53f) / .36f), 4) + Mathf.Pow(Mathf.Abs((v - .48f) / .34f), 4);
                    if (edge < 1)
                    {
                        float a = Mathf.Abs(Mathf.Repeat(32 * (u + v), 1) - .5f);
                        float b = Mathf.Abs(Mathf.Repeat(32 * (u - v), 1) - .5f);
                        color *= .58f + .52f * Mathf.SmoothStep(0, 1, Mathf.Min(a, b) * 4.6f);
                    }
                }
                else
                {
                    color = Color.Lerp(new Color(.149f,.181f,.205f), new Color(.315f,.351f,.367f),
                        .15f + .26f * haze + .43f * strands);
                    float wear = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.76f, .96f, strands));
                    color = Color.Lerp(color, new Color(.48f,.49f,.51f), wear * .24f);
                }
                color.a = 1;
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static Mesh GripBadge(int side)
        {
            var outline = new Vector2[10];
            for (int i = 0; i < outline.Length; i++)
            {
                float angle = i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? .013f : .006f;
                outline[i] = new Vector2(-.071f + Mathf.Cos(angle) * radius,
                    -.046f + Mathf.Sin(angle) * radius);
            }
            return Panel("Inset sheriff star", side, .053f, .056f, .001f, outline);
        }

        static Mesh Strip(int side, float x, float y0, float z0, float y1, float z1, float width)
        {
            var a = new Vector2(y0,z0);
            var b = new Vector2(y1,z1);
            var n = new Vector2(z0-z1,y1-y0).normalized * (width * .5f);
            return Panel("Decorative edge line", side, x - .001f, x, .00025f,
                new[] { a + n, a - n, b - n, b + n });
        }

        static Mesh Panel(string name, int side, float back, float face, float bevel,
            Vector2[] outline, float minY = -.18f, float spanY = .4f, float minZ = -.10f, float spanZ = .65f)
        {
            var center = Vector2.zero;
            float signedArea = 0;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                center += a;
                signedArea += a.x * b.y - a.y * b.x;
            }
            if (signedArea * side < 0) Array.Reverse(outline);
            center /= outline.Length;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            Vector2 Inner(Vector2 point) => Vector2.MoveTowards(point, center, bevel);
            Vector3 Point(float x, Vector2 point) => new Vector3(x * side, point.x, point.y);
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                foreach (var vertex in new[] { a, b, c })
                {
                    indices.Add(vertices.Count);
                    vertices.Add(vertex);
                    uvs.Add(new Vector2((vertex.y-minY)/spanY,(vertex.z-minZ)/spanZ));
                }
            }
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                var topA = Point(face, Inner(a));
                var topB = Point(face, Inner(b));
                var shoulderA = Point(face - bevel, a);
                var shoulderB = Point(face - bevel, b);
                var backA = Point(back, a);
                var backB = Point(back, b);
                Face(Point(face, center), topA, topB);
                Face(topA, shoulderA, shoulderB);
                Face(topA, shoulderB, topB);
                Face(shoulderA, backA, backB);
                Face(shoulderA, backB, shoulderB);
            }
            var result = new Mesh { name = name };
            result.SetVertices(vertices);
            result.SetUVs(0, uvs);
            result.SetTriangles(indices, 0);
            result.RecalculateNormals();
            return result;
        }
    }
}


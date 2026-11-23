using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed class TwinCalderaScenery : MonoBehaviour
    {
        public Material lava, sky, skyline;
        Mesh surface, mountains;
        const int Sides = 64;

        void Awake()
        {
            const float size = 1600;
            surface = new Mesh { name = "Lava surface" };
            surface.vertices = new[] { new Vector3(-size,0,-size), new Vector3(-size,0,size),
                new Vector3(size,0,size), new Vector3(size,0,-size) };
            surface.triangles = new[] { 0,1,2,0,2,3 };
            Show("Lava ocean", surface, lava);

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            void Ring(Vector3 center, float radius, float height, float angle, float band, float seed, bool connect)
            {
                int start = vertices.Count;
                for (int i = 0; i <= Sides; i++)
                {
                    float a = i * (2 * Mathf.PI / Sides);
                    float r = radius * (1 + .12f*Mathf.Sin(3*a+seed) + .055f*Mathf.Cos(7*a-seed)
                        + .045f*Mathf.Sin(11*a+band));
                    float x = Mathf.Cos(a)*r, z = Mathf.Sin(a)*r*.82f;
                    vertices.Add(center + new Vector3(x*Mathf.Cos(angle)-z*Mathf.Sin(angle),
                        height + radius*.048f*Mathf.Cos(5*a+seed), x*Mathf.Sin(angle)+z*Mathf.Cos(angle)));
                    uv.Add(new Vector2((float)i/Sides, band));
                    if (!connect || i == 0) continue;
                    int k = start+i, prev = k-Sides-1;
                    triangles.Add(prev-1); triangles.Add(k-1); triangles.Add(k);
                    triangles.Add(prev-1); triangles.Add(k); triangles.Add(prev);
                }
            }
            void Peak(float bearing, float range, float width, float height, float yaw, float seed, bool smoking)
            {
                float angle = yaw*Mathf.Deg2Rad;
                Vector3 center = Quaternion.Euler(0,bearing,0)*Vector3.forward*range;
                Vector3 lean = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width*.13f;
                for (int l=0; l<=8; l++)
                {
                    float t=l/8f;
                    Ring(center+lean*t, width*(.077f+.923f*Mathf.Pow(1-t,1.64f)), height*t-3, angle,t,seed,l>0);
                }
                Ring(center+lean,0,height-5,angle,1.02f,seed,true);
                if (!smoking) return;
                for (int n=0; n<7; n++)
                {
                    float t=n/6f, r=height*(.068f+.13f*t);
                    Vector3 puff=center+lean+new Vector3(height*(.23f*t*t+.04f*Mathf.Sin(n*1.9f)),
                        height*(1.02f+.46f*t)-3, height*.045f*Mathf.Cos(n));
                    for (int l=0;l<=6;l++)
                    {
                        float lat=l*Mathf.PI/6;
                        Ring(puff,r*Mathf.Sin(lat),-r*.87f*Mathf.Cos(lat),angle,
                            2+t+.019f*l,seed+n,l>0);
                    }
                }
            }
            Peak(6,268,77,47,19,1,true);
            Peak(-37,202,49,29,113,3,true);
            Peak(46,356,103,37,-32,6,false);
            Peak(88,222,56,39,62,5,true);
            Peak(148,323,81,32,127,8,false);
            Peak(-148,239,64,44,-58,11,true);
            Peak(-90,381,110,39,8,9,false);
            mountains = new Mesh { name = "Caldera distant volcanoes" };
            mountains.SetVertices(vertices);
            mountains.SetUVs(0,uv);
            mountains.SetTriangles(triangles,0);
            mountains.RecalculateNormals();
            Show("Volcanic skyline", mountains, skyline);
        }

        void Show(string label, Mesh mesh, Material material)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform,false);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        void OnDestroy()
        {
            if (surface) Destroy(surface);
            if (mountains) Destroy(mountains);
        }
    }
}

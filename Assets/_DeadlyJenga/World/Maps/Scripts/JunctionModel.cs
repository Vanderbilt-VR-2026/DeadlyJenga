using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Each authored compound becomes one mesh and one independently simulated rigid body.
    internal sealed class JunctionModel
    {
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<int> triangles = new();
        readonly GameObject root;
        float volume,trackLength,startMiter,endMiter;
        internal void SetTrackMiters(float length,float start,float end)
        {
            trackLength=length; startMiter=start; endMiter=end;
        }
        Vector3 CutToTrackJoint(Vector3 vertex)
        {
            if(trackLength>0)
                vertex.z+=vertex.x*Mathf.Lerp(startMiter,endMiter,.5f+vertex.z/trackLength);
            return vertex;
        }
        internal JunctionModel(GameObject root) => this.root = root;
        internal void Shape(Vector3 p, Vector3 size, Color tint, int sides = 4, float top = 1,
            Quaternion rotation = default, bool solid = true, float bevel = .07f)
        {
            if (rotation == default) rotation = Quaternion.identity;
            int firstVertex=vertices.Count;
            var ring = new Vector3[4, sides];
            float inset = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.z,size.y)) * .18f);
            for (int j = 0; j < 4; j++)
            {
                bool upper = j >= 2, edge = j == 0 || j == 3;
                float y = upper ? size.y * .5f : -size.y * .5f;
                if (!edge) y += upper ? -inset : inset;
                float sx = size.x * .5f * (upper ? top : 1), sz = size.z * .5f * (upper ? top : 1);
                if (edge) { sx = Mathf.Max(.001f,sx-inset); sz = Mathf.Max(.001f,sz-inset); }
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + .5f) * 2 * Mathf.PI / sides;
                    float r = sides == 4 ? 1.41421356f : 1;
                    ring[j,i] = CutToTrackJoint(p + rotation * new Vector3(r*sx*Mathf.Sin(a), y, r*sz*Mathf.Cos(a)));
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int k = (i + 1) % sides;
                for (int j = 0; j < 3; j++)
                    Quad(ring[j,i],ring[j,k],ring[j+1,k],ring[j+1,i],tint);
                Tri(CutToTrackJoint(p+rotation*(Vector3.up*size.y*.5f)),ring[3,i],ring[3,k],tint);
                Tri(CutToTrackJoint(p-rotation*(Vector3.up*size.y*.5f)),ring[0,k],ring[0,i],tint);
            }
            if (!solid) return;
            if (trackLength>0 || sides!=4 || !Mathf.Approximately(top,1))
            {
                int count=vertices.Count-firstVertex;
                var hull=new Mesh {name=root.name+" bearing hull"};
                hull.SetVertices(vertices.GetRange(firstVertex,count));
                var indices=new int[count];
                for(int i=0;i<count;i++) indices[i]=i;
                hull.SetTriangles(indices,0);
                var convex=root.AddComponent<MeshCollider>();
                convex.convex=true;
                convex.sharedMesh=hull;
                convex.includeLayers=1<<DeadlyJenga.Core.Layers.Survivor;
                convex.layerOverridePriority=12;
                volume+=size.x*size.y*size.z;
                return;
            }
            var owner = root;
            if (rotation != Quaternion.identity)
            {
                owner = new GameObject("Angled bearing");
                owner.layer = root.layer;
                owner.transform.SetParent(root.transform,false);
                owner.transform.SetLocalPositionAndRotation(p,rotation);
                p = Vector3.zero;
            }
            var box = owner.AddComponent<BoxCollider>();
            box.center = p;
            box.size = size;
            box.includeLayers = 1 << DeadlyJenga.Core.Layers.Survivor;
            box.layerOverridePriority = 12;
            volume += size.x*size.y*size.z;
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Tri(a,b,c,color); Tri(a,c,d,color);
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c, Color tint)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(tint); colors.Add(tint); colors.Add(tint);
            triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2);
        }
        internal Rigidbody Finish(Material material)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform,false);
            var mesh = new Mesh { name = root.name };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            var body = root.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(volume*2, 4, 115);
            body.solverIterations = 18;
            body.solverVelocityIterations = 5;
            root.AddComponent<JunctionMember>().Setup(body, visual.transform);
            return body;
        }
    }
}

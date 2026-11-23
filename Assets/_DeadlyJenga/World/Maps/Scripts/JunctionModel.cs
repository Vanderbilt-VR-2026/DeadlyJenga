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
        internal void BearingBox(Vector3 center, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.includeLayers = 1 << DeadlyJenga.Core.Layers.Survivor;
            box.layerOverridePriority = 12;
            volume += size.x * size.y * size.z;
        }
        internal void AddBakedPart(JunctionStructure.Part part)
        {
            int first = vertices.Count;
            var points = new Vector3[part.Points.Length];
            var fromMap = Quaternion.Inverse(root.transform.localRotation);
            var origin = root.transform.localPosition;
            for (int i = 0; i < points.Length; i++)
                points[i] = fromMap * (part.Points[i] - origin);
            for (int i = 0; i < part.Triangles.Length; i++)
            {
                var face = part.Triangles[i];
                Tri(points[face.A], points[face.B], points[face.C], face.Color);
            }
            if (!part.Solid) return;
            ConvexHull(first);
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            var size = bounds.size;
            volume += size.x * size.y * size.z * .45f;
        }
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
                ConvexHull(firstVertex);
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
        void ConvexHull(int firstVertex)
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
        }

        // Sections run from chin to crown. Each row gives the two radii, elevation and fore/aft offset.
        internal void Carving(Vector3 origin,Color color,params Vector4[] sections)
        {
            const int sides=16;
            int first=vertices.Count;
            var rings=new Vector3[sections.Length,sides];
            for(int row=0;row<sections.Length;row++)
            {
                var section=sections[row];
                for(int i=0;i<sides;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/sides;
                    rings[row,i]=origin+new Vector3(section.x*Mathf.Sin(a),section.z,section.w+section.y*Mathf.Cos(a));
                }
            }
            int last=sections.Length-1;
            for(int i=0;i<sides;i++)
            {
                int next=(i+1)%sides;
                for(int row=0;row<last;row++)
                    Quad(rings[row,i],rings[row,next],rings[row+1,next],rings[row+1,i],color);
                Tri(origin+new Vector3(0,sections[0].z,sections[0].w),rings[0,next],rings[0,i],color);
                Tri(origin+new Vector3(0,sections[last].z,sections[last].w),rings[last,i],rings[last,next],color);
            }
            ConvexHull(first);
            volume+=sections[last/2].x*sections[last/2].y*(sections[last].z-sections[0].z)*Mathf.PI;
        }
        internal void DrapedBand(Vector3[] lower,Vector3[] upper,Color color)
        {
            int first=vertices.Count;
            for(int i=0;i<lower.Length;i++)
            {
                int next=(i+1)%lower.Length;
                Quad(lower[i],lower[next],upper[next],upper[i],color);
                if(i>0 && next!=0)
                {
                    Tri(lower[0],lower[next],lower[i],color);
                    Tri(upper[0],upper[i],upper[next],color);
                }
            }
            float sixVolumes=0;
            for(int i=first;i<vertices.Count;i+=3)
                sixVolumes+=Vector3.Dot(vertices[i],Vector3.Cross(vertices[i+1],vertices[i+2]));
            volume+=Mathf.Abs(sixVolumes)/6;
            ConvexHull(first);
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
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

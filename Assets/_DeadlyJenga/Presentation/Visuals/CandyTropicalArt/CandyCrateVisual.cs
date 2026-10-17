using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Visual-only geometry. Shared per size/style, with no physics components or per-frame allocations.
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CandyCrateVisual : MonoBehaviour
    {
        public CandyArtProfile profile;
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        MeshFilter filter;
        MeshRenderer meshRenderer;
        string lastKey;
        Vector3 lastSize;
        float lastBevel,lastDepth;
        bool lastButtons;
        int lastDetail = -1;
        float nextRefresh;
        static Camera view;
        static float nextCameraCheck;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetView() { view = null; nextCameraCheck = 0; }
        void OnEnable() { Refresh(); if (Application.isPlaying) CandyCrateBatchRenderer.Register(this); }
        void OnDisable() { CandyCrateBatchRenderer.Unregister(this); if(meshRenderer)meshRenderer.forceRenderingOff=false; }
        internal Mesh Mesh => filter ? filter.sharedMesh : null;
        internal MeshRenderer Renderer => meshRenderer ? meshRenderer : meshRenderer=GetComponent<MeshRenderer>();
        internal bool Distant => lastDetail > 0;
        void Update()
        {
            if (Application.isPlaying && Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .22f + (GetInstanceID() & 31) * .004f;
            Refresh();
        }
        int Detail(Vector3 size)
        {
            if (!Application.isPlaying) return 0;
            if (Time.unscaledTime >= nextCameraCheck)
            { view = Camera.main; nextCameraCheck = Time.unscaledTime + .25f; }
            if (!view || !view.isActiveAndEnabled) return 0;
            float distance = Vector3.Distance(view.transform.position, transform.position);
            // Match detail to projected size, including unusually scaled blocks and narrower views.
            distance *= Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f) / .57735f;
            distance /= Mathf.Max(1, size.magnitude / 6.63325f);
            float near = lastDetail == 0 ? 34 : 28, far = lastDetail == 2 ? 80 : 95;
            return distance < near ? 0 : distance < far ? 1 : 2;
        }
        public void Refresh()
        {
            if (!profile) return;
            if (!filter) filter = GetComponent<MeshFilter>();
            Vector3 size = transform.lossyScale;
            size = new Vector3(Mathf.Max(.01f,Mathf.Abs(size.x)),Mathf.Max(.01f,Mathf.Abs(size.y)),Mathf.Max(.01f,Mathf.Abs(size.z)));
            int detail = Detail(size);
            if(size==lastSize && detail==lastDetail && profile.bevel==lastBevel && profile.panelDepth==lastDepth && profile.cornerButtons==lastButtons && filter.sharedMesh)return;
            string key = size.ToString("F3") + ":" + profile.bevel.ToString("F3") + ":" + profile.panelDepth.ToString("F3") + ":" + profile.cornerButtons + ":" + detail;
            if (key == lastKey && filter.sharedMesh) return;
            if (!Cache.TryGetValue(key,out var mesh) || !mesh)
            {
                mesh = Build(size,profile,detail); mesh.hideFlags = HideFlags.HideAndDontSave; Cache[key] = mesh;
            }
            filter.sharedMesh = mesh; lastKey = key;lastSize=size;lastBevel=profile.bevel;lastDepth=profile.panelDepth;lastButtons=profile.cornerButtons;lastDetail=detail;
        }
        static Mesh Build(Vector3 size,CandyArtProfile p,int detail)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            float smallest = Mathf.Min(size.x,Mathf.Min(size.y,size.z));
            float r = Mathf.Min(p.bevel,smallest*.15f), depth = Mathf.Min(p.panelDepth,smallest*.025f);
            void Box(Vector3 center,Vector3 dimensions,float radius,int resolution)
            {
                Vector3 h = dimensions*.5f; radius = Mathf.Min(radius,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f);
                Vector3 inner = h-Vector3.one*radius;
                for(int face=0;face<6;face++)
                {
                    int axis=face/2; float sign=face%2==0?1:-1;
                    Vector3 normal=Vector3.zero;normal[axis]=sign;
                    Vector3 u=axis==0?Vector3.forward:Vector3.right;
                    Vector3 v=Vector3.Cross(normal,u);
                    float hu=Vector3.Dot(h,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));
                    float hv=Vector3.Dot(h,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
                    float Sample(int i,float extent)
                    {
                        if(resolution==2) return i==0?-extent:extent;
                        if(resolution==4) return i==0?-extent:i==1?-extent+radius:i==2?extent-radius:extent;
                        float[] values={-extent,-extent+radius*.293f,-extent+radius,extent-radius,extent-radius*.293f,extent};
                        return values[i];
                    }
                    int start=vertices.Count;
                    for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
                    {
                        Vector3 point=normal*h[axis]+u*Sample(x,hu)+v*Sample(y,hv);
                        Vector3 clamped=new Vector3(Mathf.Clamp(point.x,-inner.x,inner.x),Mathf.Clamp(point.y,-inner.y,inner.y),Mathf.Clamp(point.z,-inner.z,inner.z));
                        Vector3 n=(point-clamped).normalized;
                        Vector3 rounded=center+clamped+n*radius;
                        vertices.Add(new Vector3(rounded.x/size.x,rounded.y/size.y,rounded.z/size.z));
                        normals.Add(Vector3.Scale(n,size).normalized);
                        uv.Add(new Vector2((float)x/(resolution-1),(float)y/(resolution-1)));
                    }
                    for(int y=0;y<resolution-1;y++)for(int x=0;x<resolution-1;x++)
                    {int a=start+y*resolution+x,b=a+1,c=a+resolution,d=c+1; triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);}
                }
            }
            Box(Vector3.zero,size-Vector3.one*(detail==0?depth*2+.012f:.012f),r,detail==2?4:6);
            // Shallow molded panels on every face. All details remain inside the unit collider bounds.
            if(detail==0)for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                Vector3 dimensions=size-Vector3.one*(r*2.6f); dimensions[axis]=depth+.018f;
                Vector3 center=Vector3.zero;center[axis]=sign*(size[axis]*.5f-dimensions[axis]*.5f-.006f);
                Box(center,dimensions,Mathf.Min(r*.45f,dimensions[axis]*.4f),6);
            }
            if(detail==0 && p.cornerButtons)
            {
                float button=Mathf.Min(.17f,smallest*.09f);
                for(int face=0;face<6;face++)
                {
                    int axis=face/2,a=(axis+1)%3,b=(axis+2)%3;int sign=face%2==0?1:-1;
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
                    {
                        Vector3 center=Vector3.zero,dimensions=Vector3.one*button;
                        dimensions[axis]=.04f;
                        center[axis]=sign*(size[axis]*.5f-.024f);
                        center[a]=x*(size[a]*.5f-r*1.7f);center[b]=y*(size[b]*.5f-r*1.7f);
                        Box(center,dimensions,.017f,2);
                    }
                }
            }
            var result=new Mesh {name="Candy crate "+size.ToString("F2")};
            result.SetVertices(vertices);result.SetNormals(normals);result.SetUVs(0,uv);result.SetTriangles(triangles,0);result.RecalculateBounds();
            return result;
        }
    }
}

using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyMysteryTrails:MonoBehaviour
    {
        sealed class Batch
        {
            public Mesh mesh;
            public Transform root;
            public Renderer renderer;
            public readonly List<Vector3> vertices=new List<Vector3>();
            public readonly List<Vector3> normals=new List<Vector3>();
            public readonly List<int> indices=new List<int>();
            public int prints;
        }
        readonly Dictionary<string,Batch> open=new Dictionary<string,Batch>();
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        readonly List<Batch> batches=new List<Batch>();
        int seen;MysteryTrailFrame first;
        float nextVisibility;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyMysteryTrails>())go.AddComponent<CandyMysteryTrails>();};
        void Update()
        {
            var mystery=MatchRuntime.Current as IMysteryControls;if(mystery==null)return;
            if(!mystery.Active||mystery.Trails.Count<seen||mystery.Trails.Count>0&&first!=mystery.Trails[0])Clear();
            if(!mystery.Active)return;
            int limit=Mathf.Min(mystery.Trails.Count,seen+128);var dirty=new HashSet<Batch>();
            while(seen<limit)
            {
                var trail=mystery.Trails[seen++];first=mystery.Trails[0];var blocks=Services.World?.Blocks;
                if(blocks==null||trail.block<0||trail.block>=blocks.Count||!blocks[trail.block])continue;
                string color=ColorUtility.ToHtmlStringRGB(trail.color),key=trail.block+":"+color;
                if(!open.TryGetValue(key,out var batch)||!batch.root||batch.prints>=64)
                {
                    if(!materials.TryGetValue(color,out var material)){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",trail.color);material.SetFloat("_Smoothness",.7f);materials[color]=material;}
                    var go=new GameObject("Visual - colored footprints",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(blocks[trail.block].transform,false);var mesh=new Mesh{name="Batched mystery footprints"};mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                    batch=new Batch{root=go.transform,mesh=mesh,renderer=renderer};batches.Add(batch);open[key]=batch;
                }
                var block=blocks[trail.block].transform;
                var localNormal=trail.normal.sqrMagnitude>.1f?trail.normal.normalized:Vector3.up;
                var normal=block.TransformDirection(localNormal).normalized;
                var rotation=block.rotation*Quaternion.FromToRotation(Vector3.up,localNormal)*Quaternion.Euler(0,trail.yaw,0);
                float scale=FootScale(trail.color);
                var point=block.TransformPoint(trail.point);
                for(int side=-1;side<=1;side+=2)
                {
                    var center=point+normal*.006f+rotation*Vector3.right*(side*.12f*scale);
                    Oval(batch,center+rotation*Vector3.forward*(.075f*scale),rotation,normal,.092f*scale,.12f*scale);
                    Oval(batch,center-rotation*Vector3.forward*(.075f*scale),rotation,normal,.07f*scale,.045f*scale);
                }
                batch.prints++;dirty.Add(batch);
            }
            foreach(var batch in dirty){batch.mesh.SetVertices(batch.vertices);batch.mesh.SetNormals(batch.normals);batch.mesh.SetTriangles(batch.indices,0);batch.mesh.RecalculateBounds();}
            if(Time.unscaledTime>=nextVisibility&&Services.Session?.ActiveCamera){nextVisibility=Time.unscaledTime+.25f;var point=Services.Session.ActiveCamera.transform.position;foreach(var batch in batches)if(batch.renderer)batch.renderer.enabled=batch.renderer.bounds.SqrDistance(point)<625;}
        }
        static float FootScale(Color color)
        {
            var runtime=MatchRuntime.Current;
            if(runtime?.Frame?.players!=null)foreach(var player in runtime.Frame.players)
                if(player.color==color){var visual=runtime.FindActor(player.id)?.Visual;if(visual)return Mathf.Clamp(Mathf.Abs(visual.lossyScale.x),.05f,3);}
            var local=Services.Session?.Player?.gameObject.GetComponent<IModeActor>()?.Visual;
            return local?Mathf.Clamp(Mathf.Abs(local.lossyScale.x),.05f,3):1;
        }
        static void Oval(Batch batch,Vector3 center,Quaternion rotation,Vector3 normal,float width,float length)
        {
            var frame=batch.root;var localNormal=frame.localToWorldMatrix.transpose.MultiplyVector(normal).normalized;
            int start=batch.vertices.Count;batch.vertices.Add(frame.InverseTransformPoint(center));batch.normals.Add(localNormal);
            for(int i=0;i<9;i++){float angle=i*Mathf.PI*2/8;batch.vertices.Add(frame.InverseTransformPoint(center+rotation*new Vector3(Mathf.Sin(angle)*width,0,Mathf.Cos(angle)*length)));batch.normals.Add(localNormal);if(i<8){batch.indices.Add(start);batch.indices.Add(start+i+1);batch.indices.Add(start+i+2);}}
        }
        void Clear(){foreach(var b in batches){if(b.root)Destroy(b.root.gameObject);if(b.mesh)Destroy(b.mesh);}batches.Clear();open.Clear();seen=0;first=null;}
        void OnDestroy(){Clear();foreach(var m in materials.Values)if(m)Destroy(m);}
    }
}

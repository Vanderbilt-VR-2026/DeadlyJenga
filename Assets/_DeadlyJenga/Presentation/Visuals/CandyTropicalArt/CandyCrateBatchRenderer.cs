using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Batch distant artwork only. Every block keeps its own dynamic body and collider.
    [DefaultExecutionOrder(500)]
    public sealed class CandyCrateBatchRenderer : MonoBehaviour
    {
        static readonly ProfilerMarker DJCrateBatchesMarker = new ProfilerMarker("DJ.CrateBatches");
        static CandyCrateBatchRenderer instance;
        readonly Dictionary<CandyCrateVisual, Entry> visuals = new Dictionary<CandyCrateVisual, Entry>();
        bool supported;
        sealed class Entry
        {
            public CandyCrateVisual visual;
            public MeshRenderer renderer;
            public Batch batch;
            public float refreshAt;
            public bool hidden;
            public Transform transform;
            public Matrix4x4 matrix;
            public bool hasMatrix;
        }
        readonly Dictionary<(Mesh, Material, int, ShadowCastingMode, bool, uint), Batch> batches =
            new Dictionary<(Mesh, Material, int, ShadowCastingMode, bool, uint), Batch>();
        sealed class Batch
        {
            public Mesh mesh;
            public RenderParams parameters;
            public readonly Matrix4x4[] matrices = new Matrix4x4[256];
            public int count;
            public void Flush()
            {
                if(count==0)return;
                if(mesh&&parameters.material)Graphics.RenderMeshInstanced(parameters,mesh,0,matrices,count);
                count=0;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { instance=null; }
        internal static void Register(CandyCrateVisual visual)
        {
            if(!instance)
            {
                var go=new GameObject("Candy crate artwork batches");
                go.hideFlags=HideFlags.DontSave;
                instance=go.AddComponent<CandyCrateBatchRenderer>();
            }
            instance.visuals[visual]=new Entry{visual=visual,renderer=visual.Renderer,transform=visual.Renderer.transform};
        }
        internal static void Unregister(CandyCrateVisual visual)
        {if(instance)instance.visuals.Remove(visual);}
        void Awake(){supported=SystemInfo.supportsInstancing;}
        void Refresh(Entry entry)
        {
                var visual=entry.visual;var renderer=entry.renderer;var mesh=visual.Mesh;
                var material=renderer.sharedMaterial;
                // Property blocks (for example ice tint) and custom artwork retain ordinary rendering.
                bool batch=supported&&visual.Distant&&mesh&&mesh.subMeshCount==1&&material&&material.enableInstancing
                    &&!renderer.HasPropertyBlock();
                entry.batch=null;
                if(!batch)return;
                var key=(mesh,material,renderer.gameObject.layer,renderer.shadowCastingMode,renderer.receiveShadows,renderer.renderingLayerMask);
                if(!batches.TryGetValue(key,out var group))
                {
                    group=new Batch{mesh=mesh,parameters=new RenderParams(material)
                    {
                        layer=renderer.gameObject.layer,shadowCastingMode=renderer.shadowCastingMode,
                        receiveShadows=renderer.receiveShadows,renderingLayerMask=renderer.renderingLayerMask,
                        lightProbeUsage=LightProbeUsage.BlendProbes
                    }};
                    batches.Add(key,group);
                }
                entry.batch=group;
        }
        void LateUpdate()
        {
            using var measurement = DJCrateBatchesMarker.Auto();
            foreach(var entry in visuals.Values)
            {
                if(Time.unscaledTime>=entry.refreshAt)
                {
                    Refresh(entry);
                    entry.refreshAt=Time.unscaledTime+.22f+(entry.visual.GetInstanceID()&31)*.004f;
                }
                bool batch=entry.batch!=null&&entry.renderer.enabled&&entry.visual.Distant;
                if(entry.hidden!=batch){entry.renderer.forceRenderingOff=batch;entry.hidden=batch;}
                if(!batch)continue;
                var group=entry.batch;
                if(!entry.hasMatrix||entry.transform.hasChanged)
                {
                    entry.matrix=entry.renderer.localToWorldMatrix;entry.hasMatrix=true;
                    entry.transform.hasChanged=false;
                }
                group.matrices[group.count++]=entry.matrix;
                if(group.count==group.matrices.Length)group.Flush();
            }
            foreach(var group in batches.Values)group.Flush();
        }
        void OnDisable()
        {foreach(var entry in visuals.Values)if(entry.renderer){entry.renderer.forceRenderingOff=false;entry.hidden=false;}}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}

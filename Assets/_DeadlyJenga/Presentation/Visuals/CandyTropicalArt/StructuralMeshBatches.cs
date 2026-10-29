using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace DeadlyJenga.CandyArt
{
    public sealed class StructuralMeshBatches:MonoBehaviour
    {
        sealed class Batch
        {
            public Mesh mesh; public int submesh; public Material material; public ShadowCastingMode shadows; public bool receive;
            public Matrix4x4[] matrices; public int count,users;
            public void Flush()
            {
                if(count==0)return;
                var parameters=new RenderParams(material){shadowCastingMode=shadows,receiveShadows=receive,lightProbeUsage=LightProbeUsage.BlendProbes};
                Graphics.RenderMeshInstanced(parameters,mesh,submesh,matrices,count);DrawCalls++;SubmittedInstances+=count;count=0;
            }
        }
        sealed class Entry{public MeshRenderer renderer;public Transform anchor;public Batch[] batches;public bool active;}
        public static bool BatchingEnabled=true;
        static readonly Unity.Profiling.ProfilerMarker BatchMarker=new Unity.Profiling.ProfilerMarker("DJ.StructureBatches");
        static StructuralMeshBatches instance;
        readonly List<Entry> entries=new(); readonly List<Batch> batches=new();readonly Dictionary<Material,Material> materials=new();
        IWorldQuery world; int revision=-1;bool lastEnabled;
        public static int DrawCalls {get;private set;} public static int SubmittedInstances {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){instance=null;SceneManager.sceneLoaded-=Loaded;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRuntime(){SceneManager.sceneLoaded+=Loaded;Install();}
        static void Loaded(Scene scene,LoadSceneMode mode){Install();}
        static void Install(){if(Application.isPlaying&&!instance)instance=new GameObject("Structural artwork batches").AddComponent<StructuralMeshBatches>();}
        void Release()
        {
            foreach(var e in entries)if(e.renderer)e.renderer.forceRenderingOff=false;
            entries.Clear();batches.Clear();foreach(var m in materials.Values)if(m)Destroy(m);materials.Clear();
        }
        void Refresh()
        {
            var current=Services.World;int version=current is ISyncedWorld sync?sync.Revision:0;
            if(ReferenceEquals(current,world)&&version==revision&&lastEnabled==BatchingEnabled)return;
            Release();world=current;revision=version;lastEnabled=BatchingEnabled;
            if(!BatchingEnabled||!SystemInfo.supportsInstancing||!(world is ITraversalCollisionWorld policy)||!policy.UsesOriginalColliders)return;
            foreach(var body in world.Blocks)
            {
                if(!body)continue;var visual=body.transform.Find("Visual");if(!visual)continue;
                var renderer=visual.GetComponent<MeshRenderer>();var filter=visual.GetComponent<MeshFilter>();
                if(!renderer||!filter||!filter.sharedMesh||filter.sharedMesh.subMeshCount<=1||renderer.HasPropertyBlock())continue;
                var mesh=filter.sharedMesh;var shared=renderer.sharedMaterials;var groups=new List<Batch>();
                for(int sub=0;sub<mesh.subMeshCount&&sub<shared.Length;sub++)
                {
                    if(mesh.GetIndexCount(sub)==0||!shared[sub])continue;
                    if(!materials.TryGetValue(shared[sub],out var material)){material=new Material(shared[sub]){enableInstancing=true,name=shared[sub].name+" instances"};materials.Add(shared[sub],material);}
                    Batch group=null;foreach(var candidate in batches)if(candidate.mesh==mesh&&candidate.material==material&&candidate.submesh==sub&&candidate.shadows==renderer.shadowCastingMode&&candidate.receive==renderer.receiveShadows){group=candidate;break;}
                    if(group==null){group=new Batch{mesh=mesh,material=material,submesh=sub,shadows=renderer.shadowCastingMode,receive=renderer.receiveShadows};batches.Add(group);}groups.Add(group);
                }
                if(groups.Count>0)entries.Add(new Entry{renderer=renderer,anchor=visual,batches=groups.ToArray()});
            }
        }
        void LateUpdate()
        {
            using var measurement=BatchMarker.Auto();
            DrawCalls=SubmittedInstances=0;Refresh();
            foreach(var pair in materials)if(pair.Key&&pair.Value){pair.Value.CopyPropertiesFromMaterial(pair.Key);pair.Value.enableInstancing=true;}
            foreach(var batch in batches)batch.users=0;
            foreach(var e in entries)
            {
                e.active=e.renderer&&e.renderer.enabled&&e.renderer.gameObject.activeInHierarchy&&!e.renderer.HasPropertyBlock();
                if(e.active)foreach(var batch in e.batches)batch.users++;
            }
            try
            {
                foreach(var e in entries)
                {
                    if(!e.renderer)continue;bool instance=e.active;
                    foreach(var batch in e.batches)if(batch.users<3)instance=false;
                    e.renderer.forceRenderingOff=instance;if(!instance)continue;var matrix=e.anchor.localToWorldMatrix;
                    foreach(var batch in e.batches){batch.matrices??=new Matrix4x4[256];batch.matrices[batch.count++]=matrix;if(batch.count==batch.matrices.Length)batch.Flush();}
                }
                foreach(var batch in batches)batch.Flush();
            }
            catch(System.Exception e){BatchingEnabled=false;Release();Debug.LogWarning("Structural instancing fell back to normal renderers: "+e.Message);}
        }
        void OnDisable(){Release();world=null;revision=-1;}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}

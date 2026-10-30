using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed class CandyHandPowerFade:MonoBehaviour
    {
        readonly HashSet<string> hands=new HashSet<string>();readonly List<string> stale=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>go.AddComponent<CandyHandPowerFade>();
        void Update()
        {
            var runtime=MatchRuntime.Current;if(runtime==null)return;
            foreach(var p in runtime.Frame.players)
            {
                if(p.role!="hand"){hands.Remove(p.id);continue;}
                if(p.handPowerRemaining>0&&hands.Add(p.id))CandyPowerGhost.Spawn(runtime.FindActor(p.id)?.Visual);
            }
        }
    }
    public sealed class CandyPowerGhost:MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();readonly List<Material> materials=new List<Material>();readonly List<Renderer> renderers=new List<Renderer>();float born;MaterialPropertyBlock properties;
        void Awake(){properties=new MaterialPropertyBlock();}
        public static void Spawn(Transform visual)
        {
            if(!visual)return;var root=new GameObject("Visual - survivor fades into flying hand");var effect=root.AddComponent<CandyPowerGhost>();effect.born=Time.time;
            foreach(var source in visual.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh=null;if(source is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);effect.meshes.Add(mesh);}else if(source is MeshRenderer)mesh=source.GetComponent<MeshFilter>()?.sharedMesh;if(!mesh)continue;
                var go=new GameObject("Fading avatar",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=mesh;var target=go.GetComponent<MeshRenderer>();
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;effect.materials.Add(mat);target.sharedMaterial=mat;source.GetPropertyBlock(effect.properties);if(!effect.properties.HasColor("_BaseColor"))effect.properties.SetColor("_BaseColor",source.sharedMaterial&&source.sharedMaterial.HasProperty("_BaseColor")?source.sharedMaterial.GetColor("_BaseColor"):Color.white);target.SetPropertyBlock(effect.properties);effect.renderers.Add(target);
            }
            Destroy(root,.65f);
        }
        void Update(){float alpha=1-Mathf.Clamp01((Time.time-born)/.6f);foreach(var renderer in renderers)if(renderer){renderer.GetPropertyBlock(properties);var color=properties.GetColor("_BaseColor");color.a=alpha;properties.SetColor("_BaseColor",color);renderer.SetPropertyBlock(properties);}}
        void OnDestroy(){foreach(var mesh in meshes)Destroy(mesh);foreach(var material in materials)Destroy(material);}
    }
}

using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeadlyJenga.Presentation
{
    public sealed class CandyEnemyHealth:MonoBehaviour
    {
        static Material material;readonly List<GameObject> roots=new List<GameObject>();float nextScan,nextHealth,nextRenderers;Renderer[] renderers=System.Array.Empty<Renderer>();
        readonly Dictionary<Renderer,Material[]> original=new Dictionary<Renderer,Material[]>();MaterialPropertyBlock properties;
        void Awake(){properties=new MaterialPropertyBlock();}IModeActor actor;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>go.AddComponent<CandyEnemyHealth>();
        void Start(){actor=GetComponent<IModeActor>();}
        void Update()
        {
            if(actor==null)
            {
                if(Time.time<nextScan)return;nextScan=Time.time+1;
                for(int i=0;i<SceneManager.sceneCount;i++){SceneManager.GetSceneAt(i).GetRootGameObjects(roots);foreach(var root in roots)if(root&&root.GetComponent<IEnemyActor>()?.IsEnemy==true&&!root.GetComponent<CandyEnemyHealth>())root.AddComponent<CandyEnemyHealth>();}return;
            }
            if(!actor.Alive||!actor.Visual||Time.time<nextHealth)return;nextHealth=Time.time+.1f;
            if(!material){var shader=Resources.Load<Shader>("CandyScenery/CandyAvatarHealth");if(!shader)return;material=new Material(shader){enableInstancing=true};}
            if(Time.time>=nextRenderers){nextRenderers=Time.time+2;renderers=actor.Visual.GetComponentsInChildren<Renderer>();}
            foreach(var r in renderers)
            {
                if(!r||!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer))continue;
                if(!original.ContainsKey(r)){var materials=r.sharedMaterials;original[r]=materials;r.GetPropertyBlock(properties);if(materials.Length>0&&materials[0]){if(!properties.HasColor("_BaseColor"))properties.SetColor("_BaseColor",materials[0].HasProperty("_BaseColor")?materials[0].GetColor("_BaseColor"):Color.white);if(!properties.GetTexture("_BaseMap")&&materials[0].HasProperty("_BaseMap"))properties.SetTexture("_BaseMap",materials[0].GetTexture("_BaseMap")??Texture2D.whiteTexture);}var replacements=new Material[materials.Length];for(int n=0;n<replacements.Length;n++)replacements[n]=material;r.sharedMaterials=replacements;}
                else r.GetPropertyBlock(properties);
                properties.SetFloat("_Enemy",1);properties.SetFloat("_Health",actor.Health/Mathf.Max(1,actor.MaxHealth));properties.SetFloat("_Bottom",actor.transform.position.y);bool low=GetComponent<IEnemyActor>()?.ArchetypeId!="mushroom"&&GetComponent<IEnemyPose>()?.PreparingMelee!=true&&GetComponent<IMovementState>()?.Climbing!=true;properties.SetFloat("_Top",low?actor.transform.position.y+.95f:Mathf.Max(actor.transform.position.y+.2f,actor.ViewPose.position.y));r.SetPropertyBlock(properties);
            }
        }
        void OnDestroy(){foreach(var pair in original)if(pair.Key)pair.Key.sharedMaterials=pair.Value;}
    }
}

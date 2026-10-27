using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeadlyJenga.Presentation
{
    // Bounds for tall upright enemies, plus their candy splat on elimination.
    [DefaultExecutionOrder(800)]
    public sealed class CandyMayhemEnemyFinish:MonoBehaviour
    {
        readonly List<GameObject> roots=new List<GameObject>();
        readonly List<Renderer> parts=new List<Renderer>();
        MaterialPropertyBlock properties;
        IModeActor actor;
        float nextScan,top;
        Transform splat;float born;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>go.AddComponent<CandyMayhemEnemyFinish>();
        void LateUpdate()
        {
            if(splat){float t=Mathf.SmoothStep(0,1,(Time.time-born)/1.3f);splat.localScale=new Vector3(1+t*.6f,1-t*.98f,1+t*.6f);return;}
            if(actor==null){
                if(Time.time<nextScan)return;nextScan=Time.time+1;
                for(int i=0;i<SceneManager.sceneCount;i++){SceneManager.GetSceneAt(i).GetRootGameObjects(roots);foreach(var root in roots){
                    var enemy=root?root.GetComponent<IEnemyActor>():null;
                    if(enemy?.IsEnemy==true&&(enemy.ArchetypeId=="longhand"||enemy.ArchetypeId=="gum-brute")&&!root.GetComponent<CandyMayhemEnemyFinish>()){
                        var finish=root.AddComponent<CandyMayhemEnemyFinish>();finish.actor=root.GetComponent<IModeActor>();
                    }
                }}return;
            }
            if(!actor.Visual)return;
            if(Time.time>=nextScan){
                nextScan=Time.time+.2f;actor.Visual.GetComponentsInChildren(false,parts);top=actor.transform.position.y+.2f;
                foreach(var part in parts)if(part&&!part.forceRenderingOff)top=Mathf.Max(top,part.bounds.max.y);
            }
            properties??=new MaterialPropertyBlock();
            Renderer first=null;foreach(var part in parts)if(part&&!part.forceRenderingOff){first=part;break;}
            if(!first)return;first.GetPropertyBlock(properties);
            if(Mathf.Abs(properties.GetFloat("_Top")-top)<.001f)return;
            foreach(var part in parts)if(part&&!part.forceRenderingOff){part.GetPropertyBlock(properties);properties.SetFloat("_Top",top);part.SetPropertyBlock(properties);}
        }
        public static void Splat(Transform visual)
        {
            if(!visual)return;
            var root=new GameObject("Visual - melted mayhem enemy");root.transform.position=visual.position;
            var finish=root.AddComponent<CandyMayhemEnemyFinish>();finish.born=Time.time;finish.splat=new GameObject("Candy splat").transform;finish.splat.SetParent(root.transform,false);
            foreach(var part in visual.GetComponentsInChildren<MeshRenderer>(true)){
                if(part.forceRenderingOff)continue;var mesh=part.GetComponent<MeshFilter>();if(!mesh)continue;
                var go=new GameObject("Candy piece",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(finish.splat,false);go.transform.SetPositionAndRotation(part.transform.position,part.transform.rotation);go.transform.localScale=part.transform.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterials=part.sharedMaterials;
                var block=new MaterialPropertyBlock();part.GetPropertyBlock(block);block.SetFloat("_Enemy",0);go.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            }
            Destroy(root,4);
        }
    }
}

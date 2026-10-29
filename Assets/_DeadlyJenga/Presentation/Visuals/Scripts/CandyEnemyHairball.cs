using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed class CandyEnemyHairball:MonoBehaviour
    {
        sealed class Piece {public Transform root;public Vector3 from,scale;}
        readonly List<Piece> pieces=new List<Piece>();readonly List<Mesh> meshes=new List<Mesh>();Transform ball;float born;
        public static void Spawn(Transform visual)
        {
            if(!visual)return;var root=new GameObject("Visual - jaguar curls into hairball");root.transform.position=visual.position;var effect=root.AddComponent<CandyEnemyHairball>();effect.born=Time.time;
            Material coat=null;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh=null;if(renderer is SkinnedMeshRenderer skinned){mesh=new Mesh();skinned.BakeMesh(mesh);effect.meshes.Add(mesh);}else if(renderer is MeshRenderer)mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(!mesh)continue;
                var go=new GameObject("Curling coat",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);go.GetComponent<MeshRenderer>().SetPropertyBlock(properties);if(!coat)coat=renderer.sharedMaterial;effect.pieces.Add(new Piece{root=go.transform,from=go.transform.localPosition,scale=go.transform.localScale});
            }
            var sphere=new GameObject("Fuzzy hairball",typeof(MeshFilter),typeof(MeshRenderer));sphere.transform.SetParent(root.transform,false);sphere.transform.localPosition=Vector3.up*.22f;sphere.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");sphere.GetComponent<MeshRenderer>().sharedMaterial=coat;var coatSource=visual.GetComponentInChildren<Renderer>();if(coatSource){var properties=new MaterialPropertyBlock();coatSource.GetPropertyBlock(properties);properties.SetFloat("_Enemy",0);properties.SetFloat("_Health",1);sphere.GetComponent<MeshRenderer>().SetPropertyBlock(properties);}effect.ball=sphere.transform;effect.ball.localScale=Vector3.one*.01f;Object.Destroy(root,3);
        }
        void Update()
        {
            float age=Time.time-born,t=Mathf.SmoothStep(0,1,age/.65f);
            foreach(var p in pieces)if(p.root){p.root.localPosition=Vector3.Lerp(p.from,Vector3.up*.22f,t);p.root.localScale=p.scale*(1-t);}
            if(ball){float dry=1-Mathf.SmoothStep(0,1,(age-1.8f)/1.1f);ball.localScale=Vector3.one*(.48f*t*dry);ball.localRotation=Quaternion.Euler(age*40,age*70,age*15);}
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}

using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyEquipmentView
    {
        static Material pickupCloud;static Mesh pickupCloudMesh;
        void BuildPickupCloud(Transform root,Color color)
        {
            if(!pickupCloud){pickupCloud=new Material(Shader.Find("DeadlyJenga/Pickup Cloud"));pickupCloud.SetColor("_BaseColor",Color.white);}
            if(!pickupCloudMesh)
            {
                var source=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");pickupCloudMesh=Instantiate(source);var v=pickupCloudMesh.vertices;var bounds=source.bounds;
                for(int i=0;i<v.Length;i++){var p=(v[i]-bounds.center)/bounds.extents.magnitude;float noise=1+.13f*Mathf.Sin(p.x*17+p.z*11)*Mathf.Cos(p.y*13-p.x*8);v[i]=(v[i]-bounds.center)*noise/Mathf.Max(.001f,bounds.size.x);}
                pickupCloudMesh.vertices=v;pickupCloudMesh.RecalculateNormals();pickupCloudMesh.RecalculateBounds();
            }
            for(int i=0;i<2;i++)
            {
                var cloud=new GameObject("Translucent static pickup cloud",typeof(MeshFilter),typeof(MeshRenderer));cloud.transform.SetParent(root,false);cloud.transform.localScale=Vector3.one*(i==0?.63f:.57f);cloud.transform.localRotation=Quaternion.Euler(i*31,i*67,i*19);
                cloud.GetComponent<MeshFilter>().sharedMesh=pickupCloudMesh;var renderer=cloud.GetComponent<MeshRenderer>();renderer.sharedMaterial=pickupCloud;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(color.r,color.g,color.b,.12f));renderer.SetPropertyBlock(block);
            }
        }
        void UpdatePickupModel(PickupVisual view)
        {
            if(!view.model&&EquipmentModelData.Parts.TryGetValue(view.kind,out var parts))
            {
                view.model=new GameObject("Live equipment model").transform;view.model.SetParent(view.root,false);
                Bounds bounds=default;bool first=true;
                foreach(var part in parts)
                {
                    var go=new GameObject("Equipment geometry",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(view.model,false);go.transform.localPosition=part.local.GetColumn(3);go.transform.localRotation=part.local.rotation;go.transform.localScale=part.local.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=part.mesh;
                    var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=part.material;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",part.color);block.SetFloat("_Smoothness",.9f);block.SetFloat("_Metallic",.65f);renderer.SetPropertyBlock(block);renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    var b=renderer.bounds;if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
                }
                if(!first){float scale=.42f/Mathf.Max(.1f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));var center=view.model.InverseTransformPoint(bounds.center);var content=new GameObject("Centered model geometry").transform;var children=new System.Collections.Generic.List<Transform>();foreach(Transform child in view.model)children.Add(child);content.SetParent(view.model,false);foreach(var child in children)child.SetParent(content,false);content.localPosition=-center;view.model.localScale=Vector3.one*scale;view.model.localPosition=Vector3.zero;}
            }
            if(view.model)view.model.localRotation=Quaternion.Euler(12,Time.time*24,0);
        }
    }
}

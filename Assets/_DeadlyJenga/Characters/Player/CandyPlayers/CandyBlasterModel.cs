using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public static class CandyBlasterModel
    {
        static Mesh[] meshes;
        public static Transform Create(Transform parent,Material material)
        {
            meshes??=CandyAccessoryMeshes.Blaster();var root=new GameObject("Visual - three ring alien blaster").transform;root.SetParent(parent,false);
            Color[] colors={new Color(.95f,.09f,.08f),new Color(.3f,.95f,1),new Color(1,.85f,.18f),new Color(.7f,.83f,.9f)};
            for(int i=0;i<meshes.Length;i++)
            {
                var go=new GameObject("Blaster part "+i);go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(0,.09f,.08f);go.AddComponent<MeshFilter>().sharedMesh=meshes[i];var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",colors[i]);properties.SetFloat("_Smoothness",.94f);properties.SetFloat("_Metallic",i==0?.38f:.72f);properties.SetColor("_EmissionColor",i==1?colors[i]*.35f:Color.black);renderer.SetPropertyBlock(properties);
            }
            return root;
        }
    }
}

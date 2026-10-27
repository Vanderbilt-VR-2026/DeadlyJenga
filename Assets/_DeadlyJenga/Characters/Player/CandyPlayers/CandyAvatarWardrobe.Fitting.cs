using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyAvatarWardrobe
    {
        static readonly Dictionary<string,Mesh> garments=new Dictionary<string,Mesh>();
        void BatchPieces(CandyPlayerRig rig)
        {
            var groups=new Dictionary<string,List<Piece>>();
            foreach(var p in pieces)if(p.renderer is MeshRenderer){string key=p.bone+":"+p.tone+":"+(p.renderer.sharedMaterial?p.renderer.sharedMaterial.GetInstanceID():0);if(!groups.TryGetValue(key,out var group)){group=new List<Piece>();groups[key]=group;}group.Add(p);}
            foreach(var pair in groups)
            {
                var group=pair.Value;if(group.Count<2)continue;string key="wardrobe batch:"+CandyPlayerAvatar.StyleBits(appearance)+":"+(uint)appearance.accessories+":"+pair.Key;
                if(!meshes.TryGetValue(key,out var mesh)){var combine=new CombineInstance[group.Count];for(int i=0;i<group.Count;i++)combine[i]=new CombineInstance{mesh=group[i].renderer.GetComponent<MeshFilter>().sharedMesh,transform=Matrix4x4.identity};mesh=new Mesh{name=key};mesh.CombineMeshes(combine,true,false);meshes[key]=mesh;}
                group[0].renderer.GetComponent<MeshFilter>().sharedMesh=mesh;
                for(int i=1;i<group.Count;i++){group[i].renderer.gameObject.SetActive(false);Destroy(group[i].renderer.gameObject);pieces.Remove(group[i]);}
            }
        }
        void FittedSkirt(CandyPlayerRig rig,string id,Tone tone)
        {
            string key="skirt:"+rig.bodyRenderer.sharedMesh.GetInstanceID();
            if(!garments.TryGetValue(key,out var mesh))
            {
                const int rows=12,sides=32;var vertices=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var triangles=new List<int>();
                for(int row=0;row<=rows;row++)for(int side=0;side<=sides;side++)
                {
                    float y=Mathf.Lerp(.29f,.84f,(float)row/rows),angle=side*Mathf.PI*2/sides,radius=Mathf.Lerp(.32f,.205f,(float)row/rows);float x=Mathf.Cos(angle)*radius;
                    vertices.Add(new Vector3(x,y,Mathf.Sin(angle)*radius*.90f));uv.Add(new Vector2((float)side/sides,(float)row/rows));float root=Mathf.SmoothStep(.15f,1,Mathf.InverseLerp(.29f,.74f,y));weights.Add(new BoneWeight{boneIndex0=0,weight0=root,boneIndex1=x<0?8:11,weight1=1-root});
                    if(row==rows||side==sides)continue;int v=row*(sides+1)+side;triangles.AddRange(new[]{v,v+sides+1,v+1,v+1,v+sides+1,v+sides+2});
                }
                mesh=new Mesh{name="Fitted flowing skirt"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=rig.bodyRenderer.sharedMesh.bindposes;mesh.RecalculateNormals();mesh.RecalculateBounds();garments[key]=mesh;
            }
            var go=new GameObject("Visual - "+id);go.transform.SetParent(rig.transform,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=rig.bodyRenderer.sharedMaterial;renderer.bones=rig.bodyRenderer.bones;renderer.rootBone=rig.bodyRenderer.rootBone;renderer.updateWhenOffscreen=true;pieces.Add(new Piece{renderer=renderer,tone=tone,bone=0});
        }
        void FittedGarment(CandyPlayerRig rig,string id,Tone tone,bool pants)
        {
            var source=rig.bodyRenderer.sharedMesh;if(!source||!source.isReadable)return;string key=source.GetInstanceID()+":"+pants;
            if(!garments.TryGetValue(key,out var mesh))
            {
                mesh=Instantiate(source);mesh.name="Fitted "+(pants?"trousers":"jacket");var vertices=source.vertices;var normals=source.normals;var weights=source.boneWeights;var indices=new List<int>();
                bool Covered(int i){int bone=weights[i].boneIndex0;return pants?bone>=8&&bone<=13||bone==0&&vertices[i].y<.77f:bone==0&&vertices[i].y>.73f||bone==2||bone==3||bone==5||bone==6;}
                var triangles=source.triangles;for(int i=0;i<triangles.Length;i+=3)if(Covered(triangles[i])&&Covered(triangles[i+1])&&Covered(triangles[i+2])){indices.Add(triangles[i]);indices.Add(triangles[i+1]);indices.Add(triangles[i+2]);}
                for(int i=0;i<vertices.Length;i++)vertices[i]+=normals[i]*(pants?.012f:.018f);mesh.vertices=vertices;mesh.triangles=indices.ToArray();mesh.RecalculateBounds();garments[key]=mesh;
            }
            for(int i=pieces.Count-1;i>=0;i--)if(pieces[i].renderer is SkinnedMeshRenderer old&&old.sharedMesh==mesh){old.gameObject.SetActive(false);Destroy(old.gameObject);pieces.RemoveAt(i);}
            var go=new GameObject("Visual - "+id);go.transform.SetParent(rig.transform,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=rig.bodyRenderer.sharedMaterial;renderer.bones=rig.bodyRenderer.bones;renderer.rootBone=rig.bodyRenderer.rootBone;renderer.localBounds=rig.bodyRenderer.localBounds;renderer.updateWhenOffscreen=true;pieces.Add(new Piece{renderer=renderer,tone=tone,bone=0});
        }
    }
}

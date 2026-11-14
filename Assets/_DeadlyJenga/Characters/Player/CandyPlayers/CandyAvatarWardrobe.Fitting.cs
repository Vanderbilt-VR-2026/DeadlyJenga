using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    // Garments that are cut from the avatar's own skinned body, so they follow every bone and sit on the body.
    public sealed partial class CandyAvatarWardrobe
    {
        static readonly Dictionary<string,Mesh> garments=new Dictionary<string,Mesh>();
        static bool IsLimb(int bone)=>bone==2||bone==3||bone==5||bone==6||bone==8||bone==9||bone==11||bone==12;
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
        // A skirt that hangs from the waist. The legs pull its hem gently, and the left and right leg weights
        // blend smoothly across the middle so the front and back never tear open when the legs swing.
        void FittedSkirt(CandyPlayerRig rig,string id,Tone tone)
        {
            string key="skirt:"+rig.bodyRenderer.sharedMesh.GetInstanceID();
            if(!garments.TryGetValue(key,out var mesh))
            {
                const int rows=12,sides=32;var vertices=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var triangles=new List<int>();
                for(int row=0;row<=rows;row++)for(int side=0;side<=sides;side++)
                {
                    float t=(float)row/rows,y=Mathf.Lerp(.29f,.84f,t),angle=side*Mathf.PI*2/sides;
                    // A soft flare, with the hem tucked in slightly so the cut edge reads as a rolled hem.
                    float radius=Mathf.Lerp(.32f,.215f,Mathf.Pow(t,.8f));if(row==0)radius-=.012f;
                    float x=Mathf.Cos(angle)*radius;
                    vertices.Add(new Vector3(x,row==0?y+.012f:y,Mathf.Sin(angle)*radius*.90f));uv.Add(new Vector2((float)side/sides,t));
                    float root=Mathf.SmoothStep(.15f,1,Mathf.InverseLerp(.29f,.74f,y));float mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.10f,.10f,x));
                    weights.Add(new BoneWeight{boneIndex0=0,weight0=root,boneIndex1=8,weight1=(1-root)*(1-mix),boneIndex2=11,weight2=(1-root)*mix});
                    if(row==rows||side==sides)continue;int v=row*(sides+1)+side;triangles.AddRange(new[]{v,v+sides+1,v+1,v+1,v+sides+1,v+sides+2});
                }
                mesh=new Mesh{name="Fitted flowing skirt"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=rig.bodyRenderer.sharedMesh.bindposes;mesh.RecalculateNormals();
                // Share normals across the seam where the last column meets the first, so no line shows down the side.
                var normals=mesh.normals;for(int row=0;row<=rows;row++){int first=row*(sides+1),last=first+sides;var joined=(normals[first]+normals[last]).normalized;normals[first]=joined;normals[last]=joined;}
                mesh.normals=normals;mesh.RecalculateBounds();garments[key]=mesh;
            }
            var go=new GameObject("Visual - "+id);go.transform.SetParent(rig.transform,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=rig.bodyRenderer.sharedMaterial;renderer.bones=rig.bodyRenderer.bones;renderer.rootBone=rig.bodyRenderer.rootBone;renderer.localBounds=rig.bodyRenderer.localBounds;renderer.updateWhenOffscreen=true;pieces.Add(new Piece{renderer=renderer,tone=tone,bone=0});
        }
        void FittedGarment(CandyPlayerRig rig,string id,Tone tone,bool pants)
        {
            bool vest=id=="puffer vest";
            FittedPatch(rig,id,tone,pants?.016f:.022f,pants?"trousers":vest?"vest":"jacket",(p,n,bone)=>pants?bone>=8&&bone<=13||bone==0&&p.y<.77f:bone==0&&p.y>.73f||!vest&&(bone==2||bone==3||bone==5||bone==6));
        }
        // Cuts the part of the body picked by include(position, normal, dominant bone), lifts it along the normals
        // and skins it to the same bones. The result is a thin shell that hugs the body.
        void FittedPatch(CandyPlayerRig rig,string id,Tone tone,float offset,string kind,System.Func<Vector3,Vector3,int,bool> include)
        {
            var source=rig.bodyRenderer.sharedMesh;if(!source||!source.isReadable)return;bool shared=kind=="jacket"||kind=="trousers"||kind=="vest";string key=source.GetInstanceID()+":patch:"+kind+":"+(shared?"":id)+":"+offset;
            if(!garments.TryGetValue(key,out var mesh))
            {
                var vertices=source.vertices;var normals=source.normals;var weights=source.boneWeights;var triangles=source.triangles;
                var ok=new bool[vertices.Length];for(int i=0;i<ok.Length;i++)ok[i]=include(vertices[i],normals[i],weights[i].boneIndex0);
                var remap=new int[vertices.Length];for(int i=0;i<remap.Length;i++)remap[i]=-1;
                var outVertices=new List<Vector3>();var outNormals=new List<Vector3>();var outWeights=new List<BoneWeight>();var outTriangles=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    if(!(ok[triangles[i]]&&ok[triangles[i+1]]&&ok[triangles[i+2]]))continue;
                    for(int k=0;k<3;k++){int old=triangles[i+k];if(remap[old]<0){remap[old]=outVertices.Count;outVertices.Add(vertices[old]+normals[old]*offset);outNormals.Add(normals[old]);outWeights.Add(weights[old]);}outTriangles.Add(remap[old]);}
                }
                mesh=new Mesh{name="Fitted "+kind+" "+id};mesh.indexFormat=outVertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(outVertices);mesh.SetNormals(outNormals);mesh.boneWeights=outWeights.ToArray();mesh.bindposes=source.bindposes;mesh.SetTriangles(outTriangles,0);mesh.RecalculateBounds();garments[key]=mesh;
            }
            if(mesh.vertexCount==0)return;
            if(shared)for(int i=pieces.Count-1;i>=0;i--)if(pieces[i].renderer is SkinnedMeshRenderer old&&old.sharedMesh==mesh){old.gameObject.SetActive(false);Destroy(old.gameObject);pieces.RemoveAt(i);}
            var go=new GameObject("Visual - "+id);go.transform.SetParent(rig.transform,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=rig.bodyRenderer.sharedMaterial;renderer.bones=rig.bodyRenderer.bones;renderer.rootBone=rig.bodyRenderer.rootBone;renderer.localBounds=rig.bodyRenderer.localBounds;renderer.updateWhenOffscreen=true;pieces.Add(new Piece{renderer=renderer,tone=tone,bone=0});
        }
        // Horizontal bands (belts, stripes, bandages). ranges are pairs of heights: min,max,min,max...
        void Band(CandyPlayerRig rig,string id,Tone tone,float offset,bool limbs,params float[] ranges)
        {
            FittedPatch(rig,id,tone,offset,"band",(p,n,bone)=>
            {
                if(!(bone==0||limbs&&IsLimb(bone)))return false;
                for(int i=0;i+1<ranges.Length;i+=2)if(p.y>=ranges[i]&&p.y<=ranges[i+1])return true;
                return false;
            });
        }
        static float[] Stripes(float from,float to,float step,float thickness)
        {
            var list=new List<float>();for(float y=from;y<=to;y+=step){list.Add(y);list.Add(y+thickness);}return list.ToArray();
        }
        // The point on the torso skin that faces a direction at a height, with its normal. Used to seat spots and bows on the real surface.
        Vector3 SurfacePoint(CandyPlayerRig rig,float angle,float y,out Vector3 normal)
        {
            normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var source=rig.bodyRenderer.sharedMesh;if(!source||!source.isReadable)return new Vector3(Mathf.Cos(angle)*.19f,y,Mathf.Sin(angle)*.15f);
            var vertices=source.vertices;var normals=source.normals;var weights=source.boneWeights;var dir=normal;
            float best=-9;int pick=-1;
            for(int i=0;i<vertices.Length;i++)
            {
                if(weights[i].boneIndex0!=0||Mathf.Abs(vertices[i].y-y)>.05f)continue;
                float score=Vector3.Dot(normals[i],dir)-Mathf.Abs(vertices[i].y-y)*3;if(score>best){best=score;pick=i;}
            }
            if(pick<0)return new Vector3(Mathf.Cos(angle)*.19f,y,Mathf.Sin(angle)*.15f);
            normal=normals[pick];return vertices[pick];
        }
    }
}

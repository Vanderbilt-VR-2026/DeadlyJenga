using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerRig
    {
        readonly Dictionary<(Mesh source,bool clearance),Mesh> firstPersonBodies = new();
        Mesh renderedBody;

        void SetFirstPersonBody(bool active,bool clearance)
        {
            RestoreFirstPersonBody();
            if(!active || !bodyRenderer || !bodyRenderer.sharedMesh) return;
            var source = bodyRenderer.sharedMesh;
            var key=(source,clearance);
            if(!firstPersonBodies.TryGetValue(key,out var visible))
            {
                if(!source.isReadable) return;
                var weights = source.boneWeights;
                if(weights.Length != source.vertexCount) return;
                visible = Instantiate(source); visible.name = source.name + (clearance?" first-person ledge clearance":" first-person body");
                var vertices=source.vertices;
                var triangles = new List<int>();
                for(int submesh=0;submesh<source.subMeshCount;submesh++)
                {
                    triangles.Clear();var indices=source.GetTriangles(submesh);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];
                        bool torso=Torso(weights[a])&&Torso(weights[b])&&Torso(weights[c]);
                        if(torso&&(clearance||(vertices[a].y+vertices[b].y+vertices[c].y)/3f>.99f))continue;
                        triangles.Add(a);triangles.Add(b);triangles.Add(c);
                    }
                    visible.SetTriangles(triangles,submesh,false);
                }
                firstPersonBodies.Add(key,visible);
            }
            renderedBody=source;bodyRenderer.sharedMesh=visible;
        }
        static bool Torso(BoneWeight weight)
            => TorsoShare(weight.boneIndex0,weight.weight0)+TorsoShare(weight.boneIndex1,weight.weight1)
                +TorsoShare(weight.boneIndex2,weight.weight2)+TorsoShare(weight.boneIndex3,weight.weight3)>.99f;
        // Pelvis, spine and chest together make the torso.
        static float TorsoShare(int bone,float weight)=>bone==0||bone==SpineBoneIndex||bone==ChestBoneIndex?weight:0;
        void RestoreFirstPersonBody()
        {
            if(renderedBody && bodyRenderer) bodyRenderer.sharedMesh=renderedBody;
            renderedBody=null;
        }
        void OnDestroy()
        {
            RestoreFirstPersonBody();
            foreach(var mesh in firstPersonBodies.Values)if(mesh)Destroy(mesh);
            firstPersonBodies.Clear();
        }
    }
}

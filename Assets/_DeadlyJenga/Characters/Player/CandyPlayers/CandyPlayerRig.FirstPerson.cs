using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerRig
    {
        readonly Dictionary<Mesh,Mesh> firstPersonBodies = new Dictionary<Mesh,Mesh>();
        Mesh renderedBody;

        void SetFirstPersonBody(bool active)
        {
            RestoreFirstPersonBody();
            if(!active || !bodyRenderer || !bodyRenderer.sharedMesh) return;
            var source = bodyRenderer.sharedMesh;
            if(!firstPersonBodies.TryGetValue(source,out var visible))
            {
                if(!source.isReadable) return;
                var weights = source.boneWeights;
                if(weights.Length != source.vertexCount) return;
                visible = Instantiate(source); visible.name = source.name + " first-person arms and legs";
                var triangles = new List<int>();
                for(int submesh=0;submesh<source.subMeshCount;submesh++)
                {
                    triangles.Clear();var indices=source.GetTriangles(submesh);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        if(Torso(weights[indices[i]]) && Torso(weights[indices[i+1]]) && Torso(weights[indices[i+2]])) continue;
                        triangles.Add(indices[i]);triangles.Add(indices[i+1]);triangles.Add(indices[i+2]);
                    }
                    visible.SetTriangles(triangles,submesh,false);
                }
                firstPersonBodies.Add(source,visible);
            }
            renderedBody=source;bodyRenderer.sharedMesh=visible;
        }
        static bool Torso(BoneWeight weight)
            => (weight.boneIndex0==0?weight.weight0:0)+(weight.boneIndex1==0?weight.weight1:0)
                +(weight.boneIndex2==0?weight.weight2:0)+(weight.boneIndex3==0?weight.weight3:0)>.99f;
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

using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerRig
    {
        static readonly Dictionary<Mesh,Mesh> wristMeshes=new();

        // Older baked bodies end in a pinched point. Keep a full wrist inside the palm cuff.
        static Mesh BlendWristMesh(Mesh source)
        {
            if(!source||!source.isReadable||source.bindposes.Length<14)return source;
            if(wristMeshes.TryGetValue(source,out var cached)&&cached)return cached;
            var vertices=source.vertices;
            var normals=source.normals;
            var weights=source.boneWeights;
            var bindings=source.bindposes;
            var triangles=source.triangles;
            if(weights.Length!=vertices.Length||normals.Length!=vertices.Length)return source;
            for(int side=0;side<2;side++)
            {
                int upper=side==0?2:5,forearm=upper+1,palm=upper+2;
                bool Arm(BoneWeight w) =>
                    w.weight0>0&&(w.boneIndex0==upper||w.boneIndex0==forearm)||
                    w.weight1>0&&(w.boneIndex1==upper||w.boneIndex1==forearm)||
                    w.weight2>0&&(w.boneIndex2==upper||w.boneIndex2==forearm)||
                    w.weight3>0&&(w.boneIndex3==upper||w.boneIndex3==forearm);
                var arm=new bool[vertices.Length];
                // Include the terminal ring, whose old weights belonged entirely to the palm.
                // The separate hand component has no arm influences and stays unchanged.
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    if(Arm(weights[a])||Arm(weights[b])||Arm(weights[c]))arm[a]=arm[b]=arm[c]=true;
                }
                Matrix4x4 local=bindings[forearm],root=local.inverse;
                float length=local.MultiplyPoint3x4(bindings[palm].inverse.MultiplyPoint3x4(Vector3.zero)).y;
                if(length<.05f)continue;
                for(int i=0;i<vertices.Length;i++)
                {
                    if(!arm[i])continue;
                    Vector3 p=local.MultiplyPoint3x4(vertices[i]);
                    float f=p.y/length;
                    if(f<.5f)continue;
                    float t=Mathf.Clamp01((f-.5f)*2),t2=t*t,t3=t2*t;
                    const float start=.043f,end=.024f,tangent=-.008f;
                    float radius=(2*t3-3*t2+1)*start+(t3-2*t2+t)*tangent+(-2*t3+3*t2)*end;
                    float slope=((6*t2-6*t)*start+(3*t2-4*t+1)*tangent+(-6*t2+6*t)*end)*2/length;
                    Vector3 radial=new Vector3(p.x,0,p.z);
                    if(radial.sqrMagnitude<.0000001f)
                    {
                        radial=local.MultiplyVector(normals[i]);
                        radial.y=0;
                    }
                    radial.Normalize();
                    vertices[i]=root.MultiplyPoint3x4(new Vector3(radial.x*radius,p.y,radial.z*radius));
                    normals[i]=root.MultiplyVector(new Vector3(radial.x,-slope,radial.z).normalized).normalized;
                    weights[i]=new BoneWeight{boneIndex0=forearm,weight0=1};
                }
            }
            var mesh=Object.Instantiate(source);
            mesh.name=source.name+" (blended wrists)";
            mesh.vertices=vertices;mesh.normals=normals;mesh.boneWeights=weights;
            mesh.RecalculateTangents();mesh.RecalculateBounds();
            wristMeshes[source]=mesh;wristMeshes[mesh]=mesh;
            return mesh;
        }
    }
}

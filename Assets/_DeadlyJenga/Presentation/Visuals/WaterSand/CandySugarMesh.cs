using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Sparse raised facets complement the fine, mipmapped surface texture.
    static class CandySugarMesh
    {
        struct Anchor { public int a, b, c; public Vector3 bary; }

        public static Mesh Build(Mesh source, Transform transform, float bottom, float span, int seed)
        {
            var original = source.vertices;
            var normal = source.normals;
            if (normal.Length != original.Length) return UnityEngine.Object.Instantiate(source);
            var vertices = new List<Vector3>(original);
            var normals = new List<Vector3>(normal);
            var rest = new List<Vector4>();
            var raised = new List<Vector2>();
            var weights = new List<BoneWeight>(source.boneWeights);
            bool skinned = weights.Count == original.Length;
            var anchors = new List<Anchor>();
            var submeshes = new List<int>[source.subMeshCount];
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            Vector4 Rest(Vector3 v)
            {
                var p = transform.TransformPoint(v);
                var scaled = Vector3.Scale(v, transform.lossyScale);
                return new Vector4(scaled.x, scaled.y, scaled.z, (p.y-bottom)/span);
            }
            foreach (var v in original) { rest.Add(Rest(v)); raised.Add(Vector2.zero); }
            int remaining = 1800;
            var influences = new Dictionary<int,float>();
            BoneWeight Weight(Anchor a)
            {
                influences.Clear();
                void Add(BoneWeight w, float amount)
                {
                    void One(int bone, float value)
                    {
                        if (value <= 0) return;
                        influences.TryGetValue(bone, out float old);
                        influences[bone] = old + value*amount;
                    }
                    One(w.boneIndex0,w.weight0); One(w.boneIndex1,w.weight1);
                    One(w.boneIndex2,w.weight2); One(w.boneIndex3,w.weight3);
                }
                Add(weights[a.a],a.bary.x); Add(weights[a.b],a.bary.y); Add(weights[a.c],a.bary.z);
                var sorted = new List<KeyValuePair<int,float>>(influences);
                sorted.Sort((x,y)=>y.Value.CompareTo(x.Value));
                float total=0; for(int i=0;i<Math.Min(4,sorted.Count);i++) total+=sorted[i].Value;
                int Index(int i)=>i<sorted.Count?sorted[i].Key:0;
                float Value(int i)=>i<sorted.Count?sorted[i].Value/Mathf.Max(.00001f,total):0;
                return new BoneWeight { boneIndex0=Index(0),boneIndex1=Index(1),boneIndex2=Index(2),boneIndex3=Index(3),
                    weight0=Value(0),weight1=Value(1),weight2=Value(2),weight3=Value(3) };
            }
            for (int sub=0;sub<submeshes.Length;sub++)
            {
                var indices=source.GetTriangles(sub);
                submeshes[sub]=new List<int>(indices);
                // Stratify by triangle area, independent of the original vertex density.
                for(int t=0;t<indices.Length&&remaining>0;t+=3)
                {
                    int ia=indices[t],ib=indices[t+1],ic=indices[t+2];
                    var a=original[ia];var b=original[ib];var c=original[ic];
                    float band=(rest[ia].w+rest[ib].w+rest[ic].w)/3;
                    if(band<-.1f||band>1.2f)continue;
                    float area=Vector3.Cross(transform.TransformVector(b-a),transform.TransformVector(c-a)).magnitude*.5f;
                    float expected=area*4200;
                    int count=Math.Min(remaining,Mathf.FloorToInt(expected)+(Next()<expected%1?1:0));
                    for(int g=0;g<count;g++)
                    {
                        float u=Mathf.Sqrt(Next()),v=Next();
                        var anchor=new Anchor{a=ia,b=ib,c=ic,bary=new Vector3(1-u,u*(1-v),u*v)};
                        var bary=anchor.bary;
                        Vector3 center=a*bary.x+b*bary.y+c*bary.z;
                        Vector3 n=(normal[ia]*bary.x+normal[ib]*bary.y+normal[ic]*bary.z).normalized;
                        Vector3 tangent=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;
                        tangent=Quaternion.AngleAxis(Next()*360,n)*tangent;
                        Vector3 bitangent=Vector3.Cross(n,tangent);
                        float scale=Mathf.Max(.01f,transform.lossyScale.magnitude/Mathf.Sqrt(3));
                        float radius=Mathf.Lerp(.00035f,.00085f,Next())/scale;
                        var points=new[]{center+n*radius*1.1f,center-n*radius*.12f,
                            center+tangent*radius,center+bitangent*radius*.8f,
                            center-tangent*radius,center-bitangent*radius*.8f};
                        int[] faces={0,2,3,0,3,4,0,4,5,0,5,2,1,3,2,1,4,3,1,5,4,1,2,5};
                        var w=skinned?Weight(anchor):default;
                        for(int f=0;f<faces.Length;f+=3)
                        {
                            var fn=Vector3.Cross(points[faces[f+1]]-points[faces[f]],points[faces[f+2]]-points[faces[f]]).normalized;
                            for(int j=0;j<3;j++)
                            {
                                submeshes[sub].Add(vertices.Count);
                                vertices.Add(points[faces[f+j]]);normals.Add(fn);rest.Add(Rest(center));raised.Add(Vector2.right);
                                if(skinned)weights.Add(w);
                                anchors.Add(anchor);
                            }
                        }
                        remaining--;
                    }
                }
            }
            var mesh=new Mesh{name=source.name+" fine sugar",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(3,rest);mesh.SetUVs(4,raised);
            mesh.subMeshCount=submeshes.Length;
            for(int i=0;i<submeshes.Length;i++)mesh.SetTriangles(submeshes[i],i,false);
            if(skinned){mesh.boneWeights=weights.ToArray();mesh.bindposes=source.bindposes;}
            // Grain anchors follow the same blend shape displacement as the skin below them.
            if(source.blendShapeCount>0)
            {
                var delta=new Vector3[original.Length];var dn=new Vector3[original.Length];var dt=new Vector3[original.Length];
                for(int shape=0;shape<source.blendShapeCount;shape++)
                for(int frame=0;frame<source.GetBlendShapeFrameCount(shape);frame++)
                {
                    source.GetBlendShapeFrameVertices(shape,frame,delta,dn,dt);
                    var all=new Vector3[vertices.Count];Array.Copy(delta,all,original.Length);
                    for(int i=0;i<anchors.Count;i++)
                    {
                        var a=anchors[i];all[original.Length+i]=delta[a.a]*a.bary.x+delta[a.b]*a.bary.y+delta[a.c]*a.bary.z;
                    }
                    mesh.AddBlendShapeFrame(source.GetBlendShapeName(shape),source.GetBlendShapeFrameWeight(shape,frame),all,null,null);
                }
            }
            mesh.RecalculateBounds();return mesh;
        }
    }
}

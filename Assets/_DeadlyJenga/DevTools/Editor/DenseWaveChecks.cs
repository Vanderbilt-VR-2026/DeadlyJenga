using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class DenseWaveChecks
    {
        static string Report => ProjectPath.Report("DenseWave-regressions.txt");
        static int checks;
        static DenseWaveChecks()
        {
            EditorApplication.update += () => {
                var request=ProjectPath.Report("DenseWaveChecks.request");
                if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(request);Run();
            };
        }
        [MenuItem("Tools/Deadly Jenga/Performance/Check Dense Wave Optimizations")]
        public static void Run()
        {
            File.WriteAllText(Report,"Deterministic algorithm and mesh regressions. These timings are CPU microbenchmarks, not game FPS.\n");checks=0;
            try {
                var random=new System.Random(19741);var predictions=new List<Vector3>();var queries=new List<Vector3>();var index=new FallingBlockThreats();
                float Next(float range)=>(float)(random.NextDouble()*2-1)*range;
                for(int i=0;i<6098;i++){var p=new Vector3(Next(100),Next(50),Next(100));predictions.Add(p);index.Add(p);}
                for(int i=0;i<2048;i++)queries.Add(new Vector3(Next(110),Next(60),Next(110)));
                // Exact boundaries, negative coordinates and multiple threats in the same cell.
                foreach(var p in new[]{Vector3.zero,new Vector3(-3,0,-3),new Vector3(3,8,0),new Vector3(0,-.5f,0)}){predictions.Add(p);index.Add(p);queries.Add(p);}
                foreach(var q in queries)if(Mathf.Abs(index.Cost(q)-Brute(predictions,q))>.00005f)throw new Exception("Threat score differs at "+q);
                Check(true,"Spatial danger scores match the exhaustive scan for 2052 queries, including signed cell boundaries and height limits");
                var watch=Stopwatch.StartNew();float sum=0;foreach(var q in queries)sum+=Brute(predictions,q);watch.Stop();double brute=watch.Elapsed.TotalMilliseconds;
                watch.Restart();foreach(var q in queries)sum+=index.Cost(q);watch.Stop();
                File.AppendAllText(Report,"CPU 6102 threats x 2052 queries: exhaustive="+brute.ToString("F2")+"ms indexed="+watch.Elapsed.TotalMilliseconds.ToString("F2")+"ms checksum="+sum+"\n");
                index.Clear();Check(index.Cost(Vector3.zero)==0,"Clearing removes all threats");index.Add(Vector3.zero);Check(index.Cost(Vector3.zero)==2.4f,"Pooled index rebuild restores the original score");
                BotPlanningBudget.Reset();double oldLimit=BotPlanningBudget.MaximumMillisecondsPerFrame;
                try {
                    BotPlanningBudget.MaximumMillisecondsPerFrame=4;
                    Check(BotPlanningBudget.TrySense(1000,2),"Planning budget grants an initial sensing credit");
                    BotPlanningBudget.BeginWork();BotPlanningBudget.BeginWork();BotPlanningBudget.EndWork();
                    watch.Restart();while(watch.Elapsed.TotalMilliseconds<6){}
                    Check(!BotPlanningBudget.TrySense(1001,2),"Nested planning work cannot bypass the existing CPU time limit");
                    BotPlanningBudget.EndWork();
                } finally {BotPlanningBudget.MaximumMillisecondsPerFrame=oldLimit;BotPlanningBudget.Reset();}
                var root=new GameObject("Temporary traversal sample regression");
                try {
                    var body=root.AddComponent<Rigidbody>();var box=root.AddComponent<BoxCollider>();
                    var before=new List<Vector3>();var after=new List<Vector3>();
                    for(int i=0;i<128;i++) {
                        root.transform.SetPositionAndRotation(new Vector3(Next(100),Next(50),Next(100)),Quaternion.Euler(Next(180),Next(180),Next(180)));
                        root.transform.localScale=new Vector3(.2f+(float)random.NextDouble()*8,.2f+(float)random.NextDouble()*3,.2f+(float)random.NextDouble()*8);
                        box.center=new Vector3(Next(1),Next(1),Next(1));box.size=new Vector3(.1f+(float)random.NextDouble()*2,.1f+(float)random.NextDouble()*2,.1f+(float)random.NextDouble()*2);
                        OriginalSurfaceSamples(body,box,before);CastleBotNavigation.SurfaceSamples(body,box,after);
                        if(before.Count!=after.Count||before.Where((p,i)=>(p-after[i]).sqrMagnitude>.000001f).Any())throw new Exception("Traversal samples differ at pose "+i);
                    }
                    Check(true,"Traversal samples match the original at 128 translated, rotated, scaled and offset collider poses");
                } finally {UnityEngine.Object.DestroyImmediate(root);}
                foreach(string name in new[]{"Candy Body.asset","Candy Head.asset"}) {
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(ProjectPath.Find(name));Check(mesh&&mesh.isReadable,name+" source is readable");
                    int original=mesh.vertexCount;var originalIndices=mesh.triangles;var bindings=mesh.bindposes;
                    for(int level=1;level<=2;level++) {
                        var reduced=CandyAvatarDetail.Reduced(mesh,level);var indices=reduced.triangles;var positions=reduced.vertices;var skin=reduced.boneWeights;
                        Check(reduced!=mesh&&reduced.vertexCount<original,"Detail "+level+" reduces "+name+" vertices: "+original+" -> "+reduced.vertexCount+", triangles "+originalIndices.Length/3+" -> "+indices.Length/3);
                        Check(indices.Length>0&&indices.All(i=>i>=0&&i<positions.Length),"Reduced triangle indices are valid");
                        Check(positions.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"Reduced positions stay finite");
                        Check(skin.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f),"All reduced skin weights remain normalized");
                        Check(reduced.bindposes.SequenceEqual(bindings)&&reduced.bounds==mesh.bounds,"Bind poses and conservative skin bounds remain exact");
                        Check(ReferenceEquals(reduced,CandyAvatarDetail.Reduced(mesh,level)),"All matching avatars reuse the reduced mesh");
                    }
                    Check(mesh.vertexCount==original&&mesh.triangles.SequenceEqual(originalIndices),"Original near/first-person geometry is unchanged");
                }
                foreach(string menu in new[]{"Tools/Deadly Jenga/Test Tsunami Forces","Tools/Deadly Jenga/Test Disaster Map Contract","Tools/Deadly Jenga/Validate Disaster Setup"})
                    Check(EditorApplication.ExecuteMenuItem(menu),"Executed "+menu);
                File.AppendAllText(Report,"COMPLETE checks="+checks+"\n");
            } catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");UnityEngine.Debug.LogException(e);}
        }
        static void OriginalSurfaceSamples(Rigidbody body,BoxCollider box,List<Vector3> points)
        {
            points.Clear();
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=face%2==0?1:-1;
                Vector3 normal=Vector3.zero;normal[axis]=sign;
                if(body.transform.TransformDirection(normal).y<.4f)continue;
                int a=(axis+1)%3,b=(axis+2)%3;
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    Vector3 local=box.center;local[axis]+=box.size[axis]*.5f*sign;
                    local[a]+=box.size[a]*x*.37f;local[b]+=box.size[b]*z*.37f;
                    points.Add(body.transform.TransformPoint(local));
                }
                int longAxis=box.size[a]*body.transform.lossyScale[a]>=box.size[b]*body.transform.lossyScale[b]?a:b;
                int shortAxis=longAxis==a?b:a;
                if(box.size[longAxis]*body.transform.lossyScale[longAxis]>box.size[shortAxis]*body.transform.lossyScale[shortAxis]*1.5f)
                    for(int side=-1;side<=1;side+=2)
                    {
                        var local=box.center;local[axis]+=box.size[axis]*.5f*sign;local[longAxis]+=box.size[longAxis]*side*.185f;
                        points.Add(body.transform.TransformPoint(local));
                    }
            }
        }
        static float Brute(List<Vector3> threats,Vector3 point)
        {
            float cost=0;
            foreach(var p in threats){if(p.y<point.y-.5f||p.y>point.y+8)continue;float x=p.x-point.x,z=p.z-point.z,d=x*x+z*z;if(d<9)cost+=(3-Mathf.Sqrt(d))*.8f;}
            return cost;
        }
        static void Check(bool pass,string text)
        {if(!pass)throw new Exception(text);checks++;File.AppendAllText(Report,"PASS "+text+"\n");}
    }
}

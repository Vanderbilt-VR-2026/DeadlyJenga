using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad] public static class TowerNative
{
    const string Key="TowerNative.Run";
    static string Output {get {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-towerOutput");return i>=0?args[i+1]:"Results";} }
    static TowerNative()
    {
        EditorApplication.update+=()=>{if(EditorApplication.isPlaying && SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);Measure();}};
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"native.txt"),"START isolated native Unity 6000.3.23f1 geometry, same project physics settings. No production bots or frame-rate claims.\n");
        SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
    }
    static Material[] Palette()
    {
        Color[] colors={new(.12f,.82f,.7f),new(1,.39f,.24f),new(.58f,.35f,1),new(1,.76f,.16f)};
        return colors.Select(c=>{var m=new Material(Shader.Find("Standard"));m.color=c;m.SetFloat("_Glossiness",.72f);m.SetFloat("_Metallic",.08f);return m;}).ToArray();
    }
    static void Measure()
    {
        int code=0;var report=new StringBuilder(File.ReadAllText(Path.Combine(Output,"native.txt")));var palette=Palette();
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.7f,.82f,.95f);RenderSettings.ambientEquatorColor=new Color(.43f,.52f,.64f);RenderSettings.ambientGroundColor=new Color(.36f,.32f,.25f);
        var original=SceneManager.GetActiveScene();
        try
        {
            for(int trial=0;trial<(Environment.GetCommandLineArgs().Contains("-compareArchSolvers")?3:1);trial++) foreach(bool city in new[]{true,false})for(int district=0;district<10;district++)
            {
                var args=Environment.GetCommandLineArgs();int filter=Array.IndexOf(args,"-towerDistrict");if(filter>=0 && !args[filter+1].Split(',').Contains(district.ToString()))continue; var scene=SceneManager.CreateScene("Physics district "+city+district+" trial "+trial,new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
                var go=new GameObject("Foundry district "+district);var map=go.AddComponent<TowerExhibitionMap>();map.generateOnStart=false;map.city=city;map.district=district;map.palette=palette;
                var friction=new PhysicsMaterial("Copied structural friction"){staticFriction=.65f,dynamicFriction=.55f,frictionCombine=PhysicsMaterialCombine.Average,bounciness=0};map.friction=friction;
                var ground=new GameObject("Island foundation");ground.transform.SetParent(go.transform,false);ground.AddComponent<BoxCollider>();map.foundation=ground.transform;map.Rebuild();ground.transform.localPosition=TowerExhibitionLayout.Centers[district]+new Vector3(0,-1.02f,0);ground.transform.localScale=new Vector3(160,2,150);Physics.SyncTransforms();
                var physics=scene.GetPhysicsScene();var bodies=map.Blocks;var shapes=go.GetComponentsInChildren<Collider>().Where(c=>c.attachedRigidbody).ToArray();
                report.AppendLine("\nDISTRICT "+(district+(city?11:1))+" "+TowerExhibitionLayout.Names(map.city)[district]+" solver="+bodies[0].solverIterations+"/"+bodies[0].solverVelocityIterations+" bodies="+bodies.Count+" colliders="+shapes.Length);
                int penetrations=0;float deepest=0;
                for(int i=0;i<shapes.Length;i++)for(int j=i+1;j<shapes.Length;j++)
                {
                    var a=shapes[i];var b=shapes[j];if(a.attachedRigidbody==b.attachedRigidbody||!a.bounds.Intersects(b.bounds))continue;
                    if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float d)&&d>.04f)
                    {penetrations++;deepest=Mathf.Max(deepest,d);if(penetrations<=15)report.AppendLine("OVERLAP "+a.attachedRigidbody.name+" / "+b.attachedRigidbody.name+" "+d.ToString("F3")+"m");}
                }
                report.AppendLine("GEOMETRY overlaps>.04m="+penetrations+" deepest="+deepest.ToString("F3"));
                var loads=bodies.Select(rb=>rb.GetComponent<TowerSectionWaterLoad>()).ToArray();
                report.AppendLine("WATER cached exposure m2="+loads.Min(l=>l.FrontalArea).ToString("F2")+".."+loads.Max(l=>l.FrontalArea).ToString("F2")+" resistance="+loads.Min(l=>l.WaterResistance).ToString("F3")+".."+loads.Max(l=>l.WaterResistance).ToString("F3")+" colliderLookup="+shapes.All(shape=>ReferenceEquals(HitAbilities.Get<IWaterResistance>(shape),shape.attachedRigidbody.GetComponent<TowerSectionWaterLoad>())));
                var positions=bodies.Select(b=>b.worldCenterOfMass).ToArray();var rotations=bodies.Select(b=>b.rotation).ToArray();
                var watch=Stopwatch.StartNew();for(int step=0;step<750;step++)physics.Simulate(.02f);watch.Stop();
                float drift=0,tilt=0,rms=0;int moved=0,awake=0;
                for(int i=0;i<bodies.Count;i++)
                {
                    var rb=bodies[i];float d=Vector3.Distance(rb.worldCenterOfMass,positions[i]);drift=Mathf.Max(drift,d);tilt=Mathf.Max(tilt,Quaternion.Angle(rb.rotation,rotations[i]));rms+=rb.linearVelocity.sqrMagnitude;if(d>.6f)moved++;if(!rb.IsSleeping())awake++;
                }
                rms=Mathf.Sqrt(rms/bodies.Count);
                report.AppendLine($"SETTLE 15s maxDrift={drift:F4}m maxTilt={tilt:F3}deg rmsSpeed={rms:F4}m/s moved>.6m={moved} awake={awake}/{bodies.Count} meanNativeStep={watch.Elapsed.TotalMilliseconds/750:F3}ms. Native-only timing, not FPS.");
                foreach(var e in bodies.Select((rb,i)=>new {rb,i,d=Vector3.Distance(rb.worldCenterOfMass,positions[i])}).Where(e=>e.d>.6f).OrderByDescending(e=>e.d).Take(15))report.AppendLine("MOVED "+e.rb.name+" "+e.d.ToString("F2")+"m");
                Sync(bodies);Capture(map,scene,district,"settled");
                var points=new List<Vector3>();int count=0,clear=0;var bands=new HashSet<int>();var hits=new Collider[64];
                foreach(var rb in bodies)
                {
                    DeadlyJenga.CastleBots.BotGeometrySamples.Fill(rb,points);count+=points.Count;
                    foreach(var p in points)
                    {
                        if(physics.OverlapCapsule(p+Vector3.up*.25f,p+Vector3.up*.9f,.18f,hits,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)>0)continue;
                        clear++;bands.Add(Mathf.RoundToInt(p.y*2));
                    }
                }
                int routeSamples=0,routeBlocked=0,turnSamples=0,turnBlocked=0;var routeProblems=new List<string>();
                for(int index=0;index<map.Authored.Count;index++)
                {
                    var module=map.Authored[index];var rb=bodies[index];var inv=Quaternion.Inverse(module.shapes[0].rotation);
                    var flights=module.shapes.Where(shape=>shape.vertices!=null&&shape.vertices[6].y-shape.vertices[4].y>.5f&&shape.vertices[6].y-shape.vertices[4].y<6&&Vector3.Distance(shape.vertices[0],shape.vertices[4])<.001f).ToArray();
                    Vector3 Point(MayhemShape shape,int v)=>rb.transform.TransformPoint(inv*(shape.center+shape.rotation*shape.vertices[v]));
                    foreach(var flight in flights)
                    {
                        var from=(Point(flight,4)+Point(flight,5))*.5f;var to=(Point(flight,6)+Point(flight,7))*.5f;
                        for(int sample=0;sample<=22;sample++)
                        {
                            var foot=Vector3.Lerp(from,to,sample/22f)+Vector3.up*.06f;routeSamples++;
                            if(physics.OverlapCapsule(foot+Vector3.up*.24f,foot+Vector3.up*1.46f,.24f,hits,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)>0)
                            {routeBlocked++;if(routeProblems.Count<10)routeProblems.Add(module.purpose+" foot="+foot+" hit="+hits[0].attachedRigidbody?.name);}
                        }
                    }
                    foreach(var firstFlight in flights)foreach(var nextFlight in flights)
                    {
                        var af=(Point(firstFlight,4)+Point(firstFlight,5))*.5f;var at=(Point(firstFlight,6)+Point(firstFlight,7))*.5f;
                        var bf=(Point(nextFlight,4)+Point(nextFlight,5))*.5f;var bt=(Point(nextFlight,6)+Point(nextFlight,7))*.5f;
                        var ad=Vector3.ProjectOnPlane(at-af,Vector3.up).normalized;var bd=Vector3.ProjectOnPlane(bt-bf,Vector3.up).normalized;
                        var from=at+ad*.9f;var to=bf-bd*.9f;
                        if(Mathf.Abs(at.y-bf.y)>.02f||Vector3.Dot(ad,bd)>-.9f||Vector3.Distance(from,to)>5||Vector3.Distance(from,to)<2)continue;
                        for(int sample=0;sample<=12;sample++)
                        {
                            var foot=Vector3.Lerp(from,to,sample/12f)+Vector3.up*.06f;turnSamples++;
                            if(physics.OverlapCapsule(foot+Vector3.up*.24f,foot+Vector3.up*1.46f,.24f,hits,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)>0)
                            {turnBlocked++;if(routeProblems.Count<10)routeProblems.Add("TURN "+module.purpose+" foot="+foot+" hit="+hits[0].attachedRigidbody?.name);}
                        }
                    }
                }
                report.AppendLine("FULL-SIZE TURN CAPSULE samples="+turnSamples+" blocked="+turnBlocked);report.AppendLine("FULL-SIZE ROUTE CAPSULE height=1.7 radius=.24 flightSamples="+routeSamples+" blocked="+routeBlocked+" (clearance only, not the production motor)");if(routeProblems.Count>0)report.AppendLine(string.Join("; ",routeProblems));
                string state=map.SaveLayout();int revision=map.Revision;
                bool invalid=!map.LoadLayout(state,bodies.Count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision;
                map.SetReplica(true);bool replicas=bodies.All(b=>b.isKinematic);map.SetReplica(false);
                report.AppendLine("CONTRACT rootCollider="+bodies.All(b=>b.GetComponent<Collider>())+" registeredClimb="+shapes.All(s=>Climbables.TryGet(s,out var c)&&c.Body==s.attachedRigidbody)+" invalidCountRejected="+invalid+" replica="+replicas+" dynamic="+bodies.All(b=>!b.isKinematic));
                report.AppendLine("PRODUCTION BOT SAMPLER points="+count+" capsuleClear="+clear+" halfMetreBands="+bands.Count+". Surface coverage, not route completion.");
                var settled=bodies.Select(b=>b.worldCenterOfMass).ToArray();
                if(!city&&(district==5||district==8||district==9))
                {
                    string purpose=district==5?"Removable drop shoe":district==8?"Wing receiving foot":"Loose crown compression keystone";
                    foreach(var rb in bodies.Where(b=>b.name.Contains(purpose)).Take(district==5?2:1))rb.gameObject.SetActive(false);
                    foreach(var rb in bodies)if(rb.gameObject.activeSelf)rb.WakeUp();for(int step=0;step<300;step++)physics.Simulate(.02f);
                    report.AppendLine("MECHANISM "+purpose+" otherMoved>.5m="+bodies.Where((rb,i)=>rb.gameObject.activeSelf&&Vector3.Distance(rb.worldCenterOfMass,settled[i])>.5f).Count());
                    if(district==8)foreach(var rb in bodies.Where(rb=>rb.GetComponent<HingeJoint>()))report.AppendLine("WING "+rb.name+" rotationFromAuthored="+Quaternion.Angle(rb.rotation,Quaternion.identity).ToString("F2")+" speed="+rb.linearVelocity.magnitude.ToString("F3"));if(district==5){var body=bodies.First(rb=>rb.name.Contains("Two-floor dropping cassette"));int i=bodies.ToList().IndexOf(body);report.AppendLine("DROP "+(settled[i].y-body.worldCenterOfMass.y).ToString("F3")+"m speed="+body.linearVelocity.magnitude.ToString("F3"));}
                    Sync(bodies);Capture(map,scene,district,"released");
                }
                var candidate=bodies.FirstOrDefault(rb=>rb.gameObject.activeSelf&&rb.position.y>.5f&&rb.position.y<18)??bodies[1];
                candidate.WakeUp();candidate.AddForceAtPosition(new Vector3(22,0,9),candidate.worldCenterOfMass+Vector3.up*4,ForceMode.VelocityChange);report.AppendLine("Target "+candidate.name+" pulse22/9 offset4m");for(int step=0;step<600;step++)physics.Simulate(.02f);
                report.AppendLine("IMPACT 22m/s X + 9m/s Z, offset4m lower member moved>.5m="+bodies.Where((rb,i)=>rb.gameObject.activeSelf&&Vector3.Distance(rb.worldCenterOfMass,settled[i])>.5f).Count());
                Sync(bodies);Capture(map,scene,district,"impact");
                File.WriteAllText(Path.Combine(Output,"native.txt"),report.ToString());
                Object.DestroyImmediate(go);Object.DestroyImmediate(friction);SceneManager.SetActiveScene(original);SceneManager.UnloadSceneAsync(scene);
            }
            report.AppendLine("COMPLETE selected districts; gravity, collisions and native support release. No bots, gameplay movement, networking or tsunami in this isolated runner.");
            File.WriteAllText(Path.Combine(Output,"native.txt"),report.ToString());
        }
        catch(Exception e){code=1;report.AppendLine("FAIL "+e);File.WriteAllText(Path.Combine(Output,"native.txt"),report.ToString());UnityEngine.Debug.LogException(e);}
        finally{foreach(var m in palette)Object.DestroyImmediate(m);SessionState.SetBool(Key,false);EditorApplication.Exit(code);}
    }
    static void Sync(IReadOnlyList<Rigidbody> bodies){foreach(var rb in bodies)rb.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();}
    static void Capture(TowerExhibitionMap map,Scene scene,int district,string state)
    {
        var go=new GameObject("Review camera");var camera=go.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.6f,.85f);camera.fieldOfView=48;camera.farClipPlane=1500;
        var center=TowerExhibitionLayout.Centers[district]+Vector3.up*23;camera.transform.position=center+new Vector3(.65f,.7f,-1).normalized*170;camera.transform.LookAt(center);
        var sunGo=new GameObject("Sun");var sun=sunGo.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
        var sand=GameObject.CreatePrimitive(PrimitiveType.Cube);sand.transform.position=TowerExhibitionLayout.Centers[district]+new Vector3(0,-1.02f,0);sand.transform.localScale=new Vector3(160,2,150);Object.DestroyImmediate(sand.GetComponent<Collider>());var mat=new Material(Shader.Find("Standard")){color=new Color(.78f,.69f,.46f)};sand.GetComponent<Renderer>().sharedMaterial=mat;
        var rt=new RenderTexture(1200,800,24);var previous=RenderTexture.active;var image=new Texture2D(1200,800,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,(map.city?"CityExhibition-":"TowerExhibition-")+(district+(map.city?11:1)).ToString("D2")+"-"+state+".png"),image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);Object.DestroyImmediate(sunGo);Object.DestroyImmediate(sand);Object.DestroyImmediate(mat);}
    }
}











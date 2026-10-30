using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BreakwaterVillageChecks
    {
        const string Key="BreakwaterVillageChecks.Pending";
        static readonly StringBuilder report=new StringBuilder();
        static string Output=>Path.GetFullPath("ArtDirection/BreakwaterVillage");
        static int errors;
        static BreakwaterVillageChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+".Game",false))
                {SessionState.SetBool(Key+".Game",false);EditorApplication.delayCall+=BreakwaterVillagePlayChecks.Run;return;}
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);EditorApplication.delayCall+=Run;
            };
        }
        public static void BuildAndPlayTest()
        {SessionState.SetBool(Key+".Full",true);BuildAndTest();}
        [MenuItem("Tools/Deadly Jenga/Validation/Build and Test Breakwater Village")]
        public static void BuildAndTest()
        {
            BreakwaterVillageBuilder.Build();
            if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))throw new InvalidOperationException("Disaster setup validator is unavailable.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;report.AppendLine("ERROR "+message);}}
        static void Run()
        {
            int result=0;report.Clear();errors=0;Application.logMessageReceived+=Log;
            report.AppendLine("ACTUAL UNITY/PHYSX: connected dynamic village, finite native FixedJoints, no magnetic pinning.\nNative fixed-step tests call the real registered disaster implementations under manual stepping. Captures render the actual simulated scene. Timings include physics and fixture bookkeeping; NOT frame rates.\n");
            var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            Directory.CreateDirectory(Output);
            GameObject root=null;
            try
            {
                var prefab=Resources.Load<GameObject>("ModeMaps/Mode_breakwater-village");Require(prefab,"map prefab exists");
                root=Object.Instantiate(prefab);var map=root.GetComponent<BreakwaterVillageMap>();map.Rebuild();Services.World=map;
                Lighting();
                report.AppendLine("Bodies="+map.pieces.Length+" structural="+map.pieces.Count(p=>p.structural)+" native connections="+map.pieces.Sum(p=>p.connections.Length));
                Require(map.pieces.All(p=>p.Body&&!p.Body.isKinematic&&p.GetComponent<CastleBlock>()),"all authored members remain dynamic and climbable");
                AuditContacts(map);
                Shot("00-authored-village",new Vector3(-47,35,-52),new Vector3(0,3,5));
                var start=map.pieces.Select(p=>p.Body.position).ToArray();
                int failuresLogged=0;
                foreach(var p in map.pieces)p.ConnectionFailed+=(piece,force)=>{if(failuresLogged++<30)report.AppendLine($"JOINT FAILURE {piece.building}/{piece.purpose}: force={force:F1} health={piece.Health:F1}");};
                Step(map,500,null);Metrics(map,start,"10s quiet settle");
                foreach(var p in map.pieces)
                    if(p.BrokenConnections>0||p.structural&&Vector3.Distance(p.Body.position,start[Array.IndexOf(map.pieces,p)])>.15f)
                        report.AppendLine($"QUIET MEMBER {p.building}/{p.purpose}: broken={p.BrokenConnections} health={p.Health:F2} move={Vector3.Distance(p.Body.position,start[Array.IndexOf(map.pieces,p)]):F3}m");
                Shot("01-village-before",new Vector3(-47,35,-52),new Vector3(0,3,5));
                Shot("02-main-street-before",new Vector3(-1,2,-23),new Vector3(0,3,5));
                Shot("03-inn-before",new Vector3(-28,14,-5),new Vector3(-13,4.5f,11));
                Shot("04-cottage-before",new Vector3(7,8,-24),new Vector3(18,3,-8));
                Shot("05-courtyard-before",new Vector3(9,7,-18),new Vector3(-1,2,-1));
                Require(map.BrokenConnections==0,"quiet village has no broken connections");
                Require(map.pieces.Where(p=>p.structural).Select(p=>Vector3.Distance(p.Body.position,start[Array.IndexOf(map.pieces,p)])).Max()<.15f,"quiet structure remains within 15cm");
                string clean=map.SaveLayout();int revision=map.Revision;
                Require(!map.LoadLayout(clean,map.pieces.Length-1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision,"invalid layout count rejects without mutation");
                var victim=map.pieces.Where(p=>p.purpose.StartsWith("Balcony slab")&&p.building=="THE LANTERN INN").OrderByDescending(p=>p.Body.position.y).First();
                Vector3 old=victim.Body.position;victim.Eliminate(null);Step(map,180,null);
                Require(Vector3.Distance(victim.Body.position,old)>.5f,"severing balcony supports produces actual falling motion");
                string broken=map.SaveLayout();Require(broken!=clean,"fracture state is serialized");
                map.SetReplica(true);Require(map.pieces.All(p=>p.Body.isKinematic),"replica makes all gameplay bodies kinematic");map.SetReplica(false);
                Require(map.LoadLayout(clean,map.pieces.Length,new Pose(Vector3.zero,Quaternion.identity),Vector3.one),"clean layout restores");
                Step(map,300,null);Require(map.BrokenConnections==0,"clean layout settles without residual constraint damage");start=map.pieces.Select(p=>p.Body.position).ToArray();
                var quake=MakeHazard("Earthquake",new Dictionary<string,float>{{"force",14},{"frequency",1.35f},{"vertical",.12f},{"warning",0},{"duration",12}});
                quake.Begin();Require(quake.Active,"actual earthquake validates and begins");
                Step(map,600,quake,"TickEarthquake");quake.End();Object.DestroyImmediate(quake.gameObject);Step(map,200,null);
                Metrics(map,start,"12s earthquake + 4s settle");Require(map.BrokenConnections>0,"earthquake breaks actual structural connections");
                Shot("06-village-after-earthquake",new Vector3(-47,35,-52),new Vector3(0,3,5));
                Shot("07-street-after-earthquake",new Vector3(-1,2,-23),new Vector3(0,3,5));
                map.Rebuild();Step(map,300,null);Require(map.BrokenConnections==0,"reset after earthquake settles intact before meteors");start=map.pieces.Select(p=>p.Body.position).ToArray();
                UnityEngine.Random.InitState(91827);
                var meteor=MakeHazard("MeteorShower",new Dictionary<string,float>{{"size",3.6f},{"deviation",.6f},{"frequency",3},{"force",18},{"warning",0},{"duration",24}});
                meteor.Begin();Require(meteor.Active,"actual meteor shower validates and begins");
                Step(map,1200,meteor,"TickMeteors");meteor.End();Object.DestroyImmediate(meteor.gameObject);Step(map,200,null);
                Metrics(map,start,"24s seeded meteor shower + 4s settle");Require(map.BrokenConnections>0,"meteor shower breaks actual structural connections");
                Shot("08-village-after-meteors",new Vector3(-47,35,-52),new Vector3(0,3,5));
                Shot("09-inn-after-meteors",new Vector3(-28,14,-5),new Vector3(-13,4.5f,11));
                Require(map.pieces.All(p=>Finite(p.Body.position)&&Finite(p.Body.linearVelocity)),"no NaN or infinite body state after disasters");
                map.Rebuild();Step(map,300,null);Metrics(map,map.pieces.Select(p=>p.transform.parent.TransformPoint(p.homePosition)).ToArray(),"post-destruction reset");
                foreach(var p in map.pieces)if(p.BrokenConnections>0)report.AppendLine($"RESET FAILURE {p.building}/{p.purpose}: {p.BrokenConnections}");
                Require(map.BrokenConnections==0,"reset restores all native connections after destruction");
                for(int cycle=0;cycle<3;cycle++)
                {map.Rebuild();Step(map,300,null);Require(map.BrokenConnections==0,"repeated reset stays intact, cycle "+(cycle+1));}
                Require(map.pieces.All(p=>p.gameObject.activeInHierarchy),"stable member identities survive reset");
                Require(errors==0,"no runtime errors");report.AppendLine("PASS native suite. Still requires actual Game scene controls/bots, two-player test and Quest.");
            }
            catch(Exception e){report.AppendLine("FAIL "+e);UnityEngine.Debug.LogException(e);result=1;}
            finally
            {
                Physics.simulationMode=mode;Application.logMessageReceived-=Log;
                File.WriteAllText(ProjectPath.Report("BreakwaterPhysics.txt"),report.ToString());File.WriteAllText(Path.Combine(Output,"Physics-results.txt"),report.ToString());
                if(root)Object.DestroyImmediate(root);
            }
            if(Application.isBatchMode&&result==0&&SessionState.GetBool(Key+".Full",false))
            {SessionState.SetBool(Key+".Full",false);SessionState.SetBool(Key+".Game",true);EditorApplication.ExitPlaymode();}
            else if(Application.isBatchMode)EditorApplication.Exit(result);else EditorApplication.ExitPlaymode();
        }
        public static BasicDisaster MakeHazard(string name,Dictionary<string,float> settings)
        {
            var prefab=Resources.Load<HazardCatalog>("DisasterCatalog").prefabs.First(p=>p.GetComponent<BasicDisaster>()&&p.name==name);
            var hazard=Object.Instantiate(prefab).GetComponent<BasicDisaster>();var values=hazard.Parameters.Select(p=>settings.TryGetValue(p.id,out float value)?value:p.initial).ToArray();hazard.Configure(values);return hazard;
        }
        static void AuditContacts(BreakwaterVillageMap map)
        {
            int overlaps=0;
            var ground=map.GetComponentsInChildren<BoxCollider>().Where(c=>!c.attachedRigidbody&&c.gameObject.layer==0).ToArray();
            foreach(var piece in map.pieces)foreach(var shape in piece.GetComponents<BoxCollider>())foreach(var surface in ground)
                if(Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,surface,surface.transform.position,surface.transform.rotation,out _,out float penetration)&&penetration>.015f)
                {overlaps++;report.AppendLine($"INITIAL GROUND PENETRATION {piece.building}/{piece.purpose}: {penetration:F3}m");}
            for(int i=0;i<map.pieces.Length;i++)for(int j=i+1;j<map.pieces.Length;j++)
            {
                var a=map.pieces[i];var b=map.pieces[j];
                if(!a.structural&&!b.structural||a.connections.Any(c=>c.target==b)||b.connections.Any(c=>c.target==a))continue;
                foreach(var ca in a.GetComponents<BoxCollider>())foreach(var cb in b.GetComponents<BoxCollider>())
                {
                    if(!ca.bounds.Intersects(cb.bounds))continue;
                    if(Physics.ComputePenetration(ca,ca.transform.position,ca.transform.rotation,cb,cb.transform.position,cb.transform.rotation,out var direction,out float depth)&&depth>.015f)
                    {if(overlaps++<40)report.AppendLine($"INITIAL PENETRATION {a.building}/{a.purpose} / {b.building}/{b.purpose}: {depth:F3}m");}
                }
            }
            report.AppendLine("Initial unjoined structural contact penetrations over 15mm="+overlaps);
        }
        static void Step(BreakwaterVillageMap map,int steps,BasicDisaster hazard,string method=null)
        {
            var tick=method==null?null:typeof(BasicDisaster).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic);
            var age=typeof(BasicDisaster).GetField("age",BindingFlags.Instance|BindingFlags.NonPublic);
            var timer=Stopwatch.StartNew();
            for(int i=0;i<steps;i++)
            {
                if(hazard&&(method!="TickEarthquake"||i%5==0))
                {age.SetValue(hazard,i*.02f);tick.Invoke(hazard,new object[]{method=="TickEarthquake"?.1f:.02f});}
                Physics.Simulate(.02f);
                // Destroy requests from impact fracture are delayed in Play Mode; let them stop constraining immediately.
                foreach(var p in map.pieces){p.RefreshBreaks();p.GetComponent<CastleBlock>().SyncSurface();p.GetComponent<VillageExtraSurfaces>()?.Sync();}
                Physics.SyncTransforms();
            }
            timer.Stop();report.AppendLine($"PHYSICS steps={steps} mean={timer.Elapsed.TotalMilliseconds/steps:F3}ms incl test bookkeeping");
            foreach(var p in map.pieces)p.GetComponent<CastleBlock>().SyncSurface();Physics.SyncTransforms();
            File.WriteAllText(ProjectPath.Report("BreakwaterPhysics.txt"),report.ToString());
        }
        static void Metrics(BreakwaterVillageMap map,Vector3[] initial,string label)
        {
            int moved=0;float max=0;for(int i=0;i<map.pieces.Length;i++){float d=Vector3.Distance(initial[i],map.pieces[i].Body.position);if(map.pieces[i].structural&&d>.5f)moved++;max=Mathf.Max(max,d);}
            report.AppendLine($"RESULT {label}: brokenConnections={map.BrokenConnections}; structuralMovedOver0.5m={moved}; maxDisplacement={max:F3}m; awake={map.pieces.Count(p=>!p.Body.IsSleeping())}");
        }
        static void Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.66f,.78f,.93f);RenderSettings.ambientEquatorColor=new Color(.43f,.5f,.6f);RenderSettings.ambientGroundColor=new Color(.37f,.29f,.19f);RenderSettings.fog=false;
            var sun=new GameObject("Evidence sunlight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
        }
        public static void Shot(string name,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Evidence camera");var camera=go.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);
            camera.fieldOfView=53;camera.nearClipPlane=.05f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.42f,.7f,.9f);
            if(name.Contains("village")||name.StartsWith("10-")||name.StartsWith("11-")||name.StartsWith("12-")){camera.orthographic=true;camera.orthographicSize=27;}
            var texture=new RenderTexture(1600,1100,24);var old=RenderTexture.active;
            try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;var png=new Texture2D(1600,1100,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1100),0,0);png.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),png.EncodeToPNG());Object.DestroyImmediate(png);}
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(go);}
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        static void Require(bool pass,string text){report.AppendLine((pass?"PASS ":"FAIL ")+text);if(!pass)throw new InvalidOperationException(text);}
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class CastleVariantChecks
    {
        const string Key="CastleVariantChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static int checks,errors;
        static string Report=>ProjectPath.Report("CastleVariants.txt");
        static CastleVariantChecks()
        {
            EditorApplication.update+=()=>{var request=ProjectPath.Report("CastleVariants.request");if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(request);Run();};
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){checks=errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>new GameObject("Castle variant validation").AddComponent<CastleVariantDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);var saved=JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes;if(saved.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(saved);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);File.AppendAllText(Report,"PASS Original scenes and choices restored\n");if(Application.isBatchMode)EditorApplication.Exit(File.ReadAllText(Report).Contains("FAIL ")||errors>0?1:0);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Castle Variants")]
        public static void Run()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            File.WriteAllText(Report,"Actual Game, dynamic PhysX and replicated layout checks for experimental castle variants.\n");EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static IEnumerator Play()
        {
            checks=errors=0;File.WriteAllText(Report,"Actual Game and isolated dynamic castle physics.\n");
            yield return Checks();File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");
        }
        public static void PhysicsOnly()
        {
            string report=ProjectPath.Report("CastlePhysicsTuning.txt");File.WriteAllText(report,"Actual Unity PhysX in an empty temporary Edit Mode scene, no presentation callbacks.\n");
            var setup=EditorSceneManager.GetSceneManagerSetup();if(setup.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes first.");
            var previous=Services.World;var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                foreach(string id in new[]{"castle-gaps","castle-catwalks","castle-four-keeps"})
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);GameObject root=null;
                    try
                    {
                        root=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_"+id));var map=root.GetComponent<CastleGenerator>();map.Rebuild();var bodies=map.Bodies.ToArray();var positions=bodies.Select(b=>b.position).ToArray();var rotations=bodies.Select(b=>b.rotation).ToArray();
                        Physics.SyncTransforms();
                        for(int i=0;i<bodies.Length;i++)for(int j=i+1;j<bodies.Length;j++)
                        {
                            var a=bodies[i].GetComponent<BoxCollider>();var b=bodies[j].GetComponent<BoxCollider>();if(!a.bounds.Intersects(b.bounds))continue;
                            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.005f)
                                File.AppendAllText(report,"OVERLAP "+id+" "+bodies[i].name+" "+bodies[i].transform.localScale+" and "+bodies[j].name+" "+bodies[j].transform.localScale+" depth="+depth+"\n");
                        }
                        for(int step=0;step<1500;step++)Physics.Simulate(.02f);
                        foreach(var item in bodies.Select((b,i)=>new{body=b,start=positions[i],drift=Vector3.Distance(b.position,positions[i]),tilt=Quaternion.Angle(b.rotation,rotations[i])}).OrderByDescending(v=>v.drift).Take(12))
                            File.AppendAllText(report,"MEASURE "+id+" "+item.body.name+" size="+item.body.transform.localScale+" start="+item.start+" end="+item.body.position+" drift="+item.drift+" tilt="+item.tilt+"\n");
                    }
                    finally{if(root)Object.DestroyImmediate(root);}
                }
            }
            finally{Physics.simulationMode=mode;if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Services.World=previous;}
            File.AppendAllText(report,"COMPLETE\n");
        }
        static void Log(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool pass,string text){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new Exception(text);checks++;}
        static IEnumerator Guard()
        {
            var iterator=Checks();while(true){bool more=false;Exception error=null;try{more=iterator.MoveNext();}catch(Exception e){error=e;}if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more)break;yield return iterator.Current;}
            (iterator as IDisposable)?.Dispose();File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");if(Application.isBatchMode){SessionState.SetBool(Key,false);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);EditorApplication.Exit(File.ReadAllText(Report).Contains("FAIL ")||errors>0?1:0);}else EditorApplication.ExitPlaymode();
        }
        static IEnumerator Checks()
        {
            float end=Time.realtimeSinceStartup+40;while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<end)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session,"Game initializes");session.SurvivorBots.AutoFill=false;if(session.Social!=null)session.Social.VoiceEnabled=false;session.Survivor().InputSuspended=true;
            var rules=session.Modes.Rules.Copy();rules.countdownSeconds=0;rules.automaticDisasters=false;rules.fallDamage=false;rules.island=IslandRule.Walkable;session.Modes.Configure(rules);session.Modes.StartRound();
            foreach(string id in new[]{"castle-catwalks","castle-four-keeps"})
            {
                var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+id);Check(prefab,id+" resource exists");
                var previous=Services.World;var oldScene=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene(id+" gravity validation",new CreateSceneParameters(LocalPhysicsMode.Physics3D));GameObject root=null,replica=null;
                try
                {
                    SceneManager.SetActiveScene(scene);root=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(root,scene);var map=root.GetComponent<CastleGenerator>();map.Rebuild();var layout=CastleLayout.Build(map.settings);var bodies=map.Bodies.ToArray();
                    Check(bodies.Length==layout.Count&&bodies.All(b=>b.useGravity&&!b.isKinematic&&b.constraints==RigidbodyConstraints.None),id+" uses only loose gravity-driven bodies");
                    Check(map.PlayableBounds.size.x==61&&map.foundation.localScale==new Vector3(61,1,61),id+" foundation and disaster bounds are 61m");
                    Check(bodies.Select((b,i)=>Vector3.Distance(b.transform.localScale,layout[i].BlockSize(map.settings))<.001f).All(v=>v),id+" mixed block dimensions reach actual colliders");
                    if(map.settings.structureVariant==CastleStructure.Catwalks)Check(layout.Count(p=>p.Size.y==.4f)==36,"Eight cross decks plus four external bridges and end bearings");
                    else {Check(layout.Count(p=>p.Section==CastleSection.Keep&&p.Size.x==6)==256,"Four small-block octagonal keeps reach red rooftop level");Check(map.PlayableBounds.size.y==94,"Full 30-course upper keep sits above the four supporting keeps at 94m overall");}
                    replica=Object.Instantiate(prefab);var copy=replica.GetComponent<CastleGenerator>();Check(copy.LoadLayout(map.SaveLayout(),bodies.Length,new Pose(new Vector3(200,0,0),Quaternion.identity),Vector3.one),id+" deterministic mixed-size layout restores through existing sync contract");copy.SetReplica(true);Check(copy.Bodies.All(b=>b.isKinematic),id+" replicas suspend local physics");Object.DestroyImmediate(replica);replica=null;
                    var positions=bodies.Select(b=>b.position).ToArray();var rotations=bodies.Select(b=>b.rotation).ToArray();var physics=scene.GetPhysicsScene();Physics.SyncTransforms();for(int i=0;i<1500;i++)physics.Simulate(.02f);
                    float drift=bodies.Select((b,i)=>Vector3.Distance(b.position,positions[i])).Max();float tilt=bodies.Select((b,i)=>Quaternion.Angle(b.rotation,rotations[i])).Max();File.AppendAllText(Report,"MEASURE "+id+" bodies="+bodies.Length+" 30s max drift="+drift+"m max tilt="+tilt+"deg\n");
                    foreach(var item in bodies.Select((b,i)=>new{body=b,drift=Vector3.Distance(b.position,positions[i]),tilt=Quaternion.Angle(b.rotation,rotations[i])}).OrderByDescending(v=>v.tilt).Take(4))File.AppendAllText(Report,"DETAIL "+id+" "+item.body.name+" size="+item.body.transform.localScale+" drift="+item.drift+" tilt="+item.tilt+"\n");
                    Check(drift<.7f&&tilt<5&&bodies.All(b=>b.position.y>0),id+" stays standing under 30 seconds of dynamic gravity");
                    var beam=bodies.OrderByDescending(b=>b.position.y).First();var before=beam.position;beam.AddForce(Vector3.up*8+Vector3.right*4,ForceMode.VelocityChange);float excursion=0;for(int i=0;i<50;i++){physics.Simulate(.02f);excursion=Mathf.Max(excursion,Vector3.Distance(beam.position,before));}Check(excursion>.5f,id+" exposed top block can be displaced by an impulse");
                }
                finally{SceneManager.SetActiveScene(oldScene);if(replica)Object.DestroyImmediate(replica);if(root)Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);Services.World=previous;}
                MatchSetup.MapId="mode-"+id;MatchSetup.ApplyMap();yield return new WaitForSeconds(.3f);var live=Services.World as CastleGenerator;Check(live&&live.settings.structureVariant!=CastleStructure.Original,id+" loads from the real match map selector");
                session.Survivor().Place(live.SafeSpawnPoint(),Quaternion.identity);
                typeof(QueuedFeatureChecks).GetMethod("Capture",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{live.Center+(id=="castle-four-keeps"?new Vector3(95,110,110):new Vector3(78,67,88)),live.Center+Vector3.up*(id=="castle-four-keeps"?46:24),id+".png"});
            }
            Check(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster validator executes");Check(errors==0,"No unexpected runtime errors");
        }
    }
    public sealed class CastleVariantDriver:MonoBehaviour { }
}

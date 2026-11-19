using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class OrdnanceVisualChecks
    {
        const string Key="OrdnanceVisualChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("OrdnanceVisualChecks.txt");
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public string choices;public bool background,fromLobby,local;public float volume;}
        static OrdnanceVisualChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),background=Application.runInBackground,volume=AudioListener.volume,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity ordnance presentation validation\n"+root+"\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool(Key+"Entered",true);errors=checks=0;failed=false;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();run=Flatten(Validate());new GameObject("Ordnance validation driver").AddComponent<TidalCheckDriver>().StartCoroutine(Drive());}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Entered",false))
            {
                SessionState.SetBool(Key+"Entered",false);Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));Application.runInBackground=saved.background;AudioListener.volume=saved.volume;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(saved.choices));
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nActual Game with controlled shooting and art fixtures. Quest and GPU performance untested.\n");
            }
        }
        static void Log(string m,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Known independent editor service failures are recorded separately.
            if(trace.Contains("UnityEditor.Search.SearchDatabase")||trace.Contains("UnityEditor.AsyncHTTPClient")){File.AppendAllText(Report,"EDITOR SERVICE "+m+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+m+"\n"+trace+"\n");
        }
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("OrdnanceVisualChecks.request");
                if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()){File.Delete(request);Run();}
                return;
            }
        }
        static IEnumerator Drive()
        {
            while(run!=null)
            {
                object current;
                try{if(!run.MoveNext())break;current=run.Current;}
                catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                yield return current;
            }
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        static void Check(bool ok,string text){checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static IEnumerator Until(Func<bool> ready,string label,float seconds=40){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
        static IEnumerator Flatten(IEnumerator source)
        {
            while(source.MoveNext())
            {
                if(source.Current is IEnumerator nested){var child=Flatten(nested);while(child.MoveNext())yield return child.Current;}
                else if(source.Current is AsyncOperation operation){while(!operation.isDone)yield return null;}
                
                else yield return source.Current;
            }
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            yield return Until(()=>Services.Session is CastlePlaySession ready&&ready.Modes&&Services.World!=null,"Game initializes");
            var session=(CastlePlaySession)Services.Session;
            session.SetMenu(false);session.SurvivorBots.AutoFill=false;
            var rules=session.Modes.Rules.Copy();rules.countdownSeconds=0;rules.automaticDisasters=false;rules.healthEnabled=true;rules.fallDamage=false;rules.island=IslandRule.Walkable;
            session.Modes.Configure(rules);session.Modes.StartRound();
            int oldCapture=Time.captureFramerate;
            var stage=new GameObject("Ordnance test fixtures");
            try
            {
                Time.captureFramerate=60;
                for(int i=0;i<20;i++)yield return null;
                foreach(var key in new[]{"CandyOrdnance","CandyScenery/CandySpirit"})
                {
                    var shader=Resources.Load<Shader>(key);
                    Check(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),key+" shader imports");
                }
                var kinds=new[]{EquipmentKind.RocketLauncher,EquipmentKind.GrenadeLauncher,EquipmentKind.BarbedBat,EquipmentKind.ChainGun,
                    EquipmentKind.WristLaser,EquipmentKind.Chainsaw,EquipmentKind.SawStaff,EquipmentKind.LaserAxe,EquipmentKind.Dagger};
                for(int i=0;i<kinds.Length;i++)
                {
                    var kind=kinds[i];Check(EquipmentModelData.Parts.TryGetValue(kind,out var parts)&&parts.Any(x=>x.mesh.name.StartsWith("Foundry")),kind+" uses new authored mesh in production pickup cache");
                    Check(parts.Length<=9&&parts.All(x=>x.mesh&&x.material&&x.mesh.vertexCount>0),kind+" shares bounded textured material groups");
                    var root=new GameObject(kind.ToString()).transform;root.SetParent(stage.transform,false);
                    root.position=new Vector3(350+(i%3-1)*1.85f,25+(1-i/3)*1.2f,350);
                    root.rotation=Quaternion.Euler(0,67,kind==EquipmentKind.BarbedBat?21:0);
                    if(i>=4)root.localScale=Vector3.one*1.23f;
                    foreach(var part in parts)
                    {
                        var go=new GameObject(part.mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                        go.transform.localPosition=part.local.GetColumn(3);go.transform.localRotation=part.local.rotation;go.transform.localScale=part.local.lossyScale;
                        go.GetComponent<MeshFilter>().sharedMesh=part.mesh;go.GetComponent<MeshRenderer>().sharedMaterial=part.material;
                    }
                }
                foreach(var child in stage.GetComponentsInChildren<Transform>())child.gameObject.layer=31;
                Capture("Ordnance-Armory.png",new Vector3(350,25.25f,357),new Vector3(350,25,350),1<<31,2.45f);
                yield return WeaponEventChecks.Run(session);
                session.Modes.StartRound();for(int i=0;i<20;i++)yield return null;

                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(stage.transform);floor.layer=Layers.Surface;
                floor.transform.position=new Vector3(150,9,150);floor.transform.localScale=new Vector3(30,2,40);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(stage.transform);wall.layer=Layers.Surface;
                wall.transform.position=new Vector3(150,12,162);wall.transform.localScale=new Vector3(5,4,.5f);
                var player=session.Survivor();player.Place(new Vector3(150,10.05f,150),Quaternion.identity);player.InputSuspended=true;
                var inventory=(IEquipmentRuntime)session.Modes;
                foreach(var kind in new[]{EquipmentKind.DualRifle,EquipmentKind.RocketLauncher,EquipmentKind.GrenadeLauncher})
                {
                    var control=(IDemoEquipmentControls)session.Modes;
                    Check(control.GiveEquipment(kind),"Actual demo grant "+kind);
                    Check(inventory.RequestEquipment(true,false,false),"Actual draw "+kind);
                    for(int i=0;i<6;i++)yield return null;
                    Physics.SyncTransforms();
                    var start=player.ViewPose.position+Vector3.forward*.75f;
                    Check(inventory.RequestEquipmentAttack(new Pose(start,Quaternion.Euler(kind==EquipmentKind.GrenadeLauncher?-12:0,0,0))),"Accepted shot "+kind);
                    for(int i=0;i<(kind==EquipmentKind.DualRifle?3:12);i++)yield return null;
                    var cameraAt=start+new Vector3(4,2,-2);
                    Capture("Ordnance-"+kind+".png",cameraAt,start+Vector3.forward*4,~(1<<5),0);
                    if(kind==EquipmentKind.DualRifle)
                    {
                        var trails=session.GetComponent<CandyBulletTrails>();
                        Check(trails&&trails.AllocatedCount>0&&trails.GetComponentsInChildren<Collider>(true).Length==0,"Live pooled bullet renderer adds no colliders");
                    }
                    else
                    {
                        Check(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(f=>f.sharedMesh&&f.sharedMesh.name=="Sculpted "+kind+" projectile"),"Live projectile uses sculpted "+kind+" casing");
                    }
                    if(kind==EquipmentKind.RocketLauncher)
                    {
                        for(int i=0;i<39;i++)yield return null;
                        Capture("Ordnance-RocketImpact.png",start+new Vector3(6,4,3),start+Vector3.forward*11,~(1<<5),0);
                    }
                    for(int i=0;i<90;i++)yield return null;
                    if(kind==EquipmentKind.RocketLauncher||kind==EquipmentKind.GrenadeLauncher)
                        Check(session.Modes.Frame.weaponProjectiles.All(x=>x.kind!=kind),"Projectile visual follows authoritative removal "+kind);
                }
                var rig=new GameObject("Sea spirit art fixture");rig.transform.SetParent(stage.transform);
                rig.AddComponent<TidalSpiritFixture>().Build(new Vector3(175,15,170));
                for(int i=0;i<78;i++)yield return null;
                var spirit=Object.FindObjectsByType<CandySeaSpirit>(FindObjectsSortMode.None).FirstOrDefault(x=>x.transform.Find("Visual - ocean spirit bust and arms"));
                Check(spirit&&spirit.GetComponentsInChildren<MeshFilter>().Count(x=>x.sharedMesh&&x.sharedMesh.name.StartsWith("Sculpted tidal arm"))==8,"Sea spirit binds all eight sculpted arm sections");
                Check(spirit.GetComponentsInChildren<Collider>().Length==0,"Sea spirit art adds no colliders");
                Check(spirit.GetComponentsInChildren<MeshFilter>().All(x=>!x.sharedMesh||x.sharedMesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))),"All sea spirit mesh vertices are finite");
                Capture("Ordnance-SeaSpirit.png",new Vector3(190,23,192),new Vector3(175,13,170),~(1<<5),0);
            }
            finally{Time.captureFramerate=oldCapture;Object.Destroy(stage);}
        }
        static void Capture(string name,Vector3 position,Vector3 target,int mask,float size)
        {
            var go=new GameObject("Ordnance capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.nearClipPlane=.03f;camera.farClipPlane=90;camera.fieldOfView=49;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.075f,.10f);
            camera.cullingMask=mask;camera.transform.position=position;camera.transform.LookAt(target);
            camera.orthographic=size>0;camera.orthographicSize=Mathf.Max(.01f,size);
            var key=new GameObject("Ordnance capture light");key.transform.rotation=Quaternion.Euler(32,147,0);var light=key.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=mask;
            var texture=new RenderTexture(1280,960,24);var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                image=new Texture2D(1280,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();
                File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Object.Destroy(texture);if(image)Object.Destroy(image);Object.Destroy(go);Object.Destroy(key);}
        }
    }
}

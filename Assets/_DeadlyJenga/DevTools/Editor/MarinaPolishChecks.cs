using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class MarinaPolishChecks
    {
        const string Key = "MarinaPolishChecks";
        static string Output => Path.GetFullPath("Logs/MarinaPolish");
        static string Request => Path.GetFullPath("Logs/MarinaPolish.request");
        static readonly Stack<IEnumerator> steps = new();
        static double nextPoll;

        static MarinaPolishChecks()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += State;
        }

        [MenuItem("Tools/Deadly Jenga/Lobby/Check Marina routes and capture")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene edits before running Marina checks.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"report.txt"),"Actual Marina Play Mode: collision routes and rendered scenery. No headset or network claim.\n");
            SessionState.SetInt(Key+"Legacy",PlayerPrefs.GetInt(IslandLobby.LegacyLobbyPreference,-1));
            SessionState.SetFloat(Key+"Volume",AudioListener.volume);
            PlayerPrefs.DeleteKey(IslandLobby.LegacyLobbyPreference);
            SessionState.SetBool(Key,true);
            SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!File.Exists(Request) || File.ReadAllText(Request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(Request);
            Run();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived+=OnLog;
                Log("Presentation MVID "+typeof(MayhemMarina).Assembly.ManifestModule.ModuleVersionId);
                steps.Push(CheckLobby());
                EditorApplication.update+=Pump;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Pump;
                Application.logMessageReceived-=OnLog;
                steps.Clear();
                int prior=SessionState.GetInt(Key+"Legacy",-1);
                if(prior<0)PlayerPrefs.DeleteKey(IslandLobby.LegacyLobbyPreference);
                else PlayerPrefs.SetInt(IslandLobby.LegacyLobbyPreference,prior);
                AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);
                SessionState.SetBool(Key,false);
                Log("COMPLETE "+(SessionState.GetBool(Key+"Failed",false)?"FAIL":"PASS")+"; stable Edit Mode");
            }
        }
        static void OnLog(string message,string stack,LogType kind)
        {
            if(kind!=LogType.Error && kind!=LogType.Exception && kind!=LogType.Assert)return;
            SessionState.SetBool(Key+"Failed",true);Log("ERROR "+message+"\n"+stack);
        }
        static void Pump()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(steps.Count==0){EditorApplication.update-=Pump;EditorApplication.ExitPlaymode();return;}
                var current=steps.Peek();
                if(!current.MoveNext()){steps.Pop();return;}
                if(current.Current is IEnumerator next)steps.Push(next);
            }
            catch(Exception error)
            {
                SessionState.SetBool(Key+"Failed",true);Log("FAIL "+error);
                EditorApplication.update-=Pump;EditorApplication.ExitPlaymode();
            }
        }
        static void Log(string message)=>File.AppendAllText(Path.Combine(Output,"report.txt"),message+"\n");
        static void Check(bool pass,string description)
        {
            Log((pass?"PASS ":"FAIL ")+description);
            if(!pass)SessionState.SetBool(Key+"Failed",true);
        }
        static IEnumerator Wait(float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<end)yield return null;
        }
        static IEnumerator CheckLobby()
        {
            Application.runInBackground=true;
            AudioListener.volume=0;
            float deadline=Time.realtimeSinceStartup+45;
            IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>()) && Time.realtimeSinceStartup<deadline)yield return null;
            if(!lobby)throw new InvalidOperationException("Lobby did not load");
            yield return Wait(4);
            var marina=lobby.GetComponent<MayhemMarina>();
            Check(marina && marina.Built,"Marina geometry built");
            if(!marina)yield break;
            lobby.SetMenu(false);
            lobby.enabled=false;
            if(Services.Session?.SurvivorBots!=null)
            {
                Services.Session.SurvivorBots.AutoFill=false;
                Services.Session.SurvivorBots.Running=false;
                Services.Session.SurvivorBots.Clear();
            }
            yield return Wait(1);
            var motor=lobby.Survivor();
            var capsule=motor.GetComponent<CharacterController>();
            Log($"Player alive={NetworkActors.Alive(lobby.Player)}; suspended={motor.InputSuspended}; triangles={marina.RenderTriangles}/{marina.CollisionTriangles}; capsule={capsule.radius}/{capsule.height}; step={capsule.stepOffset}");
            Check(RenderSettings.skybox && RenderSettings.skybox.GetFloat("_CoastDay")>.5f,"Catwalks coastal sky active");
            var ocean=marina.transform.Find("Visual · Mayhem Marina/Marina ocean").GetComponent<Renderer>();
            var properties=new MaterialPropertyBlock();ocean.GetPropertyBlock(properties);
            Check(properties.GetFloat("_CatwalksCoast")>.5f && properties.GetTexture("_CoastSpectrum"),"Catwalks water spectrum actually bound");
            Check(marina.transform.Find("Visual · Mayhem Marina/Marina distant island backdrop"),"Distant painted coastline present");
            var origin=lobby.transform.position;
            yield return Walk(lobby,new[]{new Vector3(-8.1f,1.3f,11.4f),new Vector3(-8.1f,4.14f,19),new Vector3(-8.1f,1.3f,11.4f)},"terrace stairs both directions");
            yield return Walk(lobby,new[]{new Vector3(7.6f,1.3f,10.25f),new Vector3(7.6f,4.10f,17.6f),new Vector3(10.0f,4.03f,17.95f)},"east ramp to terrace bridge");
            yield return Walk(lobby,new[]{new Vector3(-21,1.3f,2.4f),new Vector3(-21,4.14f,18.5f),new Vector3(-18,4.14f,19.4f)},"west ramp and landing");
            yield return Walk(lobby,new[]{new Vector3(-10.2f,1.3f,7.6f),new Vector3(-5.0f,1.86f,7.6f),new Vector3(0,1.86f,7.6f)},"garden deck to terminal platform");
            var plaza=MayhemMarina.PlazaCenter;
            yield return Walk(lobby,new[]{new Vector3(-10,1.33f,-8),plaza,new Vector3(6.9f,1.31f,-4.6f),new Vector3(11,1.29f,1.4f)},"arrival across Showtime Plaza to Beacon Walk");
            yield return Walk(lobby,new[]{plaza,new Vector3(0,1.29f,3.2f),new Vector3(0,1.86f,6.0f)},"plaza to cinema ramp and terminals");
            yield return Walk(lobby,new[]{plaza,plaza+Quaternion.Euler(0,-58,0)*new Vector3(0,0,2.6f)},"plaza to wardrobe stage");
            yield return Walk(lobby,new[]{plaza,plaza+Quaternion.Euler(0,58,0)*new Vector3(0,0,3.2f)},"plaza to Hall of Fame steps");
            yield return Walk(lobby,new[]{new Vector3(22.9f,1.25f,8.8f),new Vector3(26.1f,.30f,9.2f),new Vector3(22.9f,1.25f,8.8f)},"east beach ramp both directions");
            yield return Walk(lobby,new[]{new Vector3(-22.9f,1.25f,8.8f),new Vector3(-25.8f,.36f,9.25f),new Vector3(-22.9f,1.25f,8.8f)},"west beach ramp both directions");
            yield return Walk(lobby,new[]{new Vector3(1.1f,1.25f,24.4f),new Vector3(1.1f,.39f,28.4f),new Vector3(1.1f,1.25f,24.4f)},"rear beach ramp both directions");
            motor.InputSuspended=true;
            foreach(var renderer in motor.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            yield return Wait(2);
            Capture(origin+new Vector3(-29,20,-37),origin+new Vector3(0,2.6f,3),61,"Marina-overview.png");
            Capture(origin+new Vector3(-9,3.0f,-11),origin+new Vector3(0,3.2f,9),76,"Marina-arrival.png");
            Capture(origin+new Vector3(-8.1f,3.0f,10.7f),origin+new Vector3(-8.1f,4.7f,17.5f),72,"Marina-terrace-stairs.png");
            Capture(origin+new Vector3(-19.8f,5.85f,18.8f),origin+new Vector3(8,2.8f,-3),78,"Marina-upper-terrace.png");
            Capture(origin+new Vector3(23,3,5.5f),origin+new Vector3(60,4,150),74,"Marina-ocean.png");
            Capture(origin+new Vector3(11,3,3),origin+new Vector3(14.2f,2.7f,6.7f),74,"Marina-lounge-materials.png");
            var center=origin+MayhemMarina.PlazaCenter;
            Capture(origin+new Vector3(-12.5f,3.1f,-10.5f),center+new Vector3(0,1.6f,2),72,"Marina-plaza-arrival.png");
            Capture(center+Quaternion.Euler(0,122,0)*new Vector3(0,1.75f,3.2f),origin+MayhemMarina.Wardrobe+Vector3.up*1.1f,70,"Marina-wardrobe-stage.png");
            Capture(center+Quaternion.Euler(0,-122,0)*new Vector3(0,1.75f,3.2f),origin+MayhemMarina.FameBoard+Vector3.down*.6f,70,"Marina-hall-of-fame.png");
            Capture(center+new Vector3(0,9,-14),center+new Vector3(0,0,1.5f),64,"Marina-plaza-overhead.png");
            foreach(var shipName in new[]{"Cruise ship · Coral Dream","Cruise ship · Candy Wonder"})
            {
                var ship=GameObject.Find(shipName);
                Check(ship,"Cruise ship present: "+shipName);
                if(ship)Capture(center+new Vector3(0,3.2f,-6),ship.transform.position+Vector3.up*8,38,shipName.Replace("Cruise ship · ","Marina-cruise-").Replace(" ","-")+".png");
            }
            var sky=RenderSettings.skybox;
            GameEvents.RaiseEmoted(lobby.gameObject,"LobbyParked");
            Check(!ocean.gameObject.activeInHierarchy,"Parked lobby hides its ocean");
            GameEvents.RaiseEmoted(lobby.gameObject,"LobbyResumed");
            Check(ocean.gameObject.activeInHierarchy && RenderSettings.skybox==sky,"Lobby scenery restores on resume");
        }
        static IEnumerator Walk(IslandLobby lobby,Vector3[] points,string label)
        {
            var motor=lobby.Survivor();
            var driver=new MarinaWalkDriver();
            motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(lobby.transform.TransformPoint(points[0]),Quaternion.identity);
            try
            {
                for(int i=1;i<points.Length;i++)
                {
                    driver.target=lobby.transform.TransformPoint(points[i]);
                    float start=Time.time,wall=Time.realtimeSinceStartup;
                    while(Vector3.ProjectOnPlane(driver.target-motor.transform.position,Vector3.up).magnitude>.40f && Time.time<start+15 && Time.realtimeSinceStartup<wall+45)
                        yield return null;
                    float error=Vector3.ProjectOnPlane(driver.target-motor.transform.position,Vector3.up).magnitude;
                    Check(error<.6f && Mathf.Abs(motor.transform.position.y-driver.target.y)<.5f,$"{label} leg {i}: position={motor.transform.position}; target={driver.target}; simulated={Time.time-start:F1}s");
                }
            }
            finally{motor.InputDriver=null;motor.externallyDriven=false;motor.InputSuspended=true;}
        }
        static void Capture(Vector3 eye,Vector3 target,float fov,string filename)
        {
            var go=new GameObject("Marina review camera");var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=fov;
            camera.nearClipPlane=.08f;camera.farClipPlane=700;camera.allowHDR=true;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            var urp=camera.GetUniversalAdditionalCameraData();urp.renderPostProcessing=true;urp.requiresDepthTexture=true;urp.allowXRRendering=false;
            var targetTexture=new RenderTexture(1920,1080,24);camera.targetTexture=targetTexture;
            var old=RenderTexture.active;
            try
            {
                camera.Render();RenderTexture.active=targetTexture;
                var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,filename),image.EncodeToPNG());Object.Destroy(image);
                Log($"CAPTURE {filename}; eye={eye}; target={target}; FOV={fov}");
            }
            finally{RenderTexture.active=old;targetTexture.Release();Object.Destroy(targetTexture);Object.Destroy(go);}
        }
    }
}

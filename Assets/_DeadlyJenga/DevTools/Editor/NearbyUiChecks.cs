using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class NearbyUiChecks
    {
        const string Key="NearbyUiChecks",NameKey="DeadlyJenga.PlayerName.v1";
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static string Output=>Path.GetDirectoryName(ProjectPath.Report("NearbyUi/report.txt"));
        static string Report=>Path.Combine(Output,"report.txt");
        static double started;
        static int errors;
        [Serializable] sealed class Saved {public SceneSetup[] scenes;}
        static NearbyUiChecks(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new Exception("Use an isolated checked batch");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||EditorSceneManager.GetSceneByPath(s.path).isDirty)))throw new Exception("Unsaved scenes preserved");
            if(!ValidationRunGate.TryAcquire(Key))throw new Exception("Editor already has a validation owner");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Report,"Real Unity Play Mode menu components; simulated nearby provider and session. Commands invoked through UI bindings; no physical controller, radio or Photon proof.\n");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));
            SessionState.SetBool(Key+"HadName",PlayerPrefs.HasKey(NameKey));
            SessionState.SetString(Key+"Name",PlayerPrefs.GetString(NameKey,""));
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",2);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){started=EditorApplication.timeSinceStartup;errors=0;Application.logMessageReceived+=Log;}
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            Application.logMessageReceived-=Log;
            if(SessionState.GetBool(Key+"HadName",false))PlayerPrefs.SetString(NameKey,SessionState.GetString(Key+"Name",""));else PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.Save();
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes","")).scenes);
            SessionState.SetBool(Key,false);ValidationRunGate.Release(Key);
            File.AppendAllText(Report,"CLEANUP restored name preference and saved scenes.\n");
            EditorApplication.Exit(SessionState.GetInt(Key+"Exit",2));
        }
        static void Log(string text,string stack,LogType type){if(type is LogType.Error or LogType.Exception or LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+text+"\n"+stack+"\n");}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||started==0||EditorApplication.timeSinceStartup-started<2)return;
            started=0;
            try{Checks();Check(errors==0,"No runtime errors during menu checks");SessionState.SetInt(Key+"Exit",0);File.AppendAllText(Report,"COMPLETE UI assertions passed.\n");}
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");}
            finally{EditorApplication.ExitPlaymode();}
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Flags).GetValue(obj);
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        static void Click(Transform root,string text)
        {
            var button=root.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text==text);
            Check(button.interactable,"Button available: "+text);button.onClick.Invoke();
        }
        static void TabletClick(CastleWorldMenu menu,string prefix)
        {
            Call(menu,"PointPlayerTablet",Camera.main,false,false);
            var entries=((IEnumerable)Field<object>(menu,"tabletActions")).Cast<object>();
            var entry=entries.First(e=>Field<Button>(e,"Button").gameObject.activeInHierarchy&&Field<Func<string>>(e,"Title")().StartsWith(prefix,StringComparison.Ordinal));
            Check(Field<Func<bool>>(entry,"Allowed")?.Invoke()!=false,"Tablet command available: "+prefix);
            Field<Action>(entry,"Select")();Call(menu,"RefreshTabletNearby");Call(menu,"PointPlayerTablet",Camera.main,false,false);
        }
        static void Checks()
        {
            File.AppendAllText(Report,"Loaded Presentation MVID: "+typeof(HomeMenuView).Assembly.ManifestModule.ModuleVersionId+"\nFixture MVID: "+typeof(NearbyUiChecks).Assembly.ManifestModule.ModuleVersionId+"\n");
            var host=new GameObject("Explicit nearby UI fixture");var fixture=host.AddComponent<NearbyUiFixture>();
            typeof(NearbyRoomUi).GetField("provider",Flags).SetValue(null,fixture);
            for(int i=0;i<7;i++)fixture.Rooms.Add(new NearbyRoomInfo("room-"+i,new[]{"Evan","Renée","Alex","Sam","Taylor","Morgan","Casey"}[i],i+1,10));
            var home=host.AddComponent<HomeMenuView>();Call(home,"Build",fixture,false);Call(home,"ShowBluetooth");Call(home,"RefreshBluetooth");
            Check(fixture.Scans==1,"Opening Bluetooth page scans once");
            var page=Field<GameObject>(home,"bluetoothPage");
            Check(page.activeInHierarchy,"Home Bluetooth page visible");
            var pageRect=(RectTransform)page.transform;
            var buttonRects=page.GetComponentsInChildren<Button>().Select(b=>(RectTransform)b.transform).ToArray();
            Check(buttonRects.All(r=>pageRect.anchoredPosition.y+r.anchoredPosition.y+r.sizeDelta.y/2<190&&pageRect.anchoredPosition.y+r.anchoredPosition.y-r.sizeDelta.y/2>-334),"Home buttons fit between tagline and input-mode control");
            Click(page.transform,"Evan's Room · 1/10 · Request");Check(fixture.LastRequest=="room-0","Named room selection uses stable room ID");
            Click(page.transform,"NEXT");Call(home,"RefreshBluetooth");Click(page.transform,"Taylor's Room · 5/10 · Request");Check(fixture.LastRequest=="room-4","Pagination selects correct room");
            Click(page.transform,"CANCEL REQUEST");Check(fixture.Cancels==1,"Cancel reaches provider");
            Click(page.transform,"PREVIOUS");Call(home,"RefreshBluetooth");
            Capture(Field<Camera>(home,"eye"),Field<Canvas>(home,"homeCanvas"),"01-home-rooms.png");
            fixture.Rooms.Clear();Call(home,"RefreshBluetooth");Check(Field<Text[]>(home,"bluetoothLabels")[0].text.StartsWith("No rooms"),"Empty discovery clears stale rows");
            Object.DestroyImmediate(home);foreach(Transform child in host.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            var camera=new GameObject("UI fixture camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.025f,.06f);camera.transform.position=new Vector3(0,0,-3);camera.orthographic=true;camera.orthographicSize=.9f;
            var session=host.AddComponent<FakeSession>();
            var menu=new GameObject("Production Room tablet",typeof(RectTransform)).AddComponent<CastleWorldMenu>();menu.Build(session);menu.gameObject.SetActive(true);
            var pageType=typeof(CastleWorldMenu).GetNestedType("TabletPage",BindingFlags.NonPublic);
            Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Room"));
            fixture.Requests.Add(new NearbyJoinRequest("request-a","Alex",50));fixture.Requests.Add(new NearbyJoinRequest("request-b","Renée",45));
            Check(NearbyRoomUi.Notice.Contains("Alex wants to join")&&NearbyRoomUi.Notice.Contains("Room"),"Named notification directs host to Room");
            TabletClick(menu,"Nearby requests");Capture(camera,menu.GetComponent<Canvas>(),"02-host-requests.png");
            TabletClick(menu,"Approve Alex");Check(fixture.LastDecision=="request-a"&&fixture.Accepted,"Approve targets correct request");
            TabletClick(menu,"Deny");Check(fixture.LastDecision=="request-b"&&!fixture.Accepted,"Deny targets remaining request");
            Check(NearbyRoomUi.Notice=="","Notification clears after requests resolve");
            menu.OpenNearbyProfile();
            typeof(CastleWorldMenu).GetField("profileDraft",Flags).SetValue(menu,"EVAN");
            TabletClick(menu,"A");TabletClick(menu,"Delete");TabletClick(menu,"Space");TabletClick(menu,"B");
            Check(Field<string>(menu,"profileDraft")=="EVAN B","Name keyboard adds, deletes and spaces");
            Capture(camera,menu.GetComponent<Canvas>(),"03-player-name.png");TabletClick(menu,"Save name");
            Check(PlayerPrefs.GetString(NameKey)=="EVAN B","Save writes production persistent preference");
            Object.DestroyImmediate(fixture);fixture=host.AddComponent<NearbyUiFixture>();Check(fixture.PlayerName=="EVAN B","Recreated provider loads saved name");
            typeof(NearbyRoomUi).GetField("provider",Flags).SetValue(null,fixture);
            menu.OpenNearbyProfile();typeof(CastleWorldMenu).GetField("profileDraft",Flags).SetValue(menu,"UNSAVED");TabletClick(menu,"Cancel");Check(fixture.PlayerName=="EVAN B","Cancelling name edit preserves saved name");
            Object.DestroyImmediate(menu.gameObject);Object.DestroyImmediate(host);Object.DestroyImmediate(camera.gameObject);
        }
        static void Capture(Camera camera,Canvas canvas,string name)
        {
            var mode=canvas.renderMode;var worldCamera=canvas.worldCamera;var distance=canvas.planeDistance;
            var target=new RenderTexture(1600,1000,24);var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);var previous=camera.targetTexture;var active=RenderTexture.active;
            try{if(mode==RenderMode.ScreenSpaceOverlay){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());}
            finally{canvas.renderMode=mode;canvas.worldCamera=worldCamera;canvas.planeDistance=distance;camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class CandyLobbyLayoutChecks
    {
        const string Key="CandyLobbyLayout";
        static int checks,errors;static bool failed;
        static string Report=>ProjectPath.Report("CandyLobbyLayout.txt");
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static CandyLobbyLayoutChecks()
        {
            EditorApplication.update+=()=>
            {
                string path=ProjectPath.Report("CandyLobbyLayout.request");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(path);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=false;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<IslandLobby>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Candy Lobby Layout")]
        public static void Run()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Real Game hover, rifle, cosmetics and private Mystery replay checks.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Check(bool pass,string label)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");checks++;if(!pass)throw new InvalidOperationException(label);}
        static IEnumerator Guard()
        {
            var run=Test();
            try
            {
                while(true)
                {
                    bool more=false;object value=null;Exception error=null;
                    try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){error=e;}
                    if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                    if(!more)break;yield return value;
                }
            }
            finally{(run as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();}
        }
        static IEnumerator Test()
        {
            float until=Time.realtimeSinceStartup+30;while(!(Services.Session is IslandLobby)&&Time.realtimeSinceStartup<until)yield return null;
            var lobby=Services.Session as IslandLobby;Check(lobby,"Actual Lobby starts");yield return new WaitForSeconds(1);
            var view=lobby.View();var join=(RectTransform)view.transform.Find("JOIN");var host=(RectTransform)view.transform.Find("HOST");var mode=(RectTransform)view.transform.Find("MAP + MODE");
            Check(join&&host&&mode,"All three authored terminals exist");
            Check(Quaternion.Angle(join.rotation,host.rotation)<.1f&&Quaternion.Angle(host.rotation,mode.rotation)<.1f,"All terminals are parallel");
            Check(mode.sizeDelta.x>=1100&&mode.sizeDelta.y<=820&&mode.position.y+mode.rect.height*mode.lossyScale.y*.5f<2.9f,"Mode screen is wider and its top is below 2.9 metres");
            Check(host.sizeDelta.x>=1000&&host.sizeDelta.y<=800,"Host screen is widened and shortened");
            Check(join.position.y<2&&join.GetComponentsInChildren<UnityEngine.UI.Button>().All(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().resizeTextMaxSize>=32),"Join screen is lowered with larger button type");
            foreach(var screen in new[]{join,host,mode})Check(screen.GetComponents<UnityEngine.CanvasGroup>().Length==1,"Single readable face controller: "+screen.name);
            var game=view.transform.Find("Mode controller computer/Vertical controller face");var hosting=view.transform.Find("Host controller computer/Vertical controller face");
            Check(game&&hosting&&Vector3.Dot(game.up,Vector3.up)>.99f&&Vector3.Dot(hosting.up,Vector3.up)>.99f,"Decorative controls are upright on flat vertical walls");
            foreach(var face in new[]{game,hosting})
            {
                var wall=face.parent.Find("Flat grounded terminal wall");
                Check(wall&&wall.localPosition.y-wall.localScale.y*.5f<0&&wall.localScale.z<=.181f,"Thin terminal wall reaches below ground: "+face.parent.name);
                Check(face.GetComponentsInChildren<MeshRenderer>().All(r=>r.GetComponent<TextMesh>()||r.transform.localScale.z<=.02f),"All retro decorations are shallow relief: "+face.parent.name);
            }
            var player=lobby.Survivor();player.Place(new Vector3(0,0,-5),Quaternion.identity);player.InputSuspended=true;yield return null;
            Capture(view.transform,new Vector3(0,2,-8),new Vector3(0,1.5f,1),"Lobby-Terminals-Front.png",65);
            player.Place(new Vector3(3.05f,0,-2.5f),Quaternion.identity);yield return null;Capture(view.transform,new Vector3(3.05f,1.7f,-2.5f),new Vector3(3.05f,1.65f,1),"Lobby-Mode-Cabinet.png",50);
            player.Place(new Vector3(-3.05f,0,-2.5f),Quaternion.identity);yield return null;Capture(view.transform,new Vector3(-3.05f,1.7f,-2.5f),new Vector3(-3.05f,1.65f,1),"Lobby-Join-Cabinet.png",50);
            var avatar=player.GetComponent<CandyPlayerAvatar>();var appearance=avatar.Appearance;
            try
            {
                avatar.Rig.SetAppearance(new AvatarAppearance{skin=AvatarSkin.JackOLantern});var head=avatar.Rig.HeadAnchor.position;
                Capture(view.transform,head+new Vector3(.08f,.015f,.85f),head,"Carved-Jack-Lantern-Lobby.png",40);
            }
            finally{avatar.Rig.SetAppearance(appearance);}
        }
        static void Capture(Transform root,Vector3 position,Vector3 target,string name,float fov)
        {
            var go=new GameObject("Lobby layout capture");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=fov;camera.transform.position=position;camera.transform.LookAt(target);camera.stereoTargetEye=StereoTargetEyeMask.None;
            var rt=new RenderTexture(1440,1000,24);camera.targetTexture=rt;var old=RenderTexture.active;
            try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());Object.Destroy(image);}
            finally{RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
        }
    }
}

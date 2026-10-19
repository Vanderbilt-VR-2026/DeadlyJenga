using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HudReadabilityChecks
    {
        const string Key="HudReadabilityChecks";
        static string Report=>ProjectPath.Report("HudReadability.txt");
        static int failures,errors;
        static HudReadabilityChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("HudReadability.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Health and lobby checks");Object.DontDestroyOnLoad(go);go.AddComponent<HudReadabilityCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Time.timeScale=1;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Gameplay HUD Readability")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before this check.");
            File.WriteAllText(Report,"Gameplay HUD layout and player colors\n");
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            if(message.StartsWith("[Package Manager Window]",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE "+message+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static IEnumerator Guard()
        {
            var test=CheckScene();
            while(true)
            {
                bool next=false;object value=null;Exception caught=null;
                try{next=test.MoveNext();if(next)value=test.Current;}catch(Exception e){caught=e;}
                if(caught!=null){failures++;File.AppendAllText(Report,"FAIL "+caught+"\n");break;}
                if(!next)break;yield return value;
            }
            File.AppendAllText(Report,$"COMPLETE failures={failures}; errors={errors}. Headset visibility and comfort remain unverified.\n");
            EditorApplication.ExitPlaymode();
        }
        static Rect Bounds(Text label)
        {var r=label.rectTransform;return new Rect(r.anchoredPosition-r.sizeDelta*.5f,r.sizeDelta);}
        static IEnumerator CheckScene()
        {
            yield return new WaitForSeconds(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Game scene starts");
            Time.timeScale=0;Call(session,"OnApplicationFocus",true);session.SetMenu(false);session.Modes.enabled=false;
            var frame=session.Modes.Frame;frame.disasterWarning="Meteor shower and earthquake";frame.disasterWarningRemaining=8;
            frame.topTen=Enumerable.Range(0,10).Select(i=>new MatchPlayerFrame{id=i==0?session.Modes.LocalActorId:"hud:"+i,name=(i%2==0?"Red":"Blue")+"Explorer"+i,color=i%2==0?new Color(1,.1f,.2f):new Color(.1f,.4f,1),rank=i+1,metric=100-i}).ToArray();
            var hud=session.GetComponent<CandyMatchHud>();Set(hud,"sampleAt",0f);Call(hud,"Update");
            var board=(Text)Field(hud,"board");var status=(Text)Field(hud,"status");var teams=(Text)Field(hud,"teams");var warning=(Text)Field(hud,"warning");var claim=(Text)Field(hud,"claim");
            var labels=new[]{board,status,teams,warning,claim};
            for(int i=0;i<labels.Length;i++)for(int j=i+1;j<labels.Length;j++)Check(!Bounds(labels[i]).Overlaps(Bounds(labels[j])),labels[i].name+" does not overlap "+labels[j].name);
            Check(warning.text.Contains("Meteor shower")&&!status.text.Contains("WARNING"),"Disaster warning owns its own bounded text region");
            Check(board.text.Contains(CandyHudLayout.PlayerName(frame.topTen[0]))&&board.text.Contains(CandyHudLayout.PlayerName(frame.topTen[1]))&&board.text.Contains("▶</color>"),"Leaderboard uses exact avatar colors and preserves a separate local-player indicator");
            Check(board.preferredHeight<board.rectTransform.rect.height,"Ten rows and heading fit the leaderboard without overflowing");
            var mystery=session.GetComponent<CandyMysteryView>();
            foreach(string name in new[]{"role","prompt","choices"})
            {var label=(Text)Field(mystery,name);foreach(var other in labels)Check(!Bounds(label).Overlaps(Bounds(other)),"Mystery "+name+" does not overlap "+other.name);}
            var hints=session.GetComponent<CandyDemoHud>();var hintCanvas=(Canvas)Field(hints,"hintCanvas");var matchCanvas=(Canvas)Field(hud,"canvas");var mysteryCanvas=(Canvas)Field(mystery,"canvas");
            foreach(var canvas in new[]{hintCanvas,matchCanvas,mysteryCanvas})
            {
                Check(canvas.GetComponent<CanvasScaler>().referenceResolution==CandyHudLayout.ReferenceSize&&canvas.GetComponent<CanvasScaler>().screenMatchMode==CanvasScaler.ScreenMatchMode.Expand,"Overlay shares the same scaling: "+canvas.name);
                CandyHudLayout.Configure(canvas,session.transform,session.ActiveCamera,true);
                Check(canvas.GetComponent<RectTransform>().sizeDelta==CandyHudLayout.ReferenceSize&&Vector3.Distance(canvas.transform.localPosition,new Vector3(0,0,1.1f))<.001f,"VR overlay shares the same projection: "+canvas.name);
                CandyHudLayout.Configure(canvas,session.transform,session.ActiveCamera,false);
            }
            foreach(var size in new[]{new Vector2(210,36),new Vector2(430,125)})foreach(var view in new[]{Vector3.zero,Vector3.one,new Vector3(.5f,.5f,1)})
            {var p=CandyHudLayout.MarkerPosition(view,size,true);var r=new Rect(p-size*.5f,size);Check(r.xMax<=280&&r.yMax<=16&&r.yMin>=-140,"World clue stays outside fixed HUD regions");}
            var gameType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(gameType!=null)EditorWindow.GetWindow(gameType).Focus();
            session.SetMenu(false);yield return new WaitForSecondsRealtime(.3f);
            if(!Application.isBatchMode){yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();if(shot){File.WriteAllBytes(ProjectPath.Report("HudReadability.png"),shot.EncodeToPNG());Object.Destroy(shot);}}
            Check(errors==0,"No runtime errors in the actual Game scene");Time.timeScale=1;
        }
    }
    public sealed class HudReadabilityCheckDriver:MonoBehaviour {}
}

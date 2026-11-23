using System;
using System.Linq;
using System.IO;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class ShrineDragonChecks
    {
        const string Pending="ShrineDragonChecks.Pending", WarmResult="ShrineDragonChecks.WarmResult";
        static double deadline;
        static int phase;
        static bool breathed, steamed, burned, rose;
        static float water;
        static IDisasterState source;
        static CastlePlaySession previousSession;
        static string wildlifeError;

        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start in edit mode.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetBool(Pending,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SelectMap()
        {
            if(!SessionState.GetBool(Pending,false))return;
            MatchChoices.MapId="castle"; MatchChoices.ModeId="ninja"; MatchChoices.BotTarget=0; MatchChoices.HandCount=0;
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=s=>{
                if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(WarmResult,-1) >= 0)
                {
                    int result = SessionState.GetInt(WarmResult,-1);
                    SessionState.EraseInt(WarmResult);
                    Directory.CreateDirectory("Logs/Warm");
                    File.WriteAllText("Logs/Warm/exit.txt", result.ToString());
                }
                if(s!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
                phase=-2; previousSession=null; wildlifeError=null;
                deadline=EditorApplication.timeSinceStartup+300;
                Application.logMessageReceived+=OnRuntimeLog;
                EditorApplication.update+=Tick;
            };
        }
        static void OnRuntimeLog(string message, string stack, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            if (stack.Contains("FossilBasinSwimmer") || stack.Contains("ShrineSamuraiPatrol") ||
                stack.Contains("ShrineDragon"))
                wildlifeError ??= message;
        }
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Tick()
        {
            try{
                Require(EditorApplication.timeSinceStartup<deadline,"Timed out in dragon phase "+phase);
                Require(wildlifeError == null, "Wildlife or encounter runtime error: " + wildlifeError);
                var session=Services.Session as CastlePlaySession;
                if(!session || Services.World==null || Time.frameCount<25)return;
                var demo = session.Demo;
                var maps = (IDemoMapControls)demo;
                if (phase == -2)
                {
                    Require(maps.CanSelectMap, "Standalone Developer Maps unavailable");
                    var options = maps.Maps;
                    foreach (string id in new[] { "mode-tycoon", "mode-ninja-temple", "mode-needlefrost-keep",
                        "mode-twin-caldera", "mode-prismatic-citadel", "mode-walled-castle", "mode-sphinx-junction",
                        "mode-gingerbread-kingdom", "epic-candy-mechanism-city", "workshop-under-the-tree",
                        "block-spires", "fossil-basin", "tidal-circuit" })
                        Require(options.Count(m => m.id == id && m.available) == 1, id + " is missing, disabled or duplicated in Developer Maps");
                    previousSession = session;
                    maps.SelectMap("fossil-basin");
                    phase++;
                    return;
                }
                if (phase == -1)
                {
                    if (session == previousSession || maps.CurrentMapId != "fossil-basin" ||
                        !NetworkActors.Alive(session.Player) || Services.World.Blocks.Count == 0) return;
                    var swimmer = GameObject.Find("Circling Spinosaurus");
                    Require(swimmer && swimmer.GetComponent<MeshRenderer>(), "Fossil Basin swimmer did not initialize");
                    Require(MatchChoices.ModeId == "demo", "A Demo-only map must load in Demo after selecting it from Shadow Lantern");
                    Require(!Enumerable.Range(0, demo.DisasterCount).Any(i => demo.DisasterLabel(i) is "Flying Dragon" or "Dragon Attack"), "Shrine dragons leaked onto Fossil Basin");
                    Require(maps.CanSelectMap && maps.Maps.Any(m => m.id == "mode-ninja-temple" && m.available), "Shrine Town unavailable from Developer Maps");
                    previousSession = session;
                    maps.SelectMap("mode-ninja-temple");
                    phase++;
                    return;
                }
                if (session == previousSession || maps.CurrentMapId != "mode-ninja-temple" ||
                    !NetworkActors.Alive(session.Player) || Services.World.Blocks.Count == 0) return;
                if (phase == 0)
                {
                    foreach (string guardName in new[] { "Crescent Guard", "Reed Watcher", "Vermilion Captain" })
                    {
                        var guard = GameObject.Find(guardName);
                        Require(guard && guard.GetComponentInChildren<SkinnedMeshRenderer>(true),
                            "Shrine patrol missing: " + guardName);
                    }
                }
                if(phase==0 || phase==2){
                    bool bronze=phase==0;
                    string label=bronze?"Dragon Attack":"Flying Dragon";
                    int index = Enumerable.Range(0, demo.DisasterCount).Single(i => demo.DisasterLabel(i) == label);
                    demo.SetDisasterValue(index, 0, 1);
                    string result = demo.LaunchDisaster(index);
                    Require(result.EndsWith("triggered / toggled.", StringComparison.Ordinal), label + ": " + result);
                    source=DisasterStates.Active.Single(s=>s.Capture()?.kind==(bronze?"DragonAttack":"FlyingDragon"));
                    water=Services.World.WaterHeight(source.Capture().points[0].position);
                    Require(source.Capture().points[0].position.y<water,"Expected underwater emergence");
                    breathed=steamed=burned=rose=false;phase++;return;
                }
                var frame=source.Capture();
                burned|=DisasterStates.Active.Any(s=>s.Capture() is DisasterFrame f && f.kind=="Fire" && f.burns!=null && f.burns.Length>0);
                if(frame!=null){
                    rose|=frame.points[0].position.y>water;
                    steamed|=frame.points[1].size>.1f;
                    if(frame.points[1].id!=0){
                        var root=GameObject.Find(frame.kind+" visual");
                        Require(root && root.GetComponentInChildren<SkinnedMeshRenderer>(),"Missing dragon skin");
                        var anim=root.GetComponentInChildren<Animation>();
                        Require(anim && anim.GetClipCount()>0,"Missing baked animation");
                        Require(root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.Any(m=>m && m.IsKeywordEnabled("_EMISSION")),"Missing eye emission");
                        breathed|=(frame.points[1].position-frame.points[1].velocity).sqrMagnitude>1;
                    }
                    return;
                }
                Require(rose&&steamed&&breathed,"Missing emergence, steam or breath in phase "+phase);
                Require(burned==(phase==3),"Wrong spreading fire behavior in phase "+phase);
                if(phase==1){phase=2;return;}
                Debug.Log("PASS Developer map selection, Demo fallback and Shrine-only listing; both dragon encounters, emergence, animation, eye emission, steam, breath, distinct block ignition and cleanup.");
                Finish(0);
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Finish(int code){
            SessionState.SetBool(Pending,false);
            Application.logMessageReceived-=OnRuntimeLog;
            EditorApplication.update-=Tick;
            if (string.IsNullOrEmpty(SessionState.GetString("DJWarm.id","")))
                EditorApplication.Exit(code);
            else
            {
                SessionState.SetInt(WarmResult,code);
                EditorApplication.ExitPlaymode();
            }
        }
    }
}

using DeadlyJenga.DevTools.Editor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FlyingHand;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CandyHudChecks
    {
        const string Key="CandyHud.ExplicitCheck";
        static CandyHudChecks(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.delayCall+=Run;}};}
        [MenuItem("Tools/Castle Prototype/Check Candy HUD")]
        public static void Run()
        {
            if(!Application.isPlaying){SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();return;}
            var session=Object.FindFirstObjectByType<CastlePlaySession>();if(session)session.StartCoroutine(Check(session));
        }
        static IEnumerator Check(CastlePlaySession session)
        {
            yield return new WaitForSecondsRealtime(2);
            var lines=new List<string>();const string output="ArtDirection/CandyHud/";Directory.CreateDirectory(output);
            void Assert(bool ok,string name){lines.Add((ok?"PASS ":"FAIL ")+name);File.WriteAllLines(output+"RuntimeChecks.txt",lines);}
            var hud=session.GetComponent<CandyDemoHud>();Assert(hud,"New HUD is installed by the demo session.");if(!hud)yield break;
            var oldMode=session.Survivor().controlMode;bool oldHand=session.PlayingHand;var oldHints=hud.hintMode;
            int listeners=Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            try
            {
                session.Survivor().controlMode=ControlMode.Desktop;session.SetRole(false);session.Survivor().RefreshMode();session.SetMenu(false);hud.hintMode=CandyDemoHud.HintMode.Always;
                yield return new WaitForSecondsRealtime(.6f);
                Assert(hud.GameplayHintsVisible,"Control hints appear while playing.");Assert(hud.InputLabel=="Keyboard + mouse","Desktop prompts identify supported inputs.");
                var desktop=hud.Hints();Assert(desktop[0].key=="W A S D"&&desktop[1].key=="SPACE","Desktop prompts match real movement / jump bindings.");
                Assert(hud.GetComponentsInChildren<Collider>(true).Length==0&&hud.GetComponentsInChildren<Rigidbody>(true).Length==0,"HUD adds no physics colliders or bodies.");
                ScreenCapture.CaptureScreenshot(output+"Gameplay.png");
                session.SetMenu(true);yield return new WaitForSecondsRealtime(.6f);
                Assert(!hud.GameplayHintsVisible,"Menu hides gameplay hints.");Assert(session.Survivor().InputSuspended&&session.HandActor().motor.InputSuspended,"Opening the menu suspends local movement input.");
                ScreenCapture.CaptureScreenshot(output+"Menu.png");
                yield return new WaitForSecondsRealtime(.3f);
                session.SetMenu(false);hud.hintMode=CandyDemoHud.HintMode.Hidden;yield return new WaitForSecondsRealtime(.5f);
                Assert(!hud.GameplayHintsVisible,"Hidden hint setting actually hides the control overlay.");
                hud.hintMode=CandyDemoHud.HintMode.Always;session.Survivor().controlMode=ControlMode.VR;session.Survivor().RefreshMode();session.SetMenu(false);
                yield return new WaitForSecondsRealtime(.6f);
                Assert(hud.InputLabel=="VR controllers"||hud.InputLabel=="Hand tracking","VR uses VR / hand prompts, not keyboard instructions.");
                var canvas=hud.ControlHintCanvas;
                Assert(canvas&&canvas.renderMode==RenderMode.WorldSpace,"VR hints use a world-space canvas visible to both eyes.");
                session.SetMenu(true);yield return new WaitForSecondsRealtime(.3f);
                Assert(Object.FindFirstObjectByType<CastleWorldMenu>(),"VR menu remains available.");
                Assert(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==listeners,"HUD creates no extra audio listener.");
                lines.Add("VR layout uses forced control mode; real headset legibility and hand gestures still need device testing.");File.WriteAllLines(output+"RuntimeChecks.txt",lines);
            }
            finally
            {
                session.Survivor().controlMode=oldMode;session.Survivor().RefreshMode();hud.hintMode=oldHints;session.SetRole(oldHand);session.SetMenu(true);Debug.Log(string.Join("\n",lines));
            }
        }
    }
}

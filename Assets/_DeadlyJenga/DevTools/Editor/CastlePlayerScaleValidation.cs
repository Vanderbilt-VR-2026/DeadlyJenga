using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using FlyingHand;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastlePlayerScaleValidation
    {
        const string Request="ArtDirection/CandyTropical/verify-player-scale.request";
        const string Key="CastleHalfPlayerValidation";
        static CastlePlayerScaleValidation()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
                File.Delete(Request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                {
                    SessionState.SetBool(Key,false);
                    EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Validate());
                }
            };
        }
        [MenuItem("Tools/Castle Prototype/Validate Half Size Player")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Game")
                throw new System.InvalidOperationException("Open the Game scene first.");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static IEnumerator Validate()
        {
            yield return null;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            // Isolate initial spawn from physical keyboard/mouse input while the tester waits.
            session.SetRole(false);session.SetMenu(true);
            yield return new WaitForSeconds(2);
            var p=session.Player;p.RefreshMode();
            var report=new StringBuilder();
            void Check(string label,bool ok)=>report.AppendLine((ok?"PASS ":"FAIL ")+label);
            Check("Half-size body height in actual player loop",Mathf.Abs(p.Capsule.height-.85f)<.001f);
            Check("Half-size body radius",Mathf.Abs(p.Capsule.radius-.12f)<.001f);
            Check("Half-size desktop camera height",Mathf.Abs(p.transform.InverseTransformPoint(p.viewCamera.transform.position).y-.8f)<.001f);
            Check($"Half-size player stays grounded on top: grounded={p.Grounded}, actual={p.transform.position}, spawn={session.LastSpawnFeet}, drift={Vector3.Distance(p.transform.position,session.LastSpawnFeet):F3}",p.Grounded&&Vector3.Distance(p.transform.position,session.LastSpawnFeet)<.2f);
            Check("Movement retained and jump requires vault",Mathf.Abs(p.walkSpeed-4.6f)<.001f&&Mathf.Abs(p.jumpHeight-1.82f)<.001f);
            p.controlMode=ControlMode.VR;p.RefreshMode();
            Check("Tracked-head translation uses half scale",p.cameraOffset.localScale==Vector3.one*.5f);
            p.controlMode=ControlMode.Auto;p.RefreshMode();
            Check("Returning to desktop keeps half-size eye and body",Mathf.Abs(p.Capsule.height-.85f)<.001f&&Mathf.Abs(p.viewCamera.transform.localPosition.y-.8f)<.001f);
            File.WriteAllText(ProjectPath.Report("PlayerScaleValidation.txt"),report.ToString());
            yield return CastleReferenceValidation.Validate(session);
            yield return CastleTraversalValidation.Validate(session);
            yield return CastleInputValidation.Validate(session);
            File.WriteAllText("ArtDirection/CandyTropical/player-scale-complete.txt","Half-size player validation completed. Inspect PlayerScaleValidation, ReferenceValidation, TraversalValidation and InputValidation reports.");
            EditorApplication.isPlaying=false;
        }
    }
}

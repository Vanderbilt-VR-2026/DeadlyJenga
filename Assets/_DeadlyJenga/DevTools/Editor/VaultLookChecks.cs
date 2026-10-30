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
    [InitializeOnLoad] public static class VaultLookChecks
    {
        const string Key="VaultLookChecks";
        static string Report=>ProjectPath.Report("VaultLook.txt");
        static int failures,errors;
        static VaultLookChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("VaultLook.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Health and lobby checks");Object.DontDestroyOnLoad(go);go.AddComponent<VaultLookCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Look Ahead Vault Glow")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before this check.");
            File.WriteAllText(Report,"Look-ahead vault glow integration\n");
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
        static IEnumerator CheckScene()
        {
            yield return new WaitForSeconds(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Actual Game scene starts");
            var player=session.Player.gameObject.GetComponent<CastleSurvivor>();player.controlMode=FlyingHand.ControlMode.Desktop;player.RefreshMode();
            Call(session,"OnApplicationFocus",true);session.SetMenu(false);
            var fixture=new GameObject("Temporary vault look geometry");var origin=new Vector3(1000,1000,1000);
            GameObject Box(string name,Vector3 offset,Vector3 scale)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(fixture.transform);go.transform.position=origin+offset;go.transform.localScale=scale;return go;}
            Box("Floor",new Vector3(0,-.1f,0),new Vector3(30,.2f,30));
            var ledge=Box("Look-ahead ledge",new Vector3(0,.7f,3.6f),new Vector3(4,1.4f,1.2f));
            player.Teleport(origin,Quaternion.identity);player.viewCamera.transform.position=origin+new Vector3(0,1.6f,0);
            void Aim()=>player.viewCamera.transform.LookAt(origin+new Vector3(0,1.4f,ledge.transform.position.z-origin.z-.6f));
            Aim();Physics.SyncTransforms();
            IVaultLookTargetProvider look=player;IVaultTargetProvider ready=player;
            Check(!ready.TryGetVaultTarget(out _,out _,out _),"Distant ledge cannot activate a vault now");
            var before=player.transform.position;var oldGrip=(Vector3)Field(player,"candidateGrip");
            Check(look.TryGetLookVaultTarget(5,out var surface,out var edge,out var landing)&&surface==ledge.GetComponent<Collider>(),"Centered look-ahead edge is found before vault can activate");
            Check(player.transform.position==before&&!player.Vaulting&&(Vector3)Field(player,"candidateGrip")==oldGrip,"Preview never moves the player, starts a vault or replaces the active grip candidate");
            Check(landing.y>origin.y+1.4f&&Vector3.Distance(player.viewCamera.transform.position,edge)<=5,"Preview provides a clear landing within five meters");
            var glow=player.GetComponent<CandyVaultGlow>();Check(glow&&glow.tint.b>glow.tint.g*3&&glow.brightness>=3&&glow.topDepth>=.14f,"Prefab glow migrates to a pronounced deep-blue wider ribbon");
            Set(glow,"nextLookCheck",0f);Call(glow,"LateUpdate");Check((Collider)Field(glow,"lastSurface")==surface,"Presentation accepts the optional look-ahead target");
            Set(glow,"opacity",1f);Call(glow,"LateUpdate");var rim=(GameObject)Field(glow,"visual");Check(rim.GetComponent<MeshRenderer>().enabled&&rim.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"Look-ahead preview renders the existing rim mesh");
            var material=rim.GetComponent<MeshRenderer>().sharedMaterial;Check(material.GetColor("_Tint").b>material.GetColor("_Tint").g&&material.GetFloat("_Brightness")>=3,"Rendered rim uses stronger blue material values");
            ledge.transform.position+=Vector3.right*.1f;Physics.SyncTransforms();Call(glow,"LateUpdate");Check(Vector3.Distance(rim.transform.position,ledge.transform.position)<.001f,"Rim remains attached when its block moves");ledge.transform.position-=Vector3.right*.1f;
            ledge.transform.position=origin+new Vector3(0,.7f,6.6f);Aim();Physics.SyncTransforms();Check(!look.TryGetLookVaultTarget(5,out _,out _,out _)&&!look.TryGetLookVaultTarget(50,out _,out _,out _),"Both ordinary and oversized requests stop at five meters");
            ledge.transform.position=origin+new Vector3(0,.7f,3.6f);Aim();Physics.SyncTransforms();
            var wall=Box("Occluding wall",new Vector3(0,2,1.5f),new Vector3(5,4,.2f));Physics.SyncTransforms();Check(!look.TryGetLookVaultTarget(5,out _,out _,out _),"An intervening wall blocks preview rather than showing an edge through it");wall.SetActive(false);
            var ceiling=Box("Blocked landing",new Vector3(0,1.4f+player.Capsule.height*.65f,3.6f),new Vector3(5,.25f,1.5f));Physics.SyncTransforms();Check(!look.TryGetLookVaultTarget(5,out _,out _,out _),"Low ceiling rejects an edge without standing room");ceiling.SetActive(false);
            ledge.transform.rotation=Quaternion.Euler(0,0,45);Physics.SyncTransforms();Check(!look.TryGetLookVaultTarget(5,out _,out _,out _),"Steep top without a walkable face is not advertised as a vault edge");
            ledge.transform.rotation=Quaternion.Euler(0,0,180);Physics.SyncTransforms();Check(look.TryGetLookVaultTarget(5,out _,out _,out _),"Upside-down block with a level world-space top remains eligible");ledge.transform.rotation=Quaternion.identity;
            player.viewCamera.transform.rotation=Quaternion.Euler(0,90,0);Check(!look.TryGetLookVaultTarget(5,out _,out _,out _),"Looking away clears the preview target");Aim();
            player.InputSuspended=true;Check(!look.TryGetLookVaultTarget(5,out _,out _,out _),"Paused input suppresses look-ahead targeting");player.InputSuspended=false;
            var rules=session.Modes.Rules.Copy();rules.canVault=false;player.ApplyModeRules(rules);Check(!look.TryGetLookVaultTarget(5,out _,out _,out _),"Game mode vault restrictions also suppress look-ahead preview");rules.canVault=true;player.ApplyModeRules(rules);
            var shader=Shader.Find("DeadlyJenga/Candy Vault Glow");Check(shader&&!ShaderUtil.ShaderHasError(shader),"Blue vault shader imports without errors");
            var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<100;i++)look.TryGetLookVaultTarget(5,out _,out _,out _);watch.Stop();File.AppendAllText(Report,"MEASURE 100 look-ahead checks: "+watch.Elapsed.TotalMilliseconds.ToString("0.00")+" ms on this laptop. Presentation requests at most 10 per second.\n");
            Object.Destroy(fixture);Check(errors==0,"No gameplay runtime errors during checks");
        }
    }
    public sealed class VaultLookCheckDriver:MonoBehaviour {}
}
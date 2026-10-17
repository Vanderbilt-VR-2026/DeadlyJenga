using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.DevTools.Editor;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastlePrototype.Editor;
namespace DeadlyJenga.CastleSocial.Editor
{
    [InitializeOnLoad]
    public static class SocialMergeValidation
    {
        const string Key="CastleMergeSuite";
        static readonly string[] reports={"ArtDirection/CastleSocial/UnityValidation.txt","Logs/Validation/ReferenceValidation.txt","Logs/Validation/TraversalValidation.txt","Logs/Validation/InputValidation.txt","ArtDirection/VRViewChecks.txt","ArtDirection/SeamCameraChecks.txt","ArtDirection/SlopeChecks.txt","ArtDirection/StaminaChecks.txt","ArtDirection/ClimbChecks.txt"};
        static SocialMergeValidation()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                int phase=SessionState.GetInt(Key+"Phase",0);
                if(state==PlayModeStateChange.EnteredPlayMode&&phase!=1)EditorApplication.delayCall+=()=>
                {var session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();MonoBehaviour host=session;if(!host)host=UnityEngine.Object.FindFirstObjectByType<IslandLobby>();host.StartCoroutine(Guard(Check(session,phase)));};
                if(state==PlayModeStateChange.EnteredEditMode)
                {SessionState.SetInt(Key+"Phase",phase+1);EditorApplication.delayCall+=Next;}
            };
        }
        public static void RunBatch()
        {
            File.WriteAllText(ProjectPath.Report("MainMergeValidation.txt"),"Integrated merge regression\n");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetString(Key+"Started",DateTime.UtcNow.Ticks.ToString());SessionState.SetInt(Key+"Phase",0);Next();
        }
        static void Next()
        {
            int phase=SessionState.GetInt(Key+"Phase",0);
            if(phase>=6)
            {
                bool fail=SessionState.GetBool(Key+"Failed",false);
                foreach(var path in reports)
                {bool exists=File.Exists(path);bool bad=!exists||File.GetLastWriteTimeUtc(path).Ticks<long.Parse(SessionState.GetString(Key+"Started","0"))||File.ReadAllText(path).Contains("FAIL");fail|=bad;File.AppendAllText(ProjectPath.Report("MainMergeValidation.txt"),(bad?"FAIL ":"PASS ")+path+"\n");}
                SessionState.SetBool(Key,false);MatchSetup.StartLocalSession=false;EditorApplication.Exit(fail?1:0);return;
            }
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=phase==1;
            EditorSceneManager.OpenScene(ProjectPath.Find(phase==5?"Lobby.unity":"Game.unity"));
            if(phase==1)SocialValidation.Run();else EditorApplication.EnterPlaymode();
        }
        static IEnumerator Guard(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object current=null;bool has=false;Exception error=null;
                try{has=stack.Peek().MoveNext();if(has)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(ProjectPath.Report("MainMergeValidation.txt"),"FAIL "+error+"\n");Debug.LogException(error);EditorApplication.ExitPlaymode();yield break;}
                if(!has){stack.Pop();continue;}if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
        }
        static IEnumerator Check(CastlePlaySession session,int phase)
        {
            yield return null;yield return null;
            if(phase==5)
            {
                if(EditorBuildSettings.scenes.First(x=>x.enabled).path!=ProjectPath.Find("Lobby.unity"))throw new Exception("Lobby must be the default build scene");
                if(!UnityEngine.Object.FindFirstObjectByType<IslandLobby>())throw new Exception("Island lobby failed to initialize");
                foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach(var t in go.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing lobby script on "+t.name);
                File.AppendAllText(ProjectPath.Report("MainMergeValidation.txt"),"PASS Lobby launches with match setup UI, no missing scripts, and remains first enabled build scene.\n");
                EditorApplication.ExitPlaymode();yield break;
            }
            if(phase==0)
            {
                var social=session.GetComponent<SocialWorld>();
                if(!social||!social.IsAuthority||session.Network)throw new Exception("Direct scene social initialization failed");
                session.Bots().SpawnBot();yield return new WaitForSeconds(.6f);
                if(social.Agents.Count!=1)throw new Exception("Standalone bot social adapter absent");
                File.AppendAllText(ProjectPath.Report("MainMergeValidation.txt"),"PASS Direct Game scene works without a network session; bot social adapter initializes.\n");
                EditorApplication.ExitPlaymode();
            }
            else if(phase==2)
            {
                yield return CastleReferenceValidation.Validate(session);
                yield return CastleTraversalValidation.Validate(session);
                yield return CastleInputValidation.Validate(session);
                yield return (IEnumerator)typeof(CastleVRViewChecks).GetMethod("Check",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);

            }
            else if(phase==3)yield return CastleStaminaChecks.Check();
            else if(phase==4)yield return (IEnumerator)typeof(CastleSlopeChecks).GetMethod("Check",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        }
    }
}

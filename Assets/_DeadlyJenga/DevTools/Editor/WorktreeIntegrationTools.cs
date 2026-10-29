using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class WorktreeIntegrationTools
    {
        const string SmokeKey="WorktreeIntegratedSmoke";
        static int smokeErrors;
        [Serializable] sealed class SavedScenes{public SceneSetup[] values;}
        static WorktreeIntegrationTools()
        {
            EditorApplication.update+=Poll;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(SmokeKey,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {smokeErrors=0;Application.logMessageReceived+=SmokeLog;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(SmokeGuard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=SmokeLog;SessionState.SetBool(SmokeKey,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(SmokeKey+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(SmokeKey+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(SmokeKey+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SavedScenes>(SessionState.GetString(SmokeKey+"Scenes","")).values);
                    File.AppendAllText(ProjectPath.Report("WorktreeMapsPlay.txt"),"EXIT runtimeErrors="+smokeErrors+"\n");
                }
            };
        }
        static void SmokeLog(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){smokeErrors++;File.AppendAllText(ProjectPath.Report("WorktreeMapsPlay.txt"),"ERROR "+message+"\n");}}
        static void Poll()
        {
            string path=ProjectPath.Report("WorktreeIntegration.request");
            if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string command=File.ReadAllText(path).Trim();File.Delete(path);
            try
            {
                if(command=="audit")Audit();
                else if(command=="install"){LinkMaps();Audit();}
                else if(command=="powerups")PowerupBotChecks.Run();
                else if(command=="firearms")FirearmUpgradeChecks.Run();
                else if(command=="aim")FirearmUpgradeChecks.RunAimOnly();
                else if(command=="village")BreakwaterVillagePlayChecks.Run();
                else if(command=="maps-play")StartSmoke();
                else if(command=="contracts")EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validation/Check Bot Objective Contracts");
                else if(command=="drafts")EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Maps/Remove Empty Drafts and Create Andy");
                else throw new ArgumentException(command);
            }
            catch(Exception e){File.WriteAllText(ProjectPath.Report("WorktreeIntegration-error.txt"),e.ToString());Debug.LogException(e);}
        }
        static void StartSmoke()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes before smoke checks.");
            SessionState.SetString(SmokeKey+"Scenes",JsonUtility.ToJson(new SavedScenes{values=scenes}));
            SessionState.SetString(SmokeKey+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(SmokeKey+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(SmokeKey+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(SmokeKey,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(ProjectPath.Report("WorktreeMapsPlay.txt"),"Actual Game runtime launch of imported map variants, followed by Mystery on Breakwater. Short smoke, not FPS, collapse or headset certification.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static IEnumerator SmokeGuard()
        {
            var run=Smoke();
            try
            {
                while(true)
                {
                    bool more=false;object value=null;Exception error=null;
                    try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){error=e;}
                    if(error!=null){File.AppendAllText(ProjectPath.Report("WorktreeMapsPlay.txt"),"FAIL "+error+"\n");break;}
                    if(!more)break;yield return value;
                }
            }
            finally{(run as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();}
        }
        static IEnumerator Smoke()
        {
            string path=ProjectPath.Report("WorktreeMapsPlay.txt");
            void Check(bool pass,string label){File.AppendAllText(path,(pass?"PASS ":"FAIL ")+label+"\n");if(!pass)throw new InvalidOperationException(label);}
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float deadline=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"solo Game initializes");
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();session.SetMenu(true);if(session.Social!=null)session.Social.VoiceEnabled=false;
            foreach(string id in WorkshopStructureStudy.Ids.Concat(new[]{"breakwater-village","sparse-festival-city-optimized"}))
            {
                MatchSetup.MapId=id;MatchSetup.ApplyMap();yield return null;
                var world=Services.World;Check(world!=null&&world.Blocks.Count>0,id+" loads through production map selection");
                Check(id.StartsWith("workshop-")?world is WorkshopStructureMap:id=="breakwater-village"?world is BreakwaterVillageMap:world is ITraversalCollisionWorld w&&w.UsesOriginalColliders,id+" uses intended implementation");
                session.Player.Place(world.SafeSpawnPoint(),Quaternion.identity);int members=world.Blocks.Count;
                yield return new WaitForSeconds(.6f);
                Check(world.Blocks.Count==members&&world.Blocks.All(b=>b&&!float.IsNaN(b.position.sqrMagnitude)&&!float.IsInfinity(b.position.sqrMagnitude)),id+" keeps valid physical bodies after live ticks");
            }
            MatchSetup.MapId="breakwater-village";MatchSetup.ModeId="mystery";MatchSetup.ApplyMap();yield return null;
            var rules=MatchSetup.DefaultModeRules(GameModeKind.MurderMystery,MatchSetup.SelectedMap);rules.countdownSeconds=0;
            session.Modes.Configure(rules);session.Player.Place(Services.World.SafeSpawnPoint(),Quaternion.identity);
            for(int i=0;i<3;i++)session.SurvivorBots.SpawnOne();session.Modes.StartRound();yield return new WaitForSeconds(2);
            Check(Services.World is BreakwaterVillageMap&&session.Modes.Rules.mode==GameModeKind.MurderMystery,"Breakwater starts Murder Mystery on the walkable island");
            Check(session.SurvivorBots.Count>=2,"Mystery has actual survivor bots");
            Check(smokeErrors==0,"runtime reports no errors");File.AppendAllText(path,"COMPLETE\n");
        }
        static void LinkMaps()
        {
            var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            foreach(string id in WorkshopStructureStudy.Ids.Concat(new[]{"breakwater-village"}))
            {
                var entry=catalog.maps.Single(m=>m.id==id);
                entry.mapPrefab=Resources.Load<GameObject>("ModeMaps/Mode_"+id);
                if(!entry.mapPrefab)throw new InvalidOperationException("Missing imported map "+id);
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Verify All Integrated Maps")]
        public static void Audit()
        {
            var report=new StringBuilder("Integrated worktree map audit. Actual Unity Resources and isolated scene instantiation.\n");
            int failures=0,count=0;
            void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);if(!value)failures++;}
            var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            Check(catalog,"runtime lobby catalog loads");
            var oldWorld=Services.World;var active=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach(var entry in catalog.maps)
                {
                    Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath),entry.title+" game scene resolves");
                    if(entry.id.StartsWith("workshop-")||entry.id=="breakwater-village")Check(entry.mapPrefab,entry.title+" imported prefab reference resolves");
                    if(!entry.mapPrefab)continue;
                    GameObject root=null;
                    try
                    {
                        root=Object.Instantiate(entry.mapPrefab);
                        Check(root.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),entry.title+" has no missing scripts");
                        var world=root.GetComponents<MonoBehaviour>().OfType<IWorldQuery>().FirstOrDefault();
                        Check(world!=null,entry.title+" has a world provider");
                        if(entry.id.StartsWith("workshop-"))Check(world is WorkshopStructureMap,entry.title+" uses its own workshop implementation");
                        if(world is WorkshopStructureMap workshop)workshop.Rebuild();
                        if(world is BreakwaterVillageMap village)village.Rebuild();
                        if(world is WorkshopStructureMap||world is BreakwaterVillageMap)
                        {
                            Check(world.Blocks.Count>0&&world.Blocks.All(b=>b),entry.title+" generates physical members");
                            var spawn=world.SafeSpawnPoint();Check(!float.IsNaN(spawn.sqrMagnitude)&&!float.IsInfinity(spawn.sqrMagnitude),entry.title+" provides a finite spawn");
                            var synced=(ISyncedWorld)world;
                            Check(synced.LoadLayout(synced.SaveLayout(),world.Blocks.Count,new Pose(root.transform.position,root.transform.rotation),root.transform.localScale),entry.title+" layout round trip");
                        }
                        count++;
                    }
                    catch(Exception e){Check(false,entry.title+": "+e);}
                    finally{if(root)Object.DestroyImmediate(root);}
                }
                Check(catalog.maps.Select(m=>m.id).Distinct().Count()==catalog.maps.Length,"map IDs are unique");
                Check(catalog.maps.Any(m=>m.id=="breakwater-village")&&catalog.maps.Count(m=>m.id.StartsWith("workshop-"))==9,"Breakwater and all nine workshop maps are selectable");
            }
            finally
            {
                if(active.IsValid())SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(scene,true);Services.World=oldWorld;
            }
            report.AppendLine("COMPLETE prefabMaps="+count+" failures="+failures);
            File.WriteAllText(ProjectPath.Report("WorktreeIntegration.txt"),report.ToString());Debug.Log(report.ToString());
        }
    }
}

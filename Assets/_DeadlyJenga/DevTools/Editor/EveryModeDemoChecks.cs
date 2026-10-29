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
    [InitializeOnLoad] public static class EveryModeDemoChecks
    {
        const string Key="EveryModeDemo";
        static int checks,errors;static bool failed,completed;
        static string Report=>ProjectPath.Report(SessionState.GetBool(Key+"VisualOnly",false)?"EveryModeDemo-Visual.txt":"EveryModeDemo.txt");
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static EveryModeDemoChecks()
        {
            EditorApplication.update+=()=>
            {
                string path=ProjectPath.Report("EveryModeDemo.request");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                string command=File.ReadAllText(path).Trim(),pid=System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                if(command!=pid&&command!=pid+"|probe"&&command!=pid+"|visual")return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(path);if(command.EndsWith("|probe",StringComparison.Ordinal)){try{ProbeSnapshot();}finally{ValidationRunGate.Release(Key);}return;}Start(command.EndsWith("|visual",StringComparison.Ordinal));
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=completed=false;if(!SessionState.GetBool(Key+"RefreshBlocked",false)){AssetDatabase.DisallowAutoRefresh();SessionState.SetBool(Key+"RefreshBlocked",true);}EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.update-=BeginTest;EditorApplication.update+=BeginTest;}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update-=BeginTest;RestoreCaptureSize();ValidationRunGate.Release(Key);
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);EditorApplication.UnlockReloadAssemblies();}
                    if(SessionState.GetBool(Key+"RefreshBlocked",false)){SessionState.SetBool(Key+"RefreshBlocked",false);AssetDatabase.AllowAutoRefresh();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    failed|=!completed;File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                }
            };
        }
        static void BeginTest()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();if(!session)return;
            EditorApplication.update-=BeginTest;EditorApplication.isPaused=false;if(SessionState.GetBool(Key+"VisualOnly",false))ConfigureCaptureSize();session.StartCoroutine(Guard());
        }
        static void ConfigureCaptureSize()
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");var current=sizesType.GetProperty("currentGroupType",flags|System.Reflection.BindingFlags.Static)?.GetValue(sizes)??Enum.Parse(groupType,"Standalone");var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{current});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,new[]{Enum.Parse(kind,"FixedResolution"),(object)1280,800,"Demo UI QA"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            var viewType=assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(viewType).FirstOrDefault();
            if(!view)throw new InvalidOperationException("An existing Game view is required for UI capture.");
            var selected=viewType.GetProperty("selectedSizeIndex",flags);SessionState.SetInt(Key+"ViewSize",(int)selected.GetValue(view));selected.SetValue(view,index);
            viewType.GetMethod("SizeSelectionCallback",flags)?.Invoke(view,new object[]{index,null});
            File.AppendAllText(Report,"CAPTURE SETUP: target="+EditorUserBuildSettings.activeBuildTarget+", group="+current+", selected="+selected.GetValue(view)+", requested="+index+", targetSize="+viewType.GetProperty("targetSize",flags)?.GetValue(view)+"\n");
            SessionState.SetBool(Key+"ViewSizeChanged",true);((EditorWindow)view).Repaint();
        }
        static void RestoreCaptureSize()
        {
            if(!SessionState.GetBool(Key+"ViewSizeChanged",false))return;
            SessionState.SetBool(Key+"ViewSizeChanged",false);var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
            if(view)type.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(view,SessionState.GetInt(Key+"ViewSize",0));
        }
        static void ProbeSnapshot()
        {
            var snapshot=JsonUtility.FromJson<MatchFrame>(File.ReadAllText(ProjectPath.Report("EveryModeDemo-invalid-CaptureFlag.json")));
            bool before=ModeFrameValidation.Frame(snapshot,1);int empty=0;
            foreach(var flag in snapshot.flags)if(flag.carrier==""){empty++;flag.carrier=null;}
            bool after=ModeFrameValidation.Frame(snapshot,1);
            File.WriteAllText(ProjectPath.Report("EveryModeDemo-CTFProbe.txt"),$"Captured runtime CTF snapshot: originalValid={before}, emptyCarriers={empty}, validAfterOnlyEmptyCarrierNormalization={after}. Diagnostic only.\n");
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Demo Controls In Every Mode")]
        public static void Run()=>Start(false);
        static void Start(bool visualOnly)
        {
            SessionState.SetBool(Key+"VisualOnly",visualOnly);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Actual Game session with a small fixture world: all-mode equipment, menus, hazards, hand switching and two-peer snapshot transport.\n");
            AssetDatabase.DisallowAutoRefresh();SessionState.SetBool(Key+"RefreshBlocked",true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {
            if(trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")){File.AppendAllText(Report,"EDITOR STARTUP NOTE: Unity search-index initialization exception; separate from gameplay. "+message+"\n");return;}
            if(Fusion.NetworkProjectConfig.Global.PeerMode==Fusion.NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"TRANSPORT FIXTURE NOTE: empty scene multiple-peer diagnostic; packet test, not replicated gameplay scene.\n");return;}
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}
        }
        static void Check(bool pass,string label)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");checks++;if(!pass)throw new InvalidOperationException(label);}
        static IEnumerator Guard()
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Test());
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();completed=!failed;EditorApplication.ExitPlaymode();
        }
        static IEnumerator Test()
        {
            Application.runInBackground=true;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float until=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"Actual Game scene initializes");
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            var original=Services.World;var oldRoot=(original as Component)?.gameObject;
            bool active=oldRoot&&oldRoot.activeSelf;if(oldRoot&&oldRoot!=session.gameObject)oldRoot.SetActive(false);
            var fixture=new GameObject("All-mode demo control fixture");fixture.transform.position=new Vector3(1000,0,1000);
            var world=fixture.AddComponent<FakeWorldQuery>();world.waterHeight=-100;world.spawnPoint=new Vector3(1000,.05f,1000);world.topHeight=0;world.radius=40;
            world.playableBounds=new Bounds(Vector3.up*10,new Vector3(100,30,100));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.SetParent(fixture.transform,false);floor.transform.localPosition=Vector3.down*.5f;floor.transform.localScale=new Vector3(100,1,100);
            var body=floor.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;world.blocks.Add(body);
            var runtime=session.Modes;var player=session.Survivor();
            MatchSetup.FromLobby=true;MatchSetup.BotTarget=4;
            try
            {
                for(int i=0;i<3;i++)session.SurvivorBots.SpawnOne();
                foreach(var member in session.SurvivorBots.Members)if(member){var brain=member.GetComponent<CastleBotBrain>();if(brain)brain.enabled=false;}
                yield return null;
                var bulk=(IDemoLoadoutControls)runtime;
                if(!SessionState.GetBool(Key+"VisualOnly",false))foreach(GameModeKind mode in Enum.GetValues(typeof(GameModeKind)))
                {
                    var rules=ModeSimulation.Defaults(mode);rules.countdownSeconds=0;rules.roundSeconds=600;rules.fallDamage=false;rules.island=IslandRule.Walkable;
                    runtime.Configure(rules);runtime.StartRound();player.Place(world.spawnPoint,Quaternion.identity);player.InputSuspended=true;
                    int offset=0;foreach(var member in session.SurvivorBots.Members)if(member){var brain=member.GetComponent<CastleBotBrain>();if(brain)brain.enabled=false;var bot=member.GetComponent<CastleSurvivor>();bot.Place(world.spawnPoint+Vector3.right*(4+(offset++)*4),Quaternion.identity);bot.InputSuspended=true;}
                    yield return new WaitForSeconds(.25f);
                    until=Time.realtimeSinceStartup+5;
                    while(runtime.Frame.players.Any(p=>p.dying)&&Time.realtimeSinceStartup<until)yield return null;
                    yield return new WaitForSeconds(.25f);
                    player.Place(world.spawnPoint,Quaternion.identity);player.InputSuspended=true;
                    foreach(var member in session.SurvivorBots.Members)if(member){var brain=member.GetComponent<CastleBotBrain>();if(brain)brain.enabled=false;member.GetComponent<CastleSurvivor>().InputSuspended=true;}
                    Check(runtime.Frame.players.All(p=>!p.dying),"Round-start character transitions finish in "+mode);
                    Check(runtime.Frame.phase==RoundPhase.Playing&&session.Demo.CanCommand,"Host demo commands available in "+mode);
                    foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
                    {
                        if(kind==EquipmentKind.None||kind==EquipmentKind.FlyingHand)continue;
                        bool given=runtime.GiveEquipment(kind);var held=runtime.EquipmentFor(runtime.LocalActorId);
                        Check(given&&(held.kind==kind||held.activePower==kind),"Self grant "+kind+" in "+mode);
                    }
                    Check(runtime.GiveEveryoneEquipment(EquipmentKind.DualRifle,true)&&bulk.RespawnEquipment==EquipmentKind.DualRifle,"Persistent everyone grant in "+mode);
                    Check(runtime.Frame.players.Where(p=>p.alive&&!p.dying&&p.role!="hand"&&p.role!="spectator").All(p=>runtime.EquipmentFor(p.id).kind==EquipmentKind.DualRifle),"Players and bots receive the item in "+mode);
                    var saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                    if(!ModeFrameValidation.Frame(saved,world.Blocks.Count))File.WriteAllText(ProjectPath.Report("EveryModeDemo-invalid-"+mode+".json"),runtime.CaptureFrameJson());
                    Check(saved.demoRespawnEquipment==EquipmentKind.DualRifle&&ModeFrameValidation.Frame(saved,world.Blocks.Count),"Production snapshot accepts active loadout in "+mode);
                    if(mode==GameModeKind.CaptureFlag)CheckFlagRoundtrip(saved,world.Blocks.Count);
                    string role=runtime.Frame.players.First(p=>p.id==runtime.LocalActorId).role;
                    session.SetRole(true);yield return new WaitForSeconds(.15f);
                    Check(session.PlayingHand&&runtime.Frame.players.First(p=>p.id==runtime.LocalActorId).role=="hand","Demo hand switch updates mode state in "+mode);
                    session.SetRole(false);yield return new WaitForSeconds(.15f);
                    Check(!session.PlayingHand&&runtime.Frame.players.First(p=>p.id==runtime.LocalActorId).role==role,"Returning from hand restores mode role in "+mode);
                    player.Place(world.spawnPoint,Quaternion.identity);player.InputSuspended=true;
                    Check(runtime.SpawnEquipment(EquipmentKind.Speed),"Ground pickup command in "+mode);
                    Check(bulk.GiveEveryoneEquipment(EquipmentKind.None,false),"Recurring grant can be stopped in "+mode);
                }
                var ctf=ModeSimulation.Defaults(GameModeKind.CaptureFlag);ctf.countdownSeconds=0;ctf.fallDamage=false;ctf.island=IslandRule.Walkable;
                runtime.Configure(ctf);runtime.StartRound();player.Place(world.spawnPoint,Quaternion.identity);yield return new WaitForSeconds(.25f);
                if(!SessionState.GetBool(Key+"VisualOnly",false))
                {
                Check(runtime.GiveEveryoneEquipment(EquipmentKind.Blaster,true),"CTF persistent loadout selected");
                var victim=session.SurvivorBots.Members.First().GetComponent<CastleSurvivor>();victim.Eliminate(null);yield return new WaitForSeconds(.3f);
                until=Time.realtimeSinceStartup+12;while(!victim.Alive&&Time.realtimeSinceStartup<until)yield return null;yield return new WaitForSeconds(.4f);
                Check(victim.Alive&&runtime.EquipmentFor(victim.ActorId).kind==EquipmentKind.Blaster,"Real CTF respawn reapplies the selected item");
                var hazard=DemoDisasters.Prefabs.FirstOrDefault(p=>p&&p.name.Contains("Gravity"));Check(hazard,"Catalog supplies gravity disaster");
                Check(DemoDisasters.Execute(session,hazard.name),"Disaster starts while CTF is active");yield return null;
                Check(session.GetComponentsInChildren<MonoBehaviour>().OfType<IHazard>().Any(h=>h.Active),"Disaster is actually active");
                session.Demo.Run(DemoCommand.StopDisasters);yield return null;
                Check(!session.GetComponentsInChildren<MonoBehaviour>().OfType<IHazard>().Any(h=>h.Active),"Demo stop command ends the active disaster");
                Check(runtime.GiveEveryoneEquipment(EquipmentKind.PoweredArmor,true)&&runtime.RequestThrust(true),"CTF suit snapshot fixture starts real thrust");
                var thrust=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());runtime.RequestThrust(false);
                var stopped=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                yield return PoweredSuitNetworkChecks.Run(runtime,thrust,stopped,Check);
                }
                var replicaObject=new GameObject("Non-authoritative demo command fixture");var replica=replicaObject.AddComponent<MatchModeRuntime>();
                Check(!replica.GiveEquipment(EquipmentKind.Blaster)&&!replica.GiveEveryoneEquipment(EquipmentKind.Blaster,true)&&!replica.SpawnEquipment(EquipmentKind.Blaster),"Non-authoritative runtimes cannot grant or spawn items");Object.Destroy(replicaObject);
                session.SetMenu(true);var hud=session.GetComponent<CandyDemoHud>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,0);typeof(CandyDemoHud).GetField("subsection",flags).SetValue(hud,1);
                yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
                var actions=(System.Collections.Generic.List<Action>)typeof(CandyDemoHud).GetField("navActions",flags).GetValue(hud);
                Check(actions.Count>4&&actions[4]!=null,"Actual desktop equipment card is enabled during CTF");actions[4]();
                Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Blaster,"Actual desktop card gives the requested item during CTF");
                session.SetMenu(true);yield return new WaitForEndOfFrame();
                if(SessionState.GetBool(Key+"VisualOnly",false))Check(Screen.width>=1280&&Screen.height>=800,"Native UI capture resolution: "+Screen.width+"x"+Screen.height);
                Capture("Demo-Equipment-CTF.png");
                var skin=(GUISkin)typeof(CandyDemoHud).GetField("themed",flags).GetValue(hud);
                Check(skin.font&&skin.label.font&&skin.button.CalcHeight(new GUIContent("Third person"),207)<=30,"Consistent font and short-header text fit");
                typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,2);typeof(CandyDemoHud).GetField("subsection",flags).SetValue(hud,1);
                yield return new WaitForEndOfFrame();Capture("Demo-Modes-CTF.png");
                Check(skin.button.normal.textColor.grayscale>.9f,"Custom mint/lavender buttons do not leak dark text into settings forms");
                typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,0);typeof(CandyDemoHud).GetField("subsection",flags).SetValue(hud,3);
                yield return new WaitForEndOfFrame();Capture("Demo-Settings-CTF.png");
                typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,1);typeof(CandyDemoHud).GetField("subsection",flags).SetValue(hud,3);
                yield return new WaitForEndOfFrame();Capture("Demo-Enemies-CTF.png");
                var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);menu.Show(true,session.ActiveCamera);
                typeof(CastleWorldMenu).GetField("giveEquipmentPage",flags).SetValue(menu,true);
                typeof(CastleWorldMenu).GetMethod("PointGiveEquipmentPage",flags).Invoke(menu,new object[]{session.ActiveCamera,false});
                typeof(CastleWorldMenu).GetMethod("LateUpdate",flags).Invoke(menu,null);
                var targets=(BoxCollider[])typeof(CastleWorldMenu).GetField("targets",flags).GetValue(menu);
                Check(Enumerable.Range(1,8).All(i=>targets[i].enabled),"Actual world-space equipment menu is enabled during CTF");
                menu.Show(false,session.ActiveCamera);
                BotObjectiveContractChecks.Run();Check(BotObjectiveContractChecks.LastPassed,"Bot objective authoring contracts remain valid");
                Check(errors==0,"No runtime errors");
            }
            finally
            {
                session.SurvivorBots.Clear();Object.Destroy(fixture);
                if(oldRoot&&oldRoot!=session.gameObject)oldRoot.SetActive(active);Services.World=original;
            }
        }
        static void Capture(string name)
        {
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());Object.Destroy(image);
        }
        static void CheckFlagRoundtrip(MatchFrame saved,int blockCount)
        {
            var restored=new ModeSimulation(saved.rules);restored.Restore(saved);
            Check(restored.Flags.All(f=>f.carrier!=""),"Restored CTF simulation normalizes uncarried flags");
            var invalid=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(saved));invalid.flags[0].carrier="unregistered-test-carrier";
            Check(!ModeFrameValidation.Frame(invalid,blockCount),"CTF snapshot still rejects an unknown carrier");
            var empty=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(saved));
            empty.players=empty.topTen=empty.secondaryLeaders=Array.Empty<MatchPlayerFrame>();empty.equipment=Array.Empty<EquipmentState>();
            var root=new GameObject("Empty-roster CTF decoder fixture");
            try{var replica=root.AddComponent<MatchModeRuntime>();Check(replica.ReceiveFrameJson(JsonUtility.ToJson(empty))&&replica.Frame.flags.All(f=>f.carrier==null),"Production guest decoder normalizes uncarried CTF flags");}
            finally{Object.Destroy(root);}
        }
    }
}

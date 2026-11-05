using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HudReadabilityChecks
    {
        const string Key="HudReadabilityChecks";
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
        [Serializable] sealed class SnapshotIdentity { public string snapshot_sha256,baseline; }
        [Serializable] sealed class CaptureReceipt
        {
            public string sourceSnapshot,baseline,project,scenePath,mapId,worldType,mode,phase,localRole,inputMode,evidenceKind,capturedUtc,imageSha256;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float fieldOfView;
            public int width,height;
        }
        static string Report=>ProjectPath.Report("HudReadability.txt");
        static int checks,errors;static IMatchRuntime original;
        static HudReadabilityChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("HudReadability.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                try{File.Delete(request);}catch(IOException){return;}Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    errors=checks=0;Application.logMessageReceived+=Log;
                    AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);
                    EditorApplication.delayCall+=()=>{var go=new GameObject("HUD integration checks");Object.DontDestroyOnLoad(go);go.AddComponent<HudReadabilityCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    Time.timeScale=SessionState.GetFloat(Key+"Scale",1);CandyMatchHud.ScoreboardPinned=false;
                    File.AppendAllText(Report,"PASS Original scenes, choices and timing restored.\n");
                    if(Application.isBatchMode)
                    {
                        string report=File.ReadAllText(Report);
                        EditorApplication.Exit(report.Contains("FAIL ")||report.Contains("ERROR ")||!report.Contains("COMPLETE")?1:0);
                    }
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Gameplay HUD Readability")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("Stop Play Mode and recordings first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(EditorApplication.isCompiling){File.WriteAllText(ProjectPath.Report("HudReadability.request"),"run");return;}
            if(Application.isBatchMode && string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            File.WriteAllText(Report,"HUD overhaul: actual Mystery kill/report followed by clearly identified snapshot rendering fixtures. No headset evidence.\n");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetFloat(Key+"Scale",Time.timeScale);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="mode-christmas-town";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool value,string label)
        {File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);checks++;}
        static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static void Call(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        static void Refresh(CastlePlaySession session)
        {session.SetMenu(false);typeof(CandyMatchHud).GetField("sampleAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session.GetComponent<CandyMatchHud>(),0f);Call(session.GetComponent<CandyMatchHud>(),"Update");Call(session.GetComponent<CandyMysteryView>(),"Update");Call(session.GetComponent<CandyDemoHud>(),"PresentHints");Canvas.ForceUpdateCanvases();}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(CheckScene());
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            MatchRuntime.Current=original;CandyMatchHud.ScoreboardPinned=false;
            File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");EditorApplication.ExitPlaymode();
        }
        static IEnumerator Until(Func<bool> ready,string label)
        {float end=Time.realtimeSinceStartup+30;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),label);}
        static Text TextOf(Component view,string name)=>(Text)Field(view,name);
        static Text[] CardTexts(CastlePlaySession session)=>session.GetComponentsInChildren<Canvas>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&(c.name=="Visual - match telemetry"||c.name=="Visual - mystery clues")).SelectMany(c=>c.GetComponentsInChildren<Text>(false)).Where(t=>t.transform.parent.GetComponent<Image>()&&t.transform.parent.parent==t.canvas.transform&&!string.IsNullOrEmpty(t.text)).Distinct().ToArray();
        static Rect CardRect(Text text){var r=(RectTransform)text.transform.parent;return new Rect(r.anchoredPosition-r.sizeDelta/2,r.sizeDelta);}
        static void Fit(CastlePlaySession session,string stage)
        {
            var labels=CardTexts(session);
            foreach(var text in labels)Check(text.preferredHeight<=text.rectTransform.rect.height+1,stage+" text fits "+text.transform.parent.name+" ("+text.preferredHeight+" / "+text.rectTransform.rect.height+")");
            for(int i=0;i<labels.Length;i++)for(int j=i+1;j<labels.Length;j++)Check(!CardRect(labels[i]).Overlaps(CardRect(labels[j])),stage+" panels do not overlap: "+labels[i].transform.parent.name+" / "+labels[j].transform.parent.name);
        }
        static void Render(CastlePlaySession session,string name,int width=1280,int height=800)
        {
            var camera=session.ActiveCamera;
            var previousTarget=camera.targetTexture;var previousRect=camera.rect;
            int previousMask=camera.cullingMask;var previousClear=camera.clearFlags;
            var cameraData=camera.GetComponent<UniversalAdditionalCameraData>();
            bool postProcessing=cameraData&&cameraData.renderPostProcessing;
            var antialiasing=cameraData?cameraData.antialiasing:AntialiasingMode.None;
            var target=new RenderTexture(width,height,24);camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            var canvases=session.GetComponentsInChildren<Canvas>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var pixelPerfect=canvases.Select(c=>c.pixelPerfect).ToArray();
            var uiObjects=canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).Distinct().ToArray();
            var layers=uiObjects.Select(go=>go.layer).ToArray();
            var old=RenderTexture.active;Texture2D pixels=null;
            try
            {
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;canvas.pixelPerfect=true;}
                foreach(var go in uiObjects)go.layer=31;
                Canvas.ForceUpdateCanvases();
                camera.cullingMask=previousMask&~(1<<31);camera.Render();
                camera.clearFlags=CameraClearFlags.Depth;camera.cullingMask=1<<31;
                if(cameraData){cameraData.renderPostProcessing=false;cameraData.antialiasing=AntialiasingMode.None;}
                camera.Render();
                RenderTexture.active=target;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();var png=pixels.EncodeToPNG();File.WriteAllBytes(ProjectPath.Report(name+".png"),png);
                var project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
                var identity=JsonUtility.FromJson<SnapshotIdentity>(File.ReadAllText(Path.Combine(project,"Logs","ScoreboardSnapshot.json")));
                var match=MatchRuntime.Current;
                var self=match?.Frame.players.FirstOrDefault(player=>player.id==match.LocalActorId);
                string kind=name=="Hud-Elimination-ActualAuthority"||name=="Hud-Mystery-ActualReport"
                    ?"Actual Game scene interaction"
                    :name=="Hud-Results-ActualAvatars"?"Results phase fixture over actual participants and avatars":"UI snapshot/projection fixture";
                string hash;
                using(var digest=System.Security.Cryptography.SHA256.Create())
                    hash=BitConverter.ToString(digest.ComputeHash(png)).Replace("-","").ToLowerInvariant();
                var receipt=new CaptureReceipt
                {
                    sourceSnapshot=identity.snapshot_sha256,baseline=identity.baseline,project=project,
                    scenePath=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,mapId=MatchSetup.MapId,
                    worldType=Services.World?.GetType().FullName??"unknown",mode=match?.Rules.mode.ToString()??"unknown",
                    phase=match?.Frame.phase.ToString()??"unknown",localRole=self?.role??"unknown",
                    inputMode=session.IsVR?"VR projection":"Desktop",evidenceKind=kind,capturedUtc=DateTime.UtcNow.ToString("O"),
                    cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation,
                    fieldOfView=camera.fieldOfView,width=width,height=height,imageSha256=hash
                };
                File.WriteAllText(ProjectPath.Report(name+".capture.json"),JsonUtility.ToJson(receipt,true));
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];canvases[i].pixelPerfect=pixelPerfect[i];}
                for(int i=0;i<uiObjects.Length;i++)uiObjects[i].layer=layers[i];
                camera.cullingMask=previousMask;camera.clearFlags=previousClear;
                if(cameraData){cameraData.renderPostProcessing=postProcessing;cameraData.antialiasing=antialiasing;}
                RenderTexture.active=old;camera.targetTexture=previousTarget;camera.rect=previousRect;Object.Destroy(target);if(pixels)Object.Destroy(pixels);Canvas.ForceUpdateCanvases();
            }
        }
        static IEnumerator CheckScene()
        {
            yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes,"Actual Game initializes");
            var session=(CastlePlaySession)Services.Session;original=session.Modes;var mode=session.Modes;
            Check(DemoMenuSections.Sections.SelectMany(tab=>tab).Distinct().Count()==17,
                "Every in-game subtab has one home");
            Check(DemoMenuSections.Sections.SelectMany(tab=>tab).All(name=>!string.IsNullOrEmpty(DemoMenuSections.Caption(name))),
                "Every in-game subtab describes its purpose");
            var meleeMount=typeof(DeadlyJenga.CandyPlayers.CandyWeaponVisual).GetMethod("MeleeMount",BindingFlags.Static|BindingFlags.NonPublic);
            var meleeGrip=typeof(DeadlyJenga.CandyPlayers.CandyHandSkeleton).GetMethod("MeleeGrip",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var kind in new[]{EquipmentKind.MurderSword,EquipmentKind.SawStaff,EquipmentKind.BarbedBat,EquipmentKind.Chainsaw,EquipmentKind.LaserAxe,EquipmentKind.Dagger,EquipmentKind.FlamingMarshmallow})
            foreach(bool left in new[]{false,true})
            {
                object[] mountArgs={kind,left,Vector3.zero,Quaternion.identity};
                bool mounted=(bool)meleeMount.Invoke(null,mountArgs);
                var palm=(Vector3)mountArgs[2];
                var mount=(Quaternion)mountArgs[3];
                var axis=mount*(kind==EquipmentKind.Chainsaw?Vector3.up:Vector3.forward);
                var skeleton=(DeadlyJenga.CandyPlayers.CandyHandSkeleton)meleeGrip.Invoke(null,new object[]{left,kind});
                Check(mounted&&Mathf.Abs(Vector3.Dot(axis,Vector3.right))>.99f&&
                    Vector3.Distance(skeleton.Joint(6),palm)<.06f,
                    kind+" handle crosses the palm within the curled fingers ("+(left?"left":"right")+")");
            }
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            typeof(CastlePlaySession).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{true});session.SetMenu(false);session.Survivor().InputSuspended=true;
            var rules=ModeSimulation.Defaults(GameModeKind.MurderMystery);rules.countdownSeconds=0;rules.automaticDisasters=false;rules.mystery.attackGraceSeconds=0;rules.mystery.reportDelaySeconds=0;
            MatchSetup.BotTarget=7;mode.Configure(rules);yield return new WaitForSeconds(1);
            foreach(var bot in session.Bots().Bots){bot.enabled=false;bot.Motor.InputSuspended=true;}
            mode.StartRound();yield return new WaitForSeconds(1);mode.SetDemoRole(MysteryRole.Murderer);
            var candidates=mode.Frame.players.Where(p=>p.bot&&p.alive&&JsonUtility.FromJson<MysteryPrivateFrame>(mode.CaptureSecret(p.id)).role==MysteryRole.Innocent).ToArray();Check(candidates.Length>0,"Real Mystery has an innocent target");
            var source=mode.FindActor(mode.LocalActorId);var victim=mode.FindActor(candidates[0].id);var point=Services.World.SafeSpawnPoint()+Vector3.up*.1f;
            int offset=0;foreach(var bot in session.Bots().Bots)bot.Motor.Place(point+Vector3.right*(10+offset++*2),Quaternion.identity);
            session.Survivor().InputSuspended=true;session.Player.Place(point,Quaternion.identity);victim.gameObject.GetComponent<IPlayableCharacter>().Place(point+Vector3.forward*.55f,Quaternion.identity);Physics.SyncTransforms();mode.SetSpecialAttack(true);
            Check(mode.RequestPunch(source.gameObject,source.ViewPose.position,(victim.ViewPose.position-source.ViewPose.position).normalized,3),"Real murderer special punch accepted");
            yield return Until(()=>mode.Clues.bodies.Any(b=>b.id==victim.ActorId),"Real punch creates a body");
            var body=mode.Clues.bodies.First(b=>b.id==victim.ActorId);session.Player.Place(body.position+Vector3.back*.6f,Quaternion.identity);mode.ReportBody(body.id);
            yield return Until(()=>mode.Clues.bodies.Any(b=>b.id==victim.ActorId&&b.reported),"Real report command publishes the body");
            mode.enabled=false;Time.timeScale=0;Refresh(session);
            var mystery=session.GetComponent<CandyMysteryView>();var hud=session.GetComponent<CandyMatchHud>();
            Check(TextOf(mystery,"reports").text.Contains(body.name),"Actual report appears in the persistent case list");Check(TextOf(mystery,"notice").text.Contains("REPORTED"),"Actual report generates a visible alert");
            Fit(session,"Actual reported body");Render(session,"Hud-Mystery-ActualReport");
            var fixture=new Fixture(mode);fixture.Frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(mode.Frame));fixture.Personal=new MysteryPrivateFrame{role=MysteryRole.Innocent};MatchRuntime.Current=fixture;
            var hidden=new MysteryBodyFrame{id="hidden",name="UnseenVictim",position=session.ActiveCamera.transform.position+Vector3.forward*60};fixture.Frame.mystery=new MysteryPublicFrame{bodies=new[]{hidden}};fixture.Frame.elapsed=0;Refresh(session);
            Check(!TextOf(mystery,"reports").text.Contains(hidden.name)&&!CardTexts(session).Any(t=>t.text.Contains("UNREPORTED BODY")),"Distant unreported body never leaks into HUD");
            hidden.reported=true;hidden.finder=fixture.LocalActorId;hidden.position=session.ActiveCamera.transform.position-session.ActiveCamera.transform.forward*25+Vector3.up*6;fixture.Frame.elapsed=1;Refresh(session);
            Check(TextOf(mystery,"reports").text.Contains("BEHIND")&&TextOf(mystery,"reports").text.Contains("ABOVE"),"Case list explains offscreen direction and elevation");
            Check(CardTexts(session).Any(t=>t.transform.parent.name=="Objective waypoint"&&t.text.Contains("BEHIND")),"Reported body behind the player retains a directional marker");
            hidden.reportedAge=600;fixture.Frame.elapsed=601;Refresh(session);Check(TextOf(mystery,"reports").text.Contains(hidden.name),"Old reports remain readable instead of fading out");
            fixture.Frame.mystery.bodies=new[]{hidden,new MysteryBodyFrame{id="second",name="SecondVictim",reported=true,finder=fixture.LocalActorId,reportedAge=8,position=hidden.position+Vector3.left*5},new MysteryBodyFrame{id="third",name="ThirdVictim",reported=true,finder=fixture.LocalActorId,reportedAge=2,position=hidden.position+Vector3.right*5}};
            fixture.Personal.disasterChoices=new[]{"Meteor Shower","Earthquake"};fixture.Personal.choiceRemaining=12;Refresh(session);Fit(session,"Multiple reports and choices");Render(session,"Hud-Mystery-MultipleReports");
            CandyMysteryView.ReportPage=1;Refresh(session);Check(TextOf(mystery,"reports").text.Contains(hidden.name),"Older body reports can be paged back into view");CandyMysteryView.ReportPage=0;Refresh(session);
            Check(CandyHudContent.Suspects(new MysteryBodyFrame{suspected=new[]{Color.red}}).Contains("red")&&CandyHudContent.Suspects(new MysteryBodyFrame{suspected=new[]{Color.red}}).Contains("not proof"),"Suspicions use color names and explain uncertainty");
            Check(!CandyHudContent.BodyKnown(new MysteryBodyFrame(),false)&&CandyHudContent.BodyKnown(new MysteryBodyFrame{reported=true},false)&&!CandyHudContent.BodyKnown(new MysteryBodyFrame{reported=true,lost=true},true),"Body visibility respects report, proximity and loss");
            foreach(MysteryRole role in new[]{MysteryRole.Innocent,MysteryRole.Sheriff,MysteryRole.Murderer}){fixture.Personal.role=role;Refresh(session);Fit(session,"Role "+role);}
            fixture.Personal.role=MysteryRole.Innocent;fixture.Personal.disasterChoices=Array.Empty<string>();
            fixture.Frame.rules=ModeSimulation.Defaults(GameModeKind.CaptureFlag);fixture.Frame.metricLabel="Captures";fixture.Frame.remaining=-1;fixture.Frame.players=Enumerable.Range(1,100).Select(i=>new MatchPlayerFrame{id=i==75?fixture.LocalActorId:"player-"+i,name="Explorer "+i,rank=i,metric=101-i,alive=true,team=i==75?0:1,position=Vector3.one*1000,viewPosition=Vector3.one*1000}).ToArray();
            fixture.Frame.topTen=fixture.Frame.players.Take(10).ToArray();foreach(var player in fixture.Frame.players.Skip(10))player.rank=0;fixture.Frame.teams=new[]{new MatchTeamFrame{id=0,name="Violet",captures=2},new MatchTeamFrame{id=1,name="Mint",captures=1}};fixture.Frame.mystery=new MysteryPublicFrame();
            var own=fixture.Frame.players[74];var flags=new[]{new FlagFrame{team=0,home=point,position=point},new FlagFrame{team=1,home=point+Vector3.forward*35,position=point+Vector3.forward*35}};fixture.Frame.flags=flags;Refresh(session);
            Check(!TextOf(hud,"board").transform.parent.gameObject.activeSelf,"Full standings stay out of the default view");
            CandyMatchHud.ScoreboardPinned=true;Refresh(session);
            Check(TextOf(hud,"board").text.Contains("YOUR POSITION")&&TextOf(hud,"board").text.Contains("Explorer 75")&&TextOf(hud,"board").text.Contains("YOU · 75."),"Local rank is reconstructed and pinned when the wire rank is zero outside the top ten");
            CandyMatchHud.ScoreboardPinned=false;Refresh(session);
            Check(CardTexts(session).Any(t=>t.text.Contains("STEAL ENEMY FLAG")),"Enemy flag has a distant location marker at base");Fit(session,"CTF flags at home");Render(session,"Hud-CTF-AtBase");
            flags[0].atHome=false;flags[0].carrier="player-1";flags[0].position=point+Vector3.left*15;flags[1].atHome=false;flags[1].carrier=fixture.LocalActorId;Refresh(session);
            Check(CandyHudContent.FlagStatus(fixture.Frame,flags[0],own.id).Contains("CARRIED BY")&&CandyHudContent.FlagStatus(fixture.Frame,flags[1],own.id).Contains("YOU ARE CARRYING"),"Carrier identity and local carrying state are explicit");
            Check(CardTexts(session).Any(t=>t.text.Contains("FLAG NOT HOME")),"Carrier sees why delivery cannot score yet");Fit(session,"CTF carried flags");Render(session,"Hud-CTF-Carrying");
            flags[0].carrier=null;Check(CandyHudContent.FlagStatus(fixture.Frame,flags[0],own.id).Contains("RETURN"),"Dropped friendly flag says return");flags[1].carrier=null;Check(CandyHudContent.FlagStatus(fixture.Frame,flags[1],own.id).Contains("TAKE"),"Dropped enemy flag says take");
            flags[1].carrier="player-2";fixture.Frame.players[1].team=0;Check(CandyHudContent.FlagTask(fixture.Frame,flags[1],own).Contains("ESCORT"),"Friendly carrier gets an escort objective");
            CandyMatchHud.ScoreboardPinned=true;Refresh(session);Check(TextOf(hud,"board").text.Contains("PAGE 1 / 10")&&TextOf(hud,"board").text.Contains("Explorer 75"),"Expanded 100-player standings paginate and pin the local row");Fit(session,"Expanded standings");Render(session,"Hud-Standings-100Players");
            CandyMatchHud.ScoreboardPinned=false;fixture.Frame.teams=Enumerable.Range(0,8).Select(i=>new MatchTeamFrame{id=i,name="LongTeam"+i,captures=i}).ToArray();fixture.Frame.flags=Enumerable.Range(0,8).Select(i=>new FlagFrame{team=i,home=point+Vector3.forward*(10+i*3),position=point+Vector3.forward*(10+i*3)}).ToArray();Refresh(session);Fit(session,"Eight-team CTF");
            var initialFocus=CandyHudContent.FocusedFlag(fixture.Frame,own,0);CandyMatchHud.FlagFocusIndex=1;Refresh(session);Check(CandyHudContent.FocusedFlag(fixture.Frame,own,CandyMatchHud.FlagFocusIndex)!=initialFocus,"Multi-team CTF cycles the tracked enemy flag");
            CandyMatchHud.ScoreboardPinned=true;Refresh(session);Fit(session,"Eight-team full standings");Check(TextOf(hud,"board").text.Contains("LongTeam7"),"All eight teams remain available in full standings");Render(session,"Hud-CTF-EightTeams");
            fixture.Frame.teams=fixture.Frame.teams.Take(2).ToArray();fixture.Frame.flags=Array.Empty<FlagFrame>();
            CandyMatchHud.ScoreboardPinned=false;fixture.Frame.rules=ModeSimulation.Defaults(GameModeKind.MurderMystery);Refresh(session);Check(!TextOf(hud,"board").text.Contains("kills")&&!TextOf(hud,"board").text.Contains("100.0"),"Mystery roster does not expose kill scores or hidden roles");
            var worldMenu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);Check(worldMenu,"Actual VR menu is available");worldMenu.Show(true,session.ActiveCamera);fixture.authority=false;
            typeof(CastleWorldMenu).GetMethod("PointModePage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(worldMenu,new object[]{session.ActiveCamera,false});
            var targets=(BoxCollider[])Field(worldMenu,"targets");Check(targets[10].enabled,"Non-host can open HUD controls from the VR rules page");
            typeof(CastleWorldMenu).GetMethod("PointHudPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(worldMenu,new object[]{session.ActiveCamera,false});Check(targets[1].enabled&&targets[2].enabled&&!targets[4].enabled,"Non-host VR standings and report paging are enabled while scene edits stay locked");fixture.authority=true;worldMenu.Show(false,session.ActiveCamera);
            foreach(var dimensions in new[]{new Vector2Int(1024,768),new Vector2Int(1920,1080),new Vector2Int(2560,1080)})Render(session,"Hud-Layout-"+dimensions.x+"x"+dimensions.y,dimensions.x,dimensions.y);
            foreach(var canvas in new[]{(Canvas)Field(hud,"canvas"),(Canvas)Field(mystery,"canvas")})
            {CandyHudLayout.Configure(canvas,session.transform,session.ActiveCamera,true);Check(canvas.renderMode==RenderMode.WorldSpace&&canvas.GetComponent<RectTransform>().sizeDelta==CandyHudLayout.ReferenceSize,"Headset canvas uses the shared projection (geometry only)");CandyHudLayout.Configure(canvas,session.transform,session.ActiveCamera,false);}
            yield return ScoreboardAndFeed(session, fixture, hud, mode);
            var hints=session.GetComponent<CandyDemoHud>();
            hints.hintMode=CandyDemoHud.HintMode.Always;
            session.Survivor().controlMode=FlyingHand.ControlMode.VR;
            session.Survivor().RefreshMode();
            typeof(CandyDemoHud).GetField("nextHintSample",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(hints,0f);
            Call(hints,"PresentHints");
            var hintIcons=(RawImage[])Field(hints,"hintIcons");
            Check(hintIcons[0].texture&&hintIcons[0].texture.name=="HUD controller diagram","VR movement prompt displays a highlighted controller diagram");
            Check(hints.ControlHintCanvas.renderMode==RenderMode.WorldSpace,"VR control hints use a world-space canvas");
            hints.ControlHintCanvas.GetComponent<CanvasGroup>().alpha=1;
            hints.ControlHintCanvas.enabled=true;
            Render(session,"Hud-VR-ControllerPrompts");
            session.Survivor().controlMode=FlyingHand.ControlMode.Desktop;
            session.Survivor().RefreshMode();
            Check(CandyHudContent.Safe("<b>Injected</b>\nName").IndexOf('<')<0,"Player labels cannot inject rich text");Check(errors==0,"No runtime errors");
        }
        static IEnumerator ScoreboardAndFeed(CastlePlaySession session, Fixture fixture, CandyMatchHud hud, MatchModeRuntime mode)
        {
            fixture.Frame.rules=ModeSimulation.Defaults(GameModeKind.CaptureFlag);
            fixture.Frame.phase=RoundPhase.Playing;
            fixture.Frame.eliminations=Array.Empty<EliminationFrame>();
            Refresh(session);
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.F2));
                InputSystem.Update();
                Refresh(session);
                Check(CandyMatchHud.Expanded&&TextOf(hud,"board").text.Contains("captures /"),"Held F2 shows mode stats and local rank");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                InputSystem.Update();
                Refresh(session);
                Check(!CandyMatchHud.Expanded,"Release closes held standings immediately");
            }
            finally { InputSystem.RemoveDevice(keyboard); }

            yield return CheckControllerStandings();
            var sim=new ModeSimulation(ModeSimulation.Defaults(GameModeKind.Demo));
            sim.Register(fixture.LocalActorId,Color.cyan,false,true);
            sim.Register("attacker",Color.magenta,true,false);
            sim.Kill(fixture.LocalActorId,"attacker");
            sim.Kill("attacker",null);
            var copied=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(sim.Snapshot()));
            Check(ModeFrameValidation.Frame(copied,0)&&copied.eliminations.Length==2&&
                copied.eliminations[0].killer=="attacker"&&copied.eliminations[0].victim==fixture.LocalActorId&&
                string.IsNullOrEmpty(copied.eliminations[1].killer),"Serialized host snapshot preserves attributed and environmental eliminations");
            var successor=new ModeSimulation(copied.rules);
            successor.Restore(copied);
            successor.Kill("attacker",fixture.LocalActorId);
            Check(successor.Snapshot().eliminations.Last().sequence==3,"Restored authority continues the elimination sequence");
            for(int i=0;i<20;i++)sim.Kill("attacker",null);
            Check(sim.Snapshot().eliminations.Length==EliminationFrame.Capacity,"Host retains a bounded recent feed");
            string oldEpoch=sim.Snapshot().eliminationEpoch;
            sim.StartRound();
            Check(sim.Snapshot().eliminations.Length==0 && sim.Snapshot().eliminationEpoch!=oldEpoch,"New round clears history and changes its token");
            sim.Kill("attacker",fixture.LocalActorId);
            Check(sim.Snapshot().eliminations[0].sequence==1,"New round starts a fresh token with sequence one");
            var mystery=new ModeSimulation(ModeSimulation.Defaults(GameModeKind.MurderMystery));
            mystery.Register("secret",Color.red,false,false);
            mystery.Register("victim",Color.green,false,false);
            mystery.StartRound();
            for(int i=0;i<10;i++)mystery.Tick(.5f);
            Check(mystery.Kill("victim","secret")!=EliminationOutcome.None&&mystery.Snapshot().eliminations.Length==0,"Host omits accepted Mystery eliminations from the public feed");

            fixture.Frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(fixture.Frame));
            fixture.Frame.eliminationEpoch=Guid.NewGuid().ToString("N");
            fixture.Frame.eliminations=new[]{new EliminationFrame{sequence=1,victim=fixture.LocalActorId,killer=fixture.Frame.players[0].id},
                new EliminationFrame{sequence=2,victim=fixture.Frame.players[1].id}};
            Refresh(session);
            var feed=CardTexts(session).Where(label=>label.transform.parent.name=="Elimination feed").ToArray();
            Check(feed.Length==2&&feed.Any(label=>label.text.Contains("›  YOU"))&&
                feed.Any(label=>label.text.Contains("was eliminated")),"Visible feed shows an exact pair and an environmental elimination");
            Render(session,"Hud-Elimination-Feed");
            fixture.Frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(fixture.Frame));
            fixture.Frame.eliminationEpoch=Guid.NewGuid().ToString("N");
            fixture.Frame.eliminations=new[]{new EliminationFrame{sequence=1,victim=fixture.LocalActorId,killer=fixture.Frame.players[2].id}};
            Refresh(session);
            feed=CardTexts(session).Where(label=>label.transform.parent.name=="Elimination feed").ToArray();
            Check(feed.Length==1&&feed[0].text.Contains("Explorer 3")&&feed[0].text.Contains("YOU"),"Consumer accepts first kill after a missed empty reset snapshot");
            fixture.Frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(fixture.Frame));
            Refresh(session);
            Check(CardTexts(session).Count(label=>label.transform.parent.name=="Elimination feed")==1,"Repeated authoritative sequence is displayed once");
            float end=Time.unscaledTime+5.2f;
            while(Time.unscaledTime<end)yield return null;
            Check(!CardTexts(session).Any(label=>label.transform.parent.name=="Elimination feed"),"Feed expires after its fade window");

            fixture.Frame.phase=RoundPhase.Results;
            fixture.Frame.revision++;
            var results=session.GetComponent<CandyResultsHud>();
            Call(results,"Update");
            var canvas=(Canvas)Field(results,"canvas");
            Check(canvas.enabled&&TextOf(results,"summary").text.Contains("YOU #75")&&TextOf(results,"paging").text=="Page 1 / 13",
                "100-player results show 13 pages and identify local rank off-page");
            var buttons=(Image[])Field(results,"buttons");
            Check(buttons[0].rectTransform.anchoredPosition.y<buttons[2].rectTransform.anchoredPosition.y&&
                buttons[1].GetComponentInChildren<Text>().text=="RETURN TO LOBBY","Results reserve a footer for Stay and Return to Lobby");
            var act=typeof(CandyResultsHud).GetMethod("Act",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=0;i<9;i++)act.Invoke(results,new object[]{3});
            yield return null;
            Call(results,"Update");
            Check(canvas.GetComponentsInChildren<Text>().Any(label=>label.text.Contains("Explorer 75")&&label.text.Contains("YOU")),
                "Paging to the local row highlights it");
            Check(canvas.GetComponentsInChildren<RawImage>().Any(image=>image.texture&&image.texture.name=="Head portrait"),
                "Results show a rendered portrait from the real local avatar");
            Render(session,"Hud-Results-100Players");
            fixture.Frame.rules=ModeSimulation.Defaults(GameModeKind.Infection);
            fixture.Frame.revision++;
            Call(results,"Update");
            Check(TextOf(results,"secondaryHeading").text=="SURVIVORS INFECTED","Infection has both leaderboard headings");
            foreach(var label in canvas.GetComponentsInChildren<Text>())
                Check(label.preferredHeight<=label.rectTransform.rect.height+1,"Results text fits "+label.name);
            Render(session,"Hud-Results-Infection");
            fixture.Frame.phase=RoundPhase.Playing;
            fixture.Frame.eliminations=Array.Empty<EliminationFrame>();
            Call(results,"Update");
            Refresh(session);
            yield return ActualPublicElimination(session,mode);
            yield return EliminationTransport(copied);
        }
        static IEnumerator ActualPublicElimination(CastlePlaySession session, MatchModeRuntime mode)
        {
            var prior=MatchRuntime.Current;
            bool enabled=mode.enabled;
            float scale=Time.timeScale;
            try
            {
                MatchRuntime.Current=mode;
                mode.enabled=true;
                Time.timeScale=1;
                var rules=ModeSimulation.Defaults(GameModeKind.KingOfHill);
                rules.countdownSeconds=0;rules.automaticDisasters=false;
                mode.Configure(rules);mode.StartRound();
                yield return Until(()=>mode.Frame.phase==RoundPhase.Playing,"Actual public round enters play");
                session.SetMenu(false);
                session.Survivor().InputSuspended=true;
                var attacker=mode.FindActor(mode.LocalActorId);
                var row=mode.Frame.players.FirstOrDefault(player=>player.bot&&player.alive&&mode.FindActor(player.id)!=null);
                Check(attacker!=null&&row!=null,"Actual public round has a local attacker and a bot victim");
                var victim=mode.FindActor(row.id);
                Refresh(session);
                victim.gameObject.GetComponent<IDamageable>().Eliminate(attacker.gameObject);
                yield return Until(()=>mode.Frame.eliminations.Any(entry=>entry.victim==row.id&&entry.killer==mode.LocalActorId),
                    "Real character elimination publishes the accepted killer/victim pair");
                Refresh(session);
                yield return null;
                Refresh(session);
                Check(CardTexts(session).Any(label=>label.transform.parent.name=="Elimination feed"&&
                    label.text.Contains("YOU")&&label.text.Contains("›")),"Actual authoritative pair reaches the visible feed");
                Render(session,"Hud-Elimination-ActualAuthority");
                var display=new Fixture(mode)
                {
                    Frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(mode.Frame)),
                    Personal=new MysteryPrivateFrame()
                };
                display.Frame.phase=RoundPhase.Results;
                display.Frame.winner="";
                MatchRuntime.Current=display;
                var results=session.GetComponent<CandyResultsHud>();
                Call(results,"Update");
                for(int i=0;i<8;i++)yield return null;
                Call(results,"Update");
                Render(session,"Hud-Results-ActualAvatars");
                File.AppendAllText(Report,"RUNTIME NOTE Public kill used the actual character IDamageable entry point and authoritative mode. The results capture is a phase-only display fixture over actual participant identities/avatars.\n");
                Call(results,"OnDisable");
            }
            finally
            {
                MatchRuntime.Current=prior;
                mode.enabled=enabled;
                Time.timeScale=scale;
                session.SetMenu(false);
            }
        }

        static IEnumerator EliminationTransport(MatchFrame accepted)
        {
            var config=Fusion.NetworkProjectConfig.Global;
            var oldPeerMode=config.PeerMode;
            var oldPhysics=Physics.simulationMode;
            float oldScale=Time.timeScale;
            Fusion.NetworkRunner host=null, guest=null;
            try
            {
                config.PeerMode=Fusion.NetworkProjectConfig.PeerModes.Multiple;
                Time.timeScale=1;
                host=new GameObject("Scoreboard transport host").AddComponent<Fusion.NetworkRunner>();
                guest=new GameObject("Scoreboard transport guest").AddComponent<Fusion.NetworkRunner>();
                var app=Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
                app.FixedRegion="us";
                app.AppVersion=(app.AppVersion??"")+"/scoreboard-check";
                string room="scoreboard-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var start=host.StartGame(new Fusion.StartGameArgs
                {
                    GameMode=Fusion.GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,
                    Scene=new Fusion.NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<MayhemTransportScenes>()
                });
                yield return TransportWait(()=>start.IsCompleted,"Photon scoreboard host startup");
                Check(!start.IsFaulted&&!start.IsCanceled&&start.Result.Ok,"Independent Photon host connects");
                MatchFrame received=null;
                string error=null;
                var callbacks=new Fusion.NetworkDelegates();
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out _,out _);
                    if(channel!=0x43415354||kind!=12)return;
                    try
                    {
                        received=JsonUtility.FromJson<MatchFrame>(System.Text.Encoding.UTF8.GetString(
                            CastleFusionWire.Decompress(data.ToArray())));
                        if(!ModeFrameValidation.Frame(received,0))error="Rejected by production mode validator";
                    }
                    catch(Exception failure){error=failure.ToString();}
                };
                guest.AddCallbacks(callbacks);
                var join=guest.StartGame(new Fusion.StartGameArgs
                {
                    GameMode=Fusion.GameMode.Client,SessionName=room,EnableClientSessionCreation=false,
                    CustomPhotonAppSettings=app,Scene=new Fusion.NetworkSceneInfo(),
                    SceneManager=guest.gameObject.AddComponent<MayhemTransportScenes>()
                });
                yield return TransportWait(()=>join.IsCompleted,"Photon scoreboard guest startup");
                Check(!join.IsFaulted&&!join.IsCanceled&&join.Result.Ok,"Second independent Photon peer joins");
                var packet=CastleFusionWire.Compress(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(accepted)));
                host.SendReliableDataToPlayer(guest.LocalPlayer,Fusion.Sockets.ReliableKey.FromInts(0x43415354,12,1,0),packet);
                yield return TransportWait(()=>received!=null||error!=null,"Production match packet crosses real Photon channel");
                Check(error==null&&received.eliminationEpoch==accepted.eliminationEpoch&&received.eliminations.Length==2&&
                    received.eliminations[0].killer==accepted.eliminations[0].killer&&
                    received.eliminations[0].victim==accepted.eliminations[0].victim&&
                    string.IsNullOrEmpty(received.eliminations[1].killer),"Two-peer transport preserves exact elimination identities and environmental deaths");
                File.AppendAllText(Report,"TRANSPORT NOTE Two real Photon peers, production compression/channel/validation. The guest is a transport fixture without a gameplay avatar.\n");
                var shutdown=guest.Shutdown();
                yield return TransportWait(()=>shutdown.IsCompleted,"Scoreboard guest shutdown");
                if(guest)Object.Destroy(guest.gameObject);guest=null;
                shutdown=host.Shutdown();
                yield return TransportWait(()=>shutdown.IsCompleted,"Scoreboard host shutdown");
                if(host)Object.Destroy(host.gameObject);host=null;
            }
            finally
            {
                if(guest){_=guest.Shutdown();Object.Destroy(guest.gameObject);}
                if(host){_=host.Shutdown();Object.Destroy(host.gameObject);}
                config.PeerMode=oldPeerMode;Physics.simulationMode=oldPhysics;Time.timeScale=oldScale;
            }
        }

        static IEnumerator TransportWait(Func<bool> condition,string label)
        {
            float deadline=Time.realtimeSinceStartup+45;
            while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(condition(),label);
        }

        static IEnumerator CheckControllerStandings()
        {
            const string preference="CandyBlaster.LeftHanded";
            bool hadPreference=PlayerPrefs.HasKey(preference);
            int previous=PlayerPrefs.GetInt(preference,0);
            var originalSettings=InputSystem.settings;
            var originalFlags=originalSettings.hideFlags;
            originalSettings.hideFlags=HideFlags.DontUnloadUnusedAsset;
            var fixtureSettings=Object.Instantiate(originalSettings);
            fixtureSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            fixtureSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=fixtureSettings;
            InputSystem.RegisterLayout(@"{""name"":""StandingsCheckController"",""extend"":""XRController"",""controls"":[{""name"":""primary2DAxisClick"",""layout"":""Button""},{""name"":""gripPressed"",""layout"":""Button""}]}");
            var left=(UnityEngine.InputSystem.XR.XRController)InputSystem.AddDevice("StandingsCheckController");
            var right=(UnityEngine.InputSystem.XR.XRController)InputSystem.AddDevice("StandingsCheckController");
            InputSystem.SetDeviceUsage(left,UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right,UnityEngine.InputSystem.CommonUsages.RightHand);
            var read=typeof(CandyMatchHud).GetMethod("VrStandingsHeld",BindingFlags.Static|BindingFlags.NonPublic);
            try
            {
                PlayerPrefs.SetInt(preference,0);
                ControllerState(left,true,true);
                ControllerState(right,true,false);
                yield return null;
                File.AppendAllText(Report,"CONTROLLER STATE update="+InputState.currentUpdateType+
                    " leftEnabled="+left.enabled+" leftTracked="+left.isTracked.isPressed+
                    " leftClick="+left.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primary2DAxisClick").isPressed+
                    " rightEnabled="+right.enabled+" rightTracked="+right.isTracked.isPressed+"\n");
                object[] control={""};
                Check((bool)read.Invoke(null,control)&&(string)control[0]=="LEFT STICK","Right-dominant player holds offhand click, preserving sprint click");
                PlayerPrefs.SetInt(preference,1);
                Check(!(bool)read.Invoke(null,control),"Dominant left click does not show left-handed standings");
                ControllerState(right,true,true);
                yield return null;
                Check((bool)read.Invoke(null,control)&&(string)control[0]=="RIGHT STICK","Left-dominant player holds offhand right click");
                ControllerState(right,true,true,true);
                yield return null;
                Check(!(bool)read.Invoke(null,control),"Offhand grip reserves the stick chord for emotes");
                ControllerState(right,true,true);
                ControllerState(left,true,true,true);
                yield return null;
                Check(!(bool)read.Invoke(null,control),"Either controller emote chord suppresses standings");
                ControllerState(left,false,true);
                yield return null;
                Check(!(bool)read.Invoke(null,control)&&(string)control[0]=="","Single controller keeps its hybrid mode click");
            }
            finally
            {
                InputSystem.RemoveDevice(left);InputSystem.RemoveDevice(right);
                InputSystem.RemoveLayout("StandingsCheckController");
                InputSystem.settings=originalSettings;
                originalSettings.hideFlags=originalFlags;
                Object.Destroy(fixtureSettings);
                if(hadPreference)PlayerPrefs.SetInt(preference,previous);else PlayerPrefs.DeleteKey(preference);
            }
        }

        static void ControllerState(UnityEngine.InputSystem.XR.XRController controller,bool tracked,bool clicked,bool gripping=false)
        {
            using(StateEvent.From(controller,out var input))
            {
                controller.isTracked.WriteValueIntoEvent(tracked?1f:0f,input);
                controller.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primary2DAxisClick")
                    .WriteValueIntoEvent(clicked?1f:0f,input);
                controller.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("gripPressed")
                    .WriteValueIntoEvent(gripping?1f:0f,input);
                InputSystem.QueueEvent(input);
            }
        }

        sealed class Fixture:IMatchRuntime,IMysteryControls
        {
            readonly IMatchRuntime source;public Fixture(IMatchRuntime source){this.source=source;}
            public MatchFrame Frame {get;set;}public ModeRules Rules=>Frame.rules;public string LocalActorId=>source.LocalActorId;public bool authority=true;public bool Authority=>authority;
            public bool Active=>Rules.mode==GameModeKind.MurderMystery;public MysteryPrivateFrame Personal {get;set;}public MysteryPublicFrame Clues=>Frame.mystery;public IReadOnlyList<MysteryTrailFrame> Trails=>Array.Empty<MysteryTrailFrame>();
            public IModeActor FindActor(string id)=>source.FindActor(id);public Transform FindParticipantRoot(string id)=>source.FindParticipantRoot(id);public ModeRules RulesFor(GameObject actor)=>Rules;public bool CanClimb(GameObject actor,IClimbable climbable)=>true;
            public bool RequestPunch(GameObject source,Vector3 origin,Vector3 direction,float speed)=>false;public void Configure(ModeRules rules){}public void StartRound(){}public void ReturnToLobby(){}public void NextSpectator(int step){}public void ActivateHandPower(float seconds){}
            public void SetSpecialAttack(bool enabled){}public void ReportBody(string id){}public void Revive(string id,bool held){}public void Suspect(string id,int sequence){}public void ChooseDisaster(string id){}public void SetDemoRole(MysteryRole role){}public void ConfigureMystery(MysterySettings settings){}
        }
    }
    public sealed class HudReadabilityCheckDriver:MonoBehaviour{}
}

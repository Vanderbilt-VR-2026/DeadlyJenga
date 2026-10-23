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
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HudReadabilityChecks
    {
        const string Key="HudReadabilityChecks";
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
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
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Gameplay HUD Readability")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("Stop Play Mode and recordings first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(EditorApplication.isCompiling){File.WriteAllText(ProjectPath.Report("HudReadability.request"),"run");return;}
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
        {typeof(CandyMatchHud).GetField("sampleAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session.GetComponent<CandyMatchHud>(),0f);Call(session.GetComponent<CandyMatchHud>(),"Update");Call(session.GetComponent<CandyMysteryView>(),"Update");Call(session.GetComponent<CandyDemoHud>(),"PresentHints");Canvas.ForceUpdateCanvases();}
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
            var go=new GameObject("HUD capture camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(session.ActiveCamera);camera.transform.SetPositionAndRotation(session.ActiveCamera.transform.position,session.ActiveCamera.transform.rotation);camera.enabled=false;
            var target=new RenderTexture(width,height,24);camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            var canvases=session.GetComponentsInChildren<Canvas>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var old=RenderTexture.active;Texture2D pixels=null;
            try
            {
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
                RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(target);if(pixels)Object.Destroy(pixels);Object.Destroy(go);Canvas.ForceUpdateCanvases();
            }
        }
        static IEnumerator CheckScene()
        {
            yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes,"Actual Game initializes");
            var session=(CastlePlaySession)Services.Session;original=session.Modes;var mode=session.Modes;
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
            Check(TextOf(hud,"board").text.Contains("YOUR POSITION")&&TextOf(hud,"board").text.Contains("Explorer 75")&&TextOf(hud,"board").text.Contains("YOU · 75."),"Local rank is reconstructed and pinned when the wire rank is zero outside the top ten");
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
            Check(CandyHudContent.Safe("<b>Injected</b>\nName").IndexOf('<')<0,"Player labels cannot inject rich text");Check(errors==0,"No runtime errors");
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

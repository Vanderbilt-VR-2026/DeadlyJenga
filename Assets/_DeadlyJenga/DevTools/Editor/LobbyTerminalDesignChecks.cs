using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class LobbyTerminalDesignChecks
    {
        const string Key="CandyTerminalDesign";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static IEnumerator run;static int checks,errors,editorErrors;static bool failed;
        static string Report=>ProjectPath.Report("CandyTerminalDesign.txt");
        [Serializable] sealed class Saved {public SceneSetup[] scenes;public string choices;public bool fromLobby,local,background;public float volume;}
        static LobbyTerminalDesignChecks()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;
        }
        static void Tick()
        {
            if(run==null&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string request=ProjectPath.Report("CandyTerminalDesign.request");
                if(File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString())
                {File.Delete(request);Run();return;}
                string quit=ProjectPath.Report("CandyTerminalDesign.quit");
                if(File.Exists(quit)&&File.ReadAllText(quit).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()&&
                    !Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                {File.Delete(quit);EditorApplication.Exit(0);return;}
            }
            if(run==null)return;
            try{if(run.MoveNext())return;}catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");}
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        public static void CaptureModes()
        {SessionState.SetBool(Key+"ModesOnly",true);Run();}
        public static void CaptureCabinets()
        {SessionState.SetBool(Key+"CabinetsOnly",true);Run();}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            string owner=ProjectPath.Report("CandyTerminalDesign.owner");
            if(!File.Exists(owner)||!string.Equals(root,Path.GetFullPath(File.ReadAllText(owner).Trim()),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("An exact-project owner marker is required.");
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Editor is busy.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes left untouched.");
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validator owns this editor.");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession,background=Application.runInBackground,volume=AudioListener.volume}));
            SessionState.SetBool(Key,true);checks=errors=editorErrors=0;failed=false;
            File.WriteAllText(Report,"Actual Unity lobby / Game solo validation. Project: "+root+"\nConnected and guest screens additionally use explicitly labeled presentation stand-ins; no networking/headset claim.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                checks=errors=editorErrors=0;failed=false;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();
                SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;run=Validate();
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(saved.choices));MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                Application.runInBackground=saved.background;AudioListener.volume=saved.volume;
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                ValidationRunGate.Release(Key);
                File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+" editorServiceErrors="+editorErrors+" failed="+failed+". Desktop Editor; headset not tested.\n");
            }
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            bool editorService=(message.StartsWith("[Package Manager Window] Error while getting auth code:")&&trace.Contains("UnityEditor.AsyncHTTPClient"))||
                (trace.Contains("UnityEditor.Search.SearchDatabase")&&trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"));
            if(editorService)editorErrors++;else errors++;
            File.AppendAllText(Report,(editorService?"EDITOR SERVICE ERROR ":"ERROR ")+message+"\n"+trace+"\n");
        }
        static void Check(bool value,string message)
        {checks++;File.AppendAllText(Report,(value?"PASS ":"FAIL ")+message+"\n");if(!value)throw new InvalidOperationException(message);}
        static object Field(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        static void Sync(IslandLobbyView view){Call(view,"UpdateStations");view.Point(new Ray(new Vector3(0,20,-20),Vector3.up),false);Canvas.ForceUpdateCanvases();}
        static Button Button(IslandLobbyView view,string id)=>view.GetComponentsInChildren<Button>(true).Single(b=>b.name==id);
        static bool Shown(IslandLobbyView view,string id)=>Button(view,id).gameObject.activeInHierarchy;
        static string Page(IslandLobbyView view)=>(string)Field(view,"setupPage");
        static void Layout(IslandLobbyView view,string page)
        {
            var choices=((IEnumerable)Field(view,"choices")).Cast<object>().Where(c=>(bool)Field(c,"candy")).ToArray();
            foreach(var c in choices)
            {
                var b=(Button)Field(c,"button");if(!b.gameObject.activeInHierarchy)continue;
                var rect=(RectTransform)b.transform;var hit=(BoxCollider)Field(c,"collider");var text=(Text)Field(c,"label");
                Check(Mathf.Abs(hit.size.x-rect.rect.width)<.1f&&Mathf.Abs(hit.size.y-rect.rect.height)<.1f,"Visual/hit target agree: "+page+" / "+b.name);
                Check(text.font&&text.resizeTextMinSize>=20,"Readable font minimum: "+page+" / "+b.name);
                var content=b.transform;
                while(content.parent&&content.name!="Candy Arcade content")content=content.parent;
                var corners=new Vector3[4];rect.GetWorldCorners(corners);
                Check(corners.All(p=>{var local=content.InverseTransformPoint(p);return Mathf.Abs(local.x)<=551&&Mathf.Abs(local.y)<=441;}),"Button contained by screen: "+page+" / "+b.name);
            }
            var shown=choices.Select(c=>(Button)Field(c,"button")).Where(b=>b.gameObject.activeInHierarchy).ToArray();
            for(int i=0;i<shown.Length;i++)for(int j=i+1;j<shown.Length;j++)
            {
                if(shown[i].transform.parent!=shown[j].transform.parent)continue;
                string a=shown[i].name,b=shown[j].name;
                if(a.StartsWith("mode-card-")&&b.StartsWith("mode-card-")&&
                    (shown[i].transform.localPosition.z<0||shown[j].transform.localPosition.z<0))continue;
                if((a=="player-target"&&b.StartsWith("population-"))||(b=="player-target"&&a.StartsWith("population-")))continue;
                var x=(RectTransform)shown[i].transform;var y=(RectTransform)shown[j].transform;
                var rx=new Rect(x.anchoredPosition-x.rect.size*.5f,x.rect.size);var ry=new Rect(y.anchoredPosition-y.rect.size*.5f,y.rect.size);
                Check(!rx.Overlaps(ry),"No overlapping buttons: "+page+" / "+a+" / "+b);
            }
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float until=Time.realtimeSinceStartup+90;
            while(!IslandLobby.Current&&Time.realtimeSinceStartup<until)yield return null;
            var lobby=IslandLobby.Current;Check(lobby,"Actual lobby starts");var view=lobby.View();Check(view,"Actual lobby presentation builds");
            lobby.Player.InputSuspended=true;lobby.Player.Place(new Vector3(0,.1f,-8),Quaternion.identity);
            if(Services.Session.SurvivorBots!=null){Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Clear();}Services.Session.HandBots?.Clear();
            if(Services.Session.Social!=null)Services.Session.Social.VoiceEnabled=false;
            yield return null;Sync(view);
            if(SessionState.GetBool(Key+"CabinetsOnly",false))
            {
                foreach(string name in new[]{"Host controller computer","Mode controller computer"})
                {
                    var cabinet=view.transform.Find(name);Check(cabinet,"Controller cabinet exists: "+name);
                    var face=cabinet.Find("Vertical controller face");var panel=cabinet.Find("Flush control panel");
                    Check(Mathf.Abs(face.localPosition.y-panel.localPosition.y)<.001f,"Controls centered vertically on panel: "+name);
                    Check(face.localRotation==Quaternion.identity,"Controls parallel to terminal wall: "+name);
                    Check(face.GetComponentsInChildren<TextMesh>().Length==0,"No floating text legends: "+name);
                }
                var modeFace=view.transform.Find("Mode controller computer/Vertical controller face");
                Check(modeFace.Find("Xbox style thumbstick/Decorative concave thumbstick cap"),"Mode cabinet uses an actual concave thumbstick");
                foreach(char letter in "ABXY")
                {
                    var button=modeFace.Find("Decorative "+letter+" button assembly");
                    var legend=button.Find("Decorative "+letter+" fitted legend");
                    var bounds=legend.GetComponent<MeshFilter>().sharedMesh.bounds;
                    Check(bounds.extents.x<.045f&&bounds.extents.y<.045f,"Legend fits button cap: "+letter);
                    Check(Mathf.Abs(bounds.center.x)<.004f&&Mathf.Abs(bounds.center.y)<.004f,"Legend centered on cap: "+letter);
                }
                Check(modeFace.Find("Decorative Y button assembly").localPosition.y>modeFace.Find("Decorative A button assembly").localPosition.y,"Y above A");
                Check(modeFace.Find("Decorative X button assembly").localPosition.x<modeFace.Find("Decorative B button assembly").localPosition.x,"X left of B");
                Capture(view,"HOST","Actual-Host-Offline.png");Capture(view,"MAP + MODE","Actual-Setup.png");
                Capture(view,"HOST","Actual-Host-Controls.png");Capture(view,"MAP + MODE","Actual-Mode-Controls.png");
                SessionState.SetBool(Key+"CabinetsOnly",false);Check(errors==0,"Controller cabinet captures have no runtime errors");yield break;
            }
            Check(view.transform.Find("JOIN/Candy Arcade content")&&view.transform.Find("HOST/Candy Arcade content")&&view.transform.Find("MAP + MODE/Candy Arcade content"),"All three terminals use approved design");
            var wardrobe=view.transform.Find("WARDROBE");Check(wardrobe&&!wardrobe.Find("Candy Arcade content"),"Existing wardrobe kept separate");
            Layout(view,"main");
            Capture(view,"JOIN","Actual-Join.png");Capture(view,"HOST","Actual-Host-Offline.png");Capture(view,"MAP + MODE","Actual-Setup.png");
            lobby.EnteredCode="";Check(view.InvokeChoice("key-A")&&view.InvokeChoice("key-1")&&view.InvokeChoice("key--")&&view.InvokeChoice("key-B")&&lobby.EnteredCode=="A1-B","Actual numeric/letter/hyphen keyboard routes through lobby");
            Check(view.InvokeChoice("backspace")&&lobby.EnteredCode=="A1-","Actual backspace works");
            Check(view.InvokeChoice("clear-code")&&lobby.EnteredCode=="","Actual clear works");
            Check(!view.InvokeChoice("keyboard-enter")&&!view.InvokeChoice("join"),"Invalid code cannot trigger join");
            foreach(string code in new[]{"US-ABC234","EU-ABC234XYZ"})
            {
                lobby.EnteredCode=code;Sync(view);
                var slots=(List<RectTransform>)Field(view,"candyCodeSlots");var letters=(List<Text>)Field(view,"candyCodeLetters");
                Check(slots.Count(s=>s.gameObject.activeSelf)==code.Length&&string.Concat(letters.Take(code.Length).Select(t=>t.text))==code,"Region-prefixed code renders without truncation: "+code);
            }
            Capture(view,"JOIN","Actual-Join-Code.png");lobby.EnteredCode="";lobby.UnfocusCode();Sync(view);
            Check(view.InvokeChoice("mode-current")&&Page(view)=="modes","Mode picker opens");Sync(view);Layout(view,"modes");
            bool viewWasEnabled=view.enabled;view.enabled=false;
            float focusUntil=Time.realtimeSinceStartup+.3f;
            while(Time.realtimeSinceStartup<focusUntil){yield return null;Sync(view);}
            var modeImages=(RawImage[])Field(view,"modePictures");
            foreach(var picture in modeImages.Where(p=>p.texture&&p.gameObject.activeInHierarchy))
            {
                Check(picture.rectTransform.sizeDelta==new Vector2(118,118),"Mode picture uses larger square framing");
                Check(picture.uvRect==new Rect(0,0,1,1),"Mode artwork remains uncropped");
            }
            string selectedMode=((ILobbyDetails)lobby).SelectedModeId;
            int selectedSlot=Enumerable.Range(0,8).First(i=>Call(view,"ModeCardOption",i) is LobbyOption option&&option.id==selectedMode);
            Check(Button(view,"mode-card-"+selectedSlot).transform.localScale.x>1.3f,"Selected mode enlarges the whole image/text/card");
            Capture(view,"MAP + MODE","Actual-Modes.png");
            int hoverSlot=Enumerable.Range(0,8).First(i=>Call(view,"ModeCardOption",i) is LobbyOption option&&option.id!=selectedMode);
            var hoverCard=Button(view,"mode-card-"+hoverSlot).transform;
            focusUntil=Time.realtimeSinceStartup+.3f;
            while(Time.realtimeSinceStartup<focusUntil)
            {yield return null;view.Point(new Ray(hoverCard.position-hoverCard.forward,hoverCard.forward),false);}
            Check(hoverCard.localScale.x>1.3f,"Pointer hover enlarges the full mode card");
            Check(((ILobbyDetails)lobby).SelectedModeId==selectedMode,"Hover enlargement does not select a mode");
            var focusCorners=new Vector3[4];((RectTransform)hoverCard).GetWorldCorners(focusCorners);
            Check(focusCorners.All(p=>{var local=hoverCard.parent.InverseTransformPoint(p);return Mathf.Abs(local.x)<=550&&Mathf.Abs(local.y)<=440;}),"Enlarged mode stays within its terminal");
            Capture(view,"MAP + MODE","Actual-Modes-Hover.png");
            view.enabled=viewWasEnabled;
            if(SessionState.GetBool(Key+"ModesOnly",false))
            {SessionState.SetBool(Key+"ModesOnly",false);Check(errors==0,"Mode captures have no runtime errors");yield break;}

            int card=Enumerable.Range(0,8).First(i=>Call(view,"ModeCardOption",i)!=null);
            Check(view.InvokeChoice("mode-card-"+card)&&Page(view)=="modes","Mode selection stays visible for confirmation");Sync(view);
            Check(view.InvokeChoice("mode-confirm")&&Page(view)=="maps","Mode confirmation advances to map picker");Sync(view);Layout(view,"maps");
            var largePreview=(RawImage)Field(view,"browserMapPreview");
            Check(largePreview.gameObject.activeInHierarchy&&largePreview.enabled&&largePreview.texture,"Large map preview appears above names");
            var first=(RectTransform)Button(view,"picker-0").transform;
            var secondColumn=(RectTransform)Button(view,"picker-5").transform;
            Check(first.anchoredPosition.x<0&&secondColumn.anchoredPosition.x>0&&Mathf.Approximately(first.anchoredPosition.y,secondColumn.anchoredPosition.y),"Map names occupy two aligned columns");
            int categorySlot=Enumerable.Range(0,10).First(i=>((string)Call(view,"PickerTitle",i)).Contains("Game-mode"));
            Check(view.InvokeChoice("picker-"+categorySlot),"Map category expands");Sync(view);Layout(view,"expanded maps");
            string selectedMap=((ILobbyDetails)lobby).SelectedMapId;
            int previewSlot=Enumerable.Range(0,10).First(i=>Call(view,"PickerOption",i) is LobbyOption map&&map.id!=selectedMap);
            var previewOption=(LobbyOption)Call(view,"PickerOption",previewSlot);
            var previewButton=Button(view,"picker-"+previewSlot).transform;
            view.Point(new Ray(previewButton.position-previewButton.forward,previewButton.forward),false);
            Check((string)Field(view,"browserMapId")==previewOption.id,"World-space cursor changes the large preview without clicking");
            Check(Page(view)=="maps"&&((ILobbyDetails)lobby).SelectedMapId==selectedMap,"Hover leaves the browser open and preserves selected map");
            Check(largePreview.uvRect==new Rect(0,0,1,1),"Preview shows the full screenshot without cropping");
            Capture(view,"MAP + MODE","Actual-Maps.png");
            Check(view.InvokeChoice("setup-back")&&Page(view)=="main","Back returns to setup");
            Check(view.InvokeChoice("mode-settings")&&Page(view)=="settings","All existing settings remain accessible");Sync(view);Layout(view,"settings");Capture(view,"MAP + MODE","Actual-Settings.png");
            var details=(ILobbyDetails)lobby;var saved=JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(details.Setup));
            int before=details.Setup.population;Check(view.InvokeChoice("setting-plus-0"),"Settings adjustment action works");
            Check(details.Setup.population==Mathf.Min(100,before+1),"Settings command reaches actual lobby setup");details.ConfigureSetup(saved);
            Check(view.InvokeChoice("setup-back"),"Settings back works");Sync(view);
            Fixture(lobby,view,details);
            lobby.SelectMode("demo");lobby.SelectMap("castle");var setup=details.Setup;setup.population=1;setup.hands=0;details.ConfigureSetup(setup);
            Sync(view);Check(lobby.CanStart&&view.InvokeChoice("launch"),"Actual Start Game command accepted");
            until=Time.realtimeSinceStartup+90;
            while((Services.Session?.Demo==null||Services.World==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(Services.Session?.Demo!=null&&Services.World!=null&&NetworkActors.Alive(Services.Session.Player),"Actual Game scene enters solo from new terminal");
            float settle=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<settle)yield return null;
            Check(errors==0,"No runtime errors");
        }
        static void Fixture(IslandLobby source,IslandLobbyView original,ILobbyDetails details)
        {
            File.AppendAllText(Report,"STAGED PRESENTATION FIXTURES: connected host, guest, host requests, nearby rooms. No Photon commands sent.\n");
            var root=new GameObject("Candy terminal presentation fixture");var view=root.AddComponent<IslandLobbyView>();view.enabled=false;
            var controls=new FixtureLobby(source,details);
            Set(view,"session",Services.Session);Set(view,"lobby",controls);Set(view,"localArt",Object.Instantiate((ScriptableObject)Field(original,"localArt")));
            try
            {
                Call(view,"BuildTerminals");Call(view,"BuildCandyDesign");Sync(view);
                Check(view.InvokeChoice("host")&&controls.Connected,"Create room command routed to presentation stand-in");Sync(view);Layout(view,"connected");
                Check(!Shown(view,"host")&&Shown(view,"invite-all")&&Shown(view,"abandon-host"),"Connected host actions replace offline help");
                Capture(view,"HOST","Staged-Host-Connected.png");
                controls.host=false;Sync(view);
                Check(Shown(view,"request-host")&&!Shown(view,"shared-controls")&&!view.InvokeChoice("launch"),"Guest can request host and cannot launch");
                Check(view.InvokeChoice("request-host")&&controls.requestsSent==1,"Host request command routed");
                Check(((LobbyTerminalFaces)Field(view,"setupFace")).Opacity==1,"Guest setup remains readable");
                Capture(view,"HOST","Staged-Host-Guest.png");
                controls.host=true;controls.requests.Add("friend");Sync(view);Layout(view,"host-request");
                Check(Shown(view,"request-accept")&&Shown(view,"request-deny"),"Incoming host request actions available");
                Capture(view,"HOST","Staged-Host-Request.png");
                Check(view.InvokeChoice("request-deny")&&controls.reviews==1,"Review request command routed");
                controls.connected=false;controls.host=false;controls.nearby=true;Sync(view);Layout(view,"discovery");
                Capture(view,"JOIN","Staged-Join-Nearby.png");
                var target=Button(view,"nearby-room-0").GetComponent<BoxCollider>();view.Point(new Ray(target.transform.position-target.transform.forward,target.transform.forward),true);
                Check(controls.joinedSlot==0,"World-space pointing hits and invokes nearby room");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void Capture(IslandLobbyView view,string terminal,string name)
        {
            var screen=view.transform.Find(terminal);Canvas.ForceUpdateCanvases();
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);var enabled=canvases.Select(c=>c.enabled).ToArray();
            var go=new GameObject("Candy terminal offscreen camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Services.Session.ActiveCamera);
            camera.enabled=false;camera.fieldOfView=52;camera.rect=new Rect(0,0,1,1);camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.transform.position=screen.position-screen.forward*3.25f;camera.transform.LookAt(screen.position-Vector3.up*.22f);
            if(name.Contains("Controls"))
            {
                var face=view.transform.Find((terminal=="HOST"?"Host":"Mode")+" controller computer/Vertical controller face");
                camera.transform.position=face.position-screen.forward*1.75f+Vector3.up*.30f;camera.transform.LookAt(face.position);
            }
            var rt=RenderTexture.GetTemporary(1600,1100,24);var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                foreach(var c in canvases)c.enabled=c.transform==screen||c.transform.IsChildOf(screen)||terminal=="JOIN"&&c.name=="Giant piano keyboard"&&c.transform.IsChildOf(view.transform);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());
            }
            finally{for(int i=0;i<canvases.Length;i++)if(canvases[i])canvases[i].enabled=enabled[i];RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
        }
        sealed class FixtureLobby : ILobbyControls,ILobbyDetails
        {
            readonly ILobbyControls source;public bool connected,host,nearby;public int requestsSent,reviews,joinedSlot=-1;
            public readonly List<string> requests=new List<string>();
            readonly List<LobbyParticipant> participants=new List<LobbyParticipant>{new LobbyParticipant{id="local",displayName="Evan",host=true},new LobbyParticipant{id="friend",displayName="Riley"}};
            string selectedMap,selectedMode;bool focused,shared;string code="";
            public FixtureLobby(ILobbyControls original,ILobbyDetails details){source=original;Maps=details.Maps;Modes=details.Modes;selectedMap=details.SelectedMapId;selectedMode=details.SelectedModeId;Setup=JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(details.Setup));}
            public MonoBehaviour EnvironmentPrefab=>source.EnvironmentPrefab;public ScriptableObject Art=>source.Art;
            public void SetFloor(Mesh mesh,Transform pose){}public void AddBlocker(Transform pose,Vector3 center,Vector3 size){}
            public bool JoinEnabled=>!connected;public bool ShowingNearby=>nearby;public bool CodeFocused=>focused;
            public string EnteredCode{get=>code;set=>code=value??"";}public bool CodeValid=>code.Length>=6;
            public void FocusCode(){focused=true;nearby=false;}public void UnfocusCode()=>focused=false;
            public void TypeCode(char c){if(code.Length<12)code+=char.ToUpperInvariant(c);}public void EraseCode(){if(code.Length>0)code=code.Substring(0,code.Length-1);}
            public void Join()=>connected=true;public void DetectNearby()=>nearby=true;public void ShowCodeEntry()=>nearby=false;
            public string NearbyTitle(int slot)=>slot<3?new[]{"Riley's island / 2 players\nJOIN US-ABC234","Sam's island / 3 players\nJOIN US-XYZ567","Kai's island / 1 player\nJOIN EU-ABC789"}[slot]:"";
            public bool HasNearby(int slot)=>nearby&&slot<3;public void JoinNearby(int slot)=>joinedSlot=slot;public bool HasMoreNearby=>nearby;public void NextNearbyPage(){}
            public string InvitationFrom=>null;public void AcceptInvitation()=>connected=true;public bool CanLeave=>connected;public bool Busy=>false;public void Leave(){connected=host=false;}
            public string JoinHint=>"Choose an island or enter a room code.";public bool HostActive=>connected&&host;public bool CanHost=>!connected;
            public void Host(){connected=host=true;}public string HostCode=>connected?"US-ABC234":"PRIVATE ISLAND";public string Region=>"us";public void NextRegion(){}
            public bool CanInvite=>true;public void InviteNearby(){}public bool DemoSelected=>selectedMode=="demo";public bool SharedControls=>shared;public void ToggleSharedControls()=>shared=!shared;
            public string HostHint=>connected?"Share your room code.":"Create a room or invite nearby.";
            public bool IsHost=>!connected||host;public bool Connected=>connected;public bool CanConfigure=>!connected||host;
            public string MapTitle=>Maps.First(m=>m.id==selectedMap).title;public string ModeTitle=>Modes.First(m=>m.id==selectedMode).title;
            public void CycleMap(int step){}public void CycleMode(int step){}
            public bool SelectMap(string id){if(!CanConfigure||!Maps.Any(m=>m.id==id))return false;selectedMap=id;return true;}
            public bool SelectMode(string id){if(!CanConfigure||!Modes.Any(m=>m.id==id))return false;selectedMode=id;return true;}
            public bool CanStart=>CanConfigure;public bool StartPending=>false;public void StartOrCancel(){}public string SetupHint=>"Ready when you are.";
            public IReadOnlyList<LobbyParticipant> Participants=>participants;public IReadOnlyList<string> HostRequests=>requests;
            public IReadOnlyList<LobbyOption> Maps{get;}public IReadOnlyList<LobbyOption> Modes{get;}
            public string LocalParticipantId=>"local";public string SelectedMapId=>selectedMap;public string SelectedModeId=>selectedMode;
            public string Notification=>"";public int ConnectedCount=>connected?2:0;public Transform ParticipantRoot(string id)=>null;
            public void RequestHost()=>requestsSent++;public void ReviewHostRequest(string id,bool accept){reviews++;requests.Remove(id);}
            public LobbySetup Setup{get;private set;}public void ConfigureSetup(LobbySetup setup)=>Setup=setup;
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static partial class DeveloperUiChecks
    {
        const string Key="DeveloperUiChecks",NameKey="DeadlyJenga.PlayerName.v1";
        static readonly string[] ComfortKeys = { "SurvivorVR.InvertLookY", "SurvivorVR.Vignette", "SurvivorVR.Haptics", "SurvivorVR.HeadBob" };
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static string Output=>Path.GetDirectoryName(ProjectPath.Report("DeveloperUi/report.txt"));
        static string Report=>Path.Combine(Output,"report.txt");
        static double started;
        static int errors;
        static IEnumerator run;
        [Serializable] sealed class Saved {public SceneSetup[] scenes;}
        static DeveloperUiChecks(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;}
        public static void Run()=>Begin(false);
        public static void RunDialogs()=>Begin(true);
        public static void RunTabletTabs()=>Begin(false,true);
        public static void RunLobbyTerminals()=>Begin(false,false,true);
        public static void RunMatchVisuals()=>Begin(false,false,true,true);
        static void Begin(bool dialogs,bool tabs=false,bool lobby=false,bool matchVisuals=false)
        {
            SessionState.SetBool(Key+"MatchVisuals",matchVisuals);
            SessionState.SetBool(Key+"LobbyTerminals",lobby);
            SessionState.SetBool(Key+"Dialogs",dialogs);
            SessionState.SetBool(Key+"Tabs",tabs);
            if(!Application.isBatchMode)throw new Exception("Use an isolated checked batch");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||EditorSceneManager.GetSceneByPath(s.path).isDirty)))throw new Exception("Unsaved scenes preserved");
            if(!ValidationRunGate.TryAcquire(Key))throw new Exception("Editor already has a validation owner");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Report,"Actual Game scene and production menu, scripted pointer/binding checks. Capture camera temporarily frames the world-locked tablet for readability; these are not headset eye-placement measurements. No physical headset or multiplayer proof.\n");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));
            foreach (string pref in ComfortKeys)
            {
                SessionState.SetBool(Key + pref + "Exists", PlayerPrefs.HasKey(pref));
                SessionState.SetInt(Key + pref, PlayerPrefs.GetInt(pref));
            }
            SessionState.SetBool(Key+"HadName",PlayerPrefs.HasKey(NameKey));
            SessionState.SetString(Key+"Name",PlayerPrefs.GetString(NameKey,""));
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",2);
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"FromLobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find(lobby ? "Lobby.unity" : "Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){started=EditorApplication.timeSinceStartup;errors=0;Application.logMessageReceived+=Log;}
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            Application.logMessageReceived-=Log;
            MatchSetup.FromLobby=SessionState.GetBool(Key+"FromLobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
            SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
            if(SessionState.GetBool(Key+"HadName",false))PlayerPrefs.SetString(NameKey,SessionState.GetString(Key+"Name",""));else PlayerPrefs.DeleteKey(NameKey);
            foreach (string pref in ComfortKeys)
                if (SessionState.GetBool(Key + pref + "Exists", false)) PlayerPrefs.SetInt(pref, SessionState.GetInt(Key + pref, 0));
                else PlayerPrefs.DeleteKey(pref);
            PlayerPrefs.Save();
            try
            {
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes","")).scenes;
                if(saved.Any(s=>s.isLoaded)&&saved.Count(s=>s.isActive)==1)EditorSceneManager.RestoreSceneManagerSetup(saved);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                File.AppendAllText(Report,"CLEANUP restored name preference and initial scene state.\n");
            }
            catch(Exception e){File.AppendAllText(Report,"CLEANUP FAIL "+e+"\n");SessionState.SetInt(Key+"Exit",2);}
            finally{SessionState.SetBool(Key,false);ValidationRunGate.Release(Key);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",2));}
        }
        static void Log(string text,string stack,LogType type){if(type is LogType.Error or LogType.Exception or LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+text+"\n"+stack+"\n");}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||started==0||EditorApplication.timeSinceStartup-started<8)return;
            if(!SessionState.GetBool(Key+"LobbyTerminals",false)&&!(Services.Session is CastlePlaySession ready&&ready.Modes&&Services.World!=null)&&EditorApplication.timeSinceStartup-started<60)return;
            started=0;
            run=SessionState.GetBool(Key+"MatchVisuals",false)?MatchVisualChecks():SessionState.GetBool(Key+"LobbyTerminals",false)?LobbyTerminalChecks():Checks();
            var driver=new GameObject("Developer UI check driver");Object.DontDestroyOnLoad(driver);driver.AddComponent<TidalCheckDriver>().StartCoroutine(Drive());
        }
        static IEnumerator Drive()
        {
            bool failed=false;
            while(run!=null)
            {
                object step;
                try{if(!run.MoveNext())break;step=run.Current;}
                catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                yield return step;
            }
            (run as IDisposable)?.Dispose();run=null;
            if(!failed&&errors==0){SessionState.SetInt(Key+"Exit",0);File.AppendAllText(Report,"COMPLETE UI assertions passed with no runtime errors.\n");}
            else File.AppendAllText(Report,"FAILED runtime errors="+errors+"\n");
            EditorApplication.ExitPlaymode();
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Flags).GetValue(obj);
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        static void Click(Transform root,string text)
        {
            var button=root.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text==text);
            Check(button.interactable,"Button available: "+text);button.onClick.Invoke();
        }
        static void TabletClick(CastleWorldMenu menu,string prefix)
        {
            Call(menu,"PointPlayerTablet",Camera.main,false,false);
            var entries=((IEnumerable)Field<object>(menu,"tabletActions")).Cast<object>();
            var entry=entries.First(e=>Field<Button>(e,"Button").gameObject.activeInHierarchy&&Field<Func<string>>(e,"Title")().StartsWith(prefix,StringComparison.Ordinal));
            Check(Field<Func<bool>>(entry,"Allowed")?.Invoke()!=false,"Tablet command available: "+prefix);
            Field<Action>(entry,"Select")();Call(menu,"RefreshTabletNearby");Call(menu,"PointPlayerTablet",Camera.main,false,false);
        }
        static IEnumerator Checks()
        {
            var session=Services.Session as CastlePlaySession;
            Check(session&&session.Modes&&Services.World!=null,"Actual Game initializes");
            session.SurvivorBots.AutoFill=false;
            GameExperiencePanel.Resolve(session)?.SetExperience(GameExperience.Demo);
            session.Modes.Configure(ModeDefaults.Create(GameModeKind.Demo));
            var camera=session.ActiveCamera;Check(camera,"Session camera available");
            var presentation=session.GetComponent<SessionPresentation>();Check(presentation,"Production session presentation exists");
            session.SetMenu(true);yield return null;
            var menu=Field<CastleWorldMenu>(presentation,"worldMenu");Check(menu,"Use the actual game tablet, not an overlapping duplicate");
            typeof(SessionPresentation).GetField("inputFocused",Flags).SetValue(presentation,false);
            menu.Show(true,camera);
            var pageType=typeof(CastleWorldMenu).GetNestedType("TabletPage",BindingFlags.NonPublic);
            Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Developer"));
            void Draw(){Call(menu,"PointPlayerTablet",camera,false,false);Canvas.ForceUpdateCanvases();Physics.SyncTransforms();}
            void Back(){Call(menu,"OnDeveloperBackClicked");Draw();}
            void Row(string prefix)
            {
                Draw();var labels=Field<Text[]>(menu,"labels");var targets=Field<BoxCollider[]>(menu,"targets");
                int index=Array.FindIndex(labels,x=>x.gameObject.activeInHierarchy&&x.text==prefix);
                if(index<0)index=Array.FindIndex(labels,x=>x.gameObject.activeInHierarchy&&x.text.Contains(prefix));
                if(index<0&&Field<bool>(menu,"developerMapSearchOpen"))
                {
                    var key=((IEnumerable)Field<object>(menu,"developerSearchKeys")).Cast<object>().Single(e=>Field<Func<string>>(e,"Title")()==prefix);
                    var hit=Field<BoxCollider>(key,"Target");
                    typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(camera.transform.position,hit.bounds.center-camera.transform.position));
                    Call(menu,"PointPlayerTablet",camera,true,false);Draw();return;
                }
                Check(index>=0&&targets[index].enabled,"Row available: "+prefix);
                var target=targets[index];
                typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(camera.transform.position,target.bounds.center-camera.transform.position));
                Call(menu,"PointPlayerTablet",camera,true,false);Draw();
            }
            IEnumerator Shot(string file)
            {
                Draw();yield return null;Draw();yield return null;
                Check(Object.FindObjectsByType<CastleWorldMenu>(FindObjectsSortMode.None).Count(x=>x.gameObject.activeInHierarchy)==1,"Exactly one rendered tablet: "+file);
                Capture(camera,menu.GetComponent<Canvas>(),file);
            }
            if(SessionState.GetBool(Key+"Tabs",false))
            {
                Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Comfort")); Draw();
                var comfort = Field<RectTransform>(menu, "comfortMainGroup");
                var movementIcons = comfort.GetComponentsInChildren<RawImage>().Where(x => x.name.StartsWith("Movement icon ")).ToArray();
                Check(movementIcons.Length == 9 && movementIcons.All(x => x.texture && !x.raycastTarget), "Nine loaded movement icons do not intercept input");
                Check(movementIcons.All(x => x.texture.width >= 1024), "Movement atlas retains high-resolution source");
                string[] toggles = { "Invert stick", "Vignette", "Haptics", "Head bob" };
                for (int i = 0; i < toggles.Length; i++)
                {
                    var button = comfort.GetComponentsInChildren<Button>().Single(x => x.name == toggles[i] + " toggle");
                    var entry = ((IEnumerable)Field<object>(menu, "tabletActions")).Cast<object>().Single(e => Field<Button>(e,"Button") == button);
                    bool initial = Field<Func<bool>>(entry, "Selected")();
                    try
                    {
                        for (int click = 0; click < 2; click++)
                        {
                            var target = Field<BoxCollider>(entry,"Target");
                            typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(camera.transform.position,target.bounds.center-camera.transform.position));
                            Call(menu,"PointPlayerTablet",camera,true,false); Draw();
                            bool expected = click == 0 ? !initial : initial;
                            Check(Field<Func<bool>>(entry,"Selected")() == expected, toggles[i] + " pointer toggles state " + click);
                            Check(PlayerPrefs.GetInt(ComfortKeys[i]) == (expected ? 1 : 0), toggles[i] + " persists state " + click);
                            Check(Field<Text>(entry,"Label").text == (expected ? "✓ On" : "Off"), toggles[i] + " shows current state " + click);
                        }
                    }
                    finally
                    {
                        if (SessionState.GetBool(Key + ComfortKeys[i] + "Exists", false)) PlayerPrefs.SetInt(ComfortKeys[i], SessionState.GetInt(Key + ComfortKeys[i],0));
                        else PlayerPrefs.DeleteKey(ComfortKeys[i]);
                        PlayerPrefs.Save();
                    }
                }
                typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(camera.transform.position, -camera.transform.forward));
                yield return Shot("35-movement-settings.png");
                Call(menu,"OpenComfortSlider");yield return Shot("36-turn-tuning.png");Call(menu,"CloseComfortSlider");
                Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Room"));yield return Shot("37-room.png");
                Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Developer"));yield return Shot("38-developer-alignment.png");
                typeof(SessionPresentation).GetField("inputFocused",Flags).SetValue(presentation,true);session.SetMenu(false);yield break;
            }
            if(!SessionState.GetBool(Key+"Dialogs",false))
            {
            Draw();
            var grid=Field<RectTransform>(menu,"tabletDeveloperGrid");
            var tiles=grid.GetComponentsInChildren<Button>();
            Check(tiles.Length==12,"Developer has exactly twelve tiles");
            Check(tiles.Select(x=>((RectTransform)x.transform).anchoredPosition.x).Distinct().Count()==3,"Three columns");
            Check(tiles.Select(x=>((RectTransform)x.transform).anchoredPosition.y).Distinct().Count()==4,"Four rows");
            Check(tiles.Count(x=>!x.interactable)==2,"Exactly two coming-soon tiles disabled in local demo");
            yield return Shot("01-developer-root.png");
            var toolsTile=((IEnumerable)Field<object>(menu,"tabletActions")).Cast<object>().Single(e=>Field<Func<string>>(e,"Title")()=="More tools");
            var toolsTarget=Field<BoxCollider>(toolsTile,"Target");
            typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(camera.transform.position,toolsTarget.bounds.center-camera.transform.position));
            yield return Shot("23-more-tools-hover.png");
            Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Controls"));yield return Shot("22-controls.png");
            Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Developer"));
            TabletClick(menu,"Weapons");yield return Shot("21-weapons.png");
            foreach(var slot in ((IEnumerable)Field<object>(menu,"tabletWheelSlotButtons")).Cast<object>())
            {
                string title=Field<Func<string>>(slot,"Title")();string rendered=Field<Text>(slot,"Label").text;
                Check(rendered==title||rendered=="✓ "+title,"Weapon label matches command: "+title+" / "+rendered);
            }
            Back();
            TabletClick(menu,"Bots");yield return Shot("02-bots.png");Row("Population presets");yield return Shot("03-bot-presets.png");
            Row("Bot voices");yield return Shot("04-bot-voices.png");Back();
            Check(!Field<bool>(menu,"tabletDeveloperDetails"),"Bots Back returns to root");
            TabletClick(menu,"More tools");yield return Shot("05-more-tools.png");Row("World & pickups");yield return Shot("06-world-tools.png");
            Row("Ground pickups");Back();Check(Field<string>(menu,"developerRoute")=="More tools"&&!Field<bool>(menu,"developerChild"),"Child Back returns to owning tools page");
            Row("Automatic powerups");yield return Shot("17-automatic-powerups.png");Row("Show item weights");yield return Shot("18-powerup-weights.png");Back();Row("Player debug");Row("Host stamina tuning");yield return Shot("20-stamina.png");Back();Back();
            TabletClick(menu,"Map");yield return Shot("07-maps.png");Row("Filter:");yield return Shot("08-map-filters.png");Row("In development");yield return Shot("09-filtered-maps.png");
            Row("Search:");yield return Shot("19-map-search.png");Row("A");Row("A");Row("A");Row("Show results");
            Check(Field<Text[]>(menu,"labels").Any(x=>x.gameObject.activeInHierarchy&&x.text=="No available entries in this filter"),"Search has an explicit empty result state");
            Row("Search:");Row("Clear");Row("Show results");Back();
            Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Room"));Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Developer"));
            Check(Field<string>(menu,"developerRoute")=="","Top-level tabs clear stale developer route");
            TabletClick(menu,"Spectate");yield return Shot("16-spectate.png");Back();
            TabletClick(menu,"Mode");yield return Shot("10-mode.png");Row("advanced rules");yield return Shot("11-advanced-rules.png");Back();
            TabletClick(menu,"Disasters");yield return Shot("12-disasters.png");
            var card=((IEnumerable)Field<object>(menu,"disasterCards")).Cast<object>().First();
            Check(Field<Image>(card,"cardImage").material==Field<Text>(menu,"heading").material,"Lazy-created disaster cards share the foreground menu material");
            var cardTarget=Field<BoxCollider>(card,"cardTarget");
            typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(camera.transform.position,cardTarget.bounds.center-camera.transform.position));
            Call(menu,"PointPlayerTablet",camera,true,false);Draw();
            Check(Field<int>(menu,"disasterSelection")>=0&&!Field<GameObject>(menu,"disasterWheelUI").activeSelf,"Selecting a disaster hides its selector before showing settings");
            yield return Shot("13-disaster-selected.png");Row("Show tuning");yield return Shot("14-disaster-tuning.png");Back();Back();
            void OpenRoute(string route)
            {
                Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Developer"));TabletClick(menu,route);
            }
            // Capture each expanded tool family without running gameplay commands.
            foreach(string fold in new[]{"hands","behavior","equipment","voices"})
            {
                OpenRoute("Bots");typeof(CastleWorldMenu).GetField("developerFoldout",Flags).SetValue(menu,fold);
                yield return Shot("24-bots-"+fold+".png");
            }
            Call(menu,"DeveloperVolume","Bot volume",new Action<float>(_=>{}));yield return Shot("25-bot-volume.png");Back();
            OpenRoute("Bots");typeof(CastleWorldMenu).GetField("developerFoldout",Flags).SetValue(menu,"equipment");Draw();Row("Everyone loadout");
            for(int category=0;category<3;category++)
            {
                typeof(CastleWorldMenu).GetField("wheelCategory",Flags).SetValue(menu,category);
                typeof(CastleWorldMenu).GetField("equipmentOffset",Flags).SetValue(menu,0);
                yield return Shot("26-equipment-"+category+".png");
            }
            Back();Back();
            foreach(string fold in new[]{"player","world","round","diagnostics"})
            {
                OpenRoute("More tools");typeof(CastleWorldMenu).GetField("developerFoldout",Flags).SetValue(menu,fold);
                yield return Shot("27-tools-"+fold+".png");
            }
            OpenRoute("More tools");typeof(CastleWorldMenu).GetField("developerFoldout",Flags).SetValue(menu,"player");Draw();
            Row("Show hand physics");yield return Shot("28-hand-physics.png");Row("Hide hand physics");
            Row("Sea spirit / tabletop");yield return Shot("29-tabletop.png");Back();
            typeof(CastleWorldMenu).GetField("developerFoldout",Flags).SetValue(menu,"world");Draw();
            Row("Ground pickups");yield return Shot("30-ground-pickups.png");Back();
            Row("Wildlife & enemies");yield return Shot("31-wildlife.png");Back();Back();
            TabletClick(menu,"Disasters");
            for(int disaster=0;disaster<session.Demo.DisasterCount;disaster++)
            {
                typeof(CastleWorldMenu).GetField("disasterSelection",Flags).SetValue(menu,disaster);
                typeof(CastleWorldMenu).GetField("developerDisasterTuning",Flags).SetValue(menu,true);
                yield return Shot("32-disaster-"+disaster.ToString("00")+"-tuning.png");
            }
            Back();Back();
            }
            CharacterPicker.Open(session);yield return null;yield return null;
            var appearance=Object.FindFirstObjectByType<CharacterPicker>();Check(appearance,"Hand appearance panel opens");
            Capture(camera,Field<Canvas>(appearance,"panel"),"33-hand-appearance.png");Call(appearance,"Close",true);
            yield return null;
            GameExperiencePanel.Open(session);yield return null;
            var experience=session.GetComponent<GameExperiencePanel>();Check(experience,"Round setup panel opens");
            Capture(camera,Field<Canvas>(experience,"canvas"),"34-round-setup.png",Field<RectTransform>(experience,"panel"));Call(experience,"Close",true);
            yield return null;menu.Show(true,camera);Call(menu,"OpenPlayerTabletPage",Enum.Parse(pageType,"Developer"));Draw();
            var god=session.Player.gameObject.GetComponent<IDeveloperGodMode>();Check(god?.CanSetGodMode==true,"God Mode permitted in local Demo");
            TabletClick(menu,"God mode");Check(god.GodModeEnabled,"God Mode toggles on");
            var damage=session.Player.gameObject.GetComponent<IDamageable>();float health=damage.Health;
            damage.Damage(100000,menu.gameObject);damage.Eliminate(menu.gameObject);
            Check(damage.Health==health,"God Mode rejects direct damage and elimination");yield return Shot("15-god-mode.png");
            var survivor=(CastleSurvivor)session.Player;
            var oldPosition=survivor.transform.position;var oldRotation=survivor.transform.rotation;bool oldPause=survivor.InputSuspended;
            survivor.InputSuspended=false;survivor.Teleport(oldPosition+Vector3.up*100,oldRotation);
            Vector3 start=survivor.transform.position;
            Call(survivor,"MoveDeveloperFlight",Vector2.up,false,false,.05f);float slow=Vector3.Distance(start,survivor.transform.position);
            start=survivor.transform.position;Call(survivor,"MoveDeveloperFlight",Vector2.up,false,true,.05f);float fast=Vector3.Distance(start,survivor.transform.position);
            Check(slow>.01f&&fast>slow*5,"Actual character flight: fast travels over five times slow distance");
            start=survivor.transform.position;Call(survivor,"MoveDeveloperFlight",Vector2.zero,false,false,.05f);
            Check(Vector3.Distance(start,survivor.transform.position)<.001f,"Flight brakes when movement released");
            survivor.InputSuspended=true;start=survivor.transform.position;Call(survivor,"MoveDeveloperFlight",Vector2.up,false,true,.05f);
            Check(Vector3.Distance(start,survivor.transform.position)<.001f,"Menu input pause prevents flight movement");
            survivor.Teleport(oldPosition,oldRotation);survivor.InputSuspended=oldPause;
            TabletClick(menu,"God mode");Check(!god.GodModeEnabled,"God Mode toggles off");
            damage.Damage(5,menu.gameObject);Check(damage.Health<health,"Damage works again after disabling God Mode");survivor.Revive();
            god.SetGodMode(true);session.Modes.Configure(ModeDefaults.Create(GameModeKind.Survival));Call(survivor,"Update");
            Check(!god.CanSetGodMode&&!god.GodModeEnabled,"Leaving Demo revokes God Mode");
            session.Modes.Configure(ModeDefaults.Create(GameModeKind.Demo));Check(!god.GodModeEnabled,"Returning to Demo does not silently restore God Mode");
            typeof(SessionPresentation).GetField("inputFocused",Flags).SetValue(presentation,true);
            session.SetMenu(false);
        }
        static void Capture(Camera camera,Canvas canvas,string name,RectTransform framingRect=null)
        {
            var mode=canvas.renderMode;var worldCamera=canvas.worldCamera;var distance=canvas.planeDistance;
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            // Frame the real world-locked tablet independently of the player's settling/falling motion.
            var rect=framingRect?framingRect:(RectTransform)canvas.transform;
            float halfHeight=rect.rect.height*Mathf.Abs(rect.lossyScale.y)*.5f;
            float halfWidth=rect.rect.width*Mathf.Abs(rect.lossyScale.x)*.5f;
            float tangent=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float framing=Mathf.Max(halfHeight/(tangent*.82f),halfWidth/(tangent*1.6f*.82f));
            camera.transform.SetPositionAndRotation(rect.position-rect.forward*framing,rect.rotation);
            var target=new RenderTexture(1600,1000,24);var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);var previous=camera.targetTexture;var active=RenderTexture.active;
            try{if(mode==RenderMode.ScreenSpaceOverlay){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());}
            finally{camera.transform.SetPositionAndRotation(position,rotation);canvas.renderMode=mode;canvas.worldCamera=worldCamera;canvas.planeDistance=distance;camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
